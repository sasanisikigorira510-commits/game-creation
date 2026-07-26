using UnityEngine;

namespace WitchTower.Battle
{
    public readonly struct BattleSpiritModifier
    {
        public BattleSpiritModifier(
            float maxHpMultiplier,
            float attackMultiplier,
            float wisdomMultiplier,
            float defenseMultiplier,
            float magicDefenseMultiplier,
            float attackSpeedMultiplier,
            float skillCooldownMultiplier,
            float guardDurationMultiplier,
            int guardDefenseBonus,
            float critRateBonus,
            float critDamageBonus,
            float goldRewardMultiplier,
            float expRewardMultiplier)
        {
            MaxHpMultiplier = maxHpMultiplier;
            AttackMultiplier = attackMultiplier;
            WisdomMultiplier = wisdomMultiplier;
            DefenseMultiplier = defenseMultiplier;
            MagicDefenseMultiplier = magicDefenseMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            SkillCooldownMultiplier = skillCooldownMultiplier;
            GuardDurationMultiplier = guardDurationMultiplier;
            GuardDefenseBonus = guardDefenseBonus;
            CritRateBonus = critRateBonus;
            CritDamageBonus = critDamageBonus;
            GoldRewardMultiplier = goldRewardMultiplier;
            ExpRewardMultiplier = expRewardMultiplier;
        }

        public float MaxHpMultiplier { get; }
        public float AttackMultiplier { get; }
        public float WisdomMultiplier { get; }
        public float DefenseMultiplier { get; }
        public float MagicDefenseMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public float SkillCooldownMultiplier { get; }
        public float GuardDurationMultiplier { get; }
        public int GuardDefenseBonus { get; }
        public float CritRateBonus { get; }
        public float CritDamageBonus { get; }
        public float GoldRewardMultiplier { get; }
        public float ExpRewardMultiplier { get; }

        public static BattleSpiritModifier Identity => new BattleSpiritModifier(
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            0,
            0f,
            0f,
            1f,
            1f);

        public BattleSpiritModifier Combine(BattleSpiritModifier other)
        {
            return new BattleSpiritModifier(
                MaxHpMultiplier * Mathf.Max(0.01f, other.MaxHpMultiplier),
                AttackMultiplier * Mathf.Max(0.01f, other.AttackMultiplier),
                WisdomMultiplier * Mathf.Max(0.01f, other.WisdomMultiplier),
                DefenseMultiplier * Mathf.Max(0.01f, other.DefenseMultiplier),
                MagicDefenseMultiplier * Mathf.Max(0.01f, other.MagicDefenseMultiplier),
                AttackSpeedMultiplier * Mathf.Max(0.01f, other.AttackSpeedMultiplier),
                SkillCooldownMultiplier * Mathf.Max(0.01f, other.SkillCooldownMultiplier),
                GuardDurationMultiplier * Mathf.Max(0.01f, other.GuardDurationMultiplier),
                GuardDefenseBonus + other.GuardDefenseBonus,
                CritRateBonus + other.CritRateBonus,
                CritDamageBonus + other.CritDamageBonus,
                GoldRewardMultiplier * Mathf.Max(0.01f, other.GoldRewardMultiplier),
                ExpRewardMultiplier * Mathf.Max(0.01f, other.ExpRewardMultiplier));
        }
    }
}
