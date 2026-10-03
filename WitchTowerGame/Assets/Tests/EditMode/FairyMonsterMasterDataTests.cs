using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class FairyMonsterMasterDataTests
    {
        private const string Lili = "monster_bud_fairy_lili";
        private const string Lilia = "monster_flower_fairy_lilia";
        private const string Liliana = "monster_flower_crown_spirit_liliana";
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
            owner = new GameObject("FairyMasterDataTestOwner");
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

        [TestCase(false)]
        [TestCase(true)]
        public void ClientSummonPoolsContainAllThreeFairiesAtTheirClasses(bool includePaidClass4)
        {
            // Exercise both the initialized manager and the production Resources fallback.
            foreach (bool useRootFallback in new[] { false, true })
            {
                GameType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, useRootFallback ? null : master);
                try
                {
                    object[] pool = ((IEnumerable)StaticCall("Home.GachaPanelController", "CollectSummonPool", includePaidClass4))
                        .Cast<object>().ToArray();
                    foreach (var expected in new[] { (Lili, 1), (Lilia, 2), (Liliana, 3) })
                    {
                        object[] matches = pool.Where(data => (string)Field(data, "monsterId") == expected.Item1).ToArray();
                        Assert.That(matches.Length, Is.EqualTo(1), expected.Item1 + " must be available exactly once in the actual client pool.");
                        Assert.That(Field(matches[0], "classRank"), Is.EqualTo(expected.Item2));
                        Assert.That(Field(matches[0], "fusionExclusive"), Is.False);
                    }
                }
                finally
                {
                    GameType("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
                }
            }
        }

        [TestCase(Lili)]
        [TestCase(Lilia)]
        [TestCase(Liliana)]
        public void FairyPortraitsIllustrationsAndFacingResolveThroughProductionVisuals(string monsterId)
        {
            object data = Monster(monsterId);
            foreach (string field in new[] { "portraitResourcePath", "illustrationResourcePath" })
            {
                string path = (string)Field(data, field);
                Assert.That(path, Is.Not.Null.And.Not.Empty);
                var sprite = (Sprite)StaticCall("Battle.BattleVisualResolver", "LoadSprite", path);
                Assert.That(sprite, Is.Not.Null, monsterId + " / " + field);
                Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(path)), "The configured image2 asset must resolve without a generated fallback.");
                Assert.That(sprite.rect.width, Is.GreaterThan(0));
                Assert.That(sprite.rect.height, Is.GreaterThan(0));
            }

            foreach (string pose in new[] { "Idle", "Move", "Attack" })
            {
                object poseValue = Enum.Parse(GameType("Battle.BattleVisualPose"), pose);
                object facing = StaticCall("Battle.BattleVisualResolver", "ResolveMonsterFacing", data, poseValue);
                Assert.That(facing.ToString(), Is.EqualTo("Right"), monsterId + " / " + pose);
            }
        }

        [TestCase(Lili, 1)]
        [TestCase(Lilia, 1)]
        [TestCase(Liliana, 2)]
        public void SavedFairyBuildsGrowingRangedMagicStatsWithConsistentPlusGrowth(string monsterId, int targetCount)
        {
            object data = Monster(monsterId);
            object resolvedPlus = StaticCall("MasterData.MonsterGrowthUtility", "ResolvePlusGrowth", data);
            Assert.That(StaticCall("MasterData.MonsterGrowthUtility", "AreEqual", Field(data, "plusGrowth"), resolvedPlus),
                Is.True, "Serialized plus growth must match the production utility after import.");

            object save = StaticCall("Save.PlayerSaveData", "CreateDefault");
            object profile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { save });
            ((IList)Property(profile, "OwnedMonsters")).Clear();
            object owned = Call(profile, "AddOwnedMonster", monsterId, 10, 3, false);
            string instanceId = (string)Field(owned, "InstanceId");
            object beforeSaveStats = StaticCall("Battle.MonsterBattleStatsFactory", "Create", profile, owned, data);

            // A JSON round trip avoids sharing the owned object through ToSaveData's list copy.
            string json = JsonUtility.ToJson(Call(profile, "ToSaveData", 1));
            object restoredSave = JsonUtility.FromJson(json, GameType("Save.PlayerSaveData"));
            object restoredProfile = Activator.CreateInstance(GameType("Data.PlayerProfile"), new[] { restoredSave });
            object restoredOwned = Call(restoredProfile, "GetOwnedMonster", instanceId);
            Assert.That(restoredOwned, Is.Not.Null);
            Assert.That(Field(restoredOwned, "MonsterId"), Is.EqualTo(monsterId));
            Assert.That(Field(restoredOwned, "Level"), Is.EqualTo(10));
            Assert.That(Property(restoredOwned, "TotalPlusValue"), Is.EqualTo(3));

            object restoredData = Monster((string)Field(restoredOwned, "MonsterId"));
            Assert.That(Field(restoredData, "raceId"), Is.EqualTo("spirit"));
            Assert.That(Field(restoredData, "rangeType").ToString(), Is.EqualTo("Ranged"));
            Assert.That(Field(restoredData, "damageType").ToString(), Is.EqualTo("Magic"));
            Assert.That(Field(restoredData, "normalAttackTargetCount"), Is.EqualTo(targetCount));

            object grownStats = StaticCall("Battle.MonsterBattleStatsFactory", "Create", restoredProfile, restoredOwned, restoredData);
            Assert.That(grownStats, Is.Not.Null);
            foreach (string stat in new[] { "MaxHp", "Wisdom", "MagicDefense" })
            {
                Assert.That((int)Field(grownStats, stat), Is.GreaterThan(0), monsterId + " / " + stat);
                Assert.That(Field(grownStats, stat), Is.EqualTo(Field(beforeSaveStats, stat)), "Save restoration must retain individual and plus bonuses.");
            }
            Assert.That(Field(grownStats, "CurrentHp"), Is.EqualTo(Field(grownStats, "MaxHp")));
            Assert.That((float)Field(grownStats, "AttackSpeed"), Is.GreaterThan(0));

            // Compare the same saved individual with the same plus/account bonuses.
            restoredOwned.GetType().GetField("Level", Any).SetValue(restoredOwned, 1);
            object levelOneStats = StaticCall("Battle.MonsterBattleStatsFactory", "Create", restoredProfile, restoredOwned, restoredData);
            foreach (string stat in new[] { "MaxHp", "Wisdom", "MagicDefense" })
            {
                Assert.That((int)Field(grownStats, stat), Is.GreaterThan((int)Field(levelOneStats, stat)),
                    monsterId + " / " + stat + " must grow between levels 1 and 10.");
            }
        }

        private object Monster(string monsterId)
        {
            object data = Call(master, "GetMonsterData", monsterId);
            Assert.That(data, Is.Not.Null, "Missing runtime master data for " + monsterId);
            return data;
        }
    }
}
