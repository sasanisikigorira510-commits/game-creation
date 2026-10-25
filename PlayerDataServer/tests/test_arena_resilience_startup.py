import importlib.util
from pathlib import Path
import stat
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

source=Path(__file__).resolve().parents[1]/'deploy/install_arena_resilience_20261025.py'
spec=importlib.util.spec_from_file_location('arena_resilience_install_test',source)
install=importlib.util.module_from_spec(spec);spec.loader.exec_module(install)


class StartupTests(unittest.TestCase):
    def test_active_process_is_not_treated_as_socket_readiness(self):
        ready=SimpleNamespace(st_mode=stat.S_IFSOCK|0o660,st_uid=0,st_gid=988)
        with patch.object(install,'state',return_value={'ActiveState':'active'}), \
             patch.object(Path,'lstat',side_effect=[FileNotFoundError(),FileNotFoundError(),ready]) as read, \
             patch.object(install.time,'sleep') as sleep:
            install.broker_ready()
        self.assertEqual(read.call_count,3)
        self.assertEqual(sleep.call_count,2)

    def test_publisher_checks_follow_socket_readiness_and_restored_timers(self):
        events=[]
        with patch.object(install,'ctl',side_effect=lambda verb,unit:events.append((verb,unit))), \
             patch.object(install,'broker_ready',side_effect=lambda:events.append(('socket','ready'))), \
             patch.object(install,'wait_quiet',side_effect=lambda:events.append(('checks','quiet'))), \
             patch.object(install,'observations',return_value={'Healthy':True}):
            self.assertEqual(install.refresh('a'*64),{'Healthy':True})
        for timer in install.TIMERS:
            self.assertLess(events.index(('start',timer)),events.index(('start',install.CHECKS[0])))
        self.assertLess(events.index(('socket','ready')),events.index(('start',install.CHECKS[0])))
        self.assertLess(events.index(('checks','quiet')),events.index(('start',install.CHECKS[2])))


if __name__=='__main__':unittest.main()
