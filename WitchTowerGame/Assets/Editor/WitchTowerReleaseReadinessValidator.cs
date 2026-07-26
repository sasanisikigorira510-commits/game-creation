using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WitchTowerReleaseReadinessValidator
{
    private static readonly string[] RequiredScenes =
    {
        "Assets/Scenes/BootScene.unity",
        "Assets/Scenes/HomeScene.unity",
        "Assets/Scenes/FormationScene.unity",
        "Assets/Scenes/EquipmentScene.unity",
        "Assets/Scenes/BattleScene.unity",
        "Assets/Scenes/FusionScene.unity",
        "Assets/Scenes/GachaScene.unity"
    };

    private static readonly string[] UnsafeReleaseDefines =
    {
        "WITCHTOWER_PREVIEW_ROSTER",
        "WITCHTOWER_IAP_ENABLED",
        "WITCHTOWER_ADS_ENABLED"
    };

    [MenuItem("WitchTower/Validate Release Readiness")]
    public static void ValidateFromMenu()
    {
        ValidationResult result = ValidateProject(includeBothMobileTargets: true, BuildTargetGroup.Unknown);
        LogResult(result);
        if (result.Blockers.Count > 0)
        {
            throw new BuildFailedException($"Release readiness failed with {result.Blockers.Count} blocker(s).");
        }
    }

    public static ValidationResult ValidateProject(bool includeBothMobileTargets, BuildTargetGroup buildTargetGroup)
    {
        var result = new ValidationResult();
        ValidateScenes(result);
        ValidateProductIdentity(result);

        if (includeBothMobileTargets)
        {
            ValidateMobileTarget(BuildTargetGroup.Android, result);
            ValidateMobileTarget(BuildTargetGroup.iOS, result);
        }
        else if (buildTargetGroup == BuildTargetGroup.Android || buildTargetGroup == BuildTargetGroup.iOS)
        {
            ValidateMobileTarget(buildTargetGroup, result);
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIcon.png") != null)
        {
            result.Warnings.Add("Assets/Branding/AppIcon.png is still documented as a placeholder; replace it with final branding.");
        }

        if (!PlayerSettings.Android.useCustomKeystore)
        {
            result.Warnings.Add("Android custom release keystore is not configured in this project.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.appleDeveloperTeamID))
        {
            result.Warnings.Add("Apple Developer Team ID is not configured.");
        }

        return result;
    }

    private static void ValidateScenes(ValidationResult result)
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene != null && scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (!enabledScenes.SequenceEqual(RequiredScenes))
        {
            result.Blockers.Add(
                "Build Settings scenes must be enabled in this exact order: " +
                string.Join(", ", RequiredScenes));
        }

        foreach (string scenePath in RequiredScenes)
        {
            if (!File.Exists(scenePath))
            {
                result.Blockers.Add("Required scene is missing: " + scenePath);
            }
        }
    }

    private static void ValidateProductIdentity(ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
        {
            result.Blockers.Add("Product name is empty.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName))
        {
            result.Blockers.Add("Company name is empty.");
        }
        else if (string.Equals(PlayerSettings.companyName, "andou", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("Company name is still 'andou'; confirm the final publisher name before submission.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
        {
            result.Blockers.Add("Bundle version is empty.");
        }
    }

    private static void ValidateMobileTarget(BuildTargetGroup group, ValidationResult result)
    {
        string identifier = PlayerSettings.GetApplicationIdentifier(group);
        if (string.IsNullOrWhiteSpace(identifier) || identifier.StartsWith("com.DefaultCompany", StringComparison.OrdinalIgnoreCase))
        {
            result.Blockers.Add($"{group} application identifier is missing or still uses the Unity default.");
        }

        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var defineSet = new HashSet<string>(
            (defines ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
        foreach (string unsafeDefine in UnsafeReleaseDefines)
        {
            if (defineSet.Contains(unsafeDefine))
            {
                result.Blockers.Add(
                    $"{group} contains unsafe release define '{unsafeDefine}'. " +
                    "The current project has no verified store/ad provider and preview roster must remain disabled.");
            }
        }
    }

    private static void LogResult(ValidationResult result)
    {
        foreach (string warning in result.Warnings)
        {
            Debug.LogWarning("[ReleaseReadiness] " + warning);
        }

        foreach (string blocker in result.Blockers)
        {
            Debug.LogError("[ReleaseReadiness] " + blocker);
        }

        if (result.Blockers.Count == 0)
        {
            Debug.Log($"[ReleaseReadiness] PASS blockers=0 warnings={result.Warnings.Count}");
        }
    }

    public sealed class ValidationResult
    {
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }
}

public sealed class WitchTowerReleaseBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
        WitchTowerReleaseReadinessValidator.ValidationResult result =
            WitchTowerReleaseReadinessValidator.ValidateProject(includeBothMobileTargets: false, group);
        if (result.Blockers.Count > 0)
        {
            throw new BuildFailedException(string.Join("\n", result.Blockers));
        }
    }
}
