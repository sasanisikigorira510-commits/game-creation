"""Offline recovery evidence chain: pinned head -> ciphertext -> inventory -> events.

The head digest must come from independently retained, reviewed evidence, NOT
from the downloaded head itself. Equality does not establish latestness after
loss of the live DB. This tool never decrypts, contacts storage, edits a DB or
clears recovery quarantine.
"""
import argparse
import hashlib
import hmac
import json
import os
import re

from deletion_event_restore import bounded_read, checkpoint_from_directory
from deletion_inventory import parse_inventory
from store import encode

MAX_INVENTORY = 512 * 1024
MAX_CIPHER = MAX_INVENTORY + 65536
HEAD_FIELDS = {'Version', 'InstanceId', 'Sequence', 'InventoryKey', 'CipherSha256',
               'CipherBytes', 'InventorySha256', 'Events', 'CreatedUtc', 'PreviousHeadSha256'}


def require(ok):
    if not ok: raise ValueError('Recovery evidence mismatch')


def sha(raw): return hashlib.sha256(raw).hexdigest()


def verify_publication(head_raw, expected_head_sha256, instance, ciphertext, inventory_raw):
    require(isinstance(head_raw, bytes) and 0 < len(head_raw) <= 4096
            and isinstance(expected_head_sha256, str)
            and re.fullmatch(r'[a-f0-9]{64}', expected_head_sha256)
            and hmac.compare_digest(sha(head_raw), expected_head_sha256))
    require(isinstance(instance, str) and re.fullmatch(r'[A-Za-z0-9_-]{8,80}', instance))
    head = json.loads(head_raw)
    require(isinstance(head, dict) and set(head) == HEAD_FIELDS
            and (encode(head)+'\n').encode() == head_raw)
    require(type(head['Version']) is int and head['Version'] == 1
            and head['InstanceId'] == instance and type(head['Sequence']) is int
            and 1 <= head['Sequence'] <= 99999999999999999999)
    for field in ('CipherSha256', 'InventorySha256', 'PreviousHeadSha256'):
        require(isinstance(head[field], str) and re.fullmatch(r'[a-f0-9]{64}', head[field]))
    require((head['PreviousHeadSha256'] == '0'*64) == (head['Sequence'] == 1))
    require(isinstance(ciphertext, bytes) and 64 <= len(ciphertext) <= MAX_CIPHER
            and ciphertext.startswith(b'age-encryption.org/v1\n')
            and type(head['CipherBytes']) is int and head['CipherBytes'] == len(ciphertext)
            and hmac.compare_digest(head['CipherSha256'], sha(ciphertext)))
    require(head['InventoryKey'] == 'deletion-inventories/'+instance+'/'
            +str(head['Sequence']).zfill(20)+'-'+sha(ciphertext)+'.age')
    require(isinstance(inventory_raw, bytes) and 0 < len(inventory_raw) <= MAX_INVENTORY
            and hmac.compare_digest(head['InventorySha256'], sha(inventory_raw)))
    inventory = parse_inventory(inventory_raw, instance)
    require(type(head['Events']) is int and head['Events'] == len(inventory['Events'])
            and head['CreatedUtc'] == inventory['CreatedUtc'])
    return head


def verify_files(head, expected_head_sha256, instance, ciphertext, inventory, events_dir, output):
    metadata = verify_publication(bounded_read(head, 4096), expected_head_sha256, instance,
                                  bounded_read(ciphertext, MAX_CIPHER),
                                  bounded_read(inventory, MAX_INVENTORY))
    # The existing checker rereads and rechecks the inventory digest, so changing
    # the plaintext between these two steps cannot substitute another inventory.
    result = checkpoint_from_directory(inventory, metadata['InventorySha256'], instance, events_dir, output)
    return dict(result, Status='PINNED_RECOVERY_EVIDENCE_VERIFIED',
                HeadSha256=expected_head_sha256, Sequence=metadata['Sequence'],
                QuarantineMustRemain=True, LatestCompletenessProven=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ('head', 'expected-head-sha256', 'instance-id', 'ciphertext', 'inventory', 'events-dir', 'output'):
        parser.add_argument('--'+option, required=True)
    args = parser.parse_args(); os.umask(0o077)
    print(json.dumps(verify_files(args.head, args.expected_head_sha256, args.instance_id,
          args.ciphertext, args.inventory, args.events_dir, args.output), sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('RECOVERY_EVIDENCE_STOPPED: inputs retained; no private details printed.')
        raise SystemExit(1)
