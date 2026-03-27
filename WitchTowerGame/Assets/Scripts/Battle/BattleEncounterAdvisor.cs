using UnityEngine;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class BattleEncounterAdvisor
    {
        public static BattleUnitStats CreateEnemyPreview(int floor)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            EnemyDataSO enemyData = floorData != null ? floorData.enemyData : null;

            if (enemyData == null)
            {
                return new BattleUnitStats
                {
                    MaxHp = 40,
                    CurrentHp = 40,
                    Attack = 8,
                    Defense = 2,
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
                Defense = enemyData.defense + runtime.DefenseBonus,
                AttackSpeed = enemyData.attackSpeed * runtime.AttackSpeedMultiplier,
                CritRate = enemyData.critRate + runtime.CritRateBonus,
                CritDamage = enemyData.critDamage
            };
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
    }
}
