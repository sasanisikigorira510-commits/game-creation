using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class RebirthPanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private RebirthSkillStatusView[] skillViews;

        private const string SkillTreeBoardSpritePath = "UI/SkillTree/SkillTreeBoardImage2";
        private const string SkillTreeNodeCoreSpritePath = "UI/SkillTree/SkillTreeNodeCoreImage2";
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 1720f;
        private const float TreeTopY = -398f;
        private const float TreeColumnSpacing = 286f;
        private const float TreeRowSpacing = 272f;
        private const float NodeWidth = 140f;
        private const float NodeHeight = 156f;
        private const float NodeCoreSize = 104f;
        private const float NodeCoreCenterY = -52f;
        private const float NodeLevelTextHeight = 22f;
        private const float BottomRowNodeHorizontalOffset = 3f;
        private const float BottomRowNodeVerticalOffset = -17f;

        private static readonly Color PageTint = new Color(0.006f, 0.007f, 0.016f, 0.96f);
        private static readonly Color PanelColor = new Color(0.035f, 0.030f, 0.058f, 0.98f);
        private static readonly Color LockedNodeColor = new Color(0.18f, 0.24f, 0.30f, 0.68f);
        private static readonly Color AvailableNodeColor = new Color(0.50f, 0.64f, 0.76f, 0.84f);
        private static readonly Color ActivatedNodeColor = Color.white;
        private static readonly Color ActivatedOutlineColor = new Color(0.02f, 1.00f, 0.84f, 0.96f);
        private static readonly Color MaxNodeColor = new Color(1f, 0.94f, 0.62f, 1f);
        private static readonly Color AccentGold = new Color(1f, 0.78f, 0.30f, 1f);
        private static readonly Color AccentBlue = new Color(0.40f, 0.86f, 1f, 1f);
        private static readonly Color TextMain = new Color(1f, 0.98f, 0.92f, 1f);
        private static readonly Color TextSub = new Color(0.76f, 0.82f, 0.94f, 1f);
        private static readonly Color TransparentHitColor = new Color(0f, 0f, 0f, 0.001f);
        private static readonly Dictionary<Vector2Int, Vector2> BoardSocketCenters = new Dictionary<Vector2Int, Vector2>
        {
            { new Vector2Int(0, 0), new Vector2(-265f, -456f) },
            { new Vector2Int(1, 0), new Vector2(-2f, -456f) },
            { new Vector2Int(2, 0), new Vector2(263f, -456f) },
            { new Vector2Int(0, 1), new Vector2(-226f, -774f) },
            { new Vector2Int(1, 1), new Vector2(-4f, -774f) },
            { new Vector2Int(2, 1), new Vector2(218f, -774f) },
            { new Vector2Int(0, 2), new Vector2(-183f, -1085f) },
            { new Vector2Int(1, 2), new Vector2(-5f, -1085f) },
            { new Vector2Int(2, 2), new Vector2(174f, -1085f) }
        };
        private Action onClosed;
        private Font runtimeFont;
        private Sprite runtimeBoardSprite;
        private Sprite runtimeNodeCoreSprite;
        private Text runtimeSummaryText;
        private Text runtimeMessageText;
        private Button runtimeRebirthButton;
        private Text runtimeRebirthButtonText;
        private readonly List<RebirthSkillNodeRuntime> runtimeNodes = new List<RebirthSkillNodeRuntime>();
        private bool isRuntimeBuilt;

        private sealed class RebirthSkillNodeRuntime
        {
            public string SkillId;
            public Button Button;
            public Image SocketImage;
            public Outline SocketOutline;
            public Text TitleText;
            public Text LevelText;
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            BuildRuntimeViewIfNeeded();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        public void Refresh()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (summaryText != null)
            {
                summaryText.text = BuildSummary(profile);
            }

            RefreshRuntimeView(profile);

            if (skillViews == null)
            {
                return;
            }

            foreach (RebirthSkillStatusView skillView in skillViews)
            {
                if (skillView != null)
                {
                    skillView.Bind(profile);
                }
            }
        }

        public void Rebirth()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (!RebirthService.TryRebirth(profile, out int gainedPoints))
            {
                SetRuntimeMessage(profile == null ? "プレイヤーデータがありません。" : $"Lv.{RebirthService.MinimumLevel}から転生できます。");
                Refresh();
                return;
            }

            GameManager.Instance.SetCurrentFloor(1);
            SaveManager.Instance.SaveCurrentGame();
            SetRuntimeMessage($"+{gainedPoints}魂片を獲得しました。");
            Refresh();
        }

        public void PurchaseSkill(string skillId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (!RebirthService.TryPurchaseSkill(profile, skillId, out string blockedReason))
            {
                SetRuntimeMessage(blockedReason);
                Refresh();
                return;
            }

            SaveManager.Instance.SaveCurrentGame();
            RebirthSkillDefinition definition = RebirthSkillCatalog.GetDefinition(skillId);
            SetRuntimeMessage(definition != null ? $"{definition.DisplayName} を強化しました。" : "刻印を強化しました。");
            Refresh();
        }

        public void ActivateAllSkillsForPreview()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            foreach (RebirthSkillDefinition definition in RebirthSkillCatalog.Definitions)
            {
                profile.SetRebirthSkillLevel(definition.SkillId, definition.MaxLevel);
            }

            profile.RebirthPoints = Mathf.Max(profile.RebirthPoints, 999);
            profile.TotalRebirthPoints = Mathf.Max(profile.TotalRebirthPoints, 999);
            SaveManager.Instance?.SaveCurrentGame();
            SetRuntimeMessage("プレビュー用に全魂樹ノードを活性化しました。");
            Refresh();
        }

        public void PurchaseAttackPact()
        {
            PurchaseSkill(RebirthSkillCatalog.AttackPactId);
        }

        public void PurchaseHpOath()
        {
            PurchaseSkill(RebirthSkillCatalog.HpOathId);
        }

        public void PurchaseExpMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.ExpMemoryId);
        }

        public void PurchaseCriticalMark()
        {
            PurchaseSkill(RebirthSkillCatalog.CriticalMarkId);
        }

        public void PurchaseDefenseOath()
        {
            PurchaseSkill(RebirthSkillCatalog.DefenseOathId);
        }

        public void PurchaseGoldMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.GoldMemoryId);
        }

        public void PurchaseTempoMemory()
        {
            PurchaseSkill(RebirthSkillCatalog.TempoMemoryId);
        }

        private static string BuildSummary(PlayerProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            int reward = profile.GetPendingRebirthPointReward();
            string rebirthStatus = reward > 0
                ? $"転生可能: +{reward}魂片"
                : $"転生解放: Lv.{RebirthService.MinimumLevel}";

            return $"魂片 {profile.RebirthPoints} / 累計 {profile.TotalRebirthPoints} / 転生 {profile.RebirthCount}回\n{rebirthStatus}";
        }

        private void BuildRuntimeViewIfNeeded()
        {
            if (isRuntimeBuilt)
            {
                return;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            runtimeBoardSprite = Resources.Load<Sprite>(SkillTreeBoardSpritePath);
            runtimeNodeCoreSprite = Resources.Load<Sprite>(SkillTreeNodeCoreSpritePath);

            RectTransform rootRect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlay = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            overlay.color = PageTint;
            overlay.raycastTarget = true;

            GameObject panel = CreatePanel("SkillTreeMainPanel", transform, Vector2.zero, new Vector2(PanelWidth, PanelHeight), PanelColor);
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null && runtimeBoardSprite != null)
            {
                panelImage.sprite = runtimeBoardSprite;
                panelImage.color = Color.white;
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
            }

            CreateText("Title", panel.transform, "魂樹スキル", 52, FontStyle.Bold,
                new Vector2(0f, -58f), new Vector2(620f, 72f), AccentGold, TextAnchor.MiddleCenter);
            CreateText("Subtitle", panel.transform, "転生で得た魂片を刻み、次の周回を強くする", 22, FontStyle.Bold,
                new Vector2(0f, -120f), new Vector2(760f, 42f), TextSub, TextAnchor.MiddleCenter);

            GameObject summaryPanel = CreatePanel("SkillTreeSummaryPanel", panel.transform,
                new Vector2(0f, -204f), new Vector2(850f, 116f), new Color(0.020f, 0.030f, 0.055f, 0.96f));
            runtimeSummaryText = CreateText("SkillTreeSummary", summaryPanel.transform, string.Empty, 25, FontStyle.Bold,
                new Vector2(-120f, -34f), new Vector2(540f, 78f), TextMain, TextAnchor.MiddleLeft);
            runtimeRebirthButton = CreateButton("RebirthButton", summaryPanel.transform, "転生", new Vector2(304f, -34f), new Vector2(190f, 68f), Rebirth);
            runtimeRebirthButtonText = runtimeRebirthButton.GetComponentInChildren<Text>(true);

            List<RebirthSkillDefinition> definitions = RebirthSkillCatalog.Definitions
                .OrderBy(x => x.TreeRow)
                .ThenBy(x => x.TreeColumn)
                .ToList();
            Dictionary<string, Vector2> nodePositions = BuildNodePositions(definitions);

            // The background artwork defines this tree's routes. Do not layer a second,
            // data-drawn graph over it, otherwise the two visual routes can disagree.

            GameObject nodeLayer = CreateLayer("SkillTreeNodeLayer", panel.transform);
            foreach (RebirthSkillDefinition definition in definitions)
            {
                if (nodePositions.TryGetValue(definition.SkillId, out Vector2 position))
                {
                    runtimeNodes.Add(CreateSkillNode(nodeLayer.transform, definition.SkillId, position));
                }
            }

            runtimeMessageText = CreateText("SkillTreeMessage", panel.transform, string.Empty, 22, FontStyle.Bold,
                new Vector2(0f, -1244f), new Vector2(820f, 42f), AccentBlue, TextAnchor.MiddleCenter);

            HomeReturnButtonStyle.Create(transform, "SkillTreeCloseButton", Hide);
            isRuntimeBuilt = true;
        }

        private void RefreshRuntimeView(PlayerProfile profile)
        {
            if (!isRuntimeBuilt)
            {
                return;
            }

            if (runtimeSummaryText != null)
            {
                runtimeSummaryText.text = BuildSummary(profile);
            }

            int reward = profile != null ? profile.GetPendingRebirthPointReward() : 0;
            if (runtimeRebirthButton != null)
            {
                runtimeRebirthButton.interactable = reward > 0;
            }

            if (runtimeRebirthButtonText != null)
            {
                runtimeRebirthButtonText.text = reward > 0 ? $"+{reward}魂片" : $"Lv.{RebirthService.MinimumLevel}";
            }

            for (int i = 0; i < runtimeNodes.Count; i += 1)
            {
                RefreshRuntimeNode(runtimeNodes[i], profile);
            }
        }

        private void RefreshRuntimeNode(RebirthSkillNodeRuntime node, PlayerProfile profile)
        {
            if (node == null)
            {
                return;
            }

            RebirthSkillDefinition definition = RebirthSkillCatalog.GetDefinition(node.SkillId);
            if (definition == null)
            {
                return;
            }

            int currentLevel = profile != null ? profile.GetRebirthSkillLevel(node.SkillId) : 0;
            bool canPurchase = RebirthService.CanPurchaseSkill(profile, node.SkillId, out _);
            bool isMax = currentLevel >= definition.MaxLevel;
            bool isActivated = currentLevel > 0;

            if (node.SocketImage != null)
            {
                node.SocketImage.color = isMax
                    ? MaxNodeColor
                    : isActivated
                        ? ActivatedNodeColor
                    : canPurchase
                        ? AvailableNodeColor
                        : LockedNodeColor;
            }

            if (node.SocketOutline != null)
            {
                node.SocketOutline.effectColor = isMax
                    ? new Color(1f, 0.76f, 0.16f, 0.96f)
                    : isActivated
                        ? ActivatedOutlineColor
                        : canPurchase
                            ? new Color(0.94f, 0.82f, 0.47f, 0.82f)
                            : new Color(0.25f, 0.29f, 0.38f, 0.48f);
                node.SocketOutline.effectDistance = isActivated
                    ? new Vector2(2.8f, -2.8f)
                    : new Vector2(1.2f, -1.2f);
            }

            if (node.Button != null)
            {
                node.Button.interactable = true;
            }

            if (node.TitleText != null)
            {
                node.TitleText.text = definition.DisplayName;
            }

            if (node.LevelText != null)
            {
                node.LevelText.text = $"Lv.{currentLevel}/{definition.MaxLevel}";
                node.LevelText.color = isMax
                    ? AccentGold
                    : isActivated
                        ? ActivatedNodeColor
                        : AccentBlue;
            }

        }

        private static Dictionary<string, Vector2> BuildNodePositions(IReadOnlyList<RebirthSkillDefinition> definitions)
        {
            Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
            if (definitions == null || definitions.Count == 0)
            {
                return positions;
            }

            int minColumn = definitions.Min(x => x.TreeColumn);
            int maxColumn = definitions.Max(x => x.TreeColumn);
            float centerColumn = (minColumn + maxColumn) * 0.5f;

            foreach (RebirthSkillDefinition definition in definitions)
            {
                Vector2Int socketKey = new Vector2Int(definition.TreeColumn, definition.TreeRow);
                Vector2 socketCenter = BoardSocketCenters.TryGetValue(socketKey, out Vector2 mappedCenter)
                    ? mappedCenter
                    : new Vector2(
                        (definition.TreeColumn - centerColumn) * TreeColumnSpacing,
                        TreeTopY - definition.TreeRow * TreeRowSpacing + NodeCoreCenterY);
                float verticalOffset = definition.TreeRow == 2 ? BottomRowNodeVerticalOffset : 0f;
                float horizontalOffset = definition.TreeRow == 2 ? BottomRowNodeHorizontalOffset : 0f;
                Vector2 fineAdjustment = GetNodeFineAdjustment(definition.SkillId);
                positions[definition.SkillId] = socketCenter
                    - new Vector2(0f, NodeCoreCenterY)
                    + new Vector2(horizontalOffset, verticalOffset)
                    + fineAdjustment;
            }

            return positions;
        }

        private static Vector2 GetNodeFineAdjustment(string skillId)
        {
            switch (skillId)
            {
                case RebirthSkillCatalog.TempoMemoryId:
                    return new Vector2(-1f, 3f);
                case RebirthSkillCatalog.DeepMemoryId:
                    return new Vector2(0f, 3f);
                case RebirthSkillCatalog.GreatTreeBlessingId:
                    return new Vector2(3f, 3f);
                case RebirthSkillCatalog.CriticalMarkId:
                case RebirthSkillCatalog.GoldMemoryId:
                case RebirthSkillCatalog.DefenseOathId:
                    return new Vector2(2f, -3f);
                default:
                    return Vector2.zero;
            }
        }

        private RebirthSkillNodeRuntime CreateSkillNode(Transform parent, string skillId, Vector2 position)
        {
            GameObject root = CreatePanel("SkillNode_" + skillId, parent, position, new Vector2(NodeWidth, NodeHeight), TransparentHitColor);
            Image hitImage = root.GetComponent<Image>();
            hitImage.raycastTarget = true;

            GameObject core = CreatePanel("Core", root.transform, Vector2.zero, new Vector2(NodeCoreSize, NodeCoreSize), Color.white);
            Image coreImage = core.GetComponent<Image>();
            coreImage.raycastTarget = false;
            if (runtimeNodeCoreSprite != null)
            {
                coreImage.sprite = runtimeNodeCoreSprite;
                coreImage.type = Image.Type.Simple;
                coreImage.preserveAspect = true;
            }

            Outline socketOutline = core.AddComponent<Outline>();
            socketOutline.effectColor = new Color(0.24f, 0.72f, 0.86f, 0.42f);
            socketOutline.effectDistance = new Vector2(1.2f, -1.2f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = coreImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => PurchaseSkill(skillId));

            Text level = CreateText("Level", root.transform, string.Empty, 14, FontStyle.Bold,
                new Vector2(0f, NodeCoreCenterY + NodeLevelTextHeight * 0.5f),
                new Vector2(76f, NodeLevelTextHeight), AccentBlue, TextAnchor.MiddleCenter);
            Text title = CreateText("Title", root.transform, string.Empty, 16, FontStyle.Bold,
                new Vector2(0f, -121f), new Vector2(150f, 28f), TextMain, TextAnchor.MiddleCenter);

            return new RebirthSkillNodeRuntime
            {
                SkillId = skillId,
                Button = button,
                SocketImage = coreImage,
                SocketOutline = socketOutline,
                TitleText = title,
                LevelText = level
            };
        }

        private GameObject CreateLayer(string name, Transform parent)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            return root;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject root = CreatePanel(name, parent, position, size, new Color(0.55f, 0.20f, 0.07f, 1f));
            Image image = root.GetComponent<Image>();
            image.raycastTarget = true;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.77f, 0.34f, 0.88f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(onClick);
            Vector2 labelSize = size - new Vector2(14f, 12f);
            Vector2 labelPosition = new Vector2(0f, -(size.y - labelSize.y) * 0.5f);
            CreateText("Label", root.transform, label, 22, FontStyle.Bold,
                labelPosition, labelSize, TextMain, TextAnchor.MiddleCenter);
            return button;
        }

        private GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return root;
        }

        private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, Vector2 position, Vector2 size, Color color, TextAnchor alignment)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = root.GetComponent<Text>();
            text.font = runtimeFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private void SetRuntimeMessage(string message)
        {
            if (runtimeMessageText != null)
            {
                runtimeMessageText.text = message ?? string.Empty;
            }
        }
    }
}
