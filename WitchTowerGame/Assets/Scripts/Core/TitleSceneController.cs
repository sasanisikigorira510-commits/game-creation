using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Home;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Core
{
    [ExecuteAlways]
    public sealed class TitleSceneController : MonoBehaviour
    {
        [Serializable]
        private sealed class FormationMonsterEntry
        {
            public string Id;
            public string Name;
            public string Role;
            public string TexturePath;
            public Color FrameColor;

            public FormationMonsterEntry(string id, string name, string role, string texturePath, Color frameColor)
            {
                Id = id;
                Name = name;
                Role = role;
                TexturePath = texturePath;
                FrameColor = frameColor;
            }
        }

        private sealed class FormationSlotView
        {
            public Image Frame;
            public Text SlotLabel;
            public Text NameLabel;
            public Text RoleLabel;
            public RawImage Portrait;
            public Button Button;
        }

        private sealed class FormationRosterCardView
        {
            public Image Frame;
            public Text NameLabel;
            public Text RoleLabel;
            public Text StateLabel;
            public RawImage Portrait;
            public Button Button;
        }

        private enum EquipmentMonsterPickerSortMode
        {
            Default,
            Level,
            Favorite
        }

        private enum EquipmentInventoryFilter
        {
            All,
            Weapon,
            Armor,
            Accessory
        }

        private enum EquipmentInventorySortMode
        {
            Default,
            Rarity,
            Power,
            Name
        }

        [SerializeField] private string homeSceneName = "HomeScene";
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string formationSceneName = "FormationScene";
        [SerializeField] private string equipmentSceneName = "EquipmentScene";
        [SerializeField] private string fusionSceneName = "FusionScene";
        [SerializeField] private string gachaSceneName = "GachaScene";

        private static readonly string[] TitleOverlayObjectNames =
        {
            "TitleBackgroundShade",
            "TitleGlowLeft",
            "TitleGlowRight",
            "TitleBandTop",
            "TitleBandBottom",
            "TitleGround",
            "TitleTotemLeft",
            "TitleTotemRight",
            "TitleTopRibbon",
            "TitleBottomRibbon",
            "TitleFrame",
            "Overline",
            "TitleSigil",
            "GameTitle",
            "GameSubtitle",
            "RelicChamber",
            "ActionCard",
            "LoreCard",
            "ContinueButton",
            "Start New RunButton"
        };

        private static readonly FormationMonsterEntry[] FormationRoster =
        {
            new FormationMonsterEntry("dragon_whelp", "ヒナドラ", "幼竜", "FamilyMonsterCards/Dragon/dragon_whelp", new Color(0.95f, 0.45f, 0.28f)),
            new FormationMonsterEntry("flare_drake", "フレアドレイク", "火竜", "FamilyMonsterCards/Dragon/flare_drake", new Color(0.88f, 0.5f, 0.34f)),
            new FormationMonsterEntry("abyss_dragon", "蒼黒竜アビス", "黒焔", "FamilyMonsterCards/Dragon/abyss_dragon", new Color(0.36f, 0.58f, 0.95f)),
            new FormationMonsterEntry("chibi_gear", "チビギア", "小型機", "FamilyMonsterCards/Robot/chibi_gear", new Color(0.55f, 0.8f, 0.9f)),
            new FormationMonsterEntry("armed_droid", "アームドロイド", "戦闘機", "FamilyMonsterCards/Robot/armed_droid", new Color(0.48f, 0.7f, 0.92f)),
            new FormationMonsterEntry("omega_leon", "機皇オメガレオン", "機皇", "FamilyMonsterCards/Robot/omega_leon", new Color(0.92f, 0.76f, 0.42f)),
            new FormationMonsterEntry("rock_golem", "ロックゴーレム", "岩兵", "FamilyMonsterCards/Golem/rock_golem", new Color(0.6f, 0.58f, 0.46f)),
            new FormationMonsterEntry("ore_giant_garm", "鉱石巨人ガルム", "鉱巨", "FamilyMonsterCards/Golem/ore_giant_garm", new Color(0.56f, 0.7f, 0.68f)),
            new FormationMonsterEntry("cosmic_ore_fortress_golem", "宇宙鉱石要塞ゴーレム", "要塞", "FamilyMonsterCards/Golem/cosmic_ore_fortress_golem", new Color(0.56f, 0.62f, 0.9f)),
            new FormationMonsterEntry("apprentice_swordsman", "見習い剣士", "剣士", "FamilyMonsterCards/Swordsman/apprentice_swordsman", new Color(0.78f, 0.58f, 0.42f)),
            new FormationMonsterEntry("holy_armor_leon", "聖鎧剣士レオン", "聖鎧", "FamilyMonsterCards/Swordsman/holy_armor_leon", new Color(0.9f, 0.82f, 0.56f)),
            new FormationMonsterEntry("sword_saint_alvarez", "剣聖アルヴァレス", "剣聖", "FamilyMonsterCards/Swordsman/sword_saint_alvarez", new Color(0.88f, 0.72f, 0.48f)),
            new FormationMonsterEntry("apprentice_mage", "見習い魔導士", "魔導", "FamilyMonsterCards/Mage/apprentice_mage", new Color(0.58f, 0.46f, 0.86f)),
            new FormationMonsterEntry("dark_robe_curse_mage_noah", "黒衣の呪術師ノア", "呪術", "FamilyMonsterCards/Mage/dark_robe_curse_mage_noah", new Color(0.5f, 0.38f, 0.82f)),
            new FormationMonsterEntry("abyss_grand_mage_seraphis", "深淵大魔導セラフィス", "深淵", "FamilyMonsterCards/Mage/abyss_grand_mage_seraphis", new Color(0.72f, 0.46f, 0.94f))
        };

        private const string FormationScreenTexturePath = "FormationUI/FormationScreen";
        private const string EquipmentBackgroundTexturePath = "EquipmentBackgrounds/equipment_scene_background";
        private const string BronzeBladeIconTexturePath = "EquipmentIcons/eq_bronze_blade_icon";
        private const string IronBladeIconTexturePath = "EquipmentIcons/eq_iron_blade_icon";
        private const string GoldBladeIconTexturePath = "EquipmentIcons/eq_gold_blade_icon";
        private const string ClothArmorIconTexturePath = "EquipmentIcons/eq_cloth_armor_icon";
        private const string LeatherArmorIconTexturePath = "EquipmentIcons/eq_leather_armor_icon";
        private const string PlateArmorIconTexturePath = "EquipmentIcons/eq_plate_armor_icon";
        private const string GreenRingIconTexturePath = "EquipmentIcons/eq_green_ring_icon";
        private const string RedRingIconTexturePath = "EquipmentIcons/eq_red_ring_icon";
        private const string VioletPendantIconTexturePath = "EquipmentIcons/eq_violet_pendant_icon";
        private const string FrostGreatswordIconTexturePath = "EquipmentIcons/eq_frost_greatsword_icon";
        private const string IceDragonArmorIconTexturePath = "EquipmentIcons/eq_ice_dragon_armor_icon";
        private const string IceStarTalismanIconTexturePath = "EquipmentIcons/eq_ice_star_talisman_icon";
        private const string Class1EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_1_slot_frame";
        private const string Class2EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_2_slot_frame";
        private const string Class3EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_3_slot_frame";
        private const string Class4EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_4_slot_frame";
        private const string Class5EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_5_slot_frame";
        private const string Class6EquipmentFrameTexturePath = "MonsterCardFrames/monster_class_6_slot_frame";
        private const string Class1MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_1_card_frame";
        private const string Class2MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_2_card_frame";
        private const string Class3MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_3_card_frame";
        private const string Class4MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_4_card_frame";
        private const string Class5MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_5_card_frame";
        private const string Class6MonsterCardFrameTexturePath = "MonsterCardFrames/monster_class_6_card_frame";
        private const string FavoriteHeartFilledTexturePath = "UI/Favorite/FavoriteHeartFilledImage2";
        private const string SafeRelicTexturePath = "EquipmentRelics/relic_safe_ember_icon";
        private const string RiskyRelicTexturePath = "EquipmentRelics/relic_risky_ember_icon";
        private const string VolatileRelicTexturePath = "EquipmentRelics/relic_volatile_ember_icon";
        private const string LockedEquipmentIconTexturePath = "EquipmentUi/ui_lock_locked_icon";
        private const string UnlockedEquipmentIconTexturePath = "EquipmentUi/ui_lock_unlocked_icon";
        private const string EquipmentEnhanceRuneTexturePath = "UI/EquipmentEnhance/EnhanceRuneCircle";
        private const string EquipmentEnhanceSuccessBasePath = "UI/EquipmentEnhance/EnhanceSuccess_";
        private const string EquipmentEnhanceFailBasePath = "UI/EquipmentEnhance/EnhanceFail_";
        private const string EquipmentEnhanceDestroyBasePath = "UI/EquipmentEnhance/EnhanceDestroy_";
        private const int EquipmentEnhanceEffectFrameCount = 8;
        private const float EquipmentEnhanceEffectDuration = 1.35f;
        private const float EquipmentInventoryWidth = 872f;
        private const float EquipmentInventoryPanelHeight = 960f;
        private const float EquipmentInventoryControlsHeight = 104f;
        private const float EquipmentInventoryViewportHeight = EquipmentInventoryPanelHeight - EquipmentInventoryControlsHeight;

        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly FormationSlotView[] slotViews = new FormationSlotView[5];
        private readonly FormationRosterCardView[] rosterViews = new FormationRosterCardView[FormationRoster.Length];
        private readonly int[] assignedMonsterIndices = { 0, 1, 2, 3, 4 };
        private readonly List<Image> equipmentMonsterClassFilterButtonImages = new List<Image>();
        private readonly List<Text> equipmentMonsterClassFilterButtonTexts = new List<Text>();
        private readonly List<Image> equipmentMonsterElementFilterButtonImages = new List<Image>();
        private readonly List<Text> equipmentMonsterElementFilterButtonTexts = new List<Text>();
        private readonly List<int> equipmentMonsterElementFilterValues = new List<int>();
        private readonly List<Image> equipmentInventoryFilterButtonImages = new List<Image>();
        private readonly List<Text> equipmentInventoryFilterButtonTexts = new List<Text>();
        private readonly List<EquipmentInventoryFilter> equipmentInventoryFilterValues = new List<EquipmentInventoryFilter>();

        private GameObject formationPanelRoot;
        private GameObject equipmentSceneRoot;
        private Text formationSummaryText;
        private Text formationHintText;
        private Text floorLabelText;
        private Text equipmentTitleText;
        private Text equipmentGoldText;
        private Text equipmentHeadlineText;
        private Text equipmentSummaryText;
        private Text equipmentBonusSummaryText;
        private Text equippedWeaponText;
        private Text equippedArmorText;
        private Text equippedAccessoryText;
        private Text equipmentMonsterNameText;
        private Text equipmentMonsterMetaText;
        private Image equipmentMonsterPortraitBackdropImage;
        private Image equipmentMonsterPortraitShadowImage;
        private Image equipmentMonsterPortraitImage;
        private GameObject equipmentMonsterPickerOverlayRoot;
        private RectTransform equipmentMonsterPickerListRect;
        private Text equipmentMonsterPickerSummaryText;
        private Text equipmentMonsterPickerSortButtonText;
        private InputField equipmentMonsterSearchInput;
        private Text equipmentInventorySummaryText;
        private Text equipmentInventorySortButtonText;
        private RectTransform equipmentInventoryContentRect;
        private GameObject equipmentEnhanceOverlayRoot;
        private RectTransform equipmentEnhanceOverlayListRect;
        private Text equipmentEnhanceOverlayTitleText;
        private Text equipmentEnhanceOverlayInfoText;
        private Text equipmentEnhanceOverlayResultText;
        private Image equipmentEnhanceDarkOverlayImage;
        private RawImage equipmentEnhanceRuneImage;
        private RawImage equipmentEnhanceItemFrameImage;
        private RawImage equipmentEnhanceItemImage;
        private RawImage equipmentEnhanceEffectImage;
        private RectTransform equipmentEnhanceRuneRect;
        private RectTransform equipmentEnhanceItemFrameRect;
        private RectTransform equipmentEnhanceItemRect;
        private RectTransform equipmentEnhanceEffectRect;
        private Texture2D[] equipmentEnhanceSuccessTextures;
        private Texture2D[] equipmentEnhanceFailTextures;
        private Texture2D[] equipmentEnhanceDestroyTextures;
        private string selectedEquipmentMonsterInstanceId;
        private string selectedEquipmentEnhanceInstanceId;
        private string equipmentLastActionMessage;
        private string equipmentEnhanceLastActionMessage;
        private string equipmentEnhanceTargetTitle;
        private string equipmentEnhanceTargetInfo;
        private string equipmentMonsterSearchQuery = string.Empty;
        private EquipmentEnhancementResultType activeEquipmentEnhanceEffect = EquipmentEnhancementResultType.None;
        private EquipmentMonsterPickerSortMode equipmentMonsterPickerSortMode = EquipmentMonsterPickerSortMode.Default;
        private EquipmentInventoryFilter equipmentInventoryFilter = EquipmentInventoryFilter.All;
        private EquipmentInventorySortMode equipmentInventorySortMode = EquipmentInventorySortMode.Default;
        private float equipmentEnhanceEffectTimer;
        private int equipmentMonsterClassFilter;
        private int equipmentMonsterElementFilter = -1;
        private int selectedSlotIndex;

        private void Start()
        {
            NormalizeCanvasScales();
            SimplifyTitlePresentation();

            if (Application.isPlaying)
            {
                EnsureRuntimeState();
            }

            if (IsEquipmentScene())
            {
                HideEquipmentSceneLegacyUi();
                EnsureEquipmentScene();
                RefreshEquipmentScene();
            }
        }

        private void OnEnable()
        {
            NormalizeCanvasScales();
            if (Application.isPlaying)
            {
                return;
            }

            SimplifyTitlePresentation();
            if (IsEquipmentScene())
            {
                HideEquipmentSceneLegacyUi();
                EnsureEquipmentScene();
                RefreshEquipmentScene();
            }
        }

        private void OnValidate()
        {
            NormalizeCanvasScales();
        }

        private void Update()
        {
            if (equipmentEnhanceOverlayRoot == null)
            {
                return;
            }

            AnimateEquipmentEnhancementEffect();
        }

        public void StartNewGame()
        {
            EnsureRuntimeState();
            var defaultSave = Save.PlayerSaveData.CreateDefault();
            SaveManager.Instance.Save(defaultSave);
            GameManager.Instance.InitializeFromSave(defaultSave);
            SceneManager.LoadScene(homeSceneName);
        }

        public void ContinueGame()
        {
            EnsureRuntimeState();
            SaveManager.Instance.LoadOrCreate();
            GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            SceneManager.LoadScene(homeSceneName);
        }

        public void OpenBattle()
        {
            SceneManager.LoadScene(battleSceneName);
        }

        public void OpenFormation()
        {
            SceneManager.LoadScene(formationSceneName);
        }

        public void OpenEquipment()
        {
            SceneManager.LoadScene(equipmentSceneName);
        }

        public void OpenFusion()
        {
            EnsureRuntimeState();
            SceneManager.LoadScene(fusionSceneName);
        }

        public void OpenGacha()
        {
            EnsureRuntimeState();
            SceneManager.LoadScene(gachaSceneName);
        }

        public void CloseFormation()
        {
            if (formationPanelRoot != null)
            {
                formationPanelRoot.SetActive(false);
            }
        }

        public void ReturnHomeFromEquipment()
        {
            SceneManager.LoadScene(homeSceneName);
        }

        private static void EnsureRuntimeState()
        {
            Application.runInBackground = true;
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureAudioManager();
            ManagerFactory.EnsureUiPresentationCamera();

            if (SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            MasterDataManager.Instance?.Initialize();

            if (GameManager.Instance.PlayerProfile == null && SaveManager.Instance.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        private static void SimplifyTitlePresentation()
        {
            foreach (string objectName in TitleOverlayObjectNames)
            {
                GameObject target = GameObject.Find(objectName);
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        private bool IsEquipmentScene()
        {
            return SceneManager.GetActiveScene().name == equipmentSceneName;
        }

        private static void HideEquipmentSceneLegacyUi()
        {
            string[] objectNames =
            {
                "EquipmentSceneRoot",
                "RuntimeEquipmentSceneRoot",
                "HomeMenuRoot",
                "BattleButton",
                "FormationButton",
                "EquipmentButton",
                "FusionButton"
            };

            for (int i = 0; i < objectNames.Length; i += 1)
            {
                GameObject target;
                while ((target = GameObject.Find(objectNames[i])) != null)
                {
                    target.SetActive(false);
                }
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

        private void EnsureEquipmentScene()
        {
            if (equipmentSceneRoot != null)
            {
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }
            canvas.transform.localScale = Vector3.one;

            Font font = ResolveRuntimeFont();
            equipmentEnhanceSuccessTextures = LoadTextureSequence(EquipmentEnhanceSuccessBasePath, EquipmentEnhanceEffectFrameCount);
            equipmentEnhanceFailTextures = LoadTextureSequence(EquipmentEnhanceFailBasePath, EquipmentEnhanceEffectFrameCount);
            equipmentEnhanceDestroyTextures = LoadTextureSequence(EquipmentEnhanceDestroyBasePath, EquipmentEnhanceEffectFrameCount);
            equipmentSceneRoot = CreateUiObject("RuntimeEquipmentSceneRoot", canvas.transform);
            RectTransform rootRect = equipmentSceneRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = equipmentSceneRoot.AddComponent<Image>();
            rootImage.color = new Color(0.02f, 0.03f, 0.05f, 0.52f);

            RawImage backgroundImage = CreateRawPortrait("EquipmentBackground", equipmentSceneRoot.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            backgroundImage.texture = LoadMonsterTexture(EquipmentBackgroundTexturePath);
            backgroundImage.color = Color.white;
            backgroundImage.transform.SetSiblingIndex(0);

            GameObject panel = CreateUiObject("EquipmentPanel", equipmentSceneRoot.transform);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1040f, 1520f);
            panelRect.anchoredPosition = new Vector2(0f, -8f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);

            GameObject panelAccent = CreateUiObject("EquipmentPanelAccent", panel.transform);
            RectTransform accentRect = panelAccent.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 10f);
            accentRect.anchoredPosition = Vector2.zero;
            Image accentImage = panelAccent.AddComponent<Image>();
            accentImage.color = new Color(0.82f, 0.63f, 0.30f, 1f);

            HomeReturnButtonStyle.Create(equipmentSceneRoot.transform, ReturnHomeFromEquipment);

            equipmentTitleText = CreateText("EquipmentTitle", panel.transform, font, "装備", 42, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.97f, 0.94f, 0.86f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(260f, 48f));

            equipmentGoldText = CreateText("EquipmentGold", panel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color(0.95f, 0.86f, 0.52f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -38f), new Vector2(240f, 30f));

            equipmentHeadlineText = CreateText("EquipmentHeadline", panel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 0.96f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(860f, 32f));

            GameObject equippedPanel = CreateUiObject("EquippedSummaryPanel", panel.transform);
            RectTransform equippedPanelRect = equippedPanel.AddComponent<RectTransform>();
            equippedPanelRect.anchorMin = new Vector2(0.5f, 1f);
            equippedPanelRect.anchorMax = new Vector2(0.5f, 1f);
            equippedPanelRect.pivot = new Vector2(0.5f, 1f);
            equippedPanelRect.sizeDelta = new Vector2(1020f, 318f);
            equippedPanelRect.anchoredPosition = new Vector2(0f, -136f);
            Image equippedPanelImage = equippedPanel.AddComponent<Image>();
            equippedPanelImage.color = new Color(0.10f, 0.13f, 0.17f, 0.92f);

            GameObject portraitBackdropObject = CreateUiObject("EquipmentMonsterPortraitBackdrop", equippedPanel.transform);
            RectTransform portraitBackdropRect = portraitBackdropObject.AddComponent<RectTransform>();
            portraitBackdropRect.anchorMin = new Vector2(0.5f, 1f);
            portraitBackdropRect.anchorMax = new Vector2(0.5f, 1f);
            portraitBackdropRect.pivot = new Vector2(0.5f, 0.5f);
            portraitBackdropRect.anchoredPosition = new Vector2(230f, -154f);
            portraitBackdropRect.sizeDelta = new Vector2(178f, 178f);
            equipmentMonsterPortraitBackdropImage = portraitBackdropObject.AddComponent<Image>();
            equipmentMonsterPortraitBackdropImage.color = new Color(0.62f, 0.68f, 0.70f, 0.52f);
            equipmentMonsterPortraitBackdropImage.raycastTarget = false;

            GameObject portraitShadowObject = CreateUiObject("EquipmentMonsterPortraitShadow", equippedPanel.transform);
            RectTransform portraitShadowRect = portraitShadowObject.AddComponent<RectTransform>();
            portraitShadowRect.anchorMin = new Vector2(0.5f, 1f);
            portraitShadowRect.anchorMax = new Vector2(0.5f, 1f);
            portraitShadowRect.pivot = new Vector2(0.5f, 0.5f);
            portraitShadowRect.anchoredPosition = new Vector2(234f, -153f);
            portraitShadowRect.sizeDelta = new Vector2(156f, 156f);
            equipmentMonsterPortraitShadowImage = portraitShadowObject.AddComponent<Image>();
            equipmentMonsterPortraitShadowImage.preserveAspect = true;
            equipmentMonsterPortraitShadowImage.raycastTarget = false;

            GameObject portraitObject = CreateUiObject("EquipmentMonsterPortrait", equippedPanel.transform);
            RectTransform portraitRect = portraitObject.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(230f, -156f);
            portraitRect.sizeDelta = new Vector2(166f, 166f);
            equipmentMonsterPortraitImage = portraitObject.AddComponent<Image>();
            equipmentMonsterPortraitImage.preserveAspect = true;
            equipmentMonsterPortraitImage.raycastTarget = false;

            Text equippedHeaderText = CreateText("EquippedHeader", equippedPanel.transform, font, "装備対象モンスター", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(300f, 32f));
            equippedHeaderText.horizontalOverflow = HorizontalWrapMode.Overflow;

            CreateActionButton(equippedPanel.transform, font, "←", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-176f, -22f), new Vector2(40f, 36f),
                new Color(0.24f, 0.20f, 0.16f, 0.96f), () => ChangeEquipmentMonster(-1), 18);
            CreateActionButton(equippedPanel.transform, font, "→", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-124f, -22f), new Vector2(40f, 36f),
                new Color(0.24f, 0.20f, 0.16f, 0.96f), () => ChangeEquipmentMonster(1), 18);
            CreateActionButton(equippedPanel.transform, font, "選ぶ", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-26f, -22f), new Vector2(86f, 36f),
                new Color(0.20f, 0.32f, 0.46f, 0.96f), OpenEquipmentMonsterPicker, 14);

            equipmentMonsterNameText = CreateText("EquipmentMonsterName", equippedPanel.transform, font, string.Empty, 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -64f), new Vector2(300f, 34f));
            equipmentMonsterNameText.resizeTextForBestFit = true;
            equipmentMonsterNameText.resizeTextMinSize = 18;
            equipmentMonsterNameText.resizeTextMaxSize = 28;
            equipmentMonsterNameText.verticalOverflow = VerticalWrapMode.Truncate;

            equipmentMonsterMetaText = CreateText("EquipmentMonsterMeta", equippedPanel.transform, font, string.Empty, 16, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.9f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -98f), new Vector2(500f, 24f));

            equippedWeaponText = CreateText("EquippedWeaponText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -138f), new Vector2(604f, 28f));
            ConfigureEquippedLineText(equippedWeaponText);
            equippedArmorText = CreateText("EquippedArmorText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -174f), new Vector2(604f, 28f));
            ConfigureEquippedLineText(equippedArmorText);
            equippedAccessoryText = CreateText("EquippedAccessoryText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -210f), new Vector2(604f, 28f));
            ConfigureEquippedLineText(equippedAccessoryText);

            equipmentBonusSummaryText = CreateText("EquipmentBonusSummaryText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -246f), new Vector2(604f, 58f));
            ConfigureEquipmentBonusSummaryText(equipmentBonusSummaryText);

            equipmentSummaryText = CreateText("EquipmentSummaryText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.UpperRight, Color.white, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -78f), new Vector2(166f, 226f));
            ConfigureEquipmentSummaryText(equipmentSummaryText);

            GameObject optionGrid = CreateUiObject("EquipmentOptionGrid", panel.transform);
            RectTransform optionGridRect = optionGrid.AddComponent<RectTransform>();
            optionGridRect.anchorMin = new Vector2(0.5f, 0f);
            optionGridRect.anchorMax = new Vector2(0.5f, 0f);
            optionGridRect.pivot = new Vector2(0.5f, 0f);
            optionGridRect.sizeDelta = new Vector2(872f, 1040f);
            optionGridRect.anchoredPosition = new Vector2(0f, -20f);

            CreateText("EquipmentListHeader", optionGrid.transform, font, "所持装備", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, -12f), new Vector2(180f, 32f));

            GameObject inventoryListPanel = CreateUiObject("EquipmentInventoryListPanel", optionGrid.transform);
            RectTransform inventoryListPanelRect = inventoryListPanel.AddComponent<RectTransform>();
            inventoryListPanelRect.anchorMin = new Vector2(0f, 1f);
            inventoryListPanelRect.anchorMax = new Vector2(0f, 1f);
            inventoryListPanelRect.pivot = new Vector2(0f, 1f);
            inventoryListPanelRect.anchoredPosition = new Vector2(0f, -52f);
            inventoryListPanelRect.sizeDelta = new Vector2(EquipmentInventoryWidth, EquipmentInventoryPanelHeight);

            Image inventoryListPanelImage = inventoryListPanel.AddComponent<Image>();
            inventoryListPanelImage.color = new Color(0.02f, 0.04f, 0.07f, 0.16f);

            equipmentInventorySummaryText = CreateText("EquipmentInventorySummary", inventoryListPanel.transform, font, string.Empty, 17, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.92f, 0.88f, 0.76f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(438f, 30f));
            equipmentInventorySummaryText.resizeTextForBestFit = true;
            equipmentInventorySummaryText.resizeTextMinSize = 12;
            equipmentInventorySummaryText.resizeTextMaxSize = 17;

            equipmentInventoryFilterButtonImages.Clear();
            equipmentInventoryFilterButtonTexts.Clear();
            equipmentInventoryFilterValues.Clear();
            CreateEquipmentInventoryFilterButton(inventoryListPanel.transform, font, EquipmentInventoryFilter.All, "全て", new Vector2(18f, -56f), new Vector2(92f, 36f));
            CreateEquipmentInventoryFilterButton(inventoryListPanel.transform, font, EquipmentInventoryFilter.Weapon, "武器", new Vector2(122f, -56f), new Vector2(92f, 36f));
            CreateEquipmentInventoryFilterButton(inventoryListPanel.transform, font, EquipmentInventoryFilter.Armor, "防具", new Vector2(226f, -56f), new Vector2(92f, 36f));
            CreateEquipmentInventoryFilterButton(inventoryListPanel.transform, font, EquipmentInventoryFilter.Accessory, "装飾品", new Vector2(330f, -56f), new Vector2(112f, 36f));

            Button inventorySortButton = CreateActionButton(inventoryListPanel.transform, font, "並び: 通常", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-18f, -56f), new Vector2(190f, 36f),
                new Color(0.18f, 0.28f, 0.38f, 0.96f), CycleEquipmentInventorySort, 14);
            equipmentInventorySortButtonText = inventorySortButton.GetComponentInChildren<Text>();

            GameObject inventoryViewport = CreateUiObject("EquipmentInventoryViewport", inventoryListPanel.transform);
            RectTransform inventoryViewportRect = inventoryViewport.AddComponent<RectTransform>();
            inventoryViewportRect.anchorMin = Vector2.zero;
            inventoryViewportRect.anchorMax = Vector2.one;
            inventoryViewportRect.offsetMin = Vector2.zero;
            inventoryViewportRect.offsetMax = new Vector2(0f, -EquipmentInventoryControlsHeight);

            Image inventoryViewportImage = inventoryViewport.AddComponent<Image>();
            inventoryViewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            inventoryViewport.AddComponent<RectMask2D>();

            GameObject contentRoot = CreateUiObject("EquipmentInventoryContent", inventoryViewport.transform);
            equipmentInventoryContentRect = contentRoot.AddComponent<RectTransform>();
            equipmentInventoryContentRect.anchorMin = new Vector2(0f, 1f);
            equipmentInventoryContentRect.anchorMax = new Vector2(0f, 1f);
            equipmentInventoryContentRect.pivot = new Vector2(0f, 1f);
            equipmentInventoryContentRect.anchoredPosition = Vector2.zero;
            equipmentInventoryContentRect.sizeDelta = new Vector2(EquipmentInventoryWidth, EquipmentInventoryViewportHeight);

            ScrollRect inventoryScrollRect = inventoryListPanel.AddComponent<ScrollRect>();
            inventoryScrollRect.viewport = inventoryViewportRect;
            inventoryScrollRect.content = equipmentInventoryContentRect;
            inventoryScrollRect.horizontal = false;
            inventoryScrollRect.vertical = true;
            inventoryScrollRect.scrollSensitivity = 42f;
            inventoryScrollRect.movementType = ScrollRect.MovementType.Clamped;

            BuildEquipmentMonsterPickerOverlay(font);

            equipmentEnhanceOverlayRoot = CreateUiObject("EquipmentEnhanceOverlay", equipmentSceneRoot.transform);
            RectTransform overlayRootRect = equipmentEnhanceOverlayRoot.AddComponent<RectTransform>();
            overlayRootRect.anchorMin = Vector2.zero;
            overlayRootRect.anchorMax = Vector2.one;
            overlayRootRect.offsetMin = Vector2.zero;
            overlayRootRect.offsetMax = Vector2.zero;

            Image overlayRootImage = equipmentEnhanceOverlayRoot.AddComponent<Image>();
            overlayRootImage.color = new Color(0.01f, 0.02f, 0.03f, 0.72f);

            GameObject overlayPanel = CreateUiObject("EquipmentEnhanceOverlayPanel", equipmentEnhanceOverlayRoot.transform);
            RectTransform overlayPanelRect = overlayPanel.AddComponent<RectTransform>();
            overlayPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayPanelRect.pivot = new Vector2(0.5f, 0.5f);
            overlayPanelRect.anchoredPosition = new Vector2(0f, 0f);
            overlayPanelRect.sizeDelta = new Vector2(860f, 980f);

            Image overlayPanelImage = overlayPanel.AddComponent<Image>();
            overlayPanelImage.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);

            CreateText("EquipmentEnhanceOverlayHeader", overlayPanel.transform, font, "強化", 34, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.86f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(220f, 40f));

            equipmentEnhanceOverlayTitleText = CreateText("EquipmentEnhanceOverlayTitle", overlayPanel.transform, font, string.Empty, 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(28f, -92f), new Vector2(540f, 28f));

            equipmentEnhanceOverlayInfoText = CreateText("EquipmentEnhanceOverlayInfo", overlayPanel.transform, font, string.Empty, 16, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.80f, 0.86f, 0.92f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(28f, -126f), new Vector2(804f, 60f));

            GameObject ritualArea = CreateUiObject("EquipmentEnhanceRitualArea", overlayPanel.transform);
            RectTransform ritualRect = ritualArea.AddComponent<RectTransform>();
            ritualRect.anchorMin = new Vector2(0.5f, 1f);
            ritualRect.anchorMax = new Vector2(0.5f, 1f);
            ritualRect.pivot = new Vector2(0.5f, 0.5f);
            ritualRect.anchoredPosition = new Vector2(0f, -292f);
            ritualRect.sizeDelta = new Vector2(600f, 260f);

            Image ritualImage = ritualArea.AddComponent<Image>();
            ritualImage.color = new Color(0.015f, 0.02f, 0.035f, 0.86f);

            equipmentEnhanceRuneImage = CreateRawPortrait("EquipmentEnhanceRune", ritualArea.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(246f, 246f));
            equipmentEnhanceRuneImage.texture = LoadMonsterTexture(EquipmentEnhanceRuneTexturePath);
            equipmentEnhanceRuneImage.raycastTarget = false;
            equipmentEnhanceRuneRect = equipmentEnhanceRuneImage.GetComponent<RectTransform>();

            GameObject darkOverlay = CreateUiObject("EquipmentEnhanceDarkOverlay", ritualArea.transform);
            RectTransform darkOverlayRect = darkOverlay.AddComponent<RectTransform>();
            darkOverlayRect.anchorMin = Vector2.zero;
            darkOverlayRect.anchorMax = Vector2.one;
            darkOverlayRect.offsetMin = Vector2.zero;
            darkOverlayRect.offsetMax = Vector2.zero;
            equipmentEnhanceDarkOverlayImage = darkOverlay.AddComponent<Image>();
            equipmentEnhanceDarkOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            equipmentEnhanceDarkOverlayImage.raycastTarget = false;

            equipmentEnhanceEffectImage = CreateRawPortrait("EquipmentEnhanceEffect", ritualArea.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f));
            equipmentEnhanceEffectImage.texture = null;
            equipmentEnhanceEffectImage.color = new Color(1f, 1f, 1f, 0f);
            equipmentEnhanceEffectImage.raycastTarget = false;
            equipmentEnhanceEffectRect = equipmentEnhanceEffectImage.GetComponent<RectTransform>();

            equipmentEnhanceItemFrameImage = CreateRawPortrait("EquipmentEnhanceItemFrame", ritualArea.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 156f));
            equipmentEnhanceItemFrameImage.texture = null;
            equipmentEnhanceItemFrameImage.color = new Color(1f, 1f, 1f, 0f);
            equipmentEnhanceItemFrameImage.raycastTarget = false;
            equipmentEnhanceItemFrameRect = equipmentEnhanceItemFrameImage.GetComponent<RectTransform>();

            equipmentEnhanceItemImage = CreateRawPortrait("EquipmentEnhanceItem", ritualArea.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            equipmentEnhanceItemImage.texture = null;
            equipmentEnhanceItemImage.color = new Color(1f, 1f, 1f, 0f);
            equipmentEnhanceItemImage.raycastTarget = false;
            equipmentEnhanceItemRect = equipmentEnhanceItemImage.GetComponent<RectTransform>();

            equipmentEnhanceOverlayResultText = CreateText("EquipmentEnhanceOverlayResult", overlayPanel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.52f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(804f, 30f));

            equipmentEnhanceOverlayListRect = CreateUiObject("EquipmentEnhanceOverlayList", overlayPanel.transform).AddComponent<RectTransform>();
            equipmentEnhanceOverlayListRect.anchorMin = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.anchorMax = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.pivot = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.anchoredPosition = new Vector2(28f, -490f);
            equipmentEnhanceOverlayListRect.sizeDelta = new Vector2(804f, 436f);

            CreateActionButton(overlayPanel.transform, font, "閉じる", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(96f, 40f),
                new Color(0.34f, 0.20f, 0.16f, 0.96f), CloseEquipmentEnhancementOverlay, 16);

            equipmentEnhanceOverlayRoot.SetActive(false);
        }

        private void BuildEquipmentMonsterPickerOverlay(Font font)
        {
            equipmentMonsterPickerOverlayRoot = CreateUiObject("EquipmentMonsterPickerOverlay", equipmentSceneRoot.transform);
            RectTransform rootRect = equipmentMonsterPickerOverlayRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = equipmentMonsterPickerOverlayRoot.AddComponent<Image>();
            rootImage.color = new Color(0.01f, 0.02f, 0.03f, 0.76f);

            Button backdropButton = equipmentMonsterPickerOverlayRoot.AddComponent<Button>();
            backdropButton.targetGraphic = rootImage;
            backdropButton.onClick.AddListener(CloseEquipmentMonsterPicker);

            GameObject panel = CreateUiObject("EquipmentMonsterPickerPanel", equipmentMonsterPickerOverlayRoot.transform);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(860f, 1040f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.09f, 0.12f, 0.99f);

            Button panelBlocker = panel.AddComponent<Button>();
            panelBlocker.targetGraphic = panelImage;

            CreateText("EquipmentMonsterPickerHeader", panel.transform, font, "装備対象を選択", 34, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.86f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(360f, 42f));

            equipmentMonsterPickerSummaryText = CreateText("EquipmentMonsterPickerSummary", panel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 0.96f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(640f, 30f));

            CreateActionButton(panel.transform, font, "閉じる", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(96f, 40f),
                new Color(0.34f, 0.20f, 0.16f, 0.96f), CloseEquipmentMonsterPicker, 16);

            GameObject searchBox = CreateUiObject("EquipmentMonsterSearchBox", panel.transform);
            RectTransform searchRect = searchBox.AddComponent<RectTransform>();
            searchRect.anchorMin = new Vector2(0.5f, 1f);
            searchRect.anchorMax = new Vector2(0.5f, 1f);
            searchRect.pivot = new Vector2(0.5f, 1f);
            searchRect.anchoredPosition = new Vector2(-188f, -128f);
            searchRect.sizeDelta = new Vector2(436f, 48f);

            Image searchImage = searchBox.AddComponent<Image>();
            searchImage.color = new Color(0.02f, 0.04f, 0.07f, 0.96f);

            equipmentMonsterSearchInput = searchBox.AddComponent<InputField>();
            equipmentMonsterSearchInput.targetGraphic = searchImage;
            equipmentMonsterSearchInput.lineType = InputField.LineType.SingleLine;
            equipmentMonsterSearchInput.contentType = InputField.ContentType.Standard;
            equipmentMonsterSearchInput.characterLimit = 24;
            equipmentMonsterSearchInput.selectionColor = new Color(0.80f, 0.64f, 0.34f, 0.42f);

            Text searchText = CreateText("SearchText", searchBox.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(10f, 0f), new Vector2(-28f, 0f));
            searchText.raycastTarget = false;
            Text searchPlaceholder = CreateText("SearchPlaceholder", searchBox.transform, font, "名前で検索", 20, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color(0.54f, 0.60f, 0.66f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(10f, 0f), new Vector2(-28f, 0f));
            searchPlaceholder.raycastTarget = false;
            equipmentMonsterSearchInput.textComponent = searchText;
            equipmentMonsterSearchInput.placeholder = searchPlaceholder;
            equipmentMonsterSearchInput.text = equipmentMonsterSearchQuery;
            equipmentMonsterSearchInput.onValueChanged.AddListener(OnEquipmentMonsterSearchChanged);

            Button sortButton = CreateActionButton(panel.transform, font, "並び: 通常", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(284f, -128f), new Vector2(196f, 48f),
                new Color(0.20f, 0.28f, 0.40f, 0.96f), CycleEquipmentMonsterPickerSort, 16);
            equipmentMonsterPickerSortButtonText = sortButton.GetComponentInChildren<Text>();

            equipmentMonsterClassFilterButtonImages.Clear();
            equipmentMonsterClassFilterButtonTexts.Clear();
            for (int classRank = 0; classRank <= 6; classRank += 1)
            {
                int capturedClassRank = classRank;
                string label = classRank == 0 ? "全" : $"C{classRank}";
                Button filterButton = CreateActionButton(panel.transform, font, label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(-354f + (classRank * 118f), -196f), new Vector2(92f, 38f),
                    new Color(0.14f, 0.18f, 0.24f, 0.96f), () => SetEquipmentMonsterClassFilter(capturedClassRank), 15);
                equipmentMonsterClassFilterButtonImages.Add(filterButton.GetComponent<Image>());
                equipmentMonsterClassFilterButtonTexts.Add(filterButton.GetComponentInChildren<Text>());
            }

            equipmentMonsterElementFilterButtonImages.Clear();
            equipmentMonsterElementFilterButtonTexts.Clear();
            equipmentMonsterElementFilterValues.Clear();
            int[] elementFilterValues = { -1, (int)MonsterElement.Wood, (int)MonsterElement.Water, (int)MonsterElement.Fire, (int)MonsterElement.Light, (int)MonsterElement.Dark };
            for (int i = 0; i < elementFilterValues.Length; i += 1)
            {
                int capturedElementValue = elementFilterValues[i];
                string label = capturedElementValue < 0 ? "全" : ResolveMonsterElementLabel((MonsterElement)capturedElementValue);
                Button filterButton = CreateActionButton(panel.transform, font, label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(-354f + (i * 118f), -240f), new Vector2(92f, 34f),
                    new Color(0.11f, 0.17f, 0.22f, 0.96f), () => SetEquipmentMonsterElementFilter(capturedElementValue), 14);
                equipmentMonsterElementFilterButtonImages.Add(filterButton.GetComponent<Image>());
                equipmentMonsterElementFilterButtonTexts.Add(filterButton.GetComponentInChildren<Text>());
                equipmentMonsterElementFilterValues.Add(capturedElementValue);
            }

            GameObject listPanel = CreateUiObject("EquipmentMonsterPickerListPanel", panel.transform);
            RectTransform listPanelRect = listPanel.AddComponent<RectTransform>();
            listPanelRect.anchorMin = new Vector2(0.5f, 1f);
            listPanelRect.anchorMax = new Vector2(0.5f, 1f);
            listPanelRect.pivot = new Vector2(0.5f, 1f);
            listPanelRect.anchoredPosition = new Vector2(0f, -296f);
            listPanelRect.sizeDelta = new Vector2(804f, 700f);

            Image listPanelImage = listPanel.AddComponent<Image>();
            listPanelImage.color = new Color(0.02f, 0.04f, 0.07f, 0.66f);

            GameObject viewport = CreateUiObject("EquipmentMonsterPickerViewport", listPanel.transform);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(14f, 14f);
            viewportRect.offsetMax = new Vector2(-14f, -14f);

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateUiObject("EquipmentMonsterPickerContent", viewport.transform);
            equipmentMonsterPickerListRect = content.AddComponent<RectTransform>();
            equipmentMonsterPickerListRect.anchorMin = new Vector2(0f, 1f);
            equipmentMonsterPickerListRect.anchorMax = new Vector2(0f, 1f);
            equipmentMonsterPickerListRect.pivot = new Vector2(0f, 1f);
            equipmentMonsterPickerListRect.anchoredPosition = Vector2.zero;
            equipmentMonsterPickerListRect.sizeDelta = new Vector2(776f, 0f);

            ScrollRect scrollRect = listPanel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = equipmentMonsterPickerListRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            equipmentMonsterPickerOverlayRoot.SetActive(false);
        }

        private void RefreshEquipmentScene()
        {
            if (equipmentSceneRoot == null)
            {
                return;
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (equipmentGoldText != null)
            {
                int gold = profile != null ? profile.Gold : 0;
                equipmentGoldText.text = $"Gold {gold}";
            }

            EnsureSelectedEquipmentMonster(profile);
            OwnedMonsterData selectedMonster = profile != null ? profile.GetOwnedMonster(selectedEquipmentMonsterInstanceId) : null;

            if (equipmentHeadlineText != null)
            {
                equipmentHeadlineText.text = string.IsNullOrEmpty(equipmentLastActionMessage)
                    ? "武器 / 防具 / 装飾 をモンスター個別に装備・強化できます"
                    : equipmentLastActionMessage;
            }

            if (equipmentMonsterNameText != null)
            {
                equipmentMonsterNameText.text = selectedMonster != null ? GetMonsterDisplayName(selectedMonster) : "モンスター未選択";
            }

            if (equipmentMonsterMetaText != null)
            {
                equipmentMonsterMetaText.text = selectedMonster != null
                    ? $"Lv.{selectedMonster.Level}  +{selectedMonster.TotalPlusValue}  {GetMonsterDamageTypeLabel(selectedMonster)}  個別装備"
                    : "所持モンスターがいないため装備変更できません";
            }

            RefreshEquipmentMonsterPortrait(selectedMonster);

            if (equippedWeaponText != null)
            {
                equippedWeaponText.text = BuildMonsterEquipmentLine(profile, selectedMonster, EquipmentSlotType.Weapon);
            }

            if (equippedArmorText != null)
            {
                equippedArmorText.text = BuildMonsterEquipmentLine(profile, selectedMonster, EquipmentSlotType.Armor);
            }

            if (equippedAccessoryText != null)
            {
                equippedAccessoryText.text = BuildMonsterEquipmentLine(profile, selectedMonster, EquipmentSlotType.Accessory);
            }

            if (equipmentBonusSummaryText != null)
            {
                equipmentBonusSummaryText.text = BuildMonsterEquipmentBonusSummary(profile, selectedMonster);
            }

            if (equipmentSummaryText != null)
            {
                equipmentSummaryText.text = BuildMonsterEquipmentSummary(profile, selectedMonster);
            }

            RebuildEquipmentInventory(profile, selectedMonster);
            if (equipmentMonsterPickerOverlayRoot != null && equipmentMonsterPickerOverlayRoot.activeSelf)
            {
                RefreshEquipmentMonsterPicker(profile);
            }
        }

        private void RefreshEquipmentMonsterPortrait(OwnedMonsterData selectedMonster)
        {
            MonsterDataSO monsterData = selectedMonster != null
                ? MasterDataManager.Instance?.GetMonsterData(selectedMonster.MonsterId)
                : null;

            if (equipmentMonsterPortraitBackdropImage != null)
            {
                equipmentMonsterPortraitBackdropImage.color = selectedMonster != null
                    ? new Color(0.62f, 0.68f, 0.70f, 0.52f)
                    : new Color(0.62f, 0.68f, 0.70f, 0f);
            }

            Sprite portraitSprite = selectedMonster != null
                ? LoadMonsterSprite(GetMonsterPortraitResourcePath(monsterData))
                : null;

            if (equipmentMonsterPortraitShadowImage != null)
            {
                equipmentMonsterPortraitShadowImage.sprite = portraitSprite;
                equipmentMonsterPortraitShadowImage.color = portraitSprite != null
                    ? new Color(0f, 0f, 0f, 0.48f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            if (equipmentMonsterPortraitImage != null)
            {
                equipmentMonsterPortraitImage.sprite = portraitSprite;
                equipmentMonsterPortraitImage.color = portraitSprite != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f);
            }
        }

        private void ChangeEquipmentMonster(int delta)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            List<OwnedMonsterData> monsters = GetEquipmentSceneMonsters(profile);
            if (monsters.Count <= 0)
            {
                return;
            }

            int currentIndex = monsters.FindIndex(x => x.InstanceId == selectedEquipmentMonsterInstanceId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + delta + monsters.Count) % monsters.Count;
            selectedEquipmentMonsterInstanceId = monsters[nextIndex].InstanceId;
            equipmentLastActionMessage = string.Empty;
            RefreshEquipmentScene();
        }

        private void OpenEquipmentMonsterPicker()
        {
            if (equipmentMonsterPickerOverlayRoot == null)
            {
                return;
            }

            equipmentMonsterPickerOverlayRoot.transform.SetAsLastSibling();
            equipmentMonsterPickerOverlayRoot.SetActive(true);
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentMonsterPicker(profile);

            if (Application.isPlaying && equipmentMonsterSearchInput != null)
            {
                equipmentMonsterSearchInput.Select();
                equipmentMonsterSearchInput.ActivateInputField();
            }
        }

        private void CloseEquipmentMonsterPicker()
        {
            if (equipmentMonsterPickerOverlayRoot != null)
            {
                equipmentMonsterPickerOverlayRoot.SetActive(false);
            }
        }

        private void OnEquipmentMonsterSearchChanged(string value)
        {
            equipmentMonsterSearchQuery = value ?? string.Empty;
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentMonsterPicker(profile);
        }

        private void SetEquipmentMonsterClassFilter(int classRank)
        {
            equipmentMonsterClassFilter = Mathf.Clamp(classRank, 0, 6);
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentMonsterPicker(profile);
        }

        private void SetEquipmentMonsterElementFilter(int elementValue)
        {
            equipmentMonsterElementFilter = Mathf.Clamp(elementValue, -1, (int)MonsterElement.Dark);
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentMonsterPicker(profile);
        }

        private void CycleEquipmentMonsterPickerSort()
        {
            switch (equipmentMonsterPickerSortMode)
            {
                case EquipmentMonsterPickerSortMode.Default:
                    equipmentMonsterPickerSortMode = EquipmentMonsterPickerSortMode.Level;
                    break;
                case EquipmentMonsterPickerSortMode.Level:
                    equipmentMonsterPickerSortMode = EquipmentMonsterPickerSortMode.Favorite;
                    break;
                default:
                    equipmentMonsterPickerSortMode = EquipmentMonsterPickerSortMode.Default;
                    break;
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentMonsterPicker(profile);
        }

        private void RefreshEquipmentMonsterPicker(PlayerProfile profile)
        {
            if (equipmentMonsterPickerListRect == null)
            {
                return;
            }

            List<OwnedMonsterData> allMonsters = GetEquipmentSceneMonsters(profile);
            List<OwnedMonsterData> displayMonsters = BuildEquipmentMonsterPickerDisplayMonsters(allMonsters);
            ClearChildren(equipmentMonsterPickerListRect);

            if (equipmentMonsterPickerSummaryText != null)
            {
                string classFilterLabel = equipmentMonsterClassFilter == 0 ? "全クラス" : $"C{equipmentMonsterClassFilter}";
                string elementFilterLabel = equipmentMonsterElementFilter < 0
                    ? "全属性"
                    : ResolveMonsterElementLabel((MonsterElement)equipmentMonsterElementFilter);
                equipmentMonsterPickerSummaryText.text =
                    $"{displayMonsters.Count}/{allMonsters.Count}体  {classFilterLabel}  {elementFilterLabel}  {GetEquipmentMonsterPickerSortLabel()}";
            }

            UpdateEquipmentMonsterPickerControls();

            if (displayMonsters.Count <= 0)
            {
                equipmentMonsterPickerListRect.sizeDelta = new Vector2(776f, 672f);
                CreateText("EquipmentMonsterPickerEmpty", equipmentMonsterPickerListRect, ResolveRuntimeFont(), "該当するモンスターがいません", 22, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(680f, 40f));
                return;
            }

            int rowCount = Mathf.CeilToInt(displayMonsters.Count / 3f);
            equipmentMonsterPickerListRect.sizeDelta = new Vector2(776f, Mathf.Max(672f, rowCount * 236f));
            Font font = ResolveRuntimeFont();
            for (int i = 0; i < displayMonsters.Count; i += 1)
            {
                CreateEquipmentMonsterPickerCard(equipmentMonsterPickerListRect, font, profile, displayMonsters[i], i);
            }
        }

        private List<OwnedMonsterData> BuildEquipmentMonsterPickerDisplayMonsters(List<OwnedMonsterData> allMonsters)
        {
            var result = new List<OwnedMonsterData>();
            string normalizedSearch = NormalizeEquipmentMonsterSearchText(equipmentMonsterSearchQuery);

            for (int i = 0; i < allMonsters.Count; i += 1)
            {
                OwnedMonsterData monster = allMonsters[i];
                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monster.MonsterId);
                if (equipmentMonsterClassFilter > 0 && GetEquipmentMonsterClassRank(monsterData) != equipmentMonsterClassFilter)
                {
                    continue;
                }

                if (equipmentMonsterElementFilter >= 0 && (monsterData == null || (int)monsterData.element != equipmentMonsterElementFilter))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedSearch))
                {
                    string displayName = monsterData != null ? monsterData.monsterName : monster.MonsterId;
                    string searchableText = NormalizeEquipmentMonsterSearchText($"{displayName} {monster.MonsterId}");
                    if (!searchableText.Contains(normalizedSearch))
                    {
                        continue;
                    }
                }

                result.Add(monster);
            }

            if (equipmentMonsterPickerSortMode != EquipmentMonsterPickerSortMode.Default)
            {
                result.Sort(CompareEquipmentMonsterPickerEntries);
            }

            return result;
        }

        private int CompareEquipmentMonsterPickerEntries(OwnedMonsterData left, OwnedMonsterData right)
        {
            MonsterDataSO leftData = MasterDataManager.Instance?.GetMonsterData(left.MonsterId);
            MonsterDataSO rightData = MasterDataManager.Instance?.GetMonsterData(right.MonsterId);

            if (equipmentMonsterPickerSortMode == EquipmentMonsterPickerSortMode.Favorite)
            {
                int favoriteCompare = right.IsFavorite.CompareTo(left.IsFavorite);
                if (favoriteCompare != 0)
                {
                    return favoriteCompare;
                }
            }

            int levelCompare = right.Level.CompareTo(left.Level);
            if (levelCompare != 0)
            {
                return levelCompare;
            }

            if (equipmentMonsterPickerSortMode == EquipmentMonsterPickerSortMode.Level)
            {
                int classCompare = GetEquipmentMonsterClassRank(rightData).CompareTo(GetEquipmentMonsterClassRank(leftData));
                if (classCompare != 0)
                {
                    return classCompare;
                }
            }

            int acquiredCompare = right.AcquiredOrder.CompareTo(left.AcquiredOrder);
            if (acquiredCompare != 0)
            {
                return acquiredCompare;
            }

            return string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }

        private void CreateEquipmentMonsterPickerCard(
            Transform parent,
            Font font,
            PlayerProfile profile,
            OwnedMonsterData monster,
            int index)
        {
            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monster.MonsterId);
            int classRank = GetEquipmentMonsterClassRank(monsterData);
            int row = index / 3;
            int column = index % 3;
            bool isSelected = monster.InstanceId == selectedEquipmentMonsterInstanceId;

            GameObject card = CreateUiObject("EquipmentMonsterPickerCard_" + index, parent);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(column * 266f, -(row * 236f));
            cardRect.sizeDelta = new Vector2(244f, 222f);

            Image cardImage = card.AddComponent<Image>();
            cardImage.color = isSelected
                ? new Color(0.13f, 0.32f, 0.24f, 0.98f)
                : new Color(0.07f, 0.10f, 0.14f, 0.98f);

            Button cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            string capturedInstanceId = monster.InstanceId;
            cardButton.onClick.AddListener(() => SelectEquipmentMonsterFromPicker(capturedInstanceId));

            RawImage frameImage = CreateRawPortrait("MonsterCardFrame", card.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(244f, 222f));
            frameImage.texture = LoadMonsterTexture(ResolveMonsterCardFrameTexturePath(classRank));
            frameImage.color = frameImage.texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            frameImage.raycastTarget = false;

            GameObject portraitObject = CreateUiObject("MonsterPortrait", card.transform);
            RectTransform portraitRect = portraitObject.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 1f);
            portraitRect.anchoredPosition = new Vector2(0f, -22f);
            portraitRect.sizeDelta = new Vector2(128f, 128f);

            Image portraitImage = portraitObject.AddComponent<Image>();
            portraitImage.sprite = LoadMonsterSprite(GetMonsterPortraitResourcePath(monsterData));
            portraitImage.preserveAspect = true;
            portraitImage.color = portraitImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            portraitImage.raycastTarget = false;

            Text nameText = CreateText("MonsterName", card.transform, font, GetMonsterDisplayName(monster), 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -151f), new Vector2(210f, 28f));
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 11;
            nameText.resizeTextMaxSize = 16;

            GameObject infoPanel = CreateEquipmentMonsterPickerInfoPanel(card.transform, isSelected);
            Text metaText = CreateText("MonsterMeta", infoPanel.transform, font, $"Lv.{monster.Level} / C{classRank} / {GetMonsterDamageTypeLabel(monster)} / +{monster.TotalPlusValue}", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(196f, 16f));
            metaText.resizeTextForBestFit = true;
            metaText.resizeTextMinSize = 10;
            metaText.resizeTextMaxSize = 13;

            CreateText("MonsterState", infoPanel.transform, font, BuildEquipmentMonsterPickerStatus(profile, monster, isSelected), 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, isSelected ? new Color(0.58f, 1f, 0.74f) : new Color(0.82f, 0.90f, 0.98f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(196f, 16f));

            CreateEquipmentMonsterPickerFavoriteBadge(card.transform, monster);
        }

        private GameObject CreateEquipmentMonsterPickerInfoPanel(Transform parent, bool isSelected)
        {
            GameObject panel = CreateUiObject("MonsterInfoPanel", parent);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -184f);
            rect.sizeDelta = new Vector2(208f, 36f);

            Image image = panel.AddComponent<Image>();
            image.color = isSelected
                ? new Color(0.02f, 0.13f, 0.10f, 0.86f)
                : new Color(0.01f, 0.025f, 0.045f, 0.86f);
            image.raycastTarget = false;

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = isSelected
                ? new Color(0.54f, 1f, 0.68f, 0.82f)
                : new Color(0.96f, 0.70f, 0.28f, 0.78f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            return panel;
        }

        private void CreateEquipmentMonsterPickerFavoriteBadge(Transform parent, OwnedMonsterData monster)
        {
            if (monster == null || !monster.IsFavorite)
            {
                return;
            }

            GameObject badge = CreateUiObject("FavoriteHeartBadge", parent);
            RectTransform rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(34f, 34f);

            Image image = badge.AddComponent<Image>();
            image.sprite = LoadMonsterSprite(FavoriteHeartFilledTexturePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = image.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        private void SelectEquipmentMonsterFromPicker(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            selectedEquipmentMonsterInstanceId = instanceId;
            equipmentLastActionMessage = string.Empty;
            CloseEquipmentMonsterPicker();
            RefreshEquipmentScene();
        }

        private void UpdateEquipmentMonsterPickerControls()
        {
            for (int i = 0; i < equipmentMonsterClassFilterButtonImages.Count; i += 1)
            {
                bool isSelected = i == equipmentMonsterClassFilter;
                Image image = equipmentMonsterClassFilterButtonImages[i];
                if (image != null)
                {
                    image.color = isSelected
                        ? new Color(0.46f, 0.34f, 0.16f, 0.98f)
                        : new Color(0.14f, 0.18f, 0.24f, 0.96f);
                }

                Text text = i < equipmentMonsterClassFilterButtonTexts.Count ? equipmentMonsterClassFilterButtonTexts[i] : null;
                if (text != null)
                {
                    text.color = isSelected ? new Color(1f, 0.92f, 0.68f) : Color.white;
                }
            }

            for (int i = 0; i < equipmentMonsterElementFilterButtonImages.Count; i += 1)
            {
                int elementValue = i < equipmentMonsterElementFilterValues.Count ? equipmentMonsterElementFilterValues[i] : -1;
                bool isSelected = elementValue == equipmentMonsterElementFilter;
                Image image = equipmentMonsterElementFilterButtonImages[i];
                if (image != null)
                {
                    image.color = isSelected
                        ? new Color(0.22f, 0.38f, 0.30f, 0.98f)
                        : new Color(0.11f, 0.17f, 0.22f, 0.96f);
                }

                Text text = i < equipmentMonsterElementFilterButtonTexts.Count ? equipmentMonsterElementFilterButtonTexts[i] : null;
                if (text != null)
                {
                    text.color = isSelected ? new Color(0.72f, 1f, 0.78f) : Color.white;
                }
            }

            if (equipmentMonsterPickerSortButtonText != null)
            {
                equipmentMonsterPickerSortButtonText.text = "並び: " + GetEquipmentMonsterPickerSortLabel();
            }
        }

        private string GetEquipmentMonsterPickerSortLabel()
        {
            switch (equipmentMonsterPickerSortMode)
            {
                case EquipmentMonsterPickerSortMode.Level:
                    return "レベル";
                case EquipmentMonsterPickerSortMode.Favorite:
                    return "お気に入り";
                default:
                    return "通常";
            }
        }

        private static string BuildEquipmentMonsterPickerStatus(PlayerProfile profile, OwnedMonsterData monster, bool isSelected)
        {
            var labels = new List<string>();
            if (isSelected)
            {
                labels.Add("選択中");
            }

            int partySlot = GetPartySlotNumber(profile, monster != null ? monster.InstanceId : string.Empty);
            if (partySlot > 0)
            {
                labels.Add($"編成{partySlot}");
            }

            if (monster != null && monster.IsLocked)
            {
                labels.Add("ロック中");
            }

            return labels.Count > 0 ? string.Join(" / ", labels) : "所持";
        }

        private static int GetPartySlotNumber(PlayerProfile profile, string instanceId)
        {
            if (profile == null || profile.PartyMonsterInstanceIds == null || string.IsNullOrEmpty(instanceId))
            {
                return 0;
            }

            for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
            {
                if (profile.PartyMonsterInstanceIds[i] == instanceId)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static int GetEquipmentMonsterClassRank(MonsterDataSO monsterData)
        {
            return Mathf.Clamp(monsterData != null ? monsterData.classRank : 1, 1, 6);
        }

        private static string NormalizeEquipmentMonsterSearchText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] characters = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < characters.Length; i += 1)
            {
                if (characters[i] >= '\u30A1' && characters[i] <= '\u30F6')
                {
                    characters[i] = (char)(characters[i] - 0x60);
                }
            }

            return new string(characters);
        }

        private static List<OwnedMonsterData> GetEquipmentSceneMonsters(PlayerProfile profile)
        {
            var result = new List<OwnedMonsterData>();
            if (profile == null)
            {
                return result;
            }

            foreach (string instanceId in profile.PartyMonsterInstanceIds)
            {
                OwnedMonsterData partyMonster = profile.GetOwnedMonster(instanceId);
                if (partyMonster != null && result.FindIndex(x => x.InstanceId == partyMonster.InstanceId) < 0)
                {
                    result.Add(partyMonster);
                }
            }

            foreach (OwnedMonsterData ownedMonster in profile.OwnedMonsters)
            {
                if (ownedMonster != null && result.FindIndex(x => x.InstanceId == ownedMonster.InstanceId) < 0)
                {
                    result.Add(ownedMonster);
                }
            }

            return result;
        }

        private void EnsureSelectedEquipmentMonster(PlayerProfile profile)
        {
            List<OwnedMonsterData> monsters = GetEquipmentSceneMonsters(profile);
            if (monsters.Count <= 0)
            {
                selectedEquipmentMonsterInstanceId = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(selectedEquipmentMonsterInstanceId) || monsters.FindIndex(x => x.InstanceId == selectedEquipmentMonsterInstanceId) < 0)
            {
                selectedEquipmentMonsterInstanceId = monsters[0].InstanceId;
            }
        }

        private Button CreateEquipmentInventoryFilterButton(
            Transform parent,
            Font font,
            EquipmentInventoryFilter filter,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Button button = CreateActionButton(parent, font, label, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), anchoredPosition, size,
                new Color(0.12f, 0.18f, 0.24f, 0.96f), () => SetEquipmentInventoryFilter(filter), 14);
            equipmentInventoryFilterButtonImages.Add(button.GetComponent<Image>());
            equipmentInventoryFilterButtonTexts.Add(button.GetComponentInChildren<Text>());
            equipmentInventoryFilterValues.Add(filter);
            return button;
        }

        private void SetEquipmentInventoryFilter(EquipmentInventoryFilter filter)
        {
            equipmentInventoryFilter = filter;
            RefreshEquipmentScene();
        }

        private void CycleEquipmentInventorySort()
        {
            switch (equipmentInventorySortMode)
            {
                case EquipmentInventorySortMode.Default:
                    equipmentInventorySortMode = EquipmentInventorySortMode.Rarity;
                    break;
                case EquipmentInventorySortMode.Rarity:
                    equipmentInventorySortMode = EquipmentInventorySortMode.Power;
                    break;
                case EquipmentInventorySortMode.Power:
                    equipmentInventorySortMode = EquipmentInventorySortMode.Name;
                    break;
                default:
                    equipmentInventorySortMode = EquipmentInventorySortMode.Default;
                    break;
            }

            RefreshEquipmentScene();
        }

        private List<OwnedEquipmentData> BuildEquipmentInventoryDisplayEquipments(PlayerProfile profile)
        {
            var result = new List<OwnedEquipmentData>();
            if (profile == null)
            {
                return result;
            }

            for (int i = 0; i < profile.OwnedEquipments.Count; i += 1)
            {
                OwnedEquipmentData equipment = profile.OwnedEquipments[i];
                EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
                if (!PassesEquipmentInventoryFilter(equipmentData))
                {
                    continue;
                }

                result.Add(equipment);
            }

            result.Sort(CompareEquipmentInventoryEntries);
            return result;
        }

        private bool PassesEquipmentInventoryFilter(EquipmentDataSO equipmentData)
        {
            if (equipmentInventoryFilter == EquipmentInventoryFilter.All)
            {
                return true;
            }

            if (equipmentData == null)
            {
                return false;
            }

            switch (equipmentInventoryFilter)
            {
                case EquipmentInventoryFilter.Weapon:
                    return equipmentData.slotType == EquipmentSlotType.Weapon;
                case EquipmentInventoryFilter.Armor:
                    return equipmentData.slotType == EquipmentSlotType.Armor;
                case EquipmentInventoryFilter.Accessory:
                    return equipmentData.slotType == EquipmentSlotType.Accessory;
                default:
                    return true;
            }
        }

        private int CompareEquipmentInventoryEntries(OwnedEquipmentData left, OwnedEquipmentData right)
        {
            EquipmentDataSO leftData = MasterDataManager.Instance?.GetEquipmentData(left.EquipmentId);
            EquipmentDataSO rightData = MasterDataManager.Instance?.GetEquipmentData(right.EquipmentId);

            switch (equipmentInventorySortMode)
            {
                case EquipmentInventorySortMode.Rarity:
                {
                    int rarityCompare = GetEquipmentClassRank(rightData, right).CompareTo(GetEquipmentClassRank(leftData, left));
                    if (rarityCompare != 0)
                    {
                        return rarityCompare;
                    }

                    int powerCompare = ResolveEquipmentPowerScore(rightData, right).CompareTo(ResolveEquipmentPowerScore(leftData, left));
                    if (powerCompare != 0)
                    {
                        return powerCompare;
                    }

                    break;
                }
                case EquipmentInventorySortMode.Power:
                {
                    int powerCompare = ResolveEquipmentPowerScore(rightData, right).CompareTo(ResolveEquipmentPowerScore(leftData, left));
                    if (powerCompare != 0)
                    {
                        return powerCompare;
                    }

                    int rarityCompare = GetEquipmentClassRank(rightData, right).CompareTo(GetEquipmentClassRank(leftData, left));
                    if (rarityCompare != 0)
                    {
                        return rarityCompare;
                    }

                    break;
                }
                case EquipmentInventorySortMode.Name:
                {
                    int nameCompare = string.Compare(ResolveEquipmentInventoryName(leftData, left), ResolveEquipmentInventoryName(rightData, right), StringComparison.CurrentCulture);
                    if (nameCompare != 0)
                    {
                        return nameCompare;
                    }

                    break;
                }
                default:
                {
                    int slotCompare = ResolveEquipmentSlotOrder(leftData).CompareTo(ResolveEquipmentSlotOrder(rightData));
                    if (slotCompare != 0)
                    {
                        return slotCompare;
                    }

                    break;
                }
            }

            int equippedCompare = IsEquipmentInventoryItemEquipped(right).CompareTo(IsEquipmentInventoryItemEquipped(left));
            if (equippedCompare != 0)
            {
                return equippedCompare;
            }

            int fallbackSlotCompare = ResolveEquipmentSlotOrder(leftData).CompareTo(ResolveEquipmentSlotOrder(rightData));
            if (fallbackSlotCompare != 0)
            {
                return fallbackSlotCompare;
            }

            int fallbackNameCompare = string.Compare(ResolveEquipmentInventoryName(leftData, left), ResolveEquipmentInventoryName(rightData, right), StringComparison.CurrentCulture);
            if (fallbackNameCompare != 0)
            {
                return fallbackNameCompare;
            }

            return string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }

        private void UpdateEquipmentInventoryControls(int visibleCount, int totalCount, int storageLimit)
        {
            if (equipmentInventorySummaryText != null)
            {
                equipmentInventorySummaryText.text =
                    $"{visibleCount}/{totalCount}個  枠 {totalCount}/{Mathf.Max(1, storageLimit)}  {GetEquipmentInventoryFilterLabel(equipmentInventoryFilter)} / {GetEquipmentInventorySortLabel()}";
            }

            for (int i = 0; i < equipmentInventoryFilterButtonImages.Count; i += 1)
            {
                EquipmentInventoryFilter filter = i < equipmentInventoryFilterValues.Count
                    ? equipmentInventoryFilterValues[i]
                    : EquipmentInventoryFilter.All;
                bool isSelected = filter == equipmentInventoryFilter;
                Image image = equipmentInventoryFilterButtonImages[i];
                if (image != null)
                {
                    image.color = isSelected
                        ? new Color(0.42f, 0.30f, 0.13f, 0.98f)
                        : new Color(0.12f, 0.18f, 0.24f, 0.96f);
                }

                Text text = i < equipmentInventoryFilterButtonTexts.Count ? equipmentInventoryFilterButtonTexts[i] : null;
                if (text != null)
                {
                    text.color = isSelected ? new Color(1f, 0.92f, 0.66f) : Color.white;
                }
            }

            if (equipmentInventorySortButtonText != null)
            {
                equipmentInventorySortButtonText.text = "並び: " + GetEquipmentInventorySortLabel();
            }
        }

        private static int ResolveEquipmentSlotOrder(EquipmentDataSO equipmentData)
        {
            return equipmentData != null ? (int)equipmentData.slotType : 99;
        }

        private static bool IsEquipmentInventoryItemEquipped(OwnedEquipmentData equipment)
        {
            return equipment != null && !string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId);
        }

        private static float ResolveEquipmentPowerScore(EquipmentDataSO equipmentData, OwnedEquipmentData equipment)
        {
            if (equipmentData == null || equipment == null)
            {
                return 0f;
            }

            EquipmentResolvedBonus bonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, equipment);
            return ((bonus.AttackPercent + bonus.WisdomPercent) * 120f)
                + ((bonus.DefensePercent + bonus.MagicDefensePercent) * 105f)
                + (bonus.HpPercent * 55f)
                + (bonus.CritRate * 130f)
                + (bonus.AttackSpeed * 45f);
        }

        private static string ResolveEquipmentInventoryName(EquipmentDataSO equipmentData, OwnedEquipmentData equipment)
        {
            if (equipmentData != null && !string.IsNullOrEmpty(equipmentData.equipmentName))
            {
                return equipmentData.equipmentName;
            }

            return equipment != null ? equipment.EquipmentId : string.Empty;
        }

        private static string GetEquipmentInventoryFilterLabel(EquipmentInventoryFilter filter)
        {
            switch (filter)
            {
                case EquipmentInventoryFilter.Weapon:
                    return "武器";
                case EquipmentInventoryFilter.Armor:
                    return "防具";
                case EquipmentInventoryFilter.Accessory:
                    return "装飾品";
                default:
                    return "全て";
            }
        }

        private string GetEquipmentInventorySortLabel()
        {
            switch (equipmentInventorySortMode)
            {
                case EquipmentInventorySortMode.Rarity:
                    return "レア度";
                case EquipmentInventorySortMode.Power:
                    return "能力値";
                case EquipmentInventorySortMode.Name:
                    return "名前";
                default:
                    return "通常";
            }
        }

        private void RebuildEquipmentInventory(PlayerProfile profile, OwnedMonsterData selectedMonster)
        {
            if (equipmentInventoryContentRect == null)
            {
                return;
            }

            ClearChildren(equipmentInventoryContentRect);
            Font font = ResolveRuntimeFont();
            int totalEquipmentCount = profile != null ? profile.OwnedEquipments.Count : 0;
            int equipmentStorageLimit = profile != null ? profile.EquipmentStorageLimit : PlayerProfile.DefaultEquipmentStorageLimit;
            List<OwnedEquipmentData> sortedEquipments = BuildEquipmentInventoryDisplayEquipments(profile);
            UpdateEquipmentInventoryControls(sortedEquipments.Count, totalEquipmentCount, equipmentStorageLimit);

            if (totalEquipmentCount <= 0)
            {
                equipmentInventoryContentRect.sizeDelta = new Vector2(EquipmentInventoryWidth, EquipmentInventoryViewportHeight);
                CreateText("EquipmentEmptyState", equipmentInventoryContentRect, font, "所持装備がありません", 20, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 40f));
                return;
            }

            if (sortedEquipments.Count <= 0)
            {
                equipmentInventoryContentRect.sizeDelta = new Vector2(EquipmentInventoryWidth, EquipmentInventoryViewportHeight);
                CreateText("EquipmentEmptyState", equipmentInventoryContentRect, font, "条件に合う装備がありません", 20, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 40f));
                return;
            }

            int rowCount = (sortedEquipments.Count + 1) / 2;
            equipmentInventoryContentRect.sizeDelta = new Vector2(EquipmentInventoryWidth, Mathf.Max(EquipmentInventoryViewportHeight, rowCount * 244f));

            for (int i = 0; i < sortedEquipments.Count; i += 1)
            {
                CreateEquipmentInventoryCard(equipmentInventoryContentRect, font, profile, selectedMonster, sortedEquipments[i], i);
            }
        }

        private void CreateEquipmentInventoryCard(Transform parent, Font font, PlayerProfile profile, OwnedMonsterData selectedMonster, OwnedEquipmentData equipment, int index)
        {
            EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
            int row = index / 2;
            int column = index % 2;

            GameObject card = CreateUiObject("EquipmentCard_" + index, parent);
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(column * 442f, -(row * 244f));
            rect.sizeDelta = new Vector2(420f, 224f);

            Image frame = card.AddComponent<Image>();
            bool equippedToSelectedMonster = selectedMonster != null && equipment.EquippedMonsterInstanceId == selectedMonster.InstanceId;
            bool equippedToOtherMonster = !string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId) && !equippedToSelectedMonster;
            frame.color = equippedToSelectedMonster
                ? new Color(0.17f, 0.34f, 0.24f, 0.96f)
                : (equippedToOtherMonster ? new Color(0.23f, 0.18f, 0.16f, 0.94f) : new Color(0.15f, 0.19f, 0.25f, 0.96f));

            RawImage equipmentFrame = CreateRawPortrait($"EquipmentFrame{index}", card.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -36f), new Vector2(104f, 104f));
            equipmentFrame.texture = LoadMonsterTexture(ResolveEquipmentFrameTexturePath(equipmentData, equipment));
            equipmentFrame.color = equipmentFrame.texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            equipmentFrame.raycastTarget = false;

            RawImage equipmentIcon = CreateRawPortrait($"EquipmentIcon{index}", card.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -50f), new Vector2(76f, 76f));
            equipmentIcon.texture = LoadMonsterTexture(ResolveEquipmentIconTexturePath(equipment.EquipmentId));
            equipmentIcon.color = equipmentIcon.texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            equipmentIcon.raycastTarget = false;

            CreateText("SlotLabel", card.transform, font, BuildSlotLabel(equipmentData != null ? equipmentData.slotType : EquipmentSlotType.Weapon), 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.92f, 0.76f, 0.42f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(122f, -16f), new Vector2(120f, 22f));
            Text nameText = CreateText("Name", card.transform, font, equipmentData != null ? equipmentData.equipmentName : equipment.EquipmentId, 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(122f, -48f), new Vector2(230f, 28f));
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 16;
            nameText.resizeTextMaxSize = 24;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;

            Text ownerText = CreateText("Owner", card.transform, font, BuildEquipmentOwnerText(profile, equipment), 15, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color(0.84f, 0.9f, 0.96f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-50f, -18f), new Vector2(136f, 24f));
            ownerText.resizeTextForBestFit = true;
            ownerText.resizeTextMinSize = 11;
            ownerText.resizeTextMaxSize = 15;
            ownerText.verticalOverflow = VerticalWrapMode.Truncate;

            Text statsText = CreateText("Stats", card.transform, font, BuildEquipmentInventoryStatSummary(equipmentData, equipment), 15, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 0.94f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(122f, -84f), new Vector2(282f, 54f));
            statsText.verticalOverflow = VerticalWrapMode.Truncate;

            string qualityAndAttempts = $"{EquipmentEnhancementCatalog.BuildQualityLabel(equipmentData, equipment)}  強化可能 {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipment)}";
            Text enhanceText = CreateText("Enhance", card.transform, font, qualityAndAttempts, 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, equipment.IsLocked ? new Color(0.96f, 0.74f, 0.44f) : new Color(0.70f, 0.94f, 0.76f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -142f), new Vector2(282f, 22f));
            enhanceText.resizeTextForBestFit = true;
            enhanceText.resizeTextMinSize = 11;
            enhanceText.resizeTextMaxSize = 14;
            enhanceText.verticalOverflow = VerticalWrapMode.Truncate;

            CreateIconActionButton($"EquipmentLock{index}", card.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(34f, 34f),
                equipment.IsLocked ? LockedEquipmentIconTexturePath : UnlockedEquipmentIconTexturePath,
                () => ToggleEquipmentLockState(equipment.InstanceId));

            const float actionButtonWidth = 91f;
            const float actionButtonHeight = 34f;
            const float actionButtonGap = 8f;
            const float actionButtonY = 18f;
            float actionButtonX = 16f;
            Vector2 actionButtonSize = new Vector2(actionButtonWidth, actionButtonHeight);

            Color equipButtonColor = equippedToSelectedMonster
                ? new Color(0.18f, 0.26f, 0.50f, 0.98f)
                : new Color(0.24f, 0.42f, 0.28f, 0.96f);
            Color equipDisabledColor = equippedToSelectedMonster
                ? new Color(0.18f, 0.26f, 0.50f, 0.98f)
                : new Color(0.16f, 0.19f, 0.24f, 0.84f);

            Button equipButton = CreateActionButton(card.transform, font, equippedToSelectedMonster ? "装備中" : "装備", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(actionButtonX, actionButtonY), actionButtonSize, equipButtonColor,
                () => EquipEquipmentInstance(equipment.InstanceId), 14);
            ApplyEquipmentActionButtonFrame(equipButton, new Color(0.86f, 0.94f, 1f, 0.96f), equipDisabledColor);
            equipButton.interactable = selectedMonster != null && !equippedToSelectedMonster;
            actionButtonX += actionButtonWidth + actionButtonGap;

            Button unequipButton = CreateActionButton(card.transform, font, "外す", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(actionButtonX, actionButtonY), actionButtonSize, new Color(0.26f, 0.31f, 0.42f, 0.96f),
                () => UnequipEquipmentInstance(equipment.InstanceId), 14);
            ApplyEquipmentActionButtonFrame(unequipButton, new Color(0.72f, 0.80f, 0.96f, 0.92f), new Color(0.18f, 0.20f, 0.25f, 0.84f));
            unequipButton.interactable = equippedToSelectedMonster;
            actionButtonX += actionButtonWidth + actionButtonGap;

            Button discardButton = CreateActionButton(card.transform, font, "捨てる", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(actionButtonX, actionButtonY), actionButtonSize, new Color(0.46f, 0.20f, 0.18f, 0.96f),
                () => DiscardEquipmentInstance(equipment.InstanceId), 14);
            ApplyEquipmentActionButtonFrame(discardButton, new Color(0.96f, 0.68f, 0.58f, 0.94f), new Color(0.24f, 0.16f, 0.16f, 0.84f));
            discardButton.interactable = !equipment.IsLocked;
            actionButtonX += actionButtonWidth + actionButtonGap;

            Button enhanceButton = CreateActionButton(card.transform, font, "強化", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(actionButtonX, actionButtonY), actionButtonSize, new Color(0.30f, 0.27f, 0.52f, 0.96f),
                () => OpenEquipmentEnhancementOverlay(equipment.InstanceId), 14);
            ApplyEquipmentActionButtonFrame(enhanceButton, new Color(0.82f, 0.76f, 1f, 0.94f), new Color(0.20f, 0.18f, 0.28f, 0.84f));
        }

        private static void ApplyEquipmentActionButtonFrame(Button button, Color borderColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.disabledColor = disabledColor;
            button.colors = colors;

            Image buttonImage = button.targetGraphic as Image;
            if (buttonImage == null)
            {
                return;
            }

            Outline outline = buttonImage.gameObject.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        private void EquipEquipmentInstance(string equipmentInstanceId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            OwnedMonsterData selectedMonster = profile != null ? profile.GetOwnedMonster(selectedEquipmentMonsterInstanceId) : null;
            if (profile == null || selectedMonster == null)
            {
                equipmentLastActionMessage = "装備対象モンスターを選択してください。";
                RefreshEquipmentScene();
                return;
            }

            if (profile.EquipEquipmentToMonster(selectedMonster.InstanceId, equipmentInstanceId))
            {
                equipmentLastActionMessage = $"{GetMonsterDisplayName(selectedMonster)} に装備しました。";
                if (Application.isPlaying && SaveManager.Instance != null)
                {
                    SaveManager.Instance.SaveCurrentGame();
                }
            }

            RefreshEquipmentScene();
        }

        private void UnequipEquipmentInstance(string equipmentInstanceId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            bool unequipped = profile.TryUnequipEquipment(equipmentInstanceId, out string message);
            equipmentLastActionMessage = message;
            if (unequipped && Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            RefreshEquipmentScene();
        }

        private void ToggleEquipmentLockState(string equipmentInstanceId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            bool locked = profile.ToggleEquipmentLock(equipmentInstanceId);
            equipmentLastActionMessage = locked ? "装備をロックしました。" : "装備ロックを解除しました。";
            if (Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            RefreshEquipmentScene();
        }

        private void DiscardEquipmentInstance(string equipmentInstanceId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            bool discarded = profile.TryDiscardEquipment(equipmentInstanceId, out string message);
            equipmentLastActionMessage = message;
            if (discarded && selectedEquipmentEnhanceInstanceId == equipmentInstanceId)
            {
                CloseEquipmentEnhancementOverlay();
            }

            if (Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            RefreshEquipmentScene();
        }

        private void EnhanceEquipmentInstance(string equipmentInstanceId, string relicId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            OwnedEquipmentData targetEquipment = profile.GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            EquipmentDataSO targetEquipmentData = targetEquipment != null
                ? MasterDataManager.Instance?.GetEquipmentData(targetEquipment.EquipmentId)
                : null;
            CacheEquipmentEnhancementTargetText(targetEquipment, targetEquipmentData);
            BindEquipmentEnhancementTargetVisual(targetEquipment, targetEquipmentData, true);

            EquipmentEnhancementResult result = profile.TryEnhanceEquipment(equipmentInstanceId, relicId);
            equipmentLastActionMessage = result.Message;
            equipmentEnhanceLastActionMessage = result.Message;
            StartEquipmentEnhancementEffect(result.ResultType);
            if (result.ResultType == EquipmentEnhancementResultType.Destroyed)
            {
                selectedEquipmentEnhanceInstanceId = string.Empty;
            }

            if (Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            RefreshEquipmentScene();
            RefreshEquipmentEnhancementOverlay(profile);
        }

        private void OpenEquipmentEnhancementOverlay(string equipmentInstanceId)
        {
            selectedEquipmentEnhanceInstanceId = equipmentInstanceId;
            ResetEquipmentEnhancementFeedback();

            if (equipmentEnhanceOverlayRoot != null)
            {
                equipmentEnhanceOverlayRoot.SetActive(true);
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            RefreshEquipmentEnhancementOverlay(profile);
        }

        private void CloseEquipmentEnhancementOverlay()
        {
            selectedEquipmentEnhanceInstanceId = string.Empty;
            ResetEquipmentEnhancementFeedback();
            if (equipmentEnhanceOverlayRoot != null)
            {
                equipmentEnhanceOverlayRoot.SetActive(false);
            }
        }

        private void ResetEquipmentEnhancementFeedback()
        {
            equipmentEnhanceLastActionMessage = string.Empty;
            equipmentEnhanceTargetTitle = string.Empty;
            equipmentEnhanceTargetInfo = string.Empty;
            activeEquipmentEnhanceEffect = EquipmentEnhancementResultType.None;
            equipmentEnhanceEffectTimer = 0f;
            if (equipmentEnhanceEffectImage != null)
            {
                equipmentEnhanceEffectImage.texture = null;
                equipmentEnhanceEffectImage.color = new Color(1f, 1f, 1f, 0f);
            }

            if (equipmentEnhanceDarkOverlayImage != null)
            {
                equipmentEnhanceDarkOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            }

            if (equipmentEnhanceEffectRect != null)
            {
                equipmentEnhanceEffectRect.localScale = Vector3.one;
                equipmentEnhanceEffectRect.localEulerAngles = Vector3.zero;
            }

            if (equipmentEnhanceItemFrameImage != null)
            {
                equipmentEnhanceItemFrameImage.texture = null;
                equipmentEnhanceItemFrameImage.color = new Color(1f, 1f, 1f, 0f);
            }

            if (equipmentEnhanceItemImage != null)
            {
                equipmentEnhanceItemImage.texture = null;
                equipmentEnhanceItemImage.color = new Color(1f, 1f, 1f, 0f);
            }

            ResetEquipmentEnhancementItemTransform();
            if (equipmentEnhanceOverlayResultText != null)
            {
                equipmentEnhanceOverlayResultText.color = new Color(1f, 0.86f, 0.52f);
            }

            if (equipmentEnhanceOverlayResultText != null)
            {
                equipmentEnhanceOverlayResultText.text = string.Empty;
            }
        }

        private void RefreshEquipmentEnhancementOverlay(PlayerProfile profile)
        {
            if (equipmentEnhanceOverlayRoot == null || equipmentEnhanceOverlayListRect == null)
            {
                return;
            }

            bool hasSelection = profile != null && !string.IsNullOrEmpty(selectedEquipmentEnhanceInstanceId);
            OwnedEquipmentData equipment = hasSelection ? profile.GetOwnedEquipmentByInstanceId(selectedEquipmentEnhanceInstanceId) : null;
            EquipmentDataSO equipmentData = equipment != null ? MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId) : null;
            if (equipment != null)
            {
                CacheEquipmentEnhancementTargetText(equipment, equipmentData);
            }

            if (equipment != null || equipmentEnhanceEffectTimer <= 0f)
            {
                BindEquipmentEnhancementTargetVisual(equipment, equipmentData, false);
            }

            if (equipmentEnhanceOverlayTitleText != null)
            {
                equipmentEnhanceOverlayTitleText.text = equipmentData != null
                    ? $"{equipmentData.equipmentName}  {EquipmentEnhancementCatalog.BuildQualityLabel(equipmentData, equipment)} の強化"
                    : (!string.IsNullOrEmpty(equipmentEnhanceTargetTitle) ? equipmentEnhanceTargetTitle : "強化対象未選択");
            }

            if (equipmentEnhanceOverlayInfoText != null)
            {
                equipmentEnhanceOverlayInfoText.text = equipment != null
                    ? $"現在 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipment)} / {(equipment.IsLocked ? "ロック中" : "未ロック")}\n現在所持している強化遺物だけを表示しています。"
                    : (!string.IsNullOrEmpty(equipmentEnhanceTargetInfo) ? equipmentEnhanceTargetInfo : "装備カードの「強化」から対象装備を選ぶと、ここに使用可能な強化遺物が表示されます。");
            }

            if (equipmentEnhanceOverlayResultText != null)
            {
                equipmentEnhanceOverlayResultText.text = equipmentEnhanceLastActionMessage;
            }

            ClearChildren(equipmentEnhanceOverlayListRect);
            if (profile == null)
            {
                return;
            }

            Font font = ResolveRuntimeFont();
            int visibleRelicIndex = 0;
            for (int i = 0; i < EquipmentEnhancementCatalog.AllRelics.Count; i += 1)
            {
                EnhancementRelicDefinition relic = EquipmentEnhancementCatalog.AllRelics[i];
                if (profile.GetEnhancementRelicAmount(relic.RelicId) > 0)
                {
                    CreateEquipmentEnhancementRelicCard(equipmentEnhanceOverlayListRect, font, profile, equipment, relic, visibleRelicIndex);
                    visibleRelicIndex += 1;
                }
            }

            if (equipmentEnhanceOverlayListRect.childCount <= 0)
            {
                CreateText("EquipmentEnhanceOverlayEmpty", equipmentEnhanceOverlayListRect, font, "所持している強化遺物がありません", 22, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 36f));
            }
        }

        private void CreateEquipmentEnhancementRelicCard(Transform parent, Font font, PlayerProfile profile, OwnedEquipmentData equipment, EnhancementRelicDefinition relic, int index)
        {
            EquipmentDataSO equipmentData = equipment != null && MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipment.EquipmentId)
                : null;

            GameObject card = CreateUiObject($"EnhancementRelicCard{index + 1}", parent);
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * 146f));
            rect.sizeDelta = new Vector2(804f, 128f);

            Image frame = card.AddComponent<Image>();
            frame.color = new Color(0.14f, 0.17f, 0.22f, 0.96f);

            RawImage icon = CreateRawPortrait($"EnhancementRelicCardIcon{index + 1}", card.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(84f, 84f));
            icon.texture = LoadMonsterTexture(ResolveEnhancementRelicTexturePath(relic.RelicId));
            icon.color = Color.white;

            CreateText($"EnhancementRelicCardName{index + 1}", card.transform, font, relic.RelicName, 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(120f, -18f), new Vector2(220f, 28f));
            CreateText($"EnhancementRelicCardMeta{index + 1}", card.transform, font,
                $"成功率 {(relic.SuccessRate * 100f):0.#}% / {EquipmentEnhancementCatalog.BuildRelicEffectSummary(equipmentData, relic)} / 所持 x{profile.GetEnhancementRelicAmount(relic.RelicId)}",
                15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.78f, 0.54f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(120f, -50f), new Vector2(480f, 22f));
            CreateText($"EnhancementRelicCardDesc{index + 1}", card.transform, font, relic.Description, 15, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.80f, 0.86f, 0.92f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(120f, -78f), new Vector2(-230f, 42f));

            Button useButton = CreateActionButton(card.transform, font, "使用", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(118f, 40f), new Color(0.28f, 0.34f, 0.52f, 0.96f),
                () => EnhanceEquipmentInstance(equipment != null ? equipment.InstanceId : string.Empty, relic.RelicId), 16);

            bool canUse = equipment != null
                && equipment.RemainingEnhanceAttempts > 0
                && profile.GetEnhancementRelicAmount(relic.RelicId) > 0
                && (!equipment.IsLocked || !relic.DestroysOnFailure);
            useButton.interactable = canUse;
        }

        private static string GetMonsterDisplayName(OwnedMonsterData ownedMonster)
        {
            if (ownedMonster == null)
            {
                return "-";
            }

            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId);
            return monsterData != null ? monsterData.monsterName : ownedMonster.MonsterId;
        }

        private static string GetMonsterDamageTypeLabel(OwnedMonsterData ownedMonster)
        {
            MonsterDataSO monsterData = ownedMonster != null
                ? MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId)
                : null;
            return monsterData != null && monsterData.damageType == MonsterDamageType.Magic
                ? "魔法型"
                : "物理型";
        }

        private static string ResolveMonsterElementLabel(MonsterElement element)
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

        private static string GetMonsterPortraitResourcePath(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(monsterData.portraitResourcePath))
            {
                return monsterData.portraitResourcePath;
            }

            if (!string.IsNullOrEmpty(monsterData.illustrationResourcePath))
            {
                return monsterData.illustrationResourcePath;
            }

            return string.Empty;
        }

        private static string BuildMonsterEquipmentLine(PlayerProfile profile, OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            string label = BuildCompactSlotLabel(slotType);
            if (profile == null || monster == null)
            {
                return $"{label}  -";
            }

            OwnedEquipmentData equipped = profile.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            if (equipped == null)
            {
                return $"{label}  -";
            }

            EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipped.EquipmentId);
            string name = equipmentData != null ? equipmentData.equipmentName : equipped.EquipmentId;
            string summary = BuildCompactEquipmentSummary(equipmentData, equipped);
            return $"{label}:{name} [{EquipmentEnhancementCatalog.ResolveQualityName(equipmentData, equipped)}] {summary} {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipped)}";
        }

        private static string BuildCompactSlotLabel(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Weapon => "武",
                EquipmentSlotType.Armor => "防",
                EquipmentSlotType.Accessory => "装",
                _ => "装"
            };
        }

        private static string BuildCompactEquipmentSummary(EquipmentDataSO equipmentData, OwnedEquipmentData equipment)
        {
            if (equipmentData == null || equipment == null)
            {
                return "補正なし";
            }

            EquipmentResolvedBonus bonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, equipment);
            var parts = new List<string>();
            if (bonus.AttackPercent > 0.0001f) parts.Add($"攻+{bonus.AttackPercent * 100f:0.#}%");
            if (bonus.WisdomPercent > 0.0001f) parts.Add($"賢+{bonus.WisdomPercent * 100f:0.#}%");
            if (bonus.DefensePercent > 0.0001f) parts.Add($"防+{bonus.DefensePercent * 100f:0.#}%");
            if (bonus.MagicDefensePercent > 0.0001f) parts.Add($"魔+{bonus.MagicDefensePercent * 100f:0.#}%");
            if (bonus.HpPercent > 0.0001f) parts.Add($"HP+{bonus.HpPercent * 100f:0.#}%");
            if (bonus.CritRate > 0.0001f) parts.Add($"会+{bonus.CritRate * 100f:0.#}%");
            if (bonus.AttackSpeed > 0.0001f) parts.Add($"速+{bonus.AttackSpeed:0.###}");
            return parts.Count > 0 ? string.Join(" ", parts) : "補正なし";
        }

        private static string BuildMonsterEquipmentSummary(PlayerProfile profile, OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return "モンスターを選択すると装備補正が表示されます。";
            }

            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monster.MonsterId);
            BattleUnitStats stats = MonsterBattleStatsFactory.Create(profile, monster, monsterData);
            if (stats == null)
            {
                return "戦力プレビューを取得できません。";
            }

            return $"HP {stats.MaxHp}\n攻撃 {stats.Attack}\n賢さ {stats.Wisdom}\n防御 {stats.Defense}\n魔防 {stats.MagicDefense}\n会心 {(stats.CritRate * 100f):0.#}%\n速度 {stats.AttackSpeed:0.###}";
        }

        private static string BuildMonsterEquipmentBonusSummary(PlayerProfile profile, OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return "装備合計 -";
            }

            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monster.MonsterId);
            BattleUnitStats equippedStats = MonsterBattleStatsFactory.Create(profile, monster, monsterData);
            BattleUnitStats baseStats = MonsterBattleStatsFactory.Create(null, monster, monsterData);
            if (equippedStats == null || baseStats == null)
            {
                return "装備合計 -";
            }

            EquipmentResolvedBonus bonus = profile.GetMonsterEquipmentBonus(monster.InstanceId);
            return BuildMonsterEquipmentBonusSummary(bonus, baseStats, equippedStats);
        }

        private static string BuildMonsterEquipmentBonusSummary(EquipmentResolvedBonus bonus, BattleUnitStats baseStats, BattleUnitStats equippedStats)
        {
            var parts = new List<string>();
            if (bonus.AttackPercent > 0.0001f) parts.Add($"攻+{bonus.AttackPercent * 100f:0.#}%({FormatSignedInt(equippedStats.Attack - baseStats.Attack)})");
            if (bonus.WisdomPercent > 0.0001f) parts.Add($"賢+{bonus.WisdomPercent * 100f:0.#}%({FormatSignedInt(equippedStats.Wisdom - baseStats.Wisdom)})");
            if (bonus.DefensePercent > 0.0001f) parts.Add($"防+{bonus.DefensePercent * 100f:0.#}%({FormatSignedInt(equippedStats.Defense - baseStats.Defense)})");
            if (bonus.MagicDefensePercent > 0.0001f) parts.Add($"魔防+{bonus.MagicDefensePercent * 100f:0.#}%({FormatSignedInt(equippedStats.MagicDefense - baseStats.MagicDefense)})");
            if (bonus.HpPercent > 0.0001f) parts.Add($"HP+{bonus.HpPercent * 100f:0.#}%({FormatSignedInt(equippedStats.MaxHp - baseStats.MaxHp)})");
            if (bonus.CritRate > 0.0001f) parts.Add($"会心+{bonus.CritRate * 100f:0.#}%");
            if (bonus.AttackSpeed > 0.0001f) parts.Add($"速+{bonus.AttackSpeed:0.###}");

            if (parts.Count == 0)
            {
                return "装備合計 補正なし";
            }

            int firstLineCount = Mathf.Min(3, parts.Count);
            string firstLine = "装備合計 " + string.Join(" ", parts.GetRange(0, firstLineCount));
            if (parts.Count <= firstLineCount)
            {
                return firstLine;
            }

            return firstLine + "\n" + string.Join(" ", parts.GetRange(firstLineCount, parts.Count - firstLineCount));
        }

        private static string FormatSignedInt(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        private static string ResolveEnhancementRelicTexturePath(string relicId)
        {
            switch (relicId)
            {
                case "relic_safe_ember":
                    return SafeRelicTexturePath;
                case "relic_risky_ember":
                    return RiskyRelicTexturePath;
                case "relic_volatile_ember":
                    return VolatileRelicTexturePath;
                default:
                    return string.Empty;
            }
        }

        private void StartEquipmentEnhancementEffect(EquipmentEnhancementResultType resultType)
        {
            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                case EquipmentEnhancementResultType.Failed:
                case EquipmentEnhancementResultType.Destroyed:
                    activeEquipmentEnhanceEffect = resultType;
                    equipmentEnhanceEffectTimer = EquipmentEnhanceEffectDuration;
                    BringEquipmentEnhancementVisualsToFront(resultType);
                    ApplyEquipmentEnhancementResultTextColor(resultType);
                    break;
                default:
                    activeEquipmentEnhanceEffect = EquipmentEnhancementResultType.None;
                    equipmentEnhanceEffectTimer = 0f;
                    break;
            }
        }

        private void CacheEquipmentEnhancementTargetText(OwnedEquipmentData equipment, EquipmentDataSO equipmentData)
        {
            if (equipment == null)
            {
                return;
            }

            string equipmentName = equipmentData != null ? equipmentData.equipmentName : equipment.EquipmentId;
            equipmentEnhanceTargetTitle = $"{equipmentName}  {EquipmentEnhancementCatalog.BuildQualityLabel(equipmentData, equipment)} の強化";
            equipmentEnhanceTargetInfo = $"現在 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipment)} / {(equipment.IsLocked ? "ロック中" : "未ロック")}\n現在所持している強化遺物だけを表示しています。";
        }

        private void AnimateEquipmentEnhancementEffect()
        {
            float time = Application.isPlaying ? Time.unscaledTime : 0f;
            if (equipmentEnhanceRuneRect != null)
            {
                float scale = 1f + Mathf.Sin(time * 2.8f) * 0.04f;
                equipmentEnhanceRuneRect.localScale = Vector3.one * scale;
                equipmentEnhanceRuneRect.localEulerAngles = new Vector3(0f, 0f, time * 16f);
            }

            if (equipmentEnhanceEffectImage == null || equipmentEnhanceEffectTimer <= 0f)
            {
                if (equipmentEnhanceEffectImage != null)
                {
                    equipmentEnhanceEffectImage.color = new Color(1f, 1f, 1f, 0f);
                }

                if (equipmentEnhanceDarkOverlayImage != null)
                {
                    equipmentEnhanceDarkOverlayImage.color = new Color(0f, 0f, 0f, 0f);
                }

                ResetEquipmentEnhancementItemTransform();
                if (activeEquipmentEnhanceEffect != EquipmentEnhancementResultType.None)
                {
                    activeEquipmentEnhanceEffect = EquipmentEnhancementResultType.None;
                    if (string.IsNullOrEmpty(selectedEquipmentEnhanceInstanceId))
                    {
                        BindEquipmentEnhancementTargetVisual(null, null, false);
                        equipmentEnhanceTargetTitle = string.Empty;
                        equipmentEnhanceTargetInfo = string.Empty;
                        if (equipmentEnhanceOverlayTitleText != null)
                        {
                            equipmentEnhanceOverlayTitleText.text = "強化対象未選択";
                        }

                        if (equipmentEnhanceOverlayInfoText != null)
                        {
                            equipmentEnhanceOverlayInfoText.text = "装備カードの「強化」から対象装備を選ぶと、ここに使用可能な強化遺物が表示されます。";
                        }
                    }
                    else
                    {
                        SetEquipmentEnhancementItemVisual(Color.white, 1f, Vector2.zero, 0f);
                    }
                }

                return;
            }

            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
            equipmentEnhanceEffectTimer = Mathf.Max(0f, equipmentEnhanceEffectTimer - deltaTime);
            float progress = Mathf.Clamp01(1f - equipmentEnhanceEffectTimer / EquipmentEnhanceEffectDuration);
            Texture2D[] frames = ResolveEquipmentEnhancementEffectTextures(activeEquipmentEnhanceEffect);
            if (frames != null && frames.Length > 0)
            {
                int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1);
                equipmentEnhanceEffectImage.texture = frames[frameIndex];
            }

            AnimateEquipmentEnhancementResultVisuals(activeEquipmentEnhanceEffect, progress, time);
        }

        private void BindEquipmentEnhancementTargetVisual(OwnedEquipmentData equipment, EquipmentDataSO equipmentData, bool forceVisible)
        {
            Texture2D frameTexture = equipmentData != null ? LoadMonsterTexture(ResolveEquipmentFrameTexturePath(equipmentData, equipment)) : null;
            Texture2D iconTexture = equipment != null ? LoadMonsterTexture(ResolveEquipmentIconTexturePath(equipment.EquipmentId)) : null;
            bool visible = forceVisible || equipment != null;

            if (equipmentEnhanceItemFrameImage != null)
            {
                equipmentEnhanceItemFrameImage.texture = frameTexture;
                equipmentEnhanceItemFrameImage.color = visible && frameTexture != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f);
            }

            if (equipmentEnhanceItemImage != null)
            {
                equipmentEnhanceItemImage.texture = iconTexture;
                equipmentEnhanceItemImage.color = visible && iconTexture != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f);
            }

            ResetEquipmentEnhancementItemTransform();
            BringEquipmentEnhancementVisualsToFront(activeEquipmentEnhanceEffect);
        }

        private void BringEquipmentEnhancementVisualsToFront(EquipmentEnhancementResultType resultType)
        {
            if (equipmentEnhanceDarkOverlayImage != null)
            {
                equipmentEnhanceDarkOverlayImage.transform.SetAsLastSibling();
            }

            if (equipmentEnhanceEffectImage != null)
            {
                equipmentEnhanceEffectImage.transform.SetAsLastSibling();
            }

            if (equipmentEnhanceItemFrameImage != null)
            {
                equipmentEnhanceItemFrameImage.transform.SetAsLastSibling();
            }

            if (equipmentEnhanceItemImage != null)
            {
                equipmentEnhanceItemImage.transform.SetAsLastSibling();
            }

            if (resultType == EquipmentEnhancementResultType.Destroyed && equipmentEnhanceEffectImage != null)
            {
                equipmentEnhanceEffectImage.transform.SetAsLastSibling();
            }
        }

        private void ApplyEquipmentEnhancementResultTextColor(EquipmentEnhancementResultType resultType)
        {
            if (equipmentEnhanceOverlayResultText == null)
            {
                return;
            }

            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                    equipmentEnhanceOverlayResultText.color = new Color(1f, 0.92f, 0.44f);
                    break;
                case EquipmentEnhancementResultType.Destroyed:
                    equipmentEnhanceOverlayResultText.color = new Color(1f, 0.36f, 0.24f);
                    break;
                case EquipmentEnhancementResultType.Failed:
                    equipmentEnhanceOverlayResultText.color = new Color(0.56f, 0.66f, 0.88f);
                    break;
                default:
                    equipmentEnhanceOverlayResultText.color = new Color(1f, 0.86f, 0.52f);
                    break;
            }
        }

        private void AnimateEquipmentEnhancementResultVisuals(EquipmentEnhancementResultType resultType, float progress, float time)
        {
            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                    AnimateEquipmentEnhancementSuccess(progress, time);
                    break;
                case EquipmentEnhancementResultType.Destroyed:
                    AnimateEquipmentEnhancementDestroyed(progress, time);
                    break;
                case EquipmentEnhancementResultType.Failed:
                    AnimateEquipmentEnhancementFailed(progress, time);
                    break;
                default:
                    AnimateEquipmentEnhancementFailed(progress, time);
                    break;
            }
        }

        private void AnimateEquipmentEnhancementSuccess(float progress, float time)
        {
            float burst = Mathf.Sin(progress * Mathf.PI);
            float sparkle = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 10f));
            SetEquipmentEnhancementDarkOverlay(new Color(0f, 0f, 0f, 0f));
            SetEquipmentEnhancementEffectVisual(
                new Color(1f, 0.92f, 0.48f, Mathf.Clamp01(0.28f + burst * 0.98f)),
                0.72f + progress * 0.92f + sparkle * 0.16f,
                Mathf.Sin(time * 24f) * 4f);
            SetEquipmentEnhancementItemVisual(
                Color.white,
                1.04f + burst * 0.26f + sparkle * 0.08f,
                new Vector2(0f, burst * 8f),
                Mathf.Sin(time * 20f) * burst * 4f);
        }

        private void AnimateEquipmentEnhancementFailed(float progress, float time)
        {
            float gloom = Mathf.Sin(progress * Mathf.PI);
            float shake = Mathf.Sin(time * 54f) * Mathf.Clamp01(gloom * 1.35f);
            SetEquipmentEnhancementDarkOverlay(new Color(0.01f, 0.02f, 0.06f, Mathf.Clamp01(gloom * 0.62f)));
            SetEquipmentEnhancementEffectVisual(
                new Color(0.34f, 0.44f, 0.92f, Mathf.Clamp01(0.18f + gloom * 0.74f)),
                0.86f + progress * 0.28f,
                -progress * 10f);
            SetEquipmentEnhancementItemVisual(
                new Color(0.48f, 0.54f, 0.68f, 1f),
                0.98f - gloom * 0.10f,
                new Vector2(shake * 8f, -gloom * 8f),
                shake * 5f);
        }

        private void AnimateEquipmentEnhancementDestroyed(float progress, float time)
        {
            float charge = Mathf.Clamp01(progress / 0.38f);
            float explode = Mathf.Clamp01((progress - 0.34f) / 0.34f);
            float fade = 1f - Mathf.Clamp01((progress - 0.48f) / 0.30f);
            float violence = Mathf.Sin(time * 82f) * (1f - explode * 0.45f);
            SetEquipmentEnhancementDarkOverlay(new Color(0.16f, 0.02f, 0.01f, Mathf.Clamp01(0.24f + Mathf.Sin(progress * Mathf.PI) * 0.54f)));
            SetEquipmentEnhancementEffectVisual(
                new Color(1f, 0.42f, 0.24f, Mathf.Clamp01(0.34f + Mathf.Sin(progress * Mathf.PI) * 0.98f)),
                0.62f + progress * 1.42f,
                violence * 6f);
            SetEquipmentEnhancementItemVisual(
                new Color(1f, 0.64f, 0.50f, fade),
                Mathf.Lerp(1f, 1.56f, charge) + explode * 0.36f,
                new Vector2(violence * 14f, Mathf.Sin(time * 68f) * 10f * (1f - explode)),
                violence * 14f);
        }

        private void SetEquipmentEnhancementDarkOverlay(Color color)
        {
            if (equipmentEnhanceDarkOverlayImage != null)
            {
                equipmentEnhanceDarkOverlayImage.color = color;
            }
        }

        private void SetEquipmentEnhancementEffectVisual(Color color, float scale, float rotation)
        {
            if (equipmentEnhanceEffectImage != null)
            {
                equipmentEnhanceEffectImage.color = color;
            }

            if (equipmentEnhanceEffectRect != null)
            {
                equipmentEnhanceEffectRect.localScale = Vector3.one * scale;
                equipmentEnhanceEffectRect.localEulerAngles = new Vector3(0f, 0f, rotation);
            }
        }

        private void SetEquipmentEnhancementItemVisual(Color color, float scale, Vector2 anchoredOffset, float rotation)
        {
            if (equipmentEnhanceItemFrameImage != null && equipmentEnhanceItemFrameImage.texture != null)
            {
                equipmentEnhanceItemFrameImage.color = color;
            }

            if (equipmentEnhanceItemImage != null && equipmentEnhanceItemImage.texture != null)
            {
                equipmentEnhanceItemImage.color = color;
            }

            if (equipmentEnhanceItemFrameRect != null)
            {
                equipmentEnhanceItemFrameRect.anchoredPosition = anchoredOffset;
                equipmentEnhanceItemFrameRect.localScale = Vector3.one * scale;
                equipmentEnhanceItemFrameRect.localEulerAngles = new Vector3(0f, 0f, rotation);
            }

            if (equipmentEnhanceItemRect != null)
            {
                equipmentEnhanceItemRect.anchoredPosition = anchoredOffset;
                equipmentEnhanceItemRect.localScale = Vector3.one * scale;
                equipmentEnhanceItemRect.localEulerAngles = new Vector3(0f, 0f, rotation);
            }
        }

        private void ResetEquipmentEnhancementItemTransform()
        {
            if (equipmentEnhanceItemFrameRect != null)
            {
                equipmentEnhanceItemFrameRect.anchoredPosition = Vector2.zero;
                equipmentEnhanceItemFrameRect.localScale = Vector3.one;
                equipmentEnhanceItemFrameRect.localEulerAngles = Vector3.zero;
            }

            if (equipmentEnhanceItemRect != null)
            {
                equipmentEnhanceItemRect.anchoredPosition = Vector2.zero;
                equipmentEnhanceItemRect.localScale = Vector3.one;
                equipmentEnhanceItemRect.localEulerAngles = Vector3.zero;
            }
        }

        private Texture2D[] ResolveEquipmentEnhancementEffectTextures(EquipmentEnhancementResultType resultType)
        {
            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                    return equipmentEnhanceSuccessTextures;
                case EquipmentEnhancementResultType.Destroyed:
                    return equipmentEnhanceDestroyTextures;
                case EquipmentEnhancementResultType.Failed:
                    return equipmentEnhanceFailTextures;
                default:
                    return equipmentEnhanceFailTextures;
            }
        }

        private Texture2D[] LoadTextureSequence(string basePath, int count)
        {
            Texture2D[] textures = new Texture2D[Mathf.Max(0, count)];
            for (int i = 0; i < textures.Length; i += 1)
            {
                textures[i] = LoadMonsterTexture(basePath + i);
            }

            return textures;
        }

        private static string ResolveEquipmentIconTexturePath(string equipmentId)
        {
            switch (equipmentId)
            {
                case "equip_bronze_blade":
                    return BronzeBladeIconTexturePath;
                case "equip_iron_sword":
                case "equip_iron_saber":
                    return IronBladeIconTexturePath;
                case "equip_gold_blade":
                    return GoldBladeIconTexturePath;
                case "equip_frost_greatsword":
                    return FrostGreatswordIconTexturePath;
                case "equip_c1_arcane_wand":
                    return "EquipmentIcons/ClassMagic/equip_c1_arcane_wand_icon";
                case "equip_c2_runic_staff":
                    return "EquipmentIcons/ClassMagic/equip_c2_runic_staff_icon";
                case "equip_c3_astral_scepter":
                    return "EquipmentIcons/ClassMagic/equip_c3_astral_scepter_icon";
                case "equip_c4_abyss_grimoire":
                    return "EquipmentIcons/ClassMagic/equip_c4_abyss_grimoire_icon";
                case "equip_guard_cloth":
                    return ClothArmorIconTexturePath;
                case "equip_c1_spellguard_robe":
                    return "EquipmentIcons/ClassMagic/equip_c1_spellguard_robe_icon";
                case "equip_bone_mail":
                case "equip_bastion_mail":
                    return PlateArmorIconTexturePath;
                case "equip_leather_armor":
                    return LeatherArmorIconTexturePath;
                case "equip_c2_sage_mantle":
                    return "EquipmentIcons/ClassMagic/equip_c2_sage_mantle_icon";
                case "equip_ice_dragon_armor":
                    return IceDragonArmorIconTexturePath;
                case "equip_c3_aurora_robe":
                    return "EquipmentIcons/ClassMagic/equip_c3_aurora_robe_icon";
                case "equip_c4_voidweave_raiment":
                    return "EquipmentIcons/ClassMagic/equip_c4_voidweave_raiment_icon";
                case "equip_ashen_ring":
                    return RedRingIconTexturePath;
                case "equip_sage_ring":
                    return GreenRingIconTexturePath;
                case "equip_quick_charm":
                case "equip_moon_charm":
                case "equip_apprentice_charm":
                case "equip_barrier_talisman":
                    return VioletPendantIconTexturePath;
                case "equip_c1_mana_brooch":
                    return "EquipmentIcons/ClassMagic/equip_c1_mana_brooch_icon";
                case "equip_green_ring":
                    return GreenRingIconTexturePath;
                case "equip_c2_runic_ring":
                    return "EquipmentIcons/ClassMagic/equip_c2_runic_ring_icon";
                case "equip_ice_star_talisman":
                case "equip_oracle_orb":
                    return IceStarTalismanIconTexturePath;
                case "equip_c3_starseer_charm":
                    return "EquipmentIcons/ClassMagic/equip_c3_starseer_charm_icon";
                case "equip_c4_eclipse_core":
                    return "EquipmentIcons/ClassMagic/equip_c4_eclipse_core_icon";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveEquipmentFrameTexturePath(EquipmentDataSO equipmentData, OwnedEquipmentData equipment = null)
        {
            int classRank = GetEquipmentClassRank(equipmentData, equipment);
            switch (classRank)
            {
                case 1:
                    return Class1EquipmentFrameTexturePath;
                case 2:
                    return Class2EquipmentFrameTexturePath;
                case 3:
                    return Class3EquipmentFrameTexturePath;
                case 4:
                    return Class4EquipmentFrameTexturePath;
                case 5:
                    return Class5EquipmentFrameTexturePath;
                case 6:
                    return Class6EquipmentFrameTexturePath;
                default:
                    return Class1EquipmentFrameTexturePath;
            }
        }

        private static string ResolveMonsterCardFrameTexturePath(int classRank)
        {
            switch (Mathf.Clamp(classRank, 1, 6))
            {
                case 1:
                    return Class1MonsterCardFrameTexturePath;
                case 2:
                    return Class2MonsterCardFrameTexturePath;
                case 3:
                    return Class3MonsterCardFrameTexturePath;
                case 4:
                    return Class4MonsterCardFrameTexturePath;
                case 5:
                    return Class5MonsterCardFrameTexturePath;
                case 6:
                    return Class6MonsterCardFrameTexturePath;
                default:
                    return Class1MonsterCardFrameTexturePath;
            }
        }

        private static int GetEquipmentClassRank(EquipmentDataSO equipmentData, OwnedEquipmentData equipment = null)
        {
            if (equipmentData == null && equipment == null)
            {
                return 1;
            }

            return Mathf.Clamp(EquipmentEnhancementCatalog.ResolveQualityRank(equipmentData, equipment), 1, 5);
        }

        private static string BuildEquipmentOwnerText(PlayerProfile profile, OwnedEquipmentData equipment)
        {
            if (profile == null || equipment == null)
            {
                return "未所持";
            }

            if (string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId))
            {
                return "未装備";
            }

            OwnedMonsterData owner = profile.GetOwnedMonster(equipment.EquippedMonsterInstanceId);
            return owner != null ? $"{GetMonsterDisplayName(owner)} 装備中" : "装備中";
        }

        private static string BuildEquipmentInventoryStatSummary(EquipmentDataSO equipmentData, OwnedEquipmentData equipment)
        {
            if (equipmentData == null || equipment == null)
            {
                return "装備データなし";
            }

            return EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment);
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


        private static bool IsEquipped(PlayerProfile profile, string equipmentId)
        {
            return profile != null &&
                   (profile.EquippedWeaponId == equipmentId ||
                    profile.EquippedArmorId == equipmentId ||
                    profile.EquippedAccessoryId == equipmentId);
        }

        private static string GetEquipmentName(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                return "-";
            }

            EquipmentDataSO equipmentData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipmentId)
                : null;
            return equipmentData != null ? equipmentData.equipmentName : equipmentId;
        }

        private static string BuildEquipmentSceneSummary(BattleUnitStats stats)
        {
            if (stats == null)
            {
                return "戦力プレビューを取得できません";
            }

            return $"HP {stats.MaxHp}\n攻撃 {stats.Attack}\n賢さ {stats.Wisdom}\n防御 {stats.Defense}\n魔防 {stats.MagicDefense}\n会心 {(stats.CritRate * 100f):0}%\n速度 {stats.AttackSpeed:0.###}";
        }

        private static string BuildEquipmentBonusSummary(EquipmentDataSO equipmentData)
        {
            if (equipmentData == null)
            {
                return "装備データなし";
            }

            List<string> parts = new List<string>();
            if (equipmentData.baseAttack > 0)
            {
                parts.Add($"+{equipmentData.baseAttack}% 攻撃");
            }

            if (equipmentData.baseWisdom > 0)
            {
                parts.Add($"+{equipmentData.baseWisdom}% 賢さ");
            }

            if (equipmentData.baseDefense > 0)
            {
                parts.Add($"+{equipmentData.baseDefense}% 防御");
            }

            if (equipmentData.baseMagicDefense > 0)
            {
                parts.Add($"+{equipmentData.baseMagicDefense}% 魔防");
            }

            if (equipmentData.baseHp > 0)
            {
                parts.Add($"+{equipmentData.baseHp}% HP");
            }

            if (equipmentData.bonusCritRate > 0f)
            {
                parts.Add($"+{equipmentData.bonusCritRate * 100f:0}% 会心");
            }

            if (equipmentData.bonusAttackSpeed > 0f)
            {
                parts.Add($"+{equipmentData.bonusAttackSpeed:0.###} 速度");
            }

            if (parts.Count == 0)
            {
                parts.Add("補正なし");
            }

            return string.Join(" / ", parts);
        }

        private static string BuildSlotLabel(EquipmentSlotType slotType)
        {
            switch (slotType)
            {
                case EquipmentSlotType.Weapon:
                    return "武器";
                case EquipmentSlotType.Armor:
                    return "防具";
                case EquipmentSlotType.Accessory:
                    return "装飾";
                default:
                    return "装備";
            }
        }

        private static Font ResolveRuntimeFont()
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

        private void EnsureFormationPanel()
        {
            if (formationPanelRoot != null)
            {
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }
            canvas.transform.localScale = Vector3.one;

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

            formationPanelRoot = CreateUiObject("FormationPanelRoot", canvas.transform);
            RectTransform rootRect = formationPanelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image dimmer = formationPanelRoot.AddComponent<Image>();
            dimmer.color = new Color(0.01f, 0.03f, 0.06f, 0.9f);

            Button backdropButton = formationPanelRoot.AddComponent<Button>();
            backdropButton.targetGraphic = dimmer;
            backdropButton.onClick.AddListener(CloseFormation);

            GameObject panel = CreateUiObject("FormationPanel", formationPanelRoot.transform);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1024f, 1536f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f);

            Button panelBlocker = panel.AddComponent<Button>();
            panelBlocker.targetGraphic = panelImage;

            RawImage panelBackground = CreateRawPortrait("FormationBackground", panel.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panelBackground.texture = LoadMonsterTexture(FormationScreenTexturePath);
            panelBackground.color = Color.white;

            CreateActionButton(panel.transform, font, "閉じる", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -18f), new Vector2(132f, 48f),
                new Color(0.48f, 0.22f, 0.18f, 0.92f), CloseFormation, 16);

            GameObject topPreview = CreateUiObject("FormationPreview", panel.transform);
            RectTransform previewRect = topPreview.AddComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;

            GameObject floorBadge = CreateUiObject("FloorBadge", topPreview.transform);
            RectTransform floorRect = floorBadge.AddComponent<RectTransform>();
            floorRect.anchorMin = new Vector2(0.5f, 1f);
            floorRect.anchorMax = new Vector2(0.5f, 1f);
            floorRect.pivot = new Vector2(0.5f, 1f);
            floorRect.anchoredPosition = new Vector2(0f, -52f);
            floorRect.sizeDelta = new Vector2(196f, 46f);
            floorLabelText = CreateText("FloorLabel", floorBadge.transform, font, string.Empty, 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject slotArea = CreateUiObject("SlotArea", topPreview.transform);
            RectTransform slotAreaRect = slotArea.AddComponent<RectTransform>();
            slotAreaRect.anchorMin = Vector2.zero;
            slotAreaRect.anchorMax = Vector2.one;
            slotAreaRect.offsetMin = Vector2.zero;
            slotAreaRect.offsetMax = Vector2.zero;

            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i] = CreateFormationSlot(slotArea.transform, font, i);
            }

            GameObject rosterPanel = CreateUiObject("RosterPanel", panel.transform);
            RectTransform rosterRect = rosterPanel.AddComponent<RectTransform>();
            rosterRect.anchorMin = Vector2.zero;
            rosterRect.anchorMax = Vector2.one;
            rosterRect.offsetMin = Vector2.zero;
            rosterRect.offsetMax = Vector2.zero;

            formationSummaryText = CreateText("FormationSummary", rosterPanel.transform, font, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.93f, 0.85f, 0.53f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(320f, 22f));

            CreateText("RosterTitle", rosterPanel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(84f, -708f), new Vector2(160f, 28f));

            formationHintText = CreateText("FormationHint", rosterPanel.transform, font, string.Empty, 11, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.79f, 0.86f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(360f, 18f));

            for (int i = 0; i < FormationRoster.Length; i++)
            {
                rosterViews[i] = CreateRosterCard(rosterPanel.transform, font, i);
            }

            CreateActionButton(panel.transform, font, "編成を閉じる", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(250f, 48f),
                new Color(0.62f, 0.32f, 0.12f, 0.92f), CloseFormation, 16);

            formationPanelRoot.SetActive(false);
            RefreshFormationPanel();
        }

        private void RefreshFormationPanel()
        {
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            int level = GameManager.Instance?.PlayerProfile?.Level ?? 1;
            int gold = GameManager.Instance?.PlayerProfile?.Gold ?? 0;

            if (floorLabelText != null)
            {
                floorLabelText.text = $"F{floor}";
            }

            if (formationSummaryText != null)
            {
                formationSummaryText.text = $"保有 {FormationRoster.Length}体  Lv.{level}  Gold {gold}";
            }

            if (formationHintText != null)
            {
                formationHintText.text = string.Empty;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                RefreshSlotView(i);
            }

            for (int i = 0; i < rosterViews.Length; i++)
            {
                RefreshRosterCard(i);
            }
        }

        private FormationSlotView CreateFormationSlot(Transform parent, Font font, int slotIndex)
        {
            GameObject slotObject = CreateUiObject($"FormationSlot{slotIndex + 1}", parent);
            RectTransform rect = slotObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            Vector2[] positions =
            {
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f)
            };
            rect.anchoredPosition = positions[Mathf.Clamp(slotIndex, 0, positions.Length - 1)];
            rect.sizeDelta = new Vector2(1f, 1f);

            Image frame = slotObject.AddComponent<Image>();
            frame.color = new Color(0.12f, 0.7f, 0.86f, 0.14f);

            Button button = slotObject.AddComponent<Button>();
            button.targetGraphic = frame;
            int capturedSlot = slotIndex;
            button.onClick.AddListener(() => SelectFormationSlot(capturedSlot));

            Text slotLabel = CreateText($"FormationSlotLabel{slotIndex + 1}", slotObject.transform, font, $"枠 {slotIndex + 1}", 16,
                FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.95f, 0.86f, 0.68f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 16f), new Vector2(80f, 18f));

            RawImage portrait = CreateRawPortrait($"FormationSlotPortrait{slotIndex + 1}", slotObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));

            Text nameLabel = CreateText($"FormationSlotName{slotIndex + 1}", slotObject.transform, font, string.Empty, 18,
                FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(100f, 14f));

            Text roleLabel = CreateText($"FormationSlotRole{slotIndex + 1}", slotObject.transform, font, string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.82f, 0.88f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(100f, 14f));

            return new FormationSlotView
            {
                Frame = frame,
                SlotLabel = slotLabel,
                Portrait = portrait,
                NameLabel = nameLabel,
                RoleLabel = roleLabel,
                Button = button
            };
        }

        private FormationRosterCardView CreateRosterCard(Transform parent, Font font, int monsterIndex)
        {
            GameObject cardObject = CreateUiObject($"RosterCard{monsterIndex + 1}", parent);
            RectTransform rect = cardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Vector2[] positions =
            {
                new Vector2(162f, -720f),
                new Vector2(352f, -720f),
                new Vector2(542f, -720f),
                new Vector2(732f, -720f),
                new Vector2(162f, -908f),
                new Vector2(352f, -908f),
                new Vector2(542f, -908f),
                new Vector2(732f, -908f),
                new Vector2(162f, -1096f),
                new Vector2(352f, -1096f),
                new Vector2(542f, -1096f),
                new Vector2(732f, -1096f),
                new Vector2(162f, -1284f),
                new Vector2(352f, -1284f),
                new Vector2(542f, -1284f),
                new Vector2(732f, -1284f)
            };
            rect.anchoredPosition = positions[Mathf.Clamp(monsterIndex, 0, positions.Length - 1)];
            rect.sizeDelta = new Vector2(112f, 112f);

            Image frame = cardObject.AddComponent<Image>();
            frame.color = new Color(0f, 0f, 0f, 0f);

            Button button = cardObject.AddComponent<Button>();
            button.targetGraphic = frame;
            int capturedIndex = monsterIndex;
            button.onClick.AddListener(() => AssignMonsterToSelectedSlot(capturedIndex));

            RawImage portrait = CreateRawPortrait($"RosterPortrait{monsterIndex + 1}", cardObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));

            Text nameLabel = CreateText($"RosterName{monsterIndex + 1}", cardObject.transform, font, string.Empty, 18,
                FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(90f, 12f));

            Text roleLabel = CreateText($"RosterRole{monsterIndex + 1}", cardObject.transform, font, string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(90f, 12f));

            Text stateLabel = CreateText($"RosterState{monsterIndex + 1}", cardObject.transform, font, string.Empty, 16,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.48f, 0.95f, 0.64f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(70f, 12f));

            return new FormationRosterCardView
            {
                Frame = frame,
                Portrait = portrait,
                NameLabel = nameLabel,
                RoleLabel = roleLabel,
                StateLabel = stateLabel,
                Button = button
            };
        }

        private void SelectFormationSlot(int slotIndex)
        {
            selectedSlotIndex = Mathf.Clamp(slotIndex, 0, slotViews.Length - 1);
            RefreshFormationPanel();
        }

        private void AssignMonsterToSelectedSlot(int monsterIndex)
        {
            if (monsterIndex < 0 || monsterIndex >= FormationRoster.Length)
            {
                return;
            }

            for (int i = 0; i < assignedMonsterIndices.Length; i++)
            {
                if (assignedMonsterIndices[i] == monsterIndex)
                {
                    assignedMonsterIndices[i] = assignedMonsterIndices[selectedSlotIndex];
                    break;
                }
            }

            assignedMonsterIndices[selectedSlotIndex] = monsterIndex;
            RefreshFormationPanel();
        }

        private void RefreshSlotView(int slotIndex)
        {
            FormationSlotView view = slotViews[slotIndex];
            FormationMonsterEntry entry = FormationRoster[assignedMonsterIndices[slotIndex]];
            bool isSelected = slotIndex == selectedSlotIndex;

            view.Frame.color = isSelected
                ? new Color(0.22f, 0.98f, 0.94f, 0.24f)
                : new Color(0.12f, 0.7f, 0.86f, 0.14f);

            view.SlotLabel.text = selectedSlotIndex == slotIndex ? "選択" : string.Empty;
            view.NameLabel.text = string.Empty;
            view.RoleLabel.text = string.Empty;
            view.Portrait.texture = LoadMonsterTexture(entry.TexturePath);
            view.Portrait.color = Color.white;
        }

        private void RefreshRosterCard(int monsterIndex)
        {
            FormationRosterCardView view = rosterViews[monsterIndex];
            FormationMonsterEntry entry = FormationRoster[monsterIndex];
            int assignedSlot = Array.IndexOf(assignedMonsterIndices, monsterIndex);

            view.Frame.color = assignedSlot >= 0
                ? new Color(entry.FrameColor.r, entry.FrameColor.g, entry.FrameColor.b, 0.14f)
                : new Color(0.88f, 0.84f, 0.76f, 0.1f);

            view.NameLabel.text = string.Empty;
            view.RoleLabel.text = string.Empty;
            view.StateLabel.text = string.Empty;
            view.StateLabel.color = assignedSlot >= 0
                ? new Color(0.45f, 0.98f, 0.64f)
                : new Color(0.8f, 0.78f, 0.72f);
            view.Portrait.texture = LoadMonsterTexture(entry.TexturePath);
            view.Portrait.color = Color.white;
        }

        private Texture2D LoadMonsterTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (textureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            textureCache[resourcePath] = texture;
            return texture;
        }

        private Sprite LoadMonsterSprite(string resourcePath)
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
            if (sprite == null)
            {
                Texture2D texture = LoadMonsterTexture(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }

            spriteCache[resourcePath] = sprite;
            return sprite;
        }

        private static void CreateFieldMonsterPreview(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int order)
        {
            GameObject marker = CreateUiObject(name, parent);
            RectTransform rect = marker.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image shadow = marker.AddComponent<Image>();
            shadow.color = new Color(0.12f, 0.15f, 0.16f, 0.78f - (order * 0.1f));
        }

        private static RawImage CreateRawPortrait(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject portraitObject = CreateUiObject(name, parent);
            RectTransform rect = portraitObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            RawImage portrait = portraitObject.AddComponent<RawImage>();
            portrait.color = Color.white;
            return portrait;
        }

        private static Button CreateActionButton(
            Transform parent,
            Font font,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick,
            int fontSize)
        {
            GameObject buttonObject = CreateUiObject(label + "Button", parent);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateText(label + "Text", buttonObject.transform, font, label, fontSize, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            return button;
        }

        private Button CreateIconActionButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            string texturePath,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.02f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            RawImage icon = CreateRawPortrait(name + "Icon", buttonObject.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            icon.texture = LoadMonsterTexture(texturePath);
            icon.color = Color.white;
            icon.raycastTarget = false;

            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string textValue,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject textObject = CreateUiObject(name, parent);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void ConfigureEquippedLineText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 13;
            text.resizeTextMaxSize = 20;
        }

        private static void ConfigureEquipmentBonusSummaryText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 13;
            text.resizeTextMaxSize = 20;
        }

        private static void ConfigureEquipmentSummaryText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.22f;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 20;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = parent.gameObject.layer;
            return gameObject;
        }
    }
}
