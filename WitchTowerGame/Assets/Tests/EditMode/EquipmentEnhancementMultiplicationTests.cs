using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class EquipmentEnhancementMultiplicationTests
    {
        private Type catalog;
        private ScriptableObject data;
        private object owned;

        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field).SetValue(target, value);

        private static float Get(object target, string field) =>
            (float)target.GetType().GetField(field).GetValue(target);

        [SetUp]
        public void SetUp()
        {
            catalog = GameType("WitchTower.Data.EquipmentEnhancementCatalog");
            data = ScriptableObject.CreateInstance(GameType("WitchTower.MasterData.EquipmentDataSO"));
            owned = Activator.CreateInstance(GameType("WitchTower.Save.OwnedEquipmentData"));
            Set(data, "classRank", 3);
            Set(owned, "QualityRank", 4);
            Set(owned, "HasRolledStats", true);
            foreach (string stat in new[] { "Attack", "Wisdom", "Defense", "MagicDefense", "Hp" })
                Set(owned, "Rolled" + stat, 20);
            Set(owned, "RolledCritRate", 0.02f);
            Set(owned, "RolledAttackSpeed", 0.03f);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(data);

        private object Bonus() => catalog.GetMethod("ResolveEquipmentBonus").Invoke(null, new[] { data, owned });

        private void Enhance(string kind)
        {
            object relic = catalog.GetMethod("GetRelic").Invoke(null, new object[] { "relic_" + kind + "_ember" });
            catalog.GetMethod("ApplyEnhancementSuccess").Invoke(null, new[] { data, owned, relic });
        }

        [TestCase("safe", 1.05f)]
        [TestCase("safe,safe", 1.1025f)]
        [TestCase("risky,risky", 1.3225f)]
        [TestCase("volatile,volatile", 1.69f)]
        [TestCase("safe,risky,volatile", 1.56975f)]
        [TestCase("volatile,risky,safe", 1.56975f)]
        public void SuccessfulEnhancementsMultiplyResolvedStatsAcrossSaveReload(string sequence, float multiplier)
        {
            object before = Bonus();
            foreach (string kind in sequence.Split(','))
            {
                Enhance(kind);
                owned = JsonUtility.FromJson(JsonUtility.ToJson(owned), owned.GetType());
            }

            object after = Bonus();
            foreach (string stat in new[] { "AttackPercent", "WisdomPercent", "DefensePercent", "MagicDefensePercent", "HpPercent" })
                Assert.That(Get(after, stat), Is.EqualTo(Get(before, stat) * multiplier).Within(0.00001f), stat);
        }

        [Test]
        public void ExistingSavedBonusIsPreservedAndNextEnhancementCompounds()
        {
            Set(owned, "EnhancementBonusRate", 0.20f);
            owned = JsonUtility.FromJson(JsonUtility.ToJson(owned), owned.GetType());
            object before = Bonus();
            Assert.That(Get(owned, "EnhancementBonusRate"), Is.EqualTo(0.20f));
            Enhance("safe");
            Assert.That(Get(owned, "EnhancementBonusRate"), Is.EqualTo(0.26f).Within(0.00001f));
            Assert.That(Get(Bonus(), "AttackPercent"), Is.EqualTo(Get(before, "AttackPercent") * 1.05f).Within(0.00001f));
        }

        [Test]
        public void CritAndSpeedKeepTheirFixedBonuses()
        {
            Enhance("safe");
            Enhance("risky");
            Enhance("volatile");
            Assert.That(Get(owned, "EnhancementCritRateFlat"), Is.EqualTo(0.016f).Within(0.000001f));
            Assert.That(Get(owned, "EnhancementAttackSpeedFlat"), Is.EqualTo(0.016f).Within(0.000001f));
        }
    }
}
