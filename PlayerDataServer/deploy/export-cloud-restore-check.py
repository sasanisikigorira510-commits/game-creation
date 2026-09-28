"""Export only verified cloud ciphertext for a Mac-side recovery drill."""
import hashlib
import json
import os
from pathlib import Path
import pwd
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, '/usr/local/lib/nasus-backup')
from sakura_offsite_backup import curl_args, private_file


def main():
    if os.geteuid() != 0:
        raise SystemExit('Root required.')
    os.umask(0o077)
    config = Path('/etc/witch-player-offsite/curl.conf')
    private_file(config)
    # Read one immutable generation, even if the hourly timer publishes a newer one.
    report = json.loads(Path('/var/lib/witch-player-offsite/last-success.json').read_text())
    if report['Status'] != 'OFFSITE_CIPHERTEXT_VERIFIED':
        raise SystemExit('No verified generation recorded.')
    account = pwd.getpwnam('ubuntu')
    parent = Path('/home/ubuntu/nasus-backup-export')
    info = parent.lstat()
    if parent.is_symlink() or not parent.is_dir() or info.st_uid != account.pw_uid or info.st_mode & 0o077:
        raise SystemExit('Unexpected ciphertext export directory permissions.')
    with tempfile.TemporaryDirectory(prefix='nasus-cloud-restore-') as temporary:
        cipher = Path(temporary) / 'cloud-backup.tar.gz.age'
        result = subprocess.run(curl_args(config, report['Key']) + ['--output', str(cipher)],
                                stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=600)
        if result.returncode:
            raise SystemExit('Cloud download failed; no credentials were printed.')
        if cipher.stat().st_size != report['Bytes'] or hashlib.sha256(cipher.read_bytes()).hexdigest() != report['Sha256']:
            raise SystemExit('Cloud ciphertext checksum mismatch.')
        destination = Path(tempfile.mkdtemp(prefix='cloud-restore-', dir=parent))
        shutil.copyfile(cipher, destination / cipher.name)
        (destination / 'verification-source.json').write_text(json.dumps(report, sort_keys=True) + '\n')
        for item in destination.iterdir():
            item.chmod(0o600)
            os.chown(item, account.pw_uid, account.pw_gid)
        os.chown(destination, account.pw_uid, account.pw_gid)
        print('CLOUD_RESTORE_EXPORT_READY: ' + str(destination))
        print('Only ciphertext and non-secret verification metadata were exported. Live database unchanged.')


if __name__ == '__main__':
    main()
