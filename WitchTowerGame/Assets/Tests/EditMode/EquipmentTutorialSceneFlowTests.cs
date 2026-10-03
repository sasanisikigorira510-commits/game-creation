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
    public sealed class EquipmentTutorialSceneFlowTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
        private static object Field(object obj, string name) => obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(obj);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);

        [UnityTest]
        public IEnumerator SavedAutoEquipReplacementCanEnhanceAndReturnToNextHomeLesson()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string folder = Path.Combine(Path.GetTempPath(), "WitchTowerEquipmentRecovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, folder);
            foreach (string method in new[] { "EnsureGameManager", "EnsureSaveManager", "EnsureMasterDataManager" })
                T("Core.ManagerFactory").GetMethod(method).Invoke(null, null);
            Call(Instance("Managers.MasterDataManager"), "Initialize");
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("TutorialStepId").SetValue(save, "T07B");
            save.GetType().GetField("InitialTutorialSummonCount").SetValue(save, 3);
            Call(Instance("Managers.GameManager"), "InitializeFromSave", save);
            object profile = T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            object mage = Call(profile, "AddOwnedMonster", "monster_apprentice_mage", 1, 0, false);
            T("Data.StoryTutorialService").GetMethod("EnsureEquipmentTutorialGift").Invoke(null, new[] { profile });
            object drop = Call(profile, "AddOwnedEquipmentWithInstancePrefix", "equip_apprentice_charm",
                Enum.Parse(T("MasterData.EquipmentRarity"), "Uncommon"), "battle_drop_");
            Call(profile, "EquipEquipmentToMonster", Field(mage, "InstanceId"), Field(drop, "InstanceId"));
            foreach (string hint in new[] { "tutorial_equipment", "tutorial_equipment_auto_equip" })
                T("Data.StoryTutorialService").GetMethod("MarkHintSeen").Invoke(null, new[] { profile, hint });
            Call(Instance("Managers.SaveManager"), "SaveCurrentGame");

            // Load the on-disk save through the same model initialization path.
            Call(Instance("Managers.SaveManager"), "LoadOrCreate");
            Call(Instance("Managers.GameManager"), "InitializeFromSave",
                T("Managers.SaveManager").GetProperty("CurrentSaveData").GetValue(Instance("Managers.SaveManager")));
            SceneManager.LoadScene("EquipmentScene");
            yield return WaitForScene("EquipmentScene");
            var controller = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(c => c.GetType() == T("Core.TitleSceneController"));
            Assert.That(((Text)Field(controller, "equipmentTutorialGuideTitleText")).text, Is.EqualTo("ルシェの強化レッスン"));
            var list = (RectTransform)Field(controller, "equipmentInventoryContentRect");
            yield return Click(list.GetChild(0).GetComponent<Button>());
            yield return null;
            yield return Click((Button)Field(controller, "equipmentDetailEnhanceButton"));
            yield return null;
            var relicList = (RectTransform)Field(controller, "equipmentEnhanceOverlayListRect");
            yield return Click(relicList.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>().text == "使用"));
            yield return null;
            Assert.That(((Text)Field(controller, "equipmentEnhanceTutorialGuideTitleText")).text, Is.EqualTo("強化成功です"));
            yield return Click((Button)Field(controller, "equipmentEnhanceCloseButton"));
            yield return null;
            yield return Click((Button)Field(controller, "equipmentHomeReturnButton"));
            yield return WaitForScene("HomeScene");
            profile = T("Managers.GameManager").GetProperty("PlayerProfile").GetValue(Instance("Managers.GameManager"));
            Assert.That(profile.GetType().GetProperty("TutorialStepId").GetValue(profile), Is.EqualTo("T07C_SHOP"));
            Assert.That(((IList)profile.GetType().GetProperty("OwnedEquipments").GetValue(profile)).Count, Is.EqualTo(2));
            Assert.That(File.ReadAllText(Path.Combine(folder, "save.json")), Does.Contain("T07C_SHOP"));
            Debug.Log("[EquipmentTutorialRecovery] Saved replacement -> visible highlight -> enhance -> relic -> close -> home/shop: passed with raycast-checked buttons. Isolated save: " + folder);
        }

        private static IEnumerator Click(Button button)
        {
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
                float until = Time.realtimeSinceStartup + 3f;
                while (!sampled && Time.realtimeSinceStartup < until) yield return null;
            }
            finally { Canvas.willRenderCanvases -= sample; }
            Assert.That(sampled, Is.True, "Need a real game-view render to check the touch target.");
            Assert.That(handler, Is.EqualTo(button.gameObject), button.name + " must not be covered by the tutorial guide or another modal.");
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
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
