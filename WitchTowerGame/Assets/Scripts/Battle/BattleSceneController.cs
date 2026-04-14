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
        private static readonly Vector2[] AllyPreviewAnchors = BattleFormationLayout.AllyHomeAnchors;
        private static readonly Vector2[] AllyApproachAnchors = BattleFormationLayout.AllyAdvanceAnchors;
        private static readonly float[] EnemyPreviewLaneYAnchors = BattleFormationLayout.EnemyLaneYAnchors;

        private static readonly string[] DevPartyOverrideIdlePaths =
        {
            "MonsterBattle/mon_family_mage_1",
            "MonsterBattle/mon_family_slime_1",
            "MonsterBattle/mon_family_robot_1"
        };

        private static readonly string[] DevPartyOverrideMovePaths =
        {
            "MonsterBattle/mon_family_mage_1",
            "MonsterBattle/mon_family_slime_1",
            "MonsterBattle/mon_family_robot_1"
        };

        private static readonly string[] DevPartyOverrideAttackPaths =
        {
            "MonsterBattle/mon_family_mage_1",
            "MonsterBattle/mon_family_slime_1",
            "MonsterBattle/mon_family_robot_1"
        };

        private static readonly string[] DevPartyOverrideMonsterIds =
        {
            "monster_death_mage_elf",
            "monster_worm",
            "monster_rock_golem"
        };

        private static readonly Vector2 AllyPreviewSize = new Vector2(220f, 220f);
        private const int InitialEnemyPreviewSlotCapacity = 100;
        private static readonly Vector2 BossPreviewAnchor = new Vector2(0.78f, 0.43f);
        private static readonly Vector2 EnemyPreviewSpawnOffset = new Vector2(0f, 0.01f);
        private static readonly Vector2 EnemyPreviewSize = new Vector2(196f, 196f);
        private static readonly Vector2 BossPreviewSize = new Vector2(272f, 272f);
        private static Sprite fallbackRangedAttackEffectSprite;
        private static BattleAttackEffectProfileSO builtInFireProjectileEffectProfile;
        private const float AttackPreviewScaleMultiplier = 1.18f;
        private const float BattlefieldMinX = 0.06f;
        private const float BattlefieldMaxX = 0.95f;
        private const float BattlefieldMinY = 0.14f;
        private const float BattlefieldMaxY = 0.965f;
        private const float SkillPanelHeightRatio = 0.12f;
        private const float RangedAttackThreshold = 1.35f;
        private const float MeleeContactPaddingPixels = 0f;
        private const float MeleeHorizontalMoveSpeed = 0.88f;
        private const float MeleeVerticalFollowStrength = 0.88f;
        private const float MeleeVerticalMoveSpeed = 0.72f;

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
        [SerializeField] private BattleAttackEffectProfileSO defaultFireProjectileEffect;
        [SerializeField] private BattleAttackEffectProfileSO defaultThunderStrikeEffect;
        [SerializeField] private string[] normalBackdropResourcePaths =
        {
            "BattleBackgrounds/dungeon1_1170x2532",
            "BattleBackgrounds/dungeon2_1170x2532",
            "BattleBackgrounds/dungeon3_1170x2532"
        };
        [SerializeField] private string bossBackdropResourcePath = "BattleBackgrounds/boss3";
        [SerializeField] private int bossFloorInterval = 10;

        private sealed class ActiveRangedAttackEffect
        {
            public Image Image;
            public RectTransform RectTransform;
            public Color BaseColor;
            public Vector2 StaticPosition;
            public Vector2 StartPosition;
            public Vector2 EndPosition;
            public float Duration;
            public float Elapsed;
            public float ArcHeight;
            public float BaseSize;
            public float StartDelay;
            public float FadeOutScale = 0.66f;
            public bool UseArcMovement;
        }

        private sealed class PreviewHpBar
        {
            public RectTransform Root;
            public Image Background;
            public Image Fill;
            public Text Label;
        }

        private struct PendingHitReaction
        {
            public BattleHitInfo HitInfo;
            public float RemainingDelay;
        }

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
        private GameObject rangedEffectRoot;
        private GameObject skillPanelRoot;
        private GameObject waveHudRoot;
        private readonly List<Image> allyPreviewImages = new List<Image>();
        private readonly List<Image> enemyPreviewImages = new List<Image>();
        private readonly List<PreviewHpBar> allyPreviewHpBars = new List<PreviewHpBar>();
        private readonly List<PreviewHpBar> enemyPreviewHpBars = new List<PreviewHpBar>();
        private readonly List<float> allyPreviewTrackedTargetXAnchors = new List<float>();
        private readonly List<float> allyPreviewTrackedTargetYAnchors = new List<float>();
        private readonly List<float> enemyPreviewTrackedTargetXAnchors = new List<float>();
        private readonly List<float> enemyPreviewTrackedTargetYAnchors = new List<float>();
        private readonly List<int> allyPreviewLockedEnemyIndices = new List<int>();
        private readonly List<int> enemyPreviewLockedAllyIndices = new List<int>();
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
        private readonly List<PendingHitReaction> pendingHitReactions = new List<PendingHitReaction>();
        private readonly List<float> allyAttackVisualRemainings = new List<float>();
        private readonly List<float> enemyAttackVisualRemainings = new List<float>();
        private readonly List<float> allyDefeatVanishRemainings = new List<float>();
        private readonly List<float> enemyDefeatVanishRemainings = new List<float>();
        private readonly List<ActiveRangedAttackEffect> activeRangedAttackEffects = new List<ActiveRangedAttackEffect>();
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
        private static readonly Vector2 PreviewHpBarSize = new Vector2(52f, 8f);

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
            ClearActiveRangedAttackEffects();
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

            if (resultHandled)
            {
                UpdateBattlePresentation(Time.deltaTime);
                return;
            }

            var result = stateMachine.Tick(Time.deltaTime);
            UpdateBattlePresentation(Time.deltaTime);
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

        public void LogPreviewState()
        {
            EnsureMonsterPreviewRoot();
            var builder = new System.Text.StringBuilder();
            builder.Append("[BattleSceneController] PreviewRoot=");
            builder.Append(monsterPreviewRoot != null ? monsterPreviewRoot.activeInHierarchy.ToString() : "null");
            builder.Append(" AllyCount=");
            builder.Append(allyPreviewImages.Count);
            builder.Append(" EnemyCount=");
            builder.Append(enemyPreviewImages.Count);

            for (int i = 0; i < Mathf.Min(3, allyPreviewImages.Count); i += 1)
            {
                AppendPreviewImageDebug(builder, "Ally", i, allyPreviewImages[i]);
            }

            for (int i = 0; i < Mathf.Min(3, enemyPreviewImages.Count); i += 1)
            {
                AppendPreviewImageDebug(builder, "Enemy", i, enemyPreviewImages[i]);
            }

            Debug.Log(builder.ToString());
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

        private static void AppendPreviewImageDebug(System.Text.StringBuilder builder, string label, int index, Image image)
        {
            builder.Append(" ");
            builder.Append(label);
            builder.Append("[");
            builder.Append(index);
            builder.Append("]=");
            if (image == null)
            {
                builder.Append("null");
                return;
            }

            RectTransform rect = image.rectTransform;
            builder.Append("{active=");
            builder.Append(image.gameObject.activeInHierarchy);
            builder.Append(",sprite=");
            builder.Append(image.sprite != null ? image.sprite.name : "null");
            builder.Append(",alpha=");
            builder.Append(image.color.a.ToString("0.##"));
            builder.Append(",size=");
            builder.Append(rect != null ? rect.sizeDelta.ToString("0.##") : "null");
            builder.Append(",anchorMin=");
            builder.Append(rect != null ? rect.anchorMin.ToString("0.##") : "null");
            builder.Append(",anchorMax=");
            builder.Append(rect != null ? rect.anchorMax.ToString("0.##") : "null");
            builder.Append("}");
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
            bool useDebugPartyOverrides = partyMonsters.Count <= 0;
            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                MonsterDataSO partyData = null;
                if (!useDebugPartyOverrides && i < partyMonsters.Count)
                {
                    partyData = MasterDataManager.Instance?.GetMonsterData(partyMonsters[i].MonsterId);
                }
                else if (useDebugPartyOverrides && i < DevPartyOverrideMonsterIds.Length && !string.IsNullOrEmpty(DevPartyOverrideMonsterIds[i]))
                {
                    partyData = MasterDataManager.Instance?.GetMonsterData(DevPartyOverrideMonsterIds[i]);
                }

                if (useDebugPartyOverrides)
                {
                    allyIdleSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideIdlePaths, partyData, BattleVisualResolver.ResolveMonsterIdleSprites));
                    allyMoveSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideMovePaths, partyData, BattleVisualResolver.ResolveMonsterMoveSprites));
                    allyAttackSprites.Add(ResolvePartyOverrideFrames(i, DevPartyOverrideAttackPaths, partyData, BattleVisualResolver.ResolveMonsterAttackSprites));
                }
                else
                {
                    allyIdleSprites.Add(BattleVisualResolver.ResolveMonsterIdleSprites(partyData));
                    allyMoveSprites.Add(BattleVisualResolver.ResolveMonsterMoveSprites(partyData));
                    allyAttackSprites.Add(BattleVisualResolver.ResolveMonsterAttackSprites(partyData));
                }

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
                        ? "単一フィールド / ボス戦"
                        : $"単一フィールド / 残敵 {simulator.CurrentRemainingEnemyCount}";
                }
                else
                {
                    bool isBossFloor = bossFloorInterval > 0 && floor > 0 && floor % bossFloorInterval == 0;
                    enemyHint.text = isBossFloor ? "単一フィールド / ボス戦" : "単一フィールド / 敵部隊";
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
            allyPreviewTrackedTargetXAnchors.Clear();
            enemyPreviewTrackedTargetYAnchors.Clear();
            enemyPreviewTrackedTargetXAnchors.Clear();
            enemyPreviewLockedAllyIndices.Clear();
            allyPreviewLockedEnemyIndices.Clear();
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

            EnsureAllyPreviewHpBarCapacity();
            EnsureAllyPreviewEffectCapacity();
            EnsureEnemyPreviewCapacity(InitialEnemyPreviewSlotCapacity);
        }

        private void UpdatePreviewLayout()
        {
            EnsureAllyPreviewEffectCapacity();
            EnsureEnemyPreviewCapacity(enemyPreviewImages.Count);

            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            if (Application.isPlaying && simulator != null)
            {
                UpdatePreviewLayoutFromSimulator(simulator);
                return;
            }

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
            bool isBossWave = stateMachine != null && stateMachine.Simulator != null && stateMachine.Simulator.IsBossWave;
            bool enemyIsMelee = IsEnemyCloseCombat(currentPreviewEnemyData, enemyAttackRange);
            int activePreviewCount = Mathf.Clamp(targetEnemyPreviewCount, 0, enemyPreviewImages.Count);
            var resolvedAllyAnchors = new Vector2[AllyPreviewAnchors.Length];
            var allySlotAlive = new bool[AllyPreviewAnchors.Length];

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
                MonsterDataSO allyData = i < allyPreviewMonsterData.Count ? allyPreviewMonsterData[i] : null;
                float allyRange = i < allyAttackRanges.Count ? allyAttackRanges[i] : 1f;
                float allyHoldOffset = BattleAttackRangeResolver.ToAllyHoldOffset(allyRange);
                Vector2 allyStartAnchor = AllyPreviewAnchors[i];
                float allyTargetX = AllyApproachAnchors[i].x - allyHoldOffset;
                float allyTargetY = AllyApproachAnchors[i].y;
                if (IsMonsterCloseCombat(allyData))
                {
                    float enemyReferenceWidth = isBossWave ? BossPreviewSize.x : EnemyPreviewSize.x;
                    float contactGap = ResolveMeleeContactGapAnchor(AllyPreviewSize.x, enemyReferenceWidth);
                    int targetEnemyIndex = ResolveAllyLockedEnemyIndex(i, simulator, isBossWave, activePreviewCount);
                    float enemyHoldOffset = BattleAttackRangeResolver.ToEnemyHoldOffset(enemyAttackRange);
                    allyTargetX = ResolveAllyMeleeTargetXAnchor(targetEnemyIndex, isBossWave, enemyHoldOffset, contactGap, allyTargetX);
                    allyTargetY = ResolveAllyMeleeTargetYAnchor(allyStartAnchor.y, targetEnemyIndex, isBossWave);
                }
                allyTargetX = ResolveSmoothedTrackedTargetAnchor(allyPreviewTrackedTargetXAnchors, i, allyStartAnchor.x, allyTargetX, MeleeHorizontalMoveSpeed);
                allyTargetY = ResolveSmoothedTrackedTargetY(allyPreviewTrackedTargetYAnchors, i, allyStartAnchor.y, allyTargetY);

                Vector2 allyTargetAnchor = new Vector2(allyTargetX, allyTargetY);
                Vector2 allyAnchor = Vector2.Lerp(allyStartAnchor, allyTargetAnchor, allyApproachT);
                allyAnchor += new Vector2(-allyKnockbackOffset, 0f);
                resolvedAllyAnchors[i] = allyAnchor;
                allySlotAlive[i] = allyAlive;
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
                    BattleFacingDirection allySourceFacing = BattleVisualResolver.ResolveMonsterFacing(allyData, allyPose);
                    allyPreviewImages[i].rectTransform.localScale = ResolveFacingScale(allySourceFacing, BattleFacingDirection.Right);
                    Color allyColor = allyPreviewImages[i].color;
                    allyColor.a = allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT);
                    allyPreviewImages[i].color = allyColor;
                }

                int allyCurrentHp = simulator != null && simulator.HasAllyRuntime(i) ? simulator.GetAllyCurrentHp(i) : 0;
                int allyMaxHp = simulator != null && simulator.HasAllyRuntime(i) ? simulator.GetAllyMaxHp(i) : 0;
                UpdatePreviewHpBar(
                    i < allyPreviewHpBars.Count ? allyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(allyAnchor),
                    AllyPreviewSize * (allyScale * allyAttackScale),
                    allyAlive && allyMaxHp > 0,
                    allyColorAlpha: allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT),
                    currentHp: allyCurrentHp,
                    maxHp: allyMaxHp,
                    fillColor: new Color(0.28f, 0.88f, 0.66f, 0.95f));
            }

            if (isBossWave)
            {
                if (enemyPreviewImages.Count > 0)
                {
                    float enemyHoldOffset = BattleAttackRangeResolver.ToEnemyHoldOffset(enemyAttackRange);
                    float enemySearchOffset = BattleAttackRangeResolver.ToEnemySearchOffset(enemySearchRange);
                    Vector2 bossSpawnAnchor = ResolveBossSpawnAnchor();
                    Vector2 bossSearchAnchor = BossPreviewAnchor + new Vector2(enemySearchOffset, 0f);
                    float bossContactX = BossPreviewAnchor.x + enemyHoldOffset;
                    int bossTargetAllyIndex = ResolveEnemyLockedAllyIndex(0, 1, allySlotAlive, bossSearchAnchor, resolvedAllyAnchors, simulator);
                    float bossContactY = enemyIsMelee
                        ? ResolveEnemyMeleeTargetYAnchor(BossPreviewAnchor.y, bossTargetAllyIndex, resolvedAllyAnchors)
                        : BossPreviewAnchor.y;
                    if (enemyIsMelee)
                    {
                        float meleeContactGap = ResolveMeleeContactGapAnchor(AllyPreviewSize.x, BossPreviewSize.x);
                        bossContactX = ResolveEnemyMeleeTargetXAnchor(BossPreviewAnchor.x, meleeContactGap, bossTargetAllyIndex, resolvedAllyAnchors);
                    }
                    bossContactX = ResolveSmoothedTrackedTargetAnchor(enemyPreviewTrackedTargetXAnchors, 0, BossPreviewAnchor.x, bossContactX, MeleeHorizontalMoveSpeed);
                    bossContactY = ResolveSmoothedTrackedTargetY(enemyPreviewTrackedTargetYAnchors, 0, BossPreviewAnchor.y, bossContactY);
                    Vector2 bossAnchor = Vector2.Lerp(
                        bossSpawnAnchor,
                        bossSearchAnchor,
                        searchT);
                    bossAnchor = Vector2.Lerp(
                        bossAnchor,
                        new Vector2(bossContactX, bossContactY),
                        contactT);
                    float enemyKnockbackRemaining = enemyKnockbackRemainings.Count > 0 ? enemyKnockbackRemainings[0] : 0f;
                    float enemyKnockbackT = KnockbackDuration > 0f
                        ? Mathf.Clamp01(enemyKnockbackRemaining / KnockbackDuration)
                        : 0f;
                    float enemyVanishRemaining = enemyDefeatVanishRemainings.Count > 0 ? enemyDefeatVanishRemainings[0] : 0f;
                    float enemyVanishT = enemyVanishRemaining > 0f && EnemyDefeatVanishDuration > 0f
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
                    int bossCurrentHp = simulator != null && simulator.HasEnemyRuntime(0) ? simulator.GetEnemyCurrentHp(0) : 0;
                    int bossMaxHp = simulator != null && simulator.HasEnemyRuntime(0) ? simulator.GetEnemyMaxHp(0) : 0;
                    UpdatePreviewHpBar(
                        enemyPreviewHpBars.Count > 0 ? enemyPreviewHpBars[0] : null,
                        MapBattlefieldAnchor(bossAnchor),
                        BossPreviewSize * (bossScale * bossAttackScale),
                        bossMaxHp > 0,
                        1f - enemyVanishT,
                        bossCurrentHp,
                        bossMaxHp,
                        new Color(0.96f, 0.44f, 0.40f, 0.95f));
                    engagedEnemyPreviewCount = contactT >= 1f ? 1 : 0;
                }

                for (int i = 1; i < enemyPreviewImages.Count; i += 1)
                {
                    ApplyPreviewImageLayout(enemyPreviewImages[i], MapBattlefieldAnchor(BossPreviewAnchor), Vector2.zero);
                    UpdatePreviewHpBar(i < enemyPreviewHpBars.Count ? enemyPreviewHpBars[i] : null, MapBattlefieldAnchor(BossPreviewAnchor), Vector2.zero, false, 0f, 0, 0, new Color(0.96f, 0.44f, 0.40f, 0.95f));
                }

                if (Application.isPlaying && stateMachine != null)
                {
                    stateMachine.SetEngagedEnemyCount(engagedEnemyPreviewCount);
                }

                return;
            }

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
                float enemyVanishT = enemyVanishRemaining > 0f && EnemyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(enemyVanishRemaining / EnemyDefeatVanishDuration)
                    : 0f;
                Vector2 contactAnchor = ResolveEnemySwarmContactAnchor(baseY, verticalOffset, enemyHoldOffset, contactJitter);
                Vector2 searchAnchor = ResolveEnemySwarmSearchAnchor(baseY, verticalOffset, enemySearchOffset, searchJitter);
                Vector2 spawnAnchor = ResolveEnemySwarmSpawnAnchor(baseY, verticalOffset, spawnXJitter);
                if (enemyIsMelee)
                {
                    float meleeContactGap = ResolveMeleeContactGapAnchor(AllyPreviewSize.x, EnemyPreviewSize.x);
                    int targetAllyIndex = ResolveEnemyLockedAllyIndex(i, activePreviewCount, allySlotAlive, searchAnchor, resolvedAllyAnchors, simulator);
                    contactAnchor.x = ResolveEnemyMeleeTargetXAnchor(contactAnchor.x, meleeContactGap, targetAllyIndex, resolvedAllyAnchors);
                    contactAnchor.y = ResolveEnemyMeleeTargetYAnchor(contactAnchor.y, targetAllyIndex, resolvedAllyAnchors);
                }
                contactAnchor.x = ResolveSmoothedTrackedTargetAnchor(enemyPreviewTrackedTargetXAnchors, i, contactAnchor.x, contactAnchor.x, MeleeHorizontalMoveSpeed);
                contactAnchor.y = ResolveSmoothedTrackedTargetY(enemyPreviewTrackedTargetYAnchors, i, contactAnchor.y, contactAnchor.y);
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

                int enemyCurrentHp = simulator != null && simulator.HasEnemyRuntime(i) ? simulator.GetEnemyCurrentHp(i) : 0;
                int enemyMaxHp = simulator != null && simulator.HasEnemyRuntime(i) ? simulator.GetEnemyMaxHp(i) : 0;
                UpdatePreviewHpBar(
                    i < enemyPreviewHpBars.Count ? enemyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(anchor),
                    EnemyPreviewSize * (scale * enemyAttackScale),
                    shouldShow && enemyMaxHp > 0,
                    alpha,
                    enemyCurrentHp,
                    enemyMaxHp,
                    new Color(0.96f, 0.44f, 0.40f, 0.95f));
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

        private void UpdatePreviewLayoutFromSimulator(BattleSimulator simulator)
        {
            if (simulator.CurrentEnemyData != currentPreviewEnemyData ||
                ((enemyIdleSprites == null || enemyIdleSprites.Count == 0) &&
                 (enemyMoveSprites == null || enemyMoveSprites.Count == 0) &&
                 (enemyAttackSprites == null || enemyAttackSprites.Count == 0)))
            {
                ApplyCombatantVisuals(currentFloor);
            }

            EnsureEnemyPreviewCapacity(Mathf.Max(InitialEnemyPreviewSlotCapacity, simulator.CurrentActiveEnemyCount));
            int activeEnemyCount = Mathf.Clamp(simulator.CurrentActiveEnemyCount, 0, enemyPreviewImages.Count);

            for (int i = 0; i < allyPreviewImages.Count && i < AllyPreviewAnchors.Length; i += 1)
            {
                bool allyAlive = simulator.HasAllyRuntime(i) && simulator.IsAllyAlive(i);
                bool allyMoving = allyAlive && simulator.IsAllyMoving(i);
                Vector2 allyAnchor = simulator.GetAllyPositionAnchor(i);
                float allyDefeatRemaining = i < allyDefeatVanishRemainings.Count ? allyDefeatVanishRemainings[i] : 0f;
                float allyVanishT = AllyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(allyDefeatRemaining / AllyDefeatVanishDuration)
                    : 0f;
                float allyScale = allyAlive ? 1f : Mathf.Lerp(1f, 0.24f, allyVanishT);
                float allyAttackScale = IsAllyAttackVisualActive(i) ? AttackPreviewScaleMultiplier : 1f;

                ApplyPreviewImageLayout(
                    allyPreviewImages[i],
                    MapBattlefieldAnchor(allyAnchor),
                    AllyPreviewSize * (allyScale * allyAttackScale));

                MonsterDataSO allyData = i < allyPreviewMonsterData.Count ? allyPreviewMonsterData[i] : null;
                float allyApproachT = allyMoving ? 0f : 1f;
                Sprite allySprite = SelectAllyPreviewSprite(i, allyApproachT);
                SetImageSprite(allyPreviewImages[i], allySprite);
                BattleVisualPose allyPose = ResolveAllyPreviewPose(i, allyApproachT);
                BattleFacingDirection allySourceFacing = BattleVisualResolver.ResolveMonsterFacing(allyData, allyPose);
                allyPreviewImages[i].rectTransform.localScale = ResolveFacingScale(allySourceFacing, BattleFacingDirection.Right);
                Color allyColor = allyPreviewImages[i].color;
                allyColor.a = allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT);
                allyPreviewImages[i].color = allyColor;

                UpdatePreviewHpBar(
                    i < allyPreviewHpBars.Count ? allyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(allyAnchor),
                    AllyPreviewSize * (allyScale * allyAttackScale),
                    simulator.HasAllyRuntime(i),
                    allyColor.a,
                    simulator.GetAllyCurrentHp(i),
                    simulator.GetAllyMaxHp(i),
                    new Color(0.28f, 0.88f, 0.66f, 0.95f));
            }

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                Image image = enemyPreviewImages[i];
                if (image == null)
                {
                    continue;
                }

                bool shouldShow = i < activeEnemyCount && simulator.HasEnemyRuntime(i);
                if (!shouldShow)
                {
                    ApplyPreviewImageLayout(image, MapBattlefieldAnchor(new Vector2(1.10f, ResolveEnemyPreviewLaneY(i))), Vector2.zero);
                    UpdatePreviewHpBar(i < enemyPreviewHpBars.Count ? enemyPreviewHpBars[i] : null, MapBattlefieldAnchor(new Vector2(1.10f, ResolveEnemyPreviewLaneY(i))), Vector2.zero, false, 0f, 0, 0, new Color(0.96f, 0.44f, 0.40f, 0.95f));
                    continue;
                }

                Vector2 enemyAnchor = simulator.GetEnemyPositionAnchor(i);
                bool enemyMoving = simulator.IsEnemyMoving(i);
                float enemyDefeatRemaining = i < enemyDefeatVanishRemainings.Count ? enemyDefeatVanishRemainings[i] : 0f;
                float enemyVanishT = enemyDefeatRemaining > 0f && EnemyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(enemyDefeatRemaining / EnemyDefeatVanishDuration)
                    : 0f;
                float scale = Mathf.Lerp(1f, 0.24f, enemyVanishT);
                float enemyAttackScale = IsEnemyAttackVisualActive(i) ? AttackPreviewScaleMultiplier : 1f;
                Vector2 previewSize = (simulator.IsBossWave && i == 0 ? BossPreviewSize : EnemyPreviewSize) * (scale * enemyAttackScale);
                ApplyPreviewImageLayout(image, MapBattlefieldAnchor(enemyAnchor), previewSize);

                SetImageSprite(image, SelectEnemyPreviewSprite(i, enemyMoving));
                BattleVisualPose enemyPose = ResolveEnemyPreviewPose(i, enemyMoving);
                BattleFacingDirection enemySourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, enemyPose);
                image.rectTransform.localScale = ResolveFacingScale(enemySourceFacing, BattleFacingDirection.Left);
                Color color = image.color;
                color.a = 1f - enemyVanishT;
                image.color = color;

                UpdatePreviewHpBar(
                    i < enemyPreviewHpBars.Count ? enemyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(enemyAnchor),
                    previewSize,
                    true,
                    color.a,
                    simulator.GetEnemyCurrentHp(i),
                    simulator.GetEnemyMaxHp(i),
                    new Color(0.96f, 0.44f, 0.40f, 0.95f));
            }

            if (stateMachine != null)
            {
                stateMachine.SetEngagedEnemyCount(simulator.CurrentEngagedEnemyCount);
            }
        }

        private PreviewHpBar CreatePreviewHpBar(string objectName, Transform parent)
        {
            GameObject rootObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(rootObject);
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Image background = rootObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);

            GameObject fillObject = new GameObject($"{objectName}_Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(fillObject);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(rootRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            Image fill = fillObject.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.color = new Color(0.28f, 0.88f, 0.66f, 0.95f);

            GameObject labelObject = new GameObject($"{objectName}_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RegisterSceneObjectIfEditing(labelObject);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(0f, 14f);

            Text label = labelObject.GetComponent<Text>();
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.96f);
            label.fontSize = 10;
            label.fontStyle = FontStyle.Bold;
            label.font = ResolveBuiltinUiFont();

            rootObject.transform.SetAsLastSibling();
            return new PreviewHpBar
            {
                Root = rootRect,
                Background = background,
                Fill = fill,
                Label = label
            };
        }

        private void UpdatePreviewHpBar(PreviewHpBar hpBar, Vector2 anchor, Vector2 previewSize, bool visible, float allyColorAlpha, int currentHp, int maxHp, Color fillColor)
        {
            if (hpBar == null || hpBar.Root == null)
            {
                return;
            }

            if (!visible || maxHp <= 0f || previewSize.x <= 0f || previewSize.y <= 0f)
            {
                hpBar.Root.gameObject.SetActive(false);
                return;
            }

            hpBar.Root.gameObject.SetActive(true);
            hpBar.Root.anchorMin = anchor;
            hpBar.Root.anchorMax = anchor;
            hpBar.Root.anchoredPosition = new Vector2(0f, (previewSize.y * 0.60f) + 12f);
            hpBar.Root.sizeDelta = new Vector2(Mathf.Clamp(previewSize.x * 0.82f, 34f, 88f), PreviewHpBarSize.y);

            float alpha = Mathf.Clamp01(allyColorAlpha);
            if (hpBar.Background != null)
            {
                Color bg = hpBar.Background.color;
                bg.a = 0.78f * alpha;
                hpBar.Background.color = bg;
            }

            if (hpBar.Fill != null)
            {
                hpBar.Fill.fillAmount = Mathf.Clamp01((float)Mathf.Clamp(currentHp, 0, maxHp) / Mathf.Max(1, maxHp));
                Color fill = fillColor;
                fill.a *= alpha;
                hpBar.Fill.color = fill;
            }

            if (hpBar.Label != null)
            {
                Color labelColor = hpBar.Label.color;
                labelColor.a = alpha;
                hpBar.Label.color = labelColor;
                hpBar.Label.text = $"{Mathf.Clamp(currentHp, 0, maxHp)}/{maxHp}";
            }
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

        private void TrySpawnRangedAttackEffect(BattleHitInfo hitInfo)
        {
            if (!minimalMonsterPresentation || !Application.isPlaying || !IsRangedAttackHit(hitInfo))
            {
                return;
            }

            EnsureMinimalCanvas();
            EnsureRangedEffectRoot();

            BattleAttackEffectProfileSO profile = ResolveAttackEffectProfile(hitInfo);
            if (!TryResolveRangedAttackEndpoints(hitInfo, profile, out Vector2 startPosition, out Vector2 endPosition))
            {
                return;
            }

            if (profile == null)
            {
                SpawnFallbackRangedAttackEffect(hitInfo, startPosition, endPosition);
                return;
            }

            Color tint = profile.colorTint.a > 0f ? profile.colorTint : Color.white;
            float scale = Mathf.Max(0.1f, profile.scale);

            if (profile.UsesProjectile && profile.HasProjectileSprite)
            {
                float distance = Vector2.Distance(startPosition, endPosition);
                float projectileDuration = Mathf.Max(
                    0.05f,
                    profile.projectileDuration > 0f
                        ? profile.projectileDuration
                        : Mathf.Lerp(0.12f, 0.22f, Mathf.Clamp01(distance / 520f)));
                float arcHeight = Mathf.Lerp(16f, 42f, Mathf.Clamp01(distance / 480f));

                SpawnMovingRangedAttackEffect(
                    profile.projectileSprite,
                    tint,
                    startPosition,
                    endPosition,
                    projectileDuration,
                    arcHeight,
                    ResolveSpriteBaseSize(profile.projectileSprite, scale, 34f),
                    Mathf.Max(0f, profile.projectileDelay));
            }

            if (profile.UsesSummonStrike)
            {
                if (profile.HasWarningAirSprite)
                {
                    SpawnStaticRangedAttackEffect(
                        profile.warningAirSprite,
                        tint,
                        endPosition + profile.warningAirOffset,
                        Mathf.Max(0.08f, profile.projectileDelay),
                        ResolveSpriteBaseSize(profile.warningAirSprite, scale, 78f),
                        0f,
                        0.92f);
                }

                if (profile.HasWarningGroundSprite)
                {
                    SpawnStaticRangedAttackEffect(
                        profile.warningGroundSprite,
                        tint,
                        endPosition + profile.warningGroundOffset,
                        Mathf.Max(0.08f, profile.projectileDelay),
                        ResolveSpriteBaseSize(profile.warningGroundSprite, scale, 88f),
                        0f,
                        0.92f);
                }

                if (profile.HasProjectileSprite)
                {
                    SpawnMovingRangedAttackEffect(
                        profile.projectileSprite,
                        tint,
                        startPosition,
                        endPosition,
                        Mathf.Max(0.05f, profile.projectileDuration),
                        0f,
                        ResolveSpriteBaseSize(profile.projectileSprite, scale, 86f),
                        Mathf.Max(0f, profile.projectileDelay));
                }
            }

            if (profile.HasImpactSprite)
            {
                SpawnStaticRangedAttackEffect(
                    profile.impactSprite,
                    tint,
                    endPosition + profile.targetOffset,
                    Mathf.Max(0f, profile.impactDelay),
                    ResolveSpriteBaseSize(profile.impactSprite, scale, 70f),
                    0f,
                    0.60f);
            }

            if (profile.HasHitOverlaySprite)
            {
                SpawnStaticRangedAttackEffect(
                    profile.hitOverlaySprite,
                    tint,
                    endPosition + profile.targetOffset,
                    Mathf.Max(0f, profile.hitOverlayDelay),
                    ResolveSpriteBaseSize(profile.hitOverlaySprite, scale, 64f),
                    Mathf.Max(0.10f, profile.loopDuration),
                    0.78f);
            }
        }

        private void UpdateRangedAttackEffects(float deltaTime)
        {
            if (activeRangedAttackEffects.Count <= 0)
            {
                return;
            }

            for (int i = activeRangedAttackEffects.Count - 1; i >= 0; i -= 1)
            {
                ActiveRangedAttackEffect effect = activeRangedAttackEffects[i];
                if (effect == null || effect.Image == null || effect.RectTransform == null)
                {
                    activeRangedAttackEffects.RemoveAt(i);
                    continue;
                }

                effect.Elapsed += deltaTime;
                if (effect.Elapsed < effect.StartDelay)
                {
                    effect.Image.enabled = false;
                    continue;
                }

                effect.Image.enabled = true;
                float activeElapsed = effect.Elapsed - effect.StartDelay;
                float normalized = effect.Duration > 0f
                    ? Mathf.Clamp01(activeElapsed / effect.Duration)
                    : 1f;

                Vector2 currentPosition = effect.UseArcMovement
                    ? Vector2.Lerp(effect.StartPosition, effect.EndPosition, normalized)
                    : effect.StaticPosition;
                if (effect.UseArcMovement)
                {
                    float arcOffset = Mathf.Sin(normalized * Mathf.PI) * effect.ArcHeight;
                    currentPosition.y += arcOffset;
                }

                effect.RectTransform.anchoredPosition = currentPosition;

                float fadeProgress;
                if (effect.UseArcMovement)
                {
                    fadeProgress = normalized >= 0.82f
                        ? Mathf.InverseLerp(0.82f, 1f, normalized)
                        : 0f;
                }
                else
                {
                    fadeProgress = normalized;
                }

                effect.RectTransform.sizeDelta = Vector2.one * Mathf.Lerp(effect.BaseSize, effect.BaseSize * effect.FadeOutScale, fadeProgress);

                float glow = 1f - Mathf.Abs((normalized * 2f) - 1f);
                Color color = effect.BaseColor;
                color.a = Mathf.Lerp(0.95f, 0.15f, fadeProgress);
                color = Color.Lerp(color, Color.white, glow * 0.22f);
                effect.Image.color = color;

                if (normalized < 1f)
                {
                    continue;
                }

                Destroy(effect.Image.gameObject);
                activeRangedAttackEffects.RemoveAt(i);
            }
        }

        private void ClearActiveRangedAttackEffects()
        {
            for (int i = activeRangedAttackEffects.Count - 1; i >= 0; i -= 1)
            {
                ActiveRangedAttackEffect effect = activeRangedAttackEffects[i];
                if (effect?.Image != null)
                {
                    Destroy(effect.Image.gameObject);
                }
            }

            activeRangedAttackEffects.Clear();
        }

        private bool IsRangedAttackHit(BattleHitInfo hitInfo)
        {
            if (hitInfo.AttackerIndex < 0)
            {
                return false;
            }

            if (hitInfo.TargetIsPlayer)
            {
                return enemyAttackRange >= RangedAttackThreshold;
            }

            if (hitInfo.AttackerIndex >= allyAttackRanges.Count)
            {
                return false;
            }

            return allyAttackRanges[hitInfo.AttackerIndex] >= RangedAttackThreshold;
        }

        private BattleAttackEffectProfileSO ResolveAttackEffectProfile(BattleHitInfo hitInfo)
        {
            if (!hitInfo.TargetIsPlayer)
            {
                MonsterDataSO attackerData = hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < allyPreviewMonsterData.Count
                    ? allyPreviewMonsterData[hitInfo.AttackerIndex]
                    : null;
                if (attackerData != null &&
                    attackerData.monsterId == "monster_death_mage_elf")
                {
                    return defaultFireProjectileEffect != null
                        ? defaultFireProjectileEffect
                        : GetBuiltInFireProjectileEffectProfile();
                }
            }

            MonsterDamageType damageType = ResolveAttackDamageType(hitInfo);
            if (damageType == MonsterDamageType.Magic && defaultThunderStrikeEffect != null)
            {
                return defaultThunderStrikeEffect;
            }

            if (defaultFireProjectileEffect != null)
            {
                return defaultFireProjectileEffect;
            }

            return null;
        }

        private MonsterDamageType ResolveAttackDamageType(BattleHitInfo hitInfo)
        {
            if (hitInfo.TargetIsPlayer)
            {
                return currentPreviewEnemyData != null ? currentPreviewEnemyData.damageType : MonsterDamageType.Physical;
            }

            MonsterDataSO attackerData = hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < allyPreviewMonsterData.Count
                ? allyPreviewMonsterData[hitInfo.AttackerIndex]
                : null;
            return attackerData != null ? attackerData.damageType : MonsterDamageType.Physical;
        }

        private bool TryResolveRangedAttackEndpoints(BattleHitInfo hitInfo, BattleAttackEffectProfileSO profile, out Vector2 startPosition, out Vector2 endPosition)
        {
            startPosition = Vector2.zero;
            endPosition = Vector2.zero;

            if (minimalCanvasRoot == null)
            {
                return false;
            }

            Image attackerImage;
            Image targetImage;
            bool travelsRight;

            if (hitInfo.TargetIsPlayer)
            {
                attackerImage = hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < enemyPreviewImages.Count
                    ? enemyPreviewImages[hitInfo.AttackerIndex]
                    : null;
                targetImage = hitInfo.TargetIndex >= 0 && hitInfo.TargetIndex < allyPreviewImages.Count
                    ? allyPreviewImages[hitInfo.TargetIndex]
                    : null;
                travelsRight = false;
            }
            else
            {
                attackerImage = hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < allyPreviewImages.Count
                    ? allyPreviewImages[hitInfo.AttackerIndex]
                    : null;
                targetImage = hitInfo.TargetIndex >= 0 && hitInfo.TargetIndex < enemyPreviewImages.Count
                    ? enemyPreviewImages[hitInfo.TargetIndex]
                    : null;
                travelsRight = true;
            }

            if (attackerImage == null || targetImage == null)
            {
                return false;
            }

            if (!TryGetCanvasLocalCenter(attackerImage.rectTransform, out startPosition) ||
                !TryGetCanvasLocalCenter(targetImage.rectTransform, out endPosition))
            {
                return false;
            }

            float startOffset = ResolvePreviewHalfWidth(attackerImage) * 0.32f;
            float endOffset = ResolvePreviewHalfWidth(targetImage) * 0.24f;
            if (travelsRight)
            {
                startPosition.x += startOffset;
                endPosition.x -= endOffset;
            }
            else
            {
                startPosition.x -= startOffset;
                endPosition.x += endOffset;
            }

            Vector2 spawnOffset = profile != null ? profile.spawnOffset : new Vector2(0f, 10f);
            Vector2 targetOffset = profile != null ? profile.targetOffset : new Vector2(0f, 6f);
            startPosition += spawnOffset;
            endPosition += targetOffset;
            return true;
        }

        private bool TryGetCanvasLocalCenter(RectTransform targetRect, out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            if (targetRect == null || minimalCanvasRoot == null)
            {
                return false;
            }

            RectTransform canvasRect = minimalCanvasRoot.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return false;
            }

            Vector3 worldPosition = targetRect.TransformPoint(targetRect.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPosition);
        }

        private static float ResolvePreviewHalfWidth(Image image)
        {
            if (image == null)
            {
                return 0f;
            }

            RectTransform rect = image.rectTransform;
            return rect.rect.width * Mathf.Abs(rect.lossyScale.x) * 0.5f;
        }

        private void SpawnFallbackRangedAttackEffect(BattleHitInfo hitInfo, Vector2 startPosition, Vector2 endPosition)
        {
            Color fallbackColor = ResolveFallbackRangedAttackEffectColor(ResolveAttackDamageType(hitInfo), !hitInfo.TargetIsPlayer);
            float distance = Vector2.Distance(startPosition, endPosition);
            SpawnMovingRangedAttackEffect(
                GetFallbackRangedAttackEffectSprite(),
                fallbackColor,
                startPosition,
                endPosition,
                Mathf.Lerp(0.12f, 0.22f, Mathf.Clamp01(distance / 520f)),
                Mathf.Lerp(16f, 42f, Mathf.Clamp01(distance / 480f)),
                Mathf.Lerp(22f, 34f, Mathf.Clamp01(distance / 540f)),
                0f);
        }

        private void SpawnMovingRangedAttackEffect(
            Sprite sprite,
            Color tint,
            Vector2 startPosition,
            Vector2 endPosition,
            float duration,
            float arcHeight,
            float baseSize,
            float startDelay)
        {
            if (sprite == null)
            {
                return;
            }

            Image image = CreateRangedEffectImage(sprite, tint);
            activeRangedAttackEffects.Add(new ActiveRangedAttackEffect
            {
                Image = image,
                RectTransform = image.rectTransform,
                BaseColor = tint,
                StartPosition = startPosition,
                EndPosition = endPosition,
                Duration = Mathf.Max(0.01f, duration),
                ArcHeight = arcHeight,
                BaseSize = Mathf.Max(8f, baseSize),
                StartDelay = Mathf.Max(0f, startDelay),
                UseArcMovement = true
            });
        }

        private void SpawnStaticRangedAttackEffect(
            Sprite sprite,
            Color tint,
            Vector2 position,
            float startDelay,
            float baseSize,
            float sustainDuration,
            float fadeOutScale)
        {
            if (sprite == null)
            {
                return;
            }

            Image image = CreateRangedEffectImage(sprite, tint);
            activeRangedAttackEffects.Add(new ActiveRangedAttackEffect
            {
                Image = image,
                RectTransform = image.rectTransform,
                BaseColor = tint,
                StaticPosition = position,
                Duration = Mathf.Max(0.08f, sustainDuration <= 0f ? 0.18f : sustainDuration),
                BaseSize = Mathf.Max(8f, baseSize),
                StartDelay = Mathf.Max(0f, startDelay),
                FadeOutScale = Mathf.Clamp(fadeOutScale, 0.4f, 1f),
                UseArcMovement = false
            });
        }

        private Image CreateRangedEffectImage(Sprite sprite, Color tint)
        {
            GameObject effectObject = new GameObject("RangedAttackEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = effectObject.GetComponent<RectTransform>();
            rect.SetParent(rangedEffectRoot.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = effectObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.enabled = false;
            effectObject.transform.SetAsLastSibling();
            return image;
        }

        private static float ResolveSpriteBaseSize(Sprite sprite, float scale, float fallbackSize)
        {
            if (sprite == null)
            {
                return fallbackSize * scale;
            }

            Rect rect = sprite.rect;
            float longestSide = Mathf.Max(rect.width, rect.height);
            float normalizedSize = Mathf.Lerp(36f, 110f, Mathf.Clamp01(longestSide / 512f));
            return normalizedSize * scale;
        }

        private static Color ResolveFallbackRangedAttackEffectColor(MonsterDamageType damageType, bool isPlayerSide)
        {
            if (damageType == MonsterDamageType.Magic)
            {
                return isPlayerSide
                    ? new Color(0.46f, 0.92f, 1f, 0.95f)
                    : new Color(0.86f, 0.52f, 1f, 0.95f);
            }

            return isPlayerSide
                ? new Color(1f, 0.82f, 0.34f, 0.95f)
                : new Color(1f, 0.46f, 0.34f, 0.95f);
        }

        private static Sprite GetFallbackRangedAttackEffectSprite()
        {
            if (fallbackRangedAttackEffectSprite == null)
            {
                fallbackRangedAttackEffectSprite = BuildFallbackRangedAttackEffectSprite();
            }

            return fallbackRangedAttackEffectSprite;
        }

        private static BattleAttackEffectProfileSO GetBuiltInFireProjectileEffectProfile()
        {
            if (builtInFireProjectileEffectProfile != null)
            {
                return builtInFireProjectileEffectProfile;
            }

            builtInFireProjectileEffectProfile = ScriptableObject.CreateInstance<BattleAttackEffectProfileSO>();
            builtInFireProjectileEffectProfile.hideFlags = HideFlags.HideAndDontSave;
            builtInFireProjectileEffectProfile.effectId = "builtin_fire_projectile";
            builtInFireProjectileEffectProfile.displayName = "Built-in Fire Projectile";
            builtInFireProjectileEffectProfile.pattern = BattleAttackEffectPattern.Projectile;
            builtInFireProjectileEffectProfile.projectileSprite = BattleVisualResolver.LoadSprite("BattleEffects/Fire/fx_fire_projectile_01");
            builtInFireProjectileEffectProfile.impactSprite = BattleVisualResolver.LoadSprite("BattleEffects/Fire/fx_fire_impact_01");
            builtInFireProjectileEffectProfile.hitOverlaySprite = BattleVisualResolver.LoadSprite("BattleEffects/Fire/fx_fire_hit_overlay_01");
            builtInFireProjectileEffectProfile.projectileDelay = 0f;
            builtInFireProjectileEffectProfile.impactDelay = 0.56f;
            builtInFireProjectileEffectProfile.hitOverlayDelay = 0.60f;
            builtInFireProjectileEffectProfile.loopDuration = 0.24f;
            builtInFireProjectileEffectProfile.spawnOffset = new Vector2(8f, 10f);
            builtInFireProjectileEffectProfile.targetOffset = new Vector2(0f, 6f);
            builtInFireProjectileEffectProfile.scale = 1.28f;
            builtInFireProjectileEffectProfile.projectileDuration = 0.56f;
            builtInFireProjectileEffectProfile.colorTint = Color.white;
            return builtInFireProjectileEffectProfile;
        }

        private static Sprite BuildFallbackRangedAttackEffectSprite()
        {
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "BattleFallbackRangedEffectSprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = 15.5f;
            float radius = 15.5f;
            for (int y = 0; y < 32; y += 1)
            {
                for (int x = 0; x < 32; x += 1)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / radius;
                    float alpha = Mathf.Clamp01(1f - Mathf.Pow(distance, 1.65f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f);
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
            EnsureRangedEffectRoot();
        }

        private void EnsureRangedEffectRoot()
        {
            if (minimalCanvasRoot == null)
            {
                return;
            }

            Transform existingRoot = minimalCanvasRoot.transform.Find("BattleRangedEffectRoot");
            if (existingRoot != null)
            {
                rangedEffectRoot = existingRoot.gameObject;
                return;
            }

            rangedEffectRoot = new GameObject("BattleRangedEffectRoot", typeof(RectTransform));
            RegisterSceneObjectIfEditing(rangedEffectRoot);
            RectTransform rect = rangedEffectRoot.GetComponent<RectTransform>();
            rect.SetParent(minimalCanvasRoot.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rangedEffectRoot.transform.SetAsLastSibling();
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
                ApplySkillPanelLayout();
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
            dividerRect.offsetMin = new Vector2(0f, -14f);
            dividerRect.offsetMax = new Vector2(0f, -8f);
            divider.GetComponent<Image>().color = new Color(0.62f, 0.48f, 0.24f, 0.95f);

            CreateSkillButtonPreview("SkillPreviewButton_1", skillPanelRoot.transform, 0.08f, "Strike");
            CreateSkillButtonPreview("SkillPreviewButton_2", skillPanelRoot.transform, 0.38f, "Drain");
            CreateSkillButtonPreview("SkillPreviewButton_3", skillPanelRoot.transform, 0.68f, "Guard");
            ApplySkillPanelLayout();
        }

        private static void CreateSkillButtonPreview(string objectName, Transform parent, float minX, string label)
        {
            GameObject button = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(button);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(minX, 0.12f);
            rect.anchorMax = new Vector2(minX + 0.22f, 0.74f);
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

        private void ApplySkillPanelLayout()
        {
            if (skillPanelRoot == null)
            {
                return;
            }

            RectTransform panelRect = skillPanelRoot.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0f, 0f);
                panelRect.anchorMax = new Vector2(1f, SkillPanelHeightRatio);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            RectTransform dividerRect = skillPanelRoot.transform.Find("BattleSkillPanelDivider")?.GetComponent<RectTransform>();
            if (dividerRect != null)
            {
                dividerRect.anchorMin = new Vector2(0f, 1f);
                dividerRect.anchorMax = new Vector2(1f, 1f);
                dividerRect.offsetMin = new Vector2(0f, -14f);
                dividerRect.offsetMax = new Vector2(0f, -8f);
            }

            ApplySkillButtonLayout("SkillPreviewButton_1", 0.08f);
            ApplySkillButtonLayout("SkillPreviewButton_2", 0.38f);
            ApplySkillButtonLayout("SkillPreviewButton_3", 0.68f);
        }

        private void ApplySkillButtonLayout(string objectName, float minX)
        {
            if (skillPanelRoot == null)
            {
                return;
            }

            RectTransform rect = skillPanelRoot.transform.Find(objectName)?.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(minX, 0.12f);
            rect.anchorMax = new Vector2(minX + 0.22f, 0.74f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            hudRect.anchorMin = new Vector2(0.08f, 0.835f);
            hudRect.anchorMax = new Vector2(0.92f, 0.872f);
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

        private float ResolveMeleeContactGapAnchor(float allyWidthPixels, float enemyWidthPixels)
        {
            RectTransform canvasRect = minimalCanvasRoot != null ? minimalCanvasRoot.GetComponent<RectTransform>() : null;
            float canvasWidth = canvasRect != null && canvasRect.rect.width > 0f ? canvasRect.rect.width : 1080f;
            float battlefieldWidthPixels = Mathf.Max(1f, canvasWidth * (BattlefieldMaxX - BattlefieldMinX));
            float centerDistancePixels = ((allyWidthPixels + enemyWidthPixels) * 0.5f) + MeleeContactPaddingPixels;
            return Mathf.Clamp(centerDistancePixels / battlefieldWidthPixels, 0.02f, 0.20f);
        }

        private int ResolveAllyLockedEnemyIndex(int allyIndex, BattleSimulator simulator, bool isBossWave, int activePreviewCount)
        {
            if (isBossWave)
            {
                TrackLockedTargetIndex(allyPreviewLockedEnemyIndices, allyIndex, 0);
                return 0;
            }

            if ((simulator == null || simulator.CurrentActiveEnemyCount <= 0) && activePreviewCount <= 0)
            {
                TrackLockedTargetIndex(allyPreviewLockedEnemyIndices, allyIndex, -1);
                return -1;
            }

            int candidateCount = Mathf.Clamp(
                simulator != null ? Mathf.Max(1, simulator.CurrentEngagedEnemyCount) : Mathf.Max(1, activePreviewCount),
                1,
                Mathf.Max(1, activePreviewCount));

            return ResolveLockedTargetIndex(
                allyPreviewLockedEnemyIndices,
                allyIndex,
                candidateCount,
                () => ResolveDistributedTargetIndex(allyIndex, AllyPreviewAnchors.Length, candidateCount));
        }

        private float ResolveAllyMeleeTargetXAnchor(int targetEnemyIndex, bool isBossWave, float enemyHoldOffset, float contactGap, float fallbackX)
        {
            if (targetEnemyIndex < 0)
            {
                return fallbackX;
            }

            float enemyContactX;
            if (isBossWave)
            {
                enemyContactX = BossPreviewAnchor.x + enemyHoldOffset;
            }
            else
            {
                float baseY = targetEnemyIndex < enemyPreviewBaseYAnchors.Count ? enemyPreviewBaseYAnchors[targetEnemyIndex] : 0.40f;
                float verticalOffset = targetEnemyIndex < enemyPreviewVerticalOffsets.Count ? enemyPreviewVerticalOffsets[targetEnemyIndex] : 0f;
                float contactJitter = targetEnemyIndex < enemyPreviewContactJitters.Count ? enemyPreviewContactJitters[targetEnemyIndex] : 0f;
                enemyContactX = ResolveEnemySwarmContactAnchor(baseY, verticalOffset, enemyHoldOffset, contactJitter).x;
            }

            return Mathf.Clamp(enemyContactX - contactGap, 0.10f, 0.92f);
        }

        private float ResolveAllyMeleeTargetYAnchor(float fallbackY, int targetEnemyIndex, bool isBossWave)
        {
            if (targetEnemyIndex < 0)
            {
                return fallbackY;
            }

            if (isBossWave)
            {
                return ResolveMeleeTrackedYAnchor(fallbackY, BossPreviewAnchor.y);
            }

            float baseY = targetEnemyIndex < enemyPreviewBaseYAnchors.Count ? enemyPreviewBaseYAnchors[targetEnemyIndex] : 0.40f;
            float verticalOffset = targetEnemyIndex < enemyPreviewVerticalOffsets.Count ? enemyPreviewVerticalOffsets[targetEnemyIndex] : 0f;
            float contactJitter = targetEnemyIndex < enemyPreviewContactJitters.Count ? enemyPreviewContactJitters[targetEnemyIndex] : 0f;
            float contactY = ResolveEnemySwarmContactAnchor(baseY, verticalOffset, 0f, contactJitter).y;
            return ResolveMeleeTrackedYAnchor(fallbackY, contactY);
        }

        private int ResolveEnemyLockedAllyIndex(int enemyIndex, int enemySlotCount, IReadOnlyList<bool> allySlotAlive, Vector2 referenceAnchor, IReadOnlyList<Vector2> allyAnchors, BattleSimulator simulator)
        {
            if (simulator != null)
            {
                int simulatorTargetIndex = simulator.GetEnemyTargetAllyIndex(enemyIndex);
                if (simulatorTargetIndex >= 0 && simulatorTargetIndex < allySlotAlive.Count && allySlotAlive[simulatorTargetIndex])
                {
                    TrackLockedTargetIndex(enemyPreviewLockedAllyIndices, enemyIndex, simulatorTargetIndex);
                    return simulatorTargetIndex;
                }
            }

            int resolvedAllyIndex = ResolveEnemyPreferredAllyIndex(allySlotAlive);
            if (resolvedAllyIndex < 0)
            {
                TrackLockedTargetIndex(enemyPreviewLockedAllyIndices, enemyIndex, -1);
                return -1;
            }

            while (enemyPreviewLockedAllyIndices.Count <= enemyIndex)
            {
                enemyPreviewLockedAllyIndices.Add(-1);
            }

            int lockedAllyIndex = enemyPreviewLockedAllyIndices[enemyIndex];
            if (lockedAllyIndex >= 0 && lockedAllyIndex < allySlotAlive.Count && allySlotAlive[lockedAllyIndex])
            {
                return lockedAllyIndex;
            }

            enemyPreviewLockedAllyIndices[enemyIndex] = resolvedAllyIndex;
            return resolvedAllyIndex;
        }

        private static float ResolveEnemyMeleeTargetXAnchor(float fallbackX, float contactGap, int targetAllyIndex, IReadOnlyList<Vector2> allyAnchors)
        {
            if (targetAllyIndex < 0 || targetAllyIndex >= allyAnchors.Count)
            {
                return fallbackX;
            }

            return Mathf.Clamp(allyAnchors[targetAllyIndex].x + contactGap, 0.10f, 0.92f);
        }

        private static float ResolveEnemyMeleeTargetYAnchor(float fallbackY, int targetAllyIndex, IReadOnlyList<Vector2> allyAnchors)
        {
            if (targetAllyIndex < 0 || targetAllyIndex >= allyAnchors.Count)
            {
                return fallbackY;
            }

            return ResolveMeleeTrackedYAnchor(fallbackY, allyAnchors[targetAllyIndex].y);
        }

        private static float ResolveMeleeTrackedYAnchor(float fallbackY, float targetY)
        {
            return Mathf.Clamp(
                Mathf.Lerp(fallbackY, targetY, MeleeVerticalFollowStrength),
                0.06f,
                0.94f);
        }

        private static int ResolveDistributedTargetIndex(int slotIndex, int slotCount, int targetCount)
        {
            if (targetCount <= 1 || slotCount <= 1)
            {
                return 0;
            }

            float normalized = Mathf.Clamp01((float)slotIndex / Mathf.Max(1, slotCount - 1));
            return Mathf.Clamp(Mathf.RoundToInt(normalized * (targetCount - 1)), 0, targetCount - 1);
        }

        private int ResolveEnemyPreferredAllyIndex(IReadOnlyList<bool> allySlotAlive)
        {
            int bestIndex = -1;
            float bestPriority = float.MinValue;
            for (int i = 0; i < allySlotAlive.Count; i += 1)
            {
                if (!allySlotAlive[i])
                {
                    continue;
                }

                float priority = i < AllyApproachAnchors.Length ? AllyApproachAnchors[i].x : 0f;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int ResolveLockedTargetIndex(List<int> lockedTargets, int sourceIndex, int validTargetCount, System.Func<int> fallbackResolver)
        {
            while (lockedTargets.Count <= sourceIndex)
            {
                lockedTargets.Add(-1);
            }

            int lockedIndex = lockedTargets[sourceIndex];
            if (lockedIndex >= 0 && lockedIndex < validTargetCount)
            {
                return lockedIndex;
            }

            int resolvedIndex = Mathf.Clamp(fallbackResolver(), 0, Mathf.Max(0, validTargetCount - 1));
            lockedTargets[sourceIndex] = resolvedIndex;
            return resolvedIndex;
        }

        private static void TrackLockedTargetIndex(List<int> lockedTargets, int sourceIndex, int targetIndex)
        {
            while (lockedTargets.Count <= sourceIndex)
            {
                lockedTargets.Add(-1);
            }

            lockedTargets[sourceIndex] = targetIndex;
        }

        private float ResolveSmoothedTrackedTargetY(List<float> trackedTargets, int index, float fallbackY, float desiredY)
        {
            return ResolveSmoothedTrackedTargetAnchor(trackedTargets, index, fallbackY, desiredY, MeleeVerticalMoveSpeed);
        }

        private float ResolveSmoothedTrackedTargetAnchor(List<float> trackedTargets, int index, float fallbackValue, float desiredValue, float moveSpeed)
        {
            while (trackedTargets.Count <= index)
            {
                trackedTargets.Add(fallbackValue);
            }

            float currentValue = trackedTargets[index];
            float deltaTime = Application.isPlaying ? Mathf.Max(0f, lastDeltaTime) : 0f;
            float smoothedValue = deltaTime > 0f
                ? Mathf.MoveTowards(currentValue, desiredValue, moveSpeed * deltaTime)
                : desiredValue;
            trackedTargets[index] = smoothedValue;
            return smoothedValue;
        }

        private static bool IsMonsterCloseCombat(MonsterDataSO monsterData)
        {
            return monsterData == null || monsterData.rangeType == MonsterRangeType.Melee;
        }

        private static bool IsEnemyCloseCombat(EnemyDataSO enemyData, float resolvedAttackRange)
        {
            if (enemyData == null)
            {
                return true;
            }

            return resolvedAttackRange < RangedAttackThreshold;
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
                    waveTitleText.text = "ENEMY";
                }

                if (waveEnemyCountText != null)
                {
                    waveEnemyCountText.text = "残り 5";
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
                waveTitleText.text = simulator.IsBossWave ? "BOSS" : "ENEMY";
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

                    pendingHitReactions.Clear();
                    ClearActiveRangedAttackEffects();
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

            for (int i = pendingHitReactions.Count - 1; i >= 0; i -= 1)
            {
                PendingHitReaction pending = pendingHitReactions[i];
                pending.RemainingDelay -= deltaTime;
                if (pending.RemainingDelay > 0f)
                {
                    pendingHitReactions[i] = pending;
                    continue;
                }

                ApplyHitReaction(pending.HitInfo);
                pendingHitReactions.RemoveAt(i);
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
            UpdateRangedAttackEffects(deltaTime);
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

            TrySpawnRangedAttackEffect(hitInfo);

            if (hitInfo.PresentationDelay > 0f)
            {
                pendingHitReactions.Add(new PendingHitReaction
                {
                    HitInfo = hitInfo,
                    RemainingDelay = hitInfo.PresentationDelay
                });
                return;
            }

            ApplyHitReaction(hitInfo);
        }

        private void ApplyHitReaction(BattleHitInfo hitInfo)
        {
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
            if (removalIndex < enemyPreviewTrackedTargetXAnchors.Count)
            {
                enemyPreviewTrackedTargetXAnchors.RemoveAt(removalIndex);
            }
            if (removalIndex < enemyPreviewTrackedTargetYAnchors.Count)
            {
                enemyPreviewTrackedTargetYAnchors.RemoveAt(removalIndex);
            }
            if (removalIndex < enemyPreviewLockedAllyIndices.Count)
            {
                enemyPreviewLockedAllyIndices.RemoveAt(removalIndex);
            }
            enemyKnockbackRemainings.RemoveAt(removalIndex);
            enemyAttackVisualRemainings.RemoveAt(removalIndex);
            enemyDefeatVanishRemainings.RemoveAt(removalIndex);
            AppendEnemyPreviewSlotMetadata();
        }

        private void CollectExistingAllyPreviews(Transform existingRoot)
        {
            allyPreviewImages.Clear();
            allyPreviewHpBars.Clear();
            allyPreviewTrackedTargetXAnchors.Clear();
            allyPreviewTrackedTargetYAnchors.Clear();
            allyPreviewLockedEnemyIndices.Clear();
            for (int i = 1; i <= AllyPreviewAnchors.Length; i += 1)
            {
                Image image = existingRoot.Find($"AllyMonsterPreview_{i}")?.GetComponent<Image>();
                if (image != null)
                {
                    allyPreviewImages.Add(image);
                }

                allyPreviewHpBars.Add(existingRoot.Find($"AllyMonsterHp_{i}") != null
                    ? CollectPreviewHpBar(existingRoot.Find($"AllyMonsterHp_{i}"))
                    : null);
                allyPreviewTrackedTargetXAnchors.Add(i - 1 < AllyApproachAnchors.Length ? AllyApproachAnchors[i - 1].x : 0.4f);
                allyPreviewTrackedTargetYAnchors.Add(i - 1 < AllyPreviewAnchors.Length ? AllyPreviewAnchors[i - 1].y : 0.5f);
                allyPreviewLockedEnemyIndices.Add(-1);
            }

            EnsureAllyPreviewHpBarCapacity();
            EnsureAllyPreviewEffectCapacity();
        }

        private void CollectExistingEnemyPreviews(Transform existingRoot)
        {
            enemyPreviewImages.Clear();
            enemyPreviewHpBars.Clear();
            enemyPreviewTrackedTargetXAnchors.Clear();
            enemyPreviewTrackedTargetYAnchors.Clear();
            enemyPreviewLockedAllyIndices.Clear();
            RemoveLegacyPreview(existingRoot.Find("EnemyMonsterPreview"));

            int index = 1;
            while (true)
            {
                Image image = existingRoot.Find($"EnemyMonsterPreview_{index}")?.GetComponent<Image>();
                if (image != null)
                {
                    enemyPreviewImages.Add(image);
                    enemyPreviewHpBars.Add(existingRoot.Find($"EnemyMonsterHp_{index}") != null
                        ? CollectPreviewHpBar(existingRoot.Find($"EnemyMonsterHp_{index}"))
                        : null);
                    enemyPreviewTrackedTargetXAnchors.Add(0.76f);
                    enemyPreviewTrackedTargetYAnchors.Add(0.40f);
                    enemyPreviewLockedAllyIndices.Add(-1);
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
            while (allyPreviewTrackedTargetXAnchors.Count < allyPreviewImages.Count)
            {
                int index = allyPreviewTrackedTargetXAnchors.Count;
                allyPreviewTrackedTargetXAnchors.Add(index < AllyApproachAnchors.Length ? AllyApproachAnchors[index].x : 0.4f);
            }

            while (allyPreviewTrackedTargetYAnchors.Count < allyPreviewImages.Count)
            {
                int index = allyPreviewTrackedTargetYAnchors.Count;
                allyPreviewTrackedTargetYAnchors.Add(index < AllyPreviewAnchors.Length ? AllyPreviewAnchors[index].y : 0.5f);
            }

            while (allyPreviewLockedEnemyIndices.Count < allyPreviewImages.Count)
            {
                allyPreviewLockedEnemyIndices.Add(-1);
            }

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

        private void EnsureAllyPreviewHpBarCapacity()
        {
            while (allyPreviewHpBars.Count < allyPreviewImages.Count)
            {
                int index = allyPreviewHpBars.Count + 1;
                allyPreviewHpBars.Add(CreatePreviewHpBar($"AllyMonsterHp_{index}", monsterPreviewRoot.transform));
            }

            for (int i = 0; i < allyPreviewHpBars.Count; i += 1)
            {
                if (allyPreviewHpBars[i] != null)
                {
                    continue;
                }

                allyPreviewHpBars[i] = CreatePreviewHpBar($"AllyMonsterHp_{i + 1}", monsterPreviewRoot.transform);
            }
        }

        private void EnsureEnemyPreviewCapacity(int requiredCount)
        {
            int clampedRequiredCount = Mathf.Max(0, requiredCount);
            while (enemyPreviewImages.Count < clampedRequiredCount)
            {
                int index = enemyPreviewImages.Count + 1;
                enemyPreviewImages.Add(CreatePreviewImage($"EnemyMonsterPreview_{index}", monsterPreviewRoot.transform));
                enemyPreviewHpBars.Add(CreatePreviewHpBar($"EnemyMonsterHp_{index}", monsterPreviewRoot.transform));
            }

            while (enemyPreviewSlotProgress.Count < enemyPreviewImages.Count)
            {
                AppendEnemyPreviewSlotMetadata();
            }

            while (enemyPreviewTrackedTargetYAnchors.Count < enemyPreviewImages.Count)
            {
                enemyPreviewTrackedTargetYAnchors.Add(0.40f);
            }

            while (enemyPreviewTrackedTargetXAnchors.Count < enemyPreviewImages.Count)
            {
                enemyPreviewTrackedTargetXAnchors.Add(0.76f);
            }

            while (enemyPreviewLockedAllyIndices.Count < enemyPreviewImages.Count)
            {
                enemyPreviewLockedAllyIndices.Add(-1);
            }

            while (enemyPreviewHpBars.Count < enemyPreviewImages.Count)
            {
                int index = enemyPreviewHpBars.Count + 1;
                enemyPreviewHpBars.Add(CreatePreviewHpBar($"EnemyMonsterHp_{index}", monsterPreviewRoot.transform));
            }

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                if (enemyPreviewHpBars[i] != null)
                {
                    continue;
                }

                enemyPreviewHpBars[i] = CreatePreviewHpBar($"EnemyMonsterHp_{i + 1}", monsterPreviewRoot.transform);
            }
        }

        private PreviewHpBar CollectPreviewHpBar(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            return new PreviewHpBar
            {
                Root = root.GetComponent<RectTransform>(),
                Background = root.GetComponent<Image>(),
                Fill = root.Find($"{root.name}_Fill")?.GetComponent<Image>(),
                Label = root.Find($"{root.name}_Label")?.GetComponent<Text>()
            };
        }

        private void AppendEnemyPreviewSlotMetadata()
        {
            int slotIndex = enemyPreviewSlotProgress.Count;
            enemyPreviewSlotProgress.Add(0f);
            enemyPreviewBaseYAnchors.Add(ResolveEnemyPreviewLaneY(slotIndex));
            enemyPreviewVerticalOffsets.Add(0f);
            enemyPreviewContactJitters.Add(0f);
            enemyPreviewSearchJitters.Add(0f);
            enemyPreviewSpawnXJitters.Add(0f);
            enemyPreviewTrackedTargetXAnchors.Add(0.76f);
            enemyPreviewTrackedTargetYAnchors.Add(0.40f);
            enemyPreviewLockedAllyIndices.Add(-1);
            enemyKnockbackRemainings.Add(0f);
            enemyAttackVisualRemainings.Add(0f);
            enemyDefeatVanishRemainings.Add(0f);
        }

        private static float ResolveEnemyPreviewLaneY(int slotIndex)
        {
            if (slotIndex < 0)
            {
                return EnemyPreviewLaneYAnchors[0];
            }

            return EnemyPreviewLaneYAnchors[Mathf.Clamp(slotIndex % EnemyPreviewLaneYAnchors.Length, 0, EnemyPreviewLaneYAnchors.Length - 1)];
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
