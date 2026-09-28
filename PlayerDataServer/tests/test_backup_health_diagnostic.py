import datetime as dt
import io
import json
from contextlib import redirect_stdout
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from deploy import diagnose_backup_health as d


class HealthDiagnosticTests(unittest.TestCase):
    def test_backup_summary_excludes_object_names_and_other_fields(self):
        raw = json.dumps({'Status': 'OFFSITE_CIPHERTEXT_VERIFIED',
                          'CompletedUtc': '2026-09-25T12:00:00+00:00',
                          'Key': 'SECRET_OBJECT', 'Sha256': 'SECRET_HASH',
                          'Unexpected': 'SECRET_EXTRA'}).encode()
        now = dt.datetime.fromisoformat('2026-09-25T12:01:00+00:00').timestamp()
        result = d.backup_summary(raw, now)
        self.assertEqual(60, result['AgeSeconds'])
        self.assertTrue(result['AgeWithinTwoHours'])
        self.assertNotIn('SECRET', json.dumps(result))
        self.assertFalse(d.backup_summary(raw, now - 120)['AgeWithinTwoHours'])
        self.assertFalse(d.backup_summary(raw, now + 7200)['AgeWithinTwoHours'])

    def test_bounded_and_invalid_timestamp(self):
        self.assertEqual({'BoundedSize': False}, d.backup_summary(b'x' * 4097, 0))
        self.assertFalse(d.backup_summary(b'{}', 0)['TimestampValid'])
        self.assertEqual({'OK': False, 'ErrorType': 'PermissionError', 'Errno': 13},
                         d.attempt(lambda: (_ for _ in ()).throw(PermissionError(13, 'SECRET'))))

    def test_unit_probe_does_not_return_stderr_or_unknown_properties(self):
        reply = SimpleNamespace(returncode=1, stdout='LoadState=loaded\nEnvironment=SECRET\n',
                                stderr='Failed to connect to bus: Permission denied SECRET')
        with patch.object(d.subprocess, 'run', return_value=reply): result = d.unit_probe('fixture.service')
        self.assertEqual({'LoadState': 'loaded'}, result['Properties'])
        self.assertTrue(result['PermissionError']); self.assertTrue(result['BusError'])
        self.assertNotIn('SECRET', json.dumps(result))

    def test_sandbox_preserves_existing_protections_and_has_no_persistent_unit(self):
        cmd = d.sandbox_command(Path('/run/fixture/diagnose.py'))
        for arg in ('--collect', '--property=NoNewPrivileges=true', '--property=PrivateTmp=true',
                    '--property=ProtectSystem=strict', '--property=ProtectHome=true',
                    '--property=RestrictAddressFamilies=AF_UNIX', '--property=MemoryMax=64M'):
            self.assertIn(arg, cmd)
        self.assertFalse(any('AmbientCapabilities' in arg or 'CapabilityBoundingSet' in arg for arg in cmd))
        self.assertIn('--context=service-sandbox', cmd)

    def test_snapshot_does_not_publish_and_only_collects_sanitized_metadata(self):
        def forbidden(): raise AssertionError('must never publish')
        scope = {'units_healthy': lambda: True,
                 'collect': lambda: {'Healthy': True, 'CheckedUnix': 100}, 'publish': forbidden}
        output = io.StringIO()
        with patch.object(d, 'publisher', return_value=scope), patch.object(d, 'process_metadata', return_value={}), \
             patch.object(d, 'metadata', return_value={}), patch.object(d, 'read_bounded', return_value=b'{}'), \
             patch.object(d, 'unit_probe', return_value={}), patch.object(d, 'public_health', return_value={}), \
             redirect_stdout(output):
            d.snapshot('normal-root')
        report = json.loads(output.getvalue().split('=', 1)[1])
        self.assertTrue(report['OriginalCollect']['Value']['Healthy'])


if __name__ == '__main__': unittest.main()
