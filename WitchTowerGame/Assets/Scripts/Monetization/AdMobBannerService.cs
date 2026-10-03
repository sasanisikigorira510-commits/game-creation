using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Home;
using WitchTower.Managers;

namespace WitchTower.Monetization
{
    [DefaultExecutionOrder(-9100)]
    public sealed class AdMobBannerService : MonoBehaviour
    {
        private const float VisibilityRefreshIntervalSeconds = 0.25f;
        private const float BannerRequestTimeoutSeconds = 60f;
        private BannerView bannerView;
        private bool consentInFlight;
        private bool consentRetryPending;
        private bool initializationStarted;
        private bool adsInitialized;
        private bool bannerCreated;
        private bool bannerLoadInFlight;
        private bool bannerLoaded;
        private bool bannerShown;
        private float bannerHeightPixels;
        private bool disposed;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private int bannerGeneration;
        private int consentFailures;
        private int bannerFailures;
        private float nextConsentRetryAt;
        private float nextInitializationAttemptAt;
        private float nextBannerAttemptAt;
        private float bannerRequestStartedAt;
        private float nextVisibilityRefreshAt;

#if UNITY_EDITOR
        // Isolated tests exercise the actual lifecycle without making ad or consent requests.
        private Action<Action<string>> EditorConsentUpdateOverride;
        private Action<Action<string>> EditorConsentFormOverride;
        private Func<bool> EditorCanRequestAdsOverride;
        private Action<Action<bool>> EditorInitializeOverride;
        private Action<int> EditorLoadBannerOverride;
        private Action<bool> EditorBannerVisibilityOverride;
        private Action EditorDestroyBannerOverride;
        private Func<bool> EditorBannerEligibilityOverride;
        private Func<float> EditorClockOverride;
#endif

        public static AdMobBannerService Instance { get; private set; }
        public bool CanRequestRewardedAds => !disposed && adsInitialized && !consentInFlight && CanRequestAds();
        // Native ads are outside Unity canvases and Screen.safeArea. Overlay
        // controls must reserve their visible height separately.
        public float VisibleHeightPixels => bannerShown ? bannerHeightPixels : 0f;
        public static event Action PrivacyOptionsAvailabilityChanged;
        public static bool IsPrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        private float Now
        {
            get
            {
#if UNITY_EDITOR
                if (EditorClockOverride != null) return EditorClockOverride();
#endif
                return Time.realtimeSinceStartup;
            }
        }

        private bool CanRequestAds()
        {
#if UNITY_EDITOR
            if (EditorCanRequestAdsOverride != null) return EditorCanRequestAdsOverride();
#endif
            return ConsentInformation.CanRequestAds();
        }

        private bool IsBannerPlacementEligible()
        {
#if UNITY_EDITOR
            if (EditorBannerEligibilityOverride != null) return EditorBannerEligibilityOverride();
#endif
            return SceneManager.GetActiveScene().name == "HomeScene" &&
                AdRemovalEntitlementService.ShouldShowBanner(
                    GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!MonetizationFeatureFlags.AdsEnabled || Instance != null) return;
            var root = new GameObject("AdMobBannerService");
            DontDestroyOnLoad(root);
            root.AddComponent<AdMobBannerService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            AdRemovalEntitlementService.EntitlementChanged += RefreshBannerVisibility;
        }

        private void Start()
        {
            // UMP callbacks are queued before MobileAds.Initialize. The dispatcher must already
            // exist: otherwise the first consent callback waits forever for its own initializer.
            MobileAdsEventExecutor.Initialize();
            BeginConsentFlow();
        }

        private void Update()
        {
            if (Now < nextVisibilityRefreshAt) return;
            nextVisibilityRefreshAt = Now + VisibilityRefreshIntervalSeconds;
            ServicePendingWork();
            RefreshBannerVisibility();
        }

