using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class GachaConnectionStateTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private Type online, panel;
        private GameObject owner, saveOwner;
        private Component service;
        private object previous, previousSave, previousOverride, previousRootOverride, profile;
        private string previousMessage;
        private PropertyInfo saveRootOverride;
        private bool globalsCaptured;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static FieldInfo Singleton(Type type) => type.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        private void Set(string name, object value) => online.GetField(name, Hidden).SetValue(service, value);
        private object Call(Type type, string name, params object[] args) => type.GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private string Message => (string)online.GetProperty("ContractUnavailableMessage").GetValue(null);

        [SetUp]
        public void Setup()
        {
            try
            {
                online = T("Save.OnlinePlayerData");
                panel = T("Home.GachaPanelController");
                Type saveManager = T("Managers.SaveManager");
                previous = Singleton(online).GetValue(null);
                previousMessage = (string)online.GetProperty("LastMessage").GetValue(null);
                previousSave = Singleton(saveManager).GetValue(null);
                previousOverride = online.GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                saveRootOverride = saveManager.GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
                previousRootOverride = saveRootOverride.GetValue(null);
                globalsCaptured = true;
                online.GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
                // This unique fixture path is never created, loaded or saved.
                // No state under Application.persistentDataPath is inspected.
                string root = Path.Combine(Path.GetTempPath(), "WitchTower-GachaConnection-" + Guid.NewGuid().ToString("N"));
                saveRootOverride.SetValue(null, root);
                saveOwner = new GameObject("IsolatedGachaConnectionSave");
                saveOwner.SetActive(false);
                Component saves = saveOwner.AddComponent(saveManager);
                Singleton(saveManager).SetValue(null, saves);
                Assert.That(saveManager.GetProperty("RootDirectory").GetValue(saves), Is.EqualTo(root));
                Assert.That(saveManager.GetProperty("StorageAccessAvailable").GetValue(saves), Is.True);

                owner = new GameObject("OfflineConnectionTest");
                owner.SetActive(false); // No Awake, network calls, save loading, or writes.
                service = owner.AddComponent(online);
                Singleton(online).SetValue(null, service);
                Set("configuredStorageOwner", saves);
                Set("baseUrl", "http://127.0.0.1:1"); // Synthetic only; no transport entrypoint runs.
                Set("credentials", Activator.CreateInstance(online.GetNestedType("Credentials", BindingFlags.NonPublic)));
                var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                foreach (var entry in new[] { ("FreeGachaStones", 1200), ("PaidGachaStones", 9600), ("MonsterStorageLimit", 380) })
                    save.GetType().GetField(entry.Item1).SetValue(save, entry.Item2);
                save.GetType().GetField("HasCompletedTutorial").SetValue(save, true);
                saveManager.GetProperty("CurrentSaveData").SetValue(saves, save);
                profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            }
            catch { Cleanup(); throw; }
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (saveOwner != null) UnityEngine.Object.DestroyImmediate(saveOwner);
            }
            finally
            {
                owner = saveOwner = null;
                service = null;
                profile = null;
                if (globalsCaptured)
                {
                    try
                    {
                        Singleton(online).SetValue(null, previous);
                        Singleton(T("Managers.SaveManager")).SetValue(null, previousSave);
                        online.GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, previousOverride);
                        online.GetProperty("LastMessage").SetValue(null, previousMessage);
                    }
                    finally
                    {
                        saveRootOverride.SetValue(null, previousRootOverride);
                        globalsCaptured = false;
                    }
                }
            }
        }

        private void Complete(string error, bool delivered = false) => online.GetMethod("CompleteSyncStatus", Hidden)
            .Invoke(service, new object[] { error, delivered });

        private void CompleteOperation(string kind, string error = null)
        {
            object operation = Activator.CreateInstance(T("Save.OnlineOperation"));
            operation.GetType().GetField("Kind").SetValue(operation, kind);
            online.GetMethod("CompleteOperationStatus", Hidden).Invoke(service, new[] { error, operation });
        }

        [TestCase("reward")] [TestCase("ad_reward")]
        [TestCase("purchase")] [TestCase("upgrade")]
        public void SuccessfulQuietDeliveryClearsRecoveredConnectionError(string kind)
        {
            Complete("接続失敗");
            CompleteOperation(kind);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
            Assert.That(Message, Is.Empty);
            // Unrelated important notices are not dismissed by successful delivery.
            online.GetProperty("LastMessage").SetValue(null, "復旧データをご確認ください。");
            CompleteOperation(kind);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("復旧データをご確認ください。"));
        }

        [TestCase("reward")] [TestCase("ad_reward")]
        [TestCase("purchase")] [TestCase("upgrade")]
        public void QuietDeliveryStillShowsSaveErrorsAndRefundWarnings(string kind)
        {
            CompleteOperation(kind, "端末への保存を再試行する必要があります。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("保存"));
            Set("refundDebt", 300);
            CompleteOperation(kind);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("300"));
        }

        [TestCase("purchase")] [TestCase("upgrade")]
        public void SuccessfulPurchaseDoesNotShowGenericNoticeAndClearsEarlierSuccessNotice(string kind)
        {
            online.GetProperty("LastMessage").SetValue(null, string.Empty);
            CompleteOperation(kind);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
            CompleteOperation("gacha");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("データを反映しました。"));
            CompleteOperation(kind);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
        }

        [TestCase(true)] [TestCase(false)]
        public void QuestClaimCallbackDoesNotCreateSuccessNoticeButStillReportsFailure(bool success)
        {
            bool? claimed = null;
            online.GetProperty("LastMessage").SetValue(null, string.Empty);
            var execute = online.GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static);
            execute.SetValue(null, new Action<string, Action<string, string>>((json, callback) =>
            {
                Assert.That(json, Does.Contain("\"Kind\":\"reward\""));
                callback(success ? "{\"Kind\":\"reward\"}" : null, success ? null : "通信状態をご確認ください。");
            }));
            online.GetMethod("ClaimReward").Invoke(null, new object[] { "daily_battle_win_1", new Action<bool>(ok => claimed = ok) });
            Assert.That(claimed, Is.EqualTo(success));
            if (success) Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
            else Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("通信状態をご確認ください。"));
        }

        [Test]
        public void NonRewardDeliveryKeepsItsExistingNotice()
        {
            CompleteOperation("gacha");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("データを反映しました。"));
        }

        [Test]
        public void SuccessfulNoTransactionRefreshClearsOldErrorAndRestoresReadiness()
        {
            Complete("オンライン接続が必要です。通信状態をご確認ください。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("オンライン接続"));
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.False);
            Complete(null);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo(string.Empty));
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.True);
        }

        [Test]
        public void RefreshPreservesUnrelatedNoticeAndDeliveryStillShowsItsResult()
        {
            Complete("接続失敗");
            online.GetProperty("LastMessage").SetValue(null, "報酬を受け取りました。");
            Complete(null);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("報酬を受け取りました。"));
            Complete(null, true);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("データを反映しました。"));
        }

        [Test]
        public void ReconnectHintSchedulesCheckButNeverClearsAuthenticationFailure()
        {
            Complete("この端末の認証が無効です。");
            float due = Time.realtimeSinceStartup + 3;
            Set("nextReconnectAllowedAt", due);
            online.GetMethod("RequestConnectionRefresh").Invoke(service, null);
            Assert.That((float)online.GetField("nextSync", Hidden).GetValue(service), Is.EqualTo(due).Within(.1));
            Assert.That(Message, Does.Contain("認証が無効"));
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.False);
            Set("busy", true);
            Set("nextSync", 999999f);
            online.GetMethod("RequestConnectionRefresh").Invoke(service, null);
            Assert.That(online.GetField("nextSync", Hidden).GetValue(service), Is.EqualTo(999999f));
        }

        [TestCase(401, "認証が無効")]
        [TestCase(403, "許可されていません")]
        [TestCase(409, "残高")]
        [TestCase(423, "確認待ち")]
        [TestCase(429, "回数制限")]
        [TestCase(0, "オンライン接続")]
        [TestCase(503, "サーバー")]
        public void FailedConnectionNeverShowsReadyOrEnablesAnyPull(long code, string expected)
        {
            string error = (string)Call(online, "ConnectionFailureMessage", code, null);
            Set("contractUnavailableMessage", error);
            Assert.That(Message, Does.Contain(expected));
            Assert.That(Call(panel, "BuildRuntimePreviewStatusText", profile), Is.EqualTo(error));
            foreach (int count in new[] { 1, 10 })
                foreach (bool paid in new[] { false, true })
                    Assert.That(Call(panel, "CanRequestRuntimeContract", profile, count, paid), Is.False);
        }

        [TestCase(null, "購入の確認・反映")]
        [TestCase("{}", "購入の確認・反映")]
        [TestCase("not-json", "購入の確認・反映")]
        [TestCase("{\"ErrorCode\":\"PURCHASE_ENVIRONMENT_MISMATCH\"}", "環境が一致しません")]
        [TestCase("{\"ErrorCode\":\"PURCHASE_VERIFICATION_REJECTED\"}", "購入情報を確認できず")]
        [TestCase("{\"ErrorCode\":\"PURCHASE_VERIFICATION_PENDING\"}", "購入の確認・反映")]
        public void Purchase503IsAProtectedPendingPurchaseNotAnOfflineNotice(string body, string expected)
        {
            string error = (string)Call(online, "OperationFailureMessage", 503L,
                "/v1/players/fixture/operations", body, "purchase");
            Assert.That(error, Does.Contain(expected));
            Assert.That(error, Does.Contain("再購入"));
            Assert.That(error, Does.Not.Contain("オンライン接続が必要"));
            Complete(error);
            Assert.That(Message, Is.EqualTo(error));
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, false), Is.False,
                "Correcting or dismissing a notice must never bypass the failed transaction.");
        }

        [TestCase(0L, "/v1/players/fixture/operations", "purchase", "オンライン接続")]
        [TestCase(503L, "/v1/players/fixture/head?after=0", "purchase", "サーバー")]
        [TestCase(503L, "/v1/players/fixture/operations", "gacha", "サーバー")]
        [TestCase(503L, "/v1/apple/verify", "purchase", "サーバー")]
        [TestCase(401L, "/v1/players/fixture/operations", "purchase", "認証が無効")]
        [TestCase(429L, "/v1/players/fixture/operations", "purchase", "回数制限")]
        public void PurchaseErrorCodesCannotChangeOtherHttpOrOperationFailures(long code, string path, string kind, string expected)
        {
            string error = (string)Call(online, "OperationFailureMessage", code, path,
                "{\"ErrorCode\":\"PURCHASE_ENVIRONMENT_MISMATCH\"}", kind);
            Assert.That(error, Does.Contain(expected));
            Assert.That(error, Does.Not.Contain("環境が一致しません"));
        }

        [Test]
        public void PurchaseNoticeNeverDisplaysArbitraryServerBodyOrOversizedJson()
        {
            foreach (string body in new[] { "{\"Error\":\"private-receipt-canary\",\"ErrorCode\":\"private-token-canary\"}",
                "{\"ErrorCode\":\"PURCHASE_ENVIRONMENT_MISMATCH\",\"Error\":\"" + new string('x', 5000) + "\"}" })
            {
                string error = (string)Call(online, "OperationFailureMessage", 503L,
                    "/v1/players/fixture/operations", body, "purchase");
                Assert.That(error, Does.Contain("購入の確認・反映"));
                Assert.That(error, Does.Not.Contain("private-"));
                Assert.That(error.Length, Is.LessThan(200));
            }
        }

        [Test]
        public void DismissedRetryRemainsQuietWithoutChangingReadinessOrProfile()
        {
            Set("failureNoticeContext", "purchase-fixture-a");
            Complete("購入の反映を保留しています。");
            string before = JsonUtility.ToJson(profile);
            online.GetMethod("DismissStatusNotification", Hidden).Invoke(service, null);
            for (int i = 0; i < 3; i++) Complete("購入の反映を保留しています。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
            Assert.That(Message, Does.Contain("保留"));
            Assert.That(online.GetProperty("CanStartPurchase").GetValue(null), Is.False);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, false), Is.False);
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before));
            Set("failureNoticeContext", "purchase-fixture-b");
            Complete("購入の反映を保留しています。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("保留"),
                "A distinct transaction must not inherit the previous dismissal.");
        }

        [Test]
        public void NewFailureAccountOrRecoveryResetsDismissalBoundary()
        {
            Complete("接続失敗");
            online.GetMethod("DismissStatusNotification", Hidden).Invoke(service, null);
            Complete("認証が無効です。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("認証"));
            online.GetMethod("DismissStatusNotification", Hidden).Invoke(service, null);
            var credentials = online.GetField("credentials", Hidden).GetValue(service);
            credentials.GetType().GetField("PlayerId").SetValue(credentials, "different-fixture-player");
            Complete("認証が無効です。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("認証"));
            online.GetMethod("DismissStatusNotification", Hidden).Invoke(service, null);
            Complete(null);
            Complete("認証が無効です。");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("認証"),
                "A fresh outage after successful sync is a new incident.");
        }

        [Test]
        public void RepeatedDismissedFailureDoesNotReplaceUnrelatedNotice()
        {
            Complete("接続失敗");
            online.GetMethod("DismissStatusNotification", Hidden).Invoke(service, null);
            online.GetProperty("LastMessage").SetValue(null, "復旧データをご確認ください。");
            Complete("接続失敗");
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.EqualTo("復旧データをご確認ください。"));
        }

        [Test]
        public void RefundDeficitBlocksPaidOnlyAndClearsItsNoticeAfterResolution()
        {
            Set("contractUnavailableMessage", string.Empty);
            Set("refundDebt", 300);
            Complete(null);
            Assert.That(Message, Is.Empty, "A refund deficit is not a connection error.");
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, false), Is.True);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.False);
            Assert.That(Call(panel, "BuildRuntimePreviewStatusText", profile), Does.Contain("返金"));
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Does.Contain("300"));
            Set("refundDebt", 0);
            Complete(null);
            Assert.That(online.GetProperty("LastMessage").GetValue(null), Is.Empty);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.True);
        }

        [Test]
        public void RefundOperationUpdatesWalletWithoutRemovingMonstersOrGold()
        {
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("PaidGachaStones").SetValue(save, 650);
            var monsters = (System.Collections.IList)save.GetType().GetField("OwnedMonsters").GetValue(save);
            var monster = Activator.CreateInstance(monsters.GetType().GetGenericArguments()[0]);
            monsters.Add(monster);
            string monsterBefore = JsonUtility.ToJson(monster);
            string before = JsonUtility.ToJson(save);
            var operation = Activator.CreateInstance(T("Save.OnlineOperation"));
            foreach (var item in new[] { ("Kind", (object)"refund"), ("Revision", (object)1L),
                ("Free", (object)900), ("Paid", (object)0), ("RefundDebt", (object)300) })
                operation.GetType().GetField(item.Item1).SetValue(operation, item.Item2);
            var staged = T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { save, operation });
            Assert.That(staged.GetType().GetField("PaidGachaStones").GetValue(staged), Is.EqualTo(0));
            Assert.That(staged.GetType().GetField("Gold").GetValue(staged), Is.EqualTo(save.GetType().GetField("Gold").GetValue(save)));
            var retained = (System.Collections.IList)staged.GetType().GetField("OwnedMonsters").GetValue(staged);
            Assert.That(retained.Count, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(retained[0]), Is.EqualTo(monsterBefore));
            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(before), "Never mutate the live source before persistence.");
            var repeated = T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { staged, operation });
            Assert.That(JsonUtility.ToJson(repeated), Is.EqualTo(JsonUtility.ToJson(staged)));
        }

        [Test]
        public void RegistrationCredentialRejectionIsDistinguishedFromOtherForbiddenRequests()
        {
            Assert.That(Call(online, "ConnectionFailureMessage", 403L, "/v1/accounts"), Does.Contain("認証が無効"));
            Assert.That(Call(online, "ConnectionFailureMessage", 403L, "/v1/apple/verify"), Does.Contain("許可されていません"));
        }

        [TestCase(null, 60)]
        [TestCase("60", 60)]
        [TestCase("120", 120)]
        [TestCase("-1", 1)]
        [TestCase("99999", 3600)]
        [TestCase("invalid", 60)]
        public void RateLimitDelayIsBounded(string header, float expected)
        {
            Assert.That(Call(online, "RetryDelaySeconds", header), Is.EqualTo(expected));
        }

        [Test]
        public void FocusAndScreenRefreshCannotShortenServerRateLimitWait()
        {
            float due = Time.realtimeSinceStartup + 60;
            Set("retryNotBefore", due);
            Complete((string)Call(online, "ConnectionFailureMessage", 429L, null));
            online.GetMethod("RequestConnectionRefresh").Invoke(service, null);
            Assert.That((float)online.GetField("nextSync", Hidden).GetValue(service), Is.GreaterThanOrEqualTo(due));
            Assert.That(online.GetMethod("Prepare", Hidden).Invoke(service, null), Is.False);
            Assert.That(online.GetField("failure", Hidden).GetValue(service), Does.Contain("回数制限"));
        }

        [Test]
        public void CheckingBlocksThenSuccessfulSyncRestoresAffordablePulls()
        {
            Assert.That(Message, Does.Contain("確認中"));
            Set("contractUnavailableMessage", string.Empty);
            Set("busy", true);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.False);
            Set("busy", false);
            Assert.That(Call(panel, "BuildRuntimePreviewStatusText", profile), Is.EqualTo("契約可能"));
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, false), Is.True);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 10, false), Is.False);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 10, true), Is.True);
            Set("credentials", null);
            Assert.That(Call(panel, "CanRequestRuntimeContract", profile, 1, true), Is.False,
                "Switching accounts must not retain the previous session's readiness.");
        }
    }
}
