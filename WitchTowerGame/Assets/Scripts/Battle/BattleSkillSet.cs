using System.Collections.Generic;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed class BattleSkillSet
    {
        private readonly Dictionary<BattleSkillType, BattleSkillState> skillStates;

        public BattleSkillSet(float cooldownMultiplier = 1f)
        {
            float safeCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, 0.25f, 3f);
            skillStates = new Dictionary<BattleSkillType, BattleSkillState>
            {
                { BattleSkillType.Strike, new BattleSkillState(BattleSkillType.Strike, 6f * safeCooldownMultiplier) },
                { BattleSkillType.Drain, new BattleSkillState(BattleSkillType.Drain, 8f * safeCooldownMultiplier) },
                { BattleSkillType.Guard, new BattleSkillState(BattleSkillType.Guard, 10f * safeCooldownMultiplier) }
            };
        }

        public BattleSkillState Get(BattleSkillType skillType)
        {
            return skillStates[skillType];
        }

        public void Tick(float deltaTime)
        {
            foreach (var pair in skillStates)
            {
                pair.Value.Tick(deltaTime);
            }
        }
    }
}
