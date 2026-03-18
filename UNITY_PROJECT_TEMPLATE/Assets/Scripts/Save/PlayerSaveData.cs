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
        public int HighestFloor;
        public int CurrentFloor;
        public int AttackUpgradeLevel;
        public int DefenseUpgradeLevel;
        public int HpUpgradeLevel;
        public string LastDailyRewardDate;
        public string LastActiveAt;
        public int PendingIdleRewardGold;
        public List<MissionProgressData> MissionProgressList;
        public string EquippedWeaponId;
        public string EquippedArmorId;
        public string EquippedAccessoryId;
        public List<OwnedMaterialData> OwnedMaterials;
        public List<OwnedEquipmentData> OwnedEquipments;
        public List<SkillLevelData> SkillLevels;

        public static PlayerSaveData CreateDefault()
        {
            return new PlayerSaveData
            {
                PlayerLevel = 1,
                PlayerExp = 0,
                Gold = 100,
                HighestFloor = 1,
                CurrentFloor = 1,
                AttackUpgradeLevel = 0,
                DefenseUpgradeLevel = 0,
                HpUpgradeLevel = 0,
                LastDailyRewardDate = string.Empty,
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
                        EquipmentId = "equip_bronze_blade",
                        UpgradeLevel = 0,
                        IsEquipped = true
                    },
                    new OwnedEquipmentData
                    {
                        EquipmentId = "equip_guard_cloth",
                        UpgradeLevel = 0,
                        IsEquipped = true
                    },
                    new OwnedEquipmentData
                    {
                        EquipmentId = "equip_ashen_ring",
                        UpgradeLevel = 0,
                        IsEquipped = true
                    },
                    new OwnedEquipmentData
                    {
                        EquipmentId = "equip_iron_sword",
                        UpgradeLevel = 0,
                        IsEquipped = false
                    },
                    new OwnedEquipmentData
                    {
                        EquipmentId = "equip_bone_mail",
                        UpgradeLevel = 0,
                        IsEquipped = false
                    },
                    new OwnedEquipmentData
                    {
                        EquipmentId = "equip_quick_charm",
                        UpgradeLevel = 0,
                        IsEquipped = false
                    }
                },
                SkillLevels = new List<SkillLevelData>()
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
        public string EquipmentId;
        public int UpgradeLevel;
        public bool IsEquipped;
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
