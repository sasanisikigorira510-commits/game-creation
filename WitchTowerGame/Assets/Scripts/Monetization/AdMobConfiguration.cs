using UnityEngine;

namespace WitchTower.Monetization
{
    public static class AdMobConfiguration
    {
        public const string GoogleTestIosAppId = "ca-app-pub-3940256099942544~1458002511";
        public const string GoogleTestIosBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        public const string GoogleTestIosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
        // AdMob placement: 毎日のプレゼント (rewarded).
        public const string ProductionIosRewardedAdUnitId = "ca-app-pub-5913477837491716/6036187005";

        public static string IosRewardedAdUnitId => Application.isEditor || Debug.isDebugBuild
            ? GoogleTestIosRewardedAdUnitId
            : ProductionIosRewardedAdUnitId;

        public const string ProductionIosAppId = "ca-app-pub-5913477837491716~4374078982";
        public const string ProductionIosBannerAdUnitId = "ca-app-pub-5913477837491716/8707191186";

        public static bool HasProductionIosConfiguration =>
            IsAdMobAppId(ProductionIosAppId) &&
            IsAdMobAdUnitId(ProductionIosBannerAdUnitId) &&
            ProductionIosAppId != GoogleTestIosAppId &&
            ProductionIosBannerAdUnitId != GoogleTestIosBannerAdUnitId;

        public static string IosBannerAdUnitId => Debug.isDebugBuild
            ? GoogleTestIosBannerAdUnitId
            : ProductionIosBannerAdUnitId;

        private static bool IsAdMobAppId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.StartsWith("ca-app-pub-", System.StringComparison.Ordinal) &&
                value.Contains("~");
        }

        private static bool IsAdMobAdUnitId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.StartsWith("ca-app-pub-", System.StringComparison.Ordinal) &&
                value.Contains("/");
        }
    }
}
