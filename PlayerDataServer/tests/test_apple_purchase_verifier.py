"""No real key, receipt, account, network or wallet is used by these tests."""
import datetime
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.x509.oid import NameOID
from appstoreserverlibrary.models.Environment import Environment

from apple_verifier import build_verifier
from store import Fault


class PurchaseVerifierTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory(); self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        signing = ec.generate_private_key(ec.SECP256R1())
        (self.root/'key.p8').write_bytes(signing.private_bytes(serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
        name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'Synthetic test root')])
        stamp = datetime.datetime.now(datetime.timezone.utc)
        cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name)
                .public_key(signing.public_key()).serial_number(1)
                .not_valid_before(stamp-datetime.timedelta(days=1))
                .not_valid_after(stamp+datetime.timedelta(days=1))
                .add_extension(x509.BasicConstraints(ca=True, path_length=None), critical=True)
                .sign(signing, hashes.SHA256()))
        (self.root/'root.cer').write_bytes(cert.public_bytes(serialization.Encoding.DER))
        self.env = dict(WITCH_APPLE_ENVIRONMENT='Sandbox', WITCH_APPLE_BUNDLE_ID='com.test.game',
            WITCH_APPLE_ROOTS_DIR=str(self.root), WITCH_APPLE_KEY_FILE=str(self.root/'key.p8'),
            WITCH_APPLE_KEY_ID='ABCDEFGHIJ', WITCH_APPLE_ISSUER_ID='11111111-1111-4111-8111-111111111111')
        self.request = dict(Receipt='synthetic-jws', TransactionId='123', Target='product',
                            _AuthenticatedPlayer='a'*32)

    def transaction(self, **changes):
        return SimpleNamespace(**dict(dict(transactionId='123', productId='product', quantity=1,
            revocationDate=None, appAccountToken='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
            bundleId='com.test.game', environment=Environment.SANDBOX), **changes))

    def run_mocked(self, supplied=None, fresh=None, failure=None):
        with patch('appstoreserverlibrary.signed_data_verifier.SignedDataVerifier') as signed, \
                patch('appstoreserverlibrary.api_client.AppStoreServerAPIClient') as client:
            signed.return_value.verify_and_decode_signed_transaction.side_effect = [
                supplied or self.transaction(), fresh or self.transaction()]
            client.return_value.get_transaction_info.return_value = SimpleNamespace(signedTransactionInfo='fresh-jws')
            if failure: client.return_value.get_transaction_info.side_effect = failure
            result = build_verifier(self.env, sandbox_only=True)(self.request)
            self.assertTrue(signed.call_args.args[1])  # Online certificate checks remain on.
            self.assertEqual(Environment.SANDBOX, signed.call_args.args[2])
            self.assertEqual(Environment.SANDBOX, client.call_args.args[4])
            client.return_value.get_transaction_info.assert_called_once_with('123')
            return result

    def test_fresh_verified_account_bound_transaction(self):
        self.assertEqual(dict(verified=True, store='apple', transaction='123', product='product'), self.run_mocked())

    def test_explicit_mapping_not_ambient_production_environment(self):
        with patch.dict(os.environ, {'WITCH_APPLE_ENVIRONMENT':'Production'}, clear=True):
            self.assertTrue(self.run_mocked()['verified'])

    def test_environment_missing_misspelled_or_production_rejected_in_sandbox(self):
        for value in (None, '', 'sandbox', 'Xcode', 'Production'):
            with self.subTest(value=value):
                self.env['WITCH_APPLE_ENVIRONMENT'] = value
                with self.assertRaises(ValueError): build_verifier(self.env, sandbox_only=True)

    def test_missing_roots_rejected(self):
        (self.root/'root.cer').unlink()
        with self.assertRaises(RuntimeError): build_verifier(self.env)

    def test_supplied_wrong_id_app_or_environment_rejected_before_api(self):
        for changes in ({'transactionId':'other'}, {'bundleId':'other.app'},
                        {'environment':Environment.PRODUCTION}):
            with self.subTest(changes=changes), \
                    patch('appstoreserverlibrary.signed_data_verifier.SignedDataVerifier') as signed, \
                    patch('appstoreserverlibrary.api_client.AppStoreServerAPIClient') as client:
                signed.return_value.verify_and_decode_signed_transaction.return_value = self.transaction(**changes)
                with self.assertRaises(Fault): build_verifier(self.env, sandbox_only=True)(self.request)
                client.return_value.get_transaction_info.assert_not_called()

    def test_fresh_mismatches_revoked_or_foreign_account_never_granted(self):
        for changes in ({'transactionId':'other'}, {'bundleId':'other.app'},
                        {'environment':Environment.PRODUCTION}, {'revocationDate':1},
                        {'productId':'other'}, {'quantity':2}, {'appAccountToken':None},
                        {'appAccountToken':'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'}):
            with self.subTest(changes=changes):
                with self.assertRaises(Fault): self.run_mocked(fresh=self.transaction(**changes))

    def test_api_outage_is_pending_without_fallback(self):
        with self.assertRaises(Fault) as result: self.run_mocked(failure=TimeoutError('private-detail'))
        self.assertNotIn('private-detail', str(result.exception))
        self.assertIn('retry without repurchasing', str(result.exception))

    def test_real_library_rejects_unsigned_receipt_without_network(self):
        with patch('requests.sessions.Session.request', side_effect=AssertionError('no network')) as network:
            verifier = build_verifier(self.env, sandbox_only=True)
            with self.assertRaises(Fault): verifier(self.request)
            network.assert_not_called()


if __name__ == '__main__': unittest.main()
