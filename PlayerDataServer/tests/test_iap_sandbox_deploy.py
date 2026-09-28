import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import deploy.install_iap_sandbox as deploy


class SandboxDeployTests(unittest.TestCase):
    def setUp(self):
        tmp=tempfile.TemporaryDirectory(); self.addCleanup(tmp.cleanup)
        self.source=Path(tmp.name)
        self.files={name:b'synthetic' for name in deploy.FILES}
        self.files['iap-sandbox.json']=Path('deploy/iap-sandbox.json.example').read_bytes()
        self.files['wheels/example-1-py3-none-any.whl']=b'synthetic-wheel'
        self.manifest=dict(Files={name:hashlib.sha256(raw).hexdigest() for name,raw in self.files.items()},
                           SigningKeySha256='a'*64)

    def package(self):
        with patch.object(deploy,'SOURCE',self.source), \
                patch.object(deploy,'read',side_effect=lambda path,uid:self.files[str(path.relative_to(self.source))]):
            return deploy.package(self.manifest,123)

    def test_only_reviewed_files_and_matching_hashes(self):
        self.assertEqual(self.files,self.package())
        self.files['apple_verifier.py']=b'tampered'
        with self.assertRaises(ValueError):self.package()

    def test_traversal_or_executable_outside_allowlist_rejected(self):
        for name in ('../other.py','wheels/../../other.py','install.sh','roots/unreviewed.cer'):
            with self.subTest(name=name):
                self.manifest['Files'][name]='a'*64
                with self.assertRaises(ValueError):self.package()
                del self.manifest['Files'][name]

    def test_production_configuration_rejected(self):
        config=json.loads(self.files['iap-sandbox.json']);config['Environment']='Production'
        self.files['iap-sandbox.json']=json.dumps(config).encode()
        self.manifest['Files']['iap-sandbox.json']=hashlib.sha256(self.files['iap-sandbox.json']).hexdigest()
        with self.assertRaises(ValueError):self.package()

    def test_override_does_not_replace_isolation_environment_or_bind_publicly(self):
        text=deploy.override()
        self.assertIn('--bind 127.0.0.1:8790',text)
        self.assertIn('qa_purchase_runtime:application_factory()',text)
        for forbidden in ('Environment','InaccessiblePaths','ReadWritePaths','ProtectHome','0.0.0.0'):
            self.assertNotIn(forbidden,text)

    def test_failure_retains_files_and_restores_only_old_executable(self):
        source=Path(deploy.__file__).read_text().split('except BaseException:')[1]
        self.assertIn("DROP.rename(TARGET/'failed-95-iap-sandbox.conf')",source)
        self.assertNotIn('unlink',source)
        self.assertNotIn('backup(',source)
        self.assertNotIn('players.sqlite',source)

    def test_check_mode_never_writes_or_runs_installer(self):
        with patch.object(deploy.sys,'argv',['install.py','a'*64,'--check']), \
                patch.object(deploy,'preflight',return_value=(None,{},b'',None,b'',{},{})) as preflight, \
                patch.object(deploy,'mkdir') as mkdir, patch.object(deploy,'write_new') as write, \
                patch.object(deploy,'run') as run, patch.object(deploy,'ctl') as ctl:
            deploy.main()
            preflight.assert_called_once_with('a'*64)
            mkdir.assert_not_called();write.assert_not_called();run.assert_not_called();ctl.assert_not_called()

    def test_preflight_contains_no_mutations(self):
        import ast
        source=Path(deploy.__file__).read_text()
        fn=next(n for n in ast.parse(source).body if isinstance(n,ast.FunctionDef) and n.name=='preflight')
        calls=[ast.unparse(n.func) for n in ast.walk(fn) if isinstance(n,ast.Call)]
        for forbidden in ('write_new','mkdir','run','os.chown','os.chmod','os.umask'):
            self.assertNotIn(forbidden,calls)
        for n in ast.walk(fn):
            if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='ctl':
                self.assertIn(n.args[0].value,('show','is-active'))


if __name__=='__main__':unittest.main()
