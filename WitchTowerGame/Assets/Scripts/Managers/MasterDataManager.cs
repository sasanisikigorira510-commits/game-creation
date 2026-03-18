using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Managers
{
    public sealed class MasterDataManager : MonoBehaviour
    {
        public static MasterDataManager Instance { get; private set; }

        private const string DefaultMasterDataRootPath = "MasterData/MasterDataRoot";

        [Header("Master Data")]
        [SerializeField] private PlayerBaseDataSO playerBaseData;
        [SerializeField] private EnemyDataSO[] enemyDataList;
        [SerializeField] private SkillDataSO[] skillDataList;
        [SerializeField] private EquipmentDataSO[] equipmentDataList;
        [SerializeField] private FloorDataSO[] floorDataList;
        [SerializeField] private DropTableDataSO[] dropTableDataList;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            if (playerBaseData != null || floorDataList != null && floorDataList.Length > 0)
            {
                return;
            }

            var root = Resources.Load<MasterDataRoot>(DefaultMasterDataRootPath);
            if (root == null)
            {
                Debug.LogWarning($"MasterDataRoot not found at Resources/{DefaultMasterDataRootPath}.");
                return;
            }

            ApplyRoot(root);
        }

        public PlayerBaseDataSO GetPlayerBaseData()
        {
            return playerBaseData;
        }

        public FloorDataSO GetFloorData(int floorNumber)
        {
            foreach (var floorData in floorDataList)
            {
                if (floorData != null && floorData.floorNumber == floorNumber)
                {
                    return floorData;
                }
            }

            return null;
        }

        public EnemyDataSO GetEnemyData(string enemyId)
        {
            foreach (var enemyData in enemyDataList)
            {
                if (enemyData != null && enemyData.enemyId == enemyId)
                {
                    return enemyData;
                }
            }

            return null;
        }

        public SkillDataSO GetSkillData(string skillId)
        {
            foreach (var skillData in skillDataList)
            {
                if (skillData != null && skillData.skillId == skillId)
                {
                    return skillData;
                }
            }

            return null;
        }

        public EquipmentDataSO GetEquipmentData(string equipmentId)
        {
            foreach (var equipmentData in equipmentDataList)
            {
                if (equipmentData != null && equipmentData.equipmentId == equipmentId)
                {
                    return equipmentData;
                }
            }

            return null;
        }

        public DropTableDataSO GetDropTableData(string dropTableId)
        {
            foreach (var dropTableData in dropTableDataList)
            {
                if (dropTableData != null && dropTableData.dropTableId == dropTableId)
                {
                    return dropTableData;
                }
            }

            return null;
        }

        private void ApplyRoot(MasterDataRoot root)
        {
            playerBaseData = root.playerBaseData;
            enemyDataList = root.enemyDataList;
            skillDataList = root.skillDataList;
            equipmentDataList = root.equipmentDataList;
            floorDataList = root.floorDataList;
            dropTableDataList = root.dropTableDataList;
        }
    }
}
