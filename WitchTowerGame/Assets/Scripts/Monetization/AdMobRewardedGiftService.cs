using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;
using WitchTower.Home;
using WitchTower.Managers;
using WitchTower.Save;

namespace WitchTower.Monetization
{
    public sealed class AdMobRewardedGiftService : MonoBehaviour
    {
        private RewardedAd readyAd;
        private bool loading;
        private float loadStartedAt, loadedAt, nextLoadAt;
        private int generation;
        private bool disposed;
        private GiftSession session;
#if UNITY_EDITOR
        // Isolated lifecycle tests never contact the advertising or player-data services.
        private Func<string, string, bool> EditorQueueRewardOverride;
        private Action<bool> EditorCompleteRewardOverride;
#endif
        private sealed class GiftSession
        {
            public string PlayerId, Target;
            public RewardedAd Ad;
            public OnlinePlayerData Online;
            public bool RewardCallbackReceived, Queued, Closed;
            public string Error;
        }

        public static AdMobRewardedGiftService Instance { get; private set; }
        public static bool PlacementEnabled => MonetizationFeatureFlags.AdsEnabled &&
            !string.IsNullOrEmpty(AdMobConfiguration.IosRewardedAdUnitId);
        public string Message { get; private set; } = "広告を準備しています。";
        public bool IsBusy => session != null;
        private bool ConsentReady => AdMobBannerService.Instance != null && AdMobBannerService.Instance.CanRequestRewardedAds;
        public bool CanShow => PlacementEnabled && ConsentReady && OnlinePlayerData.AdRewardsAvailable && session == null && readyAd != null &&
            Time.realtimeSinceStartup - loadedAt < 3300f && readyAd.CanShowAd() && !OnlinePlayerData.Busy;

        public static AdMobRewardedGiftService Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("AdMobRewardedGiftService").AddComponent<AdMobRewardedGiftService>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            MobileAdsEventExecutor.Initialize();
        }
        private void Update()
        {
            if (session != null || !PlacementEnabled || !OnlinePlayerData.AdRewardsAvailable) return;
            if (!ConsentReady)
            {
                if (readyAd != null) { readyAd.Destroy(); readyAd = null; }
                if (loading) { generation++; loading = false; }
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (loading && now - loadStartedAt >= 60f)
            {
                generation++; loading = false; nextLoadAt = now + 30f;
                Message = "広告を読み込めませんでした。少し待ってお試しください。";
            }
            if (readyAd != null && (now - loadedAt >= 3300f || !readyAd.CanShowAd()))
            { readyAd.Destroy(); readyAd = null; }
            if (readyAd == null && !loading && now >= nextLoadAt) Load();
        }
        private void Load()
        {
            loading = true;
            loadStartedAt = Time.realtimeSinceStartup;
            int requestGeneration = ++generation;
            RewardedAd.Load(AdMobConfiguration.IosRewardedAdUnitId, new AdRequest(), (ad, error) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (disposed || requestGeneration != generation || !ConsentReady) { ad?.Destroy(); return; }
                    loading = false;
                    if (error != null || ad == null)
                    {
                        ad?.Destroy(); nextLoadAt = Time.realtimeSinceStartup + 30f;
                        Message = "広告を準備できませんでした。少し待ってお試しください。";
                        return;
                    }
                    readyAd = ad; loadedAt = Time.realtimeSinceStartup;
                    Message = "各1日1回・日本時間0時更新";
                }));
        }
        public void Show(string target)
        {
            var profile = GameManager.Instance?.PlayerProfile;
            if (!CanShow || profile == null || string.IsNullOrEmpty(DailyAdRewardCatalog.Label(target))) return;
            if (DailyAdRewardCatalog.IsClaimed(profile, target, DateTime.UtcNow))
            { Message = "このプレゼントは本日受け取り済みです。"; return; }
            var online = OnlinePlayerData.Ensure();
            if (!online.TryReserveRewardedAd(out string error)) { Message = error; return; }
            var current = new GiftSession { PlayerId = profile.PlayerId, Target = target, Ad = readyAd, Online = online };
            session = current; readyAd = null;
            Message = "広告を視聴中です。";
            current.Ad.OnAdFullScreenContentClosed += () => Queue(() => Closed(current));
            current.Ad.OnAdFullScreenContentFailed += _ => Queue(() =>
            {
                current.Error = "広告を表示できませんでした。回数は消費されていません。";
                Closed(current);
            });
            try { current.Ad.Show(_ => Queue(() => Earned(current))); }
            catch (Exception)
            {
                current.Error = "広告を表示できませんでした。回数は消費されていません。";
                Closed(current);
            }
        }
        private void Queue(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (!disposed) action(); });
        private void Earned(GiftSession current)
        {
            if (current.RewardCallbackReceived || current.Closed || session != current) return;
            current.RewardCallbackReceived = true;
#if UNITY_EDITOR
            if (EditorQueueRewardOverride != null)
            {
                current.Queued = EditorQueueRewardOverride(current.PlayerId, current.Target);
                return;
            }
#endif
            current.Queued = current.Online.QueueEarnedAdReward(current.PlayerId, current.Target, out current.Error);
        }
        private void Closed(GiftSession current)
        {
            if (current.Closed || session != current) return;
            current.Closed = true;
            current.Ad?.Destroy();
            Message = current.Queued ? "プレゼントを受け取っています。" :
                current.Error ?? "視聴を完了すると受け取れます。回数は消費されていません。";
#if UNITY_EDITOR
            if (EditorCompleteRewardOverride != null)
            {
                EditorCompleteRewardOverride(current.Queued);
                session = null;
                return;
            }
#endif
            current.Online.CompleteRewardedAd(current.Queued, (op, error) =>
            {
                session = null;
                nextLoadAt = Time.realtimeSinceStartup + 1f;
                if (current.Queued) Message = op != null && op.Kind == "ad_reward" && op.Target == current.Target
                    ? DailyAdRewardCatalog.Label(current.Target) + "を受け取りました。"
                    : error ?? "報酬を確認中です。再接続時に受け取りを再試行します。";
                FindFirstObjectByType<HomeSceneController>()?.RefreshAllPanels();
            });
        }
        private void OnDestroy()
        {
            disposed = true; generation++;
            readyAd?.Destroy();
            session?.Ad?.Destroy();
            if (session?.Online != null) session.Online.CompleteRewardedAd(false, null);
            if (Instance == this) Instance = null;
        }
    }
}
