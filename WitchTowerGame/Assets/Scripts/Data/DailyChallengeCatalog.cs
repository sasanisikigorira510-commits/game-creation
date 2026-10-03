using System;
using System.Linq;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public static class DailyChallengeCatalog
    {
        public const string Avalanche = "avalanche";
        public const string Champion = "champion";
        public const int StageCount = 10;
        public const int EntriesPerDay = 2;
        public const int AvalancheLivingCap = 30;
        public static readonly string[] Modes = { Avalanche, Champion };
        private static readonly int[] Hp = { 90, 150, 230, 280, 350, 650, 1000, 1600, 2500, 3800 };
        private static readonly int[] Attack = { 6, 8, 11, 14, 18, 34, 60, 100, 165, 240 };
        private static readonly int[] ChampionHp = { 1260, 2500, 4800, 8000, 11500, 21000, 36000, 57000, 85000, 125000 };
        private static readonly int[] ChampionAttack = { 27, 40, 65, 105, 150, 250, 380, 570, 790, 1080 };
        private static readonly int[] Defense = { 1, 3, 5, 8, 12, 18, 26, 36, 48, 64 };
        private static readonly int[] Counts = { 12, 16, 20, 24, 30, 36, 42, 48, 54, 60 };
        private static readonly int[] Cores = { 0, 0, 0, 0, 1, 3, 4, 4, 5, 5 };
        public static bool IsMode(string mode) => mode == Avalanche || mode == Champion;
        public static string Label(string mode) => mode == Avalanche ? "雪崩の試練" : "強敵の試練";
        public static string Description(string mode) => mode == Avalanche
            ? "押し寄せる群れを突破する10段階。範囲攻撃が活躍します。"
            : "強敵を1体ずつ倒す10連戦。単体火力と耐久が活躍します。";
        public static int EnemyCount(string mode, int stage) => mode == Champion ? 1 : Counts[Index(stage)];
        public static int LivingCap(string mode) => mode == Champion ? 1 : AvalancheLivingCap;
        public static int Drops(int stage) => stage <= 2 ? 1 : stage <= 4 ? 2 : stage == 5 ? 3 : 0;
        public static int StarCores(int stage) => Cores[Index(stage)];
        public static int Gold(int stage) => Mathf.Clamp(stage, 1, 10) * 100;
        public static int MonsterExp(int stage) => Gold(stage);
        public static int DesiredEnemyClass(int stage, int spawnIndex)
        {
            if (stage <= 3) return spawnIndex % 2 == 0 ? 1 : 2;
            if (stage <= 5) return spawnIndex % 2 == 0 ? 2 : 3;
            return spawnIndex % 2 == 0 ? 4 : 5;
        }
        public static string RewardLabel(int stage) =>
            (Drops(stage) > 0 ? $"修練の雫 ×{Drops(stage)}  " : string.Empty) +
            (StarCores(stage) > 0 ? $"試練の星核 ×{StarCores(stage)}  " : string.Empty) +
            $"ゴールド {Gold(stage)}・参加モンスター経験値 {MonsterExp(stage)}";
        public static int Remaining(DailyChallengeModeState state) => Mathf.Clamp(EntriesPerDay - (state?.UsedEntries ?? 0), 0, EntriesPerDay);
        public static string JapanDay(DateTime utc) => utc.ToUniversalTime().AddHours(9).ToString("yyyy-MM-dd");
        private static int Index(int stage) => Mathf.Clamp(stage, 1, StageCount) - 1;

        // The strength table is fixed, independent of owned party strength or normal-dungeon floor.
        // Registered class-five monsters appear in the latter half. A nearest-class fallback
        // also keeps the encounter valid when a particular class has no master data.
        public static EnemyDataSO CreateEnemy(string mode, int stage, int spawnIndex)
        {
            int index = Index(stage);
            bool elite = mode == Champion;
            int desiredClass = DesiredEnemyClass(stage, elite ? stage : spawnIndex);
            var all = MasterDataManager.Instance?.GetAllMonsterData() ?? Array.Empty<MonsterDataSO>();
            var available = all.Where(m => m != null && !string.IsNullOrEmpty(m.monsterId) && m.raceId != "guardian").ToArray();
            var candidates = available.Where(m => m.classRank == desiredClass).OrderBy(m => m.monsterId, StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0 && available.Length > 0)
            {
                int nearest = available.OrderBy(m => Math.Abs(m.classRank - desiredClass)).First().classRank;
                candidates = available.Where(m => m.classRank == nearest).OrderBy(m => m.monsterId, StringComparer.Ordinal).ToArray();
            }
            var monster = candidates.Length > 0 ? candidates[(stage + spawnIndex) % candidates.Length] : null;
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.hideFlags = HideFlags.HideAndDontSave;
            enemy.enemyId = monster?.monsterId ?? "slime";
            enemy.enemyName = monster?.monsterName ?? "試練の敵";
            enemy.canBeRecruited = false;
            enemy.maxHp = elite ? ChampionHp[index] : Hp[index];
            enemy.attack = elite ? ChampionAttack[index] : Attack[index];
            enemy.magicAttack = enemy.attack;
            enemy.defense = Defense[index] + (elite ? stage * 2 : 0);
            enemy.magicDefense = enemy.defense;
            enemy.attackSpeed = elite ? .65f : .55f;
            enemy.critRate = .03f;
            enemy.critDamage = 1.3f;
            if (monster != null)
            {
                enemy.damageType = monster.damageType;
                enemy.attackRange = monster.attackRange;
                enemy.normalAttackTargetCount = monster.normalAttackTargetCount;
                enemy.battleIdleFacing = monster.battleIdleFacing;
                enemy.battleMoveFacing = monster.battleMoveFacing;
                enemy.battleAttackFacing = monster.battleAttackFacing;
            }
            return enemy;
        }
    }
}
