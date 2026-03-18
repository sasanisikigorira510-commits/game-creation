using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "PlayerBaseData", menuName = "WitchTower/MasterData/Player Base Data")]
    public sealed class PlayerBaseDataSO : ScriptableObject
    {
        public int initialHp = 100;
        public int initialAttack = 15;
        public int initialDefense = 5;
        public float initialAttackSpeed = 1.0f;
        public float initialCritRate = 0.05f;
        public float initialCritDamage = 1.5f;
    }
}
