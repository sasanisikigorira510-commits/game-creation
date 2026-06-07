# 近接モンスターの接敵待機対策メモ

## 発生した症状

- 聖鎧剣士レオンを後衛枠に置いた時、敵へ近づいたあとに立ち止まり、待機状態を挟んでから攻撃モーションへ移る時間が長かった。
- 2026-05-28 の実機プローブでは、レオンは `x=0.49` で停止し、攻撃モーションへ入るまで約 3.2 秒の待機が発生していた。
- 攻撃クールダウンはほぼ溜まっていたため、「構えてから斬るのが遅い」問題ではなく、接敵位置の計算が原因だった。

## 原因

後衛配置された responsive melee 系モンスターは、`BattleSimulator.ResolveAllyHomeAnchor` で実行時ホーム座標を前へ寄せる。

- 後衛枠の基本ホーム: `x=0.07`
- レオンなどの実行時ホーム: `x=0.24`
- 旧クランプ上限: `0.07 + 0.42 = 0.49`
- 本来の上限: `0.24 + 0.42 = 0.66`

移動先を制限する `BattleFormationLayout.ClampAllyCombatAnchor` が基本ホームを再解決していたため、実行時ホームより手前の `x=0.49` で止まり、敵が近づくかターゲットが変わるまで待機していた。

## 今回の修正ルール

- 戦闘中にユニットのホーム座標を補正した場合、その補正後の `HomeAnchor` を移動クランプにも渡す。
- `BattleSimulator` からは `BattleFormationLayout.ClampAllyCombatAnchor(i, ally.Data, targetAnchor, ally.HomeAnchor)` を使う。
- 引数なしの `ClampAllyCombatAnchor(... desiredAnchor)` は、基本陣形座標だけで処理する呼び出し向けに残す。

## 修正後確認

2026-05-28 の修正後プローブでは、後衛枠のレオンは旧停止位置 `x=0.49` を越え、`t=0.999` に `x=0.544` 付近で close combat が成立した。

- 修正前レポート: `tools/reports/leon_motion_probe_20260528_224129.json`
- 修正後レポート: `tools/reports/leon_motion_probe_20260528_225204.json`
- 修正後の `idleToAttackWindows` は空。接敵後に待機してから攻撃へ入る長いウィンドウは再現しなかった。
- 後半の idle はレオン撃破後の状態であり、今回の接敵待機とは別。

## 既存モンスター棚卸し

`Assets/MasterData/Monster` の実装済みモンスター 43 体を確認し、近接は 24 体。

今回と同じ座標不一致の影響を受け得る responsive melee は 11 体。今回の `HomeAnchor` 連携修正で一括カバーされる。

- `monster_apprentice_swordsman`
- `monster_holy_armor_leon`
- `monster_sword_saint_alvarez`
- `monster_rock_golem`
- `monster_ore_giant_garm`
- `monster_cosmic_ore_fortress_golem`
- `monster_dragon_sword_saint_agito`
- `monster_mecha_sword_saint_gransaber`
- `monster_magic_sword_saint_luciel`
- `monster_drag_gaia`
- `monster_rock_knight_gaius`

近接だが responsive melee ではない 13 体も確認済み。これらは実行時ホーム補正とクランプ基準のずれがないため、今回と同じ原因では停止しない。

- `monster_armed_droid`
- `monster_chibi_gear`
- `monster_dragoon`
- `monster_fortress_machine_gigafort`
- `monster_goblin`
- `monster_hell_knight`
- `monster_naga`
- `monster_omega_leon`
- `monster_shadow`
- `monster_soul_eater`
- `monster_spectral_warrior`
- `monster_vault_guard`
- `monster_worm`

## 今後追加する時のチェック

