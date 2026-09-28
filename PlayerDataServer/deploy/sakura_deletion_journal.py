"""Explicit adapters for a future deletion worker. No CLI, credentials or scheduler.

Only encrypted events go to the already selected private Sakura bucket. This
module never lists, deletes or changes bucket permissions; no live use in tests.
"""
import hashlib
import os
from pathlib import Path
import re
import subprocess

from deletion_journal import MAX_PAYLOAD, MAX_CIPHERTEXT

ENDPOINT = 'https://s3.tky01.sakurastorage.jp/nasus-player-backup-4433119/'
KEY = re.compile(r'deletions/[A-Za-z0-9_-]{8,80}/[a-f0-9]{32}\.json\.age')


def _private(path):
    path = Path(path)
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise ValueError('An explicit regular private configuration file is required')
    info = path.stat()
    if info.st_uid != os.geteuid() or info.st_mode & 0o077:
        raise ValueError('Unsafe deletion worker configuration permissions')
    return path


class AgeEncryptor:
    def __init__(self, binary, recipient_file, *, run=subprocess.run):
        self.binary = Path(binary)
        if not self.binary.is_absolute() or not self.binary.is_file():
            raise ValueError('An explicitly installed age binary is required')
        self.recipient = _private(recipient_file)
        self.run = run

    def __call__(self, payload):
        if not isinstance(payload, bytes) or not 0 < len(payload) <= MAX_PAYLOAD:
            raise ValueError('Invalid deletion plaintext size')
        _private(self.recipient)
        with self.recipient.open() as stream:
            recipient = stream.read(128).strip()
        if not re.fullmatch(r'age1[0-9a-z]{58}', recipient):
            raise ValueError('A single age public recipient is required; never a private identity')
        result = self.run([str(self.binary), '-r', recipient], input=payload,
                          capture_output=True, timeout=10)
        if (result.returncode or not result.stdout.startswith(b'age-encryption.org/v1\n')
                or not 64 <= len(result.stdout) <= MAX_CIPHERTEXT):
            raise RuntimeError('Deletion event encryption failed')
        return result.stdout


class SakuraPublisher:
    def __init__(self, curl_config, *, run=subprocess.run):
        self.config = _private(curl_config)
        self.run = run

    def __call__(self, key, ciphertext):
        if not isinstance(key, str) or not KEY.fullmatch(key):
            raise ValueError('Unexpected deletion object key')
        return self._transfer(key, ciphertext)

    def probe(self, key, ciphertext):
        """Explicit installer-only synthetic probe; never a deletion event."""
        if not isinstance(key, str) or not re.fullmatch(r'deletion-probes/[a-f0-9]{32}\.age', key):
            raise ValueError('Unexpected probe key')
        return self._transfer(key, ciphertext)

    def _transfer(self, key, ciphertext):
        if (not isinstance(ciphertext, bytes) or not ciphertext.startswith(b'age-encryption.org/v1\n')
                or not 64 <= len(ciphertext) <= MAX_CIPHERTEXT):
            raise ValueError('Only bounded age ciphertext may be uploaded')
        _private(self.config)
        base = ['/usr/bin/curl', '--disable', '--config', str(self.config),
                '--aws-sigv4', 'aws:amz:jp-east-1:s3', '--proto', '=https',
                '--silent', '--show-error', '--fail', '--retry', '0',
                '--connect-timeout', '10', '--max-time', '30',
                '--max-filesize', str(MAX_CIPHERTEXT), ENDPOINT + key]
        try:
            put = self.run(base + ['--request', 'PUT', '--data-binary', '@-',
                '--header', 'x-amz-acl: private', '--header', 'Content-Type: application/octet-stream',
                '--output', '/dev/null'], input=ciphertext, capture_output=True, timeout=35)
            if put.returncode:
                raise RuntimeError('Deletion event upload unverified')
            get = self.run(base + ['--request', 'GET'], capture_output=True, timeout=35)
            expected = hashlib.sha256(ciphertext).hexdigest()
            if get.returncode or len(get.stdout) != len(ciphertext) or hashlib.sha256(get.stdout).hexdigest() != expected:
                raise RuntimeError('Deletion event readback unverified')
            return expected
        except subprocess.TimeoutExpired:
            raise RuntimeError('Deletion event transfer timed out') from None
