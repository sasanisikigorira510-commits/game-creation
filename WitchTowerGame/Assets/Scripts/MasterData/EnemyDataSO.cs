using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "WitchTower/MasterData/Enemy Data")]
    public sealed class EnemyDataSO : ScriptableObject
    {
        public string enemyId;
        public string enemyName;
        public int maxHp;
        public int attack;
        public int magicAttack;
        public int defense;
        public int magicDefense;
        public MonsterDamageType damageType = MonsterDamageType.Physical;
        [Min(0.1f)] public float attackRange = 0f;
        [Min(1)] public int normalAttackTargetCount = 1;
        public bool normalAttackAppliesKnockback;
        [Min(0f)] public float normalAttackKnockbackDuration = 0.18f;
        public float attackSpeed = 1.0f;
        public float critRate = 0.05f;
        public float critDamage = 1.5f;
        public int rewardGold;
        public int rewardExp;
        public string dropTableId;
        public EnemyTrait enemyTrait;
        public BattleFacingDirection battleIdleFacing = BattleFacingDirection.Left;
        public BattleFacingDirection battleMoveFacing = BattleFacingDirection.Left;
        public BattleFacingDirection battleAttackFacing = BattleFacingDirection.Left;
    }
}
