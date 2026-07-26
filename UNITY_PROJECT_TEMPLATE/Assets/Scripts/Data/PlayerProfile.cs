using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed class PlayerProfile
    {
        public int Level { get; set; }
        public int Exp { get; set; }
        public int RebirthPoints { get; set; }
        public int TotalRebirthPoints { get; set; }
        public int RebirthCount { get; set; }
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
        public List<SkillLevelData> SkillLevels { get; }
        public List<RebirthSkillLevelData> RebirthSkillLevels { get; }
        public List<MissionProgressData> MissionProgressList { get; }

        public PlayerProfile(PlayerSaveData saveData)
        {
            Level = Math.Max(1, saveData.PlayerLevel);
            Exp = Math.Max(0, saveData.PlayerExp);
            RebirthPoints = Math.Max(0, saveData.RebirthPoints);
            TotalRebirthPoints = Math.Max(0, saveData.TotalRebirthPoints);
            RebirthCount = Math.Max(0, saveData.RebirthCount);
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
            SkillLevels = saveData.SkillLevels ?? new List<SkillLevelData>();
            RebirthSkillLevels = saveData.RebirthSkillLevels ?? new List<RebirthSkillLevelData>();
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

        public int GetLevelAttackBonus()
        {
            return Math.Max(0, Level - 1);
        }

        public int GetLevelDefenseBonus()
        {
            return Math.Max(0, (Level - 1) / 2);
        }

        public int GetLevelHpBonus()
        {
            return Math.Max(0, Level - 1) * 5;
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

        public float GetAttackMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.AttackMultiplier);
        }

        public float GetHpMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.HpMultiplier);
        }

        public float GetDefenseMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.DefenseMultiplier);
        }

        public float GetCritRateBonus()
        {
            return GetRebirthSkillValue(RebirthSkillEffectType.CritRateBonus);
        }

        public float GetAttackSpeedMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.AttackSpeedMultiplier);
        }

        public float GetStrikePowerMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.StrikePowerMultiplier);
        }

        public float GetDrainHealMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.DrainHealMultiplier);
        }

        public float GetExpRewardMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.ExpRewardMultiplier);
        }

        public float GetGoldRewardMultiplier()
        {
            return 1f + GetRebirthSkillValue(RebirthSkillEffectType.GoldRewardMultiplier);
        }

        public int GetPendingRebirthPointReward()
        {
            return RebirthService.CalculateRebirthPointReward(this);
        }

        public int ApplyRebirth()
        {
            var gainedPoints = RebirthService.CalculateRebirthPointReward(this);
            if (gainedPoints <= 0)
            {
                return 0;
            }

            RebirthPoints += gainedPoints;
            TotalRebirthPoints += gainedPoints;
            RebirthCount += 1;
            Level = 1;
            Exp = 0;
            return gainedPoints;
        }

        public bool TrySpendRebirthPoints(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (RebirthPoints < amount)
            {
                return false;
            }

            RebirthPoints -= amount;
            return true;
        }

        public int GetRebirthSkillLevel(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return 0;
            }

            var skillLevel = RebirthSkillLevels.FirstOrDefault(x => x != null && x.SkillId == skillId);
            return skillLevel != null ? Math.Max(0, skillLevel.Level) : 0;
        }

        public void SetRebirthSkillLevel(string skillId, int level)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return;
            }

            var skillLevel = RebirthSkillLevels.FirstOrDefault(x => x != null && x.SkillId == skillId);
            if (skillLevel == null)
            {
                skillLevel = new RebirthSkillLevelData
                {
                    SkillId = skillId,
                    Level = 0
                };
                RebirthSkillLevels.Add(skillLevel);
            }

            var definition = RebirthSkillCatalog.GetDefinition(skillId);
            var maxLevel = definition != null ? definition.MaxLevel : level;
            skillLevel.Level = Math.Max(0, Math.Min(level, maxLevel));
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
                SkillLevels = new List<SkillLevelData>(SkillLevels),
                RebirthPoints = RebirthPoints,
                TotalRebirthPoints = TotalRebirthPoints,
                RebirthCount = RebirthCount,
                RebirthSkillLevels = new List<RebirthSkillLevelData>(RebirthSkillLevels)
            };
        }

        private float GetRebirthSkillValue(RebirthSkillEffectType effectType)
        {
            var value = 0f;
            foreach (var definition in RebirthSkillCatalog.GetDefinitionsForEffect(effectType))
            {
                value += definition.GetTotalValue(GetRebirthSkillLevel(definition.SkillId));
            }

            return value;
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
