using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "FloorData", menuName = "WitchTower/MasterData/Floor Data")]
    public sealed class FloorDataSO : ScriptableObject
    {
        public int floorNumber;
        public EnemyDataSO enemyData;
        public int firstClearRewardGold;
        public string repeatRewardTableId;
        [Range(0f, 1f)] public float monsterRecruitChance = 0.35f;
        public string[] recruitableMonsterIds;
    }
}
