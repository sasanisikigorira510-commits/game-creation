using System;
using WitchTower.Data;

namespace WitchTower.Home
{
    public static class IdleRewardService
    {
        private const int MaxIdleHours = 8;
        private const int GoldPerMinute = 2;

        public static void EvaluatePendingReward(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(profile.LastActiveAt))
            {
                profile.LastActiveAt = now.ToString("O");
                return;
            }

            if (!DateTime.TryParse(profile.LastActiveAt, out var lastActiveAt))
            {
                profile.LastActiveAt = now.ToString("O");
                return;
            }

            var elapsed = now - lastActiveAt;
            if (elapsed <= TimeSpan.Zero)
            {
                profile.LastActiveAt = now.ToString("O");
                return;
            }

            var cappedMinutes = Math.Min((int)elapsed.TotalMinutes, MaxIdleHours * 60);
            if (cappedMinutes > 0)
            {
                profile.AddPendingIdleReward(cappedMinutes * GoldPerMinute);
            }

            profile.LastActiveAt = now.ToString("O");
        }

        public static int Claim(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return 0;
            }

            var reward = profile.ClaimPendingIdleReward();
            profile.LastActiveAt = now.ToString("O");
            return reward;
        }
    }
}
