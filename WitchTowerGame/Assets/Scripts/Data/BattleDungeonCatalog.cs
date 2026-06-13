using System.Collections.Generic;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public sealed class BattleDungeonFloorDefinition
    {
        public BattleDungeonFloorDefinition(
            int localFloor,
            string floorName,
            string enemyMonsterId,
            float recruitChance,
            int enemyCount,
            bool isBossEncounter = false)
        {
            LocalFloor = Mathf.Max(1, localFloor);
            FloorName = floorName;
            EnemyMonsterId = enemyMonsterId;
            RecruitChance = Mathf.Clamp01(recruitChance);
            EnemyCount = Mathf.Max(1, enemyCount);
            IsBossEncounter = isBossEncounter;
        }

        public int LocalFloor { get; }
        public string FloorName { get; }
        public string EnemyMonsterId { get; }
        public float RecruitChance { get; }
        public int EnemyCount { get; }
        public bool IsBossEncounter { get; }
    }

    public sealed class BattleDungeonDefinition
    {
        public BattleDungeonDefinition(
            string dungeonId,
            string dungeonName,
            string description,
            string cardResourcePath,
            string battleBackdropResourcePath,
            int globalFloorStart,
            IReadOnlyList<BattleDungeonFloorDefinition> floors)
        {
            DungeonId = dungeonId;
            DungeonName = dungeonName;
            Description = description;
            CardResourcePath = cardResourcePath;
            BattleBackdropResourcePath = battleBackdropResourcePath;
            GlobalFloorStart = Mathf.Max(1, globalFloorStart);
            Floors = floors;
        }

        public string DungeonId { get; }
        public string DungeonName { get; }
        public string Description { get; }
        public string CardResourcePath { get; }
        public string BattleBackdropResourcePath { get; }
        public int GlobalFloorStart { get; }
        public IReadOnlyList<BattleDungeonFloorDefinition> Floors { get; }
    }

    public static class BattleDungeonCatalog
    {
        private const int FloorsPerDungeon = 5;
        private const float EnemyStatScaleBase = 0.46f;
        private const float EnemyStatScalePerGlobalFloor = 0.045f;
        private const float EnemyStatScalePerDungeon = 0.17f;
        private const int RewardGoldBase = 8;
        private const int RewardGoldPerGlobalFloor = 3;
        private const int RewardGoldPerDungeon = 5;
        private const int RewardExpBase = 8;
        private const float RewardExpHpWeight = 0.16f;
        private const float RewardExpAttackWeight = 1.35f;
        private const float RewardExpDefenseWeight = 0.55f;
        private const float RewardExpAttackSpeedWeight = 10f;
        private const float RewardExpCritWeight = 80f;
        private const float RewardExpStatScoreMultiplier = 0.30f;
        private const float RewardExpPerGlobalFloor = 0.25f;
        private const int RewardExpPerDungeon = 4;
        private const float RewardExpCountBase = 0.78f;
        private const float RewardExpCountSqrtStep = 0.32f;
        private const float RewardExpBossHpMultiplier = 5.0f;
        private const float RewardExpBossAttackMultiplier = 2.0f;
        private const int RewardExpBossDefenseBonus = 8;

        private static readonly BattleDungeonDefinition[] DungeonDefinitions =
        {
            new BattleDungeonDefinition(
                "blight_cavern",
                "見習いの五門洞",
                "五つの小門が並ぶ浅層洞窟。全階層でクラス1剣士系の見習い剣士を捕獲できる。",
                "UI/DungeonSelect/DungeonCard_BlightCavern",
                "BattleBackgrounds/dungeon1_1170x2532",
                1,
                BuildDungeonFloors(
                    "monster_apprentice_swordsman",
                    0.48f,
                    6,
                    2,
                    false,
                    "木剣の入口",
                    "石床の稽古場",
                    "盾の小門",
                    "剣灯の回廊",
                    "五門の奥稽古")),
            new BattleDungeonDefinition(
                "gear_crypt",
                "中位魔獣の廃工廠",
                "炉心と鉱石炉が残る中層工廠。全階層でクラス2ゴーレム系の鉱石巨人ガルムを捕獲できる。",
                "UI/DungeonSelect/DungeonCard_GearCrypt",
                "BattleBackgrounds/dungeon2_1170x2532",
                6,
                BuildDungeonFloors(
                    "monster_ore_giant_garm",
                    0.32f,
                    18,
                    5,
                    false,
                    "錆びた搬入口",
                    "鉱石運搬路",
                    "結晶整備室",
                    "炉心採掘線",
                    "ガルムの鉱炉")),
            new BattleDungeonDefinition(
                "curse_library",
                "上位召喚の地下書庫",
                "上位個体の契約書が封じられた地下書庫。全階層でクラス3魔導士系の深淵大魔導を捕獲できる。",
                "UI/DungeonSelect/DungeonCard_CurseLibrary",
                "BattleBackgrounds/dungeon3_1170x2532",
                11,
                BuildDungeonFloors(
                    "monster_abyss_grand_mage_seraphis",
                    0.26f,
                    34,
                    7,
                    false,
                    "封印書架",
                    "召喚円の閲覧室",
                    "鎖の禁書橋",
                    "紫灯の契約庫",
                    "深淵大魔導の奥書")),
            new BattleDungeonDefinition(
                "ember_drake_pass",
                "紅蓮竜道",
                "溶岩脈に沿って続く竜の通り道。全階層でクラス4竜系の機竜ヴァルドレイクを捕獲できる。",
                "UI/DungeonSelect/DungeonCard_EmberDrakePass",
                "BattleBackgrounds/dungeon4_1170x2532",
                16,
                BuildDungeonFloors(
                    "monster_mecha_dragon_valdrake",
                    0.20f,
                    48,
                    8,
                    true,
                    "火口の入口",
                    "紅蓮の石橋",
                    "竜鱗の回廊",
                    "機竜の滑走路",
                    "紅蓮竜道の頂")),
            new BattleDungeonDefinition(
                "star_ore_citadel",
                "星鉱の巨殿",
                "星を含んだ鉱石が鳴る巨大殿堂。全階層でクラス4ゴーレム系の星蝕魔像を捕獲できる。",
                "UI/DungeonSelect/DungeonCard_StarOreCitadel",
                "BattleBackgrounds/dungeon5_1170x2532",
                21,
                BuildDungeonFloors(
                    "monster_astral_eclipse_golem",
                    0.16f,
                    62,
                    8,
                    true,
                    "星鉱の外郭",
                    "結晶橋の広間",
                    "巨殿の採掘路",
                    "星光の玉座",
                    "星蝕魔像の炉心")),
            new BattleDungeonDefinition(
                "abyssal_grimoire_spire",
                "深淵魔導塔",
                "深淵の術式が空間を歪める魔導塔。全階層でクラス4天使系の熾天使ミカエルを捕獲できる。",
                "UI/DungeonSelect/DungeonCard_AbyssalGrimoireSpire",
                "BattleBackgrounds/dungeon6_1170x2532",
                26,
                BuildDungeonFloors(
                    "monster_seraph_michael",
                    0.12f,
                    74,
                    9,
                    true,
                    "深淵塔の入口",
                    "浮遊階段",
                    "紫光の魔導室",
                    "熾天使の天窓",
                    "深淵塔の頂"))
        };

        public static IReadOnlyList<BattleDungeonDefinition> Dungeons => DungeonDefinitions;

        private static BattleDungeonFloorDefinition[] BuildDungeonFloors(
            string enemyMonsterId,
            float firstRecruitChance,
            int firstEnemyCount,
            int enemyCountStep,
            bool finalFloorIsBoss,
            params string[] floorNames)
        {
            BattleDungeonFloorDefinition[] floors = new BattleDungeonFloorDefinition[FloorsPerDungeon];
            for (int i = 0; i < floors.Length; i += 1)
            {
                bool isFinalBossFloor = finalFloorIsBoss && i == floors.Length - 1;
                int enemyCount = isFinalBossFloor ? 1 : firstEnemyCount + i * Mathf.Max(0, enemyCountStep);
                string floorName = i < floorNames.Length && !string.IsNullOrEmpty(floorNames[i])
                    ? floorNames[i]
                    : "第" + (i + 1) + "層";
                floors[i] = new BattleDungeonFloorDefinition(
                    i + 1,
                    floorName,
                    enemyMonsterId,
                    Mathf.Max(0.01f, firstRecruitChance - i * 0.01f),
                    enemyCount,
                    isFinalBossFloor);
            }

            return floors;
        }

        public static BattleDungeonDefinition GetDungeon(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId))
            {
                return DungeonDefinitions[0];
            }

            for (int i = 0; i < DungeonDefinitions.Length; i += 1)
            {
                if (DungeonDefinitions[i].DungeonId == dungeonId)
                {
                    return DungeonDefinitions[i];
                }
            }

            return DungeonDefinitions[0];
        }

        private static int ResolveDungeonIndex(BattleDungeonDefinition dungeon)
        {
            if (dungeon == null)
            {
                return 0;
            }

            for (int i = 0; i < DungeonDefinitions.Length; i += 1)
            {
                if (DungeonDefinitions[i].DungeonId == dungeon.DungeonId)
                {
                    return i;
                }
            }

            return 0;
        }

        public static BattleDungeonDefinition GetDungeonForGlobalFloor(int globalFloor)
        {
            int clampedFloor = Mathf.Max(1, globalFloor);
            for (int i = DungeonDefinitions.Length - 1; i >= 0; i -= 1)
            {
                BattleDungeonDefinition dungeon = DungeonDefinitions[i];
                if (clampedFloor >= dungeon.GlobalFloorStart)
                {
                    return dungeon;
                }
            }

            return DungeonDefinitions[0];
        }

        public static BattleDungeonFloorDefinition GetFloor(string dungeonId, int localFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeon(dungeonId);
            int index = Mathf.Clamp(localFloor - 1, 0, dungeon.Floors.Count - 1);
            return dungeon.Floors[index];
        }

        public static BattleDungeonFloorDefinition GetFloorForGlobalFloor(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            int localFloor = Mathf.Clamp(globalFloor - dungeon.GlobalFloorStart + 1, 1, dungeon.Floors.Count);
            return GetFloor(dungeon.DungeonId, localFloor);
        }

        public static int ResolveGlobalFloor(string dungeonId, int localFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeon(dungeonId);
            int clampedLocalFloor = Mathf.Clamp(localFloor, 1, dungeon.Floors.Count);
            return dungeon.GlobalFloorStart + clampedLocalFloor - 1;
        }

        public static int ResolveLocalFloor(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            return Mathf.Clamp(globalFloor - dungeon.GlobalFloorStart + 1, 1, dungeon.Floors.Count);
        }

        public static string ResolveEnemyIdFromMonsterId(string monsterId)
        {
            return string.IsNullOrEmpty(monsterId)
                ? string.Empty
                : "enemy_class1_" + monsterId.Replace("monster_", string.Empty);
        }

        public static string ResolveMonsterIdFromEnemyId(string enemyId)
        {
            const string prefix = "enemy_class1_";
            if (string.IsNullOrEmpty(enemyId) || !enemyId.StartsWith(prefix))
            {
                return string.Empty;
            }

            return "monster_" + enemyId.Substring(prefix.Length);
        }

        public static string ResolveBattleBackdropResourcePath(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            return dungeon != null ? dungeon.BattleBackdropResourcePath : string.Empty;
        }

        public static string[] ResolveRecruitableMonsterIds(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null && !string.IsNullOrEmpty(floor.EnemyMonsterId)
                ? new[] { floor.EnemyMonsterId }
                : new string[0];
        }

        public static float ResolveRecruitChance(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null ? floor.RecruitChance : 0.35f;
        }

        public static int ResolveEnemyCount(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null ? Mathf.Max(1, floor.EnemyCount) : 100;
        }

        public static bool ResolveIsBossEncounter(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null && floor.IsBossEncounter;
        }

        public static EnemyDataSO CreateEnemyDataForGlobalFloor(int globalFloor, MasterDataManager masterDataManager)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return null;
            }

            MonsterDataSO monsterData = masterDataManager != null
                ? masterDataManager.GetMonsterData(floor.EnemyMonsterId)
                : null;
            if (monsterData == null)
            {
                return null;
            }

            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            int dungeonIndex = ResolveDungeonIndex(dungeon);
            float scale =
                EnemyStatScaleBase +
                Mathf.Max(1, globalFloor) * EnemyStatScalePerGlobalFloor +
                dungeonIndex * EnemyStatScalePerDungeon;
            EnemyDataSO enemyData = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemyData.enemyId = ResolveEnemyIdFromMonsterId(monsterData.monsterId);
            enemyData.enemyName = monsterData.monsterName;
            enemyData.maxHp = Mathf.Max(8, Mathf.RoundToInt(monsterData.baseStats.maxHp * scale));
            enemyData.attack = Mathf.Max(1, Mathf.RoundToInt(monsterData.baseStats.attack * scale));
            enemyData.magicAttack = Mathf.Max(0, Mathf.RoundToInt(monsterData.baseStats.magicAttack * scale));
            enemyData.defense = Mathf.Max(0, Mathf.RoundToInt(monsterData.baseStats.defense * scale * 0.78f));
            enemyData.magicDefense = Mathf.Max(0, Mathf.RoundToInt(monsterData.baseStats.magicDefense * scale * 0.78f));
            enemyData.damageType = monsterData.damageType;
            enemyData.attackRange = monsterData.attackRange;
            enemyData.normalAttackTargetCount = Mathf.Max(1, monsterData.normalAttackTargetCount);
            enemyData.normalAttackAppliesKnockback = false;
            enemyData.normalAttackKnockbackDuration = 0f;
            enemyData.attackSpeed = Mathf.Max(0.45f, monsterData.baseStats.attackSpeed * 0.88f);
            enemyData.critRate = Mathf.Clamp(monsterData.baseStats.attackSpeed * 0.025f, 0.02f, 0.08f);
            enemyData.critDamage = 1.35f;
            enemyData.rewardGold =
                RewardGoldBase +
                Mathf.Max(1, globalFloor) * RewardGoldPerGlobalFloor +
                dungeonIndex * RewardGoldPerDungeon;
            enemyData.rewardExp = ResolveRewardExpForFloor(globalFloor, dungeonIndex, floor, enemyData);
            enemyData.dropTableId = "drop_common_floor";
            enemyData.enemyTrait = ResolveTrait(monsterData);
            enemyData.battleIdleFacing = monsterData.battleIdleFacing;
            enemyData.battleMoveFacing = monsterData.battleMoveFacing;
            enemyData.battleAttackFacing = monsterData.battleAttackFacing;
            return enemyData;
        }

        private static int ResolveRewardExpForFloor(
            int globalFloor,
            int dungeonIndex,
            BattleDungeonFloorDefinition floor,
            EnemyDataSO enemyData)
        {
            if (floor == null || enemyData == null)
            {
                return 5;
            }

            bool isBossEncounter = floor.IsBossEncounter;
            int enemyCount = Mathf.Max(1, floor.EnemyCount);
            float effectiveHp = Mathf.Max(1, enemyData.maxHp);
            float effectiveAttack = Mathf.Max(1, Mathf.Max(enemyData.attack, enemyData.magicAttack));
            float effectiveDefense = Mathf.Max(0, enemyData.defense + enemyData.magicDefense);

            if (isBossEncounter)
            {
                effectiveHp *= RewardExpBossHpMultiplier;
                effectiveAttack *= RewardExpBossAttackMultiplier;
                effectiveDefense += RewardExpBossDefenseBonus * 2f;
            }

            float statScore =
                effectiveHp * RewardExpHpWeight +
                effectiveAttack * RewardExpAttackWeight +
                effectiveDefense * RewardExpDefenseWeight +
                Mathf.Max(0.1f, enemyData.attackSpeed) * RewardExpAttackSpeedWeight +
                Mathf.Clamp01(enemyData.critRate) * RewardExpCritWeight;
            float enemyCountMultiplier = isBossEncounter
                ? 1f
                : RewardExpCountBase + Mathf.Sqrt(enemyCount) * RewardExpCountSqrtStep;
            float floorComponent =
                RewardExpBase +
                statScore * RewardExpStatScoreMultiplier +
                Mathf.Max(1, globalFloor) * RewardExpPerGlobalFloor +
                Mathf.Max(0, dungeonIndex) * RewardExpPerDungeon;

            return Mathf.Max(1, Mathf.RoundToInt(floorComponent * enemyCountMultiplier));
        }

        private static EnemyTrait ResolveTrait(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return EnemyTrait.None;
            }

            switch (monsterData.raceId)
            {
                case "robot":
                case "golem":
                    return EnemyTrait.HighDefense;
                case "swordsman":
                    return EnemyTrait.FastAttack;
                case "mage":
                    return EnemyTrait.Drain;
                case "dragon":
                    return EnemyTrait.Critical;
                default:
                    return EnemyTrait.None;
            }
        }
    }
}
