using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class FusionPresentationReceiptTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private Scene scene;
        private string directory;
        private object game, saves, master, profile;
        private object oldGame, oldSaves, oldMaster, oldDirectory, oldReceipt, oldReceiptDirectory;

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object instance, string method, params object[] args) => instance.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(instance, args);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
        private static object P(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private static void SetP(object instance, string name, object value) => instance.GetType().GetProperty(name).SetValue(instance, value);
        private static object F(object instance, string name) => instance.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(instance);
        private static void SetF(object instance, string name, object value) => instance.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(instance, value);
        private static IList List(object instance, string name) => (IList)P(instance, name);
        private static object Instance(string type) => T(type).GetProperty("Instance").GetValue(null);
        private static void SetInstance(string type, object instance) => T(type).GetProperty("Instance").SetValue(null, instance);

        [SetUp]
        public void SetUp()
        {
            oldGame = Instance("Managers.GameManager");
            oldSaves = Instance("Managers.SaveManager");
            oldMaster = Instance("Managers.MasterDataManager");
            oldDirectory = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden).GetValue(null);
            oldReceipt = T("Home.FusionPresentationReceipt").GetField("memory", StaticHidden).GetValue(null);
            oldReceiptDirectory = T("Home.FusionPresentationReceipt").GetField("memoryDirectory", StaticHidden).GetValue(null);
            ClearReceiptMemory();
            directory = Path.Combine(Path.GetTempPath(), "WitchTowerFusionReceipt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden).SetValue(null, directory);
            scene = EditorSceneManager.NewPreviewScene();
            var owner = new GameObject("FusionReceiptTestManagers");
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            saves = owner.AddComponent(T("Managers.SaveManager"));
            master = owner.AddComponent(T("Managers.MasterDataManager"));
            SetInstance("Managers.GameManager", game);
            SetInstance("Managers.SaveManager", saves);
            SetInstance("Managers.MasterDataManager", master);
            Call(master, "Initialize");
            Call(saves, "LoadOrCreate");
            object snapshot = JsonUtility.FromJson(JsonUtility.ToJson(P(saves, "CurrentSaveData")), T("Save.PlayerSaveData"));
            SetF(snapshot, "HasCompletedTutorial", true);
            SetF(snapshot, "TutorialStepId", "Complete");
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), snapshot);
            SetP(game, "PlayerProfile", profile);
            Call(game, "SetCurrentFloor", 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            SetInstance("Managers.GameManager", oldGame);
            SetInstance("Managers.SaveManager", oldSaves);
            SetInstance("Managers.MasterDataManager", oldMaster);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", StaticHidden).SetValue(null, oldDirectory);
            T("Home.FusionPresentationReceipt").GetField("memory", StaticHidden).SetValue(null, oldReceipt);
            T("Home.FusionPresentationReceipt").GetField("memoryDirectory", StaticHidden).SetValue(null, oldReceiptDirectory);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ReloadedReceiptShowsOnlyCommittedExistingChildAndNeverChangesInventory()
        {
            object child = Call(profile, "AddOwnedMonster", "monster_ore_giant_garm", 1, 0, false);
            Call(saves, "SaveCurrentGameWithReason", "test_fusion");
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            string savedBefore = File.ReadAllText(Path.Combine(directory, "save.json"));
            Store(child);
            Assert.That(File.Exists(Path.Combine(directory, "fusion-presentation.json")), Is.True);
            ClearReceiptMemory();
            object[] read = { profile, null };
            Assert.That(S("Home.FusionPresentationReceipt", "TryRead", read), Is.True);
            Assert.That(F(read[1], "CreatedInstanceId"), Is.EqualTo(F(child, "InstanceId")));
            Assert.That(F(read[1], "CompletedTutorial"), Is.True);
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before));
            Assert.That(File.ReadAllText(Path.Combine(directory, "save.json")), Is.EqualTo(savedBefore));

            // Reopening a page after the child has since been consumed cannot
            // resurrect that unit through the display-only recovery record.
            List(profile, "OwnedMonsters").Remove(child);
            ClearReceiptMemory();
            read = new object[] { profile, null };
            Assert.That(S("Home.FusionPresentationReceipt", "TryRead", read), Is.False);
            Assert.That(List(profile, "OwnedMonsters").Cast<object>().Any(value => F(value, "InstanceId").Equals(F(child, "InstanceId"))), Is.False);
            Assert.That(File.Exists(Path.Combine(directory, "fusion-presentation.json")), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UncommittedOrDifferentPlayerReceiptCannotBeReplayed(bool differentPlayer)
        {
            object child = Call(profile, "AddOwnedMonster", "monster_ore_giant_garm", 1, 0, false);
            if (differentPlayer) Call(saves, "SaveCurrentGameWithReason", "test_fusion");
            Store(child);
            if (differentPlayer) SetP(profile, "PlayerId", Guid.NewGuid().ToString("N"));
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            ClearReceiptMemory();
            object[] read = { profile, null };
            Assert.That(S("Home.FusionPresentationReceipt", "TryRead", read), Is.False);
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before));
        }

        [Test]
        public void AcknowledgementIsIdempotentAndDoesNotAlterTheSavedResult()
        {
            object child = Call(profile, "AddOwnedMonster", "monster_ore_giant_garm", 1, 0, false);
            Call(saves, "SaveCurrentGameWithReason", "test_fusion");
            Store(child);
            string save = File.ReadAllText(Path.Combine(directory, "save.json"));
            S("Home.FusionPresentationReceipt", "Clear", F(child, "InstanceId"));
            S("Home.FusionPresentationReceipt", "Clear", F(child, "InstanceId"));
            Assert.That(File.Exists(Path.Combine(directory, "fusion-presentation.json")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(directory, "save.json")), Is.EqualTo(save));
            Assert.That(List(profile, "OwnedMonsters").Contains(child), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedSaveRestoresParentsEquipmentPartyDexAndTutorialFlag(bool saveManagerMissing)
        {
            object parentA = Call(profile, "AddOwnedMonster", "monster_rock_golem", 20, 0, false);
            object parentB = Call(profile, "AddOwnedMonster", "monster_rock_golem", 20, 0, false);
            object equipment = Activator.CreateInstance(T("Save.OwnedEquipmentData"));
            SetF(equipment, "InstanceId", "fusion_test_equipment");
            SetF(equipment, "EquipmentId", "weapon_bronze_sword");
            SetF(equipment, "EquippedMonsterInstanceId", F(parentA, "InstanceId"));
            SetF(equipment, "IsEquipped", true);
            List(profile, "OwnedEquipments").Add(equipment);
            SetF(parentA, "EquippedWeaponInstanceId", F(equipment, "InstanceId"));
            Call(profile, "SetPartyMonsterIds", (object)new[] { (string)F(parentA, "InstanceId"), (string)F(parentB, "InstanceId") });
            Call(saves, "SaveCurrentGameWithReason", "before_fusion");
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            string diskBefore = File.ReadAllText(Path.Combine(directory, "save.json"));
            var panelObject = new GameObject("FusionRollbackPanel", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(panelObject, scene);
            panelObject.SetActive(false);
            Component panel = panelObject.AddComponent(T("Home.MonsterFusionPanelController"));
            Call(panel, "Build");
            SetF(panel, "parentAInstanceId", F(parentA, "InstanceId"));
            SetF(panel, "parentBInstanceId", F(parentB, "InstanceId"));
            SetF(panel, "fusionTutorialPractice", true);
            if (saveManagerMissing) SetInstance("Managers.SaveManager", null);
            else SetP(saves, "RecoveryRequired", true);

            Call(panel, "FuseSelectedParents");
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before),
                "A failed commit must restore every inventory/party/lesson mutation, not just re-add parents.");
            Assert.That(File.ReadAllText(Path.Combine(directory, "save.json")), Is.EqualTo(diskBefore));
            Assert.That(F(panel, "fusionInProgress"), Is.False);
            Assert.That(F(panel, "fusionCinematic"), Is.Null);
            Assert.That(((Text)F(panel, "statusLabel")).text, Does.Contain("保存できません"));
            Assert.That(File.Exists(Path.Combine(directory, "fusion-presentation.json")), Is.False);
        }

        private void Store(object child)
        {
            object parent = Call(master, "GetMonsterData", "monster_rock_golem");
            S("Home.FusionPresentationReceipt", "Store", profile, child, parent, parent, true);
        }

        private static void ClearReceiptMemory()
        {
            T("Home.FusionPresentationReceipt").GetField("memory", StaticHidden).SetValue(null, null);
            T("Home.FusionPresentationReceipt").GetField("memoryDirectory", StaticHidden).SetValue(null, null);
        }
    }
}
