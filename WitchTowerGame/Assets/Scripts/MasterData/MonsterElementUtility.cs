namespace WitchTower.MasterData
{
    public static class MonsterElementUtility
    {
        private const float AdvantageMultiplier = 1.5f;

        public static float GetDamageMultiplier(MonsterElement attacker, MonsterElement defender)
        {
            if (HasAdvantage(attacker, defender))
            {
                return AdvantageMultiplier;
            }

            return 1.0f;
        }

        public static bool HasAdvantage(MonsterElement attacker, MonsterElement defender)
        {
            return attacker switch
            {
                MonsterElement.Wood => defender == MonsterElement.Water,
                MonsterElement.Water => defender == MonsterElement.Fire,
                MonsterElement.Fire => defender == MonsterElement.Wood,
                MonsterElement.Light => defender == MonsterElement.Dark,
                MonsterElement.Dark => defender == MonsterElement.Light,
                _ => false
            };
        }
    }
}
