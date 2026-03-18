using UnityEngine;
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

            if (enemyData == null)
            {
                return new BattleRewardResult(10, 5);
            }

            var gold = enemyData.rewardGold;
            if (floorData != null && floor > highestClearedFloor)
            {
                gold += floorData.firstClearRewardGold;
            }

            return new BattleRewardResult(gold, enemyData.rewardExp);
        }
    }
}
