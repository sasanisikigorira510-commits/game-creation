"""Development-only transactional deletion outbox with verified offsite acknowledgement.

No production factory or scheduler enables this module. A verified upload is not
an immutable-storage guarantee or proof of a complete disaster-recovery inventory.
"""
import datetime as dt
import hashlib
import json
import re
import secrets
import time
import uuid
from pathlib import Path

from store import Fault, encode

MAX_PAYLOAD = 1024 * 1024
MAX_CIPHERTEXT = MAX_PAYLOAD + 65536
HEX = re.compile(r'[a-f0-9]{64}')
IDENTITY = re.compile(r'[A-Za-z0-9_-]{8,80}')


def validate_event(payload, instance):
    if not isinstance(payload, bytes) or len(payload) > MAX_PAYLOAD:
        raise ValueError('Invalid deletion event size')
    data = json.loads(payload)
    if (not isinstance(data, dict) or set(data) != {'Version', 'InstanceId', 'EventId',
            'DeletedUtc', 'PlayerHash', 'RetiredPurchases'} or type(data['Version']) is not int
            or data['Version'] != 1 or data['InstanceId'] != instance
            or not isinstance(instance, str) or not IDENTITY.fullmatch(instance)
            or not isinstance(data['EventId'], str) or not re.fullmatch(r'[a-f0-9]{32}', data['EventId'])
            or not isinstance(data['PlayerHash'], str) or not HEX.fullmatch(data['PlayerHash'])):
        raise ValueError('Invalid deletion event identity or schema')
    try:
        if dt.datetime.fromisoformat(data['DeletedUtc']).tzinfo is None:
            raise ValueError()
    except (TypeError, ValueError):
        raise ValueError('Invalid deletion event timestamp') from None
    retired = data['RetiredPurchases']
    if (not isinstance(retired, list) or len(retired) > 10000
            or any(not isinstance(h, str) or not HEX.fullmatch(h) for h in retired)
            or retired != sorted(set(retired)) or (encode(data) + '\n').encode() != payload):
        raise ValueError('Invalid deletion event encoding or purchase hashes')
    return data


