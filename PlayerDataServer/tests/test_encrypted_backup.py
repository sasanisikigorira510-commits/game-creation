import io
import json
import os
import subprocess
import sys
import tarfile
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from maintenance import initialize, backup_generation
from store import Store
from deploy.verify_encrypted_backup import verify_payload, verify_encrypted


class EncryptedBackupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        initialize(self.root / 'data', 'synthetic-backup-test')
        Store(self.root / 'data/players.sqlite').register('0123456789abcdef0123456789abcdef', 't' * 64)
        path = backup_generation(self.root / 'data/players.sqlite', self.root / 'backups')
        self.members = [(path.name, path.read_bytes()), (path.name + '.json', Path(str(path) + '.json').read_bytes())]

    def tearDown(self):
        self.temp.cleanup()

    def payload(self, members=None):
        result = io.BytesIO()
        with tarfile.open(fileobj=result, mode='w:gz') as archive:
            for name, data in (self.members if members is None else members):
                info = tarfile.TarInfo(name)
                info.size = len(data)
                archive.addfile(info, io.BytesIO(data))
        return result.getvalue()

    def test_snapshot_restores_to_separate_memory_database(self):
        report = verify_payload(self.payload())
        self.assertEqual('RESTORE_VERIFIED_IN_MEMORY', report['Status'])
        self.assertFalse(report['PlaintextWrittenToDisk'])

    def test_manifest_mismatch_rejected(self):
        manifest = json.loads(self.members[1][1])
        manifest['Sha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'checksum'):
            verify_payload(self.payload([self.members[0], (self.members[1][0], json.dumps(manifest).encode())]))

    def test_unexpected_member_rejected(self):
        with self.assertRaises(ValueError):
            verify_payload(self.payload(self.members + [('../unexpected', b'no')]))

    @unittest.skipUnless(os.environ.get('NASUS_TEST_AGE'), 'age integration binary not supplied')
    def test_encryption_roundtrip_wrong_key_and_tampering(self):
        age = Path(os.environ['NASUS_TEST_AGE'])
        keygen = age.with_name('age-keygen')
        key = self.root / 'identity'
        wrong_key = self.root / 'wrong-identity'
        for path in (key, wrong_key):
            subprocess.run([str(keygen), '-o', str(path)], capture_output=True, check=True)
            path.chmod(0o600)
        recipient = subprocess.check_output([str(keygen), '-y', str(key)]).decode().strip()
        encrypted = self.root / 'backup.age'
        subprocess.run([str(age), '-r', recipient, '-o', str(encrypted)], input=self.payload(), capture_output=True, check=True)
        self.assertEqual('RESTORE_VERIFIED_IN_MEMORY', verify_encrypted(age, key, encrypted)['Status'])
        with self.assertRaisesRegex(ValueError, 'Decryption'):
            verify_encrypted(age, wrong_key, encrypted)
        damaged = bytearray(encrypted.read_bytes())
        damaged[-1] ^= 1
        tampered = self.root / 'tampered.age'
        tampered.write_bytes(damaged)
        with self.assertRaisesRegex(ValueError, 'Decryption'):
            verify_encrypted(age, key, tampered)


if __name__ == '__main__':
    unittest.main()
