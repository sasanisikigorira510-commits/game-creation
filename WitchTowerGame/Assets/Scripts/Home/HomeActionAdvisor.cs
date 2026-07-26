using System;
using WitchTower.Battle;
using WitchTower.Data;

namespace WitchTower.Home
{
    public static class HomeActionAdvisor
    {
        private static readonly (string missionId, int targetValue)[] MissionChecks =
        {
            ("mission_clear_1", 1),
            ("mission_reach_floor_3", 3)
        };

        public static int GetEnhanceBadgeCount(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = 0;
            count += profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel) ? 1 : 0;
            count += profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel) ? 1 : 0;
            count += profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel) ? 1 : 0;
            return count;
        }

        public static int GetEquipmentBadgeCount(PlayerProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = 0;
            count += IsOwnedButNotEquipped(profile, "equip_iron_sword", profile.EquippedWeaponId) ? 1 : 0;
            count += IsOwnedButNotEquipped(profile, "equip_bone_mail", profile.EquippedArmorId) ? 1 : 0;
            count += IsOwnedButNotEquipped(profile, "equip_quick_charm", profile.EquippedAccessoryId) ? 1 : 0;
            return count;
        }

        public static int GetMissionBadgeCount(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = 0;
            count += DailyRewardService.GetClaimableQuestCount(profile, now);
            count += IsMissionClaimable(profile, "mission_clear_1", 1) ? 1 : 0;
            count += IsMissionClaimable(profile, "mission_reach_floor_3", 3) ? 1 : 0;
            return count;
        }

        public static int GetHomeBadgeCount(PlayerProfile profile)
        {
            return 0;
        }

        public static string BuildHomeHeadline(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "次の一手: データを読み込んで探索を再開します。";
            }

            return string.Format(
                "次の一手: バトルで{0}階に挑戦しましょう。",
                profile.HighestFloor + 1);
        }

        public static string BuildRunProgressText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "進行状況: データがありません。";
            }

            return string.Format(
                "進行状況: {0}階まで踏破、次は{1}階です。",
                profile.HighestFloor,
                profile.HighestFloor + 1);
        }

        public static string BuildRunAlertText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "行動メモ: データを確認できません。";
            }

            int missionClaims = GetMissionBadgeCount(profile, now);
            if (missionClaims > 0)
            {
                return string.Format("行動メモ: 受け取れる報酬が{0}件あります。まずミッションを確認しましょう。", missionClaims);
            }

            int affordableUpgrades = GetEnhanceBadgeCount(profile, baseUpgradeCost);
            if (affordableUpgrades > 0)
            {
                return string.Format("行動メモ: 今すぐ強化できる項目が{0}件あります。", affordableUpgrades);
            }

            int equipmentChoices = GetEquipmentBadgeCount(profile);
            if (equipmentChoices > 0)
            {
                return string.Format("行動メモ: 入れ替え候補の装備が{0}件あります。", equipmentChoices);
            }

            string threat = TrimThreat(GetNextFloorThreat(profile));
            if (threat.Contains("dangerous"))
            {
                return $"行動メモ: {profile.HighestFloor + 1}階は危険です。育成と装備確認を優先しましょう。";
            }

            if (threat.Contains("even"))
            {
                return $"行動メモ: {profile.HighestFloor + 1}階は五分です。ひとつ準備してから挑むと安心です。";
            }

            return "行動メモ: 準備は安定しています。次のバトルへ進めます。";
        }

        public static string BuildRewardForecastText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "報酬予測: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleRewardResult reward = BattleRewardCalculator.Calculate(nextFloor, profile.HighestFloor);
            return string.Format(
                "報酬予測: {0}階クリアで約{1}ゴールド / 経験値{2}。",
                nextFloor,
                reward.Gold,
                reward.Exp);
        }

        public static string BuildThreatReadText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "敵の強さ: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleEncounterAdvisor.BattleEncounterAssessment assessment = BattleEncounterAdvisor.AssessFloor(profile, nextFloor);
            string threat = TrimThreat(assessment.ThreatText);
            return $"敵の強さ: {nextFloor}階は{DescribeThreatWithNoun(threat)}です（推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上）。";
        }

        public static string BuildConfidenceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "勝算: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleEncounterAdvisor.BattleEncounterAssessment assessment = BattleEncounterAdvisor.AssessFloor(profile, nextFloor);
            string threat = TrimThreat(assessment.ThreatText);
            string powerRead = BuildPartyPowerRead(assessment);
            string equipmentRead = assessment.NeedsEquipment ? "装備更新と" : string.Empty;

            if (threat.Contains("dangerous"))
            {
                return $"勝算: {nextFloor}階は低めです。{powerRead}推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上なので、{equipmentRead}育成後に挑みましょう。";
            }

            if (threat.Contains("even"))
            {
                return $"勝算: {nextFloor}階は五分寄りです。{powerRead}推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上を目安に、装備を確認すると安定します。";
            }

            return $"勝算: {nextFloor}階は高めです。{powerRead}推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上を満たしていれば挑戦できます。";
        }

        public static string BuildFloorRiskSummary(PlayerProfile profile, int globalFloor)
        {
            return string.Empty;
        }

        public static string BuildHomeGuideReadinessText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "よぉし！\n今日の冒険を始めましょう。";
            }

            int nextFloor = Math.Max(1, profile.HighestFloor + 1);
            string floorLabel = BuildFloorLabel(nextFloor);
            BattleEncounterAdvisor.BattleEncounterAssessment assessment = BattleEncounterAdvisor.AssessFloor(profile, nextFloor);
            string threat = TrimThreat(assessment.ThreatText);
            if (threat.Contains("dangerous"))
            {
                return $"まだ準備が要りそうです。\n{floorLabel}は推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上。装備と育成を整えましょう。";
            }

            if (threat.Contains("even"))
            {
                return $"挑戦圏内ですが慎重に！\n{floorLabel}は推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上。装備確認後が安心です。";
            }

            return $"準備はいい感じ！\n{floorLabel}へ挑戦して、報酬を狙いましょう。";
        }

        public static string BuildLoadoutAlertText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "装備メモ: 確認できません。";
            }

            System.Collections.Generic.List<string> upgrades = new System.Collections.Generic.List<string>();

            if (profile.HasEquipment("equip_iron_sword") && profile.EquippedWeaponId != "equip_iron_sword")
            {
                upgrades.Add("鉄の剣");
            }

            if (profile.HasEquipment("equip_bone_mail") && profile.EquippedArmorId != "equip_bone_mail")
            {
                upgrades.Add("骨の鎧");
            }

            if (profile.HasEquipment("equip_quick_charm") && profile.EquippedAccessoryId != "equip_quick_charm")
            {
                upgrades.Add("俊足のお守り");
            }

            if (upgrades.Count == 0)
            {
                return "装備メモ: 現在の装備は、解放済みの中では整っています。";
            }

            if (upgrades.Count == 1)
            {
                return $"装備メモ: {upgrades[0]}に入れ替えると戦力が上がります。";
            }

            if (upgrades.Count == 2)
            {
                return $"装備メモ: {upgrades[0]}と{upgrades[1]}を見直すと一気に強くなります。";
            }

            return $"装備メモ: {upgrades[0]}、{upgrades[1]}、{upgrades[2]}をまとめて更新できます。";
        }

        public static string BuildGoldRouteText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "ゴールド導線: 確認できません。";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);
            int cheapestUpgrade = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            string threat = TrimThreat(GetNextFloorThreat(profile));

            if (readyGold > 0)
            {
                return $"ゴールド導線: まず報酬の{readyGold}ゴールドを受け取り、強化に回しましょう。";
            }

            if (profile.Gold >= cheapestUpgrade)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"ゴールド導線: {profile.HighestFloor + 1}階の前に、防御かHPへ使うのが安全です。";
                }

                return "ゴールド導線: 次の強化費用は足りています。強化タブを確認しましょう。";
            }

            int missingGold = Math.Max(0, cheapestUpgrade - profile.Gold);
            return $"ゴールド導線: あと{missingGold}ゴールドで次の強化ができます。バトルで稼ぎましょう。";
        }

        public static string BuildUpgradeRouteText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "強化導線: 確認できません。";
            }

            string threat = TrimThreat(GetNextFloorThreat(profile));
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);

            if (threat.Contains("dangerous"))
            {
                if (profile.Gold >= defenseCost)
                {
                    return $"強化導線: {profile.HighestFloor + 1}階の前に防御を{defenseCost}ゴールドで上げましょう。";
                }

                if (profile.Gold >= hpCost)
                {
                    return $"強化導線: {profile.HighestFloor + 1}階の前にHPを{hpCost}ゴールドで上げましょう。";
                }

                int missing = Math.Min(Math.Max(0, defenseCost - profile.Gold), Math.Max(0, hpCost - profile.Gold));
                return $"強化導線: あと{missing}ゴールドで耐久強化に届きます。";
            }

            if (threat.Contains("even"))
            {
                if (profile.Gold >= attackCost)
                {
                    return $"強化導線: 攻撃を{attackCost}ゴールドで上げると{profile.HighestFloor + 1}階を押しやすくなります。";
                }

                int missing = Math.Max(0, attackCost - profile.Gold);
                return $"強化導線: あと{missing}ゴールドで攻撃強化に届きます。";
            }

            if (profile.Gold >= attackCost)
            {
                return $"強化導線: 攻撃を{attackCost}ゴールドで上げると周回が速くなります。";
            }

            int cheapest = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            return $"強化導線: あと{Math.Max(0, cheapest - profile.Gold)}ゴールドで次の強化ができます。";
        }

        public static string BuildRewardRouteText(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "報酬導線: 確認できません。";
            }

            bool dailyReady = DailyRewardService.HasClaimableQuest(profile, now);
            bool missionClearReady = IsMissionClaimable(profile, "mission_clear_1", 1);
            bool missionFloorReady = IsMissionClaimable(profile, "mission_reach_floor_3", 3);

            if (dailyReady)
            {
                return $"報酬導線: デイリー達成分から無償石{DailyRewardService.GetClaimableRewardFreeGachaStones(profile, now)}個を受け取りましょう。";
            }

            if (missionFloorReady)
            {
                MissionDefinition? definition = MissionService.GetDefinition("mission_reach_floor_3");
                int gold = definition.HasValue ? definition.Value.RewardGold : 0;
                return $"報酬導線: 階層ミッション報酬の{gold}ゴールドを受け取りましょう。";
            }

            if (missionClearReady)
            {
                MissionDefinition? definition = MissionService.GetDefinition("mission_clear_1");
                int gold = definition.HasValue ? definition.Value.RewardGold : 0;
                return $"報酬導線: 初勝利ミッション報酬の{gold}ゴールドを受け取りましょう。";
            }

            return "報酬導線: 受け取り待ちはありません。次の階で新しい報酬を狙いましょう。";
        }

        public static string BuildPushWindowText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "挑戦判断: 確認できません。";
            }

            string threat = TrimThreat(GetNextFloorThreat(profile));
            int readyGold = GetClaimableRewardGold(profile, now);
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(profile.HighestFloor + 1),
                PlayerBattleStatsFactory.CreatePreview(profile));

            if (readyGold > 0)
            {
                return $"挑戦判断: 先に{readyGold}ゴールドを受け取り、強化後に再確認しましょう。";
            }

            if (threat.Contains("dangerous"))
            {
                if (best.Label != null)
                {
                    return $"挑戦判断: 先に{best.Label}を行うと安全に入れます。";
                }

                return $"挑戦判断: {profile.HighestFloor + 1}階はまだ危険です。少し準備を足しましょう。";
            }

            if (threat.Contains("even"))
            {
                if (best.Label != null)
                {
                    return $"挑戦判断: {best.Label}を挟むと{profile.HighestFloor + 1}階が楽になります。";
                }

                return $"挑戦判断: 挑戦は可能です。余裕があれば強化してから進みましょう。";
            }

            return $"挑戦判断: {profile.HighestFloor + 1}階は今すぐ挑戦できます。";
        }

        public static string BuildRoiReadText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "報酬効率: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleRewardResult reward = BattleRewardCalculator.Calculate(nextFloor, profile.HighestFloor);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);
            int cheapestUpgrade = Math.Min(attackCost, Math.Min(defenseCost, hpCost));

            if (reward.Gold >= cheapestUpgrade)
            {
                return $"報酬効率: 1回クリアすれば{reward.Gold}ゴールドで次の強化に届きます。";
            }

            int shortfall = cheapestUpgrade - reward.Gold;
            if (reward.Gold * 2 >= cheapestUpgrade)
            {
                return $"報酬効率: 1回でほぼ強化費用に届きます。不足は{shortfall}ゴールドです。";
            }

            return $"報酬効率: この階は{reward.Gold}ゴールド。次の強化まであと{shortfall}ゴールドです。";
        }

        public static string BuildDecisionLineText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "判断ライン: 確認できません。";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(profile.HighestFloor + 1),
                PlayerBattleStatsFactory.CreatePreview(profile));
            string threat = TrimThreat(GetNextFloorThreat(profile));

            if (readyGold > 0)
            {
                return "判断ライン: まず報酬を受け取り、強化できるか見直しましょう。";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"判断ライン: {best.Label}を済ませてから挑戦しましょう。"
                    : $"判断ライン: {profile.HighestFloor + 1}階は準備を増やしてから挑みましょう。";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? "判断ライン: ひとつ準備してから挑戦するのが安定です。"
                    : "判断ライン: 今でも挑めますが、もう一段強化すると楽です。";
            }

            return "判断ライン: 受け取り待ちがなければ、そのままバトルへ進みましょう。";
        }

        public static string BuildDecisionBadgeText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "判断: 不明";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            string threat = TrimThreat(GetNextFloorThreat(profile));

            if (readyGold > 0)
            {
                return "判断: 報酬受取";
            }

            if (threat.Contains("dangerous"))
            {
                return "判断: 準備";
            }

            if (threat.Contains("even"))
            {
                return "判断: 調整";
            }

            return "判断: 挑戦";
        }

        public static string BuildCommandStackText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "行動順: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            int readyGold = GetClaimableRewardGold(profile, now);
            int missionClaims = GetClaimableMissionCount(profile, now);
            string threat = TrimThreat(GetNextFloorThreat(profile));
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(nextFloor),
                PlayerBattleStatsFactory.CreatePreview(profile));

            string stepOne;
            if (readyGold > 0)
            {
                stepOne = $"1. 報酬{missionClaims}件を受け取り、{readyGold}ゴールドを確保";
            }
            else if (best.Label != null && (threat.Contains("dangerous") || threat.Contains("even")))
            {
                stepOne = $"1. {best.Label}";
            }
            else
            {
                stepOne = $"1. {nextFloor}階を開く";
            }

            string stepTwo;
            if (best.Label != null)
            {
                stepTwo = $"2. 上昇値を確認（{best.PlanDetail}）";
            }
            else if (threat.Contains("dangerous"))
            {
                stepTwo = "2. ゴールドを貯めて安全を作る";
            }
            else
            {
                stepTwo = "2. 現在の編成を維持";
            }

            string stepThree;
            if (threat.Contains("dangerous"))
            {
                stepThree = $"3. {nextFloor}階の危険度を再確認";
            }
            else if (threat.Contains("even"))
            {
                stepThree = $"3. 準備後に{nextFloor}階へ挑戦";
            }
            else
            {
                stepThree = $"3. 今すぐ{nextFloor}階へ挑戦";
            }

            return $"行動順: {stepOne} / {stepTwo} / {stepThree}";
        }

        public static string BuildMomentumReadText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "流れ: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            int readyGold = GetClaimableRewardGold(profile, now);
            string threat = TrimThreat(GetNextFloorThreat(profile));
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(nextFloor),
                PlayerBattleStatsFactory.CreatePreview(profile));

            if (readyGold > 0)
            {
                return $"流れ: {nextFloor}階の前に{readyGold}ゴールドを受け取れます。";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"流れ: {nextFloor}階の前に{best.Label}で安定させましょう。"
                    : $"流れ: {nextFloor}階は足止め気味です。育成を挟みましょう。";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? $"流れ: {best.Label}で{nextFloor}階を有利にできます。"
                    : $"流れ: {nextFloor}階は五分です。少し強化すると安定します。";
            }

            return $"流れ: 現在の編成で{nextFloor}階に進めます。";
        }

        public static string BuildRunCallText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "探索判断: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            int readyGold = GetClaimableRewardGold(profile, now);
            string threat = TrimThreat(GetNextFloorThreat(profile));
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(nextFloor),
                PlayerBattleStatsFactory.CreatePreview(profile));

            if (readyGold > 0)
            {
                return $"探索判断: 先に報酬を受け取り、{nextFloor}階へ進む準備を整えましょう。";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"探索判断: {best.Label}をしてから挑戦しましょう。"
                    : $"探索判断: {nextFloor}階はまだ無理押ししない方が安全です。";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? $"探索判断: {best.Label}を済ませたら挑戦しましょう。"
                    : "探索判断: 小さく整えてから進みましょう。";
            }

            return $"探索判断: {nextFloor}階へ進んで大丈夫です。";
        }

        public static string BuildRiskBufferText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "耐久余裕: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int hpMargin = playerStats.MaxHp - enemyStats.Attack * 3;
            int defenseMargin = playerStats.Defense - enemyStats.Defense;
            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);

            if (hpMargin <= 0)
            {
                return $"耐久余裕: 薄めです。{nextFloor}階では敵の3発以内に崩れる可能性があります。";
            }

            if (hpMargin <= 20)
            {
                return $"耐久余裕: 小さめです。3発ラインから{hpMargin}HPだけ上回っています。";
            }

            if (enemyDamage == 1 && enemyHitsToBreak >= 20)
            {
                return $"耐久余裕: 十分です。{nextFloor}階の被ダメージは今のところ1ずつです。";
            }

            if (defenseMargin >= 5)
            {
                return $"耐久余裕: 安定しています。{nextFloor}階に対して{hpMargin}HP分の余裕があります。";
            }

            return $"耐久余裕: なんとか戦えます。{nextFloor}階に対して約{hpMargin}HPの余裕です。";
        }

        public static string BuildEnemyTempoText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "敵テンポ: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            float swingRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float swingSeconds = 1f / swingRate;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "敵テンポ: {0}階は約{1:0.00}秒ごとに攻撃、圧力は攻撃{2}です。",
                nextFloor,
                swingSeconds,
                enemyStats.Attack);
        }

        public static string BuildDamageRaceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "ダメージ競争: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);

            if (enemyDamage == 1 && enemyHitsToBreak >= 20)
            {
                if (playerHitsToWin <= 3)
                {
                    return $"ダメージ競争: 有利です。敵は1ずつしか削れず、こちらは{playerHitsToWin}発で倒せます。";
                }

                return $"ダメージ競争: 有利です。敵の削りは1ずつなので、{playerHitsToWin}発かけても主導権があります。";
            }

            if (playerHitsToWin < enemyHitsToBreak)
            {
                return $"ダメージ競争: 有利です。敵が{enemyHitsToBreak}発で崩す前に、こちらは{playerHitsToWin}発で倒せます。";
            }

            if (playerHitsToWin == enemyHitsToBreak)
            {
                return $"ダメージ競争: 五分です。お互いに約{playerHitsToWin}発で決着します。";
            }

            return $"ダメージ競争: 不利です。こちらは{playerHitsToWin}発必要で、敵は{enemyHitsToBreak}発で崩してきます。";
        }

        public static string BuildBurstReadText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "初動火力: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int normalHit = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int critHit = Math.Max(normalHit, (int)Math.Round(normalHit * Math.Max(1f, playerStats.CritDamage)));
            int openingBurst = normalHit * 2 + critHit;

            if (openingBurst >= enemyStats.MaxHp)
            {
                return $"初動火力: 強力です。良い初動なら{nextFloor}階を一気に削り切れます。";
            }

            int remaining = enemyStats.MaxHp - openingBurst;
            if (remaining <= normalHit)
            {
                return $"初動火力: あと一押しです。強い初動なら残り{remaining}HPまで削れます。";
            }

            return $"初動火力: 標準です。初動後も約{remaining}HPの削りが必要です。";
        }

        public static string BuildKillClockText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "撃破時間: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            float attackRate = playerStats.AttackSpeed <= 0f ? 1f : playerStats.AttackSpeed;
            float secondsToKill = playerHitsToWin / attackRate;

            if (secondsToKill <= 2.5f)
            {
                return $"撃破時間: 速いです。約{FormatDurationShort(secondsToKill)}で倒せる見込みです。";
            }

            if (secondsToKill <= 4.5f)
            {
                return $"撃破時間: 安定です。{nextFloor}階は約{FormatDurationShort(secondsToKill)}で倒せる見込みです。";
            }

            return $"撃破時間: 長めです。{nextFloor}階は約{FormatDurationShort(secondsToKill)}の攻撃時間が必要です。";
        }

        public static string BuildCritWindowText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "会心期待: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int normalHit = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int critHit = Math.Max(normalHit, (int)Math.Round(normalHit * Math.Max(1f, playerStats.CritDamage)));
            int critDelta = Math.Max(0, critHit - normalHit);
            int critChancePercent = (int)Math.Round(playerStats.CritRate * 100f);

            if (critChancePercent <= 0 || critDelta <= 0)
            {
                return $"会心期待: 低めです。{nextFloor}階は素の火力勝負になります。";
            }

            if (critChancePercent >= 15)
            {
                return $"会心期待: 高めです。{critChancePercent}%の会心で初動に+{critDelta}ダメージを狙えます。";
            }

            if (critChancePercent >= 8)
            {
                return $"会心期待: 有効です。{critChancePercent}%の会心で削りを{critDelta}分短縮できます。";
            }

            return $"会心期待: 控えめです。会心率{critChancePercent}%なので安定火力を優先しましょう。";
        }

        public static string BuildSurvivalWindowText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "生存時間: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);
            float enemyRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float secondsToBreak = enemyHitsToBreak / enemyRate;

            if (enemyDamage == 1 && enemyHitsToBreak >= 20)
            {
                return $"生存時間: 十分です。{nextFloor}階が倒し切るには約{FormatDurationShort(secondsToBreak)}かかります。";
            }

            if (secondsToBreak <= 3.0f)
            {
                return $"生存時間: 短めです。{nextFloor}階では約{FormatDurationShort(secondsToBreak)}で崩されます。";
            }

            if (secondsToBreak <= 6.0f)
            {
                return $"生存時間: 標準です。危険になるまで約{FormatDurationShort(secondsToBreak)}あります。";
            }

            return $"生存時間: 長めです。約{FormatDurationShort(secondsToBreak)}は圧力に耐えられます。";
        }

        public static string BuildClockEdgeText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "時間差: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);
            float playerRate = playerStats.AttackSpeed <= 0f ? 1f : playerStats.AttackSpeed;
            float enemyRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float secondsToKill = playerHitsToWin / playerRate;
            float secondsToBreak = enemyHitsToBreak / enemyRate;
            float edge = secondsToBreak - secondsToKill;

            if (edge >= 8f)
            {
                return $"時間差: 大きく有利です。{nextFloor}階で約{FormatDurationShort(edge)}の余裕があります。";
            }

            if (edge >= 3f)
            {
                return $"時間差: 有利です。約{FormatDurationShort(edge)}先行できます。";
            }

            if (edge >= 0f)
            {
                return $"時間差: ぎりぎり有利です。余裕は約{FormatDurationShort(edge)}です。";
            }

            return $"時間差: 不利です。{nextFloor}階の方が約{FormatDurationShort(Math.Abs(edge))}速く崩してきます。";
        }

        public static string BuildTempoVerdictText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "テンポ判断: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);
            float playerRate = playerStats.AttackSpeed <= 0f ? 1f : playerStats.AttackSpeed;
            float enemyRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float secondsToKill = playerHitsToWin / playerRate;
            float secondsToBreak = enemyHitsToBreak / enemyRate;
            float edge = secondsToBreak - secondsToKill;

            if (edge >= 20f)
            {
                return $"テンポ判断: 圧倒的に有利です。{nextFloor}階は今の速度についてこられません。";
            }

            if (edge >= 6f)
            {
                return $"テンポ判断: 主導権があります。報酬を受け取ってからでも{nextFloor}階を押せます。";
            }

            if (edge >= 0f)
            {
                return $"テンポ判断: 挑戦可能です。{nextFloor}階は安全圏ですが余裕は大きくありません。";
            }

            return $"テンポ判断: 準備優先です。強化しないと{nextFloor}階に押し負けます。";
        }

        public static string BuildPressureCallText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "圧力判断: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int enemyDamage = Math.Max(1, enemyStats.Attack - playerStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            int enemyHitsToBreak = (int)Math.Ceiling(playerStats.MaxHp / (double)enemyDamage);
            float playerRate = playerStats.AttackSpeed <= 0f ? 1f : playerStats.AttackSpeed;
            float enemyRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float secondsToKill = playerHitsToWin / playerRate;
            float secondsToBreak = enemyHitsToBreak / enemyRate;
            float edge = secondsToBreak - secondsToKill;

            if (edge >= 20f)
            {
                return $"圧力判断: 強気で進めます。{nextFloor}階は完全にこちらのペースです。";
            }

            if (edge >= 6f)
            {
                return "圧力判断: 有利です。報酬を受け取ってからでも主導権を保てます。";
            }

            if (edge >= 0f)
            {
                return "圧力判断: 慎重に進めましょう。時間差は小さめです。";
            }

            return $"圧力判断: {nextFloor}階は強めです。準備で時間を稼いでから挑戦しましょう。";
        }

        public static string BuildRewardPaceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "報酬ペース: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleRewardResult reward = BattleRewardCalculator.Calculate(nextFloor, profile.HighestFloor);
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int playerDamage = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int playerHitsToWin = (int)Math.Ceiling(enemyStats.MaxHp / (double)playerDamage);
            float playerRate = playerStats.AttackSpeed <= 0f ? 1f : playerStats.AttackSpeed;
            float secondsToKill = Math.Max(0.5f, playerHitsToWin / playerRate);
            float goldPerMinute = reward.Gold * (60f / secondsToKill);
            float expPerMinute = reward.Exp * (60f / secondsToKill);

            if (goldPerMinute >= 180f)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "報酬ペース: 良好です。1分あたり約{0:0}ゴールド / 経験値{1:0}を見込めます。",
                    goldPerMinute,
                    expPerMinute);
            }

            if (goldPerMinute >= 90f)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "報酬ペース: 安定です。1分あたり約{0:0}ゴールド / 経験値{1:0}です。",
                    goldPerMinute,
                    expPerMinute);
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "報酬ペース: 控えめです。{0}階はこの速度だと1分あたり約{1:0}ゴールドです。",
                nextFloor,
                goldPerMinute);
        }

        public static string BuildEnhanceHeadline(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "強化優先: データを確認できません。";
            }

            string threat = GetNextFloorThreat(profile);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);

            if (threat.Contains("dangerous"))
            {
                if (profile.Gold >= defenseCost)
                {
                    return string.Format("強化優先: 危険な階の前に、防御を{0}ゴールドで上げましょう。", defenseCost);
                }

                if (profile.Gold >= hpCost)
                {
                    return string.Format("強化優先: 危険な階の前に、HPを{0}ゴールドで上げましょう。", hpCost);
                }
            }

            if (threat.Contains("even"))
            {
                if (profile.Gold >= attackCost)
                {
                    return string.Format("強化優先: 攻撃を{0}ゴールドで上げると五分の階を押し切れます。", attackCost);
                }

                if (profile.Gold >= defenseCost)
                {
                    return string.Format("強化優先: 防御を{0}ゴールドで上げると次の階が安定します。", defenseCost);
                }
            }

            if (profile.Gold >= attackCost)
            {
                return string.Format("強化優先: 攻撃を{0}ゴールドで上げられます。", attackCost);
            }

            int cheapestCost = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            return string.Format("強化優先: 次の強化まであと{0}ゴールドです。", cheapestCost - profile.Gold);
        }

        public static string BuildEquipmentHeadline(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "装備方針: データを確認できません。";
            }

            if (!profile.HasEquipment("equip_iron_sword"))
            {
                return "装備方針: 2階クリアで鉄の剣が解放されます。";
            }

            if (!profile.HasEquipment("equip_bone_mail"))
            {
                return "装備方針: 4階クリアで骨の鎧が解放されます。";
            }

            if (!profile.HasEquipment("equip_quick_charm"))
            {
                return "装備方針: 6階クリアで俊足のお守りが解放されます。";
            }

            string threat = GetNextFloorThreat(profile);
            if (threat.Contains("dangerous") && profile.EquippedArmorId != "equip_bone_mail")
            {
                return "装備方針: 危険な階の前に骨の鎧へ入れ替えましょう。";
            }

            if (threat.Contains("even") && profile.EquippedWeaponId != "equip_iron_sword")
            {
                return "装備方針: 五分の階を押すなら鉄の剣へ入れ替えましょう。";
            }

            return "装備方針: 解放済み装備は揃っています。次の挑戦前に好みで調整できます。";
        }

        public static string BuildMissionHeadline(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "ミッション: データを確認できません。";
            }

            if (DailyRewardService.HasClaimableQuest(profile, now))
            {
                return string.Format(
                    "ミッション: 達成済みデイリーから無償石{0}個を受け取りましょう。",
                    DailyRewardService.GetClaimableRewardFreeGachaStones(profile, now));
            }

            if (!DailyRewardService.AreAllClaimed(profile, now))
            {
                DailyQuestDefinition finalQuest = DailyRewardService.GetDefinitions()[DailyRewardService.GetDefinitions().Count - 1];
                int progress = DailyRewardService.GetBattleWinProgress(profile, now, finalQuest.Id);
                return string.Format(
                    "ミッション: デイリー全達成までバトル勝利{1}/{0}です。",
                    DailyRewardService.GetMaximumRequiredBattleWins(),
                    progress);
            }

            if (IsMissionClaimable(profile, "mission_clear_1", 1))
            {
                return "ミッション: 初勝利報酬を受け取りましょう。";
            }

            if (IsMissionClaimable(profile, "mission_reach_floor_3", 3))
            {
                return "ミッション: 3階到達報酬を受け取りましょう。";
            }

            return "ミッション: 階層を進めると新しい報酬が開きます。";
        }

        public static int GetClaimableMissionGold(PlayerProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var missionCheck in MissionChecks)
            {
                if (!IsMissionClaimable(profile, missionCheck.missionId, missionCheck.targetValue))
                {
                    continue;
                }

                var definition = MissionService.GetDefinition(missionCheck.missionId);
                if (definition.HasValue)
                {
                    total += definition.Value.RewardGold;
                }
            }

            return total;
        }

        public static int GetClaimableMissionCount(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = 0;
            count += DailyRewardService.GetClaimableQuestCount(profile, now);
            foreach (var missionCheck in MissionChecks)
            {
                count += IsMissionClaimable(profile, missionCheck.missionId, missionCheck.targetValue) ? 1 : 0;
            }

            return count;
        }

        public static int GetClaimableRewardGold(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return 0;
            }

            return GetClaimableMissionGold(profile);
        }

        public static string BuildHomeRewardSummary(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "受取可能: 確認できません。";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            int missionCount = GetClaimableMissionCount(profile, now);
            return readyGold > 0
                ? $"受取可能: ミッションに{readyGold}ゴールド（{missionCount}件）あります。"
                : "受取可能: 今すぐ受け取れるゴールドはありません。";
        }

        public static string BuildMissionRewardSummary(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "受取報酬: 確認できません。";
            }

            int missionGold = GetClaimableMissionGold(profile);
            bool dailyReady = DailyRewardService.HasClaimableQuest(profile, now);
            int totalGold = missionGold;
            int totalStones = dailyReady ? DailyRewardService.GetClaimableRewardFreeGachaStones(profile, now) : 0;
            int totalClaims = GetClaimableMissionCount(profile, now);
            if (totalClaims <= 0)
            {
                return "受取報酬: 現在受け取れる報酬はありません。";
            }

            if (totalGold > 0 && totalStones > 0)
            {
                return $"受取報酬: {totalGold}ゴールドと無償石{totalStones}個（{totalClaims}件）を受け取れます。";
            }

            if (totalStones > 0)
            {
                return $"受取報酬: 無償石{totalStones}個を受け取れます。";
            }

            return $"受取報酬: {totalGold}ゴールド（{totalClaims}件）を受け取れます。";
        }

        public static string BuildPriorityTabText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "優先タブ: 確認できません。";
            }

            string threat = GetNextFloorThreat(profile);
            int claimableGold = GetClaimableRewardGold(profile, now);

            int missionClaims = GetClaimableMissionCount(profile, now);
            if (missionClaims > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"優先タブ: ミッション。危険な階の前に{claimableGold}ゴールドを受け取りましょう。";
                }

                return $"優先タブ: ミッション。受け取れる報酬が{missionClaims}件あります。";
            }

            int enhanceCount = GetEnhanceBadgeCount(profile, baseUpgradeCost);
            if (enhanceCount > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return "優先タブ: 強化。危険な階の前に防御かHPを上げましょう。";
                }

                if (threat.Contains("even"))
                {
                    return "優先タブ: 強化。攻撃を上げると五分の階を押しやすくなります。";
                }

                return $"優先タブ: 強化。今すぐ上げられる項目が{enhanceCount}件あります。";
            }

            int equipmentCount = GetEquipmentBadgeCount(profile);
            if (equipmentCount > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return "優先タブ: 装備。危険な階の前に防具を見直しましょう。";
                }

                return $"優先タブ: 装備。入れ替え候補が{equipmentCount}件あります。";
            }

            if (threat.Contains("dangerous"))
            {
                return $"優先タブ: 育成。{profile.HighestFloor + 1}階は危険なので準備してから挑戦しましょう。";
            }

            if (threat.Contains("even"))
            {
                return $"優先タブ: バトル。{profile.HighestFloor + 1}階は五分なので、準備後なら安定します。";
            }

            return $"優先タブ: バトル。{profile.HighestFloor + 1}階へ進めます。";
        }

        public static string BuildPrepAdviceText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "準備アドバイス: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            BattleUnitStats currentStats = PlayerBattleStatsFactory.CreatePreview(profile);
            CandidateAdvice best = BuildBestCandidate(profile, baseUpgradeCost, enemyStats, currentStats);

            if (best.Label == null)
            {
                string currentThreat = TrimThreat(BattleEncounterAdvisor.BuildThreatText(currentStats, enemyStats));
                return $"準備アドバイス: 現在は{DescribeThreatWithNoun(currentThreat)}です。準備できたら挑戦しましょう。";
            }

            if (string.Equals(best.BeforeThreat, best.AfterThreat, StringComparison.Ordinal))
            {
                if (best.AfterThreat.Contains("dangerous"))
                {
                    return $"準備アドバイス: {best.Label}で{best.Detail}。{nextFloor}階はまだ危険なので、追加の育成や装備も見ましょう。";
                }

                if (best.AfterThreat.Contains("even"))
                {
                    return $"準備アドバイス: {best.Label}で{best.Detail}。{nextFloor}階は五分なので、もう一手で安定します。";
                }

                return $"準備アドバイス: {best.Label}で{best.Detail}。{nextFloor}階は{DescribeThreatWithNoun(best.AfterThreat)}のまま安定します。";
            }

            return $"準備アドバイス: {best.Label}で{nextFloor}階が{DescribeThreat(best.BeforeThreat)}から{DescribeThreat(best.AfterThreat)}へ改善します（{best.Detail}）。";
        }

        public static string BuildBattlePlanText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "バトル計画: 確認できません。";
            }

            int nextFloor = profile.HighestFloor + 1;
            int missionClaims = GetClaimableMissionCount(profile, now);
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(nextFloor),
                PlayerBattleStatsFactory.CreatePreview(profile));

            string prepStep = best.Label != null
                ? $"{best.Label}（{best.PlanDetail}）後に{nextFloor}階へ挑戦"
                : $"{nextFloor}階へ挑戦";
            BattleEncounterAdvisor.BattleEncounterAssessment assessment = BattleEncounterAdvisor.AssessFloor(profile, nextFloor);
            string threat = TrimThreat(assessment.ThreatText);

            if (missionClaims > 0)
            {
                return $"バトル計画: 報酬{missionClaims}件を受け取ってから、{prepStep}。";
            }

            if (threat.Contains("dangerous"))
            {
                return $"バトル計画: {nextFloor}階は推奨戦力{FormatCombatPower(assessment.RecommendedCombatPower)}以上です。挑戦前に育成と装備確認を優先しましょう。";
            }

            if (threat.Contains("even"))
            {
                return $"バトル計画: {nextFloor}階は五分です。{prepStep}の前に装備を確認すると安定します。";
            }

            return $"バトル計画: 準備は整っています。今から{nextFloor}階へ挑戦できます。";
        }

        private static string GetNextFloorThreat(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Threat: unknown";
            }

            int nextFloor = profile.HighestFloor + 1;
            return BattleEncounterAdvisor.AssessFloor(profile, nextFloor).ThreatText;
        }

        private static CandidateAdvice BuildBestCandidate(PlayerProfile profile, int baseUpgradeCost, BattleUnitStats enemyStats, BattleUnitStats currentStats)
        {
            string currentThreat = TrimThreat(BattleEncounterAdvisor.BuildThreatText(currentStats, enemyStats));
            float currentScore = ScoreThreat(currentStats, enemyStats);
            CandidateAdvice best = default;

            EvaluateCandidate(
                ref best,
                "攻撃強化",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Attack)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateCandidate(
                ref best,
                "防御強化",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Defense)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateCandidate(
                ref best,
                "HP強化",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Hp)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateEquipmentCandidate(ref best, "鉄の剣を装備", profile.HasEquipment("equip_iron_sword") && profile.EquippedWeaponId != "equip_iron_sword"
                ? PlayerBattleStatsFactory.CreatePreviewWithEquipment(profile, "equip_iron_sword", null, null)
                : null, currentStats, currentThreat, currentScore, enemyStats);
            EvaluateEquipmentCandidate(ref best, "骨の鎧を装備", profile.HasEquipment("equip_bone_mail") && profile.EquippedArmorId != "equip_bone_mail"
                ? PlayerBattleStatsFactory.CreatePreviewWithEquipment(profile, null, "equip_bone_mail", null)
                : null, currentStats, currentThreat, currentScore, enemyStats);
            EvaluateEquipmentCandidate(ref best, "俊足のお守りを装備", profile.HasEquipment("equip_quick_charm") && profile.EquippedAccessoryId != "equip_quick_charm"
                ? PlayerBattleStatsFactory.CreatePreviewWithEquipment(profile, null, null, "equip_quick_charm")
                : null, currentStats, currentThreat, currentScore, enemyStats);

            return best;
        }

        private static void EvaluateEquipmentCandidate(ref CandidateAdvice best, string label, BattleUnitStats candidateStats, BattleUnitStats currentStats, string currentThreat, float currentScore, BattleUnitStats enemyStats)
        {
            EvaluateCandidate(ref best, label, candidateStats, currentStats, currentThreat, currentScore, enemyStats);
        }

        private static void EvaluateCandidate(ref CandidateAdvice best, string label, BattleUnitStats candidateStats, BattleUnitStats currentStats, string currentThreat, float currentScore, BattleUnitStats enemyStats)
        {
            if (candidateStats == null)
            {
                return;
            }

            string nextThreat = TrimThreat(BattleEncounterAdvisor.BuildThreatText(candidateStats, enemyStats));
            float nextScore = ScoreThreat(candidateStats, enemyStats);
            CandidateAdvice candidate = new CandidateAdvice
            {
                Label = label,
                BeforeThreat = currentThreat,
                AfterThreat = nextThreat,
                ThreatRank = ThreatRank(nextThreat),
                Score = nextScore,
                Detail = BuildDeltaDetail(candidateStats, currentStats),
                PlanDetail = BuildPlanDeltaDetail(candidateStats, currentStats)
            };

            if (best.Label == null ||
                candidate.ThreatRank < best.ThreatRank ||
                (candidate.ThreatRank == best.ThreatRank && candidate.Score < best.Score) ||
                (candidate.ThreatRank == best.ThreatRank && Math.Abs(candidate.Score - best.Score) < 0.001f && nextScore < currentScore))
            {
                best = candidate;
            }
        }

        private static int ThreatRank(string threat)
        {
            if (threat.Contains("dangerous"))
            {
                return 2;
            }

            if (threat.Contains("even"))
            {
                return 1;
            }

            return 0;
        }

        private static float ScoreThreat(BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            float playerScore = playerStats.MaxHp + playerStats.Attack * 4f + playerStats.Defense * 3f + playerStats.CritRate * 100f;
            float enemyScore = enemyStats.MaxHp + enemyStats.Attack * 4f + enemyStats.Defense * 3f + enemyStats.CritRate * 100f;
            return enemyScore / Math.Max(1f, playerScore);
        }

        private static string BuildDeltaDetail(BattleUnitStats candidateStats, BattleUnitStats currentStats)
        {
            if (candidateStats == null || currentStats == null)
            {
                return "変化量を確認できません";
            }

            int hpDelta = candidateStats.MaxHp - currentStats.MaxHp;
            int attackDelta = candidateStats.Attack - currentStats.Attack;
            int defenseDelta = candidateStats.Defense - currentStats.Defense;
            return BuildCompactDeltaDetail(hpDelta, attackDelta, defenseDelta);
        }

        private static string BuildPlanDeltaDetail(BattleUnitStats candidateStats, BattleUnitStats currentStats)
        {
            if (candidateStats == null || currentStats == null)
            {
                return "戦力アップ";
            }

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            int hpDelta = candidateStats.MaxHp - currentStats.MaxHp;
            int attackDelta = candidateStats.Attack - currentStats.Attack;
            int defenseDelta = candidateStats.Defense - currentStats.Defense;

            if (hpDelta != 0)
            {
                parts.Add($"{FormatSigned(hpDelta)} HP");
            }

            if (attackDelta != 0)
            {
                parts.Add($"{FormatSigned(attackDelta)} ATK");
            }

            if (defenseDelta != 0)
            {
                parts.Add($"{FormatSigned(defenseDelta)} DEF");
            }

            if (parts.Count == 0)
            {
                return "相性が安定";
            }

            if (parts.Count == 1)
            {
                return parts[0];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + "、" + parts[parts.Count - 1];
        }

        private static string BuildCompactDeltaDetail(int hpDelta, int attackDelta, int defenseDelta)
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();

            if (hpDelta != 0)
            {
                parts.Add($"HP {FormatSigned(hpDelta)}");
            }

            if (attackDelta != 0)
            {
                parts.Add($"ATK {FormatSigned(attackDelta)}");
            }

            if (defenseDelta != 0)
            {
                parts.Add($"DEF {FormatSigned(defenseDelta)}");
            }

            if (parts.Count == 0)
            {
                return "ステータス変化なし";
            }

            return string.Join(", ", parts);
        }

        private static string BuildPartyPowerRead(BattleEncounterAdvisor.BattleEncounterAssessment assessment)
        {
            if (assessment.PartyCombatPower <= 0)
            {
                return string.Empty;
            }

            return $"現戦力{FormatCombatPower(assessment.PartyCombatPower)}/";
        }

        private static string FormatCombatPower(int combatPower)
        {
            return Math.Max(0, combatPower).ToString("N0");
        }

        private static string BuildFloorLabel(int globalFloor)
        {
            int safeFloor = Math.Max(1, globalFloor);
            string dungeonName = BattleDungeonCatalog.ResolveDungeonName(safeFloor);
            int localFloor = BattleDungeonCatalog.ResolveLocalFloor(safeFloor);
            return string.IsNullOrEmpty(dungeonName)
                ? $"{safeFloor}階"
                : $"{dungeonName} 第{localFloor}階層";
        }

        private static string DescribeThreat(string threat)
        {
            if (string.IsNullOrEmpty(threat))
            {
                return "不明";
            }

            if (threat.Contains("dangerous"))
            {
                return "危険";
            }

            if (threat.Contains("even"))
            {
                return "五分";
            }

            if (threat.Contains("favorable"))
            {
                return "有利";
            }

            return "不明";
        }

        private static string DescribeThreatWithNoun(string threat)
        {
            if (string.IsNullOrEmpty(threat))
            {
                return "強さ不明の相手";
            }

            if (threat.Contains("dangerous"))
            {
                return "危険な相手";
            }

            if (threat.Contains("even"))
            {
                return "五分の相手";
            }

            if (threat.Contains("favorable"))
            {
                return "有利な相手";
            }

            return "強さ不明の相手";
        }

        private static string FormatSigned(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        private static string FormatDurationShort(float seconds)
        {
            if (seconds < 60f)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}s", seconds);
            }

            int totalSeconds = Math.Max(0, (int)Math.Round(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes}m {remainingSeconds:D2}s";
        }

        private static string TrimThreat(string threatLabel)
        {
            return (threatLabel ?? "Threat: unknown").Replace("Threat: ", string.Empty).Trim().ToLowerInvariant();
        }

        private struct CandidateAdvice
        {
            public string Label;
            public string BeforeThreat;
            public string AfterThreat;
            public int ThreatRank;
            public float Score;
            public string Detail;
            public string PlanDetail;
        }

        private static bool IsOwnedButNotEquipped(PlayerProfile profile, string equipmentId, string equippedId)
        {
            return profile.HasEquipment(equipmentId) && equippedId != equipmentId;
        }

        private static int GetUpgradeCost(int baseUpgradeCost, int currentLevel)
        {
            return baseUpgradeCost + currentLevel * 5;
        }

        private static bool IsMissionClaimable(PlayerProfile profile, string missionId, int requiredProgress)
        {
            var progress = profile.GetMissionProgress(missionId);
            return progress != null && !progress.IsClaimed && progress.Progress >= requiredProgress;
        }
    }
}
