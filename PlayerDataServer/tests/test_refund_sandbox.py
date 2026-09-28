"""Sandbox wiring tests with private disposable DB/keys and synthetic providers."""
import hashlib
from dataclasses import replace
import io
import json
from pathlib import Path
import sqlite3
import time
import unittest
from unittest.mock import patch
import uuid

from refund_sandbox import create_sandbox_apps
from store import Store, Fault
from purchase_refunds import RefundState
from tests import test_apple_application as fixture


class RefundSandboxTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.AppleApplicationTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.root, self.data, self.env = self.f.root, self.f.data, dict(self.f.env)
        self.store = Store(self.data/'players.sqlite', refunds_enabled=True)
        for path in self.data.iterdir(): path.chmod(0o600)
        self.state = self.root/'refund-state'; self.state.mkdir(mode=0o700)
        self.roots = self.root/'iap-roots'; self.roots.mkdir(mode=0o700)
        self.write(self.roots/'root.cer', b'not-a-real-certificate')
        self.write(self.root/'iap.key', b'not-a-real-iap-key')
        self.apple = dict(WITCH_APPLE_ENVIRONMENT='Sandbox', WITCH_APPLE_BUNDLE_ID='com.test.game',
            WITCH_APPLE_KEY_FILE=str(self.root/'iap.key'), WITCH_APPLE_ROOTS_DIR=str(self.roots),
            WITCH_APPLE_KEY_ID='FIXTUREKEY', WITCH_APPLE_ISSUER_ID='fixture-issuer')
        self.config = dict(Version=1, Environment='Sandbox', BatchLimit=2, InstanceId=self.f.config['InstanceId'],
            DataDirectory=str(self.data), StateDirectory=str(self.state), AppleConfigFile=str(self.root/'iap.json'))
        self.marker = dict(Purpose='SANDBOX_ONLY_NOT_PRODUCTION', InstanceId=self.config['InstanceId'],
                           ProductionMigrationApproved=False)
        self.gatekey = 'c'*64
        self.gate = dict(Version=1, TokenSha256=hashlib.sha256(self.gatekey.encode()).hexdigest(),
                         IssuedUnix=time.time()-1, ExpiresUnix=time.time()+600)
        self.env.update(WITCH_REFUND_SANDBOX_ENABLED='1', WITCH_REFUND_WORKER_CONFIG=str(self.root/'refund.json'),
                        WITCH_QA_GATE_CONFIG=str(self.root/'gate.json'))
        self.save()
        self.builders = []; self.health_calls = []; self.healthy = True; self.event = None
        self.player, self.token = uuid.uuid4().hex, 'b'*64
        self.store.register(self.player, self.token)
        self.store.snapshot(self.player, dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0,
            EconomyRevision=0, SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900,
            PaidGachaStones=0, OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100))
        self.admin = (self.root/'operator-token').read_text().strip()

    def write(self, path, value): self.f.write(path, value)
    def save(self):
        for path, value in [(self.root/'iap.json', self.apple), (self.root/'refund.json', self.config),
                            (self.root/'gate.json', self.gate), (self.data/'SANDBOX-ONLY.json', self.marker)]:
            self.write(path, json.dumps(value).encode())
    def purchase(self, config, *, sandbox_only):
        self.builders.append(('purchase', dict(config), sandbox_only))
        return lambda r: dict(verified=True, store='apple', transaction=r['TransactionId'], product=r['Target'])
    def notification(self, config, *, sandbox_only):
        self.builders.append(('notification', dict(config), sandbox_only))
        def verify(payload):
            if payload != 'synthetic-valid': raise Fault(400, 'Invalid signature')
            return self.event
        return verify
    def health(self, *args): self.health_calls.append(args); return self.healthy
    def build(self):
        return create_sandbox_apps(self.env, components=self.f.components, purchase_builder=self.purchase,
                                   notification_builder=self.notification, health=self.health)
    def call(self, surface, path, body=None, token='', qa=False, **changes):
        raw = json.dumps(body or {}).encode(); status = []
        auth = 'Bearer '+token
        if qa: auth = 'NasusQA '+self.gatekey+' '+auth
        env = dict(REQUEST_METHOD='GET' if body is None else 'POST', PATH_INFO=path,
            HTTP_HOST='api.example.com', REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='203.0.113.5',
            CONTENT_TYPE='application/json', CONTENT_LENGTH=str(len(raw)), HTTP_AUTHORIZATION=auth,
            **{'wsgi.input':io.BytesIO(raw), 'wsgi.url_scheme':'https'})
        env.update(changes)
        result = b''.join(surface(env, lambda s,h: status.append(int(s[:3]))))
        return status[0], json.loads(result) if result else None

    def test_construction_is_offline_unchanged_and_shares_database(self):
        with self.store.connect() as db: before = list(db.iterdump())
        with patch('requests.sessions.Session.request', side_effect=AssertionError('no network')):
            apps = self.build()
        app = apps.review.app
        self.assertIs(app, apps.notifications.app); self.assertIs(app, apps.device.app.app)
        for service in (app.purchase_notifications, app.refund_review, app.account_linking.vault,
                        app.account_deletion.journal): self.assertIs(app.store, service.store)
        self.assertTrue(app.store.refunds_enabled)
        self.assertEqual([('purchase', self.apple, True), ('notification', self.apple, True)], self.builders)
        with self.store.connect() as db: self.assertEqual(before, list(db.iterdump()))
        self.assertEqual([], self.health_calls)

    def test_explicit_opt_in_and_sandbox_only(self):
        self.env.pop('WITCH_REFUND_SANDBOX_ENABLED')
        with self.assertRaises(ValueError): self.build()
        self.env['WITCH_REFUND_SANDBOX_ENABLED'] = '1'
        self.config['Environment'] = self.apple['WITCH_APPLE_ENVIRONMENT'] = 'Production'
        self.apple['WITCH_APPLE_APP_ID'] = '123'; self.save()
        with self.assertRaises(ValueError): self.build()
        self.assertEqual([], self.builders)

    def test_expired_gate_rejects_before_reading_worker_or_keys(self):
        self.gate.update(IssuedUnix=time.time()-600, ExpiresUnix=time.time()-1); self.save()
        with patch('refund_sandbox.load', side_effect=AssertionError('too late')):
            with self.assertRaises(ValueError): self.build()
        self.assertEqual([], self.builders)

    def test_database_instance_bundle_and_inherited_environment_must_match(self):
        for key, value in [('WITCH_DATA_DIR', str(self.root)), ('WITCH_INSTANCE_ID', 'other-instance'),
                           ('WITCH_APPLE_ENVIRONMENT', 'Production'), ('WITCH_APPLE_BUNDLE_ID', 'other.bundle')]:
            old = dict(self.env); self.env[key] = value
            with self.assertRaises(ValueError): self.build()
            self.env = old
        self.apple['WITCH_APPLE_BUNDLE_ID'] = 'other.bundle'; self.save()
        with self.assertRaises(ValueError): self.build()
        self.assertEqual([], self.builders)

    def test_sandbox_marker_and_separate_health_paths_required(self):
        self.marker['ProductionMigrationApproved'] = True; self.save()
        with self.assertRaises(ValueError): self.build()
        self.marker['ProductionMigrationApproved'] = False; self.save()
        self.config['StateDirectory'] = str(self.f.state); self.save()
        with self.assertRaises(ValueError): self.build()
        self.config['StateDirectory'] = str(self.state); self.save()
        self.env['WITCH_BACKUP_HEALTH_FILE'] = str(self.state/'status.json')
        with self.assertRaises(ValueError): self.build()

    def test_quarantine_and_missing_ledger_never_initialized(self):
        marker = self.data/'RECOVERY-PENDING.txt'; self.write(marker, b'quarantine')
        with self.assertRaises(ValueError): self.build()
        marker.unlink()
        with self.store.connect() as db: db.execute('DROP TABLE refund_checks')
        with self.assertRaises(ValueError): self.build()
        with self.store.connect() as db:
            self.assertIsNone(db.execute("SELECT name FROM sqlite_master WHERE name='refund_checks'").fetchone())
        self.assertEqual([], self.builders)

    def test_surfaces_cannot_be_used_as_general_admin_or_device_api(self):
        apps = self.build(); player_path = '/v1/players/'+self.player+'/head'
        self.assertEqual(404, self.call(apps.device, player_path, token=self.token)[0])
        self.assertEqual(200, self.call(apps.device, player_path, token=self.token, qa=True)[0])
        self.assertEqual(401, self.call(apps.device, player_path, token='bad', qa=True)[0])
        for path in ('/admin/session', '/admin/refunds/preview', '/v1/store/apple/notifications'):
            self.assertEqual(404, self.call(apps.device, path, {}, self.admin, qa=True)[0])
        for surface in (apps.review, apps.notifications):
            for path in (player_path, '/admin/session', '/v1/accounts', '/'):
                self.assertEqual(404, self.call(surface, path, {}, self.admin)[0])
        self.assertEqual(404, self.call(apps.notifications, '/admin/refunds/preview', {}, self.admin)[0])
        self.assertEqual(404, self.call(apps.review, '/v1/store/apple/notifications', {})[0])

    def test_named_review_auth_and_notification_signature_are_required(self):
        apps = self.build()
        self.assertEqual(401, self.call(apps.review, '/admin/refunds/preview', {}, self.token)[0])
        self.assertEqual(400, self.call(apps.notifications, '/v1/store/apple/notifications',
                                       dict(signedPayload='untrusted'))[0])
        self.assertEqual(404, self.call(apps.notifications, '/v1/store/apple/notifications/', {})[0])
        self.assertEqual(403, self.call(apps.notifications, '/v1/store/apple/notifications',
                                       dict(signedPayload='synthetic-valid'), **{'wsgi.url_scheme':'http'})[0])

    def test_expiry_closes_all_surfaces_without_acknowledging_notification(self):
        apps = self.build(); apps.device.deadline = 0
        self.assertEqual(404, self.call(apps.device, '/healthz', qa=True)[0])
        self.assertEqual(503, self.call(apps.review, '/admin/refunds/preview', {}, self.admin)[0])
        self.assertEqual(503, self.call(apps.notifications, '/v1/store/apple/notifications',
                                       dict(signedPayload='synthetic-valid'))[0])

    def test_health_includes_worker_but_failure_does_not_block_player_reads(self):
        apps = self.build()
        self.assertEqual(200, self.call(apps.device, '/healthz', qa=True)[0])
        self.assertEqual([(self.state/'status.json', self.config['InstanceId'], 'Sandbox')], self.health_calls)
        self.healthy = False
        self.assertEqual(503, self.call(apps.device, '/healthz', qa=True)[0])
        self.assertEqual(200, self.call(apps.device, '/v1/players/'+self.player+'/head', token=self.token, qa=True)[0])
        self.assertEqual(503, self.call(apps.device, '/v1/account-deletion/preview',
                                       dict(PlayerId=self.player), self.token, qa=True)[0])

    def test_purchase_notification_and_review_share_one_ledger(self):
        apps = self.build(); product = 'com.nasus.dungeonmonsterroguelike.crystals650'
        def operation(body):
            return self.call(apps.device, '/v1/players/'+self.player+'/operations',
                             dict(RequestId=uuid.uuid4().hex, Epoch=0, **body), self.token, qa=True)
        self.assertEqual(200, operation(dict(Kind='purchase', Target=product,
                                            TransactionId='sandbox-fixture', Receipt='synthetic'))[0])
        self.assertEqual(200, operation(dict(Kind='gacha', Count=1, Paid=True))[0])
        self.event = RefundState(str(uuid.uuid4()), hashlib.sha256(b'fixture').hexdigest(),
                                 'sandbox-fixture', self.player, product, 1000, 100000)
        self.assertEqual(200, self.call(apps.notifications, '/v1/store/apple/notifications',
                                       dict(signedPayload='synthetic-valid'))[0])
        self.assertEqual(402, operation(dict(Kind='gacha', Count=1, Paid=True))[0])
        self.assertEqual(200, operation(dict(Kind='gacha', Count=1, Paid=False))[0])
        body = dict(PlayerId=self.player, TransactionId='sandbox-fixture')
        code, preview = self.call(apps.review, '/admin/refunds/preview', body, self.admin)
        self.assertEqual(200, code); self.assertEqual(300, preview['MaxWaiver'])
        body.update(Amount=300, ExpectedEconomyRevision=apps.review.app.store.head(self.player)['EconomyRevision'],
                    RequestId=str(uuid.uuid4()), Case='sandbox-case', Confirm=True)
        self.assertEqual(200, self.call(apps.review, '/admin/refunds/commit', body, self.admin)[0])
        head = self.call(apps.device, '/v1/players/'+self.player+'/head', token=self.token, qa=True)[1]
        self.assertEqual(0, head['RefundDebt']); self.assertEqual(0, head['Paid'])
        # The separately constructed periodic worker must use the same DB and
        # absorb the waiver when Apple reverses this refund, not grant 650 again.
        from refund_runtime import execute
        reverse = replace(self.event, notification_id=str(uuid.uuid4()), signed_at=2000, percentage=0)
        def checker(config, *, sandbox_only):
            self.assertEqual(self.apple, config); self.assertTrue(sandbox_only)
            return lambda p: reverse
        result = execute(self.root/'refund.json', run=True, checker_factory=checker)
        self.assertTrue(result['Healthy'])
        head = self.call(apps.device, '/v1/players/'+self.player+'/head', token=self.token, qa=True)[1]
        self.assertEqual((350, 0, 600), (head['Paid'], head['RefundDebt'], head['Free']))

    def test_missing_required_configuration_has_no_provider_side_effects(self):
        for key in ('WITCH_QA_GATE_CONFIG', 'WITCH_REFUND_WORKER_CONFIG', 'WITCH_OPERATORS_FILE',
                    'WITCH_BACKUP_HEALTH_FILE', 'WITCH_APPLE_CONFIG'):
            old = self.env.pop(key)
            with self.assertRaises(ValueError): self.build()
            self.env[key] = old
        self.assertEqual([], self.builders)


if __name__ == '__main__': unittest.main()
