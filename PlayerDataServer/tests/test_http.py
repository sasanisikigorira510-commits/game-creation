import json
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
import uuid
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from store import Store
from server import create_server


class HttpTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.store = Store(Path(self.tmp.name)/'db.sqlite')
        self.server = create_server(self.store, 'test-admin', 0)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True); self.thread.start()
        self.url = 'http://127.0.0.1:'+str(self.server.server_address[1])
        self.player=uuid.uuid4().hex; self.token='a'*64
    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join(); self.tmp.cleanup()
    def call(self, path, body=None, token='', origin=None):
        headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'}
        if origin: headers['Origin']=origin
        req=urllib.request.Request(self.url+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=5) as r:return r.status,json.loads(r.read())
        except urllib.error.HTTPError as e:return e.code,json.loads(e.read())
    def test_authorization_and_registration_roundtrip(self):
        self.assertEqual(200,self.call('/v1/accounts',dict(PlayerId=self.player,Token=self.token))[0])
        self.assertEqual(401,self.call('/v1/players/'+self.player+'/head')[0])
        status,head=self.call('/v1/players/'+self.player+'/head',token=self.token)
        self.assertEqual(200,status);self.assertFalse(head['PurchasesEnabled'])
        self.assertEqual(401,self.call('/admin/players',token=self.token)[0])
        self.assertEqual(200,self.call('/admin/players',token='test-admin')[0])
    def test_cross_origin_and_malformed_body_rejected(self):
        self.assertEqual(403,self.call('/v1/accounts',dict(PlayerId=self.player,Token=self.token),origin='https://attacker.invalid')[0])
        self.assertEqual(400,self.call('/v1/accounts',[])[0])
    def test_admin_page_has_no_embedded_secrets(self):
        with urllib.request.urlopen(self.url) as response:
            page=response.read().decode()
            self.assertNotIn('test-admin',page)
            self.assertIn('no-store',response.headers['Cache-Control'])
            self.assertIn('frame-ancestors',response.headers['Content-Security-Policy'])

if __name__=='__main__': unittest.main()
