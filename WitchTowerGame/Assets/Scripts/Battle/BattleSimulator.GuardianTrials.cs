using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public sealed partial class BattleSimulator
    {
        // A trial guardian occupies a large, fixed footprint. Ordinary melee allies
        // can reach its near edge without abandoning their formation limits.
        private static readonly Vector2 GuardianTrialAnchor = new Vector2(.78f, .44f);
        private const float GuardianTrialCombatRadius = .22f;

        private float guardianTrialElapsed;
        private int guardianTrialBarrierCycle;
        private int guardianTrialStrongAttackCycle;
        private readonly Dictionary<int, int> guardianTrialBarriers = new Dictionary<int, int>();
        public float GuardianTrialRemaining => GuardianTrialSession.IsPractice
            ? Mathf.Max(0f, GuardianTrialSession.PracticeDuration - guardianTrialElapsed) : 0f;
        public string GuardianTrialCue
        {
            get
            {
                if (!GuardianTrialSession.IsActive || GuardianTrialSession.IsPractice) return string.Empty;
                switch (GuardianTrialSession.GuardianId)
                {
                    case "seiryu": return guardianTrialElapsed % 6f < 3f ? "青龍の連撃 ─ 次は息継ぎ" : "息継ぎ ─ 防御が低下";
                    case "suzaku": return GuardianTrialMinionsRemain() ? "焔の眷属を倒すと炎撃が弱まる" : "眷属を撃破 ─ 朱雀の炎撃が弱体化";
                    case "byakko":
                        float phase = guardianTrialElapsed % 7f;
                        return phase >= 4f && phase < 5f ? "白虎が力を溜めている…" :
                            phase >= 5f && phase < 5.7f ? "白耀の強打！" : phase >= 5.7f ? "強打の隙 ─ 防御が低下" : "白虎の牙を見極めよ";
                    case "genbu": return guardianTrialElapsed % 8f < 3f ? "玄武の有限障壁 ─ 攻撃で砕ける" : "障壁消失 ─ 攻撃の好機";
                    default: return string.Empty;
                }
            }
        }

        private void SetupGuardianTrial()
        {
            guardianTrialElapsed = 0f;
            guardianTrialBarrierCycle = guardianTrialStrongAttackCycle = -1;
            guardianTrialBarriers.Clear();
        }

        private void TickGuardianTrial(float dt)
        {
            if (!GuardianTrialSession.IsActive) return;
            guardianTrialElapsed += Mathf.Max(0f, dt);
            if (GuardianTrialSession.IsPractice || GuardianTrialSession.GuardianId != "genbu") return;
            int cycle = Mathf.FloorToInt(guardianTrialElapsed / 8f);
            if (guardianTrialElapsed % 8f >= 3f) { guardianTrialBarriers.Clear(); return; }
            if (cycle == guardianTrialBarrierCycle) return;
            foreach (var enemy in activeEnemyRuntimes)
            {
                if (!IsGuardianTrialAvatar(enemy) || enemy.Stats.IsDead()) continue;
                guardianTrialBarriers[enemy.RuntimeId] = Mathf.Max(1, Mathf.RoundToInt(enemy.Stats.MaxHp * .12f));
                guardianTrialBarrierCycle = cycle;
            }
        }

        private bool IsGuardianTrialAvatar(EnemyRuntime enemy) => GuardianTrialSession.IsActive &&
            !GuardianTrialSession.IsPractice && enemy?.Data != null &&
            enemy.Data.enemyId == "guardian_trial_" + GuardianTrialSession.GuardianId;

        private bool GuardianTrialMinionsRemain()
        {
            if (spawnedEnemiesInCurrentWave < GuardianTrialSession.EnemyCount) return true;
            foreach (var enemy in activeEnemyRuntimes)
                if (enemy?.Stats != null && !enemy.Stats.IsDead() && !IsGuardianTrialAvatar(enemy)) return true;
            return false;
        }

        private float GuardianTrialEnemyAttackRate(EnemyRuntime enemy) => IsGuardianTrialAvatar(enemy) &&
            GuardianTrialSession.GuardianId == "seiryu" ? (guardianTrialElapsed % 6f < 3f ? 1.5f : .6f) : 1f;

        private bool GuardianTrialEnemyIsWindingUp(EnemyRuntime enemy) => IsGuardianTrialAvatar(enemy) &&
            GuardianTrialSession.GuardianId == "byakko" && guardianTrialElapsed % 7f >= 4f && guardianTrialElapsed % 7f < 5f;

        private bool GuardianTrialStrongAttackReady(EnemyRuntime enemy) => IsGuardianTrialAvatar(enemy) &&
            GuardianTrialSession.GuardianId == "byakko" && guardianTrialElapsed % 7f >= 5f && guardianTrialElapsed % 7f < 5.7f &&
            guardianTrialStrongAttackCycle != Mathf.FloorToInt(guardianTrialElapsed / 7f);

        private float GuardianTrialEnemyDamageMultiplier(EnemyRuntime enemy)
        {
            if (!IsGuardianTrialAvatar(enemy)) return 1f;
            if (GuardianTrialSession.GuardianId == "suzaku") return GuardianTrialMinionsRemain() ? 1.15f : .65f;
            if (GuardianTrialSession.GuardianId == "byakko")
            {
                int cycle = Mathf.FloorToInt(guardianTrialElapsed / 7f);
                float phase = guardianTrialElapsed % 7f;
                if (phase >= 5f && phase < 5.7f && guardianTrialStrongAttackCycle != cycle)
                { guardianTrialStrongAttackCycle = cycle; return 1.8f; }
            }
            return 1f;
        }

        private int GuardianTrialDefense(EnemyRuntime enemy, MonsterDamageType damageType)
        {
            int defense = damageType == MonsterDamageType.Magic ? enemy.Stats.MagicDefense : enemy.Stats.Defense;
            if (IsGuardianTrialAvatar(enemy) && ((GuardianTrialSession.GuardianId == "seiryu" && guardianTrialElapsed % 6f >= 3f) ||
                (GuardianTrialSession.GuardianId == "byakko" && guardianTrialElapsed % 7f >= 5.7f)))
                return Mathf.RoundToInt(defense * .6f);
            return defense;
        }

        private int AbsorbGuardianTrialBarrier(EnemyRuntime enemy, int damage)
        {
            if (!guardianTrialBarriers.TryGetValue(enemy.RuntimeId, out int shield) || shield <= 0) return damage;
            int absorbed = Mathf.Min(shield, Mathf.Max(0, damage));
            guardianTrialBarriers[enemy.RuntimeId] = shield - absorbed;
            return damage - absorbed;
        }
    }
}
