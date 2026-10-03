using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    // No PlayMode, transport, native StoreKit or real save entrypoints. Every
    // filesystem fixture is test-owned and created with POSIX private modes;
    // hooks, singleton references, Random and the one prefs key are restored.
    public sealed class StorageStartupContextTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private const string PresentationKey = "witchtower_pending_ten_pull_presentation_v1";
        private const string Product = "com.nasus.dungeonmonsterroguelike.crystals120";
        private const string Configuration = "{\"BaseUrl\":\"https://synthetic.invalid/no-network\"}";
        private const uint DirectoryMode = 448; // 0700
        private const uint FileModePrivate = 384; // 0600
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly Dictionary<FieldInfo, object> previousFields = new Dictionary<FieldInfo, object>();
        private string root;
        private bool rootCreated, overrideCaptured, randomCaptured;
        private PropertyInfo directoryOverride;
        private object previousOverride;
        private UnityEngine.Random.State previousRandom;
        private Component saves, game;
        private object syntheticSave, profile;

        [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
        private static extern int MakePrivateDirectory(string path, uint mode);
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int SetPrivateMode(string path, uint mode);

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower." + name, true);
        private static object P(object value, string name) => value.GetType().GetProperty(name, AnyInstance).GetValue(value);
        private static void SetProperty(object value, string name, object data) => value.GetType().GetProperty(name, AnyInstance).SetValue(value, data);
        private static object F(object value, string name) => value.GetType().GetField(name, AnyInstance).GetValue(value);
        private static void SetField(object value, string name, object data) => value.GetType().GetField(name, AnyInstance).SetValue(value, data);
        private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, AnyInstance).Invoke(value, args);
        private static object Context(string factory, params object[] args) => T("Save.StorageStartupContext")
            .GetMethod(factory, StaticHidden).Invoke(null, args);
        private static object NewSave() => T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static string Constant(string field) => (string)T("Save.StorageStartupContext").GetField(field, StaticHidden).GetRawConstantValue();
        private static FieldInfo Singleton(Type type) => type.GetField("<Instance>k__BackingField", StaticHidden);
        private static object StaticProperty(string type, string name) => T(type).GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        private static object StaticCall(string type, string method, params object[] args) => T(type)
            .GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.LinuxEditor)
                Assert.Ignore("Private POSIX fixture modes are required; no weaker filesystem fallback is provided.");
            try
            {
                previousRandom = UnityEngine.Random.state;
                randomCaptured = true;
                root = Path.Combine(Path.GetTempPath(), "WitchTower-StartupContext-" + Guid.NewGuid().ToString("N"));
                Assert.That(MakePrivateDirectory(root, DirectoryMode), Is.Zero, "Cannot create the private test fixture.");
                rootCreated = true;
                directoryOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden);
                previousOverride = directoryOverride.GetValue(null);
                overrideCaptured = true;
                directoryOverride.SetValue(null, root);
                CaptureAndSet(T("Managers.SaveManager").GetField("EditorNamespaceBindingRequiredOverride", StaticHidden), false);
                foreach (string type in new[] { "Managers.SaveManager", "Managers.GameManager", "Managers.MasterDataManager", "Save.OnlinePlayerData", "Monetization.InAppPurchaseService" })
                    CaptureAndSet(Singleton(T(type)), null);
                CaptureAndSet(T("Save.OnlinePlayerData").GetField("<LastMessage>k__BackingField", StaticHidden), string.Empty);
                CaptureAndSet(T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", StaticHidden), null);
                CaptureAndSet(T("Save.OnlinePlayerData").GetField("EditorAppleAccountEnabled", StaticHidden), false);
                CaptureAndSet(T("Save.OnlinePlayerData").GetField("EditorAppleAccountOverride", StaticHidden), null);
                foreach (string name in new[] { "ProductsUpdated", "PurchaseSucceeded", "PurchaseFailed" })
                    CaptureAndSet(T("Monetization.InAppPurchaseService").GetField(name, StaticHidden), null);
                saves = Manager("Managers.SaveManager");
                game = Manager("Managers.GameManager");
                syntheticSave = NewSave();
                SetField(syntheticSave, "PlayerId", new string('a', 32));
                SetField(syntheticSave, "Gold", 321);
                SetField(syntheticSave, "PaidGachaStones", 120);
                SetField(syntheticSave, "LastActiveAt", "synthetic-last-active-must-not-change");
                profile = Activator.CreateInstance(T("Data.PlayerProfile"), syntheticSave);
                SetProperty(saves, "CurrentSaveData", syntheticSave);
                SetProperty(game, "PlayerProfile", profile);
                SetProperty(game, "CurrentFloor", 7);
            }
            catch { Cleanup(); throw; }
        }

        [TearDown] public void TearDown() => Cleanup();

        private void CaptureAndSet(FieldInfo field, object value)
        {
            Assert.That(field, Is.Not.Null);
            if (!previousFields.ContainsKey(field)) previousFields.Add(field, field.GetValue(null));
            field.SetValue(null, value);
        }

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
                    foreach (var previous in previousFields) previous.Key.SetValue(null, previous.Value);
                }
                finally
                {
                    previousFields.Clear();
                    try { if (overrideCaptured) directoryOverride.SetValue(null, previousOverride); }
                    finally
                    {
                        overrideCaptured = false;
                        try { if (randomCaptured) UnityEngine.Random.state = previousRandom; }
                        finally
                        {
                            randomCaptured = false;
                            if (rootCreated && Directory.Exists(root)) Directory.Delete(root, true);
                            rootCreated = false;
                            root = null;
                        }
                    }
                }
            }
        }

        private Component Manager(string name)
        {
            var owner = new GameObject("IsolatedStorageStartupContextTest");
            owner.SetActive(false);
            owners.Add(owner);
            Component component = owner.AddComponent(T(name));
            Singleton(T(name)).SetValue(null, component);
            return component;
        }

        private void BindingRequired() => T("Managers.SaveManager")
            .GetField("EditorNamespaceBindingRequiredOverride", StaticHidden).SetValue(null, true);

        private void PrivateDirectory(string relative)
        {
            string path = Path.Combine(root, relative);
            Assert.That(MakePrivateDirectory(path, DirectoryMode), Is.Zero);
        }

        private void PrivateFile(string relative, string contents)
        {
            string path = Path.Combine(root, relative);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(contents);
            Assert.That(SetPrivateMode(path, FileModePrivate), Is.Zero);
        }

        private void PoisonAccountFiles()
        {
            foreach (string name in new[] { "save.json", "online-identity.json", "online-request.json", "active-account.json", "apple-recovery.json", "account-deletion.json" })
                PrivateFile(name, "synthetic-existing-state-must-not-be-read:" + name);
            PrivateDirectory("iap-isolation-recovery");
            PrivateFile("iap-isolation-recovery/journal.json", "synthetic-isolated-journal-must-not-be-read");
        }

        private Action AssertTreeUnchangedAfter()
        {
            string[] beforeEntries = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var beforeFiles = beforeEntries.Where(File.Exists).ToDictionary(path => path, File.ReadAllBytes);
            return () =>
            {
                Assert.That(Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray(), Is.EqualTo(beforeEntries));
                foreach (var item in beforeFiles) CollectionAssert.AreEqual(item.Value, File.ReadAllBytes(item.Key));
            };
        }

        // Inspect memory only. ToSaveData normalizes inventory and is deliberately
        // not used as a snapshot oracle for blocked lifecycle entrypoints.
        private Action AssertMemoryUnchangedAfter()
        {
            object beforeCurrent = P(saves, "CurrentSaveData"), beforeProfile = P(game, "PlayerProfile");
            string currentJson = beforeCurrent == null ? null : JsonUtility.ToJson(beforeCurrent);
            string[] scalarNames = { "PlayerId", "Gold", "LastActiveAt", "FreeGachaStones", "PaidGachaStones", "RecoveryEpoch", "EconomyRevision" };
            object[] scalars = beforeProfile == null ? null : scalarNames.Select(name => P(beforeProfile, name)).ToArray();
            string[] collections = { "OwnedMonsters", "OwnedEquipments", "ProcessedIapTransactionIds", "PartyMonsterInstanceIds" };
            object[] references = beforeProfile == null ? null : collections.Select(name => P(beforeProfile, name)).ToArray();
            object[][] items = references?.Select(value => ((IList)value).Cast<object>().ToArray()).ToArray();
            int floor = (int)P(game, "CurrentFloor");
            bool recovery = (bool)P(saves, "RecoveryRequired");
            object recoveryMessage = P(saves, "RecoveryMessage");
            object online = Singleton(T("Save.OnlinePlayerData")).GetValue(null);
            return () =>
            {
                Assert.That(P(saves, "CurrentSaveData"), Is.SameAs(beforeCurrent));
                if (beforeCurrent != null) Assert.That(JsonUtility.ToJson(beforeCurrent), Is.EqualTo(currentJson));
                Assert.That(P(game, "PlayerProfile"), Is.SameAs(beforeProfile));
                if (beforeProfile != null)
                {
                    for (int i = 0; i < scalarNames.Length; i++) Assert.That(P(beforeProfile, scalarNames[i]), Is.EqualTo(scalars[i]), scalarNames[i]);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        Assert.That(P(beforeProfile, collections[i]), Is.SameAs(references[i]), collections[i]);
                        Assert.That(((IList)references[i]).Cast<object>().ToArray(), Is.EqualTo(items[i]), collections[i]);
                    }
                }
                Assert.That(P(game, "CurrentFloor"), Is.EqualTo(floor));
                Assert.That(P(saves, "RecoveryRequired"), Is.EqualTo(recovery));
                Assert.That(P(saves, "RecoveryMessage"), Is.EqualTo(recoveryMessage));
                Assert.That(Singleton(T("Save.OnlinePlayerData")).GetValue(null), Is.SameAs(online));
            };
        }

        private static Delegate Completion(Action<object, string> completed)
        {
            Type operation = T("Save.OnlineOperation");
            var op = Expression.Parameter(operation);
            var error = Expression.Parameter(typeof(string));
            return Expression.Lambda(typeof(Action<,>).MakeGenericType(operation, typeof(string)),
                Expression.Invoke(Expression.Constant(completed), Expression.Convert(op, typeof(object)), error), op, error).Compile();
        }

        private static object Request()
        {
            object request = Activator.CreateInstance(T("Save.OnlineRequest"));
            SetField(request, "Kind", "purchase");
            SetField(request, "Target", Product);
            SetField(request, "RequestId", "synthetic-startup-pending-request");
            SetField(request, "TransactionId", "synthetic-startup-pending-transaction");
            SetField(request, "Receipt", "synthetic-never-send");
            return request;
        }

        // Drive only the outer operation state machine. The yielded transport
        // enumerators are not executed: these replies are synthetic and cannot
        // register, grant or contact a configured service outside this fixture.
        private IEnumerator AppliedOperationWaitingForFinalSnapshot(Component online, Delegate completed)
        {
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            SetField(online, "configuredStorageOwner", saves);
            SetField(online, "baseUrl", "https://synthetic.invalid/no-network");
            object requested = Activator.CreateInstance(T("Save.OnlineRequest"));
            SetField(requested, "Kind", "reward");
            SetField(requested, "Target", "mission_clear_1");
            SetField(requested, "RequestId", "synthetic-final-snapshot-request");
            object head = Activator.CreateInstance(T("Save.OnlineHead"));
            SetField(head, "PlayerId", P(profile, "PlayerId"));
            SetField(head, "Epoch", P(profile, "RecoveryEpoch"));
            object operation = Activator.CreateInstance(T("Save.OnlineOperation"));
            SetField(operation, "RequestId", F(requested, "RequestId"));
            SetField(operation, "Kind", F(requested, "Kind"));
            SetField(operation, "Target", F(requested, "Target"));
            SetField(operation, "Revision", 1L);
            SetField(operation, "Free", 910);
            SetField(operation, "Paid", 120);
            SetField(operation, "GoldDelta", 7);
            IEnumerator routine = (IEnumerator)Call(online, "RunCore", requested, completed);
            try
            {
                foreach (string response in new[] { JsonUtility.ToJson(head), "{}", JsonUtility.ToJson(operation) })
                {
                    Assert.That(routine.MoveNext(), Is.True);
                    Assert.That(routine.Current, Is.InstanceOf<IEnumerator>());
                    SetField(online, "status", 200L);
                    SetField(online, "response", response);
                    SetField(online, "failure", null);
                }
                Assert.That(routine.MoveNext(), Is.True, "The applied operation must await its final snapshot.");
                Assert.That(routine.Current, Is.InstanceOf<IEnumerator>());
                Assert.That(P(P(game, "PlayerProfile"), "EconomyRevision"), Is.EqualTo(1L));
                Assert.That(P(P(game, "PlayerProfile"), "FreeGachaStones"), Is.EqualTo(910));
                Assert.That(P(P(game, "PlayerProfile"), "Gold"), Is.EqualTo(328));
                Assert.That(F(P(saves, "CurrentSaveData"), "EconomyRevision"), Is.EqualTo(1L));
                Assert.That(File.Exists(Path.Combine(root, "online-request.json")), Is.False);
                Assert.That(File.Exists(Path.Combine(root, "save.json")), Is.True);
                return routine;
            }
            catch { (routine as IDisposable)?.Dispose(); throw; }
        }

        private void AssertBlockedGetter(string name, string expectedMessage)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => P(saves, name));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message, Is.EqualTo(expectedMessage));
        }

        [TestCase(true)] [TestCase(false)]
        public void PureLegacySnapshotRetainsRootConfigurationAndBuildWithoutPurchaseAuthority(bool development)
        {
            string missing = Path.Combine(root, "pure-not-created");
            object context = Context("PreserveLegacyGameplay", missing, Configuration, development);
            Assert.That(P(context, "AccessAvailable"), Is.True);
            Assert.That(P(context, "RootDirectory"), Is.EqualTo(Path.GetFullPath(missing)));
            Assert.That(P(context, "ConfigurationJson"), Is.EqualTo(Configuration));
            Assert.That(P(context, "Development"), Is.EqualTo(development));
            Assert.That(P(context, "HasPurchaseRealmAuthority"), Is.False);
            Assert.That(P(context, "Message"), Is.Empty);
            Assert.That(Directory.Exists(missing), Is.False);
        }

        [TestCase(null)] [TestCase("")] [TestCase("{")] [TestCase("{\"Version\":99}")]
        public void BindingRequiredIsNotLegacyAbsenceAndCannotExposeARoot(string raw)
        {
            object context = Context("BindingRequired", raw, true);
            Assert.That(P(context, "AccessAvailable"), Is.False);
            Assert.That(P(context, "RootDirectory"), Is.Null);
            Assert.That(P(context, "ConfigurationJson"), Is.EqualTo(raw));
            Assert.That(P(context, "Message"), Is.EqualTo(Constant("BindingMessage")));
            Assert.That(P(context, "HasPurchaseRealmAuthority"), Is.False);
        }

        [TestCase(true)] [TestCase(false)]
        public void InvalidConfigurationIsDistinctFromBindingPendingAndNeverAnAvailableDefault(bool development)
        {
            object context = Context("InvalidConfiguration", development);
            Assert.That(P(context, "AccessAvailable"), Is.False);
            Assert.That(P(context, "RootDirectory"), Is.Null);
            Assert.That(P(context, "ConfigurationJson"), Is.Null);
            Assert.That(P(context, "Development"), Is.EqualTo(development));
            Assert.That(P(context, "Message"), Is.EqualTo(Constant("InvalidMessage")));
            Assert.That(P(context, "Message"), Is.Not.EqualTo(Constant("BindingMessage")));
            Assert.That(P(context, "HasPurchaseRealmAuthority"), Is.False);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase("relative-root")]
        public void LegacyFactoryRejectsMissingOrRelativeRootWithoutCreatingStorage(string invalidRoot)
        {
            Action unchanged = AssertTreeUnchangedAfter();
            var exception = Assert.Throws<TargetInvocationException>(() => Context("PreserveLegacyGameplay", invalidRoot, null, true));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            unchanged();
        }

        [Test]
        public void ContextPropertiesAreReadonlyAndNoFactoryCanClaimPurchaseRealmAuthority()
        {
            Type type = T("Save.StorageStartupContext");
            Assert.That(type.IsSealed, Is.True);
            foreach (string name in new[] { "AccessAvailable", "RootDirectory", "ConfigurationJson", "Development", "Message", "HasPurchaseRealmAuthority" })
                Assert.That(type.GetProperty(name, AnyInstance).GetSetMethod(true), Is.Null, name);
            foreach (object context in new[] { Context("PreserveLegacyGameplay", root, null, true),
                Context("BindingRequired", null, false), Context("InvalidConfiguration", true) })
                Assert.That(P(context, "HasPurchaseRealmAuthority"), Is.False);
        }

        [Test]
        public void SaveManagerCapturesLegacyOverrideLazilyAndRestorationOnlyReopensTheSameSnapshot()
        {
            string selected = Path.Combine(root, "selected-before-first-access");
            directoryOverride.SetValue(null, selected);
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            object context = F(saves, "storageContext");
            object raw = P(saves, "PlayerDataConfigurationJson");
            Assert.That(P(saves, "RootDirectory"), Is.EqualTo(selected));
            Assert.That(P(saves, "HasPurchaseRealmAuthority"), Is.False);
            directoryOverride.SetValue(null, root);
            Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
            AssertBlockedGetter("RootDirectory", Constant("ChangedMessage"));
            AssertBlockedGetter("DataDirectory", Constant("ChangedMessage"));
            Assert.That(ReferenceEquals(raw, P(saves, "PlayerDataConfigurationJson")), Is.True);
            directoryOverride.SetValue(null, selected);
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            Assert.That(F(saves, "storageContext"), Is.SameAs(context));
            Assert.That(P(saves, "RootDirectory"), Is.EqualTo(selected));
            Assert.That(P(saves, "DataDirectory"), Is.EqualTo(selected));
            Assert.That(Directory.Exists(selected), Is.False);
        }

        [Test]
        public void PendingRootAndDataGettersFailBeforeAnyAccountPointerOrDirectoryAccess()
        {
            BindingRequired();
            string missing = Path.Combine(root, "never-created-pending-root");
            directoryOverride.SetValue(null, missing);
            Action unchanged = AssertTreeUnchangedAfter();
            Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
            AssertBlockedGetter("RootDirectory", Constant("BindingMessage"));
            AssertBlockedGetter("DataDirectory", Constant("BindingMessage"));
            Assert.That(Directory.Exists(missing), Is.False);
            Assert.That(P(saves, "RecoveryRequired"), Is.False);
            unchanged();
        }

        [TestCase(false)] [TestCase(true)]
        public void PendingLoadSaveAndLifecyclePreserveLiveProfileFilesAndRecoveryState(bool existingRecovery)
        {
            BindingRequired();
            PoisonAccountFiles();
            SetProperty(saves, "RecoveryRequired", existingRecovery);
            SetProperty(saves, "RecoveryMessage", "synthetic-previous-recovery-message");
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Call(saves, "LoadOrCreate");
            object[] args = { syntheticSave, null };
            Assert.That(Call(saves, "TrySave", args), Is.False);
            Assert.That(args[1], Is.EqualTo(Constant("BindingMessage")));
            object[] reasonArgs = { syntheticSave, "purchase", null };
            Assert.That(Call(saves, "TrySaveWithReason", reasonArgs), Is.False);
            Assert.That(reasonArgs[2], Is.EqualTo(Constant("BindingMessage")));
            foreach (string method in new[] { "SaveCurrentGame", "SaveForSuspend" }) Call(saves, method);
            Call(saves, "SaveCurrentGameWithReason", "suspend");
            Call(saves, "SaveAfterDungeonStageClear", 7);
            Call(game, "InitializeFromSave", NewSave());
            Assert.That(P(saves, "StorageStartupMessage"), Is.EqualTo(Constant("BindingMessage")));
            memory(); unchanged();
        }

        [Test]
        public void InvalidContextDoesNotReadStorageOrTurnConfigurationFailureIntoSaveRecovery()
        {
            PoisonAccountFiles();
            SetField(saves, "storageContext", Context("InvalidConfiguration", true));
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Call(saves, "LoadOrCreate");
            Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
            Assert.That(P(saves, "StorageStartupMessage"), Is.EqualTo(Constant("InvalidMessage")));
            AssertBlockedGetter("RootDirectory", Constant("InvalidMessage"));
            AssertBlockedGetter("DataDirectory", Constant("InvalidMessage"));
            Assert.That(P(saves, "RecoveryRequired"), Is.False);
            memory(); unchanged();
        }

        [Test]
        public void BindingPendingCannotFallBackWhenTheGlobalOptInOverrideIsLaterRemoved()
        {
            BindingRequired();
            Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
            object captured = F(saves, "storageContext");
            T("Managers.SaveManager").GetField("EditorNamespaceBindingRequiredOverride", StaticHidden).SetValue(null, false);
            Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
            Assert.That(F(saves, "storageContext"), Is.SameAs(captured));
            AssertBlockedGetter("RootDirectory", Constant("BindingMessage"));
        }

        [Test]
        public void ChangingCapturedOverrideBlocksLoadSaveAndLifecycleWithoutRetargeting()
        {
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            object captured = F(saves, "storageContext");
            PoisonAccountFiles();
            string other = Path.Combine(root, "other-root-must-not-be-created");
            directoryOverride.SetValue(null, other);
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Call(saves, "LoadOrCreate");
            object[] args = { syntheticSave, null };
            Assert.That(Call(saves, "TrySave", args), Is.False);
            Assert.That(args[1], Is.EqualTo(Constant("ChangedMessage")));
            Call(saves, "SaveForSuspend"); Call(saves, "SaveAfterDungeonStageClear", 7);
            AssertBlockedGetter("RootDirectory", Constant("ChangedMessage"));
            AssertBlockedGetter("DataDirectory", Constant("ChangedMessage"));
            memory(); unchanged();
            directoryOverride.SetValue(null, root);
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            Assert.That(F(saves, "storageContext"), Is.SameAs(captured));
            Assert.That(P(saves, "RootDirectory"), Is.EqualTo(root));
            Assert.That(Directory.Exists(other), Is.False);
        }

        [Test]
        public void PendingOnlineStatusExecutePrepareAndRunCoreIgnoreSyntheticLiveDataAndEditorDelivery()
        {
            BindingRequired(); PoisonAccountFiles();
            Component online = Manager("Save.OnlinePlayerData");
            SetField(online, "baseUrl", "https://synthetic.invalid/no-network");
            SetField(online, "purchaseReady", true);
            SetField(online, "adRewardsEnabled", true);
            int editorCalls = 0, completedCalls = 0;
            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", StaticHidden)
                .SetValue(null, (Action<string, Action<string, string>>)((json, done) => editorCalls++));
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Assert.That(StaticProperty("Save.OnlinePlayerData", "Busy"), Is.True);
            Assert.That(StaticProperty("Save.OnlinePlayerData", "CanStartPurchase"), Is.False);
            Assert.That(StaticProperty("Save.OnlinePlayerData", "AdRewardsAvailable"), Is.False);
            Assert.That(StaticProperty("Save.OnlinePlayerData", "ContractUnavailableMessage"), Is.EqualTo(Constant("BindingMessage")));
            Delegate completed = Completion((operation, error) =>
            {
                completedCalls++;
                Assert.That(operation, Is.Null);
                Assert.That(error, Is.EqualTo(Constant("BindingMessage")));
            });
            Call(online, "Execute", Request(), completed);
            Assert.That(completedCalls, Is.EqualTo(1));
            Assert.That(editorCalls, Is.Zero);
            Assert.That(Call(online, "Prepare"), Is.False);
            Assert.That(F(online, "failure"), Is.EqualTo(Constant("BindingMessage")));
            IEnumerator routine = (IEnumerator)Call(online, "RunCore", Request(), completed);
            try { Assert.That(routine.MoveNext(), Is.False); }
            finally { (routine as IDisposable)?.Dispose(); }
            Assert.That(completedCalls, Is.EqualTo(2));
            Assert.That(editorCalls, Is.Zero);
            Assert.That(F(online, "credentials"), Is.Null);
            Assert.That(F(online, "configuredStorageOwner"), Is.Null);
            Assert.That(F(online, "busy"), Is.False);
            memory(); unchanged();
        }

        [Test]
        public void PrivateFixtureSendRejectsNonLoopbackBeforeConstructingTransport()
        {
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            PoisonAccountFiles();
            Component online = Manager("Save.OnlinePlayerData");
            SetField(online, "baseUrl", "https://synthetic.invalid/no-network");
            SetField(online, "status", 200L);
            SetField(online, "response", "synthetic-prior-response");
            SetField(online, "failure", "synthetic-prior-failure");
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            IEnumerator routine = (IEnumerator)Call(online, "Send", "GET", "/v1/store/apple/capabilities", null, false, null);
            try { Assert.That(routine.MoveNext(), Is.False, "A private fixture must not reach any transport yield."); }
            finally { (routine as IDisposable)?.Dispose(); }
            Assert.That(F(online, "status"), Is.EqualTo(0L));
            Assert.That(F(online, "failure"), Is.EqualTo("私有テスト保存先ではローカル検証サーバーのみ利用できます。"));
            Assert.That(F(online, "response"), Is.EqualTo("synthetic-prior-response"));
            Assert.That(F(online, "credentials"), Is.Null);
            memory(); unchanged();
        }

        [TestCase(false)] [TestCase(true)]
        public void FinalSnapshotCannotReturnAnOldDeliveryAfterStorageOwnerReplacement(bool destroyPreviousOwner)
        {
            Component online = Manager("Save.OnlinePlayerData");
            int callbacks = 0;
            object delivered = null;
            string error = null;
            Delegate completed = Completion((operation, message) => { callbacks++; delivered = operation; error = message; });
            IEnumerator routine = AppliedOperationWaitingForFinalSnapshot(online, completed);
            try
            {
                Assert.That(callbacks, Is.Zero);
                if (destroyPreviousOwner) UnityEngine.Object.DestroyImmediate(saves.gameObject);
                Component replacement = Manager("Managers.SaveManager");
                object replacementSave = NewSave();
                SetField(replacementSave, "PlayerId", new string('b', 32));
                SetField(replacementSave, "Gold", 77);
                SetField(replacementSave, "FreeGachaStones", 600);
                SetProperty(replacement, "CurrentSaveData", replacementSave);
                SetProperty(game, "PlayerProfile", Activator.CreateInstance(T("Data.PlayerProfile"), replacementSave));
                SetProperty(game, "CurrentFloor", 11);
                Assert.That(P(replacement, "StorageAccessAvailable"), Is.True);
                string replacementJson = JsonUtility.ToJson(replacementSave);
                Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
                // This one nested Send is safe to resume: its first instruction
                // rejects the stale owner before constructing any HTTP request.
                Assert.That(((IEnumerator)routine.Current).MoveNext(), Is.False);
                string blocked = (string)F(online, "failure");
                Assert.That(blocked, Is.Not.Empty);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(delivered, Is.Null);
                Assert.That(error, Is.EqualTo(blocked));
                Assert.That(F(online, "failure"), Is.EqualTo(blocked));
                Assert.That(F(online, "busy"), Is.False);
                Call(online, "LateUpdate");
                Assert.That(((GameObject)F(online, "inputBlocker")).activeSelf, Is.True,
                    "An available replacement must not unblock the old online session.");
                Assert.That(P(replacement, "CurrentSaveData"), Is.SameAs(replacementSave));
                Assert.That(JsonUtility.ToJson(replacementSave), Is.EqualTo(replacementJson));
                memory(); unchanged();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        [Test]
        public void FinalSnapshotNetworkFailureOnSameOwnerPreservesTheDurableDelivery()
        {
            Component online = Manager("Save.OnlinePlayerData");
            int callbacks = 0;
            object delivered = null;
            string error = "synthetic-callback-not-reached";
            Delegate completed = Completion((operation, message) => { callbacks++; delivered = operation; error = message; });
            IEnumerator routine = AppliedOperationWaitingForFinalSnapshot(online, completed);
            try
            {
                Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
                // Do not run the transport. Model only its ordinary network
                // failure while keeping the original immutable owner intact.
                SetField(online, "status", 0L);
                SetField(online, "failure", "synthetic-final-snapshot-network-failure");
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(error, Is.Null);
                Assert.That(delivered, Is.Not.Null);
                Assert.That(F(delivered, "RequestId"), Is.EqualTo("synthetic-final-snapshot-request"));
                Assert.That(F(delivered, "Revision"), Is.EqualTo(1L));
                Assert.That(F(online, "failure"), Is.Null);
                Assert.That(F(online, "busy"), Is.False);
                Call(online, "LateUpdate");
                Assert.That(F(online, "inputBlocker"), Is.Null);
                memory(); unchanged();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        [TestCase(false)] [TestCase(true)]
        public void PendingAppleOpenAndResumeDoNotLoadExistingRecoveryOrDeletionJournals(bool resume)
        {
            BindingRequired(); PoisonAccountFiles();
            Component online = Manager("Save.OnlinePlayerData");
            T("Save.OnlinePlayerData").GetField("EditorAppleAccountOverride", StaticHidden).SetValue(null, true);
            SetField(online, "accountMessage", "synthetic-previous-account-message");
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Assert.That(StaticProperty("Save.OnlinePlayerData", "AppleAccountEnabled"), Is.False);
            Call(online, "OpenAppleAccount", resume);
            Assert.That(F(online, "accountMenu"), Is.False);
            Assert.That(F(online, "accountJournal"), Is.Null);
            Assert.That(F(online, "deletionJournal"), Is.Null);
            Assert.That(F(online, "accountMessage"), Is.EqualTo("synthetic-previous-account-message"));
            memory(); unchanged();
        }

        [TestCase(false)] [TestCase(true)]
        public void PendingTenPullJournalDoesNotReadReplaceOrClearTheExistingPrefsKey(bool hasKey)
        {
            BindingRequired();
            bool previouslyPresent = PlayerPrefs.HasKey(PresentationKey);
            string previous = previouslyPresent ? PlayerPrefs.GetString(PresentationKey) : null;
            const string sentinel = "synthetic-existing-presentation-must-not-be-parsed";
            try
            {
                if (hasKey) PlayerPrefs.SetString(PresentationKey, sentinel);
                else PlayerPrefs.DeleteKey(PresentationKey);
                PlayerPrefs.Save();
                int ownsCallbacks = 0;
                Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
                Assert.That(StaticProperty("Home.TenPullPresentationJournal", "HasPending"), Is.False);
                Assert.That(StaticCall("Home.TenPullPresentationJournal", "Read", (Func<string, bool>)(_ => { ownsCallbacks++; return true; })), Is.Null);
                Array results = Array.CreateInstance(T("Home.SummonPresentationResult"), 1);
                object item = Activator.CreateInstance(T("Home.SummonPresentationResult"));
                SetField(item, "InstanceId", "synthetic-presentation-instance");
                results.SetValue(item, 0);
                StaticCall("Home.TenPullPresentationJournal", "Store", results);
                StaticCall("Home.TenPullPresentationJournal", "Clear");
                Assert.That(ownsCallbacks, Is.Zero);
                Assert.That(PlayerPrefs.HasKey(PresentationKey), Is.EqualTo(hasKey));
                if (hasKey) Assert.That(PlayerPrefs.GetString(PresentationKey), Is.EqualTo(sentinel));
                memory(); unchanged();
            }
            finally
            {
                if (previouslyPresent) PlayerPrefs.SetString(PresentationKey, previous);
                else PlayerPrefs.DeleteKey(PresentationKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void PendingIapFulfillmentYieldsBeforeIsolatedJournalDeliveryOrConfirmation()
        {
            BindingRequired(); PoisonAccountFiles();
            Component service = Manager("Monetization.InAppPurchaseService");
            int deliveries = 0, confirmations = 0, sceneChecks = 0;
            SetField(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, transaction, done) => deliveries++));
            SetField(service, "EditorConfirmPurchaseOverride", (Action)(() => confirmations++));
            SetField(service, "EditorDeliverySceneReadinessOverride", (Func<bool>)(() => { sceneChecks++; return true; }));
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Product, 120, "synthetic-price");
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, "synthetic-startup-pending-transaction");
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(routine.MoveNext(), Is.True);
                    Assert.That(routine.Current, Is.Null, "Only the pre-storage pending loop may execute.");
                }
                Assert.That(deliveries, Is.Zero); Assert.That(confirmations, Is.Zero); Assert.That(sceneChecks, Is.Zero);
                memory(); unchanged();
            }
            finally { (routine as IDisposable)?.Dispose(); }
            Assert.That(deliveries, Is.Zero); Assert.That(confirmations, Is.Zero);
            memory(); unchanged();
        }

        [TestCase(false)] [TestCase(true)]
        public void ConfirmationForPreviousStorageOwnerCannotAcknowledgeAgainstANewManager(bool destroyPreviousOwner)
        {
            Assert.That(P(saves, "StorageAccessAvailable"), Is.True);
            PoisonAccountFiles();
            Component service = Manager("Monetization.InAppPurchaseService");
            SetField(service, "deliveryStorageOwner", saves);
            int confirmations = 0;
            SetField(service, "EditorConfirmPurchaseOverride", (Action)(() => confirmations++));
            const string transaction = "synthetic-confirmation-original-owner";
            Action unchanged = AssertTreeUnchangedAfter(), memory = AssertMemoryUnchangedAfter();
            Call(service, "ConfirmPurchase", null, transaction);
            Assert.That(confirmations, Is.EqualTo(1));
            var pending = (HashSet<string>)F(service, "confirmationsInFlight");
            var failed = (HashSet<string>)F(service, "failedConfirmations");
            var bindings = (IDictionary)F(service, "confirmationStorageOwners");
            Assert.That(pending.Contains(transaction), Is.True);
            Assert.That(bindings[transaction], Is.SameAs(saves));
            // A new session owner can have the same path and synthetic memory;
            // that still must not inherit an in-flight confirmation binding.
            // Unity's destroyed-object == null must not turn a bound owner into
            // a supposedly unbound service that can adopt the replacement.
            if (destroyPreviousOwner) UnityEngine.Object.DestroyImmediate(saves.gameObject);
            Component replacement = Manager("Managers.SaveManager");
            SetProperty(replacement, "CurrentSaveData", syntheticSave);
            Assert.That(P(replacement, "StorageAccessAvailable"), Is.True);
            Call(service, "HandleConfirmationResult", transaction, false);
            Assert.That(pending.Contains(transaction), Is.False);
            Assert.That(bindings.Contains(transaction), Is.False);
            Assert.That(failed.Contains(transaction), Is.True);
            Call(service, "ConfirmPurchase", null, transaction);
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(pending.Contains(transaction), Is.False);
            memory(); unchanged();
        }
    }
}
