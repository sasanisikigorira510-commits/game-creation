import datetime as dt
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from backup_health import healthy_marker
from deploy.publish_backup_health import collect, publish, recent_success, units_healthy


class BackupHealthTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name) / 'status.json'
        self.now = 1790262000

    def tearDown(self):
        self.temp.cleanup()

    def test_marker_requires_fresh_true_value(self):
        for value, expected in [
            ({'Healthy': True, 'CheckedUnix': self.now}, True),
            ({'Healthy': True, 'CheckedUnix': self.now - 180}, True),
            ({'Healthy': True, 'CheckedUnix': self.now - 181}, False),
            ({'Healthy': True, 'CheckedUnix': self.now + 1}, False),
            ({'Healthy': False, 'CheckedUnix': self.now}, False),
            ({'Healthy': 1, 'CheckedUnix': self.now}, False),
            ({'Healthy': True, 'CheckedUnix': float('nan')}, False),
            ({'Healthy': True, 'CheckedUnix': True}, False),
            ([], False), ({}, False)]:
            with self.subTest(value=value):
                self.path.write_text(json.dumps(value))
                self.assertEqual(expected, healthy_marker(self.path, self.now))
        self.path.write_text('{')
        self.assertFalse(healthy_marker(self.path, self.now))
        self.path.write_text(' ' * 1025)
        self.assertFalse(healthy_marker(self.path, self.now))
        self.assertFalse(healthy_marker(self.path.parent / 'absent', self.now))

    def report(self, age=0):
        return {'Status': 'OFFSITE_CIPHERTEXT_VERIFIED',
                'CompletedUtc': dt.datetime.fromtimestamp(self.now-age, dt.timezone.utc).isoformat(),
                'Key': 'NOT_FOR_PUBLICATION', 'Sha256': 'PRIVATE_METADATA'}

    def test_only_verified_cloud_backup_within_two_hours(self):
        for age, expected in [(0, True), (7200, True), (7201, False), (-1, False)]:
            self.assertEqual(expected, recent_success(self.report(age), self.now))
        for value in [{}, [], {'Status': 'failed'}, dict(self.report(), CompletedUtc='2026-09-24'),
                      dict(self.report(), Status='UPLOADED_BUT_NOT_VERIFIED')]:
            self.assertFalse(recent_success(value, self.now))

    @staticmethod
    def good_units(unit):
        return {'LoadState': 'loaded', 'ActiveState': 'active' if unit.endswith('.timer') else 'inactive',
                'Result': 'success', 'ExecMainStatus': '0'}

    def test_failed_services_and_stopped_timers(self):
        self.assertTrue(units_healthy(self.good_units))
        for name, changes in [('witch-player-offsite.timer', {'ActiveState': 'inactive'}),
                              ('witch-player-backup.timer', {'LoadState': 'not-found'}),
                              ('witch-player-offsite.service', {'Result': 'exit-code'}),
                              ('witch-player-backup.service', {'ExecMainStatus': '1'}),
                              ('witch-player-offsite.service', {'ActiveState': 'failed'})]:
            def read(unit):
                return dict(self.good_units(unit), **(changes if unit == name else {}))
            self.assertFalse(units_healthy(read))

    def test_collector_publishes_no_private_fields_and_fails_closed(self):
        self.path.write_text(json.dumps(self.report()))
        value = collect(self.path, self.now, self.good_units)
        self.assertEqual({'Healthy': True, 'CheckedUnix': self.now}, value)
        target = self.path.parent / 'published.json'
        publish(value, target)
        self.assertTrue(healthy_marker(target, self.now))
        self.assertEqual(0o644, target.stat().st_mode & 0o777)
        self.assertNotIn('PRIVATE', target.read_text())
        def fail(unit):
            raise subprocess.TimeoutExpired('systemctl', 10)
        self.assertFalse(collect(self.path, self.now, fail)['Healthy'])
        self.path.write_text('invalid')
        self.assertFalse(collect(self.path, self.now, self.good_units)['Healthy'])


if __name__ == '__main__':
    unittest.main()
