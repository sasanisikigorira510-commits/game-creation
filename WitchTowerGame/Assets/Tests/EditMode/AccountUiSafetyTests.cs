using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class AccountUiSafetyTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Save." + name, true);
        private static object F(object obj, string name) => obj.GetType().GetField(name, Private | BindingFlags.Public).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private | BindingFlags.Public).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        private static string DeletionSummary(object review) => (string)T("OnlinePlayerData")
            .GetMethod("DeletionReviewSummary", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { review });

        // UI reads require a live, fixed storage owner, even when no transport is
        // used. Capture only this fixture's root; never load an existing save.
        private sealed class StorageProbe : IDisposable
        {
            private readonly Type saveType = T("PlayerSaveData").Assembly.GetType("WitchTower.Managers.SaveManager", true);
            private readonly FieldInfo saveInstance, onlineInstance;
            private readonly PropertyInfo rootOverride;
            private readonly object previousSave, previousOnline, previousRoot;
            private GameObject owner;
            private bool disposed;

            public StorageProbe(string root, Component online)
            {
                saveInstance = saveType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                onlineInstance = T("OnlinePlayerData").GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                rootOverride = saveType.GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
                previousSave = saveInstance.GetValue(null);
                previousOnline = onlineInstance.GetValue(null);
                previousRoot = rootOverride.GetValue(null);
                try
                {
                    rootOverride.SetValue(null, root);
                    owner = new GameObject("InactiveAccountStorageProbe"); owner.SetActive(false);
                    var saves = owner.AddComponent(saveType);
                    saveInstance.SetValue(null, saves);
                    Assert.That(saveType.GetProperty("RootDirectory").GetValue(saves), Is.EqualTo(root));
                    Assert.That(saveType.GetProperty("StorageAccessAvailable").GetValue(saves), Is.True);
                    saveType.GetProperty("CurrentSaveData").SetValue(saves, T("PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null));
                    onlineInstance.SetValue(null, online);
                    Set(online, "configuredStorageOwner", saves);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
                finally
                {
                    try
                    {
                        saveInstance.SetValue(null, previousSave);
                        onlineInstance.SetValue(null, previousOnline);
                    }
                    finally { rootOverride.SetValue(null, previousRoot); }
                }
            }
        }

        private static object Review()
        {
            var review = Activator.CreateInstance(T("AccountDeletionResult"));
            Set(review, "Status", "confirm_delete"); Set(review, "PlayerId", new string('a', 32));
            Set(review, "ConfirmationToken", new string('b', 64)); Set(review, "Free", 900); Set(review, "Paid", 120);
            return review;
        }

        [TestCase(null)] [TestCase("")] [TestCase("abc")]
        [TestCase("gggggggggggggggggggggggggggggggg")]
        public void DeletionDisplayRejectsIncompleteIdentityWithoutThrowing(string id)
        {
            var review = Review(); Set(review, "PlayerId", id);
            Assert.IsNull(DeletionSummary(review));
        }

        [TestCase("Status", "deleted")] [TestCase("ConfirmationToken", "short")]
        [TestCase("ConfirmationToken", null)] [TestCase("Free", -1)] [TestCase("Paid", -1)]
        public void IncompleteDeletionReviewCannotBecomeAConfirmation(string field, object value)
        {
            var review = Review(); Set(review, field, value);
            Assert.IsNull(DeletionSummary(review));
        }

        [Test]
        public void ValidDeletionDisplayKeepsAmountsAndOnlyAnIdPrefix()
        {
            Assert.AreEqual("削除対象：aaaaaaaa…\nサーバー所持：無償石 900 / 有償石 120", DeletionSummary(Review()));
            Assert.IsNull(DeletionSummary(null));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void CompletedDeletionReloadKeepsCopyLimitsAndRevocationStatus(bool pending, bool manual)
        {
            string root = Path.Combine(Path.GetTempPath(), "NasusDeletionNotice_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var owner = new GameObject("InactiveDeletionNoticeProbe"); owner.SetActive(false);
            StorageProbe storage = null;
            try
            {
                string path = Path.Combine(root, "account-deletion.json");
                string record = "{\"Phase\":\"completed\",\"Files\":[],\"RevocationPending\":" +
                    (pending ? "true" : "false") + ",\"ManualRevocationRequired\":" + (manual ? "true" : "false") + "}";
                File.WriteAllText(path, record);
                var online = owner.AddComponent(T("OnlinePlayerData"));
                storage = new StorageProbe(root, online);
                Call(online, "ReloadAccountRecords", root);
                Assert.IsFalse((bool)F(online, "accountRecordInvalid"));
                string message = (string)F(online, "accountMessage");
                StringAssert.Contains("削除は完了", message);
                StringAssert.Contains("他端末・手動コピー", message);
                StringAssert.Contains("直接消去されません", message);
                StringAssert.Contains("確認用記録は別に保持", message);
                StringAssert.DoesNotContain("【開発検証】", message);
                StringAssert.DoesNotContain("未対応", message);
                StringAssert.DoesNotContain("30日", message);
                if (manual) StringAssert.Contains("追加確認が必要", message);
                else if (pending) StringAssert.Contains("処理待ち", message);
                else StringAssert.DoesNotContain("Apple側", message);
                Assert.AreEqual(record, File.ReadAllText(path), "Displaying a result must not rewrite the record.");
            }
            finally
            {
                try { UnityEngine.Object.DestroyImmediate(owner); }
                finally
                {
                    try { storage?.Dispose(); }
                    finally { Directory.Delete(root, true); } // Only this test-created temporary directory.
                }
            }
        }

        [TestCase("recovery")] [TestCase("recovery-temp")] [TestCase("deletion")]
        public void FailedRecordReadClearsStaleActionsButPreservesAllFiles(string kind)
        {
            string root = Path.Combine(Path.GetTempPath(), "NasusAccountUi_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var owner = new GameObject("InactiveAccountRecordProbe"); owner.SetActive(false);
            StorageProbe storage = null;
            try
            {
                var online = owner.AddComponent(T("OnlinePlayerData"));
                storage = new StorageProbe(root, online);
                Set(online, "accountJournal", Activator.CreateInstance(T("AppleRecoveryJournal")));
                Set(online, "deletionJournal", Activator.CreateInstance(T("AccountDeletionJournal")));
                Set(online, "deletionReview", Review());
                Set(online, "unlinkReview", Activator.CreateInstance(T("OnlinePlayerData+AppleUnlinkResult")));
                Set(online, "deletionMayRetry", true);
                string name = kind == "deletion" ? "account-deletion.json" :
                    kind == "recovery-temp" ? "apple-recovery.json.tmp" : "apple-recovery.json";
                string path = Path.Combine(root, name);
                File.WriteAllText(path, "{");
                File.WriteAllText(Path.Combine(root, "save.json"), "untouched-save");
                Call(online, "ReloadAccountRecords", root);
                Assert.IsTrue((bool)F(online, "accountRecordInvalid"));
                foreach (string field in new[] { "accountJournal", "deletionJournal", "deletionReview", "unlinkReview" })
                    Assert.IsNull(F(online, field), field);
                Assert.IsFalse((bool)F(online, "deletionMayRetry"));
                Assert.AreEqual("{", File.ReadAllText(path));
                Assert.AreEqual("untouched-save", File.ReadAllText(Path.Combine(root, "save.json")));
                // Removing the deliberately corrupt fixture simulates a repaired
                // read; production code must never remove it on the user's behalf.
                File.Delete(path);
                Call(online, "ReloadAccountRecords", root);
                Assert.IsFalse((bool)F(online, "accountRecordInvalid"));
                StringAssert.Contains("Apple連携は任意", (string)F(online, "accountMessage"));
            }
            finally
            {
                try { UnityEngine.Object.DestroyImmediate(owner); }
                finally
                {
                    try { storage?.Dispose(); }
                    finally { Directory.Delete(root, true); } // Only this test-created temporary directory.
                }
            }
        }

        [UnityTest]
        public IEnumerator DrawingFailureRestoresGuiStateAndUsesSafeViewOnFollowingEvents()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var hook = T("OnlinePlayerData").GetField("EditorAccountDrawingOverride", BindingFlags.NonPublic | BindingFlags.Static);
            object previousHook = hook.GetValue(null);
            string root = Path.Combine(Path.GetTempPath(), "NasusAccountDraw_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var owner = new GameObject("AccountDrawSafetyProbe"); owner.SetActive(false);
            var probeObject = new GameObject("AccountGuiEventProbe");
            StorageProbe storage = null;
            try
            {
                var online = owner.AddComponent(T("OnlinePlayerData"));
                storage = new StorageProbe(root, online);
                Set(online, "accountMenu", true);
                int attempted = 0;
                hook.SetValue(null, (Action)(() => { attempted++; throw new InvalidOperationException("Synthetic draw error"); }));
                LogAssert.Expect(LogType.Error, "[AppleAccountUI] Drawing stopped safely: InvalidOperationException");
                var probe = probeObject.AddComponent<AccountGuiEventProbe>();
                probe.Draw = () =>
                {
                    try { Call(online, "DrawAppleAccount"); }
                    catch (TargetInvocationException exception) when (exception.InnerException is ExitGUIException)
                    { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
                };
                for (int frame = 0; frame < 12; frame++) yield return null;
                Assert.AreEqual(1, attempted, "A failing draw must not recur every frame.");
                Assert.IsTrue((bool)F(online, "accountDrawFailed"));
                Assert.IsTrue(probe.AllStatesRestored, "Matrix/colors/enabled/depth must survive exceptions.");
                Assert.Greater(probe.CompletedEvents, 1, "The safe view and following GUILayout must still render.");
                Assert.IsNull(F(online, "accountJournal"));
                Assert.IsNull(F(online, "deletionJournal"));
                Assert.IsFalse((bool)F(online, "busy"));
            }
            finally
            {
                hook.SetValue(null, previousHook);
                try
                {
                    UnityEngine.Object.DestroyImmediate(probeObject);
                    UnityEngine.Object.DestroyImmediate(owner);
                }
                finally
                {
                    try { storage?.Dispose(); }
                    finally { Directory.Delete(root, true); } // Only this test-created temporary directory.
                }
            }
            yield return new ExitPlayMode();
        }
    }

    public sealed class AccountGuiEventProbe : MonoBehaviour
    {
        public Action Draw;
        public bool AllStatesRestored = true;
        public int CompletedEvents;
        private void OnGUI()
        {
            if (Draw == null) return;
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color, originalBackground = GUI.backgroundColor, originalContent = GUI.contentColor;
            bool originalEnabled = GUI.enabled;
            int originalDepth = GUI.depth;
            var matrix = Matrix4x4.Scale(new Vector3(.8f, .8f, 1f));
            var color = new Color(.2f, .4f, .6f, .8f);
            GUI.matrix = matrix;
            GUI.color = GUI.backgroundColor = GUI.contentColor = color;
            GUI.enabled = false;
            GUI.depth = 51;
            try
            {
                Draw();
                GUILayout.Label("Layout remains usable after the account dialog.");
                CompletedEvents++;
            }
            finally
            {
                AllStatesRestored &= GUI.matrix == matrix && GUI.color == color && GUI.backgroundColor == color &&
                    GUI.contentColor == color && !GUI.enabled && GUI.depth == 51;
                GUI.matrix = originalMatrix;
                GUI.color = originalColor; GUI.backgroundColor = originalBackground; GUI.contentColor = originalContent;
                GUI.enabled = originalEnabled; GUI.depth = originalDepth;
            }
        }
    }
}
