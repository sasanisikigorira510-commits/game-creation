using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Core;
using WitchTower.Home;
using WitchTower.UI;

public static class UnityMcpSceneBuilder
{
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string HomeScenePath = "Assets/Scenes/HomeScene.unity";
    private const string FormationScenePath = "Assets/Scenes/FormationScene.unity";
    private const string EquipmentScenePath = "Assets/Scenes/EquipmentScene.unity";
    private const string FusionScenePath = "Assets/Scenes/FusionScene.unity";
    private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
    private const string UiPresentationCameraName = "UiPresentationCamera";
    private const string WitchSpritePath = "Assets/Art/External/Derived/witch_idle.png";
    private const string WitchCastSpritePath = "Assets/Art/External/Derived/witch_cast.png";
    private const string EnemySpritePath = "Assets/Art/External/Derived/enemy_death_mage_elf.png";
    private const string TreeSpritePath = "Assets/Art/External/Derived/tree.png";
    private const string DirtTilePath = "Assets/Art/External/Derived/dirt_tile.png";
    private const string GrassTilePath = "Assets/Art/External/Derived/grass_tile.png";
    private const string TitleBackgroundPath = "Assets/Art/External/TitleBackground.png";
    private const string HomeChamberBackgroundPath = "Assets/Art/External/HomeChamberBackground.png";

