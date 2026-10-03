using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed partial class GuardianPresentationTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ArtRoot = "UI/GuardiansReborn/";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, InstanceFlags).Invoke(target, args);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method, StaticFlags).Invoke(null, args);
        private static object P(object target, string name) => target.GetType().GetProperty(name, InstanceFlags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, InstanceFlags).SetValue(target, value);
        private static object F(object target, string name) => target.GetType().GetField(name, InstanceFlags).GetValue(target);
        private static void F(object target, string name, object value) => target.GetType().GetField(name, InstanceFlags).SetValue(target, value);
        private GameObject owner, canvas;
        private object game, master, profile, simulator, controller;
        private object oldGame, oldMaster, oldSave;
        private SceneSetup[] scenes;
        private IList Enemies => (IList)F(simulator, "activeEnemyRuntimes");
        private static string ArtName(string id) => char.ToUpperInvariant(id[0]) + id.Substring(1);

        [SetUp] public void Setup()
        {
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            owner = new GameObject("GuardianPresentationTest"); owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, null);
            Call(master, "Initialize");
            S("Data.GuardianTrialSession", "Reset");
            var save = S("Save.PlayerSaveData", "CreateDefault");
            F(save, "HighestFloor", 60); F(save, "HasCompletedTutorial", true); F(save, "TutorialStepId", "Complete");
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            Set(game, "PlayerProfile", profile);
            Call(game, "SetCurrentFloor", 31);
            foreach (string id in new[] { "monster_flare_drake", "monster_rock_golem", "monster_apprentice_mage", "monster_apprentice_swordsman", "monster_chibi_gear" })
            {
                object owned = Call(profile, "AddOwnedMonster", id, 20, 0, false);
                ((IList)P(profile, "PartyMonsterInstanceIds")).Add(F(owned, "InstanceId"));
            }
        }

        [TearDown] public void Cleanup()
        {
            S("Data.GuardianTrialSession", "Reset");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null, oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null, oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null, oldSave);
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }

        private void BuildBattle(string id)
        {
            S("Data.GuardianService", "GrantCore", profile, id);
            S("Data.GuardianService", "Birth", profile, id);
            F(S("Data.GuardianService", "Owned", profile, id), "Level", 10);
            S("Data.GuardianService", "Equip", profile, id);
            simulator = owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator, "Setup", 31);
            while (Enemies.Count < 3) Call(simulator, "QueueEnemySpawn", false);
            for (int i = 0; i < Enemies.Count; i++)
            {
                F(F(Enemies[i], "Stats"), "MaxHp", 100000);
                F(F(Enemies[i], "Stats"), "CurrentHp", 100000);
                F(Enemies[i], "PositionAnchor", new Vector2(.65f + .10f * i, .40f + .10f * i));
            }
            InitializeBattlePresentation(31);
        }

        private void InitializeBattlePresentation(int floor)
        {
            var machine = owner.AddComponent(T("Battle.BattleStateMachine")); F(machine, "simulator", simulator);
            var hud = owner.AddComponent(T("Battle.BattleHudController")); F(machine, "hudController", hud);
            controller = owner.AddComponent(T("Battle.BattleSceneController"));
            F(controller, "stateMachine", machine); F(controller, "currentFloor", floor);
            canvas = new GameObject("GuardianPresentationCapture", typeof(RectTransform), typeof(Canvas));
            F(controller, "minimalCanvasRoot", canvas);
            Call(controller, "EnsureMinimalCanvas");
            Call(controller, "ApplyCombatantVisuals", floor);
            Call(controller, "ApplyBackdropForFloor", floor);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Call(controller, "UpdateGuardianBattlePanel");
            Call(controller, "UpdateWaveHud", simulator);
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void NewArtContainsPortraitEightIdleEightAttackAndFourEffectFrames(string id)
        {
            var monster = S("Data.GuardianService", "Monster", id);
            string name = ArtName(id);
            Assert.That(F(monster, "portraitResourcePath"), Is.EqualTo(ArtRoot + name + "Portrait"));
            Assert.That(F(monster, "battleIdleResourcePath"), Is.EqualTo(ArtRoot + "Animation/" + name + "/Idle"));
            Assert.That(F(monster, "battleAttackResourcePath"), Is.EqualTo(ArtRoot + "Animation/" + name + "/Attack"));
            var portrait = Resources.Load<Sprite>(ArtRoot + name + "Portrait");
            Assert.That(portrait, Is.Not.Null, "The dedicated high-resolution image2 portrait must be imported.");
            Assert.That(Mathf.Max(portrait.rect.width, portrait.rect.height), Is.GreaterThanOrEqualTo(512));
            foreach (string pose in new[] { "Idle", "Attack" })
            {
                var frames = ((IList)S("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster)).Cast<Sprite>().ToArray();
                Assert.That(frames.Length, Is.EqualTo(8), id + " " + pose);
                Assert.That(frames.Distinct().Count(), Is.EqualTo(8), "Every drawing must load as a separate frame.");
                for (int i = 0; i < frames.Length; i++)
                {
                    Assert.That(frames[i], Is.EqualTo(Resources.Load<Sprite>(ArtRoot + "Animation/" + name + "/" + pose + "_" + i)));
                    bool wide = pose == "Attack" && (id == "seiryu" || id == "suzaku");
                    Assert.That(frames[i].rect.size, Is.EqualTo(new Vector2(wide ? 960 : 384, 384)));
                    Assert.That(frames[i].pivot.x, Is.EqualTo(192f).Within(.001f));
                    Assert.That(frames[i].pivot.y, Is.EqualTo(192f).Within(.001f));
                    Rect opaque = OpaqueSpriteBounds(frames[i]);
                    Assert.That(opaque.xMin, Is.GreaterThan(0f), "Keep transparent padding on the left edge.");
                    Assert.That(opaque.yMin, Is.GreaterThan(0f), "Keep transparent padding on the bottom edge.");
                    Assert.That(opaque.xMax, Is.LessThan(frames[i].rect.width), "The breath tip must not touch the canvas edge.");
                    Assert.That(opaque.yMax, Is.LessThan(frames[i].rect.height), "Keep transparent padding on the top edge.");
                }
            }
            var effects = Enumerable.Range(0, 4).Select(i => Resources.Load<Sprite>(ArtRoot + "Effects/" + name + "_" + i)).ToArray();
            Assert.That(effects.All(sprite => sprite != null), Is.True);
            Assert.That(effects.Distinct().Count(), Is.EqualTo(4));
            Assert.That(effects.All(sprite => sprite.rect.size == new Vector2(384, 384)), Is.True);
            Assert.That(Resources.Load<AudioClip>("Audio/SE/GuardiansReborn/" + id), Is.Not.Null);
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void RegisteredGuardianDrawingKeepsLargeStableRectangleAcrossAllFrames(string id)
        {
            BuildBattle(id);
            var monster = S("Data.GuardianService", "Monster", id);
            foreach (object pose in Enum.GetValues(T("Battle.BattleVisualPose")))
                Assert.That(S("Battle.BattleSceneController", "ResolveMonsterPreviewScale", monster, pose), Is.EqualTo(2.05f));
            var image = ((IList)F(controller, "allyPreviewImages"))[5] as Image;
            Assert.That(image, Is.Not.Null);
            Assert.That(image.rectTransform.sizeDelta.x, Is.EqualTo(451f).Within(.001f));
            Assert.That(image.rectTransform.sizeDelta.y, Is.EqualTo(451f).Within(.001f));
            Vector2 anchor = image.rectTransform.anchorMin;
            foreach (string pose in new[] { "Idle", "Attack" })
            {
                foreach (Sprite sprite in (IList)S("Battle.BattleVisualResolver", "ResolveMonster" + pose + "Sprites", monster))
                {
                    image.sprite = sprite;
                    S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, new Vector2(451f, 451f));
                    Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(451f * sprite.rect.width / 384f, 451f)));
                    Assert.That(image.rectTransform.pivot.x, Is.EqualTo(192f / sprite.rect.width).Within(.00001f));
                    Assert.That(image.rectTransform.pivot.y, Is.EqualTo(.5f).Within(.00001f));
                    Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                    Assert.That(image.rectTransform.anchorMin, Is.EqualTo(anchor));
                    Assert.That(image.preserveAspect, Is.True);
                }
            }
            Call(simulator, "TickGuardianSkill", 4f);
            Call(controller, "PlayGuardianDivinePresentation");
            Call(controller, "UpdateGuardianDivinePresentation", .25f);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Call(controller, "UpdateGuardianBattlePanel");
            Assert.That(((Image)F(controller, "guardianHalo")).gameObject.activeSelf, Is.True);
            Capture("battle-" + id);
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void DirectedAttackBodyKeepsRegistrationAndEveryFrameInsideCanvas(string id)
        {
            BuildBattle(id);
            PrepareCaptureCanvas();
            Call(controller, "UpdateGuardianDivinePresentation", 0f);
            var image = (Image)((IList)F(controller, "allyPreviewImages"))[5];
            Vector3 originalBodyCenter = BodyCenterWorld(image);
            var monster = S("Data.GuardianService", "Monster", id);
            var idle = ((IList)S("Battle.BattleVisualResolver", "ResolveMonsterIdleSprites", monster)).Cast<Sprite>().ToArray();
            var attacks = Enumerable.Range(0, 8).Select(i => Resources.Load<Sprite>(ArtRoot + "Animation/" + ArtName(id) + "/AttackBody_" + i)).ToArray();
            Assert.That(attacks.All(sprite => sprite != null), Is.True);
            foreach (var sprite in idle.Concat(attacks))
            {
                image.sprite = sprite;
                S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, new Vector2(451f, 451f));
                Assert.That(Vector3.Distance(BodyCenterWorld(image), originalBodyCenter), Is.LessThan(.01f), sprite.name + " shifted the body.");
                AssertOpaqueInsideCanvas(image);
            }
            Assert.That(attacks.All(sprite => sprite.rect.width == 384f), Is.True, "Live attack poses contain only the body; projectile travel is a separate layer.");
            image.sprite = attacks[4];
            S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, new Vector2(451f, 451f));
            Capture("battle-" + id + "-body");
            // Returning to idle must retain the same body anchor.
            image.sprite = idle[0];
            S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, new Vector2(451f, 451f));
            Assert.That(Vector3.Distance(BodyCenterWorld(image), originalBodyCenter), Is.LessThan(.01f));
            Assert.That(image.rectTransform.sizeDelta.x, Is.EqualTo(451f));
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void TrialGuardianIsAllySizedAndItsRealAttackPlaysInPlace(string id)
        {
            S("Data.GuardianTrialSession", "Begin", profile, id);
            simulator = owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator, "Setup", 30);
            Call(simulator, "TickPreparation", .5f);
            InitializeBattlePresentation(30); PrepareCaptureCanvas();
            var hits = ConnectDirectedHitPresentation();
            var image = (Image)((IList)F(controller, "enemyPreviewImages"))[0];
            var anchor = image.rectTransform.anchorMin;
            Assert.That(image.rectTransform.sizeDelta.y, Is.EqualTo(451f).Within(.001f));
            for(int i=0;i<100 && !hits.Cast<object>().Any(h=>(bool)P(h,"TargetIsPlayer"));i++)
            {
                Call(simulator,"TickGuardianTrial",.05f);
                Call(simulator,"TickUnitMovement",.05f);
                Call(simulator,"TickEnemyAttackers",.05f);
            }
            Assert.That(hits.Cast<object>().Any(h=>(bool)P(h,"TargetIsPlayer")),Is.True,"A real timer-driven enemy attack must dispatch its presentation.");
            Call(controller,"UpdatePreviewLayoutFromSimulator",simulator);
            Assert.That(image.sprite.name,Does.StartWith("Attack"));
            Assert.That(image.rectTransform.anchorMin,Is.EqualTo(anchor));
            Assert.That(image.rectTransform.anchoredPosition,Is.EqualTo(Vector2.zero));
            Assert.That(image.rectTransform.sizeDelta.y,Is.EqualTo(451f).Within(.001f));
            AssertOpaqueInsideCanvas(image);
            Capture("trial-"+id+"-stationary-attack");
            if(id=="seiryu" || id=="suzaku")
            {
                Assert.That(DirectedAttacks.Count,Is.GreaterThan(0));
                StepDirected(.30f);
                Capture("trial-"+id+"-projectile");
                StepDirected(.5f);
                Capture("trial-"+id+"-impact");
            }
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void TrialGuardianUsesBodyRegistrationWhenFacingLeft(string id)
        {
            Assert.That(S("Data.GuardianTrialSession", "Begin", profile, id), Is.True);
            simulator = owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator, "Setup", 30);
            Call(simulator, "QueueEnemySpawn", false);
            F(Enemies[0], "PositionAnchor", new Vector2(.80f, .62f));
            InitializeBattlePresentation(30);
            PrepareCaptureCanvas();
            var image = (Image)((IList)F(controller, "enemyPreviewImages"))[0];
            Assert.That(image.rectTransform.localScale.x, Is.LessThan(0f), "Trial guardians use the left-facing mirror of the same registered drawing.");
            Vector3 bodyCenter = BodyCenterWorld(image);
            float bodySize = image.rectTransform.sizeDelta.y;
            ((IList)F(controller, "enemyAttackVisualRemainings"))[0] = .4f;
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That(image.sprite.rect.width, Is.EqualTo(384));
            Assert.That(image.rectTransform.sizeDelta.x, Is.EqualTo(bodySize).Within(.001f));
            Assert.That(Vector3.Distance(BodyCenterWorld(image), bodyCenter), Is.LessThan(.01f));
            var monster = S("Data.GuardianService", "Monster", id);
            var frames = Enumerable.Range(0, 8).Select(i => Resources.Load<Sprite>(ArtRoot + "Animation/" + ArtName(id) + "/AttackBody_" + i)).ToArray();
            Assert.That(frames.All(sprite => sprite != null), Is.True);
            foreach (var sprite in frames)
            {
                image.sprite = sprite;
                S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, Vector2.one * bodySize);
                Assert.That(Vector3.Distance(BodyCenterWorld(image), bodyCenter), Is.LessThan(.01f));
                AssertOpaqueInsideCanvas(image);
            }
            image.sprite = frames[4];
            S("Battle.BattleSceneController", "ApplyGuardianPreviewVisualLayout", image, Vector2.one * bodySize);
            Capture("trial-" + id + "-body");
        }

        private static Rect OpaqueSpriteBounds(Sprite sprite)
        {
            var pixels = sprite.texture.GetPixels32();
            int textureWidth = sprite.texture.width;
            int offsetX = Mathf.RoundToInt(sprite.rect.x), offsetY = Mathf.RoundToInt(sprite.rect.y);
            int width = Mathf.RoundToInt(sprite.rect.width), height = Mathf.RoundToInt(sprite.rect.height);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Match the imported artwork's registration check. Alpha <= 8
                    // is the almost invisible antialias fringe, not visible flame.
                    if (pixels[(offsetY + y) * textureWidth + offsetX + x].a <= 8) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
            Assert.That(maxX, Is.GreaterThanOrEqualTo(0), sprite.name + " is empty.");
            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static Vector3 BodyCenterWorld(Image image)
        {
            var sprite = image.sprite;
            var rect = image.rectTransform;
            return rect.TransformPoint(new Vector3(
                (192f / sprite.rect.width - rect.pivot.x) * rect.rect.width,
                (192f / sprite.rect.height - rect.pivot.y) * rect.rect.height, 0f));
        }

        private void AssertOpaqueInsideCanvas(Image image)
        {
            var sprite = image.sprite;
            Rect opaque = OpaqueSpriteBounds(sprite);
            var rect = image.rectTransform;
            var canvasRect = (RectTransform)canvas.transform;
            foreach (Vector2 pixel in new[] {
                new Vector2(opaque.xMin, opaque.yMin), new Vector2(opaque.xMax, opaque.yMin),
                new Vector2(opaque.xMax, opaque.yMax), new Vector2(opaque.xMin, opaque.yMax) })
            {
                var local = new Vector3((pixel.x / sprite.rect.width - rect.pivot.x) * rect.rect.width,
                    (pixel.y / sprite.rect.height - rect.pivot.y) * rect.rect.height, 0f);
                Vector3 inCanvas = canvasRect.InverseTransformPoint(rect.TransformPoint(local));
                Assert.That(inCanvas.x, Is.InRange(canvasRect.rect.xMin - 1f, canvasRect.rect.xMax + 1f), sprite.name + " visible pixels clipped horizontally.");
                Assert.That(inCanvas.y, Is.InRange(canvasRect.rect.yMin - 1f, canvasRect.rect.yMax + 1f), sprite.name + " visible pixels clipped vertically.");
            }
        }

        private void PrepareCaptureCanvas()
        {
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(1080, 2340); rect.position = Vector3.zero;
            Canvas.ForceUpdateCanvases();
        }

        [Test] public void GrowthAnnouncementNamesOnlyTheGuardianThatActuallyLeveled()
        {
            BuildBattle("seiryu");
            Assert.That(S("Data.GuardianService", "GrantCore", profile, "suzaku"), Is.True);
            Assert.That(S("Data.GuardianService", "Birth", profile, "suzaku"), Is.True);
            var suzaku = S("Data.GuardianService", "Owned", profile, "suzaku");
            F(suzaku, "Level", 20);
            var seiryu = S("Data.GuardianService", "Owned", profile, "seiryu");
            F(seiryu, "Level", 9);
            F(seiryu, "Exp", (int)S("Data.GuardianService", "RequiredExp", 9) - 1);
            Call(controller, "ApplyPartyMonsterExp", profile, 2);
            Assert.That(S("Data.GuardianService", "Level", profile, "seiryu"), Is.EqualTo(10));
            Assert.That(S("Data.GuardianService", "Level", profile, "suzaku"), Is.EqualTo(20));
            Assert.That((string)F(controller, "activeBattleAnnouncementText"), Does.Contain("青龍").And.Contain("Lv.10").And.Not.Contain("朱雀"));
            Call(controller, "UpdateGuardianBattlePanel");
            Assert.That(((Text)F(controller, "guardianBattleLabel")).text, Does.Contain("青龍 Lv.10"));
            S("Data.GuardianService", "Equip", profile, "suzaku");
            Call(simulator, "Setup", 31);
            Call(controller, "UpdateGuardianBattlePanel");
            Assert.That(((Text)F(controller, "guardianBattleLabel")).text, Does.Contain("朱雀 Lv.20"));
        }

        [TestCase("byakko")] [TestCase("genbu")]
        public void DivineEffectUsesCastSnapshotAndStopsAfterItsDuration(string id)
        {
            BuildBattle(id);
            if (id != "genbu") foreach (var enemy in Enemies) F(F(enemy, "Stats"), "CurrentHp", 1);
            Call(simulator, "TickGuardianSkill", 4f);
            var expected = id == "genbu"
                ? (Vector2)T("Battle.BattleSceneController").GetProperty("GuardianPresentationAnchor", StaticFlags).GetValue(null)
                : (Vector2)S("Battle.BattleSceneController", "MapBattlefieldAnchor", P(simulator, "LastGuardianSkillTargetAnchor"));
            if (id != "genbu")
            {
                // Remove every defeated target before starting the visual, then move
                // the surviving/new enemy elsewhere. Neither can retarget the cast.
                int dead;
                while ((dead = (int)Call(simulator, "FindDefeatedEnemyIndex")) >= 0)
                    Call(simulator, "AdvanceEncounterAfterEnemyDefeat", dead);
                if (Enemies.Count == 0) Call(simulator, "QueueEnemySpawn", false);
                foreach (var enemy in Enemies) F(enemy, "PositionAnchor", new Vector2(.95f, .95f));
            }
            Call(controller, "PlayGuardianDivinePresentation");
            Call(controller, "UpdateGuardianDivinePresentation", .2f);
            var effect = (Image)F(controller, "guardianDivineEffect");
            Assert.That(effect.gameObject.activeSelf, Is.True);
            Assert.That(effect.rectTransform.anchorMin, Is.EqualTo(expected));
            Assert.That(effect.raycastTarget, Is.False);
            Call(controller, "UpdateGuardianDivinePresentation", .7f);
            Assert.That(effect.gameObject.activeSelf, Is.False, "A completed cast cannot leave a stale overlay.");
        }

        private void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("WITCHTOWER_GUARDIAN_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            PrepareCaptureCanvas();
            var rect = (RectTransform)canvas.transform;
            string capturePath = Path.Combine(folder, name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", StaticFlags).Invoke(null,
                new object[] { rect, 1179, 2556, capturePath });
        }
    }
}
