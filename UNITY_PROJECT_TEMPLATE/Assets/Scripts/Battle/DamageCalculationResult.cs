namespace WitchTower.Battle
{
    public readonly struct DamageCalculationResult
    {
        public DamageCalculationResult(int damage, bool isCritical)
        {
            Damage = damage;
            IsCritical = isCritical;
        }

        public int Damage { get; }
        public bool IsCritical { get; }
    }
}
