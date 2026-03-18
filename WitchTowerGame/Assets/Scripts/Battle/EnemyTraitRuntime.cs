namespace WitchTower.Battle
{
    public readonly struct EnemyTraitRuntime
    {
        public EnemyTraitRuntime(float attackMultiplier, float attackSpeedMultiplier, int defenseBonus, float critRateBonus, float lifeStealRate)
        {
            AttackMultiplier = attackMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            DefenseBonus = defenseBonus;
            CritRateBonus = critRateBonus;
            LifeStealRate = lifeStealRate;
        }

        public float AttackMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public int DefenseBonus { get; }
        public float CritRateBonus { get; }
        public float LifeStealRate { get; }
    }
}
