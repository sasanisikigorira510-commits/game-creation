using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class MonsterFusionPanelController : MonoBehaviour
    {
        private sealed class FusionSlotView
        {
            public Text TitleLabel;
            public Text NameLabel;
            public Text DetailLabel;
            public Image Portrait;
            public Image Frame;
        }

        private const float RosterRowHeight = 242f;
        private int fusionTutorialGuidePage;
        private bool fusionTutorialPractice;
        private bool fusionTutorialCompleted;
        private int rosterSortMode;
        private Text rosterSortText;
        private GameObject fusionCompletionGuide;
        private const float RosterRowSpacing = 12f;
        private const string BackgroundSpritePath = "UI/FusionPage/FusionBackground";
        private const string MainFrameSpritePath = "UI/FusionPage/FusionMainFrame";
        private const string ParentSlotSpritePath = "UI/FusionPage/FusionParentSlot";
        private const string ResultSlotSpritePath = "UI/FusionPage/FusionResultSlot";
        private const string RosterFrameSpritePath = "UI/FusionPage/FusionRosterFrame";
        private const string ConfirmButtonSpritePath = "UI/FusionPage/FusionConfirmButton";
        private const string SmallButtonSpritePath = "UI/FusionPage/FusionSmallButton";
        private const string MagicCircleSpritePath = "UI/FusionPage/FusionMagicCircle";
        private const string SlotGlowSpritePath = "UI/FusionPage/Effects/FusionSlotGlow";
        private const string EnergyStreamSpriteBasePath = "UI/FusionPage/Effects/FusionEnergyStream_";
        private const string BirthBurstSpriteBasePath = "UI/FusionPage/Effects/FusionBirthBurst_";
        private const string ResultEffectNormalSpritePath = "UI/FusionPage/Effects/FusionResultEffect_Normal";
        private const string ResultEffectRareSpritePath = "UI/FusionPage/Effects/FusionResultEffect_Rare";
        private const string ResultEffectLegendarySpritePath = "UI/FusionPage/Effects/FusionResultEffect_Legendary";
        private const string ResultEffectFallbackSpritePath = "UI/FusionPage/FusionSuccessEffect";
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const int CeremonyFrameCount = 8;
        private const float BirthEffectDuration = 1.28f;

        private static readonly Vector2 FusionSlotSize = new Vector2(284f, 390f);
        private static readonly Vector2 ParentASlotPosition = new Vector2(-310f, -66f);
        private static readonly Vector2 ParentBSlotPosition = new Vector2(0f, -66f);
        private static readonly Vector2 ResultSlotPosition = new Vector2(310f, -66f);
        private static readonly Vector2 ParentAEffectPosition = new Vector2(-310f, -234f);
        private static readonly Vector2 ParentBEffectPosition = new Vector2(0f, -234f);
        private static readonly Vector2 ResultEffectPosition = new Vector2(310f, -234f);
        private static readonly Color PageTint = new Color(0.01f, 0.02f, 0.025f, 0.96f);
        private static readonly Color MainPanelColor = new Color(0.04f, 0.075f, 0.08f, 0.96f);
        private static readonly Color SlotColor = new Color(0.035f, 0.055f, 0.065f, 0.97f);
        private static readonly Color ResultSlotColor = new Color(0.055f, 0.075f, 0.04f, 0.98f);
        private static readonly Color RosterColor = new Color(0.018f, 0.035f, 0.043f, 0.97f);
        private static readonly Color AccentGold = new Color(1f, 0.76f, 0.32f, 1f);
        private static readonly Color TextMain = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextSub = new Color(0.78f, 0.88f, 0.92f, 0.95f);
        private static readonly Color TextPlateColor = new Color(0.005f, 0.010f, 0.012f, 0.78f);
        private static readonly Color TextPlateAccentColor = new Color(0.09f, 0.14f, 0.14f, 0.74f);
        private static readonly Color TextOutlineColor = new Color(0f, 0f, 0f, 0.88f);
        private static readonly Color WarningText = new Color(1f, 0.24f, 0.20f, 1f);
        private static readonly Color WarningPlateColor = new Color(0.22f, 0.025f, 0.02f, 0.84f);
        private static readonly Color RosterActionRailColor = new Color(0f, 0f, 0f, 0.30f);
        private static readonly Color ParentButtonColor = new Color(0.16f, 0.36f, 0.42f, 1f);
        private static readonly Color ParentButtonSelectedColor = new Color(0.8f, 0.54f, 0.16f, 1f);
        private static readonly Color FuseButtonColor = new Color(0.24f, 0.62f, 0.36f, 1f);

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly List<GameObject> rosterRows = new List<GameObject>();

        private Action onClosed;
        private Font runtimeFont;
        private RectTransform rosterContent;
        private GameObject selectionRoot;
        private GameObject resultStageRoot;
        private CanvasGroup resultStageCanvasGroup;
        private FusionSlotView parentASlot;
        private FusionSlotView parentBSlot;
        private FusionSlotView resultSlot;
        private Text statusLabel;
        private Image statusPlate;
        private Text rosterTitleLabel;
        private Button fuseButton;
        private GameObject fusionTutorialGuideRoot;
        private Text fusionTutorialGuideTitleText;
        private Text fusionTutorialGuideBodyText;
        private Text fusionTutorialGuideFooterText;
        private Text fusionTutorialGuideCloseButtonText;
        private Image fusionTutorialGuideCharacterImage;
        private string activeFusionTutorialEventId;
        private string parentAInstanceId;
        private string parentBInstanceId;
        private bool isBuilt;
        private bool previewCanFuse;
        private float ceremonyTime;
        private float birthEffectTimer;
        private Image magicCircleImage;
        private Image parentAGlowImage;
        private Image parentBGlowImage;
        private Image resultGlowImage;
        private Image streamAImage;
        private Image streamBImage;
        private Image birthBurstImage;
        private RectTransform magicCircleRect;
        private RectTransform parentAGlowRect;
        private RectTransform parentBGlowRect;
        private RectTransform resultGlowRect;
        private RectTransform streamARect;
        private RectTransform streamBRect;
        private RectTransform birthBurstRect;
        private Image resultStageEffectImage;
        private Image resultStageFlashImage;
        private Image resultStageParentAImage;
        private Image resultStageParentBImage;
        private Image resultStageParentALightImage;
        private Image resultStageParentBLightImage;
        private Image resultStageCoreImage;
        private Image resultStageBornImage;
        private Image resultStageBornShadowImage;
        private RectTransform resultStageEffectRect;
        private RectTransform resultStageParentARect;
        private RectTransform resultStageParentBRect;
        private RectTransform resultStageParentALightRect;
        private RectTransform resultStageParentBLightRect;
        private RectTransform resultStageCoreRect;
        private RectTransform resultStageBornRect;
        private Text resultStageTitleLabel;
        private Text resultStageSummaryLabel;
        private Text resultStageBornNameLabel;
        private Text resultStageClassLabel;
        private Text resultStageParentLabel;
        private Button resultStageNextButton;
        private Button resultStageCloseButton;
        private Sprite[] energyStreamSprites;
        private Sprite[] birthBurstSprites;
        private Coroutine resultStageRoutine;
        private bool fusionInProgress;
        private FusionCinematicPresentation fusionCinematic;
        private MonsterFusionResult pendingFusionResult;
        private MonsterDataSO pendingFusionParentA;
        private MonsterDataSO pendingFusionParentB;
        private string pendingFusionMessage;
        private bool resultAwaitingAcknowledgement;

        private enum FusionResultTier
        {
            Normal,
            Rare,
            Legendary
        }

        private void OnEnable()
        {
            // Some overlay owners reactivate their existing panel directly,
            // without calling Show again. Its cancelled cinematic still owns an
            // acknowledged-on-exit result, so never leave both stages hidden.
            if (isBuilt && resultAwaitingAcknowledgement && pendingFusionResult != null && !fusionInProgress)
                CompleteFusionResultStage();
        }

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            StopFusionCinematic();
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            fusionTutorialCompleted = false;
            fusionTutorialPractice = false;
            if (fusionCompletionGuide != null) fusionCompletionGuide.SetActive(false);
            fusionInProgress = false;
            if (selectionRoot != null)
            {
                selectionRoot.SetActive(true);
            }

            if (resultStageRoot != null)
            {
                resultStageRoot.SetActive(false);
            }

            ClearFusionResultStagePresentation();

            if (resultStageCanvasGroup != null)
            {
                resultStageCanvasGroup.blocksRaycasts = false;
                resultStageCanvasGroup.interactable = false;
            }

            parentAInstanceId = string.Empty;
            parentBInstanceId = string.Empty;
            previewCanFuse = false;
            birthEffectTimer = 0f;
            PrepareFusionInheritanceTutorialGift();
            RefreshRoster();
            RefreshPreview();
            if (TryRestoreFusionResult()) return;
            RefreshFusionTutorialGuide();
        }

        private void Update()
        {
            if (!isBuilt)
            {
                return;
            }

            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
            ceremonyTime += deltaTime;
            if (birthEffectTimer > 0f)
            {
                birthEffectTimer = Mathf.Max(0f, birthEffectTimer - deltaTime);
            }

            AnimateCeremonyEffects();
            AnimateFusionTutorialGuide();
        }

        private void Hide()
        {
            if (!fusionInProgress) AcknowledgeFusionResult();
            StopFusionCinematic();
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private void OnDisable()
        {
            // The transaction was saved before playback. Scene navigation only
            // cancels its presentation; reopening the page shows that same child.
            StopFusionCinematic();
        }

        private void OnDestroy()
        {
            StopFusionCinematic();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused || !fusionInProgress || pendingFusionResult == null) return;
            StopFusionCinematic();
            CompleteFusionResultStage();
        }

        private void Build()
        {
            ClearGeneratedChildren();
            runtimeFont = GetRuntimeFont();
            RectTransform rootRect = EnsureRootRect();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlay = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            overlay.color = PageTint;
            overlay.raycastTarget = false;

            selectionRoot = CreateUiObject("FusionSelectionRoot", transform);
            RectTransform selectionRect = selectionRoot.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = Vector2.zero;
            selectionRect.offsetMax = Vector2.zero;

            CreateFullScreenImage("FusionBackground", selectionRoot.transform, BackgroundSpritePath, new Color(0.015f, 0.03f, 0.035f, 0.98f));

            GameObject panel = CreatePanel("FusionMainPanel", selectionRoot.transform, MainFrameSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 1710f), MainPanelColor);

            Text titleLabel = CreateText("Title", panel.transform, "モンスター配合", 56, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -38f), new Vector2(640f, 76f), TextAnchor.MiddleCenter, AccentGold);
            AddTextContrast(titleLabel);

            CreateDecorationPanel("RuleHintPlate", panel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -124f), new Vector2(920f, 128f), WarningPlateColor);
            Text ruleWarningLabel = CreateText("RuleWarning", panel.transform, "配合には親2体とも最大レベルが必要です", 27, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -136f), new Vector2(900f, 40f), TextAnchor.MiddleCenter, WarningText);
            AddTextContrast(ruleWarningLabel);
            Text ruleHintLabel = CreateText("RuleHint", panel.transform, "同種族・同クラスはクラス3まで成長。クラス3同士はクラス3。\nクラス4以上：特殊配合優先／該当なしは高クラスの親（同クラスは親1）", 19, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -178f), new Vector2(900f, 64f), TextAnchor.MiddleCenter, TextSub);
            ruleHintLabel.resizeTextForBestFit = true;
            ruleHintLabel.resizeTextMinSize = 17;
            ruleHintLabel.resizeTextMaxSize = 19;
            AddTextContrast(ruleHintLabel);

            HomeReturnButtonStyle.Create(selectionRoot.transform, Hide);

            GameObject ritualPanel = CreatePanel("FusionRitualPanel", panel.transform, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -260f), new Vector2(940f, 800f), new Color(0.02f, 0.045f, 0.052f, 0.96f));

            CreateText("RitualTitle", ritualPanel.transform, "配合の間", 30, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(360f, 40f), TextAnchor.MiddleCenter, TextMain);

            CreateButton("InheritanceHelpButton", ritualPanel.transform, "個体値のしくみ",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(232f, 70f), SmallButtonSpritePath, ParentButtonColor,
                () => HelpDialog.Show(selectionRoot.transform, "InheritanceHelp", "個体値の継承",
                    "個体値はHP・攻撃・魔攻・防御・魔防・攻速の6項目にあり、各0〜100です。\n\n配合では項目ごとに、親1か親2の値を50%ずつの確率で引き継ぎます。\n\n例：親のHP個体値が80と40なら、子は80か40。平均の60にはなりません。高い値が必ず選ばれるわけでもありません。\n\nプラス値は別の仕組みで、親2体の合計を引き継ぎます。"));

            CreateCeremonyEffects(ritualPanel.transform);

            parentASlot = CreateFusionSlot("ParentASlot", ritualPanel.transform, "親1", ParentSlotSpritePath, SlotColor, ParentASlotPosition);
            parentBSlot = CreateFusionSlot("ParentBSlot", ritualPanel.transform, "親2", ParentSlotSpritePath, SlotColor, ParentBSlotPosition);
            resultSlot = CreateFusionSlot("ResultSlot", ritualPanel.transform, "誕生", ResultSlotSpritePath, ResultSlotColor, ResultSlotPosition);

            CreateText("FormulaText", ritualPanel.transform, "親1 + 親2  =>  配合結果", 24, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-210f, 172f), new Vector2(420f, 36f), TextAnchor.MiddleCenter, AccentGold);

            CreateButton("SwapButton", ritualPanel.transform, "親を入替",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220f, 32f), new Vector2(360f, 124f), SmallButtonSpritePath, ParentButtonColor, SwapParents);

            fuseButton = CreateButton("FuseButton", ritualPanel.transform, "配合する",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(220f, 32f), new Vector2(360f, 124f), ConfirmButtonSpritePath, FuseButtonColor, FuseSelectedParents).GetComponent<Button>();

            statusPlate = CreateDecorationPanel("StatusPlate", panel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -754f), new Vector2(910f, 88f), WarningPlateColor).GetComponent<Image>();
            if (statusPlate != null)
            {
                statusPlate.gameObject.SetActive(false);
            }

            statusLabel = CreateText("StatusLabel", panel.transform, string.Empty, 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -754f), new Vector2(880f, 82f), TextAnchor.MiddleCenter, new Color(0.94f, 0.98f, 1f, 0.96f));
            AddTextContrast(statusLabel);

            GameObject rosterPanel = CreatePanel("FusionRosterPanel", panel.transform, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1080f), new Vector2(940f, 670f), RosterColor);

            rosterTitleLabel = CreateText("RosterTitle", rosterPanel.transform, "所持モンスター", 29, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150f, -38f), new Vector2(520f, 42f), TextAnchor.MiddleCenter, TextMain);

            var sortButton = CreateButton("RosterSortButton", rosterPanel.transform, "並び替え: クラス",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -18f), new Vector2(300f, 104f), SmallButtonSpritePath, ParentButtonColor, CycleRosterSort);
            rosterSortText = sortButton.transform.Find("Label")?.GetComponent<Text>();

            GameObject viewport = CreatePanel("Viewport", rosterPanel.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(880f, 512f), new Color(0f, 0f, 0f, 0.24f));
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateUiObject("Content", viewport.transform);
            rosterContent = content.GetComponent<RectTransform>();
            rosterContent.anchorMin = new Vector2(0f, 1f);
            rosterContent.anchorMax = new Vector2(1f, 1f);
            rosterContent.pivot = new Vector2(0.5f, 1f);
            rosterContent.anchoredPosition = Vector2.zero;
            rosterContent.sizeDelta = Vector2.zero;

            ScrollRect scrollRect = rosterPanel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = rosterContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            CreateResultStage();
            BuildFusionTutorialGuide(panel.transform);

            isBuilt = true;
            AnimateCeremonyEffects();
        }

        private void BuildFusionTutorialGuide(Transform panelTransform)
        {
            if (panelTransform == null || fusionTutorialGuideRoot != null)
            {
                return;
            }

            fusionTutorialGuideRoot = CreatePanel("FusionTutorialGuideRoot", panelTransform, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                // The inheritance lesson contains several lines of Japanese
                // guidance.  Leave enough vertical room for the body and the
                // next-action row without shrinking the text to a blur.
                new Vector2(0f, -570f), new Vector2(980f, 620f), new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Outline panelOutline = fusionTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            fusionTutorialGuideCharacterImage = CreatePortraitImage(
                fusionTutorialGuideRoot.transform,
                "FusionTutorialGuideLuse",
                TutorialGuideSpritePath,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(16f, 32f),
                new Vector2(250f, 300f));

            Text badgeText = CreateText("FusionTutorialGuideBadge", fusionTutorialGuideRoot.transform, "TUTORIAL", 20, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(300f, -22f), new Vector2(136f, 28f), TextAnchor.MiddleCenter, AccentGold);
            AddTextContrast(badgeText);

            fusionTutorialGuideTitleText = CreateText("FusionTutorialGuideTitle", fusionTutorialGuideRoot.transform, "ルシェの配合レッスン", 40, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(290f, -66f), new Vector2(650f, 56f), TextAnchor.MiddleLeft, new Color(1f, 0.96f, 0.78f, 1f));
            AddTextContrast(fusionTutorialGuideTitleText);

            fusionTutorialGuideBodyText = CreateText("FusionTutorialGuideBody", fusionTutorialGuideRoot.transform, string.Empty, 30, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(290f, -144f), new Vector2(650f, 300f), TextAnchor.UpperLeft, new Color(0.96f, 0.95f, 0.88f, 1f));
            fusionTutorialGuideBodyText.resizeTextForBestFit = false;
            fusionTutorialGuideBodyText.fontSize = 34;
            AddTextContrast(fusionTutorialGuideBodyText);

            fusionTutorialGuideFooterText = CreateText("FusionTutorialGuideFooter", fusionTutorialGuideRoot.transform, "次の操作: 説明を確認してホームへ戻る", 24, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(290f, 120f), new Vector2(650f, 52f), TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 1f, 1f));
            AddTextContrast(fusionTutorialGuideFooterText);

            GameObject guideCloseButton = CreateButton("FusionTutorialGuideClose", fusionTutorialGuideRoot.transform, "ホームへ戻る",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-26f, 20f), new Vector2(260f, 90f), ConfirmButtonSpritePath, ParentButtonColor, CompleteFusionTutorialGuide);
            fusionTutorialGuideCloseButtonText = guideCloseButton.transform.Find("Label")?.GetComponent<Text>();

            fusionTutorialGuideRoot.SetActive(false);
        }

        private void PrepareFusionInheritanceTutorialGift()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null ||
                !profile.HasCompletedTutorial ||
                profile.HighestFloor < StoryTutorialService.FusionInheritanceTutorialUnlockFloor ||
                StoryTutorialService.HasSeenHint(profile, StoryTutorialService.HintFusionInheritance))
            {
                return;
            }

            bool changed = StoryTutorialService.EnsureFusionInheritanceTutorialGift(profile);
            if (StoryTutorialService.TryGetFusionInheritanceTutorialGiftParents(profile, out string firstParentId, out string secondParentId))
            {
                parentAInstanceId = firstParentId;
                parentBInstanceId = secondParentId;
            }

            if (changed && Application.isPlaying)
            {
                SaveManager.Instance?.SaveCurrentGame();
            }
        }

        private void RefreshFusionTutorialGuide()
        {
            if (fusionTutorialGuideRoot == null)
            {
                return;
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            StoryTutorialEvent tutorialEvent = StoryTutorialService.GetNextEvent(profile, "FusionScene");
            bool isInheritanceLesson = tutorialEvent != null &&
                string.Equals(
                    tutorialEvent.EventId,
                    StoryTutorialService.HintFusionInheritance,
                    StringComparison.Ordinal);
            bool hasInheritanceGiftParents = !isInheritanceLesson ||
                StoryTutorialService.TryGetFusionInheritanceTutorialGiftParents(
                    profile,
                    out _,
                    out _);
            bool shouldShow = selectionRoot != null &&
                selectionRoot.activeSelf &&
                tutorialEvent != null &&
                string.Equals(tutorialEvent.TargetKey, "fusion.guide", StringComparison.Ordinal) &&
                hasInheritanceGiftParents;

            fusionTutorialGuideRoot.SetActive(shouldShow);
            if (!shouldShow)
            {
                activeFusionTutorialEventId = string.Empty;
                return;
            }

            fusionTutorialGuideRoot.transform.SetAsLastSibling();
            if (activeFusionTutorialEventId != tutorialEvent.EventId) fusionTutorialGuidePage = 0;
            activeFusionTutorialEventId = tutorialEvent.EventId;
            if (fusionTutorialGuideTitleText != null)
            {
                fusionTutorialGuideTitleText.text = $"配合レッスン {fusionTutorialGuidePage + 1}/3";
            }

            if (fusionTutorialGuideBodyText != null)
            {
                fusionTutorialGuideBodyText.text = fusionTutorialGuidePage == 0
                    ? "配合には、親2体とも\nレベルMAXが必要です。\n一覧の「親1」「親2」で選びます。\n" + (isInheritanceLesson ? "練習用のロックゴーレム2体を用意\nしました。" : "レベルが足りない仲間は、\n戦闘で育ててから選びましょう。")
                    : fusionTutorialGuidePage == 1
                        ? "プラス値は親2体の合計を\n引き継ぎます。\n例：親1が +1、親2が +2なら、\n子は +3です。\n個体値は能力毎に50％の確率で\n親１か親２どちらかの値を継承します。"
                        : "配合すると親2体は消費され、\n子1体がレベル1で誕生します。\n親の能力の一部も継承します。\n残したい仲間でないか確認してから、\n「配合する」を押しましょう。";
            }

            if (fusionTutorialGuideFooterText != null)
            {
                fusionTutorialGuideFooterText.text = fusionTutorialGuidePage < 2 ? "「次へ」で、引き継ぎと注意点を確認しましょう。"
                    : isInheritanceLesson ? "次は、用意した親2体の配合を試してみましょう。"
                    : "今回は説明を確認したらホームへ戻りましょう。";
            }

            if (fusionTutorialGuideCloseButtonText != null)
            {
                fusionTutorialGuideCloseButtonText.text = fusionTutorialGuidePage < 2 ? "次へ" : isInheritanceLesson ? "やってみる" : "ホームへ戻る";
            }
        }

        private void CompleteFusionTutorialGuide()
        {
            if (fusionTutorialGuidePage < 2)
            {
                fusionTutorialGuidePage++;
                RefreshFusionTutorialGuide();
                return;
            }
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            bool isInheritanceLesson = string.Equals(
                activeFusionTutorialEventId,
                StoryTutorialService.HintFusionInheritance,
                StringComparison.Ordinal);
            bool changed = !isInheritanceLesson && StoryTutorialService.MarkHintSeen(profile, StoryTutorialService.HintFusion);
            if (changed && Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            if (isInheritanceLesson)
            {
                fusionTutorialPractice = true;
                fusionTutorialGuideRoot.SetActive(false);
                if (StoryTutorialService.TryGetFusionInheritanceTutorialGiftParents(profile, out string firstParentId, out string secondParentId))
                {
                    parentAInstanceId = firstParentId;
                    parentBInstanceId = secondParentId;
                }

                RefreshRoster();
                RefreshPreview();
                return;
            }

            Hide();
        }

        private void AnimateFusionTutorialGuide()
        {
            if (fusionTutorialGuideRoot == null || !fusionTutorialGuideRoot.activeInHierarchy || fusionTutorialGuideCharacterImage == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            float scale = Mathf.Lerp(0.985f, 1.035f, pulse);
            fusionTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private FusionSlotView CreateFusionSlot(string name, Transform parent, string title, string spritePath, Color color, Vector2 anchoredPosition)
        {
            GameObject slotObject = CreatePanel(name, parent, spritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                anchoredPosition, FusionSlotSize, color);

            FusionSlotView slot = new FusionSlotView();
            slot.Frame = slotObject.GetComponent<Image>();
            CreateDecorationPanel("TitlePlate", slotObject.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(144f, 34f), TextPlateAccentColor);
            slot.TitleLabel = CreateText("TitleLabel", slotObject.transform, title, 25, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(220f, 32f), TextAnchor.MiddleCenter, AccentGold);
            AddTextContrast(slot.TitleLabel);

            GameObject portraitPanel = CreatePanel("PortraitPanel", slotObject.transform, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(132f, 132f), new Color(0.01f, 0.018f, 0.022f, 0.92f));

            GameObject portraitObject = CreateUiObject("Portrait", portraitPanel.transform);
            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(118f, 118f);
            slot.Portrait = portraitObject.AddComponent<Image>();
            slot.Portrait.preserveAspect = true;
            slot.Portrait.raycastTarget = false;

            CreateDecorationPanel("NamePlate", slotObject.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -198f), new Vector2(254f, 54f), TextPlateColor);
            slot.NameLabel = CreateText("NameLabel", slotObject.transform, "未選択", 25, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -202f), new Vector2(244f, 46f), TextAnchor.MiddleCenter, TextMain);
            slot.NameLabel.resizeTextForBestFit = true;
            slot.NameLabel.resizeTextMinSize = 18;
            slot.NameLabel.resizeTextMaxSize = 25;
            slot.NameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            AddTextContrast(slot.NameLabel);

            CreateDecorationPanel("DetailPlate", slotObject.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -262f), new Vector2(254f, 104f), TextPlateColor);
            slot.DetailLabel = CreateText("DetailLabel", slotObject.transform, "-", 19, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -268f), new Vector2(244f, 92f), TextAnchor.MiddleCenter, TextSub);
            slot.DetailLabel.resizeTextForBestFit = true;
            slot.DetailLabel.resizeTextMinSize = 15;
            slot.DetailLabel.resizeTextMaxSize = 19;
            slot.DetailLabel.verticalOverflow = VerticalWrapMode.Truncate;
            AddTextContrast(slot.DetailLabel);

            return slot;
        }

        private void CreateResultStage()
        {
            resultStageRoot = CreateUiObject("FusionResultStageRoot", transform);
            RectTransform rootRect = resultStageRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            resultStageCanvasGroup = resultStageRoot.AddComponent<CanvasGroup>();
            resultStageCanvasGroup.alpha = 0f;
            resultStageCanvasGroup.blocksRaycasts = false;
            resultStageCanvasGroup.interactable = false;

            CreateFullScreenImage("FusionResultBackground", resultStageRoot.transform, BackgroundSpritePath, new Color(0.006f, 0.012f, 0.015f, 1f));
            CreatePanel("FusionResultVeil", resultStageRoot.transform, null,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.50f));
            CreatePanel("FusionResultBottomShade", resultStageRoot.transform, null,
                Vector2.zero, Vector2.right, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 620f), new Color(0f, 0f, 0f, 0.66f));

            resultStageFlashImage = CreatePanel("FusionResultFlash", resultStageRoot.transform, null,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            resultStageFlashImage.raycastTarget = false;

            resultStageTitleLabel = CreateText("ResultStageTitle", resultStageRoot.transform, "配合陣 起動", 50, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -142f), new Vector2(820f, 68f), TextAnchor.MiddleCenter, AccentGold);
            resultStageSummaryLabel = CreateText("ResultStageSummary", resultStageRoot.transform, "親の因子を結合中", 25, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -212f), new Vector2(860f, 46f), TextAnchor.MiddleCenter, TextSub);

            resultStageEffectImage = CreateEffectImage("FusionResultEffect", resultStageRoot.transform, ResultEffectFallbackSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -94f), new Vector2(1040f, 1040f), true);
            resultStageEffectRect = resultStageEffectImage.GetComponent<RectTransform>();

            resultStageParentALightImage = CreateEffectImage("FusionResultParentALight", resultStageRoot.transform, SlotGlowSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360f, -126f), new Vector2(390f, 390f), true);
            resultStageParentALightRect = resultStageParentALightImage.GetComponent<RectTransform>();

            resultStageParentBLightImage = CreateEffectImage("FusionResultParentBLight", resultStageRoot.transform, SlotGlowSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(360f, -126f), new Vector2(390f, 390f), true);
            resultStageParentBLightRect = resultStageParentBLightImage.GetComponent<RectTransform>();

            resultStageParentAImage = CreateEffectImage("FusionResultParentA", resultStageRoot.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360f, -126f), new Vector2(330f, 330f), true);
            resultStageParentARect = resultStageParentAImage.GetComponent<RectTransform>();

            resultStageParentBImage = CreateEffectImage("FusionResultParentB", resultStageRoot.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(360f, -126f), new Vector2(330f, 330f), true);
            resultStageParentBRect = resultStageParentBImage.GetComponent<RectTransform>();

            resultStageCoreImage = CreateEffectImage("FusionResultCore", resultStageRoot.transform, SlotGlowSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -126f), new Vector2(500f, 500f), true);
            resultStageCoreRect = resultStageCoreImage.GetComponent<RectTransform>();

            resultStageBornShadowImage = CreateEffectImage("FusionResultBornShadow", resultStageRoot.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(24f, -156f), new Vector2(560f, 560f), true);
            resultStageBornImage = CreateEffectImage("FusionResultBornMonster", resultStageRoot.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -144f), new Vector2(570f, 570f), true);
            resultStageBornRect = resultStageBornImage.GetComponent<RectTransform>();

            resultStageBornNameLabel = CreateText("ResultBornName", resultStageRoot.transform, string.Empty, 42, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -468f), new Vector2(860f, 66f), TextAnchor.MiddleCenter, TextMain);
            resultStageClassLabel = CreateText("ResultBornClass", resultStageRoot.transform, string.Empty, 29, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -528f), new Vector2(760f, 44f), TextAnchor.MiddleCenter, AccentGold);
            resultStageParentLabel = CreateText("ResultParents", resultStageRoot.transform, string.Empty, 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -590f), new Vector2(860f, 70f), TextAnchor.MiddleCenter, TextSub);

            resultStageNextButton = CreateButton("ResultNextFusionButton", resultStageRoot.transform, "次の配合へ",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-184f, 92f), new Vector2(330f, 82f), ConfirmButtonSpritePath, FuseButtonColor, ReturnToSelectionScreen).GetComponent<Button>();
            resultStageCloseButton = HomeReturnButtonStyle.Create(resultStageRoot.transform, "ResultHomeReturnButton", Hide);

            SetResultStageButtonsVisible(false);
            resultStageRoot.SetActive(false);
        }

        private void CycleRosterSort()
        {
            rosterSortMode = (rosterSortMode + 1) % 4;
            if (rosterSortText != null) rosterSortText.text = "並び替え: " + new[] { "クラス", "レベル", "プラス値", "入手順" }[rosterSortMode];
            rosterContent.anchoredPosition = Vector2.zero;
            RefreshRoster();
        }

        private void RefreshRoster()
        {
            foreach (GameObject row in rosterRows)
            {
                if (row != null)
                {
                    DestroyObject(row);
                }
            }

            rosterRows.Clear();
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            if (profile == null || profile.OwnedMonsters == null || masterDataManager == null)
            {
                SetStatus("所持モンスター情報を読み込めませんでした。");
                return;
            }

            var rosterEntries = profile.OwnedMonsters
                .Where(monster => monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                .Select(monster => new
                {
                    Monster = monster,
                    Data = ResolveMonsterData(masterDataManager, monster)
                })
                .Where(entry => entry.Data != null)
                .OrderByDescending(entry => rosterSortMode == 0 ? entry.Data.classRank : rosterSortMode == 1 ? entry.Monster.Level : rosterSortMode == 2 ? entry.Monster.TotalPlusValue : entry.Monster.AcquiredOrder)
                .ThenBy(entry => entry.Data.raceId ?? string.Empty)
                .ThenByDescending(entry => entry.Monster.AcquiredOrder)
                .ToList();

            if (rosterTitleLabel != null)
            {
                rosterTitleLabel.text = $"所持モンスター  {rosterEntries.Count}/{Mathf.Max(1, profile.MonsterStorageLimit)}";
            }

            float contentHeight = Mathf.Max(0f, rosterEntries.Count * (RosterRowHeight + RosterRowSpacing));
            rosterContent.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < rosterEntries.Count; i += 1)
            {
                GameObject row = CreateRosterRow(rosterEntries[i].Monster, rosterEntries[i].Data, i);
                rosterRows.Add(row);
            }
        }

        private GameObject CreateRosterRow(OwnedMonsterData monster, MonsterDataSO monsterData, int index)
        {
            bool isParentA = monster.InstanceId == parentAInstanceId;
            bool isParentB = monster.InstanceId == parentBInstanceId;
            Color rowColor = isParentA || isParentB
                ? new Color(0.16f, 0.13f, 0.06f, 1f)
                : new Color(0.05f, 0.075f, 0.085f, 1f);

            GameObject row = CreatePanel("FusionMonsterRow_" + index, rosterContent, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -index * (RosterRowHeight + RosterRowSpacing)), new Vector2(0f, RosterRowHeight), rowColor);
            Button detailButton = row.AddComponent<Button>();
            Image rowImage = row.GetComponent<Image>();
            if (rowImage != null)
            {
                rowImage.raycastTarget = true;
            }

            detailButton.targetGraphic = rowImage;
            detailButton.onClick.AddListener(() => ShowMonsterDetail(monster, monsterData));

            string classText = monsterData != null ? "C" + Mathf.Max(1, monsterData.classRank) : "C?";
            string raceText = monsterData != null ? ResolveRaceName(monsterData.raceId) : "不明";
            string favorite = monster.IsFavorite ? "  ★" : string.Empty;
            string locked = monster.IsLocked ? "  [ロック]" : string.Empty;
            string selected = isParentA ? "  [親1]" : isParentB ? "  [親2]" : string.Empty;
            string displayName = monsterData != null ? monsterData.monsterName : monster.MonsterId;
            string damageText = monsterData != null ? ResolveDamageTypeLabel(monsterData.damageType) : "型不明";

            Image thumbnail = CreatePortraitImage(row.transform, "Thumbnail", GetPortraitResourcePath(monsterData),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(92f, 92f));

            if (thumbnail.sprite == null)
            {
                thumbnail.color = new Color(0.1f, 0.14f, 0.16f, 0.9f);
            }

            Text rowNameLabel = CreateText("Name", row.transform, $"{displayName}{favorite}{locked}{selected}", 22, FontStyle.Bold,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(126f, 80f), new Vector2(430f, 34f), TextAnchor.MiddleLeft, TextMain);
            rowNameLabel.resizeTextForBestFit = true;
            rowNameLabel.resizeTextMinSize = 18;
            rowNameLabel.resizeTextMaxSize = 22;

            Text rowSubLabel = CreateText("Sub", row.transform, $"{BuildMonsterLevelProgressText(monster, monsterData)} / IV{MonsterIndividualValueService.GetAverage(monster)} / {raceText} / {classText} / {damageText}{BuildFusionBonusShort(monster)}", 18, FontStyle.Bold,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(126f, 24f), new Vector2(430f, 66f), TextAnchor.MiddleLeft, TextSub);
            rowSubLabel.resizeTextForBestFit = true;
            rowSubLabel.resizeTextMinSize = 16;
            rowSubLabel.resizeTextMaxSize = 18;
            CreateText("IndividualStats", row.transform,
                $"個体値：HP {monster.IndividualHp} ／ 攻撃 {monster.IndividualAttack} ／ 魔攻 {monster.IndividualWisdom}\n　　　　防御 {monster.IndividualDefense} ／ 魔防 {monster.IndividualMagicDefense} ／ 攻速 {monster.IndividualAttackSpeed}",
                24, FontStyle.Bold, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(126f, 12f), new Vector2(770f, 62f), TextAnchor.UpperLeft, TextSub);

            CreateDecorationPanel("ActionRail", row.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 40f), new Vector2(306f, 146f), RosterActionRailColor);

            CreateButton("ParentAButton", row.transform, "親1",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-160f, 40f), new Vector2(138f, 138f), SmallButtonSpritePath, isParentA ? ParentButtonSelectedColor : ParentButtonColor,
                () => SelectParent(monster.InstanceId, true));

            CreateButton("ParentBButton", row.transform, "親2",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 40f), new Vector2(138f, 138f), SmallButtonSpritePath, isParentB ? ParentButtonSelectedColor : ParentButtonColor,
                () => SelectParent(monster.InstanceId, false));

            return row;
        }

        private void SelectParent(string instanceId, bool asParentA)
        {
            if (IsFusionTutorialGuideActive())
            {
                return;
            }

            if (asParentA)
            {
                parentAInstanceId = instanceId;
                if (parentBInstanceId == instanceId)
                {
                    parentBInstanceId = string.Empty;
                }
            }
            else
            {
                parentBInstanceId = instanceId;
                if (parentAInstanceId == instanceId)
                {
                    parentAInstanceId = string.Empty;
                }
            }

            RefreshRoster();
            RefreshPreview();
        }

        private void ShowMonsterDetail(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MonsterStatusDetailPopup.Show(transform, profile, monster, monsterData);
        }

        private void SwapParents()
        {
            if (IsFusionTutorialGuideActive())
            {
                return;
            }

            (parentAInstanceId, parentBInstanceId) = (parentBInstanceId, parentAInstanceId);
            RefreshRoster();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            OwnedMonsterData parentA = profile?.GetOwnedMonster(parentAInstanceId);
            OwnedMonsterData parentB = profile?.GetOwnedMonster(parentBInstanceId);
            MonsterDataSO parentDataA = ResolveMonsterData(masterDataManager, parentA);
            MonsterDataSO parentDataB = ResolveMonsterData(masterDataManager, parentB);
            if (parentA != null && parentDataA == null)
            {
                parentAInstanceId = string.Empty;
                parentA = null;
            }

            if (parentB != null && parentDataB == null)
            {
                parentBInstanceId = string.Empty;
                parentB = null;
            }

            BindSlot(parentASlot, parentA, parentDataA, "親1", "未選択");
            BindSlot(parentBSlot, parentB, parentDataB, "親2", "未選択");

            MonsterFusionResult preview = MonsterFusionService.PreviewFusion(profile, parentAInstanceId, parentBInstanceId, masterDataManager);
            bool hasProtectedParent = HasProtectedParent(profile);
            previewCanFuse = preview.CanFuse && !hasProtectedParent;
            fuseButton.interactable = previewCanFuse;
            TutorialTargetFrame.SetVisible(fuseButton.transform, fusionTutorialPractice && previewCanFuse && !fusionTutorialCompleted, "ここをタップ");

            if (preview.CanFuse)
            {
                BindMysteryResultSlot(true);
                SetStatus(hasProtectedParent
                    ? "お気に入りまたはロック中の親は配合できません。必要なら先に保護を外してください。"
                    : preview.Message);
                AnimateCeremonyEffects();
                return;
            }

            BindMysteryResultSlot(false);
            SetStatus(preview.Message);
            AnimateCeremonyEffects();
        }

        private void BindSlot(FusionSlotView slot, OwnedMonsterData monster, MonsterDataSO monsterData, string title, string emptyName)
        {
            if (slot == null)
            {
                return;
            }

            slot.TitleLabel.text = title;
            if (monster == null || monsterData == null)
            {
                slot.NameLabel.text = emptyName;
                slot.DetailLabel.text = "親を選択\n\n";
                SetPortrait(slot.Portrait, null);
                return;
            }

            slot.NameLabel.text = monsterData.monsterName;
            slot.DetailLabel.text = BuildFusionSlotDetailText(monster, monsterData);
            SetPortrait(slot.Portrait, GetPortraitResourcePath(monsterData));
        }

        private void BindMysteryResultSlot(bool isReady)
        {
            if (resultSlot == null)
            {
                return;
            }

            resultSlot.TitleLabel.text = "誕生";
            resultSlot.NameLabel.text = "？？？";
            resultSlot.DetailLabel.text = isReady
                ? "配合可能\n誕生は儀式後に判明\n継承あり"
                : "親2体を選択\n誕生は儀式後に判明\n";
            SetPortrait(resultSlot.Portrait, null);
        }

        private void FuseSelectedParents()
        {
            if (fusionInProgress || resultAwaitingAcknowledgement || IsFusionTutorialGuideActive())
            {
                return;
            }

            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            bool isInheritanceTutorialRun = string.Equals(
                activeFusionTutorialEventId,
                StoryTutorialService.HintFusionInheritance,
                StringComparison.Ordinal);
            if (isInheritanceTutorialRun &&
                StoryTutorialService.TryGetFusionInheritanceTutorialGiftParents(
                    profile,
                    out string tutorialParentA,
                    out string tutorialParentB) &&
                (!string.Equals(parentAInstanceId, tutorialParentA, StringComparison.Ordinal) ||
                    !string.Equals(parentBInstanceId, tutorialParentB, StringComparison.Ordinal)))
            {
                // The inheritance lesson must use the two supplied tutorial
                // golems.  Prevent an old roster selection (for example the
                // starter mage) from becoming the lesson result.
                parentAInstanceId = tutorialParentA;
                parentBInstanceId = tutorialParentB;
                RefreshRoster();
                RefreshPreview();
                SetStatus("教材のロックゴーレム2体を親に選んでいます。\nこの2体で配合を実行してください。");
                return;
            }

            OwnedMonsterData parentA = profile?.GetOwnedMonster(parentAInstanceId);
            OwnedMonsterData parentB = profile?.GetOwnedMonster(parentBInstanceId);
            MonsterDataSO parentDataA = ResolveMonsterData(masterDataManager, parentA);
            MonsterDataSO parentDataB = ResolveMonsterData(masterDataManager, parentB);
            MonsterFusionResult preview = MonsterFusionService.PreviewFusion(
                profile,
                parentAInstanceId,
                parentBInstanceId,
                masterDataManager);
            if (!preview.CanFuse)
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                RefreshPreview();
                SetStatus(preview.Message);
                return;
            }

            if (isInheritanceTutorialRun &&
                (preview.Recipe == null ||
                    string.Equals(preview.Recipe.ResultMonsterId, MonsterFusionCatalog.ApprenticeMageId, StringComparison.Ordinal)))
            {
                // This is a safety net for stale/corrupt tutorial state.  A
                // tutorial fusion must never silently substitute the starter
                // mage as its result.
                SetStatus("配合教材の結果を確認できません。ロックゴーレム2体を選び直してください。");
                return;
            }

            // Lock before the one gameplay operation, not only when the first
            // animation frame runs. Keep a deep snapshot for failed disk commits.
            fusionInProgress = true;
            if (fuseButton != null) fuseButton.interactable = false;
            PlayerSaveData beforeFusion = JsonUtility.FromJson<PlayerSaveData>(
                JsonUtility.ToJson(profile.ToSaveData(GameManager.Instance.CurrentFloor)));
            MonsterFusionResult result;
            try
            {
                result = MonsterFusionService.Fuse(profile, parentAInstanceId, parentBInstanceId, masterDataManager);
                if (result.CanFuse)
                {
                    if (fusionTutorialPractice) StoryTutorialService.MarkHintSeen(profile, StoryTutorialService.HintFusionInheritance);
                    SaveManager saves = SaveManager.Instance;
                    if (saves == null || !saves.TrySaveWithReason(
                        profile.ToSaveData(GameManager.Instance.CurrentFloor), "fusion", out _))
                    {
                        RestoreFusionMutation(profile, beforeFusion);
                        fusionInProgress = false;
                        RefreshRoster();
                        RefreshPreview();
                        SetStatus("配合結果を保存できませんでした。親はそのまま残っています。もう一度お試しください。");
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                RestoreFusionMutation(profile, beforeFusion);
                fusionInProgress = false;
                RefreshRoster();
                RefreshPreview();
                SetStatus("配合を完了できませんでした。親はそのまま残っています。");
                Debug.LogWarning("[Fusion] Transaction cancelled: " + exception.Message);
                return;
            }
            if (!result.CanFuse)
            {
                fusionInProgress = false;
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                RefreshPreview();
                SetStatus(result.Message);
                return;
            }

            string successMessage = result.Message;
            FusionPresentationReceipt.Store(profile, result.CreatedMonster, parentDataA, parentDataB, fusionTutorialPractice);
            parentAInstanceId = string.Empty;
            parentBInstanceId = string.Empty;
            RefreshRoster();
            RefreshPreview();
            SetStatus(successMessage);
            FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
            fusionTutorialCompleted = fusionTutorialPractice;
            StartFusionResultStage(result, parentA, parentB, parentDataA, parentDataB, successMessage);
        }

        private bool IsFusionTutorialGuideActive()
        {
            return fusionTutorialGuideRoot != null && fusionTutorialGuideRoot.activeInHierarchy;
        }

        private static void RestoreFusionMutation(PlayerProfile profile, PlayerSaveData before)
        {
            // Fuse changes only inventory, equipment links, the party, dex, and
            // the optional lesson flag. Restore these without reinitializing the
            // whole game (which would also auto-fill an empty party).
            profile.EquippedWeaponId = before.EquippedWeaponId;
            profile.EquippedArmorId = before.EquippedArmorId;
            profile.EquippedAccessoryId = before.EquippedAccessoryId;
            profile.OwnedMonsters.Clear();
            profile.OwnedMonsters.AddRange(before.OwnedMonsters);
            profile.OwnedEquipments.Clear();
            profile.OwnedEquipments.AddRange(before.OwnedEquipments);
            profile.PartyMonsterInstanceIds.Clear();
            profile.PartyMonsterInstanceIds.AddRange(before.PartyMonsterInstanceIds);
            profile.MonsterDexEntries.Clear();
            profile.MonsterDexEntries.AddRange(before.MonsterDexEntries);
            profile.SeenTutorialHintIds.Clear();
            profile.SeenTutorialHintIds.AddRange(before.SeenTutorialHintIds);
        }

        private void StartFusionResultStage(
            MonsterFusionResult result,
            OwnedMonsterData parentA,
            OwnedMonsterData parentB,
            MonsterDataSO parentDataA,
            MonsterDataSO parentDataB,
            string successMessage)
        {
            StopFusionCinematic();
            pendingFusionResult = result;
            pendingFusionParentA = parentDataA;
            pendingFusionParentB = parentDataB;
            pendingFusionMessage = successMessage;
            resultAwaitingAcknowledgement = result?.CreatedMonster != null;
            resultStageRoutine = StartCoroutine(PlayFusionResultStage(result, parentA, parentB, parentDataA, parentDataB, successMessage));
        }

        private IEnumerator PlayFusionResultStage(
            MonsterFusionResult result,
            OwnedMonsterData parentA,
            OwnedMonsterData parentB,
            MonsterDataSO parentDataA,
            MonsterDataSO parentDataB,
            string successMessage)
        {
            fusionInProgress = true;
            SetResultStageButtonsVisible(false);
            if (selectionRoot != null) selectionRoot.SetActive(false);
            if (fusionTutorialGuideRoot != null) fusionTutorialGuideRoot.SetActive(false);
            if (resultStageRoot != null) resultStageRoot.SetActive(false);

            IEnumerator playback = null;
            try
            {
                fusionCinematic = new FusionCinematicPresentation(EnsureRootRect());
                playback = fusionCinematic.Play(ResolveMonsterSprite(parentDataA), ResolveMonsterSprite(parentDataB),
                    result.ResultMonsterData, result.CreatedMonster);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Fusion] Presentation unavailable; showing saved result: " + exception.Message);
            }

            // Drive the presentation explicitly so missing artwork or a renderer
            // exception cannot strand the user after the already-committed fuse.
            while (playback != null)
            {
                bool hasFrame;
                object frame = null;
                try
                {
                    hasFrame = playback.MoveNext();
                    if (hasFrame) frame = playback.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Fusion] Presentation interrupted; showing saved result: " + exception.Message);
                    break;
                }
                if (!hasFrame) break;
                yield return frame;
            }
            fusionCinematic?.Dispose();
            fusionCinematic = null;
            CompleteFusionResultStage();
            resultStageRoutine = null;
        }

        private void StopFusionCinematic()
        {
            if (resultStageRoutine != null)
            {
                StopCoroutine(resultStageRoutine);
                resultStageRoutine = null;
            }
            fusionCinematic?.Dispose();
            fusionCinematic = null;
            fusionInProgress = false;
        }

        private bool TryRestoreFusionResult()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (!FusionPresentationReceipt.TryRead(profile, out FusionPresentationReceipt receipt))
            {
                pendingFusionResult = null;
                resultAwaitingAcknowledgement = false;
                return false;
            }
            OwnedMonsterData created = profile.GetOwnedMonster(receipt.CreatedInstanceId);
            MasterDataManager master = MasterDataManager.Instance;
            MonsterDataSO born = master?.GetMonsterData(created.MonsterId);
            if (born == null) return false;
            pendingFusionResult = new MonsterFusionResult(MonsterFusionStatus.Success,
                resultMonsterData: born, createdMonster: created, message: born.monsterName + " が誕生しました。");
            pendingFusionParentA = master.GetMonsterData(receipt.ParentMonsterIdA);
            pendingFusionParentB = master.GetMonsterData(receipt.ParentMonsterIdB);
            pendingFusionMessage = pendingFusionResult.Message;
            fusionTutorialCompleted = receipt.CompletedTutorial;
            resultAwaitingAcknowledgement = true;
            CompleteFusionResultStage();
            return true;
        }

        private void AcknowledgeFusionResult()
        {
            if (!resultAwaitingAcknowledgement) return;
            FusionPresentationReceipt.Clear(pendingFusionResult?.CreatedMonster?.InstanceId);
            resultAwaitingAcknowledgement = false;
            pendingFusionResult = null;
            pendingFusionParentA = null;
            pendingFusionParentB = null;
            pendingFusionMessage = null;
        }

        private void CompleteFusionResultStage()
        {
            if (pendingFusionResult == null) return;
            MonsterDataSO bornMonsterData = pendingFusionResult.ResultMonsterData;
            OwnedMonsterData createdMonster = pendingFusionResult.CreatedMonster;
            FusionResultTier tier = ResolveFusionResultTier(bornMonsterData);
            Color tierColor = ResolveFusionTierColor(tier);
            Sprite bornSprite = ResolveMonsterSprite(bornMonsterData);

            if (selectionRoot != null) selectionRoot.SetActive(false);
            if (fusionTutorialGuideRoot != null) fusionTutorialGuideRoot.SetActive(false);
            if (resultStageRoot != null)
            {
                resultStageRoot.SetActive(true);
                resultStageRoot.transform.SetAsLastSibling();
            }
            if (resultStageCanvasGroup != null)
            {
                resultStageCanvasGroup.alpha = 1f;
                resultStageCanvasGroup.blocksRaycasts = true;
                resultStageCanvasGroup.interactable = true;
            }
            SetText(resultStageTitleLabel, GetFusionRevealTitle(tier), tierColor);
            SetText(resultStageSummaryLabel, string.IsNullOrEmpty(pendingFusionMessage)
                ? "新たなモンスターが誕生しました" : pendingFusionMessage, TextMain);
            SetText(resultStageBornNameLabel, GetMonsterDisplayName(bornMonsterData), TextMain);
            SetText(resultStageClassLabel, BuildFusionResultClassLabel(bornMonsterData, createdMonster), tierColor);
            SetText(resultStageParentLabel, BuildFusionParentText(pendingFusionParentA, pendingFusionParentB, createdMonster), TextSub);
            SetStageSprite(resultStageBornImage, resultStageBornRect, bornSprite, Color.white, new Vector2(0f, -144f), 1f, 0f);
            if (resultStageBornShadowImage != null) resultStageBornShadowImage.sprite = bornSprite;
            SetBornRevealAlpha(1f);
            if (resultStageEffectImage != null)
            {
                resultStageEffectImage.sprite = ResolveResultEffectSprite(tier);
                resultStageEffectImage.color = new Color(1f, 1f, 1f, GetFusionEffectHoldAlpha(tier));
                resultStageEffectImage.enabled = resultStageEffectImage.sprite != null;
            }
            if (resultStageEffectRect != null)
            {
                resultStageEffectRect.localScale = Vector3.one * GetFusionEffectEndScale(tier) * .92f;
                resultStageEffectRect.localEulerAngles = Vector3.zero;
            }
            SetImageAlpha(resultStageParentAImage, 0f);
            SetImageAlpha(resultStageParentBImage, 0f);
            SetImageAlpha(resultStageParentALightImage, 0f);
            SetImageAlpha(resultStageParentBLightImage, 0f);
            SetImageAlpha(resultStageCoreImage, 0f);
            if (resultStageFlashImage != null) resultStageFlashImage.color = Color.clear;
            fusionInProgress = false;
            SetResultStageButtonsVisible(true);
            if (fusionTutorialCompleted) ShowFusionCompletionGuide();
        }

        private void ShowFusionCompletionGuide()
        {
            if (resultStageNextButton != null) resultStageNextButton.gameObject.SetActive(false);
            if (fusionCompletionGuide == null)
            {
                fusionCompletionGuide = CreatePanel("FusionCompletionGuide", resultStageRoot.transform, null,
                    new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                    new Vector2(0f, 80f), new Vector2(940f, 340f), new Color(.025f,.035f,.055f,.98f));
                CreatePortraitImage(fusionCompletionGuide.transform, "Luche", TutorialGuideSpritePath,
                    new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(10,0), new Vector2(230,280));
                CreateText("CompletionBody", fusionCompletionGuide.transform,
                    "ルシェ：配合できました！\n親の力を受け継いだ仲間が\nレベル1で誕生しました。\nホームへ戻って編成しましょう。", 30, FontStyle.Bold,
                    new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(250,-30), new Vector2(660,190), TextAnchor.MiddleLeft, TextMain);
                CreateButton("CompletionHomeButton", fusionCompletionGuide.transform, "ホームへ戻る",
                    new Vector2(1,0), new Vector2(1,0), new Vector2(1,0), new Vector2(-28,20), new Vector2(330,100), ConfirmButtonSpritePath, FuseButtonColor, Hide);
            }
            fusionCompletionGuide.SetActive(true);
            fusionCompletionGuide.transform.SetAsLastSibling();
        }

        private void ReturnToSelectionScreen()
        {
            if (fusionInProgress || fusionTutorialCompleted)
            {
                return;
            }

            AcknowledgeFusionResult();
            if (resultStageRoot != null)
            {
                resultStageRoot.SetActive(false);
            }

            if (resultStageCanvasGroup != null)
            {
                resultStageCanvasGroup.blocksRaycasts = false;
                resultStageCanvasGroup.interactable = false;
            }

            if (selectionRoot != null)
            {
                selectionRoot.SetActive(true);
            }

            ClearFusionResultStagePresentation();

            SetResultStageButtonsVisible(false);
            RefreshRoster();
            RefreshPreview();
        }

        private void ClearFusionResultStagePresentation()
        {
            if (resultStageBornImage != null)
            {
                resultStageBornImage.sprite = null;
                resultStageBornImage.color = Color.clear;
                resultStageBornImage.enabled = false;
            }

            if (resultStageBornShadowImage != null)
            {
                resultStageBornShadowImage.sprite = null;
                resultStageBornShadowImage.color = Color.clear;
                resultStageBornShadowImage.enabled = false;
            }

            SetText(resultStageBornNameLabel, string.Empty, Color.clear);
            SetText(resultStageClassLabel, string.Empty, Color.clear);
            SetText(resultStageParentLabel, string.Empty, Color.clear);
        }

        private void SetResultStageButtonsVisible(bool visible)
        {
            if (resultStageNextButton != null)
            {
                resultStageNextButton.gameObject.SetActive(visible);
                resultStageNextButton.interactable = visible;
            }

            if (resultStageCloseButton != null)
            {
                resultStageCloseButton.gameObject.SetActive(visible);
                resultStageCloseButton.interactable = visible;
            }
        }

        private void SetBornRevealAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (resultStageBornImage != null)
            {
                resultStageBornImage.color = resultStageBornImage.sprite != null
                    ? new Color(1f, 1f, 1f, alpha)
                    : new Color(1f, 1f, 1f, alpha * 0.12f);
                resultStageBornImage.enabled = resultStageBornImage.sprite != null && alpha > 0.01f;
            }

            if (resultStageBornShadowImage != null)
            {
                resultStageBornShadowImage.color = new Color(0f, 0f, 0f, alpha * 0.54f);
                resultStageBornShadowImage.enabled = resultStageBornShadowImage.sprite != null && alpha > 0.01f;
            }

            SetTextAlpha(resultStageBornNameLabel, alpha);
            SetTextAlpha(resultStageClassLabel, alpha);
            SetTextAlpha(resultStageParentLabel, alpha);
        }

        private static void SetStageSprite(
            Image image,
            RectTransform rect,
            Sprite sprite,
            Color color,
            Vector2 anchoredPosition,
            float scale,
            float rotation)
        {
            if (image != null)
            {
                image.sprite = sprite;
                image.color = color;
                image.enabled = sprite != null && color.a > 0.01f;
            }

            SetStageTransform(rect, anchoredPosition, scale, rotation);
        }

        private static void SetStageTransform(RectTransform rect, Vector2 anchoredPosition, float scale, float rotation)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private Sprite ResolveResultEffectSprite(FusionResultTier tier)
        {
            Sprite sprite = LoadSprite(GetResultEffectSpritePath(tier));
            if (sprite != null)
            {
                return sprite;
            }

            sprite = LoadSprite(ResultEffectFallbackSpritePath);
            return sprite != null ? sprite : LoadSprite(BirthBurstSpriteBasePath + "0");
        }

        private Sprite ResolveMonsterSprite(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (monsterData.illustrationSprite != null)
            {
                return monsterData.illustrationSprite;
            }

            if (monsterData.portraitSprite != null)
            {
                return monsterData.portraitSprite;
            }

            string resourcePath = !string.IsNullOrEmpty(monsterData.illustrationResourcePath)
                ? monsterData.illustrationResourcePath
                : monsterData.portraitResourcePath;
            return LoadSprite(resourcePath);
        }

        private static FusionResultTier ResolveFusionResultTier(MonsterDataSO monsterData)
        {
            int classRank = Mathf.Max(1, monsterData != null ? monsterData.classRank : 1);
            if (classRank >= 4)
            {
                return FusionResultTier.Legendary;
            }

            return classRank >= 3 ? FusionResultTier.Rare : FusionResultTier.Normal;
        }

        private static string GetResultEffectSpritePath(FusionResultTier tier)
        {
            switch (tier)
            {
                case FusionResultTier.Legendary:
                    return ResultEffectLegendarySpritePath;
                case FusionResultTier.Rare:
                    return ResultEffectRareSpritePath;
                default:
                    return ResultEffectNormalSpritePath;
            }
        }

        private static string GetFusionRevealTitle(FusionResultTier tier)
        {
            switch (tier)
            {
                case FusionResultTier.Legendary:
                    return "未知の深層から誕生";
                case FusionResultTier.Rare:
                    return "未知の光から誕生";
                default:
                    return "新たな命が誕生";
            }
        }

        private static Color ResolveFusionTierColor(FusionResultTier tier)
        {
            switch (tier)
            {
                case FusionResultTier.Legendary:
                    return new Color(1f, 0.78f, 0.30f, 1f);
                case FusionResultTier.Rare:
                    return new Color(0.97f, 0.55f, 0.22f, 1f);
                default:
                    return new Color(0.42f, 0.88f, 0.96f, 1f);
            }
        }

        private static float GetFusionEffectEndScale(FusionResultTier tier)
        {
            switch (tier)
            {
                case FusionResultTier.Legendary:
                    return 1.28f;
                case FusionResultTier.Rare:
                    return 1.16f;
                default:
                    return 1.02f;
            }
        }

        private static float GetFusionEffectHoldAlpha(FusionResultTier tier)
        {
            switch (tier)
            {
                case FusionResultTier.Legendary:
                    return 0.72f;
                case FusionResultTier.Rare:
                    return 0.58f;
                default:
                    return 0.44f;
            }
        }

        private static string BuildFusionResultClassLabel(MonsterDataSO bornMonsterData, OwnedMonsterData createdMonster)
        {
            int classRank = Mathf.Max(1, bornMonsterData != null ? bornMonsterData.classRank : 1);
            string damageText = bornMonsterData != null ? $" / {ResolveDamageTypeLabel(bornMonsterData.damageType)}" : string.Empty;
            string levelText = createdMonster != null ? $" / Lv.{createdMonster.Level}{BuildPlusSuffix(createdMonster)}" : string.Empty;
            return $"CLASS {classRank}{damageText}{levelText}";
        }

        private static string BuildFusionParentText(MonsterDataSO parentDataA, MonsterDataSO parentDataB, OwnedMonsterData createdMonster)
        {
            string parentAName = GetMonsterDisplayName(parentDataA);
            string parentBName = GetMonsterDisplayName(parentDataB);
            string inheritedText = createdMonster != null ? BuildFusionBonusShort(createdMonster) : string.Empty;
            if (!string.IsNullOrEmpty(inheritedText))
            {
                inheritedText = "\n" + inheritedText.TrimStart(' ', '/');
            }

            return $"{parentAName} + {parentBName}{inheritedText}";
        }

        private static string GetMonsterDisplayName(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return "不明";
            }

            return !string.IsNullOrEmpty(monsterData.monsterName) ? monsterData.monsterName : monsterData.monsterId;
        }

        private static void SetText(Text label, string value, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.text = value ?? string.Empty;
            label.color = color;
        }

        private static void SetTextAlpha(Text label, float alpha)
        {
            if (label == null)
            {
                return;
            }

            Color color = label.color;
            color.a = Mathf.Clamp01(alpha);
            label.color = color;
        }

        private void CreateCeremonyEffects(Transform parent)
        {
            energyStreamSprites = LoadSpriteSequence(EnergyStreamSpriteBasePath, CeremonyFrameCount);
            birthBurstSprites = LoadSpriteSequence(BirthBurstSpriteBasePath, CeremonyFrameCount);

            magicCircleImage = CreateEffectImage("FusionMagicCircleEffect", parent, MagicCircleSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -242f), new Vector2(520f, 520f));
            magicCircleRect = magicCircleImage.GetComponent<RectTransform>();

            parentAGlowImage = CreateEffectImage("ParentAGlow", parent, SlotGlowSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                ParentAEffectPosition, new Vector2(300f, 300f));
            parentAGlowRect = parentAGlowImage.GetComponent<RectTransform>();

            parentBGlowImage = CreateEffectImage("ParentBGlow", parent, SlotGlowSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                ParentBEffectPosition, new Vector2(300f, 300f));
            parentBGlowRect = parentBGlowImage.GetComponent<RectTransform>();

            resultGlowImage = CreateEffectImage("ResultGlow", parent, SlotGlowSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                ResultEffectPosition, new Vector2(330f, 330f));
            resultGlowRect = resultGlowImage.GetComponent<RectTransform>();

            streamAImage = CreateEffectImage("ParentAToResultStream", parent, EnergyStreamSpriteBasePath + "0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                ParentBEffectPosition, new Vector2(620f, 118f), false);
            streamARect = streamAImage.GetComponent<RectTransform>();

            streamBImage = CreateEffectImage("ParentBToResultStream", parent, EnergyStreamSpriteBasePath + "0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(156f, -180f), new Vector2(330f, 92f), false);
            streamBRect = streamBImage.GetComponent<RectTransform>();
            streamBRect.localEulerAngles = new Vector3(0f, 0f, -5f);

            birthBurstImage = CreateEffectImage("FusionBirthBurst", parent, BirthBurstSpriteBasePath + "0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                ResultEffectPosition, new Vector2(430f, 430f));
            birthBurstRect = birthBurstImage.GetComponent<RectTransform>();
        }

        private Image CreateEffectImage(
            string objectName,
            Transform parent,
            string spritePath,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            bool preserveAspect = true)
        {
            GameObject effectObject = CreateUiObject(objectName, parent);
            RectTransform rect = effectObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = effectObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.color = Color.clear;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void AnimateCeremonyEffects()
        {
            bool hasParentA = !string.IsNullOrEmpty(parentAInstanceId);
            bool hasParentB = !string.IsNullOrEmpty(parentBInstanceId);
            bool isCharged = previewCanFuse || birthEffectTimer > 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(ceremonyTime * 4.6f);
            float slowPulse = 0.5f + 0.5f * Mathf.Sin(ceremonyTime * 2.2f);

            AnimateGlow(parentAGlowImage, parentAGlowRect, hasParentA, 0.18f + pulse * 0.18f, 0.98f + slowPulse * 0.05f);
            AnimateGlow(parentBGlowImage, parentBGlowRect, hasParentB, 0.18f + pulse * 0.18f, 0.98f + (1f - slowPulse) * 0.05f);
            AnimateGlow(resultGlowImage, resultGlowRect, isCharged, isCharged ? 0.22f + pulse * 0.28f : 0f, 1.02f + pulse * 0.08f);

            float circleAlpha = isCharged
                ? 0.34f + slowPulse * 0.22f
                : hasParentA || hasParentB ? 0.12f + pulse * 0.08f : 0f;
            SetImageAlpha(magicCircleImage, circleAlpha);
            if (magicCircleRect != null)
            {
                float circleScale = isCharged ? 1.02f + slowPulse * 0.04f : 0.96f + slowPulse * 0.02f;
                magicCircleRect.localScale = Vector3.one * circleScale;
                magicCircleRect.localEulerAngles = new Vector3(0f, 0f, ceremonyTime * 16f);
            }

            AnimateStream(streamAImage, streamARect, hasParentA && isCharged, 0.18f + pulse * 0.42f, 1f + pulse * 0.025f, false);
            AnimateStream(streamBImage, streamBRect, hasParentB && isCharged, 0.16f + (1f - pulse) * 0.38f, 1f + (1f - pulse) * 0.025f, true);
            AnimateBirthBurst();
        }

        private void AnimateGlow(Image image, RectTransform rect, bool visible, float alpha, float scale)
        {
            SetImageAlpha(image, visible ? alpha : 0f);
            if (rect != null)
            {
                rect.localScale = Vector3.one * scale;
            }
        }

        private void AnimateStream(Image image, RectTransform rect, bool visible, float alpha, float scale, bool isSecondary)
        {
            if (image != null && energyStreamSprites != null && energyStreamSprites.Length > 0)
            {
                int offset = isSecondary ? 3 : 0;
                int index = Mathf.FloorToInt(ceremonyTime * 13f + offset) % energyStreamSprites.Length;
                image.sprite = energyStreamSprites[Mathf.Clamp(index, 0, energyStreamSprites.Length - 1)];
            }

            SetImageAlpha(image, visible ? alpha : 0f);
            if (rect != null)
            {
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void AnimateBirthBurst()
        {
            if (birthBurstImage == null)
            {
                return;
            }

            if (birthEffectTimer <= 0f)
            {
                SetImageAlpha(birthBurstImage, 0f);
                return;
            }

            float progress = Mathf.Clamp01(1f - birthEffectTimer / BirthEffectDuration);
            if (birthBurstSprites != null && birthBurstSprites.Length > 0)
            {
                int index = Mathf.Clamp(Mathf.FloorToInt(progress * birthBurstSprites.Length), 0, birthBurstSprites.Length - 1);
                birthBurstImage.sprite = birthBurstSprites[index];
            }

            float alpha = Mathf.Sin(progress * Mathf.PI) * 0.95f;
            SetImageAlpha(birthBurstImage, alpha);
            if (birthBurstRect != null)
            {
                float scale = 0.72f + progress * 0.62f;
                birthBurstRect.localScale = Vector3.one * scale;
                birthBurstRect.localEulerAngles = new Vector3(0f, 0f, -progress * 18f);
            }
        }

        private void StartBirthEffect()
        {
            birthEffectTimer = BirthEffectDuration;
            if (birthBurstImage != null)
            {
                birthBurstImage.transform.SetAsLastSibling();
            }
        }

        private Sprite[] LoadSpriteSequence(string basePath, int count)
        {
            Sprite[] sprites = new Sprite[Mathf.Max(0, count)];
            for (int i = 0; i < sprites.Length; i += 1)
            {
                sprites[i] = LoadSprite(basePath + i);
            }

            return sprites;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
            image.enabled = image.sprite != null && color.a > 0.01f;
        }

        private bool HasProtectedParent(PlayerProfile profile)
        {
            OwnedMonsterData parentA = profile?.GetOwnedMonster(parentAInstanceId);
            OwnedMonsterData parentB = profile?.GetOwnedMonster(parentBInstanceId);
            return parentA != null && (parentA.IsFavorite || parentA.IsLocked)
                || parentB != null && (parentB.IsFavorite || parentB.IsLocked);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                bool isEmpty = string.IsNullOrEmpty(message);
                bool isLevelWarning = IsMaxLevelRequirementMessage(message);
                statusLabel.text = isEmpty ? string.Empty : message;
                statusLabel.color = isLevelWarning ? WarningText : new Color(0.94f, 0.98f, 1f, 0.96f);
                if (statusPlate != null)
                {
                    statusPlate.gameObject.SetActive(isLevelWarning);
                    statusPlate.color = WarningPlateColor;
                }
            }
        }

        private static bool IsMaxLevelRequirementMessage(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                (message.Contains("最大Lv") ||
                    message.Contains("最大レベル") ||
                    message.Contains("親2体とも最大"));
        }

        private static MonsterDataSO ResolveMonsterData(MasterDataManager masterDataManager, OwnedMonsterData monster)
        {
            return masterDataManager != null && monster != null
                ? masterDataManager.GetMonsterData(monster.MonsterId)
                : null;
        }

        private static string ResolveRaceName(string raceId)
        {
            return raceId switch
            {
                "dragon" => "ドラゴン",
                "robot" => "ロボット",
                "golem" => "ゴーレム",
                "swordsman" => "剣士",
                "mage" => "魔法使い",
                "angel" => "天使",
                "spirit" => "精霊",
                "special" => "特殊",
                _ => string.IsNullOrEmpty(raceId) ? "不明" : raceId
            };
        }

        private static string ResolveDamageTypeLabel(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法型" : "物理型";
        }

        private static string GetPortraitResourcePath(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(monsterData.portraitResourcePath))
            {
                return monsterData.portraitResourcePath;
            }

            return monsterData.illustrationResourcePath ?? string.Empty;
        }

        private static string BuildFusionBonusShort(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return string.Empty;
            }

            int totalBonus =
                Mathf.Max(0, monster.FusionBonusHp) +
                Mathf.Max(0, monster.FusionBonusAttack) +
                Mathf.Max(0, monster.FusionBonusWisdom) +
                Mathf.Max(0, monster.FusionBonusDefense) +
                Mathf.Max(0, monster.FusionBonusMagicDefense);
            string inheritedText = totalBonus > 0 ? $" / 継承+{totalBonus}" : string.Empty;
            return inheritedText;
        }

        private static string BuildFusionBonusLong(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "継承なし";
            }

            int totalBonus =
                Mathf.Max(0, monster.FusionBonusHp) +
                Mathf.Max(0, monster.FusionBonusAttack) +
                Mathf.Max(0, monster.FusionBonusWisdom) +
                Mathf.Max(0, monster.FusionBonusDefense) +
                Mathf.Max(0, monster.FusionBonusMagicDefense);
            string fusionText = totalBonus > 0 ? $"継承ボーナス +{totalBonus}" : "継承なし";
            return $"{fusionText}\n{MonsterIndividualValueService.BuildSummary(monster)}";
        }

        private static string BuildFusionSlotDetailText(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            if (monster == null)
            {
                return "親を選択\n\n";
            }

            int average = MonsterIndividualValueService.GetAverage(monster);
            string levelText = BuildMonsterLevelShortText(monster, monsterData);
            string raceText = monsterData != null ? ResolveRaceName(monsterData.raceId) : "不明";
            string classText = "C" + Mathf.Max(1, monsterData != null ? monsterData.classRank : 1);
            string damageText = monsterData != null ? ResolveDamageTypeLabel(monsterData.damageType) : "型不明";
            string bonusText = BuildFusionBonusLabel(monster);
            return $"{levelText}   IV{average}\n{raceText} / {classText} / {damageText}   {bonusText}";
        }

        private static string BuildFusionBonusLabel(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "継承なし";
            }

            int totalBonus =
                Mathf.Max(0, monster.FusionBonusHp) +
                Mathf.Max(0, monster.FusionBonusAttack) +
                Mathf.Max(0, monster.FusionBonusWisdom) +
                Mathf.Max(0, monster.FusionBonusDefense) +
                Mathf.Max(0, monster.FusionBonusMagicDefense);
            return totalBonus > 0 ? $"継承+{totalBonus}" : "継承なし";
        }

        private static string BuildMonsterLevelShortText(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            int level = MonsterLevelService.ClampLevelToMax(monster != null ? monster.Level : 1, monsterData);
            int maxLevel = MonsterLevelService.GetMaxLevel(monsterData);
            string plusText = BuildPlusSuffix(monster);
            return level >= maxLevel ? $"Lv.{level}/{maxLevel}{plusText} MAX" : $"Lv.{level}/{maxLevel}{plusText}";
        }

        private static string BuildMonsterLevelProgressText(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            int level = MonsterLevelService.ClampLevelToMax(monster != null ? monster.Level : 1, monsterData);
            int maxLevel = MonsterLevelService.GetMaxLevel(monsterData);
            string plusText = BuildPlusSuffix(monster);
            if (level >= maxLevel)
            {
                return $"Lv.{level}/{maxLevel}{plusText} MAX";
            }

            int requiredExp = MonsterLevelService.GetRequiredExpForNextLevel(monster, monsterData);
            int currentExp = Mathf.Max(0, monster != null ? monster.Exp : 0);
            return $"Lv.{level}/{maxLevel}{plusText} 経験値 {currentExp}/{Mathf.Max(1, requiredExp)}";
        }

        private static string BuildPlusSuffix(OwnedMonsterData monster)
        {
            int plusValue = Mathf.Max(0, monster != null ? monster.TotalPlusValue : 0);
            return plusValue > 0 ? $" +{plusValue}" : string.Empty;
        }

        private RectTransform EnsureRootRect()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            return rect;
        }

        private void CreateFullScreenImage(string objectName, Transform parent, string spritePath, Color fallbackColor)
        {
            GameObject root = CreateUiObject(objectName, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = root.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.preserveAspect = false;
            image.raycastTarget = false;
            root.transform.SetAsFirstSibling();
        }

        private GameObject CreateButton(
            string objectName,
            Transform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            string spritePath,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, spritePath, anchorMin, anchorMax, pivot, anchoredPosition, size, color);
            Button button = buttonObject.AddComponent<Button>();
            Image buttonImage = buttonObject.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
            }

            button.targetGraphic = buttonImage;
            button.onClick.AddListener(onClick);

            Text label = CreateText("Label", buttonObject.transform, text, 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 14f, size.y - 14f), TextAnchor.MiddleCenter, Color.white);
            if (objectName == "SwapButton" || objectName == "FuseButton" || objectName == "CompletionHomeButton") label.fontSize = 32;
            AddTextContrast(label);

            return buttonObject;
        }

        private GameObject CreatePanel(
            string objectName,
            Transform parent,
            string spritePath,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject panel = CreateUiObject(objectName, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = panel.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : color;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return panel;
        }

        private GameObject CreateDecorationPanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject panel = CreatePanel(objectName, parent, null, anchorMin, anchorMax, pivot, anchoredPosition, size, color);
            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }

            return panel;
        }

        private Image CreatePortraitImage(
            Transform parent,
            string objectName,
            string spritePath,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject portraitObject = CreateUiObject(objectName, parent);
            RectTransform rect = portraitObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = portraitObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetPortrait(image, spritePath);
            return image;
        }

        private void SetPortrait(Image target, string spritePath)
        {
            if (target == null)
            {
                return;
            }

            Sprite sprite = LoadSprite(spritePath);
            target.sprite = sprite;
            target.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        }

        private Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (spriteCache.TryGetValue(resourcePath, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            spriteCache[resourcePath] = sprite;
            return sprite;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            string text,
            int fontSize,
            FontStyle fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment,
            Color color)
        {
            GameObject label = CreateUiObject(objectName, parent);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text textComponent = label.AddComponent<Text>();
            textComponent.font = runtimeFont;
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        private static void AddTextContrast(Text label)
        {
            if (label == null)
            {
                return;
            }

            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = TextOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i -= 1)
            {
                DestroyObject(transform.GetChild(i).gameObject);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Font GetRuntimeFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                font = null;
            }

            if (font == null)
            {
                try
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                    font = null;
                }
            }

            return font;
        }
    }
}
