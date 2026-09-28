"""Disposable SQLite fixtures; no VPS, real accounts, keys or bucket requests."""
from contextlib import closing
import datetime as dt
import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import patch

from account_deletion import AccountDeletion
from deletion_event_restore import checkpoint_from_events
from deletion_inventory import (inspect_inventory, export_inventory, monotonic_inventory,
                                inventory_from_connection, parse_inventory)
from deletion_journal import DeletionJournal
from maintenance import initialize
from store import Store, Fault, encode

NOW = dt.datetime(2026, 9, 26, 12, tzinfo=dt.timezone.utc)


class DeletionInventoryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.data = self.root/'live'; self.instance = 'inventory-fixture-001'
        initialize(self.data, self.instance)
        self.store = Store(self.data/'players.sqlite')
        self.journal = DeletionJournal(self.store, self.instance, clock=lambda: NOW.timestamp()+1)
        self.api = AccountDeletion(self.store, journal=self.journal, clock=lambda: NOW.timestamp()+1)
        self.baseline, _ = self.inspect()
        self.previous = self.root/'previous.json'
        self.previous.write_bytes(self.baseline); self.previous.chmod(0o600)
        self.digest = hashlib.sha256(self.baseline).hexdigest()

    def inspect(self):
        return inspect_inventory(self.data, self.instance, clock=lambda: NOW+dt.timedelta(seconds=5))

    def delete(self, char='a', purchased=False):
        player, token = char*32, char*64
        self.store.register(player, token)
        if purchased:
            with self.store.connect() as db:
                db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple','private-order-'+char,player,'fixture',NOW.isoformat()))
        confirmation = self.api.preview(player, token)['ConfirmationToken']
        with patch('account_deletion.now', return_value=NOW.isoformat()):
            with self.assertRaises(Fault) as caught: self.api.commit(player, token, confirmation)
        self.assertEqual(503, caught.exception.status)

    def send(self):
        self.assertEqual('verified', self.journal.process_one(
            lambda payload: b'age-encryption.org/v1\n'+b'x'*64,
            lambda key, cipher: hashlib.sha256(cipher).hexdigest()))

    def events(self):
        with self.store.connect() as db:
            return {row[0]: row[1].encode() for row in db.execute('SELECT event_id,payload FROM deletion_outbox')}

    def export(self, **kwargs):
        return export_inventory(self.data, self.instance, self.root/'output.json',
            clock=lambda: NOW+dt.timedelta(seconds=6), **kwargs)

    def test_empty_baseline_is_explicit_and_private(self):
        with self.assertRaises(ValueError): self.export()
        result = self.export(bootstrap_empty=True)
        self.assertEqual(0, result['CurrentEvents'])
        self.assertFalse(result['ExternallyStored']); self.assertFalse(result['LatestCompletenessProven'])
        self.assertEqual(0o600, (self.root/'output.json').stat().st_mode & 0o777)

    def test_pending_event_included_before_acknowledgement(self):
        self.delete(purchased=True)
        raw, states = self.inspect()
        self.assertEqual(dict(Pending=1, Sending=0, Verified=0), states)
        entries = json.loads(raw)['Events']; self.assertEqual(1, len(entries))
        self.assertEqual(set(entries[0]), {'EventId','Sha256','Bytes'})
        for forbidden in ('PlayerHash', 'RetiredPurchases', 'proof_hash', 'a'*32, 'private-order'):
            self.assertNotIn(forbidden, raw.decode())
        with self.assertRaises(ValueError):
            checkpoint_from_events(raw, hashlib.sha256(raw).hexdigest(), self.instance, {})

    def test_pending_sending_verified_payload_identity_unchanged(self):
        self.delete()
        pending, _ = self.inspect()
        ticket = self.journal.claim(lambda _: b'age-encryption.org/v1\n'+b'x'*64)
        sending, states = self.inspect(); self.assertEqual(1, states['Sending'])
        self.journal.finish(ticket['event_id'], ticket['lease'], True)
        verified, states = self.inspect(); self.assertEqual(1, states['Verified'])
        self.assertEqual(pending, sending); self.assertEqual(pending, verified)

    def test_source_rows_unchanged(self):
        self.delete()
        def dump():
            with closing(sqlite3.connect(self.data/'players.sqlite')) as db: return list(db.iterdump())
        before = dump(); self.inspect(); self.assertEqual(before, dump())

    def test_nonempty_bootstrap_rejected(self):
        self.delete()
        with self.assertRaises(ValueError): self.export(bootstrap_empty=True)
        self.assertFalse((self.root/'output.json').exists())

    def test_monotonic_growth_and_offline_restore_compatibility(self):
        self.delete(purchased=True); self.send()
        first, _ = self.inspect()
        self.delete('b')
        second, _ = self.inspect()
        changes = monotonic_inventory(second, first, hashlib.sha256(first).hexdigest(), self.instance)
        self.assertEqual(dict(PreviousEvents=1, CurrentEvents=2, AddedEvents=1), changes)
        checkpoint = json.loads(checkpoint_from_events(second, hashlib.sha256(second).hexdigest(), self.instance, self.events()))
        self.assertEqual(2, len(checkpoint['DeletedPlayers'])); self.assertEqual(1, len(checkpoint['RetiredPurchases']))

    def test_missing_outbox_event_or_purchase_tombstone_rejected(self):
        self.delete(purchased=True)
        with self.store.connect() as db: db.execute('DELETE FROM deletion_outbox')
        with self.assertRaises(ValueError): self.inspect()

    def test_missing_player_tombstone_rejected(self):
        self.delete()
        with self.store.connect() as db: db.execute('DELETE FROM deleted_players')
        with self.assertRaises(ValueError): self.inspect()

    def test_extra_or_missing_retired_purchase_rejected(self):
        self.delete(purchased=True)
        with self.store.connect() as db: db.execute('DELETE FROM retired_purchases')
        with self.assertRaises(ValueError): self.inspect()
        with self.store.connect() as db: db.execute('INSERT INTO retired_purchases VALUES(?)', ('f'*64,))
        with self.assertRaises(ValueError): self.inspect()

    def test_whole_history_rollback_detected_by_previous_anchor(self):
        self.delete(); previous, _ = self.inspect()
        with self.store.connect() as db:
            db.execute('DELETE FROM deletion_outbox'); db.execute('DELETE FROM deleted_players')
        current, _ = self.inspect()  # Internally consistent, but older than retained history.
        with self.assertRaises(ValueError):
            monotonic_inventory(current, previous, hashlib.sha256(previous).hexdigest(), self.instance)

    def test_payload_change_and_wrong_digest_rejected(self):
        self.delete(); previous, _ = self.inspect()
        value = json.loads(previous); value['Events'][0]['Sha256'] = 'f'*64
        for current, digest in (((encode(value)+'\n').encode(), hashlib.sha256(previous).hexdigest()),
                                (previous, '0'*64)):
            with self.assertRaises(ValueError): monotonic_inventory(current, previous, digest, self.instance)

    def test_backward_capture_time_and_wrong_instance_rejected(self):
        value = json.loads(self.baseline); value['CreatedUtc'] = (NOW-dt.timedelta(days=1)).isoformat()
        with self.assertRaises(ValueError):
            monotonic_inventory((encode(value)+'\n').encode(), self.baseline, self.digest, self.instance)
        with self.assertRaises(ValueError): inspect_inventory(self.data, 'wrong-instance-001')

    def test_quarantine_missing_db_public_permissions_and_symlinks_rejected(self):
        quarantine = self.data/'RECOVERY-PENDING.txt'; quarantine.touch()
        with self.assertRaises(ValueError): self.inspect()
        quarantine.unlink()
        database = self.data/'players.sqlite'; database.chmod(0o644)
        with self.assertRaises(ValueError): self.inspect()
        database.chmod(0o600)
        link = self.root/'linked'; link.symlink_to(self.data)
        with self.assertRaises(ValueError): inspect_inventory(link, self.instance)
        with self.assertRaises(OSError): inspect_inventory(self.root/'absent', self.instance)
        self.assertFalse((self.root/'absent').exists())

    def test_query_only_transaction_required(self):
        with closing(sqlite3.connect(self.data/'players.sqlite')) as db:
            with self.assertRaises(ValueError): inventory_from_connection(db, self.instance, NOW.isoformat())
            db.execute('BEGIN')
            with self.assertRaises(ValueError): inventory_from_connection(db, self.instance, NOW.isoformat())

    def test_unexpected_schema_and_invalid_cipher_stop(self):
        self.delete(); self.send()
        with self.store.connect() as db: db.execute("UPDATE deletion_outbox SET ciphertext=x'00'")
        with self.assertRaises(ValueError): self.inspect()
        with self.store.connect() as db: db.execute('CREATE TABLE unrelated(x)')
        with self.assertRaises(ValueError): self.inspect()

    def test_valid_export_with_anchor_and_no_overwrite(self):
        self.delete()
        result = self.export(previous=self.previous, expected_previous_sha256=self.digest)
        self.assertEqual(1, result['AddedEvents']); self.assertEqual(1, result['Pending'])
        with self.assertRaises(ValueError): self.export(previous=self.previous, expected_previous_sha256=self.digest)

    def test_duplicate_and_noncanonical_inventory_rejected(self):
        self.delete(); raw, _ = self.inspect(); value = json.loads(raw)
        value['Events'] *= 2
        with self.assertRaises(ValueError): parse_inventory((encode(value)+'\n').encode(), self.instance)
        with self.assertRaises(ValueError): parse_inventory(raw+b' ', self.instance)

    def test_committed_read_snapshot_does_not_claim_later_write(self):
        def concurrent_write():
            self.delete()
            return NOW+dt.timedelta(seconds=5)
        raw, _ = inspect_inventory(self.data, self.instance, clock=concurrent_write)
        self.assertEqual([], json.loads(raw)['Events'])
        later, _ = self.inspect(); self.assertEqual(1, len(json.loads(later)['Events']))
