"""Deletion tests only use disposable databases and synthetic provider responses."""
import concurrent.futures
import io
import json
from pathlib import Path
import secrets
import sqlite3
import sys
import tempfile
import unittest
import uuid
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from account_deletion import AccountDeletion
from application import Application
from security import AdminAuth
from store import Store, Fault
import test_account_grants as grant_fixture


class DeletionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.store = Store(Path(self.tmp.name) / 'db.sqlite')
        self.player, self.token = uuid.uuid4().hex, secrets.token_hex(32)
        self.store.register(self.player, self.token)
        self.save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                         SchemaVersion=3, PlayerLevel=1, Gold=10, FreeGachaStones=900, PaidGachaStones=0,
                         OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
        self.store.snapshot(self.player, self.save)
        self.clock = 1000
        self.deletion = AccountDeletion(self.store, clock=lambda: self.clock)

    def preview(self):
        return self.deletion.preview(self.player, self.token)['ConfirmationToken']

    def commit(self, confirmation):
        return self.deletion.commit(self.player, self.token, confirmation)

    def fault(self, status, fn):
        with self.assertRaises(Fault) as caught:
            fn()
        self.assertEqual(status, caught.exception.status)

    def purchase(self, transaction='synthetic-transaction'):
        return dict(Kind='purchase', Epoch=0, RequestId=uuid.uuid4().hex,
                    Target='com.nasus.dungeonmonsterroguelike.crystals120', TransactionId=transaction, Receipt='synthetic')

    @staticmethod
    def verify(request):
        return dict(verified=True, store='apple', transaction=request['TransactionId'], product=request['Target'])

    def call(self, action, body, token=None):
        raw = json.dumps(body).encode()
        env = dict(REQUEST_METHOD='POST', PATH_INFO='/v1/account-deletion/' + action,
                   HTTP_HOST='api.example.com', REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='203.0.113.1',
                   HTTP_AUTHORIZATION='Bearer ' + (self.token if token is None else token),
                   CONTENT_TYPE='application/json', CONTENT_LENGTH=str(len(raw)))
        env.update({'wsgi.input': io.BytesIO(raw), 'wsgi.url_scheme': 'https'})
        response = {}
        def start(status, headers):
            response.update(status=int(status[:3]), headers=dict(headers))
        response['body'] = json.loads(b''.join(self.app(env, start)))
        return response

    def test_preview_and_status_never_delete_or_modify_progress(self):
        before = self.store.inspect(self.player)
        confirm = self.preview()
        self.assertEqual('not_deleted', self.deletion.status(self.player, self.token, confirm)['Status'])
        self.assertEqual(before, self.store.inspect(self.player))

    def test_guest_without_checkpoint_can_delete(self):
        other, token = uuid.uuid4().hex, secrets.token_hex(32)
        self.store.register(other, token)
        preview = self.deletion.preview(other, token)
        self.assertEqual('deleted', self.deletion.commit(other, token, preview['ConfirmationToken'])['Status'])

    def test_removes_all_live_player_records_and_preserves_other_player(self):
        self.store.purchase_verifier = self.verify
        self.store.operation(self.player, self.purchase(), token=self.token)
        with self.store.connect() as db:
            db.execute('INSERT INTO claims VALUES(?,?)', (self.player, 'claim'))
            db.execute('INSERT INTO flags(player,code,received,detail) VALUES(?,?,?,?)', (self.player, 'test', 'now', 'private'))
        other = uuid.uuid4().hex
        self.store.register(other, secrets.token_hex(32))
        before = self.store.inspect(other)
        confirmation = self.preview()
        self.commit(confirmation)
        with self.store.connect() as db:
            for table in ('snapshots', 'operations', 'claims', 'flags', 'audit', 'purchases'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table + ' WHERE player=?', (self.player,)).fetchone()[0])
            self.assertEqual(0, db.execute('SELECT count(*) FROM deletion_confirmations').fetchone()[0])
            # Logical records only: historical SQLite pages/WAL/backups need a
            # separate retention policy. Do not assert physical media erasure.
            for table in ('deleted_players', 'retired_purchases', 'deletion_receipts'):
                contents = repr([tuple(r) for r in db.execute('SELECT * FROM ' + table)])
                for secret in (self.player, self.token, confirmation, 'synthetic-transaction'):
                    self.assertNotIn(secret, contents)
        self.assertEqual(before, self.store.inspect(other))

    def test_deleted_identity_cannot_register_with_old_or_new_secret_after_restart(self):
        self.commit(self.preview())
        reopened = Store(self.store.path)
        for token in (self.token, secrets.token_hex(32)):
            self.fault(410, lambda: reopened.register(self.player, token))
        self.fault(401, lambda: reopened.head(self.player, token=self.token))
        self.fault(401, lambda: reopened.snapshot(self.player, self.save, token=self.token))
        self.fault(404, lambda: reopened.head(self.player))

    def test_wrong_credentials_cannot_preview_delete_or_read_receipt(self):
        other = secrets.token_hex(32)
        confirm = self.preview()
        self.fault(401, lambda: self.deletion.preview(self.player, other))
        self.fault(401, lambda: self.deletion.commit(self.player, other, confirm))
        self.commit(confirm)
        self.fault(401, lambda: self.deletion.status(self.player, other, confirm))

    def test_missing_bad_or_unissued_confirmation_does_not_delete(self):
        for confirmation in (None, '', 'x' * 64, 'a' * 64, [], 10):
            self.fault(409, lambda: self.commit(confirmation))
        self.assertEqual(900, self.store.head(self.player)['Free'])

    def test_confirmation_cannot_be_used_for_another_player(self):
        confirm = self.preview()
        other = uuid.uuid4().hex
        self.store.register(other, self.token)
        self.fault(409, lambda: self.deletion.commit(other, self.token, confirm))
        self.assertEqual(2, len(self.store.players()))

    def test_expired_confirmation_requires_new_review(self):
        confirm = self.preview()
        self.clock += 300
        self.fault(409, lambda: self.commit(confirm))
        self.commit(self.preview())

    def test_new_snapshot_invalidates_confirmation(self):
        confirm = self.preview()
        self.store.snapshot(self.player, dict(self.save, SaveRevision=2, Gold=99), token=self.token)
        self.fault(409, lambda: self.commit(confirm))
        self.commit(self.preview())

    def test_wallet_change_invalidates_confirmation(self):
        confirm = self.preview()
        self.store.purchase_verifier = self.verify
        self.store.operation(self.player, self.purchase(), token=self.token)
        self.fault(409, lambda: self.commit(confirm))
        self.assertEqual(120, self.store.head(self.player)['Paid'])

    def test_rotated_credential_cannot_delete_using_old_confirmation(self):
        import hashlib
        confirm = self.preview()
        new = secrets.token_hex(32)
        with self.store.connect() as db:
            db.execute('UPDATE players SET token_hash=?,epoch=1 WHERE id=?', (hashlib.sha256(new.encode()).hexdigest(), self.player))
        self.fault(401, lambda: self.commit(confirm))
        self.fault(409, lambda: self.deletion.commit(self.player, new, confirm))

    def test_frozen_and_migration_pending_account_can_request_deletion(self):
        with self.store.connect() as db:
            db.execute('UPDATE players SET frozen=1,migration_required=1 WHERE id=?', (self.player,))
        self.assertEqual('deleted', self.commit(self.preview())['Status'])

    def test_lost_response_retry_and_status_survive_service_restart(self):
        confirm = self.preview()
        first = self.commit(confirm)
        self.deletion = AccountDeletion(Store(self.store.path), clock=lambda: self.clock)
        self.assertEqual(first, self.commit(confirm))
        self.assertEqual(first, self.deletion.status(self.player, self.token, confirm))
        self.fault(401, lambda: self.deletion.status(self.player, self.token, 'b' * 64))

    def test_expired_receipt_never_recreates_account(self):
        confirm = self.preview()
        self.commit(confirm)
        self.clock += 86400
        self.fault(401, lambda: self.commit(confirm))
        self.fault(401, lambda: self.deletion.status(self.player, self.token, confirm))
        self.fault(410, lambda: self.store.register(self.player, self.token))

    def test_concurrent_commits_are_idempotent(self):
        confirm = self.preview()
        with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
            results = list(pool.map(lambda _: self.commit(confirm), range(10)))
        self.assertTrue(all(result == results[0] for result in results))
        with self.store.connect() as db:
            self.assertEqual(1, db.execute('SELECT count(*) FROM deleted_players').fetchone()[0])
            self.assertEqual(1, db.execute('SELECT count(*) FROM deletion_receipts').fetchone()[0])

    def test_failure_rolls_back_data_and_tombstones(self):
        confirm = self.preview()
        before = self.store.inspect(self.player)
        with self.store.connect() as db:
            db.execute("CREATE TRIGGER simulate_disk_error BEFORE INSERT ON deletion_receipts BEGIN SELECT RAISE(ABORT,'test'); END")
        with self.assertRaises(sqlite3.IntegrityError):
            self.commit(confirm)
        self.assertEqual(before, self.store.inspect(self.player))
        with self.store.connect() as db:
            self.assertEqual(0, db.execute('SELECT count(*) FROM deleted_players').fetchone()[0])
            db.execute('DROP TRIGGER simulate_disk_error')
        self.commit(confirm)

    def test_delivered_receipt_cannot_be_replayed_by_new_player(self):
        self.store.purchase_verifier = self.verify
        request = self.purchase()
        self.store.operation(self.player, request, token=self.token)
        self.commit(self.preview())
        other, token = uuid.uuid4().hex, secrets.token_hex(32)
        self.store.register(other, token)
        self.store.snapshot(other, dict(self.save, PlayerId=other), token=token)
        request['RequestId'] = uuid.uuid4().hex
        self.fault(409, lambda: self.store.operation(other, request, token=token))
        self.assertEqual(0, self.store.head(other)['Paid'])
        self.assertEqual(120, self.store.operation(other, self.purchase('genuinely-new'), token=token)['Paid'])

    def test_purchase_provider_response_after_deletion_cannot_revive_data(self):
        confirm = self.preview()
        def verify(request):
            self.commit(confirm)
            return self.verify(request)
        self.store.purchase_verifier = verify
        self.fault(401, lambda: self.store.operation(self.player, self.purchase(), token=self.token))
        self.assertEqual([], self.store.players())

    def test_preview_has_no_game_contents_or_credentials(self):
        result = self.deletion.preview(self.player, self.token)
        self.assertEqual('confirm_delete', result['Status'])
        self.assertEqual(900, result['Free'])
        self.assertNotIn(self.token, json.dumps(result))
        self.assertNotIn('OwnedMonsters', result)

    def test_http_confirm_status_and_logs_do_not_expose_credentials(self):
        self.app = Application(self.store, AdminAuth(token='admin'), public_origin='https://api.example.com',
                               account_deletion=self.deletion)
        preview = self.call('preview', dict(PlayerId=self.player))
        confirm = preview['body']['ConfirmationToken']
        body = dict(PlayerId=self.player, ConfirmationToken=confirm)
        with self.assertLogs('player_data', level='INFO') as logs:
            deleted = self.call('commit', body)
            retry = self.call('status', body)
        self.assertEqual(200, deleted['status'])
        self.assertEqual(deleted['body'], retry['body'])
        self.assertEqual('no-store', retry['headers']['Cache-Control'])
        for sensitive in (self.player, self.token, confirm):
            self.assertNotIn(sensitive, '\n'.join(logs.output))

    def test_routes_disabled_unless_explicitly_injected_and_suffixes_rejected(self):
        self.app = Application(self.store, AdminAuth(token='admin'))
        for action in ('preview', 'commit', 'status', 'cancel'):
            self.assertEqual(503, self.call(action, dict(PlayerId=self.player))['status'])
        self.assertEqual(404, self.call('commit/extra', {})['status'])
        self.assertEqual(1, len(self.store.players()))

    def test_cancel_fences_delayed_commit_and_is_idempotent(self):
        confirmation = self.preview()
        result = self.deletion.cancel(self.player, self.token, confirmation)
        self.assertEqual('cancelled', result['Status'])
        self.assertEqual(result, self.deletion.cancel(self.player, self.token, confirmation))
        self.fault(409, lambda: self.commit(confirmation))
        self.assertEqual(900, self.store.head(self.player)['Free'])

    def test_cancel_after_deletion_returns_deleted_instead_of_restoring(self):
        confirmation = self.preview()
        result = self.commit(confirmation)
        self.assertEqual(result, self.deletion.cancel(self.player, self.token, confirmation))
        self.assertEqual([], self.store.players())

    def test_cancel_and_commit_race_serializes_to_one_outcome(self):
        confirmation = self.preview()
        def commit():
            try: return self.commit(confirmation)['Status']
            except Fault as error: return error.status
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            a = pool.submit(commit)
            b = pool.submit(self.deletion.cancel, self.player, self.token, confirmation)
            result, cancelled = a.result(), b.result()['Status']
        self.assertIn((result, cancelled), [('deleted', 'deleted'), (409, 'cancelled')])

    def test_cancel_http_requires_authentication_and_preserves_account(self):
        self.app = Application(self.store, AdminAuth(token='admin'), account_deletion=self.deletion)
        body = dict(PlayerId=self.player, ConfirmationToken=self.preview())
        self.assertEqual(401, self.call('cancel', body, token='x' * 64)['status'])
        self.assertEqual('cancelled', self.call('cancel', body)['body']['Status'])


