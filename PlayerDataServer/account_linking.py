"""Opt-in Apple linking and two-step, credential-fenced account transfer.

Not constructed by the production factory until native recovery, revocation and
real-device tests pass. The verifier is a server dependency, never a request field.
"""
import hashlib
import hmac
import json
import secrets
import time

from store import Fault, encode, now


class AccountLinking:
    TTL = 300
    RETRY_TTL = 86400
    MAX_TAIL = 1000

    def __init__(self, store, verifier, clock=time.time, *, tokens=None, vault=None, initialize=True):
        if not callable(verifier):
            raise ValueError('A verified Apple identity adapter is required')
        self.store, self.verifier, self.clock = store, verifier, clock
        if (tokens is None) != (vault is None):
            raise ValueError('Token exchange and encrypted storage must be configured together')
        if tokens is not None and (tokens.client_id != vault.client_id or vault.store is not store
                                   or getattr(verifier, 'audience', None) != tokens.client_id):
            raise ValueError('Apple identity, token client, vault and database must match')
        self.tokens, self.vault = tokens, vault
        with store.connect() as db:
            if not initialize:
                db.execute('PRAGMA query_only=ON')
                for query in (
                    'SELECT subject_hash,player,created FROM apple_links LIMIT 0',
                    'SELECT id,nonce,mode,player,credential_hash,expires,result,fingerprint FROM apple_challenges LIMIT 0',
                    'SELECT challenge,subject_hash,generation,state,grant_id,created FROM apple_exchanges LIMIT 0',
                    'SELECT id,player,credential_hash,subject_hash,expires,result FROM apple_unlinks LIMIT 0'):
                    db.execute(query)
                return
            db.executescript('''
            CREATE TABLE IF NOT EXISTS apple_links (
              subject_hash TEXT PRIMARY KEY, player TEXT NOT NULL UNIQUE REFERENCES players(id),
              created TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS apple_challenges (
              id TEXT PRIMARY KEY, nonce TEXT NOT NULL, mode TEXT NOT NULL,
              player TEXT REFERENCES players(id), credential_hash TEXT NOT NULL,
              expires REAL NOT NULL, result TEXT, fingerprint TEXT);
            CREATE TABLE IF NOT EXISTS apple_exchanges (
              challenge TEXT PRIMARY KEY, subject_hash TEXT NOT NULL, generation INTEGER NOT NULL,
              state TEXT NOT NULL CHECK(state IN ('requested','uncertain','failed','stored','attached')),
              grant_id TEXT, created REAL NOT NULL);
            CREATE INDEX IF NOT EXISTS apple_exchanges_subject ON apple_exchanges(subject_hash,state);
            CREATE TABLE IF NOT EXISTS apple_unlinks (
              id TEXT PRIMARY KEY, player TEXT NOT NULL REFERENCES players(id),
              credential_hash TEXT NOT NULL, subject_hash TEXT NOT NULL,
              expires REAL NOT NULL, result TEXT);
            ''')

    @staticmethod
    def credential_hash(token):
        if not isinstance(token, str) or not 48 <= len(token) <= 128 or not token.isascii():
            raise Fault(401, 'Invalid session credential')
        return hashlib.sha256(token.encode()).hexdigest()

    def _challenge(self, db, challenge, token):
        if not isinstance(challenge, str) or len(challenge) != 64:
            raise Fault(401, 'Invalid or expired sign-in session')
        row = db.execute('SELECT * FROM apple_challenges WHERE id=?', (challenge,)).fetchone()
        if (not row or row['expires'] <= self.clock() or
                not hmac.compare_digest(row['credential_hash'], self.credential_hash(token))):
            raise Fault(401, 'Invalid or expired sign-in session')
        if row['mode'] == 'link':
            self.store._authenticate(db, row['player'], token)
        return row

    def start(self, mode, token, player=None):
        if mode not in ('link', 'recover'):
            raise Fault(400, 'Invalid sign-in purpose')
        hashed = self.credential_hash(token)
        # New recovery credentials are generated and durably journaled by the
        # device BEFORE this request. This secret is stored only as a hash; an
        # optional vault stores Apple refresh tokens encrypted, never the JWT/code.
        if mode == 'recover' and (len(token) != 64 or any(c not in '0123456789abcdef' for c in token)):
            raise Fault(400, 'A new random 256-bit recovery credential is required')
        if mode == 'recover' and player is not None:
            raise Fault(400, 'Recovery identity is determined only by Apple')
        challenge, nonce = secrets.token_hex(32), secrets.token_hex(32)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if mode == 'link':
                self.store._authenticate(db, player, token)
                self._available(self.store._player(db, player))
            # Preserve the player association for unresolved external exchanges.
            # Expiry still prevents use, but deletion must be able to fence a
            # late token even before the first Apple link was committed.
            db.execute("DELETE FROM apple_challenges WHERE expires<=? AND id NOT IN "
                       "(SELECT challenge FROM apple_exchanges WHERE state IN ('requested','uncertain','stored'))",
                       (self.clock(),))
            if db.execute('SELECT count(*) FROM apple_challenges').fetchone()[0] >= 10000:
                raise Fault(429, 'Too many sign-in sessions; retry later')
            db.execute('INSERT INTO apple_challenges VALUES(?,?,?,?,?,?,NULL,NULL)',
                       (challenge, nonce, mode, player, hashed, self.clock() + self.TTL))
        return dict(ChallengeId=challenge, Nonce=nonce, ExpiresIn=self.TTL)

    @staticmethod
    def _available(player):
        if player['frozen'] or player['migration_required']:
            raise Fault(423, 'Account requires operator review')

    def _bundle(self, db, player):
        self._available(player)
        snapshot = db.execute('SELECT data FROM snapshots WHERE player=? AND epoch=? '
                              'ORDER BY revision DESC LIMIT 1', (player['id'], player['epoch'])).fetchone()
        if not snapshot:
            raise Fault(409, 'A synchronized cloud save is required')
        save = json.loads(snapshot['data'])
        cursor = save.get('EconomyRevision')
        if (save.get('PlayerId') != player['id'] or save.get('RecoveryEpoch') != player['epoch']
                or type(cursor) is not int or not 0 <= cursor <= player['economy_revision']):
            raise Fault(409, 'Cloud save requires operator review')
        operations = [json.loads(r['response']) for r in db.execute(
            'SELECT response FROM operations WHERE player=? AND revision>? ORDER BY revision LIMIT ?',
            (player['id'], cursor, self.MAX_TAIL + 1))]
        if (len(operations) > self.MAX_TAIL or len(operations) != player['economy_revision'] - cursor or
                any(op['Revision'] != cursor + i + 1 for i, op in enumerate(operations))):
            raise Fault(409, 'Cloud save must be synchronized before transfer')
        free, paid = (operations[-1]['Free'], operations[-1]['Paid']) if operations else (
            save.get('FreeGachaStones'), save.get('PaidGachaStones'))
        if free != player['free'] or paid != player['paid']:
            raise Fault(409, 'Cloud wallet requires operator review')
        # Financial history is not changed. Client reuses OnlineGrantApplier to
        # replay this exact tail onto the snapshot, never re-rolls or re-purchases.
        save['RecoveryEpoch'] = player['epoch'] + 1
        return dict(PlayerId=player['id'], Epoch=player['epoch'] + 1,
                    EconomyRevision=player['economy_revision'], Free=free, Paid=paid,
                    Snapshot=save, Operations=operations)

    def _target(self, db, row, subject_hash):
        link = db.execute('SELECT player FROM apple_links WHERE subject_hash=?', (subject_hash,)).fetchone()
        if row['mode'] == 'link':
            existing = db.execute('SELECT subject_hash FROM apple_links WHERE player=?', (row['player'],)).fetchone()
            if (link and link['player'] != row['player']) or (existing and existing['subject_hash'] != subject_hash):
                raise Fault(409, 'Already linked to another account; accounts cannot be merged')
            player = self.store._player(db, row['player'])
        else:
            if not link:
                raise Fault(404, 'No linked game data; no account was created')
            player = self.store._player(db, link['player'])
            if hmac.compare_digest(player['token_hash'], row['credential_hash']):
                raise Fault(409, 'Recovery requires a new installation credential')
        self._bundle(db, player)
        return player

    def _active_exchange(self, db, challenge):
        exchange = db.execute('SELECT * FROM apple_exchanges WHERE challenge=?', (challenge,)).fetchone()
        if not exchange or exchange['state'] not in ('stored', 'attached'):
            if exchange and exchange['state'] == 'failed':
                raise Fault(401, 'Apple authorization expired; start a new sign-in')
            raise Fault(409, 'Apple authorization result is pending or requires operator review')
        grant = db.execute("SELECT 1 FROM apple_grants WHERE id=? AND subject_hash=? AND client_id=? AND state='active'",
                           (exchange['grant_id'], exchange['subject_hash'], self.vault.client_id)).fetchone()
        if (not grant or self.vault.pending(db, exchange['subject_hash'])
                or self.vault.generation(db, exchange['subject_hash']) != exchange['generation']):
            raise Fault(409, 'Apple authorization has been invalidated; sign in again after revocation')
        return exchange['subject_hash']

    def _exchange(self, challenge, token, identity_token, authorization_code):
        with self.store.connect() as db:
            row = self._challenge(db, challenge, token)
            if db.execute('SELECT 1 FROM apple_exchanges WHERE challenge=?', (challenge,)).fetchone():
                return self._active_exchange(db, challenge)
            nonce = row['nonce']
        # A result-only retry never initiates a new external exchange.
        if (not isinstance(authorization_code, str) or not 1 <= len(authorization_code) <= 16384
                or not authorization_code.isascii() or any(not 33 <= ord(c) <= 126 for c in authorization_code)):
            raise Fault(400, 'A fresh Apple authorization code is required')
        subject = self.verifier(identity_token, nonce)
        subject_hash = self.vault.subject_hash(subject)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = self._challenge(db, challenge, token)
            if db.execute('SELECT 1 FROM apple_exchanges WHERE challenge=?', (challenge,)).fetchone():
                return self._active_exchange(db, challenge)
            self._target(db, row, subject_hash)  # No code consumption for known account conflicts.
            if self.vault.pending(db, subject_hash):
                raise Fault(409, 'Apple revocation must complete before signing in again')
            if db.execute("SELECT 1 FROM apple_exchanges WHERE subject_hash=? AND state IN ('requested','uncertain')",
                          (subject_hash,)).fetchone():
                raise Fault(409, 'An earlier Apple authorization is pending or requires operator review')
            generation = self.vault.generation(db, subject_hash)
            db.execute("INSERT INTO apple_exchanges VALUES(?,?,?,'requested',NULL,?)",
                       (challenge, subject_hash, generation, self.clock()))
        try:
            grant = self.tokens.exchange(authorization_code, identity_token, nonce)
            if self.vault.subject_hash(grant.subject) != subject_hash:
                raise Fault(401, 'Apple authorization identity does not match')
            with self.store.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                # Save even if the challenge expired during the request. Otherwise
                # a returned token would be lost and could no longer be revoked.
                grant_id = self.vault.save(db, grant)
                if self.vault.generation(db, subject_hash) != generation:
                    db.execute("UPDATE apple_grants SET state='pending',due=? WHERE id=?", (self.clock(), grant_id))
                db.execute("UPDATE apple_exchanges SET state='stored',grant_id=? WHERE challenge=?",
                           (grant_id, challenge))
        except Exception as error:
            from apple_tokens import AppleCodeRejected
            # Do not resend single-use codes after an ambiguous failure. This
            # marker also survives restart and blocks a conflicting new sign-in.
            # Deliberately no exception text, identity JWT, code or raw subject.
            with self.store.connect() as db:
                state = 'failed' if isinstance(error, AppleCodeRejected) else 'uncertain'
                db.execute("UPDATE apple_exchanges SET state=? WHERE challenge=? AND state='requested'", (state, challenge))
            raise
        with self.store.connect() as db:
            self._challenge(db, challenge, token)
            return self._active_exchange(db, challenge)

    def verify(self, challenge, token, identity_token, authorization_code=None):
        with self.store.connect() as db:
            row = self._challenge(db, challenge, token)
            if row['result']:
                return self._retry(db, row, token)
            nonce = row['nonce']
        # Signature/Apple key retrieval happens outside the SQLite write lock.
        if self.tokens is not None:
            subject_hash = self._exchange(challenge, token, identity_token, authorization_code)
        else:
            # Signature-only mode is retained solely for existing isolated tests.
            # production.create_app never constructs this development feature.
            subject = self.verifier(identity_token, nonce)
            if not isinstance(subject, str) or not 1 <= len(subject) <= 255:
                raise Fault(401, 'Invalid Apple identity')
            subject_hash = hashlib.sha256(('https://appleid.apple.com\0' + subject).encode()).hexdigest()
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = self._challenge(db, challenge, token)
            if row['result']:
                return self._retry(db, row, token)
            if self.tokens is not None:
                self._active_exchange(db, challenge)
            player = self._target(db, row, subject_hash)
            if row['mode'] == 'link':
                # Linking only after a recoverable checkpoint exists avoids a false
                # "protected" status on an account with no cloud save.
                db.execute('INSERT OR IGNORE INTO apple_links VALUES(?,?,?)', (subject_hash, row['player'], now()))
                result = dict(Status='linked', PlayerId=row['player'])
                self.store._audit(db, row['player'], 'apple_identity', 'apple_link', 'User confirmed linking', {})
                fingerprint = None
            else:
                result = self._bundle(db, player)
                fingerprint = hashlib.sha256(encode(result).encode()).hexdigest()
                result.update(Status='preview', PreviewToken=secrets.token_hex(32))
                db.execute('UPDATE apple_challenges SET player=? WHERE id=?', (player['id'], challenge))
            db.execute('UPDATE apple_challenges SET result=?,fingerprint=? WHERE id=?',
                       (encode(result), fingerprint, challenge))
            if self.tokens is not None:
                db.execute("UPDATE apple_exchanges SET state='attached' WHERE challenge=?", (challenge,))
            return result

    def _retry(self, db, row, token):
        result = json.loads(row['result'])
        if self.tokens is not None and result['Status'] != 'committed':
            self._active_exchange(db, row['id'])
        if result['Status'] == 'committed':
            self.store._authenticate(db, row['player'], token)
            self.store._player(db, row['player'], result['Epoch'])
        return result

    def unlink_preview(self, player, token):
        if self.vault is None:
            raise Fault(503, 'Apple revocation is not configured')
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.store._authenticate(db, player, token)
            link = db.execute('SELECT subject_hash FROM apple_links WHERE player=?', (player,)).fetchone()
            if not link:
                return dict(Status='not_linked', PlayerId=player)
            db.execute('DELETE FROM apple_unlinks WHERE expires<=?', (self.clock(),))
            if db.execute('SELECT count(*) FROM apple_unlinks').fetchone()[0] >= 10000:
                raise Fault(429, 'Too many confirmation requests')
            confirmation = secrets.token_hex(32)
            db.execute('INSERT INTO apple_unlinks VALUES(?,?,?,?,?,NULL)',
                       (confirmation, player, self.credential_hash(token), link['subject_hash'], self.clock() + self.TTL))
            return dict(Status='confirm_unlink', PlayerId=player, ConfirmationToken=confirmation, ExpiresIn=self.TTL)

    def unlink_commit(self, player, token, confirmation):
        if self.vault is None:
            raise Fault(503, 'Apple revocation is not configured')
        if not isinstance(confirmation, str) or len(confirmation) != 64:
            raise Fault(409, 'Review and confirm Apple unlinking first')
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.store._authenticate(db, player, token)
            row = db.execute('SELECT * FROM apple_unlinks WHERE id=?', (confirmation,)).fetchone()
            if (not row or row['player'] != player or row['expires'] <= self.clock()
                    or not hmac.compare_digest(row['credential_hash'], self.credential_hash(token))):
                raise Fault(409, 'Apple unlink confirmation expired or does not match')
            link = db.execute('SELECT subject_hash FROM apple_links WHERE player=?', (player,)).fetchone()
            if row['result']:
                if link:
                    raise Fault(409, 'Account has been linked again; review a new confirmation')
                return json.loads(row['result'])
            if not link or link['subject_hash'] != row['subject_hash']:
                raise Fault(409, 'Account link changed; review a new confirmation')
            # Immediate removal of the recovery link and pending challenges; the
            # guest credential, game saves and financial ledger stay unchanged.
            # queue also fences token exchanges that are still in flight.
            self.vault.queue(db, row['subject_hash'])
            db.execute('DELETE FROM apple_links WHERE player=?', (player,))
            db.execute('DELETE FROM apple_challenges WHERE player=? OR id IN '
                       '(SELECT challenge FROM apple_exchanges WHERE subject_hash=?)', (player, row['subject_hash']))
            pending = self.vault.pending(db, row['subject_hash'])
            uncertain = db.execute("SELECT 1 FROM apple_exchanges WHERE subject_hash=? AND state IN ('requested','uncertain')",
                                   (row['subject_hash'],)).fetchone() is not None
            result = dict(Status='unlinked', PlayerId=player, RevocationPending=pending or uncertain,
                          ManualRevocationRequired=not pending or uncertain)
            db.execute('UPDATE apple_unlinks SET result=?,expires=? WHERE id=?',
                       (encode(result), self.clock() + self.RETRY_TTL, confirmation))
            self.store._audit(db, player, 'self_service', 'apple_unlink', 'User confirmed unlinking', {})
            return result

    def commit(self, challenge, token, preview_token):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = self._challenge(db, challenge, token)
            if row['mode'] != 'recover' or not row['result']:
                raise Fault(409, 'Verify and confirm a recovery preview first')
            result = json.loads(row['result'])
            if (not isinstance(preview_token, str) or not preview_token.isascii() or
                    not hmac.compare_digest(result['PreviewToken'], preview_token)):
                raise Fault(409, 'Recovery confirmation does not match')
            if result['Status'] == 'committed':
                return self._retry(db, row, token)
            if self.tokens is not None:
                subject_hash = self._active_exchange(db, challenge)
                link = db.execute('SELECT player FROM apple_links WHERE subject_hash=?', (subject_hash,)).fetchone()
                if not link or link['player'] != row['player']:
                    raise Fault(409, 'Apple account link changed; recovery cannot be committed')
            player = self.store._player(db, row['player'])
            current = self._bundle(db, player)
            if hashlib.sha256(encode(current).encode()).hexdigest() != row['fingerprint']:
                raise Fault(409, 'Game data changed; sign in again to review the latest save')
            # One transaction rotates the secret, fences old epochs and retains a
            # baseline in the new epoch. A lost response is safely retried for 24h.
            db.execute('UPDATE players SET token_hash=?,epoch=?,recovery=NULL WHERE id=?',
                       (row['credential_hash'], result['Epoch'], row['player']))
            raw = encode(result['Snapshot'])
            db.execute('INSERT INTO snapshots(player,revision,epoch,received,data,hash,source) VALUES(?,?,?,?,?,?,?)',
                       (row['player'], result['Snapshot']['SaveRevision'], result['Epoch'], now(), raw,
                        hashlib.sha256(raw.encode()).hexdigest(), 'apple_transfer_client_unverified'))
            result['Status'] = 'committed'
            db.execute('UPDATE apple_challenges SET result=?,expires=? WHERE id=?',
                       (encode(result), self.clock() + self.RETRY_TTL, challenge))
            self.store._audit(db, row['player'], 'apple_identity', 'apple_transfer', 'User confirmed recovery',
                              dict(Epoch=result['Epoch'], EconomyRevision=result['EconomyRevision']))
            return result
