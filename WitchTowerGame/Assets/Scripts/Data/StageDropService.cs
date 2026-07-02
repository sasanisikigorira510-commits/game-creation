using System;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public static class StageDropService
    {
        public const double TargetEquipmentDropsPerStage = 1d;
        public const double EnhancementRelicDropRatePerKill = 2d / 1001d;
        public const double CommonEquipmentQualityRate = 60d;
        public const double UncommonEquipmentQualityRate = 25d;
        public const double RareEquipmentQualityRate = 10d;
        public const double EpicEquipmentQualityRate = 4d;
        public const double LegendaryEquipmentQualityRate = 1d;

        public static double ResolveEquipmentDropRatePerRoll(int stageEnemyCount)
        {
            int rollCount = Math.Max(1, stageEnemyCount);
            return Math.Min(1d, TargetEquipmentDropsPerStage / rollCount);
        }

        public static bool TryRollEquipmentDrop(int stageEnemyCount, Random random)
        {
            Random rng = random ?? new Random();
            double threshold = ResolveEquipmentDropRatePerRoll(stageEnemyCount);
            return rng.NextDouble() <= threshold;
        }

        public static EquipmentRarity RollEquipmentQuality(Random random)
        {
            Random rng = random ?? new Random();
            double roll = rng.NextDouble() * 100d;
            if (roll < CommonEquipmentQualityRate)
            {
                return EquipmentRarity.Common;
            }

            roll -= CommonEquipmentQualityRate;
            if (roll < UncommonEquipmentQualityRate)
            {
                return EquipmentRarity.Uncommon;
            }

            roll -= UncommonEquipmentQualityRate;
            if (roll < RareEquipmentQualityRate)
            {
                return EquipmentRarity.Rare;
            }

            roll -= RareEquipmentQualityRate;
            return roll < EpicEquipmentQualityRate
                ? EquipmentRarity.Epic
                : EquipmentRarity.Legendary;
        }

        public static int ResolveEquipmentAutoSellGold(EquipmentRarity quality)
        {
            switch (quality)
            {
                case EquipmentRarity.Legendary:
                    return 900;
                case EquipmentRarity.Epic:
                    return 360;
                case EquipmentRarity.Rare:
                    return 150;
                case EquipmentRarity.Uncommon:
                    return 60;
                case EquipmentRarity.Common:
                default:
                    return 25;
            }
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