@unittest.skipIf(grant_fixture.AppleGrantVault is None, 'Install requirements-identity.txt')
class AppleDeletionTests(unittest.TestCase):
    setUp = grant_fixture.AccountGrantTests.setUp
    reopen = grant_fixture.AccountGrantTests.reopen
    start = grant_fixture.AccountGrantTests.start
    finish = grant_fixture.AccountGrantTests.finish
    rows = grant_fixture.AccountGrantTests.rows
    fault = grant_fixture.AccountGrantTests.fault

    def prepare_deletion(self):
        self.deletion = AccountDeletion(self.store, account_linking=self.accounts, clock=lambda: self.clock)
        self.confirmation = self.deletion.preview(self.player, self.token)['ConfirmationToken']

    def delete(self):
        return self.deletion.commit(self.player, self.token, self.confirmation)

    def test_deletes_link_and_cached_recovery_save_and_queues_revoke(self):
        self.finish(self.start())
        c = self.start('recover')
        preview = self.finish(c, self.new_token)
        self.prepare_deletion()
        result = self.delete()
        self.assertTrue(result['RevocationPending'])
        self.assertFalse(result['ManualRevocationRequired'])
        self.assertEqual([], self.rows('apple_links'))
        self.assertTrue(all(r['state'] == 'pending' for r in self.rows('apple_grants')))
        with self.store.connect() as db:
            self.assertEqual(0, db.execute('SELECT count(*) FROM apple_challenges').fetchone()[0])
            self.assertEqual(0, db.execute('SELECT count(*) FROM apple_unlinks').fetchone()[0])
        self.fault(401, lambda: self.accounts.commit(c['ChallengeId'], self.new_token, preview['PreviewToken']))
        self.assertEqual(result, self.delete())

    def test_missing_apple_token_does_not_block_game_deletion(self):
        self.finish(self.start())
        with self.store.connect() as db:
            db.execute('DELETE FROM apple_grants')
        self.prepare_deletion()
        result = self.delete()
        self.assertTrue(result['ManualRevocationRequired'])
        self.assertEqual([], self.store.players())

    def test_apple_network_failure_does_not_undo_deletion(self):
        self.finish(self.start())
        self.prepare_deletion()
        self.delete()
        def unavailable(token):
            raise Fault(503, 'Synthetic provider failure')
        self.tokens.revoke = unavailable
        self.assertEqual('retry', self.vault.process_one(self.tokens))
        self.assertEqual([], self.store.players())
        self.assertEqual('pending', self.rows('apple_grants')[0]['state'])

    def test_first_link_response_after_deletion_is_queued_not_linked(self):
        self.prepare_deletion()
        self.hook = self.delete
        self.fault(401, lambda: self.finish(self.start()))
        self.assertEqual([], self.store.players())
        self.assertEqual([], self.rows('apple_links'))
        self.assertEqual('pending', self.rows('apple_grants')[0]['state'])

    def test_recovery_provider_response_after_deletion_cannot_restore(self):
        self.finish(self.start())
        self.prepare_deletion()
        self.hook = self.delete
        c = self.start('recover')
        self.fault(401, lambda: self.finish(c, self.new_token))
        self.assertTrue(all(r['state'] == 'pending' for r in self.rows('apple_grants')))
        self.assertEqual([], self.store.players())

    def test_expired_unresolved_first_link_keeps_deletion_fence(self):
        c = self.start()
        def after_expiry():
            self.clock += 301
            self.start()  # Ordinary challenge cleanup must not orphan exchange.
            self.prepare_deletion()
            self.delete()
        self.hook = after_expiry
        self.fault(401, lambda: self.finish(c))
        self.assertEqual([], self.store.players())
        self.assertEqual('pending', self.rows('apple_grants')[0]['state'])

    def test_ambiguous_first_link_does_not_prevent_deletion_or_lose_revoke_warning(self):
        def ambiguous():
            raise Fault(503, 'Synthetic lost response')
        self.hook = ambiguous
        self.fault(503, lambda: self.finish(self.start()))
        self.clock += 301
        self.start()
        self.prepare_deletion()
        result = self.delete()
        self.assertTrue(result['RevocationPending'])
        self.assertTrue(result['ManualRevocationRequired'])
        self.assertEqual([], self.store.players())

    def test_revocation_queue_and_deletion_roll_back_together(self):
        self.finish(self.start())
        self.prepare_deletion()
        original = self.vault.queue
        def fail_after_queue(db, subject):
            original(db, subject)
            raise OSError('Synthetic disk failure')
        with patch.object(self.vault, 'queue', fail_after_queue):
            with self.assertRaises(OSError):
                self.delete()
        self.assertEqual(1, len(self.store.players()))
        self.assertEqual(1, len(self.rows('apple_links')))
        self.assertEqual('active', self.rows('apple_grants')[0]['state'])
        self.delete()

    def test_deleting_old_unlinked_account_does_not_revoke_new_link_owner(self):
        self.finish(self.start())
        unlink = self.accounts.unlink_preview(self.player, self.token)
        self.accounts.unlink_commit(self.player, self.token, unlink['ConfirmationToken'])
        self.tokens.revoke = lambda token: None
        self.vault.process_one(self.tokens)
        other = uuid.uuid4().hex
        self.store.register(other, self.new_token)
        self.store.snapshot(other, dict(self.save, PlayerId=other))
        self.finish(self.start(token=self.new_token, player=other), self.new_token)
        self.prepare_deletion()
        self.delete()
        self.assertEqual(other, self.rows('apple_links')[0]['player'])
        self.assertEqual('active', self.rows('apple_grants')[0]['state'])
        self.assertEqual(900, self.store.head(other, token=self.new_token)['Free'])

    def test_apple_database_cannot_silently_omit_revocation_dependency(self):
        with self.assertRaises(ValueError):
            AccountDeletion(self.store)

    def test_link_changed_after_review_requires_new_confirmation(self):
        self.prepare_deletion()
        self.finish(self.start())
        self.fault(409, self.delete)
        self.assertEqual(1, len(self.store.players()))
