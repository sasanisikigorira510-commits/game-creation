"""Read-only fixed-bucket inventory. No delete, PUT, ACL change or DB access."""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
from urllib.parse import urlencode
import xml.etree.ElementTree as ET

BUCKET = 'nasus-player-backup-4433119'
ENDPOINT = 'https://s3.tky01.sakurastorage.jp/' + BUCKET + '/'
CONFIG = Path('/etc/witch-player-offsite/curl.conf')
MAX_BYTES = 2 * 1024 * 1024
NS = '{http://s3.amazonaws.com/doc/2006-03-01/}'


def require(value):
    if not value: raise ValueError('Invalid backup inventory response')


def xml(raw, root):
    require(isinstance(raw, bytes) and len(raw) <= MAX_BYTES)
    require(b'<!DOCTYPE' not in raw.upper() and b'<!ENTITY' not in raw.upper())
    result = ET.fromstring(raw)
    require(result.tag == NS + root)
    return result


def field(root, name, optional=False):
    values = root.findall(NS + name)
    require(len(values) == (0 if optional and not values else 1))
    if not values: return None
    require(not list(values[0]))
    return values[0].text or ''


def versioning(raw):
    root = xml(raw, 'VersioningConfiguration')
    value = field(root, 'Status', optional=True)
    if value is None:
        require(not list(root))
        return 'NeverEnabled'
    require(value in ('Enabled', 'Suspended'))
    return value


def page(raw):
    root = xml(raw, 'ListBucketResult')
    require(field(root, 'Name') == BUCKET and field(root, 'Prefix') == 'db/')
    require(not root.findall(NS+'CommonPrefixes'))
    require(field(root, 'EncodingType', optional=True) is None)
    truncated = field(root, 'IsTruncated')
    require(truncated in ('true', 'false'))
    objects = []
    for item in root.findall(NS+'Contents'):
        key, size = field(item, 'Key'), field(item, 'Size')
        require(re.fullmatch(r'db/\d{8}T\d{6}Z-[a-f0-9]{32}\.tar\.gz\.age', key))
        require(re.fullmatch(r'[0-9]{1,10}', size))
        objects.append(dict(Key=key, Size=int(size), LastModified=field(item, 'LastModified')))
    require(len(objects) <= 1000 and field(root, 'KeyCount') == str(len(objects)))
    token = field(root, 'NextContinuationToken', optional=True)
    if truncated == 'true': require(isinstance(token, str) and 0 < len(token) <= 4096)
    else: require(token is None)
    return objects, token


def collect(get, receipt, clock=lambda: dt.datetime.now(dt.timezone.utc)):
    started = clock()
    mode = versioning(get({'versioning': ''}))
    objects, seen, tokens, evidence = [], set(), set(), []
    token = None
    for _ in range(10):
        query = {'list-type': '2', 'prefix': 'db/', 'max-keys': '1000'}
        if token is not None: query['continuation-token'] = token
        raw = get(query)
        evidence.append(hashlib.sha256(raw).hexdigest())
        entries, token = page(raw)
        for entry in entries:
            require(entry['Key'] not in seen)
            seen.add(entry['Key']); objects.append(entry)
        if token is None: break
        require(token not in tokens); tokens.add(token)
    else: raise ValueError('Inventory page limit; no complete inventory produced')
    finished = clock()
    require(dt.timedelta(0) <= finished-started <= dt.timedelta(minutes=5))
    return dict(Bucket=BUCKET, Prefix='db/', Complete=True, Versioning=mode,
                StartedUtc=started.isoformat(), CapturedUtc=finished.isoformat(),
                Objects=objects, LastVerifiedBackup=receipt, PageSha256=evidence,
                Acquisition='AUTHENTICATED_GET_ONLY_NON_ATOMIC_LIST')


def private_root_file(path):
    for parent in path.parents:
        info = parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
    info = path.lstat()
    require(stat.S_ISREG(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o077)


def get(query):
    allowed = set(query) == {'versioning'} or set(query) in (
        {'list-type', 'prefix', 'max-keys'}, {'list-type', 'prefix', 'max-keys', 'continuation-token'})
    require(allowed)
    if 'versioning' not in query:
        require(query['prefix'] == 'db/' and query['list-type'] == '2' and query['max-keys'] == '1000')
    private_root_file(CONFIG)
    args = ['/usr/bin/curl', '--disable', '--config', str(CONFIG),
            '--aws-sigv4', 'aws:amz:jp-east-1:s3', '--proto', '=https',
            '--silent', '--show-error', '--fail', '--retry', '0', '--connect-timeout', '10',
            '--max-time', '30', '--max-filesize', str(MAX_BYTES), '--request', 'GET',
            ENDPOINT+'?'+urlencode(query)]
    result = subprocess.run(args, capture_output=True, timeout=35)
    require(result.returncode == 0) # Includes permission failures: never retry with broader credentials.
    require(len(result.stdout) <= MAX_BYTES)
    return result.stdout


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    stage = Path(__file__).parent
    private_root_file(stage/'inspect.py')
    private_root_file(stage/'backup_retention_plan.py')
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(stage))
    from backup_retention_plan import plan
    source = Path('/var/lib/witch-player-offsite/last-success.json')
    private_root_file(source)
    with source.open('rb') as stream: raw = stream.read(8193)
    require(len(raw) <= 8192)
    stored = json.loads(raw)
    receipt = {key: stored.get(key) for key in ('Status', 'Key', 'Bytes', 'Sha256', 'CompletedUtc')}
    snapshot = collect(get, receipt)
    review = plan(snapshot, now=dt.datetime.now(dt.timezone.utc))
    print('RETENTION_INVENTORY_JSON='+json.dumps(snapshot, sort_keys=True))
    print('RETENTION_REVIEW_JSON='+json.dumps(review, sort_keys=True))
    print('RETENTION_INSPECTION_COMPLETE: GET only; no objects or settings changed.')


if __name__ == '__main__':
    try: main()
    except Exception:
        print('RETENTION_INSPECTION_STOPPED: no changes; inspect access/configuration. No secret details printed.')
        raise SystemExit(1)
