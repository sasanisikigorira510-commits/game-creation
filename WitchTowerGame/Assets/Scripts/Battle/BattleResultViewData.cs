namespace WitchTower.Battle
{
    public readonly struct BattleResultViewData
    {
        public BattleResultViewData(bool isWin, int gold, int exp, int nextFloor, string recruitSummary)
        {
            IsWin = isWin;
            Gold = gold;
            Exp = exp;
            NextFloor = nextFloor;
            RecruitSummary = recruitSummary ?? string.Empty;
        }

        public bool IsWin { get; }
        public int Gold { get; }
        public int Exp { get; }
        public int NextFloor { get; }
        public string RecruitSummary { get; }
    }
}
