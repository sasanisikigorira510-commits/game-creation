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
            RebirthSkillDefinition definition = RebirthSkillCatalog.GetDefinition(skillId);
            if (profile == null)
            {
                blockedReason = "プレイヤーデータがありません";
                return false;
            }

            if (definition == null)
            {
                blockedReason = "不明な刻印です";
                return false;
            }

            int currentLevel = profile.GetRebirthSkillLevel(skillId);
            if (currentLevel >= definition.MaxLevel)
            {
                blockedReason = "最大Lv";
                return false;
            }

            if (definition.HasRequirement &&
                profile.GetRebirthSkillLevel(definition.RequiredSkillId) < definition.RequiredSkillLevel)
            {
                RebirthSkillDefinition requiredDefinition = RebirthSkillCatalog.GetDefinition(definition.RequiredSkillId);
                string requiredName = requiredDefinition != null ? requiredDefinition.DisplayName : definition.RequiredSkillId;
                blockedReason = $"{requiredName} Lv.{definition.RequiredSkillLevel} が必要";
                return false;
            }

            int cost = definition.GetCost(currentLevel);
            if (profile.RebirthPoints < cost)
            {
                blockedReason = $"魂片 {cost} 必要";
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

            RebirthSkillDefinition definition = RebirthSkillCatalog.GetDefinition(skillId);
            int currentLevel = profile.GetRebirthSkillLevel(skillId);
            int cost = definition.GetCost(currentLevel);
            if (!profile.TrySpendRebirthPoints(cost))
            {
                blockedReason = $"魂片 {cost} 必要";
                return false;
            }

            profile.SetRebirthSkillLevel(skillId, currentLevel + 1);
            return true;
        }
    }
}
