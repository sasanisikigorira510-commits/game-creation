"""Single-use, pinned QA deployment. Live factory/keys/schema are not changed.

The QA entrance expires within 24h. Root/admin remain closed. On failure restore
the exact previous proxy file, stop the new service, retain evidence, never retry.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import subprocess
import sys
import time
import urllib.request

sys.path.insert(0,str(Path(__file__).resolve().parent))
from install_apple_staging import safe_file, run, require


def write_new(path, raw, mode=0o600, owner=None):
    """Honor the final mode even under umask 077; secrets still default to 600."""
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as out:
        out.write(raw); out.flush()
        if owner: os.fchown(out.fileno(), owner.pw_uid, owner.pw_gid)
        os.fchmod(out.fileno(), mode)
        os.fsync(out.fileno())

SOURCE=Path('/home/ubuntu/nasus-deploy/20260926-apple-qa-01')
TARGET=Path('/opt/nasus-apple-qa-20260926')
CONFIG=Path('/etc/witch-player-qa')
UNIT=Path('/etc/systemd/system/witch-player-qa.service')
CADDY=Path('/etc/caddy/Caddyfile')
BASE_HASH='75ceb4d8ce8b107f11786354a7ca56442d5bb41a960ffa251e0f20bf724efa44'
DOMAIN='api.nasus-games.com'
PROXY='''    handle_path /qa/* {
        reverse_proxy 127.0.0.1:8790 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-Proto https
            transport http {
                dial_timeout 3s
                response_header_timeout 45s
            }
        }
    }
'''
SERVICE='''[Unit]
Description=Expiring private Apple device QA API
After=network.target
StartLimitIntervalSec=120
StartLimitBurst=3
[Service]
Type=simple
User=witchplayer
Group=witchplayer
WorkingDirectory=/opt/nasus-apple-qa-20260926
EnvironmentFile=/etc/witch-player/server.env
Environment=WITCH_APPLE_CONFIG=/etc/witch-player-apple/worker.json
Environment=WITCH_BACKUP_HEALTH_FILE=/run/witch-player-health/status.json
Environment=WITCH_QA_GATE_CONFIG=/etc/witch-player-qa/gate.json
ExecStart=/opt/nasus-apple-qa-20260926/venv/bin/gunicorn --config gunicorn.conf.py --bind 127.0.0.1:8790 qa_gateway:application_factory()
Restart=on-failure
RestartSec=5
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/witch-player
RestrictSUIDSGID=true
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6
CapabilityBoundingSet=
MemoryMax=400M
LimitCORE=0
'''


def ctl(*args): return run(['/usr/bin/systemctl',*args],100)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): return None


def open_request(request,timeout=15):
    return urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect()).open(request,timeout=timeout)


def status(url,token=None):
    headers={'Authorization':'NasusQA '+token} if token else {}
    request=urllib.request.Request(url,headers=headers)
    try:
        with open_request(request) as result: return result.status
    except urllib.error.HTTPError as error: return error.code


def validate_caddy(path):
    # Caddy service owns these same existing values. Do not print the env file.
    script='set -e; set -a; . /etc/witch-player/caddy.env; set +a; exec /usr/sbin/runuser -u caddy -- /usr/bin/caddy validate --config "$1" --adapter caddyfile'
    run(['/bin/bash','-c',script,'qa-validate',str(path)])


def main():
    require(os.geteuid()==0 and len(sys.argv)==3 and all(re.fullmatch('[a-f0-9]{64}',v) for v in sys.argv[1:]))
    os.umask(0o077)
    user=pwd.getpwnam('witchplayer'); uploader=pwd.getpwnam('ubuntu')
    for path in (TARGET,CONFIG,UNIT): require(not os.path.lexists(path))
    previous=safe_file(CADDY,0)
    require(hashlib.sha256(previous).hexdigest()==BASE_HASH)
    raw=safe_file(SOURCE/'manifest.json',uploader.pw_uid)
    require(hashlib.sha256(raw).hexdigest()==sys.argv[1])
    manifest=json.loads(raw)
    require(isinstance(manifest,dict) and 10<=len(manifest)<=50)
    contents={}
    for name,digest in manifest.items():
        require(re.fullmatch(r'(?:[a-z_]+\.py|gunicorn\.conf\.py|catalog\.json|wheels/[A-Za-z0-9_.-]+\.whl)',name))
        data=safe_file(SOURCE/name,uploader.pw_uid)
        require(hashlib.sha256(data).hexdigest()==digest)
        contents[name]=data
    require({'qa_gateway.py','apple_application.py','gunicorn.conf.py','catalog.json'}<=contents.keys())
    client_raw=safe_file(SOURCE/'client.json',uploader.pw_uid,4096,True)
    require(hashlib.sha256(client_raw).hexdigest()==sys.argv[2])
    client=json.loads(client_raw)
    require(set(client)=={'BaseUrl','ExperimentalAppleLinking','QaAccessKey'})
    require(client['BaseUrl']=='https://'+DOMAIN+'/qa' and client['ExperimentalAppleLinking'] is True)
    token=client['QaAccessKey']; require(isinstance(token,str) and re.fullmatch('[a-f0-9]{64}',token))
    require(status('https://'+DOMAIN+'/healthz')==200 and status('https://'+DOMAIN+'/')==503)
    for name in ('witch-player-apple-revoke.timer','witch-player-health.timer'):
        require(ctl('is-active',name)=='active')
    # Use the actual existing health-path assignment; never guess a new marker.
    health_drop=safe_file(Path('/etc/systemd/system/witch-player.service.d/backup-health.conf'),0).decode()
    match=re.search(r'Environment="?WITCH_BACKUP_HEALTH_FILE=([^\s"\n]+)',health_drop)
    require(match is not None and match.group(1).startswith('/run/'))
    service=SERVICE.replace('/run/witch-player-health/status.json',match.group(1))
    old_env=safe_file(Path('/etc/witch-player/server.env'),0).decode()
    require(not re.search(r'^WITCH_APPLE_BUNDLE_ID=.+',old_env,re.M))
    TARGET.mkdir(mode=0o755); TARGET.chmod(0o755)
    (TARGET/'wheels').mkdir(mode=0o755); (TARGET/'wheels').chmod(0o755)
    write_new(TARGET/'previous-Caddyfile',previous)
    for name,data in contents.items():
        write_new(TARGET/name,data,0o644); (TARGET/name).chmod(0o644)
    CONFIG.mkdir(mode=0o700); os.chown(CONFIG,user.pw_uid,user.pw_gid)
    now=time.time()
    gate=dict(Version=1,IssuedUnix=now,ExpiresUnix=now+24*3600,
              TokenSha256=hashlib.sha256(token.encode()).hexdigest())
    write_new(CONFIG/'gate.json',json.dumps(gate).encode(),owner=user)
    candidate=previous.replace(b'    @health {',PROXY.encode()+b'    @health {',1)
    require(candidate!=previous)
    write_new(TARGET/'candidate-Caddyfile',candidate,0o644)
    stage='runtime'
    proxy_changed=False
    try:
        print('1/4 Build isolated QA runtime. Existing live API unchanged.',flush=True)
        run(['/usr/bin/python3','-m','venv',str(TARGET/'venv')],120)
        py=str(TARGET/'venv/bin/python')
        wheels=[str(TARGET/name) for name in contents if name.startswith('wheels/')]
        run([py,'-I','-m','pip','install','--no-index','--no-deps',*wheels],180)
        run([py,'-I','-m','pip','check'])
        run(['/bin/chmod','-R','u=rwX,go=rX',str(TARGET/'venv')])
        # Compile without adding source bytecode. Dependency import validates the installed environment.
        run([py,'-I','-B','-c','import gunicorn,jwt,cryptography,cffi'])
        validate_caddy(TARGET/'candidate-Caddyfile')
        stage='service'
        print('2/4 Start QA on loopback only. No migrations or key changes.',flush=True)
        write_new(UNIT,service.encode(),0o644)
        ctl('daemon-reload'); ctl('start',UNIT.name)
        require(ctl('is-active',UNIT.name)=='active')
        # Health callback uses existing live DB; request itself is read-only.
        for _ in range(20):
            request=urllib.request.Request('http://127.0.0.1:8790/healthz',headers={
                'Host':DOMAIN,'X-Real-IP':'127.0.0.1','X-Forwarded-Proto':'https','Authorization':'NasusQA '+token})
            try:
                with open_request(request,timeout=3) as response:
                    if response.status==200: break
            except (OSError,urllib.error.HTTPError): pass
            time.sleep(1)
        else: raise ValueError('QA health rejected')
        stage='proxy'
        print('3/4 Attach expiring gated /qa entrance; root/admin remain closed.',flush=True)
        require(safe_file(CADDY,0)==previous)
        # Replace only after ownership, content and Caddy validation have passed.
        temporary=CADDY.with_name('Caddyfile.qa-new')
        write_new(temporary,candidate,0o644)
        os.replace(temporary,CADDY); proxy_changed=True
        ctl('reload','caddy.service')
        stage='verify'
        print('4/4 Verify valid key, rejected missing/wrong keys, closed admin and normal health.',flush=True)
        checks=[('/healthz',None,200),('/',None,503),('/admin',None,503),
                ('/qa/healthz',None,404),('/qa/healthz','0'*64,404),
                ('/qa/healthz',token,200),('/qa/admin',token,404),('/qa/',token,404)]
        for path,key,expected in checks: require(status('https://'+DOMAIN+path,key)==expected)
        require(ctl('is-active','witch-player.service')=='active')
        print(json.dumps(dict(Status='APPLE_QA_GATE_VERIFIED',ExpiresUnix=gate['ExpiresUnix'],
            PublicGameClosed=True,AdminClosed=True,SchemaChanged=False,KeysChanged=False,
            RealAppleAuthenticationTested=False)),flush=True)
    except BaseException:
        rollback=True
        if proxy_changed:
            try:
                require(safe_file(CADDY,0)==candidate)
                temporary=CADDY.with_name('Caddyfile.qa-rollback')
                write_new(temporary,previous,0o644); os.replace(temporary,CADDY)
                ctl('reload','caddy.service')
            except BaseException: rollback=False
        try:
            if UNIT.exists(): ctl('stop',UNIT.name)
        except BaseException: rollback=False
        print('APPLE_QA_STOPPED: stage='+stage+'; rollback='+str(rollback)+'; files retained; do not rerun.',flush=True)
        raise SystemExit(1)


if __name__=='__main__':
    try: main()
    except Exception:
        print('APPLE_QA_PRECHECK_STOPPED: no secret details printed; inspect before retry.',flush=True)
        raise SystemExit(1)
