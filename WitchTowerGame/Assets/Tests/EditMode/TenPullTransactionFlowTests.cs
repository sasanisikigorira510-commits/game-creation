using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class TenPullTransactionFlowTests
    {
        private const string Key = "witchtower_pending_ten_pull_presentation_v1";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, AnyInstance).Invoke(value, args);
        private static object P(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        private static object F(object value, string name) => value.GetType().GetField(name, AnyInstance).GetValue(value);
        private static object Instance(string name) => T(name).GetProperty("Instance").GetValue(null);

        [UnityTest]
        public IEnumerator RealSingleAndTenPullTransactionsGrantOnlyConfirmedResultsAndSurviveRecovery()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineUiTestGateway.Install();
            bool hadPending = PlayerPrefs.HasKey(Key); string oldPending = PlayerPrefs.GetString(Key);
            var oldRandom = UnityEngine.Random.state;
            string folder = Path.Combine(Path.GetTempPath(), "WitchTowerTenPullTransaction-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var overrideProperty = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            overrideProperty.SetValue(null, folder);
            try
            {
                PlayerPrefs.DeleteKey(Key);
                foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" }) T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
                Call(Instance("Managers.MasterDataManager"), "Initialize");
                object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                save.GetType().GetField("HasCompletedTutorial").SetValue(save, true);
                save.GetType().GetField("TutorialStepId").SetValue(save, "Complete");
                Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
                object profile = P(Instance("Managers.GameManager"), "PlayerProfile");
                profile.GetType().GetProperty("FreeGachaStones").SetValue(profile, 10000);
                profile.GetType().GetProperty("PaidGachaStones").SetValue(profile, 10000);
                profile.GetType().GetProperty("MonsterStorageLimit").SetValue(profile, 100);
                Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
                var root = new GameObject("TransactionCanvas", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var panel = new GameObject("GachaScenePanel", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                Component gacha = panel.AddComponent(T("Home.GachaPanelController"));
                Call(gacha, "Show", (Action)null);
                foreach (int count in new[] { 1, 10, 1 })
                foreach (bool paid in new[] { false, true })
                {
                    int beforeOwned = ((IList)P(profile, "OwnedMonsters")).Count;
                    int beforeFree = (int)P(profile, "FreeGachaStones"), beforePaid = (int)P(profile, "PaidGachaStones");
                    UnityEngine.Random.InitState(paid ? 2014 : 2013);
                    Call(gacha, "RunContract", count, paid);
                    Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(beforeOwned + count));
                    Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(beforeFree - (paid ? 0 : 300 * count)));
                    Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(beforePaid - (paid ? 300 * count : 0)));
                    var presentation = (Component)F(gacha, "tenPullPresentation");
                    Assert.That(presentation, Is.Not.Null);
                    Assert.That(P(P(presentation, "Sequence"), "Phase").ToString(), Is.EqualTo("Introduction"));
                    string persisted = File.ReadAllText(Path.Combine(folder, "save.json"));
                    var results = new object[count];
                    for (int i = 0; i < count; i++) results[i] = Call(P(presentation, "Sequence"), "GetResult", i);
                    if (paid && count == 10) Assert.That(results.Any(x => (int)F(x, "ClassRank") == 3), Is.True, "Existing paid ten-pull C3 guarantee is unchanged.");
                    var rngAfterGrant = UnityEngine.Random.state;
                    // A second press while the presentation is active cannot start another transaction.
                    for (int i = 0; i < 10; i++) Call(gacha, "RunContract", count, paid);
                    int introTicks = Mathf.CeilToInt((float)P(P(presentation, "Sequence"), "Duration") / 0.1f) + 1;
                    for (int i = 0; i < (paid ? introTicks : 5); i++) Call(presentation, "TickPresentation", 0.1f);
                    Assert.That(P(P(presentation, "Sequence"), "Phase").ToString(), Is.EqualTo(paid ? "Materialization" : "Introduction"));
                    Call(presentation, "OnApplicationPause", true);
                    Call(presentation, "OnApplicationFocus", false);
                    Assert.That(P(P(presentation, "Sequence"), "Phase").ToString(), Is.EqualTo("Summary"));
                    Call(gacha, "Close");
                    // Recreate the screen and reload inventory from disk, as after leaving/restarting the app.
                    UnityEngine.Object.Destroy(root);
                    yield return null;
                    Call(Instance("Managers.SaveManager"), "LoadOrCreate");
                    Call(Instance("Managers.GameManager"), "InitializeFromSave", P(Instance("Managers.SaveManager"), "CurrentSaveData"));
                    profile = P(Instance("Managers.GameManager"), "PlayerProfile");
                    root = new GameObject("RecoveredTransactionCanvas", typeof(RectTransform), typeof(Canvas));
                    root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    panel = new GameObject("RecoveredGachaPanel", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                    gacha = panel.AddComponent(T("Home.GachaPanelController"));
                    Call(gacha, "Show", (Action)null); // Reads only the already-saved, already-owned journal.
                    presentation = (Component)F(gacha, "tenPullPresentation");
                    Assert.That(P(P(presentation, "Sequence"), "Phase").ToString(), Is.EqualTo("Summary"));
                    for (int i = 0; i < count; i++)
                        Assert.That(JsonUtility.ToJson(Call(P(presentation, "Sequence"), "GetResult", i)), Is.EqualTo(JsonUtility.ToJson(results[i])));
                    for (int i = 0; i < 10; i++) Call(presentation, "Skip");
                    Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(beforeOwned + count));
                    Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(beforeFree - (paid ? 0 : 300 * count)));
                    Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(beforePaid - (paid ? 300 * count : 0)));
                    Assert.That(File.ReadAllText(Path.Combine(folder, "save.json")), Is.EqualTo(persisted), "Animation and recovery do not rewrite granted inventory.");
                    Assert.That(UnityEngine.Random.state, Is.EqualTo(rngAfterGrant));
                    presentation.transform.Find("TenPullSafeContent/BackToSummon").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(PlayerPrefs.HasKey(Key), Is.False);
                    Assert.That(presentation.gameObject.activeSelf, Is.False);
                }
                TestContext.WriteLine("Real transaction test used only isolated save: " + folder);
            }
            finally
            {
                OnlineUiTestGateway.Clear();
                if (hadPending) PlayerPrefs.SetString(Key, oldPending); else PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save(); UnityEngine.Random.state = oldRandom;
                overrideProperty.SetValue(null, null);
            }
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
