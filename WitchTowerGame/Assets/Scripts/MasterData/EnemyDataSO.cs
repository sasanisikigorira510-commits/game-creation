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
        public int defense;
        public float attackSpeed = 1.0f;
        public float critRate = 0.05f;
        public float critDamage = 1.5f;
        public int rewardGold;
        public int rewardExp;
        public string dropTableId;
        public EnemyTrait enemyTrait;
    }
}
