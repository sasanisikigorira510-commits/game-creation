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

        var equipment = new List<EquipmentDataSO>
        {
            CreateEquipment("equip_bronze_blade", "青銅の刃", EquipmentSlotType.Weapon, 3, 0, 0, 0, 0, 0.01f, 0.02f, EquipmentRarity.Common),
            CreateEquipment("equip_guard_cloth", "守護の布鎧", EquipmentSlotType.Armor, 0, 0, 2, 0, 8, 0f, 0f, EquipmentRarity.Common),
            CreateEquipment("equip_ashen_ring", "灰燼の指輪", EquipmentSlotType.Accessory, 5, 0, 0, 0, 0, 0.02f, 0.03f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_apprentice_charm", "見習いの護符", EquipmentSlotType.Accessory, 0, 2, 0, 2, 4, 0.01f, 0.02f, EquipmentRarity.Common),
            CreateEquipment("equip_iron_sword", "鉄の剣", EquipmentSlotType.Weapon, 6, 0, 0, 0, 0, 0.02f, 0.03f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_bone_mail", "骨の鎧", EquipmentSlotType.Armor, 0, 0, 4, 0, 13, 0f, 0f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_sage_ring", "賢者の指輪", EquipmentSlotType.Accessory, 0, 4, 0, 3, 6, 0.02f, 0.03f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_quick_charm", "俊足のお守り", EquipmentSlotType.Accessory, 2, 0, 1, 0, 6, 0.03f, 0.04f, EquipmentRarity.Rare),
            CreateEquipment("equip_iron_saber", "鉄のサーベル", EquipmentSlotType.Weapon, 9, 0, 0, 0, 0, 0.03f, 0.04f, EquipmentRarity.Rare),
            CreateEquipment("equip_bastion_mail", "城塞の鎧", EquipmentSlotType.Armor, 0, 0, 6, 0, 18, 0f, 0f, EquipmentRarity.Rare),
            CreateEquipment("equip_barrier_talisman", "結界の護符", EquipmentSlotType.Accessory, 0, 6, 0, 5, 10, 0.03f, 0.04f, EquipmentRarity.Rare),
            CreateEquipment("equip_moon_charm", "月影のお守り", EquipmentSlotType.Accessory, 0, 0, 0, 0, 4, 0.01f, 0.02f, EquipmentRarity.Common),
            CreateEquipment("equip_frost_greatsword", "冬晶の大剣", EquipmentSlotType.Weapon, 12, 0, 0, 0, 0, 0.04f, 0.05f, EquipmentRarity.Epic),
            CreateEquipment("equip_ice_dragon_armor", "氷竜の鎧", EquipmentSlotType.Armor, 0, 0, 8, 0, 24, 0f, 0f, EquipmentRarity.Epic),
            CreateEquipment("equip_ice_star_talisman", "星氷の護符", EquipmentSlotType.Accessory, 0, 4, 0, 3, 8, 0.04f, 0.05f, EquipmentRarity.Epic),
            CreateEquipment("equip_oracle_orb", "星詠みの宝珠", EquipmentSlotType.Accessory, 0, 8, 0, 6, 14, 0.04f, 0.05f, EquipmentRarity.Epic)
        };
        equipment.AddRange(CreateClassMagicEquipmentSets());

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
        root.equipmentDataList = equipment.ToArray();
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

    [MenuItem("Tools/MCP/Add Class Magic Equipment Sets")]
    public static void AddClassMagicEquipmentSets()
    {
        EnsureFolders();
        EquipmentDataSO[] equipment = CreateClassMagicEquipmentSets();
        MasterDataRoot root = CreateOrReplaceAsset<MasterDataRoot>(ResourceFolder + "/MasterDataRoot.asset");
        root.equipmentDataList = LoadAssetsInFolder<EquipmentDataSO>(EquipmentFolder);

        foreach (EquipmentDataSO equipmentData in equipment)
        {
            EditorUtility.SetDirty(equipmentData);
        }

        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static EquipmentDataSO[] CreateClassMagicEquipmentSets()
    {
        return new[]
        {
            CreateEquipment("equip_c1_arcane_wand", "魔導の小杖", EquipmentSlotType.Weapon, 0, 4, 0, 0, 0, 0.01f, 0.01f, EquipmentRarity.Common),
            CreateEquipment("equip_c1_spellguard_robe", "術守のローブ", EquipmentSlotType.Armor, 0, 0, 0, 3, 7, 0f, 0f, EquipmentRarity.Common),
            CreateEquipment("equip_c1_mana_brooch", "魔力のブローチ", EquipmentSlotType.Accessory, 0, 2, 0, 2, 4, 0f, 0.01f, EquipmentRarity.Common),

            CreateEquipment("equip_c2_runic_staff", "刻印の魔杖", EquipmentSlotType.Weapon, 0, 7, 0, 0, 0, 0.02f, 0.02f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_c2_sage_mantle", "賢者の法衣", EquipmentSlotType.Armor, 0, 0, 0, 6, 13, 0f, 0f, EquipmentRarity.Uncommon),
            CreateEquipment("equip_c2_runic_ring", "刻印の指輪", EquipmentSlotType.Accessory, 0, 4, 0, 4, 7, 0.02f, 0.01f, EquipmentRarity.Uncommon),

            CreateEquipment("equip_c3_astral_scepter", "星界の王笏", EquipmentSlotType.Weapon, 0, 11, 0, 0, 0, 0.03f, 0.03f, EquipmentRarity.Rare),
            CreateEquipment("equip_c3_aurora_robe", "極光の霊装", EquipmentSlotType.Armor, 0, 0, 2, 9, 19, 0f, 0f, EquipmentRarity.Rare),
            CreateEquipment("equip_c3_starseer_charm", "星詠みの護符", EquipmentSlotType.Accessory, 0, 7, 0, 6, 11, 0.03f, 0.03f, EquipmentRarity.Rare),

            CreateEquipment("equip_c4_abyss_grimoire", "深淵の魔導書", EquipmentSlotType.Weapon, 0, 16, 0, 0, 0, 0.05f, 0.04f, EquipmentRarity.Epic),
            CreateEquipment("equip_c4_voidweave_raiment", "虚空織の神衣", EquipmentSlotType.Armor, 0, 0, 4, 13, 28, 0f, 0f, EquipmentRarity.Epic),
            CreateEquipment("equip_c4_eclipse_core", "蝕星の魔核", EquipmentSlotType.Accessory, 0, 10, 0, 9, 17, 0.05f, 0.05f, EquipmentRarity.Epic)
        };
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
        int baseWisdom,
        int baseDefense,
        int baseMagicDefense,
        int baseHp,
        float bonusCritRate,
        float bonusAttackSpeed,
        EquipmentRarity rarity)
    {
        EquipmentDataSO equipment = CreateOrReplaceAsset<EquipmentDataSO>(EquipmentFolder + "/" + equipmentId + ".asset");
        equipment.equipmentId = equipmentId;
        equipment.equipmentName = equipmentName;
        equipment.slotType = slotType;
        equipment.baseAttack = Mathf.Max(0, baseAttack);
        equipment.baseWisdom = Mathf.Max(0, baseWisdom);
        equipment.baseDefense = Mathf.Max(0, baseDefense);
        equipment.baseMagicDefense = Mathf.Max(0, baseMagicDefense);
        equipment.baseHp = Mathf.Max(0, baseHp);
        equipment.bonusCritRate = Mathf.Max(0f, bonusCritRate);
        equipment.bonusAttackSpeed = Mathf.Max(0f, bonusAttackSpeed);
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
