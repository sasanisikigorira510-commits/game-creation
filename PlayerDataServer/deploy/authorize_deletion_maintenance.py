"""User-authenticated one-time installation of a fixed expiring sudo helper.

Does NOT run repair, enable timers, change game data, or expose credentials.
"""
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import time

SOURCE = Path('/home/ubuntu/nasus-deploy/20260925-deletion-maintenance-01/deletion_maintenance.py')
EXPECTED = '51698a01a881d73b4b2b4a646038a4f48d003f2e7e6ed4b8bf4c52b4b3c32c83'
ROOT = Path('/var/lib/nasus-maintenance-20260925')
HELPER = Path('/usr/local/sbin/nasus-deletion-maintenance-20260925')
POLICY = Path('/etc/sudoers.d/nasus-deletion-maintenance-20260925')
ACTION_NAMES = ('apply', 'verify', 'rollback', 'revoke')


def checked(args):
    subprocess.run(args, check=True, capture_output=True, timeout=30,
                   stdin=subprocess.DEVNULL, env={'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL': 'C'})


def new_file(path, data, mode):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(data); stream.flush(); os.fsync(stream.fileno()); os.fchmod(stream.fileno(), mode)


def safe_parent(path):
    for parent in (path.parent, *path.parent.parents):
        info = parent.lstat()
        if not stat.S_ISDIR(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
            raise ValueError('Unsafe destination parent')


def policy_bytes():
    commands = [f'sha256:{EXPECTED} {HELPER} {action}' for action in ACTION_NAMES]
    return ('# Fixed, root-owned maintenance; helper rejects apply/verify after two hours.\n'
            'ubuntu ALL=(root) NOPASSWD: ' + ', '.join(commands) + '\n').encode()


def main():
    if os.geteuid() != 0: raise PermissionError('Manual sudo authentication required')
    os.umask(0o077)
    for path in (ROOT, HELPER, POLICY):
        if path.exists() or path.is_symlink(): raise ValueError('Already prepared; do not overwrite')
        safe_parent(path)
    checked(['/usr/sbin/visudo', '-c'])
    # Read a bounded unprivileged payload as data. Never execute it before pinning.
    fd = os.open(SOURCE, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        if not stat.S_ISREG(os.fstat(stream.fileno()).st_mode): raise ValueError('Not a regular payload')
        raw = stream.read(65537)
    if len(raw) > 65536 or hashlib.sha256(raw).hexdigest() != EXPECTED:
        raise ValueError('Payload checksum mismatch')
    ROOT.mkdir(mode=0o700)
    pending = ROOT / 'authorization.pending'
    policy = policy_bytes()
    new_file(pending, policy, 0o440)
    checked(['/usr/sbin/visudo', '-cf', str(pending)])
    new_file(HELPER, raw, 0o755)
    new_file(ROOT / 'grant.json', json.dumps({'Version': 1, 'IssuedUnix': time.time(),
             'PolicySha256': hashlib.sha256(policy).hexdigest()}, sort_keys=True).encode(), 0o600)
    installed = False
    try:
        new_file(POLICY, policy, 0o440); installed = True
        checked(['/usr/sbin/visudo', '-c'])
    except Exception:
        if installed: POLICY.rename(ROOT / 'authorization.invalid')
        raise
    print('MAINTENANCE_AUTHORIZED_2_HOURS: exact apply/verify/rollback/revoke commands only.')
    print('Repair NOT yet run. No database, service, backup or API settings changed.')


if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('AUTHORIZATION_SETUP_STOPPED: ' + type(error).__name__ + '; no secrets printed.')
        raise SystemExit(1)
