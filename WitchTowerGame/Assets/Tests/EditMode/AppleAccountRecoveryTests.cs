using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AppleAccountRecoveryTests
    {
        private string root;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(string method, params object[] args) => T("Save.AppleRecoveryStorage").GetMethod(method).Invoke(null, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static object Clone(object obj) => JsonUtility.FromJson(JsonUtility.ToJson(obj), obj.GetType());
        private object Bundle()
        {
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            Set(save, "PlayerId", new string('a', 32)); Set(save, "RecoveryEpoch", 1);
            Set(save, "EconomyRevision", 0L); Set(save, "SaveRevision", 5L);
            Set(save, "FreeGachaStones", 900); Set(save, "PaidGachaStones", 0);
            var bundle = Activator.CreateInstance(T("Save.AppleRecoveryBundle"));
            Set(bundle, "Status", "preview"); Set(bundle, "PlayerId", new string('a', 32));
            Set(bundle, "PreviewToken", new string('b', 64)); Set(bundle, "Epoch", 1);
            Set(bundle, "Free", 900); Set(bundle, "Paid", 0); Set(bundle, "EconomyRevision", 0L);
            Set(bundle, "Snapshot", save); Set(bundle, "Operations", Array.CreateInstance(T("Save.OnlineOperation"), 0));
            return bundle;
        }
        private object Preview()
        {
            var journal = Call("Begin", root);
            Call("SetChallenge", root, journal, new string('c', 64));
            Call("Stage", root, journal, Bundle());
            return journal;
        }
        private void Confirm(object journal)
        {
            Call("MarkRequested", root, journal);
            var confirmed = Clone(Field(journal, "Bundle")); Set(confirmed, "Status", "committed");
            Call("MarkCommitted", root, journal, confirmed);
        }
        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "NasusAppleRecovery_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "save.json"), "original-guest-save");
            File.WriteAllText(Path.Combine(root, "online-identity.json"), "original-guest-identity");
        }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        [Test] public void UnlinkedGuestUsesExistingDirectory()
        { Assert.AreEqual(root, Call("ActiveDirectory", root)); Assert.IsNull(Call("Pending", root)); }

        [Test] public void RestartDuringSignInHasNoRecoveryPreviewAndCanCancelWithoutChangingSave()
        {
            var started = Call("Begin", root);
            Call("SetChallenge", root, started, new string('c', 64));
            string path = (string)Call("JournalPath", root);
            byte[] before = File.ReadAllBytes(path);
            var reloaded = Call("Pending", root);
            Assert.AreEqual("started", Field(reloaded, "Phase"));
            Assert.IsNull(Field(reloaded, "Bundle"), "Unity deserializes a null nested Bundle as an empty object; it is not a preview.");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "Reading must not rewrite the journal.");
            Call("Cancel", root, reloaded);
            Assert.IsNull(Call("Pending", root));
            Assert.AreEqual(root, Call("ActiveDirectory", root));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
            Assert.AreEqual("original-guest-identity", File.ReadAllText(Path.Combine(root, "online-identity.json")));
        }

        private static string Summary(object journal) => (string)T("Save.OnlinePlayerData")
            .GetMethod("AppleRecoverySummary", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { journal });

        [Test] public void StartedJournalNeverDisplaysAnEmptyOrStalePreview()
        {
            var started = Call("Begin", root);
            Assert.IsNull(Summary(started));
            Assert.IsNull(Summary(Clone(started)), "Restart must not interpret an empty nested object as a preview.");
            Set(started, "Bundle", Bundle());
            Assert.IsNull(Summary(started), "The durable phase, not object presence, determines whether a preview exists.");
        }

        [TestCase(null)] [TestCase("")] [TestCase("short")]
        public void RecoverySummaryNeverThrowsForIncompleteIdentity(string playerId)
        {
            var journal = Preview();
            Set(Field(journal, "Bundle"), "PlayerId", playerId);
            Assert.IsNull(Summary(journal));
        }

        [Test] public void RecoverySummaryRequiresSnapshotAndRetainsValidPreviewOnRestart()
        {
            var journal = Preview();
            Assert.AreEqual("復旧先：aaaaaaaa…\nプレイヤーLv 1\n無償石 900 / 有償石 0", Summary(Call("Pending", root)));
            Set(Field(journal, "Bundle"), "Snapshot", null);
            Assert.IsNull(Summary(journal));
        }

        [Test] public void PreviewDoesNotReplaceOriginalAndCancelPreservesBoth()
        {
            var journal = Preview(); string slot = Path.Combine(root, "linked-accounts", (string)Field(journal, "SlotId"));
            Assert.AreEqual(root, Call("ActiveDirectory", root));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
            Call("Cancel", root, journal);
            Assert.IsNull(Call("Pending", root)); Assert.IsTrue(File.Exists(Path.Combine(slot, "save.json")));
            Assert.AreEqual("original-guest-identity", File.ReadAllText(Path.Combine(root, "online-identity.json")));
        }
        [Test] public void UnconfirmedRecoveryCannotActivateOrDiscardSubmittedRequest()
        {
            var journal = Preview();
            Assert.Throws<TargetInvocationException>(() => Call("Activate", root, journal));
            Call("MarkRequested", root, journal);
            Assert.Throws<TargetInvocationException>(() => Call("Cancel", root, journal));
            Assert.Throws<TargetInvocationException>(() => Call("Activate", root, journal));
            Assert.AreEqual("requested", Field(Call("Pending", root), "Phase"));
        }
        [Test] public void ConfirmThenRestartActivatesCompletePairAndPreservesOldSave()
        {
            var journal = Preview(); Confirm(journal);
            var reloaded = Call("Pending", root);
            Call("Activate", root, reloaded);
            string active = (string)Call("ActiveDirectory", root);
            StringAssert.Contains("linked-accounts", active);
            StringAssert.Contains((string)Field(journal, "Token"), File.ReadAllText(Path.Combine(active, "online-identity.json")));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
            Assert.IsNull(Call("Pending", root));
        }
        [Test] public void RepeatedActivationAfterPointerWriteIsIdempotent()
        {
            var journal = Preview(); Confirm(journal);
            string durable = File.ReadAllText((string)Call("JournalPath", root));
            Call("Activate", root, journal);
            string first = (string)Call("ActiveDirectory", root);
            // Crash after pointer rename but before journal cleanup.
            File.WriteAllText((string)Call("JournalPath", root), durable);
            Call("Activate", root, Call("Pending", root));
            Assert.AreEqual(first, Call("ActiveDirectory", root));
        }
        [Test] public void MismatchedConfirmationCannotActivate()
        {
            var journal = Preview(); Call("MarkRequested", root, journal);
            var confirmed = Clone(Field(journal, "Bundle")); Set(confirmed, "Status", "committed"); Set(confirmed, "Paid", 999);
            Assert.Throws<TargetInvocationException>(() => Call("MarkCommitted", root, journal, confirmed));
            Assert.AreEqual(root, Call("ActiveDirectory", root));
        }
        [Test] public void MissingStagedIdentityStopsBeforeServerCommit()
        {
            var journal = Preview(); string slot = Path.Combine(root, "linked-accounts", (string)Field(journal, "SlotId"));
            File.Delete(Path.Combine(slot, "online-identity.json"));
            Assert.Throws<TargetInvocationException>(() => Call("MarkRequested", root, journal));
            Assert.AreEqual("preview", Field(Call("Pending", root), "Phase"));
        }
        [Test] public void CorruptPointerNeverFallsBackToGuestOrCreatesSave()
        {
            File.WriteAllText(Path.Combine(root, "active-account.json"), "{\"SlotId\":\"../../escape\",\"PlayerId\":\"" + new string('a', 32) + "\"}");
            Assert.Throws<TargetInvocationException>(() => Call("ActiveDirectory", root));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
        }
        [Test] public void MissingActiveSaveNeverFallsBackToGuest()
        {
            var journal = Preview(); Confirm(journal); Call("Activate", root, journal);
            string active = (string)Call("ActiveDirectory", root);
            File.Delete(Path.Combine(active, "save.json"));
            Assert.Throws<TargetInvocationException>(() => Call("ActiveDirectory", root));
        }
        [Test] public void InvalidJournalBlocksNewTransfer()
        {
            File.WriteAllText((string)Call("JournalPath", root), "{}");
            Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
            Assert.Throws<TargetInvocationException>(() => Call("Begin", root));
        }
        [TestCase(false)] [TestCase(true)]
        public void InterruptedJournalWriteIsNeverTreatedAsNoPendingOperation(bool mainExists)
        {
            if (mainExists) Call("Begin", root);
            string path = (string)Call("JournalPath", root);
            File.WriteAllText(path + ".tmp", "{\"Phase\":\"requested\"");
            byte[] pending = File.ReadAllBytes(path + ".tmp");
            Assert.IsTrue((bool)Call("Blocks", root));
            Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
            Assert.Throws<TargetInvocationException>(() => Call("Begin", root));
            CollectionAssert.AreEqual(pending, File.ReadAllBytes(path + ".tmp"));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
            Assert.AreEqual("original-guest-identity", File.ReadAllText(Path.Combine(root, "online-identity.json")));
        }

        [TestCase("preview")] [TestCase("requested")] [TestCase("committed")]
        public void RestartAtEachValidatedRecoveryPhaseKeepsPreviewAndOriginalSave(string phase)
        {
            var journal = Preview();
            if (phase == "requested") Call("MarkRequested", root, journal);
            if (phase == "committed") Confirm(journal);
            var reloaded = Call("Pending", root);
            Assert.AreEqual(phase, Field(reloaded, "Phase"));
            Assert.IsNotNull(Summary(reloaded));
            Assert.AreEqual("original-guest-save", File.ReadAllText(Path.Combine(root, "save.json")));
            Assert.AreEqual("original-guest-identity", File.ReadAllText(Path.Combine(root, "online-identity.json")));
        }
        [Test] public void TransferSecretsAreRandomAndNeverUseTheOldCredential()
        {
            string a = (string)Call("NewCredential"), b = (string)Call("NewCredential");
            Assert.AreEqual(64, a.Length); Assert.IsTrue(a.All(c => "0123456789abcdef".Contains(c)));
            Assert.AreNotEqual(a, b);
        }
        [Test] public void ReplayUsesExactCommittedTailWithoutMutatingOriginal()
        {
            var bundle = Bundle();
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "Revision", 1L); Set(op, "Free", 900); Set(op, "Paid", 120);
            Set(op, "Kind", "purchase"); Set(op, "TransactionId", "synthetic-purchase");
            var ops = Array.CreateInstance(op.GetType(), 1); ops.SetValue(op, 0);
            Set(bundle, "Operations", ops); Set(bundle, "Paid", 120); Set(bundle, "EconomyRevision", 1L);
            var first = Call("Replay", bundle); var second = Call("Replay", bundle);
            Assert.AreEqual(JsonUtility.ToJson(first), JsonUtility.ToJson(second));
            Assert.AreEqual(0, Field(Field(bundle, "Snapshot"), "PaidGachaStones"));
            Assert.AreEqual(120, Field(first, "PaidGachaStones"));
            Assert.AreEqual(true, Field(first, "HasRemovedAds"), "Account recovery must restore the first-purchase benefit.");
            Assert.AreEqual(false, Field(Field(bundle, "Snapshot"), "HasRemovedAds"), "Do not change the snapshot before replay succeeds.");
        }
        [Test] public void MissingTailDuplicateTailAndWalletMismatchAreRejected()
        {
            var bundle = Bundle(); Set(bundle, "EconomyRevision", 1L);
            Assert.Throws<TargetInvocationException>(() => Call("Replay", bundle));
            bundle = Bundle(); Set(bundle, "Paid", 5);
            Assert.Throws<TargetInvocationException>(() => Call("Replay", bundle));
            bundle = Bundle(); Set(Field(bundle, "Snapshot"), "RecoveryEpoch", 0);
            Assert.Throws<TargetInvocationException>(() => Call("Replay", bundle));
        }
        [Test] public void PublicConfigurationEnablesApprovedAccountUIWithoutQaSettings()
        {
            var asset = Resources.Load<TextAsset>("PlayerDataService");
            StringAssert.Contains("\"ReleaseAppleLinking\": true", asset.text);
            StringAssert.DoesNotContain("QaAccessKey", asset.text);
            StringAssert.DoesNotContain("ExperimentalAppleLinking", asset.text);
            Assert.IsTrue((bool)T("Save.OnlinePlayerData").GetProperty("AppleAccountEnabled").GetValue(null));
        }
        [Test] public void MissingPreviewInSubmittedJournalFailsClosed()
        {
            var journal = Preview(); Call("MarkRequested", root, journal);
            Set(journal, "Bundle", null);
            File.WriteAllText((string)Call("JournalPath", root), JsonUtility.ToJson(journal));
            Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
        }
        [Test] public void WrongActivePlayerCannotBeExposedToGameplay()
        {
            var journal = Preview(); Confirm(journal); Call("Activate", root, journal);
            var other = Field(Bundle(), "Snapshot"); Set(other, "PlayerId", new string('d', 32));
            Assert.Throws<TargetInvocationException>(() => Call("ValidateActivePlayer", root, other));
        }
        [Test] public void SettingsAccountButtonDoesNotOverlapExistingControls()
        {
            var enabled = T("Save.OnlinePlayerData").GetField("EditorAppleAccountEnabled", BindingFlags.Static | BindingFlags.NonPublic);
            var go = new GameObject("AppleSettingsLayout", typeof(RectTransform));
            go.SetActive(false); // Avoid running unrelated ExecuteAlways preview setup.
            enabled.SetValue(null, true);
            try
            {
                var home = go.AddComponent(T("Home.HomeSceneController"));
                home.GetType().GetMethod("BuildAudioSettingsPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(home, new object[] { go.transform });
                var panel = go.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "AudioSettingsPanel");
                var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Assert.IsTrue(buttons.Any(b => b.name == "AppleAccountButton"));
                var rects = buttons.Select(b => {
                    var r = (RectTransform)b.transform;
                    var center = r.anchoredPosition + Vector2.Scale(r.anchorMin - panel.pivot, panel.rect.size);
                    return new Rect(center - r.sizeDelta * .5f, r.sizeDelta);
                }).ToArray();
                for (int i = 0; i < rects.Length; i++)
                {
                    Assert.GreaterOrEqual(rects[i].height, 64);
                    Assert.IsTrue(panel.rect.Contains(rects[i].min)); Assert.IsTrue(panel.rect.Contains(rects[i].max));
                    for (int j = i + 1; j < rects.Length; j++) Assert.IsFalse(rects[i].Overlaps(rects[j]), "Settings buttons overlap.");
                }
            }
            finally { enabled.SetValue(null, false); UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void SaveManagerBlocksPendingTransferAndLoadsCommittedAccount()
        {
            var managerType = T("Managers.SaveManager");
            var saveOverride = managerType.GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            saveOverride.SetValue(null, root);
            var go = new GameObject("RecoverySaveTest");
            try
            {
                var manager = go.AddComponent(managerType);
                var journal = Preview();
                managerType.GetMethod("LoadOrCreate").Invoke(manager, null);
                Assert.IsTrue((bool)managerType.GetProperty("RecoveryRequired").GetValue(manager));
                Confirm(journal);
                managerType.GetMethod("LoadOrCreate").Invoke(manager, null);
                Assert.IsFalse((bool)managerType.GetProperty("RecoveryRequired").GetValue(manager));
                Assert.AreEqual(new string('a', 32), Field(managerType.GetProperty("CurrentSaveData").GetValue(manager), "PlayerId"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); saveOverride.SetValue(null, null); }
        }
    }
}
