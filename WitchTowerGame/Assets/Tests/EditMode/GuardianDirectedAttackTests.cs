using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed partial class GuardianPresentationTests
    {
        private IList DirectedAttacks => (IList)F(controller, "activeGuardianDirectedAttacks");
        private object GuardianRuntime => ((IList)F(simulator, "activeAllyRuntimes")).Cast<object>()
            .Single(ally => (int)F(ally, "SlotIndex") == 5);

        private IList ConnectDirectedHitPresentation(bool genericDamageNumbers = false)
        {
            F(controller, "showFloatingDamageNumbers", genericDamageNumbers);
            var hitType = T("Battle.BattleHitInfo");
            var hits = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(hitType));
            var hitEvent = simulator.GetType().GetEvent("HitResolved");
            hitEvent.AddEventHandler(simulator, Delegate.CreateDelegate(hitEvent.EventHandlerType,
                hits, hits.GetType().GetMethod("Add")));
            hitEvent.AddEventHandler(simulator, Delegate.CreateDelegate(hitEvent.EventHandlerType,
                controller, controller.GetType().GetMethod("HandleBattleHitResolved", InstanceFlags)));
            var defeated = simulator.GetType().GetEvent("EnemyDefeated");
            defeated.AddEventHandler(simulator, Delegate.CreateDelegate(defeated.EventHandlerType,
                controller, controller.GetType().GetMethod("HandleEnemyDefeated", InstanceFlags)));
            return hits;
        }

        private object StartGuardianNormalAttack(int target = 0)
        {
            F(GuardianRuntime, "TargetEnemyRuntimeId", F(Enemies[target], "RuntimeId"));
            // Isolate a single contact for timing/identity checks. Suzaku's real
            // three-target normal attack is covered separately below.
            var original = (UnityEngine.Object)F(GuardianRuntime, "Data");
            var oneTarget = UnityEngine.Object.Instantiate(original);
            try
            {
                F(oneTarget, "normalAttackTargetCount", 1);
                F(GuardianRuntime, "Data", oneTarget);
                Call(simulator, "PerformAttackOnEnemy", GuardianRuntime, false, 5);
            }
            finally
            {
                F(GuardianRuntime, "Data", original);
                UnityEngine.Object.DestroyImmediate(oneTarget);
            }
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That(DirectedAttacks.Count, Is.GreaterThan(0), "The real hit event must launch a directed attack.");
            var body = (Image)((IList)F(controller, "allyPreviewImages"))[5];
            Assert.That(body.sprite.rect.width, Is.EqualTo(384), "Live rendering must use the body-only attack art, not the old wide breath.");
            Assert.That(body.sprite.name, Does.StartWith("AttackBody_"));
            return DirectedAttacks[DirectedAttacks.Count - 1];
        }

        private void StepDirected(float dt) => Call(controller, "UpdateGuardianDirectedAttacks", dt);
        private static float ImpactAt(string id) => (float)S("Battle.BattleSceneController", "GuardianDirectedImpactDelay", id);
        private Vector2 CanvasCenter(Image image) => ((RectTransform)canvas.transform)
            .InverseTransformPoint(image.rectTransform.TransformPoint(image.rectTransform.rect.center));

        private void AssertChargeAtPaintedMouth(string id, Image body, object shot)
        {
            // Measured directly on the generated 384px body PNGs (top-left origin).
            // Projecting that painted point through the image transform also tests
            // the trial's left-facing mirror without reusing the runtime socket.
            Vector2 paintedPixel = id == "seiryu" ? new Vector2(338, 145) : new Vector2(320, 124);
            Rect bodyRect = body.rectTransform.rect;
            Vector3 paintedWorld = body.rectTransform.TransformPoint(new Vector3(
                bodyRect.xMin + paintedPixel.x / 384f * bodyRect.width,
                bodyRect.yMax - paintedPixel.y / 384f * bodyRect.height, 0));
            var charge = (Image)F(shot, "Charge");
            var canvasRect = (RectTransform)canvas.transform;
            Vector2 paintedOnCanvas = canvasRect.InverseTransformPoint(paintedWorld);
            Vector2 chargeOnCanvas = canvasRect.InverseTransformPoint(charge.rectTransform.TransformPoint(Vector3.zero));
            Assert.That(Vector2.Distance(paintedOnCanvas, chargeOnCanvas), Is.LessThan(10f),
                "The charge must attach to the painted mouth rather than the chest after layout and capture have settled.");
            Assert.That(canvas.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvasRect.sizeDelta, Is.EqualTo(new Vector2(1080, 2340)));
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void ChargeRemainsAttachedToThePaintedMouthAcrossRepeatedCaptureLayouts(string id)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            object shot = StartGuardianNormalAttack();
            var body = (Image)((IList)F(controller, "allyPreviewImages"))[5];
            StepDirected(.08f);
            AssertChargeAtPaintedMouth(id, body, shot);
            Capture("directed-" + id + "-mouth-registration");
            PrepareCaptureCanvas();
            Canvas.ForceUpdateCanvases();
            AssertChargeAtPaintedMouth(id, body, shot);
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void DirectedAttackArtHasSeparateBodyChargeFlightTrailAndImpact(string id)
        {
            string name = ArtName(id);
            foreach (string layer in new[] { "Charge", "Projectile", "Trail", "Impact" })
            {
                var sprite = Resources.Load<Sprite>(ArtRoot + "DirectedAttack/" + name + "_" + layer);
                Assert.That(sprite, Is.Not.Null, name + " " + layer);
                Assert.That(sprite.rect.width, Is.EqualTo(sprite.rect.height));
                Assert.That(sprite.rect.width, Is.GreaterThanOrEqualTo(384));
                Rect bounds = OpaqueSpriteBounds(sprite);
                Assert.That(bounds.xMin, Is.GreaterThan(0));
                Assert.That(bounds.yMin, Is.GreaterThan(0));
                Assert.That(bounds.xMax, Is.LessThan(sprite.rect.width));
                Assert.That(bounds.yMax, Is.LessThan(sprite.rect.height));
            }
            for (int i = 0; i < 8; i++)
            {
                var body = Resources.Load<Sprite>(ArtRoot + "Animation/" + name + "/AttackBody_" + i);
                Assert.That(body, Is.Not.Null);
                Assert.That(body.rect.size, Is.EqualTo(new Vector2(384, 384)));
                Assert.That(body.pivot, Is.EqualTo(new Vector2(192, 192)));
            }
        }

        [TestCase("seiryu", false)] [TestCase("seiryu", true)]
        [TestCase("suzaku", false)] [TestCase("suzaku", true)]
        public void DirectedPathConnectsBothEndpointsAndSuzakuRisesAboveTheDirectLine(string id, bool mirrored)
        {
            var start = new Vector2(mirrored ? 350 : -350, 120);
            var end = new Vector2(mirrored ? -210 : 210, -60);
            Vector2 Sample(float t) => (Vector2)S("Battle.BattleSceneController", "SampleGuardianDirectedPath", id, start, end, t);
            Assert.That(Vector2.Distance(Sample(0), start), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(Sample(1), end), Is.LessThan(.001f));
            Vector2 previous = start;
            for (int i = 1; i <= 40; i++)
            {
                float progress = i / 40f;
                Vector2 point = Sample(progress);
                Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y), Is.False);
                Assert.That(Vector2.Distance(point, previous), Is.LessThan(100f), "No frame discontinuity along the flight path.");
                if (id == "seiryu")
                {
                    Vector2 delta = point - start, direction = end - start;
                    float cross = delta.x * direction.y - delta.y * direction.x;
                    Assert.That(Mathf.Abs(cross), Is.LessThan(.1f), "Seiryu is a direct beam to the actual target.");
                }
                previous = point;
            }
            if (id == "suzaku")
                Assert.That(Sample(.5f).y, Is.GreaterThan(Mathf.Max(start.y, end.y) + 70f), "Suzaku must visibly rise before descending onto the target.");
        }

        [TestCase("seiryu", false)] [TestCase("seiryu", true)]
        [TestCase("suzaku", false)] [TestCase("suzaku", true)]
        public void RealGuardianAttackChargesTravelsAndReactsOnlyAtTargetContact(string id, bool genericDamageNumbers)
        {
            BuildBattle(id); PrepareCaptureCanvas();
            var hits = ConnectDirectedHitPresentation(genericDamageNumbers);
            object shot = StartGuardianNormalAttack();
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That((Vector2)F(shot, "StartPosition"), Is.Not.EqualTo((Vector2)F(shot, "EndPosition")));
            Assert.That(F(shot, "ImpactApplied"), Is.False);
            StepDirected(.10f);
            Assert.That(((Image)F(shot, "Charge")).gameObject.activeSelf, Is.True);
            Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[0], Is.EqualTo(0));
            Capture("directed-" + id + "-charge");
            StepDirected(.25f);
            Assert.That(((Image)F(shot, "Projectile")).gameObject.activeSelf, Is.True);
            Assert.That(F(shot, "ImpactApplied"), Is.False);
            Capture("directed-" + id + "-flight");
            StepDirected(ImpactAt(id) - .35f - .01f);
            Assert.That(F(shot, "ImpactApplied"), Is.False);
            Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[0], Is.EqualTo(0));
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.Zero);
            StepDirected(.02f);
            Assert.That(F(shot, "ImpactApplied"), Is.True);
            Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[0], Is.GreaterThan(0));
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.EqualTo(genericDamageNumbers ? 1 : 0),
                "Guardian contact must respect the same damage-number setting as ordinary attacks.");
            var bodiesLayer = (GameObject)F(controller, "monsterPreviewRoot");
            var effectsLayer = (GameObject)F(controller, "rangedEffectRoot");
            var numbersLayer = (GameObject)F(controller, "floatingDamageRoot");
            Assert.That(effectsLayer.transform.parent, Is.EqualTo(bodiesLayer.transform.parent));
            if (genericDamageNumbers) Assert.That(numbersLayer.transform.parent, Is.EqualTo(bodiesLayer.transform.parent));
            Assert.That(effectsLayer.transform.GetSiblingIndex(), Is.GreaterThan(bodiesLayer.transform.GetSiblingIndex()));
            if (genericDamageNumbers) Assert.That(numbersLayer.transform.GetSiblingIndex(), Is.GreaterThan(effectsLayer.transform.GetSiblingIndex()),
                "Enabled damage numbers must remain in front of the creature and its impact art.");
            var impact = (Image)F(shot, "Impact");
            Assert.That(impact.gameObject.activeSelf, Is.True);
            Vector3 targetWorld = ((RectTransform)canvas.transform).TransformPoint((Vector2)F(shot, "EndPosition"));
            Assert.That(impact.rectTransform.rect.Contains(impact.rectTransform.InverseTransformPoint(targetWorld)), Is.True,
                "The impact drawing must cover the target, including the base of Suzaku's rising pillar.");
            Capture("directed-" + id + "-impact");
            if (!genericDamageNumbers)
            {
                var ordinaryHit = Activator.CreateInstance(T("Battle.BattleHitInfo"), new object[] {
                    false, 7, false, false, false, 0, 0, 0f, (int)F(Enemies[0], "RuntimeId"), -1 });
                Call(controller, "HandleBattleHitResolved", ordinaryHit);
                Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.Zero,
                    "With damage numbers disabled, neither a guardian nor an ordinary hit may create a label.");
            }
            StepDirected(1f);
            Assert.That(DirectedAttacks.Count, Is.EqualTo(0), "Completed attacks must release all transient layers.");
            if (!genericDamageNumbers) CaptureDirectedMotionWhenRequested(id, false);
        }

        [Test] public void SuzakuDivineAttackReachesEveryDamagedTargetAndBurnTicksDoNotLaunchAgain()
        {
            BuildBattle("suzaku"); PrepareCaptureCanvas();
            var hits = ConnectDirectedHitPresentation();
            Call(simulator, "TickGuardianSkill", 4f);
            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(DirectedAttacks.Count, Is.EqualTo(3));
            var targetIndices = DirectedAttacks.Cast<object>().Select(shot => (int)P(F(shot, "HitInfo"), "TargetIndex")).ToArray();
            Assert.That(targetIndices.OrderBy(i => i), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(DirectedAttacks.Cast<object>().Select(shot => (Vector2)F(shot, "EndPosition")).Distinct().Count(), Is.EqualTo(3),
                "Area damage must have an impact at each actual target, not one decorative effect at the first enemy.");
            StepDirected(.42f); Capture("directed-suzaku-divine-flight");
            StepDirected(ImpactAt("suzaku") - .42f + .01f);
            foreach (object shot in DirectedAttacks) Assert.That(F(shot, "ImpactApplied"), Is.True);
            for (int i = 0; i < 3; i++) Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[i], Is.GreaterThan(0));
            Capture("directed-suzaku-divine-impact");
            StepDirected(1f);
            Assert.That(DirectedAttacks.Count, Is.Zero);
            Call(simulator, "TickGuardianSkill", 1f);
            Assert.That(hits.Count, Is.EqualTo(6), "The already-applied burn still damages each affected enemy.");
            Assert.That(DirectedAttacks.Count, Is.Zero, "A burn tick is not another breath launch from the guardian.");
            Assert.That(P(simulator, "GuardianSkillCount"), Is.EqualTo(1));
        }

        [Test] public void SuzakuNormalAttackPreservesItsThreeTargetsAndUsesOneFlightForEach()
        {
            BuildBattle("suzaku"); PrepareCaptureCanvas();
            var hits = ConnectDirectedHitPresentation();
            Assert.That(F(F(GuardianRuntime, "Data"), "normalAttackTargetCount"), Is.EqualTo(3));
            Call(simulator, "PerformAttackOnEnemy", GuardianRuntime, false, 5);
            Assert.That(hits.Count, Is.EqualTo(1), "One combat action still emits one grouped hit event.");
            Assert.That(DirectedAttacks.Count, Is.EqualTo(3));
            var targets = DirectedAttacks.Cast<object>().Select(shot => (int)P(F(shot, "HitInfo"), "TargetIndex")).OrderBy(i => i).ToArray();
            Assert.That(targets, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(Enemies.Cast<object>().Count(enemy => (int)F(F(enemy, "Stats"), "CurrentHp") < 100000), Is.EqualTo(3));
        }

        [TestCase("seiryu", false)] [TestCase("seiryu", true)]
        [TestCase("suzaku", false)] [TestCase("suzaku", true)]
        public void LethalTargetRemainsVisibleUntilItsOwnImpactAndNeverFlashesTheReplacement(string id, bool genericDamageNumbers)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation(genericDamageNumbers);
            F(F(Enemies[0], "Stats"), "CurrentHp", 1);
            object shot = StartGuardianNormalAttack();
            Assert.That(F(F(Enemies[0], "Stats"), "CurrentHp"), Is.EqualTo(0), "Combat resolution stays authoritative and immediate.");
            Vector2 contact = (Vector2)F(shot, "EndPosition");
            Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            var defeatedImage = (Image)F(shot, "DefeatedTarget");
            Assert.That(defeatedImage, Is.Not.Null, "A defeated target needs a visual snapshot while the projectile travels.");
            StepDirected(ImpactAt(id) - .01f);
            Assert.That(defeatedImage.gameObject.activeSelf, Is.True);
            Assert.That(defeatedImage.color.a, Is.GreaterThan(.9f), "The target must not fade out before contact.");
            Assert.That(F(shot, "ImpactApplied"), Is.False);
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.Zero, "Damage numbers must wait for contact.");
            Assert.That((Vector2)F(shot, "EndPosition"), Is.EqualTo(contact));
            Capture("directed-" + id + "-lethal-before-impact");
            StepDirected(.02f);
            Assert.That(F(shot, "ImpactApplied"), Is.True);
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.EqualTo(genericDamageNumbers ? 1 : 0), "Killing blows obey the shared setting even after their combat target was removed.");
            Assert.That(((IList)F(controller, "enemyHitFlashRemainings")).Cast<float>().All(value => value <= 0), Is.True,
                "A new enemy occupying the defeated index cannot inherit its damage reaction.");
            Capture("directed-" + id + "-lethal-impact");
            StepDirected(.02f);
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.EqualTo(genericDamageNumbers ? 1 : 0), "The same contact cannot display damage twice or bypass the setting.");
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void TargetRuntimeIdentitySurvivesRemovalOfAnEarlierEnemy(string id)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            object shot = StartGuardianNormalAttack(1);
            int targetId = (int)F(Enemies[1], "RuntimeId");
            Assert.That(P(F(shot, "HitInfo"), "TargetRuntimeId"), Is.EqualTo(targetId));
            F(F(Enemies[0], "Stats"), "CurrentHp", 0);
            Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            StepDirected(ImpactAt(id) + .01f);
            Assert.That(F(Enemies[0], "RuntimeId"), Is.EqualTo(targetId));
            Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[0], Is.GreaterThan(0));
            Assert.That((float)((IList)F(controller, "enemyHitFlashRemainings"))[1], Is.EqualTo(0));
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void InFlightAttackTracksALivingMovingTargetUntilContact(string id)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            object shot = StartGuardianNormalAttack();
            Vector2 launchTarget = (Vector2)F(shot, "EndPosition");
            StepDirected(.20f);
            F(Enemies[0], "PositionAnchor", new Vector2(.72f, .68f));
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            StepDirected(.12f);
            var targetImage = (Image)((IList)F(controller, "enemyPreviewImages"))[0];
            Assert.That(Vector2.Distance((Vector2)F(shot, "EndPosition"), launchTarget), Is.GreaterThan(10f));
            Assert.That(Vector2.Distance((Vector2)F(shot, "EndPosition"), CanvasCenter(targetImage)), Is.LessThan(.1f));
            StepDirected(ImpactAt(id) - .32f + .01f);
            Assert.That(F(shot, "ImpactApplied"), Is.True);
            Assert.That(Vector2.Distance((Vector2)F(shot, "EndPosition"), CanvasCenter(targetImage)), Is.LessThan(.1f));
        }

        [TestCase("seiryu", false)] [TestCase("seiryu", true)]
        [TestCase("suzaku", false)] [TestCase("suzaku", true)]
        public void EnemyTrialGuardianMirrorsTheLaunchAndLandsOnTheSelectedAlly(string id, bool genericDamageNumbers)
        {
            Assert.That(S("Data.GuardianTrialSession", "Begin", profile, id), Is.True);
            simulator = owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator, "Setup", 30);
            Call(simulator, "QueueEnemySpawn", false);
            F(Enemies[0], "PositionAnchor", new Vector2(.80f, .62f));
            var allies = (IList)F(simulator, "activeAllyRuntimes");
            foreach (var ally in allies)
            {
                F(F(ally, "Stats"), "MaxHp", 100000); F(F(ally, "Stats"), "CurrentHp", 100000);
            }
            F(Enemies[0], "TargetAllyRuntimeId", F(allies[0], "RuntimeId"));
            Call(simulator, "ClearEnemyMovementQueueCache");
            InitializeBattlePresentation(30); PrepareCaptureCanvas(); ConnectDirectedHitPresentation(genericDamageNumbers);
            Call(simulator, "PerformAttackOnPlayer", Enemies[0], 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That(DirectedAttacks.Count, Is.EqualTo(1));
            var shot = DirectedAttacks[0];
            Assert.That(P(F(shot, "HitInfo"), "TargetIsPlayer"), Is.True);
            Assert.That(((Vector2)F(shot, "StartPosition")).x, Is.GreaterThan(((Vector2)F(shot, "EndPosition")).x));
            int target = (int)P(F(shot, "HitInfo"), "TargetIndex");
            var targetImage = (Image)((IList)F(controller, "allyPreviewImages"))[target];
            AssertChargeAtPaintedMouth(id, (Image)((IList)F(controller, "enemyPreviewImages"))[0], shot);
            StepDirected(.35f); Capture("directed-trial-" + id + "-flight");
            StepDirected(ImpactAt(id) - .35f + .01f);
            Assert.That(Vector2.Distance((Vector2)F(shot, "EndPosition"), CanvasCenter(targetImage)), Is.LessThan(.1f));
            Assert.That((float)((IList)F(controller, "allyHitFlashRemainings"))[target], Is.GreaterThan(0));
            Assert.That(((IList)F(controller, "activeFloatingDamageTexts")).Count, Is.EqualTo(genericDamageNumbers ? 1 : 0),
                "Enemy trial guardians must also obey the shared damage-number setting.");
            Capture("directed-trial-" + id + "-impact");
            CaptureDirectedMotionWhenRequested(id, true);
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void InterruptedDirectedAttackClearsEveryTransientAndCannotProduceALateHit(string id)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            StartGuardianNormalAttack(); StepDirected(.25f);
            var visuals = DirectedAttacks.Cast<object>().SelectMany(shot => new[] {
                F(shot, "Charge") as Image, F(shot, "Projectile") as Image, F(shot, "Impact") as Image
            }).Where(image => image != null).ToArray();
            Call(controller, "OnDisable");
            Assert.That(DirectedAttacks.Count, Is.Zero);
            Assert.That(visuals.All(image => image == null || !image.gameObject.activeSelf), Is.True);
            StepDirected(2f);
            Assert.That(((IList)F(controller, "enemyHitFlashRemainings")).Cast<float>().All(value => value <= 0), Is.True);
        }

        [TestCase("Win")] [TestCase("Lose")]
        public void BattleResultWaitsForImpactThenReleasesOnceEvenWithoutASecondSimulationResult(string resultName)
        {
            BuildBattle("suzaku"); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            StartGuardianNormalAttack();
            object result = Enum.Parse(T("Battle.BattleResult"), resultName);
            object none = Enum.Parse(T("Battle.BattleResult"), "None");
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", result), Is.EqualTo(none));
            StepDirected(ImpactAt("suzaku") + .02f);
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", none), Is.EqualTo(none),
                "The result panel must also allow the impact flash to finish.");
            StepDirected(1f);
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", none), Is.EqualTo(result),
                "Practice timeout emits Win once, so the presentation must retain that result until its last attack finishes.");
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", none), Is.EqualTo(none));
        }

        [Test] public void LeavingBattleCancelsItsDeferredResult()
        {
            BuildBattle("seiryu"); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            StartGuardianNormalAttack();
            object win = Enum.Parse(T("Battle.BattleResult"), "Win");
            object none = Enum.Parse(T("Battle.BattleResult"), "None");
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", win), Is.EqualTo(none));
            Call(controller, "OnDisable");
            Assert.That(Call(controller, "ResolveGuardianPresentationResult", none), Is.EqualTo(none),
                "An interrupted battle must not show an old result after the scene is reused.");
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void AnEarlierNonlethalProjectileCannotHideTheTargetBeforeItsLaterKillingBlow(string id)
        {
            BuildBattle(id); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            object earlier = StartGuardianNormalAttack();
            StepDirected(.15f);
            F(F(Enemies[0], "Stats"), "CurrentHp", 1);
            object lethal = StartGuardianNormalAttack();
            Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That(F(earlier, "OwnsDefeat"), Is.False);
            Assert.That(F(lethal, "OwnsDefeat"), Is.True);
            StepDirected(ImpactAt(id) - .15f + .01f);
            Assert.That(F(earlier, "ImpactApplied"), Is.True);
            Assert.That(F(lethal, "ImpactApplied"), Is.False);
            var retainedTarget = (Image)F(lethal, "DefeatedTarget");
            Assert.That(retainedTarget.gameObject.activeSelf, Is.True);
            Assert.That(retainedTarget.color.a, Is.GreaterThan(.99f), "The first, nonlethal hit cannot begin the target's death fade.");
            StepDirected(.15f);
            Assert.That(F(lethal, "ImpactApplied"), Is.True);
            Assert.That(retainedTarget.color.a, Is.LessThan(1f));
        }

        private void CaptureDirectedMotionWhenRequested(string id, bool trial)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WITCHTOWER_GUARDIAN_MOTION_CAPTURE"))) return;
            Call(controller, "ClearGuardianDirectedAttacks");
            Call(controller, "ClearFloatingDamageTexts");
            foreach (string field in new[] { "allyHitFlashRemainings", "enemyHitFlashRemainings", "allyAttackVisualRemainings", "enemyAttackVisualRemainings" })
            {
                var values = (IList)F(controller, field);
                for (int i = 0; i < values.Count; i++) values[i] = 0f;
            }
            // Use the unmodified combat action for review movies: Suzaku really
            // launches toward all three normal-attack recipients.
            if (trial) Call(simulator, "PerformAttackOnPlayer", Enemies[0], 0);
            else Call(simulator, "PerformAttackOnEnemy", GuardianRuntime, false, 5);
            const float dt = 1f / 30f;
            for (int frame = 0; frame <= 36; frame++)
            {
                if (frame > 0)
                {
                    foreach (string field in new[] { "allyAttackVisualRemainings", "enemyAttackVisualRemainings", "allyHitFlashRemainings", "enemyHitFlashRemainings" })
                    {
                        var values = (IList)F(controller, field);
                        for (int i = 0; i < values.Count; i++) values[i] = Mathf.Max(0f, (float)values[i] - dt);
                    }
                }
                Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
                Call(controller, "UpdateGuardianDivinePresentation", frame == 0 ? 0f : dt);
                StepDirected(frame == 0 ? 0f : dt);
                Call(controller, "UpdateFloatingDamageTexts", frame == 0 ? 0f : dt);
                Capture("motion-" + (trial ? "trial-" : "") + id + "/frame-" + frame.ToString("D3"));
            }
        }
    }
}
