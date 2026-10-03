using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed partial class GuardianPresentationTests
    {
        [Test]
        public void FullyAbsorbedEnemyAttackStillAnimatesWithoutFalseDamageReaction()
        {
            BuildBattle("genbu");
            var hits = ConnectDirectedHitPresentation();
            var ally = ((IList)F(simulator, "activeAllyRuntimes"))[0];
            var enemy = Enemies[0];
            F(enemy, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            F(F(enemy, "Stats"), "Attack", 5);
            F(F(enemy, "Stats"), "Wisdom", 5);
            F(F(enemy, "Stats"), "CritRate", -1f);
            F(simulator, "guardianBarrierRemaining", 6f);
            ((IDictionary)F(simulator, "guardianBarriers"))[0] = 100000;
            int hp = (int)F(F(ally, "Stats"), "CurrentHp");
            Call(simulator, "ClearEnemyMovementQueueCache");
            Call(simulator, "PerformAttackOnPlayer", enemy, 0);
            Assert.That(F(F(ally, "Stats"), "CurrentHp"), Is.EqualTo(hp));
            Assert.That((int)P(simulator, "GuardianDamageBlocked"), Is.GreaterThan(0));
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[0], Is.GreaterThan(0f),
                "A shield blocks damage, not the enemy's attack animation.");
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(P(hits[0], "Damage"), Is.EqualTo(0));
            Assert.That((float)F(controller, "visualHitStopRemaining"), Is.Zero);
            Assert.That((float)((IList)F(controller, "allyHitFlashRemainings"))[0], Is.Zero);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            var visual = Call(controller, "ResolveEnemyPreviewVisualData", simulator, 0);
            var attackFrames = (IList)F(visual, "AttackSprites");
            Assert.That(((Image)((IList)F(controller, "enemyPreviewImages"))[0]).sprite, Is.EqualTo(attackFrames[0]));
            Assert.That(Call(controller, "ResolveEnemyPreviewPose", 0, false, true).ToString(), Is.EqualTo("Attack"));
        }

        [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 10)] [TestCase(true, 10)]
        public void FrequentHitsCannotFreezeTheEntireBattleAnimationClock(bool critical, int speed)
        {
            BuildBattle("seiryu");
            var hits = ConnectDirectedHitPresentation();
            Call(controller, "UpdateBattlePresentation", 0f);
            var ally = ((IList)F(simulator, "activeAllyRuntimes"))[0];
            var enemy = Enemies[0];
            F(enemy, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            F(F(enemy, "Stats"), "CritRate", critical ? 1f : -1f);
            Call(simulator, "ClearEnemyMovementQueueCache");
            Call(simulator, "PerformAttackOnPlayer", enemy, 0);
            Assert.That(hits.Count, Is.EqualTo(1));
            object hit = hits[0];
            Assert.That(P(hit, "IsCritical"), Is.EqualTo(critical));
            float start = (float)F(controller, "battlePresentationClock");
            bool attackAdvanced = false;
            for (int frame = 0; frame < 120 / speed; frame++)
            for (int step = 0; step < speed; step++)
            {
                if ((frame * speed + step) % 3 == 0) Call(controller, "HandleBattleHitResolved", hit);
                Call(controller, "UpdateBattlePresentation", 1f / 60f);
                float remaining = (float)((IList)F(controller, "enemyAttackVisualRemainings"))[0];
                if (remaining > 0f && remaining < .4f) attackAdvanced = true;
            }
            Assert.That((float)F(controller, "battlePresentationClock") - start, Is.GreaterThan(1f),
                "Damage every 50 ms must not continually renew a global animation freeze.");
            Assert.That(attackAdvanced, Is.True, "An attack must reach its later frames even during frequent impacts.");
        }

        [Test]
        public void EnemyQueueSlotsDoNotSwapWhenUnitsReachTheirAssignedPositions()
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31);
            for (int i = 0; i < 8; i++) Call(simulator, "QueueEnemySpawn", false);
            var ally = ((IList)F(simulator, "activeAllyRuntimes"))[0];
            F(ally, "PositionAnchor", new Vector2(.4f, .4f));
            for (int i = 0; i < Enemies.Count; i++)
            {
                F(Enemies[i], "TargetAllyRuntimeId", F(ally, "RuntimeId"));
                F(Enemies[i], "HomeAnchor", new Vector2(1.04f, .4f));
                F(Enemies[i], "PositionAnchor", new Vector2(.55f + i * .025f, .4f));
            }
            Call(simulator, "RebuildEnemyMovementQueueCache");
            var original = new int[Enemies.Count];
            for (int i = 0; i < original.Length; i++) original[i] = (int)Call(simulator, "ResolveCachedEnemyQueueIndex", i);
            for (int frame = 0; frame < 20; frame++)
            {
                for (int i = 0; i < original.Length; i++)
                    F(Enemies[i], "PositionAnchor", Call(simulator, "ResolveEnemyCombatAnchor", Enemies[i], ally, original[i]));
                Call(simulator, "RebuildEnemyMovementQueueCache");
                for (int i = 0; i < original.Length; i++)
                    Assert.That(Call(simulator, "ResolveCachedEnemyQueueIndex", i), Is.EqualTo(original[i]),
                        "Reaching an angled contact slot must not reshuffle the queue every frame.");
            }
            F(F(Enemies[0], "Stats"), "CurrentHp", 0);
            Call(simulator, "RebuildEnemyMovementQueueCache");
            Assert.That(Call(simulator, "ResolveCachedEnemyQueueIndex", 5), Is.LessThan(5),
                "A waiting enemy must advance when a front attacker dies.");
        }

        [Test]
        public void EnemyAlreadyInAttackRangeHoldsContactInsteadOfChasingItsSlot()
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31); Call(simulator, "QueueEnemySpawn", false);
            var allies = (IList)F(simulator, "activeAllyRuntimes");
            var ally = allies[0];
            foreach (var member in allies) F(member, "AttackMotionLockRemaining", 100f);
            F(ally, "PositionAnchor", new Vector2(.4f, .4f));
            var enemy = Enemies[0];
            F(enemy, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            Vector2 contact = new Vector2(.44f, .43f);
            F(enemy, "PositionAnchor", contact);
            Assert.That(Call(simulator, "CanEnemyAttackTarget", enemy, 0, 0), Is.True);
            for (int frame = 0; frame < 120; frame++) Call(simulator, "TickUnitMovement", 1f / 60f);
            Assert.That(F(enemy, "PositionAnchor"), Is.EqualTo(contact));
            Assert.That(F(enemy, "IsMoving"), Is.False);
            Call(simulator, "TickEnemyAttackers", (float)Call(simulator, "GetEnemyAttackInterval", 0) + .01f);
            Assert.That((float)F(enemy, "AttackMotionLockRemaining"), Is.GreaterThan(0f));
        }

        [TestCase(.18f, .78f)]
        [TestCase(.78f, .18f)]
        [TestCase(.40f, .62f)]
        public void EnemyFrontSlotsAlwaysFinishApproachInsideAttackRange(float enemyLane, float allyLane)
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31);
            Call(simulator, "QueueEnemySpawn", false);
            var ally = ((IList)F(simulator, "activeAllyRuntimes"))[0];
            var enemy = Enemies[0];
            F(ally, "PositionAnchor", new Vector2(.4f, allyLane));
            F(ally, "HomeAnchor", new Vector2(.4f, allyLane));
            F(enemy, "HomeAnchor", new Vector2(1.04f, enemyLane));
            F(enemy, "AttackReachAnchor", .012f);
            for (int slot = 0; slot < 5; slot++)
            {
                var destination = (Vector2)Call(simulator, "ResolveEnemyCombatAnchor", enemy, ally, slot);
                F(enemy, "PositionAnchor", destination);
                Assert.That(Call(simulator, "CanEnemyAttackTarget", enemy, 0, 0), Is.True,
                    $"Front slot {slot} stopped at {destination} outside attack range.");
            }
            F(enemy, "PositionAnchor", Call(simulator, "ResolveEnemyCombatAnchor", enemy, ally, 10));
            Assert.That(Call(simulator, "CanEnemyAttackTarget", enemy, 0, 0), Is.False,
                "Waiting enemies must not gain long-range melee attacks.");
        }

        [Test]
        public void EnemyQueueSurvivesNewArrivalsAndReassignsWhenTargetDies()
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31);
            for (int i = 0; i < 3; i++) Call(simulator, "QueueEnemySpawn", false);
            var allies = (IList)F(simulator, "activeAllyRuntimes");
            var ally = allies[0];
            for (int i = 0; i < Enemies.Count; i++)
            {
                F(Enemies[i], "TargetAllyRuntimeId", F(ally, "RuntimeId"));
                F(Enemies[i], "PositionAnchor", new Vector2(.6f + i * .05f, .4f));
            }
            Call(simulator, "RebuildEnemyMovementQueueCache");
            Call(simulator, "QueueEnemySpawn", false);
            var newcomer = Enemies[Enemies.Count - 1];
            F(newcomer, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            F(newcomer, "PositionAnchor", F(ally, "PositionAnchor"));
            Call(simulator, "RebuildEnemyMovementQueueCache");
            Assert.That(F(newcomer, "QueueOrder"), Is.EqualTo(3), "A new arrival cannot displace established attackers.");
            for (int i = 0; i < 3; i++) Assert.That(F(Enemies[i], "QueueOrder"), Is.EqualTo(i));

            F(F(ally, "Stats"), "CurrentHp", 0);
            Call(simulator, "RebuildEnemyMovementQueueCache");
            foreach (var enemy in Enemies)
            {
                Assert.That(F(enemy, "QueueTargetAllyRuntimeId"), Is.Not.EqualTo(F(ally, "RuntimeId")));
                Assert.That(F(enemy, "QueueTargetAllyRuntimeId"), Is.EqualTo(F(enemy, "TargetAllyRuntimeId")));
                Assert.That((int)F(enemy, "QueueOrder"), Is.GreaterThanOrEqualTo(0));
            }
        }

        [TestCase(1)] [TestCase(10)]
        public void EnemyApproachesAttacksAndResumesChaseWhenTargetLeavesRange(int speed)
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31); Call(simulator, "QueueEnemySpawn", false);
            var allies = (IList)F(simulator, "activeAllyRuntimes");
            foreach (var member in allies)
            {
                F(member, "AttackMotionLockRemaining", 100f);
                F(F(member, "Stats"), "MaxHp", 1000000);
                F(F(member, "Stats"), "CurrentHp", 1000000);
            }
            var ally = allies[0]; var enemy = Enemies[0];
            F(ally, "PositionAnchor", new Vector2(.4f, .4f));
            F(enemy, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            F(enemy, "PositionAnchor", new Vector2(.9f, .4f));
            Vector2 settled = Vector2.zero;
            for (int frame = 0; frame < 600 / speed; frame++)
            for (int step = 0; step < speed; step++)
            {
                Call(simulator, "TickUnitMovement", 1f / 60f);
                Call(simulator, "TickEnemyAttackers", 1f / 60f);
                if (frame * speed + step == 300) settled = (Vector2)F(enemy, "PositionAnchor");
                if (frame * speed + step > 300)
                    Assert.That(F(enemy, "PositionAnchor"), Is.EqualTo(settled), "Settled combat must not oscillate.");
            }
            Assert.That((int)F(F(ally, "Stats"), "CurrentHp"), Is.LessThan(1000000), "Contact must produce real attacks.");
            F(ally, "PositionAnchor", new Vector2(.15f, .4f));
            F(enemy, "AttackMotionLockRemaining", 0f);
            Call(simulator, "TickUnitMovement", 1f / 60f);
            Assert.That(F(enemy, "PositionAnchor"), Is.Not.EqualTo(settled), "Contact holding must not prevent a new chase.");
            Assert.That(F(enemy, "IsMoving"), Is.True);
        }

        [Test]
        public void EnemyBriefRangeLossPreservesCooldownWithoutDealingDistantDamage()
        {
            simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator, "Setup", 31);
            Call(simulator, "QueueEnemySpawn", false);
            var ally = ((IList)F(simulator, "activeAllyRuntimes"))[0];
            var enemy = Enemies[0];
            F(enemy, "TargetAllyRuntimeId", F(ally, "RuntimeId"));
            Call(simulator, "ClearEnemyMovementQueueCache");
            F(enemy, "PositionAnchor", new Vector2(2f, .5f));
            int hpBefore = (int)F(F(ally, "Stats"), "CurrentHp");
            float interval = (float)Call(simulator, "GetEnemyAttackInterval", 0);
            Call(simulator, "TickEnemyAttackers", interval * 2f);
            Assert.That(F(F(ally, "Stats"), "CurrentHp"), Is.EqualTo(hpBefore));
            Assert.That((float)F(enemy, "AttackTimer"), Is.Zero, "Approach must not accelerate the first attack.");
            F(enemy, "AttackTimer", interval * .97f);
            Call(simulator, "TickEnemyAttackers", interval * 2f);
            Assert.That((float)F(enemy, "AttackTimer"), Is.EqualTo(interval * .97f).Within(.001f));
            InitializeBattlePresentation(31);
            ConnectDirectedHitPresentation();
            F(enemy, "PositionAnchor", Call(simulator, "ResolveEnemyCombatAnchor", enemy, ally, 0));
            Call(simulator, "TickEnemyAttackers", interval * .04f);
            Assert.That((float)F(enemy, "AttackMotionLockRemaining"), Is.GreaterThan(0f),
                "Contact should resume a real attack, not restart a full idle cooldown.");
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[0], Is.GreaterThan(0f));
            Assert.That(Call(controller, "ResolveEnemyPreviewPose", 0, false, true).ToString(), Is.EqualTo("Attack"));
        }

        [TestCase("seiryu", 1920)] [TestCase("suzaku", 1920)]
        [TestCase("byakko", 1920)] [TestCase("genbu", 1920)]
        [TestCase("seiryu", 2340)] [TestCase("suzaku", 2340)]
        [TestCase("byakko", 2340)] [TestCase("genbu", 2340)]
        public void GuardianBodyAndHaloStayBelowEnemyCountHud(string id, int height)
        {
            BuildBattle(id);
            PrepareCaptureCanvas();
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, height);
            Call(controller, "EnsureWaveHud");
            Call(controller, "UpdateGuardianDivinePresentation", 0f);
            Canvas.ForceUpdateCanvases();
            var image = (Image)((IList)F(controller, "allyPreviewImages"))[5];
            var hud = ((GameObject)F(controller, "waveHudRoot")).GetComponent<RectTransform>();
            var bodyBounds = WorldRect(image.rectTransform);
            var halo = (Image)F(controller, "guardianHalo");
            var guardianHp = ((IList)F(controller, "allyPreviewHpBars"))[5];
            foreach (string element in new[] { "EnemyCountBarFrame", "WaveTitleText", "EnemyCountText" })
            {
                Rect hudBounds = WorldRect((RectTransform)hud.Find(element));
                hudBounds.xMin -= 16f; hudBounds.yMin -= 16f;
                hudBounds.xMax += 16f; hudBounds.yMax += 16f;
                Assert.That(bodyBounds.Overlaps(hudBounds), Is.False, id + " body overlaps " + element);
                Assert.That(WorldRect(halo.rectTransform).Overlaps(hudBounds), Is.False, id + " halo overlaps " + element);
                Assert.That(WorldRect((RectTransform)F(guardianHp, "Root")).Overlaps(hudBounds), Is.False, id + " HP overlaps " + element);
                Assert.That(WorldRect(((Text)F(guardianHp, "Label")).rectTransform).Overlaps(hudBounds), Is.False, id + " HP label overlaps " + element);
            }
            Assert.That(image.rectTransform.sizeDelta.y, Is.EqualTo(451f).Within(.001f),
                "Keep guardian size relative to the other monsters unchanged.");
            Rect guardianVisible = VisibleWorldRect(image);
            var allies = (IList)F(controller, "allyPreviewImages");
            for (int i = 0; i < 5; i++)
                Assert.That(guardianVisible.Overlaps(VisibleWorldRect((Image)allies[i])), Is.False,
                    id + " must clear ordinary ally " + i);
            if (height == 2340) Capture("contact-fix-guardian-" + id);
        }

        [TestCase("seiryu", 2340)] [TestCase("suzaku", 2340)]
        [TestCase("byakko", 2340)] [TestCase("genbu", 2340)]
        [TestCase("seiryu", 1920)] [TestCase("suzaku", 1920)]
        [TestCase("byakko", 1920)] [TestCase("genbu", 1920)]
        public void EnemyTopBarClearsGuardianHpAfterIPhoneSafeAreaMapping(string id, int height)
        {
            BuildBattle(id); PrepareCaptureCanvas();
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, height);
            Call(controller, "EnsureWaveHud");
            Call(controller, "EnsureRetireControls");
            Call(controller, "EnsurePermanentEffectsPanel");
            var hud = ((GameObject)F(controller, "waveHudRoot")).GetComponent<RectTransform>();
            var retire = ((Button)F(controller, "retireButton")).GetComponent<RectTransform>();
            var settings = canvas.transform.Find("BattlePermanentEffectsRoot");
            // Tall notched iPhones reserve both ends; 16:9 iPhones have no
            // notch/home-indicator and only reserve the status-bar edge.
            Rect safeArea = height == 2340 ? new Rect(0, height * .04f, 1080, height * .89f)
                : new Rect(0, 0, 1080, height - 60);
            foreach (var root in new[] { hud.gameObject, retire.gameObject, settings.gameObject })
            {
                var fitter = root.AddComponent(T("UI.SafeAreaFitter"));
                Call(fitter, "Apply", safeArea, new Vector2(1080, height));
            }
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Canvas.ForceUpdateCanvases();
            var hp = ((IList)F(controller, "allyPreviewHpBars"))[5];
            Rect hpBounds = WorldRect((RectTransform)F(hp, "Root"));
            Rect labelBounds = WorldRect(((Text)F(hp, "Label")).rectTransform);
            foreach (string element in new[] { "EnemyCountBarFrame", "WaveTitleText", "EnemyCountText" })
            {
                Rect bounds = WorldRect((RectTransform)hud.Find(element));
                var paddedHp = hpBounds; paddedHp.yMax += 16f;
                var paddedLabel = labelBounds; paddedLabel.yMax += 16f;
                Assert.That(bounds.Overlaps(paddedHp), Is.False, id + " HP " + paddedHp + " / " + element + " " + bounds);
                Assert.That(bounds.Overlaps(paddedLabel), Is.False, id + " label / " + element);
                Assert.That(bounds.Overlaps(WorldRect(retire)), Is.False, element + " / retire");
                Assert.That(bounds.Overlaps(WorldRect((RectTransform)settings.Find("PermanentEffectsSummary"))), Is.False,
                    element + " / settings");
            }
            if (height == 2340) Capture("raised-enemy-hud-" + id);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Rect VisibleWorldRect(Image image)
        {
            Rect pixels = OpaqueSpriteBounds(image.sprite);
            Rect rect = image.rectTransform.rect;
            Vector2 size = image.sprite.rect.size;
            if (image.preserveAspect)
            {
                float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
                Vector2 drawn = size * scale;
                rect = new Rect(rect.position + Vector2.Scale(rect.size - drawn, image.rectTransform.pivot), drawn);
            }
            Vector3 min = image.rectTransform.TransformPoint(new Vector3(rect.xMin + pixels.xMin / size.x * rect.width, rect.yMin + pixels.yMin / size.y * rect.height));
            Vector3 max = image.rectTransform.TransformPoint(new Vector3(rect.xMin + pixels.xMax / size.x * rect.width, rect.yMin + pixels.yMax / size.y * rect.height));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
