using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.UI;

namespace WitchTower.Battle
{
    public sealed class ResultPanelController : MonoBehaviour
    {
        private static readonly Color WinTitleColor = new Color(0.97f, 0.88f, 0.55f, 1f);
        private static readonly Color LoseTitleColor = new Color(0.96f, 0.54f, 0.54f, 1f);
        private static readonly Color WinSummaryColor = new Color(0.95f, 0.92f, 0.76f, 0.98f);
        private static readonly Color LoseSummaryColor = new Color(0.96f, 0.82f, 0.82f, 0.98f);
        private static readonly Color WinHintColor = new Color(0.78f, 0.84f, 0.92f, 0.94f);
        private static readonly Color LoseHintColor = new Color(0.95f, 0.81f, 0.81f, 0.94f);
        private static readonly Color PrimaryActionColor = new Color(0.21f, 0.56f, 0.78f, 1f);
        private static readonly Color RetryActionColor = new Color(0.28f, 0.39f, 0.24f, 1f);
        private static readonly Color SecondaryActionColor = new Color(0.35f, 0.24f, 0.42f, 1f);
        private const string ResultPanelResourcePath = "UI/BattleResult/BattleResultPanelImage2";
        private static readonly string[] LegacyReturnHomeChromeNames =
        {
            "ReturnHomeButtonFrame",
            "ReturnHomeAura",
            "ReturnHomeAuraTag",
            "ReturnHomeAuraTagText",
            "ReturnHomeButtonAccentLeft",
            "ReturnHomeButtonAccentRight"
        };

        [SerializeField] private GameObject rootObject;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text rewardHintText;
        [SerializeField] private TMP_Text nextRewardForecastText;
        [SerializeField] private TMP_Text nextActionText;
        [SerializeField] private Button nextFloorButton;
        [SerializeField] private Button retryFloorButton;
        [SerializeField] private Button returnHomeButton;
        [SerializeField] private TMP_Text nextFloorButtonText;
        [SerializeField] private TMP_Text retryFloorButtonText;
        [SerializeField] private TMP_Text returnHomeButtonText;

        private GameObject rewardVisualRoot;
        private readonly List<GameObject> rewardVisualObjects = new List<GameObject>();

        private void Awake()
        {
            NormalizeResultLayout();
            NormalizeReturnHomeButton();
            Hide();
        }

        public void Show(BattleResultViewData viewData)
        {
            EnsureVisibleResultParent();

            if (rootObject != null)
            {
                rootObject.SetActive(true);
                rootObject.transform.SetAsLastSibling();
            }

            NormalizeResultLayout();
            ShowResultChrome(viewData);
            NormalizeReturnHomeButton();

            if (titleText != null)
            {
                titleText.text = viewData.IsWin ? "勝利" : "敗北";
                titleText.color = viewData.IsWin ? WinTitleColor : LoseTitleColor;
            }

            if (goldText != null)
            {
                goldText.text = $"ゴールド +{viewData.Gold:N0}";
            }

            if (expText != null)
            {
                expText.text = $"経験値 +{viewData.Exp:N0}";
            }

            if (summaryText != null)
            {
                summaryText.text = viewData.IsWin
                    ? $"第{viewData.ClearedFloor}階層を突破しました\n次の階層: 第{viewData.NextFloor}階層"
                    : "戦闘に敗北しました\nホームで編成や装備を見直しましょう";
                summaryText.color = viewData.IsWin ? WinSummaryColor : LoseSummaryColor;
            }

            if (rewardHintText != null)
            {
                rewardHintText.text = BuildRewardDetailText(viewData);
                rewardHintText.color = viewData.IsWin ? WinHintColor : LoseHintColor;
            }

            ShowRewardVisuals(viewData);

            if (nextRewardForecastText != null)
            {
                nextRewardForecastText.text = string.Empty;
                nextRewardForecastText.gameObject.SetActive(false);
            }

            if (nextActionText != null)
            {
                nextActionText.text = viewData.IsWin
                    ? $"第{viewData.NextFloor}階層へ進む"
                    : "ホームへ戻る";
            }

            if (nextFloorButton != null)
            {
                nextFloorButton.gameObject.SetActive(viewData.IsWin);
            }

            if (returnHomeButton != null)
            {
                returnHomeButton.gameObject.SetActive(true);
            }

            EnsureRetryFloorButton();
            if (retryFloorButton != null)
            {
                retryFloorButton.gameObject.SetActive(true);
                retryFloorButton.onClick.RemoveAllListeners();
                BattleSceneController battleSceneController = ResolveBattleSceneController();
                retryFloorButton.interactable = battleSceneController != null;
                if (battleSceneController != null)
                {
                    retryFloorButton.onClick.AddListener(battleSceneController.RetryClearedFloor);
                }
            }

            if (nextFloorButtonText != null)
            {
                nextFloorButtonText.text = $"第{viewData.NextFloor}階層へ";
            }

            if (retryFloorButtonText != null)
            {
                retryFloorButtonText.text = "この階層に再挑戦";
            }

            if (returnHomeButtonText != null)
            {
                returnHomeButtonText.gameObject.SetActive(false);
            }

            ApplyButtonEmphasis(nextFloorButton, viewData.IsWin ? PrimaryActionColor : SecondaryActionColor);
            ApplyButtonEmphasis(retryFloorButton, RetryActionColor);
            HomeReturnButtonStyle.Apply(returnHomeButton);
        }

