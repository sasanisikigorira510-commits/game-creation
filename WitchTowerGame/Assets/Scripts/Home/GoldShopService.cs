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
            string equipmentId = "")
        {
            Id = id;
            Title = title;
            Description = description;
            Cost = cost;
            RewardType = rewardType;
            Amount = amount;
            EquipmentId = equipmentId ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public int Cost { get; }
        public GoldShopRewardType RewardType { get; }
        public int Amount { get; }
        public string EquipmentId { get; }
    }

    public static class GoldShopService
    {
        private static readonly IReadOnlyList<GoldShopProductDefinition> Products =
            new List<GoldShopProductDefinition>
            {
                new GoldShopProductDefinition(
                    "player_training_book",
                    "冒険者の修練書",
                    "プレイヤー経験値 +100",
                    100,
                    GoldShopRewardType.PlayerExp,
                    100),
                new GoldShopProductDefinition(
                    "free_stone_pouch",
                    "無料石の小袋",
                    "無料石 +50",
                    500,
                    GoldShopRewardType.FreeGachaStones,
                    50),
                new GoldShopProductDefinition(
                    "iron_sword",
                    "鉄の剣",
                    "装備を1個獲得",
                    800,
                    GoldShopRewardType.Equipment,
                    1,
                    "equip_iron_sword"),
                new GoldShopProductDefinition(
                    "bone_mail",
                    "骨の鎧",
                    "装備を1個獲得",
                    800,
                    GoldShopRewardType.Equipment,
                    1,
                    "equip_bone_mail"),
                new GoldShopProductDefinition(
                    "quick_charm",
                    "迅速のお守り",
                    "装備を1個獲得",
                    800,
                    GoldShopRewardType.Equipment,
                    1,
                    "equip_quick_charm"),
                new GoldShopProductDefinition(
                    "monster_storage_10",
                    "モンスター枠拡張",
                    "所持上限 +10",
                    1000,
                    GoldShopRewardType.MonsterStorage,
                    10)
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
                case GoldShopRewardType.MonsterStorage:
                    profile.MonsterStorageLimit += product.Amount;
                    return true;
                default:
                    return false;
            }
        }
    }
}
