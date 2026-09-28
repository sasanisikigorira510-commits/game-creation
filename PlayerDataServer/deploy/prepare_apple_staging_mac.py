"""Create a reviewed code-only bundle or local private token key. Never print secrets."""
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import stat
import sys

BACKEND = Path(__file__).resolve().parents[1]
PRIVATE = Path('/Users/andou/Library/Application Support/NasusApple/keys')


def keys():
    from cryptography.hazmat.primitives import serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    signing = PRIVATE/'AuthKey_38GM3RYVD9.p8'
    token = PRIVATE/'token-encryption-20260926.key'
    for path in (PRIVATE, signing):
        info=path.lstat()
        if (any(p.is_symlink() for p in (path,*path.parents)) or info.st_uid != os.getuid()
                or info.st_mode & 0o077): raise ValueError('Private key metadata rejected')
    key=serialization.load_pem_private_key(signing.read_bytes(),None)
    if not isinstance(key,ec.EllipticCurvePrivateKey) or not isinstance(key.curve,ec.SECP256R1):
        raise ValueError('Wrong signing key type')
    if not os.path.lexists(token):
        fd=os.open(token,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'wb') as out:
            out.write(secrets.token_bytes(32)); out.flush(); os.fsync(out.fileno())
    info=token.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077 or info.st_size != 32:
        raise ValueError('Existing token key must not be overwritten')
    # Caller uses these digests for transfer binding; no key bytes are printed.
    print(hashlib.sha256(signing.read_bytes()).hexdigest(),hashlib.sha256(token.read_bytes()).hexdigest())


def bundle(destination):
    sys.path.insert(0,str(BACKEND))
    from deploy.install_apple_staging import CODE,WHEELS
    destination=Path(destination).resolve()
    manifest={}
    for name in sorted(CODE):
        source=BACKEND/('deploy/'+name if name == 'prepare_apple_schema.py' else name)
        if source.is_symlink(): raise ValueError('Unexpected source')
        shutil.copyfile(source,destination/name)
        manifest[name]=hashlib.sha256((destination/name).read_bytes()).hexdigest()
    if {p.name for p in (destination/'wheels').iterdir()} != WHEELS:
        raise ValueError('Wheel set mismatch')
    for name in sorted(WHEELS): manifest['wheels/'+name]=hashlib.sha256((destination/'wheels'/name).read_bytes()).hexdigest()
    (destination/'manifest.json').write_text(json.dumps(manifest,sort_keys=True,indent=2)+'\n')
    shutil.copyfile(BACKEND/'deploy/install_apple_staging.py',destination/'install_apple_staging.py')
    print(json.dumps({name:hashlib.sha256((destination/name).read_bytes()).hexdigest()
                      for name in ('manifest.json','install_apple_staging.py')},sort_keys=True))


if __name__ == '__main__':
    os.umask(0o077)
    try:
        if sys.argv[1:] == ['--keys']: keys()
        elif len(sys.argv) == 3 and sys.argv[1] == '--bundle': bundle(sys.argv[2])
        else: raise ValueError('Explicit mode required')
    except Exception:
        print('APPLE_LOCAL_PREPARATION_STOPPED: existing keys retained; no secret details printed.',file=sys.stderr)
        raise SystemExit(1)
