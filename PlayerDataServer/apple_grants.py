"""Encrypted grants and a durable revocation outbox; no automatic job or route.

Caller must authenticate/authorize before queueing. Queue in the SAME transaction
as unlink/deletion; do not wait for Apple before fulfilling account deletion.
This module alone does not implement account deletion or Apple notifications.
"""
import hashlib
import secrets
import time

from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

from apple_tokens import AppleGrant, AppleTokenClient
from store import Fault


class AppleGrantVault:
    LEASE_SECONDS = 120

    def __init__(self, store, encryption_key, client_id, *, clock=time.time, must_exist=False):
        if not isinstance(encryption_key, bytes) or len(encryption_key) != 32:
            raise ValueError('An explicitly provisioned 32-byte token-encryption key is required')
        if not isinstance(client_id, str) or not client_id or len(client_id) > 255:
            raise ValueError('An explicit Apple client ID is required')
        self.store, self.client_id, self.clock = store, client_id, clock
        self._cipher = AESGCM(encryption_key)
        if must_exist:
            with store.connect() as db:
                db.execute('PRAGMA query_only=ON')
                db.execute('SELECT id,subject_hash,client_id,encrypted_token,created,state,attempts,due,lease,last_error '
                           'FROM apple_grants LIMIT 0')
                db.execute('SELECT subject_hash,client_id,generation FROM apple_grant_generations LIMIT 0')
            return
        with store.connect() as db:
            db.executescript('''
            CREATE TABLE IF NOT EXISTS apple_grants (
              id TEXT PRIMARY KEY, subject_hash TEXT NOT NULL, client_id TEXT NOT NULL,
              encrypted_token BLOB NOT NULL, created REAL NOT NULL,
              state TEXT NOT NULL CHECK(state IN ('active','pending','leased')),
              attempts INTEGER NOT NULL DEFAULT 0, due REAL NOT NULL DEFAULT 0,
              lease TEXT, last_error TEXT);
            CREATE INDEX IF NOT EXISTS apple_grants_due ON apple_grants(client_id,state,due);
            CREATE INDEX IF NOT EXISTS apple_grants_subject ON apple_grants(subject_hash,client_id,state);
            CREATE TABLE IF NOT EXISTS apple_grant_generations (
              subject_hash TEXT NOT NULL, client_id TEXT NOT NULL, generation INTEGER NOT NULL,
              PRIMARY KEY(subject_hash,client_id));
            ''')

    def generation(self, db, subject_hash):
        row = db.execute('SELECT generation FROM apple_grant_generations WHERE subject_hash=? AND client_id=?',
                         (subject_hash, self.client_id)).fetchone()
        return row[0] if row else 0

    def pending(self, db, subject_hash):
        return db.execute("SELECT 1 FROM apple_grants WHERE subject_hash=? AND client_id=? AND state!='active' LIMIT 1",
                          (subject_hash, self.client_id)).fetchone() is not None

    @staticmethod
    def subject_hash(subject):
        if not isinstance(subject, str) or not 1 <= len(subject) <= 255:
            raise ValueError('A verified Apple subject is required')
        return hashlib.sha256(('https://appleid.apple.com\0' + subject).encode()).hexdigest()

    def _aad(self, grant_id, subject_hash):
        # Ciphertext cannot be moved between subjects, rows or app IDs.
        return ('nasus.apple.grant.v1\0' + self.client_id + '\0' + grant_id + '\0' + subject_hash).encode()

    @staticmethod
    def _transaction(db):
        if not db.in_transaction:
            raise ValueError('Caller must begin a transaction before changing Apple grants')

    def save(self, db, grant):
        self._transaction(db)
        if not isinstance(grant, AppleGrant) or not AppleTokenClient._secret(grant.refresh_token):
            raise ValueError('A verified Apple grant is required')
        subject_hash = self.subject_hash(grant.subject)
        grant_id, nonce = secrets.token_hex(16), secrets.token_bytes(12)
        encrypted = nonce + self._cipher.encrypt(nonce, grant.refresh_token.encode(), self._aad(grant_id, subject_hash))
        # Keep older grants too: overwriting them would lose the ability to revoke
        # authorizations issued by earlier sign-ins. No plaintext dedup hashes.
        db.execute('INSERT INTO apple_grants(id,subject_hash,client_id,encrypted_token,created,state) '
                   'VALUES(?,?,?,?,?,?)', (grant_id, subject_hash, self.client_id, encrypted, self.clock(), 'active'))
        return grant_id

    def queue(self, db, subject_hash):
        self._transaction(db)
        if (not isinstance(subject_hash, str) or len(subject_hash) != 64
                or any(c not in '0123456789abcdef' for c in subject_hash)):
            raise ValueError('An exact subject hash is required')
        # Fence exchanges already in flight even when there is no saved token yet.
        db.execute('INSERT INTO apple_grant_generations VALUES(?,?,1) ON CONFLICT(subject_hash,client_id) '
                   'DO UPDATE SET generation=generation+1', (subject_hash, self.client_id))
        return db.execute("UPDATE apple_grants SET state='pending',due=? WHERE subject_hash=? "
                          "AND client_id=? AND state='active'", (self.clock(), subject_hash, self.client_id)).rowcount

    def process_one(self, client):
        if getattr(client, 'client_id', None) != self.client_id:
            raise ValueError('Revocation client must match the stored app ID')
        lease = secrets.token_hex(16)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute("SELECT * FROM apple_grants WHERE client_id=? AND state IN ('pending','leased') "
                             'AND due<=? ORDER BY due,id LIMIT 1', (self.client_id, self.clock())).fetchone()
            if row is None:
                return 'idle'
            db.execute("UPDATE apple_grants SET state='leased',lease=?,due=?,attempts=attempts+1 WHERE id=?",
                       (lease, self.clock() + self.LEASE_SECONDS, row['id']))
        # No database write lock while making an external request. Leases make
        # worker crashes retryable; a stale worker must not erase a newer lease.
        failed = None
        try:
            encrypted = bytes(row['encrypted_token'])
            token = self._cipher.decrypt(encrypted[:12], encrypted[12:],
                                         self._aad(row['id'], row['subject_hash'])).decode('ascii')
            try:
                client.revoke(token)
            finally:
                token = None
        except (InvalidTag, UnicodeError, ValueError, TypeError):
            failed = 'stored_token_unavailable'
        except (Fault, OSError):
            failed = 'provider_unavailable'
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if failed:
                delay = min(3600, 30 * 2 ** min(row['attempts'], 7))
                changed = db.execute("UPDATE apple_grants SET state='pending',lease=NULL,last_error=?,due=? "
                                     "WHERE id=? AND state='leased' AND lease=?",
                                     (failed, self.clock() + delay, row['id'], lease)).rowcount
            else:
                # Forget the encrypted credential only after confirmed 200.
                # SQLite/WAL and historical backup erasure need a separate policy.
                changed = db.execute("DELETE FROM apple_grants WHERE id=? AND state='leased' AND lease=?",
                                     (row['id'], lease)).rowcount
        return ('retry' if failed else 'revoked') if changed else 'stale'
