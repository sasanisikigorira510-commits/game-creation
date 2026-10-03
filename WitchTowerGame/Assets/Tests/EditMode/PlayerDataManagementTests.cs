using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class PlayerDataManagementTests
    {
        private string directory;
        private string path;
        private object repository;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Save." + name, true);
        private static object Save() => T("PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static object F(object obj, string field) => obj.GetType().GetField(field).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field).SetValue(obj, value);
        private bool Commit(object save, object previous = null)
        {
            var args = new object[] { save, previous, "test", null };
            return (bool)repository.GetType().GetMethod("TryCommit").Invoke(repository, args);
        }
        private object Load(out bool blocked)
        {
            var args = new object[] { null, false, null };
            bool result = (bool)repository.GetType().GetMethod("TryLoad").Invoke(repository, args);
            blocked = (bool)args[1];
            return result ? args[0] : null;
        }
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "WitchTowerData_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "save.json");
            repository = Activator.CreateInstance(T("PlayerSaveRepository"), path);
        }
        [TearDown] public void Cleanup() { Directory.Delete(directory, true); }

        [Test] public void FirstLaunchCreatesStableIdentityAndDurableGeneration()
        {
            var first = Load(out bool blocked);
            Assert.That(blocked, Is.False);
            Assert.That((string)F(first, "PlayerId"), Has.Length.EqualTo(32));
            var second = Load(out _);
            Assert.That(F(second, "PlayerId"), Is.EqualTo(F(first, "PlayerId")));
            Assert.That(F(second, "SaveRevision"), Is.EqualTo(1L));
            Assert.That(Directory.GetFiles(path + ".history", "*.json"), Has.Length.EqualTo(1));
        }
        [Test] public void CorruptLegacyFilesAreRetainedAndNeverReset()
        {
            File.WriteAllText(path, "{ broken }");
            File.WriteAllText(path + ".bak", "{ broken backup }");
            Assert.That(Load(out bool blocked), Is.Null);
            Assert.That(blocked, Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo("{ broken }"));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("{ broken backup }"));
        }
        [Test] public void EvenReadableOlderBackupRequiresReviewOfPurchases()
        {
            File.WriteAllText(path, "{ broken }");
            File.WriteAllText(path + ".bak", JsonUtility.ToJson(Save()));
            Assert.That(Load(out bool blocked), Is.Null);
            Assert.That(blocked, Is.True);
        }
        [Test] public void DurableGenerationSurvivesCorruptCache()
        {
            var first = Load(out _);
            var next = Save(); Set(next, "PaidGachaStones", 120);
            Assert.That(Commit(next, first), Is.True);
            File.WriteAllText(path, "broken cache");
            var loaded = Load(out bool blocked);
            Assert.That(blocked, Is.False);
            Assert.That(F(loaded, "PaidGachaStones"), Is.EqualTo(120));
        }
        [Test] public void CorruptLatestGenerationDoesNotSilentlyUndoDeliveredPurchase()
        {
            var first = Load(out _);
            var next = Save(); Set(next, "PaidGachaStones", 120);
            Assert.That(Commit(next, first), Is.True);
            File.WriteAllText(Path.Combine(path + ".history", "00000000000000000002.json"), "broken");
            Assert.That(Load(out bool blocked), Is.Null);
            Assert.That(blocked, Is.True);
        }
        [Test] public void DuplicateRevisionDoesNotOverwriteCommittedGeneration()
        {
            var first = Load(out _);
            var next = Save(); Set(next, "Gold", 123);
            Assert.That(Commit(next, first), Is.True);
            var competing = Save(); Set(competing, "Gold", 999);
            Assert.That(Commit(competing, first), Is.False);
            Assert.That(F(Load(out _), "Gold"), Is.EqualTo(123));
        }
        [Test] public void LegacyMigrationPreservesOriginalBytes()
        {
            string original = JsonUtility.ToJson(Save());
            File.WriteAllText(path, original);
            var first = Load(out _);
            Assert.That(Commit(first), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(path + ".history", "legacy-original.json")), Is.EqualTo(original));
        }
        [Test] public void AnomaliesAreRecordedWithoutDeletingTheEvidence()
        {
            var save = Save(); Set(save, "Gold", -20);
            Assert.That(Commit(save), Is.True);
            var loaded = Load(out _);
            Assert.That(F(loaded, "Gold"), Is.EqualTo(-20));
            Assert.That((IList)F(loaded, "DataWarnings"), Does.Contain("negative_currency"));
        }
        [Test] public void ReplayedGrantNeverDuplicatesMonstersOrRewards()
        {
            var save = Save();
            var op = Activator.CreateInstance(T("OnlineOperation"));
            Set(op, "Revision", 1L); Set(op, "Free", 600); Set(op, "Paid", 120); Set(op, "GoldDelta", 30);
            Set(op, "TransactionId", "tx-1");
            var monster = Activator.CreateInstance(T("OwnedMonsterData"));
            Set(monster, "InstanceId", "server-monster-1"); Set(monster, "MonsterId", "monster_rock_golem"); Set(monster, "Level", 1);
            var monsters = Array.CreateInstance(T("OwnedMonsterData"), 1); monsters.SetValue(monster, 0); Set(op, "Monsters", monsters);
            var stage = T("OnlineGrantApplier").GetMethod("Stage");
            var applied = stage.Invoke(null, new[] { save, op });
            var replayed = stage.Invoke(null, new[] { applied, op });
            Assert.That(F(save, "Gold"), Is.EqualTo(100), "Must not expose unpersisted result to gameplay.");
            Assert.That(F(replayed, "Gold"), Is.EqualTo(130));
            Assert.That((IList)F(replayed, "OwnedMonsters"), Has.Count.EqualTo(1));
            Assert.That((IList)F(replayed, "ProcessedIapTransactionIds"), Has.Count.EqualTo(1));
        }
        [Test] public void MissingEconomyOperationCannotBeSkipped()
        {
            var op = Activator.CreateInstance(T("OnlineOperation")); Set(op, "Revision", 2L);
            var error = Assert.Throws<TargetInvocationException>(() => T("OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { Save(), op }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }
        [Test] public void RetentionKeepsLatestHundredAndDailyCheckpoint()
        {
            object previous = Load(out _);
            for (int i = 0; i < 110; i++)
            {
                var next = Save(); Set(next, "Gold", i);
                Assert.That(Commit(next, previous), Is.True);
                previous = next;
            }
            Assert.That(Directory.GetFiles(path + ".history", "*.json").Length, Is.LessThanOrEqualTo(101));
            Assert.That(F(Load(out _), "Gold"), Is.EqualTo(109));
        }
    }
}
