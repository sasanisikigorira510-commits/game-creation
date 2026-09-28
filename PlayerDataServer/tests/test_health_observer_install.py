import hashlib
import json
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch
from contextlib import ExitStack, redirect_stdout
import io

from deploy import install_health_observer as i


class HealthObserverInstallTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        base = Path(self.temp.name)
        self.target = base / 'installed'
        self.hook = base / 'unit.d' / '40-reason.conf'
        self.source = base / 'source.py'
        self.source.write_text('pass\n')
        self.marker = base / 'status.json'
        self.calls = []
        self.fail_start = False
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        for name, value in [('TARGET', self.target), ('SOURCE', self.source), ('HOOK', self.hook),
                            ('MARKER', self.marker), ('OBS_HASH', hashlib.sha256(b'pass\n').hexdigest())]:
            self.stack.enter_context(patch.object(i, name, value))
        for name in ('trusted', 'pins', 'state', 'https', 'run'):
            self.stack.enter_context(patch.object(i, name))
        self.stack.enter_context(patch.object(i.os, 'geteuid', return_value=0))
        self.stack.enter_context(patch.object(i.sys, 'argv', ['installer']))
        self.stack.enter_context(patch.object(i, 'ctl', side_effect=self.ctl))
        self.stack.enter_context(redirect_stdout(io.StringIO()))
        # Installer umask changes belong only to its real process, not test runner.
        self.stack.enter_context(patch.object(i.os, 'umask'))

    def ctl(self, *args, **kwargs):
        self.calls.append(args)
        if 'DropInPaths' in args:
            return str(self.hook) if self.hook.exists() else ''
        if 'Result' in args: return 'success'
        if args[0] == 'start':
            if self.fail_start and self.hook.exists(): raise RuntimeError('test')
            self.marker.write_text(json.dumps({'Healthy': True, 'CheckedUnix': time.time()}))
        return ''

    def test_success_only_adds_own_hook_and_leaves_worker_alone(self):
        i.main()
        self.assertEqual(i.TEXT, self.hook.read_text())
        self.assertTrue((self.target / 'installed.json').exists())
        self.assertEqual(b'pass\n', (self.target / 'observe.py').read_bytes())
        self.assertFalse(any('witch-player-deletion.service' in cmd or 'enable' in cmd for cmd in self.calls))

    def test_failure_restores_old_config_retaining_observer(self):
        self.fail_start = True
        with self.assertRaises(RuntimeError): i.main()
        self.assertFalse(self.hook.exists())
        self.assertEqual(i.TEXT, (self.target / 'disabled-reason-logging.conf').read_text())
        self.assertTrue((self.target / 'observe.py').exists())
        self.assertFalse((self.target / 'installed.json').exists())

    def test_changed_staged_bytes_refused_before_install(self):
        self.source.write_text('malicious\n')
        with self.assertRaises(ValueError): i.main()
        self.assertFalse(self.target.exists())
        self.assertFalse(self.hook.exists())

    def test_existing_partial_install_is_never_overwritten(self):
        self.target.mkdir()
        (self.target / 'preserve').write_text('keep')
        with self.assertRaises(ValueError): i.main()
        self.assertEqual('keep', (self.target / 'preserve').read_text())

    def test_rollback_refuses_unrelated_hook(self):
        self.target.mkdir(); self.hook.parent.mkdir()
        self.hook.write_text('other configuration')
        with self.assertRaises(ValueError): i.rollback()
        self.assertEqual('other configuration', self.hook.read_text())


if __name__ == '__main__': unittest.main()
