using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class DungeonExperienceRewardsTests
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject owner;
        private object game;
        private object master;
        private object oldGame;
        private object oldMaster;
        private object oldSave;
        private SceneSetup[] previousScenes;
        private UnityEngine.Random.State previousRandomState;

        [SetUp]
        public void SetUp()
        {
            previousRandomState = UnityEngine.Random.state;
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = RuntimeType("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = RuntimeType("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = RuntimeType("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            owner = new GameObject("DungeonExperienceRewardTests");
            owner.SetActive(false);
            game = owner.AddComponent(RuntimeType("Managers.GameManager"));
            master = owner.AddComponent(RuntimeType("Managers.MasterDataManager"));
            RuntimeType("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            RuntimeType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            RuntimeType("Managers.SaveManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            Static("Data.GuardianTrialSession", "Reset");
        }

        [TearDown]
        public void TearDown()
        {
            Static("Data.GuardianTrialSession", "Reset");
            UnityEngine.Object.DestroyImmediate(owner);
            RuntimeType("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
            RuntimeType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
            RuntimeType("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSave);
            if (previousScenes.Length > 0 && previousScenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            UnityEngine.Random.state = previousRandomState;
        }

        // Stages are dungeons of ten floors, not individual floors. These pairs
        // cover each stage boundary and retain the complete opening reward curve.
        [TestCase(1, "blight_cavern", 28, 11)]
        [TestCase(10, "blight_cavern", 45, 38)]
        [TestCase(11, "gear_crypt", 61, 46)]
        [TestCase(20, "gear_crypt", 85, 73)]
        [TestCase(21, "curse_library", 234, 81)]
        [TestCase(30, "curse_library", 342, 108)]
        [TestCase(31, "ember_drake_pass", 582, 116)]
        [TestCase(40, "ember_drake_pass", 815, 143)]
        [TestCase(41, "star_ore_citadel", 1056, 151)]
        [TestCase(50, "star_ore_citadel", 1593, 178)]
        [TestCase(51, "abyssal_grimoire_spire", 1880, 186)]
        [TestCase(60, "abyssal_grimoire_spire", 2772, 213)]
        public void StageBoundariesIncreaseOnlyExperience(int floor, string dungeonId, int exp, int gold)
        {
            object dungeon = Static("Data.BattleDungeonCatalog", "GetDungeonForGlobalFloor", floor);
            Assert.That(Property(dungeon, "DungeonId"), Is.EqualTo(dungeonId));
            var enemy = (UnityEngine.Object)Static("Data.BattleDungeonCatalog", "CreateEnemyDataForGlobalFloor", floor, master);
            Assert.That(enemy, Is.Not.Null);
            try
            {
                Assert.That(Field<int>(enemy, "rewardExp"), Is.EqualTo(exp));
                Assert.That(Field<int>(enemy, "rewardGold"), Is.EqualTo(gold), "Stage EXP must not change gold.");
                Assert.That(Field<string>(enemy, "dropTableId"), Is.EqualTo("drop_common_floor"));
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy); }
        }

        [TestCase(20, "monster_armed_droid", 104, 73)]
        [TestCase(30, "monster_abyss_dragon", 440, 108)]
        [TestCase(40, "monster_abyss_grand_mage_seraphis", 1222, 143)]
        [TestCase(50, "monster_seraph_michael", 2589, 178)]
        [TestCase(60, "monster_demon_king_garza", 4358, 213)]
        public void FinalBossDataUsesStageExperienceWithoutChangingItsGold(int floor, string monsterId, int exp, int gold)
        {
            var candidates = (string[])Static("Data.BattleDungeonCatalog", "ResolveBossMonsterIds", floor);
            Assert.That(candidates, Does.Contain(monsterId));
            var enemy = (UnityEngine.Object)Static("Data.BattleDungeonCatalog", "CreateEnemyDataForMonsterAtGlobalFloor", floor, master, monsterId);
            Assert.That(enemy, Is.Not.Null);
            try
            {
                Assert.That(Field<int>(enemy, "rewardExp"), Is.EqualTo(exp));
                Assert.That(Field<int>(enemy, "rewardGold"), Is.EqualTo(gold));
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy); }
        }

        [Test]
        public void RewardModifiersApplyOnceAfterStageIncreaseAndFirstClearChangesOnlyGold()
        {
            object profile = CreateProfile();
            // Soul-tree fields in old saves are retained for compatibility; the
            // current removed system must not accidentally reactivate a bonus.
            Call(profile, "SetRebirthSkillLevel", "exp_memory", 10);
            Assert.That(Call(profile, "GetExpRewardMultiplier"), Is.EqualTo(1f));
            object modifier = Call(ExperienceModifier(1.2f), "Combine", ExperienceModifier(1.25f));
            object repeated = Static("Battle.BattleRewardCalculator", "Calculate", 31, 31, modifier);
            object firstClear = Static("Battle.BattleRewardCalculator", "Calculate", 31, 30, modifier);
            Assert.That(Property(repeated, "Exp"), Is.EqualTo(873), "582 stage EXP × 1.5 combined spirit modifier.");
            Assert.That(Property(firstClear, "Exp"), Is.EqualTo(873), "First-clear gold must not grant extra EXP.");
            Assert.That(Property(repeated, "Gold"), Is.EqualTo(232));
            Assert.That(Property(firstClear, "Gold"), Is.EqualTo(366));
        }

        [TestCase(31, 0, 60, 0, 0)]
        [TestCase(31, 1, 60, 9, 3)]
        [TestCase(31, 30, 60, 291, 116)]
        [TestCase(31, 60, 60, 582, 232)]
        [TestCase(60, 50, 155, 894, 137)]
        public void RetreatKeepsDefeatRatioAndGoldWhileUsingIncreasedExperience(int floor, int defeated, int enemyCount, int exp, int gold)
        {
            CreateProfile();
            object identity = RuntimeType("Battle.BattleSpiritModifier").GetProperty("Identity").GetValue(null);
            object fullReward = Static("Battle.BattleRewardCalculator", "Calculate", floor, floor, identity);
            object partial = Static("Battle.BattleSceneController", "ScaleRewardForDefeatedEnemies", fullReward, defeated, enemyCount);
            Assert.That(Property(partial, "Exp"), Is.EqualTo(exp));
            Assert.That(Property(partial, "Gold"), Is.EqualTo(gold));
        }

        [Test]
        public void IncreasedRewardReachesEachPartyMonsterAndGuardianWithoutDividingOrTrainingBench()
        {
            object profile = CreateProfile();
            var party = (IList)Property(profile, "PartyMonsterInstanceIds");
            party.Clear();
            object michael = Call(profile, "AddOwnedMonster", "monster_seraph_michael", 20, 0, false);
            object titania = Call(profile, "AddOwnedMonster", "monster_spirit_queen_titania", 20, 0, false);
            object capped = Call(profile, "AddOwnedMonster", "monster_mecha_dragon_valdrake", 80, 0, false);
            object bench = Call(profile, "AddOwnedMonster", "monster_dark_magic_machine_god_merchion", 20, 0, false);
            party.Add(Field<string>(michael, "InstanceId"));
            party.Add(Field<string>(titania, "InstanceId"));
            party.Add(Field<string>(capped, "InstanceId"));
            foreach (object monster in new[] { michael, titania, bench }) SetField(monster, "Exp", 7);
            Assert.That(Static("Data.GuardianService", "GrantCore", profile, "seiryu"), Is.EqualTo(true));
            Assert.That(Static("Data.GuardianService", "Birth", profile, "seiryu"), Is.EqualTo(true));
            Assert.That(Static("Data.GuardianService", "Equip", profile, "seiryu"), Is.EqualTo(true));
            object guardian = Static("Data.GuardianService", "Owned", profile, "seiryu");
            SetField(guardian, "Level", 20);
            SetField(guardian, "Exp", 7);
            object modifier = Static("Data.GuardianService", "Modifier", "seiryu");
            object reward = Static("Battle.BattleRewardCalculator", "Calculate", 51, 51, modifier);
            int exp = (int)Property(reward, "Exp");
            Assert.That(exp, Is.EqualTo(1880));
            object controller = owner.AddComponent(RuntimeType("Battle.BattleSceneController"));
            Assert.That(Call(controller, "ApplyPartyMonsterExp", profile, exp), Is.EqualTo(3));
            Assert.That(Field<int>(michael, "Exp"), Is.EqualTo(1887));
            Assert.That(Field<int>(titania, "Exp"), Is.EqualTo(1887));
            Assert.That(Field<int>(bench, "Exp"), Is.EqualTo(7), "Bench monsters must not receive battle EXP.");
            Assert.That(Field<int>(guardian, "Exp"), Is.EqualTo(1887), "The equipped guardian receives the same full reward.");
            Assert.That(Field<int>(capped, "Level"), Is.EqualTo(80));
            Assert.That(Field<int>(capped, "Exp"), Is.Zero, "Level caps continue discarding excess EXP.");
        }

        private object CreateProfile()
        {
            object save = Static("Save.PlayerSaveData", "CreateDefault");
            SetField(save, "HighestFloor", 60);
            SetField(save, "HasCompletedTutorial", true);
            SetField(save, "TutorialStepId", "Complete");
            object profile = Activator.CreateInstance(RuntimeType("Data.PlayerProfile"), new[] { save });
            game.GetType().GetProperty("PlayerProfile").SetValue(game, profile);
            return profile;
        }

        private static object ExperienceModifier(float multiplier) => Activator.CreateInstance(
            RuntimeType("Battle.BattleSpiritModifier"),
            new object[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0, 0f, 0f, 1f, multiplier });

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower." + name, true);

        private static object Static(string type, string method, params object[] args) => RuntimeType(type)
            .GetMethods(StaticFlags).Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
            .Invoke(null, args);

        private static object Call(object target, string method, params object[] args) => target.GetType()
            .GetMethods(InstanceFlags).Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
            .Invoke(target, args);

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static TValue Field<TValue>(object target, string name) => (TValue)target.GetType().GetField(name).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    }
}
