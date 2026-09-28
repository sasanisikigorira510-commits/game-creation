"""Disposable markers reproduce a writer finishing during the health check."""
import datetime as dt
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy.publish_backup_health import collect


class ConcurrentHealthTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.backup = Path(self.temp.name) / 'backup.json'
        self.worker = Path(self.temp.name) / 'worker.json'
        self.write_backup(999)
        self.worker.write_text(json.dumps({'Healthy': True, 'CheckedUnix': 999}))
        self.worker.chmod(0o600)

    def write_backup(self, stamp):
        self.backup.write_text(json.dumps({'Status': 'OFFSITE_CIPHERTEXT_VERIFIED',
            'CompletedUtc': dt.datetime.fromtimestamp(stamp, dt.timezone.utc).isoformat()}))

    @staticmethod
    def units(name):
        return {'LoadState': 'loaded', 'ActiveState': 'active' if name.endswith('.timer') else 'inactive',
                'Result': 'success', 'ExecMainStatus': '0'}

    def test_backup_completed_after_check_started_is_not_future(self):
        # collect starts at 1000; the atomic report replacement completes before
        # the file is read, at 1000.5; wall time at validation is 1001.
        self.write_backup(1000.5)
        with patch('deploy.publish_backup_health.time.time', side_effect=[1000, 1001]):
            value = collect(self.backup, read=self.units)
        self.assertTrue(value['Healthy'])
        self.assertEqual(1000, value['CheckedUnix'])

    def test_worker_finishes_during_unit_queries(self):
        def read(name):
            if name == 'witch-player-deletion.service':
                self.worker.write_text(json.dumps({'Healthy': True, 'CheckedUnix': 1000.5}))
            return self.units(name)
        with patch('deploy.publish_backup_health.time.time', side_effect=[1000, 1000, 1001]):
            value = collect(self.backup, read=read, require_deletion_worker=True, deletion_report=self.worker)
        self.assertTrue(value['Healthy'])

    def test_actual_future_marker_still_rejected(self):
        self.write_backup(1002)
        with patch('deploy.publish_backup_health.time.time', side_effect=[1000, 1001]):
            self.assertFalse(collect(self.backup, read=self.units)['Healthy'])

    def test_worker_future_stale_failed_and_public_are_still_rejected(self):
        for stamp, healthy, mode in ((1002, True, 0o600), (699, True, 0o600),
                                     (999, False, 0o600), (999, True, 0o644)):
            self.worker.write_text(json.dumps({'Healthy': healthy, 'CheckedUnix': stamp}))
            self.worker.chmod(mode)
            self.assertFalse(collect(self.backup, 1000, self.units, require_deletion_worker=True,
                                     deletion_report=self.worker)['Healthy'])

    def test_reason_codes_and_public_fields_are_separate(self):
        reasons = []
        value = collect(self.backup, 1000, self.units, require_deletion_worker=True,
                        deletion_report=self.worker, on_reason=reasons.append)
        self.assertEqual({'Healthy': True, 'CheckedUnix': 1000}, value)
        self.assertEqual([{'Reason': 'HEALTHY'}], reasons)
        reasons.clear()
        def bad(name):
            return dict(self.units(name), ActiveState='inactive')
        collect(self.backup, 1000, bad, on_reason=reasons.append)
        self.assertEqual([{'Reason': 'TIMER_STATE_REJECTED', 'Unit': 'witch-player-offsite.timer'}], reasons)


if __name__ == '__main__': unittest.main()
