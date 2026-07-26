using System.Collections.Generic;
using System.Linq;

namespace WitchTower.Data
{
    public static class RebirthSkillCatalog
    {
        public const string AttackPactId = "attack_pact";
        public const string HpOathId = "hp_oath";
        public const string ExpMemoryId = "exp_memory";
        public const string CriticalMarkId = "critical_mark";
        public const string DefenseOathId = "defense_oath";
        public const string GoldMemoryId = "gold_memory";
        public const string StrikeMasteryId = "strike_mastery";
        public const string DrainMasteryId = "drain_mastery";
        public const string TempoMemoryId = "tempo_memory";

        private static readonly RebirthSkillDefinition[] definitions =
        {
            new RebirthSkillDefinition(
                AttackPactId,
                "Attack Pact",
                "Attack +2% per level.",
                RebirthSkillEffectType.AttackMultiplier,
                0.02f,
                5,
                1,
                1),
            new RebirthSkillDefinition(
                HpOathId,
                "Vital Oath",
                "Max HP +3% per level.",
                RebirthSkillEffectType.HpMultiplier,
                0.03f,
                5,
                1,
                1),
            new RebirthSkillDefinition(
                ExpMemoryId,
                "Tower Memory",
                "Battle EXP +5% per level.",
                RebirthSkillEffectType.ExpRewardMultiplier,
                0.05f,
                5,
                1,
                1),
            new RebirthSkillDefinition(
                CriticalMarkId,
                "Critical Mark",
                "Critical rate +1% per level.",
                RebirthSkillEffectType.CritRateBonus,
                0.01f,
                5,
                1,
                1,
                AttackPactId,
                2),
            new RebirthSkillDefinition(
                DefenseOathId,
                "Iron Oath",
                "Defense +2% per level.",
                RebirthSkillEffectType.DefenseMultiplier,
                0.02f,
                5,
                1,
                1,
                HpOathId,
                2),
            new RebirthSkillDefinition(
                GoldMemoryId,
                "Coin Memory",
                "Battle gold +5% per level.",
                RebirthSkillEffectType.GoldRewardMultiplier,
                0.05f,
                5,
                1,
                1,
                ExpMemoryId,
                2),
            new RebirthSkillDefinition(
                StrikeMasteryId,
                "Strike Mastery",
                "Strike skill power +8% per level.",
                RebirthSkillEffectType.StrikePowerMultiplier,
                0.08f,
                5,
                2,
                1,
                CriticalMarkId,
                2),
            new RebirthSkillDefinition(
                DrainMasteryId,
                "Drain Mastery",
                "Drain healing +10% per level.",
                RebirthSkillEffectType.DrainHealMultiplier,
                0.10f,
                5,
                2,
                1,
                DefenseOathId,
                2),
            new RebirthSkillDefinition(
                TempoMemoryId,
                "Tempo Memory",
                "Attack speed +1% per level.",
                RebirthSkillEffectType.AttackSpeedMultiplier,
                0.01f,
                5,
                2,
                1,
                GoldMemoryId,
                2)
        };

        public static IReadOnlyList<RebirthSkillDefinition> Definitions => definitions;

        public static RebirthSkillDefinition GetDefinition(string skillId)
        {
            return definitions.FirstOrDefault(x => x.SkillId == skillId);
        }

        public static IEnumerable<RebirthSkillDefinition> GetDefinitionsForEffect(RebirthSkillEffectType effectType)
        {
            return definitions.Where(x => x.EffectType == effectType);
        }
    }
}
