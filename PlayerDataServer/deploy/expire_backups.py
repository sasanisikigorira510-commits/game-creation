"""Authorized db/ 30-day expiration. No DB access, PUT, or other prefixes.

Holds the existing uploader lock. UUID object names must never be reused by
other writers. HEAD + DELETE is not claimed to be an atomic conditional delete
on Sakura; do not change bucket versioning or overwrite objects concurrently.
"""
import datetime as dt
from email.utils import parsedate_to_datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time

if __package__:
    from . import inspect_backup_retention as inventory
    from .backup_retention_plan import KEY, plan, timestamp
else:
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import inspect_backup_retention as inventory
    from backup_retention_plan import KEY, plan, timestamp

STATE = Path('/var/lib/witch-player-retention')
BACKUP = Path('/var/lib/witch-player-offsite')
MAX_OBJECT = 64 * 1024 * 1024
UTC = dt.timezone.utc


def require(ok):
    if not ok: raise ValueError('Retention safety check failed')


def now(): return dt.datetime.now(UTC)


def atomic(path, value):
    fd, temporary = tempfile.mkstemp(prefix='.status-', dir=path.parent)
    try:
        with os.fdopen(fd, 'w') as out:
            json.dump(value, out, sort_keys=True); out.flush(); os.fsync(out.fileno())
        os.replace(temporary, path)
        fd = os.open(path.parent, os.O_RDONLY)
        try: os.fsync(fd)
        finally: os.close(fd)
    finally: Path(temporary).unlink(missing_ok=True)


def read_receipt():
    path = BACKUP / 'last-success.json'
    inventory.private_root_file(path)
    with path.open('rb') as source: raw = source.read(8193)
    require(len(raw) <= 8192)
    value = json.loads(raw)
    return {key: value.get(key) for key in ('Status', 'Key', 'Bytes', 'Sha256', 'CompletedUtc')}


def request(method, key, *, etag=None):
    require(method in ('GET', 'HEAD', 'DELETE') and isinstance(key, str) and KEY.fullmatch(key))
    if etag is not None:
        require(method == 'DELETE' and isinstance(etag, str) and 2 < len(etag) < 256
                and etag.startswith('"') and etag.endswith('"')
                and all(32 <= ord(c) < 127 for c in etag))
    inventory.private_root_file(inventory.CONFIG)
    with tempfile.TemporaryDirectory(prefix='request-', dir=STATE) as temporary:
        body, headers = Path(temporary)/'body', Path(temporary)/'headers'
        args = ['/usr/bin/curl', '--disable', '--config', str(inventory.CONFIG),
                '--aws-sigv4', 'aws:amz:jp-east-1:s3', '--proto', '=https',
                '--silent', '--show-error', '--retry', '0', '--connect-timeout', '10',
                '--max-time', '90', '--max-filesize', str(MAX_OBJECT),
                '--dump-header', str(headers), '--output', str(body), '--write-out', '%{http_code}']
        args += ['--head'] if method == 'HEAD' else ['--request', method]
        if etag is not None: args += ['--header', 'If-Match: '+etag]
        result = subprocess.run(args+[inventory.ENDPOINT+key], capture_output=True, timeout=95)
        require(result.returncode == 0 and result.stdout in (b'200', b'204', b'404'))
        require(headers.stat().st_size <= 65536)
        block = headers.read_bytes().decode('iso-8859-1').strip().split('\r\n\r\n')[-1]
        values = {}
        for line in block.split('\r\n')[1:]:
            name, value = line.split(':', 1); name = name.lower()
            require(name not in values); values[name] = value.strip()
        stamp = parsedate_to_datetime(values['date'])
        require(stamp.utcoffset() is not None and abs((now()-stamp).total_seconds()) <= 300)
        require(values.get('x-amz-version-id') in (None, 'null'))
        raw = b''
        if method == 'GET':
            require(body.stat().st_size <= MAX_OBJECT); raw = body.read_bytes()
        return int(result.stdout), values, raw


def check_head(response, obj):
    status, headers, _ = response
    require(status == 200 and int(headers['content-length']) == obj['Bytes'])
    # HTTP timestamps have second precision; the inventory may have milliseconds.
    modified = parsedate_to_datetime(headers['last-modified'])
    require(modified.utcoffset() is not None
            and modified == timestamp(obj['LastModified']).replace(microsecond=0))
    tag = headers.get('etag')
    require(isinstance(tag, str) and tag.startswith('"') and tag.endswith('"'))
    return tag


