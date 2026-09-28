"""Installer orchestration under fake sudo/systemd/cloud in a disposable tree."""
from contextlib import ExitStack, redirect_stdout
import datetime as dt
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import stat
import tempfile
import time
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from deploy import install_deletion_worker as installer


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        backend = Path(__file__).resolve().parents[1]
        self.source = self.root / 'source'; self.source.mkdir()
        manifest = {}
        for name in installer.FILES:
            src = backend / ('deploy/run_worker.py' if name == 'run_worker.py' else name)
            dst = self.source / name; dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(src, dst)
            manifest[name] = hashlib.sha256(dst.read_bytes()).hexdigest()
        self.manifest = self.root / 'manifest.json'; self.manifest.write_text(json.dumps(manifest))
        self.target = self.root / 'installed'
        self.units = self.root / 'units'; self.units.mkdir()
        self.hook = self.units / 'witch-player-health.service.d/30-deletion-worker.conf'
        self.stage = self.root / 'root-stage'; self.stage.mkdir()
        self.state = self.root / 'state'
        self.calls = []
        self.fail_at = None
        from deploy.preflight_deletion_worker import INSPECTED, JOURNAL
        inspected = dict(INSPECTED, Caddyfile=installer.CADDY_HASH, publish_backup_health_py=installer.HEALTH_HASH)
        original_digest = installer.digest
        def digest(path):
            if str(path).startswith('/opt/witch-player/server/'): return inspected[path.name]
            if str(path) == '/etc/caddy/Caddyfile': return installer.CADDY_HASH
            if str(path) == '/usr/local/lib/nasus-backup/publish_backup_health.py': return installer.HEALTH_HASH
            return original_digest(path)
        def systemctl(*args, **kwargs):
            self.calls.append(args)
            if self.fail_at == args: raise RuntimeError('fixture failure')
            return ''
        def migrate(data):
            self.calls.append(('migrate',))
            return dict(SchemaAdded=True, CoreRowsUnchanged=True, SnapshotVerified=True)
        cipher = b'age-encryption.org/v1\n' + b'x' * 100
        publisher = Mock()
        publisher.probe.return_value = hashlib.sha256(cipher).hexdigest()
        self.publisher = publisher
        stack = ExitStack(); self.addCleanup(stack.close)
        patches = [
            patch.object(installer, 'TARGET', self.target), patch.object(installer, 'UNITS', self.units),
            patch.object(installer, 'HOOK', self.hook), patch.object(installer, 'digest', side_effect=digest),
            patch.object(installer, 'systemctl', side_effect=systemctl), patch.object(installer, 'command', return_value=''),
            patch.object(installer, 'http_code', side_effect=lambda path, **kw: '503' if path == '/' else '200'),
            patch('sys.argv', ['install', '--source', str(self.source), '--manifest', str(self.manifest)]),
            patch('os.geteuid', return_value=0), patch('pwd.getpwnam', return_value=SimpleNamespace(pw_uid=999, pw_gid=988)),
            patch('tempfile.mkdtemp', return_value=str(self.stage)),
            patch('deploy.preflight_deletion_worker.private_file', return_value=True),
            patch('deploy.preflight_deletion_worker.database_as_service_user', return_value={'Status': 'INSPECTED', 'Database': dict(CoreTablesPresent=True, QuickCheckPassed=True, ForeignKeysPassed=True, DeletionTables={name: False for name in JOURNAL})}),
            patch('deploy.deletion_worker.STATE', self.state),
            patch('deploy.deletion_worker.AGE', Mock(lstat=lambda: SimpleNamespace(st_mode=stat.S_IFREG | 0o755, st_uid=0))),
            patch('deploy.deletion_worker.as_database_user', side_effect=lambda account, fn: fn()),
            patch('deploy.deletion_worker.migrate_empty_schema', side_effect=migrate),
            patch('deploy.deletion_worker.reserve_upload'),
            patch('deploy.deletion_worker.read_json', side_effect=lambda path: {'Healthy': True} if path.name == 'status.json' else {'Status':'OFFSITE_CIPHERTEXT_VERIFIED','CompletedUtc':dt.datetime.now(dt.timezone.utc).isoformat()}),
            patch('deploy.sakura_deletion_journal.AgeEncryptor', return_value=lambda value: cipher),
            patch('deploy.sakura_deletion_journal.SakuraPublisher', return_value=publisher),
            patch('deploy.publish_backup_health.collect', return_value={'Healthy': True}),
            patch('subprocess.run', return_value=SimpleNamespace(returncode=0)),
        ]
        for p in patches: stack.enter_context(p)
        self.output = io.StringIO(); stack.enter_context(redirect_stdout(self.output))

    def test_success_orders_backup_migration_worker_health_and_leaves_api_untouched(self):
        installer.main()
        self.assertLess(self.calls.index(('start','witch-player-offsite.service')), self.calls.index(('migrate',)))
        self.assertLess(self.calls.index(('migrate',)), self.calls.index(('start','witch-player-deletion.service')))
        self.assertIn(('enable','--now','witch-player-deletion.timer'), self.calls)
        self.assertTrue(self.hook.exists())
        self.assertIn('--require-deletion-worker', self.hook.read_text())
        self.assertIn('DELETION_WORKER_SETUP_COMPLETE', self.output.getvalue())
        self.assertFalse(any('witch-player.service' in args for args in self.calls))
        self.publisher.probe.assert_called_once()

    def test_probe_failure_stops_before_migration(self):
        self.publisher.probe.side_effect = RuntimeError('fixture provider failure')
        with self.assertRaises(RuntimeError): installer.main()
        self.assertNotIn(('migrate',), self.calls)
        self.assertFalse(self.hook.exists())
        self.assertNotIn('DELETION_WORKER_SETUP_COMPLETE', self.output.getvalue())

    def test_worker_start_failure_leaves_schema_and_stops_timer(self):
        self.fail_at = ('start','witch-player-deletion.service')
        with patch('subprocess.run') as run:
            with self.assertRaises(RuntimeError): installer.main()
        commands = [call.args[0] for call in run.call_args_list]
        self.assertIn(['/usr/bin/systemctl','disable','--now','witch-player-deletion.timer'], commands)
        self.assertIn(('migrate',), self.calls)
        self.assertTrue((self.state / 'migration.json').exists())
        self.assertFalse(self.hook.exists())

    def test_health_failure_restores_prior_command_and_retains_diagnostics(self):
        self.fail_at = ('start','witch-player-health.service')
        with patch('subprocess.run') as run:
            with self.assertRaises(RuntimeError): installer.main()
        self.assertFalse(self.hook.exists())
        self.assertTrue((self.stage / 'disabled-health-hook.conf').exists())
        self.assertTrue((self.state / 'migration.json').exists())
        self.assertIn(['/usr/bin/systemctl','start','witch-player-health.service'], [c.args[0] for c in run.call_args_list])

    def test_existing_target_is_not_overwritten_or_reinstalled(self):
        self.target.mkdir(); marker = self.target / 'keep'; marker.write_text('existing')
        with self.assertRaises(ValueError): installer.main()
        self.assertEqual('existing', marker.read_text())
        self.assertEqual([], self.calls)

    def test_tampered_bundle_never_reaches_backup_or_schema(self):
        (self.source / 'store.py').write_text('tampered fixture')
        with self.assertRaises(ValueError): installer.main()
        self.assertEqual([], self.calls)
        self.assertFalse(self.target.exists())


if __name__ == '__main__': unittest.main()
