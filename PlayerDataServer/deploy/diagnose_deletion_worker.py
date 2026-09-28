"""Read-only reproduction under the installed service's sandbox.

No claiming, encrypting, uploading, acknowledging, migrating or status writes.
Only exception TYPE and code location are printed, never messages/locals/rows.
"""
import hashlib
import argparse
import json
import os
import signal
from pathlib import Path
import stat
import sys
import traceback


def safe_error(error):
    return {'Type': type(error).__name__,
            'Errno': error.errno if isinstance(error, OSError) else None,
            'Frames': [{'File': Path(frame.filename).name, 'Function': frame.name,
                        'Line': frame.lineno} for frame in traceback.extract_tb(error.__traceback__)]}


def security_context():
    """Allowlisted process security metadata only; never environment/commands."""
    allowed = {'Uid', 'Gid', 'CapInh', 'CapPrm', 'CapEff', 'CapBnd', 'CapAmb',
               'NoNewPrivs', 'Seccomp', 'Seccomp_filters'}
    result = {}
    try:
        for line in Path('/proc/self/status').read_text().splitlines():
            key, _, value = line.partition(':')
            if key in allowed: result[key] = value.strip()
        result['AppArmor'] = Path('/proc/self/attr/current').read_text()[:256].strip()
        result['UidMap'] = Path('/proc/self/uid_map').read_text()[:256].strip()
        result['GidMap'] = Path('/proc/self/gid_map').read_text()[:256].strip()
    except OSError as error:
        result['MetadataErrorType'] = type(error).__name__
    return result


def minimal_root_context(context):
    try:
        return (context.get('NoNewPrivs') == '1'
                and all(int(context[name], 16) == 0xc0 for name in ('CapEff', 'CapPrm', 'CapBnd'))
                and all(int(context[name], 16) & ~0xc0 == 0 for name in ('CapInh', 'CapAmb')))
    except (KeyError, ValueError, TypeError):
        return False


def unprivileged_context(context, account):
    try:
        return (list(map(int, context['Uid'].split())) == [account.pw_uid] * 4
                and list(map(int, context['Gid'].split())) == [account.pw_gid] * 4
                and all(int(context[name], 16) == 0 for name in ('CapEff', 'CapPrm', 'CapAmb')))
    except (KeyError, ValueError, TypeError):
        return False


