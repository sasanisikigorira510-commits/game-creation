using System;
using WitchTower.Save;

namespace WitchTower.Data
{
    public enum MonsterTrainingStatType
    {
        Hp = 0,
        Attack = 1,
        Wisdom = 2,
        Defense = 3,
        MagicDefense = 4,
        AttackSpeed = 5
    }

    public enum MonsterTrainingStatus
    {
        Success = 0,
        InvalidProfile = 1,
        InvalidMonster = 2,
        InvalidStat = 3,
        LevelCapReached = 4,
        InsufficientDrops = 5,
        ChallengeActive = 6
    }

    public sealed class MonsterTrainingResult
    {
        internal MonsterTrainingResult(MonsterTrainingStatus status, int previousLevel = 0, int newLevel = 0, int cost = 0, string message = "")
        {
            Status = status;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            Cost = cost;
            Message = message;
        }

        public bool Succeeded => Status == MonsterTrainingStatus.Success;
        public MonsterTrainingStatus Status { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public int Cost { get; }
        public string Message { get; }
    }

    /// <summary>
    /// 修練の雫の実装既定値（Lv10への拡張案）。6項目を個体ごとにLv0からLv10まで育成する。
    /// 各段階の費用は1,2,4,6,9,12,16,20,25,30個（1項目125個、全6項目750個）。
    /// 基礎能力＋Lv成長分へ修練Lvごとに3%、攻速は2%を先天個体値倍率に加算する。
    /// 配合時は各項目の親の高い修練Lvを保証継承し、合算しない。
    /// 保存・オンライン取引は呼び出し側が行う。失敗時に雫や対象の値を変更しない。
    /// </summary>
    public static class MonsterTrainingService
    {
        public const int MaxTrainingLevel = 10;
        public const float IntegerStatBonusPerLevel = 0.03f;
        public const float AttackSpeedBonusPerLevel = 0.02f;

        private static readonly int[] Costs = { 1, 2, 4, 6, 9, 12, 16, 20, 25, 30 };

        public static int NormalizeLevel(int level) => Math.Max(0, Math.Min(MaxTrainingLevel, level));

        public static int GetCostForNextLevel(int currentLevel)
        {
            int level = NormalizeLevel(currentLevel);
            return level < MaxTrainingLevel ? Costs[level] : 0;
        }

        public static float GetIntegerStatBonus(int trainingLevel) => NormalizeLevel(trainingLevel) * IntegerStatBonusPerLevel;
        public static float GetAttackSpeedBonus(int trainingLevel) => NormalizeLevel(trainingLevel) * AttackSpeedBonusPerLevel;

        public static int GetLevel(OwnedMonsterData monster, MonsterTrainingStatType stat)
        {
            if (monster == null) return 0;
            switch (stat)
            {
                case MonsterTrainingStatType.Hp: return NormalizeLevel(monster.TrainingHp);
                case MonsterTrainingStatType.Attack: return NormalizeLevel(monster.TrainingAttack);
                case MonsterTrainingStatType.Wisdom: return NormalizeLevel(monster.TrainingWisdom);
                case MonsterTrainingStatType.Defense: return NormalizeLevel(monster.TrainingDefense);
                case MonsterTrainingStatType.MagicDefense: return NormalizeLevel(monster.TrainingMagicDefense);
                case MonsterTrainingStatType.AttackSpeed: return NormalizeLevel(monster.TrainingAttackSpeed);
                default: return 0;
            }
        }

        public static void Normalize(OwnedMonsterData monster)
        {
            if (monster == null) return;
            monster.TrainingHp = NormalizeLevel(monster.TrainingHp);
            monster.TrainingAttack = NormalizeLevel(monster.TrainingAttack);
            monster.TrainingWisdom = NormalizeLevel(monster.TrainingWisdom);
            monster.TrainingDefense = NormalizeLevel(monster.TrainingDefense);
            monster.TrainingMagicDefense = NormalizeLevel(monster.TrainingMagicDefense);
            monster.TrainingAttackSpeed = NormalizeLevel(monster.TrainingAttackSpeed);
        }

        public static void Inherit(OwnedMonsterData parentA, OwnedMonsterData parentB, OwnedMonsterData child)
        {
            if (child == null) return;
            for (int index = 0; index < 6; index++)
            {
                var stat = (MonsterTrainingStatType)index;
                SetLevel(child, stat, Math.Max(GetLevel(parentA, stat), GetLevel(parentB, stat)));
            }
        }

        public static MonsterTrainingResult TryTrain(PlayerProfile profile, string instanceId, MonsterTrainingStatType stat)
        {
            if (profile == null)
                return new MonsterTrainingResult(MonsterTrainingStatus.InvalidProfile, message: "プレイヤー情報がありません。");
            if ((int)stat < 0 || (int)stat > 5)
                return new MonsterTrainingResult(MonsterTrainingStatus.InvalidStat, message: "修練する能力が不正です。");
            if (string.IsNullOrWhiteSpace(instanceId))
                return new MonsterTrainingResult(MonsterTrainingStatus.InvalidMonster, message: "修練するモンスターが見つかりません。");
            OwnedMonsterData monster = profile.GetOwnedMonster(instanceId);
            if (monster == null)
                return new MonsterTrainingResult(MonsterTrainingStatus.InvalidMonster, message: "修練するモンスターを所持していません。");
            if (profile.DailyChallenges?.ActiveRun?.IsActive == true)
                return new MonsterTrainingResult(MonsterTrainingStatus.ChallengeActive, message: "デイリー試練の挑戦中は修練できません。試練を終了してから操作してください。");

            int previousLevel = GetLevel(monster, stat);
            if (previousLevel >= MaxTrainingLevel)
                return new MonsterTrainingResult(MonsterTrainingStatus.LevelCapReached, previousLevel, previousLevel, message: "修練Lvは最大です。");
            int cost = GetCostForNextLevel(previousLevel);
            int availableDrops = Math.Max(0, profile.TrainingDrops);
            if (availableDrops < cost)
                return new MonsterTrainingResult(MonsterTrainingStatus.InsufficientDrops, previousLevel, previousLevel, cost, "修練の雫が足りません。");

            // All values are bounded before either write: no overflow or partial consumption.
            int newLevel = previousLevel + 1;
            int remainingDrops = availableDrops - cost;
            Normalize(monster);
            SetLevel(monster, stat, newLevel);
            profile.TrainingDrops = remainingDrops;
            return new MonsterTrainingResult(MonsterTrainingStatus.Success, previousLevel, newLevel, cost, "修練Lvが上がりました。");
        }

        private static void SetLevel(OwnedMonsterData monster, MonsterTrainingStatType stat, int level)
        {
            switch (stat)
            {
                case MonsterTrainingStatType.Hp: monster.TrainingHp = level; break;
                case MonsterTrainingStatType.Attack: monster.TrainingAttack = level; break;
                case MonsterTrainingStatType.Wisdom: monster.TrainingWisdom = level; break;
                case MonsterTrainingStatType.Defense: monster.TrainingDefense = level; break;
                case MonsterTrainingStatType.MagicDefense: monster.TrainingMagicDefense = level; break;
                case MonsterTrainingStatType.AttackSpeed: monster.TrainingAttackSpeed = level; break;
            }
        }
    }
}
