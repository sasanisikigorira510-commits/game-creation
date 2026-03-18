namespace WitchTower.Battle
{
    public readonly struct BattleHitInfo
    {
        public BattleHitInfo(bool targetIsPlayer, int damage, bool isCritical, bool isSkill)
        {
            TargetIsPlayer = targetIsPlayer;
            Damage = damage;
            IsCritical = isCritical;
            IsSkill = isSkill;
        }

        public bool TargetIsPlayer { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
        public bool IsSkill { get; }
    }
}
