using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public sealed class BattleSimulator : MonoBehaviour
    {
        [SerializeField] private float playerAttackInterval = 1.0f;
        [SerializeField] private float enemyAttackInterval = 1.2f;
        [SerializeField] private float guardDuration = 5.0f;
        [SerializeField] private int guardDefenseBonus = 5;
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
        private float playerAttackTimer;
        private float enemyAttackTimer;
        private float guardRemainingTime;
        private bool isRunning;
        private int tickCount;
        private float lastDeltaTime;
        private int currentFloor;
        private int currentWave;
        private int defeatedEnemiesInCurrentWave;
        private int encounterSerial;

        public event System.Action<BattleHitInfo> HitResolved;
        public event System.Action EncounterChanged;

        public BattleUnitStats PlayerStats => playerStats;
        public BattleUnitStats EnemyStats => enemyStats;
        public bool IsRunning => isRunning;
        public int DebugTickCount => tickCount;
        public float DebugLastDeltaTime => lastDeltaTime;
        public float DebugPlayerAttackTimer => playerAttackTimer;
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

        public void Setup(int floor)
        {
            currentFloor = Mathf.Max(1, floor);
            currentWave = 1;
            defeatedEnemiesInCurrentWave = 0;
            encounterSerial = 0;
            playerStats = CreatePlayerStats();
            skillSet = new BattleSkillSet();
            playerAttackTimer = 0f;
            enemyAttackTimer = 0f;
            guardRemainingTime = 0f;
            SpawnEnemyForCurrentEncounter();
            isRunning = playerStats != null && enemyStats != null;
        }

        public BattleResult Tick(float deltaTime)
        {
            tickCount += 1;
            lastDeltaTime = deltaTime;

            if (!isRunning)
            {
                return BattleResult.None;
            }

            playerAttackTimer += deltaTime;
            enemyAttackTimer += deltaTime;
            skillSet.Tick(deltaTime);
            TickGuard(deltaTime);

            if (playerAttackTimer >= GetCurrentPlayerAttackInterval())
            {
                playerAttackTimer -= GetCurrentPlayerAttackInterval();
                PerformAttackOnEnemy(false);
            }

            if (enemyStats.IsDead())
            {
                if (AdvanceEncounterAfterEnemyDefeat())
                {
                    return BattleResult.None;
                }

                isRunning = false;
                return BattleResult.Win;
            }

            if (enemyAttackTimer >= GetCurrentEnemyAttackInterval())
            {
                enemyAttackTimer -= GetCurrentEnemyAttackInterval();
                PerformAttackOnPlayer();
            }

            if (playerStats.IsDead())
            {
                isRunning = false;
                return BattleResult.Lose;
            }

            return BattleResult.None;
        }

        public bool TryUseSkill(BattleSkillType skillType)
        {
            if (!isRunning || enemyStats == null || playerStats == null || skillSet == null)
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
            var damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 2.0f) - enemyStats.Defense);
            enemyStats.ApplyDamage(damage);
            RaiseHitResolved(new BattleHitInfo(false, damage, false, true));
        }

        private void UseSkillDrain()
        {
            var damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 1.2f) - enemyStats.Defense);
            enemyStats.ApplyDamage(damage);
            var healAmount = Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f));
            playerStats.CurrentHp = Mathf.Min(playerStats.MaxHp, playerStats.CurrentHp + healAmount);
            RaiseHitResolved(new BattleHitInfo(false, damage, false, true));
        }

        private void UseSkillGuard()
        {
            guardRemainingTime = guardDuration;
        }

        private void PerformAttackOnPlayer()
        {
            var result = DamageCalculator.Calculate(enemyStats, GetCurrentPlayerDefense());
            playerStats.ApplyDamage(result.Damage);
            ApplyEnemyLifeSteal(result.Damage);
            RaiseHitResolved(new BattleHitInfo(true, result.Damage, result.IsCritical, false));
        }

        private void PerformAttackOnEnemy(bool isSkill)
        {
            var result = DamageCalculator.Calculate(playerStats, enemyStats);
            enemyStats.ApplyDamage(result.Damage);
            RaiseHitResolved(new BattleHitInfo(false, result.Damage, result.IsCritical, isSkill));
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

        private static BattleUnitStats CreatePlayerStats()
        {
            var profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            return PlayerBattleStatsFactory.CreatePreview(profile);
        }

        private void SpawnEnemyForCurrentEncounter()
        {
            enemyStats = CreateEnemyStats(currentFloor, IsBossWave, out enemyTraitRuntime, out currentEnemyData);
            enemyAttackTimer = 0f;
            encounterSerial += 1;
            EncounterChanged?.Invoke();
        }

        private bool AdvanceEncounterAfterEnemyDefeat()
        {
            defeatedEnemiesInCurrentWave += 1;
            if (defeatedEnemiesInCurrentWave < CurrentEnemyCountTarget)
            {
                SpawnEnemyForCurrentEncounter();
                return true;
            }

            if (currentWave < TotalWaveCount)
            {
                currentWave += 1;
                defeatedEnemiesInCurrentWave = 0;
                SpawnEnemyForCurrentEncounter();
                return true;
            }

            return false;
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
                    Defense = fallbackDefense,
                    AttackSpeed = 0.8f,
                    CritRate = 0.03f,
                    CritDamage = 1.3f
                };
            }

            runtime = EnemyTraitResolver.Resolve(enemyData.enemyTrait);
            int maxHp = enemyData.maxHp;
            int attack = Mathf.RoundToInt(enemyData.attack * runtime.AttackMultiplier);
            int defense = enemyData.defense + runtime.DefenseBonus;
            float attackSpeed = enemyData.attackSpeed * runtime.AttackSpeedMultiplier;
            float critRate = enemyData.critRate + runtime.CritRateBonus;

            if (isBossEncounter)
            {
                maxHp = Mathf.Max(maxHp + 1, Mathf.RoundToInt(maxHp * bossHpMultiplier));
                attack = Mathf.Max(attack + 1, Mathf.RoundToInt(attack * bossAttackMultiplier));
                defense += bossDefenseBonus;
                attackSpeed *= 1.1f;
                critRate += 0.05f;
            }

            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = attack,
                Defense = defense,
                AttackSpeed = attackSpeed,
                CritRate = critRate,
                CritDamage = enemyData.critDamage
            };
        }

        public int GetCurrentPlayerDefense()
        {
            var guardBonus = guardRemainingTime > 0f ? guardDefenseBonus : 0;
            return playerStats != null ? playerStats.Defense + guardBonus : 0;
        }

        private float GetCurrentEnemyAttackInterval()
        {
            if (enemyStats == null || enemyStats.AttackSpeed <= 0f)
            {
                return enemyAttackInterval;
            }

            return enemyAttackInterval / enemyStats.AttackSpeed;
        }

        private float GetCurrentPlayerAttackInterval()
        {
            if (playerStats == null || playerStats.AttackSpeed <= 0f)
            {
                return playerAttackInterval;
            }

            return playerAttackInterval / playerStats.AttackSpeed;
        }

        private void ApplyEnemyLifeSteal(int dealtDamage)
        {
            if (enemyStats == null || enemyTraitRuntime.LifeStealRate <= 0f || dealtDamage <= 0)
            {
                return;
            }

            var healAmount = Mathf.Max(1, Mathf.RoundToInt(dealtDamage * enemyTraitRuntime.LifeStealRate));
            enemyStats.CurrentHp = Mathf.Min(enemyStats.MaxHp, enemyStats.CurrentHp + healAmount);
        }

        private void RaiseHitResolved(BattleHitInfo hitInfo)
        {
            HitResolved?.Invoke(hitInfo);
        }
    }
}
