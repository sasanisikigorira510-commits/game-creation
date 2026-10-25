"""Fresh finite rollout with preflight, native tests, pinned metadata and rollback."""
import base64
import copy
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time

PREFIX = Path('/opt/witch-player-release/arena-resilience/20261025-v2')
DROPIN = Path('/etc/systemd/system/witch-player-arena-production-broker.service.d/99-queued-admission-20261021.conf')
OPS = Path('/etc/witch-player-arena/production-operations.json')
OPS_BIND = Path('/etc/witch-player-arena/production-operations.sha256')
MONITOR = Path('/etc/witch-player-release/monitor.json')
BIND = Path('/etc/witch-player-release/api-binding.env')
PYTHON = '/opt/witch-player-release/releases/20260930-v03/venv/bin/python'
BROKER = 'witch-player-arena-production-broker.service'
CHECKS = ['witch-player-arena-production-'+x+'.service' for x in ('native-probe','settlement','operations')]
TIMERS = [x.removesuffix('.service')+'.timer' for x in CHECKS]
HEALTH = 'witch-player-release-health.service'
HTIMER = 'witch-player-release-health.timer'
APIS = ['witch-player-release-api@'+x+'.service' for x in ('device','review','notifications')]
ARCHIVE = Path('/opt/witch-player-arena/production/canary-failures-20261025')
HISTORICAL = Path('/opt/witch-player-arena/production/recovery-20261025-1792905259653627922')
EXPECTED = {
    DROPIN:'44a00fbdbbe41b0597022cca24335a0da0dbe82c3ee2714588883c92b400f7ed',
    OPS:'1d46f51cac1eb0f961cf9827e4846ea0f9bd4840ddcdbaacad49e1ee7d12a4b9',
    OPS_BIND:'eedd480f3656792bad90f420da1aa024a1689f7e859eb2747fe483247063dc9f',
    MONITOR:'501fb00397e4bd328a204a0ce6de9c7d903e14eb41e08e65d9cd00791dae783d',
}


def require(value, reason='PREFLIGHT_REJECTED'):
    if not value:
        raise RuntimeError(reason)


def sha(raw):return hashlib.sha256(raw).hexdigest()
def canonical(value):return json.dumps(value,sort_keys=True,separators=(',',':'),allow_nan=False).encode()


def read(path):
    for parent in path.parents:
        s=parent.lstat();require(stat.S_ISDIR(s.st_mode) and s.st_uid==0 and not s.st_mode&0o022)
    s=path.lstat();require(stat.S_ISREG(s.st_mode) and s.st_uid==0 and s.st_nlink==1
                          and not s.st_mode&0o022 and s.st_size<1048576)
    fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
    try:
        raw=os.pread(fd,1048576,0)
        require(len(raw)==s.st_size and os.fstat(fd).st_ino==s.st_ino==path.lstat().st_ino)
        return raw
    finally:os.close(fd)


def install(path, raw, mode=0o644, before=None):
    if before is not None:require(read(path)==before,'CONFIGURATION_DRIFT')
    else:require(not os.path.lexists(path),'INSTALL_DESTINATION_EXISTS')
    temporary=path.with_name(path.name+'.resilience-new')
    with temporary.open('xb') as stream:
        os.fchmod(stream.fileno(),mode);stream.write(raw);stream.flush();os.fsync(stream.fileno())
    if before is not None:require(read(path)==before,'CONFIGURATION_DRIFT')
    os.replace(temporary,path)
    require(read(path)==raw)
    fd=os.open(path.parent,os.O_RDONLY|os.O_DIRECTORY)
    try:os.fsync(fd)
    finally:os.close(fd)


def ctl(verb, unit):
    require(unit in APIS+CHECKS+TIMERS+[BROKER,HEALTH,HTIMER] and verb in ('start','stop'))
    subprocess.run(['systemctl',verb,unit],check=True,stdin=subprocess.DEVNULL,
                   stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=75)


def state(unit):
    r=subprocess.run(['systemctl','show',unit,'--property=ActiveState,SubState,Result,ExecMainStatus,MainPID,ControlPID,Job'],
                     check=True,capture_output=True,text=True,timeout=10)
    return dict(x.split('=',1) for x in r.stdout.splitlines() if '=' in x)


def wait_quiet():
    deadline=time.monotonic()+65
    while True:
        if all(state(u)['ActiveState'] in ('inactive','failed') for u in CHECKS):return
        require(time.monotonic()<deadline,'CHECKS_NOT_QUIET');time.sleep(.2)


