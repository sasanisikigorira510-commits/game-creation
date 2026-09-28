import contextlib
import hashlib
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
from deploy import prepare_apple_staging_mac as local
from deploy import install_apple_staging as remote


class StagingSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve()
        self.signing=self.root/'AuthKey_38GM3RYVD9.p8'
        self.signing.write_bytes(ec.generate_private_key(ec.SECP256R1()).private_bytes(
            serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        self.signing.chmod(0o600)

    def invoke(self):
        out=io.StringIO()
        with patch.object(local,'PRIVATE',self.root),contextlib.redirect_stdout(out): local.keys()
        return out.getvalue()

    def test_local_key_is_private_retained_and_not_printed(self):
        first=self.invoke()
        token=self.root/'token-encryption-20260926.key'; raw=token.read_bytes()
        self.assertEqual(len(raw),32); self.assertEqual(token.stat().st_mode & 0o777,0o600)
        self.assertEqual(first,self.invoke()); self.assertEqual(raw,token.read_bytes())
        self.assertEqual(first.split(),[hashlib.sha256(self.signing.read_bytes()).hexdigest(),hashlib.sha256(raw).hexdigest()])
        self.assertNotIn('PRIVATE KEY',first)

    def test_wrong_existing_token_never_overwritten(self):
        token=self.root/'token-encryption-20260926.key'; token.write_bytes(b'keep-me'); token.chmod(0o600)
        with self.assertRaises(ValueError): self.invoke()
        self.assertEqual(token.read_bytes(),b'keep-me')

    def test_shared_signing_key_rejected(self):
        self.signing.chmod(0o644)
        with self.assertRaises(ValueError): self.invoke()
        self.assertFalse((self.root/'token-encryption-20260926.key').exists())

    def test_remote_input_owner_size_and_permissions(self):
        self.assertEqual(remote.safe_file(self.signing,os.getuid(),16384,True),self.signing.read_bytes())
        with self.assertRaises(ValueError): remote.safe_file(self.signing,os.getuid()+1)
        with self.assertRaises(ValueError): remote.safe_file(self.signing,os.getuid(),10)
        self.signing.chmod(0o644)
        with self.assertRaises(ValueError): remote.safe_file(self.signing,os.getuid(),16384,True)

    def test_symlink_and_overwrite_rejected(self):
        link=self.root/'linked'; link.symlink_to(self.signing)
        with self.assertRaises(ValueError): remote.safe_file(link,os.getuid())
        before=self.signing.read_bytes()
        with self.assertRaises(FileExistsError): remote.write_new(self.signing,b'replace')
        self.assertEqual(before,self.signing.read_bytes())

    def test_wrapper_pins_installer_and_helper(self):
        directory=Path(__file__).resolve().parents[1]/'deploy'
        wrapper=(directory/'run-apple-staging-mac.sh').read_text()
        for name in ('install_apple_staging.py','prepare_apple_staging_mac.py'):
            self.assertIn(hashlib.sha256((directory/name).read_bytes()).hexdigest(),wrapper)
        self.assertNotIn('NOPASSWD',wrapper)
