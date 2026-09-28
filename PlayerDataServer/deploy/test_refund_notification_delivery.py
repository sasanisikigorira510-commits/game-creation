"""Explicit, one-shot Apple Sandbox TEST notification; never purchase/refund."""
import json
import os
from pathlib import Path
import pwd
import re
import stat
import time

ROOT = Path('/var/lib/nasus-refund-sandbox-20260928')
CREDS = Path('/etc/nasus-refund-sandbox-20260928')
REPORT = ROOT/'notification-test-20260929'
BUNDLE = 'com.nasus.dungeonmonsterroguelike'


def require(ok):
    if not ok: raise ValueError('Sandbox test precondition failed')


def private(path):
    path = Path(path)
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    with path.open('rb') as source:
        info = os.fstat(source.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == os.geteuid()
                and not info.st_mode & 0o077 and info.st_size < 65536)
        return source.read()


def save(name, data):
    fd = os.open(REPORT/name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'w') as stream:
        json.dump(data, stream, indent=2); stream.flush(); os.fsync(stream.fileno())


def verified_result(response, verifier):
    notice = verifier.verify_and_decode_notification(response.signedPayload)
    enum = lambda item: getattr(item, 'value', item)
    require(notice.version == '2.0' and enum(notice.notificationType) == 'TEST'
            and notice.data.bundleId == BUNDLE and enum(notice.data.environment) == 'Sandbox')
    results = [enum(item.sendAttemptResult) for item in (response.sendAttempts or [])]
    require(all(isinstance(v, str) and re.fullmatch('[A-Z_]{1,80}', v) for v in results))
    return dict(Status='APPLE_TEST_DELIVERED' if 'SUCCESS' in results else 'APPLE_TEST_NOT_DELIVERED',
                Environment='Sandbox', SignatureVerified=True, SendAttempts=results,
                CheckedUnix=time.time(), PurchaseOrRefundExecuted=False)


def main():
    os.umask(0o077)
    require(os.geteuid() == pwd.getpwnam('nasusrefund').pw_uid)
    gate = json.loads(private(CREDS/'gate.json'))
    require(0 < gate['ExpiresUnix']-gate['IssuedUnix'] <= 86400
            and gate['IssuedUnix'] <= time.time() < gate['ExpiresUnix']-300)
    config = json.loads(private(CREDS/'iap.json'))
    require(config['WITCH_APPLE_ENVIRONMENT'] == 'Sandbox' and config['WITCH_APPLE_BUNDLE_ID'] == BUNDLE
            and config['WITCH_APPLE_KEY_FILE'] == str(CREDS/'iap-signing.p8')
            and config['WITCH_APPLE_ROOTS_DIR'] == str(CREDS/'roots'))
    from appstoreserverlibrary.api_client import AppStoreServerAPIClient, APIException
    from appstoreserverlibrary.models.Environment import Environment
    from appstoreserverlibrary.signed_data_verifier import SignedDataVerifier
    roots = [private(p) for p in sorted((CREDS/'roots').glob('*.cer'))]
    require(bool(roots))
    verifier = SignedDataVerifier(roots, True, Environment.SANDBOX, BUNDLE)
    client = AppStoreServerAPIClient(private(CREDS/'iap-signing.p8'), config['WITCH_APPLE_KEY_ID'],
                                    config['WITCH_APPLE_ISSUER_ID'], BUNDLE, Environment.SANDBOX)
    REPORT.mkdir(mode=0o700)  # No blind repeat sends, even after an ambiguous timeout.
    save('started.json', dict(StartedUnix=time.time(), Environment='Sandbox'))
    print('1/2 Request one Apple Sandbox TEST notification; no purchase or refund.', flush=True)
    requested = client.request_test_notification()
    token = requested.testNotificationToken
    require(isinstance(token, str) and 0 < len(token) <= 4096)
    save('token.json', dict(TestNotificationToken=token))
    print('2/2 Checking Apple delivery status (token retained privately).', flush=True)
    for attempt in range(4):
        if attempt: time.sleep(10)
        try:
            response = client.get_test_notification_status(token)
        except APIException as error:
            if error.http_status_code == 404: continue
            raise  # No retry on permission or rate-limit errors.
        if not response.sendAttempts: continue
        result = verified_result(response, verifier)
        save('result.json', result)
        print(json.dumps(result), flush=True)
        return 0 if result['Status'] == 'APPLE_TEST_DELIVERED' else 2
    save('result.json', dict(Status='APPLE_TEST_PENDING', CheckedUnix=time.time()))
    print('APPLE_TEST_PENDING: inspect saved token; do not send another test blindly.', flush=True)
    return 2


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        code = getattr(error, 'http_status_code', None)
        print('APPLE_TEST_STOPPED' + (' HTTP '+str(code) if type(code) is int else '')
              + ': private credentials/payload omitted; do not rerun blindly.', flush=True)
        raise SystemExit(1)
