import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from apple_revocation_health import healthy_runtime
from apple_revocation_runtime import running_status
from deploy import apple_integrated_health_v2 as health


class IntegratedAppleHealthTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.state = Path(temp.name).resolve()
        self.marker = self.state/'status.json'
        self.service = dict(LoadState='loaded', ActiveState='inactive', Result='success', ExecMainStatus='0')
        self.timer = dict(LoadState='loaded', ActiveState='active', UnitFileState='enabled')
        self.write(dict(Healthy=True, CheckedUnix=1000, Status='APPLE_REVOCATION_BATCH_OBSERVED'))

    def write(self, value):
        self.marker.write_text(json.dumps(value)); self.marker.chmod(0o600)

    def begin(self):
        with patch('apple_revocation_runtime.time.time', return_value=1060), \
             patch('apple_revocation_runtime.time.monotonic_ns', return_value=2_000_000_000):
            value = running_status(self.state)
        self.write(value)
        self.service.update(ActiveState='activating', SubState='start', ExecMainStartTimestampMonotonic='1000000')
        return value

    def test_running_accepts_only_recent_completed_success_same_live_run(self):
        value = self.begin()
        self.assertFalse(value['Healthy'])
        self.assertEqual(value['PreviousSuccessUnix'], 1000)
        self.assertTrue(healthy_runtime(self.marker, self.service, self.timer, 1061, 3_000_000_000))
        for changes in ({'ActiveState':'failed'}, {'Result':'timeout'}, {'ExecMainStatus':'1'},
                        {'ExecMainStartTimestampMonotonic':'4000000'}, {'SubState':'stop'},
                        {'ExecMainStartTimestampMonotonic':'0'}):
            self.assertFalse(healthy_runtime(self.marker, dict(self.service, **changes), self.timer,
                                            1061, 3_000_000_000))
        for now, mono in ((1181, 3_000_000_000), (1061, 92_000_000_000), (999, 3_000_000_000)):
            self.assertFalse(healthy_runtime(self.marker, self.service, self.timer, now, mono))

    def test_running_failed_interrupted_or_missing_marker_cannot_extend_success(self):
        for status in ('APPLE_WORKER_RUNNING', 'APPLE_WORKER_INTERRUPTED', 'APPLE_REVOCATION_BATCH_STOPPED'):
            self.write(dict(Healthy=True, CheckedUnix=1000, Status=status, PreviousSuccessUnix=999))
            self.assertIsNone(self.begin()['PreviousSuccessUnix'])
        self.marker.unlink()
        self.assertIsNone(self.begin()['PreviousSuccessUnix'])
        self.write(dict(Healthy=False, CheckedUnix=1000, Status='APPLE_REVOCATION_BATCH_OBSERVED'))
        self.assertIsNone(self.begin()['PreviousSuccessUnix'])

    def test_stale_and_malformed_running_markers_rejected(self):
        valid = self.begin()
        for changes in ({'PreviousSuccessUnix':None}, {'PreviousSuccessUnix':float('nan')},
                        {'PreviousSuccessUnix':True}, {'StartedMonotonicNs':False},
                        {'StartedMonotonicNs':9_000_000_000}, {'CheckedUnix':1062},
                        {'Status':'APPLE_WORKER_INTERRUPTED'}, {'Healthy':True}):
            self.write(dict(valid, **changes))
            self.assertFalse(healthy_runtime(self.marker, self.service, self.timer, 1061, 3_000_000_000))

    def collect(self, healthy=True, query=None):
        self.reasons = []
        def existing(on_reason):
            on_reason({'Reason':'HEALTHY' if healthy else 'RETENTION_MARKER_REJECTED'})
            return dict(Healthy=healthy, CheckedUnix=1000)
        return health.collect(existing, report=self.marker, owner=os.geteuid(), now=1001,
            query=query or (lambda unit: self.timer if unit.endswith('.timer') else self.service),
            on_reason=self.reasons.append)

    def test_existing_backup_deletion_retention_failure_cannot_be_masked(self):
        def forbidden(_): raise AssertionError('Do not query Apple after existing failure')
        self.assertFalse(self.collect(False, forbidden)['Healthy'])
        self.assertEqual(self.reasons[-1]['Reason'], 'RETENTION_MARKER_REJECTED')

    def test_combined_health_requires_apple_units_marker_and_metadata(self):
        self.assertTrue(self.collect()['Healthy'])
        self.timer['UnitFileState'] = 'disabled'
        self.assertFalse(self.collect()['Healthy'])
        self.timer['UnitFileState'] = 'enabled'
        self.marker.chmod(0o644)
        self.assertFalse(self.collect()['Healthy'])
        self.assertEqual(self.reasons[-1]['Reason'], 'APPLE_MARKER_REJECTED')
        self.marker.unlink(); self.marker.symlink_to(self.state/'missing')
        self.assertFalse(self.collect()['Healthy'])

    def test_query_exception_is_sanitized(self):
        def failure(_): raise ValueError('secret-value')
        self.assertFalse(self.collect(query=failure)['Healthy'])
        self.assertNotIn('secret-value', json.dumps(self.reasons))

    def test_shared_directory_or_wrong_owner_rejected(self):
        self.assertTrue(health.private_marker(self.marker, os.geteuid()))
        self.assertFalse(health.private_marker(self.marker, os.geteuid()+1))
        self.state.chmod(0o755)
        self.assertFalse(health.private_marker(self.marker, os.geteuid()))

    def test_start_finish_race_is_resampled_not_reported_as_stable_failure(self):
        starting=dict(self.service,ActiveState='activating',SubState='start')
        samples=iter([starting,self.service,self.service,self.service])
        def query(unit): return self.timer if unit.endswith('.timer') else next(samples)
        with patch.object(health,'healthy_runtime',side_effect=[False,True]), \
             patch.object(health.time,'sleep') as pause:
            self.assertTrue(health.sampled_runtime(self.marker,query,pause=pause))
        pause.assert_called_once_with(0.25)

    def test_service_changes_after_healthy_read_cannot_publish_mixed_success(self):
        failed=dict(self.service,ActiveState='failed',Result='exit-code')
        samples=iter([self.service,failed,failed,failed])
        def query(unit): return self.timer if unit.endswith('.timer') else next(samples)
        with patch.object(health,'healthy_runtime',side_effect=[True,False]):
            self.assertFalse(health.sampled_runtime(self.marker,query,pause=lambda _:None))

    def test_stuck_startup_has_bounded_retries_and_remains_unhealthy(self):
        starting=dict(self.service,ActiveState='activating',SubState='start')
        def query(unit): return self.timer if unit.endswith('.timer') else starting
        pauses=[]
        with patch.object(health,'healthy_runtime',return_value=False) as evaluate:
            self.assertFalse(health.sampled_runtime(self.marker,query,pause=pauses.append))
        self.assertEqual(evaluate.call_count,3)
        self.assertEqual(pauses,[0.25,0.25])
