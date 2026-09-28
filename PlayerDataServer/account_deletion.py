"""Development-only, two-step deletion of live game data.

No production factory enables this dependency. Tombstones and receipt hashes are
minimal anti-replay records, NOT anonymous data or a backup-erasure guarantee.
Local-device erasure, backup retention/restore suppression, and the Apple outbox
worker must be completed before this feature may be released.
"""
import hashlib
import hmac
import json
import secrets
import time

from store import Fault, encode, now


class AccountDeletion:
    TTL = 300
    RETRY_TTL = 86400

    def __init__(self, store, *, account_linking=None, journal=None, clock=time.time, initialize=True):
        self.store, self.clock = store, clock
        self.linking = account_linking
        self.journal = journal
        if journal is not None and journal.store is not store:
            raise ValueError('Deletion journal must use the matching game database')
        if account_linking is not None and (account_linking.store is not store or account_linking.vault is None):
            raise ValueError('Deletion must use the matching Apple account service and revocation vault')
        with store.connect() as db:
            self._configuration(db)
            if not initialize:
                db.execute('PRAGMA query_only=ON')
                db.execute('SELECT confirmation_hash,player_hash,credential_hash,fingerprint,expires FROM deletion_confirmations LIMIT 0')
                db.execute('SELECT proof_hash,expires,result FROM deletion_receipts LIMIT 0')
                return
            db.executescript('''
            CREATE TABLE IF NOT EXISTS deletion_confirmations (
              confirmation_hash TEXT PRIMARY KEY, player_hash TEXT NOT NULL,
              credential_hash TEXT NOT NULL, fingerprint TEXT NOT NULL, expires REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS deletion_receipts (
              proof_hash TEXT PRIMARY KEY, expires REAL NOT NULL, result TEXT NOT NULL);
            ''')

    def _configuration(self, db):
        if self.journal is None and db.execute(
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='deletion_outbox'").fetchone():
            raise ValueError('Journal-enabled deletion cannot bypass offsite acknowledgement')
        # Never silently delete Apple links without the matching revocation
        # machinery. No Apple network call is needed to complete live deletion.
        if self.linking is None and db.execute(
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name='apple_links'").fetchone():
            raise ValueError('Apple-enabled databases require an explicit revocation dependency')

    @staticmethod
    def _digest(value):
        return hashlib.sha256(encode(value).encode()).hexdigest()

    @staticmethod
    def _input(player, token, confirmation=None):
        if not isinstance(player, str) or len(player) != 32 or any(c not in '0123456789abcdef' for c in player):
            raise Fault(401, 'Unauthorized')
        if not isinstance(token, str) or not 48 <= len(token) <= 128:
            raise Fault(401, 'Unauthorized')
        if confirmation is not None and (not isinstance(confirmation, str) or len(confirmation) != 64
                                        or any(c not in '0123456789abcdef' for c in confirmation)):
            raise Fault(409, 'Review and confirm game account deletion first')

    def _fingerprint(self, db, player):
        row = dict(self.store._player(db, player))
        snapshot = db.execute('SELECT revision,epoch,hash FROM snapshots WHERE player=? '
                              'ORDER BY epoch DESC,revision DESC LIMIT 1', (player,)).fetchone()
        links = [r[0] for r in db.execute('SELECT subject_hash FROM apple_links WHERE player=?', (player,))] if self.linking else []
        return self._digest([row, dict(snapshot) if snapshot else None, links])

    def preview(self, player, token):
        self._input(player, token)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self._configuration(db)
            self.store._authenticate(db, player, token)
            row = self.store._player(db, player)
            # Frozen/migration-required accounts may still request deletion.
            db.execute('DELETE FROM deletion_confirmations WHERE expires<=?', (self.clock(),))
            if self.journal:
                db.execute("DELETE FROM deletion_receipts WHERE expires<=? AND proof_hash NOT IN "
                           "(SELECT proof_hash FROM deletion_outbox WHERE state!='verified')", (self.clock(),))
            else:
                db.execute('DELETE FROM deletion_receipts WHERE expires<=?', (self.clock(),))
            if db.execute('SELECT count(*) FROM deletion_confirmations').fetchone()[0] >= 10000:
                raise Fault(429, 'Too many deletion confirmation requests')
            confirmation = secrets.token_hex(32)
            db.execute('INSERT INTO deletion_confirmations VALUES(?,?,?,?,?)',
                       (self._digest(confirmation), self.store.player_hash(player),
                        hashlib.sha256(token.encode()).hexdigest(), self._fingerprint(db, player), self.clock() + self.TTL))
            return dict(Status='confirm_delete', PlayerId=player, ConfirmationToken=confirmation,
                        ExpiresIn=self.TTL, Epoch=row['epoch'], EconomyRevision=row['economy_revision'],
                        Free=row['free'], Paid=row['paid'])

    def _proof(self, player, token, confirmation):
        self._input(player, token, confirmation)
        if confirmation is None:
            raise Fault(409, 'Review and confirm game account deletion first')
        return self._digest(['nasus.deletion-receipt.v1', player, token, confirmation])

    def _receipt(self, db, proof, player):
        self._configuration(db)
        row = db.execute('SELECT result,expires FROM deletion_receipts WHERE proof_hash=?', (proof,)).fetchone()
        if row and (row['expires'] > self.clock() or (self.journal and self.journal.pending(db, proof))):
            if self.journal:
                self.journal.require_ack(db, proof)
            return dict(json.loads(row['result']), PlayerId=player)
        return None

    def status(self, player, token, confirmation):
        """Read-only recovery after a lost response; never confirms a deletion."""
        proof = self._proof(player, token, confirmation)
        with self.store.connect() as db:
            db.execute('BEGIN')
            receipt = self._receipt(db, proof, player)
            if receipt:
                return receipt
            self.store._authenticate(db, player, token)
            return dict(Status='not_deleted', PlayerId=player)

    def _revoke_and_remove_links(self, db, player):
        if self.linking is None:
            return False, False
        vault = self.linking.vault
        # Include first-time link exchanges still waiting for Apple, recovery
        # exchanges, and previous unlink requests. Their tokens must not become
        # usable merely because their parent player/challenge was deleted.
        subjects = {r[0] for r in db.execute('SELECT subject_hash FROM apple_links WHERE player=? '
                    'UNION SELECT subject_hash FROM apple_unlinks WHERE player=? '
                    'UNION SELECT e.subject_hash FROM apple_exchanges e JOIN apple_challenges c '
                    'ON c.id=e.challenge WHERE c.player=?', (player, player, player))}
        pending = manual = False
        for subject in subjects:
            owner = db.execute('SELECT player FROM apple_links WHERE subject_hash=?', (subject,)).fetchone()
            if owner and owner['player'] != player:
                # An old unlink receipt is not authority to revoke another game
                # account that this Apple identity subsequently linked to.
                continue
            vault.queue(db, subject)
            has_pending = vault.pending(db, subject)
            unknown = db.execute("SELECT 1 FROM apple_exchanges WHERE subject_hash=? AND state IN ('requested','uncertain')",
                                 (subject,)).fetchone() is not None
            pending |= has_pending or unknown
            manual |= not has_pending or unknown
            db.execute('DELETE FROM apple_challenges WHERE id IN '
                       '(SELECT challenge FROM apple_exchanges WHERE subject_hash=?)', (subject,))
            # Preserve in-flight exchange fences and grant IDs needed for outbox
            # reconciliation. They contain no save, bearer token, code, or JWT.
        db.execute('DELETE FROM apple_challenges WHERE player=?', (player,))
        db.execute('DELETE FROM apple_unlinks WHERE player=?', (player,))
        db.execute('DELETE FROM apple_links WHERE player=?', (player,))
        return pending, manual

    def cancel(self, player, token, confirmation):
        """Serialize cancellation against a delayed commit; status alone cannot."""
        proof = self._proof(player, token, confirmation)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            receipt = self._receipt(db, proof, player)
            if receipt:
                return receipt
            self.store._authenticate(db, player, token)
            db.execute('DELETE FROM deletion_confirmations WHERE confirmation_hash=? AND player_hash=? '
                       'AND credential_hash=?', (self._digest(confirmation), self.store.player_hash(player),
                                                hashlib.sha256(token.encode()).hexdigest()))
            return dict(Status='cancelled', PlayerId=player)

    def commit(self, player, token, confirmation):
        proof = self._proof(player, token, confirmation)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self._configuration(db)
            receipt = self._receipt(db, proof, player)
            if receipt:
                return receipt
            self.store._authenticate(db, player, token)
            row = db.execute('SELECT * FROM deletion_confirmations WHERE confirmation_hash=?',
                             (self._digest(confirmation),)).fetchone()
            if (not row or row['player_hash'] != self.store.player_hash(player) or row['expires'] <= self.clock()
                    or not hmac.compare_digest(row['credential_hash'], hashlib.sha256(token.encode()).hexdigest())):
                raise Fault(409, 'Deletion confirmation expired or does not match')
            if not hmac.compare_digest(row['fingerprint'], self._fingerprint(db, player)):
                raise Fault(409, 'Game data changed; review a new deletion confirmation')
            pending, manual = self._revoke_and_remove_links(db, player)
            retired = []
            for purchase in db.execute('SELECT store,transaction_id FROM purchases WHERE player=?', (player,)).fetchall():
                transaction_hash = self.store.transaction_hash(purchase['store'], purchase['transaction_id'])
                retired.append(transaction_hash)
                db.execute('INSERT OR IGNORE INTO retired_purchases VALUES(?)', (transaction_hash,))
            # Audit may include old save contents/restore previews. Do not retain
            # it as a disguised undelete record. Only fixed, known tables below.
            for table in ('snapshots', 'operations', 'claims', 'flags', 'audit', 'purchases'):
                db.execute('DELETE FROM ' + table + ' WHERE player=?', (player,))
            db.execute('DELETE FROM players WHERE id=?', (player,))
            deleted_at = now()
            db.execute('INSERT INTO deleted_players VALUES(?,?)', (self.store.player_hash(player), deleted_at))
            db.execute('DELETE FROM deletion_confirmations WHERE player_hash=?', (self.store.player_hash(player),))
            result = dict(Status='deleted', RevocationPending=pending, ManualRevocationRequired=manual)
            # Keep only an opaque proof of the exact successful request for 24h;
            # neither the old bearer credential nor confirmation is recoverable.
            db.execute('INSERT INTO deletion_receipts VALUES(?,?,?)',
                       (proof, self.clock() + self.RETRY_TTL, encode(result)))
            if self.journal:
                self.journal.enqueue(db, proof, self.store.player_hash(player), deleted_at, retired)
        # Commit local erasure AND delivery obligation first. Never roll them
        # back merely because the offsite worker has not verified delivery yet.
        if self.journal:
            with self.store.connect() as db:
                self.journal.require_ack(db, proof)
        return dict(result, PlayerId=player)
