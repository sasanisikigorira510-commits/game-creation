using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class OnlinePlayerDataIntegrationTests
    {
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n).SetValue(o, v);
        private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n).Invoke(o, args);
        private static object Manager(string n) => T("Managers." + n).GetProperty("Instance").GetValue(null);
        private static Delegate Callback(Action<object, string> done)
        {
            var operation = Expression.Parameter(T("Save.OnlineOperation"));
            var error = Expression.Parameter(typeof(string));
            return Expression.Lambda(typeof(Action<,>).MakeGenericType(operation.Type, typeof(string)),
                Expression.Invoke(Expression.Constant(done), Expression.Convert(operation, typeof(object)), error), operation, error).Compile();
        }

        [Serializable] private sealed class AdminBody
        {
            public string Actor = "integration-test";
            public string Reason = "restore-test";
            public bool Frozen = true;
            public int Epoch;
            public int SnapshotId;
            public string PreviewToken;
        }
        [Serializable] private sealed class SnapshotRow { public int id; }
        [Serializable] private sealed class Inspection { public SnapshotRow[] Snapshots; }
        [Serializable] private sealed class Preview { public string PreviewToken; }
        private static string Admin(int port, string token, string path, AdminBody body = null)
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + path);
            request.Timeout = 5000;
            request.Headers["Authorization"] = "Bearer " + token;
            if (body != null)
            {
                request.Method = "POST";
                request.ContentType = "application/json";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
                using var stream = request.GetRequestStream(); stream.Write(bytes, 0, bytes.Length);
            }
            using var response = request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();
        }

        [UnityTest]
        public IEnumerator RealServerDrawRetryRestartAndOutagePreserveWalletAndInventory()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string directory = Path.Combine(Path.GetTempPath(), "WitchTowerOnline_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            saveOverride.SetValue(null, directory);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            string serverDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../PlayerDataServer"));
            var process = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo("/usr/bin/python3", "server.py --port " + port + " --data-dir " + Path.Combine(directory, "server")) {
                    WorkingDirectory = serverDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                }
            };
            try
            {
                process.Start();
                bool listening = false;
                float deadline = Time.realtimeSinceStartup + 15;
                while (!listening && Time.realtimeSinceStartup < deadline)
                {
                    try { using var client = new TcpClient(); client.Connect(IPAddress.Loopback, port); listening = true; }
                    catch (SocketException) { }
                    if (!listening) yield return null;
                }
                Assert.That(listening, Is.True, "Local fixture server did not start.");
                foreach (string n in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                    T("Core.ManagerFactory").GetMethod(n).Invoke(null, null);
                Call(Manager("MasterDataManager"), "Initialize");
                Call(Manager("SaveManager"), "LoadOrCreate");
                Call(Manager("GameManager"), "InitializeFromSave", P(Manager("SaveManager"), "CurrentSaveData"));
                object online = T("Save.OnlinePlayerData").GetMethod("Ensure").Invoke(null, null);
                online.GetType().GetField("baseUrl", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(online, "http://127.0.0.1:" + port);
                online.GetType().GetField("qaAccessKey", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(online, null);
                object request = Activator.CreateInstance(T("Save.OnlineRequest"));
                Set(request, "Kind", "gacha"); Set(request, "Count", 1); Set(request, "RequestId", "integration-same-request");
                object result = null; string error = null; bool finished = false;
                Delegate callback = Callback((op, message) => { result = op; error = message; finished = true; });
                Call(online, "Execute", request, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(error, Is.Null); Assert.That(result, Is.Not.Null);
                object profile = P(Manager("GameManager"), "PlayerProfile");
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                Assert.That(F(((IList)P(profile, "OwnedMonsters"))[0], "MonsterId"), Is.EqualTo("monster_dragon_whelp"));
                string committedId = (string)F(((IList)P(profile, "OwnedMonsters"))[0], "InstanceId");
                var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
                var diagnostics = online.GetType().GetField("connectionDiagnostics", hidden).GetValue(online);
                var entries = (IList)F(diagnostics, "Entries");
                Assert.That(entries.Cast<object>().Count(e => (string)F(e, "Route") == "accounts"), Is.EqualTo(1),
                    "A genuinely new player registers exactly once.");
                // More refreshes than the production registration quota. Even a
                // fresh client session must use authenticated head, not registration.
                online.GetType().GetField("registrationResolvedFor", hidden).SetValue(online, null);
                for (int refresh = 0; refresh < 24; refresh++)
                {
                    entries.Clear(); finished = false;
                    Call(online, "Execute", null, callback);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(finished, Is.True); Assert.That(error, Is.Null);
                    Assert.That(entries.Cast<object>().Select(e => (string)F(e, "Route")), Is.EqualTo(new[] { "head", "snapshots" }));
                }
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                // A different credential cannot re-register or spend this player.
                var credentialsField = online.GetType().GetField("credentials", hidden);
                object originalCredentials = credentialsField.GetValue(online);
                object rejectedCredentials = JsonUtility.FromJson(JsonUtility.ToJson(originalCredentials), originalCredentials.GetType());
                Set(rejectedCredentials, "Token", new string('f', 64));
                credentialsField.SetValue(online, rejectedCredentials);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    entries.Clear(); finished = false;
                    Call(online, "Execute", request, callback);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(finished, Is.True); Assert.That(error, Does.Contain("認証が無効"));
                    Assert.That(result, Is.Null);
                    Assert.That(entries.Cast<object>().Select(e => (string)F(e, "Route")),
                        Is.EqualTo(attempt == 0 ? new[] { "head", "accounts" } : new[] { "head" }));
                }
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                credentialsField.SetValue(online, originalCredentials);
                // Emulate restarting after the durable commit but before showing results.
                Call(Manager("SaveManager"), "LoadOrCreate");
                Call(Manager("GameManager"), "InitializeFromSave", P(Manager("SaveManager"), "CurrentSaveData"));
                finished = false; Call(online, "Execute", request, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(error, Is.Null);
                profile = P(Manager("GameManager"), "PlayerProfile");
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                Assert.That(F(((IList)P(profile, "OwnedMonsters"))[0], "InstanceId"), Is.EqualTo(committedId));
                // Register an operator restore at the same economy cursor, then
                // verify the client preserves unsynced progress before applying it.
                profile.GetType().GetProperty("Gold").SetValue(profile, 500);
                finished = false; Call(online, "Execute", null, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(error, Is.Null);
                string token = File.ReadAllText(Path.Combine(directory, "server/admin-token"));
                string adminPath = "/admin/players/" + P(profile, "PlayerId");
                var inspection = JsonUtility.FromJson<Inspection>(Admin(port, token, adminPath));
                var body = new AdminBody { SnapshotId = inspection.Snapshots[inspection.Snapshots.Length - 2].id };
                Admin(port, token, adminPath + "/freeze", body);
                body.PreviewToken = JsonUtility.FromJson<Preview>(Admin(port, token, adminPath + "/preview", body)).PreviewToken;
                Admin(port, token, adminPath + "/restore", body);
                profile.GetType().GetProperty("Gold").SetValue(profile, 600);
                finished = false; Call(online, "Execute", null, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(error, Is.Not.Null, "Must request recovery instead of uploading the old branch.");
                Assert.That(Call(online, "ApplyPendingRecovery"), Is.True);
                profile = P(Manager("GameManager"), "PlayerProfile");
                Assert.That(P(profile, "Gold"), Is.EqualTo(100));
                Assert.That(P(profile, "RecoveryEpoch"), Is.EqualTo(1));
                Assert.That(P(profile, "EconomyRevision"), Is.EqualTo(1L));
                bool preserved = Directory.GetFiles(Path.Combine(directory, "save.json.history"), "*.json").Any(file => {
                    var saved = JsonUtility.FromJson(File.ReadAllText(file), T("Save.PlayerSaveData"));
                    return (string)F(saved, "SaveReason") == "before_operator_restore" && (int)F(saved, "Gold") == 600;
                });
                Assert.That(preserved, Is.True, "Unsynced local progress must remain recoverable.");
                Admin(port, token, adminPath + "/freeze", new AdminBody { Frozen = false });
                process.Kill(); process.WaitForExit();
                Set(request, "RequestId", "outage-request"); finished = false;
                Call(online, "Execute", request, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(error, Is.Not.Null);
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                Assert.That(T("Save.OnlinePlayerData").GetProperty("LastMessage").GetValue(null), Does.Contain("オンライン接続"));
                // Restore the same isolated server and perform ONLY a refresh.
                // The old error must disappear without spending stones or drawing.
                process.Start();
                listening = false;
                deadline = Time.realtimeSinceStartup + 15;
                while (!listening && Time.realtimeSinceStartup < deadline)
                {
                    try { using var client = new TcpClient(); client.Connect(IPAddress.Loopback, port); listening = true; }
                    catch (SocketException) { }
                    if (!listening) yield return null;
                }
                Assert.That(listening, Is.True);
                finished = false; Call(online, "Execute", null, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(error, Is.Null);
                Assert.That(T("Save.OnlinePlayerData").GetProperty("LastMessage").GetValue(null), Is.Empty);
                profile = P(Manager("GameManager"), "PlayerProfile");
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                // The fixture intentionally has no store verifier: a real HTTP503
                // purchase must remain pending, not masquerade as no connectivity.
                object purchase = Activator.CreateInstance(T("Save.OnlineRequest"));
                Set(purchase, "Kind", "purchase");
                Set(purchase, "Target", "com.nasus.dungeonmonsterroguelike.crystals120");
                Set(purchase, "RequestId", "fixture-pending-purchase");
                Set(purchase, "TransactionId", "fixture-store-transaction");
                Set(purchase, "Receipt", "fixture-unverified-receipt");
                long revision = (long)P(profile, "EconomyRevision");
                int paid = (int)P(profile, "PaidGachaStones");
                entries.Clear(); finished = false; Call(online, "Execute", purchase, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(result, Is.Null);
                Assert.That(error, Does.Contain("購入の確認・反映"));
                Assert.That(error, Does.Not.Contain("オンライン接続が必要"));
                Assert.That(entries.Cast<object>().Select(e => (string)F(e, "Route")), Is.EqualTo(new[] { "head", "snapshots", "operations" }));
                Assert.That(F(entries[entries.Count - 1], "Status"), Is.EqualTo(503L));
                string pendingPath = Path.Combine((string)P(Manager("SaveManager"), "DataDirectory"), "online-request.json");
                string pending = File.ReadAllText(pendingPath);
                Assert.That(pending, Does.Contain("fixture-unverified-receipt"));
                online.GetType().GetMethod("DismissStatusNotification", hidden).Invoke(online, null);
                finished = false; Call(online, "Execute", null, callback);
                deadline = Time.realtimeSinceStartup + 20;
                while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(finished, Is.True); Assert.That(result, Is.Null);
                Assert.That(error, Does.Contain("購入の確認・反映"));
                Assert.That(T("Save.OnlinePlayerData").GetProperty("LastMessage").GetValue(null), Is.Empty);
                Assert.That(File.ReadAllText(pendingPath), Is.EqualTo(pending), "Dismissing must never remove or replace the durable purchase.");
                Assert.That(P(profile, "EconomyRevision"), Is.EqualTo(revision));
                Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(paid));
                Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(600));
                Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
                Assert.That(Call(profile, "HasProcessedIapTransaction", "fixture-store-transaction"), Is.False);
            }
            finally
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                process.Dispose();
                saveOverride.SetValue(null, null);
                foreach (string name in new[] { "OnlinePlayerData", "SaveManager", "GameManager", "MasterDataManager" })
                {
                    GameObject owner = GameObject.Find(name);
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                }
                Directory.Delete(directory, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
