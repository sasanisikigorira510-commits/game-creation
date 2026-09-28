import datetime as dt
from pathlib import Path
import subprocess
import tempfile
import unittest

from deploy.observe_backup_health import observe


class HealthObserverTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.report = Path(self.temp.name) / 'report.json'
        raw = (Path(__file__).parents[1] / 'deploy/publish_backup_health.py').read_text()
        self.scope = {'__name__': 'test_publisher'}
        exec(compile(raw, 'publisher.py', 'exec'), self.scope)
        original_collect = self.scope['collect']
        self.now = 1790341372
        self.scope['collect'] = lambda **kw: original_collect(self.report, self.now, **kw)
        self.scope['unit_properties'] = self.good
        self.valid_report()

    @staticmethod
    def good(unit):
        return {'LoadState': 'loaded', 'ActiveState': 'active' if unit.endswith('.timer') else 'inactive',
                'Result': 'success', 'ExecMainStatus': '0'}

    def valid_report(self, age=0):
        import json
        self.report.write_text(json.dumps({'Status': 'OFFSITE_CIPHERTEXT_VERIFIED',
            'CompletedUtc': dt.datetime.fromtimestamp(self.now-age, dt.timezone.utc).isoformat(),
            'Key': 'SECRET', 'Sha256': 'SECRET'}))

    def result(self):
        expected = self.scope['collect'](read=self.scope['unit_properties'])
        original = self.scope['recent_success']
        actual, reason = observe(self.scope)
        self.assertEqual(expected, actual)
        self.assertIs(original, self.scope['recent_success'])
        self.assertEqual({'Healthy', 'CheckedUnix'}, set(actual))
        self.assertNotIn('SECRET', str(reason))
        return reason

    def test_healthy_and_boundary_decisions_unchanged(self):
        for age in (0, 7200, 7201, -1):
            self.valid_report(age)
            self.assertEqual('HEALTHY' if 0 <= age <= 7200 else 'BACKUP_REPORT_REJECTED',
                             self.result()['Reason'])

    def test_file_parse_and_size_failures_unchanged(self):
        for raw in ('{', ' ' * 4097):
            self.report.write_text(raw)
            self.assertEqual('REPORT_READ_SIZE_OR_PARSE_FAILED', self.result()['Reason'])
        self.report.unlink()
        self.assertEqual('REPORT_READ_SIZE_OR_PARSE_FAILED', self.result()['Reason'])

    def test_stopped_timer_reports_fixed_unit_only(self):
        self.scope['unit_properties'] = lambda unit: dict(self.good(unit), ActiveState='inactive', Environment='SECRET')
        self.assertEqual({'Reason': 'UNIT_STATE_REJECTED', 'Unit': 'witch-player-offsite.timer'}, self.result())

    def test_command_failure_never_prints_exception(self):
        def fail(unit):
            raise subprocess.CalledProcessError(1, 'SECRET', stderr='SECRET')
        self.scope['unit_properties'] = fail
        self.assertEqual({'Reason': 'UNIT_QUERY_FAILED', 'Unit': 'witch-player-offsite.timer'}, self.result())

    def test_only_one_collection_no_reprobe_or_publish(self):
        seen = []
        self.scope['unit_properties'] = lambda unit: seen.append(unit) or self.good(unit)
        self.scope['publish'] = lambda _: self.fail('observe must not publish')
        value, reason = observe(self.scope)
        self.assertTrue(value['Healthy'])
        self.assertEqual('HEALTHY', reason['Reason'])
        self.assertEqual(4, len(seen))
        self.assertEqual(4, len(set(seen)))


if __name__ == '__main__':
    unittest.main()
