namespace WitchTower.Data
{
    // Retain legacy entry points for serialized scenes. The removed soul tree
    // cannot reset progress, award its currency, or purchase nodes.
    public static class RebirthService
    {
        public const int MinimumLevel = 10;
        public static int CalculateRebirthPointReward(PlayerProfile profile) => 0;
        public static bool CanRebirth(PlayerProfile profile) => false;
        public static bool TryRebirth(PlayerProfile profile, out int gainedPoints) { gainedPoints = 0; return false; }
        public static bool CanPurchaseSkill(PlayerProfile profile, string skillId, out string blockedReason)
        { blockedReason = "この機能は終了しました"; return false; }
        public static bool TryPurchaseSkill(PlayerProfile profile, string skillId, out string blockedReason)
        { blockedReason = "この機能は終了しました"; return false; }
    }
}
