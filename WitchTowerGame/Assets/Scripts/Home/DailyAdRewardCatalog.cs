using System;
using System.Collections.Generic;
using WitchTower.Data;

namespace WitchTower.Home
{
    public static class DailyAdRewardCatalog
    {
        public static readonly IReadOnlyList<string> Ids = Array.AsReadOnly(new[] { "ad_stones", "ad_relics", "ad_gold" });
        public static string Label(string id) => id switch
        {
            "ad_stones" => "無償魔晶石 600個",
            "ad_relics" => "通常遺物 5個",
            "ad_gold" => "ゴールド 3,000",
            _ => string.Empty
        };
        public static string DateKey(DateTime utcNow) => utcNow.ToUniversalTime().AddHours(9).ToString("yyyy-MM-dd");
        public static bool IsClaimed(PlayerProfile profile, string id, DateTime utcNow) =>
            profile != null && profile.DailyAdRewardDate == DateKey(utcNow) && profile.DailyClaimedAdRewardIds.Contains(id);
    }
}
