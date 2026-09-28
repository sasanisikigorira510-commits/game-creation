import hashlib
from pathlib import Path
import unittest
from unittest.mock import patch

from deploy import install_apple_qa as install
from deploy.prepare_apple_qa_mac import CODE


class AppleQaDeployTests(unittest.TestCase):
    def test_proxy_keeps_current_health_and_closed_fallback(self):
        base=Path('deploy/Caddyfile-health-only').read_bytes()
        self.assertEqual(install.BASE_HASH,hashlib.sha256(base).hexdigest())
        candidate=base.replace(b'    @health {',install.PROXY.encode()+b'    @health {',1)
        self.assertEqual(base,candidate.replace(install.PROXY.encode(),b'',1))
        self.assertIn('handle_path /qa/*',install.PROXY)
        self.assertIn('127.0.0.1:8790',install.PROXY)
        self.assertNotIn('8788',install.PROXY)

    def test_service_is_loopback_non_root_not_boot_enabled(self):
        self.assertIn('User=witchplayer',install.SERVICE)
        self.assertIn('--bind 127.0.0.1:8790 qa_gateway:application_factory()',install.SERVICE)
        self.assertNotIn('[Install]',install.SERVICE)
        self.assertIn('ReadWritePaths=/var/lib/witch-player\n',install.SERVICE)
        self.assertIn('ProtectSystem=strict',install.SERVICE)
        self.assertIn('CapabilityBoundingSet=\n',install.SERVICE)

    def test_validation_uses_observed_service_environment(self):
        with patch.object(install,'run') as run:
            install.validate_caddy(Path('/tmp/candidate'))
        self.assertIn('/etc/witch-player/caddy.env',run.call_args.args[0][2])
        self.assertIn('runuser -u caddy --',run.call_args.args[0][2])
        self.assertEqual('/tmp/candidate',run.call_args.args[0][-1])

    def test_code_bundle_contains_no_secret_or_live_factory(self):
        self.assertNotIn('production.py',CODE)
        self.assertTrue(all(name.endswith(('.py','.json')) for name in CODE))
        self.assertNotIn('worker.json',CODE)
        self.assertNotIn('client.json',CODE)

    def test_wrong_manifest_argument_does_not_mutate(self):
        with patch.object(install.os,'geteuid',return_value=0),patch.object(install.sys,'argv',['installer','bad']),\
                patch.object(install,'write_new') as write,self.assertRaises(ValueError):
            install.main()
        write.assert_not_called()
