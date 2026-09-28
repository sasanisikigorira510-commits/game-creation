import io
import json
from pathlib import Path
import unittest
import uuid

from application import Application
from maintenance import add_operator
from refund_review import RefundReview
from security import AdminAuth
from tests import test_purchase_refunds as fixture


class RefundAdminTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.RefundTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.f.draw(); self.f.service.apply(self.f.event)
        self.root = Path(self.f.tmp.name)
        self.operators = self.root/'operators.json'
        add_operator(self.operators, 'support.one@example.test', 'operator', self.root/'write')
        add_operator(self.operators, 'viewer', 'viewer', self.root/'read')
        self.writer = (self.root/'write').read_text().strip()
        self.viewer = (self.root/'read').read_text().strip()
        self.auth = AdminAuth(path=self.operators)
        self.review = RefundReview(self.f.store)
        self.app = Application(self.f.store, self.auth, public_origin='https://example.test', refund_review=self.review)
        self.preview = dict(PlayerId=self.f.player, TransactionId=self.f.event.transaction)
        self.commit = dict(self.preview, Amount=300, ExpectedEconomyRevision=3,
                          RequestId=str(uuid.uuid4()), Case='case-123', Confirm=True)

    def call(self, route, body, token=None, **changes):
        raw = json.dumps(body).encode(); output = []
        env = dict(REQUEST_METHOD='POST', PATH_INFO=route, HTTP_HOST='example.test',
            REMOTE_ADDR='127.0.0.1', HTTP_X_REAL_IP='203.0.113.15',
            HTTP_AUTHORIZATION='Bearer ' + (self.writer if token is None else token),
            CONTENT_TYPE='application/json', CONTENT_LENGTH=str(len(raw)),
            **{'wsgi.input':io.BytesIO(raw), 'wsgi.url_scheme':'https'})
        env.update(changes)
        raw = b''.join(self.app(env, lambda s,h: output.append((int(s[:3]), dict(h)))))
        return output[0][0], json.loads(raw) if raw else None

    def test_named_operator_preview_commit_and_replay(self):
        code, preview = self.call('/admin/refunds/preview', self.preview)
        self.assertEqual(200, code); self.assertEqual(300, preview['MaxWaiver'])
        a = self.call('/admin/refunds/commit', self.commit)
        self.assertEqual(200, a[0]); self.assertEqual(a, self.call('/admin/refunds/commit', self.commit))
        self.assertEqual((0, 0, 900), self.f.wallet())
        with self.f.store.connect() as db:
            self.assertEqual('support.one@example.test', db.execute("SELECT actor FROM audit WHERE action='refund_waiver'").fetchone()[0])

    def test_viewer_player_anonymous_and_shared_local_token_cannot_review(self):
        for token, expected in [(self.viewer,403), (self.f.token,401), ('',401)]:
            for action, body in [('preview',self.preview), ('commit',self.commit)]:
                self.assertEqual(expected, self.call('/admin/refunds/'+action, body, token)[0])
        self.app = Application(self.f.store, AdminAuth(token='shared'), refund_review=self.review)
        self.assertEqual(403, self.call('/admin/refunds/commit', self.commit, 'shared')[0])
        self.assertEqual((0, 300, 900), self.f.wallet())

    def test_disabled_by_default_and_different_database_rejected(self):
        self.app = Application(self.f.store, self.auth)
        self.assertEqual(404, self.call('/admin/refunds/commit', self.commit)[0])
        from types import SimpleNamespace
        with self.assertRaises(ValueError): Application(self.f.store, self.auth, refund_review=SimpleNamespace(store=None))

    def test_forged_actor_extra_fields_and_missing_confirmation_rejected(self):
        for body in [dict(self.commit, Actor='boss'), dict(self.commit, Confirm=False),
                     dict(self.commit, Confirm=1), dict(self.commit, Amount=True)]:
            self.assertEqual(400, self.call('/admin/refunds/commit', body)[0])
        self.assertEqual((0, 300, 900), self.f.wallet())

    def test_revoked_operator_takes_effect_without_restart(self):
        entries = json.loads(self.operators.read_text())
        entries[0]['TokenHash'] = '0'*64
        self.operators.write_text(json.dumps(entries))
        self.assertEqual(401, self.call('/admin/refunds/commit', self.commit)[0])

    def test_preview_cannot_authorize_stale_commit(self):
        self.assertEqual(200, self.call('/admin/refunds/preview', self.preview)[0])
        self.assertEqual(200, self.call('/admin/refunds/commit', dict(self.commit, Amount=100))[0])
        self.assertEqual(409, self.call('/admin/refunds/commit',
                         dict(self.commit, RequestId=str(uuid.uuid4())))[0])
        self.assertEqual((0, 200, 900), self.f.wallet())

    def test_preview_and_commit_share_named_operator_rate_limit(self):
        for _ in range(20):
            self.assertEqual(200, self.call('/admin/refunds/preview', self.preview)[0])
        self.assertEqual(429, self.call('/admin/refunds/commit', self.commit)[0])
        self.assertEqual((0, 300, 900), self.f.wallet())

    def test_origin_transport_method_and_route_boundaries(self):
        for change, code in [(dict(HTTP_ORIGIN='https://foreign.test'),403),
                             ({'wsgi.url_scheme':'http'},403), (dict(REQUEST_METHOD='GET'),404),
                             (dict(PATH_INFO='/admin/refunds/commit/'),404)]:
            self.assertEqual(code, self.call('/admin/refunds/commit', self.commit, **change)[0])

    def test_health_fail_closed_without_blocking_player_reads(self):
        self.app.refund_health = lambda: False
        self.assertEqual(503, self.call('/healthz', {}, REQUEST_METHOD='GET')[0])
        self.assertEqual(200, self.call('/v1/players/'+self.f.player+'/head', {}, self.f.token,
                                      REQUEST_METHOD='GET')[0])
        self.app.refund_health = lambda: True
        self.assertEqual(200, self.call('/healthz', {}, REQUEST_METHOD='HEAD')[0])

    def test_logs_do_not_contain_review_personal_details(self):
        with self.assertLogs('player_data', level='INFO') as captured:
            self.call('/admin/refunds/commit', self.commit)
        log = '\n'.join(captured.output)
        for private in (self.writer, self.f.player, self.f.event.transaction, 'case-123', 'support.one@example.test'):
            self.assertNotIn(private, log)
