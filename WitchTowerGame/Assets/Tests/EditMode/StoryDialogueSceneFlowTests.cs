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
    public sealed class StoryDialogueSceneFlowTests
    {
        private const string ChapterOne = "story_chapter_2_unlocked";
        private const string ChapterThree = "story_chapter_4_unlocked";
        private const string Finale = "story_first_arc_complete";
        private static readonly string[] ChapterIds =
        {
            ChapterOne, "story_chapter_3_unlocked", ChapterThree,
            "story_chapter_5_unlocked", "story_chapter_6_unlocked", Finale
        };
        private string saveDirectory;

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Instance(string name) => T(name).GetProperty("Instance").GetValue(null);
        private static object P(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name).SetValue(owner, value);
        private static object Profile => P(Instance("Managers.GameManager"), "PlayerProfile");
        private static bool IsShowing => (bool)T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null);
        private static string CurrentEvent => (string)T("UI.StoryDialogueController").GetProperty("CurrentEventId").GetValue(null);
        private static MonoBehaviour Dialogue => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType() == T("UI.StoryDialogueController") && c.gameObject.activeInHierarchy);
        private static bool HasSeen(string id) => ((IList)P(Profile, "SeenStoryEventIds")).Contains(id);
        private static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(b => b.name == name && b.gameObject.activeInHierarchy);

        [UnityTest]
        public IEnumerator ChapterThreeEndsOnHomeAndGuardianOpensOnlyWhenRequested()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(30, 2, false);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForStory(ChapterThree);

            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null,
                "Read the chapter story before the first guardian lesson.");
            Assert.That(HasSeen(ChapterThree), Is.False, "Opening dialogue must not mark it read.");
            Assert.That((bool)T("UI.StoryDialogueController").GetMethod("TryShowPreview").Invoke(null,
                new object[] { Finale, null }), Is.False, "A preview must not replace a live chapter.");
            Assert.That(CurrentEvent, Is.EqualTo(ChapterThree));
            yield return CaptureIfRequested("chapter-03-before-guardian.png");
            yield return new WaitForSecondsRealtime(.25f);
            Button("StoryDialogueSkip").onClick.Invoke();
            for (int i = 0; i < 12; i++) yield return null;

            Assert.That(HasSeen(ChapterThree), Is.True);
            yield return WaitForStory("story_guardian_trial_intro");
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null);
            yield return CaptureIfRequested("guardian-story-intro.png");
            yield return new WaitForSecondsRealtime(.25f);
            Button("StoryDialogueSkip").onClick.Invoke();
            yield return null;
            Assert.That(IsShowing, Is.False);
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null,
                "Story completion must not force the guardian page open.");
            Assert.That(Button("GuardianSlot").GetComponentInChildren<Text>().text, Does.Contain("試練で仲間にする"));
            Button("GuardianSlot").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Not.Null);
            Assert.That(GameObject.Find("GuardianProgressGuide"), Is.Null);
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text, Does.Contain("神獣を選んで"));
            Button("GuardianCard_seiryu").onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text, Does.Contain("試練へ"));
            Assert.That(Button("GuardianPrimaryAction").transform.Find("TutorialTargetFrame").gameObject.activeSelf, Is.True);
            yield return CaptureIfRequested("guardian-trial-guide.png");
            Button("CloseSanctuary").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("GuardianSanctuaryModal"), Is.Null);
            Button("GuardianSlot").onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text, Does.Contain("神獣を選んで"));
            Assert.That(Button("GuardianCard_seiryu").transform.Find("TutorialTargetFrame").gameObject.activeSelf, Is.True);
            Button("CloseSanctuary").onClick.Invoke(); yield return null;
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            Assert.That(IsShowing, Is.False, "Introduction does not repeat after closing the sanctuary.");
            Assert.That(Button("GuardianSlot").transform.Find("TutorialTargetFrame").gameObject.activeSelf, Is.True);
            Assert.That((string)P(Profile, "StoryDialogueEventId"), Is.Empty);
            Assert.That(((IList)P(Profile, "SeenStoryEventIds")).Cast<string>().Count(id => id == ChapterThree), Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save.json")), Does.Contain(ChapterThree));
            yield return CaptureIfRequested("chapter-03-guardian-handoff.png");
        }

        [UnityTest]
        public IEnumerator FinaleSkipKeepsGameAssetsAndDoesNotReplayOnNextHomeVisit()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(60, 5, true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForStory(Finale);
            string assetsBefore = GameplaySnapshot();
            yield return CaptureIfRequested("chapter-06-home.png");
            yield return new WaitForSecondsRealtime(.25f);
            var controller = Dialogue;
            for (int i = 0; i < 30; i++) Call(controller, "Skip");
            for (int i = 0; i < 8; i++) yield return null;

            Assert.That(IsShowing, Is.False);
            Assert.That(HasSeen(Finale), Is.True);
            Assert.That(GameplaySnapshot(), Is.EqualTo(assetsBefore),
                "The ending changes dialogue progress only, not monsters, equipment, currency or floors.");
            Assert.That(((IList)P(Profile, "SeenStoryEventIds")).Cast<string>().Count(id => id == Finale), Is.EqualTo(1));

            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            Assert.That(IsShowing, Is.False, "Completed chapters must not repeat during continued exploration.");
            Assert.That(GameplaySnapshot(), Is.EqualTo(assetsBefore));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("HomeScene"));
        }

        [UnityTest]
        public IEnumerator EveryEditorPreviewLeavesProfileAndSaveBytesUnchanged()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(0, 0, false);
            string profileBefore = FullProfileSnapshot();
            byte[] fileBefore = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            var definitions = (IEnumerable)T("Data.StoryDialogueCatalog").GetProperty("All").GetValue(null);
            int count = 0;
            foreach (var definition in definitions)
            {
                string id = (string)P(definition, "EventId");
                Assert.That((bool)T("UI.StoryDialogueController").GetMethod("TryShowPreview")
                    .Invoke(null, new object[] { id, null }), Is.True, id);
                Assert.That(CurrentEvent, Is.EqualTo(id));
                yield return new WaitForSecondsRealtime(.25f);
                Call(Dialogue, "NextLine");
                yield return new WaitForSecondsRealtime(.25f);
                Call(Dialogue, "Skip");
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(IsShowing, Is.False, id);
                Assert.That(FullProfileSnapshot(), Is.EqualTo(profileBefore), id + " profile");
                CollectionAssert.AreEqual(fileBefore, File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")), id + " save file");
                count++;
            }
            Assert.That(count, Is.EqualTo(9), "Two summon segments, six chapters and the guardian introduction.");
        }

        [UnityTest]
        public IEnumerator InterruptedChapterResumesSavedLineWithoutMarkingItRead()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(10, 0, false);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForStory(ChapterOne);
            yield return new WaitForSecondsRealtime(.25f);
            Call(Dialogue, "NextLine");
            yield return new WaitForSecondsRealtime(.25f);
            if ((int)P(Profile, "StoryDialogueLineIndex") == 0) Call(Dialogue, "NextLine");
            int savedLine = (int)P(Profile, "StoryDialogueLineIndex");
            Assert.That(savedLine, Is.EqualTo(1), "A tap first completes the current text, then advances once.");
            Assert.That(HasSeen(ChapterOne), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save.json")), Does.Contain("\"StoryDialogueLineIndex\": 1"));
            yield return CaptureIfRequested("chapter-01-in-progress.png");

            // Destroy the real home scene, then reconstruct the profile from disk.
            // This exercises interruption without resetting the player's real save.
            Scene previousHome = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("StoryDialogueInterruption");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(previousHome);
            yield return null;
            Assert.That(IsShowing, Is.False, "No dialogue overlay should survive an unrelated scene.");
            Call(Instance("Managers.SaveManager"), "LoadOrCreate");
            Call(Instance("Managers.GameManager"), "InitializeFromSave", P(Instance("Managers.SaveManager"), "CurrentSaveData"));
            Assert.That((int)P(Profile, "StoryDialogueLineIndex"), Is.EqualTo(savedLine));
            SceneManager.LoadScene("HomeScene");
            yield return WaitForStory(ChapterOne);
            Assert.That((int)P(Profile, "StoryDialogueLineIndex"), Is.EqualTo(savedLine));
            Assert.That((int)P(Dialogue, "CurrentLineIndex"), Is.EqualTo(savedLine));
            Assert.That(GameObject.Find("StoryDialogueCounter").GetComponent<Text>().text, Is.EqualTo("2 / 16"));
            Assert.That(GameObject.Find("StoryDialogueSpeaker").GetComponent<Text>().text, Is.EqualTo("ルシェ"));
            Assert.That(HasSeen(ChapterOne), Is.False);
            Assert.That(Button("StoryDialogueNext").interactable, Is.True);
            yield return CaptureIfRequested("chapter-01-resumed.png");
        }

        [UnityTest]
        public IEnumerator HomeArchiveButtonsReplayStoryAndDismissWithoutChangingSave()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(60, 6, true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            Assert.That(IsShowing, Is.False);
            yield return ClickVisibleButton(Button("AudioSettingsButton"));
            var archiveButton = Button("StoryArchiveButton");
            Assert.That(archiveButton.GetComponentsInChildren<Text>().Any(t => t.text == "物語を読み返す"), Is.True);
            string profileBefore = FullProfileSnapshot();
            string gameplayBefore = GameplaySnapshot();
            byte[] savedBefore = File.ReadAllBytes(Path.Combine(saveDirectory, "save.json"));
            yield return CaptureIfRequested("story-home.png");

            yield return ClickVisibleButton(archiveButton);
            Assert.That(IsShowing, Is.True);
            Assert.That(GameObject.Find("StoryArchiveTitle").GetComponent<Text>().text, Is.EqualTo("冒険の記録"));
            var entries = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.name.StartsWith("StoryArchive_", StringComparison.Ordinal) && b.gameObject.activeInHierarchy).ToArray();
            Assert.That(entries.Length, Is.EqualTo(9));
            Assert.That(entries.All(b => b.interactable), Is.True, "All chapters are available after the first arc.");
            yield return CaptureIfRequested("story-archive.png");
            yield return new WaitForSecondsRealtime(.25f);

            var scroll = GameObject.Find("StoryArchiveViewport").GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            yield return SwipeArchiveToBottom(scroll);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.01f));
            yield return ClickVisibleButton(Button("StoryArchive_" + Finale));
            Assert.That(CurrentEvent, Is.EqualTo(Finale));
            yield return new WaitForSecondsRealtime(.20f);
            yield return ClickVisibleButton(Button("StoryDialogueNext"));
            var definition = T("Data.StoryDialogueCatalog").GetMethod("Find").Invoke(null, new object[] { Finale });
            var lines = (Array)P(definition, "Lines");
            int shownLine = (int)P(Dialogue, "CurrentLineIndex");
            string fullText = (string)P(lines.GetValue(shownLine), "Text");
            // If the natural reveal finished during a slow render, the tap
            // advances normally; use the next deliberate tap to reveal that line.
            if (GameObject.Find("StoryDialogueBody").GetComponent<Text>().text != fullText)
            {
                yield return new WaitForSecondsRealtime(.20f);
                yield return ClickVisibleButton(Button("StoryDialogueNext"));
            }
            Assert.That(GameObject.Find("StoryDialogueBody").GetComponent<Text>().text, Is.EqualTo(fullText));
            Assert.That(FullProfileSnapshot(), Is.EqualTo(profileBefore));
            Assert.That(GameplaySnapshot(), Is.EqualTo(gameplayBefore));
            CollectionAssert.AreEqual(savedBefore, File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")));
            yield return CaptureIfRequested("story-archive-finale.png");

            yield return new WaitForSecondsRealtime(.20f);
            yield return ClickVisibleButton(Button("StoryDialogueSkip"));
            for (int i = 0; i < 3; i++) yield return null;
            scroll = GameObject.Find("StoryArchiveViewport").GetComponent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.01f), "Returning from a replay retains the list position.");
            yield return CaptureIfRequested("story-archive-scrolled.png");
            yield return new WaitForSecondsRealtime(.20f);
            yield return ClickVisibleButton(Button("StoryArchiveClose"));

            T("UI.StoryDialogueController").GetMethod("Dismiss").Invoke(null, null);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(IsShowing, Is.False, "An interruption must dismiss the replay without reopening its archive.");
            Assert.That(GameObject.Find("StoryDialogueOverlay"), Is.Null);
            Assert.That(GameObject.Find("StoryArchiveTitle"), Is.Null);
            yield return ClickVisibleButton(Button("AudioSettingsButton"));
            Assert.That(Button("StoryArchiveButton").interactable, Is.True);
            yield return ClickVisibleButton(Button("AudioSettingsCloseButton"));
            Assert.That(FullProfileSnapshot(), Is.EqualTo(profileBefore));
            Assert.That(GameplaySnapshot(), Is.EqualTo(gameplayBefore));
            CollectionAssert.AreEqual(savedBefore, File.ReadAllBytes(Path.Combine(saveDirectory, "save.json")));
        }

        private void InitializeIsolatedGame(int highestFloor, int readChapters, bool guardianLessonComplete)
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerStoryFlow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, saveDirectory);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            Set(save, "HighestFloor", highestFloor);
            Set(save, "CurrentFloor", highestFloor + 1);
            Set(save, "HasCompletedTutorial", true);
            Set(save, "TutorialStepId", "Complete");
            Set(save, "InitialTutorialSummonCount", 3);
            Set(save, "FreeGachaStones", 1234);
            Set(save, "PaidGachaStones", 567);
            Set(save, "Gold", 9876);
            var seen = (IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save);
            for (int i = 0; i < readChapters; i++) seen.Add(ChapterIds[i]);
            if (guardianLessonComplete)
            {
                seen.Add("story_guardian_trial_intro");
                ((IList)save.GetType().GetField("SeenTutorialHintIds").GetValue(save)).Add("guardian_first_formation");
            }
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            Call(Profile, "AddOwnedMonster", "monster_rock_golem", 10, 0, false);
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
            TestContext.WriteLine("Isolated story save: " + saveDirectory);
        }

        [UnityTest]
        public IEnumerator CompletedArcOpensAtlasAndReturnsToSixDungeonsWithoutChangingProgress()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            InitializeIsolatedGame(60,6,true);
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            string before=FullProfileSnapshot();
            var home=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(c=>c.GetType()==T("Home.HomeSceneController"));
            Call(home,"StartBattle");yield return null;
            var atlasType=T("UI.WorldAtlasController");
            Assert.That(atlasType.GetProperty("IsShowing").GetValue(null),Is.True);
            Assert.That(GameObject.Find("WorldAtlasOverlay").GetComponent<Canvas>().isRootCanvas,Is.True,
                "The live map must use a screen-sized root canvas, not a tiny nested canvas.");
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("HomeScene"));
            yield return CaptureIfRequested("world-atlas-live.png");
            yield return ClickVisibleButton(Button("AtlasBack"));
            yield return null;
            Assert.That(atlasType.GetProperty("IsShowing").GetValue(null),Is.False);
            Assert.That(Button("StartBattleButton").interactable,Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Count(b=>b.name.StartsWith("DungeonMapNode_") && b.gameObject.activeInHierarchy),Is.EqualTo(6));
            yield return ClickVisibleButton(Button("OpenWorldAtlas"));yield return null;
            Assert.That(atlasType.GetProperty("IsShowing").GetValue(null),Is.True);
            var panel=GameObject.Find("DungeonSelectionPanel");panel.SetActive(false);yield return null;
            Assert.That(atlasType.GetProperty("IsShowing").GetValue(null),Is.False);
            panel.SetActive(true);yield return null;
            yield return ClickVisibleButton(Button("OpenWorldAtlas"));yield return null;
            Assert.That(atlasType.GetProperty("IsShowing").GetValue(null),Is.True,"Leaving the screen must not leave a blocking overlay.");
            Assert.That(FullProfileSnapshot(),Is.EqualTo(before));
            yield return ClickVisibleButton(Button("AtlasBack"));
        }

        private static string FullProfileSnapshot() => JsonUtility.ToJson(
            Call(Profile, "ToSaveData", P(Instance("Managers.GameManager"), "CurrentFloor")));

        private static string GameplaySnapshot()
        {
            var snapshot = Call(Profile, "ToSaveData", P(Instance("Managers.GameManager"), "CurrentFloor"));
            Set(snapshot, "StoryDialogueEventId", string.Empty);
            Set(snapshot, "StoryDialogueLineIndex", 0);
            Set(snapshot, "SeenStoryEventIds", new List<string>());
            return JsonUtility.ToJson(snapshot);
        }

        private static IEnumerator WaitForStory(string id)
        {
            yield return WaitForScene("HomeScene");
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((!IsShowing || CurrentEvent != id) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(IsShowing, Is.True, "Expected story " + id);
            Assert.That(CurrentEvent, Is.EqualTo(id));
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static IEnumerator CaptureIfRequested(string filename)
        {
            string directory = Environment.GetEnvironmentVariable("WITCHTOWER_STORY_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2f);
            // ScreenCapture uses the active Game view dimensions; the caller may
            // select a portrait resolution without modifying player preferences.
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, filename));
            for (int i = 0; i < 3; i++) yield return null;
        }

        private static IEnumerator SwipeArchiveToBottom(ScrollRect scroll)
        {
            PointerEventData pointer = null;
            GameObject handler = null;
            float distance = 0;
            Canvas.WillRenderCanvases sample = () =>
            {
                if (pointer != null || Screen.height <= Screen.width) return;
                var rect = scroll.viewport;
                pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) : null;
                distance = Screen.height * .8f;
            };
            Canvas.willRenderCanvases += sample;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3f;
                while (pointer == null && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(pointer, Is.Not.Null);
            Assert.That(handler, Is.EqualTo(scroll.gameObject), "Dragging over an archive row reaches the scroll view.");
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.beginDragHandler);
            pointer.position += Vector2.up * distance;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.endDragHandler);
            scroll.StopMovement();
            yield return null;
        }

        private static IEnumerator ClickVisibleButton(Button button)
        {
            yield return null;
            yield return null;
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            Assert.That(EventSystem.current, Is.Not.Null);
            bool sampled = false;
            GameObject handler = null;
            PointerEventData pointer = null;
            Canvas.WillRenderCanvases sample = () =>
            {
                // EditMode coroutines can observe the editor window dimensions.
                // Sample the actual portrait Game view during its render instead.
                if (sampled || Screen.height <= Screen.width) return;
                var rect = (RectTransform)button.transform;
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                sampled = true;
            };
            Canvas.willRenderCanvases += sample;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3f;
                while (!sampled && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(sampled, Is.True, "Need a portrait Game view render to check the real touch target.");
            Assert.That(handler, Is.EqualTo(button.gameObject), button.name + " must be the topmost clickable target.");
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying) yield return new ExitPlayMode();
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
        }
    }
}
