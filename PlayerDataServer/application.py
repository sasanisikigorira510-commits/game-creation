"""Shared WSGI routes for local validation and the production Gunicorn worker."""
import json
import logging
import time
import uuid
from http import HTTPStatus
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

from security import RateLimiter, client_network
from store import Fault, encode

MAX_BODY = 4 * 1024 * 1024
ASSETS = Path(__file__).parent


class Application:
    def __init__(self, store, admin_auth, public_origin=None, limiter=None, backup_health=None, account_linking=None,
                 account_deletion=None, purchase_notifications=None, refund_review=None, refund_health=None):
        self.store, self.admin_auth = store, admin_auth
        self.public_origin = public_origin
        self.production = public_origin is not None
        self.limiter = limiter or RateLimiter()
        self.backup_health = backup_health
        self.account_linking = account_linking
        self.account_deletion = account_deletion
        self.purchase_notifications = purchase_notifications
        self.refund_review, self.refund_health = refund_review, refund_health
        if refund_review is not None and refund_review.store is not store:
            raise ValueError('Refund review must use the application database')
        if refund_health is not None and not callable(refund_health):
            raise ValueError('Refund health must be an explicit read-only callback')
        if purchase_notifications is not None and purchase_notifications.store is not store:
            raise ValueError('Purchase notifications must use the application database')
        if account_deletion is not None and account_deletion.store is not store:
            raise ValueError('Deletion must use the application database')
        if self.production:
            url = urlsplit(public_origin)
            if (url.scheme != 'https' or not url.hostname or url.username or url.password or
                    url.path or url.query or url.fragment or url.netloc != url.netloc.lower()):
                raise ValueError('Public origin must be an HTTPS origin without a path')
            self.host = url.netloc

    @staticmethod
    def body(environ):
        # Chunked and ambiguous lengths are rejected by Gunicorn/proxy as well.
        if environ.get('HTTP_TRANSFER_ENCODING'):
            raise Fault(400, 'Content-Length required')
        length = environ.get('CONTENT_LENGTH', '')
        if not length.isascii() or not length.isdigit():
            raise Fault(400, 'Invalid Content-Length')
        size = int(length)
        if not 0 < size <= MAX_BODY:
            raise Fault(413, 'Request too large or empty')
        if environ.get('CONTENT_TYPE', '').split(';')[0].strip().lower() != 'application/json':
            raise Fault(415, 'JSON required')
        raw = environ['wsgi.input'].read(size)
        if len(raw) != size:
            raise Fault(400, 'Incomplete request')
        value = json.loads(raw, parse_constant=lambda _: (_ for _ in ()).throw(ValueError('Invalid number')))
        if not isinstance(value, dict):
            raise Fault(400, 'JSON object required')
        return value

    def __call__(self, environ, start_response):
        started, request_id = time.monotonic(), uuid.uuid4().hex
        kind = 'unknown'
        mime = 'application/json; charset=utf-8'
        try:
            host = environ.get('HTTP_HOST', '')
            if self.production and host != self.host:
                raise Fault(400, 'Invalid host')
            expected_origin = self.public_origin or 'http://' + host
            if environ.get('HTTP_ORIGIN') not in (None, expected_origin):
                raise Fault(403, 'Cross-origin request rejected')
            network = client_network(environ, self.production)
            if self.production and environ.get('wsgi.url_scheme') != 'https':
                raise Fault(403, 'HTTPS required')
            method, path = environ['REQUEST_METHOD'], environ.get('PATH_INFO', '/')
            health_head = method == 'HEAD' and path == '/healthz'
            if method not in ('GET', 'POST') and not health_head:
                raise Fault(405, 'Method not allowed')
            if self.production:
                self.limiter.check('ip', network, 600, 60)
            auth = environ.get('HTTP_AUTHORIZATION', '')
            token = auth[7:] if auth.startswith('Bearer ') else ''
            if len(token) > 256:
                raise Fault(401, 'Unauthorized')
            parts = path.strip('/').split('/')
            # Reject suffixes and trailing separators rather than aliasing routes.
            if path != '/' and path != '/' + '/'.join(parts):
                raise Fault(404, 'Not found')
            get = method == 'GET' or health_head
            status = 200
            if get and path in ('/', '/admin.js'):
                kind = 'asset'
                result = (ASSETS / ('admin.html' if path == '/' else 'admin.js')).read_bytes()
                mime = 'text/html; charset=utf-8' if path == '/' else 'application/javascript; charset=utf-8'
            elif get and path == '/healthz':
                kind = 'health'
                # Checks database access without publishing account counts or data.
                with self.store.connect() as db:
                    db.execute('SELECT id FROM players LIMIT 1').fetchone()
                if self.backup_health is not None and not self.backup_health():
                    raise Fault(503, 'Service unavailable')
                if self.refund_health is not None and not self.refund_health():
                    raise Fault(503, 'Service unavailable')
                result = {'Status': 'ok'}
            elif not get and path == '/v1/store/apple/notifications' and self.purchase_notifications is not None:
                kind = 'purchase_notification'
                self.limiter.check('purchase_notification', network, 120, 60)
                result = self.purchase_notifications.receive(self.body(environ))
            elif not get and path == '/v1/accounts':
                kind = 'registration'
                if self.production:
                    self.limiter.check('registration', network, 20, 3600)
                b = self.body(environ)
                result = self.store.register(b.get('PlayerId'), b.get('Token'), b.get('Legacy', False))
            elif not get and path in ('/v1/apple/challenge', '/v1/apple/verify', '/v1/apple/commit',
                                     '/v1/apple/unlink/preview', '/v1/apple/unlink/commit'):
                kind = 'apple_identity'
                # Deliberately disabled by default, including the production
                # factory. No client flag can enable this dependency.
                if self.account_linking is None:
                    raise Fault(503, 'Apple account linking is not enabled')
                self.limiter.check('apple_identity', network, 30, 300)
                b = self.body(environ)
                if path == '/v1/apple/unlink/preview':
                    result = self.account_linking.unlink_preview(b.get('PlayerId'), token)
                elif path == '/v1/apple/unlink/commit':
                    result = self.account_linking.unlink_commit(b.get('PlayerId'), token, b.get('ConfirmationToken'))
                elif path.endswith('/challenge'):
                    result = self.account_linking.start(b.get('Mode'), token, b.get('PlayerId'))
                elif path.endswith('/verify'):
                    result = self.account_linking.verify(b.get('ChallengeId'), token, b.get('IdentityToken'), b.get('AuthorizationCode'))
                else:
                    result = self.account_linking.commit(b.get('ChallengeId'), token, b.get('PreviewToken'))
            elif not get and path in ('/v1/account-deletion/preview', '/v1/account-deletion/commit',
                                     '/v1/account-deletion/status', '/v1/account-deletion/cancel'):
                kind = 'account_deletion'
                if self.account_deletion is None:
                    raise Fault(503, 'Game account deletion is not enabled')
                self.limiter.check('account_deletion', network, 30, 300)
                b = self.body(environ)
                if path.endswith('/preview'):
                    result = self.account_deletion.preview(b.get('PlayerId'), token)
                elif path.endswith('/commit'):
                    result = self.account_deletion.commit(b.get('PlayerId'), token, b.get('ConfirmationToken'))
                elif path.endswith('/cancel'):
                    result = self.account_deletion.cancel(b.get('PlayerId'), token, b.get('ConfirmationToken'))
                else:
                    result = self.account_deletion.status(b.get('PlayerId'), token, b.get('ConfirmationToken'))
            elif len(parts) == 4 and parts[:2] == ['v1', 'players']:
                kind = 'player'
                player, resource = parts[2:4]
                self.store.authenticate(player, token)
                if self.production:
                    self.limiter.check('player', player, 120, 60)
                if get and resource == 'head':
                    after = int(parse_qs(environ.get('QUERY_STRING', '')).get('after', ['0'])[0])
                    if after < 0: raise Fault(400, 'Invalid cursor')
                    result = self.store.head(player, after, token=token)
                elif not get and resource == 'snapshots':
                    result = self.store.snapshot(player, self.body(environ), token=token)
                elif not get and resource == 'operations':
                    result = self.store.operation(player, self.body(environ), token=token)
                else: raise Fault(404, 'Not found')
            elif parts[0] == 'admin':
                kind = 'admin'
                if self.production:
                    self.limiter.check('admin', network, 60, 60)
                identity = self.admin_auth.authenticate(token)
                if not get and parts in (['admin', 'refunds', 'preview'], ['admin', 'refunds', 'commit']):
                    if self.refund_review is None:
                        raise Fault(404, 'Not found')
                    # A shared local admin token has no accountable identity.
                    # Never accept a claimed Actor from a request body here.
                    if identity['Role'] != 'operator' or identity['ActorFromCredential'] is not True:
                        raise Fault(403, 'Named operator credential required')
                    self.limiter.check('refund_review', identity['Name'], 20, 60)
                    b = self.body(environ)
                    expected = {'PlayerId', 'TransactionId'}
                    if parts[2] == 'commit':
                        expected |= {'Amount', 'ExpectedEconomyRevision', 'RequestId', 'Case', 'Confirm'}
                    if set(b) != expected:
                        raise Fault(400, 'Unexpected review fields')
                    if parts[2] == 'preview':
                        result = self.refund_review.preview(b['PlayerId'], b['TransactionId'])
                    else:
                        if b['Confirm'] is not True:
                            raise Fault(400, 'Explicit review confirmation required')
                        result = self.refund_review.commit(player=b['PlayerId'], transaction=b['TransactionId'],
                            amount=b['Amount'], expected_revision=b['ExpectedEconomyRevision'],
                            request_id=b['RequestId'], actor=identity['Name'], case=b['Case'])
                elif get and parts == ['admin', 'session']:
                    result = identity
                elif get and parts == ['admin', 'players']:
                    result = {'Players': self.store.players()}
                elif len(parts) >= 3 and parts[1] == 'players':
                    player = parts[2]
                    if get and len(parts) == 3:
                        result = self.store.inspect(player)
                    elif not get and len(parts) == 4:
                        if identity['Role'] != 'operator':
                            raise Fault(403, 'Read-only operator')
                        b = self.body(environ)
                        actor = identity['Name'] if identity['ActorFromCredential'] else b.get('Actor', '')
                        reason = b.get('Reason', '')
                        if parts[3] == 'freeze': result = self.store.freeze(player, b.get('Frozen'), actor, reason)
                        elif parts[3] in ('preview', 'restore'):
                            result = self.store.restore(player, b.get('SnapshotId'), b.get('Epoch'), actor, reason, parts[3] == 'restore', b.get('PreviewToken'))
                        elif parts[3] in ('migrate', 'adjust'):
                            result = self.store.adjust(player, b.get('Free'), b.get('Paid'), b.get('ExpectedEconomyRevision'), actor, reason, parts[3] == 'migrate')
                        else: raise Fault(404, 'Not found')
                    else: raise Fault(404, 'Not found')
                else: raise Fault(404, 'Not found')
            else: raise Fault(404, 'Not found')
        except Fault as error:
            status, result = error.status, {'Error': error.message}
        except (ValueError, TypeError, KeyError, UnicodeError, RecursionError):
            status, result = 400, {'Error': 'Malformed request'}
        except Exception:
            status, result = 503, {'Error': 'Service unavailable; retry with the same request ID'}
        raw = result if isinstance(result, bytes) else encode(result).encode()
        headers = [('Content-Type', mime), ('Content-Length', str(len(raw))),
                   ('Cache-Control', 'no-store'), ('X-Content-Type-Options', 'nosniff'),
                   ('Referrer-Policy', 'no-referrer'), ('X-Request-ID', request_id),
                   ('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")]
        if status == 429: headers.append(('Retry-After', '60'))
        if status == 405: headers.append(('Allow', 'GET, POST'))
        if self.production:
            headers.append(('Strict-Transport-Security', 'max-age=31536000'))
            # No URLs, IP addresses, authorization, request bodies or exception text.
            logging.getLogger('player_data').info(encode(dict(Event='http', RequestId=request_id,
                Route=kind, Status=status, DurationMs=round((time.monotonic() - started) * 1000))))
        start_response(f'{status} {HTTPStatus(status).phrase}', headers)
        return [b'' if environ.get('REQUEST_METHOD') == 'HEAD' else raw]
