using System.Collections.Generic;

namespace WitchTower.Battle
{
    public readonly struct BattleHitTargetInfo
    {
        public BattleHitTargetInfo(int targetIndex, int damage)
        {
            TargetIndex = targetIndex;
            Damage = damage;
        }

        public int TargetIndex { get; }
        public int Damage { get; }
    }

    public readonly struct BattleHitInfo
    {
        public BattleHitInfo(bool targetIsPlayer, int damage, bool isCritical, bool isSkill, bool causesKnockback, int targetIndex = -1, int attackerIndex = -1, float presentationDelay = 0f)
            : this(targetIsPlayer, damage, isCritical, isSkill, causesKnockback, targetIndex, attackerIndex, null, presentationDelay)
        {
        }

        public BattleHitInfo(
            bool targetIsPlayer,
            int damage,
            bool isCritical,
            bool isSkill,
            bool causesKnockback,
            int targetIndex,
            int attackerIndex,
            IReadOnlyList<BattleHitTargetInfo> targetHits,
            float presentationDelay = 0f)
        {
            TargetIsPlayer = targetIsPlayer;
            Damage = damage;
            IsCritical = isCritical;
            IsSkill = isSkill;
            CausesKnockback = causesKnockback;
            TargetIndex = targetIndex;
            AttackerIndex = attackerIndex;
            TargetHits = targetHits;
            PresentationDelay = presentationDelay;
        }

        public bool TargetIsPlayer { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
        public bool IsSkill { get; }
        public bool CausesKnockback { get; }
        public int TargetIndex { get; }
        public int AttackerIndex { get; }
        public IReadOnlyList<BattleHitTargetInfo> TargetHits { get; }
        public float PresentationDelay { get; }
        public bool HasTargetHits => TargetHits != null && TargetHits.Count > 0;
    }
}
