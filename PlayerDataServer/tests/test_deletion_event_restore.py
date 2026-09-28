import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from deletion_event_restore import checkpoint_from_events, checkpoint_from_directory
from store import encode


class EventRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.instance = 'synthetic-inventory-001'
        self.event = dict(Version=1, InstanceId=self.instance, EventId='a' * 32,
                          PlayerHash='b' * 64, DeletedUtc='2026-09-25T00:00:00+00:00',
                          RetiredPurchases=['c' * 64])
        self.payload = (encode(self.event) + '\n').encode()
        self.events = {self.event['EventId']: self.payload}
        self.inventory = dict(Version=1, InstanceId=self.instance, CreatedUtc='2026-09-25T01:00:00+00:00',
            Events=[dict(EventId=self.event['EventId'], Bytes=len(self.payload),
                         Sha256=hashlib.sha256(self.payload).hexdigest())])

    def build(self, inventory=None, events=None, digest=None, instance=None):
        raw = (encode(self.inventory if inventory is None else inventory) + '\n').encode()
        return checkpoint_from_events(raw, digest or hashlib.sha256(raw).hexdigest(),
                                      instance or self.instance, self.events if events is None else events)

    def test_builds_only_tombstones_and_retired_purchases(self):
        value = json.loads(self.build())
        self.assertEqual([{'Hash': 'b'*64, 'DeletedUtc': self.event['DeletedUtc']}], value['DeletedPlayers'])
        self.assertEqual(['c'*64], value['RetiredPurchases'])
        self.assertNotIn('Events', value)

    def test_missing_extra_modified_and_old_inventory_are_rejected(self):
        for events in ({}, dict(self.events, extra=self.payload), {'a'*32: self.payload + b' '}):
            with self.assertRaises(ValueError): self.build(events=events)
        with self.assertRaises(ValueError): self.build(digest='0'*64)
        with self.assertRaises(ValueError): self.build(instance='different-instance')

    def test_duplicate_inventory_and_future_event_are_rejected(self):
        self.inventory['Events'] *= 2
        with self.assertRaises(ValueError): self.build()
        self.inventory['Events'] = self.inventory['Events'][:1]
        self.inventory['CreatedUtc'] = '2026-09-24T00:00:00+00:00'
        with self.assertRaises(ValueError): self.build()

    def test_bound_event_identity_cannot_be_substituted(self):
        changed = dict(self.event, EventId='d'*32)
        payload = (encode(changed)+'\n').encode()
        self.inventory['Events'][0].update(Sha256=hashlib.sha256(payload).hexdigest(), Bytes=len(payload))
        with self.assertRaises(ValueError): self.build(events={'a'*32: payload})

    def test_duplicate_json_keys_are_not_canonical(self):
        raw = (encode(self.inventory)[:-1] + ',"Version":1}\n').encode()
        with self.assertRaises(ValueError):
            checkpoint_from_events(raw, hashlib.sha256(raw).hexdigest(), self.instance, self.events)

    def disk_fixture(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        root = Path(temp.name).resolve()
        events = root / 'events'; events.mkdir(mode=0o700)
        event = events / ('a'*32 + '.json'); event.write_bytes(self.payload); event.chmod(0o600)
        raw = (encode(self.inventory)+'\n').encode()
        inventory = root / 'inventory.json'; inventory.write_bytes(raw); inventory.chmod(0o600)
        return root, events, event, inventory, hashlib.sha256(raw).hexdigest()

    def test_offline_directory_interface_and_no_overwrite(self):
        root, folder, _, inventory, digest = self.disk_fixture()
        output = root / 'checkpoint.json'
        result = checkpoint_from_directory(inventory, digest, self.instance, folder, output)
        self.assertEqual(self.build(), output.read_bytes())
        self.assertEqual(0o600, output.stat().st_mode & 0o777)
        self.assertFalse(result['LatestCompletenessProven'])
        with self.assertRaises(ValueError):
            checkpoint_from_directory(inventory, digest, self.instance, folder, output)

    def test_offline_directory_rejects_public_input(self):
        root, folder, event, inventory, digest = self.disk_fixture()
        event.chmod(0o644)
        with self.assertRaises(ValueError):
            checkpoint_from_directory(inventory, digest, self.instance, folder, root/'result.json')
        self.assertFalse((root/'result.json').exists())

    def test_offline_directory_rejects_symlink_and_extra_file(self):
        root, folder, event, inventory, digest = self.disk_fixture()
        extra = folder / ('d'*32+'.json'); extra.symlink_to(event)
        with self.assertRaises(ValueError):
            checkpoint_from_directory(inventory, digest, self.instance, folder, root/'result.json')
        extra.unlink(); extra.write_bytes(self.payload); extra.chmod(0o600)
        with self.assertRaises(ValueError):
            checkpoint_from_directory(inventory, digest, self.instance, folder, root/'result.json')


if __name__ == '__main__': unittest.main()
