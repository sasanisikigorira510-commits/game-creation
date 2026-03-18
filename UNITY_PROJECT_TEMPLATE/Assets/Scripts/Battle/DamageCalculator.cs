using UnityEngine;

namespace WitchTower.Battle
{
    public static class DamageCalculator
    {
        public static DamageCalculationResult Calculate(BattleUnitStats attacker, BattleUnitStats defender)
        {
            return Calculate(attacker, defender.Defense);
        }

        public static DamageCalculationResult Calculate(BattleUnitStats attacker, int defenderDefense)
        {
            var baseDamage = Mathf.Max(1, attacker.Attack - defenderDefense);
            var isCrit = Random.value <= attacker.CritRate;

            if (!isCrit)
            {
                return new DamageCalculationResult(baseDamage, false);
            }

            return new DamageCalculationResult(Mathf.Max(1, Mathf.RoundToInt(baseDamage * attacker.CritDamage)), true);
        }
    }
}
