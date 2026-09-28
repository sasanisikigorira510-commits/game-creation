import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
from deploy import backup_apple_keys as backup


class AppleKeyBackupTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.keys = self.root/'keys'
        self.keys.mkdir(mode=0o700)
        self.signing = ec.generate_private_key(ec.SECP256R1()).private_bytes(
            serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8,
            serialization.NoEncryption())
        self.token = os.urandom(32)
        for name, raw in zip(backup.FILES, (self.signing, self.token)):
            backup.new_file(self.keys/name, raw)

    def test_payload_exact_key_set_and_digests(self):
        value = json.loads(backup.payload(self.keys))
        self.assertEqual(set(value['Files']), set(backup.FILES))
        self.assertEqual(value['KeyId'], '38GM3RYVD9')
        for name, raw in zip(backup.FILES, (self.signing, self.token)):
            entry = value['Files'][name]
            self.assertEqual(base64.b64decode(entry['Base64']), raw)
            self.assertEqual(entry['Sha256'], hashlib.sha256(raw).hexdigest())

    def test_bad_token_length_preserves_originals(self):
        path = self.keys/backup.FILES[1]
        path.write_bytes(b'wrong')
        with self.assertRaises(ValueError): backup.payload(self.keys)
        self.assertEqual(path.read_bytes(), b'wrong')

    def test_public_key_permissions_rejected(self):
        (self.keys/backup.FILES[0]).chmod(0o644)
        with self.assertRaises(ValueError): backup.payload(self.keys)

    def test_symlink_key_and_parent_rejected(self):
        link = self.root/'link'
        link.symlink_to(self.keys, target_is_directory=True)
        with self.assertRaises(ValueError): backup.payload(link)
        with self.assertRaises(ValueError): backup.private_read(link/backup.FILES[0], 16384)

    def test_new_file_never_overwrites(self):
        path = self.keys/backup.FILES[1]
        with self.assertRaises(FileExistsError): backup.new_file(path, b'new')
        self.assertEqual(path.read_bytes(), self.token)

    def test_subprocess_failure_does_not_expose_stderr(self):
        with patch.object(subprocess, 'run', return_value=subprocess.CompletedProcess(
                [], 1, stdout=b'', stderr=b'sensitive-details')):
            with self.assertRaises(ValueError) as error:
                backup.run_age(Path('/unused/age'), [])
        self.assertNotIn('sensitive', str(error.exception))

    def test_real_age_roundtrip_and_tamper_rejection(self):
        executable = os.environ.get('NASUS_TEST_AGE')
        if not executable: self.skipTest('NASUS_TEST_AGE required for real encryption')
        age = Path(executable)
        identity = self.root/'identity'
        generated = subprocess.run([str(age.parent/'age-keygen')], capture_output=True, check=True)
        backup.new_file(identity, generated.stdout)
        recipient = self.root/'recipient'
        backup.new_file(recipient, backup.run_age(age.parent/'age-keygen', ['-y', identity]))
        destination = self.root/'recovery'
        result = backup.create_backup(self.keys, recipient, identity, age, destination)
        self.assertEqual(result['Status'], 'APPLE_KEY_CIPHERTEXT_VERIFIED')
        self.assertFalse(result['IndependentCopyVerified'])
        self.assertEqual(set(p.name for p in destination.iterdir()),
                         {'apple-recovery-v1.json.age', 'SHA256SUMS', 'README.txt', 'verified.json'})
        for path in destination.iterdir():
            self.assertNotIn(self.signing, path.read_bytes())
            self.assertNotIn(self.token, path.read_bytes())
            self.assertEqual(path.stat().st_mode & 0o777, 0o600)
        encrypted = (destination/'apple-recovery-v1.json.age').read_bytes()
        decoded = backup.run_age(age, ['-d', '-i', identity], encrypted)
        self.assertEqual(decoded, backup.payload(self.keys))
        with self.assertRaises(ValueError):
            backup.run_age(age, ['-d', '-i', identity], encrypted[:-1]+bytes([encrypted[-1] ^ 1]))
        with self.assertRaises(FileExistsError):
            backup.create_backup(self.keys, recipient, identity, age, destination)

    def test_mismatched_recipient_no_backup_written(self):
        identity = self.root/'identity'
        recipient = self.root/'recipient'
        backup.new_file(identity, b'synthetic-private-key')
        backup.new_file(recipient, b'age1intended')
        with patch.object(backup, 'run_age', return_value=b'age1wrong'):
            with self.assertRaises(ValueError):
                backup.create_backup(self.keys, recipient, identity, Path('/age'), self.root/'out')
        self.assertFalse((self.root/'out').exists())
