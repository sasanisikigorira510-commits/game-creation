using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WitchTower.Tests
{
    public sealed class SafeAreaSceneFlowTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
        private static object Instance(string name) => T(name).GetProperty("Instance").GetValue(null);
        private static Component Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType() == T(name));

        [UnityTest]
        public IEnumerator BattleHomeAndLazyPagesStayFixedFromFirstRender()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder = Path.Combine(Path.GetTempPath(), "WitchTowerSafeAreaFlow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, folder);
            T("UI.SafeAreaLayoutController").GetProperty("SafeAreaOverride").SetValue(null,
                new Rect(0, 1920f * 102f / 2556f, 1080f, 1920f * 2277f / 2556f));
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save, true);
            save.GetType().GetField("TutorialStepId").SetValue(save, "Complete");
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            object profile = T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            IList party = (IList)profile.GetType().GetProperty("PartyMonsterInstanceIds").GetValue(profile);
            party.Clear();
            foreach (string id in new[] { "monster_apprentice_mage", "monster_rock_golem", "monster_dragon_whelp" })
            {
                object monster = Call(profile, "AddOwnedMonster", id, 20, 0, false);
                party.Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            for (int roundTrip = 0; roundTrip < 2; roundTrip++)
            {
                if (roundTrip == 1)
                {
                    T("UI.SceneTransitionGuard").GetMethod("LoadScene").Invoke(null, new object[] { "BattleScene" });
                    yield return WaitForScene("BattleScene");
                    Call(Find("Battle.BattleSceneController"), "ReturnHome");
                    yield return WaitForScene("HomeScene");
                }
                foreach (bool dungeon in new[] { false, true })
                {
                    Call(Find("Home.HomeSceneController"), dungeon ? "StartBattle" : "OpenGoldShopMenu");
                    var root = GameObject.Find(dungeon ? "DungeonSelectionPanel" : "GoldShopPanel")?.transform as RectTransform;
                    Assert.That(root, Is.Not.Null);
                    var title = (RectTransform)root.Find(dungeon ? "DungeonSelectionFrame/Title" : "ShopMainPanel/Title");
                    var button = (RectTransform)root.Find(dungeon ? "CloseButton" : "ShopCloseButton");
                    // The project uses a fixed 1080x1920 Game view. In a batch
                    // EditMode coroutine Screen can instead report the editor
                    // window size; observe actual Game-view render callbacks,
                    // not a ForceUpdateCanvases issued in that editor context.
                    Vector3 firstTitle = default, firstButton = default;
                    int renders = 0;
                    string failure = null;
                    Canvas.WillRenderCanvases observe = () =>
                    {
                        if (Screen.width != 1080 || Screen.height != 1920) return;
                        if (Mathf.Abs(root.anchorMin.y - 102f / 2556f) > 0.00001f ||
                            Mathf.Abs(root.anchorMax.y - (1f - 177f / 2556f)) > 0.00001f)
                            failure = "Page reached a render with incorrect safe-area anchors.";
                        if (renders++ == 0)
                        {
                            firstTitle = title.position;
                            firstButton = button.position;
                        }
                        if (Vector3.Distance(title.position, firstTitle) > 0.1f ||
                            Vector3.Distance(button.position, firstButton) > 0.1f)
                            failure = "Title or home button jumped after its first actual render.";
                    };
                    Canvas.willRenderCanvases += observe;
                    try
                    {
                        float until = Time.realtimeSinceStartup + 0.6f;
                        while (Time.realtimeSinceStartup < until) yield return null;
                    }
                    finally { Canvas.willRenderCanvases -= observe; }
                    Assert.That(renders, Is.GreaterThan(2));
                    Assert.That(failure, Is.Null);
                    Debug.Log($"[SafeAreaSceneFlow] afterBattle={roundTrip == 1}, page={root.name}: {renders} actual renders stable, including first render.");
                    Call(root.GetComponent(T(dungeon ? "Home.DungeonSelectionPanelController" : "Home.GoldShopPanelController")),
                        dungeon ? "Close" : "Hide");
                    yield return null;
                }
            }
            TestContext.WriteLine("Isolated integration save: " + folder);
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
            for (int i = 0; i < 8; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
