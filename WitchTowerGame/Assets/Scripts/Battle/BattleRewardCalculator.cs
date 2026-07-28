using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class BattleRewardCalculator
    {
        private const int GoldRewardMultiplier = 2;

        public static BattleRewardResult Calculate(int floor, int highestClearedFloor)
        {
            return Calculate(floor, highestClearedFloor, BattleSpiritModifier.Identity);
        }

        public static BattleRewardResult Calculate(int floor, int highestClearedFloor, BattleSpiritModifier spiritModifier)
        {
            var masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            EnemyDataSO enemyData = BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(floor, masterDataManager);
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;

            if (enemyData == null)
            {
                return new BattleRewardResult(
                    ApplyGoldRewardMultiplier(10, spiritModifier, profile),
                    ApplyExpRewardMultiplier(5, spiritModifier, profile));
            }

            var gold = enemyData.rewardGold;
            if (floor > highestClearedFloor)
            {
                gold += ResolveFirstClearRewardGold(floor);
            }

            return new BattleRewardResult(
                ApplyGoldRewardMultiplier(gold, spiritModifier, profile),
                ApplyExpRewardMultiplier(enemyData.rewardExp, spiritModifier, profile));
        }

        private static int ApplyGoldRewardMultiplier(int gold, BattleSpiritModifier spiritModifier, PlayerProfile profile)
        {
            float rebirthMultiplier = profile != null ? profile.GetGoldRewardMultiplier() : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, gold) * GoldRewardMultiplier * spiritModifier.GoldRewardMultiplier * rebirthMultiplier));
        }

        private static int ApplyExpRewardMultiplier(int exp, BattleSpiritModifier spiritModifier, PlayerProfile profile)
        {
            float rebirthMultiplier = profile != null ? profile.GetExpRewardMultiplier() : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, exp) * spiritModifier.ExpRewardMultiplier * rebirthMultiplier));
        }

        private static int ResolveFirstClearRewardGold(int floor)
        {
            return 5 + Mathf.Max(1, floor) * 2;
        }
    }
}
