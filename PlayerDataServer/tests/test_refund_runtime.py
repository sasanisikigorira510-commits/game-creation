import fcntl
import json
import os
from pathlib import Path
import unittest
from unittest.mock import patch

from refund_runtime import execute, load
from refund_health import healthy_runtime, check_systemd
from tests import test_refund_restore as fixture


class RefundRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.RefundRestoreTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.root, self.data = self.f.root, self.f.live
        self.state = self.root/'worker-state'; self.state.mkdir(mode=0o700)
        self.roots = self.root/'certs'; self.roots.mkdir(mode=0o700)
        self.write(self.roots/'test.cer', b'non-secret-fixture')
        self.key = self.root/'test-key'; self.write(self.key, b'not-a-real-key')
        self.apple = dict(WITCH_APPLE_ENVIRONMENT='Sandbox', WITCH_APPLE_BUNDLE_ID='com.test.game',
            WITCH_APPLE_ROOTS_DIR=str(self.roots), WITCH_APPLE_KEY_FILE=str(self.key),
            WITCH_APPLE_KEY_ID='FIXTUREKEY', WITCH_APPLE_ISSUER_ID='fixture-issuer')
        self.apple_file = self.root/'apple.json'; self.write(self.apple_file, json.dumps(self.apple).encode())
        self.config = dict(Version=1, InstanceId=self.f.instance, DataDirectory=str(self.data),
            StateDirectory=str(self.state), AppleConfigFile=str(self.apple_file), Environment='Sandbox', BatchLimit=2)
        self.path = self.root/'worker.json'; self.save()
        with self.f.store.connect() as db:
            db.execute('UPDATE refund_checks SET due=0,lease=NULL,lease_until=0')
        for path in self.data.iterdir(): path.chmod(0o600)
        self.service = dict(LoadState='loaded', ActiveState='inactive', Result='success', ExecMainStatus='0')
        self.timer = dict(LoadState='loaded', ActiveState='active', UnitFileState='enabled')

    def write(self, path, data): path.write_bytes(data); path.chmod(0o600)
    def save(self): self.write(self.path, json.dumps(self.config).encode())
    def checker_factory(self, config, *, sandbox_only):
        self.assertTrue(sandbox_only)
        return lambda p: self.f.events[p['player']]
    def run_worker(self): return execute(self.path, run=True, checker_factory=self.checker_factory)
    def marker(self): return json.loads((self.state/'status.json').read_text())
    def health(self, **changes):
        args = dict(marker=self.state/'status.json', instance=self.f.instance, environment='Sandbox',
                    service=self.service, timer=self.timer)
        args.update(changes)
        return healthy_runtime(**args)

    def test_default_preflight_has_no_network_key_read_schema_or_state_change(self):
        with self.f.store.connect() as db: before = list(db.iterdump())
        original = Path.read_bytes
        def read(path):
            self.assertNotEqual(self.key, path)
            return original(path)
        with patch('pathlib.Path.read_bytes', read), \
             patch('requests.sessions.Session.request', side_effect=AssertionError('no network')), \
             patch('apple_refund_verifier.build_transaction_verifier', side_effect=AssertionError('no key factory')):
            result = execute(self.path)
        self.assertFalse(result['SigningKeyRead']); self.assertFalse(result['Activated'])
        self.assertEqual([], list(self.state.iterdir()))
        with self.f.store.connect() as db: self.assertEqual(before, list(db.iterdump()))

    def test_explicit_synthetic_run_publishes_private_status_and_health(self):
        self.assertTrue(self.run_worker()['Healthy'])
        self.assertTrue(self.health())
        self.assertEqual(0o600, (self.state/'status.json').stat().st_mode & 0o777)
        raw = (self.state/'status.json').read_text()
        for secret in (self.f.player, self.f.other, self.f.token, 'not-a-real-key'):
            self.assertNotIn(secret, raw)

    def test_checker_failure_keeps_pending_and_cannot_reuse_old_success(self):
        self.run_worker()
        with self.f.store.connect() as db: db.execute('UPDATE refund_checks SET due=0')
        def factory(*args, **kwargs):
            def fail(_): raise TimeoutError('private-secret')
            return fail
        result = execute(self.path, run=True, checker_factory=factory)
        self.assertFalse(result['Healthy']); self.assertFalse(self.health())
        self.assertEqual(2, result['PendingFailures'])
        self.assertNotIn('private-secret', (self.state/'status.json').read_text())

    def test_factory_crash_marks_failure_before_rethrow(self):
        self.run_worker()
        with self.assertRaises(ValueError):
            execute(self.path, run=True, checker_factory=lambda *a,**k: (_ for _ in ()).throw(ValueError('private-secret')))
        self.assertEqual('REFUND_WORKER_FAILED', self.marker()['Status'])
        self.assertFalse(self.health())

    def test_lock_prevents_concurrent_process_batch(self):
        fd = os.open(self.state/'worker.lock', os.O_CREAT | os.O_RDWR, 0o600)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with self.assertRaises(BlockingIOError): self.run_worker()
        finally: os.close(fd)
        self.assertFalse((self.state/'status.json').exists())

    def test_quarantine_wrong_instance_missing_schema_environment_and_paths_refused(self):
        self.write(self.data/'RECOVERY-PENDING.txt', b'fixture')
        with self.assertRaises(ValueError): execute(self.path)
        (self.data/'RECOVERY-PENDING.txt').unlink()
        for edits in (dict(InstanceId='wrong-instance'), dict(Environment='Production'),
                      dict(StateDirectory=str(self.data)), dict(BatchLimit=True), dict(BatchLimit=11)):
            previous = dict(self.config); self.config.update(edits); self.save()
            with self.assertRaises(ValueError): execute(self.path)
            self.config = previous; self.save()
        with self.f.store.connect() as db: db.execute('DROP TABLE refund_waivers')
        with self.assertRaises(ValueError): execute(self.path)

    def test_public_key_path_and_symlink_config_rejected(self):
        self.key.chmod(0o644)
        with self.assertRaises(ValueError): execute(self.path)
        self.key.chmod(0o600)
        link = self.root/'link'; link.symlink_to(self.path)
        with self.assertRaises(ValueError): execute(link)

    def test_readonly_loaded_store_cannot_write(self):
        import sqlite3
        _, store, _, _ = load(self.path)
        with store.connect() as db, self.assertRaises(sqlite3.OperationalError):
            db.execute('UPDATE players SET paid=paid+1')

    def test_health_rejects_stale_future_foreign_failed_disabled_running_and_malformed(self):
        self.run_worker(); stamp = self.marker()['CheckedUnix']
        self.assertFalse(self.health(now=stamp+241)); self.assertFalse(self.health(now=stamp-1))
        self.assertFalse(self.health(instance='other-instance')); self.assertFalse(self.health(environment='Production'))
        for edits in (dict(ActiveState='failed'), dict(Result='timeout'), dict(ExecMainStatus='1'), dict(ActiveState='activating')):
            self.assertFalse(self.health(service=dict(self.service, **edits)))
        self.assertFalse(self.health(timer=dict(self.timer, UnitFileState='disabled')))
        good = self.marker()
        for edit in (dict(CheckedUnix=True), dict(Healthy=1), dict(Counts={}), dict(Status='REFUND_WORKER_RUNNING')):
            self.write(self.state/'status.json', json.dumps(dict(good, **edit)).encode())
            self.assertFalse(self.health())
        self.write(self.state/'status.json', b'bad-json'); self.assertFalse(self.health())

    def test_systemd_adapter_only_queries_fresh_unit_properties(self):
        from types import SimpleNamespace
        self.run_worker()
        def show(cmd, **kwargs):
            self.assertEqual(['/usr/bin/systemctl','show'], cmd[:2])
            state = self.service if cmd[2].endswith('.service') else self.timer
            return SimpleNamespace(stdout='\n'.join(k+'='+v for k,v in state.items()))
        with patch('refund_health.subprocess.run', side_effect=show) as calls:
            self.assertTrue(check_systemd(self.state/'status.json', self.f.instance, 'Sandbox'))
            self.assertEqual(2, calls.call_count)
        with patch('refund_health.subprocess.run', side_effect=FileNotFoundError):
            self.assertFalse(check_systemd(self.state/'status.json', self.f.instance, 'Sandbox'))
