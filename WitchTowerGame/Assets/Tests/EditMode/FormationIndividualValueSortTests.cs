using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class FormationIndividualValueSortTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject owner;
        private Type controllerType;
        private Type entryType;
        private Type sortType;
        private object controller;

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

        [SetUp]
        public void SetUp()
        {
            controllerType = RuntimeType("WitchTower.Formation.FormationSceneController");
            entryType = controllerType.GetNestedType("MonsterEntry", BindingFlags.NonPublic);
            sortType = controllerType.GetNestedType("SortMode", BindingFlags.NonPublic);
            owner = new GameObject("FormationIndividualValueSortFixture");
            owner.SetActive(false);
            controller = owner.AddComponent(controllerType);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        }

        [Test]
        public void IndividualValueSortUsesExactSixStatTotalRatherThanRoundedAverageOrLevel()
        {
            object lower = AddEntry("lower", 301, 99, 99);
            object higher = AddEntry("higher", 302, 1, 1);
            Assert.That(entryType.GetField("IndividualAverage").GetValue(lower), Is.EqualTo(50));
            Assert.That(entryType.GetField("IndividualAverage").GetValue(higher), Is.EqualTo(50));
            SetSort("IndividualValue");

            Assert.That(DisplayIds(), Is.EqualTo(new[] { "higher", "lower" }));
            Assert.That(Roster.Cast<object>(), Is.EqualTo(new[] { lower, higher }), "Sorting must not reorder the owned roster itself.");
        }

        [Test]
        public void EqualIndividualValuesUseNewestAcquisitionThenOrdinalInstanceId()
        {
            AddEntry("z-tie", 450, 99, 8);
            AddEntry("a-tie", 450, 1, 8);
            AddEntry("older", 450, 99, 7);
            AddEntry("best", 600, 1, 1);
            SetSort("IndividualValue");

            for (int i = 0; i < 3; i++)
                Assert.That(DisplayIds(), Is.EqualTo(new[] { "best", "a-tie", "z-tie", "older" }));
        }

        [Test]
        public void IndividualValueSortPreservesPartySlotPriorityFiltersAndBulkSelection()
        {
            AddEntry("unselected-high", 600, 99, 9, true);
            object partySecond = AddEntry("party-second", 480, 20, 8, true);
            object partyFirst = AddEntry("party-first", 120, 1, 7);
            AddEntry("unselected-low", 60, 1, 6);
            IList selected = GetList("selectedMonsters");
            foreach (object entry in new[] { partyFirst, partySecond, null, null, null }) selected.Add(entry);
            var bulkSelection = (System.Collections.Generic.HashSet<string>)GetField("bulkReleaseSelectedInstanceIds");
            bulkSelection.Add("unselected-low");
            object[] originalParty = selected.Cast<object>().ToArray();
            object[] originalRoster = Roster.Cast<object>().ToArray();
            SetSort("IndividualValue");

            Assert.That(DisplayIds(), Is.EqualTo(new[] { "party-first", "party-second", "unselected-high", "unselected-low" }));
            SetFilter("Selected");
            Assert.That(DisplayIds(), Is.EqualTo(new[] { "party-first", "party-second" }));
            SetFilter("Unselected");
            Assert.That(DisplayIds(), Is.EqualTo(new[] { "unselected-high", "unselected-low" }));
            SetFilter("Favorite");
            Assert.That(DisplayIds(), Is.EqualTo(new[] { "party-second", "unselected-high" }));
            Assert.That(selected.Cast<object>(), Is.EqualTo(originalParty), "Display sorting/filtering must not change party slots.");
            Assert.That(Roster.Cast<object>(), Is.EqualTo(originalRoster));
            Assert.That(bulkSelection, Is.EquivalentTo(new[] { "unselected-low" }));
        }

        [Test]
        public void SortButtonCycleIncludesIndividualValueAndReturnsToExistingDefault()
        {
            string[] expectedModes = { "Favorite", "Level", "Acquired", "Class", "IndividualValue" };
            Assert.That(Enum.GetNames(sortType), Is.EqualTo(expectedModes), "Existing sort values retain their order.");
            foreach (string mode in expectedModes)
            {
                Assert.That(GetField("currentSortMode").ToString(), Is.EqualTo(mode));
                Call("CycleSortMode");
            }

            Assert.That(GetField("currentSortMode").ToString(), Is.EqualTo("Favorite"));
            Assert.That(controllerType.GetMethod("GetSortModeLabel", PrivateStatic)
                .Invoke(null, new[] { Enum.Parse(sortType, "IndividualValue") }), Is.EqualTo("個体値順"));
        }

        [Test]
        public void IndividualValueTotalUsesAllSixNormalizedOwnedMonsterValues()
        {
            Type ownedType = RuntimeType("WitchTower.Save.OwnedMonsterData");
            object owned = Activator.CreateInstance(ownedType);
            ownedType.GetField("HasIndividualValues").SetValue(owned, true);
            string[] fields = { "IndividualHp", "IndividualAttack", "IndividualWisdom", "IndividualDefense", "IndividualMagicDefense", "IndividualAttackSpeed" };
            int[] values = { -1, 25, 50, 75, 100, 101 };
            for (int i = 0; i < fields.Length; i++) ownedType.GetField(fields[i]).SetValue(owned, values[i]);
            ownedType.GetField("PlusHp").SetValue(owned, 999);
            ownedType.GetField("Level").SetValue(owned, 999);

            Assert.That(controllerType.GetMethod("GetIndividualValueTotal", PrivateStatic).Invoke(null, new[] { owned }), Is.EqualTo(350));
            Assert.That(controllerType.GetMethod("GetIndividualValueTotal", PrivateStatic).Invoke(null, new object[] { null }), Is.EqualTo(300));
            Assert.That(ownedType.GetField("PlusHp").GetValue(owned), Is.EqualTo(999), "Plus values are not individual values.");
            Assert.That(ownedType.GetField("Level").GetValue(owned), Is.EqualTo(999));
        }

        private IList Roster => GetList("roster");
        private IList GetList(string name) => (IList)GetField(name);
        private object GetField(string name) => controllerType.GetField(name, PrivateInstance).GetValue(controller);
        private object Call(string name) => controllerType.GetMethod(name, PrivateInstance).Invoke(controller, null);
        private void SetSort(string name) => controllerType.GetField("currentSortMode", PrivateInstance).SetValue(controller, Enum.Parse(sortType, name));
        private void SetFilter(string name)
        {
            Type filterType = controllerType.GetNestedType("FilterMode", BindingFlags.NonPublic);
            controllerType.GetField("currentFilterMode", PrivateInstance).SetValue(controller, Enum.Parse(filterType, name));
        }

        private object AddEntry(string id, int total, int level, int acquired, bool favorite = false)
        {
            Type damageType = RuntimeType("WitchTower.MasterData.MonsterDamageType");
            object entry = Activator.CreateInstance(entryType, new object[]
            {
                id, id, string.Empty, level, 100, 1, 0, Mathf.RoundToInt(total / 6f), acquired,
                favorite, false, Enum.Parse(damageType, "Physical"), total
            });
            Roster.Add(entry);
            return entry;
        }

        private string[] DisplayIds() => ((IEnumerable)Call("BuildDisplayEntries")).Cast<object>()
            .Select(entry => (string)entryType.GetField("InstanceId").GetValue(entry)).ToArray();
    }
}
