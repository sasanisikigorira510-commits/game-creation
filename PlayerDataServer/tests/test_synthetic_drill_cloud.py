import hashlib
from pathlib import Path
import subprocess
import tempfile
import unittest

from deploy.sakura_deletion_journal import SakuraPublisher
from deploy.synthetic_drill_cloud import readback, validate_transfer


class SyntheticCloudTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.config = Path(self.temp.name) / 'config'
        self.config.write_text('synthetic'); self.config.chmod(0o600)
        self.cipher = b'age-encryption.org/v1\n' + b'x' * 100
        self.manifest = dict(Kind='SYNTHETIC_DELETION_PROBE', Key='deletion-probes/'+'a'*32+'.age',
                            Bytes=len(self.cipher), Sha256=hashlib.sha256(self.cipher).hexdigest())

    def test_only_private_probe_put_then_verified_get(self):
        calls = []
        def run(args, **kwargs):
            calls.append((args, kwargs))
            return subprocess.CompletedProcess(args, 0, self.cipher if args[-1] == 'GET' else b'', b'')
        raw, receipt = readback(self.manifest, self.cipher, SakuraPublisher, self.config, run)
        self.assertEqual(self.cipher, raw)
        self.assertEqual('SYNTHETIC_PROBE_READBACK_VERIFIED', receipt['Status'])
        self.assertEqual(2, len(calls))
        self.assertIn('x-amz-acl: private', calls[0][0])
        self.assertEqual(self.cipher, calls[0][1]['input'])

    def test_disallows_live_keys_and_invalid_payload_before_network(self):
        for updates in ({'Key': 'deletions/live/'+'a'*32+'.json.age'}, {'Bytes': True},
                        {'Sha256': '0'*64}, {'Key': '../secret'}, {'Unexpected': 'x'}):
            with self.subTest(updates=updates), self.assertRaises(ValueError):
                validate_transfer(dict(self.manifest, **updates), self.cipher)

    def test_rejects_changed_readback(self):
        def run(args, **kwargs):
            return subprocess.CompletedProcess(args, 0, b'changed', b'')
        with self.assertRaises(RuntimeError):
            readback(self.manifest, self.cipher, SakuraPublisher, self.config, run)

    def test_failed_upload_does_not_get(self):
        calls = []
        def run(args, **kwargs):
            calls.append(args); return subprocess.CompletedProcess(args, 22, b'', b'private-error')
        with self.assertRaises(RuntimeError):
            readback(self.manifest, self.cipher, SakuraPublisher, self.config, run)
        self.assertEqual(1, len(calls))
