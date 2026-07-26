using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public static class BattleEncounterAdvisor
    {
        public readonly struct BattleEncounterAssessment
        {
            public BattleEncounterAssessment(
                string threatText,
                int recommendedPartyLevel,
                float partyLevel,
                int partyCount,
                bool needsEquipment,
                int recommendedCombatPower,
                int partyCombatPower,
                float estimatedClearSeconds,
                float estimatedSurvivalSeconds)
            {
                ThreatText = threatText ?? "Threat: unknown";
                RecommendedPartyLevel = Mathf.Max(1, recommendedPartyLevel);
                PartyLevel = Mathf.Max(0f, partyLevel);
                PartyCount = Mathf.Max(0, partyCount);
                NeedsEquipment = needsEquipment;
                RecommendedCombatPower = Mathf.Max(1, recommendedCombatPower);
                PartyCombatPower = Mathf.Max(0, partyCombatPower);
                EstimatedClearSeconds = Mathf.Max(0f, estimatedClearSeconds);
                EstimatedSurvivalSeconds = Mathf.Max(0f, estimatedSurvivalSeconds);
            }

            public string ThreatText { get; }
            public int RecommendedPartyLevel { get; }
            public float PartyLevel { get; }
            public int PartyCount { get; }
            public bool NeedsEquipment { get; }
            public int RecommendedCombatPower { get; }
            public int PartyCombatPower { get; }
            public float EstimatedClearSeconds { get; }
            public float EstimatedSurvivalSeconds { get; }
        }

        private sealed class PartyCombatantPreview
        {
            public BattleUnitStats Stats;
            public MonsterDataSO MonsterData;
            public OwnedMonsterData OwnedMonster;
            public int Level;
        }

        public static BattleUnitStats CreateEnemyPreview(int floor)
        {
            var masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            EnemyDataSO enemyData = BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(floor, masterDataManager);
            if (enemyData == null)
            {
                enemyData = floorData != null ? floorData.enemyData : null;
            }

            if (enemyData == null)
            {
                return new BattleUnitStats
                {
                    MaxHp = 40,
                    CurrentHp = 40,
                    Attack = 8,
                    Wisdom = 8,
                    Defense = 2,
                    MagicDefense = 2,
                    AttackSpeed = 0.8f,
                    CritRate = 0.03f,
                    CritDamage = 1.3f
                };
            }

            EnemyTraitRuntime runtime = EnemyTraitResolver.Resolve(enemyData.enemyTrait);
            return new BattleUnitStats
            {
                MaxHp = enemyData.maxHp,
                CurrentHp = enemyData.maxHp,
                Attack = Mathf.RoundToInt(enemyData.attack * runtime.AttackMultiplier),
                Wisdom = Mathf.RoundToInt(Mathf.Max(enemyData.magicAttack, enemyData.attack) * runtime.AttackMultiplier),
                Defense = enemyData.defense + runtime.DefenseBonus,
                MagicDefense = enemyData.magicDefense + runtime.DefenseBonus,
                AttackSpeed = enemyData.attackSpeed * runtime.AttackSpeedMultiplier,
                CritRate = enemyData.critRate + runtime.CritRateBonus,
                CritDamage = enemyData.critDamage
            };
        }

        public static BattleEncounterAssessment AssessFloor(PlayerProfile profile, int floor)
        {
            int safeFloor = Mathf.Max(1, floor);
            var masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            BattleUnitStats enemyStats = CreateEnemyPreview(safeFloor);
            EnemyDataSO enemyData = BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(safeFloor, masterDataManager);
            if (enemyData == null)
            {
                FloorDataSO floorData = masterDataManager != null ? masterDataManager.GetFloorData(safeFloor) : null;
                enemyData = floorData != null ? floorData.enemyData : null;
            }

            int enemyCount = Mathf.Max(1, BattleDungeonCatalog.ResolveEnemyCount(safeFloor));
            int recommendedPartyLevel = ResolveRecommendedPartyLevel(safeFloor);
            int recommendedCombatPower = ResolveRecommendedCombatPower(enemyStats, enemyCount, safeFloor);
            List<PartyCombatantPreview> party = BuildPartyCombatants(profile);
            float partyLevel = ResolvePartyLevel(profile, party);
            int partyCombatPower = ResolvePartyCombatPower(party);
            bool needsEquipment = ResolveNeedsEquipment(profile, party, safeFloor, partyCombatPower, recommendedCombatPower);

            if (enemyStats == null || party.Count == 0)
            {
                return new BattleEncounterAssessment(
                    "Threat: unknown",
                    recommendedPartyLevel,
                    partyLevel,
                    party.Count,
                    needsEquipment,
                    recommendedCombatPower,
                    partyCombatPower,
                    0f,
                    0f);
            }

            MonsterDamageType enemyDamageType = enemyData != null ? enemyData.damageType : MonsterDamageType.Physical;
            float partyDamagePerSecond = 0f;
            float totalPartyHp = 0f;
            float totalEnemyDamagePerHit = 0f;
            int validCombatantCount = 0;
            for (int i = 0; i < party.Count; i += 1)
            {
                PartyCombatantPreview combatant = party[i];
                if (combatant == null || combatant.Stats == null)
                {
                    continue;
                }

                MonsterDamageType allyDamageType = combatant.MonsterData != null
                    ? combatant.MonsterData.damageType
                    : MonsterDamageType.Physical;
                partyDamagePerSecond += EstimateDamagePerSecond(combatant.Stats, enemyStats, allyDamageType);
                totalPartyHp += Mathf.Max(1, combatant.Stats.MaxHp);
                totalEnemyDamagePerHit += EstimateDamagePerHit(enemyStats, combatant.Stats, enemyDamageType);
                validCombatantCount += 1;
            }

            if (validCombatantCount <= 0)
            {
                return new BattleEncounterAssessment(
                    "Threat: unknown",
                    recommendedPartyLevel,
                    partyLevel,
                    party.Count,
                    needsEquipment,
                    recommendedCombatPower,
                    partyCombatPower,
                    0f,
                    0f);
            }

            float enemyPressure = ResolveEnemyPressure(enemyCount, validCombatantCount, safeFloor);
            float averageEnemyDamagePerHit = totalEnemyDamagePerHit / validCombatantCount;
            float enemyDamagePerSecond = averageEnemyDamagePerHit * Mathf.Max(0.2f, enemyStats.AttackSpeed) * enemyPressure;
            float estimatedClearSeconds = enemyStats.MaxHp * enemyCount / Mathf.Max(1f, partyDamagePerSecond);
            float estimatedSurvivalSeconds = totalPartyHp / Mathf.Max(1f, enemyDamagePerSecond);
            recommendedCombatPower = Mathf.Max(
                recommendedCombatPower,
                ResolvePerformanceRecommendedCombatPower(partyCombatPower, estimatedClearSeconds, estimatedSurvivalSeconds));
            needsEquipment = ResolveNeedsEquipment(profile, party, safeFloor, partyCombatPower, recommendedCombatPower);
            string threatText = ResolveAssessmentThreatText(
                partyLevel,
                recommendedPartyLevel,
                partyCombatPower,
                recommendedCombatPower,
                estimatedClearSeconds,
                estimatedSurvivalSeconds,
                needsEquipment,
                safeFloor);

            return new BattleEncounterAssessment(
                threatText,
                recommendedPartyLevel,
                partyLevel,
                validCombatantCount,
                needsEquipment,
                recommendedCombatPower,
                partyCombatPower,
                estimatedClearSeconds,
                estimatedSurvivalSeconds);
        }

        public static int CalculateCombatPower(BattleUnitStats stats)
        {
            if (stats == null)
            {
                return 0;
            }

            float attackSpeed = Mathf.Max(0.2f, stats.AttackSpeed);
            float critMultiplier = 1f + Mathf.Clamp01(stats.CritRate) * (Mathf.Max(1f, stats.CritDamage) - 1f);
            float offense = Mathf.Max(stats.Attack, stats.Wisdom) * attackSpeed * critMultiplier * 10f;
            float durability = Mathf.Max(1, stats.MaxHp) * 0.8f + (Mathf.Max(0, stats.Defense) + Mathf.Max(0, stats.MagicDefense)) * 4f;
            float tempo = attackSpeed * 18f;
            return Mathf.Max(1, Mathf.RoundToInt(offense + durability + tempo));
        }

        public static string BuildThreatText(BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            if (playerStats == null || enemyStats == null)
            {
                return "Threat: unknown";
            }

            float playerScore = playerStats.MaxHp + playerStats.Attack * 4f + playerStats.Defense * 3f + playerStats.CritRate * 100f;
            float enemyScore = enemyStats.MaxHp + enemyStats.Attack * 4f + enemyStats.Defense * 3f + enemyStats.CritRate * 100f;
            float ratio = enemyScore / Mathf.Max(1f, playerScore);

            if (ratio >= 1.1f)
            {
                return "Threat: dangerous matchup";
            }

            if (ratio >= 0.85f)
            {
                return "Threat: even fight";
            }

            return "Threat: favorable push";
        }

        public static Color GetThreatColor(string threatLabel)
        {
            if (string.IsNullOrEmpty(threatLabel))
            {
                return new Color(0.97f, 0.82f, 0.55f, 0.98f);
            }

            if (threatLabel.Contains("dangerous"))
            {
                return new Color(0.96f, 0.47f, 0.47f, 0.98f);
            }

            if (threatLabel.Contains("even"))
            {
                return new Color(0.97f, 0.82f, 0.55f, 0.98f);
            }

            return new Color(0.50f, 0.90f, 0.69f, 0.98f);
        }

        private static List<PartyCombatantPreview> BuildPartyCombatants(PlayerProfile profile)
        {
            var combatants = new List<PartyCombatantPreview>();
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsters(profile, 5);
            for (int i = 0; i < partyMonsters.Count; i += 1)
            {
                OwnedMonsterData ownedMonster = partyMonsters[i];
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.MonsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = masterDataManager != null
                    ? masterDataManager.GetMonsterData(ownedMonster.MonsterId)
                    : null;
                BattleUnitStats stats = MonsterBattleStatsFactory.Create(profile, ownedMonster, monsterData);
                if (stats == null)
                {
                    continue;
                }

                combatants.Add(new PartyCombatantPreview
                {
                    Stats = stats,
                    MonsterData = monsterData,
                    OwnedMonster = ownedMonster,
                    Level = Mathf.Max(1, ownedMonster.Level)
                });
            }

            if (combatants.Count > 0)
            {
                return combatants;
            }

            BattleUnitStats fallbackStats = PlayerBattleStatsFactory.CreatePreview(profile);
            if (fallbackStats != null)
            {
                combatants.Add(new PartyCombatantPreview
                {
                    Stats = fallbackStats,
                    MonsterData = null,
                    OwnedMonster = null,
                    Level = Mathf.Max(1, profile != null ? profile.Level : 1)
                });
            }

            return combatants;
        }

        private static float ResolvePartyLevel(PlayerProfile profile, List<PartyCombatantPreview> party)
        {
            if (party == null || party.Count <= 0)
            {
                return profile != null ? Mathf.Max(1, profile.Level) : 0f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 0; i < party.Count; i += 1)
            {
                if (party[i] == null)
                {
                    continue;
                }

                total += Mathf.Max(1, party[i].Level);
                count += 1;
            }

            return count > 0 ? total / count : 0f;
        }

        private static bool ResolveNeedsEquipment(
            PlayerProfile profile,
            List<PartyCombatantPreview> party,
            int floor,
            int partyCombatPower,
            int recommendedCombatPower)
        {
            if (profile == null || party == null || party.Count <= 0 || floor < 6)
            {
                return false;
            }

            int expectedCoreSlots = 0;
            int equippedCoreSlots = 0;
            for (int i = 0; i < party.Count; i += 1)
            {
                OwnedMonsterData monster = party[i]?.OwnedMonster;
                if (monster == null)
                {
                    continue;
                }

                expectedCoreSlots += 2;
                equippedCoreSlots += string.IsNullOrEmpty(monster.EquippedWeaponInstanceId) ? 0 : 1;
                equippedCoreSlots += string.IsNullOrEmpty(monster.EquippedArmorInstanceId) ? 0 : 1;
            }

            if (expectedCoreSlots <= 0)
            {
                return false;
            }

            bool underRecommendedPower = recommendedCombatPower > 0 && partyCombatPower < recommendedCombatPower;
            return equippedCoreSlots < expectedCoreSlots || underRecommendedPower;
        }

        private static int ResolveRecommendedPartyLevel(int floor)
        {
            int safeFloor = Mathf.Max(1, floor);
            int dungeonIndex = Mathf.Max(0, (safeFloor - 1) / 5);
            int localFloor = BattleDungeonCatalog.ResolveLocalFloor(safeFloor);
            if (dungeonIndex <= 0)
            {
                return Mathf.Max(1, 1 + (localFloor - 1) * 2);
            }

            if (dungeonIndex == 1)
            {
                int[] recommendations = { 12, 15, 20, 22, 24 };
                return recommendations[Mathf.Clamp(localFloor - 1, 0, recommendations.Length - 1)];
            }

            return Mathf.Max(24, 24 + (safeFloor - 10) * 3);
        }

        private static float EstimateDamagePerSecond(BattleUnitStats attacker, BattleUnitStats defender, MonsterDamageType damageType)
        {
            return EstimateDamagePerHit(attacker, defender, damageType) * Mathf.Max(0.2f, attacker != null ? attacker.AttackSpeed : 1f);
        }

        private static float EstimateDamagePerHit(BattleUnitStats attacker, BattleUnitStats defender, MonsterDamageType damageType)
        {
            int offenseValue = damageType == MonsterDamageType.Magic
                ? Mathf.Max(1, attacker != null ? attacker.Wisdom : 0)
                : Mathf.Max(1, attacker != null ? attacker.Attack : 0);
            int defenseValue = 0;
            if (defender != null)
            {
                defenseValue = damageType == MonsterDamageType.Magic
                    ? defender.MagicDefense
                    : defender.Defense;
            }

            int baseDamage = Mathf.Max(1, Mathf.RoundToInt(offenseValue * (100f / (100f + Mathf.Max(0, defenseValue)))));
            float critRate = Mathf.Clamp01(attacker != null ? attacker.CritRate : 0f);
            float critDamage = Mathf.Max(1f, attacker != null ? attacker.CritDamage : 1.5f);
            return baseDamage * (1f + critRate * (critDamage - 1f));
        }

        private static float ResolveEnemyPressure(int enemyCount, int partyCount, int floor)
        {
            float partyPressure = partyCount <= 1
                ? 1.2f
                : partyCount <= 2
                    ? 1.8f
                    : 2.8f;
            float floorPressure = Mathf.Clamp(1f + Mathf.Max(0, floor - 1) * 0.24f, 1f, 4.6f);
            float countPressure = Mathf.Sqrt(Mathf.Max(1, enemyCount));
            return Mathf.Clamp(Mathf.Min(enemyCount, Mathf.Max(partyPressure, Mathf.Min(floorPressure, countPressure))), 1f, 5f);
        }

        private static int ResolvePartyCombatPower(List<PartyCombatantPreview> party)
        {
            if (party == null || party.Count <= 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < party.Count; i += 1)
            {
                total += CalculateCombatPower(party[i]?.Stats);
            }

            return Mathf.Max(0, total);
        }

        private static int ResolveRecommendedCombatPower(BattleUnitStats enemyStats, int enemyCount, int floor)
        {
            int singleEnemyPower = CalculateCombatPower(enemyStats);
            if (singleEnemyPower <= 0)
            {
                return 1;
            }

            float countScale = 1f + Mathf.Sqrt(Mathf.Max(1, enemyCount)) * 0.34f;
            float pressureScale = ResolveEnemyPressure(enemyCount, 5, floor) * 0.25f;
            float floorScale = 1f + Mathf.Max(0, floor - 1) * 0.015f;
            return Mathf.Max(1, Mathf.RoundToInt(singleEnemyPower * (countScale + pressureScale) * floorScale));
        }

        private static int ResolvePerformanceRecommendedCombatPower(int partyCombatPower, float estimatedClearSeconds, float estimatedSurvivalSeconds)
        {
            if (partyCombatPower <= 0 || estimatedClearSeconds <= 0f || estimatedSurvivalSeconds <= 0f)
            {
                return 0;
            }

            const float targetClearToSurvivalRatio = 0.76f;
            float currentRatio = estimatedClearSeconds / Mathf.Max(1f, estimatedSurvivalSeconds);
            if (currentRatio <= targetClearToSurvivalRatio)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.CeilToInt(partyCombatPower * (currentRatio / targetClearToSurvivalRatio)));
        }

        private static string ResolveAssessmentThreatText(
            float partyLevel,
            int recommendedPartyLevel,
            int partyCombatPower,
            int recommendedCombatPower,
            float estimatedClearSeconds,
            float estimatedSurvivalSeconds,
            bool needsEquipment,
            int floor)
        {
            float clearToSurvivalRatio = estimatedClearSeconds / Mathf.Max(1f, estimatedSurvivalSeconds);
            bool levelKnown = partyLevel > 0f;
            bool powerKnown = partyCombatPower > 0 && recommendedCombatPower > 0;
            float powerRatio = powerKnown ? partyCombatPower / (float)recommendedCombatPower : 1f;
            bool heavilyUnderRecommended = powerKnown
                ? powerRatio < 0.72f
                : levelKnown && partyLevel <= recommendedPartyLevel - 4;
            bool underRecommended = powerKnown
                ? powerRatio < 0.98f
                : levelKnown && partyLevel + 0.01f < recommendedPartyLevel;

            if (heavilyUnderRecommended || clearToSurvivalRatio >= 1.05f)
            {
                return "Threat: dangerous matchup";
            }

            if (underRecommended || clearToSurvivalRatio >= 0.78f || (needsEquipment && floor >= 6))
            {
                return "Threat: even fight";
            }

            return "Threat: favorable push";
        }
    }
}
