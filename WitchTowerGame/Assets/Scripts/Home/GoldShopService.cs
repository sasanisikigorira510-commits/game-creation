using System;
using System.Collections.Generic;
using WitchTower.Data;

namespace WitchTower.Home
{
    public enum GoldShopRewardType
    {
        PlayerExp,
        FreeGachaStones,
        Equipment,
        EnhancementRelic,
        MonsterStorage
    }

    public readonly struct GoldShopProductDefinition
    {
        public GoldShopProductDefinition(
            string id,
            string title,
            string description,
            int cost,
            GoldShopRewardType rewardType,
            int amount,
            string equipmentId = "",
            string relicId = "")
        {
            Id = id;
            Title = title;
            Description = description;
            Cost = cost;
            RewardType = rewardType;
            Amount = amount;
            EquipmentId = equipmentId ?? string.Empty;
            RelicId = relicId ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public int Cost { get; }
        public GoldShopRewardType RewardType { get; }
        public int Amount { get; }
        public string EquipmentId { get; }
        public string RelicId { get; }
    }

    public static class GoldShopService
    {
        private static readonly IReadOnlyList<GoldShopProductDefinition> Products =
            new List<GoldShopProductDefinition>
            {
                new GoldShopProductDefinition(
                    "safe_ember",
                    "通常遺物",
                    "装備強化用の通常遺物 x1",
                    500,
                    GoldShopRewardType.EnhancementRelic,
                    1,
                    relicId: "relic_safe_ember"),
                new GoldShopProductDefinition(
                    "safe_ember_bundle",
                    "通常遺物セット",
                    "装備強化用の通常遺物 x3",
                    1200,
                    GoldShopRewardType.EnhancementRelic,
                    3,
                    relicId: "relic_safe_ember"),
                new GoldShopProductDefinition(
                    "risky_ember",
                    "上級遺物",
                    "50%で基礎効果を+15%する遺物 x1",
                    1500,
                    GoldShopRewardType.EnhancementRelic,
                    1,
                    relicId: "relic_risky_ember"),
                new GoldShopProductDefinition(
                    "volatile_ember",
                    "危険遺物",
                    "35%で基礎効果を+30%、失敗時に装備が消滅する遺物 x1",
                    3000,
                    GoldShopRewardType.EnhancementRelic,
                    1,
                    relicId: "relic_volatile_ember")
            };

        public static IReadOnlyList<GoldShopProductDefinition> GetProducts()
        {
            return Products;
        }

        public static bool TryPurchase(PlayerProfile profile, string productId, out string message)
        {
            if (profile == null)
            {
                message = "プレイヤーデータを読み込めませんでした。";
                return false;
            }

            GoldShopProductDefinition? product = FindProduct(productId);
            if (!product.HasValue)
            {
                message = "商品が見つかりません。";
                return false;
            }

            GoldShopProductDefinition definition = product.Value;
            if (definition.RewardType == GoldShopRewardType.Equipment && !profile.HasEquipmentStorageSpace())
            {
                message = "装備枠がいっぱいです。永続強化ショップで装備枠を拡張できます。";
                return false;
            }

            if (!profile.TrySpendGold(definition.Cost))
            {
                message = $"ゴールドが不足しています。あと{Math.Max(0, definition.Cost - profile.Gold):N0}必要です。";
                return false;
            }

            bool granted = GrantReward(profile, definition);
            if (!granted)
            {
                profile.AddGold(definition.Cost);
                message = "商品を受け取れなかったため、ゴールドを返却しました。";
                return false;
            }

            message = $"{definition.Title}を購入しました。";
            return true;
        }

        private static GoldShopProductDefinition? FindProduct(string productId)
        {
            foreach (GoldShopProductDefinition product in Products)
            {
                if (string.Equals(product.Id, productId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        private static bool GrantReward(PlayerProfile profile, GoldShopProductDefinition product)
        {
            switch (product.RewardType)
            {
                case GoldShopRewardType.PlayerExp:
                    profile.AddExp(product.Amount);
                    return true;
                case GoldShopRewardType.FreeGachaStones:
                    profile.AddFreeGachaStones(product.Amount);
                    return true;
                case GoldShopRewardType.Equipment:
                    return profile.AddOwnedEquipment(product.EquipmentId);
                case GoldShopRewardType.EnhancementRelic:
                    if (string.IsNullOrEmpty(product.RelicId) || product.Amount <= 0)
                    {
                        return false;
                    }

                    profile.AddEnhancementRelics(product.RelicId, product.Amount);
                    return true;
                case GoldShopRewardType.MonsterStorage:
                    profile.MonsterStorageLimit += product.Amount;
                    return true;
                default:
                    return false;
            }
        }
    }
}
