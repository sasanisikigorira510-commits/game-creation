using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using System.Collections.Generic;

namespace WitchTower.Battle
{
    public sealed class BattleSimulator : MonoBehaviour
    {
        private static readonly string[] DevPartyOverrideMonsterIds =
        {
            "monster_flare_drake",
            "monster_dragon_whelp",
            "monster_abyss_dragon"
        };
        private const string DevEnemyOverrideId = "enemy_slime";
        private static readonly float[] EnemyAttackSlotAngles =
        {
            0f,
            24f,
            -24f,
            48f,
            -48f
        };
        private static readonly float[] SingleLaneSpawnOffsets =
        {
            0f,
            0.075f,
            -0.075f,
            0.14f,
            -0.14f,
            0.04f,
            -0.04f
        };
        private static readonly int[] EnemySpawnLanePattern =
        {
            2,
            3,
            1,
            4,
            2,
            0,
            5,
            3,
            1,
            4,
            2,
            5
        };
        private const float EnemySpawnX = 1.04f;
        private const float AllyMoveSpeed = 0.26f;
        private const float EnemyMoveSpeed = 0.42f;
        private const float AllyReturnSpeed = 0.34f;
        private const float EnemyReturnSpeed = 0.26f;
        private const float MonsterMoveSpeedMultiplier = 0.34f;
        private const float EnemySpawnIntervalMultiplier = 2.0f;
        private const float DefaultAllyCombatRadius = 0.035f;
        private const float DefaultEnemyCombatRadius = 0.037f;
        private const float RangeOffsetPadding = 0.012f;
        private const float PositionEpsilon = 0.0025f;
        private const float ReferenceSpawnInterval = 0.20f;
        private const int ReferenceEncounterEnemyCount = 5;
        private const int ReferenceOpeningSpawnBurst = 3;
        private const int MaxEncounterEnemyCount = 240;
        private const int MinEncounterEnemyCount = 3;
        private const int LargeEncounterEnemyCount = 40;
        private const int MassiveEncounterEnemyCount = 100;
        private const int MaxConcurrentEnemyAttackersPerAlly = 5;
        private const float EnemyQueueSpacing = 0.12f;
        private const float EnemyQueueLaneBlend = 0.34f;
        private const float SparsePartySpawnInterval = 0.26f;
        private const float DuoPartySpawnInterval = 0.22f;

        private sealed class AllyRuntime
        {
            public int RuntimeId;
            public BattleUnitStats Stats;
            public MonsterDataSO Data;
            public OwnedMonsterData OwnedMonster;
            public float AttackTimer;
            public Vector2 HomeAnchor;
            public Vector2 PositionAnchor;
            public int TargetEnemyRuntimeId = -1;
            public float CombatRadius;
            public float AttackReachAnchor;
            public float SearchReachAnchor;
            public float MoveSpeed;
            public bool IsMoving;
        }

        private sealed class EnemyRuntime
        {
            public int RuntimeId;
            public BattleUnitStats Stats;
            public EnemyDataSO Data;
            public EnemyTraitRuntime Trait;
            public float AttackTimer;
            public Vector2 HomeAnchor;
            public Vector2 PositionAnchor;
            public int TargetAllyRuntimeId = -1;
            public float CombatRadius;
            public float AttackReachAnchor;
            public float MoveSpeed;
            public bool IsMoving;
        }

        [SerializeField] private float playerAttackInterval = 1.0f;
        [SerializeField] private float enemyAttackInterval = 1.2f;
        [SerializeField] private float guardDuration = 5.0f;
        [SerializeField] private int guardDefenseBonus = 5;
        [SerializeField] private float normalEnemySpawnInterval = 0.6f;
        [SerializeField] private int initialEnemySpawnBurst = 0;
        [SerializeField] private int normalEnemySpawnBurstSize = 1;
        [SerializeField] private int normalWaveEnemyCount = 100;
        [SerializeField] private int totalWaveCount = 10;
        [SerializeField] private int bossWaveEnemyCount = 1;
        [SerializeField] private float bossHpMultiplier = 5.0f;
        [SerializeField] private float bossAttackMultiplier = 2.0f;
        [SerializeField] private int bossDefenseBonus = 8;

        private BattleUnitStats playerStats;
        private BattleUnitStats enemyStats;
        private BattleSkillSet skillSet;
        private EnemyTraitRuntime enemyTraitRuntime;
        private EnemyDataSO currentEnemyData;
        private MonsterDataSO currentPlayerMonsterData;
        private float enemyAttackTimer;
        private float guardRemainingTime;
        private bool isRunning;
        private int tickCount;
        private float lastDeltaTime;
        private int currentFloor;
        private int currentWave;
        private int defeatedEnemiesInCurrentWave;
        private int spawnedEnemiesInCurrentWave;
        private int activeEnemiesInCurrentWave;
        private int encounterEnemyCountTarget;
        private int encounterSerial;
        private float enemySpawnTimer;
        private int engagedEnemyCount;
        private bool isBossEncounter;
        private bool openingBurstSpawned;
        private int nextAllyRuntimeId = 1;
        private int nextEnemyRuntimeId = 1;
        private readonly List<AllyRuntime> activeAllyRuntimes = new List<AllyRuntime>();
        private readonly List<EnemyRuntime> activeEnemyRuntimes = new List<EnemyRuntime>();
        private const float MagicPresentationDelay = 0.60f;
        private const float MeleePresentationDelay = 0.18f;
        private const float RangedAttackThreshold = 1.35f;

        public event System.Action<BattleHitInfo> HitResolved;
        public event System.Action EncounterChanged;
        public event System.Action<int, int> EnemyDefeated;
        public event System.Action<int> AllyDefeated;

        public BattleUnitStats PlayerStats => playerStats;
        public BattleUnitStats EnemyStats => enemyStats;
        public bool IsRunning => isRunning;
        public int DebugTickCount => tickCount;
        public float DebugLastDeltaTime => lastDeltaTime;
        public float DebugPlayerAttackTimer => ResolveLeadAliveAllyRuntime()?.AttackTimer ?? 0f;
        public float DebugEnemyAttackTimer => enemyAttackTimer;
        public float DebugGuardRemainingTime => guardRemainingTime;
        public int CurrentFloor => currentFloor;
        public int CurrentWave => currentWave;
        public int TotalWaveCount => 1;
        public bool IsBossWave => isBossEncounter;
        public int EncounterSerial => encounterSerial;
        public EnemyDataSO CurrentEnemyData => currentEnemyData;
        public int CurrentEnemyCountTarget => Mathf.Max(1, encounterEnemyCountTarget);
        public int CurrentEnemyIndexInWave => Mathf.Clamp(defeatedEnemiesInCurrentWave + 1, 1, CurrentEnemyCountTarget);
        public int CurrentRemainingEnemyCount => Mathf.Max(0, CurrentEnemyCountTarget - defeatedEnemiesInCurrentWave);
        public int CurrentSpawnedEnemyCount => Mathf.Max(0, spawnedEnemiesInCurrentWave);
        public int CurrentActiveEnemyCount => Mathf.Max(0, activeEnemiesInCurrentWave);
        public int CurrentEngagedEnemyCount => Mathf.Max(0, engagedEnemyCount);
        public int CurrentAliveAllyCount => CountAliveAllies();
        public int CurrentAllyRuntimeCount => activeAllyRuntimes.Count;
        public int CurrentPreferredEnemyTargetIndex => activeEnemyRuntimes.Count > 0
            ? ResolveEnemyAttackTargetIndex(activeEnemyRuntimes[0], 0)
            : -1;

