"""Unprivileged fixed CalculateProfile bridge with bounded, nonpersonal diagnostics."""
import json
import os
from pathlib import Path
import re
import stat
import struct
import subprocess
import sys
import time

MAGIC = b'WWJOB2\x00\x01'
MAX_RESULT = 3*1024*1024
FLAGS = {
    'ManagedOutOfMemory': (b'OutOfMemoryException', b'Could not allocate memory'),
    'NativeOutOfMemory': (b'out of memory', b'Allocation failed', b'Could not allocate'),
    'ThreadCreationFailed': (b'pthread_create', b'Failed to create thread', b'Resource temporarily unavailable'),
    'NativeCrash': (b'SIGSEGV', b'SIGABRT', b'Native Crash Reporting'),
    'DiskFull': (b'No space left on device',),
    'PermissionDenied': (b'Permission denied', b'UnauthorizedAccessException'),
    'MissingDependency': (b'DllNotFoundException', b'cannot open shared object file'),
}
ERRORS = {'ArgumentException', 'InvalidOperationException', 'InvalidDataException',
          'IOException', 'UnauthorizedAccessException', 'OutOfMemoryException',
          'NullReferenceException', 'OverflowException', 'FormatException'}


def scan_log(raw):
    flags = [key for key, patterns in FLAGS.items() if any(pattern in raw for pattern in patterns)]
    match = re.search(rb'WEEKLY_STANDALONE_JOB_FAILED ([A-Za-z]+Exception)', raw)
    error = match[1].decode('ascii') if match and match[1].decode('ascii') in ERRORS else None
    return flags, error


def read_private(fd, name, limit):
    descriptor = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=fd)
    with os.fdopen(descriptor, 'rb') as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1 or info.st_uid != os.geteuid():
            raise ValueError()
        if info.st_size > limit:
            stream.seek(max(0, info.st_size-limit))
        return stream.read(limit)


def execute(unit):
    started = time.monotonic()
    diagnostic = dict(Stage='RuntimeOpen', NativeExit=None, Flags=[], ManagedError=None,
                      ElapsedMs=0, MemoryPeakBytes=None, OomKills=None)
    result = None
    fd = None
    try:
        if not re.fullmatch(r'witch-weekly-job-[a-f0-9]{32}', unit) or os.geteuid() == 0:
            raise ValueError()
        runtime = Path('/run')/unit
        fd = os.open(runtime, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        info = os.fstat(fd)
        if info.st_uid != os.geteuid() or info.st_mode & 0o077:
            raise ValueError()
        diagnostic['Stage'] = 'OutputMustBeNew'
        try:
            os.stat('output.json', dir_fd=fd, follow_symlinks=False)
        except FileNotFoundError:
            pass
        else:
            raise ValueError()
        diagnostic['Stage'] = 'NativeExecute'
        command = ['/worker/WeeklyReplayWorker.x86_64', '-batchmode', '-nographics',
                   '-job-worker-count', '1', '-logFile', str(runtime/'unity.log'),
                   '-weeklyOperation', 'CalculateProfile', '-weeklyReplayInput', '/weekly-input.json',
                   '-weeklyReplayOutput', str(runtime/'output.json')]
        environment = dict(PATH='/usr/bin:/bin', LANG='C.UTF-8', LC_ALL='C.UTF-8',
                           HOME=str(runtime), TMPDIR=str(runtime), XDG_CACHE_HOME=str(runtime))
        process = subprocess.run(command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                 stderr=subprocess.DEVNULL, cwd=runtime, env=environment, timeout=110)
        diagnostic['NativeExit'] = process.returncode
        try:
            diagnostic['Flags'], diagnostic['ManagedError'] = scan_log(read_private(fd, 'unity.log', 131072))
        except (OSError, ValueError):
            pass
        if process.returncode != 0:
            diagnostic['Stage'] = 'NativeExit'
        else:
            diagnostic['Stage'] = 'OutputRead'
            raw = read_private(fd, 'output.json', MAX_RESULT+1)
            if not 0 < len(raw) <= MAX_RESULT:
                raise ValueError()
            result = json.loads(raw)
            if type(result) is not dict:
                raise ValueError()
            diagnostic['Stage'] = 'Complete'
    except subprocess.TimeoutExpired:
        diagnostic['Stage'] = 'NativeTimeout'
    except (OSError, ValueError, TypeError):
        pass
    finally:
        if fd is not None:
            os.close(fd)
    # Cgroup namespace may be unavailable inside some systemd jails: null is honest.
    try:
        group = next(line[3:] for line in Path('/proc/self/cgroup').read_text().splitlines() if line.startswith('0::'))
        if group != '/' and not group.endswith('/'+unit+'.service'):
            raise ValueError()
        cgroup = Path('/sys/fs/cgroup')/group.lstrip('/')
        diagnostic['MemoryPeakBytes'] = int((cgroup/'memory.peak').read_text())
        events = dict(line.split() for line in (cgroup/'memory.events').read_text().splitlines())
        diagnostic['OomKills'] = int(events['oom_kill'])
    except (OSError, ValueError, KeyError, StopIteration):
        pass
    diagnostic['ElapsedMs'] = round((time.monotonic()-started)*1000)
    return dict(Version=1, Success=diagnostic['Stage'] == 'Complete', Diagnostic=diagnostic, Result=result)


if __name__ == '__main__':
    if len(sys.argv) != 3 or sys.argv[1] != 'CalculateProfile':
        raise SystemExit(2)
    value = execute(sys.argv[2])
    raw = json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()
    if len(raw) > MAX_RESULT:
        raise SystemExit(2)
    sys.stdout.buffer.write(MAGIC + struct.pack('!I', len(raw)) + raw)
    sys.stdout.buffer.flush()
