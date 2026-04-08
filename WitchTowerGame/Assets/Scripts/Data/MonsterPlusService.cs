using System;
using System.Collections.Generic;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed class MonsterPlusGrantResult
    {
        public bool Granted;
        public string MonsterInstanceId;
        public MonsterPlusStatType StatType;
    }

    public static class MonsterPlusService
    {
        public const double PlusGrantChancePerKill = 0.00005d; // 0.005%

        public static bool TryRollAndGrant(PlayerProfile profile, Random random, out MonsterPlusGrantResult result)
        {
            result = new MonsterPlusGrantResult { Granted = false, MonsterInstanceId = string.Empty, StatType = MonsterPlusStatType.Hp };
            if (profile == null)
            {
                return false;
            }

            Random rng = random ?? new Random();
            if (rng.NextDouble() > PlusGrantChancePerKill)
            {
                return false;
            }

            List<OwnedMonsterData> candidates = ResolveCandidates(profile);
            if (candidates.Count <= 0)
            {
                return false;
            }

            OwnedMonsterData target = candidates[rng.Next(candidates.Count)];
            MonsterPlusStatType statType = (MonsterPlusStatType)rng.Next(0, 5);
            if (!profile.TryApplyMonsterPlus(target.InstanceId, statType, 1))
            {
                return false;
            }

            result = new MonsterPlusGrantResult
            {
                Granted = true,
                MonsterInstanceId = target.InstanceId,
                StatType = statType
            };
            return true;
        }

        private static List<OwnedMonsterData> ResolveCandidates(PlayerProfile profile)
        {
            var result = new List<OwnedMonsterData>();
            if (profile.PartyMonsterInstanceIds != null)
            {
                for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
                {
                    string instanceId = profile.PartyMonsterInstanceIds[i];
                    OwnedMonsterData monster = profile.GetOwnedMonster(instanceId);
                    if (monster != null)
                    {
                        result.Add(monster);
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            if (profile.OwnedMonsters != null)
            {
                for (int i = 0; i < profile.OwnedMonsters.Count; i += 1)
                {
                    OwnedMonsterData monster = profile.OwnedMonsters[i];
                    if (monster != null)
                    {
                        result.Add(monster);
                    }
                }
            }

            return result;
        }
    }
}
