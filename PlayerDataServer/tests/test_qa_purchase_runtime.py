import hashlib
import json
import os
import time
import unittest
from unittest.mock import patch

from tests.test_qa_isolation import QaIsolationTests
from tests.test_apple_application import AppleApplicationTests
import qa_purchase_runtime as purchase
import qa_isolated_runtime as base


class QaPurchaseRuntimeTests(unittest.TestCase):
    write = QaIsolationTests.write
    save_config = QaIsolationTests.save_config
    markers = QaIsolationTests.markers
    perform = QaIsolationTests.perform
    runtime_context = QaIsolationTests.runtime_context
    call = AppleApplicationTests.call

    def setUp(self):
        QaIsolationTests.setUp(self)
        self.runtime_context()
        self.credentials = self.root/'iap'; self.credentials.mkdir(mode=0o700)
        self.write(self.credentials/'signing.p8', b'synthetic-key')
        roots = self.credentials/'roots'; roots.mkdir(mode=0o700)
        self.write(roots/'test.cer', b'synthetic-certificate')
        self.iap = dict(Version=1, Environment='Sandbox', BundleId=purchase.BUNDLE,
                       KeyId='ABCDEFGHIJ', IssuerId='11111111-1111-4111-8111-111111111111')
        self.iap_path = self.root/'iap-sandbox.json'; self.save_iap()
        for name, value in dict(CONFIG=self.iap_path, CREDENTIALS=self.credentials).items():
            p = patch.object(purchase, name, value); p.start(); self.addCleanup(p.stop)
        self.config['ClientId'] = purchase.BUNDLE; self.save_config()
        self.gate_key = 'e'*64
        self.gate = dict(Version=1, TokenSha256=hashlib.sha256(self.gate_key.encode()).hexdigest(),
                         IssuedUnix=time.time()-1, ExpiresUnix=time.time()+120)
        os.environ['WITCH_QA_GATE_CONFIG'] = str(self.root/'gate.json'); self.save_gate()

    def save_iap(self): self.write(self.iap_path, json.dumps(self.iap).encode())
    def save_gate(self): self.write(self.root/'gate.json', json.dumps(self.gate).encode())

    def test_only_sandbox_fixed_paths_and_app(self):
        env = purchase.purchase_environment(os.environ)
        self.assertEqual('Sandbox', env['WITCH_APPLE_ENVIRONMENT'])
        self.assertEqual(str(self.credentials/'signing.p8'), env['WITCH_APPLE_KEY_FILE'])
        self.assertNotIn('WITCH_APPLE_BUNDLE_ID', os.environ)
        base.guard(os.environ)  # Unchanged maintenance/non-IAP startup still works.

    def test_production_or_unknown_fields_rejected(self):
        for field, value in [('Environment','Production'), ('BundleId','other.app'),
                             ('Version',True), ('KeyId','bad'), ('IssuerId','bad'),
                             ('SigningKeyFile','/etc/other/key')]:
            with self.subTest(field=field):
                original = dict(self.iap); self.iap[field] = value; self.save_iap()
                with self.assertRaises((ValueError, TypeError)): purchase.purchase_environment(os.environ)
                self.iap = original; self.save_iap()

    def test_production_db_and_ambient_bundle_rejected(self):
        for changes in ({'WITCH_DATA_DIR':'/var/lib/witch-player'},
                        {'WITCH_APPLE_BUNDLE_ID':purchase.BUNDLE}):
            with self.subTest(changes=changes):
                with self.assertRaises(ValueError): purchase.purchase_environment(dict(os.environ, **changes))

    def test_shared_key_and_symlink_rejected(self):
        path = self.credentials/'signing.p8'; path.chmod(0o644)
        with self.assertRaises(ValueError): purchase.purchase_environment(os.environ)
        path.chmod(0o600); path.rename(self.credentials/'original.p8'); path.symlink_to(self.credentials/'original.p8')
        with self.assertRaises(ValueError): purchase.purchase_environment(os.environ)

    def test_missing_roots_rejected(self):
        (self.credentials/'roots/test.cer').unlink()
        with self.assertRaises(ValueError): purchase.purchase_environment(os.environ)

    def test_factory_sets_verifier_but_keeps_admin_deletion_and_gate_closed(self):
        with patch.object(purchase, 'build_verifier', return_value=lambda _:None) as build:
            gateway = purchase.application_factory()
            self.assertTrue(build.call_args.kwargs['sandbox_only'])
        self.assertTrue(gateway.app.app.store.head(self.player)['PurchasesEnabled'])
        def wrapped(env, start):
            env['HTTP_AUTHORIZATION'] = 'NasusQA '+self.gate_key+' '+env['HTTP_AUTHORIZATION']
            return gateway(env, start)
        self.assertEqual(404, self.call(gateway, '/healthz')['status'])
        self.assertEqual(404, self.call(wrapped, '/admin')['status'])
        self.assertEqual(503, self.call(wrapped, '/v1/account-deletion/commit', {}, 'b'*64)['status'])
        result = self.call(wrapped, '/v1/players/'+self.player+'/head', token='b'*64)
        self.assertEqual(200, result['status'])
        self.assertEqual((1200,9600), (result['body']['Free'],result['body']['Paid']))
        self.assertEqual(401,self.call(wrapped,'/v1/players/'+self.player+'/head',token='wrong')['status'])

    def test_expired_gate_rejected_before_loading_verifier_or_app(self):
        self.gate.update(IssuedUnix=time.time()-100, ExpiresUnix=time.time()-1); self.save_gate()
        with patch.object(purchase, 'build_verifier') as build, patch.object(purchase, 'create_apple_app') as app:
            with self.assertRaises(ValueError): purchase.application_factory()
            build.assert_not_called(); app.assert_not_called()


if __name__ == '__main__': unittest.main()
