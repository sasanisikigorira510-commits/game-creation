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
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class DailyChallengePresentationTests
    {
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object S(string t, string m, params object[] args) => T(t).GetMethod(m).Invoke(null, args);
        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n).SetValue(o, v);
        private static object Manager(string n) => T("Managers." + n).GetProperty("Instance").GetValue(null);
        private static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m).Invoke(o, args);
        private string directory;

        [UnityTest]
        public IEnumerator HomeEntranceAndSeparateDailyCardsShowEntriesRewardsAndResumeWithoutSpending()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, (Action<string, Action<string, string>>)((_, done) => done(null, null)));
            directory = Path.Combine(Path.GetTempPath(), "WitchTowerDailyUi-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, directory);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" }) T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Manager("MasterDataManager"), "Initialize");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            Set(save, "HighestFloor", 60); Set(save, "CurrentFloor", 61); Set(save, "HasCompletedTutorial", true); Set(save, "TutorialStepId", "Complete");
            var seen = (IList)F(save, "SeenStoryEventIds");
            foreach (var story in (IEnumerable)T("Data.StoryDialogueCatalog").GetProperty("All").GetValue(null))
                seen.Add(P(story, "EventId"));
            foreach (string id in new[] { "story_chapter_2_unlocked", "story_chapter_3_unlocked", "story_chapter_4_unlocked", "story_chapter_5_unlocked", "story_chapter_6_unlocked", "story_first_arc_complete" }) seen.Add(id);
            var hints = (IList)F(save, "SeenTutorialHintIds");
            foreach (var field in T("Data.StoryTutorialService").GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.StartsWith("Hint", StringComparison.Ordinal))) hints.Add(field.GetRawConstantValue());
            hints.Add("guardian_introduction"); hints.Add("guardian_first_formation");
            Call(Manager("GameManager"), "InitializeFromSave", save);
            var profile = P(Manager("GameManager"), "PlayerProfile");
            var state = Activator.CreateInstance(T("Data.DailyChallengeState")); Set(state, "Day", "2026-10-03");
            var modes = Array.CreateInstance(T("Data.DailyChallengeModeState"), 2);
            for (int i = 0; i < 2; i++)
            {
                var mode = Activator.CreateInstance(T("Data.DailyChallengeModeState"));
                Set(mode, "Mode", i == 0 ? "avalanche" : "champion"); Set(mode, "UsedEntries", i + 1); Set(mode, "BestStage", 5 + i); modes.SetValue(mode, i);
            }
            Set(state, "Modes", modes); profile.GetType().GetProperty("DailyChallenges").SetValue(profile, state);
            Call(Manager("SaveManager"), "SaveCurrentGame");
            // Read-only presentation fixture: no request can reach an external account.
            var online = (MonoBehaviour)S("Save.OnlinePlayerData", "Ensure");
            online.gameObject.SetActive(false);
            T("Save.OnlinePlayerData").GetProperty("LastMessage").SetValue(null, string.Empty);
            SceneManager.LoadScene("HomeScene");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != "HomeScene" && Time.realtimeSinceStartup < deadline) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("HomeScene"));
            var shortcut = GameObject.Find("DailyChallengeButton").GetComponent<Button>();
            Assert.That(shortcut.GetComponentInChildren<Text>().text, Is.EqualTo("デイリー試練"));
            Assert.That(shortcut.transform.Find("TrialStarCore").GetComponent<Image>().sprite, Is.Not.Null);
            AssertNoOverlap((RectTransform)shortcut.transform, (RectTransform)GameObject.Find("FormationButton").transform);
            yield return Capture("daily-home-entrance.png");
            shortcut.onClick.Invoke(); yield return null;
            var panel = GameObject.Find("DailyChallengePanel");
            var labels = panel.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            Assert.That(labels, Has.Some.Contains("雪崩の試練")); Assert.That(labels, Has.Some.Contains("強敵の試練"));
            Assert.That(labels, Has.Some.Contains("残り 1/2")); Assert.That(labels, Has.Some.Contains("残り 0/2"));
            Assert.That(labels, Has.Some.Contains("修練の雫")); Assert.That(labels, Has.Some.Contains("10段階"));
            var buttons = panel.GetComponentsInChildren<Button>().Where(b => b.name == "Start").ToArray();
            Assert.That(buttons.Length, Is.EqualTo(2)); Assert.That(buttons[0].interactable, Is.True); Assert.That(buttons[1].interactable, Is.False);
            yield return Capture("daily-challenge-list.png");
            var run = Activator.CreateInstance(T("Data.DailyChallengeRun"));
            Set(run, "RunId", "ui-resume-fixture"); Set(run, "Mode", "avalanche"); Set(run, "ClearedStage", 3); Set(run, "Status", "active"); Set(state, "ActiveRun", run);
            var behaviour = panel.GetComponent(T("Home.DailyChallengePanel")); Call(behaviour, "Refresh", state);
            var resume = panel.GetComponentsInChildren<Button>().Single(b => b.name == "Resume");
            Assert.That(resume.gameObject.activeInHierarchy, Is.True); Assert.That(resume.GetComponentInChildren<Text>().text, Does.Contain("第4段階から再開"));
            Assert.That(buttons.All(b => !b.interactable), Is.True, "An active run must be resumed before a new paid entry starts.");
            Assert.That(F(modes.GetValue(0), "UsedEntries"), Is.EqualTo(1)); Assert.That(F(modes.GetValue(1), "UsedEntries"), Is.EqualTo(2));
            yield return Capture("daily-challenge-resume.png");
        }

        private static void AssertNoOverlap(RectTransform a, RectTransform b)
        {
            var ac = new Vector3[4]; var bc = new Vector3[4]; a.GetWorldCorners(ac); b.GetWorldCorners(bc);
            Assert.That(new Rect(ac[0], ac[2] - ac[0]).Overlaps(new Rect(bc[0], bc[2] - bc[0])), Is.False);
        }
        private static IEnumerator Capture(string name)
        {
            Assert.That(T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null), Is.False, "A story must not obscure the daily UI capture.");
            Assert.That(T("Save.OnlinePlayerData").GetProperty("LastMessage").GetValue(null), Is.EqualTo(string.Empty), "A connection notice must not obscure the daily UI capture.");
            var online = (MonoBehaviour)T("Save.OnlinePlayerData").GetProperty("Instance").GetValue(null);
            Assert.That(online.gameObject.activeInHierarchy, Is.False, "The read-only fixture disables runtime HTTP/IMGUI surfaces.");
            if (name != "daily-home-entrance.png")
            {
                var panel = GameObject.Find("DailyChallengePanel"); Assert.That(panel, Is.Not.Null);
                panel.transform.SetAsLastSibling();
                Assert.That(panel.transform.GetSiblingIndex(), Is.EqualTo(panel.transform.parent.childCount - 1));
                Assert.That(GameObject.Find("UnifiedHomeMenu"), Is.Null, "Home guidance must be hidden behind the daily modal.");
                Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text), Has.None.Contains("神獣の試練へ"));
            }
            string output = Environment.GetEnvironmentVariable("WITCHTOWER_DAILY_CAPTURE_DIR") ?? Path.Combine(Path.GetTempPath(), "WitchTowerDailyChallengePreviews");
            Directory.CreateDirectory(output); Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(.2f);
            string path = Path.Combine(output, name); if (File.Exists(path)) File.Delete(path); ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 10 && !File.Exists(path); i++) yield return null;
            Assert.That(File.Exists(path), Is.True, path); TestContext.WriteLine("DAILY CAPTURE " + path);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            if (Application.isPlaying) yield return new ExitPlayMode();
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }
}
