using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Managers;

namespace WitchTower.Home
{
    [ExecuteAlways]
    public sealed class HomeSceneController : MonoBehaviour
    {
        private enum HomeTab
        {
            Home,
            Enhance,
            Equipment,
            Mission
        }

        [SerializeField] private PanelSwitcher panelSwitcher;
        [SerializeField] private HomePanelController homePanelController;
        [SerializeField] private EnhancePanelController enhancePanelController;
        [SerializeField] private EquipmentPanelController equipmentPanelController;
        [SerializeField] private MissionPanelController missionPanelController;
        [SerializeField] private MonsterDexPanelController monsterDexPanelController;
        [SerializeField] private DungeonSelectionPanelController dungeonSelectionPanelController;
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string fusionSceneName = "FusionScene";
        private static readonly string[] LegacyHomeObjectNames =
        {
            "ContentRoot",
            "NavBar",
            "HomeBackgroundShade",
            "HomeTopScrim",
            "HomeBottomScrim",
            "HomeTitleSigil",
            "ScreenTitle",
            "ScreenSubtitle"
        };

        private HomeTab currentTab = HomeTab.Home;
        private GameObject unifiedMenuRoot;
        private bool unifiedMenuRuntimeBound;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                return;
            }

            ApplyEditorPreview();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
                return;
            }

            EnsureRuntimeState();
            RefreshAllPanels();
            RefreshCurrentTab();
            HideLegacyHomeUi();
            BuildUnifiedMenu();
        }

        private void Update()
        {
            if (!Application.isPlaying || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (unifiedMenuRoot != null && unifiedMenuRoot.activeInHierarchy)
            {
                InvokeButtonUnderPointer(unifiedMenuRoot.transform, Input.mousePosition);
            }
        }

        private void ApplyEditorPreview()
        {
            NormalizeCanvasScales();
            HideLegacyHomeUi();
            BuildUnifiedMenu();
        }

        public void OpenHome()
        {
            currentTab = HomeTab.Home;
            RefreshCurrentTab();
        }

        public void OpenEnhance()
        {
            currentTab = HomeTab.Enhance;
            RefreshCurrentTab();
        }

        public void OpenEquipment()
        {
            currentTab = HomeTab.Equipment;
            RefreshCurrentTab();
        }

        public void OpenMission()
        {
            currentTab = HomeTab.Mission;
            RefreshCurrentTab();
        }

        public void StartBattle()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HideUnifiedMenu();
            DungeonSelectionPanelController panel = EnsureDungeonSelectionPanel();
            if (panel == null)
            {
                GameManager.Instance.SetCurrentFloor(Mathf.Max(1, GameManager.Instance.CurrentFloor));
                SceneManager.LoadScene(battleSceneName);
                return;
            }

            panel.Show(battleSceneName, () =>
            {
                if (unifiedMenuRoot != null)
                {
                    unifiedMenuRoot.SetActive(true);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                }
            });
        }

        public void Refresh()
        {
            currentTab = HomeTab.Home;
            RefreshCurrentTab();
        }

        public void RefreshAllPanels()
        {
            if (homePanelController != null)
            {
                homePanelController.Refresh();
            }

            if (enhancePanelController != null)
            {
                enhancePanelController.Refresh();
            }

            if (equipmentPanelController != null)
            {
                equipmentPanelController.Refresh();
            }

            if (missionPanelController != null)
            {
                missionPanelController.Refresh();
            }

            if (panelSwitcher != null)
            {
                var profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
                int baseUpgradeCost = enhancePanelController != null ? enhancePanelController.BaseUpgradeCost : 10;
                panelSwitcher.RefreshNavigation(profile, baseUpgradeCost);
            }
        }

        private void RefreshCurrentTab()
        {
            if (panelSwitcher == null)
            {
                return;
            }

            switch (currentTab)
            {
                case HomeTab.Home:
                    panelSwitcher.ShowHome();
                    if (homePanelController != null)
                    {
                        homePanelController.Refresh();
                    }
                    break;
                case HomeTab.Enhance:
                    panelSwitcher.ShowEnhance();
                    if (enhancePanelController != null)
                    {
                        enhancePanelController.Refresh();
                    }
                    break;
                case HomeTab.Equipment:
                    panelSwitcher.ShowEquipment();
                    if (equipmentPanelController != null)
                    {
                        equipmentPanelController.Refresh();
                    }
                    break;
                case HomeTab.Mission:
                    panelSwitcher.ShowMission();
                    if (missionPanelController != null)
                    {
                        missionPanelController.Refresh();
                    }
                    break;
            }
        }

        private static void EnsureRuntimeState()
        {
            Application.runInBackground = true;
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureUiPresentationCamera();
            EnsureUiInputPipeline();

            if (SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            if (MasterDataManager.Instance != null)
            {
                MasterDataManager.Instance.Initialize();
            }

            if (GameManager.Instance.PlayerProfile == null && SaveManager.Instance.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        private void BuildUnifiedMenu()
        {
            if (unifiedMenuRoot != null)
            {
                if (Application.isPlaying && !unifiedMenuRuntimeBound)
                {
                    Destroy(unifiedMenuRoot);
                    unifiedMenuRoot = null;
                }
                else
                {
                    unifiedMenuRoot.SetActive(true);
                    EnsureMonsterDexButton(unifiedMenuRoot.transform);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                    return;
                }
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return;
            }
            canvas.transform.localScale = Vector3.one;

            Transform existingMenu = canvas.transform.Find("UnifiedHomeMenu");
            if (existingMenu != null)
            {
                if (!Application.isPlaying)
                {
                    DestroyImmediate(existingMenu.gameObject);
                }
                else
                {
                    Destroy(existingMenu.gameObject);
                }
            }

            Sprite backgroundSprite = Resources.Load<Sprite>("UI/HomeMenu/HomeMenuBackground");
            Sprite panelSprite = Resources.Load<Sprite>("UI/HomeMenu/HomeMenuPanel");
            if (backgroundSprite == null)
            {
                return;
            }

            unifiedMenuRoot = CreateUiRoot("UnifiedHomeMenu", canvas.transform);
            unifiedMenuRuntimeBound = Application.isPlaying;
            unifiedMenuRoot.transform.SetAsLastSibling();
            CreateMenuImage("UnifiedHomeBackground", unifiedMenuRoot.transform, backgroundSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2560f, 1920f), true);

            Sprite battleSprite = Resources.Load<Sprite>("UI/HomeMenu/BattleButton");
            Sprite formationSprite = Resources.Load<Sprite>("UI/HomeMenu/FormationButton");
            Sprite equipmentSprite = Resources.Load<Sprite>("UI/HomeMenu/EquipmentButton");
            Sprite fusionSprite = Resources.Load<Sprite>("UI/HomeMenu/FusionButton");
            if (battleSprite != null && formationSprite != null && equipmentSprite != null && fusionSprite != null)
            {
                CreateSpriteButton("BattleButton", unifiedMenuRoot.transform, battleSprite, new Vector2(-272f, 520f), new Vector2(500f, 330f), StartBattle);
                CreateSpriteButton("FormationButton", unifiedMenuRoot.transform, formationSprite, new Vector2(272f, 520f), new Vector2(500f, 330f), OpenFormationMenu);
                CreateSpriteButton("EquipmentButton", unifiedMenuRoot.transform, equipmentSprite, new Vector2(-272f, 185f), new Vector2(500f, 330f), OpenEquipmentMenu);
                CreateSpriteButton("FusionButton", unifiedMenuRoot.transform, fusionSprite, new Vector2(272f, 185f), new Vector2(500f, 330f), OpenFusionMenu);
                EnsureMonsterDexButton(unifiedMenuRoot.transform);
                return;
            }

            if (panelSprite == null)
            {
                return;
            }

            Image panelImage = CreateMenuImage("UnifiedHomePanel", unifiedMenuRoot.transform, panelSprite, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1080f, 430f), true);
            RectTransform panelRect = panelImage.rectTransform;
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 16f);

            CreateTransparentButton("BattleButton", panelImage.transform, new Vector2(-272f, 104f), new Vector2(505f, 174f), StartBattle);
            CreateTransparentButton("FormationButton", panelImage.transform, new Vector2(272f, 104f), new Vector2(486f, 174f), OpenFormationMenu);
            CreateTransparentButton("EquipmentButton", panelImage.transform, new Vector2(-272f, -91f), new Vector2(508f, 160f), OpenEquipmentMenu);
            CreateTransparentButton("FusionButton", panelImage.transform, new Vector2(272f, -91f), new Vector2(486f, 160f), OpenFusionMenu);
            EnsureMonsterDexButton(unifiedMenuRoot.transform);
        }

        private void OpenFormationMenu()
        {
            OpenHome();
            HideUnifiedMenu();
        }

        private void OpenEquipmentMenu()
        {
            OpenEquipment();
            HideUnifiedMenu();
        }

        public void OpenFusionMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.LoadScene(fusionSceneName);
        }

        private void OpenMonsterDexMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HideUnifiedMenu();
            MonsterDexPanelController dexPanel = EnsureMonsterDexPanel();
            if (dexPanel == null)
            {
                BuildUnifiedMenu();
                return;
            }

            dexPanel.Show(() =>
            {
                if (unifiedMenuRoot != null)
                {
                    unifiedMenuRoot.SetActive(true);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                }
            });
        }

        private void HideUnifiedMenu()
        {
            if (unifiedMenuRoot != null)
            {
                unifiedMenuRoot.SetActive(false);
            }
        }

        private MonsterDexPanelController EnsureMonsterDexPanel()
        {
            if (monsterDexPanelController != null)
            {
                return monsterDexPanelController;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return null;
            }

            Transform existingPanel = canvas.transform.Find("MonsterDexPanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : CreateUiRoot("MonsterDexPanel", canvas.transform);

            monsterDexPanelController = panelObject.GetComponent<MonsterDexPanelController>();
            if (monsterDexPanelController == null)
            {
                monsterDexPanelController = panelObject.AddComponent<MonsterDexPanelController>();
            }

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            return monsterDexPanelController;
        }

        private DungeonSelectionPanelController EnsureDungeonSelectionPanel()
        {
            if (dungeonSelectionPanelController != null)
            {
                return dungeonSelectionPanelController;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return null;
            }

            Transform existingPanel = canvas.transform.Find("DungeonSelectionPanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : CreateUiRoot("DungeonSelectionPanel", canvas.transform);

            dungeonSelectionPanelController = panelObject.GetComponent<DungeonSelectionPanelController>();
            if (dungeonSelectionPanelController == null)
            {
                dungeonSelectionPanelController = panelObject.AddComponent<DungeonSelectionPanelController>();
            }

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            return dungeonSelectionPanelController;
        }

        private void EnsureMonsterDexButton(Transform menuRoot)
        {
            if (menuRoot == null || menuRoot.Find("MonsterDexButton") != null)
            {
                return;
            }

            Sprite buttonSprite = Resources.Load<Sprite>("UI/FusionPage/FusionSmallButton");
            CreateTextSpriteButton("MonsterDexButton", menuRoot, buttonSprite, "図鑑", new Vector2(0f, 850f), new Vector2(430f, 136f), OpenMonsterDexMenu);
        }

        private static void NormalizeCanvasScales()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null)
                {
                    canvas.transform.localScale = Vector3.one;
                }
            }
        }

        private static void EnsureUiInputPipeline()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>(true);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystemObject.SetActive(true);
            }
            else
            {
                eventSystem.gameObject.SetActive(true);
                if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                }
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }
        }

        private static void HideLegacyHomeUi()
        {
            foreach (string objectName in LegacyHomeObjectNames)
            {
                GameObject target = GameObject.Find(objectName);
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        private static GameObject CreateUiRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return root;
        }

        private static Image CreateMenuImage(string name, Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, bool preserveAspect)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateSpriteButton(string name, Transform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            GameObject visualRoot = new GameObject($"{name}Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visualRoot.transform.SetParent(root.transform, false);
            RectTransform visualRect = visualRoot.GetComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.anchoredPosition = Vector2.zero;

            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float scale = Mathf.Min(size.x / spriteWidth, size.y / spriteHeight);
            visualRect.sizeDelta = new Vector2(spriteWidth * scale, spriteHeight * scale);

            Image visual = visualRoot.GetComponent<Image>();
            visual.sprite = sprite;
            visual.color = Color.white;
            visual.preserveAspect = true;
            visual.raycastTarget = false;
        }

        private static void CreateTextSpriteButton(string name, Transform parent, Sprite sprite, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image targetImage = root.GetComponent<Image>();
            targetImage.color = sprite != null ? new Color(1f, 1f, 1f, 0.001f) : new Color(0.04f, 0.11f, 0.13f, 0.96f);
            targetImage.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = targetImage;
            button.onClick.AddListener(action);

            if (sprite != null)
            {
                GameObject visualRoot = new GameObject($"{name}Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                visualRoot.transform.SetParent(root.transform, false);
                RectTransform visualRect = visualRoot.GetComponent<RectTransform>();
                visualRect.anchorMin = new Vector2(0.5f, 0.5f);
                visualRect.anchorMax = new Vector2(0.5f, 0.5f);
                visualRect.pivot = new Vector2(0.5f, 0.5f);
                visualRect.anchoredPosition = Vector2.zero;

                float spriteWidth = Mathf.Max(1f, sprite.rect.width);
                float spriteHeight = Mathf.Max(1f, sprite.rect.height);
                float scale = Mathf.Min(size.x / spriteWidth, size.y / spriteHeight);
                visualRect.sizeDelta = new Vector2(spriteWidth * scale, spriteHeight * scale);

                Image visual = visualRoot.GetComponent<Image>();
                visual.sprite = sprite;
                visual.color = Color.white;
                visual.preserveAspect = true;
                visual.raycastTarget = false;
            }

            GameObject labelRoot = new GameObject($"{name}Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelRoot.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelRoot.GetComponent<Text>();
            text.font = GetRuntimeFont();
            text.text = label;
            text.fontSize = 38;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private static void CreateTransparentButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
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
