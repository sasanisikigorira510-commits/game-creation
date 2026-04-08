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
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace WitchTower.Battle
{
    [ExecuteAlways]
    public sealed class BattleSceneController : MonoBehaviour
    {
        private static readonly Vector2[] AllyPreviewAnchors =
        {
            new Vector2(0.07f, 0.31f),
            new Vector2(0.25f, 0.31f),
            new Vector2(0.16f, 0.40f),
            new Vector2(0.07f, 0.49f),
            new Vector2(0.25f, 0.49f)
        };

        private static readonly Vector2[] AllyApproachAnchors =
        {
            new Vector2(0.35f, 0.31f),
            new Vector2(0.53f, 0.31f),
            new Vector2(0.44f, 0.40f),
            new Vector2(0.35f, 0.49f),
            new Vector2(0.53f, 0.49f)
        };

        private static readonly string[] DevPartyOverrideIdlePaths =
        {
            "MonsterBattle/mon_dragoon_idle",
            "MonsterBattle/mon_dragoon_royal_idle",
            "MonsterBattle/mon_dragoon_inferno_idle"
        };

        private static readonly string[] DevPartyOverrideMovePaths =
        {
            "MonsterBattle/mon_dragoon_move",
            "MonsterBattle/mon_dragoon_royal_move",
            "MonsterBattle/mon_dragoon_inferno_move"
        };

        private static readonly string[] DevPartyOverrideAttackPaths =
        {
            "MonsterBattle/mon_dragoon_attack",
            "MonsterBattle/mon_dragoon_royal_attack",
            "MonsterBattle/mon_dragoon_inferno_attack"
        };

        private static readonly Vector2 AllyPreviewSize = new Vector2(74f, 74f);
        private const int InitialEnemyPreviewSlotCapacity = 100;
        private static readonly Vector2 BossPreviewAnchor = new Vector2(0.78f, 0.39f);
        private static readonly Vector2 EnemyPreviewSpawnOffset = new Vector2(0f, 0.01f);
        private static readonly Vector2 EnemyPreviewSize = new Vector2(62f, 62f);
        private static readonly Vector2 BossPreviewSize = new Vector2(100f, 100f);
        private const float AttackPreviewScaleMultiplier = 1.18f;
        private const float BattlefieldMinX = 0.06f;
        private const float BattlefieldMaxX = 0.95f;
        private const float BattlefieldMinY = 0.34f;
        private const float BattlefieldMaxY = 0.94f;
        private const float SkillPanelHeightRatio = 0.34f;

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
        private GameObject skillPanelRoot;
        private GameObject waveHudRoot;
        private readonly List<Image> allyPreviewImages = new List<Image>();
        private readonly List<Image> enemyPreviewImages = new List<Image>();
        private readonly List<List<Sprite>> allyIdleSprites = new List<List<Sprite>>();
        private readonly List<List<Sprite>> allyMoveSprites = new List<List<Sprite>>();
        private readonly List<List<Sprite>> allyAttackSprites = new List<List<Sprite>>();
        private readonly List<MonsterDataSO> allyPreviewMonsterData = new List<MonsterDataSO>();
        private readonly List<float> allyAttackRanges = new List<float>();
        private readonly List<float> allySearchRanges = new List<float>();
        private List<Sprite> enemyIdleSprites = new List<Sprite>();
        private List<Sprite> enemyMoveSprites = new List<Sprite>();
        private List<Sprite> enemyAttackSprites = new List<Sprite>();
        private EnemyDataSO currentPreviewEnemyData;
        private Image waveEnemyCountFillImage;
        private Text waveEnemyCountText;
        private Text waveTitleText;
        private Text battleStatusText;
        private TMP_Text playerNameText;
        private TMP_Text enemyNameText;
        private TMP_Text playerRoleText;
        private TMP_Text enemyRoleText;
        private TMP_Text playerHintText;
        private TMP_Text enemyHintText;
        private int lastEncounterSerial = -1;
        private int lastPresentedWave = -1;
        private BattleSimulator subscribedSimulator;
        private int targetEnemyPreviewCount;
        private int visibleEnemyPreviewCount;
        private int displayedEnemyPreviewCount;
        private int observedSpawnedEnemyCount;
        private readonly List<int> pendingEnemyPreviewRemovalIndices = new List<int>();
        private float enemyPreviewPressure;
        private readonly List<float> enemyPreviewSlotProgress = new List<float>();
        private readonly List<float> enemyPreviewBaseYAnchors = new List<float>();
        private readonly List<float> enemyPreviewVerticalOffsets = new List<float>();
        private readonly List<float> enemyPreviewContactJitters = new List<float>();
        private readonly List<float> enemyPreviewSearchJitters = new List<float>();
        private readonly List<float> enemyPreviewSpawnXJitters = new List<float>();
        private float engagementProgress = 1f;
        private float combatLoopProgress;
        private float enemyAttackRange = 1f;
        private float enemySearchRange = 1.8f;
        private float combatSearchProgress = 0.55f;
        private float combatStartProgress = 1f;
        private int displayedRemainingEnemyCount = -1;
        private int pendingRemainingEnemyCount = -1;
        private float enemyCountCommitDelayRemaining;
        private readonly List<float> allyKnockbackRemainings = new List<float>();
        private readonly List<float> enemyKnockbackRemainings = new List<float>();
        private readonly List<float> allyAttackVisualRemainings = new List<float>();
        private readonly List<float> enemyAttackVisualRemainings = new List<float>();
        private readonly List<float> allyDefeatVanishRemainings = new List<float>();
        private readonly List<float> enemyDefeatVanishRemainings = new List<float>();
        private bool lastBattleWon;
        private const float EngagementDuration = 2.70f;
        private const float CombatLoopDuration = 1.8f;
        private const float EnemyCountCommitDelay = 0.12f;
        private const float KnockbackDuration = 0.16f;
        private const float AttackVisualDuration = 0.36f;
        private const float AllyKnockbackDistance = 0.016f;
        private const float EnemyKnockbackDistance = 0.028f;
        private const float AllyDefeatVanishDuration = 0.28f;
        private const float EnemyDefeatVanishDuration = 0.18f;

        public int DebugUpdateCount => updateCount;
        public float DebugLastDeltaTime => lastDeltaTime;

        private static Font ResolveBuiltinUiFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                // Ignore and try fallback below.
            }

            if (font != null)
            {
                return font;
            }

            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                // Ignore. Unity 6 no longer guarantees Arial.ttf.
            }

            return font;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SyncSimulatorSubscription();
            }

            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
            }
        }

        private void OnDisable()
        {
            UnsubscribeSimulator();
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

            if (!IsCombatEngaged())
            {
                stateMachine.TickPreparation(Time.deltaTime);
                UpdateBattlePresentation(Time.deltaTime);
                return;
            }

            UpdateBattlePresentation(Time.deltaTime);

            if (resultHandled)
            {
                return;
            }

            var result = stateMachine.Tick(Time.deltaTime);
            if (result == BattleResult.Win)
            {
                resultHandled = true;
                lastBattleWon = true;
                if (!minimalMonsterPresentation)
                {
                    stateMachine.ShowResult(true);
                }

                OnBattleWin();
            }
            else if (result == BattleResult.Lose)
            {
                resultHandled = true;
                lastBattleWon = false;
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
            SyncSimulatorSubscription();
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
            lastBattleWon = false;
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
            allyIdleSprites.Clear();
            allyMoveSprites.Clear();
            allyAttackSprites.Clear();
            allyPreviewMonsterData.Clear();
            allyAttackRanges.Clear();
            allySearchRanges.Clear();
            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                MonsterDataSO partyData = null;
                if (i < partyMonsters.Count)
                {
                    partyData = MasterDataManager.Instance?.GetMonsterData(partyMonsters[i].MonsterId);
                }

                allyIdleSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideIdlePaths, partyData, BattleVisualResolver.ResolveMonsterIdleSprites));
                allyMoveSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideMovePaths, partyData, BattleVisualResolver.ResolveMonsterMoveSprites));
                allyAttackSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideAttackPaths, partyData, BattleVisualResolver.ResolveMonsterAttackSprites));
                allyPreviewMonsterData.Add(partyData);

                allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(partyData));
                allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(partyData));
            }

            while (allyIdleSprites.Count < allyPreviewImages.Count)
            {
                allyIdleSprites.Add(new List<Sprite>());
                allyMoveSprites.Add(new List<Sprite>());
                allyAttackSprites.Add(new List<Sprite>());
                allyPreviewMonsterData.Add(null);
            }

            enemyAttackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(enemyData);
            enemySearchRange = BattleAttackRangeResolver.ResolveEnemySearchRange(enemyData);
            combatStartProgress = BattleAttackRangeResolver.ResolveCombatStartProgress(allyAttackRanges, enemyAttackRange);
            combatSearchProgress = Mathf.Min(
                Mathf.Max(0.12f, combatStartProgress - 0.12f),
                BattleAttackRangeResolver.ResolveCombatSearchProgress(allySearchRanges, enemySearchRange));
            enemyIdleSprites = BattleVisualResolver.ResolveEnemyIdleSprites(enemyData);
            enemyMoveSprites = BattleVisualResolver.ResolveEnemyMoveSprites(enemyData);
            enemyAttackSprites = BattleVisualResolver.ResolveEnemyAttackSprites(enemyData);
            currentPreviewEnemyData = enemyData;

            int previewTargetCount = InitialEnemyPreviewSlotCapacity;
            bool isBossWave = false;
            if (simulator != null)
            {
                if (simulator.IsBossWave)
                {
                    previewTargetCount = Mathf.Min(1, simulator.CurrentRemainingEnemyCount);
                }
                else
                {
                    previewTargetCount = Mathf.Max(
                        displayedEnemyPreviewCount,
                        simulator.CurrentActiveEnemyCount);
                }
                isBossWave = simulator.IsBossWave;
            }

            ApplyEnemyQueueSprites(
                enemyIdleSprites.Count > 0 ? enemyIdleSprites[0] : null,
                isBossWave,
                previewTargetCount);

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
                    ? $"単一フィールド / 出撃{partyMonsters.Count}体"
                    : "編成中モンスターなし";
            }

            TMP_Text enemyHint = ResolveText(ref enemyHintText, "EnemyFrameHint");
            if (enemyHint != null)
            {
                if (simulator != null)
                {
                    enemyHint.text = simulator.IsBossWave
                        ? $"Wave {simulator.CurrentWave} Boss"
                        : $"Wave {simulator.CurrentWave} / 残敵 {simulator.CurrentRemainingEnemyCount}";
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
            engagementProgress = 0f;
            ResetEnemyPreviewProgress();
            UpdatePreviewLayout();
            Sprite[] previewPartySprites =
            {
                BattleVisualResolver.LoadSprite("MonsterPortraits/mon_rock_golem_portrait"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Goblin"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Wraith"),
                BattleVisualResolver.LoadSprite("FormationMonsters/Centaur"),
                BattleVisualResolver.LoadSprite("FormationMonsters/HellKnight")
            };

            allyIdleSprites.Clear();
            allyMoveSprites.Clear();
            allyAttackSprites.Clear();
            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                Sprite sprite = i < previewPartySprites.Length ? previewPartySprites[i] : null;
                allyIdleSprites.Add(sprite != null ? new List<Sprite> { sprite } : new List<Sprite>());
                allyMoveSprites.Add(sprite != null ? new List<Sprite> { sprite } : new List<Sprite>());
                allyAttackSprites.Add(sprite != null ? new List<Sprite> { sprite } : new List<Sprite>());
                SetImageSprite(allyPreviewImages[i], sprite);
            }

            Sprite previewEnemySprite = BattleVisualResolver.LoadSprite("FormationMonsters/HellKnight");
            enemyIdleSprites = previewEnemySprite != null ? new List<Sprite> { previewEnemySprite } : new List<Sprite>();
            enemyMoveSprites = previewEnemySprite != null ? new List<Sprite> { previewEnemySprite } : new List<Sprite>();
            enemyAttackSprites = previewEnemySprite != null ? new List<Sprite> { previewEnemySprite } : new List<Sprite>();
            ApplyEnemyQueueSprites(
                previewEnemySprite,
                false,
                0);
            allyAttackRanges.Clear();
            allySearchRanges.Clear();
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_rock_golem")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_goblin")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_wraith")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_centaur")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_hell_knight")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_rock_golem")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_goblin")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_wraith")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_centaur")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_hell_knight")));
            enemyAttackRange = BattleAttackRangeResolver.ResolveEnemyAttackRange(MasterDataManager.Instance?.GetFloorData(1)?.enemyData);
            enemySearchRange = BattleAttackRangeResolver.ResolveEnemySearchRange(MasterDataManager.Instance?.GetFloorData(1)?.enemyData);
            combatStartProgress = BattleAttackRangeResolver.ResolveCombatStartProgress(allyAttackRanges, enemyAttackRange);
            combatSearchProgress = Mathf.Min(
                Mathf.Max(0.12f, combatStartProgress - 0.12f),
                BattleAttackRangeResolver.ResolveCombatSearchProgress(allySearchRanges, enemySearchRange));
            UpdateWaveHud(null);
            UpdatePreviewLayout();
        }

        private void ResetEnemyPreviewProgress()
        {
            targetEnemyPreviewCount = 0;
            visibleEnemyPreviewCount = 0;
            displayedEnemyPreviewCount = 0;
            observedSpawnedEnemyCount = 0;
            pendingEnemyPreviewRemovalIndices.Clear();
            enemyPreviewPressure = 0f;
            enemyPreviewSlotProgress.Clear();
            enemyPreviewBaseYAnchors.Clear();
            enemyPreviewVerticalOffsets.Clear();
            enemyPreviewContactJitters.Clear();
            enemyPreviewSearchJitters.Clear();
            enemyPreviewSpawnXJitters.Clear();
            enemyKnockbackRemainings.Clear();
            enemyAttackVisualRemainings.Clear();
            enemyDefeatVanishRemainings.Clear();
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

        private static List<Sprite> ResolvePartyOverrideFrames(
            int slotIndex,
            IReadOnlyList<string> overridePaths,
            MonsterDataSO fallbackMonsterData,
            System.Func<MonsterDataSO, List<Sprite>> fallbackResolver)
        {
            if (slotIndex >= 0 && slotIndex < overridePaths.Count)
            {
                string overridePath = overridePaths[slotIndex];
                if (!string.IsNullOrEmpty(overridePath))
                {
                    List<Sprite> overrideFrames = BattleVisualResolver.ResolveSpriteFramesFromResourcePath(overridePath);
                    if (overrideFrames != null && overrideFrames.Count > 0)
                    {
                        return overrideFrames;
                    }
                }
            }

            return fallbackResolver != null
                ? fallbackResolver(fallbackMonsterData)
                : new List<Sprite>();
        }

        private Sprite SelectAllyPreviewSprite(int index, float allyApproachT)
        {
            if (index >= 0 && index < allyAttackVisualRemainings.Count && allyAttackVisualRemainings[index] > 0f)
            {
                List<Sprite> attackSprites = index < allyAttackSprites.Count ? allyAttackSprites[index] : null;
                Sprite attackSprite = SelectAttackFrame(attackSprites, allyAttackVisualRemainings[index], index * 0.17f);
                if (attackSprite != null)
                {
                    return attackSprite;
                }
            }

            if (allyApproachT < 1f)
            {
                List<Sprite> moveSprites = index < allyMoveSprites.Count ? allyMoveSprites[index] : null;
                Sprite moveSprite = SelectAnimatedFrame(moveSprites, 8f, index * 0.21f);
                if (moveSprite != null)
                {
                    return moveSprite;
                }
            }

            List<Sprite> idleSprites = index < allyIdleSprites.Count ? allyIdleSprites[index] : null;
            return SelectAnimatedFrame(idleSprites, 4f, index * 0.13f);
        }

        private BattleVisualPose ResolveAllyPreviewPose(int index, float allyApproachT)
        {
            if (index >= 0 && index < allyAttackVisualRemainings.Count && allyAttackVisualRemainings[index] > 0f)
            {
                return BattleVisualPose.Attack;
            }

            if (allyApproachT < 1f)
            {
                return BattleVisualPose.Move;
            }

            return BattleVisualPose.Idle;
        }

        private bool IsAllyAttackVisualActive(int index)
        {
            return index >= 0 &&
                   index < allyAttackVisualRemainings.Count &&
                   allyAttackVisualRemainings[index] > 0f;
        }

        private Sprite SelectEnemyPreviewSprite(int index, bool isMoving)
        {
            if (index >= 0 && index < enemyAttackVisualRemainings.Count && enemyAttackVisualRemainings[index] > 0f)
            {
                Sprite attackSprite = SelectAttackFrame(enemyAttackSprites, enemyAttackVisualRemainings[index], index * 0.11f);
                if (attackSprite != null)
                {
                    return attackSprite;
                }
            }

            if (isMoving)
            {
                Sprite moveSprite = SelectAnimatedFrame(enemyMoveSprites, 8f, index * 0.19f);
                if (moveSprite != null)
                {
                    return moveSprite;
                }
            }

            return SelectAnimatedFrame(enemyIdleSprites, 4f, index * 0.09f);
        }

        private BattleVisualPose ResolveEnemyPreviewPose(int index, bool isMoving)
        {
            if (index >= 0 && index < enemyAttackVisualRemainings.Count && enemyAttackVisualRemainings[index] > 0f)
            {
                return BattleVisualPose.Attack;
            }

            if (isMoving)
            {
                return BattleVisualPose.Move;
            }

            return BattleVisualPose.Idle;
        }

        private static Vector3 ResolveFacingScale(BattleFacingDirection sourceFacing, BattleFacingDirection desiredFacing)
        {
            return sourceFacing == desiredFacing
                ? Vector3.one
                : new Vector3(-1f, 1f, 1f);
        }

        private bool IsEnemyAttackVisualActive(int index)
        {
            return index >= 0 &&
                   index < enemyAttackVisualRemainings.Count &&
                   enemyAttackVisualRemainings[index] > 0f;
        }

        private Sprite SelectAttackFrame(IReadOnlyList<Sprite> frames, float remaining, float phaseOffset)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1 || !Application.isPlaying)
            {
                return frames[0];
            }

            float normalized = Mathf.Clamp01(1f - (remaining / AttackVisualDuration));
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * frames.Count), 0, frames.Count - 1);
            return frames[frameIndex];
        }

        private Sprite SelectAnimatedFrame(IReadOnlyList<Sprite> frames, float fps, float phaseOffset)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1 || !Application.isPlaying)
            {
                return frames[0];
            }

            float time = Time.realtimeSinceStartup * fps + phaseOffset;
            int frameIndex = Mathf.Abs(Mathf.FloorToInt(time)) % frames.Count;
            return frames[frameIndex];
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
                allyPreviewImages.Count == AllyPreviewAnchors.Length)
            {
                EnsureAllyPreviewEffectCapacity();
                EnsureEnemyPreviewCapacity(InitialEnemyPreviewSlotCapacity);
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
                RegisterSceneObjectIfEditing(monsterPreviewRoot);
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

            EnsureAllyPreviewEffectCapacity();
            EnsureEnemyPreviewCapacity(InitialEnemyPreviewSlotCapacity);
        }

        private void UpdatePreviewLayout()
        {
            EnsureAllyPreviewEffectCapacity();
            EnsureEnemyPreviewCapacity(enemyPreviewImages.Count);

            float searchT = combatSearchProgress > 0f
                ? Mathf.Clamp01(engagementProgress / combatSearchProgress)
                : 1f;
            float contactT = combatStartProgress > combatSearchProgress
                ? Mathf.Clamp01((engagementProgress - combatSearchProgress) / (combatStartProgress - combatSearchProgress))
                : 1f;
            float allyApproachT = combatStartProgress > 0f
                ? Mathf.Clamp01(engagementProgress / combatStartProgress)
                : 1f;
            int engagedEnemyPreviewCount = 0;
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;

            for (int i = 0; i < allyPreviewImages.Count && i < AllyPreviewAnchors.Length; i += 1)
            {
                float allyKnockbackRemaining = i < allyKnockbackRemainings.Count ? allyKnockbackRemainings[i] : 0f;
                float allyKnockbackT = KnockbackDuration > 0f
                    ? Mathf.Clamp01(allyKnockbackRemaining / KnockbackDuration)
                    : 0f;
                float allyDefeatRemaining = i < allyDefeatVanishRemainings.Count ? allyDefeatVanishRemainings[i] : 0f;
                float allyVanishT = AllyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(allyDefeatRemaining / AllyDefeatVanishDuration)
                    : 0f;
                float allyKnockbackOffset = AllyKnockbackDistance * EaseOutCubic(allyKnockbackT);
                bool allyAlive = simulator == null || !simulator.HasAllyRuntime(i) || simulator.IsAllyAlive(i);
                float allyRange = i < allyAttackRanges.Count ? allyAttackRanges[i] : 1f;
                float allyHoldOffset = BattleAttackRangeResolver.ToAllyHoldOffset(allyRange);
                Vector2 allyStartAnchor = AllyPreviewAnchors[i];
                Vector2 allyTargetAnchor = AllyApproachAnchors[i] + new Vector2(-allyHoldOffset, 0f);
                Vector2 allyAnchor = Vector2.Lerp(allyStartAnchor, allyTargetAnchor, allyApproachT);
                allyAnchor += new Vector2(-allyKnockbackOffset, 0f);
                float allyScale = allyAlive
                    ? 1f
                    : Mathf.Lerp(1f, 0.24f, allyVanishT);
                float allyAttackScale = IsAllyAttackVisualActive(i) ? AttackPreviewScaleMultiplier : 1f;
                ApplyPreviewImageLayout(
                    allyPreviewImages[i],
                    MapBattlefieldAnchor(allyAnchor),
                    AllyPreviewSize * (allyScale * allyAttackScale));
                if (allyPreviewImages[i] != null)
                {
                    BattleVisualPose allyPose = ResolveAllyPreviewPose(i, allyApproachT);
                    Sprite allySprite = SelectAllyPreviewSprite(i, allyApproachT);
                    SetImageSprite(allyPreviewImages[i], allySprite);
                    MonsterDataSO allyData = i < allyPreviewMonsterData.Count ? allyPreviewMonsterData[i] : null;
                    BattleFacingDirection allySourceFacing = BattleVisualResolver.ResolveMonsterFacing(allyData, allyPose);
                    allyPreviewImages[i].rectTransform.localScale = ResolveFacingScale(allySourceFacing, BattleFacingDirection.Right);
                    Color allyColor = allyPreviewImages[i].color;
                    allyColor.a = allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT);
                    allyPreviewImages[i].color = allyColor;
                }
            }

            bool isBossWave = stateMachine != null && stateMachine.Simulator != null && stateMachine.Simulator.IsBossWave;
            if (isBossWave)
            {
                if (enemyPreviewImages.Count > 0)
                {
                    float enemyHoldOffset = BattleAttackRangeResolver.ToEnemyHoldOffset(enemyAttackRange);
                    float enemySearchOffset = BattleAttackRangeResolver.ToEnemySearchOffset(enemySearchRange);
                    Vector2 bossSpawnAnchor = ResolveBossSpawnAnchor();
                    Vector2 bossSearchAnchor = BossPreviewAnchor + new Vector2(enemySearchOffset, 0f);
                    Vector2 bossAnchor = Vector2.Lerp(
                        bossSpawnAnchor,
                        bossSearchAnchor,
                        searchT);
                    bossAnchor = Vector2.Lerp(
                        bossAnchor,
                        BossPreviewAnchor + new Vector2(enemyHoldOffset, 0f),
                        contactT);
                    float enemyKnockbackRemaining = enemyKnockbackRemainings.Count > 0 ? enemyKnockbackRemainings[0] : 0f;
                    float enemyKnockbackT = KnockbackDuration > 0f
                        ? Mathf.Clamp01(enemyKnockbackRemaining / KnockbackDuration)
                        : 0f;
                    float enemyVanishRemaining = enemyDefeatVanishRemainings.Count > 0 ? enemyDefeatVanishRemainings[0] : 0f;
                    float enemyVanishT = EnemyDefeatVanishDuration > 0f
                        ? 1f - Mathf.Clamp01(enemyVanishRemaining / EnemyDefeatVanishDuration)
                        : 0f;
                    float enemyKnockbackOffset = EnemyKnockbackDistance * EaseOutCubic(enemyKnockbackT);
                    bossAnchor += new Vector2(enemyKnockbackOffset, 0f);
                    float bossScale = Mathf.Lerp(1f, 0.24f, enemyVanishT);
                    float bossAttackScale = IsEnemyAttackVisualActive(0) ? AttackPreviewScaleMultiplier : 1f;
                    ApplyPreviewImageLayout(
                        enemyPreviewImages[0],
                        MapBattlefieldAnchor(bossAnchor),
                        BossPreviewSize * (bossScale * bossAttackScale));
                    bool bossMoving = contactT < 1f;
                    BattleVisualPose bossPose = ResolveEnemyPreviewPose(0, bossMoving);
                    SetImageSprite(enemyPreviewImages[0], SelectEnemyPreviewSprite(0, bossMoving));
                    BattleFacingDirection bossSourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, bossPose);
                    enemyPreviewImages[0].rectTransform.localScale = ResolveFacingScale(bossSourceFacing, BattleFacingDirection.Left);
                    Color bossColor = enemyPreviewImages[0].color;
                    bossColor.a = 1f - enemyVanishT;
                    enemyPreviewImages[0].color = bossColor;
                    engagedEnemyPreviewCount = contactT >= 1f ? 1 : 0;
                }

                for (int i = 1; i < enemyPreviewImages.Count; i += 1)
                {
                    ApplyPreviewImageLayout(enemyPreviewImages[i], MapBattlefieldAnchor(BossPreviewAnchor), Vector2.zero);
                }

                if (Application.isPlaying && stateMachine != null)
                {
                    stateMachine.SetEngagedEnemyCount(engagedEnemyPreviewCount);
                }

                return;
            }

            int activePreviewCount = Mathf.Clamp(targetEnemyPreviewCount, 0, enemyPreviewImages.Count);

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                Image image = enemyPreviewImages[i];
                if (image == null)
                {
                    continue;
                }

                float enemyHoldOffset = BattleAttackRangeResolver.ToEnemyHoldOffset(enemyAttackRange);
                float enemySearchOffset = BattleAttackRangeResolver.ToEnemySearchOffset(enemySearchRange);
                float baseY = i < enemyPreviewBaseYAnchors.Count ? enemyPreviewBaseYAnchors[i] : 0.40f;
                float verticalOffset = i < enemyPreviewVerticalOffsets.Count ? enemyPreviewVerticalOffsets[i] : 0f;
                float contactJitter = i < enemyPreviewContactJitters.Count ? enemyPreviewContactJitters[i] : 0f;
                float searchJitter = i < enemyPreviewSearchJitters.Count ? enemyPreviewSearchJitters[i] : 0f;
                float spawnXJitter = i < enemyPreviewSpawnXJitters.Count ? enemyPreviewSpawnXJitters[i] : 0f;
                float enemyKnockbackRemaining = i < enemyKnockbackRemainings.Count ? enemyKnockbackRemainings[i] : 0f;
                float enemyKnockbackT = KnockbackDuration > 0f
                    ? Mathf.Clamp01(enemyKnockbackRemaining / KnockbackDuration)
                    : 0f;
                float enemyKnockbackOffset = EnemyKnockbackDistance * EaseOutCubic(enemyKnockbackT);
                float enemyVanishRemaining = i < enemyDefeatVanishRemainings.Count ? enemyDefeatVanishRemainings[i] : 0f;
                float enemyVanishT = EnemyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(enemyVanishRemaining / EnemyDefeatVanishDuration)
                    : 0f;
                Vector2 contactAnchor = ResolveEnemySwarmContactAnchor(baseY, verticalOffset, enemyHoldOffset, contactJitter);
                Vector2 searchAnchor = ResolveEnemySwarmSearchAnchor(baseY, verticalOffset, enemySearchOffset, searchJitter);
                Vector2 spawnAnchor = ResolveEnemySwarmSpawnAnchor(baseY, verticalOffset, spawnXJitter);
                Vector2 anchor;
                float scale;
                float alpha;
                bool isActiveSlot = i < activePreviewCount;
                float slotProgress = enemyPreviewSlotProgress[i];
                bool shouldShow = isActiveSlot || slotProgress > 0.001f;
                float depthScale = 1f;
                float slotSearchT = combatSearchProgress > 0f
                    ? Mathf.Clamp01(slotProgress / combatSearchProgress)
                    : 1f;
                float slotContactT = combatStartProgress > combatSearchProgress
                    ? Mathf.Clamp01((slotProgress - combatSearchProgress) / (combatStartProgress - combatSearchProgress))
                    : 1f;

                if (!shouldShow)
                {
                    anchor = spawnAnchor;
                    scale = 0.72f;
                    alpha = 0f;
                }
                else if (!isActiveSlot)
                {
                    anchor = contactAnchor;
                    scale = depthScale;
                    alpha = Mathf.Clamp01(slotProgress);
                }
                else
                {
                    Vector2 searchApproachAnchor = Vector2.Lerp(spawnAnchor, searchAnchor, slotProgress);
                    anchor = Vector2.Lerp(searchApproachAnchor, contactAnchor, slotProgress);
                    scale = Mathf.Lerp(0.72f, depthScale, Mathf.Max(slotSearchT, slotContactT));
                    alpha = 1f;
                }

                if (shouldShow && i < 8)
                {
                    anchor += new Vector2(enemyKnockbackOffset, 0f);
                }

                if (shouldShow && enemyVanishRemaining > 0f)
                {
                    scale *= Mathf.Lerp(1f, 0.24f, enemyVanishT);
                    alpha *= 1f - enemyVanishT;
                }

                if (isActiveSlot && slotProgress >= combatStartProgress)
                {
                    engagedEnemyPreviewCount += 1;
                }

                float enemyAttackScale = IsEnemyAttackVisualActive(i) ? AttackPreviewScaleMultiplier : 1f;
                ApplyPreviewImageLayout(
                    image,
                    MapBattlefieldAnchor(anchor),
                    EnemyPreviewSize * (scale * enemyAttackScale));
                bool enemyMoving = slotProgress < combatStartProgress;
                BattleVisualPose enemyPose = ResolveEnemyPreviewPose(i, enemyMoving);
                SetImageSprite(image, SelectEnemyPreviewSprite(i, enemyMoving));
                BattleFacingDirection enemySourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, enemyPose);
                image.rectTransform.localScale = ResolveFacingScale(enemySourceFacing, BattleFacingDirection.Left);

                Color color = image.color;
                color.a = alpha;
                image.color = color;
            }

            if (Application.isPlaying && stateMachine != null)
            {
                stateMachine.SetEngagedEnemyCount(engagedEnemyPreviewCount);
            }
        }

        private static Image CreatePreviewImage(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(go);
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
                RegisterSceneObjectIfEditing(minimalCanvasRoot);

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
                RegisterSceneObjectIfEditing(backdropObject);
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

            EnsureSkillPanel();
            EnsureWaveHud();
        }

        private void EnsureSkillPanel()
        {
            if (minimalCanvasRoot == null)
            {
                return;
            }

            Transform existingPanel = minimalCanvasRoot.transform.Find("BattleSkillPanel");
            if (existingPanel != null)
            {
                skillPanelRoot = existingPanel.gameObject;
                return;
            }

            skillPanelRoot = new GameObject("BattleSkillPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(skillPanelRoot);
            RectTransform panelRect = skillPanelRoot.GetComponent<RectTransform>();
            panelRect.SetParent(minimalCanvasRoot.transform, false);
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, SkillPanelHeightRatio);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = skillPanelRoot.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.06f, 0.07f, 0.88f);

            GameObject divider = new GameObject("BattleSkillPanelDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(divider);
            RectTransform dividerRect = divider.GetComponent<RectTransform>();
            dividerRect.SetParent(skillPanelRoot.transform, false);
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.offsetMin = new Vector2(0f, -6f);
            dividerRect.offsetMax = new Vector2(0f, 0f);
            divider.GetComponent<Image>().color = new Color(0.62f, 0.48f, 0.24f, 0.95f);

            CreateSkillButtonPreview("SkillPreviewButton_1", skillPanelRoot.transform, 0.08f, "Strike");
            CreateSkillButtonPreview("SkillPreviewButton_2", skillPanelRoot.transform, 0.38f, "Drain");
            CreateSkillButtonPreview("SkillPreviewButton_3", skillPanelRoot.transform, 0.68f, "Guard");
        }

        private static void CreateSkillButtonPreview(string objectName, Transform parent, float minX, string label)
        {
            GameObject button = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(button);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(minX, 0.18f);
            rect.anchorMax = new Vector2(minX + 0.22f, 0.58f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = button.GetComponent<Image>();
            image.color = new Color(0.23f, 0.16f, 0.10f, 0.96f);

            GameObject accent = new GameObject($"{objectName}_Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(accent);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.SetParent(button.transform, false);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.offsetMin = new Vector2(0f, -6f);
            accentRect.offsetMax = Vector2.zero;
            accent.GetComponent<Image>().color = new Color(0.82f, 0.64f, 0.30f, 1f);

            GameObject textObject = new GameObject($"{objectName}_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RegisterSceneObjectIfEditing(textObject);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(button.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.93f, 0.86f, 1f);
            text.fontSize = 34;
            text.font = ResolveBuiltinUiFont();
        }

        private void EnsureWaveHud()
        {
            if (minimalCanvasRoot == null)
            {
                return;
            }

            Transform existingHud = minimalCanvasRoot.transform.Find("BattleWaveHud");
            if (existingHud != null)
            {
                waveHudRoot = existingHud.gameObject;
                if (waveEnemyCountFillImage == null)
                {
                    waveEnemyCountFillImage = waveHudRoot.transform.Find("EnemyCountFill")?.GetComponent<Image>();
                }

                if (waveEnemyCountText == null)
                {
                    waveEnemyCountText = waveHudRoot.transform.Find("EnemyCountText")?.GetComponent<Text>();
                }

                if (waveTitleText == null)
                {
                    waveTitleText = waveHudRoot.transform.Find("WaveTitleText")?.GetComponent<Text>();
                }

                if (battleStatusText == null)
                {
                    battleStatusText = waveHudRoot.transform.Find("BattleStatusText")?.GetComponent<Text>();
                }

                if (waveEnemyCountText == null)
                {
                    waveEnemyCountText = CreateHudText("EnemyCountText", waveHudRoot.transform, new Vector2(0.62f, -1.05f), new Vector2(0.99f, -0.10f), TextAnchor.MiddleRight, 34);
                }

                if (waveTitleText == null)
                {
                    waveTitleText = CreateHudText("WaveTitleText", waveHudRoot.transform, new Vector2(0.03f, 0.05f), new Vector2(0.26f, 0.95f), TextAnchor.MiddleLeft, 28);
                }

                if (battleStatusText == null)
                {
                    battleStatusText = CreateHudText("BattleStatusText", waveHudRoot.transform, new Vector2(0.30f, -1.10f), new Vector2(0.70f, -0.10f), TextAnchor.MiddleCenter, 44);
                }

                ApplyWaveHudLayout();
                return;
            }

            waveHudRoot = new GameObject("BattleWaveHud", typeof(RectTransform));
            RegisterSceneObjectIfEditing(waveHudRoot);
            RectTransform hudRect = waveHudRoot.GetComponent<RectTransform>();
            hudRect.SetParent(minimalCanvasRoot.transform, false);
            hudRect.anchorMin = new Vector2(0.08f, 0.825f);
            hudRect.anchorMax = new Vector2(0.92f, 0.865f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            GameObject barFrame = new GameObject("EnemyCountBarFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(barFrame);
            RectTransform barFrameRect = barFrame.GetComponent<RectTransform>();
            barFrameRect.SetParent(waveHudRoot.transform, false);
            barFrameRect.anchorMin = new Vector2(0f, 0.26f);
            barFrameRect.anchorMax = new Vector2(1f, 0.74f);
            barFrameRect.offsetMin = Vector2.zero;
            barFrameRect.offsetMax = Vector2.zero;
            barFrame.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.08f, 0.92f);

            GameObject fill = new GameObject("EnemyCountFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(fill);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(barFrame.transform, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            waveEnemyCountFillImage = fill.GetComponent<Image>();
            waveEnemyCountFillImage.color = new Color(0.60f, 0.86f, 0.24f, 1f);
            waveEnemyCountFillImage.type = Image.Type.Filled;
            waveEnemyCountFillImage.fillMethod = Image.FillMethod.Horizontal;
            waveEnemyCountFillImage.fillOrigin = 0;

            waveTitleText = CreateHudText("WaveTitleText", waveHudRoot.transform, new Vector2(0.03f, 0.05f), new Vector2(0.28f, 0.95f), TextAnchor.MiddleLeft, 28);
            waveEnemyCountText = CreateHudText("EnemyCountText", waveHudRoot.transform, new Vector2(0.62f, -1.05f), new Vector2(0.99f, -0.10f), TextAnchor.MiddleRight, 34);
            battleStatusText = CreateHudText("BattleStatusText", waveHudRoot.transform, new Vector2(0.30f, -1.10f), new Vector2(0.70f, -0.10f), TextAnchor.MiddleCenter, 44);
            waveTitleText.color = Color.white;
            waveEnemyCountText.color = new Color(1f, 0.96f, 0.82f, 1f);
            battleStatusText.color = new Color(1f, 0.92f, 0.72f, 1f);
            ApplyWaveHudLayout();
        }

        private static Text CreateHudText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment, int fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RegisterSceneObjectIfEditing(textObject);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.alignment = alignment;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.font = ResolveBuiltinUiFont();
            return text;
        }

        private void ApplyWaveHudLayout()
        {
            if (waveHudRoot == null)
            {
                return;
            }

            RectTransform hudRect = waveHudRoot.GetComponent<RectTransform>();
            if (hudRect != null)
            {
                hudRect.anchorMin = new Vector2(0.08f, 0.79f);
                hudRect.anchorMax = new Vector2(0.92f, 0.835f);
                hudRect.offsetMin = Vector2.zero;
                hudRect.offsetMax = Vector2.zero;
            }

            RectTransform barFrameRect = waveHudRoot.transform.Find("EnemyCountBarFrame")?.GetComponent<RectTransform>();
            if (barFrameRect != null)
            {
                barFrameRect.anchorMin = new Vector2(0f, 0.30f);
                barFrameRect.anchorMax = new Vector2(1f, 0.70f);
                barFrameRect.offsetMin = Vector2.zero;
                barFrameRect.offsetMax = Vector2.zero;
            }

            RectTransform fillRect = waveEnemyCountFillImage != null ? waveEnemyCountFillImage.rectTransform : null;
            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = new Vector2(4f, 4f);
                fillRect.offsetMax = new Vector2(-4f, -4f);
            }

            if (waveTitleText != null)
            {
                RectTransform titleRect = waveTitleText.rectTransform;
                titleRect.anchorMin = new Vector2(0.03f, 0.05f);
                titleRect.anchorMax = new Vector2(0.26f, 0.95f);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
                waveTitleText.alignment = TextAnchor.MiddleLeft;
                waveTitleText.fontSize = 28;
                waveTitleText.color = Color.white;
            }

            if (waveEnemyCountText != null)
            {
                RectTransform countRect = waveEnemyCountText.rectTransform;
                countRect.anchorMin = new Vector2(0.62f, -1.05f);
                countRect.anchorMax = new Vector2(0.99f, -0.10f);
                countRect.offsetMin = Vector2.zero;
                countRect.offsetMax = Vector2.zero;
                waveEnemyCountText.alignment = TextAnchor.MiddleRight;
                waveEnemyCountText.fontSize = 34;
                waveEnemyCountText.fontStyle = FontStyle.Bold;
                waveEnemyCountText.color = new Color(1f, 0.96f, 0.82f, 1f);
            }

            if (battleStatusText != null)
            {
                RectTransform statusRect = battleStatusText.rectTransform;
                statusRect.anchorMin = new Vector2(0.30f, -1.10f);
                statusRect.anchorMax = new Vector2(0.70f, -0.10f);
                statusRect.offsetMin = Vector2.zero;
                statusRect.offsetMax = Vector2.zero;
                battleStatusText.alignment = TextAnchor.MiddleCenter;
                battleStatusText.fontSize = 44;
                battleStatusText.fontStyle = FontStyle.Bold;
                battleStatusText.color = new Color(1f, 0.92f, 0.72f, 1f);
            }
        }

        private static Vector2 MapBattlefieldAnchor(Vector2 anchor)
        {
            return new Vector2(
                Mathf.Lerp(BattlefieldMinX, BattlefieldMaxX, anchor.x),
                Mathf.Lerp(BattlefieldMinY, BattlefieldMaxY, anchor.y));
        }

        private static Vector2 ResolveEnemySwarmContactAnchor(float baseY, float verticalOffset, float enemyHoldOffset, float contactJitter)
        {
            float x = 0.65f + enemyHoldOffset + contactJitter;
            float y = baseY + verticalOffset;
            return new Vector2(x, y);
        }

        private static Vector2 ResolveEnemySwarmSearchAnchor(float baseY, float verticalOffset, float enemySearchOffset, float searchJitter)
        {
            float x = 0.76f + enemySearchOffset + searchJitter;
            float y = baseY + verticalOffset;
            return new Vector2(x, y);
        }

        private static Vector2 ResolveEnemySwarmSpawnAnchor(float baseY, float verticalOffset, float spawnXJitter)
        {
            float x = 1.18f + spawnXJitter;
            float y = baseY + verticalOffset + EnemyPreviewSpawnOffset.y;
            return new Vector2(x, y);
        }

        private static Vector2 ResolveBossSpawnAnchor()
        {
            return new Vector2(1.14f, BossPreviewAnchor.y + EnemyPreviewSpawnOffset.y);
        }

        private void UpdateWaveHud(BattleSimulator simulator)
        {
            EnsureWaveHud();
            if (waveHudRoot == null)
            {
                return;
            }

            if (simulator == null)
            {
                if (waveTitleText != null)
                {
                    waveTitleText.text = "WAVE 1";
                }

                if (waveEnemyCountText != null)
                {
                    waveEnemyCountText.text = "残り 100";
                }

                if (waveEnemyCountFillImage != null)
                {
                    waveEnemyCountFillImage.fillAmount = 1f;
                }

                if (battleStatusText != null)
                {
                    battleStatusText.text = string.Empty;
                }

                return;
            }

            int totalCount = simulator.CurrentEnemyCountTarget;
            if (displayedRemainingEnemyCount < 0 || displayedRemainingEnemyCount > totalCount)
            {
                displayedRemainingEnemyCount = simulator.CurrentRemainingEnemyCount;
            }

            int remainingCount = Mathf.Clamp(displayedRemainingEnemyCount, 0, totalCount);
            float fill = totalCount > 0 ? (float)remainingCount / totalCount : 0f;

            if (waveTitleText != null)
            {
                waveTitleText.text = $"WAVE {simulator.CurrentWave}";
            }

            if (waveEnemyCountText != null)
            {
                waveEnemyCountText.text = $"残り {remainingCount}";
            }

            if (waveEnemyCountFillImage != null)
            {
                waveEnemyCountFillImage.fillAmount = fill;
            }

            if (battleStatusText != null)
            {
                battleStatusText.text = resultHandled ? (lastBattleWon ? "勝利" : "敗北") : string.Empty;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void RegisterSceneObjectIfEditing(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (gameObject == null || Application.isPlaying)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(gameObject, "Create Battle Scene Scaffold");
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
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
            SyncSimulatorSubscription();
            bool isBossEncounter = simulator != null && simulator.IsBossWave;
            int encounterSerial = simulator != null ? simulator.EncounterSerial : -1;
            int currentWave = simulator != null ? simulator.CurrentWave : -1;
            bool waveChanged = currentWave != lastPresentedWave;

            if (force || encounterSerial != lastEncounterSerial)
            {
                if (force || waveChanged)
                {
                    ResetEnemyPreviewProgress();
                    combatLoopProgress = 0f;
                    engagementProgress = Application.isPlaying ? 0f : 1f;
                    if (simulator != null)
                    {
                        displayedRemainingEnemyCount = simulator.CurrentRemainingEnemyCount;
                        pendingRemainingEnemyCount = simulator.CurrentRemainingEnemyCount;
                        enemyCountCommitDelayRemaining = 0f;
                    }
                    displayedEnemyPreviewCount = 0;
                    observedSpawnedEnemyCount = 0;
                    pendingEnemyPreviewRemovalIndices.Clear();
                    for (int i = 0; i < allyKnockbackRemainings.Count; i += 1)
                    {
                        allyKnockbackRemainings[i] = 0f;
                    }

                    for (int i = 0; i < allyAttackVisualRemainings.Count; i += 1)
                    {
                        allyAttackVisualRemainings[i] = 0f;
                    }

                    for (int i = 0; i < allyDefeatVanishRemainings.Count; i += 1)
                    {
                        allyDefeatVanishRemainings[i] = 0f;
                    }

                    for (int i = 0; i < enemyKnockbackRemainings.Count; i += 1)
                    {
                        enemyKnockbackRemainings[i] = 0f;
                    }

                    for (int i = 0; i < enemyAttackVisualRemainings.Count; i += 1)
                    {
                        enemyAttackVisualRemainings[i] = 0f;
                    }

                    for (int i = 0; i < enemyDefeatVanishRemainings.Count; i += 1)
                    {
                        enemyDefeatVanishRemainings[i] = 0f;
                    }
                }

                ApplyBackdropForEncounter(currentFloor, isBossEncounter);
                ApplyCombatantVisuals(currentFloor);
                UpdateWaveHud(simulator);
                lastEncounterSerial = encounterSerial;
            }

            lastPresentedWave = currentWave;
            UpdateWaveHud(simulator);
            UpdatePreviewLayout();
        }

        private void UpdateBattlePresentation(float deltaTime)
        {
            if (!minimalMonsterPresentation)
            {
                return;
            }

            RefreshBattlePresentation(force: false);

            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            bool hasSpawnedEnemies = simulator != null && simulator.CurrentSpawnedEnemyCount > 0;

            if (enemyCountCommitDelayRemaining > 0f)
            {
                enemyCountCommitDelayRemaining = Mathf.Max(0f, enemyCountCommitDelayRemaining - deltaTime);
                if (enemyCountCommitDelayRemaining <= 0f && pendingRemainingEnemyCount >= 0)
                {
                    displayedRemainingEnemyCount = pendingRemainingEnemyCount;
                }
            }

            for (int i = 0; i < allyKnockbackRemainings.Count; i += 1)
            {
                allyKnockbackRemainings[i] = Mathf.Max(0f, allyKnockbackRemainings[i] - deltaTime);
            }

            for (int i = 0; i < enemyKnockbackRemainings.Count; i += 1)
            {
                enemyKnockbackRemainings[i] = Mathf.Max(0f, enemyKnockbackRemainings[i] - deltaTime);
            }

            for (int i = 0; i < allyAttackVisualRemainings.Count; i += 1)
            {
                allyAttackVisualRemainings[i] = Mathf.Max(0f, allyAttackVisualRemainings[i] - deltaTime);
            }

            for (int i = 0; i < enemyAttackVisualRemainings.Count; i += 1)
            {
                enemyAttackVisualRemainings[i] = Mathf.Max(0f, enemyAttackVisualRemainings[i] - deltaTime);
            }

            for (int i = 0; i < allyDefeatVanishRemainings.Count; i += 1)
            {
                allyDefeatVanishRemainings[i] = Mathf.Max(0f, allyDefeatVanishRemainings[i] - deltaTime);
            }

            for (int i = 0; i < enemyDefeatVanishRemainings.Count; i += 1)
            {
                enemyDefeatVanishRemainings[i] = Mathf.Max(0f, enemyDefeatVanishRemainings[i] - deltaTime);
            }

            UpdateDisplayedEnemyPreviewCount(simulator);

            if (!hasSpawnedEnemies)
            {
                engagementProgress = 0f;
            }
            else if (engagementProgress < 1f)
            {
                engagementProgress = Mathf.Clamp01(engagementProgress + (deltaTime / EngagementDuration));
            }

            combatLoopProgress = Mathf.Repeat(combatLoopProgress + (deltaTime / CombatLoopDuration), 1f);
            EnsureEnemyPreviewCapacity(targetEnemyPreviewCount);
            enemyPreviewPressure = targetEnemyPreviewCount;
            for (int i = 0; i < enemyPreviewSlotProgress.Count; i += 1)
            {
                if (i < targetEnemyPreviewCount)
                {
                    enemyPreviewSlotProgress[i] = Mathf.MoveTowards(
                        enemyPreviewSlotProgress[i],
                        1f,
                        deltaTime / EngagementDuration);
                }
                else
                {
                    enemyPreviewSlotProgress[i] = Mathf.MoveTowards(
                        enemyPreviewSlotProgress[i],
                        0f,
                        deltaTime / 0.22f);
                }
            }

            UpdatePreviewLayout();
        }

        private bool IsCombatEngaged()
        {
            if (!minimalMonsterPresentation)
            {
                return true;
            }

            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            if (simulator == null || simulator.CurrentSpawnedEnemyCount <= 0)
            {
                return true;
            }

            return engagementProgress >= combatStartProgress;
        }

        private void SyncSimulatorSubscription()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            if (ReferenceEquals(subscribedSimulator, simulator))
            {
                return;
            }

            UnsubscribeSimulator();
            subscribedSimulator = simulator;
            if (subscribedSimulator != null)
            {
                subscribedSimulator.HitResolved += HandleBattleHitResolved;
                subscribedSimulator.EnemyDefeated += HandleEnemyDefeated;
                subscribedSimulator.AllyDefeated += HandleAllyDefeated;
            }
        }

        private void UnsubscribeSimulator()
        {
            if (subscribedSimulator != null)
            {
                subscribedSimulator.HitResolved -= HandleBattleHitResolved;
                subscribedSimulator.EnemyDefeated -= HandleEnemyDefeated;
                subscribedSimulator.AllyDefeated -= HandleAllyDefeated;
                subscribedSimulator = null;
            }
        }

        private void HandleBattleHitResolved(BattleHitInfo hitInfo)
        {
            if (hitInfo.TargetIsPlayer)
            {
                if (hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < enemyAttackVisualRemainings.Count)
                {
                    enemyAttackVisualRemainings[hitInfo.AttackerIndex] = AttackVisualDuration;
                }
            }
            else if (hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < allyAttackVisualRemainings.Count)
            {
                allyAttackVisualRemainings[hitInfo.AttackerIndex] = AttackVisualDuration;
            }

            if (!hitInfo.CausesKnockback)
            {
                return;
            }

            if (hitInfo.TargetIsPlayer)
            {
                if (hitInfo.TargetIndex >= 0 && hitInfo.TargetIndex < allyKnockbackRemainings.Count)
                {
                    allyKnockbackRemainings[hitInfo.TargetIndex] = KnockbackDuration;
                }
                return;
            }

            if (hitInfo.TargetIndex >= 0 && hitInfo.TargetIndex < enemyKnockbackRemainings.Count)
            {
                enemyKnockbackRemainings[hitInfo.TargetIndex] = KnockbackDuration;
            }
        }

        private void HandleEnemyDefeated(int remainingCount, int defeatedPreviewIndex)
        {
            pendingRemainingEnemyCount = Mathf.Max(0, remainingCount);
            enemyCountCommitDelayRemaining = EnemyCountCommitDelay;
            if (defeatedPreviewIndex >= 0)
            {
                pendingEnemyPreviewRemovalIndices.Add(defeatedPreviewIndex);
                if (defeatedPreviewIndex < enemyDefeatVanishRemainings.Count)
                {
                    enemyDefeatVanishRemainings[defeatedPreviewIndex] = EnemyDefeatVanishDuration;
                }
                if (defeatedPreviewIndex < enemyAttackVisualRemainings.Count)
                {
                    enemyAttackVisualRemainings[defeatedPreviewIndex] = 0f;
                }
            }
        }

        private void HandleAllyDefeated(int allyIndex)
        {
            if (allyIndex < 0 || allyIndex >= allyDefeatVanishRemainings.Count)
            {
                return;
            }

            allyDefeatVanishRemainings[allyIndex] = AllyDefeatVanishDuration;
            if (allyIndex < allyKnockbackRemainings.Count)
            {
                allyKnockbackRemainings[allyIndex] = 0f;
            }
            if (allyIndex < allyAttackVisualRemainings.Count)
            {
                allyAttackVisualRemainings[allyIndex] = 0f;
            }
        }

        private void UpdateDisplayedEnemyPreviewCount(BattleSimulator simulator)
        {
            if (simulator == null)
            {
                targetEnemyPreviewCount = 0;
                visibleEnemyPreviewCount = 0;
                displayedEnemyPreviewCount = 0;
                observedSpawnedEnemyCount = 0;
                pendingEnemyPreviewRemovalIndices.Clear();
                return;
            }

            if (simulator.IsBossWave)
            {
                displayedEnemyPreviewCount = simulator.CurrentRemainingEnemyCount > 0 ? 1 : 0;
                observedSpawnedEnemyCount = simulator.CurrentSpawnedEnemyCount;
                pendingEnemyPreviewRemovalIndices.Clear();
                targetEnemyPreviewCount = displayedEnemyPreviewCount;
                visibleEnemyPreviewCount = displayedEnemyPreviewCount;
                return;
            }

            if (simulator.CurrentSpawnedEnemyCount < observedSpawnedEnemyCount)
            {
                displayedEnemyPreviewCount = 0;
                observedSpawnedEnemyCount = 0;
                pendingEnemyPreviewRemovalIndices.Clear();
            }

            if (simulator.CurrentSpawnedEnemyCount > observedSpawnedEnemyCount)
            {
                int addedCount = simulator.CurrentSpawnedEnemyCount - observedSpawnedEnemyCount;
                displayedEnemyPreviewCount += addedCount;
                observedSpawnedEnemyCount = simulator.CurrentSpawnedEnemyCount;
            }

            if (pendingEnemyPreviewRemovalIndices.Count > 0)
            {
                pendingEnemyPreviewRemovalIndices.Sort((left, right) => left.CompareTo(right));
                int appliedRemovals = 0;
                for (int i = pendingEnemyPreviewRemovalIndices.Count - 1; i >= 0; i -= 1)
                {
                    int removalIndex = pendingEnemyPreviewRemovalIndices[i];
                    if (removalIndex < 0 || removalIndex >= enemyDefeatVanishRemainings.Count)
                    {
                        pendingEnemyPreviewRemovalIndices.RemoveAt(i);
                        continue;
                    }

                    if (enemyDefeatVanishRemainings[removalIndex] > 0f)
                    {
                        continue;
                    }

                    ConsumeEnemyPreviewRemovalAt(removalIndex);
                    pendingEnemyPreviewRemovalIndices.RemoveAt(i);
                    appliedRemovals += 1;
                }

                if (appliedRemovals > 0)
                {
                    displayedEnemyPreviewCount = Mathf.Max(
                        simulator.CurrentActiveEnemyCount,
                        displayedEnemyPreviewCount - appliedRemovals);
                }
            }

            displayedEnemyPreviewCount = Mathf.Max(displayedEnemyPreviewCount, simulator.CurrentActiveEnemyCount);
            targetEnemyPreviewCount = Mathf.Max(0, displayedEnemyPreviewCount);
            visibleEnemyPreviewCount = targetEnemyPreviewCount;
        }

        private void ConsumeEnemyPreviewRemovalAt(int removalIndex)
        {
            if (removalIndex < 0 || removalIndex >= enemyPreviewSlotProgress.Count)
            {
                return;
            }

            enemyPreviewSlotProgress.RemoveAt(removalIndex);
            enemyPreviewBaseYAnchors.RemoveAt(removalIndex);
            enemyPreviewVerticalOffsets.RemoveAt(removalIndex);
            enemyPreviewContactJitters.RemoveAt(removalIndex);
            enemyPreviewSearchJitters.RemoveAt(removalIndex);
            enemyPreviewSpawnXJitters.RemoveAt(removalIndex);
            enemyKnockbackRemainings.RemoveAt(removalIndex);
            enemyAttackVisualRemainings.RemoveAt(removalIndex);
            enemyDefeatVanishRemainings.RemoveAt(removalIndex);
            AppendEnemyPreviewSlotMetadata();
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

            EnsureAllyPreviewEffectCapacity();
        }

        private void CollectExistingEnemyPreviews(Transform existingRoot)
        {
            enemyPreviewImages.Clear();
            RemoveLegacyPreview(existingRoot.Find("EnemyMonsterPreview"));

            int index = 1;
            while (true)
            {
                Image image = existingRoot.Find($"EnemyMonsterPreview_{index}")?.GetComponent<Image>();
                if (image != null)
                {
                    enemyPreviewImages.Add(image);
                    index += 1;
                    continue;
                }

                break;
            }

            while (enemyPreviewSlotProgress.Count < enemyPreviewImages.Count)
            {
                AppendEnemyPreviewSlotMetadata();
            }
        }

        private void EnsureAllyPreviewEffectCapacity()
        {
            while (allyKnockbackRemainings.Count < allyPreviewImages.Count)
            {
                allyKnockbackRemainings.Add(0f);
            }

            while (allyAttackVisualRemainings.Count < allyPreviewImages.Count)
            {
                allyAttackVisualRemainings.Add(0f);
            }

            while (allyDefeatVanishRemainings.Count < allyPreviewImages.Count)
            {
                allyDefeatVanishRemainings.Add(0f);
            }
        }

        private void EnsureEnemyPreviewCapacity(int requiredCount)
        {
            int clampedRequiredCount = Mathf.Max(0, requiredCount);
            while (enemyPreviewImages.Count < clampedRequiredCount)
            {
                int index = enemyPreviewImages.Count + 1;
                enemyPreviewImages.Add(CreatePreviewImage($"EnemyMonsterPreview_{index}", monsterPreviewRoot.transform));
            }

            while (enemyPreviewSlotProgress.Count < enemyPreviewImages.Count)
            {
                AppendEnemyPreviewSlotMetadata();
            }
        }

        private void AppendEnemyPreviewSlotMetadata()
        {
            enemyPreviewSlotProgress.Add(0f);
            enemyPreviewBaseYAnchors.Add(Random.Range(0.27f, 0.55f));
            enemyPreviewVerticalOffsets.Add(Random.Range(-0.035f, 0.035f));
            enemyPreviewContactJitters.Add(Random.Range(-0.028f, 0.028f));
            enemyPreviewSearchJitters.Add(Random.Range(-0.035f, 0.035f));
            enemyPreviewSpawnXJitters.Add(Random.Range(-0.02f, 0.03f));
            enemyKnockbackRemainings.Add(0f);
            enemyAttackVisualRemainings.Add(0f);
            enemyDefeatVanishRemainings.Add(0f);
        }

        private void ApplyEnemyQueueSprites(Sprite enemySprite, bool isBossWave, int remainingEnemyCount)
        {
            EnsureMonsterPreviewRoot();

            int visibleEnemyCount = isBossWave
                ? 1
                : Mathf.Max(0, remainingEnemyCount);
            EnsureEnemyPreviewCapacity(visibleEnemyCount);
            targetEnemyPreviewCount = visibleEnemyCount;
            visibleEnemyPreviewCount = visibleEnemyCount;

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                Image image = enemyPreviewImages[i];
                bool shouldAssignSprite = isBossWave
                    ? i == 0 && visibleEnemyCount > 0
                    : visibleEnemyCount > 0;
                SetImageSprite(image, shouldAssignSprite ? enemySprite : null);

                if (image == null)
                {
                    continue;
                }

                Color color = image.color;
                color.a = shouldAssignSprite ? color.a : 0f;
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
