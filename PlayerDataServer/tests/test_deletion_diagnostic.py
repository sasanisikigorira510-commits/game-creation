import unittest
from types import SimpleNamespace
from unittest.mock import patch

from deploy.diagnose_deletion_worker import drop_privileges, minimal_root_context, unprivileged_context


class DiagnosticTests(unittest.TestCase):
    def test_boundary_failures_show_stage_not_sensitive_message(self):
        account = SimpleNamespace(pw_gid=123, pw_uid=456)
        for stage in ('setgroups', 'setgid', 'setuid'):
            with patch('signal.alarm'), patch('os.setgroups'), patch('os.setgid'), patch('os.setuid'), patch('os.umask'):
                with patch('os.' + stage, side_effect=PermissionError(1, 'sensitive-value-not-for-output')):
                    value = drop_privileges(account, lambda: self.fail('must not call database'))
            self.assertFalse(value['OK'])
            self.assertEqual(stage, value['BoundaryPhase'])
            self.assertEqual(1, value['Error']['Errno'])
            self.assertNotIn('sensitive-value-not-for-output', str(value))

    def test_success_and_callback_failure_are_distinct(self):
        with patch('signal.alarm'), patch('os.setgroups'), patch('os.setgid'), patch('os.setuid'), patch('os.umask'):
            account = SimpleNamespace(pw_gid=123, pw_uid=456)
            result = drop_privileges(account, lambda: None)
            self.assertTrue(result['OK'])
            self.assertIsNone(result['Value'])
            def fail(): raise ValueError('private data')
            value = drop_privileges(account, fail)
            self.assertEqual('database_callback', value['BoundaryPhase'])
            self.assertNotIn('private data', str(value))

    def test_minimal_parent_rejects_missing_and_excess_privilege(self):
        good = dict(NoNewPrivs='1', CapEff='c0', CapPrm='c0', CapBnd='c0', CapInh='c0', CapAmb='c0')
        self.assertTrue(minimal_root_context(good))
        for key in ('CapEff', 'CapPrm', 'CapBnd'):
            for bits in ('40', '1c0', 'invalid'):
                self.assertFalse(minimal_root_context(dict(good, **{key: bits})))
        self.assertFalse(minimal_root_context(dict(good, NoNewPrivs='0')))
        self.assertFalse(minimal_root_context({}))

    def test_privilege_remaining_after_uid_drop_blocks_database(self):
        account = SimpleNamespace(pw_uid=456, pw_gid=123)
        good = dict(Uid='456 456 456 456', Gid='123 123 123 123', CapEff='0', CapPrm='0', CapAmb='0')
        self.assertTrue(unprivileged_context(good, account))
        bad = dict(good, CapPrm='c0')
        self.assertFalse(unprivileged_context(bad, account))
        with patch('signal.alarm'), patch('os.setgroups'), patch('os.setgid'), patch('os.setuid'), patch('os.umask'), patch('deploy.diagnose_deletion_worker.security_context', return_value=bad):
            value = drop_privileges(account, lambda: self.fail('database must remain untouched'), require_minimal=True)
        self.assertFalse(value['OK'])
        self.assertEqual('post_drop_capabilities', value['BoundaryPhase'])
