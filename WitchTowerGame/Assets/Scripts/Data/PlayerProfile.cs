using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed partial class PlayerProfile
    {
        public const int PartySlotCount = 5;
        private const string LegacyPrimaryDailyQuestId = "daily_battle_win_1";

        public int Level { get; set; }
        public int Exp { get; set; }
        public int Gold { get; set; }
        public int FreeGachaStones { get; set; }
        public int PaidGachaStones { get; set; }
        public int HighestFloor { get; set; }
        public int AttackUpgradeLevel { get; set; }
        public int DefenseUpgradeLevel { get; set; }
        public int HpUpgradeLevel { get; set; }
        public string LastDailyRewardDate { get; set; }
        public string DailyQuestProgressDate { get; set; }
        public int DailyBattleWinCount { get; set; }
        public List<string> DailyClaimedQuestIds { get; }
        public string LastActiveAt { get; set; }
        public int PendingIdleRewardGold { get; set; }
        public List<OwnedMaterialData> OwnedMaterials { get; }
        public List<OwnedEquipmentData> OwnedEquipments { get; }
        public List<OwnedEnhancementRelicData> OwnedEnhancementRelics { get; }
        public int MonsterStorageLimit { get; set; }
        public List<OwnedMonsterData> OwnedMonsters { get; }
        public List<MonsterDexEntryData> MonsterDexEntries { get; }
        public List<string> PartyMonsterInstanceIds { get; }
        public List<MissionProgressData> MissionProgressList { get; }

        public PlayerProfile(PlayerSaveData saveData)
        {
            Level = saveData.PlayerLevel;
            Exp = saveData.PlayerExp;
            Gold = saveData.Gold;
            FreeGachaStones = Math.Max(0, saveData.FreeGachaStones);
            PaidGachaStones = Math.Max(0, saveData.PaidGachaStones);
            HighestFloor = saveData.HighestFloor;
            AttackUpgradeLevel = saveData.AttackUpgradeLevel;
            DefenseUpgradeLevel = saveData.DefenseUpgradeLevel;
            HpUpgradeLevel = saveData.HpUpgradeLevel;
            LastDailyRewardDate = saveData.LastDailyRewardDate ?? string.Empty;
            DailyQuestProgressDate = saveData.DailyQuestProgressDate ?? string.Empty;
            DailyBattleWinCount = Math.Max(0, saveData.DailyBattleWinCount);
            DailyClaimedQuestIds = saveData.DailyClaimedQuestIds ?? new List<string>();
            LastActiveAt = saveData.LastActiveAt ?? string.Empty;
            PendingIdleRewardGold = saveData.PendingIdleRewardGold;
            OwnedMaterials = saveData.OwnedMaterials ?? new List<OwnedMaterialData>();
            OwnedEquipments = saveData.OwnedEquipments ?? new List<OwnedEquipmentData>();
            OwnedEnhancementRelics = saveData.OwnedEnhancementRelics ?? new List<OwnedEnhancementRelicData>();
            MonsterStorageLimit = saveData.MonsterStorageLimit > 0 ? saveData.MonsterStorageLimit : 100;
            OwnedMonsters = saveData.OwnedMonsters ?? new List<OwnedMonsterData>();
            MonsterDexEntries = saveData.MonsterDexEntries ?? new List<MonsterDexEntryData>();
            PartyMonsterInstanceIds = saveData.PartyMonsterInstanceIds ?? new List<string>();
            MissionProgressList = saveData.MissionProgressList ?? new List<MissionProgressData>();
            NormalizeMonsterPlusValues();
            NormalizeMonsterIndividualValues();
            InitializeEquipmentState(saveData);
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }

        public bool TrySpendGold(int amount)
        {
            int cost = Math.Max(0, amount);
            if (Gold < cost)
            {
                return false;
            }

            Gold -= cost;
            return true;
        }

        public void AddFreeGachaStones(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            FreeGachaStones += amount;
        }

        public void AddPaidGachaStones(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            PaidGachaStones += amount;
        }

        public bool CanSpendFreeGachaStones(int amount)
        {
            return FreeGachaStones >= Math.Max(0, amount);
        }

        public bool CanSpendPaidGachaStones(int amount)
        {
            return PaidGachaStones >= Math.Max(0, amount);
        }

        public bool TrySpendFreeGachaStones(int amount)
        {
            int cost = Math.Max(0, amount);
            if (FreeGachaStones < cost)
            {
                return false;
            }

            FreeGachaStones -= cost;
            return true;
        }

        public bool TrySpendPaidGachaStones(int amount)
        {
            int cost = Math.Max(0, amount);
            if (PaidGachaStones < cost)
            {
                return false;
            }

            PaidGachaStones -= cost;
            return true;
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
            int levelOffset = Math.Max(0, Level - 1);
            return 120 + levelOffset * 60 + levelOffset * levelOffset * 16;
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

        public bool HasClaimedDailyReward(string currentDate)
        {
            return LastDailyRewardDate == currentDate;
        }

        public void MarkDailyRewardClaimed(string currentDate)
        {
            LastDailyRewardDate = currentDate;
        }

        public int GetDailyBattleWinCount(string currentDate)
        {
            ResetDailyQuestProgressIfNeeded(currentDate);
            return DailyBattleWinCount;
        }

        public void RecordDailyBattleWin(string currentDate)
        {
            ResetDailyQuestProgressIfNeeded(currentDate);
            DailyBattleWinCount += 1;
        }

        public bool HasClaimedDailyQuest(string currentDate, string questId)
        {
            ResetDailyQuestProgressIfNeeded(currentDate);
            if (string.IsNullOrEmpty(questId))
            {
                return false;
            }

            if (questId == LegacyPrimaryDailyQuestId && HasClaimedDailyReward(currentDate))
            {
                return true;
            }

            return DailyClaimedQuestIds.Contains(questId);
        }

        public void MarkDailyQuestClaimed(string currentDate, string questId)
        {
            ResetDailyQuestProgressIfNeeded(currentDate);
            if (string.IsNullOrEmpty(questId) || DailyClaimedQuestIds.Contains(questId))
            {
                return;
            }

            DailyClaimedQuestIds.Add(questId);
            if (questId == LegacyPrimaryDailyQuestId)
            {
                MarkDailyRewardClaimed(currentDate);
            }
        }

        public bool HasMonsterStorageSpace()
        {
            return OwnedMonsters.Count < MonsterStorageLimit;
        }

        public OwnedMonsterData GetOwnedMonster(string instanceId)
        {
            return OwnedMonsters.FirstOrDefault(x => x != null && x.InstanceId == instanceId);
        }

        public bool ToggleMonsterLock(string monsterInstanceId)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            if (monster == null) return false;
            monster.IsLocked = !monster.IsLocked;
            return monster.IsLocked;
        }

        public int GetOwnedMonsterCount(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return 0;
            }

            return OwnedMonsters.Count(x => x != null && x.MonsterId == monsterId);
        }

        public OwnedMonsterData AddOwnedMonster(string monsterId, int level, int plusValue = 0, bool isFavorite = false)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return null;
            }

            int acquiredOrder = 1;
            foreach (OwnedMonsterData ownedMonster in OwnedMonsters)
            {
                if (ownedMonster == null)
                {
                    continue;
                }

                acquiredOrder = Math.Max(acquiredOrder, ownedMonster.AcquiredOrder + 1);
            }

            var newMonster = new OwnedMonsterData
            {
                InstanceId = monsterId + "_" + Guid.NewGuid().ToString("N"),
                MonsterId = monsterId,
                Level = Math.Max(1, level),
                Exp = 0,
                PlusValue = Math.Max(0, plusValue),
                PlusHp = Math.Max(0, plusValue),
                PlusAttack = Math.Max(0, plusValue),
                PlusWisdom = Math.Max(0, plusValue),
                PlusDefense = Math.Max(0, plusValue),
                PlusMagicDefense = Math.Max(0, plusValue),
                FusionBonusHp = 0,
                FusionBonusAttack = 0,
                FusionBonusWisdom = 0,
                FusionBonusDefense = 0,
                FusionBonusMagicDefense = 0,
                FusionBonusAttackSpeed = 0f,
                HasIndividualValues = false,
                IsFavorite = isFavorite,
                IsLocked = false,
                AcquiredOrder = acquiredOrder,
                EquippedWeaponInstanceId = string.Empty,
                EquippedArmorInstanceId = string.Empty,
                EquippedAccessoryInstanceId = string.Empty
            };
            MonsterIndividualValueService.Apply(newMonster, MonsterIndividualValueService.Roll());

            OwnedMonsters.Add(newMonster);
            MarkMonsterDexOwned(monsterId);
            SyncLegacyRepresentativeEquipmentIds();
            return newMonster;
        }

        public bool TryApplyMonsterPlus(string monsterInstanceId, MonsterPlusStatType statType, int amount = 1)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            if (monster == null || amount <= 0)
            {
                return false;
            }

            switch (statType)
            {
                case MonsterPlusStatType.Hp:
                    monster.PlusHp += amount;
                    break;
                case MonsterPlusStatType.Attack:
                    monster.PlusAttack += amount;
                    break;
                case MonsterPlusStatType.Wisdom:
                    monster.PlusWisdom += amount;
                    break;
                case MonsterPlusStatType.Defense:
                    monster.PlusDefense += amount;
                    break;
                case MonsterPlusStatType.MagicDefense:
                    monster.PlusMagicDefense += amount;
                    break;
                default:
                    return false;
            }

            monster.PlusValue = monster.TotalPlusValue;
            return true;
        }

        private void NormalizeMonsterPlusValues()
        {
            foreach (OwnedMonsterData monster in OwnedMonsters)
            {
                if (monster == null)
                {
                    continue;
                }

                monster.PlusHp = Math.Max(0, monster.PlusHp);
                monster.PlusAttack = Math.Max(0, monster.PlusAttack);
                monster.PlusWisdom = Math.Max(0, monster.PlusWisdom);
                monster.PlusDefense = Math.Max(0, monster.PlusDefense);
                monster.PlusMagicDefense = Math.Max(0, monster.PlusMagicDefense);
                monster.FusionBonusHp = Math.Max(0, monster.FusionBonusHp);
                monster.FusionBonusAttack = Math.Max(0, monster.FusionBonusAttack);
                monster.FusionBonusWisdom = Math.Max(0, monster.FusionBonusWisdom);
                monster.FusionBonusDefense = Math.Max(0, monster.FusionBonusDefense);
                monster.FusionBonusMagicDefense = Math.Max(0, monster.FusionBonusMagicDefense);
                monster.FusionBonusAttackSpeed = Math.Max(0f, monster.FusionBonusAttackSpeed);
                monster.PlusValue = Math.Max(0, monster.PlusValue);

                bool hasItemizedPlus =
                    monster.PlusHp > 0 ||
                    monster.PlusAttack > 0 ||
                    monster.PlusWisdom > 0 ||
                    monster.PlusDefense > 0 ||
                    monster.PlusMagicDefense > 0;

                if (!hasItemizedPlus && monster.PlusValue > 0)
                {
                    // Legacy migration: old single plus value affected multiple combat stats at once,
                    // so copy it across the new per-stat fields to preserve relative strength.
                    monster.PlusHp = monster.PlusValue;
                    monster.PlusAttack = monster.PlusValue;
                    monster.PlusWisdom = monster.PlusValue;
                    monster.PlusDefense = monster.PlusValue;
                    monster.PlusMagicDefense = monster.PlusValue;
                }
            }
        }

        private void NormalizeMonsterIndividualValues()
        {
            foreach (OwnedMonsterData monster in OwnedMonsters)
            {
                MonsterIndividualValueService.EnsureInitialized(monster);
            }
        }

        public void MarkMonsterDexOwned(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return;
            }

            MonsterDexEntryData dexEntry = MonsterDexEntries.FirstOrDefault(x => x != null && x.MonsterId == monsterId);
            int ownedCount = GetOwnedMonsterCount(monsterId);
            if (dexEntry == null)
            {
                MonsterDexEntries.Add(new MonsterDexEntryData
                {
                    MonsterId = monsterId,
                    IsUnlocked = true,
                    OwnedCount = Math.Max(1, ownedCount)
                });
                return;
            }

            dexEntry.IsUnlocked = true;
            dexEntry.OwnedCount = Math.Max(1, ownedCount);
        }

        public void SetPartyMonsterIds(IEnumerable<string> monsterInstanceIds)
        {
            PartyMonsterInstanceIds.Clear();
            if (monsterInstanceIds == null)
            {
                for (int i = 0; i < PartySlotCount; i += 1)
                {
                    PartyMonsterInstanceIds.Add(string.Empty);
                }

                SyncLegacyRepresentativeEquipmentIds();
                return;
            }

            foreach (string instanceId in monsterInstanceIds.Take(PartySlotCount))
            {
                PartyMonsterInstanceIds.Add(instanceId ?? string.Empty);
            }

            while (PartyMonsterInstanceIds.Count < PartySlotCount)
            {
                PartyMonsterInstanceIds.Add(string.Empty);
            }

            SyncLegacyRepresentativeEquipmentIds();
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
            int reward = PendingIdleRewardGold;
            if (reward > 0)
            {
                AddGold(reward);
                PendingIdleRewardGold = 0;
            }

            return reward;
        }

        public PlayerSaveData ToSaveData(int currentFloor)
        {
            SyncLegacyRepresentativeEquipmentIds();
            SyncEquippedFlags();

            return new PlayerSaveData
            {
                PlayerLevel = Level,
                PlayerExp = Exp,
                Gold = Gold,
                FreeGachaStones = FreeGachaStones,
                PaidGachaStones = PaidGachaStones,
                HighestFloor = HighestFloor,
                CurrentFloor = currentFloor,
                AttackUpgradeLevel = AttackUpgradeLevel,
                DefenseUpgradeLevel = DefenseUpgradeLevel,
                HpUpgradeLevel = HpUpgradeLevel,
                LastDailyRewardDate = LastDailyRewardDate,
                DailyQuestProgressDate = DailyQuestProgressDate,
                DailyBattleWinCount = DailyBattleWinCount,
                DailyClaimedQuestIds = new List<string>(DailyClaimedQuestIds),
                LastActiveAt = LastActiveAt,
                PendingIdleRewardGold = PendingIdleRewardGold,
                MissionProgressList = new List<MissionProgressData>(MissionProgressList),
                EquippedWeaponId = legacyEquippedWeaponId,
                EquippedArmorId = legacyEquippedArmorId,
                EquippedAccessoryId = legacyEquippedAccessoryId,
                OwnedMaterials = new List<OwnedMaterialData>(OwnedMaterials),
                OwnedEquipments = new List<OwnedEquipmentData>(OwnedEquipments),
                OwnedEnhancementRelics = new List<OwnedEnhancementRelicData>(OwnedEnhancementRelics),
                MonsterStorageLimit = MonsterStorageLimit,
                OwnedMonsters = new List<OwnedMonsterData>(OwnedMonsters),
                MonsterDexEntries = new List<MonsterDexEntryData>(MonsterDexEntries),
                PartyMonsterInstanceIds = new List<string>(PartyMonsterInstanceIds),
                SkillLevels = new List<SkillLevelData>()
            };
        }

        private void ProcessLevelUp()
        {
            int requiredExp = GetRequiredExpForNextLevel();
            while (Exp >= requiredExp)
            {
                Exp -= requiredExp;
                Level += 1;
                requiredExp = GetRequiredExpForNextLevel();
            }
        }

        private void ResetDailyQuestProgressIfNeeded(string currentDate)
        {
            if (string.IsNullOrEmpty(currentDate))
            {
                return;
            }

            if (DailyQuestProgressDate == currentDate)
            {
                return;
            }

            DailyQuestProgressDate = currentDate;
            DailyBattleWinCount = 0;
            DailyClaimedQuestIds.Clear();
        }

        partial void InitializeEquipmentState(PlayerSaveData saveData);
        partial void SyncLegacyRepresentativeEquipmentIds();
        partial void SyncEquippedFlags();
    }
}
