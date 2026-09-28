import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from deploy import diagnose_apple_activation as diagnostic


class DiagnosticTests(unittest.TestCase):
    def setUp(self):
        temp=tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.path=Path(temp.name)/'marker'

    def test_marker_exposes_only_allowlisted_status_and_numeric_age(self):
        self.path.write_text(json.dumps(dict(Healthy=True,CheckedUnix=100,
            Status='secret-value',Token='private-token',Queue={'secret':'hidden'})))
        with patch.object(diagnostic.time,'time',return_value=101):
            value,stamp=diagnostic.safe_marker(self.path)
        self.assertEqual(value,dict(Healthy=True,Status='UNRECOGNIZED',AgeSeconds=1))
        self.assertEqual(stamp,100)
        self.assertNotIn('private',json.dumps(value))

    def test_bad_timestamps_and_large_markers_rejected(self):
        for stamp in (True,None,float('nan'),float('inf')):
            self.path.write_text(json.dumps(dict(CheckedUnix=stamp)))
            with self.assertRaises(ValueError): diagnostic.safe_marker(self.path)
        self.path.write_bytes(b'x'*4097)
        with self.assertRaises(ValueError): diagnostic.safe_marker(self.path)

    def test_wrapper_pins_diagnostic_and_no_mutating_commands(self):
        folder=Path(__file__).resolve().parents[1]/'deploy'
        wrapper=(folder/'run-apple-activation-diagnostic-mac.sh').read_text()
        code=(folder/'diagnose_apple_activation.py').read_bytes()
        self.assertIn(hashlib.sha256(code).hexdigest(),wrapper)
        for text in ('systemctl start','systemctl enable','NOPASSWD','base_health.publish('):
            self.assertNotIn(text,wrapper+code.decode())
