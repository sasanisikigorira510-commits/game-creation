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
            public int IndividualAverage;
            public int AcquiredOrder;
            public bool IsFavorite;

            public MonsterEntry(string instanceId, string name, string resourcePath, int level, int maxLevel, int classRank, int individualAverage, int acquiredOrder, bool isFavorite)
            {
                InstanceId = instanceId;
                Name = name;
                ResourcePath = resourcePath;
                Level = level;
                MaxLevel = maxLevel;
                ClassRank = classRank;
                IndividualAverage = individualAverage;
                AcquiredOrder = acquiredOrder;
                IsFavorite = isFavorite;
            }
        }

        private sealed class FormationSlotView
        {
            public Image Background;
            public RawImage FrameArt;
            public Image Portrait;
            public Text NameLabel;
            public Text StatusLabel;
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

        [SerializeField] private string homeSceneName = "HomeScene";

        private const int MaxPartySize = 5;
        private const int DefaultStorageLimit = 100;
        private const int GridColumnCount = 4;

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

        private RectTransform rosterContent;
        private Text summaryText;
        private Text sortModeLabel;
        private Text filterModeLabel;
        private Text emptyStateLabel;
        private Font runtimeFont;
        private bool scaffoldCreated;
        private SortMode currentSortMode = SortMode.Favorite;
        private FilterMode currentFilterMode = FilterMode.All;

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
                    MonsterIndividualValueService.GetAverage(ownedMonster),
                    ownedMonster.AcquiredOrder,
                    ownedMonster.IsFavorite);

                roster.Add(entry);
                entryLookup[entry.InstanceId] = entry;
            }

            roster.Sort(CompareByAcquired);

            foreach (string instanceId in profile.PartyMonsterInstanceIds)
            {
                if (!string.IsNullOrEmpty(instanceId) && entryLookup.TryGetValue(instanceId, out MonsterEntry entry))
                {
                    selectedMonsters.Add(entry);
                }
            }

            if (selectedMonsters.Count == 0)
            {
                for (int i = 0; i < roster.Count && i < MaxPartySize; i++)
                {
                    selectedMonsters.Add(roster[i]);
                }
            }

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
            roster.Add(new MonsterEntry("dragon_whelp_a", "ヒナドラ", "FamilyMonsterCards/Dragon/dragon_whelp", 14, 20, 1, 50, 9, false));
            roster.Add(new MonsterEntry("chibi_gear_a", "チビギア", "FamilyMonsterCards/Robot/chibi_gear", 12, 20, 1, 50, 8, false));
            roster.Add(new MonsterEntry("rock_golem_a", "ロックゴーレム", "FamilyMonsterCards/Golem/rock_golem", 18, 20, 1, 50, 7, false));
            roster.Add(new MonsterEntry("apprentice_swordsman_a", "見習い剣士", "FamilyMonsterCards/Swordsman/apprentice_swordsman", 20, 20, 1, 50, 6, false));
            roster.Add(new MonsterEntry("apprentice_mage_a", "見習い魔導士", "FamilyMonsterCards/Mage/apprentice_mage", 20, 20, 1, 50, 5, false));

            selectedMonsters.Add(roster[0]);
            selectedMonsters.Add(roster[1]);
            selectedMonsters.Add(roster[2]);
            selectedMonsters.Add(roster[3]);
            selectedMonsters.Add(roster[4]);
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

            CreateActionButton("ReturnButton", root.transform, runtimeFont, "ホームへ戻る",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(54f, -160f), new Vector2(220f, 88f),
                new Color(0.54f, 0.29f, 0.14f, 0.94f), ReturnHome);

            GameObject teamPanel = CreatePanel("SelectedPanel", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -404f), new Vector2(1000f, 300f), new Color(0.03f, 0.06f, 0.1f, 0.82f));

            CreateText("SelectedTitle", teamPanel.transform, runtimeFont, "出撃メンバー", 28, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(420f, 36f), TextAnchor.MiddleCenter,
                new Color(0.93f, 0.96f, 0.99f, 1f));

            float slotWidth = 180f;
            float slotHeight = 182f;
            float slotSpacing = 28f;
            float totalWidth = (slotWidth * MaxPartySize) + (slotSpacing * (MaxPartySize - 1));
            float startX = -totalWidth * 0.5f + (slotWidth * 0.5f);

            for (int i = 0; i < MaxPartySize; i++)
            {
                GameObject slotObject = CreatePanel("SelectedSlot" + i, teamPanel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (slotWidth + slotSpacing), -48f), new Vector2(slotWidth, slotHeight),
                    new Color(0.07f, 0.1f, 0.14f, 0.82f));

                FormationSlotView slotView = new FormationSlotView();
                slotView.Background = slotObject.GetComponent<Image>();

                GameObject slotFrameObject = CreateUiObject("FrameArt", slotObject.transform);
                RectTransform slotFrameRect = slotFrameObject.GetComponent<RectTransform>();
                slotFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotFrameRect.pivot = new Vector2(0.5f, 0.5f);
                slotFrameRect.anchoredPosition = Vector2.zero;
                slotFrameRect.sizeDelta = new Vector2(188f, 188f);
                slotView.FrameArt = slotFrameObject.AddComponent<RawImage>();
                slotView.FrameArt.color = new Color(1f, 1f, 1f, 0.94f);
                slotView.FrameArt.raycastTarget = false;
                slotFrameObject.transform.SetAsFirstSibling();

                GameObject portraitObject = CreateUiObject("Portrait", slotObject.transform);
                RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
                portraitRect.anchorMin = new Vector2(0.5f, 1f);
                portraitRect.anchorMax = new Vector2(0.5f, 1f);
                portraitRect.pivot = new Vector2(0.5f, 1f);
                portraitRect.anchoredPosition = new Vector2(0f, -22f);
                portraitRect.sizeDelta = new Vector2(84f, 84f);
                slotView.Portrait = portraitObject.AddComponent<Image>();
                slotView.Portrait.preserveAspect = true;

                slotView.NameLabel = CreateText("NameLabel", slotObject.transform, runtimeFont, string.Empty, 18, FontStyle.Bold,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 42f), new Vector2(154f, 24f), TextAnchor.MiddleCenter,
                    new Color(0.96f, 0.98f, 1f, 1f));

                slotView.StatusLabel = CreateText("StatusLabel", slotObject.transform, runtimeFont, string.Empty, 16, FontStyle.Normal,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 18f), new Vector2(160f, 20f), TextAnchor.MiddleCenter,
                    new Color(0.82f, 0.89f, 0.95f, 0.85f));

                slotView.Button = slotObject.AddComponent<Button>();
                int slotIndex = i;
                slotView.Button.onClick.AddListener(() => OnSlotPressed(slotIndex));

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
                new Vector2(-36f, 0f), new Vector2(280f, 74f),
                new Color(0.15f, 0.25f, 0.31f, 0.96f), CycleSortMode);
            sortModeLabel = FindChildText(sortButton);

            GameObject filterButton = CreateActionButton("FilterButton", controlPanel.transform, runtimeFont, string.Empty,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-334f, 0f), new Vector2(240f, 74f),
                new Color(0.11f, 0.2f, 0.15f, 0.96f), CycleFilterMode);
            filterModeLabel = FindChildText(filterButton);

            GameObject rosterPanel = CreatePanel("RosterPanel", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -870f), new Vector2(1000f, 1650f), new Color(0.03f, 0.06f, 0.1f, 0.82f));

            GameObject viewport = CreatePanel("Viewport", rosterPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -4f), new Vector2(948f, 1560f), new Color(0.02f, 0.04f, 0.07f, 0.38f));
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
            summaryText = FindText(root, "FormationHeader/SummaryText");
            sortModeLabel = FindText(root, "ControlPanel/SortButton/Label");
            filterModeLabel = FindText(root, "ControlPanel/FilterButton/Label");
            emptyStateLabel = FindText(root, "RosterPanel/Viewport/EmptyState");

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
                    Portrait = FindImage(slotTransform, "Portrait"),
                    NameLabel = FindText(slotTransform, "NameLabel"),
                    StatusLabel = FindText(slotTransform, "StatusLabel"),
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
                    slotFrameRect.sizeDelta = new Vector2(188f, 188f);
                    slotView.FrameArt = slotFrameObject.AddComponent<RawImage>();
                    slotView.FrameArt.color = new Color(1f, 1f, 1f, 0.94f);
                    slotView.FrameArt.raycastTarget = false;
                    slotFrameObject.transform.SetAsFirstSibling();
                }
                else
                {
                    slotView.FrameArt.transform.SetAsFirstSibling();
                }

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

            for (int i = 0; i < slotViews.Count; i++)
            {
                int slotIndex = i;
                Button button = slotViews[i].Button;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnSlotPressed(slotIndex));
            }
        }

        private static void ApplyScaffoldLayout(Transform root)
        {
            SetRect(root.Find("FormationHeader"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(980f, 176f));
            SetRect(root.Find("ReturnButton"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -160f), new Vector2(220f, 88f));
            SetRect(root.Find("SelectedPanel"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -404f), new Vector2(1000f, 300f));
            SetRect(root.Find("ControlPanel"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -734f), new Vector2(1000f, 124f));
            SetRect(root.Find("RosterPanel"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -870f), new Vector2(1000f, 1650f));
            SetRect(root.Find("RosterPanel/Viewport"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(948f, 1560f));
            SetRect(root.Find("FormationHeader/SummaryText"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(760f, 32f));
            SetRect(root.Find("SelectedPanel/SelectedTitle"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(420f, 36f));

            const float slotWidth = 180f;
            const float slotHeight = 182f;
            const float slotSpacing = 28f;
            float totalWidth = (slotWidth * MaxPartySize) + (slotSpacing * (MaxPartySize - 1));
            float startX = -totalWidth * 0.5f + (slotWidth * 0.5f);

            for (int i = 0; i < MaxPartySize; i++)
            {
                SetRect(
                    root.Find("SelectedPanel/SelectedSlot" + i),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (slotWidth + slotSpacing), -48f),
                    new Vector2(slotWidth, slotHeight));

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/FrameArt"),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(188f, 188f));

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/Portrait"),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -22f),
                    new Vector2(84f, 84f));
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
                    new Vector2(0f, 42f),
                    new Vector2(154f, 24f));

                SetRect(root.Find("SelectedPanel/SelectedSlot" + i + "/StatusLabel"),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 18f),
                    new Vector2(160f, 20f));
            }
        }

        private void RefreshView()
        {
            if (!scaffoldCreated)
            {
                return;
            }

            if (summaryText != null)
            {
                summaryText.text = $"保有 {roster.Count}/{GetStorageLimit()}   出撃 {selectedMonsters.Count}/{MaxPartySize}";
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
        }

        private void RefreshSelectedSlots()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                FormationSlotView view = slotViews[i];
                MonsterEntry entry = i < selectedMonsters.Count ? selectedMonsters[i] : null;

                if (entry != null)
                {
                    view.Background.color = new Color(0.09f, 0.15f, 0.12f, 0.72f);
                    if (view.FrameArt != null)
                    {
                        view.FrameArt.texture = LoadFrameTexture(ResolveMonsterSlotFrameTexturePath(entry.ClassRank));
                        view.FrameArt.color = Color.white;
                    }
                    view.Portrait.sprite = LoadPortrait(entry.ResourcePath);
                    view.Portrait.color = Color.white;
                    view.NameLabel.text = entry.Name;
                    view.StatusLabel.text = "タップで外す";
                    view.StatusLabel.color = new Color(0.68f, 0.94f, 0.78f, 0.96f);
                }
                else
                {
                    view.Background.color = new Color(0.06f, 0.09f, 0.13f, 0.66f);
                    if (view.FrameArt != null)
                    {
                        view.FrameArt.texture = LoadFrameTexture(ResolveMonsterSlotFrameTexturePath(1));
                        view.FrameArt.color = new Color(1f, 1f, 1f, 0.48f);
                    }
                    view.Portrait.sprite = null;
                    view.Portrait.color = new Color(1f, 1f, 1f, 0f);
                    view.NameLabel.text = "空きスロット";
                    view.StatusLabel.text = "一覧から選択";
                    view.StatusLabel.color = new Color(0.82f, 0.89f, 0.95f, 0.78f);
                }
            }
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

        private MonsterCardView CreateMonsterCard(MonsterEntry entry, Vector2 anchoredPosition, Vector2 size)
        {
            bool isSelected = selectedMonsters.Contains(entry);

            GameObject card = CreatePanel("Card_" + entry.InstanceId, rosterContent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPosition, size, new Color(0f, 0f, 0f, 0f));

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = isSelected
                ? new Color(0.14f, 0.34f, 0.26f, 0.22f)
                : new Color(0.12f, 0.16f, 0.22f, 0.12f);

            Button cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            cardButton.onClick.AddListener(() =>
            {
                ToggleSelection(entry);
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
            frameImage.color = isSelected
                ? Color.white
                : new Color(1f, 1f, 1f, 0.96f);
            frameImage.raycastTarget = false;
            frameObject.transform.SetAsFirstSibling();

            GameObject body = CreatePanel("Body", card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 34f, size.y - 44f),
                isSelected ? new Color(0.09f, 0.17f, 0.14f, 0.98f) : new Color(0.06f, 0.09f, 0.13f, 0.98f));
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

            Text nameLabel = CreateText("NameLabel", body.transform, runtimeFont, entry.Name, 15, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(174f, 34f), TextAnchor.MiddleCenter,
                new Color(0.96f, 0.98f, 1f, 1f));
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 10;
            nameLabel.resizeTextMaxSize = 15;

            CreateText("LevelLabel", body.transform, runtimeFont, $"Lv.{entry.Level}/{entry.MaxLevel}  IV{entry.IndividualAverage}", 13, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 10f), new Vector2(130f, 20f), TextAnchor.MiddleLeft,
                new Color(0.98f, 0.91f, 0.66f, 1f));

            GameObject favoriteButton = CreateActionButton("FavoriteButton", body.transform, runtimeFont,
                entry.IsFavorite ? "♥" : "♡",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -8f), new Vector2(54f, 54f),
                new Color(0f, 0f, 0f, 0f), () => ToggleFavorite(entry));

            Text favoriteText = FindChildText(favoriteButton);
            if (favoriteText != null)
            {
                favoriteText.fontSize = entry.IsFavorite ? 34 : 32;
                favoriteText.color = entry.IsFavorite
                    ? new Color(1f, 0.44f, 0.54f, 1f)
                    : new Color(0.84f, 0.88f, 0.92f, 0.86f);
            }

            GameObject checkBadge = CreatePanel("CheckBadge", body.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f, 10f), new Vector2(74f, 28f),
                isSelected ? new Color(0.12f, 0.38f, 0.22f, 0.98f) : new Color(0f, 0f, 0f, 0.18f));
            checkBadge.GetComponent<Image>().raycastTarget = false;

            CreateText("CheckText", checkBadge.transform, runtimeFont,
                isSelected ? "出撃中" : "未編成", 12, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(62f, 18f), TextAnchor.MiddleCenter,
                isSelected ? Color.white : new Color(0.84f, 0.88f, 0.92f, 0.9f));

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
                    return selectedMonsters.Contains(entry);
                case FilterMode.Unselected:
                    return !selectedMonsters.Contains(entry);
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
            int index = selectedMonsters.IndexOf(entry);
            if (index >= 0)
            {
                selectedMonsters.RemoveAt(index);
            }
            else if (selectedMonsters.Count < MaxPartySize)
            {
                selectedMonsters.Add(entry);
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
            MonsterStatusDetailPopup.Show(transform, profile, ownedMonster, monsterData);
        }

        private void OnSlotPressed(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= selectedMonsters.Count)
            {
                return;
            }

            selectedMonsters.RemoveAt(slotIndex);
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
            foreach (var monster in selectedMonsters)
            {
                if (monster != null && !string.IsNullOrEmpty(monster.InstanceId))
                {
                    selectedIds.Add(monster.InstanceId);
                }
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

            CreateText("Label", buttonObject.transform, font, text, 20, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 18f, 30f), TextAnchor.MiddleCenter, Color.white);

            return buttonObject;
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
