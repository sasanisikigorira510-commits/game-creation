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

        private static readonly RebirthSkillDefinition[] definitions = new RebirthSkillDefinition[0];

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