        public void Setup(int floor)
        {
            currentFloor = Mathf.Max(1, floor);
            currentWave = 1;
            isBossEncounter = ResolveBossEncounter(currentFloor);
            encounterEnemyCountTarget = ResolveEncounterEnemyCount();
            defeatedEnemiesInCurrentWave = 0;
            spawnedEnemiesInCurrentWave = 0;
            activeEnemiesInCurrentWave = 0;
            encounterSerial = 0;
            enemySpawnTimer = 0f;
            openingBurstSpawned = false;
            nextAllyRuntimeId = 1;
            nextEnemyRuntimeId = 1;
            activeAllyRuntimes.Clear();
            CreatePlayerPartyRuntimes();
            SyncPlayerAggregateState();
            skillSet = new BattleSkillSet();
            enemyAttackTimer = 0f;
            guardRemainingTime = 0f;
            enemyStats = null;
            currentEnemyData = null;
            enemyTraitRuntime = default;
            activeEnemyRuntimes.Clear();
            enemySpawnTimer = ResolveEnemySpawnInterval();
            engagedEnemyCount = 0;
            isRunning = activeAllyRuntimes.Count > 0 && playerStats != null;
        }

        public BattleResult Tick(float deltaTime)
        {
            tickCount += 1;
            lastDeltaTime = deltaTime;

            if (!isRunning)
            {
                return BattleResult.None;
            }

            TickEnemySpawns(deltaTime);
            TickUnitMovement(deltaTime);
            skillSet ??= new BattleSkillSet();
            skillSet.Tick(deltaTime);
            TickGuard(deltaTime);
            TickAllyAttackers(deltaTime);
            TickEnemyAttackers(deltaTime);

            int defeatedEnemyIndex = FindDefeatedEnemyIndex();
            if (defeatedEnemyIndex >= 0)
            {
                if (AdvanceEncounterAfterEnemyDefeat(defeatedEnemyIndex))
                {
                    return BattleResult.None;
                }

                isRunning = false;
                return BattleResult.Win;
            }

            if (CountAliveAllies() <= 0)
            {
                SyncPlayerAggregateState();
                isRunning = false;
                return BattleResult.Lose;
            }

            return BattleResult.None;
        }

        public void TickPreparation(float deltaTime)
        {
            if (!isRunning)
            {
                return;
            }

            TickEnemySpawns(deltaTime);
            TickUnitMovement(deltaTime);
        }

        public void SetEngagedEnemyCount(int count)
        {
            engagedEnemyCount = CountActuallyEngagedEnemies();
            enemyAttackTimer = activeEnemyRuntimes.Count > 0 ? activeEnemyRuntimes[0].AttackTimer : 0f;
        }

        public bool TryUseSkill(BattleSkillType skillType)
        {
            AllyRuntime leadAlly = ResolveLeadAliveAllyRuntime();
            if (!isRunning || enemyStats == null || leadAlly == null || skillSet == null)
            {
                return false;
            }

            var skillState = skillSet.Get(skillType);
            if (!skillState.IsReady)
            {
                return false;
            }

            switch (skillType)
            {
                case BattleSkillType.Strike:
                    UseSkillStrike();
                    break;
                case BattleSkillType.Drain:
                    UseSkillDrain();
                    break;
                case BattleSkillType.Guard:
                    UseSkillGuard();
                    break;
            }

            skillState.Trigger();
            return true;
        }

        public BattleSkillState GetSkillState(BattleSkillType skillType)
        {
            return skillSet != null ? skillSet.Get(skillType) : null;
        }

        private void UseSkillStrike()
        {
            AllyRuntime attacker = ResolveLeadAliveAllyRuntime();
            if (attacker == null || attacker.Stats == null)
            {
                return;
            }

            int targetIndex = ResolvePlayerAttackTargetIndex(attacker.Data);
            if (targetIndex < 0)
            {
                return;
            }

            int attackerIndex = ResolveLeadAliveAllyIndex();

            EnemyRuntime targetEnemy = activeEnemyRuntimes[targetIndex];
            var damage = Mathf.Max(1, Mathf.RoundToInt(attacker.Stats.Attack * 2.0f) - targetEnemy.Stats.Defense);
            targetEnemy.Stats.ApplyDamage(damage);
            if (targetIndex == 0)
            {
                SyncLeadEnemyState();
            }

            RaiseHitResolved(new BattleHitInfo(
                false,
                damage,
                false,
                true,
                false,
                targetIndex,
                attackerIndex,
                ResolvePlayerPresentationDelay(attacker.Data)));
        }

