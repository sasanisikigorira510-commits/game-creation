import hashlib
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy import recover_apple_qa as repair
from deploy import install_apple_qa as installer


class QaRecoveryTests(unittest.TestCase):
    def test_explicit_mode_survives_private_umask_and_secrets_stay_private(self):
        with tempfile.TemporaryDirectory() as directory:
            previous = os.umask(0o077)
            try:
                for index, writer in enumerate((repair.write_exact, installer.write_new)):
                    public = Path(directory) / ('public' + str(index))
                    secret = Path(directory) / ('secret' + str(index))
                    writer(public, b'config', 0o644)
                    writer(secret, b'private')
                    self.assertEqual(public.stat().st_mode & 0o777, 0o644)
                    self.assertEqual(secret.stat().st_mode & 0o777, 0o600)
                    with self.assertRaises(FileExistsError):
                        writer(public, b'replace')
                    self.assertEqual(public.read_bytes(), b'config')
            finally:
                os.umask(previous)

    def test_unexpected_configuration_is_not_replaced(self):
        with patch.object(repair, 'check_parent'), \
                patch.object(repair.qa, 'safe_file', return_value=b'unrelated'), \
                patch.object(repair.tempfile, 'mkstemp') as create, \
                self.assertRaises(ValueError):
            repair.replace_proxy(b'old', (b'old', b'candidate'))
        create.assert_not_called()

    def test_validate_runs_as_the_caddy_service_user(self):
        with patch.object(repair.qa, 'run') as run:
            repair.validate_as_caddy(Path('/etc/caddy/candidate'))
        command = run.call_args.args[0]
        self.assertIn('/usr/sbin/runuser -u caddy --', command[2])
        self.assertEqual(command[-1], '/etc/caddy/candidate')

    def test_preflight_rejects_non_root_without_changing_files(self):
        with patch.object(repair.os, 'geteuid', return_value=1000), \
                patch.object(repair, 'replace_proxy') as replace, self.assertRaises(ValueError):
            repair.main()
        replace.assert_not_called()

    def test_retained_failed_package_is_pinned(self):
        bundle = Path('deploy/reports/apple-qa-package.H3ZC81')
        self.assertEqual(hashlib.sha256((bundle/'manifest.json').read_bytes()).hexdigest(), repair.MANIFEST_HASH)
        old = (bundle/'install_apple_qa.py').read_bytes()
        self.assertEqual(hashlib.sha256(old).hexdigest(),
                         'b9c45e26ec3593d15383f446e9ede39201a0b8a674ed07d2c7db596c7adca8f8')
        wrapper = Path('deploy/run-apple-qa-recovery-mac.sh').read_text()
        for source in (Path('deploy/recover_apple_qa.py'), bundle/'install_apple_qa.py', bundle/'install_apple_staging.py'):
            self.assertIn(hashlib.sha256(source.read_bytes()).hexdigest(), wrapper)

    def test_expired_gate_stops_before_client_read(self):
        import json
        raw = (Path('deploy/reports/apple-qa-package.H3ZC81')/'manifest.json').read_bytes()
        manifest = json.loads(raw)
        def read(path, *_args):
            if path.name == 'manifest.json': return raw
            if path.name == 'candidate-Caddyfile': return b'candidate'
            if path == repair.qa.UNIT: return repair.qa.SERVICE.encode()
            if path.name == 'gate.json':
                return json.dumps(dict(Version=1, IssuedUnix=100, ExpiresUnix=200, TokenSha256='0'*64)).encode()
            return b'code'
        with patch.object(repair, 'check_parent'), patch.object(repair.qa, 'safe_file', side_effect=read), \
                patch.object(repair, 'digest', side_effect=lambda v: repair.MANIFEST_HASH if v == raw else next(iter(manifest.values()))), \
                patch.object(repair.json, 'loads', side_effect=[{k:next(iter(manifest.values())) for k in manifest},
                    dict(Version=1, IssuedUnix=100, ExpiresUnix=200, TokenSha256='0'*64)]), \
                patch.object(repair.pwd, 'getpwnam'), patch.object(repair.qa, 'ctl', return_value='static'), \
                self.assertRaises(ValueError):
            repair.validate_existing_runtime(b'candidate')
