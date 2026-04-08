using System;
using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public enum EquipmentBalanceStatFamily
    {
        WeaponPercent = 0,
        ArmorPercent = 1,
        AccessoryAttackLike = 2,
        AccessoryDefenseLike = 3,
        AccessoryHp = 4,
        AccessoryAttackSpeed = 5
    }

    public static class EquipmentBalanceTable
    {
        public static Vector2Int ResolveRange(EquipmentRarity rarity, EquipmentBalanceStatFamily family)
        {
            switch (family)
            {
                case EquipmentBalanceStatFamily.WeaponPercent:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(8, 12),
                        EquipmentRarity.Uncommon => new Vector2Int(12, 16),
                        EquipmentRarity.Rare => new Vector2Int(16, 20),
                        EquipmentRarity.Epic => new Vector2Int(20, 24),
                        EquipmentRarity.Legendary => new Vector2Int(24, 28),
                        _ => new Vector2Int(8, 12)
                    };
                case EquipmentBalanceStatFamily.ArmorPercent:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(10, 14),
                        EquipmentRarity.Uncommon => new Vector2Int(14, 18),
                        EquipmentRarity.Rare => new Vector2Int(18, 22),
                        EquipmentRarity.Epic => new Vector2Int(22, 26),
                        EquipmentRarity.Legendary => new Vector2Int(26, 30),
                        _ => new Vector2Int(10, 14)
                    };
                case EquipmentBalanceStatFamily.AccessoryAttackLike:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(3, 5),
                        EquipmentRarity.Uncommon => new Vector2Int(5, 8),
                        EquipmentRarity.Rare => new Vector2Int(8, 12),
                        EquipmentRarity.Epic => new Vector2Int(12, 18),
                        EquipmentRarity.Legendary => new Vector2Int(18, 25),
                        _ => new Vector2Int(3, 5)
                    };
                case EquipmentBalanceStatFamily.AccessoryDefenseLike:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(2, 4),
                        EquipmentRarity.Uncommon => new Vector2Int(4, 6),
                        EquipmentRarity.Rare => new Vector2Int(6, 10),
                        EquipmentRarity.Epic => new Vector2Int(10, 14),
                        EquipmentRarity.Legendary => new Vector2Int(14, 20),
                        _ => new Vector2Int(2, 4)
                    };
                case EquipmentBalanceStatFamily.AccessoryHp:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(15, 25),
                        EquipmentRarity.Uncommon => new Vector2Int(25, 40),
                        EquipmentRarity.Rare => new Vector2Int(40, 60),
                        EquipmentRarity.Epic => new Vector2Int(60, 90),
                        EquipmentRarity.Legendary => new Vector2Int(90, 130),
                        _ => new Vector2Int(15, 25)
                    };
                case EquipmentBalanceStatFamily.AccessoryAttackSpeed:
                    return rarity switch
                    {
                        EquipmentRarity.Common => new Vector2Int(5, 5),
                        EquipmentRarity.Uncommon => new Vector2Int(8, 8),
                        EquipmentRarity.Rare => new Vector2Int(12, 12),
                        EquipmentRarity.Epic => new Vector2Int(16, 16),
                        EquipmentRarity.Legendary => new Vector2Int(20, 20),
                        _ => new Vector2Int(5, 5)
                    };
                default:
                    return new Vector2Int(0, 0);
            }
        }

        public static int RollValue(EquipmentRarity rarity, EquipmentBalanceStatFamily family, System.Random random)
        {
            Vector2Int range = ResolveRange(rarity, family);
            if (range.x == range.y)
            {
                return range.x;
            }

            System.Random rng = random ?? new System.Random();
            return rng.Next(range.x, range.y + 1);
        }
    }
}
