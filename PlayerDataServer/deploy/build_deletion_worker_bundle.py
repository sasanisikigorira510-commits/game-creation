"""Build a small allowlisted sidecar bundle, never a workspace-wide archive."""
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from deploy.install_deletion_worker import FILES


def build():
    backend = Path(__file__).resolve().parents[1]
    destination = Path(tempfile.mkdtemp(prefix='nasus-deletion-worker-bundle-'))
    manifest = {}
    for name in sorted(FILES):
        source = backend / ('deploy/run_worker.py' if name == 'run_worker.py' else name)
        if source.is_symlink() or not source.is_file(): raise ValueError('Unexpected release source')
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest[name] = hashlib.sha256(target.read_bytes()).hexdigest()
    manifest_file = destination / 'manifest.json'
    manifest_file.write_text(json.dumps(manifest, sort_keys=True, indent=2) + '\n')
    installer = destination / 'install_deletion_worker.py'
    shutil.copyfile(backend / 'deploy/install_deletion_worker.py', installer)
    return dict(Directory=str(destination),
                ManifestSha256=hashlib.sha256(manifest_file.read_bytes()).hexdigest(),
                InstallerSha256=hashlib.sha256(installer.read_bytes()).hexdigest())


if __name__ == '__main__': print(json.dumps(build(), indent=2))
