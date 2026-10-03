using System;
using System.Collections;
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
    public sealed class GuardianSceneFlowTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);
        private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method).Invoke(obj,args);
        private static object P(object obj,string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name && b.gameObject.activeInHierarchy);

        [Test]
        public void HomeMenuDoesNotUseTheFirstUnrelatedCanvasAsItsParent()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var first = new GameObject("First", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var second = new GameObject("Second", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            // Reproduce either instance-ID order seen after a scene reload.
            var unrelated = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            unrelated.name = "GuardianSanctuary";
            var homeCanvas = (unrelated.gameObject == first ? second : first).GetComponent<Canvas>();
            homeCanvas.name = "HomeCanvas";
            var owner = new GameObject("HomeControllerForCanvasTest");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent(T("Home.HomeSceneController"));
                controller.GetType().GetMethod("BuildUnifiedMenu", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                Assert.That(homeCanvas.transform.Find("UnifiedHomeMenu"), Is.Not.Null,
                    "The opaque village background must belong to HomeCanvas.");
                Assert.That(unrelated.transform.Find("UnifiedHomeMenu"), Is.Null,
                    "Home must not cover the sanctuary entry or modal with its background.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [UnityTest]
        public IEnumerator TrialToBirthToFormationWorksAcrossRealScenesAndSaves()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder=Path.Combine(Path.GetTempPath(),"WitchTowerGuardianFlow-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,folder);
            var factory=T("Core.ManagerFactory");
            foreach(var method in new[]{"EnsureGameManager","EnsureSaveManager","EnsureMasterDataManager"}) factory.GetMethod(method).Invoke(null,null);
            var master=Instance("Managers.MasterDataManager"); Call(master,"Initialize");
            var save=T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
            save.GetType().GetField("HighestFloor").SetValue(save,30);
            save.GetType().GetField("CurrentFloor").SetValue(save,31);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save,true);
            save.GetType().GetField("TutorialStepId").SetValue(save,"Complete");
            // This test covers the guardian trial, birth and formation. Story
            // priority before that lesson is covered by StoryDialogueSceneFlowTests.
            var seenStories=(IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save);
            foreach(var storyId in new[]{"story_chapter_2_unlocked","story_chapter_3_unlocked",
                "story_chapter_4_unlocked","story_chapter_5_unlocked","story_chapter_6_unlocked","story_first_arc_complete"})
                seenStories.Add(storyId);
            seenStories.Add("story_guardian_trial_intro");
            var game=Instance("Managers.GameManager"); Call(game,"InitializeFromSave",save);
            var profile=P(game,"PlayerProfile");
            var party=(IList)P(profile,"PartyMonsterInstanceIds"); party.Clear();
            foreach(var id in new[]{"monster_cosmic_ore_fortress_golem","monster_sword_saint_alvarez","monster_abyss_dragon","monster_omega_leon","monster_abyss_grand_mage_seraphis"})
            {
                var monster=Call(profile,"AddOwnedMonster",id,60,20,false);
                party.Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            Call(Instance("Managers.SaveManager"),"SaveCurrentGame");
            SceneManager.LoadScene("HomeScene");
            yield return WaitForScene("HomeScene");
            Assert.That(GameObject.Find("GuardianSanctuaryModal"),Is.Null,"Unlocking guardians must leave the player on Home.");
            yield return AssertVisibleTapTarget(Button("GuardianSlot"));
            Button("GuardianSlot").onClick.Invoke(); yield return null;
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text,Does.Contain("試練へ"));
            Button("GuardianCard_seiryu").onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text,Does.Contain("試練へ"));
            Button("GuardianPrimaryAction").onClick.Invoke();
            yield return WaitForScene("BattleScene");
            var simulator=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(b=>b.GetType()==T("Battle.BattleSimulator"));
            Assert.That(P(simulator,"CurrentEnemyCountTarget"),Is.EqualTo(1));
            float deadline=Time.realtimeSinceStartup+25;
            Time.timeScale=6;
            while(GameObject.Find("BattleMinimalResultOverlay")==null && Time.realtimeSinceStartup<deadline) yield return null;
            Time.timeScale=1;
            Assert.That(GameObject.Find("BattleMinimalResultOverlay"),Is.Not.Null,"Trial must finish through the normal battle loop.");
            string captureDirectory = Environment.GetEnvironmentVariable("WITCHTOWER_GUARDIAN_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(captureDirectory))
            {
                Directory.CreateDirectory(captureDirectory);
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory, "trial-result.png"));
                for (int i = 0; i < 3; i++) yield return null;
            }
            Assert.That(((IList)P(profile,"GuardianCoreIds")).Cast<string>(),Is.EqualTo(new[]{"seiryu"}));
            Assert.That(P(profile,"HighestFloor"),Is.EqualTo(30));
            Assert.That(P(game,"CurrentFloor"),Is.EqualTo(31));
            Button("HomeButton").onClick.Invoke();
            yield return WaitForScene("HomeScene");
            Assert.That(GameObject.Find("GuardianSanctuaryModal"),Is.Not.Null);
            Assert.That(GameObject.Find("UnifiedHomeMenu").GetComponentInParent<Canvas>().name,Is.EqualTo("HomeCanvas"));
            yield return AssertVisibleTapTarget(Button("GuardianPrimaryAction"));
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text,Does.Contain("誕生"));
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text,Does.Contain("仲間に迎え"));
            Button("GuardianPrimaryAction").onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("GuardianBirthPresentation"),Is.Not.Null);
            Button("GuardianBirthSkip").onClick.Invoke(); yield return null;
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text,Does.Contain("編成"));
            Assert.That(GameObject.Find("SeparateSlotNote").GetComponent<Text>().text,Does.Contain("専用枠へ編成"));
            Button("GuardianPrimaryAction").onClick.Invoke(); yield return null;
            Assert.That(P(profile,"EquippedGuardianId"),Is.EqualTo("seiryu"));
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text,Does.Contain("編成中"));
            Assert.That(Button("GuardianPrimaryAction").transform.Find("TutorialTargetFrame").gameObject.activeSelf,Is.False);
            Assert.That(File.ReadAllText(Path.Combine(folder,"save.json")),Does.Contain("\"EquippedGuardianId\": \"seiryu\""));
            Button("CloseSanctuary").onClick.Invoke();
            T("UI.SceneTransitionGuard").GetMethod("LoadScene").Invoke(null,new object[]{"FormationScene"});
            yield return WaitForScene("FormationScene");
            Assert.That(Button("GuardianSlot").GetComponentInChildren<Text>().text,Does.Contain("青龍"));
            Button("GuardianSlot").onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("GuardianSanctuaryModal"),Is.Not.Null);
            Button("CloseSanctuary").onClick.Invoke();
            T("UI.SceneTransitionGuard").GetMethod("LoadScene").Invoke(null,new object[]{"HomeScene"});
            yield return WaitForScene("HomeScene");
            Assert.That(GameObject.Find("GuardianSanctuaryModal"),Is.Null,"Completed lesson must not reopen on every return.");
            yield return AssertVisibleTapTarget(Button("GuardianSlot"));
            Button("GuardianSlot").onClick.Invoke(); yield return null;
            yield return AssertVisibleTapTarget(Button("GuardianPrimaryAction"), false);
            Assert.That(Button("GuardianPrimaryAction").GetComponentInChildren<Text>().text,Does.Contain("編成中"));
            Button("CloseSanctuary").onClick.Invoke(); yield return null;
            yield return AssertVisibleTapTarget(Button("GuardianSlot"));
            TestContext.WriteLine("Isolated integration save: "+folder);
        }

        private static IEnumerator AssertVisibleTapTarget(Button button, bool interactable = true)
        {
            Assert.That(button.interactable, Is.EqualTo(interactable));
            bool sampled = false;
            GameObject hit = null;
            Canvas.WillRenderCanvases sample = () =>
            {
                if (sampled) return;
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                var pointer = new PointerEventData(EventSystem.current) { position = point };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                hit = hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                sampled = true;
            };
            Canvas.willRenderCanvases += sample;
            try
            {
                float deadline = Time.realtimeSinceStartup + 5;
                while (!sampled && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(sampled, Is.True);
            Assert.That(hit, Is.EqualTo(button.gameObject), "The sanctuary must remain above the opaque Home UI and receive a real tap.");
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(SceneManager.GetActiveScene().name!=name && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(name));
            for(int i=0;i<8;i++) yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            if(Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
