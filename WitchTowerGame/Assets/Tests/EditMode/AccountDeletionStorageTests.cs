using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AccountDeletionStorageTests
    {
        private string root;
        private readonly string player = new string('a', 32), token = new string('b', 64), confirmation = new string('c', 64);
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object value) => o.GetType().GetField(n).SetValue(o, value);
        private object Call(string name, params object[] args) => T("Save.AccountDeletionStorage").GetMethod(name).Invoke(null, args);
        private string Journal => Path.Combine(root, "account-deletion.json");
        private void Seed(string directory, string id, string credential)
        {
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "save.json.history"));
            string save = "{\"PlayerId\":\"" + id + "\",\"PlayerLevel\":1,\"SaveRevision\":1,\"SchemaVersion\":3}";
            File.WriteAllText(Path.Combine(directory, "save.json"), save);
            File.WriteAllText(Path.Combine(directory, "save.json.bak"), save);
            File.WriteAllText(Path.Combine(directory, "save.json.history", "00000000000000000001.json"), save);
            File.WriteAllText(Path.Combine(directory, "online-identity.json"), "{\"PlayerId\":\"" + id + "\",\"Token\":\"" + credential + "\"}");
            File.WriteAllText(Path.Combine(directory, "online-request.json"), "{\"Receipt\":\"synthetic-private-receipt\"}");
        }
        private object Begin() => Call("Begin", root, player, token, confirmation, "https://api.example.com");
        private object Result(string status, string id = null)
        {
            object value = Activator.CreateInstance(T("Save.AccountDeletionResult"));
            Set(value, "Status", status); Set(value, "PlayerId", id ?? player);
            return value;
        }
        private void Confirm() => Call("Confirm", root, Result("deleted"));
        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "NasusDeletion_" + Guid.NewGuid().ToString("N"));
            Seed(root, player, token);
        }
        [TearDown] public void TearDown() { Directory.Delete(root, true); }

        [Test] public void RequestedJournalIsDurableBeforeAnyErasure()
        {
            Begin();
            Assert.AreEqual("requested", Field(Call("Pending", root), "Phase"));
            Assert.IsTrue((bool)Call("Blocks", root));
            Assert.Throws<TargetInvocationException>(() => Call("Erase", root));
            Assert.IsTrue(File.Exists(Path.Combine(root, "save.json")));
        }
        [Test] public void BadServerConfirmationNeverErases()
        {
            Begin();
            Assert.Throws<TargetInvocationException>(() => Call("Confirm", root, Result("not_deleted")));
            Assert.Throws<TargetInvocationException>(() => Call("Confirm", root, Result("deleted", new string('d', 32))));
            Assert.AreEqual("requested", Field(Call("Pending", root), "Phase"));
        }
        [Test] public void ConfirmationCleanupAndRestartRemoveSecretsButKeepCompletedBarrier()
        {
            Begin(); Confirm(); Call("Erase", root); Call("Finish", root);
            Assert.IsFalse(File.Exists(Path.Combine(root, "save.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "online-identity.json")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(root, "save.json.history")));
            Assert.AreEqual("completed", Field(Call("Pending", root), "Phase"));
            Assert.IsTrue((bool)Call("Blocks", root));
            string text = File.ReadAllText(Journal);
            foreach (string secret in new[] { player, token, confirmation, "private-receipt" }) StringAssert.DoesNotContain(secret, text);
        }
        [Test] public void PartialCleanupCanResumeWithoutSourceIdentity()
        {
            Begin(); Confirm();
            File.Delete(Path.Combine(root, "online-identity.json"));
            File.Delete(Path.Combine(root, "save.json"));
            Call("Erase", root); Call("Erase", root); Call("Finish", root);
            Assert.AreEqual("completed", Field(Call("Pending", root), "Phase"));
        }
        [Test] public void ChangedFilePausesBeforeDeletingAnyOtherFile()
        {
            Begin(); Confirm();
            File.AppendAllText(Path.Combine(root, "save.json"), " ");
            Assert.Throws<TargetInvocationException>(() => Call("Erase", root));
            Assert.IsTrue(File.Exists(Path.Combine(root, "online-identity.json")));
            Assert.AreEqual("confirmed", Field(Call("Pending", root), "Phase"));
        }
        [Test] public void NewHistoryAfterConfirmationPausesCleanup()
        {
            Begin(); Confirm();
            File.Copy(Path.Combine(root, "save.json"), Path.Combine(root, "save.json.history", "00000000000000000002.json"));
            Assert.Throws<TargetInvocationException>(() => Call("Erase", root));
            Assert.IsTrue(File.Exists(Path.Combine(root, "save.json")));
        }
        [Test] public void FinishCannotSkipErasure()
        {
            Begin(); Confirm();
            Assert.Throws<TargetInvocationException>(() => Call("Finish", root));
        }
        [Test] public void OnlyServerCancellationCanUnblockRequestedDeletion()
        {
            Begin();
            Assert.Throws<TargetInvocationException>(() => Call("Cancel", root, Result("not_deleted")));
            Assert.Throws<TargetInvocationException>(() => Call("Cancel", root, Result("cancelled", new string('d', 32))));
            Call("Cancel", root, Result("cancelled"));
            Assert.IsFalse((bool)Call("Blocks", root));
            Assert.IsTrue(File.Exists(Path.Combine(root, "save.json")));
        }
        [Test] public void ConfirmedDeletionCannotBeCancelled()
        {
            Begin(); Confirm();
            Assert.Throws<TargetInvocationException>(() => Call("Cancel", root, Result("cancelled")));
        }
        [Test] public void CorruptAndTempOnlyJournalsFailClosed()
        {
            File.WriteAllText(Journal + ".tmp", "{");
            Assert.IsTrue((bool)Call("Blocks", root));
            Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
            Assert.Throws<TargetInvocationException>(() => Begin());
            File.WriteAllText(Journal, "{}");
            Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
        }
        [Test] public void JournalCannotTargetArbitraryFileOrPathTraversal()
        {
            var journal = Begin();
            var files = (Array)Field(journal, "Files");
            foreach (string bad in new[] { "../save.json", "personal.txt", "/tmp/save.json", "linked-accounts/../../save.json" })
            {
                Set(files.GetValue(0), "Path", bad);
                File.WriteAllText(Journal, JsonUtility.ToJson(journal));
                Assert.Throws<TargetInvocationException>(() => Call("Pending", root));
            }
            Assert.IsTrue(File.Exists(Path.Combine(root, "save.json")));
        }
        [Test] public void MixedPlayerHistoryIsPreservedAndRequestNeverStarts()
        {
            File.WriteAllText(Path.Combine(root, "save.json.history", "legacy-original.json"), "{\"PlayerId\":\"" + new string('d', 32) + "\"}");
            Assert.Throws<TargetInvocationException>(() => Begin());
            Assert.IsFalse(File.Exists(Journal));
        }
        [Test] public void MismatchedCredentialNeverCreatesJournal()
        {
            Assert.Throws<TargetInvocationException>(() => Call("Begin", root, player, new string('e', 64), confirmation, "https://api.example.com"));
            Assert.IsFalse(File.Exists(Journal));
        }
        [Test] public void UnsafeEndpointNeverCreatesJournal()
        {
            foreach (string endpoint in new[] { "http://api.example.com", "https://user:password@api.example.com", "https://api.example.com?secret=x" })
                Assert.Throws<TargetInvocationException>(() => Call("Begin", root, player, token, confirmation, endpoint));
        }
        [Test] public void EveryOwnedSlotIsErasedButOtherGuestAndUnrelatedFilesRemain()
        {
            string owned = Path.Combine(root, "linked-accounts", new string('1', 32));
            string other = Path.Combine(root, "linked-accounts", new string('2', 32));
            Seed(owned, player, new string('e', 64)); Seed(other, new string('d', 32), new string('f', 64));
            File.WriteAllText(Path.Combine(root, "preferences.txt"), "unrelated");
            byte[] before = File.ReadAllBytes(Path.Combine(other, "save.json"));
            Begin(); Confirm(); Call("Erase", root); Call("Finish", root);
            Assert.IsEmpty(Directory.GetFiles(owned, "*", SearchOption.AllDirectories));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(other, "save.json")));
            Assert.AreEqual("unrelated", File.ReadAllText(Path.Combine(root, "preferences.txt")));
        }
        [Test] public void LinkedActiveDeletionNeverFallsBackToRetainedRootGuest()
        {
            string other = new string('d', 32), slot = new string('1', 32);
            Seed(root, other, new string('e', 64));
            Seed(Path.Combine(root, "linked-accounts", slot), player, token);
            File.WriteAllText(Path.Combine(root, "active-account.json"), "{\"SlotId\":\"" + slot + "\",\"PlayerId\":\"" + player + "\"}");
            byte[] original = File.ReadAllBytes(Path.Combine(root, "save.json"));
            Begin(); Confirm(); Call("Erase", root); Call("Finish", root);
            Assert.IsTrue((bool)Call("Blocks", root));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(root, "save.json")));
            Call("StartNewGuest", root);
            Assert.IsFalse((bool)Call("Blocks", root));
            string active = (string)T("Save.AppleRecoveryStorage").GetMethod("ActiveDirectory").Invoke(null, new object[] { root });
            Assert.AreNotEqual(root, active);
            string identity = File.ReadAllText(Path.Combine(active, "online-identity.json"));
            StringAssert.DoesNotContain(player, identity); StringAssert.DoesNotContain(other, identity);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(root, "save.json")));
        }
        [Test] public void FreshGuestCannotBeStartedDuringUncertainDeletion()
        {
            Begin(); Assert.Throws<TargetInvocationException>(() => Call("StartNewGuest", root));
        }
        [Test] public void SaveManagerAndRecoveryAreBlockedAcrossRestart()
        {
            Begin();
            var managerType = T("Managers.SaveManager");
            var directory = managerType.GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            directory.SetValue(null, root);
            var go = new GameObject("DeletionSaveTest");
            var singleton = managerType.GetProperty("Instance");
            object originalInstance = singleton.GetValue(null);
            try
            {
                var manager = go.AddComponent(managerType);
                singleton.SetValue(null, manager); // EditMode does not invoke Awake.
                managerType.GetMethod("LoadOrCreate").Invoke(manager, null);
                Assert.IsTrue((bool)managerType.GetProperty("RecoveryRequired").GetValue(manager));
                Assert.IsNull(managerType.GetProperty("CurrentSaveData").GetValue(manager));
                var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                object[] args = { save, "gameplay", null };
                Assert.IsFalse((bool)managerType.GetMethod("TrySaveWithReason").Invoke(manager, args));
                Assert.Throws<TargetInvocationException>(() => T("Save.AppleRecoveryStorage").GetMethod("Begin").Invoke(null, new object[] { root }));
                Assert.IsTrue((bool)T("Save.OnlinePlayerData").GetProperty("Busy").GetValue(null));
                Assert.IsFalse((bool)T("Save.OnlinePlayerData").GetProperty("CanStartPurchase").GetValue(null));
            }
            finally { singleton.SetValue(null, originalInstance); UnityEngine.Object.DestroyImmediate(go); directory.SetValue(null, null); }
        }
        [Test] public void LinkedSaveFileIsRejectedBeforeRequestStarts()
        {
            string outside = Path.Combine(root, "unrelated.txt");
            File.WriteAllText(outside, "do-not-delete");
            string path = Path.Combine(root, "save.json.bak");
            File.Delete(path);
            using (var process = System.Diagnostics.Process.Start("/bin/ln", "-s \"" + outside + "\" \"" + path + "\""))
            { process.WaitForExit(); Assert.AreEqual(0, process.ExitCode); }
            Assert.Throws<TargetInvocationException>(() => Begin());
            Assert.AreEqual("do-not-delete", File.ReadAllText(outside));
            File.Delete(path);
        }
        [Test] public void ServerConfirmedCancellationRestoresSavedAccountOnReload()
        {
            Begin(); Call("Cancel", root, Result("cancelled"));
            var managerType = T("Managers.SaveManager");
            var directory = managerType.GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            directory.SetValue(null, root);
            var go = new GameObject("DeletionCancelReloadTest");
            try
            {
                var manager = go.AddComponent(managerType);
                managerType.GetMethod("LoadOrCreate").Invoke(manager, null);
                Assert.IsFalse((bool)managerType.GetProperty("RecoveryRequired").GetValue(manager));
                Assert.AreEqual(player, Field(managerType.GetProperty("CurrentSaveData").GetValue(manager), "PlayerId"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); directory.SetValue(null, null); }
        }
    }
}
