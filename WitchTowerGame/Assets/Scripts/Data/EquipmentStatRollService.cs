using System;
using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public static class EquipmentStatRollService
    {
        private enum EquipmentAffinity
        {
            Physical,
            Magic,
            Mixed
        }

        private enum EquipmentRollStat
        {
            Attack,
            Wisdom,
            Defense,
            MagicDefense,
            Hp,
            CritRate,
            AttackSpeed
        }

        public static void RollInitialStats(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, System.Random random)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return;
            }

            System.Random rng = random ?? new System.Random();
            EquipmentRarity quality = ResolveQuality(ownedEquipment);
            float mainMultiplier = RollMainStatMultiplier(quality, rng);
            ResetRolledStats(ownedEquipment);

            RollMainStats(equipmentData, ownedEquipment, quality, mainMultiplier);
            RollSubStats(equipmentData, ownedEquipment, quality, rng);
            ownedEquipment.HasRolledStats = true;
        }

        public static void EnsureRolledStatFloors(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null || !ownedEquipment.HasRolledStats)
            {
                return;
            }

            EquipmentRarity quality = ResolveQuality(ownedEquipment);
            ownedEquipment.RolledAttack = EnsureRolledMainStatFloor(equipmentData, EquipmentRollStat.Attack, quality, ownedEquipment.RolledAttack);
            ownedEquipment.RolledWisdom = EnsureRolledMainStatFloor(equipmentData, EquipmentRollStat.Wisdom, quality, ownedEquipment.RolledWisdom);
            ownedEquipment.RolledDefense = EnsureRolledMainStatFloor(equipmentData, EquipmentRollStat.Defense, quality, ownedEquipment.RolledDefense);
            ownedEquipment.RolledMagicDefense = EnsureRolledMainStatFloor(equipmentData, EquipmentRollStat.MagicDefense, quality, ownedEquipment.RolledMagicDefense);
        }

        private static void RollMainStats(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, EquipmentRarity quality, float multiplier)
        {
            EquipmentAffinity affinity = ResolveAffinity(equipmentData);
            switch (equipmentData.slotType)
            {
                case EquipmentSlotType.Weapon:
                    if (affinity == EquipmentAffinity.Magic)
                    {
                        ownedEquipment.RolledWisdom = RollMainStat(equipmentData, EquipmentRollStat.Wisdom, quality, multiplier);
                    }
                    else
                    {
                        ownedEquipment.RolledAttack = RollMainStat(equipmentData, EquipmentRollStat.Attack, quality, multiplier);
                    }

                    ownedEquipment.RolledCritRate = RollFloat(equipmentData.bonusCritRate, multiplier);
                    ownedEquipment.RolledAttackSpeed = RollFloat(equipmentData.bonusAttackSpeed, multiplier);
                    break;
                case EquipmentSlotType.Armor:
                    if (affinity == EquipmentAffinity.Magic)
                    {
                        ownedEquipment.RolledMagicDefense = RollMainStat(equipmentData, EquipmentRollStat.MagicDefense, quality, multiplier);
                    }
                    else
                    {
                        ownedEquipment.RolledDefense = RollMainStat(equipmentData, EquipmentRollStat.Defense, quality, multiplier);
                    }

                    ownedEquipment.RolledHp = RollInt(equipmentData.baseHp, multiplier);
                    break;
                case EquipmentSlotType.Accessory:
                    if (AllowsStat(equipmentData, EquipmentRollStat.Attack))
                    {
                        ownedEquipment.RolledAttack = RollMainStat(equipmentData, EquipmentRollStat.Attack, quality, multiplier);
                    }

                    if (AllowsStat(equipmentData, EquipmentRollStat.Wisdom))
                    {
                        ownedEquipment.RolledWisdom = RollMainStat(equipmentData, EquipmentRollStat.Wisdom, quality, multiplier);
                    }

                    if (AllowsStat(equipmentData, EquipmentRollStat.Defense))
                    {
                        ownedEquipment.RolledDefense = RollMainStat(equipmentData, EquipmentRollStat.Defense, quality, multiplier);
                    }

                    if (AllowsStat(equipmentData, EquipmentRollStat.MagicDefense))
                    {
                        ownedEquipment.RolledMagicDefense = RollMainStat(equipmentData, EquipmentRollStat.MagicDefense, quality, multiplier);
                    }

                    ownedEquipment.RolledHp = RollInt(equipmentData.baseHp, multiplier);
                    ownedEquipment.RolledCritRate = RollFloat(equipmentData.bonusCritRate, multiplier);
                    ownedEquipment.RolledAttackSpeed = RollFloat(equipmentData.bonusAttackSpeed, multiplier);
                    break;
            }
        }

        private static void RollSubStats(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, EquipmentRarity quality, System.Random rng)
        {
            int subStatCount = ResolveSubStatCount(quality, rng);
            if (subStatCount <= 0)
            {
                return;
            }

            EquipmentRollStat[] candidates = BuildSubStatCandidates(equipmentData);
            if (candidates.Length <= 0)
            {
                return;
            }

            bool[] used = new bool[candidates.Length];
            int appliedCount = 0;
            while (appliedCount < subStatCount && appliedCount < candidates.Length)
            {
                int index = RollUnusedIndex(used, rng);
                if (index < 0)
                {
                    break;
                }

                used[index] = true;
                ApplySubStat(ownedEquipment, candidates[index], quality, rng);
                appliedCount += 1;
            }
        }

        private static EquipmentRollStat[] BuildSubStatCandidates(EquipmentDataSO equipmentData)
        {
            EquipmentAffinity affinity = ResolveAffinity(equipmentData);
            switch (equipmentData.slotType)
            {
                case EquipmentSlotType.Weapon:
                    return affinity == EquipmentAffinity.Magic
                        ? new[] { EquipmentRollStat.Wisdom, EquipmentRollStat.CritRate, EquipmentRollStat.AttackSpeed }
                        : new[] { EquipmentRollStat.Attack, EquipmentRollStat.CritRate, EquipmentRollStat.AttackSpeed };
                case EquipmentSlotType.Armor:
                    return affinity == EquipmentAffinity.Magic
                        ? new[] { EquipmentRollStat.MagicDefense, EquipmentRollStat.Hp }
                        : new[] { EquipmentRollStat.Defense, EquipmentRollStat.Hp };
                case EquipmentSlotType.Accessory:
                    if (affinity == EquipmentAffinity.Magic)
                    {
                        return new[]
                        {
                            EquipmentRollStat.Wisdom,
                            EquipmentRollStat.MagicDefense,
                            EquipmentRollStat.Hp,
                            EquipmentRollStat.CritRate,
                            EquipmentRollStat.AttackSpeed
                        };
                    }

                    if (affinity == EquipmentAffinity.Physical)
                    {
                        return new[]
                        {
                            EquipmentRollStat.Attack,
                            EquipmentRollStat.Defense,
                            EquipmentRollStat.Hp,
                            EquipmentRollStat.CritRate,
                            EquipmentRollStat.AttackSpeed
                        };
                    }

                    return new[]
                    {
                        EquipmentRollStat.Attack,
                        EquipmentRollStat.Wisdom,
                        EquipmentRollStat.Defense,
                        EquipmentRollStat.MagicDefense,
                        EquipmentRollStat.Hp,
                        EquipmentRollStat.CritRate,
                        EquipmentRollStat.AttackSpeed
                    };
                default:
                    return new EquipmentRollStat[0];
            }
        }

        private static bool AllowsStat(EquipmentDataSO equipmentData, EquipmentRollStat stat)
        {
            EquipmentAffinity affinity = ResolveAffinity(equipmentData);
            switch (equipmentData.slotType)
            {
                case EquipmentSlotType.Weapon:
                    return affinity == EquipmentAffinity.Magic
                        ? stat == EquipmentRollStat.Wisdom || stat == EquipmentRollStat.CritRate || stat == EquipmentRollStat.AttackSpeed
                        : stat == EquipmentRollStat.Attack || stat == EquipmentRollStat.CritRate || stat == EquipmentRollStat.AttackSpeed;
                case EquipmentSlotType.Armor:
                    return affinity == EquipmentAffinity.Magic
                        ? stat == EquipmentRollStat.MagicDefense || stat == EquipmentRollStat.Hp
                        : stat == EquipmentRollStat.Defense || stat == EquipmentRollStat.Hp;
                case EquipmentSlotType.Accessory:
                    if (affinity == EquipmentAffinity.Magic)
                    {
                        return stat == EquipmentRollStat.Wisdom ||
                            stat == EquipmentRollStat.MagicDefense ||
                            stat == EquipmentRollStat.Hp ||
                            stat == EquipmentRollStat.CritRate ||
                            stat == EquipmentRollStat.AttackSpeed;
                    }

                    if (affinity == EquipmentAffinity.Physical)
                    {
                        return stat == EquipmentRollStat.Attack ||
                            stat == EquipmentRollStat.Defense ||
                            stat == EquipmentRollStat.Hp ||
                            stat == EquipmentRollStat.CritRate ||
                            stat == EquipmentRollStat.AttackSpeed;
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static int RollMainStat(EquipmentDataSO equipmentData, EquipmentRollStat stat, EquipmentRarity quality, float multiplier)
        {
            int baseValue = ResolveMasterMainStat(equipmentData, stat);
            if (baseValue <= 0)
            {
                return 0;
            }

            int effectiveBaseValue = Mathf.Max(baseValue, ResolveMainStatFloor(equipmentData, stat, quality));
            return RollInt(effectiveBaseValue, multiplier);
        }

        private static int EnsureRolledMainStatFloor(EquipmentDataSO equipmentData, EquipmentRollStat stat, EquipmentRarity quality, int currentValue)
        {
            if (ResolveMasterMainStat(equipmentData, stat) <= 0 || !AllowsStat(equipmentData, stat))
            {
                return currentValue;
            }

            int floor = ResolveMainStatFloor(equipmentData, stat, quality);
            return floor > 0 ? Mathf.Max(currentValue, floor) : currentValue;
        }

        private static int ResolveMasterMainStat(EquipmentDataSO equipmentData, EquipmentRollStat stat)
        {
            if (equipmentData == null)
            {
                return 0;
            }

            switch (stat)
            {
                case EquipmentRollStat.Attack:
                    return Mathf.Max(0, equipmentData.baseAttack);
                case EquipmentRollStat.Wisdom:
                    return Mathf.Max(0, equipmentData.baseWisdom);
                case EquipmentRollStat.Defense:
                    return Mathf.Max(0, equipmentData.baseDefense);
                case EquipmentRollStat.MagicDefense:
                    return Mathf.Max(0, equipmentData.baseMagicDefense);
                default:
                    return 0;
            }
        }

        private static int ResolveMainStatFloor(EquipmentDataSO equipmentData, EquipmentRollStat stat, EquipmentRarity quality)
        {
            if (equipmentData == null || equipmentData.slotType != EquipmentSlotType.Accessory)
            {
                return 0;
            }

            switch (stat)
            {
                case EquipmentRollStat.Attack:
                case EquipmentRollStat.Wisdom:
                    return EquipmentBalanceTable.ResolveRange(quality, EquipmentBalanceStatFamily.AccessoryAttackLike).x;
                case EquipmentRollStat.Defense:
                case EquipmentRollStat.MagicDefense:
                    return EquipmentBalanceTable.ResolveRange(quality, EquipmentBalanceStatFamily.AccessoryDefenseLike).x;
                default:
                    return 0;
            }
        }

        private static EquipmentAffinity ResolveAffinity(EquipmentDataSO equipmentData)
        {
            if (equipmentData == null)
            {
                return EquipmentAffinity.Physical;
            }

            int physicalScore = Mathf.Max(0, equipmentData.baseAttack) + Mathf.Max(0, equipmentData.baseDefense);
            int magicScore = Mathf.Max(0, equipmentData.baseWisdom) + Mathf.Max(0, equipmentData.baseMagicDefense);
            if (physicalScore > 0 && magicScore > 0 && equipmentData.slotType == EquipmentSlotType.Accessory)
            {
                return EquipmentAffinity.Mixed;
            }

            return magicScore > physicalScore ? EquipmentAffinity.Magic : EquipmentAffinity.Physical;
        }

        private static EquipmentRarity ResolveQuality(OwnedEquipmentData ownedEquipment)
        {
            int rank = ownedEquipment != null && ownedEquipment.QualityRank > 0
                ? Mathf.Clamp(ownedEquipment.QualityRank, 1, 5)
                : 1;
            return (EquipmentRarity)(rank - 1);
        }

        private static float RollMainStatMultiplier(EquipmentRarity quality, System.Random rng)
        {
            Vector2 range = quality switch
            {
                EquipmentRarity.Uncommon => new Vector2(0.90f, 1.18f),
                EquipmentRarity.Rare => new Vector2(0.95f, 1.28f),
                EquipmentRarity.Epic => new Vector2(1.00f, 1.42f),
                EquipmentRarity.Legendary => new Vector2(1.08f, 1.60f),
                _ => new Vector2(0.85f, 1.10f)
            };

            return RollFloatRange(range.x, range.y, rng);
        }

        private static int ResolveSubStatCount(EquipmentRarity quality, System.Random rng)
        {
            switch (quality)
            {
                case EquipmentRarity.Uncommon:
                    return rng.NextDouble() < 0.65d ? 1 : 0;
                case EquipmentRarity.Rare:
                    return rng.NextDouble() < 0.30d ? 2 : 1;
                case EquipmentRarity.Epic:
                    return rng.NextDouble() < 0.45d ? 3 : 2;
                case EquipmentRarity.Legendary:
                    return rng.NextDouble() < 0.35d ? 4 : 3;
                default:
                    return rng.NextDouble() < 0.25d ? 1 : 0;
            }
        }

        private static void ApplySubStat(OwnedEquipmentData ownedEquipment, EquipmentRollStat stat, EquipmentRarity quality, System.Random rng)
        {
            switch (stat)
            {
                case EquipmentRollStat.Attack:
                    ownedEquipment.RolledAttack += RollPercentPointSubStat(quality, rng, 2, 7);
                    break;
                case EquipmentRollStat.Wisdom:
                    ownedEquipment.RolledWisdom += RollPercentPointSubStat(quality, rng, 2, 7);
                    break;
                case EquipmentRollStat.Defense:
                    ownedEquipment.RolledDefense += RollPercentPointSubStat(quality, rng, 2, 7);
                    break;
                case EquipmentRollStat.MagicDefense:
                    ownedEquipment.RolledMagicDefense += RollPercentPointSubStat(quality, rng, 2, 7);
                    break;
                case EquipmentRollStat.Hp:
                    ownedEquipment.RolledHp += RollPercentPointSubStat(quality, rng, 5, 16);
                    break;
                case EquipmentRollStat.CritRate:
                    ownedEquipment.RolledCritRate += RollFloatRange(0.003f, 0.008f + ((int)quality * 0.004f), rng);
                    break;
                case EquipmentRollStat.AttackSpeed:
                    ownedEquipment.RolledAttackSpeed += RollFloatRange(0.003f, 0.008f + ((int)quality * 0.004f), rng);
                    break;
            }
        }

        private static int RollPercentPointSubStat(EquipmentRarity quality, System.Random rng, int commonMin, int legendaryMax)
        {
            int min = commonMin + (int)quality;
            int max = Mathf.Max(min, commonMin + 2 + ((int)quality * 2));
            max = Mathf.Min(max, legendaryMax);
            return rng.Next(min, max + 1);
        }

        private static int RollUnusedIndex(bool[] used, System.Random rng)
        {
            int remaining = 0;
            for (int i = 0; i < used.Length; i += 1)
            {
                if (!used[i])
                {
                    remaining += 1;
                }
            }

            if (remaining <= 0)
            {
                return -1;
            }

            int selected = rng.Next(remaining);
            for (int i = 0; i < used.Length; i += 1)
            {
                if (used[i])
                {
                    continue;
                }

                if (selected == 0)
                {
                    return i;
                }

                selected -= 1;
            }

            return -1;
        }

        private static void ResetRolledStats(OwnedEquipmentData ownedEquipment)
        {
            ownedEquipment.RolledAttack = 0;
            ownedEquipment.RolledWisdom = 0;
            ownedEquipment.RolledDefense = 0;
            ownedEquipment.RolledMagicDefense = 0;
            ownedEquipment.RolledHp = 0;
            ownedEquipment.RolledCritRate = 0f;
            ownedEquipment.RolledAttackSpeed = 0f;
            ownedEquipment.HasRolledStats = false;
        }

        private static int RollInt(int baseValue, float multiplier)
        {
            if (baseValue <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));
        }

        private static float RollFloat(float baseValue, float multiplier)
        {
            return baseValue <= 0f ? 0f : Mathf.Max(0.001f, baseValue * multiplier);
        }

        private static float RollFloatRange(float min, float max, System.Random rng)
        {
            if (max <= min)
            {
                return min;
            }

            return min + ((float)rng.NextDouble() * (max - min));
        }
    }
}
