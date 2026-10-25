"""Additive Production broker: bounded failure telemetry and synthetic-only archival.

The frozen 192 MiB executor checks, host admission lock, policy and protocol stay
in force. No native request is retried. Real player failures retain the original
eight-input fail-closed limit. Only exact synthetic canaries leave that spool.
"""
import argparse
from dataclasses import replace
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys
import threading
import time
import uuid

PREFIX = Path('/opt/witch-player-release/arena-resilience/20261025-v2')
OLD = Path('/opt/witch-player-release/request-logging/20261021-v1/production_arena_queued_broker.py')
OLD_SHA = '334aa672ad12641299fc49ef823ba96f694319432455fa478257b9a9489bfc39'
PROBE = Path('/etc/witch-player-arena/production-probe.json')
PROBE_SHA = '7430cb20a02345ee05f95d494cc29167ef05b6d6581da0c6e14d97ecc39ee02d'
ARCHIVE = Path('/opt/witch-player-arena/production/canary-failures-20261025')
MAX_ARCHIVES = 4096
MAX_INPUT = 4*1024*1024
STAGES = {'RuntimeOpen','OutputMustBeNew','NativeExecute','NativeExit','NativeTimeout',
          'OutputRead','Complete','Delivery','Retention'}
FLAGS = {'ManagedOutOfMemory','NativeOutOfMemory','ThreadCreationFailed','NativeCrash',
         'DiskFull','PermissionDenied','MissingDependency'}
ERRORS = {'ArgumentException','InvalidOperationException','InvalidDataException','IOException',
          'UnauthorizedAccessException','OutOfMemoryException','NullReferenceException',
          'OverflowException','FormatException'}


def require(value):
    if not value:
        raise ValueError('Pinned Production resilient broker rejected')


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()


def protected_directory(path):
    for node in (path, *path.parents):
        info = node.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)


def read(path, maximum, pin=None):
    protected_directory(path.parent)
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_uid == 0 and before.st_nlink == 1
            and not before.st_mode & 0o022 and 0 < before.st_size <= maximum)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    try:
        stamp = lambda s: (s.st_dev,s.st_ino,s.st_mode,s.st_uid,s.st_nlink,s.st_size,s.st_mtime_ns,s.st_ctime_ns)
        data = os.pread(fd, maximum+1, 0)
        require(len(data) == before.st_size and stamp(before) == stamp(os.fstat(fd)) == stamp(path.lstat())
                and (pin is None or hashlib.sha256(data).hexdigest() == pin))
        return data
    finally:
        os.close(fd)


def is_canary(value, template):
    try:
        if type(value) is not dict or set(value) != {'Version','Profile'} or type(value['Version']) is not int or value['Version'] != 1:
            return False
        selected = value['Profile']['PartyMonsterInstanceIds']
        original = template['Profile']['PartyMonsterInstanceIds']
        if type(selected) is not list or len(selected) != 2 or len(original) != 2:
            return False
        matches = [re.fullmatch(r'arena-probe-([a-f0-9]{32})-([01])', x) if type(x) is str else None for x in selected]
        if not all(matches) or matches[0][1] != matches[1][1] or [x[2] for x in matches] != ['0','1']:
            return False
        mapping = dict(zip(selected, original))
        def convert(node):
            if type(node) is dict:
                return {k: convert(v) for k,v in node.items()}
            if type(node) is list:
                return [convert(v) for v in node]
            return mapping.get(node, node) if type(node) is str else node
        return convert(value) == template
    except (ValueError,TypeError,KeyError):
        return False


