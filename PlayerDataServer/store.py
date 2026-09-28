"""Transactional player operations. Client snapshots remain explicitly unverified."""
import datetime as dt
import hashlib
import hmac
import json
import secrets
import sqlite3
import uuid
from pathlib import Path


def encode(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False)


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


class Fault(Exception):
    def __init__(self, status, message):
        self.status, self.message = status, message
        super().__init__(message)


class StoreConnection(sqlite3.Connection):
    def __exit__(self, *args):
        try:
            return super().__exit__(*args)
        finally:
            self.close()


class Store:
    # ExistingStore deliberately bypasses __init__ to avoid schema changes.
    # Such legacy runtimes remain disabled and must never ignore a refund debt.
    refunds_enabled = False

    def __init__(self, path, catalog=None, purchase_verifier=None, must_exist=False, refunds_enabled=False):
        self.path = str(path)
        self.must_exist = must_exist
        self.catalog = catalog or []
        self.purchase_verifier = purchase_verifier
        self.refunds_enabled = refunds_enabled
        with self.connect() as db:
            if not refunds_enabled and self.has_refund_schema(db):
                raise RuntimeError('Refund-enabled database requires a refund-aware runtime')
            db.executescript('''
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS players (
              id TEXT PRIMARY KEY, token_hash TEXT NOT NULL, created TEXT NOT NULL,
              free INTEGER NOT NULL DEFAULT 900, paid INTEGER NOT NULL DEFAULT 0,
              economy_revision INTEGER NOT NULL DEFAULT 0, epoch INTEGER NOT NULL DEFAULT 0,
              frozen INTEGER NOT NULL DEFAULT 0, migration_required INTEGER NOT NULL DEFAULT 0,
              tutorial_pulls INTEGER NOT NULL DEFAULT 0, recovery TEXT);
            CREATE TABLE IF NOT EXISTS snapshots (
              id INTEGER PRIMARY KEY, player TEXT NOT NULL REFERENCES players(id), revision INTEGER NOT NULL,
              epoch INTEGER NOT NULL, received TEXT NOT NULL, data TEXT NOT NULL, hash TEXT NOT NULL,
              source TEXT NOT NULL DEFAULT 'client_unverified', UNIQUE(player,epoch,revision));
            CREATE TABLE IF NOT EXISTS operations (
              player TEXT NOT NULL REFERENCES players(id), request_id TEXT NOT NULL, request_hash TEXT NOT NULL,
              revision INTEGER NOT NULL, kind TEXT NOT NULL, received TEXT NOT NULL, response TEXT NOT NULL,
              PRIMARY KEY(player,request_id), UNIQUE(player,revision));
            CREATE TABLE IF NOT EXISTS claims (
              player TEXT NOT NULL REFERENCES players(id), claim_key TEXT NOT NULL,
              PRIMARY KEY(player,claim_key));
            CREATE TABLE IF NOT EXISTS purchases (
              store TEXT NOT NULL, transaction_id TEXT NOT NULL, player TEXT NOT NULL REFERENCES players(id),
              product TEXT NOT NULL, received TEXT NOT NULL, PRIMARY KEY(store,transaction_id));
            CREATE TABLE IF NOT EXISTS audit (
              id INTEGER PRIMARY KEY, player TEXT, actor TEXT NOT NULL, action TEXT NOT NULL,
              reason TEXT NOT NULL, received TEXT NOT NULL, detail TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS flags (
              id INTEGER PRIMARY KEY, player TEXT NOT NULL, code TEXT NOT NULL, received TEXT NOT NULL,
              detail TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS deleted_players (
              player_hash TEXT PRIMARY KEY, deleted_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS retired_purchases (
              transaction_hash TEXT PRIMARY KEY);
            ''')
            if refunds_enabled:
                from purchase_refunds import initialize
                initialize(db)

    def connect(self):
        location = Path(self.path).resolve().as_uri() + '?mode=rw' if self.must_exist else self.path
        db = sqlite3.connect(location, timeout=15, uri=self.must_exist, factory=StoreConnection)
        db.row_factory = sqlite3.Row
        db.execute('PRAGMA foreign_keys=ON')
        db.execute('PRAGMA synchronous=FULL')
        return db

    def register(self, player, token, legacy=False):
        if not isinstance(player, str) or len(player) != 32 or any(c not in '0123456789abcdef' for c in player):
            raise Fault(400, 'Invalid player ID')
        if not isinstance(token, str) or len(token) < 48 or len(token) > 128:
            raise Fault(400, 'Invalid installation credential')
        hashed = hashlib.sha256(token.encode()).hexdigest()
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if db.execute('SELECT 1 FROM deleted_players WHERE player_hash=?',
                          (self.player_hash(player),)).fetchone():
                raise Fault(410, 'This game account was deleted; it cannot be registered again')
            existing = db.execute('SELECT * FROM players WHERE id=?', (player,)).fetchone()
            if existing and not hmac.compare_digest(existing['token_hash'], hashed):
                raise Fault(403, 'Account already exists; recover the original credential')
            if not existing:
                db.execute('INSERT INTO players(id,token_hash,created,migration_required) VALUES(?,?,?,?)',
                           (player, hashed, now(), int(bool(legacy))))
        return {'PlayerId': player}

    @staticmethod
    def player_hash(player):
        return hashlib.sha256(encode(['nasus.deleted-player.v1', player]).encode()).hexdigest()

    @staticmethod
    def transaction_hash(store, transaction):
        return hashlib.sha256(encode(['nasus.retired-purchase.v1', store, transaction]).encode()).hexdigest()

    def authenticate(self, player, token):
        with self.connect() as db:
            self._authenticate(db, player, token)

    def _authenticate(self, db, player, token):
        # Route-level authentication is not enough: a transfer can rotate the
        # credential while an operation is waiting for Apple or a database lock.
        if not isinstance(token, str):
            raise Fault(401, 'Unauthorized')
        row = db.execute('SELECT token_hash FROM players WHERE id=?', (player,)).fetchone()
        if not row or not hmac.compare_digest(row['token_hash'], hashlib.sha256(token.encode()).hexdigest()):
            raise Fault(401, 'Unauthorized')

    def _player(self, db, player, epoch=None):
        row = db.execute('SELECT * FROM players WHERE id=?', (player,)).fetchone()
        if not row:
            raise Fault(404, 'Player not found')
        if epoch is not None and epoch != row['epoch']:
            raise Fault(409, 'Recovery must be applied before uploading or spending')
        return row

    def _audit(self, db, player, actor, action, reason, detail):
        db.execute('INSERT INTO audit(player,actor,action,reason,received,detail) VALUES(?,?,?,?,?,?)',
                   (player, actor, action, reason, now(), encode(detail)))

    def head(self, player, after=0, token=None):
        with self.connect() as db:
            db.execute('BEGIN')
            if token is not None: self._authenticate(db, player, token)
            row = self._player(db, player)
            operations = [json.loads(r['response']) for r in db.execute(
                'SELECT response FROM operations WHERE player=? AND revision>? ORDER BY revision LIMIT 100', (player, after))]
            return {'PlayerId': player, 'Epoch': row['epoch'], 'Free': row['free'], 'Paid': row['paid'],
                    'RefundDebt': self.refund_debt(db, player),
                    'EconomyRevision': row['economy_revision'], 'Frozen': bool(row['frozen']),
                    'MigrationRequired': bool(row['migration_required']),
                    'PurchasesEnabled': self.purchase_verifier is not None,
                    'Operations': operations, 'Recovery': json.loads(row['recovery']) if row['recovery'] else None}

    def refund_debt(self, db, player):
        if not self.refunds_enabled:
            if self.has_refund_schema(db):
                raise RuntimeError('Refund ledger requires an explicitly refund-enabled runtime')
            return 0
        from purchase_refunds import debt
        return debt(db, player)

    @staticmethod
    def has_refund_schema(db):
        return db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name IN "
                          "('refund_wallets','purchase_refunds','refund_events',"
                          "'refund_waivers','refund_reviews','refund_checks') LIMIT 1").fetchone() is not None

    def snapshot(self, player, data, token=None):
        if not isinstance(data, dict) or data.get('PlayerId') != player:
            raise Fault(400, 'Snapshot owner mismatch')
        revision, epoch = data.get('SaveRevision'), data.get('RecoveryEpoch')
        if type(revision) is not int or revision < 1 or type(epoch) is not int or epoch < 0:
            raise Fault(400, 'Invalid revision')
        if type(data.get('SchemaVersion')) is not int or not 0 <= data['SchemaVersion'] <= 3:
            raise Fault(400, 'Unsupported save schema')
        if type(data.get('PlayerLevel')) is not int or data['PlayerLevel'] < 1:
            raise Fault(400, 'Invalid player level')
        raw = encode(data)
        digest = hashlib.sha256(raw.encode()).hexdigest()
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if token is not None: self._authenticate(db, player, token)
            p = self._player(db, player, epoch)
            existing = db.execute('SELECT hash FROM snapshots WHERE player=? AND epoch=? AND revision=?',
                                  (player, epoch, revision)).fetchone()
            if existing:
                if existing['hash'] != digest:
                    raise Fault(409, 'Conflicting contents for the same save revision')
                return {'Saved': True}
            latest = db.execute('SELECT revision FROM snapshots WHERE player=? AND epoch=? ORDER BY revision DESC LIMIT 1',
                                (player, epoch)).fetchone()
            if latest and revision <= latest['revision']:
                raise Fault(409, 'Stale save revision')
            issues = []
            for name in ('Gold', 'FreeGachaStones', 'PaidGachaStones'):
                if type(data.get(name)) is not int or data[name] < 0:
                    issues.append('invalid_' + name)
            if data.get('EconomyRevision') == p['economy_revision'] and (
                data.get('FreeGachaStones') != p['free'] or data.get('PaidGachaStones') != p['paid']):
                issues.append('wallet_mismatch')
            for name in ('OwnedMonsters', 'OwnedEquipments'):
                items = data.get(name, [])
                if not isinstance(items, list) or any(not isinstance(x, dict) or not x.get('InstanceId') for x in items):
                    issues.append('invalid_' + name)
                elif len({x['InstanceId'] for x in items}) != len(items):
                    issues.append('duplicate_' + name)
            for code in issues:
                db.execute('INSERT INTO flags(player,code,received,detail) VALUES(?,?,?,?)',
                           (player, code, now(), encode({'save_revision': revision})))
            db.execute('INSERT INTO snapshots(player,revision,epoch,received,data,hash) VALUES(?,?,?,?,?,?)',
                       (player, revision, epoch, now(), raw, digest))
        return {'Saved': True, 'Warnings': issues}

    def _snapshot(self, db, player, epoch):
        row = db.execute('SELECT data FROM snapshots WHERE player=? AND epoch=? ORDER BY revision DESC LIMIT 1',
                         (player, epoch)).fetchone()
        if not row:
            raise Fault(409, 'Upload a snapshot first')
        return json.loads(row['data'])

    def operation(self, player, request, token=None):
        if type(request.get('Epoch')) is not int or request['Epoch'] < 0:
            raise Fault(400, 'Recovery epoch is required')
        request_id = request.get('RequestId', '')
        if not isinstance(request_id, str) or not 8 <= len(request_id) <= 128:
            raise Fault(400, 'Invalid request ID')
        digest = hashlib.sha256(encode({k: v for k, v in request.items() if k != 'Receipt'}).encode()).hexdigest()
        with self.connect() as db:
            db.execute('BEGIN')
            if token is not None: self._authenticate(db, player, token)
            self._player(db, player, request.get('Epoch'))
            old = db.execute('SELECT * FROM operations WHERE player=? AND request_id=?', (player, request_id)).fetchone()
            if old:
                if old['request_hash'] != digest: raise Fault(409, 'Request ID reused')
                return json.loads(old['response'])
        # Verification calls execute before the DB transaction; the result must be
        # independently verified by the provider adapter, never by client assertions.
        verified = None
        if request.get('Kind') == 'purchase':
            if not self.purchase_verifier:
                raise Fault(503, 'Store verification is not configured; delivery remains pending')
            verified = self.purchase_verifier(dict(request, _AuthenticatedPlayer=player))
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if token is not None: self._authenticate(db, player, token)
            p = self._player(db, player, request.get('Epoch'))
            old = db.execute('SELECT * FROM operations WHERE player=? AND request_id=?', (player, request_id)).fetchone()
            if old:
                if old['request_hash'] != digest:
                    raise Fault(409, 'Request ID was reused for a different operation')
                return json.loads(old['response'])
            if p['frozen'] or p['migration_required']:
                raise Fault(423, 'Online transactions are pending operator review')
            s = self._snapshot(db, player, p['epoch'])
            kind, target = request.get('Kind'), request.get('Target', '')
            refund_debt = self.refund_debt(db, player)
            if refund_debt and (kind == 'upgrade' or (kind == 'gacha' and request.get('Paid') is True)):
                raise Fault(402, 'Refund deficit requires review before spending paid gems')
            free, paid = p['free'], p['paid']
            result = {'RequestId': request_id, 'Revision': p['economy_revision'] + 1, 'Kind': kind,
                      'Target': target, 'Monsters': [], 'GoldDelta': 0, 'ClaimDate': '', 'TransactionId': '',
                      'TutorialPulls': p['tutorial_pulls']}
            result['RefundDebt'] = refund_debt
            if kind == 'gacha':
                count, is_paid = request.get('Count'), request.get('Paid')
                if count not in (1, 10) or type(count) is not int or type(is_paid) is not bool:
                    raise Fault(400, 'Invalid draw')
                if len(s.get('OwnedMonsters', [])) + count > s.get('MonsterStorageLimit', 100):
                    raise Fault(409, 'Monster storage is full')
                cost = count * 300
                if (paid if is_paid else free) < cost:
                    raise Fault(409, 'Insufficient stones')
                pool = [m for m in self.catalog if not m['fusionExclusive'] or (is_paid and m['classRank'] == 4)]
                if not pool:
                    raise Fault(503, 'Gacha catalog unavailable')
                tutorial = not is_paid and p['tutorial_pulls'] < 3
                if tutorial and count != 1:
                    raise Fault(409, 'Complete the three tutorial single draws first')
                guaranteed = False
                for i in range(count):
                    if tutorial:
                        mid = ['monster_dragon_whelp', 'monster_rock_golem', 'monster_apprentice_mage'][p['tutorial_pulls']]
                        monster = next((m for m in pool if m['monsterId'] == mid), None)
                    else:
                        roll = secrets.randbelow(100)
                        rank = (4 if roll < 1 else 3 if roll < 4 else 2 if roll < 13 else 1) if is_paid else (3 if roll < 1 else 2 if roll < 10 else 1)
                        if is_paid and count == 10 and i == 9 and not guaranteed:
                            rank = 3
                        candidates = [m for m in pool if m['classRank'] == rank]
                        monster = secrets.choice(candidates) if candidates else None
                    if not monster:
                        raise Fault(503, 'Gacha catalog is incomplete')
                    guaranteed |= monster['classRank'] == 3
                    item = {'InstanceId': uuid.uuid4().hex, 'MonsterId': monster['monsterId'], 'Level': 1,
                            'HasIndividualValues': True}
                    for field in ('Hp', 'Attack', 'Wisdom', 'Defense', 'MagicDefense', 'AttackSpeed'):
                        item['Individual' + field] = 50 if tutorial else max(secrets.randbelow(101) for _ in range(3 if is_paid else 1))
                    result['Monsters'].append(item)
                if is_paid: paid -= cost
                else: free -= cost
                if tutorial:
                    result['TutorialPulls'] += 1
            elif kind == 'reward':
                today = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(hours=9)).strftime('%Y-%m-%d')
                daily = {'daily_battle_win_1': (1, 300), 'daily_battle_win_3': (3, 100), 'daily_battle_win_5': (5, 200)}
                if target in daily:
                    wins, amount = daily[target]
                    if s.get('DailyQuestProgressDate') != today or s.get('DailyBattleWinCount', 0) < wins:
                        raise Fault(409, 'Reward conditions not met for the server date')
                    key = today + ':' + target
                    result['ClaimDate'] = today
                elif target == 'tutorial_complete':
                    if p['tutorial_pulls'] != 3 or not s.get('HasCompletedTutorial'):
                        raise Fault(409, 'Tutorial is incomplete')
                    key, amount = target, 600
                elif target in ('mission_clear_1', 'mission_reach_floor_3'):
                    needed, gold = (1, 30) if target == 'mission_clear_1' else (3, 60)
                    progress = next((x.get('Progress', 0) for x in s.get('MissionProgressList', []) if x.get('MissionId') == target), 0)
                    if progress < needed:
                        raise Fault(409, 'Mission incomplete')
                    key, amount, result['GoldDelta'] = target, 0, gold
                else:
                    raise Fault(400, 'Unknown reward')
                if db.execute('SELECT 1 FROM claims WHERE player=? AND claim_key=?', (player, key)).fetchone():
                    raise Fault(409, 'Reward already claimed')
                db.execute('INSERT INTO claims VALUES(?,?)', (player, key))
                free += amount
                # Eligibility based on offline progress is not proof of legitimate play.
                self._audit(db, player, 'server', 'reward_eligibility', 'offline_progress_unverified', {'claim': key})
            elif kind == 'upgrade':
                definitions = {'auto_repeat': (1200, 'HasAutoRepeatFloorUpgrade'), 'auto_sell': (1200, 'HasAutoSellEquipmentUpgrade'),
                               'auto_release': (1200, 'HasAutoReleaseMonsterUpgrade'), 'monster_storage': (1500, 'MonsterStorageLimit'),
                               'equipment_storage': (1500, 'EquipmentStorageLimit')}
                if target not in definitions:
                    raise Fault(400, 'Unknown upgrade')
                cost, field = definitions[target]
                if target.startswith('auto_'):
                    if db.execute('SELECT 1 FROM claims WHERE player=? AND claim_key=?', (player, target)).fetchone():
                        raise Fault(409, 'Already owned')
                    db.execute('INSERT INTO claims VALUES(?,?)', (player, target))
                if paid < cost:
                    raise Fault(409, 'Insufficient paid stones')
                paid -= cost
                result['UpgradeField'] = field
            elif kind == 'purchase':
                if not verified or not verified.get('verified') or verified.get('product') != target or verified.get('transaction') != request.get('TransactionId'):
                    raise Fault(403, 'Store verification failed')
                amounts = {f'com.nasus.dungeonmonsterroguelike.crystals{x}': x for x in (120,650,2000,4200,8600,15000)}
                if target not in amounts:
                    raise Fault(400, 'Unknown product')
                if db.execute('SELECT 1 FROM retired_purchases WHERE transaction_hash=?',
                              (self.transaction_hash(verified['store'], verified['transaction']),)).fetchone():
                    raise Fault(409, 'Purchase belongs to a deleted account and cannot be delivered again')
                try:
                    db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', (verified['store'], verified['transaction'], player, target, now()))
                except sqlite3.IntegrityError:
                    raise Fault(409, 'Purchase was already delivered; synchronize its original operation')
                paid += amounts[target]
                result['TransactionId'] = verified['transaction']
            else:
                raise Fault(400, 'Unknown operation')
            if result['GoldDelta'] and (type(s.get('Gold')) is not int or not 0 <= s['Gold'] + result['GoldDelta'] <= 2147483647):
                raise Fault(409, 'Gold limit exceeded')
            result.update(Free=free, Paid=paid)
            if free > 2147483647 or paid > 2147483647:
                raise Fault(409, 'Wallet limit exceeded')
            db.execute('UPDATE players SET free=?,paid=?,economy_revision=?,tutorial_pulls=? WHERE id=?',
                       (free, paid, result['Revision'], result['TutorialPulls'], player))
            db.execute('INSERT INTO operations VALUES(?,?,?,?,?,?,?)',
                       (player, request_id, digest, result['Revision'], kind, now(), encode(result)))
            self._audit(db, player, 'server', kind, target, {'request': request_id, 'before_free': p['free'], 'before_paid': p['paid'], 'after_free': free, 'after_paid': paid})
            return result

    def players(self):
        with self.connect() as db:
            return [dict(x) for x in db.execute('SELECT id,created,free,paid,economy_revision,epoch,frozen,migration_required FROM players ORDER BY created DESC LIMIT 200')]

    def inspect(self, player):
        with self.connect() as db:
            self._player(db, player)
            return {'Snapshots': [dict(x) for x in db.execute('SELECT id,revision,epoch,received,source FROM snapshots WHERE player=? ORDER BY id DESC LIMIT 100', (player,))],
                    'Flags': [dict(x) for x in db.execute('SELECT * FROM flags WHERE player=? ORDER BY id DESC LIMIT 100', (player,))],
                    'Audit': [dict(x) for x in db.execute('SELECT * FROM audit WHERE player=? ORDER BY id DESC LIMIT 100', (player,))]}

    def restore(self, player, snapshot_id, expected_epoch, actor, reason, commit=False, preview_token=None):
        if type(expected_epoch) is not int or expected_epoch < 0:
            raise Fault(400, 'Expected recovery epoch is required')
        if not reason.strip() or not actor.strip():
            raise Fault(400, 'Operator and reason are required')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            p = self._player(db, player, expected_epoch)
            row = db.execute('SELECT data FROM snapshots WHERE id=? AND player=?', (snapshot_id, player)).fetchone()
            if not row:
                raise Fault(404, 'Snapshot not found')
            target = json.loads(row['data'])
            current = self._snapshot(db, player, p['epoch'])
            # Economy is never rolled back. Only restore a generation at the current
            # economy cursor; otherwise paid summons/consumption would need replay.
            if target.get('EconomyRevision', 0) != p['economy_revision']:
                raise Fault(409, 'Snapshot crosses an economy transaction. Use reviewed compensation instead.')
            target.update(FreeGachaStones=p['free'], PaidGachaStones=p['paid'], RecoveryEpoch=p['epoch']+1)
            diff = {k: {'Before': current.get(k), 'After': target.get(k)} for k in set(current)|set(target) if current.get(k) != target.get(k)}
            token = hashlib.sha256(encode({'current': current, 'target': target, 'epoch': p['epoch'], 'economy': p['economy_revision']}).encode()).hexdigest()
            if commit:
                if preview_token != token:
                    raise Fault(409, 'Data changed after preview; inspect the diff again')
                if not p['frozen']:
                    raise Fault(409, 'Freeze online transactions before restoring')
                self._audit(db, player, actor, 'restore', reason, {'snapshot': snapshot_id, 'before': current, 'diff': diff})
                db.execute('UPDATE players SET epoch=epoch+1,recovery=? WHERE id=?', (encode(target), player))
            return {'Diff': diff, 'Epoch': p['epoch'] + (1 if commit else 0), 'PreviewToken': token}

    def freeze(self, player, frozen, actor, reason):
        if not actor.strip() or not reason.strip() or type(frozen) is not bool:
            raise Fault(400, 'Operator, reason and frozen boolean are required')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self._player(db, player)
            db.execute('UPDATE players SET frozen=? WHERE id=?', (int(frozen), player))
            self._audit(db, player, actor, 'freeze' if frozen else 'unfreeze', reason, {})
        return {'Frozen': frozen}

    def adjust(self, player, free, paid, expected_revision, actor, reason, migration=False):
        if not actor.strip() or not reason.strip() or type(free) is not int or type(paid) is not int:
            raise Fault(400, 'Operator, reason and integer amounts required')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            p = self._player(db, player)
            if p['economy_revision'] != expected_revision:
                raise Fault(409, 'Economy changed; reload before adjusting')
            if migration and (not p['migration_required'] or p['economy_revision'] != 0):
                raise Fault(409, 'Migration is not pending')
            new_free, new_paid = (free, paid) if migration else (p['free'] + free, p['paid'] + paid)
            if not 0 <= new_free <= 2147483647 or not 0 <= new_paid <= 2147483647:
                raise Fault(400, 'Invalid resulting balance')
            request_id = uuid.uuid4().hex
            kind = 'migration' if migration else 'compensation'
            tutorial = p['tutorial_pulls']
            if migration:
                snapshot = self._snapshot(db, player, p['epoch'])
                tutorial = min(3, max(0, snapshot.get('InitialTutorialSummonCount', 0)))
                for target, field in [('auto_repeat','HasAutoRepeatFloorUpgrade'),('auto_sell','HasAutoSellEquipmentUpgrade'),('auto_release','HasAutoReleaseMonsterUpgrade')]:
                    if snapshot.get(field): db.execute('INSERT OR IGNORE INTO claims VALUES(?,?)', (player, target))
                if 'tutorial_completion_reward_free_stones' in snapshot.get('SeenTutorialHintIds', []):
                    db.execute('INSERT OR IGNORE INTO claims VALUES(?,?)', (player, 'tutorial_complete'))
                for mission in snapshot.get('MissionProgressList', []):
                    if mission.get('IsClaimed'): db.execute('INSERT OR IGNORE INTO claims VALUES(?,?)', (player, mission['MissionId']))
                for claim in snapshot.get('DailyClaimedQuestIds', []):
                    db.execute('INSERT OR IGNORE INTO claims VALUES(?,?)', (player, snapshot.get('DailyQuestProgressDate','') + ':' + claim))
            result = {'RequestId': request_id, 'Revision': p['economy_revision'] + 1, 'Kind': kind, 'Target': '',
                      'Free': new_free, 'Paid': new_paid, 'RefundDebt': self.refund_debt(db, player),
                      'GoldDelta': 0, 'Monsters': [], 'TutorialPulls': tutorial}
            db.execute('INSERT INTO operations VALUES(?,?,?,?,?,?,?)', (player, request_id, '', result['Revision'], kind, now(), encode(result)))
            db.execute('UPDATE players SET free=?,paid=?,economy_revision=?,migration_required=0,tutorial_pulls=? WHERE id=?',
                       (new_free, new_paid, result['Revision'], tutorial, player))
            self._audit(db, player, actor, kind, reason, {'before_free': p['free'], 'before_paid': p['paid'], 'after_free':new_free,'after_paid':new_paid})
            return result

    def backup(self, target):
        from maintenance import backup_database
        return backup_database(self.path, target)
