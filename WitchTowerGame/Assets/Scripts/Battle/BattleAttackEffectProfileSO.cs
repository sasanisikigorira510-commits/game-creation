using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public enum BattleAttackEffectPattern
    {
        Projectile = 0,
        SummonStrike = 1
    }

    [CreateAssetMenu(fileName = "BattleAttackEffectProfile", menuName = "WitchTower/Battle/Attack Effect Profile")]
    public sealed class BattleAttackEffectProfileSO : ScriptableObject
    {
        [Header("Identity")]
        public string effectId;
        public string displayName;
        public MonsterElement element = MonsterElement.None;
        public MonsterDamageType damageType = MonsterDamageType.Physical;
        public BattleAttackEffectPattern pattern = BattleAttackEffectPattern.Projectile;

        [Header("Sprites")]
        public Sprite castSprite;
        public Sprite projectileSprite;
        public Sprite impactSprite;
        public Sprite hitOverlaySprite;
        public Sprite warningAirSprite;
        public Sprite warningGroundSprite;

        [Header("Timing")]
        [Min(0f)] public float castDelay;
        [Min(0f)] public float projectileDelay = 0.08f;
        [Min(0f)] public float impactDelay = 0.18f;
        [Min(0f)] public float hitOverlayDelay = 0.02f;
        [Min(0f)] public float loopDuration = 0.2f;

        [Header("Placement")]
        public Vector2 spawnOffset = new Vector2(0f, 24f);
        public Vector2 targetOffset = new Vector2(0f, 12f);
        public Vector2 warningAirOffset = new Vector2(0f, 54f);
        public Vector2 warningGroundOffset = Vector2.zero;

        [Header("Motion")]
        public Color colorTint = Color.white;
        [Min(0.1f)] public float scale = 1f;
        [Min(0.01f)] public float projectileDuration = 0.16f;
        public bool attachHitOverlayToTarget = true;

        public bool UsesProjectile => pattern == BattleAttackEffectPattern.Projectile;
        public bool UsesSummonStrike => pattern == BattleAttackEffectPattern.SummonStrike;
        public bool HasCastSprite => castSprite != null;
        public bool HasProjectileSprite => projectileSprite != null;
        public bool HasImpactSprite => impactSprite != null;
        public bool HasHitOverlaySprite => hitOverlaySprite != null;
        public bool HasWarningAirSprite => warningAirSprite != null;
        public bool HasWarningGroundSprite => warningGroundSprite != null;
    }
}
