"""Fixed private inventory prefix, GET/PUT only. Reuses configured credentials."""
import json
import re
import subprocess

from deletion_inventory_ledger import MAX_CIPHER, canonical
from deploy.sakura_deletion_journal import ENDPOINT, _private

KEY = re.compile(r'deletion-inventories/[A-Za-z0-9_-]{8,80}/(?:head\.json|[0-9]{20}-[a-f0-9]{64}\.age)')


class SakuraInventoryStore:
    def __init__(self, config, *, run=subprocess.run):
        self.config, self.run = _private(config), run

    def request(self, key, data=None):
        if not isinstance(key, str) or not KEY.fullmatch(key): raise ValueError('Inventory scope rejected')
        _private(self.config)
        args = ['/usr/bin/curl', '--disable', '--config', str(self.config),
                '--aws-sigv4', 'aws:amz:jp-east-1:s3', '--proto', '=https',
                '--silent', '--show-error', '--retry', '0', '--connect-timeout', '10',
                '--max-time', '20', '--max-filesize', str(MAX_CIPHER), '--write-out', '\n%{http_code}',
                ENDPOINT+key]
        if data is None: args += ['--request', 'GET']
        else:
            if not isinstance(data, bytes) or not 0 < len(data) <= MAX_CIPHER: raise ValueError('Inventory size rejected')
            if key.endswith('.age'):
                if not data.startswith(b'age-encryption.org/v1\n'): raise ValueError('Ciphertext required')
            else:
                value = json.loads(data)
                if (len(data) > 4096 or not isinstance(value, dict) or canonical(value) != data
                        or set(value) != {'Version','InstanceId','Sequence','InventoryKey','CipherSha256',
                                         'CipherBytes','InventorySha256','Events','CreatedUtc','PreviousHeadSha256'}):
                    raise ValueError('Only receipt metadata may be published')
            args += ['--request','PUT','--data-binary','@-','--header','x-amz-acl: private',
                     '--header','Content-Type: application/octet-stream','--output','/dev/null']
        result = self.run(args, input=data, capture_output=True, timeout=25)
        if result.returncode or len(result.stdout) > MAX_CIPHER+4: raise RuntimeError('Inventory transfer failed')
        body, separator, status = result.stdout.rpartition(b'\n')
        if not separator: raise RuntimeError('Inventory response invalid')
        if data is None and status == b'404': return None
        if status != b'200': raise RuntimeError('Inventory access or transfer failed; no fallback')
        return body

    def get(self, key): return self.request(key)
    def put(self, key, data): self.request(key, data)
