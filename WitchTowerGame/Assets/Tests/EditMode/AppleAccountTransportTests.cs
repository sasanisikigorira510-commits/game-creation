using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class AppleAccountTransportTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object F(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
        private static object Manager(string name) => T("Managers." + name).GetProperty("Instance").GetValue(null);
        private static FieldInfo EditorField(string type, string field) => T(type).GetField(field, BindingFlags.Static | BindingFlags.NonPublic);
        private static Delegate NativeFixture(Type type, bool includeCode = true)
        {
            var nonce = Expression.Parameter(typeof(string)); var state = Expression.Parameter(typeof(string));
            var done = Expression.Parameter(type.GenericTypeArguments[2]);
            Action<string, string, object> body = (n, s, cb) =>
            {
                var response = Activator.CreateInstance(T("Save.AppleNativeIdentity+Result"));
                Set(response, "State", s); Set(response, "IdentityToken", "synthetic-unity:" + n);
                Set(response, "AuthorizationCode", includeCode ? "synthetic-code" : null);
                ((Delegate)cb).DynamicInvoke(response);
            };
            return Expression.Lambda(type, Expression.Invoke(Expression.Constant(body), nonce, state,
                Expression.Convert(done, typeof(object))), nonce, state, done).Compile();
        }
        private static IEnumerator Wait(object online)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)F(online, "busy") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse((bool)F(online, "busy"), "Apple flow timed out.");
        }
        [UnityTest]
        public IEnumerator LinkGuestRecoverOtherDeviceOverRealHttpPreservesOriginals()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string root = Path.Combine(Path.GetTempPath(), "NasusAppleTransport_" + Guid.NewGuid().ToString("N"));
            string original = Path.Combine(root, "old-device"), replacement = Path.Combine(root, "new-device");
            Directory.CreateDirectory(original); Directory.CreateDirectory(replacement);
            var saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            var enabled = EditorField("Save.OnlinePlayerData", "EditorAppleAccountEnabled");
            var switched = EditorField("Save.OnlinePlayerData", "EditorAccountSwitchCompleted");
            var native = EditorField("Save.AppleNativeIdentity", "EditorAuthorizeOverride");
            var cleanup = EditorField("Save.OnlinePlayerData", "EditorDeletionPreferencesCleanup");
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            string serverRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../PlayerDataServer"));
            var process = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(
                Path.Combine(serverRoot, ".venv-production/bin/python"), "tests/apple_unity_fixture.py " + port) {
                WorkingDirectory = serverRoot,
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
            try
            {
                process.Start();
                bool listening = false; float deadline = Time.realtimeSinceStartup + 15;
                while (!listening && Time.realtimeSinceStartup < deadline)
                {
                    try { using var client = new TcpClient(); client.Connect(IPAddress.Loopback, port); listening = true; }
                    catch (SocketException) { }
                    if (!listening) yield return null;
                }
                Assert.IsTrue(listening);
                saveOverride.SetValue(null, original); enabled.SetValue(null, true);
                bool prefsCleared = false;
                cleanup.SetValue(null, (Action)(() => prefsCleared = true));
                native.SetValue(null, NativeFixture(native.FieldType));
                bool didSwitch = false; switched.SetValue(null, (Action)(() => didSwitch = true));
                foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                    T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
                Call(Manager("MasterDataManager"), "Initialize");
                Call(Manager("SaveManager"), "LoadOrCreate");
                Call(Manager("GameManager"), "InitializeFromSave", P(Manager("SaveManager"), "CurrentSaveData"));
                var online = T("Save.OnlinePlayerData").GetMethod("Ensure").Invoke(null, null);
                Set(online, "baseUrl", "http://127.0.0.1:" + port);
                Call(online, "OpenAppleAccount", false);
                native.SetValue(null, NativeFixture(native.FieldType, false));
                Call(online, "AppleFlow", Call(online, "AppleSignIn", false));
                yield return Wait(online);
                StringAssert.Contains("認証を確認できません", (string)F(online, "accountMessage"));
                native.SetValue(null, NativeFixture(native.FieldType));
                Call(online, "AppleFlow", Call(online, "AppleSignIn", false));
                yield return Wait(online);
                StringAssert.Contains("連携が完了", (string)F(online, "accountMessage"));
                string player = (string)F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId");
                byte[] originalSave = File.ReadAllBytes(Path.Combine(original, "save.json"));
                // Simulate reinstall/new device with a DIFFERENT existing guest.
                // A running manager's startup context cannot switch roots. A
                // new installation has fresh storage/transport owners instead.
                var originalSaves = (Component)Manager("SaveManager");
                object originalStorageContext = F(originalSaves, "storageContext");
                UnityEngine.Object.DestroyImmediate(((Component)online).gameObject);
                UnityEngine.Object.DestroyImmediate(originalSaves.gameObject);
                saveOverride.SetValue(null, replacement);
                T("Core.ManagerFactory").GetMethod("EnsureSaveManager").Invoke(null, null);
                Assert.AreNotSame(originalSaves, Manager("SaveManager"));
                Call(Manager("SaveManager"), "LoadOrCreate");
                Call(Manager("GameManager"), "InitializeFromSave", P(Manager("SaveManager"), "CurrentSaveData"));
                Assert.AreEqual(replacement, P(Manager("SaveManager"), "RootDirectory"));
                Assert.AreNotSame(originalStorageContext, F(Manager("SaveManager"), "storageContext"));
                online = T("Save.OnlinePlayerData").GetMethod("Ensure").Invoke(null, null);
                Set(online, "baseUrl", "http://127.0.0.1:" + port);
                Assert.AreSame(Manager("SaveManager"), F(online, "configuredStorageOwner"));
                Call(online, "OpenAppleAccount", false);
                string guest = (string)F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId");
                Assert.AreNotEqual(player, guest);
                byte[] guestSave = File.ReadAllBytes(Path.Combine(replacement, "save.json"));
                Call(online, "AppleFlow", Call(online, "AppleSignIn", true));
                yield return Wait(online);
                var journal = F(online, "accountJournal");
                Assert.NotNull(journal);
                Assert.AreEqual("started", F(journal, "Phase"), "Fixture deliberately loses the first verified response.");
                Call(online, "AppleFlow", Call(online, "AppleVerifyRetry"));
                yield return Wait(online);
                journal = F(online, "accountJournal");
                Assert.AreEqual("preview", F(journal, "Phase"), (string)F(online, "accountMessage"));
                Assert.AreEqual(guest, F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId"));
                Call(online, "AppleFlow", Call(online, "AppleCommit"));
                yield return Wait(online);
                Assert.IsTrue(didSwitch, (string)F(online, "accountMessage"));
                Assert.AreEqual(player, F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId"));
                Assert.AreEqual(1, F(P(Manager("SaveManager"), "CurrentSaveData"), "RecoveryEpoch"));
                Assert.AreEqual(600, F(P(Manager("SaveManager"), "CurrentSaveData"), "FreeGachaStones"));
                Assert.AreEqual(120, F(P(Manager("SaveManager"), "CurrentSaveData"), "PaidGachaStones"));
                var monsters = (IList)F(P(Manager("SaveManager"), "CurrentSaveData"), "OwnedMonsters");
                Assert.AreEqual(1, monsters.Count);
                Assert.AreEqual("monster_dragon_whelp", F(monsters[0], "MonsterId"));
                Assert.AreEqual(2L, F(P(Manager("SaveManager"), "CurrentSaveData"), "EconomyRevision"));
                CollectionAssert.AreEqual(originalSave, File.ReadAllBytes(Path.Combine(original, "save.json")));
                CollectionAssert.AreEqual(guestSave, File.ReadAllBytes(Path.Combine(replacement, "save.json")));
                Assert.IsFalse(File.Exists(Path.Combine(replacement, "apple-recovery.json")));
                Assert.IsTrue(File.Exists(Path.Combine(replacement, "active-account.json")));
                Call(online, "OpenAppleAccount", false);
                Call(online, "AppleFlow", Call(online, "AppleUnlinkPreview"));
                yield return Wait(online);
                Assert.NotNull(F(online, "unlinkReview"), (string)F(online, "accountMessage"));
                StringAssert.Contains("削除ではありません", (string)F(online, "accountMessage"));
                // Preview alone cannot mutate the game; commit is a separate action.
                Assert.AreEqual(120, F(P(Manager("SaveManager"), "CurrentSaveData"), "PaidGachaStones"));
                Call(online, "AppleFlow", Call(online, "AppleUnlinkCommit"));
                yield return Wait(online);
                Assert.IsNull(F(online, "unlinkReview"), (string)F(online, "accountMessage"));
                StringAssert.Contains("連携を解除しました", (string)F(online, "accountMessage"));
                Assert.AreEqual(player, F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId"));
                Assert.AreEqual(600, F(P(Manager("SaveManager"), "CurrentSaveData"), "FreeGachaStones"));
                Assert.AreEqual(120, F(P(Manager("SaveManager"), "CurrentSaveData"), "PaidGachaStones"));
                Assert.AreEqual(1, ((IList)F(P(Manager("SaveManager"), "CurrentSaveData"), "OwnedMonsters")).Count);
                Call(online, "AppleFlow", Call(online, "AppleUnlinkPreview"));
                yield return Wait(online);
                Assert.IsNull(F(online, "unlinkReview"));
                StringAssert.Contains("連携されていません", (string)F(online, "accountMessage"));
                // Crash just after journaling but before HTTP: status does not
                // cancel. A server-confirmed cancellation safely resumes play.
                Call(online, "AppleFlow", Call(online, "DeletionPreview"));
                yield return Wait(online);
                var reviewToCancel = F(online, "deletionReview");
                Assert.NotNull(reviewToCancel, (string)F(online, "accountMessage"));
                var credential = F(online, "credentials");
                T("Save.AccountDeletionStorage").GetMethod("Begin").Invoke(null, new object[] {
                    replacement, player, F(credential, "Token"), F(reviewToCancel, "ConfirmationToken"), F(online, "baseUrl") });
                Set(online, "deletionReview", null);
                Call(Manager("SaveManager"), "LoadOrCreate");
                Call(online, "AppleFlow", Call(online, "DeletionRequest", "status"));
                yield return Wait(online);
                Assert.IsTrue((bool)F(online, "deletionMayRetry"));
                Assert.IsNull(P(Manager("SaveManager"), "CurrentSaveData"));
                Call(online, "AppleFlow", Call(online, "DeletionRequest", "cancel"));
                yield return Wait(online);
                Assert.IsNull(F(online, "deletionJournal"));
                Assert.AreEqual(player, F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId"));
                Assert.IsFalse(File.Exists(Path.Combine(replacement, "account-deletion.json")));
                Call(online, "OpenAppleAccount", false);
                Call(online, "AppleFlow", Call(online, "DeletionPreview"));
                yield return Wait(online);
                Assert.NotNull(F(online, "deletionReview"), (string)F(online, "accountMessage"));
                StringAssert.Contains("元に戻せません", (string)F(online, "accountMessage"));
                StringAssert.Contains("この削除操作では", (string)F(online, "accountMessage"));
                StringAssert.Contains("直接消去されません", (string)F(online, "accountMessage"));
                StringAssert.Contains("確認用記録は別に保持", (string)F(online, "accountMessage"));
                StringAssert.DoesNotContain("【開発検証】", (string)F(online, "accountMessage"));
                StringAssert.DoesNotContain("消去は未対応", (string)F(online, "accountMessage"));
                string activeDirectory = (string)P(Manager("SaveManager"), "DataDirectory");
                Call(online, "AppleFlow", Call(online, "DeletionSubmit"));
                yield return Wait(online);
                Assert.AreEqual("requested", F(F(online, "deletionJournal"), "Phase"), "Fixture loses the committed response.");
                Assert.IsTrue(File.Exists(Path.Combine(activeDirectory, "save.json")), "Do not erase before confirmation.");
                Assert.IsNull(P(Manager("SaveManager"), "CurrentSaveData"));
                Assert.IsNull(P(Manager("GameManager"), "PlayerProfile"));
                Assert.IsTrue((bool)T("Save.OnlinePlayerData").GetProperty("Busy").GetValue(null));
                Assert.IsFalse((bool)T("Save.OnlinePlayerData").GetProperty("CanStartPurchase").GetValue(null));
                // Reload durable state as after a restart; no in-memory secret is required.
                Set(online, "deletionJournal", null); Set(online, "credentials", null);
                Call(Manager("SaveManager"), "LoadOrCreate");
                Assert.IsTrue((bool)P(Manager("SaveManager"), "RecoveryRequired"));
                Set(online, "baseUrl", "https://example.invalid");
                Call(online, "AppleFlow", Call(online, "DeletionRequest", "status"));
                yield return Wait(online);
                StringAssert.Contains("認証情報は送信せず", (string)F(online, "accountMessage"));
                Assert.AreEqual("requested", F(F(online, "deletionJournal"), "Phase"));
                Set(online, "baseUrl", "http://127.0.0.1:" + port);
                Call(online, "AppleFlow", Call(online, "DeletionRequest", "status"));
                yield return Wait(online);
                Assert.AreEqual("completed", F(F(online, "deletionJournal"), "Phase"), (string)F(online, "accountMessage"));
                StringAssert.Contains("直接消去されません", (string)F(online, "accountMessage"));
                StringAssert.Contains("確認用記録は別に保持", (string)F(online, "accountMessage"));
                StringAssert.DoesNotContain("消去は未対応", (string)F(online, "accountMessage"));
                Assert.IsTrue(prefsCleared);
                Assert.IsEmpty(Directory.GetFiles(activeDirectory, "*", SearchOption.AllDirectories));
                CollectionAssert.AreEqual(guestSave, File.ReadAllBytes(Path.Combine(replacement, "save.json")));
                CollectionAssert.AreEqual(originalSave, File.ReadAllBytes(Path.Combine(original, "save.json")), "Other physical device is not erased remotely.");
                Call(Manager("SaveManager"), "LoadOrCreate");
                Assert.IsNull(P(Manager("SaveManager"), "CurrentSaveData"), "Never auto-load the retained root guest.");
                Call(online, "DeletionNewGuest");
                string fresh = (string)F(P(Manager("SaveManager"), "CurrentSaveData"), "PlayerId");
                Assert.AreNotEqual(player, fresh); Assert.AreNotEqual(guest, fresh);
                Assert.IsFalse((bool)P(Manager("SaveManager"), "RecoveryRequired"));
                Assert.AreEqual(0, F(P(Manager("SaveManager"), "CurrentSaveData"), "PaidGachaStones"));
                foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                {
                    string contents = File.ReadAllText(file);
                    StringAssert.DoesNotContain("synthetic-code", contents);
                    StringAssert.DoesNotContain("synthetic-unity:", contents);
                    StringAssert.DoesNotContain("synthetic-refresh-", contents);
                }
            }
            finally
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                process.Dispose();
                enabled.SetValue(null, false); native.SetValue(null, null); switched.SetValue(null, null);
                cleanup.SetValue(null, null);
                saveOverride.SetValue(null, null);
                foreach (string name in new[] { "OnlinePlayerData", "SaveManager", "GameManager", "MasterDataManager" })
                { var go = GameObject.Find(name); if (go != null) UnityEngine.Object.DestroyImmediate(go); }
                Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
