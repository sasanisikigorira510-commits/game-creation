using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AngelMonsterMasterDataTests
    {
        private const string Lumie = "monster_apprentice_angel_lumie";
        private const string Lumiel = "monster_holy_wing_angel_lumiel";
        private const string Seraphina = "monster_archangel_seraphina";
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
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(item, arguments);
        private static object StaticCall(string type, string name, params object[] arguments) => GameType(type).GetMethods(Any)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(null, arguments);

        [SetUp]
        public void SetUp()
        {
            randomState = UnityEngine.Random.state;
            previousMaster = GameType("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            owner = new GameObject("AngelMasterDataTestOwner");
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

        [TestCase(Lumie, "見習い天使ルミエ", 1, 31, 1)]
        [TestCase(Lumiel, "聖翼天使ルミエル", 2, 32, 1)]
        [TestCase(Seraphina, "大天使セラフィナ", 3, 33, 2)]
        public void ProductionRootRegistersUniqueLightMagicMeleeAngels(string id, string name, int rank, int number, int targets)
        {
            var root = Resources.Load<ScriptableObject>("MasterData/MasterDataRoot");
            Assert.That(root, Is.Not.Null);
            object[] monsters = ((IEnumerable)Field(root, "monsterDataList")).Cast<object>().Where(item => item != null).ToArray();
            object[] matches = monsters.Where(item => (string)Field(item, "monsterId") == id).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "The production root must register the new asset exactly once.");
            object data = Monster(id);
            Assert.That(data, Is.SameAs(matches[0]), "The runtime manager must use the imported root asset.");
            Assert.That(monsters.Count(item => (int)Field(item, "encyclopediaNumber") == number), Is.EqualTo(1));
            Assert.That(Field(data, "monsterName"), Is.EqualTo(name));
            Assert.That(Field(data, "encyclopediaNumber"), Is.EqualTo(number));
            Assert.That(Field(data, "classRank"), Is.EqualTo(rank));
            Assert.That(Field(data, "raceId"), Is.EqualTo("angel"));
            Assert.That(Field(data, "element").ToString(), Is.EqualTo("Light"));
            Assert.That(Field(data, "rangeType").ToString(), Is.EqualTo("Melee"));
            Assert.That(Field(data, "damageType").ToString(), Is.EqualTo("Magic"));
            Assert.That(Field(data, "normalAttackTargetCount"), Is.EqualTo(targets));
            Assert.That(Field(data, "fusionExclusive"), Is.False);
            if (targets == 2) Assert.That((string)Field(data, "description"), Does.Contain("二体"));
        }

        [TestCase(Lumie, 1.18f, .96f)]
        [TestCase(Lumiel, 1.22f, 1.04f)]
        [TestCase(Seraphina, 1.26f, 1.18f)]
        public void ImportedBalanceAndGrowthSurviveUnityValidation(string id, float range, float scale)
        {
            object data = Monster(id);
            int rank = (int)Field(data, "classRank");
            float[][] stats = { new[] { 56f, 6f, 18f, 10f, 15f, 1.02f }, new[] { 88f, 10f, 34f, 18f, 30f, .96f }, new[] { 126f, 16f, 56f, 28f, 46f, .90f } };
            float[][] growth = { new[] { .94f, .65f, 1.22f, .86f, 1.30f, 1f }, new[] { 1f, .70f, 1.40f, .93f, 1.46f, .94f }, new[] { 1.05f, .74f, 1.58f, 1f, 1.58f, .88f } };
            string[] statNames = { "maxHp", "attack", "magicAttack", "defense", "magicDefense", "attackSpeed" };
            for (int i = 0; i < statNames.Length; i++)
            {
                Assert.That(Convert.ToSingle(Field(Field(data, "baseStats"), statNames[i])), Is.EqualTo(stats[rank - 1][i]).Within(.0001f), id + " / " + statNames[i]);
                Assert.That((float)Field(Field(data, "levelGrowth"), statNames[i] + "Coefficient"), Is.EqualTo(growth[rank - 1][i]).Within(.0001f));
            }
            Assert.That((float)Field(data, "attackRange"), Is.EqualTo(range).Within(.0001f));
            Assert.That((float)Field(data, "battleVisualScale"), Is.EqualTo(scale).Within(.0001f));

            // OnValidate and battle stat creation both derive plus growth from the serialized coefficients.
            object serialized = Field(data, "plusGrowth");
            object resolved = StaticCall("MasterData.MonsterGrowthUtility", "ResolvePlusGrowth", data);
            Assert.That(StaticCall("MasterData.MonsterGrowthUtility", "AreEqual", serialized, resolved), Is.True);
            string fairy = rank == 1 ? "monster_bud_fairy_lili" : rank == 2 ? "monster_flower_fairy_lilia" : "monster_flower_crown_spirit_liliana";
            Assert.That(StaticCall("MasterData.MonsterGrowthUtility", "AreEqual", serialized, Field(Monster(fairy), "plusGrowth")), Is.True);
        }

        [TestCase(Lumie, "lumie")]
        [TestCase(Lumiel, "lumiel")]
        [TestCase(Seraphina, "seraphina")]
        public void Image2PortraitsResolveFromAngelResourcesAndAllPosesFaceRight(string id, string key)
        {
            object data = Monster(id);
            foreach (var visual in new[] { ("portraitResourcePath", "FamilyMonsterCards"), ("illustrationResourcePath", "FamilyMonsters") })
            {
                string path = visual.Item2 + "/Angel/" + key;
                Assert.That(Field(data, visual.Item1), Is.EqualTo(path));
                var sprite = (Sprite)StaticCall("Battle.BattleVisualResolver", "LoadSprite", path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(path)), "Use the configured image2 sprite without a visual fallback.");
                string assetPath = "Assets/Resources/" + path + ".png";
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(assetPath));
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(sprite.rect.width, Is.EqualTo(1254));
                Assert.That(sprite.rect.height, Is.EqualTo(1254));
            }

            foreach (string pose in new[] { "Idle", "Move", "Attack" })
            {
                Assert.That(Field(data, "battle" + pose + "ResourcePath"), Is.EqualTo("MonsterBattle/mon_" + key + "_" + pose.ToLowerInvariant()));
                object poseValue = Enum.Parse(GameType("Battle.BattleVisualPose"), pose);
                Assert.That(StaticCall("Battle.BattleVisualResolver", "ResolveMonsterFacing", data, poseValue).ToString(), Is.EqualTo("Right"));
            }
        }

        [TestCase(Lumie)]
        [TestCase(Lumiel)]
        [TestCase(Seraphina)]
        public void SavedAngelBuildsRetainMagicStatsAndGrowAtTheFrontLine(string id)
        {
            object data = Monster(id);
            object save = StaticCall("Save.PlayerSaveData", "CreateDefault");
            object profile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { save });
            ((IList)Property(profile, "OwnedMonsters")).Clear();
            object owned = Call(profile, "AddOwnedMonster", id, 10, 3, false);
            string instanceId = (string)Field(owned, "InstanceId");
            object before = StaticCall("Battle.MonsterBattleStatsFactory", "Create", profile, owned, data);
            object restoredSave = JsonUtility.FromJson(JsonUtility.ToJson(Call(profile, "ToSaveData", 1)), GameType("Save.PlayerSaveData"));
            object restoredProfile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { restoredSave });
            object restoredOwned = Call(restoredProfile, "GetOwnedMonster", instanceId);
            Assert.That(restoredOwned, Is.Not.Null);
            Assert.That(Field(restoredOwned, "MonsterId"), Is.EqualTo(id));
            Assert.That(Field(restoredOwned, "Level"), Is.EqualTo(10));
            Assert.That(Property(restoredOwned, "TotalPlusValue"), Is.EqualTo(3));
            object grown = StaticCall("Battle.MonsterBattleStatsFactory", "Create", restoredProfile, restoredOwned, Monster(id));
            foreach (string stat in new[] { "MaxHp", "Attack", "Wisdom", "Defense", "MagicDefense", "AttackSpeed" })
                Assert.That(Field(grown, stat), Is.EqualTo(Field(before, stat)), id + " / " + stat + " must survive save restoration.");
            Assert.That(Field(grown, "CurrentHp"), Is.EqualTo(Field(grown, "MaxHp")));
            restoredOwned.GetType().GetField("Level", Any).SetValue(restoredOwned, 1);
            object levelOne = StaticCall("Battle.MonsterBattleStatsFactory", "Create", restoredProfile, restoredOwned, data);
            foreach (string stat in new[] { "MaxHp", "Wisdom", "Defense", "MagicDefense" })
                Assert.That((int)Field(grown, stat), Is.GreaterThan((int)Field(levelOne, stat)), id + " / " + stat);
        }

        private object Monster(string id)
        {
            object data = Call(master, "GetMonsterData", id);
            Assert.That(data, Is.Not.Null, "Missing runtime master data for " + id);
            return data;
        }
    }
}
