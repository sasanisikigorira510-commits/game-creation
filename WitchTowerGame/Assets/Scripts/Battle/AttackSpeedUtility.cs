using UnityEngine;

namespace WitchTower.Battle
{
    public static class AttackSpeedUtility
    {
        public static float ResolveAttackRateMultiplier(float attackSpeedValue)
        {
            if (attackSpeedValue <= 0f)
            {
                return 1f;
            }

            // Legacy compatibility: older data used direct multipliers such as 0.8 or 1.2.
            if (attackSpeedValue <= 5f)
            {
                return Mathf.Max(0.2f, attackSpeedValue);
            }

            float clampedBonus = Mathf.Clamp(attackSpeedValue - 100f, -100f, 200f);
            return Mathf.Max(0.1f, (100f + clampedBonus) / 100f);
        }
    }
}
