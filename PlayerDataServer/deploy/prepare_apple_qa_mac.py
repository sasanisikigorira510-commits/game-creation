"""Prepare code-only QA bundle and separate private device configuration."""
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import sys

BACKEND=Path(__file__).resolve().parents[1]
PRIVATE=Path('/Users/andou/Library/Application Support/NasusApple/qa-20260926')
CODE={'account_deletion.py','account_linking.py','apple_application.py','apple_grants.py',
    'apple_identity.py','apple_tokens.py','apple_revocation_runtime.py','apple_revocation_worker.py',
    'application.py','backup_health.py','deletion_journal.py','maintenance.py',
    'security.py','store.py','qa_gateway.py','catalog.json','gunicorn.conf.py'}


def prepare(destination, create_client=True):
    os.umask(0o077)
    destination=Path(destination).resolve()
    if not destination.is_dir() or destination.is_symlink(): raise ValueError('Bundle directory missing')
    manifest={}
    for name in sorted(CODE):
        source=BACKEND/name
        if source.is_symlink(): raise ValueError('Unexpected source link')
        shutil.copyfile(source,destination/name)
        manifest[name]=hashlib.sha256((destination/name).read_bytes()).hexdigest()
    wheels=list((destination/'wheels').glob('*.whl'))
    if len(wheels)!=5: raise ValueError('Expected four Apple wheels plus gunicorn')
    for path in wheels: manifest['wheels/'+path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
    for name in ('install_apple_qa.py','install_apple_staging.py'):
        shutil.copyfile(BACKEND/'deploy'/name,destination/name)
    (destination/'manifest.json').write_text(json.dumps(manifest,sort_keys=True,indent=2)+'\n')
    if create_client:
        if PRIVATE.exists(): raise ValueError('Retain existing QA credentials; do not regenerate automatically')
        PRIVATE.mkdir(mode=0o700)
        client={'BaseUrl':'https://api.nasus-games.com/qa','ExperimentalAppleLinking':True,'QaAccessKey':secrets.token_hex(32)}
        with (PRIVATE/'client.json').open('x') as out: json.dump(client,out)
    print(json.dumps({name:hashlib.sha256((destination/name).read_bytes()).hexdigest()
        for name in ('manifest.json','install_apple_qa.py','install_apple_staging.py')}))


if __name__=='__main__':
    try:
        if len(sys.argv)==2: prepare(sys.argv[1])
        elif len(sys.argv)==3 and sys.argv[1]=='--code-only': prepare(sys.argv[2],False)
        else: raise ValueError('Destination required')
    except Exception:
        print('QA_PREPARATION_STOPPED: retain existing files; no secret details printed.',file=sys.stderr)
        raise SystemExit(1)