def broker_ready():
    path=Path('/run/witch-player-arena-production-broker/worker.sock')
    deadline=time.monotonic()+25
    while True:
        require(state(BROKER)['ActiveState']=='active','BROKER_NOT_RUNNING')
        try:
            value=path.lstat()
            if stat.S_ISSOCK(value.st_mode) and value.st_uid==0 and value.st_gid==988 and stat.S_IMODE(value.st_mode)==0o660:return
        except FileNotFoundError:pass
        require(time.monotonic()<deadline,'BROKER_SOCKET_NOT_READY')
        time.sleep(.2)


def observations(monitor_sha):
    now=time.time();result={}
    for name,path in [('Native','/run/witch-player-arena/production-health.json'),
                      ('Operations','/run/witch-player-arena-production-operations/status.json'),
                      ('Release','/run/witch-player-release-health/status.json')]:
        value=json.loads(read(Path(path)))
        require(value['Healthy'] is True and 0<=now-value['CheckedUnix']<120,name+'_NOT_HEALTHY')
        result[name]={'Healthy':True,'CheckedUnix':value['CheckedUnix']}
        if name=='Operations':require(value['NativeReady'] and value['SettlementCaughtUp'])
        if name=='Release':require(value['ConfigurationSha256']==monitor_sha)
    return result


def refresh(monitor_sha):
    # Timers must be restored before the fixed publishers attest their unit state.
    for unit in TIMERS:ctl('start',unit)
    broker_ready()
    for unit in CHECKS[:2]:ctl('start',unit)
    wait_quiet()
    ctl('start',CHECKS[2])
    ctl('start',HEALTH)
    return observations(monitor_sha)


def native_tests(module, prepared, historical, evidence):
    probe=json.loads(read(Path('/etc/witch-player-arena/production-probe.json')))
    results=[]
    evidence['HistoricalReplays']=results
    for item in historical:
        raw=read(HISTORICAL/item['Name']/'input.json');require(sha(raw)==item['Sha256'])
        value=json.loads(raw);require(module.is_canary(value,probe['InputTemplate']))
        expected=copy.deepcopy(probe['ExpectedResult'])
        mapping=dict(zip(probe['InputTemplate']['Profile']['PartyMonsterInstanceIds'],value['Profile']['PartyMonsterInstanceIds']))
        for unit in expected['Party']['Units']:
            unit['Snapshot']['InstanceId']=mapping[unit['Snapshot']['InstanceId']]
        try:output=prepared.executor(raw.decode(),'CalculateProfile')
        except Exception:
            failed=dict(InputSha256=item['Sha256'],MatchedExpected=False)
            for path in ARCHIVE.glob('witch-weekly-job-*/failure.json'):
                value=json.loads(read(path))
                if value.get('InputSha256')==item['Sha256']:failed['Diagnostic']=value['Diagnostic']
            results.append(failed)
            raise
        require(canonical(output)==canonical(expected),'HISTORICAL_INPUT_REPLAY_MISMATCH')
        results.append(dict(InputSha256=item['Sha256'],MatchedExpected=True))
    # One deliberate invalid synthetic profile in a separate diagnostic spool.
    # This tests the real native failure path, never a live user's request.
    spool=PREFIX/'diagnostic-inputs';spool.mkdir(mode=0o700)
    old=prepared.executor
    failing=type(old)(old.artifact_hash,old.root,old.files,spool,old.bridge_sha256)
    failing._resilience=old._resilience;failing._arena_guard=old._arena_guard
    try:failing('{"Version":1,"Profile":{}}','CalculateProfile')
    except (ValueError,RuntimeError):pass
    failures=list(spool.glob('witch-weekly-job-*/failure.json'))
    require(len(failures)==1,'CONTROLLED_FAILURE_EVIDENCE_MISSING')
    failure=json.loads(read(failures[0]));require(failure['Diagnostic']['Stage']=='NativeExit'
        and failure['Diagnostic']['NativeExit']!=0,'CONTROLLED_NATIVE_FAILURE_NOT_RECORDED')
    # Real filesystem regression: eight exact canaries must not block the ninth.
    synthetic=PREFIX/'archive-selftest';synthetic.mkdir(mode=0o700)
    archive=PREFIX/'archive-selftest-evidence';archive.mkdir(mode=0o700)
    reference=read(HISTORICAL/historical[0]['Name']/'input.json')
    for index in range(8):
        directory=synthetic/('witch-weekly-job-'+format(index,'032x'));directory.mkdir(mode=0o700)
        install(directory/'input.json',reference,mode=0o444)
    require(module.archive_canaries(synthetic,probe['InputTemplate'],archive)==8 and not list(synthetic.iterdir()),
            'ARCHIVE_SELFTEST_FAILED')
    return dict(HistoricalReplays=results,ControlledFailure=failure['Diagnostic'],
                SyntheticArchiveRegressionPassed=True)


