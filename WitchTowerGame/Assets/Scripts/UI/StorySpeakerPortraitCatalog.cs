namespace WitchTower.UI
{
    // Illustrations affect only presentation; they never change dialogue progress
    // or make recorded/unknown voices appear to be a living character.
    public static class StorySpeakerPortraitCatalog
    {
        public const float FadeDuration = .18f;
        public const string IonaResourcePath = "UI/StoryPortraits/IonaDialogueImage2";
        public const string LucheResourcePath = "UI/StoryPortraits/LucheDialogueImage2";
        public const string ContractorResourcePath = "UI/StoryPortraits/ContractorDialogueImage2";

        public static string GetResourcePath(string speaker)
        {
            switch (speaker)
            {
                case "魔王ガルザ": return WitchTower.Data.GarzaBossPresentation.PortraitPath;
                case "イオナ": return IonaResourcePath;
                case "ルシェ": return LucheResourcePath;
                case "契約師":
                case "主人公": return ContractorResourcePath;
                default: return null;
            }
        }
    }
}
