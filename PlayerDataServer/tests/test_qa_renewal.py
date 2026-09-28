import hashlib
import json
import unittest
import io
from contextlib import redirect_stdout
from unittest.mock import patch, Mock

from deploy import renew_apple_qa as renewal


class QaRenewalTests(unittest.TestCase):
    def setUp(self):
        self.token = 'a' * 64
        self.gate = dict(Version=1, IssuedUnix=100, ExpiresUnix=200,
            TokenSha256=hashlib.sha256(self.token.encode()).hexdigest())

    def test_exact_24_hours_same_key(self):
        result = renewal.renew_config(json.dumps(self.gate), self.token, 201.5)
        self.assertEqual(result['ExpiresUnix'] - result['IssuedUnix'], 86400)
        self.assertEqual(result['TokenSha256'], self.gate['TokenSha256'])
        self.assertEqual(result['IssuedUnix'], 201)

    def test_original_service_owned_private_directory_is_valid(self):
        directory = Mock()
        directory.is_dir.return_value = True
        directory.is_symlink.return_value = False
        directory.lstat.return_value = type('Stat', (), {'st_uid':999,'st_gid':988,'st_mode':0o40700})()
        parent = Mock()
        parent.is_dir.return_value = True
        parent.is_symlink.return_value = False
        parent.lstat.return_value = type('Stat', (), {'st_uid':0,'st_mode':0o40755})()
        directory.parents = [parent]
        gate = Mock(parent=directory)
        # Mock's reserved parent parameter is not a filesystem parent attribute.
        gate.parent = directory
        with patch.object(renewal, 'GATE', gate):
            renewal.validate_gate_directory(type('User', (), {'pw_uid':999,'pw_gid':988})())
            directory.lstat.return_value.st_mode = 0o40777
            with self.assertRaises(ValueError):
                renewal.validate_gate_directory(type('User', (), {'pw_uid':999,'pw_gid':988})())

    def test_receipt_is_outside_service_owned_directory(self):
        self.assertNotEqual(renewal.RECEIPT.parent, renewal.GATE.parent)
        self.assertEqual(str(renewal.RECEIPT.parent), '/etc')

    def test_rejects_live_gate_wrong_key_and_bad_times(self):
        for changes, token, now in (({}, self.token, 150), ({}, 'b'*64, 201),
                ({'ExpiresUnix': float('nan')}, self.token, 201),
                ({'IssuedUnix': True}, self.token, 201),
                ({'ExpiresUnix': 100000}, self.token, 200000),
                ({'Extra': True}, self.token, 201)):
            with self.subTest(changes=changes, now=now), self.assertRaises(ValueError):
                renewal.renew_config(json.dumps(dict(self.gate, **changes)), token, now)

    def test_duplicate_gate_fields_rejected(self):
        with self.assertRaises(ValueError):
            renewal.renew_config(json.dumps(self.gate)[:-1]+',"Version":1}', self.token, 201)

    def test_non_root_does_not_write_or_control_services(self):
        with patch.object(renewal.os, 'geteuid', return_value=1000), \
                patch.object(renewal, 'write_new') as write, \
                patch.object(renewal, 'ctl') as ctl, self.assertRaises(ValueError):
            renewal.main()
        write.assert_not_called(); ctl.assert_not_called()

    def test_boundaries_include_wrong_key_and_admin(self):
        with patch.object(renewal, 'status', side_effect=[200,503,503,404,404,404,404]) as status:
            renewal.boundaries(self.token)
        self.assertIn(unittest.mock.call('/qa/healthz', '0'*64), status.call_args_list)
        self.assertIn(unittest.mock.call('/qa/admin', self.token), status.call_args_list)

    def test_unexpected_gate_never_replaced(self):
        with patch.object(renewal, 'safe_file', return_value=b'unknown'), \
                patch.object(renewal.tempfile, 'mkstemp') as create, \
                self.assertRaises(ValueError):
            renewal.replace_gate(b'expected', b'new', type('User', (), {'pw_uid': 999})())
        create.assert_not_called()

    def test_start_failure_stops_qa_and_restores_expired_gate(self):
        raw = json.dumps(self.gate).encode()
        renewed = renewal.renew_config(raw, self.token, 201)
        new_raw = json.dumps(renewed, sort_keys=True).encode()
        user = type('User', (), {'pw_uid': 999})()
        def ctl(*args):
            if args[0] == 'start': raise ValueError('failure')
        output = io.StringIO()
        with patch.object(renewal, 'ctl', side_effect=ctl) as control, \
                patch.object(renewal, 'prop', return_value='inactive'), \
                patch.object(renewal, 'replace_gate') as replace, \
                patch.object(renewal, 'safe_file', side_effect=[new_raw, raw]), \
                redirect_stdout(output), self.assertRaises(SystemExit):
            renewal.activate(raw, renewed, self.token, user, [])
        self.assertEqual(replace.call_args_list, [unittest.mock.call(raw,new_raw,user),
            unittest.mock.call(new_raw,raw,user)])
        self.assertEqual(control.call_args_list[-1], unittest.mock.call('stop', renewal.UNIT))
        self.assertIn('"ExpiredGateRestored": true', output.getvalue())
        self.assertNotIn(self.token, output.getvalue())

    def test_directory_sync_failure_after_replace_still_restores_gate(self):
        raw = json.dumps(self.gate).encode()
        renewed = renewal.renew_config(raw, self.token, 201)
        new_raw = json.dumps(renewed, sort_keys=True).encode()
        user = type('User', (), {'pw_uid': 999})()
        with patch.object(renewal, 'ctl'), \
                patch.object(renewal, 'prop', return_value='inactive'), \
                patch.object(renewal, 'replace_gate', side_effect=[OSError('fsync'), None]) as replace, \
                patch.object(renewal, 'safe_file', side_effect=[new_raw, raw]), \
                redirect_stdout(io.StringIO()), self.assertRaises(SystemExit):
            renewal.activate(raw, renewed, self.token, user, [])
        self.assertEqual(replace.call_count, 2)

    def test_success_does_not_touch_other_configs_or_print_key(self):
        raw = json.dumps(self.gate).encode()
        renewed = renewal.renew_config(raw, self.token, 201)
        new_raw = json.dumps(renewed, sort_keys=True).encode()
        output = io.StringIO()
        with patch.object(renewal, 'ctl') as control, \
                patch.object(renewal, 'prop', return_value='inactive'), \
                patch.object(renewal, 'replace_gate') as replace, \
                patch.object(renewal, 'status', return_value=200), \
                patch.object(renewal, 'boundaries') as boundaries, \
                patch.object(renewal, 'safe_file', return_value=new_raw), redirect_stdout(output):
            renewal.activate(raw, renewed, self.token, type('User', (), {'pw_uid':999})(), [])
        self.assertEqual(control.call_args_list, [unittest.mock.call('stop',renewal.UNIT),
            unittest.mock.call('start',renewal.UNIT)])
        replace.assert_called_once(); boundaries.assert_called_once_with(self.token)
        self.assertIn('QA_RENEWAL_VERIFIED', output.getvalue())
        self.assertNotIn(self.token, output.getvalue())
