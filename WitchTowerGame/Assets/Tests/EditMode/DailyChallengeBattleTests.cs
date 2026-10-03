using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class DailyChallengeBattleTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object S(string t, string m, params object[] args) => T(t).GetMethod(m).Invoke(null, args);
        private static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m).Invoke(o, args);
        private static object F(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object value) => o.GetType().GetField(n).SetValue(o, value);
        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static IList PrivateList(object o, string n) => (IList)o.GetType().GetField(n, Hidden).GetValue(o);
        private object game, master, profile, oldGame, oldMaster, oldSave;
        private SceneSetup[] scenes;
        private UnityEngine.Random.State randomState;
        private readonly List<object> runs = new List<object>();

        [SetUp]
        public void Setup()
        {
            randomState = UnityEngine.Random.state;
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("DailyChallengeTestManagers"); owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager")); master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            S("Data.GuardianTrialSession", "Reset"); S("Data.DailyChallengeSession", "Reset");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            Set(save, "CurrentFloor", 38); Set(save, "HighestFloor", 37);
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            T("Managers.GameManager").GetProperty("PlayerProfile").SetValue(game, profile);
            Call(game, "SetCurrentFloor", 38);
            var slots = (IList)P(profile, "PartyMonsterInstanceIds"); slots.Clear();
            foreach (string id in new[] { "monster_dragon_whelp", "monster_rock_golem" })
            {
                var owned = Call(profile, "AddOwnedMonster", id, 10, 0, false);
                slots.Add(F(owned, "InstanceId"));
            }
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var run in runs) { S("Data.DailyChallengeSession", "Begin", run); S("Data.DailyChallengeSession", "End", true); }
            runs.Clear(); S("Data.DailyChallengeSession", "Reset"); S("Data.GuardianTrialSession", "Reset");
            UnityEngine.Random.state = randomState;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSave);
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }

        private object Run(string mode, int cleared = 0)
        {
            var run = Activator.CreateInstance(T("Data.DailyChallengeRun"));
            Set(run, "RunId", "test-daily-" + Guid.NewGuid().ToString("N")); Set(run, "Mode", mode);
            Set(run, "StartDay", "2026-10-03"); Set(run, "ClearedStage", cleared); Set(run, "Status", "active");
            Set(run, "PartyInstanceIds", ((IList)P(profile, "PartyMonsterInstanceIds")).Cast<string>().ToArray());
            runs.Add(run); Assert.That(S("Data.DailyChallengeSession", "Begin", run), Is.True); return run;
        }
        private object Simulator()
        {
            var owner = new GameObject("DailyChallengeSimulator"); owner.SetActive(false);
            return owner.AddComponent(T("Battle.BattleSimulator"));
        }
        private static object CurrentRun => T("Data.DailyChallengeSession").GetProperty("Run").GetValue(null);

        [Test]
        public void FixedRewardTableAndJapanMidnightAreIndependentOfDungeonProgress()
        {
            Assert.That(Enumerable.Range(1, 10).Select(n => S("Data.DailyChallengeCatalog", "Drops", n)),
                Is.EqualTo(new object[] { 1, 1, 2, 2, 3, 0, 0, 0, 0, 0 }));
            Assert.That(Enumerable.Range(1, 10).Select(n => S("Data.DailyChallengeCatalog", "StarCores", n)),
                Is.EqualTo(new object[] { 0, 0, 0, 0, 1, 3, 4, 4, 5, 5 }));
            Assert.That(S("Data.DailyChallengeCatalog", "JapanDay", new DateTime(2026, 10, 3, 14, 59, 59, DateTimeKind.Utc)), Is.EqualTo("2026-10-03"));
            Assert.That(S("Data.DailyChallengeCatalog", "JapanDay", new DateTime(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc)), Is.EqualTo("2026-10-04"));
            var a = (UnityEngine.Object)S("Data.DailyChallengeCatalog", "CreateEnemy", "avalanche", 7, 0);
            Call(game, "SetCurrentFloor", 1);
            var b = (UnityEngine.Object)S("Data.DailyChallengeCatalog", "CreateEnemy", "avalanche", 7, 0);
            try
            {
                foreach (string field in new[] { "maxHp", "attack", "defense", "enemyId" }) Assert.That(F(a, field), Is.EqualTo(F(b, field)));
                Assert.That(F(a, "canBeRecruited"), Is.False);
                Assert.That(F(a, "rewardGold"), Is.EqualTo(0)); Assert.That(F(a, "rewardExp"), Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        [TestCase("avalanche", 30)]
        [TestCase("champion", 1)]
        public void BurstSpawnsObeyModeCapAndMidFightRestoresExactEnemies(string mode, int cap)
        {
            Run(mode, 5);
            var sim = Simulator(); Call(sim, "Setup", 999); Call(sim, "TickPreparation", 20f);
            Assert.That(P(sim, "CurrentFloor"), Is.EqualTo(6));
            Assert.That(P(sim, "CurrentActiveEnemyCount"), Is.EqualTo(cap));
            var enemy = PrivateList(sim, "activeEnemyRuntimes")[0]; Set(F(enemy, "Stats"), "CurrentHp", 13);
            Set(enemy, "AttackTimer", .31f); Set(enemy, "PositionAnchor", new Vector2(.64f, .47f));
            var checkpoint = Call(sim, "CaptureDailyChallengeCheckpoint", false);
            S("Data.DailyChallengeSession", "SetCheckpoint", checkpoint);
            var resumed = Simulator(); Call(resumed, "Setup", 1);
            Assert.That(P(resumed, "CurrentActiveEnemyCount"), Is.EqualTo(cap));
            Assert.That(P(resumed, "CurrentSpawnedEnemyCount"), Is.EqualTo(cap));
            var restored = PrivateList(resumed, "activeEnemyRuntimes")[0];
            Assert.That(F(F(restored, "Stats"), "CurrentHp"), Is.EqualTo(13));
            Assert.That(F(restored, "RuntimeId"), Is.EqualTo(F(enemy, "RuntimeId")));
            Assert.That(F(restored, "AttackTimer"), Is.EqualTo(.31f).Within(.0001f));
            Assert.That(F(restored, "PositionAnchor"), Is.EqualTo(new Vector2(.64f, .47f)));
        }

        [Test]
        public void NextStageCarriesWoundedAndDeadPartyFrozenStatsAndBothCooldownKinds()
        {
            Run("champion"); var sim = Simulator(); Call(sim, "Setup", 1);
            var allies = PrivateList(sim, "activeAllyRuntimes");
            Set(F(allies[0], "Stats"), "CurrentHp", 11); Set(F(allies[1], "Stats"), "CurrentHp", 0);
            Set(allies[0], "AttackTimer", .7f);
            var skills = sim.GetType().GetField("skillSet", Hidden).GetValue(sim);
            var strike = Call(skills, "Get", Enum.ToObject(T("Battle.BattleSkillType"), 0));
            Call(strike, "Trigger"); Call(strike, "Tick", 1.5f);
            int attack = (int)F(F(allies[0], "Stats"), "Attack");
            var checkpoint = Call(sim, "CaptureDailyChallengeCheckpoint", true);
            var confirmed = S("Data.DailyChallengeSession", "Clone", CurrentRun);
            Set(confirmed, "ClearedStage", 1); Set(confirmed, "Checkpoint", checkpoint);
            S("Data.DailyChallengeSession", "Accept", confirmed);
            var originalIds = (string[])F(confirmed, "PartyInstanceIds");
            Set(Call(profile, "GetOwnedMonster", originalIds[0]), "Level", 99);
            ((IList)P(profile, "PartyMonsterInstanceIds")).Clear();
            Call(sim, "Setup", 2);
            var restored = PrivateList(sim, "activeAllyRuntimes");
            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(F(F(restored[0], "Stats"), "CurrentHp"), Is.EqualTo(11));
            Assert.That(F(F(restored[1], "Stats"), "CurrentHp"), Is.EqualTo(0), "KO does not revive at a stage boundary.");
            Assert.That(F(F(restored[0], "Stats"), "Attack"), Is.EqualTo(attack), "Run stats do not change after leveling or changing formation at home.");
            Assert.That(F(restored[0], "AttackTimer"), Is.EqualTo(.7f).Within(.0001f));
            skills = sim.GetType().GetField("skillSet", Hidden).GetValue(sim);
            Assert.That(P(Call(skills, "Get", Enum.ToObject(T("Battle.BattleSkillType"), 0)), "RemainingCooldown"), Is.EqualTo(4.5f).Within(.0001f));
            Assert.That(P(game, "CurrentFloor"), Is.EqualTo(38)); Assert.That(P(profile, "HighestFloor"), Is.EqualTo(37));
        }

        [TestCase("avalanche", 60)]
        [TestCase("champion", 1)]
        public void StageOnlyClearsAfterItsEntireSpecifiedGroupIsDefeated(string mode, int total)
        {
            Run(mode, 9); var sim = Simulator(); Call(sim, "Setup", 10);
            string result = "None";
            for (int step = 0; step < 250 && result == "None"; step++)
            {
                Call(sim, "TickPreparation", .7f);
                foreach (var enemy in PrivateList(sim, "activeEnemyRuntimes")) Set(F(enemy, "Stats"), "CurrentHp", 0);
                result = Call(sim, "Tick", .01f).ToString();
            }
            Assert.That(result, Is.EqualTo("Win"));
            Assert.That(P(sim, "CurrentSpawnedEnemyCount"), Is.EqualTo(total));
            Assert.That(P(sim, "CurrentRemainingEnemyCount"), Is.EqualTo(0));
            Assert.That(F(CurrentRun, "ClearedStage"), Is.EqualTo(9), "Simulation completion cannot locally grant server progression/rewards.");
        }

        [Test]
        public void DailyResultAndAutoRepeatDoNotAdvanceNormalDungeonOrTutorial()
        {
            Run("champion");
            var owner = new GameObject("DailyController"); owner.SetActive(false);
            var controller = owner.AddComponent(T("Battle.BattleSceneController"));
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 38));
            Call(controller, "StartAutoRepeatSameFloor"); Call(controller, "RetryClearedFloor");
            controller.GetType().GetMethod("TryApplyMonsterRecruitmentForDefeatedEnemy", Hidden).Invoke(controller, new object[] { null, false });
            Assert.That(P(controller, "CanEnableBattleAutoRepeat"), Is.False);
            Assert.That(P(controller, "IsPermanentEffectsInputBlocked"), Is.True);
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 38)), Is.EqualTo(before));
        }

        [Test]
        public void MeasureReferencePartiesAgainstFirstFifthAndTenthStages()
        {
            string[][] parties = {
                new[] { "monster_rock_golem", "monster_apprentice_swordsman", "monster_dragon_whelp", "monster_chibi_gear", "monster_apprentice_mage" },
                new[] { "monster_ore_giant_garm", "monster_holy_armor_leon", "monster_flare_drake", "monster_armed_droid", "monster_dark_robe_curse_mage_noah" },
                new[] { "monster_geoatlas", "monster_celestia", "monster_astravarn", "monster_ordion", "monster_yggdrasia" },
                new[] { "monster_geoatlas", "monster_celestia", "monster_astravarn", "monster_regnard", "monster_noxveil" }
            };
            int[] stages = { 1, 5, 10 }, levels = { 1, 40, 100 };
            var times = new float[2, 3]; var hpFractions = new float[2, 3];
            foreach (string mode in new[] { "avalanche", "champion" })
                for (int tier = 0; tier < 3; tier++)
                {
                    UnityEngine.Random.InitState(823 + tier);
                    var save = S("Save.PlayerSaveData", "CreateDefault");
                    profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
                    T("Managers.GameManager").GetProperty("PlayerProfile").SetValue(game, profile);
                    var slots = (IList)P(profile, "PartyMonsterInstanceIds"); slots.Clear();
                    foreach (string id in parties[tier == 2 && mode == "champion" ? 3 : tier])
                    {
                        Assert.That(Call(master, "GetMonsterData", id), Is.Not.Null, id);
                        var owned = Call(profile, "AddOwnedMonster", id, levels[tier], 0, false);
                        foreach (string stat in new[] { "IndividualHp", "IndividualAttack", "IndividualWisdom", "IndividualDefense", "IndividualMagicDefense", "IndividualAttackSpeed" }) Set(owned, stat, 100);
                        Set(owned, "HasIndividualValues", true);
                        slots.Add(F(owned, "InstanceId"));
                    }
                    Run(mode, stages[tier] - 1);
                    var sim = Simulator(); Call(sim, "Setup", stages[tier]);
                    string result = "None"; float seconds = 0f; int peak = 0;
                    while (result == "None" && seconds < 600f)
                    {
                        result = Call(sim, "Tick", .05f).ToString(); seconds += .05f;
                        peak = Math.Max(peak, (int)P(sim, "CurrentActiveEnemyCount"));
                    }
                    var allies = PrivateList(sim, "activeAllyRuntimes").Cast<object>().ToArray();
                    int currentHp = allies.Sum(a => (int)F(F(a, "Stats"), "CurrentHp"));
                    int maxHp = allies.Sum(a => (int)F(F(a, "Stats"), "MaxHp"));
                    TestContext.WriteLine($"DAILY BALANCE mode={mode} stage={stages[tier]} class={(tier == 2 ? 5 : tier + 1)} level={levels[tier]} result={result} seconds={seconds:0.0} remainingHP={currentHp}/{maxHp} alive={P(sim, "CurrentAliveAllyCount")} peak={peak} defeated={(int)P(sim, "CurrentEnemyCountTarget") - (int)P(sim, "CurrentRemainingEnemyCount")} / {P(sim, "CurrentEnemyCountTarget")} (individual100,plus0,no gear/guardian/manual skills)");
                    Assert.That(result, Is.EqualTo("Win"), mode + " stage " + stages[tier]);
                    Assert.That(peak, Is.LessThanOrEqualTo(mode == "avalanche" ? 30 : 1));
                    int modeIndex = mode == "avalanche" ? 0 : 1; times[modeIndex, tier] = seconds; hpFractions[modeIndex, tier] = (float)currentHp / maxHp;
                    S("Data.DailyChallengeSession", "End", true);
                    UnityEngine.Object.DestroyImmediate((UnityEngine.Object)sim);
                }
            for (int tier = 0; tier < 3; tier++)
            {
                Assert.That(Math.Max(times[0, tier], times[1, tier]) / Math.Min(times[0, tier], times[1, tier]), Is.LessThan(1.4f), "Comparable reference-party time for both mode choices.");
                Assert.That(Math.Abs(hpFractions[0, tier] - hpFractions[1, tier]), Is.LessThan(.20f), "A mode should not be substantially more punishing at equal target tier.");
            }
        }

        [Test]
        public void ContinuousRunsMeasureHpAndCooldownCarryThroughFirstFiveAndAllTenStages()
        {
            foreach (string mode in new[] { "avalanche", "champion" })
                foreach (int target in new[] { 5, 10 })
                {
                    UnityEngine.Random.InitState(196 + target);
                    profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { S("Save.PlayerSaveData", "CreateDefault") });
                    T("Managers.GameManager").GetProperty("PlayerProfile").SetValue(game, profile);
                    var slots = (IList)P(profile, "PartyMonsterInstanceIds"); slots.Clear();
                    string[] ids = target == 5 ?
                        new[] { "monster_ore_giant_garm", "monster_holy_armor_leon", "monster_flare_drake", "monster_armed_droid", "monster_dark_robe_curse_mage_noah" } :
                        mode == "avalanche" ? new[] { "monster_geoatlas", "monster_celestia", "monster_astravarn", "monster_ordion", "monster_yggdrasia" } :
                        new[] { "monster_geoatlas", "monster_celestia", "monster_astravarn", "monster_regnard", "monster_noxveil" };
                    foreach (string id in ids)
                    {
                        var owned = Call(profile, "AddOwnedMonster", id, target == 5 ? 40 : 100, 0, false);
                        foreach (string stat in new[] { "IndividualHp", "IndividualAttack", "IndividualWisdom", "IndividualDefense", "IndividualMagicDefense", "IndividualAttackSpeed" }) Set(owned, stat, 100);
                        Set(owned, "HasIndividualValues", true); slots.Add(F(owned, "InstanceId"));
                    }
                    Run(mode); var sim = Simulator(); int cleared = 0; float totalTime = 0;
                    for (int stage = 1; stage <= target; stage++)
                    {
                        Call(sim, "Setup", stage); string result = "None"; float seconds = 0;
                        while (result == "None" && seconds < 600f) { result = Call(sim, "Tick", .05f).ToString(); seconds += .05f; }
                        totalTime += seconds;
                        var allies = PrivateList(sim, "activeAllyRuntimes").Cast<object>().ToArray();
                        int hp = allies.Sum(a => (int)F(F(a, "Stats"), "CurrentHp")), max = allies.Sum(a => (int)F(F(a, "Stats"), "MaxHp"));
                        TestContext.WriteLine($"DAILY CONTINUOUS mode={mode} target={target} stage={stage} result={result} seconds={seconds:0.0} totalSeconds={totalTime:0.0} HP={hp}/{max} alive={P(sim, "CurrentAliveAllyCount")} (individual100,plus0,no gear/guardian/manual skills)");
                        if (result != "Win") break;
                        cleared = stage;
                        var checkpoint = Call(sim, "CaptureDailyChallengeCheckpoint", true);
                        var acknowledged = S("Data.DailyChallengeSession", "Clone", CurrentRun);
                        Set(acknowledged, "ClearedStage", stage); Set(acknowledged, "Checkpoint", checkpoint);
                        S("Data.DailyChallengeSession", "Accept", acknowledged);
                    }
                    TestContext.WriteLine($"DAILY CONTINUOUS RESULT mode={mode} target={target} cleared={cleared} totalSeconds={totalTime:0.0}");
                    Assert.That(cleared, Is.GreaterThanOrEqualTo(5), "A developed party must reach the stage-five drop/core checkpoint with HP carried.");
                    S("Data.DailyChallengeSession", "End", true); UnityEngine.Object.DestroyImmediate((UnityEngine.Object)sim);
                }
        }

        [Test]
        public void LocalCheckpointUsesConfiguredAccountDirectoryAndUnavailableStorageNeverReadsWritesOrDeletes()
        {
            var type = T("Managers.SaveManager"); var rootOverride = type.GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = rootOverride.GetValue(null); string temp = Path.Combine(Path.GetTempPath(), "DailyCheckpointStore-" + Guid.NewGuid().ToString("N"));
            var owner = new GameObject("DailyCheckpointStore"); owner.SetActive(false);
            try
            {
                Assert.That(T("Data.DailyChallengeSession").GetMethod("PathFor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "no-store" }), Is.Null);
                Directory.CreateDirectory(temp); rootOverride.SetValue(null, temp);
                var saves = owner.AddComponent(type); type.GetProperty("Instance").SetValue(null, saves);
                var run = Run("champion"); string data = (string)P(saves, "DataDirectory");
                string path = (string)T("Data.DailyChallengeSession").GetMethod("PathFor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { F(run, "RunId") });
                Assert.That(path, Does.StartWith(Path.Combine(data, "daily-challenge-runs")));
                Assert.That(File.Exists(path), Is.True);
                byte[] before = File.ReadAllBytes(path);
                rootOverride.SetValue(null, temp + "-other-context");
                Assert.That(P(saves, "StorageAccessAvailable"), Is.False);
                var checkpoint = Activator.CreateInstance(T("Data.DailyChallengeCheckpoint")); Set(checkpoint, "DefeatedEnemies", 2);
                S("Data.DailyChallengeSession", "SetCheckpoint", checkpoint);
                var incoming = Activator.CreateInstance(T("Data.DailyChallengeCheckpoint")); Set(incoming, "DefeatedEnemies", 7);
                Set(run, "Checkpoint", incoming);
                S("Data.DailyChallengeSession", "Begin", run);
                Assert.That(F(F(CurrentRun, "Checkpoint"), "DefeatedEnemies"), Is.EqualTo(7), "Unavailable storage must retain the incoming server checkpoint rather than read a cached checkpoint.");
                S("Data.DailyChallengeSession", "End", true);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "Unavailable storage cannot write or delete the old account file.");
                Assert.That(Directory.Exists(temp + "-other-context"), Is.False);
            }
            finally
            {
                type.GetProperty("Instance").SetValue(null, null); rootOverride.SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(owner); if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangingHomeFormationCannotReleaseAnActiveDailyParticipantEvenWithForce(bool force)
        {
            var run = Run("champion"); Set(P(profile, "DailyChallenges"), "ActiveRun", run);
            string member = ((string[])F(run, "PartyInstanceIds"))[0];
            ((IList)P(profile, "PartyMonsterInstanceIds")).Clear();
            var release = profile.GetType().GetMethods().Single(m => m.Name == "TryReleaseMonster" && m.GetParameters().Length == 3);
            object[] args = { member, force, null };
            Assert.That(release.Invoke(profile, args), Is.False); Assert.That((string)args[2], Does.Contain("デイリー試練"));
            Assert.That(Call(profile, "GetOwnedMonster", member), Is.Not.Null);
            var restored = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { Call(profile, "ToSaveData", 38) });
            Assert.That(Call(restored, "IsDailyChallengePartyMonster", member), Is.True, "The lock survives returning home and loading the saved profile.");
        }

        [Test]
        public void EmptyEntryCheckpointDoesNotReplaceTheEquippedGuardianWithAnEmptySerializedObject()
        {
            var guardian = Activator.CreateInstance(T("Save.OwnedGuardianData")); Set(guardian, "Id", "seiryu"); Set(guardian, "Level", 10);
            ((IList)P(profile, "OwnedGuardians")).Add(guardian);
            profile.GetType().GetProperty("EquippedGuardianId").SetValue(profile, "seiryu");
            var run = Run("champion");
            var empty = Activator.CreateInstance(T("Data.DailyChallengeCheckpoint"));
            Set(empty, "Guardian", Activator.CreateInstance(T("Data.DailyChallengeGuardianSnapshot"))); Set(run, "Checkpoint", empty);
            S("Data.DailyChallengeSession", "Begin", run);
            var sim = Simulator(); Call(sim, "Setup", 1);
            Assert.That(P(sim, "GuardianId"), Is.EqualTo("seiryu"));
            Assert.That(P(sim, "GuardianMonsterData"), Is.Not.Null);
            Assert.That(F(CurrentRun, "Checkpoint"), Is.Not.Null);
            Assert.That(((Array)F(F(CurrentRun, "Checkpoint"), "Parties")).Length, Is.EqualTo(2), "Stage-zero capture freezes the party immediately, before the first autosave.");
            Assert.That(F(F(F(CurrentRun, "Checkpoint"), "Guardian"), "Id"), Is.EqualTo("seiryu"));
        }
    }
}
