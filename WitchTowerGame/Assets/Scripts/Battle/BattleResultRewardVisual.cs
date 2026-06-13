namespace WitchTower.Battle
{
    public readonly struct BattleResultRewardVisual
    {
        public BattleResultRewardVisual(
            string displayName,
            string detailText,
            string iconResourcePath,
            string frameResourcePath,
            bool isRecruit)
        {
            DisplayName = displayName ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            IconResourcePath = iconResourcePath ?? string.Empty;
            FrameResourcePath = frameResourcePath ?? string.Empty;
            IsRecruit = isRecruit;
        }

        public string DisplayName { get; }
        public string DetailText { get; }
        public string IconResourcePath { get; }
        public string FrameResourcePath { get; }
        public bool IsRecruit { get; }
    }
}
