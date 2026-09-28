"""No sudo, production data or cloud access: disposable files and mocked units."""
from contextlib import ExitStack, redirect_stdout
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

from deploy import deletion_maintenance as m


class MaintenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.stack = ExitStack(); self.addCleanup(self.stack.close)
        for key, value in [('ROOT', self.root), ('CAP', self.root / 'cap.d' / 'cap.conf'),
                           ('HOOK', self.root / 'health.d' / 'health.conf'),
                           ('POLICY', self.root / 'sudoers')]:
            self.stack.enter_context(patch.object(m, key, value))
        self.stack.enter_context(patch.object(m, 'trusted'))
        self.output = io.StringIO(); self.stack.enter_context(redirect_stdout(self.output))

    def grant(self, issued=None):
        value = {'Version': 1, 'IssuedUnix': time.time() if issued is None else issued}
        (self.root / 'grant.json').write_text(json.dumps(value))
        return value

    def test_scope_and_expiry(self):
        self.grant()
        m.authorize('apply'); m.authorize('verify')
        with self.assertRaises(RuntimeError): m.authorize('shell')
        self.grant(time.time() - 7201)
        for action in ('apply', 'verify'):
            with self.assertRaises(RuntimeError): m.authorize(action)
        # Removing the permission and restoring our own settings remain possible.
        m.authorize('revoke'); m.authorize('rollback')
        self.grant(time.time() + 10)
        with self.assertRaises(RuntimeError): m.authorize('apply')

    def test_revoke_archives_only_matching_policy(self):
        raw = b'fixture exact command permission\n'; m.POLICY.write_bytes(raw)
        grant = {'PolicySha256': hashlib.sha256(raw).hexdigest()}
        m.revoke(grant)
        self.assertFalse(m.POLICY.exists())
        self.assertEqual(raw, (self.root / 'authorization.revoked').read_bytes())
        m.revoke(grant)

    def test_revoke_refuses_changed_policy(self):
        m.POLICY.write_text('another policy')
        with self.assertRaises(RuntimeError): m.revoke({'PolicySha256': '0' * 64})
        self.assertTrue(m.POLICY.exists())

    def test_dropin_exclusive_and_rollback_preserves_changes(self):
        m.owned_dropin(m.CAP, m.CAP_TEXT)
        with self.assertRaises(FileExistsError): m.owned_dropin(m.CAP, m.CAP_TEXT)
        m.CAP.write_text('unrelated edit')
        with self.assertRaises(RuntimeError): m.archive_dropin(m.CAP, m.CAP_TEXT, 'saved.conf')
        self.assertEqual('unrelated edit', m.CAP.read_text())

    def mock_apply(self):
        self.calls = []
        def ctl(*args, **kwargs):
            self.calls.append(args)
            return 'disabled' if args[0] == 'is-enabled' else ''
        def prop(unit, key):
            if key == 'DropInPaths': return ''
            if key == 'ActiveState': return 'inactive'
            raise AssertionError((unit, key))
        for name in ('unchanged', 'base_health', 'queue_empty', 'check_settings', 'run'):
            self.stack.enter_context(patch.object(m, name))
        self.stack.enter_context(patch.object(m, 'ctl', side_effect=ctl))
        self.stack.enter_context(patch.object(m, 'prop', side_effect=prop))
        self.stack.enter_context(patch.object(m, 'check_worker', side_effect=lambda: time.time()))
        self.stack.enter_context(patch.object(m, 'verify', return_value={'Status': 'fixture verified'}))

    def test_apply_order_and_no_repeat(self):
        self.mock_apply(); m.apply()
        self.assertLess(self.calls.index(('start', m.SERVICE)), self.calls.index(('enable', '--now', m.TIMER)))
        self.assertTrue(m.CAP.exists()); self.assertTrue(m.HOOK.exists())
        self.assertTrue((self.root / 'applied.json').exists())
        self.assertFalse(any('witch-player.service' in command for command in self.calls))
        self.assertEqual({'cap_setuid', 'cap_setgid'},
                         set(m.CAP_TEXT.split('CapabilityBoundingSet=')[2].splitlines()[0].lower().split()))
        with self.assertRaises(RuntimeError): m.apply()

    def test_initial_start_failure_rolls_back_without_enabling_timer(self):
        self.mock_apply()
        def failed(*args, **kwargs):
            self.calls.append(args)
            if args == ('start', m.SERVICE): raise RuntimeError('fixture')
            return 'disabled' if args[0] == 'is-enabled' else ''
        with patch.object(m, 'ctl', side_effect=failed):
            with self.assertRaises(RuntimeError): m.apply()
        self.assertNotIn(('enable', '--now', m.TIMER), self.calls)
        self.assertFalse(m.CAP.exists()); self.assertFalse(m.HOOK.exists())
        self.assertTrue((self.root / 'disabled-capability-hook.conf').exists())

    def test_health_failure_stops_timer_and_restores_hooks(self):
        self.mock_apply()
        with patch.object(m, 'verify', side_effect=RuntimeError('fixture')):
            with self.assertRaises(RuntimeError): m.apply()
        self.assertIn(('disable', '--now', m.TIMER), self.calls)
        self.assertFalse(m.CAP.exists()); self.assertFalse(m.HOOK.exists())
        self.assertTrue((self.root / 'disabled-health-hook.conf').exists())

    def test_nonempty_queue_stops_before_any_changes(self):
        self.mock_apply()
        with patch.object(m, 'queue_empty', side_effect=RuntimeError('pending event')):
            with self.assertRaises(RuntimeError): m.apply()
        self.assertEqual([], self.calls)
        self.assertFalse((self.root / 'apply-started.json').exists())

    def test_rollback_without_apply_cannot_touch_service(self):
        with patch.object(m, 'ctl') as ctl:
            with self.assertRaises(RuntimeError): m.rollback()
        ctl.assert_not_called()

    def test_new_file_refuses_symlink_and_existing_file(self):
        dst = self.root / 'dst'; dst.write_text('preserve')
        link = self.root / 'link'; link.symlink_to(dst)
        for path in (dst, link):
            with self.assertRaises(FileExistsError): m.new_file(path, 'overwrite')
        self.assertEqual('preserve', dst.read_text())


if __name__ == '__main__': unittest.main()
