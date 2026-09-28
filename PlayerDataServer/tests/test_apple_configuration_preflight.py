import importlib.util
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('apple_preflight', Path(__file__).resolve().parents[1] / 'deploy/preflight_apple_configuration.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class AppleConfigurationPreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name).resolve()
        self.database = self.directory / 'players.sqlite'
        with sqlite3.connect(self.database) as db:
            db.execute('CREATE TABLE sentinel (secret TEXT)')
            db.execute('INSERT INTO sentinel VALUES (?)', ('DO_NOT_PRINT_TOKEN',))
        self.database.chmod(0o600)

    def test_missing_apple_schema_is_reported_without_migration(self):
        before = self.database.read_bytes()
        result = module.inspect_database(self.directory)
        self.assertTrue(result['QuickCheckPassed'])
        self.assertFalse(any(result['RequiredColumnsPresent'].values()))
        self.assertNotIn('DO_NOT_PRINT_TOKEN', json.dumps(result))
        self.assertEqual(before, self.database.read_bytes())

    def test_schema_and_counts_only(self):
        with sqlite3.connect(self.database) as db:
            db.execute('CREATE TABLE apple_links(subject_hash TEXT, player TEXT, created TEXT)')
            db.execute("INSERT INTO apple_links VALUES ('PRIVATE_SUBJECT','PRIVATE_PLAYER','DATE')")
        result = module.inspect_database(self.directory)
        self.assertTrue(result['RequiredColumnsPresent']['apple_links'])
        self.assertEqual(result['RowCounts']['apple_links'], 1)
        self.assertNotIn('PRIVATE_', json.dumps(result))

    def test_shared_database_rejected(self):
        self.database.chmod(0o644)
        with self.assertRaises(ValueError): module.inspect_database(self.directory)

    def test_quarantine_rejected(self):
        (self.directory / 'RECOVERY-PENDING.txt').touch()
        with self.assertRaises(ValueError): module.inspect_database(self.directory)

    def test_symlink_metadata_rejected(self):
        link = self.directory / 'alias'
        link.symlink_to(self.database)
        self.assertFalse(module.metadata(link)['OwnerPrivate'])

    def test_missing_metadata(self):
        self.assertEqual(module.metadata(self.directory / 'absent'), {'Present': False, 'OwnerPrivate': False})
