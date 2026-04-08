using UnityEngine;

namespace WitchTower.Battle
{
    [System.Serializable]
    public sealed class BattleUnitStats
    {
        public int MaxHp;
        public int CurrentHp;
        public int Attack;
        public int Wisdom;
        public int Defense;
        public int MagicDefense;
        public float AttackSpeed;
        public float CritRate;
        public float CritDamage;

        public void ResetCurrentHp()
        {
            CurrentHp = MaxHp;
        }

        public void ApplyDamage(int damage)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - damage);
        }

        public bool IsDead()
        {
            return CurrentHp <= 0;
        }
    }
}
