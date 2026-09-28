import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from deploy import activate_apple_worker as a
from deploy import resume_apple_activation as resume


class ResumeTests(unittest.TestCase):
    def test_pinned_inputs_and_wrapper_match(self):
        folder=Path(__file__).resolve().parents[1]/'deploy'
        for name,digest in (('activate_apple_worker.py',resume.HELPER_HASH),
                            ('apple-activation-manifest.json',resume.MANIFEST_HASH),
                            ('apple_integrated_health_v2.py',resume.V2_HASH)):
            self.assertEqual(hashlib.sha256((folder/name).read_bytes()).hexdigest(),digest)
        wrapper=(folder/'run-apple-activation-resume-mac.sh').read_text()
        self.assertIn(hashlib.sha256((folder/'resume_apple_activation.py').read_bytes()).hexdigest(),wrapper)
        self.assertNotIn('NOPASSWD',wrapper)

    def test_success_path_waits_for_automatic_run_and_publishes_only_after_attachment(self):
        temp=tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        root=Path(temp.name).resolve(); new=root/'v2'; old=root/'old'
        hook=root/'hook'; calls=[]; enabled=False
        def control(*args,**kw):
            nonlocal enabled
            calls.append(args)
            if args[0]=='enable': enabled=True
            return 'enabled' if args[0]=='is-enabled' else ''
        def prop(unit,key):
            if key=='DropInPaths': return a.PREVIOUS_DROPS+(' '+str(hook) if hook.exists() else '')
            if key=='Result': return 'success'
            if key=='UnitFileState': return 'enabled' if enabled else 'disabled'
            if unit==a.TIMER: return 'active' if enabled else 'inactive'
            return 'active' if unit.endswith('health.timer') else 'inactive'
        manifest=json.loads((Path(resume.__file__).parent/'apple-activation-manifest.json').read_bytes())
        def checked(path,digest):
            if path.parent==Path(resume.__file__).parent: return path.read_bytes()
            return b'# retained test fixture\n'
        def trusted(path,*args):
            if path==a.MARKER: return json.dumps(dict(Healthy=True,CheckedUnix=1001)).encode()
            if str(path)=='/run/witch-player-health/status.json':
                return json.dumps(dict(Healthy=True,CheckedUnix=1003)).encode()
            return b'unchanged protected file'
        def marker(path,owner,since):
            if path==a.MARKER:
                return dict(Healthy=True,CheckedUnix=1001 if enabled else 1000,
                            Processed=dict(revoked=0,retry=0,stale=0,idle=1))
            return dict(Healthy=True,CheckedUnix=1002)
        with contextlib.ExitStack() as stack:
            for name,value in (('HEALTH',old),('HOOK',hook),('HOOK_TEXT',a.HOOK_TEXT),('MARKER',root/'marker')):
                stack.enter_context(patch.object(a,name,value))
            stack.enter_context(patch.dict('sys.modules',{'activate_apple_worker':a}))
            stack.enter_context(patch.object(resume,'NEW_HEALTH',new))
            stack.enter_context(patch.object(resume.os,'geteuid',return_value=0))
            stack.enter_context(patch.object(resume.sys,'argv',['resume.py']))
            stack.enter_context(patch.object(resume.pwd,'getpwnam',return_value=SimpleNamespace(pw_uid=999)))
            stack.enter_context(patch.object(resume.time,'time',return_value=1000))
            stack.enter_context(patch.object(resume.time,'sleep'))
            for name,function in (('checked',checked),('trusted',trusted),('ctl',control),
                                  ('prop',prop),('marker',marker)):
                stack.enter_context(patch.object(a,name,side_effect=function))
            stack.enter_context(patch.object(a,'external'))
            stack.enter_context(patch.object(a,'empty_runtime_probe'))
            run=stack.enter_context(patch.object(a,'run',return_value='{"Healthy":true,"Reasons":[{"Reason":"HEALTHY"}]}'))
            output=stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            resume.main()
        result=json.loads((new/'activation-result.json').read_bytes())
        self.assertEqual(result['Status'],'APPLE_WORKER_ACTIVATION_VERIFIED')
        self.assertTrue(result['AutomaticCombinedHealth'])
        self.assertEqual(calls.count(('start',a.SERVICE)),1)
        self.assertEqual(calls.count(('start','witch-player-health.service')),1)
        self.assertNotIn('.publish(',run.call_args.args[0][-1])
        self.assertIn('APPLE_RESUME_COLLECTOR=',output.getvalue())
