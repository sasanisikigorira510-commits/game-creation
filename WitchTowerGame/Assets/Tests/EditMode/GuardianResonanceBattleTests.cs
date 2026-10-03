using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class GuardianResonanceBattleTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Members).Invoke(target, args);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
        private static object P(object target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
        private static object F(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        private static void F(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private GameObject owner;
        private object game, master, simulator, profile, oldGame, oldMaster, oldSave;
        private SceneSetup[] scenes;
        private readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        private IList Allies => (IList)F(simulator, "activeAllyRuntimes");
        private IList Enemies => (IList)F(simulator, "activeEnemyRuntimes");
        private object Guardian => Allies.Cast<object>().Single(a => (int)F(a, "SlotIndex") == 5);
        private static object Stats(object runtime) => F(runtime, "Stats");
        private int Hp(object runtime) => (int)F(Stats(runtime), "CurrentHp");
        private float Gauge => (float)P(simulator, "GuardianResonance");
        private void Tick(float dt) => Call(simulator, "TickGuardianSkill", dt);

        [SetUp] public void Setup()
        {
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            owner = new GameObject("GuardianResonanceTest"); owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            S("Data.GuardianTrialSession", "Reset");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            F(save, "HighestFloor", 60); F(save, "HasCompletedTutorial", true); F(save, "TutorialStepId", "Complete");
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            Set(game, "PlayerProfile", profile);
            foreach (string id in new[] { "monster_apprentice_swordsman", "monster_rock_golem", "monster_apprentice_mage" })
            {
                object monster = Call(profile, "AddOwnedMonster", id, 20, 0, false);
                ((IList)P(profile, "PartyMonsterInstanceIds")).Add(F(monster, "InstanceId"));
            }
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
        }

        [TearDown] public void Cleanup()
        {
            S("Data.GuardianTrialSession", "Reset");
            foreach (var asset in temporary) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            temporary.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSave);
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }

        private void Build(string id, string contract = "basic")
        {
            S("Data.GuardianService", "GrantCore", profile, id);
            S("Data.GuardianService", "Birth", profile, id);
            F(S("Data.GuardianService", "Owned", profile, id), "Level", 10);
            S("Data.GuardianService", "Equip", profile, id);
            Assert.That(S("Data.GuardianService", "SetContract", profile, id, contract), Is.True);
            Call(simulator, "Setup", 31);
            while (Enemies.Count < 3) Call(simulator, "QueueEnemySpawn", false);
            foreach (var enemy in Enemies)
            {
                F(Stats(enemy), "MaxHp", 1000000); F(Stats(enemy), "CurrentHp", 1000000);
                F(Stats(enemy), "Defense", 0); F(Stats(enemy), "MagicDefense", 0);
                F(enemy, "PositionAnchor", (Vector2)F(Allies[0], "PositionAnchor"));
            }
            foreach (var ally in Allies)
            {
                F(Stats(ally), "CritRate", -1f);
                F(Stats(ally), "Attack", 100); F(Stats(ally), "Wisdom", 100);
            }
        }

        private object MakeAttack(int slot = 0, bool magic = false, int targets = 1, bool critical = false)
        {
            var ally = Allies.Cast<object>().Single(a => (int)F(a, "SlotIndex") == slot);
            var data = UnityEngine.Object.Instantiate((UnityEngine.Object)F(ally, "Data")); temporary.Add(data);
            F(data, "normalAttackTargetCount", targets);
            F(data, "damageType", Enum.Parse(T("MasterData.MonsterDamageType"), magic ? "Magic" : "Physical"));
            F(ally, "Data", data);
            F(Stats(ally), "CritRate", critical ? 1f : -1f); F(Stats(ally), "CritDamage", 1f);
            foreach (var enemy in Enemies) F(enemy, "PositionAnchor", F(ally, "PositionAnchor"));
            return ally;
        }
        private void Attack(object ally, bool skill = false) => Call(simulator, "PerformAttackOnEnemy", ally, skill, (int)F(ally, "SlotIndex"));

        [Test] public void EachGuardianUsesItsOwnLevelAndDoesNotInheritAnotherGuardiansStats()
        {
            Build("seiryu");
            F(S("Data.GuardianService", "Owned", profile, "seiryu"), "Level", 30);
            Call(simulator, "Setup", 31);
            var seiryuStats = S("Data.GuardianService", "Stats", "seiryu", 30);
            Assert.That(Call(simulator, "GetAllyMaxHp", 5), Is.EqualTo(F(seiryuStats, "MaxHp")));
            Assert.That(F(Stats(Guardian), "Attack"), Is.EqualTo(F(seiryuStats, "Attack")));
            Assert.That(S("Data.GuardianService", "GrantCore", profile, "suzaku"), Is.True);
            Assert.That(S("Data.GuardianService", "Birth", profile, "suzaku"), Is.True);
            Assert.That(S("Data.GuardianService", "Level", profile, "suzaku"), Is.EqualTo(1));
            S("Data.GuardianService", "Equip", profile, "suzaku");
            Call(simulator, "Setup", 31);
            var suzakuStats = S("Data.GuardianService", "Stats", "suzaku", 1);
            Assert.That(Call(simulator, "GetAllyMaxHp", 5), Is.EqualTo(F(suzakuStats, "MaxHp")));
            Assert.That(F(Stats(Guardian), "Wisdom"), Is.EqualTo(F(suzakuStats, "Wisdom")));
            S("Data.GuardianService", "AddBattleExperience", profile, 2400);
            Assert.That(S("Data.GuardianService", "Level", profile, "suzaku"), Is.EqualTo(3));
            Assert.That(S("Data.GuardianService", "Level", profile, "seiryu"), Is.EqualTo(30));
            S("Data.GuardianService", "Equip", profile, "seiryu");
            Call(simulator, "Setup", 31);
            Assert.That(Call(simulator, "GetAllyMaxHp", 5), Is.EqualTo(F(seiryuStats, "MaxHp")));
        }

        [Test] public void GaugeStartsHalfFullAndPassiveFirstInvocationTakesFourSeconds()
        {
            Build("seiryu"); Assert.That(Gauge, Is.EqualTo(50f));
            Tick(3.99f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(0));
            Tick(.01f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1));
            Assert.That(Gauge, Is.EqualTo(0f).Within(.001f));
            Tick(7.99f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1));
            Tick(.01f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(2));
        }

        [TestCase("seiryu", false, false, 2f)]
        [TestCase("suzaku", true, false, 4f)]
        [TestCase("byakko", false, true, 8f)]
        public void AreaAttacksAddResonanceOncePerAction(string id, bool magic, bool crit, float gain)
        {
            Build(id); var ally = MakeAttack(0, magic, 3, crit);
            Attack(ally);
            Assert.That(Gauge, Is.EqualTo(50f + gain));
            Assert.That(Enemies.Cast<object>().Count(e => Hp(e) < 1000000), Is.EqualTo(3));
            for (int i = 0; i < 20; i++) Attack(ally);
            Assert.That(Gauge, Is.EqualTo(62.5f), "Action gain must be capped at 12.5 in a rolling second.");
            Tick(.99f); Attack(ally);
            Assert.That(Gauge, Is.EqualTo(74.875f).Within(.001f));
            Tick(.01f); Attack(ally);
            Assert.That(Gauge, Is.EqualTo(75f + gain).Within(.001f));
        }

        [TestCase("suzaku", false, false)]
        [TestCase("byakko", false, false)]
        public void NonQualifyingAttackDoesNotAddResonance(string id, bool magic, bool crit)
        {
            Build(id); Attack(MakeAttack(0, magic, 3, crit)); Assert.That(Gauge, Is.EqualTo(50f));
        }

        [Test] public void SkillsGuardianAttacksAndBurnCannotRecursivelyChargeGauge()
        {
            Build("suzaku"); Attack(MakeAttack(0, true, 3), true);
            Attack(MakeAttack(5, true, 3));
            Assert.That(Gauge, Is.EqualTo(50f));
            Tick(4f); Tick(1f);
            Assert.That(Gauge, Is.EqualTo(12.5f));
            Assert.That(P(simulator, "GuardianDamageDealt"), Is.EqualTo(300 + 600 + 75));
        }

        [Test] public void InitialTwoSecondAndSubsequentFourSecondMinimumsAreEnforced()
        {
            Build("seiryu"); F(simulator, "guardianResonance", 100f);
            Tick(1.99f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(0));
            Tick(.01f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1));
            F(simulator, "guardianResonance", 100f);
            Tick(3.99f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1));
            Tick(.01f); Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(2));
        }

        [Test] public void GaugePausesWithoutLivingEnemiesAndNeverRefillsOnReplacement()
        {
            Build("seiryu"); Tick(1f);
            foreach (var enemy in Enemies) F(Stats(enemy), "CurrentHp", 0);
            Tick(3f); Assert.That(Gauge, Is.EqualTo(62.5f));
            while (Enemies.Count > 0) Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            Call(simulator, "QueueEnemySpawn", false);
            Assert.That(Gauge, Is.EqualTo(62.5f));
        }

        [TestCase("basic", 1.15f)] [TestCase("alternate", 1f)]
        public void SeiryuOnlyBasicContractAcceleratesOrdinaryAllies(string contract, float rate)
        {
            Build("seiryu", contract); Tick(4f);
            Assert.That(Call(simulator, "GuardianAllyAttackRate", Allies[0]), Is.EqualTo(rate));
            Assert.That(Call(simulator, "GuardianAllyAttackRate", Guardian), Is.EqualTo(1f));
            F(Stats(Guardian), "CurrentHp", 0);
            Tick(3.1f);
            Assert.That(Call(simulator, "GuardianAllyAttackRate", Allies[0]), Is.EqualTo(1f));
            Assert.That(P(simulator, "BattleSpiritInvoked"), Is.True, "The base blessing survives a guardian defeat.");
        }

        [Test] public void SeiryuFollowupBuffsWholeNextActionAndThenExpiresForThatAlly()
        {
            Build("seiryu", "alternate"); var ally = MakeAttack(0, false, 3); Tick(4f);
            var before = Enemies.Cast<object>().Select(Hp).ToArray(); Attack(ally);
            for (int i = 0; i < 3; i++) Assert.That(before[i] - Hp(Enemies[i]), Is.EqualTo(125));
            int hp = Hp(Enemies[0]); Attack(ally); Assert.That(hp - Hp(Enemies[0]), Is.EqualTo(100));
            Assert.That(P(simulator, "GuardianSupportedAttackCount"), Is.EqualTo(1));
            Attack(MakeAttack(1)); Assert.That(P(simulator, "GuardianSupportedAttackCount"), Is.EqualTo(2));
        }

        [TestCase("basic", 115, 3)] [TestCase("alternate", 130, 1)]
        public void SuzakuContractControlsTargetsAndMagicSupport(string contract, int expected, int targetCount)
        {
            Build("suzaku", contract); var ally = MakeAttack(0, true); Tick(4f);
            Assert.That(Enemies.Cast<object>().Count(e => Hp(e) < 1000000), Is.EqualTo(targetCount));
            // The primary target is also the highest maximum HP target in this fixture.
            int hp = Hp(Enemies[0]); Attack(ally); Assert.That(hp - Hp(Enemies[0]), Is.EqualTo(expected));
            F(Stats(Enemies[0]), "CurrentHp", 1); Attack(ally);
            Assert.That(P(simulator, "GuardianBurnAssistKillCount"), Is.EqualTo(1));
        }

        [Test] public void BurnContinuesAfterGuardianDefeatThenEndsExactlyOnce()
        {
            Build("suzaku"); Tick(4f); int before = Hp(Enemies[0]);
            F(Stats(Guardian), "CurrentHp", 0); Tick(4f);
            Assert.That(before - Hp(Enemies[0]), Is.EqualTo(100), "Four stored 25 damage ticks must survive the caster.");
            Assert.That(P(simulator, "GuardianSupportRemaining"), Is.EqualTo(0f));
            int ended = Hp(Enemies[0]); Tick(20f); Attack(MakeAttack(0, true));
            Assert.That(ended - Hp(Enemies[0]), Is.EqualTo(100));
            Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1)); Assert.That(Gauge, Is.EqualTo(0f));
        }

        [TestCase("basic")] [TestCase("alternate")]
        public void ByakkoSupportUsesCorrectConditionAndCountsOneAction(string contract)
        {
            Build("byakko", contract); var ally = MakeAttack(0, false, 3, true); Tick(4f);
            if (contract == "alternate") foreach (var enemy in Enemies) F(Stats(enemy), "CurrentHp", 300000);
            int before = Hp(Enemies[0]); Attack(ally);
            Assert.That(before - Hp(Enemies[0]), Is.EqualTo(120));
            Assert.That(P(simulator, "GuardianSupportedAttackCount"), Is.EqualTo(1));
            Tick(4.01f); before = Hp(Enemies[0]); Attack(ally);
            Assert.That(before - Hp(Enemies[0]), Is.EqualTo(100));
        }

        [TestCase("basic", .18f, 100)] [TestCase("alternate", .12f, 120)]
        public void GenbuBarrierAndCounterattackUseActualRemainingShield(string contract, float fraction, int expectedDamage)
        {
            Build("genbu", contract); var ally = MakeAttack(); Tick(4f);
            int shield = Mathf.RoundToInt((int)F(Stats(Guardian), "MaxHp") * fraction);
            int before = Hp(Enemies[0]); Attack(ally); Assert.That(before - Hp(Enemies[0]), Is.EqualTo(expectedDamage));
            Assert.That(Call(simulator, "AbsorbGuardianBarrier", 0, shield + 10), Is.EqualTo(10));
            Assert.That(P(simulator, "GuardianDamageBlocked"), Is.EqualTo(shield));
            before = Hp(Enemies[0]); Attack(ally); Assert.That(before - Hp(Enemies[0]), Is.EqualTo(100));
        }

        [Test] public void FullBarrierAbsorptionStillChargesGenbuAndDoesNotCountExcessShield()
        {
            Build("genbu"); Tick(4f);
            var target = Allies[0]; var enemy = Enemies[0];
            F(enemy, "TargetAllyRuntimeId", F(target, "RuntimeId"));
            F(Stats(enemy), "Attack", 5); F(Stats(enemy), "Wisdom", 5); F(Stats(enemy), "CritRate", -1f);
            Call(simulator, "ClearEnemyMovementQueueCache");
            int hp = Hp(target); Call(simulator, "PerformAttackOnPlayer", enemy, 0);
            Assert.That(Hp(target), Is.EqualTo(hp));
            Assert.That(Gauge, Is.EqualTo(3f));
            Assert.That((int)P(simulator, "GuardianDamageBlocked"), Is.InRange(1, 5));
            F(Stats(Guardian), "CurrentHp", 0); Tick(1f);
            Assert.That(Call(simulator, "AbsorbGuardianBarrier", 0, 10), Is.EqualTo(0));
            Tick(5.1f); Assert.That(Call(simulator, "AbsorbGuardianBarrier", 0, 10), Is.EqualTo(10));
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void PublicEffectQueriesFollowLivingRecipientsStoredEffectsAndExpiry(string id)
        {
            Build(id); Tick(4f);
            string query = id == "seiryu" ? "GuardianSupportsAlly" : id == "genbu" ? "GuardianHasBarrier" :
                id == "suzaku" ? "GuardianIsBurning" : "GuardianHasMark";
            Assert.That(P(simulator, "GuardianId"), Is.EqualTo(id));
            Assert.That(Call(simulator, query, 0), Is.True);
            Assert.That(Call(simulator, query, -1), Is.False);
            Assert.That(Call(simulator, query, 999), Is.False);
            F(Stats(Guardian), "CurrentHp", 0);
            Assert.That(Call(simulator, query, 0), Is.True, "Already cast effects remain on surviving recipients.");
            object recipient = id == "seiryu" || id == "genbu" ? Allies[0] : Enemies[0];
            F(Stats(recipient), "CurrentHp", 0);
            Assert.That(Call(simulator, query, 0), Is.False);
            F(Stats(recipient), "CurrentHp", 10000);
            Tick(6.1f);
            Assert.That(Call(simulator, query, 0), Is.False, "Expired effects must never leave a visual mark.");
        }

        [Test] public void PublicAllySupportQueriesStopAfterFollowupOrShieldConsumption()
        {
            Build("seiryu", "alternate"); Tick(4f);
            Assert.That(Call(simulator, "GuardianSupportsAlly", 0), Is.True);
            Assert.That(Call(simulator, "GuardianSupportsAlly", 5), Is.False);
            Attack(MakeAttack());
            Assert.That(Call(simulator, "GuardianSupportsAlly", 0), Is.False);
            Assert.That(Call(simulator, "GuardianSupportsAlly", 1), Is.True);
        }

        [TestCase("seiryu", "basic")] [TestCase("suzaku", "basic")]
        [TestCase("suzaku", "alternate")] [TestCase("byakko", "basic")] [TestCase("genbu", "basic")]
        public void DivinePresentationKeepsOriginalTargetAfterLethalDamage(string id, string contract)
        {
            Build(id, contract);
            F(Guardian, "TargetEnemyRuntimeId", F(Enemies[0], "RuntimeId"));
            F(Stats(Enemies[1]), "MaxHp", 2000000);
            for (int i = 0; i < Enemies.Count; i++)
            {
                F(Stats(Enemies[i]), "CurrentHp", 1);
                F(Enemies[i], "PositionAnchor", new Vector2(.61f + i * .10f, .3f + i * .08f));
            }
            object expected = id == "genbu" ? Guardian : id == "byakko" || contract == "alternate" ? Enemies[1] : Enemies[0];
            var anchor = (Vector2)F(expected, "PositionAnchor");
            int runtimeId = (int)F(expected, "RuntimeId");
            Tick(4f);
            Assert.That(P(simulator, "LastGuardianSkillTargetAnchor"), Is.EqualTo(anchor));
            Assert.That(P(simulator, "LastGuardianSkillTargetRuntimeId"), Is.EqualTo(runtimeId));
            if (id != "genbu") Assert.That(Hp(expected), Is.EqualTo(0));
        }

        [Test] public void GuardianDamageCounterIncludesNormalAttacksWithoutOverkill()
        {
            Build("seiryu"); var guardian = MakeAttack(5); F(Stats(Enemies[0]), "CurrentHp", 7);
            Attack(guardian); Assert.That(P(simulator, "GuardianDamageDealt"), Is.EqualTo(7));
            Assert.That(Gauge, Is.EqualTo(50f));
        }

        [TestCase(false)] [TestCase(true)]
        public void PracticeUsesUnownedSelectedGuardianAndNeverChangesSavedProgress(bool elite)
        {
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 61));
            Assert.That(S("Data.GuardianTrialSession", "BeginPractice", profile, "suzaku", elite), Is.True);
            Call(simulator, "Setup", 60);
            Assert.That(Call(simulator, "GetAllyMonsterId", 5), Is.EqualTo("guardian_suzaku"));
            string result = "None";
            for (int i = 0; i < 400 && result == "None"; i++) result = Call(simulator, "Tick", .05f).ToString();
            Assert.That(result, Is.Not.EqualTo("None"));
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 61)), Is.EqualTo(before));
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void AcquisitionTrialsExposeDistinctMechanicsAndStillNeedRealVictory(string id)
        {
            Assert.That(S("Data.GuardianTrialSession", "Begin", profile, id), Is.True);
            Call(simulator, "Setup", 30); Call(simulator, "TickPreparation", 1f);
            while (Enemies.Count < (id == "suzaku" ? 3 : 1)) Call(simulator, "QueueEnemySpawn", false);
            Call(simulator, "TickGuardianTrial", .1f);
            var primary = Enemies[0];
            if (id == "seiryu")
            {
                Assert.That(Call(simulator, "GuardianTrialEnemyAttackRate", primary), Is.EqualTo(1.5f));
                Call(simulator, "TickGuardianTrial", 3f);
                Assert.That(Call(simulator, "GuardianTrialEnemyAttackRate", primary), Is.EqualTo(.6f));
                Assert.That((string)P(simulator, "GuardianTrialCue"), Does.Contain("防御"));
            }
            else if (id == "suzaku")
            {
                Assert.That(Call(simulator, "GuardianTrialEnemyDamageMultiplier", primary), Is.EqualTo(1.15f));
                F(Stats(Enemies[1]), "CurrentHp", 0); F(Stats(Enemies[2]), "CurrentHp", 0);
                Assert.That(Call(simulator, "GuardianTrialEnemyDamageMultiplier", primary), Is.EqualTo(.65f));
            }
            else if (id == "byakko")
            {
                Call(simulator, "TickGuardianTrial", 4f);
                Assert.That(Call(simulator, "GuardianTrialEnemyIsWindingUp", primary), Is.True);
                Call(simulator, "TickGuardianTrial", 1f);
                Assert.That(Call(simulator, "GuardianTrialEnemyDamageMultiplier", primary), Is.EqualTo(1.8f));
                Assert.That(Call(simulator, "GuardianTrialEnemyDamageMultiplier", primary), Is.EqualTo(1f));
            }
            else
            {
                int hp = (int)F(Stats(primary), "MaxHp"); int shield = Mathf.RoundToInt(hp * .12f);
                Assert.That(Call(simulator, "AbsorbGuardianTrialBarrier", primary, shield + 9), Is.EqualTo(9));
                Assert.That(Call(simulator, "AbsorbGuardianTrialBarrier", primary, 50), Is.EqualTo(50));
                Call(simulator, "TickGuardianTrial", 8f);
                Assert.That(Call(simulator, "AbsorbGuardianTrialBarrier", primary, 10), Is.EqualTo(0));
                Call(simulator, "TickGuardianTrial", 3f);
                Assert.That(Call(simulator, "AbsorbGuardianTrialBarrier", primary, 10), Is.EqualTo(10));
            }
            Assert.That((IList)P(profile, "GuardianCoreIds"), Is.Empty);
        }
    }
}
