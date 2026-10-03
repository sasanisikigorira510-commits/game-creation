using System.Collections.Generic;

namespace WitchTower.Monetization
{
    public sealed class IapProductDefinition
    {
        public IapProductDefinition(string productId, int paidStoneAmount, string fallbackPrice)
        {
            ProductId = productId;
            PaidStoneAmount = paidStoneAmount;
            FallbackPrice = fallbackPrice;
        }

        public string ProductId { get; }
        public int PaidStoneAmount { get; }
        public string FallbackPrice { get; }
    }

    public static class IapProductCatalog
    {
        public const string Crystals120 = "com.nasus.dungeonmonsterroguelike.crystals120";
        public const string Crystals650 = "com.nasus.dungeonmonsterroguelike.crystals650";
        public const string Crystals2000 = "com.nasus.dungeonmonsterroguelike.crystals2000";
        public const string Crystals4200 = "com.nasus.dungeonmonsterroguelike.crystals4200";
        public const string Crystals8600 = "com.nasus.dungeonmonsterroguelike.crystals8600";
        public const string Crystals15000 = "com.nasus.dungeonmonsterroguelike.crystals15000";

        private static readonly IapProductDefinition[] Definitions =
        {
            new IapProductDefinition(Crystals120, 120, "¥160"),
            new IapProductDefinition(Crystals650, 650, "¥800"),
            new IapProductDefinition(Crystals2000, 2000, "¥2,400"),
            new IapProductDefinition(Crystals4200, 4200, "¥4,800"),
            new IapProductDefinition(Crystals8600, 8600, "¥9,600"),
            new IapProductDefinition(Crystals15000, 15000, "¥16,000")
        };

        public static IReadOnlyList<IapProductDefinition> Products => Definitions;

        public static bool TryGet(string productId, out IapProductDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i += 1)
            {
                if (Definitions[i].ProductId == productId)
                {
                    definition = Definitions[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