        public void Hide()
        {
            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }

            ClearRewardVisuals();

            if (returnHomeButton != null)
            {
                returnHomeButton.gameObject.SetActive(false);
            }

            if (retryFloorButton != null)
            {
                retryFloorButton.gameObject.SetActive(false);
            }
        }

        private void NormalizeReturnHomeButton()
        {
            if (returnHomeButton == null)
            {
                return;
            }

            Canvas canvas = returnHomeButton.GetComponentInParent<Canvas>(true);
            Transform targetParent = canvas != null && canvas.gameObject.activeInHierarchy
                ? canvas.transform
                : ResolveActiveResultCanvas();

            targetParent = targetParent != null
                ? targetParent
                : rootObject != null && rootObject.transform.parent != null
                    ? rootObject.transform.parent
                    : rootObject != null
                        ? rootObject.transform
                        : returnHomeButton.transform.parent;

            if (targetParent != null && returnHomeButton.transform.parent != targetParent)
            {
                returnHomeButton.transform.SetParent(targetParent, false);
            }

            HomeReturnButtonStyle.Apply(returnHomeButton);
            HideLegacyReturnHomeChrome();
            returnHomeButton.transform.SetAsLastSibling();
        }

        private void EnsureVisibleResultParent()
        {
            if (rootObject == null)
            {
                return;
            }

            Transform currentParent = rootObject.transform.parent;
            if (currentParent != null && currentParent.gameObject.activeInHierarchy)
            {
                return;
            }

            Transform targetParent = ResolveActiveResultCanvas();
            if (targetParent == null)
            {
                targetParent = currentParent;
                if (targetParent != null)
                {
                    targetParent.gameObject.SetActive(true);
                }
            }

            if (targetParent != null && rootObject.transform.parent != targetParent)
            {
                rootObject.transform.SetParent(targetParent, false);
            }
        }

        private static Transform ResolveActiveResultCanvas()
        {
            GameObject minimalCanvas = GameObject.Find("BattleMinimalCanvas");
            if (minimalCanvas != null && minimalCanvas.activeInHierarchy)
            {
                return minimalCanvas.transform;
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                {
                    return canvas.transform;
                }
            }

            return null;
        }

