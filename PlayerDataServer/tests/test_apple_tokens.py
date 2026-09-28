"""Synthetic signing keys and fake transport only; no Apple network requests."""
import io
import http.client
import json
import secrets
import sys
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    import jwt
    from cryptography.hazmat.primitives.asymmetric import ec, rsa
    from apple_identity import AppleIdentityVerifier
    from apple_tokens import AppleTokenClient, _NoRedirect
except ImportError:
    jwt = None
from store import Fault


class Response(io.BytesIO):
    def __init__(self, raw, code=200):
        super().__init__(raw)
        self.code = code


@unittest.skipIf(jwt is None, 'Install requirements-identity.txt')
class AppleTokenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.apple_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        cls.team_key = ec.generate_private_key(ec.SECP256R1())

    def setUp(self):
        keys = SimpleNamespace(get_signing_key_from_jwt=lambda token: SimpleNamespace(
            key=self.apple_key.public_key(), algorithm_name='RS256', key_type='RSA'))
        self.verifier = AppleIdentityVerifier('com.test.game', keys)
        self.requests, self.error = [], None
        self.response = dict(access_token='synthetic-access', refresh_token='synthetic-refresh',
                             token_type='Bearer', expires_in=3600, id_token=self.signed())
        self.raw = None
        self.client = AppleTokenClient('com.test.game', 'TEAM123456', 'KEY1234567', self.team_key,
                                       self.verifier, opener=SimpleNamespace(open=self.open))

    def signed(self, **changes):
        claims = dict(iss='https://appleid.apple.com', aud='com.test.game', sub='synthetic-user',
                      iat=int(time.time()), exp=int(time.time()) + 300, nonce='synthetic-nonce')
        return jwt.encode(dict(claims, **changes), self.apple_key, algorithm='RS256', headers={'kid': 'test'})

    def open(self, request, timeout):
        self.requests.append(request)
        self.assertEqual(10, timeout)
        if self.error: raise self.error
        return Response(json.dumps(self.response).encode() if self.raw is None else self.raw)

    def exchange(self, code='synthetic-code', identity=None):
        return self.client.exchange(code, identity or self.signed(), 'synthetic-nonce')

    def fields(self):
        return {key: values[0] for key, values in urllib.parse.parse_qs(self.requests[-1].data.decode()).items()}

    def test_exchange_verifies_both_tokens_and_returns_minimum_grant(self):
        grant = self.exchange()
        self.assertEqual('synthetic-user', grant.subject)
        self.assertEqual('synthetic-refresh', grant.refresh_token)
        self.assertNotIn('synthetic-', repr(grant))
        self.assertEqual('https://appleid.apple.com/auth/token', self.requests[0].full_url)
        self.assertEqual('POST', self.requests[0].method)
        self.assertEqual('authorization_code', self.fields()['grant_type'])
        self.assertEqual('synthetic-code', self.fields()['code'])
        self.assertNotIn('redirect_uri', self.fields())

    def test_client_secret_is_short_lived_es256_scoped_to_native_app(self):
        self.exchange()
        token = self.fields()['client_secret']
        claims = jwt.decode(token, self.team_key.public_key(), algorithms=['ES256'],
                            audience='https://appleid.apple.com', issuer='TEAM123456')
        self.assertEqual('com.test.game', claims['sub'])
        self.assertEqual(300, claims['exp'] - claims['iat'])
        self.assertEqual('KEY1234567', jwt.get_unverified_header(token)['kid'])

    def test_code_uses_form_encoding_not_url_or_query(self):
        self.exchange('code+with/special=&characters')
        self.assertEqual('code+with/special=&characters', self.fields()['code'])
        self.assertEqual('', urllib.parse.urlparse(self.requests[0].full_url).query)

    def test_bad_code_never_contacts_apple(self):
        for code in (None, '', 'a' * 16385, 'has space', 'has\nnewline', '全角', {}, []):
            with self.subTest(code_type=type(code)), self.assertRaises(Fault) as error:
                self.exchange(code)
            self.assertEqual(400, error.exception.status)
        self.assertEqual([], self.requests)

    def test_bad_native_identity_does_not_consume_code(self):
        with self.assertRaises(Fault): self.exchange(identity=self.signed(nonce='wrong'))
        self.assertEqual([], self.requests)

    def test_exchanged_identity_must_match_subject_nonce_audience_and_signature(self):
        for changes in (dict(sub='other-user'), dict(nonce='other-nonce'), dict(aud='other-app')):
            self.response['id_token'] = self.signed(**changes)
            with self.subTest(changes=changes), self.assertRaises(Fault) as error: self.exchange()
            self.assertEqual(401, error.exception.status)
        self.response['id_token'] = jwt.encode({}, 'different-key' * 4, algorithm='HS256')
        with self.assertRaises(Fault): self.exchange()

    def test_malformed_response_never_produces_grant(self):
        original = dict(self.response)
        for changes in (dict(refresh_token=''), dict(refresh_token=None), dict(access_token=''),
                        dict(expires_in=True), dict(expires_in=0), dict(expires_in='3600'),
                        dict(token_type='other'), dict(refresh_token='x\ny')):
            self.response = dict(original, **changes)
            with self.subTest(changes=changes), self.assertRaises(Fault): self.exchange()
        self.response = original
        for raw in (b'null', b'[]', b'not-json', b'x' * 65537, b'\xff'):
            self.raw = raw
            with self.subTest(raw_length=len(raw)), self.assertRaises(Fault) as error: self.exchange()
            self.assertEqual(503, error.exception.status)

    def test_provider_errors_are_sanitized_and_not_automatically_retried(self):
        cases = [(400, 'invalid_grant', 401), (400, 'invalid_client', 503), (429, 'slow_down', 503),
                 (500, 'server_error', 503)]
        for status, category, expected in cases:
            self.requests.clear()
            self.error = urllib.error.HTTPError('https://appleid.apple.com/auth/token', status, 'private-detail', {},
                                                io.BytesIO(json.dumps(dict(error=category, secret='never-print')).encode()))
            with self.subTest(status=status), self.assertRaises(Fault) as error: self.exchange()
            self.assertEqual(expected, error.exception.status)
            self.assertNotIn('never-print', str(error.exception))
            self.assertNotIn('private-detail', str(error.exception))
            self.assertEqual(1, len(self.requests))

    def test_timeout_does_not_retry_a_single_use_code_or_expose_exception(self):
        self.error = TimeoutError('sensitive-token')
        with self.assertRaises(Fault) as error: self.exchange()
        self.assertEqual(503, error.exception.status)
        self.assertNotIn('sensitive-token', str(error.exception))
        self.assertEqual(1, len(self.requests))

    def test_truncated_http_response_is_sanitized(self):
        self.error = http.client.IncompleteRead(b'private-response-data')
        with self.assertRaises(Fault) as error: self.exchange()
        self.assertEqual(503, error.exception.status)
        self.assertNotIn('private-response-data', str(error.exception))

    def test_exchange_vault_and_revocation_round_trip_leave_game_data_unchanged(self):
        import tempfile
        from apple_grants import AppleGrantVault
        from store import Store
        with tempfile.TemporaryDirectory() as directory:
            store = Store(Path(directory) / 'test.sqlite')
            vault = AppleGrantVault(store, secrets.token_bytes(32), 'com.test.game')
            grant = self.exchange()
            with store.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                vault.save(db, grant)
                vault.queue(db, vault.subject_hash(grant.subject))
            self.raw = b''
            self.assertEqual('revoked', vault.process_one(self.client))
            self.assertEqual('synthetic-refresh', self.fields()['token'])
            self.assertEqual([], store.players())
            with store.connect() as db:
                self.assertEqual(0, db.execute('SELECT count(*) FROM apple_grants').fetchone()[0])

    def test_revocation_accepts_empty_200_and_uses_refresh_hint(self):
        self.raw = b''
        self.client.revoke('synthetic-refresh')
        self.client.revoke('synthetic-refresh')
        self.assertEqual('https://appleid.apple.com/auth/revoke', self.requests[-1].full_url)
        self.assertEqual('refresh_token', self.fields()['token_type_hint'])
        self.assertEqual('synthetic-refresh', self.fields()['token'])

    def test_failed_revoke_is_not_reported_as_success(self):
        for status in (400, 401, 429, 500):
            self.error = urllib.error.HTTPError('https://appleid.apple.com/auth/revoke', status, 'sensitive-detail', {},
                                                io.BytesIO(b'{"error":"invalid_grant"}'))
            with self.subTest(status=status), self.assertRaises(Fault) as error:
                self.client.revoke('synthetic-refresh')
            self.assertEqual(503, error.exception.status)

    def test_invalid_config_and_redirects_are_refused(self):
        for key in (self.apple_key, ec.generate_private_key(ec.SECP384R1()), None):
            with self.assertRaises(ValueError):
                AppleTokenClient('com.test.game', 'TEAM123456', 'KEY1234567', key, self.verifier)
        with self.assertRaises(ValueError):
            AppleTokenClient('other.app', 'TEAM123456', 'KEY1234567', self.team_key, self.verifier)
        for status in (301, 302, 303, 307, 308):
            self.assertIsNone(_NoRedirect().redirect_request(urllib.request.Request('https://appleid.apple.com/auth/token'),
                                                            None, status, 'redirect', {}, 'https://attacker.invalid'))


if __name__ == '__main__': unittest.main()
