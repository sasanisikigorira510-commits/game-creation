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
        "WITCHTOWER_PREVIEW_ROSTER"
    };

    private const string PlaceholderIconPath = "Assets/Branding/AppIcon.png";
    private const string FinalIconPath = "Assets/Branding/AppIconFinal.png";
    private const string AdaptiveForegroundPath = "Assets/Branding/AppIconAdaptiveForeground.png";
    private const string AdaptiveBackgroundPath = "Assets/Branding/AppIconAdaptiveBackground.png";

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
        ValidateDisplaySettings(result);
        ValidateAppIcons(result);
        if (includeBothMobileTargets || buildTargetGroup == BuildTargetGroup.iOS || buildTargetGroup == BuildTargetGroup.Android)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/PlayerDataService.json");
            string error = ValidateOnlineConfiguration(configuration != null ? configuration.text : null);
            if (error != null) result.Blockers.Add(error);
        }

        if (includeBothMobileTargets)
        {
            ValidateMobileTarget(BuildTargetGroup.Android, result);
            ValidateMobileTarget(BuildTargetGroup.iOS, result);
        }
        else if (buildTargetGroup == BuildTargetGroup.Android || buildTargetGroup == BuildTargetGroup.iOS)
        {
            ValidateMobileTarget(buildTargetGroup, result);
        }

        if (!includeBothMobileTargets &&
            buildTargetGroup == BuildTargetGroup.Android &&
            !PlayerSettings.Android.useCustomKeystore)
        {
            result.Warnings.Add("Android custom release keystore is not configured in this project.");
        }

        if ((includeBothMobileTargets || buildTargetGroup == BuildTargetGroup.iOS) &&
            string.IsNullOrWhiteSpace(PlayerSettings.iOS.appleDeveloperTeamID))
        {
            result.Warnings.Add("Apple Developer Team ID is not configured.");
        }

        return result;
    }

    [Serializable]
    private sealed class OnlineConfiguration { public string BaseUrl; }

    // Device builds cannot use the Editor-only localhost fallback, even when
    // Development Build is selected. Fail before exporting an unusable player.
    public static string ValidateOnlineConfiguration(string json)
    {
        const string error = "オンライン機能の接続先が未設定または無効です。Assets/Resources/PlayerDataService.json の BaseUrl に稼働中サーバーのHTTPS URLを設定してから、実機ビルドしてください。";
        if (string.IsNullOrWhiteSpace(json)) return error;
        try
        {
            string url = JsonUtility.FromJson<OnlineConfiguration>(json)?.BaseUrl;
            if (string.IsNullOrWhiteSpace(url) || url != url.Trim() ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                uri.IsLoopback || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return error;
            return null;
        }
        catch (ArgumentException) { return error; }
    }

    private static void ValidateAppIcons(ValidationResult result)
    {
        Texture2D placeholder = AssetDatabase.LoadAssetAtPath<Texture2D>(PlaceholderIconPath);
        Texture2D finalIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalIconPath);
        Texture2D adaptiveForeground = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveForegroundPath);
        Texture2D adaptiveBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveBackgroundPath);
        if (finalIcon == null || adaptiveForeground == null || adaptiveBackground == null)
        {
            result.Blockers.Add(
                "Final app icon assets are incomplete. Run WitchTower > Release > Apply App Icons.");
            return;
        }

        ValidatePlatformIcons(
            NamedBuildTarget.Android,
            placeholder,
            finalIcon,
            adaptiveForeground,
            adaptiveBackground,
            result);
        ValidatePlatformIcons(
            NamedBuildTarget.iOS,
            placeholder,
            finalIcon,
            finalIcon,
            finalIcon,
            result);
    }

    private static void ValidatePlatformIcons(
        NamedBuildTarget target,
        Texture2D placeholder,
        Texture2D finalIcon,
        Texture2D adaptiveForeground,
        Texture2D adaptiveBackground,
        ValidationResult result)
    {
        foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(target))
        {
            foreach (PlatformIcon icon in PlayerSettings.GetPlatformIcons(target, kind))
            {
                Texture2D[] textures = icon.GetTextures();
                bool usesAdaptiveLayers = target == NamedBuildTarget.Android && icon.maxLayerCount >= 2;
                bool valid = textures != null &&
                             textures.Length > 0 &&
                             textures.All(texture => texture != null && texture != placeholder);

                if (usesAdaptiveLayers)
                {
                    valid &= textures.Length >= 2 &&
                             textures[0] == adaptiveBackground &&
                             textures[1] == adaptiveForeground;
                }
                else
                {
                    valid &= textures.All(texture => texture == finalIcon);
                }

                if (!valid)
                {
                    result.Blockers.Add(
                        $"{target.TargetName} {kind} app icon ({icon.width}x{icon.height}) " +
                        "is missing or does not use the final branding.");
                }
            }
        }
    }

    private static void ValidateDisplaySettings(ValidationResult result)
    {
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
        {
            result.Blockers.Add(
                "Default interface orientation must be upright Portrait. " +
                $"Current value: {PlayerSettings.defaultInterfaceOrientation}.");
        }
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
        NamedBuildTarget namedTarget = group == BuildTargetGroup.iOS
            ? NamedBuildTarget.iOS
            : NamedBuildTarget.Android;
        string identifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
        if (string.IsNullOrWhiteSpace(identifier) || identifier.StartsWith("com.DefaultCompany", StringComparison.OrdinalIgnoreCase))
        {
            result.Blockers.Add($"{group} application identifier is missing or still uses the Unity default.");
        }

        string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        var defineSet = new HashSet<string>(
            (defines ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
        foreach (string unsafeDefine in UnsafeReleaseDefines)
        {
            if (defineSet.Contains(unsafeDefine))
            {
                result.Blockers.Add(
                    $"{group} contains unsafe release define '{unsafeDefine}'. " +
                    "Preview roster must remain disabled in release builds.");
            }
        }

        ValidateMonetizationConfiguration(group, defineSet, result);
    }

    private static void ValidateMonetizationConfiguration(
        BuildTargetGroup group,
        HashSet<string> defineSet,
        ValidationResult result)
    {
        bool iapEnabled = defineSet.Contains("WITCHTOWER_IAP_ENABLED");
        bool adsEnabled = defineSet.Contains("WITCHTOWER_ADS_ENABLED");

        if (iapEnabled)
        {
            IReadOnlyList<WitchTower.Monetization.IapProductDefinition> products =
                WitchTower.Monetization.IapProductCatalog.Products;
            if (products.Count != 6 ||
                products.Any(product =>
                    product == null ||
                    string.IsNullOrWhiteSpace(product.ProductId) ||
                    product.PaidStoneAmount <= 0) ||
                products.Select(product => product.ProductId).Distinct(StringComparer.Ordinal).Count() != products.Count)
            {
                result.Blockers.Add($"{group} IAP catalog must contain six valid, unique paid-stone products.");
            }
        }

        if (!adsEnabled || group != BuildTargetGroup.iOS)
        {
            return;
        }

        if (!WitchTower.Monetization.AdMobConfiguration.HasProductionIosConfiguration)
        {
            result.Blockers.Add("iOS ads are enabled, but production AdMob app/banner IDs are not configured.");
            return;
        }

        const string settingsPath =
            "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        if (!File.Exists(settingsPath) ||
            !File.ReadAllText(settingsPath).Contains(
                WitchTower.Monetization.AdMobConfiguration.ProductionIosAppId,
                StringComparison.Ordinal))
        {
            result.Blockers.Add("Google Mobile Ads iOS settings do not match the production AdMob app ID.");
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
