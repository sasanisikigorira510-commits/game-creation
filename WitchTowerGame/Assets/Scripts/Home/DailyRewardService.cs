using System;
using WitchTower.Data;

namespace WitchTower.Home
{
    public static class DailyRewardService
    {
        public static int Claim(PlayerProfile profile, DateTime now)
        {
            if (profile == null)
            {
                return 0;
            }

            var dateKey = now.ToString("yyyy-MM-dd");
            if (!profile.CanClaimDailyReward(dateKey))
            {
                return 0;
            }

            const int rewardGold = 50;
            profile.AddGold(rewardGold);
            profile.MarkDailyRewardClaimed(dateKey);
            return rewardGold;
        }
    }
}
