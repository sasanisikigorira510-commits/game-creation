"""Package only allowlisted application files; never sweep the workspace."""
import argparse
import hashlib
import io
import json
import tarfile
from pathlib import Path

FILES = '''application.py security.py production.py backup_health.py gunicorn.conf.py store.py server.py
maintenance.py refund_integrity.py purchase_refunds.py deletion_restore.py deletion_event_restore.py deletion_inventory.py deletion_recovery_evidence.py deletion_journal.py apple_verifier.py apple_identity.py apple_tokens.py apple_grants.py account_linking.py account_deletion.py catalog.json admin.html admin.js
apple_revocation_worker.py apple_revocation_runtime.py apple_revocation_health.py run_apple_revocation.py apple_application.py
apple_refund_verifier.py refund_review.py refund_reconciliation.py refund_runtime.py refund_health.py run_refund_worker.py
refund_migration.py run_refund_migration.py
refund_sandbox.py refund_lab_runtime.py qa_gateway.py qa_isolated_runtime.py
requirements-production.txt requirements-apple.txt requirements-identity.txt README.md DEPLOYMENT.md
deploy/Caddyfile deploy/witch-player.service deploy/witch-player-backup.service
deploy/witch-player-backup.timer deploy/caddy-env.conf deploy/server.env.example
deploy/caddy.env.example deploy/APPLE-ACCOUNT-LINKING.md deploy/APPLE-TOKEN-LIFECYCLE.md
deploy/APPLE-ACCOUNT-INTEGRATION-2026-09-25.md deploy/ACCOUNT-DELETION-2026-09-25.md
deploy/ACCOUNT-DELETION-CLIENT-2026-09-25.md deploy/ACCOUNT-DELETION-RESTORE-2026-09-25.md
deploy/sakura_deletion_journal.py deploy/ACCOUNT-DELETION-JOURNAL-2026-09-25.md
deploy/RETENTION-AND-RECOVERY.md deploy/APPLE-REVOCATION-WORKER.md deploy/APPLE-APPLICATION-ASSEMBLY.md
deploy/apple-worker.json.example deploy/witch-player-apple-revoke.service
deploy/witch-player-apple-revoke.timer deploy/requirements-iap-sandbox.lock
deploy/refund-worker.json.example deploy/witch-player-refunds.service.example deploy/witch-player-refunds.timer.example
deploy/REFUND-CONTROLS-2026-09-28.md deploy/REFUND-MIGRATION-2026-09-28.md
deploy/REFUND-SANDBOX-ASSEMBLY-2026-09-28.md'''.split()


def build(output):
    root = Path(__file__).parent
    contents = {}
    for name in FILES:
        source = root / name
        if source.is_symlink() or not source.is_file():
            raise ValueError('Release input must be a regular file: ' + name)
        contents[name] = source.read_bytes()
    manifest = {name: hashlib.sha256(data).hexdigest() for name, data in contents.items()}
    contents['SHA256.json'] = (json.dumps(manifest, indent=2, sort_keys=True) + '\n').encode()
    with Path(output).open('xb') as stream, tarfile.open(fileobj=stream, mode='w:gz') as archive:
        for name, data in contents.items():
            info = tarfile.TarInfo(name)
            info.size, info.mode = len(data), 0o644
            archive.addfile(info, io.BytesIO(data))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    print('Packaged', len(build(args.output)), 'files; no runtime data or credentials included.')
