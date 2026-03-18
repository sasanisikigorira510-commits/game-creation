using System.Collections.Generic;
using WitchTower.Data;
using WitchTower.Save;

namespace WitchTower.Home
{
    public static class MissionService
    {
        private static readonly Dictionary<string, MissionDefinition> Definitions = new()
        {
            { "mission_clear_1", new MissionDefinition("mission_clear_1", "Win 1 Battle", 1, 30) },
            { "mission_reach_floor_3", new MissionDefinition("mission_reach_floor_3", "Reach Floor 3", 3, 60) }
        };

        public static void RecordBattleWin(PlayerProfile profile)
        {
            IncrementProgress(profile, "mission_clear_1", 1);
        }

        public static void RecordHighestFloor(PlayerProfile profile, int highestFloor)
        {
            var progress = profile?.GetMissionProgress("mission_reach_floor_3");
            if (progress == null)
            {
                return;
            }

            progress.Progress = highestFloor;
        }

        public static int ClaimMission(PlayerProfile profile, string missionId)
        {
            if (profile == null || !Definitions.TryGetValue(missionId, out var definition))
            {
                return 0;
            }

            var progress = profile.GetMissionProgress(missionId);
            if (progress == null || progress.IsClaimed || progress.Progress < definition.TargetValue)
            {
                return 0;
            }

            progress.IsClaimed = true;
            profile.AddGold(definition.RewardGold);
            return definition.RewardGold;
        }

        public static MissionDefinition? GetDefinition(string missionId)
        {
            return Definitions.TryGetValue(missionId, out var definition) ? definition : null;
        }

        private static void IncrementProgress(PlayerProfile profile, string missionId, int amount)
        {
            var progress = profile?.GetMissionProgress(missionId);
            if (progress == null)
            {
                return;
            }

            progress.Progress += amount;
        }
    }
}
