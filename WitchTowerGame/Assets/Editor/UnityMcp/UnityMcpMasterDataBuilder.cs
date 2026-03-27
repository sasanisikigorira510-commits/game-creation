using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WitchTower.MasterData;

public static class UnityMcpMasterDataBuilder
{
    private const string PlayerFolder = "Assets/MasterData/Player";
    private const string MonsterFolder = "Assets/MasterData/Monster";
    private const string EnemyFolder = "Assets/MasterData/Enemy";
    private const string SkillFolder = "Assets/MasterData/Skill";
    private const string EquipmentFolder = "Assets/MasterData/Equipment";
    private const string FloorFolder = "Assets/MasterData/Floor";
    private const string DropTableFolder = "Assets/MasterData/DropTable";
    private const string ResourceFolder = "Assets/Resources/MasterData";

    [MenuItem("Tools/MCP/Rebuild Sample Master Data")]
    public static void RebuildSampleMasterData()
    {
        EnsureFolders();

        PlayerBaseDataSO playerBaseData = CreateOrReplaceAsset<PlayerBaseDataSO>(PlayerFolder + "/PlayerBaseData.asset");
        playerBaseData.initialHp = 100;
        playerBaseData.initialAttack = 15;
        playerBaseData.initialDefense = 5;
        playerBaseData.initialAttackSpeed = 1.0f;
        playerBaseData.initialCritRate = 0.05f;
        playerBaseData.initialCritDamage = 1.5f;

        DropTableDataSO commonDropTable = CreateOrReplaceAsset<DropTableDataSO>(DropTableFolder + "/drop_common_floor.asset");
        commonDropTable.dropTableId = "drop_common_floor";
        commonDropTable.minGold = 8;
        commonDropTable.maxGold = 16;
        commonDropTable.materialDrops = new[]
        {
            new MaterialDropEntry { materialId = "mat_stone_shard", amount = 1, dropRate = 0.75f },
            new MaterialDropEntry { materialId = "mat_shadow_thread", amount = 1, dropRate = 0.3f }
        };

        var enemies = new List<EnemyDataSO>
        {
            CreateEnemy("enemy_slime", "Ash Slime", 40, 8, 2, 0.8f, 0.03f, 1.3f, 10, 5, EnemyTrait.None, commonDropTable.dropTableId),
            CreateEnemy("enemy_guard", "Tower Guard", 64, 11, 5, 0.85f, 0.04f, 1.35f, 16, 8, EnemyTrait.HighDefense, commonDropTable.dropTableId),
            CreateEnemy("enemy_harpy", "Needle Harpy", 55, 12, 3, 1.2f, 0.06f, 1.4f, 20, 10, EnemyTrait.FastAttack, commonDropTable.dropTableId),
            CreateEnemy("enemy_wraith", "Hollow Wraith", 72, 13, 4, 1.0f, 0.05f, 1.45f, 24, 12, EnemyTrait.Drain, commonDropTable.dropTableId),
            CreateEnemy("enemy_knight", "Crimson Knight", 92, 16, 7, 0.95f, 0.1f, 1.6f, 30, 16, EnemyTrait.Critical, commonDropTable.dropTableId)
        };

        var skills = new[]
        {
            CreateSkill("skill_strike", "Strike", "Deal a heavy hit to the enemy.", 6f, 2f, 0f, BuffType.None, 0f, 0f),
            CreateSkill("skill_drain", "Drain", "Damage the enemy and recover some HP.", 8f, 1.2f, 0.5f, BuffType.Heal, 0.5f, 0f),
            CreateSkill("skill_guard", "Guard", "Raise defense for a short time.", 10f, 0f, 0f, BuffType.DefenseUp, 5f, 5f)
        };

        var equipment = new[]
        {
            CreateEquipment("equip_bronze_blade", "Bronze Blade", EquipmentSlotType.Weapon, 3, 0, 0, 0.01f, 0.02f, EquipmentRarity.Common),
            CreateEquipment("equip_guard_cloth", "Guard Cloth", EquipmentSlotType.Armor, 0, 2, 8, 0f, 0f, EquipmentRarity.Common),
            CreateEquipment("equip_ashen_ring", "Ashen Ring", EquipmentSlotType.Accessory, 1, 0, 0, 0.02f, 0.03f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_iron_sword", "Iron Sword", EquipmentSlotType.Weapon, 6, 0, 0, 0.02f, 0.03f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_bone_mail", "Bone Mail", EquipmentSlotType.Armor, 0, 5, 18, 0f, -0.02f, EquipmentRarity.Rare),
            CreateEquipment("equip_quick_charm", "Quick Charm", EquipmentSlotType.Accessory, 2, 1, 6, 0.03f, 0.04f, EquipmentRarity.Rare)
        };

        var floors = new List<FloorDataSO>();
        for (int floorNumber = 1; floorNumber <= 10; floorNumber++)
        {
            EnemyDataSO enemy = enemies[Mathf.Min((floorNumber - 1) / 2, enemies.Count - 1)];
            FloorDataSO floor = CreateOrReplaceAsset<FloorDataSO>(FloorFolder + "/Floor_" + floorNumber + ".asset");
            floor.floorNumber = floorNumber;
            floor.enemyData = enemy;
            floor.firstClearRewardGold = 5 + floorNumber * 2;
            floor.repeatRewardTableId = commonDropTable.dropTableId;
            floors.Add(floor);
        }

        MasterDataRoot root = CreateOrReplaceAsset<MasterDataRoot>(ResourceFolder + "/MasterDataRoot.asset");
        root.playerBaseData = playerBaseData;
        root.monsterDataList = LoadAssetsInFolder<MonsterDataSO>(MonsterFolder);
        root.enemyDataList = enemies.ToArray();
        root.skillDataList = skills;
        root.equipmentDataList = equipment;
        root.floorDataList = floors.ToArray();
        root.dropTableDataList = new[] { commonDropTable };

        EditorUtility.SetDirty(playerBaseData);
        EditorUtility.SetDirty(commonDropTable);
        foreach (EnemyDataSO enemy in enemies)
        {
            EditorUtility.SetDirty(enemy);
        }

        foreach (SkillDataSO skill in skills)
        {
            EditorUtility.SetDirty(skill);
        }

        foreach (EquipmentDataSO equipmentData in equipment)
        {
            EditorUtility.SetDirty(equipmentData);
        }

        foreach (FloorDataSO floor in floors)
        {
            EditorUtility.SetDirty(floor);
        }

        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static EnemyDataSO CreateEnemy(
        string enemyId,
        string enemyName,
        int maxHp,
        int attack,
        int defense,
        float attackSpeed,
        float critRate,
        float critDamage,
        int rewardGold,
        int rewardExp,
        EnemyTrait enemyTrait,
        string dropTableId)
    {
        EnemyDataSO enemy = CreateOrReplaceAsset<EnemyDataSO>(EnemyFolder + "/" + enemyId + ".asset");
        enemy.enemyId = enemyId;
        enemy.enemyName = enemyName;
        enemy.maxHp = maxHp;
        enemy.attack = attack;
        enemy.defense = defense;
        enemy.attackSpeed = attackSpeed;
        enemy.critRate = critRate;
        enemy.critDamage = critDamage;
        enemy.rewardGold = rewardGold;
        enemy.rewardExp = rewardExp;
        enemy.dropTableId = dropTableId;
        enemy.enemyTrait = enemyTrait;
        return enemy;
    }

    private static SkillDataSO CreateSkill(
        string skillId,
        string skillName,
        string description,
        float cooldown,
        float powerRate,
        float healRate,
        BuffType buffType,
        float buffValue,
        float buffDuration)
    {
        SkillDataSO skill = CreateOrReplaceAsset<SkillDataSO>(SkillFolder + "/" + skillId + ".asset");
        skill.skillId = skillId;
        skill.skillName = skillName;
        skill.description = description;
        skill.cooldown = cooldown;
        skill.powerRate = powerRate;
        skill.healRate = healRate;
        skill.buffType = buffType;
        skill.buffValue = buffValue;
        skill.buffDuration = buffDuration;
        return skill;
    }

    private static EquipmentDataSO CreateEquipment(
        string equipmentId,
        string equipmentName,
        EquipmentSlotType slotType,
        int baseAttack,
        int baseDefense,
        int baseHp,
        float bonusCritRate,
        float bonusAttackSpeed,
        EquipmentRarity rarity)
    {
        EquipmentDataSO equipment = CreateOrReplaceAsset<EquipmentDataSO>(EquipmentFolder + "/" + equipmentId + ".asset");
        equipment.equipmentId = equipmentId;
        equipment.equipmentName = equipmentName;
        equipment.slotType = slotType;
        equipment.baseAttack = baseAttack;
        equipment.baseDefense = baseDefense;
        equipment.baseHp = baseHp;
        equipment.bonusCritRate = bonusCritRate;
        equipment.bonusAttackSpeed = bonusAttackSpeed;
        equipment.rarity = rarity;
        return equipment;
    }

    private static T CreateOrReplaceAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    private static T[] LoadAssetsInFolder<T>(string folder) where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
        var assets = new List<T>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets.ToArray();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "MasterData");
        EnsureFolder("Assets/MasterData", "Player");
        EnsureFolder("Assets/MasterData", "Monster");
        EnsureFolder("Assets/MasterData", "Enemy");
        EnsureFolder("Assets/MasterData", "Skill");
        EnsureFolder("Assets/MasterData", "Equipment");
        EnsureFolder("Assets/MasterData", "Floor");
        EnsureFolder("Assets/MasterData", "DropTable");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "MasterData");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string folderPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
