using System.Collections.Generic;

namespace WitchTower.Battle
{
    public readonly struct BattleHitTargetInfo
    {
        public BattleHitTargetInfo(int targetIndex, int damage, int targetRuntimeId = -1)
        {
            TargetIndex = targetIndex;
            Damage = damage;
            TargetRuntimeId = targetRuntimeId;
        }

        public int TargetIndex { get; }
        public int Damage { get; }
        public int TargetRuntimeId { get; }
    }

    public readonly struct BattleHitInfo
    {
        public BattleHitInfo(bool targetIsPlayer, int damage, bool isCritical, bool isSkill, bool causesKnockback, int targetIndex = -1, int attackerIndex = -1, float presentationDelay = 0f, int targetRuntimeId = -1, int attackerRuntimeId = -1, string monsterSkillId = null)
            : this(targetIsPlayer, damage, isCritical, isSkill, causesKnockback, targetIndex, attackerIndex, null, presentationDelay, targetRuntimeId, attackerRuntimeId, monsterSkillId)
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
            float presentationDelay = 0f, int targetRuntimeId = -1, int attackerRuntimeId = -1, string monsterSkillId = null)
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
            TargetRuntimeId = targetRuntimeId;
            AttackerRuntimeId = attackerRuntimeId;
            MonsterSkillId = monsterSkillId;
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
        public int TargetRuntimeId { get; }
        public int AttackerRuntimeId { get; }
        public string MonsterSkillId { get; }
        public bool IsMonsterSkill => !string.IsNullOrEmpty(MonsterSkillId);
        public bool HasTargetHits => TargetHits != null && TargetHits.Count > 0;
    }
}
