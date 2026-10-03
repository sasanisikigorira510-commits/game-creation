using System;
using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public sealed partial class BattleSimulator
    {
        private const float GuardianGaugeMaximum = 100f;
        private const float GuardianPassiveGainPerSecond = 12.5f;
        private const float GuardianActionGainPerSecond = 12.5f;
        private string guardianId;
        private string guardianContractId;
        private int guardianLevel;
        private float guardianResonance;
        private float guardianCombatTime;
        private float guardianLastSkillTime;
        private float guardianBurnTimer;
        private float guardianBurnRemaining;
        private int guardianBurnDamage;
        private float guardianBarrierRemaining;
        private float guardianSupportRemaining;
        private int guardianMarkedEnemyId = -1;
        private readonly Dictionary<int, int> guardianBarriers = new Dictionary<int, int>();
        private readonly HashSet<int> guardianBurnTargets = new HashSet<int>();
        private readonly HashSet<int> guardianFollowupSlots = new HashSet<int>();
        private readonly Queue<GuardianResonanceGain> guardianActionGains = new Queue<GuardianResonanceGain>();
        private float guardianActionGainWindow;
        private struct GuardianResonanceGain { public float Time; public float Amount; }

        public event Action<string> GuardianSkillUsed;
        public int GuardianSkillCount { get; private set; }
        public Vector2 LastGuardianSkillTargetAnchor { get; private set; }
        public int LastGuardianSkillTargetRuntimeId { get; private set; } = -1;
        public int GuardianDamageDealt { get; private set; }
        public int GuardianDamageBlocked { get; private set; }
        public int GuardianSupportedAttackCount { get; private set; }
        public int GuardianBurnAssistKillCount { get; private set; }
        public string GuardianId => guardianId ?? string.Empty;
        public float GuardianResonance => guardianResonance;
        public float GuardianResonanceNormalized => guardianResonance / GuardianGaugeMaximum;
        public string GuardianContractTypeId => guardianContractId ?? "basic";
        public float GuardianSupportRemaining => guardianId == "suzaku" ? guardianBurnRemaining :
            guardianId == "genbu" ? guardianBarrierRemaining : guardianSupportRemaining;
        public MonsterDataSO GuardianMonsterData => string.IsNullOrEmpty(guardianId) ? null : GuardianService.Monster(guardianId);
        // Estimate from passive gain only; allied actions can bring the next invocation forward.
        public float GuardianSkillRemaining => Mathf.Max(
            (GuardianGaugeMaximum - guardianResonance) / GuardianPassiveGainPerSecond,
            (GuardianSkillCount == 0 ? 2f : 4f) - (guardianCombatTime - guardianLastSkillTime));
        public string GuardianContributionLabel => guardianId == "suzaku" ? "炎上中の敵を仲間が撃破" :
            guardianId == "genbu" ? "障壁で防いだダメージ" : guardianId == "byakko" ?
            (guardianContractId == "alternate" ? "追討が乗った攻撃" : "牙印への会心攻撃") :
            (guardianContractId == "alternate" ? "追撃が乗った攻撃" : "追風中の仲間の攻撃");
        public int GuardianContributionValue => guardianId == "suzaku" ? GuardianBurnAssistKillCount :
            guardianId == "genbu" ? GuardianDamageBlocked : GuardianSupportedAttackCount;

        public bool GuardianHasBarrier(int allySlot)
        {
            var ally = ResolveAllyRuntimeBySlotIndex(allySlot);
            return ally?.Stats != null && !ally.Stats.IsDead() && guardianBarrierRemaining > 0f &&
                guardianBarriers.TryGetValue(allySlot, out int shield) && shield > 0;
        }

        public bool GuardianIsBurning(int enemyIndex) => enemyIndex >= 0 && enemyIndex < activeEnemyRuntimes.Count &&
            activeEnemyRuntimes[enemyIndex]?.Stats != null && !activeEnemyRuntimes[enemyIndex].Stats.IsDead() &&
            guardianBurnRemaining > 0f && guardianBurnTargets.Contains(activeEnemyRuntimes[enemyIndex].RuntimeId);

        public bool GuardianHasMark(int enemyIndex) => enemyIndex >= 0 && enemyIndex < activeEnemyRuntimes.Count &&
            activeEnemyRuntimes[enemyIndex]?.Stats != null && !activeEnemyRuntimes[enemyIndex].Stats.IsDead() &&
            guardianId == "byakko" && !IsAlternateGuardianContract && guardianSupportRemaining > 0f &&
            guardianMarkedEnemyId == activeEnemyRuntimes[enemyIndex].RuntimeId;

        public bool GuardianSupportsAlly(int slot)
        {
            var ally = ResolveAllyRuntimeBySlotIndex(slot);
            if (ally?.Stats == null || ally.Stats.IsDead() || IsGuardian(ally)) return false;
            if (guardianId == "seiryu") return guardianSupportRemaining > 0f &&
                (!IsAlternateGuardianContract || guardianFollowupSlots.Contains(slot));
            if (guardianId == "genbu") return IsAlternateGuardianContract && GuardianHasBarrier(slot);
            if (guardianId == "byakko" && IsAlternateGuardianContract) return guardianSupportRemaining > 0f;
            // Suzaku and basic Byakko mark the actual affected enemy instead of
            // implying an unconditional modifier on an allied unit.
            return false;
        }

        private static bool IsGuardian(AllyRuntime ally) => ally != null && ally.SlotIndex == GuardianService.BattleSlot;
        private bool IsAlternateGuardianContract => guardianContractId == "alternate";

        private void SetupGuardian()
        {
            var profile = GameManager.Instance?.PlayerProfile;
            var guardian = GuardianService.Equipped(profile);
            bool practice = GuardianTrialSession.IsPractice;
            guardianId = practice ? GuardianTrialSession.GuardianId : guardian?.Id;
            guardianLevel = practice ? GuardianTrialSession.LevelOverride : GuardianService.Level(profile, guardianId);
            guardianContractId = practice ? GuardianTrialSession.ContractId : GuardianService.ContractId(profile, guardianId);
            guardianResonance = string.IsNullOrEmpty(guardianId) ? 0f : 50f;
            guardianCombatTime = guardianLastSkillTime = 0f;
            guardianBurnTimer = guardianBurnRemaining = guardianBarrierRemaining = guardianSupportRemaining = 0f;
            guardianBurnDamage = 0;
            guardianMarkedEnemyId = -1;
            guardianBarriers.Clear(); guardianBurnTargets.Clear(); guardianFollowupSlots.Clear();
            guardianActionGains.Clear(); guardianActionGainWindow = 0f;
            GuardianSkillCount = GuardianDamageDealt = GuardianDamageBlocked = 0;
            LastGuardianSkillTargetAnchor = Vector2.zero;
            LastGuardianSkillTargetRuntimeId = -1;
            GuardianSupportedAttackCount = GuardianBurnAssistKillCount = 0;
            if (string.IsNullOrEmpty(guardianId)) return;
            battleSpiritModifier = GuardianService.Modifier(guardianId);
            battleSpiritInvoked = true;
            invokedBattleSpiritDefinition = GuardianService.Find(guardianId);
        }

        private void CreateGuardianRuntime()
        {
            if (string.IsNullOrEmpty(guardianId)) return;
            var data = GuardianService.Monster(guardianId);
            var anchor = BattleFormationLayout.GuardianRearAnchor;
            var stats = GuardianService.Stats(guardianId, guardianLevel);
            activeAllyRuntimes.Add(new AllyRuntime { RuntimeId = nextAllyRuntimeId++, SlotIndex = GuardianService.BattleSlot,
                Data = data, Stats = stats, HomeAnchor = anchor, PositionAnchor = anchor,
                CombatRadius = ResolveAllyCombatRadius(data), AttackReachAnchor = ResolveAllyAttackReach(data),
                SearchReachAnchor = ResolveAllySearchReach(data, GuardianService.BattleSlot), MoveSpeed = 0f });
        }

        private void TickGuardianSkill(float dt)
        {
            dt = Mathf.Max(0f, dt);
            guardianCombatTime += dt;
            ExpireGuardianActionGains();
            TickGuardianOngoingEffects(dt);
            var guardian = ResolveAllyRuntimeBySlotIndex(GuardianService.BattleSlot);
            if (guardian == null || guardian.Stats.IsDead() || !HasLivingGuardianTarget()) return;
            guardianResonance = Mathf.Min(GuardianGaugeMaximum, guardianResonance + GuardianPassiveGainPerSecond * dt);
            TryActivateGuardianSkill(guardian);
        }

        private bool HasLivingGuardianTarget()
        {
            foreach (var enemy in activeEnemyRuntimes)
                if (enemy?.Stats != null && !enemy.Stats.IsDead()) return true;
            return false;
        }

        private void TickGuardianOngoingEffects(float dt)
        {
            // Cast effects have their own lifetime. Losing the caster never freezes or
            // cancels a shield, burn, or support effect that is already in flight.
            guardianBarrierRemaining = Mathf.Max(0f, guardianBarrierRemaining - dt);
            if (guardianBarrierRemaining <= 0f) guardianBarriers.Clear();
            guardianSupportRemaining = Mathf.Max(0f, guardianSupportRemaining - dt);
            if (guardianSupportRemaining <= 0f)
            {
                guardianFollowupSlots.Clear();
                guardianMarkedEnemyId = -1;
            }
            if (guardianBurnRemaining <= 0f) return;
            guardianBurnTimer += Mathf.Min(dt, guardianBurnRemaining);
            guardianBurnRemaining = Mathf.Max(0f, guardianBurnRemaining - dt);
            while (guardianBurnTimer + .00001f >= 1f)
            {
                guardianBurnTimer -= 1f;
                for (int i = 0; i < activeEnemyRuntimes.Count; i++)
                    if (guardianBurnTargets.Contains(activeEnemyRuntimes[i].RuntimeId))
                        GuardianHit(i, guardianBurnDamage, false, true);
            }
            if (guardianBurnRemaining <= 0f) guardianBurnTargets.Clear();
        }

        private void ExpireGuardianActionGains()
        {
            // Rolling window prevents a boundary burst from bypassing the per-second cap.
            while (guardianActionGains.Count > 0 && guardianActionGains.Peek().Time <= guardianCombatTime - 1f + .00001f)
                guardianActionGainWindow -= guardianActionGains.Dequeue().Amount;
            guardianActionGainWindow = Mathf.Max(0f, guardianActionGainWindow);
        }

        private void AddGuardianActionResonance(float requested)
        {
            var guardian = ResolveAllyRuntimeBySlotIndex(GuardianService.BattleSlot);
            if (guardian == null || guardian.Stats.IsDead()) return;
            ExpireGuardianActionGains();
            float gain = Mathf.Min(requested, GuardianActionGainPerSecond - guardianActionGainWindow,
                GuardianGaugeMaximum - guardianResonance);
            if (gain <= 0f) return;
            guardianActionGains.Enqueue(new GuardianResonanceGain { Time = guardianCombatTime, Amount = gain });
            guardianActionGainWindow += gain;
            guardianResonance += gain;
        }

        // Called once after a complete normal attack action, never per target or visual hit.
        private void RecordGuardianAlliedAttack(AllyRuntime attacker, bool isSkill, bool critical, bool supported)
        {
            if (IsGuardian(attacker) || isSkill) return;
            if (supported) GuardianSupportedAttackCount++;
            if (guardianId == "seiryu") AddGuardianActionResonance(2f);
            else if (guardianId == "suzaku" && ResolvePlayerDamageType(attacker.Data) == MonsterDamageType.Magic)
                AddGuardianActionResonance(4f);
            else if (guardianId == "byakko" && critical) AddGuardianActionResonance(8f);
        }

        private void RecordGuardianIncomingAttack(AllyRuntime target, int effectiveDamageOrAbsorption)
        {
            if (guardianId == "genbu" && !IsGuardian(target) && effectiveDamageOrAbsorption > 0)
                AddGuardianActionResonance(3f);
        }

        private float GuardianAllyAttackRate(AllyRuntime ally) => guardianId == "seiryu" &&
            !IsAlternateGuardianContract && guardianSupportRemaining > 0f && !IsGuardian(ally) ? 1.15f : 1f;

        private bool BeginGuardianSupportedAttack(AllyRuntime attacker, bool isSkill)
        {
            if (isSkill || IsGuardian(attacker) || guardianId != "seiryu" || guardianSupportRemaining <= 0f) return false;
            return !IsAlternateGuardianContract || guardianFollowupSlots.Remove(attacker.SlotIndex);
        }

        private int ApplyGuardianNormalAttackBonus(AllyRuntime attacker, EnemyRuntime enemy, int damage,
            bool critical, bool isSkill, bool seiryuSupport, out bool supported)
        {
            supported = seiryuSupport;
            if (isSkill || IsGuardian(attacker)) return damage;
            float multiplier = seiryuSupport && IsAlternateGuardianContract ? 1.25f : 1f;
            if (guardianId == "suzaku" && guardianBurnRemaining > 0f && guardianBurnTargets.Contains(enemy.RuntimeId) &&
                ResolvePlayerDamageType(attacker.Data) == MonsterDamageType.Magic)
                multiplier = IsAlternateGuardianContract ? 1.30f : 1.15f;
            else if (guardianId == "byakko" && guardianSupportRemaining > 0f)
            {
                supported = IsAlternateGuardianContract
                    ? (long)enemy.Stats.CurrentHp * 100 <= (long)enemy.Stats.MaxHp * 30
                    : enemy.RuntimeId == guardianMarkedEnemyId && critical;
                if (supported) multiplier = 1.20f;
            }
            else if (guardianId == "genbu" && IsAlternateGuardianContract && guardianBarrierRemaining > 0f &&
                guardianBarriers.TryGetValue(attacker.SlotIndex, out int shield) && shield > 0)
                multiplier = 1.20f;
            return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
        }

        private void RecordGuardianDamage(AllyRuntime attacker, EnemyRuntime enemy, int actualDamage)
        {
            if (IsGuardian(attacker)) GuardianDamageDealt += actualDamage;
            else if (actualDamage > 0 && enemy.Stats.IsDead() && guardianId == "suzaku" &&
                guardianBurnRemaining > 0f && guardianBurnTargets.Contains(enemy.RuntimeId)) GuardianBurnAssistKillCount++;
        }

        private void TryActivateGuardianSkill(AllyRuntime guardian)
        {
            if (guardianResonance + .0001f < GuardianGaugeMaximum ||
                guardianCombatTime - guardianLastSkillTime + .0001f < (GuardianSkillCount == 0 ? 2f : 4f)) return;
            int target = ResolveAllyTargetEnemyIndex(guardian, GuardianService.BattleSlot);
            if (target < 0 || !CanAllyAttackTarget(guardian, target)) return;
            // Capture the cast target before damage. Presentation is raised after
            // resolution, when the original enemy may already be defeated.
            int castTarget = guardianId == "byakko" || (guardianId == "suzaku" && IsAlternateGuardianContract)
                ? GuardianStrongestTarget(guardian, target) : target;
            LastGuardianSkillTargetAnchor = guardianId == "genbu" ? guardian.PositionAnchor : activeEnemyRuntimes[castTarget].PositionAnchor;
            LastGuardianSkillTargetRuntimeId = guardianId == "genbu" ? guardian.RuntimeId : activeEnemyRuntimes[castTarget].RuntimeId;
            guardianResonance = 0f;
            guardianLastSkillTime = guardianCombatTime;
            GuardianSkillCount++;
            if (guardianId == "genbu")
            {
                guardianBarrierRemaining = 6f;
                guardianBarriers.Clear();
                foreach (var ally in activeAllyRuntimes)
                    if (!ally.Stats.IsDead()) guardianBarriers[ally.SlotIndex] =
                        Mathf.RoundToInt(guardian.Stats.MaxHp * (IsAlternateGuardianContract ? .12f : .18f));
            }
            else if (guardianId == "suzaku")
            {
                guardianBurnTargets.Clear(); guardianBurnTimer = 0f; guardianBurnRemaining = 4f;
                guardianBurnDamage = Mathf.Max(1, guardian.Stats.Wisdom / 4);
                var targets = IsAlternateGuardianContract ? new List<int> { castTarget }
                    : CollectEnemyTargetIndices(6, guardian, GuardianService.BattleSlot);
                foreach (int i in targets)
                {
                    var enemy = activeEnemyRuntimes[i];
                    guardianBurnTargets.Add(enemy.RuntimeId);
                    GuardianHit(i, Mathf.Max(1, guardian.Stats.Wisdom * 2 - enemy.Stats.MagicDefense), false);
                }
            }
            else if (guardianId == "byakko")
            {
                int strongest = castTarget;
                guardianSupportRemaining = 4f;
                guardianMarkedEnemyId = IsAlternateGuardianContract ? -1 : activeEnemyRuntimes[strongest].RuntimeId;
                GuardianHit(strongest, guardian.Stats.Attack * 3, true);
            }
            else
            {
                guardianSupportRemaining = IsAlternateGuardianContract ? 4f : 3f;
                guardianFollowupSlots.Clear();
                if (IsAlternateGuardianContract)
                    foreach (var ally in activeAllyRuntimes)
                        if (!IsGuardian(ally) && !ally.Stats.IsDead()) guardianFollowupSlots.Add(ally.SlotIndex);
                for (int hit = 0; hit < 3; hit++)
                {
                    int next = ResolveAllyTargetEnemyIndex(guardian, GuardianService.BattleSlot);
                    if (next >= 0 && CanAllyAttackTarget(guardian, next))
                        GuardianHit(next, Mathf.Max(1, guardian.Stats.Attack - activeEnemyRuntimes[next].Stats.Defense / 2), false);
                }
            }
            LockAttackMotion(guardian);
            GuardianSkillUsed?.Invoke(GuardianService.Skill(guardianId));
            SyncLeadEnemyState();
        }

        private int GuardianStrongestTarget(AllyRuntime guardian, int fallback)
        {
            int strongest = fallback;
            for (int i = 0; i < activeEnemyRuntimes.Count; i++)
                if (CanAllyAttackTarget(guardian, i) && activeEnemyRuntimes[i].Stats.MaxHp > activeEnemyRuntimes[strongest].Stats.MaxHp)
                    strongest = i;
            return strongest;
        }

        private void GuardianHit(int target, int damage, bool critical, bool periodic = false)
        {
            if (target < 0 || target >= activeEnemyRuntimes.Count || activeEnemyRuntimes[target].Stats.IsDead()) return;
            damage = Mathf.Min(AbsorbGuardianTrialBarrier(activeEnemyRuntimes[target], damage), activeEnemyRuntimes[target].Stats.CurrentHp);
            activeEnemyRuntimes[target].Stats.ApplyDamage(damage);
            GuardianDamageDealt += damage;
            RaiseHitResolved(new BattleHitInfo(false, damage, critical, true, false, target, periodic ? -1 : GuardianService.BattleSlot));
        }

        private int AbsorbGuardianBarrier(int slot, int damage)
        {
            if (guardianBarrierRemaining <= 0f || !guardianBarriers.TryGetValue(slot, out int shield)) return damage;
            int absorbed = Mathf.Min(shield, Mathf.Max(0, damage));
            guardianBarriers[slot] = shield - absorbed;
            GuardianDamageBlocked += absorbed;
            return damage - absorbed;
        }
    }
}
