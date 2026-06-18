using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchTower.Data
{
    public sealed class StoryTutorialEvent
    {
        public StoryTutorialEvent(
            string eventId,
            string stepId,
            string title,
            string body,
            string targetKey = "",
            bool blocksInput = false)
        {
            EventId = eventId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            TargetKey = targetKey ?? string.Empty;
            BlocksInput = blocksInput;
        }

        public string EventId { get; }
        public string StepId { get; }
        public string Title { get; }
        public string Body { get; }
        public string TargetKey { get; }
        public bool BlocksInput { get; }
        public bool IsValid => !string.IsNullOrEmpty(EventId);
    }

    public static class StoryTutorialService
    {
        public const string CompleteStepId = "Complete";
        public const string StepWakeup = "T00";
        public const string StepOpenGacha = "T01";
        public const string StepFirstSummon = "T02";
        public const string StepOpenFormation = "T03";
        public const string StepFirstFormation = "T04";
        public const string StepOpenBattle = "T05";
        public const string StepFirstBattle = "T06";
        public const string StepFirstResult = "T07";
        public const string StepWrapUp = "T08";

        public const string StoryPrologueWakeup = "story_prologue_wakeup";
        public const string StoryFirstSummonDone = "story_first_summon_done";
        public const string StoryFirstBattleWin = "story_first_battle_win";
        public const string StoryFirstEquipmentQuality = "story_first_equipment_quality";
        public const string StoryFirstEquipmentEnhance = "story_first_equipment_enhance";
        public const string StoryChapter2Unlocked = "story_chapter_2_unlocked";
        public const string StoryChapter3Unlocked = "story_chapter_3_unlocked";
        public const string StoryChapter4Unlocked = "story_chapter_4_unlocked";
        public const string StoryChapter5Unlocked = "story_chapter_5_unlocked";
        public const string StoryChapter6Unlocked = "story_chapter_6_unlocked";
        public const string StoryFirstArcComplete = "story_first_arc_complete";

        public const string HintEquipment = "tutorial_equipment";
        public const string HintEquipmentQuality = "tutorial_equipment_quality";
        public const string HintEquipmentEnhance = "tutorial_equipment_enhance";
        public const string HintFusion = "tutorial_fusion";
        public const string HintDex = "tutorial_dex";
        public const string HintShop = "tutorial_shop";

        private const string DefaultStarterMonsterId = "monster_dragon_whelp";

        private static readonly Dictionary<string, string> NextStepByCompletedStep = new Dictionary<string, string>
        {
            { StepWakeup, StepOpenGacha },
            { StepOpenGacha, StepFirstSummon },
            { StepFirstSummon, StepOpenFormation },
            { StepOpenFormation, StepFirstFormation },
            { StepFirstFormation, StepOpenBattle },
            { StepOpenBattle, StepFirstBattle },
            { StepFirstBattle, StepFirstResult },
            { StepFirstResult, StepWrapUp },
            { StepWrapUp, CompleteStepId }
        };

        public static StoryTutorialEvent GetNextEvent(PlayerProfile profile, string sceneName)
        {
            if (profile == null)
            {
                return null;
            }

            NormalizeTutorialState(profile);
            if (!profile.HasCompletedTutorial)
            {
                StoryTutorialEvent requiredEvent = GetRequiredTutorialEvent(profile, sceneName);
                if (requiredEvent != null)
                {
                    return requiredEvent;
                }
            }

            StoryTutorialEvent chapterEvent = GetChapterStoryEvent(profile, sceneName);
            if (chapterEvent != null)
            {
                return chapterEvent;
            }

            return GetOptionalHint(profile, sceneName);
        }

        public static bool HasSeenStory(PlayerProfile profile, string eventId)
        {
            return HasSeen(profile?.SeenStoryEventIds, eventId);
        }

        public static bool HasSeenHint(PlayerProfile profile, string hintId)
        {
            return HasSeen(profile?.SeenTutorialHintIds, hintId);
        }

        public static bool MarkStorySeen(PlayerProfile profile, string eventId)
        {
            return MarkSeen(profile?.SeenStoryEventIds, eventId);
        }

        public static bool MarkHintSeen(PlayerProfile profile, string hintId)
        {
            return MarkSeen(profile?.SeenTutorialHintIds, hintId);
        }

        public static bool IsChapterStoryEvent(string eventId)
        {
            return eventId == StoryChapter2Unlocked ||
                   eventId == StoryChapter3Unlocked ||
                   eventId == StoryChapter4Unlocked ||
                   eventId == StoryChapter5Unlocked ||
                   eventId == StoryChapter6Unlocked ||
                   eventId == StoryFirstArcComplete;
        }

        public static void BackfillClearedChapterStories(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            MarkClearedChapterStory(profile, 5, StoryChapter2Unlocked);
            MarkClearedChapterStory(profile, 10, StoryChapter3Unlocked);
            MarkClearedChapterStory(profile, 15, StoryChapter4Unlocked);
            MarkClearedChapterStory(profile, 20, StoryChapter5Unlocked);
            MarkClearedChapterStory(profile, 25, StoryChapter6Unlocked);
            MarkClearedChapterStory(profile, 30, StoryFirstArcComplete);
        }

        public static bool AdvanceTutorial(PlayerProfile profile, string completedStepId)
        {
            if (profile == null || string.IsNullOrEmpty(completedStepId))
            {
                return false;
            }

            NormalizeTutorialState(profile);
            if (profile.HasCompletedTutorial)
            {
                return false;
            }

            if (!string.Equals(profile.TutorialStepId, completedStepId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!NextStepByCompletedStep.TryGetValue(completedStepId, out string nextStepId))
            {
                return false;
            }

            profile.TutorialStepId = nextStepId;
            if (nextStepId == CompleteStepId)
            {
                CompleteTutorial(profile);
            }

            return true;
        }

        public static bool CompleteTutorial(PlayerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            bool changed = !profile.HasCompletedTutorial ||
                !string.Equals(profile.TutorialStepId, CompleteStepId, StringComparison.Ordinal);
            profile.HasCompletedTutorial = true;
            profile.TutorialStepId = CompleteStepId;
            return changed;
        }

        public static void ApplySkipRecovery(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.OwnedMonsters.Count == 0)
            {
                profile.AddOwnedMonster(DefaultStarterMonsterId, 1);
            }

            bool hasPartyMember = profile.PartyMonsterInstanceIds.Any(instanceId =>
                !string.IsNullOrEmpty(instanceId) && profile.GetOwnedMonster(instanceId) != null);
            if (!hasPartyMember)
            {
                string firstOwnedInstanceId = profile.OwnedMonsters
                    .FirstOrDefault(monster => monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                    ?.InstanceId ?? string.Empty;
                profile.SetPartyMonsterIds(new[] { firstOwnedInstanceId });
            }

            CompleteTutorial(profile);
        }

        private static StoryTutorialEvent GetRequiredTutorialEvent(PlayerProfile profile, string sceneName)
        {
            string normalizedSceneName = sceneName ?? string.Empty;
            switch (profile.TutorialStepId)
            {
                case StepWakeup:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            StoryPrologueWakeup,
                            StepWakeup,
                            "契約炉の目覚め",
                            "……契約炉、再起動。あなたはこの契約炉に選ばれた契約師です。まずは、失われた眷属を一体呼び戻しましょう。",
                            blocksInput: true)
                        : null;
                case StepOpenGacha:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            "tutorial_open_gacha",
                            StepOpenGacha,
                            "最初の召喚",
                            "契約片が一つ残っています。召喚で、最初の眷属を呼び戻しましょう。",
                            "home.gacha",
                            true)
                        : null;
                case StepFirstSummon:
                    return IsScene(normalizedSceneName, "GachaScene")
                        ? new StoryTutorialEvent(
                            "tutorial_first_summon",
                            StepFirstSummon,
                            "契約召喚",
                            "契約炉に石を捧げると、ダンジョンに残った記録から眷属が応えてくれます。",
                            "gacha.single_free",
                            true)
                        : null;
                case StepOpenFormation:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            StoryFirstSummonDone,
                            StepOpenFormation,
                            "最初の眷属",
                            "契約成功です。呼び戻した眷属を編成に入れて、探索隊として送り出しましょう。",
                            "home.formation",
                            true)
                        : null;
                case StepFirstFormation:
                    return IsScene(normalizedSceneName, "FormationScene")
                        ? new StoryTutorialEvent(
                            "tutorial_first_formation",
                            StepFirstFormation,
                            "探索隊編成",
                            "探索隊は最大5体まで組めます。まずは一体で大丈夫。契約した眷属を先頭に置きましょう。",
                            "formation.slot_1",
                            true)
                        : null;
                case StepOpenBattle:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            "tutorial_open_battle",
                            StepOpenBattle,
                            "最初の探索",
                            "よぉし、探索隊の準備完了です。見習いの五門洞へ向かい、契約片を回収しましょう。",
                            "home.battle",
                            true)
                        : null;
                case StepFirstBattle:
                    if (IsScene(normalizedSceneName, "DungeonSelectionPanel"))
                    {
                        return new StoryTutorialEvent(
                            "tutorial_choose_first_dungeon",
                            StepFirstBattle,
                            "ダンジョン選択",
                            "探索先ごとに出現する眷属と報酬の傾向が変わります。最初は見習いの五門洞 第1階層から契約片を回収しましょう。",
                            "dungeon.start",
                            true);
                    }

                    return IsScene(normalizedSceneName, "BattleScene")
                        ? new StoryTutorialEvent(
                            "tutorial_first_battle",
                            StepFirstBattle,
                            "セミオートバトル",
                            "戦闘は探索隊が自動で進めます。あなたは流れを見て、必要な時に力を解放してください。",
                            "battle.skill_1")
                        : null;
                case StepFirstResult:
                    return IsScene(normalizedSceneName, "BattleScene")
                        ? new StoryTutorialEvent(
                            StoryFirstBattleWin,
                            StepFirstResult,
                            "探索報酬",
                            "契約片を回収しました。ゴールド、経験値、装備、仲間化結果を確認してから拠点へ戻りましょう。",
                            "result.return_home",
                            true)
                        : null;
                case StepWrapUp:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            "tutorial_wrap_up",
                            StepWrapUp,
                            "探索の基本",
                            "これで基本は大丈夫。ダンジョンは何度でも挑めます。勝てなくなったら、編成、装備、品質、強化を見直しましょう。",
                            blocksInput: true)
                        : null;
                default:
                    return null;
            }
        }

        private static StoryTutorialEvent GetChapterStoryEvent(PlayerProfile profile, string sceneName)
        {
            if (!profile.HasCompletedTutorial || !IsScene(sceneName ?? string.Empty, "HomeScene"))
            {
                return null;
            }

            if (profile.HighestFloor >= 5 && !HasSeenStory(profile, StoryChapter2Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter2Unlocked,
                    string.Empty,
                    "第2章 獣影の廃工廠",
                    "五つの小門が点灯し、廃工廠への転移門が開きました。次は編成と装備を整えて、暴走した生産炉を止めましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= 10 && !HasSeenStory(profile, StoryChapter3Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter3Unlocked,
                    string.Empty,
                    "第3章 古契約の地下書庫",
                    "廃工廠の生産炉が静まり、古い契約記録への道が現れました。ロックと品質を確かめ、失いたくない力を守りましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= 15 && !HasSeenStory(profile, StoryChapter4Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter4Unlocked,
                    string.Empty,
                    "第4章 紅蓮竜道",
                    "書庫の記録が、灼熱の竜道を指し示しています。契約核の配合と遺物強化を使い、上位の眷属に備えましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= 20 && !HasSeenStory(profile, StoryChapter5Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter5Unlocked,
                    string.Empty,
                    "第5章 星鉱の巨殿",
                    "紅蓮の奥で、星を含む鉱脈が脈動し始めました。高品質の装備を選び、探索隊全体の力を引き上げましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= 25 && !HasSeenStory(profile, StoryChapter6Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter6Unlocked,
                    string.Empty,
                    "第6章 深淵魔導回廊",
                    "星鉱の転移路が復旧し、契約網の深部へ続く回廊が現れました。この破損は事故ではない。答えを探しに行きましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= 30 && !HasSeenStory(profile, StoryFirstArcComplete))
            {
                return new StoryTutorialEvent(
                    StoryFirstArcComplete,
                    string.Empty,
                    "契約網の残響",
                    "全ての転移路がつながった瞬間、黒い契約炉があなたの名を呼びました。探索は終わりません。各ダンジョンを巡り、残された真相を追いましょう。",
                    "home.battle");
            }

            return null;
        }

        private static StoryTutorialEvent GetOptionalHint(PlayerProfile profile, string sceneName)
        {
            string normalizedSceneName = sceneName ?? string.Empty;
            if (IsScene(normalizedSceneName, "EquipmentScene") || IsScene(normalizedSceneName, "HomeScene"))
            {
                if (!HasSeenHint(profile, HintEquipment) && profile.OwnedEquipments.Count > 0)
                {
                    return new StoryTutorialEvent(
                        HintEquipment,
                        string.Empty,
                        "装備",
                        "ダンジョンで見つかる遺物は、眷属ごとに持たせられます。前に出る子には耐久、攻撃役には火力を伸ばす装備が向いています。",
                        "equipment.first_item");
                }

                if (!HasSeenHint(profile, HintEquipmentQuality) && HasHighQualityEquipment(profile))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentQuality,
                        string.Empty,
                        "遺物の品質",
                        "同じ名前の装備でも、品質が違うと力の伸び方が変わります。高品質な遺物ほど効果が高く、鍛えられる回数も多くなります。",
                        "equipment.quality_label");
                }

                if (!HasSeenHint(profile, HintEquipmentEnhance) && HasAnyEnhancementRelic(profile))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentEnhance,
                        string.Empty,
                        "装備強化",
                        "強化遺物を使うと、装備の刻印を深くできます。通常遺物は確実、上級遺物は挑戦向け、危険遺物は失敗時に装備を失います。",
                        "equipment.enhance_button");
                }
            }

            if (IsScene(normalizedSceneName, "HomeScene"))
            {
                if (!HasSeenHint(profile, HintFusion) && profile.OwnedMonsters.Count >= 2)
                {
                    return new StoryTutorialEvent(
                        HintFusion,
                        string.Empty,
                        "契約核の配合",
                        "2体の契約核を統合すると、記憶と力を継いだ新しい眷属が生まれます。親は戻らないので、ロックを確認してから行いましょう。",
                        "home.fusion");
                }

                if (!HasSeenHint(profile, HintDex) && profile.OwnedMonsters.Count > 0)
                {
                    return new StoryTutorialEvent(
                        HintDex,
                        string.Empty,
                        "契約図鑑",
                        "図鑑では、仲間にした眷属と未発見の系譜を確認できます。次に挑むダンジョンの編成を考える手掛かりになります。",
                        "home.dex");
                }

                if (!HasSeenHint(profile, HintShop))
                {
                    return new StoryTutorialEvent(
                        HintShop,
                        string.Empty,
                        "探索準備",
                        "ショップでは探索で集めたゴールドを育成素材と交換できます。行き詰まった時にのぞいてみましょう。",
                        "home.shop");
                }
            }

            return null;
        }

        private static void NormalizeTutorialState(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(profile.TutorialStepId))
            {
                profile.TutorialStepId = profile.HasCompletedTutorial ? CompleteStepId : StepWakeup;
            }

            if (profile.TutorialStepId == CompleteStepId)
            {
                profile.HasCompletedTutorial = true;
            }
        }

        private static bool HasHighQualityEquipment(PlayerProfile profile)
        {
            if (profile?.OwnedEquipments == null)
            {
                return false;
            }

            return profile.OwnedEquipments.Any(equipment => equipment != null && equipment.QualityRank >= 3);
        }

        private static bool HasAnyEnhancementRelic(PlayerProfile profile)
        {
            if (profile?.OwnedEnhancementRelics == null)
            {
                return false;
            }

            return profile.OwnedEnhancementRelics.Any(relic => relic != null && relic.Amount > 0);
        }

        private static void MarkClearedChapterStory(PlayerProfile profile, int requiredFloor, string eventId)
        {
            if (profile.HighestFloor >= requiredFloor)
            {
                MarkStorySeen(profile, eventId);
            }
        }

        private static bool IsScene(string sceneName, string expectedSceneName)
        {
            return string.Equals(sceneName, expectedSceneName, StringComparison.Ordinal);
        }

        private static bool HasSeen(List<string> seenIds, string id)
        {
            return !string.IsNullOrEmpty(id) && seenIds != null && seenIds.Contains(id);
        }

        private static bool MarkSeen(List<string> seenIds, string id)
        {
            if (seenIds == null || string.IsNullOrEmpty(id) || seenIds.Contains(id))
            {
                return false;
            }

            seenIds.Add(id);
            return true;
        }
    }
}
