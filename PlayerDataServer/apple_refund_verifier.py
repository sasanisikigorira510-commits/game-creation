"""Verify notification + embedded transaction + current Apple transaction.

No implicit environment, no unverified JWT decoding, no client refund assertion.
Construction is explicit and does not register a webhook or contact Apple.
"""
import hashlib
import uuid
from pathlib import Path

from purchase_refunds import PRODUCTS, RefundState
from store import Fault, encode


def value(item):
    return getattr(item, 'value', item)


def current_state(verifier, client, bundle, environment, transaction, player, product, minimum_date=1):
    fetched = client.get_transaction_info(transaction)
    current = verifier.verify_and_decode_signed_transaction(fetched.signedTransactionInfo)
    if (current.transactionId != transaction or current.bundleId != bundle
            or value(current.environment) != environment or current.productId != product
            or product not in PRODUCTS or type(current.quantity) is not int or current.quantity != 1
            or value(current.type) != 'Consumable' or uuid.UUID(str(current.appAccountToken)).hex != player
            or type(current.signedDate) is not int or type(minimum_date) is not int
            or minimum_date <= 0 or current.signedDate < minimum_date):
        raise ValueError('Current transaction binding')
    if not hasattr(current, 'revocationType') or not hasattr(current, 'revocationPercentage'):
        raise ValueError('Refund-capable SDK required')
    revocation = value(current.revocationType)
    if revocation is None and getattr(current, 'rawRevocationType', None) is not None:
        raise ValueError('Unknown revocation type')
    percentage = current.revocationPercentage
    if current.revocationDate is None:
        if revocation is not None or percentage not in (None, 0):
            raise ValueError('Inconsistent non-revoked transaction')
        percentage = 0
    else:
        if type(current.revocationDate) is not int or current.revocationDate <= 0:
            raise ValueError('Invalid revocation time')
        if revocation in (None, 'REFUND_FULL') and percentage is None:
            percentage = 100000
        if (revocation not in (None, 'REFUND_FULL', 'REFUND_PRORATED')
                or type(percentage) is not int or not 0 < percentage <= 100000
                or (revocation == 'REFUND_FULL' and percentage != 100000)):
            raise ValueError('Unsupported revocation state')
    return current.signedDate, percentage


def transaction_verifier(verifier, client, bundle, environment):
    """Recheck a known delivered purchase, even without an incoming notification."""
    if environment not in ('Sandbox', 'Production') or not bundle:
        raise ValueError('Explicit app/environment required')

    def verify(purchase):
        try:
            transaction, player, product = (purchase[k] for k in ('transaction_id', 'player', 'product'))
            signed_at, percentage = current_state(verifier, client, bundle, environment, transaction, player, product)
            digest = hashlib.sha256(encode([bundle, environment, transaction, player, product,
                                            signed_at, percentage]).encode()).hexdigest()
            event_id = str(uuid.uuid5(uuid.NAMESPACE_URL, 'nasus:refund-reconciliation:' + digest))
            return RefundState(event_id, digest, transaction, player, product, signed_at, percentage)
        except Exception as error:
            raise Fault(503, 'Refund reconciliation verification pending') from error
    return verify


def notification_verifier(verifier, client, bundle, environment):
    if environment not in ('Sandbox', 'Production') or not bundle:
        raise ValueError('Explicit app/environment required')

    def verify(payload):
        try:
            notice = verifier.verify_and_decode_notification(payload)
            if notice.version != '2.0': raise ValueError('Notification version')
            notification_id = str(uuid.UUID(notice.notificationUUID))
            kind = value(notice.notificationType)
            if kind not in ('REFUND', 'REFUND_REVERSED', 'REVOKE'):
                return None
            data = notice.data
            if data.bundleId != bundle or value(data.environment) != environment:
                raise ValueError('Notification app/environment')
            supplied = verifier.verify_and_decode_signed_transaction(data.signedTransactionInfo)
            if (supplied.bundleId != bundle or value(supplied.environment) != environment
                    or supplied.productId not in PRODUCTS or type(supplied.quantity) is not int or supplied.quantity != 1
                    or value(supplied.type) != 'Consumable'
                    or not supplied.appAccountToken):
                raise ValueError('Embedded transaction binding')
            player = uuid.UUID(str(supplied.appAccountToken)).hex
        except Exception as error:
            if getattr(getattr(error, 'status', None), 'name', None) == 'RETRYABLE_VERIFICATION_FAILURE':
                raise Fault(503, 'Certificate verification pending; retry notification') from error
            raise Fault(400, 'Invalid signed purchase notification') from error
        try:
            signed_at, percentage = current_state(verifier, client, bundle, environment,
                supplied.transactionId, player, supplied.productId, supplied.signedDate)
            return RefundState(notification_id, hashlib.sha256(payload.encode()).hexdigest(),
                supplied.transactionId, player, supplied.productId, signed_at, percentage)
        except Exception as error:
            # A timeout, unverifiable fresh state or unsupported field must not
            # be acknowledged as applied. Keep Apple retry/reconciliation possible.
            raise Fault(503, 'Refund verification pending; retry notification') from error
    return verify


def build_notification_verifier(config, *, sandbox_only=False):
    return _build_verifiers(config, sandbox_only=sandbox_only)[0]


def build_transaction_verifier(config, *, sandbox_only=False):
    return _build_verifiers(config, sandbox_only=sandbox_only)[1]


def _build_verifiers(config, *, sandbox_only=False):
    environment = config.get('WITCH_APPLE_ENVIRONMENT')
    if environment not in ('Sandbox', 'Production') or (sandbox_only and environment != 'Sandbox'):
        raise ValueError('Explicit allowed Apple environment required')
    from appstoreserverlibrary.api_client import AppStoreServerAPIClient
    from appstoreserverlibrary.models.Environment import Environment
    from appstoreserverlibrary.models.JWSTransactionDecodedPayload import JWSTransactionDecodedPayload
    from appstoreserverlibrary.signed_data_verifier import SignedDataVerifier
    if not all(hasattr(JWSTransactionDecodedPayload(), name) for name in ('revocationType', 'revocationPercentage')):
        raise ValueError('Refund-capable SDK required')
    env = Environment.SANDBOX if environment == 'Sandbox' else Environment.PRODUCTION
    bundle = config['WITCH_APPLE_BUNDLE_ID']
    app_id = int(config['WITCH_APPLE_APP_ID']) if environment == 'Production' else None
    roots = [p.read_bytes() for p in sorted(Path(config['WITCH_APPLE_ROOTS_DIR']).glob('*.cer'))]
    if not roots: raise ValueError('Apple roots required')
    verifier = SignedDataVerifier(roots, True, env, bundle, app_id)
    client = AppStoreServerAPIClient(Path(config['WITCH_APPLE_KEY_FILE']).read_bytes(),
        config['WITCH_APPLE_KEY_ID'], config['WITCH_APPLE_ISSUER_ID'], bundle, env)
    return (notification_verifier(verifier, client, bundle, environment),
            transaction_verifier(verifier, client, bundle, environment))
