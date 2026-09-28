"""DB-only age encryption and private S3 transfer. Never upload plaintext or delete remote objects."""
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import subprocess
import tarfile
import tempfile
import uuid

BASE = Path('/var/lib/witch-player-offsite')
CONFIG = Path('/etc/witch-player-offsite')
ENDPOINT = 'https://s3.tky01.sakurastorage.jp/nasus-player-backup-4433119/'
MAX_BYTES = 64 * 1024 * 1024
# Conservative cumulative upload ceiling; manual review is needed to raise it.
TOTAL_BUDGET = 50 * 1024**3


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def private_file(path):
    info = path.lstat()
    if path.is_symlink() or not path.is_file() or info.st_uid != 0 or info.st_mode & 0o077:
        raise ValueError('Unsafe credential file permissions')


def atomic_json(path, data):
    fd, temporary = tempfile.mkstemp(dir=path.parent, prefix='.state-')
    try:
        with os.fdopen(fd, 'w') as out:
            json.dump(data, out, sort_keys=True)
            out.flush()
            os.fsync(out.fileno())
        os.replace(temporary, path)
        directory = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        Path(temporary).unlink(missing_ok=True)


def reserve_budget(path, amount):
    state = json.loads(path.read_text()) if path.exists() else {'ReservedBytes': 0}
    total = state['ReservedBytes']
    if not isinstance(total, int) or total < 0 or amount <= 0 or total + amount > TOTAL_BUDGET:
        raise ValueError('Upload budget exceeded; manual capacity review required')
    # Count attempts conservatively even if network transfer fails.
    atomic_json(path, {'ReservedBytes': total + amount})


def curl_args(config, key):
    if not re.fullmatch(r'db/\d{8}T\d{6}Z-[a-f0-9]{32}\.tar\.gz\.age', key):
        raise ValueError('Unexpected remote object name')
    return ['/usr/bin/curl', '--disable', '--config', str(config),
            '--aws-sigv4', 'aws:amz:jp-east-1:s3', '--proto', '=https',
            '--silent', '--show-error', '--fail', '--retry', '2',
            '--connect-timeout', '15', '--max-time', '180',
            '--max-filesize', str(MAX_BYTES), ENDPOINT + key]


def transfer(config, key, encrypted, downloaded, run=subprocess.run):
    base = curl_args(config, key)
    for extra in (['--upload-file', str(encrypted), '--header', 'x-amz-acl: private',
                   '--header', 'Content-Type: application/octet-stream', '--output', '/dev/null'],
                  ['--output', str(downloaded)]):
        result = run(base + extra, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=600)
        if result.returncode:
            # Do not echo provider responses, command lines or credentials.
            raise RuntimeError('S3 transfer failed (curl exit %d)' % result.returncode)
    if encrypted.stat().st_size != downloaded.stat().st_size or digest(encrypted) != digest(downloaded):
        raise ValueError('Downloaded ciphertext does not match the uploaded snapshot')


def main():
    if os.geteuid() != 0:
        raise ValueError('Run as root through the backup service')
    os.umask(0o077)
    config = CONFIG / 'curl.conf'
    private_file(config)
    private_file(CONFIG / 'recipient.txt')
    with (BASE / 'lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        account = pwd.getpwnam('witchplayer')
        with tempfile.TemporaryDirectory(prefix='run-', dir=BASE / 'work') as temporary:
            work = Path(temporary)
            # Only the snapshot subdirectory is accessible to the DB service user.
            work.chmod(0o711)
            snapshot_dir = work / 'snapshot'
            snapshot_dir.mkdir(mode=0o700)
            os.chown(snapshot_dir, account.pw_uid, account.pw_gid)
            maintenance = ['/usr/sbin/runuser', '-u', 'witchplayer', '--',
                           '/opt/witch-player/venv/bin/python', '/opt/witch-player/server/maintenance.py']
            result = subprocess.run(maintenance + ['backup', '--database', '/var/lib/witch-player/players.sqlite',
                                     '--output-dir', str(snapshot_dir)], capture_output=True, timeout=300)
            if result.returncode:
                raise RuntimeError('Consistent database snapshot failed')
            snapshot = Path(result.stdout.decode().strip())
            if snapshot.parent != snapshot_dir or not re.fullmatch(r'players-\d{8}T\d{12}Z-[a-f0-9]{8}\.sqlite', snapshot.name):
                raise ValueError('Unexpected snapshot path')
            if snapshot.stat().st_size > 128 * 1024 * 1024:
                raise ValueError('Database exceeds reviewed small-server limit')
            encrypted = work / 'snapshot.tar.gz.age'
            with subprocess.Popen(['/usr/local/lib/nasus-backup/age', '-R', str(CONFIG / 'recipient.txt'),
                                   '-o', str(encrypted)], stdin=subprocess.PIPE,
                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) as process:
                try:
                    with tarfile.open(fileobj=process.stdin, mode='w|gz') as archive:
                        for item in (snapshot, Path(str(snapshot) + '.json')):
                            archive.add(item, arcname=item.name, recursive=False)
                    process.stdin.close()
                    if process.wait(timeout=120):
                        raise RuntimeError('Encryption failed')
                finally:
                    if process.poll() is None:
                        process.kill()
            if not 0 < encrypted.stat().st_size <= MAX_BYTES:
                raise ValueError('Encrypted snapshot exceeds transfer limit')
            key = 'db/' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex + '.tar.gz.age'
            reserve_budget(BASE / 'budget.json', encrypted.stat().st_size)
            transfer(config, key, encrypted, work / 'downloaded.age')
            report = {'Status': 'OFFSITE_CIPHERTEXT_VERIFIED', 'Key': key,
                      'Bytes': encrypted.stat().st_size, 'Sha256': digest(encrypted),
                      'CompletedUtc': dt.datetime.now(dt.timezone.utc).isoformat()}
            atomic_json(BASE / 'last-success.json', report)
            print(json.dumps(report, sort_keys=True))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # No arbitrary external output or secrets go to journald.
        print('OFFSITE_BACKUP_FAILED: ' + type(error).__name__, flush=True)
        raise SystemExit(1)
