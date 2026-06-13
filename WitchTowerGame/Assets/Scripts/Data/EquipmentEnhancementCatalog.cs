using System;
using System.Collections.Generic;
using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public readonly struct EquipmentResolvedBonus
    {
        public readonly float AttackPercent;
        public readonly float WisdomPercent;
        public readonly float DefensePercent;
        public readonly float MagicDefensePercent;
        public readonly float HpPercent;
        public readonly float CritRate;
        public readonly float AttackSpeed;

        public EquipmentResolvedBonus(float attackPercent, float wisdomPercent, float defensePercent, float magicDefensePercent, float hpPercent, float critRate, float attackSpeed)
        {
            AttackPercent = Mathf.Max(0f, attackPercent);
            WisdomPercent = Mathf.Max(0f, wisdomPercent);
            DefensePercent = Mathf.Max(0f, defensePercent);
            MagicDefensePercent = Mathf.Max(0f, magicDefensePercent);
            HpPercent = Mathf.Max(0f, hpPercent);
            CritRate = Mathf.Max(0f, critRate);
            AttackSpeed = Mathf.Max(0f, attackSpeed);
        }

        public static EquipmentResolvedBonus operator +(EquipmentResolvedBonus left, EquipmentResolvedBonus right)
        {
            return new EquipmentResolvedBonus(
                left.AttackPercent + right.AttackPercent,
                left.WisdomPercent + right.WisdomPercent,
                left.DefensePercent + right.DefensePercent,
                left.MagicDefensePercent + right.MagicDefensePercent,
                left.HpPercent + right.HpPercent,
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
            Attack = Mathf.Max(0, attack);
            Wisdom = Mathf.Max(0, wisdom);
            Defense = Mathf.Max(0, defense);
            MagicDefense = Mathf.Max(0, magicDefense);
            Hp = Mathf.Max(0, hp);
            CritRate = Mathf.Max(0f, critRate);
            AttackSpeed = Mathf.Max(0f, attackSpeed);
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
                Description = "確実に成功する。装備の基礎効果を強化し、会心・速度付き装備には固定ボーナスを加える。"
            },
            new EnhancementRelicDefinition
            {
                RelicId = "relic_risky_ember",
                RelicName = "上級遺物",
                SuccessRate = 0.3f,
                BonusPercent = 0.10f,
                DestroysOnFailure = false,
                Description = "装備の基礎効果を強化し、会心・速度付き装備には固定ボーナスを加える。失敗しても装備は残る。"
            },
            new EnhancementRelicDefinition
            {
                RelicId = "relic_volatile_ember",
                RelicName = "危険遺物",
                SuccessRate = 0.10f,
                BonusPercent = 0.25f,
                DestroysOnFailure = true,
                Description = "装備の基礎効果を大きく強化し、会心・速度付き装備には固定ボーナスを加える。失敗時に装備が消滅する。"
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
            return ResolveMaxEnhanceAttempts(equipmentData);
        }

        public static int ResolveMaxEnhanceAttempts(EquipmentDataSO equipmentData)
        {
            return 4 + ResolveQualityRank(equipmentData);
        }

        public static int ResolveMaxEnhanceAttempts(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            return 4 + ResolveQualityRank(equipmentData, ownedEquipment);
        }

        public static int ResolveQualityRank(EquipmentDataSO equipmentData)
        {
            EquipmentRarity quality = equipmentData != null ? equipmentData.rarity : EquipmentRarity.Common;
            return Mathf.Clamp((int)quality + 1, 1, 5);
        }

        public static int ResolveQualityRank(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            return ownedEquipment != null && ownedEquipment.QualityRank > 0
                ? Mathf.Clamp(ownedEquipment.QualityRank, 1, 5)
                : ResolveQualityRank(equipmentData);
        }

        public static float ResolveQualityMultiplier(EquipmentDataSO equipmentData)
        {
            return 1f + ((ResolveQualityRank(equipmentData) - 1) * 0.2f);
        }

        public static float ResolveQualityMultiplier(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            return 1f + ((ResolveQualityRank(equipmentData, ownedEquipment) - 1) * 0.2f);
        }

        public static string ResolveQualityName(EquipmentDataSO equipmentData)
        {
            EquipmentRarity quality = equipmentData != null ? equipmentData.rarity : EquipmentRarity.Common;
            return ResolveQualityName(quality);
        }

        public static string ResolveQualityName(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            int rank = ResolveQualityRank(equipmentData, ownedEquipment);
            EquipmentRarity quality = (EquipmentRarity)Mathf.Clamp(rank - 1, 0, 4);
            return ResolveQualityName(quality);
        }

        public static string ResolveQualityName(EquipmentRarity quality)
        {
            switch (quality)
            {
                case EquipmentRarity.Uncommon:
                    return "アンコモン";
                case EquipmentRarity.Rare:
                    return "レア";
                case EquipmentRarity.Epic:
                    return "エピック";
                case EquipmentRarity.Legendary:
                    return "レジェンダリー";
                case EquipmentRarity.Common:
                default:
                    return "コモン";
            }
        }

        public static string BuildQualityLabel(EquipmentDataSO equipmentData)
        {
            return $"品質:{ResolveQualityName(equipmentData)}";
        }

        public static string BuildQualityLabel(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            return $"品質:{ResolveQualityName(equipmentData, ownedEquipment)}";
        }

        public static string BuildEnhanceAttemptsLabel(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            int maxAttempts = ownedEquipment != null && ownedEquipment.MaxEnhanceAttempts > 0
                ? ownedEquipment.MaxEnhanceAttempts
                : ResolveMaxEnhanceAttempts(equipmentData, ownedEquipment);
            int remainingAttempts = ownedEquipment != null ? Mathf.Max(0, ownedEquipment.RemainingEnhanceAttempts) : 0;
            return $"残り {remainingAttempts}/{maxAttempts}回";
        }

        public static void EnsureQualityEnhanceAttempts(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (ownedEquipment == null)
            {
                return;
            }

            if (ownedEquipment.QualityRank <= 0)
            {
                ownedEquipment.QualityRank = ResolveQualityRank(equipmentData);
            }

            int qualityMax = ResolveMaxEnhanceAttempts(equipmentData, ownedEquipment);
            if (ownedEquipment.MaxEnhanceAttempts <= 0)
            {
                int legacyMax = ResolveLegacyMaxEnhanceAttempts(equipmentData);
                int consumedAttempts = Mathf.Max(0, legacyMax - Mathf.Max(0, ownedEquipment.RemainingEnhanceAttempts));
                ownedEquipment.MaxEnhanceAttempts = qualityMax;
                ownedEquipment.RemainingEnhanceAttempts = Mathf.Max(0, qualityMax - consumedAttempts);
                return;
            }

            int usedAttempts = Mathf.Max(0, ownedEquipment.MaxEnhanceAttempts - Mathf.Max(0, ownedEquipment.RemainingEnhanceAttempts));
            ownedEquipment.MaxEnhanceAttempts = qualityMax;
            ownedEquipment.RemainingEnhanceAttempts = Mathf.Max(0, qualityMax - usedAttempts);
        }

        public static void EnsureRolledStats(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, System.Random random)
        {
            if (equipmentData == null || ownedEquipment == null || ownedEquipment.HasRolledStats)
            {
                return;
            }

            SyncRolledStatsFromMaster(equipmentData, ownedEquipment);
        }

        public static void SyncRolledStatsFromMaster(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return;
            }

            ownedEquipment.RolledAttack = Mathf.Max(0, equipmentData.baseAttack);
            ownedEquipment.RolledWisdom = Mathf.Max(0, equipmentData.baseWisdom);
            ownedEquipment.RolledDefense = Mathf.Max(0, equipmentData.baseDefense);
            ownedEquipment.RolledMagicDefense = Mathf.Max(0, equipmentData.baseMagicDefense);
            ownedEquipment.RolledHp = Mathf.Max(0, equipmentData.baseHp);
            ownedEquipment.RolledCritRate = Mathf.Max(0f, equipmentData.bonusCritRate);
            ownedEquipment.RolledAttackSpeed = Mathf.Max(0f, equipmentData.bonusAttackSpeed);
            ownedEquipment.HasRolledStats = true;
        }

        public static EquipmentRolledBaseBonus ResolveBaseBonus(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null)
            {
                return default;
            }

            return new EquipmentRolledBaseBonus(
                equipmentData.baseAttack,
                equipmentData.baseWisdom,
                equipmentData.baseDefense,
                equipmentData.baseMagicDefense,
                equipmentData.baseHp,
                equipmentData.bonusCritRate,
                equipmentData.bonusAttackSpeed);
        }

        public static EquipmentResolvedBonus ResolveEquipmentBonus(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return default;
            }

            EquipmentRolledBaseBonus rolledBase = ResolveBaseBonus(equipmentData, ownedEquipment);
            float enhancementMultiplier = 1f + Mathf.Max(0f, ownedEquipment.EnhancementBonusRate);
            float qualityMultiplier = ResolveQualityMultiplier(equipmentData, ownedEquipment);
            float attackPercent = (((rolledBase.Attack * enhancementMultiplier) + ownedEquipment.EnhancementAttackFlat) * qualityMultiplier) / 100f;
            float wisdomPercent = (((rolledBase.Wisdom * enhancementMultiplier) + ownedEquipment.EnhancementWisdomFlat) * qualityMultiplier) / 100f;
            float defensePercent = (((rolledBase.Defense * enhancementMultiplier) + ownedEquipment.EnhancementDefenseFlat) * qualityMultiplier) / 100f;
            float magicDefensePercent = (((rolledBase.MagicDefense * enhancementMultiplier) + ownedEquipment.EnhancementMagicDefenseFlat) * qualityMultiplier) / 100f;
            float hpPercent = (((rolledBase.Hp * enhancementMultiplier) + ownedEquipment.EnhancementHpFlat) * qualityMultiplier) / 100f;
            float critRate = (rolledBase.CritRate + ownedEquipment.EnhancementCritRateFlat) * qualityMultiplier;
            float attackSpeed = (rolledBase.AttackSpeed + ownedEquipment.EnhancementAttackSpeedFlat) * qualityMultiplier;
            return new EquipmentResolvedBonus(attackPercent, wisdomPercent, defensePercent, magicDefensePercent, hpPercent, critRate, attackSpeed);
        }

        public static void ApplyEnhancementSuccess(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment, EnhancementRelicDefinition relic)
        {
            if (equipmentData == null || ownedEquipment == null || relic == null)
            {
                return;
            }

            ownedEquipment.EnhancementBonusRate += relic.BonusPercent;
            if (equipmentData.bonusCritRate > 0f)
            {
                ownedEquipment.EnhancementCritRateFlat += ResolveCritRateEnhancement(relic);
            }

            if (equipmentData.bonusAttackSpeed > 0f)
            {
                ownedEquipment.EnhancementAttackSpeedFlat += ResolveAttackSpeedEnhancement(relic);
            }
        }

        public static string BuildEnhancementSummary(EquipmentDataSO equipmentData, OwnedEquipmentData ownedEquipment)
        {
            if (equipmentData == null || ownedEquipment == null)
            {
                return "補正なし";
            }

            EquipmentResolvedBonus bonus = ResolveEquipmentBonus(equipmentData, ownedEquipment);
            var parts = new List<string>();
            if (bonus.AttackPercent > 0.0001f) parts.Add($"攻+{bonus.AttackPercent * 100f:0.#}%");
            if (bonus.WisdomPercent > 0.0001f) parts.Add($"賢+{bonus.WisdomPercent * 100f:0.#}%");
            if (bonus.DefensePercent > 0.0001f) parts.Add($"防+{bonus.DefensePercent * 100f:0.#}%");
            if (bonus.MagicDefensePercent > 0.0001f) parts.Add($"魔防+{bonus.MagicDefensePercent * 100f:0.#}%");
            if (bonus.HpPercent > 0.0001f) parts.Add($"HP+{bonus.HpPercent * 100f:0.#}%");
            if (bonus.CritRate > 0.0001f) parts.Add($"会心+{bonus.CritRate * 100f:0.#}%");
            if (bonus.AttackSpeed > 0.0001f) parts.Add($"速+{bonus.AttackSpeed:0.###}");
            return parts.Count > 0 ? string.Join(" / ", parts) : "補正なし";
        }

        public static string BuildRelicEffectSummary(EquipmentDataSO equipmentData, EnhancementRelicDefinition relic)
        {
            if (equipmentData == null || relic == null)
            {
                return string.Empty;
            }

            var parts = new List<string>
            {
                $"基礎効果 +{relic.BonusPercent * 100f:0.#}%"
            };
            if (equipmentData.bonusCritRate > 0f)
            {
                parts.Add($"会心+{ResolveCritRateEnhancement(relic) * 100f:0.#}%");
            }

            if (equipmentData.bonusAttackSpeed > 0f)
            {
                parts.Add($"速+{ResolveAttackSpeedEnhancement(relic):0.###}");
            }

            return "成功時 " + string.Join(" / ", parts);
        }

        private static float ResolveCritRateEnhancement(EnhancementRelicDefinition relic)
        {
            return relic?.RelicId switch
            {
                "relic_safe_ember" => 0.002f,
                "relic_risky_ember" => 0.004f,
                "relic_volatile_ember" => 0.01f,
                _ => 0f
            };
        }

        private static float ResolveAttackSpeedEnhancement(EnhancementRelicDefinition relic)
        {
            return relic?.RelicId switch
            {
                "relic_safe_ember" => 0.002f,
                "relic_risky_ember" => 0.004f,
                "relic_volatile_ember" => 0.01f,
                _ => 0f
            };
        }

        private static int ResolveLegacyMaxEnhanceAttempts(EquipmentDataSO equipmentData)
        {
            EquipmentRarity rarity = equipmentData != null ? equipmentData.rarity : EquipmentRarity.Common;
            int defaultByRarity = rarity switch
            {
                EquipmentRarity.Epic => 6,
                EquipmentRarity.Legendary => 7,
                _ => 5
            };
            return equipmentData != null && equipmentData.maxEnhancementAttempts > 0
                ? Mathf.Max(defaultByRarity, equipmentData.maxEnhancementAttempts)
                : defaultByRarity;
        }
    }
}
