"""Small, explicit security boundary for the single-instance deployment."""
import hashlib
import hmac
import ipaddress
import json
import re
import threading
import time
from collections import OrderedDict
from pathlib import Path

from store import Fault


def read_operators(path):
    path = Path(path)
    if path.stat().st_mode & 0o077:
        raise ValueError('Operator file must only be accessible to its owner (chmod 600)')
    data = json.loads(path.read_text())
    if not isinstance(data, list) or not data:
        raise ValueError('At least one operator is required')
    names, hashes = set(), set()
    for entry in data:
        if (not isinstance(entry, dict) or
                not re.fullmatch(r'[A-Za-z0-9_.@-]{1,80}', entry.get('Name', '')) or
                entry.get('Role') not in ('viewer', 'operator') or
                not re.fullmatch(r'[a-f0-9]{64}', entry.get('TokenHash', ''))):
            raise ValueError('Invalid operator entry')
        if entry['Name'] in names or entry['TokenHash'] in hashes:
            raise ValueError('Duplicate operator identity or credential')
        names.add(entry['Name']); hashes.add(entry['TokenHash'])
    if not any(x['Role'] == 'operator' for x in data):
        raise ValueError('At least one operator with write access is required')
    return data


class AdminAuth:
    def __init__(self, *, token=None, path=None):
        self.token, self.path = token, path
        if path:
            read_operators(path)
        elif not token:
            raise ValueError('Admin credentials required')

    def authenticate(self, token):
        if self.path:
            # Read each time so revocation takes effect without a worker restart.
            digest = hashlib.sha256(token.encode()).hexdigest()
            for entry in read_operators(self.path):
                if hmac.compare_digest(digest, entry['TokenHash']):
                    return {'Name': entry['Name'], 'Role': entry['Role'], 'ActorFromCredential': True}
        elif hmac.compare_digest(token.encode(), self.token.encode()):
            return {'Name': 'local', 'Role': 'operator', 'ActorFromCredential': False}
        raise Fault(401, 'Unauthorized')


class RateLimiter:
    """Thread safe, bounded fixed windows. One Gunicorn worker is intentional."""
    def __init__(self, clock=time.monotonic, max_keys=10000):
        self.clock, self.max_keys = clock, max_keys
        self.windows = OrderedDict()
        self.lock = threading.Lock()

    def check(self, bucket, identity, limit, seconds):
        now = self.clock()
        # Hash identities so neither bearer credentials nor IPs remain in the map.
        key = (bucket, hashlib.sha256(identity.encode()).digest())
        with self.lock:
            # Expire entries before imposing a bounded-memory admission limit.
            if len(self.windows) >= self.max_keys:
                self.windows = OrderedDict((k, v) for k, v in self.windows.items() if v[0] > now)
            expires, count = self.windows.get(key, (now + seconds, 0))
            if expires <= now:
                expires, count = now + seconds, 0
            if count >= limit or (key not in self.windows and len(self.windows) >= self.max_keys):
                raise Fault(429, 'Too many requests; retry later with the same request ID')
            self.windows[key] = (expires, count + 1)


def client_network(environ, production):
    peer = environ.get('REMOTE_ADDR', '')
    if production:
        # Deployment binds Gunicorn to loopback. Caddy overwrites X-Real-IP.
        # Never trust a caller-supplied X-Forwarded-For chain.
        if peer not in ('127.0.0.1', '::1'):
            raise Fault(403, 'Trusted proxy required')
        peer = environ.get('HTTP_X_REAL_IP', '')
    try:
        address = ipaddress.ip_address(peer)
    except ValueError:
        raise Fault(400, 'Invalid client address')
    return str(ipaddress.ip_network(str(address) + ('/64' if address.version == 6 else '/32'), strict=False))
