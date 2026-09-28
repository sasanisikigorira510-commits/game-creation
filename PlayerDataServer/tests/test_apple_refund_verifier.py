import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
import uuid

from apple_refund_verifier import notification_verifier, build_notification_verifier, transaction_verifier, build_transaction_verifier
from store import Fault


class RefundVerifierTests(unittest.TestCase):
    def setUp(self):
        self.product = 'com.nasus.dungeonmonsterroguelike.crystals650'
        self.tx = dict(transactionId='fixture', bundleId='com.test.game', environment='Sandbox',
            productId=self.product, appAccountToken='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
            type='Consumable', quantity=1, signedDate=1000, revocationDate=900,
            revocationType='REFUND_FULL', revocationPercentage=100000)
        self.notice = SimpleNamespace(version='2.0', notificationUUID=str(uuid.uuid4()),
            notificationType='REFUND', data=SimpleNamespace(bundleId='com.test.game',
                environment='Sandbox', signedTransactionInfo='signed-embedded'))
        self.signed, self.api = Mock(), Mock()
        self.signed.verify_and_decode_notification.return_value = self.notice
        self.api.get_transaction_info.return_value = SimpleNamespace(signedTransactionInfo='signed-current')
        self.verify = notification_verifier(self.signed, self.api, 'com.test.game', 'Sandbox')

    def run_state(self, current=None, supplied=None):
        self.signed.verify_and_decode_signed_transaction.side_effect = [
            supplied or SimpleNamespace(**self.tx), current or SimpleNamespace(**self.tx)]
        return self.verify('signed-fixture-notification')

    def fault(self, code, run):
        with self.assertRaises(Fault) as e: run()
        self.assertEqual(code, e.exception.status)
        self.assertNotIn('private-secret', str(e.exception))

    def test_full_refund_requires_both_signatures_and_fresh_transaction(self):
        event = self.run_state()
        self.assertEqual((100000, 'a'*32, self.product), (event.percentage, event.player, event.product))
        self.api.get_transaction_info.assert_called_once_with('fixture')
        self.assertEqual(['signed-embedded', 'signed-current'],
            [c.args[0] for c in self.signed.verify_and_decode_signed_transaction.call_args_list])

    def test_partial_percentage_and_legacy_full_supported(self):
        for edits, expected in ((dict(revocationType='REFUND_PRORATED', revocationPercentage=25000), 25000),
                                (dict(revocationType=None, revocationPercentage=None), 100000)):
            event = self.run_state(current=SimpleNamespace(**dict(self.tx, **edits)))
            self.assertEqual(expected, event.percentage)

    def test_reversed_refund_uses_current_state_even_for_late_refund_notice(self):
        current = SimpleNamespace(**dict(self.tx, signedDate=2000, revocationDate=None,
                                        revocationType=None, revocationPercentage=None))
        self.assertEqual(0, self.run_state(current=current).percentage)
        self.notice.notificationType = 'REFUND_REVERSED'
        self.assertEqual(0, self.run_state(current=current).percentage)

    def test_signature_failure_never_calls_api(self):
        self.signed.verify_and_decode_notification.side_effect = ValueError('private-secret')
        self.fault(400, self.run_state)
        self.api.get_transaction_info.assert_not_called()

    def test_transient_certificate_check_failure_remains_retryable(self):
        from appstoreserverlibrary.signed_data_verifier import VerificationException, VerificationStatus
        self.signed.verify_and_decode_notification.side_effect = VerificationException(VerificationStatus.RETRYABLE_VERIFICATION_FAILURE)
        self.fault(503, self.run_state)
        self.api.get_transaction_info.assert_not_called()

    def test_wrong_notification_app_environment_version_rejected(self):
        for owner, key, value in ((self.notice.data, 'bundleId', 'foreign'),
                                  (self.notice.data, 'environment', 'Production'),
                                  (self.notice, 'version', '1.0')):
            previous = getattr(owner, key); setattr(owner, key, value)
            self.fault(400, self.run_state); setattr(owner, key, previous)
        self.api.get_transaction_info.assert_not_called()

    def test_embedded_account_product_quantity_type_rejected_before_api(self):
        for edits in ({'appAccountToken':None}, {'productId':'foreign'}, {'quantity':2},
                      {'type':'Non-Consumable'}, {'environment':'Production'}):
            with self.subTest(edits=edits):
                self.fault(400, lambda: self.run_state(supplied=SimpleNamespace(**dict(self.tx, **edits))))
        self.api.get_transaction_info.assert_not_called()

    def test_fresh_mismatch_stale_or_ambiguous_partial_never_applied(self):
        for edits in ({'appAccountToken':str(uuid.uuid4())}, {'productId':'foreign'}, {'quantity':2},
                      {'bundleId':'foreign'}, {'environment':'Production'}, {'transactionId':'other'},
                      {'type':'Non-Consumable'}, {'signedDate':999}, {'signedDate':True},
                      {'revocationType':'REFUND_PRORATED', 'revocationPercentage':None},
                      {'revocationPercentage':100001}, {'revocationPercentage':True},
                      {'revocationType':'FAMILY_REVOKE'}, {'revocationDate':None},
                      {'revocationType':None, 'rawRevocationType':'FUTURE_PARTIAL'},
                      {'revocationDate':False}):
            with self.subTest(edits=edits):
                self.fault(503, lambda: self.run_state(current=SimpleNamespace(**dict(self.tx, **edits))))

    def test_old_sdk_missing_partial_fields_fails_closed(self):
        old = dict(self.tx); del old['revocationPercentage']
        self.fault(503, lambda: self.run_state(current=SimpleNamespace(**old)))

    def test_fresh_api_outage_or_bad_signature_is_retryable_without_debit(self):
        self.api.get_transaction_info.side_effect = TimeoutError('private-secret')
        self.fault(503, self.run_state)
        self.api.get_transaction_info.side_effect = None
        self.signed.verify_and_decode_signed_transaction.side_effect = [SimpleNamespace(**self.tx), ValueError('private-secret')]
        self.fault(503, lambda: self.verify('signed-fixture-notification'))

    def test_verified_unrelated_notification_ignored_without_transaction_lookup(self):
        self.notice.notificationType = 'TEST'
        self.assertIsNone(self.run_state())
        self.api.get_transaction_info.assert_not_called()

    def test_factory_rejects_implicit_or_wrong_environment_before_loading_keys(self):
        for env in (None, '', 'sandbox', 'Production'):
            with self.assertRaises(ValueError):
                build_notification_verifier({'WITCH_APPLE_ENVIRONMENT': env}, sandbox_only=True)

    def test_real_sdk_rejects_unsigned_notification_without_network(self):
        # Reuse synthetic certificate fixture, never a task/user key.
        from tests.test_apple_purchase_verifier import PurchaseVerifierTests
        fixture = PurchaseVerifierTests(); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        with patch('requests.sessions.Session.request', side_effect=AssertionError('no network')) as network:
            verify = build_notification_verifier(fixture.env, sandbox_only=True)
            self.fault(400, lambda: verify('unsigned-fixture'))
            network.assert_not_called()

    def test_direct_lookup_does_not_need_notification_and_has_stable_identity(self):
        checker = transaction_verifier(self.signed, self.api, 'com.test.game', 'Sandbox')
        self.signed.verify_and_decode_signed_transaction.return_value = SimpleNamespace(**self.tx)
        purchase = dict(transaction_id='fixture', player='a'*32, product=self.product)
        first = checker(purchase)
        self.assertEqual(first, checker(purchase))
        self.assertEqual(100000, first.percentage)
        self.signed.verify_and_decode_notification.assert_not_called()
        self.signed.verify_and_decode_signed_transaction.return_value = SimpleNamespace(**dict(self.tx,
            signedDate=2000, revocationDate=None, revocationType=None, revocationPercentage=None))
        reversed_state = checker(purchase)
        self.assertEqual(0, reversed_state.percentage)
        self.assertNotEqual(first.notification_id, reversed_state.notification_id)

    def test_direct_lookup_rejects_unverified_or_mismatched_transaction(self):
        checker = transaction_verifier(self.signed, self.api, 'com.test.game', 'Sandbox')
        purchase = dict(transaction_id='fixture', player='a'*32, product=self.product)
        for edits in (dict(environment='Production'), dict(appAccountToken=str(uuid.uuid4())),
                      dict(transactionId='foreign'), dict(productId='foreign'), dict(signedDate=False),
                      dict(revocationType='REFUND_PRORATED', revocationPercentage=None)):
            self.signed.verify_and_decode_signed_transaction.return_value = SimpleNamespace(**dict(self.tx, **edits))
            self.fault(503, lambda: checker(purchase))
        self.signed.verify_and_decode_signed_transaction.side_effect = ValueError('private-secret')
        self.fault(503, lambda: checker(purchase))

    def test_direct_factory_and_real_sdk_do_not_trust_unsigned_state(self):
        from tests.test_apple_purchase_verifier import PurchaseVerifierTests
        fixture = PurchaseVerifierTests(); fixture.setUp(); self.addCleanup(fixture.doCleanups)
        with patch('appstoreserverlibrary.api_client.AppStoreServerAPIClient.get_transaction_info',
                   return_value=SimpleNamespace(signedTransactionInfo='unsigned-fixture')), \
             patch('requests.sessions.Session.request', side_effect=AssertionError('no network')) as network:
            checker = build_transaction_verifier(fixture.env, sandbox_only=True)
            self.fault(503, lambda: checker(dict(transaction_id='fixture', player='a'*32, product=self.product)))
            network.assert_not_called()

    def test_direct_factory_rejects_production_in_sandbox_mode_before_keys(self):
        with self.assertRaises(ValueError):
            build_transaction_verifier({'WITCH_APPLE_ENVIRONMENT': 'Production'}, sandbox_only=True)


if __name__ == '__main__': unittest.main()
