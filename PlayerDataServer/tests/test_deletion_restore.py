"""Disposable offline restoration fixtures; never use live accounts or keys."""
import hashlib
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from account_deletion import AccountDeletion
from account_linking import AccountLinking
from apple_grants import AppleGrantVault
from apple_tokens import AppleGrant
from deletion_restore import export_checkpoint, load_checkpoint, restore_filtered
from maintenance import backup_generation, digest_file, initialize, restore_copy, write_private
from production import create_app
from store import Store, Fault, encode, now


class DeletionRestoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.live = self.root / 'live'
        self.instance = 'restore-fixture-001'
        initialize(self.live, self.instance)
        self.store = Store(self.live / 'players.sqlite')
        self.player, self.other = uuid.uuid4().hex, uuid.uuid4().hex
        self.token, self.other_token = 'a' * 64, 'b' * 64
        self.store.register(self.player, self.token)
        self.store.register(self.other, self.other_token)
        save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                    SchemaVersion=3, PlayerLevel=1, Gold=10, FreeGachaStones=900, PaidGachaStones=0,
                    OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
        self.store.snapshot(self.player, save)
        self.store.snapshot(self.other, dict(save, PlayerId=self.other))
        self.deletion = AccountDeletion(self.store)
        self.checkpoint = self.root / 'checkpoint.json'
        self.destination = self.root / 'recovery'

    def backup(self):
        self.backup_path = backup_generation(self.store.path, self.root / 'backups')
        return self.backup_path

    def delete(self):
        confirm = self.deletion.preview(self.player, self.token)['ConfirmationToken']
        self.deletion.commit(self.player, self.token, confirm)

    def export(self, output=None):
        self.digest = export_checkpoint(self.live, output or self.checkpoint, self.instance)
        return self.digest

    def restore(self):
        return restore_filtered(self.backup_path, self.checkpoint, self.digest, self.instance, self.destination)

    def insert_purchase(self, owner, transaction):
        with self.store.connect() as db:
            db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple', transaction, owner, 'synthetic-product', now()))

    def test_old_account_save_credentials_and_post_backup_purchase_do_not_return(self):
        with self.store.connect() as db:
            db.execute('INSERT INTO claims VALUES(?,?)', (self.player, 'synthetic-claim'))
            db.execute('INSERT INTO flags(player,code,received,detail) VALUES(?,?,?,?)', (self.player, 'fixture', now(), 'private'))
        self.insert_purchase(self.player, 'before-backup')
        self.backup()
        original = digest_file(self.backup_path)
        self.insert_purchase(self.player, 'after-backup')
        self.delete()
        before = self.store.inspect(self.other)
        self.export()
        result = self.restore()
        self.assertEqual(1, result['RemovedPlayers'])
        recovered = Store(self.destination / 'players.sqlite')
        self.assertEqual(before, recovered.inspect(self.other))
        self.assertEqual(original, digest_file(self.backup_path))
        self.assertEqual(before, self.store.inspect(self.other))
        for token in (self.token, 'c' * 64):
            with self.assertRaises(Fault) as caught:
                recovered.register(self.player, token)
            self.assertEqual(410, caught.exception.status)
        with self.assertRaises(Fault):
            recovered.authenticate(self.player, self.token)
        recovered.purchase_verifier = lambda request: dict(verified=True, store='apple',
            transaction=request['TransactionId'], product=request['Target'])
        with self.assertRaises(Fault) as caught:
            recovered.operation(self.other, dict(Kind='purchase', Epoch=0, RequestId=uuid.uuid4().hex,
                Target='com.nasus.dungeonmonsterroguelike.crystals120', TransactionId='after-backup',
                Receipt='synthetic'), token=self.other_token)
        self.assertEqual(409, caught.exception.status)
        with recovered.connect() as db:
            for table in ('snapshots', 'operations', 'claims', 'flags', 'audit', 'purchases'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table + ' WHERE player=?', (self.player,)).fetchone()[0])
            self.assertEqual({Store.transaction_hash('apple', t) for t in ('before-backup', 'after-backup')},
                             {r[0] for r in db.execute('SELECT transaction_hash FROM retired_purchases')})
        self.assertFalse((self.destination / 'instance-id').exists())
        self.assertTrue((self.destination / 'RECOVERY-PENDING.txt').is_file())

    def test_checkpoint_contains_only_hashes_dates_and_instance_not_save_or_credentials(self):
        self.insert_purchase(self.player, 'private-transaction')
        self.delete(); self.export()
        payload = self.checkpoint.read_text()
        for secret in (self.player, self.token, 'private-transaction', 'OwnedMonsters', 'confirmation_hash', 'proof_hash'):
            self.assertNotIn(secret, payload)
        self.assertEqual(0, self.checkpoint.stat().st_mode & 0o077)
        self.assertEqual(self.digest, hashlib.sha256(self.checkpoint.read_bytes()).hexdigest())

    def test_known_newer_digest_rejects_older_valid_checkpoint(self):
        self.export()
        old_digest = self.digest
        self.delete()
        latest = self.export(self.root / 'latest.json')
        self.assertNotEqual(old_digest, latest)
        with self.assertRaises(ValueError):
            load_checkpoint(self.checkpoint, latest, self.instance)
        # Explicit limitation: a stale *trusted record* cannot be detected from
        # a consistent old file alone. Never advertise this as a live journal.
        self.assertEqual([], load_checkpoint(self.checkpoint, old_digest, self.instance)['DeletedPlayers'])

    def test_wrong_instance_and_modified_checkpoint_fail_before_copy(self):
        self.backup(); self.delete(); self.export()
        with self.assertRaises(ValueError):
            restore_filtered(self.backup_path, self.checkpoint, self.digest, 'another-instance', self.destination)
        self.checkpoint.write_text(self.checkpoint.read_text() + ' ')
        with self.assertRaises(ValueError): self.restore()
        self.assertFalse(self.destination.exists())

    def test_duplicate_unsorted_invalid_and_extra_fields_are_rejected(self):
        self.delete(); self.export()
        original = json.loads(self.checkpoint.read_text())
        variants = [dict(original, Version=True), dict(original, Extra='private'),
                    dict(original, DeletedPlayers=original['DeletedPlayers'] * 2),
                    dict(original, RetiredPurchases=['z' * 64]),
                    dict(original, RetiredPurchases=['f' * 64, 'a' * 64]),
                    dict(original, CreatedUtc='not-a-date')]
        for data in variants:
            payload = (encode(data) + '\n').encode()
            self.checkpoint.write_bytes(payload)
            with self.assertRaises(ValueError):
                load_checkpoint(self.checkpoint, hashlib.sha256(payload).hexdigest(), self.instance)

    def test_legacy_backup_without_instance_binding_is_rejected(self):
        self.backup(); self.delete(); self.export()
        path = Path(str(self.backup_path) + '.json')
        manifest = json.loads(path.read_text()); del manifest['InstanceId']
        path.write_text(json.dumps(manifest))
        with self.assertRaises(ValueError): self.restore()
        self.assertFalse(self.destination.exists())

    def test_source_missing_deletion_tables_is_not_empty_checkpoint(self):
        with self.store.connect() as db:
            db.execute('DROP TABLE retired_purchases')
        with self.assertRaises(ValueError): self.export()
        self.assertFalse(self.checkpoint.exists())

    def test_existing_tombstones_in_newer_backup_are_preserved(self):
        self.export()  # Empty older deletion checkpoint, supplied as a fixture.
        self.delete(); self.backup(); self.restore()
        recovered = Store(self.destination / 'players.sqlite')
        with self.assertRaises(Fault): recovered.register(self.player, self.token)

    def test_orphan_audit_and_flags_are_scrubbed_without_live_player(self):
        self.delete()
        with self.store.connect() as db:
            db.execute('INSERT INTO audit(player,actor,action,reason,received,detail) VALUES(?,?,?,?,?,?)',
                       (self.player, 'fixture', 'test', 'test', now(), 'private-old-save'))
            db.execute('INSERT INTO flags(player,code,received,detail) VALUES(?,?,?,?)',
                       (self.player, 'test', now(), 'private-old-save'))
        self.backup(); self.export(); self.restore()
        with sqlite3.connect(self.destination / 'players.sqlite') as db:
            self.assertEqual(0, db.execute('SELECT count(*) FROM audit WHERE player=?', (self.player,)).fetchone()[0])
            self.assertEqual(0, db.execute('SELECT count(*) FROM flags WHERE player=?', (self.player,)).fetchone()[0])

    def test_retired_purchase_collision_with_survivor_stops_recovery(self):
        self.insert_purchase(self.other, 'collision'); self.backup()
        with self.store.connect() as db:
            db.execute('INSERT INTO retired_purchases VALUES(?)', (Store.transaction_hash('apple', 'collision'),))
        self.export()
        with self.assertRaises(ValueError): self.restore()
        self.assertTrue((self.destination / 'RECOVERY-PENDING.txt').exists())
        self.assertFalse((self.destination / 'DELETION-RECONCILIATION.json').exists())

    def test_unreviewed_tables_columns_and_triggers_stop_recovery(self):
        self.delete(); self.export()
        for index, sql in enumerate(('CREATE TABLE future_private_data(player TEXT)',
                'ALTER TABLE players ADD COLUMN future_secret TEXT',
                "CREATE TRIGGER unexpected AFTER DELETE ON players BEGIN DELETE FROM flags; END")):
            with self.subTest(sql=sql):
                # Independently build another synthetic database for each case.
                path = self.root / ('schema-' + str(index))
                initialize(path, self.instance)
                with sqlite3.connect(path / 'players.sqlite') as db: db.execute(sql)
                backup = backup_generation(path / 'players.sqlite', self.root / 'backups')
                dest = self.root / ('candidate-' + str(index))
                with self.assertRaises(ValueError):
                    restore_filtered(backup, self.checkpoint, self.digest, self.instance, dest)
                self.assertTrue((dest / 'RECOVERY-PENDING.txt').exists())

    def test_failure_keeps_quarantine_and_never_rewrites_backup_or_existing_output(self):
        self.backup(); self.delete(); self.export()
        before = digest_file(self.backup_path)
        with patch('deletion_restore._reconcile', side_effect=OSError('synthetic disk failure')):
            with self.assertRaises(OSError): self.restore()
        self.assertTrue((self.destination / 'RECOVERY-PENDING.txt').exists())
        self.assertFalse((self.destination / 'DELETION-RECONCILIATION.json').exists())
        with self.assertRaises(FileExistsError): self.restore()
        with self.assertRaises(FileExistsError): self.export()
        self.assertEqual(before, digest_file(self.backup_path))

    def test_marker_written_before_database_copy(self):
        self.backup()
        def crash(source, target):
            self.assertTrue((Path(target).parent / 'RECOVERY-PENDING.txt').exists())
            raise OSError('synthetic copy failure')
        with patch('maintenance.backup_database', side_effect=crash):
            with self.assertRaises(OSError): restore_copy(self.backup_path, self.destination)
        self.assertTrue((self.destination / 'RECOVERY-PENDING.txt').exists())

    def test_quarantine_cannot_be_bypassed_by_copying_instance_marker(self):
        self.backup(); self.delete(); self.export(); self.restore()
        write_private(self.destination / 'instance-id', self.instance + '\n')
        env = dict(WITCH_DATA_DIR=str(self.destination), WITCH_INSTANCE_ID=self.instance,
                   WITCH_PUBLIC_ORIGIN='https://fixture.invalid', WITCH_OPERATORS_FILE=str(self.root / 'not-read'))
        with self.assertRaisesRegex(ValueError, 'quarantined'): create_app(env)
        with self.assertRaisesRegex(ValueError, 'authoritative'):
            export_checkpoint(self.destination, self.root / 'unsafe.json', self.instance)
        (self.destination / 'RECOVERY-PENDING.txt').unlink()
        (self.destination / 'RECOVERY-PENDING.txt').symlink_to(self.root / 'missing')
        with self.assertRaisesRegex(ValueError, 'quarantined'): create_app(env)

    def test_symlink_input_or_output_refused(self):
        self.backup(); self.delete(); self.export()
        linked = self.root / 'linked.json'; linked.symlink_to(self.checkpoint)
        with self.assertRaises(ValueError): load_checkpoint(linked, self.digest, self.instance)
        self.destination.symlink_to(self.root / 'missing')
        with self.assertRaises(ValueError): self.restore()

    def test_historical_confirmations_and_receipts_are_invalidated(self):
        self.deletion.preview(self.other, self.other_token)
        with self.store.connect() as db:
            db.execute('INSERT INTO deletion_receipts VALUES(?,?,?)', ('c' * 64, 9999999999, '{}'))
        self.backup(); self.delete(); self.export(); self.restore()
        with sqlite3.connect(self.destination / 'players.sqlite') as db:
            for table in ('deletion_confirmations', 'deletion_receipts'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])

    def apple_setup(self):
        # Explicit synthetic key exists only in this temporary test process.
        vault = AppleGrantVault(self.store, b'v' * 32, 'com.fixture.game')
        verifier = lambda identity, nonce: 'fixture-subject'
        linking = AccountLinking(self.store, verifier)
        self.subject = vault.subject_hash('fixture-subject')
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            vault.save(db, AppleGrant('fixture-subject', 'private-fixture-refresh'))
            db.execute('INSERT INTO apple_links VALUES(?,?,?)', (self.subject, self.player, now()))
            db.execute('INSERT INTO apple_challenges VALUES(?,?,?,?,?,?,?,?)',
                       ('c' * 64, 'nonce', 'recover', None, 'h' * 64, 9999999999, 'private-cached-save', 'fingerprint'))
        # Existing API requires explicit revocation dependency, no network here.
        linking.vault = vault
        self.deletion = AccountDeletion(self.store, account_linking=linking)

    def test_apple_cached_saves_removed_but_encrypted_revocation_evidence_retained(self):
        self.apple_setup(); self.backup(); self.delete(); self.export()
        result = self.restore()
        self.assertTrue(result['AppleReconciliationRequired'])
        self.assertEqual(1, result['QueuedHistoricalGrants'])
        with sqlite3.connect(self.destination / 'players.sqlite') as db:
            for table in ('apple_links', 'apple_challenges', 'apple_unlinks'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])
            self.assertEqual(('pending', None), db.execute('SELECT state,lease FROM apple_grants').fetchone())
            self.assertEqual(1, db.execute('SELECT generation FROM apple_grant_generations').fetchone()[0])

    def test_old_unlink_does_not_queue_new_owner_grant(self):
        self.apple_setup()
        with self.store.connect() as db:
            db.execute('UPDATE apple_links SET player=?', (self.other,))
            db.execute('INSERT INTO apple_unlinks VALUES(?,?,?,?,?,?)',
                       ('u' * 64, self.player, 'h' * 64, self.subject, 9999999999, '{}'))
        self.backup(); self.delete(); self.export(); self.restore()
        with sqlite3.connect(self.destination / 'players.sqlite') as db:
            self.assertEqual(self.other, db.execute('SELECT player FROM apple_links').fetchone()[0])
            self.assertEqual('active', db.execute('SELECT state FROM apple_grants').fetchone()[0])

    def test_interrupted_scrub_rolls_back_candidate_and_stays_offline(self):
        self.apple_setup(); self.backup(); self.delete(); self.export()
        from deletion_restore import _scrub_apple
        def fail_after_changes(db, players):
            _scrub_apple(db, players)
            raise OSError('synthetic disk failure after modifying candidate')
        with patch('deletion_restore._scrub_apple', side_effect=fail_after_changes):
            with self.assertRaises(OSError): self.restore()
        with sqlite3.connect(self.destination / 'players.sqlite') as db:
            self.assertEqual(2, db.execute('SELECT count(*) FROM players').fetchone()[0])
            self.assertEqual(0, db.execute('SELECT count(*) FROM deleted_players').fetchone()[0])
            self.assertEqual('active', db.execute('SELECT state FROM apple_grants').fetchone()[0])
        self.assertTrue((self.destination / 'RECOVERY-PENDING.txt').exists())
        self.assertFalse((self.destination / 'DELETION-RECONCILIATION.json').exists())

    def test_deletion_before_backup_can_filter_legacy_schema_with_bound_instance(self):
        # A reviewed legacy database may predate the deletion tables. Identity
        # provenance must still be supplied by a bound backup manifest.
        with self.store.connect() as db:
            db.execute('DROP TABLE deleted_players'); db.execute('DROP TABLE retired_purchases')
        self.backup()
        self.store = Store(self.store.path)
        self.deletion = AccountDeletion(self.store)
        self.delete(); self.export(); self.restore()
        recovered = Store(self.destination / 'players.sqlite')
        with self.assertRaises(Fault): recovered.register(self.player, self.token)


if __name__ == '__main__':
    unittest.main()
