using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class EnemyTraitResolver
    {
        public static EnemyTraitRuntime Resolve(EnemyTrait trait)
        {
            return trait switch
            {
                EnemyTrait.HighDefense => new EnemyTraitRuntime(1.0f, 1.0f, 4, 0f, 0f),
                EnemyTrait.FastAttack => new EnemyTraitRuntime(1.0f, 1.35f, 0, 0f, 0f),
                EnemyTrait.Drain => new EnemyTraitRuntime(1.0f, 1.0f, 0, 0f, 0.25f),
                EnemyTrait.Critical => new EnemyTraitRuntime(1.0f, 1.0f, 0, 0.15f, 0f),
                _ => new EnemyTraitRuntime(1.0f, 1.0f, 0, 0f, 0f)
            };
        }
    }
}
