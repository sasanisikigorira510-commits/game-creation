# Monster System Foundation

## Purpose
- `MonsterDataSO` を中心に、図鑑・所持・編成・合体の土台を先に固める
- 今後 `FormationScene`、`MonsterBookScene`、`FusionScene` を作る時に共通のデータ源を使えるようにする

## Monster Master Data
- `monsterId`
- `monsterName`
- `encyclopediaNumber`
- `rarity`
  - `Iron / Bronze / Silver / Gold / Emerald / Diamond`
- `element`
  - `Wood / Water / Fire / Light / Dark`
- `rangeType`
  - `Melee / Ranged`
- `damageType`
  - `Physical / Magic`
- `baseStats`
  - `HP`
  - `攻撃力`
  - `魔法攻撃力`
  - `防御力`
  - `魔法防御力`
  - `攻撃速度`
- `plusValueCap`
- `plusGrowth`
  - `+値` ごとの成長量
- `fusionExclusive`
- `fusionRecipes`
  - 特定組み合わせでのみ誕生するモンスター用
- `portraitSprite`
- `illustrationSprite`
- `description`

## Element Rules
- `木 -> 水 -> 火 -> 木`
- `光 <-> 闇`
- 有利属性は `1.5倍`
- 実装土台:
  - `MonsterElementUtility.GetDamageMultiplier(attacker, defender)`

## Save Data
- `MonsterStorageLimit`
  - 初期値 `100`
- `OwnedMonsters`
  - 同種重複あり
  - 個体IDベース管理
- `MonsterDexEntries`
  - 図鑑の開放状況
- `PartyMonsterInstanceIds`
  - 戦闘へ連れていく `1〜5体`

## Owned Monster Data
- `InstanceId`
- `MonsterId`
- `Level`
- `Exp`
- `PlusValue`
- `IsFavorite`
- `AcquiredOrder`

## Fusion Data
- 方式:
  - `result monster` 側に `fusionRecipes` を持たせる
- 例:
  - `A + B => C`
  - `ignoreOrder = true` なら `B + A` でも成立
- 実装土台:
  - `MasterDataManager.GetFusionResult(firstMonsterId, secondMonsterId)`

## Image Plan
- 画像の持ち方:
  - `portraitSprite`
  - `illustrationSprite`
- 推奨配置:
  - `Assets/Art/Monsters/Portraits`
  - `Assets/Art/Monsters/Illustrations`
- 推奨命名:
  - `mon_<monsterId>_portrait`
  - `mon_<monsterId>_full`

## Immediate Next Work
1. 最初の `10〜20体` のモンスターマスターを決める
2. 開始時に仲間化済みで持っている初期モンスターを決める
3. `FormationScene` を仮データではなく `OwnedMonsters` 参照に切り替える
4. `MonsterBookScene` を作って図鑑表示を始める
5. `FusionScene` を作って組み合わせ確認と結果表示を始める
