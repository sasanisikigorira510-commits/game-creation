using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class FairyMonsterFusionTests
    {
        private const string Lili = "monster_bud_fairy_lili";
        private const string Lilia = "monster_flower_fairy_lilia";
        private const string Liliana = "monster_flower_crown_spirit_liliana";
        private const string Titania = "monster_spirit_queen_titania";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private GameObject owner;
        private object master;
        private object previousMaster;
        private UnityEngine.Random.State randomState;

        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object item, string name) => item.GetType().GetField(name, Any).GetValue(item);
        private static object Property(object item, string name) => item.GetType().GetProperty(name, Any).GetValue(item);
        private static object Call(object item, string name, params object[] arguments) => item.GetType().GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length)
            .Invoke(item, arguments);
        private static object StaticCall(string type, string name, params object[] arguments) => GameType(type).GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length)
            .Invoke(null, arguments);

        [SetUp]
        public void SetUp()
        {
            randomState = UnityEngine.Random.state;
            previousMaster = GameType("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            owner = new GameObject("FairyFusionTestMasterData");
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

        [TestCase(Lili, Lilia, 2)]
        [TestCase(Lilia, Liliana, 3)]
        [TestCase(Liliana, Titania, 4)]
        public void FairyEvolutionResolvesAndConsumesTwoDistinctParentsInEitherOrder(string parentId, string resultId, int resultClass)
        {
            object[] recipeArguments = { parentId, parentId, null };
            Assert.That(StaticCall("Data.MonsterFusionCatalog", "TryResolveRecipe", recipeArguments), Is.True);
            Assert.That(Property(recipeArguments[2], "IgnoreOrder"), Is.True);
            Assert.That(Property(recipeArguments[2], "RecipeType").ToString(), Is.EqualTo("ClassUp"));

            foreach (bool reverse in new[] { false, true })
            {
                object profile = NewProfile();
                object first = AddMaxLevelParent(profile, parentId, 2);
                object second = AddMaxLevelParent(profile, parentId, 3);
                object instanceA = Field(reverse ? second : first, "InstanceId");
                object instanceB = Field(reverse ? first : second, "InstanceId");
                string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));

                object preview = StaticCall("Data.MonsterFusionService", "PreviewFusion", profile, instanceA, instanceB, master);
                Assert.That(Property(preview, "CanFuse"), Is.True);
                Assert.That(Property(Property(preview, "Recipe"), "ResultMonsterId"), Is.EqualTo(resultId));
                Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before), "Preview must preserve both parents.");
                Assert.That(Field(Call(master, "GetFusionResult", parentId, parentId), "monsterId"), Is.EqualTo(resultId));

                object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, instanceA, instanceB, master, false);
                Assert.That(Property(result, "CanFuse"), Is.True);
                object child = Property(result, "CreatedMonster");
                Assert.That(Field(child, "MonsterId"), Is.EqualTo(resultId));
                Assert.That(Field(child, "Level"), Is.EqualTo(1));
                Assert.That(Property(child, "TotalPlusValue"), Is.EqualTo(5));
                Assert.That(Field(Property(result, "ResultMonsterData"), "classRank"), Is.EqualTo(resultClass));
                Assert.That(Field(Property(result, "ResultMonsterData"), "raceId"), Is.EqualTo("spirit"));
                Assert.That(((IEnumerable)Property(profile, "OwnedMonsters")).Cast<object>().ToArray(), Is.EqualTo(new[] { child }));
                Assert.That(Field(child, "InstanceId"), Is.Not.EqualTo(instanceA).And.Not.EqualTo(instanceB));

                object restored = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { Call(profile, "ToSaveData", 1) });
                object savedChild = ((IEnumerable)Property(restored, "OwnedMonsters")).Cast<object>().Single();
                Assert.That(Field(savedChild, "MonsterId"), Is.EqualTo(resultId), "The evolved species must survive save restoration.");
                Assert.That(Property(savedChild, "TotalPlusValue"), Is.EqualTo(5));
            }
        }

        [TestCase(Lili, Lilia, Lilia)]
        [TestCase(Lilia, Lili, Lilia)]
        [TestCase(Lili, "monster_flare_drake", Lilia)]
        [TestCase("monster_flare_drake", Lili, "monster_flare_drake")]
        [TestCase(Lilia, "monster_dragon_whelp", Lilia)]
        [TestCase("monster_dragon_whelp", Lilia, "monster_flare_drake")]
        public void FairyMixedClassFusionUsesParentOneRaceAtTheHigherClass(string firstId, string secondId, string expectedId)
        {
            object profile = NewProfile();
            object first = AddMaxLevelParent(profile, firstId);
            object second = AddMaxLevelParent(profile, secondId);
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "CanFuse"), Is.True);
            Assert.That(Field(Property(result, "CreatedMonster"), "MonsterId"), Is.EqualTo(expectedId));
            Assert.That(Property(Property(result, "Recipe"), "RecipeType").ToString(), Is.EqualTo("ParentRaceHighestClass"));
            Assert.That(Property(Property(result, "Recipe"), "IgnoreOrder"), Is.False);
            Assert.That(Field(Property(result, "ResultMonsterData"), "classRank"), Is.EqualTo(2));
            Assert.That(Field(Call(master, "GetFusionResult", firstId, secondId), "monsterId"), Is.EqualTo(expectedId));
        }

        [TestCase(Lili)]
        [TestCase(Lilia)]
        [TestCase(Liliana)]
        public void FairyEvolutionStillRequiresTwoMaximumLevelIndividuals(string parentId)
        {
            object profile = NewProfile();
            object first = AddMaxLevelParent(profile, parentId);
            object second = AddMaxLevelParent(profile, parentId);
            first.GetType().GetField("Level", Any).SetValue(first, (int)Field(first, "Level") - 1);
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "Status").ToString(), Is.EqualTo("ParentLevelTooLow"));
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before));
        }

        private static object NewProfile()
        {
            object save = StaticCall("Save.PlayerSaveData", "CreateDefault");
            object profile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { save });
            ((IList)Property(profile, "OwnedMonsters")).Clear();
            return profile;
        }

        private object AddMaxLevelParent(object profile, string monsterId, int plusValue = 0)
        {
            object data = Call(master, "GetMonsterData", monsterId);
            Assert.That(data, Is.Not.Null, "Missing master data for " + monsterId);
            // Select the MonsterDataSO overload, avoiding a direct Assembly-CSharp reference.
            int maxLevel = (int)GameType("Data.MonsterLevelService").GetMethods(Any)
                .Single(method => method.Name == "GetMaxLevel" && method.GetParameters()[0].ParameterType.IsInstanceOfType(data))
                .Invoke(null, new[] { data });
            return Call(profile, "AddOwnedMonster", monsterId, maxLevel, plusValue, false);
        }
    }
}
