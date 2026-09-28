import datetime as dt
import unittest

from deploy.backup_retention_plan import BUCKET, plan

UTC = dt.timezone.utc


class RetentionPlanTests(unittest.TestCase):
    def setUp(self):
        self.now = dt.datetime(2026, 10, 26, 12, tzinfo=UTC)
        self.fresh = self.obj(self.now-dt.timedelta(minutes=30), 'a')
        self.snapshot = dict(Bucket=BUCKET, Prefix='db/', Complete=True, Versioning='NeverEnabled',
            CapturedUtc=self.now.isoformat(), Objects=[self.fresh], LastVerifiedBackup=dict(
                Status='OFFSITE_CIPHERTEXT_VERIFIED', Key=self.fresh['Key'], Bytes=100,
                Sha256='f'*64, CompletedUtc=(self.now-dt.timedelta(minutes=25)).isoformat()))

    def obj(self, stamp, identifier):
        return dict(Key='db/'+stamp.strftime('%Y%m%dT%H%M%SZ')+'-'+identifier*32+'.tar.gz.age',
                    Size=100, LastModified=stamp.isoformat())

    def result(self): return plan(self.snapshot, now=self.now)

    def test_only_strictly_older_than_30_days_are_candidates(self):
        cutoff = self.now-dt.timedelta(days=30)
        expired = self.obj(cutoff-dt.timedelta(seconds=1), 'b')
        self.snapshot['Objects'] += [expired, self.obj(cutoff, 'c'), self.obj(cutoff+dt.timedelta(seconds=1), 'd')]
        report = self.result()
        self.assertEqual('RETENTION_REVIEW_READY', report['Status'])
        self.assertEqual([expired['Key']], [o['Key'] for o in report['Candidates']])
        self.assertEqual(3, report['RetainedCount'])
        self.assertEqual(0, report['ObjectsDeleted'])
        self.assertFalse(report['DeleteAuthorized'])

    def test_newly_overwritten_old_name_retained(self):
        old = self.obj(self.now-dt.timedelta(days=60), 'b')
        old['LastModified'] = self.now.isoformat(); self.snapshot['Objects'].append(old)
        self.assertEqual(0, self.result()['CandidateCount'])

    def test_deletion_history_keys_cannot_be_candidates(self):
        for key in ('deletions/instance/event.json.age', 'deletion-probes/'+'a'*32+'.age',
                    'db/../secret', 'db/unknown.age'):
            with self.subTest(key=key):
                self.snapshot['Objects'] = [dict(self.fresh, Key=key)]
                with self.assertRaises(ValueError): self.result()

    def test_partial_or_wrong_scope_rejected(self):
        for key, value in (('Complete', False), ('Complete', 1), ('Bucket', 'other'), ('Prefix', '')):
            with self.subTest(key=key):
                altered = dict(self.snapshot, **{key: value})
                with self.assertRaises(ValueError): plan(altered, now=self.now)

    def test_stale_or_future_inventory_rejected(self):
        for offset in (dt.timedelta(minutes=-11), dt.timedelta(seconds=1)):
            self.snapshot['CapturedUtc'] = (self.now+offset).isoformat()
            with self.assertRaises(ValueError): self.result()

    def test_versioned_or_unknown_bucket_blocked(self):
        for versioning in ('Enabled', 'Suspended', None):
            self.snapshot['Versioning'] = versioning
            self.assertIn('VERSION_HISTORY_REVIEW_REQUIRED', self.result()['Blockers'])

    def test_no_recent_verified_backup_blocks_plan(self):
        self.snapshot['LastVerifiedBackup']['CompletedUtc'] = (self.now-dt.timedelta(hours=3)).isoformat()
        self.assertIn('RECENT_VERIFIED_BACKUP_REQUIRED', self.result()['Blockers'])

    def test_verified_receipt_must_match_inventory(self):
        self.snapshot['LastVerifiedBackup']['Bytes'] = 101
        self.assertIn('RECENT_VERIFIED_BACKUP_REQUIRED', self.result()['Blockers'])

    def test_duplicate_and_malformed_objects_rejected(self):
        self.snapshot['Objects'].append(self.fresh)
        with self.assertRaises(ValueError): self.result()
        for update in ({'Size': True}, {'LastModified': '2026-10-26T12:00:00'}, {'Size': -1}):
            self.snapshot['Objects'] = [dict(self.fresh, **update)]
            with self.assertRaises(ValueError): self.result()

    def test_empty_inventory_is_not_safe_to_prune(self):
        self.snapshot['Objects'] = []
        self.assertEqual('RETENTION_REVIEW_BLOCKED', self.result()['Status'])
