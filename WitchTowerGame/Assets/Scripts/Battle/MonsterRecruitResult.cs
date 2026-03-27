namespace WitchTower.Battle
{
    public readonly struct MonsterRecruitResult
    {
        public MonsterRecruitResult(bool wasEligible, bool attempted, bool succeeded, string monsterId, string monsterName, string summary)
        {
            WasEligible = wasEligible;
            Attempted = attempted;
            Succeeded = succeeded;
            MonsterId = monsterId ?? string.Empty;
            MonsterName = monsterName ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public bool WasEligible { get; }
        public bool Attempted { get; }
        public bool Succeeded { get; }
        public string MonsterId { get; }
        public string MonsterName { get; }
        public string Summary { get; }

        public static MonsterRecruitResult Empty =>
            new MonsterRecruitResult(false, false, false, string.Empty, string.Empty, string.Empty);
    }
}
