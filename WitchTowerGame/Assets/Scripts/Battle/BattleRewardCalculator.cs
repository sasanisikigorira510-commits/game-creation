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
            masterDataManager?.Initialize();
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            EnemyDataSO enemyData = BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(floor, masterDataManager);
            if (enemyData == null)
            {
                enemyData = floorData != null ? floorData.enemyData : null;
            }

            if (enemyData == null)
            {
                return new BattleRewardResult(10, 5);
            }

            var gold = enemyData.rewardGold;
            if (floor > highestClearedFloor)
            {
                gold += ResolveFirstClearRewardGold(floorData, floor);
            }

            return new BattleRewardResult(gold, enemyData.rewardExp);
        }

        private static int ResolveFirstClearRewardGold(FloorDataSO floorData, int floor)
        {
            if (floorData != null && floorData.firstClearRewardGold > 0)
            {
                return floorData.firstClearRewardGold;
            }

            return 5 + Mathf.Max(1, floor) * 2;
        }
    }
}
