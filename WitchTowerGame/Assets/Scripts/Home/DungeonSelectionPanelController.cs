using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Home
{
    public sealed class DungeonSelectionPanelController : MonoBehaviour
    {
        private const string BackgroundPath = "UI/DungeonSelect/DungeonSelectBackground";
        private const string FloorNodeUnlockedPath = "UI/DungeonSelect/FloorNodeUnlocked";
        private const string FloorNodeSelectedPath = "UI/DungeonSelect/FloorNodeSelected";
        private const string FloorNodeLockedPath = "UI/DungeonSelect/FloorNodeLocked";

        private readonly List<Image> dungeonCardFrames = new List<Image>();
        private readonly List<Image> floorNodeImages = new List<Image>();
        private readonly List<Text> floorNodeLabels = new List<Text>();

        private GameObject panelRoot;
        private RectTransform dungeonListRoot;
        private RectTransform floorListRoot;
        private Text titleText;
        private Text dungeonDescriptionText;
        private Text floorDescriptionText;
        private Text enemyPreviewText;
        private Action closeCallback;
        private string battleSceneName = "BattleScene";
        private string selectedDungeonId;
        private int selectedLocalFloor = 1;

        private void Update()
        {
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
            EnsurePanel();
            if (panelRoot == null)
            {
                return;
            }

            SelectDungeon(GameManager.Instance != null ? GameManager.Instance.CurrentDungeonId : BattleDungeonCatalog.Dungeons[0].DungeonId);
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        private void EnsurePanel()
        {
            if (panelRoot != null)
            {
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

            Button closeButton = CreateTextButton("CloseButton", panel.transform, "戻る",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-26f, -32f), new Vector2(130f, 52f), new Color(0.36f, 0.18f, 0.16f, 0.96f), Close, 18);
            closeButton.targetGraphic.raycastTarget = true;

            dungeonListRoot = CreateUiObject("DungeonList", panel.transform).GetComponent<RectTransform>();
            dungeonListRoot.anchorMin = new Vector2(0.5f, 1f);
            dungeonListRoot.anchorMax = new Vector2(0.5f, 1f);
            dungeonListRoot.pivot = new Vector2(0.5f, 1f);
            dungeonListRoot.anchoredPosition = new Vector2(0f, -164f);
            dungeonListRoot.sizeDelta = new Vector2(820f, 810f);

            dungeonDescriptionText = CreateText("DungeonDescription", panel.transform, string.Empty, 21, FontStyle.Bold,
                TextAnchor.UpperCenter, new Color(0.92f, 0.87f, 0.72f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -986f), new Vector2(820f, 82f));

            floorListRoot = CreateUiObject("FloorList", panel.transform).GetComponent<RectTransform>();
            floorListRoot.anchorMin = new Vector2(0.5f, 1f);
            floorListRoot.anchorMax = new Vector2(0.5f, 1f);
            floorListRoot.pivot = new Vector2(0.5f, 1f);
            floorListRoot.anchoredPosition = new Vector2(0f, -1086f);
            floorListRoot.sizeDelta = new Vector2(820f, 210f);

            floorDescriptionText = CreateText("FloorDescription", panel.transform, string.Empty, 20, FontStyle.Bold,
                TextAnchor.UpperCenter, new Color(0.78f, 0.92f, 0.98f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -1300f), new Vector2(820f, 54f));

            enemyPreviewText = CreateText("EnemyPreview", panel.transform, string.Empty, 22, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -1370f), new Vector2(820f, 44f));

            CreateTextButton("StartBattleButton", panel.transform, "この階層へ挑む",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 66f), new Vector2(560f, 92f), new Color(0.48f, 0.18f, 0.08f, 0.98f), StartSelectedBattle, 27);

            BuildDungeonCards();
            BuildFloorNodes();
            panelRoot.SetActive(false);
        }

        private void BuildDungeonCards()
        {
            ClearChildren(dungeonListRoot);
            dungeonCardFrames.Clear();
            IReadOnlyList<BattleDungeonDefinition> dungeons = BattleDungeonCatalog.Dungeons;
            for (int i = 0; i < dungeons.Count; i += 1)
            {
                BattleDungeonDefinition dungeon = dungeons[i];
                GameObject card = CreateUiObject("DungeonCard_" + dungeon.DungeonId, dungeonListRoot);
                RectTransform cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.5f, 1f);
                cardRect.anchorMax = new Vector2(0.5f, 1f);
                cardRect.pivot = new Vector2(0.5f, 1f);
                cardRect.anchoredPosition = new Vector2(0f, -i * 258f);
                cardRect.sizeDelta = new Vector2(790f, 232f);

                Image hitArea = card.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                Button button = card.AddComponent<Button>();
                button.targetGraphic = hitArea;
                string capturedDungeonId = dungeon.DungeonId;
                button.onClick.AddListener(() => SelectDungeon(capturedDungeonId));

                Image frame = CreateImage("Frame", card.transform, null,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
                frame.color = new Color(0.03f, 0.045f, 0.07f, 0.78f);
                frame.raycastTarget = false;
                dungeonCardFrames.Add(frame);

                Image art = CreateImage("Art", card.transform, LoadSprite(dungeon.CardResourcePath),
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, -18f), true);
                art.raycastTarget = false;

                CreateText("DungeonName", card.transform, dungeon.DungeonName, 34, FontStyle.Bold,
                    TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), new Vector2(42f, -30f), new Vector2(-84f, 42f));

                CreateText("DungeonMeta", card.transform, $"全{dungeon.Floors.Count}階層 / クラス1敵のみ", 18, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.54f), new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 0f), new Vector2(42f, 28f), new Vector2(-84f, 32f));
            }
        }

        private void BuildFloorNodes()
        {
            ClearChildren(floorListRoot);
            floorNodeImages.Clear();
            floorNodeLabels.Clear();
            for (int i = 0; i < 5; i += 1)
            {
                GameObject node = CreateUiObject("FloorNode_" + (i + 1), floorListRoot);
                RectTransform rect = node.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(84f + i * 164f, 0f);
                rect.sizeDelta = new Vector2(142f, 142f);

                Image hitArea = node.AddComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                Button button = node.AddComponent<Button>();
                button.targetGraphic = hitArea;
                int localFloor = i + 1;
                button.onClick.AddListener(() => SelectFloor(localFloor));

                Image visual = CreateImage("Visual", node.transform, LoadSprite(FloorNodeUnlockedPath),
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
                visual.raycastTarget = false;
                floorNodeImages.Add(visual);

                Text label = CreateText("Label", node.transform, (i + 1).ToString(), 34, FontStyle.Bold,
                    TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                floorNodeLabels.Add(label);
            }
        }

        private void SelectDungeon(string dungeonId)
        {
            selectedDungeonId = BattleDungeonCatalog.GetDungeon(dungeonId).DungeonId;
            selectedLocalFloor = Mathf.Clamp(
                GameManager.Instance != null && GameManager.Instance.CurrentDungeonId == selectedDungeonId
                    ? GameManager.Instance.CurrentDungeonFloor
                    : 1,
                1,
                BattleDungeonCatalog.GetDungeon(selectedDungeonId).Floors.Count);
            RefreshSelection();
        }

        private void SelectFloor(int localFloor)
        {
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
                bool selected = i < BattleDungeonCatalog.Dungeons.Count &&
                    BattleDungeonCatalog.Dungeons[i].DungeonId == dungeon.DungeonId;
                dungeonCardFrames[i].color = selected
                    ? new Color(0.95f, 0.76f, 0.38f, 0.38f)
                    : new Color(0.03f, 0.045f, 0.07f, 0.78f);
            }

            if (dungeonDescriptionText != null)
            {
                dungeonDescriptionText.text = dungeon.Description;
            }

            for (int i = 0; i < floorNodeImages.Count; i += 1)
            {
                bool selected = i + 1 == selectedLocalFloor;
                floorNodeImages[i].sprite = LoadSprite(selected ? FloorNodeSelectedPath : FloorNodeUnlockedPath);
                floorNodeLabels[i].color = selected ? new Color(0.08f, 0.12f, 0.09f) : Color.white;
            }

            BattleDungeonFloorDefinition floor = BattleDungeonCatalog.GetFloor(dungeon.DungeonId, selectedLocalFloor);
            int globalFloor = BattleDungeonCatalog.ResolveGlobalFloor(dungeon.DungeonId, selectedLocalFloor);
            MonsterDataSO enemyMonster = MasterDataManager.Instance != null && floor != null
                ? MasterDataManager.Instance.GetMonsterData(floor.EnemyMonsterId)
                : null;

            if (floorDescriptionText != null)
            {
                floorDescriptionText.text = floor != null
                    ? $"第{selectedLocalFloor}階層: {floor.FloorName} / 内部階層 {globalFloor}"
                    : $"第{selectedLocalFloor}階層";
            }

            if (enemyPreviewText != null)
            {
                int enemyCount = floor != null ? Mathf.Max(1, floor.EnemyCount) : 0;
                enemyPreviewText.text = enemyMonster != null
                    ? $"出現敵: {enemyMonster.monsterName}  クラス{Mathf.Max(1, enemyMonster.classRank)}  敵数{enemyCount}"
                    : "出現敵: 未設定";
            }
        }

        private void StartSelectedBattle()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            MasterDataManager.Instance?.Initialize();
            GameManager.Instance?.SetCurrentDungeonFloor(selectedDungeonId, selectedLocalFloor);
            SceneManager.LoadScene(battleSceneName);
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
