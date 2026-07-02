namespace WitchTower.Battle
{
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
            bool autoReleased = false,
            int autoReleaseThreshold = -1)
        {
            WasEligible = wasEligible;
            Attempted = attempted;
            Succeeded = succeeded;
            MonsterId = monsterId ?? string.Empty;
            MonsterName = monsterName ?? string.Empty;
            Summary = summary ?? string.Empty;
            IndividualAverage = individualAverage;
            AutoReleased = autoReleased;
            AutoReleaseThreshold = autoReleaseThreshold;
        }

        public bool WasEligible { get; }
        public bool Attempted { get; }
        public bool Succeeded { get; }
        public string MonsterId { get; }
        public string MonsterName { get; }
        public string Summary { get; }
        public int IndividualAverage { get; }
        public bool AutoReleased { get; }
        public int AutoReleaseThreshold { get; }

        public static MonsterRecruitResult Empty =>
            new MonsterRecruitResult(false, false, false, string.Empty, string.Empty, string.Empty);
    }
}
