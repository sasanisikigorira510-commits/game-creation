using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed partial class PlayerProfile
    {
        public const int PartySlotCount = 5;
        public const int DefaultMonsterStorageLimit = 100;
        public const int DefaultEquipmentStorageLimit = 100;
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
        public bool HasAutoRepeatFloorUpgrade { get; set; }
        public bool IsAutoRepeatFloorUpgradeEnabled { get; set; }
        public bool HasAutoSellEquipmentUpgrade { get; set; }
        public bool IsAutoSellEquipmentUpgradeEnabled { get; set; }
        public int AutoSellEquipmentQualityThreshold { get; set; }
        public bool HasAutoReleaseMonsterUpgrade { get; set; }
        public bool IsAutoReleaseMonsterUpgradeEnabled { get; set; }
        public int AutoReleaseMonsterIndividualValueThreshold { get; set; }
        public string LastDailyRewardDate { get; set; }
        public string DailyQuestProgressDate { get; set; }
        public int DailyBattleWinCount { get; set; }
        public List<string> DailyClaimedQuestIds { get; }
        public string LastActiveAt { get; set; }
        public List<OwnedMaterialData> OwnedMaterials { get; }
        public List<OwnedEquipmentData> OwnedEquipments { get; }
        public List<OwnedEnhancementRelicData> OwnedEnhancementRelics { get; }
        public int EquipmentStorageLimit { get; set; }
        public int MonsterStorageLimit { get; set; }
        public List<OwnedMonsterData> OwnedMonsters { get; }
        public List<MonsterDexEntryData> MonsterDexEntries { get; }
        public List<string> PartyMonsterInstanceIds { get; }
        public List<MissionProgressData> MissionProgressList { get; }
        public bool HasCompletedTutorial { get; set; }
        public string TutorialStepId { get; set; }
        public List<string> SeenStoryEventIds { get; }
        public List<string> SeenTutorialHintIds { get; }

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
            HasAutoRepeatFloorUpgrade = saveData.HasAutoRepeatFloorUpgrade;
            IsAutoRepeatFloorUpgradeEnabled = HasAutoRepeatFloorUpgrade && saveData.AutoRepeatFloorUpgradeEnabledState != 2;
            HasAutoSellEquipmentUpgrade = saveData.HasAutoSellEquipmentUpgrade;
            IsAutoSellEquipmentUpgradeEnabled = HasAutoSellEquipmentUpgrade && saveData.AutoSellEquipmentUpgradeEnabledState != 2;
            AutoSellEquipmentQualityThreshold = ResolveSavedEquipmentQualityThreshold(saveData.AutoSellEquipmentQualityThreshold);
            HasAutoReleaseMonsterUpgrade = saveData.HasAutoReleaseMonsterUpgrade;
            IsAutoReleaseMonsterUpgradeEnabled = HasAutoReleaseMonsterUpgrade && saveData.AutoReleaseMonsterUpgradeEnabledState != 2;
            AutoReleaseMonsterIndividualValueThreshold = ResolveSavedIndividualValueThreshold(saveData.AutoReleaseMonsterIndividualValueThreshold);
            LastDailyRewardDate = saveData.LastDailyRewardDate ?? string.Empty;
            DailyQuestProgressDate = saveData.DailyQuestProgressDate ?? string.Empty;
            DailyBattleWinCount = Math.Max(0, saveData.DailyBattleWinCount);
            DailyClaimedQuestIds = saveData.DailyClaimedQuestIds ?? new List<string>();
            LastActiveAt = saveData.LastActiveAt ?? string.Empty;
            OwnedMaterials = saveData.OwnedMaterials ?? new List<OwnedMaterialData>();
            OwnedEquipments = saveData.OwnedEquipments ?? new List<OwnedEquipmentData>();
            OwnedEnhancementRelics = saveData.OwnedEnhancementRelics ?? new List<OwnedEnhancementRelicData>();
            EquipmentStorageLimit = Math.Max(
                saveData.EquipmentStorageLimit > 0 ? saveData.EquipmentStorageLimit : DefaultEquipmentStorageLimit,
                OwnedEquipments.Count);
            MonsterStorageLimit = saveData.MonsterStorageLimit > 0 ? saveData.MonsterStorageLimit : DefaultMonsterStorageLimit;
            OwnedMonsters = saveData.OwnedMonsters ?? new List<OwnedMonsterData>();
            MonsterDexEntries = saveData.MonsterDexEntries ?? new List<MonsterDexEntryData>();
            PartyMonsterInstanceIds = saveData.PartyMonsterInstanceIds ?? new List<string>();
            MissionProgressList = saveData.MissionProgressList ?? new List<MissionProgressData>();
            bool hasSavedTutorialState = !string.IsNullOrEmpty(saveData.TutorialStepId);
            HasCompletedTutorial = hasSavedTutorialState ? saveData.HasCompletedTutorial : true;
            TutorialStepId = hasSavedTutorialState
                ? saveData.TutorialStepId
                : "Complete";
            SeenStoryEventIds = saveData.SeenStoryEventIds ?? new List<string>();
            SeenTutorialHintIds = saveData.SeenTutorialHintIds ?? new List<string>();
            if (!hasSavedTutorialState)
            {
                StoryTutorialService.BackfillClearedChapterStories(this);
            }
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

        public bool HasEquipmentStorageSpace()
        {
            return OwnedEquipments.Count < EquipmentStorageLimit;
        }

        public bool CanAutoSellEquipmentDrops()
        {
            return HasAutoSellEquipmentUpgrade && IsAutoSellEquipmentUpgradeEnabled;
        }

        public bool ShouldAutoSellEquipment(int qualityRank)
        {
            return CanAutoSellEquipmentDrops() &&
                ClampEquipmentQualityThreshold(qualityRank) < AutoSellEquipmentQualityThreshold;
        }

        public bool CanAutoReleaseNewMonsters()
        {
            return HasAutoReleaseMonsterUpgrade && IsAutoReleaseMonsterUpgradeEnabled;
        }

        public bool ShouldAutoReleaseMonster(OwnedMonsterData monster)
        {
            return CanAutoReleaseNewMonsters() &&
                MonsterIndividualValueService.GetAverage(monster) < AutoReleaseMonsterIndividualValueThreshold;
        }

        public void SetAutoSellEquipmentQualityThreshold(int qualityRank)
        {
            AutoSellEquipmentQualityThreshold = ClampEquipmentQualityThreshold(qualityRank);
        }

        public void SetAutoReleaseMonsterIndividualValueThreshold(int threshold)
        {
            AutoReleaseMonsterIndividualValueThreshold = ClampIndividualValueThreshold(threshold);
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

            int normalizedPlus = Math.Max(0, plusValue);
            var newMonster = new OwnedMonsterData
            {
                InstanceId = monsterId + "_" + Guid.NewGuid().ToString("N"),
                MonsterId = monsterId,
                Level = Math.Max(1, level),
                Exp = 0,
                PlusValue = normalizedPlus,
                PlusHp = normalizedPlus,
                PlusAttack = normalizedPlus,
                PlusWisdom = normalizedPlus,
                PlusDefense = normalizedPlus,
                PlusMagicDefense = normalizedPlus,
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

            if (!Enum.IsDefined(typeof(MonsterPlusStatType), statType))
            {
                return false;
            }

            monster.PlusValue = Math.Max(0, monster.PlusValue) + amount;
            SyncMonsterPlusFields(monster);
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

                int singlePlus = Math.Max(0, monster.PlusValue);
                singlePlus = Math.Max(singlePlus, Math.Max(0, monster.PlusHp));
                singlePlus = Math.Max(singlePlus, Math.Max(0, monster.PlusAttack));
                singlePlus = Math.Max(singlePlus, Math.Max(0, monster.PlusWisdom));
                singlePlus = Math.Max(singlePlus, Math.Max(0, monster.PlusDefense));
                singlePlus = Math.Max(singlePlus, Math.Max(0, monster.PlusMagicDefense));
                monster.PlusValue = singlePlus;
                SyncMonsterPlusFields(monster);
                monster.FusionBonusHp = Math.Max(0, monster.FusionBonusHp);
                monster.FusionBonusAttack = Math.Max(0, monster.FusionBonusAttack);
                monster.FusionBonusWisdom = Math.Max(0, monster.FusionBonusWisdom);
                monster.FusionBonusDefense = Math.Max(0, monster.FusionBonusDefense);
                monster.FusionBonusMagicDefense = Math.Max(0, monster.FusionBonusMagicDefense);
                monster.FusionBonusAttackSpeed = Math.Max(0f, monster.FusionBonusAttackSpeed);
            }
        }

        private static void SyncMonsterPlusFields(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return;
            }

            int plusValue = Math.Max(0, monster.PlusValue);
            monster.PlusValue = plusValue;
            monster.PlusHp = plusValue;
            monster.PlusAttack = plusValue;
            monster.PlusWisdom = plusValue;
            monster.PlusDefense = plusValue;
            monster.PlusMagicDefense = plusValue;
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

        public bool TryReleaseMonster(string monsterInstanceId, out string message)
        {
            return TryReleaseMonster(monsterInstanceId, false, out message);
        }

        public bool TryReleaseMonster(string monsterInstanceId, bool force, out string message)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            if (monster == null)
            {
                message = "対象モンスターが見つかりません。";
                return false;
            }

            if (!force && monster.IsFavorite)
            {
                message = "お気に入り登録中のモンスターは逃がせません。";
                return false;
            }

            if (!force && monster.IsLocked)
            {
                message = "ロック中のモンスターは逃がせません。";
                return false;
            }

            if (!force && PartyMonsterInstanceIds.Contains(monster.InstanceId))
            {
                message = "パーティ編成中のモンスターは逃がせません。";
                return false;
            }

            string displayName = ResolveMonsterDisplayName(monster.MonsterId);
            ClearReleasedMonsterEquipment(monster);
            OwnedMonsters.Remove(monster);
            ClearReleasedMonsterFromParty(monster.InstanceId);
            RefreshMonsterDexOwnedCount(monster.MonsterId);
            SyncLegacyRepresentativeEquipmentIds();
            SyncEquippedFlags();
            message = $"{displayName} を逃がしました。";
            return true;
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
                HasAutoRepeatFloorUpgrade = HasAutoRepeatFloorUpgrade,
                AutoRepeatFloorUpgradeEnabledState = !HasAutoRepeatFloorUpgrade
                    ? 0
                    : IsAutoRepeatFloorUpgradeEnabled
                        ? 1
                        : 2,
                HasAutoSellEquipmentUpgrade = HasAutoSellEquipmentUpgrade,
                AutoSellEquipmentUpgradeEnabledState = !HasAutoSellEquipmentUpgrade
                    ? 0
                    : IsAutoSellEquipmentUpgradeEnabled
                        ? 1
                        : 2,
                AutoSellEquipmentQualityThreshold = ClampEquipmentQualityThreshold(AutoSellEquipmentQualityThreshold),
                HasAutoReleaseMonsterUpgrade = HasAutoReleaseMonsterUpgrade,
                AutoReleaseMonsterUpgradeEnabledState = !HasAutoReleaseMonsterUpgrade
                    ? 0
                    : IsAutoReleaseMonsterUpgradeEnabled
                        ? 1
                        : 2,
                AutoReleaseMonsterIndividualValueThreshold = ClampIndividualValueThreshold(AutoReleaseMonsterIndividualValueThreshold),
                LastDailyRewardDate = LastDailyRewardDate,
                DailyQuestProgressDate = DailyQuestProgressDate,
                DailyBattleWinCount = DailyBattleWinCount,
                DailyClaimedQuestIds = new List<string>(DailyClaimedQuestIds),
                LastActiveAt = LastActiveAt,
                MissionProgressList = new List<MissionProgressData>(MissionProgressList),
                EquippedWeaponId = legacyEquippedWeaponId,
                EquippedArmorId = legacyEquippedArmorId,
                EquippedAccessoryId = legacyEquippedAccessoryId,
                OwnedMaterials = new List<OwnedMaterialData>(OwnedMaterials),
                OwnedEquipments = new List<OwnedEquipmentData>(OwnedEquipments),
                OwnedEnhancementRelics = new List<OwnedEnhancementRelicData>(OwnedEnhancementRelics),
                EquipmentStorageLimit = EquipmentStorageLimit,
                MonsterStorageLimit = MonsterStorageLimit,
                OwnedMonsters = new List<OwnedMonsterData>(OwnedMonsters),
                MonsterDexEntries = new List<MonsterDexEntryData>(MonsterDexEntries),
                PartyMonsterInstanceIds = new List<string>(PartyMonsterInstanceIds),
                SkillLevels = new List<SkillLevelData>(),
                HasCompletedTutorial = HasCompletedTutorial,
                TutorialStepId = TutorialStepId ?? string.Empty,
                SeenStoryEventIds = new List<string>(SeenStoryEventIds),
                SeenTutorialHintIds = new List<string>(SeenTutorialHintIds)
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

        private static int ClampEquipmentQualityThreshold(int qualityRank)
        {
            return Math.Min(5, Math.Max(1, qualityRank));
        }

        private static int ResolveSavedEquipmentQualityThreshold(int qualityRank)
        {
            return ClampEquipmentQualityThreshold(qualityRank > 0 ? qualityRank : 3);
        }

        private static int ClampIndividualValueThreshold(int threshold)
        {
            return Math.Min(MonsterIndividualValueService.MaxValue, Math.Max(1, threshold));
        }

        private static int ResolveSavedIndividualValueThreshold(int threshold)
        {
            return ClampIndividualValueThreshold(threshold > 0 ? threshold : 50);
        }

        private void ClearReleasedMonsterEquipment(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return;
            }

            ClearEquipmentOwner(monster.EquippedWeaponInstanceId);
            ClearEquipmentOwner(monster.EquippedArmorInstanceId);
            ClearEquipmentOwner(monster.EquippedAccessoryInstanceId);
            monster.EquippedWeaponInstanceId = string.Empty;
            monster.EquippedArmorInstanceId = string.Empty;
            monster.EquippedAccessoryInstanceId = string.Empty;
        }

        private void ClearEquipmentOwner(string equipmentInstanceId)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (equipment == null)
            {
                return;
            }

            equipment.EquippedMonsterInstanceId = string.Empty;
            equipment.IsEquipped = false;
        }

        private void ClearReleasedMonsterFromParty(string monsterInstanceId)
        {
            if (string.IsNullOrEmpty(monsterInstanceId))
            {
                return;
            }

            for (int i = 0; i < PartyMonsterInstanceIds.Count; i += 1)
            {
                if (string.Equals(PartyMonsterInstanceIds[i], monsterInstanceId, StringComparison.Ordinal))
                {
                    PartyMonsterInstanceIds[i] = string.Empty;
                }
            }
        }

        private void RefreshMonsterDexOwnedCount(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return;
            }

            MonsterDexEntryData dexEntry = MonsterDexEntries.FirstOrDefault(x => x != null && x.MonsterId == monsterId);
            if (dexEntry == null)
            {
                return;
            }

            dexEntry.IsUnlocked = true;
            dexEntry.OwnedCount = Math.Max(0, GetOwnedMonsterCount(monsterId));
        }

        private static string ResolveMonsterDisplayName(string monsterId)
        {
            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monsterId);
            return monsterData != null && !string.IsNullOrEmpty(monsterData.monsterName)
                ? monsterData.monsterName
                : monsterId ?? string.Empty;
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
