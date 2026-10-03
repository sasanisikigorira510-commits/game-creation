namespace WitchTower.Battle
{
    public enum MonsterRecruitBlockReason
    {
        None,
        ProfileUnavailable,
        Tutorial,
        StorageFull
    }

    public readonly struct MonsterRecruitResult
    {
        public MonsterRecruitResult(
            bool wasEligible,
            bool attempted,
            bool succeeded,
            string monsterId,
            string monsterName,
            string summary,
            int individualAverage = -1,
            string individualSummary = "",
            bool autoReleased = false,
            int autoReleaseThreshold = -1,
            MonsterRecruitBlockReason blockReason = MonsterRecruitBlockReason.None)
        {
            WasEligible = wasEligible;
            Attempted = attempted;
            Succeeded = succeeded;
            MonsterId = monsterId ?? string.Empty;
            MonsterName = monsterName ?? string.Empty;
            Summary = summary ?? string.Empty;
            IndividualAverage = individualAverage;
            IndividualSummary = individualSummary ?? string.Empty;
            AutoReleased = autoReleased;
            AutoReleaseThreshold = autoReleaseThreshold;
            BlockReason = blockReason;
        }

        public bool WasEligible { get; }
        public bool Attempted { get; }
        public bool Succeeded { get; }
        public string MonsterId { get; }
        public string MonsterName { get; }
        public string Summary { get; }
        public int IndividualAverage { get; }
        public string IndividualSummary { get; }
        public bool AutoReleased { get; }
        public int AutoReleaseThreshold { get; }
        public MonsterRecruitBlockReason BlockReason { get; }

        public static MonsterRecruitResult Empty =>
            new MonsterRecruitResult(false, false, false, string.Empty, string.Empty, string.Empty);
    }
}
