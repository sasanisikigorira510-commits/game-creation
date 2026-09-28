"""Resume ONLY the verified stopped v1 activation; preserve its code/evidence.

Wait for a completed automatic worker run before attaching v2 health. Log
fixed stage/reason codes, never exceptions, credentials or database contents.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import signal
import sys
import time

HELPER_HASH='3730d1aeba6d2ac032441a400f879d0870ea1ee74f347931e21a1eccc6139beb'
MANIFEST_HASH='27dbabd260c60a1b41794da73c36d1644d3ec6d9dfaf403edcf7965eacf092bc'
V2_HASH='688f54e5c8bcc29cdea6a490f520079999f2922b34c4d5e1559550f593f6cd2c'
NEW_HEALTH=Path('/usr/local/lib/nasus-apple-health-20260926-v2')


def main():
    if os.geteuid()!=0 or len(sys.argv)!=1: raise ValueError('Root only')
    source=Path(__file__).resolve().parent
    helper=source/'activate_apple_worker.py'
    if hashlib.sha256(helper.read_bytes()).hexdigest()!=HELPER_HASH: raise ValueError('Helper digest')
    sys.path.insert(0,str(source))
    import activate_apple_worker as a
    os.umask(0o077)
    manifest=json.loads(a.checked(source/'apple-activation-manifest.json',MANIFEST_HASH))
    a.require(set(manifest)==a.PACKAGE)
    new_collector=a.checked(source/'apple_integrated_health_v2.py',V2_HASH)
    compile(new_collector,'collector','exec')
    account=pwd.getpwnam('witchplayer'); a.require(account.pw_uid!=0)
    # The installed, stopped v1 deployment must match exactly. No repair based
    # on a loose existence check or automatic installer retry is permitted.
    for name,digest in a.UNCHANGED.items():
        a.checked(a.OLD/name,digest); a.checked(a.WORKER/name,digest)
    a.checked(a.OLD/'apple_revocation_runtime.py','eff153a9c4f6a7f08fa69e12cea6f335a18a09b53a0676d9dab3354045500f9a')
    a.checked(a.WORKER/'apple_revocation_runtime.py',manifest['apple_revocation_runtime.py'])
    old_health={}
    for name in ('apple_integrated_health.py','apple_revocation_health.py','backup_health.py'):
        old_health[name]=a.checked(a.HEALTH/name,manifest[name])
    for name,digest in a.COLLECTORS.items():
        old_health[name]=a.checked(a.HEALTH/name,digest)
    for name in (a.SERVICE,a.TIMER): a.checked(a.UNITS/name,manifest[name])
    a.require(a.prop(a.SERVICE,'ActiveState')=='inactive' and a.prop(a.SERVICE,'Result')=='success')
    a.require(a.prop(a.TIMER,'ActiveState')=='inactive' and a.prop(a.TIMER,'UnitFileState')=='disabled')
    a.require(a.prop('witch-player-health.service','DropInPaths')==a.PREVIOUS_DROPS)
    a.require(a.prop('witch-player-health.timer','ActiveState')=='active')
    for path in (a.HOOK,a.HEALTH/'disabled-hook.conf',NEW_HEALTH):
        a.require(not os.path.lexists(path))
    protected=[Path('/etc/caddy/Caddyfile'),a.UNITS/'witch-player.service',a.UNITS/'witch-player-health.service']
    before={p:hashlib.sha256(a.trusted(p)).hexdigest() for p in protected}
    a.external(); a.empty_runtime_probe()
    print('1/4 Stopped deployment verified. Existing API, keys, schema and v1 evidence preserved.',flush=True)
    NEW_HEALTH.mkdir(mode=0o700)
    for name,raw in old_health.items():
        a.write_new(NEW_HEALTH/name,new_collector if name=='apple_integrated_health.py' else raw)
    # Existing helper's rollback remains hash-pinned; bind it only to our new
    # fixed collector path, not to the retained v1 directory.
    a.HEALTH=NEW_HEALTH
    a.HOOK_TEXT='[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '+str(NEW_HEALTH/'apple_integrated_health.py')+'\n'
    phase='first_worker_run'
    try:
        started=time.time(); a.ctl('start',a.SERVICE,timeout=100)
        first=a.marker(a.MARKER,account.pw_uid,started)
        a.require(first.get('Processed')==dict(revoked=0,retry=0,stale=0,idle=1))
        phase='enable_timer'; a.ctl('enable','--now',a.TIMER)
        a.require(a.ctl('is-enabled',a.TIMER)=='enabled')
        print('2/4 Waiting for a completed automatic worker run before health attachment.',flush=True)
        phase='automatic_worker_observation'; deadline=time.monotonic()+130
        while time.monotonic()<deadline:
            time.sleep(1)
            if a.prop(a.SERVICE,'ActiveState')!='inactive': continue
            value=json.loads(a.trusted(a.MARKER,account.pw_uid,4096))
            if value.get('Healthy') is not True or value.get('CheckedUnix',0)<=first['CheckedUnix']: continue
            auto=a.marker(a.MARKER,account.pw_uid,first['CheckedUnix'])
            a.require(auto.get('Processed')==dict(revoked=0,retry=0,stale=0,idle=1))
            break
        else: raise ValueError('No completed automatic worker run')
        phase='readonly_combined_health'
        # collect(), never main()/publish(): inspect the candidate without
        # writing the live heartbeat before the systemd hook is installed.
        probe=('import sys,json;sys.path.insert(0,'+repr(str(NEW_HEALTH))+');'
            'import apple_integrated_health as h,retention_health as r,base_health as b;'
            'reasons=[];v=h.collect(lambda **kw:r.collect(b,**kw),on_reason=reasons.append);'
            'print(json.dumps(dict(Healthy=v["Healthy"],Reasons=reasons)))')
        observed=json.loads(a.run(['/usr/bin/python3','-I','-B','-c',probe]))
        print('APPLE_RESUME_COLLECTOR='+json.dumps(observed,sort_keys=True),flush=True)
        a.require(observed.get('Healthy') is True)
        phase='attach_health_hook'
        print('3/4 Attach combined monitoring; keep backup/deletion/retention checks.',flush=True)
        a.write_new(a.HOOK,a.HOOK_TEXT.encode()); a.ctl('daemon-reload')
        a.require(a.prop('witch-player-health.service','DropInPaths')==a.PREVIOUS_DROPS+' '+str(a.HOOK))
        started=time.time(); a.ctl('start','witch-player-health.service')
        first_health=a.marker(Path('/run/witch-player-health/status.json'),0,started)
        a.external()
        phase='automatic_health_observation'
        print('4/4 Verify automatic combined health publication and closed game routes.',flush=True)
        deadline=time.monotonic()+100
        while time.monotonic()<deadline:
            time.sleep(3)
            value=json.loads(a.trusted(Path('/run/witch-player-health/status.json'),0,4096))
            if value.get('Healthy') is True and value.get('CheckedUnix',0)>first_health['CheckedUnix']:
                a.marker(Path('/run/witch-player-health/status.json'),0,first_health['CheckedUnix']); break
        else: raise ValueError('No automatic healthy observation')
        phase='final_verification'
        a.external(); a.empty_runtime_probe()
        for path,digest in before.items(): a.require(hashlib.sha256(a.trusted(path)).hexdigest()==digest)
        a.require(a.prop(a.TIMER,'ActiveState')=='active' and a.ctl('is-enabled',a.TIMER)=='enabled')
        receipt=dict(Status='APPLE_WORKER_ACTIVATION_VERIFIED',AutomaticWorkerRun=True,
            AutomaticCombinedHealth=True,GameRootClosed=True,AppleTablesStillEmpty=True,
            SchemaChanged=False,KeysChanged=False,LiveApiConfigurationChanged=False,VerifiedUnix=time.time())
        a.write_new(NEW_HEALTH/'activation-result.json',json.dumps(receipt,sort_keys=True).encode())
        print(json.dumps(receipt,sort_keys=True),flush=True)
    except BaseException:
        print('APPLE_RESUME_FAILED_STAGE='+phase,flush=True)
        a.rollback()
        raise


if __name__=='__main__':
    def interrupt(*_): raise KeyboardInterrupt()
    for sig in (signal.SIGTERM,signal.SIGHUP): signal.signal(sig,interrupt)
    try: main()
    except BaseException:
        print('APPLE_RESUME_STOPPED: retained files; no secret details printed; do not rerun.',flush=True)
        raise SystemExit(1)
