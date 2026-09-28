import json
import os
from pathlib import Path
import sqlite3
import tempfile
import unittest

from account_deletion import AccountDeletion
from deletion_journal import DeletionJournal
from deploy.preflight_deletion_worker import inspect_database, private_file, summarize
from maintenance import initialize
from store import Store


class DeletionPreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.data = self.root / 'data'
        initialize(self.data, 'synthetic-preflight')
        self.store = Store(self.data / 'players.sqlite')
        self.player = 'a' * 32; self.token = 'b' * 64
        self.store.register(self.player, self.token)
        self.metadata = dict(ExistingCodeMatchesInspected=True, CredentialsMetadataSafe=True,
                             RecipientMetadataSafe=True, AgeInstalled=True)

    def test_missing_journal_report_does_not_create_tables_or_change_db(self):
        before = (self.data / 'players.sqlite').read_bytes()
        result = inspect_database(self.data)
        report = summarize(dict(Status='INSPECTED', Database=result), self.metadata)
        self.assertIn('DELETION_SCHEMA_NOT_DEPLOYED', report['Blockers'])
        self.assertFalse(report['WorkerSchemaReady'])
        self.assertEqual(before, (self.data / 'players.sqlite').read_bytes())
        with self.store.connect() as db:
            self.assertIsNone(db.execute("SELECT 1 FROM sqlite_master WHERE name='deletion_outbox'").fetchone())

    def test_reports_only_counts_and_booleans_not_account_secrets(self):
        journal = DeletionJournal(self.store, 'synthetic-preflight')
        api = AccountDeletion(self.store, journal=journal)
        confirm = api.preview(self.player, self.token)['ConfirmationToken']
        from store import Fault
        with self.assertRaises(Fault): api.commit(self.player, self.token, confirm)
        result = inspect_database(self.data)
        self.assertEqual(1, result['PendingEvents'])
        self.assertTrue(result['JournalInstanceMatches'])
        report = summarize(dict(Status='INSPECTED', Database=result), self.metadata)
        self.assertTrue(report['WorkerSchemaReady'])
        self.assertFalse(report['Installed'])
        for value in (self.player, self.token, confirm, 'synthetic-preflight'):
            self.assertNotIn(value, json.dumps(report))

    def test_missing_database_does_not_create_an_empty_database(self):
        with self.assertRaises(ValueError): inspect_database(self.root / 'missing')
        self.assertFalse((self.root / 'missing').exists())

    def test_quarantine_permissions_and_symlinks_rejected(self):
        marker = self.data / 'RECOVERY-PENDING.txt'; marker.touch()
        with self.assertRaises(ValueError): inspect_database(self.data)
        marker.unlink()
        marker.symlink_to(self.root / 'absent')
        with self.assertRaises(ValueError): inspect_database(self.data)
        marker.unlink()
        (self.data / 'players.sqlite').chmod(0o644)
        with self.assertRaises(ValueError): inspect_database(self.data)

    def test_private_file_metadata_only(self):
        path = self.root / 'private'; path.write_text('secret-do-not-read'); path.chmod(0o600)
        self.assertTrue(private_file(path, os.geteuid()))
        self.assertFalse(private_file(path, os.geteuid() + 1))
        link = self.root / 'link'; link.symlink_to(path)
        self.assertFalse(private_file(link, os.geteuid()))
        path.chmod(0o644)
        self.assertFalse(private_file(path, os.geteuid()))

    def test_unavailable_inspection_and_missing_configs_fail_closed(self):
        for result in ({}, {'Status': 'INSPECTION_FAILED'}):
            report = summarize(result, {})
            self.assertFalse(report['WorkerSchemaReady'])
            self.assertIn('DATABASE_INSPECTION_FAILED', report['Blockers'])
            self.assertIn('CREDENTIAL_CONFIGURATION_REVIEW_REQUIRED', report['Blockers'])
            self.assertEqual(0, report['CloudRequests'])

    def test_wrong_journal_instance_is_not_ready(self):
        journal = DeletionJournal(self.store, 'synthetic-preflight')
        AccountDeletion(self.store, journal=journal)
        with self.store.connect() as db:
            db.execute("UPDATE deletion_journal_meta SET instance='wrong-instance'")
        result = inspect_database(self.data)
        report = summarize(dict(Status='INSPECTED', Database=result), self.metadata)
        self.assertIn('JOURNAL_INSTANCE_MISMATCH', report['Blockers'])

    def test_integrity_failure_has_no_success_report(self):
        (self.data / 'players.sqlite').write_bytes(b'not-a-database')
        with self.assertRaises(sqlite3.DatabaseError): inspect_database(self.data)


if __name__ == '__main__': unittest.main()
