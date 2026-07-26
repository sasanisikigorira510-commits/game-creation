using System;

namespace WitchTower.Data
{
    public static class RebirthService
    {
        public const int MinimumLevel = 10;

        public static int CalculateRebirthPointReward(PlayerProfile profile)
        {
            if (profile == null || profile.Level < MinimumLevel)
            {
                return 0;
            }

            return Math.Max(1, profile.Level / 10 + profile.HighestFloor / 10);
        }

        public static bool CanRebirth(PlayerProfile profile)
        {
            return CalculateRebirthPointReward(profile) > 0;
        }

        public static bool TryRebirth(PlayerProfile profile, out int gainedPoints)
        {
            gainedPoints = 0;
            if (!CanRebirth(profile))
            {
                return false;
            }

            gainedPoints = profile.ApplyRebirth();
            return gainedPoints > 0;
        }

        public static bool CanPurchaseSkill(PlayerProfile profile, string skillId, out string blockedReason)
        {
            blockedReason = string.Empty;
            var definition = RebirthSkillCatalog.GetDefinition(skillId);
            if (profile == null)
            {
                blockedReason = "No profile";
                return false;
            }

            if (definition == null)
            {
                blockedReason = "Unknown skill";
                return false;
            }

            var currentLevel = profile.GetRebirthSkillLevel(skillId);
            if (currentLevel >= definition.MaxLevel)
            {
                blockedReason = "Max level";
                return false;
            }

            if (definition.HasRequirement &&
                profile.GetRebirthSkillLevel(definition.RequiredSkillId) < definition.RequiredSkillLevel)
            {
                var requiredDefinition = RebirthSkillCatalog.GetDefinition(definition.RequiredSkillId);
                var requiredName = requiredDefinition != null ? requiredDefinition.DisplayName : definition.RequiredSkillId;
                blockedReason = $"Requires {requiredName} Lv. {definition.RequiredSkillLevel}";
                return false;
            }

            var cost = definition.GetCost(currentLevel);
            if (profile.RebirthPoints < cost)
            {
                blockedReason = $"Need {cost} Soul";
                return false;
            }

            return true;
        }

        public static bool TryPurchaseSkill(PlayerProfile profile, string skillId, out string blockedReason)
        {
            if (!CanPurchaseSkill(profile, skillId, out blockedReason))
            {
                return false;
            }

            var definition = RebirthSkillCatalog.GetDefinition(skillId);
            var currentLevel = profile.GetRebirthSkillLevel(skillId);
            var cost = definition.GetCost(currentLevel);
            if (!profile.TrySpendRebirthPoints(cost))
            {
                blockedReason = $"Need {cost} Soul";
                return false;
            }

            profile.SetRebirthSkillLevel(skillId, currentLevel + 1);
            return true;
        }
    }
}
