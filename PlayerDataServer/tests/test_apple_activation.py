import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy import activate_apple_worker as install
from deploy.prepare_apple_schema import TABLES


class AppleActivationTests(unittest.TestCase):
    def setUp(self):
        temp=tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root=Path(temp.name).resolve()

    def test_trusted_reader_rejects_wrong_owner_shared_file_symlink_and_size(self):
        path=self.root/'code.py'; path.write_bytes(b'code'); path.chmod(0o600)
        self.assertEqual(install.trusted(path,os.getuid()),b'code')
        for owner,maximum in ((os.getuid()+1,100),(os.getuid(),2)):
            with self.assertRaises(ValueError): install.trusted(path,owner,maximum)
        path.chmod(0o666)
        with self.assertRaises(ValueError): install.trusted(path,os.getuid())
        path.chmod(0o600); link=self.root/'linked'; link.symlink_to(path)
        with self.assertRaises(ValueError): install.trusted(link,os.getuid())

    def test_new_file_refuses_existing_target_and_sets_requested_permissions(self):
        path=self.root/'unit'
        install.write_new(path,b'original',0o644)
        self.assertEqual(path.stat().st_mode & 0o777,0o644)
        with self.assertRaises(FileExistsError): install.write_new(path,b'replacement')
        self.assertEqual(path.read_bytes(),b'original')

    def test_empty_runtime_probe_is_readonly_and_uses_fixed_table_allowlist(self):
        self.assertEqual(set(install.APPLE_TABLES),TABLES)
        with patch.object(install,'run',return_value='APPLE_EMPTY_RUNTIME_VERIFIED') as command:
            install.empty_runtime_probe()
        args=command.call_args.args[0]
        self.assertEqual(args[:4],['/usr/sbin/runuser','-u','witchplayer','--'])
        script=args[-1]; compile(script,'probe','exec')
        self.assertIn('readonly=True',script)
        self.assertNotIn('process_one',script)
        self.assertNotIn('print(config',script)
        for table in TABLES: self.assertIn(table,script)

    def test_probe_failure_stops_without_success(self):
        with patch.object(install,'run',return_value='unexpected'):
            with self.assertRaises(ValueError): install.empty_runtime_probe()

    def test_rollback_restores_health_even_if_worker_stop_fails(self):
        health=self.root/'health'; health.mkdir()
        hook=self.root/'80-apple-worker.conf'; hook.write_text(install.HOOK_TEXT)
        calls=[]
        def control(*args,**kwargs):
            calls.append(args)
            if args[0]=='disable': raise ValueError('private details')
        output=io.StringIO()
        with patch.object(install,'HEALTH',health),patch.object(install,'HOOK',hook), \
             patch.object(install,'trusted',side_effect=lambda path,*args:path.read_bytes()), \
             patch.object(install,'ctl',side_effect=control), \
             patch.object(install,'prop',return_value=install.PREVIOUS_DROPS), \
             patch.object(install,'external') as external,contextlib.redirect_stdout(output):
            install.rollback()
        self.assertFalse(hook.exists())
        self.assertEqual((health/'disabled-hook.conf').read_text(),install.HOOK_TEXT)
        self.assertIn(('start','witch-player-health.service'),calls)
        external.assert_called_once()
        self.assertIn('ROLLBACK_INCOMPLETE',output.getvalue())
        self.assertNotIn('private details',output.getvalue())

    def test_rollback_does_not_replace_unrecognized_health_hook(self):
        health=self.root/'health'; health.mkdir()
        hook=self.root/'80-apple-worker.conf'; hook.write_text('unrelated')
        with patch.object(install,'HEALTH',health),patch.object(install,'HOOK',hook), \
             patch.object(install,'trusted',side_effect=lambda path,*args:path.read_bytes()), \
             patch.object(install,'ctl'), contextlib.redirect_stdout(io.StringIO()):
            install.rollback()
        self.assertEqual(hook.read_text(),'unrelated')

    def test_marker_rejects_unhealthy_stale_future_and_boolean_timestamps(self):
        good=dict(Healthy=True,CheckedUnix=1000)
        with patch.object(install.time,'time',return_value=1001):
            for changes in ({'Healthy':False},{'CheckedUnix':True},{'CheckedUnix':1002},
                            {'CheckedUnix':800},{'CheckedUnix':float('nan')}):
                with patch.object(install,'trusted',return_value=json.dumps(dict(good,**changes)).encode()):
                    with self.assertRaises(ValueError): install.marker(Path('/unused'),0,900)

    def test_manifest_and_wrapper_are_pinned_and_secret_free(self):
        directory=Path(__file__).resolve().parents[1]/'deploy'
        manifest_raw=(directory/'apple-activation-manifest.json').read_bytes()
        manifest=json.loads(manifest_raw)
        self.assertEqual(set(manifest),install.PACKAGE)
        backend=directory.parent
        for name,digest in manifest.items():
            path=directory/name if name.startswith('witch-') or name=='apple_integrated_health.py' else backend/name
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),digest)
        wrapper=(directory/'run-apple-activation-mac.sh').read_text()
        for path in (directory/'activate_apple_worker.py',directory/'apple-activation-manifest.json'):
            self.assertIn(hashlib.sha256(path.read_bytes()).hexdigest(),wrapper)
        self.assertNotIn('NOPASSWD',wrapper)
        self.assertNotIn('signing.p8',wrapper)
