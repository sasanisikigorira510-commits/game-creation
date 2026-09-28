import copy
import concurrent.futures
import json
import sqlite3
import sys
import tempfile
import unittest
import uuid
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from store import Store, Fault


class StoreTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.catalog = json.loads((Path(__file__).resolve().parents[1] / 'catalog.json').read_text())
        self.store = Store(Path(self.temp.name) / 'db.sqlite', self.catalog)
        self.player = uuid.uuid4().hex
        self.token = 'a' * 64
        self.store.register(self.player, self.token)
        self.save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                         SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900, PaidGachaStones=0,
                         OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100,
                         InitialTutorialSummonCount=0, HasCompletedTutorial=False)
        self.store.snapshot(self.player, self.save)

    def tearDown(self): self.temp.cleanup()
    def request(self, **kw):
        return dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='gacha', Count=1, Paid=False, **kw)
    def fault(self, status, fn):
        with self.assertRaises(Fault) as error: fn()
        self.assertEqual(status, error.exception.status)
    def snapshot(self, **changes):
        self.save.update(changes)
        self.save['SaveRevision'] += 1
        return self.store.snapshot(self.player, self.save)

    def test_missing_epoch_cannot_bypass_restore_fence(self):
        request = self.request(); del request['Epoch']
        self.fault(400, lambda: self.store.operation(self.player, request))

    def test_catalog_matches_runtime_master(self):
        from export_catalog import catalog
        self.assertEqual(catalog(), self.catalog)

    def test_auth_and_registration_cannot_claim_other_account(self):
        self.store.authenticate(self.player, self.token)
        self.fault(401, lambda: self.store.authenticate(self.player, 'wrong'))
        self.fault(403, lambda: self.store.register(self.player, 'b' * 64))
        self.store.register(self.player, self.token)

    def test_retry_returns_exact_monsters_and_only_one_charge(self):
        request = self.request()
        first = self.store.operation(self.player, request)
        self.assertEqual(first, self.store.operation(self.player, request))
        self.assertEqual(600, self.store.head(self.player)['Free'])
        self.assertEqual('monster_dragon_whelp', first['Monsters'][0]['MonsterId'])
        self.assertEqual(1, len(self.store.head(self.player)['Operations']))

    def test_same_id_different_request_rejected(self):
        request = self.request()
        self.store.operation(self.player, request)
        request['Count'] = 10
        self.fault(409, lambda: self.store.operation(self.player, request))

    def test_concurrent_retry_never_double_spends(self):
        request = self.request()
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            results = list(pool.map(lambda _: self.store.operation(self.player, request), range(12)))
        self.assertTrue(all(x == results[0] for x in results))
        self.assertEqual(600, self.store.head(self.player)['Free'])

    def test_failed_draw_does_not_charge(self):
        request = self.request(); request['Paid'] = True
        self.fault(409, lambda: self.store.operation(self.player, request))
        self.assertEqual(900, self.store.head(self.player)['Free'])
        self.assertEqual([], self.store.head(self.player)['Operations'])

    def test_client_wallet_edits_only_raise_flags_not_server_balance(self):
        result = self.snapshot(FreeGachaStones=999999, PaidGachaStones=10000)
        self.assertIn('wallet_mismatch', result['Warnings'])
        self.assertEqual(900, self.store.head(self.player)['Free'])
        self.assertEqual(0, self.store.head(self.player)['Paid'])

    def test_snapshot_duplicate_content_and_stale_revisions(self):
        self.store.snapshot(self.player, self.save)
        changed = dict(self.save, Gold=800)
        self.fault(409, lambda: self.store.snapshot(self.player, changed))
        self.snapshot()
        self.fault(409, lambda: self.store.snapshot(self.player, dict(self.save, SaveRevision=1, Gold=22)))

    def test_snapshot_cross_account_rejected(self):
        self.fault(400, lambda: self.store.snapshot(self.player, dict(self.save, PlayerId=uuid.uuid4().hex)))

    def test_daily_claim_uses_server_date_and_deduplicates_across_ids(self):
        import datetime as dt
        today = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(hours=9)).strftime('%Y-%m-%d')
        self.snapshot(DailyQuestProgressDate=today, DailyBattleWinCount=5)
        request = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='reward', Target='daily_battle_win_1')
        self.assertEqual(1200, self.store.operation(self.player, request)['Free'])
        request['RequestId'] = uuid.uuid4().hex
        self.fault(409, lambda: self.store.operation(self.player, request))
        self.snapshot(DailyQuestProgressDate='2099-01-01')
        request['Target'] = 'daily_battle_win_3'
        self.fault(409, lambda: self.store.operation(self.player, request))

    def test_no_receipt_verifier_never_grants_currency(self):
        request = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='purchase', Target='x', TransactionId='fake', Receipt='fake')
        self.fault(503, lambda: self.store.operation(self.player, request))
        self.assertEqual(0, self.store.head(self.player)['Paid'])

    def test_verified_purchase_global_uniqueness_and_account_bound_adapter(self):
        seen = []
        def verifier(request):
            seen.append(request['_AuthenticatedPlayer'])
            return dict(verified=True, store='apple', transaction=request['TransactionId'], product=request['Target'])
        self.store.purchase_verifier = verifier  # Only a test double; never selectable by the HTTP API.
        request = dict(RequestId='apple-tx-1', Epoch=0, Kind='purchase', Target='com.nasus.dungeonmonsterroguelike.crystals120', TransactionId='tx-1', Receipt='signed')
        first = self.store.operation(self.player, request)
        request['Receipt'] = 'renewed-signature'
        self.assertEqual(first, self.store.operation(self.player, request))
        self.assertEqual([self.player], seen)
        self.assertEqual(120, self.store.head(self.player)['Paid'])
        request['RequestId'] = 'another-id'
        self.fault(409, lambda: self.store.operation(self.player, request))

    def test_restore_requires_freeze_diff_and_preserves_ledger(self):
        old_id = self.store.inspect(self.player)['Snapshots'][0]['id']
        self.snapshot(Gold=500)
        preview = self.store.restore(self.player, old_id, 0, 'operator', 'bug')
        self.assertEqual({'Before':500,'After':100}, preview['Diff']['Gold'])
        self.fault(409, lambda: self.store.restore(self.player, old_id, 0, 'operator', 'bug', True, preview['PreviewToken']))
        self.store.freeze(self.player, True, 'operator', 'bug')
        self.store.restore(self.player, old_id, 0, 'operator', 'bug', True, preview['PreviewToken'])
        self.assertEqual(1, self.store.head(self.player)['Epoch'])
        self.assertEqual(100, self.store.head(self.player)['Recovery']['Gold'])
        self.fault(409, lambda: self.store.snapshot(self.player, dict(self.save, SaveRevision=999)))
        self.fault(409, lambda: self.store.operation(self.player, self.request()))

    def test_restore_preview_invalidated_by_new_snapshot(self):
        old_id = self.store.inspect(self.player)['Snapshots'][0]['id']
        self.store.freeze(self.player, True, 'operator', 'bug')
        preview = self.store.restore(self.player, old_id, 0, 'operator', 'bug')
        self.snapshot(Gold=501)
        self.fault(409, lambda: self.store.restore(self.player, old_id, 0, 'operator', 'bug', True, preview['PreviewToken']))

    def test_restore_cannot_cross_paid_or_free_economy_transaction(self):
        old_id = self.store.inspect(self.player)['Snapshots'][0]['id']
        self.store.operation(self.player, self.request())
        self.fault(409, lambda: self.store.restore(self.player, old_id, 0, 'operator', 'bug'))

    def test_frozen_player_cannot_spend_but_can_backup(self):
        self.store.freeze(self.player, True, 'operator', 'investigation')
        self.fault(423, lambda: self.store.operation(self.player, self.request()))
        self.snapshot(Gold=500)

    def test_migration_is_reviewed_once_and_audited(self):
        other = uuid.uuid4().hex
        self.store.register(other, 'c' * 64, legacy=True)
        self.store.snapshot(other, dict(self.save, PlayerId=other, InitialTutorialSummonCount=3))
        self.fault(423, lambda: self.store.operation(other, self.request()))
        result = self.store.adjust(other, 500, 120, 0, 'operator', 'verified legacy receipts', True)
        self.assertEqual(120, result['Paid'])
        self.assertFalse(self.store.head(other)['MigrationRequired'])
        self.fault(409, lambda: self.store.adjust(other, 500, 120, 1, 'operator', 'again', True))
        self.assertEqual('migration', self.store.inspect(other)['Audit'][0]['action'])

    def test_compensation_compare_and_set_and_negative_balance(self):
        self.store.adjust(self.player, 100, 0, 0, 'operator', 'bug compensation')
        self.fault(409, lambda: self.store.adjust(self.player, 100, 0, 0, 'operator', 'duplicate'))
        self.fault(400, lambda: self.store.adjust(self.player, -9999, 0, 1, 'operator', 'bad'))
        self.assertEqual(1000, self.store.head(self.player)['Free'])

    def test_backup_restore_contains_wal_transactions_and_purchase_ledger(self):
        self.store.operation(self.player, self.request())
        backup = Path(self.temp.name) / 'backup.sqlite'
        self.store.backup(backup)
        restored = Store(backup, self.catalog)
        self.assertEqual(self.store.head(self.player), restored.head(self.player))
        with sqlite3.connect(backup) as db: self.assertEqual('ok', db.execute('PRAGMA integrity_check').fetchone()[0])

    def test_paid_ten_guarantee_and_permanent_costs(self):
        self.store.adjust(self.player, 0, 10000, 0, 'operator', 'test paid wallet')
        request = self.request(); request.update(Count=10, Paid=True)
        result = self.store.operation(self.player, request)
        rank = {x['monsterId']: x['classRank'] for x in self.catalog}
        self.assertIn(3, [rank[x['MonsterId']] for x in result['Monsters']])
        self.assertEqual(7000, result['Paid'])
        upgrade = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='upgrade', Target='auto_repeat')
        self.assertEqual(5800, self.store.operation(self.player, upgrade)['Paid'])
        upgrade['RequestId'] = uuid.uuid4().hex
        self.fault(409, lambda: self.store.operation(self.player, upgrade))


if __name__ == '__main__': unittest.main()
