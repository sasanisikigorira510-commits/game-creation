using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AdMobBannerLifecycleTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private Component service;
        private Component oldExecutor;
        private bool oldExecutorActive;
        private object oldService, oldGame, oldIap;
        private Component iapSentinel;
        private SceneSetup[] originalScenes;
        private bool previousPlayModeOptionsEnabled;
        private EnterPlayModeOptions previousPlayModeOptions;
        private float now;
        private bool consentAllowed, eligible;
        private int consentUpdates, consentForms, initializations, destroyedViews;
        private readonly List<int> requests = new List<int>();
        private readonly List<bool> visibility = new List<bool>();
        private Action<string> consentCallback, formCallback;
        private Action<bool> initializationCallback;

        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static Type Executor => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GoogleMobileAds.Common.MobileAdsEventExecutor")).First(t => t != null);
        private static object Call(object obj, string name, params object[] args) => obj.GetType()
            .GetMethod(name, Hidden | BindingFlags.Public).Invoke(obj, args);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
        private static object Get(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
        private static void Singleton(Type type, object value) => type.GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private static Component CurrentExecutor => (Component)Executor.GetField("instance").GetValue(null);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
            // Keep inactive test doubles through BeforeSceneLoad. This prevents the runtime
            // installers from starting real consent/ad/store integrations during PlayMode entry.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload |
                EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            now = 0;
            consentAllowed = false;
            eligible = true;
            consentUpdates = consentForms = initializations = destroyedViews = 0;
            requests.Clear();
            visibility.Clear();
            consentCallback = formCallback = null;
            initializationCallback = null;
            oldService = Runtime("Monetization.AdMobBannerService").GetProperty("Instance").GetValue(null);
            oldGame = Runtime("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldIap = Runtime("Monetization.InAppPurchaseService").GetProperty("Instance").GetValue(null);
            var iapRoot = new GameObject("InactiveIapInstallerGuard");
            iapRoot.SetActive(false);
            iapSentinel = iapRoot.AddComponent(Runtime("Monetization.InAppPurchaseService"));
            Singleton(iapSentinel.GetType(), iapSentinel);
            Singleton(Runtime("Monetization.AdMobBannerService"), null);
            Singleton(Runtime("Managers.GameManager"), null);
            oldExecutor = CurrentExecutor;
            if (oldExecutor != null)
            {
                oldExecutorActive = oldExecutor.gameObject.activeSelf;
                oldExecutor.gameObject.SetActive(false);
            }
            Executor.GetField("instance").SetValue(null, null);
            var root = new GameObject("IsolatedBannerService");
            root.SetActive(false);
            service = root.AddComponent(Runtime("Monetization.AdMobBannerService"));
            Singleton(service.GetType(), service);
            Set(service, "EditorClockOverride", (Func<float>)(() => now));
            Set(service, "EditorCanRequestAdsOverride", (Func<bool>)(() => consentAllowed));
            Set(service, "EditorBannerEligibilityOverride", (Func<bool>)(() => eligible));
            Set(service, "EditorConsentUpdateOverride", (Action<Action<string>>)(callback =>
            {
                ++consentUpdates;
                consentCallback = callback;
            }));
            Set(service, "EditorConsentFormOverride", (Action<Action<string>>)(callback =>
            {
                ++consentForms;
                formCallback = callback;
            }));
            Set(service, "EditorInitializeOverride", (Action<Action<bool>>)(callback =>
            {
                ++initializations;
                initializationCallback = callback;
            }));
            Set(service, "EditorLoadBannerOverride", (Action<int>)(generation => requests.Add(generation)));
            Set(service, "EditorBannerVisibilityOverride", (Action<bool>)(visible => visibility.Add(visible)));
            Set(service, "EditorDestroyBannerOverride", (Action)(() => ++destroyedViews));
            yield return new EnterPlayMode(false);
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(Runtime("Monetization.AdMobBannerService").GetProperty("Instance").GetValue(null),
                Is.SameAs(service), "The runtime installer must retain the isolated service.");
            Assert.That(Runtime("Monetization.InAppPurchaseService").GetProperty("Instance").GetValue(null),
                Is.SameAs(iapSentinel), "No real store may start while testing ads.");
            Assert.That(consentUpdates, Is.Zero, "The service remains inactive until the test calls Start.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (service != null)
            {
                Call(service, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
            // Drain only this test's retired callbacks before removing its dispatcher.
            if (CurrentExecutor != null)
            {
                Pump();
                UnityEngine.Object.DestroyImmediate(CurrentExecutor.gameObject);
            }
            if (iapSentinel != null) UnityEngine.Object.DestroyImmediate(iapSentinel.gameObject);
            Singleton(Runtime("Monetization.AdMobBannerService"), null);
            Singleton(Runtime("Monetization.InAppPurchaseService"), null);
            Singleton(Runtime("Managers.GameManager"), null);
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayModeOptions;
            if (oldExecutor != null) oldExecutor.gameObject.SetActive(oldExecutorActive);
            Executor.GetField("instance").SetValue(null, oldExecutor);
            Singleton(Runtime("Monetization.AdMobBannerService"), oldService);
            Singleton(Runtime("Monetization.InAppPurchaseService"), oldIap);
            Singleton(Runtime("Managers.GameManager"), oldGame);
            if (originalScenes.Length > 0 && originalScenes.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Pump() => Call(CurrentExecutor, "Update");
        private void Tick(float time)
        {
            now = time;
            Call(service, "Update");
        }
        private void Ready()
        {
            Call(service, "Start");
            consentCallback(null);
            Pump();
            consentAllowed = true;
            formCallback(null);
            Pump();
            initializationCallback(true);
            Pump();
            Assert.That(requests.Count, Is.EqualTo(1));
        }
        private void Loaded() => Call(service, "HandleBannerLoaded", requests.Last());
        private void Failed() => Call(service, "HandleBannerFailed", requests.Last(), "simulated network error");

        [Test]
        public void FirstLaunchCreatesDispatcherBeforeConsentCallbackAndReachesBannerLoad()
        {
            Assert.That(CurrentExecutor, Is.Null);
            Call(service, "Start");
            Assert.That(CurrentExecutor, Is.Not.Null, "Consent completion must not wait for the SDK it initializes.");
            Assert.That(consentUpdates, Is.EqualTo(1));
            Assert.That(initializations, Is.Zero);
            consentCallback(null);
            Assert.That(consentForms, Is.Zero, "Native callback is queued onto the Unity thread.");
            Pump();
            Assert.That(consentForms, Is.EqualTo(1));
            consentAllowed = true;
            formCallback(null);
            Pump();
            Assert.That(initializations, Is.EqualTo(1));
            initializationCallback(true);
            Pump();
            Assert.That(requests.Count, Is.EqualTo(1));
            Loaded();
            Assert.That(visibility.Last(), Is.True);
        }

        [Test]
        public void RequestsStayBlockedUntilConsentAllowsAds()
        {
            Call(service, "Start");
            consentCallback(null);
            Pump();
            formCallback(null);
            Pump();
            Tick(200f);
            Assert.That(initializations, Is.Zero);
            Assert.That(requests, Is.Empty);
            Assert.That(visibility, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void FirstPurchaseDestroysBannerAndKeepsRewardedSdkAvailable(bool alreadyLoaded)
        {
            Ready();
            int originalGeneration = requests.Last();
            if (alreadyLoaded) Loaded();
            var root = new GameObject("PurchasedPlayer");
            root.SetActive(false);
            try
            {
                var game = root.AddComponent(Runtime("Managers.GameManager"));
                var save = Runtime("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                var profile = Activator.CreateInstance(Runtime("Data.PlayerProfile"), save);
                Set(game, "<PlayerProfile>k__BackingField", profile);
                Singleton(game.GetType(), game);
                Assert.That(profile.GetType().GetMethod("TryGrantPaidStonePurchase").Invoke(profile,
                    new object[] { "com.nasus.dungeonmonsterroguelike.crystals120", 120, "verified-first-purchase" }), Is.True);
                eligible = false;
                Tick(1f);
                Assert.That(destroyedViews, Is.EqualTo(1));
                Assert.That(visibility.Last(), Is.False);
                Call(service, "HandleBannerLoaded", originalGeneration);
                Tick(500f);
                Assert.That(requests, Has.Count.EqualTo(1));
                Assert.That(visibility.Last(), Is.False, "A late banner callback must not bring the ad back.");
                Assert.That(service.GetType().GetProperty("CanRequestRewardedAds").GetValue(service), Is.True);
            }
            finally
            {
                Singleton(Runtime("Managers.GameManager"), null);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConsentNetworkFailureRetriesWithoutBypassingConsentOrDuplicatingFlow()
        {
            Call(service, "Start");
            consentCallback("offline");
            Pump();
            Tick(14f);
            Assert.That(consentUpdates, Is.EqualTo(1));
            Tick(15f);
            Assert.That(consentUpdates, Is.EqualTo(2));
            Tick(60f);
            Call(service, "OnApplicationFocus", true);
            Assert.That(consentUpdates, Is.EqualTo(2), "Never overlap a pending form/update.");
            consentCallback(null);
            Pump();
            consentAllowed = true;
            formCallback(null);
            Pump();
            initializationCallback(true);
            Pump();
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void InitializationFailureRetriesOnceAfterDelay()
        {
            Call(service, "Start");
            consentCallback(null);
            Pump();
            consentAllowed = true;
            formCallback(null);
            Pump();
            initializationCallback(false);
            Pump();
            Tick(14f);
            Assert.That(initializations, Is.EqualTo(1));
            Tick(15f);
            Tick(16f);
            Assert.That(initializations, Is.EqualTo(2));
            initializationCallback(true);
            Pump();
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void InitialLoadFailureRetriesSameViewWithCappedBackoff()
        {
            Ready();
            int originalGeneration = requests[0];
            float[] delays = { 15f, 30f, 60f, 120f, 120f };
            for (int i = 0; i < delays.Length; ++i)
            {
                Failed();
                float deadline = now + delays[i];
                Tick(deadline - 1f);
                Assert.That(requests.Count, Is.EqualTo(i + 1));
                Tick(deadline);
                Assert.That(requests.Count, Is.EqualTo(i + 2));
                Assert.That(requests.Last(), Is.EqualTo(originalGeneration));
            }
            Assert.That(destroyedViews, Is.Zero);
            Loaded();
            Tick(now + 1000f);
            Assert.That(requests.Count, Is.EqualTo(6), "Loaded ads use SDK refresh, not a parallel refresh loop.");
        }

        [Test]
        public void ForegroundAndHomeReturnRecoverDueRetryWithoutDuplicateRequests()
        {
            Ready();
            Failed();
            eligible = false;
            Call(service, "OnApplicationPause", true);
            Tick(30f);
            Assert.That(requests.Count, Is.EqualTo(1));
            Call(service, "OnApplicationPause", false);
            Assert.That(requests.Count, Is.EqualTo(1), "No ad request outside Home.");
            eligible = true;
            Call(service, "ResumeBanner");
            Call(service, "OnApplicationFocus", true);
            Call(service, "OnApplicationPause", false);
            Tick(31f);
            Assert.That(requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void BackgroundAndOtherScenesHideLoadedBannerAndHomeRestoresIt()
        {
            Ready();
            Loaded();
            Call(service, "OnApplicationPause", true);
            Assert.That(visibility.Last(), Is.False);
            Call(service, "OnApplicationFocus", false);
            Call(service, "OnApplicationPause", false);
            Assert.That(visibility.Last(), Is.False);
            Call(service, "OnApplicationFocus", true);
            Assert.That(visibility.Last(), Is.True);
            eligible = false;
            Call(service, "ResumeBanner");
            Assert.That(visibility.Last(), Is.False);
            eligible = true;
            Call(service, "ResumeBanner");
            Assert.That(visibility.Last(), Is.True);
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void RevokingConsentCancelsViewAndIgnoresItsLateCallbacks()
        {
            Ready();
            int stale = requests[0];
            consentAllowed = false;
            Tick(1f);
            Assert.That(destroyedViews, Is.EqualTo(1));
            Call(service, "HandleBannerLoaded", stale);
            Assert.That((bool)Get(service, "bannerLoaded"), Is.False);
            consentAllowed = true;
            Tick(2f);
            Assert.That(requests.Count, Is.EqualTo(2));
            Call(service, "HandleBannerFailed", stale, "late callback");
            Assert.That((bool)Get(service, "bannerLoadInFlight"), Is.True);
            Loaded();
            Assert.That(visibility.Last(), Is.True);
        }

        [Test]
        public void RequestTimeoutCancelsNativeViewBeforeReplacementAndRejectsStaleSuccess()
        {
            Ready();
            int stale = requests[0];
            Tick(59f);
            Assert.That(requests.Count, Is.EqualTo(1));
            Tick(60f);
            Assert.That(destroyedViews, Is.EqualTo(1));
            Assert.That(requests.Count, Is.EqualTo(1));
            Tick(75f);
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[1], Is.Not.EqualTo(stale));
            Call(service, "HandleBannerLoaded", stale);
            Assert.That((bool)Get(service, "bannerLoaded"), Is.False);
            Loaded();
            Assert.That(visibility.Last(), Is.True);
        }

        [Test]
        public void SdkRefreshFailureKeepsExistingAdAndDoesNotStartSecondRefreshLoop()
        {
            Ready();
            Loaded();
            Failed();
            Tick(200f);
            Assert.That((bool)Get(service, "bannerLoaded"), Is.True);
            Assert.That((bool)Get(service, "bannerShown"), Is.True);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(destroyedViews, Is.Zero);
        }

        [Test]
        public void QueuedCallbackAfterDestructionCannotInitializeAds()
        {
            Call(service, "Start");
            consentCallback(null);
            Call(service, "OnDestroy");
            Pump();
            Assert.That(consentForms, Is.Zero);
            Assert.That(initializations, Is.Zero);
            Assert.That(requests, Is.Empty);
        }
    }
}
