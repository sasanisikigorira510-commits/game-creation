namespace WitchTower.Battle
{
    // One clock for displayed HP, contact, and the dedicated guardian effects.
    // These values change presentation only; simulation damage remains immediate.
    public static class GuardianAttackPresentationSettings
    {
        public const float ChargeDuration = .18f;
        public const float SeiryuTravelDuration = .40f;
        public const float SuzakuTravelDuration = .52f;
        public const float ImpactDuration = .36f;
        public static float ImpactDelay(string id) => ChargeDuration +
            (id == "suzaku" ? SuzakuTravelDuration : SeiryuTravelDuration);
    }
}
