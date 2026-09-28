"""Interactive, no-echo secret entry on the VPS. Never pass credentials in argv."""
import getpass
import os
from pathlib import Path
import re


def main():
    os.umask(0o077)
    target = Path('/etc/witch-player-offsite/curl.conf')
    if os.geteuid() != 0 or target.exists():
        raise SystemExit('Root required; existing credentials will not be overwritten.')
    key = getpass.getpass('Access key ID (hidden): ')
    secret = getpass.getpass('Secret access key (hidden): ')
    if not re.fullmatch(r'[A-Za-z0-9]{16,128}', key) or not re.fullmatch(r'[A-Za-z0-9+/=]{20,256}', secret):
        raise SystemExit('Invalid credential format; nothing saved.')
    confirm = getpass.getpass('Repeat secret access key (hidden): ')
    if secret != confirm:
        raise SystemExit('Secrets differ; nothing saved.')
    fd = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'w') as out:
        out.write('user = "' + key + ':' + secret + '"\n')
        out.flush()
        os.fsync(out.fileno())
    print('Dedicated credentials saved root-only. No secret was printed.')


if __name__ == '__main__':
    main()
