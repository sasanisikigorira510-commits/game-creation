"""Real production worker tests. Run with requirements-production installed."""
import importlib.util
import json
import os
import socket
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.error
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from maintenance import initialize, add_operator


@unittest.skipUnless(importlib.util.find_spec('gunicorn'), 'Install requirements-production.txt for real worker tests')
class GunicornTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        initialize(self.root / 'data', 'gunicorn-instance-123')
        add_operator(self.root / 'operators.json', 'tester', 'operator', self.root / 'admin-token')
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0)); self.port = sock.getsockname()[1]
        self.url = 'http://127.0.0.1:' + str(self.port)
        self.env = dict(os.environ, WITCH_DATA_DIR=str(self.root / 'data'), WITCH_INSTANCE_ID='gunicorn-instance-123',
                        WITCH_PUBLIC_ORIGIN='https://api.example.com', WITCH_OPERATORS_FILE=str(self.root / 'operators.json'))
        # Tests must never inherit real Apple credentials from the workstation.
        for key in list(self.env):
            if key.startswith('WITCH_APPLE_'): del self.env[key]
        self.process = None
        self.log = (self.root / 'worker.log').open('w+')
        self.start()

    def start(self):
        self.process = subprocess.Popen([sys.executable, '-m', 'gunicorn', '--config', 'gunicorn.conf.py',
                                        '--bind', '127.0.0.1:' + str(self.port), 'production:application_factory()'],
                                       cwd=ROOT, env=self.env, stdout=self.log, stderr=self.log)
        until = time.monotonic() + 15
        while time.monotonic() < until:
            if self.process.poll() is not None:
                self.fail('Production worker failed to start')
            try:
                if self.call('/healthz')[0] == 200: return
            except (OSError, urllib.error.URLError): pass
            time.sleep(0.05)
        self.fail('Production worker did not become ready')

    def stop(self):
        if self.process and self.process.poll() is None:
            self.process.terminate()
            try: self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill(); self.process.wait(timeout=5)

    def tearDown(self):
        self.stop(); self.log.close(); self.tmp.cleanup()

    def call(self, path, body=None, token='', **headers):
        h = {'Host': 'api.example.com', 'X-Real-IP': '203.0.113.10', 'X-Forwarded-Proto': 'https',
             'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}
        h.update(headers)
        req = urllib.request.Request(self.url + path, headers=h, data=json.dumps(body).encode() if body is not None else None)
        try:
            with urllib.request.urlopen(req, timeout=5) as result: return result.status, json.loads(result.read())
        except urllib.error.HTTPError as error: return error.code, json.loads(error.read())

    def test_registration_gacha_restart_retry_and_admin_audit(self):
        player, token = uuid.uuid4().hex, 'p' * 64
        status, _ = self.call('/v1/accounts', dict(PlayerId=player, Token=token))
        self.assertEqual(200, status)
        save = dict(PlayerId=player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0, SchemaVersion=3,
                    PlayerLevel=1, Gold=100, FreeGachaStones=900, PaidGachaStones=0,
                    OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
        path = '/v1/players/' + player
        self.assertEqual(200, self.call(path + '/snapshots', save, token)[0])
        request = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='gacha', Count=1, Paid=False)
        status, first = self.call(path + '/operations', request, token)
        self.assertEqual(200, status); self.assertEqual(600, first['Free'])
        self.stop(); self.start()
        status, replay = self.call(path + '/operations', request, token)
        self.assertEqual(200, status); self.assertEqual(first, replay)
        head = self.call(path + '/head', token=token)[1]
        self.assertEqual(600, head['Free']); self.assertEqual(1, len(head['Operations']))
        admin = (self.root / 'admin-token').read_text().strip()
        self.assertEqual(200, self.call('/admin/players/' + player + '/freeze',
                                       dict(Frozen=True, Actor='forged', Reason='integration'), admin)[0])
        audit = self.call('/admin/players/' + player, token=admin)[1]['Audit']
        self.assertEqual('tester', audit[0]['actor'])
        self.log.flush(); log = (self.root / 'worker.log').read_text()
        self.assertIn('"Event":"http"', log)
        for value in (player, token, admin): self.assertNotIn(value, log)

    def test_plain_http_and_forged_host_rejected_by_real_worker(self):
        self.assertEqual(403, self.call('/healthz', **{'X-Forwarded-Proto': 'http'})[0])
        self.assertEqual(400, self.call('/healthz', Host='attacker.invalid')[0])

    def test_100_players_with_10_concurrent_clients_and_operation_retries(self):
        """Isolated correctness smoke test, not a production capacity benchmark."""
        def exercise_player(index):
            player, token = uuid.uuid4().hex, uuid.uuid4().hex + uuid.uuid4().hex
            # Model distinct clients through the local trusted proxy. Keep the
            # production per-IP registration limit unchanged.
            headers = {'X-Real-IP': '203.0.113.' + str(index + 1)}
            self.assertEqual(200, self.call('/v1/accounts',
                                           dict(PlayerId=player, Token=token), **headers)[0])
            save = dict(PlayerId=player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                        SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900,
                        PaidGachaStones=0, OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
            path = '/v1/players/' + player
            self.assertEqual(200, self.call(path + '/snapshots', save, token, **headers)[0])
            request = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='gacha', Count=1, Paid=False)
            status, first = self.call(path + '/operations', request, token, **headers)
            self.assertEqual(200, status)
            self.assertEqual(600, first['Free'])
            status, replay = self.call(path + '/operations', request, token, **headers)
            self.assertEqual(200, status)
            self.assertEqual(first, replay)
            status, head = self.call(path + '/head', token=token, **headers)
            self.assertEqual(200, status)
            self.assertEqual(600, head['Free'])
            self.assertEqual(1, len(head['Operations']))
            return player

        with ThreadPoolExecutor(max_workers=10) as clients:
            players = list(clients.map(exercise_player, range(100)))
        self.assertEqual(100, len(set(players)))


if __name__ == '__main__': unittest.main()
