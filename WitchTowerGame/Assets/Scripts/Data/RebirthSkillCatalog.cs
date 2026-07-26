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
        public const string TempoMemoryId = "tempo_memory";
        public const string DeepMemoryId = "deep_memory";
        public const string GreatTreeBlessingId = "great_tree_blessing";

        private static readonly RebirthSkillDefinition[] definitions =
        {
            new RebirthSkillDefinition(
                AttackPactId,
                "攻契約",
                "味方全体の攻撃と魔力 +2%。",
                RebirthSkillEffectType.AttackMultiplier,
                0.02f,
                5,
                1,
                1,
                treeColumn: 0,
                treeRow: 2),
            new RebirthSkillDefinition(
                CriticalMarkId,
                "会心刻印",
                "味方全体の会心率 +2%。",
                RebirthSkillEffectType.CritRateBonus,
                0.02f,
                4,
                2,
                1,
                AttackPactId,
                3,
                treeColumn: 0,
                treeRow: 1),
            new RebirthSkillDefinition(
                TempoMemoryId,
                "巡回加速",
                "味方全体の攻撃速度 +3%。",
                RebirthSkillEffectType.AttackSpeedMultiplier,
                0.03f,
                3,
                4,
                2,
                CriticalMarkId,
                4,
                treeColumn: 0,
                treeRow: 0),
            new RebirthSkillDefinition(
                ExpMemoryId,
                "塔の記憶",
                "戦闘で得る経験値 +5%。",
                RebirthSkillEffectType.ExpRewardMultiplier,
                0.05f,
                5,
                1,
                1,
                treeColumn: 1,
                treeRow: 2),
            new RebirthSkillDefinition(
                GoldMemoryId,
                "金脈の記憶",
                "戦闘で得るゴールド +8%。",
                RebirthSkillEffectType.GoldRewardMultiplier,
                0.08f,
                4,
                2,
                1,
                ExpMemoryId,
                3,
                treeColumn: 1,
                treeRow: 1),
            new RebirthSkillDefinition(
                DeepMemoryId,
                "深層の記憶",
                "戦闘で得る経験値 +12%。",
                RebirthSkillEffectType.ExpRewardMultiplier,
                0.12f,
                3,
                4,
                2,
                GoldMemoryId,
                4,
                treeColumn: 1,
                treeRow: 0),
            new RebirthSkillDefinition(
                HpOathId,
                "命契約",
                "味方全体の最大HP +3%。",
                RebirthSkillEffectType.HpMultiplier,
                0.03f,
                5,
                1,
                1,
                treeColumn: 2,
                treeRow: 2),
            new RebirthSkillDefinition(
                DefenseOathId,
                "守契約",
                "味方全体の防御と魔防 +4%。",
                RebirthSkillEffectType.DefenseMultiplier,
                0.04f,
                4,
                2,
                1,
                HpOathId,
                3,
                treeColumn: 2,
                treeRow: 1),
            new RebirthSkillDefinition(
                GreatTreeBlessingId,
                "大樹の加護",
                "味方全体の最大HP +6%。",
                RebirthSkillEffectType.HpMultiplier,
                0.06f,
                3,
                4,
                2,
                DefenseOathId,
                4,
                treeColumn: 2,
                treeRow: 0)
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
