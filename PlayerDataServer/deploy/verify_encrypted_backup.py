"""Decrypt and restore an age DB snapshot entirely in memory; print no player data."""
import argparse
import hashlib
import io
import json
import re
import sqlite3
import subprocess
import tarfile
from contextlib import closing
from pathlib import Path

MAX_ARCHIVE = 64 * 1024 * 1024
MAX_DATABASE = 128 * 1024 * 1024
TABLES = {'players', 'snapshots', 'operations', 'claims', 'purchases', 'audit', 'flags'}


def verify_payload(payload):
    if len(payload) > MAX_ARCHIVE:
        raise ValueError('Archive exceeds this small-server verification limit')
    with tarfile.open(fileobj=io.BytesIO(payload), mode='r:gz') as archive:
        members = archive.getmembers()
        if len(members) != 2 or any(not m.isfile() for m in members):
            raise ValueError('Expected exactly a database and its manifest')
        databases = [m for m in members if re.fullmatch(r'players-\d{8}T\d{12}Z-[a-f0-9]{8}\.sqlite', m.name)]
        if len(databases) != 1:
            raise ValueError('Unexpected database filename')
        database = databases[0]
        manifests = [m for m in members if m.name == database.name + '.json']
        if len(manifests) != 1 or database.size > MAX_DATABASE or manifests[0].size > 16384:
            raise ValueError('Invalid manifest or member sizes')
        data = archive.extractfile(database).read()
        manifest = json.load(archive.extractfile(manifests[0]))
    digest = hashlib.sha256(data).hexdigest()
    if manifest.get('File') != database.name or manifest.get('Bytes') != len(data) or manifest.get('Sha256') != digest:
        raise ValueError('Database checksum does not match manifest')
    # No plaintext database or SQL dump is written to the Mac filesystem.
    with closing(sqlite3.connect(':memory:')) as source, closing(sqlite3.connect(':memory:')) as restored:
        source.deserialize(data)
        # Do not allow database schema to invoke unsafe application functions.
        source.execute('PRAGMA trusted_schema=OFF')
        source.execute('PRAGMA query_only=ON')
        source.backup(restored)
        restored.execute('PRAGMA trusted_schema=OFF')
        restored.execute('PRAGMA query_only=ON')
        for db in (source, restored):
            if db.execute('PRAGMA integrity_check').fetchall() != [('ok',)]:
                raise ValueError('SQLite integrity check failed')
            if db.execute('PRAGMA foreign_key_check').fetchone():
                raise ValueError('Foreign key check failed')
            tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
            if not TABLES <= tables:
                raise ValueError('Required database tables missing')
        if source.serialize() != restored.serialize():
            raise ValueError('Restored database differs from source snapshot')
    return {'Status': 'RESTORE_VERIFIED_IN_MEMORY', 'Database': database.name,
            'Bytes': len(data), 'Sha256': digest, 'PlaintextWrittenToDisk': False}


def verify_encrypted(age, identity, encrypted):
    identity, encrypted = Path(identity), Path(encrypted)
    if identity.stat().st_mode & 0o077:
        raise ValueError('Decryption identity must have mode 600')
    if encrypted.stat().st_size > MAX_ARCHIVE:
        raise ValueError('Encrypted backup too large for this verification tool')
    result = subprocess.run([str(age), '--decrypt', '--identity', str(identity), str(encrypted)], capture_output=True)
    if result.returncode:
        raise ValueError('Decryption/authentication failed; no plaintext was saved')
    return verify_payload(result.stdout)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--age', required=True)
    parser.add_argument('--identity', required=True)
    parser.add_argument('--backup', required=True)
    args = parser.parse_args()
    print(json.dumps(verify_encrypted(args.age, args.identity, args.backup), sort_keys=True))
