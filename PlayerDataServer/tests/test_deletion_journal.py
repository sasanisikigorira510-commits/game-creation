"""Synthetic offsite storage only. No actual bucket, credentials or Apple calls."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import uuid

from account_deletion import AccountDeletion
from deletion_journal import DeletionJournal, validate_event
from deletion_restore import export_checkpoint, restore_filtered
from deploy.sakura_deletion_journal import AgeEncryptor, SakuraPublisher, ENDPOINT
from maintenance import initialize, backup_generation
from store import Store, Fault, now


class JournalTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.instance = 'synthetic-journal-001'
        self.live = self.root / 'live'
        initialize(self.live, self.instance)
        self.store = Store(self.live / 'players.sqlite')
        self.player, self.token = uuid.uuid4().hex, 'a' * 64
        self.store.register(self.player, self.token)
        self.clock = 1000
        self.journal = DeletionJournal(self.store, self.instance, clock=lambda: self.clock)
        self.api = AccountDeletion(self.store, journal=self.journal, clock=lambda: self.clock)
        self.confirm = self.api.preview(self.player, self.token)['ConfirmationToken']
        self.remote = {}
        self.encrypted = []

    def encrypt(self, payload):
        # Fake cipher for deterministic failure tests. Real age tested separately.
        self.encrypted.append(payload)
        return b'age-encryption.org/v1\n' + hashlib.sha256(payload).hexdigest().encode()

    def publish(self, key, ciphertext):
        self.remote[key] = ciphertext
        return hashlib.sha256(ciphertext).hexdigest()

    def fault(self, status, fn):
        with self.assertRaises(Fault) as caught: fn()
        self.assertEqual(status, caught.exception.status)

    def commit(self):
        return self.api.commit(self.player, self.token, self.confirm)

    def status(self):
        return self.api.status(self.player, self.token, self.confirm)

    def cancel(self):
        return self.api.cancel(self.player, self.token, self.confirm)

    def row(self):
        with self.store.connect() as db:
            return dict(db.execute('SELECT * FROM deletion_outbox').fetchone())

    def send(self, publisher=None):
        return self.journal.process_one(self.encrypt, publisher or self.publish)

    def test_deleted_not_returned_until_verified_readback(self):
        with self.store.connect() as db:
            db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple', 'private-order', self.player, 'fixture', now()))
        # Purchases affect the deletion fingerprint only through player/snapshot,
        # but normal Store operations update the wallet too; review once again.
        self.confirm = self.api.preview(self.player, self.token)['ConfirmationToken']
        self.fault(503, self.commit)
        self.assertEqual([], self.store.players())
        for fn in (self.commit, self.status, self.cancel): self.fault(503, fn)
        self.assertEqual('pending', self.row()['state'])
        event = validate_event(self.row()['payload'].encode(), self.instance)
        self.assertEqual([Store.transaction_hash('apple', 'private-order')], event['RetiredPurchases'])
        for secret in (self.player, self.token, self.confirm, 'private-order', self.row()['proof_hash']):
            self.assertNotIn(secret, self.row()['payload'])
        self.assertEqual('verified', self.send())
        for fn in (self.commit, self.status, self.cancel): self.assertEqual('deleted', fn()['Status'])
        self.assertEqual('idle', self.send())
        self.assertEqual(1, len(self.remote))
        self.assertEqual(dict(Pending=0, Sending=0, Verified=1), self.journal.health())

    def test_outbox_failure_rolls_back_deletion_and_receipt(self):
        original = self.journal.enqueue
        def fail(*args):
            original(*args)
            raise OSError('synthetic disk full')
        with patch.object(self.journal, 'enqueue', fail):
            with self.assertRaises(OSError): self.commit()
        self.store.authenticate(self.player, self.token)
        with self.store.connect() as db:
            for table in ('deletion_outbox', 'deleted_players', 'deletion_receipts'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])

    def test_receipt_failure_cannot_leave_delivery_record(self):
        with self.store.connect() as db:
            db.execute("CREATE TRIGGER fixture_failure BEFORE INSERT ON deletion_receipts BEGIN SELECT RAISE(ABORT,'fixture'); END")
        with self.assertRaises(sqlite3.IntegrityError): self.commit()
        self.store.authenticate(self.player, self.token)
        self.assertEqual('idle', self.send())

    def test_cancel_before_commit_never_produces_event(self):
        self.assertEqual('cancelled', self.cancel()['Status'])
        self.fault(409, self.commit)
        self.assertEqual('idle', self.send())
        self.store.authenticate(self.player, self.token)

    def test_wrong_proof_cannot_see_pending_or_verified_receipt(self):
        self.fault(503, self.commit)
        for phase in ('pending', 'verified'):
            with self.subTest(phase=phase):
                self.fault(401, lambda: self.api.status(self.player, 'b' * 64, self.confirm))
                self.fault(401, lambda: self.api.cancel(self.player, self.token, 'c' * 64))
            self.send()

    def test_network_failure_retries_same_key_and_exact_ciphertext(self):
        self.fault(503, self.commit)
        calls = []
        def lost_response(key, ciphertext):
            calls.append((key, ciphertext)); self.publish(key, ciphertext)
            raise RuntimeError('sensitive provider text must not be retained')
        self.assertEqual('retry', self.send(lost_response))
        self.fault(503, self.status)
        self.assertEqual('idle', self.send())
        self.assertEqual('offsite_unverified', self.row()['last_error'])
        self.clock += 31
        def verified(key, ciphertext):
            calls.append((key, ciphertext)); return self.publish(key, ciphertext)
        self.assertEqual('verified', self.send(verified))
        self.assertEqual(calls[0], calls[1])
        self.assertEqual(1, len(self.encrypted))
        self.assertEqual(1, len(self.remote))

    def test_wrong_readback_digest_never_acknowledges(self):
        self.fault(503, self.commit)
        self.assertEqual('retry', self.send(lambda key, ciphertext: '0' * 64))
        self.fault(503, self.status)

    def test_encryption_failure_never_publishes_plaintext(self):
        self.fault(503, self.commit)
        for value in (b'plaintext', 'not-bytes'):
            with self.assertRaises(ValueError):
                self.journal.process_one(lambda payload: value, self.publish)
        self.assertEqual({}, self.remote)
        self.assertIsNone(self.row()['ciphertext'])
        self.assertEqual('pending', self.row()['state'])

    def test_crash_after_upload_before_ack_retries_after_lease(self):
        self.fault(503, self.commit)
        def die(key, ciphertext):
            self.publish(key, ciphertext)
            raise KeyboardInterrupt()  # Model abrupt worker death, not a retryable error.
        with self.assertRaises(KeyboardInterrupt): self.send(die)
        first = self.row()
        self.assertEqual('sending', first['state'])
        self.assertEqual('idle', self.send())
        self.clock += 121
        reopened = DeletionJournal(self.store, self.instance, clock=lambda: self.clock)
        self.assertEqual('verified', reopened.process_one(self.encrypt, self.publish))
        self.assertEqual(first['ciphertext'], self.row()['ciphertext'])
        self.assertEqual(1, len(self.remote))

    def test_stale_worker_cannot_change_newer_verified_result(self):
        self.fault(503, self.commit)
        def slow(key, ciphertext):
            self.clock += 121
            self.assertEqual('verified', self.send())
            return '0' * 64
        self.assertEqual('stale', self.send(slow))
        self.assertEqual('verified', self.row()['state'])
        self.assertEqual('deleted', self.status()['Status'])

    def test_concurrent_delivery_has_one_active_lease(self):
        self.fault(503, self.commit)
        with concurrent.futures.ThreadPoolExecutor(2) as executor:
            outcomes = list(executor.map(lambda _: self.send(), range(2)))
        self.assertEqual(['idle', 'verified'], sorted(outcomes))
        self.assertEqual(1, len(self.encrypted))

    def test_commit_cancel_race_never_reports_cancelled_after_erasure(self):
        def call(fn):
            try: return fn()['Status']
            except Fault as error: return error.status
        with concurrent.futures.ThreadPoolExecutor(2) as executor:
            futures = [executor.submit(call, fn) for fn in (self.commit, self.cancel)]
            outcomes = [f.result() for f in futures]
        self.assertIn(outcomes, ([503, 503], [409, 'cancelled']))

    def test_pending_receipt_survives_outage_longer_than_one_day(self):
        self.fault(503, self.commit)
        self.clock += 90000
        other = uuid.uuid4().hex; self.store.register(other, 'o' * 64)
        self.api.preview(other, 'o' * 64)  # Normal receipt expiry sweep.
        self.fault(503, self.status)
        self.assertEqual('verified', self.send())
        self.assertEqual('deleted', self.status()['Status'])
        self.clock += 86401
        self.fault(401, self.status)

    def test_dependency_cannot_be_omitted_after_journal_activation(self):
        with self.assertRaises(ValueError): AccountDeletion(self.store)
        self.fault(503, self.commit)
        self.api.journal = None  # Also catches a previously constructed legacy instance.
        with self.assertRaises(ValueError): self.status()

    def test_bootstrap_cannot_silently_ignore_historical_deletions(self):
        other_dir = self.root / 'old'
        initialize(other_dir, 'synthetic-legacy-001')
        other = Store(other_dir / 'players.sqlite')
        with other.connect() as db:
            db.execute('INSERT INTO deleted_players VALUES(?,?)', ('f' * 64, now()))
        with self.assertRaisesRegex(ValueError, 'Historical'):
            DeletionJournal(other, 'synthetic-legacy-001')
        with self.assertRaises(ValueError): DeletionJournal(self.store, 'wrong-instance')

    def test_journal_backup_filters_deletions_and_cannot_start_uploader(self):
        backup = backup_generation(self.store.path, self.root / 'backups')
        self.fault(503, self.commit); self.send()
        checkpoint = self.root / 'checkpoint'
        sha = export_checkpoint(self.live, checkpoint, self.instance)
        destination = self.root / 'recovery'
        restore_filtered(backup, checkpoint, sha, self.instance, destination)
        (destination / 'instance-id').write_text(self.instance)
        with self.assertRaisesRegex(ValueError, 'recovery candidate'):
            DeletionJournal(Store(destination / 'players.sqlite'), self.instance)


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.config = self.root / 'curl.conf'; self.config.write_text('# synthetic configuration')
        self.config.chmod(0o600)
        self.key = 'deletions/synthetic-instance/' + 'a' * 32 + '.json.age'
        self.ciphertext = b'age-encryption.org/v1\n' + b'c' * 100

    def test_adapter_put_readback_private_bounded_fixed_endpoint(self):
        calls = []
        def fake(args, **kwargs):
            calls.append((args, kwargs))
            return subprocess.CompletedProcess(args, 0, stdout=self.ciphertext, stderr=b'')
        digest = SakuraPublisher(self.config, run=fake)(self.key, self.ciphertext)
        self.assertEqual(hashlib.sha256(self.ciphertext).hexdigest(), digest)
        self.assertEqual(2, len(calls))
        for args, kwargs in calls:
            self.assertIn(ENDPOINT + self.key, args)
            self.assertEqual('--disable', args[1])
            self.assertIn('=https', args)
            self.assertEqual(35, kwargs['timeout'])
            for unsafe in ('--insecure', '--location', '--user', 'DELETE'): self.assertNotIn(unsafe, args)
        self.assertEqual(self.ciphertext, calls[0][1]['input'])
        self.assertIn('x-amz-acl: private', calls[0][0])
        self.assertNotIn('input', calls[1][1])

    def test_adapter_rejects_bad_readback_plaintext_paths_and_permissions(self):
        fake = lambda args, **kwargs: subprocess.CompletedProcess(args, 0, stdout=b'wrong', stderr=b'private-response')
        publish = SakuraPublisher(self.config, run=fake)
        with self.assertRaisesRegex(RuntimeError, '^Deletion event readback unverified$'):
            publish(self.key, self.ciphertext)
        for key, body in (('../elsewhere', self.ciphertext), (self.key, b'plaintext')):
            with self.assertRaises(ValueError): publish(key, body)
        self.config.chmod(0o644)
        with self.assertRaises(ValueError): publish(self.key, self.ciphertext)

    def test_adapter_timeout_hides_command_and_credentials(self):
        def fail(args, **kwargs): raise subprocess.TimeoutExpired(['private-command'], 35, stderr=b'secret')
        with self.assertRaisesRegex(RuntimeError, '^Deletion event transfer timed out$'):
            SakuraPublisher(self.config, run=fail)(self.key, self.ciphertext)

    @unittest.skipUnless(os.environ.get('NASUS_TEST_AGE'), 'age integration binary not supplied')
    def test_real_age_encrypt_decrypt_synthetic_event(self):
        age = Path(os.environ['NASUS_TEST_AGE'])
        identity = self.root / 'identity'
        subprocess.run([str(age.with_name('age-keygen')), '-o', str(identity)], capture_output=True, check=True)
        recipient = subprocess.check_output([str(age.with_name('age-keygen')), '-y', str(identity)])
        public = self.root / 'recipient'; public.write_bytes(recipient); public.chmod(0o600)
        payload = b'{"synthetic":"no-real-players"}\n'
        cipher = AgeEncryptor(age, public)(payload)
        result = subprocess.run([str(age), '-d', '-i', str(identity)], input=cipher, capture_output=True, check=True)
        self.assertEqual(payload, result.stdout)
        self.assertNotIn(payload, cipher)


if __name__ == '__main__': unittest.main()