- 前衛向けの近接モンスターでも、プレイヤーが後衛に配置する前提で見る。
- `raceId` を `swordsman` / `golem` にする、または `ResponsiveMeleeLineageMonsterIds` に追加する場合は、後衛枠 3 / 4 / 5 で接敵確認する。
- 実行時ホーム、探索範囲、攻撃射程、移動クランプは同じ座標基準で計算する。ホームだけ補正してクランプが基本陣形を見ている状態を作らない。
- 接敵後に `moving=false`、攻撃演出なし、close combat 未成立の待機が 0.5 秒以上続く場合は、攻撃速度ではなく接敵座標を先に疑う。
- 攻撃モーションの見た目調整と、接敵してから攻撃へ入るまでのロジック調整を分けて確認する。

## 2026-05-29 追加: 遠距離・特殊モンスターの攻撃待機

アストラルゴーレムで、`raceId: special` / `rangeType: Ranged` のため close combat 判定に入らず、射程内で停止しても通常攻撃ヒット時以外は idle に戻って見える問題を確認した。

根本対応として、表示側の攻撃ポーズ遷移は近接分類ではなく `BattleSimulator` の攻撃可能判定に寄せる。

- 味方は `IsAllyAttackEngaged(index)` で、現在ターゲットを攻撃できるかを見る。
- 敵は `IsEnemyAttackEngaged(index)` で、現在ターゲットを攻撃できるかを見る。
- `IsAllyCloseCombatEngaged` / `IsEnemyCloseCombatEngaged` は近接接触の意味として残すが、攻撃ポーズ選択の主条件にしない。
- 新規モンスター確認では、近接/遠距離/特殊の分類に関係なく「射程内で停止したら attack フレームへ入る」ことを見る。

## 2026-05-30 追加: ギガフォート後衛配置

ギガフォートは `raceId: special` / `rangeType: Melee` / `attackRange: 1.18` だが、旧ロジックでは swordsman/golem などの responsive melee だけが後衛ホーム補正対象だった。
このため後衛枠では基本ホーム `x=0.07` からのクランプ上限 `x=0.49` が残り、巨体の近接ユニットなのに十分前へ出ず、接敵していないように見えた。

根本対応として、後衛ホーム補正は系統ホワイトリストではなく実戦闘分類で決める。

- `rangeType == Melee`
- `attackRange < RangedAttackThreshold`
- 後衛枠 `index >= 2`

この条件を満たす味方は全員、実行時ホームを `index == 2 ? x=0.30 : x=0.24` まで前に寄せ、その補正後 `HomeAnchor` を移動クランプにも渡す。
個別モンスターIDの追加ではなく、「後衛に置ける近接は後衛からでも接敵できる」というルールに寄せる。

## 2026-05-30 Unity 実測: Astral / 遠距離 / 近接比較

Unity 6000.3.11f1 の Play Mode で、後衛スロット 4 と 5 に対象モンスターを置いて `/battle-debug` を 0.2 秒間隔で取得した。

確認した観測項目:

- `attackEngaged`: シミュレータ上で現在ターゲットを攻撃可能か
- `previewPose`: 表示側が選んだ姿勢
- `previewSprite`: 実際に Image に入っているスプライト名
- `attackTimer` のリセット: 実攻撃が発生したか

結果:

- Astral Golem は後衛スロット 4 で `2.702s`、後衛スロット 5 で `2.706s` に `attackEngaged=true`、`previewPose=Attack`、`mon_astral_eclipse_golem_attack_*`、`attackTimer` reset が同時に成立した。
- 比較対象の Seraphis / Merchion / Leon も同様に、`attackEngaged=true` なのに `previewPose=Idle` になるサンプルは 0 件。
- Astral の攻撃フレームは 4 枚ロードされており、idle フレームとは別ハッシュの実画像だった。

レポート:

- `tools/reports/battle_attack_transition_probe_20260530_090427.json`
- `tools/reports/battle_attack_transition_slot4_probe_20260530_090518.json`

この結果から、現在の修正後コードでは「特殊・遠距離だから攻撃表示へ入らない」状態は再現しない。再発時は、まず `attackEngaged=true` と `previewPose=Attack` が一致しているかを確認し、一致しているのに待機に見える場合は攻撃スプライトの欠落/類似を疑う。
