using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class GuardianSanctuaryPresentationTests
    {
        private string saveDirectory;
        private static readonly string[] ChapterIds = {
            "story_chapter_2_unlocked", "story_chapter_3_unlocked", "story_chapter_4_unlocked",
            "story_chapter_5_unlocked", "story_chapter_6_unlocked", "story_first_arc_complete" };
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);
        private static object Profile => P(Instance("Managers.GameManager"), "PlayerProfile");
        private static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name && b.gameObject.activeInHierarchy);
        private static MonoBehaviour Sanctuary => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType() == T("Home.GuardianSanctuaryController") && c.gameObject.activeInHierarchy);

        [TestCase("SanctuaryBackground")]
        [TestCase("PanelFrame")]
        [TestCase("PrimaryButton")]
        [TestCase("TabFrame")]
        [TestCase("SeiryuPortrait")]
        [TestCase("SuzakuPortrait")]
        [TestCase("ByakkoPortrait")]
        [TestCase("GenbuPortrait")]
        [TestCase("ContractSeal")]
        [TestCase("DivineRay")]
        public void SanctuaryUsesImportedNewArtwork(string name)
        {
            var sprite = Resources.Load<Sprite>("UI/GuardiansReborn/" + name);
            Assert.That(sprite, Is.Not.Null, "Generated artwork must be imported as a Sprite: " + name);
            Assert.That(sprite.rect.width, Is.GreaterThanOrEqualTo(128));
            Assert.That(sprite.rect.height, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator BirthIsSavedBeforeAnimationAndSurvivesPauseWithoutDuplicateGrant()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(30, true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            var profile = Profile;
            Assert.That(((IList)P(profile, "GuardianCoreIds")).Contains("seiryu"), Is.True);
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            var primary = Button("GuardianPrimaryAction");
            primary.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianBirthPresentation"), Is.Not.Null);
            Assert.That(((IList)P(profile, "GuardianCoreIds")).Count, Is.EqualTo(0));
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save.json")), Does.Contain("seiryu"));
            for (int i = 0; i < 20; i++) primary.onClick.Invoke();
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.EqualTo(1));
            Sanctuary.SendMessage("OnApplicationPause", true);
            yield return null;
            Assert.That(GameObject.Find("GuardianBirthPresentation"), Is.Null);
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text, Does.Contain("編成"));
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null, "A dismissed introduction remains a badge, not a forced reopen.");
            Assert.That(Button("GuardianSlot").GetComponentInChildren<Text>().text, Does.Contain("編成する"));
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            Button("GuardianPrimaryAction").onClick.Invoke();
            yield return null;
            Assert.That(P(profile, "EquippedGuardianId"), Is.EqualTo("seiryu"));
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save.json")), Does.Contain("\"EquippedGuardianId\": \"seiryu\""));
        }

        [UnityTest]
        public IEnumerator SanctuaryEntryHidesForHomeSubpagesAndStoryMovesToSettings()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(30, false);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            var home = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(x => x.GetType() == T("Home.HomeSceneController"));
            Assert.That(GameObject.Find("StoryArchiveButton"), Is.Null);
            foreach (string method in new[] { "OpenMonsterDexMenu", "OpenGoldShopMenu", "OpenPaidShopMenu" })
            {
                home.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(home, null);
                yield return null; yield return null;
                Assert.That(GameObject.Find("GuardianSlot"), Is.Null, method);
                var close = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .First(x => x.name.Contains("CloseButton") && x.gameObject.activeInHierarchy);
                close.onClick.Invoke();
                yield return null; yield return null;
                Assert.That(GameObject.Find("GuardianSlot"), Is.Not.Null, method);
            }
            Call(home, "OpenAudioSettingsPanel");
            yield return null;
            Assert.That(GameObject.Find("GuardianSlot"), Is.Null);
            var archive = Button("StoryArchiveButton");
            Assert.That(archive.transform.parent.name, Is.EqualTo("AudioSettingsPanel"));
            archive.onClick.Invoke();
            yield return null;
            Assert.That((bool)T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null), Is.True);
        }

        [UnityTest]
        public IEnumerator OptionalGuardianEntryNeverForcesThePageOpenOnHomeReturn()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(30, false);
            T("Data.StoryTutorialService").GetMethod("MarkHintSeen").Invoke(null,
                new[] { Profile, "tutorial_fusion_inheritance" });
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            yield return CaptureIfRequested("home-guardian-entry.png");
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null);
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            Button("CloseSanctuary").onClick.Invoke();
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null);
            Assert.That(Button("GuardianSlot").GetComponentInChildren<Text>().text, Does.Contain("試練で仲間にする"));
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Not.Null);
            Assert.That(((IList)P(Profile, "OwnedGuardians")).Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator FourGuardianPreviewsAndBirthSkipLeaveLiveSaveUnchanged()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(0, false);
            new GameObject("GuardianPreviewEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            string before = Snapshot();
            byte[] savedBefore = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            var previewWindow = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("GuardianSanctuaryPreviewWindow", true);
            var sample = previewWindow.GetMethod("CreateProfile").Invoke(null, new object[] { 6, "seiryu" });
            var view = T("Home.GuardianSanctuaryController").GetMethod("ShowPreview").Invoke(null, new[] { sample, "seiryu" });
            yield return null;
            foreach (string id in new[] { "seiryu", "suzaku", "byakko", "genbu" })
            {
                Button("GuardianCard_" + id).onClick.Invoke();
                yield return null;
                Assert.That(P(view, "SelectedGuardianId"), Is.EqualTo(id));
                var portrait = GameObject.Find("GuardianHeroPortrait").GetComponent<Image>();
                Assert.That(portrait.sprite, Is.Not.Null);
                Assert.That(portrait.preserveAspect, Is.True);
                Assert.That(portrait.rectTransform.rect.width, Is.GreaterThan(850));
                AssertFrameHasContentArea(Button("GuardianCard_" + id).GetComponent<Image>());
                AssertFrameHasContentArea(Button("GuardianPrimaryAction").GetComponent<Image>());
                AssertFrameHasContentArea(GameObject.Find("GuardianGrowth").GetComponent<Image>());
                AssertFrameHasContentArea(GameObject.Find("SanctuaryActionFrame").GetComponent<Image>());
                Assert.That(GameObject.Find("GuardianPracticeGroup"), Is.Null, "Practice has been removed from the player page.");
                Button("GuardianContract_alternate").onClick.Invoke();
                yield return null;
                Assert.That((string)T("Data.GuardianService").GetMethod("ContractId").Invoke(null, new[] { sample, id }), Is.EqualTo("alternate"));
                yield return CaptureIfRequested("sanctuary-" + id + ".png");
            }
            Button("GuardianPerformanceToggle").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianPerformanceStats").GetComponent<Text>().text, Does.Contain("攻撃速度"));
            Assert.That(GameObject.Find("GuardianPassiveValues").GetComponent<Text>().text, Does.Contain("最大HP +8%"));
            GameObject.Find("SanctuaryScroll").GetComponent<ScrollRect>().verticalNormalizedPosition = .40f;
            yield return CaptureIfRequested("sanctuary-performance.png");
            Call(view, "PreviewBirth");
            yield return new WaitForSecondsRealtime(1.8f);
            var born = GameObject.Find("GuardianBirthPortrait").GetComponent<RectTransform>();
            Assert.That(born.localScale.x, Is.EqualTo(born.localScale.y).Within(.0001f), "No anisotropic stretching in the cutscene.");
            yield return CaptureIfRequested("guardian-birth.png");
            for (int i = 0; i < 20; i++) Call(view, "FinishBirthPresentation");
            yield return null;
            Assert.That(GameObject.Find("GuardianBirthPresentation"), Is.Null);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(savedBefore));
            Button("CloseSanctuary").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianSanctuaryPreview"), Is.Null);
        }

        [UnityTest]
        public IEnumerator SelectedGuardianUsesItsOwnLevelExperienceAndUnlocks()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(0, false);
            string before = Snapshot();
            byte[] savedBefore = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            var previewWindow = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                .GetType("GuardianSanctuaryPreviewWindow", true);
            var sample = previewWindow.GetMethod("CreateProfile").Invoke(null, new object[] { 7, "seiryu" });
            T("Home.GuardianSanctuaryController").GetMethod("ShowPreview").Invoke(null, new[] { sample, "seiryu" });
            yield return null;
            int[] levels = { 30, 20, 10, 1 };
            int[] experience = { 0, 35, 90, 40 };
            string[] ids = { "seiryu", "suzaku", "byakko", "genbu" };
            Button("GuardianPerformanceToggle").onClick.Invoke();
            yield return null;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                Button("GuardianCard_" + id).onClick.Invoke();
                yield return null;
                var growth = GameObject.Find("GuardianGrowth").transform;
                Assert.That(growth.Find("Level").GetComponent<Text>().text, Does.Contain("Lv." + levels[i]));
                string expLabel = growth.Find("Experience").GetComponent<Text>().text;
                if (levels[i] == 30) Assert.That(expLabel, Does.Contain("最大レベル"));
                else
                {
                    int required = (int)T("Data.GuardianService").GetMethod("RequiredExp").Invoke(null, new object[] { levels[i] });
                    Assert.That(expLabel, Does.Contain((required - experience[i]) + " EXP"));
                }
                Assert.That(growth.Find("GrowthHint"), Is.Null);
                Assert.That(GameObject.Find("GuardianPerformanceTitle").GetComponent<Text>().text, Does.Contain("Lv." + levels[i]));
                Assert.That(Button("GuardianContract_alternate").interactable, Is.EqualTo(levels[i] >= 10), id);
                Assert.That(GameObject.Find("ConversationHeading"), Is.Null);
                Assert.That(GameObject.Find("GuardianBondSeal") != null, Is.EqualTo(levels[i] >= 20), id);
                Button("GuardianContract_alternate").onClick.Invoke();
                yield return null;
                Assert.That((string)T("Data.GuardianService").GetMethod("ContractId").Invoke(null, new[] { sample, id }),
                    Is.EqualTo(levels[i] >= 10 ? "alternate" : "basic"));
                yield return CaptureIfRequested("sanctuary-level-" + id + ".png");
            }
            var owned = (IList)P(sample, "OwnedGuardians");
            var genbu = owned.Cast<object>().Single(g => (string)g.GetType().GetField("Id").GetValue(g) == "genbu");
            owned.Remove(genbu);
            Button("GuardianCard_genbu").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianGrowth").transform.Find("Level").GetComponent<Text>().text, Does.Contain("Lv.1"));
            Assert.That(GameObject.Find("GuardianGrowth").transform.Find("Experience").GetComponent<Text>().text, Is.Empty);
            yield return CaptureIfRequested("sanctuary-unowned-genbu.png");
            Assert.That(Button("GuardianContract_alternate").interactable, Is.False, "An unowned guardian cannot select an alternate skill type.");
            Assert.That(GameObject.Find("GuardianDialogue_guardian_genbu_birth"), Is.Null);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), Is.EqualTo(savedBefore));
        }

        [UnityTest]
        public IEnumerator RemovedPracticeAndDialogueStayAbsentAndLegacyReadFlagsSurvive()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeGame(30, true);
            T("Data.GuardianService").GetMethod("Birth").Invoke(null, new[] { Profile, "seiryu" });
            ((IList)P(Profile, "SeenGuardianDialogueIds")).Add("guardian_seiryu_birth");
            SceneManager.LoadScene("HomeScene");
            yield return WaitForHome();
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            string before = Snapshot();
            foreach (string id in new[] { "seiryu", "suzaku", "byakko", "genbu" })
            {
                Button("GuardianCard_" + id).onClick.Invoke();
                yield return null;
                var labels = Sanctuary.GetComponentsInChildren<Text>();
                Assert.That(labels.Any(t => t.text.Contains("体験") || t.text.Contains("対話")), Is.False);
                Assert.That(Sanctuary.GetComponentsInChildren<Button>().Any(b =>
                    b.name.StartsWith("GuardianPractice") || b.name.StartsWith("GuardianDialogue_")), Is.False);
                Assert.That(GameObject.Find("GuardianProgressGuide"), Is.Null);
                yield return CaptureIfRequested("guardian-guide-" + id + ".png");
            }
            Button("CloseSanctuary").onClick.Invoke();
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(((IList)P(Profile, "SeenGuardianDialogueIds")).Cast<string>(), Is.EqualTo(new[] { "guardian_seiryu_birth" }));
        }

        private static void AssertFrameHasContentArea(Image image)
        {
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.preserveAspect, Is.False, image.name + " must fill its target rectangle.");
            Assert.That(image.sprite, Is.Not.Null);
            float unitsPerPixel = 100f / image.sprite.pixelsPerUnit / image.pixelsPerUnitMultiplier;
            float centreHeight = image.rectTransform.rect.height -
                (image.sprite.border.y + image.sprite.border.w) * unitsPerPixel;
            Assert.That(centreHeight, Is.GreaterThan(40), image.name + " must retain a text area between its upper and lower frame corners.");
        }

        private void InitializeGame(int floor, bool core)
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerGuardianPresentation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, saveDirectory);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" }) T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            Set(save, "HighestFloor", floor); Set(save, "CurrentFloor", floor + 1);
            Set(save, "HasCompletedTutorial", true); Set(save, "TutorialStepId", "Complete");
            var seen = (IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save);
            foreach (string id in ChapterIds) seen.Add(id);
            seen.Add("story_guardian_trial_intro");
            if (core) ((IList)save.GetType().GetField("GuardianCoreIds").GetValue(save)).Add("seiryu");
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
        }

        private static string Snapshot() => JsonUtility.ToJson(Call(Profile, "ToSaveData", P(Instance("Managers.GameManager"), "CurrentFloor")));
        private static IEnumerator WaitForHome()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != "HomeScene" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("HomeScene"));
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
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        }
    }
}
