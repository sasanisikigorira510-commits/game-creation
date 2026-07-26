using System;
using WitchTower.Data;

namespace WitchTower.Monetization
{
    /// <summary>
    /// Central policy for every banner placement. Store integrations must call
    /// <see cref="TryGrantVerifiedPurchase"/> only after their purchase receipt has been verified.
    /// </summary>
    public static class AdRemovalEntitlementService
    {
        public const string RemoveAdsProductId = "witchtower.remove_ads";

        public static event Action EntitlementChanged;

        public static bool ShouldShowBanner(PlayerProfile profile)
        {
            return profile != null && !profile.HasRemovedAds;
        }

        public static bool TryGrantVerifiedPurchase(PlayerProfile profile, string productId)
        {
            if (profile == null || !string.Equals(productId, RemoveAdsProductId, StringComparison.Ordinal))
            {
                return false;
            }

            if (profile.HasRemovedAds)
            {
                return true;
            }

            profile.HasRemovedAds = true;
            EntitlementChanged?.Invoke();
            return true;
        }
    }
}
