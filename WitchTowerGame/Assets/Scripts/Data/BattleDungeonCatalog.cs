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
        private const float EnemyStatScaleBase = 0.58f;
        private const float EnemyStatScalePerGlobalFloor = 0.055f;

        private static readonly BattleDungeonDefinition[] DungeonDefinitions =
        {
            new BattleDungeonDefinition(
                "blight_cavern",
                "瘴牙の洞穴",
                "瘴気と牙跡が残る浅層洞窟。序盤の下位モンスターが群れで現れる。",
                "UI/DungeonSelect/DungeonCard_BlightCavern",
                "BattleBackgrounds/dungeon1_1170x2532",
                1,
                new[]
                {
                    new BattleDungeonFloorDefinition(1, "粘る入口", "monster_rock_golem", 0.46f, 10),
                    new BattleDungeonFloorDefinition(2, "焦げた横穴", "monster_dragon_whelp", 0.44f, 14),
                    new BattleDungeonFloorDefinition(3, "落石の小道", "monster_rock_golem", 0.42f, 18),
                    new BattleDungeonFloorDefinition(4, "小竜の巣", "monster_dragon_whelp", 0.40f, 20),
                    new BattleDungeonFloorDefinition(5, "瘴牙の奥", "monster_apprentice_swordsman", 0.38f, 24)
                }),
            new BattleDungeonDefinition(
                "gear_crypt",
                "機骸の廃工場",
                "壊れた機械と番兵が残る地下工場。金属系と剣士系の敵が前線を押してくる。",
                "UI/DungeonSelect/DungeonCard_GearCrypt",
                "BattleBackgrounds/dungeon2_1170x2532",
                6,
                new[]
                {
                    new BattleDungeonFloorDefinition(1, "錆びた搬入口", "monster_chibi_gear", 0.42f, 18),
                    new BattleDungeonFloorDefinition(2, "歯車通路", "monster_chibi_gear", 0.40f, 22),
                    new BattleDungeonFloorDefinition(3, "警備兵の詰所", "monster_apprentice_swordsman", 0.38f, 22),
                    new BattleDungeonFloorDefinition(4, "炉心前線", "monster_rock_golem", 0.36f, 24),
                    new BattleDungeonFloorDefinition(5, "機骸炉", "monster_chibi_gear", 0.34f, 30)
                }),
            new BattleDungeonDefinition(
                "curse_library",
                "呪灯の地下書庫",
                "呪いの灯が消えない地下書庫。魔導士と幼竜が遠距離から圧をかける。",
                "UI/DungeonSelect/DungeonCard_CurseLibrary",
                "BattleBackgrounds/dungeon3_1170x2532",
                11,
                new[]
                {
                    new BattleDungeonFloorDefinition(1, "呪灯の棚道", "monster_apprentice_mage", 0.40f, 26),
                    new BattleDungeonFloorDefinition(2, "古文書の回廊", "monster_apprentice_mage", 0.38f, 28),
                    new BattleDungeonFloorDefinition(3, "黒煙の閲覧室", "monster_dragon_whelp", 0.36f, 30),
                    new BattleDungeonFloorDefinition(4, "封印された階段", "monster_apprentice_swordsman", 0.34f, 32),
                    new BattleDungeonFloorDefinition(5, "地下書庫の心臓", "monster_apprentice_mage", 0.32f, 34)
                })
        };

        public static IReadOnlyList<BattleDungeonDefinition> Dungeons => DungeonDefinitions;

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

            float scale = EnemyStatScaleBase + Mathf.Max(1, globalFloor) * EnemyStatScalePerGlobalFloor;
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
            enemyData.rewardGold = 8 + Mathf.Max(1, globalFloor) * 3;
            enemyData.rewardExp = 5 + Mathf.Max(1, globalFloor) * 2;
            enemyData.dropTableId = "drop_common_floor";
            enemyData.enemyTrait = ResolveTrait(monsterData);
            enemyData.battleIdleFacing = monsterData.battleIdleFacing;
            enemyData.battleMoveFacing = monsterData.battleMoveFacing;
            enemyData.battleAttackFacing = monsterData.battleAttackFacing;
            return enemyData;
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
