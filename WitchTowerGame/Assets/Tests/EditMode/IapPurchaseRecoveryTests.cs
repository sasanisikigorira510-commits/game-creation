using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class IapPurchaseRecoveryTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Prefix = "com.nasus.dungeonmonsterroguelike.crystals";
        private string directory;
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly Dictionary<Type, object> previousInstances = new Dictionary<Type, object>();
        private object previousSaveOverride;
        private bool saveOverrideInstalled;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static Type Iap(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.Purchasing." + name)).First(t => t != null);
        private static object Field(object value, string field) => value.GetType().GetField(field).GetValue(value);
        private static object Prop(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        private static object Call(object value, string method, params object[] args) =>
            value.GetType().GetMethod(method, Hidden).Invoke(value, args);
        private static void Set(object value, string field, object data) => value.GetType().GetField(field, Hidden).SetValue(value, data);
        private static object NewSave() => T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static object Profile(object save = null) => Activator.CreateInstance(T("Data.PlayerProfile"), save ?? NewSave());
        private static int Balance(object profile) => (int)Prop(profile, "PaidGachaStones");
        private static IList Ids(object profile) => (IList)Prop(profile, "ProcessedIapTransactionIds");
        private string SavePath => Path.Combine(directory, "save.json");

        [SetUp]
        public void SetUp()
        {
            directory = null;
            saveOverrideInstalled = false;
            directory = Path.Combine(Path.GetTempPath(), "WitchTower-IapRecovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            previousSaveOverride = saveOverride.GetValue(null);
            saveOverride.SetValue(null, directory);
            saveOverrideInstalled = true;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject owner in owners) UnityEngine.Object.DestroyImmediate(owner);
            owners.Clear();
            foreach (var entry in previousInstances)
                entry.Key.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, entry.Value);
            previousInstances.Clear();
            if (saveOverrideInstalled)
            {
                T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousSaveOverride);
                saveOverrideInstalled = false;
            }
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private Component Manager(string typeName)
        {
            Type type = T(typeName);
            var singleton = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            previousInstances[type] = singleton.GetValue(null);
            var owner = new GameObject("IsolatedIapTest");
            owner.SetActive(false);
            owners.Add(owner);
            Component component = owner.AddComponent(type);
            singleton.SetValue(null, component);
            if (typeName == "Monetization.InAppPurchaseService")
                Set(component, "EditorDeliverySceneReadinessOverride", (Func<bool>)(() => true));
            return component;
        }

        private bool Persist(object save)
        {
            var args = new[] { (object)SavePath, save, null, true };
            return (bool)T("Save.SaveFileStore").GetMethod("TrySave").Invoke(null, args);
        }

        private object ReadSave()
        {
            var args = new object[] { SavePath, null, null };
            Assert.That(T("Save.SaveFileStore").GetMethod("TryLoad").Invoke(null, args), Is.True, args[2]?.ToString());
            return args[1];
        }

        private string BlockNextManagedSave(Component saves)
        {
            Assert.That(Prop(saves, "StorageAccessAvailable"), Is.True);
            Assert.That(Prop(saves, "RootDirectory"), Is.EqualTo(directory));
            object current = Prop(saves, "CurrentSaveData");
            Assert.That(current, Is.Not.Null, "Keep a successful generation before injecting a file failure.");
            long nextRevision = checked((long)Field(current, "SaveRevision") + 1);
            string blocked = Path.Combine(SavePath + ".history", nextRevision.ToString("D20") + ".json.tmp");
            Assert.That(File.Exists(blocked) || Directory.Exists(blocked), Is.False);
            // FileStream must fail at the real generation temp path. The fixed
            // startup root and every previous committed file remain untouched.
            Directory.CreateDirectory(blocked);
            return blocked;
        }

        private Dictionary<string, byte[]> CommittedSaveFiles() => Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);

        private void AssertCommittedSaveFilesUnchanged(Dictionary<string, byte[]> before)
        {
            CollectionAssert.AreEquivalent(before.Keys, Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
            foreach (var file in before) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
        }

        private bool Fulfill(object profile, string productId, string tx, Func<object, bool> save, Action confirm, out bool granted)
        {
            var parameter = Expression.Parameter(T("Save.PlayerSaveData"));
            Delegate persist = Expression.Lambda(typeof(Func<,>).MakeGenericType(parameter.Type, typeof(bool)),
                Expression.Invoke(Expression.Constant(save), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            var args = new object[] { profile, 7, productId, tx, persist, confirm, false, null };
            bool result = (bool)T("Monetization.IapPurchaseFulfillment").GetMethod("TryFulfill").Invoke(null, args);
            granted = (bool)args[6];
            return result;
        }

        [TestCase(120)] [TestCase(650)] [TestCase(2000)]
        [TestCase(4200)] [TestCase(8600)] [TestCase(15000)]
        public void EveryProductIsDurableBeforeBalanceChangesAndStoreConfirmation(int amount)
        {
            object profile = Profile();
            int confirmed = 0;
            Assert.That(Fulfill(profile, Prefix + amount, "success-1", staged =>
            {
                Assert.That(Balance(profile), Is.Zero, "Unpersisted currency must not be spendable.");
                Assert.That(Ids(profile), Is.Empty);
                Assert.That(Prop(profile, "HasRemovedAds"), Is.False);
                Assert.That(Field(staged, "PaidGachaStones"), Is.EqualTo(amount));
                Assert.That(Field(staged, "HasRemovedAds"), Is.True);
                Assert.That((IList)Field(staged, "ProcessedIapTransactionIds"), Does.Contain("success-1"));
                return Persist(staged);
            }, () =>
            {
                confirmed++;
                Assert.That(Balance(profile), Is.EqualTo(amount));
                Assert.That(Prop(profile, "HasRemovedAds"), Is.True);
                Assert.That(Field(ReadSave(), "HasRemovedAds"), Is.True);
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(amount));
            }, out bool granted), Is.True);
            Assert.That(granted, Is.True);
            Assert.That(confirmed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void SaveFailureLeavesLiveBalanceIdsAndDiskUntouched(bool throws)
        {
            object profile = Profile();
            Assert.That(Persist(profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 7 })), Is.True);
            string before = File.ReadAllText(SavePath);
            int confirmed = 0;
            Assert.That(Fulfill(profile, Prefix + "120", "failed-1", _ =>
            {
                if (throws) throw new IOException("Simulated full disk");
                return false;
            }, () => confirmed++, out bool granted), Is.False);
            Assert.That(granted, Is.False);
            Assert.That(Balance(profile), Is.Zero);
            Assert.That(Ids(profile), Is.Empty);
            Assert.That(Prop(profile, "HasRemovedAds"), Is.False);
            Assert.That(confirmed, Is.Zero);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(before));
            object restarted = Profile(ReadSave());
            Assert.That(Fulfill(restarted, Prefix + "120", "failed-1", Persist, () => confirmed++, out granted), Is.True);
            Assert.That(granted, Is.True);
            Assert.That(Balance(restarted), Is.EqualTo(120));
            Assert.That(confirmed, Is.EqualTo(1));
        }

        [Test]
        public void HistoricalPurchaseRemovesBannerAfterRestartEvenWithNoPaidStones()
        {
            object save = NewSave();
            ((IList)Field(save, "ProcessedIapTransactionIds")).Add("old-confirmed-purchase");
            Assert.That(Field(save, "HasRemovedAds"), Is.False, "Simulate a save from before this benefit.");
            object profile = Profile(save);
            Assert.That(Balance(profile), Is.Zero);
            Assert.That(Prop(profile, "HasRemovedAds"), Is.True);
            Assert.That(T("Monetization.AdRemovalEntitlementService").GetMethod("ShouldShowBanner")
                .Invoke(null, new[] { profile }), Is.False);
            Assert.That(Persist(profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 })), Is.True);
            Assert.That(Prop(Profile(ReadSave()), "HasRemovedAds"), Is.True);
        }

        [Test]
        public void CurrencyAndEmptyTransactionIdsDoNotCountAsPurchases()
        {
            object save = NewSave();
            save.GetType().GetField("PaidGachaStones").SetValue(save, 120);
            var ids = (IList)Field(save, "ProcessedIapTransactionIds");
            ids.Add(null); ids.Add(""); ids.Add(" ");
            object profile = Profile(save);
            Assert.That(Prop(profile, "HasRemovedAds"), Is.False);
            Assert.That(T("Monetization.AdRemovalEntitlementService").GetMethod("ShouldShowBanner")
                .Invoke(null, new[] { profile }), Is.True);
        }

        [Test]
        public void RedeliveryAfterRestartAndSpendingDoesNotGrantAgainOrResetBalance()
        {
            object profile = Profile();
            Assert.That(Fulfill(profile, Prefix + "650", "persisted-1", Persist, () => { }, out _), Is.True);
            object restarted = Profile(ReadSave());
            restarted.GetType().GetMethod("TrySpendPaidGachaStones").Invoke(restarted, new object[] { 100 });
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Fulfill(restarted, Prefix + "650", "persisted-1", Persist, () => { }, out bool granted), Is.True);
                Assert.That(granted, Is.False);
            }
            Assert.That(Balance(restarted), Is.EqualTo(550));
            Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(550));
            Assert.That(Ids(restarted).Count, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmationFailureRetainsDurableGrantAndRetryOnlyConfirms()
        {
            object profile = Profile();
            Assert.That(Fulfill(profile, Prefix + "120", "confirm-failed", Persist,
                () => throw new IOException("Store disconnected"), out bool granted), Is.False);
            Assert.That(granted, Is.True);
            object restarted = Profile(ReadSave());
            Assert.That(Balance(restarted), Is.EqualTo(120));
            Assert.That(Fulfill(restarted, Prefix + "120", "confirm-failed", Persist, () => { }, out granted), Is.True);
            Assert.That(granted, Is.False);
            Assert.That(Balance(restarted), Is.EqualTo(120));
        }

        [Test]
        public void InMemoryTransactionIdIsNotEnoughToConfirmAfterFailedSave()
        {
            object profile = Profile();
            profile.GetType().GetMethod("TryGrantPaidStonePurchase").Invoke(profile, new object[] { Prefix + "120", 120, "memory-only" });
            int confirmed = 0;
            Assert.That(Fulfill(profile, Prefix + "120", "memory-only", _ => false, () => confirmed++, out _), Is.False);
            Assert.That(confirmed, Is.Zero);
            Assert.That(Fulfill(profile, Prefix + "120", "memory-only", Persist, () => confirmed++, out _), Is.True);
            Assert.That(Balance(profile), Is.EqualTo(120));
            Assert.That(confirmed, Is.EqualTo(1));
        }

        [TestCase("unknown", "tx")] [TestCase(Prefix + "120", "")]
        public void InvalidOrderNeverSavesOrConfirms(string productId, string tx)
        {
            Assert.That(Fulfill(Profile(), productId, tx, _ => throw new Exception("Must not save"),
                () => Assert.Fail("Must not confirm"), out bool granted), Is.False);
            Assert.That(granted, Is.False);
        }

        [Test]
        public void OverflowNeverAcknowledgesOrCorruptsBalance()
        {
            object profile = Profile();
            profile.GetType().GetProperty("PaidGachaStones").SetValue(profile, int.MaxValue);
            bool saved = false, confirmed = false;
            Assert.That(Fulfill(profile, Prefix + "120", "overflow", _ => saved = true, () => confirmed = true, out _), Is.False);
            Assert.That(saved || confirmed, Is.False);
            Assert.That(Balance(profile), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void SaveManagerReportsActualFileFailureWithoutReplacingLastSuccessfulSave()
        {
            Component saves = Manager("Managers.SaveManager");
            Manager("Managers.GameManager");
            var goodArgs = new object[] { NewSave(), null };
            var method = saves.GetType().GetMethod("TrySave");
            Assert.That(method.Invoke(saves, goodArgs), Is.True);
            object previous = Prop(saves, "CurrentSaveData");
            var before = CommittedSaveFiles();
            string blocked = BlockNextManagedSave(saves);
            try
            {
                var failedArgs = new object[] { NewSave(), null };
                Assert.That(method.Invoke(saves, failedArgs), Is.False);
                Assert.That(failedArgs[1], Is.Not.Empty);
                Assert.That(Prop(saves, "StorageAccessAvailable"), Is.True, "Failure is storage I/O, not a changed startup context.");
                Assert.That(Prop(saves, "RootDirectory"), Is.EqualTo(directory));
                Assert.That(Prop(saves, "CurrentSaveData"), Is.SameAs(previous));
                AssertCommittedSaveFilesUnchanged(before);
            }
            finally { if (Directory.Exists(blocked)) Directory.Delete(blocked); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ServiceFetchesPendingPurchasesAfterProductsAndOnForegroundWithoutConcurrentFetches(bool newCheckoutBlocked)
        {
            Component service = Manager("Monetization.InAppPurchaseService");
            if (newCheckoutBlocked)
                Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => "Synthetic checkout blocked."));
            Set(service, "initializationStarted", true);
            Set(service, "storeConnected", true);
            Set(service, "EditorFetchProductsOverride", (Action)(() => { }));
            int fetched = 0;
            Set(service, "EditorFetchPurchasesOverride", (Action)(() => fetched++));
            object definition = Activator.CreateInstance(Iap("ProductDefinition"), Prefix + "120", Enum.Parse(Iap("ProductType"), "Consumable"));
            object product = Activator.CreateInstance(Iap("Product"), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { definition, Activator.CreateInstance(Iap("ProductMetadata")), true }, null);
            IList products = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Iap("Product")));
            products.Add(product);
            Call(service, "HandleProductsFetched", products);
            Assert.That(fetched, Is.EqualTo(1));
            Call(service, "OnApplicationFocus", true);
            Assert.That(fetched, Is.EqualTo(1));
            Call(service, "HandlePurchasesFetched", new object[] { null });
            Call(service, "OnApplicationFocus", true);
            Assert.That(fetched, Is.EqualTo(1), "Foreground refresh must finish updating the catalog before pending-order recovery.");
            Call(service, "HandleProductsFetched", products);
            Assert.That(fetched, Is.EqualTo(2));
            Call(service, "HandlePurchasesFetched", new object[] { null });
            Call(service, "OnApplicationFocus", false);
            Assert.That(fetched, Is.EqualTo(2));
        }

        private void InstallVerifiedServerDelivery(Component service, Component saves, object profile)
        {
            // Simulate an authenticated server response, using the actual staging
            // and durable-save code before the store acknowledgement is allowed.
            Set(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, tx, done) =>
            {
                object before = profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 7 });
                object operation = Activator.CreateInstance(T("Save.OnlineOperation"));
                operation.GetType().GetField("Revision").SetValue(operation, 1L);
                operation.GetType().GetField("Free").SetValue(operation, 900);
                operation.GetType().GetField("Paid").SetValue(operation, 120);
                operation.GetType().GetField("TransactionId").SetValue(operation, tx);
                object staged = T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { before, operation });
                var args = new object[] { staged, null };
                bool saved = (bool)saves.GetType().GetMethod("TrySave").Invoke(saves, args);
                if (saved)
                {
                    profile.GetType().GetProperty("PaidGachaStones").SetValue(profile, 120);
                    profile.GetType().GetProperty("EconomyRevision").SetValue(profile, 1L);
                    if (!Ids(profile).Contains(tx)) Ids(profile).Add(tx);
                }
                done(saved, saved ? null : "Delivery is pending local storage.");
            }));
        }

        [Test]
        public void DeferredDeliveryReportsItsSpecificReasonOnceWithoutGrantOrConfirmation()
        {
            const string transaction = "fixture-environment-mismatch";
            const string reason = "テスト購入と接続先の環境が一致しません。再購入しないでください。";
            Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            Manager("Save.OnlinePlayerData");
            Component service = Manager("Monetization.InAppPurchaseService");
            int deliveries = 0, confirmations = 0;
            Set(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, tx, done) =>
            { deliveries++; done(false, reason); }));
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() => confirmations++));
            var pending = (HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service);
            pending.Add(transaction);
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            var reports = new List<string>();
            Action<string> received = reports.Add;
            var failed = service.GetType().GetEvent("PurchaseFailed");
            failed.AddEventHandler(null, received);
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, transaction);
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.InstanceOf<WaitForSecondsRealtime>());
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(deliveries, Is.EqualTo(2));
                Assert.That(reports, Is.EqualTo(new[] { reason }));
                Assert.That(confirmations, Is.Zero);
                Assert.That(Balance(profile), Is.Zero);
                Assert.That(Ids(profile), Is.Empty);
                Assert.That(File.Exists(SavePath), Is.False);
                Assert.That(pending, Does.Contain(transaction));
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                failed.RemoveEventHandler(null, received);
            }
        }

        [Test]
        public void DeliveryWaitsForSceneReadinessWithoutGrantingOrAcknowledging()
        {
            const string transaction = "synthetic-scene-readiness-transaction";
            Component saves = Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            Manager("Save.OnlinePlayerData");
            Component service = Manager("Monetization.InAppPurchaseService");
            bool ready = false;
            Set(service, "EditorDeliverySceneReadinessOverride", (Func<bool>)(() => ready));
            InstallVerifiedServerDelivery(service, saves, profile);
            var verifiedDelivery = (Action<string, string, Action<bool, string>>)service.GetType()
                .GetField("EditorVerifiedDeliveryOverride", Hidden).GetValue(service);
            int deliveries = 0, confirmations = 0;
            Set(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, tx, done) =>
            {
                deliveries++;
                verifiedDelivery(product, tx, done);
            }));
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() =>
            {
                confirmations++;
                Call(service, "HandleConfirmationResult", transaction, false);
            }));
            var pending = (HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service);
            pending.Add(transaction);
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, transaction);
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.InstanceOf<WaitForSecondsRealtime>());
                Assert.That(deliveries, Is.Zero);
                Assert.That(confirmations, Is.Zero);
                Assert.That(Balance(profile), Is.Zero);
                Assert.That(Ids(profile), Is.Empty);
                Assert.That(File.Exists(SavePath), Is.False);
                Assert.That(pending, Does.Contain(transaction));
                ready = true;
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(confirmations, Is.EqualTo(1));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
                Assert.That(Ids(profile).Count, Is.EqualTo(1));
                Assert.That(pending, Is.Empty);
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }

        [TestCase(false)] [TestCase(true)]
        public void DeliveryRetryDoesNotLogSensitiveMessageAndStillRecoversWithoutPrematureConfirmation(bool newCheckoutBlocked)
        {
            const string marker = "SYNTHETIC_IAP_PRIVATE_DELIVERY_RECEIPT_TRANSACTION";
            string transaction = marker + "-transaction";
            Component saves = Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            // Prevent another service's state from affecting the isolated Busy check.
            Manager("Save.OnlinePlayerData");
            Component service = Manager("Monetization.InAppPurchaseService");
            if (newCheckoutBlocked)
                Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => "Synthetic checkout blocked."));
            Set(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)
                ((product, tx, done) => done(false, marker + "-provider-message")));
            int confirmed = 0;
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() =>
            {
                confirmed++;
                Call(service, "HandleConfirmationResult", transaction, false);
            }));
            ((HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service)).Add(transaction);
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, transaction);
            var messages = new List<string>();
            var allLogText = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                allLogText.Add(message);
                allLogText.Add(trace);
                if (message.StartsWith("[IAP]", StringComparison.Ordinal)) messages.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(messages, Is.EqualTo(new[] { "[IAP] Event=DeliveryRetryPending" }));
                Assert.That(string.Join("\n", allLogText), Does.Not.Contain(marker));
                Assert.That(confirmed, Is.Zero);
                Assert.That(Balance(profile), Is.Zero);
                Assert.That(File.Exists(SavePath), Is.False);
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.True);

                InstallVerifiedServerDelivery(service, saves, profile);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(confirmed, Is.EqualTo(1));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Ids(profile).Count, Is.EqualTo(1));
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.False);
                Assert.That(messages.Count, Is.EqualTo(1));
                Assert.That(string.Join("\n", allLogText), Does.Not.Contain(marker));
            }
            finally
            {
                Application.logMessageReceived -= capture;
                (routine as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void SynchronousConfirmationExceptionRetriesWithoutLoggingSecretsOrReportingPrematureSuccess()
        {
            const string marker = "SYNTHETIC_IAP_PRIVATE_CONFIRM_RECEIPT_TRANSACTION";
            string transaction = marker + "-transaction";
            Component saves = Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            Manager("Save.OnlinePlayerData");
            Component service = Manager("Monetization.InAppPurchaseService");
            InstallVerifiedServerDelivery(service, saves, profile);
            var initialDelivery = (Action<string, string, Action<bool, string>>)service.GetType()
                .GetField("EditorVerifiedDeliveryOverride", Hidden).GetValue(service);
            int deliveries = 0;
            Set(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, tx, done) =>
            {
                if (++deliveries == 1) initialDelivery(product, tx, done);
                else done(true, null); // Duplicate delivery does not mutate the durable grant.
            }));
            int confirmationAttempts = 0, successes = 0;
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() =>
            {
                confirmationAttempts++;
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120), "Grant must be durable before acknowledgement.");
                if (confirmationAttempts == 1) throw new IOException(marker + "-exception-message");
                Call(service, "HandleConfirmationResult", transaction, false);
            }));
            var pending = (HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service);
            pending.Add(transaction);
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, transaction);
            var messages = new List<string>();
            var allLogText = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                allLogText.Add(message);
                allLogText.Add(trace);
                if (message.StartsWith("[IAP]", StringComparison.Ordinal)) messages.Add(message);
            };
            Action<string, int> succeeded = (product, amount) => successes++;
            EventInfo successEvent = service.GetType().GetEvent("PurchaseSucceeded");
            successEvent.AddEventHandler(null, succeeded);
            Application.logMessageReceived += capture;
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=PurchaseConfirmationFailed; Exception=IOException");
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.InstanceOf<WaitForSecondsRealtime>());
                Assert.That(confirmationAttempts, Is.EqualTo(1));
                Assert.That(successes, Is.Zero);
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Ids(profile).Count, Is.EqualTo(1));
                Assert.That(Prop(profile, "EconomyRevision"), Is.EqualTo(1L));
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.True);
                Assert.That(pending, Does.Contain(transaction));
                string beforeRetry = File.ReadAllText(SavePath);

                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(confirmationAttempts, Is.EqualTo(2));
                Assert.That(deliveries, Is.EqualTo(2));
                Assert.That(successes, Is.EqualTo(1));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Ids(profile).Count, Is.EqualTo(1));
                Assert.That(Ids(profile), Does.Contain(transaction));
                Assert.That(Prop(profile, "EconomyRevision"), Is.EqualTo(1L));
                Assert.That(File.ReadAllText(SavePath), Is.EqualTo(beforeRetry));
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.False);
                Assert.That(pending, Is.Empty);
                Assert.That((HashSet<string>)service.GetType().GetField("confirmationsInFlight", Hidden).GetValue(service), Is.Empty);
                Assert.That((HashSet<string>)service.GetType().GetField("failedConfirmations", Hidden).GetValue(service), Is.Empty);
                Assert.That(messages, Is.EqualTo(new[] {
                    "[IAP] Event=PurchaseConfirmationFailed; Exception=IOException", "[IAP] Event=DeliveryRetryPending" }));
                Assert.That(string.Join("\n", allLogText), Does.Not.Contain(marker));
            }
            finally
            {
                Application.logMessageReceived -= capture;
                successEvent.RemoveEventHandler(null, succeeded);
                (routine as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void ActualFulfillmentCoroutineDoesNotConfirmUntilRealSaveRecovers()
        {
            Component saves = Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            game.GetType().GetProperty("CurrentFloor").SetValue(game, 7);
            Manager("Save.OnlinePlayerData");
            var initialSaveArgs = new object[] { profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 7 }), null };
            Assert.That(saves.GetType().GetMethod("TrySave").Invoke(saves, initialSaveArgs), Is.True);
            var before = CommittedSaveFiles();
            Component service = Manager("Monetization.InAppPurchaseService");
            InstallVerifiedServerDelivery(service, saves, profile);
            int confirmed = 0;
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() =>
            {
                confirmed++;
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
                Call(service, "HandleConfirmationResult", "coroutine-1", false);
            }));
            ((HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service)).Add("coroutine-1");
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            string blocked = BlockNextManagedSave(saves);
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, "coroutine-1");
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(confirmed, Is.Zero);
                Assert.That(Balance(profile), Is.Zero);
                Assert.That(Ids(profile), Is.Empty);
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.True);
                Assert.That(Prop(saves, "StorageAccessAvailable"), Is.True);
                Assert.That(Prop(saves, "RootDirectory"), Is.EqualTo(directory));
                AssertCommittedSaveFilesUnchanged(before);
                Directory.Delete(blocked);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(confirmed, Is.EqualTo(1));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
                Assert.That(Ids(profile), Does.Contain("coroutine-1"));
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.False);
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                if (Directory.Exists(blocked)) Directory.Delete(blocked);
            }
        }

        [Test]
        public void AsynchronousConfirmationFailureRetriesWithoutDependingOnSdkRedelivery()
        {
            Component saves = Manager("Managers.SaveManager");
            Component game = Manager("Managers.GameManager");
            object profile = Profile();
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            Manager("Save.OnlinePlayerData"); // Isolate Busy from a previous transport's captured storage owner.
            Component service = Manager("Monetization.InAppPurchaseService");
            Set(service, "EditorDeliverySceneReadinessOverride", (Func<bool>)(() => true));
            InstallVerifiedServerDelivery(service, saves, profile);
            int requests = 0;
            Set(service, "EditorConfirmPurchaseOverride", (Action)(() => requests++));
            ((HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service)).Add("async-confirm");
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), Prefix + "120", 120, "¥160");
            IEnumerator routine = (IEnumerator)Call(service, "FulfillWhenPlayerProfileIsReady", null, definition, "async-confirm");
            try
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.True);

                Call(service, "HandleConfirmationResult", "async-confirm", true);
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.That(routine.MoveNext(), Is.True); // Retry delay, not a second grant.
                Assert.That(routine.MoveNext(), Is.True); // Resubmit confirmation, await store.
                Assert.That(requests, Is.EqualTo(2));
                Assert.That(Balance(profile), Is.EqualTo(120));
                Call(service, "HandleConfirmationResult", "async-confirm", false);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(Prop(service, "IsPurchaseInProgress"), Is.False);
                Assert.That(Field(ReadSave(), "PaidGachaStones"), Is.EqualTo(120));
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
    }
}
