using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AngelMonsterFusionTests
    {
        private const string Lumie = "monster_apprentice_angel_lumie";
        private const string Lumiel = "monster_holy_wing_angel_lumiel";
        private const string Seraphina = "monster_archangel_seraphina";
        private const string Michael = "monster_seraph_michael";
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
            owner = new GameObject("AngelFusionTestMasterData");
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

        [Test]
        public void CatalogAndRuntimeMasterCoverEveryRecipeIncludingTheAngelLineage()
        {
            object[] definitions = ((IEnumerable)StaticCall("Data.MonsterFusionCatalog", "GetMonsterDefinitions")).Cast<object>().ToArray();
            string[] catalogIds = definitions.Select(definition => (string)Property(definition, "MonsterId")).ToArray();
            string[] runtimeIds = ((IEnumerable)Call(master, "GetAllMonsterData")).Cast<object>()
                .Select(data => (string)Field(data, "monsterId")).ToArray();
            Assert.That(catalogIds, Has.Length.EqualTo(40));
            Assert.That(catalogIds.Distinct().Count(), Is.EqualTo(catalogIds.Length));
            Assert.That(runtimeIds, Is.EquivalentTo(catalogIds), "Every catalog species must be registered in the playable master root.");

            object[] recipes = ((IEnumerable)StaticCall("Data.MonsterFusionCatalog", "GetRecipes")).Cast<object>().ToArray();
            Assert.That(recipes, Has.Length.EqualTo(33));
            Assert.That(recipes.Count(recipe => Property(recipe, "RecipeType").ToString() == "ClassUp"), Is.EqualTo(16));
            Assert.That(recipes.Count(recipe => Property(recipe, "RecipeType").ToString() == "Special"), Is.EqualTo(17));
            foreach (object recipe in recipes)
            {
                string first = (string)Property(recipe, "ParentMonsterIdA");
                string second = (string)Property(recipe, "ParentMonsterIdB");
                string result = (string)Property(recipe, "ResultMonsterId");
                Assert.That(Call(master, "GetMonsterData", first), Is.Not.Null);
                Assert.That(Call(master, "GetMonsterData", second), Is.Not.Null);
                Assert.That(Field(Call(master, "GetFusionResult", first, second), "monsterId"), Is.EqualTo(result));
                if ((bool)Property(recipe, "IgnoreOrder"))
                    Assert.That(Field(Call(master, "GetFusionResult", second, first), "monsterId"), Is.EqualTo(result));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClientSummonPoolsIncludeTheAngelLineageAndKeepMichaelPaidOnly(bool paid)
        {
            object[] pool = ((IEnumerable)StaticCall("Home.GachaPanelController", "CollectSummonPool", paid)).Cast<object>().ToArray();
            string[] ids = pool.Select(data => (string)Field(data, "monsterId")).ToArray();
            foreach (string id in new[] { Lumie, Lumiel, Seraphina })
            {
                Assert.That(ids.Count(candidate => candidate == id), Is.EqualTo(1));
                Assert.That(Field(pool.Single(data => (string)Field(data, "monsterId") == id), "fusionExclusive"), Is.False);
            }
            Assert.That(ids.Contains(Michael), Is.EqualTo(paid));
            Assert.That(Field(Call(master, "GetMonsterData", Michael), "classRank"), Is.EqualTo(4));
            Assert.That(Field(Call(master, "GetMonsterData", Michael), "fusionExclusive"), Is.True);
        }

        [TestCase(Lumie, Lumiel, 2)]
        [TestCase(Lumiel, Seraphina, 3)]
        [TestCase(Seraphina, Michael, 4)]
        public void AngelEvolutionConsumesDistinctParentsInEitherOrderAndPersistsAtLevelOne(string parentId, string resultId, int resultClass)
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

                object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, instanceA, instanceB, master, false);
                Assert.That(Property(result, "CanFuse"), Is.True);
                object child = Property(result, "CreatedMonster");
                object data = Property(result, "ResultMonsterData");
                Assert.That(Field(child, "MonsterId"), Is.EqualTo(resultId));
                Assert.That(Field(child, "Level"), Is.EqualTo(1));
                Assert.That(Property(child, "TotalPlusValue"), Is.EqualTo(5));
                Assert.That(Field(data, "classRank"), Is.EqualTo(resultClass));
                Assert.That(Field(data, "raceId"), Is.EqualTo("angel"));
                Assert.That(Field(data, "fusionExclusive"), Is.EqualTo(resultId == Michael));
                Assert.That(((IEnumerable)Property(profile, "OwnedMonsters")).Cast<object>().ToArray(), Is.EqualTo(new[] { child }));
                Assert.That(Call(profile, "GetOwnedMonster", instanceA), Is.Null);
                Assert.That(Call(profile, "GetOwnedMonster", instanceB), Is.Null);
                Assert.That(Field(child, "InstanceId"), Is.Not.EqualTo(instanceA).And.Not.EqualTo(instanceB));

                object restored = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { Call(profile, "ToSaveData", 1) });
                object savedChild = ((IEnumerable)Property(restored, "OwnedMonsters")).Cast<object>().Single();
                Assert.That(Field(savedChild, "MonsterId"), Is.EqualTo(resultId));
                Assert.That(Field(savedChild, "Level"), Is.EqualTo(1));
                Assert.That(Property(savedChild, "TotalPlusValue"), Is.EqualTo(5));
            }
        }

        [TestCase(Lumie, "monster_dragon_whelp", Lumie, 1)]
        [TestCase("monster_dragon_whelp", Lumie, "monster_dragon_whelp", 1)]
        [TestCase(Lumie, "monster_flare_drake", Lumiel, 2)]
        [TestCase("monster_flare_drake", Lumie, "monster_flare_drake", 2)]
        [TestCase(Lumiel, "monster_abyss_dragon", Seraphina, 3)]
        [TestCase("monster_abyss_dragon", Lumiel, "monster_abyss_dragon", 3)]
        [TestCase(Lumie, Seraphina, Seraphina, 3)]
        [TestCase(Seraphina, Lumie, Seraphina, 3)]
        public void NormalFusionUsesParentOneRaceAtTheHigherClass(string firstId, string secondId, string resultId, int resultClass)
        {
            AssertFusion(firstId, secondId, resultId, resultClass, "ParentRaceHighestClass");
        }

        [TestCase(Michael, Lumie, Michael)]
        [TestCase(Lumie, Michael, Michael)]
        public void MichaelRetainsExistingClassFourFallbackBehavior(string firstId, string secondId, string resultId)
        {
            AssertFusion(firstId, secondId, resultId, 4, "HighestClassParent");
            Assert.That(Field(Call(master, "GetMonsterData", Michael), "fusionExclusive"), Is.True);
        }

        [TestCase(Lumie, 20, false)]
        [TestCase(Lumie, 20, true)]
        [TestCase(Lumiel, 40, false)]
        [TestCase(Lumiel, 40, true)]
        [TestCase(Seraphina, 60, false)]
        [TestCase(Seraphina, 60, true)]
        public void EvolutionRequiresBothParentsAtTheirClassLevelLimit(string parentId, int maxLevel, bool lowerSecond)
        {
            object profile = NewProfile();
            object first = AddMaxLevelParent(profile, parentId);
            object second = AddMaxLevelParent(profile, parentId);
            Assert.That(Field(first, "Level"), Is.EqualTo(maxLevel));
            object belowLimit = lowerSecond ? second : first;
            belowLimit.GetType().GetField("Level", Any).SetValue(belowLimit, maxLevel - 1);
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "Status").ToString(), Is.EqualTo("ParentLevelTooLow"));
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before));
        }

        [Test]
        public void MichaelEvolutionDoesNotConsumeALockedParentOrOneIndividualTwice()
        {
            object profile = NewProfile();
            object first = AddMaxLevelParent(profile, Seraphina);
            object second = AddMaxLevelParent(profile, Seraphina);
            object instance = Field(first, "InstanceId");
            object same = StaticCall("Data.MonsterFusionService", "Fuse", profile, instance, instance, master, false);
            Assert.That(Property(same, "Status").ToString(), Is.EqualTo("SameMonsterInstance"));
            second.GetType().GetField("IsLocked", Any).SetValue(second, true);
            string before = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            object locked = StaticCall("Data.MonsterFusionService", "Fuse", profile, instance, Field(second, "InstanceId"), master, false);
            Assert.That(Property(locked, "Status").ToString(), Is.EqualTo("LockedParentBlocked"));
            Assert.That(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), Is.EqualTo(before));
        }

        private void AssertFusion(string firstId, string secondId, string resultId, int resultClass, string recipeType)
        {
            object profile = NewProfile();
            object first = AddMaxLevelParent(profile, firstId);
            object second = AddMaxLevelParent(profile, secondId);
            object result = StaticCall("Data.MonsterFusionService", "Fuse", profile, Field(first, "InstanceId"), Field(second, "InstanceId"), master, false);
            Assert.That(Property(result, "CanFuse"), Is.True);
            Assert.That(Field(Property(result, "CreatedMonster"), "MonsterId"), Is.EqualTo(resultId));
            Assert.That(Field(Property(result, "CreatedMonster"), "Level"), Is.EqualTo(1));
            Assert.That(Field(Property(result, "ResultMonsterData"), "classRank"), Is.EqualTo(resultClass));
            Assert.That(Property(Property(result, "Recipe"), "RecipeType").ToString(), Is.EqualTo(recipeType));
            Assert.That(Property(Property(result, "Recipe"), "IgnoreOrder"), Is.False);
            Assert.That(Field(Call(master, "GetFusionResult", firstId, secondId), "monsterId"), Is.EqualTo(resultId));
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
            int maxLevel = (int)GameType("Data.MonsterLevelService").GetMethods(Any)
                .Single(method => method.Name == "GetMaxLevel" && method.GetParameters()[0].ParameterType.IsInstanceOfType(data))
                .Invoke(null, new[] { data });
            return Call(profile, "AddOwnedMonster", monsterId, maxLevel, plusValue, false);
        }
    }
}
