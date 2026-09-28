"""Offline provisioning and verified SQLite backups. Never overwrite live data."""
import argparse
import datetime as dt
import hashlib
import json
import os
import re
import secrets
import sqlite3
import tempfile
import uuid
from contextlib import closing
from pathlib import Path

TABLES = {'players', 'snapshots', 'operations', 'claims', 'purchases', 'audit', 'flags'}


def sync_directory(path):
    descriptor = os.open(str(path), os.O_RDONLY)
    try: os.fsync(descriptor)
    finally: os.close(descriptor)


def write_private(path, data):
    path = Path(path)
    with os.fdopen(os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as output:
        output.write(data)
        output.flush(); os.fsync(output.fileno())
    sync_directory(path.parent)


def verify_database(path):
    path = Path(path).resolve()
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro', uri=True)) as db:
        if db.execute('PRAGMA integrity_check').fetchall() != [('ok',)]:
            raise ValueError('Database integrity check failed')
        if db.execute('PRAGMA foreign_key_check').fetchone():
            raise ValueError('Database foreign key check failed')
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        if not TABLES <= tables:
            raise ValueError('Not a complete player database')
        from refund_integrity import validate_refunds
        validate_refunds(db)


def backup_database(source, target):
    source, target = Path(source).resolve(), Path(target).absolute()
    if target.exists() or target.is_symlink() or source == target.resolve():
        raise ValueError('Backup target must be new; never overwrite a database or backup')
    descriptor, temporary = tempfile.mkstemp(prefix='.backup-', dir=target.parent)
    os.close(descriptor)
    temporary = Path(temporary)
    try:
        with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as src, closing(sqlite3.connect(str(temporary))) as dest:
            # Includes committed WAL records; does not require stopping the API.
            src.backup(dest, pages=256, sleep=0.05)
            dest.execute('PRAGMA journal_mode=DELETE')
        verify_database(temporary)
        with temporary.open('rb') as data: os.fsync(data.fileno())
        # link provides atomic publication AND refuses a concurrently created target.
        os.link(temporary, target)
        sync_directory(target.parent)
    finally:
        temporary.unlink(missing_ok=True)
    return str(target)


def digest_file(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as data:
        for block in iter(lambda: data.read(1024 * 1024), b''): digest.update(block)
    return digest.hexdigest()


def backup_generation(source, directory):
    identity = Path(source).resolve().parent / 'instance-id'
    instance = identity.read_text().strip() if identity.is_file() else None
    if instance is not None and not re.fullmatch(r'[a-zA-Z0-9_-]{8,80}', instance):
        raise ValueError('Invalid source instance identity')
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    if directory.stat().st_mode & 0o077:
        raise ValueError('Backup directory must have mode 700')
    stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    target = directory / ('players-' + stamp + '-' + uuid.uuid4().hex[:8] + '.sqlite')
    backup_database(source, target)
    manifest = {'File': target.name, 'Sha256': digest_file(target), 'Bytes': target.stat().st_size,
                'CreatedUtc': dt.datetime.now(dt.timezone.utc).isoformat()}
    if instance is not None:
        manifest['InstanceId'] = instance
    write_private(str(target) + '.json', json.dumps(manifest, sort_keys=True) + '\n')
    return target


def verify_generation(path):
    path = Path(path)
    manifest = json.loads(Path(str(path) + '.json').read_text())
    if (manifest['File'] != path.name or manifest['Bytes'] != path.stat().st_size or
            manifest['Sha256'] != digest_file(path)):
        raise ValueError('Backup does not match its manifest')
    verify_database(path)


def prune_generations(directory, recent=48, days=30):
    # Retain latest 48 plus one per UTC day for the last 30 days.
    pattern = re.compile(r'players-(\d{8})T\d{12}Z-[a-f0-9]{8}\.sqlite')
    entries = sorted((p for p in Path(directory).iterdir() if pattern.fullmatch(p.name)), reverse=True)
    keep = set(entries[:recent]); daily = set()
    earliest = (dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=days)).strftime('%Y%m%d')
    for entry in entries:
        day = pattern.fullmatch(entry.name).group(1)
        if day >= earliest and day not in daily:
            keep.add(entry); daily.add(day)
    for entry in entries:
        if entry not in keep and Path(str(entry) + '.json').exists():
            entry.unlink(); Path(str(entry) + '.json').unlink()
    sync_directory(directory)


def initialize(directory, instance_id):
    from store import Store
    if not re.fullmatch(r'[a-zA-Z0-9_-]{8,80}', instance_id):
        raise ValueError('Instance ID must contain 8-80 letters, digits, underscores or hyphens')
    directory = Path(directory)
    if directory.exists() and any(directory.iterdir()):
        raise ValueError('Initialization requires an empty directory')
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    os.chmod(directory, 0o700)
    Store(directory / 'players.sqlite')
    os.chmod(directory / 'players.sqlite', 0o600)
    write_private(directory / 'instance-id', instance_id + '\n')


def add_operator(path, name, role, token_out):
    from security import read_operators
    path, token_out = Path(path), Path(token_out)
    if path.absolute() == token_out.absolute(): raise ValueError('Use a separate token output file')
    if not re.fullmatch(r'[A-Za-z0-9_.@-]{1,80}', name) or role not in ('viewer', 'operator'):
        raise ValueError('Invalid operator name or role')
    entries = read_operators(path) if path.exists() else []
    if not entries and role != 'operator': raise ValueError('First identity must have operator role')
    if any(e['Name'] == name for e in entries): raise ValueError('Operator already exists')
    token = secrets.token_urlsafe(48)
    entry = {'Name': name, 'Role': role, 'TokenHash': hashlib.sha256(token.encode()).hexdigest()}
    write_private(token_out, token + '\n')
    descriptor, temporary = tempfile.mkstemp(prefix='.operators-', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as data:
            data.write(json.dumps(entries + [entry], indent=2) + '\n')
            data.flush(); os.fsync(data.fileno())
        os.replace(temporary, path)
        sync_directory(path.parent)
    finally:
        Path(temporary).unlink(missing_ok=True)


def restore_copy(source, destination):
    source, destination = Path(source), Path(destination)
    verify_generation(source)
    destination.mkdir(mode=0o700)  # Existing directory is deliberately rejected.
    try:
        write_private(destination / 'RECOVERY-PENDING.txt',
            'Investigation copy only. No instance-id is generated. Do not serve this database until '\
            'deletions, Apple grants, purchases and operations after the backup have been reconciled.\n')
        backup_database(source, destination / 'players.sqlite')
    except Exception:
        # Keep any failed recovery artifacts for investigation; never touch source.
        raise


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    init = commands.add_parser('init'); init.add_argument('--data-dir', required=True); init.add_argument('--instance-id', required=True)
    admin = commands.add_parser('admin-add')
    admin.add_argument('--file', required=True); admin.add_argument('--name', required=True)
    admin.add_argument('--role', choices=('viewer', 'operator'), default='operator'); admin.add_argument('--token-out', required=True)
    backup = commands.add_parser('backup'); backup.add_argument('--database', required=True); backup.add_argument('--output-dir', required=True)
    verify = commands.add_parser('verify'); verify.add_argument('--backup', required=True)
    restore = commands.add_parser('restore-copy'); restore.add_argument('--backup', required=True); restore.add_argument('--new-directory', required=True)
    args = parser.parse_args()
    if args.command == 'init': initialize(args.data_dir, args.instance_id)
    elif args.command == 'admin-add': add_operator(args.file, args.name, args.role, args.token_out)
    elif args.command == 'backup':
        target = backup_generation(args.database, args.output_dir)
        verify_generation(target)
        prune_generations(args.output_dir)
        print(target)
    elif args.command == 'verify': verify_generation(args.backup)
    elif args.command == 'restore-copy': restore_copy(args.backup, args.new_directory)


if __name__ == '__main__': main()
