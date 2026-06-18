using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Formation
{
    [ExecuteAlways]
    public sealed class FormationSceneController : MonoBehaviour
    {
        private sealed class MonsterEntry
        {
            public string InstanceId;
            public string Name;
            public string ResourcePath;
            public int Level;
            public int MaxLevel;
            public int ClassRank;
            public int PlusValue;
            public int IndividualAverage;
            public int AcquiredOrder;
            public bool IsFavorite;
            public bool IsLocked;
            public MonsterDamageType DamageType;

            public MonsterEntry(string instanceId, string name, string resourcePath, int level, int maxLevel, int classRank, int plusValue, int individualAverage, int acquiredOrder, bool isFavorite, bool isLocked, MonsterDamageType damageType = MonsterDamageType.Physical)
            {
                InstanceId = instanceId;
                Name = name;
                ResourcePath = resourcePath;
                Level = level;
                MaxLevel = maxLevel;
                ClassRank = classRank;
                PlusValue = plusValue;
                IndividualAverage = individualAverage;
                AcquiredOrder = acquiredOrder;
                IsFavorite = isFavorite;
                IsLocked = isLocked;
                DamageType = damageType;
            }
        }

        private sealed class FormationSlotView
        {
            public Image Background;
            public RawImage FrameArt;
            public Image RoleBand;
            public Text RoleLabel;
            public Image Portrait;
            public Text NameLabel;
            public Text StatusLabel;
            public Image StatusTypeBadge;
            public Text StatusTypeText;
            public Image StatusActionBadge;
            public Text StatusActionText;
            public Button StatusActionButton;
            public Button Button;
        }

        private sealed class MonsterCardView
        {
            public GameObject Root;
        }

        private enum SortMode
        {
            Favorite,
            Level,
            Acquired,
            Class
        }

        private enum FilterMode
        {
            All,
            Favorite,
            Selected,
            Unselected
        }

        private enum BulkReleaseClassFilter
        {
            All,
            Class1,
            Class2,
            Class3,
            Class4Plus
        }

        private enum BulkReleaseIvFilter
        {
            All,
            Under30,
            Under50,
            Under70
        }

        [SerializeField] private string homeSceneName = "HomeScene";

        private const int MaxPartySize = 5;
        private const int DefaultStorageLimit = 100;
        private const int PlusPreviewTotalValue = 15;
        private const string PlusPreviewMonsterInstanceId = "formation_plus_preview_15";
        private const int GridColumnCount = 4;
        private const float RosterPanelWidth = 1000f;
        private const float RosterPanelTopInset = 870f;
        private const float RosterPanelBottomInset = 56f;
        private const float RosterViewportHorizontalInset = 26f;
        private const float RosterViewportTopInset = 44f;
        private const float RosterViewportBottomInset = 34f;
        private const float SelectedSlotWidth = 196f;
        private const float SelectedSlotHeight = 252f;
        private const float SelectedSlotSpacing = 4f;

        private static readonly string[] HiddenObjectNames =
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
            "RelicChamberFrame",
            "ActionCard",
            "ActionTitle",
            "LoreCard",
            "ChamberHint",
            "ContinueButton",
            "Start New RunButton",
            "HomeMenuRoot"
        };

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private readonly List<MonsterEntry> roster = new List<MonsterEntry>();
        private readonly List<MonsterEntry> selectedMonsters = new List<MonsterEntry>();
        private readonly List<MonsterCardView> rosterViews = new List<MonsterCardView>();
        private readonly List<FormationSlotView> slotViews = new List<FormationSlotView>();
        private readonly HashSet<string> bulkReleaseSelectedInstanceIds = new HashSet<string>();
        private Sprite favoriteHeartFilledSprite;
        private Sprite favoriteHeartOutlineSprite;

        private const string Class1CardFrameTexturePath = "MonsterCardFrames/monster_class_1_card_frame";
        private const string Class2CardFrameTexturePath = "MonsterCardFrames/monster_class_2_card_frame";
        private const string Class3CardFrameTexturePath = "MonsterCardFrames/monster_class_3_card_frame";
        private const string Class4CardFrameTexturePath = "MonsterCardFrames/monster_class_4_card_frame";
        private const string Class5CardFrameTexturePath = "MonsterCardFrames/monster_class_5_card_frame";
        private const string Class6CardFrameTexturePath = "MonsterCardFrames/monster_class_6_card_frame";
        private const string Class1SlotFrameTexturePath = "MonsterCardFrames/monster_class_1_slot_frame";
        private const string Class2SlotFrameTexturePath = "MonsterCardFrames/monster_class_2_slot_frame";
        private const string Class3SlotFrameTexturePath = "MonsterCardFrames/monster_class_3_slot_frame";
        private const string Class4SlotFrameTexturePath = "MonsterCardFrames/monster_class_4_slot_frame";
        private const string Class5SlotFrameTexturePath = "MonsterCardFrames/monster_class_5_slot_frame";
        private const string Class6SlotFrameTexturePath = "MonsterCardFrames/monster_class_6_slot_frame";
        private const string FavoriteHeartFilledTexturePath = "UI/Favorite/FavoriteHeartFilledImage2";
        private const string FavoriteHeartOutlineTexturePath = "UI/Favorite/FavoriteHeartOutlineImage2";
        private const string LockedMonsterIconTexturePath = "EquipmentUi/ui_lock_locked_icon";
        private const string UnlockedMonsterIconTexturePath = "EquipmentUi/ui_lock_unlocked_icon";
        private const int FavoriteHeartPixelSize = 32;
        private const float CardCornerActionInset = 4f;
        private const float CardCornerActionButtonSize = 42f;
        private const float CardCornerActionIconSize = 30f;

        private static readonly int[] FavoriteHeartLeftEdges =
        {
            -1, -1, 8, 6, 5, 4, 3, 2,
            2, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, -1,
            -1, -1, -1, -1, -1, -1, -1, -1
        };

        private static readonly int[] FavoriteHeartRightEdges =
        {
            -1, -1, 12, 14, 26, 27, 28, 29,
            29, 29, 28, 27, 26, 25, 24, 23,
            22, 21, 20, 19, 18, 17, 16, -1,
            -1, -1, -1, -1, -1, -1, -1, -1
        };

        private static readonly int[] FavoriteHeartSecondLeftEdges =
        {
            -1, -1, 19, 17, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1
        };

        private static readonly int[] FavoriteHeartSecondRightEdges =
        {
            -1, -1, 23, 25, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1
        };

        private RectTransform rosterContent;
        private Text summaryText;
        private Text sortModeLabel;
        private Text filterModeLabel;
        private Text bulkReleaseToggleLabel;
        private Text bulkReleaseClassFilterLabel;
        private Text bulkReleaseIvFilterLabel;
        private Text bulkReleaseSelectFilteredLabel;
        private Text bulkReleaseExecuteLabel;
        private Text bulkReleaseStatusLabel;
        private Text emptyStateLabel;
        private GameObject bulkReleasePanel;
        private Font runtimeFont;
        private bool scaffoldCreated;
        private SortMode currentSortMode = SortMode.Favorite;
        private FilterMode currentFilterMode = FilterMode.All;
        private BulkReleaseClassFilter currentBulkReleaseClassFilter = BulkReleaseClassFilter.All;
        private BulkReleaseIvFilter currentBulkReleaseIvFilter = BulkReleaseIvFilter.Under50;
        private bool bulkReleaseModeActive;
        private bool bulkReleaseConfirmArmed;
        private int activeSlotIndex;

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
            NormalizeCanvasScales();
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeState();
            HideSceneArtifacts();
            SeedRoster();
            EnsureScaffold();
            RefreshView();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnHome();
            }
        }

        private void ApplyEditorPreview()
        {
            NormalizeCanvasScales();
            HideSceneArtifacts();
            roster.Clear();
            selectedMonsters.Clear();
            SeedFallbackRoster();
            EnsureScaffold();
            RefreshView();
        }

        public void ReturnHome()
        {
            SaveManager.Instance?.SaveCurrentGame();
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

        private static void HideSceneArtifacts()
        {
            foreach (string objectName in HiddenObjectNames)
            {
                GameObject target = GameObject.Find(objectName);
                if (target != null)
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

        private void SeedRoster()
        {
            roster.Clear();
            selectedMonsters.Clear();

            if (TrySeedRosterFromPlayerProfile())
            {
                return;
            }

            SeedFallbackRoster();
        }

        private bool TrySeedRosterFromPlayerProfile()
        {
            var profile = GameManager.Instance?.PlayerProfile;
            var masterDataManager = MasterDataManager.Instance;
            if (profile == null || masterDataManager == null)
            {
                return false;
            }

            BootstrapPrototypeOwnedMonsters(profile, masterDataManager);
            if (profile.OwnedMonsters == null || profile.OwnedMonsters.Count == 0)
            {
                return false;
            }

            var entryLookup = new Dictionary<string, MonsterEntry>();
            foreach (var ownedMonster in profile.OwnedMonsters)
            {
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.MonsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = masterDataManager.GetMonsterData(ownedMonster.MonsterId);
                if (monsterData == null)
                {
                    continue;
                }

                var entry = new MonsterEntry(
                    ownedMonster.InstanceId,
                    monsterData.monsterName,
                    GetPortraitResourcePath(monsterData),
                    MonsterLevelService.ClampLevelToMax(ownedMonster.Level, monsterData),
                    MonsterLevelService.GetMaxLevel(monsterData),
                    Mathf.Max(1, monsterData.classRank),
                    Mathf.Max(0, ownedMonster.TotalPlusValue),
                    MonsterIndividualValueService.GetAverage(ownedMonster),
                    ownedMonster.AcquiredOrder,
                    ownedMonster.IsFavorite,
                    ownedMonster.IsLocked,
                    monsterData.damageType);

                roster.Add(entry);
                entryLookup[entry.InstanceId] = entry;
            }

            roster.Sort(CompareByAcquired);

            EnsureSelectedSlotCapacity();
            for (int i = 0; i < MaxPartySize; i += 1)
            {
                string instanceId = profile.PartyMonsterInstanceIds != null && i < profile.PartyMonsterInstanceIds.Count
                    ? profile.PartyMonsterInstanceIds[i]
                    : string.Empty;
                if (!string.IsNullOrEmpty(instanceId) && entryLookup.TryGetValue(instanceId, out MonsterEntry entry))
                {
                    selectedMonsters[i] = entry;
                }
            }

            if (CountSelectedSlots() == 0)
            {
                for (int i = 0; i < roster.Count && i < MaxPartySize; i++)
                {
                    selectedMonsters[i] = roster[i];
                }
            }

            activeSlotIndex = ResolveDefaultActiveSlotIndex();
            return roster.Count > 0;
        }

        private void BootstrapPrototypeOwnedMonsters(PlayerProfile profile, MasterDataManager masterDataManager)
        {
            if (profile == null || masterDataManager == null)
            {
                return;
            }

            if (PrototypePartyBootstrapService.EnsureParty(profile, MaxPartySize))
            {
                SaveManager.Instance?.SaveCurrentGame();
            }

            if (EnsurePlusPreviewMonster(profile, masterDataManager))
            {
                SaveManager.Instance?.SaveCurrentGame();
            }
        }

        private static bool EnsurePlusPreviewMonster(PlayerProfile profile, MasterDataManager masterDataManager)
        {
            if (profile?.OwnedMonsters == null || masterDataManager == null)
            {
                return false;
            }

            OwnedMonsterData existingPreview = profile.GetOwnedMonster(PlusPreviewMonsterInstanceId);
            if (existingPreview != null)
            {
                return NormalizePlusPreviewMonster(existingPreview);
            }

            MonsterDataSO monsterData = masterDataManager.GetMonsterData(MonsterFusionCatalog.SeraphMichaelId);
            if (monsterData == null)
            {
                return false;
            }

            OwnedMonsterData previewMonster = profile.AddOwnedMonster(
                monsterData.monsterId,
                MonsterLevelService.GetMaxLevel(monsterData));
            if (previewMonster == null)
            {
                return false;
            }

            previewMonster.InstanceId = PlusPreviewMonsterInstanceId;
            previewMonster.IsFavorite = true;
            NormalizePlusPreviewMonster(previewMonster);
            return true;
        }

        private static bool NormalizePlusPreviewMonster(OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return false;
            }

            bool changed =
                monster.PlusValue != PlusPreviewTotalValue ||
                monster.PlusHp != PlusPreviewTotalValue ||
                monster.PlusAttack != PlusPreviewTotalValue ||
                monster.PlusWisdom != PlusPreviewTotalValue ||
                monster.PlusDefense != PlusPreviewTotalValue ||
                monster.PlusMagicDefense != PlusPreviewTotalValue;

            monster.PlusValue = PlusPreviewTotalValue;
            monster.PlusHp = PlusPreviewTotalValue;
            monster.PlusAttack = PlusPreviewTotalValue;
            monster.PlusWisdom = PlusPreviewTotalValue;
            monster.PlusDefense = PlusPreviewTotalValue;
            monster.PlusMagicDefense = PlusPreviewTotalValue;
            return changed;
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

            if (!string.IsNullOrEmpty(monsterData.illustrationResourcePath))
            {
                return monsterData.illustrationResourcePath;
            }

            return string.Empty;
        }

        private void SeedFallbackRoster()
        {
            roster.Add(new MonsterEntry("dragon_whelp_a", "ヒナドラ", "FamilyMonsterCards/Dragon/dragon_whelp", 14, 20, 1, 0, 50, 9, false, false, MonsterDamageType.Magic));
            roster.Add(new MonsterEntry("chibi_gear_a", "チビギア", "FamilyMonsterCards/Robot/chibi_gear", 12, 20, 1, 0, 50, 8, false, false, MonsterDamageType.Physical));
            roster.Add(new MonsterEntry("rock_golem_a", "ロックゴーレム", "FamilyMonsterCards/Golem/rock_golem", 18, 20, 1, PlusPreviewTotalValue, 50, 7, false, false, MonsterDamageType.Physical));
            roster.Add(new MonsterEntry("apprentice_swordsman_a", "見習い剣士", "FamilyMonsterCards/Swordsman/apprentice_swordsman", 20, 20, 1, 0, 50, 6, false, false, MonsterDamageType.Physical));
            roster.Add(new MonsterEntry("apprentice_mage_a", "見習い魔導士", "FamilyMonsterCards/Mage/apprentice_mage", 20, 20, 1, 0, 50, 5, false, false, MonsterDamageType.Magic));

            EnsureSelectedSlotCapacity();
            selectedMonsters[0] = roster[0];
            selectedMonsters[1] = roster[1];
            selectedMonsters[2] = roster[2];
            selectedMonsters[3] = roster[3];
            selectedMonsters[4] = roster[4];
            activeSlotIndex = ResolveDefaultActiveSlotIndex();
        }

        private void EnsureScaffold()
        {
            if (TryBindExistingScaffold())
            {
                scaffoldCreated = true;
                return;
            }

            if (scaffoldCreated)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }
            canvas.transform.localScale = Vector3.one;

            runtimeFont = GetRuntimeFont();

            GameObject root = CreateUiObject("FormationUiRoot", canvas.transform);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            CreateBackdrop(root.transform, new Color(0.01f, 0.03f, 0.06f, 0.34f));

            GameObject header = CreatePanel("FormationHeader", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -122f), new Vector2(980f, 176f), new Color(0.04f, 0.08f, 0.13f, 0.78f));

            CreateText("TitleText", header.transform, runtimeFont, "チーム編成", 40, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -38f), new Vector2(420f, 44f), TextAnchor.MiddleCenter,
                new Color(0.96f, 0.98f, 1f, 1f));

            summaryText = CreateText("SummaryText", header.transform, runtimeFont, string.Empty, 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(760f, 32f), TextAnchor.MiddleCenter,
                new Color(0.99f, 0.9f, 0.62f, 0.98f));

            HomeReturnButtonStyle.Create(root.transform, "ReturnButton", ReturnHome);

            GameObject teamPanel = CreatePanel("SelectedPanel", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -352f), new Vector2(1020f, 370f), new Color(0.03f, 0.06f, 0.1f, 0.82f));

            CreateText("SelectedTitle", teamPanel.transform, runtimeFont, "出撃メンバー", 28, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(420f, 36f), TextAnchor.MiddleCenter,
                new Color(0.93f, 0.96f, 0.99f, 1f));

            EnsureSelectedPanelRoleGuides(teamPanel.transform);

            float totalWidth = (SelectedSlotWidth * MaxPartySize) + (SelectedSlotSpacing * (MaxPartySize - 1));
            float startX = -totalWidth * 0.5f + (SelectedSlotWidth * 0.5f);

            for (int i = 0; i < MaxPartySize; i++)
            {
                GameObject slotObject = CreatePanel("SelectedSlot" + i, teamPanel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (SelectedSlotWidth + SelectedSlotSpacing), -42f), new Vector2(SelectedSlotWidth, SelectedSlotHeight),
                    new Color(0.07f, 0.1f, 0.14f, 0.82f));

                FormationSlotView slotView = new FormationSlotView();
                slotView.Background = slotObject.GetComponent<Image>();

                GameObject slotFrameObject = CreateUiObject("FrameArt", slotObject.transform);
                RectTransform slotFrameRect = slotFrameObject.GetComponent<RectTransform>();
                slotFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotFrameRect.pivot = new Vector2(0.5f, 0.5f);
                slotFrameRect.anchoredPosition = Vector2.zero;
                slotFrameRect.sizeDelta = new Vector2(232f, 232f);
                slotView.FrameArt = slotFrameObject.AddComponent<RawImage>();
                slotView.FrameArt.color = new Color(1f, 1f, 1f, 0.94f);
                slotView.FrameArt.raycastTarget = false;
                slotFrameObject.transform.SetAsFirstSibling();

                EnsureSlotRoleChrome(slotView, slotObject.transform, i);

                GameObject portraitObject = CreateUiObject("Portrait", slotObject.transform);
                RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
                portraitRect.anchorMin = new Vector2(0.5f, 1f);
                portraitRect.anchorMax = new Vector2(0.5f, 1f);
                portraitRect.pivot = new Vector2(0.5f, 1f);
                portraitRect.anchoredPosition = new Vector2(0f, -64f);
                portraitRect.sizeDelta = new Vector2(112f, 112f);
                slotView.Portrait = portraitObject.AddComponent<Image>();
                slotView.Portrait.preserveAspect = true;

                slotView.NameLabel = CreateText("NameLabel", slotObject.transform, runtimeFont, string.Empty, 18, FontStyle.Bold,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 62f), new Vector2(180f, 30f), TextAnchor.MiddleCenter,
                    new Color(0.96f, 0.98f, 1f, 1f));

                slotView.StatusLabel = CreateText("StatusLabel", slotObject.transform, runtimeFont, string.Empty, 16, FontStyle.Normal,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 34f), new Vector2(184f, 24f), TextAnchor.MiddleCenter,
                    new Color(0.82f, 0.89f, 0.95f, 0.85f));
                EnsureSelectedSlotStatusBadges(slotView, slotObject.transform);

                slotView.Button = slotObject.AddComponent<Button>();
                int slotIndex = i;
                slotView.Button.onClick.AddListener(() => OnSlotPressed(slotIndex));
                if (slotView.StatusActionButton != null)
                {
                    slotView.StatusActionButton.onClick.AddListener(() => OnSlotActionPressed(slotIndex));
                }

                slotFrameObject.transform.SetAsFirstSibling();

                slotViews.Add(slotView);
            }

            GameObject controlPanel = CreatePanel("ControlPanel", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -734f), new Vector2(1000f, 124f), new Color(0.04f, 0.07f, 0.1f, 0.8f));

            CreateText("RosterTitle", controlPanel.transform, runtimeFont, "保有モンスター", 26, FontStyle.Bold,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(36f, 0f), new Vector2(220f, 32f), TextAnchor.MiddleLeft,
                new Color(0.95f, 0.98f, 1f, 1f));

            GameObject sortButton = CreateActionButton("SortButton", controlPanel.transform, runtimeFont, string.Empty,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-36f, 0f), new Vector2(260f, 64f),
                new Color(0.15f, 0.25f, 0.31f, 0.96f), CycleSortMode);
            sortModeLabel = FindChildText(sortButton);

            GameObject filterButton = CreateActionButton("FilterButton", controlPanel.transform, runtimeFont, string.Empty,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-318f, 0f), new Vector2(190f, 64f),
                new Color(0.11f, 0.2f, 0.15f, 0.96f), CycleFilterMode);
            filterModeLabel = FindChildText(filterButton);

            GameObject bulkReleaseButton = CreateActionButton("BulkReleaseToggleButton", controlPanel.transform, runtimeFont, string.Empty,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-530f, 0f), new Vector2(190f, 64f),
                new Color(0.36f, 0.14f, 0.10f, 0.96f), ToggleBulkReleaseMode);
            bulkReleaseToggleLabel = FindChildText(bulkReleaseButton);
            AddUiOutline(bulkReleaseButton, new Color(1f, 0.48f, 0.34f, 0.64f), new Vector2(1f, -1f));

            CreateBulkReleasePanel(root.transform);

            GameObject rosterPanel = CreatePanel("RosterPanel", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -RosterPanelTopInset), new Vector2(RosterPanelWidth, 990f), new Color(0.03f, 0.06f, 0.1f, 0.82f));
            SetCenteredVerticalStretchRect(rosterPanel.transform, RosterPanelWidth, RosterPanelTopInset, RosterPanelBottomInset);

            GameObject viewport = CreatePanel("Viewport", rosterPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(948f, 912f), new Color(0.02f, 0.04f, 0.07f, 0.38f));
            SetInsetStretchRect(
                viewport.transform,
                RosterViewportHorizontalInset,
                RosterViewportTopInset,
                RosterViewportHorizontalInset,
                RosterViewportBottomInset);
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateUiObject("Content", viewport.transform);
            rosterContent = content.GetComponent<RectTransform>();
            rosterContent.anchorMin = new Vector2(0f, 1f);
            rosterContent.anchorMax = new Vector2(1f, 1f);
            rosterContent.pivot = new Vector2(0.5f, 1f);
            rosterContent.anchoredPosition = Vector2.zero;
            rosterContent.sizeDelta = new Vector2(0f, 0f);

            ScrollRect scrollRect = rosterPanel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = rosterContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            emptyStateLabel = CreateText("EmptyState", viewport.transform, runtimeFont, "該当するモンスターがいません。", 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(480f, 40f), TextAnchor.MiddleCenter,
                new Color(0.86f, 0.91f, 0.96f, 0.88f));

            ApplyScaffoldLayout(root.transform);
            scaffoldCreated = true;
        }

        private bool TryBindExistingScaffold()
        {
            GameObject rootObject = GameObject.Find("FormationUiRoot");
            if (rootObject == null)
            {
                return false;
            }

            runtimeFont = GetRuntimeFont();

            Transform root = rootObject.transform;
            Transform selectedPanel = root.Find("SelectedPanel");
            EnsureSelectedPanelRoleGuides(selectedPanel);

            summaryText = FindText(root, "FormationHeader/SummaryText");
            sortModeLabel = FindText(root, "ControlPanel/SortButton/Label");
            filterModeLabel = FindText(root, "ControlPanel/FilterButton/Label");
            bulkReleaseToggleLabel = FindText(root, "ControlPanel/BulkReleaseToggleButton/Label");
            emptyStateLabel = FindText(root, "RosterPanel/Viewport/EmptyState");
            EnsureBulkReleaseControls(root);

            Transform rosterContentTransform = root.Find("RosterPanel/Viewport/Content");
            if (summaryText == null || sortModeLabel == null || filterModeLabel == null || emptyStateLabel == null || rosterContentTransform == null)
            {
                return false;
            }

            rosterContent = rosterContentTransform as RectTransform;

            slotViews.Clear();
            for (int i = 0; i < MaxPartySize; i++)
            {
                Transform slotTransform = root.Find("SelectedPanel/SelectedSlot" + i);
                if (slotTransform == null)
                {
                    return false;
                }

                FormationSlotView slotView = new FormationSlotView
                {
                    Background = slotTransform.GetComponent<Image>(),
                    FrameArt = FindRawImage(slotTransform, "FrameArt"),
                    RoleBand = FindImage(slotTransform, "RoleBand"),
                    RoleLabel = FindText(slotTransform, "RoleBand/RoleLabel"),
                    Portrait = FindImage(slotTransform, "Portrait"),
                    NameLabel = FindText(slotTransform, "NameLabel"),
                    StatusLabel = FindText(slotTransform, "StatusLabel"),
                    StatusTypeBadge = FindImage(slotTransform, "StatusTypeBadge"),
                    StatusTypeText = FindText(slotTransform, "StatusTypeBadge/Label"),
                    StatusActionBadge = FindImage(slotTransform, "StatusActionBadge"),
                    StatusActionText = FindText(slotTransform, "StatusActionBadge/Label"),
                    StatusActionButton = slotTransform.Find("StatusActionBadge")?.GetComponent<Button>(),
                    Button = slotTransform.GetComponent<Button>()
                };

                if (slotView.FrameArt == null)
                {
                    GameObject slotFrameObject = CreateUiObject("FrameArt", slotTransform);
                    RectTransform slotFrameRect = slotFrameObject.GetComponent<RectTransform>();
                    slotFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
                    slotFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    slotFrameRect.pivot = new Vector2(0.5f, 0.5f);
                    slotFrameRect.anchoredPosition = Vector2.zero;
                    slotFrameRect.sizeDelta = new Vector2(232f, 232f);
                    slotView.FrameArt = slotFrameObject.AddComponent<RawImage>();
                    slotView.FrameArt.color = new Color(1f, 1f, 1f, 0.94f);
                    slotView.FrameArt.raycastTarget = false;
                    slotFrameObject.transform.SetAsFirstSibling();
                }
                else
                {
                    slotView.FrameArt.transform.SetAsFirstSibling();
                }

                EnsureSlotRoleChrome(slotView, slotTransform, i);
                EnsureSelectedSlotStatusBadges(slotView, slotTransform);

                if (slotView.Background == null || slotView.FrameArt == null || slotView.Portrait == null || slotView.NameLabel == null || slotView.StatusLabel == null)
                {
                    return false;
                }

                if (slotView.Button == null)
                {
                    slotView.Button = slotTransform.gameObject.AddComponent<Button>();
                }

                slotView.Button.targetGraphic = slotView.Background;
                slotViews.Add(slotView);
            }

            ApplyScaffoldLayout(root);

            if (Application.isPlaying)
            {
                BindButtonActions(root);
            }

            return true;
        }

        private void BindButtonActions(Transform root)
        {
            Button returnButton = root.Find("ReturnButton")?.GetComponent<Button>();
            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(ReturnHome);
            }

            Button sortButton = root.Find("ControlPanel/SortButton")?.GetComponent<Button>();
            if (sortButton != null)
            {
                sortButton.onClick.RemoveAllListeners();
                sortButton.onClick.AddListener(CycleSortMode);
            }

            Button filterButton = root.Find("ControlPanel/FilterButton")?.GetComponent<Button>();
            if (filterButton != null)
            {
                filterButton.onClick.RemoveAllListeners();
                filterButton.onClick.AddListener(CycleFilterMode);
            }

            Button bulkReleaseButton = root.Find("ControlPanel/BulkReleaseToggleButton")?.GetComponent<Button>();
            if (bulkReleaseButton != null)
            {
                bulkReleaseButton.onClick.RemoveAllListeners();
                bulkReleaseButton.onClick.AddListener(ToggleBulkReleaseMode);
            }

            Button classFilterButton = root.Find("BulkReleasePanel/ClassFilterButton")?.GetComponent<Button>();
            if (classFilterButton != null)
            {
                classFilterButton.onClick.RemoveAllListeners();
                classFilterButton.onClick.AddListener(CycleBulkReleaseClassFilter);
            }

            Button ivFilterButton = root.Find("BulkReleasePanel/IvFilterButton")?.GetComponent<Button>();
            if (ivFilterButton != null)
            {
                ivFilterButton.onClick.RemoveAllListeners();
                ivFilterButton.onClick.AddListener(CycleBulkReleaseIvFilter);
            }

            Button selectFilteredButton = root.Find("BulkReleasePanel/SelectFilteredButton")?.GetComponent<Button>();
            if (selectFilteredButton != null)
            {
                selectFilteredButton.onClick.RemoveAllListeners();
                selectFilteredButton.onClick.AddListener(SelectBulkReleaseFilteredCandidates);
            }

            Button executeButton = root.Find("BulkReleasePanel/ExecuteButton")?.GetComponent<Button>();
            if (executeButton != null)
            {
                executeButton.onClick.RemoveAllListeners();
                executeButton.onClick.AddListener(ConfirmBulkReleaseSelection);
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                int slotIndex = i;
                Button button = slotViews[i].Button;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnSlotPressed(slotIndex));

                Button statusActionButton = slotViews[i].StatusActionButton;
                if (statusActionButton != null)
                {
                    statusActionButton.onClick.RemoveAllListeners();
                    statusActionButton.onClick.AddListener(() => OnSlotActionPressed(slotIndex));
                }
            }
        }

        private void EnsureBulkReleaseControls(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform controlPanel = root.Find("ControlPanel");
            if (controlPanel != null && controlPanel.Find("BulkReleaseToggleButton") == null)
            {
                GameObject bulkReleaseButton = CreateActionButton(
                    "BulkReleaseToggleButton",
                    controlPanel,
                    runtimeFont,
                    string.Empty,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-530f, 0f),
                    new Vector2(190f, 64f),
                    new Color(0.36f, 0.14f, 0.10f, 0.96f),
                    ToggleBulkReleaseMode);
                AddUiOutline(bulkReleaseButton, new Color(1f, 0.48f, 0.34f, 0.64f), new Vector2(1f, -1f));
            }

            bulkReleaseToggleLabel = FindText(root, "ControlPanel/BulkReleaseToggleButton/Label");

            if (root.Find("BulkReleasePanel") == null)
            {
                CreateBulkReleasePanel(root);
            }

            Transform panel = root.Find("BulkReleasePanel");
            bulkReleasePanel = panel != null ? panel.gameObject : null;
            bulkReleaseStatusLabel = FindText(root, "BulkReleasePanel/StatusLabel");
            bulkReleaseClassFilterLabel = FindText(root, "BulkReleasePanel/ClassFilterButton/Label");
            bulkReleaseIvFilterLabel = FindText(root, "BulkReleasePanel/IvFilterButton/Label");
            bulkReleaseSelectFilteredLabel = FindText(root, "BulkReleasePanel/SelectFilteredButton/Label");
            bulkReleaseExecuteLabel = FindText(root, "BulkReleasePanel/ExecuteButton/Label");
            ApplyBulkReleaseLayout(root);
            UpdateBulkReleaseControls();
        }

        private void CreateBulkReleasePanel(Transform root)
        {
            if (root == null)
            {
                return;
            }

            bulkReleasePanel = CreatePanel(
                "BulkReleasePanel",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -926f),
                new Vector2(1000f, 236f),
                new Color(0.025f, 0.025f, 0.03f, 0.97f));
            AddUiOutline(bulkReleasePanel, new Color(1f, 0.56f, 0.28f, 0.78f), new Vector2(2f, -2f));

            bulkReleaseStatusLabel = CreateText(
                "StatusLabel",
                bulkReleasePanel.transform,
                runtimeFont,
                string.Empty,
                18,
                FontStyle.Bold,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 86f),
                new Vector2(944f, 42f),
                TextAnchor.MiddleLeft,
                new Color(1f, 0.9f, 0.66f, 1f));

            GameObject classFilterButton = CreateActionButton(
                "ClassFilterButton",
                bulkReleasePanel.transform,
                runtimeFont,
                string.Empty,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 16f),
                new Vector2(450f, 70f),
                new Color(0.12f, 0.2f, 0.28f, 0.98f),
                CycleBulkReleaseClassFilter);
            bulkReleaseClassFilterLabel = FindChildText(classFilterButton);

            GameObject ivFilterButton = CreateActionButton(
                "IvFilterButton",
                bulkReleasePanel.transform,
                runtimeFont,
                string.Empty,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(522f, 16f),
                new Vector2(450f, 70f),
                new Color(0.12f, 0.2f, 0.28f, 0.98f),
                CycleBulkReleaseIvFilter);
            bulkReleaseIvFilterLabel = FindChildText(ivFilterButton);

            GameObject selectFilteredButton = CreateActionButton(
                "SelectFilteredButton",
                bulkReleasePanel.transform,
                runtimeFont,
                string.Empty,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, -72f),
                new Vector2(450f, 70f),
                new Color(0.12f, 0.32f, 0.2f, 0.98f),
                SelectBulkReleaseFilteredCandidates);
            bulkReleaseSelectFilteredLabel = FindChildText(selectFilteredButton);

            GameObject executeButton = CreateActionButton(
                "ExecuteButton",
                bulkReleasePanel.transform,
                runtimeFont,
                string.Empty,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(522f, -72f),
                new Vector2(450f, 70f),
                new Color(0.52f, 0.09f, 0.06f, 0.98f),
                ConfirmBulkReleaseSelection);
            bulkReleaseExecuteLabel = FindChildText(executeButton);

            bulkReleasePanel.SetActive(false);
        }

        private static void ApplyBulkReleaseLayout(Transform root)
        {
            if (root == null)
            {
                return;
            }

            SetActionButtonRect(root.Find("ControlPanel/BulkReleaseToggleButton"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-530f, 0f),
                new Vector2(190f, 64f));
            SetRect(root.Find("BulkReleasePanel"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -926f),
                new Vector2(1000f, 236f));
            SetRect(root.Find("BulkReleasePanel/StatusLabel"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 86f),
                new Vector2(944f, 42f));
            SetActionButtonRect(root.Find("BulkReleasePanel/ClassFilterButton"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 16f),
                new Vector2(450f, 70f));
            SetActionButtonRect(root.Find("BulkReleasePanel/IvFilterButton"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(522f, 16f),
                new Vector2(450f, 70f));
            SetActionButtonRect(root.Find("BulkReleasePanel/SelectFilteredButton"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, -72f),
                new Vector2(450f, 70f));
            SetActionButtonRect(root.Find("BulkReleasePanel/ExecuteButton"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(522f, -72f),
                new Vector2(450f, 70f));
            ApplyBulkReleaseVisualStyle(root);
        }

        private static void ApplyBulkReleaseVisualStyle(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform panel = root.Find("BulkReleasePanel");
            SetImageColor(panel, new Color(0.025f, 0.025f, 0.03f, 0.97f));
            if (panel != null)
            {
                AddUiOutline(panel.gameObject, new Color(1f, 0.56f, 0.28f, 0.78f), new Vector2(2f, -2f));
            }

            Text statusText = root.Find("BulkReleasePanel/StatusLabel")?.GetComponent<Text>();
            if (statusText != null)
            {
                statusText.fontSize = 21;
                statusText.color = new Color(1f, 0.9f, 0.66f, 1f);
                statusText.resizeTextForBestFit = true;
                statusText.resizeTextMinSize = 13;
                statusText.resizeTextMaxSize = 21;
                statusText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            SetImageColor(root.Find("BulkReleasePanel/ClassFilterButton"), new Color(0.12f, 0.2f, 0.28f, 0.98f));
            SetImageColor(root.Find("BulkReleasePanel/IvFilterButton"), new Color(0.12f, 0.2f, 0.28f, 0.98f));
            SetImageColor(root.Find("BulkReleasePanel/SelectFilteredButton"), new Color(0.12f, 0.32f, 0.2f, 0.98f));
            SetImageColor(root.Find("BulkReleasePanel/ExecuteButton"), new Color(0.52f, 0.09f, 0.06f, 0.98f));
            ConfigureBulkReleaseButtonLabel(root.Find("BulkReleasePanel/ClassFilterButton/Label"));
            ConfigureBulkReleaseButtonLabel(root.Find("BulkReleasePanel/IvFilterButton/Label"));
            ConfigureBulkReleaseButtonLabel(root.Find("BulkReleasePanel/SelectFilteredButton/Label"));
            ConfigureBulkReleaseButtonLabel(root.Find("BulkReleasePanel/ExecuteButton/Label"));
        }

        private static void ConfigureBulkReleaseButtonLabel(Transform labelTransform)
        {
            Text label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label == null)
            {
                return;
            }

            label.fontSize = 22;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 22;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void ApplyControlPanelLayout(Transform root)
        {
            if (root == null)
            {
                return;
            }

            SetRect(root.Find("ControlPanel"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -734f),
                new Vector2(1000f, 124f));
            SetRect(root.Find("ControlPanel/RosterTitle"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(36f, 0f),
                new Vector2(220f, 32f));
            SetActionButtonRect(root.Find("ControlPanel/BulkReleaseToggleButton"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-530f, 0f),
                new Vector2(190f, 64f));
            SetActionButtonRect(root.Find("ControlPanel/FilterButton"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-318f, 0f),
                new Vector2(190f, 64f));
            SetActionButtonRect(root.Find("ControlPanel/SortButton"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-36f, 0f),
                new Vector2(260f, 64f));
        }

        private void EnsureSelectedPanelRoleGuides(Transform selectedPanel)
        {
            if (selectedPanel == null)
            {
                return;
            }

            EnsureRoleGuidePanel(selectedPanel, "FrontLaneGuide", "前衛", ResolveSlotRoleColor(0, 0.76f));
            EnsureRoleGuidePanel(selectedPanel, "MidLaneGuide", "中衛", ResolveSlotRoleColor(2, 0.76f));
            EnsureRoleGuidePanel(selectedPanel, "RearLaneGuide", "後衛", ResolveSlotRoleColor(3, 0.76f));
            SetRoleGuideLayout(selectedPanel);
        }

        private void EnsureRoleGuidePanel(Transform selectedPanel, string objectName, string label, Color color)
        {
            Transform guideTransform = selectedPanel.Find(objectName);
            GameObject guideObject;
            Image guideImage;
            if (guideTransform == null)
            {
                guideObject = CreatePanel(objectName, selectedPanel,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(180f, 30f), color);
                guideImage = guideObject.GetComponent<Image>();
            }
            else
            {
                guideObject = guideTransform.gameObject;
                guideImage = guideObject.GetComponent<Image>();
                if (guideImage == null)
                {
                    guideImage = guideObject.AddComponent<Image>();
                }
                guideImage.color = color;
            }

            guideImage.raycastTarget = false;
            Text guideLabel = FindText(guideObject.transform, "Label");
            if (guideLabel == null)
            {
                guideLabel = CreateText("Label", guideObject.transform, runtimeFont, label, 17, FontStyle.Bold,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(120f, 24f), TextAnchor.MiddleCenter,
                    Color.white);
            }

            guideLabel.text = label;
            guideLabel.font = runtimeFont;
            guideLabel.fontSize = 17;
            guideLabel.fontStyle = FontStyle.Bold;
            guideLabel.alignment = TextAnchor.MiddleCenter;
            guideLabel.color = Color.white;
            guideLabel.raycastTarget = false;
            guideObject.transform.SetAsFirstSibling();
        }

        private void EnsureSlotRoleChrome(FormationSlotView slotView, Transform slotTransform, int slotIndex)
        {
            if (slotView == null || slotTransform == null)
            {
                return;
            }

            if (slotView.RoleBand == null)
            {
                Transform existingBand = slotTransform.Find("RoleBand");
                if (existingBand != null)
                {
                    slotView.RoleBand = existingBand.GetComponent<Image>();
                    if (slotView.RoleBand == null)
                    {
                        slotView.RoleBand = existingBand.gameObject.AddComponent<Image>();
                    }
                }
                else
                {
                    GameObject roleBandObject = CreatePanel("RoleBand", slotTransform,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -6f), new Vector2(158f, 32f), ResolveSlotRoleColor(slotIndex, 0.96f));
                    slotView.RoleBand = roleBandObject.GetComponent<Image>();
                }
            }

            slotView.RoleBand.raycastTarget = false;

            if (slotView.RoleLabel == null)
            {
                slotView.RoleLabel = FindText(slotTransform, "RoleBand/RoleLabel");
                if (slotView.RoleLabel == null)
                {
                    slotView.RoleLabel = CreateText("RoleLabel", slotView.RoleBand.transform, runtimeFont, string.Empty, 16, FontStyle.Bold,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(144f, 24f), TextAnchor.MiddleCenter,
                        Color.white);
                }
            }

            slotView.RoleLabel.font = runtimeFont;
            slotView.RoleLabel.fontSize = 16;
            slotView.RoleLabel.fontStyle = FontStyle.Bold;
            slotView.RoleLabel.alignment = TextAnchor.MiddleCenter;
            slotView.RoleLabel.text = ResolveSlotRoleLabel(slotIndex);
            slotView.RoleLabel.color = Color.white;
            slotView.RoleLabel.raycastTarget = false;
            ApplySlotRoleStyle(slotView, slotIndex, true);
            SetSlotRoleLayout(slotTransform, slotIndex);

            if (slotView.FrameArt != null)
            {
                slotView.FrameArt.transform.SetAsFirstSibling();
            }
        }

        private void EnsureSelectedSlotStatusBadges(FormationSlotView slotView, Transform slotTransform)
        {
            if (slotView == null || slotTransform == null)
            {
                return;
            }

            if (slotView.StatusTypeBadge == null)
            {
                Transform existingTypeBadge = slotTransform.Find("StatusTypeBadge");
                if (existingTypeBadge != null)
                {
                    slotView.StatusTypeBadge = existingTypeBadge.GetComponent<Image>();
                    if (slotView.StatusTypeBadge == null)
                    {
                        slotView.StatusTypeBadge = existingTypeBadge.gameObject.AddComponent<Image>();
                    }
                }
                else
                {
                    GameObject badgeObject = CreatePanel("StatusTypeBadge", slotTransform,
                        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(-52f, 34f), new Vector2(78f, 24f), new Color(0.10f, 0.20f, 0.25f, 0.92f));
                    slotView.StatusTypeBadge = badgeObject.GetComponent<Image>();
                }
            }

            if (slotView.StatusTypeText == null)
            {
                slotView.StatusTypeText = FindText(slotTransform, "StatusTypeBadge/Label");
                if (slotView.StatusTypeText == null)
                {
                    slotView.StatusTypeText = CreateText("Label", slotView.StatusTypeBadge.transform, runtimeFont, string.Empty, 14, FontStyle.Bold,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(70f, 20f), TextAnchor.MiddleCenter,
                        Color.white);
                }
            }

            if (slotView.StatusActionBadge == null)
            {
                Transform existingActionBadge = slotTransform.Find("StatusActionBadge");
                if (existingActionBadge != null)
                {
                    slotView.StatusActionBadge = existingActionBadge.GetComponent<Image>();
                    if (slotView.StatusActionBadge == null)
                    {
                        slotView.StatusActionBadge = existingActionBadge.gameObject.AddComponent<Image>();
                    }
                }
                else
                {
                    GameObject badgeObject = CreatePanel("StatusActionBadge", slotTransform,
                        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(52f, 34f), new Vector2(66f, 24f), new Color(0.24f, 0.10f, 0.10f, 0.92f));
                    slotView.StatusActionBadge = badgeObject.GetComponent<Image>();
                }
            }

            slotView.StatusActionButton = slotView.StatusActionBadge.GetComponent<Button>();
            if (slotView.StatusActionButton == null)
            {
                slotView.StatusActionButton = slotView.StatusActionBadge.gameObject.AddComponent<Button>();
            }
            slotView.StatusActionButton.targetGraphic = slotView.StatusActionBadge;
            slotView.StatusActionButton.transition = Selectable.Transition.ColorTint;

            if (slotView.StatusActionText == null)
            {
                slotView.StatusActionText = FindText(slotTransform, "StatusActionBadge/Label");
                if (slotView.StatusActionText == null)
                {
                    slotView.StatusActionText = CreateText("Label", slotView.StatusActionBadge.transform, runtimeFont, string.Empty, 14, FontStyle.Bold,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(58f, 20f), TextAnchor.MiddleCenter,
                        Color.white);
                }
            }

            ConfigureStatusBadge(slotView.StatusTypeBadge, new Color(0.10f, 0.20f, 0.25f, 0.92f), new Color(0.52f, 0.86f, 1f, 0.72f));
            ConfigureStatusBadge(slotView.StatusActionBadge, new Color(0.24f, 0.10f, 0.10f, 0.92f), new Color(1f, 0.68f, 0.45f, 0.72f));
            ConfigureStatusBadgeText(slotView.StatusTypeText, 14);
            ConfigureStatusBadgeText(slotView.StatusActionText, 14);
            SetSelectedSlotStatusBadgeLayout(slotTransform);
            SetSelectedSlotStatusBadgesVisible(slotView, false);
        }

        private static void ConfigureStatusBadge(Image badge, Color backgroundColor, Color outlineColor)
        {
            if (badge == null)
            {
                return;
            }

            badge.color = backgroundColor;
            badge.raycastTarget = badge.GetComponent<Button>() != null;

            Outline outline = badge.GetComponent<Outline>();
            if (outline == null)
            {
                outline = badge.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void ConfigureStatusBadgeText(Text text, int fontSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void ApplySlotRoleStyle(FormationSlotView slotView, int slotIndex, bool strong)
        {
            if (slotView?.RoleBand == null)
            {
                return;
            }

            slotView.RoleBand.color = ResolveSlotRoleColor(slotIndex, strong ? 0.96f : 0.68f);
        }

        private static void ApplyScaffoldLayout(Transform root)
        {
            SetRect(root.Find("FormationHeader"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(980f, 176f));
            Button returnButton = root.Find("ReturnButton")?.GetComponent<Button>();
            if (returnButton != null)
            {
                HomeReturnButtonStyle.Apply(returnButton);
            }
            SetRect(root.Find("SelectedPanel"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(1020f, 370f));
            ApplyControlPanelLayout(root);
            ApplyBulkReleaseLayout(root);
            SetCenteredVerticalStretchRect(root.Find("RosterPanel"), RosterPanelWidth, RosterPanelTopInset, RosterPanelBottomInset);
            SetInsetStretchRect(
                root.Find("RosterPanel/Viewport"),
                RosterViewportHorizontalInset,
                RosterViewportTopInset,
                RosterViewportHorizontalInset,
                RosterViewportBottomInset);
            SetRect(root.Find("FormationHeader/SummaryText"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(760f, 32f));
            SetRect(root.Find("SelectedPanel/SelectedTitle"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(420f, 36f));
            SetRoleGuideLayout(root.Find("SelectedPanel"));

            float totalWidth = (SelectedSlotWidth * MaxPartySize) + (SelectedSlotSpacing * (MaxPartySize - 1));
            float startX = -totalWidth * 0.5f + (SelectedSlotWidth * 0.5f);

            for (int i = 0; i < MaxPartySize; i++)
            {
                SetRect(
                    root.Find("SelectedPanel/SelectedSlot" + i),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (SelectedSlotWidth + SelectedSlotSpacing), -42f),
                    new Vector2(SelectedSlotWidth, SelectedSlotHeight));

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/FrameArt"),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(232f, 232f));

                SetSlotRoleLayout(root.Find("SelectedPanel/SelectedSlot" + i), i);

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/Portrait"),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -64f),
                    new Vector2(112f, 112f));
            }

            Transform guideText = root.Find("FormationHeader/GuideText");
            if (guideText != null)
            {
                guideText.gameObject.SetActive(false);
            }

            Transform selectedHint = root.Find("SelectedPanel/SelectedHint");
            if (selectedHint != null)
            {
                selectedHint.gameObject.SetActive(false);
            }

            for (int i = 0; i < MaxPartySize; i++)
            {
                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/NameLabel"),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 62f),
                    new Vector2(180f, 30f));

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/StatusLabel"),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 34f),
                    new Vector2(184f, 24f));
                SetSelectedSlotStatusBadgeLayout(root.Find("SelectedPanel/SelectedSlot" + i));
            }
        }

        private static void SetRoleGuideLayout(Transform selectedPanel)
        {
            if (selectedPanel == null)
            {
                return;
            }

            SetRect(selectedPanel.Find("FrontLaneGuide"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-312f, -64f),
                new Vector2(388f, 30f));
            SetRect(selectedPanel.Find("FrontLaneGuide/Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(150f, 24f));

            SetRect(selectedPanel.Find("MidLaneGuide"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -64f),
                new Vector2(180f, 30f));
            SetRect(selectedPanel.Find("MidLaneGuide/Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(120f, 24f));

            SetRect(selectedPanel.Find("RearLaneGuide"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(312f, -64f),
                new Vector2(388f, 30f));
            SetRect(selectedPanel.Find("RearLaneGuide/Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(150f, 24f));
        }

        private static void SetSlotRoleLayout(Transform slotTransform, int slotIndex)
        {
            if (slotTransform == null)
            {
                return;
            }

            SetRect(slotTransform.Find("RoleBand"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(158f, 32f));
            SetRect(slotTransform.Find("RoleBand/RoleLabel"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(144f, 24f));

            Text roleLabel = FindText(slotTransform, "RoleBand/RoleLabel");
            if (roleLabel != null)
            {
                roleLabel.text = ResolveSlotRoleLabel(slotIndex);
            }
        }

        private static void SetSelectedSlotStatusBadgeLayout(Transform slotTransform)
        {
            if (slotTransform == null)
            {
                return;
            }

            SetRect(slotTransform.Find("StatusTypeBadge"),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-52f, 34f),
                new Vector2(78f, 24f));
            SetRect(slotTransform.Find("StatusTypeBadge/Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(70f, 20f));

            SetRect(slotTransform.Find("StatusActionBadge"),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(52f, 34f),
                new Vector2(66f, 24f));
            SetRect(slotTransform.Find("StatusActionBadge/Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(58f, 20f));
        }

        private static string ResolveSlotRoleLabel(int slotIndex)
        {
            if (slotIndex <= 1)
            {
                return "前衛 " + (slotIndex + 1);
            }

            if (slotIndex == 2)
            {
                return "中衛";
            }

            return "後衛 " + (slotIndex - 2);
        }

        private static Color ResolveSlotRoleColor(int slotIndex, float alpha)
        {
            if (slotIndex <= 1)
            {
                return new Color(0.82f, 0.20f, 0.13f, alpha);
            }

            if (slotIndex == 2)
            {
                return new Color(0.86f, 0.58f, 0.16f, alpha);
            }

            return new Color(0.12f, 0.46f, 0.68f, alpha);
        }

        private static string ResolveDamageTypeLabel(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法型" : "物理型";
        }

        private static Color ResolveDamageTypeColor(MonsterDamageType damageType, float alpha)
        {
            return damageType == MonsterDamageType.Magic
                ? new Color(0.52f, 0.86f, 1f, alpha)
                : new Color(1f, 0.78f, 0.45f, alpha);
        }

        private void EnsureSelectedSlotCapacity()
        {
            while (selectedMonsters.Count < MaxPartySize)
            {
                selectedMonsters.Add(null);
            }

            if (selectedMonsters.Count > MaxPartySize)
            {
                selectedMonsters.RemoveRange(MaxPartySize, selectedMonsters.Count - MaxPartySize);
            }
        }

        private int CountSelectedSlots()
        {
            EnsureSelectedSlotCapacity();
            int count = 0;
            for (int i = 0; i < selectedMonsters.Count; i += 1)
            {
                if (selectedMonsters[i] != null)
                {
                    count += 1;
                }
            }

            return count;
        }

        private int FindFirstEmptySlot()
        {
            EnsureSelectedSlotCapacity();
            for (int i = 0; i < selectedMonsters.Count; i += 1)
            {
                if (selectedMonsters[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveDefaultActiveSlotIndex()
        {
            int emptySlot = FindFirstEmptySlot();
            return emptySlot >= 0 ? emptySlot : 0;
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < MaxPartySize;
        }

        private bool IsRosterEntrySelected(MonsterEntry entry)
        {
            return entry != null && selectedMonsters.Contains(entry);
        }

        private void RefreshView()
        {
            if (!scaffoldCreated)
            {
                return;
            }

            if (summaryText != null)
            {
                summaryText.text = $"保有 {roster.Count}/{GetStorageLimit()}   出撃 {CountSelectedSlots()}/{MaxPartySize}";
            }

            if (sortModeLabel != null)
            {
                sortModeLabel.text = "並び替え: " + GetSortModeLabel(currentSortMode);
            }

            if (filterModeLabel != null)
            {
                filterModeLabel.text = "表示: " + GetFilterModeLabel(currentFilterMode);
            }

            RefreshSelectedSlots();
            RefreshRosterCards();
            UpdateBulkReleaseControls();
        }

        private void RefreshSelectedSlots()
        {
            EnsureSelectedSlotCapacity();
            for (int i = 0; i < slotViews.Count; i++)
            {
                FormationSlotView view = slotViews[i];
                MonsterEntry entry = i < selectedMonsters.Count ? selectedMonsters[i] : null;
                bool isActiveSlot = i == activeSlotIndex;
                ApplySlotRoleStyle(view, i, entry != null || isActiveSlot);
                if (view.RoleLabel != null)
                {
                    view.RoleLabel.text = ResolveSlotRoleLabel(i);
                }

                if (entry != null)
                {
                    view.Background.color = isActiveSlot
                        ? new Color(0.12f, 0.21f, 0.16f, 0.82f)
                        : new Color(0.09f, 0.15f, 0.12f, 0.72f);
                    if (view.FrameArt != null)
                    {
                        view.FrameArt.texture = LoadFrameTexture(ResolveMonsterSlotFrameTexturePath(entry.ClassRank));
                        view.FrameArt.color = Color.white;
                    }
                    view.Portrait.sprite = LoadPortrait(entry.ResourcePath);
                    view.Portrait.color = Color.white;
                    ApplySelectedSlotNameLabel(view.NameLabel, BuildMonsterDisplayName(entry));
                    ApplySelectedSlotStatusBadges(view, entry.DamageType, isActiveSlot ? "配置先" : "外す");
                }
                else
                {
                    SetSelectedSlotStatusBadgesVisible(view, false);
                    view.Background.color = isActiveSlot
                        ? new Color(0.10f, 0.16f, 0.21f, 0.78f)
                        : new Color(0.06f, 0.09f, 0.13f, 0.66f);
                    if (view.FrameArt != null)
                    {
                        view.FrameArt.texture = LoadFrameTexture(ResolveMonsterSlotFrameTexturePath(1));
                        view.FrameArt.color = isActiveSlot
                            ? new Color(1f, 1f, 1f, 0.72f)
                            : new Color(1f, 1f, 1f, 0.48f);
                    }
                    view.Portrait.sprite = null;
                    view.Portrait.color = new Color(1f, 1f, 1f, 0f);
                    ApplySelectedSlotNameLabel(view.NameLabel, isActiveSlot ? "配置先" : "空きスロット");
                    view.StatusLabel.text = isActiveSlot ? "一覧から配置" : "一覧から選択";
                    view.StatusLabel.color = new Color(0.82f, 0.89f, 0.95f, 0.78f);
                }
            }
        }

        private static void ApplySelectedSlotStatusBadges(FormationSlotView view, MonsterDamageType damageType, string actionLabel)
        {
            if (view == null)
            {
                return;
            }

            SetSelectedSlotStatusBadgesVisible(view, true);

            Color damageTextColor = ResolveDamageTypeColor(damageType, 1f);
            ConfigureStatusBadge(
                view.StatusTypeBadge,
                ResolveDamageTypeBadgeBackground(damageType),
                ResolveDamageTypeColor(damageType, 0.76f));
            if (view.StatusTypeText != null)
            {
                view.StatusTypeText.text = ResolveDamageTypeLabel(damageType);
                view.StatusTypeText.color = damageTextColor;
            }

            bool isRemoveAction = actionLabel == "外す";
            ConfigureStatusBadge(
                view.StatusActionBadge,
                isRemoveAction ? new Color(0.30f, 0.10f, 0.09f, 0.94f) : new Color(0.07f, 0.22f, 0.20f, 0.94f),
                isRemoveAction ? new Color(1f, 0.56f, 0.42f, 0.76f) : new Color(0.42f, 1f, 0.82f, 0.70f));
            if (view.StatusActionText != null)
            {
                view.StatusActionText.text = actionLabel;
                view.StatusActionText.color = isRemoveAction
                    ? new Color(1f, 0.82f, 0.70f, 1f)
                    : new Color(0.76f, 1f, 0.92f, 1f);
            }
        }

        private static void SetSelectedSlotStatusBadgesVisible(FormationSlotView view, bool visible)
        {
            if (view == null)
            {
                return;
            }

            if (view.StatusLabel != null)
            {
                view.StatusLabel.gameObject.SetActive(!visible);
            }

            if (view.StatusTypeBadge != null)
            {
                view.StatusTypeBadge.gameObject.SetActive(visible);
            }

            if (view.StatusActionBadge != null)
            {
                view.StatusActionBadge.gameObject.SetActive(visible);
            }
        }

        private static Color ResolveDamageTypeBadgeBackground(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic
                ? new Color(0.06f, 0.18f, 0.28f, 0.94f)
                : new Color(0.30f, 0.20f, 0.08f, 0.94f);
        }

        private static void ApplySelectedSlotNameLabel(Text label, string monsterName)
        {
            if (label == null)
            {
                return;
            }

            string displayName = FormatSelectedSlotMonsterName(monsterName);
            bool usesTwoLines = displayName.Contains("\n");
            label.text = displayName;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = usesTwoLines ? 12 : 13;
            label.resizeTextMaxSize = 20;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(0f, usesTwoLines ? 58f : 62f);
            rect.sizeDelta = new Vector2(180f, usesTwoLines ? 54f : 30f);
        }

        private static string BuildMonsterDisplayName(MonsterEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return entry.PlusValue > 0
                ? $"{entry.Name} +{entry.PlusValue}"
                : entry.Name;
        }

        private static string FormatSelectedSlotMonsterName(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName))
            {
                return string.Empty;
            }

            bool hasKanjiBefore = false;
            for (int i = 0; i < monsterName.Length; i += 1)
            {
                char character = monsterName[i];
                if (IsKanji(character))
                {
                    hasKanjiBefore = true;
                    continue;
                }

                if (hasKanjiBefore && IsKatakana(character))
                {
                    return monsterName.Substring(0, i) + "\n" + monsterName.Substring(i);
                }
            }

            return monsterName;
        }

        private static bool IsKanji(char character)
        {
            return character >= '\u3400' && character <= '\u4dbf'
                || character >= '\u4e00' && character <= '\u9fff'
                || character == '\u3005';
        }

        private static bool IsKatakana(char character)
        {
            return character >= '\u30a0' && character <= '\u30ff'
                || character >= '\uff65' && character <= '\uff9f';
        }

        private void RefreshRosterCards()
        {
            for (int i = 0; i < rosterViews.Count; i++)
            {
                if (rosterViews[i].Root != null)
                {
                    Destroy(rosterViews[i].Root);
                }
            }

            rosterViews.Clear();

            List<MonsterEntry> displayEntries = BuildDisplayEntries();
            emptyStateLabel.gameObject.SetActive(displayEntries.Count == 0);

            const float cardWidth = 218f;
            const float cardHeight = 300f;
            const float spacingX = 16f;
            const float spacingY = 24f;
            const float paddingLeft = 18f;
            const float paddingTop = 24f;

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(displayEntries.Count / (float)GridColumnCount));
            float contentHeight = paddingTop + rowCount * cardHeight + Mathf.Max(0, rowCount - 1) * spacingY + 18f;
            rosterContent.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < displayEntries.Count; i++)
            {
                MonsterEntry entry = displayEntries[i];
                int column = i % GridColumnCount;
                int row = i / GridColumnCount;

                float x = paddingLeft + column * (cardWidth + spacingX);
                float y = -(paddingTop + row * (cardHeight + spacingY));

                MonsterCardView view = CreateMonsterCard(entry, new Vector2(x, y), new Vector2(cardWidth, cardHeight));
                rosterViews.Add(view);
            }
        }

        private Color ResolveMonsterCardBackgroundColor(bool isSelected, bool isBulkSelected, bool isBulkProtected)
        {
            if (bulkReleaseModeActive)
            {
                if (isBulkSelected)
                {
                    return new Color(0.48f, 0.20f, 0.10f, 0.34f);
                }

                return isBulkProtected
                    ? new Color(0.10f, 0.12f, 0.15f, 0.16f)
                    : new Color(0.20f, 0.14f, 0.10f, 0.20f);
            }

            return isSelected
                ? new Color(0.14f, 0.34f, 0.26f, 0.22f)
                : new Color(0.12f, 0.16f, 0.22f, 0.12f);
        }

        private Color ResolveMonsterCardBodyColor(bool isSelected, bool isBulkSelected, bool isBulkProtected)
        {
            if (bulkReleaseModeActive)
            {
                if (isBulkSelected)
                {
                    return new Color(0.20f, 0.08f, 0.05f, 0.98f);
                }

                return isBulkProtected
                    ? new Color(0.045f, 0.055f, 0.065f, 0.88f)
                    : new Color(0.10f, 0.07f, 0.06f, 0.96f);
            }

            return isSelected
                ? new Color(0.09f, 0.17f, 0.14f, 0.98f)
                : new Color(0.06f, 0.09f, 0.13f, 0.98f);
        }

        private string ResolveMonsterCardActionLabel(bool isSelected, bool isBulkSelected, bool canBulkSelect)
        {
            if (!bulkReleaseModeActive)
            {
                return isSelected ? "外す" : "編成";
            }

            if (!canBulkSelect)
            {
                return "保護";
            }

            return isBulkSelected ? "解除" : "選択";
        }

        private Color ResolveMonsterCardActionColor(bool isSelected, bool isBulkSelected, bool canBulkSelect)
        {
            if (!bulkReleaseModeActive)
            {
                return isSelected ? new Color(0.23f, 0.12f, 0.12f, 0.94f) : new Color(0.12f, 0.29f, 0.20f, 0.94f);
            }

            if (!canBulkSelect)
            {
                return new Color(0.12f, 0.13f, 0.15f, 0.76f);
            }

            return isBulkSelected ? new Color(0.52f, 0.16f, 0.10f, 0.96f) : new Color(0.24f, 0.18f, 0.14f, 0.96f);
        }

        private Color ResolveMonsterCardActionOutlineColor(bool isSelected, bool isBulkSelected, bool canBulkSelect)
        {
            if (!bulkReleaseModeActive)
            {
                return isSelected ? new Color(1f, 0.54f, 0.42f, 0.70f) : new Color(0.48f, 1f, 0.72f, 0.64f);
            }

            if (!canBulkSelect)
            {
                return new Color(0.55f, 0.58f, 0.62f, 0.36f);
            }

            return isBulkSelected ? new Color(1f, 0.52f, 0.34f, 0.82f) : new Color(1f, 0.78f, 0.44f, 0.50f);
        }

        private MonsterCardView CreateMonsterCard(MonsterEntry entry, Vector2 anchoredPosition, Vector2 size)
        {
            bool isSelected = IsRosterEntrySelected(entry);
            bool isBulkSelected = IsBulkReleaseEntrySelected(entry);
            bool canBulkSelect = CanBulkReleaseSelect(entry);

            GameObject card = CreatePanel("Card_" + entry.InstanceId, rosterContent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPosition, size, new Color(0f, 0f, 0f, 0f));

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = ResolveMonsterCardBackgroundColor(isSelected, isBulkSelected, bulkReleaseModeActive && !canBulkSelect);
            cardImage.raycastTarget = true;

            Button cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            cardButton.onClick.AddListener(() =>
            {
                if (bulkReleaseModeActive)
                {
                    ToggleBulkReleaseEntry(entry);
                    return;
                }

                ShowMonsterDetail(entry);
            });

            GameObject frameObject = CreateUiObject("FrameArt", card.transform);
            RectTransform frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.sizeDelta = new Vector2(size.x + 16f, size.y + 16f);
            RawImage frameImage = frameObject.AddComponent<RawImage>();
            frameImage.texture = LoadFrameTexture(ResolveMonsterCardFrameTexturePath(entry.ClassRank));
            frameImage.color = isSelected || isBulkSelected
                ? Color.white
                : new Color(1f, 1f, 1f, 0.96f);
            frameImage.raycastTarget = false;
            frameObject.transform.SetAsFirstSibling();

            GameObject body = CreatePanel("Body", card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 34f, size.y - 44f),
                ResolveMonsterCardBodyColor(isSelected, isBulkSelected, bulkReleaseModeActive && !canBulkSelect));
            body.GetComponent<Image>().raycastTarget = false;

            Sprite portraitSprite = LoadPortrait(entry.ResourcePath);
            if (portraitSprite != null && portraitSprite.texture != null)
            {
                portraitSprite.texture.filterMode = UnityEngine.FilterMode.Trilinear;
            }

            GameObject portraitShadow = CreateUiObject("PortraitShadow", body.transform);
            RectTransform portraitShadowRect = portraitShadow.GetComponent<RectTransform>();
            portraitShadowRect.anchorMin = new Vector2(0.5f, 1f);
            portraitShadowRect.anchorMax = new Vector2(0.5f, 1f);
            portraitShadowRect.pivot = new Vector2(0.5f, 1f);
            portraitShadowRect.anchoredPosition = new Vector2(2f, -18f);
            portraitShadowRect.sizeDelta = new Vector2(160f, 160f);
            Image portraitShadowImage = portraitShadow.AddComponent<Image>();
            portraitShadowImage.sprite = portraitSprite;
            portraitShadowImage.preserveAspect = true;
            portraitShadowImage.useSpriteMesh = false;
            portraitShadowImage.color = new Color(0f, 0f, 0f, 0.58f);
            portraitShadowImage.raycastTarget = false;

            GameObject portrait = CreateUiObject("Portrait", body.transform);
            RectTransform portraitRect = portrait.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 1f);
            portraitRect.anchoredPosition = new Vector2(0f, -20f);
            portraitRect.sizeDelta = new Vector2(156f, 156f);
            Image portraitImage = portrait.AddComponent<Image>();
            portraitImage.sprite = portraitSprite;
            portraitImage.preserveAspect = true;
            portraitImage.useSpriteMesh = false;
            portraitImage.color = Color.white;
            portraitImage.raycastTarget = false;

            Text nameLabel = CreateText("NameLabel", body.transform, runtimeFont, BuildMonsterDisplayName(entry), 15, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(174f, 34f), TextAnchor.MiddleCenter,
                new Color(0.96f, 0.98f, 1f, 1f));
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 10;
            nameLabel.resizeTextMaxSize = 15;

            CreateText("DamageTypeLabel", body.transform, runtimeFont, ResolveDamageTypeLabel(entry.DamageType), 13, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 22f), new Vector2(86f, 18f), TextAnchor.MiddleLeft,
                ResolveDamageTypeColor(entry.DamageType, 1f));

            CreateText("LevelLabel", body.transform, runtimeFont, $"Lv.{entry.Level}/{entry.MaxLevel}  IV{entry.IndividualAverage}", 13, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 1f), new Vector2(130f, 18f), TextAnchor.MiddleLeft,
                new Color(0.98f, 0.91f, 0.66f, 1f));

            GameObject favoriteButton = CreateActionButton("FavoriteButton", body.transform, runtimeFont,
                string.Empty,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(CardCornerActionInset, -CardCornerActionInset),
                new Vector2(CardCornerActionButtonSize, CardCornerActionButtonSize),
                new Color(0f, 0f, 0f, 0f), () => ToggleFavorite(entry));

            CreateFavoriteHeartIcon(favoriteButton.transform, entry.IsFavorite);

            CreateMonsterLockButton(body.transform, entry);

            GameObject selectionButton = CreateActionButton("SelectionButton", body.transform, runtimeFont,
                ResolveMonsterCardActionLabel(isSelected, isBulkSelected, canBulkSelect),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-4f, 10f), new Vector2(74f, 28f),
                ResolveMonsterCardActionColor(isSelected, isBulkSelected, canBulkSelect),
                () =>
                {
                    if (bulkReleaseModeActive)
                    {
                        ToggleBulkReleaseEntry(entry);
                        return;
                    }

                    ToggleSelection(entry);
                });
            Button actionButton = selectionButton.GetComponent<Button>();
            if (actionButton != null)
            {
                actionButton.interactable = !bulkReleaseModeActive || canBulkSelect;
            }
            AddUiOutline(
                selectionButton,
                ResolveMonsterCardActionOutlineColor(isSelected, isBulkSelected, canBulkSelect),
                new Vector2(1f, -1f));
            Text selectionText = FindChildText(selectionButton);
            if (selectionText != null)
            {
                selectionText.fontSize = 12;
                selectionText.resizeTextForBestFit = true;
                selectionText.resizeTextMinSize = 10;
                selectionText.resizeTextMaxSize = 12;
            }

            return new MonsterCardView
            {
                Root = card
            };
        }

        private List<MonsterEntry> BuildDisplayEntries()
        {
            List<MonsterEntry> entries = new List<MonsterEntry>();

            for (int i = 0; i < roster.Count; i++)
            {
                MonsterEntry entry = roster[i];
                if (PassesFilter(entry))
                {
                    entries.Add(entry);
                }
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private bool PassesFilter(MonsterEntry entry)
        {
            switch (currentFilterMode)
            {
                case FilterMode.Favorite:
                    return entry.IsFavorite;
                case FilterMode.Selected:
                    return IsRosterEntrySelected(entry);
                case FilterMode.Unselected:
                    return !IsRosterEntrySelected(entry);
                default:
                    return true;
            }
        }

        private int CompareEntries(MonsterEntry left, MonsterEntry right)
        {
            switch (currentSortMode)
            {
                case SortMode.Level:
                    return CompareByLevel(left, right);
                case SortMode.Acquired:
                    return CompareByAcquired(left, right);
                case SortMode.Class:
                    return CompareByClassRank(left, right);
                default:
                    return CompareByFavorite(left, right);
            }
        }

        private static int CompareByLevel(MonsterEntry left, MonsterEntry right)
        {
            int result = right.Level.CompareTo(left.Level);
            if (result != 0)
            {
                return result;
            }

            return CompareByAcquired(left, right);
        }

        private static int CompareByAcquired(MonsterEntry left, MonsterEntry right)
        {
            int result = right.AcquiredOrder.CompareTo(left.AcquiredOrder);
            if (result != 0)
            {
                return result;
            }

            return string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }

        private static int CompareByClassRank(MonsterEntry left, MonsterEntry right)
        {
            int result = right.ClassRank.CompareTo(left.ClassRank);
            if (result != 0)
            {
                return result;
            }

            return CompareByLevel(left, right);
        }

        private static int CompareByFavorite(MonsterEntry left, MonsterEntry right)
        {
            int result = right.IsFavorite.CompareTo(left.IsFavorite);
            if (result != 0)
            {
                return result;
            }

            return CompareByLevel(left, right);
        }

        private void ToggleSelection(MonsterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            EnsureSelectedSlotCapacity();
            int index = selectedMonsters.IndexOf(entry);
            if (index >= 0)
            {
                selectedMonsters[index] = null;
                activeSlotIndex = index;
            }
            else
            {
                int targetSlot = IsValidSlotIndex(activeSlotIndex) ? activeSlotIndex : FindFirstEmptySlot();
                if (!IsValidSlotIndex(targetSlot))
                {
                    targetSlot = 0;
                }

                selectedMonsters[targetSlot] = entry;
                activeSlotIndex = targetSlot;
            }

            SyncProfileSelection();
            RefreshView();
        }

        private void ToggleFavorite(MonsterEntry entry)
        {
            entry.IsFavorite = !entry.IsFavorite;
            SyncProfileFavorite(entry);
            RefreshView();
        }

        private void ToggleMonsterLock(MonsterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var profile = GameManager.Instance?.PlayerProfile;
            if (profile != null)
            {
                entry.IsLocked = profile.ToggleMonsterLock(entry.InstanceId);
                SaveManager.Instance?.SaveCurrentGame();
            }
            else
            {
                entry.IsLocked = !entry.IsLocked;
            }

            RefreshView();
        }

        private void ShowMonsterDetail(MonsterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            OwnedMonsterData ownedMonster = profile?.GetOwnedMonster(entry.InstanceId);
            MonsterDataSO monsterData = ownedMonster != null && masterDataManager != null
                ? masterDataManager.GetMonsterData(ownedMonster.MonsterId)
                : null;
            bool canRelease = CanReleaseMonster(profile, ownedMonster, entry, out string releaseMessage);
            Transform popupParent = ResolvePopupParent();
            MonsterStatusDetailPopup.Show(
                popupParent,
                profile,
                ownedMonster,
                monsterData,
                () => ReleaseMonster(entry.InstanceId),
                canRelease,
                releaseMessage);
        }

        private Transform ResolvePopupParent()
        {
            Canvas canvas = null;
            if (rosterContent != null)
            {
                canvas = rosterContent.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            return canvas != null ? canvas.transform : transform;
        }

        private bool CanReleaseMonster(PlayerProfile profile, OwnedMonsterData ownedMonster, MonsterEntry entry, out string message)
        {
            if (!Application.isPlaying)
            {
                message = "再生中のみ逃がせます。";
                return false;
            }

            if (profile == null || profile.OwnedMonsters == null || ownedMonster == null || entry == null)
            {
                message = "対象モンスターが見つかりません。";
                return false;
            }

            if (ownedMonster.IsLocked || entry.IsLocked)
            {
                message = "ロック中は逃がせません。";
                return false;
            }

            if (ownedMonster.IsFavorite || entry.IsFavorite)
            {
                message = "お気に入り登録中は逃がせません。";
                return false;
            }

            if (IsRosterEntrySelected(entry))
            {
                message = "出撃メンバーから外すと逃がせます。";
                return false;
            }

            if (CountOwnedMonsters(profile) <= 1)
            {
                message = "最後の1体は逃がせません。";
                return false;
            }

            message = "逃がしたモンスターは戻せません。";
            return true;
        }

        private bool ReleaseMonster(string instanceId)
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            OwnedMonsterData ownedMonster = profile?.GetOwnedMonster(instanceId);
            MonsterEntry entry = FindRosterEntry(instanceId);
            if (!CanReleaseMonster(profile, ownedMonster, entry, out string message))
            {
                if (summaryText != null)
                {
                    summaryText.text = message;
                }

                return false;
            }

            string monsterId = ownedMonster.MonsterId;
            ClearReleasedMonsterReferences(profile, instanceId);
            profile.OwnedMonsters.Remove(ownedMonster);
            UpdateDexOwnedCount(profile, monsterId);
            SaveManager.Instance?.SaveCurrentGame();

            SeedRoster();
            RefreshView();
            return true;
        }

        private void ToggleBulkReleaseMode()
        {
            bulkReleaseModeActive = !bulkReleaseModeActive;
            bulkReleaseSelectedInstanceIds.Clear();
            bulkReleaseConfirmArmed = false;
            RefreshView();
        }

        private void CycleBulkReleaseClassFilter()
        {
            currentBulkReleaseClassFilter = (BulkReleaseClassFilter)(((int)currentBulkReleaseClassFilter + 1) % Enum.GetValues(typeof(BulkReleaseClassFilter)).Length);
            bulkReleaseConfirmArmed = false;
            RefreshView();
        }

        private void CycleBulkReleaseIvFilter()
        {
            currentBulkReleaseIvFilter = (BulkReleaseIvFilter)(((int)currentBulkReleaseIvFilter + 1) % Enum.GetValues(typeof(BulkReleaseIvFilter)).Length);
            bulkReleaseConfirmArmed = false;
            RefreshView();
        }

        private void ToggleBulkReleaseEntry(MonsterEntry entry)
        {
            if (!bulkReleaseModeActive || entry == null || string.IsNullOrEmpty(entry.InstanceId))
            {
                return;
            }

            bulkReleaseConfirmArmed = false;
            if (bulkReleaseSelectedInstanceIds.Contains(entry.InstanceId))
            {
                bulkReleaseSelectedInstanceIds.Remove(entry.InstanceId);
                RefreshView();
                return;
            }

            if (!CanBulkReleaseSelect(entry))
            {
                SetBulkReleasePanelMessage("保護対象のため選択できません。");
                return;
            }

            int maxSelectable = ResolveBulkReleaseMaxSelectableCount();
            if (bulkReleaseSelectedInstanceIds.Count >= maxSelectable)
            {
                SetBulkReleasePanelMessage("最後の1体を残すため、これ以上は選択できません。");
                return;
            }

            bulkReleaseSelectedInstanceIds.Add(entry.InstanceId);
            RefreshView();
        }

        private void SelectBulkReleaseFilteredCandidates()
        {
            if (!bulkReleaseModeActive)
            {
                bulkReleaseModeActive = true;
            }

            bulkReleaseConfirmArmed = false;
            bulkReleaseSelectedInstanceIds.Clear();
            int maxSelectable = ResolveBulkReleaseMaxSelectableCount();
            for (int i = 0; i < roster.Count && bulkReleaseSelectedInstanceIds.Count < maxSelectable; i += 1)
            {
                MonsterEntry entry = roster[i];
                if (entry == null || !PassesBulkReleaseFilters(entry) || !CanBulkReleaseSelect(entry))
                {
                    continue;
                }

                bulkReleaseSelectedInstanceIds.Add(entry.InstanceId);
            }

            RefreshView();
        }

        private void ConfirmBulkReleaseSelection()
        {
            if (!bulkReleaseModeActive)
            {
                bulkReleaseModeActive = true;
                RefreshView();
                return;
            }

            PruneBulkReleaseSelection();
            if (bulkReleaseSelectedInstanceIds.Count <= 0)
            {
                SetBulkReleasePanelMessage("逃がすモンスターを選択してください。");
                return;
            }

            if (!bulkReleaseConfirmArmed)
            {
                bulkReleaseConfirmArmed = true;
                UpdateBulkReleaseControls();
                return;
            }

            ExecuteBulkReleaseSelection();
        }

        private void ExecuteBulkReleaseSelection()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (profile?.OwnedMonsters == null)
            {
                SetSummaryMessage("プレイヤーデータがありません。");
                return;
            }

            var releaseIds = new List<string>(bulkReleaseSelectedInstanceIds);
            int releasedCount = 0;
            int skippedCount = 0;
            for (int i = 0; i < releaseIds.Count; i += 1)
            {
                string instanceId = releaseIds[i];
                OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                MonsterEntry entry = FindRosterEntry(instanceId);
                if (!CanReleaseMonster(profile, ownedMonster, entry, out _))
                {
                    skippedCount += 1;
                    continue;
                }

                string monsterId = ownedMonster.MonsterId;
                ClearReleasedMonsterReferences(profile, instanceId);
                profile.OwnedMonsters.Remove(ownedMonster);
                UpdateDexOwnedCount(profile, monsterId);
                releasedCount += 1;
            }

            bulkReleaseSelectedInstanceIds.Clear();
            bulkReleaseConfirmArmed = false;
            if (releasedCount > 0)
            {
                SaveManager.Instance?.SaveCurrentGame();
            }

            SeedRoster();
            RefreshView();
            string skippedText = skippedCount > 0 ? $"（保護などで{skippedCount}体は除外）" : string.Empty;
            SetSummaryMessage($"{releasedCount}体を逃がしました。{skippedText}");
        }

        private void UpdateBulkReleaseControls()
        {
            PruneBulkReleaseSelection();
            if (bulkReleaseToggleLabel != null)
            {
                bulkReleaseToggleLabel.text = bulkReleaseModeActive ? "選択終了" : "一括で逃す";
            }

            if (bulkReleasePanel != null)
            {
                bulkReleasePanel.SetActive(bulkReleaseModeActive);
                if (bulkReleaseModeActive)
                {
                    bulkReleasePanel.transform.SetAsLastSibling();
                }
            }

            if (bulkReleaseClassFilterLabel != null)
            {
                bulkReleaseClassFilterLabel.text = "クラス: " + GetBulkReleaseClassFilterLabel(currentBulkReleaseClassFilter);
            }

            if (bulkReleaseIvFilterLabel != null)
            {
                bulkReleaseIvFilterLabel.text = "IV: " + GetBulkReleaseIvFilterLabel(currentBulkReleaseIvFilter);
            }

            if (bulkReleaseSelectFilteredLabel != null)
            {
                bulkReleaseSelectFilteredLabel.text = "条件で選択";
            }

            int selectedCount = bulkReleaseSelectedInstanceIds.Count;
            if (bulkReleaseExecuteLabel != null)
            {
                bulkReleaseExecuteLabel.text = selectedCount <= 0
                    ? "一括で逃す"
                    : bulkReleaseConfirmArmed ? "もう一度押して実行" : $"{selectedCount}体を逃す";
            }

            if (bulkReleaseStatusLabel != null)
            {
                int candidateCount = CountBulkReleaseFilteredCandidates();
                bulkReleaseStatusLabel.text = bulkReleaseConfirmArmed
                    ? $"最終確認: {selectedCount}体を逃がします。右下の赤いボタンをもう一度押すと実行します。"
                    : $"選択 {selectedCount}体 / 条件候補 {candidateCount}体   条件を選んでまとめて選択できます。";
                bulkReleaseStatusLabel.color = bulkReleaseConfirmArmed
                    ? new Color(1f, 0.62f, 0.38f, 1f)
                    : new Color(1f, 0.9f, 0.66f, 1f);
            }

            Button executeButton = bulkReleasePanel != null
                ? bulkReleasePanel.transform.Find("ExecuteButton")?.GetComponent<Button>()
                : null;
            if (executeButton != null)
            {
                executeButton.interactable = selectedCount > 0;
                Image executeImage = executeButton.GetComponent<Image>();
                if (executeImage != null)
                {
                    executeImage.color = bulkReleaseConfirmArmed
                        ? new Color(0.76f, 0.08f, 0.04f, 1f)
                        : new Color(0.52f, 0.09f, 0.06f, 0.98f);
                }
            }
        }

        private void PruneBulkReleaseSelection()
        {
            if (bulkReleaseSelectedInstanceIds.Count <= 0)
            {
                return;
            }

            var removeIds = new List<string>();
            foreach (string instanceId in bulkReleaseSelectedInstanceIds)
            {
                MonsterEntry entry = FindRosterEntry(instanceId);
                if (entry == null || !CanBulkReleaseSelect(entry))
                {
                    removeIds.Add(instanceId);
                }
            }

            for (int i = 0; i < removeIds.Count; i += 1)
            {
                bulkReleaseSelectedInstanceIds.Remove(removeIds[i]);
            }

            if (removeIds.Count > 0)
            {
                bulkReleaseConfirmArmed = false;
            }
        }

        private bool IsBulkReleaseEntrySelected(MonsterEntry entry)
        {
            return entry != null &&
                !string.IsNullOrEmpty(entry.InstanceId) &&
                bulkReleaseSelectedInstanceIds.Contains(entry.InstanceId);
        }

        private bool CanBulkReleaseSelect(MonsterEntry entry)
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            OwnedMonsterData ownedMonster = profile?.GetOwnedMonster(entry != null ? entry.InstanceId : string.Empty);
            return CanReleaseMonster(profile, ownedMonster, entry, out _);
        }

        private int ResolveBulkReleaseMaxSelectableCount()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            return Mathf.Max(0, CountOwnedMonsters(profile) - 1);
        }

        private int CountBulkReleaseFilteredCandidates()
        {
            int count = 0;
            for (int i = 0; i < roster.Count; i += 1)
            {
                MonsterEntry entry = roster[i];
                if (entry != null && PassesBulkReleaseFilters(entry) && CanBulkReleaseSelect(entry))
                {
                    count += 1;
                }
            }

            return Mathf.Min(count, ResolveBulkReleaseMaxSelectableCount());
        }

        private bool PassesBulkReleaseFilters(MonsterEntry entry)
        {
            return entry != null &&
                PassesBulkReleaseClassFilter(entry) &&
                PassesBulkReleaseIvFilter(entry);
        }

        private bool PassesBulkReleaseClassFilter(MonsterEntry entry)
        {
            switch (currentBulkReleaseClassFilter)
            {
                case BulkReleaseClassFilter.Class1:
                    return entry.ClassRank == 1;
                case BulkReleaseClassFilter.Class2:
                    return entry.ClassRank == 2;
                case BulkReleaseClassFilter.Class3:
                    return entry.ClassRank == 3;
                case BulkReleaseClassFilter.Class4Plus:
                    return entry.ClassRank >= 4;
                default:
                    return true;
            }
        }

        private bool PassesBulkReleaseIvFilter(MonsterEntry entry)
        {
            switch (currentBulkReleaseIvFilter)
            {
                case BulkReleaseIvFilter.Under30:
                    return entry.IndividualAverage <= 30;
                case BulkReleaseIvFilter.Under50:
                    return entry.IndividualAverage <= 50;
                case BulkReleaseIvFilter.Under70:
                    return entry.IndividualAverage <= 70;
                default:
                    return true;
            }
        }

        private static string GetBulkReleaseClassFilterLabel(BulkReleaseClassFilter filter)
        {
            switch (filter)
            {
                case BulkReleaseClassFilter.Class1:
                    return "1";
                case BulkReleaseClassFilter.Class2:
                    return "2";
                case BulkReleaseClassFilter.Class3:
                    return "3";
                case BulkReleaseClassFilter.Class4Plus:
                    return "4+";
                default:
                    return "全て";
            }
        }

        private static string GetBulkReleaseIvFilterLabel(BulkReleaseIvFilter filter)
        {
            switch (filter)
            {
                case BulkReleaseIvFilter.Under30:
                    return "30以下";
                case BulkReleaseIvFilter.Under50:
                    return "50以下";
                case BulkReleaseIvFilter.Under70:
                    return "70以下";
                default:
                    return "全て";
            }
        }

        private void SetSummaryMessage(string message)
        {
            if (summaryText != null)
            {
                summaryText.text = message;
            }
        }

        private void SetBulkReleasePanelMessage(string message)
        {
            if (bulkReleaseModeActive && bulkReleaseStatusLabel != null)
            {
                bulkReleaseStatusLabel.text = message;
                bulkReleaseStatusLabel.color = new Color(1f, 0.68f, 0.44f, 1f);
                return;
            }

            SetSummaryMessage(message);
        }

        private MonsterEntry FindRosterEntry(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            for (int i = 0; i < roster.Count; i += 1)
            {
                MonsterEntry entry = roster[i];
                if (entry != null && string.Equals(entry.InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static int CountOwnedMonsters(PlayerProfile profile)
        {
            if (profile?.OwnedMonsters == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < profile.OwnedMonsters.Count; i += 1)
            {
                if (profile.OwnedMonsters[i] != null)
                {
                    count += 1;
                }
            }

            return count;
        }

        private void ClearReleasedMonsterReferences(PlayerProfile profile, string instanceId)
        {
            if (profile == null || string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            EnsureSelectedSlotCapacity();
            for (int i = 0; i < selectedMonsters.Count; i += 1)
            {
                if (selectedMonsters[i] != null && string.Equals(selectedMonsters[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    selectedMonsters[i] = null;
                }
            }

            if (profile.PartyMonsterInstanceIds != null)
            {
                for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
                {
                    if (string.Equals(profile.PartyMonsterInstanceIds[i], instanceId, StringComparison.Ordinal))
                    {
                        profile.PartyMonsterInstanceIds[i] = string.Empty;
                    }
                }
            }

            if (profile.OwnedEquipments != null)
            {
                for (int i = 0; i < profile.OwnedEquipments.Count; i += 1)
                {
                    OwnedEquipmentData equipment = profile.OwnedEquipments[i];
                    if (equipment != null && string.Equals(equipment.EquippedMonsterInstanceId, instanceId, StringComparison.Ordinal))
                    {
                        equipment.EquippedMonsterInstanceId = string.Empty;
                        equipment.IsEquipped = false;
                    }
                }
            }
        }

        private static void UpdateDexOwnedCount(PlayerProfile profile, string monsterId)
        {
            if (profile == null || profile.MonsterDexEntries == null || string.IsNullOrEmpty(monsterId))
            {
                return;
            }

            int ownedCount = profile.GetOwnedMonsterCount(monsterId);
            MonsterDexEntryData dexEntry = null;
            for (int i = 0; i < profile.MonsterDexEntries.Count; i += 1)
            {
                MonsterDexEntryData candidate = profile.MonsterDexEntries[i];
                if (candidate != null && string.Equals(candidate.MonsterId, monsterId, StringComparison.Ordinal))
                {
                    dexEntry = candidate;
                    break;
                }
            }

            if (dexEntry == null)
            {
                profile.MonsterDexEntries.Add(new MonsterDexEntryData
                {
                    MonsterId = monsterId,
                    IsUnlocked = true,
                    OwnedCount = Mathf.Max(0, ownedCount)
                });
                return;
            }

            dexEntry.IsUnlocked = true;
            dexEntry.OwnedCount = Mathf.Max(0, ownedCount);
        }

        private void OnSlotPressed(int slotIndex)
        {
            EnsureSelectedSlotCapacity();
            if (!IsValidSlotIndex(slotIndex))
            {
                return;
            }

            activeSlotIndex = slotIndex;
            RefreshView();
        }

        private void OnSlotActionPressed(int slotIndex)
        {
            EnsureSelectedSlotCapacity();
            if (!IsValidSlotIndex(slotIndex))
            {
                return;
            }

            if (slotIndex == activeSlotIndex || selectedMonsters[slotIndex] == null)
            {
                OnSlotPressed(slotIndex);
                return;
            }

            selectedMonsters[slotIndex] = null;
            activeSlotIndex = slotIndex;
            SyncProfileSelection();
            RefreshView();
        }

        private int GetStorageLimit()
        {
            return GameManager.Instance?.PlayerProfile?.MonsterStorageLimit ?? DefaultStorageLimit;
        }

        private void SyncProfileSelection()
        {
            var profile = GameManager.Instance?.PlayerProfile;
            if (profile == null)
            {
                return;
            }

            var selectedIds = new List<string>();
            EnsureSelectedSlotCapacity();
            for (int i = 0; i < MaxPartySize; i += 1)
            {
                MonsterEntry monster = selectedMonsters[i];
                selectedIds.Add(monster != null && !string.IsNullOrEmpty(monster.InstanceId)
                    ? monster.InstanceId
                    : string.Empty);
            }

            profile.SetPartyMonsterIds(selectedIds);
            SaveManager.Instance?.SaveCurrentGame();
        }

        private void SyncProfileFavorite(MonsterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var profile = GameManager.Instance?.PlayerProfile;
            OwnedMonsterData ownedMonster = profile?.GetOwnedMonster(entry.InstanceId);
            if (ownedMonster == null)
            {
                return;
            }

            ownedMonster.IsFavorite = entry.IsFavorite;
            SaveManager.Instance?.SaveCurrentGame();
        }

        private void CycleSortMode()
        {
            currentSortMode = (SortMode)(((int)currentSortMode + 1) % Enum.GetValues(typeof(SortMode)).Length);
            RefreshView();
        }

        private void CycleFilterMode()
        {
            currentFilterMode = (FilterMode)(((int)currentFilterMode + 1) % Enum.GetValues(typeof(FilterMode)).Length);
            RefreshView();
        }

        private static string GetSortModeLabel(SortMode mode)
        {
            switch (mode)
            {
                case SortMode.Level:
                    return "レベル順";
                case SortMode.Acquired:
                    return "入手順";
                case SortMode.Class:
                    return "クラス順";
                default:
                    return "お気に入り優先";
            }
        }

        private static string GetFilterModeLabel(FilterMode mode)
        {
            switch (mode)
            {
                case FilterMode.Favorite:
                    return "お気に入り";
                case FilterMode.Selected:
                    return "出撃中";
                case FilterMode.Unselected:
                    return "未編成";
                default:
                    return "全て";
            }
        }

        private Sprite LoadPortrait(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (spriteCache.TryGetValue(resourcePath, out Sprite cached))
            {
                return cached;
            }

            Sprite loaded = Resources.Load<Sprite>(resourcePath);
            if (loaded == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    loaded = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            spriteCache[resourcePath] = loaded;
            return loaded;
        }

        private Texture2D LoadFrameTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (textureCache.TryGetValue(resourcePath, out Texture2D cached))
            {
                return cached;
            }

            Texture2D loaded = Resources.Load<Texture2D>(resourcePath);
            if (loaded != null)
            {
                loaded.filterMode = UnityEngine.FilterMode.Trilinear;
                loaded.wrapMode = TextureWrapMode.Clamp;
            }
            textureCache[resourcePath] = loaded;
            return loaded;
        }

        private static string ResolveMonsterCardFrameTexturePath(int classRank)
        {
            switch (Mathf.Clamp(classRank, 1, 6))
            {
                case 1:
                    return Class1CardFrameTexturePath;
                case 2:
                    return Class2CardFrameTexturePath;
                case 3:
                    return Class3CardFrameTexturePath;
                case 4:
                    return Class4CardFrameTexturePath;
                case 5:
                    return Class5CardFrameTexturePath;
                case 6:
                    return Class6CardFrameTexturePath;
                default:
                    return Class1CardFrameTexturePath;
            }
        }

        private static string ResolveMonsterSlotFrameTexturePath(int classRank)
        {
            switch (Mathf.Clamp(classRank, 1, 6))
            {
                case 1:
                    return Class1SlotFrameTexturePath;
                case 2:
                    return Class2SlotFrameTexturePath;
                case 3:
                    return Class3SlotFrameTexturePath;
                case 4:
                    return Class4SlotFrameTexturePath;
                case 5:
                    return Class5SlotFrameTexturePath;
                case 6:
                    return Class6SlotFrameTexturePath;
                default:
                    return Class1SlotFrameTexturePath;
            }
        }

        private void CreateFavoriteHeartIcon(Transform parent, bool isFavorite)
        {
            GameObject iconObject = CreateUiObject("HeartIcon", parent);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(CardCornerActionIconSize, CardCornerActionIconSize);

            Image image = iconObject.AddComponent<Image>();
            image.sprite = GetFavoriteHeartSprite(isFavorite);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        private void CreateMonsterLockButton(Transform parent, MonsterEntry entry)
        {
            GameObject buttonObject = CreateUiObject("LockButton", parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-CardCornerActionInset, -CardCornerActionInset);
            rect.sizeDelta = new Vector2(CardCornerActionButtonSize, CardCornerActionButtonSize);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => ToggleMonsterLock(entry));

            GameObject iconObject = CreateUiObject("LockIcon", buttonObject.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 2f);
            iconRect.sizeDelta = new Vector2(CardCornerActionIconSize, CardCornerActionIconSize);

            RawImage icon = iconObject.AddComponent<RawImage>();
            bool isLocked = entry != null && entry.IsLocked;
            Texture2D lockTexture = LoadFrameTexture(isLocked ? LockedMonsterIconTexturePath : UnlockedMonsterIconTexturePath);
            if (lockTexture != null)
            {
                lockTexture.filterMode = UnityEngine.FilterMode.Point;
                lockTexture.wrapMode = TextureWrapMode.Clamp;
            }

            icon.texture = lockTexture;
            icon.color = lockTexture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            icon.raycastTarget = false;
        }

        private Sprite GetFavoriteHeartSprite(bool isFavorite)
        {
            if (isFavorite)
            {
                if (favoriteHeartFilledSprite == null)
                {
                    favoriteHeartFilledSprite = LoadFavoriteHeartSprite(FavoriteHeartFilledTexturePath) ?? CreateFavoriteHeartSprite(true);
                }

                return favoriteHeartFilledSprite;
            }

            if (favoriteHeartOutlineSprite == null)
            {
                favoriteHeartOutlineSprite = LoadFavoriteHeartSprite(FavoriteHeartOutlineTexturePath) ?? CreateFavoriteHeartSprite(false);
            }

            return favoriteHeartOutlineSprite;
        }

        private static Sprite LoadFavoriteHeartSprite(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            texture.filterMode = UnityEngine.FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static Sprite CreateFavoriteHeartSprite(bool filled)
        {
            Texture2D texture = new Texture2D(FavoriteHeartPixelSize, FavoriteHeartPixelSize, TextureFormat.RGBA32, false);
            texture.filterMode = UnityEngine.FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(1f, 0.40f, 0.53f, 1f);
            Color innerEdge = new Color(0.86f, 0.18f, 0.33f, 1f);
            Color outerEdge = new Color(0.30f, 0.04f, 0.11f, 0.94f);
            Color outline = new Color(0.86f, 0.90f, 0.96f, 0.94f);

            for (int y = 0; y < FavoriteHeartPixelSize; y += 1)
            {
                for (int x = 0; x < FavoriteHeartPixelSize; x += 1)
                {
                    bool isHeartPixel = IsFavoriteHeartPixel(x, y);
                    bool touchesHeart = isHeartPixel || HasFavoriteHeartNeighbor(x, y);
                    bool isInnerPixel = isHeartPixel && IsFavoriteHeartInteriorPixel(x, y);
                    Color pixel = clear;

                    if (filled)
                    {
                        if (isHeartPixel)
                        {
                            pixel = isInnerPixel ? fill : innerEdge;
                        }
                        else if (touchesHeart)
                        {
                            pixel = outerEdge;
                        }
                    }
                    else if (touchesHeart && !isInnerPixel)
                    {
                        pixel = outline;
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply(false, false);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, FavoriteHeartPixelSize, FavoriteHeartPixelSize),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static bool IsFavoriteHeartPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= FavoriteHeartPixelSize || y >= FavoriteHeartPixelSize)
            {
                return false;
            }

            int topRow = FavoriteHeartPixelSize - 1 - y;
            return IsBetween(x, FavoriteHeartLeftEdges[topRow], FavoriteHeartRightEdges[topRow])
                || IsBetween(x, FavoriteHeartSecondLeftEdges[topRow], FavoriteHeartSecondRightEdges[topRow]);
        }

        private static bool HasFavoriteHeartNeighbor(int x, int y)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY += 1)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX += 1)
                {
                    if (IsFavoriteHeartPixel(x + offsetX, y + offsetY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsFavoriteHeartInteriorPixel(int x, int y)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY += 1)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX += 1)
                {
                    if (!IsFavoriteHeartPixel(x + offsetX, y + offsetY))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsBetween(int value, int min, int max)
        {
            return min >= 0 && value >= min && value <= max;
        }

        private static void CreateBackdrop(Transform parent, Color color)
        {
            GameObject backdrop = CreateUiObject("FormationBackdrop", parent);
            RectTransform rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = backdrop.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
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
            image.color = color;
            return panel;
        }

        private static GameObject CreateActionButton(
            string objectName,
            Transform parent,
            Font font,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMin, anchorMax, pivot, anchoredPosition, size, color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);

            Text label = CreateText("Label", buttonObject.transform, font, text, 20, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 18f, 30f), TextAnchor.MiddleCenter, Color.white);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 20;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return buttonObject;
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

        private static Text CreateText(
            string objectName,
            Transform parent,
            Font font,
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
            textComponent.font = font;
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

        private static void SetRect(Transform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (!(target is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetActionButtonRect(Transform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            SetRect(target, anchorMin, anchorMax, pivot, anchoredPosition, size);
            SetRect(
                target.Find("Label"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(Mathf.Max(24f, size.x - 18f), Mathf.Max(24f, size.y - 20f)));

            Text label = target.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 20;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private static void SetImageColor(Transform target, Color color)
        {
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void SetCenteredVerticalStretchRect(Transform target, float width, float topInset, float bottomInset)
        {
            if (!(target is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-width * 0.5f, bottomInset);
            rect.offsetMax = new Vector2(width * 0.5f, -topInset);
        }

        private static void SetInsetStretchRect(Transform target, float leftInset, float topInset, float rightInset, float bottomInset)
        {
            if (!(target is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(leftInset, bottomInset);
            rect.offsetMax = new Vector2(-rightInset, -topInset);
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

        private static Text FindChildText(GameObject root)
        {
            return root.GetComponentInChildren<Text>(true);
        }

        private static Text FindText(Transform parent, string path)
        {
            Transform target = parent.Find(path);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private static Image FindImage(Transform parent, string path)
        {
            Transform target = parent.Find(path);
            return target != null ? target.GetComponent<Image>() : null;
        }

        private static RawImage FindRawImage(Transform parent, string path)
        {
            Transform target = parent.Find(path);
            return target != null ? target.GetComponent<RawImage>() : null;
        }
    }
}
