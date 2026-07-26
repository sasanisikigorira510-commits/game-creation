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
        private static readonly HashSet<string> ResponsiveMeleeLineageMonsterIds = new HashSet<string>
        {
            "monster_apprentice_swordsman",
            "monster_holy_armor_leon",
            "monster_sword_saint_alvarez",
            "monster_rock_golem",
            "monster_ore_giant_garm",
            "monster_cosmic_ore_fortress_golem",
            "monster_dragon_sword_saint_agito",
            "monster_mecha_sword_saint_gransaber",
            "monster_magic_sword_saint_luciel",
            "monster_drag_gaia",
            "monster_rock_knight_gaius"
        };
        private const float EnemySpawnX = 1.04f;
        private const float AllyMoveSpeed = 0.26f;
        private const float MeleeAllyMoveSpeed = 0.46f;
        private const float EnemyMoveSpeed = 0.42f;
        private const float AllyReturnSpeed = 0.34f;
        private const float EnemyReturnSpeed = 0.26f;
        private const float MonsterMoveSpeedMultiplier = 0.34f;
        private const float EnemySpawnIntervalMultiplier = 2.0f;
        private const float DefaultAllyCombatRadius = 0.035f;
        private const float DefaultEnemyCombatRadius = 0.037f;
        private const float RangeOffsetPadding = 0.012f;
        private const float PositionEpsilon = 0.0025f;
        private const float AllyAttackForgivenessX = 0.16f;
        private const float AllyAttackForgivenessY = 0.18f;
        private const int RearAllySlotStartIndex = 3;
        private const float RangedAllySearchReach = 1.02f;
        private const float RearRangedAllySearchReach = 1.02f;
        private const float ForwardLimitAttackForgivenessBonusX = 0.10f;
        private const float ForwardLimitAttackForgivenessBonusY = 0.04f;
        private const float ForwardLimitPositionEpsilon = 0.006f;
        private const float CloseCombatVisualEngagementPaddingX = 0.035f;
        private const float CloseCombatVisualEngagementPaddingY = 0.045f;
        private const float ReferenceSpawnInterval = 0.20f;
        private const int ReferenceEncounterEnemyCount = 5;
        private const int ReferenceOpeningSpawnBurst = 3;
        private const int MaxEncounterEnemyCount = 240;
        private const int MinEncounterEnemyCount = 3;
        private const int LargeEncounterEnemyCount = 40;
        private const int MassiveEncounterEnemyCount = 100;
        private const int MaxConcurrentEnemyAttackersPerAlly = 5;
        private const float EnemyQueueSpacing = 0.12f;
        private const float EnemyQueueLaneBlend = 0.18f;
        private const float AllyMeleeVerticalFollowStrength = 0.34f;
        private const float AllyMeleeVerticalLeash = 0.11f;
        private const float EnemyCombatLaneBlend = 0.78f;
        private const float EnemyCombatVerticalLeash = 0.075f;
        private const float SparsePartySpawnInterval = 0.26f;
        private const float DuoPartySpawnInterval = 0.22f;

        private sealed class AllyRuntime
        {
            public int RuntimeId;
            public int SlotIndex;
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
            public float AttackMotionLockRemaining;
            public int OpeningAttackPrimedEnemyRuntimeId = -1;
        }

        private sealed class EnemyRuntime
        {
            public int RuntimeId;
            public BattleUnitStats Stats;
            public EnemyDataSO Data;
            public EnemyTraitRuntime Trait;
            public bool IsBoss;
            public float AttackTimer;
            public Vector2 HomeAnchor;
            public Vector2 PositionAnchor;
            public int TargetAllyRuntimeId = -1;
            public float CombatRadius;
            public float AttackReachAnchor;
            public float MoveSpeed;
            public bool IsMoving;
            public float AttackMotionLockRemaining;
        }

        private sealed class EnemyQueueIndexComparer : IComparer<int>
        {
            private readonly BattleSimulator owner;

            public EnemyQueueIndexComparer(BattleSimulator owner)
            {
                this.owner = owner;
            }

            public int TargetAllyIndex { get; set; } = -1;

            public int Compare(int leftIndex, int rightIndex)
            {
                return owner.CompareEnemyQueueOrder(leftIndex, rightIndex, TargetAllyIndex);
            }
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
        private BattleSpiritModifier battleSpiritModifier = BattleSpiritModifier.Identity;
        private bool battleSpiritInvoked;
        private BattleSpiritDefinition invokedBattleSpiritDefinition;
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
        private int defeatedEnemyMaxHpInCurrentWave;
        private int anticipatedUnspawnedEnemyMaxHpInCurrentWave;
        private readonly List<int> anticipatedEnemyMaxHpBySpawnIndex = new List<int>();
        private int encounterEnemyCountTarget;
        private int encounterSerial;
        private float enemySpawnTimer;
        private int engagedEnemyCount;
        private bool isBossEncounter;
        private bool currentEnemyIsBoss;
        private string[] finalBossMonsterIds = new string[0];
        private bool openingBurstSpawned;
        private int nextAllyRuntimeId = 1;
        private int nextEnemyRuntimeId = 1;
        private readonly List<AllyRuntime> activeAllyRuntimes = new List<AllyRuntime>();
        private readonly List<EnemyRuntime> activeEnemyRuntimes = new List<EnemyRuntime>();
        private readonly List<int> cachedEnemyTargetAllyIndices = new List<int>();
        private readonly List<int> cachedEnemyQueueIndices = new List<int>();
        private readonly List<int> enemyQueueSortScratch = new List<int>();
        private EnemyQueueIndexComparer enemyQueueIndexComparer;
        private const float MeleePresentationDelay = 0.18f;
        private const float TargetImpactPresentationDelay = 0.22f;
        private const float DefaultRangedImpactPresentationDelay = 0.32f;
        private const float RangedAttackThreshold = 1.35f;
        private const float AttackMotionLockDuration = 0.34f;
        private const float MinMeleeAllyAttackReach = 0.04f;
        private const float ResponsiveMeleeAllyAttackReach = 0.085f;
        private const float ResponsiveMeleeClass2SearchReach = 0.78f;
        private const float ResponsiveMeleeMidlineHomeX = 0.30f;
        private const float ResponsiveMeleeRearHomeX = 0.24f;
        private const float ResponsiveMeleeClass1RearMoveMultiplier = 1.18f;
        private const float ResponsiveMeleeClass2RearMoveMultiplier = 1.02f;
        private const float ResponsiveMeleeAdvancedRearMoveMultiplier = 0.70f;
        private const float ResponsiveMeleeClass2OpeningAttackReadiness = 0.82f;
        private const float RearMeleeMoveSpeedMultiplier = 1.6f;
        private const float RearMeleeEngagementMoveMultiplier = 1.35f;
        private static readonly Dictionary<string, float> ProjectileImpactPresentationDelays = new Dictionary<string, float>
        {
            { "monster_dragon_whelp", 0.34f },
            { "monster_flare_drake", 0.42f },
            { "monster_abyss_dragon", 0.58f },
            { "monster_abyss_grand_mage_seraphis", 0.48f },
            { "monster_mecha_dragon_valdrake", 0.60f },
            { "monster_abyss_dragon_mage_valflare", 0.50f },
            { "monster_fortress_machine_gigafort", 0.34f },
            { "monster_bat", 0.30f },
            { "monster_bee", 0.26f },
            { "monster_centaur", 0.30f }
        };

        public event System.Action<BattleHitInfo> HitResolved;
        public event System.Action EncounterChanged;
        public event System.Action<int, int, EnemyDataSO, bool> EnemyDefeated;
        public event System.Action<int> AllyDefeated;
        public event System.Action<BattleSpiritDefinition> SpiritInvoked;

        public BattleUnitStats PlayerStats => playerStats;
        public BattleUnitStats EnemyStats => enemyStats;
        public bool IsRunning => isRunning;
        public bool BattleSpiritInvoked => battleSpiritInvoked;
        public BattleSpiritDefinition InvokedBattleSpiritDefinition => invokedBattleSpiritDefinition;
        public BattleSpiritModifier ActiveBattleSpiritModifier => battleSpiritModifier;
        public int DebugTickCount => tickCount;
        public float DebugLastDeltaTime => lastDeltaTime;
        public float DebugPlayerAttackTimer => ResolveLeadAliveAllyRuntime()?.AttackTimer ?? 0f;
        public float DebugEnemyAttackTimer => enemyAttackTimer;
        public float DebugGuardRemainingTime => guardRemainingTime;
        public int CurrentFloor => currentFloor;
        public int CurrentWave => currentWave;
        public int TotalWaveCount => 1;
        public bool IsBossWave => isBossEncounter || currentEnemyIsBoss;
        public int EncounterSerial => encounterSerial;
        public EnemyDataSO CurrentEnemyData => currentEnemyData;
        public int CurrentEnemyCountTarget => Mathf.Max(1, encounterEnemyCountTarget);
        public int CurrentEnemyIndexInWave => Mathf.Clamp(defeatedEnemiesInCurrentWave + 1, 1, CurrentEnemyCountTarget);
        public int CurrentRemainingEnemyCount => Mathf.Max(0, CurrentEnemyCountTarget - defeatedEnemiesInCurrentWave);
        public int CurrentSpawnedEnemyCount => Mathf.Max(0, spawnedEnemiesInCurrentWave);
        public int CurrentActiveEnemyCount => Mathf.Max(0, activeEnemiesInCurrentWave);
        public int CurrentEngagedEnemyCount => Mathf.Max(0, engagedEnemyCount);
        public int CurrentWaveEnemyCurrentHp => CalculateCurrentWaveEnemyCurrentHp();
        public int CurrentWaveEnemyMaxHp => CalculateCurrentWaveEnemyMaxHp();
        public int CurrentAliveAllyCount => CountAliveAllies();
        public int CurrentAllyRuntimeCount => BattleFormationLayout.AllyHomeAnchors.Length;
        public int CurrentPreferredEnemyTargetIndex
        {
            get
            {
                int activeTargetIndex = activeEnemyRuntimes.Count > 0
                    ? ResolveCachedEnemyTargetAllyIndex(activeEnemyRuntimes[0], 0)
                    : -1;
                return activeTargetIndex >= 0 && activeTargetIndex < activeAllyRuntimes.Count && activeAllyRuntimes[activeTargetIndex] != null
                    ? activeAllyRuntimes[activeTargetIndex].SlotIndex
                    : -1;
            }
        }

        public void Setup(int floor)
        {
            currentFloor = Mathf.Max(1, floor);
            currentWave = 1;
            isBossEncounter = ResolveBossEncounter(currentFloor);
            finalBossMonsterIds = isBossEncounter ? new string[0] : BattleDungeonCatalog.ResolveBossMonsterIds(currentFloor);
            currentEnemyIsBoss = false;
            encounterEnemyCountTarget = ResolveEncounterEnemyCount();
            defeatedEnemiesInCurrentWave = 0;
            spawnedEnemiesInCurrentWave = 0;
            activeEnemiesInCurrentWave = 0;
            defeatedEnemyMaxHpInCurrentWave = 0;
            BuildAnticipatedEnemyMaxHpPlan();
            encounterSerial = 0;
            enemySpawnTimer = 0f;
            openingBurstSpawned = false;
            nextAllyRuntimeId = 1;
            nextEnemyRuntimeId = 1;
            battleSpiritModifier = BattleSpiritModifier.Identity;
            battleSpiritInvoked = false;
            invokedBattleSpiritDefinition = null;
            activeAllyRuntimes.Clear();
            CreatePlayerPartyRuntimes();
            SyncPlayerAggregateState();
            skillSet = new BattleSkillSet(battleSpiritModifier.SkillCooldownMultiplier);
            enemyAttackTimer = 0f;
            guardRemainingTime = 0f;
            enemyStats = null;
            currentEnemyData = null;
            enemyTraitRuntime = default;
            activeEnemyRuntimes.Clear();
            ClearEnemyMovementQueueCache();
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
            skillSet ??= new BattleSkillSet(battleSpiritModifier.SkillCooldownMultiplier);
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

        public bool TryInvokeSpirit(BattleSpiritType spiritType)
        {
            if (!isRunning || battleSpiritInvoked)
            {
                return false;
            }

            BattleSpiritDefinition definition = BattleSpiritCatalog.GetDefinition(spiritType);
            if (definition == null)
            {
                return false;
            }

            battleSpiritModifier = definition.Modifier;
            battleSpiritInvoked = true;
            invokedBattleSpiritDefinition = definition;
            ApplySpiritStatModifiersToAllies();
            SyncPlayerAggregateState();
            skillSet = new BattleSkillSet(battleSpiritModifier.SkillCooldownMultiplier);
            SpiritInvoked?.Invoke(definition);
            return true;
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

            EnemyRuntime targetEnemy = activeEnemyRuntimes[targetIndex];
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            float powerRate = 2.0f * GetStrikePowerMultiplier(profile);
            var damage = Mathf.Max(1, Mathf.RoundToInt(attacker.Stats.Attack * powerRate) - targetEnemy.Stats.Defense);
            targetEnemy.Stats.ApplyDamage(damage);
            if (targetIndex == 0)
            {
                SyncLeadEnemyState();
            }

            LockAttackMotion(attacker);
            RaiseHitResolved(new BattleHitInfo(
                false,
                damage,
                false,
                true,
                false,
                targetIndex,
                attacker.SlotIndex,
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

            EnemyRuntime targetEnemy = activeEnemyRuntimes[targetIndex];
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            var damage = Mathf.Max(1, Mathf.RoundToInt(attacker.Stats.Attack * 1.2f) - targetEnemy.Stats.Defense);
            targetEnemy.Stats.ApplyDamage(damage);
            if (targetIndex == 0)
            {
                SyncLeadEnemyState();
            }

            var healAmount = Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f * GetDrainHealMultiplier(profile)));
            attacker.Stats.CurrentHp = Mathf.Min(attacker.Stats.MaxHp, attacker.Stats.CurrentHp + healAmount);
            SyncPlayerAggregateState();
            LockAttackMotion(attacker);
            RaiseHitResolved(new BattleHitInfo(
                false,
                damage,
                false,
                true,
                false,
                targetIndex,
                attacker.SlotIndex,
                ResolvePlayerPresentationDelay(attacker.Data)));
        }

        private void UseSkillGuard()
        {
            guardRemainingTime = Mathf.Max(0.1f, guardDuration * battleSpiritModifier.GuardDurationMultiplier);
        }

        private static float GetStrikePowerMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetStrikePowerMultiplier() : 1f;
        }

        private static float GetDrainHealMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetDrainHealMultiplier() : 1f;
        }

        private void PerformAttackOnPlayer(EnemyRuntime attacker, int attackerIndex)
        {
            if (attacker == null || attacker.Stats == null)
            {
                return;
            }

            int targetIndex = ResolveCachedEnemyTargetAllyIndex(attacker, attackerIndex);
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
            LockAttackMotion(attacker);
            RaiseHitResolved(new BattleHitInfo(
                true,
                totalDamage,
                result.IsCritical,
                false,
                false,
                targetAlly.SlotIndex,
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
            var targetHits = new List<BattleHitTargetInfo>();

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
                targetHits.Add(new BattleHitTargetInfo(targetIndex, damage));
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

            LockAttackMotion(attacker);
            RaiseHitResolved(new BattleHitInfo(
                false,
                totalDamage,
                anyCritical,
                isSkill,
                false,
                primaryTargetIndex,
                attacker.SlotIndex,
                targetHits,
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
                    attacker.AttackTimer = Mathf.Min(attacker.AttackTimer + deltaTime, interval * 0.97f);
                    continue;
                }

                PrimeOpeningAttackIfNeeded(attacker, targetIndex, interval);
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
                    ally.AttackMotionLockRemaining = 0f;
                    ally.PositionAnchor = Vector2.MoveTowards(
                        ally.PositionAnchor,
                        ally.HomeAnchor,
                        AllyReturnSpeed * MonsterMoveSpeedMultiplier * deltaTime);
                    continue;
                }

                if (ally.AttackMotionLockRemaining > 0f)
                {
                    ally.AttackMotionLockRemaining = Mathf.Max(0f, ally.AttackMotionLockRemaining - deltaTime);
                    ally.IsMoving = false;
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
                    ? ResolveAllyMeleeCombatAnchor(ally, targetEnemy.PositionAnchor, desiredSeparation)
                    : new Vector2(targetEnemy.PositionAnchor.x - desiredSeparation, ally.HomeAnchor.y);
                targetAnchor.x += ResolveAllyCombatPressureAdvance(ally.SlotIndex, ally.Data);
                targetAnchor = BattleFormationLayout.ClampAllyCombatAnchor(ally.SlotIndex, ally.Data, targetAnchor, ally.HomeAnchor);
                if (!IsMonsterMelee(ally.Data))
                {
                    targetAnchor.y = ally.HomeAnchor.y;
                }
                float movementSpeed = ally.MoveSpeed;
                if (IsRearAllySlot(ally.SlotIndex) &&
                    IsMonsterMelee(ally.Data) &&
                    !CanAllyAttackTarget(ally, targetEnemyIndex))
                {
                    // Rear melee units have farther to travel after the front line has
                    // engaged. Accelerate only that first approach so they do not look
                    // idle while waiting for a reachable attack position.
                    movementSpeed *= RearMeleeEngagementMoveMultiplier;
                }

                ally.PositionAnchor = MoveRuntimeTowards(ally.PositionAnchor, targetAnchor, movementSpeed, deltaTime, out bool allyMoving);
                ally.IsMoving = allyMoving;
            }

            RebuildEnemyMovementQueueCache();
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
                    enemy.AttackMotionLockRemaining = 0f;
                    enemy.PositionAnchor = Vector2.MoveTowards(
                        enemy.PositionAnchor,
                        enemy.HomeAnchor,
                        EnemyReturnSpeed * MonsterMoveSpeedMultiplier * deltaTime);
                    continue;
                }

                if (enemy.AttackMotionLockRemaining > 0f)
                {
                    enemy.AttackMotionLockRemaining = Mathf.Max(0f, enemy.AttackMotionLockRemaining - deltaTime);
                    enemy.IsMoving = false;
                    continue;
                }

                int targetAllyIndex = ResolveCachedEnemyTargetAllyIndex(enemy, i);
                if (targetAllyIndex < 0)
                {
                    enemy.PositionAnchor = MoveRuntimeTowards(enemy.PositionAnchor, enemy.HomeAnchor, enemy.MoveSpeed, deltaTime, out bool enemyReturning);
                    enemy.IsMoving = enemyReturning;
                    continue;
                }

                AllyRuntime targetAlly = activeAllyRuntimes[targetAllyIndex];
                int queueIndex = ResolveCachedEnemyQueueIndex(i);
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
            float searchOriginX = Mathf.Max(ally.HomeAnchor.x, ally.PositionAnchor.x);
            float searchThresholdX = searchOriginX + Mathf.Max(ally.AttackReachAnchor, ally.SearchReachAnchor);
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

        private void RebuildEnemyMovementQueueCache()
        {
            cachedEnemyTargetAllyIndices.Clear();
            cachedEnemyQueueIndices.Clear();
            int enemyCount = activeEnemyRuntimes.Count;
            for (int i = 0; i < enemyCount; i += 1)
            {
                cachedEnemyTargetAllyIndices.Add(-1);
                cachedEnemyQueueIndices.Add(0);
            }

            for (int i = 0; i < enemyCount; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (!IsActiveEnemyRuntime(enemy))
                {
                    continue;
                }

                cachedEnemyTargetAllyIndices[i] = ResolveEnemyAttackTargetIndex(enemy, i);
            }

            EnemyQueueIndexComparer comparer = ResolveEnemyQueueIndexComparer();
            for (int allyIndex = 0; allyIndex < activeAllyRuntimes.Count; allyIndex += 1)
            {
                enemyQueueSortScratch.Clear();
                for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex += 1)
                {
                    if (cachedEnemyTargetAllyIndices[enemyIndex] == allyIndex &&
                        IsActiveEnemyRuntimeIndex(enemyIndex))
                    {
                        enemyQueueSortScratch.Add(enemyIndex);
                    }
                }

                if (enemyQueueSortScratch.Count <= 1)
                {
                    if (enemyQueueSortScratch.Count == 1)
                    {
                        cachedEnemyQueueIndices[enemyQueueSortScratch[0]] = 0;
                    }

                    continue;
                }

                comparer.TargetAllyIndex = allyIndex;
                enemyQueueSortScratch.Sort(comparer);
                for (int queueIndex = 0; queueIndex < enemyQueueSortScratch.Count; queueIndex += 1)
                {
                    cachedEnemyQueueIndices[enemyQueueSortScratch[queueIndex]] = queueIndex;
                }
            }

            comparer.TargetAllyIndex = -1;
            enemyQueueSortScratch.Clear();
        }

        private void ClearEnemyMovementQueueCache()
        {
            cachedEnemyTargetAllyIndices.Clear();
            cachedEnemyQueueIndices.Clear();
            enemyQueueSortScratch.Clear();
        }

        private EnemyQueueIndexComparer ResolveEnemyQueueIndexComparer()
        {
            if (enemyQueueIndexComparer == null)
            {
                enemyQueueIndexComparer = new EnemyQueueIndexComparer(this);
            }

            return enemyQueueIndexComparer;
        }

        private int ResolveCachedEnemyTargetAllyIndex(EnemyRuntime attacker, int attackerIndex)
        {
            if (attacker == null)
            {
                return -1;
            }

            bool cacheMatchesActiveEnemies = cachedEnemyTargetAllyIndices.Count == activeEnemyRuntimes.Count;
            if (cacheMatchesActiveEnemies &&
                attackerIndex >= 0 &&
                attackerIndex < cachedEnemyTargetAllyIndices.Count)
            {
                int cachedIndex = cachedEnemyTargetAllyIndices[attackerIndex];
                if (IsActiveAllyRuntimeIndex(cachedIndex))
                {
                    return cachedIndex;
                }
            }

            int resolvedIndex = ResolveEnemyAttackTargetIndex(attacker, attackerIndex);
            if (cacheMatchesActiveEnemies &&
                attackerIndex >= 0 &&
                attackerIndex < cachedEnemyTargetAllyIndices.Count)
            {
                cachedEnemyTargetAllyIndices[attackerIndex] = resolvedIndex;
            }

            return resolvedIndex;
        }

        private int ResolveCachedEnemyQueueIndex(int enemyIndex)
        {
            bool cacheMatchesActiveEnemies = cachedEnemyQueueIndices.Count == activeEnemyRuntimes.Count;
            return cacheMatchesActiveEnemies &&
                enemyIndex >= 0 &&
                enemyIndex < cachedEnemyQueueIndices.Count
                    ? Mathf.Max(0, cachedEnemyQueueIndices[enemyIndex])
                    : 0;
        }

        private int CompareEnemyQueueOrder(int leftIndex, int rightIndex, int targetAllyIndex)
        {
            if (leftIndex == rightIndex)
            {
                return 0;
            }

            EnemyRuntime left = leftIndex >= 0 && leftIndex < activeEnemyRuntimes.Count ? activeEnemyRuntimes[leftIndex] : null;
            EnemyRuntime right = rightIndex >= 0 && rightIndex < activeEnemyRuntimes.Count ? activeEnemyRuntimes[rightIndex] : null;
            bool leftActive = IsActiveEnemyRuntime(left);
            bool rightActive = IsActiveEnemyRuntime(right);
            if (!leftActive || !rightActive)
            {
                if (leftActive == rightActive)
                {
                    return leftIndex.CompareTo(rightIndex);
                }

                return leftActive ? -1 : 1;
            }

            if (!IsActiveAllyRuntimeIndex(targetAllyIndex))
            {
                return leftIndex.CompareTo(rightIndex);
            }

            AllyRuntime targetAlly = activeAllyRuntimes[targetAllyIndex];
            float leftDistance = Vector2.SqrMagnitude(left.PositionAnchor - targetAlly.PositionAnchor);
            float rightDistance = Vector2.SqrMagnitude(right.PositionAnchor - targetAlly.PositionAnchor);
            if (Mathf.Abs(leftDistance - rightDistance) > 0.0001f)
            {
                return leftDistance < rightDistance ? -1 : 1;
            }

            float xDifference = left.PositionAnchor.x - right.PositionAnchor.x;
            if (Mathf.Abs(xDifference) > PositionEpsilon)
            {
                return xDifference < 0f ? -1 : 1;
            }

            int runtimeComparison = left.RuntimeId.CompareTo(right.RuntimeId);
            return runtimeComparison != 0 ? runtimeComparison : leftIndex.CompareTo(rightIndex);
        }

        private bool IsActiveEnemyRuntimeIndex(int enemyIndex)
        {
            return enemyIndex >= 0 &&
                enemyIndex < activeEnemyRuntimes.Count &&
                IsActiveEnemyRuntime(activeEnemyRuntimes[enemyIndex]);
        }

        private bool IsActiveAllyRuntimeIndex(int allyIndex)
        {
            if (allyIndex < 0 || allyIndex >= activeAllyRuntimes.Count)
            {
                return false;
            }

            AllyRuntime ally = activeAllyRuntimes[allyIndex];
            return ally != null && ally.Stats != null && !ally.Stats.IsDead();
        }

        private static bool IsActiveEnemyRuntime(EnemyRuntime enemy)
        {
            return enemy != null && enemy.Stats != null && !enemy.Stats.IsDead();
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
            float queueLaneY = Mathf.Lerp(enemy.HomeAnchor.y, targetAlly.HomeAnchor.y, EnemyQueueLaneBlend);
            float laneBlend = Mathf.Clamp01(EnemyCombatLaneBlend + (ringIndex * 0.18f));
            combatAnchor.y = Mathf.Lerp(combatAnchor.y, queueLaneY, laneBlend);
            combatAnchor.y = Mathf.Clamp(
                combatAnchor.y,
                queueLaneY - EnemyCombatVerticalLeash,
                queueLaneY + EnemyCombatVerticalLeash);
            if (ringIndex > 0)
            {
                combatAnchor.x = Mathf.Max(combatAnchor.x, targetAlly.PositionAnchor.x + baseSeparation);
            }

            return combatAnchor;
        }

        private void CreatePlayerPartyRuntimes()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            MasterDataManager.Instance?.Initialize();

            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsterSlots(profile, 5);
            bool useDebugParty = !HasAnyPartyMonster(partyMonsters);
            int desiredCount = useDebugParty
                ? DevPartyOverrideMonsterIds.Length
                : Mathf.Min(partyMonsters.Count, BattleFormationLayout.AllyHomeAnchors.Length);
            for (int i = 0; i < desiredCount; i += 1)
            {
                OwnedMonsterData ownedMonster = !useDebugParty && i < partyMonsters.Count ? partyMonsters[i] : null;
                string monsterId;
                if (useDebugParty)
                {
                    monsterId = i < DevPartyOverrideMonsterIds.Length ? DevPartyOverrideMonsterIds[i] : null;
                }
                else
                {
                    if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.MonsterId))
                    {
                        continue;
                    }

                    monsterId = ownedMonster.MonsterId;
                }

                if (string.IsNullOrEmpty(monsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monsterId);
                if (monsterData == null)
                {
                    continue;
                }

                int slotIndex = i;
                Vector2 homeAnchor = ResolveAllyHomeAnchor(slotIndex, monsterData);
                BattleUnitStats allyStats = MonsterBattleStatsFactory.Create(profile, ownedMonster, monsterData);
                ApplySpiritStatModifiers(allyStats);
                activeAllyRuntimes.Add(new AllyRuntime
                {
                    RuntimeId = nextAllyRuntimeId++,
                    SlotIndex = slotIndex,
                    Stats = allyStats,
                    Data = monsterData,
                    OwnedMonster = ownedMonster,
                    AttackTimer = 0f,
                    HomeAnchor = homeAnchor,
                    PositionAnchor = homeAnchor,
                    CombatRadius = ResolveAllyCombatRadius(monsterData),
                    AttackReachAnchor = ResolveAllyAttackReach(monsterData),
                    SearchReachAnchor = ResolveAllySearchReach(monsterData, slotIndex),
                    MoveSpeed = ResolveAllyMoveSpeed(monsterData, slotIndex)
                });
            }

            if (activeAllyRuntimes.Count > 0)
            {
                return;
            }

            BattleUnitStats fallbackStats = PlayerBattleStatsFactory.CreatePreview(profile);
            if (fallbackStats != null)
            {
                ApplySpiritStatModifiers(fallbackStats);
                int allyIndex = activeAllyRuntimes.Count;
                Vector2 homeAnchor = ResolveAllyHomeAnchor(allyIndex, null);
                activeAllyRuntimes.Add(new AllyRuntime
                {
                    RuntimeId = nextAllyRuntimeId++,
                    SlotIndex = allyIndex,
                    Stats = fallbackStats,
                    Data = null,
                    OwnedMonster = null,
                    AttackTimer = 0f,
                    HomeAnchor = homeAnchor,
                    PositionAnchor = homeAnchor,
                    CombatRadius = ResolveAllyCombatRadius(null),
                    AttackReachAnchor = ResolveAllyAttackReach(null),
                    SearchReachAnchor = ResolveAllySearchReach(null, allyIndex),
                    MoveSpeed = ResolveAllyMoveSpeed(null, allyIndex)
                });
            }
        }

        private void ApplySpiritStatModifiers(BattleUnitStats stats)
        {
            if (stats == null)
            {
                return;
            }

            float hpRatio = stats.MaxHp > 0
                ? Mathf.Clamp01((float)Mathf.Max(0, stats.CurrentHp) / stats.MaxHp)
                : 1f;
            stats.MaxHp = Mathf.Max(1, Mathf.RoundToInt(stats.MaxHp * battleSpiritModifier.MaxHpMultiplier));
            stats.CurrentHp = Mathf.Clamp(Mathf.RoundToInt(stats.MaxHp * hpRatio), 1, stats.MaxHp);
            stats.Attack = Mathf.Max(1, Mathf.RoundToInt(stats.Attack * battleSpiritModifier.AttackMultiplier));
            stats.Wisdom = Mathf.Max(1, Mathf.RoundToInt(stats.Wisdom * battleSpiritModifier.WisdomMultiplier));
            stats.Defense = Mathf.Max(0, Mathf.RoundToInt(stats.Defense * battleSpiritModifier.DefenseMultiplier));
            stats.MagicDefense = Mathf.Max(0, Mathf.RoundToInt(stats.MagicDefense * battleSpiritModifier.MagicDefenseMultiplier));
            stats.AttackSpeed = Mathf.Max(0.1f, stats.AttackSpeed * battleSpiritModifier.AttackSpeedMultiplier);
            stats.CritRate = Mathf.Clamp01(stats.CritRate + battleSpiritModifier.CritRateBonus);
            stats.CritDamage = Mathf.Max(1f, stats.CritDamage + battleSpiritModifier.CritDamageBonus);
        }

        private void ApplySpiritStatModifiersToAllies()
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null || ally.Stats == null || ally.Stats.IsDead())
                {
                    continue;
                }

                ApplySpiritStatModifiers(ally.Stats);
            }
        }

        private static bool HasAnyPartyMonster(List<OwnedMonsterData> partyMonsters)
        {
            if (partyMonsters == null)
            {
                return false;
            }

            for (int i = 0; i < partyMonsters.Count; i += 1)
            {
                if (partyMonsters[i] != null)
                {
                    return true;
                }
            }

            return false;
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
                    if (!CanAllyAttackTarget(ally, lockedIndex))
                    {
                        int preferredIndex = ResolvePreferredEnemyTargetIndex(ally, allyIndex);
                        if (preferredIndex >= 0 &&
                            preferredIndex != lockedIndex &&
                            IsEnemyInsideAllyAttackSelectionRange(ally, activeEnemyRuntimes[preferredIndex]))
                        {
                            ally.TargetEnemyRuntimeId = activeEnemyRuntimes[preferredIndex].RuntimeId;
                            return preferredIndex;
                        }
                    }

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
            bool bestIsAttackable = false;
            float bestHorizontalGap = float.MaxValue;
            float bestVerticalGap = float.MaxValue;
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

                bool isAttackable = IsEnemyInsideAllyAttackSelectionRange(ally, enemy);
                float verticalGap = Mathf.Abs(enemy.PositionAnchor.y - referenceAnchor.y);
                float horizontalGap = Mathf.Abs(enemy.PositionAnchor.x - referenceAnchor.x);
                float distance = Vector2.SqrMagnitude(enemy.PositionAnchor - referenceAnchor);
                bool isBetter = bestIndex < 0;
                if (!isBetter && isAttackable != bestIsAttackable)
                {
                    isBetter = isAttackable;
                }
                else if (!isBetter && Mathf.Abs(horizontalGap - bestHorizontalGap) > 0.0001f)
                {
                    isBetter = horizontalGap < bestHorizontalGap;
                }
                else if (!isBetter && Mathf.Abs(verticalGap - bestVerticalGap) > 0.0001f)
                {
                    isBetter = verticalGap < bestVerticalGap;
                }
                else if (!isBetter)
                {
                    isBetter = distance < bestDistance;
                }

                if (isBetter)
                {
                    bestIsAttackable = isAttackable;
                    bestVerticalGap = verticalGap;
                    bestHorizontalGap = horizontalGap;
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static bool IsEnemyInsideAllyAttackSelectionRange(AllyRuntime attacker, EnemyRuntime target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            float attackDistance = attacker.CombatRadius + target.CombatRadius + attacker.AttackReachAnchor + PositionEpsilon;
            float horizontalGap = Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x);
            float forgivenessX = ResolveAllyAttackForgivenessX(attacker);
            if (!IsMonsterMelee(attacker.Data))
            {
                return horizontalGap <= attackDistance + forgivenessX;
            }

            float verticalGap = Mathf.Abs(attacker.PositionAnchor.y - target.PositionAnchor.y);
            return horizontalGap <= attackDistance + forgivenessX &&
                verticalGap <= attackDistance + ResolveAllyAttackForgivenessY(attacker);
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
            float resolvedReach = Mathf.Max(0f, BattleAttackRangeResolver.ToMonsterHoldOffset(monsterData) + RangeOffsetPadding);
            float minReach = IsResponsiveMeleeLineage(monsterData)
                ? ResponsiveMeleeAllyAttackReach
                : MinMeleeAllyAttackReach;
            return IsMonsterMelee(monsterData)
                ? Mathf.Max(resolvedReach, minReach)
                : resolvedReach;
        }

        private static bool IsRearAllySlot(int allyIndex)
        {
            return allyIndex >= RearAllySlotStartIndex;
        }

        private static float ResolveAllyAttackForgivenessX(AllyRuntime attacker)
        {
            return AllyAttackForgivenessX +
                (ShouldUseForwardLimitAttackBonus(attacker) ? ForwardLimitAttackForgivenessBonusX : 0f);
        }

        private static float ResolveAllyAttackForgivenessY(AllyRuntime attacker)
        {
            return AllyAttackForgivenessY +
                (ShouldUseForwardLimitAttackBonus(attacker) ? ForwardLimitAttackForgivenessBonusY : 0f);
        }

        private static bool ShouldUseForwardLimitAttackBonus(AllyRuntime attacker)
        {
            return attacker != null &&
                IsMonsterMelee(attacker.Data) &&
                IsAtForwardCombatLimit(attacker);
        }

        private static bool IsAtForwardCombatLimit(AllyRuntime attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            float maxX = attacker.HomeAnchor.x +
                BattleFormationLayout.ResolveAllyMaxCombatAdvance(attacker.SlotIndex, attacker.Data);
            return attacker.PositionAnchor.x >= maxX - ForwardLimitPositionEpsilon;
        }

        private static float ResolveAllySearchReach(MonsterDataSO monsterData, int allyIndex)
        {
            if (monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged)
            {
                return IsRearAllySlot(allyIndex) ? RearRangedAllySearchReach : RangedAllySearchReach;
            }

            float searchRange = BattleAttackRangeResolver.ResolveMonsterSearchRange(monsterData);
            float searchReach = Mathf.Max(0f, BattleAttackRangeResolver.ToAllySearchOffset(searchRange));
            return IsResponsiveMeleeLineage(monsterData) && monsterData.classRank == 2
                ? Mathf.Max(searchReach, ResponsiveMeleeClass2SearchReach)
                : searchReach;
        }

        private static float ResolveAllyMoveSpeed(MonsterDataSO monsterData, int allyIndex)
        {
            bool isMelee = IsMonsterMelee(monsterData);
            float baseSpeed = isMelee ? MeleeAllyMoveSpeed : AllyMoveSpeed;
            if (isMelee && allyIndex >= 2)
            {
                baseSpeed *= RearMeleeMoveSpeedMultiplier;
                if (IsResponsiveMeleeLineage(monsterData))
                {
                    baseSpeed *= ResolveResponsiveMeleeRearMoveMultiplier(monsterData);
                }
            }

            return baseSpeed * MonsterMoveSpeedMultiplier;
        }

        private static float ResolveResponsiveMeleeRearMoveMultiplier(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return ResponsiveMeleeAdvancedRearMoveMultiplier;
            }

            if (monsterData.classRank <= 1)
            {
                return ResponsiveMeleeClass1RearMoveMultiplier;
            }

            return monsterData.classRank == 2
                ? ResponsiveMeleeClass2RearMoveMultiplier
                : ResponsiveMeleeAdvancedRearMoveMultiplier;
        }

        private void PrimeOpeningAttackIfNeeded(AllyRuntime attacker, int targetEnemyIndex, float interval)
        {
            if (attacker == null ||
                interval <= 0f ||
                !ShouldPrimeResponsiveMeleeOpeningAttack(attacker.Data) ||
                targetEnemyIndex < 0 ||
                targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return;
            }

            EnemyRuntime target = activeEnemyRuntimes[targetEnemyIndex];
            if (target == null || attacker.OpeningAttackPrimedEnemyRuntimeId == target.RuntimeId)
            {
                return;
            }

            attacker.OpeningAttackPrimedEnemyRuntimeId = target.RuntimeId;
            attacker.AttackTimer = Mathf.Max(
                attacker.AttackTimer,
                interval * ResponsiveMeleeClass2OpeningAttackReadiness);
        }

        private static bool ShouldPrimeResponsiveMeleeOpeningAttack(MonsterDataSO monsterData)
        {
            return monsterData != null &&
                monsterData.classRank == 2 &&
                string.Equals(monsterData.raceId, "swordsman", System.StringComparison.Ordinal) &&
                IsResponsiveMeleeLineage(monsterData);
        }

        private static float ResolveEnemyAttackReach(EnemyDataSO enemyData)
        {
            float attackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData);
            return Mathf.Max(0f, BattleAttackRangeResolver.ToEnemyHoldOffset(attackRange) + RangeOffsetPadding);
        }

        private static Vector2 ResolveAllyHomeAnchor(int allyIndex) => ResolveAllyHomeAnchor(allyIndex, null);

        private static Vector2 ResolveAllyHomeAnchor(int allyIndex, MonsterDataSO monsterData)
        {
            Vector2 anchor = BattleFormationLayout.ResolveAllyHomeAnchor(allyIndex);
            if (!ShouldAdvanceRearMeleeHome(monsterData) || allyIndex < 2)
            {
                return anchor;
            }

            anchor.x = Mathf.Max(anchor.x, allyIndex == 2 ? ResponsiveMeleeMidlineHomeX : ResponsiveMeleeRearHomeX);
            return anchor;
        }

        private static bool ShouldAdvanceRearMeleeHome(MonsterDataSO monsterData)
        {
            return monsterData != null &&
                IsMonsterMelee(monsterData) &&
                BattleAttackRangeResolver.ResolveMonsterAttackRange(monsterData) < RangedAttackThreshold;
        }

        private static bool IsResponsiveMeleeLineage(MonsterDataSO monsterData)
        {
            if (monsterData == null || !IsMonsterMelee(monsterData))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(monsterData.raceId) &&
                (string.Equals(monsterData.raceId, "swordsman", System.StringComparison.Ordinal) ||
                string.Equals(monsterData.raceId, "golem", System.StringComparison.Ordinal)))
            {
                return true;
            }

            return !string.IsNullOrEmpty(monsterData.monsterId) &&
                ResponsiveMeleeLineageMonsterIds.Contains(monsterData.monsterId);
        }

        private static Vector2 ResolveAllyMeleeCombatAnchor(AllyRuntime ally, Vector2 target, float desiredSeparation)
        {
            float homeY = ally != null ? ally.HomeAnchor.y : target.y;
            float desiredY = Mathf.Lerp(homeY, target.y, AllyMeleeVerticalFollowStrength);
            desiredY = Mathf.Clamp(desiredY, homeY - AllyMeleeVerticalLeash, homeY + AllyMeleeVerticalLeash);
            return new Vector2(
                target.x - Mathf.Max(0.01f, desiredSeparation),
                desiredY);
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
                return Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x) <=
                    attackDistance + ResolveAllyAttackForgivenessX(attacker);
            }

            if (Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance)
            {
                return true;
            }

            if (!IsEnemyInsideAllySearchRange(attacker, target))
            {
                return false;
            }

            // Movement is clamped by formation leashes. If the unit has reached its
            // closest allowed point, let a visually-contacting monster swing instead
            // of freezing in idle just outside the exact radius.
            float horizontalGap = Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x);
            float verticalGap = Mathf.Abs(attacker.PositionAnchor.y - target.PositionAnchor.y);
            return horizontalGap <= attackDistance + ResolveAllyAttackForgivenessX(attacker) &&
                verticalGap <= attackDistance + ResolveAllyAttackForgivenessY(attacker);
        }

        private static void LockAttackMotion(AllyRuntime attacker)
        {
            if (attacker != null)
            {
                attacker.AttackMotionLockRemaining = Mathf.Max(attacker.AttackMotionLockRemaining, AttackMotionLockDuration);
                attacker.IsMoving = false;
            }
        }

        private static void LockAttackMotion(EnemyRuntime attacker)
        {
            if (attacker != null)
            {
                attacker.AttackMotionLockRemaining = Mathf.Max(attacker.AttackMotionLockRemaining, AttackMotionLockDuration);
                attacker.IsMoving = false;
            }
        }

        private static bool IsAllyInCloseCombatVisualRange(AllyRuntime attacker, EnemyRuntime target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            if (!IsEnemyInsideAllySearchRange(attacker, target))
            {
                return false;
            }

            float attackDistance = attacker.CombatRadius + target.CombatRadius + attacker.AttackReachAnchor + PositionEpsilon;
            if (Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance)
            {
                return true;
            }

            float horizontalGap = Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x);
            float verticalGap = Mathf.Abs(attacker.PositionAnchor.y - target.PositionAnchor.y);
            return horizontalGap <= attackDistance + ResolveAllyAttackForgivenessX(attacker) + CloseCombatVisualEngagementPaddingX &&
                verticalGap <= attackDistance + ResolveAllyAttackForgivenessY(attacker) + CloseCombatVisualEngagementPaddingY;
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
            if (Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance)
            {
                return true;
            }

            float horizontalGap = Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x);
            float verticalGap = Mathf.Abs(attacker.PositionAnchor.y - target.PositionAnchor.y);
            return horizontalGap <= attackDistance + CloseCombatVisualEngagementPaddingX &&
                verticalGap <= attackDistance + CloseCombatVisualEngagementPaddingY;
        }

        private static bool IsEnemyInCloseCombatVisualRange(EnemyRuntime attacker, AllyRuntime target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            float attackDistance = attacker.CombatRadius + target.CombatRadius + attacker.AttackReachAnchor + PositionEpsilon;
            if (Vector2.Distance(attacker.PositionAnchor, target.PositionAnchor) <= attackDistance)
            {
                return true;
            }

            float horizontalGap = Mathf.Abs(attacker.PositionAnchor.x - target.PositionAnchor.x);
            float verticalGap = Mathf.Abs(attacker.PositionAnchor.y - target.PositionAnchor.y);
            return horizontalGap <= attackDistance + CloseCombatVisualEngagementPaddingX &&
                verticalGap <= attackDistance + CloseCombatVisualEngagementPaddingY;
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

                int targetAllyIndex = ResolveCachedEnemyTargetAllyIndex(enemy, i);
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

        private AllyRuntime ResolveAllyRuntimeBySlotIndex(int slotIndex)
        {
            if (slotIndex < 0)
            {
                return null;
            }

            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally != null && ally.SlotIndex == slotIndex)
                {
                    return ally;
                }
            }

            return null;
        }

        public bool IsAllyAlive(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Stats != null && !ally.Stats.IsDead();
        }

        public bool HasAllyRuntime(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Stats != null;
        }

        public int GetAllyCurrentHp(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Stats != null
                ? Mathf.Max(0, ally.Stats.CurrentHp)
                : 0;
        }

        public int GetAllyMaxHp(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Stats != null
                ? Mathf.Max(0, ally.Stats.MaxHp)
                : 0;
        }

        public Vector2 GetAllyPositionAnchor(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? ally.PositionAnchor : ResolveAllyHomeAnchor(index);
        }

        public Vector2 GetAllyHomeAnchor(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? ally.HomeAnchor : ResolveAllyHomeAnchor(index);
        }

        public string GetAllyMonsterId(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Data != null ? ally.Data.monsterId : string.Empty;
        }

        public string GetAllyRangeTypeName(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.Data != null ? ally.Data.rangeType.ToString() : string.Empty;
        }

        public float GetAllyAttackRange(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? BattleAttackRangeResolver.ResolveMonsterAttackRange(ally.Data) : 0f;
        }

        public float GetAllyAttackTimer(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? Mathf.Max(0f, ally.AttackTimer) : 0f;
        }

        public float GetAllyAttackInterval(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? GetCurrentPlayerAttackInterval(ally.Stats) : playerAttackInterval;
        }

        public float GetAllyAttackMotionLockRemaining(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? Mathf.Max(0f, ally.AttackMotionLockRemaining) : 0f;
        }

        public float GetAllyCombatRadius(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? ally.CombatRadius : 0f;
        }

        public float GetAllyAttackReachAnchor(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? ally.AttackReachAnchor : 0f;
        }

        public float GetAllySearchReachAnchor(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null ? ally.SearchReachAnchor : 0f;
        }

        public float GetAllyAttackDistanceThreshold(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            if (ally == null)
            {
                return 0f;
            }

            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime target = activeEnemyRuntimes[targetEnemyIndex];
            return target != null
                ? ally.CombatRadius + target.CombatRadius + ally.AttackReachAnchor + PositionEpsilon
                : 0f;
        }

        public float GetAllyHorizontalGapToTarget(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (ally == null || targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime target = activeEnemyRuntimes[targetEnemyIndex];
            return target != null ? Mathf.Abs(ally.PositionAnchor.x - target.PositionAnchor.x) : 0f;
        }

        public float GetAllyVerticalGapToTarget(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (ally == null || targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime target = activeEnemyRuntimes[targetEnemyIndex];
            return target != null ? Mathf.Abs(ally.PositionAnchor.y - target.PositionAnchor.y) : 0f;
        }

        public bool IsAllyTargetInsideSearchRange(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (ally == null || targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            return IsEnemyInsideAllySearchRange(ally, activeEnemyRuntimes[targetEnemyIndex]);
        }

        public int GetAllyTargetEnemyIndex(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ResolveAllyTargetEnemyIndex(ally, index);
        }

        public bool IsAllyMoving(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            return ally != null && ally.IsMoving;
        }

        public bool IsAllyCloseCombatEngaged(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            if (ally == null || ally.Stats == null || ally.Stats.IsDead() || !IsMonsterMelee(ally.Data))
            {
                return false;
            }

            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            return IsAllyInCloseCombatVisualRange(ally, activeEnemyRuntimes[targetEnemyIndex]);
        }

        public bool IsAllyAttackEngaged(int index)
        {
            AllyRuntime ally = ResolveAllyRuntimeBySlotIndex(index);
            if (ally == null || ally.Stats == null || ally.Stats.IsDead())
            {
                return false;
            }

            int targetEnemyIndex = ResolveAllyTargetEnemyIndex(ally, index);
            if (targetEnemyIndex < 0 || targetEnemyIndex >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            return CanAllyAttackTarget(ally, targetEnemyIndex);
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

        public Vector2 GetEnemyHomeAnchor(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return new Vector2(EnemySpawnX, ResolveEnemyLaneY(index));
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? enemy.HomeAnchor : new Vector2(EnemySpawnX, ResolveEnemyLaneY(index));
        }

        public string GetEnemyId(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return string.Empty;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null && enemy.Data != null ? enemy.Data.enemyId : string.Empty;
        }

        public float GetEnemyAttackTimer(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? Mathf.Max(0f, enemy.AttackTimer) : 0f;
        }

        public float GetEnemyAttackInterval(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return enemyAttackInterval;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? GetCurrentEnemyAttackInterval(enemy.Stats) : enemyAttackInterval;
        }

        public float GetEnemyAttackMotionLockRemaining(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? Mathf.Max(0f, enemy.AttackMotionLockRemaining) : 0f;
        }

        public float GetEnemyAttackReachAnchor(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return 0f;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            return enemy != null ? enemy.AttackReachAnchor : 0f;
        }

        public int GetEnemyTargetAllyIndex(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return -1;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            int activeTargetIndex = ResolveCachedEnemyTargetAllyIndex(enemy, index);
            return activeTargetIndex >= 0 && activeTargetIndex < activeAllyRuntimes.Count && activeAllyRuntimes[activeTargetIndex] != null
                ? activeAllyRuntimes[activeTargetIndex].SlotIndex
                : -1;
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

        public bool IsEnemyCloseCombatEngaged(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead() || !IsEnemyMelee(enemy.Data))
            {
                return false;
            }

            int targetAllyIndex = ResolveCachedEnemyTargetAllyIndex(enemy, index);
            if (targetAllyIndex < 0 || targetAllyIndex >= activeAllyRuntimes.Count)
            {
                return false;
            }

            return IsEnemyInCloseCombatVisualRange(enemy, activeAllyRuntimes[targetAllyIndex]);
        }

        public bool IsEnemyAttackEngaged(int index)
        {
            if (index < 0 || index >= activeEnemyRuntimes.Count)
            {
                return false;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[index];
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead())
            {
                return false;
            }

            int targetAllyIndex = ResolveCachedEnemyTargetAllyIndex(enemy, index);
            if (targetAllyIndex < 0 || targetAllyIndex >= activeAllyRuntimes.Count)
            {
                return false;
            }

            return CanEnemyAttackTarget(enemy, index, targetAllyIndex);
        }

        private int CountAliveAllies()
        {
            int aliveCount = 0;
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally != null && ally.Stats != null && !ally.Stats.IsDead())
                {
                    aliveCount += 1;
                }
            }

            return aliveCount;
        }

        private static bool ResolveBossEncounter(int floor)
        {
            return BattleDungeonCatalog.ResolveIsBossEncounter(floor);
        }

        private bool HasFinalBossEnemy()
        {
            return !isBossEncounter && finalBossMonsterIds != null && finalBossMonsterIds.Length > 0;
        }

        private int ResolveFinalBossEnemyCount()
        {
            return HasFinalBossEnemy()
                ? Mathf.Clamp(finalBossMonsterIds.Length, 1, CurrentEnemyCountTarget)
                : 0;
        }

        private bool IsFinalBossSpawnIndex(int spawnIndex)
        {
            int finalBossEnemyCount = ResolveFinalBossEnemyCount();
            return finalBossEnemyCount > 0 && spawnIndex >= CurrentEnemyCountTarget - finalBossEnemyCount;
        }

        private string ResolveFinalBossMonsterIdForSpawnIndex(int spawnIndex)
        {
            if (!IsFinalBossSpawnIndex(spawnIndex) || finalBossMonsterIds == null || finalBossMonsterIds.Length == 0)
            {
                return string.Empty;
            }

            int firstBossSpawnIndex = Mathf.Max(0, CurrentEnemyCountTarget - ResolveFinalBossEnemyCount());
            int bossIndex = Mathf.Clamp(spawnIndex - firstBossSpawnIndex, 0, finalBossMonsterIds.Length - 1);
            return finalBossMonsterIds[bossIndex] ?? string.Empty;
        }

        private bool ShouldDelayFinalBossSpawn()
        {
            int finalBossEnemyCount = ResolveFinalBossEnemyCount();
            int firstBossSpawnIndex = CurrentEnemyCountTarget - finalBossEnemyCount;
            if (finalBossEnemyCount <= 0 || spawnedEnemiesInCurrentWave != firstBossSpawnIndex)
            {
                return false;
            }

            int requiredDefeatedMinions = Mathf.Max(0, firstBossSpawnIndex);
            return defeatedEnemiesInCurrentWave < requiredDefeatedMinions || activeEnemyRuntimes.Count > 0;
        }

        private int ResolveEncounterEnemyCount()
        {
            if (isBossEncounter)
            {
                return Mathf.Max(1, bossWaveEnemyCount);
            }

            int dungeonEnemyCount = BattleDungeonCatalog.ResolveEnemyCount(currentFloor);
            int configuredEnemyCount = dungeonEnemyCount > 0
                ? dungeonEnemyCount
                : normalWaveEnemyCount > 0
                    ? normalWaveEnemyCount
                    : ReferenceEncounterEnemyCount;
            return Mathf.Clamp(configuredEnemyCount, MinEncounterEnemyCount, MaxEncounterEnemyCount);
        }

        private void BuildAnticipatedEnemyMaxHpPlan()
        {
            anticipatedEnemyMaxHpBySpawnIndex.Clear();
            anticipatedUnspawnedEnemyMaxHpInCurrentWave = 0;

            int targetCount = Mathf.Max(0, CurrentEnemyCountTarget);
            for (int spawnIndex = 0; spawnIndex < targetCount; spawnIndex += 1)
            {
                int estimatedMaxHp = EstimateEnemyMaxHpForSpawnIndex(spawnIndex);
                anticipatedEnemyMaxHpBySpawnIndex.Add(estimatedMaxHp);
                anticipatedUnspawnedEnemyMaxHpInCurrentWave += estimatedMaxHp;
            }
        }

        private void ConsumeAnticipatedEnemyMaxHp(int spawnIndex)
        {
            int estimatedMaxHp = spawnIndex >= 0 && spawnIndex < anticipatedEnemyMaxHpBySpawnIndex.Count
                ? anticipatedEnemyMaxHpBySpawnIndex[spawnIndex]
                : EstimateEnemyMaxHpForSpawnIndex(spawnIndex);
            anticipatedUnspawnedEnemyMaxHpInCurrentWave = Mathf.Max(
                0,
                anticipatedUnspawnedEnemyMaxHpInCurrentWave - Mathf.Max(0, estimatedMaxHp));
        }

        private int EstimateEnemyMaxHpForSpawnIndex(int spawnIndex)
        {
            var masterDataManager = MasterDataManager.Instance;
            bool applyBossModifiers = isBossEncounter || IsFinalBossSpawnIndex(spawnIndex);
            string finalBossMonsterId = ResolveFinalBossMonsterIdForSpawnIndex(spawnIndex);
            if (!string.IsNullOrEmpty(finalBossMonsterId))
            {
                return EstimateEnemyMaxHpForMonster(currentFloor, masterDataManager, finalBossMonsterId, true);
            }

            BattleDungeonFloorDefinition floor = BattleDungeonCatalog.GetFloorForGlobalFloor(currentFloor);
            int highestMaxHp = 0;
            if (floor?.EnemyMonsterIds != null)
            {
                for (int i = 0; i < floor.EnemyMonsterIds.Count; i += 1)
                {
                    string monsterId = floor.EnemyMonsterIds[i];
                    if (string.IsNullOrEmpty(monsterId))
                    {
                        continue;
                    }

                    highestMaxHp = Mathf.Max(
                        highestMaxHp,
                        EstimateEnemyMaxHpForMonster(currentFloor, masterDataManager, monsterId, applyBossModifiers));
                }
            }

            return highestMaxHp > 0
                ? highestMaxHp
                : EstimateEnemyMaxHpFromData(null, applyBossModifiers);
        }

        private int EstimateEnemyMaxHpForMonster(
            int floor,
            MasterDataManager masterDataManager,
            string monsterId,
            bool applyBossModifiers)
        {
            EnemyDataSO enemyData = BattleDungeonCatalog.CreateEnemyDataForMonsterAtGlobalFloor(
                floor,
                masterDataManager,
                monsterId);
            return EstimateEnemyMaxHpFromData(enemyData, applyBossModifiers);
        }

        private int EstimateEnemyMaxHpFromData(EnemyDataSO enemyData, bool applyBossModifiers)
        {
            int maxHp = enemyData != null ? enemyData.maxHp : 40;
            if (applyBossModifiers)
            {
                maxHp = Mathf.Max(maxHp + 1, Mathf.RoundToInt(maxHp * bossHpMultiplier));
            }

            return Mathf.Max(1, maxHp);
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
            bool isBossEnemy = isBossEncounter || IsFinalBossSpawnIndex(spawnIndex);
            string forcedMonsterId = isBossEnemy && !isBossEncounter
                ? ResolveFinalBossMonsterIdForSpawnIndex(spawnIndex)
                : string.Empty;
            BattleUnitStats spawnedStats = CreateEnemyStats(currentFloor, isBossEnemy, forcedMonsterId, out EnemyTraitRuntime spawnedTrait, out EnemyDataSO spawnedData);
            ConsumeAnticipatedEnemyMaxHp(spawnIndex);
            Vector2 homeAnchor = new Vector2(EnemySpawnX, ResolveEnemySpawnLaneY(spawnIndex));
            var runtime = new EnemyRuntime
            {
                RuntimeId = nextEnemyRuntimeId++,
                Stats = spawnedStats,
                Data = spawnedData,
                Trait = spawnedTrait,
                IsBoss = isBossEnemy,
                AttackTimer = 0f,
                HomeAnchor = homeAnchor,
                PositionAnchor = homeAnchor,
                TargetAllyRuntimeId = ResolveNearestAliveAllyRuntimeId(homeAnchor),
                CombatRadius = ResolveEnemyCombatRadius(spawnedData),
                AttackReachAnchor = ResolveEnemyAttackReach(spawnedData),
                MoveSpeed = EnemyMoveSpeed * MonsterMoveSpeedMultiplier
            };

            activeEnemyRuntimes.Add(runtime);
            ClearEnemyMovementQueueCache();
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

            if (ShouldDelayFinalBossSpawn())
            {
                enemySpawnTimer = 0f;
                return;
            }

            enemySpawnTimer += deltaTime;
            float interval = ResolveEnemySpawnInterval();
            while (spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget && enemySpawnTimer >= interval)
            {
                if (ShouldDelayFinalBossSpawn())
                {
                    break;
                }

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
                if (ShouldDelayFinalBossSpawn())
                {
                    break;
                }

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

            EnemyRuntime defeatedEnemy = activeEnemyRuntimes[removalIndex];
            EnemyDataSO defeatedEnemyData = defeatedEnemy?.Data;
            bool defeatedEnemyIsDungeonBoss = defeatedEnemy != null && defeatedEnemy.IsBoss;
            defeatedEnemyMaxHpInCurrentWave += Mathf.Max(0, defeatedEnemy?.Stats?.MaxHp ?? 0);
            activeEnemyRuntimes.RemoveAt(removalIndex);
            ClearEnemyMovementQueueCache();

            defeatedEnemiesInCurrentWave += 1;
            activeEnemiesInCurrentWave = activeEnemyRuntimes.Count;
            RetargetAlliesToNearestSearchableEnemies();
            SetEngagedEnemyCount(Mathf.Min(engagedEnemyCount, activeEnemiesInCurrentWave));
            EnemyDefeated?.Invoke(CurrentRemainingEnemyCount, removalIndex, defeatedEnemyData, defeatedEnemyIsDungeonBoss);

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

                int targetIndex = ResolveCachedEnemyTargetAllyIndex(runtime, i);
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

                int targetIndex = ResolveCachedEnemyTargetAllyIndex(attacker, i);
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
            AllyDefeated?.Invoke(ally.SlotIndex);
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

        private BattleUnitStats CreateEnemyStats(int floor, bool applyBossModifiers, string forcedMonsterId, out EnemyTraitRuntime runtime, out EnemyDataSO enemyData)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            enemyData = !string.IsNullOrEmpty(forcedMonsterId)
                ? BattleDungeonCatalog.CreateEnemyDataForMonsterAtGlobalFloor(floor, masterDataManager, forcedMonsterId)
                : BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(floor, masterDataManager, true);
            if (enemyData == null)
            {
                enemyData = floorData != null ? floorData.enemyData : null;
            }

            if (enemyData == null)
            {
                runtime = EnemyTraitResolver.Resolve(EnemyTrait.None);
                int fallbackHp = 40;
                int fallbackAttack = 8;
                int fallbackDefense = 2;
                if (applyBossModifiers)
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

            if (applyBossModifiers)
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

        private int ResolveCurrentGuardDefenseBonus()
        {
            return guardRemainingTime > 0f
                ? guardDefenseBonus + Mathf.Max(0, battleSpiritModifier.GuardDefenseBonus)
                : 0;
        }

        public int GetCurrentPlayerDefense()
        {
            var guardBonus = ResolveCurrentGuardDefenseBonus();
            AllyRuntime leadAlly = ResolveLeadAliveAllyRuntime();
            return leadAlly != null && leadAlly.Stats != null ? leadAlly.Stats.Defense + guardBonus : 0;
        }

        private int GetCurrentPlayerDefense(AllyRuntime ally)
        {
            if (ally == null || ally.Stats == null)
            {
                return 0;
            }

            var guardBonus = ResolveCurrentGuardDefenseBonus();
            return ally.Stats.Defense + guardBonus;
        }

        private BattleUnitStats BuildCurrentPlayerDefenseSnapshot(AllyRuntime ally)
        {
            if (ally == null || ally.Stats == null)
            {
                return null;
            }

            int guardBonus = ResolveCurrentGuardDefenseBonus();
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

        private static bool IsEnemyMelee(EnemyDataSO enemyData)
        {
            return BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData) < RangedAttackThreshold;
        }

        private static float ResolvePlayerPresentationDelay(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return 0f;
            }

            float attackRange = BattleAttackRangeResolver.ResolveMonsterAttackRange(monsterData);
            return ResolveAttackImpactPresentationDelay(
                monsterData.monsterId,
                attackRange,
                monsterData.damageType);
        }

        private static float ResolveEnemyPresentationDelay(EnemyDataSO enemyData)
        {
            if (enemyData == null)
            {
                return 0f;
            }

            string monsterId = !string.IsNullOrEmpty(enemyData.enemyId)
                ? BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId)
                : string.Empty;
            float attackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData);
            return ResolveAttackImpactPresentationDelay(
                monsterId,
                attackRange,
                enemyData.damageType);
        }

        private static float ResolveAttackImpactPresentationDelay(string monsterId, float attackRange, MonsterDamageType damageType)
        {
            if (!string.IsNullOrEmpty(monsterId) &&
                ProjectileImpactPresentationDelays.TryGetValue(monsterId, out float projectileImpactDelay))
            {
                return projectileImpactDelay;
            }

            if (attackRange < RangedAttackThreshold)
            {
                return MeleePresentationDelay;
            }

            return damageType == MonsterDamageType.Magic
                ? TargetImpactPresentationDelay
                : DefaultRangedImpactPresentationDelay;
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
                currentEnemyIsBoss = false;
                enemyAttackTimer = 0f;
                return;
            }

            EnemyRuntime leadEnemy = activeEnemyRuntimes[0];
            enemyStats = leadEnemy.Stats;
            currentEnemyData = leadEnemy.Data;
            enemyTraitRuntime = leadEnemy.Trait;
            currentEnemyIsBoss = leadEnemy.IsBoss;
            enemyAttackTimer = leadEnemy.AttackTimer;
        }

        private int CalculateCurrentWaveEnemyCurrentHp()
        {
            int total = Mathf.Max(0, anticipatedUnspawnedEnemyMaxHpInCurrentWave);
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy?.Stats == null)
                {
                    continue;
                }

                total += Mathf.Max(0, enemy.Stats.CurrentHp);
            }

            return total;
        }

        private int CalculateCurrentWaveEnemyMaxHp()
        {
            int total = Mathf.Max(0, defeatedEnemyMaxHpInCurrentWave) +
                Mathf.Max(0, anticipatedUnspawnedEnemyMaxHpInCurrentWave);
            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy?.Stats == null)
                {
                    continue;
                }

                total += Mathf.Max(0, enemy.Stats.MaxHp);
            }

            return total;
        }

        private void RaiseHitResolved(BattleHitInfo hitInfo)
        {
            HitResolved?.Invoke(hitInfo);
        }
    }
}
