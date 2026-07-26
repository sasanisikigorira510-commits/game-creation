using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class BattleRewardCalculator
    {
        public static BattleRewardResult Calculate(int floor, int highestClearedFloor)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            EnemyDataSO enemyData = floorData != null ? floorData.enemyData : null;
            var profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;

            if (enemyData == null)
            {
                return ApplyRewardMultipliers(10, 5, profile);
            }

            var gold = enemyData.rewardGold;
            if (floorData != null && floor > highestClearedFloor)
            {
                gold += floorData.firstClearRewardGold;
            }

            return ApplyRewardMultipliers(gold, enemyData.rewardExp, profile);
        }

        private static BattleRewardResult ApplyRewardMultipliers(int gold, int exp, PlayerProfile profile)
        {
            if (profile == null)
            {
                return new BattleRewardResult(gold, exp);
            }

            return new BattleRewardResult(
                ApplyMultiplier(gold, profile.GetGoldRewardMultiplier()),
                ApplyMultiplier(exp, profile.GetExpRewardMultiplier()));
        }

        private static int ApplyMultiplier(int amount, float multiplier)
        {
            if (amount <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
        }
    }
}