        private void UseSkillDrain()
        {
            AllyRuntime attacker = ResolveLeadAliveAllyRuntime();
            if (attacker == null || attacker.Stats == null)
            {
                return;
            }

            int targetIndex = ResolvePlayerAttackTargetIndex(attacker.Data);
            if (targetIndex < 0)
            {
                return;
            }

            int attackerIndex = ResolveLeadAliveAllyIndex();

            EnemyRuntime targetEnemy = activeEnemyRuntimes[targetIndex];
            var damage = Mathf.Max(1, Mathf.RoundToInt(attacker.Stats.Attack * 1.2f) - targetEnemy.Stats.Defense);
            targetEnemy.Stats.ApplyDamage(damage);
            if (targetIndex == 0)
            {
                SyncLeadEnemyState();
            }

            var healAmount = Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f));
            attacker.Stats.CurrentHp = Mathf.Min(attacker.Stats.MaxHp, attacker.Stats.CurrentHp + healAmount);
            SyncPlayerAggregateState();
            RaiseHitResolved(new BattleHitInfo(
                false,
                damage,
                false,
                true,
                false,
                targetIndex,
                attackerIndex,
                ResolvePlayerPresentationDelay(attacker.Data)));
        }

        private void UseSkillGuard()
        {
            guardRemainingTime = guardDuration;
        }

        private void PerformAttackOnPlayer(EnemyRuntime attacker, int attackerIndex)
        {
            if (attacker == null || attacker.Stats == null)
            {
                return;
            }

            int targetIndex = ResolveEnemyAttackTargetIndex(attacker, attackerIndex);
            if (targetIndex < 0)
            {
                return;
            }

            AllyRuntime targetAlly = activeAllyRuntimes[targetIndex];
            if (targetAlly == null || targetAlly.Stats == null || targetAlly.Stats.IsDead())
            {
                return;
            }

            bool wasAlive = !targetAlly.Stats.IsDead();
            var result = DamageCalculator.Calculate(attacker.Stats, BuildCurrentPlayerDefenseSnapshot(targetAlly), ResolveEnemyDamageType(attacker.Data));
            int targetCount = Mathf.Min(ResolveEnemyNormalAttackTargetCount(attacker.Data), 1);
            int totalDamage = 0;

            for (int i = 0; i < targetCount; i += 1)
            {
                int damage = Mathf.Max(1, result.Damage);
                targetAlly.Stats.ApplyDamage(damage);
                totalDamage += damage;
            }

            if (totalDamage <= 0)
            {
                return;
            }

            ApplyEnemyLifeSteal(attacker, totalDamage);
            SyncPlayerAggregateState();
            NotifyAllyDefeatedIfNeeded(targetIndex, wasAlive);
            RaiseHitResolved(new BattleHitInfo(
                true,
                totalDamage,
                result.IsCritical,
                false,
                false,
                targetIndex,
                attackerIndex,
                ResolveEnemyPresentationDelay(attacker.Data)));
        }

        private void PerformAttackOnEnemy(AllyRuntime attacker, bool isSkill, int attackerIndex)
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
            {
                return;
            }

            int targetCount = isSkill
                ? 1
                : Mathf.Max(1, ResolvePlayerNormalAttackTargetCount(attacker.Data));
            List<int> targetIndices = CollectEnemyTargetIndices(targetCount, attacker, attackerIndex);
            if (targetIndices.Count <= 0)
            {
                return;
            }

            int totalDamage = 0;
            bool anyCritical = false;
            int primaryTargetIndex = targetIndices[0];

            for (int i = 0; i < targetIndices.Count; i += 1)
            {
                int targetIndex = targetIndices[i];
                if (targetIndex < 0 || targetIndex >= activeEnemyRuntimes.Count)
                {
                    continue;
                }

                EnemyRuntime targetEnemy = activeEnemyRuntimes[targetIndex];
                if (targetEnemy == null || targetEnemy.Stats == null || targetEnemy.Stats.IsDead())
                {
                    continue;
                }

                var result = DamageCalculator.Calculate(attacker.Stats, targetEnemy.Stats, ResolvePlayerDamageType(attacker.Data));
                int damage = Mathf.Max(1, result.Damage);
                targetEnemy.Stats.ApplyDamage(damage);
                totalDamage += damage;
                anyCritical |= result.IsCritical;
                if (targetIndex == 0)
                {
                    SyncLeadEnemyState();
                }
            }

            if (totalDamage <= 0)
            {
                return;
            }

            RaiseHitResolved(new BattleHitInfo(
                false,
                totalDamage,
                anyCritical,
                isSkill,
                false,
                primaryTargetIndex,
                attackerIndex,
                ResolvePlayerPresentationDelay(attacker.Data)));
        }

        private void TickGuard(float deltaTime)
        {
            if (guardRemainingTime <= 0f)
            {
                guardRemainingTime = 0f;
                return;
            }

            guardRemainingTime -= deltaTime;
        }

        private void TickAllyAttackers(float deltaTime)
        {
            if (activeEnemyRuntimes.Count == 0 || enemyStats == null)
            {
                return;
            }

            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime attacker = activeAllyRuntimes[i];
                if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
                {
                    continue;
                }

                int targetIndex = ResolveAllyTargetEnemyIndex(attacker, i);
                float interval = GetCurrentPlayerAttackInterval(attacker.Stats);
                if (!CanAllyAttackTarget(attacker, targetIndex))
                {
                    attacker.AttackTimer = Mathf.Min(attacker.AttackTimer + deltaTime, interval * 0.90f);
                    continue;
                }

                attacker.AttackTimer += deltaTime;
                while (attacker.AttackTimer >= interval)
                {
                    attacker.AttackTimer -= interval;
                    PerformAttackOnEnemy(attacker, false, i);
                    if (FindDefeatedEnemyIndex() >= 0)
                    {
                        break;
                    }
                }
            }

            SyncPlayerAggregateState();
        }

        private void TickUnitMovement(float deltaTime)
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null)
                {
                    continue;
                }

                if (ally.Stats == null || ally.Stats.IsDead())
                {
                    ally.TargetEnemyRuntimeId = -1;
                    ally.IsMoving = false;
                    ally.PositionAnchor = Vector2.MoveTowards(
                        ally.PositionAnchor,
                        ally.HomeAnchor,
                        AllyReturnSpeed * MonsterMoveSpeedMultiplier * deltaTime);
                    continue;
                }

                int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, i);
                if (targetEnemyIndex < 0)
                {
                    ally.PositionAnchor = MoveRuntimeTowards(ally.PositionAnchor, ally.HomeAnchor, ally.MoveSpeed, deltaTime, out bool allyReturning);
                    ally.IsMoving = allyReturning;
                    continue;
                }

                EnemyRuntime targetEnemy = activeEnemyRuntimes[targetEnemyIndex];
                if (ShouldAllyHoldFormation(ally, targetEnemy))
                {
                    ally.PositionAnchor = MoveRuntimeTowards(ally.PositionAnchor, ally.HomeAnchor, ally.MoveSpeed, deltaTime, out bool allyHolding);
                    ally.IsMoving = allyHolding;
                    continue;
                }

                float desiredSeparation = ally.CombatRadius + targetEnemy.CombatRadius + ally.AttackReachAnchor;
                Vector2 targetAnchor = IsMonsterMelee(ally.Data)
                    ? ResolveDesiredCombatAnchor(
                        ally.PositionAnchor,
                        targetEnemy.PositionAnchor,
                        true,
                        desiredSeparation)
                    : new Vector2(targetEnemy.PositionAnchor.x - desiredSeparation, ally.HomeAnchor.y);
                targetAnchor.x += ResolveAllyCombatPressureAdvance(i, ally.Data);
                targetAnchor = BattleFormationLayout.ClampAllyCombatAnchor(i, ally.Data, targetAnchor);
                targetAnchor.x = Mathf.Max(targetAnchor.x, ally.PositionAnchor.x);
                if (!IsMonsterMelee(ally.Data))
                {
                    targetAnchor.y = ally.HomeAnchor.y;
                }
                ally.PositionAnchor = MoveRuntimeTowards(ally.PositionAnchor, targetAnchor, ally.MoveSpeed, deltaTime, out bool allyMoving);
                ally.IsMoving = allyMoving;
            }

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null)
                {
                    continue;
                }

                if (enemy.Stats == null || enemy.Stats.IsDead())
                {
                    enemy.TargetAllyRuntimeId = -1;
                    enemy.IsMoving = false;
                    enemy.PositionAnchor = Vector2.MoveTowards(
                        enemy.PositionAnchor,
                        enemy.HomeAnchor,
                        EnemyReturnSpeed * MonsterMoveSpeedMultiplier * deltaTime);
                    continue;
                }

                int targetAllyIndex = ResolveEnemyAttackTargetIndex(enemy, i);
                if (targetAllyIndex < 0)
                {
                    enemy.PositionAnchor = MoveRuntimeTowards(enemy.PositionAnchor, enemy.HomeAnchor, enemy.MoveSpeed, deltaTime, out bool enemyReturning);
                    enemy.IsMoving = enemyReturning;
                    continue;
                }

                AllyRuntime targetAlly = activeAllyRuntimes[targetAllyIndex];
                int queueIndex = ResolveEnemyQueueIndex(enemy, i, targetAllyIndex);
                Vector2 targetAnchor = ResolveEnemyCombatAnchor(enemy, targetAlly, queueIndex);
                enemy.PositionAnchor = MoveRuntimeTowards(enemy.PositionAnchor, targetAnchor, enemy.MoveSpeed, deltaTime, out bool enemyMoving);
                enemy.IsMoving = enemyMoving;
            }

            engagedEnemyCount = CountActuallyEngagedEnemies();
        }

        private static bool ShouldAllyHoldFormation(AllyRuntime ally, EnemyRuntime targetEnemy)
        {
            return !IsEnemyInsideAllySearchRange(ally, targetEnemy);
        }

        private static bool IsEnemyInsideAllySearchRange(AllyRuntime ally, EnemyRuntime enemy)
        {
            if (ally == null || enemy == null)
            {
                return false;
            }

            float enemyFrontX = enemy.PositionAnchor.x - enemy.CombatRadius;
            float searchThresholdX = ally.HomeAnchor.x + Mathf.Max(ally.AttackReachAnchor, ally.SearchReachAnchor);
            return enemyFrontX <= searchThresholdX + PositionEpsilon;
        }

        private static float ResolveAllyCombatPressureAdvance(int allyIndex, MonsterDataSO monsterData)
        {
            bool isRanged = monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged;
            bool isFrontline = allyIndex == 0 || allyIndex == 1;
            bool isMidline = allyIndex == 2;
            if (isFrontline)
            {
                return isRanged ? 0.03f : 0.04f;
            }

            if (isMidline)
            {
                return isRanged ? 0.02f : 0.03f;
            }

            return isRanged ? 0.015f : 0.02f;
        }

        private int ResolveEnemyQueueIndex(EnemyRuntime attacker, int attackerIndex, int targetAllyIndex)
        {
            if (attacker == null || targetAllyIndex < 0 || targetAllyIndex >= activeAllyRuntimes.Count)
            {
                return 0;
            }

            AllyRuntime targetAlly = activeAllyRuntimes[targetAllyIndex];
            if (targetAlly == null)
            {
                return 0;
            }

            float attackerDistance = Vector2.SqrMagnitude(attacker.PositionAnchor - targetAlly.PositionAnchor);
            int queueIndex = 0;
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                if (i == attackerIndex)
                {
                    continue;
                }

                EnemyRuntime other = activeEnemyRuntimes[i];
                if (other == null || other.Stats == null || other.Stats.IsDead())
                {
                    continue;
                }

                int otherTargetAllyIndex = ResolveEnemyAttackTargetIndex(other, i);
                if (otherTargetAllyIndex != targetAllyIndex)
                {
                    continue;
                }

                float otherDistance = Vector2.SqrMagnitude(other.PositionAnchor - targetAlly.PositionAnchor);
                bool isAhead = otherDistance < attackerDistance - 0.0001f;
                if (!isAhead && Mathf.Abs(otherDistance - attackerDistance) <= 0.0001f)
                {
                    isAhead =
                        other.PositionAnchor.x < attacker.PositionAnchor.x - PositionEpsilon ||
                        (Mathf.Abs(other.PositionAnchor.x - attacker.PositionAnchor.x) <= PositionEpsilon &&
                         other.RuntimeId < attacker.RuntimeId);
                }

                if (isAhead)
                {
                    queueIndex += 1;
                }
            }

            return queueIndex;
        }

        private Vector2 ResolveEnemyCombatAnchor(EnemyRuntime enemy, AllyRuntime targetAlly, int queueIndex)
        {
            float baseSeparation = enemy.CombatRadius + targetAlly.CombatRadius + enemy.AttackReachAnchor;
            int slotCount = Mathf.Max(1, Mathf.Min(MaxConcurrentEnemyAttackersPerAlly, EnemyAttackSlotAngles.Length));
            int pressureIndex = Mathf.Max(0, queueIndex);
            int ringIndex = pressureIndex / slotCount;
            int slotIndex = pressureIndex % slotCount;
            float angle = EnemyAttackSlotAngles[Mathf.Clamp(slotIndex, 0, EnemyAttackSlotAngles.Length - 1)] * Mathf.Deg2Rad;
            float separation = baseSeparation + (ringIndex * EnemyQueueSpacing);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 combatAnchor = targetAlly.PositionAnchor + (direction * separation);
            if (ringIndex <= 0)
            {
                return combatAnchor;
            }

            float queueLaneY = Mathf.Lerp(enemy.HomeAnchor.y, targetAlly.HomeAnchor.y, EnemyQueueLaneBlend);
            float ringBlend = Mathf.Clamp01(ringIndex * 0.22f);
            combatAnchor.y = Mathf.Lerp(combatAnchor.y, queueLaneY, ringBlend);
            combatAnchor.x = Mathf.Max(combatAnchor.x, targetAlly.PositionAnchor.x + baseSeparation);
            return combatAnchor;
        }

        private void CreatePlayerPartyRuntimes()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            MasterDataManager.Instance?.Initialize();

            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsters(profile, 5);
            bool useDebugParty = partyMonsters.Count <= 0;
            int desiredCount = useDebugParty
                ? DevPartyOverrideMonsterIds.Length
                : Mathf.Min(partyMonsters.Count, BattleFormationLayout.AllyHomeAnchors.Length);
            for (int i = 0; i < desiredCount; i += 1)
            {
                OwnedMonsterData ownedMonster = !useDebugParty && i < partyMonsters.Count ? partyMonsters[i] : null;
                string monsterId = !useDebugParty && ownedMonster != null && !string.IsNullOrEmpty(ownedMonster.MonsterId)
                    ? ownedMonster.MonsterId
                    : (i < DevPartyOverrideMonsterIds.Length ? DevPartyOverrideMonsterIds[i] : null);

                if (string.IsNullOrEmpty(monsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monsterId);
                if (monsterData == null)
                {
                    continue;
                }

                Vector2 homeAnchor = ResolveAllyHomeAnchor(activeAllyRuntimes.Count);
                activeAllyRuntimes.Add(new AllyRuntime
                {
                    RuntimeId = nextAllyRuntimeId++,
                    Stats = MonsterBattleStatsFactory.Create(profile, ownedMonster, monsterData),
                    Data = monsterData,
                    OwnedMonster = ownedMonster,
                    AttackTimer = 0f,
                    HomeAnchor = homeAnchor,
                    PositionAnchor = homeAnchor,
                    CombatRadius = ResolveAllyCombatRadius(monsterData),
                    AttackReachAnchor = ResolveAllyAttackReach(monsterData),
                    SearchReachAnchor = ResolveAllySearchReach(monsterData),
                    MoveSpeed = AllyMoveSpeed * MonsterMoveSpeedMultiplier
                });
            }

            if (activeAllyRuntimes.Count > 0)
            {
                return;
            }

            BattleUnitStats fallbackStats = PlayerBattleStatsFactory.CreatePreview(profile);
            if (fallbackStats != null)
            {
                Vector2 homeAnchor = ResolveAllyHomeAnchor(activeAllyRuntimes.Count);
                activeAllyRuntimes.Add(new AllyRuntime
                {
                    RuntimeId = nextAllyRuntimeId++,
                    Stats = fallbackStats,
                    Data = null,
                    OwnedMonster = null,
                    AttackTimer = 0f,
                    HomeAnchor = homeAnchor,
                    PositionAnchor = homeAnchor,
                    CombatRadius = ResolveAllyCombatRadius(null),
                    AttackReachAnchor = ResolveAllyAttackReach(null),
                    SearchReachAnchor = ResolveAllySearchReach(null),
                    MoveSpeed = AllyMoveSpeed * MonsterMoveSpeedMultiplier
                });
            }
        }

        private int ResolveAllyTargetEnemyIndex(AllyRuntime ally, int allyIndex)
        {
            if (ally == null || ally.Stats == null || ally.Stats.IsDead())
            {
                return -1;
            }

            int lockedIndex = ResolveEnemyRuntimeIndexById(ally.TargetEnemyRuntimeId);
            if (lockedIndex >= 0)
            {
                EnemyRuntime lockedEnemy = activeEnemyRuntimes[lockedIndex];
                if (lockedEnemy != null &&
                    lockedEnemy.Stats != null &&
                    !lockedEnemy.Stats.IsDead() &&
                    IsEnemyInsideAllySearchRange(ally, lockedEnemy))
                {
                    return lockedIndex;
                }
            }

            int resolvedIndex = ResolvePreferredEnemyTargetIndex(ally, allyIndex);
            ally.TargetEnemyRuntimeId = resolvedIndex >= 0 ? activeEnemyRuntimes[resolvedIndex].RuntimeId : -1;
            return resolvedIndex;
        }

        private int ResolvePreferredEnemyTargetIndex(AllyRuntime ally, int allyIndex)
        {
            Vector2 referenceAnchor = ally != null ? ally.PositionAnchor : ResolveAllyHomeAnchor(allyIndex);
            int bestIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead())
                {
                    continue;
                }

                if (!IsEnemyInsideAllySearchRange(ally, enemy))
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(enemy.PositionAnchor - referenceAnchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int ResolveNearestAliveEnemyIndex(Vector2 referenceAnchor)
        {
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead())
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(enemy.PositionAnchor - referenceAnchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int ResolveNearestAliveAllyIndex(Vector2 referenceAnchor)
        {
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null || ally.Stats == null || ally.Stats.IsDead())
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(ally.PositionAnchor - referenceAnchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int ResolveNearestAliveAllyRuntimeId(Vector2 referenceAnchor)
        {
            int index = ResolvePreferredAllyTargetIndex(referenceAnchor);
            return index >= 0 && index < activeAllyRuntimes.Count ? activeAllyRuntimes[index].RuntimeId : -1;
        }

        private int ResolveEnemyRuntimeIndexById(int runtimeId)
        {
            if (runtimeId < 0)
            {
                return -1;
            }

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy != null && enemy.RuntimeId == runtimeId)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveAllyRuntimeIndexById(int runtimeId)
        {
            if (runtimeId < 0)
            {
                return -1;
            }

            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally != null && ally.RuntimeId == runtimeId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static float ResolveAllyCombatRadius(MonsterDataSO monsterData)
        {
            return monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged
                ? DefaultAllyCombatRadius * 0.90f
                : DefaultAllyCombatRadius;
        }

        private static float ResolveEnemyCombatRadius(EnemyDataSO enemyData)
        {
            float attackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData);
            return attackRange >= RangedAttackThreshold
                ? DefaultEnemyCombatRadius * 0.92f
                : DefaultEnemyCombatRadius;
        }

        private static float ResolveAllyAttackReach(MonsterDataSO monsterData)
        {
            float attackRange = BattleAttackRangeResolver.ResolveMonsterAttackRange(monsterData);
            return Mathf.Max(0f, BattleAttackRangeResolver.ToAllyHoldOffset(attackRange) + RangeOffsetPadding);
        }

        private static float ResolveAllySearchReach(MonsterDataSO monsterData)
        {
            if (monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged)
            {
                return 0.86f;
            }

            float searchRange = BattleAttackRangeResolver.ResolveMonsterSearchRange(monsterData);
            return Mathf.Max(0f, BattleAttackRangeResolver.ToAllySearchOffset(searchRange));
        }

        private static float ResolveEnemyAttackReach(EnemyDataSO enemyData)
        {
            float attackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData);
            return Mathf.Max(0f, BattleAttackRangeResolver.ToEnemyHoldOffset(attackRange) + RangeOffsetPadding);
        }

        private static Vector2 ResolveAllyHomeAnchor(int allyIndex) => BattleFormationLayout.ResolveAllyHomeAnchor(allyIndex);

        private static Vector2 ResolveDesiredCombatAnchor(Vector2 origin, Vector2 target, bool keepOnLeftSide, float desiredSeparation)
        {
            Vector2 away = origin - target;
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = keepOnLeftSide ? Vector2.left : Vector2.right;
            }

            if (keepOnLeftSide && away.x >= -0.001f)
            {
                away.x = -Mathf.Max(0.001f, Mathf.Abs(away.x));
            }
            else if (!keepOnLeftSide && away.x <= 0.001f)
            {
                away.x = Mathf.Max(0.001f, Mathf.Abs(away.x));
            }

            Vector2 direction = away.normalized;
            return target + (direction * Mathf.Max(0.01f, desiredSeparation));
        }

        private static Vector2 MoveRuntimeTowards(Vector2 current, Vector2 destination, float speed, float deltaTime, out bool isMoving)
        {
            Vector2 next = Vector2.MoveTowards(current, destination, Mathf.Max(0f, speed) * deltaTime);
            isMoving = Vector2.Distance(next, destination) > PositionEpsilon;
            return next;
        }

        private bool CanAllyAttackTarget(AllyRuntime attacker, int targetEnemyIndex)
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
            {
                return false;
            }

            if (targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            EnemyRuntime target = activeEnemyRuntimes[targetEnemyIndex];
            if (target == null || target.Stats == null || target.Stats.IsDead())
            {
                return false;
            }

            float attackDistance = attacker.CombatRadius + target.CombatRadius + attacker.AttackReachAnchor + PositionEpsilon;
            if (!IsMonsterMelee(attacker.Data))
            {
                return Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x) <= attackDistance;
            }

            return Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance;
        }

        private bool CanEnemyAttackTarget(EnemyRuntime attacker, int attackerIndex, int targetAllyIndex)
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
            {
                return false;
            }

            if (targetAllyIndex < 0 || targetAllyIndex >= activeAllyRuntimes.Count)
            {
                return false;
            }

            AllyRuntime target = activeAllyRuntimes[targetAllyIndex];
            if (target == null || target.Stats == null || target.Stats.IsDead())
            {
                return false;
            }

            float attackDistance = attacker.CombatRadius + target.CombatRadius + attacker.AttackReachAnchor + PositionEpsilon;
            return Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance;
        }

        private int CountActuallyEngagedEnemies()
        {
            int count = 0;
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead())
                {
                    continue;
                }

                int targetAllyIndex = ResolveEnemyAttackTargetIndex(enemy, i);
                if (CanEnemyAttackTarget(enemy, i, targetAllyIndex))
                {
                    count += 1;
                }
            }

            return count;
        }

        private int ResolveNearestAdditionalEnemyIndex(Vector2 referencePosition, List<int> excludedIndices)
        {
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                if (excludedIndices != null && excludedIndices.Contains(i))
                {
                    continue;
                }

                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead())
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(enemy.PositionAnchor - referencePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void SyncPlayerAggregateState()
        {
            if (playerStats == null)
            {
                playerStats = new BattleUnitStats();
            }

            int totalMaxHp = 0;
            int totalCurrentHp = 0;
            int totalAttack = 0;
            int totalWisdom = 0;
            int totalDefense = 0;
            int totalMagicDefense = 0;
            float totalAttackSpeed = 0f;
            float critRate = 0.05f;
            float critDamage = 1.5f;
            int aliveCount = 0;
            MonsterDataSO leadMonsterData = null;

            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null || ally.Stats == null)
                {
                    continue;
                }

                totalMaxHp += ally.Stats.MaxHp;
                totalCurrentHp += Mathf.Max(0, ally.Stats.CurrentHp);

                if (ally.Stats.IsDead())
                {
                    continue;
                }

                aliveCount += 1;
                totalAttack += ally.Stats.Attack;
                totalWisdom += ally.Stats.Wisdom;
                totalDefense += ally.Stats.Defense;
                totalMagicDefense += ally.Stats.MagicDefense;
                totalAttackSpeed += ally.Stats.AttackSpeed;
                if (leadMonsterData == null)
                {
                    leadMonsterData = ally.Data;
                    critRate = ally.Stats.CritRate;
                    critDamage = ally.Stats.CritDamage;
                }
            }

            playerStats.MaxHp = Mathf.Max(1, totalMaxHp);
            playerStats.CurrentHp = Mathf.Clamp(totalCurrentHp, 0, playerStats.MaxHp);
            playerStats.Attack = Mathf.Max(1, totalAttack);
            playerStats.Wisdom = Mathf.Max(1, totalWisdom);
            playerStats.Defense = aliveCount > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)totalDefense / aliveCount)) : 0;
            playerStats.MagicDefense = aliveCount > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)totalMagicDefense / aliveCount)) : 0;
            playerStats.AttackSpeed = aliveCount > 0 ? Mathf.Max(0.2f, totalAttackSpeed / aliveCount) : 0f;
            playerStats.CritRate = critRate;
            playerStats.CritDamage = critDamage;
            currentPlayerMonsterData = leadMonsterData;
        }

        private AllyRuntime ResolveLeadAliveAllyRuntime()
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally != null && ally.Stats != null && !ally.Stats.IsDead())
                {
                    return ally;
                }
            }

            return null;
        }

        private int ResolveLeadAliveAllyIndex()
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally != null && ally.Stats != null && !ally.Stats.IsDead())
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveEnemyAttackTargetIndex(EnemyRuntime attacker, int attackerIndex)
        {
            if (attacker == null)
            {
                return -1;
            }

            int lockedIndex = ResolveAllyRuntimeIndexById(attacker.TargetAllyRuntimeId);
            if (lockedIndex >= 0)
            {
                AllyRuntime lockedAlly = activeAllyRuntimes[lockedIndex];
                if (lockedAlly != null && lockedAlly.Stats != null && !lockedAlly.Stats.IsDead())
                {
                    return lockedIndex;
                }
            }

            int resolvedIndex = ResolvePreferredAllyTargetIndex(attacker.PositionAnchor);
            attacker.TargetAllyRuntimeId = resolvedIndex >= 0 ? activeAllyRuntimes[resolvedIndex].RuntimeId : -1;
            if (attackerIndex >= 0 && attackerIndex < activeEnemyRuntimes.Count)
            {
                activeEnemyRuntimes[attackerIndex].TargetAllyRuntimeId = attacker.TargetAllyRuntimeId;
            }

            return resolvedIndex;
        }

        private int ResolveEnemyAttackTargetIndex(EnemyDataSO attackerData, int attackerIndex)
        {
            Vector2 referenceAnchor = attackerIndex >= 0 && attackerIndex < activeEnemyRuntimes.Count
                ? activeEnemyRuntimes[attackerIndex].PositionAnchor
                : new Vector2(EnemySpawnX, ResolveEnemySpawnLaneY(attackerIndex));
            return ResolvePreferredAllyTargetIndex(referenceAnchor);
        }

        private int ResolvePreferredAllyTargetIndex(Vector2 referenceAnchor)
        {
            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null || ally.Stats == null || ally.Stats.IsDead())
                {
                    continue;
                }

                float yDistance = Mathf.Abs(ally.HomeAnchor.y - referenceAnchor.y);
                float xPressure = Mathf.Max(0f, referenceAnchor.x - ally.HomeAnchor.x);
                float score = (xPressure * 0.62f) + (yDistance * 1.80f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float ResolveEnemyLaneY(int enemyIndex)
        {
            return BattleFormationLayout.ResolveEnemyLaneY(enemyIndex);
        }

        private float ResolveEnemySpawnLaneY(int spawnIndex)
        {
            int safeSpawnIndex = Mathf.Max(0, spawnIndex);
            int patternIndex = EnemySpawnLanePattern[safeSpawnIndex % EnemySpawnLanePattern.Length];
            int laneCycle = safeSpawnIndex / EnemySpawnLanePattern.Length;
            float offset = SingleLaneSpawnOffsets[laneCycle % SingleLaneSpawnOffsets.Length] * 0.34f;
            return Mathf.Clamp(ResolveEnemyLaneY(patternIndex) + offset, 0.18f, 0.68f);
        }

        public bool IsAllyAlive(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return false;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null && ally.Stats != null && !ally.Stats.IsDead();
        }

        public bool HasAllyRuntime(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return false;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null && ally.Stats != null;
        }

        public int GetAllyCurrentHp(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return 0;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null && ally.Stats != null
                ? Mathf.Max(0, ally.Stats.CurrentHp)
                : 0;
        }

        public int GetAllyMaxHp(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return 0;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null && ally.Stats != null
                ? Mathf.Max(0, ally.Stats.MaxHp)
                : 0;
        }

        public Vector2 GetAllyPositionAnchor(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return ResolveAllyHomeAnchor(index);
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null ? ally.PositionAnchor : ResolveAllyHomeAnchor(index);
        }

        public int GetAllyTargetEnemyIndex(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return -1;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ResolveAllyTargetEnemyIndex(ally, index);
        }

        public bool IsAllyMoving(int index)
        {
            if (index < 0 || index >= activeAllyRuntimes.Count)
            {
                return false;
            }

            AllyRuntime ally = activeAllyRuntimes[index];
            return ally != null && ally.IsMoving;
        }

        public bool HasEnemyRuntime(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null && enemy.Stats != null;
        }

        public int GetEnemyCurrentHp(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return 0;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null && enemy.Stats != null
                ? Mathf.Max(0, enemy.Stats.CurrentHp)
                : 0;
        }

        public int GetEnemyMaxHp(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return 0;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null && enemy.Stats != null
                ? Mathf.Max(0, enemy.Stats.MaxHp)
                : 0;
        }

        public Vector2 GetEnemyPositionAnchor(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return new Vector2(EnemySpawnX, ResolveEnemyLaneY(index));
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? enemy.PositionAnchor : new Vector2(EnemySpawnX, ResolveEnemyLaneY(index));
        }

        public int GetEnemyTargetAllyIndex(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return -1;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return ResolveEnemyAttackTargetIndex(enemy, index);
        }

        public bool IsEnemyMoving(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null && enemy.IsMoving;
        }

        private int CountAliveAllies()
        {
            int aliveCount = 0;
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                if (IsAllyAlive(i))
                {
                    aliveCount += 1;
                }
            }

            return aliveCount;
        }

        private static bool ResolveBossEncounter(int floor)
        {
            return floor > 0 && floor % 10 == 0;
        }

        private int ResolveEncounterEnemyCount()
        {
            if (isBossEncounter)
            {
                return Mathf.Max(1, bossWaveEnemyCount);
            }

            int configuredEnemyCount = normalWaveEnemyCount > 0
                ? normalWaveEnemyCount
                : ReferenceEncounterEnemyCount;
            return Mathf.Clamp(configuredEnemyCount, MinEncounterEnemyCount, MaxEncounterEnemyCount);
        }

        private float ResolveEnemySpawnInterval()
        {
            float minInterval = 0.08f * EnemySpawnIntervalMultiplier;
            float maxInterval = 0.30f * EnemySpawnIntervalMultiplier;

            if (isBossEncounter)
            {
                return Mathf.Clamp(0.12f * EnemySpawnIntervalMultiplier, minInterval, maxInterval);
            }

            int activePartyCount = Mathf.Max(1, CurrentAliveAllyCount > 0 ? CurrentAliveAllyCount : activeAllyRuntimes.Count);
            float interval;
            if (normalEnemySpawnInterval <= 0f || normalEnemySpawnInterval >= 0.45f)
            {
                if (activePartyCount <= 1)
                {
                    interval = SparsePartySpawnInterval;
                }
                else if (activePartyCount == 2)
                {
                    interval = DuoPartySpawnInterval;
                }
                else
                {
                    interval = ReferenceSpawnInterval;
                }
            }
            else
            {
                interval = Mathf.Clamp(normalEnemySpawnInterval, 0.08f, 0.30f);
                if (activePartyCount <= 1)
                {
                    interval = Mathf.Max(interval, SparsePartySpawnInterval);
                }
                else if (activePartyCount == 2)
                {
                    interval = Mathf.Max(interval, DuoPartySpawnInterval);
                }
            }

            if (CurrentEnemyCountTarget >= MassiveEncounterEnemyCount)
            {
                interval *= 0.78f;
            }
            else if (CurrentEnemyCountTarget >= LargeEncounterEnemyCount)
            {
                interval *= 0.88f;
            }

            return Mathf.Clamp(interval * EnemySpawnIntervalMultiplier, minInterval, maxInterval);
        }

        private int ResolveOpeningSpawnBurst()
        {
            if (isBossEncounter)
            {
                return 1;
            }

            int activePartyCount = Mathf.Max(1, CurrentAliveAllyCount > 0 ? CurrentAliveAllyCount : activeAllyRuntimes.Count);
            if (activePartyCount <= 1)
            {
                if (CurrentEnemyCountTarget >= MassiveEncounterEnemyCount)
                {
                    return Mathf.Min(3, CurrentEnemyCountTarget);
                }

                return CurrentEnemyCountTarget >= LargeEncounterEnemyCount
                    ? Mathf.Min(2, CurrentEnemyCountTarget)
                    : 1;
            }

            if (activePartyCount == 2)
            {
                if (CurrentEnemyCountTarget >= MassiveEncounterEnemyCount)
                {
                    return Mathf.Min(4, CurrentEnemyCountTarget);
                }

                return CurrentEnemyCountTarget >= LargeEncounterEnemyCount
                    ? Mathf.Min(3, CurrentEnemyCountTarget)
                    : Mathf.Min(2, CurrentEnemyCountTarget);
            }

            if (initialEnemySpawnBurst > 0)
            {
                return Mathf.Clamp(initialEnemySpawnBurst, 1, CurrentEnemyCountTarget);
            }

            if (CurrentEnemyCountTarget >= MassiveEncounterEnemyCount)
            {
                return Mathf.Min(4, CurrentEnemyCountTarget);
            }

            if (CurrentEnemyCountTarget >= LargeEncounterEnemyCount)
            {
                return Mathf.Min(3, CurrentEnemyCountTarget);
            }

            return Mathf.Clamp(ReferenceOpeningSpawnBurst, 1, CurrentEnemyCountTarget);
        }

        private int ResolveFollowupSpawnBurst()
        {
            if (isBossEncounter)
            {
                return 1;
            }

            if (normalEnemySpawnBurstSize > 0)
            {
                return Mathf.Clamp(normalEnemySpawnBurstSize, 1, 4);
            }

            if (CurrentEnemyCountTarget >= MassiveEncounterEnemyCount)
            {
                return 3;
            }

            if (CurrentEnemyCountTarget >= LargeEncounterEnemyCount)
            {
                return 2;
            }

            return 1;
        }

        private void SpawnEnemyForCurrentEncounter()
        {
            int spawnIndex = Mathf.Max(0, spawnedEnemiesInCurrentWave - 1);
            BattleUnitStats spawnedStats = CreateEnemyStats(currentFloor, IsBossWave, out EnemyTraitRuntime spawnedTrait, out EnemyDataSO spawnedData);
            Vector2 homeAnchor = new Vector2(EnemySpawnX, ResolveEnemySpawnLaneY(spawnIndex));
            var runtime = new EnemyRuntime
            {
                RuntimeId = nextEnemyRuntimeId++,
                Stats = spawnedStats,
                Data = spawnedData,
                Trait = spawnedTrait,
                AttackTimer = 0f,
                HomeAnchor = homeAnchor,
                PositionAnchor = homeAnchor,
                TargetAllyRuntimeId = ResolveNearestAliveAllyRuntimeId(homeAnchor),
                CombatRadius = ResolveEnemyCombatRadius(spawnedData),
                AttackReachAnchor = ResolveEnemyAttackReach(spawnedData),
                MoveSpeed = EnemyMoveSpeed * MonsterMoveSpeedMultiplier
            };

            activeEnemyRuntimes.Add(runtime);
            activeEnemiesInCurrentWave = activeEnemyRuntimes.Count;
            SyncLeadEnemyState();
            encounterSerial += 1;
            EncounterChanged?.Invoke();
        }

        private void QueueEnemySpawn(bool activateImmediately)
        {
            if (spawnedEnemiesInCurrentWave >= CurrentEnemyCountTarget)
            {
                return;
            }

            spawnedEnemiesInCurrentWave += 1;
            SpawnEnemyForCurrentEncounter();
        }

        private void TickEnemySpawns(float deltaTime)
        {
            if (spawnedEnemiesInCurrentWave >= CurrentEnemyCountTarget)
            {
                return;
            }

            enemySpawnTimer += deltaTime;
            float interval = ResolveEnemySpawnInterval();
            while (spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget && enemySpawnTimer >= interval)
            {
                enemySpawnTimer -= interval;
                int burstSize = !openingBurstSpawned
                    ? ResolveOpeningSpawnBurst()
                    : ResolveFollowupSpawnBurst();
                openingBurstSpawned = true;
                SpawnEnemyBurst(burstSize, false);
            }
        }

        private void SpawnEnemyBurst(int count, bool activateImmediately)
        {
            int burstCount = Mathf.Max(1, count);
            for (int i = 0; i < burstCount && spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget; i += 1)
            {
                QueueEnemySpawn(activateImmediately && i == 0);
            }
        }

        private bool AdvanceEncounterAfterEnemyDefeat(int defeatedEnemyIndex)
        {
            int removalIndex = ResolveEnemyRemovalIndex(defeatedEnemyIndex);
            if (removalIndex < 0)
            {
                return activeEnemyRuntimes.Count > 0;
            }

            activeEnemyRuntimes.RemoveAt(removalIndex);

            defeatedEnemiesInCurrentWave += 1;
            activeEnemiesInCurrentWave = activeEnemyRuntimes.Count;
            RetargetAlliesToNearestSearchableEnemies();
            SetEngagedEnemyCount(Mathf.Min(engagedEnemyCount, activeEnemiesInCurrentWave));
            EnemyDefeated?.Invoke(CurrentRemainingEnemyCount, removalIndex);

            if (activeEnemyRuntimes.Count > 0)
            {
                SyncLeadEnemyState();
                encounterSerial += 1;
                EncounterChanged?.Invoke();
                return true;
            }

            if (defeatedEnemiesInCurrentWave >= CurrentEnemyCountTarget && spawnedEnemiesInCurrentWave >= CurrentEnemyCountTarget)
            {
                return false;
            }

            enemyStats = null;
            currentEnemyData = null;
            enemyTraitRuntime = default;
            enemyAttackTimer = 0f;
            encounterSerial += 1;
            EncounterChanged?.Invoke();
            return true;
        }

        private void RetargetAlliesToNearestSearchableEnemies()
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null || ally.Stats == null || ally.Stats.IsDead())
                {
                    continue;
                }

                int targetIndex = ResolvePreferredEnemyTargetIndex(ally, i);
                ally.TargetEnemyRuntimeId = targetIndex >= 0 ? activeEnemyRuntimes[targetIndex].RuntimeId : -1;
            }
        }

        private int ResolvePlayerAttackTargetIndex(MonsterDataSO attackerData)
        {
            int attackerIndex = ResolveLeadAliveAllyIndex();
            if (attackerIndex < 0 || attackerIndex >= activeAllyRuntimes.Count)
            {
                return -1;
            }

            return ResolveAllyTargetEnemyIndex(activeAllyRuntimes[attackerIndex], attackerIndex);
        }

        private List<int> CollectEnemyTargetIndices(int maxTargets, AllyRuntime attacker, int attackerIndex)
        {
            var result = new List<int>();
            int desiredCount = Mathf.Max(1, maxTargets);
            if (attacker == null || attackerIndex < 0 || attackerIndex >= activeAllyRuntimes.Count)
            {
                return result;
            }

            int primaryTargetIndex = ResolveAllyTargetEnemyIndex(attacker, attackerIndex);
            if (primaryTargetIndex < 0)
            {
                return result;
            }

            result.Add(primaryTargetIndex);
            if (desiredCount <= 1)
            {
                return result;
            }

            Vector2 referencePosition = activeEnemyRuntimes[primaryTargetIndex].PositionAnchor;
            while (result.Count < desiredCount)
            {
                int nextIndex = ResolveNearestAdditionalEnemyIndex(referencePosition, result);
                if (nextIndex < 0)
                {
                    break;
                }

                result.Add(nextIndex);
            }

            return result;
        }

        private bool HasEngagedEnemyTarget()
        {
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime == null || runtime.Stats == null || runtime.Stats.IsDead())
                {
                    continue;
                }

                int targetIndex = ResolveEnemyAttackTargetIndex(runtime, i);
                if (CanEnemyAttackTarget(runtime, i, targetIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private int FindDefeatedEnemyIndex()
        {
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime != null && runtime.Stats != null && runtime.Stats.IsDead())
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveEnemyRemovalIndex(int defeatedEnemyIndex)
        {
            if (defeatedEnemyIndex >= 0 && defeatedEnemyIndex < activeEnemyRuntimes.Count)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[defeatedEnemyIndex];
                if (runtime != null && runtime.Stats != null && runtime.Stats.IsDead())
                {
                    return defeatedEnemyIndex;
                }
            }

            return FindDefeatedEnemyIndex();
        }

        private void TickEnemyAttackers(float deltaTime)
        {
            if (activeEnemyRuntimes.Count == 0 || CountAliveAllies() <= 0)
            {
                enemyAttackTimer = 0f;
                return;
            }

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime attacker = activeEnemyRuntimes[i];
                if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
                {
                    continue;
                }

                int targetIndex = ResolveEnemyAttackTargetIndex(attacker, i);
                if (!CanEnemyAttackTarget(attacker, i, targetIndex))
                {
                    attacker.AttackTimer = 0f;
                    continue;
                }

                float interval = GetCurrentEnemyAttackInterval(attacker.Stats);
                attacker.AttackTimer += deltaTime;
                while (attacker.AttackTimer >= interval)
                {
                    attacker.AttackTimer -= interval;
                    PerformAttackOnPlayer(attacker, i);
                    if (CountAliveAllies() <= 0)
                    {
                        break;
                    }
                }

                if (CountAliveAllies() <= 0)
                {
                    break;
                }
            }

            enemyAttackTimer = activeEnemyRuntimes.Count > 0 ? activeEnemyRuntimes[0].AttackTimer : 0f;
        }

        private void NotifyAllyDefeatedIfNeeded(int allyIndex, bool wasAlive)
        {
            if (!wasAlive || allyIndex < 0 || allyIndex >= activeAllyRuntimes.Count)
            {
                return;
            }

            AllyRuntime ally = activeAllyRuntimes[allyIndex];
            if (ally == null || ally.Stats == null || !ally.Stats.IsDead())
            {
                return;
            }

            ally.AttackTimer = 0f;
            AllyDefeated?.Invoke(allyIndex);
        }

        private int ResolveEnemyNormalAttackTargetCount(EnemyDataSO enemyData)
        {
            if (enemyData != null && enemyData.normalAttackTargetCount > 0)
            {
                return enemyData.normalAttackTargetCount;
            }

            return 1;
        }

        private int ResolvePlayerNormalAttackTargetCount(MonsterDataSO monsterData)
        {
            if (monsterData != null && monsterData.normalAttackTargetCount > 0)
            {
                return monsterData.normalAttackTargetCount;
            }

            return 1;
        }

        private BattleUnitStats CreateEnemyStats(int floor, bool isBossEncounter, out EnemyTraitRuntime runtime, out EnemyDataSO enemyData)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            enemyData = floorData != null ? floorData.enemyData : null;
            EnemyDataSO devEnemyData = masterDataManager != null ? masterDataManager.GetEnemyData(DevEnemyOverrideId) : null;
            if (devEnemyData != null)
            {
                enemyData = devEnemyData;
            }

            if (enemyData == null)
            {
                runtime = EnemyTraitResolver.Resolve(EnemyTrait.None);
                int fallbackHp = 40;
                int fallbackAttack = 8;
                int fallbackDefense = 2;
                if (isBossEncounter)
                {
                    fallbackHp = Mathf.RoundToInt(fallbackHp * bossHpMultiplier);
                    fallbackAttack = Mathf.RoundToInt(fallbackAttack * bossAttackMultiplier);
                    fallbackDefense += bossDefenseBonus;
                }

                return new BattleUnitStats
                {
                    MaxHp = fallbackHp,
                    CurrentHp = fallbackHp,
                    Attack = fallbackAttack,
                    Wisdom = fallbackAttack,
                    Defense = fallbackDefense,
                    MagicDefense = fallbackDefense,
                    AttackSpeed = 0.8f,
                    CritRate = 0.03f,
                    CritDamage = 1.3f
                };
            }

            runtime = EnemyTraitResolver.Resolve(enemyData.enemyTrait);
            int maxHp = enemyData.maxHp;
            int attack = Mathf.RoundToInt(enemyData.attack * runtime.AttackMultiplier);
            int wisdom = Mathf.RoundToInt(Mathf.Max(enemyData.magicAttack, enemyData.attack) * runtime.AttackMultiplier);
            int defense = enemyData.defense + runtime.DefenseBonus;
            int magicDefense = enemyData.magicDefense + runtime.DefenseBonus;
            float attackSpeed = enemyData.attackSpeed * runtime.AttackSpeedMultiplier;
            float critRate = enemyData.critRate + runtime.CritRateBonus;

            if (isBossEncounter)
            {
                maxHp = Mathf.Max(maxHp + 1, Mathf.RoundToInt(maxHp * bossHpMultiplier));
                attack = Mathf.Max(attack + 1, Mathf.RoundToInt(attack * bossAttackMultiplier));
                wisdom = Mathf.Max(wisdom + 1, Mathf.RoundToInt(wisdom * bossAttackMultiplier));
                defense += bossDefenseBonus;
                magicDefense += bossDefenseBonus;
                attackSpeed *= 1.1f;
                critRate += 0.05f;
            }

            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = attack,
                Wisdom = wisdom,
                Defense = defense,
                MagicDefense = magicDefense,
                AttackSpeed = attackSpeed,
                CritRate = critRate,
                CritDamage = enemyData.critDamage
            };
        }

        public int GetCurrentPlayerDefense()
        {
            var guardBonus = guardRemainingTime > 0f ? guardDefenseBonus : 0;
            AllyRuntime leadAlly = ResolveLeadAliveAllyRuntime();
            return leadAlly != null && leadAlly.Stats != null ? leadAlly.Stats.Defense + guardBonus : 0;
        }

        private int GetCurrentPlayerDefense(AllyRuntime ally)
        {
            if (ally == null || ally.Stats == null)
            {
                return 0;
            }

            var guardBonus = guardRemainingTime > 0f ? guardDefenseBonus : 0;
            return ally.Stats.Defense + guardBonus;
        }

        private BattleUnitStats BuildCurrentPlayerDefenseSnapshot(AllyRuntime ally)
        {
            if (ally == null || ally.Stats == null)
            {
                return null;
            }

            int guardBonus = guardRemainingTime > 0f ? guardDefenseBonus : 0;
            return new BattleUnitStats
            {
                MaxHp = ally.Stats.MaxHp,
                CurrentHp = ally.Stats.CurrentHp,
                Attack = ally.Stats.Attack,
                Wisdom = ally.Stats.Wisdom,
                Defense = ally.Stats.Defense + guardBonus,
                MagicDefense = ally.Stats.MagicDefense + guardBonus,
                AttackSpeed = ally.Stats.AttackSpeed,
                CritRate = ally.Stats.CritRate,
                CritDamage = ally.Stats.CritDamage
            };
        }

        private static MonsterDamageType ResolveEnemyDamageType(EnemyDataSO enemyData)
        {
            return enemyData != null ? enemyData.damageType : MonsterDamageType.Physical;
        }

        private static MonsterDamageType ResolvePlayerDamageType(MonsterDataSO monsterData)
        {
            return monsterData != null ? monsterData.damageType : MonsterDamageType.Physical;
        }

        private static bool IsMonsterMelee(MonsterDataSO monsterData)
        {
            return monsterData == null || monsterData.rangeType == MonsterRangeType.Melee;
        }

        private static float ResolvePlayerPresentationDelay(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return 0f;
            }

            if (monsterData.rangeType == MonsterRangeType.Melee)
            {
                return MeleePresentationDelay;
            }

            return monsterData.damageType == MonsterDamageType.Magic
                ? MagicPresentationDelay
                : 0f;
        }

        private static float ResolveEnemyPresentationDelay(EnemyDataSO enemyData)
        {
            if (enemyData == null)
            {
                return 0f;
            }

            if (enemyData.attackRange < RangedAttackThreshold)
            {
                return MeleePresentationDelay;
            }

            return enemyData.damageType == MonsterDamageType.Magic
                ? MagicPresentationDelay
                : 0f;
        }

        private float GetCurrentEnemyAttackInterval(BattleUnitStats stats)
        {
            if (stats == null || stats.AttackSpeed <= 0f)
            {
                return enemyAttackInterval;
            }

            return enemyAttackInterval / AttackSpeedUtility.ResolveAttackRateMultiplier(stats.AttackSpeed);
        }

        private float GetCurrentPlayerAttackInterval(BattleUnitStats stats)
        {
            if (stats == null || stats.AttackSpeed <= 0f)
            {
                return playerAttackInterval;
            }

            return playerAttackInterval / AttackSpeedUtility.ResolveAttackRateMultiplier(stats.AttackSpeed);
        }

        private void ApplyEnemyLifeSteal(EnemyRuntime attacker, int dealtDamage)
        {
            if (attacker == null || attacker.Stats == null || attacker.Trait.LifeStealRate <= 0f || dealtDamage <= 0)
            {
                return;
            }

            var healAmount = Mathf.Max(1, Mathf.RoundToInt(dealtDamage * attacker.Trait.LifeStealRate));
            attacker.Stats.CurrentHp = Mathf.Min(attacker.Stats.MaxHp, attacker.Stats.CurrentHp + healAmount);
            SyncLeadEnemyState();
        }

        private void SyncLeadEnemyState()
        {
            if (activeEnemyRuntimes.Count <= 0)
            {
                enemyStats = null;
                currentEnemyData = null;
                enemyTraitRuntime = default;
                enemyAttackTimer = 0f;
                return;
            }

            EnemyRuntime leadEnemy = activeEnemyRuntimes[0];
            enemyStats = leadEnemy.Stats;
            currentEnemyData = leadEnemy.Data;
            enemyTraitRuntime = leadEnemy.Trait;
            enemyAttackTimer = leadEnemy.AttackTimer;
        }

        private void RaiseHitResolved(BattleHitInfo hitInfo)
        {
            HitResolved?.Invoke(hitInfo);
        }
    }
}
