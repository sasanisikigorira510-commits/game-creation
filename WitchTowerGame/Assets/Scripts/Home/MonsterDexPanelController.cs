using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Home
{
    public sealed class MonsterDexPanelController : MonoBehaviour
    {
        private const int ColumnCount = 3;
        private const float CardWidth = 276f;
        private const float CardHeight = 360f;
        private const float CardGapX = 22f;
        private const float CardGapY = 24f;
        private const string BackgroundSpritePath = "UI/FusionPage/FusionBackground";
        private const string MainFrameSpritePath = "UI/FusionPage/FusionMainFrame";
        private const string RosterFrameSpritePath = "UI/FusionPage/FusionRosterFrame";
        private const string SmallButtonSpritePath = "UI/FusionPage/FusionSmallButton";
        private const string CardFrameBasePath = "MonsterCardFrames/monster_class_";

        private static readonly Color PageTint = new Color(0.005f, 0.012f, 0.018f, 0.97f);
        private static readonly Color PanelColor = new Color(0.025f, 0.045f, 0.055f, 0.98f);
        private static readonly Color CardFallbackColor = new Color(0.032f, 0.052f, 0.064f, 0.98f);
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
        private Image selectedPortrait;
        private Text selectedNameLabel;
        private Text selectedInfoLabel;
        private Text selectedStatsLabel;
        private Text selectedDescriptionLabel;
        private Text counterLabel;
        private string selectedMonsterId;
        private bool isBuilt;

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            Refresh();
        }

        private void Hide()
        {
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
            List<MonsterDataSO> monsters = SortMonsters(allMonsterData);

            if (string.IsNullOrEmpty(selectedMonsterId) || monsters.All(monster => monster.monsterId != selectedMonsterId))
            {
                selectedMonsterId = monsters.Count > 0 ? monsters[0].monsterId : string.Empty;
            }

            RebuildCards(monsters);
            MonsterDataSO selectedMonster = monsters.FirstOrDefault(monster => monster.monsterId == selectedMonsterId);
            int selectedIndex = selectedMonster != null ? monsters.IndexOf(selectedMonster) + 1 : 0;
            BindDetail(selectedMonster, selectedIndex);
            UpdateCounter(monsters);
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
                new Vector2(0f, -48f), new Vector2(520f, 62f), TextAnchor.MiddleCenter, AccentGold);

            CreateText("SortHint", panel.transform, "表示順: クラス昇順 / 種族順 / 図鑑番号", 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -111f), new Vector2(720f, 36f), TextAnchor.MiddleCenter, TextSub);

            CreateButton("CloseButton", panel.transform, "戻る",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-38f, -44f), new Vector2(150f, 66f), SmallButtonSpritePath, new Color(0.34f, 0.2f, 0.16f, 1f), Hide);

            BuildDetailPanel(panel.transform);
            BuildCardGrid(panel.transform);
            isBuilt = true;
        }

        private void BuildDetailPanel(Transform parent)
        {
            GameObject detailPanel = CreatePanel("DexDetailPanel", parent, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -330f), new Vector2(920f, 360f), DetailColor);

            selectedFrame = CreatePanel("SelectedFrame", detailPanel.transform, null,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(150f, 0f), new Vector2(250f, 310f), CardFallbackColor).GetComponent<Image>();

            selectedPortrait = CreateImage("SelectedPortrait", selectedFrame.transform, string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 22f), new Vector2(188f, 188f));

            selectedNameLabel = CreateText("SelectedName", detailPanel.transform, "-", 30, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(160f, -38f), new Vector2(600f, 44f), TextAnchor.MiddleLeft, TextMain);

            selectedInfoLabel = CreateText("SelectedInfo", detailPanel.transform, "-", 21, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(160f, -86f), new Vector2(600f, 40f), TextAnchor.MiddleLeft, AccentCyan);

            selectedStatsLabel = CreateText("SelectedStats", detailPanel.transform, "-", 17, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(160f, -164f), new Vector2(600f, 96f), TextAnchor.UpperLeft, TextSub);

            selectedDescriptionLabel = CreateText("SelectedDescription", detailPanel.transform, "-", 18, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(160f, 40f), new Vector2(600f, 74f), TextAnchor.UpperLeft, TextMain);
        }

        private void BuildCardGrid(Transform parent)
        {
            GameObject gridPanel = CreatePanel("DexGridPanel", parent, RosterFrameSpritePath,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1112f), new Vector2(920f, 1040f), new Color(0.016f, 0.033f, 0.04f, 0.98f));

            counterLabel = CreateText("Counter", gridPanel.transform, "", 22, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(620f, 40f), TextAnchor.MiddleCenter, TextMain);

            GameObject viewport = CreatePanel("Viewport", gridPanel.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(860f, 918f), new Color(0f, 0f, 0f, 0.18f));
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
            float contentHeight = Mathf.Max(0f, rowCount * (CardHeight + CardGapY));
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
                cardImage.color = isSelected ? new Color(1f, 0.96f, 0.74f, 1f) : Color.white;
            }

            Button button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.onClick.AddListener(() => SelectMonster(monsterData));

            CreateText("Number", card.transform, BuildNumberText(monsterData, displayIndex), 17, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(210f, 28f), TextAnchor.MiddleCenter, AccentGold);

            Image portrait = CreateImage("Portrait", card.transform, GetPortraitResourcePath(monsterData),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -151f), new Vector2(174f, 174f));
            portrait.color = Color.white;

            CreateText("Name", card.transform, monsterData != null ? monsterData.monsterName : "不明", 19, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 88f), new Vector2(220f, 38f), TextAnchor.MiddleCenter, TextMain);

            CreateText("ClassRace", card.transform, monsterData != null ? $"{ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)}" : "-",
                15, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 53f), new Vector2(220f, 28f), TextAnchor.MiddleCenter, TextSub);

            CreateText("OwnedState", card.transform, BuildOwnedText(monsterData), 15, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 25f), new Vector2(220f, 28f), TextAnchor.MiddleCenter, isSelected ? AccentGold : AccentCyan);

            if (isSelected)
            {
                CreateText("SelectedBadge", card.transform, "選択中", 15, FontStyle.Bold,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -317f), new Vector2(170f, 24f), TextAnchor.MiddleCenter, AccentGold);
            }

            return card;
        }

        private void SelectMonster(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return;
            }

            selectedMonsterId = monsterData.monsterId;
            Refresh();
        }

        private void BindDetail(MonsterDataSO monsterData, int fallbackIndex)
        {
            if (monsterData == null)
            {
                SetPortrait(selectedPortrait, null);
                if (selectedNameLabel != null) selectedNameLabel.text = "モンスター未登録";
                if (selectedInfoLabel != null) selectedInfoLabel.text = "-";
                if (selectedStatsLabel != null) selectedStatsLabel.text = "-";
                if (selectedDescriptionLabel != null) selectedDescriptionLabel.text = "マスターデータを読み込めませんでした。";
                return;
            }

            if (selectedFrame != null)
            {
                selectedFrame.sprite = LoadSprite(ResolveCardFramePath(monsterData));
                selectedFrame.color = selectedFrame.sprite != null ? Color.white : CardFallbackColor;
            }

            SetPortrait(selectedPortrait, GetPortraitResourcePath(monsterData));
            if (selectedNameLabel != null)
            {
                selectedNameLabel.text = monsterData.monsterName;
            }

            if (selectedInfoLabel != null)
            {
                selectedInfoLabel.text = $"{BuildNumberText(monsterData, fallbackIndex)} / {ResolveRaceName(monsterData.raceId)} / C{Mathf.Max(1, monsterData.classRank)} / 最大Lv.{MonsterLevelService.GetMaxLevel(monsterData)} / {ResolveElementName(monsterData.element)} / {ResolveRangeName(monsterData.rangeType)}";
            }

            if (selectedStatsLabel != null)
            {
                MonsterBaseStats stats = monsterData.baseStats;
                MonsterLevelGrowthCoefficients growth = monsterData.levelGrowth;
                selectedStatsLabel.text =
                    $"HP {stats.maxHp}    攻撃 {stats.attack}    魔力 {stats.magicAttack}\n" +
                    $"防御 {stats.defense}    魔防 {stats.magicDefense}    攻速 {stats.attackSpeed:0.##}\n" +
                    $"攻撃範囲 {monsterData.attackRange:0.##}    対象数 {Mathf.Max(1, monsterData.normalAttackTargetCount)}    {ResolveDamageName(monsterData.damageType)}\n" +
                    $"成長 HPx{ResolveGrowthCoefficient(growth.maxHpCoefficient):0.##} 攻x{ResolveGrowthCoefficient(growth.attackCoefficient):0.##} 魔x{ResolveGrowthCoefficient(growth.magicAttackCoefficient):0.##} 防x{ResolveGrowthCoefficient(growth.defenseCoefficient):0.##} 魔防x{ResolveGrowthCoefficient(growth.magicDefenseCoefficient):0.##}";
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
                counterLabel.text = $"登録モンスター {monsters.Count}体 / 所持済み {ownedKinds}種";
            }
        }

        private static List<MonsterDataSO> SortMonsters(MonsterDataSO[] monsters)
        {
            if (monsters == null)
            {
                return new List<MonsterDataSO>();
            }

            return monsters
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
            return damageType == MonsterDamageType.Magic ? "魔法攻撃" : "物理攻撃";
        }

        private static float ResolveGrowthCoefficient(float coefficient)
        {
            return coefficient > 0f ? coefficient : 1f;
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
            return ownedCount > 0 ? $"所持 {ownedCount}体" : "未所持";
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
