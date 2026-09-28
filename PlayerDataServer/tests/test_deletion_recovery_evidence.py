import json
from pathlib import Path
import tempfile
import unittest

from deletion_inventory_ledger import InventoryLedger, initialize_ledger, canonical, digest
from deletion_recovery_evidence import verify_publication, verify_files


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve(); self.instance = 'recovery-evidence-fixture'
        self.event = canonical(dict(Version=1, InstanceId=self.instance, EventId='a'*32,
            PlayerHash='b'*64, DeletedUtc='2026-09-26T00:00:00+00:00', RetiredPurchases=['c'*64]))
        baseline = dict(Version=1, InstanceId=self.instance, CreatedUtc='2026-09-26T00:01:00+00:00', Events=[])
        self.raw = canonical(dict(baseline, Events=[dict(EventId='a'*32, Sha256=digest(self.event), Bytes=len(self.event))]))
        class Remote:
            def __init__(self): self.objects = {}
            def get(self, key): return self.objects.get(key)
            def put(self, key, raw): self.objects[key] = raw
        self.remote = Remote()
        initialize_ledger(self.root/'ledger', self.instance, canonical(baseline))
        ledger = InventoryLedger(self.root/'ledger', self.instance,
            lambda raw: b'age-encryption.org/v1\n'+digest(raw).encode(), self.remote)
        self.receipt = ledger.publish(self.raw)
        self.head_raw = self.remote.get(ledger.head_key)
        self.head = json.loads(self.head_raw); self.cipher = self.remote.get(self.head['InventoryKey'])

    def verify(self, head=None, pin=None, instance=None, cipher=None, raw=None):
        return verify_publication(self.head_raw if head is None else head,
            self.receipt['HeadSha256'] if pin is None else pin,
            self.instance if instance is None else instance,
            self.cipher if cipher is None else cipher, self.raw if raw is None else raw)

    def test_accepts_actual_ledger_publication_shape(self):
        self.assertEqual(self.head, self.verify())

    def test_rejects_wrong_pin_instance_cipher_or_plaintext(self):
        for args in (dict(pin='0'*64), dict(instance='other-instance'), dict(cipher=self.cipher+b'x'),
                     dict(raw=self.raw+b' '), dict(head=self.head_raw+b' ')):
            with self.subTest(args=list(args)):
                with self.assertRaises(ValueError): self.verify(**args)

    def test_rejects_head_inconsistent_with_pinned_content(self):
        for key, value in [('Version', True), ('Sequence', True), ('Sequence', 0),
                           ('InventoryKey', '../escape'), ('CipherBytes', True), ('Events', 2),
                           ('PreviousHeadSha256', 'f'*64), ('CreatedUtc', 'bad'), ('Extra', 1)]:
            head = canonical(dict(self.head, **{key:value}))
            with self.subTest(key=key):
                with self.assertRaises(ValueError): self.verify(head=head, pin=digest(head))

    def test_rejects_duplicate_json_and_oversized_input(self):
        head = self.head_raw[:-2]+b',"Version":1}\n'
        with self.assertRaises(ValueError): self.verify(head=head, pin=digest(head))
        with self.assertRaises(ValueError): self.verify(head=b'x'*4097, pin=digest(b'x'*4097))

    def disk_fixture(self):
        for name, raw in [('head', self.head_raw), ('cipher', self.cipher), ('inventory', self.raw)]:
            path = self.root/name; path.write_bytes(raw); path.chmod(0o600)
        events = self.root/'events'; events.mkdir(mode=0o700)
        path = events/('a'*32+'.json'); path.write_bytes(self.event); path.chmod(0o600)
        return (self.root/'head', self.receipt['HeadSha256'], self.instance, self.root/'cipher',
                self.root/'inventory', events, self.root/'checkpoint')

    def test_complete_file_chain_preserves_quarantine_no_overwrite(self):
        args = self.disk_fixture(); result = verify_files(*args)
        self.assertEqual('PINNED_RECOVERY_EVIDENCE_VERIFIED', result['Status'])
        self.assertFalse(result['LatestCompletenessProven']); self.assertTrue(result['QuarantineMustRemain'])
        self.assertFalse(result['ProductionDataChanged'])
        value = json.loads(args[-1].read_bytes())
        self.assertEqual('b'*64, value['DeletedPlayers'][0]['Hash'])
        self.assertEqual(['c'*64], value['RetiredPurchases'])
        self.assertEqual(0o600, args[-1].stat().st_mode & 0o777)
        with self.assertRaises(ValueError): verify_files(*args)

    def test_missing_or_modified_event_produces_no_checkpoint(self):
        args = self.disk_fixture(); path = args[-2]/('a'*32+'.json')
        path.write_bytes(b'changed')
        with self.assertRaises(ValueError): verify_files(*args)
        self.assertFalse(args[-1].exists()); path.unlink()
        with self.assertRaises(ValueError): verify_files(*args)
        self.assertFalse(args[-1].exists())

    def test_public_or_symlink_evidence_rejected(self):
        args = self.disk_fixture(); args[0].chmod(0o644)
        with self.assertRaises(ValueError): verify_files(*args)
        args[0].chmod(0o600); link = self.root/'link'; link.symlink_to(args[0])
        with self.assertRaises(ValueError): verify_files(link, *args[1:])
        self.assertFalse(args[-1].exists())

    def test_old_head_cannot_match_new_independent_receipt(self):
        new = canonical(dict(self.head, Sequence=2, PreviousHeadSha256=digest(self.head_raw)))
        with self.assertRaises(ValueError): self.verify(pin=digest(new))


if __name__ == '__main__': unittest.main()
