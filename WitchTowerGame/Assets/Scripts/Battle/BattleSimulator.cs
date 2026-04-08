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
        private sealed class AllyRuntime
        {
            public BattleUnitStats Stats;
            public MonsterDataSO Data;
            public OwnedMonsterData OwnedMonster;
            public float AttackTimer;
            public float AttackLockRemaining;
        }

        private sealed class EnemyRuntime
        {
            public BattleUnitStats Stats;
            public EnemyDataSO Data;
            public EnemyTraitRuntime Trait;
            public float AttackTimer;
            public float AttackLockRemaining;
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
        private int encounterSerial;
        private float enemySpawnTimer;
        private int engagedEnemyCount;
        private readonly List<AllyRuntime> activeAllyRuntimes = new List<AllyRuntime>();
        private readonly List<EnemyRuntime> activeEnemyRuntimes = new List<EnemyRuntime>();

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
        public int TotalWaveCount => Mathf.Max(1, totalWaveCount);
        public bool IsBossWave => currentWave >= TotalWaveCount;
        public int EncounterSerial => encounterSerial;
        public EnemyDataSO CurrentEnemyData => currentEnemyData;
        public int CurrentEnemyCountTarget => IsBossWave ? Mathf.Max(1, bossWaveEnemyCount) : Mathf.Max(1, normalWaveEnemyCount);
        public int CurrentEnemyIndexInWave => Mathf.Clamp(defeatedEnemiesInCurrentWave + 1, 1, CurrentEnemyCountTarget);
        public int CurrentRemainingEnemyCount => Mathf.Max(0, CurrentEnemyCountTarget - defeatedEnemiesInCurrentWave);
        public int CurrentSpawnedEnemyCount => Mathf.Max(0, spawnedEnemiesInCurrentWave);
        public int CurrentActiveEnemyCount => Mathf.Max(0, activeEnemiesInCurrentWave);
        public int CurrentEngagedEnemyCount => Mathf.Max(0, engagedEnemyCount);
        public int CurrentAliveAllyCount => CountAliveAllies();
        public int CurrentAllyRuntimeCount => activeAllyRuntimes.Count;

        public void Setup(int floor)
        {
            currentFloor = Mathf.Max(1, floor);
            currentWave = 1;
            defeatedEnemiesInCurrentWave = 0;
            spawnedEnemiesInCurrentWave = 0;
            activeEnemiesInCurrentWave = 0;
            encounterSerial = 0;
            enemySpawnTimer = 0f;
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
            enemySpawnTimer = Mathf.Max(0f, normalEnemySpawnInterval + 0.001f);
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

            TickAttackLocks(deltaTime);

            skillSet.Tick(deltaTime);
            TickGuard(deltaTime);
            TickEnemySpawns(deltaTime);
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
        }

        public void SetEngagedEnemyCount(int count)
        {
            int clamped = Mathf.Clamp(count, 0, activeEnemiesInCurrentWave);
            engagedEnemyCount = clamped;
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

            int targetIndex = ResolvePlayerAttackTargetIndex();
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

            RaiseHitResolved(new BattleHitInfo(false, damage, false, true, false, targetIndex, attackerIndex));
        }

        private void UseSkillDrain()
        {
            AllyRuntime attacker = ResolveLeadAliveAllyRuntime();
            if (attacker == null || attacker.Stats == null)
            {
                return;
            }

            int targetIndex = ResolvePlayerAttackTargetIndex();
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
            RaiseHitResolved(new BattleHitInfo(false, damage, false, true, false, targetIndex, attackerIndex));
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

            int targetIndex = ResolveEnemyAttackTargetIndex();
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
            bool causesKnockback = ResolveEnemyNormalAttackAppliesKnockback(attacker.Data);

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

            if (causesKnockback)
            {
                ApplyPlayerAttackKnockbackLock(targetIndex, ResolveEnemyKnockbackDuration(attacker.Data));
            }

            ApplyEnemyLifeSteal(attacker, totalDamage);
            SyncPlayerAggregateState();
            NotifyAllyDefeatedIfNeeded(targetIndex, wasAlive);
            RaiseHitResolved(new BattleHitInfo(true, totalDamage, result.IsCritical, false, causesKnockback, targetIndex, attackerIndex));
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
            List<int> targetIndices = CollectEnemyTargetIndices(targetCount);
            if (targetIndices.Count <= 0)
            {
                return;
            }

            int totalDamage = 0;
            bool anyCritical = false;
            bool causesKnockback = !isSkill && ResolvePlayerNormalAttackAppliesKnockback(attacker.Data);
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

            if (causesKnockback)
            {
                ApplyEnemyAttackKnockbackLock(primaryTargetIndex, ResolvePlayerKnockbackDuration(attacker.Data));
            }

            RaiseHitResolved(new BattleHitInfo(false, totalDamage, anyCritical, isSkill, causesKnockback, primaryTargetIndex, attackerIndex));
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

        private void TickAttackLocks(float deltaTime)
        {
            for (int i = 0; i < activeAllyRuntimes.Count; i += 1)
            {
                AllyRuntime ally = activeAllyRuntimes[i];
                if (ally == null)
                {
                    continue;
                }

                ally.AttackLockRemaining = Mathf.Max(0f, ally.AttackLockRemaining - deltaTime);
            }

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime enemy = activeEnemyRuntimes[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.AttackLockRemaining = Mathf.Max(0f, enemy.AttackLockRemaining - deltaTime);
            }
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

                if (attacker.AttackLockRemaining > 0f)
                {
                    attacker.AttackTimer = 0f;
                    continue;
                }

                float interval = GetCurrentPlayerAttackInterval(attacker.Stats);
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

        private void CreatePlayerPartyRuntimes()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            MasterDataManager.Instance?.Initialize();

            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsters(profile, 5);
            for (int i = 0; i < partyMonsters.Count; i += 1)
            {
                OwnedMonsterData ownedMonster = partyMonsters[i];
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.MonsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId);
                if (monsterData == null)
                {
                    continue;
                }

                activeAllyRuntimes.Add(new AllyRuntime
                {
                    Stats = MonsterBattleStatsFactory.Create(profile, ownedMonster, monsterData),
                    Data = monsterData,
                    OwnedMonster = ownedMonster,
                    AttackTimer = 0f,
                    AttackLockRemaining = 0f
                });
            }

            if (activeAllyRuntimes.Count > 0)
            {
                return;
            }

            BattleUnitStats fallbackStats = PlayerBattleStatsFactory.CreatePreview(profile);
            if (fallbackStats != null)
            {
                activeAllyRuntimes.Add(new AllyRuntime
                {
                    Stats = fallbackStats,
                    Data = null,
                    OwnedMonster = null,
                    AttackTimer = 0f,
                    AttackLockRemaining = 0f
                });
            }
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

        private int ResolveEnemyAttackTargetIndex()
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

        private void SpawnEnemyForCurrentEncounter()
        {
            BattleUnitStats spawnedStats = CreateEnemyStats(currentFloor, IsBossWave, out EnemyTraitRuntime spawnedTrait, out EnemyDataSO spawnedData);
            var runtime = new EnemyRuntime
            {
                Stats = spawnedStats,
                Data = spawnedData,
                Trait = spawnedTrait,
                AttackTimer = 0f
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
            if (IsBossWave || spawnedEnemiesInCurrentWave >= CurrentEnemyCountTarget)
            {
                return;
            }

            enemySpawnTimer += deltaTime;
            float interval = Mathf.Max(0.05f, normalEnemySpawnInterval);
            while (spawnedEnemiesInCurrentWave < CurrentEnemyCountTarget && enemySpawnTimer >= interval)
            {
                enemySpawnTimer -= interval;
                SpawnEnemyBurst(Mathf.Max(1, normalEnemySpawnBurstSize), false);
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
                if (currentWave < TotalWaveCount)
                {
                    currentWave += 1;
                    defeatedEnemiesInCurrentWave = 0;
                    spawnedEnemiesInCurrentWave = 0;
                    activeEnemiesInCurrentWave = 0;
                    enemyStats = null;
                    currentEnemyData = null;
                    enemyTraitRuntime = default;
                    enemyAttackTimer = 0f;
                    engagedEnemyCount = 0;
                    activeEnemyRuntimes.Clear();
                    encounterSerial += 1;
                    EncounterChanged?.Invoke();
                    if (IsBossWave)
                    {
                        SpawnEnemyBurst(1, true);
                        enemySpawnTimer = 0f;
                    }
                    else
                    {
                        enemySpawnTimer = Mathf.Max(0f, normalEnemySpawnInterval + 0.001f);
                    }
                    return true;
                }

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

        private int ResolvePlayerAttackTargetIndex()
        {
            if (activeEnemyRuntimes.Count <= 0)
            {
                return -1;
            }

            int engagedCount = Mathf.Min(Mathf.Max(1, engagedEnemyCount), activeEnemyRuntimes.Count);
            for (int i = 0; i < engagedCount; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime != null && runtime.Stats != null && !runtime.Stats.IsDead())
                {
                    return i;
                }
            }

            for (int i = 0; i < activeEnemyRuntimes.Count; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime != null && runtime.Stats != null && !runtime.Stats.IsDead())
                {
                    return i;
                }
            }

            return -1;
        }

        private List<int> CollectEnemyTargetIndices(int maxTargets)
        {
            var result = new List<int>();
            int desiredCount = Mathf.Max(1, maxTargets);
            int engagedCount = Mathf.Min(Mathf.Max(1, engagedEnemyCount), activeEnemyRuntimes.Count);

            for (int i = 0; i < engagedCount && result.Count < desiredCount; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime != null && runtime.Stats != null && !runtime.Stats.IsDead())
                {
                    result.Add(i);
                }
            }

            for (int i = engagedCount; i < activeEnemyRuntimes.Count && result.Count < desiredCount; i += 1)
            {
                EnemyRuntime runtime = activeEnemyRuntimes[i];
                if (runtime != null && runtime.Stats != null && !runtime.Stats.IsDead())
                {
                    result.Add(i);
                }
            }

            return result;
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
            if (activeEnemyRuntimes.Count == 0 || engagedEnemyCount <= 0 || CountAliveAllies() <= 0)
            {
                enemyAttackTimer = 0f;
                return;
            }

            int engagedCount = Mathf.Min(engagedEnemyCount, activeEnemyRuntimes.Count);
            for (int i = 0; i < engagedCount; i += 1)
            {
                EnemyRuntime attacker = activeEnemyRuntimes[i];
                if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead())
                {
                    continue;
                }

                if (attacker.AttackLockRemaining > 0f)
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
            ally.AttackLockRemaining = 0f;
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

        private bool ResolveEnemyNormalAttackAppliesKnockback(EnemyDataSO enemyData)
        {
            return enemyData != null && enemyData.normalAttackAppliesKnockback;
        }

        private bool ResolvePlayerNormalAttackAppliesKnockback(MonsterDataSO monsterData)
        {
            return monsterData != null && monsterData.normalAttackAppliesKnockback;
        }

        private float ResolveEnemyKnockbackDuration(EnemyDataSO enemyData)
        {
            if (enemyData != null)
            {
                return Mathf.Max(0f, enemyData.normalAttackKnockbackDuration);
            }

            return 0f;
        }

        private float ResolvePlayerKnockbackDuration(MonsterDataSO monsterData)
        {
            if (monsterData != null)
            {
                return Mathf.Max(0f, monsterData.normalAttackKnockbackDuration);
            }

            return 0f;
        }

        private void ApplyPlayerAttackKnockbackLock(int allyIndex, float duration)
        {
            if (allyIndex < 0 || allyIndex >= activeAllyRuntimes.Count)
            {
                return;
            }

            AllyRuntime ally = activeAllyRuntimes[allyIndex];
            if (ally == null)
            {
                return;
            }

            ally.AttackLockRemaining = Mathf.Max(ally.AttackLockRemaining, duration);
            ally.AttackTimer = 0f;
        }

        private void ApplyEnemyAttackKnockbackLock(int enemyIndex, float duration)
        {
            if (enemyIndex < 0 || enemyIndex >= activeEnemyRuntimes.Count)
            {
                return;
            }

            EnemyRuntime enemy = activeEnemyRuntimes[enemyIndex];
            if (enemy == null)
            {
                return;
            }

            enemy.AttackLockRemaining = Mathf.Max(enemy.AttackLockRemaining, duration);
            enemy.AttackTimer = 0f;
            enemyAttackTimer = activeEnemyRuntimes.Count > 0 ? activeEnemyRuntimes[0].AttackTimer : 0f;
        }

        private BattleUnitStats CreateEnemyStats(int floor, bool isBossEncounter, out EnemyTraitRuntime runtime, out EnemyDataSO enemyData)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            enemyData = floorData != null ? floorData.enemyData : null;

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
