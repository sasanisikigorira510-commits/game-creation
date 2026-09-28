"""Encrypt the two configured Apple keys, then verify decryption in memory.

No plaintext archive, cloud upload, key rotation, or server change. The CLI has
fixed sources and creates a fresh private directory; it never overwrites a key.
Copying to an independent recovery device is a separate, verified operation.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile

FILES = ('AuthKey_38GM3RYVD9.p8', 'token-encryption-20260926.key')
BASE = Path('/Users/andou/Library/Application Support/NasusApple')
BACKUPS = Path('/Users/andou/Library/Application Support/NasusBackups')
RECOVERY = Path('/Users/andou/.config/nasus-backup')
AGE = BACKUPS/'tools/age-v1.3.2-darwin/age/age'
README = '''Apple key recovery bundle — version 1

Contains the Apple Sign In signing key (38GM3RYVD9) and the 32-byte token
encryption key, encrypted to the existing Nasus backup recipient. The private
age identity is NOT included. Keep its existing offline USB copy safe.

This is NOT a complete server backup. Recovery also needs the database, its
deletion history/completeness checks, configuration and compatible application
code. Do not replace a live database or clear recovery quarantine automatically.
The token encryption key must match the database generation being recovered.

SHA256SUMS detects transfer corruption; age authenticated decryption verifies
the contents. Decryption was tested in memory on the source Mac. An independent
device copy is not complete until separately copied and verified.
Do not paste decrypted contents into chat, logs, or a public repository.
'''


def require(value):
    if not value:
        raise ValueError('Apple recovery validation failed')


def private_read(path, limit):
    path = Path(path)
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    for parent in path.parents:
        info = parent.stat()
        require(info.st_uid in (0, os.getuid()) and not info.st_mode & 0o022)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(fd, 'rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == os.getuid()
                and not info.st_mode & 0o077 and 0 < info.st_size <= limit)
        raw = stream.read(limit + 1)
    require(0 < len(raw) <= limit)
    return raw


def run_age(age, args, raw=None):
    result = subprocess.run([str(age), *map(str, args)], input=raw,
                            capture_output=True, timeout=30)
    # Never propagate stderr: it may contain sensitive input details.
    require(result.returncode == 0)
    return result.stdout


def payload(keys):
    from cryptography.hazmat.primitives import serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    signing = private_read(keys/FILES[0], 16384)
    key = serialization.load_pem_private_key(signing, password=None)
    require(isinstance(key, ec.EllipticCurvePrivateKey)
            and isinstance(key.curve, ec.SECP256R1))
    token = private_read(keys/FILES[1], 32)
    require(len(token) == 32)
    values = dict(zip(FILES, (signing, token)))
    return json.dumps(dict(Version=1, TeamId='687D767B8W', KeyId='38GM3RYVD9',
        ClientId='com.nasus.dungeonmonsterroguelike', Files={
            name: dict(Base64=base64.b64encode(raw).decode('ascii'),
                       Sha256=hashlib.sha256(raw).hexdigest())
            for name, raw in values.items()}), sort_keys=True).encode('ascii')


def new_file(path, raw):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())


def create_backup(keys, recipient, identity, age, destination):
    require(not any(p.is_symlink() for p in (destination, *destination.parents)))
    for parent in destination.parents:
        info = parent.stat()
        require(info.st_uid in (0, os.getuid()) and not info.st_mode & 0o022)
    info = destination.parent.stat()
    require(info.st_uid == os.getuid() and not info.st_mode & 0o077)
    # Check identity metadata without printing/copying its contents.
    private_read(identity, 16384)
    target = private_read(recipient, 1024).decode('ascii').strip()
    require(target.startswith('age1') and not any(c.isspace() for c in target))
    derived = run_age(age.parent/'age-keygen', ['-y', identity]).decode('ascii').strip()
    require(derived == target)
    plain = payload(keys)
    ciphertext = run_age(age, ['-r', target], plain)
    require(run_age(age, ['-d', '-i', identity], ciphertext) == plain)
    # Only ciphertext and non-secret instructions/receipt ever reach disk.
    destination.mkdir(mode=0o700)  # Refuse reuse, including symlink targets.
    name = 'apple-recovery-v1.json.age'
    new_file(destination/name, ciphertext)
    saved = private_read(destination/name, 65536)
    require(saved == ciphertext and run_age(age, ['-d', '-i', identity], saved) == plain)
    digest = hashlib.sha256(saved).hexdigest()
    new_file(destination/'SHA256SUMS', (digest+'  '+name+'\n').encode())
    new_file(destination/'README.txt', README.encode())
    result = dict(Status='APPLE_KEY_CIPHERTEXT_VERIFIED', KeyCount=2,
                  PlaintextArchiveWritten=False, IndependentCopyVerified=False,
                  CiphertextSha256=digest)
    new_file(destination/'verified.json', (json.dumps(result, sort_keys=True)+'\n').encode())
    fd = os.open(destination, os.O_RDONLY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)
    return result


if __name__ == '__main__':
    import sys
    os.umask(0o077)
    try:
        require(len(sys.argv) == 1)
        # Existing configured backup root must be private; no discovery of keys.
        info = BACKUPS.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.getuid()
                and not info.st_mode & 0o077 and not BACKUPS.is_symlink())
        parent = Path(tempfile.mkdtemp(prefix='apple-keys-', dir=BACKUPS))
        result = create_backup(BASE/'keys', RECOVERY/'recipient.txt', RECOVERY/'identity.txt',
                               AGE, parent/'recovery')
        result['Directory'] = str(parent/'recovery')
        print(json.dumps(result, sort_keys=True))
    except Exception:
        print('APPLE_KEY_BACKUP_STOPPED: originals retained; no secret details printed.', file=sys.stderr)
        raise SystemExit(1)
