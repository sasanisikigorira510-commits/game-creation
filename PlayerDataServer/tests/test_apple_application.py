"""Private synthetic DB and provider only; deployed factory stays unchanged."""
import io
import json
import sqlite3
import time
import unittest
from unittest.mock import patch

from tests import test_apple_revocation_runtime as fixture
from apple_application import create_apple_app
from apple_revocation_runtime import load_components, execute
from apple_tokens import AppleGrant
from account_deletion import AccountDeletion
from account_linking import AccountLinking
from deletion_journal import DeletionJournal
from maintenance import add_operator
from production import create_app
from store import now


class AppleApplicationTests(unittest.TestCase):
    write=fixture.RuntimeTests.write
    save_config=fixture.RuntimeTests.save_config

    def setUp(self):
        fixture.RuntimeTests.setUp(self)
        self.journal=DeletionJournal(self.store,self.config['InstanceId'])
        verifier=lambda *_:'fixture-subject'; verifier.audience=self.config['ClientId']
        self.client.verifier=verifier
        linking=AccountLinking(self.store,verifier,tokens=self.client,vault=self.vault)
        AccountDeletion(self.store,account_linking=linking,journal=self.journal)
        self.operators=self.root/'operators.json'
        add_operator(self.operators,'fixture','operator',self.root/'operator-token')
        for p in self.data.iterdir(): p.chmod(0o600)
        self.env=dict(WITCH_APPLE_CONFIG=str(self.config_path),WITCH_DATA_DIR=str(self.data),
            WITCH_INSTANCE_ID=self.config['InstanceId'],WITCH_PUBLIC_ORIGIN='https://api.example.com',
            WITCH_OPERATORS_FILE=str(self.operators),WITCH_BACKUP_HEALTH_FILE=str(self.root/'backup.json'))
        self.markers(True)

    def markers(self,healthy):
        raw=json.dumps(dict(Healthy=healthy,CheckedUnix=time.time())).encode()
        self.write(self.root/'backup.json',raw); self.write(self.state/'status.json',raw)

    def components(self,path,readonly=True):
        vault,_,state=load_components(path,readonly=readonly)
        return vault,self.client,state

    def build(self): return create_apple_app(self.env,components=self.components)

    def call(self,app,path,body=None,token=''):
        raw=json.dumps(body or {}).encode(); response={}
        env=dict(REQUEST_METHOD='GET' if body is None else 'POST',PATH_INFO=path,
            HTTP_HOST='api.example.com',REMOTE_ADDR='127.0.0.1',HTTP_X_REAL_IP='203.0.113.15',CONTENT_LENGTH=str(len(raw)),
            CONTENT_TYPE='application/json',HTTP_AUTHORIZATION='Bearer '+token)
        env['wsgi.url_scheme']='https'; env['wsgi.input']=io.BytesIO(raw)
        def start(status,headers): response['status']=int(status.split()[0])
        response['body']=json.loads(b''.join(app(env,start)))
        return response

    def test_construction_does_not_migrate_or_contact_apple(self):
        with self.store.connect() as db: before=list(db.iterdump())
        with patch('apple_tokens.AppleTokenClient._post',side_effect=AssertionError('no network')):
            app=create_apple_app(self.env)
        self.assertIs(app.store,app.account_linking.vault.store)
        self.assertIs(app.store,app.account_deletion.journal.store)
        with self.store.connect() as db: self.assertEqual(before,list(db.iterdump()))

    def test_missing_apple_and_deletion_schema_not_created(self):
        for table in ('apple_links','deletion_confirmations','deletion_journal_meta'):
            with self.subTest(table=table):
                with self.store.connect() as db:
                    schema=db.execute('SELECT sql FROM sqlite_master WHERE name=?',(table,)).fetchone()[0]
                    rows=list(db.execute('SELECT * FROM '+table))
                    db.execute('DROP TABLE '+table)
                with self.assertRaises((sqlite3.Error,ValueError)): self.build()
                with self.store.connect() as db:
                    self.assertIsNone(db.execute('SELECT name FROM sqlite_master WHERE name=?',(table,)).fetchone())
                    db.execute(schema)
                    for row in rows: db.execute('INSERT INTO '+table+' VALUES('+','.join('?' for _ in row)+')',tuple(row))

    def test_identity_mismatch_and_quarantine_rejected(self):
        self.env['WITCH_INSTANCE_ID']='wrong-instance'
        with self.assertRaises(ValueError): self.build()
        self.env['WITCH_INSTANCE_ID']=self.config['InstanceId']
        self.write(self.data/'RECOVERY-PENDING.txt',b'quarantined')
        with self.assertRaises(ValueError): self.build()

    def test_default_production_factory_still_does_not_enable_apple(self):
        app=create_app(self.env)
        self.assertIsNone(app.account_linking); self.assertIsNone(app.account_deletion)

    def test_link_challenge_reaches_explicit_dependency(self):
        app=self.build(); player='a'*32; token='b'*64
        app.store.register(player,token)
        response=self.call(app,'/v1/apple/challenge',dict(Mode='link',PlayerId=player),token)
        self.assertEqual(200,response['status']); self.assertIn('Nonce',response['body'])
        self.assertEqual([],self.calls)

    def test_qa_gate_still_requires_individual_player_authentication(self):
        import hashlib
        from qa_gateway import QaGateway
        key='c'*64; app=self.build(); player='a'*32; token='b'*64
        app.store.register(player,token)
        gate=QaGateway(app,dict(Version=1,TokenSha256=hashlib.sha256(key.encode()).hexdigest(),
            IssuedUnix=time.time()-1,ExpiresUnix=time.time()+100))
        def wrapped(env,start):
            env['HTTP_AUTHORIZATION']='NasusQA '+key+' '+env['HTTP_AUTHORIZATION']
            return gate(env,start)
        path='/v1/players/'+player+'/head'
        self.assertEqual(200,self.call(wrapped,path,token=token)['status'])
        self.assertEqual(401,self.call(wrapped,path,token='wrong')['status'])
        self.assertEqual(404,self.call(wrapped,'/admin/session',token=token)['status'])

    def test_deletion_still_waits_for_offsite_ack(self):
        app=self.build(); player='a'*32; token='b'*64; app.store.register(player,token)
        preview=self.call(app,'/v1/account-deletion/preview',dict(PlayerId=player),token)
        self.assertEqual(200,preview['status'])
        body=dict(PlayerId=player,ConfirmationToken=preview['body']['ConfirmationToken'])
        self.assertEqual(503,self.call(app,'/v1/account-deletion/commit',body,token)['status'])
        self.assertEqual(503,self.call(app,'/v1/account-deletion/status',body,token)['status'])
        with app.store.connect() as db:
            self.assertEqual('pending',db.execute('SELECT state FROM deletion_outbox').fetchone()[0])
        self.assertEqual([],self.calls)

    def test_unlink_http_to_persistent_worker_and_player_preserved(self):
        app=self.build(); player='a'*32; token='b'*64; app.store.register(player,token)
        vault=app.account_linking.vault
        with app.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            vault.save(db,AppleGrant('fixture-subject','fixture-revoke-secret'))
            db.execute('INSERT INTO apple_links VALUES(?,?,?)',
                       (vault.subject_hash('fixture-subject'),player,now()))
        preview=self.call(app,'/v1/apple/unlink/preview',dict(PlayerId=player),token)
        self.assertEqual(200,preview['status'])
        body=dict(PlayerId=player,ConfirmationToken=preview['body']['ConfirmationToken'])
        result=self.call(app,'/v1/apple/unlink/commit',body,token)
        self.assertEqual(200,result['status']); self.assertTrue(result['body']['RevocationPending'])
        self.assertEqual([],self.calls)
        worker=execute(self.config_path,run=True,components=self.components)
        self.assertTrue(worker['Healthy']); self.assertEqual(['fixture-revoke-secret'],self.calls)
        with app.store.connect() as db:
            self.assertEqual(0,db.execute('SELECT count(*) FROM apple_grants').fetchone()[0])
            self.assertEqual(0,db.execute('SELECT count(*) FROM apple_links').fetchone()[0])
        app.store.authenticate(player,token)
        self.assertEqual(200,self.call(app,'/healthz')['status'])

    def test_both_health_markers_required(self):
        app=self.build(); self.assertEqual(200,self.call(app,'/healthz')['status'])
        for target in (self.root/'backup.json',self.state/'status.json'):
            for raw in (b'{"Healthy":false,"CheckedUnix":0}',b'{"Healthy":true,"CheckedUnix":1}'):
                self.write(target,raw)
                self.assertEqual(503,self.call(app,'/healthz')['status'])
                self.markers(True)
        (self.state/'status.json').unlink()
        self.assertEqual(503,self.call(app,'/healthz')['status'])

    def test_purchase_configuration_cannot_silently_fall_back(self):
        self.env['WITCH_APPLE_BUNDLE_ID']=self.config['ClientId']
        with self.assertRaises(ValueError): self.build()
        verifier=lambda *_:None
        app=create_apple_app(self.env,components=self.components,purchase_verifier=verifier)
        self.assertIs(verifier,app.store.purchase_verifier)
        self.env['WITCH_APPLE_BUNDLE_ID']='other.bundle'
        with self.assertRaises(ValueError):
            create_apple_app(self.env,components=self.components,purchase_verifier=verifier)


if __name__ == '__main__': unittest.main()
