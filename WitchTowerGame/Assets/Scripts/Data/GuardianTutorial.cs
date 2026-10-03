namespace WitchTower.Data
{
    // Guidance is derived from saved ownership and cores. It never grants rewards
    // or marks formation complete on a dialogue skip, loss or navigation away.
    public static class GuardianTutorial
    {
        public static bool ShouldIntroduce(PlayerProfile p) => GuardianService.NeedsTutorial(p) &&
            StoryTutorialService.HasSeenStory(p, StoryTutorialService.StoryChapter4Unlocked) &&
            !StoryTutorialService.HasSeenStory(p, StoryDialogueCatalog.GuardianIntroId) &&
            p.OwnedGuardians.Count == 0 && p.GuardianCoreIds.Count == 0;

        public static bool IsActive(PlayerProfile p) => GuardianService.NeedsTutorial(p) &&
            StoryTutorialService.HasSeenStory(p, StoryTutorialService.StoryChapter4Unlocked) &&
            (StoryTutorialService.HasSeenStory(p, StoryDialogueCatalog.GuardianIntroId) ||
             p.OwnedGuardians.Count > 0 || p.GuardianCoreIds.Count > 0);

        public static string Step(PlayerProfile p, string id, bool selected)
        {
            if (!IsActive(p)) return "";
            if (GuardianService.Owned(p, id) != null) return "equip";
            if (p.GuardianCoreIds.Contains(id)) return "birth";
            if (!GuardianService.CanChallenge(p, id)) return "choose";
            return selected ? "trial" : "choose";
        }

        public static string Instruction(string step)
        {
            switch (step)
            {
                case "choose": return "ルシェ：まずは下の四柱から神獣を選んでください。";
                case "trial": return "イオナ：準備はいい？ このボタンから試練へ進めるわ。";
                case "birth": return "イオナ：神核を持ち帰ったのね。ここから仲間に迎えましょう。";
                case "equip": return "ルシェ：最後に専用枠へ編成すると一緒に戦えます。";
                default: return "";
            }
        }
    }
}
