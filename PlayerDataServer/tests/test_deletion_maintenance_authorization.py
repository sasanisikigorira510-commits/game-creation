from contextlib import ExitStack, redirect_stdout
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from deploy import authorize_deletion_maintenance as a
from deploy import deletion_maintenance as m


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.stack = ExitStack(); self.addCleanup(self.stack.close)
        for name, value in [('ROOT', root / 'state'), ('SOURCE', root / 'source'),
                            ('HELPER', root / 'helper'), ('POLICY', root / 'sudoers')]:
            self.stack.enter_context(patch.object(a, name, value))
        a.SOURCE.write_bytes(Path(m.__file__).read_bytes())
        self.stack.enter_context(patch.object(a, 'safe_parent'))
        self.stack.enter_context(patch.object(os, 'geteuid', return_value=0))
        self.stack.enter_context(redirect_stdout(io.StringIO()))
        self.mask = os.umask(0o077); self.addCleanup(os.umask, self.mask)

    def test_payload_pin_matches_reviewed_source(self):
        self.assertEqual(a.EXPECTED, hashlib.sha256(a.SOURCE.read_bytes()).hexdigest())

    def test_policy_allows_only_four_pinned_exact_actions(self):
        policy = a.policy_bytes().decode()
        line = policy.splitlines()[1]
        commands = line.split('NOPASSWD: ', 1)[1].split(', ')
        self.assertEqual([f'sha256:{a.EXPECTED} {a.HELPER} {x}' for x in m.ACTIONS], commands)
        self.assertNotIn('*', policy)
        self.assertNotIn('/bin/bash', policy)

    def test_install_does_not_run_helper_and_sets_private_expiring_grant(self):
        with patch.object(a, 'checked') as checked: a.main()
        self.assertEqual(a.SOURCE.read_bytes(), a.HELPER.read_bytes())
        self.assertEqual(a.policy_bytes(), a.POLICY.read_bytes())
        grant = json.loads((a.ROOT / 'grant.json').read_text())
        self.assertEqual(1, grant['Version'])
        self.assertEqual(hashlib.sha256(a.POLICY.read_bytes()).hexdigest(), grant['PolicySha256'])
        self.assertEqual(0o600, (a.ROOT / 'grant.json').stat().st_mode & 0o777)
        self.assertTrue(all(call.args[0][0] == '/usr/sbin/visudo' for call in checked.call_args_list))

    def test_bad_hash_stops_without_grant(self):
        a.SOURCE.write_text('tampered')
        with patch.object(a, 'checked'):
            with self.assertRaises(ValueError): a.main()
        self.assertFalse(a.POLICY.exists()); self.assertFalse(a.ROOT.exists())

    def test_existing_setup_is_never_overwritten_or_extended(self):
        a.ROOT.mkdir(); marker = a.ROOT / 'grant.json'; marker.write_text('existing')
        with self.assertRaises(ValueError): a.main()
        self.assertEqual('existing', marker.read_text())

    def test_invalid_published_policy_is_removed_from_sudoers(self):
        with patch.object(a, 'checked', side_effect=[None, None, RuntimeError('fixture')]):
            with self.assertRaises(RuntimeError): a.main()
        self.assertFalse(a.POLICY.exists())
        self.assertEqual(a.policy_bytes(), (a.ROOT / 'authorization.invalid').read_bytes())


if __name__ == '__main__': unittest.main()
