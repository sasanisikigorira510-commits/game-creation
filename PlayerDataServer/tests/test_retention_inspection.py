import datetime as dt
import unittest
from deploy.inspect_backup_retention import BUCKET, NS, versioning, page, collect


def wrap(name, body): return ('<'+name+' xmlns="'+NS[1:-1]+'">'+body+'</'+name+'>').encode()


def listing(identifier='a', token=None):
    body = '<Name>'+BUCKET+'</Name><Prefix>db/</Prefix><KeyCount>1</KeyCount>'
    body += '<IsTruncated>'+('true' if token else 'false')+'</IsTruncated>'
    body += '<Contents><Key>db/20260925T141536Z-'+identifier*32+'.tar.gz.age</Key><Size>100</Size>'
    body += '<LastModified>2026-09-25T14:15:36Z</LastModified></Contents>'
    if token: body += '<NextContinuationToken>'+token+'</NextContinuationToken>'
    return wrap('ListBucketResult', body)


class RetentionInspectionTests(unittest.TestCase):
    def test_versioning_empty_enabled_suspended(self):
        self.assertEqual('NeverEnabled', versioning(wrap('VersioningConfiguration', '')))
        for status in ('Enabled', 'Suspended'):
            self.assertEqual(status, versioning(wrap('VersioningConfiguration', '<Status>'+status+'</Status>')))

    def test_unknown_or_duplicate_status_rejected(self):
        for content in ('<Error>denied</Error>', '<Status>Unknown</Status>', '<Status>Enabled</Status>'*2):
            with self.assertRaises(ValueError): versioning(wrap('VersioningConfiguration', content))

    def test_complete_paginated_collection(self):
        responses = [wrap('VersioningConfiguration',''), listing('a','next'), listing('b')]
        queries = []
        def get(query): queries.append(query); return responses.pop(0)
        clock = lambda: dt.datetime(2026,9,25,15,tzinfo=dt.timezone.utc)
        result = collect(get, None, clock)
        self.assertEqual(2, len(result['Objects']))
        self.assertTrue(result['Complete'])
        self.assertEqual('next', queries[2]['continuation-token'])
        self.assertEqual(2, len(result['PageSha256']))

    def test_duplicate_keys_stop_collection(self):
        responses = [wrap('VersioningConfiguration',''), listing('a','next'), listing('a')]
        with self.assertRaises(ValueError): collect(lambda q: responses.pop(0), None)

    def test_bad_scope_and_malformed_pages_stop(self):
        for raw in (listing().replace(b'<Prefix>db/', b'<Prefix>deletions/'),
                    listing().replace(b'<KeyCount>1',b'<KeyCount>2'),
                    listing().replace(b'<IsTruncated>false',b'<IsTruncated>true'),
                    listing().replace(b'db/20260925',b'deletions/20260925')):
            with self.assertRaises(ValueError): page(raw)

    def test_entities_not_accepted(self):
        with self.assertRaises(ValueError):
            versioning(b'<!DOCTYPE x [<!ENTITY z "x">]>'+wrap('VersioningConfiguration', '&z;'))

    def test_permission_failure_not_retried(self):
        queries=[]
        def get(query): queries.append(query); raise PermissionError('test')
        with self.assertRaises(PermissionError): collect(get, None)
        self.assertEqual(1,len(queries))
