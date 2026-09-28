"""Synthetic RSA keys only: no real Apple accounts, secrets or network calls."""
import sys
import time
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    import jwt
    from cryptography.hazmat.primitives.asymmetric import rsa
    from apple_identity import AppleIdentityVerifier
except ImportError:
    jwt = None
from store import Fault


@unittest.skipIf(jwt is None, 'Install requirements-identity.txt to test the optional identity verifier')
class AppleIdentityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.private = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        cls.other = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    def setUp(self):
        self.key_calls = 0
        owner = self
        class Keys:
            def get_signing_key_from_jwt(self, token):
                owner.key_calls += 1
                return SimpleNamespace(key=owner.private.public_key(), algorithm_name='RS256', key_type='RSA')
        self.verifier = AppleIdentityVerifier('com.test.game', Keys())
        self.claims = dict(iss='https://appleid.apple.com', aud='com.test.game',
                           sub='apple-subject', nonce='server-nonce', iat=int(time.time()), exp=int(time.time()) + 300)

    def signed(self, **changes):
        return jwt.encode(dict(self.claims, **changes), self.private, algorithm='RS256', headers={'kid': 'synthetic-key'})

    def rejected(self, token, status=401):
        with self.assertRaises(Fault) as error: self.verifier(token, 'server-nonce')
        self.assertEqual(status, error.exception.status)

    def test_valid_subject_only_not_email_or_client_user(self):
        self.assertEqual('apple-subject', self.verifier(self.signed(email='ignored@example.com'), 'server-nonce'))

    def test_wrong_signature_issuer_audience_nonce_expiry_and_future_iat(self):
        for changes in (dict(iss='https://attacker.invalid'), dict(aud='com.other.game'),
                        dict(aud=['com.test.game', 'other']), dict(nonce='wrong'),
                        dict(exp=int(time.time()) - 1), dict(iat=int(time.time()) + 100), dict(sub=''),
                        dict(iat=True), dict(exp=str(int(time.time()) + 300))):
            with self.subTest(changes=changes): self.rejected(self.signed(**changes))
        self.rejected(jwt.encode(self.claims, self.other, algorithm='RS256', headers={'kid': 'synthetic-key'}))

    def test_required_claims_cannot_be_omitted(self):
        for field in self.claims:
            claims = dict(self.claims); del claims[field]
            with self.subTest(field=field):
                self.rejected(jwt.encode(claims, self.private, algorithm='RS256', headers={'kid': 'synthetic-key'}))

    def test_no_unsigned_hmac_or_header_selected_key_endpoints(self):
        self.rejected(jwt.encode(self.claims, '', algorithm='none', headers={'kid': 'synthetic-key'}))
        self.rejected(jwt.encode(self.claims, 'attacker' * 8, algorithm='HS256', headers={'kid': 'synthetic-key'}))
        self.assertEqual(0, self.key_calls)
        # jku is ignored; the fixed key provider cannot be changed by the token.
        token = jwt.encode(self.claims, self.other, algorithm='RS256', headers={'kid': 'synthetic-key', 'jku': 'https://attacker.invalid'})
        self.rejected(token)

    def test_malformed_and_oversized_tokens_are_rejected(self):
        for token in (None, '', 'not-a-jwt', 'a' * 16385, {}, []):
            self.rejected(token)

    def test_apple_key_service_failure_does_not_accept_identity(self):
        def unavailable(token): raise jwt.PyJWKClientConnectionError('private network details')
        self.verifier.keys.get_signing_key_from_jwt = unavailable
        self.rejected(self.signed(), 503)

    def test_critical_header_and_wrong_key_algorithm_rejected(self):
        self.rejected(jwt.encode(self.claims, self.private, algorithm='RS256',
                                headers={'kid': 'synthetic-key', 'crit': ['unsupported']}))
        self.assertEqual(0, self.key_calls)
        self.verifier.keys.get_signing_key_from_jwt = lambda token: SimpleNamespace(
            key=self.private.public_key(), algorithm_name='PS256', key_type='RSA')
        self.rejected(self.signed())


if __name__ == '__main__': unittest.main()
