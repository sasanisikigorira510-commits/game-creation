"""Real age and SQLite; simulated transport here, not cloud evidence."""
import json
import os
from pathlib import Path
import tempfile
import unittest

from deploy.deletion_restore_drill import prepare, complete, save, save_bytes


@unittest.skipUnless(os.environ.get('NASUS_TEST_AGE'), 'Needs explicit test age executable')
class SyntheticDrillTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve() / 'drill'
        self.age = Path(os.environ['NASUS_TEST_AGE'])
        self.prepared = prepare(self.root, self.age)

    def returned(self):
        folder = self.root / 'cloud-return'; folder.mkdir(mode=0o700)
        transfer = json.loads((self.root / 'transfer.json').read_text())
        save(folder / 'receipt.json', dict(transfer, Status='SYNTHETIC_PROBE_READBACK_VERIFIED'))
        save_bytes(folder / 'readback.age', (self.root / 'outgoing.age').read_bytes())

    def test_complete_real_encryption_and_quarantined_restore(self):
        self.returned()
        report = complete(self.root, self.age)
        self.assertEqual('SYNTHETIC_DELETION_RESTORE_VERIFIED', report['Status'])
        self.assertFalse(report['ProductionDataUsed'])
        self.assertTrue(report['SurvivorUnchanged'])
        self.assertEqual(0o600, (self.root / 'synthetic-identity.txt').stat().st_mode & 0o777)
        with self.assertRaises(ValueError): complete(self.root, self.age)

    def test_changed_ciphertext_stops_before_restore(self):
        self.returned()
        path = self.root / 'cloud-return/readback.age'
        path.write_bytes(path.read_bytes()[:-1] + b'!')
        with self.assertRaises(ValueError): complete(self.root, self.age)
        self.assertFalse((self.root / 'quarantined-restore').exists())

    def test_changed_inventory_stops_before_restore(self):
        self.returned()
        (self.root / 'independent-inventory.json').write_text('{}\n')
        with self.assertRaises(ValueError): complete(self.root, self.age)
        self.assertFalse((self.root / 'quarantined-restore').exists())

    def test_never_overwrites_existing_fixture(self):
        with self.assertRaises(FileExistsError): prepare(self.root, self.age)

    def test_missing_return_does_not_claim_success(self):
        with self.assertRaises(FileNotFoundError): complete(self.root, self.age)
        self.assertFalse((self.root / 'verified.json').exists())
