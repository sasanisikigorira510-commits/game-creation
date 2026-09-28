"""Loopback-only, disposable Unity transport fixture. NEVER packaged/deployed.

Apple identity is synthetic; signature checks are tested separately with RSA.
"""
import json
import secrets
import sys
import tempfile
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from account_linking import AccountLinking
from account_deletion import AccountDeletion
from apple_grants import AppleGrantVault
from apple_tokens import AppleGrant
from application import Application
from security import AdminAuth
from server import create_server
from store import Store, Fault


def verify(token, nonce):
    if token != 'synthetic-unity:' + nonce:
        raise Fault(401, 'Invalid synthetic identity')
    return 'synthetic-unity-apple-user'

verify.audience = 'com.test.unity'


class TestTokens:
    client_id = verify.audience
    calls = 0

    def exchange(self, code, identity, nonce):
        if code != 'synthetic-code':
            raise Fault(400, 'Synthetic authorization code is required')
        self.calls += 1
        if self.calls > 2:
            raise Fault(409, 'Single-use code must not be exchanged again')
        return AppleGrant(verify(identity, nonce), 'synthetic-refresh-' + str(self.calls))


class TestAccounts(AccountLinking):
    def verify(self, challenge, token, identity_token, authorization_code=None):
        if identity_token is not None and authorization_code != 'synthetic-code':
            raise Fault(400, 'Synthetic authorization code must traverse the native/client/API boundary')
        result = super().verify(challenge, token, identity_token, authorization_code)
        if result['Status'] == 'preview' and not getattr(self, 'lost_response', False):
            self.lost_response = True
            raise Fault(503, 'Synthetic lost response after durable verification')
        if result['Status'] == 'linked' and not getattr(self, 'seeded', False):
            # Simulate two committed responses that never reached the old device:
            # no updated client snapshot exists when the new device recovers.
            self.store.purchase_verifier = lambda r: dict(verified=True, store='apple',
                transaction='synthetic-paid-tx', product=r['Target'])
            self.store.operation(result['PlayerId'], dict(RequestId='fixture-purchase', Epoch=0,
                Kind='purchase', Target='com.nasus.dungeonmonsterroguelike.crystals120',
                TransactionId='synthetic-paid-tx', Receipt='synthetic'), token)
            self.store.operation(result['PlayerId'], dict(RequestId='fixture-summon', Epoch=0,
                Kind='gacha', Count=1, Paid=False), token)
            self.seeded = True
        return result


class TestDeletion(AccountDeletion):
    def commit(self, player, token, confirmation):
        result = super().commit(player, token, confirmation)
        if not getattr(self, 'lost_deletion_response', False):
            self.lost_deletion_response = True
            raise Fault(503, 'Synthetic lost response after committed deletion')
        return result


if __name__ == '__main__':
    with tempfile.TemporaryDirectory(prefix='NasusAppleUnity_') as folder:
        store = Store(Path(folder) / 'test.sqlite', json.loads(Path(__file__).resolve().parents[1].joinpath('catalog.json').read_text()))
        http = create_server(store, 'synthetic-admin', int(sys.argv[1]))
        vault = AppleGrantVault(store, secrets.token_bytes(32), verify.audience)
        accounts = TestAccounts(store, verify, tokens=TestTokens(), vault=vault)
        http.set_app(Application(store, AdminAuth(token='synthetic-admin'), account_linking=accounts,
                                 account_deletion=TestDeletion(store, account_linking=accounts)))
        http.serve_forever()