def apply(payload):
    report=dict(Purpose='ARENA_RESILIENCE_ROLLOUT_20261025',StartedUnix=time.time(),Stage='Preflight',
                DatabaseDirectlyOpened=False,WorkerArtifactChanged=False,ResourceLimitsChanged=False,
                DeletedEvidenceFiles=0,RollbackAttempted=False)
    old={};new={};changed=False;maintenance=False;active=[]
    try:
        require(os.geteuid()==0,'ADMIN_REQUIRED')
        require(not PREFIX.exists(),'FRESH_ROLLOUT_ALREADY_EXISTS')
        for path,pin in EXPECTED.items():
            old[path]=read(path);require(sha(old[path])==pin,'BASELINE_PIN_MISMATCH')
        old[BIND]=read(BIND);require(old[BIND].count(EXPECTED[MONITOR].encode())==1)
        # Resume the device API stopped by the preceding failed rollout before
        # any diagnostics or new maintenance. Only exact restored bindings pass.
        require(state(BROKER)['ActiveState']=='active','PRIOR_BROKER_NOT_ACTIVE')
        broker_ready()
        ctl('start',HEALTH)
        release=json.loads(read(Path('/run/witch-player-release-health/status.json')))
        require(release['Healthy'] and release['ConfigurationSha256']==EXPECTED[MONITOR])
        ctl('start',APIS[0])
        report['PriorDeviceApiRestoredBeforeDiagnostics']=True
        for unit in TIMERS+[HTIMER]:require(state(unit)['ActiveState']=='active','TIMER_BASELINE_MISMATCH')
        require(state(BROKER)['ActiveState']=='active')
        for unit in APIS:
            require(state(unit)['ActiveState'] in ('active','inactive'))
            if state(unit)['ActiveState']=='active':active.append(unit)
        report['PriorActiveApis']=active
        observations(EXPECTED[MONITOR])
        memory=int(next(x.split()[1] for x in Path('/proc/meminfo').read_text().splitlines() if x.startswith('MemAvailable:')))
        require(memory>=393216,'MEMORY_RESERVE_UNAVAILABLE')
        PREFIX.parent.mkdir(mode=0o755,parents=True,exist_ok=True)
        PREFIX.mkdir(mode=0o755)
        ARCHIVE.mkdir(mode=0o700,exist_ok=True)
        files={name:base64.b64decode(value) for name,value in payload['Sources'].items()}
        require(set(files)=={'production_arena_resilient_broker.py','arena_diagnostic_job_bridge.py'})
        for name,raw in files.items():compile(raw,name,'exec');install(PREFIX/name,raw)
        manifest=canonical({name:sha(raw) for name,raw in files.items()})+b'\n'
        install(PREFIX/'SHA256.json',manifest)
        spec=importlib.util.spec_from_file_location('resilient_candidate',PREFIX/'production_arena_resilient_broker.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        prepared=module.prepare(sha(manifest))
        report['Stage']='NativeDiagnostics'
        report['NativeDiagnostics']={}
        report['NativeDiagnostics'].update(native_tests(module,prepared,payload['HistoricalInputs'],report['NativeDiagnostics']))
        report['Stage']='RenderBindings'
        command=[PYTHON,'-I','-B',str(PREFIX/'production_arena_resilient_broker.py'),
                 '--expected-manifest-sha256',sha(manifest),'--serve']
        dropin=('[Service]\nExecStart=\nExecStart='+' '.join(command)+'\nReadWritePaths='+str(ARCHIVE)+'\n').encode()
        ops=json.loads(old[OPS])
        model=dict(ExecStart=json.dumps([[PYTHON,command,False]],separators=(',',':')),
            User='root',Group='witchplayer',FragmentPath='/etc/systemd/system/'+BROKER,
            DropInPaths=str(DROPIN),Triggers='')
        ops['UnitPins'][BROKER]=sha(canonical(model))
        new={DROPIN:dropin,OPS:canonical(ops)+b'\n',OPS_BIND:(sha(canonical(ops))+'\n').encode()}
        monitor=json.loads(old[MONITOR])
        for path,raw in new.items():
            require(monitor['FilePins'][str(path)]==sha(old[path]),'MONITOR_BASELINE_MISMATCH')
            monitor['FilePins'][str(path)]=sha(raw)
        for name,raw in {**files,'SHA256.json':manifest}.items():
            require(str(PREFIX/name) not in monitor['FilePins'])
            monitor['FilePins'][str(PREFIX/name)]=sha(raw)
        new[MONITOR]=(json.dumps(monitor,sort_keys=True,indent=2)+'\n').encode()
        new[BIND]=old[BIND].replace(EXPECTED[MONITOR].encode(),sha(new[MONITOR]).encode())
        backup=PREFIX/'rollback';backup.mkdir(mode=0o700)
        for i,(path,raw) in enumerate(old.items()):install(backup/str(i),raw,mode=0o600)
        install(backup/'paths.json',canonical([str(p) for p in old]),mode=0o600)
        report['Stage']='Maintenance';maintenance=True
        ctl('stop',HTIMER)
        for unit in TIMERS:ctl('stop',unit)
        wait_quiet()
        for unit in active:ctl('stop',unit)
        fd=os.open('/opt/witch-player-arena/native-admission.lock',os.O_RDONLY|os.O_NOFOLLOW)
        try:
            fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB);ctl('stop',BROKER)
        finally:os.close(fd)
        changed=True
        for path,raw in new.items():
            install(path,raw,mode=0o600 if path in (MONITOR,BIND) else 0o644,before=old[path])
        subprocess.run(['systemctl','daemon-reload'],check=True,timeout=20)
        ctl('start',BROKER);broker_ready()
        require(state(BROKER)['ActiveState']=='active','NEW_BROKER_NOT_RUNNING')
        report['Stage']='VerifyPublishers'
        report['Health']=refresh(sha(new[MONITOR]))
        for unit in active:ctl('start',unit)
        ctl('start',HTIMER)
        report['MonitorSha256']=sha(new[MONITOR]);report['ManifestSha256']=sha(manifest)
        report['OperationsConfigurationHash']=new[OPS_BIND].decode().strip()
        for unit in TIMERS+[HTIMER]:require(state(unit)['ActiveState']=='active')
        report.update(Status='INSTALLED_AND_NATIVE_VERIFIED',Stage='Complete')
    except Exception as error:
        report.update(Status='STOPPED',ErrorType=type(error).__name__)
        if type(error) is RuntimeError:report['Reason']=str(error)
        if changed:
            report['RollbackAttempted']=True
            try:
                for unit in active:ctl('stop',unit)
                for unit in TIMERS:ctl('stop',unit)
                wait_quiet();ctl('stop',BROKER)
                for path,raw in old.items():
                    current=read(path);require(current in (raw,new[path]))
                    if current!=raw:install(path,raw,mode=0o600 if path in (MONITOR,BIND) else 0o644,before=current)
                subprocess.run(['systemctl','daemon-reload'],check=True,timeout=20)
                ctl('start',BROKER);broker_ready();refresh(EXPECTED[MONITOR])
                for unit in active:ctl('start',unit)
                report['RollbackVerified']=True
            except Exception as rollback_error:
                report['RollbackVerified']=False;report['RollbackErrorType']=type(rollback_error).__name__
    finally:
        if maintenance:
            for unit in TIMERS+[HTIMER]:
                try:ctl('start',unit)
                except Exception:report.setdefault('TimerRestoreErrors',[]).append(unit)
            if not changed:
                try:
                    ctl('start',BROKER)
                    for unit in active:ctl('start',unit)
                except Exception:report['OriginalServicesRestoreError']=True
            elif report.get('Status')!='INSTALLED_AND_NATIVE_VERIFIED':
                # Publisher failure must not skip resuming the original API.
                try:
                    require(all(read(path)==raw for path,raw in old.items()))
                    broker_ready();ctl('start',HEALTH)
                    for unit in active:ctl('start',unit)
                    report['PriorApisResumedAfterFailure']=True
                except Exception:report['OriginalServicesRestoreError']=True
        report['FinishedUnix']=time.time()
        if PREFIX.exists():
            try:install(PREFIX/'rollout-result.json',canonical(report),mode=0o600)
            except Exception:report['ResultDurabilityError']=True
    return report


if __name__=='__main__':
    payload=json.loads(base64.b64decode(sys.argv[1]))
    print('ARENA_PREVENTION_RESULT='+json.dumps(apply(payload),sort_keys=True),flush=True)
