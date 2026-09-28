"""Disposable visual QA fixture. No real player records or store purchases."""
import json
import sys
import tempfile
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from store import Store
from server import create_server
with tempfile.TemporaryDirectory() as folder:
    store=Store(Path(folder)/'test.sqlite')
    player='11111111111111111111111111111111'
    store.register(player,'fixture-player-token-'*4)
    save=dict(PlayerId=player,SaveRevision=1,RecoveryEpoch=0,EconomyRevision=0,SchemaVersion=3,PlayerLevel=1,Gold=100,FreeGachaStones=900,PaidGachaStones=0,OwnedMonsters=[],OwnedEquipments=[])
    store.snapshot(player,save)
    store.snapshot(player,dict(save,SaveRevision=2,Gold=500))
    create_server(store,'local-ui-test-only',18788).serve_forever()
