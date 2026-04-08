using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class DamageCalculator
    {
        public static DamageCalculationResult Calculate(BattleUnitStats attacker, BattleUnitStats defender)
        {
            return Calculate(attacker, defender, MonsterDamageType.Physical);
        }

        public static DamageCalculationResult Calculate(BattleUnitStats attacker, int defenderDefense)
        {
            return Calculate(attacker, defenderDefense, MonsterDamageType.Physical);
        }

        public static DamageCalculationResult Calculate(BattleUnitStats attacker, BattleUnitStats defender, MonsterDamageType damageType)
        {
            if (defender == null)
            {
                return Calculate(attacker, 0, damageType);
            }

            int defenseValue = damageType == MonsterDamageType.Magic
                ? defender.MagicDefense
                : defender.Defense;
            return Calculate(attacker, defenseValue, damageType);
        }

        public static DamageCalculationResult Calculate(BattleUnitStats attacker, int defenderValue, MonsterDamageType damageType)
        {
            int offenseValue = damageType == MonsterDamageType.Magic
                ? Mathf.Max(1, attacker != null ? attacker.Wisdom : 0)
                : Mathf.Max(1, attacker != null ? attacker.Attack : 0);
            int safeDefense = Mathf.Max(0, defenderValue);
            var baseDamage = Mathf.Max(1, Mathf.RoundToInt(offenseValue * (100f / (100f + safeDefense))));
            float critRate = attacker != null ? attacker.CritRate : 0f;
            float critDamage = attacker != null ? attacker.CritDamage : 1.5f;
            var isCrit = Random.value <= critRate;

            if (!isCrit)
            {
                return new DamageCalculationResult(baseDamage, false);
            }

            return new DamageCalculationResult(Mathf.Max(1, Mathf.RoundToInt(baseDamage * critDamage)), true);
        }
    }
}
