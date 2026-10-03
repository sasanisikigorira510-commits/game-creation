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
    public sealed class GuardianTrialSceneIsolationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private string saveDirectory;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        private static object S(string name, string method, params object[] args) => T(name).GetMethod(method).Invoke(null, args);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);
        private static object Profile => P(Instance("Managers.GameManager"), "PlayerProfile");
        private static MonoBehaviour Controller => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType() == T("Battle.BattleSceneController"));
        private static MonoBehaviour Simulator => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType() == T("Battle.BattleSimulator"));
        private static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(c => c.name == name && c.gameObject.activeInHierarchy);

        [UnityTest]
        public IEnumerator BorrowedGuardianPracticeFinishesNaturallyAndLeavesProfileAndSaveUntouched()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(false);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            string before = Snapshot();
            byte[] bytes = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            Assert.That(S("Data.GuardianTrialSession", "BeginPractice", Profile, "suzaku", false), Is.True);
            SceneManager.LoadScene("BattleScene");
            yield return WaitForScene("BattleScene");
            Assert.That(P(Simulator, "GuardianMonsterData"), Is.Not.Null, "The unowned guardian must be lent for this battle only.");
            Assert.That(P(Controller, "IsPermanentEffectsInputBlocked"), Is.True);
            float deadline = Time.realtimeSinceStartup + 20;
            Time.timeScale = 4;
            while (GameObject.Find("BattleMinimalResultOverlay") == null && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = 1;
            Assert.That(GameObject.Find("BattleMinimalResultOverlay"), Is.Not.Null, "Practice must finish within its 15-second battle limit.");
            Assert.That(GameObject.Find("BattleMinimalResultOverlay").GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("共鳴体験"));
            Assert.That(GameObject.Find("BattleMinimalResultOverlay").GetComponentsInChildren<Text>().Select(t => t.text), Has.None.Contains("神核"));
            Assert.That(GameObject.Find("GuardianResonanceStatus").GetComponent<Text>().text, Does.Contain("共鳴"));
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(bytes));
            yield return CaptureIfRequested("guardian-practice-result.png");
            Button("HomeButton").onClick.Invoke();
            yield return WaitForScene("HomeScene");
            Assert.That(Snapshot(), Is.EqualTo(before), "Returning from a practice cannot advance main-story tutorial flags.");
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(bytes));
            Assert.That(((IList)P(Profile, "OwnedGuardians")).Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PracticeDefeatAndRetreatAreBothReadOnly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(false);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            for (int exit = 0; exit < 2; exit++)
            {
                string before = Snapshot();
                byte[] bytes = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
                Assert.That(S("Data.GuardianTrialSession", "BeginPractice", Profile, "genbu", true), Is.True);
                SceneManager.LoadScene("BattleScene");
                yield return WaitForScene("BattleScene");
                if (exit == 0)
                {
                    Call(Controller, "OnBattleLose");
                    Assert.That(GameObject.Find("BattleMinimalResultOverlay"), Is.Not.Null);
                    Call(Controller, "ReturnHome");
                }
                else Call(Controller, "Retreat");
                yield return WaitForScene("HomeScene");
                Assert.That(Snapshot(), Is.EqualTo(before));
                Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(bytes));
                Assert.That(T("Data.GuardianTrialSession").GetProperty("IsActive").GetValue(null), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator OathGrantsOnlyItsTitleOnceAndKeepsAllGameplayProgress()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            string before = SnapshotIgnoringOath();
            Assert.That(S("Data.GuardianTrialSession", "BeginOath", Profile, "seiryu"), Is.True);
            SceneManager.LoadScene("BattleScene");
            yield return WaitForScene("BattleScene");
            var controller = Controller;
            Call(controller, "OnBattleWin");
            for (int i = 0; i < 20; i++) Call(controller, "OnBattleWin");
            Assert.That(((IList)P(Profile, "GuardianOathIds")).Cast<string>(), Is.EqualTo(new[] { "seiryu" }));
            Assert.That(SnapshotIgnoringOath(), Is.EqualTo(before));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save.json")), Does.Contain("GuardianOathIds"));
            yield return CaptureIfRequested("guardian-oath-result.png");
            Call(controller, "ReturnHome");
            yield return WaitForScene("HomeScene");
            string completed = Snapshot();
            byte[] bytes = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            Assert.That(S("Data.GuardianTrialSession", "BeginOath", Profile, "seiryu"), Is.True);
            SceneManager.LoadScene("BattleScene");
            yield return WaitForScene("BattleScene");
            Call(Controller, "OnBattleWin");
            Assert.That(GameObject.Find("BattleMinimalResultOverlay").GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("達成済み"));
            Call(Controller, "ReturnHome");
            yield return WaitForScene("HomeScene");
            Assert.That(Snapshot(), Is.EqualTo(completed));
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator OathCannotCompleteWithOnlyTheGuardianSurviving()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            string before = Snapshot();
            byte[] bytes = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            Assert.That(S("Data.GuardianTrialSession", "BeginOath", Profile, "seiryu"), Is.True);
            SceneManager.LoadScene("BattleScene");
            yield return WaitForScene("BattleScene");
            var allies = (IList)Simulator.GetType().GetField("activeAllyRuntimes", Hidden).GetValue(Simulator);
            foreach (var ally in allies)
            {
                if ((int)ally.GetType().GetField("SlotIndex").GetValue(ally) >= 5) continue;
                var stats = ally.GetType().GetField("Stats").GetValue(ally);
                stats.GetType().GetField("CurrentHp").SetValue(stats, 0);
            }
            Call(Controller, "OnBattleWin");
            Assert.That(((IList)P(Profile, "GuardianOathIds")).Count, Is.EqualTo(0));
            Assert.That(GameObject.Find("BattleMinimalResultOverlay").GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("通常の仲間"));
            Call(Controller, "ReturnHome");
            yield return WaitForScene("HomeScene");
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(bytes));
        }

        private void InitializeGame(bool oath)
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerGuardianTrialIsolation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, saveDirectory);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" }) T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            save.GetType().GetField("HighestFloor").SetValue(save, 60);
            save.GetType().GetField("CurrentFloor").SetValue(save, 61);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save, true);
            save.GetType().GetField("TutorialStepId").SetValue(save, "Complete");
            var stories = (IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save);
            foreach (string id in new[] { "story_chapter_2_unlocked", "story_chapter_3_unlocked", "story_chapter_4_unlocked", "story_chapter_5_unlocked", "story_chapter_6_unlocked", "story_first_arc_complete" }) stories.Add(id);
            var hints = (IList)save.GetType().GetField("SeenTutorialHintIds").GetValue(save);
            hints.Add("guardian_introduction");
            if (oath)
            {
                var owned = Activator.CreateInstance(T("Save.OwnedGuardianData"));
                owned.GetType().GetField("Id").SetValue(owned, "seiryu");
                owned.GetType().GetField("Level").SetValue(owned, 30);
                ((IList)save.GetType().GetField("OwnedGuardians").GetValue(save)).Add(owned);
                save.GetType().GetField("EquippedGuardianId").SetValue(save, "seiryu");
                hints.Add("guardian_first_formation");
            }
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            var party = (IList)P(Profile, "PartyMonsterInstanceIds"); party.Clear();
            foreach (string id in new[] { "monster_flare_drake", "monster_rock_golem", "monster_apprentice_mage", "monster_apprentice_swordsman", "monster_chibi_gear" })
            {
                var monster = Call(Profile, "AddOwnedMonster", id, 20, 0, false);
                party.Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
        }

        private static string Snapshot() => JsonUtility.ToJson(Call(Profile, "ToSaveData", P(Instance("Managers.GameManager"), "CurrentFloor")));
        private static string SnapshotIgnoringOath()
        {
            var save = Call(Profile, "ToSaveData", P(Instance("Managers.GameManager"), "CurrentFloor"));
            ((IList)save.GetType().GetField("GuardianOathIds").GetValue(save)).Clear();
            return JsonUtility.ToJson(save);
        }
        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
            for (int i = 0; i < 10; i++) yield return null;
        }
        private static IEnumerator CaptureIfRequested(string file)
        {
            string directory = Environment.GetEnvironmentVariable("WITCHTOWER_GUARDIAN_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, file));
            for (int i = 0; i < 3; i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (Application.isPlaying) yield return new ExitPlayMode();
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }
}
