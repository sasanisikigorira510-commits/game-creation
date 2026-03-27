using System;
using WitchTower.Battle;
using WitchTower.Data;

namespace WitchTower.Home
{
    public static class HomeActionAdvisor
    {
        private const int DailyRewardGold = 50;
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
            count += profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd")) ? 1 : 0;
            count += IsMissionClaimable(profile, "mission_clear_1", 1) ? 1 : 0;
            count += IsMissionClaimable(profile, "mission_reach_floor_3", 3) ? 1 : 0;
            return count;
        }

        public static int GetHomeBadgeCount(PlayerProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            return profile.PendingIdleRewardGold > 0 ? 1 : 0;
        }

        public static string BuildHomeHeadline(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Next Step: load a profile to resume the climb.";
            }

            if (profile.PendingIdleRewardGold > 0)
            {
                return string.Format(
                    "Next Step: claim {0} idle gold, then push floor {1}.",
                    profile.PendingIdleRewardGold,
                    profile.HighestFloor + 1);
            }

            return string.Format(
                "Next Step: enter Battle and challenge floor {0}.",
                profile.HighestFloor + 1);
        }

        public static string BuildRunProgressText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Progress: no active run data.";
            }

            return string.Format(
                "Progress: cleared floor {0}, targeting floor {1}.",
                profile.HighestFloor,
                profile.HighestFloor + 1);
        }

        public static string BuildRunAlertText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Action Cue: profile unavailable.";
            }

            if (profile.PendingIdleRewardGold > 0)
            {
                return string.Format("Action Cue: collect {0} idle gold before the next push.", profile.PendingIdleRewardGold);
            }

            int missionClaims = GetMissionBadgeCount(profile, now);
            if (missionClaims > 0)
            {
                return string.Format("Action Cue: {0} mission reward{1} ready to claim.", missionClaims, missionClaims == 1 ? "" : "s");
            }

            int affordableUpgrades = GetEnhanceBadgeCount(profile, baseUpgradeCost);
            if (affordableUpgrades > 0)
            {
                return string.Format("Action Cue: {0} upgrade{1} affordable right now.", affordableUpgrades, affordableUpgrades == 1 ? "" : "s");
            }

            int equipmentChoices = GetEquipmentBadgeCount(profile);
            if (equipmentChoices > 0)
            {
                return string.Format("Action Cue: {0} unlocked gear swap{1} available.", equipmentChoices, equipmentChoices == 1 ? "" : "s");
            }

            return "Action Cue: build is stable. Push the next battle.";
        }

        public static string BuildRewardForecastText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Reward Forecast: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleRewardResult reward = BattleRewardCalculator.Calculate(nextFloor, profile.HighestFloor);
            return string.Format(
                "Reward Forecast: floor {0} should pay about {1} Gold and {2} EXP.",
                nextFloor,
                reward.Gold,
                reward.Exp);
        }

        public static string BuildThreatReadText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Threat Read: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            string threat = TrimThreat(GetNextFloorThreat(profile));
            return $"Threat Read: floor {nextFloor} looks {threat}.";
        }

        public static string BuildConfidenceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Confidence: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            string threat = TrimThreat(BattleEncounterAdvisor.BuildThreatText(playerStats, enemyStats));
            float score = ScoreThreat(playerStats, enemyStats);

            if (threat.Contains("dangerous"))
            {
                return score >= 1.05f
                    ? $"Confidence: low on floor {nextFloor}; bank rewards and prep before the next pull."
                    : $"Confidence: fragile on floor {nextFloor}; one upgrade or gear swap should steady the climb.";
            }

            if (threat.Contains("even"))
            {
                return score >= 0.95f
                    ? $"Confidence: measured on floor {nextFloor}; take one prep step before you push."
                    : $"Confidence: steady on floor {nextFloor}; a clean upgrade should flip the matchup.";
            }

            if (score <= 0.72f)
            {
                return $"Confidence: high on floor {nextFloor}; this matchup favors an aggressive push.";
            }

            return $"Confidence: solid on floor {nextFloor}; rewards can wait if you want momentum.";
        }

        public static string BuildLoadoutAlertText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Loadout Alert: unavailable.";
            }

            System.Collections.Generic.List<string> upgrades = new System.Collections.Generic.List<string>();

            if (profile.HasEquipment("equip_iron_sword") && profile.EquippedWeaponId != "equip_iron_sword")
            {
                upgrades.Add("Iron Sword");
            }

            if (profile.HasEquipment("equip_bone_mail") && profile.EquippedArmorId != "equip_bone_mail")
            {
                upgrades.Add("Bone Mail");
            }

            if (profile.HasEquipment("equip_quick_charm") && profile.EquippedAccessoryId != "equip_quick_charm")
            {
                upgrades.Add("Quick Charm");
            }

            if (upgrades.Count == 0)
            {
                return "Loadout Alert: current gear is already on the strongest unlocked setup.";
            }

            if (upgrades.Count == 1)
            {
                return $"Loadout Alert: {upgrades[0]} is ready now and stronger than your current slot.";
            }

            if (upgrades.Count == 2)
            {
                return $"Loadout Alert: {upgrades[0]} and {upgrades[1]} are ready now for an immediate power spike.";
            }

            return $"Loadout Alert: {upgrades[0]}, {upgrades[1]}, and {upgrades[2]} are all ready now for a full upgrade pass.";
        }

        public static string BuildGoldRouteText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Gold Route: unavailable.";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);
            int cheapestUpgrade = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            string threat = TrimThreat(GetNextFloorThreat(profile));

            if (readyGold > 0)
            {
                return $"Gold Route: collect {readyGold} Gold first, then decide whether to bank it or buy into floor {profile.HighestFloor + 1}.";
            }

            if (profile.Gold >= cheapestUpgrade)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"Gold Route: spend now; your stash already covers a defensive upgrade before floor {profile.HighestFloor + 1}.";
                }

                return $"Gold Route: spend now; your stash already covers the next upgrade break point.";
            }

            int missingGold = Math.Max(0, cheapestUpgrade - profile.Gold);
            return $"Gold Route: save {missingGold} more Gold to unlock the next upgrade tier, or keep pushing for momentum.";
        }

        public static string BuildUpgradeRouteText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "Upgrade Route: unavailable.";
            }

            string threat = TrimThreat(GetNextFloorThreat(profile));
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);

            if (threat.Contains("dangerous"))
            {
                if (profile.Gold >= defenseCost)
                {
                    return $"Upgrade Route: Defense is the cleanest spend at {defenseCost} Gold before floor {profile.HighestFloor + 1}.";
                }

                if (profile.Gold >= hpCost)
                {
                    return $"Upgrade Route: HP is the cleanest spend at {hpCost} Gold before floor {profile.HighestFloor + 1}.";
                }

                int missing = Math.Min(Math.Max(0, defenseCost - profile.Gold), Math.Max(0, hpCost - profile.Gold));
                return $"Upgrade Route: save {missing} more Gold for a defensive buy before floor {profile.HighestFloor + 1}.";
            }

            if (threat.Contains("even"))
            {
                if (profile.Gold >= attackCost)
                {
                    return $"Upgrade Route: Attack is the cleanest spend at {attackCost} Gold to flip floor {profile.HighestFloor + 1}.";
                }

                int missing = Math.Max(0, attackCost - profile.Gold);
                return $"Upgrade Route: save {missing} more Gold for the Attack breakpoint on floor {profile.HighestFloor + 1}.";
            }

            if (profile.Gold >= attackCost)
            {
                return $"Upgrade Route: Attack is the fastest spend at {attackCost} Gold for a cleaner push.";
            }

            int cheapest = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            return $"Upgrade Route: save {Math.Max(0, cheapest - profile.Gold)} more Gold for the next upgrade break point.";
        }

        public static string BuildRewardRouteText(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "Reward Route: unavailable.";
            }

            bool dailyReady = profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd"));
            bool missionClearReady = IsMissionClaimable(profile, "mission_clear_1", 1);
            bool missionFloorReady = IsMissionClaimable(profile, "mission_reach_floor_3", 3);

            if (profile.PendingIdleRewardGold > 0)
            {
                return $"Reward Route: claim idle first for {profile.PendingIdleRewardGold} Gold that is already banked.";
            }

            if (dailyReady)
            {
                return $"Reward Route: daily reward first for {DailyRewardGold} Gold before anything else.";
            }

            if (missionFloorReady)
            {
                MissionDefinition? definition = MissionService.GetDefinition("mission_reach_floor_3");
                int gold = definition.HasValue ? definition.Value.RewardGold : 0;
                return $"Reward Route: floor milestone claim next for {gold} Gold.";
            }

            if (missionClearReady)
            {
                MissionDefinition? definition = MissionService.GetDefinition("mission_clear_1");
                int gold = definition.HasValue ? definition.Value.RewardGold : 0;
                return $"Reward Route: first victory claim next for {gold} Gold.";
            }

            return "Reward Route: no claims are waiting; push the next floor for fresh rewards.";
        }

        public static string BuildPushWindowText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Push Window: unavailable.";
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
                return $"Push Window: hold for a moment, cash out {readyGold} Gold, then reassess the climb.";
            }

            if (threat.Contains("dangerous"))
            {
                if (best.Label != null)
                {
                    return $"Push Window: prep first; {best.Label} gives you the safer entry.";
                }

                return $"Push Window: narrow; floor {profile.HighestFloor + 1} needs a little more setup.";
            }

            if (threat.Contains("even"))
            {
                if (best.Label != null)
                {
                    return $"Push Window: flexible; one prep step like {best.Label} should tip the floor.";
                }

                return $"Push Window: playable, but one upgrade would make floor {profile.HighestFloor + 1} cleaner.";
            }

            return $"Push Window: open now; floor {profile.HighestFloor + 1} is ready for an immediate push.";
        }

        public static string BuildRoiReadText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "ROI Read: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleRewardResult reward = BattleRewardCalculator.Calculate(nextFloor, profile.HighestFloor);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);
            int cheapestUpgrade = Math.Min(attackCost, Math.Min(defenseCost, hpCost));

            if (reward.Gold >= cheapestUpgrade)
            {
                return $"ROI Read: one clear covers the next upgrade outright with {reward.Gold} Gold.";
            }

            int shortfall = cheapestUpgrade - reward.Gold;
            if (reward.Gold * 2 >= cheapestUpgrade)
            {
                return $"ROI Read: one clear nearly covers the next upgrade, missing only {shortfall} Gold.";
            }

            return $"ROI Read: this floor pays {reward.Gold} Gold, so you still need {shortfall} more for the next upgrade.";
        }

        public static string BuildDecisionLineText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Decision Line: unavailable.";
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
                return $"Decision Line: cash out first, then revisit the floor with a stronger budget.";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"Decision Line: take {best.Label}, then enter once the matchup settles."
                    : $"Decision Line: delay the push and build more safety before floor {profile.HighestFloor + 1}.";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? $"Decision Line: one prep step, then commit to the push."
                    : $"Decision Line: the floor is playable now, but one more boost is cleaner.";
            }

            return $"Decision Line: push now unless you want to squeeze extra value from open claims.";
        }

        public static string BuildDecisionBadgeText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Decision: Unknown";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            string threat = TrimThreat(GetNextFloorThreat(profile));

            if (readyGold > 0)
            {
                return "Decision: Cash Out";
            }

            if (threat.Contains("dangerous"))
            {
                return "Decision: Prep";
            }

            if (threat.Contains("even"))
            {
                return "Decision: Tune";
            }

            return "Decision: Push";
        }

        public static string BuildCommandStackText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Command Stack: unavailable.";
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
                stepOne = missionClaims > 0
                    ? $"1. Claim {missionClaims} reward {(missionClaims == 1 ? "step" : "steps")} for {readyGold} Gold"
                    : $"1. Collect {readyGold} idle Gold";
            }
            else if (best.Label != null && (threat.Contains("dangerous") || threat.Contains("even")))
            {
                stepOne = $"1. Prep with {best.Label}";
            }
            else
            {
                stepOne = $"1. Open floor {nextFloor}";
            }

            string stepTwo;
            if (best.Label != null)
            {
                stepTwo = $"2. Lock in {best.Label} ({best.PlanDetail})";
            }
            else if (threat.Contains("dangerous"))
            {
                stepTwo = "2. Bank resources before the pull";
            }
            else
            {
                stepTwo = "2. Keep the current build";
            }

            string stepThree;
            if (threat.Contains("dangerous"))
            {
                stepThree = $"3. Recheck floor {nextFloor} before entering";
            }
            else if (threat.Contains("even"))
            {
                stepThree = $"3. Push floor {nextFloor} once the prep lands";
            }
            else
            {
                stepThree = $"3. Push floor {nextFloor} now";
            }

            return $"Command Stack: {stepOne}  {stepTwo}  {stepThree}.";
        }

        public static string BuildMomentumReadText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Momentum Read: unavailable.";
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
                return $"Momentum Read: paused; {readyGold} Gold is on the table before floor {nextFloor}.";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"Momentum Read: cautious; {best.Label} is the clean stabilizer before floor {nextFloor}."
                    : $"Momentum Read: stalled; floor {nextFloor} needs a sturdier setup.";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? $"Momentum Read: teed up; {best.Label} should flip floor {nextFloor}."
                    : $"Momentum Read: balanced; one extra spend would make floor {nextFloor} cleaner.";
            }

            return $"Momentum Read: live; your current build can press floor {nextFloor} immediately.";
        }

        public static string BuildRunCallText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Run Call: unavailable.";
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
                return $"Run Call: cash out first, then make the floor {nextFloor} call.";
            }

            if (threat.Contains("dangerous"))
            {
                return best.Label != null
                    ? $"Run Call: prep with {best.Label}, then reopen the push."
                    : $"Run Call: do not force floor {nextFloor} yet.";
            }

            if (threat.Contains("even"))
            {
                return best.Label != null
                    ? $"Run Call: take {best.Label}, then commit."
                    : $"Run Call: one small tune, then go.";
            }

            return $"Run Call: green light, take floor {nextFloor} now.";
        }

        public static string BuildRiskBufferText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Risk Buffer: unavailable.";
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
                return $"Risk Buffer: thin; floor {nextFloor} can crack you inside three enemy hits.";
            }

            if (hpMargin <= 20)
            {
                return $"Risk Buffer: narrow; you are only {hpMargin} HP above a three-hit break line.";
            }

            if (enemyDamage == 1 && enemyHitsToBreak >= 20)
            {
                return $"Risk Buffer: locked in; floor {nextFloor} only chips for 1 damage a hit right now.";
            }

            if (defenseMargin >= 5)
            {
                return $"Risk Buffer: sturdy; your build carries a {hpMargin} HP cushion into floor {nextFloor}.";
            }

            return $"Risk Buffer: workable; floor {nextFloor} leaves about {hpMargin} HP of breathing room.";
        }

        public static string BuildEnemyTempoText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Enemy Tempo: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            float swingRate = enemyStats.AttackSpeed <= 0f ? 1f : enemyStats.AttackSpeed;
            float swingSeconds = 1f / swingRate;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Enemy Tempo: floor {0} swings every {1:0.00}s for {2} damage pressure.",
                nextFloor,
                swingSeconds,
                enemyStats.Attack);
        }

        public static string BuildDamageRaceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Damage Race: unavailable.";
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
                    return $"Damage Race: favored; you finish in {playerHitsToWin} hits while the enemy only chips for 1.";
                }

                return $"Damage Race: favored; the enemy only chips for 1, so the pace is yours to control over {playerHitsToWin} hits.";
            }

            if (playerHitsToWin < enemyHitsToBreak)
            {
                return $"Damage Race: favored; you close in {playerHitsToWin} hits before the enemy can grind through your {enemyHitsToBreak}-hit buffer.";
            }

            if (playerHitsToWin == enemyHitsToBreak)
            {
                return $"Damage Race: even; both sides project a {playerHitsToWin}-hit finish.";
            }

            return $"Damage Race: behind; you need {playerHitsToWin} hits while the enemy can crack you in {enemyHitsToBreak}.";
        }

        public static string BuildBurstReadText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Burst Read: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);

            int normalHit = Math.Max(1, playerStats.Attack - enemyStats.Defense);
            int critHit = Math.Max(normalHit, (int)Math.Round(normalHit * Math.Max(1f, playerStats.CritDamage)));
            int openingBurst = normalHit * 2 + critHit;

            if (openingBurst >= enemyStats.MaxHp)
            {
                return $"Burst Read: lethal; a hot opener can erase floor {nextFloor} almost immediately.";
            }

            int remaining = enemyStats.MaxHp - openingBurst;
            if (remaining <= normalHit)
            {
                return $"Burst Read: closeout range; one strong opener leaves only {remaining} HP behind.";
            }

            return $"Burst Read: measured; the opener still leaves about {remaining} HP to clean up.";
        }

        public static string BuildKillClockText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Kill Clock: unavailable.";
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
                return $"Kill Clock: fast; this build projects a finish in about {FormatDurationShort(secondsToKill)}.";
            }

            if (secondsToKill <= 4.5f)
            {
                return $"Kill Clock: steady; expect roughly {FormatDurationShort(secondsToKill)} to close floor {nextFloor}.";
            }

            return $"Kill Clock: long; floor {nextFloor} asks for about {FormatDurationShort(secondsToKill)} of clean uptime.";
        }

        public static string BuildCritWindowText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Crit Window: unavailable.";
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
                return $"Crit Window: flat; floor {nextFloor} is mostly a straight damage check.";
            }

            if (critChancePercent >= 15)
            {
                return $"Crit Window: live; a {critChancePercent}% crit spike adds {critDelta} extra damage to the opener.";
            }

            if (critChancePercent >= 8)
            {
                return $"Crit Window: useful; a {critChancePercent}% crit chance can trim {critDelta} damage off the cleanup.";
            }

            return $"Crit Window: light; crits only show up at {critChancePercent}%, so play the steady line first.";
        }

        public static string BuildSurvivalWindowText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Survival Window: unavailable.";
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
                return $"Survival Window: massive; floor {nextFloor} needs about {FormatDurationShort(secondsToBreak)} to wear you down at this pace.";
            }

            if (secondsToBreak <= 3.0f)
            {
                return $"Survival Window: short; floor {nextFloor} can break you in about {FormatDurationShort(secondsToBreak)}.";
            }

            if (secondsToBreak <= 6.0f)
            {
                return $"Survival Window: fair; you have about {FormatDurationShort(secondsToBreak)} before the floor turns lethal.";
            }

            return $"Survival Window: long; you can absorb roughly {FormatDurationShort(secondsToBreak)} of pressure here.";
        }

        public static string BuildClockEdgeText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Clock Edge: unavailable.";
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
                return $"Clock Edge: dominant; you hold about {FormatDurationShort(edge)} of spare tempo on floor {nextFloor}.";
            }

            if (edge >= 3f)
            {
                return $"Clock Edge: favorable; your timer stays ahead by about {FormatDurationShort(edge)}.";
            }

            if (edge >= 0f)
            {
                return $"Clock Edge: narrow; you only keep about {FormatDurationShort(edge)} in hand here.";
            }

            return $"Clock Edge: behind; floor {nextFloor} beats your pace by about {FormatDurationShort(Math.Abs(edge))}.";
        }

        public static string BuildTempoVerdictText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Tempo Verdict: unavailable.";
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
                return $"Tempo Verdict: overwhelming; floor {nextFloor} cannot keep up with your current pace.";
            }

            if (edge >= 6f)
            {
                return $"Tempo Verdict: yours to control; you have time to cash out and still dictate floor {nextFloor}.";
            }

            if (edge >= 0f)
            {
                return $"Tempo Verdict: playable; floor {nextFloor} is safe, but the edge is no longer huge.";
            }

            return $"Tempo Verdict: prep first; floor {nextFloor} wins the timer unless you tune the build.";
        }

        public static string BuildPressureCallText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Pressure Call: unavailable.";
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
                return $"Pressure Call: full send; floor {nextFloor} is running on your clock now.";
            }

            if (edge >= 6f)
            {
                return $"Pressure Call: favored; you can safely bank rewards and still own the next exchange.";
            }

            if (edge >= 0f)
            {
                return $"Pressure Call: measured; take the clean line because the timer edge is smaller now.";
            }

            return $"Pressure Call: respect the floor; buy time with prep before you take floor {nextFloor}.";
        }

        public static string BuildRewardPaceText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Reward Pace: unavailable.";
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
                    "Reward Pace: rich; this line projects about {0:0} Gold and {1:0} EXP per minute.",
                    goldPerMinute,
                    expPerMinute);
            }

            if (goldPerMinute >= 90f)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Reward Pace: steady; expect roughly {0:0} Gold and {1:0} EXP per minute.",
                    goldPerMinute,
                    expPerMinute);
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Reward Pace: lean; floor {0} only returns about {1:0} Gold a minute at this speed.",
                nextFloor,
                goldPerMinute);
        }

        public static string BuildEnhanceHeadline(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "Upgrade Priority: profile unavailable.";
            }

            string threat = GetNextFloorThreat(profile);
            int attackCost = GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel);
            int defenseCost = GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel);
            int hpCost = GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel);

            if (threat.Contains("dangerous"))
            {
                if (profile.Gold >= defenseCost)
                {
                    return string.Format("Upgrade Priority: Defense is affordable now for {0} Gold before a dangerous floor.", defenseCost);
                }

                if (profile.Gold >= hpCost)
                {
                    return string.Format("Upgrade Priority: HP is affordable now for {0} Gold before a dangerous floor.", hpCost);
                }
            }

            if (threat.Contains("even"))
            {
                if (profile.Gold >= attackCost)
                {
                    return string.Format("Upgrade Priority: Attack is affordable now for {0} Gold to break the even floor.", attackCost);
                }

                if (profile.Gold >= defenseCost)
                {
                    return string.Format("Upgrade Priority: Defense is affordable now for {0} Gold to steady the next floor.", defenseCost);
                }
            }

            if (profile.Gold >= attackCost)
            {
                return string.Format("Upgrade Priority: Attack is affordable now for {0} Gold.", attackCost);
            }

            int cheapestCost = Math.Min(attackCost, Math.Min(defenseCost, hpCost));
            return string.Format("Upgrade Priority: save {0} more Gold for the next boost.", cheapestCost - profile.Gold);
        }

        public static string BuildEquipmentHeadline(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Loadout Focus: profile unavailable.";
            }

            if (!profile.HasEquipment("equip_iron_sword"))
            {
                return "Loadout Focus: clear floor 2 to unlock Iron Sword.";
            }

            if (!profile.HasEquipment("equip_bone_mail"))
            {
                return "Loadout Focus: clear floor 4 to unlock Bone Mail.";
            }

            if (!profile.HasEquipment("equip_quick_charm"))
            {
                return "Loadout Focus: clear floor 6 to unlock Quick Charm.";
            }

            string threat = GetNextFloorThreat(profile);
            if (threat.Contains("dangerous") && profile.EquippedArmorId != "equip_bone_mail")
            {
                return "Loadout Focus: switch into Bone Mail before the dangerous floor.";
            }

            if (threat.Contains("even") && profile.EquippedWeaponId != "equip_iron_sword")
            {
                return "Loadout Focus: switch into Iron Sword to push through the even floor.";
            }

            return "Loadout Focus: all sample gear unlocked. Tune your build before the next push.";
        }

        public static string BuildMissionHeadline(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "Mission Focus: profile unavailable.";
            }

            if (profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd")))
            {
                return string.Format("Mission Focus: claim today's {0} Gold daily reward.", DailyRewardGold);
            }

            if (IsMissionClaimable(profile, "mission_clear_1", 1))
            {
                return "Mission Focus: claim the first battle victory reward.";
            }

            if (IsMissionClaimable(profile, "mission_reach_floor_3", 3))
            {
                return "Mission Focus: claim the floor 3 milestone reward.";
            }

            return "Mission Focus: keep climbing to open more reward claims.";
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
            count += profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd")) ? 1 : 0;
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

            int total = profile.PendingIdleRewardGold;
            total += GetClaimableMissionGold(profile);
            if (profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd")))
            {
                total += DailyRewardGold;
            }

            return total;
        }

        public static string BuildHomeRewardSummary(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "Ready Gold: unavailable.";
            }

            int readyGold = GetClaimableRewardGold(profile, now);
            int missionCount = GetClaimableMissionCount(profile, now);
            return readyGold > 0
                ? $"Ready Gold: {readyGold} waiting across idle and mission claims ({missionCount} ready)."
                : "Ready Gold: nothing to claim right now.";
        }

        public static string BuildMissionRewardSummary(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return "Claimable Rewards: unavailable.";
            }

            int missionGold = GetClaimableMissionGold(profile);
            bool dailyReady = profile.CanClaimDailyReward(now.ToString("yyyy-MM-dd"));
            int totalGold = missionGold + (dailyReady ? DailyRewardGold : 0);
            int totalClaims = GetClaimableMissionCount(profile, now);
            return totalClaims > 0
                ? $"Claimable Rewards: {totalGold} Gold across {totalClaims} ready claims."
                : "Claimable Rewards: no reward claims ready.";
        }

        public static string BuildPriorityTabText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Priority Tab: unavailable.";
            }

            string threat = GetNextFloorThreat(profile);
            int claimableGold = GetClaimableRewardGold(profile, now);

            if (profile.PendingIdleRewardGold > 0)
            {
                return $"Priority Tab: Home first, collect {profile.PendingIdleRewardGold} idle Gold.";
            }

            int missionClaims = GetClaimableMissionCount(profile, now);
            if (missionClaims > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"Priority Tab: Mission first, bank {claimableGold} Gold before the dangerous floor.";
                }

                return $"Priority Tab: Mission first, {missionClaims} reward claim{(missionClaims == 1 ? "" : "s")} ready.";
            }

            int enhanceCount = GetEnhanceBadgeCount(profile, baseUpgradeCost);
            if (enhanceCount > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"Priority Tab: Enhance first, buy Defense or HP before the dangerous floor.";
                }

                if (threat.Contains("even"))
                {
                    return $"Priority Tab: Enhance first, Attack can flip the even floor.";
                }

                return $"Priority Tab: Enhance first, {enhanceCount} boost{(enhanceCount == 1 ? "" : "s")} affordable.";
            }

            int equipmentCount = GetEquipmentBadgeCount(profile);
            if (equipmentCount > 0)
            {
                if (threat.Contains("dangerous"))
                {
                    return $"Priority Tab: Equipment first, armor up before the dangerous floor.";
                }

                return $"Priority Tab: Equipment first, {equipmentCount} gear swap{(equipmentCount == 1 ? "" : "s")} ready.";
            }

            if (threat.Contains("dangerous"))
            {
                return $"Priority Tab: Battle only after prep, floor {profile.HighestFloor + 1} looks dangerous.";
            }

            if (threat.Contains("even"))
            {
                return $"Priority Tab: Battle next, floor {profile.HighestFloor + 1} looks even.";
            }

            return $"Priority Tab: Battle next, floor {profile.HighestFloor + 1} is the clean push.";
        }

        public static string BuildPrepAdviceText(PlayerProfile profile, int baseUpgradeCost)
        {
            if (profile == null)
            {
                return "Prep Advice: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            BattleUnitStats currentStats = PlayerBattleStatsFactory.CreatePreview(profile);
            CandidateAdvice best = BuildBestCandidate(profile, baseUpgradeCost, enemyStats, currentStats);

            if (best.Label == null)
            {
                return $"Prep Advice: current build is {TrimThreat(BattleEncounterAdvisor.BuildThreatText(currentStats, enemyStats))}; push when ready.";
            }

            if (string.Equals(best.BeforeThreat, best.AfterThreat, StringComparison.Ordinal))
            {
                return $"Prep Advice: {best.Label} keeps floor {nextFloor} at {best.AfterThreat} while adding {best.Detail}.";
            }

            return $"Prep Advice: {best.Label} shifts floor {nextFloor} from {best.BeforeThreat} to {best.AfterThreat} ({best.Detail}).";
        }

        public static string BuildBattlePlanText(PlayerProfile profile, int baseUpgradeCost, DateTime now)
        {
            if (profile == null)
            {
                return "Battle Plan: unavailable.";
            }

            int nextFloor = profile.HighestFloor + 1;
            int missionClaims = GetClaimableMissionCount(profile, now);
            CandidateAdvice best = BuildBestCandidate(
                profile,
                baseUpgradeCost,
                BattleEncounterAdvisor.CreateEnemyPreview(nextFloor),
                PlayerBattleStatsFactory.CreatePreview(profile));

            string prepStep = best.Label != null
                ? $"{best.Label} for {best.PlanDetail}, then challenge floor {nextFloor}."
                : $"challenge floor {nextFloor}.";

            if (missionClaims > 0)
            {
                return $"Battle Plan: claim {missionClaims} reward {(missionClaims == 1 ? "step" : "steps")}, {prepStep}";
            }

            if (profile.PendingIdleRewardGold > 0)
            {
                return $"Battle Plan: collect {profile.PendingIdleRewardGold} idle Gold, {prepStep}";
            }

            return $"Battle Plan: build is ready, challenge floor {nextFloor} now.";
        }

        private static string GetNextFloorThreat(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Threat: unknown";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats playerStats = PlayerBattleStatsFactory.CreatePreview(profile);
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            return BattleEncounterAdvisor.BuildThreatText(playerStats, enemyStats);
        }

        private static CandidateAdvice BuildBestCandidate(PlayerProfile profile, int baseUpgradeCost, BattleUnitStats enemyStats, BattleUnitStats currentStats)
        {
            string currentThreat = TrimThreat(BattleEncounterAdvisor.BuildThreatText(currentStats, enemyStats));
            float currentScore = ScoreThreat(currentStats, enemyStats);
            CandidateAdvice best = default;

            EvaluateCandidate(
                ref best,
                "Attack upgrade",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.AttackUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Attack)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateCandidate(
                ref best,
                "Defense upgrade",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.DefenseUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Defense)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateCandidate(
                ref best,
                "HP upgrade",
                profile.Gold >= GetUpgradeCost(baseUpgradeCost, profile.HpUpgradeLevel)
                    ? PlayerBattleStatsFactory.CreatePreviewAfterUpgrade(profile, UpgradeType.Hp)
                    : null,
                currentStats,
                currentThreat,
                currentScore,
                enemyStats);

            EvaluateEquipmentCandidate(ref best, "Equip Iron Sword", profile.HasEquipment("equip_iron_sword") && profile.EquippedWeaponId != "equip_iron_sword"
                ? PlayerBattleStatsFactory.CreatePreviewWithEquipment(profile, "equip_iron_sword", null, null)
                : null, currentStats, currentThreat, currentScore, enemyStats);
            EvaluateEquipmentCandidate(ref best, "Equip Bone Mail", profile.HasEquipment("equip_bone_mail") && profile.EquippedArmorId != "equip_bone_mail"
                ? PlayerBattleStatsFactory.CreatePreviewWithEquipment(profile, null, "equip_bone_mail", null)
                : null, currentStats, currentThreat, currentScore, enemyStats);
            EvaluateEquipmentCandidate(ref best, "Equip Quick Charm", profile.HasEquipment("equip_quick_charm") && profile.EquippedAccessoryId != "equip_quick_charm"
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
                return "preview unavailable";
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
                return "a stronger build";
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
                return "a steadier matchup";
            }

            if (parts.Count == 1)
            {
                return parts[0];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
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
                return "no stat shift";
            }

            return string.Join(", ", parts);
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
