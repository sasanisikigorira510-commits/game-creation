using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class DungeonSelectionPanelController : MonoBehaviour
    {
        private const string BackgroundPath = "UI/DungeonSelect/DungeonSelectBackground";
        private const string DungeonWorldMapPath = "UI/DungeonSelect/WorldMap/DungeonWorldMapCurrentBase";
        private const string FloorNodeLockedPath = "UI/DungeonSelect/FloorNodeLocked";
        private const string FloorNodeUnlockedPath = "UI/DungeonSelect/FloorNodeUnlocked";
        private const string FloorNodeSelectedPath = "UI/DungeonSelect/FloorNodeSelected";
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const string TutorialHighlightFramePath = "UI/Tutorial/TutorialSummonHighlightFrameImage2";
        private const string DungeonTutorialTitle = "ルシェの探索案内";
        private const string DungeonTutorialBody = "探索先ごとに出現する眷属と報酬の傾向が変わります。最初は見習いの五門洞 第1階層から契約片を回収しましょう。";
        private const string DungeonTutorialFooter = "次の操作: 枠で囲まれた「この階層へ挑む」をタップ";
        private const float DungeonMapWidth = 840f;
        private const float DungeonMapHeight = 560f;

        private readonly List<Image> dungeonCardFrames = new List<Image>();
        private readonly List<string> dungeonCardIds = new List<string>();
        private readonly List<GameObject> dungeonMapLabelRoots = new List<GameObject>();
        private readonly List<Text> dungeonMapLabels = new List<Text>();
        private readonly List<Image> floorNodeImages = new List<Image>();
        private readonly List<Text> floorNodeLabels = new List<Text>();
        private readonly List<int> floorNodeLocalFloors = new List<int>();

        private GameObject panelRoot;
        private RectTransform dungeonListRoot;
        private RectTransform floorListRoot;
        private Text titleText;
        private Image selectedDungeonPreviewImage;
        private Text selectedDungeonNameText;
        private Text dungeonDescriptionText;
        private Text floorDescriptionText;
        private Text enemyPreviewText;
        private Button startBattleButton;
        private GameObject dungeonTutorialGuideRoot;
        private Text dungeonTutorialGuideTitleText;
        private Text dungeonTutorialGuideBodyText;
        private Text dungeonTutorialGuideFooterText;
        private Image dungeonTutorialGuideCharacterImage;
        private Image dungeonTutorialStartHighlight;
        private Action closeCallback;
        private string battleSceneName = "BattleScene";
        private string selectedDungeonId;
        private int selectedLocalFloor = 1;
        private bool hasClickedDungeonMapNode;

        private struct DungeonMapAnchor
        {
            public DungeonMapAnchor(string dungeonId, Vector2 nodeNormalized, Vector2 labelPosition, Vector2 labelSize)
            {
                DungeonId = dungeonId;
                NodeNormalized = nodeNormalized;
                LabelPosition = labelPosition;
                LabelSize = labelSize;
            }

            public string DungeonId { get; }
            public Vector2 NodeNormalized { get; }
            public Vector2 LabelPosition { get; }
            public Vector2 LabelSize { get; }
        }

        private static readonly DungeonMapAnchor[] DungeonMapAnchors =
        {
            new DungeonMapAnchor("blight_cavern", new Vector2(0.145833f, 0.208984f), new Vector2(-300f, 246f), new Vector2(212f, 56f)),
            new DungeonMapAnchor("gear_crypt", new Vector2(0.408854f, 0.212891f), new Vector2(-60f, 246f), new Vector2(212f, 56f)),
            new DungeonMapAnchor("curse_library", new Vector2(0.429688f, 0.582031f), new Vector2(-255f, -45f), new Vector2(236f, 62f)),
            new DungeonMapAnchor("ember_drake_pass", new Vector2(0.832031f, 0.152344f), new Vector2(286f, 96f), new Vector2(190f, 56f)),
            new DungeonMapAnchor("star_ore_citadel", new Vector2(0.725260f, 0.632813f), new Vector2(112f, -141f), new Vector2(190f, 56f)),
            new DungeonMapAnchor("abyssal_grimoire_spire", new Vector2(0.901042f, 0.738281f), new Vector2(304f, -216f), new Vector2(218f, 56f))
        };

        private void Update()
        {
            AnimateDungeonTutorialGuide();
            if (!Application.isPlaying || panelRoot == null || !panelRoot.activeInHierarchy || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            InvokeButtonUnderPointer(panelRoot.transform, Input.mousePosition);
        }

        public void Show(string targetBattleSceneName, Action onClose)
        {
            battleSceneName = string.IsNullOrEmpty(targetBattleSceneName) ? "BattleScene" : targetBattleSceneName;
            closeCallback = onClose;
            ManagerFactory.EnsureMasterDataManager();
            MasterDataManager.Instance?.Initialize();
            EnsurePanel();
            if (panelRoot == null)
            {
                return;
            }

            hasClickedDungeonMapNode = false;
            BuildDungeonWorldMap();
            SelectDungeon(GameManager.Instance != null ? GameManager.Instance.CurrentDungeonId : BattleDungeonCatalog.Dungeons[0].DungeonId);
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            RefreshDungeonTutorialGuide(GetDungeonTutorialEvent());
        }

        private void EnsurePanel()
        {
            if (panelRoot != null)
            {
                RefreshSelectionMetricLayout();
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return;
            }

            panelRoot = gameObject;
            if (panelRoot.transform.parent != canvas.transform)
            {
                panelRoot.transform.SetParent(canvas.transform, false);
            }

            RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = panelRoot.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image blocker = panelRoot.GetComponent<Image>();
            if (blocker == null)
            {
                blocker = panelRoot.AddComponent<Image>();
            }

            blocker.color = new Color(0.01f, 0.015f, 0.025f, 0.98f);

            Image background = CreateImage("DungeonSelectionBackground", panelRoot.transform, LoadSprite(BackgroundPath),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
            background.raycastTarget = false;

            GameObject panel = CreateUiObject("DungeonSelectionFrame", panelRoot.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(960f, 1660f);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.035f, 0.055f, 0.88f);

            titleText = CreateText("Title", panel.transform, "ダンジョン選択", 46, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.78f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(520f, 58f));

            CreateText("SubTitle", panel.transform, "挑む場所と階層を選んで戦闘を開始します", 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 0.96f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(760f, 34f));

            HomeReturnButtonStyle.Create(panelRoot.transform, "CloseButton", Close);

            dungeonListRoot = CreateUiObject("DungeonList", panel.transform).GetComponent<RectTransform>();
            dungeonListRoot.anchorMin = new Vector2(0.5f, 1f);
            dungeonListRoot.anchorMax = new Vector2(0.5f, 1f);
            dungeonListRoot.pivot = new Vector2(0.5f, 1f);
            dungeonListRoot.anchoredPosition = new Vector2(0f, -136f);
            dungeonListRoot.sizeDelta = new Vector2(DungeonMapWidth, DungeonMapHeight);

            GameObject selectedPanel = CreatePanel("SelectedDungeonSpotlight", panel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -714f), new Vector2(840f, 328f), new Color(0.018f, 0.028f, 0.046f, 0.94f));
            Outline selectedPanelOutline = selectedPanel.AddComponent<Outline>();
            selectedPanelOutline.effectColor = new Color(0.78f, 0.62f, 0.30f, 0.64f);
            selectedPanelOutline.effectDistance = new Vector2(3f, -3f);
            selectedPanelOutline.useGraphicAlpha = false;

            selectedDungeonPreviewImage = CreateImage("SelectedDungeonPreviewArt", selectedPanel.transform, null,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -24f), new Vector2(282f, 156f), false);
            selectedDungeonPreviewImage.raycastTarget = false;

            Image selectedPreviewShade = CreateImage("SelectedDungeonPreviewShade", selectedPanel.transform, null,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -24f), new Vector2(282f, 156f), false);
            selectedPreviewShade.color = new Color(0f, 0f, 0f, 0.24f);
            selectedPreviewShade.raycastTarget = false;

            selectedDungeonNameText = CreateText("SelectedDungeonName", selectedPanel.transform, string.Empty, 30, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(1f, 0.94f, 0.72f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(326f, -24f), new Vector2(484f, 42f));
            ConfigureSelectionInfoText(selectedDungeonNameText, 30, 19);

            dungeonDescriptionText = CreateText("DungeonDescription", selectedPanel.transform, string.Empty, 24, FontStyle.Bold,
                TextAnchor.UpperLeft, new Color(0.92f, 0.87f, 0.72f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(326f, -74f), new Vector2(-356f, 190f));

            floorListRoot = CreateUiObject("FloorList", panel.transform).GetComponent<RectTransform>();
            floorListRoot.anchorMin = new Vector2(0.5f, 1f);
            floorListRoot.anchorMax = new Vector2(0.5f, 1f);
            floorListRoot.pivot = new Vector2(0.5f, 1f);
            floorListRoot.anchoredPosition = new Vector2(0f, -1060f);
            floorListRoot.sizeDelta = new Vector2(820f, 124f);

            floorDescriptionText = CreateText("FloorDescription", panel.transform, string.Empty, 20, FontStyle.Bold,
                TextAnchor.UpperCenter, new Color(0.78f, 0.92f, 0.98f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -1196f), new Vector2(820f, 44f));

            enemyPreviewText = CreateText("EnemyPreview", panel.transform, string.Empty, 22, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -1248f), new Vector2(820f, 76f));
            ConfigureSelectionInfoText(dungeonDescriptionText, 24, 16);
            ConfigureSelectionInfoText(floorDescriptionText, 20, 13);
            ConfigureSelectionInfoText(enemyPreviewText, 22, 13);

            startBattleButton = CreateTextButton("StartBattleButton", panel.transform, "この階層へ挑む",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 66f), new Vector2(560f, 92f), new Color(0.48f, 0.18f, 0.08f, 0.98f), StartSelectedBattle, 27);
            BuildDungeonTutorialGuide(panel.transform);
            RefreshSelectionMetricLayout();

            panelRoot.SetActive(false);
        }

        private void RefreshSelectionMetricLayout()
        {
            if (panelRoot == null)
            {
                return;
            }

            Transform selectedPanel = panelRoot.transform.Find("DungeonSelectionFrame/SelectedDungeonSpotlight");
            if (selectedPanel == null)
            {
                return;
            }

            Transform metaFrame = selectedPanel.Find("SelectedDungeonMetaFrame");
            if (metaFrame != null)
            {
                metaFrame.gameObject.SetActive(false);
            }

            Text descriptionText = selectedPanel.Find("DungeonDescription")?.GetComponent<Text>();
            if (descriptionText != null)
            {
                RectTransform descriptionRect = descriptionText.rectTransform;
                descriptionRect.anchoredPosition = new Vector2(326f, -74f);
                descriptionRect.sizeDelta = new Vector2(-356f, 190f);
                descriptionText.fontSize = 24;
                ConfigureSelectionInfoText(descriptionText, 24, 16);
            }

            HideSelectionMetric(selectedPanel, "SelectedDungeonRewardFrame");
            HideSelectionMetric(selectedPanel, "SelectedDungeonEnemyHintFrame");
        }

        private static void HideSelectionMetric(Transform selectedPanel, string objectName)
        {
            Transform metric = selectedPanel != null ? selectedPanel.Find(objectName) : null;
            if (metric != null)
            {
                metric.gameObject.SetActive(false);
            }
        }

        private void BuildDungeonTutorialGuide(Transform panelTransform)
        {
            if (panelTransform == null || startBattleButton == null || dungeonTutorialGuideRoot != null)
            {
                return;
            }

            dungeonTutorialGuideRoot = CreatePanel(
                "DungeonTutorialGuideRoot",
                panelTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 236f),
                new Vector2(900f, 222f),
                new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Outline panelOutline = dungeonTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            dungeonTutorialGuideCharacterImage = CreateImage(
                "DungeonTutorialGuideLuse",
                dungeonTutorialGuideRoot.transform,
                LoadSprite(TutorialGuideSpritePath),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(94f, -4f),
                new Vector2(170f, 170f),
                true);
            dungeonTutorialGuideCharacterImage.raycastTarget = false;

            Text badgeText = CreateText(
                "DungeonTutorialGuideBadge",
                dungeonTutorialGuideRoot.transform,
                "TUTORIAL",
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.78f, 0.38f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(290f, -22f),
                new Vector2(140f, 28f));

            dungeonTutorialGuideTitleText = CreateText(
                "DungeonTutorialGuideTitle",
                dungeonTutorialGuideRoot.transform,
                "ルシェの探索案内",
                28,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.96f, 0.78f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(290f, -58f),
                new Vector2(570f, 42f));

            dungeonTutorialGuideBodyText = CreateText(
                "DungeonTutorialGuideBody",
                dungeonTutorialGuideRoot.transform,
                string.Empty,
                19,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Color(0.94f, 0.89f, 0.80f, 0.96f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(290f, -108f),
                new Vector2(570f, 72f));
            dungeonTutorialGuideBodyText.resizeTextForBestFit = true;
            dungeonTutorialGuideBodyText.resizeTextMinSize = 14;
            dungeonTutorialGuideBodyText.resizeTextMaxSize = 19;

            dungeonTutorialGuideFooterText = CreateText(
                "DungeonTutorialGuideFooter",
                dungeonTutorialGuideRoot.transform,
                "次の操作: 枠で囲まれた「この階層へ挑む」をタップ",
                17,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.78f, 0.92f, 1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(290f, 24f),
                new Vector2(570f, 32f));

            dungeonTutorialStartHighlight = CreateImage(
                "DungeonTutorialStartHighlight",
                startBattleButton.transform,
                LoadSprite(TutorialHighlightFramePath),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(622f, 134f),
                false);
            dungeonTutorialStartHighlight.raycastTarget = false;
            dungeonTutorialStartHighlight.transform.SetAsLastSibling();

            dungeonTutorialGuideRoot.SetActive(false);
            dungeonTutorialStartHighlight.gameObject.SetActive(false);
        }

        private void BuildDungeonWorldMap()
        {
            ClearChildren(dungeonListRoot);
            dungeonCardFrames.Clear();
            dungeonCardIds.Clear();
            dungeonMapLabelRoots.Clear();
            dungeonMapLabels.Clear();
            IReadOnlyList<BattleDungeonDefinition> dungeons = BattleDungeonCatalog.Dungeons;

            Image mapBase = CreateImage("DungeonWorldMapBase", dungeonListRoot.transform, LoadSprite(DungeonWorldMapPath),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
            mapBase.raycastTarget = false;

            Outline mapOutline = mapBase.gameObject.AddComponent<Outline>();
            mapOutline.effectColor = new Color(0.78f, 0.62f, 0.30f, 0.42f);
            mapOutline.effectDistance = new Vector2(2f, -2f);
            mapOutline.useGraphicAlpha = false;

            Image mapShade = CreateImage("DungeonWorldMapShade", dungeonListRoot.transform, null,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
            mapShade.color = new Color(0f, 0f, 0f, 0.06f);
            mapShade.raycastTarget = false;

            BuildDungeonMapRoute(dungeonListRoot, dungeons);

            for (int i = 0; i < dungeons.Count; i += 1)
            {
                BattleDungeonDefinition dungeon = dungeons[i];
                if (!IsDungeonVisibleOnMap(dungeon))
                {
                    continue;
                }

                DungeonMapAnchor anchor = GetDungeonMapAnchor(dungeon.DungeonId);
                Vector2 nodePosition = ResolveDungeonMapPosition(anchor.NodeNormalized);

                Text mapLabel = CreateDungeonMapLabel(dungeonListRoot, dungeon, anchor, out GameObject labelRoot);
                labelRoot.SetActive(false);
                dungeonMapLabelRoots.Add(labelRoot);
                dungeonMapLabels.Add(mapLabel);

                GameObject node = CreateUiObject("DungeonMapNode_" + dungeon.DungeonId, dungeonListRoot);
                RectTransform nodeRect = node.GetComponent<RectTransform>();
                nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.anchoredPosition = nodePosition;
                nodeRect.sizeDelta = new Vector2(72f, 72f);

                Image hitArea = node.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                Button button = node.AddComponent<Button>();
                button.targetGraphic = hitArea;
                button.interactable = true;
                string capturedDungeonId = dungeon.DungeonId;
                button.onClick.AddListener(() =>
                {
                    hasClickedDungeonMapNode = true;
                    SelectDungeon(capturedDungeonId);
                });

                Image frame = CreateImage("Visual", node.transform, LoadSprite(FloorNodeUnlockedPath),
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
                frame.color = Color.white;
                frame.raycastTarget = false;
                dungeonCardFrames.Add(frame);
                dungeonCardIds.Add(dungeon.DungeonId);
            }
        }

        private static void BuildDungeonMapRoute(Transform mapParent, IReadOnlyList<BattleDungeonDefinition> dungeons)
        {
            if (mapParent == null || dungeons == null)
            {
                return;
            }

            BattleDungeonDefinition previousVisibleDungeon = null;
            for (int i = 0; i < dungeons.Count; i += 1)
            {
                BattleDungeonDefinition currentDungeon = dungeons[i];
                if (!IsDungeonVisibleOnMap(currentDungeon))
                {
                    continue;
                }

                if (previousVisibleDungeon == null)
                {
                    previousVisibleDungeon = currentDungeon;
                    continue;
                }

                DungeonMapAnchor fromAnchor = GetDungeonMapAnchor(previousVisibleDungeon.DungeonId);
                DungeonMapAnchor toAnchor = GetDungeonMapAnchor(currentDungeon.DungeonId);
                Vector2 from = ResolveDungeonMapPosition(fromAnchor.NodeNormalized);
                Vector2 to = ResolveDungeonMapPosition(toAnchor.NodeNormalized);
                CreateDungeonMapRouteSegment(mapParent, from, to, 10f, new Color(0.10f, 0.08f, 0.04f, 0.24f), "RouteShadow_" + i);
                CreateDungeonMapRouteSegment(mapParent, from, to, 4f, new Color(1f, 0.82f, 0.34f, 0.36f), "RouteGlow_" + i);
                previousVisibleDungeon = currentDungeon;
            }
        }

        private static void CreateDungeonMapRouteSegment(Transform parent, Vector2 from, Vector2 to, float thickness, Color color, string objectName)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.1f)
            {
                return;
            }

            Image segment = CreateImage(objectName, parent, null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                from + delta * 0.5f, new Vector2(length, thickness), false);
            segment.color = color;
            segment.raycastTarget = false;
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static Text CreateDungeonMapLabel(Transform parent, BattleDungeonDefinition dungeon, DungeonMapAnchor anchor, out GameObject labelFrame)
        {
            labelFrame = CreatePanel(
                "DungeonMapLabel_" + dungeon.DungeonId,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchor.LabelPosition,
                anchor.LabelSize,
                new Color(0.015f, 0.022f, 0.035f, 0.78f));

            Outline labelOutline = labelFrame.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0.60f, 0.72f, 0.86f, 0.38f);
            labelOutline.effectDistance = new Vector2(1.5f, -1.5f);
            labelOutline.useGraphicAlpha = false;

            Text label = CreateText(
                "Label",
                labelFrame.transform,
                BuildDungeonMapLabelText(dungeon),
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Color.white,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-18f, -8f));
            ConfigureSelectionInfoText(label, 15, 10);
            return label;
        }

        private static DungeonMapAnchor GetDungeonMapAnchor(string dungeonId)
        {
            for (int i = 0; i < DungeonMapAnchors.Length; i += 1)
            {
                if (string.Equals(DungeonMapAnchors[i].DungeonId, dungeonId, StringComparison.Ordinal))
                {
                    return DungeonMapAnchors[i];
                }
            }

            return new DungeonMapAnchor(dungeonId, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 58f));
        }

        private static Vector2 ResolveDungeonMapPosition(Vector2 normalized)
        {
            return new Vector2(
                (normalized.x - 0.5f) * DungeonMapWidth,
                (0.5f - normalized.y) * DungeonMapHeight);
        }

        private static string BuildDungeonMapLabelText(BattleDungeonDefinition dungeon)
        {
            if (dungeon == null)
            {
                return string.Empty;
            }

            return dungeon.DungeonName;
        }

        private void BuildFloorNodes(BattleDungeonDefinition dungeon)
        {
            ClearChildren(floorListRoot);
            floorNodeImages.Clear();
            floorNodeLabels.Clear();
            floorNodeLocalFloors.Clear();
            if (dungeon == null)
            {
                return;
            }

            int visibleIndex = 0;
            int layoutFloorCount = Mathf.Max(1, dungeon.Floors.Count);
            float listWidth = floorListRoot != null ? floorListRoot.sizeDelta.x : 820f;
            float horizontalPadding = layoutFloorCount > 5 ? 48f : 102f;
            float availableWidth = Mathf.Max(1f, listWidth - horizontalPadding * 2f);
            float nodeSpacing = layoutFloorCount > 1 ? availableWidth / (layoutFloorCount - 1) : 0f;
            float nodeSize = layoutFloorCount > 5 ? Mathf.Clamp(nodeSpacing * 0.72f, 54f, 108f) : 108f;
            int labelFontSize = layoutFloorCount > 5 ? 21 : 27;
            float startX = layoutFloorCount > 1 ? horizontalPadding : listWidth * 0.5f;
            for (int i = 0; i < dungeon.Floors.Count; i += 1)
            {
                int localFloor = dungeon.Floors[i].LocalFloor;
                if (!IsFloorUnlocked(dungeon.DungeonId, localFloor))
                {
                    continue;
                }

                GameObject node = CreateUiObject("FloorNode_" + localFloor, floorListRoot);
                RectTransform rect = node.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + visibleIndex * nodeSpacing, 0f);
                rect.sizeDelta = new Vector2(nodeSize, nodeSize);

                Image hitArea = node.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                Button button = node.AddComponent<Button>();
                button.targetGraphic = hitArea;
                int capturedLocalFloor = localFloor;
                button.onClick.AddListener(() => SelectFloor(capturedLocalFloor));

                Image visual = CreateImage("Visual", node.transform, LoadSprite(FloorNodeUnlockedPath),
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
                visual.raycastTarget = false;
                floorNodeImages.Add(visual);

                Text label = CreateText("Label", node.transform, localFloor.ToString(), labelFontSize, FontStyle.Bold,
                    TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                floorNodeLabels.Add(label);
                floorNodeLocalFloors.Add(localFloor);
                visibleIndex += 1;
            }
        }

        private void SelectDungeon(string dungeonId)
        {
            BattleDungeonDefinition dungeon = BattleDungeonCatalog.GetDungeon(dungeonId);
            if (!IsDungeonUnlocked(dungeon))
            {
                dungeon = GetLatestUnlockedDungeon();
            }

            selectedDungeonId = dungeon.DungeonId;
            selectedLocalFloor = Mathf.Clamp(
                GameManager.Instance != null && GameManager.Instance.CurrentDungeonId == selectedDungeonId
                    ? GameManager.Instance.CurrentDungeonFloor
                    : 1,
                1,
                ResolveMaxUnlockedLocalFloor(dungeon));
            BuildFloorNodes(dungeon);
            RefreshSelection();
        }

        private void SelectFloor(int localFloor)
        {
            if (!IsFloorUnlocked(selectedDungeonId, localFloor))
            {
                return;
            }

            selectedLocalFloor = Mathf.Max(1, localFloor);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            BattleDungeonDefinition dungeon = BattleDungeonCatalog.GetDungeon(selectedDungeonId);
            if (dungeon == null)
            {
                return;
            }

            for (int i = 0; i < dungeonCardFrames.Count; i += 1)
            {
                bool selected = i < dungeonCardIds.Count && dungeonCardIds[i] == dungeon.DungeonId;
                BattleDungeonDefinition mapDungeon = i < dungeonCardIds.Count
                    ? BattleDungeonCatalog.GetDungeon(dungeonCardIds[i])
                    : null;
                bool unlocked = IsDungeonUnlocked(mapDungeon);
                dungeonCardFrames[i].sprite = LoadSprite(selected
                    ? FloorNodeSelectedPath
                    : unlocked ? FloorNodeUnlockedPath : FloorNodeLockedPath);
                dungeonCardFrames[i].color = selected
                    ? Color.white
                    : unlocked ? new Color(0.92f, 1f, 1f, 0.96f) : new Color(0.76f, 0.80f, 0.86f, 0.78f);

                if (i < dungeonMapLabels.Count)
                {
                    dungeonMapLabels[i].text = BuildDungeonMapLabelText(mapDungeon);
                    dungeonMapLabels[i].color = selected
                        ? new Color(1f, 0.94f, 0.70f, 1f)
                        : unlocked ? Color.white : new Color(0.82f, 0.86f, 0.92f, 0.86f);
                }

                if (i < dungeonMapLabelRoots.Count)
                {
                    bool showLabel = hasClickedDungeonMapNode && selected;
                    dungeonMapLabelRoots[i].SetActive(showLabel);
                    if (showLabel)
                    {
                        dungeonMapLabelRoots[i].transform.SetAsLastSibling();
                    }
                }
            }

            BattleDungeonFloorDefinition selectedFloor = GetSelectedFloorDefinition(dungeon);
            if (selectedDungeonPreviewImage != null)
            {
                Sprite previewSprite = LoadSprite(dungeon.CardResourcePath);
                selectedDungeonPreviewImage.sprite = previewSprite;
                selectedDungeonPreviewImage.color = previewSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (selectedDungeonNameText != null)
            {
                selectedDungeonNameText.text = $"{dungeon.DungeonName}  第{selectedLocalFloor}階層";
            }

            if (dungeonDescriptionText != null)
            {
                StoryTutorialEvent tutorialEvent = GetDungeonTutorialEvent();
                bool showTutorialGuide = ShouldShowDungeonTutorialGuide(tutorialEvent);
                dungeonDescriptionText.text = showTutorialGuide ? string.Empty : dungeon.Description;
                RefreshDungeonTutorialGuide(tutorialEvent);
            }

            for (int i = 0; i < floorNodeImages.Count; i += 1)
            {
                bool selected = i < floorNodeLocalFloors.Count && floorNodeLocalFloors[i] == selectedLocalFloor;
                floorNodeImages[i].sprite = LoadSprite(selected ? FloorNodeSelectedPath : FloorNodeUnlockedPath);
                floorNodeLabels[i].color = selected ? new Color(0.08f, 0.12f, 0.09f) : Color.white;
            }

            if (floorDescriptionText != null)
            {
                floorDescriptionText.text = selectedFloor != null ? selectedFloor.FloorName : string.Empty;
            }

            if (enemyPreviewText != null)
            {
                enemyPreviewText.text = BuildEnemyPreviewText(dungeon, selectedFloor);
            }
        }

        private void RefreshDungeonTutorialGuide(StoryTutorialEvent tutorialEvent)
        {
            bool shouldShow = ShouldShowDungeonTutorialGuide(tutorialEvent);

            if (dungeonTutorialGuideRoot != null)
            {
                dungeonTutorialGuideRoot.SetActive(shouldShow);
                if (shouldShow)
                {
                    dungeonTutorialGuideRoot.transform.SetAsLastSibling();
                }
            }

            if (dungeonTutorialGuideTitleText != null && shouldShow)
            {
                dungeonTutorialGuideTitleText.text = DungeonTutorialTitle;
            }

            if (dungeonTutorialGuideBodyText != null && shouldShow)
            {
                dungeonTutorialGuideBodyText.text = !string.IsNullOrEmpty(tutorialEvent?.Body)
                    ? tutorialEvent.Body
                    : DungeonTutorialBody;
            }

            if (dungeonTutorialGuideFooterText != null && shouldShow)
            {
                dungeonTutorialGuideFooterText.text = DungeonTutorialFooter;
            }

            if (dungeonTutorialStartHighlight != null)
            {
                dungeonTutorialStartHighlight.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    dungeonTutorialStartHighlight.transform.SetAsLastSibling();
                }
            }

            if (shouldShow && startBattleButton != null)
            {
                startBattleButton.transform.SetAsLastSibling();
            }
        }

        private static StoryTutorialEvent GetDungeonTutorialEvent()
        {
            return StoryTutorialService.GetNextEvent(GameManager.Instance?.PlayerProfile, "DungeonSelectionPanel");
        }

        private static bool ShouldShowDungeonTutorialGuide(StoryTutorialEvent tutorialEvent)
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (StoryTutorialService.HasFinishedHomeGuide(profile))
            {
                return false;
            }

            if (tutorialEvent != null &&
                tutorialEvent.IsValid &&
                tutorialEvent.StepId == StoryTutorialService.StepFirstBattle &&
                string.Equals(tutorialEvent.TargetKey, "dungeon.start", StringComparison.Ordinal))
            {
                return true;
            }

            return profile != null &&
                !profile.HasCompletedTutorial &&
                string.Equals(profile.TutorialStepId, StoryTutorialService.StepFirstBattle, StringComparison.Ordinal);
        }

        private void AnimateDungeonTutorialGuide()
        {
            if (dungeonTutorialGuideRoot == null || !dungeonTutorialGuideRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            if (dungeonTutorialGuideCharacterImage != null)
            {
                float characterScale = Mathf.Lerp(0.985f, 1.035f, pulse);
                dungeonTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(characterScale, characterScale, 1f);
            }

            if (dungeonTutorialStartHighlight != null && dungeonTutorialStartHighlight.gameObject.activeSelf)
            {
                float frameScale = Mathf.Lerp(0.99f, 1.035f, pulse);
                dungeonTutorialStartHighlight.rectTransform.localScale = new Vector3(frameScale, frameScale, 1f);
                dungeonTutorialStartHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.82f, 1f, pulse));
            }
        }

        private BattleDungeonFloorDefinition GetSelectedFloorDefinition(BattleDungeonDefinition dungeon)
        {
            if (dungeon == null || dungeon.Floors == null)
            {
                return null;
            }

            for (int i = 0; i < dungeon.Floors.Count; i += 1)
            {
                BattleDungeonFloorDefinition floor = dungeon.Floors[i];
                if (floor != null && floor.LocalFloor == selectedLocalFloor)
                {
                    return floor;
                }
            }

            return null;
        }

        private static string BuildEnemyPreviewText(BattleDungeonDefinition dungeon, BattleDungeonFloorDefinition floor)
        {
            if (floor == null)
            {
                return string.Empty;
            }

            List<string> enemyNames = new List<string>();
            for (int i = 0; i < floor.EnemyMonsterIds.Count; i += 1)
            {
                string monsterName = ResolveMonsterName(floor.EnemyMonsterIds[i]);
                if (!string.IsNullOrEmpty(monsterName) && !enemyNames.Contains(monsterName))
                {
                    enemyNames.Add(monsterName);
                }
            }

            int finalBossCount = floor.BossMonsterIds != null ? floor.BossMonsterIds.Count : 0;
            if (floor.IsBossEncounter || finalBossCount > 0)
            {
                if (finalBossCount > 0)
                {
                    for (int i = 0; i < floor.BossMonsterIds.Count; i += 1)
                    {
                        string bossName = ResolveMonsterName(floor.BossMonsterIds[i]);
                        string bossLabel = string.IsNullOrEmpty(bossName) ? string.Empty : bossName + " ボス";
                        if (!string.IsNullOrEmpty(bossLabel) && !enemyNames.Contains(bossLabel))
                        {
                            enemyNames.Add(bossLabel);
                        }
                    }
                }
                else
                {
                    string bossName = ResolveMonsterName(floor.BossMonsterId);
                    string bossLabel = string.IsNullOrEmpty(bossName) ? string.Empty : bossName + " ボス";
                    if (!string.IsNullOrEmpty(bossLabel) && !enemyNames.Contains(bossLabel))
                    {
                        enemyNames.Add(bossLabel);
                    }
                }
            }

            string enemySummary = enemyNames.Count > 0 ? string.Join(" / ", enemyNames) : "未確認";
            string bossSuffix = finalBossCount > 1
                ? "  ボス2体出現"
                : floor.IsBossEncounter || finalBossCount > 0 ? "  ボス出現" : string.Empty;
            string riskSummary = dungeon != null
                ? HomeActionAdvisor.BuildFloorRiskSummary(
                    GameManager.Instance?.PlayerProfile,
                    BattleDungeonCatalog.ResolveGlobalFloor(dungeon.DungeonId, floor.LocalFloor))
                : string.Empty;
            string enemyLine = $"出現: {enemySummary}  敵数 {Mathf.Max(1, floor.EnemyCount)}{bossSuffix}";
            return string.IsNullOrEmpty(riskSummary)
                ? enemyLine
                : enemyLine + "\n" + riskSummary;
        }

        private static string ResolveMonsterName(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return string.Empty;
            }

            MonsterDataSO monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monsterId)
                : null;
            return monsterData != null && !string.IsNullOrEmpty(monsterData.monsterName)
                ? monsterData.monsterName
                : monsterId;
        }

        public void StartSelectedBattle()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            MasterDataManager.Instance?.Initialize();
            if (!IsFloorUnlocked(selectedDungeonId, selectedLocalFloor))
            {
                return;
            }

            GameManager.Instance?.SetCurrentDungeonFloor(selectedDungeonId, selectedLocalFloor);
            SceneManager.LoadScene(battleSceneName);
        }

        private static int ResolveMaxUnlockedGlobalFloor()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            return Mathf.Max(1, (profile?.HighestFloor ?? 0) + 1);
        }

        private static bool IsDungeonUnlocked(BattleDungeonDefinition dungeon)
        {
            return dungeon != null && dungeon.GlobalFloorStart <= ResolveMaxUnlockedGlobalFloor();
        }

        private static bool IsDungeonVisibleOnMap(BattleDungeonDefinition dungeon)
        {
            return IsDungeonUnlocked(dungeon);
        }

        private static bool IsFloorUnlocked(string dungeonId, int localFloor)
        {
            return BattleDungeonCatalog.ResolveGlobalFloor(dungeonId, localFloor) <= ResolveMaxUnlockedGlobalFloor();
        }

        private static int ResolveMaxUnlockedLocalFloor(BattleDungeonDefinition dungeon)
        {
            if (!IsDungeonUnlocked(dungeon))
            {
                return 1;
            }

            return Mathf.Clamp(
                ResolveMaxUnlockedGlobalFloor() - dungeon.GlobalFloorStart + 1,
                1,
                dungeon.Floors.Count);
        }

        private static BattleDungeonDefinition GetLatestUnlockedDungeon()
        {
            IReadOnlyList<BattleDungeonDefinition> dungeons = BattleDungeonCatalog.Dungeons;
            BattleDungeonDefinition latestUnlocked = dungeons[0];
            for (int i = 0; i < dungeons.Count; i += 1)
            {
                if (!IsDungeonUnlocked(dungeons[i]))
                {
                    break;
                }

                latestUnlocked = dungeons[i];
            }

            return latestUnlocked;
        }

        private void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            closeCallback?.Invoke();
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return obj;
        }

        private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, bool preserveAspect)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            image.preserveAspect = preserveAspect;
            return image;
        }

        private static Text CreateText(string objectName, Transform parent, string textValue, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = obj.GetComponent<Text>();
            text.font = GetRuntimeFont();
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Text CreateDungeonMetricText(string objectName, Transform parent, Vector2 anchoredPosition, Color accentColor)
        {
            GameObject chip = CreatePanel(
                objectName + "Frame",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                anchoredPosition,
                new Vector2(420f, 78f),
                new Color(0.030f, 0.044f, 0.062f, 0.94f));

            Outline outline = chip.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.34f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;

            Text text = CreateText(
                objectName,
                chip.transform,
                string.Empty,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                accentColor,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-16f, -12f));
            ConfigureSelectionInfoText(text, 18, 12);
            return text;
        }

        private static void ConfigureSelectionInfoText(Text text, int maxSize, int minSize)
        {
            if (text == null)
            {
                return;
            }

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, minSize);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, maxSize);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Button CreateTextButton(string objectName, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color, UnityEngine.Events.UnityAction onClick, int fontSize)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = obj.GetComponent<Image>();
            image.color = color;
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateText("Label", obj.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            return texture != null
                ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f)
                : null;
        }

        private static Font GetRuntimeFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
                // Fall back below.
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                return null;
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i -= 1)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static bool InvokeButtonUnderPointer(Transform root, Vector2 screenPosition)
        {
            if (root == null)
            {
                return false;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int i = buttons.Length - 1; i >= 0; i -= 1)
            {
                Button button = buttons[i];
                if (button == null || !button.IsActive() || !button.interactable)
                {
                    continue;
                }

                RectTransform rectTransform = button.transform as RectTransform;
                if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, null))
                {
                    button.onClick.Invoke();
                    return true;
                }
            }

            return false;
        }
    }
}
