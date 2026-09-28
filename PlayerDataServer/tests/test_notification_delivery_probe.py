from types import SimpleNamespace as Item
from unittest.mock import Mock
import unittest
from deploy.test_refund_notification_delivery import verified_result, BUNDLE


class NotificationDeliveryProbeTests(unittest.TestCase):
    def setUp(self):
        self.notice = Item(version='2.0', notificationType='TEST',
                           data=Item(bundleId=BUNDLE, environment='Sandbox'))
        self.verifier = Mock()
        self.verifier.verify_and_decode_notification.return_value = self.notice
        self.response = Item(signedPayload='fixture', sendAttempts=[Item(sendAttemptResult='SUCCESS')])

    def test_verified_test_and_apple_success_required(self):
        result = verified_result(self.response, self.verifier)
        self.assertEqual('APPLE_TEST_DELIVERED', result['Status'])
        self.assertFalse(result['PurchaseOrRefundExecuted'])
        self.verifier.verify_and_decode_notification.assert_called_once_with('fixture')

    def test_wrong_environment_or_type_never_success(self):
        self.notice.data.environment = 'Production'
        with self.assertRaises(ValueError): verified_result(self.response, self.verifier)
        self.notice.data.environment = 'Sandbox'; self.notice.notificationType = 'REFUND'
        with self.assertRaises(ValueError): verified_result(self.response, self.verifier)

    def test_invalid_signature_never_success(self):
        self.verifier.verify_and_decode_notification.side_effect = ValueError('invalid')
        with self.assertRaises(ValueError): verified_result(self.response, self.verifier)

    def test_failed_delivery_and_empty_attempts_not_success(self):
        for attempts in ([], [Item(sendAttemptResult='TLS_ISSUE')]):
            self.response.sendAttempts = attempts
            self.assertEqual('APPLE_TEST_NOT_DELIVERED', verified_result(self.response, self.verifier)['Status'])
