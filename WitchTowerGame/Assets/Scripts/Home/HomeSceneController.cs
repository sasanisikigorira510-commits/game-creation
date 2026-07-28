using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Monetization;
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
        [SerializeField] private RebirthPanelController rebirthPanelController;
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
        private const string SkillTreeButtonIconSpritePath = "UI/HomeMenu/SkillTreeButtonImage2";
        private const string PaidShopLabel = "宝晶商店";
        private const string PermanentUpgradeLabel = "永続強化";
        private const string SkillTreeLabel = "魂樹";
        private const string GoldShopLabel = "商店";
        private const string HomeTopHudFramePath = "UI/HomeRedesign/HomeTopHudFrame";
        private const string HomeBottomNavBarPath = "UI/HomeRedesign/HomeBottomNavBar";
        private const string HomeTopHudProfileFramePath = "UI/HomeRedesign/HomeTopHudProfile";
        private const string HomeTopHudGoldFramePath = "UI/HomeRedesign/HomeTopHudGold";
        private const string HomeTopHudFreeStoneFramePath = "UI/HomeRedesign/HomeTopHudFreeStone";
        private const string HomeTopHudPaidStoneFramePath = "UI/HomeRedesign/HomeTopHudPaidStone";
        private const string HomeTeamCombatPowerFramePath = "UI/HomeRedesign/HomeTeamCombatPowerFrame";
        private const string HomeFallbackHeroSpritePath = "FamilyMonsterCards/Dragon/dragon_whelp";
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const string TutorialPrologueBackgroundPath = "UI/Tutorial/TutorialPrologueContractFurnace";
        private const string TutorialPrologueSparklePath = "UI/Tutorial/TutorialPrologueSpark";
        private const string TutorialSummonHighlightFramePath = "UI/Tutorial/TutorialSummonHighlightFrameImage2";
        private const string AudioSettingsPanelFramePath = "UI/AudioSettings/SettingsPanelFrameImage2";
        private const string AudioSettingsGearButtonPath = "UI/AudioSettings/SettingsGearButtonImage2";
        private const string AudioSettingsActionButtonPath = "UI/AudioSettings/SettingsActionButtonImage2";
        private const string AudioSettingsSliderTrackPath = "UI/AudioSettings/SettingsSliderTrackImage2";
        private const string AudioSettingsSliderKnobPath = "UI/AudioSettings/SettingsSliderKnobImage2";
        private const string AudioSettingsToggleFramePath = "UI/AudioSettings/SettingsToggleFrameImage2";
        private const int TutorialProloguePageCount = 4;
        private const string RockGolemMonsterId = "monster_rock_golem";
        private const string RockGolemHomeHeroSpritePath = "MonsterBattle/mon_rock_golem_attack_0";
        // Keeps the home controls clear of the native banner area when a banner is enabled later.
        // No Unity-side ad placeholder is drawn while no banner is present.
        private const float HomeFooterContentInset = 170f;
        private static readonly Vector2 HomeGuidePanelPosition = new Vector2(0f, HomeFooterContentInset + 1050f);
        private static readonly Vector2 HomeGuidePanelSize = new Vector2(920f, 318f);
        private static readonly Vector2 HomeMenuButtonSize = new Vector2(480f, 250f);
        private static readonly Vector2 HomeMainActionButtonSize = new Vector2(470f, 220f);
        private static readonly Vector2 HomeBottomNavButtonSize = new Vector2(196f, 152f);
        private const float HomeBottomNavVisualWidth = 1080f;
        private const float HomeBottomNavSlotWidth = HomeBottomNavVisualWidth / 5f;
        private const float HomeBottomNavHeight = 190f;
        private const float HomeBottomNavCenterOffsetX = 0f;
        private const float HomeBottomNavCenterY = HomeFooterContentInset + 94f;
        private const float FirstSummonHighlightOffsetX = 11f;
        private const float ShopHighlightOffsetX = 21f;
        private const float DexHighlightOffsetX = 0f;
        private const float EquipmentHighlightOffsetX = -10f;
        private const float FusionHighlightOffsetX = -23f;
        private static readonly Vector2 HomeBottomNavButtonHitSize = new Vector2(HomeBottomNavSlotWidth, HomeBottomNavHeight);
        private static readonly Vector2 HomeBottomNavSegmentSize = new Vector2(HomeBottomNavSlotWidth, HomeBottomNavHeight);
        private static readonly Vector2 HomeBottomNavLabelPosition = new Vector2(9f, 42f);
        private static readonly Vector2 HomeBottomNavLabelSize = new Vector2(HomeBottomNavSlotWidth, 46f);
        private static readonly Vector2[] HomeBottomNavLabelPositions =
        {
            new Vector2(18f, 47f),
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
            "SkillTreeButton",
            "QuestButton"
        };
        private static readonly string[] TutorialPrologueSpeakers =
        {
            "古い契約記録",
            "契約網崩壊の日",
            "？？？",
            "契約炉の案内役・ルシェ"
        };
        private static readonly string[] TutorialPrologueBodies =
        {
            "かつて、六つの大迷宮は\nひとつの「契約網」で結ばれていた。",
            "だがある夜、契約網は何者かに断ち切られ、\n眷属たちの記憶は契約片となって\n各地のダンジョンへ散った。",
            "……聞こえますか、契約師様。\n私は契約炉の案内役ルシェ。\nこの灯が消える前にあなたを待っていました。",
            "最後の契約炉があなたを選びました。\n失われた仲間を呼び戻し六つのダンジョンを巡って\n契約網を壊した者の正体を突き止めましょう。"
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
        private static readonly Vector2 HomeAudioSettingsButtonPosition = new Vector2(474f, -68f);
        private static readonly Vector2 HomeAudioSettingsButtonSize = new Vector2(88f, 88f);
        private static readonly Vector2 HomeTeamCombatPowerPanelPosition = new Vector2(0f, -198f);
        private static readonly Vector2 HomeTeamCombatPowerPanelSize = new Vector2(620f, 110f);
        private static readonly Vector2 HomeTeamCombatPowerTextPosition = Vector2.zero;
        private static readonly Vector2 HomeTeamCombatPowerTextSize = new Vector2(500f, 44f);
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
        private static readonly Vector2 SkillTreeButtonPosition = new Vector2(456f, -405f);
        private static readonly Vector2 SkillTreeButtonSize = new Vector2(168f, 168f);
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
        private Text homeTeamCombatPowerText;
        private Text homeExpText;
        private Text homePlayerLevelText;
        private bool homePlayerExpDetailsVisible;
        private Text homeGuideText;
        private Text homeNextFloorText;
        private Text homeGuideBadgeText;
        private Text homeGuideTitleText;
        private Image homeGuideCharacterImage;
        private Button homeGuideButton;
        private GameObject homeTutorialFocusRoot;
        private Text homeTutorialFocusText;
        private readonly List<Image> homeTutorialFocusFrameImages = new List<Image>();
        private Graphic homeTutorialTargetGraphic;
        private Outline homeTutorialTargetOutline;
        private GameObject homeFirstSummonPulseRoot;
        private Image homeFirstSummonPulseFrame;
        private GameObject homePrologueRoot;
        private CanvasGroup homePrologueCanvasGroup;
        private RectTransform homePrologueBackgroundRect;
        private Image homePrologueGuideImage;
        private Image homePrologueDialoguePanel;
        private Text homePrologueTitleText;
        private Text homePrologueSpeakerText;
        private Text homePrologueBodyText;
        private Text homePrologueProgressText;
        private Text homeProloguePromptText;
        private readonly List<RectTransform> homePrologueSparkRects = new List<RectTransform>();
        private readonly List<Image> homePrologueSparkImages = new List<Image>();
        private int homeProloguePageIndex = -1;
        private float homeProloguePageStartedAt;
        private float homePrologueLastAdvanceAt = -10f;
        private float homePrologueCloseStartedAt;
        private bool homePrologueClosing;
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
        private Text skillTreeButtonText;
        private Button skillTreeButton;
        private GameObject audioSettingsRoot;
        private Slider audioSettingsBgmSlider;
        private Slider audioSettingsSeSlider;
        private Toggle audioSettingsHapticsToggle;
        private Text audioSettingsBgmValueText;
        private Text audioSettingsSeValueText;
        private Text audioSettingsHapticsValueText;
        private GameObject dailyQuestListRoot;
        private Text dailyQuestStatusText;
        private BannerAdVisibilityController homeBannerAdVisibilityController;
        private readonly List<DailyQuestCardView> dailyQuestCards = new List<DailyQuestCardView>();

        public BannerAdVisibilityController HomeBannerAdController => homeBannerAdVisibilityController;

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

            NormalizeCanvasScales();
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

                EnsureHomeBannerAdSlot(unifiedMenuRoot.transform);
                RefreshHomeStoneBalanceBar();
                RefreshDailyQuestList();
                AnimateHomeTutorialFocus();
                AnimateFirstSummonTutorialPulse();
            }

            if (IsHomePrologueVisible())
            {
                AnimateHomePrologue();
                if (Input.GetMouseButtonDown(0))
                {
                    InvokeButtonUnderPointer(homePrologueRoot.transform, Input.mousePosition);
                }

                return;
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
                    EnsureHomeBannerAdSlot(unifiedMenuRoot.transform);
                    RefreshHomeStoneBalanceBar();
                    RefreshHomePrologue(GetRuntimeProfile());
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

            Sprite backgroundSprite = Resources.Load<Sprite>("UI/HomeMenu/HomeMenuBackground_NoAdZone");
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
            CreateHomeHeroShowcase(unifiedMenuRoot.transform);
            EnsureHomeGuidePanel(unifiedMenuRoot.transform);

            Sprite battleSprite = Resources.Load<Sprite>("UI/HomeMenu/BattleButton");
            Sprite formationSprite = Resources.Load<Sprite>("UI/HomeMenu/FormationButton");
            Sprite equipmentSprite = Resources.Load<Sprite>("UI/HomeMenu/EquipmentButton");
            Sprite fusionSprite = Resources.Load<Sprite>("UI/HomeMenu/FusionButton");
            Sprite gachaSprite = Resources.Load<Sprite>("UI/HomeMenu/GachaButton");
            Sprite dexSprite = Resources.Load<Sprite>("UI/HomeMenu/DexButton");

            CreateHomeSpriteButton("BattleButton", unifiedMenuRoot.transform, battleSprite, "冒険開始", new Vector2(-250f, HomeFooterContentInset + 324f), HomeMainActionButtonSize, StartBattle, 36);
            CreateHomeSpriteButton("FormationButton", unifiedMenuRoot.transform, formationSprite, "編成", new Vector2(250f, HomeFooterContentInset + 324f), HomeMainActionButtonSize, OpenFormationMenu, 36);
            CreateHomeBottomNavigation(unifiedMenuRoot.transform, panelSprite, gachaSprite, dexSprite, equipmentSprite, fusionSprite);
            EnsureHomeBannerAdSlot(unifiedMenuRoot.transform);
            EnsureHomeStoneBalanceBar(unifiedMenuRoot.transform);
            RefreshHomeStoneBalanceBar();
            RefreshHomePrologue(GetRuntimeProfile());
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
            if (!Application.isPlaying || !MonetizationFeatureFlags.StorefrontEnabled)
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
            PlayerProfile profile = GetRuntimeProfile();
            if (!Application.isPlaying ||
                (!MonetizationFeatureFlags.StorefrontEnabled && !HasManageablePermanentUpgrade(profile)))
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

            Action closeCallback = () =>
            {
                if (unifiedMenuRoot != null)
                {
                    unifiedMenuRoot.SetActive(true);
                    unifiedMenuRoot.transform.SetAsLastSibling();
                    RefreshHomeStoneBalanceBar();
                }
            };

            if (MonetizationFeatureFlags.StorefrontEnabled)
            {
                shopPanel.Show(closeCallback);
                shopPanel.OpenPurchasedPermanentUpgradeList();
            }
            else
            {
                shopPanel.ShowPurchasedPermanentUpgradeList(closeCallback);
            }
        }

        public void OpenSkillTreeScene()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HideUnifiedMenu();
            RebirthPanelController skillTreePanel = EnsureRebirthSkillTreePanel();
            if (skillTreePanel == null)
            {
                BuildUnifiedMenu();
                return;
            }

            skillTreePanel.Show(() =>
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
                SetHomeGuidePanelVisible(false);
                HideHomeTutorialFocus();
                RefreshDailyQuestList();
            }
        }

        private void CloseDailyQuestList()
        {
            if (dailyQuestListRoot != null)
            {
                dailyQuestListRoot.SetActive(false);
            }

            PlayerProfile profile = GetRuntimeProfile();
            ApplyHomeGuideDisplay(profile);
            ApplyHomeTutorialFocus(profile);
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

        private RebirthPanelController EnsureRebirthSkillTreePanel()
        {
            if (rebirthPanelController != null)
            {
                return rebirthPanelController;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                return null;
            }

            Transform existingPanel = canvas.transform.Find("RebirthSkillTreePanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : CreateUiRoot("RebirthSkillTreePanel", canvas.transform);

            rebirthPanelController = panelObject.GetComponent<RebirthPanelController>();
            if (rebirthPanelController == null)
            {
                rebirthPanelController = panelObject.AddComponent<RebirthPanelController>();
            }

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            return rebirthPanelController;
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

        private void EnsureHomeBannerAdSlot(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            // Remove the former MISSION / PARTY strip even when an already-built menu is reused.
            RemoveDirectChild(menuRoot, "HomeLowerIntelStrip");

            Transform existingSlot = menuRoot.Find("HomeBannerAdSlot");
            GameObject slot = existingSlot != null
                ? existingSlot.gameObject
                : new GameObject("HomeBannerAdSlot", typeof(RectTransform));
            if (existingSlot == null)
            {
                slot.transform.SetParent(menuRoot, false);
            }

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(0f, HomeFooterContentInset * 0.5f);
            slotRect.sizeDelta = new Vector2(1080f, HomeFooterContentInset);

            homeBannerAdVisibilityController = slot.GetComponent<BannerAdVisibilityController>();
            if (homeBannerAdVisibilityController == null)
            {
                homeBannerAdVisibilityController = slot.AddComponent<BannerAdVisibilityController>();
            }

            Transform existingContent = slot.transform.Find("BannerContent");
            GameObject content = existingContent != null
                ? existingContent.gameObject
                : new GameObject("BannerContent", typeof(RectTransform));
            if (existingContent == null)
            {
                content.transform.SetParent(slot.transform, false);
            }

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            homeBannerAdVisibilityController.SetBannerContent(content);
            if (existingContent == null)
            {
                // The ad SDK turns this on after its banner view is loaded.
                homeBannerAdVisibilityController.SetBannerLoaded(false);
            }
        }

        private static string BuildHomeTeamCombatPowerText(PlayerProfile profile)
        {
            int combatPower = ResolveHomeTeamCombatPower(profile);
            return $"チーム戦闘力 {combatPower:N0}";
        }

        private static int ResolveHomeTeamCombatPower(PlayerProfile profile)
        {
            if (profile == null || profile.PartyMonsterInstanceIds == null)
            {
                return 0;
            }

            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            int total = 0;
            for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
            {
                OwnedMonsterData monster = profile.GetOwnedMonster(profile.PartyMonsterInstanceIds[i]);
                if (monster == null || string.IsNullOrEmpty(monster.MonsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = masterDataManager != null
                    ? masterDataManager.GetMonsterData(monster.MonsterId)
                    : null;
                BattleUnitStats stats = MonsterBattleStatsFactory.Create(profile, monster, monsterData);
                total += BattleEncounterAdvisor.CalculateCombatPower(stats);
            }

            return Mathf.Max(0, total);
        }

        private void CreateHomeHeroShowcase(Transform menuRoot)
        {
            GameObject showcase = new GameObject("HomeHeroShowcase", typeof(RectTransform));
            showcase.transform.SetParent(menuRoot, false);
            RectTransform showcaseRect = showcase.GetComponent<RectTransform>();
            showcaseRect.anchorMin = new Vector2(0.5f, 0f);
            showcaseRect.anchorMax = new Vector2(0.5f, 0f);
            showcaseRect.pivot = new Vector2(0.5f, 0.5f);
            showcaseRect.anchoredPosition = new Vector2(0f, HomeFooterContentInset + 760f);
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
                panelImage.color = new Color(0.018f, 0.026f, 0.038f, 0.94f);
                panelImage.raycastTarget = true;
                Outline outline = panel.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.78f, 0.34f, 0.82f);
                outline.effectDistance = new Vector2(3f, -3f);
            }
            else
            {
                Image panelImage = existingPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.018f, 0.026f, 0.038f, 0.94f);
                    panelImage.raycastTarget = true;
                }

                Outline outline = existingPanel.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = existingPanel.gameObject.AddComponent<Outline>();
                }

                outline.effectColor = new Color(1f, 0.78f, 0.34f, 0.82f);
                outline.effectDistance = new Vector2(3f, -3f);
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

            homeGuideCharacterImage = existingPanel.Find("HomeGuideCharacterImage")?.GetComponent<Image>();
            if (homeGuideCharacterImage == null)
            {
                homeGuideCharacterImage = CreateMenuImage(
                    "HomeGuideCharacterImage",
                    existingPanel,
                    LoadSpriteResource(TutorialGuideSpritePath, "TutorialGuideAssistant"),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(126f, -8f),
                    new Vector2(230f, 230f),
                    true);
            }

            ConfigureGuideRect(
                homeGuideCharacterImage.transform as RectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(126f, -8f),
                new Vector2(230f, 230f));
            homeGuideCharacterImage.sprite = LoadSpriteResource(TutorialGuideSpritePath, "TutorialGuideAssistant");
            homeGuideCharacterImage.preserveAspect = true;
            homeGuideCharacterImage.raycastTarget = false;

            homeGuideBadgeText = existingPanel.Find("HomeGuideBadgeText")?.GetComponent<Text>();
            if (homeGuideBadgeText == null)
            {
                homeGuideBadgeText = CreateUiText(
                    "HomeGuideBadgeText",
                    existingPanel,
                    "TUTORIAL",
                    18,
                    FontStyle.Bold,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(252f, -24f),
                    new Vector2(122f, 34f),
                    new Color(1f, 0.82f, 0.32f, 1f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(homeGuideBadgeText, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.5f, -1.5f));
            }

            ConfigureGuideRect(
                homeGuideBadgeText.transform as RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(252f, -22f),
                new Vector2(122f, 34f));
            homeGuideBadgeText.gameObject.SetActive(false);
            homeGuideBadgeText.resizeTextForBestFit = true;
            homeGuideBadgeText.resizeTextMinSize = 12;
            homeGuideBadgeText.resizeTextMaxSize = 18;
            homeGuideBadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            homeGuideBadgeText.verticalOverflow = VerticalWrapMode.Truncate;

            homeGuideTitleText = existingPanel.Find("HomeGuideTitleText")?.GetComponent<Text>();
            if (homeGuideTitleText == null)
            {
                homeGuideTitleText = CreateUiText(
                    "HomeGuideTitleText",
                    existingPanel,
                    string.Empty,
                    26,
                    FontStyle.Bold,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(615f, -48f),
                    new Vector2(560f, 54f),
                    new Color(1f, 0.96f, 0.78f, 1f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(homeGuideTitleText, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.6f, -1.6f));
            }

            ConfigureGuideRect(
                homeGuideTitleText.transform as RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(615f, -48f),
                new Vector2(560f, 54f));
            homeGuideTitleText.resizeTextForBestFit = true;
            homeGuideTitleText.resizeTextMinSize = 25;
            homeGuideTitleText.resizeTextMaxSize = 32;
            homeGuideTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeGuideTitleText.verticalOverflow = VerticalWrapMode.Truncate;

            homeGuideText = existingPanel.Find("HomeGuideText")?.GetComponent<Text>();
            if (homeGuideText == null)
            {
                homeGuideText = CreateUiText(
                    "HomeGuideText",
                    existingPanel,
                    string.Empty,
                    22,
                    FontStyle.Bold,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(615f, 18f),
                    new Vector2(560f, 128f),
                    new Color(0.96f, 0.95f, 0.88f, 1f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(homeGuideText, new Color(0f, 0f, 0f, 0.82f), new Vector2(1.4f, -1.4f));
            }

            ConfigureGuideRect(
                homeGuideText.transform as RectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(615f, 18f),
                new Vector2(560f, 128f));
            homeGuideText.resizeTextForBestFit = true;
            homeGuideText.resizeTextMinSize = 18;
            homeGuideText.resizeTextMaxSize = 26;
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
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(615f, 42f),
                    new Vector2(560f, 42f),
                    new Color(0.70f, 0.90f, 1f, 0.98f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(homeNextFloorText, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.4f, -1.4f));
            }

            ConfigureGuideRect(
                homeNextFloorText.transform as RectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(615f, 42f),
                new Vector2(560f, 42f));
            homeNextFloorText.resizeTextForBestFit = true;
            homeNextFloorText.resizeTextMinSize = 17;
            homeNextFloorText.resizeTextMaxSize = 21;
            homeNextFloorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homeNextFloorText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void ConfigureGuideRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private void RefreshHomePrologue(PlayerProfile profile)
        {
            bool shouldShow = Application.isPlaying &&
                profile != null &&
                !profile.HasCompletedTutorial &&
                profile.TutorialStepId == StoryTutorialService.StepWakeup;
            if (!shouldShow)
            {
                if (homePrologueRoot != null)
                {
                    homePrologueRoot.SetActive(false);
                }

                return;
            }

            Transform menuRoot = ResolveUnifiedMenuRootTransform();
            EnsureHomePrologue(menuRoot);
            if (homePrologueRoot == null)
            {
                return;
            }

            homePrologueRoot.SetActive(true);
            homePrologueRoot.transform.SetAsLastSibling();
            if (homeProloguePageIndex < 0)
            {
                ShowHomeProloguePage(0);
            }
        }

        private void EnsureHomePrologue(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            if (homePrologueRoot != null && homePrologueRoot.transform.parent == menuRoot)
            {
                return;
            }

            homePrologueSparkRects.Clear();
            homePrologueSparkImages.Clear();

            Transform existingRoot = menuRoot.Find("HomePrologue");
            if (existingRoot != null)
            {
                DestroySceneObject(existingRoot.gameObject);
            }

            homePrologueRoot = new GameObject(
                "HomePrologue",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup));
            homePrologueRoot.transform.SetParent(menuRoot, false);
            RectTransform rootRect = homePrologueRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = homePrologueRoot.GetComponent<Image>();
            rootImage.color = Color.black;
            rootImage.raycastTarget = true;

            Button rootButton = homePrologueRoot.GetComponent<Button>();
            rootButton.transition = Selectable.Transition.None;
            rootButton.targetGraphic = rootImage;
            rootButton.onClick.RemoveAllListeners();
            rootButton.onClick.AddListener(AdvanceHomePrologue);

            homePrologueCanvasGroup = homePrologueRoot.GetComponent<CanvasGroup>();
            homePrologueCanvasGroup.alpha = 1f;
            homePrologueCanvasGroup.blocksRaycasts = true;
            homePrologueCanvasGroup.interactable = true;

            Image background = CreateMenuImage(
                "HomePrologueBackground",
                homePrologueRoot.transform,
                LoadSpriteResource(TutorialPrologueBackgroundPath, "TutorialPrologueContractFurnace"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1080f, 1920f),
                false);
            background.color = Color.white;
            homePrologueBackgroundRect = background.transform as RectTransform;

            Image upperShade = CreateMenuImage(
                "HomePrologueUpperShade",
                homePrologueRoot.transform,
                null,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -145f),
                new Vector2(1080f, 290f),
                false);
            upperShade.color = new Color(0f, 0f, 0f, 0.28f);

            Image lowerShade = CreateMenuImage(
                "HomePrologueLowerShade",
                homePrologueRoot.transform,
                null,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 260f),
                new Vector2(1080f, 520f),
                false);
            lowerShade.color = new Color(0f, 0f, 0f, 0.42f);

            CreateHomePrologueSparks(homePrologueRoot.transform);

            homePrologueTitleText = CreateUiText(
                "HomePrologueTitle",
                homePrologueRoot.transform,
                "序章　最後の契約炉",
                42,
                FontStyle.Bold,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -92f),
                new Vector2(920f, 80f),
                new Color(1f, 0.93f, 0.72f, 1f),
                TextAnchor.MiddleCenter);
            AddTextShadow(homePrologueTitleText, new Color(0f, 0f, 0f, 0.95f), new Vector2(3f, -3f));

            homePrologueGuideImage = CreateMenuImage(
                "HomePrologueGuide",
                homePrologueRoot.transform,
                LoadSpriteResource(TutorialGuideSpritePath, "TutorialGuideAssistant"),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(250f, 700f),
                new Vector2(650f, 650f),
                true);
            homePrologueGuideImage.color = new Color(1f, 1f, 1f, 0f);
            homePrologueGuideImage.gameObject.SetActive(false);

            homePrologueDialoguePanel = CreateMenuImage(
                "HomePrologueDialoguePanel",
                homePrologueRoot.transform,
                null,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 300f),
                new Vector2(980f, 390f),
                false);
            homePrologueDialoguePanel.color = new Color(0.015f, 0.022f, 0.032f, 0.88f);
            AddUiOutline(
                homePrologueDialoguePanel.gameObject,
                new Color(0.90f, 0.69f, 0.29f, 0.82f),
                new Vector2(3f, -3f));

            homePrologueSpeakerText = CreateUiText(
                "HomePrologueSpeaker",
                homePrologueRoot.transform,
                string.Empty,
                29,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-320f, 438f),
                new Vector2(300f, 52f),
                new Color(0.50f, 0.88f, 1f, 1f),
                TextAnchor.MiddleLeft);
            AddTextShadow(homePrologueSpeakerText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));

            homePrologueBodyText = CreateUiText(
                "HomePrologueBody",
                homePrologueRoot.transform,
                string.Empty,
                36,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 302f),
                new Vector2(900f, 245f),
                new Color(0.98f, 0.97f, 0.91f, 1f),
                TextAnchor.MiddleCenter);
            homePrologueBodyText.resizeTextForBestFit = true;
            homePrologueBodyText.resizeTextMinSize = 29;
            homePrologueBodyText.resizeTextMaxSize = 38;
            homePrologueBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            homePrologueBodyText.verticalOverflow = VerticalWrapMode.Truncate;
            AddTextShadow(homePrologueBodyText, new Color(0f, 0f, 0f, 0.94f), new Vector2(2f, -2f));

            homePrologueProgressText = CreateUiText(
                "HomePrologueProgress",
                homePrologueRoot.transform,
                string.Empty,
                22,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 118f),
                new Vector2(320f, 44f),
                new Color(0.68f, 0.87f, 1f, 1f),
                TextAnchor.MiddleCenter);

            homeProloguePromptText = CreateUiText(
                "HomeProloguePrompt",
                homePrologueRoot.transform,
                string.Empty,
                25,
                FontStyle.Bold,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 68f),
                new Vector2(720f, 54f),
                new Color(1f, 0.90f, 0.58f, 1f),
                TextAnchor.MiddleCenter);
            AddTextShadow(homeProloguePromptText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
        }

        private void CreateHomePrologueSparks(Transform parent)
        {
            Sprite sparkleSprite = LoadSpriteResource(TutorialPrologueSparklePath, "TutorialPrologueSparkle");
            for (int i = 0; i < 10; i += 1)
            {
                float size = 24f + i % 4 * 10f;
                Image sparkle = CreateMenuImage(
                    $"HomePrologueSpark_{i}",
                    parent,
                    sparkleSprite,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(size, size),
                    true);
                sparkle.color = new Color(0.45f, 0.88f, 1f, 0f);
                homePrologueSparkImages.Add(sparkle);
                homePrologueSparkRects.Add(sparkle.transform as RectTransform);
            }
        }

        private bool IsHomePrologueVisible()
        {
            return homePrologueRoot != null &&
                homePrologueRoot.activeInHierarchy &&
                homeProloguePageIndex >= 0;
        }

        private void ShowHomeProloguePage(int pageIndex)
        {
            homeProloguePageIndex = Mathf.Clamp(pageIndex, 0, TutorialProloguePageCount - 1);
            homeProloguePageStartedAt = Time.unscaledTime;
            homePrologueClosing = false;
            if (homePrologueCanvasGroup != null)
            {
                homePrologueCanvasGroup.alpha = 1f;
                homePrologueCanvasGroup.blocksRaycasts = true;
                homePrologueCanvasGroup.interactable = true;
            }

            if (homePrologueSpeakerText != null)
            {
                homePrologueSpeakerText.text = TutorialPrologueSpeakers[homeProloguePageIndex];
            }

            if (homePrologueBodyText != null)
            {
                homePrologueBodyText.text = TutorialPrologueBodies[homeProloguePageIndex];
                RectTransform bodyRect = homePrologueBodyText.transform as RectTransform;
                if (bodyRect != null)
                {
                    bool guideVisible = homeProloguePageIndex >= 2;
                    bodyRect.anchoredPosition = new Vector2(guideVisible ? 20f : 0f, 302f);
                    bodyRect.sizeDelta = new Vector2(guideVisible ? 720f : 850f, 245f);
                }

                homePrologueBodyText.alignment = homeProloguePageIndex >= 2
                    ? TextAnchor.MiddleLeft
                    : TextAnchor.MiddleCenter;
            }

            if (homePrologueSpeakerText != null)
            {
                RectTransform speakerRect = homePrologueSpeakerText.transform as RectTransform;
                if (speakerRect != null)
                {
                    bool guideVisible = homeProloguePageIndex >= 2;
                    speakerRect.anchoredPosition = new Vector2(guideVisible ? -40f : -320f, 438f);
                    speakerRect.sizeDelta = new Vector2(guideVisible ? 600f : 300f, 52f);
                }
            }

            if (homePrologueGuideImage != null)
            {
                homePrologueGuideImage.gameObject.SetActive(homeProloguePageIndex >= 2);
            }

            if (homePrologueProgressText != null)
            {
                homePrologueProgressText.text = BuildHomePrologueProgress(homeProloguePageIndex);
            }

            if (homeProloguePromptText != null)
            {
                homeProloguePromptText.text = homeProloguePageIndex >= TutorialProloguePageCount - 1
                    ? "タップして契約を始める"
                    : "タップして次へ";
            }
        }

        private static string BuildHomePrologueProgress(int activePageIndex)
        {
            string progress = string.Empty;
            for (int i = 0; i < TutorialProloguePageCount; i += 1)
            {
                if (i > 0)
                {
                    progress += "  ";
                }

                progress += i == activePageIndex ? "◆" : "◇";
            }

            return progress;
        }

        private void AnimateHomePrologue()
        {
            if (!IsHomePrologueVisible())
            {
                return;
            }

            homePrologueRoot.transform.SetAsLastSibling();
            float now = Time.unscaledTime;
            if (homePrologueClosing)
            {
                float closeProgress = Mathf.Clamp01((now - homePrologueCloseStartedAt) / 0.55f);
                if (homePrologueCanvasGroup != null)
                {
                    homePrologueCanvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, closeProgress);
                }

                if (closeProgress >= 1f)
                {
                    CompleteHomePrologue();
                }

                return;
            }

            float pageAge = Mathf.Max(0f, now - homeProloguePageStartedAt);
            float textAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pageAge - 0.08f) / 0.55f));
            if (homePrologueCanvasGroup != null)
            {
                homePrologueCanvasGroup.alpha = 1f;
            }

            if (homePrologueBackgroundRect != null)
            {
                float drift = Mathf.Clamp01(pageAge / 8f);
                float baseScale = 1.105f + homeProloguePageIndex * 0.006f;
                float scale = baseScale - drift * 0.025f;
                homePrologueBackgroundRect.localScale = new Vector3(scale, scale, 1f);
                homePrologueBackgroundRect.anchoredPosition =
                    new Vector2(Mathf.Sin(now * 0.13f) * 5f, 118f + Mathf.Sin(now * 0.20f) * 6f);
            }

            SetTextAlpha(homePrologueSpeakerText, textAlpha);
            SetTextAlpha(homePrologueBodyText, textAlpha);
            SetTextAlpha(homePrologueProgressText, Mathf.Min(1f, textAlpha + 0.15f));
            float promptPulse = 0.72f + Mathf.Sin(now * 3.2f) * 0.18f;
            SetTextAlpha(homeProloguePromptText, textAlpha * promptPulse);
            SetTextAlpha(homePrologueTitleText, Mathf.Min(1f, textAlpha + 0.25f));

            if (homePrologueGuideImage != null && homePrologueGuideImage.gameObject.activeSelf)
            {
                homePrologueGuideImage.color = new Color(1f, 1f, 1f, textAlpha);
                RectTransform guideRect = homePrologueGuideImage.transform as RectTransform;
                if (guideRect != null)
                {
                    float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pageAge / 0.65f));
                    guideRect.anchoredPosition =
                        new Vector2(Mathf.Lerp(195f, 250f, slide), 700f + Mathf.Sin(now * 1.4f) * 7f);
                }
            }

            AnimateHomePrologueSparks(now);
        }

        private void AnimateHomePrologueSparks(float now)
        {
            for (int i = 0; i < homePrologueSparkRects.Count; i += 1)
            {
                RectTransform sparkleRect = homePrologueSparkRects[i];
                Image sparkleImage = i < homePrologueSparkImages.Count ? homePrologueSparkImages[i] : null;
                if (sparkleRect == null || sparkleImage == null)
                {
                    continue;
                }

                float speed = 0.035f + i % 3 * 0.012f;
                float cycle = Mathf.Repeat(now * speed + i * 0.137f, 1f);
                float xBase = -430f + (i * 109) % 860;
                float x = xBase + Mathf.Sin(now * (0.55f + i * 0.03f) + i) * 28f;
                float y = -760f + cycle * 1580f;
                sparkleRect.anchoredPosition = new Vector2(x, y);
                float scale = 0.65f + Mathf.Sin(cycle * Mathf.PI) * 0.6f;
                sparkleRect.localScale = new Vector3(scale, scale, 1f);
                float alpha = Mathf.Sin(cycle * Mathf.PI) * (0.20f + homeProloguePageIndex * 0.07f);
                sparkleImage.color = new Color(0.48f, 0.88f, 1f, Mathf.Clamp01(alpha));
            }
        }

        private static void SetTextAlpha(Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        public void AdvanceHomePrologue()
        {
            if (!Application.isPlaying || !IsHomePrologueVisible() || homePrologueClosing)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - homePrologueLastAdvanceAt < 0.28f ||
                now - homeProloguePageStartedAt < 0.18f)
            {
                return;
            }

            homePrologueLastAdvanceAt = now;
            if (homeProloguePageIndex < TutorialProloguePageCount - 1)
            {
                ShowHomeProloguePage(homeProloguePageIndex + 1);
                return;
            }

            homePrologueClosing = true;
            homePrologueCloseStartedAt = now;
            if (homePrologueCanvasGroup != null)
            {
                homePrologueCanvasGroup.blocksRaycasts = false;
                homePrologueCanvasGroup.interactable = false;
            }
        }

        private void CompleteHomePrologue()
        {
            PlayerProfile profile = GetRuntimeProfile();
            bool changed = false;
            if (profile != null &&
                !profile.HasCompletedTutorial &&
                profile.TutorialStepId == StoryTutorialService.StepWakeup)
            {
                changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryPrologueWakeup);
                changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWakeup);
            }

            if (homePrologueRoot != null)
            {
                homePrologueRoot.SetActive(false);
            }

            homeProloguePageIndex = -1;
            homePrologueClosing = false;
            if (homePrologueCanvasGroup != null)
            {
                homePrologueCanvasGroup.alpha = 1f;
                homePrologueCanvasGroup.blocksRaycasts = true;
                homePrologueCanvasGroup.interactable = true;
            }

            SaveHomeTutorialProgressIfNeeded(changed);
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
                new Vector2(0f, HomeFooterContentInset + 86f),
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
                    new Vector2(0f, HomeFooterContentInset + 92f),
                    new Vector2(1180f, 210f),
                    false);
                ornament.color = new Color(1f, 1f, 1f, 0.18f);
            }

            float navY = HomeFooterContentInset + 86f;
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
                "経験値 -/-",
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

        private void EnsureAudioSettingsButton(Transform barTransform)
        {
            if (barTransform == null)
            {
                return;
            }

            Transform existing = barTransform.Find("AudioSettingsButton");
            Button button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                button = CreatePlainButton(
                    "AudioSettingsButton",
                    barTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    HomeAudioSettingsButtonPosition,
                    HomeAudioSettingsButtonSize,
                    new Color(0.018f, 0.025f, 0.038f, 0.88f),
                    OpenAudioSettingsPanel);
                if (!ConfigureAudioSettingsButtonArt(button))
                {
                    AddUiOutline(button.gameObject, new Color(0.68f, 0.88f, 1f, 0.58f), new Vector2(2f, -2f));
                    Text label = CreateUiText(
                        "AudioSettingsButtonLabel",
                        button.transform,
                        "設定",
                        22,
                        FontStyle.Bold,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        Vector2.zero,
                        new Vector2(76f, 38f),
                        new Color(0.92f, 0.98f, 1f, 1f),
                        TextAnchor.MiddleCenter);
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 16;
                    label.resizeTextMaxSize = 22;
                }
            }
            else
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OpenAudioSettingsPanel);
                ConfigureAudioSettingsButtonArt(button);
            }
        }

        private static bool ConfigureAudioSettingsButtonArt(Button button)
        {
            if (button == null)
            {
                return false;
            }

            bool applied = ApplyGeneratedUiSprite(
                button.GetComponent<Image>(),
                AudioSettingsGearButtonPath,
                "SettingsGearButtonImage2",
                true);
            if (!applied)
            {
                return false;
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            Transform label = button.transform.Find("AudioSettingsButtonLabel");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            return true;
        }

        public void OpenAudioSettingsPanel()
        {
            Transform parent = ResolveUnifiedMenuRootTransform();
            if (parent == null)
            {
                return;
            }

            if (audioSettingsRoot == null)
            {
                BuildAudioSettingsPanel(parent);
            }

            if (audioSettingsRoot == null)
            {
                return;
            }

            audioSettingsRoot.SetActive(true);
            audioSettingsRoot.transform.SetAsLastSibling();
            RefreshAudioSettingsPanel();
        }

        private void CloseAudioSettingsPanel()
        {
            if (audioSettingsRoot != null)
            {
                audioSettingsRoot.SetActive(false);
            }
        }

        private void BuildAudioSettingsPanel(Transform parent)
        {
            audioSettingsRoot = new GameObject("AudioSettingsPanelRoot", typeof(RectTransform));
            audioSettingsRoot.transform.SetParent(parent, false);
            RectTransform rootRect = audioSettingsRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image shade = CreateTintPanel(
                "AudioSettingsShade",
                audioSettingsRoot.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0f, 0f, 0f, 0.52f));
            shade.raycastTarget = true;

            Image panelImage = CreateTintPanel(
                "AudioSettingsPanel",
                audioSettingsRoot.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 92f),
                new Vector2(760f, 540f),
                new Color(0.018f, 0.020f, 0.030f, 0.98f));
            panelImage.raycastTarget = true;
            if (!ApplyGeneratedUiSprite(panelImage, AudioSettingsPanelFramePath, "SettingsPanelFrameImage2"))
            {
                AddUiOutline(panelImage.gameObject, new Color(0.42f, 0.72f, 0.92f, 0.80f), new Vector2(3f, -3f));
            }

            Transform panel = panelImage.transform;
            CreateUiText(
                "AudioSettingsTitle",
                panel,
                "設定",
                36,
                FontStyle.Bold,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -58f),
                new Vector2(360f, 54f),
                new Color(1f, 0.84f, 0.48f, 1f),
                TextAnchor.MiddleCenter);
            CreateUiText(
                "AudioSettingsBgmLabel",
                panel,
                "BGM",
                26,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(178f, -160f),
                new Vector2(130f, 42f),
                Color.white,
                TextAnchor.MiddleLeft);
            CreateUiText(
                "AudioSettingsSeLabel",
                panel,
                "SE",
                26,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(178f, -256f),
                new Vector2(130f, 42f),
                Color.white,
                TextAnchor.MiddleLeft);
            CreateUiText(
                "AudioSettingsHapticsLabel",
                panel,
                "振動",
                26,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(178f, -352f),
                new Vector2(130f, 42f),
                Color.white,
                TextAnchor.MiddleLeft);
            audioSettingsBgmValueText = CreateAudioSettingsValueText(panel, "AudioSettingsBgmValue", new Vector2(606f, -160f));
            audioSettingsSeValueText = CreateAudioSettingsValueText(panel, "AudioSettingsSeValue", new Vector2(606f, -256f));
            audioSettingsHapticsValueText = CreateAudioSettingsValueText(panel, "AudioSettingsHapticsValue", new Vector2(606f, -352f));
            audioSettingsBgmSlider = CreateAudioVolumeSlider(
                "AudioSettingsBgmSlider",
                panel,
                new Vector2(405f, -160f),
                delegate(float value)
                {
                    AudioManager.Instance?.SetBgmVolume(value);
                    RefreshAudioSettingsValueTexts();
                });
            audioSettingsSeSlider = CreateAudioVolumeSlider(
                "AudioSettingsSeSlider",
                panel,
                new Vector2(405f, -256f),
                delegate(float value)
                {
                    AudioManager.Instance?.SetSeVolume(value);
                    RefreshAudioSettingsValueTexts();
                });
            audioSettingsHapticsToggle = CreateAudioSettingsToggle(
                "AudioSettingsHapticsToggle",
                panel,
                new Vector2(405f, -352f),
                delegate(bool enabled)
                {
                    AudioManager audioManager = AudioManager.Instance;
                    audioManager?.SetHapticsEnabled(enabled);
                    RefreshAudioSettingsValueTexts();
                    if (enabled)
                    {
                        audioManager?.PlayHaptic(AudioCue.UiConfirm);
                    }
                });

            CreateAudioSettingsActionButton(
                panel,
                "AudioSettingsUnmuteButton",
                "ミュート解除",
                new Vector2(-232f, 92f),
                delegate
                {
                    SetAudioSettingsVolumes(0.58f, 0.76f, true);
                });
            CreateAudioSettingsActionButton(
                panel,
                "AudioSettingsMuteButton",
                "ミュート",
                new Vector2(0f, 92f),
                delegate
                {
                    SetAudioSettingsVolumes(0f, 0f, false);
                });
            CreateAudioSettingsActionButton(
                panel,
                "AudioSettingsCloseButton",
                "閉じる",
                new Vector2(232f, 92f),
                CloseAudioSettingsPanel);

            audioSettingsRoot.SetActive(false);
        }

        private static Text CreateAudioSettingsValueText(Transform parent, string name, Vector2 anchoredPosition)
        {
            Text text = CreateUiText(
                name,
                parent,
                "100%",
                24,
                FontStyle.Bold,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                new Vector2(82f, 42f),
                new Color(0.72f, 0.92f, 1f, 1f),
                TextAnchor.MiddleRight);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 24;
            return text;
        }

        private static Slider CreateAudioVolumeSlider(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(370f, 54f);

            Image hitArea = root.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            hitArea.raycastTarget = true;

            Image background = CreateSliderImage(
                "Background",
                root.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 0f),
                new Color(0.09f, 0.11f, 0.15f, 1f));
            background.raycastTarget = false;
            RectTransform backgroundRect = background.transform as RectTransform;
            bool hasTrackArt = ApplyGeneratedUiSprite(background, AudioSettingsSliderTrackPath, "SettingsSliderTrackImage2");
            backgroundRect.offsetMin = hasTrackArt ? new Vector2(-8f, 0f) : new Vector2(0f, 18f);
            backgroundRect.offsetMax = hasTrackArt ? new Vector2(8f, 0f) : new Vector2(0f, -18f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = hasTrackArt ? new Vector2(58f, 23f) : new Vector2(0f, 18f);
            fillAreaRect.offsetMax = hasTrackArt ? new Vector2(-58f, -23f) : new Vector2(0f, -18f);

            Image fill = CreateSliderImage(
                "Fill",
                fillArea.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.14f, 0.78f, 1f, 0.88f));
            fill.raycastTarget = false;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = hasTrackArt ? new Vector2(58f, 0f) : new Vector2(8f, 0f);
            handleAreaRect.offsetMax = hasTrackArt ? new Vector2(-58f, 0f) : new Vector2(-8f, 0f);

            Image handle = CreateSliderImage(
                "Handle",
                handleArea.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(34f, 34f),
                new Color(1f, 0.88f, 0.52f, 1f));
            handle.raycastTarget = false;
            if (ApplyGeneratedUiSprite(handle, AudioSettingsSliderKnobPath, "SettingsSliderKnobImage2"))
            {
                RectTransform handleRect = handle.transform as RectTransform;
                handleRect.sizeDelta = new Vector2(42f, 68f);
            }
            else
            {
                AddUiOutline(handle.gameObject, new Color(0f, 0f, 0f, 0.44f), new Vector2(1f, -1f));
            }

            Slider slider = root.GetComponent<Slider>();
            slider.interactable = true;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = hitArea;
            slider.fillRect = fill.transform as RectTransform;
            slider.handleRect = handle.transform as RectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(onValueChanged);
            return slider;
        }

        private static Toggle CreateAudioSettingsToggle(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(152f, 54f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.09f, 0.11f, 0.15f, 1f);
            background.raycastTarget = true;
            if (!ApplyGeneratedUiSprite(background, AudioSettingsToggleFramePath, "SettingsToggleFrameImage2"))
            {
                AddUiOutline(root, new Color(0.64f, 0.86f, 1f, 0.36f), new Vector2(1f, -1f));
            }

            Image checkmark = CreateSliderImage(
                "Checkmark",
                root.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(38f, 0f),
                new Vector2(58f, 34f),
                new Color(0.38f, 0.78f, 1f, 1f));
            checkmark.raycastTarget = false;
            if (ApplyGeneratedUiSprite(checkmark, AudioSettingsSliderKnobPath, "SettingsSliderKnobImage2"))
            {
                RectTransform checkmarkRect = checkmark.transform as RectTransform;
                checkmarkRect.anchoredPosition = new Vector2(40f, 0f);
                checkmarkRect.sizeDelta = new Vector2(30f, 46f);
            }

            Text label = CreateUiText(
                "ToggleLabel",
                root.transform,
                "ON",
                20,
                FontStyle.Bold,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(36f, 0f),
                new Vector2(92f, 34f),
                Color.white,
                TextAnchor.MiddleCenter);
            label.raycastTarget = false;

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            toggle.onValueChanged.AddListener(delegate(bool isOn)
            {
                label.text = isOn ? "ON" : "OFF";
                label.color = isOn ? Color.white : new Color(0.66f, 0.70f, 0.76f, 1f);
                background.color = background.sprite != null
                    ? (isOn ? Color.white : new Color(0.58f, 0.64f, 0.70f, 1f))
                    : (isOn
                        ? new Color(0.09f, 0.11f, 0.15f, 1f)
                        : new Color(0.08f, 0.08f, 0.09f, 1f));
            });
            toggle.onValueChanged.AddListener(onValueChanged);
            return toggle;
        }

        private static Image CreateSliderImage(
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
            image.raycastTarget = true;
            return image;
        }

        private void CreateAudioSettingsActionButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action)
        {
            Button button = CreatePlainButton(
                name,
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                anchoredPosition,
                new Vector2(188f, 64f),
                new Color(0.12f, 0.19f, 0.24f, 0.96f),
                action);
            if (!ApplyGeneratedUiSprite(button.GetComponent<Image>(), AudioSettingsActionButtonPath, "SettingsActionButtonImage2"))
            {
                AddUiOutline(button.gameObject, new Color(0.64f, 0.86f, 1f, 0.42f), new Vector2(2f, -2f));
            }
            Text text = CreateUiText(
                name + "Label",
                button.transform,
                label,
                22,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(166f, 38f),
                Color.white,
                TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 15;
            text.resizeTextMaxSize = 22;
        }

        private void SetAudioSettingsVolumes(float bgm, float se, bool playPreview)
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
            {
                return;
            }

            audioManager.SetBgmVolume(bgm);
            audioManager.SetSeVolume(se);
            RefreshAudioSettingsPanel();
            if (playPreview)
            {
                audioManager.PlaySe(AudioCue.UiConfirm);
            }
        }

        private void RefreshAudioSettingsPanel()
        {
            AudioManager audioManager = AudioManager.Instance;
            float bgm = audioManager != null ? audioManager.BgmVolume : 0.58f;
            float se = audioManager != null ? audioManager.SeVolume : 0.76f;
            bool haptics = audioManager == null || audioManager.HapticsEnabled;
            if (audioSettingsBgmSlider != null)
            {
                audioSettingsBgmSlider.SetValueWithoutNotify(bgm);
            }

            if (audioSettingsSeSlider != null)
            {
                audioSettingsSeSlider.SetValueWithoutNotify(se);
            }

            if (audioSettingsHapticsToggle != null)
            {
                audioSettingsHapticsToggle.SetIsOnWithoutNotify(haptics);
                RefreshAudioSettingsToggleVisual(audioSettingsHapticsToggle, haptics);
            }

            RefreshAudioSettingsValueTexts();
        }

        private void RefreshAudioSettingsValueTexts()
        {
            AudioManager audioManager = AudioManager.Instance;
            float bgm = audioManager != null ? audioManager.BgmVolume : 0.58f;
            float se = audioManager != null ? audioManager.SeVolume : 0.76f;
            bool haptics = audioManager == null || audioManager.HapticsEnabled;
            if (audioSettingsBgmValueText != null)
            {
                audioSettingsBgmValueText.text = Mathf.RoundToInt(bgm * 100f) + "%";
            }

            if (audioSettingsSeValueText != null)
            {
                audioSettingsSeValueText.text = Mathf.RoundToInt(se * 100f) + "%";
            }

            if (audioSettingsHapticsValueText != null)
            {
                audioSettingsHapticsValueText.text = haptics ? "ON" : "OFF";
            }
        }

        private static void RefreshAudioSettingsToggleVisual(Toggle toggle, bool isOn)
        {
            if (toggle == null)
            {
                return;
            }

            Image background = toggle.GetComponent<Image>();
            if (background != null)
            {
                background.color = background.sprite != null
                    ? (isOn ? Color.white : new Color(0.58f, 0.64f, 0.70f, 1f))
                    : (isOn
                        ? new Color(0.09f, 0.11f, 0.15f, 1f)
                        : new Color(0.08f, 0.08f, 0.09f, 1f));
            }

            Transform labelTransform = toggle.transform.Find("ToggleLabel");
            Text label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null)
            {
                label.text = isOn ? "ON" : "OFF";
                label.color = isOn ? Color.white : new Color(0.66f, 0.70f, 0.76f, 1f);
            }
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
                homeGuideBadgeText = menuRoot.Find("HomeGuidePanel/HomeGuideBadgeText")?.GetComponent<Text>();
                homeGuideTitleText = menuRoot.Find("HomeGuidePanel/HomeGuideTitleText")?.GetComponent<Text>();
                homeGuideCharacterImage = menuRoot.Find("HomeGuidePanel/HomeGuideCharacterImage")?.GetComponent<Image>();
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
                EnsureSkillTreeShortcutButton(existingBar);
                EnsureAudioSettingsButton(existingBar);
                homeTeamCombatPowerText = EnsureHomeTeamCombatPowerText(existingBar);
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
                    skillTreeButtonText != null &&
                    skillTreeButton != null &&
                    homeTeamCombatPowerText != null &&
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
            skillTreeButton = CreateSkillTreeShortcutButton(bar.transform, SkillTreeButtonPosition, SkillTreeButtonSize);
            homeTeamCombatPowerText = EnsureHomeTeamCombatPowerText(bar.transform);
            EnsureAudioSettingsButton(bar.transform);
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

        private static Text EnsureHomeTeamCombatPowerText(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            Transform panel = parent.Find("HomeTeamCombatPowerPanel");
            if (panel == null)
            {
                GameObject panelObject = new GameObject("HomeTeamCombatPowerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObject.transform.SetParent(parent, false);
                panel = panelObject.transform;
            }

            RectTransform panelRect = panel as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = HomeTeamCombatPowerPanelPosition;
                panelRect.sizeDelta = HomeTeamCombatPowerPanelSize;
            }

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panel.gameObject.AddComponent<Image>();
            }

            Sprite frameSprite = LoadSpriteResource(HomeTeamCombatPowerFramePath, "HomeTeamCombatPowerFrame");
            panelImage.sprite = frameSprite;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
            panelImage.color = frameSprite != null ? Color.white : new Color(0.018f, 0.026f, 0.036f, 0.88f);
            panelImage.raycastTarget = false;

            Outline panelOutline = panel.GetComponent<Outline>();
            if (panelOutline == null)
            {
                panelOutline = panel.gameObject.AddComponent<Outline>();
            }

            panelOutline.enabled = frameSprite == null;
            panelOutline.effectColor = new Color(1f, 0.82f, 0.36f, 0.78f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            Text text = panel.Find("HomeTeamCombatPowerText")?.GetComponent<Text>();
            if (text == null)
            {
                Transform legacyText = parent.Find("HomeTeamCombatPowerText");
                text = legacyText != null ? legacyText.GetComponent<Text>() : null;
                if (text != null)
                {
                    text.transform.SetParent(panel, false);
                }
            }

            if (text == null)
            {
                text = CreateUiText(
                    "HomeTeamCombatPowerText",
                    panel,
                    "チーム戦闘力 0",
                    28,
                    FontStyle.Bold,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    HomeTeamCombatPowerTextPosition,
                    HomeTeamCombatPowerTextSize,
                    new Color(0.92f, 1f, 0.82f, 1f),
                    TextAnchor.MiddleCenter);
                AddTextShadow(text, new Color(0f, 0f, 0f, 0.92f), new Vector2(2f, -2f));
            }

            RectTransform rectTransform = text.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = HomeTeamCombatPowerTextPosition;
                rectTransform.sizeDelta = HomeTeamCombatPowerTextSize;
            }

            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = 28;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.92f, 1f, 0.82f, 1f);
            text.raycastTarget = false;
            text.transform.SetAsLastSibling();
            panel.SetAsLastSibling();
            return text;
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
                homeTeamCombatPowerText = homeTeamCombatPowerText != null
                    ? homeTeamCombatPowerText
                    : existingStoneBar.Find("HomeTeamCombatPowerText")?.GetComponent<Text>();
                if (homeTeamCombatPowerText == null)
                {
                    homeTeamCombatPowerText = EnsureHomeTeamCombatPowerText(existingStoneBar);
                }

                EnsurePermanentUpgradeShortcutButton(existingStoneBar);
                EnsureSkillTreeShortcutButton(existingStoneBar);
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
                permanentUpgradeStatusText == null &&
                skillTreeButtonText == null &&
                skillTreeButton == null &&
                homeTeamCombatPowerText == null)
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

                if (homeTeamCombatPowerText != null)
                {
                    homeTeamCombatPowerText.text = "チーム戦闘力 -";
                }

                RefreshPlayerBadgeDisplay(null);

                ApplyHomeGuideDisplay(null);

                if (homeQuestButtonText != null)
                {
                    homeQuestButtonText.text = "クエスト";
                }

                if (homeShopButtonText != null)
                {
                    homeShopButtonText.text = PaidShopLabel;
                }

                if (skillTreeButtonText != null)
                {
                    skillTreeButtonText.text = SkillTreeLabel;
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

                if (skillTreeButton != null)
                {
                    skillTreeButton.interactable = false;
                }

                RefreshHomeHeroShowcase(profile);
                ApplyHomeTutorialFocus(null);
                RefreshFirstSummonTutorialPulse(null, existingMenuRoot);
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

            if (homeTeamCombatPowerText != null)
            {
                homeTeamCombatPowerText.text = BuildHomeTeamCombatPowerText(profile);
            }

            RefreshPlayerBadgeDisplay(profile);

            ApplyHomeGuideDisplay(profile);

            if (homeQuestButtonText != null)
            {
                homeQuestButtonText.text = DailyRewardService.HasClaimableQuest(profile, DateTime.Now) ? "クエスト!" : "クエスト";
            }

            if (homeShopButtonText != null)
            {
                homeShopButtonText.text = PaidShopLabel;
            }

            if (skillTreeButtonText != null)
            {
                skillTreeButtonText.text = SkillTreeLabel;
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

            if (skillTreeButton != null)
            {
                skillTreeButton.interactable = true;
            }

            RefreshHomeHeroShowcase(profile);
            ApplyHomeTutorialFocus(profile);
            RefreshFirstSummonTutorialPulse(profile, existingMenuRoot);
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
                    ? $"Lv.{Mathf.Max(1, profile.Level)} 魂{Mathf.Max(0, profile.RebirthPoints)}"
                    : "Lv.-";
                homePlayerLevelText.gameObject.SetActive(!homePlayerExpDetailsVisible);
            }

            if (homeExpText != null)
            {
                if (profile != null)
                {
                    int reward = profile.GetPendingRebirthPointReward();
                    string rebirthRead = reward > 0
                        ? $" / 転生 +{reward}魂片"
                        : string.Empty;
                    homeExpText.text = $"経験値 {Mathf.Max(0, profile.Exp):N0}/{Mathf.Max(1, profile.GetRequiredExpForNextLevel()):N0}{rebirthRead}";
                }
                else
                {
                    homeExpText.text = "経験値 -/-";
                }

                homeExpText.gameObject.SetActive(homePlayerExpDetailsVisible);
            }
        }

        private void ApplyHomeGuideDisplay(PlayerProfile profile)
        {
            StoryTutorialEvent tutorialEvent = profile != null
                ? StoryTutorialService.GetNextEvent(profile, "HomeScene")
                : null;
            bool shouldShowTutorialEvent = ShouldShowHomeGuideEvent(profile, tutorialEvent);
            bool hasClaimableQuestReward = profile != null &&
                DailyRewardService.GetClaimableQuestCount(profile, DateTime.Now) > 0;
            bool shouldShowGuidePanel = !IsDailyQuestListOpen() && (shouldShowTutorialEvent || hasClaimableQuestReward);
            SetHomeGuidePanelVisible(shouldShowGuidePanel);
            if (!shouldShowGuidePanel)
            {
                return;
            }

            string title;
            string body;
            string footer;
            if (profile == null)
            {
                title = "読込中";
                body = "ようこそ。冒険の準備をしています。";
                footer = "次の挑戦: -";
            }
            else if (shouldShowTutorialEvent)
            {
                title = string.IsNullOrEmpty(tutorialEvent.Title) ? "チュートリアル" : tutorialEvent.Title;
                body = tutorialEvent.Body;
                footer = ResolveHomeTutorialFooter(profile, tutorialEvent);
            }
            else
            {
                SplitGuideText(BuildHomeGuideText(profile), out title, out body);
                footer = hasClaimableQuestReward
                    ? "クエストから報酬を受け取りましょう"
                    : "次の探索: " + BuildNextDungeonLabel(Mathf.Max(1, profile.HighestFloor + 1));
            }

            if (homeGuideBadgeText != null)
            {
                homeGuideBadgeText.text = string.Empty;
                homeGuideBadgeText.gameObject.SetActive(false);
            }

            ApplyHomeGuideLayout(true);

            if (homeGuideCharacterImage != null)
            {
                homeGuideCharacterImage.gameObject.SetActive(true);
            }

            if (homeGuideTitleText != null)
            {
                homeGuideTitleText.text = title;
            }

            if (homeGuideText != null)
            {
                homeGuideText.text = body;
            }

            if (homeNextFloorText != null)
            {
                homeNextFloorText.text = footer;
            }
        }

        private void SetHomeGuidePanelVisible(bool visible)
        {
            GameObject panelObject = ResolveHomeGuidePanelObject();
            if (panelObject != null)
            {
                panelObject.SetActive(visible);
            }

            if (!visible && homeGuideButton != null)
            {
                homeGuideButton.interactable = false;
            }
        }

        private GameObject ResolveHomeGuidePanelObject()
        {
            if (homeGuideButton != null)
            {
                return homeGuideButton.gameObject;
            }

            if (homeGuideTitleText != null && homeGuideTitleText.transform.parent != null)
            {
                return homeGuideTitleText.transform.parent.gameObject;
            }

            if (homeGuideText != null && homeGuideText.transform.parent != null)
            {
                return homeGuideText.transform.parent.gameObject;
            }

            if (homeNextFloorText != null && homeNextFloorText.transform.parent != null)
            {
                return homeNextFloorText.transform.parent.gameObject;
            }

            return null;
        }

        private bool IsDailyQuestListOpen()
        {
            return dailyQuestListRoot != null && dailyQuestListRoot.activeInHierarchy;
        }

        private void ApplyHomeGuideLayout(bool showCharacter)
        {
            ConfigureGuideRect(
                homeGuideTitleText != null ? homeGuideTitleText.transform as RectTransform : null,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                showCharacter ? new Vector2(615f, -48f) : new Vector2(585f, -48f),
                showCharacter ? new Vector2(560f, 54f) : new Vector2(620f, 54f));
            ConfigureGuideRect(
                homeGuideText != null ? homeGuideText.transform as RectTransform : null,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                showCharacter ? new Vector2(615f, 18f) : new Vector2(585f, 18f),
                showCharacter ? new Vector2(560f, 128f) : new Vector2(670f, 128f));
            ConfigureGuideRect(
                homeNextFloorText != null ? homeNextFloorText.transform as RectTransform : null,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f),
                showCharacter ? new Vector2(615f, 42f) : new Vector2(585f, 42f),
                showCharacter ? new Vector2(560f, 42f) : new Vector2(620f, 42f));
        }

        private static void SplitGuideText(string guideText, out string title, out string body)
        {
            string safeText = string.IsNullOrEmpty(guideText)
                ? "探索準備\n今日の冒険を始めましょう。"
                : guideText;
            int lineBreakIndex = safeText.IndexOf('\n');
            if (lineBreakIndex < 0)
            {
                title = "探索準備";
                body = safeText;
                return;
            }

            title = safeText.Substring(0, lineBreakIndex).Trim();
            body = safeText.Substring(lineBreakIndex + 1).Trim();
            if (string.IsNullOrEmpty(title))
            {
                title = "探索準備";
            }

            if (string.IsNullOrEmpty(body))
            {
                body = "今日の冒険を始めましょう。";
            }
        }

        private static string ResolveHomeTutorialFooter(PlayerProfile profile, StoryTutorialEvent tutorialEvent)
        {
            if (tutorialEvent != null &&
                string.Equals(tutorialEvent.EventId, StoryTutorialService.HintHomeGuideComplete, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (CanAdvanceHomeGuidePanel(profile, tutorialEvent))
            {
                return "パネルをタップして続ける";
            }

            string actionLabel = ResolveHomeTutorialActionLabel(tutorialEvent?.TargetKey ?? string.Empty);
            return string.IsNullOrEmpty(actionLabel)
                ? "案内に沿って操作してください"
                : "次の操作: " + actionLabel;
        }

        private void RefreshHomeHeroShowcase(PlayerProfile profile)
        {
            Sprite sprite = ResolveHomeHeroSprite(profile);
            if (homeHeroImage != null)
            {
                homeHeroImage.gameObject.SetActive(sprite != null);
                homeHeroImage.sprite = sprite;
                homeHeroImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
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
            if (leadMonster == null)
            {
                return null;
            }

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
            if (!ShouldShowHomeGuideEvent(profile, activeEvent))
            {
                activeEvent = null;
            }

            if (!profile.HasCompletedTutorial)
            {
                if (profile.TutorialStepId == StoryTutorialService.StepWakeup)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryPrologueWakeup);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWakeup);
                    SaveHomeTutorialProgressIfNeeded(changed);
                    return false;
                }

                if (profile.TutorialStepId == StoryTutorialService.StepFirstExplorationIntro)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryFirstExplorationIntro);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepFirstExplorationIntro);
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
            if (!ShouldShowHomeGuideEvent(profile, activeEvent))
            {
                activeEvent = null;
            }

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
                else if (profile.TutorialStepId == StoryTutorialService.StepFirstExplorationIntro)
                {
                    changed |= StoryTutorialService.MarkStorySeen(profile, StoryTutorialService.StoryFirstExplorationIntro);
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepFirstExplorationIntro);
                }
                else if (profile.TutorialStepId == StoryTutorialService.StepWrapUp)
                {
                    changed |= StoryTutorialService.AdvanceTutorial(profile, StoryTutorialService.StepWrapUp);
                }
                else if (string.IsNullOrEmpty(activeEvent.TargetKey))
                {
                    changed |= StoryTutorialService.MarkHintSeen(profile, activeEvent.EventId);
                    if (activeEvent.EventId == StoryTutorialService.HintHomeGuideComplete)
                    {
                        changed |= StoryTutorialService.CompleteTutorial(profile);
                    }
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
            if (!ShouldShowHomeGuideEvent(profile, activeEvent))
            {
                activeEvent = null;
            }

            bool guideCanAdvance = CanAdvanceHomeGuidePanel(profile, activeEvent);
            bool shouldFocusQuestReward = profile != null &&
                profile.HasCompletedTutorial &&
                DailyRewardService.GetClaimableQuestCount(profile, DateTime.Now) > 0;
            if (IsDailyQuestListOpen())
            {
                if (homeGuideButton != null)
                {
                    homeGuideButton.interactable = false;
                }

                HideHomeTutorialFocus();
                return;
            }

            if (profile != null && profile.HasCompletedTutorial && !shouldFocusQuestReward && activeEvent == null)
            {
                if (homeGuideButton != null)
                {
                    homeGuideButton.interactable = false;
                }

                if (menuRoot != null)
                {
                    SetTutorialShortcutButtonsVisible(menuRoot, true);
                    SetHomeTutorialActionButtonsInteractable(menuRoot, true, string.Empty);
                }

                HideHomeTutorialFocus();
                return;
            }

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
            string targetButtonName = shouldFocusQuestReward
                ? "QuestButton"
                : ResolveHomeTutorialButtonName(activeEvent?.TargetKey ?? string.Empty);
            SetTutorialShortcutButtonsVisible(menuRoot, !blocksInput);
            SetHomeTutorialActionButtonsInteractable(menuRoot, !blocksInput, targetButtonName);

            if ((activeEvent == null || !activeEvent.IsValid) && !shouldFocusQuestReward)
            {
                HideHomeTutorialFocus();
                return;
            }

            if (homeGuideButton != null)
            {
                homeGuideButton.transform.SetAsLastSibling();
            }

            Transform focusTarget = null;
            string focusLabel = string.Empty;
            if (!string.IsNullOrEmpty(targetButtonName))
            {
                focusTarget = FindDescendant(menuRoot, targetButtonName);
                focusLabel = string.Equals(targetButtonName, "GachaButton", StringComparison.Ordinal)
                    ? string.Empty
                    : "ここをタップ";
            }
            else if (guideCanAdvance)
            {
                HideHomeTutorialFocus();
                return;
            }

            if (focusTarget == null)
            {
                HideHomeTutorialFocus();
                return;
            }

            if (string.Equals(targetButtonName, "GachaButton", StringComparison.Ordinal))
            {
                HideHomeTutorialFocus();
                BindHomeTutorialTargetVisual(focusTarget);
                return;
            }

            if (string.Equals(targetButtonName, "EquipmentButton", StringComparison.Ordinal) &&
                IsHomeEquipmentHintTarget(activeEvent.TargetKey))
            {
                HideHomeTutorialFocus();
                ResetHomeTutorialTargetVisual();
                return;
            }

            if (string.Equals(targetButtonName, "MonsterDexButton", StringComparison.Ordinal) &&
                string.Equals(activeEvent.TargetKey, "home.dex", StringComparison.Ordinal))
            {
                HideHomeTutorialFocus();
                ResetHomeTutorialTargetVisual();
                return;
            }

            if (string.Equals(targetButtonName, "FusionButton", StringComparison.Ordinal) &&
                string.Equals(activeEvent.TargetKey, "home.fusion", StringComparison.Ordinal))
            {
                HideHomeTutorialFocus();
                ResetHomeTutorialTargetVisual();
                return;
            }

            if (string.Equals(targetButtonName, "GoldShopNavButton", StringComparison.Ordinal) &&
                string.Equals(activeEvent.TargetKey, "home.shop", StringComparison.Ordinal))
            {
                HideHomeTutorialFocus();
                ResetHomeTutorialTargetVisual();
                return;
            }

            ShowHomeTutorialFocus(menuRoot, focusTarget, focusLabel);
        }

        private static void SetTutorialShortcutButtonsVisible(Transform menuRoot, bool visible)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            SetNamedChildVisible(menuRoot, "GoldShopButton", visible && MonetizationFeatureFlags.StorefrontEnabled);
            SetNamedChildVisible(
                menuRoot,
                "PermanentUpgradeButton",
                visible && (MonetizationFeatureFlags.StorefrontEnabled || HasManageablePermanentUpgrade(profile)));
            SetNamedChildVisible(menuRoot, "SkillTreeButton", visible);
            SetNamedChildVisible(menuRoot, "QuestButton", visible);
        }

        private static bool HasManageablePermanentUpgrade(PlayerProfile profile)
        {
            return profile != null &&
                (profile.HasAutoRepeatFloorUpgrade ||
                 profile.HasAutoSellEquipmentUpgrade ||
                 profile.HasAutoReleaseMonsterUpgrade);
        }

        private static void SetNamedChildVisible(Transform root, string objectName, bool visible)
        {
            Transform target = FindDescendant(root, objectName);
            if (target != null)
            {
                target.gameObject.SetActive(visible);
            }
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
                    profile.TutorialStepId == StoryTutorialService.StepFirstExplorationIntro ||
                    profile.TutorialStepId == StoryTutorialService.StepWrapUp ||
                    string.IsNullOrEmpty(activeEvent.TargetKey);
            }

            return StoryTutorialService.IsChapterStoryEvent(activeEvent.EventId) ||
                string.IsNullOrEmpty(activeEvent.TargetKey);
        }

        private static bool ShouldShowHomeGuideEvent(PlayerProfile profile, StoryTutorialEvent tutorialEvent)
        {
            if (profile == null || tutorialEvent == null || !tutorialEvent.IsValid)
            {
                return false;
            }

            if (!profile.HasCompletedTutorial)
            {
                return true;
            }

            return StoryTutorialService.IsChapterStoryEvent(tutorialEvent.EventId) ||
                string.Equals(tutorialEvent.EventId, StoryTutorialService.HintFusionInheritance, StringComparison.Ordinal);
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
                case "equipment.first_item":
                case "equipment.auto_equip":
                case "equipment.quality_label":
                case "equipment.enhance_button":
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

        private static string ResolveHomeTutorialActionLabel(string targetKey)
        {
            switch (targetKey)
            {
                case "home.battle":
                    return "バトルを開く";
                case "home.formation":
                    return "編成を開く";
                case "home.gacha":
                    return "召喚を開く";
                case "home.equipment":
                case "equipment.first_item":
                case "equipment.auto_equip":
                case "equipment.quality_label":
                case "equipment.enhance_button":
                    return "装備を開く";
                case "home.fusion":
                    return "配合を開く";
                case "home.dex":
                    return "図鑑を開く";
                case "home.shop":
                    return "商店を開く";
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

            if (homeTutorialFocusRoot.transform.parent != menuRoot)
            {
                homeTutorialFocusRoot.transform.SetParent(menuRoot, false);
            }

            RectTransform menuRect = menuRoot as RectTransform;
            RectTransform targetRect = targetTransform as RectTransform;
            Vector2 targetCenter = Vector2.zero;
            Vector2 targetSize = new Vector2(180f, 120f);
            if (menuRect != null && targetRect != null)
            {
                Vector3[] corners = new Vector3[4];
                targetRect.GetWorldCorners(corners);
                Vector2 min = menuRect.InverseTransformPoint(corners[0]);
                Vector2 max = min;
                for (int i = 1; i < corners.Length; i += 1)
                {
                    Vector2 localCorner = menuRect.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, localCorner);
                    max = Vector2.Max(max, localCorner);
                }

                targetCenter = (min + max) * 0.5f;
                targetSize = max - min;
            }

            bool shouldCenterOnQuestButton = string.Equals(targetTransform.name, "QuestButton", StringComparison.Ordinal);
            Vector2 focusPadding = shouldCenterOnQuestButton
                ? Vector2.zero
                : new Vector2(32f, 32f);
            Vector2 focusSize = new Vector2(
                Mathf.Max(64f, targetSize.x + focusPadding.x),
                Mathf.Max(64f, targetSize.y + focusPadding.y));
            Vector2 focusCenter = ClampHomeTutorialFocusCenter(
                menuRect,
                targetCenter,
                focusSize,
                shouldCenterOnQuestButton ? 1f : 1.06f);

            focusRect.anchorMin = new Vector2(0.5f, 0.5f);
            focusRect.anchorMax = new Vector2(0.5f, 0.5f);
            focusRect.pivot = new Vector2(0.5f, 0.5f);
            focusRect.anchoredPosition = focusCenter;
            focusRect.sizeDelta = focusSize;

            if (homeTutorialFocusText != null)
            {
                RectTransform textRect = homeTutorialFocusText.transform as RectTransform;
                if (textRect != null)
                {
                    float targetHeight = targetSize.y;
                    float targetWidth = targetSize.x;
                    textRect.anchoredPosition = new Vector2(0f, targetHeight * 0.5f + 44f);
                    textRect.sizeDelta = new Vector2(Mathf.Max(220f, targetWidth + 40f), 44f);
                }

                homeTutorialFocusText.text = label;
            }

            BindHomeTutorialTargetVisual(targetTransform);

            homeTutorialFocusRoot.SetActive(true);
            homeTutorialFocusRoot.transform.SetAsLastSibling();
            focusRect.localScale = Vector3.one;
        }

        private static Vector2 ClampHomeTutorialFocusCenter(RectTransform parentRect, Vector2 center, Vector2 size, float clampScale = 1.06f)
        {
            if (parentRect == null)
            {
                return center;
            }

            Rect rect = parentRect.rect;
            Vector2 scaledHalfSize = size * (0.5f * Mathf.Max(1f, clampScale));
            if (scaledHalfSize.x * 2f <= rect.width)
            {
                center.x = Mathf.Clamp(center.x, rect.xMin + scaledHalfSize.x, rect.xMax - scaledHalfSize.x);
            }

            if (scaledHalfSize.y * 2f <= rect.height)
            {
                center.y = Mathf.Clamp(center.y, rect.yMin + scaledHalfSize.y, rect.yMax - scaledHalfSize.y);
            }

            return center;
        }

        private void EnsureHomeTutorialFocus(Transform menuRoot)
        {
            if (menuRoot == null)
            {
                return;
            }

            Transform existing = FindDescendant(menuRoot, "HomeTutorialFocus");
            if (homeTutorialFocusRoot == null)
            {
                homeTutorialFocusRoot = existing != null
                    ? existing.gameObject
                    : new GameObject("HomeTutorialFocus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            }

            if (homeTutorialFocusRoot.transform.parent == null)
            {
                homeTutorialFocusRoot.transform.SetParent(menuRoot, false);
            }

            Image image = homeTutorialFocusRoot.GetComponent<Image>();
            if (image == null)
            {
                image = homeTutorialFocusRoot.AddComponent<Image>();
            }

            image.color = new Color(1f, 0.86f, 0.30f, 0f);
            image.raycastTarget = false;

            Outline outline = homeTutorialFocusRoot.GetComponent<Outline>();
            if (outline == null)
            {
                outline = homeTutorialFocusRoot.AddComponent<Outline>();
            }

            outline.enabled = false;

            EnsureHomeTutorialFocusFrame(homeTutorialFocusRoot.transform);

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

        private void EnsureHomeTutorialFocusFrame(Transform focusRoot)
        {
            if (focusRoot == null)
            {
                return;
            }

            homeTutorialFocusFrameImages.Clear();
            RemoveHomeTutorialFocusBar(focusRoot, "Top");
            RemoveHomeTutorialFocusBar(focusRoot, "Bottom");
            RemoveHomeTutorialFocusBar(focusRoot, "Left");
            RemoveHomeTutorialFocusBar(focusRoot, "Right");

            Transform existing = focusRoot.Find("HomeTutorialFocusFrame");
            Image frame = existing != null ? existing.GetComponent<Image>() : null;
            if (frame == null)
            {
                GameObject frameObject = new GameObject(
                    "HomeTutorialFocusFrame",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                frameObject.transform.SetParent(focusRoot, false);
                frame = frameObject.GetComponent<Image>();
            }

            RectTransform rect = frame.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Sprite frameSprite = LoadSpriteResource(TutorialSummonHighlightFramePath, "TutorialSummonHighlightFrameImage2");
            frame.sprite = frameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.color = frameSprite != null
                ? Color.white
                : new Color(1f, 0.78f, 0.10f, 0.90f);
            frame.raycastTarget = false;
            frame.transform.SetAsFirstSibling();
            homeTutorialFocusFrameImages.Add(frame);
        }

        private static void RemoveHomeTutorialFocusBar(Transform focusRoot, string suffix)
        {
            Transform existing = focusRoot.Find($"HomeTutorialFocus{suffix}");
            if (existing != null)
            {
                DestroySceneObject(existing.gameObject);
            }
        }

        private void AnimateHomeTutorialFocus()
        {
            if (homeTutorialFocusRoot == null || !homeTutorialFocusRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
            float alpha = Mathf.Lerp(0.38f, 1f, pulse);
            Color frameColor = new Color(1f, Mathf.Lerp(0.58f, 0.88f, pulse), 0.16f, alpha);
            for (int i = 0; i < homeTutorialFocusFrameImages.Count; i += 1)
            {
                Image frameImage = homeTutorialFocusFrameImages[i];
                if (frameImage != null)
                {
                    frameImage.color = frameImage.sprite != null
                        ? new Color(1f, 1f, 1f, alpha)
                        : frameColor;
                }
            }

            RectTransform focusRect = homeTutorialFocusRoot.transform as RectTransform;
            if (focusRect != null)
            {
                float scale = Mathf.Lerp(1f, 1.055f, pulse);
                focusRect.localScale = new Vector3(scale, scale, 1f);
            }

            if (homeTutorialFocusText != null)
            {
                Color labelColor = homeTutorialFocusText.color;
                labelColor.a = Mathf.Lerp(0.72f, 1f, pulse);
                homeTutorialFocusText.color = labelColor;
            }

            if (homeTutorialTargetOutline != null)
            {
                homeTutorialTargetOutline.enabled = true;
                homeTutorialTargetOutline.effectColor = new Color(1f, 0.68f, 0.12f, Mathf.Lerp(0.58f, 1f, pulse));
                homeTutorialTargetOutline.effectDistance = new Vector2(
                    Mathf.Lerp(6f, 11f, pulse),
                    Mathf.Lerp(-6f, -11f, pulse));
            }

            if (homeTutorialTargetGraphic != null)
            {
                homeTutorialTargetGraphic.color = Color.Lerp(
                    Color.white,
                    new Color(1f, 0.86f, 0.46f, 1f),
                    pulse * 0.48f);
                float targetScale = Mathf.Lerp(1f, 1.075f, pulse);
                homeTutorialTargetGraphic.rectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
            }
        }

        private void BindHomeTutorialTargetVisual(Transform targetTransform)
        {
            Transform visualTransform = targetTransform != null
                ? targetTransform.Find("BottomNavSegmentVisual")
                : null;
            if (visualTransform == null && targetTransform != null)
            {
                visualTransform = targetTransform.Find(targetTransform.name + "Visual");
            }

            Graphic targetGraphic = visualTransform != null
                ? visualTransform.GetComponent<Graphic>()
                : targetTransform?.GetComponent<Graphic>();
            if (homeTutorialTargetGraphic == targetGraphic && homeTutorialTargetOutline != null)
            {
                return;
            }

            ResetHomeTutorialTargetVisual();
            homeTutorialTargetGraphic = targetGraphic;
            if (homeTutorialTargetGraphic == null)
            {
                return;
            }

            homeTutorialTargetOutline = homeTutorialTargetGraphic.GetComponent<Outline>();
            if (homeTutorialTargetOutline == null)
            {
                homeTutorialTargetOutline = homeTutorialTargetGraphic.gameObject.AddComponent<Outline>();
            }

            homeTutorialTargetOutline.useGraphicAlpha = false;
            homeTutorialTargetOutline.enabled = true;
        }

        private void RefreshFirstSummonTutorialPulse(PlayerProfile profile, Transform menuRoot)
        {
            StoryTutorialEvent activeEvent = profile != null
                ? StoryTutorialService.GetNextEvent(profile, "HomeScene")
                : null;
            if (!ShouldShowHomeGuideEvent(profile, activeEvent))
            {
                activeEvent = null;
            }

            bool shouldPulseFirstSummon = profile != null &&
                !profile.HasCompletedTutorial &&
                profile.TutorialStepId == StoryTutorialService.StepOpenGacha;
            bool canPulseEarlyOptionalTutorialHint = profile != null && !profile.HasCompletedTutorial;
            bool shouldPulseDex = canPulseEarlyOptionalTutorialHint &&
                activeEvent != null &&
                activeEvent.EventId == StoryTutorialService.HintDex &&
                string.Equals(activeEvent.TargetKey, "home.dex", StringComparison.Ordinal);
            bool shouldPulseEquipment = canPulseEarlyOptionalTutorialHint &&
                activeEvent != null &&
                IsHomeEquipmentHintTarget(activeEvent.TargetKey);
            bool shouldPulseFusion = activeEvent != null &&
                string.Equals(activeEvent.TargetKey, "home.fusion", StringComparison.Ordinal) &&
                ((canPulseEarlyOptionalTutorialHint && activeEvent.EventId == StoryTutorialService.HintFusion) ||
                 activeEvent.EventId == StoryTutorialService.HintFusionInheritance);
            bool shouldPulseShop = canPulseEarlyOptionalTutorialHint &&
                activeEvent != null &&
                activeEvent.EventId == StoryTutorialService.HintShop &&
                string.Equals(activeEvent.TargetKey, "home.shop", StringComparison.Ordinal);
            if ((!shouldPulseFirstSummon && !shouldPulseDex && !shouldPulseEquipment && !shouldPulseFusion && !shouldPulseShop) || menuRoot == null)
            {
                if (homeFirstSummonPulseRoot != null)
                {
                    homeFirstSummonPulseRoot.SetActive(false);
                }

                ResetHomeTutorialTargetVisual();
                return;
            }

            int targetIndex = shouldPulseShop ? 0 : shouldPulseEquipment ? 3 : shouldPulseFusion ? 4 : shouldPulseDex ? 2 : 1;
            float offsetX = shouldPulseEquipment
                ? EquipmentHighlightOffsetX
                : shouldPulseFusion
                    ? FusionHighlightOffsetX
                    : shouldPulseDex
                        ? DexHighlightOffsetX
                        : shouldPulseShop
                            ? ShopHighlightOffsetX
                            : FirstSummonHighlightOffsetX;
            EnsureFirstSummonTutorialPulse(menuRoot, targetIndex, offsetX);
            if (homeFirstSummonPulseRoot != null)
            {
                homeFirstSummonPulseRoot.SetActive(true);
                homeFirstSummonPulseRoot.transform.SetAsLastSibling();
            }

            Transform targetButton = FindDescendant(menuRoot, shouldPulseShop ? "GoldShopNavButton" : shouldPulseEquipment ? "EquipmentButton" : shouldPulseFusion ? "FusionButton" : shouldPulseDex ? "MonsterDexButton" : "GachaButton");
            if (targetButton != null && shouldPulseFirstSummon)
            {
                BindHomeTutorialTargetVisual(targetButton);
            }
            else if (shouldPulseDex || shouldPulseEquipment || shouldPulseFusion || shouldPulseShop)
            {
                ResetHomeTutorialTargetVisual();
            }
        }

        private void EnsureFirstSummonTutorialPulse(Transform menuRoot, int navIndex, float offsetX)
        {
            if (menuRoot == null)
            {
                return;
            }

            Transform existing = menuRoot.Find("HomeFirstSummonPulse");
            if (homeFirstSummonPulseRoot == null || homeFirstSummonPulseRoot.transform.parent != menuRoot)
            {
                homeFirstSummonPulseRoot = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "HomeFirstSummonPulse",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                homeFirstSummonPulseRoot.transform.SetParent(menuRoot, false);
            }

            RectTransform pulseRect = homeFirstSummonPulseRoot.transform as RectTransform;
            ConfigureBottomAnchoredRect(
                pulseRect,
                new Vector2(GetBottomNavButtonX(navIndex) + offsetX, HomeBottomNavCenterY),
                HomeBottomNavButtonHitSize + new Vector2(52f, 46f));

            homeFirstSummonPulseFrame = homeFirstSummonPulseRoot.GetComponent<Image>();
            if (homeFirstSummonPulseFrame == null)
            {
                homeFirstSummonPulseFrame = homeFirstSummonPulseRoot.AddComponent<Image>();
            }

            Sprite frameSprite = LoadSpriteResource(TutorialSummonHighlightFramePath, "TutorialSummonHighlightFrameImage2");
            homeFirstSummonPulseFrame.sprite = frameSprite;
            homeFirstSummonPulseFrame.type = Image.Type.Simple;
            homeFirstSummonPulseFrame.preserveAspect = false;
            homeFirstSummonPulseFrame.color = frameSprite != null
                ? Color.white
                : new Color(1f, 0.78f, 0.10f, 0.90f);
            homeFirstSummonPulseFrame.raycastTarget = false;

            RemoveFirstSummonPulseBar("Top");
            RemoveFirstSummonPulseBar("Bottom");
            RemoveFirstSummonPulseBar("Left");
            RemoveFirstSummonPulseBar("Right");
        }

        private void RemoveFirstSummonPulseBar(string suffix)
        {
            if (homeFirstSummonPulseRoot == null)
            {
                return;
            }

            Transform existing = homeFirstSummonPulseRoot.transform.Find($"HomeFirstSummonPulse{suffix}");
            if (existing != null)
            {
                DestroySceneObject(existing.gameObject);
            }
        }

        private void AnimateFirstSummonTutorialPulse()
        {
            if (homeFirstSummonPulseRoot == null || !homeFirstSummonPulseRoot.activeInHierarchy)
            {
                return;
            }

            homeFirstSummonPulseRoot.transform.SetAsLastSibling();
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.6f);
            if (homeFirstSummonPulseFrame != null)
            {
                homeFirstSummonPulseFrame.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, pulse));
            }

            float scale = Mathf.Lerp(0.985f, 1.045f, pulse);
            homeFirstSummonPulseRoot.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ResetHomeTutorialTargetVisual()
        {
            if (homeTutorialTargetOutline != null)
            {
                homeTutorialTargetOutline.enabled = false;
            }

            if (homeTutorialTargetGraphic != null)
            {
                homeTutorialTargetGraphic.color = Color.white;
                homeTutorialTargetGraphic.rectTransform.localScale = Vector3.one;
            }

            homeTutorialTargetOutline = null;
            homeTutorialTargetGraphic = null;
        }

        private void HideHomeTutorialFocus()
        {
            if (homeTutorialFocusRoot != null)
            {
                homeTutorialFocusRoot.SetActive(false);
                homeTutorialFocusRoot.transform.localScale = Vector3.one;
            }

            ResetHomeTutorialTargetVisual();
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
                // Fusion hints stay active until Luse explains them in the fusion scene.
                return false;
            }

            if (targetKey == "home.dex")
            {
                // Keep the hint active until the player follows Luse's guide inside the dex.
                return false;
            }

            if (targetKey == "home.shop")
            {
                // Keep the hint active until the player returns from Luse's shop guide.
                return false;
            }

            return false;
        }

        private static bool IsHomeEquipmentHintTarget(string targetKey)
        {
            return targetKey == "equipment.first_item" ||
                targetKey == "equipment.auto_equip" ||
                targetKey == "equipment.quality_label" ||
                targetKey == "equipment.enhance_button";
        }

        private static string BuildHomeGuideText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "よぉし！\n今日の冒険を始めましょう。";
            }

            StoryTutorialEvent tutorialEvent = !profile.HasCompletedTutorial
                ? StoryTutorialService.GetNextEvent(profile, "HomeScene")
                : null;
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

            return "次の探索へ進みましょう。";
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

        private void EnsureSkillTreeShortcutButton(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("SkillTreeButton");
            if (existing == null)
            {
                skillTreeButton = CreateSkillTreeShortcutButton(parent, SkillTreeButtonPosition, SkillTreeButtonSize);
                return;
            }

            skillTreeButton = existing.GetComponent<Button>();
            skillTreeButtonText = existing.Find("SkillTreeButtonLabel")?.GetComponent<Text>();
            if (skillTreeButton == null || skillTreeButtonText == null)
            {
                DestroySceneObject(existing.gameObject);
                skillTreeButton = CreateSkillTreeShortcutButton(parent, SkillTreeButtonPosition, SkillTreeButtonSize);
                return;
            }

            RectTransform rectTransform = existing as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 1f);
                rectTransform.anchorMax = new Vector2(0.5f, 1f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = SkillTreeButtonPosition;
                rectTransform.sizeDelta = SkillTreeButtonSize;
            }

            skillTreeButton.onClick.RemoveAllListeners();
            skillTreeButton.onClick.AddListener(OpenSkillTreeScene);
            ConfigureSkillTreeShortcutVisual(existing, skillTreeButtonText);
        }

        private Button CreateSkillTreeShortcutButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject root = new GameObject("SkillTreeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Button button = root.GetComponent<Button>();
            button.onClick.AddListener(OpenSkillTreeScene);

            skillTreeButtonText = CreateUiText(
                "SkillTreeButtonLabel",
                root.transform,
                SkillTreeLabel,
                17,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -62f),
                new Vector2(116f, 34f),
                Color.white,
                TextAnchor.MiddleCenter);
            AddTextShadow(skillTreeButtonText, new Color(0f, 0f, 0f, 0.85f), new Vector2(1.4f, -1.4f));

            ConfigureSkillTreeShortcutVisual(root.transform, skillTreeButtonText);
            return button;
        }

        private static void ConfigureSkillTreeShortcutVisual(Transform buttonTransform, Text labelText)
        {
            if (buttonTransform == null)
            {
                return;
            }

            Image image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                Sprite sprite = LoadSpriteResource(SkillTreeButtonIconSpritePath, "SkillTreeButtonImage2");
                image.sprite = sprite;
                image.color = sprite != null ? Color.white : new Color(0.08f, 0.14f, 0.20f, 0.96f);
                image.preserveAspect = true;
                image.raycastTarget = true;
            }

            Button button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
            }

            if (labelText != null)
            {
                RectTransform labelRect = labelText.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = new Vector2(0f, -62f);
                    labelRect.sizeDelta = new Vector2(116f, 34f);
                }

                labelText.text = SkillTreeLabel;
                labelText.fontSize = 17;
                labelText.fontStyle = FontStyle.Bold;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 11;
                labelText.resizeTextMaxSize = 17;
                labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
                labelText.verticalOverflow = VerticalWrapMode.Truncate;
                labelText.color = Color.white;
            }
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
                bool shouldShow = MonetizationFeatureFlags.StorefrontEnabled || HasManageablePermanentUpgrade(profile);
                permanentUpgradeButton.gameObject.SetActive(shouldShow);
                permanentUpgradeButton.interactable = shouldShow && profile != null;
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

            int rebirthReward = profile.GetPendingRebirthPointReward();
            if (rebirthReward > 0)
            {
                permanentUpgradeStatusText.text = $"転生 +{rebirthReward}魂片";
                permanentUpgradeStatusText.color = new Color(0.45f, 1f, 0.78f, 1f);
            }
            else if (profile.RebirthPoints > 0)
            {
                permanentUpgradeStatusText.text = $"魂片 {profile.RebirthPoints}";
                permanentUpgradeStatusText.color = new Color(1f, 0.82f, 0.38f, 1f);
            }
            else if (profile.HasAutoRepeatFloorUpgrade && profile.IsAutoRepeatFloorUpgradeEnabled)
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

        private static bool ApplyGeneratedUiSprite(Image image, string resourcePath, string fallbackName, bool preserveAspect = false)
        {
            if (image == null)
            {
                return false;
            }

            Sprite sprite = LoadSpriteResource(resourcePath, fallbackName);
            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.type = Image.Type.Simple;
            return true;
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
