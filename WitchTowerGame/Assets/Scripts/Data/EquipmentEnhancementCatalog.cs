using System;
using System.Collections.Generic;
using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public readonly struct EquipmentResolvedBonus
    {
        public readonly int Attack;
        public readonly int Wisdom;
        public readonly int Defense;
        public readonly int MagicDefense;
        public readonly int Hp;
        public readonly float CritRate;
        public readonly float AttackSpeed;

        public EquipmentResolvedBonus(int attack, int wisdom, int defense, int magicDefense, int hp, float critRate, float attackSpeed)
        {
            Attack = attack;
            Wisdom = wisdom;
            Defense = defense;
            MagicDefense = magicDefense;
            Hp = hp;
            CritRate = critRate;
            AttackSpeed = attackSpeed;
        }

        public static EquipmentResolvedBonus operator +(EquipmentResolvedBonus left, EquipmentResolvedBonus right)
        {
            return new EquipmentResolvedBonus(
                left.Attack + right.Attack,
                left.Wisdom + right.Wisdom,
                left.Defense + right.Defense,
                left.MagicDefense + right.MagicDefense,
                left.Hp + right.Hp,
                left.CritRate + right.CritRate,
                left.AttackSpeed + right.AttackSpeed);
        }
    }

    public readonly struct EquipmentRolledBaseBonus
    {
        public readonly int Attack;
        public readonly int Wisdom;
        public readonly int Defense;
        public readonly int MagicDefense;
        public readonly int Hp;
        public readonly float CritRate;
        public readonly float AttackSpeed;

        public EquipmentRolledBaseBonus(int attack, int wisdom, int defense, int magicDefense, int hp, float critRate, float attackSpeed)
        {
            Attack = attack;
            Wisdom = wisdom;
            Defense = defense;
            MagicDefense = magicDefense;
            Hp = hp;
            CritRate = critRate;
            AttackSpeed = attackSpeed;
        }
    }

    [Serializable]
    public sealed class EnhancementRelicDefinition
    {
        public string RelicId;
        public string RelicName;
        public float SuccessRate;
        public float BonusPercent;
        public bool DestroysOnFailure;
        public string Description;
    }

    public enum EquipmentEnhancementResultType
    {
        None = 0,
        Success = 1,
        Failed = 2,
        Destroyed = 3,
        InvalidEquipment = 4,
        InvalidRelic = 5,
        NoRelic = 6,
        NoAttempts = 7,
        Locked = 8
    }

    public sealed class EquipmentEnhancementResult
    {
        public EquipmentEnhancementResultType ResultType;
        public string Message;
        public string EquipmentInstanceId;
        public string EquipmentId;
        public string RelicId;
        public bool ConsumedAttempt;
        public bool ConsumedRelic;
    }

    public static class EquipmentEnhancementCatalog
    {
        private static readonly EnhancementRelicDefinition[] Relics =
        {
            new EnhancementRelicDefinition
            {
                RelicId = "relic_safe_ember",
                RelicName = "通常遺物",
                SuccessRate = 1.0f,
                BonusPercent = 0.05f,
                DestroysOnFailure = false,
                Description = "成功率100%。上昇量は小さい。"
            },
            new EnhancementRelicDefinition
            {
                RelicId = "relic_risky_ember",
                RelicName = "上級遺物",
                SuccessRate = 0.3f,
                BonusPercent = 0.10f,
                DestroysOnFailure = false,
                Description = "成功率30%。上昇量は高め。"
            },
            new EnhancementRelicDefinition
            {
                RelicId = "relic_volatile_ember",
                RelicName = "危険遺物",
                SuccessRate = 0.10f,
                BonusPercent = 0.25f,
                DestroysOnFailure = true,
                Description = "成功率10%。失敗時に装備が消滅する。"
            }
        };

        public static IReadOnlyList<EnhancementRelicDefinition> AllRelics => Relics;

        public static EnhancementRelicDefinition GetRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
            {
                return null;
            }

            for (int i = 0; i < Relics.Length; i += 1)
            {
                if (Relics[i].RelicId == relicId)
                {
                    return Relics[i];
                }
            }

            return null;
        }

        public static int ResolveInitialEnhanceAttempts(EquipmentDataSO equipmentData, string equipmentId)
        {
            EquipmentRarity rarity = equipmentData != null ? equipmentData.rarity : EquipmentRarity.Common;
            int defaultByRarity;
            switch (rarity)
            {
                case EquipmentRarity.Common:
                case EquipmentRarity.Uncommon:
                case EquipmentRarity.Rare:
                    defaultByRarity = 5;
                    break;
                case EquipmentRarity.Epic:
                    defaultByRarity = 6;
                    break;
                case EquipmentRarity.Legendary:
                    defaultByRarity = 7;
                    break;
                default:
                    defaultByRarity = 5;
                    break;
            }

            if (equipmentData != null && equipmentData.maxEnhancementAttempts > 0)
            {
                return Mathf.Max(defaultByRarity, equipmentData.maxEnhancementAttempts);
            }

            return defaultByRarity;
        }

        public static void EnsureRolledStats(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, System.Random random)
        {
            if (equipmentData == null || ownedEquipment == null || ownedEquipment.HasRolledStats)
            {
                return;
            }

            float variance = Mathf.Clamp01(equipmentData.statVarianceRate);
            ownedEquipment.RolledAttack = RollIntStat(equipmentData.baseAttack, variance, random);
            ownedEquipment.RolledWisdom = RollIntStat(equipmentData.baseWisdom, variance, random);
            ownedEquipment.RolledDefense = RollIntStat(equipmentData.baseDefense, variance, random);
            ownedEquipment.RolledMagicDefense = RollIntStat(equipmentData.baseMagicDefense, variance, random);
            ownedEquipment.RolledHp = RollIntStat(equipmentData.baseHp, variance, random);
            ownedEquipment.RolledCritRate = RollFloatStat(equipmentData.bonusCritRate, variance, random);
            ownedEquipment.RolledAttackSpeed = RollFloatStat(equipmentData.bonusAttackSpeed, variance, random);
            ownedEquipment.HasRolledStats = true;
        }

        public static EquipmentRolledBaseBonus ResolveBaseBonus(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null)
            {
                return default;
            }

            if (ownedEquipment == null || !ownedEquipment.HasRolledStats)
            {
                return new EquipmentRolledBaseBonus(
                    equipmentData.baseAttack,
                    equipmentData.baseWisdom,
                    equipmentData.baseDefense,
                    equipmentData.baseMagicDefense,
                    equipmentData.baseHp,
                    equipmentData.bonusCritRate,
                    equipmentData.bonusAttackSpeed);
            }

            return new EquipmentRolledBaseBonus(
                ownedEquipment.RolledAttack,
                ownedEquipment.RolledWisdom,
                ownedEquipment.RolledDefense,
                ownedEquipment.RolledMagicDefense,
                ownedEquipment.RolledHp,
                ownedEquipment.RolledCritRate,
                ownedEquipment.RolledAttackSpeed);
        }

        public static EquipmentResolvedBonus ResolveEquipmentBonus(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return default;
            }

            EquipmentRolledBaseBonus rolledBase = ResolveBaseBonus(equipmentData, ownedEquipment);
            if (equipmentData.slotType == EquipmentSlotType.Accessory)
            {
                return new EquipmentResolvedBonus(
                    rolledBase.Attack + ownedEquipment.EnhancementAttackFlat,
                    rolledBase.Wisdom + ownedEquipment.EnhancementWisdomFlat,
                    rolledBase.Defense + ownedEquipment.EnhancementDefenseFlat,
                    rolledBase.MagicDefense + ownedEquipment.EnhancementMagicDefenseFlat,
                    rolledBase.Hp + ownedEquipment.EnhancementHpFlat,
                    rolledBase.CritRate,
                    rolledBase.AttackSpeed + ownedEquipment.EnhancementAttackSpeedFlat);
            }

            float multiplier = 1f + Mathf.Max(0f, ownedEquipment.EnhancementBonusRate);
            int attack = rolledBase.Attack == 0 ? 0 : Mathf.RoundToInt(rolledBase.Attack * multiplier);
            int wisdom = rolledBase.Wisdom == 0 ? 0 : Mathf.RoundToInt(rolledBase.Wisdom * multiplier);
            int defense = rolledBase.Defense == 0 ? 0 : Mathf.RoundToInt(rolledBase.Defense * multiplier);
            int magicDefense = rolledBase.MagicDefense == 0 ? 0 : Mathf.RoundToInt(rolledBase.MagicDefense * multiplier);
            int hp = rolledBase.Hp == 0 ? 0 : Mathf.RoundToInt(rolledBase.Hp * multiplier);
            float critRate = Mathf.Approximately(rolledBase.CritRate, 0f) ? 0f : rolledBase.CritRate * multiplier;
            float attackSpeed = Mathf.Approximately(rolledBase.AttackSpeed, 0f) ? 0f : rolledBase.AttackSpeed * multiplier;
            return new EquipmentResolvedBonus(attack, wisdom, defense, magicDefense, hp, critRate, attackSpeed);
        }

        public static void ApplyEnhancementSuccess(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, EnhancementRelicDefinition relic)
        {
            if (equipmentData == null || ownedEquipment == null || relic == null)
            {
                return;
            }

            if (equipmentData.slotType == EquipmentSlotType.Accessory)
            {
                ApplyAccessoryEnhancementSuccess(equipmentData, ownedEquipment, relic);
                return;
            }

            ownedEquipment.EnhancementBonusRate += relic.BonusPercent;
        }

        public static string BuildEnhancementSummary(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return "強化なし";
            }

            if (equipmentData.slotType != EquipmentSlotType.Accessory)
            {
                return $"+{ownedEquipment.EnhancementBonusRate * 100f:0.#}%";
            }

            var parts = new List<string>();
            if (ownedEquipment.EnhancementAttackFlat != 0) parts.Add($"攻+{ownedEquipment.EnhancementAttackFlat}");
            if (ownedEquipment.EnhancementWisdomFlat != 0) parts.Add($"賢+{ownedEquipment.EnhancementWisdomFlat}");
            if (ownedEquipment.EnhancementDefenseFlat != 0) parts.Add($"防+{ownedEquipment.EnhancementDefenseFlat}");
            if (ownedEquipment.EnhancementMagicDefenseFlat != 0) parts.Add($"魔防+{ownedEquipment.EnhancementMagicDefenseFlat}");
            if (ownedEquipment.EnhancementHpFlat != 0) parts.Add($"HP+{ownedEquipment.EnhancementHpFlat}");
            if (Mathf.Abs(ownedEquipment.EnhancementAttackSpeedFlat) > 0.0001f) parts.Add($"速+{ownedEquipment.EnhancementAttackSpeedFlat:0.##}");
            return parts.Count > 0 ? string.Join(" / ", parts) : "強化なし";
        }

        public static string BuildRelicEffectSummary(EquipmentDataSO equipmentData, EnhancementRelicDefinition relic)
        {
            if (equipmentData == null || relic == null)
            {
                return string.Empty;
            }

            if (equipmentData.slotType != EquipmentSlotType.Accessory)
            {
                return $"成功時 ×{1f + relic.BonusPercent:0.##}";
            }

            var parts = new List<string>();
            if (equipmentData.baseAttack != 0) parts.Add($"攻+{ResolveAccessoryAttackLikeIncrement(relic)}");
            if (equipmentData.baseWisdom != 0) parts.Add($"賢+{ResolveAccessoryAttackLikeIncrement(relic)}");
            if (equipmentData.baseDefense != 0) parts.Add($"防+{ResolveAccessoryDefenseLikeIncrement(relic)}");
            if (equipmentData.baseMagicDefense != 0) parts.Add($"魔防+{ResolveAccessoryDefenseLikeIncrement(relic)}");
            if (equipmentData.baseHp != 0) parts.Add($"HP+{ResolveAccessoryHpIncrement(relic)}");
            if (!Mathf.Approximately(equipmentData.bonusAttackSpeed, 0f)) parts.Add($"速+{ResolveAccessoryAttackSpeedIncrement(relic):0}");
            return parts.Count > 0 ? "成功時 " + string.Join(" / ", parts) : "成功時 変化なし";
        }

        private static int RollIntStat(int baseValue, float variance, System.Random random)
        {
            if (baseValue == 0)
            {
                return 0;
            }

            float multiplier = RollMultiplier(variance, random);
            return Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));
        }

        private static float RollFloatStat(float baseValue, float variance, System.Random random)
        {
            if (Mathf.Approximately(baseValue, 0f))
            {
                return 0f;
            }

            return baseValue * RollMultiplier(variance, random);
        }

        private static float RollMultiplier(float variance, System.Random random)
        {
            if (random == null || variance <= 0f)
            {
                return 1f;
            }

            float range = variance * 2f;
            return 1f - variance + ((float)random.NextDouble() * range);
        }

        private static void ApplyAccessoryEnhancementSuccess(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, EnhancementRelicDefinition relic)
        {
            int attackLike = ResolveAccessoryAttackLikeIncrement(relic);
            int defenseLike = ResolveAccessoryDefenseLikeIncrement(relic);
            int hp = ResolveAccessoryHpIncrement(relic);
            float attackSpeed = ResolveAccessoryAttackSpeedIncrement(relic);

            if (equipmentData.baseAttack != 0) ownedEquipment.EnhancementAttackFlat += attackLike;
            if (equipmentData.baseWisdom != 0) ownedEquipment.EnhancementWisdomFlat += attackLike;
            if (equipmentData.baseDefense != 0) ownedEquipment.EnhancementDefenseFlat += defenseLike;
            if (equipmentData.baseMagicDefense != 0) ownedEquipment.EnhancementMagicDefenseFlat += defenseLike;
            if (equipmentData.baseHp != 0) ownedEquipment.EnhancementHpFlat += hp;
            if (!Mathf.Approximately(equipmentData.bonusAttackSpeed, 0f)) ownedEquipment.EnhancementAttackSpeedFlat += attackSpeed;
        }

        private static int ResolveAccessoryAttackLikeIncrement(EnhancementRelicDefinition relic)
        {
            switch (relic != null ? relic.RelicId : string.Empty)
            {
                case "relic_safe_ember":
                    return 1;
                case "relic_risky_ember":
                    return 2;
                case "relic_volatile_ember":
                    return 5;
                default:
                    return 0;
            }
        }

        private static int ResolveAccessoryDefenseLikeIncrement(EnhancementRelicDefinition relic)
        {
            switch (relic != null ? relic.RelicId : string.Empty)
            {
                case "relic_safe_ember":
                    return 1;
                case "relic_risky_ember":
                    return 2;
                case "relic_volatile_ember":
                    return 4;
                default:
                    return 0;
            }
        }

        private static int ResolveAccessoryHpIncrement(EnhancementRelicDefinition relic)
        {
            switch (relic != null ? relic.RelicId : string.Empty)
            {
                case "relic_safe_ember":
                    return 5;
                case "relic_risky_ember":
                    return 10;
                case "relic_volatile_ember":
                    return 25;
                default:
                    return 0;
            }
        }

        private static float ResolveAccessoryAttackSpeedIncrement(EnhancementRelicDefinition relic)
        {
            switch (relic != null ? relic.RelicId : string.Empty)
            {
                case "relic_safe_ember":
                    return 1f;
                case "relic_risky_ember":
                    return 2f;
                case "relic_volatile_ember":
                    return 4f;
                default:
                    return 0f;
            }
        }
    }
}
