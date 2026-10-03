using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class OnlineTrainingGrantTests
    {
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object F(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n).SetValue(o, v);
        private static object Save() => T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static object Owned(string id)
        {
            var m = Activator.CreateInstance(T("Save.OwnedMonsterData"));
            Set(m, "InstanceId", id); Set(m, "MonsterId", "monster_dragon_whelp");
            Set(m, "Level", 4); Set(m, "Exp", 8); Set(m, "MonsterSkillLevel", 1);
            return m;
        }
        private static object Operation(string kind, long revision, int drops, int cores, object update = null)
        {
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "Kind", kind); Set(op, "Revision", revision); Set(op, "Free", 900);
            Set(op, "TrainingDrops", drops); Set(op, "TrialStarCores", cores);
            if (update != null)
            {
                var array = Array.CreateInstance(update.GetType(), 1); array.SetValue(update, 0);
                Set(op, "UpdatedMonsters", array);
            }
            return op;
        }
        private static object Apply(object save, object op) => T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { save, op });

        [Test]
        public void TrainingDeliveryIsDurableIdempotentAndPreservesEquipmentAndInnateValues()
        {
            var source = Save(); var owned = Owned("owned-a");
            Set(source, "TrainingDrops", 20); Set(owned, "TrainingAttack", 9);
            Set(owned, "IndividualAttack", 97); Set(owned, "HasIndividualValues", true);
            Set(owned, "EquippedWeaponInstanceId", "sword-a"); Set(owned, "IsFavorite", true);
            ((IList)F(source, "OwnedMonsters")).Add(owned);
            var update = Owned("owned-a"); Set(update, "TrainingAttack", 10);
            var op = Operation("monster_training", 1, 0, 0, update);
            var committed = Apply(source, op);
            var once = ((IList)F(committed, "OwnedMonsters"))[0];
            Assert.That(F(once, "TrainingAttack"), Is.EqualTo(10));
            Assert.That(F(once, "IndividualAttack"), Is.EqualTo(97));
            Assert.That(F(once, "EquippedWeaponInstanceId"), Is.EqualTo("sword-a"));
            Assert.That(F(once, "IsFavorite"), Is.True);
            Assert.That(F(source, "TrainingDrops"), Is.EqualTo(20), "Do not mutate the live profile before persistence.");
            Assert.That(F(owned, "TrainingAttack"), Is.EqualTo(9));
            var replayed = Apply(committed, op);
            Assert.That(F(replayed, "TrainingDrops"), Is.Zero);
            Assert.That(F(replayed, "EconomyRevision"), Is.EqualTo(1));
        }

        [Test]
        public void ChallengeSettlementKeepsEachRunRewardAndReplayingTheRunDoesNotPayTwice()
        {
            var source = Save(); var owned = Owned("owned-a"); ((IList)F(source, "OwnedMonsters")).Add(owned);
            var update = Owned("owned-a"); Set(update, "Level", 5); Set(update, "Exp", 40);
            var first = Operation("daily_challenge_finish", 1, 9, 1, update);
            Set(first, "GoldDelta", 1500);
            var committed = Apply(source, first);
            var second = Operation("daily_challenge_finish", 2, 18, 9, update);
            Set(second, "GoldDelta", 2800);
            committed = Apply(committed, second); committed = Apply(committed, second);
            Assert.That(F(committed, "TrainingDrops"), Is.EqualTo(18));
            Assert.That(F(committed, "TrialStarCores"), Is.EqualTo(9));
            Assert.That(F(committed, "Gold"), Is.EqualTo((int)F(source, "Gold") + 4300));
            Assert.That(F(((IList)F(committed, "OwnedMonsters"))[0], "Level"), Is.EqualTo(4),
                "An old server snapshot must not overwrite locally earned experience.");
        }

        [Test]
        public void OrdinaryEconomyOperationDoesNotEraseTheDedicatedMaterialWallet()
        {
            var source = Save(); Set(source, "TrainingDrops", 48); Set(source, "TrialStarCores", 12);
            var committed = Apply(source, Operation("reward", 1, 0, 0));
            Assert.That(F(committed, "TrainingDrops"), Is.EqualTo(48));
            Assert.That(F(committed, "TrialStarCores"), Is.EqualTo(12));
        }

        [TestCase(-1, 0)] [TestCase(0, -1)]
        public void NegativeAuthoritativeWalletIsRejectedWithoutMutatingLiveData(int drops, int cores)
        {
            var source = Save(); Set(source, "TrainingDrops", 3);
            var thrown = Assert.Throws<TargetInvocationException>(() => Apply(source,
                Operation("daily_challenge_finish", 1, drops, cores)));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(F(source, "TrainingDrops"), Is.EqualTo(3));
        }

        [Test]
        public void InvalidTrainingLevelOrUnknownMonsterCannotReplaceAnOwnedMonster()
        {
            var source = Save(); var owned = Owned("owned-a"); ((IList)F(source, "OwnedMonsters")).Add(owned);
            var update = Owned("owned-a"); Set(update, "TrainingAttack", 11);
            Assert.Throws<TargetInvocationException>(() => Apply(source, Operation("monster_training", 1, 1, 0, update)));
            var unknown = Owned("unknown");
            Assert.Throws<TargetInvocationException>(() => Apply(source, Operation("monster_training", 1, 1, 0, unknown)));
            Assert.That(F(owned, "TrainingAttack"), Is.Zero);
        }

        [Test]
        public void ExperienceIsAddedToCurrentProgressAndReplayDoesNotRepeatIt()
        {
            var managerType = T("Managers.MasterDataManager");
            var previousManager = managerType.GetProperty("Instance").GetValue(null);
            var owner = new GameObject("TrainingGrantMaster"); owner.SetActive(false);
            var manager = owner.AddComponent(managerType);
            managerType.GetProperty("Instance").SetValue(null, manager);
            try
            {
            manager.GetType().GetMethod("Initialize").Invoke(manager, null);
            var source = Save(); var owned = Owned("owned-a"); ((IList)F(source, "OwnedMonsters")).Add(owned);
            var op = Operation("daily_challenge_finish", 1, 9, 1);
            var reward = Activator.CreateInstance(T("Save.MonsterExperienceReward"));
            Set(reward, "InstanceId", "owned-a"); Set(reward, "Amount", 1000);
            var array = Array.CreateInstance(reward.GetType(), 1); array.SetValue(reward, 0);
            Set(op, "MonsterExperienceRewards", array);
            var committed = Apply(source, op);
            var recipient = ((IList)F(committed, "OwnedMonsters"))[0];
            Assert.That(F(recipient, "Level"), Is.EqualTo(6));
            Assert.That(F(recipient, "Exp"), Is.EqualTo(301));
            Set(recipient, "Level", 8); Set(recipient, "Exp", 22);
            var replayed = Apply(committed, op);
            Assert.That(F(((IList)F(replayed, "OwnedMonsters"))[0], "Level"), Is.EqualTo(8));
            Assert.That(F(((IList)F(replayed, "OwnedMonsters"))[0], "Exp"), Is.EqualTo(22));
            }
            finally
            {
                managerType.GetProperty("Instance").SetValue(null, previousManager);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
