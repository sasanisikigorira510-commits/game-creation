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
    public sealed class UsabilitySceneFlowTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
        private static object Field(object obj, string name) => obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(obj);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);

        [UnityTest]
        public IEnumerator FusionLessonHighlightsActionThenGuidesBackHome()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder=Path.Combine(Path.GetTempPath(),"WitchTowerFusionFlow-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,folder);
            foreach(string method in new[]{"EnsureGameManager","EnsureSaveManager","EnsureMasterDataManager"}) T("Core.ManagerFactory").GetMethod(method).Invoke(null,null);
            Call(Instance("Managers.MasterDataManager"),"Initialize");
            var save=T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
            save.GetType().GetField("TutorialStepId").SetValue(save,"Complete");
            save.GetType().GetField("HasCompletedTutorial").SetValue(save,true);
            save.GetType().GetField("HighestFloor").SetValue(save,20);
            Call(Instance("Managers.GameManager"),"InitializeFromSave",save);
            SceneManager.LoadScene("FusionScene"); yield return WaitForScene("FusionScene");
            var controller=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c=>c.GetType()==T("Home.MonsterFusionPanelController"));
            for(int page=0;page<3;page++)
            {
                yield return Click(((GameObject)Field(controller,"fusionTutorialGuideRoot")).GetComponentsInChildren<Button>().Single());
                yield return null;
            }
            var fuse=(Button)Field(controller,"fuseButton");
            Assert.That(fuse.transform.Find("TutorialTargetFrame").gameObject.activeSelf,Is.True);
            yield return Click(fuse);
            float until=Time.realtimeSinceStartup+12;
            while(Field(controller,"fusionCompletionGuide")==null && Time.realtimeSinceStartup<until) yield return null;
            var guide=(GameObject)Field(controller,"fusionCompletionGuide");
            Assert.That(guide,Is.Not.Null);
            Assert.That(((Button)Field(controller,"resultStageNextButton")).gameObject.activeSelf,Is.False);
            Assert.That(guide.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("ルシェ")),Is.True);
            yield return Click(guide.GetComponentInChildren<Button>());
            yield return WaitForScene("HomeScene");
            var profile=T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            var monsters=((IEnumerable)profile.GetType().GetProperty("OwnedMonsters").GetValue(profile)).Cast<object>().ToArray();
            Assert.That(monsters.Count(m=>(string)Field(m,"MonsterId")=="monster_ore_giant_garm"),Is.EqualTo(1));
            Assert.That(monsters.Any(m=>(string)Field(m,"MonsterId")=="monster_rock_golem"),Is.False);
            Assert.That(GameObject.Find("SkillTreeButton"),Is.Null);
            var home=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c=>c.GetType()==T("Home.HomeSceneController"));
            var focus=(GameObject)Field(home,"homeTutorialFocusRoot");
            Assert.That(focus == null || !focus.activeSelf,Is.True,"Chapter dialogue cannot point at Battle while asking to advance its text.");
            Debug.Log("Fusion lesson: page actions, highlighted fuse, reveal, Luche guide, home passed with rendered raycasts.");
        }

        [UnityTest]
        public IEnumerator EquipmentNavigationAcceptsCenterAndEdgeTaps()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder=Path.Combine(Path.GetTempPath(),"WitchTowerEquipmentTargets-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,folder);
            foreach(string method in new[]{"EnsureGameManager","EnsureSaveManager","EnsureMasterDataManager"}) T("Core.ManagerFactory").GetMethod(method).Invoke(null,null);
            Call(Instance("Managers.MasterDataManager"),"Initialize");
            var save=T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
            save.GetType().GetField("TutorialStepId").SetValue(save,"Complete");
            save.GetType().GetField("HasCompletedTutorial").SetValue(save,true);
            Call(Instance("Managers.GameManager"),"InitializeFromSave",save);
            var profile=T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            Call(profile,"AddOwnedMonster","monster_rock_golem",1,0,false);
            Call(profile,"AddOwnedMonster","monster_apprentice_mage",1,0,false);
            T("Data.StoryTutorialService").GetMethod("EnsureEquipmentTutorialGift").Invoke(null,new[]{profile});
            foreach(var hint in new[]{"tutorial_equipment","tutorial_equipment_auto_equip"}) T("Data.StoryTutorialService").GetMethod("MarkHintSeen").Invoke(null,new[]{profile,hint});
            SceneManager.LoadScene("EquipmentScene"); yield return WaitForScene("EquipmentScene");
            var controller=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c=>c.GetType()==T("Core.TitleSceneController"));
            var root=(GameObject)Field(controller,"equipmentSceneRoot");
            var summary=root.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="EquippedSummaryPanel");
            foreach(var name in new[]{"←Button","→Button","自動装備Button","UnequipAllButton","選ぶButton"})
            {
                var button=summary.GetComponentsInChildren<Button>().Single(b=>b.name==name);
                foreach(var point in new[]{new Vector2(.5f,.5f),new Vector2(.12f,.5f),new Vector2(.88f,.5f),new Vector2(.5f,.12f),new Vector2(.5f,.88f)})
                    yield return CheckTarget(button,point,false);
                yield return Click(button);
            }
        }

        [UnityTest]
        public IEnumerator LargeFormationButtonWorksAcrossItsAreaDuringTutorial()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder = Path.Combine(Path.GetTempPath(), "WitchTowerFormationTargets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, folder);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("TutorialStepId").SetValue(save, "T04");
            save.GetType().GetField("InitialTutorialSummonCount").SetValue(save, 3);
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            var profile = T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            foreach (string id in new[] { "monster_rock_golem", "monster_apprentice_mage", "monster_dragon_whelp" })
                Call(profile, "AddOwnedMonster", id, 1, 0, false);
            SceneManager.LoadScene("FormationScene"); yield return WaitForScene("FormationScene");
            var c = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(v => v.GetType() == T("Formation.FormationSceneController"));
            for (int slot = 0; slot < 3; slot++)
            {
                var views = (IList)Field(c, "slotViews");
                yield return Click((Button)Field(views[slot], "Button"));
                var roster = (IList)Field(c, "roster");
                string id = (string)Field(roster[slot], "InstanceId");
                var content = (Transform)Field(c, "rosterContent");
                yield return Click(content.Find("Card_" + id).GetComponent<Button>());
                var button = content.Find("Card_" + id).Find("Body/SelectionButton").GetComponent<Button>();
                var guide = (GameObject)Field(c, "formationTutorialGuidePanel");
                var relativeTo = guide.transform.parent;
                Bounds buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(relativeTo, button.transform);
                Bounds guideBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(relativeTo, guide.transform);
                Assert.That(buttonBounds.min.y - guideBounds.max.y, Is.GreaterThan(12f), "The guide must not hide the larger confirmation button.");
                foreach (var point in new[] { new Vector2(.5f,.5f), new Vector2(.12f,.5f), new Vector2(.88f,.5f), new Vector2(.5f,.12f), new Vector2(.5f,.88f) })
                    yield return CheckTarget(button, point, false);
                yield return Click(button);
                Assert.That(((IList)Field(c, "selectedMonsters")).Cast<object>().Count(m => m != null), Is.EqualTo(slot + 1));
            }
            Debug.Log("Large formation button: destination -> monster -> confirmation for all three slots passed with center/edge raycasts.");
        }

        private static IEnumerator Click(Button button) { yield return CheckTarget(button,new Vector2(.5f,.5f),true); }
        private static IEnumerator CheckTarget(Button button,Vector2 point,bool click)
        {
            // Slot/card selection rebuilds the roster. Give the new Graphics
            // their first completed layout/render before sampling the next tap.
            yield return null; yield return null;
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            GameObject handler = null;
            PointerEventData pointer = null;
            bool sampled = false;
            // Screen in an EditMode coroutine is the editor window size, not
            // the game's fixed 1080x1920 render. Sample raycasts in the latter
            // context, then dispatch input outside the render/rebuild callback.
            Canvas.WillRenderCanvases sample = () =>
            {
                if (sampled || Screen.width != 1080 || Screen.height != 1920) return;
                var rect = (RectTransform)button.transform;
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.min + Vector2.Scale(rect.rect.size,point))),
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
                float until = Time.realtimeSinceStartup + 3f;
                while (!sampled && Time.realtimeSinceStartup < until) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(sampled, Is.True, "Need a real game-view render to check the touch target.");
            if (handler != button.gameObject)
            {
                Debug.Log($"Touch diagnostic: {button.name}, screen point {pointer.position}, rect {((RectTransform)button.transform).rect}, canvas scale {button.GetComponentInParent<Canvas>().scaleFactor}");
                string captureDir = Environment.GetEnvironmentVariable("WITCHTOWER_SEPTEMBER_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(captureDir))
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(captureDir, "formation-touch-diagnostic.png"));
                    yield return null; yield return null;
                }
            }
            Assert.That(handler, Is.EqualTo(button.gameObject), button.name + " must not be covered by the tutorial guide or another modal.");
            if (click) ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator WaitForScene(string name)
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < until) yield return null;
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
