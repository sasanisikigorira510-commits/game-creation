import hashlib
import unittest

from qa_gateway import QaGateway, HEADER, MAX_LIFETIME


class QaGatewayTests(unittest.TestCase):
    def setUp(self):
        self.now = 1000
        self.mono = 500
        self.token = 'a'*64
        self.config = dict(Version=1,TokenSha256=hashlib.sha256(self.token.encode()).hexdigest(),
                           IssuedUnix=990,ExpiresUnix=1100)
        self.calls=[]
        def app(env,start):
            self.calls.append(env)
            start('200 OK',[])
            return [b'ok']
        self.app=app
        self.gate=QaGateway(app,self.config,clock=lambda:self.now,monotonic=lambda:self.mono)

    def call(self,path='/healthz',method='GET',**extra):
        env={'REQUEST_METHOD':method,'PATH_INFO':path,'REMOTE_ADDR':'127.0.0.1',
             'wsgi.url_scheme':'https',HEADER:'NasusQA '+self.token+' Bearer player-token'}
        env.update(extra); status=[]
        result=b''.join(self.gate(env,lambda s,h:status.append(s)))
        return status[0],result

    def test_allowed_paths_preserve_player_auth_but_strip_gate_token(self):
        for method,path in [('GET','/healthz'),('HEAD','/healthz'),('POST','/v1/accounts'),
                ('POST','/v1/apple/challenge'),('POST','/v1/apple/unlink/commit'),
                ('POST','/v1/account-deletion/status'),('GET','/v1/players/'+'b'*32+'/head'),
                ('POST','/v1/players/'+'b'*32+'/snapshots')]:
            self.assertEqual('200 OK',self.call(path,method)[0])
            self.assertEqual('Bearer player-token',self.calls[-1]['HTTP_AUTHORIZATION'])
            self.assertNotIn(self.token,str(self.calls[-1]))

    def test_no_admin_or_path_alias_or_unknown_method(self):
        for path in ['/','/admin','/admin.js','/admin/players','/v1/apple/challenge/',
                     '//v1/accounts','/v1/accounts/../admin','/v1/players/invalid/head',
                     '/v1/apple/challenge%2f','/v1/new-endpoint']:
            for method in ['GET','POST','PUT','OPTIONS']:
                self.assertEqual('404 Not Found',self.call(path,method)[0])
        self.assertEqual([],self.calls)

    def test_token_missing_wrong_malformed_never_reaches_app(self):
        for token in ['', 'b'*64,'a'*65,'a'*63,'a'*64+',a','秘密',None]:
            status,body=self.call(**{HEADER:'NasusQA '+token if isinstance(token,str) else token})
            self.assertEqual('404 Not Found',status)
            self.assertEqual(b'{"Error":"Not found"}',body)
        self.assertEqual([],self.calls)

    def test_wrong_transport_rejected(self):
        for extra in [{'REMOTE_ADDR':'203.0.113.1'},{'wsgi.url_scheme':'http'}]:
            self.assertEqual('404 Not Found',self.call(**extra)[0])
        self.assertEqual([],self.calls)

    def test_expired_and_clock_rollback_rejected(self):
        for now in [1100,1101,989]:
            self.now=now
            self.assertEqual('404 Not Found',self.call()[0])
        self.assertEqual([],self.calls)

    def test_wall_clock_cannot_extend_monotonic_deadline(self):
        self.now=1001; self.mono=600
        self.assertEqual('404 Not Found',self.call()[0])
        self.assertEqual([],self.calls)

    def test_bad_config_and_expired_start_fail_closed(self):
        for changes in [dict(Version=True),dict(Extra=1),dict(TokenSha256='oops'),
                dict(ExpiresUnix=1000),dict(IssuedUnix=1001),dict(ExpiresUnix=float('inf')),
                dict(ExpiresUnix=990+MAX_LIFETIME+1),dict(ExpiresUnix=True)]:
            with self.subTest(changes=changes),self.assertRaises(ValueError):
                QaGateway(self.app,dict(self.config,**changes),clock=lambda:1000)

    def test_denied_head_has_no_body(self):
        status,body=self.call(method='HEAD',**{HEADER:''})
        self.assertEqual('404 Not Found',status); self.assertEqual(b'',body)
