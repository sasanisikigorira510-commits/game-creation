namespace WitchTower.Home
{
    public readonly struct MissionDefinition
    {
        public MissionDefinition(string missionId, string title, int targetValue, int rewardGold)
        {
            MissionId = missionId;
            Title = title;
            TargetValue = targetValue;
            RewardGold = rewardGold;
        }

        public string MissionId { get; }
        public string Title { get; }
        public int TargetValue { get; }
        public int RewardGold { get; }
    }
}
