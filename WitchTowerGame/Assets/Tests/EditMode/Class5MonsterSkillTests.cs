using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class Class5MonsterSkillTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private GameObject owner;
        private object simulator;
        private readonly List<UnityEngine.Object> fixtures = new List<UnityEngine.Object>();
        private readonly List<object> hits = new List<object>();
        private readonly List<string> casts = new List<string>();
        private UnityEngine.Random.State randomState;
        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object item, string name) => item.GetType().GetField(name, Any).GetValue(item);
        private static void Set(object item, string name, object value) => item.GetType().GetField(name, Any).SetValue(item, value);
        private static object Property(object item, string name) => item.GetType().GetProperty(name, Any).GetValue(item);
        private static object Call(object item, string name, params object[] args) => item.GetType().GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(item, args);
        private static object StaticCall(string type, string name, params object[] args) => GameType(type).GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(null, args);
        private IList Allies => (IList)Field(simulator, "activeAllyRuntimes");
        private IList Enemies => (IList)Field(simulator, "activeEnemyRuntimes");

        [SetUp]
        public void SetUp()
        {
            randomState = UnityEngine.Random.state;
            owner = new GameObject("Class5SkillTestSimulator");
            owner.SetActive(false);
            simulator = owner.AddComponent(GameType("Battle.BattleSimulator"));
            GameType("Battle.BattleSimulator").GetEvent("Class5SkillInvoked").AddEventHandler(simulator,
                new Action<int, string>((slot, id) => casts.Add(slot + ":" + id)));
            var hitEvent = GameType("Battle.BattleSimulator").GetEvent("HitResolved");
            var method = GetType().GetMethod(nameof(RecordHit), Any).MakeGenericMethod(GameType("Battle.BattleHitInfo"));
            hitEvent.AddEventHandler(simulator, Delegate.CreateDelegate(hitEvent.EventHandlerType, this, method));
        }

        private void RecordHit<T>(T info) => hits.Add(info);

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            foreach (var fixture in fixtures) UnityEngine.Object.DestroyImmediate(fixture);
            fixtures.Clear(); hits.Clear(); casts.Clear();
            UnityEngine.Random.state = randomState;
        }

        [TestCase("astravarn", 12f, 1, 6.5f, "Physical")]
        [TestCase("ordion", 10f, 6, 2.5f, "Physical")]
        [TestCase("geoatlas", 14f, 5, 1.2f, "Physical")]
        [TestCase("regnard", 8f, 1, 1.5f, "Physical")]
        [TestCase("noxveil", 12f, 8, 2.3f, "Magic")]
        [TestCase("celestia", 12f, 1, 7f, "Magic")]
        [TestCase("yggdrasia", 10f, 6, 2f, "Magic")]
        public void ApprovedSevenSkillsStartWithFullCooldownAndDoNotGrowTheirTargetCountOrCooldown(
            string key, float cooldown, int maxTargets, float power, string damageType)
        {
            object ally = AddAlly(key, 5);
            Initialize();
            object definition = StaticCall("Data.Class5SkillCatalog", "Resolve", "monster_" + key);
            Assert.That(Property(definition, "SkillId"), Is.EqualTo("class5_skill_" + key));
            Assert.That(Property(definition, "MaxTargets"), Is.EqualTo(maxTargets));
            Assert.That(Property(definition, "PowerPerHit"), Is.EqualTo(power));
            Assert.That(Property(definition, "DamageType").ToString(), Is.EqualTo(damageType));
            Assert.That(Cooldown(ally), Is.EqualTo(cooldown));
            Tick(1f);
            Assert.That(Cooldown(ally), Is.EqualTo(cooldown - 1f));
            Assert.That(Field(ally, "AttackTimer"), Is.EqualTo(3.25f));
            Assert.That(casts, Is.Empty);
        }

        [TestCase("astravarn", 1, 325)]
        [TestCase("astravarn", 5, 455)]
        [TestCase("celestia", 1, 175)]
        [TestCase("celestia", 5, 245)]
        public void SkillTrainingScalesActualDamageUsingCorrespondingDefenseWithoutCriticalHits(string key, int level, int expected)
        {
            object ally = AddAlly(key, level);
            object enemy = AddEnemy(1, .10f);
            Set(Field(enemy, "Stats"), "Defense", 100);
            Set(Field(enemy, "Stats"), "MagicDefense", 300);
            Set(Field(ally, "Stats"), "CritRate", 1f);
            Set(Field(ally, "Stats"), "CritDamage", 100f);
            Initialize(); Ready(ally); Tick(.01f);
            Assert.That(hits, Is.Empty, "The first frame is the windup; damage waits for the release frame.");
            Assert.That(Cooldown(ally), Is.EqualTo(12f));
            Tick(.2f);
            Assert.That(Hp(enemy), Is.EqualTo(1000000 - expected));
            Assert.That(Cooldown(ally), Is.EqualTo(11.8f).Within(.0001f));
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(Property(hits[0], "IsCritical"), Is.False);
            Assert.That(Property(hits[0], "IsMonsterSkill"), Is.True);
            Assert.That(Property(hits[0], "MonsterSkillId"), Is.EqualTo("class5_skill_" + key));
            Assert.That(Property(hits[0], "AttackerRuntimeId"), Is.EqualTo(Field(ally, "RuntimeId")));
            Assert.That(Property(hits[0], "TargetRuntimeId"), Is.EqualTo(1));
        }

        [Test]
        public void ReadySkillWaitsForLivingEnemyInRangeAndNormalMotionWithoutSpendingOrResettingTimers()
        {
            object ally = AddAlly("astravarn"); Initialize();
            Tick(20f);
            Assert.That(Cooldown(ally), Is.Zero);
            Assert.That(casts, Is.Empty);
            object enemy = AddEnemy(1, .9f);
            Set(ally, "AttackReachAnchor", .01f); Set(ally, "SearchReachAnchor", 2f);
            Tick(1f);
            Assert.That(casts, Is.Empty, "Searchable enemies outside attack reach must not spend a ready skill.");
            Set(enemy, "PositionAnchor", new Vector2(.10f, .5f));
            Set(ally, "AttackMotionLockRemaining", .2f);
            Tick(.1f);
            Assert.That(casts, Is.Empty);
            Set(ally, "AttackMotionLockRemaining", 0f);
            Tick(.01f);
            Assert.That(casts.Count, Is.EqualTo(1));
            Assert.That(Field(ally, "AttackTimer"), Is.EqualTo(3.25f));
            Assert.That(Field(enemy, "AttackTimer"), Is.EqualTo(2.5f));
            Assert.That(Field(ally, "AttackMotionLockRemaining"), Is.Zero);
            Assert.That(Call(simulator, "GetClass5SkillMotionRemaining", 0), Is.EqualTo(.8f));
        }

        [Test]
        public void RepeatedFastNormalSwingsCannotStarveAReadySkill()
        {
            object ally = AddAlly("astravarn"); AddEnemy(1, .1f); Initialize(); Ready(ally);
            for (int i = 0; i < 6; i++)
            {
                Set(ally, "AttackMotionLockRemaining", .34f);
                Tick(.1f);
            }
            Assert.That(casts.Count, Is.EqualTo(1));
            Assert.That(Field(ally, "AttackTimer"), Is.EqualTo(3.25f));
            Assert.That(Cooldown(ally), Is.GreaterThan(11f));
        }

        [Test]
        public void AreaSkillHitsUniqueNearbyTargetsUpToItsCapWithoutTransferringUnusedDamageToOneEnemy()
        {
            object ally = AddAlly("ordion");
            var nearby = Enumerable.Range(1, 7).Select(id => AddEnemy(id, .1f + id * .01f)).ToArray();
            object outside = AddEnemy(20, .9f);
            Initialize(); Ready(ally); Tick(.01f);
            Tick(.2f);
            Assert.That(nearby.Count(enemy => Hp(enemy) == 999750), Is.EqualTo(6));
            Assert.That(nearby.Count(enemy => Hp(enemy) == 1000000), Is.EqualTo(1));
            Assert.That(Hp(outside), Is.EqualTo(1000000));
            var targetHits = ((IEnumerable)Property(hits.Single(), "TargetHits")).Cast<object>().ToArray();
            Assert.That(targetHits.Select(hit => (int)Property(hit, "TargetRuntimeId")).Distinct().Count(), Is.EqualTo(6));
            Enemies.Clear(); AddEnemy(22, .1f);
            Tick(2f);
            Assert.That(casts.Count, Is.EqualTo(1), "Defeat/replacement must not reset the cooldown.");
        }

        [Test]
        public void TripleStrikeKeepsOneCapturedTargetAndCancelsRemainingHitsAfterThatTargetDies()
        {
            object ally = AddAlly("regnard");
            object primary = AddEnemy(1, .1f); object other = AddEnemy(2, .15f);
            Set(Field(primary, "Stats"), "CurrentHp", 250);
            Initialize(); Ready(ally); Tick(.01f);
            Assert.That(Hp(primary), Is.EqualTo(250));
            Tick(.2f);
            Assert.That(Hp(primary), Is.EqualTo(100));
            Tick(.15f);
            Assert.That(Hp(primary), Is.Zero);
            Tick(.15f);
            Assert.That(Hp(other), Is.EqualTo(1000000));
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(casts.Count, Is.EqualTo(1));
            Assert.That(Cooldown(ally), Is.EqualTo(7.5f).Within(.0001f));
        }

        [Test]
        public void TripleStrikeResumesItsPendingHitsExactlyOnceFromCheckpoint()
        {
            object ally = AddAlly("regnard"); object enemy = AddEnemy(1, .1f);
            Initialize(); Ready(ally); Tick(.01f);
            Tick(.2f);
            object checkpoint = Checkpoint(); Call(simulator, "CaptureClass5SkillState", checkpoint);
            string wire = JsonUtility.ToJson(checkpoint);
            object restored = JsonUtility.FromJson(wire, checkpoint.GetType());
            Initialize(); Call(simulator, "RestoreClass5SkillState", restored);
            Tick(.15f); Tick(.15f); Tick(.15f);
            Assert.That(Hp(enemy), Is.EqualTo(1000000 - 450));
            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(casts.Count, Is.EqualTo(1));
        }

        [Test]
        public void CrystalWallReducesOnlyItsCasterAfterDefenseAndExpiresAfterSixSeconds()
        {
            object golem = AddAlly("geoatlas"); object other = AddAlly("celestia", slot: 1);
            object front = AddEnemy(1, .1f); object behind = AddEnemy(2, -.1f);
            Initialize(); Ready(golem); Tick(.01f);
            Tick(.2f);
            Assert.That(Hp(front), Is.EqualTo(999880));
            Assert.That(Hp(behind), Is.EqualTo(1000000));
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", golem, 100), Is.EqualTo(65));
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", other, 100), Is.EqualTo(100));
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", golem, 1), Is.EqualTo(1));
            Tick(6f);
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", golem, 100), Is.EqualTo(100));
        }

        [Test]
        public void BindingSlowsOnlyMovementAndUsesStrongestReductionWithRefreshedDuration()
        {
            object ally = AddAlly("yggdrasia"); object enemy = AddEnemy(1, .1f);
            Initialize(); Ready(ally); Tick(.01f);
            Tick(.2f);
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(.7f).Within(.0001f));
            Assert.That(Field(enemy, "MoveSpeed"), Is.EqualTo(1f));
            Assert.That(Field(enemy, "AttackTimer"), Is.EqualTo(2.5f));
            Call(simulator, "ApplyClass5Slow", 1, .2f, 4f);
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(.7f).Within(.0001f));
            Tick(3f); Call(simulator, "ApplyClass5Slow", 1, .3f, 4f); Tick(3f);
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(.7f).Within(.0001f));
            Tick(1f);
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(1f));
        }

        [Test]
        public void StageTransitionKeepsCooldownAndSelfReductionButDiscardsOldEnemyEffectsAndPendingHits()
        {
            object golem = AddAlly("geoatlas"); object swordsman = AddAlly("regnard", slot: 1);
            object spirit = AddAlly("yggdrasia", slot: 2); object enemy = AddEnemy(1, .1f);
            Initialize(); Ready(golem); Ready(swordsman); Ready(spirit); Tick(.01f); Tick(.25f);
            object checkpoint = Checkpoint(); Call(simulator, "CaptureClass5SkillState", checkpoint); Set(checkpoint, "StageComplete", true);
            int before = Hp(enemy); float cooldown = Cooldown(swordsman);
            Initialize(); Call(simulator, "RestoreClass5SkillState", checkpoint);
            Assert.That(Cooldown(swordsman), Is.EqualTo(cooldown));
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", golem, 100), Is.EqualTo(65));
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(1f));
            Assert.That(Call(simulator, "GetClass5SkillMotionRemaining", 1), Is.Zero);
            Tick(.4f);
            Assert.That(Hp(enemy), Is.EqualTo(before), "An enemy reusing a runtime id on the next stage must not receive old triple hits.");
        }

        [Test]
        public void LowerClassAndUnknownMonstersDoNotGainClass5Skills()
        {
            object wrongClass = AddAlly("astravarn"); Set(Field(wrongClass, "Data"), "classRank", 4);
            AddAlly("unknown", slot: 1); AddEnemy(1, .1f); Initialize(); Tick(20f);
            Assert.That(casts, Is.Empty);
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void MalformedSkillSnapshotCannotInjectNonFiniteTimersAdditionalTargetsOrDuplicateHits()
        {
            object ally = AddAlly("regnard"); object enemy = AddEnemy(1, .1f);
            Initialize(); Ready(ally); Tick(.01f);
            Tick(.2f);
            object checkpoint = Checkpoint(); Call(simulator, "CaptureClass5SkillState", checkpoint);
            object state = ((Array)Field(checkpoint, "Class5Skills")).GetValue(0);
            Set(state, "CooldownRemaining", float.NaN); Set(state, "MotionRemaining", float.PositiveInfinity);
            Set(state, "CastOffense", float.NaN); Set(state, "CastPowerMultiplier", float.PositiveInfinity);
            Set(state, "TargetRuntimeIds", new[] { 1, 1, 99 }); Set(state, "NextHitIndex", int.MaxValue);
            Initialize(); Call(simulator, "RestoreClass5SkillState", checkpoint);
            Assert.That(float.IsNaN(Cooldown(ally)), Is.False);
            Assert.That(Call(simulator, "GetClass5SkillMotionRemaining", 0), Is.Zero);
            // Keep the ready cooldown from starting a new legitimate cast during this assertion.
            Set(ally, "AttackMotionLockRemaining", 1f); Tick(.4f);
            Assert.That(Hp(enemy), Is.EqualTo(999850));
        }

        [Test]
        public void PreparationAdvancesCooldownAndEffectDurationsWithoutStartingSkillsOrApplyingDamage()
        {
            object ally = AddAlly("geoatlas"); object enemy = AddEnemy(1, .1f); Initialize();
            Call(simulator, "TickClass5Preparation", 20f);
            Assert.That(Cooldown(ally), Is.Zero);
            Assert.That(casts, Is.Empty);
            Tick(.01f);
            Assert.That(casts.Count, Is.EqualTo(1));
            Call(simulator, "ApplyClass5Slow", 1, .3f, 4f);
            Call(simulator, "TickClass5Preparation", .2f);
            Assert.That(Hp(enemy), Is.EqualTo(1000000));
            Assert.That(Cooldown(ally), Is.EqualTo(13.8f).Within(.0001f));
            Assert.That(Call(simulator, "GetClass5SkillMotionRemaining", 0), Is.EqualTo(.6f).Within(.0001f));
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", ally, 100), Is.EqualTo(65));
            Call(simulator, "TickClass5Preparation", 6f);
            Assert.That(Call(simulator, "ApplyClass5DamageReduction", ally, 100), Is.EqualTo(100));
            Assert.That(Call(simulator, "ResolveClass5EnemyMoveSpeed", enemy), Is.EqualTo(1f));
            Assert.That(Hp(enemy), Is.EqualTo(1000000));
            Assert.That(casts.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExtremePowerAndDefenseStayWithinPositiveIntegerDamageBounds()
        {
            Assert.That(StaticCall("Data.Class5SkillCatalog", "CalculateDamage", float.MaxValue, 7f, 0, 1.4f), Is.EqualTo(int.MaxValue));
            Assert.That(StaticCall("Data.Class5SkillCatalog", "CalculateDamage", 1f, 1f, int.MaxValue, 1f), Is.EqualTo(1));
            Assert.That(StaticCall("Data.Class5SkillCatalog", "CalculateDamage", float.NaN, float.PositiveInfinity, -1, float.NaN), Is.EqualTo(1));
        }

        private void Initialize() => Call(simulator, "InitializeClass5Skills");
        private void Tick(float seconds) => Call(simulator, "TickClass5Skills", seconds);
        private float Cooldown(object ally) => (float)Call(simulator, "GetClass5SkillCooldownRemaining", Field(ally, "SlotIndex"));
        private void Ready(object ally)
        {
            var dictionary = (IDictionary)Field(simulator, "class5Skills");
            Set(Field(dictionary[Field(ally, "SlotIndex")], "State"), "CooldownRemaining", 0f);
        }
        private static int Hp(object runtime) => (int)Field(Field(runtime, "Stats"), "CurrentHp");
        private static object Checkpoint() => Activator.CreateInstance(GameType("Data.DailyChallengeCheckpoint"));

        private object AddAlly(string key, int skillLevel = 1, int slot = 0)
        {
            object runtime = Activator.CreateInstance(GameType("Battle.BattleSimulator").GetNestedType("AllyRuntime", Any), true);
            var data = ScriptableObject.CreateInstance(GameType("MasterData.MonsterDataSO")); fixtures.Add(data);
            Set(data, "monsterId", "monster_" + key); Set(data, "classRank", 5);
            Set(data, "rangeType", Enum.ToObject(GameType("MasterData.MonsterRangeType"), key == "ordion" || key == "noxveil" || key == "yggdrasia" ? 1 : 0));
            Set(data, "damageType", Enum.ToObject(GameType("MasterData.MonsterDamageType"), key == "noxveil" || key == "celestia" || key == "yggdrasia" ? 1 : 0));
            object owned = Activator.CreateInstance(GameType("Save.OwnedMonsterData"));
            Set(owned, "InstanceId", "c5-instance-" + slot); Set(owned, "MonsterId", "monster_" + key); Set(owned, "MonsterSkillLevel", skillLevel);
            Set(runtime, "Data", data); Set(runtime, "OwnedMonster", owned); Set(runtime, "Stats", Stats());
            Set(runtime, "SlotIndex", slot); Set(runtime, "RuntimeId", slot + 1);
            Set(runtime, "PositionAnchor", new Vector2(0f, .5f)); Set(runtime, "HomeAnchor", new Vector2(0f, .5f));
            Set(runtime, "AttackReachAnchor", .3f); Set(runtime, "SearchReachAnchor", 2f); Set(runtime, "CombatRadius", .035f);
            Set(runtime, "AttackTimer", 3.25f); Set(runtime, "TargetEnemyRuntimeId", 1);
            Allies.Add(runtime); return runtime;
        }

        private object AddEnemy(int id, float x)
        {
            object runtime = Activator.CreateInstance(GameType("Battle.BattleSimulator").GetNestedType("EnemyRuntime", Any), true);
            var data = ScriptableObject.CreateInstance(GameType("MasterData.EnemyDataSO")); fixtures.Add(data);
            Set(runtime, "Data", data); Set(runtime, "RuntimeId", id); Set(runtime, "Stats", Stats());
            Set(runtime, "PositionAnchor", new Vector2(x, .5f)); Set(runtime, "CombatRadius", .037f);
            Set(runtime, "MoveSpeed", 1f); Set(runtime, "AttackTimer", 2.5f);
            Enemies.Add(runtime); return runtime;
        }

        private static object Stats()
        {
            object stats = Activator.CreateInstance(GameType("Battle.BattleUnitStats"));
            Set(stats, "MaxHp", 1000000); Set(stats, "CurrentHp", 1000000);
            Set(stats, "Attack", 100); Set(stats, "Wisdom", 100); Set(stats, "AttackSpeed", 1f);
            return stats;
        }
    }
}
