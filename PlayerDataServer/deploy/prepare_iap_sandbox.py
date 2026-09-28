"""Build a non-secret deployment artifact from reviewed code and vendor assets.

No network, remote execution or key copying. The actual p8 stays outside it.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--assets',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    source=Path(__file__).resolve().parent
    args.output.mkdir(mode=0o700)
    entries={}
    files={name:source.parent/name for name in ('apple_verifier.py','qa_purchase_runtime.py')}
    files.update({name:source/name for name in ('install_iap_sandbox.py','requirements-iap-sandbox.lock')})
    files['iap-sandbox.json']=source/'iap-sandbox.json.example'
    for folder,pattern in (('roots','*.cer'),('wheels','*.whl')):
        (args.output/folder).mkdir(mode=0o700)
        for path in (args.assets/folder).glob(pattern):files[folder+'/'+path.name]=path
    for name,path in files.items():
        shutil.copyfile(path,args.output/name);(args.output/name).chmod(0o600)
        if name!='install_iap_sandbox.py':entries[name]=hashlib.sha256(path.read_bytes()).hexdigest()
    key=Path('/Users/andou/Downloads/SubscriptionKey_2LX8L872Z8.p8')
    raw=json.dumps(dict(Files=entries,SigningKeySha256=hashlib.sha256(key.read_bytes()).hexdigest()),
                   sort_keys=True,indent=2).encode()+b'\n'
    (args.output/'manifest.json').write_bytes(raw);(args.output/'manifest.json').chmod(0o600)
    print('MANIFEST_SHA256='+hashlib.sha256(raw).hexdigest())
    print('INSTALLER_SHA256='+hashlib.sha256((args.output/'install_iap_sandbox.py').read_bytes()).hexdigest())
    print('PACKAGE_FILES='+str(len(files)))


if __name__=='__main__':main()
