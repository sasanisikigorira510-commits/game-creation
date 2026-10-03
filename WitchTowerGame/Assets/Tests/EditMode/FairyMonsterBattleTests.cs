using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class FairyMonsterBattleTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object owner, string name) => owner.GetType().GetField(name, InstanceFlags).GetValue(owner);
        private static object Invoke(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, InstanceFlags).Invoke(owner, args);
        private static object Static(string type, string name, params object[] args) =>
            Runtime(type).GetMethod(name, StaticFlags).Invoke(null, args);

        private static object Monster(string id)
        {
            var root = Resources.Load("MasterData/MasterDataRoot", Runtime("MasterData.MasterDataRoot"));
            Assert.That(root, Is.Not.Null);
            var monsters = (IEnumerable)Field(root, "monsterDataList");
            var monster = monsters.Cast<object>().SingleOrDefault(m => (string)Field(m, "monsterId") == id);
            Assert.That(monster, Is.Not.Null, "The fairy must be in the master list used by battles.");
            return monster;
        }

        [TestCase("monster_bud_fairy_lili", "lili", 1)]
        [TestCase("monster_flower_fairy_lilia", "lilia", 1)]
        [TestCase("monster_flower_crown_spirit_liliana", "liliana", 2)]
        public void RegisteredFairiesUseTheirOwnCompleteRightFacingClips(string id, string key, int targetCount)
        {
            object monster = Monster(id);
            Assert.That(Field(monster, "normalAttackTargetCount"), Is.EqualTo(targetCount));
            foreach (string pose in new[] { "Idle", "Move", "Attack" })
            {
                Assert.That(Field(monster, "battle" + pose + "Facing").ToString(), Is.EqualTo("Right"));
                string path = "MonsterBattle/mon_" + key + "_" + pose.ToLowerInvariant();
                Assert.That(Field(monster, "battle" + pose + "ResourcePath"), Is.EqualTo(path));
                var frames = (IList)Static("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster);
                Assert.That(frames.Count, Is.EqualTo(4), id + " / " + pose);
                for (int index = 0; index < 4; index++)
                {
                    var sprite = (Sprite)frames[index];
                    Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(path + "_" + index)));
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512, 512)));
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(256, 256)));
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
                    Assert.That(sprite.texture.isReadable, Is.True);
                    Color32[] pixels = sprite.texture.GetPixels32();
                    Assert.That(pixels.Any(p => p.a == 255), Is.True);
                    for (int edge = 0; edge < 512; edge++)
                    {
                        Assert.That(pixels[edge].a, Is.Zero);
                        Assert.That(pixels[511 * 512 + edge].a, Is.Zero);
                        Assert.That(pixels[edge * 512].a, Is.Zero);
                        Assert.That(pixels[edge * 512 + 511].a, Is.Zero);
                    }
                }
            }
        }

        [TestCase("monster_bud_fairy_lili")]
        [TestCase("monster_flower_fairy_lilia")]
        [TestCase("monster_flower_crown_spirit_liliana")]
        public void WingsAndExtendedWandsDoNotMoveOrResizeTheAuthoredBody(string id)
        {
            object monster = Monster(id);
            var go = new GameObject("IsolatedFairyLayout", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = go.GetComponent<Image>();
                Type controller = Runtime("Battle.BattleSceneController");
                object fullSprite = Enum.Parse(controller.GetNestedType("PreviewMeasurementMode", BindingFlags.NonPublic), "FullSprite");
                Vector2? expectedSize = null, expectedPosition = null;
                foreach (string pose in new[] { "Idle", "Move", "Attack" })
                {
                    object frames = Static("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster);
                    foreach (Sprite frame in (IEnumerable)frames)
                    {
                        image.sprite = frame;
                        foreach (float facing in new[] { 1f, -1f })
                        {
                            image.rectTransform.localScale = new Vector3(facing, 1f, 1f);
                            controller.GetMethod("ApplyPreviewVisualLayout", StaticFlags).Invoke(null,
                                new[] { image, (object)new Vector2(200, 200), new Vector2(3, 5), frames, fullSprite });
                            if (!expectedSize.HasValue)
                            {
                                expectedSize = image.rectTransform.sizeDelta;
                                expectedPosition = image.rectTransform.anchoredPosition;
                            }
                            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(expectedSize.Value), id + " / " + frame.name);
                            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(expectedPosition.Value), id + " / " + frame.name);
                            Assert.That(image.rectTransform.sizeDelta.x, Is.InRange(1f, 440f));
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase("monster_bud_fairy_lili", "lili", false)]
        [TestCase("monster_bud_fairy_lili", "lili", true)]
        [TestCase("monster_flower_fairy_lilia", "lilia", false)]
        [TestCase("monster_flower_fairy_lilia", "lilia", true)]
        [TestCase("monster_flower_crown_spirit_liliana", "liliana", false)]
        [TestCase("monster_flower_crown_spirit_liliana", "liliana", true)]
        public void AuthoredProjectileWaitsForCastThenAnimatesAndFacesItsTarget(string id, string key, bool targetIsPlayer)
        {
            object monster = Monster(id);
            var owner = new GameObject("IsolatedFairyProjectile");
            owner.SetActive(false);
            var controller = owner.AddComponent(Runtime("Battle.BattleSceneController"));
            var root = new GameObject("FairyEffects", typeof(RectTransform));
            root.transform.SetParent(owner.transform, false);
            controller.GetType().GetField("rangedEffectRoot", InstanceFlags).SetValue(controller, root);
            try
            {
                object hit = Activator.CreateInstance(Runtime("Battle.BattleHitInfo"));
                object definition = Invoke(controller, "ResolveMonsterAttackEffect", hit, monster);
                Assert.That(definition, Is.Not.Null);
                string resourcePath = "BattleEffects/Monster/fx_" + key + "_attack";
                Assert.That(Field(definition, "ResourcePath"), Is.EqualTo(resourcePath));
                Assert.That(Field(definition, "Placement").ToString(), Is.EqualTo("Projectile"));
                Assert.That(Field(definition, "UseCasterCenter"), Is.True);
                var authoredFrames = (IList)Static("Battle.BattleVisualResolver", "LoadSpriteFrames", resourcePath);
                Assert.That(authoredFrames.Count, Is.EqualTo(4));
                float endX = targetIsPlayer ? -300f : 300f;
                Assert.That(Invoke(controller, "SpawnMonsterAttackEffect", definition, Vector2.zero,
                    new Vector2(endX, 0f), targetIsPlayer, monster, true), Is.True);
                var active = (IList)Field(controller, "activeRangedAttackEffects");
                Assert.That(active.Count, Is.EqualTo(1), "The source contains its own petals; class echoes must not duplicate or recolor it.");
                object effect = active[0];
                var image = (Image)Field(effect, "Image");
                float castDelay = (float)Field(effect, "StartDelay");
                float flight = (float)Field(effect, "Duration");
                Assert.That(castDelay, Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(flight, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(Field(effect, "BaseColor"), Is.EqualTo(Color.white));
                Assert.That(Field(effect, "GlowStrength"), Is.EqualTo(0f));
                Assert.That(image.rectTransform.localScale.x, Is.EqualTo(targetIsPlayer ? -1f : 1f));
                Assert.That((float)Field(effect, "BaseSize"), Is.InRange(60f, 140f));

                Invoke(controller, "UpdateRangedAttackEffects", 0.369f);
                Assert.That(image.enabled, Is.False, "Wind-up must not show the projectile.");
                Invoke(controller, "UpdateRangedAttackEffects", 0.002f);
                Assert.That(image.enabled, Is.True);
                Assert.That(image.sprite, Is.SameAs(authoredFrames[0]));
                Invoke(controller, "UpdateRangedAttackEffects", 0.11f);
                Assert.That(image.sprite, Is.SameAs(authoredFrames[1]));
                Invoke(controller, "UpdateRangedAttackEffects", 0.1f);
                Assert.That(image.sprite, Is.SameAs(authoredFrames[2]));
                Invoke(controller, "UpdateRangedAttackEffects", 0.1f);
                Assert.That(image.sprite, Is.SameAs(authoredFrames[3]));
                Assert.That(image.color, Is.EqualTo(Color.white));
                Invoke(controller, "UpdateRangedAttackEffects", 0.1f);
                Assert.That(active.Count, Is.Zero, "The four-frame effect must finish after its 0.4-second flight.");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [TestCase("monster_bud_fairy_lili")]
        [TestCase("monster_flower_fairy_lilia")]
        [TestCase("monster_flower_crown_spirit_liliana")]
        public void SimulatorHitReactionOccursWhenTheCastProjectileArrives(string id)
        {
            object monster = Monster(id);
            float impactDelay = (float)Static("Battle.BattleSimulator", "ResolvePlayerPresentationDelay", monster);
            Assert.That(impactDelay, Is.EqualTo(0.77f).Within(0.0001f),
                "Hit flash must wait for the 0.37-second wind-up and 0.40-second nature projectile.");
        }
    }
}
