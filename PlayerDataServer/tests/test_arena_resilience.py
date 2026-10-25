import contextlib
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import arena_diagnostic_job_bridge as bridge
import production_arena_resilient_broker as broker


class ResilienceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.spool, self.archive = self.root/'jobs', self.root/'archive'
        self.spool.mkdir(mode=0o700); self.archive.mkdir(mode=0o700)
        self.template = {'Version':1,'Profile':{'PartyMonsterInstanceIds':['oracle-golem','oracle-whelp'],
            'OwnedMonsters':[{'InstanceId':'oracle-golem','Level':2},{'InstanceId':'oracle-whelp','Level':3}]}}
        self.canary = json.loads(json.dumps(self.template).replace('oracle-golem','arena-probe-'+'a'*32+'-0')
                                .replace('oracle-whelp','arena-probe-'+'a'*32+'-1'))

    @contextlib.contextmanager
    def ownership(self):
        lstat, fstat = Path.lstat, os.fstat
        class RootStat:
            def __init__(self, value): self.value = value
            @property
            def st_uid(self): return 0
            def __getattr__(self, name): return getattr(self.value,name)
        with patch.object(Path,'lstat',autospec=True,side_effect=lambda p:RootStat(lstat(p))), \
             patch.object(os,'fstat',side_effect=lambda fd:RootStat(fstat(fd))):
            yield

    def job(self, index, value):
        directory = self.spool/('witch-weekly-job-'+format(index,'032x'))
        directory.mkdir(mode=0o700)
        source = directory/'input.json'; source.write_bytes(broker.canonical(value));source.chmod(0o444)
        return directory

    def test_canary_requires_exact_template_and_consistent_nonce(self):
        self.assertTrue(broker.is_canary(self.canary,self.template))
        variants = [self.template,{},None,{'Version':True,'Profile':self.canary['Profile']}]
        for field in ('Level','Extra'):
            bad = copy.deepcopy(self.canary);bad['Profile']['OwnedMonsters'][0][field]=4;variants.append(bad)
        bad=copy.deepcopy(self.canary);bad['Profile']['PartyMonsterInstanceIds'][1]='arena-probe-'+'b'*32+'-1';variants.append(bad)
        for value in variants:self.assertFalse(broker.is_canary(value,self.template))

    def test_eight_synthetic_failures_archive_without_deletion_or_real_player_move(self):
        jobs=[self.job(i,self.canary) for i in range(8)]
        real=self.job(9,self.template)
        expected={p.name:hashlib.sha256((p/'input.json').read_bytes()).hexdigest() for p in jobs}
        with self.ownership(),contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(broker.archive_canaries(self.spool,self.template,self.archive),8)
        self.assertEqual(list(self.spool.iterdir()),[real])
        for p in jobs:
            self.assertFalse(p.exists())
            self.assertEqual(hashlib.sha256((self.archive/p.name/'input.json').read_bytes()).hexdigest(),expected[p.name])

    def test_symlink_and_hardlink_inputs_rejected_without_moving(self):
        for mutation in ('symlink','hardlink'):
            with self.subTest(mutation=mutation):
                job=self.job(1 if mutation=='symlink' else 2,self.canary)
                source=job/'input.json';other=self.root/(mutation+'.json')
                source.rename(other)
                if mutation=='symlink':source.symlink_to(other)
                else:os.link(other,source)
                with self.ownership(),self.assertRaises(ValueError):
                    broker.archive_canaries(self.spool,self.template,self.archive)
                self.assertTrue(job.is_dir());self.assertFalse(list(self.archive.iterdir()))
                source.unlink();job.rmdir()

    def test_archive_limit_and_duplicate_never_overwrite_evidence(self):
        job=self.job(1,self.canary)
        with self.ownership(),patch.object(broker,'MAX_ARCHIVES',0),self.assertRaises(ValueError):
            broker.archive_canaries(self.spool,self.template,self.archive)
        self.assertTrue(job.exists())
        (self.archive/job.name).mkdir()
        with self.ownership(),self.assertRaises(ValueError):
            broker.archive_canaries(self.spool,self.template,self.archive)
        self.assertTrue(job.exists())

    def test_telemetry_accepts_only_fixed_codes_and_no_account_or_log_strings(self):
        value=dict(Stage='NativeExit',NativeExit=2,Flags=['ManagedOutOfMemory'],
                   ManagedError='OutOfMemoryException',ElapsedMs=300,MemoryPeakBytes=123,OomKills=0)
        self.assertEqual(broker.diagnostic(value),value)
        for key, bad in [('Stage','SECRET_TOKEN'),('Flags',['SECRET_PROFILE']),
                         ('ManagedError','SECRET_PLAYER'),('ElapsedMs',-1),('NativeExit',10000)]:
            changed={**value,key:bad}
            with self.assertRaises(ValueError):broker.diagnostic(changed)
        with self.assertRaises(ValueError):broker.diagnostic({**value,'RawLog':'SECRET'})

    def test_native_log_classification_does_not_emit_raw_log(self):
        secret=b'PLAYER_SECRET TOKEN_SECRET WEEKLY_STANDALONE_JOB_FAILED OutOfMemoryException\nCould not allocate memory'
        flags,error=bridge.scan_log(secret)
        self.assertIn('ManagedOutOfMemory',flags);self.assertEqual(error,'OutOfMemoryException')
        self.assertNotIn('SECRET',json.dumps([flags,error]))

    def test_original_cpu_memory_isolation_and_timeout_limits_preserved(self):
        # The adapter targets a separately deployed immutable runtime. Keep its
        # verified properties as a fixture so this test needs no app/DB imports.
        fixture=json.loads((Path(__file__).parent/'fixtures/arena_profile192_properties.json').read_text())
        def properties(root,unit,source):
            return [value.format(root=root,unit=unit,source=source) for value in fixture['Properties']]
        values=properties(self.root,'witch-weekly-job-'+'a'*32,self.spool/'input.json')
        for value in ('CPUQuota=25%','RuntimeMaxSec=120','MemorySwapMax=0','DynamicUser=yes',
                      'PrivateNetwork=yes','NoNewPrivileges=yes','LimitCORE=0'):
            self.assertIn(value,values)
        self.assertIn('MemoryHigh=160M',values);self.assertIn('MemoryMax=192M',values)
        detail=dict(Stage='Complete',NativeExit=0,Flags=[],ManagedError=None,ElapsedMs=1,
                    MemoryPeakBytes=None,OomKills=None)
        context=dict(template=self.template,profile=SimpleNamespace(profile192_properties=properties),
            dispatch=SimpleNamespace(request_for=Mock()),delivery=SimpleNamespace(collect=Mock(
                return_value=dict(Version=1,Success=True,Diagnostic=detail,Result={'Party':{}}))))
        executor=SimpleNamespace(_resilience=context,_verify=Mock(),_capacity=Mock(),root=self.root,
            spool=self.spool,artifact_hash='a'*64,_cleanup_input=Mock())
        with patch.object(broker,'archive_canaries',return_value=0), \
             patch.object(broker.subprocess,'Popen') as popen,contextlib.redirect_stderr(io.StringIO()):
            broker.NativeProfile.__call__(executor,json.dumps(self.canary),'CalculateProfile')
        command=popen.call_args.args[0]
        actual=[command[i+1] for i,value in enumerate(command) if value=='--property']
        unit=next(x.split('=',1)[1] for x in command if x.startswith('--unit='))
        expected=properties(self.root,unit,self.spool/unit/'input.json')
        expected=[value+' '+str(broker.PREFIX/'arena_diagnostic_job_bridge.py')+':/weekly-job-bridge.py'
                  if value.startswith('BindReadOnlyPaths=') else value for value in expected]
        self.assertEqual(actual,expected)

    def test_failed_native_is_not_retried_successful_result_unchanged(self):
        for success in (False,True):
            with self.subTest(success=success):
                detail=dict(Stage='Complete' if success else 'NativeExit',NativeExit=0 if success else 2,
                    Flags=[],ManagedError=None,ElapsedMs=10,MemoryPeakBytes=None,OomKills=None)
                result={'Party':{'Units':[]}} if success else None
                profile=SimpleNamespace(profile192_properties=lambda *a: [])
                delivery=SimpleNamespace(collect=Mock(return_value=dict(Version=1,Success=success,Diagnostic=detail,Result=result)))
                executor=SimpleNamespace(_resilience=dict(template=self.template,profile=profile,
                    dispatch=SimpleNamespace(request_for=Mock()),delivery=delivery),
                    _verify=Mock(),_capacity=Mock(),root=self.root,spool=self.spool,
                    artifact_hash='a'*64,_cleanup_input=Mock())
                with patch.object(broker,'archive_canaries',return_value=0),patch.object(broker.subprocess,'Popen') as popen, \
                     patch.object(broker.subprocess,'run') as stop,contextlib.redirect_stderr(io.StringIO()):
                    if success:self.assertEqual(broker.NativeProfile.__call__(executor,json.dumps(self.canary),'CalculateProfile'),result)
                    else:
                        with self.assertRaises(ValueError):broker.NativeProfile.__call__(executor,json.dumps(self.canary),'CalculateProfile')
                        executor._cleanup_input.assert_not_called();stop.assert_called_once()
                    popen.assert_called_once();delivery.collect.assert_called_once()
                for directory in list(self.spool.iterdir()):
                    for f in directory.iterdir():f.unlink()
                    directory.rmdir()


if __name__ == '__main__':unittest.main()
