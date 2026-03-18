using System.Collections.Generic;
using System.Linq;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed class PlayerProfile
    {
        public int Level { get; set; }
        public int Exp { get; set; }
        public int Gold { get; set; }
        public int HighestFloor { get; set; }
        public int AttackUpgradeLevel { get; set; }
        public int DefenseUpgradeLevel { get; set; }
        public int HpUpgradeLevel { get; set; }
        public string LastDailyRewardDate { get; set; }
        public string LastActiveAt { get; set; }
        public int PendingIdleRewardGold { get; set; }
        public string EquippedWeaponId { get; set; }
        public string EquippedArmorId { get; set; }
        public string EquippedAccessoryId { get; set; }
        public List<OwnedMaterialData> OwnedMaterials { get; }
        public List<OwnedEquipmentData> OwnedEquipments { get; }
        public List<MissionProgressData> MissionProgressList { get; }

        public PlayerProfile(PlayerSaveData saveData)
        {
            Level = saveData.PlayerLevel;
            Exp = saveData.PlayerExp;
            Gold = saveData.Gold;
            HighestFloor = saveData.HighestFloor;
            AttackUpgradeLevel = saveData.AttackUpgradeLevel;
            DefenseUpgradeLevel = saveData.DefenseUpgradeLevel;
            HpUpgradeLevel = saveData.HpUpgradeLevel;
            LastDailyRewardDate = saveData.LastDailyRewardDate ?? string.Empty;
            LastActiveAt = saveData.LastActiveAt ?? string.Empty;
            PendingIdleRewardGold = saveData.PendingIdleRewardGold;
            EquippedWeaponId = string.IsNullOrEmpty(saveData.EquippedWeaponId) ? "equip_bronze_blade" : saveData.EquippedWeaponId;
            EquippedArmorId = string.IsNullOrEmpty(saveData.EquippedArmorId) ? "equip_guard_cloth" : saveData.EquippedArmorId;
            EquippedAccessoryId = string.IsNullOrEmpty(saveData.EquippedAccessoryId) ? "equip_ashen_ring" : saveData.EquippedAccessoryId;
            OwnedMaterials = saveData.OwnedMaterials ?? new List<OwnedMaterialData>();
            OwnedEquipments = saveData.OwnedEquipments ?? new List<OwnedEquipmentData>();
            MissionProgressList = saveData.MissionProgressList ?? new List<MissionProgressData>();
            SyncEquippedFlags();
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }

        public void AddExp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Exp += amount;
            ProcessLevelUp();
        }

        public int GetRequiredExpForNextLevel()
        {
            return 10 + (Level - 1) * 5;
        }

        public int GetAttackBonus()
        {
            return AttackUpgradeLevel * 3;
        }

        public int GetDefenseBonus()
        {
            return DefenseUpgradeLevel * 2;
        }

        public int GetHpBonus()
        {
            return HpUpgradeLevel * 10;
        }

        public bool CanClaimDailyReward(string currentDate)
        {
            return LastDailyRewardDate != currentDate;
        }

        public void MarkDailyRewardClaimed(string currentDate)
        {
            LastDailyRewardDate = currentDate;
        }

        public OwnedEquipmentData GetEquippedWeapon()
        {
            return OwnedEquipments.FirstOrDefault(x => x.EquipmentId == EquippedWeaponId);
        }

        public OwnedEquipmentData GetEquippedArmor()
        {
            return OwnedEquipments.FirstOrDefault(x => x.EquipmentId == EquippedArmorId);
        }

        public OwnedEquipmentData GetEquippedAccessory()
        {
            return OwnedEquipments.FirstOrDefault(x => x.EquipmentId == EquippedAccessoryId);
        }

        public void EquipWeapon(string equipmentId)
        {
            EquippedWeaponId = equipmentId;
            SyncEquippedFlags();
        }

        public void EquipArmor(string equipmentId)
        {
            EquippedArmorId = equipmentId;
            SyncEquippedFlags();
        }

        public void EquipAccessory(string equipmentId)
        {
            EquippedAccessoryId = equipmentId;
            SyncEquippedFlags();
        }

        public MissionProgressData GetMissionProgress(string missionId)
        {
            return MissionProgressList.FirstOrDefault(x => x.MissionId == missionId);
        }

        public void AddPendingIdleReward(int gold)
        {
            if (gold <= 0)
            {
                return;
            }

            PendingIdleRewardGold += gold;
        }

        public int ClaimPendingIdleReward()
        {
            var reward = PendingIdleRewardGold;
            if (reward > 0)
            {
                AddGold(reward);
                PendingIdleRewardGold = 0;
            }

            return reward;
        }

        private void ProcessLevelUp()
        {
            var requiredExp = GetRequiredExpForNextLevel();
            while (Exp >= requiredExp)
            {
                Exp -= requiredExp;
                Level += 1;
                requiredExp = GetRequiredExpForNextLevel();
            }
        }

        public PlayerSaveData ToSaveData(int currentFloor)
        {
            return new PlayerSaveData
            {
                PlayerLevel = Level,
                PlayerExp = Exp,
                Gold = Gold,
                HighestFloor = HighestFloor,
                CurrentFloor = currentFloor,
                AttackUpgradeLevel = AttackUpgradeLevel,
                DefenseUpgradeLevel = DefenseUpgradeLevel,
                HpUpgradeLevel = HpUpgradeLevel,
                LastDailyRewardDate = LastDailyRewardDate,
                LastActiveAt = LastActiveAt,
                PendingIdleRewardGold = PendingIdleRewardGold,
                MissionProgressList = new List<MissionProgressData>(MissionProgressList),
                EquippedWeaponId = EquippedWeaponId,
                EquippedArmorId = EquippedArmorId,
                EquippedAccessoryId = EquippedAccessoryId,
                OwnedMaterials = new List<OwnedMaterialData>(OwnedMaterials),
                OwnedEquipments = new List<OwnedEquipmentData>(OwnedEquipments),
                SkillLevels = new List<SkillLevelData>()
            };
        }

        private void SyncEquippedFlags()
        {
            foreach (var equipment in OwnedEquipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                equipment.IsEquipped =
                    equipment.EquipmentId == EquippedWeaponId ||
                    equipment.EquipmentId == EquippedArmorId ||
                    equipment.EquipmentId == EquippedAccessoryId;
            }
        }
    }
}
