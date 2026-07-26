using System;

namespace WitchTower.Data
{
    public sealed class RebirthSkillDefinition
    {
        public RebirthSkillDefinition(
            string skillId,
            string displayName,
            string description,
            RebirthSkillEffectType effectType,
            float valuePerLevel,
            int maxLevel,
            int baseCost,
            int costPerLevel,
            string requiredSkillId = "",
            int requiredSkillLevel = 0,
            int treeColumn = 0,
            int treeRow = 0)
        {
            SkillId = skillId;
            DisplayName = displayName;
            Description = description;
            EffectType = effectType;
            ValuePerLevel = valuePerLevel;
            MaxLevel = Math.Max(1, maxLevel);
            BaseCost = Math.Max(0, baseCost);
            CostPerLevel = Math.Max(0, costPerLevel);
            RequiredSkillId = requiredSkillId ?? string.Empty;
            RequiredSkillLevel = Math.Max(0, requiredSkillLevel);
            TreeColumn = treeColumn;
            TreeRow = Math.Max(0, treeRow);
        }

        public string SkillId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public RebirthSkillEffectType EffectType { get; }
        public float ValuePerLevel { get; }
        public int MaxLevel { get; }
        public int BaseCost { get; }
        public int CostPerLevel { get; }
        public string RequiredSkillId { get; }
        public int RequiredSkillLevel { get; }
        public int TreeColumn { get; }
        public int TreeRow { get; }

        public bool HasRequirement => !string.IsNullOrEmpty(RequiredSkillId) && RequiredSkillLevel > 0;

        public int GetCost(int currentLevel)
        {
            if (currentLevel >= MaxLevel)
            {
                return 0;
            }

            return BaseCost + Math.Max(0, currentLevel) * CostPerLevel;
        }

        public float GetTotalValue(int currentLevel)
        {
            return ValuePerLevel * Math.Max(0, Math.Min(currentLevel, MaxLevel));
        }

        public int GetDisplayPercent(int currentLevel)
        {
            return (int)Math.Round(GetTotalValue(currentLevel) * 100f);
        }
    }
}
