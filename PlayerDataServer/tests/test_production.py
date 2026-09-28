import io
import json
import os
import sqlite3
import sys
import tempfile
import tarfile
import unittest
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from application import MAX_BODY
from maintenance import (initialize, add_operator, backup_database, backup_generation,
                         verify_generation, restore_copy, prune_generations)
from production import create_app
from security import RateLimiter, client_network
from store import Store, Fault
from build_release import build, FILES


class ProductionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.directory = self.root / 'data'
        initialize(self.directory, 'test-instance-123')
        self.operators = self.root / 'operators.json'
        add_operator(self.operators, 'support-one', 'operator', self.root / 'write-token')
        add_operator(self.operators, 'support-two', 'viewer', self.root / 'read-token')
        self.write_token = (self.root / 'write-token').read_text().strip()
        self.read_token = (self.root / 'read-token').read_text().strip()
        self.env = dict(WITCH_DATA_DIR=str(self.directory), WITCH_INSTANCE_ID='test-instance-123',
                        WITCH_OPERATORS_FILE=str(self.operators), WITCH_PUBLIC_ORIGIN='https://api.example.com')
        self.app = create_app(self.env)
        self.player, self.token = uuid.uuid4().hex, 'p' * 64
        self.app.store.register(self.player, self.token)

    def tearDown(self): self.tmp.cleanup()

    def call(self, path, body=None, token='', **changes):
        raw = json.dumps(body).encode() if body is not None else b''
        env = {'REQUEST_METHOD': 'POST' if body is not None else 'GET', 'PATH_INFO': path,
               'wsgi.input': io.BytesIO(raw), 'wsgi.url_scheme': 'https', 'QUERY_STRING': '',
               'HTTP_HOST': 'api.example.com', 'REMOTE_ADDR': '127.0.0.1',
               'HTTP_X_REAL_IP': '203.0.113.15', 'HTTP_AUTHORIZATION': 'Bearer ' + token,
               'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw))}
        env.update(changes)
        response = {}
        def start(status, headers): response.update(status=int(status[:3]), headers=dict(headers))
        raw = b''.join(self.app(env, start))
        response['body'] = json.loads(raw) if raw and response['headers']['Content-Type'].startswith('application/json') else raw
        return response

    def test_https_proxy_host_origin_boundary(self):
        self.assertEqual(200, self.call('/healthz')['status'])
        cases = [({'HTTP_HOST': 'attacker.invalid'}, 400), ({'REMOTE_ADDR': '203.0.113.1'}, 403),
                 ({'HTTP_X_REAL_IP': ''}, 400), ({'wsgi.url_scheme': 'http'}, 403),
                 ({'HTTP_ORIGIN': 'https://attacker.invalid'}, 403)]
        for changes, status in cases:
            with self.subTest(changes=changes): self.assertEqual(status, self.call('/healthz', **changes)['status'])
        self.assertEqual(200, self.call('/healthz', HTTP_ORIGIN='https://api.example.com')['status'])

    def test_actor_comes_from_credential_not_request(self):
        response = self.call('/admin/players/' + self.player + '/freeze',
                             dict(Frozen=True, Actor='impersonated-boss', Reason='ticket-1'), self.write_token)
        self.assertEqual(200, response['status'])
        audit = self.app.store.inspect(self.player)['Audit']
        self.assertEqual('support-one', audit[0]['actor'])
        self.assertEqual('ticket-1', audit[0]['reason'])

    def test_refund_controls_are_not_enabled_by_production_factory(self):
        self.assertIsNone(self.app.refund_review)
        self.assertIsNone(self.app.refund_health)
        self.assertFalse(self.app.store.refunds_enabled)
        for route in ('preview', 'commit'):
            self.assertEqual(404, self.call('/admin/refunds/' + route, {}, self.write_token)['status'])

    def test_viewer_can_read_but_cannot_change_any_admin_resource(self):
        self.assertEqual(200, self.call('/admin/players', token=self.read_token)['status'])
        for route in ('freeze', 'migrate', 'adjust', 'preview', 'restore'):
            self.assertEqual(403, self.call('/admin/players/' + self.player + '/' + route, {}, self.read_token)['status'])
        self.assertFalse(self.app.store.head(self.player)['Frozen'])
        self.assertEqual([], self.app.store.inspect(self.player)['Audit'])

    def test_revocation_applies_without_restart(self):
        identities = json.loads(self.operators.read_text())
        self.operators.write_text(json.dumps(identities[:1]))
        self.assertEqual(401, self.call('/admin/players', token=self.read_token)['status'])
        self.assertEqual(200, self.call('/admin/players', token=self.write_token)['status'])

    def test_bearer_prefix_and_exact_routes_required(self):
        self.assertEqual(401, self.call('/admin/players', HTTP_AUTHORIZATION=self.write_token)['status'])
        self.assertEqual(404, self.call('/v1/players/' + self.player + '/head/extra', token=self.token)['status'])
        self.assertEqual(404, self.call('/admin/players/', token=self.write_token)['status'])
        self.assertEqual(405, self.call('/v1/accounts', REQUEST_METHOD='DELETE')['status'])

    def test_apple_lifecycle_remains_disconnected_from_production(self):
        self.assertIsNone(self.app.account_linking)
        self.assertIsNone(self.app.account_deletion)
        for action in ('preview', 'commit', 'status', 'cancel'):
            self.assertEqual(503, self.call('/v1/account-deletion/' + action,
                                           dict(PlayerId=self.player), self.token)['status'])
        self.assertEqual(503, self.call('/v1/apple/verify', dict(AuthorizationCode='synthetic'), self.token)['status'])
        self.assertEqual(503, self.call('/v1/apple/unlink/preview', dict(PlayerId=self.player), self.token)['status'])
        self.assertEqual(503, self.call('/v1/apple/unlink/commit', dict(PlayerId=self.player), self.token)['status'])
        for route in ('/v1/apple/unlink', '/v1/apple/delete', '/v1/apple/notifications'):
            self.assertEqual(404, self.call(route, {}, self.token)['status'])
        with self.app.store.connect() as db:
            tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        self.assertFalse(tables.intersection({'apple_grants', 'apple_links', 'apple_challenges',
                                             'apple_exchanges', 'apple_unlinks', 'apple_grant_generations',
                                             'deletion_confirmations', 'deletion_receipts'}))

    def test_body_limits_and_invalid_json_do_not_register(self):
        cases = [({'CONTENT_LENGTH': str(MAX_BODY + 1)}, 413), ({'CONTENT_TYPE': 'text/plain'}, 415),
                 ({'CONTENT_LENGTH': '-1'}, 400), ({'HTTP_TRANSFER_ENCODING': 'chunked'}, 400),
                 ({'wsgi.input': io.BytesIO(b'{"bad":NaN}'), 'CONTENT_LENGTH': '11'}, 400),
                 ({'wsgi.input': io.BytesIO(b'{}'), 'CONTENT_LENGTH': '10'}, 400)]
        for changes, status in cases:
            with self.subTest(changes=changes):
                self.assertEqual(status, self.call('/v1/accounts', {}, **changes)['status'])
        self.assertEqual(1, len(self.app.store.players()))

    def test_headers_and_privacy_preserving_log(self):
        with self.assertLogs('player_data', level='INFO') as logs:
            response = self.call('/v1/players/' + self.player + '/head', token=self.token)
        for sensitive in (self.token, self.player, '203.0.113.15'):
            self.assertNotIn(sensitive, '\n'.join(logs.output))
        self.assertIn('max-age', response['headers']['Strict-Transport-Security'])
        self.assertEqual('no-store', response['headers']['Cache-Control'])
        self.assertEqual(32, len(response['headers']['X-Request-ID']))

    def test_registration_is_limited_before_writing(self):
        for i in range(20):
            self.assertEqual(200, self.call('/v1/accounts', dict(PlayerId=self.player, Token=self.token))['status'])
        result = self.call('/v1/accounts', dict(PlayerId=uuid.uuid4().hex, Token=self.token))
        self.assertEqual(429, result['status'])
        self.assertIn('Retry-After', result['headers'])
        self.assertEqual(1, len(self.app.store.players()))

    def test_startup_refuses_missing_database_or_wrong_volume(self):
        with self.assertRaises(ValueError): create_app(dict(self.env, WITCH_INSTANCE_ID='other-volume'))
        with self.assertRaises(ValueError): create_app(dict(self.env, WITCH_PUBLIC_ORIGIN='http://api.example.com'))
        with self.assertRaises(ValueError): create_app({})
        self.directory.joinpath('players.sqlite').rename(self.directory / 'preserved.sqlite')
        with self.assertRaises(sqlite3.OperationalError): create_app(self.env)
        self.assertFalse(self.directory.joinpath('players.sqlite').exists())

    def test_startup_refuses_public_credentials_or_data(self):
        self.operators.chmod(0o644)
        with self.assertRaises(ValueError): create_app(self.env)
        self.operators.chmod(0o600); self.directory.chmod(0o755)
        with self.assertRaises(ValueError): create_app(self.env)

    def test_health_reports_database_failure_without_details(self):
        # An unavailable volume is surfaced to monitoring and never recreated by health.
        self.directory.rename(self.root / 'unmounted')
        result = self.call('/healthz')
        self.assertEqual(503, result['status'])
        self.assertNotIn(self.tmp.name, json.dumps(result))

    def test_head_health_is_read_only_and_checks_backup_marker(self):
        import time
        marker = self.root / 'health.json'
        self.app = create_app(dict(self.env, WITCH_BACKUP_HEALTH_FILE=str(marker)))
        for method in ('GET', 'HEAD'):
            self.assertEqual(503, self.call('/healthz', REQUEST_METHOD=method)['status'])
        marker.write_text(json.dumps({'Healthy': True, 'CheckedUnix': time.time()}))
        head = self.call('/healthz', REQUEST_METHOD='HEAD')
        self.assertEqual(200, head['status'])
        self.assertEqual(b'', head['body'])
        self.assertEqual(200, self.call('/healthz')['status'])
        marker.write_text(json.dumps({'Healthy': False, 'CheckedUnix': time.time()}))
        self.assertEqual(503, self.call('/healthz', REQUEST_METHOD='HEAD')['status'])
        self.assertEqual(200, self.call('/v1/players/' + self.player + '/head', token=self.token)['status'])
        self.assertEqual(405, self.call('/v1/accounts', REQUEST_METHOD='HEAD')['status'])
        self.assertEqual(1, len(self.app.store.players()))

    def test_runtime_database_loss_never_creates_an_empty_replacement(self):
        self.directory.joinpath('players.sqlite').rename(self.directory / 'preserved.sqlite')
        self.assertEqual(503, self.call('/healthz')['status'])
        self.assertFalse(self.directory.joinpath('players.sqlite').exists())


class BackupAndLimiterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.root = Path(self.tmp.name)
        initialize(self.root / 'live', 'test-instance-123')
        self.source = self.root / 'live' / 'players.sqlite'
        self.store = Store(self.source)
        self.player, self.token = uuid.uuid4().hex, 'a' * 64
        self.store.register(self.player, self.token)
    def tearDown(self): self.tmp.cleanup()

    def test_verified_backup_restores_wal_and_remains_offline(self):
        # Hold the connection open so committed data remains in the WAL.
        with self.store.connect() as db:
            db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple', 'transaction-123', self.player, 'paid_stones_120', '2026-09-21'))
            db.commit()
            target = backup_generation(self.source, self.root / 'backups')
            verify_generation(target)
            restore_copy(target, self.root / 'recovery')
        restored = Store(self.root / 'recovery' / 'players.sqlite')
        restored.authenticate(self.player, self.token)
        with restored.connect() as db:
            self.assertEqual('transaction-123', db.execute('SELECT transaction_id FROM purchases').fetchone()[0])
        self.assertFalse((self.root / 'recovery' / 'instance-id').exists())
        self.assertTrue((self.root / 'recovery' / 'RECOVERY-PENDING.txt').exists())
        self.assertEqual(0, target.stat().st_mode & 0o077)

    def test_cannot_overwrite_existing_database_or_backup(self):
        before = self.source.read_bytes()
        with self.assertRaises(ValueError): backup_database(self.source, self.source)
        self.assertEqual(before, self.source.read_bytes())
        target = backup_generation(self.source, self.root / 'backups')
        with self.assertRaises(ValueError): backup_database(self.source, target)
        with self.assertRaises(FileExistsError): restore_copy(target, self.root / 'live')
        self.store.authenticate(self.player, self.token)

    def test_corrupt_backup_or_manifest_cannot_restore(self):
        target = backup_generation(self.source, self.root / 'backups')
        target.write_bytes(b'corrupt')
        with self.assertRaises(ValueError): restore_copy(target, self.root / 'recovery')
        self.assertFalse((self.root / 'recovery').exists())

    def test_missing_source_never_creates_empty_db_or_backup(self):
        with self.assertRaises(sqlite3.OperationalError): backup_database(self.root / 'absent', self.root / 'new')
        self.assertFalse((self.root / 'absent').exists()); self.assertFalse((self.root / 'new').exists())

    def test_initialization_refuses_existing_data(self):
        with self.assertRaises(ValueError): initialize(self.root / 'live', 'new-instance-123')
        self.store.authenticate(self.player, self.token)

    def test_retention_keeps_daily_and_recent_copies_and_unrelated_files(self):
        directory = self.root / 'backups'; directory.mkdir(mode=0o700)
        import datetime as dt
        stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%d')
        for i in range(5):
            path = directory / f'players-{stamp}T01010{i}000000Z-aaaaaaaa.sqlite'
            path.write_text('fixture'); Path(str(path) + '.json').write_text('{}')
        unrelated = directory / 'manual.sqlite'; unrelated.write_text('keep')
        prune_generations(directory, recent=2)
        self.assertEqual(2, len(list(directory.glob('players-*.sqlite'))))
        self.assertTrue(unrelated.exists())

    def test_rate_limit_window_and_bounded_memory(self):
        time = [0]; limiter = RateLimiter(clock=lambda: time[0], max_keys=2)
        limiter.check('ip', 'one', 1, 60)
        with self.assertRaises(Fault): limiter.check('ip', 'one', 1, 60)
        limiter.check('ip', 'two', 1, 60)
        with self.assertRaises(Fault): limiter.check('ip', 'three', 1, 60)
        time[0] = 61; limiter.check('ip', 'three', 1, 60)
        self.assertLessEqual(len(limiter.windows), 2)

    def test_ipv6_rotation_within_subnet_shares_limit(self):
        one = client_network(dict(REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='2001:db8::1'), True)
        two = client_network(dict(REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='2001:db8::abcd'), True)
        self.assertEqual(one, two)

    def test_release_contains_only_reviewed_files_with_matching_hashes(self):
        import hashlib
        archive = self.root / 'release.tar.gz'
        build(archive)
        with tarfile.open(archive) as package:
            self.assertEqual(set(FILES) | {'SHA256.json'}, set(package.getnames()))
            manifest = json.load(package.extractfile('SHA256.json'))
            for name, digest in manifest.items():
                self.assertEqual(digest, hashlib.sha256(package.extractfile(name).read()).hexdigest())
        with self.assertRaises(FileExistsError): build(archive)

    def test_packaged_refund_worker_imports_in_isolated_mode_without_activation(self):
        import subprocess
        archive = self.root / 'worker-package.tar.gz'
        build(archive)
        target = self.root / 'unpacked'; target.mkdir()
        with tarfile.open(archive) as package:
            package.extractall(target, filter='data')
        for script, flag in [('run_refund_worker.py', '--run'), ('run_refund_migration.py', '--prepare')]:
            result = subprocess.run([sys.executable, '-I', '-B', str(target/script), '--help'],
                                    cwd=target, capture_output=True, text=True, timeout=20)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn(flag, result.stdout)
        self.assertFalse(list(target.rglob('*.sqlite')))
        self.assertFalse(list(target.rglob('status.json')))
        result = subprocess.run([sys.executable, '-I', '-B', '-c',
            'import sys;sys.path.insert(0,'+repr(str(target))+');import refund_sandbox'],
            cwd=target, capture_output=True, text=True, timeout=20)
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == '__main__': unittest.main()
