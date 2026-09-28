"""Apple's official verifier and fresh transaction API, with account binding."""
import os
import uuid
from pathlib import Path
from store import Fault


def build_verifier(environ=None, *, sandbox_only=False):
    config = os.environ if environ is None else environ
    environment = config.get('WITCH_APPLE_ENVIRONMENT')
    if environment not in ('Sandbox', 'Production') or (sandbox_only and environment != 'Sandbox'):
        raise ValueError('An explicit allowed Apple environment is required')
    from appstoreserverlibrary.api_client import AppStoreServerAPIClient
    from appstoreserverlibrary.models.Environment import Environment
    from appstoreserverlibrary.signed_data_verifier import SignedDataVerifier
    bundle = config['WITCH_APPLE_BUNDLE_ID']
    env = Environment.SANDBOX if environment == 'Sandbox' else Environment.PRODUCTION
    app_id = int(config['WITCH_APPLE_APP_ID']) if env == Environment.PRODUCTION else None
    roots = [p.read_bytes() for p in sorted(Path(config['WITCH_APPLE_ROOTS_DIR']).glob('*.cer'))]
    if not roots: raise RuntimeError('Apple root certificates are required')
    verifier = SignedDataVerifier(roots, True, env, bundle, app_id)
    client = AppStoreServerAPIClient(Path(config['WITCH_APPLE_KEY_FILE']).read_bytes(),
                                    config['WITCH_APPLE_KEY_ID'], config['WITCH_APPLE_ISSUER_ID'], bundle, env)

    def verify(request):
        try:
            supplied = verifier.verify_and_decode_signed_transaction(request['Receipt'])
            if (supplied.transactionId != request['TransactionId']
                    or supplied.bundleId != bundle or supplied.environment != env):
                raise ValueError('Transaction mismatch')
            fresh = client.get_transaction_info(supplied.transactionId)
            transaction = verifier.verify_and_decode_signed_transaction(fresh.signedTransactionInfo)
            if (transaction.transactionId != supplied.transactionId
                    or transaction.bundleId != bundle or transaction.environment != env
                    or transaction.revocationDate is not None
                    or transaction.productId != request['Target'] or transaction.quantity != 1):
                raise ValueError('Revoked or mismatching transaction')
            if not transaction.appAccountToken or uuid.UUID(str(transaction.appAccountToken)).hex != request['_AuthenticatedPlayer']:
                raise ValueError('Account mismatch')
            return {'verified': True, 'store': 'apple', 'transaction': transaction.transactionId, 'product': transaction.productId}
        except Exception as error:
            # Never grant on errors or fall back to decoding an unverified JWS.
            raise Fault(503, 'Purchase verification is pending; retry without repurchasing') from error
    return verify
