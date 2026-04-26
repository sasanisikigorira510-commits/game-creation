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
        private static readonly string[] RequiredPreviewMonsterIds =
        {
            "monster_flare_drake",
            "monster_dragon_whelp",
            "monster_abyss_dragon"
        };

        private static readonly string[] PrototypePartyMonsterIds =
        {
            "monster_flare_drake",
            "monster_dragon_whelp",
            "monster_abyss_dragon",
            "monster_rock_golem",
            "monster_death_mage_elf"
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

            bool changed = false;
            List<string> validPartyIds = ResolveValidPartyIds(profile);
            foreach (string monsterId in PrototypePartyMonsterIds)
            {
                OwnedMonsterData ensuredMonster = EnsureOwnedMonster(profile, masterDataManager, monsterId, out bool addedMonster);
                changed |= addedMonster;
                if (ensuredMonster != null)
                {
                    profile.MarkMonsterDexOwned(ensuredMonster.MonsterId);
                }
            }

            if (profile.OwnedMonsters.Count < targetCount || validPartyIds.Count < targetCount)
            {
                MonsterDataSO[] allMonsterData = masterDataManager.GetAllMonsterData();
                if (allMonsterData != null)
                {
                    foreach (MonsterDataSO monsterData in allMonsterData
                                 .Where(data => data != null && !string.IsNullOrEmpty(data.monsterId))
                                 .OrderBy(data => data.encyclopediaNumber))
                    {
                        if (profile.OwnedMonsters.Count >= targetCount && validPartyIds.Count >= targetCount)
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

                validPartyIds = ResolveValidPartyIds(profile);
            }

            bool shouldPrioritizePreviewParty = IsMissingRequiredPreviewMonster(profile, validPartyIds);
            List<string> resolvedPartyIds = BuildResolvedPartyIds(profile, validPartyIds, targetCount, shouldPrioritizePreviewParty);
            if (!profile.PartyMonsterInstanceIds.SequenceEqual(resolvedPartyIds))
            {
                profile.SetPartyMonsterIds(resolvedPartyIds);
                changed = true;
            }

            return changed;
        }

        private static List<string> ResolveValidPartyIds(PlayerProfile profile)
        {
            var result = new List<string>();
            var seenInstanceIds = new HashSet<string>();
            foreach (string instanceId in profile.PartyMonsterInstanceIds)
            {
                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                {
                    continue;
                }

                result.Add(ownedMonster.InstanceId);
            }

            return result;
        }

        private static bool IsMissingRequiredPreviewMonster(PlayerProfile profile, List<string> validPartyIds)
        {
            if (profile == null)
            {
                return false;
            }

            var selectedMonsterIds = new HashSet<string>();
            foreach (string instanceId in validPartyIds)
            {
                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                if (ownedMonster != null && !string.IsNullOrEmpty(ownedMonster.MonsterId))
                {
                    selectedMonsterIds.Add(ownedMonster.MonsterId);
                }
            }

            return RequiredPreviewMonsterIds.Any(monsterId => !selectedMonsterIds.Contains(monsterId));
        }

        private static List<string> BuildResolvedPartyIds(PlayerProfile profile, List<string> validPartyIds, int targetCount, bool prioritizePreviewParty)
        {
            var resolvedIds = new List<string>();
            var seenInstanceIds = new HashSet<string>();

            void AddPartyInstance(string instanceId)
            {
                if (string.IsNullOrEmpty(instanceId) || resolvedIds.Count >= targetCount)
                {
                    return;
                }

                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                {
                    return;
                }

                resolvedIds.Add(ownedMonster.InstanceId);
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

            if (!prioritizePreviewParty)
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

            foreach (OwnedMonsterData ownedMonster in profile.OwnedMonsters
                         .Where(monster => monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                         .OrderByDescending(monster => monster.AcquiredOrder))
            {
                AddPartyInstance(ownedMonster.InstanceId);
            }

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
                isFavorite: monsterData.rarity >= MonsterRarity.Silver);
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

            int baseLevel = ((int)monsterData.rarity * 5) + monsterData.encyclopediaNumber;
            return Math.Max(1, baseLevel);
        }
    }
}
