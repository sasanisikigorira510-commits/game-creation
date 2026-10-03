using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class TutorialNavigationRecoveryTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object P(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static object Tutorial(string name, params object[] args) => T("Data.StoryTutorialService")
            .GetMethod(name).Invoke(null, args);

        // A restarted app always enters HomeScene, even when its saved lesson
        // belongs to a different screen. Every unfinished step needs a route
        // back to that lesson, or a conversation the home guide can advance.
        [TestCase("T00", "")]
        [TestCase("T01", "home.gacha")]
        [TestCase("T02", "home.gacha")]
        [TestCase("T02A", "")]
        [TestCase("T03", "home.formation")]
        [TestCase("T04", "home.formation")]
        [TestCase("T05", "home.battle")]
        [TestCase("T06", "home.battle")]
        [TestCase("T07", "home.battle")]
        [TestCase("T07A", "home.equipment")]
        [TestCase("T07B", "home.equipment")]
        [TestCase("T07C_SHOP", "home.shop")]
        [TestCase("T07D_SHOP", "home.shop")]
        [TestCase("T07C", "home.dex")]
        [TestCase("T07D", "home.dex")]
        [TestCase("T08", "")]
        public void EveryUnfinishedLessonHasAHomeRecoveryRoute(string step, string expectedTarget)
        {
            object profile = CreateProfile(step);
            string before = Snapshot(profile);

            for (int visit = 0; visit < 3; visit++)
            {
                object next = Tutorial("GetNextEvent", profile, "HomeScene");
                AssertValidTarget(next, step, expectedTarget);
                Assert.That(P(next, "BlocksInput"), Is.True,
                    "The resumed lesson must retain the tutorial's focused input policy.");
            }

            Assert.That(Snapshot(profile), Is.EqualTo(before),
                "Looking up a recovery route must not complete a lesson, summon a monster, or grant/spend resources.");
        }

        [TestCase("T02", 0, "home.gacha")]
        [TestCase("T02", 1, "home.gacha")]
        [TestCase("T02", 2, "home.gacha")]
        [TestCase("T04", 3, "home.formation")]
        [TestCase("T06", 3, "home.battle")]
        [TestCase("T07", 3, "home.battle")]
        public void RestoringAPartiallyCompletedLessonOnlyShowsWhereToContinue(string step, int summons, string target)
        {
            object profile = CreateProfile(step, summons);
            // Go through the same save representation used across process death.
            object saved = profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 });
            object loaded = JsonUtility.FromJson(JsonUtility.ToJson(saved), T("Save.PlayerSaveData"));
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { loaded });
            string before = Snapshot(profile);

            AssertValidTarget(Tutorial("GetNextEvent", profile, "HomeScene"), step, target);

            Assert.That(P(profile, "TutorialStepId"), Is.EqualTo(step));
            Assert.That(P(profile, "InitialTutorialSummonCount"), Is.EqualTo(summons));
            Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(137));
            Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(43));
            Assert.That(Snapshot(profile), Is.EqualTo(before));
        }

        [TestCase(1, false, "home.equipment")]
        [TestCase(0, true, "home.battle")]
        [TestCase(1, true, "home.equipment")]
        public void OnlySavedFloorClearRoutesToEquipmentWithoutReplayingOrRegrantingTheBattle(
            int highestFloor, bool victorySeen, string expectedTarget)
        {
            object profile = CreateProfile("T07", 3, highestFloor, victorySeen);
            string before = Snapshot(profile);

            for (int visit = 0; visit < 3; visit++)
            {
                object next = Tutorial("GetNextEvent", profile, "HomeScene");
                AssertValidTarget(next, "T07", expectedTarget);
                Assert.That(P(next, "BlocksInput"), Is.True);
            }

            Assert.That(Snapshot(profile), Is.EqualTo(before),
                "The saved victory only changes the resume destination; selecting it owns the later step transition.");
        }

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        public void ReturningFromFirstResultCanResumeThroughTheAllowedHomeAction(int highestFloor, bool victorySeen)
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            PropertyInfo gameSingleton = T("Managers.GameManager").GetProperty("Instance");
            PropertyInfo saveSingleton = T("Managers.SaveManager").GetProperty("Instance");
            object oldGame = gameSingleton.GetValue(null);
            object oldSave = saveSingleton.GetValue(null);
            GameObject owner = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                owner = new GameObject("TutorialNavigationRecoveryFixture");
                owner.SetActive(false);
                Component game = owner.AddComponent(T("Managers.GameManager"));
                gameSingleton.SetValue(null, game);
                saveSingleton.SetValue(null, null);
                object profile = CreateProfile("T07", 3, highestFloor, victorySeen);
                game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
                Component home = owner.AddComponent(T("Home.HomeSceneController"));
                MethodInfo navigate = home.GetType().GetMethod("TryAdvanceHomeTutorialForTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                string before = Snapshot(profile);

                Assert.That(navigate.Invoke(home, new object[] { "home.dex" }), Is.False,
                    "Recovery must not unlock unrelated tutorial destinations.");
                Assert.That(Snapshot(profile), Is.EqualTo(before));

                if (highestFloor == 0)
                {
                    Assert.That(navigate.Invoke(home, new object[] { "home.equipment" }), Is.False,
                        "A result story marker alone must not skip an unfinished first battle.");
                    Assert.That(navigate.Invoke(home, new object[] { "home.battle" }), Is.True);
                    Assert.That(P(profile, "TutorialStepId"), Is.EqualTo("T07"));
                    Assert.That(Snapshot(profile), Is.EqualTo(before));
                }
                else
                {
                    Assert.That(navigate.Invoke(home, new object[] { "home.equipment" }), Is.True);
                    Assert.That(P(profile, "TutorialStepId"), Is.EqualTo("T07B"),
                        "The existing equipment screen must receive its actual lesson step after the result was interrupted.");
                    Assert.That(P(profile, "HasCompletedTutorial"), Is.False);
                    Assert.That(P(profile, "Gold"), Is.EqualTo(100));
                    Assert.That(P(profile, "FreeGachaStones"), Is.EqualTo(137));
                    Assert.That(P(profile, "PaidGachaStones"), Is.EqualTo(43));
                    Assert.That(P(profile, "InitialTutorialSummonCount"), Is.EqualTo(3));
                    Assert.That(((IList)P(profile, "OwnedMonsters")).Count, Is.Zero);
                    string afterFirstAction = Snapshot(profile);
                    Assert.That(navigate.Invoke(home, new object[] { "home.equipment" }), Is.True);
                    Assert.That(Snapshot(profile), Is.EqualTo(afterFirstAction),
                        "Repeated resume input must not grant a second reward or advance the equipment lesson.");
                }
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                gameSingleton.SetValue(null, oldGame);
                saveSingleton.SetValue(null, oldSave);
                if (scenes.Length > 0 && scenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }

        [TestCase("T02", "GachaScene", "gacha.single_free", "tutorial_first_summon")]
        [TestCase("T04", "FormationScene", "formation.slot_1", "tutorial_first_formation")]
        [TestCase("T06", "DungeonSelectionPanel", "dungeon.start", "tutorial_choose_first_dungeon")]
        [TestCase("T07", "BattleScene", "result.return_home", "story_first_battle_win")]
        public void HomeRecoveryKeepsTheOriginalLessonInItsOwnScreen(string step, string scene, string target, string eventId)
        {
            object profile = CreateProfile(step);
            string before = Snapshot(profile);
            Tutorial("GetNextEvent", profile, "HomeScene");

            object next = Tutorial("GetNextEvent", profile, scene);
            AssertValidTarget(next, step, target);
            Assert.That(P(next, "EventId"), Is.EqualTo(eventId));
            Assert.That(P(next, "BlocksInput"), Is.True);
            Assert.That(Snapshot(profile), Is.EqualTo(before));
        }

        private static void AssertValidTarget(object next, string step, string target)
        {
            Assert.That(next, Is.Not.Null, step + " must not leave the home menu with no available tutorial action.");
            Assert.That(P(next, "IsValid"), Is.True, step);
            Assert.That(P(next, "StepId"), Is.EqualTo(step));
            Assert.That(P(next, "TargetKey"), Is.EqualTo(target));
            Assert.That((string)P(next, "Title"), Is.Not.Empty);
            Assert.That((string)P(next, "Body"), Is.Not.Empty);
        }

        private static object CreateProfile(string step, int summons = 0, int highestFloor = 0, bool victorySeen = false)
        {
            Type saveType = T("Save.PlayerSaveData");
            object save = saveType.GetMethod("CreateDefault").Invoke(null, null);
            saveType.GetField("TutorialStepId").SetValue(save, step);
            saveType.GetField("HasCompletedTutorial").SetValue(save, false);
            saveType.GetField("InitialTutorialSummonCount").SetValue(save, summons);
            saveType.GetField("FreeGachaStones").SetValue(save, 137);
            saveType.GetField("PaidGachaStones").SetValue(save, 43);
            saveType.GetField("HighestFloor").SetValue(save, highestFloor);
            if (victorySeen)
                ((IList)saveType.GetField("SeenStoryEventIds").GetValue(save)).Add("story_first_battle_win");
            return Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
        }

        private static string Snapshot(object profile) => JsonUtility.ToJson(profile.GetType()
            .GetMethod("ToSaveData").Invoke(profile, new object[] { 1 }));
    }
}
