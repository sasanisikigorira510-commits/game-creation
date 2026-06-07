using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public static class PrototypePartyBootstrapService
    {
        private const int DefaultPartySize = 5;
        private const int DefaultStorageLimit = 100;
        private static readonly bool UnlockAllImplementedMonstersForPreview = true;
        private static readonly string[] PrototypePartyMonsterIds =
        {
            "monster_dragon_whelp",
            "monster_chibi_gear",
            "monster_rock_golem",
            "monster_apprentice_swordsman",
            "monster_apprentice_mage"
        };

        private static readonly string[] PrototypeOwnedMonsterIds =
        {
            "monster_dragon_whelp",
            "monster_flare_drake",
            "monster_abyss_dragon",
            "monster_chibi_gear",
            "monster_armed_droid",
            "monster_omega_leon",
            "monster_rock_golem",
            "monster_ore_giant_garm",
            "monster_cosmic_ore_fortress_golem",
            "monster_apprentice_swordsman",
            "monster_holy_armor_leon",
            "monster_sword_saint_alvarez",
            "monster_apprentice_mage",
            "monster_dark_robe_curse_mage_noah",
            "monster_abyss_grand_mage_seraphis"
        };

        public static bool EnsureParty(PlayerProfile profile, int desiredPartyCount = DefaultPartySize)
        {
            if (profile == null)
            {
                return false;
            }

            int targetCount = Math.Min(DefaultPartySize, Math.Max(1, desiredPartyCount));
            MasterDataManager.Instance?.Initialize();
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            if (masterDataManager == null)
            {
                return false;
            }

            bool changed = RemoveOwnedMonstersMissingFromCurrentMaster(profile, masterDataManager);
            List<string> validPartyIds = ResolveValidPartyIds(profile, targetCount);
            if (UnlockAllImplementedMonstersForPreview)
            {
                changed |= EnsureAllImplementedMonstersOwned(profile, masterDataManager);
                validPartyIds = ResolveValidPartyIds(profile, targetCount);
            }
            else if (profile.OwnedMonsters.Count == 0)
            {
                foreach (string monsterId in PrototypeOwnedMonsterIds)
                {
                    OwnedMonsterData ensuredMonster = EnsureOwnedMonster(profile, masterDataManager, monsterId, out bool addedMonster);
                    changed |= addedMonster;
                    if (ensuredMonster != null)
                    {
                        profile.MarkMonsterDexOwned(ensuredMonster.MonsterId);
                    }
                }

                validPartyIds = ResolveValidPartyIds(profile, targetCount);
            }

            if (profile.OwnedMonsters.Count < targetCount)
            {
                MonsterDataSO[] allMonsterData = masterDataManager.GetAllMonsterData();
                if (allMonsterData != null)
                {
                    foreach (MonsterDataSO monsterData in allMonsterData
                                 .Where(data => data != null && !string.IsNullOrEmpty(data.monsterId))
                                 .OrderBy(data => data.encyclopediaNumber))
                    {
                        if (profile.OwnedMonsters.Count >= targetCount)
                        {
                            break;
                        }

                        OwnedMonsterData ensuredMonster = EnsureOwnedMonster(profile, masterDataManager, monsterData.monsterId, out bool addedMonster);
                        changed |= addedMonster;
                        if (ensuredMonster != null)
                        {
                            profile.MarkMonsterDexOwned(ensuredMonster.MonsterId);
                        }
                    }
                }

                validPartyIds = ResolveValidPartyIds(profile, targetCount);
            }

            bool shouldPrioritizePreviewParty = CountValidPartySlots(validPartyIds) == 0;
            List<string> resolvedPartyIds = BuildResolvedPartyIds(
                profile,
                validPartyIds,
                targetCount,
                shouldPrioritizePreviewParty,
                fillOpenSlots: shouldPrioritizePreviewParty);
            if (!profile.PartyMonsterInstanceIds.SequenceEqual(resolvedPartyIds))
            {
                profile.SetPartyMonsterIds(resolvedPartyIds);
                changed = true;
            }

            return changed;
        }

        private static bool RemoveOwnedMonstersMissingFromCurrentMaster(PlayerProfile profile, MasterDataManager masterDataManager)
        {
            if (profile?.OwnedMonsters == null || masterDataManager == null)
            {
                return false;
            }

            MonsterDataSO[] allMonsterData = masterDataManager.GetAllMonsterData();
            if (allMonsterData == null || allMonsterData.Length == 0)
            {
                return false;
            }

            var currentMonsterIds = new HashSet<string>(
                allMonsterData
                    .Where(data => data != null && !string.IsNullOrEmpty(data.monsterId))
                    .Select(data => data.monsterId));
            if (currentMonsterIds.Count == 0)
            {
                return false;
            }

            var removedInstanceIds = new HashSet<string>();
            bool changed = false;
            for (int i = profile.OwnedMonsters.Count - 1; i >= 0; i -= 1)
            {
                OwnedMonsterData ownedMonster = profile.OwnedMonsters[i];
                bool shouldRemove = ownedMonster == null ||
                    string.IsNullOrEmpty(ownedMonster.MonsterId) ||
                    !currentMonsterIds.Contains(ownedMonster.MonsterId);
                if (!shouldRemove)
                {
                    continue;
                }

                if (ownedMonster != null && !string.IsNullOrEmpty(ownedMonster.InstanceId))
                {
                    removedInstanceIds.Add(ownedMonster.InstanceId);
                }

                profile.OwnedMonsters.RemoveAt(i);
                changed = true;
            }

            if (removedInstanceIds.Count > 0)
            {
                ClearRemovedPartyReferences(profile, removedInstanceIds);
                ClearRemovedEquipmentReferences(profile, removedInstanceIds);
            }

            if (profile.MonsterDexEntries != null)
            {
                for (int i = profile.MonsterDexEntries.Count - 1; i >= 0; i -= 1)
                {
                    MonsterDexEntryData dexEntry = profile.MonsterDexEntries[i];
                    bool shouldRemove = dexEntry == null ||
                        string.IsNullOrEmpty(dexEntry.MonsterId) ||
                        !currentMonsterIds.Contains(dexEntry.MonsterId);
                    if (!shouldRemove)
                    {
                        continue;
                    }

                    profile.MonsterDexEntries.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private static void ClearRemovedPartyReferences(PlayerProfile profile, HashSet<string> removedInstanceIds)
        {
            if (profile?.PartyMonsterInstanceIds == null || removedInstanceIds == null || removedInstanceIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
            {
                if (removedInstanceIds.Contains(profile.PartyMonsterInstanceIds[i]))
                {
                    profile.PartyMonsterInstanceIds[i] = string.Empty;
                }
            }
        }

        private static void ClearRemovedEquipmentReferences(PlayerProfile profile, HashSet<string> removedInstanceIds)
        {
            if (profile?.OwnedEquipments == null || removedInstanceIds == null || removedInstanceIds.Count == 0)
            {
                return;
            }

            foreach (OwnedEquipmentData equipment in profile.OwnedEquipments)
            {
                if (equipment != null && removedInstanceIds.Contains(equipment.EquippedMonsterInstanceId))
                {
                    equipment.EquippedMonsterInstanceId = string.Empty;
                    equipment.IsEquipped = false;
                }
            }
        }

        private static bool EnsureAllImplementedMonstersOwned(PlayerProfile profile, MasterDataManager masterDataManager)
        {
            if (profile == null || masterDataManager == null)
            {
                return false;
            }

            MonsterDataSO[] allMonsterData = masterDataManager.GetAllMonsterData();
            if (allMonsterData == null || allMonsterData.Length == 0)
            {
                return false;
            }

            var implementedMonsterIds = allMonsterData
                .Where(data => data != null && !string.IsNullOrEmpty(data.monsterId))
                .OrderBy(data => Math.Max(1, data.classRank))
                .ThenBy(data => data.encyclopediaNumber)
                .Select(data => data.monsterId)
                .Distinct()
                .ToList();

            if (implementedMonsterIds.Count == 0)
            {
                return false;
            }

            bool changed = false;
            int missingMonsterCount = implementedMonsterIds.Count(monsterId => ResolveOwnedMonsterByMonsterId(profile, monsterId) == null);
            int requiredStorageLimit = Math.Max(DefaultStorageLimit, profile.OwnedMonsters.Count + missingMonsterCount);
            if (profile.MonsterStorageLimit < requiredStorageLimit)
            {
                profile.MonsterStorageLimit = requiredStorageLimit;
                changed = true;
            }

            foreach (string monsterId in implementedMonsterIds)
            {
                OwnedMonsterData ensuredMonster = EnsureOwnedMonster(profile, masterDataManager, monsterId, out bool addedMonster);
                changed |= addedMonster;
                if (ensuredMonster != null)
                {
                    profile.MarkMonsterDexOwned(ensuredMonster.MonsterId);
                }
            }

            return changed;
        }

        private static List<string> ResolveValidPartyIds(PlayerProfile profile, int targetCount)
        {
            var result = new List<string>();
            var seenInstanceIds = new HashSet<string>();
            int safeTargetCount = Math.Min(DefaultPartySize, Math.Max(1, targetCount));
            for (int i = 0; i < safeTargetCount; i += 1)
            {
                string instanceId = profile.PartyMonsterInstanceIds != null && i < profile.PartyMonsterInstanceIds.Count
                    ? profile.PartyMonsterInstanceIds[i]
                    : string.Empty;
                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                {
                    result.Add(string.Empty);
                    continue;
                }

                result.Add(ownedMonster.InstanceId);
            }

            return result;
        }

        private static int CountValidPartySlots(List<string> partyIds)
        {
            return partyIds != null ? partyIds.Count(instanceId => !string.IsNullOrEmpty(instanceId)) : 0;
        }

        private static List<string> BuildResolvedPartyIds(
            PlayerProfile profile,
            List<string> validPartyIds,
            int targetCount,
            bool prioritizePreviewParty,
            bool fillOpenSlots)
        {
            var resolvedIds = new List<string>();
            var seenInstanceIds = new HashSet<string>();

            bool TryResolvePartyInstance(string instanceId, out string resolvedInstanceId)
            {
                resolvedInstanceId = string.Empty;
                if (string.IsNullOrEmpty(instanceId))
                {
                    return false;
                }

                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                {
                    return false;
                }

                resolvedInstanceId = ownedMonster.InstanceId;
                return true;
            }

            void EnsureSlotCount()
            {
                while (resolvedIds.Count < targetCount)
                {
                    resolvedIds.Add(string.Empty);
                }

                if (resolvedIds.Count > targetCount)
                {
                    resolvedIds.RemoveRange(targetCount, resolvedIds.Count - targetCount);
                }
            }

            void AddPartyInstance(string instanceId)
            {
                if (!TryResolvePartyInstance(instanceId, out string resolvedInstanceId))
                {
                    return;
                }

                int emptySlotIndex = resolvedIds.FindIndex(string.IsNullOrEmpty);
                if (emptySlotIndex >= 0)
                {
                    resolvedIds[emptySlotIndex] = resolvedInstanceId;
                    return;
                }

                if (resolvedIds.Count < targetCount)
                {
                    resolvedIds.Add(resolvedInstanceId);
                }
            }

            if (!prioritizePreviewParty)
            {
                for (int i = 0; i < targetCount; i += 1)
                {
                    string instanceId = validPartyIds != null && i < validPartyIds.Count ? validPartyIds[i] : string.Empty;
                    resolvedIds.Add(TryResolvePartyInstance(instanceId, out string resolvedInstanceId)
                        ? resolvedInstanceId
                        : string.Empty);
                }
            }

            if (prioritizePreviewParty)
            {
                foreach (string monsterId in PrototypePartyMonsterIds)
                {
                    OwnedMonsterData ownedMonster = ResolveOwnedMonsterByMonsterId(profile, monsterId);
                    if (ownedMonster != null)
                    {
                        AddPartyInstance(ownedMonster.InstanceId);
                    }
                }
            }

            foreach (string instanceId in validPartyIds)
            {
                AddPartyInstance(instanceId);
            }

            if (!prioritizePreviewParty && fillOpenSlots)
            {
                foreach (string monsterId in PrototypePartyMonsterIds)
                {
                    OwnedMonsterData ownedMonster = ResolveOwnedMonsterByMonsterId(profile, monsterId);
                    if (ownedMonster != null)
                    {
                        AddPartyInstance(ownedMonster.InstanceId);
                    }
                }
            }

            if (fillOpenSlots)
            {
                foreach (OwnedMonsterData ownedMonster in profile.OwnedMonsters
                             .Where(monster => monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                             .OrderByDescending(monster => monster.AcquiredOrder))
                {
                    AddPartyInstance(ownedMonster.InstanceId);
                }
            }

            EnsureSlotCount();
            return resolvedIds;
        }

        private static OwnedMonsterData EnsureOwnedMonster(
            PlayerProfile profile,
            MasterDataManager masterDataManager,
            string monsterId,
            out bool addedMonster)
        {
            addedMonster = false;
            if (string.IsNullOrEmpty(monsterId))
            {
                return null;
            }

            OwnedMonsterData existingMonster = ResolveOwnedMonsterByMonsterId(profile, monsterId);
            if (existingMonster != null)
            {
                return existingMonster;
            }

            MonsterDataSO monsterData = masterDataManager.GetMonsterData(monsterId);
            if (monsterData == null)
            {
                return null;
            }

            addedMonster = true;
            return profile.AddOwnedMonster(
                monsterId,
                ResolvePrototypeLevel(monsterData),
                plusValue: 0,
                isFavorite: monsterData.classRank >= 3);
        }

        private static OwnedMonsterData ResolveOwnedMonsterByMonsterId(PlayerProfile profile, string monsterId)
        {
            return profile.OwnedMonsters.FirstOrDefault(monster => monster != null && monster.MonsterId == monsterId);
        }

        private static int ResolvePrototypeLevel(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return 1;
            }

            int baseLevel = (Math.Max(1, monsterData.classRank) * 5) + monsterData.encyclopediaNumber;
            return Math.Max(1, baseLevel);
        }
    }
}
