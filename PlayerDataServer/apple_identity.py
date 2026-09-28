"""Verify Apple identity tokens, independently of App Store purchase verification.

No client-supplied key URL, email address or unsigned subject is trusted. This
adapter only proves a fresh sign-in; authorization-code exchange/revocation is a
separate release prerequisite documented in deploy/APPLE-ACCOUNT-LINKING.md.
"""
import hmac
import jwt

from store import Fault


class AppleIdentityVerifier:
    def __init__(self, audience, keys=None):
        if not isinstance(audience, str) or not audience or len(audience) > 255:
            raise ValueError('An explicit native app bundle ID is required')
        self.audience = audience
        self.keys = keys or jwt.PyJWKClient('https://appleid.apple.com/auth/keys',
                                           timeout=5, lifespan=300, cooldown_duration=30)

    def __call__(self, token, nonce):
        if not isinstance(token, str) or not 1 <= len(token) <= 16384:
            raise Fault(401, 'Apple identity could not be verified')
        try:
            header = jwt.get_unverified_header(token)
            if (header.get('alg') != 'RS256' or not isinstance(header.get('kid'), str)
                    or not 1 <= len(header['kid']) <= 128 or header.get('crit')):
                raise ValueError('Invalid header')
            key = self.keys.get_signing_key_from_jwt(token)
            if key.algorithm_name != 'RS256' or key.key_type != 'RSA':
                raise ValueError('Invalid key')
            claims = jwt.decode(token, key.key, algorithms=['RS256'],
                                audience=self.audience, issuer='https://appleid.apple.com',
                                options={'require': ['iss', 'aud', 'exp', 'iat', 'sub', 'nonce'],
                                         'strict_aud': True})
            if (not isinstance(claims['nonce'], str) or not claims['nonce'].isascii()
                    or not hmac.compare_digest(claims['nonce'], nonce)
                    or type(claims['iat']) is not int or type(claims['exp']) is not int
                    or not isinstance(claims['sub'], str) or not 1 <= len(claims['sub']) <= 255):
                raise ValueError('Invalid identity binding')
            return claims['sub']
        except jwt.PyJWKClientConnectionError:
            raise Fault(503, 'Apple verification temporarily unavailable') from None
        except (jwt.PyJWTError, ValueError, TypeError, KeyError):
            raise Fault(401, 'Apple identity could not be verified') from None
