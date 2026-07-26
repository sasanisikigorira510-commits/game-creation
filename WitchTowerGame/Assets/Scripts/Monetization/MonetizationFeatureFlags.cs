namespace WitchTower.Monetization
{
    /// <summary>
    /// Release-safe gates for integrations that require store or ad-provider credentials.
    /// Enable a feature only in builds that also contain its verified provider integration.
    /// </summary>
    public static class MonetizationFeatureFlags
    {
#if WITCHTOWER_IAP_ENABLED
        public const bool StorefrontEnabled = true;
#else
        public const bool StorefrontEnabled = false;
#endif

#if WITCHTOWER_ADS_ENABLED
        public const bool AdsEnabled = true;
#else
        public const bool AdsEnabled = false;
#endif
    }
}
