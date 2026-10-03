using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class ApplePurchaseEnvironmentProbeTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private const string Request = "synthetic-environment-probe-request";
        private const string SensitiveMarker = "SYNTHETIC_PRIVATE_RECEIPT_TOKEN_TRANSACTION";
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly Dictionary<Type, object> singletons = new Dictionary<Type, object>();
        private readonly List<string> logs = new List<string>();
        private Component probe;
        private object previousNativeOverride;
        private Application.LogCallback logCapture;

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object value, string name) => value.GetType().GetField(name, Hidden).GetValue(value);
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, Hidden).SetValue(value, data);
        private static object Call(object value, string name, params object[] args) => value.GetType()
            .GetMethod(name, Hidden | BindingFlags.Public).Invoke(value, args);
        private static object Instance(Type type) => type.GetField("<Instance>k__BackingField", StaticHidden).GetValue(null);
        private static void SetInstance(Type type, object value) => type.GetField("<Instance>k__BackingField", StaticHidden).SetValue(null, value);
        private string Verdict => probe.GetType().GetProperty("Environment").GetValue(probe).ToString();

        [Serializable]
        private sealed class Payload
        {
            public string RequestId;
            public string Environment;
            public string Reason;
        }

        [SetUp]
        public void SetUp()
        {
            probe = null;
            previousNativeOverride = T("Monetization.ApplePurchaseEnvironmentProbe").GetField("EditorNativeRequestOverride", StaticHidden).GetValue(null);
            T("Monetization.ApplePurchaseEnvironmentProbe").GetField("EditorNativeRequestOverride", StaticHidden).SetValue(null, null);
            logs.Clear();
            logCapture = (message, trace, type) => { logs.Add(message); logs.Add(trace); };
            Application.logMessageReceived += logCapture;
            try { probe = Manager("Monetization.ApplePurchaseEnvironmentProbe"); }
            catch { Cleanup(); throw; }
        }

        [TearDown]
        public void TearDown() => Cleanup();

        private void Cleanup()
        {
            try
            {
                foreach (GameObject owner in owners)
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            }
            finally
            {
                owners.Clear();
                try
                {
                    foreach (var entry in singletons) SetInstance(entry.Key, entry.Value);
                }
                finally
                {
                    singletons.Clear();
                    T("Monetization.ApplePurchaseEnvironmentProbe").GetField("EditorNativeRequestOverride", StaticHidden).SetValue(null, previousNativeOverride);
                    if (logCapture != null) Application.logMessageReceived -= logCapture;
                    logCapture = null;
                    probe = null;
                }
            }
        }

        private Component Manager(string name)
        {
            Type type = T(name);
            if (!singletons.ContainsKey(type)) singletons.Add(type, Instance(type));
            var owner = new GameObject("IsolatedAppleEnvironmentProbeTest");
            owner.SetActive(false); // No Awake, Update, store, HTTP, save or native startup.
            owners.Add(owner);
            Component component = owner.AddComponent(type);
            SetInstance(type, component);
            return component;
        }

        private void Seed(string environment = "Unknown", bool expired = false)
        {
            Set(probe, "<Environment>k__BackingField", Enum.Parse(T("Monetization.VerifiedApplePurchaseEnvironment"), environment));
            Set(probe, "requestId", Request);
            Set(probe, "expiresAt", Time.realtimeSinceStartup + (expired ? -1f : 3600f));
            Set(probe, "retryAt", float.PositiveInfinity);
        }

        private void Receive(string environment, string request = Request) => Call(probe, "OnApplePurchaseEnvironment",
            JsonUtility.ToJson(new Payload { RequestId = request, Environment = environment }));

        private void AssertNoSensitiveOutput()
        {
            Assert.That(string.Join("\n", logs), Does.Not.Contain(SensitiveMarker));
            Assert.That(new[] { "Unknown", "Production", "Sandbox", "Unsupported" }, Does.Contain(Verdict));
            Assert.That(Verdict, Does.Not.Contain(SensitiveMarker));
        }

        private void AssertUnknownWithDelayedRetry(float before)
        {
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "requestId"), Is.Null);
            float retry = (float)Field(probe, "retryAt");
            Assert.That(retry, Is.GreaterThanOrEqualTo(before + 30f));
            Assert.That(retry, Is.LessThanOrEqualTo(Time.realtimeSinceStartup + 30f));
            AssertNoSensitiveOutput();
        }

        [TestCase("Production")]
        [TestCase("Sandbox")]
        [TestCase("Unsupported")]
        [TestCase("Unknown")]
        public void MatchingUnexpiredRequestAcceptsOnlyTheFixedVerdict(string environment)
        {
            Seed();
            float before = Time.realtimeSinceStartup;
            Receive(environment);
            Assert.That(Verdict, Is.EqualTo(environment));
            Assert.That(Field(probe, "requestId"), Is.Null);
            if (environment == "Unknown") AssertUnknownWithDelayedRetry(before);
            else Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            AssertNoSensitiveOutput();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("production")]
        [TestCase("sandbox")]
        [TestCase(" Production ")]
        [TestCase(SensitiveMarker)]
        public void UnrecognizedEnvironmentIsUnknownWithoutEchoingPayload(string environment)
        {
            Seed("Production");
            float before = Time.realtimeSinceStartup;
            Receive(environment);
            AssertUnknownWithDelayedRetry(before);
        }

        [Test]
        public void MissingEnvironmentIsUnknownRatherThanRetainingAnEarlierVerdict()
        {
            Seed("Sandbox");
            float before = Time.realtimeSinceStartup;
            Call(probe, "OnApplePurchaseEnvironment", "{\"RequestId\":\"" + Request + "\"}");
            AssertUnknownWithDelayedRetry(before);
        }

        [TestCase("{}")]
        [TestCase("{\"Environment\":\"Production\"}")]
        [TestCase("{\"RequestId\":null,\"Environment\":\"Production\"}")]
        [TestCase(null)]
        [TestCase("")]
        public void MissingRequestOrEmptyPayloadCannotCompleteTheActiveProbe(string payload)
        {
            Seed();
            float expires = (float)Field(probe, "expiresAt");
            Call(probe, "OnApplePurchaseEnvironment", payload);
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "requestId"), Is.EqualTo(Request));
            Assert.That(Field(probe, "expiresAt"), Is.EqualTo(expires));
            Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void MalformedPayloadFailsClosedAndDoesNotExposeParserInput()
        {
            Seed("Production");
            float before = Time.realtimeSinceStartup;
            Call(probe, "OnApplePurchaseEnvironment", SensitiveMarker);
            AssertUnknownWithDelayedRetry(before);
        }

        [TestCase("Production")]
        [TestCase("Sandbox")]
        [TestCase("Unsupported")]
        [TestCase("Unknown")]
        public void CallbackForAnotherRequestCannotReplaceOrCompleteCurrentProbe(string environment)
        {
            Seed();
            float expires = (float)Field(probe, "expiresAt");
            Receive(environment, "older-request-" + SensitiveMarker);
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "requestId"), Is.EqualTo(Request));
            Assert.That(Field(probe, "expiresAt"), Is.EqualTo(expires));
            Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            AssertNoSensitiveOutput();
        }

        [TestCase("Production")]
        [TestCase("Sandbox")]
        public void LateMatchingCallbackCannotSupplyAVerdictBeforeTimeoutUpdate(string environment)
        {
            Seed(expired: true);
            Receive(environment);
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "requestId"), Is.EqualTo(Request));
            float before = Time.realtimeSinceStartup;
            Call(probe, "Update");
            AssertUnknownWithDelayedRetry(before);
        }

        [TestCase("{\"RequestId\":\"synthetic-environment-probe-request\",\"Environment\":\"Production\"}")]
        [TestCase(SensitiveMarker)]
        [TestCase(null)]
        public void CallbackWithoutAnActiveRequestIsIgnoredIncludingMalformedInput(string payload)
        {
            Seed("Unsupported");
            Set(probe, "requestId", null);
            Call(probe, "OnApplePurchaseEnvironment", payload);
            Assert.That(Verdict, Is.EqualTo("Unsupported"));
            Assert.That(Field(probe, "requestId"), Is.Null);
            Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void TimeoutRevokesVerdictSchedulesRetryAndIgnoresSubsequentCallback()
        {
            Seed("Production", expired: true);
            float before = Time.realtimeSinceStartup;
            Call(probe, "Update");
            AssertUnknownWithDelayedRetry(before);
            float retry = (float)Field(probe, "retryAt");
            Receive("Production");
            Receive(SensitiveMarker);
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "requestId"), Is.Null);
            Assert.That(Field(probe, "retryAt"), Is.EqualTo(retry));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void EditorRefreshIsUnsupportedAndDoesNotChangeSaveOrEndpoint()
        {
            Component saves = Manager("Managers.SaveManager");
            Component online = Manager("Save.OnlinePlayerData");
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("PlayerId").SetValue(save, "synthetic-player-" + SensitiveMarker);
            save.GetType().GetField("PaidGachaStones").SetValue(save, 500000);
            Set(saves, "<CurrentSaveData>k__BackingField", save);
            const string endpoint = "https://synthetic.invalid/no-network";
            Set(online, "baseUrl", endpoint);
            string original = JsonUtility.ToJson(save);
            object originalGame = Instance(T("Managers.GameManager"));
            object originalStore = Instance(T("Monetization.InAppPurchaseService"));
            Seed("Production");
            float before = Time.realtimeSinceStartup;
            Set(probe, "requestId", null);
            Call(probe, "Refresh");
            Assert.That(Verdict, Is.EqualTo("Unsupported"));
            Assert.That(Field(probe, "requestId"), Is.Null);
            Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            Assert.That((float)Field(probe, "expiresAt"), Is.GreaterThanOrEqualTo(before + 15f));
            Assert.That(Field(saves, "<CurrentSaveData>k__BackingField"), Is.SameAs(save));
            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(original));
            Assert.That(Field(online, "baseUrl"), Is.EqualTo(endpoint));
            Assert.That(Instance(saves.GetType()), Is.SameAs(saves));
            Assert.That(Instance(online.GetType()), Is.SameAs(online));
            Assert.That(Instance(T("Managers.GameManager")), Is.SameAs(originalGame));
            Assert.That(Instance(T("Monetization.InAppPurchaseService")), Is.SameAs(originalStore));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void RetryWaitsUntilDueThenRefreshesWithoutAcceptingAnOlderCallback()
        {
            Seed(expired: true);
            Call(probe, "Update");
            float retry = (float)Field(probe, "retryAt");
            Call(probe, "Update");
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Assert.That(Field(probe, "retryAt"), Is.EqualTo(retry));
            Set(probe, "retryAt", Time.realtimeSinceStartup - 1f);
            Call(probe, "Update");
            Assert.That(Verdict, Is.EqualTo("Unsupported"));
            Assert.That(Field(probe, "requestId"), Is.Null);
            Receive("Production");
            Assert.That(Verdict, Is.EqualTo("Unsupported"));
            Assert.That((float)Field(probe, "retryAt"), Is.EqualTo(float.PositiveInfinity));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void DestroyClearsItsPendingRequestAndItsOwnSingleton()
        {
            Seed();
            Call(probe, "OnDestroy");
            Assert.That(Field(probe, "requestId"), Is.Null);
            Assert.That(Instance(probe.GetType()), Is.Null);
            Receive("Production");
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void DestroyingAnOlderProbeDoesNotClearTheReplacementSingleton()
        {
            Seed();
            Component replacement = Manager("Monetization.ApplePurchaseEnvironmentProbe");
            Call(probe, "OnDestroy");
            Assert.That(Field(probe, "requestId"), Is.Null);
            Assert.That(Instance(probe.GetType()), Is.SameAs(replacement));
            AssertNoSensitiveOutput();
        }

        private void NativeOverride(Action<string, bool> callback) => T("Monetization.ApplePurchaseEnvironmentProbe")
            .GetField("EditorNativeRequestOverride", StaticHidden).SetValue(null, callback);

        [TestCase(false)]
        [TestCase(true)]
        public void ForegroundRefreshKeepsAnInFlightRequestAndItsDeadline(bool userAction)
        {
            int calls = 0;
            bool interactive = false;
            NativeOverride((request, user) => { calls++; interactive = user; });
            Call(probe, userAction ? "RetryFromUserAction" : "Refresh");
            string request = (string)Field(probe, "requestId");
            float deadline = (float)Field(probe, "expiresAt");
            Call(probe, "Refresh"); // IAP foreground callback.
            Call(probe, "RetryFromUserAction"); // Repeated tap cannot duplicate a prompt.
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(interactive, Is.EqualTo(userAction));
            Assert.That(Field(probe, "requestId"), Is.EqualTo(request));
            Assert.That(Field(probe, "expiresAt"), Is.EqualTo(deadline));
            Receive("Sandbox", request);
            Assert.That(Verdict, Is.EqualTo("Sandbox"));
        }

        [Test]
        public void SharedFailureCanRecoverByExplicitRefreshWithoutTouchingExistingSave()
        {
            Component saves = Manager("Managers.SaveManager");
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("PlayerId").SetValue(save, "existing-production-owner");
            save.GetType().GetField("PaidGachaStones").SetValue(save, 900);
            Set(saves, "<CurrentSaveData>k__BackingField", save);
            string before = JsonUtility.ToJson(save);
            var modes = new List<bool>();
            NativeOverride((request, user) => modes.Add(user));
            Call(probe, "Refresh");
            string first = (string)Field(probe, "requestId");
            Call(probe, "OnApplePurchaseEnvironment", JsonUtility.ToJson(new Payload {
                RequestId = first, Environment = "Unknown", Reason = "Unverified" }));
            Assert.That(probe.GetType().GetProperty("DiagnosticCode").GetValue(probe), Is.EqualTo("Unverified"));
            Call(probe, "RetryFromUserAction");
            string second = (string)Field(probe, "requestId");
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That((float)Field(probe, "expiresAt"), Is.GreaterThan(Time.realtimeSinceStartup + 170f));
            Receive("Production", first); // Stale verified callback must not bind a realm.
            Assert.That(Verdict, Is.EqualTo("Unknown"));
            Receive("Sandbox", second);
            Assert.That(Verdict, Is.EqualTo("Sandbox"));
            Assert.That(modes, Is.EqualTo(new[] { false, true }));
            Assert.That(Field(saves, "<CurrentSaveData>k__BackingField"), Is.SameAs(save));
            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(before));
        }

        [Test]
        public void InteractiveTimeoutRetriesSharedWithoutAutomaticallyPromptingAgain()
        {
            var modes = new List<bool>();
            NativeOverride((request, user) => modes.Add(user));
            Call(probe, "RetryFromUserAction");
            Set(probe, "expiresAt", Time.realtimeSinceStartup - 1f);
            Call(probe, "Update");
            Assert.That(probe.GetType().GetProperty("DiagnosticCode").GetValue(probe), Is.EqualTo("Timeout"));
            Set(probe, "retryAt", Time.realtimeSinceStartup - 1f);
            Call(probe, "Update");
            Assert.That(modes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void DiagnosticPayloadIsAllowlistedAndDoesNotExposeNativeDetails()
        {
            Seed();
            Call(probe, "OnApplePurchaseEnvironment", JsonUtility.ToJson(new Payload {
                RequestId = Request, Environment = "Unknown", Reason = SensitiveMarker }));
            Assert.That(probe.GetType().GetProperty("DiagnosticCode").GetValue(probe), Is.EqualTo("Unknown"));
            Assert.That(probe.GetType().GetProperty("StatusMessage").GetValue(probe).ToString(), Does.Not.Contain(SensitiveMarker));
            AssertNoSensitiveOutput();
        }

        [Test]
        public void FixtureCleanupDestroysOnlyItsOwnerAndRestoresThePreviousSingleton()
        {
            Type type = probe.GetType();
            object previous = singletons[type];
            GameObject owned = probe.gameObject;
            Cleanup();
            Assert.That(owned == null, Is.True);
            Assert.That(Instance(type), Is.SameAs(previous));
            Assert.That(owners, Is.Empty);
            Assert.That(singletons, Is.Empty);
        }
    }
}
