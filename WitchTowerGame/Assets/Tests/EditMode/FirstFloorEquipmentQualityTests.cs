using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class FirstFloorEquipmentQualityTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private UnityEngine.Random.State previousRandomState;
        private object oldGame, oldMaster, oldSave, oldAudio, oldSaveOverride;
        private Component game, saveManager;
        private object profile;
        private GameObject owner;
        private string temporaryDirectory;

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object target, string method, params object[] arguments) => target.GetType()
            .GetMethod(method, BindingFlags.Public | Hidden).Invoke(target, arguments);
        private static object Story(string method, params object[] arguments) => T("Data.StoryTutorialService")
            .GetMethod(method).Invoke(null, arguments);
        private static object Field(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Public | Hidden).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static IList Equipments(object target) => (IList)target.GetType().GetProperty("OwnedEquipments").GetValue(target);
        private static IList Drops(object target, int floor = 1, bool full = true) =>
            (IList)Story("GrantFirstFloorEquipmentQualityDrops", target, floor, full);
        private string Target() => (string)Story("GetNextEvent", profile, "EquipmentScene")
            .GetType().GetProperty("TargetKey").GetValue(Story("GetNextEvent", profile, "EquipmentScene"));

        [SetUp]
        public void Setup()
        {
            previousRandomState = UnityEngine.Random.state;
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            oldAudio = T("Managers.AudioManager").GetProperty("Instance").GetValue(null);
            oldSaveOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerFirstFloorQuality-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, temporaryDirectory);
            owner = new GameObject("FirstFloorQualityManagers");
            owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            var master = owner.AddComponent(T("Managers.MasterDataManager"));
            saveManager = owner.AddComponent(T("Managers.SaveManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, saveManager);
            T("Managers.AudioManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            save.GetType().GetField("TutorialStepId").SetValue(save, "T06");
            save.GetType().GetField("InitialTutorialSummonCount").SetValue(save, 3);
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            Set(game, "PlayerProfile", profile);
            Call(game, "SetCurrentFloor", 1);
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (temporaryDirectory != null && Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
                T("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
                T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
                T("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSave);
                T("Managers.AudioManager").GetProperty("Instance").SetValue(null, oldAudio);
                T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, oldSaveOverride);
            }
        }

        [Test]
        public void FirstFullClearGrantsSameNamedDistinctProperlyRolledCommonAndUncommon()
        {
            IList drops = Drops(profile);
            Assert.That(drops.Count, Is.EqualTo(2));
            Assert.That(drops.Cast<object>().Select(item => Field(item, "EquipmentId")), Is.All.EqualTo("equip_apprentice_charm"));
            Assert.That(drops.Cast<object>().Select(item => (int)Field(item, "QualityRank")), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(drops.Cast<object>().Select(item => Field(item, "InstanceId")).Distinct().Count(), Is.EqualTo(2));
            foreach (object item in drops)
            {
                Assert.That(Field(item, "HasRolledStats"), Is.True);
                Assert.That((int)Field(item, "RolledWisdom"), Is.GreaterThan(0));
                Assert.That((int)Field(item, "MaxEnhanceAttempts"), Is.GreaterThan(0));
                Assert.That(Field(item, "RemainingEnhanceAttempts"), Is.EqualTo(Field(item, "MaxEnhanceAttempts")));
                Assert.That(Field(item, "UpgradeLevel"), Is.EqualTo(0));
                Assert.That(Field(item, "EquippedMonsterInstanceId"), Is.Null.Or.Empty);
            }
            object common = drops.Cast<object>().Single(item => (int)Field(item, "QualityRank") == 1);
            Assert.That(Story("FindEquipmentTutorialGift", profile), Is.SameAs(common));
            Assert.That(Story("HasEquipmentQualityTutorialPair", profile), Is.True);
            Assert.That(Drops(profile).Count, Is.Zero, "Repeated reward preparation before floor recording must not duplicate the pair.");
            Assert.That(Story("EnsureEquipmentTutorialGift", profile), Is.False, "Opening equipment must reuse the battle reward, not add a third charm.");
            Assert.That(Equipments(profile).Count, Is.EqualTo(2));
        }

        [Test]
        public void FullStorageAndAutoSellDoNotRemoveGuaranteedLessonDrops()
        {
            object unrelated = Call(profile, "AddOwnedEquipmentWithInstancePrefix", "equip_apprentice_charm",
                Enum.Parse(T("MasterData.EquipmentRarity"), "Rare"), "unrelated_");
            Set(profile, "EquipmentStorageLimit", 1);
            Set(profile, "HasAutoSellEquipmentUpgrade", true);
            Set(profile, "IsAutoSellEquipmentUpgradeEnabled", true);
            Set(profile, "AutoSellEquipmentQualityThreshold", 5);
            int gold = (int)profile.GetType().GetProperty("Gold").GetValue(profile);
            Assert.That(Drops(profile).Count, Is.EqualTo(2));
            Assert.That(Equipments(profile).Count, Is.EqualTo(3));
            Assert.That(profile.GetType().GetProperty("EquipmentStorageLimit").GetValue(profile), Is.EqualTo(3));
            Assert.That(profile.GetType().GetProperty("Gold").GetValue(profile), Is.EqualTo(gold));
            Assert.That(Equipments(profile).Contains(unrelated), Is.True);
            Assert.That(Field(unrelated, "QualityRank"), Is.EqualTo(3));
        }

        [TestCase(1, false, 0, false, "T06")]
        [TestCase(2, true, 0, false, "T06")]
        [TestCase(1, true, 1, false, "T06")]
        [TestCase(1, true, 10, false, "T07B")]
        [TestCase(1, true, 0, true, "Complete")]
        [TestCase(1, true, 0, false, "T00")]
        public void PartialClearReplaysAndOldSavesNeverReceiveNewPair(int floor, bool full, int highest, bool completed, string step)
        {
            Set(profile, "HighestFloor", highest);
            Set(profile, "HasCompletedTutorial", completed);
            Set(profile, "TutorialStepId", step);
            Assert.That(Drops(profile, floor, full).Count, Is.Zero);
            Assert.That(Equipments(profile).Count, Is.Zero);
            Assert.That(Story("HasSeenHint", profile, "tutorial_equipment_quality_pair_received"), Is.False);
        }

        [Test]
        public void BattleDropPathIncludesPairInResultsAndPersistsClearAndMarkerAsOneSave()
        {
            var battle = owner.AddComponent(T("Battle.BattleSceneController"));
            battle.GetType().GetField("currentFloor", Hidden).SetValue(battle, 1);
            // Own the entire result UI tree. Never let the controller locate
            // or change an existing scene's BattleMinimalCanvas during a test.
            var canvas = new GameObject("FirstFloorQualityCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(owner.transform, false);
            battle.GetType().GetField("minimalCanvasRoot", Hidden).SetValue(battle, canvas);
            Call(battle, "ApplyEquipmentDrop", profile, 1, 1);
            Assert.That(Story("HasEquipmentQualityTutorialPair", profile), Is.True);
            Assert.That((string)Field(battle, "lastEquipmentDropSummary"), Does.Contain("コモン").And.Contain("アンコモン"));
            Assert.That(((IList)Field(battle, "lastRewardVisuals")).Count, Is.GreaterThanOrEqualTo(2));
            Call(game, "RecordFloorClear", 1);
            T("Home.MissionService").GetMethod("RecordBattleWin").Invoke(null, new[] { profile });
            Story("AdvanceTutorial", profile, "T06");
            Call(saveManager, "SaveAfterDungeonStageClear", 1);
            string path = Path.Combine(temporaryDirectory, "save.json");
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Does.Contain("tutorial_equipment_quality_pair_received").And.Contain("battle_clear"));
            Call(saveManager, "LoadOrCreate");
            object reloaded = saveManager.GetType().GetProperty("CurrentSaveData").GetValue(saveManager);
            Assert.That(reloaded.GetType().GetField("HighestFloor").GetValue(reloaded), Is.EqualTo(1));
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { reloaded });
            Assert.That(Story("HasEquipmentQualityTutorialPair", profile), Is.True);
            string[] ids = Equipments(profile).Cast<object>().Select(item => (string)Field(item, "InstanceId")).ToArray();
            Assert.That(Drops(profile).Count, Is.Zero);
            Assert.That(Equipments(profile).Cast<object>().Select(item => (string)Field(item, "InstanceId")), Is.EquivalentTo(ids));
            // Even a stale highest-floor cursor cannot recreate sold reward items
            // while the durable receipt marker is retained.
            Set(profile, "HighestFloor", 0);
            Equipments(profile).Clear();
            Assert.That(Drops(profile).Count, Is.Zero);
        }

        [Test]
        public void NewPairStartsQualityLessonThenManualAutoEquipAndEnhancement()
        {
            Drops(profile);
            Set(profile, "HighestFloor", 1);
            Set(profile, "TutorialStepId", "T07A");
            object opening = Story("GetNextEvent", profile, "HomeScene");
            Assert.That((string)opening.GetType().GetProperty("Body").GetValue(opening), Does.Contain("コモン").And.Contain("アンコモン"));
            Set(profile, "TutorialStepId", "T07B");
            Assert.That(Target(), Is.EqualTo("equipment.quality_label"));
            Story("MarkHintSeen", profile, "tutorial_equipment_quality");
            Assert.That(Target(), Is.EqualTo("equipment.first_item"));
            Story("MarkHintSeen", profile, "tutorial_equipment");
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
            Story("MarkHintSeen", profile, "tutorial_equipment_auto_equip");
            Assert.That(Target(), Is.EqualTo("equipment.enhance_button"));
        }

        [Test]
        public void PendingQualityLessonAndItsAcknowledgementSurviveRestart()
        {
            Drops(profile);
            Set(profile, "HighestFloor", 1);
            Set(profile, "TutorialStepId", "T07B");
            SaveAndReloadProfile();
            Assert.That(Target(), Is.EqualTo("equipment.quality_label"));
            Story("MarkHintSeen", profile, "tutorial_equipment_quality");
            Story("MarkStorySeen", profile, "story_first_equipment_quality");
            SaveAndReloadProfile();
            Assert.That(Target(), Is.EqualTo("equipment.first_item"));
            Assert.That(Drops(profile).Count, Is.Zero);
            Assert.That(Equipments(profile).Count, Is.EqualTo(2));
        }

        private void SaveAndReloadProfile()
        {
            Set(game, "PlayerProfile", profile);
            Call(saveManager, "SaveCurrentGame");
            Call(saveManager, "LoadOrCreate");
            object reloaded = saveManager.GetType().GetProperty("CurrentSaveData").GetValue(saveManager);
            Assert.That(reloaded, Is.Not.Null);
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { reloaded });
            Set(game, "PlayerProfile", profile);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingOrMissingComparisonItemsKeepResumableManualLesson(bool hadPair)
        {
            if (hadPair)
            {
                Drops(profile);
                Equipments(profile).RemoveAt(1);
            }
            Set(profile, "HighestFloor", 1);
            Set(profile, "TutorialStepId", "T07B");
            Story("EnsureEquipmentTutorialGift", profile);
            Assert.That(Target(), Is.EqualTo("equipment.first_item"));
            Story("MarkHintSeen", profile, "tutorial_equipment");
            Assert.That(Target(), Is.EqualTo("equipment.auto_equip"));
        }
    }
}
