import concurrent.futures
import io
import json
import secrets
import sys
import tempfile
import unittest
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from account_linking import AccountLinking
from application import Application
from security import AdminAuth
from store import Store, Fault


class AccountLinkingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        catalog = json.loads((Path(__file__).resolve().parents[1] / 'catalog.json').read_text())
        self.store = Store(Path(self.tmp.name) / 'test.sqlite', catalog)
        self.player, self.token = uuid.uuid4().hex, secrets.token_hex(32)
        self.new_token = secrets.token_hex(32)
        self.store.register(self.player, self.token)
        self.save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                         SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900, PaidGachaStones=0,
                         OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
        self.store.snapshot(self.player, self.save)
        self.clock = 1000
        self.verifications = 0
        def verifier(token, nonce):
            self.verifications += 1
            subject, supplied_nonce = token.split(':')
            if supplied_nonce != nonce: raise Fault(401, 'Invalid nonce')
            return subject
        self.accounts = AccountLinking(self.store, verifier, lambda: self.clock)

    def tearDown(self): self.tmp.cleanup()

    def fault(self, status, call):
        with self.assertRaises(Fault) as error: call()
        self.assertEqual(status, error.exception.status)

    def link(self, subject='apple-one', player=None, token=None):
        token = token or self.token
        c = self.accounts.start('link', token, player or self.player)
        return self.accounts.verify(c['ChallengeId'], token, subject + ':' + c['Nonce'])

    def preview(self, subject='apple-one', token=None):
        token = token or self.new_token
        c = self.accounts.start('recover', token)
        r = self.accounts.verify(c['ChallengeId'], token, subject + ':' + c['Nonce'])
        return c, r

    def commit(self, c, r, token=None):
        return self.accounts.commit(c['ChallengeId'], token or self.new_token, r['PreviewToken'])

    def test_guest_registration_unchanged_and_link_not_required(self):
        op = self.store.operation(self.player, dict(RequestId=uuid.uuid4().hex, Epoch=0,
                                                  Kind='gacha', Count=1, Paid=False), self.token)
        self.assertEqual(600, op['Free'])
        with self.store.connect() as db:
            self.assertEqual(0, db.execute('SELECT count(*) FROM apple_links').fetchone()[0])

    def test_link_is_unique_no_merge_and_requires_checkpoint(self):
        self.assertEqual('linked', self.link()['Status'])
        self.assertEqual('linked', self.link()['Status'])
        self.fault(409, lambda: self.link('different-apple'))
        other, token = uuid.uuid4().hex, secrets.token_hex(32)
        self.store.register(other, token)
        self.fault(409, lambda: self.link('apple-one', other, token))
        self.fault(409, lambda: self.link('new-apple', other, token))
        self.store.snapshot(other, dict(self.save, PlayerId=other))
        self.assertEqual('linked', self.link('new-apple', other, token)['Status'])

    def test_nonce_credential_purpose_and_expiry_binding(self):
        c = self.accounts.start('link', self.token, self.player)
        self.fault(401, lambda: self.accounts.verify(c['ChallengeId'], self.new_token, 'apple-one:' + c['Nonce']))
        self.fault(401, lambda: self.accounts.verify(c['ChallengeId'], self.token, 'apple-one:other-nonce'))
        self.fault(409, lambda: self.accounts.commit(c['ChallengeId'], self.token, 'x'))
        self.clock += 301
        self.fault(401, lambda: self.accounts.verify(c['ChallengeId'], self.token, 'apple-one:' + c['Nonce']))
        self.fault(400, lambda: self.accounts.start('recover', self.new_token, self.player))
        self.fault(400, lambda: self.accounts.start('recover', 'not-random-' * 6))
        self.fault(400, lambda: self.accounts.start('delete', self.new_token))

    def test_no_link_does_not_create_a_new_player(self):
        self.fault(404, self.preview)
        self.assertEqual(1, len(self.store.players()))

    def test_preview_is_read_only_and_commit_rotates_without_granting(self):
        self.link()
        c, r = self.preview()
        self.assertEqual('preview', r['Status'])
        self.store.authenticate(self.player, self.token)
        self.assertEqual(0, self.store.head(self.player)['Epoch'])
        self.fault(409, lambda: self.accounts.commit(c['ChallengeId'], self.new_token, 'wrong'))
        result = self.commit(c, r)
        self.assertEqual('committed', result['Status'])
        self.store.authenticate(self.player, self.new_token)
        self.fault(401, lambda: self.store.authenticate(self.player, self.token))
        self.assertEqual(1, self.store.head(self.player)['Epoch'])
        self.assertEqual(900, self.store.head(self.player)['Free'])
        self.assertEqual([], self.store.head(self.player)['Operations'])
        # Financial records and the old snapshot remain intact.
        self.assertEqual(2, len(self.store.inspect(self.player)['Snapshots']))
        self.assertEqual(1, result['Snapshot']['RecoveryEpoch'])

    def test_retry_and_concurrent_commit_are_exactly_once(self):
        self.link(); c, r = self.preview()
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            results = list(pool.map(lambda _: self.commit(c, r), range(10)))
        self.assertTrue(all(result == results[0] for result in results))
        self.assertEqual(1, self.store.head(self.player)['Epoch'])
        self.assertEqual(1, sum(a['action'] == 'apple_transfer' for a in self.store.inspect(self.player)['Audit']))
        self.clock += 400
        self.assertEqual(results[0], self.commit(c, r))
        self.assertEqual(results[0], self.accounts.verify(c['ChallengeId'], self.new_token, None))
        self.assertEqual(2, self.verifications)

    def test_recovery_includes_committed_but_not_uploaded_purchase_and_summon(self):
        self.link()
        self.store.purchase_verifier = lambda req: dict(verified=True, store='apple', transaction='paid-tx', product=req['Target'])
        purchase = self.store.operation(self.player, dict(RequestId='purchase-one', Epoch=0, Kind='purchase',
            Target='com.nasus.dungeonmonsterroguelike.crystals120', TransactionId='paid-tx', Receipt='test'), self.token)
        summon = self.store.operation(self.player, dict(RequestId='summon-one', Epoch=0, Kind='gacha', Count=1, Paid=False), self.token)
        c, r = self.preview()
        self.assertEqual([purchase, summon], r['Operations'])
        self.assertEqual(0, r['Snapshot']['EconomyRevision'])
        self.assertEqual(2, r['EconomyRevision'])
        self.assertEqual((600, 120), (r['Free'], r['Paid']))
        self.commit(c, r)
        self.assertEqual([purchase, summon], self.store.head(self.player)['Operations'])
        self.fault(409, lambda: self.store.operation(self.player, dict(RequestId='duplicate-purchase', Epoch=1,
            Kind='purchase', Target=purchase['Target'], TransactionId='paid-tx', Receipt='test'), self.new_token))

    def test_changed_save_or_economy_or_freeze_requires_new_preview(self):
        for change in ('save', 'operation', 'freeze'):
            with self.subTest(change=change):
                self.link(); c, r = self.preview()
                if change == 'save':
                    self.save['SaveRevision'] += 1
                    self.store.snapshot(self.player, self.save)
                elif change == 'operation':
                    self.store.operation(self.player, dict(RequestId='changed-op', Epoch=0, Kind='gacha', Count=1, Paid=False))
                else:
                    self.store.freeze(self.player, True, 'operator', 'test')
                self.fault(423 if change == 'freeze' else 409, lambda: self.commit(c, r))
                self.store.authenticate(self.player, self.token)

    def test_old_credential_rechecked_even_if_caller_knows_new_epoch(self):
        self.link(); c, r = self.preview(); self.commit(c, r)
        self.fault(401, lambda: self.store.head(self.player, token=self.token))
        self.fault(401, lambda: self.store.snapshot(self.player, dict(self.save, RecoveryEpoch=1), self.token))
        self.fault(401, lambda: self.store.operation(self.player,
            dict(RequestId='stale-request', Epoch=1, Kind='gacha', Count=1, Paid=False), self.token))

    def test_transfer_during_slow_purchase_verification_cannot_deliver_old_request(self):
        self.link(); c, r = self.preview()
        def verifier(req):
            self.commit(c, r)
            return dict(verified=True, store='apple', transaction='slow-tx', product=req['Target'])
        self.store.purchase_verifier = verifier
        self.fault(401, lambda: self.store.operation(self.player, dict(RequestId='slow-purchase', Epoch=0,
            Kind='purchase', Target='com.nasus.dungeonmonsterroguelike.crystals120', TransactionId='slow-tx', Receipt='test'), self.token))
        self.assertEqual(0, self.store.head(self.player)['Paid'])

    def test_older_transfer_retry_cannot_reactivate_revoked_device(self):
        self.link(); c1, r1 = self.preview(); self.commit(c1, r1)
        newer = secrets.token_hex(32)
        c2, r2 = self.preview(token=newer); self.commit(c2, r2, token=newer)
        self.fault(401, lambda: self.commit(c1, r1))
        self.store.authenticate(self.player, newer)
        self.assertEqual(2, self.store.head(self.player)['Epoch'])

    def test_both_transfer_previews_cannot_commit(self):
        self.link(); c1, r1 = self.preview()
        other = secrets.token_hex(32); c2, r2 = self.preview(token=other)
        self.commit(c1, r1)
        self.fault(409, lambda: self.commit(c2, r2, token=other))

    def test_frozen_migration_missing_tail_and_wallet_mismatch_fail_closed(self):
        self.link()
        self.store.snapshot(self.player, dict(self.save, SaveRevision=2, FreeGachaStones=9999))
        self.fault(409, self.preview)
        self.store.snapshot(self.player, dict(self.save, SaveRevision=3))
        with self.store.connect() as db:
            db.execute('UPDATE players SET economy_revision=2 WHERE id=?', (self.player,))
        self.fault(409, self.preview)
        with self.store.connect() as db:
            db.execute('UPDATE players SET migration_required=1 WHERE id=?', (self.player,))
        self.fault(423, self.preview)

    def test_sensitive_identity_data_not_stored(self):
        self.link('private-apple-subject'); self.preview('private-apple-subject')
        with self.store.connect() as db:
            rows = '\n'.join(db.iterdump())
        for value in ('private-apple-subject', self.token, self.new_token):
            self.assertNotIn(value, rows)

    def test_expired_committed_challenge_cannot_be_replayed(self):
        self.link(); c, r = self.preview(); self.commit(c, r)
        self.clock += self.accounts.RETRY_TTL + 1
        self.fault(401, lambda: self.commit(c, r))
        # Expiring the response cache does not erase or revoke the actual account.
        self.store.authenticate(self.player, self.new_token)

    def test_inflight_link_challenge_is_invalid_after_transfer(self):
        self.link()
        pending = self.accounts.start('link', self.token, self.player)
        c, r = self.preview(); self.commit(c, r)
        self.fault(401, lambda: self.accounts.verify(pending['ChallengeId'], self.token,
                                                   'apple-one:' + pending['Nonce']))

    def test_http_disabled_by_default_and_no_secrets_logged(self):
        app = Application(self.store, AdminAuth(token='admin'), public_origin='https://api.example.com')
        def call(path, body, token=self.token):
            raw = json.dumps(body).encode(); result = {}
            env = dict(REQUEST_METHOD='POST', PATH_INFO=path, HTTP_HOST='api.example.com',
                       REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='203.0.113.1', HTTP_AUTHORIZATION='Bearer ' + token,
                       CONTENT_TYPE='application/json', CONTENT_LENGTH=str(len(raw)))
            env.update({'wsgi.input': io.BytesIO(raw), 'wsgi.url_scheme': 'https'})
            data = b''.join(app(env, lambda status, headers: result.update(status=int(status[:3]))))
            return result['status'], json.loads(data)
        self.assertEqual(503, call('/v1/apple/challenge', dict(Mode='link', PlayerId=self.player))[0])
        app.account_linking = self.accounts
        with self.assertLogs('player_data', level='INFO') as logs:
            status, c = call('/v1/apple/challenge', dict(Mode='link', PlayerId=self.player))
            self.assertEqual(200, status)
            status, linked = call('/v1/apple/verify', dict(ChallengeId=c['ChallengeId'], IdentityToken='private-sub:' + c['Nonce']))
            self.assertEqual(200, status)
            self.assertEqual('linked', linked['Status'])
        for value in ('private-sub', self.token, c['Nonce'], c['ChallengeId']):
            self.assertNotIn(value, '\n'.join(logs.output))


if __name__ == '__main__': unittest.main()