        private static void HideLegacyReturnHomeChrome()
        {
            foreach (string objectName in LegacyReturnHomeChromeNames)
            {
                GameObject target = GameObject.Find(objectName);
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        private static void ApplyButtonEmphasis(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static string BuildRewardDetailText(BattleResultViewData viewData)
        {
            if (!viewData.IsWin)
            {
                return "今回の獲得報酬はありません。\n強化・編成・装備を整えて再挑戦しましょう。";
            }

            var lines = new List<string>
            {
                $"プレイヤー経験値 +{viewData.Exp:N0}",
                viewData.PartyMonsterCount > 0
                    ? $"パーティ経験値 +{viewData.PartyMonsterExp:N0} / {viewData.PartyMonsterCount}体"
                    : "パーティ経験値 なし"
            };

            if (viewData.PlayerLevelBefore > 0 &&
                viewData.PlayerLevelAfter > viewData.PlayerLevelBefore)
            {
                lines.Add($"レベルアップ: Lv.{viewData.PlayerLevelBefore} -> Lv.{viewData.PlayerLevelAfter}");
            }

            return string.Join("\n", lines);
        }

        private void ShowResultChrome(BattleResultViewData viewData)
        {
            SetObjectActive(titleText, true);
            SetObjectActive(goldText, true);
            SetObjectActive(expText, true);
            SetObjectActive(summaryText, true);
            SetObjectActive(rewardHintText, true);
            SetObjectActive(nextRewardForecastText, false);
            SetObjectActive(nextActionText, true);
            ApplyGeneratedPanelSprite();

            if (goldText != null && goldText.transform.parent != null)
            {
                goldText.transform.parent.gameObject.SetActive(true);
            }

            if (nextActionText != null && nextActionText.transform.parent != null)
            {
                nextActionText.transform.parent.gameObject.SetActive(true);
            }

            if (nextFloorButton != null && nextFloorButton.transform.parent != null)
            {
                nextFloorButton.transform.parent.gameObject.SetActive(true);
            }

            if (nextFloorButton != null)
            {
                nextFloorButton.gameObject.SetActive(viewData.IsWin);
            }

            if (returnHomeButton != null)
            {
                returnHomeButton.gameObject.SetActive(true);
            }

            EnsureRetryFloorButton();
            if (retryFloorButton != null)
            {
                retryFloorButton.gameObject.SetActive(true);
            }
        }

        private static void SetObjectActive(TMP_Text text, bool active)
        {
            if (text != null)
            {
                text.gameObject.SetActive(active);
            }
        }

        private void EnsureRetryFloorButton()
        {
            if (retryFloorButton != null && retryFloorButtonText != null)
            {
                ConfigureRetryFloorButtonRect();
                return;
            }

            Transform rootTransform = rootObject != null ? rootObject.transform : transform;
            Transform parent = nextFloorButton != null && nextFloorButton.transform.parent != null
                ? nextFloorButton.transform.parent
                : rootTransform;

            Transform existing = parent.Find("RetryFloorButton");
            if (existing != null)
            {
                retryFloorButton = existing.GetComponent<Button>();
                retryFloorButtonText = existing.Find("Label")?.GetComponent<TMP_Text>();
                ConfigureRetryFloorButtonRect();
                return;
            }

            GameObject buttonObject = new GameObject("RetryFloorButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            retryFloorButton = buttonObject.GetComponent<Button>();

            Image image = buttonObject.GetComponent<Image>();
            image.color = RetryActionColor;
            image.raycastTarget = true;
            retryFloorButton.targetGraphic = image;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            retryFloorButtonText = labelObject.GetComponent<TMP_Text>();
            retryFloorButtonText.fontSize = 18f;
            retryFloorButtonText.fontSizeMin = 10f;
            retryFloorButtonText.fontSizeMax = 18f;
            retryFloorButtonText.enableAutoSizing = true;
            retryFloorButtonText.fontStyle = FontStyles.Bold;
            retryFloorButtonText.alignment = TextAlignmentOptions.Center;
            retryFloorButtonText.color = Color.white;
            retryFloorButtonText.textWrappingMode = TextWrappingModes.Normal;
            retryFloorButtonText.overflowMode = TextOverflowModes.Ellipsis;
            retryFloorButtonText.raycastTarget = false;
            ConfigureRetryFloorButtonRect();
        }

        private void ConfigureRetryFloorButtonRect()
        {
            RectTransform retryRect = retryFloorButton != null
                ? retryFloorButton.GetComponent<RectTransform>()
                : null;
            if (retryRect == null)
            {
                return;
            }

            retryRect.anchorMin = new Vector2(0.37f, 0.12f);
            retryRect.anchorMax = new Vector2(0.63f, 0.88f);
            retryRect.offsetMin = Vector2.zero;
            retryRect.offsetMax = Vector2.zero;

            RectTransform nextRect = nextFloorButton != null
                ? nextFloorButton.GetComponent<RectTransform>()
                : null;
            if (nextRect != null)
            {
                nextRect.anchorMin = new Vector2(0.06f, 0.12f);
                nextRect.anchorMax = new Vector2(0.32f, 0.88f);
                nextRect.offsetMin = Vector2.zero;
                nextRect.offsetMax = Vector2.zero;
            }
        }

        private static BattleSceneController ResolveBattleSceneController()
        {
            BattleSceneController[] controllers = FindObjectsOfType<BattleSceneController>(true);
            return controllers != null && controllers.Length > 0 ? controllers[0] : null;
        }

        private void ApplyGeneratedPanelSprite()
        {
            Transform rootTransform = rootObject != null ? rootObject.transform : transform;
            Image frameImage = rootTransform.Find("ResultFrame")?.GetComponent<Image>();
            if (frameImage == null)
            {
                return;
            }

            Sprite panelSprite = BattleVisualResolver.LoadSprite(ResultPanelResourcePath);
            if (panelSprite == null)
            {
                return;
            }

            frameImage.sprite = panelSprite;
            frameImage.color = Color.white;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
        }

        private void ShowRewardVisuals(BattleResultViewData viewData)
        {
            EnsureRewardVisualRoot();
            if (rewardVisualRoot == null)
            {
                return;
            }

            ClearRewardVisuals();

            BattleResultRewardVisual[] visuals = viewData.RewardVisuals;
            if (!viewData.IsWin || visuals == null || visuals.Length == 0)
            {
                rewardVisualRoot.SetActive(false);
                return;
            }

            rewardVisualRoot.SetActive(true);
            int count = Mathf.Min(visuals.Length, 4);
            float spacing = count > 1 ? 148f : 0f;
            float startX = -((count - 1) * spacing * 0.5f);
            for (int i = 0; i < count; i += 1)
            {
                GameObject slot = CreateRewardVisual(visuals[i], rewardVisualRoot.transform);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                rewardVisualObjects.Add(slot);
            }
        }

        private void EnsureRewardVisualRoot()
        {
            Transform rootTransform = rootObject != null ? rootObject.transform : transform;
            if (rewardVisualRoot == null)
            {
                Transform existing = rootTransform.Find("RewardVisuals");
                rewardVisualRoot = existing != null
                    ? existing.gameObject
                    : new GameObject("RewardVisuals", typeof(RectTransform));
            }

            RectTransform rect = rewardVisualRoot.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = rewardVisualRoot.AddComponent<RectTransform>();
            }

            if (rewardVisualRoot.transform.parent != rootTransform)
            {
                rect.SetParent(rootTransform, false);
            }

            rect.anchorMin = new Vector2(0.5f, 0.30f);
            rect.anchorMax = new Vector2(0.5f, 0.30f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(660f, 150f);
        }

        private static GameObject CreateRewardVisual(BattleResultRewardVisual visual, Transform parent)
        {
            GameObject slot = new GameObject("RewardVisualSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(136f, 150f);

            Image background = slot.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.05f, 0.58f);
            background.raycastTarget = false;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(slot.transform, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 20f);
            iconRect.sizeDelta = new Vector2(82f, 82f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = BattleVisualResolver.LoadSprite(visual.IconResourcePath);
            icon.color = icon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject frameObject = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.SetParent(slot.transform, false);
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = new Vector2(0f, 20f);
            frameRect.sizeDelta = new Vector2(116f, 116f);
            Image frame = frameObject.GetComponent<Image>();
            frame.sprite = BattleVisualResolver.LoadSprite(visual.FrameResourcePath);
            frame.color = frame.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            frame.preserveAspect = true;
            frame.raycastTarget = false;

            Text label = CreateRewardVisualText("Label", slot.transform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.23f), 14, Color.white);
            label.text = visual.DisplayName;

            if (!string.IsNullOrEmpty(visual.DetailText))
            {
                Text detail = CreateRewardVisualText("Detail", slot.transform, new Vector2(0.12f, 0.77f), new Vector2(0.88f, 0.92f), 13, new Color(1f, 0.93f, 0.68f, 1f));
                detail.text = visual.DetailText;
            }

            return slot;
        }

        private static Text CreateRewardVisualText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int maxFontSize, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = ResolveBuiltinUiFont();
            text.fontSize = maxFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = maxFontSize;
            text.raycastTarget = false;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static Font ResolveBuiltinUiFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
            }

            if (font != null)
            {
                return font;
            }

            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
            }

            return font;
        }

        private void ClearRewardVisuals()
        {
            if (rewardVisualRoot == null)
            {
                for (int i = 0; i < rewardVisualObjects.Count; i += 1)
                {
                    if (rewardVisualObjects[i] != null)
                    {
                        Destroy(rewardVisualObjects[i]);
                    }
                }

                rewardVisualObjects.Clear();
                return;
            }

            for (int i = rewardVisualRoot.transform.childCount - 1; i >= 0; i -= 1)
            {
                Transform child = rewardVisualRoot.transform.GetChild(i);
                if (child != null && child.name == "RewardVisualSlot")
                {
                    Destroy(child.gameObject);
                }
            }

            rewardVisualObjects.Clear();
        }

        private void NormalizeResultLayout()
        {
            RectTransform rootRect = rootObject != null
                ? rootObject.GetComponent<RectTransform>()
                : GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(780f, 620f);
            }

            Transform rootTransform = rootObject != null ? rootObject.transform : transform;
            ConfigureRect(rootTransform.Find("ResultFrame") as RectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 560f));
            ConfigureText(titleText, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(660f, 56f), 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            ConfigureText(summaryText, new Vector2(0.5f, 0.61f), new Vector2(0f, 0f), new Vector2(660f, 66f), 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            ConfigureText(rewardHintText, new Vector2(0.5f, 0.46f), new Vector2(0f, 0f), new Vector2(680f, 92f), 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            ConfigureText(nextRewardForecastText, new Vector2(0.5f, 0.25f), new Vector2(0f, 0f), new Vector2(660f, 42f), 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            ConfigureText(nextActionText, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 32f), 16f, FontStyles.Bold, TextAlignmentOptions.Center);

            Transform rewardStrip = goldText != null ? goldText.transform.parent : null;
            ConfigureRect(rewardStrip as RectTransform, new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(660f, 96f));
            ConfigureRect(rewardStrip != null ? rewardStrip.Find("RewardStripFrame") as RectTransform : null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 44f));
            ConfigureText(goldText, new Vector2(0.30f, 0.36f), Vector2.zero, new Vector2(260f, 36f), 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            ConfigureText(expText, new Vector2(0.70f, 0.36f), Vector2.zero, new Vector2(260f, 36f), 21f, FontStyles.Bold, TextAlignmentOptions.Center);

            Transform nextMoveStrip = nextActionText != null ? nextActionText.transform.parent : null;
            ConfigureRect(nextMoveStrip as RectTransform, new Vector2(0.5f, 0.17f), Vector2.zero, new Vector2(640f, 46f));
            ConfigureRect(nextMoveStrip != null ? nextMoveStrip.Find("NextMoveStripFrame") as RectTransform : null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 22f));

            Transform buttonRail = nextFloorButton != null ? nextFloorButton.transform.parent : null;
            ConfigureRect(buttonRail as RectTransform, new Vector2(0.5f, 0.055f), Vector2.zero, new Vector2(640f, 70f));
            ConfigureRect(buttonRail != null ? buttonRail.Find("ResultButtonRailFrame") as RectTransform : null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 38f));
        }

        private static void ConfigureText(TMP_Text text, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            if (text == null)
            {
                return;
            }

            ConfigureRect(text.rectTransform, anchor, anchoredPosition, size);
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static void ConfigureRect(RectTransform rectTransform, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
