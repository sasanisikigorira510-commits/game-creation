using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

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
        public const string StepFirstExplorationIntro = "T02A";
        public const string StepOpenFormation = "T03";
        public const string StepFirstFormation = "T04";
        public const string StepOpenBattle = "T05";
        public const string StepFirstBattle = "T06";
        public const string StepFirstResult = "T07";
        public const string StepWrapUp = "T08";

        public const string StoryPrologueWakeup = "story_prologue_wakeup";
        public const string StoryFirstExplorationIntro = "story_first_exploration_intro";
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
        public const string HintEquipmentGiftReceived = "tutorial_equipment_gift_received";
        public const string HintEquipmentQuality = "tutorial_equipment_quality";
        public const string HintEquipmentEnhance = "tutorial_equipment_enhance";
        public const string HintEquipmentEnhanceRelicReceived = "tutorial_equipment_enhance_relic_received";
        public const string HintEquipmentEnhanceReturnHome = "tutorial_equipment_enhance_return_home";
        public const string HintFusion = "tutorial_fusion";
        public const string HintFusionInheritance = "tutorial_fusion_inheritance";
        public const string HintFusionInheritanceGiftReceived = "tutorial_fusion_inheritance_gift_received";
        public const string HintDex = "tutorial_dex";
        public const string HintShop = "tutorial_shop";
        public const string HintHomeGuideComplete = "tutorial_home_guide_complete";

        public const int TutorialCompletionRewardFreeGachaStones = 600;
        public const int InitialSummonCount = 3;
        public const int InitialSummonStoneCost = 300;
        public const int Chapter2UnlockFloor = 10;
        public const int Chapter3UnlockFloor = 20;
        public const int Chapter4UnlockFloor = 30;
        public const int Chapter5UnlockFloor = 40;
        public const int Chapter6UnlockFloor = 50;
        public const int FirstArcCompleteFloor = 60;
        public const int FusionInheritanceTutorialUnlockFloor = Chapter3UnlockFloor;

        private const string DefaultStarterMonsterId = "monster_dragon_whelp";
        private const string HintTutorialCompletionReward = "tutorial_completion_reward_free_stones";
        public const string EquipmentTutorialGiftEquipmentId = "equip_apprentice_charm";
        public const string EquipmentEnhanceTutorialGiftRelicId = "relic_safe_ember";
        public const string FusionInheritanceTutorialGiftMonsterId = MonsterFusionCatalog.RockGolemId;
        private const string EquipmentTutorialGiftInstanceIdPrefix = "tutorial_gift_equipment_";
        private const string FusionInheritanceTutorialGiftInstanceIdPrefix = "tutorial_gift_fusion_rock_golem_";
        private const string FusionInheritanceTutorialGiftFirstInstanceIdPrefix = "tutorial_gift_fusion_rock_golem_a_";
        private const string FusionInheritanceTutorialGiftSecondInstanceIdPrefix = "tutorial_gift_fusion_rock_golem_b_";

        private static readonly Dictionary<string, string> NextStepByCompletedStep = new Dictionary<string, string>
        {
            { StepWakeup, StepOpenGacha },
            { StepOpenGacha, StepFirstSummon },
            { StepFirstSummon, StepFirstExplorationIntro },
            { StepFirstExplorationIntro, StepOpenFormation },
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

        public static bool HasFinishedHomeGuide(PlayerProfile profile)
        {
            return profile != null &&
                (HasSeenHint(profile, HintHomeGuideComplete) ||
                 HasSeenHint(profile, HintShop));
        }

        public static int GetInitialSummonRemainingCount(PlayerProfile profile)
        {
            int completedSummons = Math.Max(0, profile?.InitialTutorialSummonCount ?? 0);
            return Math.Max(0, InitialSummonCount - completedSummons);
        }

        public static bool HasCompletedInitialSummons(PlayerProfile profile)
        {
            return GetInitialSummonRemainingCount(profile) <= 0;
        }

        public static bool EnsureInitialSummonResources(PlayerProfile profile)
        {
            if (profile == null || profile.HasCompletedTutorial ||
                !string.Equals(profile.TutorialStepId, StepFirstSummon, StringComparison.Ordinal))
            {
                return false;
            }

            int requiredStones = GetInitialSummonRemainingCount(profile) * InitialSummonStoneCost;
            int missingStones = Math.Max(0, requiredStones - Math.Max(0, profile.FreeGachaStones));
            if (missingStones <= 0)
            {
                return false;
            }

            profile.AddFreeGachaStones(missingStones);
            return true;
        }

        public static bool MarkStorySeen(PlayerProfile profile, string eventId)
        {
            bool changed = MarkSeen(profile?.SeenStoryEventIds, eventId);
            if (eventId == StoryChapter3Unlocked)
            {
                changed |= EnsureFusionInheritanceTutorialGift(profile);
            }

            return changed;
        }

        public static bool MarkHintSeen(PlayerProfile profile, string hintId)
        {
            return MarkSeen(profile?.SeenTutorialHintIds, hintId);
        }

        public static bool EnsureEquipmentTutorialGift(PlayerProfile profile)
        {
            if (profile == null || HasSeenHint(profile, HintEquipment))
            {
                return false;
            }

            OwnedEquipmentData gift = FindEquipmentTutorialGift(profile);
            if (gift == null)
            {
                if (MasterDataManager.Instance?.GetEquipmentData(EquipmentTutorialGiftEquipmentId) == null)
                {
                    return false;
                }

                if (!profile.HasEquipmentStorageSpace())
                {
                    profile.EquipmentStorageLimit = Math.Max(profile.EquipmentStorageLimit, profile.OwnedEquipments.Count + 1);
                }

                gift = profile.AddOwnedEquipmentWithInstancePrefix(
                    EquipmentTutorialGiftEquipmentId,
                    EquipmentRarity.Common,
                    EquipmentTutorialGiftInstanceIdPrefix);
                if (gift == null)
                {
                    return false;
                }

                bool changed = true;
                changed |= MarkHintSeen(profile, HintEquipmentGiftReceived);
                return changed;
            }

            return MarkHintSeen(profile, HintEquipmentGiftReceived);
        }

        public static bool EnsureEquipmentEnhanceTutorialGift(PlayerProfile profile)
        {
            if (profile == null || HasSeenHint(profile, HintEquipmentEnhance))
            {
                return false;
            }

            bool changed = false;
            bool firstGrant = !HasSeenHint(profile, HintEquipmentEnhanceRelicReceived);
            if (firstGrant || profile.GetEnhancementRelicAmount(EquipmentEnhanceTutorialGiftRelicId) <= 0)
            {
                profile.AddEnhancementRelics(EquipmentEnhanceTutorialGiftRelicId, 1);
                changed = true;
            }

            changed |= MarkHintSeen(profile, HintEquipmentEnhanceRelicReceived);
            return changed;
        }

        public static bool EnsureFusionInheritanceTutorialGift(PlayerProfile profile)
        {
            if (profile == null || HasSeenHint(profile, HintFusionInheritance))
            {
                return false;
            }

            bool hasFirstGift = FindFusionInheritanceTutorialGift(profile, true) != null;
            bool hasSecondGift = FindFusionInheritanceTutorialGift(profile, false) != null;
            int missingGiftCount = (hasFirstGift ? 0 : 1) + (hasSecondGift ? 0 : 1);
            if (missingGiftCount > 0)
            {
                profile.MonsterStorageLimit = Math.Max(
                    profile.MonsterStorageLimit,
                    profile.OwnedMonsters.Count + missingGiftCount);
            }

            bool changed = false;
            if (!hasFirstGift)
            {
                changed |= AddFusionInheritanceTutorialGiftMonster(
                    profile,
                    FusionInheritanceTutorialGiftFirstInstanceIdPrefix,
                    1,
                    new MonsterIndividualValues(10, 30, 50, 10, 30, 50)) != null;
            }

            if (!hasSecondGift)
            {
                changed |= AddFusionInheritanceTutorialGiftMonster(
                    profile,
                    FusionInheritanceTutorialGiftSecondInstanceIdPrefix,
                    2,
                    new MonsterIndividualValues(50, 10, 10, 50, 10, 10)) != null;
            }

            changed |= MarkHintSeen(profile, HintFusionInheritanceGiftReceived);
            return changed;
        }

        public static bool TryGetFusionInheritanceTutorialGiftParents(
            PlayerProfile profile,
            out string firstParentInstanceId,
            out string secondParentInstanceId)
        {
            OwnedMonsterData firstGift = FindFusionInheritanceTutorialGift(profile, true);
            OwnedMonsterData secondGift = FindFusionInheritanceTutorialGift(profile, false);
            firstParentInstanceId = firstGift?.InstanceId ?? string.Empty;
            secondParentInstanceId = secondGift?.InstanceId ?? string.Empty;
            return !string.IsNullOrEmpty(firstParentInstanceId) &&
                !string.IsNullOrEmpty(secondParentInstanceId);
        }

        public static bool IsFusionInheritanceTutorialGift(OwnedMonsterData monster)
        {
            return monster != null &&
                !string.IsNullOrEmpty(monster.InstanceId) &&
                monster.InstanceId.StartsWith(FusionInheritanceTutorialGiftInstanceIdPrefix, StringComparison.Ordinal);
        }

        public static OwnedEquipmentData FindEquipmentTutorialGift(PlayerProfile profile)
        {
            return profile?.OwnedEquipments?
                .FirstOrDefault(equipment => IsEquipmentTutorialGift(equipment));
        }

        public static bool IsEquipmentTutorialGift(OwnedEquipmentData equipment)
        {
            return equipment != null &&
                !string.IsNullOrEmpty(equipment.InstanceId) &&
                equipment.InstanceId.StartsWith(EquipmentTutorialGiftInstanceIdPrefix, StringComparison.Ordinal);
        }

        private static bool HasEquippedEquipmentTutorialGift(PlayerProfile profile)
        {
            OwnedEquipmentData tutorialGift = FindEquipmentTutorialGift(profile);
            return tutorialGift != null && !string.IsNullOrEmpty(tutorialGift.EquippedMonsterInstanceId);
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

            MarkClearedChapterStory(profile, Chapter2UnlockFloor, StoryChapter2Unlocked);
            MarkClearedChapterStory(profile, Chapter3UnlockFloor, StoryChapter3Unlocked);
            MarkClearedChapterStory(profile, Chapter4UnlockFloor, StoryChapter4Unlocked);
            MarkClearedChapterStory(profile, Chapter5UnlockFloor, StoryChapter5Unlocked);
            MarkClearedChapterStory(profile, Chapter6UnlockFloor, StoryChapter6Unlocked);
            MarkClearedChapterStory(profile, FirstArcCompleteFloor, StoryFirstArcComplete);
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

            bool wasCompleted = profile.HasCompletedTutorial;
            bool changed = !profile.HasCompletedTutorial ||
                !string.Equals(profile.TutorialStepId, CompleteStepId, StringComparison.Ordinal);
            profile.HasCompletedTutorial = true;
            profile.TutorialStepId = CompleteStepId;
            if (!wasCompleted)
            {
                changed |= GrantTutorialCompletionReward(profile);
            }

            changed |= MarkMainTutorialCoveredHintsSeen(profile);
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
                            "序章 最後の契約炉",
                            "契約網は何者かに断ち切られ、仲間たちの記憶は各地のダンジョンへ散りました。\n最後の契約炉を目覚めさせ、失われた契約を取り戻しましょう。",
                            blocksInput: true)
                        : null;
                case StepOpenGacha:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            "tutorial_open_gacha",
                            StepOpenGacha,
                            "最初の召喚",
                            "魔晶石で最初の仲間を呼び戻せます。\n下の召喚から仲間を迎えましょう。",
                            "home.gacha",
                            true)
                        : null;
                case StepFirstSummon:
                    int remainingSummons = GetInitialSummonRemainingCount(profile);
                    return IsScene(normalizedSceneName, "GachaScene")
                        ? new StoryTutorialEvent(
                            "tutorial_first_summon",
                            StepFirstSummon,
                            "最初の探索隊",
                            $"今回の召喚用に、魔晶石を{InitialSummonStoneCost * InitialSummonCount}個用意しました。\n最初の探索隊として眷属を{InitialSummonCount}体呼び戻しましょう。（あと{remainingSummons}体）",
                            "gacha.single_free",
                            true)
                        : null;
                case StepFirstExplorationIntro:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            StoryFirstExplorationIntro,
                            StepFirstExplorationIntro,
                            "初回探索の目的",
                            "呼び戻した眷属が、見習いの五門洞から契約片の反応を感じています。\n契約網を復旧するには、ダンジョンへ向かい散らばった記憶を回収しなければなりません。",
                            blocksInput: true)
                        : null;
                case StepOpenFormation:
                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            StoryFirstSummonDone,
                            StepOpenFormation,
                            "最初の探索隊",
                            "呼び戻した3体を編成に入れて、探索隊として送り出しましょう。",
                            "home.formation",
                            true)
                        : null;
                case StepFirstFormation:
                    return IsScene(normalizedSceneName, "FormationScene")
                        ? new StoryTutorialEvent(
                            "tutorial_first_formation",
                            StepFirstFormation,
                            "探索隊編成",
                            "モンスターカード右下の「編成」ボタンをタップして、最初の3枠を埋めましょう。",
                            "formation.slot_1",
                            true)
                        : null;
                case StepOpenBattle:
                    if (IsScene(normalizedSceneName, "FormationScene"))
                    {
                        return new StoryTutorialEvent(
                            "tutorial_return_home_from_formation",
                            StepOpenBattle,
                            "編成完了",
                            "これで最初の探索隊が整いました。\n左上の「ホームへ戻る」から拠点へ戻り、冒険開始へ進みましょう。",
                            "formation.return_home",
                            true);
                    }

                    return IsScene(normalizedSceneName, "HomeScene")
                        ? new StoryTutorialEvent(
                            "tutorial_open_battle",
                            StepOpenBattle,
                            "最初の探索",
                            "探索隊の準備完了です。見習いの五門洞へ向かい、契約片を回収しましょう。",
                            "home.battle",
                            true)
                        : null;
                case StepFirstBattle:
                    if (HasFinishedHomeGuide(profile))
                    {
                        return null;
                    }

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
                    if (HasFinishedHomeGuide(profile))
                    {
                        return null;
                    }

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
                            "ルシェからの贈り物",
                            $"おつかれさまでした！これで基本は大丈夫です。\nチュートリアル完了報酬として、無料石{TutorialCompletionRewardFreeGachaStones}個をプレゼントします。召喚や育成の準備に使ってくださいね。",
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

            if (profile.HighestFloor >= Chapter2UnlockFloor && !HasSeenStory(profile, StoryChapter2Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter2Unlocked,
                    string.Empty,
                    "第2章 獣影の廃工廠",
                    "十の小門が点灯し、廃工廠への転移門が開きました。次は編成と装備を整えて、暴走した生産炉を止めましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= Chapter3UnlockFloor && !HasSeenStory(profile, StoryChapter3Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter3Unlocked,
                    string.Empty,
                    "第3章 古契約の地下書庫",
                    "廃工廠の生産炉が静まり、古い契約記録への道が現れました。ルシェが配合炉の教材を用意しています。個体値とプラス値の継承を確認してから地下書庫へ進みましょう。",
                    "home.fusion");
            }

            if (profile.HighestFloor >= Chapter4UnlockFloor && !HasSeenStory(profile, StoryChapter4Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter4Unlocked,
                    string.Empty,
                    "第4章 紅蓮竜道",
                    "書庫の記録が、灼熱の竜道を指し示しています。契約核の配合と遺物強化を使い、上位の眷属に備えましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= Chapter5UnlockFloor && !HasSeenStory(profile, StoryChapter5Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter5Unlocked,
                    string.Empty,
                    "第5章 星鉱の巨殿",
                    "紅蓮の奥で、星を含む鉱脈が脈動し始めました。高品質の装備を選び、探索隊全体の力を引き上げましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= Chapter6UnlockFloor && !HasSeenStory(profile, StoryChapter6Unlocked))
            {
                return new StoryTutorialEvent(
                    StoryChapter6Unlocked,
                    string.Empty,
                    "第6章 深淵魔導回廊",
                    "星鉱の転移路が復旧し、契約網の深部へ続く回廊が現れました。この破損は事故ではない。答えを探しに行きましょう。",
                    "home.battle");
            }

            if (profile.HighestFloor >= FirstArcCompleteFloor && !HasSeenStory(profile, StoryFirstArcComplete))
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
            if (IsScene(normalizedSceneName, "HomeScene") || IsScene(normalizedSceneName, "FusionScene"))
            {
                if (ShouldShowFusionInheritanceTutorial(profile))
                {
                    bool inFusionScene = IsScene(normalizedSceneName, "FusionScene");
                    return new StoryTutorialEvent(
                        HintFusionInheritance,
                        string.Empty,
                        "ルシェの配合レッスン",
                        inFusionScene
                            ? "教材として、最大レベルのロックゴーレムを2体用意しました。\n親1は+1で個体値10/30/50/10/30/50、親2は+2で50/10/10/50/10/10です。\n配合では個体値・親ステータスの一部・親のプラス値合計を継承します。"
                            : "地下書庫へ進む前に配合の継承を見ておきましょう。\n最大Lvのロックゴーレム2体を教材として用意しました。",
                        inFusionScene ? "fusion.guide" : "home.fusion");
                }
            }

            if (IsScene(normalizedSceneName, "EquipmentScene") || IsScene(normalizedSceneName, "HomeScene"))
            {
                if (!HasSeenHint(profile, HintEquipment) && profile.OwnedMonsters.Count > 0)
                {
                    OwnedEquipmentData tutorialGift = FindEquipmentTutorialGift(profile);
                    bool tutorialGiftEquipped = tutorialGift != null && !string.IsNullOrEmpty(tutorialGift.EquippedMonsterInstanceId);
                    string targetKey = tutorialGiftEquipped && IsScene(normalizedSceneName, "EquipmentScene")
                        ? "equipment.enhance_button"
                        : "equipment.auto_equip";
                    string body = IsScene(normalizedSceneName, "EquipmentScene")
                        ? tutorialGiftEquipped
                            ? "見習いの護符を装備できました。\nこのまま同じ装備カードの「強化」から、装備を鍛える流れも確認しましょう。"
                            : "ルシェから練習用の「見習いの護符」を受け取りました。画面上部の「自動装備」を押すと、選択中のモンスターに適した装備をまとめて持たせられます。"
                        : "ルシェが「見習いの護符」を用意しました。\n装備画面で「自動装備」を使って、モンスターに装備しましょう。";
                    return new StoryTutorialEvent(
                        HintEquipment,
                        string.Empty,
                        "装備の付け方",
                        body,
                        targetKey);
                }

                if (IsScene(normalizedSceneName, "EquipmentScene") &&
                    !HasSeenHint(profile, HintEquipmentEnhance) &&
                    HasEquippedEquipmentTutorialGift(profile) &&
                    HasEnhanceableEquipment(profile))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentEnhance,
                        string.Empty,
                        "装備強化",
                        "装備を鍛える練習をしましょう。強化画面でルシェが通常遺物を1つ渡すので、装備カードの「強化」から使ってみましょう。",
                        "equipment.enhance_button");
                }

                if (IsScene(normalizedSceneName, "EquipmentScene") &&
                    HasSeenHint(profile, HintEquipmentEnhance) &&
                    HasSeenHint(profile, HintEquipmentEnhanceRelicReceived) &&
                    HasSeenStory(profile, StoryFirstEquipmentEnhance) &&
                    !HasSeenHint(profile, HintEquipmentEnhanceReturnHome))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentEnhanceReturnHome,
                        string.Empty,
                        "強化完了",
                        "強化画面を閉じたら、左上の「ホームへ戻る」から拠点へ戻りましょう。",
                        "equipment.return_home");
                }

                if (!HasSeenHint(profile, HintEquipmentQuality) && HasHighQualityEquipment(profile))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentQuality,
                        string.Empty,
                        "遺物の品質",
                        "同じ名前の装備でも、品質が違うと力の伸び方が変わります。\n高品質な遺物ほど効果が高く、鍛えられる回数も多くなります。",
                        "equipment.quality_label");
                }

                if (!HasSeenHint(profile, HintEquipmentEnhance) && HasEnhanceableEquipment(profile))
                {
                    return new StoryTutorialEvent(
                        HintEquipmentEnhance,
                        string.Empty,
                        "装備強化",
                        "装備を鍛える練習をしましょう。強化画面でルシェが通常遺物を1つ渡すので、装備カードの「強化」から使ってみましょう。",
                        "equipment.enhance_button");
                }
            }

            if (IsScene(normalizedSceneName, "HomeScene") || IsScene(normalizedSceneName, "FusionScene"))
            {
                if (!HasSeenHint(profile, HintFusion) && profile.OwnedMonsters.Count >= 2)
                {
                    bool inFusionScene = IsScene(normalizedSceneName, "FusionScene");
                    return new StoryTutorialEvent(
                        HintFusion,
                        string.Empty,
                        "契約核の配合",
                        inFusionScene
                            ? "配合は親2体とも最大レベルが必要です。個体値は能力ごとに親1/親2からランダム継承し、高い個体値の親ほど良い値を引き継ぐ機会が増えます。親のプラス値を含む能力は継承ボーナスに反映されます。"
                            : "2体の契約核を統合すると、記憶と力を継いだ新しい眷属が生まれます。親は戻らないので、ロックを確認してから行いましょう。",
                        inFusionScene ? "fusion.guide" : "home.fusion");
                }
            }

            if (IsScene(normalizedSceneName, "HomeScene"))
            {
                if (!HasSeenHint(profile, HintDex) && profile.OwnedMonsters.Count > 0)
                {
                    return new StoryTutorialEvent(
                        HintDex,
                        string.Empty,
                        "契約図鑑",
                        "図鑑では、仲間にした眷属の能力・成長傾向を確認できます。\n次に挑むダンジョンの編成を考える手掛かりになります。",
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
                MarkMainTutorialCoveredHintsSeen(profile);
            }
        }

        private static bool MarkMainTutorialCoveredHintsSeen(PlayerProfile profile)
        {
            bool changed = false;
            changed |= MarkHintSeen(profile, HintFusion);
            return changed;
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

        private static bool HasEnhanceableEquipment(PlayerProfile profile)
        {
            if (profile?.OwnedEquipments == null)
            {
                return false;
            }

            return profile.OwnedEquipments.Any(equipment => equipment != null && equipment.RemainingEnhanceAttempts > 0);
        }

        private static bool ShouldShowFusionInheritanceTutorial(PlayerProfile profile)
        {
            return profile != null &&
                profile.HasCompletedTutorial &&
                profile.HighestFloor >= FusionInheritanceTutorialUnlockFloor &&
                !HasSeenHint(profile, HintFusionInheritance);
        }

        private static OwnedMonsterData FindFusionInheritanceTutorialGift(PlayerProfile profile, bool firstGift)
        {
            if (profile?.OwnedMonsters == null)
            {
                return null;
            }

            string prefix = firstGift
                ? FusionInheritanceTutorialGiftFirstInstanceIdPrefix
                : FusionInheritanceTutorialGiftSecondInstanceIdPrefix;
            return profile.OwnedMonsters.FirstOrDefault(monster =>
                monster != null &&
                !string.IsNullOrEmpty(monster.InstanceId) &&
                monster.InstanceId.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static OwnedMonsterData AddFusionInheritanceTutorialGiftMonster(
            PlayerProfile profile,
            string instanceIdPrefix,
            int plusValue,
            MonsterIndividualValues individualValues)
        {
            if (profile == null)
            {
                return null;
            }

            MasterDataManager.Instance?.Initialize();
            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(FusionInheritanceTutorialGiftMonsterId);
            int maxLevel = monsterData != null
                ? MonsterLevelService.GetMaxLevel(monsterData)
                : MonsterLevelService.GetMaxLevel(1);

            OwnedMonsterData gift = profile.AddOwnedMonster(FusionInheritanceTutorialGiftMonsterId, maxLevel, plusValue);
            if (gift == null)
            {
                return null;
            }

            gift.InstanceId = instanceIdPrefix + Guid.NewGuid().ToString("N");
            gift.Level = maxLevel;
            gift.Exp = 0;
            gift.IsFavorite = false;
            gift.IsLocked = false;
            ApplyMonsterPlusValue(gift, plusValue);
            MonsterIndividualValueService.Apply(gift, individualValues);
            return gift;
        }

        private static void ApplyMonsterPlusValue(OwnedMonsterData monster, int plusValue)
        {
            if (monster == null)
            {
                return;
            }

            int normalizedPlus = Math.Max(0, plusValue);
            monster.PlusValue = normalizedPlus;
            monster.PlusHp = normalizedPlus;
            monster.PlusAttack = normalizedPlus;
            monster.PlusWisdom = normalizedPlus;
            monster.PlusDefense = normalizedPlus;
            monster.PlusMagicDefense = normalizedPlus;
        }

        private static bool GrantTutorialCompletionReward(PlayerProfile profile)
        {
            if (profile == null || HasSeenHint(profile, HintTutorialCompletionReward))
            {
                return false;
            }

            profile.AddFreeGachaStones(TutorialCompletionRewardFreeGachaStones);
            return MarkHintSeen(profile, HintTutorialCompletionReward);
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
