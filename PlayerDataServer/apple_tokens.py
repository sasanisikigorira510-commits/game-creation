"""Sign in with Apple token exchange/revocation adapter (not enabled in production).

Native iOS flow only. Never accepts a client-selected URL or redirect URI. No
automatic retry of single-use authorization codes. No secrets in repr/errors.
"""
from dataclasses import dataclass, field
import hmac
import http.client
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request

import jwt
from cryptography.hazmat.primitives.asymmetric import ec

from store import Fault


class AppleCodeRejected(Fault):
    """Apple explicitly rejected the code, distinct from an unknown outcome."""
    def __init__(self):
        super().__init__(401, 'Apple authorization expired; sign in again')


@dataclass(frozen=True, repr=False)
class AppleGrant:
    subject: str
    refresh_token: str = field(repr=False)


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class AppleTokenClient:
    ORIGIN = 'https://appleid.apple.com'
    MAX_RESPONSE = 65536

    def __init__(self, client_id, team_id, key_id, private_key, verifier, *, clock=time.time, opener=None):
        if (not isinstance(client_id, str) or not re.fullmatch(r'[A-Za-z0-9.-]{1,255}', client_id)
                or not isinstance(team_id, str) or not re.fullmatch(r'[A-Z0-9]{10}', team_id)
                or not isinstance(key_id, str) or not re.fullmatch(r'[A-Z0-9]{10}', key_id)
                or not isinstance(private_key, ec.EllipticCurvePrivateKey)
                or not isinstance(private_key.curve, ec.SECP256R1)
                or not callable(verifier) or getattr(verifier, 'audience', None) != client_id):
            raise ValueError('Explicit matching Apple identity configuration and P-256 signing key required')
        self.client_id, self.team_id, self.key_id = client_id, team_id, key_id
        self._key, self.verifier, self.clock = private_key, verifier, clock
        # Test injection is constructor-only, never request-selected.
        self._opener = opener or urllib.request.build_opener(_NoRedirect())

    @staticmethod
    def _secret(value):
        return (isinstance(value, str) and 1 <= len(value) <= 16384
                and value.isascii() and all(33 <= ord(c) <= 126 for c in value))

    def _client_secret(self):
        issued = int(self.clock())
        return jwt.encode(dict(iss=self.team_id, sub=self.client_id, aud=self.ORIGIN,
                               iat=issued, exp=issued + 300), self._key, algorithm='ES256',
                          headers={'kid': self.key_id})

    def _post(self, path, fields):
        if path not in ('/auth/token', '/auth/revoke'):
            raise ValueError('Unsupported Apple endpoint')
        body = urllib.parse.urlencode(dict(fields, client_id=self.client_id,
                                          client_secret=self._client_secret())).encode('ascii')
        request = urllib.request.Request(self.ORIGIN + path, data=body, method='POST',
                                         headers={'Content-Type': 'application/x-www-form-urlencoded',
                                                  'Accept': 'application/json'})
        try:
            try:
                response = self._opener.open(request, timeout=10)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                status = response.code
                raw = response.read(self.MAX_RESPONSE + 1)
            if len(raw) > self.MAX_RESPONSE:
                raise ValueError('Oversized response')
            if path == '/auth/revoke' and status == 200 and not raw.strip():
                return {}
            data = json.loads(raw)
            if not isinstance(data, dict):
                raise ValueError('Invalid response')
            if status != 200 or 'error' in data:
                # invalid_client and all revocation errors are operational errors;
                # never treat a failed revoke as successful deletion of its token.
                if path == '/auth/token' and status == 400 and data.get('error') == 'invalid_grant':
                    raise AppleCodeRejected()
                raise Fault(503, 'Apple token service temporarily unavailable')
            return data
        except Fault:
            raise
        except (OSError, ValueError, TypeError, http.client.HTTPException, urllib.error.URLError):
            raise Fault(503, 'Apple token service temporarily unavailable') from None

    def exchange(self, authorization_code, identity_token, nonce):
        if not self._secret(authorization_code):
            raise Fault(400, 'A valid Apple authorization code is required')
        # Verify the native token BEFORE consuming the single-use code; verify
        # Apple's response independently and require the same subject and nonce.
        subject = self.verifier(identity_token, nonce)
        response = self._post('/auth/token', dict(grant_type='authorization_code', code=authorization_code))
        if (not self._secret(response.get('refresh_token')) or not self._secret(response.get('access_token'))
                or str(response.get('token_type', '')).lower() != 'bearer'
                or type(response.get('expires_in')) is not int or response['expires_in'] <= 0):
            raise Fault(503, 'Apple token response could not be verified')
        verified = self.verifier(response.get('id_token'), nonce)
        if not hmac.compare_digest(subject.encode('utf-8'), verified.encode('utf-8')):
            raise Fault(401, 'Apple authorization identity does not match')
        # Retain only the token needed for future revocation; no email/access JWT.
        return AppleGrant(subject, response['refresh_token'])

    def revoke(self, refresh_token):
        if not self._secret(refresh_token):
            raise Fault(503, 'Stored Apple authorization could not be verified')
        self._post('/auth/revoke', dict(token=refresh_token, token_type_hint='refresh_token'))
        # A prior successful revocation also returns 200, making retries safe.
