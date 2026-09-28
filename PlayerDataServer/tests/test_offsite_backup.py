import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from deploy.sakura_offsite_backup import curl_args, reserve_budget, transfer


class OffsiteTests(unittest.TestCase):
    def test_fixed_tls_endpoint_and_config_credentials(self):
        args = curl_args(Path('/private/curl.conf'), 'db/20260924T120000Z-' + 'a' * 32 + '.tar.gz.age')
        self.assertIn('aws:amz:jp-east-1:s3', args)
        self.assertIn('=https', args)
        self.assertNotIn('--insecure', args)
        self.assertNotIn('--location', args)
        self.assertNotIn('--user', args)
        self.assertEqual('--disable', args[1])
        with self.assertRaises(ValueError):
            curl_args(Path('/private/curl.conf'), '../other')

    def test_budget_reserved_before_upload_and_ceiling(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'budget.json'
            with patch('deploy.sakura_offsite_backup.TOTAL_BUDGET', 10):
                reserve_budget(path, 6)
                self.assertEqual(6, json.loads(path.read_text())['ReservedBytes'])
                with self.assertRaises(ValueError):
                    reserve_budget(path, 5)
                self.assertEqual(6, json.loads(path.read_text())['ReservedBytes'])
                self.assertEqual(0, path.stat().st_mode & 0o077)

    def test_upload_private_and_readback_hash(self):
        with tempfile.TemporaryDirectory() as temp:
            source, dest = Path(temp) / 'source.age', Path(temp) / 'readback.age'
            source.write_bytes(b'ciphertext')
            calls = []
            def fake(args, **kwargs):
                calls.append(args)
                if '--upload-file' not in args:
                    dest.write_bytes(source.read_bytes())
                return subprocess.CompletedProcess(args, 0)
            key = 'db/20260924T120000Z-' + 'b' * 32 + '.tar.gz.age'
            transfer(Path('/private/config'), key, source, dest, run=fake)
            self.assertIn('x-amz-acl: private', calls[0])
            self.assertEqual(2, len(calls))
            def corrupt(args, **kwargs):
                dest.write_bytes(b'corrupted!')
                return subprocess.CompletedProcess(args, 0)
            with self.assertRaises(ValueError):
                transfer(Path('/private/config'), key, source, dest, run=corrupt)
            def fail(args, **kwargs):
                return subprocess.CompletedProcess(args, 22, stderr=b'sensitive provider body')
            with self.assertRaisesRegex(RuntimeError, '^S3 transfer failed'):
                transfer(Path('/private/config'), key, source, dest, run=fail)


if __name__ == '__main__':
    unittest.main()
