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
        private static readonly Vector2[] AllyApproachAnchors = BattleFormationLayout.AllyHomeAnchors;
        private static readonly float[] EnemyPreviewLaneYAnchors = BattleFormationLayout.EnemyLaneYAnchors;

        private static readonly string[] DevPartyOverrideIdlePaths =
        {
            "MonsterBattle/mon_dragon_whelp_idle",
            "MonsterBattle/mon_chibi_gear_idle",
            "MonsterBattle/mon_rock_golem_idle",
            "MonsterBattle/mon_apprentice_swordsman_idle",
            "MonsterBattle/mon_apprentice_mage_idle"
        };

        private static readonly string[] DevPartyOverrideMovePaths =
        {
            "MonsterBattle/mon_dragon_whelp_move",
            "MonsterBattle/mon_chibi_gear_move",
            "MonsterBattle/mon_rock_golem_move",
            "MonsterBattle/mon_apprentice_swordsman_move",
            "MonsterBattle/mon_apprentice_mage_move"
        };

        private static readonly string[] DevPartyOverrideAttackPaths =
        {
            "MonsterBattle/mon_dragon_whelp_attack",
            "MonsterBattle/mon_chibi_gear_attack",
            "MonsterBattle/mon_rock_golem_attack",
            "MonsterBattle/mon_apprentice_swordsman_attack",
            "MonsterBattle/mon_apprentice_mage_attack"
        };

        private static readonly string[] DevPartyOverrideMonsterIds =
        {
            "monster_dragon_whelp",
            "monster_chibi_gear",
            "monster_rock_golem",
            "monster_apprentice_swordsman",
            "monster_apprentice_mage"
        };

        private const string ResultPanelResourcePath = "UI/BattleResult/BattleResultPanelImage2";
        private const string DropRewardFrameResourcePath = "UI/BattleResult/BattleResultDropFrameImage2";
        private const string RecruitRewardFrameResourcePath = "UI/BattleResult/BattleResultRecruitFrameImage2";
        private static readonly Vector2 AllyPreviewSize = new Vector2(220f, 220f);
        private const int InitialEnemyPreviewSlotCapacity = 100;
        private static readonly Vector2 BossPreviewAnchor = new Vector2(0.78f, 0.43f);
        private static readonly Vector2 EnemyPreviewSpawnOffset = new Vector2(0f, 0.01f);
        private static readonly Vector2 EnemyPreviewSize = new Vector2(196f, 196f);
        private static readonly Vector2 BossPreviewSize = new Vector2(272f, 272f);
        private static readonly Dictionary<string, float> EnemyPreviewScaleOverrides = new Dictionary<string, float>
        {
            { "enemy_class1_dragon_whelp", 0.94f },
            { "enemy_class1_chibi_gear", 0.90f },
            { "enemy_class1_rock_golem", 1.05f },
            { "enemy_class1_apprentice_swordsman", 1.00f },
            { "enemy_class1_apprentice_mage", 0.96f }
        };
        private static readonly Dictionary<string, float> AllyPreviewScaleOverrides = new Dictionary<string, float>
        {
            { "monster_dragon_whelp", 0.86f },
            { "monster_chibi_gear", 0.83f },
            { "monster_rock_golem", 0.96f },
            { "monster_apprentice_swordsman", 1.00f },
            { "monster_apprentice_mage", 0.96f }
        };
        private static readonly HashSet<string> ResponsiveMeleeAttackMonsterIds = new HashSet<string>
        {
            "monster_apprentice_swordsman",
            "monster_holy_armor_leon",
            "monster_sword_saint_alvarez",
            "monster_rock_golem",
            "monster_ore_giant_garm",
            "monster_cosmic_ore_fortress_golem",
            "monster_dragon_sword_saint_agito",
            "monster_mecha_sword_saint_gransaber",
            "monster_magic_sword_saint_luciel",
            "monster_drag_gaia",
            "monster_rock_knight_gaius"
        };
        private const string MechaDragonValdrakeMonsterId = "monster_mecha_dragon_valdrake";
        private static readonly Dictionary<Sprite, BattleSpriteVisualMetrics> MageBodyVisualMetricsCache = new Dictionary<Sprite, BattleSpriteVisualMetrics>();
        private static readonly Dictionary<Sprite, BattleSpriteVisualMetrics> HumanoidWeaponBodyVisualMetricsCache = new Dictionary<Sprite, BattleSpriteVisualMetrics>();
        private static readonly Dictionary<Sprite, BattleSpriteVisualMetrics> TitaniaBodyVisualMetricsCache = new Dictionary<Sprite, BattleSpriteVisualMetrics>();
        private static readonly Dictionary<Sprite, BattleSpriteVisualMetrics> ValdrakeBodyVisualMetricsCache = new Dictionary<Sprite, BattleSpriteVisualMetrics>();
        private static readonly Dictionary<Sprite, BattleSpriteVisualMetrics> ValdrakeAttackBodyVisualMetricsCache = new Dictionary<Sprite, BattleSpriteVisualMetrics>();
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
        private const float IdleFloatAmplitude = 6f;
        private const float IdleSwayAmplitude = 2.5f;
        private const float MoveBobAmplitude = 5f;
        private const float PreviewVisualTargetHeightRatio = 0.86f;
        private const float PreviewVisualBaselineRatio = 0.43f;
        private const float PreviewVisualMaxWidthMultiplier = 2.2f;
        private const float TitaniaBodyMetricsMinX = 0.18f;
        private const float TitaniaBodyMetricsMaxX = 0.92f;
        private const float TitaniaBodyMetricsIgnoreTopRatio = 0.12f;
        private const float TitaniaBodyMetricsDenseRowRatio = 0.22f;
        private const float ValdrakeChestCoreToBodyAnchorX = 136f;
        private const float ValdrakeAttackChestCoreToBodyAnchorX = 82f;
        private const float ValdrakeAttackMovementMatchedBodyHeight = 336f;
        private const float ValdrakeBeamProjectileStartDelay = 0.30f;
        private const float AttackEffectGlobalScale = 1.28f;
        private const float MaxAttackEffectLongestSide = 260f;
        private const float MinimumBeamEndpointDistance = 48f;
        private const float ResponsiveMeleeAttackStartProgress = 0.50f;
        private const float ResponsiveMeleeEngagedLoopStartProgress = 0.25f;
        private const float Class2ResponsiveMeleeAttackStartProgress = 0.25f;
        private const float Class2ResponsiveMeleeAttackEndProgress = 0.74f;
        private const float Class2ResponsiveMeleeEngagedLoopEndProgress = 0.74f;
        private const float Class2AttackEffectScaleMultiplier = 1.00f;
        private const float Class2AttackEffectDurationMultiplier = 1.04f;
        private const float Class2AttackEffectArcMultiplier = 1.08f;
        private const float Class2AttackEffectFadeOutMultiplier = 0.98f;
        private const float Class2AttackEffectEchoDelay = 0.045f;
        private const float Class2AttackEffectEchoScaleMultiplier = 0.72f;
        private const float Class2AttackEffectSparkDelay = 0.08f;
        private const float Class2AttackEffectSparkScaleMultiplier = 0.54f;
        private const float Class2AttackEffectPulseStrength = 0.16f;
        private const float Class2AttackEffectEchoPulseStrength = 0.18f;
        private const float Class2AttackEffectSparkPulseStrength = 0.12f;
        private const float Class3AttackEffectScaleMultiplier = 0.72f;
        private const float Class3AttackEffectDurationMultiplier = 1.10f;
        private const float Class3AttackEffectArcMultiplier = 1.10f;
        private const float Class3AttackEffectFadeOutMultiplier = 0.88f;
        private const float Class3AttackEffectEchoDelay = 0.035f;
        private const float Class3AttackEffectSecondaryDelay = 0.085f;
        private const float Class3AttackEffectFinishDelay = 0.13f;
        private const float Class3AttackEffectFinishScaleMultiplier = 1.00f;
        private const string PremiumDragonAttackEffectPath = "BattleEffects/Monster/fx_abyss_dragon_attack";
        private const string PremiumImpactAttackEffectPath = "BattleEffects/Monster/fx_cosmic_ore_fortress_golem_attack";
        private const string PremiumRobotAttackEffectPath = "BattleEffects/Monster/fx_omega_leon_attack";
        private const string PremiumSwordAttackEffectPath = "BattleEffects/Monster/fx_sword_saint_alvarez_attack";
        private const string PremiumMagicAttackEffectPath = "BattleEffects/Monster/fx_abyss_grand_mage_seraphis_attack";
        private const string SeraphisOrbProjectileEffectPath = "BattleEffects/Monster/fx_abyss_grand_mage_seraphis_orb_attack";
        private const string SpiritQueenTitaniaMonsterId = "monster_spirit_queen_titania";
        private const string TitaniaStaffBeamEffectPath = "BattleEffects/Monster/fx_spirit_queen_titania_staff_beam_attack";
        private static string ImageGeneratedMonsterAttackEffectPath(string key)
        {
            return $"BattleEffects/Monster/fx_{key}_attack";
        }

        private enum MonsterAttackEffectPlacement
        {
            Projectile = 0,
            TargetBurst = 1,
            CasterBurst = 2,
            Beam = 3
        }

        private enum PreviewMeasurementMode
        {
            FullSprite = 0,
            MageBody = 1,
            ValdrakeBody = 2,
            ValdrakeAttackBody = 3,
            HumanoidWeaponBody = 4,
            TitaniaBody = 5
        }

        private struct ValdrakeRedCoreComponent
        {
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
            public int Count;
            public long SumX;
            public long SumY;

            public int Width => MaxX - MinX + 1;
            public int Height => MaxY - MinY + 1;
            public float CenterX => Count > 0 ? (float)SumX / Count : 0f;
            public float CenterY => Count > 0 ? (float)SumY / Count : 0f;
        }

        private struct ValdrakeBodySpan
        {
            public int MinY;
            public int MaxY;

            public float Height => MaxY - MinY + 1f;
        }

        private sealed class MonsterAttackEffectDefinition
        {
            public string ResourcePath;
            public MonsterAttackEffectPlacement Placement = MonsterAttackEffectPlacement.Projectile;
            public float Scale = 1f;
            public float Duration = 0.32f;
            public float StartDelay;
            public Vector2 StartOffset = new Vector2(26f, 10f);
            public Vector2 TargetOffset = Vector2.zero;
            public float ArcHeight = 6f;
            public float FadeOutScale = 0.74f;
            public Color Tint = Color.white;
            public float BeamThickness;
            public float BeamLengthPadding = 18f;
        }

        private static readonly Dictionary<string, MonsterAttackEffectDefinition> MonsterAttackEffects = new Dictionary<string, MonsterAttackEffectDefinition>
        {
            {
                "monster_dragon_whelp",
                new MonsterAttackEffectDefinition
                {
                    ResourcePath = PremiumDragonAttackEffectPath,
                    Scale = 1.20f,
                    Duration = 0.34f,
                    StartOffset = new Vector2(22f, 8f),
                    TargetOffset = new Vector2(8f, 12f),
                    ArcHeight = 6f,
                    FadeOutScale = 1.10f
                }
            },
            {
                "monster_flare_drake",
                new MonsterAttackEffectDefinition
                {
                    ResourcePath = PremiumDragonAttackEffectPath,
                    Scale = 1.20f,
                    Duration = 0.42f,
                    StartOffset = new Vector2(30f, 12f),
                    TargetOffset = new Vector2(13f, 16f),
                    ArcHeight = 8f,
                    FadeOutScale = 1.17f
                }
            },
            {
                "monster_abyss_dragon",
                new MonsterAttackEffectDefinition
                {
                    ResourcePath = PremiumDragonAttackEffectPath,
                    Scale = 1.64f,
                    Duration = 0.58f,
                    StartOffset = new Vector2(36f, 15f),
                    TargetOffset = new Vector2(20f, 24f),
                    ArcHeight = 14f,
                    FadeOutScale = 1.38f
                }
            },
            { "monster_chibi_gear", PunchImpactEffect(PremiumRobotAttackEffectPath, 0.78f, 0.22f, -16f, 0f, 1.02f) },
            { "monster_armed_droid", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.62f, 0.22f, -18f, 0f, 1.00f) },
            { "monster_omega_leon", PunchImpactEffect(PremiumRobotAttackEffectPath, 1.42f, 0.32f, -22f, 8f, 1.26f) },
            { "monster_rock_golem", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.70f, 0.24f, -18f, -2f, 1.02f) },
            { "monster_ore_giant_garm", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.82f, 0.30f, -22f, 0f, 1.12f) },
            { "monster_cosmic_ore_fortress_golem", PunchImpactEffect(PremiumImpactAttackEffectPath, 1.20f, 0.40f, -26f, 6f, 1.34f) },
            { "monster_apprentice_swordsman", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.66f, 0.24f, -10f, 6f, 1.04f) },
            { "monster_holy_armor_leon", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.76f, 0.28f, -8f, 8f, 1.10f) },
            { "monster_sword_saint_alvarez", SwordSlashEffect(PremiumSwordAttackEffectPath, 1.00f, 0.32f, -8f, 12f, 1.24f) },
            { "monster_apprentice_mage", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.58f, 0.30f, 0f, 18f, 1.06f) },
            { "monster_dark_robe_curse_mage_noah", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.70f, 0.38f, 0f, 22f, 1.14f) },
            { "monster_abyss_grand_mage_seraphis", ProjectileEffect(SeraphisOrbProjectileEffectPath, 1.08f, 0.48f, 38f, 22f, 12f, 1.18f) },
            { "monster_mecha_dragon_valdrake", ProjectileEffect(ImageGeneratedMonsterAttackEffectPath("mecha_dragon_valdrake"), 0.96f, 0.30f, 34f, 16f, 8f, 1.18f, ValdrakeBeamProjectileStartDelay) },
            { "monster_drag_gaia", PunchImpactEffect(ImageGeneratedMonsterAttackEffectPath("drag_gaia"), 1.04f, 0.36f, -26f, 6f, 1.22f) },
            { "monster_dragon_sword_saint_agito", SwordSlashEffect(ImageGeneratedMonsterAttackEffectPath("dragon_sword_saint_agito"), 0.92f, 0.30f, -10f, 12f, 1.16f) },
            { "monster_abyss_dragon_mage_valflare", ProjectileEffect(ImageGeneratedMonsterAttackEffectPath("abyss_dragon_mage_valflare"), 1.02f, 0.50f, 38f, 24f, 18f, 1.26f) },
            { "monster_fortress_machine_gigafort", ProjectileEffect(ImageGeneratedMonsterAttackEffectPath("fortress_machine_gigafort"), 0.92f, 0.34f, 36f, 10f, 4f, 1.18f) },
            { "monster_mecha_sword_saint_gransaber", SwordSlashEffect(ImageGeneratedMonsterAttackEffectPath("mecha_sword_saint_gransaber"), 0.90f, 0.28f, -8f, 10f, 1.14f) },
            { "monster_dark_magic_machine_god_merchion", TargetBurstEffect(ImageGeneratedMonsterAttackEffectPath("dark_magic_machine_god_merchion"), 0.92f, 0.44f, 0f, 22f, 1.26f) },
            { "monster_rock_knight_gaius", PunchImpactEffect(ImageGeneratedMonsterAttackEffectPath("rock_knight_gaius"), 0.94f, 0.34f, -24f, 4f, 1.18f) },
            { "monster_astral_eclipse_golem", TargetBurstEffect(ImageGeneratedMonsterAttackEffectPath("astral_eclipse_golem"), 0.98f, 0.44f, 0f, 20f, 1.24f) },
            { "monster_magic_sword_saint_luciel", SwordSlashEffect(ImageGeneratedMonsterAttackEffectPath("magic_sword_saint_luciel"), 0.94f, 0.32f, -8f, 12f, 1.18f) },
            { "monster_seraph_michael", TargetBurstEffect(ImageGeneratedMonsterAttackEffectPath("seraph_michael"), 0.88f, 0.40f, 0f, 28f, 1.22f) },
            { SpiritQueenTitaniaMonsterId, SustainedBeamEffect(TitaniaStaffBeamEffectPath, 82f, 0.48f, 86f, 26f, 10f, 22f, 24f) },
            { "monster_worm", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.62f, 0.22f, -16f, -2f, 1.00f) },
            { "monster_bat", ProjectileEffect(PremiumDragonAttackEffectPath, 0.74f, 0.30f, 22f, 8f, 8f, 1.08f) },
            { "monster_goblin", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.58f, 0.22f, -10f, 4f, 1.00f) },
            { "monster_wraith", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.62f, 0.32f, 0f, 18f, 1.08f) },
            { "monster_bee", ProjectileEffect(PremiumDragonAttackEffectPath, 0.62f, 0.26f, 22f, 8f, 8f, 1.04f) },
            { "monster_naga", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.66f, 0.24f, -18f, 0f, 1.04f) },
            { "monster_centaur", ProjectileEffect(PremiumSwordAttackEffectPath, 0.72f, 0.30f, 20f, 10f, 6f, 1.08f) },
            { "monster_death_mage_elf", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.72f, 0.36f, 0f, 22f, 1.12f) },
            { "monster_hell_knight", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.82f, 0.30f, -10f, 8f, 1.14f) },
            { "monster_shadow", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.60f, 0.28f, 0f, 16f, 1.04f) },
            { "monster_dragoon", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.82f, 0.30f, -10f, 10f, 1.14f) },
            { "monster_ghost", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.60f, 0.28f, 0f, 18f, 1.04f) },
            { "monster_naga_mage", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.70f, 0.34f, 0f, 22f, 1.12f) },
            { "monster_soul_eater", TargetBurstEffect(PremiumMagicAttackEffectPath, 0.84f, 0.38f, 0f, 24f, 1.18f) },
            { "monster_spectral_warrior", SwordSlashEffect(PremiumSwordAttackEffectPath, 0.76f, 0.28f, -10f, 8f, 1.10f) },
            { "monster_vault_guard", PunchImpactEffect(PremiumImpactAttackEffectPath, 0.86f, 0.32f, -22f, 4f, 1.16f) }
        };

        private static MonsterAttackEffectDefinition ProjectileEffect(
            string resourcePath,
            float scale,
            float duration,
            float startX,
            float startY,
            float arcHeight,
            float fadeOutScale,
            float startDelay = 0f)
        {
            return new MonsterAttackEffectDefinition
            {
                ResourcePath = resourcePath,
                Placement = MonsterAttackEffectPlacement.Projectile,
                Scale = scale,
                Duration = duration,
                StartDelay = startDelay,
                StartOffset = new Vector2(startX, startY),
                TargetOffset = new Vector2(8f, startY * 0.55f),
                ArcHeight = arcHeight,
                FadeOutScale = fadeOutScale
            };
        }

        private static MonsterAttackEffectDefinition TargetBurstEffect(
            string resourcePath,
            float scale,
            float duration,
            float targetX,
            float targetY,
            float fadeOutScale)
        {
            return new MonsterAttackEffectDefinition
            {
                ResourcePath = resourcePath,
                Placement = MonsterAttackEffectPlacement.TargetBurst,
                Scale = scale,
                Duration = duration,
                TargetOffset = new Vector2(targetX, targetY),
                ArcHeight = 0f,
                FadeOutScale = fadeOutScale
            };
        }

        private static MonsterAttackEffectDefinition SustainedBeamEffect(
            string resourcePath,
            float thickness,
            float duration,
            float startX,
            float startY,
            float targetX,
            float targetY,
            float lengthPadding,
            float startDelay = 0f)
        {
            return new MonsterAttackEffectDefinition
            {
                ResourcePath = resourcePath,
                Placement = MonsterAttackEffectPlacement.Beam,
                Scale = 1f,
                Duration = duration,
                StartDelay = startDelay,
                StartOffset = new Vector2(startX, startY),
                TargetOffset = new Vector2(targetX, targetY),
                ArcHeight = 0f,
                FadeOutScale = 1f,
                BeamThickness = thickness,
                BeamLengthPadding = lengthPadding
            };
        }

        private static MonsterAttackEffectDefinition SwordSlashEffect(
            string resourcePath,
            float scale,
            float duration,
            float targetX,
            float targetY,
            float fadeOutScale)
        {
            return TargetBurstEffect(resourcePath, scale, duration, targetX, targetY, fadeOutScale);
        }

        private static MonsterAttackEffectDefinition PunchImpactEffect(
            string resourcePath,
            float scale,
            float duration,
            float targetX,
            float targetY,
            float fadeOutScale)
        {
            return TargetBurstEffect(resourcePath, scale, duration, targetX, targetY, fadeOutScale);
        }

        private static readonly string[] MinimalHiddenObjectNames =
        {
            "BattleMinimalResultOverlay",
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

        private static readonly HashSet<string> MinimalResultObjectNames = new HashSet<string>
        {
            "BattleMinimalResultOverlay",
            "ResultPanel",
            "RewardStrip",
            "RewardStripFrame",
            "RewardStripLabel",
            "NextMoveStrip",
            "NextMoveStripFrame",
            "NextActionText",
            "ReturnHomeButton",
            "NextFloorButton",
            "NextFloorButtonFrame",
            "NextFloorAura",
            "NextFloorAuraTag",
            "NextFloorAuraTagText",
            "NextFloorButtonAccentLeft",
            "NextFloorButtonAccentRight"
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
            public float AlphaMultiplier = 1f;
            public float PulseStrength = -1f;
            public float GlowStrength = 0.34f;
            public bool UseArcMovement;
            public bool UseBeamLayout;
            public float BeamLengthPadding;
            public List<Sprite> Frames;
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
        private BattleResultViewData lastResultViewData;
        private bool hasLastResultViewData;
        private string lastEquipmentDropSummary;
        private string lastRelicDropSummary;
        private readonly List<BattleResultRewardVisual> lastRewardVisuals = new List<BattleResultRewardVisual>();
        private int lastPartyMonsterExpTargetCount;
        private int lastPlayerLevelBeforeReward;
        private int lastPlayerLevelAfterReward;
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
        private static readonly System.Random DropRandom = new System.Random();
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
        private GameObject minimalResultOverlayRoot;
        private Text minimalResultTitleText;
        private Text minimalResultSummaryText;
        private Text minimalResultRewardText;
        private Text minimalResultForecastText;
        private GameObject minimalResultRewardVisualRoot;
        private Button minimalResultNextFloorButton;
        private Button minimalResultRetryFloorButton;
        private Button minimalResultHomeButton;
        private Text minimalResultNextFloorButtonText;
        private Text minimalResultRetryFloorButtonText;
        private Text minimalResultHomeButtonText;
        private readonly List<GameObject> minimalResultRewardVisualObjects = new List<GameObject>();
        private int lastEncounterSerial = -1;
        private int lastPresentedWave = -1;
        private bool isApplyingEditorPreview;
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
        private const float KnockbackDuration = 0.16f;
        private const float AttackVisualDuration = 0.62f;
        private const float AllyKnockbackDistance = 0.016f;
        private const float EnemyKnockbackDistance = 0.028f;
        private const float AllyDefeatVanishDuration = 0.28f;
        private const float EnemyDefeatVanishDuration = 0.18f;
        private static readonly Vector2 PreviewHpBarSize = new Vector2(52f, 8f);

        public int DebugUpdateCount => updateCount;
        public float DebugLastDeltaTime => lastDeltaTime;

        public string GetDebugAllyPreviewSpriteName(int index)
        {
            Image image = index >= 0 && index < allyPreviewImages.Count ? allyPreviewImages[index] : null;
            return image != null && image.sprite != null ? image.sprite.name : string.Empty;
        }

        public string GetDebugEnemyPreviewSpriteName(int index)
        {
            Image image = index >= 0 && index < enemyPreviewImages.Count ? enemyPreviewImages[index] : null;
            return image != null && image.sprite != null ? image.sprite.name : string.Empty;
        }

        public string GetDebugAllyPreviewPoseName(int index)
        {
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            if (simulator == null || index < 0 || index >= allyPreviewImages.Count)
            {
                return string.Empty;
            }

            bool allyAlive = simulator.HasAllyRuntime(index) && simulator.IsAllyAlive(index);
            float allyApproachT = allyAlive && simulator.IsAllyMoving(index) ? 0f : 1f;
            bool isAttackEngaged = allyAlive && simulator.IsAllyAttackEngaged(index);
            return ResolveAllyPreviewPose(index, allyApproachT, isAttackEngaged).ToString();
        }

        public string GetDebugEnemyPreviewPoseName(int index)
        {
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            if (simulator == null || index < 0 || index >= enemyPreviewImages.Count)
            {
                return string.Empty;
            }

            return ResolveEnemyPreviewPose(
                index,
                simulator.IsEnemyMoving(index),
                simulator.IsEnemyAttackEngaged(index)).ToString();
        }

        public int GetDebugAllyIdleSpriteCount(int index)
        {
            List<Sprite> frames = index >= 0 && index < allyIdleSprites.Count ? allyIdleSprites[index] : null;
            return frames != null ? frames.Count : 0;
        }

        public int GetDebugAllyMoveSpriteCount(int index)
        {
            List<Sprite> frames = index >= 0 && index < allyMoveSprites.Count ? allyMoveSprites[index] : null;
            return frames != null ? frames.Count : 0;
        }

        public int GetDebugAllyAttackSpriteCount(int index)
        {
            List<Sprite> frames = index >= 0 && index < allyAttackSprites.Count ? allyAttackSprites[index] : null;
            return frames != null ? frames.Count : 0;
        }

        public int GetDebugEnemyAttackSpriteCount()
        {
            return enemyAttackSprites != null ? enemyAttackSprites.Count : 0;
        }

        public float GetDebugAllyAttackVisualRemaining(int index)
        {
            return index >= 0 && index < allyAttackVisualRemainings.Count
                ? Mathf.Max(0f, allyAttackVisualRemainings[index])
                : 0f;
        }

        public float GetDebugEnemyAttackVisualRemaining(int index)
        {
            return index >= 0 && index < enemyAttackVisualRemainings.Count
                ? Mathf.Max(0f, enemyAttackVisualRemainings[index])
                : 0f;
        }

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

        private void Start()
        {
            NormalizeCanvasScales();
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
            int clearedFloor = currentFloor;
            GameManager.Instance.RecordFloorClear(currentFloor);
            var profile = GameManager.Instance.PlayerProfile;
            MissionService.RecordBattleWin(profile);
            DailyRewardService.RecordBattleWin(profile, System.DateTime.Now);
            MissionService.RecordHighestFloor(profile, profile.HighestFloor);
            SaveManager.Instance?.SaveAfterDungeonStageClear(currentFloor);
            var resultViewData = new BattleResultViewData(
                true,
                lastReward.Gold,
                lastReward.Exp,
                lastReward.Exp,
                lastPartyMonsterExpTargetCount,
                lastPlayerLevelBeforeReward,
                lastPlayerLevelAfterReward,
                clearedFloor,
                GameManager.Instance.CurrentFloor,
                BuildItemDropSummary(lastEquipmentDropSummary, lastRelicDropSummary),
                lastRecruitResult.Summary,
                lastRewardVisuals.ToArray());
            lastResultViewData = resultViewData;
            hasLastResultViewData = true;
            stateMachine.ShowResultPanel(resultViewData);
            ShowMinimalResultOverlay(resultViewData);
        }

        public void OnBattleLose()
        {
            SaveManager.Instance.SaveCurrentGame();
            var resultViewData = new BattleResultViewData(false, 0, 0, currentFloor, string.Empty);
            lastResultViewData = resultViewData;
            hasLastResultViewData = true;
            stateMachine.ShowResultPanel(resultViewData);
            ShowMinimalResultOverlay(resultViewData);
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

        public void RetryClearedFloor()
        {
            int retryFloor = hasLastResultViewData
                ? Mathf.Max(1, lastResultViewData.ClearedFloor)
                : Mathf.Max(1, currentFloor);

            GameManager.Instance.SetCurrentFloor(retryFloor);
            currentFloor = retryFloor;
            PrepareBattleSession();
            stateMachine.Begin(currentFloor);
            ApplyMinimalPresentation();
            RefreshBattlePresentation(force: true);
        }

        public void ReturnHome()
        {
            SceneManager.LoadScene(homeSceneName);
        }

#if UNITY_EDITOR
        public void ShowDebugRewardResult()
        {
            EnsureInitialized();
            int previewFloor = Mathf.Max(1, currentFloor);
            var rewardVisuals = new[]
            {
                new BattleResultRewardVisual(
                    "青銅の刃",
                    "コモン",
                    "EquipmentIcons/eq_bronze_blade_icon",
                    DropRewardFrameResourcePath,
                    false),
                new BattleResultRewardVisual(
                    "通常遺物",
                    "x1",
                    "EquipmentRelics/relic_safe_ember_icon",
                    DropRewardFrameResourcePath,
                    false),
                new BattleResultRewardVisual(
                    "ヒナドラ",
                    "仲間になりました",
                    "FamilyMonsterCards/Dragon/dragon_whelp",
                    RecruitRewardFrameResourcePath,
                    true)
            };

            var resultViewData = new BattleResultViewData(
                true,
                120,
                48,
                48,
                3,
                4,
                5,
                previewFloor,
                previewFloor + 1,
                "装備入手: 青銅の刃[コモン]\n強化遺物入手: 通常遺物 x1",
                "ヒナドラ が仲間になりました。",
                rewardVisuals);

            lastResultViewData = resultViewData;
            hasLastResultViewData = true;
            resultHandled = true;
            stateMachine.ShowResultPanel(resultViewData);
            ShowMinimalResultOverlay(resultViewData);
        }
#endif

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
            lastPlayerLevelBeforeReward = profile.Level;
            profile.AddGold(reward.Gold);
            profile.AddExp(reward.Exp);
            lastPlayerLevelAfterReward = profile.Level;
            lastPartyMonsterExpTargetCount = ApplyPartyMonsterExp(profile, reward.Exp);
            ApplyEquipmentDrop(profile);
            ApplyRelicDrop(profile);
            lastReward = reward;
        }

        private void ApplyEquipmentDrop(PlayerProfile profile)
        {
            lastEquipmentDropSummary = string.Empty;
            if (profile == null || !StageDropService.TryRollEquipmentDrop(IsCurrentBossEncounter(), DropRandom))
            {
                return;
            }

            EquipmentDataSO equipmentData = RollEquipmentDropData(DropRandom);
            if (equipmentData == null || string.IsNullOrEmpty(equipmentData.equipmentId))
            {
                return;
            }

            EquipmentRarity quality = StageDropService.RollEquipmentQuality(DropRandom);
            if (!profile.AddOwnedEquipment(equipmentData.equipmentId, quality))
            {
                return;
            }

            string qualityName = EquipmentEnhancementCatalog.ResolveQualityName(quality);
            lastEquipmentDropSummary = $"装備入手: {equipmentData.equipmentName}[{qualityName}]";
            lastRewardVisuals.Add(new BattleResultRewardVisual(
                equipmentData.equipmentName,
                qualityName,
                ResolveEquipmentIconResourcePath(equipmentData.equipmentId),
                DropRewardFrameResourcePath,
                false));
        }

        private void ApplyRelicDrop(PlayerProfile profile)
        {
            lastRelicDropSummary = string.Empty;
            if (profile == null || !StageDropService.TryRollEnhancementRelic(DropRandom, out string relicId))
            {
                return;
            }

            profile.AddEnhancementRelics(relicId, 1);
            EnhancementRelicDefinition relicDefinition = EquipmentEnhancementCatalog.GetRelic(relicId);
            string relicName = relicDefinition != null && !string.IsNullOrEmpty(relicDefinition.RelicName)
                ? relicDefinition.RelicName
                : relicId;
            lastRelicDropSummary = $"強化遺物入手: {relicName} x1";
            lastRewardVisuals.Add(new BattleResultRewardVisual(
                relicName,
                "x1",
                ResolveEnhancementRelicResourcePath(relicId),
                DropRewardFrameResourcePath,
                false));
        }

        private EquipmentDataSO RollEquipmentDropData(System.Random random)
        {
            EquipmentDataSO[] allEquipment = MasterDataManager.Instance?.GetAllEquipmentData();
            if (allEquipment == null || allEquipment.Length <= 0)
            {
                return null;
            }

            List<EquipmentDataSO> candidates = new List<EquipmentDataSO>();
            for (int i = 0; i < allEquipment.Length; i += 1)
            {
                if (allEquipment[i] != null && !string.IsNullOrEmpty(allEquipment[i].equipmentId))
                {
                    candidates.Add(allEquipment[i]);
                }
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            System.Random rng = random ?? DropRandom;
            return candidates[rng.Next(candidates.Count)];
        }

        private bool IsCurrentBossEncounter()
        {
            if (stateMachine != null && stateMachine.Simulator != null)
            {
                return stateMachine.Simulator.IsBossWave;
            }

            return bossFloorInterval > 0 && currentFloor > 0 && currentFloor % bossFloorInterval == 0;
        }

        private int ApplyPartyMonsterExp(PlayerProfile profile, int exp)
        {
            if (profile == null || exp <= 0)
            {
                return 0;
            }

            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsters(profile, 5);
            int appliedCount = 0;
            foreach (OwnedMonsterData monster in partyMonsters)
            {
                if (monster == null)
                {
                    continue;
                }

                MonsterDataSO monsterData = masterDataManager != null
                    ? masterDataManager.GetMonsterData(monster.MonsterId)
                    : null;
                MonsterLevelService.AddExperience(monster, monsterData, exp);
                appliedCount += 1;
            }

            return appliedCount;
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
            if (lastRecruitResult.Succeeded)
            {
                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(lastRecruitResult.MonsterId);
                lastRewardVisuals.Add(new BattleResultRewardVisual(
                    lastRecruitResult.MonsterName,
                    "仲間になりました",
                    ResolveMonsterRewardIconResourcePath(monsterData),
                    RecruitRewardFrameResourcePath,
                    true));
            }
        }

        private static int CountResolvedPartyMonsters(List<OwnedMonsterData> partyMonsters)
        {
            if (partyMonsters == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < partyMonsters.Count; i += 1)
            {
                if (partyMonsters[i] != null)
                {
                    count += 1;
                }
            }

            return count;
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
            lastEquipmentDropSummary = string.Empty;
            lastRelicDropSummary = string.Empty;
            lastRewardVisuals.Clear();
            lastPartyMonsterExpTargetCount = 0;
            lastPlayerLevelBeforeReward = 0;
            lastPlayerLevelAfterReward = 0;
            hasLastResultViewData = false;
            recruitEnabledAtBattleStart = MonsterRecruitService.CanAttemptRecruitThisBattle(GameManager.Instance?.PlayerProfile);
            HideMinimalResultOverlay();
        }

        private static string BuildItemDropSummary(string equipmentDropSummary, string relicDropSummary)
        {
            if (string.IsNullOrEmpty(equipmentDropSummary))
            {
                return relicDropSummary ?? string.Empty;
            }

            if (string.IsNullOrEmpty(relicDropSummary))
            {
                return equipmentDropSummary;
            }

            return equipmentDropSummary + "\n" + relicDropSummary;
        }

        private static string ResolveEquipmentIconResourcePath(string equipmentId)
        {
            switch (equipmentId)
            {
                case "equip_bronze_blade":
                    return "EquipmentIcons/eq_bronze_blade_icon";
                case "equip_iron_sword":
                case "equip_iron_saber":
                    return "EquipmentIcons/eq_iron_blade_icon";
                case "equip_gold_blade":
                    return "EquipmentIcons/eq_gold_blade_icon";
                case "equip_frost_greatsword":
                    return "EquipmentIcons/eq_frost_greatsword_icon";
                case "equip_c1_arcane_wand":
                    return "EquipmentIcons/ClassMagic/equip_c1_arcane_wand_icon";
                case "equip_c2_runic_staff":
                    return "EquipmentIcons/ClassMagic/equip_c2_runic_staff_icon";
                case "equip_c3_astral_scepter":
                    return "EquipmentIcons/ClassMagic/equip_c3_astral_scepter_icon";
                case "equip_c4_abyss_grimoire":
                    return "EquipmentIcons/ClassMagic/equip_c4_abyss_grimoire_icon";
                case "equip_guard_cloth":
                    return "EquipmentIcons/eq_cloth_armor_icon";
                case "equip_c1_spellguard_robe":
                    return "EquipmentIcons/ClassMagic/equip_c1_spellguard_robe_icon";
                case "equip_bone_mail":
                case "equip_bastion_mail":
                    return "EquipmentIcons/eq_plate_armor_icon";
                case "equip_leather_armor":
                    return "EquipmentIcons/eq_leather_armor_icon";
                case "equip_c2_sage_mantle":
                    return "EquipmentIcons/ClassMagic/equip_c2_sage_mantle_icon";
                case "equip_ice_dragon_armor":
                    return "EquipmentIcons/eq_ice_dragon_armor_icon";
                case "equip_c3_aurora_robe":
                    return "EquipmentIcons/ClassMagic/equip_c3_aurora_robe_icon";
                case "equip_c4_voidweave_raiment":
                    return "EquipmentIcons/ClassMagic/equip_c4_voidweave_raiment_icon";
                case "equip_ashen_ring":
                    return "EquipmentIcons/eq_red_ring_icon";
                case "equip_sage_ring":
                case "equip_green_ring":
                    return "EquipmentIcons/eq_green_ring_icon";
                case "equip_c2_runic_ring":
                    return "EquipmentIcons/ClassMagic/equip_c2_runic_ring_icon";
                case "equip_quick_charm":
                case "equip_moon_charm":
                case "equip_apprentice_charm":
                case "equip_barrier_talisman":
                    return "EquipmentIcons/eq_violet_pendant_icon";
                case "equip_c1_mana_brooch":
                    return "EquipmentIcons/ClassMagic/equip_c1_mana_brooch_icon";
                case "equip_ice_star_talisman":
                case "equip_oracle_orb":
                    return "EquipmentIcons/eq_ice_star_talisman_icon";
                case "equip_c3_starseer_charm":
                    return "EquipmentIcons/ClassMagic/equip_c3_starseer_charm_icon";
                case "equip_c4_eclipse_core":
                    return "EquipmentIcons/ClassMagic/equip_c4_eclipse_core_icon";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveEnhancementRelicResourcePath(string relicId)
        {
            switch (relicId)
            {
                case "relic_safe_ember":
                    return "EquipmentRelics/relic_safe_ember_icon";
                case "relic_risky_ember":
                    return "EquipmentRelics/relic_risky_ember_icon";
                case "relic_volatile_ember":
                    return "EquipmentRelics/relic_volatile_ember_icon";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveMonsterRewardIconResourcePath(MonsterDataSO monsterData)
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

            if (!string.IsNullOrEmpty(monsterData.battleIdleResourcePath))
            {
                return ResolveExistingSpriteResourcePath(monsterData.battleIdleResourcePath);
            }

            return string.Empty;
        }

        private static string ResolveExistingSpriteResourcePath(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return string.Empty;
            }

            if (HasSpriteResource(resourcePath))
            {
                return resourcePath;
            }

            string firstFramePath = resourcePath + "_0";
            return HasSpriteResource(firstFramePath) ? firstFramePath : resourcePath;
        }

        private static bool HasSpriteResource(string resourcePath)
        {
            return !string.IsNullOrEmpty(resourcePath) &&
                   (Resources.Load<Sprite>(resourcePath) != null ||
                    Resources.Load<Texture2D>(resourcePath) != null ||
                    Resources.LoadAll<Sprite>(resourcePath).Length > 0);
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
            List<OwnedMonsterData> partyMonsters = BattleVisualResolver.ResolvePartyOwnedMonsterSlots(profile, 5);
            int partyMonsterCount = CountResolvedPartyMonsters(partyMonsters);
            MonsterDataSO playerMonsterData = BattleVisualResolver.ResolvePlayerMonsterData(profile);
            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            EnemyDataSO enemyData = simulator != null
                ? simulator.CurrentEnemyData
                : null;
            if (enemyData == null)
            {
                enemyData = BattleDungeonCatalog.CreateEnemyDataForGlobalFloor(floor, MasterDataManager.Instance)
                    ?? MasterDataManager.Instance?.GetFloorData(floor)?.enemyData;
            }

            EnsureMonsterPreviewRoot();
            allyIdleSprites.Clear();
            allyMoveSprites.Clear();
            allyAttackSprites.Clear();
            allyPreviewMonsterData.Clear();
            allyAttackRanges.Clear();
            allySearchRanges.Clear();
            bool useDebugPartyOverrides = partyMonsterCount <= 0;
            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                MonsterDataSO partyData = null;
                if (!useDebugPartyOverrides && i < partyMonsters.Count && partyMonsters[i] != null)
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
                playerHint.text = partyMonsterCount > 0
                    ? $"単一フィールド / 出撃{partyMonsterCount}体"
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
                if (resultHandled && MinimalResultObjectNames.Contains(objectName))
                {
                    continue;
                }

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
            isApplyingEditorPreview = true;
            try
            {
                NormalizeCanvasScales();
                ApplyBackdropForFloor(1);
                ApplyMinimalPresentation();
                ApplyEditorCombatantPreview();
            }
            finally
            {
                isApplyingEditorPreview = false;
            }
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
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_dragon_whelp")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_chibi_gear")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_rock_golem")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_apprentice_swordsman")));
            allyAttackRanges.Add(BattleAttackRangeResolver.ResolveMonsterAttackRange(MasterDataManager.Instance?.GetMonsterData("monster_apprentice_mage")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_dragon_whelp")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_chibi_gear")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_rock_golem")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_apprentice_swordsman")));
            allySearchRanges.Add(BattleAttackRangeResolver.ResolveMonsterSearchRange(MasterDataManager.Instance?.GetMonsterData("monster_apprentice_mage")));
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

            string dungeonBackdropPath = BattleDungeonCatalog.ResolveBattleBackdropResourcePath(floor);
            if (!string.IsNullOrEmpty(dungeonBackdropPath))
            {
                return dungeonBackdropPath;
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

        private Sprite SelectAllyPreviewSprite(int index, float allyApproachT, bool isAttackEngaged)
        {
            MonsterDataSO allyData = index >= 0 && index < allyPreviewMonsterData.Count
                ? allyPreviewMonsterData[index]
                : null;
            if (index >= 0 && index < allyAttackVisualRemainings.Count && allyAttackVisualRemainings[index] > 0f)
            {
                List<Sprite> attackSprites = index < allyAttackSprites.Count ? allyAttackSprites[index] : null;
                Sprite attackSprite = SelectAttackFrame(
                    attackSprites,
                    allyAttackVisualRemainings[index],
                    index * 0.17f,
                    ResolveResponsiveAttackStartProgress(allyData),
                    ResolveResponsiveAttackEndProgress(allyData));
                if (attackSprite != null)
                {
                    return attackSprite;
                }
            }

            if (isAttackEngaged)
            {
                List<Sprite> attackSprites = index < allyAttackSprites.Count ? allyAttackSprites[index] : null;
                Sprite engagedSprite = SelectAnimatedAttackFrame(
                    attackSprites,
                    6f,
                    index * 0.17f,
                    ResolveResponsiveEngagedLoopStartProgress(allyData),
                    ResolveResponsiveEngagedLoopEndProgress(allyData));
                if (engagedSprite != null)
                {
                    return engagedSprite;
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

        private BattleVisualPose ResolveAllyPreviewPose(int index, float allyApproachT, bool isAttackEngaged)
        {
            if (index >= 0 && index < allyAttackVisualRemainings.Count && allyAttackVisualRemainings[index] > 0f)
            {
                return BattleVisualPose.Attack;
            }

            if (isAttackEngaged)
            {
                return BattleVisualPose.Attack;
            }

            if (allyApproachT < 1f)
            {
                return BattleVisualPose.Move;
            }

            return BattleVisualPose.Idle;
        }

        private Sprite SelectEnemyPreviewSprite(int index, bool isMoving, bool isAttackEngaged)
        {
            if (index >= 0 && index < enemyAttackVisualRemainings.Count && enemyAttackVisualRemainings[index] > 0f)
            {
                Sprite attackSprite = SelectAttackFrame(
                    enemyAttackSprites,
                    enemyAttackVisualRemainings[index],
                    index * 0.11f,
                    ResolveResponsiveAttackStartProgress(currentPreviewEnemyData),
                    ResolveResponsiveAttackEndProgress(currentPreviewEnemyData));
                if (attackSprite != null)
                {
                    return attackSprite;
                }
            }

            if (isAttackEngaged)
            {
                Sprite engagedSprite = SelectAnimatedAttackFrame(
                    enemyAttackSprites,
                    6f,
                    index * 0.11f,
                    ResolveResponsiveEngagedLoopStartProgress(currentPreviewEnemyData),
                    ResolveResponsiveEngagedLoopEndProgress(currentPreviewEnemyData));
                if (engagedSprite != null)
                {
                    return engagedSprite;
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

        private BattleVisualPose ResolveEnemyPreviewPose(int index, bool isMoving, bool isAttackEngaged)
        {
            if (index >= 0 && index < enemyAttackVisualRemainings.Count && enemyAttackVisualRemainings[index] > 0f)
            {
                return BattleVisualPose.Attack;
            }

            if (isAttackEngaged)
            {
                return BattleVisualPose.Attack;
            }

            if (isMoving)
            {
                return BattleVisualPose.Move;
            }

            return BattleVisualPose.Idle;
        }

        private IReadOnlyList<Sprite> ResolveAllyPreviewReferenceSprites(int index, BattleVisualPose pose)
        {
            switch (pose)
            {
                case BattleVisualPose.Attack:
                    if (index >= 0 &&
                        index < allyPreviewMonsterData.Count &&
                        ShouldUseAttackBodyMetrics(allyPreviewMonsterData[index]) &&
                        index < allyIdleSprites.Count &&
                        allyIdleSprites[index] != null &&
                        allyIdleSprites[index].Count > 0)
                    {
                        return allyIdleSprites[index];
                    }

                    return index >= 0 && index < allyAttackSprites.Count && allyAttackSprites[index] != null && allyAttackSprites[index].Count > 0
                        ? allyAttackSprites[index]
                        : ResolveAllyPreviewReferenceSprites(index, BattleVisualPose.Idle);
                case BattleVisualPose.Move:
                    return index >= 0 && index < allyMoveSprites.Count && allyMoveSprites[index] != null && allyMoveSprites[index].Count > 0
                        ? allyMoveSprites[index]
                        : ResolveAllyPreviewReferenceSprites(index, BattleVisualPose.Idle);
                case BattleVisualPose.Idle:
                default:
                    return index >= 0 && index < allyIdleSprites.Count
                        ? allyIdleSprites[index]
                        : null;
            }
        }

        private IReadOnlyList<Sprite> ResolveEnemyPreviewReferenceSprites(BattleVisualPose pose)
        {
            switch (pose)
            {
                case BattleVisualPose.Attack:
                    if (ShouldUseAttackBodyMetrics(ResolvePreviewMonsterData(currentPreviewEnemyData)) &&
                        enemyIdleSprites != null &&
                        enemyIdleSprites.Count > 0)
                    {
                        return enemyIdleSprites;
                    }

                    return enemyAttackSprites != null && enemyAttackSprites.Count > 0
                        ? enemyAttackSprites
                        : ResolveEnemyPreviewReferenceSprites(BattleVisualPose.Idle);
                case BattleVisualPose.Move:
                    return enemyMoveSprites != null && enemyMoveSprites.Count > 0
                        ? enemyMoveSprites
                        : ResolveEnemyPreviewReferenceSprites(BattleVisualPose.Idle);
                case BattleVisualPose.Idle:
                default:
                    return enemyIdleSprites;
            }
        }

        private static PreviewMeasurementMode ResolveAllyPreviewMeasurementMode(MonsterDataSO monsterData, BattleVisualPose pose)
        {
            if (IsSpiritQueenTitania(monsterData))
            {
                return PreviewMeasurementMode.TitaniaBody;
            }

            if (pose == BattleVisualPose.Move &&
                monsterData != null &&
                string.Equals(monsterData.monsterId, MechaDragonValdrakeMonsterId, System.StringComparison.Ordinal))
            {
                return PreviewMeasurementMode.ValdrakeBody;
            }

            if (pose == BattleVisualPose.Attack &&
                monsterData != null &&
                string.Equals(monsterData.monsterId, MechaDragonValdrakeMonsterId, System.StringComparison.Ordinal))
            {
                return PreviewMeasurementMode.ValdrakeAttackBody;
            }

            if (pose == BattleVisualPose.Attack &&
                monsterData != null &&
                string.Equals(monsterData.raceId, "mage", System.StringComparison.Ordinal))
            {
                return PreviewMeasurementMode.MageBody;
            }

            if (pose == BattleVisualPose.Attack && ShouldUseAttackBodyMetrics(monsterData))
            {
                return PreviewMeasurementMode.HumanoidWeaponBody;
            }

            return PreviewMeasurementMode.FullSprite;
        }

        private static PreviewMeasurementMode ResolveEnemyPreviewMeasurementMode(EnemyDataSO enemyData, BattleVisualPose pose)
        {
            MonsterDataSO monsterDataForMeasurement = ResolvePreviewMonsterData(enemyData);
            if (IsSpiritQueenTitania(monsterDataForMeasurement))
            {
                return PreviewMeasurementMode.TitaniaBody;
            }

            if (pose == BattleVisualPose.Move &&
                enemyData != null &&
                !string.IsNullOrEmpty(enemyData.enemyId))
            {
                string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
                if (string.Equals(monsterId, MechaDragonValdrakeMonsterId, System.StringComparison.Ordinal) ||
                    enemyData.enemyId.Contains("mecha_dragon_valdrake"))
                {
                    return PreviewMeasurementMode.ValdrakeBody;
                }
            }

            if (pose == BattleVisualPose.Attack &&
                enemyData != null &&
                !string.IsNullOrEmpty(enemyData.enemyId))
            {
                string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
                if (string.Equals(monsterId, MechaDragonValdrakeMonsterId, System.StringComparison.Ordinal) ||
                    enemyData.enemyId.Contains("mecha_dragon_valdrake"))
                {
                    return PreviewMeasurementMode.ValdrakeAttackBody;
                }
            }

            if (pose == BattleVisualPose.Attack &&
                enemyData != null &&
                !string.IsNullOrEmpty(enemyData.enemyId) &&
                enemyData.enemyId.Contains("mage"))
            {
                return PreviewMeasurementMode.MageBody;
            }

            if (pose == BattleVisualPose.Attack && ShouldUseAttackBodyMetrics(monsterDataForMeasurement))
            {
                return PreviewMeasurementMode.HumanoidWeaponBody;
            }

            return PreviewMeasurementMode.FullSprite;
        }

        private static Vector3 ResolveFacingScale(BattleFacingDirection sourceFacing, BattleFacingDirection desiredFacing)
        {
            return sourceFacing == desiredFacing
                ? Vector3.one
                : new Vector3(-1f, 1f, 1f);
        }

        private Sprite SelectAttackFrame(IReadOnlyList<Sprite> frames, float remaining, float phaseOffset, float startProgress = 0f, float endProgress = 1f)
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
            float start = Mathf.Clamp01(startProgress);
            float end = Mathf.Clamp(endProgress, start, 1f);
            normalized = Mathf.Lerp(start, end, normalized);
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * frames.Count), 0, frames.Count - 1);
            return frames[frameIndex];
        }

        private Sprite SelectAnimatedAttackFrame(IReadOnlyList<Sprite> frames, float fps, float phaseOffset, float startProgress, float endProgress = 1f)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1 || !Application.isPlaying)
            {
                return frames[0];
            }

            int startFrame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(startProgress) * frames.Count), 0, frames.Count - 1);
            int endFrameExclusive = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Clamp(endProgress, startProgress, 1f) * frames.Count),
                startFrame + 1,
                frames.Count);
            int frameCount = Mathf.Max(1, endFrameExclusive - startFrame);
            float time = Time.realtimeSinceStartup * fps + phaseOffset;
            int frameIndex = startFrame + (Mathf.Abs(Mathf.FloorToInt(time)) % frameCount);
            return frames[Mathf.Clamp(frameIndex, 0, frames.Count - 1)];
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

        private static float ResolveResponsiveAttackStartProgress(MonsterDataSO monsterData)
        {
            if (IsClass2ResponsiveMeleeAttackLineage(monsterData))
            {
                return Class2ResponsiveMeleeAttackStartProgress;
            }

            return IsResponsiveMeleeAttackLineage(monsterData)
                ? ResponsiveMeleeAttackStartProgress
                : 0f;
        }

        private static float ResolveResponsiveAttackEndProgress(MonsterDataSO monsterData)
        {
            return IsClass2ResponsiveMeleeAttackLineage(monsterData)
                ? Class2ResponsiveMeleeAttackEndProgress
                : 1f;
        }

        private static float ResolveResponsiveAttackStartProgress(EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.enemyId))
            {
                return 0f;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId)
                ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                : null;
            return ResolveResponsiveAttackStartProgress(monsterData);
        }

        private static float ResolveResponsiveAttackEndProgress(EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.enemyId))
            {
                return 1f;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId)
                ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                : null;
            return ResolveResponsiveAttackEndProgress(monsterData);
        }

        private static float ResolveResponsiveEngagedLoopStartProgress(MonsterDataSO monsterData)
        {
            return IsResponsiveMeleeAttackLineage(monsterData)
                ? ResponsiveMeleeEngagedLoopStartProgress
                : 0f;
        }

        private static float ResolveResponsiveEngagedLoopStartProgress(EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.enemyId))
            {
                return 0f;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId)
                ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                : null;
            return ResolveResponsiveEngagedLoopStartProgress(monsterData);
        }

        private static float ResolveResponsiveEngagedLoopEndProgress(MonsterDataSO monsterData)
        {
            return IsClass2ResponsiveMeleeAttackLineage(monsterData)
                ? Class2ResponsiveMeleeEngagedLoopEndProgress
                : 1f;
        }

        private static float ResolveResponsiveEngagedLoopEndProgress(EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.enemyId))
            {
                return 1f;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
            MonsterDataSO monsterData = !string.IsNullOrEmpty(monsterId)
                ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                : null;
            return ResolveResponsiveEngagedLoopEndProgress(monsterData);
        }

        private static bool IsClass2ResponsiveMeleeAttackLineage(MonsterDataSO monsterData)
        {
            return monsterData != null &&
                monsterData.classRank == 2 &&
                IsResponsiveMeleeAttackLineage(monsterData);
        }

        private static bool IsResponsiveMeleeAttackLineage(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return false;
            }

            if (BattleAttackRangeResolver.ResolveMonsterAttackRange(monsterData) >= RangedAttackThreshold)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(monsterData.raceId) &&
                (string.Equals(monsterData.raceId, "swordsman", System.StringComparison.Ordinal) ||
                string.Equals(monsterData.raceId, "golem", System.StringComparison.Ordinal)))
            {
                return true;
            }

            return !string.IsNullOrEmpty(monsterData.monsterId) &&
                ResponsiveMeleeAttackMonsterIds.Contains(monsterData.monsterId);
        }

        private static bool ShouldUseHumanoidWeaponBodyMetrics(MonsterDataSO monsterData)
        {
            if (monsterData == null || monsterData.classRank < 3)
            {
                return false;
            }

            if (BattleAttackRangeResolver.ResolveMonsterAttackRange(monsterData) >= RangedAttackThreshold)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(monsterData.raceId) &&
                (string.Equals(monsterData.raceId, "swordsman", System.StringComparison.Ordinal) ||
                string.Equals(monsterData.raceId, "angel", System.StringComparison.Ordinal) ||
                string.Equals(monsterData.raceId, "spirit", System.StringComparison.Ordinal)))
            {
                return true;
            }

            if (string.IsNullOrEmpty(monsterData.monsterId))
            {
                return false;
            }

            return monsterData.monsterId.Contains("sword") ||
                monsterData.monsterId.Contains("saber") ||
                monsterData.monsterId.Contains("blade");
        }

        private static bool ShouldUseAttackBodyMetrics(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return false;
            }

            return ShouldUseHumanoidWeaponBodyMetrics(monsterData) ||
                IsSpiritQueenTitania(monsterData);
        }

        private static bool IsSpiritQueenTitania(MonsterDataSO monsterData)
        {
            return monsterData != null &&
                string.Equals(monsterData.monsterId, SpiritQueenTitaniaMonsterId, System.StringComparison.Ordinal);
        }

        private Vector2 ResolvePresentationMotionOffset(int index, bool isAlly, BattleVisualPose pose, float attackRemaining, bool isRanged)
        {
            if (!Application.isPlaying)
            {
                return Vector2.zero;
            }

            float time = Time.realtimeSinceStartup;
            float phase = (index * 0.73f) + (isAlly ? 0.17f : 1.11f);
            Vector2 offset = Vector2.zero;
            if (pose == BattleVisualPose.Idle)
            {
                offset.x += Mathf.Sin((time * 1.7f) + phase) * IdleSwayAmplitude;
                offset.y += Mathf.Sin((time * 2.8f) + phase) * IdleFloatAmplitude;
            }
            else if (pose == BattleVisualPose.Move)
            {
                offset.x += Mathf.Sin((time * 5.4f) + phase) * (IdleSwayAmplitude + 1f);
                offset.y += Mathf.Abs(Mathf.Sin((time * 9.2f) + phase)) * MoveBobAmplitude;
            }

            return offset;
        }

        private static float ResolveMonsterPreviewScale(MonsterDataSO monsterData, BattleVisualPose pose)
        {
            if (monsterData == null)
            {
                return 1f;
            }

            if (!string.IsNullOrEmpty(monsterData.monsterId) && AllyPreviewScaleOverrides.TryGetValue(monsterData.monsterId, out float scale))
            {
                return scale;
            }

            return Mathf.Clamp(monsterData.battleVisualScale > 0f ? monsterData.battleVisualScale : 1f, 0.55f, 1.55f);
        }

        private static float ResolveEnemyPreviewScale(EnemyDataSO enemyData, BattleVisualPose pose)
        {
            if (enemyData != null && !string.IsNullOrEmpty(enemyData.enemyId) && EnemyPreviewScaleOverrides.TryGetValue(enemyData.enemyId, out float scale))
            {
                return scale;
            }

            MonsterDataSO monsterData = ResolvePreviewMonsterData(enemyData);
            if (monsterData != null)
            {
                return ResolveMonsterPreviewScale(monsterData, pose);
            }

            return 1f;
        }

        private static MonsterDataSO ResolvePreviewMonsterData(EnemyDataSO enemyData)
        {
            if (enemyData == null || string.IsNullOrEmpty(enemyData.enemyId))
            {
                return null;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(enemyData.enemyId);
            return string.IsNullOrEmpty(monsterId)
                ? null
                : MasterDataManager.Instance?.GetMonsterData(monsterId);
        }

        private static void ApplyPreviewVisualLayout(
            Image image,
            Vector2 baseSize,
            Vector2 motionOffset,
            IReadOnlyList<Sprite> referenceSprites = null,
            PreviewMeasurementMode measurementMode = PreviewMeasurementMode.FullSprite)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            Sprite sprite = image.sprite;
            if (sprite == null || baseSize.x <= 0f || baseSize.y <= 0f)
            {
                rect.sizeDelta = baseSize;
                rect.anchoredPosition = motionOffset;
                return;
            }

            BattleSpriteVisualMetrics metrics = ResolvePreviewVisualMetrics(sprite, measurementMode);
            if (!metrics.HasOpaquePixels || metrics.OpaqueHeight <= 0f || metrics.SpriteWidth <= 0f || metrics.SpriteHeight <= 0f)
            {
                rect.sizeDelta = baseSize;
                rect.anchoredPosition = motionOffset;
                return;
            }

            ResolvePreviewReferenceMetrics(sprite, referenceSprites, measurementMode, out float referenceOpaqueHeight, out float referenceWidthForClamp);

            float targetOpaqueHeight = Mathf.Max(1f, baseSize.y * PreviewVisualTargetHeightRatio);
            float imageScale = targetOpaqueHeight / referenceOpaqueHeight;
            Vector2 resolvedSize = new Vector2(metrics.SpriteWidth * imageScale, metrics.SpriteHeight * imageScale);
            float maxWidth = Mathf.Max(baseSize.x, baseSize.x * PreviewVisualMaxWidthMultiplier);
            float referenceWidth = UsesBodyWidthForPreviewClamp(measurementMode)
                ? Mathf.Max(metrics.OpaqueWidth, referenceWidthForClamp)
                : Mathf.Max(metrics.SpriteWidth, referenceWidthForClamp);
            if (referenceWidth * imageScale > maxWidth)
            {
                float widthClamp = maxWidth / (referenceWidth * imageScale);
                imageScale *= widthClamp;
                resolvedSize *= widthClamp;
            }

            Vector2 baselineOffset = new Vector2(0f, -baseSize.y * PreviewVisualBaselineRatio);
            Vector2 bottomCenterOffset = metrics.OpaqueBottomCenterFromSpriteCenter * imageScale;
            if (rect.localScale.x < 0f)
            {
                bottomCenterOffset.x *= -1f;
            }

            rect.sizeDelta = resolvedSize;
            rect.anchoredPosition = motionOffset + baselineOffset - bottomCenterOffset;
        }

        private static void ResolvePreviewReferenceMetrics(
            Sprite currentSprite,
            IReadOnlyList<Sprite> referenceSprites,
            PreviewMeasurementMode measurementMode,
            out float referenceOpaqueHeight,
            out float referenceWidthForClamp)
        {
            BattleSpriteVisualMetrics currentMetrics = ResolvePreviewVisualMetrics(currentSprite, measurementMode);
            referenceOpaqueHeight = Mathf.Max(1f, currentMetrics.OpaqueHeight);
            referenceWidthForClamp = ResolveReferenceWidthForClamp(currentMetrics, measurementMode);
            float currentOpaqueHeight = referenceOpaqueHeight;
            float externalReferenceOpaqueHeight = 0f;

            if (referenceSprites == null || referenceSprites.Count == 0)
            {
                return;
            }

            for (int i = 0; i < referenceSprites.Count; i += 1)
            {
                Sprite referenceSprite = referenceSprites[i];
                if (referenceSprite == null)
                {
                    continue;
                }

                BattleSpriteVisualMetrics referenceMetrics = ResolvePreviewVisualMetrics(referenceSprite, measurementMode);
                if (!referenceMetrics.HasOpaquePixels || referenceMetrics.OpaqueHeight <= 0f)
                {
                    continue;
                }

                referenceOpaqueHeight = Mathf.Max(referenceOpaqueHeight, referenceMetrics.OpaqueHeight);
                externalReferenceOpaqueHeight = Mathf.Max(externalReferenceOpaqueHeight, referenceMetrics.OpaqueHeight);
                referenceWidthForClamp = Mathf.Max(referenceWidthForClamp, ResolveReferenceWidthForClamp(referenceMetrics, measurementMode));
            }

            if ((measurementMode == PreviewMeasurementMode.HumanoidWeaponBody ||
                measurementMode == PreviewMeasurementMode.TitaniaBody) &&
                externalReferenceOpaqueHeight > 0f)
            {
                referenceOpaqueHeight = Mathf.Min(currentOpaqueHeight, externalReferenceOpaqueHeight);
            }
        }

        private static float ResolveReferenceWidthForClamp(BattleSpriteVisualMetrics metrics, PreviewMeasurementMode measurementMode)
        {
            return UsesBodyWidthForPreviewClamp(measurementMode)
                ? Mathf.Max(1f, metrics.OpaqueWidth)
                : Mathf.Max(1f, metrics.SpriteWidth);
        }

        private static bool UsesBodyWidthForPreviewClamp(PreviewMeasurementMode measurementMode)
        {
            return measurementMode == PreviewMeasurementMode.MageBody ||
                measurementMode == PreviewMeasurementMode.HumanoidWeaponBody ||
                measurementMode == PreviewMeasurementMode.TitaniaBody;
        }

        private static BattleSpriteVisualMetrics ResolvePreviewVisualMetrics(Sprite sprite, PreviewMeasurementMode measurementMode)
        {
            if (measurementMode == PreviewMeasurementMode.ValdrakeBody)
            {
                return ResolveValdrakeBodyVisualMetrics(
                    sprite,
                    ValdrakeChestCoreToBodyAnchorX,
                    ValdrakeBodyVisualMetricsCache);
            }

            if (measurementMode == PreviewMeasurementMode.ValdrakeAttackBody)
            {
                return ResolveValdrakeBodyVisualMetrics(
                    sprite,
                    ValdrakeAttackChestCoreToBodyAnchorX,
                    ValdrakeAttackBodyVisualMetricsCache,
                    ValdrakeAttackMovementMatchedBodyHeight);
            }

            if (measurementMode == PreviewMeasurementMode.HumanoidWeaponBody)
            {
                return ResolveHumanoidWeaponBodyVisualMetrics(sprite);
            }

            if (measurementMode == PreviewMeasurementMode.TitaniaBody)
            {
                return ResolveTitaniaBodyVisualMetrics(sprite);
            }

            if (measurementMode != PreviewMeasurementMode.MageBody)
            {
                return BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            }

            if (sprite != null && MageBodyVisualMetricsCache.TryGetValue(sprite, out BattleSpriteVisualMetrics cachedMetrics))
            {
                return cachedMetrics;
            }

            BattleSpriteVisualMetrics bodyMetrics = ResolveCroppedSpriteVisualMetrics(sprite, 0.34f, 0.88f);
            BattleSpriteVisualMetrics resolvedMetrics = bodyMetrics.HasOpaquePixels
                ? bodyMetrics
                : BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            if (sprite != null)
            {
                MageBodyVisualMetricsCache[sprite] = resolvedMetrics;
            }

            return resolvedMetrics;
        }

        private static BattleSpriteVisualMetrics ResolveHumanoidWeaponBodyVisualMetrics(Sprite sprite)
        {
            if (sprite != null && HumanoidWeaponBodyVisualMetricsCache.TryGetValue(sprite, out BattleSpriteVisualMetrics cachedMetrics))
            {
                return cachedMetrics;
            }

            BattleSpriteVisualMetrics bodyMetrics = ResolveCroppedSpriteVisualMetrics(sprite, 0.18f, 0.88f);
            BattleSpriteVisualMetrics resolvedMetrics = bodyMetrics.HasOpaquePixels
                ? bodyMetrics
                : BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            if (sprite != null)
            {
                HumanoidWeaponBodyVisualMetricsCache[sprite] = resolvedMetrics;
            }

            return resolvedMetrics;
        }

        private static BattleSpriteVisualMetrics ResolveTitaniaBodyVisualMetrics(Sprite sprite)
        {
            BattleSpriteVisualMetrics fullMetrics = BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            if (sprite == null || !fullMetrics.HasOpaquePixels)
            {
                return fullMetrics;
            }

            if (TitaniaBodyVisualMetricsCache.TryGetValue(sprite, out BattleSpriteVisualMetrics cachedMetrics))
            {
                return cachedMetrics;
            }

            Rect spriteRect = sprite.rect;
            Texture2D texture = sprite.texture;
            if (texture == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                TitaniaBodyVisualMetricsCache[sprite] = fullMetrics;
                return fullMetrics;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception)
            {
                TitaniaBodyVisualMetricsCache[sprite] = fullMetrics;
                return fullMetrics;
            }

            int textureWidth = texture.width;
            int textureHeight = texture.height;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.xMin + (spriteRect.width * TitaniaBodyMetricsMinX)), 0, textureWidth);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMin + (spriteRect.width * TitaniaBodyMetricsMaxX)), xMin, textureWidth);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.yMin), 0, textureHeight);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax - (spriteRect.height * TitaniaBodyMetricsIgnoreTopRatio)), yMin, textureHeight);
            int denseRowThreshold = Mathf.Max(12, Mathf.RoundToInt(spriteRect.width * TitaniaBodyMetricsDenseRowRatio));

            int bestMinY = yMax;
            int bestMaxY = yMin - 1;
            for (int y = yMin; y < yMax; y += 1)
            {
                int opaqueCount = 0;
                int rowIndex = y * textureWidth;
                for (int x = xMin; x < xMax; x += 1)
                {
                    if (pixels[rowIndex + x].a > 8)
                    {
                        opaqueCount += 1;
                    }
                }

                if (opaqueCount >= denseRowThreshold)
                {
                    bestMinY = Mathf.Min(bestMinY, y);
                    bestMaxY = Mathf.Max(bestMaxY, y);
                }
            }

            if (bestMaxY < bestMinY)
            {
                BattleSpriteVisualMetrics componentMetrics = ResolveLargestBodyComponentVisualMetrics(
                    sprite,
                    TitaniaBodyMetricsMinX,
                    TitaniaBodyMetricsMaxX,
                    TitaniaBodyVisualMetricsCache);
                return componentMetrics;
            }

            int bestMinX = xMax;
            int bestMaxX = xMin - 1;
            for (int y = bestMinY; y <= bestMaxY; y += 1)
            {
                int rowIndex = y * textureWidth;
                for (int x = xMin; x < xMax; x += 1)
                {
                    if (pixels[rowIndex + x].a > 8)
                    {
                        bestMinX = Mathf.Min(bestMinX, x);
                        bestMaxX = Mathf.Max(bestMaxX, x);
                    }
                }
            }

            if (bestMaxX < bestMinX)
            {
                TitaniaBodyVisualMetricsCache[sprite] = fullMetrics;
                return fullMetrics;
            }

            float localOpaqueMinX = bestMinX - spriteRect.xMin;
            float localOpaqueMaxX = bestMaxX + 1f - spriteRect.xMin;
            float localOpaqueMinY = bestMinY - spriteRect.yMin;
            float localOpaqueMaxY = bestMaxY + 1f - spriteRect.yMin;
            float opaqueWidth = Mathf.Max(1f, localOpaqueMaxX - localOpaqueMinX);
            float opaqueHeight = Mathf.Max(1f, localOpaqueMaxY - localOpaqueMinY);
            Vector2 spriteCenter = new Vector2(spriteRect.width * 0.5f, spriteRect.height * 0.5f);
            Vector2 fullOpaqueBottomCenter = fullMetrics.OpaqueBottomCenterFromSpriteCenter + spriteCenter;
            Vector2 opaqueBottomCenter = new Vector2(
                (localOpaqueMinX + localOpaqueMaxX) * 0.5f,
                fullOpaqueBottomCenter.y);
            BattleSpriteVisualMetrics resolvedMetrics = new BattleSpriteVisualMetrics(
                spriteRect.width,
                spriteRect.height,
                opaqueWidth,
                opaqueHeight,
                opaqueBottomCenter - spriteCenter,
                true,
                true);
            TitaniaBodyVisualMetricsCache[sprite] = resolvedMetrics;
            return resolvedMetrics;
        }

        private static BattleSpriteVisualMetrics ResolveLargestBodyComponentVisualMetrics(
            Sprite sprite,
            float normalizedMinX,
            float normalizedMaxX,
            Dictionary<Sprite, BattleSpriteVisualMetrics> cache)
        {
            BattleSpriteVisualMetrics fullMetrics = BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            if (sprite == null || !fullMetrics.HasOpaquePixels)
            {
                return fullMetrics;
            }

            if (cache.TryGetValue(sprite, out BattleSpriteVisualMetrics cachedMetrics))
            {
                return cachedMetrics;
            }

            Rect spriteRect = sprite.rect;
            Texture2D texture = sprite.texture;
            if (texture == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception)
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            int textureWidth = texture.width;
            int textureHeight = texture.height;
            float clampedMinX = Mathf.Clamp01(normalizedMinX);
            float clampedMaxX = Mathf.Clamp(normalizedMaxX, clampedMinX, 1f);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.xMin + (spriteRect.width * clampedMinX)), 0, textureWidth);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMin + (spriteRect.width * clampedMaxX)), xMin, textureWidth);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.yMin), 0, textureHeight);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax), yMin, textureHeight);
            bool[] visited = new bool[textureWidth * textureHeight];
            Queue<int> queue = new Queue<int>();
            int bestCount = 0;
            int bestMinX = xMax;
            int bestMaxX = xMin - 1;
            int bestMinY = yMax;
            int bestMaxY = yMin - 1;

            for (int y = yMin; y < yMax; y += 1)
            {
                for (int x = xMin; x < xMax; x += 1)
                {
                    int startIndex = y * textureWidth + x;
                    if (visited[startIndex] || pixels[startIndex].a <= 8)
                    {
                        continue;
                    }

                    visited[startIndex] = true;
                    queue.Clear();
                    queue.Enqueue(startIndex);
                    int componentCount = 0;
                    int componentMinX = x;
                    int componentMaxX = x;
                    int componentMinY = y;
                    int componentMaxY = y;

                    while (queue.Count > 0)
                    {
                        int currentIndex = queue.Dequeue();
                        int currentX = currentIndex % textureWidth;
                        int currentY = currentIndex / textureWidth;
                        componentCount += 1;
                        componentMinX = Mathf.Min(componentMinX, currentX);
                        componentMaxX = Mathf.Max(componentMaxX, currentX);
                        componentMinY = Mathf.Min(componentMinY, currentY);
                        componentMaxY = Mathf.Max(componentMaxY, currentY);

                        for (int offsetY = -1; offsetY <= 1; offsetY += 1)
                        {
                            int nextY = currentY + offsetY;
                            if (nextY < yMin || nextY >= yMax)
                            {
                                continue;
                            }

                            for (int offsetX = -1; offsetX <= 1; offsetX += 1)
                            {
                                if (offsetX == 0 && offsetY == 0)
                                {
                                    continue;
                                }

                                int nextX = currentX + offsetX;
                                if (nextX < xMin || nextX >= xMax)
                                {
                                    continue;
                                }

                                int nextIndex = nextY * textureWidth + nextX;
                                if (visited[nextIndex] || pixels[nextIndex].a <= 8)
                                {
                                    continue;
                                }

                                visited[nextIndex] = true;
                                queue.Enqueue(nextIndex);
                            }
                        }
                    }

                    if (componentCount > bestCount)
                    {
                        bestCount = componentCount;
                        bestMinX = componentMinX;
                        bestMaxX = componentMaxX;
                        bestMinY = componentMinY;
                        bestMaxY = componentMaxY;
                    }
                }
            }

            if (bestCount <= 0 || bestMaxX < bestMinX || bestMaxY < bestMinY)
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            float localOpaqueMinX = bestMinX - spriteRect.xMin;
            float localOpaqueMaxX = bestMaxX + 1f - spriteRect.xMin;
            float localOpaqueMinY = bestMinY - spriteRect.yMin;
            float localOpaqueMaxY = bestMaxY + 1f - spriteRect.yMin;
            float opaqueWidth = Mathf.Max(1f, localOpaqueMaxX - localOpaqueMinX);
            float opaqueHeight = Mathf.Max(1f, localOpaqueMaxY - localOpaqueMinY);
            Vector2 spriteCenter = new Vector2(spriteRect.width * 0.5f, spriteRect.height * 0.5f);
            Vector2 opaqueBottomCenter = new Vector2(
                (localOpaqueMinX + localOpaqueMaxX) * 0.5f,
                localOpaqueMinY);
            BattleSpriteVisualMetrics resolvedMetrics = new BattleSpriteVisualMetrics(
                spriteRect.width,
                spriteRect.height,
                opaqueWidth,
                opaqueHeight,
                opaqueBottomCenter - spriteCenter,
                true,
                true);
            cache[sprite] = resolvedMetrics;
            return resolvedMetrics;
        }

        private static BattleSpriteVisualMetrics ResolveValdrakeBodyVisualMetrics(
            Sprite sprite,
            float chestCoreToBodyAnchorX,
            Dictionary<Sprite, BattleSpriteVisualMetrics> cache,
            float movementMatchedBodyHeight = 0f)
        {
            BattleSpriteVisualMetrics fullMetrics = BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            if (sprite == null || !fullMetrics.HasOpaquePixels)
            {
                return fullMetrics;
            }

            if (cache.TryGetValue(sprite, out BattleSpriteVisualMetrics cachedMetrics))
            {
                return cachedMetrics;
            }

            Rect spriteRect = sprite.rect;
            Texture2D texture = sprite.texture;
            if (texture == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception)
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            if (!TryFindValdrakeChestCore(spriteRect, texture.width, texture.height, pixels, out ValdrakeRedCoreComponent chestCore))
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            if (!TryResolveValdrakeBodySpan(spriteRect, texture.width, texture.height, pixels, chestCore, out ValdrakeBodySpan bodySpan))
            {
                cache[sprite] = fullMetrics;
                return fullMetrics;
            }

            Vector2 spriteCenter = new Vector2(spriteRect.width * 0.5f, spriteRect.height * 0.5f);
            float localCoreCenterX = chestCore.CenterX - spriteRect.xMin;
            float localBodyAnchorX = Mathf.Clamp(
                localCoreCenterX + chestCoreToBodyAnchorX,
                0f,
                spriteRect.width);
            Vector2 bodyAnchoredBottomCenter = new Vector2(
                localBodyAnchorX,
                bodySpan.MinY - spriteRect.yMin);
            float resolvedOpaqueHeight = movementMatchedBodyHeight > 0f
                ? movementMatchedBodyHeight
                : bodySpan.Height;
            BattleSpriteVisualMetrics resolvedMetrics = new BattleSpriteVisualMetrics(
                fullMetrics.SpriteWidth,
                fullMetrics.SpriteHeight,
                fullMetrics.OpaqueWidth,
                resolvedOpaqueHeight,
                bodyAnchoredBottomCenter - spriteCenter,
                true,
                true);
            cache[sprite] = resolvedMetrics;
            return resolvedMetrics;
        }

        private static bool TryResolveValdrakeBodySpan(
            Rect spriteRect,
            int textureWidth,
            int textureHeight,
            Color32[] pixels,
            ValdrakeRedCoreComponent chestCore,
            out ValdrakeBodySpan bodySpan)
        {
            int xMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.xMin), 0, textureWidth);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.yMin), 0, textureHeight);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMax), xMin, textureWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax), yMin, textureHeight);
            int coreX = Mathf.RoundToInt(chestCore.CenterX);
            int headMinX = Mathf.Clamp(coreX - 150, xMin, xMax);
            int headMaxX = Mathf.Clamp(coreX + 90, headMinX, xMax);
            int footMinX = Mathf.Clamp(coreX - 120, xMin, xMax);
            int footMaxX = Mathf.Clamp(coreX + 160, footMinX, xMax);
            int bodyMinY = yMax;
            int bodyMaxY = yMin - 1;

            for (int y = yMin; y < yMax; y += 1)
            {
                int rowOffset = y * textureWidth;
                for (int x = footMinX; x < footMaxX; x += 1)
                {
                    if (pixels[rowOffset + x].a > 8)
                    {
                        bodyMinY = Mathf.Min(bodyMinY, y);
                    }
                }

                for (int x = headMinX; x < headMaxX; x += 1)
                {
                    if (pixels[rowOffset + x].a > 8)
                    {
                        bodyMaxY = Mathf.Max(bodyMaxY, y);
                    }
                }
            }

            bodySpan = new ValdrakeBodySpan
            {
                MinY = bodyMinY,
                MaxY = bodyMaxY
            };
            return bodyMaxY >= bodyMinY;
        }

        private static bool TryFindValdrakeChestCore(
            Rect spriteRect,
            int textureWidth,
            int textureHeight,
            Color32[] pixels,
            out ValdrakeRedCoreComponent bestComponent)
        {
            bestComponent = new ValdrakeRedCoreComponent();
            int xMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.xMin), 0, textureWidth);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.yMin), 0, textureHeight);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMax), xMin, textureWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax), yMin, textureHeight);
            int width = xMax - xMin;
            int height = yMax - yMin;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            bool[] redMask = new bool[width * height];
            for (int localY = 0; localY < height; localY += 1)
            {
                int textureY = yMin + localY;
                int rowOffset = textureY * textureWidth;
                int maskOffset = localY * width;
                for (int localX = 0; localX < width; localX += 1)
                {
                    Color32 pixel = pixels[rowOffset + xMin + localX];
                    redMask[maskOffset + localX] = IsValdrakeChestCorePixel(pixel);
                }
            }

            bool[] visited = new bool[redMask.Length];
            int[] queue = new int[redMask.Length];
            int bestCount = 0;
            for (int index = 0; index < redMask.Length; index += 1)
            {
                if (!redMask[index] || visited[index])
                {
                    continue;
                }

                ValdrakeRedCoreComponent component = FloodFillValdrakeRedCoreComponent(
                    index,
                    width,
                    xMin,
                    yMin,
                    redMask,
                    visited,
                    queue);
                if (component.Count < 30 ||
                    component.Count <= bestCount ||
                    !IsLikelyValdrakeChestCoreComponent(component, xMin))
                {
                    continue;
                }

                bestCount = component.Count;
                bestComponent = component;
            }

            return bestCount > 0;
        }

        private static bool IsLikelyValdrakeChestCoreComponent(ValdrakeRedCoreComponent component, int spriteTextureMinX)
        {
            float localCenterX = component.CenterX - spriteTextureMinX;
            return component.Width >= 20 &&
                component.Width <= 45 &&
                component.Height >= 24 &&
                component.Height <= 55 &&
                localCenterX >= 120f &&
                localCenterX <= 340f;
        }

        private static ValdrakeRedCoreComponent FloodFillValdrakeRedCoreComponent(
            int startIndex,
            int width,
            int textureXOffset,
            int textureYOffset,
            bool[] redMask,
            bool[] visited,
            int[] queue)
        {
            int head = 0;
            int tail = 0;
            queue[tail] = startIndex;
            tail += 1;
            visited[startIndex] = true;

            int localStartX = startIndex % width;
            int localStartY = startIndex / width;
            ValdrakeRedCoreComponent component = new ValdrakeRedCoreComponent
            {
                MinX = textureXOffset + localStartX,
                MinY = textureYOffset + localStartY,
                MaxX = textureXOffset + localStartX,
                MaxY = textureYOffset + localStartY
            };

            while (head < tail)
            {
                int index = queue[head];
                head += 1;
                int localX = index % width;
                int localY = index / width;
                int textureX = textureXOffset + localX;
                int textureY = textureYOffset + localY;

                component.MinX = Mathf.Min(component.MinX, textureX);
                component.MinY = Mathf.Min(component.MinY, textureY);
                component.MaxX = Mathf.Max(component.MaxX, textureX);
                component.MaxY = Mathf.Max(component.MaxY, textureY);
                component.Count += 1;
                component.SumX += textureX;
                component.SumY += textureY;

                TryEnqueueValdrakeRedCorePixel(index - 1, localX > 0, redMask, visited, queue, ref tail);
                TryEnqueueValdrakeRedCorePixel(index + 1, localX < width - 1, redMask, visited, queue, ref tail);
                TryEnqueueValdrakeRedCorePixel(index - width, localY > 0, redMask, visited, queue, ref tail);
                TryEnqueueValdrakeRedCorePixel(index + width, localY < redMask.Length / width - 1, redMask, visited, queue, ref tail);
            }

            return component;
        }

        private static void TryEnqueueValdrakeRedCorePixel(
            int index,
            bool isInBounds,
            bool[] redMask,
            bool[] visited,
            int[] queue,
            ref int tail)
        {
            if (!isInBounds || !redMask[index] || visited[index])
            {
                return;
            }

            visited[index] = true;
            queue[tail] = index;
            tail += 1;
        }

        private static bool IsValdrakeChestCorePixel(Color32 pixel)
        {
            int red = pixel.r;
            int green = pixel.g;
            int blue = pixel.b;
            return pixel.a > 20 &&
                red > 125 &&
                red > green * 2 &&
                red > blue * 2;
        }

        private static BattleSpriteVisualMetrics ResolveCroppedSpriteVisualMetrics(Sprite sprite, float normalizedMinX, float normalizedMaxX)
        {
            if (sprite == null)
            {
                return new BattleSpriteVisualMetrics(0f, 0f, 0f, 0f, Vector2.zero, false, false);
            }

            Rect spriteRect = sprite.rect;
            Texture2D texture = sprite.texture;
            if (texture == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                return BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception)
            {
                return BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            }

            int textureWidth = texture.width;
            float clampedMinX = Mathf.Clamp01(normalizedMinX);
            float clampedMaxX = Mathf.Clamp(normalizedMaxX, clampedMinX, 1f);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.xMin + (spriteRect.width * clampedMinX)), 0, texture.width);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.xMin + (spriteRect.width * clampedMaxX)), xMin, texture.width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(spriteRect.yMin), 0, texture.height);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(spriteRect.yMax), yMin, texture.height);
            int opaqueMinX = xMax;
            int opaqueMaxX = xMin - 1;
            int opaqueMinY = yMax;
            int opaqueMaxY = yMin - 1;

            for (int y = yMin; y < yMax; y += 1)
            {
                int rowOffset = y * textureWidth;
                for (int x = xMin; x < xMax; x += 1)
                {
                    if (pixels[rowOffset + x].a <= 8)
                    {
                        continue;
                    }

                    opaqueMinX = Mathf.Min(opaqueMinX, x);
                    opaqueMaxX = Mathf.Max(opaqueMaxX, x);
                    opaqueMinY = Mathf.Min(opaqueMinY, y);
                    opaqueMaxY = Mathf.Max(opaqueMaxY, y);
                }
            }

            if (opaqueMaxX < opaqueMinX || opaqueMaxY < opaqueMinY)
            {
                return new BattleSpriteVisualMetrics(
                    spriteRect.width,
                    spriteRect.height,
                    spriteRect.width,
                    spriteRect.height,
                    new Vector2(0f, -spriteRect.height * 0.5f),
                    false,
                    true);
            }

            float localOpaqueMinX = opaqueMinX - spriteRect.xMin;
            float localOpaqueMaxX = opaqueMaxX + 1f - spriteRect.xMin;
            float localOpaqueMinY = opaqueMinY - spriteRect.yMin;
            float localOpaqueMaxY = opaqueMaxY + 1f - spriteRect.yMin;
            float opaqueWidth = Mathf.Max(1f, localOpaqueMaxX - localOpaqueMinX);
            float opaqueHeight = Mathf.Max(1f, localOpaqueMaxY - localOpaqueMinY);
            Vector2 spriteCenter = new Vector2(spriteRect.width * 0.5f, spriteRect.height * 0.5f);
            Vector2 opaqueBottomCenter = new Vector2(
                (localOpaqueMinX + localOpaqueMaxX) * 0.5f,
                localOpaqueMinY);

            return new BattleSpriteVisualMetrics(
                spriteRect.width,
                spriteRect.height,
                opaqueWidth,
                opaqueHeight,
                opaqueBottomCenter - spriteCenter,
                true,
                true);
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
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.useSpriteMesh = false;
                return;
            }

            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.useSpriteMesh = false;
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
            ArrangeMonsterPreviewLayers();
        }

        private void ArrangeMonsterPreviewLayers()
        {
            if (monsterPreviewRoot == null || !Application.isPlaying || isApplyingEditorPreview)
            {
                return;
            }

            for (int i = 0; i < enemyPreviewImages.Count; i += 1)
            {
                if (enemyPreviewImages[i] != null)
                {
                    enemyPreviewImages[i].transform.SetAsLastSibling();
                }
            }

            for (int i = 0; i < enemyPreviewHpBars.Count; i += 1)
            {
                if (enemyPreviewHpBars[i]?.Root != null)
                {
                    enemyPreviewHpBars[i].Root.SetAsLastSibling();
                }
            }

            for (int i = 0; i < allyPreviewImages.Count; i += 1)
            {
                if (allyPreviewImages[i] != null)
                {
                    allyPreviewImages[i].transform.SetAsLastSibling();
                }
            }

            for (int i = 0; i < allyPreviewHpBars.Count; i += 1)
            {
                if (allyPreviewHpBars[i]?.Root != null)
                {
                    allyPreviewHpBars[i].Root.SetAsLastSibling();
                }
            }
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
            float allyApproachT = 1f;
            int engagedEnemyPreviewCount = 0;
            bool isBossWave = stateMachine != null && stateMachine.Simulator != null && stateMachine.Simulator.IsBossWave;
            bool enemyIsMelee = IsEnemyCloseCombat(currentPreviewEnemyData, enemyAttackRange);
            int activePreviewCount = Mathf.Clamp(targetEnemyPreviewCount, 0, enemyPreviewImages.Count);
            var resolvedAllyAnchors = new Vector2[AllyPreviewAnchors.Length];
            var allySlotAlive = new bool[AllyPreviewAnchors.Length];

            for (int i = 0; i < allyPreviewImages.Count && i < AllyPreviewAnchors.Length; i += 1)
            {
                float allyDefeatRemaining = i < allyDefeatVanishRemainings.Count ? allyDefeatVanishRemainings[i] : 0f;
                float allyVanishT = AllyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(allyDefeatRemaining / AllyDefeatVanishDuration)
                    : 0f;
                bool allyAlive = simulator == null || !simulator.HasAllyRuntime(i) || simulator.IsAllyAlive(i);
                MonsterDataSO allyData = i < allyPreviewMonsterData.Count ? allyPreviewMonsterData[i] : null;
                float allyRange = i < allyAttackRanges.Count ? allyAttackRanges[i] : 1f;
                float allyHoldOffset = allyData != null
                    ? BattleAttackRangeResolver.ToMonsterHoldOffset(allyData)
                    : BattleAttackRangeResolver.ToAllyHoldOffset(allyRange);
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
                resolvedAllyAnchors[i] = allyAnchor;
                allySlotAlive[i] = allyAlive;
                float allyScale = allyAlive
                    ? 1f
                    : Mathf.Lerp(1f, 0.24f, allyVanishT);
                BattleVisualPose allyPose = ResolveAllyPreviewPose(i, allyApproachT, false);
                Vector2 allyPreviewSize = AllyPreviewSize * (allyScale * ResolveMonsterPreviewScale(allyData, allyPose));
                ApplyPreviewImageLayout(
                    allyPreviewImages[i],
                    MapBattlefieldAnchor(allyAnchor),
                    allyPreviewSize);
                if (allyPreviewImages[i] != null)
                {
                    Sprite allySprite = SelectAllyPreviewSprite(i, allyApproachT, false);
                    float allyAttackRemaining = i < allyAttackVisualRemainings.Count ? allyAttackVisualRemainings[i] : 0f;
                    Vector2 allyMotionOffset = ResolvePresentationMotionOffset(i, true, allyPose, allyAttackRemaining, allyRange >= RangedAttackThreshold);
                    SetImageSprite(allyPreviewImages[i], allySprite);
                    BattleFacingDirection allySourceFacing = BattleVisualResolver.ResolveMonsterFacing(allyData, allyPose);
                    allyPreviewImages[i].rectTransform.localScale = ResolveFacingScale(allySourceFacing, BattleFacingDirection.Right);
                    ApplyPreviewVisualLayout(allyPreviewImages[i], allyPreviewSize, allyMotionOffset, ResolveAllyPreviewReferenceSprites(i, allyPose), ResolveAllyPreviewMeasurementMode(allyData, allyPose));
                    Color allyColor = allyPreviewImages[i].color;
                    allyColor.a = allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT);
                    allyPreviewImages[i].color = allyColor;
                }

                int allyCurrentHp = simulator != null && simulator.HasAllyRuntime(i) ? simulator.GetAllyCurrentHp(i) : 0;
                int allyMaxHp = simulator != null && simulator.HasAllyRuntime(i) ? simulator.GetAllyMaxHp(i) : 0;
                UpdatePreviewHpBar(
                    i < allyPreviewHpBars.Count ? allyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(allyAnchor),
                    allyPreviewSize,
                    allyAlive && allyMaxHp > 0,
                    allyColorAlpha: allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT),
                    currentHp: allyCurrentHp,
                    maxHp: allyMaxHp,
                    fillColor: new Color(0.28f, 0.88f, 0.66f, 0.95f),
                    motionOffset: allyPreviewImages[i] != null ? allyPreviewImages[i].rectTransform.anchoredPosition : Vector2.zero);
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
                    float enemyVanishRemaining = enemyDefeatVanishRemainings.Count > 0 ? enemyDefeatVanishRemainings[0] : 0f;
                    float enemyVanishT = enemyVanishRemaining > 0f && EnemyDefeatVanishDuration > 0f
                        ? 1f - Mathf.Clamp01(enemyVanishRemaining / EnemyDefeatVanishDuration)
                        : 0f;
                    float bossScale = Mathf.Lerp(1f, 0.24f, enemyVanishT);
                    bool bossMoving = contactT < 1f;
                    BattleVisualPose bossPose = ResolveEnemyPreviewPose(0, bossMoving, false);
                    Vector2 bossPreviewSize = BossPreviewSize * (bossScale * ResolveEnemyPreviewScale(currentPreviewEnemyData, bossPose));
                    ApplyPreviewImageLayout(
                        enemyPreviewImages[0],
                        MapBattlefieldAnchor(bossAnchor),
                        bossPreviewSize);
                    float bossAttackRemaining = enemyAttackVisualRemainings.Count > 0 ? enemyAttackVisualRemainings[0] : 0f;
                    Vector2 bossMotionOffset = ResolvePresentationMotionOffset(0, false, bossPose, bossAttackRemaining, enemyAttackRange >= RangedAttackThreshold);
                    SetImageSprite(enemyPreviewImages[0], SelectEnemyPreviewSprite(0, bossMoving, false));
                    BattleFacingDirection bossSourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, bossPose);
                    enemyPreviewImages[0].rectTransform.localScale = ResolveFacingScale(bossSourceFacing, BattleFacingDirection.Left);
                    ApplyPreviewVisualLayout(enemyPreviewImages[0], bossPreviewSize, bossMotionOffset, ResolveEnemyPreviewReferenceSprites(bossPose), ResolveEnemyPreviewMeasurementMode(currentPreviewEnemyData, bossPose));
                    Color bossColor = enemyPreviewImages[0].color;
                    bossColor.a = 1f - enemyVanishT;
                    enemyPreviewImages[0].color = bossColor;
                    int bossCurrentHp = simulator != null && simulator.HasEnemyRuntime(0) ? simulator.GetEnemyCurrentHp(0) : 0;
                    int bossMaxHp = simulator != null && simulator.HasEnemyRuntime(0) ? simulator.GetEnemyMaxHp(0) : 0;
                    UpdatePreviewHpBar(
                        enemyPreviewHpBars.Count > 0 ? enemyPreviewHpBars[0] : null,
                        MapBattlefieldAnchor(bossAnchor),
                        bossPreviewSize,
                        bossMaxHp > 0,
                        1f - enemyVanishT,
                        bossCurrentHp,
                        bossMaxHp,
                        new Color(0.96f, 0.44f, 0.40f, 0.95f),
                        bossMotionOffset);
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

                ArrangeMonsterPreviewLayers();
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

                if (shouldShow && enemyVanishRemaining > 0f)
                {
                    scale *= Mathf.Lerp(1f, 0.24f, enemyVanishT);
                    alpha *= 1f - enemyVanishT;
                }

                if (isActiveSlot && slotProgress >= combatStartProgress)
                {
                    engagedEnemyPreviewCount += 1;
                }

                bool enemyMoving = slotProgress < combatStartProgress;
                BattleVisualPose enemyPose = ResolveEnemyPreviewPose(i, enemyMoving, false);
                Vector2 enemyPreviewSize = EnemyPreviewSize * (scale * ResolveEnemyPreviewScale(currentPreviewEnemyData, enemyPose));
                ApplyPreviewImageLayout(
                    image,
                    MapBattlefieldAnchor(anchor),
                    enemyPreviewSize);
                float enemyAttackRemaining = i < enemyAttackVisualRemainings.Count ? enemyAttackVisualRemainings[i] : 0f;
                Vector2 enemyMotionOffset = ResolvePresentationMotionOffset(i, false, enemyPose, enemyAttackRemaining, enemyAttackRange >= RangedAttackThreshold);
                SetImageSprite(image, SelectEnemyPreviewSprite(i, enemyMoving, false));
                BattleFacingDirection enemySourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, enemyPose);
                image.rectTransform.localScale = ResolveFacingScale(enemySourceFacing, BattleFacingDirection.Left);
                ApplyPreviewVisualLayout(image, enemyPreviewSize, enemyMotionOffset, ResolveEnemyPreviewReferenceSprites(enemyPose), ResolveEnemyPreviewMeasurementMode(currentPreviewEnemyData, enemyPose));

                Color color = image.color;
                color.a = alpha;
                image.color = color;

                int enemyCurrentHp = simulator != null && simulator.HasEnemyRuntime(i) ? simulator.GetEnemyCurrentHp(i) : 0;
                int enemyMaxHp = simulator != null && simulator.HasEnemyRuntime(i) ? simulator.GetEnemyMaxHp(i) : 0;
                UpdatePreviewHpBar(
                    i < enemyPreviewHpBars.Count ? enemyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(anchor),
                    enemyPreviewSize,
                    shouldShow && enemyMaxHp > 0,
                    alpha,
                    enemyCurrentHp,
                    enemyMaxHp,
                    new Color(0.96f, 0.44f, 0.40f, 0.95f),
                    enemyMotionOffset);
            }

            if (Application.isPlaying && stateMachine != null)
            {
                stateMachine.SetEngagedEnemyCount(engagedEnemyPreviewCount);
            }

            ArrangeMonsterPreviewLayers();
        }

        private static Image CreatePreviewImage(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(go);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.useSpriteMesh = false;
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
                float allyAttackRemaining = i < allyAttackVisualRemainings.Count ? allyAttackVisualRemainings[i] : 0f;
                float allyVanishT = AllyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(allyDefeatRemaining / AllyDefeatVanishDuration)
                    : 0f;
                float allyScale = allyAlive ? 1f : Mathf.Lerp(1f, 0.24f, allyVanishT);
                MonsterDataSO allyData = i < allyPreviewMonsterData.Count ? allyPreviewMonsterData[i] : null;
                float allyApproachT = allyMoving ? 0f : 1f;
                bool allyAttackEngaged = allyAlive && simulator.IsAllyAttackEngaged(i);
                BattleVisualPose allyPose = ResolveAllyPreviewPose(i, allyApproachT, allyAttackEngaged);
                Vector2 allyPreviewSize = AllyPreviewSize * (allyScale * ResolveMonsterPreviewScale(allyData, allyPose));
                ApplyPreviewImageLayout(
                    allyPreviewImages[i],
                    MapBattlefieldAnchor(allyAnchor),
                    allyPreviewSize);

                Sprite allySprite = SelectAllyPreviewSprite(i, allyApproachT, allyAttackEngaged);
                SetImageSprite(allyPreviewImages[i], allySprite);
                float allyAttackRange = i < allyAttackRanges.Count ? allyAttackRanges[i] : 1f;
                Vector2 allyMotionOffset = ResolvePresentationMotionOffset(i, true, allyPose, allyAttackRemaining, allyAttackRange >= RangedAttackThreshold);
                BattleFacingDirection allySourceFacing = BattleVisualResolver.ResolveMonsterFacing(allyData, allyPose);
                allyPreviewImages[i].rectTransform.localScale = ResolveFacingScale(allySourceFacing, BattleFacingDirection.Right);
                ApplyPreviewVisualLayout(allyPreviewImages[i], allyPreviewSize, allyMotionOffset, ResolveAllyPreviewReferenceSprites(i, allyPose), ResolveAllyPreviewMeasurementMode(allyData, allyPose));
                Color allyColor = allyPreviewImages[i].color;
                allyColor.a = allyAlive ? 1f : Mathf.Clamp01(1f - allyVanishT);
                allyPreviewImages[i].color = allyColor;

                UpdatePreviewHpBar(
                    i < allyPreviewHpBars.Count ? allyPreviewHpBars[i] : null,
                    MapBattlefieldAnchor(allyAnchor),
                    allyPreviewSize,
                    simulator.HasAllyRuntime(i),
                    allyColor.a,
                    simulator.GetAllyCurrentHp(i),
                    simulator.GetAllyMaxHp(i),
                    new Color(0.28f, 0.88f, 0.66f, 0.95f),
                    allyMotionOffset);
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
                float enemyAttackRemaining = i < enemyAttackVisualRemainings.Count ? enemyAttackVisualRemainings[i] : 0f;
                float enemyVanishT = enemyDefeatRemaining > 0f && EnemyDefeatVanishDuration > 0f
                    ? 1f - Mathf.Clamp01(enemyDefeatRemaining / EnemyDefeatVanishDuration)
                    : 0f;
                float scale = Mathf.Lerp(1f, 0.24f, enemyVanishT);
                bool enemyAttackEngaged = simulator.IsEnemyAttackEngaged(i);
                BattleVisualPose enemyPose = ResolveEnemyPreviewPose(i, enemyMoving, enemyAttackEngaged);
                float enemyPreviewScale = ResolveEnemyPreviewScale(currentPreviewEnemyData, enemyPose);
                Vector2 previewSize = (simulator.IsBossWave && i == 0 ? BossPreviewSize : EnemyPreviewSize) * (scale * enemyPreviewScale);
                ApplyPreviewImageLayout(image, MapBattlefieldAnchor(enemyAnchor), previewSize);

                SetImageSprite(image, SelectEnemyPreviewSprite(i, enemyMoving, enemyAttackEngaged));
                Vector2 enemyMotionOffset = ResolvePresentationMotionOffset(i, false, enemyPose, enemyAttackRemaining, enemyAttackRange >= RangedAttackThreshold);
                BattleFacingDirection enemySourceFacing = BattleVisualResolver.ResolveEnemyFacing(currentPreviewEnemyData, enemyPose);
                image.rectTransform.localScale = ResolveFacingScale(enemySourceFacing, BattleFacingDirection.Left);
                ApplyPreviewVisualLayout(image, previewSize, enemyMotionOffset, ResolveEnemyPreviewReferenceSprites(enemyPose), ResolveEnemyPreviewMeasurementMode(currentPreviewEnemyData, enemyPose));
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
                    new Color(0.96f, 0.44f, 0.40f, 0.95f),
                    enemyMotionOffset);
            }

            if (stateMachine != null)
            {
                stateMachine.SetEngagedEnemyCount(simulator.CurrentEngagedEnemyCount);
            }

            ArrangeMonsterPreviewLayers();
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
            fill.type = Image.Type.Simple;
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

        private void UpdatePreviewHpBar(PreviewHpBar hpBar, Vector2 anchor, Vector2 previewSize, bool visible, float allyColorAlpha, int currentHp, int maxHp, Color fillColor, Vector2 motionOffset = default)
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
            hpBar.Root.anchoredPosition = motionOffset + new Vector2(0f, (previewSize.y * 0.60f) + 12f);
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
                Color fill = fillColor;
                fill.a *= alpha;
                ApplyHorizontalImageFill(hpBar.Fill, (float)Mathf.Clamp(currentHp, 0, maxHp) / Mathf.Max(1, maxHp), fill);
            }

            if (hpBar.Label != null)
            {
                Color labelColor = hpBar.Label.color;
                labelColor.a = alpha;
                hpBar.Label.color = labelColor;
                hpBar.Label.text = $"{Mathf.Clamp(currentHp, 0, maxHp)}/{maxHp}";
            }
        }

        private static void ApplyHorizontalImageFill(Image fillImage, float ratio, Color color)
        {
            if (fillImage == null)
            {
                return;
            }

            float clampedRatio = Mathf.Clamp01(ratio);
            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = clampedRatio;
            fillImage.color = color;
            fillImage.gameObject.SetActive(clampedRatio > 0.001f);

            RectTransform fillRect = fillImage.rectTransform;
            if (fillRect == null)
            {
                return;
            }

            fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
            fillRect.anchorMax = new Vector2(clampedRatio, fillRect.anchorMax.y);
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
            if (!minimalMonsterPresentation || !Application.isPlaying)
            {
                return;
            }

            MonsterDataSO attackerMonsterData = ResolveHitAttackerMonsterData(hitInfo);
            MonsterAttackEffectDefinition monsterEffect = ResolveMonsterAttackEffect(hitInfo, attackerMonsterData);
            if (monsterEffect == null && !IsRangedAttackHit(hitInfo))
            {
                return;
            }

            EnsureMinimalCanvas();
            EnsureRangedEffectRoot();

            BattleAttackEffectProfileSO profile = monsterEffect != null ? null : ResolveAttackEffectProfile(hitInfo);
            bool useAttackerEdgeOffset = monsterEffect == null ||
                monsterEffect.Placement != MonsterAttackEffectPlacement.Beam;
            if (!TryResolveRangedAttackEndpoints(hitInfo, profile, useAttackerEdgeOffset, out Vector2 startPosition, out Vector2 endPosition))
            {
                return;
            }

            if (monsterEffect != null && SpawnMonsterAttackEffect(monsterEffect, startPosition, endPosition, hitInfo.TargetIsPlayer, attackerMonsterData))
            {
                return;
            }

            if (profile == null)
            {
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
                    ResolveSpriteBaseSize(profile.projectileSprite, scale, 46f),
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
                        ResolveSpriteBaseSize(profile.warningAirSprite, scale, 108f),
                        0f,
                        1.08f);
                }

                if (profile.HasWarningGroundSprite)
                {
                    SpawnStaticRangedAttackEffect(
                        profile.warningGroundSprite,
                        tint,
                        endPosition + profile.warningGroundOffset,
                        Mathf.Max(0.08f, profile.projectileDelay),
                        ResolveSpriteBaseSize(profile.warningGroundSprite, scale, 118f),
                        0f,
                        1.08f);
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
                        ResolveSpriteBaseSize(profile.projectileSprite, scale, 112f),
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
                    ResolveSpriteBaseSize(profile.impactSprite, scale, 108f),
                    0f,
                    1.18f);
            }

            if (profile.HasHitOverlaySprite)
            {
                SpawnStaticRangedAttackEffect(
                    profile.hitOverlaySprite,
                    tint,
                    endPosition + profile.targetOffset,
                    Mathf.Max(0f, profile.hitOverlayDelay),
                    ResolveSpriteBaseSize(profile.hitOverlaySprite, scale, 96f),
                    Mathf.Max(0.14f, profile.loopDuration),
                    1.10f);
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
                if (effect.Frames != null && effect.Frames.Count > 0)
                {
                    int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * effect.Frames.Count), 0, effect.Frames.Count - 1);
                    effect.Image.sprite = effect.Frames[frameIndex];
                }

                if (effect.UseBeamLayout)
                {
                    Vector2 beamDelta = effect.EndPosition - effect.StartPosition;
                    float beamLength = Mathf.Max(12f, beamDelta.magnitude + effect.BeamLengthPadding);
                    float beamAngle = Mathf.Atan2(beamDelta.y, beamDelta.x) * Mathf.Rad2Deg;
                    float beamFadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.10f, normalized));
                    float beamFadeOut = normalized >= 0.82f
                        ? Mathf.InverseLerp(0.82f, 1f, normalized)
                        : 0f;
                    float beamPulseStrength = effect.PulseStrength >= 0f ? effect.PulseStrength : 0.045f;
                    float beamPulse = 1f + Mathf.Sin(normalized * Mathf.PI * 4f) * beamPulseStrength;

                    effect.RectTransform.pivot = new Vector2(0f, 0.5f);
                    effect.RectTransform.anchoredPosition = effect.StartPosition;
                    effect.RectTransform.localEulerAngles = new Vector3(0f, 0f, beamAngle);
                    effect.RectTransform.sizeDelta = new Vector2(beamLength, Mathf.Max(8f, effect.BaseSize * beamPulse));

                    float beamGlow = 1f - Mathf.Abs((normalized * 2f) - 1f);
                    Color beamColor = effect.BaseColor;
                    beamColor.a = effect.AlphaMultiplier * beamFadeIn * Mathf.Lerp(1f, 0.10f, beamFadeOut);
                    beamColor = Color.Lerp(beamColor, Color.white, beamGlow * effect.GlowStrength);
                    effect.Image.color = beamColor;

                    if (normalized < 1f)
                    {
                        continue;
                    }

                    Destroy(effect.Image.gameObject);
                    activeRangedAttackEffects.RemoveAt(i);
                    continue;
                }

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

                float pulseStrength = effect.UseArcMovement ? 0.12f : 0.22f;
                if (effect.PulseStrength >= 0f)
                {
                    pulseStrength = effect.PulseStrength;
                }
                float pulseScale = 1f + (Mathf.Sin(normalized * Mathf.PI) * pulseStrength);
                float resolvedSize = Mathf.Min(
                    Mathf.Lerp(effect.BaseSize, effect.BaseSize * effect.FadeOutScale, fadeProgress) * pulseScale,
                    MaxAttackEffectLongestSide);
                effect.RectTransform.sizeDelta = ResolveRangedAttackEffectSizeDelta(effect.Image.sprite, resolvedSize);

                float glow = 1f - Mathf.Abs((normalized * 2f) - 1f);
                Color color = effect.BaseColor;
                color.a = Mathf.Lerp(effect.AlphaMultiplier, 0.12f * effect.AlphaMultiplier, fadeProgress);
                color = Color.Lerp(color, Color.white, glow * effect.GlowStrength);
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

        private MonsterDataSO ResolveHitAttackerMonsterData(BattleHitInfo hitInfo)
        {
            if (hitInfo.TargetIsPlayer)
            {
                if (currentPreviewEnemyData == null || string.IsNullOrEmpty(currentPreviewEnemyData.enemyId))
                {
                    return null;
                }

                string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(currentPreviewEnemyData.enemyId);
                return !string.IsNullOrEmpty(monsterId)
                    ? MasterDataManager.Instance?.GetMonsterData(monsterId)
                    : null;
            }

            return hitInfo.AttackerIndex >= 0 && hitInfo.AttackerIndex < allyPreviewMonsterData.Count
                ? allyPreviewMonsterData[hitInfo.AttackerIndex]
                : null;
        }

        private MonsterAttackEffectDefinition ResolveMonsterAttackEffect(BattleHitInfo hitInfo, MonsterDataSO attackerData)
        {
            if (attackerData == null || string.IsNullOrEmpty(attackerData.monsterId))
            {
                return null;
            }

            return MonsterAttackEffects.TryGetValue(attackerData.monsterId, out MonsterAttackEffectDefinition definition)
                ? definition
                : null;
        }

        private bool SpawnMonsterAttackEffect(MonsterAttackEffectDefinition definition, Vector2 startPosition, Vector2 endPosition, bool targetIsPlayer, MonsterDataSO attackerData)
        {
            if (definition == null || string.IsNullOrEmpty(definition.ResourcePath))
            {
                return false;
            }

            List<Sprite> frames = BattleVisualResolver.ResolveSpriteFramesFromResourcePath(definition.ResourcePath);
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            float direction = targetIsPlayer ? -1f : 1f;
            Vector2 projectileStart = startPosition + new Vector2(definition.StartOffset.x * direction, definition.StartOffset.y);
            Vector2 projectileEnd = endPosition + new Vector2(definition.TargetOffset.x * direction, definition.TargetOffset.y);
            bool isClass2 = IsClass2Monster(attackerData);
            bool isClass3 = IsClass3Monster(attackerData);
            Color class3FlourishTint = isClass3
                ? ResolveClass3EffectTint(definition.Tint, attackerData)
                : Color.white;
            Color tint = isClass3
                ? Color.white
                : isClass2
                    ? ResolveClass2EffectTint(definition.Tint, attackerData)
                    : definition.Tint;
            float scale = definition.Scale * (isClass3
                ? Class3AttackEffectScaleMultiplier
                : isClass2
                    ? Class2AttackEffectScaleMultiplier
                    : 1f);
            float duration = definition.Duration * (isClass3
                ? Class3AttackEffectDurationMultiplier
                : isClass2
                    ? Class2AttackEffectDurationMultiplier
                    : 1f);
            float arcHeight = definition.ArcHeight * (isClass3
                ? Class3AttackEffectArcMultiplier
                : isClass2
                    ? Class2AttackEffectArcMultiplier
                    : 1f);
            float fadeOutScale = definition.FadeOutScale * (isClass3
                ? Class3AttackEffectFadeOutMultiplier
                : isClass2
                    ? Class2AttackEffectFadeOutMultiplier
                    : 1f);
            float baseSize = ResolveSpriteBaseSize(frames[0], scale, 92f);
            float pulseStrength = isClass3 ? 0.14f : isClass2 ? Class2AttackEffectPulseStrength : -1f;
            float glowStrength = isClass3 ? 0.48f : isClass2 ? 0.58f : 0.34f;

            if (definition.Placement == MonsterAttackEffectPlacement.Beam)
            {
                EnsureBeamEndpointExtendsFromStart(ref projectileStart, ref projectileEnd, targetIsPlayer);
                float beamThickness = definition.BeamThickness > 0f
                    ? definition.BeamThickness * scale * AttackEffectGlobalScale
                    : baseSize;
                SpawnAnimatedBeamRangedAttackEffect(
                    frames,
                    tint,
                    projectileStart,
                    projectileEnd,
                    definition.StartDelay,
                    beamThickness,
                    duration,
                    definition.BeamLengthPadding * Mathf.Max(0.5f, Mathf.Abs(scale)),
                    1f,
                    0.045f,
                    0.42f);
                return true;
            }

            if (definition.Placement == MonsterAttackEffectPlacement.TargetBurst)
            {
                SpawnAnimatedStaticRangedAttackEffect(
                    frames,
                    tint,
                    projectileEnd,
                    definition.StartDelay,
                    baseSize,
                    duration,
                    fadeOutScale,
                    1f,
                    pulseStrength,
                    glowStrength);
                if (isClass3)
                {
                    SpawnClass3AttackFlourish(definition, frames, class3FlourishTint, projectileStart, projectileEnd, baseSize, duration, direction);
                }
                else if (isClass2)
                {
                    SpawnClass2AttackFlourish(definition, frames, tint, projectileStart, projectileEnd, baseSize, duration, direction);
                }
                return true;
            }

            if (definition.Placement == MonsterAttackEffectPlacement.CasterBurst)
            {
                SpawnAnimatedStaticRangedAttackEffect(
                    frames,
                    tint,
                    projectileStart,
                    definition.StartDelay,
                    baseSize,
                    duration,
                    fadeOutScale,
                    1f,
                    pulseStrength,
                    glowStrength);
                if (isClass3)
                {
                    SpawnClass3AttackFlourish(definition, frames, class3FlourishTint, projectileStart, projectileEnd, baseSize, duration, direction);
                }
                else if (isClass2)
                {
                    SpawnClass2AttackFlourish(definition, frames, tint, projectileStart, projectileEnd, baseSize, duration, direction);
                }
                return true;
            }

            SpawnAnimatedMovingRangedAttackEffect(
                frames,
                tint,
                projectileStart,
                projectileEnd,
                definition.StartDelay,
                arcHeight,
                baseSize,
                duration,
                fadeOutScale,
                1f,
                pulseStrength,
                glowStrength);
            if (isClass3)
            {
                SpawnClass3AttackFlourish(definition, frames, class3FlourishTint, projectileStart, projectileEnd, baseSize, duration, direction);
            }
            else if (isClass2)
            {
                SpawnClass2AttackFlourish(definition, frames, tint, projectileStart, projectileEnd, baseSize, duration, direction);
            }
            return true;
        }

        private static void EnsureBeamEndpointExtendsFromStart(ref Vector2 startPosition, ref Vector2 endPosition, bool targetIsPlayer)
        {
            float direction = targetIsPlayer ? -1f : 1f;
            float directedDistance = (endPosition.x - startPosition.x) * direction;
            if (directedDistance >= MinimumBeamEndpointDistance)
            {
                return;
            }

            endPosition.x = startPosition.x + (MinimumBeamEndpointDistance * direction);
        }

        private void SpawnClass2AttackFlourish(
            MonsterAttackEffectDefinition definition,
            List<Sprite> frames,
            Color tint,
            Vector2 projectileStart,
            Vector2 projectileEnd,
            float baseSize,
            float duration,
            float direction)
        {
            if (definition == null || frames == null || frames.Count == 0)
            {
                return;
            }

            Color echoTint = Color.Lerp(tint, Color.white, 0.38f);
            Vector2 echoDrift = new Vector2(6f * direction, 5f);

            if (definition.Placement == MonsterAttackEffectPlacement.Projectile)
            {
                SpawnAnimatedMovingRangedAttackEffect(
                    frames,
                    echoTint,
                    projectileStart - (echoDrift * 0.65f),
                    projectileEnd - (echoDrift * 0.35f),
                    definition.StartDelay + Class2AttackEffectEchoDelay,
                    definition.ArcHeight * Class2AttackEffectArcMultiplier * 0.92f,
                    baseSize * 0.84f,
                    duration * 0.86f,
                    1.02f,
                    0.58f,
                    Class2AttackEffectEchoPulseStrength,
                    0.54f);

                SpawnAnimatedStaticRangedAttackEffect(
                    frames,
                    echoTint,
                    projectileEnd + echoDrift,
                    definition.StartDelay + Mathf.Max(Class2AttackEffectSparkDelay, duration * 0.58f),
                    baseSize * Class2AttackEffectEchoScaleMultiplier,
                    Mathf.Max(0.12f, duration * 0.62f),
                    1.06f,
                    0.68f,
                    Class2AttackEffectEchoPulseStrength,
                    0.62f);
                return;
            }

            Vector2 burstPosition = definition.Placement == MonsterAttackEffectPlacement.CasterBurst
                ? projectileStart
                : projectileEnd;

            SpawnAnimatedStaticRangedAttackEffect(
                frames,
                echoTint,
                burstPosition + echoDrift,
                definition.StartDelay + Class2AttackEffectEchoDelay,
                baseSize * Class2AttackEffectEchoScaleMultiplier,
                Mathf.Max(0.12f, duration * 0.82f),
                1.06f,
                0.68f,
                Class2AttackEffectEchoPulseStrength,
                0.64f);

            SpawnAnimatedStaticRangedAttackEffect(
                frames,
                echoTint,
                burstPosition - (echoDrift * 0.5f),
                definition.StartDelay + Class2AttackEffectSparkDelay,
                baseSize * Class2AttackEffectSparkScaleMultiplier,
                Mathf.Max(0.10f, duration * 0.56f),
                1.02f,
                0.52f,
                Class2AttackEffectSparkPulseStrength,
                0.58f);
        }

        private void SpawnClass3AttackFlourish(
            MonsterAttackEffectDefinition definition,
            List<Sprite> frames,
            Color tint,
            Vector2 projectileStart,
            Vector2 projectileEnd,
            float baseSize,
            float duration,
            float direction)
        {
            if (definition == null || frames == null || frames.Count == 0)
            {
                return;
            }

            Color haloTint = Color.Lerp(tint, Color.white, 0.56f);
            Color deepTint = Color.Lerp(tint, new Color(0.78f, 0.36f, 1f, 1f), 0.32f);
            Vector2 echoDrift = new Vector2(6f * direction, 5f);

            if (definition.Placement == MonsterAttackEffectPlacement.Projectile)
            {
                SpawnAnimatedMovingRangedAttackEffect(
                    frames,
                    deepTint,
                    projectileStart - (echoDrift * 0.82f),
                    projectileEnd - (echoDrift * 0.48f),
                    definition.StartDelay + Class3AttackEffectEchoDelay,
                    definition.ArcHeight * Class3AttackEffectArcMultiplier * 0.78f,
                    baseSize * 0.44f,
                    duration * 0.72f,
                    1.04f,
                    0.24f,
                    0.14f,
                    0.34f);

                SpawnAnimatedStaticRangedAttackEffect(
                    frames,
                    haloTint,
                    projectileEnd + echoDrift,
                    definition.StartDelay + Mathf.Max(Class3AttackEffectFinishDelay, duration * 0.52f),
                    baseSize * Class3AttackEffectFinishScaleMultiplier,
                    Mathf.Max(0.14f, duration * 0.48f),
                    1.04f,
                    0.34f,
                    0.16f,
                    0.40f);
                return;
            }

            Vector2 burstPosition = definition.Placement == MonsterAttackEffectPlacement.CasterBurst
                ? projectileStart
                : projectileEnd;

            SpawnAnimatedStaticRangedAttackEffect(
                frames,
                deepTint,
                burstPosition - echoDrift,
                definition.StartDelay + Class3AttackEffectEchoDelay,
                baseSize * 0.58f,
                Mathf.Max(0.14f, duration * 0.64f),
                1.04f,
                0.26f,
                0.14f,
                0.34f);

            SpawnAnimatedStaticRangedAttackEffect(
                frames,
                haloTint,
                burstPosition + echoDrift,
                definition.StartDelay + Class3AttackEffectSecondaryDelay,
                baseSize * Class3AttackEffectFinishScaleMultiplier,
                Mathf.Max(0.16f, duration * 0.58f),
                1.04f,
                0.34f,
                0.16f,
                0.40f);
        }

        private static bool IsClass2Monster(MonsterDataSO monsterData)
        {
            return monsterData != null && monsterData.classRank == 2;
        }

        private static bool IsClass3Monster(MonsterDataSO monsterData)
        {
            return monsterData != null && monsterData.classRank == 3;
        }

        private static Color ResolveClass2EffectTint(Color baseTint, MonsterDataSO monsterData)
        {
            Color tint = baseTint.a > 0f ? baseTint : Color.white;
            Color accent = new Color(0.74f, 0.92f, 1f, 1f);
            if (monsterData != null)
            {
                switch (monsterData.element)
                {
                    case MonsterElement.Fire:
                        accent = new Color(1f, 0.58f, 0.22f, 1f);
                        break;
                    case MonsterElement.Wood:
                        accent = new Color(0.54f, 1f, 0.58f, 1f);
                        break;
                    case MonsterElement.Water:
                        accent = new Color(0.48f, 0.82f, 1f, 1f);
                        break;
                    case MonsterElement.Light:
                        accent = new Color(1f, 0.92f, 0.48f, 1f);
                        break;
                    case MonsterElement.Dark:
                        accent = new Color(0.78f, 0.54f, 1f, 1f);
                        break;
                }
            }

            return Color.Lerp(tint, accent, 0.42f);
        }

        private static Color ResolveClass3EffectTint(Color baseTint, MonsterDataSO monsterData)
        {
            Color tint = baseTint.a > 0f ? baseTint : Color.white;
            Color accent = new Color(0.88f, 0.64f, 1f, 1f);
            if (monsterData != null)
            {
                switch (monsterData.element)
                {
                    case MonsterElement.Fire:
                        accent = new Color(1f, 0.36f, 0.08f, 1f);
                        break;
                    case MonsterElement.Wood:
                        accent = new Color(0.22f, 1f, 0.58f, 1f);
                        break;
                    case MonsterElement.Water:
                        accent = new Color(0.22f, 0.92f, 1f, 1f);
                        break;
                    case MonsterElement.Light:
                        accent = new Color(1f, 0.96f, 0.36f, 1f);
                        break;
                    case MonsterElement.Dark:
                        accent = new Color(0.74f, 0.28f, 1f, 1f);
                        break;
                }
            }

            return Color.Lerp(tint, accent, 0.62f);
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

        private bool TryResolveRangedAttackEndpoints(
            BattleHitInfo hitInfo,
            BattleAttackEffectProfileSO profile,
            bool useAttackerEdgeOffset,
            out Vector2 startPosition,
            out Vector2 endPosition)
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
                if (useAttackerEdgeOffset)
                {
                    startPosition.x += startOffset;
                }
                endPosition.x -= endOffset;
            }
            else
            {
                if (useAttackerEdgeOffset)
                {
                    startPosition.x -= startOffset;
                }
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

        private void SpawnMovingRangedAttackEffect(
            Sprite sprite,
            Color tint,
            Vector2 startPosition,
            Vector2 endPosition,
            float duration,
            float arcHeight,
            float baseSize,
            float startDelay,
            float alphaMultiplier = 1f,
            float pulseStrength = -1f,
            float glowStrength = 0.34f)
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
                AlphaMultiplier = Mathf.Clamp01(alphaMultiplier),
                PulseStrength = pulseStrength,
                GlowStrength = Mathf.Max(0f, glowStrength),
                UseArcMovement = true
            });
        }

        private void SpawnAnimatedMovingRangedAttackEffect(
            List<Sprite> frames,
            Color tint,
            Vector2 startPosition,
            Vector2 endPosition,
            float startDelay,
            float arcHeight,
            float baseSize,
            float duration,
            float fadeOutScale,
            float alphaMultiplier = 1f,
            float pulseStrength = -1f,
            float glowStrength = 0.34f)
        {
            if (frames == null || frames.Count == 0 || frames[0] == null)
            {
                return;
            }

            Image image = CreateRangedEffectImage(frames[0], tint);
            activeRangedAttackEffects.Add(new ActiveRangedAttackEffect
            {
                Image = image,
                RectTransform = image.rectTransform,
                BaseColor = tint,
                StartPosition = startPosition,
                EndPosition = endPosition,
                Duration = Mathf.Max(0.08f, duration),
                ArcHeight = arcHeight,
                BaseSize = Mathf.Max(8f, baseSize),
                StartDelay = Mathf.Max(0f, startDelay),
                FadeOutScale = Mathf.Clamp(fadeOutScale, 0.4f, 1.35f),
                AlphaMultiplier = Mathf.Clamp01(alphaMultiplier),
                PulseStrength = pulseStrength,
                GlowStrength = Mathf.Max(0f, glowStrength),
                UseArcMovement = true,
                Frames = frames
            });
        }

        private void SpawnAnimatedStaticRangedAttackEffect(
            List<Sprite> frames,
            Color tint,
            Vector2 position,
            float startDelay,
            float baseSize,
            float duration,
            float fadeOutScale,
            float alphaMultiplier = 1f,
            float pulseStrength = -1f,
            float glowStrength = 0.34f)
        {
            if (frames == null || frames.Count == 0 || frames[0] == null)
            {
                return;
            }

            Image image = CreateRangedEffectImage(frames[0], tint);
            activeRangedAttackEffects.Add(new ActiveRangedAttackEffect
            {
                Image = image,
                RectTransform = image.rectTransform,
                BaseColor = tint,
                StaticPosition = position,
                Duration = Mathf.Max(0.08f, duration),
                BaseSize = Mathf.Max(8f, baseSize),
                StartDelay = Mathf.Max(0f, startDelay),
                FadeOutScale = Mathf.Clamp(fadeOutScale, 0.4f, 1.35f),
                AlphaMultiplier = Mathf.Clamp01(alphaMultiplier),
                PulseStrength = pulseStrength,
                GlowStrength = Mathf.Max(0f, glowStrength),
                UseArcMovement = false,
                Frames = frames
            });
        }

        private void SpawnAnimatedBeamRangedAttackEffect(
            List<Sprite> frames,
            Color tint,
            Vector2 startPosition,
            Vector2 endPosition,
            float startDelay,
            float beamThickness,
            float duration,
            float lengthPadding,
            float alphaMultiplier = 1f,
            float pulseStrength = -1f,
            float glowStrength = 0.34f)
        {
            if (frames == null || frames.Count == 0 || frames[0] == null)
            {
                return;
            }

            Image image = CreateRangedEffectImage(frames[0], tint);
            image.preserveAspect = false;
            activeRangedAttackEffects.Add(new ActiveRangedAttackEffect
            {
                Image = image,
                RectTransform = image.rectTransform,
                BaseColor = tint,
                StartPosition = startPosition,
                EndPosition = endPosition,
                Duration = Mathf.Max(0.10f, duration),
                BaseSize = Mathf.Max(8f, beamThickness),
                StartDelay = Mathf.Max(0f, startDelay),
                AlphaMultiplier = Mathf.Clamp01(alphaMultiplier),
                PulseStrength = pulseStrength,
                GlowStrength = Mathf.Max(0f, glowStrength),
                UseArcMovement = false,
                UseBeamLayout = true,
                BeamLengthPadding = Mathf.Max(0f, lengthPadding),
                Frames = frames
            });
        }

        private void SpawnStaticRangedAttackEffect(
            Sprite sprite,
            Color tint,
            Vector2 position,
            float startDelay,
            float baseSize,
            float sustainDuration,
            float fadeOutScale,
            float alphaMultiplier = 1f,
            float pulseStrength = -1f,
            float glowStrength = 0.34f)
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
                FadeOutScale = Mathf.Clamp(fadeOutScale, 0.4f, 1.35f),
                AlphaMultiplier = Mathf.Clamp01(alphaMultiplier),
                PulseStrength = pulseStrength,
                GlowStrength = Mathf.Max(0f, glowStrength),
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

        private static Vector2 ResolveRangedAttackEffectSizeDelta(Sprite sprite, float longestSide)
        {
            float safeLongestSide = Mathf.Max(1f, longestSide);
            if (sprite == null)
            {
                return Vector2.one * safeLongestSide;
            }

            Rect rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return Vector2.one * safeLongestSide;
            }

            if (rect.width >= rect.height)
            {
                return new Vector2(safeLongestSide, safeLongestSide * rect.height / rect.width);
            }

            return new Vector2(safeLongestSide * rect.width / rect.height, safeLongestSide);
        }

        private static float ResolveSpriteBaseSize(Sprite sprite, float scale, float fallbackSize)
        {
            if (sprite == null)
            {
                return fallbackSize * scale * AttackEffectGlobalScale;
            }

            Rect rect = sprite.rect;
            float longestSide = Mathf.Max(rect.width, rect.height);
            float normalizedSize = Mathf.Lerp(44f, 148f, Mathf.Clamp01(longestSide / 512f));
            return normalizedSize * scale * AttackEffectGlobalScale;
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

            }
            minimalCanvasRoot.transform.localScale = Vector3.one;

            Canvas canvasComponent = minimalCanvasRoot.GetComponent<Canvas>();
            if (canvasComponent != null)
            {
                canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasComponent.pixelPerfect = true;
            }

            CanvasScaler canvasScaler = minimalCanvasRoot.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1080f, 1920f);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
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

        private void ShowMinimalResultOverlay(BattleResultViewData viewData)
        {
            if (!minimalMonsterPresentation)
            {
                return;
            }

            EnsureMinimalCanvas();
            EnsureMinimalResultOverlay();
            if (minimalResultOverlayRoot == null)
            {
                return;
            }

            minimalResultOverlayRoot.SetActive(true);
            minimalResultOverlayRoot.transform.SetAsLastSibling();

            if (minimalResultTitleText != null)
            {
                minimalResultTitleText.text = viewData.IsWin ? "勝利" : "敗北";
                minimalResultTitleText.color = viewData.IsWin
                    ? new Color(1f, 0.88f, 0.42f, 1f)
                    : new Color(1f, 0.55f, 0.55f, 1f);
            }

            if (minimalResultSummaryText != null)
            {
                minimalResultSummaryText.text = viewData.IsWin
                    ? $"第{viewData.ClearedFloor}階層を突破\n次の階層: 第{viewData.NextFloor}階層"
                    : "戦闘に敗北しました\n編成や装備を見直しましょう";
            }

            if (minimalResultRewardText != null)
            {
                minimalResultRewardText.text = BuildMinimalResultRewardText(viewData);
            }

            ShowMinimalResultRewardVisuals(viewData);

            if (minimalResultForecastText != null)
            {
                minimalResultForecastText.text = string.Empty;
                minimalResultForecastText.gameObject.SetActive(false);
            }

            if (minimalResultNextFloorButton != null)
            {
                minimalResultNextFloorButton.gameObject.SetActive(viewData.IsWin);
                minimalResultNextFloorButton.onClick.RemoveAllListeners();
                minimalResultNextFloorButton.onClick.AddListener(GoToNextFloor);
            }

            if (minimalResultNextFloorButtonText != null)
            {
                minimalResultNextFloorButtonText.text = $"第{viewData.NextFloor}階層へ";
            }

            if (minimalResultRetryFloorButton != null)
            {
                minimalResultRetryFloorButton.gameObject.SetActive(true);
                minimalResultRetryFloorButton.onClick.RemoveAllListeners();
                minimalResultRetryFloorButton.onClick.AddListener(RetryClearedFloor);
            }

            if (minimalResultRetryFloorButtonText != null)
            {
                minimalResultRetryFloorButtonText.text = "この階層に再挑戦";
            }

            if (minimalResultHomeButton != null)
            {
                minimalResultHomeButton.gameObject.SetActive(true);
                minimalResultHomeButton.onClick.RemoveAllListeners();
                minimalResultHomeButton.onClick.AddListener(ReturnHome);
            }

            if (minimalResultHomeButtonText != null)
            {
                minimalResultHomeButtonText.text = "ホームへ戻る";
            }
        }

        private void HideMinimalResultOverlay()
        {
            if (minimalResultOverlayRoot != null)
            {
                minimalResultOverlayRoot.SetActive(false);
            }
        }

        private void EnsureMinimalResultOverlay()
        {
            if (minimalCanvasRoot == null)
            {
                return;
            }

            if (minimalResultOverlayRoot == null)
            {
                Transform existing = minimalCanvasRoot.transform.Find("BattleMinimalResultOverlay");
                if (existing != null)
                {
                    minimalResultOverlayRoot = existing.gameObject;
                    minimalResultTitleText = existing.Find("ResultCard/Title")?.GetComponent<Text>();
                    minimalResultSummaryText = existing.Find("ResultCard/Summary")?.GetComponent<Text>();
                    minimalResultRewardText = existing.Find("ResultCard/Rewards")?.GetComponent<Text>();
                    minimalResultForecastText = existing.Find("ResultCard/Forecast")?.GetComponent<Text>();
                    minimalResultRewardVisualRoot = existing.Find("ResultCard/RewardVisuals")?.gameObject;
                    minimalResultNextFloorButton = existing.Find("ResultCard/NextFloorButton")?.GetComponent<Button>();
                    minimalResultRetryFloorButton = existing.Find("ResultCard/RetryFloorButton")?.GetComponent<Button>();
                    minimalResultHomeButton = existing.Find("ResultCard/HomeButton")?.GetComponent<Button>();
                    minimalResultNextFloorButtonText = existing.Find("ResultCard/NextFloorButton/Label")?.GetComponent<Text>();
                    minimalResultRetryFloorButtonText = existing.Find("ResultCard/RetryFloorButton/Label")?.GetComponent<Text>();
                    minimalResultHomeButtonText = existing.Find("ResultCard/HomeButton/Label")?.GetComponent<Text>();
                }
            }

            if (minimalResultOverlayRoot != null)
            {
                EnsureMinimalResultActionButtons();
                return;
            }

            minimalResultOverlayRoot = new GameObject("BattleMinimalResultOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(minimalResultOverlayRoot);
            RectTransform overlayRect = minimalResultOverlayRoot.GetComponent<RectTransform>();
            overlayRect.SetParent(minimalCanvasRoot.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = minimalResultOverlayRoot.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.48f);
            overlayImage.raycastTarget = true;

            GameObject card = new GameObject("ResultCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(card);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.SetParent(overlayRect, false);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(920f, 640f);
            Image cardImage = card.GetComponent<Image>();
            Sprite panelSprite = BattleVisualResolver.LoadSprite(ResultPanelResourcePath);
            if (panelSprite != null)
            {
                cardImage.sprite = panelSprite;
                cardImage.color = Color.white;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = false;
            }
            else
            {
                cardImage.color = new Color(0.05f, 0.07f, 0.09f, 0.97f);
            }

            cardImage.raycastTarget = true;

            minimalResultTitleText = CreateMinimalResultText("Title", card.transform, new Vector2(0.06f, 0.83f), new Vector2(0.94f, 0.96f), 54, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.42f, 1f));
            minimalResultSummaryText = CreateMinimalResultText("Summary", card.transform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.80f), 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 0.95f, 0.92f, 1f));
            minimalResultRewardText = CreateMinimalResultText("Rewards", card.transform, new Vector2(0.10f, 0.46f), new Vector2(0.90f, 0.66f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.84f, 0.93f, 1f, 1f));
            minimalResultRewardVisualRoot = CreateMinimalResultRewardVisualRoot(card.transform);
            minimalResultForecastText = CreateMinimalResultText("Forecast", card.transform, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.31f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.66f, 1f));
            minimalResultForecastText.gameObject.SetActive(false);
            minimalResultNextFloorButton = CreateMinimalResultButton("NextFloorButton", card.transform, new Vector2(0.06f, 0.08f), new Vector2(0.32f, 0.19f), new Color(0.15f, 0.43f, 0.70f, 1f), out minimalResultNextFloorButtonText);
            minimalResultRetryFloorButton = CreateMinimalResultButton("RetryFloorButton", card.transform, new Vector2(0.37f, 0.08f), new Vector2(0.63f, 0.19f), new Color(0.28f, 0.39f, 0.24f, 1f), out minimalResultRetryFloorButtonText);
            minimalResultHomeButton = CreateMinimalResultButton("HomeButton", card.transform, new Vector2(0.68f, 0.08f), new Vector2(0.94f, 0.19f), new Color(0.50f, 0.24f, 0.08f, 1f), out minimalResultHomeButtonText);
        }

        private void EnsureMinimalResultActionButtons()
        {
            Transform card = minimalResultOverlayRoot != null
                ? minimalResultOverlayRoot.transform.Find("ResultCard")
                : null;
            if (card == null)
            {
                return;
            }

            if (minimalResultNextFloorButton == null)
            {
                minimalResultNextFloorButton = card.Find("NextFloorButton")?.GetComponent<Button>();
            }

            if (minimalResultRetryFloorButton == null)
            {
                minimalResultRetryFloorButton = card.Find("RetryFloorButton")?.GetComponent<Button>();
            }

            if (minimalResultHomeButton == null)
            {
                minimalResultHomeButton = card.Find("HomeButton")?.GetComponent<Button>();
            }

            if (minimalResultNextFloorButton == null)
            {
                minimalResultNextFloorButton = CreateMinimalResultButton("NextFloorButton", card, new Vector2(0.06f, 0.08f), new Vector2(0.32f, 0.19f), new Color(0.15f, 0.43f, 0.70f, 1f), out minimalResultNextFloorButtonText);
            }

            if (minimalResultRetryFloorButton == null)
            {
                minimalResultRetryFloorButton = CreateMinimalResultButton("RetryFloorButton", card, new Vector2(0.37f, 0.08f), new Vector2(0.63f, 0.19f), new Color(0.28f, 0.39f, 0.24f, 1f), out minimalResultRetryFloorButtonText);
            }

            if (minimalResultHomeButton == null)
            {
                minimalResultHomeButton = CreateMinimalResultButton("HomeButton", card, new Vector2(0.68f, 0.08f), new Vector2(0.94f, 0.19f), new Color(0.50f, 0.24f, 0.08f, 1f), out minimalResultHomeButtonText);
            }

            ConfigureMinimalResultButtonRect(minimalResultNextFloorButton, new Vector2(0.06f, 0.08f), new Vector2(0.32f, 0.19f));
            ConfigureMinimalResultButtonRect(minimalResultRetryFloorButton, new Vector2(0.37f, 0.08f), new Vector2(0.63f, 0.19f));
            ConfigureMinimalResultButtonRect(minimalResultHomeButton, new Vector2(0.68f, 0.08f), new Vector2(0.94f, 0.19f));

            minimalResultNextFloorButtonText = minimalResultNextFloorButtonText != null
                ? minimalResultNextFloorButtonText
                : minimalResultNextFloorButton?.transform.Find("Label")?.GetComponent<Text>();
            minimalResultRetryFloorButtonText = minimalResultRetryFloorButtonText != null
                ? minimalResultRetryFloorButtonText
                : minimalResultRetryFloorButton?.transform.Find("Label")?.GetComponent<Text>();
            minimalResultHomeButtonText = minimalResultHomeButtonText != null
                ? minimalResultHomeButtonText
                : minimalResultHomeButton?.transform.Find("Label")?.GetComponent<Text>();
        }

        private static void ConfigureMinimalResultButtonRect(Button button, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ShowMinimalResultRewardVisuals(BattleResultViewData viewData)
        {
            if (minimalResultRewardVisualRoot == null && minimalResultOverlayRoot != null)
            {
                Transform card = minimalResultOverlayRoot.transform.Find("ResultCard");
                if (card != null)
                {
                    minimalResultRewardVisualRoot = CreateMinimalResultRewardVisualRoot(card);
                }
            }

            if (minimalResultRewardVisualRoot == null)
            {
                return;
            }

            ClearMinimalResultRewardVisuals();

            BattleResultRewardVisual[] visuals = viewData.RewardVisuals;
            if (!viewData.IsWin || visuals == null || visuals.Length == 0)
            {
                minimalResultRewardVisualRoot.SetActive(false);
                return;
            }

            minimalResultRewardVisualRoot.SetActive(true);
            int count = Mathf.Min(visuals.Length, 4);
            float spacing = count > 1 ? 148f : 0f;
            float startX = -((count - 1) * spacing * 0.5f);
            for (int i = 0; i < count; i += 1)
            {
                GameObject slot = CreateMinimalResultRewardVisual(visuals[i], minimalResultRewardVisualRoot.transform);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                minimalResultRewardVisualObjects.Add(slot);
            }
        }

        private static GameObject CreateMinimalResultRewardVisualRoot(Transform parent)
        {
            GameObject root = new GameObject("RewardVisuals", typeof(RectTransform));
            RegisterSceneObjectIfEditing(root);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.08f, 0.23f);
            rect.anchorMax = new Vector2(0.92f, 0.43f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private static GameObject CreateMinimalResultRewardVisual(BattleResultRewardVisual visual, Transform parent)
        {
            GameObject slot = new GameObject("RewardVisualSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(slot);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(136f, 150f);

            Image background = slot.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.05f, 0.58f);
            background.raycastTarget = false;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(iconObject);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(slot.transform, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 20f);
            iconRect.sizeDelta = new Vector2(82f, 82f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = BattleVisualResolver.LoadSprite(visual.IconResourcePath);
            icon.color = icon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject frameObject = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RegisterSceneObjectIfEditing(frameObject);
            RectTransform frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.SetParent(slot.transform, false);
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = new Vector2(0f, 20f);
            frameRect.sizeDelta = new Vector2(116f, 116f);
            Image frame = frameObject.GetComponent<Image>();
            frame.sprite = BattleVisualResolver.LoadSprite(visual.FrameResourcePath);
            frame.color = frame.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            frame.preserveAspect = true;
            frame.raycastTarget = false;

            Text label = CreateMinimalResultText("Label", slot.transform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.23f), 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.text = visual.DisplayName;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 14;

            if (!string.IsNullOrEmpty(visual.DetailText))
            {
                Text detail = CreateMinimalResultText("Detail", slot.transform, new Vector2(0.12f, 0.77f), new Vector2(0.88f, 0.92f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.68f, 1f));
                detail.text = visual.DetailText;
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 8;
                detail.resizeTextMaxSize = 13;
            }

            return slot;
        }

        private void ClearMinimalResultRewardVisuals()
        {
            if (minimalResultRewardVisualRoot == null)
            {
                for (int i = 0; i < minimalResultRewardVisualObjects.Count; i += 1)
                {
                    DestroyMinimalResultObject(minimalResultRewardVisualObjects[i]);
                }

                minimalResultRewardVisualObjects.Clear();
                return;
            }

            for (int i = minimalResultRewardVisualRoot.transform.childCount - 1; i >= 0; i -= 1)
            {
                Transform child = minimalResultRewardVisualRoot.transform.GetChild(i);
                if (child == null || child.name != "RewardVisualSlot")
                {
                    continue;
                }

                DestroyMinimalResultObject(child.gameObject);
            }

            minimalResultRewardVisualObjects.Clear();
        }

        private static void DestroyMinimalResultObject(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private static Text CreateMinimalResultText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RegisterSceneObjectIfEditing(textObject);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = ResolveBuiltinUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Button CreateMinimalResultButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, out Text label)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RegisterSceneObjectIfEditing(buttonObject);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            label = CreateMinimalResultText("Label", buttonObject.transform, Vector2.zero, Vector2.one, 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 26;
            label.raycastTarget = false;
            return button;
        }

        private static string BuildMinimalResultRewardText(BattleResultViewData viewData)
        {
            if (!viewData.IsWin)
            {
                return "今回の獲得報酬はありません。\n次の挑戦に向けて強化しましょう。";
            }

            var lines = new List<string>
            {
                $"ゴールド +{viewData.Gold:N0}",
                $"プレイヤー経験値 +{viewData.Exp:N0}",
                viewData.PartyMonsterCount > 0
                    ? $"パーティ経験値 +{viewData.PartyMonsterExp:N0} / {viewData.PartyMonsterCount}体"
                    : "パーティ経験値 なし"
            };

            if (viewData.PlayerLevelBefore > 0 && viewData.PlayerLevelAfter > viewData.PlayerLevelBefore)
            {
                lines.Add($"レベルアップ: Lv.{viewData.PlayerLevelBefore} -> Lv.{viewData.PlayerLevelAfter}");
            }

            return string.Join("\n", lines);
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
                    waveEnemyCountFillImage = waveHudRoot.transform.Find("EnemyCountBarFrame/EnemyCountFill")?.GetComponent<Image>();
                }

                if (waveEnemyCountText == null)
                {
                    waveEnemyCountText = ResolveLegacyHudText("EnemyCountText", TextAnchor.MiddleRight, 34);
                }

                if (waveTitleText == null)
                {
                    waveTitleText = ResolveLegacyHudText("WaveTitleText", TextAnchor.MiddleLeft, 28);
                }

                if (battleStatusText == null)
                {
                    battleStatusText = ResolveLegacyHudText("BattleStatusText", TextAnchor.MiddleCenter, 44);
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
            waveEnemyCountFillImage.type = Image.Type.Simple;

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
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Text ResolveLegacyHudText(string objectName, TextAnchor alignment, int fontSize)
        {
            if (waveHudRoot == null)
            {
                return null;
            }

            Transform child = waveHudRoot.transform.Find(objectName);
            if (child == null)
            {
                return null;
            }

            TMP_Text tmp = child.GetComponent<TMP_Text>();
            if (tmp != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(tmp);
#else
                Destroy(tmp);
#endif
            }

            Text text = child.GetComponent<Text>();
            if (text == null)
            {
                text = child.gameObject.AddComponent<Text>();
            }

            text.alignment = alignment;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.font = ResolveBuiltinUiFont();
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
                waveTitleText.fontStyle = FontStyle.Bold;
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
                    waveEnemyCountText.text = "残り 0 / 0";
                }

                if (waveEnemyCountFillImage != null)
                {
                    ApplyHorizontalImageFill(waveEnemyCountFillImage, 0f, new Color(0.60f, 0.86f, 0.24f, 1f));
                }

                if (battleStatusText != null)
                {
                    battleStatusText.text = string.Empty;
                }

                return;
            }

            int totalCount = simulator.CurrentEnemyCountTarget;
            int remainingCount = Mathf.Clamp(simulator.CurrentRemainingEnemyCount, 0, totalCount);
            float fill = totalCount > 0 ? (float)remainingCount / totalCount : 0f;
            string countText = $"残り {remainingCount} / {totalCount}";

            if (waveTitleText != null)
            {
                waveTitleText.text = simulator.IsBossWave ? "BOSS" : "ENEMY";
            }

            if (waveEnemyCountText != null)
            {
                waveEnemyCountText.text = countText;
            }

            if (waveEnemyCountFillImage != null)
            {
                ApplyHorizontalImageFill(waveEnemyCountFillImage, fill, new Color(0.60f, 0.86f, 0.24f, 1f));
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
            if (resultHandled && hasLastResultViewData &&
                (minimalResultOverlayRoot == null || !minimalResultOverlayRoot.activeInHierarchy))
            {
                ShowMinimalResultOverlay(lastResultViewData);
            }

            BattleSimulator simulator = stateMachine != null ? stateMachine.Simulator : null;
            bool hasSpawnedEnemies = simulator != null && simulator.CurrentSpawnedEnemyCount > 0;

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
        }

        private void ApplyHitReaction(BattleHitInfo hitInfo)
        {
            // Knockback has been removed from the battle presentation.
        }

        private void HandleEnemyDefeated(int _, int defeatedPreviewIndex)
        {
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

            ArrangeMonsterPreviewLayers();
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
