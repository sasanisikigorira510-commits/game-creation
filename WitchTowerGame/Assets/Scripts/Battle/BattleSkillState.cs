namespace WitchTower.Battle
{
    public sealed class BattleSkillState
    {
        public BattleSkillState(BattleSkillType skillType, float cooldown)
        {
            SkillType = skillType;
            Cooldown = cooldown;
            RemainingCooldown = 0f;
        }

        public BattleSkillType SkillType { get; }
        public float Cooldown { get; }
        public float RemainingCooldown { get; private set; }

        public bool IsReady => RemainingCooldown <= 0f;

        public void Tick(float deltaTime)
        {
            if (RemainingCooldown <= 0f)
            {
                RemainingCooldown = 0f;
                return;
            }

            RemainingCooldown -= deltaTime;
            if (RemainingCooldown < 0f)
            {
                RemainingCooldown = 0f;
            }
        }

        public void Trigger()
        {
            RemainingCooldown = Cooldown;
        }
    }
}
