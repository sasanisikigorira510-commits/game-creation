using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AllyMovementSpeedTests
    {
        private static Type Simulator => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Battle.BattleSimulator", true);

        private static float Call(string name, params object[] args) => (float)Simulator
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static UnityEngine.Object Monster(string id)
        {
            var data = AssetDatabase.LoadMainAssetAtPath("Assets/MasterData/Monster/" + id + ".asset");
            Assert.That(data, Is.Not.Null, id);
            return data;
        }

        [TestCase("monster_rock_golem")]
        [TestCase("monster_apprentice_swordsman")]
        [TestCase("monster_ore_giant_garm")]
        [TestCase("monster_holy_armor_leon")]
        [TestCase("monster_sword_saint_alvarez")]
        public void MeleeKeepsNormalPaceWithoutStackedCatchUp(string id)
        {
            var data = Monster(id);
            float speed = Call("ResolveAllyMoveSpeed", data);
            Assert.That(speed, Is.EqualTo(0.46f * 0.34f).Within(0.000001f));

            // Movement remains time-based and still reaches a distant target.
            var move = Simulator.GetMethod("MoveRuntimeTowards", BindingFlags.Static | BindingFlags.NonPublic);
            Vector2 position = Vector2.zero;
            for (int tick = 0; tick < 20; tick++)
            {
                object[] args = { position, Vector2.right, speed, 0.05f, false };
                position = (Vector2)move.Invoke(null, args);
            }
            Assert.That(position.x, Is.EqualTo(speed).Within(0.00001f));
            for (int tick = 0; tick < 140; tick++)
            {
                object[] args = { position, Vector2.right, speed, 0.05f, false };
                position = (Vector2)move.Invoke(null, args);
            }
            Assert.That(position, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void EveryMonsterUsesOnlyItsNormalRangeTypeSpeed()
        {
            string[] assets = AssetDatabase.FindAssets("t:MonsterDataSO", new[] { "Assets/MasterData/Monster" });
            Assert.That(assets.Length, Is.GreaterThan(10));
            foreach (string guid in assets)
            {
                var data = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                bool melee = data.GetType().GetField("rangeType").GetValue(data).ToString() == "Melee";
                Assert.That(Call("ResolveAllyMoveSpeed", data),
                    Is.EqualTo((melee ? 0.46f : 0.26f) * 0.34f).Within(0.000001f), data.name);
            }
        }

        [Test]
        public void RangedMovementIsUnchanged()
        {
            var data = Monster("monster_apprentice_mage");
            float speed = Call("ResolveAllyMoveSpeed", data);
            Assert.That(speed, Is.EqualTo(0.26f * 0.34f).Within(0.000001f));
        }
    }
}
