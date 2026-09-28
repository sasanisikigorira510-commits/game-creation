import json
from pathlib import Path
import secrets
import tempfile
from types import SimpleNamespace
import unittest

from apple_grants import AppleGrantVault
from apple_tokens import AppleGrant
from apple_revocation_worker import run_batch, inspect_queue
from store import Store, Fault


class RevocationWorkerTests(unittest.TestCase):
    def setUp(self):
        temp=tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root=Path(temp.name); self.now=1000
        self.store=Store(self.root/'db.sqlite'); self.key=secrets.token_bytes(32)
        self.vault=AppleGrantVault(self.store,self.key,'com.test.game',clock=lambda:self.now)
        with self.store.connect() as db:
            db.execute('CREATE TABLE apple_exchanges (state TEXT, created REAL)')
        self.calls=[]
        self.client=SimpleNamespace(client_id='com.test.game',revoke=self.calls.append)

    def queued(self, count=1):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            for _ in range(count): self.vault.save(db,AppleGrant('synthetic-subject','synthetic-secret'))
            self.vault.queue(db,self.vault.subject_hash('synthetic-subject'))

    def test_empty_queue_is_healthy_without_provider_call(self):
        result=run_batch(self.vault,self.client)
        self.assertTrue(result['Healthy']); self.assertEqual([],self.calls)
        self.assertEqual(1,result['Processed']['idle'])

    def test_batch_limit_bounds_calls_and_reports_remaining_work(self):
        self.queued(3); result=run_batch(self.vault,self.client,max_jobs=2)
        self.assertEqual(2,len(self.calls)); self.assertFalse(result['Healthy'])
        self.assertEqual(1,result['Queue']['Due']); self.assertTrue(result['BatchLimitReached'])
        self.assertTrue(run_batch(self.vault,self.client)['Healthy'])

    def test_provider_failure_keeps_ciphertext_and_backoff_survives_reopen(self):
        self.queued()
        self.client.revoke=lambda _: (_ for _ in ()).throw(Fault(503,'secret-provider-body'))
        result=run_batch(self.vault,self.client)
        self.assertIn('PROVIDER_RETRY',result['ReasonCodes'])
        self.assertEqual(0,result['Queue']['Due']); self.assertEqual(1,result['Queue']['Pending'])
        self.now+=30; self.client.revoke=self.calls.append
        reopened=AppleGrantVault(self.store,self.key,'com.test.game',clock=lambda:self.now)
        self.assertTrue(run_batch(reopened,self.client)['Healthy']); self.assertEqual(1,len(self.calls))

    def test_wrong_key_is_unhealthy_without_network(self):
        self.queued()
        wrong=AppleGrantVault(self.store,secrets.token_bytes(32),'com.test.game',clock=lambda:self.now)
        result=run_batch(wrong,self.client)
        self.assertIn('TOKEN_UNAVAILABLE',result['ReasonCodes']); self.assertEqual([],self.calls)

    def test_crash_lease_is_retained_and_resumes_after_expiry(self):
        self.queued()
        self.client.revoke=lambda _: (_ for _ in ()).throw(RuntimeError('synthetic-secret'))
        result=run_batch(self.vault,self.client)
        self.assertEqual(['WORKER_FAILED'],result['ReasonCodes'])
        self.assertNotIn('synthetic-secret',json.dumps(result))
        self.assertEqual(1,inspect_queue(self.vault,self.now)['Leased'])
        self.now+=121; self.client.revoke=self.calls.append
        self.assertEqual(1,inspect_queue(self.vault,self.now)['OverdueLeases'])
        self.assertTrue(run_batch(self.vault,self.client)['Healthy'])

    def test_stale_lease_is_not_marked_healthy_or_deleted(self):
        self.queued()
        def changed(_):
            with self.store.connect() as db: db.execute("UPDATE apple_grants SET lease='other'")
        self.client.revoke=changed
        result=run_batch(self.vault,self.client)
        self.assertIn('LEASE_CHANGED',result['ReasonCodes']); self.assertEqual(1,result['Queue']['Leased'])

    def test_uncertain_and_old_requested_exchanges_require_review_not_replay(self):
        with self.store.connect() as db:
            db.executemany('INSERT INTO apple_exchanges VALUES(?,?)',
                [('uncertain',1000),('requested',800),('stored',800),('requested',999),('attached',800)])
        result=run_batch(self.vault,self.client)
        self.assertEqual(3,result['Queue']['UnresolvedExchanges'])
        self.assertIn('EXCHANGE_REVIEW_REQUIRED',result['ReasonCodes']); self.assertEqual([],self.calls)
        with self.store.connect() as db: self.assertEqual(5,db.execute('SELECT count(*) FROM apple_exchanges').fetchone()[0])

    def test_no_exchange_schema_is_failure_not_implicit_initialization(self):
        with self.store.connect() as db: db.execute('DROP TABLE apple_exchanges')
        result=run_batch(self.vault,self.client)
        self.assertFalse(result['Healthy']); self.assertEqual([],self.calls)
        with self.store.connect() as db:
            self.assertIsNone(db.execute("SELECT name FROM sqlite_master WHERE name='apple_exchanges'").fetchone())

    def test_quarantined_database_never_contacts_provider(self):
        self.queued(); (self.root/'RECOVERY-PENDING.txt').touch()
        self.assertFalse(run_batch(self.vault,self.client)['Healthy']); self.assertEqual([],self.calls)

    def test_mismatched_client_never_processes_queue(self):
        self.queued(); self.client.client_id='other.app'
        result=run_batch(self.vault,self.client)
        self.assertEqual(['CONFIGURATION_INVALID'],result['ReasonCodes']); self.assertEqual([],self.calls)

    def test_budget_checked_between_calls(self):
        self.queued(2); clock=iter([0,0,60])
        result=run_batch(self.vault,self.client,monotonic=lambda:next(clock))
        self.assertEqual(1,len(self.calls)); self.assertTrue(result['BatchLimitReached'])
        self.assertFalse(result['Healthy'])

    def test_invalid_limits_rejected(self):
        for limits in (dict(max_jobs=True),dict(max_jobs=0),dict(max_jobs=33),dict(max_seconds=float('nan')),
                       dict(max_seconds=0),dict(max_seconds=61)):
            with self.assertRaises(ValueError): run_batch(self.vault,self.client,**limits)

    def test_future_metadata_or_regressed_clock_fails_closed(self):
        self.queued(); self.now=900
        self.assertFalse(run_batch(self.vault,self.client)['Healthy']); self.assertEqual([],self.calls)

    def test_only_counts_and_fixed_codes_returned(self):
        self.queued(); result=run_batch(self.vault,self.client)
        raw=json.dumps(result)
        for secret in ('synthetic-secret','synthetic-subject',self.vault.subject_hash('synthetic-subject'),'encrypted_token'):
            self.assertNotIn(secret,raw)
        self.assertTrue(result['Healthy']); self.assertEqual(1,result['Processed']['revoked'])

    def test_queue_inspection_does_not_change_rows(self):
        self.queued()
        def snapshot():
            with self.store.connect() as db: return list(db.iterdump())
        before=snapshot(); inspect_queue(self.vault,self.now)
        self.assertEqual(before,snapshot())

    def test_wall_clock_regression_during_call_does_not_publish_health(self):
        self.queued()
        def backwards(_): self.now-=1
        self.client.revoke=backwards
        result=run_batch(self.vault,self.client)
        self.assertFalse(result['Healthy']); self.assertIsNone(result['CheckedUnix'])

    def test_existing_heartbeat_reader_rejects_old_or_failed_observation(self):
        from backup_health import healthy_marker
        path=self.root/'status.json'
        path.write_text(json.dumps(run_batch(self.vault,self.client)))
        self.assertTrue(healthy_marker(path,now=self.now))
        self.assertFalse(healthy_marker(path,now=self.now+181))
        self.queued(); self.client.revoke=lambda _: (_ for _ in ()).throw(Fault(503,'secret'))
        path.write_text(json.dumps(run_batch(self.vault,self.client)))
        self.assertFalse(healthy_marker(path,now=self.now))


if __name__ == '__main__': unittest.main()
