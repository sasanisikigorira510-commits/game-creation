"""Read-only, root-run prerequisite inspection. No secret values are printed.

No DB connections, Apple calls, service changes, directory creation or key reads.
Intended to be sent as inline Python over the existing pinned SSH connection.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import time


def metadata(path):
    path = Path(path)
    if not path.exists() and not path.is_symlink():
        return dict(Exists=False)
    info = path.lstat()
    return dict(Exists=True, Symlink=path.is_symlink(), UID=info.st_uid, GID=info.st_gid,
                Mode=oct(stat.S_IMODE(info.st_mode)), Directory=stat.S_ISDIR(info.st_mode))


def json_file(path):
    path = Path(path)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Symlink configuration rejected')
    with path.open('rb') as stream:
        raw = stream.read(16385)
    if len(raw) > 16384: raise ValueError('Oversized configuration')
    return json.loads(raw)


def main():
    if os.geteuid() != 0: raise ValueError('Administrator authentication required')
    report = dict(Status='REFUND_HOST_READ_ONLY_INSPECTION',
                  ObservedUTC=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  ChangedServices=False, ChangedDatabase=False, ReadSigningKeys=False)
    units = ('witch-player.service', 'witch-player-qa.service', 'caddy.service',
             'witch-player-refund-sandbox.service', 'witch-player-refund-sandbox-review.service',
             'witch-player-refund-sandbox-notifications.service',
             'witch-player-refunds.service', 'witch-player-refunds.timer')
    report['Units'] = {}
    for unit in units:
        process = subprocess.run(['/usr/bin/systemctl', 'show', unit,
            '--property=Id,LoadState,ActiveState,SubState,WorkingDirectory'],
            capture_output=True, text=True, timeout=10)
        if process.returncode: raise ValueError('Unit inspection failed')
        report['Units'][unit] = dict(line.split('=', 1) for line in process.stdout.splitlines() if '=' in line)
    report['AvailableDiskBytes'] = shutil.disk_usage('/var').free
    report['Paths'] = {p:metadata(p) for p in (
        '/opt/nasus-iap-sandbox-20260928', '/opt/nasus-iap-sandbox-20260928/venv/bin/python',
        '/opt/nasus-iap-sandbox-20260928/wheels', '/etc/nasus-iap-sandbox-20260928/signing.p8',
        '/etc/nasus-iap-sandbox-20260928/roots',
        '/opt/nasus-refund-sandbox-20260928', '/var/lib/nasus-refund-sandbox-20260928',
        '/etc/nasus-refund-sandbox-20260928')}
    identity = json_file('/var/lib/nasus-qa-sandbox-20260927/worker.json')
    report['ExistingIdentityBinding'] = {k:identity[k] for k in ('InstanceId','ClientId','DataDirectory','StateDirectory')}
    report['ExistingIdentityKeyPaths'] = {identity[k]:metadata(identity[k])
                                        for k in ('SigningKeyFile','TokenEncryptionKeyFile')}
    iap = json_file('/var/lib/nasus-qa-sandbox-20260927/iap-sandbox.json')
    report['ExistingPurchaseBinding'] = {k:iap[k] for k in ('Version','Environment','BundleId')}
    gate = json_file('/etc/witch-player-qa/gate.json')
    report['ExistingQAGate'] = dict(CurrentlyValid=gate['IssuedUnix'] <= time.time() < gate['ExpiresUnix'],
                                    ExpiresUTC=datetime.datetime.fromtimestamp(gate['ExpiresUnix'], datetime.timezone.utc).isoformat())
    report['BaselineConfigSHA256'] = {}
    for name in ('/etc/caddy/Caddyfile', '/etc/witch-player/server.env',
                 '/etc/systemd/system/witch-player.service',
                 '/var/lib/nasus-qa-sandbox-20260927/runtime.env',
                 '/etc/systemd/system/witch-player-qa.service.d/95-iap-sandbox.conf'):
        path = Path(name)
        if path.is_symlink(): raise ValueError('Unexpected baseline symlink')
        report['BaselineConfigSHA256'][name] = hashlib.sha256(path.read_bytes()).hexdigest()
    process = subprocess.run(['/usr/bin/ss', '-H', '-ltn'], capture_output=True, text=True, timeout=10, check=True)
    report['CandidatePortsInUse'] = sorted({int(line.split()[3].rsplit(':',1)[-1])
        for line in process.stdout.splitlines() if len(line.split()) >= 4
        and line.split()[3].rsplit(':',1)[-1] in ('8791','8792','8793')})
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('REFUND_HOST_INSPECTION_STOPPED: no configuration or database changes performed.')
        raise SystemExit(1)
