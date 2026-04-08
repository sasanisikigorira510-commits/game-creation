using System;

namespace WitchTower.Data
{
    public static class StageDropService
    {
        public const double NormalEnemyEquipmentDropRate = 0.0015d;
        public const double BossEquipmentDropRate = 0.10d;
        public const double EnhancementRelicDropRatePerKill = 2d / 1001d;

        public static bool TryRollEquipmentDrop(bool isBossEnemy, Random random)
        {
            Random rng = random ?? new Random();
            double threshold = isBossEnemy ? BossEquipmentDropRate : NormalEnemyEquipmentDropRate;
            return rng.NextDouble() <= threshold;
        }

        public static bool TryRollEnhancementRelic(Random random, out string relicId)
        {
            relicId = string.Empty;
            Random rng = random ?? new Random();
            if (rng.NextDouble() > EnhancementRelicDropRatePerKill)
            {
                return false;
            }

            double roll = rng.NextDouble() * 100d;
            if (roll < 96d)
            {
                relicId = "relic_safe_ember";
                return true;
            }

            if (roll < 99.5d)
            {
                relicId = "relic_risky_ember";
                return true;
            }

            relicId = "relic_volatile_ember";
            return true;
        }
    }
}
