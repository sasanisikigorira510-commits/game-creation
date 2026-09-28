"""Real encrypted vault + synthetic Apple exchange; no external accounts/network."""
import concurrent.futures
import io
import json
from pathlib import Path
import secrets
import sys
import tempfile
import threading
from types import SimpleNamespace
import unittest
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from account_linking import AccountLinking
from application import Application
from security import AdminAuth
from store import Store, Fault
try:
    from apple_grants import AppleGrantVault
    from apple_tokens import AppleGrant, AppleCodeRejected
except ImportError:
    AppleGrantVault = None


@unittest.skipIf(AppleGrantVault is None, 'Install requirements-identity.txt')
class AccountGrantTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.store = Store(Path(self.tmp.name) / 'test.sqlite')
        self.clock = 1000
        self.player, self.token = uuid.uuid4().hex, secrets.token_hex(32)
        self.new_token = secrets.token_hex(32)
        self.store.register(self.player, self.token)
        self.save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                         SchemaVersion=3, PlayerLevel=1, Gold=10, FreeGachaStones=900, PaidGachaStones=0,
                         OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
        self.store.snapshot(self.player, self.save)
        def verify(identity, nonce):
            if identity != 'private-identity:' + nonce:
                raise Fault(401, 'Synthetic identity mismatch')
            return 'synthetic-apple-user'
        verify.audience = 'com.test.game'
        self.verify = verify
        self.calls = 0
        self.hook = None
        def exchange(code, identity, nonce):
            self.calls += 1
            self.assertEqual('private-code', code)
            subject = verify(identity, nonce)
            if self.hook: self.hook()
            return AppleGrant(subject, 'private-refresh-' + str(self.calls))
        self.tokens = SimpleNamespace(client_id='com.test.game', exchange=exchange)
        self.vault = AppleGrantVault(self.store, secrets.token_bytes(32), 'com.test.game', clock=lambda: self.clock)
        self.accounts = self.reopen()
        self.subject = self.vault.subject_hash('synthetic-apple-user')

    def reopen(self):
        return AccountLinking(self.store, self.verify, lambda: self.clock, tokens=self.tokens, vault=self.vault)

    def start(self, mode='link', token=None, player=None):
        return self.accounts.start(mode, token or (self.token if mode == 'link' else self.new_token),
                                   (player or self.player) if mode == 'link' else None)

    def finish(self, c, token=None):
        return self.accounts.verify(c['ChallengeId'], token or self.token, 'private-identity:' + c['Nonce'], 'private-code')

    def rows(self, table):
        self.assertIn(table, ('apple_grants', 'apple_exchanges', 'apple_links'))
        with self.store.connect() as db:
            return [dict(row) for row in db.execute('SELECT * FROM ' + table)]

    def fault(self, status, call):
        with self.assertRaises(Fault) as error: call()
        self.assertEqual(status, error.exception.status)

    def queue(self):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            return self.vault.queue(db, self.subject)

    def test_link_stores_encrypted_token_and_result_retry_does_not_exchange_again(self):
        c = self.start()
        result = self.finish(c)
        self.assertEqual('linked', result['Status'])
        self.assertEqual(result, self.accounts.verify(c['ChallengeId'], self.token, None))
        self.assertEqual(1, self.calls)
        self.assertEqual('attached', self.rows('apple_exchanges')[0]['state'])
        self.assertEqual(1, len(self.rows('apple_grants')))
        for path in Path(self.tmp.name).iterdir():
            for secret in (b'private-identity', b'private-code', b'private-refresh', b'synthetic-apple-user'):
                self.assertNotIn(secret, path.read_bytes())
        self.assertNotIn('private', json.dumps(result))

    def test_missing_or_malformed_code_never_reserves_or_exchanges(self):
        c = self.start()
        for code in (None, '', 'bad code', 'bad\ncode', '全角', 'a' * 16385):
            self.fault(400, lambda: self.accounts.verify(c['ChallengeId'], self.token, 'private-identity:' + c['Nonce'], code))
        self.assertEqual(0, self.calls)
        self.assertEqual([], self.rows('apple_exchanges'))

    def test_identity_and_installation_binding_precede_code_consumption(self):
        c = self.start()
        self.fault(401, lambda: self.finish(c, self.new_token))
        self.fault(401, lambda: self.accounts.verify(c['ChallengeId'], self.token, 'wrong', 'private-code'))
        self.assertEqual(0, self.calls)

    def test_missing_checkpoint_and_account_conflict_do_not_consume_code(self):
        other, credential = uuid.uuid4().hex, secrets.token_hex(32)
        self.store.register(other, credential)
        c = self.start(token=credential, player=other)
        self.fault(409, lambda: self.finish(c, credential))
        self.assertEqual(0, self.calls)
        self.finish(self.start())
        self.store.snapshot(other, dict(self.save, PlayerId=other))
        self.fault(409, lambda: self.finish(c, credential))
        self.assertEqual(1, self.calls)

    def test_recovery_without_a_link_does_not_consume_code_or_create_player(self):
        c = self.start('recover')
        self.fault(404, lambda: self.finish(c, self.new_token))
        self.assertEqual(0, self.calls)
        self.assertEqual(1, len(self.store.players()))

    def test_recovery_requires_stored_grant_and_preserves_wallet(self):
        self.finish(self.start())
        c = self.start('recover')
        result = self.finish(c, self.new_token)
        self.assertEqual('preview', result['Status'])
        committed = self.accounts.commit(c['ChallengeId'], self.new_token, result['PreviewToken'])
        self.assertEqual(900, committed['Free'])
        self.assertEqual(0, committed['Paid'])
        self.assertEqual(2, len(self.rows('apple_grants')))
        self.assertEqual(committed, self.accounts.verify(c['ChallengeId'], self.new_token, None))
        self.assertEqual(2, self.calls)

    def test_grant_survives_failure_between_storage_and_linking(self):
        c = self.start()
        self.hook = lambda: self.store.freeze(self.player, True, 'test-operator', 'race')
        self.fault(423, lambda: self.finish(c))
        self.assertEqual('stored', self.rows('apple_exchanges')[0]['state'])
        self.assertEqual([], self.rows('apple_links'))
        self.store.freeze(self.player, False, 'test-operator', 'resolved')
        self.accounts = self.reopen()
        result = self.accounts.verify(c['ChallengeId'], self.token, None)
        self.assertEqual('linked', result['Status'])
        self.assertEqual(1, self.calls)

    def test_expired_challenge_does_not_lose_returned_refresh_token(self):
        c = self.start()
        def expire(): self.clock += 301
        self.hook = expire
        self.fault(401, lambda: self.finish(c))
        self.assertEqual(1, len(self.rows('apple_grants')))
        self.assertEqual('stored', self.rows('apple_exchanges')[0]['state'])
        self.assertEqual([], self.rows('apple_links'))

    def test_credential_rotation_during_exchange_cannot_link_old_session(self):
        c = self.start()
        def rotate():
            with self.store.connect() as db:
                db.execute('UPDATE players SET token_hash=? WHERE id=?',
                           (self.accounts.credential_hash(self.new_token), self.player))
        self.hook = rotate
        self.fault(401, lambda: self.finish(c))
        self.assertEqual([], self.rows('apple_links'))
        self.assertEqual(1, len(self.rows('apple_grants')))

    def test_unknown_provider_result_is_durable_and_not_resent(self):
        def timeout(): raise Fault(503, 'provider unknown')
        self.hook = timeout
        c = self.start()
        self.fault(503, lambda: self.finish(c))
        self.assertEqual('uncertain', self.rows('apple_exchanges')[0]['state'])
        self.accounts = self.reopen()
        self.fault(409, lambda: self.accounts.verify(c['ChallengeId'], self.token, None))
        self.fault(409, lambda: self.finish(self.start()))
        self.assertEqual(1, self.calls)
        self.assertEqual([], self.rows('apple_links'))

    def test_explicit_apple_rejection_allows_new_sign_in_but_not_same_code_retry(self):
        def rejected(): raise AppleCodeRejected()
        self.hook = rejected
        c = self.start()
        self.fault(401, lambda: self.finish(c))
        self.assertEqual('failed', self.rows('apple_exchanges')[0]['state'])
        self.fault(401, lambda: self.finish(c))
        self.assertEqual(1, self.calls)
        self.hook = None
        self.assertEqual('linked', self.finish(self.start())['Status'])
        self.assertEqual(2, self.calls)

    def test_storage_failure_does_not_report_link_success_or_resend_code(self):
        def failed(db, grant): raise OSError('simulated disk error')
        self.vault.save = failed
        c = self.start()
        with self.assertRaises(OSError): self.finish(c)
        self.assertEqual('uncertain', self.rows('apple_exchanges')[0]['state'])
        self.assertEqual([], self.rows('apple_links'))
        self.fault(409, lambda: self.finish(c))
        self.assertEqual(1, self.calls)

    def test_duplicate_and_parallel_challenges_cannot_exchange_concurrently(self):
        c, other = self.start(), self.start()
        entered, release = threading.Event(), threading.Event()
        def wait():
            entered.set()
            self.assertTrue(release.wait(5))
        self.hook = wait
        with concurrent.futures.ThreadPoolExecutor(2) as workers:
            first = workers.submit(self.finish, c)
            try:
                self.assertTrue(entered.wait(5))
                self.fault(409, lambda: self.finish(c))
                self.fault(409, lambda: self.finish(other))
            finally: release.set()
            self.assertEqual('linked', first.result(timeout=5)['Status'])
        self.assertEqual(1, self.calls)

    def test_pending_revocation_blocks_preview_retry_and_commit(self):
        self.finish(self.start())
        c = self.start('recover')
        result = self.finish(c, self.new_token)
        self.assertEqual(2, self.queue())
        self.fault(409, lambda: self.accounts.verify(c['ChallengeId'], self.new_token, None))
        self.fault(409, lambda: self.accounts.commit(c['ChallengeId'], self.new_token, result['PreviewToken']))
        self.fault(409, lambda: self.finish(self.start()))
        self.assertEqual(2, self.calls)
        self.store.authenticate(self.player, self.token)

    def test_late_exchange_is_queued_when_revocation_happens_in_flight(self):
        c = self.start()
        self.hook = lambda: self.assertEqual(0, self.queue())
        self.fault(409, lambda: self.finish(c))
        self.assertEqual('pending', self.rows('apple_grants')[0]['state'])
        self.assertEqual([], self.rows('apple_links'))
        self.hook = None
        self.fault(409, lambda: self.finish(self.start()))
        worker = SimpleNamespace(client_id=self.tokens.client_id, revoke=lambda token: None)
        self.assertEqual('revoked', self.vault.process_one(worker))
        self.assertEqual('linked', self.finish(self.start())['Status'])

    def test_missing_grant_cannot_be_used_to_commit_recovery(self):
        self.finish(self.start())
        c = self.start('recover')
        preview = self.finish(c, self.new_token)
        with self.store.connect() as db: db.execute('DELETE FROM apple_grants')
        self.fault(409, lambda: self.accounts.commit(c['ChallengeId'], self.new_token, preview['PreviewToken']))
        self.assertEqual(0, self.store.head(self.player)['Epoch'])

    def test_http_body_delivers_code_without_logging_secrets(self):
        app = Application(self.store, AdminAuth(token='test-admin'), public_origin='https://api.example.com', account_linking=self.accounts)
        c = self.start()
        raw = json.dumps(dict(ChallengeId=c['ChallengeId'], IdentityToken='private-identity:' + c['Nonce'],
                              AuthorizationCode='private-code')).encode()
        env = dict(REQUEST_METHOD='POST', PATH_INFO='/v1/apple/verify', CONTENT_TYPE='application/json',
                   CONTENT_LENGTH=str(len(raw)), HTTP_AUTHORIZATION='Bearer ' + self.token, REMOTE_ADDR='127.0.0.1',
                   HTTP_HOST='api.example.com', HTTP_X_REAL_IP='203.0.113.1')
        env['wsgi.url_scheme'] = 'https'
        env['wsgi.input'] = io.BytesIO(raw)
        statuses = []
        with self.assertLogs('player_data', level='INFO') as logs:
            result = b''.join(app(env, lambda status, headers: statuses.append(status)))
        self.assertTrue(statuses[0].startswith('200'))
        self.assertEqual('linked', json.loads(result)['Status'])
        for secret in ('private-code', 'private-identity', 'private-refresh', self.token):
            self.assertNotIn(secret, '\n'.join(logs.output))

    def test_incomplete_or_mismatched_dependencies_are_refused(self):
        with self.assertRaises(ValueError): AccountLinking(self.store, self.verify, tokens=self.tokens)
        with self.assertRaises(ValueError): AccountLinking(self.store, self.verify, vault=self.vault)
        self.tokens.client_id = 'other.app'
        with self.assertRaises(ValueError): self.reopen()

    def test_unlink_requires_explicit_bound_confirmation_and_preview_is_read_only(self):
        self.finish(self.start())
        before = self.store.head(self.player)
        preview = self.accounts.unlink_preview(self.player, self.token)
        self.assertEqual('confirm_unlink', preview['Status'])
        self.assertEqual(1, len(self.rows('apple_links')))
        self.assertEqual('active', self.rows('apple_grants')[0]['state'])
        self.fault(409, lambda: self.accounts.unlink_commit(self.player, self.token, 'wrong'))
        self.fault(401, lambda: self.accounts.unlink_commit(self.player, self.new_token, preview['ConfirmationToken']))
        self.clock += 301
        self.fault(409, lambda: self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken']))
        self.assertEqual(before, self.store.head(self.player))
        self.assertEqual(1, len(self.rows('apple_links')))

    def test_unlink_preserves_game_and_money_invalidates_previews_and_is_idempotent(self):
        self.finish(self.start())
        c = self.start('recover')
        recovery = self.finish(c, self.new_token)
        before = self.store.head(self.player)
        snapshots = self.store.inspect(self.player)['Snapshots']
        preview = self.accounts.unlink_preview(self.player, self.token)
        result = self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken'])
        self.assertEqual('unlinked', result['Status'])
        self.assertTrue(result['RevocationPending'])
        self.assertFalse(result['ManualRevocationRequired'])
        self.assertEqual(result, self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken']))
        self.assertEqual(before, self.store.head(self.player))
        self.assertEqual(snapshots, self.store.inspect(self.player)['Snapshots'])
        self.store.authenticate(self.player, self.token)
        self.assertEqual([], self.rows('apple_links'))
        self.assertTrue(all(r['state'] == 'pending' for r in self.rows('apple_grants')))
        self.fault(401, lambda: self.accounts.commit(c['ChallengeId'], self.new_token, recovery['PreviewToken']))
        self.fault(404, lambda: self.finish(self.start('recover'), self.new_token))
        audits = self.store.inspect(self.player)['Audit']
        self.assertEqual(1, sum(row['action'] == 'apple_unlink' for row in audits))
        self.assertEqual('not_linked', self.accounts.unlink_preview(self.player, self.token)['Status'])

    def test_unlink_confirmation_cannot_remove_a_later_relink(self):
        self.finish(self.start())
        preview = self.accounts.unlink_preview(self.player, self.token)
        self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken'])
        worker = SimpleNamespace(client_id=self.tokens.client_id, revoke=lambda token: None)
        self.assertEqual('revoked', self.vault.process_one(worker))
        self.finish(self.start())
        self.fault(409, lambda: self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken']))
        self.assertEqual(1, len(self.rows('apple_links')))

    def test_parallel_unlink_confirms_once_and_does_not_enqueue_twice(self):
        self.finish(self.start())
        preview = self.accounts.unlink_preview(self.player, self.token)
        with concurrent.futures.ThreadPoolExecutor(5) as workers:
            results = list(workers.map(lambda _: self.accounts.unlink_commit(
                self.player, self.token, preview['ConfirmationToken']), range(10)))
        self.assertTrue(all(result == results[0] for result in results))
        with self.store.connect() as db: self.assertEqual(1, self.vault.generation(db, self.subject))
        self.assertEqual(1, len(self.rows('apple_grants')))
        self.assertEqual(1, sum(a['action'] == 'apple_unlink' for a in self.store.inspect(self.player)['Audit']))

    def test_unlink_without_a_retained_token_does_not_claim_apple_revocation_success(self):
        self.finish(self.start())
        with self.store.connect() as db: db.execute('DELETE FROM apple_grants')
        preview = self.accounts.unlink_preview(self.player, self.token)
        result = self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken'])
        self.assertTrue(result['ManualRevocationRequired'])
        self.assertFalse(result['RevocationPending'])
        self.assertEqual([], self.rows('apple_links'))
        self.store.authenticate(self.player, self.token)

    def test_unlink_and_revocation_queue_are_atomic(self):
        self.finish(self.start())
        preview = self.accounts.unlink_preview(self.player, self.token)
        queue = self.vault.queue
        def fail(db, subject):
            queue(db, subject)
            raise OSError('simulated storage failure')
        self.vault.queue = fail
        with self.assertRaises(OSError): self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken'])
        self.assertEqual(1, len(self.rows('apple_links')))
        self.assertEqual('active', self.rows('apple_grants')[0]['state'])
        with self.store.connect() as db: self.assertEqual(0, self.vault.generation(db, self.subject))

    def test_unlink_during_recovery_exchange_fences_late_response_and_preserves_grant(self):
        self.finish(self.start())
        c = self.start('recover')
        entered, release = threading.Event(), threading.Event()
        def wait():
            entered.set()
            self.assertTrue(release.wait(5))
        self.hook = wait
        with concurrent.futures.ThreadPoolExecutor(2) as workers:
            first = workers.submit(self.finish, c, self.new_token)
            try:
                self.assertTrue(entered.wait(5))
                preview = self.accounts.unlink_preview(self.player, self.token)
                result = self.accounts.unlink_commit(self.player, self.token, preview['ConfirmationToken'])
                self.assertTrue(result['RevocationPending'])
            finally: release.set()
            self.fault(401, lambda: first.result(timeout=5))
        self.assertEqual([], self.rows('apple_links'))
        self.assertEqual(2, len(self.rows('apple_grants')))
        self.assertTrue(all(row['state'] == 'pending' for row in self.rows('apple_grants')))
        self.assertEqual(0, self.store.head(self.player)['Epoch'])
        self.store.authenticate(self.player, self.token)

    def test_signature_only_mode_cannot_unlink_without_revocation_storage(self):
        legacy = AccountLinking(self.store, self.verify)
        self.fault(503, lambda: legacy.unlink_preview(self.player, self.token))
        self.fault(503, lambda: legacy.unlink_commit(self.player, self.token, 'x' * 64))


if __name__ == '__main__': unittest.main()
