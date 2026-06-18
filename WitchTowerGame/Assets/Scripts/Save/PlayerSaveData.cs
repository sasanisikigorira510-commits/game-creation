using System;
using System.Collections.Generic;

namespace WitchTower.Save
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public int PlayerLevel;
        public int PlayerExp;
        public int Gold;
        public int FreeGachaStones;
        public int PaidGachaStones;
        public int HighestFloor;
        public int CurrentFloor;
        public int AttackUpgradeLevel;
        public int DefenseUpgradeLevel;
        public int HpUpgradeLevel;
        public bool HasAutoRepeatFloorUpgrade;
        public int AutoRepeatFloorUpgradeEnabledState;
        public string LastDailyRewardDate;
        public string DailyQuestProgressDate;
        public int DailyBattleWinCount;
        public List<string> DailyClaimedQuestIds;
        public string LastActiveAt;
        public int PendingIdleRewardGold;
        public List<MissionProgressData> MissionProgressList;
        public string EquippedWeaponId;
        public string EquippedArmorId;
        public string EquippedAccessoryId;
        public List<OwnedMaterialData> OwnedMaterials;
        public List<OwnedEquipmentData> OwnedEquipments;
        public List<OwnedEnhancementRelicData> OwnedEnhancementRelics;
        public int EquipmentStorageLimit;
        public int MonsterStorageLimit;
        public List<OwnedMonsterData> OwnedMonsters;
        public List<MonsterDexEntryData> MonsterDexEntries;
        public List<string> PartyMonsterInstanceIds;
        public List<SkillLevelData> SkillLevels;
        public bool HasCompletedTutorial;
        public string TutorialStepId;
        public List<string> SeenStoryEventIds;
        public List<string> SeenTutorialHintIds;

        public static PlayerSaveData CreateDefault()
        {
            return new PlayerSaveData
            {
                PlayerLevel = 1,
                PlayerExp = 0,
                Gold = 100,
                FreeGachaStones = 3000,
                PaidGachaStones = 0,
                HighestFloor = 0,
                CurrentFloor = 1,
                AttackUpgradeLevel = 0,
                DefenseUpgradeLevel = 0,
                HpUpgradeLevel = 0,
                HasAutoRepeatFloorUpgrade = false,
                AutoRepeatFloorUpgradeEnabledState = 0,
                LastDailyRewardDate = string.Empty,
                DailyQuestProgressDate = string.Empty,
                DailyBattleWinCount = 0,
                DailyClaimedQuestIds = new List<string>(),
                LastActiveAt = string.Empty,
                PendingIdleRewardGold = 0,
                MissionProgressList = new List<MissionProgressData>
                {
                    new MissionProgressData
                    {
                        MissionId = "mission_clear_1",
                        Progress = 0,
                        IsClaimed = false
                    },
                    new MissionProgressData
                    {
                        MissionId = "mission_reach_floor_3",
                        Progress = 0,
                        IsClaimed = false
                    }
                },
                EquippedWeaponId = "equip_bronze_blade",
                EquippedArmorId = "equip_guard_cloth",
                EquippedAccessoryId = "equip_ashen_ring",
                OwnedMaterials = new List<OwnedMaterialData>(),
                OwnedEquipments = new List<OwnedEquipmentData>
                {
                    new OwnedEquipmentData
                    {
                        InstanceId = "equipinst_bronze_blade_001",
                        EquipmentId = "equip_bronze_blade",
                        UpgradeLevel = 0,
                        EnhancementBonusRate = 0f,
                        QualityRank = 1,
                        RemainingEnhanceAttempts = 5,
                        MaxEnhanceAttempts = 5,
                        IsEquipped = false,
                        IsLocked = false,
                        EquippedMonsterInstanceId = string.Empty
                    },
                    new OwnedEquipmentData
                    {
                        InstanceId = "equipinst_guard_cloth_001",
                        EquipmentId = "equip_guard_cloth",
                        UpgradeLevel = 0,
                        EnhancementBonusRate = 0f,
                        QualityRank = 1,
                        RemainingEnhanceAttempts = 5,
                        MaxEnhanceAttempts = 5,
                        IsEquipped = false,
                        IsLocked = false,
                        EquippedMonsterInstanceId = string.Empty
                    },
                    new OwnedEquipmentData
                    {
                        InstanceId = "equipinst_ashen_ring_001",
                        EquipmentId = "equip_ashen_ring",
                        UpgradeLevel = 0,
                        EnhancementBonusRate = 0f,
                        QualityRank = 2,
                        RemainingEnhanceAttempts = 5,
                        MaxEnhanceAttempts = 5,
                        IsEquipped = false,
                        IsLocked = false,
                        EquippedMonsterInstanceId = string.Empty
                    }
                },
                OwnedEnhancementRelics = new List<OwnedEnhancementRelicData>
                {
                    new OwnedEnhancementRelicData
                    {
                        RelicId = "relic_safe_ember",
                        Amount = 24
                    },
                    new OwnedEnhancementRelicData
                    {
                        RelicId = "relic_risky_ember",
                        Amount = 12
                    },
                    new OwnedEnhancementRelicData
                    {
                        RelicId = "relic_volatile_ember",
                        Amount = 6
                    }
                },
                EquipmentStorageLimit = 100,
                MonsterStorageLimit = 100,
                OwnedMonsters = new List<OwnedMonsterData>(),
                MonsterDexEntries = new List<MonsterDexEntryData>(),
                PartyMonsterInstanceIds = new List<string>(),
                SkillLevels = new List<SkillLevelData>(),
                HasCompletedTutorial = false,
                TutorialStepId = "T00",
                SeenStoryEventIds = new List<string>(),
                SeenTutorialHintIds = new List<string>()
            };
        }
    }

    [Serializable]
    public sealed class OwnedMaterialData
    {
        public string MaterialId;
        public int Amount;
    }

    [Serializable]
    public sealed class OwnedEquipmentData
    {
        public string InstanceId;
        public string EquipmentId;
        public int UpgradeLevel;
        public float EnhancementBonusRate;
        public int QualityRank;
        public int RemainingEnhanceAttempts;
        public int MaxEnhanceAttempts;
        public bool IsEquipped;
        public bool IsLocked;
        public string EquippedMonsterInstanceId;
        public bool HasRolledStats;
        public int RolledAttack;
        public int RolledWisdom;
        public int RolledDefense;
        public int RolledMagicDefense;
        public int RolledHp;
        public float RolledCritRate;
        public float RolledAttackSpeed;
        public int EnhancementAttackFlat;
        public int EnhancementWisdomFlat;
        public int EnhancementDefenseFlat;
        public int EnhancementMagicDefenseFlat;
        public int EnhancementHpFlat;
        public float EnhancementCritRateFlat;
        public float EnhancementAttackSpeedFlat;
    }

    [Serializable]
    public sealed class OwnedEnhancementRelicData
    {
        public string RelicId;
        public int Amount;
    }

    [Serializable]
    public sealed class OwnedMonsterData
    {
        public string InstanceId;
        public string MonsterId;
        public int Level;
        public int Exp;
        public int PlusValue;
        public int PlusHp;
        public int PlusAttack;
        public int PlusWisdom;
        public int PlusDefense;
        public int PlusMagicDefense;
        public int FusionBonusHp;
        public int FusionBonusAttack;
        public int FusionBonusWisdom;
        public int FusionBonusDefense;
        public int FusionBonusMagicDefense;
        public float FusionBonusAttackSpeed;
        public bool HasIndividualValues;
        public int IndividualHp;
        public int IndividualAttack;
        public int IndividualWisdom;
        public int IndividualDefense;
        public int IndividualMagicDefense;
        public int IndividualAttackSpeed;
        public bool IsFavorite;
        public bool IsLocked;
        public int AcquiredOrder;
        public string EquippedWeaponInstanceId;
        public string EquippedArmorInstanceId;
        public string EquippedAccessoryInstanceId;

        public int TotalPlusValue =>
            System.Math.Max(
                System.Math.Max(System.Math.Max(0, PlusValue), System.Math.Max(0, PlusHp)),
                System.Math.Max(
                    System.Math.Max(System.Math.Max(0, PlusAttack), System.Math.Max(0, PlusWisdom)),
                    System.Math.Max(System.Math.Max(0, PlusDefense), System.Math.Max(0, PlusMagicDefense))));
    }

    [Serializable]
    public sealed class MonsterDexEntryData
    {
        public string MonsterId;
        public bool IsUnlocked;
        public int OwnedCount;
    }

    [Serializable]
    public sealed class SkillLevelData
    {
        public string SkillId;
        public int Level;
    }

    [Serializable]
    public sealed class MissionProgressData
    {
        public string MissionId;
        public int Progress;
        public bool IsClaimed;
    }
}
