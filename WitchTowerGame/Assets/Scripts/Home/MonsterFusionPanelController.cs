using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

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

        private const float RosterRowHeight = 112f;
        private const float RosterRowSpacing = 10f;
        private const string BackgroundSpritePath = "UI/FusionPage/FusionBackground";
        private const string MainFrameSpritePath = "UI/FusionPage/FusionMainFrame";
        private const string ParentSlotSpritePath = "UI/FusionPage/FusionParentSlot";
        private const string ResultSlotSpritePath = "UI/FusionPage/FusionResultSlot";
        private const string RosterFrameSpritePath = "UI/FusionPage/FusionRosterFrame";
        private const string ConfirmButtonSpritePath = "UI/FusionPage/FusionConfirmButton";
        private const string SmallButtonSpritePath = "UI/FusionPage/FusionSmallButton";

        private static readonly Color PageTint = new Color(0.01f, 0.02f, 0.025f, 0.96f);
        private static readonly Color MainPanelColor = new Color(0.04f, 0.075f, 0.08f, 0.96f);
        private static readonly Color SlotColor = new Color(0.035f, 0.055f, 0.065f, 0.97f);
        private static readonly Color ResultSlotColor = new Color(0.055f, 0.075f, 0.04f, 0.98f);
        private static readonly Color RosterColor = new Color(0.018f, 0.035f, 0.043f, 0.97f);
        private static readonly Color AccentGold = new Color(1f, 0.76f, 0.32f, 1f);
        private static readonly Color TextMain = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextSub = new Color(0.78f, 0.88f, 0.92f, 0.95f);
        private static readonly Color ParentButtonColor = new Color(0.16f, 0.36f, 0.42f, 1f);
        private static readonly Color ParentButtonSelectedColor = new Color(0.8f, 0.54f, 0.16f, 1f);
        private static readonly Color FuseButtonColor = new Color(0.24f, 0.62f, 0.36f, 1f);

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly List<GameObject> rosterRows = new List<GameObject>();

        private Action onClosed;
        private Font runtimeFont;
        private RectTransform rosterContent;
        private FusionSlotView parentASlot;
        private FusionSlotView parentBSlot;
        private FusionSlotView resultSlot;
        private Text statusLabel;
        private Text rosterTitleLabel;
        private Button fuseButton;
        private string parentAInstanceId;
        private string parentBInstanceId;
        private bool isBuilt;

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            parentAInstanceId = string.Empty;
            parentBInstanceId = string.Empty;
            RefreshRoster();
            RefreshPreview();
        }

        private void Hide()
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
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

            CreateFullScreenImage("FusionBackground", transform, BackgroundSpritePath, new Color(0.015f, 0.03f, 0.035f, 0.98f));

            GameObject panel = CreatePanel("FusionMainPanel", transform, MainFrameSpritePath,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 1710f), MainPanelColor);

            CreateText("Title", panel.transform, "モンスター配合", 48, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(500f, 62f), TextAnchor.MiddleCenter, AccentGold);

            CreateText("RuleHint", panel.transform, "通常配合: 同種族・同クラスは次クラスへ進化。それ以外は高い方のクラスで親1の種族になります。", 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -112f), new Vector2(880f, 42f), TextAnchor.MiddleCenter, TextSub);

            CreateButton("CloseButton", panel.transform, "戻る",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-38f, -44f), new Vector2(150f, 66f), SmallButtonSpritePath, new Color(0.36f, 0.22f, 0.16f, 1f), Hide);

            GameObject ritualPanel = CreatePanel("FusionRitualPanel", panel.transform, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -440f), new Vector2(920f, 580f), new Color(0.02f, 0.045f, 0.052f, 0.96f));

            CreateText("RitualTitle", ritualPanel.transform, "配合の間", 28, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(360f, 40f), TextAnchor.MiddleCenter, TextMain);

            parentASlot = CreateFusionSlot("ParentASlot", ritualPanel.transform, "親1", ParentSlotSpritePath, SlotColor, new Vector2(-300f, -234f));
            parentBSlot = CreateFusionSlot("ParentBSlot", ritualPanel.transform, "親2", ParentSlotSpritePath, SlotColor, new Vector2(0f, -234f));
            resultSlot = CreateFusionSlot("ResultSlot", ritualPanel.transform, "誕生", ResultSlotSpritePath, ResultSlotColor, new Vector2(300f, -234f));

            CreateText("FormulaText", ritualPanel.transform, "親1 + 親2  =>  配合結果", 22, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 102f), new Vector2(560f, 36f), TextAnchor.MiddleCenter, AccentGold);

            CreateButton("SwapButton", ritualPanel.transform, "親を入替",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-164f, 40f), new Vector2(230f, 62f), SmallButtonSpritePath, ParentButtonColor, SwapParents);

            fuseButton = CreateButton("FuseButton", ritualPanel.transform, "配合する",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(164f, 40f), new Vector2(250f, 62f), ConfirmButtonSpritePath, FuseButtonColor, FuseSelectedParents).GetComponent<Button>();

            statusLabel = CreateText("StatusLabel", panel.transform, string.Empty, 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -755f), new Vector2(880f, 50f), TextAnchor.MiddleCenter, new Color(0.94f, 0.98f, 1f, 0.96f));

            GameObject rosterPanel = CreatePanel("FusionRosterPanel", panel.transform, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1240f), new Vector2(920f, 820f), RosterColor);

            rosterTitleLabel = CreateText("RosterTitle", rosterPanel.transform, "所持モンスター", 27, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(520f, 42f), TextAnchor.MiddleCenter, TextMain);

            GameObject viewport = CreatePanel("Viewport", rosterPanel.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(850f, 706f), new Color(0f, 0f, 0f, 0.24f));
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

            isBuilt = true;
        }

        private FusionSlotView CreateFusionSlot(string name, Transform parent, string title, string spritePath, Color color, Vector2 anchoredPosition)
        {
            GameObject slotObject = CreatePanel(name, parent, spritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                anchoredPosition, new Vector2(260f, 350f), color);

            FusionSlotView slot = new FusionSlotView();
            slot.Frame = slotObject.GetComponent<Image>();
            slot.TitleLabel = CreateText("TitleLabel", slotObject.transform, title, 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(220f, 32f), TextAnchor.MiddleCenter, AccentGold);

            GameObject portraitPanel = CreatePanel("PortraitPanel", slotObject.transform, null,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -154f), new Vector2(166f, 166f), new Color(0.01f, 0.018f, 0.022f, 0.92f));

            GameObject portraitObject = CreateUiObject("Portrait", portraitPanel.transform);
            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(148f, 148f);
            slot.Portrait = portraitObject.AddComponent<Image>();
            slot.Portrait.preserveAspect = true;
            slot.Portrait.raycastTarget = false;

            slot.NameLabel = CreateText("NameLabel", slotObject.transform, "未選択", 20, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 83f), new Vector2(230f, 38f), TextAnchor.MiddleCenter, TextMain);

            slot.DetailLabel = CreateText("DetailLabel", slotObject.transform, "-", 16, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(230f, 54f), TextAnchor.MiddleCenter, TextSub);

            return slot;
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

            List<OwnedMonsterData> monsters = profile.OwnedMonsters
                .Where(monster => monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                .OrderBy(monster => ResolveMonsterData(masterDataManager, monster)?.classRank ?? 99)
                .ThenBy(monster => ResolveMonsterData(masterDataManager, monster)?.raceId ?? string.Empty)
                .ThenByDescending(monster => monster.AcquiredOrder)
                .ToList();

            if (rosterTitleLabel != null)
            {
                rosterTitleLabel.text = $"所持モンスター  {monsters.Count}/{Mathf.Max(1, profile.MonsterStorageLimit)}";
            }

            float contentHeight = Mathf.Max(0f, monsters.Count * (RosterRowHeight + RosterRowSpacing));
            rosterContent.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < monsters.Count; i += 1)
            {
                OwnedMonsterData monster = monsters[i];
                MonsterDataSO monsterData = ResolveMonsterData(masterDataManager, monster);
                GameObject row = CreateRosterRow(monster, monsterData, i);
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

            string classText = monsterData != null ? "C" + Mathf.Max(1, monsterData.classRank) : "C?";
            string raceText = monsterData != null ? ResolveRaceName(monsterData.raceId) : "不明";
            string favorite = monster.IsFavorite ? "  ★" : string.Empty;
            string selected = isParentA ? "  [親1]" : isParentB ? "  [親2]" : string.Empty;
            string displayName = monsterData != null ? monsterData.monsterName : monster.MonsterId;

            Image thumbnail = CreatePortraitImage(row.transform, "Thumbnail", GetPortraitResourcePath(monsterData),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, 0f), new Vector2(84f, 84f));

            if (thumbnail.sprite == null)
            {
                thumbnail.color = new Color(0.1f, 0.14f, 0.16f, 0.9f);
            }

            CreateText("Name", row.transform, $"{displayName}{favorite}{selected}", 21, FontStyle.Bold,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(114f, 19f), new Vector2(450f, 34f), TextAnchor.MiddleLeft, TextMain);

            CreateText("Sub", row.transform, $"Lv.{Mathf.Max(1, monster.Level)} / {raceText} / {classText}{BuildFusionBonusShort(monster)}", 17, FontStyle.Bold,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(114f, -20f), new Vector2(480f, 28f), TextAnchor.MiddleLeft, TextSub);

            CreateButton("ParentAButton", row.transform, "親1",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-198f, 0f), new Vector2(116f, 62f), SmallButtonSpritePath, isParentA ? ParentButtonSelectedColor : ParentButtonColor,
                () => SelectParent(monster.InstanceId, true));

            CreateButton("ParentBButton", row.transform, "親2",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-70f, 0f), new Vector2(116f, 62f), SmallButtonSpritePath, isParentB ? ParentButtonSelectedColor : ParentButtonColor,
                () => SelectParent(monster.InstanceId, false));

            return row;
        }

        private void SelectParent(string instanceId, bool asParentA)
        {
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

        private void SwapParents()
        {
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

            BindSlot(parentASlot, parentA, parentDataA, "親1", "未選択");
            BindSlot(parentBSlot, parentB, parentDataB, "親2", "未選択");

            MonsterFusionResult preview = MonsterFusionService.PreviewFusion(profile, parentAInstanceId, parentBInstanceId, masterDataManager);
            bool hasFavoriteParent = HasFavoriteParent(profile);
            fuseButton.interactable = preview.CanFuse && !hasFavoriteParent;

            if (preview.CanFuse)
            {
                BindResultSlot(preview.ResultMonsterData);
                SetStatus(hasFavoriteParent
                    ? "お気に入り登録中の親は配合できません。必要なら先にお気に入りを外してください。"
                    : preview.Message);
                return;
            }

            BindResultSlot(null);
            SetStatus(preview.Message);
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
                slot.DetailLabel.text = "親を選択";
                SetPortrait(slot.Portrait, null);
                return;
            }

            slot.NameLabel.text = monsterData.monsterName;
            slot.DetailLabel.text = $"Lv.{Mathf.Max(1, monster.Level)} / {ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)}\n{BuildFusionBonusLong(monster)}";
            SetPortrait(slot.Portrait, GetPortraitResourcePath(monsterData));
        }

        private void BindResultSlot(MonsterDataSO monsterData)
        {
            if (resultSlot == null)
            {
                return;
            }

            resultSlot.TitleLabel.text = "誕生";
            if (monsterData == null)
            {
                resultSlot.NameLabel.text = "結果未確定";
                resultSlot.DetailLabel.text = "組み合わせを選択";
                SetPortrait(resultSlot.Portrait, null);
                return;
            }

            resultSlot.NameLabel.text = monsterData.monsterName;
            resultSlot.DetailLabel.text = $"{ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)}\n親ステータス合計の5%を継承";
            SetPortrait(resultSlot.Portrait, GetPortraitResourcePath(monsterData));
        }

        private void FuseSelectedParents()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MonsterFusionResult result = MonsterFusionService.Fuse(profile, parentAInstanceId, parentBInstanceId, MasterDataManager.Instance);
            if (!result.CanFuse)
            {
                RefreshPreview();
                SetStatus(result.Message);
                return;
            }

            string successMessage = result.Message;
            SaveManager.Instance?.SaveCurrentGame();
            parentAInstanceId = string.Empty;
            parentBInstanceId = string.Empty;
            RefreshRoster();
            RefreshPreview();
            SetStatus(successMessage);
            FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        private bool HasFavoriteParent(PlayerProfile profile)
        {
            OwnedMonsterData parentA = profile?.GetOwnedMonster(parentAInstanceId);
            OwnedMonsterData parentB = profile?.GetOwnedMonster(parentBInstanceId);
            return parentA != null && parentA.IsFavorite || parentB != null && parentB.IsFavorite;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrEmpty(message) ? string.Empty : message;
            }
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
                _ => string.IsNullOrEmpty(raceId) ? "不明" : raceId
            };
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
            return totalBonus > 0 ? $" / 継承+{totalBonus}" : string.Empty;
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
            return totalBonus > 0 ? $"継承ボーナス +{totalBonus}" : "継承なし";
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
