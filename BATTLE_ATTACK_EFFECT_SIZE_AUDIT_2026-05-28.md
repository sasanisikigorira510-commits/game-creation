# 攻撃エフェクトサイズ監査メモ

## 発生した症状

- 現在編成で中位魔獣の廃工廠 5階層に入ると、戦闘途中から攻撃エフェクトが急に大きく見えた。
- 修正前プローブでは、`fx_omega_leon_attack_2` が最大 `433.108px` まで拡大していた。
- 原因は味方オメガレオンではなく、当時のステージ敵 `monster_armed_droid` が同じ `fx_omega_leon_attack` 素材を使い、class2 の追加フラリッシュ倍率が重なっていたこと。

## 修正方針

- class2 の追加エフェクトは主エフェクトを超えない残光扱いにする。
- class2 全体の攻撃エフェクト倍率を控えめにし、echo / spark の倍率と pulse を下げる。
- すべての攻撃エフェクトに最大長辺 `260px` の安全上限を置く。
- 古い `monster_armed_droid` は現行ステージの敵から外し、中位魔獣の廃工廠は現行の中位モンスター `monster_ore_giant_garm` で組み直す。

## 確認結果

- 静的監査では、`MonsterAttackEffects` に定義された全モンスターの見積もり最大サイズが `260px` 以下。
- 修正後の中位魔獣の廃工廠 5階層プローブでは、最大エフェクトは味方 `monster_omega_leon` 由来の `233.811px`。
- 修正後のステージ敵 `monster_ore_giant_garm` 由来の `fx_cosmic_ore_fortress_golem_attack` は最大 `189.089px`。

## レポート

- 修正前: `tools/reports/gear_crypt_floor5_effect_probe_20260528_231046.json`
- 修正後: `tools/reports/gear_crypt_floor5_effect_probe_20260528_232112.json`

## 今後の追加チェック

- 新規モンスターを `MonsterAttackEffects` に追加したら、`Scale * class multiplier * AttackEffectGlobalScale` と追加演出倍率を確認する。
- class2 の echo / spark は主エフェクトより小さく見える値にする。
- 既存の上位モンスター専用素材を下位・中位敵に流用する場合、敵として大量に出現した時の重なりを必ず確認する。
- ダンジョンの捕獲対象は、旧プレースホルダーではなく現行のマスター・スプライト・エフェクトが揃っているモンスターを使う。
