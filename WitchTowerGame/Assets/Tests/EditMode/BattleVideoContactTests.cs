using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed partial class GuardianPresentationTests
    {
        [Test]
        public void SurvivingEnemyKeepsItsAttackWhenEarlierEnemyIsRemoved()
        {
            BuildBattle("seiryu"); ConnectDirectedHitPresentation();
            Call(controller, "UpdateBattlePresentation", 0f);
            var enemy = Enemies[1];
            Call(simulator, "PerformAttackOnPlayer", enemy, 1);
            float remaining = (float)((IList)F(controller, "enemyAttackVisualRemainings"))[1];
            Assert.That(remaining, Is.GreaterThan(0f));
            F(F(Enemies[0], "Stats"), "CurrentHp", 0);
            Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[0], Is.EqualTo(remaining),
                "Attack belongs to runtime ID, not its old array index or a delayed death effect.");
            Call(controller, "UpdateBattlePresentation", .1f);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[0], Is.GreaterThan(0f));
            remaining = (float)((IList)F(controller, "enemyAttackVisualRemainings"))[0];
            Call(controller, "ConsumeEnemyPreviewRemovalAt", 0);
            Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[0], Is.EqualTo(remaining),
                "Completing a delayed death effect cannot shift a living enemy's attack a second time.");
            Assert.That((float)((IList)F(controller, "enemyAttackVisualRemainings"))[1], Is.Zero,
                "A different enemy cannot inherit the previous slot owner's attack.");
        }

        [TestCase("seiryu")] [TestCase("suzaku")]
        public void EnemyRetainedUntilGuardianImpactDoesNotBecomeAStillImage(string guardian)
        {
            BuildBattle(guardian); PrepareCaptureCanvas(); ConnectDirectedHitPresentation();
            F(F(Enemies[0], "Stats"), "CurrentHp", 1);
            StartGuardianNormalAttack();
            var shots = (IList)F(controller, "activeGuardianDirectedAttacks");
            Assert.That(shots.Count, Is.GreaterThan(0));
            Call(simulator, "AdvanceEncounterAfterEnemyDefeat", 0);
            object shot = null;
            foreach (var candidate in shots) if ((bool)F(candidate, "OwnsDefeat")) shot = candidate;
            Assert.That(shot, Is.Not.Null);
            var retained = (UnityEngine.UI.Image)F(shot, "DefeatedTarget");
            Call(controller, "UpdateGuardianDirectedAttacks", .01f);
            Sprite first = retained.sprite;
            Call(controller, "UpdateGuardianDirectedAttacks", .26f);
            Assert.That((bool)F(shot, "ImpactApplied"), Is.False);
            Assert.That(retained.gameObject.activeSelf, Is.True);
            Assert.That(retained.sprite, Is.Not.EqualTo(first), "Visible enemies must keep animating while the projectile travels.");
        }

        [Test, Explicit("Video investigation trace; uses an isolated party and never reads or writes player saves.")]
        public void VideoFloor21EnemyContactDiagnostic()
        {
            ((IList)P(profile, "PartyMonsterInstanceIds")).Clear();
            string[] ids = { "monster_cosmic_ore_fortress_golem", "monster_fortress_machine_gigafort", "monster_abyss_dragon", "monster_magic_sword_saint_luciel", "monster_abyss_grand_mage_seraphis" };
            for (int i = 0; i < ids.Length; i++)
            {
                object owned = Call(profile, "AddOwnedMonster", ids[i], i % 2 == 0 ? 7 : 6, 0, false);
                ((IList)P(profile, "PartyMonsterInstanceIds")).Add(F(owned, "InstanceId"));
            }
            S("Data.GuardianService", "GrantCore", profile, "seiryu");
            S("Data.GuardianService", "Birth", profile, "seiryu");
            F(S("Data.GuardianService", "Owned", profile, "seiryu"), "Level", 12);
            S("Data.GuardianService", "Equip", profile, "seiryu");
            Call(game, "SetCurrentFloor", 21);
            simulator = owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator, "Setup", 21);
            InitializeBattlePresentation(21);
            var hits = ConnectDirectedHitPresentation();
            var samples = new Dictionary<int, float[]>();
            var labels = new Dictionary<int, string>();
            for (int frame = 0; frame < 1800; frame++)
            {
                string result = Call(simulator, "Tick", 1f / 60f).ToString();
                Call(controller, "UpdateBattlePresentation", 1f / 60f);
                Call(controller, "UpdatePreviewLayoutFromSimulator", simulator);
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var enemy = Enemies[i]; int id = (int)F(enemy, "RuntimeId");
                    if (!samples.ContainsKey(id)) { samples[id] = new float[5]; labels[id] = (string)F(F(enemy, "Data"), "enemyId"); }
                    float[] s = samples[id]; s[0] += 1f / 60f;
                    if ((bool)Call(simulator, "IsEnemyAttackEngaged", i)) s[1] += 1f / 60f;
                    if (!(bool)F(enemy, "IsMoving")) s[2] += 1f / 60f;
                    s[3] = Mathf.Max(s[3], (float)F(enemy, "AttackTimer"));
                    if ((float)((IList)F(controller, "enemyAttackVisualRemainings"))[i] > 0) s[4] += 1f / 60f;
                }
                if (result != "None") break;
            }
            foreach (var pair in samples)
            {
                int attacks = 0; foreach (var hit in hits) if ((bool)P(hit, "TargetIsPlayer") && (int)P(hit, "AttackerRuntimeId") == pair.Key) attacks++;
                Debug.Log($"CONTACT id={pair.Key} {labels[pair.Key]} life/range/still/timer/visual={string.Join(",", pair.Value)} hits={attacks}");
            }
        }
    }
}
