using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Home
{
    [ExecuteAlways]
    public sealed class HomeSceneController : MonoBehaviour
    {
        private sealed class DailyQuestCardView
        {
            public string QuestId;
            public Text ProgressText;
            public Button ClaimButton;
            public Text ClaimButtonText;
            public Graphic ClaimStoneGraphic;
        }

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
        [SerializeField] private GoldShopPanelController goldShopPanelController;
        [SerializeField] private DungeonSelectionPanelController dungeonSelectionPanelController;
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string formationSceneName = "FormationScene";
        [SerializeField] private string equipmentSceneName = "EquipmentScene";
        [SerializeField] private string fusionSceneName = "FusionScene";
        [SerializeField] private string gachaSceneName = "GachaScene";
        private const string FreeStoneIconPath = "UI/GachaPage/GachaStoneFreeIcon";
        private const string PaidStoneIconPath = "UI/GachaPage/GachaStonePaidIcon";
        private const string QuestButtonSpritePath = "UI/HomeMenu/QuestButtonRound";
        private const string HomeTopHudFramePath = "UI/HomeRedesign/HomeTopHudFrame";
        private const string HomeBottomNavBarPath = "UI/HomeRedesign/HomeBottomNavBar";
        private const string HomeFallbackHeroSpritePath = "FamilyMonsterCards/Dragon/dragon_whelp";
        private const float HomeAdReservedHeight = 170f;
        private static readonly Vector2 HomeMenuButtonSize = new Vector2(480f, 250f);
        private static readonly Vector2 HomeMainActionButtonSize = new Vector2(470f, 220f);
        private static readonly Vector2 HomeBottomNavButtonSize = new Vector2(196f, 152f);
        private static readonly Vector2 HomeMenuLeftTopPosition = new Vector2(-260f, 715f);
        private static readonly Vector2 HomeMenuRightTopPosition = new Vector2(260f, 715f);
        private static readonly Vector2 HomeMenuLeftMiddlePosition = new Vector2(-260f, 445f);
        private static readonly Vector2 HomeMenuRightMiddlePosition = new Vector2(260f, 445f);
        private static readonly Vector2 HomeMenuLeftBottomPosition = new Vector2(-260f, 175f);
        private static readonly Vector2 HomeMenuRightBottomPosition = new Vector2(260f, 175f);
        private static readonly Vector2 MonsterDexButtonPosition = HomeMenuRightBottomPosition;
        private static readonly Vector2 MonsterDexButtonSize = HomeMenuButtonSize;
        private static readonly Vector2 HomeStoneBarSize = new Vector2(1040f, 136f);
        private static readonly Vector2 HomeShopButtonPosition = new Vector2(-438f, -210f);
        private static readonly Vector2 HomeShopButtonSize = new Vector2(154f, 136f);
        private static readonly Vector2 HomeQuestButtonPosition = new Vector2(456f, -210f);
        private static readonly Vector2 HomeQuestButtonSize = new Vector2(118f, 118f);
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
        private Text homeFreeStoneText;
        private Text homePaidStoneText;
        private Text homeGoldText;
        private Text homeExpText;
        private Text homePlayerLevelText;
        private Text homeGuideText;
        private Text homeNextFloorText;
        private Text homeHeroNameText;
        private Text homeHeroLevelText;
        private Image homeHeroImage;
        private Text homeQuestButtonText;
        private Button homeQuestButton;
        private Text homeShopButtonText;
        private Button homeShopButton;
        private GameObject dailyQuestListRoot;
        private Text dailyQuestStatusText;
        private readonly List<DailyQuestCardView> dailyQuestCards = new List<DailyQuestCardView>();

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
            if (!Application.isPlaying)
            {
                return;
            }

            if (unifiedMenuRoot != null && unifiedMenuRoot.activeInHierarchy)
            {
                if (homeQuestButton == null || homeShopButton == null)
                {
                    EnsureHomeStoneBalanceBar(unifiedMenuRoot.transform);
                }

                if (dailyQuestListRoot == null || dailyQuestCards.Count != DailyRewardService.GetDefinitions().Count)
                {
                    EnsureDailyQuestList(unifiedMenuRoot.transform);
                }

                RefreshHomeStoneBalanceBar();
                RefreshDailyQuestList();
            }

            if (!Input.GetMouseButtonDown(0))
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
            RebuildUnifiedMenu();
        }

        private void RebuildUnifiedMenu()
        {
            if (unifiedMenuRoot != null)
            {
                DestroySceneObject(unifiedMenuRoot);
                unifiedMenuRoot = null;
                dailyQuestListRoot = null;
                dailyQuestCards.Clear();
            }

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

            RefreshHomeStoneBalanceBar();
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
                    DestroySceneObject(unifiedMenuRoot);
                    unifiedMenuRoot = null;
                }
                else
                {
                    unifiedMenuRoot.SetActive(true);
                    EnsureHomeStoneBalanceBar(unifiedMenuRoot.transform);
                    RefreshHomeStoneBalanceBar();
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
                DestroySceneObject(existingMenu.gameObject);
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
            CreateMenuImage("UnifiedHomeBackground", unifiedMenuRoot.transform, backgroundSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1920f), false);
            CreateHomeAtmosphere(unifiedMenuRoot.transform);
            CreateAdReservedSpace(unifiedMenuRoot.transform);
            CreateHomeHeroShowcase(unifiedMenuRoot.transform);
            CreateHomeGuidePanel(unifiedMenuRoot.transform);

            Sprite battleSprite = Resources.Load<Sprite>("UI/HomeMenu/BattleButton");
            Sprite formationSprite = Resources.Load<Sprite>("UI/HomeMenu/FormationButton");
            Sprite equipmentSprite = Resources.Load<Sprite>("UI/HomeMenu/EquipmentButton");
            Sprite fusionSprite = Resources.Load<Sprite>("UI/HomeMenu/FusionButton");
            Sprite gachaSprite = Resources.Load<Sprite>("UI/HomeMenu/GachaButton");
            Sprite dexSprite = Resources.Load<Sprite>("UI/HomeMenu/DexButton");

            CreateHomeSpriteButton("BattleButton", unifiedMenuRoot.transform, battleSprite, "冒険開始", new Vector2(-250f, HomeAdReservedHeight + 324f), HomeMainActionButtonSize, StartBattle, 36);
            CreateHomeSpriteButton("FormationButton", unifiedMenuRoot.transform, formationSprite, "編成", new Vector2(250f, HomeAdReservedHeight + 324f), HomeMainActionButtonSize, OpenFormationMenu, 36);
            CreateHomeBottomNavigation(unifiedMenuRoot.transform, panelSprite, gachaSprite, dexSprite, equipmentSprite, fusionSprite);
            EnsureHomeStoneBalanceBar(unifiedMenuRoot.transform);
            RefreshHomeStoneBalanceBar();
        }

        public void OpenFormationMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.LoadScene(formationSceneName);
        }

        public void OpenEquipmentMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.LoadScene(equipmentSceneName);
        }

        public void OpenFusionMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.LoadScene(fusionSceneName);
        }

        public void OpenGachaMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.LoadScene(gachaSceneName);
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

        public void OpenGoldShopMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HideUnifiedMenu();
            GoldShopPanelController shopPanel = EnsureGoldShopPanel();
            if (shopPanel == null)
            {
                BuildUnifiedMenu();
                return;
            }

            shopPanel.Show(() =>
            {
                if (unifiedMenuRoot != null)
                {
                    unifiedMenuRoot.SetActive(true);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                    RefreshHomeStoneBalanceBar();
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

        private void OpenDailyQuestList()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureDailyQuestList(unifiedMenuRoot != null ? unifiedMenuRoot.transform : null);
            if (dailyQuestListRoot != null)
            {
                dailyQuestListRoot.SetActive(true);
                dailyQuestListRoot.transform.SetAsLastSibling();
                RefreshDailyQuestList();
            }
        }

        private void CloseDailyQuestList()
        {
            if (dailyQuestListRoot != null)
            {
                dailyQuestListRoot.SetActive(false);
            }
        }

        private void ClaimDailyQuestReward(string questId)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerProfile profile = GetRuntimeProfile();
            int claimedStones = DailyRewardService.Claim(profile, DateTime.Now, questId);
            if (claimedStones > 0)
            {
                SaveManager.Instance?.SaveCurrentGame();
            }

            RefreshAllPanels();
            RefreshDailyQuestList();
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

        private GoldShopPanelController EnsureGoldShopPanel()
        {
            if (goldShopPanelController != null)
            {
                return goldShopPanelController;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return null;
            }

            Transform existingPanel = canvas.transform.Find("GoldShopPanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : CreateUiRoot("GoldShopPanel", canvas.transform);

            goldShopPanelController = panelObject.GetComponent<GoldShopPanelController>();
            if (goldShopPanelController == null)
            {
                goldShopPanelController = panelObject.AddComponent<GoldShopPanelController>();
            }

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            return goldShopPanelController;
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
            if (menuRoot == null)
            {
                return;
            }

            Transform existingButton = menuRoot.Find("MonsterDexButton");
            if (existingButton != null)
            {
                RectTransform existingRect = existingButton as RectTransform;
                bool isCurrentLayout = existingRect != null &&
                    Vector2.Distance(existingRect.anchoredPosition, MonsterDexButtonPosition) < 0.5f &&
                    Vector2.Distance(existingRect.sizeDelta, MonsterDexButtonSize) < 0.5f &&
                    existingButton.Find("MonsterDexButtonVisual") != null;
                if (isCurrentLayout)
                {
                    return;
                }

                DestroySceneObject(existingButton.gameObject);
            }

            Sprite buttonSprite = Resources.Load<Sprite>("UI/HomeMenu/DexButton");
            if (buttonSprite != null)
            {
                CreateSpriteButton("MonsterDexButton", menuRoot, buttonSprite, MonsterDexButtonPosition, MonsterDexButtonSize, OpenMonsterDexMenu);
                return;
            }

            Sprite fallbackSprite = Resources.Load<Sprite>("UI/FusionPage/FusionSmallButton");
            CreateTextSpriteButton("MonsterDexButton", menuRoot, fallbackSprite, "図鑑", MonsterDexButtonPosition, MonsterDexButtonSize, OpenMonsterDexMenu);
        }

        private static void EnsureMenuButtonPosition(Transform menuRoot, string buttonName, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Transform buttonTransform = menuRoot != null ? menuRoot.Find(buttonName) : null;
            RectTransform rectTransform = buttonTransform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private void CreateHomeAtmosphere(Transform menuRoot)
        {
            CreateTintPanel(
                "HomeTopShade",
                menuRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -224f),
                new Vector2(1080f, 448f),
                new Color(0.03f, 0.02f, 0.06f, 0.42f));
            CreateTintPanel(
                "HomeHeroGlow",
                menuRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, HomeAdReservedHeight + 574f),
                new Vector2(720f, 420f),
                new Color(0.12f, 0.72f, 1f, 0.10f));
            CreateTintPanel(
                "HomeLowerShade",
                menuRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, HomeAdReservedHeight + 230f),
                new Vector2(1080f, 430f),
                new Color(0.01f, 0.01f, 0.018f, 0.58f));
        }

        private void CreateAdReservedSpace(Transform menuRoot)
        {
            Image adReserve = CreateTintPanel(
                "HomeAdReservedSpace",
                menuRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, HomeAdReservedHeight * 0.5f),
                new Vector2(1080f, HomeAdReservedHeight),
                new Color(0.03f, 0.018f, 0.07f, 0.92f));
            adReserve.raycastTarget = false;
        }

        private void CreateHomeHeroShowcase(Transform menuRoot)
        {
            GameObject showcase = new GameObject("HomeHeroShowcase", typeof(RectTransform));
            showcase.transform.SetParent(menuRoot, false);
            RectTransform showcaseRect = showcase.GetComponent<RectTransform>();
            showcaseRect.anchorMin = new Vector2(0.5f, 0f);
            showcaseRect.anchorMax = new Vector2(0.5f, 0f);
            showcaseRect.pivot = new Vector2(0.5f, 0.5f);
            showcaseRect.anchoredPosition = new Vector2(0f, HomeAdReservedHeight + 760f);
            showcaseRect.sizeDelta = new Vector2(720f, 620f);

            CreateTintPanel(
                "HomeHeroShadow",
                showcase.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 54f),
                new Vector2(520f, 86f),
                new Color(0f, 0f, 0f, 0.34f));

            homeHeroImage = CreateMenuImage(
                "HomeHeroImage",
                showcase.transform,
                ResolveHomeHeroSprite(GetRuntimeProfile(), out _, out _),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f),
                new Vector2(520f, 520f),
                true);
            homeHeroImage.color = Color.white;

            homeHeroNameText = CreateUiText(
                "HomeHeroName",
                showcase.transform,
                string.Empty,
                28,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 124f),
                new Vector2(520f, 42f),
                Color.white,
                TextAnchor.MiddleCenter);
            homeHeroLevelText = CreateUiText(
                "HomeHeroLevel",
                showcase.transform,
                string.Empty,
                22,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 88f),
                new Vector2(360f, 34f),
                new Color(1f, 0.86f, 0.42f, 1f),
                TextAnchor.MiddleCenter);
        }

        private void CreateHomeGuidePanel(Transform menuRoot)
        {
            Image panel = CreateTintPanel(
                "HomeGuidePanel",
                menuRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f),
                new Vector2(800f, 196f),
                new Color(0.02f, 0.035f, 0.045f, 0.70f));
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.78f);
            outline.effectDistance = new Vector2(2f, -2f);

            homeNextFloorText = CreateUiText(
                "HomeNextFloorText",
                panel.transform,
                string.Empty,
                20,
                FontStyle.Bold,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(700f, 34f),
                new Color(1f, 0.86f, 0.42f, 1f),
                TextAnchor.MiddleCenter);
            homeGuideText = CreateUiText(
                "HomeGuideText",
                panel.transform,
                string.Empty,
                31,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f),
                new Vector2(700f, 110f),
                Color.white,
                TextAnchor.MiddleLeft);
        }

        private void CreateHomeBottomNavigation(
            Transform menuRoot,
            Sprite panelSprite,
            Sprite gachaSprite,
            Sprite dexSprite,
            Sprite equipmentSprite,
            Sprite fusionSprite)
        {
            Sprite navBarSprite = LoadSpriteResource(HomeBottomNavBarPath, "HomeBottomNavBar");
            if (navBarSprite != null)
            {
                CreateMenuImage(
                    "HomeBottomNavBarArt",
                    menuRoot,
                    navBarSprite,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, HomeAdReservedHeight + 94f),
                    new Vector2(1080f, 190f),
                    false);
                CreateSegmentedNavButton("GoldShopNavButton", menuRoot, "ショップ", 0, OpenGoldShopMenu);
                CreateSegmentedNavButton("GachaButton", menuRoot, "ガチャ", 1, OpenGachaMenu);
                CreateSegmentedNavButton("MonsterDexButton", menuRoot, "図鑑", 2, OpenMonsterDexMenu);
                CreateSegmentedNavButton("EquipmentButton", menuRoot, "装備", 3, OpenEquipmentMenu);
                CreateSegmentedNavButton("FusionButton", menuRoot, "配合", 4, OpenFusionMenu);
                return;
            }

            Image navFrame = CreateTintPanel(
                "HomeBottomNavFrame",
                menuRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, HomeAdReservedHeight + 86f),
                new Vector2(1080f, 172f),
                new Color(0.06f, 0.045f, 0.10f, 0.94f));
            navFrame.raycastTarget = false;

            if (panelSprite != null)
            {
                Image ornament = CreateMenuImage(
                    "HomeBottomNavOrnament",
                    menuRoot,
                    panelSprite,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, HomeAdReservedHeight + 92f),
                    new Vector2(1180f, 210f),
                    false);
                ornament.color = new Color(1f, 1f, 1f, 0.18f);
            }

            float navY = HomeAdReservedHeight + 86f;
            CreateBottomTextButton("GoldShopNavButton", menuRoot, "ショップ", "G", new Vector2(-432f, navY), OpenGoldShopMenu);
            CreateHomeSpriteButton("GachaButton", menuRoot, gachaSprite, "ガチャ", new Vector2(-216f, navY), HomeBottomNavButtonSize, OpenGachaMenu, 24);
            CreateHomeSpriteButton("MonsterDexButton", menuRoot, dexSprite, "図鑑", new Vector2(0f, navY), HomeBottomNavButtonSize, OpenMonsterDexMenu, 24);
            CreateHomeSpriteButton("EquipmentButton", menuRoot, equipmentSprite, "装備", new Vector2(216f, navY), HomeBottomNavButtonSize, OpenEquipmentMenu, 24);
            CreateHomeSpriteButton("FusionButton", menuRoot, fusionSprite, "合成", new Vector2(432f, navY), HomeBottomNavButtonSize, OpenFusionMenu, 24);
        }

        private void CreateSegmentedNavButton(string name, Transform parent, string label, int index, UnityEngine.Events.UnityAction action)
        {
            const float slotWidth = 216f;
            const float navHeight = 190f;
            float x = -432f + index * slotWidth;
            Button button = CreatePlainButton(
                name,
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(x, HomeAdReservedHeight + 94f),
                new Vector2(slotWidth, navHeight),
                new Color(1f, 1f, 1f, 0.001f),
                action);
            button.targetGraphic.raycastTarget = true;

            Text labelText = CreateUiText(
                name + "Label",
                button.transform,
                label,
                28,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 34f),
                new Vector2(180f, 44f),
                Color.white,
                TextAnchor.MiddleCenter);
            AddTextShadow(labelText, new Color(0f, 0f, 0f, 0.82f), new Vector2(2f, -2f));
        }

        private void CreateHomeSpriteButton(
            string name,
            Transform parent,
            Sprite sprite,
            string fallbackLabel,
            Vector2 anchoredPosition,
            Vector2 size,
            UnityEngine.Events.UnityAction action,
            int fallbackFontSize)
        {
            if (sprite != null)
            {
                CreateSpriteButton(name, parent, sprite, anchoredPosition, size, action);
                return;
            }

            Button button = CreatePlainButton(
                name,
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                anchoredPosition,
                size,
                new Color(0.10f, 0.09f, 0.15f, 0.96f),
                action);
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.74f, 0.26f, 0.90f);
            outline.effectDistance = new Vector2(3f, -3f);
            CreateUiText(
                name + "FallbackLabel",
                button.transform,
                fallbackLabel,
                fallbackFontSize,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size - new Vector2(24f, 24f),
                Color.white,
                TextAnchor.MiddleCenter);
        }

        private Button CreateBottomTextButton(string name, Transform parent, string label, string icon, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            Button button = CreatePlainButton(
                name,
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                anchoredPosition,
                HomeBottomNavButtonSize,
                new Color(0.12f, 0.09f, 0.16f, 0.98f),
                action);
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.76f, 0.58f, 1f, 0.86f);
            outline.effectDistance = new Vector2(3f, -3f);
            CreateUiText(
                name + "Icon",
                button.transform,
                icon,
                40,
                FontStyle.Bold,
                new Vector2(0.5f, 0.64f),
                new Vector2(0.5f, 0.64f),
                Vector2.zero,
                new Vector2(120f, 52f),
                new Color(1f, 0.78f, 0.26f, 1f),
                TextAnchor.MiddleCenter);
            CreateUiText(
                name + "Label",
                button.transform,
                label,
                24,
                FontStyle.Bold,
                new Vector2(0.5f, 0.23f),
                new Vector2(0.5f, 0.23f),
                Vector2.zero,
                new Vector2(160f, 40f),
                Color.white,
                TextAnchor.MiddleCenter);
            return button;
        }

        private void CreatePlayerBadge(Transform parent, bool useGeneratedHudFrame)
        {
            GameObject root = new GameObject("PlayerBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(-420f, -68f);
            rootRect.sizeDelta = new Vector2(214f, 70f);

            Image background = root.GetComponent<Image>();
            background.color = useGeneratedHudFrame
                ? new Color(1f, 1f, 1f, 0.001f)
                : new Color(0.04f, 0.035f, 0.07f, 0.96f);
            background.raycastTarget = false;

            Sprite portrait = ResolveHomeHeroSprite(GetRuntimeProfile(), out _, out _);
            if (portrait != null)
            {
                CreateMenuImage(
                    "PlayerPortrait",
                    root.transform,
                    portrait,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(37f, 0f),
                    new Vector2(58f, 58f),
                    true);
            }

            CreateUiText(
                "PlayerName",
                root.transform,
                "探索者",
                20,
                FontStyle.Bold,
                new Vector2(0.63f, 0.63f),
                new Vector2(0.63f, 0.63f),
                Vector2.zero,
                new Vector2(126f, 28f),
                Color.white,
                TextAnchor.MiddleLeft);
            homePlayerLevelText = CreateUiText(
                "PlayerLevel",
                root.transform,
                "Lv.1",
                20,
                FontStyle.Bold,
                new Vector2(0.63f, 0.30f),
                new Vector2(0.63f, 0.30f),
                Vector2.zero,
                new Vector2(126f, 28f),
                new Color(1f, 0.86f, 0.42f, 1f),
                TextAnchor.MiddleLeft);
        }

        private static Text CreateHomeResourcePill(
            Transform parent,
            string rootName,
            string amountName,
            string iconPath,
            string fallbackIcon,
            Vector2 anchoredPosition,
            Vector2 size,
            Color accentColor)
        {
            return CreateHomeResourcePill(parent, rootName, amountName, iconPath, fallbackIcon, anchoredPosition, size, accentColor, true);
        }

        private static Text CreateHomeResourcePill(
            Transform parent,
            string rootName,
            string amountName,
            string iconPath,
            string fallbackIcon,
            Vector2 anchoredPosition,
            Vector2 size,
            Color accentColor,
            bool showIcon)
        {
            GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = size;

            Image background = root.GetComponent<Image>();
            background.color = showIcon
                ? new Color(0.018f, 0.018f, 0.030f, 0.96f)
                : new Color(1f, 1f, 1f, 0.001f);
            background.raycastTarget = false;

            if (showIcon)
            {
                Outline outline = root.AddComponent<Outline>();
                outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.52f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            Sprite iconSprite = !string.IsNullOrEmpty(iconPath) ? Resources.Load<Sprite>(iconPath) : null;
            if (showIcon && iconSprite != null)
            {
                CreateMenuImage(
                    rootName + "Icon",
                    root.transform,
                    iconSprite,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(34f, 0f),
                    new Vector2(52f, 52f),
                    true);
            }
            else if (showIcon && !string.IsNullOrEmpty(fallbackIcon))
            {
                CreateUiText(
                    rootName + "Icon",
                    root.transform,
                    fallbackIcon,
                    fallbackIcon.Length > 1 ? 22 : 30,
                    FontStyle.Bold,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(34f, 0f),
                    new Vector2(58f, 44f),
                    accentColor,
                    TextAnchor.MiddleCenter);
            }

            return CreateUiText(
                amountName,
                root.transform,
                "0",
                22,
                FontStyle.Bold,
                new Vector2(0.62f, 0.5f),
                new Vector2(0.62f, 0.5f),
                Vector2.zero,
                new Vector2(Mathf.Max(90f, size.x - 80f), 38f),
                Color.white,
                TextAnchor.MiddleLeft);
        }

        private static Image CreateTintPanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
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
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void EnsureHomeStoneBalanceBar(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            Transform existingBar = menuRoot.Find("HomeStoneBalanceBar");
            if (existingBar != null)
            {
                bool isModernLayout = existingBar.Find("HomeHudLayoutMarker") != null;
                homeFreeStoneText = existingBar.Find("FreeStoneCounter/FreeStoneAmount")?.GetComponent<Text>();
                homePaidStoneText = existingBar.Find("PaidStoneCounter/PaidStoneAmount")?.GetComponent<Text>();
                homeGoldText = existingBar.Find("GoldCounter/GoldAmount")?.GetComponent<Text>();
                homeExpText = existingBar.Find("ExpCounter/ExpAmount")?.GetComponent<Text>();
                homePlayerLevelText = existingBar.Find("PlayerBadge/PlayerLevel")?.GetComponent<Text>();
                homeGuideText = menuRoot.Find("HomeGuidePanel/HomeGuideText")?.GetComponent<Text>();
                homeNextFloorText = menuRoot.Find("HomeGuidePanel/HomeNextFloorText")?.GetComponent<Text>();
                homeHeroImage = menuRoot.Find("HomeHeroShowcase/HomeHeroImage")?.GetComponent<Image>();
                homeHeroNameText = menuRoot.Find("HomeHeroShowcase/HomeHeroName")?.GetComponent<Text>();
                homeHeroLevelText = menuRoot.Find("HomeHeroShowcase/HomeHeroLevel")?.GetComponent<Text>();
                homeQuestButtonText = existingBar.Find("QuestButton/QuestButtonLabel")?.GetComponent<Text>();
                homeQuestButton = existingBar.Find("QuestButton")?.GetComponent<Button>();
                homeShopButtonText = existingBar.Find("GoldShopButton/GoldShopButtonLabel")?.GetComponent<Text>();
                homeShopButton = existingBar.Find("GoldShopButton")?.GetComponent<Button>();
                RectTransform questButtonRect = homeQuestButton != null ? homeQuestButton.GetComponent<RectTransform>() : null;
                Image questButtonImage = homeQuestButton != null ? homeQuestButton.GetComponent<Image>() : null;
                bool hasRoundQuestButton = questButtonRect != null &&
                    questButtonImage != null &&
                    questButtonImage.sprite != null &&
                    questButtonImage.sprite.name == "QuestButtonRound" &&
                    Vector2.Distance(questButtonRect.sizeDelta, HomeQuestButtonSize) < 0.5f;
                if (homeFreeStoneText != null &&
                    homePaidStoneText != null &&
                    homeGoldText != null &&
                    homeExpText != null &&
                    homePlayerLevelText != null &&
                    homeGuideText != null &&
                    homeNextFloorText != null &&
                    homeQuestButtonText != null &&
                    homeQuestButton != null &&
                    homeShopButtonText != null &&
                    homeShopButton != null &&
                    isModernLayout &&
                    hasRoundQuestButton)
                {
                    EnsureDailyQuestList(menuRoot);
                    return;
                }

                DestroySceneObject(existingBar.gameObject);
            }

            GameObject bar = new GameObject("HomeStoneBalanceBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar.transform.SetParent(menuRoot, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 1f);
            barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -20f);
            barRect.sizeDelta = HomeStoneBarSize;

            Image barImage = bar.GetComponent<Image>();
            barImage.color = new Color(0.012f, 0.012f, 0.022f, 0.10f);
            barImage.raycastTarget = false;

            Sprite hudFrameSprite = LoadSpriteResource(HomeTopHudFramePath, "HomeTopHudFrame");
            bool useGeneratedHudFrame = hudFrameSprite != null;
            if (useGeneratedHudFrame)
            {
                Image hudFrame = CreateMenuImage(
                    "HomeTopHudFrameArt",
                    bar.transform,
                    hudFrameSprite,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -68f),
                    new Vector2(1040f, 132f),
                    false);
                hudFrame.transform.SetAsFirstSibling();
            }

            GameObject marker = new GameObject("HomeHudLayoutMarker", typeof(RectTransform));
            marker.transform.SetParent(bar.transform, false);

            CreatePlayerBadge(bar.transform, useGeneratedHudFrame);
            homeGoldText = CreateHomeResourcePill(
                bar.transform,
                "GoldCounter",
                "GoldAmount",
                null,
                "G",
                new Vector2(-198f, -68f),
                new Vector2(180f, 58f),
                new Color(1f, 0.74f, 0.22f, 1f),
                !useGeneratedHudFrame);
            homeFreeStoneText = CreateHomeResourcePill(
                bar.transform,
                "FreeStoneCounter",
                "FreeStoneAmount",
                FreeStoneIconPath,
                string.Empty,
                new Vector2(12f, -68f),
                new Vector2(204f, 58f),
                new Color(0.54f, 0.94f, 1f, 1f),
                !useGeneratedHudFrame);
            homePaidStoneText = CreateHomeResourcePill(
                bar.transform,
                "PaidStoneCounter",
                "PaidStoneAmount",
                PaidStoneIconPath,
                string.Empty,
                new Vector2(238f, -68f),
                new Vector2(204f, 58f),
                new Color(1f, 0.54f, 1f, 1f),
                !useGeneratedHudFrame);
            homeExpText = CreateHomeResourcePill(
                bar.transform,
                "ExpCounter",
                "ExpAmount",
                null,
                "EXP",
                new Vector2(438f, -68f),
                new Vector2(190f, 58f),
                new Color(1f, 0.88f, 0.28f, 1f),
                !useGeneratedHudFrame);
            homeShopButton = CreateGoldShopButton(bar.transform, HomeShopButtonPosition, HomeShopButtonSize);
            homeQuestButton = CreateQuestButton(bar.transform, HomeQuestButtonPosition, HomeQuestButtonSize);
            EnsureDailyQuestList(menuRoot);
        }

        private void RefreshHomeStoneBalanceBar()
        {
            if (homeFreeStoneText == null &&
                homePaidStoneText == null &&
                homeGoldText == null &&
                homeExpText == null &&
                homePlayerLevelText == null &&
                homeGuideText == null &&
                homeQuestButtonText == null &&
                homeShopButtonText == null)
            {
                return;
            }

            PlayerProfile profile = GetRuntimeProfile();
            if (profile == null)
            {
                if (homeFreeStoneText != null)
                {
                    homeFreeStoneText.text = "-";
                }

                if (homePaidStoneText != null)
                {
                    homePaidStoneText.text = "-";
                }

                if (homeGoldText != null)
                {
                    homeGoldText.text = "-";
                }

                if (homeExpText != null)
                {
                    homeExpText.text = "-";
                }

                if (homePlayerLevelText != null)
                {
                    homePlayerLevelText.text = "Lv.-";
                }

                if (homeGuideText != null)
                {
                    homeGuideText.text = "ようこそ！\n冒険の準備をしましょう。";
                }

                if (homeNextFloorText != null)
                {
                    homeNextFloorText.text = "次の挑戦: -";
                }

                if (homeQuestButtonText != null)
                {
                    homeQuestButtonText.text = "クエスト";
                }

                if (homeShopButtonText != null)
                {
                    homeShopButtonText.text = "ショップ";
                }

                if (homeQuestButton != null)
                {
                    homeQuestButton.interactable = false;
                }

                if (homeShopButton != null)
                {
                    homeShopButton.interactable = false;
                }

                RefreshHomeHeroShowcase(profile);
                return;
            }

            if (homeFreeStoneText != null)
            {
                homeFreeStoneText.text = profile.FreeGachaStones.ToString("N0");
            }

            if (homePaidStoneText != null)
            {
                homePaidStoneText.text = profile.PaidGachaStones.ToString("N0");
            }

            if (homeGoldText != null)
            {
                homeGoldText.text = profile.Gold.ToString("N0");
            }

            if (homeExpText != null)
            {
                homeExpText.text = profile.Exp.ToString("N0");
            }

            if (homePlayerLevelText != null)
            {
                homePlayerLevelText.text = $"Lv.{Mathf.Max(1, profile.Level)}";
            }

            if (homeGuideText != null)
            {
                homeGuideText.text = BuildHomeGuideText(profile);
            }

            if (homeNextFloorText != null)
            {
                homeNextFloorText.text = $"次の挑戦: 第{Mathf.Max(1, profile.HighestFloor + 1)}階";
            }

            if (homeQuestButtonText != null)
            {
                homeQuestButtonText.text = DailyRewardService.HasClaimableQuest(profile, DateTime.Now) ? "クエスト!" : "クエスト";
            }

            if (homeShopButtonText != null)
            {
                homeShopButtonText.text = "ショップ";
            }

            if (homeQuestButton != null)
            {
                homeQuestButton.interactable = true;
            }

            if (homeShopButton != null)
            {
                homeShopButton.interactable = true;
            }

            RefreshHomeHeroShowcase(profile);
        }

        private void RefreshHomeHeroShowcase(PlayerProfile profile)
        {
            Sprite sprite = ResolveHomeHeroSprite(profile, out string heroName, out string heroLevelText);
            if (homeHeroImage != null)
            {
                homeHeroImage.sprite = sprite;
                homeHeroImage.color = sprite != null ? Color.white : new Color(0.18f, 0.18f, 0.24f, 0.70f);
                homeHeroImage.preserveAspect = true;
            }

            if (homeHeroNameText != null)
            {
                homeHeroNameText.text = heroName;
            }

            if (homeHeroLevelText != null)
            {
                homeHeroLevelText.text = heroLevelText;
            }
        }

        private static Sprite ResolveHomeHeroSprite(PlayerProfile profile, out string heroName, out string heroLevelText)
        {
            OwnedMonsterData leadMonster = ResolveLeadMonster(profile);
            MonsterDataSO monsterData = null;
            if (leadMonster != null && MasterDataManager.Instance != null)
            {
                monsterData = MasterDataManager.Instance.GetMonsterData(leadMonster.MonsterId);
            }

            heroName = monsterData != null && !string.IsNullOrEmpty(monsterData.monsterName)
                ? monsterData.monsterName
                : "ヒナドラ";
            heroLevelText = leadMonster != null
                ? $"Lv.{Mathf.Max(1, leadMonster.Level)}"
                : "Lv.1";

            Sprite sprite = monsterData != null ? monsterData.illustrationSprite : null;
            if (sprite == null && monsterData != null)
            {
                sprite = monsterData.portraitSprite;
            }

            if (sprite == null && monsterData != null && !string.IsNullOrEmpty(monsterData.illustrationResourcePath))
            {
                sprite = Resources.Load<Sprite>(monsterData.illustrationResourcePath);
            }

            if (sprite == null && monsterData != null && !string.IsNullOrEmpty(monsterData.portraitResourcePath))
            {
                sprite = Resources.Load<Sprite>(monsterData.portraitResourcePath);
            }

            return sprite != null ? sprite : Resources.Load<Sprite>(HomeFallbackHeroSpritePath);
        }

        private static OwnedMonsterData ResolveLeadMonster(PlayerProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
            {
                OwnedMonsterData partyMonster = profile.GetOwnedMonster(profile.PartyMonsterInstanceIds[i]);
                if (partyMonster != null)
                {
                    return partyMonster;
                }
            }

            return profile.OwnedMonsters.Count > 0 ? profile.OwnedMonsters[0] : null;
        }

        private static string BuildHomeGuideText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "よぉし！\n今日の冒険を始めましょう。";
            }

            DateTime now = DateTime.Now;
            int claimableQuestCount = DailyRewardService.GetClaimableQuestCount(profile, now);
            if (claimableQuestCount > 0)
            {
                return $"よぉし！\n受け取れるクエスト報酬が{claimableQuestCount}件あります！";
            }

            if (profile.PendingIdleRewardGold > 0)
            {
                return $"よぉし！\n放置報酬 {profile.PendingIdleRewardGold:N0}G を回収できます！";
            }

            int nextFloor = Mathf.Max(1, profile.HighestFloor + 1);
            if (profile.Gold >= 100)
            {
                return $"準備はいい感じ！\n第{nextFloor}階へ挑戦して、装備を集めましょう。";
            }

            return $"よぉし！\n第{nextFloor}階へ挑戦して、ゴールドを稼ぎましょう！";
        }

        private void EnsureDailyQuestList(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            IReadOnlyList<DailyQuestDefinition> definitions = DailyRewardService.GetDefinitions();
            Transform existingPanel = menuRoot.Find("DailyQuestListPanel");
            if (existingPanel != null)
            {
                if (dailyQuestListRoot == existingPanel.gameObject &&
                    dailyQuestCards.Count == definitions.Count &&
                    dailyQuestStatusText != null)
                {
                    return;
                }

                DestroySceneObject(existingPanel.gameObject);
            }

            dailyQuestCards.Clear();
            dailyQuestListRoot = new GameObject("DailyQuestListPanel", typeof(RectTransform));
            dailyQuestListRoot.transform.SetParent(menuRoot, false);
            RectTransform rootRect = dailyQuestListRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Button shadeButton = CreatePlainButton(
                "DailyQuestShade",
                dailyQuestListRoot.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1080f, 1920f),
                new Color(0f, 0f, 0f, 0.58f),
                CloseDailyQuestList);
            shadeButton.targetGraphic.raycastTarget = true;

            GameObject panel = new GameObject("DailyQuestPanelBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(dailyQuestListRoot.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(860f, 820f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.018f, 0.024f, 0.030f, 0.96f);
            panelImage.raycastTarget = false;

            CreateUiText(
                "DailyQuestTitle",
                panel.transform,
                "デイリークエスト",
                34,
                FontStyle.Bold,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -54f),
                new Vector2(520f, 52f),
                new Color(1f, 0.86f, 0.42f, 1f),
                TextAnchor.MiddleCenter);

            CreatePlainButton(
                "DailyQuestCloseButton",
                panel.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-62f, -54f),
                new Vector2(78f, 52f),
                new Color(0.24f, 0.10f, 0.09f, 0.96f),
                CloseDailyQuestList);
            CreateUiText(
                "DailyQuestCloseLabel",
                panel.transform.Find("DailyQuestCloseButton"),
                "閉じる",
                18,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(70f, 34f),
                Color.white,
                TextAnchor.MiddleCenter);

            for (int i = 0; i < definitions.Count; i += 1)
            {
                CreateDailyQuestCard(panel.transform, definitions[i], i);
            }

            dailyQuestStatusText = CreateUiText(
                "DailyQuestStatus",
                panel.transform,
                string.Empty,
                20,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 48f),
                new Vector2(740f, 42f),
                new Color(0.78f, 0.90f, 0.92f, 1f),
                TextAnchor.MiddleCenter);

            dailyQuestListRoot.SetActive(false);
        }

        private void CreateDailyQuestCard(Transform parent, DailyQuestDefinition definition, int index)
        {
            GameObject card = new GameObject("DailyQuestCard_" + definition.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(parent, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 190f - index * 170f);
            cardRect.sizeDelta = new Vector2(760f, 150f);

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.034f, 0.044f, 0.052f, 0.94f);
            cardImage.raycastTarget = false;

            CreateUiText(
                "DailyQuestName",
                card.transform,
                definition.Title,
                25,
                FontStyle.Bold,
                new Vector2(0f, 0.42f),
                new Vector2(0f, 0.42f),
                new Vector2(186f, 0f),
                new Vector2(330f, 40f),
                Color.white,
                TextAnchor.MiddleLeft);

            CreateUiText(
                "DailyQuestReward",
                card.transform,
                "報酬",
                17,
                FontStyle.Bold,
                new Vector2(0f, 0.68f),
                new Vector2(0f, 0.68f),
                new Vector2(186f, 0f),
                new Vector2(320f, 30f),
                new Color(1f, 0.82f, 0.36f, 1f),
                TextAnchor.MiddleLeft);

            Text progressText = CreateUiText(
                "DailyQuestProgress",
                card.transform,
                string.Empty,
                22,
                FontStyle.Bold,
                new Vector2(0.57f, 0.5f),
                new Vector2(0.57f, 0.5f),
                Vector2.zero,
                new Vector2(170f, 70f),
                new Color(0.62f, 0.94f, 1f, 1f),
                TextAnchor.MiddleCenter);

            Button claimButton = CreatePlainButton(
                "DailyQuestClaimButton",
                card.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-106f, 0f),
                new Vector2(178f, 84f),
                new Color(0.10f, 0.32f, 0.30f, 0.98f),
                () => ClaimDailyQuestReward(definition.Id));

            Graphic claimStoneGraphic = null;
            Sprite freeStoneSprite = Resources.Load<Sprite>(FreeStoneIconPath);
            if (freeStoneSprite != null)
            {
                claimStoneGraphic = CreateMenuImage(
                    "DailyQuestClaimStoneIcon",
                    claimButton.transform,
                    freeStoneSprite,
                    new Vector2(0.32f, 0.5f),
                    new Vector2(0.32f, 0.5f),
                    Vector2.zero,
                    new Vector2(58f, 58f),
                    true);
            }
            else
            {
                Texture2D freeStoneTexture = Resources.Load<Texture2D>(FreeStoneIconPath);
                if (freeStoneTexture != null)
                {
                    claimStoneGraphic = CreateMenuRawImage(
                        "DailyQuestClaimStoneIcon",
                        claimButton.transform,
                        freeStoneTexture,
                        new Vector2(0.32f, 0.5f),
                        new Vector2(0.32f, 0.5f),
                        Vector2.zero,
                        new Vector2(58f, 58f));
                }
            }

            Text claimButtonText = CreateUiText(
                "DailyQuestClaimLabel",
                claimButton.transform,
                definition.RewardFreeGachaStones.ToString(),
                28,
                FontStyle.Bold,
                new Vector2(0.68f, 0.5f),
                new Vector2(0.68f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(82f, 42f),
                Color.white,
                TextAnchor.MiddleLeft);

            dailyQuestCards.Add(new DailyQuestCardView
            {
                QuestId = definition.Id,
                ProgressText = progressText,
                ClaimButton = claimButton,
                ClaimButtonText = claimButtonText,
                ClaimStoneGraphic = claimStoneGraphic
            });
        }

        private void RefreshDailyQuestList()
        {
            if (dailyQuestListRoot == null)
            {
                return;
            }

            PlayerProfile profile = GetRuntimeProfile();
            DateTime now = DateTime.Now;
            foreach (DailyQuestCardView card in dailyQuestCards)
            {
                DailyQuestDefinition definition = DailyRewardService.GetDefinition(card.QuestId);
                if (definition == null)
                {
                    continue;
                }

                int progress = DailyRewardService.GetBattleWinProgress(profile, now, card.QuestId);
                bool isClaimed = DailyRewardService.IsClaimed(profile, now, card.QuestId);
                bool canClaim = DailyRewardService.IsClaimable(profile, now, card.QuestId);

                if (card.ProgressText != null)
                {
                    card.ProgressText.text = $"進捗\n{progress}/{definition.RequiredBattleWins}";
                }

                if (card.ClaimButton != null)
                {
                    card.ClaimButton.interactable = canClaim;
                    Image buttonImage = card.ClaimButton.GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = canClaim
                            ? new Color(0.10f, 0.38f, 0.34f, 0.98f)
                            : new Color(0.13f, 0.15f, 0.16f, 0.86f);
                    }
                }

                if (card.ClaimButtonText != null)
                {
                    card.ClaimButtonText.text = isClaimed ? "済" : definition.RewardFreeGachaStones.ToString();
                    card.ClaimButtonText.color = canClaim
                        ? Color.white
                        : isClaimed
                            ? new Color(0.64f, 0.68f, 0.70f, 1f)
                            : new Color(0.78f, 0.82f, 0.84f, 1f);
                }

                if (card.ClaimStoneGraphic != null)
                {
                    card.ClaimStoneGraphic.color = canClaim
                        ? Color.white
                        : new Color(0.74f, 0.80f, 0.82f, 0.92f);
                }
            }

            if (dailyQuestStatusText != null)
            {
                int questCount = DailyRewardService.GetDefinitions().Count;
                int claimedCount = DailyRewardService.GetClaimedQuestCount(profile, now);
                int claimableCount = DailyRewardService.GetClaimableQuestCount(profile, now);
                dailyQuestStatusText.text = claimedCount >= questCount
                    ? "今日のデイリークエストはすべて受け取り済みです。"
                    : claimableCount > 0
                        ? $"受取可能なクエストが{claimableCount}件あります。"
                        : "バトルに勝利して、デイリークエストを達成しましょう。";
            }
        }

        private static PlayerProfile GetRuntimeProfile()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile != null || !Application.isPlaying)
            {
                return profile;
            }

            EnsureRuntimeState();
            return GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
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

        private static void DestroySceneObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
            }
            else
            {
                Destroy(target);
            }
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

        private static RawImage CreateMenuRawImage(string name, Transform parent, Texture texture, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            RawImage image = root.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateHomeStoneCounter(Transform parent, string name, string iconPath, Vector2 anchoredPosition)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(248f, 62f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.04f, 0.82f);
            background.raycastTarget = false;

            Sprite iconSprite = Resources.Load<Sprite>(iconPath);
            if (iconSprite != null)
            {
                CreateMenuImage(
                    name + "Icon",
                    root.transform,
                    iconSprite,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(36f, 0f),
                    new Vector2(52f, 52f),
                    true);
            }

            return CreateUiText(
                name == "FreeStoneCounter" ? "FreeStoneAmount" : "PaidStoneAmount",
                root.transform,
                "0",
                24,
                FontStyle.Bold,
                new Vector2(0.62f, 0.5f),
                new Vector2(0.62f, 0.5f),
                Vector2.zero,
                new Vector2(148f, 38f),
                Color.white,
                TextAnchor.MiddleLeft);
        }

        private Button CreateQuestButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject root = new GameObject("QuestButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            Sprite questSprite = LoadSpriteResource(QuestButtonSpritePath, "QuestButtonRound");
            image.sprite = questSprite;
            image.color = questSprite != null ? Color.white : new Color(0.08f, 0.18f, 0.19f, 0.94f);
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenDailyQuestList);

            homeQuestButtonText = CreateUiText(
                "QuestButtonLabel",
                root.transform,
                "クエスト",
                18,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -2f),
                new Vector2(104f, 44f),
                Color.white,
                TextAnchor.MiddleCenter);
            return button;
        }

        private Button CreateGoldShopButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject root = new GameObject("GoldShopButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.20f, 0.12f, 0.05f, 0.98f);
            image.raycastTarget = true;

            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.72f, 0.24f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenGoldShopMenu);

            CreateUiText(
                "GoldShopTopLabel",
                root.transform,
                "SHOP",
                24,
                FontStyle.Bold,
                new Vector2(0.5f, 0.70f),
                new Vector2(0.5f, 0.70f),
                Vector2.zero,
                new Vector2(130f, 34f),
                new Color(1f, 0.62f, 0.20f, 1f),
                TextAnchor.MiddleCenter);
            CreateUiText(
                "GoldShopIcon",
                root.transform,
                "G",
                42,
                FontStyle.Bold,
                new Vector2(0.5f, 0.47f),
                new Vector2(0.5f, 0.47f),
                Vector2.zero,
                new Vector2(120f, 50f),
                new Color(1f, 0.82f, 0.26f, 1f),
                TextAnchor.MiddleCenter);
            homeShopButtonText = CreateUiText(
                "GoldShopButtonLabel",
                root.transform,
                "ショップ",
                18,
                FontStyle.Bold,
                new Vector2(0.5f, 0.18f),
                new Vector2(0.5f, 0.18f),
                Vector2.zero,
                new Vector2(132f, 34f),
                new Color(1f, 0.92f, 0.68f, 1f),
                TextAnchor.MiddleCenter);
            return button;
        }

        private static Sprite LoadSpriteResource(string path, string fallbackName)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                return null;
            }

            Sprite createdSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            createdSprite.name = fallbackName;
            return createdSprite;
        }

        private static Button CreatePlainButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            return button;
        }

        private static Text CreateUiText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            TextAnchor alignment)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Text text = root.GetComponent<Text>();
            text.font = GetRuntimeFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void AddTextShadow(Text text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
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
