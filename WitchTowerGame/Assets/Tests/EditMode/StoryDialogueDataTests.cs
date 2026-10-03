using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class StoryDialogueDataTests
    {
        private static readonly string[] ChapterIds =
        {
            "story_chapter_2_unlocked", "story_chapter_3_unlocked", "story_chapter_4_unlocked",
            "story_chapter_5_unlocked", "story_chapter_6_unlocked", "story_first_arc_complete"
        };
        private const string BeforeId = "story_first_summon_iona_before";
        private const string AfterId = "story_first_summon_iona_after";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Static(string type, string method, params object[] args) =>
            T(type).GetMethod(method).Invoke(null, args);
        private static object P(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        private static void Set(object value, string name, object item) => value.GetType().GetProperty(name).SetValue(value, item);
        private static void Field(object value, string name, object item) => value.GetType().GetField(name).SetValue(value, item);
        private static object Progress(string method, params object[] args) => Static("Data.StoryDialogueProgress", method, args);
        private static object Find(string id) => Static("Data.StoryDialogueCatalog", "Find", id);
        private static object[] Lines(object definition) => ((Array)P(definition, "Lines")).Cast<object>().ToArray();
        private static object[] All() => ((Array)T("Data.StoryDialogueCatalog").GetProperty("All").GetValue(null)).Cast<object>().ToArray();
        private static IList Seen(object profile) => (IList)P(profile, "SeenStoryEventIds");

        private static object Profile(int floor = 0, bool completed = true, string step = "Complete", int summons = 3)
        {
            object save = Static("Save.PlayerSaveData", "CreateDefault");
            Field(save, "HighestFloor", floor);
            Field(save, "HasCompletedTutorial", completed);
            Field(save, "TutorialStepId", step);
            Field(save, "InitialTutorialSummonCount", summons);
            return Activator.CreateInstance(T("Data.PlayerProfile"), save);
        }

        private static object Save(object profile) => profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 });
        private static object Restore(object profile) => Activator.CreateInstance(T("Data.PlayerProfile"),
            JsonUtility.FromJson(JsonUtility.ToJson(Save(profile)), T("Save.PlayerSaveData")));

        private static string GameplaySnapshot(object profile)
        {
            object save = Save(profile);
            Field(save, "StoryDialogueEventId", string.Empty);
            Field(save, "StoryDialogueLineIndex", 0);
            ((IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save)).Clear();
            return JsonUtility.ToJson(save);
        }

        [Test]
        public void CatalogHasExactApprovedTextAndSpeakerForAll114Messages()
        {
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string[] sourceFiles = new[] { "STORY_FIRST_SUMMON_IONA_2026-09-20.md" }
                .Concat(Enumerable.Range(1, 6).Select(n => $"STORY_CHAPTER_{n:00}_2026-09-20.md")).ToArray();
            var definitions = All().Where(d => (string)P(d, "EventId") != "story_guardian_trial_intro").ToArray();
            Assert.That(definitions.Select(d => (string)P(d, "EventId")),
                Is.EqualTo(new[] { BeforeId, AfterId }.Concat(ChapterIds)));
            Assert.That(definitions.Select(d => Lines(d).Length), Is.EqualTo(new[] { 6, 2, 16, 16, 16, 18, 16, 24 }));
            int checkedLines = 0;
            for (int source = 0; source < sourceFiles.Length; source++)
            {
                string markdown = File.ReadAllText(Path.Combine(repository, sourceFiles[source]));
                var expected = Regex.Matches(markdown, @"^話者：([^\r\n]*)\r?\nセリフ：([^\r\n]*)", RegexOptions.Multiline);
                object[] actual = source == 0 ? Lines(definitions[0]).Concat(Lines(definitions[1])).ToArray()
                    : Lines(definitions[source + 1]);
                Assert.That(actual.Length, Is.EqualTo(expected.Count), sourceFiles[source]);
                for (int line = 0; line < expected.Count; line++)
                {
                    Assert.That(P(actual[line], "Speaker"), Is.EqualTo(expected[line].Groups[1].Value.TrimEnd(' ')),
                        sourceFiles[source] + " speaker " + (line + 1));
                    Assert.That(P(actual[line], "Text"), Is.EqualTo(expected[line].Groups[2].Value.TrimEnd(' ')),
                        sourceFiles[source] + " line " + (line + 1));
                    checkedLines++;
                }
            }
            Assert.That(checkedLines, Is.EqualTo(114));
            Assert.That(Find("missing_story"), Is.Null);
            Assert.That(Find(null), Is.Null);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ChapterUnlocksAtItsTenthFloorAndPreservesExistingReadFlags(int chapter)
        {
            int floor = (chapter + 1) * 10;
            object profile = Profile(floor - 1);
            for (int prior = 0; prior < chapter; prior++) Seen(profile).Add(ChapterIds[prior]);
            Assert.That(P(Find(ChapterIds[chapter]), "UnlockFloor"), Is.EqualTo(floor));
            Assert.That(Progress("GetPendingChapter", profile), Is.Null);
            Set(profile, "HighestFloor", floor);
            Assert.That(P(Progress("GetPendingChapter", profile), "EventId"), Is.EqualTo(ChapterIds[chapter]));
            Assert.That(Progress("Begin", profile, ChapterIds[chapter]), Is.True);
            Assert.That(Progress("Complete", profile, ChapterIds[chapter]), Is.True);
            Assert.That(Progress("GetPendingChapter", profile), Is.Null);
            Assert.That(Progress("Begin", profile, ChapterIds[chapter]), Is.False, "Previously read stories belong in optional replay UI.");
            Assert.That(Seen(profile).Cast<string>(), Is.EqualTo(ChapterIds.Take(chapter + 1)));
        }

        [Test]
        public void ChapterQueueReturnsOldestUnreadAndDoesNotRequireGuardianOwnership()
        {
            object profile = Profile(60);
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.Zero);
            for (int chapter = 0; chapter < ChapterIds.Length; chapter++)
            {
                if (chapter == 3)
                {
                    Assert.That(P(Progress("GetPendingHomeDialogue", profile), "EventId"), Is.EqualTo("story_guardian_trial_intro"));
                    Progress("Begin", profile, "story_guardian_trial_intro");
                    Progress("Complete", profile, "story_guardian_trial_intro");
                }
                Assert.That(P(Progress("GetPendingHomeDialogue", profile), "EventId"), Is.EqualTo(ChapterIds[chapter]));
                Progress("Begin", profile, ChapterIds[chapter]);
                Progress("Complete", profile, ChapterIds[chapter]);
            }
            Assert.That(Progress("GetPendingHomeDialogue", profile), Is.Null);
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.Zero);
        }

        [Test]
        public void IncompleteTutorialCannotTriggerChapterEvenWithHighPreviewProgress()
        {
            object profile = Profile(60, false, "T02", 2);
            Assert.That(Progress("GetPendingChapter", profile), Is.Null);
            Assert.That(Progress("GetPendingHomeDialogue", profile), Is.Null);
            Assert.That(Progress("GetPendingHomeDialogue", new object[] { null }), Is.Null);
        }

        [TestCase("T02", 0, false)]
        [TestCase("T02", 3, false)]
        [TestCase("T02A", 2, false)]
        [TestCase("T02A", 3, true)]
        [TestCase("T03", 3, false)]
        public void HomeSummonEpilogueRequiresExistingThreeGrantsAndPostSummonStep(string step, int summons, bool pending)
        {
            object profile = Profile(60, false, step, summons);
            object definition = Progress("GetPendingHomeDialogue", profile);
            if (!pending) Assert.That(definition, Is.Null);
            else
            {
                Assert.That(P(definition, "EventId"), Is.EqualTo(AfterId));
                string before = GameplaySnapshot(profile);
                Progress("Begin", profile, AfterId);
                Progress("Complete", profile, AfterId);
                Assert.That(GameplaySnapshot(profile), Is.EqualTo(before), "Dialogue must not repeat grants or advance T02A.");
                Assert.That(Progress("GetPendingHomeDialogue", profile), Is.Null);
            }
        }

        [Test]
        public void GuardianLessonFollowsChapterThreeAndResumesFromRealRewards()
        {
            const string intro = "story_guardian_trial_intro";
            var profile = Profile(30);
            Seen(profile).Add(ChapterIds[0]); Seen(profile).Add(ChapterIds[1]);
            Assert.That(P(Progress("GetPendingHomeDialogue", profile), "EventId"), Is.EqualTo(ChapterIds[2]));
            Seen(profile).Add(ChapterIds[2]);
            Assert.That(P(Progress("GetPendingHomeDialogue", profile), "EventId"), Is.EqualTo(intro));
            Progress("Begin", profile, intro); Progress("Advance", profile, intro, 3);
            profile = Restore(profile);
            Assert.That(P(profile, "StoryDialogueLineIndex"), Is.EqualTo(3));
            Progress("Complete", profile, intro);
            Assert.That(((IList)P(profile, "OwnedGuardians")).Count, Is.Zero);
            Assert.That(((IList)P(profile, "GuardianCoreIds")).Count, Is.Zero);
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "seiryu", false), Is.EqualTo("choose"));
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "seiryu", true), Is.EqualTo("trial"));
            profile = Restore(profile); // Leaving or losing never advances the trial.
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "seiryu", true), Is.EqualTo("trial"));
            Assert.That(Static("Data.GuardianService", "GrantCore", profile, "seiryu"), Is.True);
            profile = Restore(profile);
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "seiryu", false), Is.EqualTo("birth"));
            Static("Data.GuardianService", "Birth", profile, "seiryu");
            profile = Restore(profile);
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "seiryu", false), Is.EqualTo("equip"));
            Static("Data.GuardianService", "Equip", profile, "seiryu");
            profile = Restore(profile);
            Assert.That(Static("Data.GuardianTutorial", "IsActive", profile), Is.False);
            Assert.That(Progress("GetPendingHomeDialogue", profile), Is.Null);
        }

        [Test]
        public void ExistingGuardianOwnersResumeFormationWithoutRepeatingTheTrialIntroduction()
        {
            var profile = Profile(30);
            foreach (string id in ChapterIds.Take(3)) Seen(profile).Add(id);
            Static("Data.GuardianService", "GrantCore", profile, "suzaku");
            Assert.That(Static("Data.GuardianTutorial", "ShouldIntroduce", profile), Is.False);
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "suzaku", false), Is.EqualTo("birth"));
            Static("Data.GuardianService", "Birth", profile, "suzaku");
            Assert.That(Static("Data.GuardianTutorial", "Step", profile, "suzaku", false), Is.EqualTo("equip"));
            Assert.That(Progress("GetPendingHomeDialogue", profile), Is.Null);
        }

        [Test]
        public void EveryDialogueResumesEverySavedLineWithoutMarkingUnreadTextAsRead()
        {
            foreach (object definition in All())
            {
                string id = (string)P(definition, "EventId");
                object profile = Profile(60);
                Progress("Begin", profile, id);
                for (int line = 0; line < Lines(definition).Length; line++)
                {
                    Progress("Advance", profile, id, line);
                    profile = Restore(profile);
                    Assert.That(P(profile, "StoryDialogueEventId"), Is.EqualTo(id));
                    Assert.That(P(profile, "StoryDialogueLineIndex"), Is.EqualTo(line));
                    Assert.That(Seen(profile).Contains(id), Is.False);
                    Assert.That(Progress("Begin", profile, id), Is.False, "Reopening the current event preserves its cursor.");
                }
            }
        }

        [Test]
        public void SkipOrRepeatedCompletePreservesGameplayAndCannotCompleteAnotherDialogue()
        {
            object profile = Profile(30);
            string before = GameplaySnapshot(profile);
            Progress("Begin", profile, ChapterIds[0]);
            Progress("Advance", profile, ChapterIds[0], 7);
            Assert.That(Progress("Complete", profile, ChapterIds[2]), Is.False);
            Assert.That(Progress("Advance", profile, ChapterIds[2], 12), Is.False);
            Assert.That(Progress("Complete", profile, ChapterIds[0]), Is.True, "Skip marks exactly the active event read.");
            Assert.That(Progress("Complete", profile, ChapterIds[0]), Is.False);
            profile = Restore(profile);
            Assert.That(P(profile, "StoryDialogueEventId"), Is.EqualTo(string.Empty));
            Assert.That(P(profile, "StoryDialogueLineIndex"), Is.Zero);
            Assert.That(Seen(profile).Cast<string>(), Is.EqualTo(new[] { ChapterIds[0] }));
            Assert.That(GameplaySnapshot(profile), Is.EqualTo(before));
            Progress("Begin", profile, ChapterIds[2]);
            Assert.That(Progress("Complete", profile, ChapterIds[0]), Is.False, "A stale callback cannot close the new event.");
            Assert.That(P(profile, "StoryDialogueEventId"), Is.EqualTo(ChapterIds[2]));
        }

        [Test]
        public void ChapterTwoRetainsExistingFusionGiftWithoutGivingItAgainOnReplay()
        {
            object profile = Profile(20);
            Progress("Begin", profile, ChapterIds[1]);
            Progress("Complete", profile, ChapterIds[1]);
            var owned = (IList)P(profile, "OwnedMonsters");
            Assert.That(owned.Count, Is.EqualTo(2));
            string[] instances = owned.Cast<object>().Select(m => (string)m.GetType().GetField("InstanceId").GetValue(m)).ToArray();
            Assert.That(instances.All(id => id.StartsWith("tutorial_gift_fusion_rock_golem_", StringComparison.Ordinal)), Is.True);
            Assert.That(Progress("Complete", profile, ChapterIds[1]), Is.False);
            Assert.That(Progress("Begin", profile, ChapterIds[1]), Is.False);
            Assert.That(owned.Cast<object>().Select(m => (string)m.GetType().GetField("InstanceId").GetValue(m)), Is.EqualTo(instances));
        }

        [Test]
        public void NewProgressFieldsAreOptionalInOlderJsonAndExistingSeenChaptersStayRead()
        {
            string json = "{\"SchemaVersion\":2,\"HighestFloor\":60,\"HasCompletedTutorial\":true,\"TutorialStepId\":\"Complete\",\"SeenStoryEventIds\":[" +
                string.Join(",", ChapterIds.Select(id => "\"" + id + "\"")) + "]}";
            object save = JsonUtility.FromJson(json, T("Save.PlayerSaveData"));
            object[] migration = { save, null };
            Assert.That(T("Save.PlayerSaveDataMigration").GetMethod("TryMigrate").Invoke(null, migration), Is.True);
            object profile = Activator.CreateInstance(T("Data.PlayerProfile"), save);
            Assert.That(P(profile, "StoryDialogueEventId"), Is.EqualTo(string.Empty));
            Assert.That(P(profile, "StoryDialogueLineIndex"), Is.Zero);
            Assert.That(Progress("GetPendingChapter", profile), Is.Null);
            Assert.That(P(Progress("GetPendingHomeDialogue", profile), "EventId"), Is.EqualTo("story_guardian_trial_intro"));
            Assert.That(Seen(profile).Cast<string>(), Is.EqualTo(ChapterIds));
        }

        [TestCase("story_chapter_2_unlocked", -20, 0)]
        [TestCase("story_chapter_2_unlocked", 999, 15)]
        [TestCase("story_chapter_2_unlocked", 16, 15)]
        [TestCase("story_chapter_2_unlocked", 17, 15)]
        [TestCase("story_first_arc_complete", 999, 23)]
        [TestCase("story_first_summon_iona_after", 999, 1)]
        [TestCase("story_first_summon_iona_before", 6, 5)]
        [TestCase("story_first_summon_iona_before", 7, 5)]
        [TestCase("story_chapter_3_unlocked", 16, 15)]
        [TestCase("story_chapter_3_unlocked", 17, 15)]
        [TestCase("story_chapter_4_unlocked", 16, 15)]
        [TestCase("story_chapter_4_unlocked", 17, 15)]
        [TestCase("story_chapter_6_unlocked", 16, 15)]
        [TestCase("story_chapter_6_unlocked", 17, 15)]
        [TestCase("missing_story", 5, 0)]
        [TestCase(null, 5, 0)]
        public void RestoredCursorIsSafeWithoutSilentlyCompletingUnreadStory(string id, int index, int expected)
        {
            object profile = Profile(60);
            object save = Save(profile);
            Field(save, "StoryDialogueEventId", id);
            Field(save, "StoryDialogueLineIndex", index);
            object restored = Activator.CreateInstance(T("Data.PlayerProfile"), save);
            Assert.That(P(restored, "StoryDialogueLineIndex"), Is.EqualTo(expected));
            Assert.That(P(restored, "StoryDialogueEventId"), Is.EqualTo(Find(id) == null ? string.Empty : id));
            Assert.That(Seen(restored), Is.Empty);
        }

        [Test]
        public void AlreadySeenStaleCursorClearsOnLoadAndOldCallbacksCannotRegressPosition()
        {
            object profile = Profile(10);
            Progress("Begin", profile, ChapterIds[0]);
            Progress("Advance", profile, ChapterIds[0], 8);
            Assert.That(Progress("Advance", profile, ChapterIds[0], 2), Is.False);
            Assert.That(P(profile, "StoryDialogueLineIndex"), Is.EqualTo(8));
            Seen(profile).Add(ChapterIds[0]);
            object restored = Restore(profile);
            Assert.That(P(restored, "StoryDialogueEventId"), Is.EqualTo(string.Empty));
            Assert.That(P(restored, "StoryDialogueLineIndex"), Is.Zero);
            Assert.That(Progress("GetPendingChapter", restored), Is.Null);
        }
    }
}
