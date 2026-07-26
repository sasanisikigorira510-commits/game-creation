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

        private BattleUnitStats playerStats;
        private BattleUnitStats enemyStats;
        private BattleSkillSet skillSet;
        private EnemyTraitRuntime enemyTraitRuntime;
        private float playerAttackTimer;
        private float enemyAttackTimer;
        private float guardRemainingTime;
        private bool isRunning;

        public event System.Action<BattleHitInfo> HitResolved;

        public BattleUnitStats PlayerStats => playerStats;
        public BattleUnitStats EnemyStats => enemyStats;
        public bool IsRunning => isRunning;

        public void Setup(int floor)
        {
            playerStats = CreatePlayerStats();
            enemyStats = CreateEnemyStats(floor, out enemyTraitRuntime);
            skillSet = new BattleSkillSet();
            playerAttackTimer = 0f;
            enemyAttackTimer = 0f;
            guardRemainingTime = 0f;
            isRunning = playerStats != null && enemyStats != null;
        }

        public BattleResult Tick(float deltaTime)
        {
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
                isRunning = false;
                return BattleResult.Win;
            }

            if (enemyAttackTimer >= enemyAttackInterval)
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
            var profile = GetProfile();
            var powerRate = 2.0f * GetStrikePowerMultiplier(profile);
            var damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * powerRate) - enemyStats.Defense);
            enemyStats.ApplyDamage(damage);
            RaiseHitResolved(new BattleHitInfo(false, damage, false, true));
        }

        private void UseSkillDrain()
        {
            var profile = GetProfile();
            var damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 1.2f) - enemyStats.Defense);
            enemyStats.ApplyDamage(damage);
            var healAmount = Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f * GetDrainHealMultiplier(profile)));
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
            var masterDataManager = MasterDataManager.Instance;
            var playerData = masterDataManager != null ? masterDataManager.GetPlayerBaseData() : null;
            var profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;

            if (playerData == null)
            {
                return CreateFallbackPlayerStats(profile);
            }

            var equipmentBonus = GetEquipmentBonus(profile);
            var maxHpBeforeMultiplier = playerData.initialHp + GetLevelHpBonus(profile) + GetHpBonus(profile) + equipmentBonus.Hp;
            var attackBeforeMultiplier = playerData.initialAttack + GetLevelAttackBonus(profile) + GetAttackBonus(profile) + equipmentBonus.Attack;
            var defenseBeforeMultiplier = playerData.initialDefense + GetLevelDefenseBonus(profile) + GetDefenseBonus(profile) + equipmentBonus.Defense;
            var maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHpBeforeMultiplier * GetHpMultiplier(profile)));
            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = Mathf.Max(1, Mathf.RoundToInt(attackBeforeMultiplier * GetAttackMultiplier(profile))),
                Defense = Mathf.Max(0, Mathf.RoundToInt(defenseBeforeMultiplier * GetDefenseMultiplier(profile))),
                AttackSpeed = (playerData.initialAttackSpeed + equipmentBonus.AttackSpeed) * GetAttackSpeedMultiplier(profile),
                CritRate = playerData.initialCritRate + equipmentBonus.CritRate + GetCritRateBonus(profile),
                CritDamage = playerData.initialCritDamage
            };
        }

        private static BattleUnitStats CreateEnemyStats(int floor, out EnemyTraitRuntime runtime)
        {
            var masterDataManager = MasterDataManager.Instance;
            var floorData = masterDataManager != null ? masterDataManager.GetFloorData(floor) : null;
            EnemyDataSO enemyData = floorData != null ? floorData.enemyData : null;

            if (enemyData == null)
            {
                runtime = EnemyTraitResolver.Resolve(EnemyTrait.None);
                return new BattleUnitStats
                {
                    MaxHp = 40,
                    CurrentHp = 40,
                    Attack = 8,
                    Defense = 2,
                    AttackSpeed = 0.8f,
                    CritRate = 0.03f,
                    CritDamage = 1.3f
                };
            }

            runtime = EnemyTraitResolver.Resolve(enemyData.enemyTrait);
            return new BattleUnitStats
            {
                MaxHp = enemyData.maxHp,
                CurrentHp = enemyData.maxHp,
                Attack = Mathf.RoundToInt(enemyData.attack * runtime.AttackMultiplier),
                Defense = enemyData.defense + runtime.DefenseBonus,
                AttackSpeed = enemyData.attackSpeed * runtime.AttackSpeedMultiplier,
                CritRate = enemyData.critRate + runtime.CritRateBonus,
                CritDamage = enemyData.critDamage
            };
        }

        private static BattleUnitStats CreateFallbackPlayerStats(PlayerProfile profile)
        {
            var equipmentBonus = GetEquipmentBonus(profile);
            var maxHpBeforeMultiplier = 100 + GetLevelHpBonus(profile) + GetHpBonus(profile) + equipmentBonus.Hp;
            var attackBeforeMultiplier = 15 + GetLevelAttackBonus(profile) + GetAttackBonus(profile) + equipmentBonus.Attack;
            var defenseBeforeMultiplier = 5 + GetLevelDefenseBonus(profile) + GetDefenseBonus(profile) + equipmentBonus.Defense;
            var maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHpBeforeMultiplier * GetHpMultiplier(profile)));
            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = Mathf.Max(1, Mathf.RoundToInt(attackBeforeMultiplier * GetAttackMultiplier(profile))),
                Defense = Mathf.Max(0, Mathf.RoundToInt(defenseBeforeMultiplier * GetDefenseMultiplier(profile))),
                AttackSpeed = (1.0f + equipmentBonus.AttackSpeed) * GetAttackSpeedMultiplier(profile),
                CritRate = 0.05f + equipmentBonus.CritRate + GetCritRateBonus(profile),
                CritDamage = 1.5f
            };
        }

        private static PlayerProfile GetProfile()
        {
            return GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
        }

        private static int GetLevelAttackBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelAttackBonus() : 0;
        }

        private static int GetLevelDefenseBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelDefenseBonus() : 0;
        }

        private static int GetLevelHpBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelHpBonus() : 0;
        }

        private static int GetAttackBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetAttackBonus() : 0;
        }

        private static int GetDefenseBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetDefenseBonus() : 0;
        }

        private static int GetHpBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetHpBonus() : 0;
        }

        private static float GetAttackMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetAttackMultiplier() : 1f;
        }

        private static float GetDefenseMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetDefenseMultiplier() : 1f;
        }

        private static float GetHpMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetHpMultiplier() : 1f;
        }

        private static float GetCritRateBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetCritRateBonus() : 0f;
        }

        private static float GetAttackSpeedMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetAttackSpeedMultiplier() : 1f;
        }

        private static float GetStrikePowerMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetStrikePowerMultiplier() : 1f;
        }

        private static float GetDrainHealMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetDrainHealMultiplier() : 1f;
        }

        private static EquipmentBonus GetEquipmentBonus(PlayerProfile profile)
        {
            var result = new EquipmentBonus();
            if (profile == null || MasterDataManager.Instance == null)
            {
                return result;
            }

            AddEquipmentBonus(profile.EquippedWeaponId, ref result);
            AddEquipmentBonus(profile.EquippedArmorId, ref result);
            AddEquipmentBonus(profile.EquippedAccessoryId, ref result);
            return result;
        }

        private static void AddEquipmentBonus(string equipmentId, ref EquipmentBonus bonus)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                return;
            }

            var equipmentData = MasterDataManager.Instance.GetEquipmentData(equipmentId);
            if (equipmentData == null)
            {
                return;
            }

            bonus.Attack += equipmentData.baseAttack;
            bonus.Defense += equipmentData.baseDefense;
            bonus.Hp += equipmentData.baseHp;
            bonus.CritRate += equipmentData.bonusCritRate;
            bonus.AttackSpeed += equipmentData.bonusAttackSpeed;
        }

        private struct EquipmentBonus
        {
            public int Attack;
            public int Defense;
            public int Hp;
            public float CritRate;
            public float AttackSpeed;
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