class DeletionJournal:
    LEASE_SECONDS = 120

    def __init__(self, store, instance, *, clock=time.time, initialize=True):
        if not isinstance(instance, str) or not IDENTITY.fullmatch(instance):
            raise ValueError('An explicit deletion journal instance is required')
        directory = Path(store.path).resolve().parent
        marker = directory / 'instance-id'
        if marker.is_symlink() or not marker.is_file() or marker.read_text().strip() != instance:
            raise ValueError('Deletion journal must match the provisioned database instance')
        quarantine = directory / 'RECOVERY-PENDING.txt'
        if quarantine.exists() or quarantine.is_symlink():
            raise ValueError('Never publish deletion events from a recovery candidate')
        self.store, self.instance, self.clock = store, instance, clock
        with store.connect() as db:
            if not initialize:
                if [tuple(r) for r in db.execute('SELECT id,instance FROM deletion_journal_meta')] != [(1, instance)]:
                    raise ValueError('Deletion journal instance mismatch')
                db.execute('SELECT event_id,proof_hash,payload,ciphertext,state,lease,lease_until,attempts,next_attempt,verified_at,last_error FROM deletion_outbox LIMIT 0')
                db.execute('SELECT proof_hash,expires,result FROM deletion_receipts LIMIT 0')
                return
            db.execute('BEGIN IMMEDIATE')
            exists = db.execute("SELECT 1 FROM sqlite_master WHERE name='deletion_journal_meta'").fetchone()
            if not exists and db.execute('SELECT 1 FROM deleted_players LIMIT 1').fetchone():
                raise ValueError('Historical deletions require reviewed offsite bootstrap before journal activation')
            db.execute('CREATE TABLE IF NOT EXISTS deletion_journal_meta (id INTEGER PRIMARY KEY CHECK(id=1), instance TEXT NOT NULL)')
            db.execute('INSERT OR IGNORE INTO deletion_journal_meta VALUES(1,?)', (instance,))
            if db.execute('SELECT instance FROM deletion_journal_meta WHERE id=1').fetchone()[0] != instance:
                raise ValueError('Deletion journal instance mismatch')
            db.execute('''CREATE TABLE IF NOT EXISTS deletion_outbox (
                event_id TEXT PRIMARY KEY, proof_hash TEXT NOT NULL UNIQUE, payload TEXT NOT NULL,
                ciphertext BLOB, state TEXT NOT NULL CHECK(state IN ('pending','sending','verified')),
                lease TEXT, lease_until REAL NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 0,
                next_attempt REAL NOT NULL DEFAULT 0, verified_at REAL, last_error TEXT)''')

    def enqueue(self, db, proof, player_hash, deleted_at, retired):
        if not db.in_transaction or not isinstance(proof, str) or not HEX.fullmatch(proof):
            raise ValueError('Deletion outbox requires the caller transaction and exact request proof')
        data = dict(Version=1, InstanceId=self.instance, EventId=uuid.uuid4().hex,
                    PlayerHash=player_hash, DeletedUtc=deleted_at, RetiredPurchases=sorted(set(retired)))
        payload = encode(data) + '\n'
        validate_event(payload.encode(), self.instance)
        db.execute("INSERT INTO deletion_outbox(event_id,proof_hash,payload,state) VALUES(?,?,?,'pending')",
                   (data['EventId'], proof, payload))

    @staticmethod
    def pending(db, proof):
        return db.execute("SELECT 1 FROM deletion_outbox WHERE proof_hash=? AND state!='verified'", (proof,)).fetchone() is not None

    @staticmethod
    def require_ack(db, proof):
        row = db.execute('SELECT state,verified_at FROM deletion_outbox WHERE proof_hash=?', (proof,)).fetchone()
        if not row or row['state'] != 'verified' or row['verified_at'] is None:
            raise Fault(503, 'Deletion is awaiting verified offsite storage; retry status later')

    def process_one(self, encrypt, publish):
        """Local encryption + leased PUT/readback. Callbacks must not log secrets.

        Encrypt once and persist the exact ciphertext before any upload. Retries
        use identical bytes/object name even after lost responses or worker death.
        Encryption is bounded local work; no network under the database lock.
        """
        ticket = self.claim(encrypt)
        if ticket is None:
            return 'idle'
        verified = False
        try:
            verified = publish(ticket['key'], ticket['ciphertext']) == hashlib.sha256(ticket['ciphertext']).hexdigest()
        except (OSError, RuntimeError, ValueError, TimeoutError):
            pass
        return self.finish(ticket['event_id'], ticket['lease'], verified)

    def claim(self, encrypt):
        """Database-user half: durable ciphertext and lease before external I/O."""
        lease = secrets.token_hex(16)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute("SELECT * FROM deletion_outbox WHERE (state='pending' AND next_attempt<=?) "
                "OR (state='sending' AND lease_until<=?) ORDER BY rowid LIMIT 1", (self.clock(), self.clock())).fetchone()
            if row is None:
                return None
            data = validate_event(row['payload'].encode(), self.instance)
            if data['EventId'] != row['event_id']:
                raise ValueError('Outbox event identity mismatch')
            ciphertext = row['ciphertext']
            if ciphertext is None:
                ciphertext = encrypt(row['payload'].encode())
            if (not isinstance(ciphertext, bytes) or not ciphertext.startswith(b'age-encryption.org/v1\n')
                    or not 64 <= len(ciphertext) <= MAX_CIPHERTEXT):
                raise ValueError('Expected bounded age ciphertext, never plaintext')
            db.execute("UPDATE deletion_outbox SET ciphertext=?,state='sending',lease=?,lease_until=?,"
                       'attempts=attempts+1 WHERE event_id=?',
                       (ciphertext, lease, self.clock() + self.LEASE_SECONDS, row['event_id']))
        key = 'deletions/' + self.instance + '/' + row['event_id'] + '.json.age'
        return dict(event_id=row['event_id'], lease=lease, key=key, ciphertext=ciphertext)

    def finish(self, event_id, lease, verified):
        """Database-user half: acknowledge only the exact still-owned lease."""
        if type(verified) is not bool:
            raise ValueError('Explicit verification result required')
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute("SELECT proof_hash,attempts FROM deletion_outbox WHERE event_id=? AND state='sending' AND lease=?",
                             (event_id, lease)).fetchone()
            if row is None:
                return 'stale'
            if verified:
                changed = db.execute("UPDATE deletion_outbox SET state='verified',verified_at=?,lease=NULL,"
                    "lease_until=0,last_error=NULL WHERE event_id=? AND state='sending' AND lease=?",
                    (self.clock(), event_id, lease)).rowcount
                if changed:
                    # Receipt lifetime starts no earlier than delivery completion.
                    db.execute('UPDATE deletion_receipts SET expires=MAX(expires,?) WHERE proof_hash=?',
                               (self.clock() + 86400, row['proof_hash']))
            else:
                delay = min(3600, 30 * 2 ** min(max(0, row['attempts'] - 1), 7))
                changed = db.execute("UPDATE deletion_outbox SET state='pending',lease=NULL,lease_until=0,"
                    "next_attempt=?,last_error='offsite_unverified' WHERE event_id=? AND state='sending' AND lease=?",
                    (self.clock() + delay, event_id, lease)).rowcount
        return ('verified' if verified else 'retry') if changed else 'stale'

    def health(self):
        with self.store.connect() as db:
            rows = db.execute("SELECT state,count(*) FROM deletion_outbox GROUP BY state").fetchall()
        counts = dict(rows)
        return dict(Pending=counts.get('pending', 0), Sending=counts.get('sending', 0),
                    Verified=counts.get('verified', 0))