def expire(snapshot, *, execute, http=request, version_get=inventory.get,
           clock=now, audit=lambda value: None):
    start = clock()
    review = plan(snapshot, now=start)
    require(not review['Blockers'] and review['CandidateCount'] <= 100)
    receipt = snapshot['LastVerifiedBackup']
    latest = next(o for o in snapshot['Objects'] if o['Key'] == receipt['Key'])
    check_head(http('HEAD', receipt['Key']), dict(latest, Bytes=latest['Size']))
    if review['CandidateCount']:
        code, _, data = http('GET', receipt['Key'])
        require(code == 200 and len(data) == receipt['Bytes']
                and hashlib.sha256(data).hexdigest() == receipt['Sha256'])
    deleted = 0
    for obj in review['Candidates']:
        current = clock()
        require(dt.timedelta(0) <= current-start <= dt.timedelta(minutes=8))
        require(not plan(snapshot, now=current)['Blockers'])
        require(inventory.versioning(version_get({'versioning': ''})) == 'NeverEnabled')
        tag = check_head(http('HEAD', obj['Key']), obj)
        if execute:
            # Intent is fsynced before any irreversible request. No silent retries.
            audit(dict(Action='DELETE_INTENT', **obj))
            code, _, _ = http('DELETE', obj['Key'], etag=tag)
            require(code == 204)
            require(http('HEAD', obj['Key'])[0] == 404)
            deleted += 1
            audit(dict(Action='DELETE_CONFIRMED', **obj))
    return dict(Status='RETENTION_COMPLETE' if execute else 'RETENTION_DRY_RUN_COMPLETE',
                Healthy=True, CheckedUnix=clock().timestamp(), RetentionDays=30,
                CandidateCount=review['CandidateCount'], ObjectsDeleted=deleted,
                RetainedCount=review['RetainedCount'], Execute=execute)


def audit(value):
    path = STATE/'audit.jsonl'
    if path.exists(): inventory.private_root_file(path)
    fd = os.open(path, os.O_APPEND | os.O_CREAT | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'a') as stream:
        stream.write(json.dumps(dict(value, CheckedUtc=now().isoformat()), sort_keys=True)+'\n')
        stream.flush(); os.fsync(stream.fileno())


def prerequisites():
    result = subprocess.run(['/usr/bin/timedatectl', 'show', '-p', 'NTPSynchronized', '--value'],
                            capture_output=True, timeout=10)
    require(result.returncode == 0 and result.stdout.strip() == b'yes')
    for unit in ('witch-player-offsite', 'witch-player-backup'):
        result = subprocess.run(['/usr/bin/systemctl', 'is-active', unit+'.timer'],
                                capture_output=True, timeout=10)
        require(result.returncode == 0 and result.stdout.strip() == b'active')
        result = subprocess.run(['/usr/bin/systemctl', 'show', unit+'.service',
                                 '-p', 'Result', '-p', 'ExecMainStatus'], capture_output=True, timeout=10)
        require(result.returncode == 0 and set(result.stdout.splitlines()) == {b'Result=success', b'ExecMainStatus=0'})


def main():
    require(os.geteuid() == 0 and sys.argv[1:] in (['--execute'], ['--dry-run']))
    execute = sys.argv[1] == '--execute'
    os.umask(0o077)
    require(STATE.is_dir() and not STATE.is_symlink() and STATE.stat().st_uid == 0
            and STATE.stat().st_mode & 0o777 == 0o700)
    try:
        prerequisites()
        lockpath = BACKUP/'lock'
        inventory.private_root_file(lockpath)
        fd = os.open(lockpath, os.O_RDWR | os.O_NOFOLLOW)
        with os.fdopen(fd, 'r+') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            snapshot = inventory.collect(inventory.get, read_receipt())
            result = expire(snapshot, execute=execute, audit=audit)
            if execute: atomic(STATE/'status.json', result)
            print(json.dumps(result, sort_keys=True), flush=True)
    except Exception:
        if execute: atomic(STATE/'status.json', dict(Healthy=False, CheckedUnix=time.time(), Status='FAILED'))
        raise


if __name__ == '__main__':
    try: main()
    except Exception:
        print('RETENTION_STOPPED: inspect service; no automatic retry or credential fallback.', flush=True)
        raise SystemExit(1)
