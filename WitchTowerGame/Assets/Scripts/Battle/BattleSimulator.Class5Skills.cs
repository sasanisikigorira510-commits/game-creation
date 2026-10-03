using System;
using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public sealed partial class BattleSimulator
    {
        private sealed class Class5Runtime
        {
            public AllyRuntime Ally;
            public Class5SkillDefinition Definition;
            public Class5SkillState State;
            public float NormalMotionWait = -1f;
        }
        private readonly Dictionary<int, Class5Runtime> class5Skills = new Dictionary<int, Class5Runtime>();
        private readonly Dictionary<int, Class5SlowState> class5Slows = new Dictionary<int, Class5SlowState>();
        public event Action<int, string> Class5SkillInvoked;

        public float GetClass5SkillMotionRemaining(int slot) => class5Skills.TryGetValue(slot, out var skill)
            && skill.Ally.Stats != null && !skill.Ally.Stats.IsDead() ? skill.State.MotionRemaining : 0f;
        public float GetClass5SkillCooldownRemaining(int slot) => class5Skills.TryGetValue(slot, out var skill)
            ? skill.State.CooldownRemaining : 0f;

        private void InitializeClass5Skills()
        {
            class5Skills.Clear(); class5Slows.Clear();
            foreach (var ally in activeAllyRuntimes)
            {
                var definition = Class5SkillCatalog.Resolve(ally?.Data?.monsterId);
                if (definition == null || ally.Data.classRank != 5) continue;
                class5Skills[ally.SlotIndex] = new Class5Runtime
                {
                    Ally = ally, Definition = definition,
                    State = new Class5SkillState
                    {
                        MonsterInstanceId = ally.OwnedMonster?.InstanceId,
                        CooldownRemaining = definition.Cooldown
                    }
                };
            }
        }

        private void TickClass5Skills(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            TickClass5Clocks(deltaTime);
            foreach (var runtime in class5Skills.Values)
            {
                var state = runtime.State;
                if (runtime.Ally.Stats == null || runtime.Ally.Stats.IsDead())
                {
                    state.TargetRuntimeIds = Array.Empty<int>(); state.NextHitIndex = runtime.Definition.HitCount;
                    state.MotionRemaining = 0f;
                    continue;
                }
                TickClass5PendingHits(runtime, deltaTime);
                // Wait for the one swing already in progress when the skill became
                // ready. Repeated fast normal attacks must not postpone it forever.
                if (state.CooldownRemaining > 0f || state.MotionRemaining > 0f)
                { runtime.NormalMotionWait = -1f; continue; }
                if (runtime.Ally.AttackMotionLockRemaining > 0f)
                {
                    if (runtime.NormalMotionWait < 0f)
                        runtime.NormalMotionWait = Mathf.Min(AttackMotionLockDuration, runtime.Ally.AttackMotionLockRemaining);
                    else runtime.NormalMotionWait = Mathf.Max(0f, runtime.NormalMotionWait - deltaTime);
                    if (runtime.NormalMotionWait > 0f) continue;
                }
                List<int> targets = CollectClass5Targets(runtime);
                if (targets.Count == 0) continue;
                StartClass5Skill(runtime, targets);
            }
        }

        private void TickClass5Preparation(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            TickClass5Clocks(deltaTime);
            foreach (var runtime in class5Skills.Values)
            {
                if (runtime.NormalMotionWait > 0f) runtime.NormalMotionWait = Mathf.Max(0f, runtime.NormalMotionWait - deltaTime);
                if (runtime.State.NextHitIndex < runtime.Definition.HitCount && runtime.State.TargetRuntimeIds.Length > 0)
                    runtime.State.PendingElapsed += deltaTime;
            }
        }

        private void TickClass5Clocks(float deltaTime)
        {
            var expired = new List<int>();
            foreach (var pair in class5Slows)
            {
                pair.Value.Remaining = Mathf.Max(0f, pair.Value.Remaining - deltaTime);
                if (pair.Value.Remaining <= 0f || ResolveEnemyRuntimeIndexById(pair.Key) < 0) expired.Add(pair.Key);
            }
            foreach (int id in expired) class5Slows.Remove(id);
            foreach (var runtime in class5Skills.Values)
            {
                var state = runtime.State;
                state.CooldownRemaining = Mathf.Max(0f, state.CooldownRemaining - deltaTime);
                state.MotionRemaining = Mathf.Max(0f, state.MotionRemaining - deltaTime);
                state.DamageReductionRemaining = Mathf.Max(0f, state.DamageReductionRemaining - deltaTime);
            }
        }

        private List<int> CollectClass5Targets(Class5Runtime runtime)
        {
            var result = new List<int>();
            var ally = runtime.Ally;
            int primary = ResolveAllyTargetEnemyIndex(ally, activeAllyRuntimes.IndexOf(ally));
            if (primary < 0 || !CanAllyAttackTarget(ally, primary)) return result;
            var definition = runtime.Definition;
            if (definition.Area == Class5SkillArea.Single) { result.Add(primary); return result; }
            Vector2 center = definition.Area == Class5SkillArea.SelfFront
                ? ally.PositionAnchor : activeEnemyRuntimes[primary].PositionAnchor;
            var candidates = new List<int>();
            for (int i = 0; i < activeEnemyRuntimes.Count; i++)
            {
                var enemy = activeEnemyRuntimes[i];
                if (enemy?.Stats == null || enemy.Stats.IsDead()) continue;
                if (definition.Area == Class5SkillArea.SelfFront && enemy.PositionAnchor.x < ally.PositionAnchor.x - .02f) continue;
                if (Vector2.Distance(center, enemy.PositionAnchor) <= definition.AreaRadius + enemy.CombatRadius) candidates.Add(i);
            }
            candidates.Sort((a, b) =>
            {
                if (a == primary) return b == primary ? 0 : -1;
                if (b == primary) return 1;
                int comparison = (activeEnemyRuntimes[a].PositionAnchor - center).sqrMagnitude.CompareTo(
                    (activeEnemyRuntimes[b].PositionAnchor - center).sqrMagnitude);
                return comparison != 0 ? comparison : activeEnemyRuntimes[a].RuntimeId.CompareTo(activeEnemyRuntimes[b].RuntimeId);
            });
            for (int i = 0; i < candidates.Count && result.Count < definition.MaxTargets; i++) result.Add(candidates[i]);
            return result;
        }

        private void StartClass5Skill(Class5Runtime runtime, List<int> targetIndices)
        {
            var state = runtime.State;
            var definition = runtime.Definition;
            state.CooldownRemaining = definition.Cooldown;
            runtime.NormalMotionWait = -1f;
            state.MotionRemaining = Class5SkillCatalog.MotionDuration;
            state.PendingElapsed = 0f; state.NextHitIndex = 0;
            state.CastOffense = definition.DamageType == MonsterDamageType.Magic ? runtime.Ally.Stats.Wisdom : runtime.Ally.Stats.Attack;
            state.CastPowerMultiplier = MonsterSkillGrowthCatalog.GetPowerMultiplier(runtime.Ally.OwnedMonster?.MonsterSkillLevel ?? 1);
            state.TargetRuntimeIds = new int[targetIndices.Count];
            for (int i = 0; i < targetIndices.Count; i++) state.TargetRuntimeIds[i] = activeEnemyRuntimes[targetIndices[i]].RuntimeId;
            state.DamageReductionRemaining = Mathf.Max(state.DamageReductionRemaining, definition.DamageReductionDuration);
            Class5SkillInvoked?.Invoke(runtime.Ally.SlotIndex, definition.SkillId);
            TickClass5PendingHits(runtime, 0f);
        }

        private void TickClass5PendingHits(Class5Runtime runtime, float deltaTime)
        {
            var state = runtime.State;
            if (state.NextHitIndex >= runtime.Definition.HitCount || state.TargetRuntimeIds.Length == 0) return;
            state.PendingElapsed += deltaTime;
            while (state.NextHitIndex < runtime.Definition.HitCount &&
                state.PendingElapsed + .00001f >= Class5SkillCatalog.FirstHitDelay + state.NextHitIndex * runtime.Definition.HitInterval)
            {
                ResolveClass5Hit(runtime);
                state.NextHitIndex++;
            }
        }

        private void ResolveClass5Hit(Class5Runtime runtime)
        {
            var hits = new List<BattleHitTargetInfo>();
            long totalDamage = 0;
            foreach (int id in runtime.State.TargetRuntimeIds)
            {
                int index = ResolveEnemyRuntimeIndexById(id);
                if (index < 0) continue;
                var enemy = activeEnemyRuntimes[index];
                if (enemy?.Stats == null || enemy.Stats.IsDead()) continue;
                int damage = Class5SkillCatalog.CalculateDamage(runtime.State.CastOffense, runtime.Definition.PowerPerHit,
                    GuardianTrialDefense(enemy, runtime.Definition.DamageType), runtime.State.CastPowerMultiplier);
                damage = AbsorbGuardianTrialBarrier(enemy, damage);
                int actualDamage = Mathf.Min(damage, enemy.Stats.CurrentHp);
                enemy.Stats.ApplyDamage(damage);
                RecordGuardianDamage(runtime.Ally, enemy, actualDamage);
                if (index == 0) SyncLeadEnemyState();
                if (!enemy.Stats.IsDead() && runtime.Definition.MovementSlow > 0f)
                    ApplyClass5Slow(enemy.RuntimeId, runtime.Definition.MovementSlow, runtime.Definition.MovementSlowDuration);
                hits.Add(new BattleHitTargetInfo(index, damage, id));
                totalDamage += damage;
            }
            if (hits.Count == 0) return;
            RaiseHitResolved(new BattleHitInfo(false, (int)Math.Min(int.MaxValue, totalDamage), false, true, false,
                hits[0].TargetIndex, runtime.Ally.SlotIndex, hits, ResolvePlayerPresentationDelay(runtime.Ally.Data),
                hits[0].TargetRuntimeId, runtime.Ally.RuntimeId, runtime.Definition.SkillId));
        }

        private void ApplyClass5Slow(int enemyRuntimeId, float reduction, float duration)
        {
            if (reduction <= 0f || duration <= 0f) return;
            if (!class5Slows.TryGetValue(enemyRuntimeId, out var state))
            {
                state = new Class5SlowState { EnemyRuntimeId = enemyRuntimeId };
                class5Slows[enemyRuntimeId] = state;
            }
            state.Reduction = Mathf.Max(state.Reduction, Mathf.Clamp01(reduction));
            state.Remaining = Mathf.Max(state.Remaining, duration);
        }

        private float ResolveClass5EnemyMoveSpeed(EnemyRuntime enemy) => enemy != null &&
            class5Slows.TryGetValue(enemy.RuntimeId, out var slow) && slow.Remaining > 0f
            ? enemy.MoveSpeed * (1f - slow.Reduction) : enemy?.MoveSpeed ?? 0f;

        private int ApplyClass5DamageReduction(AllyRuntime targetAlly, int damage)
        {
            if (damage <= 0 || targetAlly == null || !class5Skills.TryGetValue(targetAlly.SlotIndex, out var runtime)
                || runtime.State.DamageReductionRemaining <= 0f) return damage;
            return Mathf.Max(1, Mathf.RoundToInt(damage * (1f - runtime.Definition.DamageReduction)));
        }

        private void CaptureClass5SkillState(DailyChallengeCheckpoint checkpoint)
        {
            var skills = new List<Class5SkillState>();
            foreach (var ally in activeAllyRuntimes)
                if (class5Skills.TryGetValue(ally.SlotIndex, out var runtime)) skills.Add(CloneClass5State(runtime.State));
            checkpoint.Class5Skills = skills.ToArray();
            var slows = new List<Class5SlowState>();
            foreach (var slow in class5Slows.Values)
                if (slow.Remaining > 0f && ResolveEnemyRuntimeIndexById(slow.EnemyRuntimeId) >= 0)
                    slows.Add(new Class5SlowState { EnemyRuntimeId = slow.EnemyRuntimeId, Reduction = slow.Reduction, Remaining = slow.Remaining });
            checkpoint.Class5Slows = slows.ToArray();
        }

        private void RestoreClass5SkillState(DailyChallengeCheckpoint checkpoint)
        {
            if (checkpoint == null) return;
            var restoredInstances = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in checkpoint.Class5Skills ?? Array.Empty<Class5SkillState>())
            {
                if (saved == null || string.IsNullOrEmpty(saved.MonsterInstanceId) || !restoredInstances.Add(saved.MonsterInstanceId)) continue;
                foreach (var runtime in class5Skills.Values)
                {
                    if (!string.Equals(runtime.State.MonsterInstanceId, saved.MonsterInstanceId, StringComparison.Ordinal)) continue;
                    var state = CloneClass5State(saved);
                    state.CooldownRemaining = Class5Finite(state.CooldownRemaining, runtime.Definition.Cooldown);
                    state.DamageReductionRemaining = Class5Finite(state.DamageReductionRemaining, runtime.Definition.DamageReductionDuration);
                    state.MotionRemaining = checkpoint.StageComplete ? 0f : Class5Finite(state.MotionRemaining, Class5SkillCatalog.MotionDuration);
                    state.PendingElapsed = Class5Finite(state.PendingElapsed, Class5SkillCatalog.MotionDuration);
                    state.NextHitIndex = Mathf.Clamp(state.NextHitIndex, 0, runtime.Definition.HitCount);
                    state.CastOffense = Class5Finite(state.CastOffense, int.MaxValue);
                    state.CastPowerMultiplier = Mathf.Max(1f, Class5Finite(state.CastPowerMultiplier, 1.4f));
                    var targets = new List<int>();
                    if (!checkpoint.StageComplete)
                        foreach (int id in state.TargetRuntimeIds)
                            if (ResolveEnemyRuntimeIndexById(id) >= 0 && !targets.Contains(id) && targets.Count < runtime.Definition.MaxTargets) targets.Add(id);
                    state.TargetRuntimeIds = targets.ToArray();
                    if (targets.Count == 0) state.NextHitIndex = runtime.Definition.HitCount;
                    runtime.State = state;
                    break;
                }
            }
            class5Slows.Clear();
            if (checkpoint.StageComplete) return;
            foreach (var saved in checkpoint.Class5Slows ?? Array.Empty<Class5SlowState>())
                if (saved != null && ResolveEnemyRuntimeIndexById(saved.EnemyRuntimeId) >= 0)
                    ApplyClass5Slow(saved.EnemyRuntimeId, Class5Finite(saved.Reduction, .3f), Class5Finite(saved.Remaining, 4f));
        }

        private static Class5SkillState CloneClass5State(Class5SkillState source) => new Class5SkillState
        {
            MonsterInstanceId = source.MonsterInstanceId, CooldownRemaining = source.CooldownRemaining,
            MotionRemaining = source.MotionRemaining, DamageReductionRemaining = source.DamageReductionRemaining,
            PendingElapsed = source.PendingElapsed, NextHitIndex = source.NextHitIndex,
            CastOffense = source.CastOffense, CastPowerMultiplier = source.CastPowerMultiplier,
            TargetRuntimeIds = source.TargetRuntimeIds != null ? (int[])source.TargetRuntimeIds.Clone() : Array.Empty<int>()
        };

        private static float Class5Finite(float value, float maximum) => float.IsNaN(value) || float.IsInfinity(value)
            ? 0f : Mathf.Clamp(value, 0f, maximum);
    }
}
