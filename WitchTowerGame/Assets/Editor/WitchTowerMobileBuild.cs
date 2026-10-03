using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public static class WitchTowerMobileBuild
{
    private const string IosMenuPath = "WitchTower/Release/Build iOS Xcode Project";
    private const string IosSimulatorMenuPath = "WitchTower/Release/Build iOS Simulator Xcode Project";
    private const string AndroidMenuPath = "WitchTower/Release/Build Android App Bundle";
    private const string DefaultIosOutput = "Builds/iOS";
    private const string DefaultIosSimulatorOutput = "Builds/iOS-Simulator";
    private const string DefaultAndroidOutput = "Builds/Android/WitchTowerGame.aab";

    [MenuItem(IosMenuPath)]
    public static void BuildIosXcodeProjectFromMenu()
    {
        BuildIosXcodeProject(ResolveProjectPath(DefaultIosOutput), iOSSdkVersion.DeviceSDK);
    }

    [MenuItem(IosSimulatorMenuPath)]
    public static void BuildIosSimulatorXcodeProjectFromMenu()
    {
        BuildIosXcodeProject(
            ResolveProjectPath(DefaultIosSimulatorOutput),
            iOSSdkVersion.SimulatorSDK);
    }

    [MenuItem(AndroidMenuPath)]
    public static void BuildAndroidAppBundleFromMenu()
    {
        BuildAndroidAppBundle(ResolveProjectPath(DefaultAndroidOutput));
    }

    public static void BuildAndroidAppBundleFromCommandLine()
    {
        string output = ReadCommandLineValue("-buildOutput");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = ResolveProjectPath(DefaultAndroidOutput);
        }
        else if (!Path.IsPathRooted(output))
        {
            output = ResolveProjectPath(output);
        }

        BuildAndroidAppBundle(output);
    }

    public static void BuildIosXcodeProjectFromCommandLine()
    {
        string output = ReadCommandLineValue("-buildOutput");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = ResolveProjectPath(DefaultIosOutput);
        }
        else if (!Path.IsPathRooted(output))
        {
            output = ResolveProjectPath(output);
        }

        BuildIosXcodeProject(output, iOSSdkVersion.DeviceSDK);
    }

    public static void BuildIosSimulatorXcodeProjectFromCommandLine()
    {
        string output = ReadCommandLineValue("-buildOutput");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = ResolveProjectPath(DefaultIosSimulatorOutput);
        }
        else if (!Path.IsPathRooted(output))
        {
            output = ResolveProjectPath(output);
        }

        BuildIosXcodeProject(output, iOSSdkVersion.SimulatorSDK);
    }

#if UNITY_IOS
    [PostProcessBuild(1000)]
    public static void ConfigureIosInfoPlist(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        File.WriteAllText(plistPath, plist.WriteToString());

        // Preserve CocoaPods' framework locations when Unity updates an
        // existing export containing the Google Mobile Ads plugin.
        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        project.AddBuildProperty(project.GetUnityFrameworkTargetGuid(), "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
        project.WriteToFile(projectPath);
    }
#endif

    private static void BuildIosXcodeProject(string outputPath, iOSSdkVersion sdkVersion)
    {
        Directory.CreateDirectory(outputPath);
        bool updatingExistingProject = File.Exists(
            Path.Combine(outputPath, "Unity-iPhone.xcodeproj", "project.pbxproj"));

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            options = updatingExistingProject
                ? BuildOptions.AcceptExternalModificationsToPlayer
                : BuildOptions.None
        };

        iOSSdkVersion previousSdkVersion = PlayerSettings.iOS.sdkVersion;
        // ExecuteAlways UI changes can otherwise reuse an older serialized
        // scene from the incremental player cache. Release verification can
        // explicitly request a clean build without deleting project assets.
        if (Environment.GetCommandLineArgs().Contains("-cleanBuildCache"))
        {
            options.options |= BuildOptions.CleanBuildCache;
        }

        // Explicit opt-in for device QA. AdMobConfiguration uses Google's
        // test ad unit when Debug.isDebugBuild is true. Release stays default.
        if (Environment.GetCommandLineArgs().Contains("-developmentBuild"))
        {
            options.options |= BuildOptions.Development;
        }

        AppleMobileArchitectureSimulator previousSimulatorArchitecture =
            PlayerSettings.iOS.simulatorSdkArchitecture;
        try
        {
            PlayerSettings.iOS.sdkVersion = sdkVersion;
            if (sdkVersion == iOSSdkVersion.SimulatorSDK)
            {
                PlayerSettings.iOS.simulatorSdkArchitecture =
                    AppleMobileArchitectureSimulator.ARM64;
            }

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"iOS Xcode project build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}.");
            }

            Debug.Log(
                $"[MobileBuild] iOS Xcode project succeeded: {outputPath} " +
                $"({report.summary.totalSize:N0} bytes, SDK={sdkVersion}).");
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = previousSdkVersion;
            PlayerSettings.iOS.simulatorSdkArchitecture = previousSimulatorArchitecture;
        }
    }

    private static void BuildAndroidAppBundle(string outputPath)
    {
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        EditorUserBuildSettings.buildAppBundle = true;
        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Android App Bundle build failed: {report.summary.result}, " +
                $"errors={report.summary.totalErrors}.");
        }

        Debug.Log(
            $"[MobileBuild] Android App Bundle succeeded: {outputPath} " +
            $"({report.summary.totalSize:N0} bytes).");
    }

    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene != null && scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    private static string ResolveProjectPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }

    private static string ReadCommandLineValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], name, StringComparison.Ordinal))
            {
                return arguments[i + 1];
            }
        }

        return string.Empty;
    }
}
