using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class GuardianResonanceDataTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object S(string type, string name, params object[] args) => T(type).GetMethod(name).Invoke(null, args);
        private static object G(string method, params object[] args) => S("Data.GuardianService", method, args);
        private static object Session(string method, params object[] args) => S("Data.GuardianTrialSession", method, args);
        private static object D(string method, params object[] args) => S("Data.GuardianDialogueCatalog", method, args);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object F(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static IList L(object obj, string name) => (IList)P(obj, name);
        private static object Save(object p) => p.GetType().GetMethod("ToSaveData").Invoke(p, new object[] { 61 });
        private static object Restore(object p) => Profile(JsonUtility.FromJson(JsonUtility.ToJson(Save(p)), T("Save.PlayerSaveData")));
        private static object Profile(object save) => Activator.CreateInstance(T("Data.PlayerProfile"), save);
        private static object SavedGuardian(string id, int level, int exp = 0)
        {
            var g = Activator.CreateInstance(T("Save.OwnedGuardianData"));
            Set(g, "Id", id); Set(g, "Level", level); Set(g, "Exp", exp);
            return g;
        }
        private static object NewSave(int floor = 60, params object[] guardians)
        {
            var save = S("Save.PlayerSaveData", "CreateDefault");
            Set(save, "HighestFloor", floor); Set(save, "HasCompletedTutorial", true);
            Set(save, "TutorialStepId", "Complete");
            var list = (IList)F(save, "OwnedGuardians");
            foreach (var guardian in guardians) list.Add(guardian);
            return save;
        }
        private static void Party(object p)
        {
            var monster = p.GetType().GetMethod("AddOwnedMonster").Invoke(p,
                new object[] { "monster_rock_golem", 30, 0, false });
            p.GetType().GetMethod("SetPartyMonsterIds").Invoke(p, new object[] { new[] { (string)F(monster, "InstanceId") } });
        }
        [SetUp] public void Setup() => Session("Reset");
        [TearDown] public void Teardown() => Session("Reset");

        [Test]
        public void SixfoldExperienceRequiresFullThresholdAndCanStillReachMaximum()
        {
            int total = 0;
            for (int level = 1; level < 30; level++)
            {
                int required = (int)G("RequiredExp", level);
                Assert.That(required, Is.EqualTo(6 * (100 + level * 40)));
                total += required;
            }
            Assert.That(total, Is.EqualTo(121800));
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 1)));
            G("Equip", p, "seiryu");
            G("AddBattleExperience", p, 839);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(1));
            p = Restore(p);
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(839));
            G("AddBattleExperience", p, 1);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(2));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(0));
            G("AddBattleExperience", p, total - 840);
            p = Restore(p);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(30));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(0));
        }

        [Test]
        public void OriginalIndividualSavesKeepEachGuardianAndHighestDuplicate()
        {
            var save = NewSave(60, SavedGuardian("seiryu", 4, 15), SavedGuardian("suzaku", 2, 90),
                SavedGuardian("seiryu", 8, 61), SavedGuardian("invalid", 30));
            var p = Profile(save);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(8));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(61));
            Assert.That(G("Level", p, "suzaku"), Is.EqualTo(2));
            Assert.That(G("Experience", p, "suzaku"), Is.EqualTo(90));
            Assert.That(L(p, "OwnedGuardians").Count, Is.EqualTo(2));
            save = Save(p);
            Assert.That(F(save, "GuardianIndividualProgressInitialized"), Is.True);
            Assert.That(F(save, "GuardianSharedProgressInitialized"), Is.False);
            Set(save, "GuardianSharedProgressInitialized", true);
            Set(save, "GuardianSharedLevel", 30);
            var reloaded = Profile(save);
            Assert.That(G("Level", reloaded, "seiryu"), Is.EqualTo(8), "Stale shared fields cannot overwrite individual progress.");
            Assert.That(G("Experience", Restore(reloaded), "suzaku"), Is.EqualTo(90));
        }

        [Test]
        public void SharedVersionProgressTransfersOnceToExistingGuardiansWithoutLosingIndividualProgress()
        {
            var save = NewSave(60, SavedGuardian("seiryu", 1), SavedGuardian("suzaku", 9, 60), SavedGuardian("byakko", 20, 30));
            Set(save, "GuardianSharedProgressInitialized", true);
            Set(save, "GuardianSharedLevel", 12); Set(save, "GuardianSharedExp", 7);
            var p = Profile(save);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(12));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(7));
            Assert.That(G("Level", p, "suzaku"), Is.EqualTo(12));
            Assert.That(G("Level", p, "byakko"), Is.EqualTo(20), "Migration never downgrades a higher valid individual record.");
            Assert.That(G("GrantCore", p, "genbu"), Is.True);
            Assert.That(G("Birth", p, "genbu"), Is.True);
            Assert.That(G("Level", p, "genbu"), Is.EqualTo(1), "A future birth never inherits the retired shared level.");
            G("Equip", p, "seiryu"); G("AddBattleExperience", p, 100);
            p = Restore(p);
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(107));
            Assert.That(G("Experience", p, "suzaku"), Is.EqualTo(7));
            Assert.That(G("Level", p, "genbu"), Is.EqualTo(1));
        }

        [TestCase(-20, -9, 1, 0)]
        [TestCase(30, 5000, 30, 0)]
        [TestCase(1, int.MaxValue, 30, 0)]
        [TestCase(1, 320, 1, 320)]
        [TestCase(1, 1920, 3, 0)]
        public void IndividualMigrationNormalizesCorruptAndOverflowingExperience(int level, int exp, int expectedLevel, int expectedExp)
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", level, exp)));
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(expectedLevel));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(expectedExp));
        }

        [Test]
        public void BattleExpBelongsOnlyToCompanionAndNewBirthStartsAtLevelOne()
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 1), SavedGuardian("suzaku", 1)));
            G("AddBattleExperience", p, 2000);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(1), "No companion means no guardian battle EXP.");
            G("Equip", p, "seiryu");
            G("AddBattleExperience", p, 2000);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(3));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(80));
            Assert.That(G("Level", p, "suzaku"), Is.EqualTo(1));
            Assert.That(G("Experience", p, "suzaku"), Is.EqualTo(0));
            Assert.That(G("GrantCore", p, "byakko"), Is.True);
            Assert.That(G("Birth", p, "byakko"), Is.True);
            Assert.That(G("Level", p, "byakko"), Is.EqualTo(1));
            Assert.That(G("Experience", p, "byakko"), Is.EqualTo(0));
            G("Equip", p, "suzaku"); G("AddBattleExperience", p, 840);
            Assert.That(G("Level", p, "suzaku"), Is.EqualTo(2));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(80));
            G("Equip", p, "seiryu");
            G("AddBattleExperience", p, int.MaxValue);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(30));
            Assert.That(G("Experience", p, "seiryu"), Is.EqualTo(0));
            p = Restore(p);
            Assert.That(G("Level", p, "seiryu"), Is.EqualTo(30));
            Assert.That(G("Level", p, "suzaku"), Is.EqualTo(2));
            Assert.That(G("Level", p, "byakko"), Is.EqualTo(1));
        }

        [Test]
        public void AlternateContractUnlocksAtTenIsFreeAndPersistsIndividually()
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 9), SavedGuardian("genbu", 1)));
            Assert.That(G("SetContract", p, "seiryu", "alternate"), Is.False);
            G("Equip", p, "seiryu");
            G("AddBattleExperience", p, G("RequiredExp", 9));
            Assert.That(G("SetContract", p, "seiryu", "invalid"), Is.False);
            Assert.That(G("SetContract", p, "byakko", "alternate"), Is.False);
            int gold = (int)P(p, "Gold"); int stones = (int)P(p, "FreeGachaStones");
            Assert.That(G("SetContract", p, "seiryu", "alternate"), Is.True);
            Assert.That(G("CanUseAlternate", p, "genbu"), Is.False, "Another guardian reaching ten cannot unlock this one's contract.");
            Assert.That(G("SetContract", p, "genbu", "alternate"), Is.False);
            Assert.That(G("ContractLabel", "seiryu", "alternate"), Is.EqualTo("追撃"));
            p = Restore(p);
            Assert.That(G("ContractId", p, "seiryu"), Is.EqualTo("alternate"));
            Assert.That(G("ContractId", p, "genbu"), Is.EqualTo("basic"));
            Assert.That(G("SetContract", p, "seiryu", "basic"), Is.True);
            Assert.That(P(p, "Gold"), Is.EqualTo(gold));
            Assert.That(P(p, "FreeGachaStones"), Is.EqualTo(stones));
        }

        [TestCase(false, 5)] [TestCase(true, 1)]
        public void PracticeLendsUnownedGuardianAndCannotChangeSavedProfile(bool elite, int enemyCount)
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 20)));
            Party(p); G("Equip", p, "seiryu");
            string before = JsonUtility.ToJson(Save(p));
            Assert.That(Session("BeginPracticeWithContract", p, "suzaku", elite, "alternate"), Is.False);
            Assert.That(Session("BeginPracticeWithContract", p, "suzaku", elite, "basic"), Is.True);
            Assert.That(T("Data.GuardianTrialSession").GetProperty("LevelOverride").GetValue(null), Is.EqualTo(1));
            Assert.That(T("Data.GuardianTrialSession").GetProperty("IsBorrowedGuardian").GetValue(null), Is.True);
            Assert.That(T("Data.GuardianTrialSession").GetProperty("EnemyCount").GetValue(null), Is.EqualTo(enemyCount));
            Assert.That(Session("BeginPractice", p, "seiryu", false), Is.False, "Repeated starts do not overwrite a session.");
            Assert.That(Session("Win", p), Is.False);
            Assert.That(Session("CompleteOath", p, true), Is.False);
            Assert.That(G("SetContract", p, "seiryu", "alternate"), Is.False);
            G("AddBattleExperience", p, 9999);
            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = Session("Enemy", i);
                Assert.That(F(enemy, "enemyId").ToString(), Does.Not.StartWith("guardian_trial_"));
                Assert.That(F(enemy, "rewardExp"), Is.EqualTo(0));
                Assert.That(F(enemy, "rewardGold"), Is.EqualTo(0));
            }
            Session("End");
            Assert.That(JsonUtility.ToJson(Save(p)), Is.EqualTo(before));
            Assert.That(P(Restore(p), "EquippedGuardianId"), Is.EqualTo("seiryu"));
        }

        [Test]
        public void OwnedPracticeUsesThatGuardiansOwnLevelAndUnlockedContract()
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 20), SavedGuardian("genbu", 3)));
            Party(p); G("Equip", p, "genbu");
            string before = JsonUtility.ToJson(Save(p));
            Assert.That(Session("BeginPracticeWithContract", p, "seiryu", false, "alternate"), Is.True);
            Assert.That(T("Data.GuardianTrialSession").GetProperty("LevelOverride").GetValue(null), Is.EqualTo(20));
            Assert.That(T("Data.GuardianTrialSession").GetProperty("ContractId").GetValue(null), Is.EqualTo("alternate"));
            Assert.That(T("Data.GuardianTrialSession").GetProperty("IsBorrowedGuardian").GetValue(null), Is.False);
            Session("End");
            Assert.That(JsonUtility.ToJson(Save(p)), Is.EqualTo(before));
        }

        [Test]
        public void OathRequiresStoryCompletionLevelThirtyTargetCompanionAndOrdinarySurvivor()
        {
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 30), SavedGuardian("suzaku", 1)));
            Party(p); G("Equip", p, "seiryu");
            Assert.That(Session("BeginOath", p, "seiryu"), Is.False);
            L(p, "SeenStoryEventIds").Add("story_first_arc_complete");
            G("Equip", p, "suzaku");
            Assert.That(Session("BeginOath", p, "suzaku"), Is.False);
            G("Equip", p, "seiryu");
            Assert.That(Session("BeginOath", p, "seiryu"), Is.True);
            string before = JsonUtility.ToJson(Save(p));
            Assert.That(Session("CompleteOath", p, false), Is.False);
            Assert.That(Session("Win", p), Is.False, "Oath cannot award an acquisition core.");
            Assert.That(Session("CompleteOath", Restore(p), true), Is.False, "A stale result cannot grant to another profile.");
            Assert.That(JsonUtility.ToJson(Save(p)), Is.EqualTo(before));
            Assert.That(Session("CompleteOath", p, true), Is.True);
            Assert.That(Session("CompleteOath", p, true), Is.False);
            Assert.That(L(p, "GuardianOathIds").Count, Is.EqualTo(1));
            var cosmeticOnly = Save(p); ((IList)F(cosmeticOnly, "GuardianOathIds")).Clear();
            Assert.That(JsonUtility.ToJson(cosmeticOnly), Is.EqualTo(before), "Only cosmetic completion is added.");
            Session("End"); p = Restore(p);
            Assert.That(G("OathCleared", p, "seiryu"), Is.True);
            Assert.That(Session("BeginOath", p, "seiryu"), Is.True, "Cosmetic trials may be replayed.");
            Assert.That(Session("CompleteOath", p, true), Is.False, "A replay has no additional reward.");
        }

        [Test]
        public void DialogueUnlocksPerGuardianAndStageWithSeparateReadFlags()
        {
            var definitions = ((Array)T("Data.GuardianDialogueCatalog").GetProperty("All").GetValue(null)).Cast<object>().ToArray();
            Assert.That(definitions.Length, Is.EqualTo(12));
            Assert.That(definitions.Select(d => (string)P(d, "EventId")).Distinct().Count(), Is.EqualTo(12));
            foreach (var definition in definitions)
            {
                Assert.That(((Array)P(definition, "Lines")).Length, Is.EqualTo(4));
                foreach (var line in (Array)P(definition, "Lines"))
                {
                    Assert.That(P(line, "Speaker"), Is.Not.Empty);
                    Assert.That(P(line, "Text"), Is.Not.Empty);
                }
            }
            var p = Profile(NewSave(60, SavedGuardian("seiryu", 20), SavedGuardian("suzaku", 19)));
            Assert.That(D("CanRead", p, "guardian_seiryu_birth"), Is.True);
            Assert.That(D("CanRead", p, "guardian_seiryu_bond"), Is.True);
            Assert.That(D("CanRead", p, "guardian_seiryu_oath"), Is.False);
            Assert.That(D("CanRead", p, "guardian_suzaku_bond"), Is.False, "Bond conversation checks the selected guardian's level.");
            Assert.That(D("CanRead", p, "guardian_genbu_birth"), Is.False);
            Assert.That(D("MarkSeen", p, "guardian_genbu_birth"), Is.False);
            Assert.That(D("MarkSeen", p, "guardian_seiryu_birth"), Is.True);
            Assert.That(D("MarkSeen", p, "guardian_seiryu_birth"), Is.False);
            Assert.That(L(p, "SeenStoryEventIds"), Is.Empty);
            Assert.That(L(p, "SeenTutorialHintIds"), Is.Empty);
            Assert.That(D("HasSeen", Restore(p), "guardian_seiryu_birth"), Is.True);
        }
    }
}
