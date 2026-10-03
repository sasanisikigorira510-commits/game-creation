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
    public sealed class HomeMonsterTrainingPageTests
    {
        [Serializable]
        private sealed class RequestView
        {
            public string Kind;
            public string Target;
            public string Stat;
        }

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
        private static object P(object owner, string name)
        {
            Assert.That(owner, Is.Not.Null, "Missing reflection owner for property " + name);
            var property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, owner.GetType().FullName + " has no public property " + name);
            return property.GetValue(owner);
        }
        private static object F(object owner, string name)
        {
            Assert.That(owner, Is.Not.Null, "Missing reflection owner for field " + name);
            var field = owner.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, owner.GetType().FullName + " has no public field " + name);
            return field.GetValue(owner);
        }
        private static void Set(object owner, string name, object value)
        {
            Assert.That(owner, Is.Not.Null, "Missing reflection owner for field " + name);
            var field = owner.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, owner.GetType().FullName + " has no public field " + name);
            field.SetValue(owner, value);
        }
        private static object Manager(string name) => T("Managers." + name).GetProperty("Instance").GetValue(null);
        private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method).Invoke(owner, args);

        private string directory;
        private object profile;
        private RequestView pendingRequest;
        private Action<string, string> pendingDelivery;

        [UnityTest]
        public IEnumerator HomeTrainingListsOwnedMonstersAndReturnsFromConfirmedStatAndSkillTraining()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return BootHome();
            var home = UnityEngine.Object.FindFirstObjectByType(T("Home.HomeSceneController"));
            Assert.That(home, Is.Not.Null);
            var entrance = GameObject.Find("TrainingButton").GetComponent<Button>();
            Assert.That(entrance.GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("修練"));
            var artwork = Resources.Load<Sprite>("UI/HomeMenu/TrainingHomeButtonImage2");
            Assert.That(artwork, Is.Not.Null, "The home entrance must use the new image2 artwork.");
            Assert.That(entrance.GetComponentsInChildren<Image>().Select(i => i.sprite), Has.Member(artwork));
            yield return Capture("training-home-entrance.png", false);

            // Start with real outstanding guardian guidance, then verify the new page suppresses it.
            ((IList)P(profile, "SeenTutorialHintIds")).Remove("guardian_first_formation");
            yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text), Has.Some.Contains("神獣の試練へ"));

            entrance.onClick.Invoke();
            yield return null;
            var page = GameObject.Find("HomeMonsterTrainingPanel");
            Assert.That(page, Is.Not.Null);
            Assert.That(RosterRows(page).Length, Is.EqualTo(10));
            Assert.That(ButtonIn(page, "TrainingPreviousPage").interactable, Is.False);
            Assert.That(ButtonIn(page, "TrainingNextPage").interactable, Is.True);
            AssertTrainingBoundary(home);
            yield return Capture("training-owned-list.png", true);

            ButtonIn(page, "TrainingNextPage").onClick.Invoke();
            yield return null;
            Assert.That(RosterRows(page).Length, Is.EqualTo(2));
            Assert.That(ButtonIn(page, "TrainingNextPage").interactable, Is.False);
            Assert.That(ButtonIn(page, "TrainingPreviousPage").interactable, Is.True);

            var search = page.GetComponentsInChildren<InputField>().Single(i => i.name == "TrainingSearch");
            var master = Manager("MasterDataManager");
            string skillName = (string)F(Call(master, "GetMonsterData", "monster_ordion"), "monsterName");
            search.text = skillName;
            yield return null;
            Assert.That(RosterRows(page).Length, Is.EqualTo(1), "A name filter must search the whole roster, not only the current page.");
            Assert.That(ButtonIn(page, "TrainingPreviousPage").interactable, Is.False, "A changed filter resets pagination.");
            search.text = "no-such-owned-monster";
            yield return null;
            Assert.That(RosterRows(page), Is.Empty);
            search.text = string.Empty;
            yield return null;
            Assert.That(RosterRows(page).Length, Is.EqualTo(10));
            ButtonIn(page, "ClassFilter_5").onClick.Invoke();
            yield return null;
            Assert.That(RosterRows(page).Length, Is.EqualTo(1), "The class filter works independently of the current page.");
            Assert.That(RosterRows(page).Single().GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("クラス5"));
            yield return Capture("training-class5-list.png", true);
            ButtonIn(page, "ClassFilter_1").onClick.Invoke();
            yield return null;
            Assert.That(RosterRows(page).Length, Is.EqualTo(10));
            Assert.That(page.GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("11体"));
            ButtonIn(page, "ClassFilter_0").onClick.Invoke();
            yield return null;

            Assert.That(RosterRows(page).Length, Is.EqualTo(10));
            Assert.That(profile, Is.Not.Null, "The fixture profile disappeared after filter refresh.");
            var ownedList = (IList)P(profile, "OwnedMonsters");
            Assert.That(ownedList, Is.Not.Null, "The fixture profile has no owned roster after filter refresh.");
            Assert.That(ownedList.Count, Is.EqualTo(12), "Filtering must not mutate ownership.");
            Assert.That(ownedList[0], Is.Not.Null, "The first owned monster disappeared while filtering.");
            object selectedValue = F(ownedList[0], "InstanceId");
            string selectedId = selectedValue as string;
            Assert.That(selectedId, Is.Not.Null.And.Not.Empty);
            var selectedButton = ButtonIn(page, "TrainingMonster_" + selectedId);
            Assert.That(selectedButton, Is.Not.Null, "The selected owned monster must have an active card.");
            Assert.That(selectedButton.onClick, Is.Not.Null, "The selected card must have an initialized click event.");
            selectedButton.onClick.Invoke();
            yield return null;
            var training = GameObject.Find("MonsterTrainingPopup");
            Assert.That(training, Is.Not.Null);
            Assert.That(GameObject.Find("MonsterStatusDetailPopup"), Is.Null, "Selecting a monster should enter training directly.");
            Assert.That(training.transform.IsChildOf(page.transform), Is.True);
            Assert.That(training.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Train_", StringComparison.Ordinal)), Is.EqualTo(6));
            Assert.That(GameObject.Find("TrainMonsterSkill"), Is.Null, "Class 1 monsters use the six stat items.");
            Assert.That(TextIn(training, "TrainingLabel_0").text, Does.Contain("Lv0/10"));
            yield return Capture("training-stat-items.png", true);

            var owned = Call(profile, "GetOwnedMonster", selectedId);
            ButtonIn(training, "Train_Hp").onClick.Invoke();
            yield return null;
            Assert.That(pendingRequest, Is.Not.Null);
            Assert.That(pendingRequest.Kind, Is.EqualTo("monster_training"));
            Assert.That(pendingRequest.Target, Is.EqualTo(selectedId));
            Assert.That(pendingRequest.Stat, Is.EqualTo("Hp"));
            Assert.That(F(owned, "TrainingHp"), Is.Zero, "The live value stays unchanged while delivery is pending.");
            training = GameObject.Find("MonsterTrainingPopup");
            Assert.That(ButtonIn(training, "CloseTraining").interactable, Is.False);
            Assert.That(training.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Train_", StringComparison.Ordinal)).All(b => !b.interactable), Is.True);
            Call(home, "OpenDailyChallengePanel");
            ButtonIn(page, "CloseTrainingPage").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("DailyChallengePanel"), Is.Null, "A pending training delivery cannot be replaced by another modal.");
            Assert.That(GameObject.Find("HomeMonsterTrainingPanel"), Is.SameAs(page));
            Assert.That(GameObject.Find("MonsterTrainingPopup"), Is.Not.Null);
            Assert.That(pendingDelivery, Is.Not.Null);
            DeliverConfirmedTraining();
            yield return null;
            Assert.That(F(owned, "TrainingHp"), Is.Zero, "The confirmed delivery replaces the profile rather than mutating a stale selection.");
            training = GameObject.Find("MonsterTrainingPopup");
            Assert.That(TextIn(training, "TrainingLabel_0").text, Does.Contain("Lv1/10"));
            Assert.That(TextIn(training, "TrainingWallet").text, Does.Contain("124"));
            AssertTrainingBoundary(home);
            ButtonIn(training, "CloseTraining").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("MonsterTrainingPopup"), Is.Null);
            Assert.That(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("修練の雫") && t.text.Contains("124")), Is.True, "Returning to the list refreshes the material balance.");
            var refreshedRow = ButtonIn(page, "TrainingMonster_" + selectedId);
            Assert.That(refreshedRow.GetComponentsInChildren<Text>().Select(t => t.text), Has.Some.Contains("合計1/60"));
            refreshedRow.onClick.Invoke();
            yield return null;
            Assert.That(TextIn(GameObject.Find("MonsterTrainingPopup"), "TrainingLabel_0").text, Does.Contain("Lv1/10"));
            ButtonIn(GameObject.Find("MonsterTrainingPopup"), "CloseTraining").onClick.Invoke();
            yield return null;

            search.text = skillName;
            yield return null;
            RosterRows(page).Single().onClick.Invoke();
            yield return null;
            training = GameObject.Find("MonsterTrainingPopup");
            var skill = ButtonIn(training, "TrainMonsterSkill");
            Assert.That(skill.interactable, Is.True);
            Assert.That(TextIn(training, "MonsterSkillTraining").text, Does.Contain("Lv1/5"));
            Assert.That(training.transform.Find("TrainingPanel/TrialStarCoreIcon").GetComponent<Image>().sprite, Is.Not.Null);
            yield return Capture("training-class5-skill.png", true);
            skill.onClick.Invoke();
            yield return null;
            Assert.That(pendingRequest.Kind, Is.EqualTo("monster_skill_training"));
            DeliverConfirmedTraining();
            yield return null;
            training = GameObject.Find("MonsterTrainingPopup");
            Assert.That(TextIn(training, "MonsterSkillTraining").text, Does.Contain("Lv2/5"));
            Assert.That(TextIn(training, "MonsterSkillTraining").text, Does.Contain("星核80"));
            ButtonIn(training, "CloseTraining").onClick.Invoke();
            yield return null;

            ButtonIn(page, "CloseTrainingPage").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("HomeMonsterTrainingPanel"), Is.Null);
            Assert.That(GameObject.Find("UnifiedHomeMenu"), Is.Not.Null);
            Assert.That(P(home, "IsHomeMenuVisible"), Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text), Has.Some.Contains("神獣の試練へ"), "Closing the page restores outstanding home guidance.");

            // A second page cannot replace an open page or discard a training operation.
            Call(home, "OpenMonsterTrainingPage");
            yield return null;
            Call(home, "OpenDailyChallengePanel");
            yield return null;
            Assert.That(GameObject.Find("DailyChallengePanel"), Is.Null);
            Assert.That(GameObject.Find("HomeMonsterTrainingPanel"), Is.Not.Null);
            AssertTrainingBoundary(home);
            ButtonIn(GameObject.Find("HomeMonsterTrainingPanel"), "CloseTrainingPage").onClick.Invoke();
            yield return null;
            Call(home, "OpenDailyChallengePanel");
            yield return null;
            Assert.That(GameObject.Find("HomeMonsterTrainingPanel"), Is.Null);
            Assert.That(GameObject.Find("DailyChallengePanel"), Is.Not.Null);
            Call(home, "OpenMonsterTrainingPage");
            yield return null;
            Assert.That(GameObject.Find("HomeMonsterTrainingPanel"), Is.Null);
            Assert.That(GameObject.Find("DailyChallengePanel"), Is.Not.Null);
        }

        private IEnumerator BootHome()
        {
            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, (Action<string, Action<string, string>>)ReceiveRequest);
            directory = Path.Combine(Path.GetTempPath(), "WitchTowerHomeTraining-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, directory);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Manager("MasterDataManager"), "Initialize");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            Set(save, "HighestFloor", 60); Set(save, "CurrentFloor", 61);
            Set(save, "HasCompletedTutorial", true); Set(save, "TutorialStepId", "Complete");
            Set(save, "TrainingDrops", 125); Set(save, "TrialStarCores", 100);
            var stories = (IList)F(save, "SeenStoryEventIds");
            foreach (var story in (IEnumerable)T("Data.StoryDialogueCatalog").GetProperty("All").GetValue(null))
                stories.Add(P(story, "EventId"));
            foreach (string id in new[] { "story_chapter_2_unlocked", "story_chapter_3_unlocked", "story_chapter_4_unlocked", "story_chapter_5_unlocked", "story_chapter_6_unlocked", "story_first_arc_complete" })
                stories.Add(id);
            var hints = (IList)F(save, "SeenTutorialHintIds");
            foreach (var field in T("Data.StoryTutorialService").GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.StartsWith("Hint", StringComparison.Ordinal)))
                hints.Add(field.GetRawConstantValue());
            hints.Add("guardian_introduction"); hints.Add("guardian_first_formation");
            Call(Manager("GameManager"), "InitializeFromSave", save);
            profile = P(Manager("GameManager"), "PlayerProfile");
            ((IList)P(profile, "OwnedMonsters")).Clear();
            ((IList)P(profile, "PartyMonsterInstanceIds")).Clear();
            for (int i = 0; i < 12; i++)
                Call(profile, "AddOwnedMonster", i == 11 ? "monster_ordion" : "monster_dragon_whelp", 1, 0, false);
            Call(Manager("SaveManager"), "SaveCurrentGame");
            var online = (MonoBehaviour)S("Save.OnlinePlayerData", "Ensure");
            online.gameObject.SetActive(false);
            T("Save.OnlinePlayerData").GetProperty("LastMessage").SetValue(null, string.Empty);
            SceneManager.LoadScene("HomeScene");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != "HomeScene" && Time.realtimeSinceStartup < deadline) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("HomeScene"));
            Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(12));
        }

        private void ReceiveRequest(string json, Action<string, string> done)
        {
            if (string.IsNullOrEmpty(json) || json == "null") { done(null, null); return; }
            var request = JsonUtility.FromJson<RequestView>(json);
            if (request == null || string.IsNullOrEmpty(request.Kind)) { done(null, null); return; }
            Assert.That(request.Kind, Is.EqualTo("monster_training").Or.EqualTo("monster_skill_training"), "The UI fixture never starts a paid challenge or sends live HTTP.");
            Assert.That(pendingDelivery, Is.Null, "Only one training operation may be in flight.");
            pendingRequest = request; pendingDelivery = done;
        }

        private void DeliverConfirmedTraining()
        {
            Assert.That(pendingDelivery, Is.Not.Null);
            var owned = Call(profile, "GetOwnedMonster", pendingRequest.Target);
            var updated = JsonUtility.FromJson(JsonUtility.ToJson(owned), owned.GetType());
            var source = Call(profile, "ToSaveData", P(Manager("GameManager"), "CurrentFloor"));
            var operation = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(operation, "Kind", pendingRequest.Kind);
            Set(operation, "Target", pendingRequest.Target);
            Set(operation, "Revision", (long)F(source, "EconomyRevision") + 1);
            Set(operation, "Free", F(source, "FreeGachaStones"));
            Set(operation, "Paid", F(source, "PaidGachaStones"));
            Set(operation, "TrainingDrops", P(profile, "TrainingDrops"));
            Set(operation, "TrialStarCores", P(profile, "TrialStarCores"));
            if (pendingRequest.Kind == "monster_training")
            {
                Set(updated, "TrainingHp", (int)F(updated, "TrainingHp") + 1);
                Set(operation, "TrainingDrops", (int)P(profile, "TrainingDrops") - 1);
            }
            else
            {
                Set(updated, "MonsterSkillLevel", (int)F(updated, "MonsterSkillLevel") + 1);
                Set(operation, "TrialStarCores", (int)P(profile, "TrialStarCores") - 20);
            }
            var updates = Array.CreateInstance(updated.GetType(), 1); updates.SetValue(updated, 0);
            Set(operation, "UpdatedMonsters", updates);
            var committed = S("Save.OnlineGrantApplier", "Stage", source, operation);
            var saveArguments = new object[] { committed, "home_training_ui_fixture", null };
            bool saved = (bool)Manager("SaveManager").GetType().GetMethod("TrySaveWithReason").Invoke(Manager("SaveManager"), saveArguments);
            Assert.That(saved, Is.True, (string)saveArguments[2]);
            Call(Manager("GameManager"), "InitializeFromSave", committed);
            profile = P(Manager("GameManager"), "PlayerProfile");
            // The editor override receives a persisted, already-applied authenticated delivery; it never sends live HTTP.
            var delivery = pendingDelivery; pendingDelivery = null; pendingRequest = null;
            delivery(JsonUtility.ToJson(operation), null);
        }

        private static Button[] RosterRows(GameObject page) => page.GetComponentsInChildren<Button>()
            .Where(b => b != null && b.gameObject.activeInHierarchy && b.name.StartsWith("TrainingMonster_", StringComparison.Ordinal)).ToArray();
        private static Button ButtonIn(GameObject owner, string name) => owner.GetComponentsInChildren<Button>().Single(b => b != null && b.gameObject.activeInHierarchy && b.name == name);
        private static Text TextIn(GameObject owner, string name) => owner.GetComponentsInChildren<Text>().Single(t => t != null && t.gameObject.activeInHierarchy && t.name == name);

        private static void AssertTrainingBoundary(object home)
        {
            Assert.That(P(home, "IsHomeMenuVisible"), Is.False);
            Assert.That(GameObject.Find("UnifiedHomeMenu"), Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text), Has.None.Contains("神獣の試練へ"));
            Assert.That(T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null), Is.False);
        }

        private static IEnumerator Capture(string name, bool modal)
        {
            Assert.That(T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null), Is.False, "Story dialogue must not hide the training page.");
            Assert.That(T("Save.OnlinePlayerData").GetProperty("LastMessage").GetValue(null), Is.EqualTo(string.Empty));
            var online = (MonoBehaviour)T("Save.OnlinePlayerData").GetProperty("Instance").GetValue(null);
            Assert.That(online.gameObject.activeInHierarchy, Is.False, "The isolated fixture disables runtime HTTP and account notices.");
            if (modal)
            {
                var page = GameObject.Find("HomeMonsterTrainingPanel");
                Assert.That(page, Is.Not.Null);
                Assert.That(page.transform.GetSiblingIndex(), Is.EqualTo(page.transform.parent.childCount - 1), "The page must be in front without test-side reordering.");
                Assert.That(GameObject.Find("UnifiedHomeMenu"), Is.Null);
                var popup = GameObject.Find("MonsterTrainingPopup");
                if (popup != null) Assert.That(popup.transform.GetSiblingIndex(), Is.EqualTo(popup.transform.parent.childCount - 1));
                Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text), Has.None.Contains("神獣の試練へ"));
            }
            string output = Environment.GetEnvironmentVariable("WITCHTOWER_TRAINING_CAPTURE_DIR") ?? Path.Combine(Path.GetTempPath(), "WitchTowerHomeTrainingPreviews");
            Directory.CreateDirectory(output); Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(.2f);
            string path = Path.Combine(output, name);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 10 && !File.Exists(path); i++) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
            TestContext.WriteLine("TRAINING CAPTURE " + path);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            pendingDelivery = null; pendingRequest = null; profile = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }
}