        private void ServicePendingWork()
        {
            if (disposed || applicationPaused || !applicationFocused) return;
            if (consentRetryPending && !consentInFlight && Now >= nextConsentRetryAt)
                BeginConsentFlow();
            if (!CanRequestAds())
            {
                DestroyBanner();
                return;
            }
            if (!adsInitialized)
            {
                if (!initializationStarted && Now >= nextInitializationAttemptAt) InitializeAds();
                return;
            }
            if (!IsBannerPlacementEligible()) return;
            if (bannerLoadInFlight && Now - bannerRequestStartedAt >= BannerRequestTimeoutSeconds)
            {
                Debug.LogWarning("[Ads] Banner request timed out; retrying after a delay.");
                DestroyBanner(); // Cancel the native view before accepting a replacement request.
                ScheduleBannerRetry();
            }
            if (!bannerLoaded && !bannerLoadInFlight && Now >= nextBannerAttemptAt)
                CreateAndLoadBanner();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) HideBanner();
            else ResumeBanner();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused) HideBanner();
            else ResumeBanner();
        }

        private void ResumeBanner()
        {
            if (disposed) return;
            nextVisibilityRefreshAt = 0f;
            // Keep retry deadlines and in-flight guards: duplicate pause/focus notifications
            // must not create simultaneous requests or hammer a no-fill response.
            ServicePendingWork();
            RefreshBannerVisibility();
        }

        private void OnDestroy()
        {
            disposed = true;
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            AdRemovalEntitlementService.EntitlementChanged -= RefreshBannerVisibility;
            DestroyBanner();
        }

        public void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(error => QueueCallback(() =>
            {
                if (error != null) Debug.LogWarning($"[Ads] Privacy options could not be shown: {error.Message}");
                PrivacyOptionsAvailabilityChanged?.Invoke();
                if (!CanRequestAds()) DestroyBanner();
                else ServicePendingWork();
                RefreshBannerVisibility();
            }));
        }

        private void QueueCallback(Action action)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (!disposed) action();
            });
        }

        private void BeginConsentFlow()
        {
            if (disposed || consentInFlight) return;
            consentInFlight = true;
            consentRetryPending = false;
            Debug.Log("[Ads] Requesting consent status.");
#if UNITY_EDITOR
            if (EditorConsentUpdateOverride != null)
            {
                EditorConsentUpdateOverride(error => QueueCallback(() => HandleConsentUpdated(error)));
                return;
            }
#endif
            ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false },
                error => QueueCallback(() => HandleConsentUpdated(error?.Message)));
        }

        private void HandleConsentUpdated(string error)
        {
            PrivacyOptionsAvailabilityChanged?.Invoke();
            if (error != null)
            {
                CompleteConsentFlow(error);
                return;
            }
#if UNITY_EDITOR
            if (EditorConsentFormOverride != null)
            {
                EditorConsentFormOverride(formError => QueueCallback(() => CompleteConsentFlow(formError)));
                return;
            }
#endif
            ConsentForm.LoadAndShowConsentFormIfRequired(
                formError => QueueCallback(() => CompleteConsentFlow(formError?.Message)));
        }

        private void CompleteConsentFlow(string error)
        {
            consentInFlight = false;
            PrivacyOptionsAvailabilityChanged?.Invoke();
            if (error != null)
            {
                nextConsentRetryAt = Now + RetryDelay(++consentFailures);
                consentRetryPending = true;
                Debug.LogWarning($"[Ads] Consent request failed: {error}. Retrying in {nextConsentRetryAt - Now:0}s.");
            }
            else consentFailures = 0;

            Debug.Log($"[Ads] Consent completed. Can request ads: {CanRequestAds()}.");
            ServicePendingWork();
            RefreshBannerVisibility();
        }

        private void InitializeAds()
        {
            if (disposed || initializationStarted || adsInitialized || !CanRequestAds()) return;
            initializationStarted = true;
            Debug.Log("[Ads] Initializing Google Mobile Ads.");
#if UNITY_EDITOR
            if (EditorInitializeOverride != null)
            {
                EditorInitializeOverride(success => QueueCallback(() => HandleAdsInitialized(success)));
                return;
            }
#endif
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                AgeRestrictedTreatment = AgeRestrictedTreatment.Unspecified,
                MaxAdContentRating = MaxAdContentRating.T
            });
            MobileAds.Initialize(status => QueueCallback(() => HandleAdsInitialized(status != null)));
        }

        private void HandleAdsInitialized(bool success)
        {
            initializationStarted = false;
            adsInitialized = success;
            if (!success)
            {
                nextInitializationAttemptAt = Now + RetryDelay(1);
                Debug.LogWarning("[Ads] Google Mobile Ads initialization failed; retry scheduled.");
                return;
            }
            Debug.Log("[Ads] Google Mobile Ads initialized.");
            ServicePendingWork();
        }

        private void CreateAndLoadBanner()
        {
            if (disposed || bannerLoadInFlight || !CanRequestAds() || !IsBannerPlacementEligible()) return;
            if (!bannerCreated)
            {
                string adUnitId = AdMobConfiguration.IosBannerAdUnitId;
                if (string.IsNullOrWhiteSpace(adUnitId))
                {
                    nextBannerAttemptAt = Now + 120f;
                    Debug.LogError("[Ads] Production iOS banner ad unit ID is not configured.");
                    return;
                }
                ++bannerGeneration;
                bannerCreated = true;
                bannerLoaded = bannerShown = false;
#if UNITY_EDITOR
                if (EditorLoadBannerOverride == null)
#endif
                {
                    int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
                    AdSize adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(safeWidth);
                    bannerView = new BannerView(adUnitId, adSize, AdPosition.Bottom);
                    bannerView.Hide();
                    int generation = bannerGeneration;
                    bannerView.OnBannerAdLoaded += () => QueueCallback(() => HandleBannerLoaded(generation));
                    bannerView.OnBannerAdLoadFailed += error =>
                        QueueCallback(() => HandleBannerFailed(generation, error?.ToString()));
                }
            }
            bannerLoadInFlight = true;
            bannerRequestStartedAt = Now;
            Debug.Log($"[Ads] Loading home banner ({(Debug.isDebugBuild ? "test" : "production")}).");
#if UNITY_EDITOR
            if (EditorLoadBannerOverride != null)
            {
                EditorLoadBannerOverride(bannerGeneration);
                return;
            }
#endif
            // Reuse the native view on failed initial loads. Once visible, let AdMob own refresh.
            bannerView.LoadAd(new AdRequest());
        }

        private void HandleBannerLoaded(int generation)
        {
            if (disposed || !bannerCreated || generation != bannerGeneration) return;
            bannerLoadInFlight = false;
            bannerLoaded = true;
            bannerHeightPixels = bannerView != null ? bannerView.GetHeightInPixels() : 0f;
            bannerFailures = 0;
            Debug.Log("[Ads] Home banner loaded.");
            RefreshBannerVisibility();
        }

        private void HandleBannerFailed(int generation, string error)
        {
            if (disposed || !bannerCreated || generation != bannerGeneration) return;
            bannerLoadInFlight = false;
            Debug.LogWarning($"[Ads] Banner failed to load: {error}");
            // A failed SDK refresh need not discard the ad already visible. Hidden banners do
            // not auto-refresh, so an initial failure requires our bounded retry schedule.
            if (!bannerLoaded) ScheduleBannerRetry();
            RefreshBannerVisibility();
        }

        private void ScheduleBannerRetry()
        {
            nextBannerAttemptAt = Now + RetryDelay(++bannerFailures);
            Debug.Log($"[Ads] Home banner retry in {nextBannerAttemptAt - Now:0}s.");
        }

        private static float RetryDelay(int failures) => Mathf.Min(120f, 15f * Mathf.Pow(2f, Mathf.Min(failures - 1, 3)));

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ResumeBanner();

        private void RefreshBannerVisibility()
        {
            bool canRequest = !disposed && CanRequestAds();
            bool removedAds = GameManager.Instance != null && GameManager.Instance.PlayerProfile != null &&
                GameManager.Instance.PlayerProfile.HasRemovedAds;
            if (!canRequest || removedAds)
            {
                DestroyBanner();
                return;
            }
            bool shouldShow = bannerLoaded && !applicationPaused && applicationFocused && IsBannerPlacementEligible();
            if (shouldShow && !bannerShown)
            {
                SetNativeBannerVisible(true);
                bannerShown = true;
            }
            else if (!shouldShow && bannerShown) HideBanner();
            FindFirstObjectByType<HomeSceneController>()?.HomeBannerAdController?.SetBannerLoaded(shouldShow);
        }

        private void SetNativeBannerVisible(bool visible)
        {
#if UNITY_EDITOR
            if (EditorBannerVisibilityOverride != null)
            {
                EditorBannerVisibilityOverride(visible);
                return;
            }
#endif
            if (visible) bannerView?.Show();
            else bannerView?.Hide();
        }

        private void HideBanner()
        {
            if (bannerCreated) SetNativeBannerVisible(false);
            bannerShown = false;
            FindFirstObjectByType<HomeSceneController>()?.HomeBannerAdController?.SetBannerLoaded(false);
        }

        private void DestroyBanner()
        {
            HideBanner();
            if (!bannerCreated) return;
            ++bannerGeneration; // Ignore callbacks already queued by the retired native view.
            bannerCreated = bannerLoaded = bannerLoadInFlight = false;
            bannerView?.Destroy();
            bannerView = null;
#if UNITY_EDITOR
            EditorDestroyBannerOverride?.Invoke();
#endif
        }
    }
}
