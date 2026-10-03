using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class InitialSummonStoryFlowTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PendingTenPullKey = "witchtower_pending_ten_pull_presentation_v1";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object value, string method, params object[] args) => value.GetType()
            .GetMethod(method, AnyInstance).Invoke(value, args);
        private static object P(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        private static object F(object value, string name) => value.GetType().GetField(name, AnyInstance).GetValue(value);
        private static object Instance(string name) => T(name).GetProperty("Instance").GetValue(null);
        private static object DialogueState(string name) => T("UI.StoryDialogueController").GetProperty(name).GetValue(null);
        private static string IntroId(bool after) => (string)T("Data.StoryDialogueCatalog")
            .GetField(after ? "IntroAfterId" : "IntroBeforeId").GetValue(null);
        private static Component ActiveDialogue() => (Component)UnityEngine.Object.FindObjectOfType(T("UI.StoryDialogueController"));
        private static bool Seen(object profile, string eventId) => (bool)T("Data.StoryTutorialService")
            .GetMethod("HasSeenStory").Invoke(null, new[] { profile, eventId });

        [TestCase("T02", 0, false, false, true)]
        [TestCase("T02", 1, false, false, true)]
        [TestCase("T02", 2, false, true, false)]
        [TestCase("T02A", 2, false, true, false)]
        [TestCase("T02A", 3, false, true, true)]
        [TestCase("T02A", 3, false, false, false)]
        [TestCase("T03", 3, false, true, false)]
        [TestCase("Complete", 3, true, true, false)]
        public void IntroductionOnlySurroundsExistingThreeSingleSummons(string step, int count, bool completed,
            bool after, bool expected)
        {
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("TutorialStepId").SetValue(save, step);
            save.GetType().GetField("InitialTutorialSummonCount").SetValue(save, count);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save, completed);
            object profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            MethodInfo eligible = T("Home.GachaPanelController").GetMethod("ShouldShowInitialSummonDialogue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(eligible.Invoke(null, new[] { profile, (object)after }), Is.EqualTo(expected));
            T("Data.StoryTutorialService").GetMethod("MarkStorySeen").Invoke(null, new[] { profile, IntroId(after) });
            Assert.That(eligible.Invoke(null, new[] { profile, (object)after }), Is.False,
                "Reading or skipping an introduction must not repeat it on the next screen open.");
        }

        [UnityTest]
        public IEnumerator IntroductionResumesAndSkipsWithoutChangingThreeExistingTransactions()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineUiTestGateway.Install();
            string folder = Path.Combine(Path.GetTempPath(), "WitchTowerInitialSummonStory-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            PropertyInfo saveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            bool hadPending = PlayerPrefs.HasKey(PendingTenPullKey);
            string oldPending = PlayerPrefs.GetString(PendingTenPullKey);
            var oldRandom = UnityEngine.Random.state;
            saveOverride.SetValue(null, folder);
            GameObject canvas = null;
            try
            {
                PlayerPrefs.DeleteKey(PendingTenPullKey);
                foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                    T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
                Call(Instance("Managers.MasterDataManager"), "Initialize");
                object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                save.GetType().GetField("TutorialStepId").SetValue(save, "T02");
                save.GetType().GetField("HasCompletedTutorial").SetValue(save, false);
                save.GetType().GetField("InitialTutorialSummonCount").SetValue(save, 0);
                Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
                object profile = P(Instance("Managers.GameManager"), "PlayerProfile");
                canvas = new GameObject("InitialSummonStoryCanvas", typeof(RectTransform), typeof(Canvas));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var panel = new GameObject("InitialSummonStoryPanel", typeof(RectTransform));
                panel.transform.SetParent(canvas.transform, false);
                Component gacha = panel.AddComponent(T("Home.GachaPanelController"));
                int closeCount = 0;
                Call(gacha, "Show", (Action)(() => closeCount++));
                Assert.That(DialogueState("CurrentEventId"), Is.EqualTo(IntroId(false)));
                int beforeOwned = ((IList)P(profile, "OwnedMonsters")).Count;
                int beforeFree = (int)P(profile, "FreeGachaStones");
                int beforePaid = (int)P(profile, "PaidGachaStones");
                for (int i = 0; i < 5; i++)
                {
                    Call(gacha, "RunContract", 1, false);
                    Call(gacha, "RunContract", 10, true);
                    Call(gacha, "Close");
                }
                Assert.That(closeCount, Is.Zero, "Programmatic close must not bypass the active introduction.");
                AssertInventory(profile, beforeOwned, beforeFree, beforePaid);
                yield return new WaitForSecondsRealtime(0.2f);
                Call(ActiveDialogue(), "NextLine");
                yield return new WaitForSecondsRealtime(0.2f);
                Call(ActiveDialogue(), "NextLine");
                int savedLine = (int)P(profile, "StoryDialogueLineIndex");
                Assert.That(savedLine, Is.GreaterThanOrEqualTo(1));
                panel.SetActive(false);
                Assert.That(DialogueState("IsShowing"), Is.False, "Leaving the panel must remove its independent story overlay.");
                Assert.That(Seen(profile, IntroId(false)), Is.False, "Interruption must not mark the introduction as read.");
                Call(gacha, "Show", (Action)(() => closeCount++));
                Assert.That(DialogueState("CurrentEventId"), Is.EqualTo(IntroId(false)));
                Assert.That(P(profile, "StoryDialogueLineIndex"), Is.EqualTo(savedLine));
                yield return new WaitForSecondsRealtime(0.2f);
                Call(ActiveDialogue(), "Skip");
                Assert.That(Seen(profile, IntroId(false)), Is.True);
                AssertInventory(profile, beforeOwned, beforeFree, beforePaid);

                for (int pull = 1; pull <= 3; pull++)
                {
                    Call(gacha, "RunContract", 1, false);
                    AssertInventory(profile, beforeOwned + pull, beforeFree - 300 * pull, beforePaid);
                    Assert.That(P(profile, "InitialTutorialSummonCount"), Is.EqualTo(pull));
                    Assert.That(DialogueState("IsShowing"), Is.False,
                        "After-dialogue must wait for the actual summon reveal, not merely the inventory grant.");
                    float deadline = Time.realtimeSinceStartup + 8f;
                    while ((bool)F(gacha, "contractInProgress") && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(F(gacha, "contractInProgress"), Is.False, "The single-summon cinematic must complete.");
                    if (pull < 3)
                    {
                        Assert.That(DialogueState("IsShowing"), Is.False);
                        Assert.That(((Component)F(gacha, "tenPullPresentation")).transform.Find("TenPullSafeContent/BackHome").gameObject.activeSelf, Is.False,
                            "The first two summons retain the guided route to the next summon.");
                        Call(gacha, "ReturnToContractHome");
                    }
                }

                Assert.That(DialogueState("CurrentEventId"), Is.EqualTo(IntroId(true)));
                for (int i = 0; i < 5; i++) Call(gacha, "RunContract", 1, false);
                AssertInventory(profile, beforeOwned + 3, beforeFree - 900, beforePaid);
                yield return new WaitForSecondsRealtime(0.2f);
                Call(ActiveDialogue(), "Skip");
                Assert.That(Seen(profile, IntroId(true)), Is.True);
                Assert.That(P(profile, "TutorialStepId"), Is.EqualTo("T02A"));
                AssertInventory(profile, beforeOwned + 3, beforeFree - 900, beforePaid);
                var homeButton = ((Component)F(gacha, "tenPullPresentation")).transform.Find("TenPullSafeContent/BackHome").GetComponent<Button>();
                Assert.That(homeButton.gameObject.activeInHierarchy && homeButton.interactable, Is.True);
                Assert.That(((Component)F(gacha, "tenPullPresentation")).transform.Find("TenPullSafeContent/BackToSummon").gameObject.activeSelf, Is.False,
                    "After the third summon the tutorial sends the player home instead of into an unguided summon screen.");
                Call(gacha, "Close");
                Assert.That(closeCount, Is.EqualTo(1));
                TestContext.WriteLine("Initial-summon story test used isolated save: " + folder);
            }
            finally
            {
                OnlineUiTestGateway.Clear();
                T("UI.StoryDialogueController").GetMethod("Dismiss").Invoke(null, null);
                if (canvas != null) UnityEngine.Object.Destroy(canvas);
                saveOverride.SetValue(null, null);
                if (hadPending) PlayerPrefs.SetString(PendingTenPullKey, oldPending);
                else PlayerPrefs.DeleteKey(PendingTenPullKey);
                PlayerPrefs.Save();
                UnityEngine.Random.state = oldRandom;
            }
        }

        private static void AssertInventory(object profile, int owned, int free, int paid)
        {
            Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.EqualTo(owned));
            Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(free));
            Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(paid));
        }

        [UnityTest]
        public IEnumerator DisablingSummonPanelDoesNotDismissAnotherStory()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineUiTestGateway.Install();
            var panel = new GameObject("UnrelatedStoryOwnerTest", typeof(RectTransform));
            Component gacha = panel.AddComponent(T("Home.GachaPanelController"));
            try
            {
                // The panel remembers an interrupted introduction, while a
                // different scene controller now owns a chapter conversation.
                gacha.GetType().GetField("ownedStoryEventId", AnyInstance).SetValue(gacha, IntroId(false));
                Assert.That(T("UI.StoryDialogueController").GetMethod("TryShowPreview")
                    .Invoke(null, new object[] { "story_chapter_2_unlocked", null }), Is.True);
                panel.SetActive(false);
                Assert.That(DialogueState("IsShowing"), Is.True);
                Assert.That(DialogueState("CurrentEventId"), Is.EqualTo("story_chapter_2_unlocked"));
            }
            finally
            {
                OnlineUiTestGateway.Clear();
                T("UI.StoryDialogueController").GetMethod("Dismiss").Invoke(null, null);
                UnityEngine.Object.Destroy(panel);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
