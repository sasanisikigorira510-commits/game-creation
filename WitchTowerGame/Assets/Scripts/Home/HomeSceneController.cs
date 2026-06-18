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
        [SerializeField] private PaidShopPanelController paidShopPanelController;
        [SerializeField] private DungeonSelectionPanelController dungeonSelectionPanelController;
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string formationSceneName = "FormationScene";
        [SerializeField] private string equipmentSceneName = "EquipmentScene";
        [SerializeField] private string fusionSceneName = "FusionScene";
        [SerializeField] private string gachaSceneName = "GachaScene";
        private const string FreeStoneIconPath = "UI/GachaPage/GachaStoneFreeIcon";
        private const string PaidStoneIconPath = "UI/GachaPage/GachaStonePaidIcon";
        private const string QuestButtonSpritePath = "UI/HomeMenu/QuestButtonRound";
        private const string PaidShopIconSpritePath = "UI/HomeMenu/PaidShopIcon";
        private const string PaidShopButtonFrameSpritePath = "UI/HomeMenu/PaidShopButtonFrame";
        private const string PermanentUpgradeIconSpritePath = "UI/HomeMenu/PermanentUpgradeIcon";
        private const string PermanentUpgradeButtonFrameSpritePath = "UI/HomeMenu/PermanentUpgradeButtonFrame";
        private const string PaidShopLabel = "宝晶商店";
        private const string PermanentUpgradeLabel = "永続強化";
        private const string GoldShopLabel = "商店";
        private const string HomeTopHudFramePath = "UI/HomeRedesign/HomeTopHudFrame";
        private const string HomeBottomNavBarPath = "UI/HomeRedesign/HomeBottomNavBar";
        private const string HomeTopHudProfileFramePath = "UI/HomeRedesign/HomeTopHudProfile";
        private const string HomeTopHudGoldFramePath = "UI/HomeRedesign/HomeTopHudGold";
        private const string HomeTopHudFreeStoneFramePath = "UI/HomeRedesign/HomeTopHudFreeStone";
        private const string HomeTopHudPaidStoneFramePath = "UI/HomeRedesign/HomeTopHudPaidStone";
        private const string HomeFallbackHeroSpritePath = "FamilyMonsterCards/Dragon/dragon_whelp";
        private const string RockGolemMonsterId = "monster_rock_golem";
        private const string RockGolemHomeHeroSpritePath = "MonsterBattle/mon_rock_golem_attack_0";
        private const float HomeAdReservedHeight = 170f;
        private static readonly Vector2 HomeGuidePanelPosition = new Vector2(0f, HomeAdReservedHeight + 1340f);
        private static readonly Vector2 HomeGuidePanelSize = new Vector2(940f, 164f);
        private static readonly Vector2 HomeMenuButtonSize = new Vector2(480f, 250f);
        private static readonly Vector2 HomeMainActionButtonSize = new Vector2(470f, 220f);
        private static readonly Vector2 HomeBottomNavButtonSize = new Vector2(196f, 152f);
        private const float HomeBottomNavVisualWidth = 1064f;
        private const float HomeBottomNavSlotWidth = HomeBottomNavVisualWidth / 5f;
        private const float HomeBottomNavHeight = 190f;
        private const float HomeBottomNavCenterOffsetX = 4f;
        private const float HomeBottomNavCenterY = HomeAdReservedHeight + 94f;
        private static readonly Vector2 HomeBottomNavButtonHitSize = new Vector2(HomeBottomNavSlotWidth, HomeBottomNavHeight);
        private static readonly Vector2 HomeBottomNavSegmentSize = new Vector2(HomeBottomNavSlotWidth, HomeBottomNavHeight);
        private static readonly Vector2 HomeBottomNavLabelPosition = new Vector2(9f, 42f);
        private static readonly Vector2 HomeBottomNavLabelSize = new Vector2(HomeBottomNavSlotWidth, 46f);
        private static readonly Vector2[] HomeBottomNavLabelPositions =
        {
            new Vector2(18f, 37f),
            new Vector2(10f, 42f),
            new Vector2(-1f, 42f),
            new Vector2(-14f, 42f),
            new Vector2(-22f, 42f)
        };
        private static readonly string[] HomeBottomNavButtonNames =
        {
            "GoldShopNavButton",
            "GachaButton",
            "MonsterDexButton",
            "EquipmentButton",
            "FusionButton"
        };
        private static readonly string[] HomeTutorialActionButtonNames =
        {
            "BattleButton",
            "FormationButton",
            "GoldShopNavButton",
            "GachaButton",
            "MonsterDexButton",
            "EquipmentButton",
            "FusionButton",
            "GoldShopButton",
            "PermanentUpgradeButton",
            "QuestButton"
        };
        private static readonly string[] HomeBottomNavPartPaths =
        {
            "UI/HomeRedesign/HomeBottomNavShop",
            "UI/HomeRedesign/HomeBottomNavGacha",
            "UI/HomeRedesign/HomeBottomNavDex",
            "UI/HomeRedesign/HomeBottomNavEquipment",
            "UI/HomeRedesign/HomeBottomNavFusion"
        };
        private static readonly Vector2 HomeMenuLeftTopPosition = new Vector2(-260f, 715f);
        private static readonly Vector2 HomeMenuRightTopPosition = new Vector2(260f, 715f);
        private static readonly Vector2 HomeMenuLeftMiddlePosition = new Vector2(-260f, 445f);
        private static readonly Vector2 HomeMenuRightMiddlePosition = new Vector2(260f, 445f);
        private static readonly Vector2 HomeMenuLeftBottomPosition = new Vector2(-260f, 175f);
        private static readonly Vector2 HomeMenuRightBottomPosition = new Vector2(260f, 175f);
        private static readonly Vector2 MonsterDexButtonPosition = HomeMenuRightBottomPosition;
        private static readonly Vector2 MonsterDexButtonSize = HomeMenuButtonSize;
        private static readonly Vector2 HomeStoneBarSize = new Vector2(1040f, 136f);
        private static readonly Vector2 HomeTopHudProfilePosition = new Vector2(-319f, -68f);
        private static readonly Vector2 HomeTopHudProfileSize = new Vector2(190f, 132f);
        private static readonly Vector2 HomeTopHudGoldPosition = new Vector2(-118f, -68f);
        private static readonly Vector2 HomeTopHudGoldSize = new Vector2(232f, 132f);
        private static readonly Vector2 HomeTopHudFreeStonePosition = new Vector2(90f, -68f);
        private static readonly Vector2 HomeTopHudFreeStoneSize = new Vector2(224f, 132f);
        private static readonly Vector2 HomeTopHudPaidStonePosition = new Vector2(301f, -68f);
        private static readonly Vector2 HomeTopHudPaidStoneSize = new Vector2(226f, 132f);
        private const float HomeResourceAmountLeftReserve = 64f;
        private const float HomeResourceAmountRightPadding = 46f;
        private const float HomeResourceAmountNarrowRightPadding = 56f;
        private const float HomeResourceAmountVerticalOffset = -4f;
        private const float HomeYellowResourceAmountVerticalOffset = -2f;
        private static readonly Vector2 HomePlayerLevelTextAnchor = new Vector2(0.5f, 0.45f);
        private static readonly Vector2 HomePlayerExpTextAnchor = new Vector2(0.5f, 0.45f);
        private static readonly Vector2 HomePlayerLevelTextOffset = new Vector2(14f, 4f);
        private static readonly Vector2 HomePlayerExpTextOffset = new Vector2(0f, 4f);
        private static readonly Vector2 HomePlayerLevelTextSize = new Vector2(154f, 42f);
        private static readonly Vector2 HomePlayerExpTextSize = new Vector2(164f, 42f);
        private static readonly Vector2 HomeShopButtonPosition = new Vector2(-438f, -210f);
        private static readonly Vector2 HomeShopButtonSize = new Vector2(204f, 186f);
        private static readonly Vector2 PermanentUpgradeButtonPosition = new Vector2(-438f, -405f);
        private static readonly Vector2 PermanentUpgradeButtonSize = new Vector2(204f, 186f);
        private static readonly Vector2 HomeQuestButtonPosition = new Vector2(456f, -210f);
        private static readonly Vector2 HomeQuestButtonSize = new Vector2(168f, 168f);
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
        private bool homePlayerExpDetailsVisible;
        private Text homeGuideText;
        private Text homeNextFloorText;
        private Button homeGuideButton;
        private GameObject homeTutorialFocusRoot;
        private Text homeTutorialFocusText;
        private Text homeHeroNameText;
        private Text homeHeroLevelText;
        private Image homeHeroImage;
        private Text homeQuestButtonText;
        private Button homeQuestButton;
        private Text homeShopButtonText;
        private Button homeShopButton;
        private Text permanentUpgradeButtonText;
        private Text permanentUpgradeStatusText;
        private Button permanentUpgradeButton;
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

                EnsureBottomNavigationLayout(unifiedMenuRoot.transform);

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

            if (!TryAdvanceHomeTutorialForTarget("home.battle"))
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
                    RemoveHomeAtmosphereOverlays(unifiedMenuRoot.transform);
                    EnsureHomeGuidePanel(unifiedMenuRoot.transform);
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
            EnsureHomeGuidePanel(unifiedMenuRoot.transform);

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

            if (!TryAdvanceHomeTutorialForTarget("home.formation"))
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

            if (!TryAdvanceHomeTutorialForTarget("home.equipment"))
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

            if (!TryAdvanceHomeTutorialForTarget("home.fusion"))
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

            if (!TryAdvanceHomeTutorialForTarget("home.gacha"))
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

            if (!TryAdvanceHomeTutorialForTarget("home.dex"))
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

            if (!TryAdvanceHomeTutorialForTarget("home.shop"))
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

        public void OpenPaidShopMenu()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!TryAdvanceHomeTutorialForTarget("home.shop"))
            {
                return;
            }

            HideUnifiedMenu();
            PaidShopPanelController shopPanel = EnsurePaidShopPanel();
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

        public void OpenPermanentUpgradeShop()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!TryAdvanceHomeTutorialForTarget("home.shop"))
            {
                return;
            }

            HideUnifiedMenu();
            PaidShopPanelController shopPanel = EnsurePaidShopPanel();
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
            shopPanel.OpenPurchasedPermanentUpgradeList();
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

        private PaidShopPanelController EnsurePaidShopPanel()
        {
            if (paidShopPanelController != null)
            {
                return paidShopPanelController;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return null;
            }

            Transform existingPanel = canvas.transform.Find("PaidShopPanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : CreateUiRoot("PaidShopPanel", canvas.transform);

            paidShopPanelController = panelObject.GetComponent<PaidShopPanelController>();
            if (paidShopPanelController == null)
            {
                paidShopPanelController = panelObject.AddComponent<PaidShopPanelController>();
            }

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            return paidShopPanelController;
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
            RemoveHomeAtmosphereOverlays(menuRoot);
        }

        private static void RemoveHomeAtmosphereOverlays(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            RemoveDirectChild(menuRoot, "HomeTopShade");
            RemoveDirectChild(menuRoot, "HomeHeroGlow");
            RemoveDirectChild(menuRoot, "HomeLowerShade");
            Transform showcase = menuRoot.Find("HomeHeroShowcase");
            if (showcase != null)
            {
                RemoveDirectChild(showcase, "HomeHeroShadow");
            }
        }

        private static void RemoveDirectChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                DestroySceneObject(child.gameObject);
            }
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

            homeHeroImage = CreateMenuImage(
                "HomeHeroImage",
                showcase.transform,
                ResolveHomeHeroSprite(GetRuntimeProfile()),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f),
                new Vector2(520f, 520f),
                true);
            homeHeroImage.color = Color.white;

            homeHeroNameText = null;
            homeHeroLevelText = null;
        }

        private void EnsureHomeGuidePanel(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            Transform existingPanel = menuRoot.Find("HomeGuidePanel");
            if (existingPanel == null)
            {
                GameObject panel = new GameObject("HomeGuidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panel.transform.SetParent(menuRoot, false);
                existingPanel = panel.transform;

                Image panelImage = panel.GetComponent<Image>();
                panelImage.color = new Color(0.018f, 0.032f, 0.048f, 0.74f);
                panelImage.raycastTarget = true;
                Outline outline = panel.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.78f, 0.40f, 0.34f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            else
            {
                Image panelImage = existingPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.raycastTarget = true;
                }
            }

            homeGuideButton = existingPanel.GetComponent<Button>();
            if (homeGuideButton == null)
            {
                homeGuideButton = existingPanel.gameObject.AddComponent<Button>();
            }

            homeGuideButton.transition = Selectable.Transition.None;
            homeGuideButton.targetGraphic = existingPanel.GetComponent<Image>();
            homeGuideButton.onClick.RemoveAllListeners();
            homeGuideButton.onClick.AddListener(AdvanceHomeGuidePanel);

            RectTransform panelRect = existingPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = HomeGuidePanelPosition;
                panelRect.sizeDelta = HomeGuidePanelSize;
            }

            homeGuideText = existingPanel.Find("HomeGuideText")?.GetComponent<Text>();
            if (homeGuideText == null)
            {
                homeGuideText = CreateUiText(
                    "HomeGuideText",
                    existingPanel,
                    string.Empty,
                    24,
                    FontStyle.Bold,
                    new Vector2(0.5f, 0.58f),
                    new Vector2(0.5f, 0.58f),
                    Vector2.zero,
                    new Vector2(850f, 92f),
                    new Color(0.96f, 0.95f, 0.88f, 1f),
                    TextAnchor.MiddleCenter);
            }

            homeGuideText.resizeTextForBestFit = true;
            homeGuideText.resizeTextMinSize = 16;
            homeGuideText.resizeTextMaxSize = 24;
            homeGuideText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeGuideText.verticalOverflow = VerticalWrapMode.Truncate;

            homeNextFloorText = existingPanel.Find("HomeNextFloorText")?.GetComponent<Text>();
            if (homeNextFloorText == null)
            {
                homeNextFloorText = CreateUiText(
                    "HomeNextFloorText",
                    existingPanel,
                    string.Empty,
                    18,
                    FontStyle.Bold,
                    new Vector2(0.5f, 0.16f),
                    new Vector2(0.5f, 0.16f),
                    Vector2.zero,
                    new Vector2(820f, 34f),
                    new Color(0.72f, 0.88f, 1f, 0.94f),
                    TextAnchor.MiddleCenter);
            }

            homeNextFloorText.resizeTextForBestFit = true;
            homeNextFloorText.resizeTextMinSize = 13;
            homeNextFloorText.resizeTextMaxSize = 18;
            homeNextFloorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeNextFloorText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void CreateHomeBottomNavigation(
            Transform menuRoot,
            Sprite panelSprite,
            Sprite gachaSprite,
            Sprite dexSprite,
            Sprite equipmentSprite,
            Sprite fusionSprite)
        {
            Sprite[] navPartSprites = LoadBottomNavPartSprites();
            if (navPartSprites != null)
            {
                CreateSegmentedNavButton("GoldShopNavButton", menuRoot, GoldShopLabel, 0, OpenGoldShopMenu, navPartSprites[0]);
                CreateSegmentedNavButton("GachaButton", menuRoot, "召喚", 1, OpenGachaMenu, navPartSprites[1]);
                CreateSegmentedNavButton("MonsterDexButton", menuRoot, "図鑑", 2, OpenMonsterDexMenu, navPartSprites[2]);
                CreateSegmentedNavButton("EquipmentButton", menuRoot, "装備", 3, OpenEquipmentMenu, navPartSprites[3]);
                CreateSegmentedNavButton("FusionButton", menuRoot, "配合", 4, OpenFusionMenu, navPartSprites[4]);
                EnsureBottomNavigationLayout(menuRoot, navPartSprites);
                return;
            }

            Sprite navBarSprite = LoadSpriteResource(HomeBottomNavBarPath, "HomeBottomNavBar");
            if (navBarSprite != null)
            {
                CreateSegmentedNavButton("GoldShopNavButton", menuRoot, GoldShopLabel, 0, OpenGoldShopMenu, null, navBarSprite);
                CreateSegmentedNavButton("GachaButton", menuRoot, "召喚", 1, OpenGachaMenu, null, navBarSprite);
                CreateSegmentedNavButton("MonsterDexButton", menuRoot, "図鑑", 2, OpenMonsterDexMenu, null, navBarSprite);
                CreateSegmentedNavButton("EquipmentButton", menuRoot, "装備", 3, OpenEquipmentMenu, null, navBarSprite);
                CreateSegmentedNavButton("FusionButton", menuRoot, "配合", 4, OpenFusionMenu, null, navBarSprite);
                EnsureBottomNavigationLayout(menuRoot, null, navBarSprite);
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
            CreateBottomTextButton("GoldShopNavButton", menuRoot, GoldShopLabel, "G", new Vector2(-432f, navY), OpenGoldShopMenu);
            CreateHomeSpriteButton("GachaButton", menuRoot, gachaSprite, "召喚", new Vector2(-216f, navY), HomeBottomNavButtonSize, OpenGachaMenu, 24);
            CreateHomeSpriteButton("MonsterDexButton", menuRoot, dexSprite, "図鑑", new Vector2(0f, navY), HomeBottomNavButtonSize, OpenMonsterDexMenu, 24);
            CreateHomeSpriteButton("EquipmentButton", menuRoot, equipmentSprite, "装備", new Vector2(216f, navY), HomeBottomNavButtonSize, OpenEquipmentMenu, 24);
            CreateHomeSpriteButton("FusionButton", menuRoot, fusionSprite, "合成", new Vector2(432f, navY), HomeBottomNavButtonSize, OpenFusionMenu, 24);
        }

        private void CreateSegmentedNavButton(
            string name,
            Transform parent,
            string label,
            int index,
            UnityEngine.Events.UnityAction action,
            Sprite navPartSprite,
            Sprite navBarSprite = null)
        {
            Button button = CreatePlainButton(
                name,
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(GetBottomNavButtonX(index), HomeBottomNavCenterY),
                HomeBottomNavButtonHitSize,
                new Color(1f, 1f, 1f, 0.001f),
                action);
            button.targetGraphic.raycastTarget = true;
            EnsureBottomNavSegmentVisual(button.transform, navPartSprite, navBarSprite, index);

            Text labelText = CreateUiText(
                name + "Label",
                button.transform,
                label,
                26,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                GetBottomNavLabelPosition(index),
                HomeBottomNavLabelSize,
                Color.white,
                TextAnchor.MiddleCenter);
            ConfigureBottomNavLabel(labelText);
            AddTextShadow(labelText, new Color(0f, 0f, 0f, 0.82f), new Vector2(0f, -2f));
        }

        private static float GetBottomNavButtonX(int index)
        {
            return HomeBottomNavCenterOffsetX - HomeBottomNavVisualWidth * 0.5f + HomeBottomNavSlotWidth * (index + 0.5f);
        }

        private static Vector2 GetBottomNavLabelPosition(int index)
        {
            if (index >= 0 && index < HomeBottomNavLabelPositions.Length)
            {
                return HomeBottomNavLabelPositions[index];
            }

            return HomeBottomNavLabelPosition;
        }

        private static Sprite[] LoadBottomNavPartSprites()
        {
            Sprite[] sprites = new Sprite[HomeBottomNavPartPaths.Length];
            for (int i = 0; i < HomeBottomNavPartPaths.Length; i += 1)
            {
                Sprite sprite = LoadSpriteResource(HomeBottomNavPartPaths[i], HomeBottomNavButtonNames[i] + "Art");
                if (sprite == null)
                {
                    return null;
                }

                sprites[i] = sprite;
            }

            return sprites;
        }

        private static Sprite GetBottomNavPartSprite(Sprite[] navPartSprites, int index)
        {
            if (navPartSprites == null || index < 0 || index >= navPartSprites.Length)
            {
                return null;
            }

            return navPartSprites[index];
        }

        private static void EnsureBottomNavigationLayout(Transform menuRoot, Sprite[] navPartSprites = null, Sprite navBarSprite = null)
        {
            if (menuRoot == null)
            {
                return;
            }

            navPartSprites ??= LoadBottomNavPartSprites();
            if (navPartSprites == null)
            {
                navBarSprite ??= LoadSpriteResource(HomeBottomNavBarPath, "HomeBottomNavBar");
            }

            Transform artTransform = menuRoot.Find("HomeBottomNavBarArt");
            if (artTransform != null)
            {
                DestroySceneObject(artTransform.gameObject);
            }

            for (int i = 0; i < HomeBottomNavButtonNames.Length; i += 1)
            {
                string buttonName = HomeBottomNavButtonNames[i];
                Transform buttonTransform = menuRoot.Find(buttonName);
                if (buttonTransform == null)
                {
                    continue;
                }

                RectTransform buttonRect = buttonTransform as RectTransform;
                if (buttonRect != null)
                {
                    ConfigureBottomAnchoredRect(
                        buttonRect,
                        new Vector2(GetBottomNavButtonX(i), HomeBottomNavCenterY),
                        HomeBottomNavButtonHitSize);
                }

                EnsureBottomNavSegmentVisual(buttonTransform, GetBottomNavPartSprite(navPartSprites, i), navBarSprite, i);

                Transform labelTransform = buttonTransform.Find(buttonName + "Label");
                Text labelText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
                if (labelText == null)
                {
                    continue;
                }

                RectTransform labelRect = labelText.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0.5f, 0f);
                    labelRect.anchorMax = new Vector2(0.5f, 0f);
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = GetBottomNavLabelPosition(i);
                    labelRect.sizeDelta = HomeBottomNavLabelSize;
                }

                ConfigureBottomNavLabel(labelText);
            }
        }

        private static void EnsureBottomNavSegmentVisual(Transform buttonTransform, Sprite navPartSprite, Sprite navBarSprite, int index)
        {
            if (buttonTransform == null)
            {
                return;
            }

            if (navPartSprite != null)
            {
                EnsureBottomNavSpriteVisual(buttonTransform, navPartSprite, index);
                return;
            }

            if (navBarSprite == null || navBarSprite.texture == null)
            {
                return;
            }

            Transform existing = buttonTransform.Find("BottomNavSegmentVisual");
            RawImage segmentImage = existing != null ? existing.GetComponent<RawImage>() : null;
            if (segmentImage == null)
            {
                if (existing != null)
                {
                    DestroySceneObject(existing.gameObject);
                }

                segmentImage = CreateMenuRawImage(
                    "BottomNavSegmentVisual",
                    buttonTransform,
                    navBarSprite.texture,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    HomeBottomNavSegmentSize);
                segmentImage.transform.SetAsFirstSibling();
            }

            RectTransform segmentRect = segmentImage.transform as RectTransform;
            if (segmentRect != null)
            {
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                segmentRect.anchoredPosition = Vector2.zero;
                segmentRect.sizeDelta = HomeBottomNavSegmentSize;
            }

            segmentImage.texture = navBarSprite.texture;
            segmentImage.uvRect = BuildBottomNavSegmentUv(navBarSprite, index);
            segmentImage.color = Color.white;
            segmentImage.raycastTarget = false;
            segmentImage.transform.SetAsFirstSibling();
        }

        private static void EnsureBottomNavSpriteVisual(Transform buttonTransform, Sprite navPartSprite, int index)
        {
            Transform existing = buttonTransform.Find("BottomNavSegmentVisual");
            Image segmentImage = existing != null ? existing.GetComponent<Image>() : null;
            if (segmentImage == null)
            {
                if (existing != null)
                {
                    DestroySceneObject(existing.gameObject);
                }

                segmentImage = CreateMenuImage(
                    "BottomNavSegmentVisual",
                    buttonTransform,
                    navPartSprite,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    HomeBottomNavSegmentSize,
                    false);
                segmentImage.transform.SetAsFirstSibling();
            }

            RectTransform segmentRect = segmentImage.transform as RectTransform;
            if (segmentRect != null)
            {
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                segmentRect.anchoredPosition = Vector2.zero;
                segmentRect.sizeDelta = HomeBottomNavSegmentSize;
            }

            segmentImage.sprite = navPartSprite;
            segmentImage.color = Color.white;
            segmentImage.preserveAspect = false;
            segmentImage.raycastTarget = false;
            segmentImage.transform.SetAsFirstSibling();
        }

        private static Rect BuildBottomNavSegmentUv(Sprite navBarSprite, int index)
        {
            Texture2D texture = navBarSprite.texture;
            Rect spriteRect = navBarSprite.rect;
            float segmentWidth = spriteRect.width / 5f;
            return new Rect(
                (spriteRect.x + segmentWidth * Mathf.Clamp(index, 0, 4)) / texture.width,
                spriteRect.y / texture.height,
                segmentWidth / texture.width,
                spriteRect.height / texture.height);
        }

        private static void ConfigureBottomAnchoredRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void ConfigureBottomNavLabel(Text labelText)
        {
            if (labelText == null)
            {
                return;
            }

            labelText.alignByGeometry = true;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 22;
            labelText.resizeTextMaxSize = 26;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.lineSpacing = 1f;
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

        private void CreatePlayerBadge(
            Transform parent,
            bool useGeneratedHudFrame,
            Sprite frameSprite = null,
            Vector2? anchoredPosition = null,
            Vector2? size = null)
        {
            GameObject root = new GameObject("PlayerBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition ?? new Vector2(-420f, -68f);
            rootRect.sizeDelta = size ?? new Vector2(214f, 70f);

            Image background = root.GetComponent<Image>();
            background.sprite = frameSprite;
            background.preserveAspect = false;
            background.color = frameSprite != null
                ? Color.white
                : useGeneratedHudFrame
                ? new Color(1f, 1f, 1f, 0.001f)
                : new Color(0.04f, 0.035f, 0.07f, 0.96f);
            background.raycastTarget = true;

            homePlayerLevelText = CreateUiText(
                "PlayerLevel",
                root.transform,
                "Lv.-",
                22,
                FontStyle.Bold,
                HomePlayerLevelTextAnchor,
                HomePlayerLevelTextAnchor,
                HomePlayerLevelTextOffset,
                HomePlayerLevelTextSize,
                new Color(1f, 0.86f, 0.42f, 1f),
                TextAnchor.MiddleCenter);
            homeExpText = CreatePlayerBadgeExpText(root.transform);
            ConfigurePlayerBadgeLevelAndExp(root.transform, homePlayerLevelText, homeExpText);
            EnsurePlayerBadgeInteraction(root.transform);
        }

        private static Text CreatePlayerBadgeExpText(Transform badgeTransform)
        {
            if (badgeTransform == null)
            {
                return null;
            }

            return CreateUiText(
                "PlayerExp",
                badgeTransform,
                "EXP -/-",
                16,
                FontStyle.Bold,
                HomePlayerExpTextAnchor,
                HomePlayerExpTextAnchor,
                HomePlayerExpTextOffset,
                HomePlayerExpTextSize,
                new Color(1f, 0.90f, 0.54f, 1f),
                TextAnchor.MiddleCenter);
        }

        private static void ConfigurePlayerBadgeLevelAndExp(Transform badgeTransform, Text levelText, Text expText)
        {
            if (badgeTransform == null)
            {
                return;
            }

            Transform nameTransform = badgeTransform.Find("PlayerName");
            if (nameTransform != null)
            {
                DestroySceneObject(nameTransform.gameObject);
            }

            RemoveDirectChild(badgeTransform, "PlayerPortrait");

            if (levelText == null)
            {
                return;
            }

            RectTransform levelRect = levelText.transform as RectTransform;
            if (levelRect != null)
            {
                levelRect.anchorMin = HomePlayerLevelTextAnchor;
                levelRect.anchorMax = HomePlayerLevelTextAnchor;
                levelRect.pivot = new Vector2(0.5f, 0.5f);
                levelRect.anchoredPosition = HomePlayerLevelTextOffset;
                levelRect.sizeDelta = HomePlayerLevelTextSize;
            }

            levelText.fontSize = 22;
            levelText.fontStyle = FontStyle.Bold;
            levelText.alignment = TextAnchor.MiddleCenter;
            levelText.alignByGeometry = true;
            levelText.resizeTextForBestFit = true;
            levelText.resizeTextMinSize = 16;
            levelText.resizeTextMaxSize = 24;
            levelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            levelText.verticalOverflow = VerticalWrapMode.Truncate;

            if (expText == null)
            {
                return;
            }

            RectTransform expRect = expText.transform as RectTransform;
            if (expRect != null)
            {
                expRect.anchorMin = HomePlayerExpTextAnchor;
                expRect.anchorMax = HomePlayerExpTextAnchor;
                expRect.pivot = new Vector2(0.5f, 0.5f);
                expRect.anchoredPosition = HomePlayerExpTextOffset;
                expRect.sizeDelta = HomePlayerExpTextSize;
            }

            expText.fontSize = 16;
            expText.fontStyle = FontStyle.Bold;
            expText.alignment = TextAnchor.MiddleCenter;
            expText.alignByGeometry = true;
            expText.resizeTextForBestFit = true;
            expText.resizeTextMinSize = 12;
            expText.resizeTextMaxSize = 17;
            expText.horizontalOverflow = HorizontalWrapMode.Overflow;
            expText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void EnsurePlayerBadgeInteraction(Transform badgeTransform)
        {
            if (badgeTransform == null)
            {
                return;
            }

            Image background = badgeTransform.GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = true;
            }

            Button button = badgeTransform.GetComponent<Button>();
            if (button == null)
            {
                button = badgeTransform.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(TogglePlayerExpDetails);
        }

        private void TogglePlayerExpDetails()
        {
            homePlayerExpDetailsVisible = !homePlayerExpDetailsVisible;
            RefreshPlayerBadgeDisplay(GetRuntimeProfile());
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
            return CreateHomeResourcePill(parent, rootName, amountName, iconPath, fallbackIcon, anchoredPosition, size, accentColor, true, null);
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
            bool showIcon,
            Sprite frameSprite = null)
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
            bool useFrameSprite = frameSprite != null;
            bool shouldDrawGeneratedIcon = showIcon && !useFrameSprite;
            background.sprite = frameSprite;
            background.preserveAspect = false;
            background.color = useFrameSprite
                ? Color.white
                : showIcon
                ? new Color(0.018f, 0.018f, 0.030f, 0.96f)
                : new Color(1f, 1f, 1f, 0.001f);
            background.raycastTarget = false;

            if (shouldDrawGeneratedIcon)
            {
                Outline outline = root.AddComponent<Outline>();
                outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.52f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            Sprite iconSprite = !string.IsNullOrEmpty(iconPath) ? Resources.Load<Sprite>(iconPath) : null;
            if (shouldDrawGeneratedIcon && iconSprite != null)
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
            else if (shouldDrawGeneratedIcon && !string.IsNullOrEmpty(fallbackIcon))
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

            Text amountText = CreateUiText(
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
            ConfigureHomeResourceAmountText(amountText);
            return amountText;
        }

        private static void ConfigureHomeResourceAmountText(Text amountText)
        {
            if (amountText == null)
            {
                return;
            }

            RectTransform amountRect = amountText.transform as RectTransform;
            RectTransform parentRect = amountText.transform.parent as RectTransform;
            if (amountRect != null)
            {
                float rightPadding = ResolveHomeResourceAmountRightPadding(amountText);
                float parentWidth = 0f;
                if (parentRect != null)
                {
                    parentWidth = Mathf.Abs(parentRect.rect.width);
                    if (parentWidth <= 0f)
                    {
                        parentWidth = Mathf.Abs(parentRect.sizeDelta.x);
                    }
                }

                float textWidth = parentWidth > 0f
                    ? Mathf.Max(90f, parentWidth - HomeResourceAmountLeftReserve - rightPadding)
                    : Mathf.Max(90f, amountRect.sizeDelta.x);
                amountRect.anchorMin = new Vector2(1f, 0.5f);
                amountRect.anchorMax = new Vector2(1f, 0.5f);
                amountRect.pivot = new Vector2(1f, 0.5f);
                amountRect.anchoredPosition = new Vector2(-rightPadding, HomeResourceAmountVerticalOffset);
                amountRect.sizeDelta = new Vector2(textWidth, 38f);
            }

            amountText.alignment = TextAnchor.MiddleRight;
            amountText.alignByGeometry = true;
            amountText.resizeTextForBestFit = true;
            amountText.resizeTextMinSize = 16;
            amountText.resizeTextMaxSize = 22;
            amountText.horizontalOverflow = HorizontalWrapMode.Overflow;
            amountText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static float ResolveHomeResourceAmountRightPadding(Text amountText)
        {
            if (amountText == null)
            {
                return HomeResourceAmountRightPadding;
            }

            string amountName = amountText.gameObject.name;
            return amountName == "PaidStoneAmount"
                ? HomeResourceAmountNarrowRightPadding
                : HomeResourceAmountRightPadding;
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

            EnsureHomeGuidePanel(menuRoot);

            Transform existingBar = menuRoot.Find("HomeStoneBalanceBar");
            if (existingBar != null)
            {
                bool isModernLayout = existingBar.Find("HomeHudLayoutMarker") != null &&
                    existingBar.Find("HomeHudSplitLayoutMarker") != null;
                homeFreeStoneText = existingBar.Find("FreeStoneCounter/FreeStoneAmount")?.GetComponent<Text>();
                homePaidStoneText = existingBar.Find("PaidStoneCounter/PaidStoneAmount")?.GetComponent<Text>();
                homeGoldText = existingBar.Find("GoldCounter/GoldAmount")?.GetComponent<Text>();
                homePlayerLevelText = existingBar.Find("PlayerBadge/PlayerLevel")?.GetComponent<Text>();
                Transform playerBadge = existingBar.Find("PlayerBadge");
                homeExpText = playerBadge != null ? playerBadge.Find("PlayerExp")?.GetComponent<Text>() : null;
                if (playerBadge != null && homeExpText == null)
                {
                    homeExpText = CreatePlayerBadgeExpText(playerBadge);
                }
                ConfigurePlayerBadgeLevelAndExp(playerBadge, homePlayerLevelText, homeExpText);
                EnsurePlayerBadgeInteraction(playerBadge);
                RemoveDirectChild(existingBar, "ExpCounter");
                homeGuideText = menuRoot.Find("HomeGuidePanel/HomeGuideText")?.GetComponent<Text>();
                homeNextFloorText = menuRoot.Find("HomeGuidePanel/HomeNextFloorText")?.GetComponent<Text>();
                homeHeroImage = menuRoot.Find("HomeHeroShowcase/HomeHeroImage")?.GetComponent<Image>();
                homeHeroNameText = menuRoot.Find("HomeHeroShowcase/HomeHeroName")?.GetComponent<Text>();
                homeHeroLevelText = menuRoot.Find("HomeHeroShowcase/HomeHeroLevel")?.GetComponent<Text>();
                HideHomeHeroLabels();
                homeQuestButtonText = existingBar.Find("QuestButton/QuestButtonLabel")?.GetComponent<Text>();
                homeQuestButton = existingBar.Find("QuestButton")?.GetComponent<Button>();
                homeShopButtonText = existingBar.Find("GoldShopButton/GoldShopButtonLabel")?.GetComponent<Text>();
                homeShopButton = existingBar.Find("GoldShopButton")?.GetComponent<Button>();
                if (homeShopButton != null)
                {
                    homeShopButton.onClick.RemoveAllListeners();
                    homeShopButton.onClick.AddListener(OpenPaidShopMenu);
                }
                ConfigureGoldShopButtonVisual(homeShopButton != null ? homeShopButton.transform : null, homeShopButtonText);
                EnsurePermanentUpgradeShortcutButton(existingBar);
                ConfigureHomeResourceAmountText(homeFreeStoneText);
                ConfigureHomeResourceAmountText(homePaidStoneText);
                ConfigureHomeResourceAmountText(homeGoldText);
                AlignHomeResourceAmountYToPaidStone();
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
                    homeQuestButtonText != null &&
                    homeQuestButton != null &&
                    homeShopButtonText != null &&
                    homeShopButton != null &&
                    permanentUpgradeButtonText != null &&
                    permanentUpgradeStatusText != null &&
                    permanentUpgradeButton != null &&
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

            Sprite profileHudFrameSprite = LoadSpriteResource(HomeTopHudProfileFramePath, "HomeTopHudProfile");
            Sprite goldHudFrameSprite = LoadSpriteResource(HomeTopHudGoldFramePath, "HomeTopHudGold");
            Sprite freeStoneHudFrameSprite = LoadSpriteResource(HomeTopHudFreeStoneFramePath, "HomeTopHudFreeStone");
            Sprite paidStoneHudFrameSprite = LoadSpriteResource(HomeTopHudPaidStoneFramePath, "HomeTopHudPaidStone");
            bool useSplitHudFrame = profileHudFrameSprite != null &&
                goldHudFrameSprite != null &&
                freeStoneHudFrameSprite != null &&
                paidStoneHudFrameSprite != null;
            Sprite hudFrameSprite = useSplitHudFrame ? null : LoadSpriteResource(HomeTopHudFramePath, "HomeTopHudFrame");
            bool useGeneratedHudFrame = useSplitHudFrame || hudFrameSprite != null;
            if (!useSplitHudFrame && useGeneratedHudFrame)
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
            GameObject splitMarker = new GameObject("HomeHudSplitLayoutMarker", typeof(RectTransform));
            splitMarker.transform.SetParent(bar.transform, false);

            CreatePlayerBadge(
                bar.transform,
                useGeneratedHudFrame,
                useSplitHudFrame ? profileHudFrameSprite : null,
                useSplitHudFrame ? HomeTopHudProfilePosition : (Vector2?)null,
                useSplitHudFrame ? HomeTopHudProfileSize : (Vector2?)null);
            homeGoldText = CreateHomeResourcePill(
                bar.transform,
                "GoldCounter",
                "GoldAmount",
                null,
                "G",
                useSplitHudFrame ? HomeTopHudGoldPosition : new Vector2(-198f, -68f),
                useSplitHudFrame ? HomeTopHudGoldSize : new Vector2(180f, 58f),
                new Color(1f, 0.74f, 0.22f, 1f),
                !useGeneratedHudFrame,
                useSplitHudFrame ? goldHudFrameSprite : null);
            homeFreeStoneText = CreateHomeResourcePill(
                bar.transform,
                "FreeStoneCounter",
                "FreeStoneAmount",
                FreeStoneIconPath,
                string.Empty,
                useSplitHudFrame ? HomeTopHudFreeStonePosition : new Vector2(12f, -68f),
                useSplitHudFrame ? HomeTopHudFreeStoneSize : new Vector2(204f, 58f),
                new Color(0.54f, 0.94f, 1f, 1f),
                !useGeneratedHudFrame,
                useSplitHudFrame ? freeStoneHudFrameSprite : null);
            homePaidStoneText = CreateHomeResourcePill(
                bar.transform,
                "PaidStoneCounter",
                "PaidStoneAmount",
                PaidStoneIconPath,
                string.Empty,
                useSplitHudFrame ? HomeTopHudPaidStonePosition : new Vector2(238f, -68f),
                useSplitHudFrame ? HomeTopHudPaidStoneSize : new Vector2(204f, 58f),
                new Color(1f, 0.54f, 1f, 1f),
                !useGeneratedHudFrame,
                useSplitHudFrame ? paidStoneHudFrameSprite : null);
            AlignHomeResourceAmountYToPaidStone();
            homeShopButton = CreateGoldShopButton(bar.transform, HomeShopButtonPosition, HomeShopButtonSize);
            permanentUpgradeButton = CreatePermanentUpgradeShortcutButton(bar.transform, PermanentUpgradeButtonPosition, PermanentUpgradeButtonSize);
            homeQuestButton = CreateQuestButton(bar.transform, HomeQuestButtonPosition, HomeQuestButtonSize);
            EnsureDailyQuestList(menuRoot);
        }

        private void AlignHomeResourceAmountYToPaidStone()
        {
            RectTransform paidRect = homePaidStoneText != null ? homePaidStoneText.transform as RectTransform : null;
            if (paidRect == null)
            {
                return;
            }

            float targetY = paidRect.anchoredPosition.y;
            AlignHomeResourceAmountY(homeGoldText, targetY + HomeYellowResourceAmountVerticalOffset);
            AlignHomeResourceAmountY(homeFreeStoneText, targetY);
        }

        private static void AlignHomeResourceAmountY(Text amountText, float targetY)
        {
            RectTransform rectTransform = amountText != null ? amountText.transform as RectTransform : null;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, targetY);
        }

        private void RefreshHomeStoneBalanceBar()
        {
            Transform existingMenuRoot = ResolveUnifiedMenuRootTransform();
            Transform existingStoneBar = existingMenuRoot != null ? existingMenuRoot.Find("HomeStoneBalanceBar") : null;
            if (existingStoneBar != null)
            {
                homeShopButtonText = homeShopButtonText != null
                    ? homeShopButtonText
                    : existingStoneBar.Find("GoldShopButton/GoldShopButtonLabel")?.GetComponent<Text>();
                homeShopButton = homeShopButton != null
                    ? homeShopButton
                    : existingStoneBar.Find("GoldShopButton")?.GetComponent<Button>();
                EnsurePermanentUpgradeShortcutButton(existingStoneBar);
            }

            if (homeFreeStoneText == null &&
                homePaidStoneText == null &&
                homeGoldText == null &&
                homeExpText == null &&
                homePlayerLevelText == null &&
                homeGuideText == null &&
                homeQuestButtonText == null &&
                homeShopButtonText == null &&
                permanentUpgradeButtonText == null &&
                permanentUpgradeStatusText == null)
            {
                if (existingMenuRoot == null)
                {
                    return;
                }

                EnsureHomeStoneBalanceBar(existingMenuRoot);
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

                RefreshPlayerBadgeDisplay(null);

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
                    homeShopButtonText.text = PaidShopLabel;
                }

                RefreshPermanentUpgradeShortcutDisplay(null);

                if (homeQuestButton != null)
                {
                    homeQuestButton.interactable = false;
                }

                if (homeShopButton != null)
                {
                    homeShopButton.interactable = false;
                }

                RefreshHomeHeroShowcase(profile);
                ApplyHomeTutorialFocus(null);
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

            RefreshPlayerBadgeDisplay(profile);

            if (homeGuideText != null)
            {
                homeGuideText.text = BuildHomeGuideText(profile);
            }

            if (homeNextFloorText != null)
            {
                homeNextFloorText.text = "次の探索: " + BuildNextDungeonLabel(Mathf.Max(1, profile.HighestFloor + 1));
            }

            if (homeQuestButtonText != null)
            {
                homeQuestButtonText.text = DailyRewardService.HasClaimableQuest(profile, DateTime.Now) ? "クエスト!" : "クエスト";
            }

            if (homeShopButtonText != null)
            {
                homeShopButtonText.text = PaidShopLabel;
            }

            RefreshPermanentUpgradeShortcutDisplay(profile);

            if (homeQuestButton != null)
            {
                homeQuestButton.interactable = true;
            }

            if (homeShopButton != null)
            {
                homeShopButton.interactable = true;
            }

            RefreshHomeHeroShowcase(profile);
            ApplyHomeTutorialFocus(profile);
        }

        private Transform ResolveUnifiedMenuRootTransform()
        {
            if (unifiedMenuRoot != null)
            {
                return unifiedMenuRoot.transform;
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                Transform existingMenu = canvas.transform.Find("UnifiedHomeMenu");
                if (existingMenu == null)
                {
                    continue;
                }

                unifiedMenuRoot = existingMenu.gameObject;
                return existingMenu;
            }

            return null;
        }

        private void RefreshPlayerBadgeDisplay(PlayerProfile profile)
        {
            if (homePlayerLevelText != null)
            {
                homePlayerLevelText.text = profile != null
                    ? $"Lv.{Mathf.Max(1, profile.Level)}"
                    : "Lv.-";
                homePlayerLevelText.gameObject.SetActive(!homePlayerExpDetailsVisible);
            }

            if (homeExpText != null)
            {
                homeExpText.text = profile != null
                    ? $"EXP {Mathf.Max(0, profile.Exp):N0}/{Mathf.Max(1, profile.GetRequiredExpForNextLevel()):N0}"
                    : "EXP -/-";
                homeExpText.gameObject.SetActive(homePlayerExpDetailsVisible);
            }
        }

        private void RefreshHomeHeroShowcase(PlayerProfile profile)
        {
            Sprite sprite = ResolveHomeHeroSprite(profile);
            if (homeHeroImage != null)
            {
                homeHeroImage.sprite = sprite;
                homeHeroImage.color = sprite != null ? Color.white : new Color(0.18f, 0.18f, 0.24f, 0.70f);
                homeHeroImage.preserveAspect = true;
            }

            HideHomeHeroLabels();
        }

        private void HideHomeHeroLabels()
        {
            if (homeHeroNameText != null)
            {
                homeHeroNameText.text = string.Empty;
                homeHeroNameText.gameObject.SetActive(false);
            }

            if (homeHeroLevelText != null)
            {
                homeHeroLevelText.text = string.Empty;
                homeHeroLevelText.gameObject.SetActive(false);
            }
        }

        private static Sprite ResolveHomeHeroSprite(PlayerProfile profile)
        {
            OwnedMonsterData leadMonster = ResolveLeadMonster(profile);
            MonsterDataSO monsterData = null;
            if (leadMonster != null && MasterDataManager.Instance != null)
            {
                monsterData = MasterDataManager.Instance.GetMonsterData(leadMonster.MonsterId);
            }

            Sprite sprite = monsterData != null ? monsterData.illustrationSprite : null;
            if (monsterData != null && monsterData.monsterId == RockGolemMonsterId)
            {
                sprite = Resources.Load<Sprite>(RockGolemHomeHeroSpritePath);
            }

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

        private bool TryAdvanceHomeTutorialForTarget(string targetKey)
        {
            PlayerProfile profile = GetRuntimeProfile();
            if (profile == null)
            {
                return true;
            }

            bool changed = false;
            StoryTutorialEvent activeEvent = StoryTutorialService.GetNextEvent(profile, "HomeScene");
            if (!profile.HasCompletedTutorial)
            {
                if (profile.TutorialStepId == StoryTutorialService.StepWakeup)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryPrologueWakeup);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWakeup);
                    SaveHomeTutorialProgressIfNeeded(changed);
                    return false;
                }

                if (activeEvent != null &&
                    activeEvent.BlocksInput &&
                    !string.IsNullOrEmpty(activeEvent.TargetKey) &&
                    !string.Equals(activeEvent.TargetKey, targetKey, StringComparison.Ordinal))
                {
                    RefreshHomeStoneBalanceBar();
                    return false;
                }

                if (targetKey == "home.gacha" &&
                    profile.TutorialStepId == StoryTutorialService.StepOpenGacha)
                {
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepOpenGacha);
                }
                else if (targetKey == "home.formation" &&
                         profile.TutorialStepId == StoryTutorialService.StepOpenFormation)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryFirstSummonDone);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepOpenFormation);
                }
                else if (targetKey == "home.battle" &&
                         profile.TutorialStepId == StoryTutorialService.StepOpenBattle)
                {
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepOpenBattle);
                }

                if (profile.TutorialStepId == StoryTutorialService.StepWrapUp)
                {
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWrapUp);
                    SaveHomeTutorialProgressIfNeeded(changed);
                    return false;
                }
            }
            else
            {
                if (activeEvent != null &&
                    StoryTutorialService.IsChapterStoryEvent(activeEvent.EventId) &&
                    activeEvent.TargetKey == targetKey)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, activeEvent.EventId);
                }
                else if (activeEvent != null && IsHomeHintTarget(activeEvent, targetKey))
                {
                    changed |= StoryTutorialService.MarkHintSeen(profile, activeEvent.EventId);
                }
            }

            SaveHomeTutorialProgressIfNeeded(changed);
            return true;
        }

        private void AdvanceHomeGuidePanel()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerProfile profile = GetRuntimeProfile();
            StoryTutorialEvent activeEvent = StoryTutorialService.GetNextEvent(profile, "HomeScene");
            if (profile == null || activeEvent == null || !activeEvent.IsValid)
            {
                return;
            }

            bool changed = false;
            if (!profile.HasCompletedTutorial)
            {
                if (profile.TutorialStepId == StoryTutorialService.StepWakeup)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryPrologueWakeup);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWakeup);
                }
                else if (profile.TutorialStepId == StoryTutorialService.StepWrapUp)
                {
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWrapUp);
                }
            }
            else if (StoryTutorialService.IsChapterStoryEvent(activeEvent.EventId))
            {
                changed |= StoryTutorialService.MarkStorySeen(profile, activeEvent.EventId);
            }
            else if (string.IsNullOrEmpty(activeEvent.TargetKey))
            {
                changed |= StoryTutorialService.MarkHintSeen(profile, activeEvent.EventId);
            }

            SaveHomeTutorialProgressIfNeeded(changed);
        }

        private void SaveHomeTutorialProgressIfNeeded(bool changed)
        {
            if (!changed)
            {
                ApplyHomeTutorialFocus(GetRuntimeProfile());
                return;
            }

            SaveManager.Instance?.SaveCurrentGame();
            RefreshHomeStoneBalanceBar();
        }

        private void ApplyHomeTutorialFocus(PlayerProfile profile)
        {
            Transform menuRoot = ResolveUnifiedMenuRootTransform();
            StoryTutorialEvent activeEvent = profile != null
                ? StoryTutorialService.GetNextEvent(profile, "HomeScene")
                : null;
            bool guideCanAdvance = CanAdvanceHomeGuidePanel(profile, activeEvent);
            if (homeGuideButton != null)
            {
                homeGuideButton.interactable = guideCanAdvance;
            }

            if (menuRoot == null)
            {
                HideHomeTutorialFocus();
                return;
            }

            bool blocksInput = profile != null &&
                !profile.HasCompletedTutorial &&
                activeEvent != null &&
                activeEvent.BlocksInput;
            string targetButtonName = ResolveHomeTutorialButtonName(activeEvent?.TargetKey ?? string.Empty);
            SetHomeTutorialActionButtonsInteractable(menuRoot, !blocksInput, targetButtonName);

            if (activeEvent == null || !activeEvent.IsValid)
            {
                HideHomeTutorialFocus();
                return;
            }

            Transform focusTarget = null;
            string focusLabel = string.Empty;
            if (!string.IsNullOrEmpty(targetButtonName))
            {
                focusTarget = FindDescendant(menuRoot, targetButtonName);
                focusLabel = "ここをタップ";
            }
            else if (guideCanAdvance)
            {
                focusTarget = homeGuideButton != null ? homeGuideButton.transform : menuRoot.Find("HomeGuidePanel");
                focusLabel = "タップして続ける";
            }

            if (focusTarget == null)
            {
                HideHomeTutorialFocus();
                return;
            }

            ShowHomeTutorialFocus(menuRoot, focusTarget, focusLabel);
        }

        private static bool CanAdvanceHomeGuidePanel(PlayerProfile profile, StoryTutorialEvent activeEvent)
        {
            if (profile == null || activeEvent == null || !activeEvent.IsValid)
            {
                return false;
            }

            if (!profile.HasCompletedTutorial)
            {
                return profile.TutorialStepId == StoryTutorialService.StepWakeup ||
                    profile.TutorialStepId == StoryTutorialService.StepWrapUp;
            }

            return StoryTutorialService.IsChapterStoryEvent(activeEvent.EventId) ||
                string.IsNullOrEmpty(activeEvent.TargetKey);
        }

        private static void SetHomeTutorialActionButtonsInteractable(Transform menuRoot, bool allInteractable, string targetButtonName)
        {
            for (int i = 0; i < HomeTutorialActionButtonNames.Length; i += 1)
            {
                string buttonName = HomeTutorialActionButtonNames[i];
                Transform buttonTransform = FindDescendant(menuRoot, buttonName);
                Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                if (button == null)
                {
                    continue;
                }

                button.interactable = allInteractable ||
                    string.Equals(buttonName, targetButtonName, StringComparison.Ordinal);
            }
        }

        private static string ResolveHomeTutorialButtonName(string targetKey)
        {
            switch (targetKey)
            {
                case "home.battle":
                    return "BattleButton";
                case "home.formation":
                    return "FormationButton";
                case "home.gacha":
                    return "GachaButton";
                case "home.equipment":
                    return "EquipmentButton";
                case "home.fusion":
                    return "FusionButton";
                case "home.dex":
                    return "MonsterDexButton";
                case "home.shop":
                    return "GoldShopNavButton";
                default:
                    return string.Empty;
            }
        }

        private void ShowHomeTutorialFocus(Transform menuRoot, Transform targetTransform, string label)
        {
            EnsureHomeTutorialFocus(menuRoot);
            if (homeTutorialFocusRoot == null)
            {
                return;
            }

            RectTransform focusRect = homeTutorialFocusRoot.transform as RectTransform;
            if (focusRect == null)
            {
                return;
            }

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(menuRoot, targetTransform);
            focusRect.anchorMin = new Vector2(0.5f, 0f);
            focusRect.anchorMax = new Vector2(0.5f, 0f);
            focusRect.pivot = new Vector2(0.5f, 0.5f);
            focusRect.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);
            focusRect.sizeDelta = new Vector2(
                Mathf.Max(130f, bounds.size.x + 30f),
                Mathf.Max(84f, bounds.size.y + 30f));

            if (homeTutorialFocusText != null)
            {
                RectTransform textRect = homeTutorialFocusText.transform as RectTransform;
                if (textRect != null)
                {
                    textRect.anchoredPosition = new Vector2(0f, focusRect.sizeDelta.y * 0.5f + 30f);
                    textRect.sizeDelta = new Vector2(Mathf.Max(220f, focusRect.sizeDelta.x + 40f), 44f);
                }

                homeTutorialFocusText.text = label;
            }

            homeTutorialFocusRoot.SetActive(true);
            homeTutorialFocusRoot.transform.SetAsLastSibling();
        }

        private void EnsureHomeTutorialFocus(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            Transform existing = menuRoot.Find("HomeTutorialFocus");
            if (homeTutorialFocusRoot == null || homeTutorialFocusRoot.transform.parent != menuRoot)
            {
                homeTutorialFocusRoot = existing != null
                    ? existing.gameObject
                    : new GameObject("HomeTutorialFocus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            }

            if (homeTutorialFocusRoot.transform.parent != menuRoot)
            {
                homeTutorialFocusRoot.transform.SetParent(menuRoot, false);
            }

            Image image = homeTutorialFocusRoot.GetComponent<Image>();
            if (image == null)
            {
                image = homeTutorialFocusRoot.AddComponent<Image>();
            }

            image.color = new Color(1f, 0.86f, 0.30f, 0.001f);
            image.raycastTarget = false;

            Outline outline = homeTutorialFocusRoot.GetComponent<Outline>();
            if (outline == null)
            {
                outline = homeTutorialFocusRoot.AddComponent<Outline>();
            }

            outline.effectColor = new Color(1f, 0.82f, 0.32f, 0.95f);
            outline.effectDistance = new Vector2(5f, -5f);

            homeTutorialFocusText = homeTutorialFocusRoot.transform.Find("HomeTutorialFocusLabel")?.GetComponent<Text>();
            if (homeTutorialFocusText == null)
            {
                homeTutorialFocusText = CreateUiText(
                    "HomeTutorialFocusLabel",
                    homeTutorialFocusRoot.transform,
                    string.Empty,
                    24,
                    FontStyle.Bold,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(260f, 44f),
                    new Color(1f, 0.94f, 0.70f, 1f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(homeTutorialFocusText, new Color(0f, 0f, 0f, 0.92f), new Vector2(2f, -2f));
            }

            homeTutorialFocusText.raycastTarget = false;
        }

        private void HideHomeTutorialFocus()
        {
            if (homeTutorialFocusRoot != null)
            {
                homeTutorialFocusRoot.SetActive(false);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i += 1)
            {
                if (descendants[i] != null && descendants[i].name == objectName)
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private static bool IsHomeHintTarget(StoryTutorialEvent hint, string targetKey)
        {
            if (hint == null || string.IsNullOrEmpty(targetKey))
            {
                return false;
            }

            if (targetKey == "home.equipment")
            {
                // Equipment hints stay active until the player sees or uses the relevant control.
                return false;
            }

            if (targetKey == "home.fusion")
            {
                return hint.EventId == StoryTutorialService.HintFusion;
            }

            if (targetKey == "home.dex")
            {
                return hint.EventId == StoryTutorialService.HintDex;
            }

            if (targetKey == "home.shop")
            {
                return hint.EventId == StoryTutorialService.HintShop;
            }

            return false;
        }

        private static string BuildHomeGuideText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "よぉし！\n今日の冒険を始めましょう。";
            }

            StoryTutorialEvent tutorialEvent = StoryTutorialService.GetNextEvent(profile, "HomeScene");
            if (tutorialEvent != null && tutorialEvent.IsValid)
            {
                return BuildTutorialGuideText(tutorialEvent);
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
            string nextDungeonLabel = BuildNextDungeonLabel(nextFloor);
            if (profile.Gold >= 100)
            {
                return $"準備はいい感じ！\n{nextDungeonLabel}へ挑戦して、装備を集めましょう。";
            }

            return $"よぉし！\n{nextDungeonLabel}へ挑戦して、ゴールドを稼ぎましょう！";
        }

        private static string BuildTutorialGuideText(StoryTutorialEvent tutorialEvent)
        {
            if (tutorialEvent == null || !tutorialEvent.IsValid)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(tutorialEvent.Title)
                ? tutorialEvent.Body
                : $"{tutorialEvent.Title}\n{tutorialEvent.Body}";
        }

        private static string BuildNextDungeonLabel(int globalFloor)
        {
            int safeFloor = Mathf.Max(1, globalFloor);
            string dungeonName = BattleDungeonCatalog.ResolveDungeonName(safeFloor);
            int localFloor = BattleDungeonCatalog.ResolveLocalFloor(safeFloor);
            return string.IsNullOrEmpty(dungeonName)
                ? $"第{safeFloor}階層"
                : $"{dungeonName} 第{localFloor}階層";
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
                    dailyQuestStatusText != null &&
                    existingPanel.Find("DailyQuestDecorMarker") != null)
                {
                    ConfigureDailyQuestInputBlocking(existingPanel);
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

            GameObject decorMarker = new GameObject("DailyQuestDecorMarker", typeof(RectTransform));
            decorMarker.transform.SetParent(dailyQuestListRoot.transform, false);

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
            panelImage.color = new Color(0.014f, 0.018f, 0.024f, 0.97f);
            panelImage.raycastTarget = true;
            AddUiOutline(panel, new Color(0.82f, 0.64f, 0.28f, 0.56f), new Vector2(2f, -2f));
            EnsureDailyQuestInputBlocker(panel.transform);

            CreateTintPanel(
                "DailyQuestTopLine",
                panel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(560f, 3f),
                new Color(1f, 0.78f, 0.30f, 0.70f));
            CreateTintPanel(
                "DailyQuestBottomLine",
                panel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 110f),
                new Vector2(560f, 2f),
                new Color(0.25f, 0.83f, 1f, 0.34f));

            Text titleText = CreateUiText(
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
            AddTextShadow(titleText, new Color(0f, 0f, 0f, 0.86f), new Vector2(2f, -2f));

            Button closeButton = CreatePlainButton(
                "DailyQuestCloseButton",
                panel.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-62f, -54f),
                new Vector2(78f, 52f),
                new Color(0.24f, 0.10f, 0.09f, 0.96f),
                CloseDailyQuestList);
            AddUiOutline(closeButton.gameObject, new Color(1f, 0.70f, 0.45f, 0.34f), new Vector2(1f, -1f));
            CreateUiText(
                "DailyQuestCloseLabel",
                closeButton.transform,
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

        private static void ConfigureDailyQuestInputBlocking(Transform dailyQuestPanelRoot)
        {
            Transform panelBody = dailyQuestPanelRoot != null ? dailyQuestPanelRoot.Find("DailyQuestPanelBody") : null;
            Image panelImage = panelBody != null ? panelBody.GetComponent<Image>() : null;
            if (panelImage != null)
            {
                panelImage.raycastTarget = true;
            }

            EnsureDailyQuestInputBlocker(panelBody);
        }

        private static void EnsureDailyQuestInputBlocker(Transform panelBody)
        {
            RectTransform panelRect = panelBody as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            Transform existingBlocker = panelBody.Find("DailyQuestInputBlocker");
            if (existingBlocker == null)
            {
                Button blocker = CreatePlainButton(
                    "DailyQuestInputBlocker",
                    panelBody,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    panelRect.sizeDelta + new Vector2(8f, 8f),
                    new Color(0f, 0f, 0f, 0.001f),
                    null);
                blocker.transition = Selectable.Transition.None;
                existingBlocker = blocker.transform;
            }

            RectTransform blockerRect = existingBlocker as RectTransform;
            if (blockerRect != null)
            {
                blockerRect.anchorMin = new Vector2(0.5f, 0.5f);
                blockerRect.anchorMax = new Vector2(0.5f, 0.5f);
                blockerRect.pivot = new Vector2(0.5f, 0.5f);
                blockerRect.anchoredPosition = Vector2.zero;
                blockerRect.sizeDelta = panelRect.sizeDelta + new Vector2(8f, 8f);
            }

            Image blockerImage = existingBlocker.GetComponent<Image>();
            if (blockerImage != null)
            {
                blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
                blockerImage.raycastTarget = true;
            }

            existingBlocker.SetAsFirstSibling();
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
            cardImage.color = new Color(0.028f, 0.037f, 0.044f, 0.96f);
            cardImage.raycastTarget = false;
            AddUiOutline(card, new Color(0.56f, 0.78f, 0.82f, 0.20f), new Vector2(1f, -1f));
            CreateTintPanel(
                "DailyQuestCardAccent",
                card.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(8f, 0f),
                new Vector2(4f, 106f),
                new Color(1f, 0.74f, 0.24f, 0.78f));
            CreateTintPanel(
                "DailyQuestCardShine",
                card.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -2f),
                new Vector2(724f, 2f),
                new Color(1f, 1f, 1f, 0.10f));

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
            AddUiOutline(claimButton.gameObject, new Color(0.40f, 0.90f, 1f, 0.25f), new Vector2(1f, -1f));

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
            image.color = new Color(0.045f, 0.030f, 0.075f, 0.96f);
            image.raycastTarget = true;

            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.66f, 0.24f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenPaidShopMenu);

            homeShopButtonText = CreateUiText(
                "GoldShopButtonLabel",
                root.transform,
                PaidShopLabel,
                18,
                FontStyle.Bold,
                new Vector2(0.5f, 0.18f),
                new Vector2(0.5f, 0.18f),
                Vector2.zero,
                new Vector2(132f, 34f),
                new Color(1f, 0.92f, 0.68f, 1f),
                TextAnchor.MiddleCenter);
            ConfigureGoldShopButtonVisual(root.transform, homeShopButtonText);
            return button;
        }

        private static void ConfigureGoldShopButtonVisual(Transform buttonTransform, Text labelText)
        {
            if (buttonTransform == null)
            {
                return;
            }

            RectTransform buttonRect = buttonTransform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = HomeShopButtonPosition;
                buttonRect.sizeDelta = HomeShopButtonSize;
            }

            Sprite paidShopFrame = LoadSpriteResource(PaidShopButtonFrameSpritePath, "PaidShopButtonFrame");
            Image image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                if (paidShopFrame != null)
                {
                    image.sprite = paidShopFrame;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.color = Color.white;
                }
                else
                {
                    image.sprite = null;
                    image.color = new Color(0.045f, 0.030f, 0.075f, 0.96f);
                }

                image.raycastTarget = true;
            }

            Outline outline = buttonTransform.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonTransform.gameObject.AddComponent<Outline>();
            }

            outline.enabled = paidShopFrame == null;
            outline.effectColor = new Color(0.92f, 0.66f, 0.24f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            Transform topLabel = buttonTransform.Find("GoldShopTopLabel");
            if (topLabel != null)
            {
                DestroySceneObject(topLabel.gameObject);
            }

            Transform oldIcon = buttonTransform.Find("GoldShopIcon");
            if (oldIcon != null)
            {
                DestroySceneObject(oldIcon.gameObject);
            }

            Vector2 buttonSize = ResolveRectSize(buttonRect, HomeShopButtonSize);
            Vector2 paidShopIconSize = new Vector2(buttonSize.x * 0.75f, buttonSize.y * 0.62f);
            Sprite paidShopIcon = LoadSpriteResource(PaidShopIconSpritePath, "PaidShopIcon");
            if (paidShopIcon != null)
            {
                CreateMenuImage(
                    "GoldShopIcon",
                    buttonTransform,
                    paidShopIcon,
                    new Vector2(0.5f, 0.60f),
                    new Vector2(0.5f, 0.60f),
                    new Vector2(0f, -5f),
                    paidShopIconSize,
                    true);
            }

            if (labelText == null)
            {
                return;
            }

            RectTransform labelRect = labelText.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0.5f, 0.18f);
                labelRect.anchorMax = new Vector2(0.5f, 0.18f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(132f, 34f);
            }

            labelText.fontSize = 18;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 12;
            labelText.resizeTextMaxSize = 18;
            labelText.color = new Color(1f, 0.92f, 0.68f, 1f);
            labelText.text = PaidShopLabel;
        }

        private void EnsurePermanentUpgradeShortcutButton(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("PermanentUpgradeButton");
            if (existing == null)
            {
                permanentUpgradeButton = CreatePermanentUpgradeShortcutButton(parent, PermanentUpgradeButtonPosition, PermanentUpgradeButtonSize);
                return;
            }

            permanentUpgradeButton = existing.GetComponent<Button>();
            permanentUpgradeButtonText = existing.Find("PermanentUpgradeButtonLabel")?.GetComponent<Text>();
            permanentUpgradeStatusText = existing.Find("PermanentUpgradeStatus")?.GetComponent<Text>();
            if (permanentUpgradeButton == null ||
                permanentUpgradeButtonText == null ||
                permanentUpgradeStatusText == null)
            {
                DestroySceneObject(existing.gameObject);
                permanentUpgradeButton = CreatePermanentUpgradeShortcutButton(parent, PermanentUpgradeButtonPosition, PermanentUpgradeButtonSize);
                return;
            }

            RectTransform rectTransform = existing as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 1f);
                rectTransform.anchorMax = new Vector2(0.5f, 1f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = PermanentUpgradeButtonPosition;
                rectTransform.sizeDelta = PermanentUpgradeButtonSize;
            }

            permanentUpgradeButton.onClick.RemoveAllListeners();
            permanentUpgradeButton.onClick.AddListener(OpenPermanentUpgradeShop);
            ConfigurePermanentUpgradeShortcutVisual(existing, permanentUpgradeButtonText, permanentUpgradeStatusText);
        }

        private Button CreatePermanentUpgradeShortcutButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject root = new GameObject("PermanentUpgradeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            button.onClick.AddListener(OpenPermanentUpgradeShop);

            permanentUpgradeStatusText = CreateUiText(
                "PermanentUpgradeStatus",
                root.transform,
                string.Empty,
                14,
                FontStyle.Bold,
                new Vector2(0.5f, 0.42f),
                new Vector2(0.5f, 0.42f),
                Vector2.zero,
                new Vector2(164f, 38f),
                new Color(0.72f, 1f, 0.96f, 1f),
                TextAnchor.MiddleCenter);
            AddTextShadow(permanentUpgradeStatusText, new Color(0f, 0f, 0f, 0.88f), new Vector2(1.4f, -1.4f));

            permanentUpgradeButtonText = CreateUiText(
                "PermanentUpgradeButtonLabel",
                root.transform,
                PermanentUpgradeLabel,
                18,
                FontStyle.Bold,
                new Vector2(0.5f, 0.18f),
                new Vector2(0.5f, 0.18f),
                Vector2.zero,
                new Vector2(142f, 34f),
                new Color(1f, 0.92f, 0.68f, 1f),
                TextAnchor.MiddleCenter);
            AddTextShadow(permanentUpgradeButtonText, new Color(0f, 0f, 0f, 0.82f), new Vector2(1.5f, -1.5f));

            ConfigurePermanentUpgradeShortcutVisual(root.transform, permanentUpgradeButtonText, permanentUpgradeStatusText);
            return button;
        }

        private static void ConfigurePermanentUpgradeShortcutVisual(Transform buttonTransform, Text labelText, Text statusText)
        {
            if (buttonTransform == null)
            {
                return;
            }

            Sprite frameSprite = LoadSpriteResource(PermanentUpgradeButtonFrameSpritePath, "PermanentUpgradeButtonFrame");
            Image image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                if (frameSprite != null)
                {
                    image.sprite = frameSprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.color = Color.white;
                }
                else
                {
                    image.sprite = null;
                    image.color = new Color(0.025f, 0.055f, 0.070f, 0.96f);
                }

                image.raycastTarget = true;
            }

            Outline outline = buttonTransform.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonTransform.gameObject.AddComponent<Outline>();
            }

            outline.enabled = frameSprite == null;
            outline.effectColor = new Color(0.30f, 0.92f, 1f, 0.80f);
            outline.effectDistance = new Vector2(2f, -2f);

            Transform oldIcon = buttonTransform.Find("PermanentUpgradeIcon");
            if (oldIcon != null)
            {
                DestroySceneObject(oldIcon.gameObject);
            }

            Vector2 buttonSize = ResolveRectSize(buttonTransform as RectTransform, PermanentUpgradeButtonSize);
            Sprite iconSprite = LoadSpriteResource(PermanentUpgradeIconSpritePath, "PermanentUpgradeIcon");
            if (iconSprite != null)
            {
                Image iconImage = CreateMenuImage(
                    "PermanentUpgradeIcon",
                    buttonTransform,
                    iconSprite,
                    new Vector2(0.5f, 0.58f),
                    new Vector2(0.5f, 0.58f),
                    new Vector2(0f, -8f),
                    new Vector2(buttonSize.x * 0.42f, buttonSize.y * 0.42f),
                    true);
                iconImage.transform.SetAsFirstSibling();
            }

            ConfigurePermanentUpgradeShortcutText(labelText, statusText);
            if (statusText != null)
            {
                statusText.transform.SetAsLastSibling();
            }

            if (labelText != null)
            {
                labelText.transform.SetAsLastSibling();
            }
        }

        private static void ConfigurePermanentUpgradeShortcutText(Text labelText, Text statusText)
        {
            if (labelText != null)
            {
                RectTransform labelRect = labelText.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0.5f, 0.18f);
                    labelRect.anchorMax = new Vector2(0.5f, 0.18f);
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = Vector2.zero;
                    labelRect.sizeDelta = new Vector2(142f, 34f);
                }

                labelText.fontSize = 18;
                labelText.fontStyle = FontStyle.Bold;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 12;
                labelText.resizeTextMaxSize = 18;
                labelText.color = new Color(1f, 0.92f, 0.68f, 1f);
                labelText.text = PermanentUpgradeLabel;
            }

            if (statusText != null)
            {
                RectTransform statusRect = statusText.transform as RectTransform;
                if (statusRect != null)
                {
                    statusRect.anchorMin = new Vector2(0.5f, 0.36f);
                    statusRect.anchorMax = new Vector2(0.5f, 0.36f);
                    statusRect.pivot = new Vector2(0.5f, 0.5f);
                    statusRect.anchoredPosition = new Vector2(0f, -4f);
                    statusRect.sizeDelta = new Vector2(164f, 38f);
                }

                statusText.fontSize = 14;
                statusText.fontStyle = FontStyle.Bold;
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.resizeTextForBestFit = true;
                statusText.resizeTextMinSize = 9;
                statusText.resizeTextMaxSize = 14;
                statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
                statusText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private void RefreshPermanentUpgradeShortcutDisplay(PlayerProfile profile)
        {
            if (permanentUpgradeButton != null)
            {
                permanentUpgradeButton.interactable = profile != null;
            }

            if (permanentUpgradeStatusText == null)
            {
                return;
            }

            if (profile == null)
            {
                permanentUpgradeStatusText.text = "読込中";
                permanentUpgradeStatusText.color = new Color(0.72f, 0.80f, 0.90f, 1f);
                return;
            }

            if (profile.HasAutoRepeatFloorUpgrade && profile.IsAutoRepeatFloorUpgradeEnabled)
            {
                permanentUpgradeStatusText.text = "同階層再挑戦 有効";
                permanentUpgradeStatusText.color = new Color(0.45f, 1f, 0.78f, 1f);
            }
            else if (profile.HasAutoRepeatFloorUpgrade)
            {
                permanentUpgradeStatusText.text = "同階層再挑戦 無効";
                permanentUpgradeStatusText.color = new Color(0.86f, 0.78f, 0.70f, 1f);
            }
            else if (profile.MonsterStorageLimit > PlayerProfile.DefaultMonsterStorageLimit ||
                     profile.EquipmentStorageLimit > PlayerProfile.DefaultEquipmentStorageLimit)
            {
                permanentUpgradeStatusText.text = "枠拡張 有効";
                permanentUpgradeStatusText.color = new Color(0.45f, 1f, 0.78f, 1f);
            }
            else
            {
                permanentUpgradeStatusText.text = "有効化なし";
                permanentUpgradeStatusText.color = new Color(0.86f, 0.78f, 0.70f, 1f);
            }
        }

        private static Vector2 ResolveRectSize(RectTransform rectTransform, Vector2 fallback)
        {
            if (rectTransform == null)
            {
                return fallback;
            }

            Vector2 size = rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = rectTransform.sizeDelta;
            }

            if (size.x <= 0f || size.y <= 0f)
            {
                return fallback;
            }

            return size;
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

        private static Outline AddUiOutline(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return null;
            }

            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            return outline;
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
