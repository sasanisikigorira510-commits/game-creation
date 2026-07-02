using System;
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
    public sealed class MonsterDexPanelController : MonoBehaviour
    {
        private const int ColumnCount = 3;
        private const float CardWidth = 260f;
        private const float CardHeight = 244f;
        private const float CardGapX = 12f;
        private const float CardGapY = 12f;
        private const string BackgroundSpritePath = "UI/FusionPage/FusionBackground";
        private const string MainFrameSpritePath = "UI/FusionPage/FusionMainFrame";
        private const string RosterFrameSpritePath = "UI/FusionPage/FusionRosterFrame";
        private const string SmallButtonSpritePath = "UI/FusionPage/FusionSmallButton";
        private const string CardFrameBasePath = "MonsterCardFrames/monster_class_";
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const string TutorialHighlightFramePath = "UI/Tutorial/TutorialSummonHighlightFrameImage2";

        private readonly struct DexClassLevelGrowth
        {
            public DexClassLevelGrowth(float hp, float attack, float wisdom, float defense, float magicDefense)
            {
                Hp = hp;
                Attack = attack;
                Wisdom = wisdom;
                Defense = defense;
                MagicDefense = magicDefense;
            }

            public float Hp { get; }
            public float Attack { get; }
            public float Wisdom { get; }
            public float Defense { get; }
            public float MagicDefense { get; }
        }

        private static readonly Color PageTint = new Color(0.005f, 0.012f, 0.018f, 0.97f);
        private static readonly Color PanelColor = new Color(0.025f, 0.045f, 0.055f, 0.98f);
        private static readonly Color CardFallbackColor = new Color(0.032f, 0.052f, 0.064f, 0.98f);
        private static readonly Color CardSurfaceColor = new Color(0.018f, 0.030f, 0.036f, 0.98f);
        private static readonly Color CardSelectedSurfaceColor = new Color(0.055f, 0.050f, 0.032f, 0.98f);
        private static readonly Color CardImageWellColor = new Color(0.008f, 0.016f, 0.020f, 0.78f);
        private static readonly Color CardInfoPlateColor = new Color(0.010f, 0.020f, 0.025f, 0.86f);
        private static readonly Color DetailColor = new Color(0.02f, 0.04f, 0.046f, 0.98f);
        private static readonly Color AccentGold = new Color(1f, 0.76f, 0.31f, 1f);
        private static readonly Color AccentCyan = new Color(0.35f, 0.95f, 1f, 1f);
        private static readonly Color TextMain = new Color(0.96f, 0.985f, 1f, 1f);
        private static readonly Color TextSub = new Color(0.78f, 0.88f, 0.93f, 0.96f);

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly List<GameObject> cardObjects = new List<GameObject>();

        private Action onClosed;
        private Font runtimeFont;
        private RectTransform contentRoot;
        private Image selectedFrame;
        private Image selectedPortraitBackdrop;
        private Image selectedPortraitShadow;
        private Image selectedPortrait;
        private Text selectedNameLabel;
        private Text selectedInfoLabel;
        private Text selectedStatsLabel;
        private Text selectedDescriptionLabel;
        private Text counterLabel;
        private GameObject dexTutorialGuideRoot;
        private Image dexTutorialGuideCharacterImage;
        private Text dexTutorialGuideBodyText;
        private Text dexTutorialGuideFooterText;
        private Image dexTutorialCardHighlight;
        private Image dexTutorialHomeHighlight;
        private string selectedMonsterId;
        private bool isBuilt;
        private bool dexTutorialMonsterSelected;

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            dexTutorialMonsterSelected = false;
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            Refresh();
        }

        private void Hide()
        {
            if (ShouldShowDexTutorialGuide())
            {
                if (!dexTutorialMonsterSelected)
                {
                    RefreshDexTutorialGuide();
                    return;
                }

                CompleteDexTutorialGuide();
            }

            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        public void Refresh()
        {
            if (!isBuilt)
            {
                return;
            }

            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            MonsterDataSO[] allMonsterData = masterDataManager != null ? masterDataManager.GetAllMonsterData() : null;
            List<MonsterDataSO> monsters = SortMonsters(FilterUnlockedMonsters(allMonsterData));
            bool isWaitingForTutorialSelection = ShouldShowDexTutorialGuide() && !dexTutorialMonsterSelected;

            if (isWaitingForTutorialSelection)
            {
                selectedMonsterId = string.Empty;
            }
            else if (string.IsNullOrEmpty(selectedMonsterId) || monsters.All(monster => monster.monsterId != selectedMonsterId))
            {
                selectedMonsterId = monsters.Count > 0 ? monsters[0].monsterId : string.Empty;
            }

            RebuildCards(monsters);
            MonsterDataSO selectedMonster = monsters.FirstOrDefault(monster => monster.monsterId == selectedMonsterId);
            int selectedIndex = selectedMonster != null ? monsters.IndexOf(selectedMonster) + 1 : 0;
            BindDetail(selectedMonster, selectedIndex, allMonsterData, monsters.Count > 0);
            UpdateCounter(monsters);
            RefreshDexTutorialGuide();
        }

        private void Update()
        {
            AnimateDexTutorialGuide();
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

            CreateFullScreenImage("DexBackground", transform, BackgroundSpritePath, new Color(0.012f, 0.024f, 0.03f, 0.98f));

            GameObject panel = CreatePanel("DexMainPanel", transform, MainFrameSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 1710f), PanelColor);

            CreateText("Title", panel.transform, "モンスター図鑑", 48, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(520f, 62f), TextAnchor.MiddleCenter, AccentGold);

            CreateText("SortHint", panel.transform, "表示順: クラス昇順 / 種族順 / 図鑑番号", 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -96f), new Vector2(720f, 36f), TextAnchor.MiddleCenter, TextSub);

            HomeReturnButtonStyle.Create(transform, "CloseButton", Hide);

            BuildDetailPanel(panel.transform);
            BuildCardGrid(panel.transform);
            BuildDexTutorialGuide(panel.transform);
            isBuilt = true;
        }

        private void BuildDexTutorialGuide(Transform panelTransform)
        {
            if (panelTransform == null || dexTutorialGuideRoot != null)
            {
                return;
            }

            dexTutorialGuideRoot = CreatePanel("DexTutorialGuideRoot", panelTransform, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1350f), new Vector2(920f, 300f), new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Image guideBackground = dexTutorialGuideRoot.GetComponent<Image>();
            if (guideBackground != null)
            {
                guideBackground.raycastTarget = false;
            }

            Outline panelOutline = dexTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            dexTutorialGuideCharacterImage = CreateImage("DexTutorialGuideLuse", dexTutorialGuideRoot.transform, TutorialGuideSpritePath,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(28f, -4f), new Vector2(218f, 218f));

            Text badgeText = CreateText("DexTutorialGuideBadge", dexTutorialGuideRoot.transform, "TUTORIAL", 17, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(276f, -22f), new Vector2(136f, 28f), TextAnchor.MiddleCenter, AccentGold);
            AddTextContrast(badgeText);

            Text titleText = CreateText("DexTutorialGuideTitle", dexTutorialGuideRoot.transform, "ルシェの図鑑レッスン", 29, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(276f, -56f), new Vector2(560f, 36f), TextAnchor.MiddleLeft, new Color(1f, 0.96f, 0.78f, 1f));
            AddTextContrast(titleText);

            dexTutorialGuideBodyText = CreateText("DexTutorialGuideBody", dexTutorialGuideRoot.transform,
                "ここには仲間にしたモンスターの情報が記録されています。\n下のカードを選ぶと、能力・成長傾向を確認できます。\nまずは気になるモンスターを1体選んでみましょう。",
                19, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(276f, -102f), new Vector2(590f, 116f), TextAnchor.UpperLeft, new Color(0.96f, 0.95f, 0.88f, 1f));
            dexTutorialGuideBodyText.resizeTextForBestFit = true;
            dexTutorialGuideBodyText.resizeTextMinSize = 15;
            dexTutorialGuideBodyText.resizeTextMaxSize = 19;
            AddTextContrast(dexTutorialGuideBodyText);

            dexTutorialGuideFooterText = CreateText("DexTutorialGuideFooter", dexTutorialGuideRoot.transform,
                "次の操作: 金色の枠が付いたモンスターカードをタップ",
                18, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(276f, 24f), new Vector2(590f, 30f), TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 1f, 1f));
            AddTextContrast(dexTutorialGuideFooterText);

            Transform closeButton = transform.Find("CloseButton");
            if (closeButton != null)
            {
                dexTutorialHomeHighlight = CreateImage("DexTutorialHomeHighlight", closeButton, TutorialHighlightFramePath,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, HomeReturnButtonStyle.Size + new Vector2(34f, 30f));
                dexTutorialHomeHighlight.preserveAspect = false;
                dexTutorialHomeHighlight.raycastTarget = false;
                dexTutorialHomeHighlight.transform.SetAsLastSibling();
                dexTutorialHomeHighlight.gameObject.SetActive(false);
            }

            dexTutorialGuideRoot.SetActive(false);
        }

        private void RefreshDexTutorialGuide()
        {
            if (dexTutorialGuideRoot == null)
            {
                return;
            }

            bool shouldShow = ShouldShowDexTutorialGuide();
            dexTutorialGuideRoot.SetActive(shouldShow);
            if (shouldShow)
            {
                dexTutorialGuideRoot.transform.SetAsLastSibling();
            }

            if (dexTutorialGuideBodyText != null)
            {
                dexTutorialGuideBodyText.text = dexTutorialMonsterSelected
                    ? "モンスターの詳しい情報を確認できました。\n図鑑の見方はこれで大丈夫です。\n最後に左上の「ホームへ戻る」からホームへ戻りましょう。"
                    : "ここには仲間にしたモンスターの情報が記録されています。\n下のカードを選ぶと、能力・成長傾向を確認できます。\nまずは気になるモンスターを1体選んでみましょう。";
            }

            if (dexTutorialGuideFooterText != null)
            {
                dexTutorialGuideFooterText.text = dexTutorialMonsterSelected
                    ? "次の操作: 左上の「ホームへ戻る」をタップ"
                    : "次の操作: 金色の枠が付いたモンスターカードをタップ";
            }

            if (dexTutorialCardHighlight != null)
            {
                dexTutorialCardHighlight.gameObject.SetActive(shouldShow && !dexTutorialMonsterSelected);
            }

            if (dexTutorialHomeHighlight != null)
            {
                dexTutorialHomeHighlight.gameObject.SetActive(shouldShow && dexTutorialMonsterSelected);
                if (shouldShow && dexTutorialMonsterSelected)
                {
                    dexTutorialHomeHighlight.transform.SetAsLastSibling();
                }
            }
        }

        private static bool ShouldShowDexTutorialGuide()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            StoryTutorialEvent tutorialEvent = StoryTutorialService.GetNextEvent(profile, "HomeScene");
            return tutorialEvent != null &&
                tutorialEvent.EventId == StoryTutorialService.HintDex &&
                string.Equals(tutorialEvent.TargetKey, "home.dex", StringComparison.Ordinal);
        }

        private void CompleteDexTutorialGuide()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            bool changed = StoryTutorialService.MarkHintSeen(profile, StoryTutorialService.HintDex);
            if (changed && Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }
        }

        private void AnimateDexTutorialGuide()
        {
            if (dexTutorialGuideRoot == null || !dexTutorialGuideRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            if (dexTutorialGuideCharacterImage != null)
            {
                float characterScale = Mathf.Lerp(0.985f, 1.035f, pulse);
                dexTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(characterScale, characterScale, 1f);
            }

            if (dexTutorialCardHighlight != null)
            {
                dexTutorialCardHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.74f, 1f, pulse));
                float frameScale = Mathf.Lerp(0.99f, 1.035f, pulse);
                dexTutorialCardHighlight.rectTransform.localScale = new Vector3(frameScale, frameScale, 1f);
            }

            if (dexTutorialHomeHighlight != null && dexTutorialHomeHighlight.gameObject.activeSelf)
            {
                dexTutorialHomeHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.74f, 1f, pulse));
                float frameScale = Mathf.Lerp(0.99f, 1.035f, pulse);
                dexTutorialHomeHighlight.rectTransform.localScale = new Vector3(frameScale, frameScale, 1f);
            }
        }

        private void BuildDetailPanel(Transform parent)
        {
            GameObject detailPanel = CreatePanel("DexDetailPanel", parent, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -160f), new Vector2(920f, 340f), DetailColor);

            selectedFrame = CreatePanel("SelectedFrame", detailPanel.transform, null,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-145f, 0f), new Vector2(238f, 288f), CardFallbackColor).GetComponent<Image>();

            selectedPortraitBackdrop = CreatePanel("SelectedPortraitBackdrop", selectedFrame.transform, null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(196f, 238f), new Color(0.004f, 0.010f, 0.014f, 0.86f)).GetComponent<Image>();
            selectedPortraitBackdrop.raycastTarget = false;

            selectedPortraitShadow = CreateImage("SelectedPortraitShadow", selectedFrame.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(8f, 6f), new Vector2(210f, 210f));
            selectedPortraitShadow.color = new Color(0f, 0f, 0f, 0.58f);

            selectedPortrait = CreateImage("SelectedPortrait", selectedFrame.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 14f), new Vector2(210f, 210f));

            selectedNameLabel = CreateText("SelectedName", detailPanel.transform, "-", 30, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(46f, -38f), new Vector2(560f, 44f), TextAnchor.MiddleLeft, TextMain);

            selectedInfoLabel = CreateText("SelectedInfo", detailPanel.transform, "-", 21, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(46f, -86f), new Vector2(560f, 40f), TextAnchor.MiddleLeft, AccentCyan);

            selectedStatsLabel = CreateText("SelectedStats", detailPanel.transform, "-", 17, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(46f, -132f), new Vector2(560f, 116f), TextAnchor.UpperLeft, TextSub);

            selectedDescriptionLabel = CreateText("SelectedDescription", detailPanel.transform, "-", 18, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(46f, 34f), new Vector2(560f, 74f), TextAnchor.UpperLeft, TextMain);
        }

        private void BuildCardGrid(Transform parent)
        {
            GameObject gridPanel = CreatePanel("DexGridPanel", parent, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -508f), new Vector2(920f, 1192f), new Color(0.016f, 0.033f, 0.04f, 0.98f));

            counterLabel = CreateText("Counter", gridPanel.transform, "", 22, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(620f, 36f), TextAnchor.MiddleCenter, TextMain);

            GameObject viewport = CreatePanel("Viewport", gridPanel.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(860f, 1106f), new Color(0f, 0f, 0f, 0.18f));
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateUiObject("Content", viewport.transform);
            contentRoot = content.GetComponent<RectTransform>();
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = Vector2.zero;

            ScrollRect scrollRect = gridPanel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRoot;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 42f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void RebuildCards(List<MonsterDataSO> monsters)
        {
            foreach (GameObject cardObject in cardObjects)
            {
                if (cardObject != null)
                {
                    DestroyObject(cardObject);
                }
            }

            cardObjects.Clear();
            if (contentRoot == null)
            {
                return;
            }

            int rowCount = Mathf.CeilToInt(monsters.Count / (float)ColumnCount);
            float contentHeight = Mathf.Max(0f, rowCount * CardHeight + Mathf.Max(0, rowCount - 1) * CardGapY);
            contentRoot.sizeDelta = new Vector2(0f, contentHeight);

            float totalWidth = ColumnCount * CardWidth + (ColumnCount - 1) * CardGapX;
            float startX = -totalWidth * 0.5f + CardWidth * 0.5f;

            for (int i = 0; i < monsters.Count; i += 1)
            {
                MonsterDataSO monsterData = monsters[i];
                int row = i / ColumnCount;
                int column = i % ColumnCount;
                Vector2 position = new Vector2(startX + column * (CardWidth + CardGapX), -row * (CardHeight + CardGapY) - CardHeight * 0.5f);
                GameObject card = CreateMonsterCard(monsterData, i + 1, position);
                cardObjects.Add(card);
            }
        }

        private GameObject CreateMonsterCard(MonsterDataSO monsterData, int displayIndex, Vector2 anchoredPosition)
        {
            bool isSelected = monsterData != null && monsterData.monsterId == selectedMonsterId;
            string framePath = ResolveCardFramePath(monsterData);
            GameObject card = CreatePanel("DexCard_" + displayIndex, contentRoot, framePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                anchoredPosition, new Vector2(CardWidth, CardHeight), CardFallbackColor);

            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.color = Color.white;
            }

            Button button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.onClick.AddListener(() => SelectMonster(monsterData));

            Image surface = CreatePanel("CardSurface", card.transform, null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(CardWidth - 24f, CardHeight - 24f), isSelected ? CardSelectedSurfaceColor : CardSurfaceColor).GetComponent<Image>();
            surface.raycastTarget = false;

            Image imageWell = CreatePanel("PortraitWell", card.transform, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(136f, 108f), CardImageWellColor).GetComponent<Image>();
            imageWell.raycastTarget = false;
            imageWell.gameObject.AddComponent<RectMask2D>();

            Image infoPlate = CreatePanel("InfoPlate", card.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(CardWidth - 34f, 76f), CardInfoPlateColor).GetComponent<Image>();
            infoPlate.raycastTarget = false;

            CreateText("Number", card.transform, BuildNumberText(monsterData, displayIndex), 15, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(196f, 24f), TextAnchor.MiddleCenter, AccentGold);

            Image portrait = CreateImage("Portrait", imageWell.transform, GetPortraitResourcePath(monsterData),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -2f), new Vector2(88f, 88f));
            portrait.color = Color.white;

            Text nameText = CreateText("Name", card.transform, monsterData != null ? monsterData.monsterName : "不明", 17, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 54f), new Vector2(214f, 22f), TextAnchor.MiddleCenter, TextMain);
            EnableBestFit(nameText, 11, 17);

            Text classRaceText = CreateText("ClassRace", card.transform, monsterData != null ? $"{ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)} / {ResolveDamageTypeLabel(monsterData.damageType)}" : "-",
                13, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(214f, 20f), TextAnchor.MiddleCenter,
                monsterData != null ? ResolveDamageTypeColor(monsterData.damageType, 0.98f) : TextSub);
            EnableBestFit(classRaceText, 10, 13);

            Text ownedText = CreateText("OwnedState", card.transform, BuildOwnedText(monsterData), 14, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(214f, 20f), TextAnchor.MiddleCenter, isSelected ? AccentGold : AccentCyan);
            EnableBestFit(ownedText, 10, 14);

            if (isSelected)
            {
                CreateText("SelectedBadge", card.transform, "選択中", 15, FontStyle.Bold,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-26f, -22f), new Vector2(82f, 20f), TextAnchor.MiddleRight, AccentGold);
            }

            if (displayIndex == 1 && ShouldShowDexTutorialGuide() && !dexTutorialMonsterSelected)
            {
                dexTutorialCardHighlight = CreateImage("DexTutorialCardHighlight", card.transform, TutorialHighlightFramePath,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(CardWidth + 34f, CardHeight + 34f));
                dexTutorialCardHighlight.preserveAspect = false;
                dexTutorialCardHighlight.raycastTarget = false;
                dexTutorialCardHighlight.transform.SetAsLastSibling();
            }

            return card;
        }

        private void SelectMonster(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return;
            }

            if (ShouldShowDexTutorialGuide())
            {
                dexTutorialMonsterSelected = true;
            }

            selectedMonsterId = monsterData.monsterId;
            Refresh();
        }

        private void BindDetail(MonsterDataSO monsterData, int fallbackIndex, MonsterDataSO[] allMonsterData, bool hasSelectableMonsters)
        {
            if (monsterData == null)
            {
                SetPortrait(selectedPortrait, null);
                SetPortrait(selectedPortraitShadow, null);
                if (selectedPortraitShadow != null)
                {
                    selectedPortraitShadow.color = new Color(0f, 0f, 0f, 0f);
                }

                if (selectedNameLabel != null) selectedNameLabel.text = hasSelectableMonsters ? "モンスターを選択" : "モンスター未登録";
                if (selectedInfoLabel != null) selectedInfoLabel.text = "-";
                if (selectedStatsLabel != null)
                {
                    selectedStatsLabel.text = hasSelectableMonsters
                        ? "下のカードをタップすると、能力・成長傾向を確認できます。"
                        : "-";
                }

                if (selectedDescriptionLabel != null)
                {
                    selectedDescriptionLabel.text = hasSelectableMonsters
                        ? "気になるモンスターを1体選んでください。"
                        : "マスターデータを読み込めませんでした。";
                }
                return;
            }

            if (selectedFrame != null)
            {
                selectedFrame.sprite = LoadSprite(ResolveCardFramePath(monsterData));
                selectedFrame.color = selectedFrame.sprite != null ? Color.white : CardFallbackColor;
            }

            string detailPortraitPath = GetDetailPortraitResourcePath(monsterData);
            SetPortrait(selectedPortraitShadow, detailPortraitPath);
            if (selectedPortraitShadow != null)
            {
                selectedPortraitShadow.color = selectedPortraitShadow.sprite != null
                    ? new Color(0f, 0f, 0f, 0.62f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            SetPortrait(selectedPortrait, detailPortraitPath);
            if (selectedNameLabel != null)
            {
                selectedNameLabel.text = monsterData.monsterName;
            }

            if (selectedInfoLabel != null)
            {
                selectedInfoLabel.text = $"{BuildNumberText(monsterData, fallbackIndex)} / {ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)} / 最大Lv.{MonsterLevelService.GetMaxLevel(monsterData)} / {ResolveElementName(monsterData.element)} / {ResolveRangeName(monsterData.rangeType)} / {ResolveDamageTypeLabel(monsterData.damageType)}";
            }

            if (selectedStatsLabel != null)
            {
                MonsterBaseStats stats = monsterData.baseStats;
                selectedStatsLabel.text =
                    $"基礎傾向 HP {ResolveStatRank(monsterData, allMonsterData, x => x.baseStats.maxHp)}    攻撃 {ResolveStatRank(monsterData, allMonsterData, x => x.baseStats.attack)}    魔力 {ResolveStatRank(monsterData, allMonsterData, x => x.baseStats.magicAttack)}\n" +
                    $"防御 {ResolveStatRank(monsterData, allMonsterData, x => x.baseStats.defense)}    魔防 {ResolveStatRank(monsterData, allMonsterData, x => x.baseStats.magicDefense)}    攻速 {stats.attackSpeed:0.##}\n" +
                    $"攻撃範囲 {monsterData.attackRange:0.##}    対象数 {Mathf.Max(1, monsterData.normalAttackTargetCount)}    {ResolveDamageName(monsterData.damageType)}\n" +
                    $"成長しやすさ HP {ResolveGrowthRank(monsterData, allMonsterData, growth => growth.Hp, x => x.levelGrowth.maxHpCoefficient)}    攻 {ResolveGrowthRank(monsterData, allMonsterData, growth => growth.Attack, x => x.levelGrowth.attackCoefficient)}    魔 {ResolveGrowthRank(monsterData, allMonsterData, growth => growth.Wisdom, x => x.levelGrowth.magicAttackCoefficient)}    防 {ResolveGrowthRank(monsterData, allMonsterData, growth => growth.Defense, x => x.levelGrowth.defenseCoefficient)}    魔防 {ResolveGrowthRank(monsterData, allMonsterData, growth => growth.MagicDefense, x => x.levelGrowth.magicDefenseCoefficient)}";
            }

            if (selectedDescriptionLabel != null)
            {
                string description = string.IsNullOrWhiteSpace(monsterData.description)
                    ? $"{ResolveRaceName(monsterData.raceId)}系のクラス{Mathf.Max(1, monsterData.classRank)}モンスター。"
                    : monsterData.description;
                selectedDescriptionLabel.text = $"{BuildOwnedText(monsterData)}\n{description}";
            }
        }

        private void UpdateCounter(List<MonsterDataSO> monsters)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            int ownedKinds = 0;
            foreach (MonsterDataSO monsterData in monsters)
            {
                if (profile != null && monsterData != null && profile.GetOwnedMonsterCount(monsterData.monsterId) > 0)
                {
                    ownedKinds += 1;
                }
            }

            if (counterLabel != null)
            {
                counterLabel.text = $"図鑑登録 {monsters.Count}体 / 現在所持 {ownedKinds}種";
            }
        }

        private static IEnumerable<MonsterDataSO> FilterUnlockedMonsters(MonsterDataSO[] monsters)
        {
            if (monsters == null)
            {
                return Array.Empty<MonsterDataSO>();
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return Array.Empty<MonsterDataSO>();
            }

            HashSet<string> unlockedIds = new HashSet<string>(
                profile.MonsterDexEntries
                    .Where(entry => entry != null && entry.IsUnlocked && !string.IsNullOrEmpty(entry.MonsterId))
                    .Select(entry => entry.MonsterId));

            foreach (OwnedMonsterData ownedMonster in profile.OwnedMonsters)
            {
                if (ownedMonster != null && !string.IsNullOrEmpty(ownedMonster.MonsterId))
                {
                    unlockedIds.Add(ownedMonster.MonsterId);
                }
            }

            return monsters.Where(monster => monster != null && unlockedIds.Contains(monster.monsterId));
        }

        private static List<MonsterDataSO> SortMonsters(MonsterDataSO[] monsters)
        {
            return SortMonsters(monsters as IEnumerable<MonsterDataSO>);
        }

        private static List<MonsterDataSO> SortMonsters(IEnumerable<MonsterDataSO> monsters)
        {
            return (monsters ?? Array.Empty<MonsterDataSO>())
                    .Where(monster => monster != null && !string.IsNullOrEmpty(monster.monsterId))
                    .OrderBy(monster => Mathf.Max(1, monster.classRank))
                    .ThenBy(monster => ResolveRaceOrder(monster.raceId))
                    .ThenBy(monster => monster.encyclopediaNumber > 0 ? monster.encyclopediaNumber : int.MaxValue)
                    .ThenBy(monster => monster.monsterName)
                    .ToList();
        }

        private static int ResolveRaceOrder(string raceId)
        {
            return raceId switch
            {
                "dragon" => 10,
                "robot" => 20,
                "golem" => 30,
                "swordsman" => 40,
                "mage" => 50,
                "angel" => 60,
                "spirit" => 70,
                "special" => 80,
                _ => 999
            };
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

        private static string ResolveElementName(MonsterElement element)
        {
            return element switch
            {
                MonsterElement.Wood => "木",
                MonsterElement.Water => "水",
                MonsterElement.Fire => "火",
                MonsterElement.Light => "光",
                MonsterElement.Dark => "闇",
                _ => "無"
            };
        }

        private static string ResolveRangeName(MonsterRangeType rangeType)
        {
            return rangeType == MonsterRangeType.Ranged ? "遠距離" : "近距離";
        }

        private static string ResolveDamageName(MonsterDamageType damageType)
        {
            return ResolveDamageTypeLabel(damageType);
        }

        private static string ResolveDamageTypeLabel(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法型" : "物理型";
        }

        private static Color ResolveDamageTypeColor(MonsterDamageType damageType, float alpha)
        {
            return damageType == MonsterDamageType.Magic
                ? new Color(0.52f, 0.88f, 1f, alpha)
                : new Color(1f, 0.78f, 0.43f, alpha);
        }

        private static float ResolveGrowthCoefficient(float coefficient)
        {
            return coefficient > 0f ? coefficient : 1f;
        }

        private static string ResolveStatRank(MonsterDataSO monsterData, MonsterDataSO[] allMonsterData, Func<MonsterDataSO, float> selector)
        {
            return ResolveRelativeRank(monsterData, allMonsterData, selector, value => value);
        }

        private static string ResolveGrowthRank(
            MonsterDataSO monsterData,
            MonsterDataSO[] allMonsterData,
            Func<DexClassLevelGrowth, float> classGrowthSelector,
            Func<MonsterDataSO, float> coefficientSelector)
        {
            return ResolveRelativeRank(
                monsterData,
                allMonsterData,
                candidate =>
                {
                    DexClassLevelGrowth classGrowth = ResolveClassLevelGrowth(candidate.classRank);
                    return classGrowthSelector(classGrowth) * ResolveGrowthCoefficient(coefficientSelector(candidate));
                },
                value => value);
        }

        private static DexClassLevelGrowth ResolveClassLevelGrowth(int classRank)
        {
            return Mathf.Max(1, classRank) switch
            {
                1 => new DexClassLevelGrowth(5.0f, 1.10f, 1.10f, 0.70f, 0.70f),
                2 => new DexClassLevelGrowth(7.0f, 1.70f, 1.70f, 1.05f, 1.05f),
                3 => new DexClassLevelGrowth(10.0f, 2.35f, 2.35f, 1.45f, 1.45f),
                4 => new DexClassLevelGrowth(13.0f, 3.00f, 3.00f, 1.90f, 1.90f),
                _ => new DexClassLevelGrowth(15.0f, 3.45f, 3.45f, 2.20f, 2.20f)
            };
        }

        private static string ResolveRelativeRank(
            MonsterDataSO monsterData,
            MonsterDataSO[] allMonsterData,
            Func<MonsterDataSO, float> selector,
            Func<float, float> normalize)
        {
            if (monsterData == null || selector == null)
            {
                return "-";
            }

            float selectedValue = normalize != null ? normalize(selector(monsterData)) : selector(monsterData);
            List<float> values = (allMonsterData ?? Array.Empty<MonsterDataSO>())
                .Where(candidate => candidate != null && !string.IsNullOrEmpty(candidate.monsterId))
                .Select(candidate => normalize != null ? normalize(selector(candidate)) : selector(candidate))
                .Where(value => value >= 0f)
                .OrderBy(value => value)
                .ToList();

            if (values.Count == 0)
            {
                return "-";
            }

            int lowerCount = values.Count(value => value < selectedValue);
            int sameCount = values.Count(value => Mathf.Approximately(value, selectedValue));
            float percentile = (lowerCount + sameCount * 0.5f) / values.Count;
            if (percentile >= 0.90f) return "S";
            if (percentile >= 0.72f) return "A";
            if (percentile >= 0.55f) return "B";
            if (percentile >= 0.38f) return "C";
            if (percentile >= 0.20f) return "D";
            return "E";
        }

        private static string BuildNumberText(MonsterDataSO monsterData, int fallbackIndex)
        {
            int number = monsterData != null && monsterData.encyclopediaNumber > 0 ? monsterData.encyclopediaNumber : fallbackIndex;
            return number > 0 ? $"No.{number:000}" : "No.---";
        }

        private static string BuildOwnedText(MonsterDataSO monsterData)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            int ownedCount = profile != null && monsterData != null ? profile.GetOwnedMonsterCount(monsterData.monsterId) : 0;
            return ownedCount > 0 ? $"所持 {ownedCount}体" : "登録済み / 現在未所持";
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

        private static string GetDetailPortraitResourcePath(MonsterDataSO monsterData)
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

        private static string ResolveCardFramePath(MonsterDataSO monsterData)
        {
            int classRank = Mathf.Clamp(monsterData != null ? monsterData.classRank : 1, 1, 6);
            return CardFrameBasePath + classRank + "_card_frame";
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
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);

            CreateText("Label", buttonObject.transform, text, 20, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 14f, size.y - 14f), TextAnchor.MiddleCenter, Color.white);

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
            return panel;
        }

        private Image CreateImage(
            string objectName,
            Transform parent,
            string spritePath,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = imageObject.AddComponent<Image>();
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
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    sprite.name = texture.name;
                }
            }

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

        private static void EnableBestFit(Text text, int minSize, int maxSize)
        {
            if (text == null)
            {
                return;
            }

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, minSize);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, maxSize);
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void AddTextContrast(Text text)
        {
            if (text == null)
            {
                return;
            }

            Outline outline = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
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
