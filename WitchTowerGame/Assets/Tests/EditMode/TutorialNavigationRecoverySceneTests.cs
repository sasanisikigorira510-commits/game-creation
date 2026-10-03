using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class TutorialNavigationRecoverySceneTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string BeforeSummon = "story_first_summon_iona_before";
        private const string PendingTenPullKey = "witchtower_pending_ten_pull_presentation_v1";
        private string saveDirectory;
        private bool preferencesCaptured;
        private bool hadPendingTenPull;
        private string pendingTenPull;
        private UnityEngine.Random.State randomState;
        private bool storageOverrideCaptured, gatewayCaptured, saveDirectoryCreated;
        private object previousSaveDirectory, previousBindingRequired, previousGateway;

        [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
        private static extern int MakePrivateDirectory(string path, uint mode);

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Instance(string name) => T(name).GetProperty("Instance").GetValue(null);
        private static object P(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static object F(object owner, string name) => owner.GetType().GetField(name, AnyInstance).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) => owner.GetType()
            .GetMethod(name, AnyInstance).Invoke(owner, args);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name).SetValue(owner, value);
        private static object Profile => P(Instance("Managers.GameManager"), "PlayerProfile");
        private static bool IsShowing => (bool)T("UI.StoryDialogueController").GetProperty("IsShowing").GetValue(null);
        private static string CurrentEvent => (string)T("UI.StoryDialogueController").GetProperty("CurrentEventId").GetValue(null);
        private static MonoBehaviour Controller(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType() == T(name) && c.gameObject.activeInHierarchy);
        private static Button ActiveButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(b => b.name == name && b.gameObject.activeInHierarchy);

        [UnityTest]
        public IEnumerator SavedInitialSummonConversationCanReturnFromHomeTwiceWithoutChangingProgressOrInventory()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InstallIsolatedGateway();
            InitializeIsolatedGame(0, savedConversationLine: 5);
            SaveAndReload();
            string inventory = SummonSnapshot();

            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            for (int visit = 0; visit < 2; visit++)
            {
                AssertHomeSummonResumeGuide();
                Assert.That(SummonSnapshot(), Is.EqualTo(inventory), "Returning home must not perform a summon or advance its lesson.");
                if (visit == 0) yield return Capture("home-resume-first-summon.png");
                yield return Click(ActiveButton("GachaButton"));
                yield return WaitForScene("GachaScene");
                Assert.That(CurrentEvent, Is.EqualTo(BeforeSummon));
                var dialogue = Controller("UI.StoryDialogueController");
                Assert.That(P(dialogue, "CurrentLineIndex"), Is.EqualTo(5));
                Assert.That(P(Profile, "StoryDialogueLineIndex"), Is.EqualTo(5));
                Assert.That(GameObject.Find("StoryDialogueCounter").GetComponent<Text>().text, Is.EqualTo("6 / 6"));
                Assert.That(GameObject.Find("StoryDialogueSpeaker").GetComponent<Text>().text, Is.EqualTo("イオナ"));
                Assert.That(((IList)P(Profile, "SeenStoryEventIds")).Contains(BeforeSummon), Is.False);
                Assert.That(SummonSnapshot(), Is.EqualTo(inventory));
                if (visit == 0) yield return Capture("gacha-resumed-iona-line-six.png");

                if (visit == 0)
                {
                    // The actual scene exit is also used by the back/Escape path;
                    // it must dismiss the scene-owned dialogue without completing it.
                    Call(Controller("Home.GachaSceneController"), "ReturnHome");
                    yield return WaitForScene("HomeScene");
                    Assert.That(IsShowing, Is.False);
                    Assert.That(P(Profile, "StoryDialogueEventId"), Is.EqualTo(BeforeSummon));
                    Assert.That(P(Profile, "StoryDialogueLineIndex"), Is.EqualTo(5));
                }
            }

            // A recovered conversation remains interactive, including its visible
            // skip control and the single-summon action it hands back to.
            yield return new WaitForSecondsRealtime(.2f);
            yield return Click(ActiveButton("StoryDialogueSkip"));
            yield return null;
            Assert.That(IsShowing, Is.False);
            Assert.That(((Button)F(Controller("Home.GachaPanelController"), "singlePullButton")).interactable, Is.True);
            Assert.That(SummonSnapshot(), Is.EqualTo(inventory));
            TestContext.WriteLine("T02 saved intro line 5 -> Home -> Gacha -> Home -> Gacha: resumed through raycast-checked controls. Isolated save: " + saveDirectory);
        }

        [UnityTest]
        public IEnumerator OneOrTwoCompletedSummonsCanResumeFromHomeAndOnlyGrantTheNextSummon()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            InstallIsolatedGateway();
            for (int completed = 1; completed <= 2; completed++)
            {
                InitializeIsolatedGame(completed);
                SaveAndReload();
                string inventory = SummonSnapshot();
                string[] previousInstances = ((IList)P(Profile, "OwnedMonsters")).Cast<object>()
                    .Select(monster => (string)F(monster, "InstanceId")).ToArray();
                SceneManager.LoadScene("HomeScene");
                yield return WaitForScene("HomeScene");
                AssertHomeSummonResumeGuide();
                yield return Click(ActiveButton("GachaButton"));
                yield return WaitForScene("GachaScene");
                Assert.That(IsShowing, Is.False, "A previously read introduction must not repeat.");
                Assert.That(SummonSnapshot(), Is.EqualTo(inventory), "Navigation must not re-grant saved summons or spend stones.");
                var gacha = Controller("Home.GachaPanelController");
                yield return Click((Button)F(gacha, "singlePullButton"));
                Assert.That(P(Profile, "InitialTutorialSummonCount"), Is.EqualTo(completed + 1),
                    "A fresh pointer press must start exactly one resumed summon.");
                float deadline = Time.realtimeSinceStartup + 8f;
                while ((bool)F(gacha, "contractInProgress") && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(F(gacha, "contractInProgress"), Is.False, "The resumed single summon must reach its result.");
                var owned = ((IList)P(Profile, "OwnedMonsters")).Cast<object>().ToArray();
                Assert.That(owned.Length, Is.EqualTo(completed + 1));
                Assert.That(P(Profile, "InitialTutorialSummonCount"), Is.EqualTo(completed + 1));
                Assert.That(P(Profile, "FreeGachaStones"), Is.EqualTo(900 - 300 * (completed + 1)));
                Assert.That(P(Profile, "PaidGachaStones"), Is.Zero);
                CollectionAssert.IsSubsetOf(previousInstances, owned.Select(monster => (string)F(monster, "InstanceId")).ToArray());
                Assert.That(P(Profile, "TutorialStepId"), Is.EqualTo(completed == 2 ? "T02A" : "T02"));
                if (completed == 2)
                    Assert.That(CurrentEvent, Is.EqualTo("story_first_summon_iona_after"));
                else
                    Assert.That(((Component)F(gacha, "tenPullPresentation")).transform.Find("TenPullSafeContent/BackToSummon").GetComponent<Button>().isActiveAndEnabled, Is.True);
            }
        }

        private void InitializeIsolatedGame(int completedSummons, int? savedConversationLine = null)
        {
            if (!preferencesCaptured)
            {
                preferencesCaptured = true;
                hadPendingTenPull = PlayerPrefs.HasKey(PendingTenPullKey);
                pendingTenPull = PlayerPrefs.GetString(PendingTenPullKey);
                randomState = UnityEngine.Random.state;
            }
            PlayerPrefs.DeleteKey(PendingTenPullKey);
            if (!storageOverrideCaptured)
            {
                if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.LinuxEditor)
                    Assert.Ignore("A private POSIX fixture directory is required.");
                var saveType = T("Managers.SaveManager");
                var rootOverride = saveType.GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
                var bindingOverride = saveType.GetField("EditorNamespaceBindingRequiredOverride", BindingFlags.Static | BindingFlags.NonPublic);
                previousSaveDirectory = rootOverride.GetValue(null);
                previousBindingRequired = bindingOverride.GetValue(null);
                storageOverrideCaptured = true;
                saveDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerTutorialNavigation-" + Guid.NewGuid().ToString("N"));
                Assert.That(MakePrivateDirectory(saveDirectory, 448), Is.Zero, "Cannot create the private 0700 save fixture.");
                saveDirectoryCreated = true;
                rootOverride.SetValue(null, saveDirectory);
                bindingOverride.SetValue(null, false);
            }
            // The two synthetic progress scenarios use the same immutable
            // session root. Changing the global override after SaveManager.Awake
            // would correctly stop Home before it constructs the guide.
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Assert.That(P(Instance("Managers.SaveManager"), "StorageAccessAvailable"), Is.True,
                "Each synthetic starting state must keep the original session storage context.");
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            Set(save, "TutorialStepId", "T02");
            Set(save, "HasCompletedTutorial", false);
            Set(save, "InitialTutorialSummonCount", completedSummons);
            Set(save, "FreeGachaStones", 900 - completedSummons * 300);
            Set(save, "StoryDialogueEventId", savedConversationLine.HasValue ? BeforeSummon : string.Empty);
            Set(save, "StoryDialogueLineIndex", savedConversationLine ?? 0);
            var seen = (IList)F(save, "SeenStoryEventIds");
            seen.Add("story_prologue_wakeup");
            if (!savedConversationLine.HasValue) seen.Add(BeforeSummon);
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            // This is a presentation/navigation fixture, not a live server test.
            // Use isolated authenticated deliveries and suppress periodic HTTP
            // sync so a real online input blocker cannot race the test taps.
            var online = (MonoBehaviour)T("Save.OnlinePlayerData").GetMethod("Ensure").Invoke(null, null);
            online.enabled = false;
            string[] starterIds = { "monster_dragon_whelp", "monster_rock_golem" };
            for (int i = 0; i < completedSummons; i++)
                Call(Profile, "AddOwnedMonster", starterIds[i], 1, 0, false);
        }

        private void InstallIsolatedGateway()
        {
            if (!gatewayCaptured)
            {
                previousGateway = T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                gatewayCaptured = true;
            }
            OnlineUiTestGateway.Install();
        }

        private static void SaveAndReload()
        {
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");
            Call(Instance("Managers.SaveManager"), "LoadOrCreate");
            Call(Instance("Managers.GameManager"), "InitializeFromSave", P(Instance("Managers.SaveManager"), "CurrentSaveData"));
        }

        private static void AssertHomeSummonResumeGuide()
        {
            Assert.That(IsShowing, Is.False, "The intro belongs to the summon scene, so Home must expose its resume route.");
            object tutorial = T("Data.StoryTutorialService").GetMethod("GetNextEvent").Invoke(null, new[] { Profile, "HomeScene" });
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(P(tutorial, "TargetKey"), Is.EqualTo("home.gacha"));
            var guide = (Text)F(Controller("Home.HomeSceneController"), "homeGuideText");
            Assert.That(guide, Is.Not.Null, "Home must have built its guide under the available immutable storage context.");
            Assert.That(guide.isActiveAndEnabled, Is.True);
            Assert.That(guide.text, Is.Not.Empty);
            var summon = ActiveButton("GachaButton");
            Assert.That(summon.isActiveAndEnabled && summon.interactable, Is.True);
        }

        private static string SummonSnapshot()
        {
            string monsters = string.Join("|", ((IList)P(Profile, "OwnedMonsters")).Cast<object>()
                .Select(monster => JsonUtility.ToJson(monster)).ToArray());
            return P(Profile, "TutorialStepId") + ":" + P(Profile, "InitialTutorialSummonCount") + ":" +
                P(Profile, "FreeGachaStones") + ":" + P(Profile, "PaidGachaStones") + ":" + monsters;
        }

        private static IEnumerator Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            GameObject handler = null;
            PointerEventData pointer = null;
            bool sampled = false;
            string raycastDetails = string.Empty;
            Canvas.WillRenderCanvases sample = () =>
            {
                // Match the established portrait Game-view test configuration.
                // Sampling from an EditMode coroutine instead uses editor dimensions.
                if (sampled || Screen.width != 1080 || Screen.height != 1920) return;
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
                raycastDetails = "Point=" + pointer.position + "; rect=" + rect.rect + "; scale=" + rect.lossyScale +
                    "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name));
                sampled = true;
            };
            Canvas.willRenderCanvases += sample;
            try
            {
                float until = Time.realtimeSinceStartup + 3f;
                while (!sampled && Time.realtimeSinceStartup < until) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(sampled, Is.True, "Need a real game-view render to check the touch target.");
            Assert.That(handler, Is.EqualTo(button.gameObject), button.name + " must not be covered by the guide or another modal. " + raycastDetails);
            // Summon controls intentionally reject a click without a new press
            // (FreshPressButton prevents the screen-opening touch from summoning).
            // Exercise the same down/up/click sequence that a real tap produces.
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator WaitForScene(string name)
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static IEnumerator Capture(string filename)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../tools/reports/tutorial_navigation_recovery_20260918/captures"));
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, filename));
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying)
            {
                T("UI.StoryDialogueController").GetMethod("Dismiss").Invoke(null, null);
                if (preferencesCaptured)
                {
                    if (hadPendingTenPull) PlayerPrefs.SetString(PendingTenPullKey, pendingTenPull);
                    else PlayerPrefs.DeleteKey(PendingTenPullKey);
                    PlayerPrefs.Save();
                    UnityEngine.Random.state = randomState;
                }
                // Keep the isolated save override until scene/manager shutdown.
                yield return new ExitPlayMode();
            }
            try
            {
                if (gatewayCaptured)
                    T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousGateway);
            }
            finally
            {
                gatewayCaptured = false;
                try
                {
                    if (storageOverrideCaptured)
                    {
                        T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousSaveDirectory);
                        T("Managers.SaveManager").GetField("EditorNamespaceBindingRequiredOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousBindingRequired);
                    }
                }
                finally
                {
                    storageOverrideCaptured = false;
                    preferencesCaptured = false;
                    if (saveDirectoryCreated && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
                    saveDirectoryCreated = false;
                    saveDirectory = null;
                }
            }
        }
    }
}
