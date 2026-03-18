using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "MasterDataRoot", menuName = "WitchTower/MasterData/Master Data Root")]
    public sealed class MasterDataRoot : ScriptableObject
    {
        public PlayerBaseDataSO playerBaseData;
        public EnemyDataSO[] enemyDataList;
        public SkillDataSO[] skillDataList;
        public EquipmentDataSO[] equipmentDataList;
        public FloorDataSO[] floorDataList;
        public DropTableDataSO[] dropTableDataList;
    }
}
