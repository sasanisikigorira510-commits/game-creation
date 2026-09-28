"""One synthetic ciphertext probe, no DB, service or live deletion operations.

Run only from a hash-verified root-owned staging directory. Credentials stay on
the VPS; only ciphertext and non-secret receipt are returned to ubuntu.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import stat
import subprocess
import sys
import tempfile

PINS = {
    'store.py': '2d2664694f5951a00f65880a15ace2cc238e2edbf94f3ac45c9cb36fa0e95bb9',
    'deletion_journal.py': '0bddbb1c9a43ff13143f3bb72c6418b0e5b1d1ef9a693d89b0ae2f3a709e2427',
    'deploy/sakura_deletion_journal.py': '04407d59083a36295d00e514d7468c7f162d36955ba854a513b5969760874bdc',
}


def require(value):
    if not value: raise ValueError('Synthetic cloud probe rejected')


def private_read(path, limit):
    info = path.lstat()
    require(stat.S_ISREG(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o077)
    with path.open('rb') as stream: result = stream.read(limit + 1)
    require(len(result) <= limit)
    return result


def validate_transfer(manifest, cipher):
    require(isinstance(manifest, dict) and set(manifest) == {'Kind', 'Key', 'Bytes', 'Sha256'})
    require(manifest['Kind'] == 'SYNTHETIC_DELETION_PROBE')
    require(isinstance(manifest['Key'], str) and re.fullmatch(r'deletion-probes/[a-f0-9]{32}\.age', manifest['Key']))
    require(type(manifest['Bytes']) is int and 64 <= manifest['Bytes'] == len(cipher) <= 65536)
    require(cipher.startswith(b'age-encryption.org/v1\n'))
    require(manifest['Sha256'] == hashlib.sha256(cipher).hexdigest())


def readback(manifest, cipher, publisher_class, config, run=subprocess.run):
    validate_transfer(manifest, cipher)
    retrieved = []
    def capture(args, **kwargs):
        result = run(args, **kwargs)
        if args[-2:] == ['--request', 'GET']: retrieved.append(result.stdout)
        return result
    publisher = publisher_class(config, run=capture)
    require(publisher.probe(manifest['Key'], cipher) == manifest['Sha256'])
    require(len(retrieved) == 1 and retrieved[0] == cipher)
    return retrieved[0], dict(manifest, Status='SYNTHETIC_PROBE_READBACK_VERIFIED')


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    stage = Path(__file__).parent
    for path in (stage, *stage.parents):
        info = path.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
    cipher = private_read(stage / 'outgoing.age', 65536)
    manifest = json.loads(private_read(stage / 'transfer.json', 4096))
    validate_transfer(manifest, cipher)
    lib = Path('/usr/local/lib/nasus-deletion')
    for relative, expected in PINS.items():
        path = lib / relative
        for parent in path.parents:
            info = parent.lstat()
            require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
        info = path.lstat()
        require(stat.S_ISREG(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected)
    # The deploy package is a namespace package on this installation. Refuse
    # unpinned initialization code instead of importing it as root.
    require(not (lib / 'deploy/__init__.py').exists())
    sys.path.insert(0, str(lib))
    sys.dont_write_bytecode = True
    from deploy.sakura_deletion_journal import SakuraPublisher
    raw, receipt = readback(manifest, cipher, SakuraPublisher,
                            '/etc/witch-player-offsite/curl.conf')
    export = Path(tempfile.mkdtemp(prefix='nasus-drill-return.', dir='/var/tmp'))
    for name, contents in (('readback.age', raw), ('receipt.json', (json.dumps(receipt, sort_keys=True)+'\n').encode())):
        fd = os.open(export / name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, 'wb') as output:
            output.write(contents); output.flush(); os.fsync(output.fileno())
    user = pwd.getpwnam('ubuntu')
    for name in ('readback.age', 'receipt.json'): os.chown(export / name, user.pw_uid, user.pw_gid)
    os.chown(export, user.pw_uid, user.pw_gid)  # Last operation in this directory.
    print('DRILL_EXPORT_DIRECTORY=' + str(export))
    print('SYNTHETIC_PROBE_READBACK_VERIFIED: no production DB or service changed.')


if __name__ == '__main__':
    try: main()
    except Exception:
        print('SYNTHETIC_CLOUD_PROBE_STOPPED: artifacts retained; no private details printed.')
        raise SystemExit(1)
