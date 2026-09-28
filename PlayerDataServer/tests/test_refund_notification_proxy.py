import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy import install_refund_notification_proxy as proxy


class NotificationProxyTest(unittest.TestCase):
    def test_only_one_exact_post_route_added_and_original_preserved(self):
        original = b'example.test {\n    handle_path /qa/* {\n        respond 404\n    }\n}\n'
        with patch.object(proxy, 'PREVIOUS_SHA', hashlib.sha256(original).hexdigest()):
            result = proxy.candidate(original)
        self.assertEqual(original, result.replace(proxy.ROUTE, b'', 1))
        self.assertEqual(1, result.count(proxy.ROUTE))
        self.assertIn(b'path ' + proxy.PUBLIC_PATH.encode() + b'\n', proxy.ROUTE)
        self.assertIn(b'method POST\n', proxy.ROUTE)
        self.assertIn(b'reverse_proxy 127.0.0.1:8793', proxy.ROUTE)
        self.assertNotIn(b'8791', proxy.ROUTE)
        self.assertNotIn(b'8792', proxy.ROUTE)
        self.assertNotIn(b'path /sandbox-refund-20260928/*', proxy.ROUTE)

    def test_drift_and_duplicate_anchor_rejected(self):
        with self.assertRaises(ValueError):
            proxy.candidate(b'changed')
        for original in (b'no anchor', b'    handle_path /qa/* {\n' * 2):
            with patch.object(proxy, 'PREVIOUS_SHA', hashlib.sha256(original).hexdigest()):
                with self.assertRaises(ValueError):
                    proxy.candidate(original)

    def test_backup_cannot_overwrite_and_mode_is_explicit(self):
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory)/'backup'
            proxy.write_new(file, b'original')
            self.assertEqual(0o600, file.stat().st_mode & 0o777)
            with self.assertRaises(FileExistsError):
                proxy.write_new(file, b'replacement')
            self.assertEqual(b'original', file.read_bytes())
