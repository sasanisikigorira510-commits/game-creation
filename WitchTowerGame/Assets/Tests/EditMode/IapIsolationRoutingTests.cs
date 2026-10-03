using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class IapIsolationRoutingTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private const string PlayerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Transaction = "synthetic-isolated-route-order";
        private const string RequestId = "apple-synthetic-isolated-route-order";
        private const string Product = "com.nasus.dungeonmonsterroguelike.crystals15000";
        private string root, source, pending, originalRequest;
        private object previousOverride, productionProfile;
        private bool overrideInstalled;
        private Component game, service;
        private int normalDeliveries, confirmationAttempts, successes;
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly Dictionary<Type, object> singletons = new Dictionary<Type, object>();
        private readonly Dictionary<FieldInfo, object> events = new Dictionary<FieldInfo, object>();
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);
        private static void SetPublic(object value, string name, object data) => value.GetType().GetField(name).SetValue(value, data);
        private static void SetHidden(object value, string name, object data) => value.GetType().GetField(name, Hidden).SetValue(value, data);
        private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Hidden).Invoke(value, args);
        private static object Storage(string name, params object[] args) => T("Save.PurchaseIsolationRecoveryStorage").GetMethod(name).Invoke(null, args);
        private static object Online(string name, params object[] args) => T("Save.OnlinePlayerData").GetMethod(name, StaticHidden).Invoke(null, args);
        private static object NewSave() => T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static object Profile(string id)
        {
            object save = NewSave(); SetPublic(save, "PlayerId", id);
            return Activator.CreateInstance(T("Data.PlayerProfile"), save);
        }
        private string JournalPath => (string)Storage("JournalPath", root);
        private string IsolatedSavePath => (string)Storage("IsolatedSavePath", root);
        private object Journal() => Storage("Read", root);

        [SetUp] public void SetUp()
        {
            normalDeliveries = 0; confirmationAttempts = 0; successes = 0;
            game = null; service = null; productionProfile = null;
            root = Path.Combine(Path.GetTempPath(), "WitchTower-IapIsolationRouting-" + Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "environments", "production", "linked-accounts", new string('b', 32));
            Directory.CreateDirectory(source);
            pending = Path.Combine(source, "online-request.json");
            originalRequest = JsonUtility.ToJson(Request(Product, Transaction));
            File.WriteAllText(pending, originalRequest);
            File.WriteAllText(Path.Combine(source, "save.json"), "synthetic-production-save");
            File.WriteAllText(Path.Combine(source, "online-identity.json"), "synthetic-production-identity");
            try
            {
                var saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden);
                previousOverride = saveOverride.GetValue(null); saveOverride.SetValue(null, root); overrideInstalled = true;
                foreach (string name in new[] { "ProductsUpdated", "PurchaseSucceeded", "PurchaseFailed" })
                {
                    var field = T("Monetization.InAppPurchaseService").GetField(name, StaticHidden);
                    events.Add(field, field.GetValue(null)); field.SetValue(null, null);
                }
            }
            catch { Cleanup(); throw; }
        }
        [TearDown] public void TearDown() => Cleanup();
        private void Cleanup()
        {
            try
            {
                foreach (GameObject owner in owners) if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                owners.Clear();
                foreach (var entry in singletons) entry.Key.GetField("<Instance>k__BackingField", StaticHidden).SetValue(null, entry.Value);
                singletons.Clear();
                foreach (var entry in events) entry.Key.SetValue(null, entry.Value);
                events.Clear();
            }
            finally
            {
                if (overrideInstalled)
                {
                    T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden).SetValue(null, previousOverride);
                    overrideInstalled = false;
                }
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
        private Component Manager(string name)
        {
            Type type = T(name); var singleton = type.GetField("<Instance>k__BackingField", StaticHidden);
            singletons.Add(type, singleton.GetValue(null));
            var owner = new GameObject("IsolatedIapRoutingTest"); owner.SetActive(false); owners.Add(owner);
            Component component = owner.AddComponent(type); singleton.SetValue(null, component); return component;
        }
        private void InstallService()
        {
            Manager("Managers.SaveManager"); game = Manager("Managers.GameManager");
            productionProfile = Profile(PlayerId); game.GetType().GetProperty("PlayerProfile").SetValue(game, productionProfile);
            Manager("Save.OnlinePlayerData"); service = Manager("Monetization.InAppPurchaseService");
            SetHidden(service, "EditorDeliverySceneReadinessOverride", (Func<bool>)(() => true));
            SetHidden(service, "EditorVerifiedDeliveryOverride", (Action<string, string, Action<bool, string>>)((product, tx, done) =>
            { normalDeliveries++; done(false, "Synthetic normal delivery is held."); }));
            SetHidden(service, "EditorConfirmPurchaseOverride", (Action)(() => confirmationAttempts++));
            T("Monetization.InAppPurchaseService").GetEvent("PurchaseSucceeded").AddEventHandler(null,
                (Action<string, int>)((product, amount) => successes++));
        }
        private static object Request(string product, string tx)
        {
            var request = Activator.CreateInstance(T("Save.OnlineRequest"));
            SetPublic(request, "Kind", "purchase"); SetPublic(request, "Target", product);
            SetPublic(request, "RequestId", tx == Transaction ? RequestId : "apple-" + tx);
            SetPublic(request, "TransactionId", tx); SetPublic(request, "Receipt", "synthetic-receipt-never-send");
            return request;
        }
        private void Begin() => Storage("Begin", root, source, PlayerId, RequestId, Transaction, Product);
        private void Deliver()
        {
            Begin(); DeliverAfterExistingBegin();
        }
        private static bool Route(string product, string tx, out bool completed, out string message)
        {
            var args = new object[] { product, tx, false, null };
            bool handled = (bool)T("Monetization.InAppPurchaseService").GetMethod("TryHandleIsolatedDelivery", StaticHidden).Invoke(null, args);
            completed = (bool)args[2]; message = (string)args[3]; return handled;
        }
        private IEnumerator Routine(string product = Product, string tx = Transaction)
        {
            ((HashSet<string>)service.GetType().GetField("ordersBeingFulfilled", Hidden).GetValue(service)).Add(tx);
            object definition = Activator.CreateInstance(T("Monetization.IapProductDefinition"), product, 15000, "synthetic-price");
            return (IEnumerator)Invoke(service, "FulfillWhenPlayerProfileIsReady", null, definition, tx);
        }
        private void AssertProductionUntouched()
        {
            Assert.AreEqual(0, productionProfile.GetType().GetProperty("PaidGachaStones").GetValue(productionProfile));
            Assert.IsEmpty((IList)productionProfile.GetType().GetProperty("ProcessedIapTransactionIds").GetValue(productionProfile));
            Assert.IsFalse((bool)productionProfile.GetType().GetProperty("HasRemovedAds").GetValue(productionProfile));
            Assert.AreEqual("synthetic-production-save", File.ReadAllText(Path.Combine(source, "save.json")));
            Assert.AreEqual("synthetic-production-identity", File.ReadAllText(Path.Combine(source, "online-identity.json")));
            Assert.AreEqual(0, successes, "An isolated delivery is not a production purchase-success message.");
        }

        [Test] public void PendingJournalHoldsOrderWithoutNormalDeliveryOrConfirmation()
        {
            Begin(); InstallService(); Assert.IsTrue(Route(Product, Transaction, out bool completed, out string message));
            Assert.IsFalse(completed); Assert.IsNotEmpty(message);
            IEnumerator routine = Routine();
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.IsTrue(routine.MoveNext()); Assert.IsInstanceOf<WaitForSecondsRealtime>(routine.Current);
                Assert.AreEqual(0, normalDeliveries); Assert.AreEqual(0, confirmationAttempts);
                Assert.AreEqual(originalRequest, File.ReadAllText(pending)); AssertProductionUntouched();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        [Test] public void DurableIsolatedDeliveryConfirmsAndStoresTombstoneWithoutProductionGrant()
        {
            Deliver(); InstallService();
            SetHidden(service, "EditorConfirmPurchaseOverride", (Action)(() =>
            {
                confirmationAttempts++;
                Assert.IsFalse(File.Exists(pending));
                Assert.AreEqual(15000, Field(JsonUtility.FromJson(File.ReadAllText(IsolatedSavePath), T("Save.PlayerSaveData")), "PaidGachaStones"));
                AssertProductionUntouched(); Invoke(service, "HandleConfirmationResult", Transaction, false);
            }));
            IEnumerator routine = Routine();
            try { Assert.IsFalse(routine.MoveNext()); }
            finally { (routine as IDisposable)?.Dispose(); }
            Assert.AreEqual(1, confirmationAttempts); Assert.AreEqual(0, normalDeliveries);
            Assert.AreEqual("store_confirmed", Field(Journal(), "Phase"));
            Assert.IsTrue((bool)Storage("Matches", root, Product, Transaction)); AssertProductionUntouched();
        }
        [Test] public void ConfirmationFailureRetriesOnlyAcknowledgementNotNormalDeliveryOrGrant()
        {
            Deliver(); InstallService(); IEnumerator routine = Routine();
            try
            {
                Assert.IsTrue(routine.MoveNext()); Assert.IsNull(routine.Current); Assert.AreEqual(1, confirmationAttempts);
                Invoke(service, "HandleConfirmationResult", Transaction, true);
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending");
                Assert.IsTrue(routine.MoveNext()); Assert.IsInstanceOf<WaitForSecondsRealtime>(routine.Current);
                Assert.AreEqual("local_delivered", Field(Journal(), "Phase"));
                Assert.IsTrue(routine.MoveNext()); Assert.AreEqual(2, confirmationAttempts);
                Invoke(service, "HandleConfirmationResult", Transaction, false); Assert.IsFalse(routine.MoveNext());
                Assert.AreEqual("store_confirmed", Field(Journal(), "Phase")); Assert.AreEqual(0, normalDeliveries);
                Assert.AreEqual(2L, Field(JsonUtility.FromJson(File.ReadAllText(IsolatedSavePath), T("Save.PlayerSaveData")), "SaveRevision"));
                AssertProductionUntouched();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        [Test] public void ForeignActiveAccountStillRoutesExactOrderToIsolatedProof()
        {
            Deliver(); InstallService(); productionProfile = Profile(new string('c', 32));
            game.GetType().GetProperty("PlayerProfile").SetValue(game, productionProfile);
            Assert.IsTrue(Route(Product, Transaction, out bool completed, out string message));
            Assert.IsTrue(completed); Assert.IsNull(message); AssertProductionUntouched();
        }
        [Test] public void SameTransactionWithDifferentProductIsHeldNotRoutedNormally()
        {
            Begin(); InstallService();
            Assert.IsTrue(Route("com.nasus.dungeonmonsterroguelike.crystals650", Transaction, out bool completed, out string message));
            Assert.IsFalse(completed); Assert.IsNotEmpty(message); Assert.AreEqual(0, normalDeliveries); AssertProductionUntouched();
        }
        [TestCase(false)] [TestCase(true)]
        public void CorruptOrInterruptedRecordCannotUseNormalRoute(bool interrupted)
        {
            Begin(); InstallService(); File.WriteAllText(interrupted ? JournalPath + ".tmp" : JournalPath, "synthetic-incomplete-record");
            Assert.IsTrue(Route(Product, Transaction, out bool completed, out string message));
            Assert.IsFalse(completed); Assert.IsNotEmpty(message);
            IEnumerator routine = Routine();
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending"); Assert.IsTrue(routine.MoveNext());
                Assert.AreEqual(0, normalDeliveries); Assert.AreEqual(0, confirmationAttempts); AssertProductionUntouched();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        [TestCase(false)] [TestCase(true)]
        public void UnrelatedOrderUsesNormalRouteWhetherJournalIsAbsentOrDifferent(bool withJournal)
        {
            if (withJournal) Begin(); InstallService(); const string other = "synthetic-unrelated-order";
            Assert.IsFalse(Route(Product, other, out bool completed, out string message));
            Assert.IsFalse(completed); Assert.IsNull(message);
            IEnumerator routine = Routine(Product, other);
            try
            {
                LogAssert.Expect(LogType.Warning, "[IAP] Event=DeliveryRetryPending"); Assert.IsTrue(routine.MoveNext());
                Assert.AreEqual(1, normalDeliveries); Assert.AreEqual(0, confirmationAttempts); AssertProductionUntouched();
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        [Test] public void AcknowledgementForUntrackedOrderCannotPromoteJournal()
        {
            Deliver(); InstallService(); Invoke(service, "HandleConfirmationResult", Transaction, false);
            Assert.AreEqual("local_delivered", Field(Journal(), "Phase")); Assert.IsTrue(File.Exists(pending)); AssertProductionUntouched();
        }
        [Test] public void RunCorePendingGuardRecognizesArchivedRequestWithoutRewriteOrNetwork()
        {
            Begin(); var request = JsonUtility.FromJson(File.ReadAllText(pending), T("Save.OnlineRequest"));
            Assert.IsTrue((bool)Online("IsIsolatedPurchaseRequest", request));
            Assert.IsFalse((bool)Online("IsIsolatedPurchaseRequest", Request(Product, "synthetic-other-order")));
            Online("ResumeCompletedPurchaseIsolation");
            Assert.AreEqual(originalRequest, File.ReadAllText(pending));
            DeliverAfterExistingBegin(); Online("ResumeCompletedPurchaseIsolation");
            Assert.IsFalse(File.Exists(pending)); Assert.IsTrue((bool)Field(Journal(), "SourcePendingCleared"));
            Online("ResumeCompletedPurchaseIsolation");
            Assert.AreEqual("synthetic-production-save", File.ReadAllText(Path.Combine(source, "save.json")));
        }
        private void DeliverAfterExistingBegin()
        {
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            SetPublic(op, "Kind", "purchase"); SetPublic(op, "Target", Product); SetPublic(op, "RequestId", RequestId);
            SetPublic(op, "TransactionId", Transaction); SetPublic(op, "Revision", 1L); SetPublic(op, "Free", 900); SetPublic(op, "Paid", 15000);
            string endpoint = (string)T("Save.RefundSandboxConfiguration").GetField("Endpoint").GetRawConstantValue();
            Storage("MarkServerDelivered", root, endpoint, PlayerId, op); Storage("PersistLocalDelivery", root);
        }
        [Test] public void RunCoreResumeAndPendingGuardFailClosedForCorruptRecord()
        {
            Begin(); File.WriteAllText(JournalPath, "synthetic-corrupt-record");
            Assert.Throws<TargetInvocationException>(() => Online("ResumeCompletedPurchaseIsolation"));
            Assert.Throws<TargetInvocationException>(() => Online("IsIsolatedPurchaseRequest", Request(Product, Transaction)));
            Assert.AreEqual(originalRequest, File.ReadAllText(pending));
            Assert.AreEqual("synthetic-production-identity", File.ReadAllText(Path.Combine(source, "online-identity.json")));
        }
    }
}
