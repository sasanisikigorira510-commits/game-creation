using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class AngelMonsterBattleTests
    {
        private const string Lumie = "monster_apprentice_angel_lumie";
        private const string Lumiel = "monster_holy_wing_angel_lumiel";
        private const string Seraphina = "monster_archangel_seraphina";
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private GameObject masterOwner;
        private object master;
        private object previousMaster;
        private UnityEngine.Random.State previousRandomState;

        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object owner, string name) => owner.GetType().GetField(name, InstanceFlags).GetValue(owner);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType()
            .GetMethod(name, InstanceFlags).Invoke(owner, args);
        private static object Static(string type, string name, params object[] args) => Runtime(type)
            .GetMethod(name, StaticFlags).Invoke(null, args);

        [SetUp]
        public void SetUp()
        {
            previousRandomState = UnityEngine.Random.state;
            var singleton = Runtime("Managers.MasterDataManager").GetProperty("Instance");
            previousMaster = singleton.GetValue(null);
            masterOwner = new GameObject("AngelBattleTestMasterData");
            masterOwner.SetActive(false);
            master = masterOwner.AddComponent(Runtime("Managers.MasterDataManager"));
            singleton.SetValue(null, master);
            Invoke(master, "Initialize");
        }

        [TearDown]
        public void TearDown()
        {
            Runtime("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, previousMaster);
            UnityEngine.Object.DestroyImmediate(masterOwner);
            UnityEngine.Random.state = previousRandomState;
        }

        private object Monster(string id)
        {
            object monster = Invoke(master, "GetMonsterData", id);
            Assert.That(monster, Is.Not.Null, id + " must be registered in the production master root.");
            return monster;
        }

        private object Enemy(string id)
        {
            object enemy = Static("Data.BattleDungeonCatalog", "CreateEnemyDataForMonsterAtGlobalFloor", 1, master, id);
            Assert.That(enemy, Is.Not.Null, "The ordinary enemy factory must support the registered angel.");
            return enemy;
        }

        [TestCase(Lumie, "lumie", 1, 1)]
        [TestCase(Lumiel, "lumiel", 2, 1)]
        [TestCase(Seraphina, "seraphina", 3, 2)]
        public void RegisteredAngelsUseTheirOwnRightFacingClipsForAlliesAndEnemies(string id, string key, int classRank, int targetCount)
        {
            object monster = Monster(id);
            Assert.That(Field(monster, "raceId"), Is.EqualTo("angel"));
            Assert.That(Field(monster, "classRank"), Is.EqualTo(classRank));
            Assert.That(Field(monster, "element").ToString(), Is.EqualTo("Light"));
            Assert.That(Field(monster, "rangeType").ToString(), Is.EqualTo("Melee"));
            Assert.That(Field(monster, "damageType").ToString(), Is.EqualTo("Magic"));
            Assert.That(Field(monster, "normalAttackTargetCount"), Is.EqualTo(targetCount));
            float attackRange = (float)Static("Battle.BattleAttackRangeResolver", "ResolveMonsterAttackRange", monster);
            Assert.That(attackRange, Is.GreaterThan(0f).And.LessThan(2f), "Holy slashes must retain the melee combat range.");

            object enemy = Enemy(id);
            try
            {
                Assert.That(Field(enemy, "damageType").ToString(), Is.EqualTo("Magic"));
                Assert.That(Field(enemy, "normalAttackTargetCount"), Is.EqualTo(targetCount));
                foreach (string pose in new[] { "Idle", "Move", "Attack" })
                {
                    object poseValue = Enum.Parse(Runtime("Battle.BattleVisualPose"), pose);
                    object sourceFacing = Static("Battle.BattleVisualResolver", "ResolveMonsterFacing", monster, poseValue);
                    object enemySourceFacing = Static("Battle.BattleVisualResolver", "ResolveEnemyFacing", enemy, poseValue);
                    Assert.That(sourceFacing.ToString(), Is.EqualTo("Right"), id + " / " + pose);
                    Assert.That(enemySourceFacing, Is.EqualTo(sourceFacing));
                    object leftFacing = Enum.Parse(Runtime("MasterData.BattleFacingDirection"), "Left");
                    Assert.That(Static("Battle.BattleSceneController", "ResolveFacingScale", sourceFacing, sourceFacing), Is.EqualTo(Vector3.one));
                    Assert.That(Static("Battle.BattleSceneController", "ResolveFacingScale", enemySourceFacing, leftFacing),
                        Is.EqualTo(new Vector3(-1f, 1f, 1f)), "Enemy art must mirror once to face the player.");

                    string path = "MonsterBattle/mon_" + key + "_" + pose.ToLowerInvariant();
                    Assert.That(Field(monster, "battle" + pose + "ResourcePath"), Is.EqualTo(path));
                    var frames = (IList)Static("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster);
                    var enemyFrames = (IList)Static("Battle.BattleVisualResolver", "ResolveEnemy" + pose + "Sprites", enemy);
                    Assert.That(frames.Count, Is.EqualTo(4), id + " / " + pose);
                    Assert.That(enemyFrames.Count, Is.EqualTo(4), "Enemy visuals must not fall back to a single portrait.");
                    for (int index = 0; index < 4; index++)
                    {
                        var sprite = (Sprite)frames[index];
                        Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(path + "_" + index)));
                        Assert.That(enemyFrames[index], Is.SameAs(sprite));
                        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512f, 512f)));
                        Assert.That(sprite.pivot, Is.EqualTo(new Vector2(256f, 256f)));
                        Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
                        Assert.That(sprite.texture.isReadable, Is.True);
                        Color32[] pixels = sprite.texture.GetPixels32();
                        Assert.That(pixels.Any(pixel => pixel.a == 255), Is.True);
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
            finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)enemy); }
        }

        [TestCase(Lumie)]
        [TestCase(Lumiel)]
        [TestCase(Seraphina)]
        public void RealPreviewModesKeepTheAuthoredBootRootFixedAcrossAllTwelveFrames(string id)
        {
            object monster = Monster(id);
            object enemy = Enemy(id);
            var owner = new GameObject("AngelAuthoredRootLayout", typeof(RectTransform), typeof(Image));
            try
            {
                var image = owner.GetComponent<Image>();
                Vector2 baseSize = new Vector2(200f, 200f);
                Vector2 motionOffset = new Vector2(3f, 5f);
                float baselineRatio = (float)Runtime("Battle.BattleSceneController").GetField("PreviewVisualBaselineRatio", StaticFlags).GetRawConstantValue();
                Vector2 expectedFoot = motionOffset + new Vector2(0f, -baseSize.y * baselineRatio);
                Vector2? expectedSize = null;
                Vector2? expectedPosition = null;

                foreach (string pose in new[] { "Idle", "Move", "Attack" })
                {
                    object poseValue = Enum.Parse(Runtime("Battle.BattleVisualPose"), pose);
                    object frames = Static("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster);
                    object allyMode = Static("Battle.BattleSceneController", "ResolveAllyPreviewMeasurementMode", monster, poseValue);
                    object enemyMode = Static("Battle.BattleSceneController", "ResolveEnemyPreviewMeasurementMode", enemy, poseValue);
                    // Class-3 Attack selects humanoid body measurement in production.
                    // Pass the real modes so this catches any loss of the authored-anchor override.
                    foreach (object mode in new[] { allyMode, enemyMode })
                    {
                        foreach (Sprite frame in (IEnumerable)frames)
                        {
                            image.sprite = frame;
                            foreach (float facing in new[] { 1f, -1f })
                            {
                                image.rectTransform.localScale = new Vector3(facing, 1f, 1f);
                                Static("Battle.BattleSceneController", "ApplyPreviewVisualLayout", image, baseSize, motionOffset, frames, mode);
                                if (!expectedSize.HasValue)
                                {
                                    expectedSize = image.rectTransform.sizeDelta;
                                    expectedPosition = image.rectTransform.anchoredPosition;
                                }
                                Assert.That(Vector2.Distance(image.rectTransform.sizeDelta, expectedSize.Value), Is.LessThan(0.0001f), id + " / " + pose + " / " + mode);
                                Assert.That(Vector2.Distance(image.rectTransform.anchoredPosition, expectedPosition.Value), Is.LessThan(0.0001f), "Changing boot width, wing span or sword reach must not recenter the body.");
                                // Asset anchor (256,464) from top becomes (256,48) from bottom.
                                // Its x offset is zero even when the enemy sprite is mirrored.
                                Vector2 footInImage = new Vector2(0f, (48f / 512f - 0.5f) * image.rectTransform.sizeDelta.y);
                                Vector2 actualFoot = image.rectTransform.anchoredPosition + footInImage;
                                Assert.That(Vector2.Distance(actualFoot, expectedFoot), Is.LessThan(0.0001f), "Layout must use the authored root rather than each frame's silhouette.");
                                Assert.That(image.rectTransform.sizeDelta.x, Is.InRange(1f, 440f));
                            }
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)enemy);
            }
        }

        [TestCase("lumie")]
        [TestCase("lumiel")]
        [TestCase("seraphina")]
        public void StandaloneHolyEffectsKeepTheirFourAuthoredColorFrames(string key)
        {
            string path = "BattleEffects/Monster/fx_" + key + "_attack";
            var frames = (IList)Static("Battle.BattleVisualResolver", "LoadSpriteFrames", path);
            Assert.That(frames.Count, Is.EqualTo(4));
            for (int index = 0; index < 4; index++)
            {
                var sprite = (Sprite)frames[index];
                Assert.That(sprite, Is.SameAs(Resources.Load<Sprite>(path + "_" + index)));
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512f, 512f)));
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(sprite.texture.isReadable, Is.False, "The effect must not require a runtime CPU-readable copy.");

                // Decode the original PNG without mutating the imported effect or its importer.
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    string pngPath = Path.Combine(Application.dataPath, "Resources", path + "_" + index + ".png");
                    Assert.That(decoded.LoadImage(File.ReadAllBytes(pngPath)), Is.True);
                    Color32[] pixels = decoded.GetPixels32();
                    Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True, "The standalone effect must retain alpha transparency.");
                    Assert.That(pixels.Any(pixel => pixel.a == 255), Is.True);
                    if (index == 2)
                    {
                        Assert.That(pixels.Any(p => p.a >= 200 && p.r > 220 && p.g > 220 && p.b > 220), Is.True, "Peak holy light must retain its white core.");
                        Assert.That(pixels.Any(p => p.a >= 200 && p.b > 160 && p.b > p.r + 15), Is.True, "Peak holy light must retain its authored blue pixels.");
                        Assert.That(pixels.Any(p => p.a >= 200 && p.r > 180 && p.g > 140 && p.b < p.g - 15), Is.True, "Peak holy light must retain its authored gold pixels.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(decoded); }
            }
        }

        [TestCase(Lumie, "lumie", false)]
        [TestCase(Lumie, "lumie", true)]
        [TestCase(Lumiel, "lumiel", false)]
        [TestCase(Lumiel, "lumiel", true)]
        [TestCase(Seraphina, "seraphina", false)]
        [TestCase(Seraphina, "seraphina", true)]
        public void HolySlashWaitsForTheSwordStrikeThenPlaysOnceAtItsTarget(string id, string key, bool targetIsPlayer)
        {
            object monster = Monster(id);
            var owner = new GameObject("IsolatedAngelHolySlash");
            owner.SetActive(false);
            var controller = owner.AddComponent(Runtime("Battle.BattleSceneController"));
            var effectRoot = new GameObject("HolySlashEffects", typeof(RectTransform));
            effectRoot.transform.SetParent(owner.transform, false);
            controller.GetType().GetField("rangedEffectRoot", InstanceFlags).SetValue(controller, effectRoot);
            try
            {
                object hit = Activator.CreateInstance(Runtime("Battle.BattleHitInfo"));
                object definition = Invoke(controller, "ResolveMonsterAttackEffect", hit, monster);
                Assert.That(definition, Is.Not.Null);
                string resourcePath = "BattleEffects/Monster/fx_" + key + "_attack";
                Assert.That(Field(definition, "ResourcePath"), Is.EqualTo(resourcePath));
                Assert.That(Field(definition, "Placement").ToString(), Is.EqualTo("TargetBurst"));
                Assert.That(Field(definition, "PreserveSourceAppearance"), Is.True);
                Assert.That(Field(definition, "MirrorForEnemies"), Is.True);
                var authoredFrames = (IList)Static("Battle.BattleVisualResolver", "LoadSpriteFrames", resourcePath);
                Assert.That(authoredFrames.Count, Is.EqualTo(4));
                Vector2 targetPosition = new Vector2(targetIsPlayer ? -300f : 300f, 17f);
                Assert.That(Invoke(controller, "SpawnMonsterAttackEffect", definition, Vector2.zero, targetPosition, targetIsPlayer, monster, true), Is.True);
                var active = (IList)Field(controller, "activeRangedAttackEffects");
                Assert.That(active.Count, Is.EqualTo(1), "Authored holy slashes must not receive duplicate class echoes or afterglows.");
                object effect = active[0];
                var image = (Image)Field(effect, "Image");
                Assert.That((float)Field(effect, "StartDelay"), Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That((float)Field(effect, "Duration"), Is.EqualTo(0.40f).Within(0.0001f));
                Assert.That(Field(effect, "BaseColor"), Is.EqualTo(Color.white));
                Assert.That(Field(effect, "GlowStrength"), Is.EqualTo(0f));
                Assert.That(Field(effect, "PulseStrength"), Is.EqualTo(0f));
                Assert.That(Field(effect, "UseArcMovement"), Is.False);
                Assert.That(image.rectTransform.localScale.x, Is.EqualTo(targetIsPlayer ? -1f : 1f));
                Vector2 offset = (Vector2)Field(definition, "TargetOffset");
                Vector2 expectedPosition = targetPosition + new Vector2(offset.x * (targetIsPlayer ? -1f : 1f), offset.y);

                Invoke(controller, "UpdateRangedAttackEffects", 0.369f);
                Assert.That(image.enabled, Is.False, "The sword's windup must not show a premature holy slash.");
                Invoke(controller, "UpdateRangedAttackEffects", 0.002f);
                Assert.That(image.enabled, Is.True);
                for (int index = 0; index < 4; index++)
                {
                    if (index > 0) Invoke(controller, "UpdateRangedAttackEffects", 0.10f);
                    Assert.That(image.sprite, Is.SameAs(authoredFrames[index]));
                    Assert.That(Vector2.Distance(image.rectTransform.anchoredPosition, expectedPosition), Is.LessThan(0.0001f));
                    Assert.That(image.color.r, Is.EqualTo(1f));
                    Assert.That(image.color.g, Is.EqualTo(1f));
                    Assert.That(image.color.b, Is.EqualTo(1f), "Rendering must preserve the blue/gold source colors instead of tinting them.");
                    Assert.That(image.rectTransform.localScale.x, Is.EqualTo(targetIsPlayer ? -1f : 1f));
                }
                Invoke(controller, "UpdateRangedAttackEffects", 0.10f);
                Assert.That(active.Count, Is.Zero, "The standalone four-frame slash must finish after its 0.40-second duration.");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [TestCase(Lumie)]
        [TestCase(Lumiel)]
        [TestCase(Seraphina)]
        public void MeleeMagicHitPresentationWaitsForTheAuthoredHolySlash(string id)
        {
            object monster = Monster(id);
            float allyDelay = (float)Static("Battle.BattleSimulator", "ResolvePlayerPresentationDelay", monster);
            Assert.That(allyDelay, Is.EqualTo(0.77f).Within(0.0001f), "Melee classification must not bypass the 0.37 + 0.40 second authored slash timing.");
            object enemy = Enemy(id);
            try
            {
                float enemyDelay = (float)Static("Battle.BattleSimulator", "ResolveEnemyPresentationDelay", enemy);
                Assert.That(enemyDelay, Is.EqualTo(allyDelay).Within(0.0001f), "Enemy holy slashes must use the same impact presentation timing.");
            }
            finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)enemy); }
        }
    }
}
