using UnityEngine;
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
            GameManager.Instance.SetCurrentFloor(Mathf.Max(1, GameManager.Instance.CurrentFloor));
            SceneManager.LoadScene(battleSceneName);
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
                unifiedMenuRoot.SetActive(true);
                unifiedMenuRoot.transform.SetAsLastSibling();
                return;
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
                    unifiedMenuRoot = existingMenu.gameObject;
                    unifiedMenuRoot.SetActive(true);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                    return;
                }
            }

            Sprite backgroundSprite = Resources.Load<Sprite>("UI/HomeMenu/HomeMenuBackground");
            Sprite panelSprite = Resources.Load<Sprite>("UI/HomeMenu/HomeMenuPanel");
            if (backgroundSprite == null)
            {
                return;
            }

            unifiedMenuRoot = CreateUiRoot("UnifiedHomeMenu", canvas.transform);
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

        private void HideUnifiedMenu()
        {
            if (unifiedMenuRoot != null)
            {
                unifiedMenuRoot.SetActive(false);
            }
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
    }
}
