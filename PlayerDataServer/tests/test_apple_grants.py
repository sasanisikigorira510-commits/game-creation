"""Temporary SQLite + generated test AES key; never reads deployment secrets."""
import concurrent.futures
from pathlib import Path
import secrets
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
try:
    from apple_grants import AppleGrantVault
    from apple_tokens import AppleGrant
except ImportError:
    AppleGrantVault = None
from store import Store, Fault


@unittest.skipIf(AppleGrantVault is None, 'Install requirements-identity.txt')
class AppleGrantTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.store = Store(Path(self.tmp.name) / 'test.sqlite')
        self.clock = 1000
        self.key = secrets.token_bytes(32)
        self.vault = AppleGrantVault(self.store, self.key, 'com.test.game', clock=lambda: self.clock)
        self.subject = self.vault.subject_hash('synthetic-user')
        self.calls = []
        self.client = SimpleNamespace(client_id='com.test.game', revoke=self.calls.append)

    def save(self, subject='synthetic-user', token='synthetic-refresh-secret'):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            return self.vault.save(db, AppleGrant(subject, token))

    def queue(self, subject=None):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            return self.vault.queue(db, subject or self.subject)

    def rows(self):
        with self.store.connect() as db:
            return list(db.execute('SELECT * FROM apple_grants ORDER BY id'))

    def test_tokens_encrypted_in_database_and_wal(self):
        self.save()
        self.assertEqual('active', self.rows()[0]['state'])
        for path in Path(self.tmp.name).iterdir():
            raw = path.read_bytes()
            self.assertNotIn(b'synthetic-refresh-secret', raw)
            self.assertNotIn(b'synthetic-user', raw)
        self.assertEqual('idle', self.vault.process_one(self.client))
        self.assertEqual([], self.calls)

    def test_same_token_has_randomized_ciphertext_and_keeps_older_grants(self):
        first, second = self.save(), self.save()
        rows = self.rows()
        self.assertNotEqual(first, second)
        self.assertNotEqual(rows[0]['encrypted_token'], rows[1]['encrypted_token'])
        self.assertEqual(2, self.queue())
        self.assertEqual('revoked', self.vault.process_one(self.client))
        self.assertEqual('revoked', self.vault.process_one(self.client))
        self.assertEqual([], self.rows())

    def test_queue_is_idempotent_and_isolated_by_subject(self):
        self.save()
        self.save('other-user', 'other-refresh')
        self.assertEqual(1, self.queue())
        self.assertEqual(0, self.queue())
        self.assertEqual('revoked', self.vault.process_one(self.client))
        self.assertEqual(['synthetic-refresh-secret'], self.calls)
        self.assertEqual(1, len(self.rows()))
        self.assertEqual('active', self.rows()[0]['state'])

    def test_missing_grant_does_not_prevent_caller_deletion(self):
        self.assertEqual(0, self.queue())
        self.assertEqual('idle', self.vault.process_one(self.client))

    def test_save_and_queue_require_caller_transaction(self):
        with self.store.connect() as db:
            with self.assertRaises(ValueError): self.vault.save(db, AppleGrant('subject', 'token'))
            with self.assertRaises(ValueError): self.vault.queue(db, self.subject)
        self.assertEqual([], self.rows())

    def test_save_and_queue_rollback_with_caller_transaction(self):
        with self.assertRaises(RuntimeError), self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.vault.save(db, AppleGrant('synthetic-user', 'token'))
            raise RuntimeError('cancel transaction')
        self.assertEqual([], self.rows())
        self.save()
        with self.assertRaises(RuntimeError), self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.vault.queue(db, self.subject)
            raise RuntimeError('cancel transaction')
        self.assertEqual('active', self.rows()[0]['state'])

    def test_provider_failure_keeps_ciphertext_and_retries_after_backoff(self):
        self.save(); self.queue()
        encrypted = self.rows()[0]['encrypted_token']
        def unavailable(token): raise Fault(503, 'secret-details-not-for-db')
        self.client.revoke = unavailable
        self.assertEqual('retry', self.vault.process_one(self.client))
        row = self.rows()[0]
        self.assertEqual(encrypted, row['encrypted_token'])
        self.assertEqual(1, row['attempts'])
        self.assertEqual('provider_unavailable', row['last_error'])
        self.assertEqual('idle', self.vault.process_one(self.client))
        self.clock += 30
        self.client.revoke = self.calls.append
        self.assertEqual('revoked', self.vault.process_one(self.client))
        self.assertEqual([], self.rows())

    def test_reopened_vault_resumes_pending_jobs(self):
        self.save(); self.queue()
        reopened = AppleGrantVault(self.store, self.key, 'com.test.game', clock=lambda: self.clock)
        self.assertEqual('revoked', reopened.process_one(self.client))

    def test_worker_crash_lease_expires_without_losing_token(self):
        self.save(); self.queue()
        def crash(token): raise RuntimeError('simulated process crash')
        self.client.revoke = crash
        with self.assertRaises(RuntimeError): self.vault.process_one(self.client)
        self.assertEqual('leased', self.rows()[0]['state'])
        self.assertEqual('idle', self.vault.process_one(self.client))
        self.clock += 121
        self.client.revoke = self.calls.append
        self.assertEqual('revoked', self.vault.process_one(self.client))

    def test_concurrent_workers_cannot_claim_same_live_lease(self):
        self.save(); self.queue()
        # Hold the provider call while another worker polls the queue.
        import threading
        entered, release = threading.Event(), threading.Event()
        def blocked(token):
            entered.set()
            self.assertTrue(release.wait(5))
            self.calls.append(token)
        self.client.revoke = blocked
        with concurrent.futures.ThreadPoolExecutor(2) as workers:
            first = workers.submit(self.vault.process_one, self.client)
            try:
                self.assertTrue(entered.wait(5))
                self.assertEqual('idle', self.vault.process_one(self.client))
            finally:
                release.set()
            self.assertEqual('revoked', first.result(timeout=5))
        self.assertEqual(1, len(self.calls))

    def test_stale_worker_cannot_delete_new_lease(self):
        self.save(); self.queue()
        def changed_lease(token):
            with self.store.connect() as db:
                db.execute("UPDATE apple_grants SET lease='new-worker'")
        self.client.revoke = changed_lease
        self.assertEqual('stale', self.vault.process_one(self.client))
        self.assertEqual(1, len(self.rows()))
        self.assertEqual('new-worker', self.rows()[0]['lease'])

    def test_wrong_key_and_tampered_ciphertext_fail_closed_and_retain_job(self):
        self.save(); self.queue()
        wrong = AppleGrantVault(self.store, secrets.token_bytes(32), 'com.test.game', clock=lambda: self.clock)
        self.assertEqual('retry', wrong.process_one(self.client))
        self.assertEqual('stored_token_unavailable', self.rows()[0]['last_error'])
        self.assertEqual([], self.calls)
        self.clock += 30
        with self.store.connect() as db:
            db.execute('UPDATE apple_grants SET encrypted_token=?', (b'corrupt',))
        self.assertEqual('retry', self.vault.process_one(self.client))
        self.assertEqual([], self.calls)
        self.assertEqual(1, len(self.rows()))

    def test_ciphertext_cannot_be_moved_to_another_subject_or_row(self):
        first, second = self.save(), self.save('other-user', 'other-refresh')
        with self.store.connect() as db:
            raw = db.execute('SELECT encrypted_token FROM apple_grants WHERE id=?', (first,)).fetchone()[0]
            db.execute('UPDATE apple_grants SET encrypted_token=? WHERE id=?', (raw, second))
        self.queue(self.vault.subject_hash('other-user'))
        self.assertEqual('retry', self.vault.process_one(self.client))
        self.assertEqual([], self.calls)

    def test_wrong_app_cannot_process_or_queue_grants(self):
        self.save(); self.queue()
        other = AppleGrantVault(self.store, self.key, 'other.app', clock=lambda: self.clock)
        self.assertEqual('idle', other.process_one(SimpleNamespace(client_id='other.app', revoke=self.calls.append)))
        with self.assertRaises(ValueError): other.process_one(self.client)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.assertEqual(0, other.queue(db, self.subject))
        self.assertEqual([], self.calls)

    def test_explicit_encryption_key_and_exact_subject_required(self):
        for key in (None, b'', b'x' * 16, 'x' * 32):
            with self.assertRaises(ValueError): AppleGrantVault(self.store, key, 'com.test.game')
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            for subject in (None, '', '*', 'a' * 63, 'z' * 64):
                with self.assertRaises(ValueError): self.vault.queue(db, subject)


if __name__ == '__main__': unittest.main()
