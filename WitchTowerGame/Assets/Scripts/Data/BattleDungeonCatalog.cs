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
            bool isBossEncounter = false,
            string bossMonsterId = null)
            : this(
                localFloor,
                floorName,
                string.IsNullOrEmpty(enemyMonsterId) ? new string[0] : new[] { enemyMonsterId },
                recruitChance,
                enemyCount,
                isBossEncounter,
                bossMonsterId)
        {
        }

        public BattleDungeonFloorDefinition(
            int localFloor,
            string floorName,
            IReadOnlyList<string> enemyMonsterIds,
            float recruitChance,
            int enemyCount,
            bool isBossEncounter = false,
            string bossMonsterId = null)
            : this(
                localFloor,
                floorName,
                enemyMonsterIds,
                recruitChance,
                enemyCount,
                isBossEncounter,
                string.IsNullOrEmpty(bossMonsterId) ? new string[0] : new[] { bossMonsterId })
        {
        }

        public BattleDungeonFloorDefinition(
            int localFloor,
            string floorName,
            IReadOnlyList<string> enemyMonsterIds,
            float recruitChance,
            int enemyCount,
            bool isBossEncounter,
            IReadOnlyList<string> bossMonsterIds)
        {
            LocalFloor = Mathf.Max(1, localFloor);
            FloorName = floorName;
            EnemyMonsterIds = BuildEnemyMonsterIdList(enemyMonsterIds);
            EnemyMonsterId = EnemyMonsterIds.Count > 0 ? EnemyMonsterIds[0] : string.Empty;
            RecruitChance = Mathf.Clamp01(recruitChance);
            EnemyCount = Mathf.Max(1, enemyCount);
            IsBossEncounter = isBossEncounter;
            BossMonsterIds = BuildBossMonsterIdList(bossMonsterIds);
            BossMonsterId = BossMonsterIds.Count > 0 ? BossMonsterIds[0] : string.Empty;
        }

        public int LocalFloor { get; }
        public string FloorName { get; }
        public string EnemyMonsterId { get; }
        public IReadOnlyList<string> EnemyMonsterIds { get; }
        public float RecruitChance { get; }
        public int EnemyCount { get; }
        public bool IsBossEncounter { get; }
        public string BossMonsterId { get; }
        public IReadOnlyList<string> BossMonsterIds { get; }

        private static IReadOnlyList<string> BuildEnemyMonsterIdList(IReadOnlyList<string> enemyMonsterIds)
        {
            var results = new List<string>();
            if (enemyMonsterIds == null)
            {
                return results;
            }

            for (int i = 0; i < enemyMonsterIds.Count; i += 1)
            {
                string monsterId = enemyMonsterIds[i];
                if (!string.IsNullOrEmpty(monsterId) && !results.Contains(monsterId))
                {
                    results.Add(monsterId);
                }
            }

            return results;
        }

        private static IReadOnlyList<string> BuildBossMonsterIdList(IReadOnlyList<string> bossMonsterIds)
        {
            var results = new List<string>();
            if (bossMonsterIds == null)
            {
                return results;
            }

            for (int i = 0; i < bossMonsterIds.Count; i += 1)
            {
                string monsterId = bossMonsterIds[i];
                if (!string.IsNullOrEmpty(monsterId))
                {
                    results.Add(monsterId);
                }
            }

            return results;
        }
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
        private const int FloorsPerDungeon = 10;
        private const float EnemyStatScaleBase = 0.46f;
        private const float EnemyStatScalePerGlobalFloor = 0.045f;
        private const float EnemyStatScalePerDungeon = 0.17f;
        private const float GearCryptEnemyStatMultiplier = 0.65f;
        private const float GearCryptEnemyStatMultiplierPerFloor = 0.012f;
        private const float GearCryptMinimumEnemyStatMultiplier = 0.30f;
        private const float CurseLibraryEnemyStatMultiplier = 0.65f;
        private const float EmberDrakePassEnemyStatMultiplier = 0.90f;
        private const float StarOreCitadelEnemyStatMultiplier = 0.77f;
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
        private const float RewardExpCountBase = 0.82f;
        private const float RewardExpCountSqrtStep = 0.34f;
        private const float RewardExpBossHpMultiplier = 5.0f;
        private const float RewardExpBossAttackMultiplier = 2.0f;
        private const int RewardExpBossDefenseBonus = 8;
        private const float CurrentDungeonRecruitChance = 0.20f;
        private const float BossRecruitChance = 0.05f;
        private const float Class1RecruitChance = 0.05f;
        private const float Class2RecruitChance = 0.005f;
        private const float Class3RecruitChance = 0.001f;
        private const string DefaultBattleBgmKey = "battle_normal";
        private static readonly string[] Class1MonsterIds =
        {
            "monster_dragon_whelp",
            "monster_chibi_gear",
            "monster_rock_golem",
            "monster_apprentice_swordsman",
            "monster_apprentice_mage"
        };

        private static readonly string[] Class2MonsterIds =
        {
            MonsterFusionCatalog.FlareDrakeId,
            MonsterFusionCatalog.ArmedDroidId,
            MonsterFusionCatalog.OreGiantGarmId,
            MonsterFusionCatalog.HolyArmorLeonId,
            MonsterFusionCatalog.DarkRobeCurseMageNoahId
        };

        private static readonly string[] Class3MonsterIds =
        {
            MonsterFusionCatalog.AbyssDragonId,
            MonsterFusionCatalog.OmegaLeonId,
            MonsterFusionCatalog.CosmicOreFortressGolemId,
            MonsterFusionCatalog.SwordSaintAlvarezId,
            MonsterFusionCatalog.AbyssGrandMageSeraphisId
        };

        // Dungeon 1 introduces the loop: enough pressure to reward equipment and a little replay,
        // without creating a consecutive-loss wall before Dungeon 2.
        private static readonly int[] DungeonOneEnemyCounts = { 6, 6, 6, 7, 7, 10, 9, 8, 9, 8 };
        private static readonly int[] DungeonTwoEnemyCounts = { 20, 22, 24, 26, 28, 30, 32, 34, 36, 38 };

        private static readonly BattleDungeonDefinition[] DungeonDefinitions =
        {
            new BattleDungeonDefinition(
                "blight_cavern",
                "見習いの五門洞",
                "十の小門が連なる浅層洞窟。各階層でクラス1の5種族を巡りながら捕獲できる。",
                "UI/DungeonSelect/DungeonCard_BlightCavern",
                "BattleBackgrounds/dungeon1_1170x2532",
                1,
                BuildDungeonFloorsWithEnemyCounts(
                    Class1MonsterIds,
                    CurrentDungeonRecruitChance,
                    DungeonOneEnemyCounts,
                    false,
                    "竜の小門",
                    "機兵の小門",
                    "岩像の小門",
                    "剣士の小門",
                    "魔導の小門",
                    "炎息の回廊",
                    "歯車の回廊",
                    "岩根の回廊",
                    "白刃の回廊",
                    "五契の奥門")),
            new BattleDungeonDefinition(
                "gear_crypt",
                "獣影の廃工廠",
                "炉心と鉱石炉が残る廃工廠。クラス1の5種族が入り混じって出現し、捕獲できる。",
                "UI/DungeonSelect/DungeonCard_GearCrypt",
                "BattleBackgrounds/dungeon2_1170x2532",
                11,
                BuildMixedDungeonFloorsWithEnemyCounts(
                    Class1MonsterIds,
                    CurrentDungeonRecruitChance,
                    DungeonTwoEnemyCounts,
                    false,
                    "錆びた搬入口",
                    "鉱石運搬路",
                    "結晶整備室",
                    "炉心採掘線",
                    "五影の鉱炉",
                    "歯車坑道",
                    "影獣の点検室",
                    "蒸気圧縮路",
                    "暴走生産炉",
                    "獣影の中枢")),
            new BattleDungeonDefinition(
                "curse_library",
                "古契約の地下書庫",
                "古い契約書が封じられた地下書庫。クラス2の眷属が出現し、各階層の最後にクラス3ボスが出現する。",
                "UI/DungeonSelect/DungeonCard_CurseLibrary",
                "BattleBackgrounds/dungeon3_1170x2532",
                21,
                BuildDungeonFloorsWithFinalBosses(
                    Class2MonsterIds,
                    Class3MonsterIds,
                    CurrentDungeonRecruitChance,
                    30,
                    3,
                    "封印書架",
                    "召喚円の閲覧室",
                    "鎖の禁書橋",
                    "紫灯の契約庫",
                    "古契約の奥書",
                    "墨染めの写本廊",
                    "封蝋の回廊",
                    "契約獣の閲覧室",
                    "記憶封じの書庫",
                    "古契約の心室")),
            new BattleDungeonDefinition(
                "ember_drake_pass",
                "紅蓮竜道",
                "溶岩脈に沿って続く竜の通り道。クラス2の群れを突破すると、各階層の最後にクラス3ボスが出現する。",
                "UI/DungeonSelect/DungeonCard_EmberDrakePass",
                "BattleBackgrounds/dungeon4_1170x2532",
                31,
                BuildDungeonFloorsWithFinalBosses(
                    Class2MonsterIds,
                    Class3MonsterIds,
                    CurrentDungeonRecruitChance,
                    60,
                    5,
                    "火口の入口",
                    "紅蓮の石橋",
                    "竜鱗の回廊",
                    "機竜の滑走路",
                    "紅蓮竜道の中腹",
                    "溶岩脈の裂け目",
                    "焦熱の竜爪橋",
                    "竜骨の見張り場",
                    "炎冠の祭壇",
                    "紅蓮竜道の最奥")),
            new BattleDungeonDefinition(
                "star_ore_citadel",
                "星鉱の巨殿",
                "星を含んだ鉱石が鳴る巨大殿堂。全階層でクラス3の混合モンスターが出現し、各階層の最後にクラス3ボスが2体出現する。",
                "UI/DungeonSelect/DungeonCard_StarOreCitadel",
                "BattleBackgrounds/dungeon5_1170x2532",
                41,
                BuildDungeonFloorsWithFinalBosses(
                    Class3MonsterIds,
                    Class3MonsterIds,
                    CurrentDungeonRecruitChance,
                    62,
                    8,
                    2,
                    "星鉱の外郭",
                    "結晶橋の広間",
                    "巨殿の採掘路",
                    "星光の玉座",
                    "星鉱の炉心",
                    "流星鉱の回廊",
                    "青晶の昇降機",
                    "星砂の礼拝堂",
                    "天球採掘庭",
                    "星鉱の中枢")),
            new BattleDungeonDefinition(
                "abyssal_grimoire_spire",
                "深淵魔導回廊",
                "深淵の術式が空間を歪める魔導回廊。クラス3の混合モンスターが出現し、各階層の最後にクラス3ボスが2体出現する。",
                "UI/DungeonSelect/DungeonCard_AbyssalGrimoireSpire",
                "BattleBackgrounds/dungeon6_1170x2532",
                51,
                BuildDungeonFloorsWithFinalBosses(
                    Class3MonsterIds,
                    Class3MonsterIds,
                    CurrentDungeonRecruitChance,
                    74,
                    9,
                    2,
                    "深淵回廊の入口",
                    "浮遊階段",
                    "紫光の魔導室",
                    "熾天使の天窓",
                    "深淵回廊の中層",
                    "歪曲した写本廊",
                    "無音の召喚室",
                    "闇晶の観測台",
                    "黒契約の階段",
                    "深淵魔導核"))
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
            string[] enemyMonsterIds = new string[FloorsPerDungeon];
            for (int i = 0; i < enemyMonsterIds.Length; i += 1)
            {
                enemyMonsterIds[i] = enemyMonsterId;
            }

            return BuildDungeonFloors(
                enemyMonsterIds,
                firstRecruitChance,
                firstEnemyCount,
                enemyCountStep,
                finalFloorIsBoss,
                floorNames);
        }

        private static BattleDungeonFloorDefinition[] BuildDungeonFloors(
            string[] enemyMonsterIds,
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
                string enemyMonsterId = ResolveEnemyMonsterIdForFloor(enemyMonsterIds, i);
                string floorName = i < floorNames.Length && !string.IsNullOrEmpty(floorNames[i])
                    ? floorNames[i]
                    : "第" + (i + 1) + "層";
                floors[i] = new BattleDungeonFloorDefinition(
                    i + 1,
                    floorName,
                    enemyMonsterId,
                    ResolveRecruitChanceForTier(firstRecruitChance),
                    enemyCount,
                    isFinalBossFloor);
            }

            return floors;
        }

        private static BattleDungeonFloorDefinition[] BuildDungeonFloorsWithEnemyCounts(
            string[] enemyMonsterIds,
            float firstRecruitChance,
            IReadOnlyList<int> enemyCounts,
            bool finalFloorIsBoss,
            params string[] floorNames)
        {
            BattleDungeonFloorDefinition[] floors = new BattleDungeonFloorDefinition[FloorsPerDungeon];
            int fallbackEnemyCount = enemyCounts != null && enemyCounts.Count > 0
                ? Mathf.Max(1, enemyCounts[enemyCounts.Count - 1])
                : 1;
            for (int i = 0; i < floors.Length; i += 1)
            {
                bool isFinalBossFloor = finalFloorIsBoss && i == floors.Length - 1;
                int configuredEnemyCount = enemyCounts != null && i < enemyCounts.Count
                    ? enemyCounts[i]
                    : fallbackEnemyCount;
                int enemyCount = isFinalBossFloor ? 1 : Mathf.Max(1, configuredEnemyCount);
                string enemyMonsterId = ResolveEnemyMonsterIdForFloor(enemyMonsterIds, i);
                string floorName = i < floorNames.Length && !string.IsNullOrEmpty(floorNames[i])
                    ? floorNames[i]
                    : "第" + (i + 1) + "層";
                floors[i] = new BattleDungeonFloorDefinition(
                    i + 1,
                    floorName,
                    enemyMonsterId,
                    ResolveRecruitChanceForTier(firstRecruitChance),
                    enemyCount,
                    isFinalBossFloor);
            }

            return floors;
        }

        private static string ResolveEnemyMonsterIdForFloor(IReadOnlyList<string> enemyMonsterIds, int floorIndex)
        {
            if (enemyMonsterIds == null || enemyMonsterIds.Count == 0)
            {
                return string.Empty;
            }

            int index = Mathf.Abs(floorIndex) % enemyMonsterIds.Count;
            return enemyMonsterIds[index] ?? string.Empty;
        }

        private static BattleDungeonFloorDefinition[] BuildMixedDungeonFloors(
            string[] enemyMonsterIds,
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
                    enemyMonsterIds,
                    ResolveRecruitChanceForTier(firstRecruitChance),
                    enemyCount,
                    isFinalBossFloor);
            }

            return floors;
        }

        private static BattleDungeonFloorDefinition[] BuildMixedDungeonFloorsWithEnemyCounts(
            string[] enemyMonsterIds,
            float firstRecruitChance,
            IReadOnlyList<int> enemyCounts,
            bool finalFloorIsBoss,
            params string[] floorNames)
        {
            BattleDungeonFloorDefinition[] floors = new BattleDungeonFloorDefinition[FloorsPerDungeon];
            int fallbackEnemyCount = enemyCounts != null && enemyCounts.Count > 0
                ? Mathf.Max(1, enemyCounts[enemyCounts.Count - 1])
                : 1;
            for (int i = 0; i < floors.Length; i += 1)
            {
                bool isFinalBossFloor = finalFloorIsBoss && i == floors.Length - 1;
                int configuredEnemyCount = enemyCounts != null && i < enemyCounts.Count
                    ? enemyCounts[i]
                    : fallbackEnemyCount;
                int enemyCount = isFinalBossFloor ? 1 : Mathf.Max(1, configuredEnemyCount);
                string floorName = i < floorNames.Length && !string.IsNullOrEmpty(floorNames[i])
                    ? floorNames[i]
                    : "第" + (i + 1) + "層";
                floors[i] = new BattleDungeonFloorDefinition(
                    i + 1,
                    floorName,
                    enemyMonsterIds,
                    ResolveRecruitChanceForTier(firstRecruitChance),
                    enemyCount,
                    isFinalBossFloor);
            }

            return floors;
        }

        private static BattleDungeonFloorDefinition[] BuildDungeonFloorsWithFinalBosses(
            IReadOnlyList<string> enemyMonsterIds,
            IReadOnlyList<string> finalBossMonsterIds,
            float firstRecruitChance,
            int firstEnemyCount,
            int enemyCountStep,
            params string[] floorNames)
        {
            return BuildDungeonFloorsWithFinalBosses(
                enemyMonsterIds,
                finalBossMonsterIds,
                firstRecruitChance,
                firstEnemyCount,
                enemyCountStep,
                1,
                floorNames);
        }

        private static BattleDungeonFloorDefinition[] BuildDungeonFloorsWithFinalBosses(
            IReadOnlyList<string> enemyMonsterIds,
            IReadOnlyList<string> finalBossMonsterIds,
            float firstRecruitChance,
            int firstEnemyCount,
            int enemyCountStep,
            int finalBossCount,
            params string[] floorNames)
        {
            BattleDungeonFloorDefinition[] floors = new BattleDungeonFloorDefinition[FloorsPerDungeon];
            int safeFinalBossCount = Mathf.Max(1, finalBossCount);
            for (int i = 0; i < floors.Length; i += 1)
            {
                int enemyCount = Mathf.Max(safeFinalBossCount + 1, firstEnemyCount + i * Mathf.Max(0, enemyCountStep));
                string floorName = i < floorNames.Length && !string.IsNullOrEmpty(floorNames[i])
                    ? floorNames[i]
                    : "第" + (i + 1) + "層";
                floors[i] = new BattleDungeonFloorDefinition(
                    i + 1,
                    floorName,
                    enemyMonsterIds,
                    ResolveRecruitChanceForTier(firstRecruitChance),
                    enemyCount,
                    false,
                    ResolveFinalBossMonsterIds(finalBossMonsterIds, i, safeFinalBossCount));
            }

            return floors;
        }

        private static string[] ResolveFinalBossMonsterIds(IReadOnlyList<string> finalBossMonsterIds, int floorIndex, int finalBossCount)
        {
            var results = new List<string>();
            if (finalBossMonsterIds == null || finalBossMonsterIds.Count == 0)
            {
                return results.ToArray();
            }

            int safeFinalBossCount = Mathf.Max(1, finalBossCount);
            for (int i = 0; i < safeFinalBossCount; i += 1)
            {
                string monsterId = finalBossMonsterIds[(Mathf.Max(0, floorIndex) + i) % finalBossMonsterIds.Count];
                if (!string.IsNullOrEmpty(monsterId))
                {
                    results.Add(monsterId);
                }
            }

            return results.ToArray();
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

        public static int ResolveEquipmentClassRank(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            return Mathf.Clamp(ResolveDungeonIndex(dungeon) + 1, 1, 6);
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

        public static bool IsDungeonTransition(int currentGlobalFloor, int nextGlobalFloor)
        {
            BattleDungeonDefinition currentDungeon = GetDungeonForGlobalFloor(currentGlobalFloor);
            BattleDungeonDefinition nextDungeon = GetDungeonForGlobalFloor(nextGlobalFloor);
            return currentDungeon != null &&
                   nextDungeon != null &&
                   currentDungeon.DungeonId != nextDungeon.DungeonId;
        }

        public static string ResolveDungeonName(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            return dungeon != null ? dungeon.DungeonName : string.Empty;
        }

        public static string ResolveStageName(int globalFloor)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            return dungeon != null ? dungeon.DungeonName : string.Empty;
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

        public static string ResolveBattleBgmKey(int globalFloor, bool isBossEncounter)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            if (dungeon == null || string.IsNullOrEmpty(dungeon.DungeonId))
            {
                return DefaultBattleBgmKey;
            }

            switch (dungeon.DungeonId)
            {
                case "blight_cavern":
                    return isBossEncounter ? "dungeon_blight_cavern_boss" : "dungeon_blight_cavern";
                case "gear_crypt":
                    return isBossEncounter ? "dungeon_gear_crypt_boss" : "dungeon_gear_crypt";
                case "curse_library":
                    return isBossEncounter ? "dungeon_curse_library_boss" : "dungeon_curse_library";
                case "ember_drake_pass":
                    return isBossEncounter ? "dungeon_ember_drake_pass_boss" : "dungeon_ember_drake_pass";
                case "star_ore_citadel":
                    return isBossEncounter ? "dungeon_star_ore_citadel_boss" : "dungeon_star_ore_citadel";
                case "abyssal_grimoire_spire":
                    return isBossEncounter ? "dungeon_abyssal_grimoire_spire_boss" : "dungeon_abyssal_grimoire_spire";
                default:
                    return DefaultBattleBgmKey;
            }
        }

        public static string[] ResolveRecruitableMonsterIds(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return new string[0];
            }

            var monsterIds = new List<string>();
            if (floor.EnemyMonsterIds != null)
            {
                for (int i = 0; i < floor.EnemyMonsterIds.Count; i += 1)
                {
                    if (!string.IsNullOrEmpty(floor.EnemyMonsterIds[i]) && !monsterIds.Contains(floor.EnemyMonsterIds[i]))
                    {
                        monsterIds.Add(floor.EnemyMonsterIds[i]);
                    }
                }
            }

            if (floor.BossMonsterIds != null)
            {
                for (int i = 0; i < floor.BossMonsterIds.Count; i += 1)
                {
                    string bossMonsterId = floor.BossMonsterIds[i];
                    if (!string.IsNullOrEmpty(bossMonsterId) && !monsterIds.Contains(bossMonsterId))
                    {
                        monsterIds.Add(bossMonsterId);
                    }
                }
            }

            return monsterIds.ToArray();
        }

        public static bool IsRecruitableMonsterOnFloor(int globalFloor, string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return false;
            }

            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null &&
                (ContainsMonsterId(floor.EnemyMonsterIds, monsterId) || ContainsMonsterId(floor.BossMonsterIds, monsterId));
        }

        public static bool IsBossMonsterOnFloor(int globalFloor, string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return false;
            }

            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return false;
            }

            return ContainsMonsterId(floor.BossMonsterIds, monsterId) ||
                   (floor.IsBossEncounter && ContainsMonsterId(floor.EnemyMonsterIds, monsterId));
        }

        public static float ResolveRecruitChance(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null ? floor.RecruitChance : Class1RecruitChance;
        }

        public static float ResolvePerDefeatRecruitChance(int globalFloor)
        {
            float battleChance = ResolveRecruitChance(globalFloor);
            if (battleChance <= 0f)
            {
                return 0f;
            }

            int recruitableEnemyCount = ResolveRecruitableEnemyCount(globalFloor);
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(battleChance), 1f / Mathf.Max(1, recruitableEnemyCount));
        }

        public static float ResolvePerDefeatRecruitChance(int globalFloor, MonsterDataSO monsterData)
        {
            int recruitTier = ResolveRecruitTier(monsterData);
            float battleChance = ResolveRecruitChanceForMonster(globalFloor, monsterData, recruitTier);
            if (battleChance <= 0f)
            {
                return 0f;
            }

            int recruitableEnemyCount = ResolveRecruitableEnemyCount(globalFloor, recruitTier);
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(battleChance), 1f / Mathf.Max(1, recruitableEnemyCount));
        }

        private static float ResolveRecruitChanceForMonster(int globalFloor, MonsterDataSO monsterData, int recruitTier)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null || monsterData == null || string.IsNullOrEmpty(monsterData.monsterId))
            {
                return ResolveRecruitChanceForTier(recruitTier);
            }

            if (ContainsMonsterId(floor.BossMonsterIds, monsterData.monsterId))
            {
                return BossRecruitChance;
            }

            if (ContainsMonsterId(floor.EnemyMonsterIds, monsterData.monsterId))
            {
                return ResolveRecruitChanceForTier(floor.RecruitChance);
            }

            return ResolveRecruitChanceForTier(recruitTier);
        }

        public static int ResolveRecruitableEnemyCount(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return ResolveEnemyCount(globalFloor);
            }

            return Mathf.Max(1, floor.EnemyCount);
        }

        public static int ResolveRecruitableEnemyCount(int globalFloor, int recruitTier)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return ResolveRecruitableEnemyCount(globalFloor);
            }

            int enemyCount = Mathf.Max(1, floor.EnemyCount);
            int bossEnemyCount = ResolveBossEnemyCount(floor);
            int normalEnemyCount = bossEnemyCount > 0 ? Mathf.Max(0, enemyCount - bossEnemyCount) : enemyCount;
            int count = ContainsMonsterWithRecruitTier(floor.EnemyMonsterIds, recruitTier) ? normalEnemyCount : 0;
            count += CountMonsterIdsWithRecruitTier(floor.BossMonsterIds, recruitTier);

            return Mathf.Max(1, count);
        }

        private static float ResolveRecruitChanceForTier(float recruitChance)
        {
            return Mathf.Clamp01(recruitChance);
        }

        private static float ResolveRecruitChanceForTier(int recruitTier)
        {
            switch (Mathf.Clamp(recruitTier, 1, 3))
            {
                case 1:
                    return Class1RecruitChance;
                case 2:
                    return Class2RecruitChance;
                default:
                    return Class3RecruitChance;
            }
        }

        private static int ResolveRecruitTier(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return 1;
            }

            return Mathf.Clamp((int)monsterData.rarity, 1, 3);
        }

        private static int ResolveRecruitTierForMonsterId(string monsterId)
        {
            if (ContainsMonsterId(Class1MonsterIds, monsterId))
            {
                return 1;
            }

            if (ContainsMonsterId(Class2MonsterIds, monsterId))
            {
                return 2;
            }

            if (ContainsMonsterId(Class3MonsterIds, monsterId))
            {
                return 3;
            }

            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId)
                ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                : null;
            return ResolveRecruitTier(monsterData);
        }

        private static bool ContainsMonsterWithRecruitTier(IReadOnlyList<string> monsterIds, int recruitTier)
        {
            if (monsterIds == null)
            {
                return false;
            }

            for (int i = 0; i < monsterIds.Count; i += 1)
            {
                if (ResolveRecruitTierForMonsterId(monsterIds[i]) == recruitTier)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountMonsterIdsWithRecruitTier(IReadOnlyList<string> monsterIds, int recruitTier)
        {
            if (monsterIds == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < monsterIds.Count; i += 1)
            {
                if (ResolveRecruitTierForMonsterId(monsterIds[i]) == recruitTier)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static bool ContainsMonsterId(IReadOnlyList<string> monsterIds, string monsterId)
        {
            if (monsterIds == null || string.IsNullOrEmpty(monsterId))
            {
                return false;
            }

            for (int i = 0; i < monsterIds.Count; i += 1)
            {
                if (monsterIds[i] == monsterId)
                {
                    return true;
                }
            }

            return false;
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

        public static string ResolveBossMonsterId(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            return floor != null ? floor.BossMonsterId : string.Empty;
        }

        public static string[] ResolveBossMonsterIds(int globalFloor)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor?.BossMonsterIds == null || floor.BossMonsterIds.Count == 0)
            {
                return new string[0];
            }

            string[] results = new string[floor.BossMonsterIds.Count];
            for (int i = 0; i < floor.BossMonsterIds.Count; i += 1)
            {
                results[i] = floor.BossMonsterIds[i];
            }

            return results;
        }

        public static bool ResolveHasFinalBossEnemy(int globalFloor)
        {
            return ResolveBossMonsterIds(globalFloor).Length > 0;
        }

        private static int ResolveBossEnemyCount(BattleDungeonFloorDefinition floor)
        {
            return floor?.BossMonsterIds != null ? floor.BossMonsterIds.Count : 0;
        }

        public static EnemyDataSO CreateEnemyDataForGlobalFloor(int globalFloor, MasterDataManager masterDataManager)
        {
            return CreateEnemyDataForGlobalFloor(globalFloor, masterDataManager, false);
        }

        public static EnemyDataSO CreateEnemyDataForGlobalFloor(
            int globalFloor,
            MasterDataManager masterDataManager,
            bool randomizeFromFloorPool)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            if (floor == null)
            {
                return null;
            }

            MonsterDataSO monsterData = ResolveMonsterDataForFloor(floor, masterDataManager, randomizeFromFloorPool);
            if (monsterData == null)
            {
                return null;
            }

            return CreateEnemyDataForMonster(globalFloor, floor, monsterData);
        }

        public static EnemyDataSO CreateEnemyDataForMonsterAtGlobalFloor(
            int globalFloor,
            MasterDataManager masterDataManager,
            string monsterId)
        {
            BattleDungeonFloorDefinition floor = GetFloorForGlobalFloor(globalFloor);
            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId) && masterDataManager != null
                ? masterDataManager.GetMonsterData(monsterId)
                : null;
            return floor != null && monsterData != null
                ? CreateEnemyDataForMonster(globalFloor, floor, monsterData)
                : null;
        }

        private static EnemyDataSO CreateEnemyDataForMonster(
            int globalFloor,
            BattleDungeonFloorDefinition floor,
            MonsterDataSO monsterData)
        {
            BattleDungeonDefinition dungeon = GetDungeonForGlobalFloor(globalFloor);
            int dungeonIndex = ResolveDungeonIndex(dungeon);
            float scale =
                EnemyStatScaleBase +
                Mathf.Max(1, globalFloor) * EnemyStatScalePerGlobalFloor +
                dungeonIndex * EnemyStatScalePerDungeon;
            scale *= ResolveEnemyStatMultiplier(dungeon, globalFloor);
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

        private static float ResolveEnemyStatMultiplier(BattleDungeonDefinition dungeon, int globalFloor)
        {
            if (dungeon == null || dungeon.DungeonId != "gear_crypt")
            {
                if (dungeon != null && dungeon.DungeonId == "curse_library")
                {
                    return CurseLibraryEnemyStatMultiplier;
                }

                return dungeon != null && dungeon.DungeonId == "ember_drake_pass"
                    ? EmberDrakePassEnemyStatMultiplier
                    : dungeon != null && dungeon.DungeonId == "star_ore_citadel"
                        ? StarOreCitadelEnemyStatMultiplier
                        : 1f;
            }

            int localFloorIndex = Mathf.Max(0, globalFloor - dungeon.GlobalFloorStart);
            return Mathf.Max(
                GearCryptMinimumEnemyStatMultiplier,
                GearCryptEnemyStatMultiplier - localFloorIndex * GearCryptEnemyStatMultiplierPerFloor);
        }

        private static MonsterDataSO ResolveMonsterDataForFloor(
            BattleDungeonFloorDefinition floor,
            MasterDataManager masterDataManager,
            bool randomizeFromFloorPool)
        {
            if (floor == null || masterDataManager == null || floor.EnemyMonsterIds == null || floor.EnemyMonsterIds.Count == 0)
            {
                return null;
            }

            int startIndex = randomizeFromFloorPool && floor.EnemyMonsterIds.Count > 1
                ? Random.Range(0, floor.EnemyMonsterIds.Count)
                : 0;
            for (int i = 0; i < floor.EnemyMonsterIds.Count; i += 1)
            {
                int index = (startIndex + i) % floor.EnemyMonsterIds.Count;
                string monsterId = floor.EnemyMonsterIds[index];
                if (string.IsNullOrEmpty(monsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = masterDataManager.GetMonsterData(monsterId);
                if (monsterData != null)
                {
                    return monsterData;
                }
            }

            return null;
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
