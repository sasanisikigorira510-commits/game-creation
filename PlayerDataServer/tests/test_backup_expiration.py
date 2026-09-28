import copy
import datetime as dt
from email.utils import format_datetime
import hashlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from deploy.expire_backups import expire, check_head, request
from deploy.backup_retention_plan import BUCKET
from deploy.inspect_backup_retention import NS
from deploy.retention_health import collect, marker_healthy
from deploy import install_backup_retention as installer

UTC = dt.timezone.utc


class ExpirationTests(unittest.TestCase):
    def setUp(self):
        self.now = dt.datetime(2026, 10, 26, 12, tzinfo=UTC)
        def obj(age, char):
            stamp = self.now-dt.timedelta(days=age)
            return dict(Key='db/'+stamp.strftime('%Y%m%dT%H%M%SZ')+'-'+char*32+'.tar.gz.age',
                        Size=4, LastModified=stamp.isoformat())
        self.old, self.fresh = obj(31, 'a'), obj(0, 'b')
        self.snapshot = dict(Bucket=BUCKET, Prefix='db/', Complete=True, Versioning='NeverEnabled',
            CapturedUtc=self.now.isoformat(), Objects=[self.old, self.fresh], LastVerifiedBackup=dict(
                Status='OFFSITE_CIPHERTEXT_VERIFIED', Key=self.fresh['Key'], Bytes=4,
                Sha256=hashlib.sha256(b'test').hexdigest(), CompletedUtc=self.now.isoformat()))
        self.calls, self.audits = [], []
        self.deleted = set()

    def http(self, method, key, **kwargs):
        self.calls.append((method, key, kwargs))
        if method == 'DELETE':
            self.assertEqual('DELETE_INTENT', self.audits[-1]['Action'])
            self.deleted.add(key); return 204, {}, b''
        if key in self.deleted: return 404, {}, b''
        obj = next(o for o in self.snapshot['Objects'] if o['Key'] == key)
        return 200, {'content-length': '4', 'last-modified': format_datetime(
            dt.datetime.fromisoformat(obj['LastModified']), usegmt=True), 'etag': '"abc"'}, b'test'

    def run_expire(self, execute=True, http=None, version=None, clock=None):
        return expire(self.snapshot, execute=execute, http=http or self.http,
                      clock=clock or (lambda: self.now), audit=self.audits.append,
                      version_get=lambda q: version or ('<VersioningConfiguration xmlns="'+NS[1:-1]+'"/>').encode())

    def test_only_expired_key_deleted_and_absence_checked(self):
        result = self.run_expire()
        self.assertEqual(1, result['ObjectsDeleted'])
        self.assertEqual({self.old['Key']}, self.deleted)
        self.assertEqual('HEAD', self.calls[-1][0])
        self.assertEqual({'etag': '"abc"'}, self.calls[-2][2])
        self.assertEqual(['DELETE_INTENT', 'DELETE_CONFIRMED'], [a['Action'] for a in self.audits])

    def test_dry_run_performs_no_deletes_or_audit(self):
        result = self.run_expire(False)
        self.assertEqual(0, result['ObjectsDeleted'])
        self.assertEqual(set(), self.deleted); self.assertEqual([], self.audits)

    def test_zero_candidates_no_delete(self):
        self.snapshot['Objects'] = [self.fresh]
        self.assertEqual(0, self.run_expire()['CandidateCount'])
        self.assertEqual(['HEAD'], [c[0] for c in self.calls])

    def test_stale_backup_blocks_before_http(self):
        self.snapshot['LastVerifiedBackup']['CompletedUtc'] = (self.now-dt.timedelta(hours=3)).isoformat()
        with self.assertRaises(ValueError): self.run_expire()
        self.assertEqual([], self.calls)

    def test_cipher_mismatch_blocks_deletion(self):
        self.snapshot['LastVerifiedBackup']['Sha256'] = 'f'*64
        with self.assertRaises(ValueError): self.run_expire()
        self.assertEqual(set(), self.deleted)

    def test_versioning_change_stops_deletion(self):
        version = ('<VersioningConfiguration xmlns="'+NS[1:-1]+'"><Status>Enabled</Status></VersioningConfiguration>').encode()
        with self.assertRaises(ValueError): self.run_expire(version=version)
        self.assertEqual(set(), self.deleted)

    def test_changed_object_metadata_stops_deletion(self):
        def changed(method, key, **kwargs):
            code, headers, data = self.http(method, key, **kwargs)
            if key == self.old['Key']: headers['last-modified'] = format_datetime(self.now, usegmt=True)
            return code, headers, data
        with self.assertRaises(ValueError): self.run_expire(http=changed)
        self.assertEqual(set(), self.deleted)

    def test_delete_permission_failure_not_retried(self):
        def denied(method, key, **kwargs):
            if method == 'DELETE': raise PermissionError('denied')
            return self.http(method, key, **kwargs)
        with self.assertRaises(PermissionError): self.run_expire(http=denied)
        self.assertEqual(['DELETE_INTENT'], [a['Action'] for a in self.audits])

    def test_not_absent_after_delete_does_not_claim_confirmation(self):
        def bad(method, key, **kwargs):
            result = self.http(method, key, **kwargs)
            if result[0] == 404: return 200, {}, b''
            return result
        with self.assertRaises(ValueError): self.run_expire(http=bad)
        self.assertEqual(['DELETE_INTENT'], [a['Action'] for a in self.audits])

    def test_elapsed_budget_and_backward_clock_stop(self):
        for delta in (dt.timedelta(minutes=9), -dt.timedelta(seconds=1)):
            clocks = iter([self.now, self.now+delta])
            with self.assertRaises(ValueError): self.run_expire(clock=lambda: next(clocks))
        self.assertEqual(set(), self.deleted)

    def test_scope_rejected_at_network_boundary(self):
        with patch('deploy.expire_backups.subprocess.run') as run:
            for key in ('deletions/test', 'db/../secret', 'deletion-probes/test'):
                with self.assertRaises(ValueError): request('DELETE', key)
            with self.assertRaises(ValueError): request('PUT', self.old['Key'])
            with self.assertRaises(ValueError): request('DELETE', self.old['Key'], etag='"a"\r\nx: y')
            run.assert_not_called()


class RetentionHealthTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)/'status.json'
        self.now = 1790500000
        self.value = dict(Healthy=True, CheckedUnix=self.now, Status='RETENTION_COMPLETE', Execute=True, RetentionDays=30)
        self.write(self.value)

    def write(self, value):
        self.path.write_text(json.dumps(value)); self.path.chmod(0o600)

    def base(self, healthy=True, stopped=False):
        def query(unit):
            return dict(LoadState='loaded', ActiveState='inactive' if stopped else 'active', Result='success', ExecMainStatus='0')
        return SimpleNamespace(collect=lambda **kw: dict(Healthy=healthy, CheckedUnix=self.now), unit_properties=query)

    def test_fresh_real_success_required(self):
        self.assertTrue(marker_healthy(self.path, self.now))
        for update in ({'Healthy': False}, {'CheckedUnix': self.now-7201}, {'CheckedUnix': self.now+1},
                       {'CheckedUnix': True}, {'Execute': False}, {'Status': 'RETENTION_DRY_RUN_COMPLETE'}):
            self.write(dict(self.value, **update)); self.assertFalse(marker_healthy(self.path, self.now))

    def test_permission_and_symlink_rejected(self):
        self.path.chmod(0o644); self.assertFalse(marker_healthy(self.path, self.now))
        self.path.chmod(0o600)
        link = self.path.parent/'link'; link.symlink_to(self.path)
        self.assertFalse(marker_healthy(link, self.now))

    def test_existing_failure_not_masked(self):
        self.assertFalse(collect(self.base(False), report=self.path, now=self.now)['Healthy'])

    def test_stopped_timer_or_missing_marker_unhealthy(self):
        self.assertFalse(collect(self.base(stopped=True), report=self.path, now=self.now)['Healthy'])
        self.assertFalse(collect(self.base(), report=self.path.parent/'absent', now=self.now)['Healthy'])

    def test_combined_success_only_boolean_public_fields(self):
        self.assertEqual(dict(Healthy=True, CheckedUnix=self.now), collect(self.base(), report=self.path, now=self.now))


class RetentionTransportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.now = dt.datetime(2026, 10, 26, 12, tzinfo=UTC)
        self.key = 'db/20260925T120000Z-'+'a'*32+'.tar.gz.age'
        self.calls = []

    def invoke(self, method='HEAD', status=200, extra='', date=None):
        def run(args, **kwargs):
            self.calls.append(args)
            headers = Path(args[args.index('--dump-header')+1])
            body = Path(args[args.index('--output')+1])
            headers.write_bytes(('HTTP/1.1 '+str(status)+' OK\r\nDate: '+(date or format_datetime(self.now, usegmt=True))+
                '\r\nContent-Length: 4\r\n'+extra+'\r\n').encode())
            body.write_bytes(b'test')
            return SimpleNamespace(returncode=0, stdout=str(status).encode())
        with patch('deploy.expire_backups.STATE', Path(self.temp.name)), \
             patch('deploy.expire_backups.inventory.private_root_file'), \
             patch('deploy.expire_backups.now', lambda: self.now), \
             patch('deploy.expire_backups.subprocess.run', run):
            return request(method, self.key, etag='"abc"' if method == 'DELETE' else None)

    def test_head_uses_no_body_and_fixed_endpoint(self):
        code, headers, body = self.invoke()
        self.assertEqual(200, code); self.assertEqual(b'', body)
        self.assertIn('--head', self.calls[0])
        self.assertTrue(self.calls[0][-1].endswith('/'+BUCKET+'/'+self.key))
        self.assertEqual('0', self.calls[0][self.calls[0].index('--retry')+1])

    def test_get_ciphertext_and_delete_exact_key(self):
        self.assertEqual(b'test', self.invoke('GET')[2])
        self.assertEqual(204, self.invoke('DELETE', 204)[0])
        self.assertIn('If-Match: "abc"', self.calls[-1])

    def test_permission_failure_no_retry(self):
        with self.assertRaises(ValueError): self.invoke('DELETE', 403)
        self.assertEqual(1, len(self.calls))

    def test_time_skew_duplicate_headers_and_version_rejected(self):
        with self.assertRaises(ValueError):
            self.invoke(date=format_datetime(self.now-dt.timedelta(minutes=6), usegmt=True))
        for extra in ('x-amz-version-id: real-version\r\n', 'Content-Length: 5\r\n'):
            with self.assertRaises(ValueError): self.invoke(extra=extra)

    def test_install_pins_match_staged_code(self):
        root = Path(installer.__file__).parent
        for name, digest in installer.FILES.items():
            self.assertEqual(digest, hashlib.sha256((root/name).read_bytes()).hexdigest(), name)
        self.assertEqual(installer.OLD_HASH, hashlib.sha256((root/'publish_backup_health.py').read_bytes()).hexdigest())

    def test_health_hook_does_not_replace_existing_files(self):
        self.assertIn('70-backup-retention.conf', str(installer.HOOK))
        self.assertIn('retention_health.py', installer.HOOK_TEXT)
        self.assertIn('60-integrated-health.conf', installer.OLD_DROPS)
