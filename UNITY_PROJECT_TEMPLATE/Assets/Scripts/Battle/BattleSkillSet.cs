using System.Collections.Generic;

namespace WitchTower.Battle
{
    public sealed class BattleSkillSet
    {
        private readonly Dictionary<BattleSkillType, BattleSkillState> skillStates;

        public BattleSkillSet()
        {
            skillStates = new Dictionary<BattleSkillType, BattleSkillState>
            {
                { BattleSkillType.Strike, new BattleSkillState(BattleSkillType.Strike, 6f) },
                { BattleSkillType.Drain, new BattleSkillState(BattleSkillType.Drain, 8f) },
                { BattleSkillType.Guard, new BattleSkillState(BattleSkillType.Guard, 10f) }
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
