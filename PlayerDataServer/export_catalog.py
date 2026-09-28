"""Export exactly the monsters referenced by Unity's runtime master root."""
import json
import re
from pathlib import Path


def catalog():
    assets = Path(__file__).resolve().parents[1] / 'WitchTowerGame/Assets'
    root = (assets / 'Resources/MasterData/MasterDataRoot.asset').read_text()
    section = root.split('  monsterDataList:\n', 1)[1].split('\n  skillDataList:', 1)[0]
    guids = re.findall(r'guid: ([0-9a-f]{32})', section)
    paths = {}
    for meta in (assets / 'MasterData/Monster').glob('*.asset.meta'):
        guid = re.search(r'^guid: (.+)$', meta.read_text(), re.M).group(1)
        paths[guid] = Path(str(meta)[:-5])
    rows = []
    for guid in guids:
        text = paths[guid].read_text()
        def field(name): return re.search(r'^  '+name+r': (.*)$',text,re.M).group(1)
        rows.append(dict(monsterId=field('monsterId'),classRank=int(field('classRank')),fusionExclusive=field('fusionExclusive')=='1'))
    if len(rows) != len({x['monsterId'] for x in rows}): raise ValueError('Duplicate monster IDs')
    return rows


if __name__ == '__main__':
    Path(__file__).with_name('catalog.json').write_text(json.dumps(catalog(), indent=2)+'\n')
