using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public sealed partial class BattleSimulator
    {
        private readonly Dictionary<int, EnemyDataSO> dailyEnemies = new Dictionary<int, EnemyDataSO>();
        private void OnDestroy() => ClearDailyEnemyData();
        private void ClearDailyEnemyData()
        {
            foreach (var data in dailyEnemies.Values)
                if (data != null) { if (Application.isPlaying) Destroy(data); else DestroyImmediate(data); }
            dailyEnemies.Clear();
        }
        private EnemyDataSO DailyEnemy(int index)
        {
            if (!dailyEnemies.TryGetValue(index, out var data))
                dailyEnemies[index] = data = DailyChallengeCatalog.CreateEnemy(DailyChallengeSession.Run.Mode, currentFloor, index);
            return data;
        }

        private void SetupDailyChallenge()
        {
            var run = DailyChallengeSession.Run;
            var checkpoint = run.Checkpoint;
            bool hasFrozenParty = checkpoint?.Parties != null && checkpoint.Parties.Any(p => p?.Stats != null && p.Stats.MaxHp > 0);
            bool resumeCombat = hasFrozenParty && !checkpoint.StageComplete && checkpoint.Stage == run.NextStage;
            ClearDailyEnemyData();
            currentFloor = Mathf.Clamp(run.NextStage, 1, DailyChallengeCatalog.StageCount);
            currentWave = 1;
            isBossEncounter = run.Mode == DailyChallengeCatalog.Champion;
            finalBossMonsterIds = Array.Empty<string>();
            currentEnemyIsBoss = false;
            encounterEnemyCountTarget = DailyChallengeCatalog.EnemyCount(run.Mode, currentFloor);
            defeatedEnemiesInCurrentWave = resumeCombat ? checkpoint.DefeatedEnemies : 0;
            spawnedEnemiesInCurrentWave = resumeCombat ? checkpoint.SpawnedEnemies : 0;
            defeatedEnemyMaxHpInCurrentWave = resumeCombat ? checkpoint.DefeatedEnemyMaxHp : 0;
            activeEnemiesInCurrentWave = engagedEnemyCount = 0;
            encounterSerial = 0;
            enemySpawnTimer = resumeCombat ? checkpoint.EnemySpawnTimer : run.ClearedStage == 0 ? 0f : .65f;
            openingBurstSpawned = resumeCombat && checkpoint.OpeningBurstSpawned;
            nextAllyRuntimeId = 1;
            nextEnemyRuntimeId = Math.Max(1, checkpoint?.NextEnemyRuntimeId ?? 1);
            battleSpiritModifier = BattleSpiritModifier.Identity;
            battleSpiritInvoked = false;
            battleSpiritGauge = 0f;
            invokedBattleSpiritDefinition = null;
            SetupGuardian();
            SetupGuardianTrial();
            if (hasFrozenParty && checkpoint?.Guardian != null)
            {
                guardianId = checkpoint.Guardian.Id;
                guardianContractId = checkpoint.Guardian.ContractId;
                guardianLevel = checkpoint.Guardian.Level;
                battleSpiritModifier = string.IsNullOrEmpty(guardianId) ? BattleSpiritModifier.Identity : GuardianService.Modifier(guardianId);
                battleSpiritInvoked = !string.IsNullOrEmpty(guardianId);
                invokedBattleSpiritDefinition = GuardianService.Find(guardianId);
            }
            activeAllyRuntimes.Clear();
            var profile = GameManager.Instance?.PlayerProfile;
            MasterDataManager.Instance?.Initialize();
            for (int partyIndex = 0; partyIndex < run.PartyInstanceIds.Length && partyIndex < PlayerProfile.PartySlotCount; partyIndex++)
            {
                var saved = checkpoint?.Parties?.FirstOrDefault(p => p != null && p.InstanceId == run.PartyInstanceIds[partyIndex]);
                var owned = profile?.GetOwnedMonster(run.PartyInstanceIds[partyIndex]);
                int originalSlot = profile?.PartyMonsterInstanceIds?.IndexOf(run.PartyInstanceIds[partyIndex]) ?? -1;
                int slot = saved != null ? saved.Slot : originalSlot >= 0 ? originalSlot : partyIndex;
                slot = Mathf.Clamp(slot, 0, PlayerProfile.PartySlotCount - 1);
                var data = MasterDataManager.Instance?.GetMonsterData(saved?.MonsterId ?? owned?.MonsterId);
                if (data == null) continue;
                var stats = saved?.Stats != null ? RestoreStats(saved.Stats) : MonsterBattleStatsFactory.Create(profile, owned, data);
                if (saved?.Stats == null) ApplySpiritStatModifiers(stats);
                var anchor = ResolveAllyHomeAnchor(slot, data);
                var ally = new AllyRuntime { RuntimeId = saved?.RuntimeId ?? nextAllyRuntimeId, SlotIndex = slot,
                    Stats = stats, Data = data, OwnedMonster = owned, HomeAnchor = anchor, PositionAnchor = anchor,
                    CombatRadius = ResolveAllyCombatRadius(data), AttackReachAnchor = ResolveAllyAttackReach(data),
                    SearchReachAnchor = ResolveAllySearchReach(data, slot), MoveSpeed = ResolveAllyMoveSpeed(data) };
                RestoreDailyAlly(ally, saved, resumeCombat);
                nextAllyRuntimeId = Math.Max(nextAllyRuntimeId, ally.RuntimeId + 1);
                activeAllyRuntimes.Add(ally);
            }
            CreateGuardianRuntime();
            if (hasFrozenParty && checkpoint?.Guardian != null) RestoreDailyGuardian(checkpoint.Guardian, resumeCombat);
            skillSet = new BattleSkillSet(battleSpiritModifier.SkillCooldownMultiplier);
            foreach (var skill in checkpoint?.PlayerSkills ?? Array.Empty<DailyChallengePlayerSkillSnapshot>())
                if (skill != null && Enum.IsDefined(typeof(BattleSkillType), skill.SkillType))
                    skillSet.Get((BattleSkillType)skill.SkillType).RestoreCooldown(skill.CooldownRemaining);
            guardRemainingTime = Math.Max(0f, checkpoint?.GuardRemaining ?? 0f);
            enemyAttackTimer = 0f;
            enemyStats = null;
            currentEnemyData = null;
            enemyTraitRuntime = default;
            activeEnemyRuntimes.Clear();
            BuildAnticipatedEnemyMaxHpPlan();
            for (int i = 0; i < spawnedEnemiesInCurrentWave; i++) ConsumeAnticipatedEnemyMaxHp(i);
            if (resumeCombat)
                foreach (var saved in checkpoint.Enemies ?? Array.Empty<DailyChallengeEnemySnapshot>())
                {
                    if (saved?.Stats == null) continue;
                    var data = DailyEnemy(saved.SpawnIndex);
                    var anchor = new Vector2(EnemySpawnX, ResolveEnemySpawnLaneY(saved.SpawnIndex));
                    var enemy = new EnemyRuntime { RuntimeId = saved.RuntimeId, DailySpawnIndex = saved.SpawnIndex,
                        Data = data, Stats = RestoreStats(saved.Stats), Trait = EnemyTraitResolver.Resolve(data.enemyTrait),
                        IsBoss = isBossEncounter, AttackTimer = saved.AttackTimer,
                        AttackMotionLockRemaining = saved.AttackMotionLockRemaining,
                        HomeAnchor = anchor, PositionAnchor = new Vector2(saved.PositionX, saved.PositionY),
                        TargetAllyRuntimeId = saved.TargetRuntimeId, CombatRadius = ResolveEnemyCombatRadius(data),
                        AttackReachAnchor = ResolveEnemyAttackReach(data), MoveSpeed = EnemyMoveSpeed * MonsterMoveSpeedMultiplier };
                    enemy.Stats.CurrentHp = Mathf.Clamp(saved.CurrentHp, 0, enemy.Stats.MaxHp);
                    activeEnemyRuntimes.Add(enemy);
                    nextEnemyRuntimeId = Math.Max(nextEnemyRuntimeId, saved.RuntimeId + 1);
                }
            ClearEnemyMovementQueueCache();
            activeEnemiesInCurrentWave = activeEnemyRuntimes.Count;
            SyncPlayerAggregateState();
            SyncLeadEnemyState();
            InitializeClass5Skills();
            if (checkpoint != null) RestoreClass5SkillState(checkpoint);
            isRunning = run.NextStage <= DailyChallengeCatalog.StageCount && activeAllyRuntimes.Count > 0;
            // Stage-zero records fix the equipped guardian, combat stats, formation and timers.
            if (!resumeCombat && run.NextStage <= DailyChallengeCatalog.StageCount)
                DailyChallengeSession.SetCheckpoint(CaptureDailyChallengeCheckpoint(false));
        }

        private void TickDailyEnemySpawns(float deltaTime)
        {
            int cap = DailyChallengeCatalog.LivingCap(DailyChallengeSession.Run.Mode);
            if (spawnedEnemiesInCurrentWave >= CurrentEnemyCountTarget || activeEnemyRuntimes.Count >= cap) return;
            enemySpawnTimer += Math.Max(0f, deltaTime);
            const float interval = .65f;
            while (enemySpawnTimer >= interval && activeEnemyRuntimes.Count < cap && spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget)
            {
                enemySpawnTimer -= interval;
                int burst = !openingBurstSpawned ? Math.Min(12, CurrentEnemyCountTarget) : 6;
                openingBurstSpawned = true;
                for (int i = 0; i < burst && activeEnemyRuntimes.Count < cap && spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget; i++)
                    QueueEnemySpawn(false);
            }
        }

        public DailyChallengeCheckpoint CaptureDailyChallengeCheckpoint(bool stageComplete)
        {
            var result = new DailyChallengeCheckpoint { Stage = currentFloor, StageComplete = stageComplete,
                DefeatedEnemies = defeatedEnemiesInCurrentWave, SpawnedEnemies = spawnedEnemiesInCurrentWave,
                DefeatedEnemyMaxHp = defeatedEnemyMaxHpInCurrentWave, EnemySpawnTimer = enemySpawnTimer,
                OpeningBurstSpawned = openingBurstSpawned, NextAllyRuntimeId = nextAllyRuntimeId,
                NextEnemyRuntimeId = nextEnemyRuntimeId, GuardRemaining = Math.Max(0f, guardRemainingTime),
                Parties = activeAllyRuntimes.Where(a => a != null && !IsGuardian(a)).Select(CaptureDailyAlly).ToArray(),
                Enemies = activeEnemyRuntimes.Where(e => e?.Stats != null).Select(e => new DailyChallengeEnemySnapshot {
                    SpawnIndex = e.DailySpawnIndex, RuntimeId = e.RuntimeId, CurrentHp = e.Stats.CurrentHp,
                    Stats = CaptureStats(e.Stats), AttackTimer = e.AttackTimer,
                    AttackMotionLockRemaining = e.AttackMotionLockRemaining, PositionX = e.PositionAnchor.x,
                    PositionY = e.PositionAnchor.y, TargetRuntimeId = e.TargetAllyRuntimeId }).ToArray(),
                PlayerSkills = skillSet == null ? Array.Empty<DailyChallengePlayerSkillSnapshot>() :
                    Enum.GetValues(typeof(BattleSkillType)).Cast<BattleSkillType>().Select(s =>
                        new DailyChallengePlayerSkillSnapshot { SkillType = (int)s, CooldownRemaining = skillSet.Get(s).RemainingCooldown }).ToArray(),
                Guardian = CaptureDailyGuardian() };
            CaptureClass5SkillState(result);
            return result;
        }

        private DailyChallengePartySnapshot CaptureDailyAlly(AllyRuntime ally) => new DailyChallengePartySnapshot {
            InstanceId = ally.OwnedMonster?.InstanceId, MonsterId = ally.Data?.monsterId, Slot = ally.SlotIndex,
            RuntimeId = ally.RuntimeId, CurrentHp = ally.Stats.CurrentHp, Stats = CaptureStats(ally.Stats),
            AttackTimer = ally.AttackTimer, AttackCooldownRemaining = Math.Max(0f, GetCurrentPlayerAttackInterval(ally.Stats) - ally.AttackTimer),
            AttackMotionLockRemaining = ally.AttackMotionLockRemaining, PositionX = ally.PositionAnchor.x,
            PositionY = ally.PositionAnchor.y, TargetRuntimeId = ally.TargetEnemyRuntimeId };

        private void RestoreDailyAlly(AllyRuntime ally, DailyChallengePartySnapshot saved, bool resumeCombat)
        {
            if (saved == null) return;
            ally.Stats.CurrentHp = Mathf.Clamp(saved.CurrentHp, 0, ally.Stats.MaxHp);
            ally.AttackTimer = Math.Max(0f, saved.AttackTimer);
            ally.AttackMotionLockRemaining = Math.Max(0f, saved.AttackMotionLockRemaining);
            if (resumeCombat)
            {
                ally.PositionAnchor = new Vector2(saved.PositionX, saved.PositionY);
                ally.TargetEnemyRuntimeId = saved.TargetRuntimeId;
            }
        }

        private static DailyChallengeStatsSnapshot CaptureStats(BattleUnitStats stats) => new DailyChallengeStatsSnapshot {
            MaxHp = stats.MaxHp, CurrentHp = stats.CurrentHp, Attack = stats.Attack, Wisdom = stats.Wisdom,
            Defense = stats.Defense, MagicDefense = stats.MagicDefense, AttackSpeed = stats.AttackSpeed,
            CritRate = stats.CritRate, CritDamage = stats.CritDamage };
        private static BattleUnitStats RestoreStats(DailyChallengeStatsSnapshot stats) => new BattleUnitStats {
            MaxHp = Math.Max(1, stats.MaxHp), CurrentHp = Mathf.Clamp(stats.CurrentHp, 0, Math.Max(1, stats.MaxHp)),
            Attack = stats.Attack, Wisdom = stats.Wisdom, Defense = stats.Defense, MagicDefense = stats.MagicDefense,
            AttackSpeed = stats.AttackSpeed, CritRate = stats.CritRate, CritDamage = stats.CritDamage };

        private DailyChallengeGuardianSnapshot CaptureDailyGuardian()
        {
            var unit = activeAllyRuntimes.FirstOrDefault(IsGuardian);
            var gains = guardianActionGains.ToArray();
            return new DailyChallengeGuardianSnapshot { Id = guardianId, ContractId = guardianContractId, Level = guardianLevel,
                Unit = unit == null ? null : CaptureDailyAlly(unit), Resonance = guardianResonance,
                CombatTime = guardianCombatTime, LastSkillTime = guardianLastSkillTime, BurnTimer = guardianBurnTimer,
                BurnRemaining = guardianBurnRemaining, BurnDamage = guardianBurnDamage, BarrierRemaining = guardianBarrierRemaining,
                SupportRemaining = guardianSupportRemaining, MarkedEnemyId = guardianMarkedEnemyId,
                SkillCount = GuardianSkillCount, DamageDealt = GuardianDamageDealt, DamageBlocked = GuardianDamageBlocked,
                SupportedAttackCount = GuardianSupportedAttackCount, BurnAssistKillCount = GuardianBurnAssistKillCount,
                BurnTargets = guardianBurnTargets.ToArray(), FollowupSlots = guardianFollowupSlots.ToArray(),
                BarrierSlots = guardianBarriers.Keys.ToArray(), BarrierAmounts = guardianBarriers.Values.ToArray(),
                ActionGainTimes = gains.Select(g => g.Time).ToArray(), ActionGainAmounts = gains.Select(g => g.Amount).ToArray(),
                ActionGainWindow = guardianActionGainWindow };
        }

        private void RestoreDailyGuardian(DailyChallengeGuardianSnapshot saved, bool resumeCombat)
        {
            var ally = activeAllyRuntimes.FirstOrDefault(IsGuardian);
            if (ally != null && saved.Unit?.Stats != null)
            {
                ally.RuntimeId = saved.Unit.RuntimeId;
                ally.Stats = RestoreStats(saved.Unit.Stats);
                RestoreDailyAlly(ally, saved.Unit, resumeCombat);
                nextAllyRuntimeId = Math.Max(nextAllyRuntimeId, ally.RuntimeId + 1);
            }
            guardianResonance = saved.Resonance; guardianCombatTime = saved.CombatTime;
            guardianLastSkillTime = saved.LastSkillTime; guardianBurnTimer = saved.BurnTimer;
            guardianBurnRemaining = saved.BurnRemaining; guardianBurnDamage = saved.BurnDamage;
            guardianBarrierRemaining = saved.BarrierRemaining; guardianSupportRemaining = saved.SupportRemaining;
            guardianMarkedEnemyId = resumeCombat ? saved.MarkedEnemyId : -1;
            GuardianSkillCount = saved.SkillCount; GuardianDamageDealt = saved.DamageDealt;
            GuardianDamageBlocked = saved.DamageBlocked; GuardianSupportedAttackCount = saved.SupportedAttackCount;
            GuardianBurnAssistKillCount = saved.BurnAssistKillCount;
            guardianBurnTargets.Clear(); guardianFollowupSlots.Clear(); guardianBarriers.Clear(); guardianActionGains.Clear();
            if (resumeCombat) foreach (int id in saved.BurnTargets ?? Array.Empty<int>()) guardianBurnTargets.Add(id);
            foreach (int slot in saved.FollowupSlots ?? Array.Empty<int>()) guardianFollowupSlots.Add(slot);
            for (int i = 0; i < Math.Min(saved.BarrierSlots?.Length ?? 0, saved.BarrierAmounts?.Length ?? 0); i++)
                guardianBarriers[saved.BarrierSlots[i]] = saved.BarrierAmounts[i];
            for (int i = 0; i < Math.Min(saved.ActionGainTimes?.Length ?? 0, saved.ActionGainAmounts?.Length ?? 0); i++)
                guardianActionGains.Enqueue(new GuardianResonanceGain { Time = saved.ActionGainTimes[i], Amount = saved.ActionGainAmounts[i] });
            guardianActionGainWindow = saved.ActionGainWindow;
        }
    }
}
