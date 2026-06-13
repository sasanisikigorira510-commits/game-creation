using System;
using System.Collections.Generic;
using WitchTower.Data;

namespace WitchTower.Home
{
    public sealed class DailyQuestDefinition
    {
        public DailyQuestDefinition(string id, string title, int requiredBattleWins, int rewardFreeGachaStones)
        {
            Id = id;
            Title = title;
            RequiredBattleWins = requiredBattleWins;
            RewardFreeGachaStones = rewardFreeGachaStones;
        }

        public string Id { get; }
        public string Title { get; }
        public int RequiredBattleWins { get; }
        public int RewardFreeGachaStones { get; }
    }

    public static class DailyRewardService
    {
        public const string PrimaryQuestId = "daily_battle_win_1";
        public const int RequiredBattleWins = 1;
        public const int RewardFreeGachaStones = 300;

        private static readonly DailyQuestDefinition[] Definitions =
        {
            new DailyQuestDefinition(PrimaryQuestId, "バトルに1回勝利", 1, RewardFreeGachaStones),
            new DailyQuestDefinition("daily_battle_win_3", "バトルに3回勝利", 3, 100),
            new DailyQuestDefinition("daily_battle_win_5", "バトルに5回勝利", 5, 200)
        };

        public static IReadOnlyList<DailyQuestDefinition> GetDefinitions()
        {
            return Definitions;
        }

        public static DailyQuestDefinition GetDefinition(string questId)
        {
            foreach (DailyQuestDefinition definition in Definitions)
            {
                if (string.Equals(definition.Id, questId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        public static void RecordBattleWin(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return;
            }

            profile.RecordDailyBattleWin(GetDateKey(now));
        }

        public static int GetBattleWinProgress(PlayerProfile profile, DateTime now)
        {
            return GetBattleWinProgress(profile, now, PrimaryQuestId);
        }

        public static int GetBattleWinProgress(PlayerProfile profile, DateTime now, string questId)
        {
            if (profile == null)
            {
                return 0;
            }

            DailyQuestDefinition definition = GetDefinition(questId);
            if (definition == null)
            {
                return 0;
            }

            return Math.Min(definition.RequiredBattleWins, profile.GetDailyBattleWinCount(GetDateKey(now)));
        }

        public static bool IsClaimed(PlayerProfile profile, DateTime now)
        {
            return IsClaimed(profile, now, PrimaryQuestId);
        }

        public static bool IsClaimed(PlayerProfile profile, DateTime now, string questId)
        {
            return profile != null && profile.HasClaimedDailyQuest(GetDateKey(now), questId);
        }

        public static bool IsClaimable(PlayerProfile profile, DateTime now)
        {
            return IsClaimable(profile, now, PrimaryQuestId);
        }

        public static bool IsClaimable(PlayerProfile profile, DateTime now, string questId)
        {
            DailyQuestDefinition definition = GetDefinition(questId);
            if (profile == null || definition == null || IsClaimed(profile, now, questId))
            {
                return false;
            }

            return GetBattleWinProgress(profile, now, questId) >= definition.RequiredBattleWins;
        }

        public static int GetClaimableQuestCount(PlayerProfile profile, DateTime now)
        {
            int count = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                if (IsClaimable(profile, now, definition.Id))
                {
                    count += 1;
                }
            }

            return count;
        }

        public static int GetClaimedQuestCount(PlayerProfile profile, DateTime now)
        {
            int count = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                if (IsClaimed(profile, now, definition.Id))
                {
                    count += 1;
                }
            }

            return count;
        }

        public static bool HasClaimableQuest(PlayerProfile profile, DateTime now)
        {
            return GetClaimableQuestCount(profile, now) > 0;
        }

        public static bool AreAllClaimed(PlayerProfile profile, DateTime now)
        {
            return profile != null && GetClaimedQuestCount(profile, now) >= Definitions.Length;
        }

        public static int GetMaximumRequiredBattleWins()
        {
            int maximum = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                maximum = Math.Max(maximum, definition.RequiredBattleWins);
            }

            return maximum;
        }

        public static int GetTotalRewardFreeGachaStones()
        {
            int total = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                total += definition.RewardFreeGachaStones;
            }

            return total;
        }

        public static int GetClaimableRewardFreeGachaStones(PlayerProfile profile, DateTime now)
        {
            int total = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                if (IsClaimable(profile, now, definition.Id))
                {
                    total += definition.RewardFreeGachaStones;
                }
            }

            return total;
        }

        public static int Claim(PlayerProfile profile, DateTime now)
        {
            return Claim(profile, now, PrimaryQuestId);
        }

        public static int Claim(PlayerProfile profile, DateTime now, string questId)
        {
            if (profile == null)
            {
                return 0;
            }

            DailyQuestDefinition definition = GetDefinition(questId);
            if (definition == null || !IsClaimable(profile, now, questId))
            {
                return 0;
            }

            var dateKey = GetDateKey(now);
            profile.AddFreeGachaStones(definition.RewardFreeGachaStones);
            profile.MarkDailyQuestClaimed(dateKey, questId);
            return definition.RewardFreeGachaStones;
        }

        public static int ClaimAll(PlayerProfile profile, DateTime now)
        {
            int total = 0;
            foreach (DailyQuestDefinition definition in Definitions)
            {
                total += Claim(profile, now, definition.Id);
            }

            return total;
        }

        private static string GetDateKey(DateTime now)
        {
            return now.ToString("yyyy-MM-dd");
        }
    }
}