    [MenuItem("Tools/MCP/Rebuild All Minimal Scenes")]
    public static void RebuildAllMinimalScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] Scene rebuild skipped during play mode.");
            return;
        }

        RebuildMinimalTitleScene();
        RebuildMinimalHomeScene();
        RebuildFusionScene();
        RebuildMinimalBattleScene();
    }

    [MenuItem("Tools/MCP/Ensure UI Scene Cameras")]
    public static void EnsureUiSceneCameras()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] UI scene camera update skipped during play mode.");
            return;
        }

        string activeScenePath = SceneManager.GetActiveScene().path;
        string[] scenePaths =
        {
            TitleScenePath,
            HomeScenePath,
            FormationScenePath,
            EquipmentScenePath,
            FusionScenePath
        };

        foreach (string scenePath in scenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureUiPresentationCameraInOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(activeScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScenePath) != null)
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/MCP/Rebuild Minimal Title Scene")]
    public static void RebuildMinimalTitleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] Title scene rebuild skipped during play mode.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        ClearScene(scene);

        EnsureEventSystem();
        Canvas canvas = CreateCanvas("TitleCanvas");
        CreateSpriteImage(canvas.transform, "TitleBackground", TitleBackgroundPath, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1920f), false);
        GameObject titleBackgroundShade = CreateUiObject("TitleBackgroundShade", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        StyleSurface(titleBackgroundShade, new Color(0.05f, 0.06f, 0.10f, 0.48f));
        CreateGlow(canvas.transform, "TitleGlowLeft", new Vector2(0.18f, 0.84f), Vector2.zero, new Vector2(420f, 420f), new Color(0.17f, 0.45f, 0.85f, 0.18f));
        CreateGlow(canvas.transform, "TitleGlowRight", new Vector2(0.84f, 0.18f), Vector2.zero, new Vector2(480f, 480f), new Color(0.63f, 0.16f, 0.22f, 0.13f));
        CreateBackdropBand(canvas.transform, "TitleBandTop", new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1080f, 220f), new Color(0.14f, 0.18f, 0.26f, 0.18f));
        CreateBackdropBand(canvas.transform, "TitleBandBottom", new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(1080f, 280f), new Color(0.10f, 0.12f, 0.18f, 0.22f));
        CreateSpriteTileStrip(canvas.transform, "TitleGround", DirtTilePath, new Vector2(0.5f, 0f), new Vector2(0f, 108f), 20, 3, 48f);
        CreateTowerTotem(canvas.transform, "TitleTotemLeft", new Vector2(0f, 0.5f), new Vector2(84f, -12f), new Color(0.23f, 0.58f, 0.84f, 0.14f), true);
        CreateTowerTotem(canvas.transform, "TitleTotemRight", new Vector2(1f, 0.5f), new Vector2(-84f, -12f), new Color(0.86f, 0.36f, 0.28f, 0.12f), false);
        CreateSceneRibbon(canvas.transform, "TitleTopRibbon", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(980f, 26f), "WITCH TOWER // CYCLE ENTRY", new Color(0.10f, 0.13f, 0.19f, 0.94f), new Color(0.97f, 0.85f, 0.58f, 0.96f));
        CreateSceneRibbon(canvas.transform, "TitleBottomRibbon", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(980f, 26f), "CLIMB · CASH OUT · REFORGE · DESCEND", new Color(0.10f, 0.13f, 0.19f, 0.94f), new Color(0.72f, 0.86f, 0.96f, 0.92f));

        GameObject frame = CreateUiObject("TitleFrame", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(880f, 1320f));
        StyleSurface(frame, new Color(0.08f, 0.10f, 0.15f, 0.9f));
        CreateInsetFrame(frame.transform, "TitleFrameInset", new Vector2(836f, 1272f), new Color(0.95f, 0.84f, 0.56f, 0.08f));

        TMP_Text overline = CreateLabel("Overline", frame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(540f, 32f), "Relic Chamber");
        StyleText(overline, 20f, FontStyles.Bold, new Color(1f, 0.85f, 0.56f, 0.96f), TextAlignmentOptions.Center);
        CreateTitleSigil(frame.transform, "TitleSigil", new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Color(0.97f, 0.84f, 0.58f, 0.22f));
        TMP_Text title = CreateLabel("GameTitle", frame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(700f, 90f), "Witch Tower");
        StyleText(title, 74f, FontStyles.Bold, new Color(0.96f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text subtitle = CreateLabel("GameSubtitle", frame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(700f, 54f), "Break the cycle. Descend again.");
        StyleText(subtitle, 22f, FontStyles.Normal, new Color(0.82f, 0.87f, 0.94f, 0.94f), TextAlignmentOptions.Center);

        GameObject relicChamber = CreateUiObject("RelicChamber", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(760f, 430f));
        StyleSurface(relicChamber, new Color(0.09f, 0.11f, 0.16f, 0.88f));
        CreateInsetFrame(relicChamber.transform, "RelicChamberFrame", new Vector2(716f, 386f), new Color(0.96f, 0.85f, 0.58f, 0.10f));
        CreateBackdropBand(relicChamber.transform, "RelicBandUpper", new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(680f, 116f), new Color(0.16f, 0.22f, 0.30f, 0.16f));
        CreateBackdropBand(relicChamber.transform, "RelicBandLower", new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(680f, 132f), new Color(0.13f, 0.10f, 0.08f, 0.18f));

        TMP_Text chamberOverline = CreateLabel("ChamberOverline", relicChamber.transform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(620f, 26f), "Relic Chamber");
        StyleText(chamberOverline, 22f, FontStyles.Bold, new Color(1f, 0.84f, 0.56f, 0.95f), TextAlignmentOptions.Center);
        TMP_Text chamberHint = CreateLabel("ChamberHint", relicChamber.transform, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(620f, 30f), "Wake the relic and begin the climb.");
        StyleText(chamberHint, 20f, FontStyles.Normal, new Color(0.78f, 0.84f, 0.92f, 0.95f), TextAlignmentOptions.Center);

        GameObject altarBase = CreateUiObject("RelicAltarBase", relicChamber.transform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, 64f), new Vector2(360f, 34f));
        StyleSurface(altarBase, new Color(0.49f, 0.32f, 0.19f, 0.95f));
        GameObject altarPlate = CreateUiObject("RelicAltarPlate", relicChamber.transform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0f, 38f), new Vector2(270f, 18f));
        StyleSurface(altarPlate, new Color(0.84f, 0.67f, 0.42f, 0.92f));
        GameObject altarGlow = CreateUiObject("RelicGlow", relicChamber.transform, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), new Vector2(0f, -12f), new Vector2(220f, 220f));
        StyleSurface(altarGlow, new Color(0.30f, 0.74f, 0.88f, 0.16f));
        GameObject relicFrame = CreateUiObject("RelicFrame", relicChamber.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), new Vector2(0f, -12f), new Vector2(138f, 138f));
        StyleSurface(relicFrame, new Color(0.11f, 0.15f, 0.22f, 0.94f));
        CreateInsetFrame(relicFrame.transform, "RelicFrameInset", new Vector2(114f, 114f), new Color(0.97f, 0.86f, 0.60f, 0.12f));
        GameObject relicCore = CreateUiObject("RelicCore", relicFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
        StyleSurface(relicCore, new Color(0.95f, 0.86f, 0.62f, 0.98f));
        GameObject relicInner = CreateUiObject("RelicInner", relicFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
        StyleSurface(relicInner, new Color(0.23f, 0.73f, 0.92f, 0.92f));
        GameObject relicOrbitA = CreateUiObject("RelicOrbitA", relicChamber.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), new Vector2(-104f, -14f), new Vector2(18f, 18f));
        StyleSurface(relicOrbitA, new Color(0.98f, 0.84f, 0.58f, 0.92f));
        GameObject relicOrbitB = CreateUiObject("RelicOrbitB", relicChamber.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), new Vector2(104f, -14f), new Vector2(18f, 18f));
        StyleSurface(relicOrbitB, new Color(0.44f, 0.86f, 0.95f, 0.92f));
        CreateSpriteImage(relicChamber.transform, "TitleWitchSprite", WitchSpritePath, new Vector2(0.5f, 0.44f), new Vector2(0f, -8f), new Vector2(168f, 168f), true);
        CreateSpriteImage(relicChamber.transform, "TitleTreeLeft", TreeSpritePath, new Vector2(0f, 1f), new Vector2(116f, -204f), new Vector2(176f, 176f), true);
        CreateSpriteImage(relicChamber.transform, "TitleTreeRight", TreeSpritePath, new Vector2(1f, 1f), new Vector2(-116f, -204f), new Vector2(176f, 176f), true);

        GameObject loreCard = CreateUiObject("LoreCard", relicChamber.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(690f, 92f));
        StyleSurface(loreCard, new Color(0.10f, 0.13f, 0.18f, 0.94f));
        CreateInsetFrame(loreCard.transform, "LoreCardFrame", new Vector2(650f, 52f), new Color(0.70f, 0.88f, 0.97f, 0.08f));
        TMP_Text loreTitle = CreateLabel("LoreTitle", loreCard.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 28f), "Start -> Home -> Battle");
        StyleText(loreTitle, 22f, FontStyles.Bold, new Color(0.93f, 0.95f, 1f, 0.98f), TextAlignmentOptions.Center);

        GameObject actionCard = CreateUiObject("ActionCard", frame.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 182f), new Vector2(720f, 218f));
        StyleSurface(actionCard, new Color(0.12f, 0.12f, 0.17f, 0.94f));
        CreateInsetFrame(actionCard.transform, "ActionCardFrame", new Vector2(676f, 174f), new Color(0.96f, 0.85f, 0.58f, 0.10f));
        TMP_Text actionTitle = CreateLabel("ActionTitle", actionCard.transform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(620f, 34f), "Choose Your Path");
        StyleText(actionTitle, 26f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);

        GameObject controllerObject = new GameObject("TitleSceneController");
        TitleSceneController controller = controllerObject.AddComponent<TitleSceneController>();

        Button continueButton = CreateNavButton(actionCard.transform, "Continue", new Vector2(0f, -96f), controller, nameof(TitleSceneController.ContinueGame), new Vector2(320f, 58f));
        Button startButton = CreateNavButton(actionCard.transform, "Start New Run", new Vector2(0f, -160f), controller, nameof(TitleSceneController.StartNewGame), new Vector2(380f, 68f));
        StyleButton(continueButton, new Color(0.18f, 0.38f, 0.52f, 1f));
        StyleButton(startButton, new Color(0.74f, 0.42f, 0.16f, 1f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/MCP/Rebuild Minimal Home Scene")]
    public static void RebuildMinimalHomeScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] Home scene rebuild skipped during play mode.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        ClearScene(scene);

        EnsureEventSystem();
        Canvas canvas = CreateCanvas("HomeCanvas");
        CreateBackdrop(canvas.transform, new Color(0.05f, 0.07f, 0.11f, 1f));
        CreateSpriteImage(canvas.transform, "HomeChamberBackground", HomeChamberBackgroundPath, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1170f, 2532f), true);
        GameObject homeBackgroundShade = CreateUiObject("HomeBackgroundShade", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        StyleSurface(homeBackgroundShade, new Color(0.03f, 0.04f, 0.07f, 0.42f));
        CreateGlow(canvas.transform, "HomeGlowTop", new Vector2(0.50f, 0.30f), Vector2.zero, new Vector2(520f, 520f), new Color(0.59f, 0.16f, 0.88f, 0.20f));
        CreateGlow(canvas.transform, "HomeGlowBottom", new Vector2(0.88f, 0.08f), Vector2.zero, new Vector2(520f, 520f), new Color(0.14f, 0.71f, 0.61f, 0.10f));
        GameObject homeTopScrim = CreateUiObject("HomeTopScrim", canvas.transform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 260f));
        StyleSurface(homeTopScrim, new Color(0.03f, 0.04f, 0.08f, 0.72f));
        GameObject homeBottomScrim = CreateUiObject("HomeBottomScrim", canvas.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 360f));
        StyleSurface(homeBottomScrim, new Color(0.03f, 0.04f, 0.07f, 0.76f));

        TMP_Text titleText = CreateLabel("ScreenTitle", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(820f, 56f), "Witch Tower");
        StyleText(titleText, 44f, FontStyles.Bold, new Color(0.94f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        CreateTitleSigil(canvas.transform, "HomeTitleSigil", new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Color(0.96f, 0.84f, 0.58f, 0.18f));
        TMP_Text subtitleText = CreateLabel("ScreenSubtitle", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(860f, 36f), "The chamber between descents.");
        StyleText(subtitleText, 22f, FontStyles.Normal, new Color(0.82f, 0.86f, 0.94f, 0.95f), TextAlignmentOptions.Center);

        GameObject navBar = CreateUiObject("NavBar", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(900f, 64f));
        StyleSurface(navBar, new Color(0.08f, 0.10f, 0.14f, 0.90f));
        CreateInsetFrame(navBar.transform, "NavBarFrame", new Vector2(856f, 36f), new Color(0.60f, 0.89f, 0.92f, 0.08f));
        GameObject navHighlight = CreateUiObject("NavBarHighlight", navBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 22f));
        StyleSurface(navHighlight, new Color(0.96f, 0.84f, 0.56f, 0.05f));
        navHighlight.transform.SetAsFirstSibling();
        GameObject contentRoot = CreateUiObject("ContentRoot", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(940f, 980f));

        GameObject homePanel = CreatePanel("HomePanel", contentRoot.transform);
        GameObject enhancePanel = CreatePanel("EnhancePanel", contentRoot.transform);
        GameObject equipmentPanel = CreatePanel("EquipmentPanel", contentRoot.transform);
        GameObject missionPanel = CreatePanel("MissionPanel", contentRoot.transform);
        TintPanel(homePanel, new Color(0.05f, 0.06f, 0.10f, 0.76f));
        TintPanel(enhancePanel, new Color(0.05f, 0.06f, 0.10f, 0.76f));
        TintPanel(equipmentPanel, new Color(0.05f, 0.06f, 0.10f, 0.76f));
        TintPanel(missionPanel, new Color(0.05f, 0.06f, 0.10f, 0.76f));

        CreateSectionHeader(homePanel.transform, "Run Overview", "Your current climb, gold, and passive rewards.");
        CreateSectionHeader(enhancePanel.transform, "Enhancements", "Spend gold on permanent combat growth.");
        CreateSectionHeader(equipmentPanel.transform, "Loadout", "Swap unlocked gear to shift your battle profile.");
        CreateSectionHeader(missionPanel.transform, "Missions", "Claim daily income and floor progress rewards.");
        CreatePanelAccent(homePanel.transform, "HomePanelAccent", "RUN DECK", "Stage the next tower descent from here.", new Color(0.24f, 0.63f, 0.83f, 0.16f), new Color(0.98f, 0.83f, 0.54f, 0.95f));
        CreatePanelAccent(enhancePanel.transform, "EnhancePanelAccent", "FORGE", "Spend gold to harden the next climb.", new Color(0.83f, 0.34f, 0.28f, 0.16f), new Color(1f, 0.86f, 0.60f, 0.95f));
        CreatePanelAccent(equipmentPanel.transform, "EquipmentPanelAccent", "ARMORY", "Swap relics and tune your loadout silhouette.", new Color(0.34f, 0.74f, 0.71f, 0.14f), new Color(0.86f, 0.97f, 0.94f, 0.95f));
        CreatePanelAccent(missionPanel.transform, "MissionPanelAccent", "BULLETIN", "Cash out tower tasks and daily contracts.", new Color(0.62f, 0.42f, 0.81f, 0.16f), new Color(0.95f, 0.89f, 1f, 0.95f));

        GameObject launchCard = CreateUiObject("BattleLaunchCard", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -454f), new Vector2(780f, 228f));
        StyleSurface(launchCard, new Color(0.16f, 0.11f, 0.08f, 0.96f));
        GameObject launchBackPlate = CreateUiObject("LaunchBackPlate", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -448f), new Vector2(820f, 250f));
        StyleSurface(launchBackPlate, new Color(0.08f, 0.10f, 0.15f, 0.55f));
        launchBackPlate.transform.SetAsFirstSibling();
        GameObject launchShadowBand = CreateUiObject("LaunchShadowBand", launchCard.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(700f, 24f));
        StyleSurface(launchShadowBand, new Color(0.06f, 0.08f, 0.12f, 0.34f));
        CreateInsetFrame(launchCard.transform, "LaunchFrame", new Vector2(720f, 174f), new Color(1f, 0.82f, 0.48f, 0.12f));
        GameObject launchTowerGlow = CreateUiObject("LaunchTowerGlow", launchCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(128f, -4f), new Vector2(150f, 150f));
        StyleSurface(launchTowerGlow, new Color(0.29f, 0.71f, 0.88f, 0.16f));
        GameObject towerPreview = CreateUiObject("TowerPreview", launchCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(128f, -4f), new Vector2(112f, 152f));
        StyleSurface(towerPreview, new Color(0.11f, 0.15f, 0.22f, 0.95f));
        GameObject towerCore = CreateUiObject("TowerPreviewCore", towerPreview.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(40f, 122f));
        StyleSurface(towerCore, new Color(0.93f, 0.82f, 0.55f, 0.95f));
        GameObject towerCap = CreateUiObject("TowerPreviewCap", towerPreview.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(78f, 20f));
        StyleSurface(towerCap, new Color(0.96f, 0.90f, 0.70f, 0.95f));
        GameObject towerStepTop = CreateUiObject("TowerStepTop", towerPreview.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(82f, 12f));
        StyleSurface(towerStepTop, new Color(0.66f, 0.45f, 0.27f, 0.88f));
        GameObject towerStepBottom = CreateUiObject("TowerStepBottom", towerPreview.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(108f, 14f));
        StyleSurface(towerStepBottom, new Color(0.42f, 0.24f, 0.14f, 0.92f));
        GameObject towerOrbit = CreateUiObject("TowerPreviewOrbit", launchCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(128f, -4f), new Vector2(168f, 168f));
        StyleSurface(towerOrbit, new Color(0.96f, 0.84f, 0.56f, 0.07f));
        towerOrbit.transform.SetAsFirstSibling();
        CreateSpriteTileStrip(launchCard.transform, "LaunchGround", GrassTilePath, new Vector2(0.5f, 0f), new Vector2(-12f, 28f), 8, 1, 30f);
        CreateSpriteImage(launchCard.transform, "LaunchWitchSprite", WitchCastSpritePath, new Vector2(0f, 0.5f), new Vector2(132f, -4f), new Vector2(124f, 124f), true);
        CreateSpriteImage(launchCard.transform, "LaunchTreeSprite", TreeSpritePath, new Vector2(0f, 0.5f), new Vector2(222f, 4f), new Vector2(102f, 102f), true);
        TMP_Text towerMarker = CreateLabel("TowerPreviewMarker", towerPreview.transform, new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(90f, 28f), "NEXT");
        StyleText(towerMarker, 16f, FontStyles.Bold, new Color(0.07f, 0.09f, 0.12f, 0.95f), TextAlignmentOptions.Center);
        TMP_Text towerFloorBadge = CreateLabel("TowerFloorBadge", launchCard.transform, new Vector2(0f, 1f), new Vector2(128f, -168f), new Vector2(130f, 30f), "TARGET FLOOR");
        StyleText(towerFloorBadge, 15f, FontStyles.Bold, new Color(1f, 0.84f, 0.58f, 0.92f), TextAlignmentOptions.Center);
        TMP_Text launchOverline = CreateLabel("LaunchOverline", launchCard.transform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(620f, 28f), "Next Descent");
        StyleText(launchOverline, 18f, FontStyles.Bold, new Color(1f, 0.83f, 0.54f, 1f), TextAlignmentOptions.Center);
        TMP_Text launchTitle = CreateLabel("LaunchTitle", launchCard.transform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(620f, 42f), "Challenge The Next Floor");
        StyleText(launchTitle, 30f, FontStyles.Bold, new Color(0.97f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text launchBody = CreateLabel("LaunchBody", launchCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(620f, 52f), "Lock your gear, step through the gate, and dive back into the tower.");
        StyleText(launchBody, 18f, FontStyles.Normal, new Color(0.78f, 0.84f, 0.92f, 0.95f), TextAlignmentOptions.Center);
        TMP_Text launchFlavor = CreateLabel("LaunchFlavor", launchCard.transform, new Vector2(0.66f, 0f), new Vector2(0f, 24f), new Vector2(420f, 28f), "The tower hums louder the deeper you press.");
        StyleText(launchFlavor, 16f, FontStyles.Italic, new Color(0.95f, 0.88f, 0.76f, 0.92f), TextAlignmentOptions.Center);
        CreateLaunchChevron(launchCard.transform, "LaunchChevronA", new Vector2(0.82f, 0.50f), new Vector2(0f, 18f), new Color(0.98f, 0.84f, 0.56f, 0.28f));
        CreateLaunchChevron(launchCard.transform, "LaunchChevronB", new Vector2(0.86f, 0.50f), new Vector2(0f, 18f), new Color(0.98f, 0.84f, 0.56f, 0.18f));
        CreateLaunchChevron(launchCard.transform, "LaunchChevronC", new Vector2(0.90f, 0.50f), new Vector2(0f, 18f), new Color(0.98f, 0.84f, 0.56f, 0.10f));

        GameObject floorCard = CreateUiObject("FloorPreviewCard", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -704f), new Vector2(760f, 124f));
        StyleSurface(floorCard, new Color(0.09f, 0.13f, 0.18f, 0.94f));
        CreateInsetFrame(floorCard.transform, "FloorPreviewFrame", new Vector2(724f, 94f), new Color(0.36f, 0.77f, 0.83f, 0.11f));
        TMP_Text floorCardLabel = CreateLabel("FloorPreviewLabel", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(108f, 0f), new Vector2(160f, 34f), "Tower Route");
        StyleText(floorCardLabel, 24f, FontStyles.Bold, new Color(0.93f, 0.96f, 1f, 0.98f), TextAlignmentOptions.Center);
        TMP_Text floorCardBody = CreateLabel("FloorPreviewBody", floorCard.transform, new Vector2(0.55f, 0.5f), new Vector2(32f, 0f), new Vector2(500f, 60f), "Home is the staging deck. Tune gear here, then dive from the orange launch gate above.");
        StyleText(floorCardBody, 17f, FontStyles.Normal, new Color(0.77f, 0.84f, 0.92f, 0.94f), TextAlignmentOptions.MidlineLeft);
        GameObject routeNodeLeft = CreateUiObject("RouteNodeLeft", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(272f, 0f), new Vector2(18f, 18f));
        StyleSurface(routeNodeLeft, new Color(0.32f, 0.79f, 0.86f, 0.95f));
        GameObject routeLine = CreateUiObject("RouteLine", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(400f, 0f), new Vector2(220f, 4f));
        StyleSurface(routeLine, new Color(0.94f, 0.83f, 0.54f, 0.75f));
        GameObject routeNodeRight = CreateUiObject("RouteNodeRight", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(528f, 0f), new Vector2(22f, 22f));
        StyleSurface(routeNodeRight, new Color(0.86f, 0.39f, 0.22f, 0.95f));
        GameObject routeNodeLeftCore = CreateUiObject("RouteNodeLeftCore", routeNodeLeft.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
        StyleSurface(routeNodeLeftCore, new Color(0.07f, 0.10f, 0.14f, 0.72f));
        GameObject routeNodeRightCore = CreateUiObject("RouteNodeRightCore", routeNodeRight.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
        StyleSurface(routeNodeRightCore, new Color(0.14f, 0.08f, 0.06f, 0.72f));
        GameObject routeTickA = CreateUiObject("RouteTickA", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(344f, 0f), new Vector2(10f, 10f));
        StyleSurface(routeTickA, new Color(0.96f, 0.84f, 0.56f, 0.34f));
        GameObject routeTickB = CreateUiObject("RouteTickB", floorCard.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(456f, 0f), new Vector2(10f, 10f));
        StyleSurface(routeTickB, new Color(0.96f, 0.84f, 0.56f, 0.24f));

        PlayerStatusView homePlayerStatus = CreatePlayerStatusView(homePanel.transform, "HomePlayerStatus", new Vector2(0.5f, 1f), new Vector2(0f, -146f));
        ResourceView homeResource = CreateResourceView(homePanel.transform, "HomeResource", new Vector2(0.5f, 1f), new Vector2(0f, -254f));
        IdleRewardView idleReward = CreateIdleRewardView(homePanel.transform, "IdleReward", new Vector2(0.5f, 1f), new Vector2(0f, -318f));

        ResourceView enhanceResource = CreateResourceView(enhancePanel.transform, "EnhanceResource", new Vector2(0.5f, 1f), new Vector2(0f, -170f));
        UpgradeStatusView attackUpgrade = CreateUpgradeStatusView(enhancePanel.transform, "AttackUpgrade", new Vector2(0.5f, 1f), new Vector2(0f, -300f));
        UpgradeStatusView defenseUpgrade = CreateUpgradeStatusView(enhancePanel.transform, "DefenseUpgrade", new Vector2(0.5f, 1f), new Vector2(0f, -430f));
        UpgradeStatusView hpUpgrade = CreateUpgradeStatusView(enhancePanel.transform, "HpUpgrade", new Vector2(0.5f, 1f), new Vector2(0f, -560f));

        PlayerStatusView equipmentPlayerStatus = CreatePlayerStatusView(equipmentPanel.transform, "EquipmentPlayerStatus", new Vector2(0.5f, 1f), new Vector2(0f, -170f));
        ResourceView equipmentResource = CreateResourceView(equipmentPanel.transform, "EquipmentResource", new Vector2(0.5f, 1f), new Vector2(0f, -290f));
        EquipmentStatusView equipmentStatus = CreateEquipmentStatusView(equipmentPanel.transform, "EquipmentStatus", new Vector2(0.5f, 1f), new Vector2(0f, -430f));

        ResourceView missionResource = CreateResourceView(missionPanel.transform, "MissionResource", new Vector2(0.5f, 1f), new Vector2(0f, -170f));
        DailyRewardView dailyReward = CreateDailyRewardView(missionPanel.transform, "DailyReward", new Vector2(0.5f, 1f), new Vector2(0f, -270f));
        MissionItemView missionItem1 = CreateMissionItemView(missionPanel.transform, "MissionItem1", new Vector2(0.5f, 1f), new Vector2(0f, -400f));
        MissionItemView missionItem2 = CreateMissionItemView(missionPanel.transform, "MissionItem2", new Vector2(0.5f, 1f), new Vector2(0f, -530f));

        StyleCard(homePlayerStatus.gameObject, new Color(0.12f, 0.18f, 0.25f, 0.92f));
        StyleCard(homeResource.gameObject, new Color(0.18f, 0.14f, 0.09f, 0.94f));
        StyleCard(idleReward.gameObject, new Color(0.10f, 0.17f, 0.18f, 0.94f));
        StyleCard(enhanceResource.gameObject, new Color(0.18f, 0.14f, 0.09f, 0.94f));
        StyleCard(attackUpgrade.gameObject, new Color(0.18f, 0.10f, 0.14f, 0.94f));
        StyleCard(defenseUpgrade.gameObject, new Color(0.12f, 0.16f, 0.21f, 0.94f));
        StyleCard(hpUpgrade.gameObject, new Color(0.11f, 0.18f, 0.15f, 0.94f));
        StyleCard(equipmentPlayerStatus.gameObject, new Color(0.12f, 0.18f, 0.25f, 0.92f));
        StyleCard(equipmentResource.gameObject, new Color(0.18f, 0.14f, 0.09f, 0.94f));
        StyleCard(equipmentStatus.gameObject, new Color(0.12f, 0.15f, 0.20f, 0.94f));
        StyleCard(missionResource.gameObject, new Color(0.18f, 0.14f, 0.09f, 0.94f));
        StyleCard(dailyReward.gameObject, new Color(0.16f, 0.14f, 0.22f, 0.94f));
        StyleCard(missionItem1.gameObject, new Color(0.12f, 0.17f, 0.22f, 0.94f));
        StyleCard(missionItem2.gameObject, new Color(0.12f, 0.17f, 0.22f, 0.94f));
        CreateInsetFrame(attackUpgrade.transform, "AttackUpgradeFrame", new Vector2(654f, 90f), new Color(1f, 0.62f, 0.62f, 0.10f));
        CreateInsetFrame(defenseUpgrade.transform, "DefenseUpgradeFrame", new Vector2(654f, 90f), new Color(0.62f, 0.84f, 1f, 0.10f));
        CreateInsetFrame(hpUpgrade.transform, "HpUpgradeFrame", new Vector2(654f, 90f), new Color(0.62f, 0.96f, 0.78f, 0.10f));
        CreateInsetFrame(equipmentStatus.transform, "EquipmentStatusFrame", new Vector2(654f, 244f), new Color(0.72f, 0.95f, 0.88f, 0.10f));
        CreateInsetFrame(missionItem1.transform, "MissionItem1Frame", new Vector2(654f, 56f), new Color(0.86f, 0.88f, 1f, 0.10f));
        CreateInsetFrame(missionItem2.transform, "MissionItem2Frame", new Vector2(654f, 56f), new Color(0.86f, 0.88f, 1f, 0.10f));

        GameObject controllerRoot = new GameObject("HomeSceneRoot");
        PanelSwitcher panelSwitcher = controllerRoot.AddComponent<PanelSwitcher>();
        HomeSceneController homeSceneController = controllerRoot.AddComponent<HomeSceneController>();
        HomePanelController homePanelController = controllerRoot.AddComponent<HomePanelController>();
        EnhancePanelController enhancePanelController = controllerRoot.AddComponent<EnhancePanelController>();
        EquipmentPanelController equipmentPanelController = controllerRoot.AddComponent<EquipmentPanelController>();
        MissionPanelController missionPanelController = controllerRoot.AddComponent<MissionPanelController>();
        Button homeNavButton = CreateNavButton(navBar.transform, "Home", new Vector2(-270f, 0f), panelSwitcher, nameof(PanelSwitcher.ShowHome), new Vector2(168f, 48f));
        Button enhanceNavButton = CreateNavButton(navBar.transform, "Enhance", new Vector2(-90f, 0f), panelSwitcher, nameof(PanelSwitcher.ShowEnhance), new Vector2(168f, 48f));
        Button equipmentNavButton = CreateNavButton(navBar.transform, "Equipment", new Vector2(90f, 0f), panelSwitcher, nameof(PanelSwitcher.ShowEquipment), new Vector2(168f, 48f));
        Button missionNavButton = CreateNavButton(navBar.transform, "Mission", new Vector2(270f, 0f), panelSwitcher, nameof(PanelSwitcher.ShowMission), new Vector2(168f, 48f));
        TMP_Text homeNavBadgeText = CreateNavBadge(homeNavButton.transform, "HomeNavBadge", "1");
        TMP_Text enhanceNavBadgeText = CreateNavBadge(enhanceNavButton.transform, "EnhanceNavBadge", "2");
        TMP_Text equipmentNavBadgeText = CreateNavBadge(equipmentNavButton.transform, "EquipmentNavBadge", "1");
        TMP_Text missionNavBadgeText = CreateNavBadge(missionNavButton.transform, "MissionNavBadge", "3");

        SetObjectField(panelSwitcher, "homePanel", homePanel);
        SetObjectField(panelSwitcher, "enhancePanel", enhancePanel);
        SetObjectField(panelSwitcher, "equipmentPanel", equipmentPanel);
        SetObjectField(panelSwitcher, "missionPanel", missionPanel);
        SetObjectField(panelSwitcher, "homePanelController", homePanelController);
        SetObjectField(panelSwitcher, "enhancePanelController", enhancePanelController);
        SetObjectField(panelSwitcher, "equipmentPanelController", equipmentPanelController);
        SetObjectField(panelSwitcher, "missionPanelController", missionPanelController);
        SetObjectField(panelSwitcher, "homeNavButton", homeNavButton);
        SetObjectField(panelSwitcher, "enhanceNavButton", enhanceNavButton);
        SetObjectField(panelSwitcher, "equipmentNavButton", equipmentNavButton);
        SetObjectField(panelSwitcher, "missionNavButton", missionNavButton);
        SetObjectField(panelSwitcher, "homeNavBadgeText", homeNavBadgeText);
        SetObjectField(panelSwitcher, "enhanceNavBadgeText", enhanceNavBadgeText);
        SetObjectField(panelSwitcher, "equipmentNavBadgeText", equipmentNavBadgeText);
        SetObjectField(panelSwitcher, "missionNavBadgeText", missionNavBadgeText);

        SetObjectField(homeSceneController, "panelSwitcher", panelSwitcher);
        SetObjectField(homeSceneController, "homePanelController", homePanelController);
        SetObjectField(homeSceneController, "enhancePanelController", enhancePanelController);
        SetObjectField(homeSceneController, "equipmentPanelController", equipmentPanelController);
        SetObjectField(homeSceneController, "missionPanelController", missionPanelController);

        SetObjectField(homePanelController, "playerStatusView", homePlayerStatus);
        SetObjectField(homePanelController, "resourceView", homeResource);
        SetObjectField(homePanelController, "idleRewardView", idleReward);
        SetObjectField(homePanelController, "ctaText", CreateCtaLabel("HomeCtaText", homePanel.transform, "Next Step: enter Battle and challenge the next floor."));
        TMP_Text homeRewardSummaryText = CreateLabel("HomeRewardSummaryText", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -432f), new Vector2(660f, 34f), "Ready Gold: nothing to claim right now.");
        StyleText(homeRewardSummaryText, 15f, FontStyles.Normal, new Color(0.84f, 0.89f, 0.96f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(homePanelController, "rewardSummaryText", homeRewardSummaryText);
        TMP_Text prepAdviceText = CreateLabel("PrepAdviceText", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -466f), new Vector2(670f, 34f), "Prep Advice: current build is favorable; push when ready.");
        StyleText(prepAdviceText, 15f, FontStyles.Normal, new Color(0.92f, 0.84f, 0.58f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(homePanelController, "prepAdviceText", prepAdviceText);
        TMP_Text battlePlanText = CreateLabel("BattlePlanText", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -500f), new Vector2(670f, 34f), "Battle Plan: build is ready, challenge floor 2 now.");
        StyleText(battlePlanText, 15f, FontStyles.Normal, new Color(0.75f, 0.90f, 0.88f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(homePanelController, "battlePlanText", battlePlanText);
        GameObject homeActionRail = CreateUiObject("HomeActionRail", homePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -556f), new Vector2(704f, 72f));
        StyleSurface(homeActionRail, new Color(0.09f, 0.12f, 0.18f, 0.90f));
        CreateInsetFrame(homeActionRail.transform, "HomeActionRailFrame", new Vector2(664f, 34f), new Color(0.61f, 0.90f, 0.92f, 0.10f));
        TMP_Text homeActionRailText = CreateLabel("HomeActionRailText", homeActionRail.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 34f), "Collect rewards on the rail, then launch from the tower gate.");
        StyleText(homeActionRailText, 16f, FontStyles.Bold, new Color(0.93f, 0.96f, 1f, 0.94f), TextAlignmentOptions.Center);

        SetObjectField(enhancePanelController, "resourceView", enhanceResource);
        SetObjectField(enhancePanelController, "attackUpgradeView", attackUpgrade);
        SetObjectField(enhancePanelController, "defenseUpgradeView", defenseUpgrade);
        SetObjectField(enhancePanelController, "hpUpgradeView", hpUpgrade);
        SetObjectField(enhancePanelController, "ctaText", CreateCtaLabel("EnhanceCtaText", enhancePanel.transform, "Upgrade Priority: save Gold for your next boost."));

        SetObjectField(missionPanelController, "resourceView", missionResource);
        SetObjectField(missionPanelController, "dailyRewardView", dailyReward);
        SetObjectField(missionPanelController, "missionItemView1", missionItem1);
        SetObjectField(missionPanelController, "missionItemView2", missionItem2);
        SetObjectField(missionPanelController, "ctaText", CreateCtaLabel("MissionCtaText", missionPanel.transform, "Mission Focus: keep climbing to open more rewards."));
        TMP_Text missionRewardSummaryText = CreateLabel("MissionRewardSummaryText", missionPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(660f, 34f), "Claimable Rewards: no reward claims ready.");
        StyleText(missionRewardSummaryText, 15f, FontStyles.Normal, new Color(0.84f, 0.89f, 0.96f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(missionPanelController, "rewardSummaryText", missionRewardSummaryText);
        Button battleNavButton = CreateNavButton(navBar.transform, "Battle", new Vector2(0f, -70f), homeSceneController, nameof(HomeSceneController.StartBattle), new Vector2(240f, 54f));
        StyleButton(battleNavButton, new Color(0.75f, 0.39f, 0.16f, 1f));
        CreatePrimaryButtonAura(battleNavButton.transform, "BattleNavAura", new Vector2(268f, 72f), new Color(0.97f, 0.84f, 0.56f, 0.10f), "DEPLOY");
        Button challengeFloorButton = CreateNavButton(launchCard.transform, "Challenge Floor", new Vector2(0f, 62f), homeSceneController, nameof(HomeSceneController.StartBattle), new Vector2(320f, 58f));
        StyleButton(challengeFloorButton, new Color(0.77f, 0.39f, 0.16f, 1f));
        CreatePrimaryButtonAura(challengeFloorButton.transform, "ChallengeFloorAura", new Vector2(352f, 78f), new Color(0.98f, 0.84f, 0.56f, 0.12f), "DESCEND");

        Button claimIdleRewardButton = CreateNavButton(homePanel.transform, "ClaimIdleReward", new Vector2(0f, -610f), homePanelController, nameof(HomePanelController.ClaimIdleReward), new Vector2(280f, 52f));
        StyleButton(claimIdleRewardButton, new Color(0.18f, 0.49f, 0.44f, 1f));
        CreateNavButton(enhancePanel.transform, "UpgradeAttack", new Vector2(0f, -350f), enhancePanelController, nameof(EnhancePanelController.UpgradeAttack), new Vector2(220f, 40f));
        CreateNavButton(enhancePanel.transform, "UpgradeDefense", new Vector2(0f, -480f), enhancePanelController, nameof(EnhancePanelController.UpgradeDefense), new Vector2(220f, 40f));
        CreateNavButton(enhancePanel.transform, "UpgradeHp", new Vector2(0f, -610f), enhancePanelController, nameof(EnhancePanelController.UpgradeHp), new Vector2(220f, 40f));
        Button bronzeBladeButton = CreateNavButton(equipmentPanel.transform, "BronzeBlade", new Vector2(-170f, -630f), equipmentPanelController, nameof(EquipmentPanelController.EquipBronzeBlade), new Vector2(220f, 40f));
        Button ironSwordButton = CreateNavButton(equipmentPanel.transform, "IronSword", new Vector2(170f, -630f), equipmentPanelController, nameof(EquipmentPanelController.EquipIronSword), new Vector2(220f, 40f));
        Button guardClothButton = CreateNavButton(equipmentPanel.transform, "GuardCloth", new Vector2(-170f, -720f), equipmentPanelController, nameof(EquipmentPanelController.EquipGuardCloth), new Vector2(220f, 40f));
        Button boneMailButton = CreateNavButton(equipmentPanel.transform, "BoneMail", new Vector2(170f, -720f), equipmentPanelController, nameof(EquipmentPanelController.EquipBoneMail), new Vector2(220f, 40f));
        Button ashenRingButton = CreateNavButton(equipmentPanel.transform, "AshenRing", new Vector2(-170f, -810f), equipmentPanelController, nameof(EquipmentPanelController.EquipAshenRing), new Vector2(220f, 40f));
        Button quickCharmButton = CreateNavButton(equipmentPanel.transform, "QuickCharm", new Vector2(170f, -810f), equipmentPanelController, nameof(EquipmentPanelController.EquipQuickCharm), new Vector2(220f, 40f));
        CreateGearRowLabel(equipmentPanel.transform, "WeaponRowLabel", new Vector2(0f, -586f), "WEAPON LINE");
        CreateGearRowLabel(equipmentPanel.transform, "ArmorRowLabel", new Vector2(0f, -676f), "ARMOR LINE");
        CreateGearRowLabel(equipmentPanel.transform, "AccessoryRowLabel", new Vector2(0f, -766f), "ACCESSORY LINE");
        TMP_Text bronzeBladeStatusText = CreateLabel("BronzeBladeStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-170f, -665f), new Vector2(220f, 24f), "Starter");
        TMP_Text ironSwordStatusText = CreateLabel("IronSwordStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(170f, -665f), new Vector2(220f, 24f), "Unlock at Floor 2");
        TMP_Text guardClothStatusText = CreateLabel("GuardClothStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-170f, -755f), new Vector2(220f, 24f), "Starter");
        TMP_Text boneMailStatusText = CreateLabel("BoneMailStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(170f, -755f), new Vector2(220f, 24f), "Unlock at Floor 4");
        TMP_Text ashenRingStatusText = CreateLabel("AshenRingStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-170f, -845f), new Vector2(220f, 24f), "Starter");
        TMP_Text quickCharmStatusText = CreateLabel("QuickCharmStatusText", equipmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(170f, -845f), new Vector2(220f, 24f), "Unlock at Floor 6");
        CreateNavButton(missionPanel.transform, "ClaimDaily", new Vector2(0f, -320f), missionPanelController, nameof(MissionPanelController.ClaimDailyReward), new Vector2(220f, 40f));
        CreateNavButton(missionPanel.transform, "ClaimMission1", new Vector2(0f, -460f), missionPanelController, nameof(MissionPanelController.ClaimMissionClear1), new Vector2(220f, 40f));
        CreateNavButton(missionPanel.transform, "ClaimMission2", new Vector2(0f, -590f), missionPanelController, nameof(MissionPanelController.ClaimMissionReachFloor3), new Vector2(220f, 40f));

        SetObjectField(equipmentPanelController, "playerStatusView", equipmentPlayerStatus);
        SetObjectField(equipmentPanelController, "resourceView", equipmentResource);
        SetObjectField(equipmentPanelController, "equipmentStatusView", equipmentStatus);
        SetObjectField(equipmentPanelController, "bronzeBladeButton", bronzeBladeButton);
        SetObjectField(equipmentPanelController, "ironSwordButton", ironSwordButton);
        SetObjectField(equipmentPanelController, "guardClothButton", guardClothButton);
        SetObjectField(equipmentPanelController, "boneMailButton", boneMailButton);
        SetObjectField(equipmentPanelController, "ashenRingButton", ashenRingButton);
        SetObjectField(equipmentPanelController, "quickCharmButton", quickCharmButton);
        SetObjectField(equipmentPanelController, "bronzeBladeStatusText", bronzeBladeStatusText);
        SetObjectField(equipmentPanelController, "ironSwordStatusText", ironSwordStatusText);
        SetObjectField(equipmentPanelController, "guardClothStatusText", guardClothStatusText);
        SetObjectField(equipmentPanelController, "boneMailStatusText", boneMailStatusText);
        SetObjectField(equipmentPanelController, "ashenRingStatusText", ashenRingStatusText);
        SetObjectField(equipmentPanelController, "quickCharmStatusText", quickCharmStatusText);
        SetObjectField(equipmentPanelController, "ctaText", CreateCtaLabel("EquipmentCtaText", equipmentPanel.transform, "Loadout Focus: clear deeper floors to unlock more gear."));

        panelSwitcher.ShowHome();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/MCP/Rebuild Fusion Scene")]
    public static void RebuildFusionScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] Fusion scene rebuild skipped during play mode.");
            return;
        }

        Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FusionScenePath) != null
            ? EditorSceneManager.OpenScene(FusionScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ClearScene(scene);

        EnsureEventSystem();
        Canvas canvas = CreateCanvas("FusionCanvas");

        GameObject panelObject = new GameObject("FusionScenePanel", typeof(RectTransform), typeof(Image), typeof(MonsterFusionPanelController));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.001f);

        MonsterFusionPanelController panel = panelObject.GetComponent<MonsterFusionPanelController>();
        GameObject controllerRoot = new GameObject("FusionSceneRoot");
        controllerRoot.AddComponent<FusionSceneController>();
        panel.Show(null);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, FusionScenePath);
        EnsureSceneInBuildSettings(FusionScenePath);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/MCP/Rebuild Minimal Battle Scene")]
    public static void RebuildMinimalBattleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UnityMcpSceneBuilder] Battle scene rebuild skipped during play mode.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        ClearScene(scene);

        EnsureEventSystem();
        Canvas canvas = CreateCanvas("BattleCanvas");
        CreateBackdrop(canvas.transform, new Color(0.04f, 0.05f, 0.08f, 1f));
        CreateGlow(canvas.transform, "BattleGlowLeft", new Vector2(0.18f, 0.72f), Vector2.zero, new Vector2(380f, 380f), new Color(0.20f, 0.72f, 0.95f, 0.14f));
        CreateGlow(canvas.transform, "BattleGlowRight", new Vector2(0.82f, 0.33f), Vector2.zero, new Vector2(420f, 420f), new Color(0.86f, 0.28f, 0.32f, 0.12f));
        CreateBackdropBand(canvas.transform, "BattleBandTop", new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1080f, 280f), new Color(0.12f, 0.16f, 0.24f, 0.16f));
        CreateBackdropBand(canvas.transform, "BattleBandBottom", new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1080f, 260f), new Color(0.11f, 0.10f, 0.15f, 0.18f));
        CreateSpriteTileStrip(canvas.transform, "BattleGround", DirtTilePath, new Vector2(0.5f, 0f), new Vector2(0f, 112f), 20, 2, 48f);
        CreateSpriteImage(canvas.transform, "BattleTreeLeft", TreeSpritePath, new Vector2(0f, 0.5f), new Vector2(138f, 138f), new Vector2(172f, 172f), true);
        CreateSpriteImage(canvas.transform, "BattleTreeRight", TreeSpritePath, new Vector2(1f, 0.5f), new Vector2(-138f, 138f), new Vector2(172f, 172f), true);
        CreateTowerTotem(canvas.transform, "BattleTotemLeft", new Vector2(0f, 0.54f), new Vector2(74f, -24f), new Color(0.25f, 0.68f, 0.88f, 0.12f), true);
        CreateTowerTotem(canvas.transform, "BattleTotemRight", new Vector2(1f, 0.54f), new Vector2(-74f, -24f), new Color(0.88f, 0.32f, 0.34f, 0.12f), false);
        CreateSceneRibbon(canvas.transform, "BattleTopRibbon", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(980f, 26f), "TOWER ENCOUNTER // DUEL LANE", new Color(0.10f, 0.13f, 0.19f, 0.94f), new Color(0.97f, 0.85f, 0.58f, 0.96f));
        CreateSceneRibbon(canvas.transform, "BattleBottomRibbon", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(980f, 26f), "COOLDOWNS · PAYOUT · NEXT FLOOR DECISION", new Color(0.10f, 0.13f, 0.19f, 0.94f), new Color(0.72f, 0.86f, 0.96f, 0.92f));
        GameObject arenaLane = CreateUiObject("ArenaLane", canvas.transform, new Vector2(0.5f, 0.53f), new Vector2(0.5f, 0.53f), Vector2.zero, new Vector2(920f, 360f));
        StyleSurface(arenaLane, new Color(0.09f, 0.11f, 0.16f, 0.72f));
        CreateInsetFrame(arenaLane.transform, "ArenaLaneFrame", new Vector2(876f, 320f), new Color(1f, 0.86f, 0.58f, 0.10f));
        GameObject arenaStripe = CreateUiObject("ArenaStripe", arenaLane.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 6f));
        StyleSurface(arenaStripe, new Color(0.96f, 0.83f, 0.50f, 0.65f));
        GameObject arenaPulse = CreateUiObject("ArenaPulse", arenaLane.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
        StyleSurface(arenaPulse, new Color(0.97f, 0.86f, 0.58f, 0.08f));
        CreateLaneMarker(arenaLane.transform, "LaneMarkerLeft", new Vector2(0.18f, 0.5f), new Color(0.48f, 0.86f, 0.98f, 0.20f));
        CreateLaneMarker(arenaLane.transform, "LaneMarkerCenter", new Vector2(0.5f, 0.5f), new Color(0.98f, 0.86f, 0.58f, 0.18f));
        CreateLaneMarker(arenaLane.transform, "LaneMarkerRight", new Vector2(0.82f, 0.5f), new Color(0.98f, 0.48f, 0.48f, 0.20f));
        GameObject floorBadge = CreateUiObject("BattleFloorBadge", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(220f, 52f));
        StyleSurface(floorBadge, new Color(0.12f, 0.15f, 0.22f, 0.96f));
        CreateInsetFrame(floorBadge.transform, "BattleFloorBadgeFrame", new Vector2(196f, 30f), new Color(0.94f, 0.83f, 0.54f, 0.16f));
        GameObject floorBadgeGemLeft = CreateUiObject("BattleFloorBadgeGemLeft", floorBadge.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(16f, 16f));
        StyleSurface(floorBadgeGemLeft, new Color(0.97f, 0.84f, 0.56f, 0.26f));
        GameObject floorBadgeGemRight = CreateUiObject("BattleFloorBadgeGemRight", floorBadge.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(16f, 16f));
        StyleSurface(floorBadgeGemRight, new Color(0.97f, 0.84f, 0.56f, 0.26f));
        GameObject floorBadgeGemCoreLeft = CreateUiObject("BattleFloorBadgeGemCoreLeft", floorBadgeGemLeft.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        StyleSurface(floorBadgeGemCoreLeft, new Color(0.08f, 0.10f, 0.14f, 0.64f));
        GameObject floorBadgeGemCoreRight = CreateUiObject("BattleFloorBadgeGemCoreRight", floorBadgeGemRight.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        StyleSurface(floorBadgeGemCoreRight, new Color(0.08f, 0.10f, 0.14f, 0.64f));
        TMP_Text battleTitle = CreateLabel("BattleScreenTitle", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(760f, 52f), "Tower Encounter");
        StyleText(battleTitle, 40f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
        CreateTitleSigil(canvas.transform, "BattleTitleSigil", new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Color(0.96f, 0.84f, 0.58f, 0.18f));
        TMP_Text battleSubtitle = CreateLabel("BattleScreenSubtitle", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(820f, 34f), "Read the floor, time your skills, and finish the duel.");
        StyleText(battleSubtitle, 20f, FontStyles.Normal, new Color(0.73f, 0.80f, 0.90f, 0.95f), TextAlignmentOptions.Center);
        TMP_Text battleThreatText = CreateLabel("BattleThreatText", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(620f, 30f), "Threat: even fight");
        StyleText(battleThreatText, 20f, FontStyles.Bold, new Color(0.97f, 0.82f, 0.55f, 0.98f), TextAlignmentOptions.Center);
        TMP_Text battleEncounterText = CreateLabel("BattleEncounterText", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(980f, 28f), "Encounter: floor 1 enemy data preview.");
        StyleText(battleEncounterText, 15f, FontStyles.Normal, new Color(0.77f, 0.84f, 0.92f, 0.94f), TextAlignmentOptions.Center);

        GameObject root = new GameObject("BattleSceneRoot");
        BattleSceneController battleSceneController = root.AddComponent<BattleSceneController>();
        BattleStateMachine stateMachine = root.AddComponent<BattleStateMachine>();
        BattleSimulator simulator = root.AddComponent<BattleSimulator>();

        GameObject hudRoot = CreateUiObject("HudRoot", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        BattleHudController hudController = hudRoot.AddComponent<BattleHudController>();

        GameObject playerHalo = CreateUiObject("PlayerHalo", hudRoot.transform, new Vector2(0.26f, 0.52f), new Vector2(0.26f, 0.52f), Vector2.zero, new Vector2(370f, 370f));
        StyleSurface(playerHalo, new Color(0.18f, 0.58f, 0.76f, 0.10f));
        GameObject enemyHalo = CreateUiObject("EnemyHalo", hudRoot.transform, new Vector2(0.74f, 0.52f), new Vector2(0.74f, 0.52f), Vector2.zero, new Vector2(370f, 370f));
        StyleSurface(enemyHalo, new Color(0.78f, 0.24f, 0.26f, 0.10f));
        GameObject playerRing = CreateUiObject("PlayerRing", hudRoot.transform, new Vector2(0.26f, 0.52f), new Vector2(0.26f, 0.52f), Vector2.zero, new Vector2(330f, 330f));
        StyleSurface(playerRing, new Color(0.34f, 0.80f, 0.95f, 0.08f));
        GameObject enemyRing = CreateUiObject("EnemyRing", hudRoot.transform, new Vector2(0.74f, 0.52f), new Vector2(0.74f, 0.52f), Vector2.zero, new Vector2(330f, 330f));
        StyleSurface(enemyRing, new Color(0.95f, 0.38f, 0.38f, 0.08f));
        GameObject playerFrame = CreateUiObject("PlayerFrame", hudRoot.transform, new Vector2(0.26f, 0.52f), new Vector2(0.26f, 0.52f), Vector2.zero, new Vector2(310f, 300f));
        StyleSurface(playerFrame, new Color(0.10f, 0.19f, 0.27f, 0.92f));
        GameObject enemyFrame = CreateUiObject("EnemyFrame", hudRoot.transform, new Vector2(0.74f, 0.52f), new Vector2(0.74f, 0.52f), Vector2.zero, new Vector2(310f, 300f));
        StyleSurface(enemyFrame, new Color(0.25f, 0.11f, 0.14f, 0.92f));
        GameObject playerPedestal = CreateUiObject("PlayerPedestal", hudRoot.transform, new Vector2(0.26f, 0.5f), new Vector2(0.26f, 0.5f), new Vector2(0f, -118f), new Vector2(190f, 18f));
        StyleSurface(playerPedestal, new Color(0.52f, 0.86f, 0.95f, 0.16f));
        GameObject enemyPedestal = CreateUiObject("EnemyPedestal", hudRoot.transform, new Vector2(0.74f, 0.5f), new Vector2(0.74f, 0.5f), new Vector2(0f, -118f), new Vector2(190f, 18f));
        StyleSurface(enemyPedestal, new Color(0.95f, 0.48f, 0.48f, 0.16f));
        CreateInsetFrame(playerFrame.transform, "PlayerFrameBorder", new Vector2(278f, 268f), new Color(0.50f, 0.88f, 0.98f, 0.12f));
        CreateInsetFrame(enemyFrame.transform, "EnemyFrameBorder", new Vector2(278f, 268f), new Color(1f, 0.62f, 0.62f, 0.12f));
        CreateCombatPortrait(playerFrame.transform, "PlayerPortrait", true);
        CreateCombatPortrait(enemyFrame.transform, "EnemyPortrait", false);
        TMP_Text playerFrameLabel = CreateLabel("PlayerFrameLabel", playerFrame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 34f), "Witch");
        StyleText(playerFrameLabel, 24f, FontStyles.Bold, new Color(0.83f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text enemyFrameLabel = CreateLabel("EnemyFrameLabel", enemyFrame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 34f), "Tower Spawn");
        StyleText(enemyFrameLabel, 24f, FontStyles.Bold, new Color(1f, 0.86f, 0.86f, 1f), TextAlignmentOptions.Center);
        Image playerHpFillImage = CreateHealthBar(playerFrame.transform, "PlayerHpBar", new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(240f, 22f), new Color(0.22f, 0.74f, 0.65f, 1f));
        Image enemyHpFillImage = CreateHealthBar(enemyFrame.transform, "EnemyHpBar", new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(240f, 22f), new Color(0.93f, 0.42f, 0.49f, 1f));
        TMP_Text playerFrameHint = CreateLabel("PlayerFrameHint", playerFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(240f, 140f), "Crit and drain thrive when your upgrades are online.");
        StyleText(playerFrameHint, 18f, FontStyles.Normal, new Color(0.76f, 0.86f, 0.96f, 0.92f), TextAlignmentOptions.Center);
        TMP_Text enemyFrameHint = CreateLabel("EnemyFrameHint", enemyFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(240f, 140f), "Higher floors push HP, damage, and reward values upward.");
        StyleText(enemyFrameHint, 18f, FontStyles.Normal, new Color(0.96f, 0.83f, 0.83f, 0.92f), TextAlignmentOptions.Center);
        GameObject playerNameplate = CreateUiObject("PlayerNameplate", playerFrame.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(164f, 28f));
        StyleSurface(playerNameplate, new Color(0.24f, 0.63f, 0.83f, 0.92f));
        TMP_Text playerRoleText = CreateLabel("PlayerRoleText", playerNameplate.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 22f), "FRONTLINE WITCH");
        StyleText(playerRoleText, 13f, FontStyles.Bold, new Color(0.06f, 0.10f, 0.15f, 0.95f), TextAlignmentOptions.Center);
        GameObject enemyNameplate = CreateUiObject("EnemyNameplate", enemyFrame.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(170f, 28f));
        StyleSurface(enemyNameplate, new Color(0.78f, 0.28f, 0.30f, 0.92f));
        TMP_Text enemyRoleText = CreateLabel("EnemyRoleText", enemyNameplate.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 22f), "TOWER HOSTILE");
        StyleText(enemyRoleText, 13f, FontStyles.Bold, new Color(0.15f, 0.07f, 0.08f, 0.95f), TextAlignmentOptions.Center);
        TMP_Text versusText = CreateLabel("VersusText", hudRoot.transform, new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(160f, 48f), "VS");
        StyleText(versusText, 38f, FontStyles.Bold, new Color(1f, 0.89f, 0.53f, 1f), TextAlignmentOptions.Center);
        TMP_Text versusSubtext = CreateLabel("VersusSubtext", hudRoot.transform, new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(260f, 24f), "AUTO BATTLE LANE");
        StyleText(versusSubtext, 14f, FontStyles.Bold, new Color(0.94f, 0.82f, 0.58f, 0.88f), TextAlignmentOptions.Center);
        GameObject skillBar = CreateUiObject("SkillBar", hudRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(860f, 180f));
        StyleSurface(skillBar, new Color(0.08f, 0.10f, 0.15f, 0.92f));
        CreateInsetFrame(skillBar.transform, "SkillBarFrame", new Vector2(820f, 140f), new Color(0.52f, 0.86f, 0.91f, 0.10f));
        TMP_Text skillBarTitle = CreateLabel("SkillBarTitle", skillBar.transform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(520f, 30f), "Active Skills");
        StyleText(skillBarTitle, 24f, FontStyles.Bold, new Color(0.94f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text skillBarHint = CreateLabel("SkillBarHint", skillBar.transform, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(620f, 26f), "Buttons dim during cooldown and light back up when ready.");
        StyleText(skillBarHint, 16f, FontStyles.Normal, new Color(0.73f, 0.80f, 0.90f, 0.94f), TextAlignmentOptions.Center);
        CreateSkillTag(skillBar.transform, "SkillTagLeft", new Vector2(0.2f, 0f), new Vector2(0f, 152f), "FAST HIT");
        CreateSkillTag(skillBar.transform, "SkillTagCenter", new Vector2(0.5f, 0f), new Vector2(0f, 152f), "SUSTAIN");
        CreateSkillTag(skillBar.transform, "SkillTagRight", new Vector2(0.8f, 0f), new Vector2(0f, 152f), "MITIGATE");

        TMP_Text floorText = CreateLabel("FloorText", floorBadge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 34f), "Floor 1");
        TMP_Text playerHpText = CreateLabel("PlayerHpText", playerFrame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(250f, 40f), "Player HP");
        TMP_Text enemyHpText = CreateLabel("EnemyHpText", enemyFrame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(250f, 40f), "Enemy HP");
        TMP_Text skill1Text = CreateLabel("Skill1Text", skillBar.transform, new Vector2(0.2f, 0f), new Vector2(0f, 118f), new Vector2(180f, 40f), "Strike");
        TMP_Text skill2Text = CreateLabel("Skill2Text", skillBar.transform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(180f, 40f), "Drain");
        TMP_Text skill3Text = CreateLabel("Skill3Text", skillBar.transform, new Vector2(0.8f, 0f), new Vector2(0f, 118f), new Vector2(180f, 40f), "Guard");

        Button skillButton1 = CreateButton(skillBar.transform, "SkillButton1", "Strike", new Vector2(0.2f, 0f), new Vector2(0f, 60f), new Vector2(180f, 52f));
        Button skillButton2 = CreateButton(skillBar.transform, "SkillButton2", "Drain", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(180f, 52f));
        Button skillButton3 = CreateButton(skillBar.transform, "SkillButton3", "Guard", new Vector2(0.8f, 0f), new Vector2(0f, 60f), new Vector2(180f, 52f));

        GameObject winLabel = CreateSimplePanel("WinLabel", hudRoot.transform, new Vector2(0.35f, 0.5f), new Vector2(220f, 70f), "Victory");
        GameObject loseLabel = CreateSimplePanel("LoseLabel", hudRoot.transform, new Vector2(0.65f, 0.5f), new Vector2(220f, 70f), "Defeat");
        winLabel.SetActive(false);
        loseLabel.SetActive(false);

        GameObject floorPillTop = CreateUiObject("BattleFloorPillTop", floorBadge.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(150f, 16f));
        StyleSurface(floorPillTop, new Color(0.97f, 0.84f, 0.56f, 0.16f));
        StyleText(floorText, 30f, FontStyles.Bold, new Color(1f, 0.90f, 0.60f, 1f), TextAlignmentOptions.Center);
        StyleText(playerHpText, 28f, FontStyles.Bold, new Color(0.84f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
        StyleText(enemyHpText, 28f, FontStyles.Bold, new Color(1f, 0.86f, 0.86f, 1f), TextAlignmentOptions.Center);
        StyleText(skill1Text, 22f, FontStyles.Bold, new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        StyleText(skill2Text, 22f, FontStyles.Bold, new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        StyleText(skill3Text, 22f, FontStyles.Bold, new Color(0.92f, 0.96f, 1f, 1f), TextAlignmentOptions.Center);
        StyleButton(skillButton1, new Color(0.14f, 0.40f, 0.62f, 1f));
        StyleButton(skillButton2, new Color(0.18f, 0.49f, 0.44f, 1f));
        StyleButton(skillButton3, new Color(0.56f, 0.40f, 0.16f, 1f));
        StyleText(floorText, 28f, FontStyles.Bold, new Color(0.99f, 0.92f, 0.66f, 1f), TextAlignmentOptions.Center);

        ResultPanelController resultPanel = CreateResultPanel(hudRoot.transform, battleSceneController);
        BattleFeedbackController feedbackController = CreateFeedbackController(hudRoot.transform);

        skillButton1.onClick.AddListener(battleSceneController.UseSkillStrike);
        skillButton2.onClick.AddListener(battleSceneController.UseSkillDrain);
        skillButton3.onClick.AddListener(battleSceneController.UseSkillGuard);

        SetObjectField(battleSceneController, "stateMachine", stateMachine);

        SetObjectField(stateMachine, "hudController", hudController);
        SetObjectField(stateMachine, "simulator", simulator);
        SetObjectField(stateMachine, "feedbackController", feedbackController);

        SetObjectField(hudController, "floorText", floorText);
        SetObjectField(hudController, "threatText", battleThreatText);
        SetObjectField(hudController, "encounterText", battleEncounterText);
        SetObjectField(hudController, "playerHpText", playerHpText);
        SetObjectField(hudController, "enemyHpText", enemyHpText);
        SetObjectField(hudController, "playerHpFillImage", playerHpFillImage);
        SetObjectField(hudController, "enemyHpFillImage", enemyHpFillImage);
        SetObjectField(hudController, "skillCooldown1Text", skill1Text);
        SetObjectField(hudController, "skillCooldown2Text", skill2Text);
        SetObjectField(hudController, "skillCooldown3Text", skill3Text);
        SetObjectField(hudController, "skillButton1", skillButton1);
        SetObjectField(hudController, "skillButton2", skillButton2);
        SetObjectField(hudController, "skillButton3", skillButton3);
        SetObjectField(hudController, "winLabel", winLabel);
        SetObjectField(hudController, "loseLabel", loseLabel);
        SetObjectField(hudController, "resultPanelController", resultPanel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ClearScene(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i += 1)
        {
            if (scenes[i].path == scenePath)
            {
                if (!scenes[i].enabled)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                }

                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static Canvas CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.localScale = Vector3.one;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        return panel;
    }

    private static void CreateBackdrop(Transform parent, Color color)
    {
        GameObject backdrop = CreateUiObject("Backdrop", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = backdrop.AddComponent<Image>();
        image.color = color;
        backdrop.transform.SetAsFirstSibling();
    }

    private static void CreateBackdropBand(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject band = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        Image image = band.AddComponent<Image>();
        image.color = color;
        band.transform.SetAsFirstSibling();
    }

    private static void CreateGlow(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject glow = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        Image image = glow.AddComponent<Image>();
        image.color = color;
        glow.transform.SetAsFirstSibling();
    }

    private static void TintPanel(GameObject panel, Color color)
    {
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    private static void StyleSurface(GameObject root, Color color)
    {
        Image image = root.GetComponent<Image>();
        if (image == null)
        {
            image = root.AddComponent<Image>();
        }

        image.color = color;
    }

    private static void StyleCard(GameObject root, Color color)
    {
        StyleSurface(root, color);
        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        GameObject topEdge = CreateUiObject(root.name + "TopEdge", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(rect.sizeDelta.x - 46f, 2f));
        StyleSurface(topEdge, new Color(0.97f, 0.85f, 0.58f, 0.18f));
        topEdge.transform.SetAsFirstSibling();

        GameObject bottomEdge = CreateUiObject(root.name + "BottomEdge", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(rect.sizeDelta.x - 68f, 2f));
        StyleSurface(bottomEdge, new Color(0.60f, 0.88f, 0.92f, 0.10f));
        bottomEdge.transform.SetAsFirstSibling();

        CreateCornerSigil(root.transform, root.name + "CornerSigilTL", new Vector2(0f, 1f), new Vector2(18f, -18f), new Color(0.98f, 0.85f, 0.58f, 0.18f));
        CreateCornerSigil(root.transform, root.name + "CornerSigilTR", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Color(0.72f, 0.90f, 0.96f, 0.14f));
        CreateCornerSigil(root.transform, root.name + "CornerSigilBL", new Vector2(0f, 0f), new Vector2(18f, 18f), new Color(0.72f, 0.90f, 0.96f, 0.10f));
        CreateCornerSigil(root.transform, root.name + "CornerSigilBR", new Vector2(1f, 0f), new Vector2(-18f, 18f), new Color(0.98f, 0.85f, 0.58f, 0.12f));
    }

    private static void CreateSectionHeader(Transform parent, string title, string subtitle)
    {
        GameObject ribbon = CreateUiObject(title.Replace(" ", string.Empty) + "Ribbon", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(420f, 22f));
        StyleSurface(ribbon, new Color(0.11f, 0.15f, 0.21f, 0.90f));
        GameObject ribbonLine = CreateUiObject(title.Replace(" ", string.Empty) + "RibbonLine", ribbon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 2f));
        StyleSurface(ribbonLine, new Color(0.97f, 0.84f, 0.56f, 0.20f));
        TMP_Text titleText = CreateLabel(title.Replace(" ", string.Empty) + "Header", parent, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(760f, 40f), title);
        StyleText(titleText, 32f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text subtitleText = CreateLabel(subtitle.Replace(" ", string.Empty) + "Subtitle", parent, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(840f, 32f), subtitle);
        StyleText(subtitleText, 18f, FontStyles.Normal, new Color(0.72f, 0.80f, 0.90f, 0.95f), TextAlignmentOptions.Center);
    }

    private static GameObject CreateSimplePanel(string name, Transform parent, Vector2 anchor, Vector2 size, string label)
    {
        GameObject panel = CreateUiObject(name, parent, anchor, anchor, Vector2.zero, size);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.18f, 0.25f, 0.3f, 0.95f);
        CreateLabel("Label", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 20f), label);
        return panel;
    }

    private static Image CreateHealthBar(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color fillColor)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);

        GameObject fillObject = CreateUiObject("Fill", root.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6f, -6f));
        Image fill = fillObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        return fill;
    }

    private static GameObject CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return gameObject;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string text)
    {
        GameObject labelObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        TextMeshProUGUI textComponent = labelObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 30f;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        return textComponent;
    }

    private static TMP_Text CreateCtaLabel(string name, Transform parent, string text)
    {
        TMP_Text label = CreateLabel(name, parent, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(760f, 44f), text);
        StyleText(label, 18f, FontStyles.Bold, new Color(1f, 0.88f, 0.58f, 1f), TextAlignmentOptions.Center);
        return label;
    }

    private static TMP_Text CreateNavBadge(Transform parent, string name, string text)
    {
        GameObject badgeRoot = CreateUiObject(name + "Root", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -10f), new Vector2(28f, 28f));
        StyleSurface(badgeRoot, new Color(0.86f, 0.28f, 0.35f, 1f));
        TMP_Text label = CreateLabel(name, badgeRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), text);
        StyleText(label, 16f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        badgeRoot.SetActive(false);
        return label;
    }

    private static void StyleText(TMP_Text textComponent, float fontSize, FontStyles fontStyle, Color color, TextAlignmentOptions alignment)
    {
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.color = color;
        textComponent.alignment = alignment;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.35f, 0.42f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        CreateLabel("Label", buttonObject.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10f, 10f), label);
        return button;
    }

    private static void StyleButton(Button button, Color color)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            GameObject frame = CreateUiObject(button.name + "Frame", button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(rect.sizeDelta.x - 18f, rect.sizeDelta.y - 18f));
            StyleSurface(frame, new Color(1f, 1f, 1f, 0.08f));
            frame.transform.SetAsFirstSibling();

            GameObject accentLeft = CreateUiObject(button.name + "AccentLeft", button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, rect.sizeDelta.y - 24f));
            StyleSurface(accentLeft, new Color(0.98f, 0.87f, 0.62f, 0.22f));
            accentLeft.transform.SetAsFirstSibling();

            GameObject accentRight = CreateUiObject(button.name + "AccentRight", button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(16f, rect.sizeDelta.y - 24f));
            StyleSurface(accentRight, new Color(0.72f, 0.90f, 0.96f, 0.14f));
            accentRight.transform.SetAsFirstSibling();
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            StyleText(text, 24f, FontStyles.Bold, new Color(0.96f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        }
    }

    private static Button CreateNavButton(Transform parent, string label, Vector2 anchoredPosition, MonoBehaviour target, string methodName, Vector2? size = null)
    {
        Button button = CreateButton(parent, label + "Button", label, new Vector2(0.5f, 0.5f), anchoredPosition, size ?? new Vector2(160f, 44f));
        button.onClick.AddListener(new UnityEngine.Events.UnityAction(() =>
        {
            target.Invoke(methodName, 0f);
        }));
        return button;
    }

    private static PlayerStatusView CreatePlayerStatusView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(720f, 320f));
        PlayerStatusView view = root.AddComponent<PlayerStatusView>();
        CreateInsetFrame(root.transform, name + "Frame", new Vector2(680f, 280f), new Color(0.56f, 0.90f, 0.96f, 0.08f));
        CreateHorizontalAccent(root.transform, name + "TopAccent", new Vector2(0f, -122f), new Vector2(630f, 2f), new Color(0.95f, 0.83f, 0.56f, 0.34f));
        CreateHorizontalAccent(root.transform, name + "BottomAccent", new Vector2(0f, 118f), new Vector2(630f, 2f), new Color(0.42f, 0.81f, 0.90f, 0.18f));
        CreateStatChip(root.transform, name + "LevelChip", new Vector2(-222f, -22f), "LEVEL");
        CreateStatChip(root.transform, name + "FloorChip", new Vector2(-222f, 48f), "FLOOR");
        CreateStatChip(root.transform, name + "ExpChip", new Vector2(-222f, 118f), "EXP");
        GameObject commandBadge = CreateUiObject(name + "CommandBadge", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-100f, 54f), new Vector2(168f, 30f));
        StyleSurface(commandBadge, new Color(0.94f, 0.83f, 0.54f, 0.94f));
        TMP_Text commandBadgeText = CreateLabel(name + "CommandBadgeText", commandBadge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(148f, 24f), "RUN SIGNAL");
        StyleText(commandBadgeText, 14f, FontStyles.Bold, new Color(0.07f, 0.09f, 0.12f, 0.96f), TextAlignmentOptions.Center);
        TMP_Text levelText = CreateLabel("LevelText", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(560f, 34f), "Witch Lv. 1");
        StyleText(levelText, 24f, FontStyles.Bold, new Color(0.96f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        SetObjectField(view, "levelText", levelText);
        TMP_Text floorText = CreateLabel("FloorText", root.transform, new Vector2(0.5f, 0.78f), new Vector2(0f, 4f), new Vector2(560f, 34f), "Floor 1");
        StyleText(floorText, 26f, FontStyles.Bold, new Color(0.98f, 0.86f, 0.58f, 0.98f), TextAlignmentOptions.Center);
        SetObjectField(view, "floorText", floorText);
        TMP_Text expText = CreateLabel("ExpText", root.transform, new Vector2(0.5f, 0.56f), new Vector2(0f, 6f), new Vector2(560f, 30f), "EXP 0 / 10");
        StyleText(expText, 18f, FontStyles.Normal, new Color(0.80f, 0.88f, 0.96f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(view, "expText", expText);
        TMP_Text progressText = CreateLabel("ProgressText", root.transform, new Vector2(0.5f, 0.34f), new Vector2(0f, -4f), new Vector2(640f, 34f), "Next Floor: 2");
        StyleText(progressText, 18f, FontStyles.Bold, new Color(0.94f, 0.85f, 0.60f, 0.96f), TextAlignmentOptions.Center);
        SetObjectField(view, "progressText", progressText);
        TMP_Text rewardForecastText = CreateLabel("RewardForecastText", root.transform, new Vector2(0.5f, 0.22f), new Vector2(0f, -6f), new Vector2(660f, 30f), "Reward: 10 Gold / 5 EXP");
        StyleText(rewardForecastText, 16f, FontStyles.Normal, new Color(0.86f, 0.90f, 1f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "rewardForecastText", rewardForecastText);
        TMP_Text threatText = CreateLabel("ThreatText", root.transform, new Vector2(0.5f, 0.14f), new Vector2(0f, -2f), new Vector2(660f, 30f), "Danger: Low");
        StyleText(threatText, 16f, FontStyles.Bold, new Color(0.96f, 0.73f, 0.52f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(view, "threatText", threatText);
        TMP_Text confidenceText = CreateLabel("ConfidenceText", root.transform, new Vector2(0.5f, 0.06f), new Vector2(0f, 8f), new Vector2(660f, 30f), "Push Ready");
        StyleText(confidenceText, 16f, FontStyles.Bold, new Color(0.72f, 0.90f, 0.82f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(view, "confidenceText", confidenceText);
        TMP_Text loadoutAlertText = CreateLabel("LoadoutAlertText", root.transform, new Vector2(0.5f, 0.07f), new Vector2(0f, 2f), new Vector2(670f, 30f), "Loadout Alert: current gear is already on the strongest unlocked setup.");
        StyleText(loadoutAlertText, 14f, FontStyles.Normal, new Color(0.87f, 0.86f, 0.96f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "loadoutAlertText", loadoutAlertText);
        TMP_Text goldRouteText = CreateLabel("GoldRouteText", root.transform, new Vector2(0.5f, 0.04f), new Vector2(0f, 2f), new Vector2(670f, 30f), "Gold Route: spend now; your stash already covers the next upgrade break point.");
        StyleText(goldRouteText, 14f, FontStyles.Normal, new Color(0.92f, 0.83f, 0.58f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "goldRouteText", goldRouteText);
        TMP_Text upgradeRouteText = CreateLabel("UpgradeRouteText", root.transform, new Vector2(0.5f, 0.01f), new Vector2(0f, 2f), new Vector2(670f, 30f), "Upgrade Route: Attack is the fastest spend at 10 Gold for a cleaner push.");
        StyleText(upgradeRouteText, 14f, FontStyles.Normal, new Color(0.84f, 0.91f, 0.98f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "upgradeRouteText", upgradeRouteText);
        TMP_Text rewardRouteText = CreateLabel("RewardRouteText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 2f), new Vector2(670f, 30f), "Reward Route: no claims are waiting; push the next floor for fresh rewards.");
        StyleText(rewardRouteText, 14f, FontStyles.Normal, new Color(0.95f, 0.86f, 0.70f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "rewardRouteText", rewardRouteText);
        TMP_Text pushWindowText = CreateLabel("PushWindowText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 18f), new Vector2(670f, 30f), "Push Window: open now; floor 2 is ready for an immediate push.");
        StyleText(pushWindowText, 14f, FontStyles.Normal, new Color(0.78f, 0.94f, 0.80f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "pushWindowText", pushWindowText);
        TMP_Text roiReadText = CreateLabel("RoiReadText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 34f), new Vector2(670f, 30f), "ROI Read: one clear covers the next upgrade outright with 10 Gold.");
        StyleText(roiReadText, 14f, FontStyles.Normal, new Color(0.79f, 0.87f, 0.98f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "roiReadText", roiReadText);
        TMP_Text decisionLineText = CreateLabel("DecisionLineText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 50f), new Vector2(670f, 30f), "Decision Line: push now unless you want to squeeze extra value from open claims.");
        StyleText(decisionLineText, 14f, FontStyles.Bold, new Color(0.98f, 0.95f, 0.84f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(view, "decisionLineText", decisionLineText);
        TMP_Text decisionBadgeText = CreateLabel("DecisionBadgeText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 66f), new Vector2(520f, 34f), "Decision: Push");
        StyleText(decisionBadgeText, 18f, FontStyles.Bold, new Color(0.56f, 0.93f, 0.68f, 1f), TextAlignmentOptions.Center);
        SetObjectField(view, "decisionBadgeText", decisionBadgeText);
        TMP_Text commandStackText = CreateLabel("CommandStackText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 92f), new Vector2(680f, 42f), "Command Stack: 1. Open floor 2  2. Keep the current build  3. Push floor 2 now.");
        StyleText(commandStackText, 14f, FontStyles.Normal, new Color(0.86f, 0.92f, 0.98f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "commandStackText", commandStackText);
        TMP_Text momentumReadText = CreateLabel("MomentumReadText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 122f), new Vector2(680f, 30f), "Momentum Read: live; your current build can press floor 2 immediately.");
        StyleText(momentumReadText, 14f, FontStyles.Normal, new Color(0.83f, 0.95f, 0.87f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "momentumReadText", momentumReadText);
        TMP_Text runCallText = CreateLabel("RunCallText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 146f), new Vector2(680f, 30f), "Run Call: green light, take floor 2 now.");
        StyleText(runCallText, 15f, FontStyles.Bold, new Color(0.98f, 0.92f, 0.72f, 0.96f), TextAlignmentOptions.Center);
        SetObjectField(view, "runCallText", runCallText);
        TMP_Text riskBufferText = CreateLabel("RiskBufferText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 170f), new Vector2(680f, 30f), "Risk Buffer: workable; floor 2 leaves about 30 HP of breathing room.");
        StyleText(riskBufferText, 14f, FontStyles.Normal, new Color(0.88f, 0.90f, 0.98f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "riskBufferText", riskBufferText);
        TMP_Text enemyTempoText = CreateLabel("EnemyTempoText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 194f), new Vector2(680f, 30f), "Enemy Tempo: floor 2 swings every 1.00s for 8 damage pressure.");
        StyleText(enemyTempoText, 14f, FontStyles.Normal, new Color(0.95f, 0.84f, 0.84f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "enemyTempoText", enemyTempoText);
        TMP_Text damageRaceText = CreateLabel("DamageRaceText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 218f), new Vector2(680f, 30f), "Damage Race: favored; you close in 3 hits before the enemy's 6.");
        StyleText(damageRaceText, 14f, FontStyles.Normal, new Color(0.92f, 0.90f, 0.80f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "damageRaceText", damageRaceText);
        TMP_Text burstReadText = CreateLabel("BurstReadText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 242f), new Vector2(680f, 30f), "Burst Read: closeout range; one strong opener leaves only a sliver behind.");
        StyleText(burstReadText, 14f, FontStyles.Normal, new Color(0.97f, 0.88f, 0.74f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "burstReadText", burstReadText);
        TMP_Text killClockText = CreateLabel("KillClockText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 266f), new Vector2(680f, 30f), "Kill Clock: steady; expect roughly 3.0s to close floor 2.");
        StyleText(killClockText, 14f, FontStyles.Normal, new Color(0.83f, 0.92f, 0.96f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "killClockText", killClockText);
        TMP_Text critWindowText = CreateLabel("CritWindowText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 290f), new Vector2(680f, 30f), "Crit Window: useful; a 10% crit chance can trim cleanup damage.");
        StyleText(critWindowText, 14f, FontStyles.Normal, new Color(0.94f, 0.85f, 0.96f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "critWindowText", critWindowText);
        TMP_Text survivalWindowText = CreateLabel("SurvivalWindowText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 314f), new Vector2(680f, 30f), "Survival Window: fair; you have about 5.0s before the floor turns lethal.");
        StyleText(survivalWindowText, 14f, FontStyles.Normal, new Color(0.86f, 0.96f, 0.88f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "survivalWindowText", survivalWindowText);
        TMP_Text clockEdgeText = CreateLabel("ClockEdgeText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 338f), new Vector2(680f, 30f), "Clock Edge: favorable; your timer stays ahead by about 4.0s.");
        StyleText(clockEdgeText, 14f, FontStyles.Normal, new Color(0.94f, 0.92f, 0.82f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "clockEdgeText", clockEdgeText);
        TMP_Text tempoVerdictText = CreateLabel("TempoVerdictText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 362f), new Vector2(680f, 30f), "Tempo Verdict: yours to control; you have time to cash out and still dictate the floor.");
        StyleText(tempoVerdictText, 14f, FontStyles.Normal, new Color(0.99f, 0.90f, 0.73f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "tempoVerdictText", tempoVerdictText);
        TMP_Text pressureCallText = CreateLabel("PressureCallText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 386f), new Vector2(680f, 30f), "Pressure Call: favored; you can safely bank rewards and still own the next exchange.");
        StyleText(pressureCallText, 14f, FontStyles.Normal, new Color(0.96f, 0.87f, 0.80f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "pressureCallText", pressureCallText);
        TMP_Text rewardPaceText = CreateLabel("RewardPaceText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 410f), new Vector2(680f, 30f), "Reward Pace: steady; expect roughly 180 Gold and 90 EXP per minute.");
        StyleText(rewardPaceText, 14f, FontStyles.Normal, new Color(0.86f, 0.95f, 0.82f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "rewardPaceText", rewardPaceText);
        TMP_Text priorityText = CreateLabel("PriorityText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 434f), new Vector2(660f, 30f), "Priority Tab: Battle next, floor 2 is the clean push.");
        StyleText(priorityText, 15f, FontStyles.Normal, new Color(0.92f, 0.84f, 0.58f, 0.96f), TextAlignmentOptions.Center);
        SetObjectField(view, "priorityText", priorityText);
        TMP_Text summaryText = CreateLabel("SummaryText", root.transform, new Vector2(0.5f, 0.00f), new Vector2(0f, 466f), new Vector2(640f, 36f), "Prepare and descend.");
        StyleText(summaryText, 18f, FontStyles.Normal, new Color(0.79f, 0.86f, 0.94f, 0.95f), TextAlignmentOptions.Center);
        SetObjectField(view, "summaryText", summaryText);
        TMP_Text actionText = CreateLabel("ActionText", root.transform, new Vector2(0.5f, 0.0f), new Vector2(0f, 504f), new Vector2(660f, 34f), "Tap the gate to challenge the next floor.");
        StyleText(actionText, 16f, FontStyles.Italic, new Color(0.70f, 0.88f, 0.84f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "actionText", actionText);

        loadoutAlertText.gameObject.SetActive(false);
        goldRouteText.gameObject.SetActive(false);
        upgradeRouteText.gameObject.SetActive(false);
        rewardRouteText.gameObject.SetActive(false);
        pushWindowText.gameObject.SetActive(false);
        roiReadText.gameObject.SetActive(false);
        decisionLineText.gameObject.SetActive(false);
        decisionBadgeText.gameObject.SetActive(false);
        commandStackText.gameObject.SetActive(false);
        momentumReadText.gameObject.SetActive(false);
        runCallText.gameObject.SetActive(false);
        riskBufferText.gameObject.SetActive(false);
        enemyTempoText.gameObject.SetActive(false);
        damageRaceText.gameObject.SetActive(false);
        burstReadText.gameObject.SetActive(false);
        killClockText.gameObject.SetActive(false);
        critWindowText.gameObject.SetActive(false);
        survivalWindowText.gameObject.SetActive(false);
        clockEdgeText.gameObject.SetActive(false);
        tempoVerdictText.gameObject.SetActive(false);
        pressureCallText.gameObject.SetActive(false);
        rewardPaceText.gameObject.SetActive(false);
        priorityText.gameObject.SetActive(false);
        return view;
    }

    private static ResourceView CreateResourceView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(500f, 40f));
        ResourceView view = root.AddComponent<ResourceView>();
        SetObjectField(view, "goldText", CreateLabel("GoldText", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 30f), "0"));
        return view;
    }

    private static IdleRewardView CreateIdleRewardView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(600f, 50f));
        IdleRewardView view = root.AddComponent<IdleRewardView>();
        SetObjectField(view, "statusText", CreateLabel("StatusText", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 40f), "Idle Reward: None"));
        return view;
    }

    private static UpgradeStatusView CreateUpgradeStatusView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(700f, 126f));
        UpgradeStatusView view = root.AddComponent<UpgradeStatusView>();
        SetObjectField(view, "labelText", CreateLabel("LabelText", root.transform, new Vector2(0.15f, 0.68f), Vector2.zero, new Vector2(140f, 30f), "Stat"));
        SetObjectField(view, "levelText", CreateLabel("LevelText", root.transform, new Vector2(0.38f, 0.68f), Vector2.zero, new Vector2(140f, 30f), "Lv. 0"));
        SetObjectField(view, "costText", CreateLabel("CostText", root.transform, new Vector2(0.62f, 0.68f), Vector2.zero, new Vector2(160f, 30f), "Cost 10"));
        SetObjectField(view, "bonusText", CreateLabel("BonusText", root.transform, new Vector2(0.85f, 0.68f), Vector2.zero, new Vector2(120f, 30f), "+0"));
        TMP_Text impactText = CreateLabel(name + "ImpactText", root.transform, new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(640f, 34f), "Impact: preview unavailable.");
        StyleText(impactText, 15f, FontStyles.Normal, new Color(0.82f, 0.88f, 0.95f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "impactText", impactText);
        return view;
    }

    private static EquipmentStatusView CreateEquipmentStatusView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(700f, 290f));
        EquipmentStatusView view = root.AddComponent<EquipmentStatusView>();
        SetObjectField(view, "weaponText", CreateLabel("WeaponText", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(560f, 30f), "Weapon: -"));
        SetObjectField(view, "armorText", CreateLabel("ArmorText", root.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(560f, 30f), "Armor: -"));
        SetObjectField(view, "accessoryText", CreateLabel("AccessoryText", root.transform, new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(560f, 30f), "Accessory: -"));
        TMP_Text summaryText = CreateLabel("EquipmentSummaryText", root.transform, new Vector2(0.5f, 0.24f), new Vector2(0f, 10f), new Vector2(640f, 56f), "Battle Build: preview unavailable");
        SetObjectField(view, "summaryText", summaryText);
        TMP_Text matchupText = CreateLabel("EquipmentMatchupText", root.transform, new Vector2(0.5f, 0.10f), new Vector2(0f, 6f), new Vector2(640f, 32f), "Next Floor Read: unavailable");
        StyleText(matchupText, 15f, FontStyles.Normal, new Color(0.82f, 0.88f, 0.95f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(view, "matchupText", matchupText);
        TMP_Text loadoutImpactText = CreateLabel("EquipmentImpactText", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(650f, 54f), "Loadout Impact: unavailable");
        StyleText(loadoutImpactText, 14f, FontStyles.Normal, new Color(0.80f, 0.84f, 0.92f, 0.92f), TextAlignmentOptions.Center);
        SetObjectField(view, "loadoutImpactText", loadoutImpactText);
        return view;
    }

    private static DailyRewardView CreateDailyRewardView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(600f, 50f));
        DailyRewardView view = root.AddComponent<DailyRewardView>();
        SetObjectField(view, "statusText", CreateLabel("StatusText", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 40f), "Daily Reward"));
        return view;
    }

    private static MissionItemView CreateMissionItemView(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, position, new Vector2(700f, 90f));
        MissionItemView view = root.AddComponent<MissionItemView>();
        SetObjectField(view, "titleText", CreateLabel("TitleText", root.transform, new Vector2(0.2f, 0.5f), Vector2.zero, new Vector2(220f, 30f), "Mission"));
        SetObjectField(view, "progressText", CreateLabel("ProgressText", root.transform, new Vector2(0.55f, 0.5f), Vector2.zero, new Vector2(160f, 30f), "0/1"));
        SetObjectField(view, "rewardText", CreateLabel("RewardText", root.transform, new Vector2(0.82f, 0.5f), Vector2.zero, new Vector2(180f, 30f), "Reward 0 Gold"));
        return view;
    }

    private static ResultPanelController CreateResultPanel(Transform parent, BattleSceneController battleSceneController)
    {
        GameObject root = CreateUiObject("ResultPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 390f));
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);
        GameObject resultBackPlate = CreateUiObject("ResultBackPlate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(744f, 430f));
        StyleSurface(resultBackPlate, new Color(0.07f, 0.09f, 0.13f, 0.54f));
        resultBackPlate.transform.SetAsFirstSibling();
        GameObject resultBottomShadow = CreateUiObject("ResultBottomShadow", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(630f, 26f));
        StyleSurface(resultBottomShadow, new Color(0.05f, 0.06f, 0.09f, 0.34f));
        CreateInsetFrame(root.transform, "ResultFrame", new Vector2(654f, 344f), new Color(1f, 0.84f, 0.56f, 0.12f));
        GameObject resultGlow = CreateUiObject("ResultGlow", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(260f, 120f));
        StyleSurface(resultGlow, new Color(0.97f, 0.82f, 0.48f, 0.08f));
        GameObject resultCrest = CreateUiObject("ResultCrest", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(86f, 16f));
        StyleSurface(resultCrest, new Color(0.97f, 0.84f, 0.56f, 0.18f));
        GameObject resultCrestCore = CreateUiObject("ResultCrestCore", resultCrest.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        RectTransform resultCrestCoreRect = resultCrestCore.GetComponent<RectTransform>();
        resultCrestCoreRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        StyleSurface(resultCrestCore, new Color(0.97f, 0.84f, 0.56f, 0.26f));
        GameObject rewardStrip = CreateUiObject("RewardStrip", root.transform, new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f), Vector2.zero, new Vector2(600f, 78f));
        StyleSurface(rewardStrip, new Color(0.13f, 0.16f, 0.22f, 0.92f));
        CreateInsetFrame(rewardStrip.transform, "RewardStripFrame", new Vector2(560f, 40f), new Color(0.95f, 0.88f, 0.62f, 0.10f));
        TMP_Text rewardStripLabel = CreateLabel("RewardStripLabel", rewardStrip.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(300f, 22f), "RUN PAYOUT");
        StyleText(rewardStripLabel, 14f, FontStyles.Bold, new Color(0.97f, 0.84f, 0.58f, 0.95f), TextAlignmentOptions.Center);

        GameObject nextMoveStrip = CreateUiObject("NextMoveStrip", root.transform, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(620f, 54f));
        StyleSurface(nextMoveStrip, new Color(0.11f, 0.14f, 0.19f, 0.88f));
        CreateInsetFrame(nextMoveStrip.transform, "NextMoveStripFrame", new Vector2(580f, 22f), new Color(0.55f, 0.88f, 0.92f, 0.10f));

        ResultPanelController controller = root.AddComponent<ResultPanelController>();
        GameObject resultButtonRail = CreateUiObject("ResultButtonRail", root.transform, new Vector2(0.5f, 0.10f), new Vector2(0.5f, 0.10f), Vector2.zero, new Vector2(610f, 78f));
        StyleSurface(resultButtonRail, new Color(0.10f, 0.13f, 0.18f, 0.90f));
        CreateInsetFrame(resultButtonRail.transform, "ResultButtonRailFrame", new Vector2(570f, 40f), new Color(0.57f, 0.89f, 0.92f, 0.10f));

        Button nextFloorButton = CreateButton(resultButtonRail.transform, "NextFloorButton", "Next Floor", new Vector2(0.30f, 0.5f), Vector2.zero, new Vector2(220f, 52f));
        Button returnHomeButton = CreateButton(resultButtonRail.transform, "ReturnHomeButton", "Return Home", new Vector2(0.70f, 0.5f), Vector2.zero, new Vector2(220f, 52f));
        StyleButton(nextFloorButton, new Color(0.18f, 0.47f, 0.66f, 1f));
        StyleButton(returnHomeButton, new Color(0.40f, 0.23f, 0.47f, 1f));
        CreatePrimaryButtonAura(nextFloorButton.transform, "NextFloorAura", new Vector2(248f, 70f), new Color(0.52f, 0.86f, 0.95f, 0.10f), "ADVANCE");
        CreatePrimaryButtonAura(returnHomeButton.transform, "ReturnHomeAura", new Vector2(248f, 70f), new Color(0.82f, 0.64f, 0.95f, 0.08f), "REGROUP");
        nextFloorButton.onClick.AddListener(battleSceneController.GoToNextFloor);
        returnHomeButton.onClick.AddListener(battleSceneController.ReturnHome);

        SetObjectField(controller, "rootObject", root);
        SetObjectField(controller, "titleText", CreateLabel("TitleText", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 40f), "Result"));
        SetObjectField(controller, "goldText", CreateLabel("GoldText", rewardStrip.transform, new Vector2(0.30f, 0.34f), Vector2.zero, new Vector2(220f, 34f), "Gold +0"));
        SetObjectField(controller, "expText", CreateLabel("ExpText", rewardStrip.transform, new Vector2(0.70f, 0.34f), Vector2.zero, new Vector2(220f, 34f), "EXP +0"));
        TMP_Text summaryText = CreateLabel("ResultSummaryText", root.transform, new Vector2(0.5f, 0.44f), new Vector2(0f, 8f), new Vector2(620f, 44f), "Floor cleared.");
        StyleText(summaryText, 19f, FontStyles.Bold, new Color(0.95f, 0.92f, 0.76f, 0.98f), TextAlignmentOptions.Center);
        SetObjectField(controller, "summaryText", summaryText);
        TMP_Text rewardHintText = CreateLabel("RewardHintText", root.transform, new Vector2(0.5f, 0.30f), new Vector2(0f, 6f), new Vector2(620f, 52f), "Bank rewards, then choose your next move.");
        StyleText(rewardHintText, 16f, FontStyles.Normal, new Color(0.78f, 0.84f, 0.92f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(controller, "rewardHintText", rewardHintText);
        TMP_Text nextRewardForecastText = CreateLabel("NextRewardForecastText", root.transform, new Vector2(0.5f, 0.20f), new Vector2(0f, 2f), new Vector2(620f, 44f), "Next Reward Forecast: floor 2 should pay about 10 Gold and 5 EXP.");
        StyleText(nextRewardForecastText, 15f, FontStyles.Normal, new Color(0.84f, 0.88f, 0.96f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(controller, "nextRewardForecastText", nextRewardForecastText);
        TMP_Text nextActionText = CreateLabel("NextActionText", nextMoveStrip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 28f), "Next");
        StyleText(nextActionText, 16f, FontStyles.Bold, new Color(0.92f, 0.96f, 1f, 0.94f), TextAlignmentOptions.Center);
        SetObjectField(controller, "nextActionText", nextActionText);
        SetObjectField(controller, "nextFloorButton", nextFloorButton);
        SetObjectField(controller, "returnHomeButton", returnHomeButton);
        SetObjectField(controller, "nextFloorButtonText", nextFloorButton.GetComponentInChildren<TMP_Text>(true));
        SetObjectField(controller, "returnHomeButtonText", returnHomeButton.GetComponentInChildren<TMP_Text>(true));
        return controller;
    }

    private static void CreateInsetFrame(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject frame = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        StyleSurface(frame, color);
        frame.transform.SetAsFirstSibling();
    }

    private static void CreatePanelAccent(Transform parent, string name, string title, string subtitle, Color glowColor, Color chipColor)
    {
        GameObject accent = CreateUiObject(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(134f, -138f), new Vector2(220f, 92f));
        StyleSurface(accent, new Color(0.09f, 0.12f, 0.17f, 0.88f));
        CreateInsetFrame(accent.transform, name + "Frame", new Vector2(196f, 68f), glowColor);
        GameObject chip = CreateUiObject(name + "Chip", accent.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(132f, 24f));
        StyleSurface(chip, chipColor);
        TMP_Text chipText = CreateLabel(name + "ChipText", chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 20f), title);
        StyleText(chipText, 13f, FontStyles.Bold, new Color(0.07f, 0.09f, 0.12f, 0.96f), TextAlignmentOptions.Center);
        TMP_Text bodyText = CreateLabel(name + "Body", accent.transform, new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(180f, 42f), subtitle);
        StyleText(bodyText, 13f, FontStyles.Normal, new Color(0.85f, 0.90f, 0.96f, 0.92f), TextAlignmentOptions.Center);
    }

    private static void CreateTowerTotem(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Color color, bool faceRight)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, new Vector2(92f, 420f));
        root.transform.SetAsFirstSibling();
        StyleSurface(root, new Color(0f, 0f, 0f, 0f));

        GameObject baseBlock = CreateUiObject(name + "Base", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(86f, 18f));
        StyleSurface(baseBlock, color);
        GameObject lowerCore = CreateUiObject(name + "LowerCore", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(44f, 118f));
        StyleSurface(lowerCore, color);
        GameObject midCore = CreateUiObject(name + "MidCore", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(54f, 122f));
        StyleSurface(midCore, color);
        GameObject upperCore = CreateUiObject(name + "UpperCore", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(38f, 118f));
        StyleSurface(upperCore, color);
        GameObject crown = CreateUiObject(name + "Crown", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(76f, 18f));
        StyleSurface(crown, color);

        float wingOffset = faceRight ? 24f : -24f;
        float wingPivot = faceRight ? 1f : 0f;
        GameObject lowerWing = CreateUiObject(name + "LowerWing", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(wingOffset, 48f), new Vector2(38f, 10f));
        RectTransform lowerWingRect = lowerWing.GetComponent<RectTransform>();
        lowerWingRect.pivot = new Vector2(wingPivot, 0.5f);
        lowerWingRect.localRotation = Quaternion.Euler(0f, 0f, faceRight ? -28f : 28f);
        StyleSurface(lowerWing, color);
        GameObject upperWing = CreateUiObject(name + "UpperWing", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(wingOffset, -118f), new Vector2(42f, 10f));
        RectTransform upperWingRect = upperWing.GetComponent<RectTransform>();
        upperWingRect.pivot = new Vector2(wingPivot, 0.5f);
        upperWingRect.localRotation = Quaternion.Euler(0f, 0f, faceRight ? 24f : -24f);
        StyleSurface(upperWing, color);
    }

    private static void CreateSceneRibbon(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string text, Color backgroundColor, Color textColor)
    {
        GameObject ribbon = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        StyleSurface(ribbon, backgroundColor);
        GameObject line = CreateUiObject(name + "Line", ribbon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 60f, 2f));
        StyleSurface(line, new Color(textColor.r, textColor.g, textColor.b, 0.25f));
        TMP_Text ribbonText = CreateLabel(name + "Text", ribbon.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 40f, 20f), text);
        StyleText(ribbonText, 13f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
    }

    private static void CreateFlowStep(Transform parent, string name, Vector2 anchor, string number, string title, string body)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, Vector2.zero, new Vector2(190f, 92f));
        StyleSurface(root, new Color(0.12f, 0.16f, 0.23f, 0.90f));
        CreateInsetFrame(root.transform, name + "Frame", new Vector2(154f, 56f), new Color(0.97f, 0.84f, 0.56f, 0.08f));

        GameObject badge = CreateUiObject(name + "Badge", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(32f, 32f));
        StyleSurface(badge, new Color(0.97f, 0.84f, 0.56f, 0.94f));
        TMP_Text badgeText = CreateLabel(name + "BadgeText", badge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), number);
        StyleText(badgeText, 18f, FontStyles.Bold, new Color(0.08f, 0.10f, 0.14f, 0.96f), TextAlignmentOptions.Center);

        TMP_Text titleText = CreateLabel(name + "Title", root.transform, new Vector2(0.5f, 0.60f), Vector2.zero, new Vector2(150f, 24f), title);
        StyleText(titleText, 18f, FontStyles.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
        TMP_Text bodyText = CreateLabel(name + "Body", root.transform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(166f, 32f), body);
        StyleText(bodyText, 12f, FontStyles.Normal, new Color(0.78f, 0.85f, 0.93f, 0.92f), TextAlignmentOptions.Center);
    }

    private static void CreateFlowConnector(Transform parent, string name, Vector2 anchor)
    {
        GameObject line = CreateUiObject(name, parent, anchor, anchor, Vector2.zero, new Vector2(74f, 4f));
        StyleSurface(line, new Color(0.97f, 0.84f, 0.56f, 0.42f));
        GameObject tip = CreateUiObject(name + "Tip", parent, anchor, anchor, new Vector2(34f, 0f), new Vector2(12f, 12f));
        RectTransform tipRect = tip.GetComponent<RectTransform>();
        tipRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        StyleSurface(tip, new Color(0.97f, 0.84f, 0.56f, 0.42f));
    }

    private static void CreateGearRowLabel(Transform parent, string name, Vector2 anchoredPosition, string text)
    {
        GameObject root = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(220f, 22f));
        StyleSurface(root, new Color(0.11f, 0.15f, 0.20f, 0.88f));
        GameObject line = CreateUiObject(name + "Line", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 2f));
        StyleSurface(line, new Color(0.96f, 0.84f, 0.56f, 0.18f));
        TMP_Text label = CreateLabel(name + "Text", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 18f), text);
        StyleText(label, 12f, FontStyles.Bold, new Color(0.93f, 0.95f, 1f, 0.92f), TextAlignmentOptions.Center);
    }

    private static void CreateSkillTag(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, string text)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, new Vector2(136f, 22f));
        StyleSurface(root, new Color(0.12f, 0.16f, 0.22f, 0.90f));
        TMP_Text label = CreateLabel(name + "Text", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 18f), text);
        StyleText(label, 12f, FontStyles.Bold, new Color(0.96f, 0.85f, 0.58f, 0.94f), TextAlignmentOptions.Center);
    }

    private static void CreateLaunchChevron(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Color color)
    {
        GameObject chevronA = CreateUiObject(name + "A", parent, anchor, anchor, anchoredPosition + new Vector2(-8f, 10f), new Vector2(26f, 6f));
        RectTransform chevronARect = chevronA.GetComponent<RectTransform>();
        chevronARect.localRotation = Quaternion.Euler(0f, 0f, 40f);
        StyleSurface(chevronA, color);

        GameObject chevronB = CreateUiObject(name + "B", parent, anchor, anchor, anchoredPosition + new Vector2(-8f, -10f), new Vector2(26f, 6f));
        RectTransform chevronBRect = chevronB.GetComponent<RectTransform>();
        chevronBRect.localRotation = Quaternion.Euler(0f, 0f, -40f);
        StyleSurface(chevronB, color);
    }

    private static void CreateLaneMarker(Transform parent, string name, Vector2 anchor, Color color)
    {
        GameObject marker = CreateUiObject(name, parent, anchor, anchor, Vector2.zero, new Vector2(38f, 38f));
        StyleSurface(marker, color);
        GameObject inner = CreateUiObject(name + "Inner", marker.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        StyleSurface(inner, new Color(0.06f, 0.08f, 0.12f, 0.72f));
    }

    private static void CreateCornerSigil(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Color color)
    {
        GameObject sigil = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, new Vector2(18f, 18f));
        StyleSurface(sigil, color);
        GameObject core = CreateUiObject(name + "Core", sigil.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        StyleSurface(core, new Color(0.08f, 0.10f, 0.14f, 0.55f));
    }

    private static void CreatePrimaryButtonAura(Transform parent, string name, Vector2 size, Color color, string tag)
    {
        GameObject aura = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        StyleSurface(aura, color);
        aura.transform.SetAsFirstSibling();

        GameObject tagRoot = CreateUiObject(name + "Tag", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(104f, 18f));
        StyleSurface(tagRoot, new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a + 0.10f)));
        TMP_Text tagText = CreateLabel(name + "TagText", tagRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 14f), tag);
        StyleText(tagText, 11f, FontStyles.Bold, new Color(0.08f, 0.10f, 0.14f, 0.95f), TextAlignmentOptions.Center);
    }

    private static void CreateTitleSigil(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Color color)
    {
        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, new Vector2(120f, 22f));
        StyleSurface(root, new Color(0f, 0f, 0f, 0f));

        GameObject leftLine = CreateUiObject(name + "LeftLine", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(28f, 2f));
        StyleSurface(leftLine, color);
        GameObject rightLine = CreateUiObject(name + "RightLine", root.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(28f, 2f));
        StyleSurface(rightLine, color);

        GameObject diamond = CreateUiObject(name + "Diamond", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        RectTransform diamondRect = diamond.GetComponent<RectTransform>();
        diamondRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        StyleSurface(diamond, color);

        GameObject core = CreateUiObject(name + "Core", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        StyleSurface(core, new Color(0.08f, 0.10f, 0.14f, 0.55f));
    }

    private static void CreateHorizontalAccent(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject accent = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
        StyleSurface(accent, color);
    }

    private static void CreateStatChip(Transform parent, string name, Vector2 anchoredPosition, string label)
    {
        GameObject chip = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(104f, 24f));
        StyleSurface(chip, new Color(0.17f, 0.22f, 0.28f, 0.92f));
        TMP_Text chipText = CreateLabel(name + "Text", chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 18f), label);
        StyleText(chipText, 12f, FontStyles.Bold, new Color(0.95f, 0.89f, 0.68f, 0.96f), TextAlignmentOptions.Center);
    }

    private static void CreateCombatPortrait(Transform parent, string name, bool isPlayer)
    {
        GameObject portraitRoot = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(180f, 170f));
        portraitRoot.transform.SetAsFirstSibling();
        GameObject pixelStage = CreateUiObject(name + "PixelStage", portraitRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(128f, 128f));
        StyleSurface(pixelStage, new Color(0.06f, 0.08f, 0.11f, 0.14f));
        CreateInsetFrame(pixelStage.transform, name + "PixelStageFrame", new Vector2(112f, 112f), new Color(0.96f, 0.84f, 0.56f, 0.10f));

        string spritePath = isPlayer ? WitchSpritePath : EnemySpritePath;
        if (CreateSpriteImage(pixelStage.transform, name + "SpriteImage", spritePath, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(144f, 144f), true))
        {
            CreateSpriteTileStrip(portraitRoot.transform, name + "Ground", isPlayer ? GrassTilePath : DirtTilePath, new Vector2(0.5f, 0f), new Vector2(0f, 18f), 3, 1, 28f);
            GameObject baseShadowWithSprite = CreateUiObject(name + "BaseShadow", portraitRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(92f, 12f));
            StyleSurface(baseShadowWithSprite, new Color(0.04f, 0.06f, 0.09f, 0.38f));
            return;
        }

        string[] pixelMap = isPlayer
            ? new[]
            {
                "..111111....",
                ".12222221...",
                ".123332221..",
                "..23333322..",
                "..23333322..",
                "..2444442...",
                ".255445552..",
                ".255445552..",
                "..5444455...",
                "..5666665...",
                ".56.66.65...",
                ".77.77.77..."
            }
            : new[]
            {
                "...8888.....",
                "..899998....",
                ".89999998...",
                ".89988998...",
                "..9888898...",
                ".999669999..",
                ".996666699..",
                "..9666669...",
                "..9666669...",
                ".996..699...",
                ".88....88...",
                ".8......8..."
            };

        Color[] palette = isPlayer
            ? new[]
            {
                new Color(0f, 0f, 0f, 0f),
                new Color(0.80f, 0.91f, 0.98f, 0.98f),
                new Color(0.60f, 0.74f, 0.92f, 0.98f),
                new Color(0.94f, 0.79f, 0.76f, 0.98f),
                new Color(0.28f, 0.20f, 0.46f, 0.98f),
                new Color(0.16f, 0.58f, 0.74f, 0.98f),
                new Color(0.38f, 0.28f, 0.20f, 0.98f),
                new Color(0.74f, 0.88f, 0.96f, 0.98f)
            }
            : new[]
            {
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0.72f, 0.18f, 0.22f, 0.98f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0.95f, 0.74f, 0.74f, 0.98f),
                new Color(0.58f, 0.10f, 0.14f, 0.98f)
            };

        CreatePixelSprite(pixelStage.transform, name + "Sprite", pixelMap, palette, 8f);

        GameObject baseShadow = CreateUiObject(name + "BaseShadow", portraitRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(92f, 12f));
        StyleSurface(baseShadow, new Color(0.04f, 0.06f, 0.09f, 0.38f));
    }

    private static bool CreateSpriteImage(Transform parent, string name, string assetPath, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool preserveAspect)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            return false;
        }

        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
        Image image = root.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        return true;
    }

    private static void CreateSpriteTileStrip(Transform parent, string name, string assetPath, Vector2 anchor, Vector2 anchoredPosition, int columns, int rows, float tileSize)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            return;
        }

        GameObject root = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, new Vector2(columns * tileSize, rows * tileSize));
        root.transform.SetAsFirstSibling();

        float startX = -((columns - 1) * tileSize * 0.5f);
        float startY = ((rows - 1) * tileSize * 0.5f);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject tile = CreateUiObject(name + "_Tile_" + x + "_" + y, root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + (x * tileSize), startY - (y * tileSize)), new Vector2(tileSize, tileSize));
                Image image = tile.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = Color.white;
            }
        }
    }

    private static void CreatePixelSprite(Transform parent, string name, string[] rows, Color[] palette, float pixelSize)
    {
        int height = rows.Length;
        int width = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            width = Mathf.Max(width, rows[i].Length);
        }

        GameObject spriteRoot = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width * pixelSize, height * pixelSize));
        spriteRoot.transform.SetAsFirstSibling();

        float originX = -((width - 1) * pixelSize * 0.5f);
        float originY = ((height - 1) * pixelSize * 0.5f);

        for (int y = 0; y < height; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                char token = row[x];
                if (token == '.')
                {
                    continue;
                }

                int paletteIndex = token - '0';
                if (paletteIndex < 0 || paletteIndex >= palette.Length)
                {
                    continue;
                }

                Color color = palette[paletteIndex];
                if (color.a <= 0f)
                {
                    continue;
                }

                GameObject pixel = CreateUiObject(name + "_P_" + x + "_" + y, spriteRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(originX + (x * pixelSize), originY - (y * pixelSize)), new Vector2(pixelSize, pixelSize));
                StyleSurface(pixel, color);
            }
        }
    }

    private static BattleFeedbackController CreateFeedbackController(Transform parent)
    {
        GameObject root = new GameObject("BattleFeedback");
        root.transform.SetParent(parent, false);
        BattleFeedbackController controller = root.AddComponent<BattleFeedbackController>();

        TMP_Text playerDamage = CreateLabel("PlayerDamageText", parent, new Vector2(0.25f, 0.5f), new Vector2(0f, 120f), new Vector2(200f, 50f), "");
        TMP_Text enemyDamage = CreateLabel("EnemyDamageText", parent, new Vector2(0.75f, 0.5f), new Vector2(0f, 120f), new Vector2(200f, 50f), "");
        playerDamage.gameObject.SetActive(false);
        enemyDamage.gameObject.SetActive(false);

        CanvasGroup playerFlash = CreateFlashGroup(parent, "PlayerFlash", new Vector2(0.25f, 0.5f));
        CanvasGroup enemyFlash = CreateFlashGroup(parent, "EnemyFlash", new Vector2(0.75f, 0.5f));

        SetObjectField(controller, "playerDamageText", playerDamage);
        SetObjectField(controller, "enemyDamageText", enemyDamage);
        SetObjectField(controller, "playerFlashGroup", playerFlash);
        SetObjectField(controller, "enemyFlashGroup", enemyFlash);
        return controller;
    }

    private static CanvasGroup CreateFlashGroup(Transform parent, string name, Vector2 anchor)
    {
        GameObject flash = CreateUiObject(name, parent, anchor, anchor, Vector2.zero, new Vector2(260f, 260f));
        Image image = flash.AddComponent<Image>();
        image.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        CanvasGroup group = flash.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        return group;
    }

    private static void EnsureUiPresentationCameraInOpenScene()
    {
        GameObject cameraObject = GameObject.Find(UiPresentationCameraName);
        if (cameraObject == null)
        {
            cameraObject = new GameObject(UiPresentationCameraName);
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
        {
            camera = cameraObject.AddComponent<Camera>();
        }

        cameraObject.SetActive(true);
        camera.enabled = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.012f, 0.018f, 0.028f, 1f);
        camera.cullingMask = 0;
        camera.orthographic = true;
        camera.depth = -1000f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
    }

    private static void SetObjectField(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
