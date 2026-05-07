using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;

namespace WitchTower.Managers
{
    public sealed class MasterDataManager : MonoBehaviour
    {
        public static MasterDataManager Instance { get; private set; }

        private const string DefaultMasterDataRootPath = "MasterData/MasterDataRoot";

        [Header("Master Data")]
        [SerializeField] private PlayerBaseDataSO playerBaseData;
        [SerializeField] private MonsterDataSO[] monsterDataList;
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
            if (floorDataList == null)
            {
                return null;
            }

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
            if (enemyDataList == null)
            {
                return null;
            }

            foreach (var enemyData in enemyDataList)
            {
                if (enemyData != null && enemyData.enemyId == enemyId)
                {
                    return enemyData;
                }
            }

            return null;
        }

        public MonsterDataSO GetMonsterData(string monsterId)
        {
            if (monsterDataList == null)
            {
                return null;
            }

            foreach (var monsterData in monsterDataList)
            {
                if (monsterData != null && monsterData.monsterId == monsterId)
                {
                    return monsterData;
                }
            }

            return null;
        }

        public MonsterDataSO[] GetAllMonsterData()
        {
            return monsterDataList;
        }

        public MonsterDataSO GetFusionResult(string firstMonsterId, string secondMonsterId)
        {
            if (monsterDataList == null || string.IsNullOrEmpty(firstMonsterId) || string.IsNullOrEmpty(secondMonsterId))
            {
                return null;
            }

            foreach (var monsterData in monsterDataList)
            {
                if (monsterData == null || monsterData.fusionRecipes == null)
                {
                    continue;
                }

                foreach (var recipe in monsterData.fusionRecipes)
                {
                    if (recipe == null || string.IsNullOrEmpty(recipe.parentMonsterIdA) || string.IsNullOrEmpty(recipe.parentMonsterIdB))
                    {
                        continue;
                    }

                    bool directMatch =
                        recipe.parentMonsterIdA == firstMonsterId &&
                        recipe.parentMonsterIdB == secondMonsterId;

                    bool reverseMatch =
                        recipe.ignoreOrder &&
                        recipe.parentMonsterIdA == secondMonsterId &&
                        recipe.parentMonsterIdB == firstMonsterId;

                    if (directMatch || reverseMatch)
                    {
                        return monsterData;
                    }
                }
            }

            if (MonsterFusionCatalog.TryResolveRecipe(firstMonsterId, secondMonsterId, out MonsterFusionRecipeDefinition catalogRecipe, true))
            {
                MonsterDataSO catalogResult = GetMonsterData(catalogRecipe.ResultMonsterId);
                if (catalogResult != null)
                {
                    return catalogResult;
                }
            }

            MonsterDataSO firstMonsterData = GetMonsterData(firstMonsterId);
            MonsterDataSO secondMonsterData = GetMonsterData(secondMonsterId);
            if (MonsterFusionCatalog.TryResolveNormalRecipe(firstMonsterData, secondMonsterData, monsterDataList, out _, out MonsterDataSO normalFusionResult))
            {
                return normalFusionResult;
            }

            return null;
        }

        public SkillDataSO GetSkillData(string skillId)
        {
            if (skillDataList == null)
            {
                return null;
            }

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
            if (equipmentDataList == null)
            {
                return null;
            }

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
            if (dropTableDataList == null)
            {
                return null;
            }

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
            monsterDataList = root.monsterDataList;
            enemyDataList = root.enemyDataList;
            skillDataList = root.skillDataList;
            equipmentDataList = root.equipmentDataList;
            floorDataList = root.floorDataList;
            dropTableDataList = root.dropTableDataList;
        }
    }
}
