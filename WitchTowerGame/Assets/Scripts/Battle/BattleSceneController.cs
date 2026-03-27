using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Home;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    [ExecuteAlways]
    public sealed class BattleSceneController : MonoBehaviour
    {
        private static readonly Vector2[] AllyPreviewAnchors =
        {
            new Vector2(0.17f, 0.22f),
            new Vector2(0.45f, 0.22f),
            new Vector2(0.31f, 0.31f),
            new Vector2(0.17f, 0.40f),
            new Vector2(0.45f, 0.40f)
        };

        private static readonly Vector2 AllyPreviewSize = new Vector2(72f, 72f);
        private static readonly Vector2[] EnemyPreviewAnchors =
        {
            new Vector2(0.78f, 0.25f),
            new Vector2(0.92f, 0.29f),
            new Vector2(1.06f, 0.33f),
            new Vector2(1.20f, 0.37f),
            new Vector2(1.34f, 0.41f)
        };
        private static readonly Vector2 EnemyPreviewSpawnOffset = new Vector2(0.42f, 0f);
        private static readonly Vector2 EnemyPreviewSize = new Vector2(82f, 82f);
        private static readonly Vector2 BossPreviewSize = new Vector2(96f, 96f);
        private static readonly float[] EnemyPreviewScales = { 1.00f, 0.88f, 0.76f, 0.66f, 0.58f };
        private static readonly float[] EnemyPreviewAlphas = { 1.00f, 0.72f, 0.52f, 0.34f, 0.22f };
        private const float EnemyQueueDriftDistance = 0.18f;

        private static readonly string[] MinimalHiddenObjectNames =
        {
            "ResultPanel",
            "WinLabel",
            "LoseLabel",
            "RewardStrip",
            "RewardStripFrame",
            "RewardStripLabel",
            "NextMoveStrip",
            "NextMoveStripFrame",
            "NextActionText",
            "ReturnHomeButton",
            "ReturnHomeButtonFrame",
            "ReturnHomeAura",
            "ReturnHomeAuraTag",
            "ReturnHomeAuraTagText",
            "ReturnHomeButtonAccentLeft",
            "ReturnHomeButtonAccentRight",
            "NextFloorButton",
            "NextFloorButtonFrame",
            "NextFloorAura",
            "NextFloorAuraTag",
            "NextFloorAuraTagText",
            "NextFloorButtonAccentLeft",
            "NextFloorButtonAccentRight",
            "BattleScreenTitle",
            "BattleScreenSubtitle",
            "BattleTopRibbon",
            "BattleTopRibbonLine",
            "BattleBottomRibbon",
            "BattleBottomRibbonLine",
            "BattleBottomRibbonText",
            "VersusText",
            "VersusSubtext",
            "BattleThreatText",
            "BattleEncounterText",
            "FloorText",
            "BattleFloorBadge",
            "BattleFloorBadgeFrame",
            "BattleFloorBadgeGemLeft",
            "BattleFloorBadgeGemRight",
            "BattleFloorBadgeGemCoreLeft",
            "BattleFloorBadgeGemCoreRight",
            "BattleFloorPillTop",
            "BattleGlowLeft",
            "BattleGlowRight",
            "BattleTreeLeft",
            "BattleTreeRight",
            "BattleTotemLeft",
            "BattleTotemRight",
            "BattleTotemLeftBase",
            "BattleTotemRightBase",
            "BattleTotemLeftUpperWing",
            "BattleTotemLeftUpperCore",
            "BattleTotemLeftMidCore",
            "BattleTotemLeftLowerWing",
            "BattleTotemLeftLowerCore",
            "BattleTotemLeftCrown",
            "BattleTotemRightUpperWing",
            "BattleTotemRightUpperCore",
            "BattleTotemRightMidCore",
            "BattleTotemRightLowerWing",
            "BattleTotemRightLowerCore",
            "BattleTotemRightCrown",
            "ArenaLane",
            "ArenaLaneFrame",
            "ArenaStripe",
            "ArenaPulse",
            "LaneMarkerLeft",
            "LaneMarkerLeftInner",
            "LaneMarkerCenter",
            "LaneMarkerCenterInner",
            "LaneMarkerRight",
            "LaneMarkerRightInner",
            "PlayerFrame",
            "EnemyFrame",
            "PlayerFrameLabel",
            "EnemyFrameLabel",
            "PlayerFrameHint",
            "EnemyFrameHint",
            "PlayerRoleText",
            "EnemyRoleText",
            "PlayerHpText",
            "EnemyHpText",
            "PlayerHpBar",
            "EnemyHpBar",
            "SkillBar",
            "SkillBarFrame",
            "SkillBarTitle",
            "SkillBarHint",
            "SkillTagLeft",
            "SkillTagCenter",
            "SkillTagRight",
            "SkillTagLeftText",
            "SkillTagCenterText",
            "SkillTagRightText",
            "SkillButton1",
            "SkillButton2",
            "SkillButton3",
            "SkillButton1Frame",
            "SkillButton2Frame",
            "SkillButton3Frame",
            "SkillButton1AccentLeft",
            "SkillButton1AccentRight",
            "SkillButton2AccentLeft",
            "SkillButton2AccentRight",
            "SkillButton3AccentLeft",
            "SkillButton3AccentRight",
            "Skill1Text",
            "Skill2Text",
            "Skill3Text",
            "PlayerDamageText",
            "EnemyDamageText",
            "BattleFeedback",
            "PlayerFlash",
            "EnemyFlash"
        };

        private static readonly string[] MinimalTransparentObjectNames =
        {
            "PlayerFrame",
            "EnemyFrame",
            "PlayerFrameBorder",
            "EnemyFrameBorder",
            "PlayerNameplate",
            "EnemyNameplate",
            "PlayerPortraitBaseShadow",
            "EnemyPortraitBaseShadow",
            "PlayerPortraitPixelStage",
            "EnemyPortraitPixelStage",
            "PlayerPortraitPixelStageFrame",
            "EnemyPortraitPixelStageFrame",
            "PlayerRing",
            "EnemyRing"
        };

        [SerializeField] private string homeSceneName = "HomeScene";
        [SerializeField] private BattleStateMachine stateMachine;
        [SerializeField] private bool minimalMonsterPresentation = true;
        [SerializeField] private string[] normalBackdropResourcePaths =
        {
            "BattleBackgrounds/dungeon1_1170x2532",
            "BattleBackgrounds/dungeon2_1170x2532",
            "BattleBackgrounds/dungeon3_1170x2532"
        };
        [SerializeField] private string bossBackdropResourcePath = "BattleBackgrounds/boss3";
        [SerializeField] private int bossFloorInterval = 10;

        private int currentFloor;
        private bool resultHandled;
        private bool initialized;
        private BattleRewardResult lastReward;
        private MonsterRecruitResult lastRecruitResult;
        private int updateCount;
        private float lastDeltaTime;
        private bool recruitEnabledAtBattleStart;
        private Image backdropImage;
        private GameObject minimalCanvasRoot;
        private GameObject monsterPreviewRoot;
        private readonly List<Image> allyPreviewImages = new List<Image>();
        private readonly List<Image> enemyPreviewImages = new List<Image>();
        private TMP_Text playerNameText;
        private TMP_Text enemyNameText;
        private TMP_Text playerRoleText;
        private TMP_Text enemyRoleText;
        private TMP_Text playerHintText;
        private TMP_Text enemyHintText;
        private int lastEncounterSerial = -1;
        private float enemySpawnAnimationT = 1f;
        private float enemyAdvanceCycleT = 0.35f;
        private const float EnemySpawnAnimationDuration = 0.25f;
        private const float EnemyAdvanceCycleDuration = 2.2f;

        public int DebugUpdateCount => updateCount;
        public float DebugLastDeltaTime => lastDeltaTime;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
                return;
            }

            EnsureInitialized();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            updateCount += 1;
            lastDeltaTime = Time.deltaTime;
            UpdateBattlePresentation(Time.deltaTime);

            if (resultHandled)
            {
                return;
            }

            var result = stateMachine.Tick(Time.deltaTime);
            if (result == BattleResult.Win)
            {
                resultHandled = true;
                if (!minimalMonsterPresentation)
                {
                    stateMachine.ShowResult(true);
                }

                OnBattleWin();
            }
            else if (result == BattleResult.Lose)
            {
                resultHandled = true;
                if (!minimalMonsterPresentation)
                {
                    stateMachine.ShowResult(false);
                }

                OnBattleLose();
            }
        }

        private void EnsureInitialized()
        {
            EnsureRuntimeState();

            if (initialized || stateMachine == null || GameManager.Instance == null)
            {
                return;
            }

            currentFloor = GameManager.Instance.CurrentFloor;
            PrepareBattleSession();
            stateMachine.Begin(currentFloor);
            ApplyMinimalPresentation();
            RefreshBattlePresentation(force: true);
            initialized = true;
        }

        private static void EnsureRuntimeState()
        {
            Application.runInBackground = true;
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureAudioManager();

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            MasterDataManager.Instance?.Initialize();

            if (GameManager.Instance != null &&
                GameManager.Instance.PlayerProfile == null &&
                SaveManager.Instance?.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        public void InitializeForSceneLoad()
        {
            EnsureInitialized();
        }

        public void OnBattleWin()
        {
            ApplyRewards();
            ApplyMonsterRecruitment();
            GameManager.Instance.RecordFloorClear(currentFloor);
            var profile = GameManager.Instance.PlayerProfile;
            EquipmentUnlockService.GrantFloorUnlocks(profile, currentFloor);
            MissionService.RecordBattleWin(profile);
            MissionService.RecordHighestFloor(profile, profile.HighestFloor);
            SaveManager.Instance.SaveCurrentGame();
            if (!minimalMonsterPresentation)
            {
                stateMachine.ShowResultPanel(new BattleResultViewData(true, lastReward.Gold, lastReward.Exp, GameManager.Instance.CurrentFloor, lastRecruitResult.Summary));
            }
        }

        public void OnBattleLose()
        {
            SaveManager.Instance.SaveCurrentGame();
            if (!minimalMonsterPresentation)
            {
                stateMachine.ShowResultPanel(new BattleResultViewData(false, 0, 0, currentFloor, string.Empty));
            }
        }

        public void Retreat()
        {
            SaveManager.Instance.SaveCurrentGame();
            SceneManager.LoadScene(homeSceneName);
        }

        public void GoToNextFloor()
        {
            currentFloor = GameManager.Instance.CurrentFloor;
            PrepareBattleSession();
            stateMachine.Begin(currentFloor);
            ApplyMinimalPresentation();
            RefreshBattlePresentation(force: true);
        }

        public void ReturnHome()
        {
            SceneManager.LoadScene(homeSceneName);
        }

        public void UseSkillStrike()
        {
            stateMachine.UseSkill(BattleSkillType.Strike);
        }

        public void UseSkillDrain()
        {
            stateMachine.UseSkill(BattleSkillType.Drain);
        }

        public void UseSkillGuard()
        {
            stateMachine.UseSkill(BattleSkillType.Guard);
        }

        private void ApplyRewards()
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null)
            {
                lastReward = new BattleRewardResult(0, 0);
                return;
            }

            var reward = BattleRewardCalculator.Calculate(currentFloor, profile.HighestFloor);
            profile.AddGold(reward.Gold);
            profile.AddExp(reward.Exp);
            lastReward = reward;
        }

        private void ApplyMonsterRecruitment()
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null)
            {
                lastRecruitResult = MonsterRecruitResult.Empty;
                return;
            }

            lastRecruitResult = MonsterRecruitService.ResolveAfterBattleWin(currentFloor, profile, recruitEnabledAtBattleStart);
        }

        private void PrepareBattleSession()
        {
            resultHandled = false;
            lastReward = new BattleRewardResult(0, 0);
            lastRecruitResult = MonsterRecruitResult.Empty;
            recruitEnabledAtBattleStart = MonsterRecruitService.CanAttemptRecruitThisBattle(GameManager.Instance?.PlayerProfile);
        }

        private void ApplyBackdropForFloor(int floor)
        {
            ApplyBackdropForEncounter(floor, false);
        }

        private void ApplyBackdropForEncounter(int floor, bool isBossEncounter)
        {
            Image targetBackdrop = ResolveBackdropImage();
            if (targetBackdrop == null)
            {
                return;
            }

            string resourcePath = ResolveBackdropResourcePath(floor, isBossEncounter);
            if (string.IsNullOrEmpty(resourcePath))
            {
                return;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    loadedSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            if (loadedSprite == null)
            {
                Debug.LogWarning($"[BattleSceneController] Battle background not found: {resourcePath}");
                return;
            }

            targetBackdrop.sprite = loadedSprite;
            targetBackdrop.color = Color.white;
            targetBackdrop.preserveAspect = false;
        }

        private void ApplyCombatantVisuals(int floor)
        {
            MasterDataManager.Instance?.Initialize();

            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsters(profile, 5);
            MonsterDataSO playerMonsterData = BattleVisualResolver.ResolvePlayerMonsterData(profile);
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            EnemyDataSO enemyData = simulator != null
                ? simulator.CurrentEnemyData
                : MasterDataManager.Instance?.GetFloorData(floor)?.enemyData;

            EnsureMonsterPreviewRoot();
            List<Sprite> partySprites = BattleVisualResolver.ResolvePartySprites(profile, 5);
            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                Sprite partySprite = i < partySprites.Count ? partySprites[i] : null;
                SetImageSprite(allyPreviewImages[i], partySprite);
            }

            int remainingEnemyCount = EnemyPreviewAnchors.Length;
            bool isBossWave = false;
            if (simulator != null)
            {
                remainingEnemyCount = simulator.CurrentEnemyCountTarget - simulator.CurrentEnemyIndexInWave + 1;
                isBossWave = simulator.IsBossWave;
            }

            ApplyEnemyQueueSprites(
                BattleVisualResolver.ResolveEnemySprite(enemyData),
                isBossWave,
                remainingEnemyCount);

            TMP_Text playerLabel = ResolveText(ref playerNameText, "PlayerFrameLabel");
            if (playerLabel != null)
            {
                playerLabel.text = playerMonsterData != null ? playerMonsterData.monsterName : "味方";
            }

            TMP_Text enemyLabel = ResolveText(ref enemyNameText, "EnemyFrameLabel");
            if (enemyLabel != null)
            {
                enemyLabel.text = enemyData != null ? enemyData.enemyName : "Enemy";
            }

            TMP_Text playerRole = ResolveText(ref playerRoleText, "PlayerRoleText");
            if (playerRole != null)
            {
                playerRole.text = BattleVisualResolver.BuildMonsterRoleText(playerMonsterData);
            }

            TMP_Text enemyRole = ResolveText(ref enemyRoleText, "EnemyRoleText");
            if (enemyRole != null)
            {
                enemyRole.text = BattleVisualResolver.BuildEnemyRoleText(enemyData);
            }

            TMP_Text playerHint = ResolveText(ref playerHintText, "PlayerFrameHint");
            if (playerHint != null)
            {
                playerHint.text = partyMonsters.Count > 0
                    ? $"前衛2 / 中央1 / 後衛2 / 出撃{partyMonsters.Count}体"
                    : "編成中モンスターなし";
            }

            TMP_Text enemyHint = ResolveText(ref enemyHintText, "EnemyFrameHint");
            if (enemyHint != null)
            {
                if (simulator != null)
                {
                    enemyHint.text = simulator.IsBossWave
                        ? $"Wave {simulator.CurrentWave} Boss"
                        : $"Wave {simulator.CurrentWave} / Enemy {simulator.CurrentEnemyIndexInWave}/{simulator.CurrentEnemyCountTarget}";
                }
                else
                {
                    bool isBossFloor = bossFloorInterval > 0 && floor > 0 && floor % bossFloorInterval == 0;
                    enemyHint.text = isBossFloor ? $"Boss Floor {floor}" : $"Floor {floor}";
                }
            }
        }

        private void ApplyMinimalPresentation()
        {
            if (!minimalMonsterPresentation)
            {
                return;
            }

            HideLegacyBattleCanvas();

            foreach (string objectName in MinimalHiddenObjectNames)
            {
                GameObject targetObject = GameObject.Find(objectName);
                if (targetObject != null)
                {
                    targetObject.SetActive(false);
                }
            }

            foreach (string objectName in MinimalTransparentObjectNames)
            {
                Image targetImage = ResolveImageByName(objectName);
                if (targetImage != null)
                {
                    targetImage.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            EnsureMonsterPreviewRoot();
            UpdatePreviewLayout();
        }

        private void ApplyEditorPreview()
        {
            ApplyBackdropForFloor(1);
            ApplyMinimalPresentation();
            ApplyEditorCombatantPreview();
        }

        private void ApplyEditorCombatantPreview()
        {
            HideLegacyBattleCanvas();
            EnsureMonsterPreviewRoot();
            ApplyBackdropForEncounter(1, false);
            enemySpawnAnimationT = 1f;
            enemyAdvanceCycleT = 0.35f;
            UpdatePreviewLayout();
            Sprite[] previewPartySprites =
            {
                BattleVisualResolver.LoadSprite("MonsterPortraits/mon_rock_golem_portrait"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Goblin"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Wraith"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Centaur"),
                BattleVisualResolver.LoadSprite("FormationMonsters/HellKnight")
            };

            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                Sprite sprite = i < previewPartySprites.Length ? previewPartySprites[i] : null;
                SetImageSprite(allyPreviewImages[i], sprite);
            }

            ApplyEnemyQueueSprites(
                BattleVisualResolver.LoadSprite("FormationMonsters/HellKnight"),
                false,
                EnemyPreviewAnchors.Length);
        }

        private string ResolveBackdropResourcePath(int floor, bool forceBossEncounter)
        {
            bool isBossFloor = forceBossEncounter || (bossFloorInterval > 0 && floor > 0 && floor % bossFloorInterval == 0);
            if (isBossFloor && !string.IsNullOrEmpty(bossBackdropResourcePath))
            {
                return bossBackdropResourcePath;
            }

            if (normalBackdropResourcePaths == null || normalBackdropResourcePaths.Length == 0)
            {
                return bossBackdropResourcePath;
            }

            int index = Mathf.Abs(floor - 1) % normalBackdropResourcePaths.Length;
            return normalBackdropResourcePaths[index];
        }

        private Image ResolveBackdropImage()
        {
            EnsureMinimalCanvas();

            if (backdropImage != null)
            {
                return backdropImage;
            }

            return backdropImage;
        }

        private static void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                return;
            }

            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
        }

        private static Image ResolveImageByName(string objectName)
        {
            Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Image image in images)
            {
                if (image != null && image.name == objectName)
                {
                    return image;
                }
            }

            return null;
        }

        private static TMP_Text ResolveText(ref TMP_Text cache, string objectName)
        {
            if (cache != null)
            {
                return cache;
            }

            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.name == objectName)
                {
                    cache = text;
                    break;
                }
            }

            return cache;
        }

        private void EnsureMonsterPreviewRoot()
        {
            EnsureMinimalCanvas();

            if (monsterPreviewRoot != null &&
                allyPreviewImages.Count == AllyPreviewAnchors.Length &&
                enemyPreviewImages.Count == EnemyPreviewAnchors.Length)
            {
                return;
            }

            if (minimalCanvasRoot == null)
            {
                return;
            }

            Transform existingRoot = minimalCanvasRoot.transform.Find("BattleMonsterPreviewRoot");
            if (existingRoot != null)
            {
                monsterPreviewRoot = existingRoot.gameObject;
                RemoveLegacyPreview(existingRoot.Find("PlayerMonsterPreview"));
                CollectExistingAllyPreviews(existingRoot);
                CollectExistingEnemyPreviews(existingRoot);
            }

            if (monsterPreviewRoot == null)
            {
                monsterPreviewRoot = new GameObject("BattleMonsterPreviewRoot", typeof(RectTransform));
                RectTransform rootRect = monsterPreviewRoot.GetComponent<RectTransform>();
                rootRect.SetParent(minimalCanvasRoot.transform, false);
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            while (allyPreviewImages.Count < AllyPreviewAnchors.Length)
            {
                int index = allyPreviewImages.Count + 1;
                allyPreviewImages.Add(CreatePreviewImage($"AllyMonsterPreview_{index}", monsterPreviewRoot.transform));
            }

            while (enemyPreviewImages.Count < EnemyPreviewAnchors.Length)
            {
                int index = enemyPreviewImages.Count + 1;
                enemyPreviewImages.Add(CreatePreviewImage($"EnemyMonsterPreview_{index}", monsterPreviewRoot.transform));
            }
        }

        private void UpdatePreviewLayout()
        {
            for (int i = 0; i < allyPreviewImages.Count && i < AllyPreviewAnchors.Length; i += 1)
            {
                ApplyPreviewImageLayout(allyPreviewImages[i], AllyPreviewAnchors[i], AllyPreviewSize);
            }

            bool isBossWave = stateMachine != null && stateMachine.Simulator != null && stateMachine.Simulator.IsBossWave;
            if (isBossWave)
            {
                if (enemyPreviewImages.Count > 0)
                {
                    Vector2 bossAnchor = Vector2.Lerp(
                        EnemyPreviewAnchors[0] + EnemyPreviewSpawnOffset,
                        EnemyPreviewAnchors[0],
                        EaseOutCubic(enemySpawnAnimationT));
                    ApplyPreviewImageLayout(enemyPreviewImages[0], bossAnchor, BossPreviewSize);
                }

                for (int i = 1; i < enemyPreviewImages.Count; i += 1)
                {
                    ApplyPreviewImageLayout(enemyPreviewImages[i], EnemyPreviewAnchors[i], Vector2.zero);
                }

                return;
            }

            for (int i = 0; i < enemyPreviewImages.Count && i < EnemyPreviewAnchors.Length; i += 1)
            {
                float approach = EaseOutCubic(enemyAdvanceCycleT);
                float stagger = 0.010f * i;
                Vector2 advanceOffset = new Vector2(
                    Mathf.Lerp(EnemyQueueDriftDistance + stagger, 0f, approach),
                    Mathf.Sin((enemyAdvanceCycleT * Mathf.PI * 2f) + (i * 0.45f)) * 0.006f);
                Vector2 anchor = Vector2.Lerp(
                    EnemyPreviewAnchors[i] + EnemyPreviewSpawnOffset,
                    EnemyPreviewAnchors[i],
                    EaseOutCubic(enemySpawnAnimationT)) + advanceOffset;
                Vector2 size = EnemyPreviewSize * EnemyPreviewScales[Mathf.Min(i, EnemyPreviewScales.Length - 1)];
                ApplyPreviewImageLayout(enemyPreviewImages[i], anchor, size);
            }
        }

        private static Image CreatePreviewImage(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = go.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            go.transform.SetAsLastSibling();
            return image;
        }

        private static void ApplyPreviewImageLayout(Image image, Vector2 anchor, Vector2 size)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private void EnsureMinimalCanvas()
        {
            if (minimalCanvasRoot == null)
            {
                GameObject existingRoot = GameObject.Find("BattleMinimalCanvas");
                if (existingRoot != null)
                {
                    minimalCanvasRoot = existingRoot;
                }
            }

            if (minimalCanvasRoot == null)
            {
                minimalCanvasRoot = new GameObject(
                    "BattleMinimalCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

                RectTransform canvasRect = minimalCanvasRoot.GetComponent<RectTransform>();
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;

                Canvas canvas = minimalCanvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = minimalCanvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            Transform backdropTransform = minimalCanvasRoot.transform.Find("Backdrop");
            if (backdropTransform == null)
            {
                GameObject backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = backdropObject.GetComponent<RectTransform>();
                rect.SetParent(minimalCanvasRoot.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
                backdropImage = backdropObject.GetComponent<Image>();
                backdropImage.color = Color.white;
            }
            else if (backdropImage == null)
            {
                backdropImage = backdropTransform.GetComponent<Image>();
            }
        }

        private static void HideLegacyBattleCanvas()
        {
            GameObject legacyCanvas = GameObject.Find("BattleCanvas");
            if (legacyCanvas != null)
            {
                legacyCanvas.SetActive(false);
            }
        }

        private void RefreshBattlePresentation(bool force)
        {
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            bool isBossEncounter = simulator != null && simulator.IsBossWave;
            int encounterSerial = simulator != null ? simulator.EncounterSerial : -1;

            if (force || encounterSerial != lastEncounterSerial)
            {
                ApplyBackdropForEncounter(currentFloor, isBossEncounter);
                ApplyCombatantVisuals(currentFloor);
                lastEncounterSerial = encounterSerial;
                enemySpawnAnimationT = Application.isPlaying ? 0f : 1f;
                enemyAdvanceCycleT = 0f;
            }

            UpdatePreviewLayout();
        }

        private void UpdateBattlePresentation(float deltaTime)
        {
            if (!minimalMonsterPresentation)
            {
                return;
            }

            RefreshBattlePresentation(force: false);

            if (enemySpawnAnimationT >= 1f)
            {
                enemyAdvanceCycleT = Mathf.Repeat(enemyAdvanceCycleT + (deltaTime / EnemyAdvanceCycleDuration), 1f);
                UpdatePreviewLayout();
                return;
            }

            enemySpawnAnimationT = Mathf.Clamp01(enemySpawnAnimationT + (deltaTime / EnemySpawnAnimationDuration));
            enemyAdvanceCycleT = Mathf.Repeat(enemyAdvanceCycleT + (deltaTime / EnemyAdvanceCycleDuration), 1f);
            UpdatePreviewLayout();
        }

        private void CollectExistingAllyPreviews(Transform existingRoot)
        {
            allyPreviewImages.Clear();
            for (int i = 1; i <= AllyPreviewAnchors.Length; i += 1)
            {
                Image image = existingRoot.Find($"AllyMonsterPreview_{i}")?.GetComponent<Image>();
                if (image != null)
                {
                    allyPreviewImages.Add(image);
                }
            }
        }

        private void CollectExistingEnemyPreviews(Transform existingRoot)
        {
            enemyPreviewImages.Clear();
            RemoveLegacyPreview(existingRoot.Find("EnemyMonsterPreview"));

            for (int i = 1; i <= EnemyPreviewAnchors.Length; i += 1)
            {
                Image image = existingRoot.Find($"EnemyMonsterPreview_{i}")?.GetComponent<Image>();
                if (image != null)
                {
                    enemyPreviewImages.Add(image);
                }
            }
        }

        private void ApplyEnemyQueueSprites(Sprite enemySprite, bool isBossWave, int remainingEnemyCount)
        {
            EnsureMonsterPreviewRoot();

            int visibleEnemyCount = isBossWave
                ? 1
                : Mathf.Clamp(remainingEnemyCount, 1, EnemyPreviewAnchors.Length);

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                Image image = enemyPreviewImages[i];
                bool shouldShow = i < visibleEnemyCount;
                SetImageSprite(image, shouldShow ? enemySprite : null);

                if (image == null)
                {
                    continue;
                }

                Color color = image.color;
                color.a = shouldShow
                    ? (isBossWave ? 1f : EnemyPreviewAlphas[Mathf.Min(i, EnemyPreviewAlphas.Length - 1)])
                    : 0f;
                image.color = color;
            }
        }

        private static void RemoveLegacyPreview(Transform legacyTransform)
        {
            if (legacyTransform == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(legacyTransform.gameObject);
                return;
            }

            Object.DestroyImmediate(legacyTransform.gameObject);
        }

        private static float EaseOutCubic(float t)
        {
            float clamped = Mathf.Clamp01(t);
            float inverse = 1f - clamped;
            return 1f - (inverse * inverse * inverse);
        }
    }
}
