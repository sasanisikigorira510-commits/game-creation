using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class MonsterTrainingTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] TrainingFields = { "TrainingHp", "TrainingAttack", "TrainingWisdom", "TrainingDefense", "TrainingMagicDefense", "TrainingAttackSpeed" };
        private static readonly string[] IndividualFields = { "IndividualHp", "IndividualAttack", "IndividualWisdom", "IndividualDefense", "IndividualMagicDefense", "IndividualAttackSpeed" };
        private GameObject owner;
        private object master;
        private object previousMaster;
        private UnityEngine.Random.State randomState;

        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object item, string name) => item.GetType().GetField(name, Any).GetValue(item);
        private static void SetField(object item, string name, object value) => item.GetType().GetField(name, Any).SetValue(item, value);
        private static object Property(object item, string name) => item.GetType().GetProperty(name, Any).GetValue(item);
        private static void SetProperty(object item, string name, object value) => item.GetType().GetProperty(name, Any).SetValue(item, value);
        private static object Call(object item, string name, params object[] arguments) => item.GetType().GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(item, arguments);
        private static object StaticCall(string type, string name, params object[] arguments) => GameType(type).GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(null, arguments);
        private static object Stat(int value) => Enum.ToObject(GameType("Data.MonsterTrainingStatType"), value);
        private static int[] Levels(object monster) => TrainingFields.Select(name => (int)Field(monster, name)).ToArray();
        private static void SetLevels(object monster, params int[] levels)
        {
            for (int index = 0; index < TrainingFields.Length; index++) SetField(monster, TrainingFields[index], levels[index]);
        }

        [SetUp]
        public void SetUp()
        {
            randomState = UnityEngine.Random.state;
            previousMaster = GameType("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            owner = new GameObject("MonsterTrainingTestMasterData");
            owner.SetActive(false);
            master = owner.AddComponent(GameType("Managers.MasterDataManager"));
            GameType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            Call(master, "Initialize");
        }

        [TearDown]
        public void TearDown()
        {
            GameType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, previousMaster);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Random.state = randomState;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void EveryStatReachesTenWithExactly125DropsAndTheEleventhTrainingChangesNothing(int stat)
        {
            object profile = NewProfile();
            object monster = AddMonster(profile);
            SetProperty(profile, "TrainingDrops", 125);
            SetProperty(profile, "TrialStarCores", 53);
            int[] costs = { 1, 2, 4, 6, 9, 12, 16, 20, 25, 30 };
            int balance = 125;
            for (int level = 0; level < 10; level++)
            {
                object result = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, Field(monster, "InstanceId"), Stat(stat));
                Assert.That(Property(result, "Succeeded"), Is.True);
                Assert.That(Property(result, "PreviousLevel"), Is.EqualTo(level));
                Assert.That(Property(result, "NewLevel"), Is.EqualTo(level + 1));
                Assert.That(Property(result, "Cost"), Is.EqualTo(costs[level]));
                balance -= costs[level];
                Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(balance));
                Assert.That(Levels(monster), Is.EqualTo(Enumerable.Range(0, 6).Select(index => index == stat ? level + 1 : 0).ToArray()));
            }
            Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(0));
            Assert.That(Property(profile, "TrialStarCores"), Is.EqualTo(53), "Training must not spend the skill currency.");
            string before = SaveJson(profile);
            object capped = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, Field(monster, "InstanceId"), Stat(stat));
            Assert.That(Property(capped, "Status").ToString(), Is.EqualTo("LevelCapReached"));
            Assert.That(SaveJson(profile), Is.EqualTo(before));
        }

        [TestCase("insufficient", "InsufficientDrops")]
        [TestCase("missing", "InvalidMonster")]
        [TestCase("empty", "InvalidMonster")]
        [TestCase("invalid_stat", "InvalidStat")]
        [TestCase("invalid_stat_high", "InvalidStat")]
        public void RejectedTrainingPreservesWalletAndMonster(string scenario, string expectedStatus)
        {
            object profile = NewProfile();
            object monster = AddMonster(profile);
            SetProperty(profile, "TrainingDrops", scenario == "insufficient" ? 1 : 99);
            SetField(monster, "TrainingAttack", 2);
            string id = (string)Field(monster, "InstanceId");
            if (scenario == "missing") id = "not-owned";
            if (scenario == "empty") id = " ";
            object stat = Stat(scenario == "invalid_stat" ? -1 : scenario == "invalid_stat_high" ? int.MaxValue : 1);
            string before = SaveJson(profile);
            object result = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, id, stat);
            Assert.That(Property(result, "Succeeded"), Is.False);
            Assert.That(Property(result, "Status").ToString(), Is.EqualTo(expectedStatus));
            Assert.That(SaveJson(profile), Is.EqualTo(before));
        }

        [Test]
        public void MissingProfileIsRejectedWithoutThrowing()
        {
            object result = StaticCall("Data.MonsterTrainingService", "TryTrain", null, "owned", Stat(0));
            Assert.That(Property(result, "Status").ToString(), Is.EqualTo("InvalidProfile"));
        }

        [Test]
        public void MaximumIntegerWalletCanBuyTheLastLevelWithoutOverflow()
        {
            object profile = NewProfile();
            object monster = AddMonster(profile);
            SetField(monster, "TrainingAttackSpeed", 9);
            SetProperty(profile, "TrainingDrops", int.MaxValue);
            object result = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, Field(monster, "InstanceId"), Stat(5));
            Assert.That(Property(result, "Succeeded"), Is.True);
            Assert.That(Field(monster, "TrainingAttackSpeed"), Is.EqualTo(10));
            Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(int.MaxValue - 30));
        }

        [Test]
        public void LegacyAndMalformedSaveTrainingIsNormalizedWithoutLosingOtherProgress()
        {
            object save = StaticCall("Save.PlayerSaveData", "CreateDefault");
            object monster = Activator.CreateInstance(GameType("Save.OwnedMonsterData"));
            SetField(monster, "InstanceId", "legacy-training-owner");
            SetField(monster, "MonsterId", "monster_dragon_whelp");
            SetField(monster, "Level", 1);
            SetField(monster, "PlusValue", 7);
            SetLevels(monster, int.MinValue, int.MaxValue, -1, 11, 6, 0);
            SetField(monster, "MonsterSkillLevel", int.MaxValue);
            ((IList)Field(save, "OwnedMonsters")).Add(monster);
            SetField(save, "TrainingDrops", -20);
            SetField(save, "TrialStarCores", int.MinValue);
            SetField(save, "DailyChallenges", null);
            SetField(save, "Gold", 1234);
            object profile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { save });
            Assert.That(Levels(monster), Is.EqualTo(new[] { 0, 10, 0, 10, 6, 0 }));
            Assert.That(Field(monster, "MonsterSkillLevel"), Is.EqualTo(5));
            Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(0));
            Assert.That(Property(profile, "TrialStarCores"), Is.EqualTo(0));
            Assert.That(Property(profile, "DailyChallenges"), Is.Not.Null);
            Assert.That(Property(profile, "Gold"), Is.EqualTo(1234));
            Assert.That(Property(monster, "TotalPlusValue"), Is.EqualTo(7));

            object legacy = Activator.CreateInstance(GameType("Save.OwnedMonsterData"));
            StaticCall("Data.MonsterTrainingService", "Normalize", legacy);
            Assert.That(Levels(legacy), Is.EqualTo(new int[6]));
            Assert.That(Field(legacy, "MonsterSkillLevel"), Is.EqualTo(1));

            // Normalize again when exporting, even if a live field was corrupted after load.
            SetField(monster, "TrainingWisdom", 99);
            SetField(monster, "MonsterSkillLevel", -10);
            object exported = Call(profile, "ToSaveData", 7);
            object exportedMonster = ((IList)Field(exported, "OwnedMonsters"))[0];
            Assert.That(Field(exportedMonster, "TrainingWisdom"), Is.EqualTo(10));
            Assert.That(Field(exportedMonster, "MonsterSkillLevel"), Is.EqualTo(1));
        }

        [Test]
        public void TrainingSkillAndActiveDailyRunSurviveJsonSaveRestore()
        {
            object profile = NewProfile();
            object monster = AddMonster(profile);
            SetProperty(profile, "TrainingDrops", 81);
            SetProperty(profile, "TrialStarCores", 37);
            SetLevels(monster, 1, 2, 3, 4, 5, 6);
            SetField(monster, "MonsterSkillLevel", 4);
            SetField(monster, "TrainingParentInstanceIdA", "source-a");
            SetField(monster, "TrainingParentInstanceIdB", "source-b");
            object state = Property(profile, "DailyChallenges");
            SetField(state, "Day", "2026-10-03");
            object run = Activator.CreateInstance(GameType("Data.DailyChallengeRun"));
            SetField(run, "RunId", "persisted-test-run");
            SetField(run, "Mode", "horde");
            SetField(run, "StartDay", "2026-10-03");
            SetField(run, "ClearedStage", 5);
            SetField(run, "Status", "active");
            SetField(run, "PartyInstanceIds", new[] { (string)Field(monster, "InstanceId") });
            object checkpoint = Activator.CreateInstance(GameType("Data.DailyChallengeCheckpoint"));
            SetField(checkpoint, "Stage", 6);
            SetField(checkpoint, "DefeatedEnemies", 3);
            SetField(run, "Checkpoint", checkpoint);
            SetField(state, "ActiveRun", run);

            object restoredSave = JsonUtility.FromJson(SaveJson(profile), GameType("Save.PlayerSaveData"));
            object restored = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { restoredSave });
            object restoredMonster = ((IList)Property(restored, "OwnedMonsters"))[0];
            Assert.That(Levels(restoredMonster), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(Field(restoredMonster, "MonsterSkillLevel"), Is.EqualTo(4));
            Assert.That(Field(restoredMonster, "TrainingParentInstanceIdA"), Is.EqualTo("source-a"));
            Assert.That(Field(restoredMonster, "TrainingParentInstanceIdB"), Is.EqualTo("source-b"));
            Assert.That(Property(restored, "TrainingDrops"), Is.EqualTo(81));
            Assert.That(Property(restored, "TrialStarCores"), Is.EqualTo(37));
            object restoredRun = Field(Property(restored, "DailyChallenges"), "ActiveRun");
            Assert.That(Field(restoredRun, "RunId"), Is.EqualTo("persisted-test-run"));
            Assert.That(Field(restoredRun, "PartyInstanceIds"), Is.EqualTo(new[] { (string)Field(monster, "InstanceId") }));
            Assert.That(Field(restoredRun, "ClearedStage"), Is.EqualTo(5));
            Assert.That(Property(restoredRun, "IsActive"), Is.True);
            Assert.That(Field(Field(restoredRun, "Checkpoint"), "Stage"), Is.EqualTo(6));
            Assert.That(Field(Field(restoredRun, "Checkpoint"), "DefeatedEnemies"), Is.EqualTo(3));
        }

        [TestCase(1, 1000, 100, 100, 2.0f)]
        [TestCase(11, 1050, 111, 107, 2.02f)]
        public void PerfectIndividualValuesStillGainTrainingOnBaseAndLevelGrowthOnly(int level, int intrinsicHp, int intrinsicAttack, int intrinsicDefense, float intrinsicSpeed)
        {
            var data = ScriptableObject.CreateInstance(GameType("MasterData.MonsterDataSO"));
            try
            {
                SetField(data, "classRank", 1);
                object baseStats = Field(data, "baseStats");
                SetField(baseStats, "maxHp", 1000);
                foreach (string stat in new[] { "attack", "magicAttack", "defense", "magicDefense" }) SetField(baseStats, stat, 100);
                SetField(baseStats, "attackSpeed", 2f);
                SetField(data, "baseStats", baseStats);
                object growth = Field(data, "levelGrowth");
                foreach (string coefficient in new[] { "maxHpCoefficient", "attackCoefficient", "magicAttackCoefficient", "defenseCoefficient", "magicDefenseCoefficient", "attackSpeedCoefficient" }) SetField(growth, coefficient, 1f);
                SetField(data, "levelGrowth", growth);
                object monster = Activator.CreateInstance(GameType("Save.OwnedMonsterData"));
                SetField(monster, "Level", level);
                SetField(monster, "HasIndividualValues", true);
                foreach (string stat in IndividualFields) SetField(monster, stat, 100);
                SetField(monster, "PlusValue", 7);
                SetField(monster, "FusionBonusHp", 3000);
                foreach (string stat in new[] { "FusionBonusAttack", "FusionBonusWisdom", "FusionBonusDefense", "FusionBonusMagicDefense" }) SetField(monster, stat, 300);
                SetField(monster, "FusionBonusAttackSpeed", 0.5f);
                object before = StaticCall("Battle.MonsterBattleStatsFactory", "Create", null, monster, data);
                SetLevels(monster, 10, 10, 10, 10, 10, 10);
                object trained = StaticCall("Battle.MonsterBattleStatsFactory", "Create", null, monster, data);
                Assert.That((int)Field(trained, "MaxHp") - (int)Field(before, "MaxHp"), Is.EqualTo(intrinsicHp * 0.30f).Within(1f));
                foreach (string stat in new[] { "Attack", "Wisdom" })
                    Assert.That((int)Field(trained, stat) - (int)Field(before, stat), Is.EqualTo(intrinsicAttack * 0.30f).Within(1f));
                foreach (string stat in new[] { "Defense", "MagicDefense" })
                    Assert.That((int)Field(trained, stat) - (int)Field(before, stat), Is.EqualTo(intrinsicDefense * 0.30f).Within(1f));
                Assert.That((float)Field(trained, "AttackSpeed") - (float)Field(before, "AttackSpeed"), Is.EqualTo(intrinsicSpeed * 0.20f).Within(0.0001f));
                Assert.That(IndividualFields.Select(stat => (int)Field(monster, stat)), Is.All.EqualTo(100));
                Assert.That(Field(monster, "PlusValue"), Is.EqualTo(7));
                Assert.That(Field(monster, "FusionBonusHp"), Is.EqualTo(3000));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealFusionKeepsTheHigherTrainingPerStatAcrossTwoGenerationsAndRecordsConsumedParents(bool reverse)
        {
            object profile = NewProfile();
            SetProperty(profile, "TrainingDrops", 64);
            SetProperty(profile, "TrialStarCores", 75);
            object first = AddMonster(profile, "monster_dragon_whelp", 20, 2);
            object second = AddMonster(profile, "monster_dragon_whelp", 20, 3);
            SetLevels(first, 3, 9, 1, 10, 2, 4);
            SetLevels(second, 8, 2, 7, 6, 10, 5);
            foreach (string stat in IndividualFields)
            {
                SetField(first, stat, 42);
                SetField(second, stat, 88);
            }
            string idA = (string)Field(reverse ? second : first, "InstanceId");
            string idB = (string)Field(reverse ? first : second, "InstanceId");
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, idA, idB, master, false);
            Assert.That(Property(result, "CanFuse"), Is.True);
            object child = Property(result, "CreatedMonster");
            int[] inherited = { 8, 9, 7, 10, 10, 5 };
            Assert.That(Levels(child), Is.EqualTo(inherited));
            Assert.That(Field(child, "TrainingParentInstanceIdA"), Is.EqualTo(idA));
            Assert.That(Field(child, "TrainingParentInstanceIdB"), Is.EqualTo(idB));
            Assert.That(Property(child, "TotalPlusValue"), Is.EqualTo(5));
            Assert.That(Field(child, "Level"), Is.EqualTo(1));
            Assert.That(IndividualFields.Select(stat => (int)Field(child, stat)).All(value => value == 42 || value == 88), Is.True, "Training inheritance must preserve the existing parental IV selection rule.");
            Assert.That(((IList)Property(profile, "OwnedMonsters")).Count, Is.EqualTo(1));
            Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(64));
            Assert.That(Property(profile, "TrialStarCores"), Is.EqualTo(75));
            var receipts = (IList)Property(profile, "TrainingFusionReceipts");
            Assert.That(receipts.Count, Is.EqualTo(1));
            Assert.That(Field(receipts[0], "ChildInstanceId"), Is.EqualTo(Field(child, "InstanceId")));
            Assert.That(Field(receipts[0], "ChildMonsterId"), Is.EqualTo(Field(child, "MonsterId")));
            Assert.That(Field(receipts[0], "ParentInstanceIdA"), Is.EqualTo(idA));
            Assert.That(Field(receipts[0], "ParentInstanceIdB"), Is.EqualTo(idB));
            Assert.That(Field(receipts[0], "ParentMonsterIdA"), Is.EqualTo("monster_dragon_whelp"));
            Assert.That(Field(receipts[0], "ParentMonsterIdB"), Is.EqualTo("monster_dragon_whelp"));

            object restored = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { JsonUtility.FromJson(SaveJson(profile), GameType("Save.PlayerSaveData")) });
            child = ((IList)Property(restored, "OwnedMonsters"))[0];
            Assert.That(Levels(child), Is.EqualTo(inherited));
            SetField(child, "Level", 40);
            object mate = AddMonster(restored, (string)Field(child, "MonsterId"), 40);
            SetLevels(mate, 0, 1, 0, 0, 0, 0);
            object next = StaticCall("Data.MonsterFusionService", "Fuse", restored, Field(child, "InstanceId"), Field(mate, "InstanceId"), master, false);
            Assert.That(Property(next, "CanFuse"), Is.True);
            Assert.That(Levels(Property(next, "CreatedMonster")), Is.EqualTo(inherited));
            var restoredReceipts = (IList)Property(restored, "TrainingFusionReceipts");
            Assert.That(restoredReceipts.Count, Is.EqualTo(2), "Consumed intermediate monsters must retain their ancestry until acknowledged.");
            Assert.That(Field(restoredReceipts[1], "ParentInstanceIdA"), Is.EqualTo(Field(restoredReceipts[0], "ChildInstanceId")));
        }

        [Test]
        public void ReceiptSaveIsADeepCopyAndAcknowledgementRemovesOnlyExplicitServerIds()
        {
            object profile = NewProfile();
            var receipts = (IList)Property(profile, "TrainingFusionReceipts");
            object first = NewReceipt("first"); object second = NewReceipt("second");
            receipts.Add(first); receipts.Add(second);
            object clone = Call(profile, "ToSaveData", 1);
            var clonedReceipts = (IList)Field(clone, "TrainingFusionReceipts");
            SetField(clonedReceipts[0], "ChildMonsterId", "changed-in-clone");
            clonedReceipts.RemoveAt(0);
            Assert.That(receipts.Count, Is.EqualTo(2), "Preparing an acknowledged save must not mutate live ancestry before the save succeeds.");
            Assert.That(Field(first, "ChildMonsterId"), Is.EqualTo("monster_flare_drake"));
            object restored = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { JsonUtility.FromJson(SaveJson(profile), GameType("Save.PlayerSaveData")) });
            Assert.That(((IList)Property(restored, "TrainingFusionReceipts")).Count, Is.EqualTo(2));
            Assert.That(Call(restored, "AcknowledgeTrainingFusionReceipts", (object)new[] { "first", "first", "unrecognized", "" }), Is.EqualTo(1));
            var remaining = (IList)Property(restored, "TrainingFusionReceipts");
            Assert.That(remaining.Count, Is.EqualTo(1));
            Assert.That(Field(remaining[0], "ChildInstanceId"), Is.EqualTo("second"));
            Assert.That(Call(restored, "AcknowledgeTrainingFusionReceipts", (object)null), Is.EqualTo(0));
            Assert.That(Call(restored, "AcknowledgeTrainingFusionReceipts", (object)new[] { "first" }), Is.EqualTo(0));
        }

        [Test]
        public void PendingReceiptLimitStopsFusionBeforeConsumingParentsAndNeverDropsUnacknowledgedAncestry()
        {
            object profile = NewProfile();
            object first = AddMonster(profile, level: 20); object second = AddMonster(profile, level: 20);
            var receipts = (IList)Property(profile, "TrainingFusionReceipts");
            for (int i = 0; i < 512; i++) receipts.Add(NewReceipt("pending-" + i));
            string before = SaveJson(profile);
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "Status").ToString(), Is.EqualTo("PendingTrainingSyncRequired"));
            Assert.That(SaveJson(profile), Is.EqualTo(before));
            Assert.That(receipts.Count, Is.EqualTo(512));
            Assert.That(Call(profile, "AcknowledgeTrainingFusionReceipts", (object)new[] { "pending-0" }), Is.EqualTo(1));
            result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "CanFuse"), Is.True);
            Assert.That(receipts.Count, Is.EqualTo(512));
            Assert.That(Field(receipts[0], "ChildInstanceId"), Is.EqualTo("pending-1"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActiveChallengeParticipantsCannotBeFusedOrReleasedButOtherParentsCanFuse(bool reverse)
        {
            object profile = NewProfile();
            object participant = AddMonster(profile, level: 20); object mate = AddMonster(profile, level: 20);
            object run = ActivateRun(profile, (string)Field(participant, "InstanceId"));
            string idA = (string)Field(reverse ? mate : participant, "InstanceId");
            string idB = (string)Field(reverse ? participant : mate, "InstanceId");
            string before = SaveJson(profile);
            object preview = StaticCall("Data.MonsterFusionService", "PreviewFusion", profile, idA, idB, master);
            Assert.That(Property(preview, "Status").ToString(), Is.EqualTo("DailyChallengePartyLocked"));
            object fused = StaticCall("Data.MonsterFusionService", "Fuse", profile, idA, idB, master, true);
            Assert.That(Property(fused, "Status").ToString(), Is.EqualTo("DailyChallengePartyLocked"));
            Assert.That(Call(profile, "TryReleaseMonster", Field(participant, "InstanceId"), true, null), Is.False,
                "Even forced release must not consume the party saved in an active run.");
            Assert.That(SaveJson(profile), Is.EqualTo(before));
            object third = AddMonster(profile, level: 20); object fourth = AddMonster(profile, level: 20);
            object allowed = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(third, "InstanceId"), Field(fourth, "InstanceId"), master, false);
            Assert.That(Property(allowed, "CanFuse"), Is.True);
            SetField(run, "Status", "completed");
            Assert.That(Call(profile, "IsDailyChallengePartyMonster", Field(participant, "InstanceId")), Is.False);
            Assert.That(Call(profile, "TryReleaseMonster", Field(participant, "InstanceId"), true, null), Is.True);
        }

        [Test]
        public void ActiveChallengeBlocksTrainingWithoutSpendingDropsUntilRunEnds()
        {
            object profile = NewProfile(); object monster = AddMonster(profile);
            SetProperty(profile, "TrainingDrops", 5);
            object run = ActivateRun(profile, "different-party-member");
            string before = SaveJson(profile);
            object rejected = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, Field(monster, "InstanceId"), Stat(0));
            Assert.That(Property(rejected, "Status").ToString(), Is.EqualTo("ChallengeActive"));
            Assert.That(SaveJson(profile), Is.EqualTo(before));
            SetField(run, "Status", "completed");
            object trained = StaticCall("Data.MonsterTrainingService", "TryTrain", profile, Field(monster, "InstanceId"), Stat(0));
            Assert.That(Property(trained, "Succeeded"), Is.True);
            Assert.That(Property(profile, "TrainingDrops"), Is.EqualTo(4));
        }

        private static object ActivateRun(object profile, params string[] instances)
        {
            object run = Activator.CreateInstance(GameType("Data.DailyChallengeRun"));
            SetField(run, "Status", "active"); SetField(run, "PartyInstanceIds", instances);
            SetField(Property(profile, "DailyChallenges"), "ActiveRun", run);
            return run;
        }

        private static object NewReceipt(string childId)
        {
            object receipt = Activator.CreateInstance(GameType("Save.TrainingFusionReceipt"));
            SetField(receipt, "ChildInstanceId", childId); SetField(receipt, "ChildMonsterId", "monster_flare_drake");
            SetField(receipt, "ParentInstanceIdA", childId + "-parent-a"); SetField(receipt, "ParentInstanceIdB", childId + "-parent-b");
            SetField(receipt, "ParentMonsterIdA", "monster_dragon_whelp"); SetField(receipt, "ParentMonsterIdB", "monster_dragon_whelp");
            return receipt;
        }

        [TestCase(-1, 1.0f)]
        [TestCase(1, 1.0f)]
        [TestCase(2, 1.1f)]
        [TestCase(3, 1.2f)]
        [TestCase(4, 1.3f)]
        [TestCase(5, 1.4f)]
        [TestCase(int.MaxValue, 1.4f)]
        public void SkillPowerUsesTenPercentStepsAndClampsToOneThroughFive(int level, float expected)
        {
            Assert.That(StaticCall("Data.MonsterSkillGrowthCatalog", "GetPowerMultiplier", level), Is.EqualTo(expected).Within(0.00001f));
        }

        private static object NewProfile()
        {
            object profile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { StaticCall("Save.PlayerSaveData", "CreateDefault") });
            ((IList)Property(profile, "OwnedMonsters")).Clear();
            return profile;
        }

        private static object AddMonster(object profile, string id = "monster_dragon_whelp", int level = 1, int plus = 0) => Call(profile, "AddOwnedMonster", id, level, plus, false);
        private static string SaveJson(object profile) => JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
    }
}
