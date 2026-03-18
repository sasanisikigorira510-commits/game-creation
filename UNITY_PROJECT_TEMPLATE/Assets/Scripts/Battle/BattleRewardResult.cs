namespace WitchTower.Battle
{
    public readonly struct BattleRewardResult
    {
        public BattleRewardResult(int gold, int exp)
        {
            Gold = gold;
            Exp = exp;
        }

        public int Gold { get; }
        public int Exp { get; }
    }
}
