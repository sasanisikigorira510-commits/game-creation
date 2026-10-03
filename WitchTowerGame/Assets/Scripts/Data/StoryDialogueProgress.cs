using System;

namespace WitchTower.Data
{
    // Dialogue state only. The caller saves after each change; summon and battle
    // transactions remain owned by their existing controllers.
    public static class StoryDialogueProgress
    {
        public static StoryDialogueDefinition GetPendingChapter(PlayerProfile profile)
        {
            if (profile == null || !profile.HasCompletedTutorial) return null;
            foreach (var definition in StoryDialogueCatalog.All)
            {
                if (definition.UnlockFloor > 0 && profile.HighestFloor >= definition.UnlockFloor &&
                    !StoryTutorialService.HasSeenStory(profile, definition.EventId)) return definition;
            }
            return null;
        }

        public static StoryDialogueDefinition GetPendingHomeDialogue(PlayerProfile profile)
        {
            if (profile == null) return null;
            if (!profile.HasCompletedTutorial &&
                string.Equals(profile.TutorialStepId, StoryTutorialService.StepFirstExplorationIntro, StringComparison.Ordinal) &&
                StoryTutorialService.HasCompletedInitialSummons(profile) &&
                !StoryTutorialService.HasSeenStory(profile, StoryDialogueCatalog.IntroAfterId))
                return StoryDialogueCatalog.Find(StoryDialogueCatalog.IntroAfterId);
            var chapter = GetPendingChapter(profile);
            if (chapter != null && chapter.UnlockFloor <= GuardianService.UnlockFloor) return chapter;
            if (GuardianTutorial.ShouldIntroduce(profile))
                return StoryDialogueCatalog.Find(StoryDialogueCatalog.GuardianIntroId);
            return chapter;
        }

        public static bool Begin(PlayerProfile profile, string eventId)
        {
            var definition = StoryDialogueCatalog.Find(eventId);
            if (profile == null || definition == null || definition.Lines.Length == 0 ||
                StoryTutorialService.HasSeenStory(profile, eventId)) return false;
            if (string.Equals(profile.StoryDialogueEventId, eventId, StringComparison.Ordinal))
                return NormalizeSavedProgress(profile);
            profile.StoryDialogueEventId = eventId;
            profile.StoryDialogueLineIndex = 0;
            return true;
        }

        // index is the zero-based line currently displayed, not a completion
        // marker. Stale callbacks cannot overwrite another event or move back.
        public static bool Advance(PlayerProfile profile, string eventId, int index)
        {
            var definition = StoryDialogueCatalog.Find(eventId);
            if (profile == null || definition == null || definition.Lines.Length == 0 ||
                !string.Equals(profile.StoryDialogueEventId, eventId, StringComparison.Ordinal) ||
                StoryTutorialService.HasSeenStory(profile, eventId)) return false;
            int clamped = Math.Min(definition.Lines.Length - 1, Math.Max(0, index));
            int next = Math.Max(profile.StoryDialogueLineIndex, clamped);
            if (profile.StoryDialogueLineIndex == next) return false;
            profile.StoryDialogueLineIndex = next;
            return true;
        }

        public static bool Complete(PlayerProfile profile, string eventId)
        {
            if (profile == null || StoryDialogueCatalog.Find(eventId) == null ||
                !string.Equals(profile.StoryDialogueEventId, eventId, StringComparison.Ordinal)) return false;
            // Keep the existing chapter-completion behavior (including its
            // once-only fusion lesson gift) but never invoke it for replays.
            if (!StoryTutorialService.HasSeenStory(profile, eventId))
                StoryTutorialService.MarkStorySeen(profile, eventId);
            profile.StoryDialogueEventId = string.Empty;
            profile.StoryDialogueLineIndex = 0;
            return true;
        }

        internal static bool NormalizeSavedProgress(PlayerProfile profile)
        {
            if (profile == null) return false;
            string savedId = profile.StoryDialogueEventId;
            int savedIndex = profile.StoryDialogueLineIndex;
            var definition = StoryDialogueCatalog.Find(savedId);
            if (definition == null || definition.Lines.Length == 0 ||
                StoryTutorialService.HasSeenStory(profile, savedId))
            {
                profile.StoryDialogueEventId = string.Empty;
                profile.StoryDialogueLineIndex = 0;
            }
            else
            {
                profile.StoryDialogueLineIndex = Math.Min(definition.Lines.Length - 1, Math.Max(0, savedIndex));
            }
            return !string.Equals(savedId, profile.StoryDialogueEventId, StringComparison.Ordinal) ||
                savedIndex != profile.StoryDialogueLineIndex;
        }
    }
}
