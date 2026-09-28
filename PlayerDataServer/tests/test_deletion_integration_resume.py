"""No sudo/network: disposable paths and deterministic fake timers."""
from contextlib import ExitStack, redirect_stdout
import hashlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy import resume_deletion_integration as r
from deploy import deletion_maintenance as m


class IntegrationResumeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        base = Path(self.temp.name)
        self.stack = ExitStack(); self.addCleanup(self.stack.close)
        for name, path in [('STATE', base / 'state'), ('TARGET', base / 'target'),
                           ('CAP', base / 'cap.d/cap.conf'), ('HOOK', base / 'health.d/60.conf'),
                           ('BASE_HOOK', base / 'health.d/40.conf')]:
            self.stack.enter_context(patch.object(r, name, path))
        r.BASE_HOOK.parent.mkdir(); r.BASE_HOOK.write_text(r.BASE_TEXT)
        self.stack.enter_context(patch.object(r, '__file__', str(base / 'installer.py')))
        (base / 'publish_backup_health.py').write_text('pass\n')
        self.stack.enter_context(patch.object(r, 'PUBLISHER_HASH', hashlib.sha256(b'pass\n').hexdigest()))
        self.stack.enter_context(patch.object(m, 'trusted'))
        self.stack.enter_context(patch.object(m, 'ROOT', r.STATE))
        self.stack.enter_context(patch.object(r, 'verify_pins'))
        self.stack.enter_context(redirect_stdout(io.StringIO()))
        self.calls = []
        self.h = dict(vars(m))
        self.h.update(trusted=lambda *a, **kw: None, unchanged=lambda: None,
                      base_health=lambda: None, queue_empty=lambda: None,
                      check_settings=lambda: None, check_worker=lambda: 100,
                      ctl=self.ctl, prop=self.prop, run=lambda *a, **kw: None)

    def ctl(self, *args, **kwargs):
        self.calls.append(args)
        return 'disabled' if args[0] == 'is-enabled' else ''

    def prop(self, unit, key):
        if key == 'DropInPaths':
            if unit == m.SERVICE: return ''
            return str(r.BASE_HOOK) + (' ' + str(r.HOOK) if r.HOOK.exists() else '')
        return 'inactive'

    def test_nonempty_queue_has_no_side_effects(self):
        def fail(): raise RuntimeError('queue')
        self.h['queue_empty'] = fail
        with self.assertRaises(RuntimeError): r.apply(self.h)
        self.assertFalse(r.STATE.exists()); self.assertFalse(r.CAP.exists())

    def test_success_keeps_baseline_and_installs_only_new_overrides(self):
        with patch.object(r, 'verify_cycles', return_value={'LaterWorkerCompletions': 2}), \
             patch.object(r, 'integrated_health', return_value=102):
            r.apply(self.h)
        self.assertEqual(r.BASE_TEXT, r.BASE_HOOK.read_text())
        self.assertTrue(r.CAP.exists()); self.assertTrue(r.HOOK.exists())
        self.assertTrue((r.STATE / 'verified.json').exists())
        self.assertFalse(any('witch-player.service' in cmd for cmd in self.calls))
        with self.assertRaises(RuntimeError): r.apply(self.h)

    def test_failed_verification_restores_observer_and_stops_worker(self):
        with patch.object(r, 'verify_cycles', side_effect=RuntimeError('failure')):
            with self.assertRaises(RuntimeError): r.apply(self.h)
        self.assertFalse(r.CAP.exists()); self.assertFalse(r.HOOK.exists())
        self.assertEqual(r.BASE_TEXT, r.BASE_HOOK.read_text())
        self.assertTrue((r.STATE / 'disabled-integrated-health.conf').exists())
        self.assertTrue((r.STATE / 'disabled-capabilities.conf').exists())
        self.assertIn(('disable', '--now', m.TIMER), self.calls)
        self.assertFalse((r.STATE / 'verified.json').exists())

    def test_initial_start_failure_restores_baseline(self):
        def fail(*args, **kwargs):
            if args == ('start', m.SERVICE): raise RuntimeError('start')
            return self.ctl(*args, **kwargs)
        self.h['ctl'] = fail
        with self.assertRaises(RuntimeError): r.apply(self.h)
        self.assertNotIn(('enable', '--now', m.TIMER), self.calls)
        self.assertFalse(r.CAP.exists()); self.assertFalse(r.HOOK.exists())
        self.assertEqual(r.BASE_TEXT, r.BASE_HOOK.read_text())

    def test_changed_payload_rejected(self):
        with patch.object(r, 'PUBLISHER_HASH', '0' * 64):
            with self.assertRaises(RuntimeError): r.apply(self.h)
        self.assertFalse(r.STATE.exists())

    def cycle_helpers(self):
        elapsed = [0]
        def pause(seconds): elapsed[0] += seconds
        self.h['prop'] = lambda *a: 'active'
        self.h['check_worker'] = lambda: 100 + (elapsed[0] // 60)
        return elapsed, lambda: elapsed[0], pause

    def test_two_automatic_completions_and_later_health_required(self):
        elapsed, clock, pause = self.cycle_helpers()
        with patch.object(r, 'integrated_health', side_effect=lambda *a: 100 + elapsed[0] // 60):
            result = r.verify_cycles(self.h, 100, 99, clock=clock, pause=pause)
        self.assertEqual(2, result['LaterWorkerCompletions'])
        self.assertTrue(result['LaterHealthRunVerified'])
        self.assertEqual(120, elapsed[0])

    def test_no_new_worker_completion_times_out(self):
        elapsed, clock, pause = self.cycle_helpers()
        self.h['check_worker'] = lambda: 100
        with patch.object(r, 'integrated_health', return_value=101):
            with self.assertRaises(RuntimeError): r.verify_cycles(self.h, 100, 99, clock=clock, pause=pause)
        self.assertEqual(210, elapsed[0])

    def test_unhealthy_marker_never_counts_as_verified(self):
        elapsed, clock, pause = self.cycle_helpers()
        with patch.object(r, 'integrated_health', side_effect=RuntimeError('unhealthy')):
            with self.assertRaises(RuntimeError): r.verify_cycles(self.h, 100, 99, clock=clock, pause=pause)
        self.assertEqual(0, elapsed[0])


if __name__ == '__main__': unittest.main()
