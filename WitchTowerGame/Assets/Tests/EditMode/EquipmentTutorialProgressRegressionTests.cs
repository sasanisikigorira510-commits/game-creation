using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class EquipmentTutorialProgressRegressionTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private SceneSetup[] scenes;
        private object oldGame, oldMaster, oldSaveManager;
        private Component game, controller;
        private object profile, gift, drop;
        private string monsterId;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType()
            .GetMethod(method, BindingFlags.Public | Hidden).Invoke(obj, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Public | Hidden).GetValue(obj);
        private static object Tutorial(string name, params object[] args) => T("Data.StoryTutorialService").GetMethod(name).Invoke(null, args);
        private static string Id(object obj) => (string)Field(obj, "InstanceId");
        private string Target() => (string)Tutorial("GetNextEvent", profile, "EquipmentScene").GetType().GetProperty("TargetKey")
            .GetValue(Tutorial("GetNextEvent", profile, "EquipmentScene"));
        private void Mark(string id) => Tutorial("MarkHintSeen", profile, id);

        [SetUp]
        public void Setup()
        {
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSaveManager = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("EquipmentProgressFixture");
            owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            Component master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            var saveType = T("Save.PlayerSaveData");
            object save = saveType.GetMethod("CreateDefault").Invoke(null, null);
            saveType.GetField("TutorialStepId").SetValue(save, "T07B");
            saveType.GetField("InitialTutorialSummonCount").SetValue(save, 3);
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            T("Managers.GameManager").GetProperty("PlayerProfile").SetValue(game, profile);
            monsterId = Id(Call(profile, "AddOwnedMonster", "monster_apprentice_mage", 1, 0, false));
            // The video has a better battle drop with the same display name
            // as the tutorial's common charm. Keep both real item instances.
            drop = Call(profile, "AddOwnedEquipmentWithInstancePrefix", "equip_apprentice_charm",
                Enum.Parse(T("MasterData.EquipmentRarity"), "Uncommon"), "battle_drop_");
            drop.GetType().GetField("RolledWisdom").SetValue(drop, 9);
            Tutorial("EnsureEquipmentTutorialGift", profile);
            gift = Tutorial("FindEquipmentTutorialGift", profile);
            var canvas = new GameObject("EquipmentProgressCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 2341);
            controller = owner.AddComponent(T("Core.TitleSceneController"));
            Call(controller, "EnsureEquipmentScene");
            ((GameObject)Field(controller, "equipmentSceneRoot")).SetActive(true);
            Call(controller, "RefreshEquipmentScene");
        }

        [TearDown]
        public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSaveManager);
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }

        [Test]
        public void AutoEquipReplacingTutorialGiftDoesNotUndoManualLesson()
        {
            Assert.That(Target(), Is.EqualTo("equipment.first_item"));
            Call(controller, "ShowEquipmentDetailSheet", Id(gift));
            ((Button)Field(controller, "equipmentDetailEquipButton")).onClick.Invoke();
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
            ((Button)Field(controller, "equipmentAutoEquipButton")).onClick.Invoke();
            Assert.That(Field(drop, "EquippedMonsterInstanceId"), Is.EqualTo(monsterId), "Auto-equip should retain the better item.");
            Assert.That(Field(gift, "EquippedMonsterInstanceId"), Is.Null.Or.Empty);
            Assert.That(Target(), Is.EqualTo("equipment.enhance_button"), "Changing gear must never send a completed lesson backwards.");
            CompleteEnhancementThroughButtons();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ExistingSaveWithDropEquippedResumesWithoutReplacingOrDeletingItems(bool manualSeen, bool autoSeen)
        {
            Call(profile, "EquipEquipmentToMonster", monsterId, Id(drop));
            if (manualSeen) Mark("tutorial_equipment");
            if (autoSeen) Mark("tutorial_equipment_auto_equip");
            ReloadInMemory();
            Assert.That(Target(), Is.EqualTo(autoSeen ? "equipment.enhance_button" : "equipment.auto_equip"));
            Assert.That(Field(drop, "EquippedMonsterInstanceId"), Is.EqualTo(monsterId));
            Assert.That(((IList)profile.GetType().GetProperty("OwnedEquipments").GetValue(profile)).Count, Is.EqualTo(2));
            Assert.That(Field(drop, "QualityRank"), Is.EqualTo(2));
            string capture = Environment.GetEnvironmentVariable("WITCHTOWER_EQUIPMENT_RECOVERY_CAPTURE");
            if (manualSeen && autoSeen && !string.IsNullOrEmpty(capture))
            {
                Canvas.ForceUpdateCanvases();
                var canvas = controller.GetType().GetField("equipmentSceneRoot", Hidden).GetValue(controller) as GameObject;
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { (RectTransform)canvas.GetComponentInParent<Canvas>().transform, 1179, 2556, capture });
            }
            if (!autoSeen)
            {
                Assert.That(((Button)Field(controller, "equipmentAutoEquipButton")).interactable, Is.True);
                ((Button)Field(controller, "equipmentAutoEquipButton")).onClick.Invoke();
            }
            Assert.That(Target(), Is.EqualTo("equipment.enhance_button"));
            CompleteEnhancementThroughButtons();
        }

        [Test]
        public void ManuallyEquippingTheSameNamedDropAlsoCompletesTheLesson()
        {
            Call(controller, "ShowEquipmentDetailSheet", Id(drop));
            var equip = (Button)Field(controller, "equipmentDetailEquipButton");
            Assert.That(equip.interactable, Is.True);
            equip.onClick.Invoke();
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
            Call(controller, "UnequipEquipmentInstance", Id(drop));
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"), "The learned action remains learned after unequipping.");
            ReloadInMemory();
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
        }

        [Test]
        public void AutoEquipLessonStillHasAPracticeItemIfAllGearWasDiscarded()
        {
            Mark("tutorial_equipment");
            ((IList)profile.GetType().GetProperty("OwnedEquipments").GetValue(profile)).Clear();
            Call(controller, "RefreshEquipmentScene");
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
            var button = (Button)Field(controller, "equipmentAutoEquipButton");
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
            Assert.That(Target(), Is.EqualTo("equipment.enhance_button"));
        }

        private void ReloadInMemory()
        {
            object save = Call(profile, "ToSaveData", 1);
            object reloaded = JsonUtility.FromJson(JsonUtility.ToJson(save), T("Save.PlayerSaveData"));
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { reloaded });
            T("Managers.GameManager").GetProperty("PlayerProfile").SetValue(game, profile);
            drop = Call(profile, "GetOwnedEquipmentByInstanceId", Id(drop));
            gift = Call(profile, "GetOwnedEquipmentByInstanceId", Id(gift));
            Call(controller, "CloseEquipmentDetailSheet");
            Call(controller, "RefreshEquipmentScene");
        }

        private void CompleteEnhancementThroughButtons()
        {
            var list = (RectTransform)Field(controller, "equipmentInventoryContentRect");
            Assert.That(list.GetChild(0).Find("EquipmentTutorialFocusCardTop"), Is.Not.Null);
            list.GetChild(0).GetComponent<Button>().onClick.Invoke();
            var enhance = (Button)Field(controller, "equipmentDetailEnhanceButton");
            Assert.That(enhance.interactable, Is.True);
            enhance.onClick.Invoke();
            var relicList = (RectTransform)Field(controller, "equipmentEnhanceOverlayListRect");
            Button use = relicList.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>().text == "使用");
            Assert.That(use.interactable, Is.True);
            use.onClick.Invoke();
            Assert.That(Field(drop, "UpgradeLevel"), Is.EqualTo(1));
            ((Button)Field(controller, "equipmentEnhanceCloseButton")).onClick.Invoke();
            Assert.That(Target(), Is.EqualTo("equipment.return_home"));
            ReloadInMemory();
            Assert.That(Target(), Is.EqualTo("equipment.return_home"), "Returning or restarting must not restart the equipment lesson.");
        }
    }
}
