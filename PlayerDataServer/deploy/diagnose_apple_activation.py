"""Read-only inspection of a stopped Apple activation. Never publishes health."""
import hashlib
import json
import math
import os
from pathlib import Path
import pwd
import stat
import sys
import time

HEALTH = Path('/usr/local/lib/nasus-apple-health-20260926')
HASHES = {
    'apple_integrated_health.py':'c68f6483dcd537cc98e855391c0c89e7c3ef8f4e7591e148f06192733e02a5ff',
    'apple_revocation_health.py':'889009d1cf6a4a674539722b038f071b0d35f03a01ff69d3c2ae97d79245e88f',
    'backup_health.py':'d08316f245be3ba7fa3ab1530df3cabf56ea855433845af5b9eb15e5c80244bc',
    'retention_health.py':'84e395abd88f2dc8565369d182b59bbe2f95921f851c53481e5e54bec9fdbf67',
    'base_health.py':'a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb'}


def checked_code(path, digest):
    if any(p.is_symlink() for p in (path,*path.parents)): raise ValueError('Code metadata')
    for item in (path,*path.parents):
        info=item.stat()
        if info.st_uid != 0 or info.st_mode & 0o022: raise ValueError('Code metadata')
    if not path.is_file() or path.stat().st_size > 1024*1024: raise ValueError('Code size')
    if hashlib.sha256(path.read_bytes()).hexdigest() != digest: raise ValueError('Code digest')


def safe_marker(path):
    with path.open('rb') as stream: raw=stream.read(4097)
    if len(raw)>4096: raise ValueError('Marker size')
    value=json.loads(raw)
    stamp=value.get('CheckedUnix')
    if type(stamp) not in (int,float) or not math.isfinite(stamp): raise ValueError('Marker timestamp')
    status=value.get('Status')
    allowed={'APPLE_REVOCATION_BATCH_OBSERVED','APPLE_REVOCATION_BATCH_STOPPED',
             'APPLE_WORKER_RUNNING','APPLE_WORKER_INTERRUPTED'}
    return dict(Healthy=value.get('Healthy') is True,
                Status=status if status in allowed else 'UNRECOGNIZED',
                AgeSeconds=round(time.time()-stamp,3)),stamp


def inspect():
    if os.geteuid()!=0 or len(sys.argv)!=1: raise ValueError('Root diagnostic only')
    for name,digest in HASHES.items(): checked_code(HEALTH/name,digest)
    sys.path.insert(0,str(HEALTH))
    import apple_integrated_health as apple
    import retention_health
    import base_health
    from apple_revocation_health import healthy_runtime
    result=dict(Status='APPLE_ACTIVATION_DIAGNOSED',ConfigurationChanged=False,
                DatabaseChanged=False,WorkerStarted=False,HealthPublished=False,
                InstalledCodeMatches=True)
    uid=pwd.getpwnam('witchplayer').pw_uid
    metadata=apple.private_marker(apple.REPORT,uid)
    result['AppleMarkerMetadataValid']=metadata
    service=apple.properties('witch-player-apple-revoke.service')
    timer=apple.properties('witch-player-apple-revoke.timer')
    result['Service']=service; result['Timer']=timer
    if metadata:
        marker,stamp=safe_marker(apple.REPORT); result['AppleMarker']=marker
        # Strictly hypothetical classifier-only replay, NOT evidence of an
        # enabled timer or of current health. No systemctl mutations occur.
        hypothetical_timer=dict(timer,ActiveState='active',UnitFileState='enabled')
        result['HistoricalMarkerClassifierWithHypotheticalEnabledTimer']=healthy_runtime(
            apple.REPORT,service,hypothetical_timer,now=stamp)
    reasons=[]
    result['ExistingHealth']=retention_health.collect(base_health,on_reason=reasons.append)
    result['ExistingReasons']=reasons
    reasons=[]
    result['CombinedCurrentHealth']=apple.collect(
        lambda **kw:retention_health.collect(base_health,**kw),on_reason=reasons.append)
    result['CombinedCurrentReasons']=reasons
    result['NewHookRetainedAfterRollback']=(HEALTH/'disabled-hook.conf').is_file()
    result['NewHookActive']=Path('/etc/systemd/system/witch-player-health.service.d/80-apple-worker.conf').exists()
    return result


if __name__=='__main__':
    try: print(json.dumps(inspect(),sort_keys=True))
    except Exception as error:
        # Types and code locations only; no exception text, file contents or keys.
        import traceback
        print(json.dumps(dict(Status='APPLE_DIAGNOSTIC_STOPPED',Type=type(error).__name__,
            Frames=[dict(File=Path(frame.filename).name,Line=frame.lineno)
                    for frame in traceback.extract_tb(error.__traceback__)]),sort_keys=True))
        raise SystemExit(1)
