using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// The feature cannot accidentally ship by copying a QA resource into a release.
public sealed class AppleAccountBuildGate : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 1100;
    private static bool Enabled(bool development)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/PlayerDataService.json");
        return asset != null && JsonUtility.FromJson<WitchTower.Save.AppleAccountConfiguration>(asset.text)
            ?.IsEnabled(development) == true;
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/PlayerDataService.json");
        WitchTower.Save.RefundSandboxConfiguration.Validate(asset?.text,
            (report.summary.options & BuildOptions.Development) != 0,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true);
        ValidateReleaseConfiguration(asset?.text, (report.summary.options & BuildOptions.Development) != 0);
        if (report.summary.platform == BuildTarget.iOS)
            ValidateReleaseMinimumOs(asset?.text, (report.summary.options & BuildOptions.Development) != 0,
                PlayerSettings.iOS.targetOSVersionString);
    }
    public static void ValidateReleaseMinimumOs(string raw, bool development, string minimumOs)
    {
        var config = string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<WitchTower.Save.AppleAccountConfiguration>(raw);
        if (!development && config?.IsProduction == true &&
            (!Version.TryParse(minimumOs, out var version) || version < new Version(16, 0)))
            throw new BuildFailedException("提出用の購入環境判定はiOS 16以降が必要です。iOS 15対応の方針決定まで公開ビルドを停止します。対応OSの承認後にMinimum iOS Versionを16.0以上へ変更してください。");
    }
    public static void ValidateConfiguration(bool appleEnabled, string key, bool development)
    {
        Validate(new WitchTower.Save.AppleAccountConfiguration {
            ExperimentalAppleLinking = appleEnabled, QaAccessKey = key }, development);
    }
    public static void ValidateReleaseConfiguration(string raw, bool development)
    {
        var config = string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<WitchTower.Save.AppleAccountConfiguration>(raw);
        if (config != null) Validate(config, development);
    }
    private static void Validate(WitchTower.Save.AppleAccountConfiguration config, bool development)
    {
        try { config.ValidateBuild(development); }
        catch (InvalidOperationException e) { throw new BuildFailedException(e.Message); }
    }
    public void OnPostprocessBuild(BuildReport report)
    {
#if UNITY_IOS
        if (report.summary.platform != BuildTarget.iOS) return;
        string build = report.summary.outputPath;
        string projectPath = PBXProject.GetPBXProjectPath(build);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        string framework = project.GetUnityFrameworkTargetGuid();
        project.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");
        project.AddFrameworkToProject(framework, "StoreKit.framework", false);
        project.AddFrameworkToProject(framework, "AppTrackingTransparency.framework", false);
        project.AddFrameworkToProject(framework, "AuthenticationServices.framework", false);
        string plugin = project.FindFileGuidByProjectPath("Libraries/Plugins/iOS/NasusAppleIdentity.mm");
        if (!string.IsNullOrEmpty(plugin))
            project.SetCompileFlagsForFile(framework, plugin, new System.Collections.Generic.List<string> { "-fobjc-arc" });
        if (Enabled((report.summary.options & BuildOptions.Development) != 0))
        {
            string main = project.GetUnityMainTargetGuid();
            string relative = project.GetBuildPropertyForAnyConfig(main, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrEmpty(relative)) relative = "NasusAccount.entitlements";
            string path = Path.GetFullPath(Path.Combine(build, relative));
            if (!path.StartsWith(Path.GetFullPath(build) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new BuildFailedException("Entitlements path is outside the exported project.");
            var plist = new PlistDocument();
            if (File.Exists(path)) plist.ReadFromFile(path);
            plist.root.CreateArray("com.apple.developer.applesignin").AddString("Default");
            File.WriteAllText(path, plist.WriteToString());
            project.SetBuildProperty(main, "CODE_SIGN_ENTITLEMENTS", relative);
        }
        project.WriteToFile(projectPath);
#endif
    }
}
