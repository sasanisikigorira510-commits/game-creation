"""Expiring device-QA gate. Never used by the normal production factory.

The random gate token is additional to, not a replacement for, player auth.
Provisioning and proxy changes are separate explicit deployment operations.
"""
import hashlib
import hmac
import json
import math
import os
import re
import time

from apple_revocation_runtime import read_private, require, unique

HEADER = 'HTTP_AUTHORIZATION'
MAX_LIFETIME = 24 * 3600
POST = frozenset(('/v1/accounts', '/v1/apple/challenge', '/v1/apple/verify',
    '/v1/apple/commit', '/v1/apple/unlink/preview', '/v1/apple/unlink/commit',
    '/v1/account-deletion/preview', '/v1/account-deletion/commit',
    '/v1/account-deletion/status', '/v1/account-deletion/cancel'))


def allowed(method, path):
    if method in ('GET', 'HEAD') and path == '/healthz': return True
    if method == 'POST' and path in POST: return True
    if method == 'GET': return re.fullmatch(r'/v1/players/[a-f0-9]{32}/head', path) is not None
    if method == 'POST':
        return re.fullmatch(r'/v1/players/[a-f0-9]{32}/(snapshots|operations)', path) is not None
    return False


class QaGateway:
    def __init__(self, app, config, *, clock=time.time, monotonic=time.monotonic):
        require(isinstance(config, dict) and set(config) == {'Version','TokenSha256','IssuedUnix','ExpiresUnix'})
        require(type(config['Version']) is int and config['Version'] == 1)
        require(isinstance(config['TokenSha256'], str) and re.fullmatch('[a-f0-9]{64}',config['TokenSha256']))
        issued, expires = config['IssuedUnix'], config['ExpiresUnix']
        require(all(type(v) in (int,float) and math.isfinite(v) for v in (issued,expires)))
        require(0 < expires-issued <= MAX_LIFETIME)
        now = clock()
        require(issued <= now < expires)
        self.app, self.clock, self.monotonic = app, clock, monotonic
        self.issued, self.expires = issued, expires
        self.deadline = monotonic() + expires-now
        self.digest = config['TokenSha256']

    def __call__(self, environ, start_response):
        # Keep both credentials in the standard redacted Authorization header,
        # rather than a custom header a proxy error log might expose.
        authorization = environ.get(HEADER, '')
        fields = authorization.split(' ',2) if isinstance(authorization,str) else []
        token = fields[1] if len(fields)>=2 and fields[0]=='NasusQA' else ''
        player_auth = fields[2] if len(fields)==3 else ''
        now = self.clock()
        valid = (type(token) is str and re.fullmatch('[a-f0-9]{64}',token) is not None
                 and self.issued <= now < self.expires and self.monotonic() < self.deadline
                 and hmac.compare_digest(hashlib.sha256(token.encode('ascii')).hexdigest(),self.digest)
                 and environ.get('REMOTE_ADDR') in ('127.0.0.1','::1')
                 and environ.get('wsgi.url_scheme') == 'https')
        if not valid or not allowed(environ.get('REQUEST_METHOD'), environ.get('PATH_INFO','')):
            # Identical responses; no echo/logging of paths, keys or identifiers.
            body = b'{"Error":"Not found"}'
            start_response('404 Not Found', [('Content-Type','application/json'),
                ('Cache-Control','no-store'),('Content-Length',str(len(body)))])
            return [b'' if environ.get('REQUEST_METHOD') == 'HEAD' else body]
        forwarded = dict(environ)
        forwarded[HEADER] = player_auth
        return self.app(forwarded, start_response)


def application_factory():
    from apple_application import create_apple_app
    os.umask(0o077)
    config = json.loads(read_private(os.environ['WITCH_QA_GATE_CONFIG'],4096),object_pairs_hook=unique)
    # Validate the gate before constructing the DB-backed application.
    gate = QaGateway(None, config)
    gate.app = create_apple_app(os.environ)
    return gate