def fsync_directory(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def archive_canaries(spool, template, archive=ARCHIVE):
    """Caller holds frozen host admission lock. Never delete, copy, or follow links."""
    protected_directory(spool)
    protected_directory(archive)
    require(spool.stat().st_dev == archive.stat().st_dev)
    count = len(list(archive.iterdir()))
    moved = 0
    for directory in list(spool.iterdir()):
        require(re.fullmatch(r'witch-weekly-job-[a-f0-9]{32}', directory.name))
        protected_directory(directory)
        names = {x.name for x in directory.iterdir()}
        require(names in ({'input.json'}, {'input.json','failure.json'}))
        raw = read(directory/'input.json', MAX_INPUT)
        if not is_canary(json.loads(raw), template):
            continue
        require(count+moved < MAX_ARCHIVES)
        require(os.statvfs(archive).f_bavail*os.statvfs(archive).f_frsize >= 128*1024*1024)
        if 'failure.json' in names:
            read(directory/'failure.json', 4096)
        destination = archive/directory.name
        require(not os.path.lexists(destination))
        digest = hashlib.sha256(raw).hexdigest()
        os.rename(directory, destination)
        require(hashlib.sha256(read(destination/'input.json', MAX_INPUT)).hexdigest() == digest)
        fsync_directory(archive)
        fsync_directory(spool)
        moved += 1
    if moved:
        emit(dict(Event='arena_synthetic_failures_archived', Count=moved))
    return moved


def diagnostic(value):
    require(type(value) is dict and set(value) == {'Stage','NativeExit','Flags','ManagedError',
            'ElapsedMs','MemoryPeakBytes','OomKills'} and value['Stage'] in STAGES)
    require(value['NativeExit'] is None or type(value['NativeExit']) is int and -128 <= value['NativeExit'] <= 255)
    require(type(value['Flags']) is list and len(value['Flags']) <= len(FLAGS)
            and all(type(x) is str and x in FLAGS for x in value['Flags'])
            and (value['ManagedError'] is None or value['ManagedError'] in ERRORS))
    for key in ('ElapsedMs','MemoryPeakBytes','OomKills'):
        require(value[key] is None and key != 'ElapsedMs' or type(value[key]) is int and 0 <= value[key] < 2**63)
    return value


def emit(value):
    sys.stderr.write(canonical(value).decode()+'\n')
    sys.stderr.flush()


class NativeProfile:
    def __call__(self, raw, operation):
        require(operation == 'CalculateProfile')
        context = self._resilience
        context['dispatch'].request_for(self.artifact_hash, operation, raw)
        self._verify()
        self._capacity()
        archive_canaries(self.spool, context['template'])
        retained = list(self.spool.iterdir())
        if len(retained) >= 8:
            raise RuntimeError('Real failure retention needs operator review')
        unit = 'witch-weekly-job-'+uuid.uuid4().hex
        directory = self.spool/unit
        directory.mkdir(mode=0o700, exist_ok=False)
        source = directory/'input.json'
        with source.open('xb') as stream:
            stream.write(raw.encode()); stream.flush(); os.fsync(stream.fileno())
        source.chmod(0o444)
        properties = context['profile'].profile192_properties(self.root, unit, source)
        bind = 'BindReadOnlyPaths='
        properties = [v+' '+str(PREFIX/'arena_diagnostic_job_bridge.py')+':/weekly-job-bridge.py'
                      if v.startswith(bind) else v for v in properties]
        command = ['/usr/bin/systemd-run','--quiet','--wait','--pipe','--collect','--service-type=exec','--unit='+unit]
        for value in properties:
            command += ['--property', value]
        command += ['/usr/bin/python3','-I','-B','/weekly-job-bridge.py','CalculateProfile',unit]
        detail = dict(Stage='Delivery',NativeExit=None,Flags=[],ManagedError=None,
                      ElapsedMs=0,MemoryPeakBytes=None,OomKills=None)
        try:
            process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL, env={'PATH':'/usr/bin:/bin','LANG':'C.UTF-8','LC_ALL':'C.UTF-8'})
            value = context['delivery'].collect(process)
            require(type(value) is dict and set(value) == {'Version','Success','Diagnostic','Result'}
                    and type(value['Version']) is int and value['Version'] == 1 and type(value['Success']) is bool)
            detail = diagnostic(value['Diagnostic'])
            emit(dict(Event='arena_native_diagnostic', Synthetic=is_canary(json.loads(raw), context['template']), **detail))
            require(value['Success'] and detail['Stage'] == 'Complete' and detail['NativeExit'] == 0
                    and type(value['Result']) is dict)
            self._verify()
        except BaseException:
            # Only this internally generated job. Keep its input and sanitized evidence.
            subprocess.run(['/usr/bin/systemctl','stop',unit+'.service'],stdin=subprocess.DEVNULL,
                           stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=10)
            failure = dict(Version=1,InputSha256=hashlib.sha256(raw.encode()).hexdigest(),
                           CheckedUnix=time.time(),Diagnostic=detail)
            with (directory/'failure.json').open('xb') as stream:
                stream.write(canonical(failure));stream.flush();os.fsync(stream.fileno())
            (directory/'failure.json').chmod(0o600)
            archive_canaries(self.spool, context['template'])
            raise
        self._cleanup_input(unit, directory)
        return value['Result']


def prepare(expected_manifest):
    require(os.geteuid() == 0 and Path(__file__).absolute() == PREFIX/Path(__file__).name)
    manifest_path = PREFIX/'SHA256.json'
    raw = read(manifest_path, 4096, expected_manifest)
    manifest = json.loads(raw)
    require(set(manifest) == {'production_arena_resilient_broker.py','arena_diagnostic_job_bridge.py'})
    def guard():
        read(manifest_path, 4096, expected_manifest)
        for name, pin in manifest.items():
            read(PREFIX/name, 65536, pin)
    guard()
    read(OLD, 65536, OLD_SHA)
    spec = importlib.util.spec_from_file_location('pinned_queued_broker_20261021', OLD)
    queued = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(queued)
    original = queued.prepare_queued_production_broker(OLD_SHA, queued.CONFIG_HASH)
    legacy = sys.modules['arena_profile_broker']
    profile = sys.modules['arena_profile_executor_192']
    dispatch = sys.modules['weekly_worker_dispatch']
    delivery = sys.modules['weekly_systemd_executor_v2']
    probe = json.loads(read(PROBE, 131072))
    require(hashlib.sha256(canonical(probe)).hexdigest() == PROBE_SHA)
    protected_directory(ARCHIVE)
    context = dict(template=probe['InputTemplate'],profile=profile,dispatch=dispatch,delivery=delivery)
    def intact():
        guard()
        original.assert_intact()
    intact()
    old = original.executor
    executor_type = queued.queued_executor_type(legacy, NativeProfile, dispatch.request_for)
    executor = executor_type(old.artifact_hash,old.root,old.files,old.spool,old.bridge_sha256)
    executor._arena_guard, executor._resilience = intact, context
    dispatcher = dispatch.WorkerDispatcher({old.artifact_hash:executor}, original.allowed_uid, cache_bytes=1)
    return replace(original,executor=executor,dispatcher=dispatcher,assert_intact=intact)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--expected-manifest-sha256',required=True)
    parser.add_argument('--serve',action='store_true')
    args = parser.parse_args()
    value = prepare(args.expected_manifest_sha256)
    if not args.serve:
        print('ARENA_RESILIENT_BROKER_PREPARED')
        return 0
    stop = threading.Event()
    for signum in (signal.SIGINT,signal.SIGTERM):
        signal.signal(signum,lambda *_: stop.set())
    from weekly_worker_dispatch import serve_private_listener
    return serve_private_listener(value.socket_path,value.dispatcher,value.socket_gid,stop=stop)


if __name__ == '__main__':
    sys.dont_write_bytecode = True
    raise SystemExit(main())
