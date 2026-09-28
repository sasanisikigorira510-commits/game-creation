"""Explicit Sandbox assembly with separate device, review and notification surfaces.

Not a deployment entry point. No migration, gate renewal, timer activation or
proxy registration. Callers must bind each returned surface separately.
"""
from dataclasses import dataclass
import json
from pathlib import Path

from apple_application import create_apple_app
from apple_revocation_runtime import load_components, private, read_private, require, unique
from apple_verifier import build_verifier
from apple_refund_verifier import build_notification_verifier
from purchase_refunds import PurchaseRefunds
from refund_review import RefundReview
from refund_runtime import load
from refund_health import check_systemd
from qa_gateway import QaGateway
from qa_isolated_runtime import NoDeletion


class Endpoint:
    """No general admin/player access through notification or review listeners."""
    def __init__(self, app, gate, paths):
        self.app, self.gate, self.paths = app, gate, frozenset(paths)

    def __call__(self, env, start):
        g = self.gate
        valid = g.issued <= g.clock() < g.expires and g.monotonic() < g.deadline
        if (env.get('REQUEST_METHOD') != 'POST' or env.get('PATH_INFO') not in self.paths or not valid):
            # Expired notifications are not acknowledged as processed; retry later.
            status = '404 Not Found' if valid else '503 Service Unavailable'
            body = b'{"Error":"Not found"}' if valid else b'{"Error":"Service unavailable"}'
            start(status, [('Content-Type', 'application/json'), ('Cache-Control', 'no-store'),
                           ('Content-Length', str(len(body)))])
            return [b'' if env.get('REQUEST_METHOD') == 'HEAD' else body]
        return self.app(env, start)


@dataclass(frozen=True)
class SandboxApps:
    device: object
    review: object
    notifications: object


def create_sandbox_apps(environ, *, components=load_components,
                        purchase_builder=build_verifier, notification_builder=build_notification_verifier,
                        health=check_systemd):
    require(environ.get('WITCH_REFUND_SANDBOX_ENABLED') == '1')
    required = ('WITCH_REFUND_WORKER_CONFIG', 'WITCH_QA_GATE_CONFIG', 'WITCH_APPLE_CONFIG',
                'WITCH_DATA_DIR', 'WITCH_INSTANCE_ID', 'WITCH_PUBLIC_ORIGIN',
                'WITCH_OPERATORS_FILE', 'WITCH_BACKUP_HEALTH_FILE')
    require(all(isinstance(environ.get(k), str) and environ[k] for k in required))
    # Expired gate fails before loading signing credentials or constructing providers.
    gate = QaGateway(None, json.loads(read_private(environ['WITCH_QA_GATE_CONFIG'], 4096), object_pairs_hook=unique))
    config, checked_store, state, apple = load(environ['WITCH_REFUND_WORKER_CONFIG'])
    require(config['Environment'] == 'Sandbox' and apple['WITCH_APPLE_ENVIRONMENT'] == 'Sandbox')
    require(environ['WITCH_DATA_DIR'] == str(checked_store.directory)
            and environ['WITCH_INSTANCE_ID'] == config['InstanceId'])
    marker = json.loads(read_private(checked_store.directory/'SANDBOX-ONLY.json', 4096), object_pairs_hook=unique)
    require(isinstance(marker, dict) and marker.get('Purpose') == 'SANDBOX_ONLY_NOT_PRODUCTION'
            and marker.get('InstanceId') == config['InstanceId']
            and marker.get('ProductionMigrationApproved') is False)
    identity = json.loads(read_private(environ['WITCH_APPLE_CONFIG'], 8192), object_pairs_hook=unique)
    require(identity['DataDirectory'] == config['DataDirectory']
            and identity['InstanceId'] == config['InstanceId']
            and identity['ClientId'] == apple['WITCH_APPLE_BUNDLE_ID'])
    identity_state = private(identity['StateDirectory'], True)
    require(state != identity_state and state not in identity_state.parents and identity_state not in state.parents)
    backup = Path(environ['WITCH_BACKUP_HEALTH_FILE'])
    require(backup.is_absolute() and backup not in (state/'status.json', identity_state/'status.json'))
    # Refuse inherited conflicting purchase settings instead of silently overriding them.
    require(all(k not in environ or environ[k] == v for k, v in apple.items()))
    private(environ['WITCH_OPERATORS_FILE'])

    def same_components(path, readonly=False):
        vault, client, account_state = components(path, readonly=readonly)
        require(vault.store.directory == checked_store.directory
                and vault.store.instance == config['InstanceId']
                and client.client_id == apple['WITCH_APPLE_BUNDLE_ID']
                and account_state == identity_state)
        # The full preflight above verified the ledger. Never initialize a Store.
        vault.store.refunds_enabled = True
        return vault, client, account_state

    purchase = purchase_builder(apple, sandbox_only=True)
    notification = notification_builder(apple, sandbox_only=True)
    require(callable(purchase) and callable(notification) and callable(health))
    app = create_apple_app(dict(environ, **apple), components=same_components, purchase_verifier=purchase)
    app.purchase_notifications = PurchaseRefunds(app.store, notification)
    app.refund_review = RefundReview(app.store)
    app.refund_health = lambda: health(state/'status.json', config['InstanceId'], 'Sandbox')
    gate.app = NoDeletion(app)  # Offsite deletion journal has not been provisioned for this Sandbox.
    return SandboxApps(gate, Endpoint(app, gate, ('/admin/refunds/preview', '/admin/refunds/commit')),
                       Endpoint(app, gate, ('/v1/store/apple/notifications',)))
