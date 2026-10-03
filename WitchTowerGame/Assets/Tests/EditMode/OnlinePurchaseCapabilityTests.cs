using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    // No save/network/store entrypoints are called. The production path below
    // is only compared as a string through the SaveManager test override. These
    // legacy contexts have no durable purchase-realm authority; an authenticated
    // head cannot manufacture it, even when owner, epoch and native hint agree.
    public sealed class OnlinePurchaseCapabilityTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private const string Player = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string OtherPlayer = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const int Epoch = 3;
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly Dictionary<FieldInfo, object> previousSingletons = new Dictionary<FieldInfo, object>();
        private Component game, online, saves;
        private object profile, syntheticSave, previousOverride, previousMessage;
        private PropertyInfo saveOverride;
        private FieldInfo messageField;
        private bool overrideCaptured, messageCaptured, randomCaptured;
        private UnityEngine.Random.State previousRandom;

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower." + name, true);
        private static object Property(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static void SetProperty(object owner, string name, object value) =>
            owner.GetType().GetProperty(name).SetValue(owner, value);
        private static void SetPublic(object owner, string name, object value) =>
            owner.GetType().GetField(name).SetValue(owner, value);
        private static void SetHidden(object owner, string name, object value) =>
            owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static object Field(object owner, string name) => owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
        private static string Constant(string name) => (string)T("Monetization.IapPurchaseEnvironmentPolicy")
            .GetField(name).GetRawConstantValue();
        private static string ProductionRoot => Path.Combine(Application.persistentDataPath, "environments", "production");

        [SetUp]
        public void SetUp()
        {
            try
            {
                previousRandom = UnityEngine.Random.state;
                randomCaptured = true;
                saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden);
                previousOverride = saveOverride.GetValue(null);
                overrideCaptured = true;
                saveOverride.SetValue(null, ProductionRoot);
                messageField = T("Save.OnlinePlayerData").GetField("<LastMessage>k__BackingField", StaticHidden);
                previousMessage = messageField.GetValue(null);
                messageCaptured = true;

                saves = Manager("Managers.SaveManager");
                game = Manager("Managers.GameManager");
                online = Manager("Save.OnlinePlayerData");
                syntheticSave = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                SetPublic(syntheticSave, "PlayerId", Player);
                SetPublic(syntheticSave, "RecoveryEpoch", Epoch);
                profile = Activator.CreateInstance(T("Data.PlayerProfile"), syntheticSave);
                SetProperty(game, "PlayerProfile", profile);
                SetProperty(saves, "CurrentSaveData", syntheticSave);
                SetProperty(profile, "Gold", 12345);
                SetProperty(profile, "FreeGachaStones", 1800);
                SetProperty(profile, "PaidGachaStones", 500000);
                SetProperty(profile, "EconomyRevision", 11L);
                SetProperty(profile, "Level", 13);

                object monster = Activator.CreateInstance(T("Save.OwnedMonsterData"));
                SetPublic(monster, "InstanceId", "synthetic-capability-monster-instance");
                SetPublic(monster, "MonsterId", "synthetic-capability-monster");
                SetPublic(monster, "Level", 7);
                SetPublic(monster, "HasIndividualValues", true);
                SetPublic(monster, "IndividualHp", 37);
                ((IList)Property(profile, "OwnedMonsters")).Add(monster);

                object equipment = Activator.CreateInstance(T("Save.OwnedEquipmentData"));
                SetPublic(equipment, "InstanceId", "synthetic-capability-equipment-instance");
                SetPublic(equipment, "EquipmentId", "synthetic-capability-equipment");
                SetPublic(equipment, "HasRolledStats", true);
                SetPublic(equipment, "RolledAttack", 123);
                ((IList)Property(profile, "OwnedEquipments")).Add(equipment);
                ((IList)Property(profile, "ProcessedIapTransactionIds")).Add("synthetic-previous-delivery");
                InstallCapability();
            }
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
                    foreach (var previous in previousSingletons) previous.Key.SetValue(null, previous.Value);
                }
                finally
                {
                    previousSingletons.Clear();
                    try
                    {
                        if (overrideCaptured) saveOverride.SetValue(null, previousOverride);
                    }
                    finally
                    {
                        overrideCaptured = false;
                        try
                        {
                            if (messageCaptured) messageField.SetValue(null, previousMessage);
                        }
                        finally
                        {
                            messageCaptured = false;
                            if (randomCaptured) UnityEngine.Random.state = previousRandom;
                            randomCaptured = false;
                            game = online = saves = null;
                            profile = syntheticSave = null;
                        }
                    }
                }
            }
        }

        private Component Manager(string name)
        {
            Type type = T(name);
            var singleton = type.GetField("<Instance>k__BackingField", StaticHidden);
            previousSingletons.Add(singleton, singleton.GetValue(null));
            var owner = new GameObject("IsolatedOnlinePurchaseCapabilityTest");
            owner.SetActive(false);
            owners.Add(owner);
            Component component = owner.AddComponent(type);
            singleton.SetValue(null, component);
            return component;
        }

        private void InstallCapability(string owner = Player, int epoch = Epoch,
            string appleEnvironment = "Production", string dataRealm = "Production", bool ready = true)
        {
            object capability = Activator.CreateInstance(T("Monetization.PurchaseEnvironmentCapability"));
            SetPublic(capability, "Version", 1);
            SetPublic(capability, "Store", "apple");
            SetPublic(capability, "AppleEnvironment", appleEnvironment);
            SetPublic(capability, "DataRealm", dataRealm);
            SetPublic(capability, "Ready", ready);
            SetHidden(online, "purchaseCapability", capability);
            SetHidden(online, "purchaseCapabilityPlayer", owner);
            SetHidden(online, "purchaseCapabilityEpoch", epoch);
            SetHidden(online, "purchaseReady", true);
        }

        private static string Message(string environment = "Production") => (string)T("Save.OnlinePlayerData")
            .GetMethod("CheckoutEnvironmentBlockedMessage", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { Enum.Parse(T("Monetization.VerifiedApplePurchaseEnvironment"), environment) });

        private void AssertCapabilityCleared()
        {
            Assert.That(Field(online, "purchaseCapability"), Is.Null);
            Assert.That(Field(online, "purchaseCapabilityPlayer"), Is.Null);
            Assert.That(Field(online, "purchaseCapabilityEpoch"), Is.EqualTo(0));
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        // Snapshot only memory state. Do not call ToSaveData: that normalizes the
        // inventory and is intentionally outside this checkout-policy fixture.
        private Action AssertProfileUnchangedAfter()
        {
            object capturedProfile = profile;
            string savedMemoryJson = JsonUtility.ToJson(syntheticSave);
            string[] names = { "PlayerId", "RecoveryEpoch", "EconomyRevision", "Level", "Gold",
                "FreeGachaStones", "PaidGachaStones", "HasRemovedAds", "TutorialStepId", "HasCompletedTutorial" };
            object[] values = names.Select(name => Property(profile, name)).ToArray();
            string[] collections = { "OwnedMonsters", "OwnedEquipments", "ProcessedIapTransactionIds", "PartyMonsterInstanceIds" };
            object[] references = collections.Select(name => Property(profile, name)).ToArray();
            int[] counts = references.Select(value => ((IList)value).Count).ToArray();
            string[] inventoryJson = ((IList)references[0]).Cast<object>().Concat(((IList)references[1]).Cast<object>())
                .Select(value => JsonUtility.ToJson(value)).ToArray();
            object[] processed = ((IList)references[2]).Cast<object>().ToArray();
            object[] party = ((IList)references[3]).Cast<object>().ToArray();
            return () =>
            {
                Assert.That(Property(game, "PlayerProfile"), Is.SameAs(capturedProfile));
                Assert.That(Property(saves, "CurrentSaveData"), Is.SameAs(syntheticSave));
                Assert.That(JsonUtility.ToJson(syntheticSave), Is.EqualTo(savedMemoryJson));
                for (int i = 0; i < names.Length; i++) Assert.That(Property(profile, names[i]), Is.EqualTo(values[i]), names[i]);
                for (int i = 0; i < collections.Length; i++)
                {
                    Assert.That(Property(profile, collections[i]), Is.SameAs(references[i]), collections[i]);
                    Assert.That(((IList)references[i]).Count, Is.EqualTo(counts[i]), collections[i]);
                }
                Assert.That(((IList)references[0]).Cast<object>().Concat(((IList)references[1]).Cast<object>())
                    .Select(value => JsonUtility.ToJson(value)).ToArray(), Is.EqualTo(inventoryJson));
                Assert.That(((IList)references[2]).Cast<object>().ToArray(), Is.EqualTo(processed));
                Assert.That(((IList)references[3]).Cast<object>().ToArray(), Is.EqualTo(party));
            };
        }

        [TestCase("Production")]
        [TestCase("Sandbox")]
        public void BoundReleaseRootRequiresTheSameNativeEnvironmentAndFreshAuthenticatedHead(string environment)
        {
            string isolated = Path.Combine(Path.GetTempPath(), "WitchTower-ReleaseHead-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolated);
            try
            {
                var native = Enum.Parse(T("Monetization.VerifiedApplePurchaseEnvironment"), environment);
                var context = T("Save.StorageStartupContext").GetMethod("BindReleaseEnvironment", StaticHidden)
                    .Invoke(null, new object[] { isolated,
                        "{\"BaseUrl\":\"https://api.nasus-games.com\",\"ReleaseAppleLinking\":true}", native });
                saveOverride.SetValue(null, isolated);
                SetHidden(saves, "capturedEditorRootOverride", isolated);
                SetHidden(saves, "storageContext", context);
                InstallCapability(appleEnvironment: environment, dataRealm: environment);
                Assert.That(Message(environment), Is.Empty);
                Assert.That(Message(environment == "Production" ? "Sandbox" : "Production"), Is.Not.Empty);
                Call(online, "ConfigureFromStorageContext");
                Assert.That(Field(online, "baseUrl"), Is.EqualTo(environment == "Production"
                    ? "https://api.nasus-games.com" : "https://api.nasus-games.com/review-sandbox"));
                InstallCapability(appleEnvironment: environment, dataRealm: environment, ready: false);
                Assert.That(Message(environment), Is.Not.Empty);
                Assert.That(Property(saves, "StorageAccessAvailable"), Is.True, "Unavailable checkout must not block local gameplay.");
                InstallCapability(appleEnvironment: environment, dataRealm: environment, epoch: Epoch + 1);
                Assert.That(Message(environment), Is.Not.Empty);
                InstallCapability(appleEnvironment: environment, dataRealm: environment, owner: OtherPlayer);
                Assert.That(Message(environment), Is.Not.Empty);
                Assert.That(saves.GetType().GetProperty("PurchaseEnvironment", Hidden).GetValue(saves), Is.EqualTo(environment));
            }
            finally { Directory.Delete(isolated, true); }
        }

        [Test]
        public void LegacyProductionRootAndMatchingAuthenticatedHeadCannotAcquirePurchaseRealmAuthority()
        {
            Action unchanged = AssertProfileUnchangedAfter();
            Assert.That(Property(saves, "RootDirectory"), Is.EqualTo(ProductionRoot));
            Assert.That(Property(saves, "StorageAccessAvailable"), Is.True);
            Assert.That(saves.GetType().GetProperty("HasPurchaseRealmAuthority", Hidden).GetValue(saves), Is.False);
            Assert.That(Field(online, "purchaseCapabilityPlayer"), Is.EqualTo(Player));
            Assert.That(Field(online, "purchaseCapabilityEpoch"), Is.EqualTo(Epoch));
            Assert.That(Field(online, "purchaseReady"), Is.True);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
            unchanged();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(OtherPlayer)]
        public void CapabilityForDifferentOrAbsentAuthenticatedOwnerIsRejected(string capabilityPlayer)
        {
            InstallCapability(owner: capabilityPlayer);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void CapabilityForDifferentRecoveryEpochIsRejected(int capabilityEpoch)
        {
            InstallCapability(epoch: capabilityEpoch);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [Test]
        public void SwitchingCurrentProfileCannotReusePreviousAccountsCapability()
        {
            SetProperty(profile, "PlayerId", OtherPlayer);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("not-a-player-id")]
        [TestCase("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
        public void MatchingButInvalidLiveAndCachedOwnerCannotAuthorizeCheckout(string invalidOwner)
        {
            SetProperty(profile, "PlayerId", invalidOwner);
            InstallCapability(owner: invalidOwner);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [Test]
        public void IncrementingCurrentRecoveryEpochCannotReusePreviousEpochsCapability()
        {
            SetProperty(profile, "RecoveryEpoch", Epoch + 1);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase("refund-sandbox")]
        [TestCase("environments/sandbox")]
        [TestCase("")]
        public void ProductionCapabilityDoesNotAuthorizeQaSandboxOrSharedDataRoot(string relativeRoot)
        {
            // Select this fixture's initial legacy root before first context
            // capture, rather than mutating an already-running manager's root.
            string expectedRoot = string.IsNullOrEmpty(relativeRoot)
                ? Application.persistentDataPath : Path.Combine(Application.persistentDataPath, relativeRoot);
            saveOverride.SetValue(null, expectedRoot);
            Assert.That(Property(saves, "RootDirectory"), Is.EqualTo(expectedRoot));
            Assert.That(Property(saves, "StorageAccessAvailable"), Is.True);
            Assert.That(saves.GetType().GetProperty("HasPurchaseRealmAuthority", Hidden).GetValue(saves), Is.False);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase("Managers.SaveManager")]
        [TestCase("Managers.GameManager")]
        [TestCase("Save.OnlinePlayerData")]
        public void AbsentRequiredManagerFailsClosedWithoutLoadingAnything(string manager)
        {
            T(manager).GetField("<Instance>k__BackingField", StaticHidden).SetValue(null, null);
            Assert.That(Message(), Is.Not.Empty);
        }

        [Test]
        public void AbsentLiveProfileCannotAcquireCapabilityFromAuthenticatedHeadAlone()
        {
            SetProperty(game, "PlayerProfile", null);
            Assert.That(Message(), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase("Unknown", "Unknown", false)]
        [TestCase("Production", "Production", false)]
        [TestCase("Unknown", "Production", true)]
        [TestCase("Production", "Unknown", true)]
        [TestCase("Sandbox", "Sandbox", true)]
        public void InactiveOrWrongRealmAuthenticatedHeadCannotAuthorizeCheckout(
            string environment, string dataRealm, bool ready)
        {
            InstallCapability(appleEnvironment: environment, dataRealm: dataRealm, ready: ready);
            Assert.That(Message(), Is.Not.Empty);
        }

        [TestCase("Unknown")]
        [TestCase("Unsupported")]
        [TestCase("Sandbox")]
        public void AuthenticatedProductionHeadCannotReplaceProductionNativeVerification(string environment)
        {
            string expected = environment == "Sandbox" ? "PurchaseCapabilityUnavailableMessage" : "VerifiedEnvironmentUnavailableMessage";
            Assert.That(Message(environment), Is.EqualTo(Constant(expected)));
        }

        [Test]
        public void ExplicitInvalidationClearsOnlyCachedCapabilityAndPreservesPlayerData()
        {
            Action unchanged = AssertProfileUnchangedAfter();
            Call(online, "InvalidatePurchaseCapability");
            AssertCapabilityCleared();
            unchanged();
        }

        [Test]
        public void SyncFailureRevokesAuthenticatedCapabilityWithoutChangingWalletInventoryOrProfile()
        {
            Action unchanged = AssertProfileUnchangedAfter();
            Call(online, "CompleteSyncStatus", "synthetic-capability-sync-failure", false);
            AssertCapabilityCleared();
            Assert.That(Field(online, "purchaseReady"), Is.False);
            unchanged();
        }

        [Test]
        public void LaterSuccessStatusDoesNotRestoreCapabilityWithoutFreshAuthenticatedHead()
        {
            Action unchanged = AssertProfileUnchangedAfter();
            Call(online, "CompleteSyncStatus", "synthetic-capability-sync-failure", false);
            Call(online, "CompleteSyncStatus", null, false);
            AssertCapabilityCleared();
            Assert.That(Field(online, "purchaseReady"), Is.False);
            unchanged();
        }
    }
}
