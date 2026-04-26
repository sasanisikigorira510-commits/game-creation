using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

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

        [SerializeField] private string homeSceneName = "HomeScene";
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string formationSceneName = "FormationScene";
        [SerializeField] private string equipmentSceneName = "EquipmentScene";

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
            new FormationMonsterEntry("bat", "バット", "先制", "FormationMonsters/Bat", new Color(0.62f, 0.87f, 0.53f)),
            new FormationMonsterEntry("goblin", "ゴブリン", "前衛", "FormationMonsters/Goblin", new Color(0.74f, 0.42f, 0.36f)),
            new FormationMonsterEntry("wraith", "レイス", "妨害", "FamilyMonsters/Mage/Mage3", new Color(0.52f, 0.74f, 0.9f)),
            new FormationMonsterEntry("bee", "ビー", "速攻", "FormationMonsters/Bee", new Color(0.86f, 0.75f, 0.34f)),
            new FormationMonsterEntry("naga", "ナーガ", "後衛", "FormationMonsters/Naga", new Color(0.34f, 0.75f, 0.64f)),
            new FormationMonsterEntry("worm", "ワーム", "盾役", "FamilyMonsters/Slime/Slime1", new Color(0.69f, 0.55f, 0.3f)),
            new FormationMonsterEntry("centaur", "ケンタウロス", "物理", "FormationMonsters/Centaur", new Color(0.78f, 0.47f, 0.31f)),
            new FormationMonsterEntry("ghost", "ゴースト", "支援", "FormationMonsters/Ghost", new Color(0.56f, 0.84f, 0.93f)),
            new FormationMonsterEntry("death_mage_elf", "デスメイジ", "呪術", "FamilyMonsters/Mage/Mage1", new Color(0.74f, 0.48f, 0.86f)),
            new FormationMonsterEntry("dragon_whelp", "ヒナドラ", "幼竜", "FamilyMonsters/Dragon/dragon_whelp", new Color(0.95f, 0.45f, 0.28f)),
            new FormationMonsterEntry("flare_drake", "フレアドレイク", "火竜", "FamilyMonsters/Dragon/flare_drake", new Color(0.88f, 0.5f, 0.34f)),
            new FormationMonsterEntry("abyss_dragon", "蒼黒竜アビス", "黒焔", "FamilyMonsters/Dragon/abyss_dragon", new Color(0.36f, 0.58f, 0.95f)),
            new FormationMonsterEntry("hell_knight", "ヘルナイト", "重装", "FormationMonsters/HellKnight", new Color(0.82f, 0.35f, 0.28f)),
            new FormationMonsterEntry("naga_mage", "ナーガメイジ", "魔法", "FamilyMonsters/Mage/Mage2", new Color(0.38f, 0.78f, 0.82f)),
            new FormationMonsterEntry("shadow", "シャドウ", "奇襲", "FormationMonsters/Shadow", new Color(0.45f, 0.5f, 0.68f)),
            new FormationMonsterEntry("soul_eater", "ソウルイーター", "吸収", "FormationMonsters/SoulEater", new Color(0.5f, 0.92f, 0.76f)),
            new FormationMonsterEntry("spectral_warrior", "スペクトル", "霊騎", "FamilyMonsters/Robot/Robot3", new Color(0.42f, 0.72f, 0.96f)),
            new FormationMonsterEntry("vault_guard", "ヴォルトガード", "守護", "FamilyMonsters/Robot/Robot2", new Color(0.9f, 0.76f, 0.4f))
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
        private const string Rarity1FrameTexturePath = "EquipmentFrames/eq_rarity_1_frame";
        private const string Rarity2FrameTexturePath = "EquipmentFrames/eq_rarity_2_frame";
        private const string Rarity3FrameTexturePath = "EquipmentFrames/eq_rarity_3_frame";
        private const string Rarity4FrameTexturePath = "EquipmentFrames/eq_rarity_4_frame";
        private const string Rarity5FrameTexturePath = "EquipmentFrames/eq_rarity_5_frame";
        private const string Rarity6FrameTexturePath = "EquipmentFrames/eq_rarity_6_frame";
        private const string SafeRelicTexturePath = "EquipmentRelics/relic_safe_ember_icon";
        private const string RiskyRelicTexturePath = "EquipmentRelics/relic_risky_ember_icon";
        private const string VolatileRelicTexturePath = "EquipmentRelics/relic_volatile_ember_icon";
        private const string LockedEquipmentIconTexturePath = "EquipmentUi/ui_lock_locked_icon";
        private const string UnlockedEquipmentIconTexturePath = "EquipmentUi/ui_lock_unlocked_icon";

        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private readonly FormationSlotView[] slotViews = new FormationSlotView[5];
        private readonly FormationRosterCardView[] rosterViews = new FormationRosterCardView[FormationRoster.Length];
        private readonly int[] assignedMonsterIndices = { 0, 1, 2, 3, 4 };

        private GameObject formationPanelRoot;
        private GameObject equipmentSceneRoot;
        private Text formationSummaryText;
        private Text formationHintText;
        private Text floorLabelText;
        private Text equipmentTitleText;
        private Text equipmentGoldText;
        private Text equipmentHeadlineText;
        private Text equipmentSummaryText;
        private Text equippedWeaponText;
        private Text equippedArmorText;
        private Text equippedAccessoryText;
        private Text equipmentMonsterNameText;
        private Text equipmentMonsterMetaText;
        private RectTransform equipmentInventoryContentRect;
        private GameObject equipmentEnhanceOverlayRoot;
        private RectTransform equipmentEnhanceOverlayListRect;
        private Text equipmentEnhanceOverlayTitleText;
        private Text equipmentEnhanceOverlayInfoText;
        private string selectedEquipmentMonsterInstanceId;
        private string selectedEquipmentEnhanceInstanceId;
        private string equipmentLastActionMessage;
        private int selectedSlotIndex;

        private void Start()
        {
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
            if (Application.isPlaying)
            {
                return;
            }

            if (IsEquipmentScene())
            {
                HideEquipmentSceneLegacyUi();
                EnsureEquipmentScene();
                RefreshEquipmentScene();
            }
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
            Debug.Log("[TitleSceneController] Fusion menu is not implemented yet.");
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
                "HomeMenuRoot",
                "BattleButton",
                "FormationButton",
                "EquipmentButton",
                "FusionButton"
            };

            for (int i = 0; i < objectNames.Length; i += 1)
            {
                GameObject target = GameObject.Find(objectNames[i]);
                if (target != null)
                {
                    target.SetActive(false);
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

            Font font = ResolveRuntimeFont();
            equipmentSceneRoot = CreateUiObject("EquipmentSceneRoot", canvas.transform);
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
            panelRect.sizeDelta = new Vector2(960f, 1520f);
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

            CreateActionButton(panel.transform, font, "ホームへ戻る", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(220f, 52f),
                new Color(0.36f, 0.20f, 0.12f, 0.94f), ReturnHomeFromEquipment, 18);

            equipmentTitleText = CreateText("EquipmentTitle", panel.transform, font, "装備", 42, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.97f, 0.94f, 0.86f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(260f, 48f));

            equipmentGoldText = CreateText("EquipmentGold", panel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color(0.95f, 0.86f, 0.52f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -38f), new Vector2(240f, 30f));

            equipmentHeadlineText = CreateText("EquipmentHeadline", panel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 0.96f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(760f, 32f));

            GameObject equippedPanel = CreateUiObject("EquippedSummaryPanel", panel.transform);
            RectTransform equippedPanelRect = equippedPanel.AddComponent<RectTransform>();
            equippedPanelRect.anchorMin = new Vector2(0.5f, 1f);
            equippedPanelRect.anchorMax = new Vector2(0.5f, 1f);
            equippedPanelRect.pivot = new Vector2(0.5f, 1f);
            equippedPanelRect.sizeDelta = new Vector2(872f, 260f);
            equippedPanelRect.anchoredPosition = new Vector2(0f, -136f);
            Image equippedPanelImage = equippedPanel.AddComponent<Image>();
            equippedPanelImage.color = new Color(0.10f, 0.13f, 0.17f, 0.92f);

            CreateText("EquippedHeader", equippedPanel.transform, font, "装備対象モンスター", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(180f, 32f));

            CreateActionButton(equippedPanel.transform, font, "←", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-126f, -22f), new Vector2(40f, 36f),
                new Color(0.24f, 0.20f, 0.16f, 0.96f), () => ChangeEquipmentMonster(-1), 18);
            CreateActionButton(equippedPanel.transform, font, "→", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-74f, -22f), new Vector2(40f, 36f),
                new Color(0.24f, 0.20f, 0.16f, 0.96f), () => ChangeEquipmentMonster(1), 18);

            equipmentMonsterNameText = CreateText("EquipmentMonsterName", equippedPanel.transform, font, string.Empty, 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -64f), new Vector2(420f, 32f));

            equipmentMonsterMetaText = CreateText("EquipmentMonsterMeta", equippedPanel.transform, font, string.Empty, 16, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.9f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -98f), new Vector2(420f, 24f));

            equippedWeaponText = CreateText("EquippedWeaponText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -138f), new Vector2(420f, 28f));
            equippedArmorText = CreateText("EquippedArmorText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -174f), new Vector2(420f, 28f));
            equippedAccessoryText = CreateText("EquippedAccessoryText", equippedPanel.transform, font, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(26f, -210f), new Vector2(420f, 28f));

            equipmentSummaryText = CreateText("EquipmentSummaryText", equippedPanel.transform, font, string.Empty, 18, FontStyle.Normal,
                TextAnchor.UpperRight, new Color(0.82f, 0.88f, 0.94f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-26f, -70f), new Vector2(360f, 166f));

            GameObject optionGrid = CreateUiObject("EquipmentOptionGrid", panel.transform);
            RectTransform optionGridRect = optionGrid.AddComponent<RectTransform>();
            optionGridRect.anchorMin = new Vector2(0.5f, 0f);
            optionGridRect.anchorMax = new Vector2(0.5f, 0f);
            optionGridRect.pivot = new Vector2(0.5f, 0f);
            optionGridRect.sizeDelta = new Vector2(872f, 1040f);
            optionGridRect.anchoredPosition = new Vector2(0f, 38f);

            CreateText("EquipmentListHeader", optionGrid.transform, font, "所持装備", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, -12f), new Vector2(180f, 32f));

            GameObject contentRoot = CreateUiObject("EquipmentInventoryContent", optionGrid.transform);
            equipmentInventoryContentRect = contentRoot.AddComponent<RectTransform>();
            equipmentInventoryContentRect.anchorMin = new Vector2(0f, 1f);
            equipmentInventoryContentRect.anchorMax = new Vector2(0f, 1f);
            equipmentInventoryContentRect.pivot = new Vector2(0f, 1f);
            equipmentInventoryContentRect.anchoredPosition = new Vector2(0f, -52f);
            equipmentInventoryContentRect.sizeDelta = new Vector2(872f, 960f);

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

            equipmentEnhanceOverlayListRect = CreateUiObject("EquipmentEnhanceOverlayList", overlayPanel.transform).AddComponent<RectTransform>();
            equipmentEnhanceOverlayListRect.anchorMin = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.anchorMax = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.pivot = new Vector2(0f, 1f);
            equipmentEnhanceOverlayListRect.anchoredPosition = new Vector2(28f, -214f);
            equipmentEnhanceOverlayListRect.sizeDelta = new Vector2(804f, 716f);

            CreateActionButton(overlayPanel.transform, font, "閉じる", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(96f, 40f),
                new Color(0.34f, 0.20f, 0.16f, 0.96f), CloseEquipmentEnhancementOverlay, 16);

            equipmentEnhanceOverlayRoot.SetActive(false);
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
                    ? $"Lv.{selectedMonster.Level}  +{selectedMonster.TotalPlusValue}  個別装備"
                    : "所持モンスターがいないため装備変更できません";
            }

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

            if (equipmentSummaryText != null)
            {
                equipmentSummaryText.text = BuildMonsterEquipmentSummary(profile, selectedMonster);
            }

            RebuildEquipmentInventory(profile, selectedMonster);
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

        private void RebuildEquipmentInventory(PlayerProfile profile, OwnedMonsterData selectedMonster)
        {
            if (equipmentInventoryContentRect == null)
            {
                return;
            }

            ClearChildren(equipmentInventoryContentRect);
            Font font = ResolveRuntimeFont();

            if (profile == null || profile.OwnedEquipments.Count <= 0)
            {
                CreateText("EquipmentEmptyState", equipmentInventoryContentRect, font, "所持装備がありません", 20, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 40f));
                return;
            }

            var sortedEquipments = new List<OwnedEquipmentData>(profile.OwnedEquipments);
            sortedEquipments.Sort((left, right) =>
            {
                EquipmentDataSO leftData = MasterDataManager.Instance?.GetEquipmentData(left.EquipmentId);
                EquipmentDataSO rightData = MasterDataManager.Instance?.GetEquipmentData(right.EquipmentId);
                int leftSlot = leftData != null ? (int)leftData.slotType : 0;
                int rightSlot = rightData != null ? (int)rightData.slotType : 0;
                int slotCompare = leftSlot.CompareTo(rightSlot);
                if (slotCompare != 0)
                {
                    return slotCompare;
                }

                string leftName = leftData != null ? leftData.equipmentName : left.EquipmentId;
                string rightName = rightData != null ? rightData.equipmentName : right.EquipmentId;
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });

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
            equipmentFrame.texture = LoadMonsterTexture(ResolveEquipmentFrameTexturePath(equipmentData));
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
            CreateText("Name", card.transform, font, equipmentData != null ? equipmentData.equipmentName : equipment.EquipmentId, 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(122f, -48f), new Vector2(200f, 28f));
            CreateText("Owner", card.transform, font, BuildEquipmentOwnerText(profile, equipment), 15, FontStyle.Bold,
                TextAnchor.MiddleRight, new Color(0.84f, 0.9f, 0.96f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-62f, -18f), new Vector2(136f, 24f));
            CreateText("Stats", card.transform, font, BuildEquipmentInventoryStatSummary(equipmentData, equipment), 15, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 0.94f), new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(122f, -84f), new Vector2(282f, 54f));
            CreateText("Enhance", card.transform, font, $"強化 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)}  残り {equipment.RemainingEnhanceAttempts}回  {(equipment.IsLocked ? "ロック中" : "未ロック")}", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, equipment.IsLocked ? new Color(0.96f, 0.74f, 0.44f) : new Color(0.70f, 0.94f, 0.76f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(122f, -142f), new Vector2(282f, 22f));

            CreateIconActionButton($"EquipmentLock{index}", card.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(34f, 34f),
                equipment.IsLocked ? LockedEquipmentIconTexturePath : UnlockedEquipmentIconTexturePath,
                () => ToggleEquipmentLockState(equipment.InstanceId));

            CreateActionButton(card.transform, font, equippedToSelectedMonster ? "装備中" : "装備", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(16f, 18f), new Vector2(76f, 34f), new Color(0.24f, 0.42f, 0.28f, 0.96f),
                () => EquipEquipmentInstance(equipment.InstanceId), 14).interactable = selectedMonster != null;
            CreateActionButton(card.transform, font, "捨てる", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(100f, 18f), new Vector2(76f, 34f), new Color(0.46f, 0.20f, 0.18f, 0.96f),
                () => DiscardEquipmentInstance(equipment.InstanceId), 14).interactable = !equipment.IsLocked;
            CreateActionButton(card.transform, font, "強化", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(184f, 18f), new Vector2(208f, 34f), new Color(0.26f, 0.24f, 0.46f, 0.96f),
                () => OpenEquipmentEnhancementOverlay(equipment.InstanceId), 14);
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

            EquipmentEnhancementResult result = profile.TryEnhanceEquipment(equipmentInstanceId, relicId);
            equipmentLastActionMessage = result.Message;
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
            if (equipmentEnhanceOverlayRoot != null)
            {
                equipmentEnhanceOverlayRoot.SetActive(false);
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

            if (equipmentEnhanceOverlayTitleText != null)
            {
                equipmentEnhanceOverlayTitleText.text = equipmentData != null
                    ? $"{equipmentData.equipmentName} の強化"
                    : "強化対象未選択";
            }

            if (equipmentEnhanceOverlayInfoText != null)
            {
                equipmentEnhanceOverlayInfoText.text = equipment != null
                    ? $"強化値 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / 残り {equipment.RemainingEnhanceAttempts}回 / {(equipment.IsLocked ? "ロック中" : "未ロック")}\n現在所持している強化遺物だけを表示しています。"
                    : "装備カードの「強化」から対象装備を選ぶと、ここに使用可能な強化遺物が表示されます。";
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
                TextAnchor.UpperLeft, new Color(0.80f, 0.86f, 0.92f), new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(120f, 16f), new Vector2(-230f, 46f));

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

        private static string BuildMonsterEquipmentLine(PlayerProfile profile, OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            string label = BuildSlotLabel(slotType);
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
            return $"{label}  {name}  ({EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipped)} / 残{equipped.RemainingEnhanceAttempts})";
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

            EquipmentResolvedBonus bonus = profile.GetMonsterEquipmentBonus(monster.InstanceId);
            return $"HP {stats.MaxHp}\n攻撃 {stats.Attack}\n賢さ {stats.Wisdom}\n防御 {stats.Defense}\n魔防 {stats.MagicDefense}\n会心 {(stats.CritRate * 100f):0.#}%\n速度 {stats.AttackSpeed:0.##}\n補正 攻+{bonus.Attack} 賢+{bonus.Wisdom} 防+{bonus.Defense} 魔防+{bonus.MagicDefense} HP+{bonus.Hp}";
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
                case "equip_guard_cloth":
                    return ClothArmorIconTexturePath;
                case "equip_bone_mail":
                case "equip_bastion_mail":
                    return PlateArmorIconTexturePath;
                case "equip_leather_armor":
                    return LeatherArmorIconTexturePath;
                case "equip_ashen_ring":
                    return RedRingIconTexturePath;
                case "equip_quick_charm":
                case "equip_moon_charm":
                    return VioletPendantIconTexturePath;
                case "equip_green_ring":
                    return GreenRingIconTexturePath;
                default:
                    return string.Empty;
            }
        }

        private static string ResolveEquipmentFrameTexturePath(EquipmentDataSO equipmentData)
        {
            int rarityRank = GetEquipmentRarityRank(equipmentData);
            switch (rarityRank)
            {
                case 1:
                    return Rarity1FrameTexturePath;
                case 2:
                    return Rarity2FrameTexturePath;
                case 3:
                    return Rarity3FrameTexturePath;
                case 4:
                    return Rarity4FrameTexturePath;
                case 5:
                    return Rarity5FrameTexturePath;
                case 6:
                    return Rarity6FrameTexturePath;
                default:
                    return Rarity1FrameTexturePath;
            }
        }

        private static int GetEquipmentRarityRank(EquipmentDataSO equipmentData)
        {
            if (equipmentData == null)
            {
                return 1;
            }

            return Mathf.Clamp(((int)equipmentData.rarity) + 1, 1, 6);
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

            EquipmentResolvedBonus bonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, equipment);
            List<string> parts = new List<string>();
            if (bonus.Attack != 0) parts.Add($"攻+{bonus.Attack}");
            if (bonus.Wisdom != 0) parts.Add($"賢+{bonus.Wisdom}");
            if (bonus.Defense != 0) parts.Add($"防+{bonus.Defense}");
            if (bonus.MagicDefense != 0) parts.Add($"魔防+{bonus.MagicDefense}");
            if (bonus.Hp != 0) parts.Add($"HP+{bonus.Hp}");
            if (Mathf.Abs(bonus.CritRate) > 0.0001f) parts.Add($"会心+{bonus.CritRate * 100f:0.#}%");
            if (Mathf.Abs(bonus.AttackSpeed) > 0.0001f) parts.Add($"速+{bonus.AttackSpeed:0.##}");
            if (parts.Count <= 0) parts.Add("補正なし");
            return string.Join(" / ", parts);
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

            return $"HP {stats.MaxHp}\n攻撃 {stats.Attack}\n賢さ {stats.Wisdom}\n防御 {stats.Defense}\n魔防 {stats.MagicDefense}\n会心 {(stats.CritRate * 100f):0}%\n速度 {stats.AttackSpeed:0.##}";
        }

        private static string BuildEquipmentBonusSummary(EquipmentDataSO equipmentData)
        {
            if (equipmentData == null)
            {
                return "装備データなし";
            }

            List<string> parts = new List<string>();
            if (equipmentData.baseAttack != 0)
            {
                parts.Add($"+{equipmentData.baseAttack} 攻撃");
            }

            if (equipmentData.baseWisdom != 0)
            {
                parts.Add($"+{equipmentData.baseWisdom} 賢さ");
            }

            if (equipmentData.baseDefense != 0)
            {
                parts.Add($"+{equipmentData.baseDefense} 防御");
            }

            if (equipmentData.baseMagicDefense != 0)
            {
                parts.Add($"+{equipmentData.baseMagicDefense} 魔防");
            }

            if (equipmentData.baseHp != 0)
            {
                parts.Add($"+{equipmentData.baseHp} HP");
            }

            if (equipmentData.bonusCritRate != 0f)
            {
                parts.Add($"+{equipmentData.bonusCritRate * 100f:0}% 会心");
            }

            if (equipmentData.bonusAttackSpeed != 0f)
            {
                parts.Add($"+{equipmentData.bonusAttackSpeed:0.##} 速度");
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
            if (textureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            textureCache[resourcePath] = texture;
            return texture;
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

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = parent.gameObject.layer;
            return gameObject;
        }
    }
}
