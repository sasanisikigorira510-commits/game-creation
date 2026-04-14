namespace WitchTower.Battle
{
    public readonly struct BattleHitInfo
    {
        public BattleHitInfo(bool targetIsPlayer, int damage, bool isCritical, bool isSkill, bool causesKnockback, int targetIndex = -1, int attackerIndex = -1, float presentationDelay = 0f)
        {
            TargetIsPlayer = targetIsPlayer;
            Damage = damage;
            IsCritical = isCritical;
            IsSkill = isSkill;
            CausesKnockback = causesKnockback;
            TargetIndex = targetIndex;
            AttackerIndex = attackerIndex;
            PresentationDelay = presentationDelay;
        }

        public bool TargetIsPlayer { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
        public bool IsSkill { get; }
        public bool CausesKnockback { get; }
        public int TargetIndex { get; }
        public int AttackerIndex { get; }
        public float PresentationDelay { get; }
    }
}
