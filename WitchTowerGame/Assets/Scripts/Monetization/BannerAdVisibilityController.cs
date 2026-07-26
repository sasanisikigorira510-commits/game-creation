using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Monetization
{
    /// <summary>
    /// Attach this to a future banner-ad host. The ad SDK should call
    /// <see cref="SetBannerLoaded"/> after it has a banner ready and route its verified
    /// remove-ads purchase callback through <see cref="ApplyVerifiedPurchase"/>.
    /// </summary>
    public sealed class BannerAdVisibilityController : MonoBehaviour
    {
        [SerializeField] private GameObject bannerContent;

        private bool providerHasLoadedBanner;

        public bool IsBannerVisible { get; private set; }
        public RectTransform BannerContainer => bannerContent != null ? bannerContent.transform as RectTransform : null;

        private void OnEnable()
        {
            AdRemovalEntitlementService.EntitlementChanged += RefreshVisibility;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            AdRemovalEntitlementService.EntitlementChanged -= RefreshVisibility;
        }

        public void SetBannerLoaded(bool hasLoadedBanner)
        {
            providerHasLoadedBanner = hasLoadedBanner;
            RefreshVisibility();
        }

        public void SetBannerContent(GameObject content)
        {
            bannerContent = content;
            RefreshVisibility();
        }

        public bool ApplyVerifiedPurchase(string productId)
        {
            bool applied = AdRemovalEntitlementService.TryGrantVerifiedPurchase(
                GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null,
                productId);
            if (applied)
            {
                SaveManager.Instance?.SaveCurrentGame();
                RefreshVisibility();
            }

            return applied;
        }

        public void RefreshVisibility()
        {
            bool shouldShow = MonetizationFeatureFlags.AdsEnabled &&
                providerHasLoadedBanner &&
                AdRemovalEntitlementService.ShouldShowBanner(
                    GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null);
            IsBannerVisible = shouldShow;

            if (bannerContent != null)
            {
                bannerContent.SetActive(shouldShow);
            }
        }
    }
}
