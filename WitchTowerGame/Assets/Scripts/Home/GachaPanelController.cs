using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class GachaPanelController : MonoBehaviour
    {
        private const string BackgroundSpritePath = "UI/GachaPage/GachaSummonChamberBackground";
        private const string PullButtonSpritePath = "UI/GachaPage/GachaPullButton";
        private const string SmallButtonSpritePath = "UI/GachaPage/GachaSmallButton";
        private const string FreeStoneIconPath = "UI/GachaPage/GachaStoneFreeIcon";
        private const string PaidStoneIconPath = "UI/GachaPage/GachaStonePaidIcon";
        private const string MasterDataRootPath = "MasterData/MasterDataRoot";
        private const string NormalEffectSpritePath = "UI/GachaPage/Effects/GachaContractEffect_Normal";
        private const string RareEffectSpritePath = "UI/GachaPage/Effects/GachaContractEffect_Rare";
        private const string LegendaryEffectSpritePath = "UI/GachaPage/Effects/GachaContractEffect_Legendary";
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const string TutorialHighlightFramePath = "UI/Tutorial/TutorialSummonHighlightFrameImage2";
        private const string RockGolemMonsterId = "monster_rock_golem";
        private const string RockGolemHomeHeroSpritePath = "MonsterBattle/mon_rock_golem_attack_0";
        private const int TutorialStarterIndividualValue = 50;
        private const int GachaStoneCostPerPull = 300;
        private const int PaidTenPullGuaranteedClassRank = 3;
        private const int FreeClass3SummonRatePercent = 1;
        private const int FreeClass2SummonRatePercent = 9;
        private const int PaidClass4SummonRatePercent = 1;
        private const int PaidClass3SummonRatePercent = 3;
        private const int PaidClass2SummonRatePercent = 9;
        private static readonly string[] InitialTutorialMonsterIds =
        {
            "monster_dragon_whelp",
            "monster_rock_golem",
            "monster_apprentice_mage"
        };

        private static readonly Color PanelColor = new Color(0.035f, 0.030f, 0.028f, 0.72f);
        private static readonly Color DeepPanelColor = new Color(0.015f, 0.012f, 0.014f, 0.82f);
        private static readonly Color GoldTextColor = new Color(1f, 0.78f, 0.38f, 1f);
        private static readonly Color PaleTextColor = new Color(0.94f, 0.89f, 0.80f, 0.96f);
        private static readonly Color AshTextColor = new Color(0.70f, 0.74f, 0.78f, 0.94f);

        private Action closeAction;
        private bool built;
        private bool builtForPlayMode;
        private GameObject contractHomeRoot;
        private GameObject resultStageRoot;
        private Text freeStoneText;
        private Text paidStoneText;
        private Text storageText;
        private Text statusText;
        private GameObject ritePanelRoot;
        private Button singlePullButton;
        private Image singlePullButtonBackgroundImage;
        private Sprite singlePullButtonOriginalSprite;
        private Color singlePullButtonOriginalColor;
        private Image.Type singlePullButtonOriginalType;
        private bool singlePullButtonBackgroundMuted;
        private Button tenPullButton;
        private Button paidSinglePullButton;
        private Button paidTenPullButton;
        private GameObject summonTutorialGuideRoot;
        private Text summonTutorialGuideTitleText;
        private Text summonTutorialGuideBodyText;
        private Text summonTutorialGuideFooterText;
        private Image summonTutorialGuideCharacterImage;
        private Image summonTutorialSinglePullHighlight;
        private CanvasGroup effectCanvasGroup;
        private Image effectImage;
        private Image effectFlashImage;
        private RectTransform effectImageRect;
        private Image resultMonsterImage;
        private Image resultMonsterShadowImage;
        private RectTransform resultMonsterRect;
        private Text resultTitleText;
        private Text resultMonsterNameText;
        private Text resultClassText;
        private Text resultSummaryText;
        private Button resultAgainButton;
        private Button resultBackButton;
        private Button resultHomeButton;
        private GameObject resultTutorialGuideRoot;
        private Image resultTutorialGuideCharacterImage;
        private Image resultTutorialHomeHighlight;
        private readonly List<ResultSlotView> resultSlotViews = new List<ResultSlotView>();
        private Coroutine effectRoutine;
        private bool contractInProgress;
        private int lastRequestedCount = 1;
        private bool lastUsedPaidStones;

        private enum ContractEffectTier
        {
            Normal,
            Rare,
            Legendary
        }

        private sealed class ResultSlotView
        {
            public GameObject Root;
            public Image Frame;
            public Image Portrait;
            public Text ClassLabel;
            public Text NameLabel;
        }

        public void Show(Action onClose)
        {
            closeAction = onClose;
            NormalizeParentCanvasScale();
            bool isPlayModeBuild = Application.isPlaying;
            if (!built || builtForPlayMode != isPlayModeBuild)
            {
                Build();
                built = true;
                builtForPlayMode = isPlayModeBuild;
            }

            gameObject.SetActive(true);
            UpdatePreviewState();
        }

        private void NormalizeParentCanvasScale()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return;
            }

            parentCanvas.transform.localScale = Vector3.one;
            RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
            }
        }

        private void Build()
        {
            transform.SetAsLastSibling();
            ClearChildren();
            contractHomeRoot = null;
            resultStageRoot = null;
            freeStoneText = null;
            paidStoneText = null;
            storageText = null;
            statusText = null;
            ritePanelRoot = null;
            singlePullButton = null;
            singlePullButtonBackgroundImage = null;
            singlePullButtonOriginalSprite = null;
            singlePullButtonOriginalColor = Color.white;
            singlePullButtonOriginalType = Image.Type.Simple;
            singlePullButtonBackgroundMuted = false;
            tenPullButton = null;
            paidSinglePullButton = null;
            paidTenPullButton = null;
            summonTutorialGuideRoot = null;
            summonTutorialGuideTitleText = null;
            summonTutorialGuideBodyText = null;
            summonTutorialGuideFooterText = null;
            summonTutorialGuideCharacterImage = null;
            summonTutorialSinglePullHighlight = null;
            effectCanvasGroup = null;
            effectImage = null;
            effectFlashImage = null;
            effectImageRect = null;
            resultMonsterImage = null;
            resultMonsterShadowImage = null;
            resultMonsterRect = null;
            resultTitleText = null;
            resultMonsterNameText = null;
            resultClassText = null;
            resultSummaryText = null;
            resultAgainButton = null;
            resultBackButton = null;
            resultHomeButton = null;
            resultTutorialGuideRoot = null;
            resultTutorialGuideCharacterImage = null;
            resultTutorialHomeHighlight = null;
            resultSlotViews.Clear();
            effectRoutine = null;
            contractInProgress = false;
            lastRequestedCount = 1;
            lastUsedPaidStones = false;

            Image rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;
            }

            contractHomeRoot = CreateUiObject("GachaContractHomeRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateFullScreenImage("GachaBackground", contractHomeRoot.transform, BackgroundSpritePath, new Color(0.02f, 0.025f, 0.045f, 1f));
            CreateStretchImage("TopShadow", contractHomeRoot.transform, Vector2.up, Vector2.one, new Vector2(0f, -390f), Vector2.zero, new Color(0f, 0f, 0f, 0.50f));
            CreateStretchImage("BottomShadow", contractHomeRoot.transform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0f, 520f), new Color(0f, 0f, 0f, 0.58f));

            GameObject titlePanel = CreatePanel("SummonTitlePanel", contractHomeRoot.transform, null,
                new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(830f, 190f), PanelColor);
            CreateText("Title", titlePanel.transform, "契約召喚", 66, FontStyle.Bold,
                new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(700f, 82f), GoldTextColor);
            CreateText("SubTitle", titlePanel.transform, "契約炉の召喚円から、次の探索者を呼び出す", 25, FontStyle.Bold,
                new Vector2(0.5f, 0.26f), Vector2.zero, new Vector2(730f, 42f), PaleTextColor);

            GameObject ticketPanel = CreatePanel("GachaTicketPanel", contractHomeRoot.transform, null,
                new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(860f, 82f), DeepPanelColor);
            freeStoneText = CreateStoneCounter(ticketPanel.transform, "FreeStoneCounter", FreeStoneIconPath,
                new Vector2(-286f, 0f));
            paidStoneText = CreateStoneCounter(ticketPanel.transform, "PaidStoneCounter", PaidStoneIconPath,
                new Vector2(0f, 0f));
            storageText = CreateText("StorageCount", ticketPanel.transform, "所持 0/0", 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(292f, 0f), new Vector2(220f, 42f), PaleTextColor);

            ritePanelRoot = CreatePanel("ContractRitePanel", contractHomeRoot.transform, null,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -116f), new Vector2(780f, 250f), new Color(0.018f, 0.014f, 0.020f, 0.54f));
            CreateText("RiteLabel", ritePanelRoot.transform, "契約陣 起動待機", 34, FontStyle.Bold,
                new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(620f, 50f), GoldTextColor);
            statusText = CreateText("RiteStatus", ritePanelRoot.transform, "未召喚", 24, FontStyle.Bold,
                new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(700f, 138f), PaleTextColor);

            GameObject benefitPanel = CreatePanel("GachaBenefitPanel", contractHomeRoot.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0f, 416f), new Vector2(820f, 214f), DeepPanelColor);
            CreateText("BenefitTitle", benefitPanel.transform, "契約特典", 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(480f, 36f), GoldTextColor);
            CreateContractBenefitCard(
                benefitPanel.transform,
                "StandardContractBenefit",
                FreeStoneIconPath,
                "通常契約",
                string.Empty,
                new Vector2(-195f, -18f),
                new Color(0.32f, 0.68f, 0.86f, 1f));
            CreateContractBenefitCard(
                benefitPanel.transform,
                "PremiumContractBenefit",
                PaidStoneIconPath,
                "上質契約",
                "高個体値が出やすい\n10回: クラス3を1体保証",
                new Vector2(195f, -18f),
                new Color(0.80f, 0.48f, 0.92f, 1f));

            singlePullButton = CreateStoneCostButton("FreeSinglePullButton", contractHomeRoot.transform, 1, false,
                new Vector2(-205f, 246f), () => RunContract(1, false));
            tenPullButton = CreateStoneCostButton("FreeTenPullButton", contractHomeRoot.transform, 10, false,
                new Vector2(-205f, 136f), () => RunContract(10, false));
            paidSinglePullButton = CreateStoneCostButton("PaidSinglePullButton", contractHomeRoot.transform, 1, true,
                new Vector2(205f, 246f), () => RunContract(1, true));
            paidTenPullButton = CreateStoneCostButton("PaidTenPullButton", contractHomeRoot.transform, 10, true,
                new Vector2(205f, 136f), () => RunContract(10, true));
            CaptureSinglePullButtonBackground();
            Button closeButton = HomeReturnButtonStyle.Create(contractHomeRoot.transform, Close);
            BuildSummonTutorialGuide();
            singlePullButton.interactable = true;
            tenPullButton.interactable = true;
            paidSinglePullButton.interactable = true;
            paidTenPullButton.interactable = true;
            closeButton.interactable = true;

            CreateContractEffectOverlay();
        }

        private void BuildSummonTutorialGuide()
        {
            if (contractHomeRoot == null || singlePullButton == null || summonTutorialGuideRoot != null)
            {
                return;
            }

            summonTutorialGuideRoot = CreatePanel(
                "GachaSummonTutorialGuideRoot",
                contractHomeRoot.transform,
                null,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 608f),
                new Vector2(940f, 218f),
                new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Outline panelOutline = summonTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            summonTutorialGuideCharacterImage = CreateImage(
                "GachaSummonTutorialGuideLuse",
                summonTutorialGuideRoot.transform,
                TutorialGuideSpritePath,
                new Vector2(0f, 0.5f),
                new Vector2(96f, -4f),
                new Vector2(172f, 172f),
                true,
                Color.white);

            Text badgeText = CreateText(
                "GachaSummonTutorialGuideBadge",
                summonTutorialGuideRoot.transform,
                "TUTORIAL",
                16,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(318f, -24f),
                new Vector2(140f, 28f),
                GoldTextColor);
            badgeText.alignment = TextAnchor.MiddleLeft;

            summonTutorialGuideTitleText = CreateText(
                "GachaSummonTutorialGuideTitle",
                summonTutorialGuideRoot.transform,
                "ルシェの召喚案内",
                28,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(560f, -60f),
                new Vector2(620f, 42f),
                new Color(1f, 0.96f, 0.78f, 1f));
            summonTutorialGuideTitleText.alignment = TextAnchor.MiddleLeft;

            summonTutorialGuideBodyText = CreateText(
                "GachaSummonTutorialGuideBody",
                summonTutorialGuideRoot.transform,
                "今回の召喚用に、魔晶石を900個用意しました。\n契約炉に石を捧げて、最初の探索隊を3体そろえましょう。",
                20,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(560f, -116f),
                new Vector2(620f, 70f),
                PaleTextColor);
            summonTutorialGuideBodyText.alignment = TextAnchor.UpperLeft;
            summonTutorialGuideBodyText.resizeTextMinSize = 15;

            summonTutorialGuideFooterText = CreateText(
                "GachaSummonTutorialGuideFooter",
                summonTutorialGuideRoot.transform,
                "次の操作: 「1回 / 300個」をタップ（あと3体）",
                17,
                FontStyle.Bold,
                new Vector2(0f, 0f),
                new Vector2(560f, 24f),
                new Vector2(620f, 32f),
                new Color(0.78f, 0.92f, 1f, 1f));
            summonTutorialGuideFooterText.alignment = TextAnchor.MiddleLeft;

            summonTutorialSinglePullHighlight = CreateTutorialImageFrame(
                "GachaSummonTutorialSinglePullHighlight",
                singlePullButton.transform,
                new Vector2(364f, 118f));

            summonTutorialGuideRoot.SetActive(false);
            summonTutorialSinglePullHighlight.gameObject.SetActive(false);
        }

        private static Image CreateTutorialImageFrame(string name, Transform parent, Vector2 size)
        {
            GameObject imageObject = CreateUiObject(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);

            Image image = imageObject.AddComponent<Image>();
            image.sprite = LoadSpriteIncludingTexture(TutorialHighlightFramePath);
            image.color = image.sprite != null ? Color.white : new Color(1f, 0.78f, 0.12f, 0.96f);
            image.preserveAspect = false;
            image.raycastTarget = false;
            imageObject.transform.SetAsLastSibling();
            return image;
        }

        private void CaptureSinglePullButtonBackground()
        {
            singlePullButtonBackgroundImage = singlePullButton != null ? singlePullButton.GetComponent<Image>() : null;
            if (singlePullButtonBackgroundImage == null)
            {
                return;
            }

            singlePullButtonOriginalSprite = singlePullButtonBackgroundImage.sprite;
            singlePullButtonOriginalColor = singlePullButtonBackgroundImage.color;
            singlePullButtonOriginalType = singlePullButtonBackgroundImage.type;
            singlePullButtonBackgroundMuted = false;
        }

        private void SetSinglePullButtonBackgroundMuted(bool muted)
        {
            if (singlePullButtonBackgroundImage == null || singlePullButtonBackgroundMuted == muted)
            {
                return;
            }

            singlePullButtonBackgroundMuted = muted;
            if (muted)
            {
                singlePullButtonBackgroundImage.sprite = null;
                singlePullButtonBackgroundImage.type = Image.Type.Simple;
                singlePullButtonBackgroundImage.color = new Color(0.32f, 0.20f, 0.07f, 0.90f);
                return;
            }

            singlePullButtonBackgroundImage.sprite = singlePullButtonOriginalSprite;
            singlePullButtonBackgroundImage.type = singlePullButtonOriginalType;
            singlePullButtonBackgroundImage.color = singlePullButtonOriginalColor;
        }

        private void Update()
        {
            AnimateSummonTutorialGuide();
            AnimateResultTutorialGuide();
        }

        private void UpdatePreviewState()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (StoryTutorialService.EnsureInitialSummonResources(profile))
            {
                SaveManager.Instance?.SaveCurrentGame();
            }

            UpdateInventoryHeader();

            if (statusText != null)
            {
                statusText.text = BuildPreviewStatusText(profile);
            }

            SetPullButtonsInteractable(!contractInProgress);
            RefreshSummonTutorialGuide();
        }

        private void UpdateInventoryHeader()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile != null)
            {
                if (freeStoneText != null)
                {
                    freeStoneText.text = profile.FreeGachaStones.ToString("N0");
                }

                if (paidStoneText != null)
                {
                    paidStoneText.text = profile.PaidGachaStones.ToString("N0");
                }

                if (storageText != null)
                {
                    int ownedCount = profile.OwnedMonsters != null ? profile.OwnedMonsters.Count : 0;
                    storageText.text = $"所持 {ownedCount}/{Mathf.Max(1, profile.MonsterStorageLimit)}";
                }
            }
            else
            {
                if (freeStoneText != null) freeStoneText.text = "-";
                if (paidStoneText != null) paidStoneText.text = "-";
                if (storageText != null) storageText.text = "読込待ち";
            }
        }

        private static string BuildPreviewStatusText(PlayerProfile profile)
        {
            if (!Application.isPlaying)
            {
                return "エディタプレビュー";
            }

            if (profile == null)
            {
                return "セーブ読込待ち";
            }

            if (GetAvailableMonsterStorageSlots(profile) <= 0)
            {
                return "所持枠がいっぱいです";
            }

            int singleCost = GetContractCost(1);
            if (!profile.CanSpendFreeGachaStones(singleCost) && !profile.CanSpendPaidGachaStones(singleCost))
            {
                return "魔晶石が足りません";
            }

            return "契約可能";
        }

        private void RunContract(int count, bool usePaidStones)
        {
            if (contractInProgress)
            {
                return;
            }

            int requestedCount = Mathf.Max(1, count);
            if (!Application.isPlaying)
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                SetStatus("再生中に契約できます");
                return;
            }

            EnsureRuntimeState();
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                SetStatus("セーブデータを読み込めませんでした");
                return;
            }

            StoryTutorialService.EnsureInitialSummonResources(profile);

            int availableSlots = GetAvailableMonsterStorageSlots(profile);
            if (availableSlots < requestedCount)
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                UpdateInventoryHeader();
                SetPullButtonsInteractable(true);
                SetStatus(BuildStorageShortageText(requestedCount, availableSlots));
                return;
            }

            int stoneCost = GetContractCost(requestedCount);
            if (!CanSpendGachaStones(profile, stoneCost, usePaidStones))
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                UpdateInventoryHeader();
                SetPullButtonsInteractable(true);
                SetStatus(BuildStoneShortageText(profile, stoneCost, usePaidStones));
                return;
            }

            List<MonsterDataSO> summonPool = CollectSummonPool(usePaidStones);
            if (summonPool.Count == 0)
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                SetStatus("召喚候補が登録されていません");
                return;
            }

            if (!SpendGachaStones(profile, stoneCost, usePaidStones))
            {
                AudioManager.Instance?.PlaySe(AudioCue.Error);
                UpdateInventoryHeader();
                SetPullButtonsInteractable(true);
                SetStatus(BuildStoneShortageText(profile, stoneCost, usePaidStones));
                return;
            }

            int actualCount = requestedCount;
            lastRequestedCount = requestedCount;
            lastUsedPaidStones = usePaidStones;
            SetStatus("契約空間へ転移中");
            AudioManager.Instance?.PlaySe(AudioCue.GachaStart);
            var results = new List<MonsterDataSO>();
            bool paidTenPullGuarantee = usePaidStones && requestedCount >= 10;
            bool isInitialSummonTutorial = !usePaidStones &&
                !profile.HasCompletedTutorial &&
                string.Equals(profile.TutorialStepId, StoryTutorialService.StepFirstSummon, StringComparison.Ordinal);
            bool hasGuaranteedClass = false;
            for (int i = 0; i < actualCount; i += 1)
            {
                int remainingPulls = actualCount - i;
                bool shouldForceGuarantee = paidTenPullGuarantee && !hasGuaranteedClass && remainingPulls <= 1;
                MonsterDataSO result = isInitialSummonTutorial
                    ? DrawInitialTutorialMonster(summonPool, profile)
                    : shouldForceGuarantee
                        ? DrawGuaranteedClass(summonPool, PaidTenPullGuaranteedClassRank)
                        : DrawMonster(summonPool, usePaidStones);
                if (result == null)
                {
                    continue;
                }

                hasGuaranteedClass = hasGuaranteedClass || Mathf.Max(1, result.classRank) == PaidTenPullGuaranteedClassRank;
                OwnedMonsterData addedMonster = profile.AddOwnedMonster(result.monsterId, 1);
                if (addedMonster != null)
                {
                    if (isInitialSummonTutorial)
                    {
                        MonsterIndividualValueService.Apply(
                            addedMonster,
                            new MonsterIndividualValues(
                                TutorialStarterIndividualValue,
                                TutorialStarterIndividualValue,
                                TutorialStarterIndividualValue,
                                TutorialStarterIndividualValue,
                                TutorialStarterIndividualValue,
                                TutorialStarterIndividualValue));
                        profile.InitialTutorialSummonCount = Math.Min(
                            StoryTutorialService.InitialSummonCount,
                            profile.InitialTutorialSummonCount + 1);
                    }
                    else if (usePaidStones)
                    {
                        MonsterIndividualValueService.Apply(addedMonster, MonsterIndividualValueService.RollHighQuality());
                    }

                    results.Add(result);
                }
            }

            if (results.Count > 0 && StoryTutorialService.HasCompletedInitialSummons(profile))
            {
                StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepFirstSummon);
            }

            SaveManager.Instance?.SaveCurrentGame();
            UpdateInventoryHeader();
            StartContractEffect(results, requestedCount, actualCount);
        }

        private static int GetContractCost(int requestedCount)
        {
            return Mathf.Max(1, requestedCount) * GachaStoneCostPerPull;
        }

        private static bool CanSpendGachaStones(PlayerProfile profile, int stoneCost, bool usePaidStones)
        {
            if (profile == null)
            {
                return false;
            }

            return usePaidStones
                ? profile.CanSpendPaidGachaStones(stoneCost)
                : profile.CanSpendFreeGachaStones(stoneCost);
        }

        private static bool SpendGachaStones(PlayerProfile profile, int stoneCost, bool usePaidStones)
        {
            if (profile == null)
            {
                return false;
            }

            return usePaidStones
                ? profile.TrySpendPaidGachaStones(stoneCost)
                : profile.TrySpendFreeGachaStones(stoneCost);
        }

        private static string BuildStoneShortageText(PlayerProfile profile, int stoneCost, bool usePaidStones)
        {
            int owned = 0;
            if (profile != null)
            {
                owned = usePaidStones ? profile.PaidGachaStones : profile.FreeGachaStones;
            }

            return $"石が足りません（所持 {Mathf.Max(0, owned)} / 必要 {Mathf.Max(0, stoneCost)}）";
        }

        private static int GetAvailableMonsterStorageSlots(PlayerProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int ownedCount = profile.OwnedMonsters != null ? profile.OwnedMonsters.Count : 0;
            return Mathf.Max(0, profile.MonsterStorageLimit - ownedCount);
        }

        private static string BuildStorageShortageText(int requestedCount, int availableSlots)
        {
            int required = Mathf.Max(1, requestedCount);
            int available = Mathf.Max(0, availableSlots);
            if (available <= 0)
            {
                return "所持枠がいっぱいです";
            }

            return $"所持枠が足りません（空き {available} / 必要 {required}）";
        }

        private void Close()
        {
            SetResultStageInputActive(false);
            gameObject.SetActive(false);
            closeAction?.Invoke();
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, string spritePath, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color fallbackColor)
        {
            GameObject panel = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Image image = panel.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return panel;
        }

        private static Image CreateFullScreenImage(string name, Transform parent, string spritePath, Color fallbackColor)
        {
            GameObject imageObject = CreateUiObject(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image image = imageObject.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateStretchImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateImage(string name, Transform parent, string spritePath, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool preserveAspect, Color color)
        {
            GameObject imageObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Image image = imageObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.color = image.sprite != null ? color : new Color(color.r, color.g, color.b, 0.18f);
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateSpriteButton(string name, Transform parent, string spritePath, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateUiObject(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPosition, size);
            Image image = buttonObject.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0.34f, 0.19f, 0.08f, 0.95f);
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = true;
            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            CreateText("Label", buttonObject.transform, label, 29, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 18f), Color.white);
            return button;
        }

        private static Button CreateStoneCostButton(
            string name,
            Transform parent,
            int pullCount,
            bool usePaidStones,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            Vector2 buttonSize = new Vector2(340f, 92f);
            string label = $"{Mathf.Max(1, pullCount)}回\n{GetContractCost(pullCount)}個";
            Button button = CreateSpriteButton(name, parent, PullButtonSpritePath, label, anchoredPosition, buttonSize, onClick);
            Text labelText = button.transform.Find("Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                RectTransform labelRect = labelText.rectTransform;
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(230f, 74f);
                labelText.fontSize = 29;
                labelText.resizeTextMaxSize = labelText.fontSize;
            }

            CreateImage(
                "StoneIcon",
                button.transform,
                usePaidStones ? PaidStoneIconPath : FreeStoneIconPath,
                new Vector2(0.14f, 0.5f),
                Vector2.zero,
                new Vector2(52f, 52f),
                true,
                Color.white);
            return button;
        }

        private static Text CreateStoneCounter(Transform parent, string name, string iconPath, Vector2 anchoredPosition)
        {
            GameObject root = CreatePanel(name, parent, null, new Vector2(0.5f, 0.5f), anchoredPosition,
                new Vector2(252f, 58f), new Color(0.018f, 0.020f, 0.026f, 0.84f));
            Image rootImage = root.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.raycastTarget = false;
            }

            CreateImage("Icon", root.transform, iconPath, new Vector2(0f, 0.5f),
                new Vector2(36f, 0f), new Vector2(54f, 54f), true, Color.white);
            return CreateText("Amount", root.transform, "0", 23, FontStyle.Bold,
                new Vector2(0.62f, 0.5f), new Vector2(0f, 0f), new Vector2(156f, 38f), Color.white);
        }

        private static void CreateContractBenefitCard(
            Transform parent,
            string name,
            string iconPath,
            string title,
            string detail,
            Vector2 anchoredPosition,
            Color accentColor)
        {
            GameObject chip = CreatePanel(name, parent, null, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(350f, 112f), new Color(0.08f, 0.09f, 0.13f, 0.92f));
            Image image = chip.GetComponent<Image>();
            image.color = new Color(accentColor.r * 0.18f, accentColor.g * 0.18f, accentColor.b * 0.18f, 0.94f);
            CreateImage("StoneIcon", chip.transform, iconPath, new Vector2(0.16f, 0.5f), Vector2.zero, new Vector2(68f, 68f), true, Color.white);
            CreateText("Title", chip.transform, title, 23, FontStyle.Bold, new Vector2(0.62f, 0.69f), Vector2.zero, new Vector2(230f, 32f), accentColor);
            CreateText("Detail", chip.transform, detail, 17, FontStyle.Bold, new Vector2(0.62f, 0.30f), Vector2.zero, new Vector2(230f, 48f), Color.white);
        }

        private void CreateResultSlotGrid(Transform parent)
        {
            float[] xPositions = { -392f, -196f, 0f, 196f, 392f };
            float[] yPositions = { -648f, -790f };
            for (int row = 0; row < yPositions.Length; row += 1)
            {
                for (int column = 0; column < xPositions.Length; column += 1)
                {
                    int index = (row * xPositions.Length) + column;
                    resultSlotViews.Add(CreateResultSlot(parent, index, new Vector2(xPositions[column], yPositions[row])));
                }
            }
        }

        private static ResultSlotView CreateResultSlot(Transform parent, int index, Vector2 anchoredPosition)
        {
            GameObject slot = CreatePanel("ResultSlot_" + index, parent, null,
                new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(174f, 126f), new Color(0.028f, 0.024f, 0.035f, 0.90f));
            Image frame = slot.GetComponent<Image>();

            GameObject portraitObject = CreateUiObject("Portrait", slot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(78f, 78f));
            Image portrait = portraitObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = Color.clear;

            Text classLabel = CreateText("ClassLabel", slot.transform, string.Empty, 18, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(130f, 24f), GoldTextColor);
            Text nameLabel = CreateText("NameLabel", slot.transform, string.Empty, 16, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(148f, 32f), Color.white);

            slot.SetActive(false);
            return new ResultSlotView
            {
                Root = slot,
                Frame = frame,
                Portrait = portrait,
                ClassLabel = classLabel,
                NameLabel = nameLabel
            };
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject textObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Text label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            return gameObject;
        }

        private static Sprite LoadSprite(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        private static Sprite LoadSpriteIncludingTexture(string path)
        {
            Sprite sprite = LoadSprite(path);
            if (sprite != null || string.IsNullOrEmpty(path))
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(path);
            return texture != null
                ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f)
                : null;
        }

        private void CreateContractEffectOverlay()
        {
            resultStageRoot = CreateUiObject("GachaResultStageRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            effectCanvasGroup = resultStageRoot.AddComponent<CanvasGroup>();
            effectCanvasGroup.alpha = 0f;
            effectCanvasGroup.blocksRaycasts = false;
            effectCanvasGroup.interactable = false;

            CreateFullScreenImage("GachaResultBackground", resultStageRoot.transform, BackgroundSpritePath, new Color(0.008f, 0.010f, 0.018f, 1f));
            CreateStretchImage("GachaResultDarkVeil", resultStageRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.42f));
            CreateStretchImage("GachaResultTopVignette", resultStageRoot.transform, Vector2.up, Vector2.one, new Vector2(0f, -520f), Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
            CreateStretchImage("GachaResultBottomVignette", resultStageRoot.transform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0f, 600f), new Color(0f, 0f, 0f, 0.70f));

            effectFlashImage = CreateStretchImage("GachaResultFlash", resultStageRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            effectFlashImage.raycastTarget = false;

            resultTitleText = CreateText("ResultTitle", resultStageRoot.transform, "契約空間へ転移", 48, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(820f, 70f), GoldTextColor);
            resultSummaryText = CreateText("ResultSummary", resultStageRoot.transform, string.Empty, 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -224f), new Vector2(850f, 48f), PaleTextColor);

            GameObject effectObject = CreateUiObject("GachaResultEffectImage", resultStageRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(1020f, 1020f));
            effectImageRect = effectObject.GetComponent<RectTransform>();
            effectImage = effectObject.AddComponent<Image>();
            effectImage.preserveAspect = true;
            effectImage.raycastTarget = false;
            effectImage.color = Color.clear;

            GameObject monsterShadowObject = CreateUiObject("ResultMonsterShadow", resultStageRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, -162f), new Vector2(550f, 550f));
            resultMonsterShadowImage = monsterShadowObject.AddComponent<Image>();
            resultMonsterShadowImage.preserveAspect = true;
            resultMonsterShadowImage.raycastTarget = false;
            resultMonsterShadowImage.color = Color.clear;

            GameObject monsterObject = CreateUiObject("ResultMonster", resultStageRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(560f, 560f));
            resultMonsterRect = monsterObject.GetComponent<RectTransform>();
            resultMonsterImage = monsterObject.AddComponent<Image>();
            resultMonsterImage.preserveAspect = true;
            resultMonsterImage.raycastTarget = false;
            resultMonsterImage.color = Color.clear;

            resultMonsterNameText = CreateText("ResultMonsterName", resultStageRoot.transform, string.Empty, 40, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -452f), new Vector2(840f, 62f), Color.white);
            resultClassText = CreateText("ResultClass", resultStageRoot.transform, string.Empty, 31, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -510f), new Vector2(700f, 50f), GoldTextColor);

            CreateResultSlotGrid(resultStageRoot.transform);

            resultAgainButton = CreateSpriteButton("ResultAgainButton", resultStageRoot.transform, PullButtonSpritePath, "もう一度契約",
                new Vector2(-190f, 82f), new Vector2(350f, 84f), () => RunContract(lastRequestedCount, lastUsedPaidStones));
            resultBackButton = CreateSpriteButton("ResultBackButton", resultStageRoot.transform, SmallButtonSpritePath, "契約画面へ",
                new Vector2(210f, 82f), new Vector2(310f, 84f), ReturnToContractHome);
            resultHomeButton = HomeReturnButtonStyle.Create(resultStageRoot.transform, "ResultHomeReturnButton", Close);
            BuildResultTutorialGuide();

            SetResultButtonsVisible(false);
            SetResultStageInputActive(false);
            resultStageRoot.SetActive(false);
        }

        private void BuildResultTutorialGuide()
        {
            if (resultStageRoot == null || resultHomeButton == null || resultTutorialGuideRoot != null)
            {
                return;
            }

            resultTutorialGuideRoot = CreatePanel(
                "GachaResultTutorialGuideRoot",
                resultStageRoot.transform,
                null,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 226f),
                new Vector2(920f, 220f),
                new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Outline panelOutline = resultTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            resultTutorialGuideCharacterImage = CreateImage(
                "GachaResultTutorialGuideLuse",
                resultTutorialGuideRoot.transform,
                TutorialGuideSpritePath,
                new Vector2(0f, 0.5f),
                new Vector2(92f, -4f),
                new Vector2(164f, 164f),
                true,
                Color.white);

            Text badgeText = CreateText(
                "GachaResultTutorialGuideBadge",
                resultTutorialGuideRoot.transform,
                "TUTORIAL",
                16,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(318f, -24f),
                new Vector2(140f, 28f),
                GoldTextColor);
            badgeText.alignment = TextAnchor.MiddleLeft;

            Text titleText = CreateText(
                "GachaResultTutorialGuideTitle",
                resultTutorialGuideRoot.transform,
                "召喚に成功しました！",
                28,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(560f, -62f),
                new Vector2(620f, 42f),
                new Color(1f, 0.96f, 0.78f, 1f));
            titleText.alignment = TextAnchor.MiddleLeft;

            Text bodyText = CreateText(
                "GachaResultTutorialGuideBody",
                resultTutorialGuideRoot.transform,
                "仲間を呼び戻せました。\n次はホームで探索隊を編成します。",
                20,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(560f, -116f),
                new Vector2(620f, 70f),
                PaleTextColor);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.resizeTextMinSize = 15;

            Text footerText = CreateText(
                "GachaResultTutorialGuideFooter",
                resultTutorialGuideRoot.transform,
                "次の操作: 左上の「ホームへ戻る」をタップ",
                17,
                FontStyle.Bold,
                new Vector2(0f, 0f),
                new Vector2(560f, 24f),
                new Vector2(620f, 32f),
                new Color(0.78f, 0.92f, 1f, 1f));
            footerText.alignment = TextAnchor.MiddleLeft;

            resultTutorialHomeHighlight = CreateTutorialImageFrame(
                "GachaResultTutorialHomeHighlight",
                resultHomeButton.transform,
                HomeReturnButtonStyle.Size + new Vector2(34f, 30f));
            resultTutorialHomeHighlight.raycastTarget = false;
            resultTutorialHomeHighlight.transform.SetAsLastSibling();

            resultTutorialGuideRoot.SetActive(false);
            resultTutorialHomeHighlight.gameObject.SetActive(false);
        }

        private void StartContractEffect(List<MonsterDataSO> results, int requestedCount, int actualCount)
        {
            if (effectRoutine != null)
            {
                StopCoroutine(effectRoutine);
                effectRoutine = null;
            }

            effectRoutine = StartCoroutine(PlayContractEffect(results, requestedCount, actualCount));
        }

        private IEnumerator PlayContractEffect(List<MonsterDataSO> results, int requestedCount, int actualCount)
        {
            contractInProgress = true;
            SetPullButtonsInteractable(false);
            lastRequestedCount = Mathf.Max(1, requestedCount);

            ContractEffectTier tier = ResolveEffectTier(results);
            MonsterDataSO featuredMonster = ResolveFeaturedResult(results);
            string resultText = BuildResultText(results, requestedCount, actualCount);
            Sprite effectSprite = LoadSprite(GetEffectSpritePath(tier));
            if (effectSprite == null || effectCanvasGroup == null || effectImage == null || effectFlashImage == null || effectImageRect == null || resultStageRoot == null)
            {
                AudioManager.Instance?.PlaySe(ResolveGachaRevealCue(tier));
                SetStatus(resultText);
                SetPullButtonsInteractable(true);
                contractInProgress = false;
                effectRoutine = null;
                yield break;
            }

            if (contractHomeRoot != null)
            {
                contractHomeRoot.SetActive(false);
            }

            resultStageRoot.SetActive(true);
            SetResultStageInputActive(true);
            SetResultButtonsVisible(false);
            PopulateResultStage(results, featuredMonster, requestedCount, actualCount, tier);
            effectImage.sprite = effectSprite;
            effectImage.color = Color.white;
            effectImageRect.localScale = Vector3.one * GetEffectStartScale(tier);
            effectImageRect.localEulerAngles = Vector3.zero;
            effectFlashImage.color = Color.clear;
            effectCanvasGroup.alpha = 0f;

            float duration = GetEffectDuration(tier);
            float spin = GetEffectSpin(tier);
            Color flashColor = GetEffectFlashColor(tier);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                float pulse = Mathf.Sin(t * Mathf.PI * GetEffectPulseCount(tier));
                float scale = Mathf.Lerp(GetEffectStartScale(tier), GetEffectEndScale(tier), ease) + Mathf.Max(0f, pulse) * GetEffectPulseScale(tier);
                float fadeIn = Mathf.Clamp01(t / 0.18f);
                float flash = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI));
                flash *= GetEffectFlashStrength(tier);

                effectCanvasGroup.alpha = fadeIn;
                effectImageRect.localScale = Vector3.one * scale;
                effectImageRect.localEulerAngles = new Vector3(0f, 0f, spin * ease);
                effectImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, fadeIn));
                effectFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, flash * 0.30f);
                yield return null;
            }

            if (resultTitleText != null)
            {
                resultTitleText.text = GetEffectRevealTitle(tier);
            }

            AudioManager.Instance?.PlaySe(ResolveGachaRevealCue(tier));

            if (resultSummaryText != null)
            {
                resultSummaryText.text = BuildResultSummaryText(results, featuredMonster, requestedCount, actualCount);
                resultSummaryText.color = PaleTextColor;
            }

            float revealElapsed = 0f;
            const float revealDuration = 0.56f;
            while (revealElapsed < revealDuration)
            {
                revealElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(revealElapsed / revealDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                effectCanvasGroup.alpha = 1f;
                effectImageRect.localScale = Vector3.one * Mathf.Lerp(GetEffectEndScale(tier) + 0.16f, GetEffectEndScale(tier) * 0.92f, ease);
                effectImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, GetEffectHoldAlpha(tier), ease));
                effectFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, (1f - t) * GetEffectFlashStrength(tier) * 0.46f);
                SetMonsterRevealAlpha(ease);
                if (resultMonsterRect != null)
                {
                    resultMonsterRect.localScale = Vector3.one * Mathf.Lerp(0.70f, 1f, ease);
                }

                yield return null;
            }

            effectCanvasGroup.alpha = 1f;
            effectImage.color = new Color(1f, 1f, 1f, GetEffectHoldAlpha(tier));
            effectFlashImage.color = Color.clear;
            SetMonsterRevealAlpha(1f);
            ShowResultSlots(results);
            SetResultButtonsVisible(true);
            SetPullButtonsInteractable(true);
            contractInProgress = false;
            effectRoutine = null;
        }

        private void SetPullButtonsInteractable(bool interactable)
        {
            if (singlePullButton != null)
            {
                singlePullButton.interactable = interactable && CanRequestContract(1, false);
            }

            if (tenPullButton != null)
            {
                tenPullButton.interactable = interactable && CanRequestContract(10, false);
            }

            if (paidSinglePullButton != null)
            {
                paidSinglePullButton.interactable = interactable && CanRequestContract(1, true);
            }

            if (paidTenPullButton != null)
            {
                paidTenPullButton.interactable = interactable && CanRequestContract(10, true);
            }
        }

        private static bool CanRequestContract(int requestedCount, bool usePaidStones)
        {
            if (!Application.isPlaying)
            {
                return true;
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            int normalizedCount = Mathf.Max(1, requestedCount);
            bool isFirstSummonTutorial = profile != null &&
                !profile.HasCompletedTutorial &&
                profile.TutorialStepId == StoryTutorialService.StepFirstSummon;
            if (isFirstSummonTutorial && (normalizedCount != 1 || usePaidStones))
            {
                return false;
            }

            return GetAvailableMonsterStorageSlots(profile) >= normalizedCount &&
                CanSpendGachaStones(profile, GetContractCost(normalizedCount), usePaidStones);
        }

        private void RefreshSummonTutorialGuide()
        {
            StoryTutorialEvent tutorialEvent = GetFirstSummonTutorialEvent();
            bool shouldShow = tutorialEvent != null &&
                !contractInProgress &&
                contractHomeRoot != null &&
                contractHomeRoot.activeInHierarchy;

            if (ritePanelRoot != null)
            {
                ritePanelRoot.SetActive(!shouldShow);
            }

            SetSinglePullButtonBackgroundMuted(shouldShow);

            if (summonTutorialGuideRoot != null)
            {
                summonTutorialGuideRoot.SetActive(shouldShow);
                if (shouldShow)
                {
                    summonTutorialGuideRoot.transform.SetAsLastSibling();
                }
            }

            if (summonTutorialGuideTitleText != null && shouldShow)
            {
                summonTutorialGuideTitleText.text = "ルシェの召喚案内";
            }

            if (summonTutorialGuideBodyText != null && shouldShow)
            {
                summonTutorialGuideBodyText.text = tutorialEvent.Body;
            }

            if (summonTutorialGuideFooterText != null && shouldShow)
            {
                PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
                int remaining = StoryTutorialService.GetInitialSummonRemainingCount(profile);
                summonTutorialGuideFooterText.text = $"次の操作: 「1回 / 300個」をタップ（あと{remaining}体）";
            }

            if (summonTutorialSinglePullHighlight != null)
            {
                summonTutorialSinglePullHighlight.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    summonTutorialSinglePullHighlight.transform.SetAsLastSibling();
                }
            }

            if (shouldShow && singlePullButton != null)
            {
                singlePullButton.transform.SetAsLastSibling();
            }
        }

        private static StoryTutorialEvent GetFirstSummonTutorialEvent()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            StoryTutorialEvent tutorialEvent = StoryTutorialService.GetNextEvent(profile, "GachaScene");
            if (tutorialEvent == null || !tutorialEvent.IsValid)
            {
                return null;
            }

            return tutorialEvent.StepId == StoryTutorialService.StepFirstSummon &&
                string.Equals(tutorialEvent.TargetKey, "gacha.single_free", StringComparison.Ordinal)
                    ? tutorialEvent
                    : null;
        }

        private void AnimateSummonTutorialGuide()
        {
            if (summonTutorialGuideRoot == null || !summonTutorialGuideRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            if (summonTutorialGuideCharacterImage != null)
            {
                float characterScale = Mathf.Lerp(0.985f, 1.035f, pulse);
                summonTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(characterScale, characterScale, 1f);
            }

            if (summonTutorialSinglePullHighlight != null && summonTutorialSinglePullHighlight.gameObject.activeSelf)
            {
                float frameScale = Mathf.Lerp(0.99f, 1.035f, pulse);
                summonTutorialSinglePullHighlight.rectTransform.localScale = new Vector3(frameScale, frameScale, 1f);
                summonTutorialSinglePullHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.82f, 1f, pulse));
            }
        }

        private void ReturnToContractHome()
        {
            if (contractInProgress)
            {
                return;
            }

            if (resultStageRoot != null)
            {
                resultStageRoot.SetActive(false);
            }

            SetResultStageInputActive(false);

            if (contractHomeRoot != null)
            {
                contractHomeRoot.SetActive(true);
            }

            SetResultButtonsVisible(false);
            SetPullButtonsInteractable(true);
            UpdatePreviewState();
        }

        private void SetResultButtonsVisible(bool visible)
        {
            bool showTutorialGuide = visible && ShouldShowResultTutorialGuide();
            if (resultAgainButton != null)
            {
                resultAgainButton.gameObject.SetActive(visible && !showTutorialGuide);
                resultAgainButton.interactable = visible && !showTutorialGuide && CanRequestContract(lastRequestedCount, lastUsedPaidStones);
            }

            if (resultBackButton != null)
            {
                resultBackButton.gameObject.SetActive(visible && !showTutorialGuide);
                resultBackButton.interactable = visible && !showTutorialGuide;
            }

            if (resultHomeButton != null)
            {
                resultHomeButton.gameObject.SetActive(visible);
                resultHomeButton.interactable = visible;
            }

            RefreshResultTutorialGuide(showTutorialGuide);
        }

        private void SetResultStageInputActive(bool active)
        {
            if (effectCanvasGroup == null)
            {
                return;
            }

            effectCanvasGroup.blocksRaycasts = active;
            effectCanvasGroup.interactable = active;
            if (!active)
            {
                effectCanvasGroup.alpha = 0f;
            }
        }

        private static bool ShouldShowResultTutorialGuide()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            return profile != null &&
                !profile.HasCompletedTutorial &&
                string.Equals(profile.TutorialStepId, StoryTutorialService.StepFirstExplorationIntro, StringComparison.Ordinal);
        }

        private void RefreshResultTutorialGuide(bool visible)
        {
            if (resultTutorialGuideRoot != null)
            {
                resultTutorialGuideRoot.SetActive(visible);
                if (visible)
                {
                    resultTutorialGuideRoot.transform.SetAsLastSibling();
                }
            }

            if (resultTutorialHomeHighlight != null)
            {
                resultTutorialHomeHighlight.gameObject.SetActive(visible);
                if (visible)
                {
                    resultTutorialHomeHighlight.transform.SetAsLastSibling();
                }
            }

            if (visible && resultHomeButton != null)
            {
                resultHomeButton.transform.SetAsLastSibling();
            }
        }

        private void AnimateResultTutorialGuide()
        {
            if (resultTutorialGuideRoot == null || !resultTutorialGuideRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            if (resultTutorialGuideCharacterImage != null)
            {
                float characterScale = Mathf.Lerp(0.985f, 1.035f, pulse);
                resultTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(characterScale, characterScale, 1f);
            }

            if (resultTutorialHomeHighlight != null && resultTutorialHomeHighlight.gameObject.activeSelf)
            {
                resultTutorialHomeHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, pulse));
                float frameScale = Mathf.Lerp(0.99f, 1.035f, pulse);
                resultTutorialHomeHighlight.rectTransform.localScale = new Vector3(frameScale, frameScale, 1f);
            }
        }

        private void PopulateResultStage(
            List<MonsterDataSO> results,
            MonsterDataSO featuredMonster,
            int requestedCount,
            int actualCount,
            ContractEffectTier tier)
        {
            if (resultTitleText != null)
            {
                resultTitleText.text = GetEffectChargingText(tier);
                resultTitleText.color = GoldTextColor;
            }

            if (resultSummaryText != null)
            {
                resultSummaryText.text = "契約因子を展開中";
                resultSummaryText.color = PaleTextColor;
            }

            Sprite monsterSprite = ResolveMonsterSprite(featuredMonster);
            if (resultMonsterImage != null)
            {
                resultMonsterImage.sprite = monsterSprite;
                resultMonsterImage.color = Color.clear;
            }

            if (resultMonsterShadowImage != null)
            {
                resultMonsterShadowImage.sprite = monsterSprite;
                resultMonsterShadowImage.color = Color.clear;
            }

            if (resultMonsterRect != null)
            {
                resultMonsterRect.localScale = Vector3.one * 0.70f;
                resultMonsterRect.localEulerAngles = Vector3.zero;
            }

            if (resultMonsterNameText != null)
            {
                resultMonsterNameText.text = GetMonsterDisplayName(featuredMonster);
                resultMonsterNameText.color = WithAlpha(Color.white, 0f);
            }

            if (resultClassText != null)
            {
                int classRank = Mathf.Max(1, featuredMonster != null ? featuredMonster.classRank : 1);
                resultClassText.text = BuildClassLabel(classRank);
                resultClassText.color = WithAlpha(ResolveClassColor(classRank), 0f);
            }

            HideResultSlots();
        }

        private void SetMonsterRevealAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (resultMonsterImage != null)
            {
                resultMonsterImage.color = resultMonsterImage.sprite != null
                    ? new Color(1f, 1f, 1f, alpha)
                    : new Color(1f, 1f, 1f, alpha * 0.12f);
            }

            if (resultMonsterShadowImage != null)
            {
                resultMonsterShadowImage.color = new Color(0f, 0f, 0f, alpha * 0.52f);
            }

            SetTextAlpha(resultMonsterNameText, alpha);
            SetTextAlpha(resultClassText, alpha);
        }

        private void HideResultSlots()
        {
            for (int i = 0; i < resultSlotViews.Count; i += 1)
            {
                ResultSlotView view = resultSlotViews[i];
                if (view != null && view.Root != null)
                {
                    view.Root.SetActive(false);
                }
            }
        }

        private void ShowResultSlots(List<MonsterDataSO> results)
        {
            HideResultSlots();
            if (results == null || results.Count <= 1)
            {
                return;
            }

            int count = Mathf.Min(results.Count, resultSlotViews.Count);
            for (int i = 0; i < count; i += 1)
            {
                ResultSlotView view = resultSlotViews[i];
                MonsterDataSO monsterData = results[i];
                if (view == null || view.Root == null)
                {
                    continue;
                }

                int classRank = Mathf.Max(1, monsterData != null ? monsterData.classRank : 1);
                Color classColor = ResolveClassColor(classRank);
                view.Root.SetActive(true);
                if (view.Frame != null)
                {
                    view.Frame.color = new Color(classColor.r * 0.28f, classColor.g * 0.28f, classColor.b * 0.32f, 0.94f);
                }

                if (view.Portrait != null)
                {
                    Sprite portrait = ResolveMonsterSprite(monsterData);
                    view.Portrait.sprite = portrait;
                    view.Portrait.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
                }

                if (view.ClassLabel != null)
                {
                    view.ClassLabel.text = "C" + classRank;
                    view.ClassLabel.color = classColor;
                }

                if (view.NameLabel != null)
                {
                    view.NameLabel.text = GetMonsterDisplayName(monsterData);
                    view.NameLabel.color = Color.white;
                }
            }
        }

        private static MonsterDataSO ResolveFeaturedResult(List<MonsterDataSO> results)
        {
            MonsterDataSO featured = null;
            if (results == null)
            {
                return null;
            }

            for (int i = 0; i < results.Count; i += 1)
            {
                MonsterDataSO candidate = results[i];
                if (candidate == null)
                {
                    continue;
                }

                if (featured == null || Mathf.Max(1, candidate.classRank) > Mathf.Max(1, featured.classRank))
                {
                    featured = candidate;
                }
            }

            return featured;
        }

        private static Sprite ResolveMonsterSprite(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (string.Equals(monsterData.monsterId, RockGolemMonsterId, StringComparison.Ordinal))
            {
                Sprite rockGolemHomeSprite = LoadSprite(RockGolemHomeHeroSpritePath);
                if (rockGolemHomeSprite != null)
                {
                    return rockGolemHomeSprite;
                }
            }

            if (monsterData.illustrationSprite != null)
            {
                return monsterData.illustrationSprite;
            }

            if (monsterData.portraitSprite != null)
            {
                return monsterData.portraitSprite;
            }

            string resourcePath = GetMonsterVisualResourcePath(monsterData);
            return string.IsNullOrEmpty(resourcePath) ? null : LoadSprite(resourcePath);
        }

        private static string GetMonsterVisualResourcePath(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(monsterData.illustrationResourcePath))
            {
                return monsterData.illustrationResourcePath;
            }

            return monsterData.portraitResourcePath ?? string.Empty;
        }

        private static string GetMonsterDisplayName(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return "契約不明";
            }

            return !string.IsNullOrEmpty(monsterData.monsterName) ? monsterData.monsterName : monsterData.monsterId;
        }

        private static string BuildResultSummaryText(List<MonsterDataSO> results, MonsterDataSO featuredMonster, int requestedCount, int actualCount)
        {
            if (results == null || results.Count == 0)
            {
                return "契約に失敗しました";
            }

            string countText = actualCount < requestedCount
                ? $"{actualCount}/{requestedCount}体が契約に応じた"
                : $"{actualCount}体が契約に応じた";
            int highestClassRank = Mathf.Max(1, featuredMonster != null ? featuredMonster.classRank : 1);
            return $"{countText} / 最高契約 C{highestClassRank}";
        }

        private static Color ResolveClassColor(int classRank)
        {
            switch (Mathf.Max(1, classRank))
            {
                case 4:
                    return new Color(1f, 0.78f, 0.30f, 1f);
                case 3:
                    return new Color(0.98f, 0.56f, 0.22f, 1f);
                case 2:
                    return new Color(0.40f, 0.86f, 0.94f, 1f);
                default:
                    return new Color(0.72f, 0.76f, 0.96f, 1f);
            }
        }

        private static string BuildClassLabel(int classRank)
        {
            switch (Mathf.Max(1, classRank))
            {
                case 4:
                    return "クラス4 / 深層契約";
                case 3:
                    return "クラス3 / 黄金契約";
                case 2:
                    return "クラス2 / 蒼晶契約";
                default:
                    return "クラス1 / 通常契約";
            }
        }

        private static void SetTextAlpha(Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void EnsureRuntimeState()
        {
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            MasterDataManager.Instance?.Initialize();

            if (GameManager.Instance != null &&
                GameManager.Instance.PlayerProfile == null &&
                SaveManager.Instance != null &&
                SaveManager.Instance.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        private static List<MonsterDataSO> CollectSummonPool(bool includePaidClass4)
        {
            var results = new List<MonsterDataSO>();
            MonsterDataSO[] monsterDataList = MasterDataManager.Instance?.GetAllMonsterData();
            if (monsterDataList == null || monsterDataList.Length == 0)
            {
                MasterDataRoot root = Resources.Load<MasterDataRoot>(MasterDataRootPath);
                monsterDataList = root != null ? root.monsterDataList : null;
            }

            if (monsterDataList == null)
            {
                return results;
            }

            for (int i = 0; i < monsterDataList.Length; i += 1)
            {
                MonsterDataSO monsterData = monsterDataList[i];
                bool canSummon = monsterData != null &&
                    (!monsterData.fusionExclusive || (includePaidClass4 && Mathf.Max(1, monsterData.classRank) == 4));
                if (canSummon && !string.IsNullOrEmpty(monsterData.monsterId))
                {
                    results.Add(monsterData);
                }
            }

            return results;
        }

        private static MonsterDataSO DrawMonster(List<MonsterDataSO> summonPool, bool usePaidStones)
        {
            if (summonPool == null || summonPool.Count == 0)
            {
                return null;
            }

            int targetClassRank = RollClassRank(usePaidStones);
            MonsterDataSO selected = DrawFromClassRank(summonPool, targetClassRank);
            if (selected != null)
            {
                return selected;
            }

            MonsterDataSO fallback = DrawFromClassRank(summonPool, 1);
            return fallback != null ? fallback : summonPool[UnityEngine.Random.Range(0, summonPool.Count)];
        }

        private static MonsterDataSO DrawInitialTutorialMonster(List<MonsterDataSO> summonPool, PlayerProfile profile)
        {
            int ownedCount = profile?.OwnedMonsters?.Count ?? 0;
            int index = Mathf.Clamp(ownedCount, 0, InitialTutorialMonsterIds.Length - 1);
            string monsterId = InitialTutorialMonsterIds[index];
            for (int i = 0; i < summonPool.Count; i += 1)
            {
                MonsterDataSO candidate = summonPool[i];
                if (candidate != null && string.Equals(candidate.monsterId, monsterId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return DrawMonster(summonPool, false);
        }

        private static MonsterDataSO DrawGuaranteedClass(List<MonsterDataSO> summonPool, int guaranteedClassRank)
        {
            if (summonPool == null || summonPool.Count == 0)
            {
                return null;
            }

            MonsterDataSO selected = DrawFromClassRank(summonPool, Mathf.Max(1, guaranteedClassRank));
            return selected != null ? selected : DrawMonster(summonPool, true);
        }

        private static int RollClassRank(bool usePaidStones)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (usePaidStones)
            {
                if (roll < PaidClass4SummonRatePercent)
                {
                    return 4;
                }

                if (roll < PaidClass4SummonRatePercent + PaidClass3SummonRatePercent)
                {
                    return 3;
                }

                if (roll < PaidClass4SummonRatePercent + PaidClass3SummonRatePercent + PaidClass2SummonRatePercent)
                {
                    return 2;
                }

                return 1;
            }

            if (roll < FreeClass3SummonRatePercent)
            {
                return 3;
            }

            if (roll < FreeClass3SummonRatePercent + FreeClass2SummonRatePercent)
            {
                return 2;
            }

            return 1;
        }

        private static MonsterDataSO DrawFromClassRank(List<MonsterDataSO> summonPool, int classRank)
        {
            var candidates = new List<MonsterDataSO>();
            for (int i = 0; i < summonPool.Count; i += 1)
            {
                MonsterDataSO monsterData = summonPool[i];
                if (monsterData != null && Mathf.Max(1, monsterData.classRank) == classRank)
                {
                    candidates.Add(monsterData);
                }
            }

            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        private static ContractEffectTier ResolveEffectTier(List<MonsterDataSO> results)
        {
            int highestClassRank = 1;
            if (results != null)
            {
                for (int i = 0; i < results.Count; i += 1)
                {
                    MonsterDataSO monsterData = results[i];
                    if (monsterData != null)
                    {
                        highestClassRank = Mathf.Max(highestClassRank, monsterData.classRank);
                    }
                }
            }

            if (highestClassRank >= 4)
            {
                return ContractEffectTier.Legendary;
            }

            return highestClassRank >= 3 ? ContractEffectTier.Rare : ContractEffectTier.Normal;
        }

        private static string GetEffectSpritePath(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return LegendaryEffectSpritePath;
                case ContractEffectTier.Rare:
                    return RareEffectSpritePath;
                default:
                    return NormalEffectSpritePath;
            }
        }

        private static AudioCue ResolveGachaRevealCue(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return AudioCue.GachaLegendaryReveal;
                case ContractEffectTier.Rare:
                    return AudioCue.GachaRareReveal;
                default:
                    return AudioCue.GachaReveal;
            }
        }

        private static string GetEffectChargingText(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return "深層ダンジョンの契約門が開く";
                case ContractEffectTier.Rare:
                    return "黄金の契約門が反応";
                default:
                    return "契約陣 起動";
            }
        }

        private static string GetEffectRevealTitle(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return "深層契約 成立";
                case ContractEffectTier.Rare:
                    return "黄金契約 成立";
                default:
                    return "契約成立";
            }
        }

        private static float GetEffectDuration(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return 1.52f;
                case ContractEffectTier.Rare:
                    return 1.24f;
                default:
                    return 0.94f;
            }
        }

        private static float GetEffectStartScale(ContractEffectTier tier)
        {
            return tier == ContractEffectTier.Normal ? 0.74f : 0.66f;
        }

        private static float GetEffectEndScale(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return 1.24f;
                case ContractEffectTier.Rare:
                    return 1.12f;
                default:
                    return 0.98f;
            }
        }

        private static float GetEffectSpin(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return -22f;
                case ContractEffectTier.Rare:
                    return 14f;
                default:
                    return 8f;
            }
        }

        private static float GetEffectPulseCount(ContractEffectTier tier)
        {
            return tier == ContractEffectTier.Legendary ? 5.5f : tier == ContractEffectTier.Rare ? 4f : 3f;
        }

        private static float GetEffectPulseScale(ContractEffectTier tier)
        {
            return tier == ContractEffectTier.Legendary ? 0.075f : tier == ContractEffectTier.Rare ? 0.055f : 0.035f;
        }

        private static float GetEffectFlashStrength(ContractEffectTier tier)
        {
            return tier == ContractEffectTier.Legendary ? 1f : tier == ContractEffectTier.Rare ? 0.72f : 0.40f;
        }

        private static float GetEffectHoldAlpha(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return 0.74f;
                case ContractEffectTier.Rare:
                    return 0.58f;
                default:
                    return 0.42f;
            }
        }

        private static Color GetEffectFlashColor(ContractEffectTier tier)
        {
            switch (tier)
            {
                case ContractEffectTier.Legendary:
                    return new Color(1f, 0.86f, 0.42f, 1f);
                case ContractEffectTier.Rare:
                    return new Color(1f, 0.66f, 0.28f, 1f);
                default:
                    return new Color(0.50f, 0.32f, 1f, 1f);
            }
        }

        private static string BuildResultText(List<MonsterDataSO> results, int requestedCount, int actualCount)
        {
            if (results == null || results.Count == 0)
            {
                return "契約に失敗しました";
            }

            var builder = new StringBuilder();
            builder.Append(actualCount < requestedCount ? $"{actualCount}回契約成功 / 所持枠不足" : $"{actualCount}回契約成功");
            int displayCount = Mathf.Min(results.Count, 5);
            for (int i = 0; i < displayCount; i += 1)
            {
                MonsterDataSO monsterData = results[i];
                string monsterName = !string.IsNullOrEmpty(monsterData.monsterName) ? monsterData.monsterName : monsterData.monsterId;
                builder.AppendLine();
                builder.Append($"{i + 1}. C{Mathf.Max(1, monsterData.classRank)} {monsterName}");
            }

            if (results.Count > displayCount)
            {
                builder.AppendLine();
                builder.Append($"ほか {results.Count - displayCount}体を所持に追加");
            }

            return builder.ToString();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
