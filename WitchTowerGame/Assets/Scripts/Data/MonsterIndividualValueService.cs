using System;
using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Data
{
    public readonly struct MonsterIndividualValues
    {
        public MonsterIndividualValues(int hp, int attack, int wisdom, int defense, int magicDefense, int attackSpeed)
        {
            Hp = hp;
            Attack = attack;
            Wisdom = wisdom;
            Defense = defense;
            MagicDefense = magicDefense;
            AttackSpeed = attackSpeed;
        }

        public int Hp { get; }
        public int Attack { get; }
        public int Wisdom { get; }
        public int Defense { get; }
        public int MagicDefense { get; }
        public int AttackSpeed { get; }

        public int Average => Mathf.RoundToInt((Hp + Attack + Wisdom + Defense + MagicDefense + AttackSpeed) / 6f);
    }

    public static class MonsterIndividualValueService
    {
        public const int MinValue = 0;
        public const int MaxValue = 100;
        public const int DefaultValue = 50;

        private const float IntegerStatMinMultiplier = 0.85f;
        private const float IntegerStatMaxMultiplier = 1.15f;
        private const float SpeedMinMultiplier = 0.90f;
        private const float SpeedMaxMultiplier = 1.10f;

        private static readonly System.Random Random = new System.Random();

        public static MonsterIndividualValues Roll()
        {
            return new MonsterIndividualValues(
                RollOne(),
                RollOne(),
                RollOne(),
                RollOne(),
                RollOne(),
                RollOne());
        }

        public static MonsterIndividualValues Inherit(OwnedMonsterData parentA, OwnedMonsterData parentB)
        {
            EnsureInitialized(parentA);
            EnsureInitialized(parentB);
            return new MonsterIndividualValues(
                InheritOne(parentA?.IndividualHp ?? DefaultValue, parentB?.IndividualHp ?? DefaultValue),
                InheritOne(parentA?.IndividualAttack ?? DefaultValue, parentB?.IndividualAttack ?? DefaultValue),
                InheritOne(parentA?.IndividualWisdom ?? DefaultValue, parentB?.IndividualWisdom ?? DefaultValue),
                InheritOne(parentA?.IndividualDefense ?? DefaultValue, parentB?.IndividualDefense ?? DefaultValue),
                InheritOne(parentA?.IndividualMagicDefense ?? DefaultValue, parentB?.IndividualMagicDefense ?? DefaultValue),
                InheritOne(parentA?.IndividualAttackSpeed ?? DefaultValue, parentB?.IndividualAttackSpeed ?? DefaultValue));
        }

        public static void Apply(OwnedMonsterData monster, MonsterIndividualValues values)
        {
            if (monster == null)
            {
                return;
            }

            monster.HasIndividualValues = true;
            monster.IndividualHp = Clamp(values.Hp);
            monster.IndividualAttack = Clamp(values.Attack);
            monster.IndividualWisdom = Clamp(values.Wisdom);
            monster.IndividualDefense = Clamp(values.Defense);
            monster.IndividualMagicDefense = Clamp(values.MagicDefense);
            monster.IndividualAttackSpeed = Clamp(values.AttackSpeed);
        }

        public static void EnsureInitialized(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return;
            }

            if (monster.HasIndividualValues)
            {
                ClampExisting(monster);
                return;
            }

            Apply(monster, RollForExistingMonster(monster));
        }

        public static int GetAverage(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return DefaultValue;
            }

            EnsureInitialized(monster);
            return Mathf.RoundToInt((
                monster.IndividualHp +
                monster.IndividualAttack +
                monster.IndividualWisdom +
                monster.IndividualDefense +
                monster.IndividualMagicDefense +
                monster.IndividualAttackSpeed) / 6f);
        }

        public static float ResolveIntegerStatMultiplier(int individualValue)
        {
            return Mathf.Lerp(IntegerStatMinMultiplier, IntegerStatMaxMultiplier, Clamp(individualValue) / 100f);
        }

        public static float ResolveAttackSpeedMultiplier(int individualValue)
        {
            return Mathf.Lerp(SpeedMinMultiplier, SpeedMaxMultiplier, Clamp(individualValue) / 100f);
        }

        public static string BuildSummary(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "個体値 -";
            }

            EnsureInitialized(monster);
            return $"個体値 平均{GetAverage(monster)}  HP{monster.IndividualHp} 攻{monster.IndividualAttack} 魔{monster.IndividualWisdom} 防{monster.IndividualDefense} 魔防{monster.IndividualMagicDefense} 速{monster.IndividualAttackSpeed}";
        }

        public static string BuildAverageLabel(OwnedMonsterData monster)
        {
            return monster == null ? "IV-" : $"IV{GetAverage(monster)}";
        }

        private static int RollOne()
        {
            return Random.Next(MinValue, MaxValue + 1);
        }

        private static MonsterIndividualValues RollForExistingMonster(OwnedMonsterData monster)
        {
            var stableRandom = new System.Random(BuildStableSeed(monster));
            return new MonsterIndividualValues(
                stableRandom.Next(MinValue, MaxValue + 1),
                stableRandom.Next(MinValue, MaxValue + 1),
                stableRandom.Next(MinValue, MaxValue + 1),
                stableRandom.Next(MinValue, MaxValue + 1),
                stableRandom.Next(MinValue, MaxValue + 1),
                stableRandom.Next(MinValue, MaxValue + 1));
        }

        private static int InheritOne(int first, int second)
        {
            return Random.Next(0, 2) == 0
                ? Clamp(first)
                : Clamp(second);
        }

        private static void ClampExisting(OwnedMonsterData monster)
        {
            monster.IndividualHp = Clamp(monster.IndividualHp);
            monster.IndividualAttack = Clamp(monster.IndividualAttack);
            monster.IndividualWisdom = Clamp(monster.IndividualWisdom);
            monster.IndividualDefense = Clamp(monster.IndividualDefense);
            monster.IndividualMagicDefense = Clamp(monster.IndividualMagicDefense);
            monster.IndividualAttackSpeed = Clamp(monster.IndividualAttackSpeed);
        }

        private static int Clamp(int value)
        {
            return Math.Max(MinValue, Math.Min(MaxValue, value));
        }

        private static int BuildStableSeed(OwnedMonsterData monster)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(monster?.InstanceId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(monster?.MonsterId ?? string.Empty);
                hash = (hash * 31) + (monster?.AcquiredOrder ?? 0);
                return hash == int.MinValue ? 0 : Math.Abs(hash);
            }
        }
    }
}