def drop_privileges(account, function, *, require_minimal=False):
    """Capture the boundary that the original worker intentionally redacts."""
    phase = 'alarm'
    contexts = {'Before': security_context()}
    try:
        signal.alarm(45)
        phase = 'setgroups'; os.setgroups([])
        contexts['AfterSetgroups'] = security_context()
        phase = 'setgid'; os.setgid(account.pw_gid)
        contexts['AfterSetgid'] = security_context()
        phase = 'setuid'; os.setuid(account.pw_uid)
        contexts['AfterSetuid'] = security_context()
        phase = 'post_drop_capabilities'
        if require_minimal and not unprivileged_context(contexts['AfterSetuid'], account):
            raise RuntimeError('Child retains privilege or wrong identity; database callback blocked')
        phase = 'umask'; os.umask(0o077)
        phase = 'database_callback'
        return {'OK': True, 'Value': function(), 'SecurityContexts': contexts}
    except Exception as error:
        return {'OK': False, 'BoundaryPhase': phase, 'Error': safe_error(error),
                'SecurityContexts': contexts}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--minimal-capabilities', action='store_true')
    args = parser.parse_args()
    if os.geteuid() != 0: raise SystemExit('Run through the prepared sudo diagnostic')
    parent_context = security_context()
    if args.minimal_capabilities and not minimal_root_context(parent_context):
        print('NASUS_DIAGNOSTIC_JSON=' + json.dumps({'Status': 'CAPABILITY_SCOPE_MISMATCH',
              'ParentSecurityContext': parent_context, 'DatabaseChanged': False, 'CloudRequests': 0,
              'ConfigurationChanged': False}, sort_keys=True))
        print('DIAGNOSTIC_SUMMARY: CAPABILITY_SCOPE_MISMATCH; no database operation attempted')
        return
    root = Path('/usr/local/lib/nasus-deletion')
    expected = 'aa8d1e4c2ac217ccfab43cdb25bc043fdf3e95a6cf4f5c041b8352b4ef26b7c4'
    if hashlib.sha256((root / 'deploy/deletion_worker.py').read_bytes()).hexdigest() != expected:
        raise SystemExit('WORKER_CHANGED: stop and review before diagnosing')
    sys.path.insert(0, str(root))
    from deploy import deletion_worker as worker
    report = {'Status': 'DIAGNOSTIC', 'DatabaseChanged': False, 'CloudRequests': 0,
              'ConfigurationChanged': False, 'Checks': [], 'ParentSecurityContext': parent_context,
              'MinimalCapabilitiesTest': args.minimal_capabilities, 'ChildSecurityContexts': []}
    lock = worker.STATE / 'worker.lock'
    if not worker.private_file(lock, 0):
        report['Status'] = 'LOCK_METADATA_FAILED'
        print(json.dumps(report)); return
    original_open = Path.open
    phase = ['startup']

    def original_child(account, fn):
        read_fd, write_fd = os.pipe()
        pid = os.fork()
        if pid == 0:
            os.close(read_fd)
            value = drop_privileges(account, fn, require_minimal=args.minimal_capabilities)
            with os.fdopen(write_fd, 'wb') as output:
                output.write(json.dumps(value, allow_nan=False).encode())
            os._exit(0)
        os.close(write_fd)
        with os.fdopen(read_fd, 'rb') as source: raw = source.read(16385)
        _, status = os.waitpid(pid, 0)
        if status or len(raw) > 16384:
            report['BoundaryProcessStatus'] = status
            raise RuntimeError('Diagnostic child did not return normally')
        value = json.loads(raw)
        if value.get('OK') is not True:
            report['BoundaryFailure'] = value
            raise RuntimeError('Diagnostic privilege boundary failed')
        report['ChildSecurityContexts'].append(value['SecurityContexts'])
        return value['Value']

    def read_only_open(path, mode='r', *args, **kwargs):
        if path == lock and mode == 'a': mode = 'r'
        if any(flag in mode for flag in ('w', 'a', 'x', '+')):
            raise RuntimeError('Diagnostic write blocked')
        return original_open(path, mode, *args, **kwargs)

    def child_checked(account, fn):
        def wrapped():
            try: return {'Passed': True, 'Value': fn()}
            except Exception as error: return {'Passed': False, 'Error': safe_error(error)}
        result = original_child(account, wrapped)
        if not result['Passed']:
            report['Checks'].append({'Phase': phase[0], 'Passed': False, 'Error': result['Error']})
            raise RuntimeError('Diagnostic child failed')
        report['Checks'].append({'Phase': phase[0], 'Passed': True})
        return result['Value']

    def no_claim(directory, recipient):
        # Same existing-schema open as the real claim, but no BEGIN IMMEDIATE or UPDATE.
        worker.open_journal(directory)
        return None

    original_health = worker.queue_healthy
    def health(directory):
        return original_health(directory)

    call_count = [0]
    def phased_child(account, fn):
        call_count[0] += 1
        phase[0] = 'database_schema' if call_count[0] == 1 else 'queue_health'
        return child_checked(account, fn)

    # Prevent persistent writes and any external I/O, even if future code changes.
    Path.open = read_only_open
    worker.as_database_user = phased_child
    worker.claim = no_claim
    worker.queue_healthy = health
    worker.atomic_json = lambda path, value: report.update(WouldPublishHealthy=value.get('Healthy'))
    worker.SakuraPublisher = lambda *a, **k: (_ for _ in ()).throw(RuntimeError('Diagnostic cloud access blocked'))
    try:
        worker.run_once(worker.pwd.getpwnam('witchplayer'))
        report['Status'] = 'READ_ONLY_WORKER_PATH_PASSED'
    except Exception as error:
        report['Status'] = 'READ_ONLY_WORKER_PATH_FAILED'
        report['Error'] = safe_error(error)
        report['Phase'] = phase[0]
    finally:
        Path.open = original_open
    print('NASUS_DIAGNOSTIC_JSON=' + json.dumps(report, sort_keys=True))
    boundary = report.get('BoundaryFailure', {})
    print('DIAGNOSTIC_SUMMARY: ' + report['Status'] + '; phase=' + str(boundary.get('BoundaryPhase', report.get('Phase')))
          + '; errno=' + str(boundary.get('Error', {}).get('Errno')))


if __name__ == '__main__': main()
