using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Reads private configuration only during an explicit device-QA export.
// Restores the exact original resource even when the build fails.
public static class AppleQaDeviceBuild
{
    [Serializable] private sealed class Config { public string BaseUrl; public bool ExperimentalAppleLinking; public string QaAccessKey; }
    public static void Build()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-qaConfig");
        if (index < 0 || index + 1 >= args.Length || Array.IndexOf(args, "-developmentBuild") < 0)
            throw new BuildFailedException("Explicit private QA config and development build are required.");
        string source = Path.GetFullPath(args[index + 1]);
        string project = Directory.GetParent(Application.dataPath).FullName;
        if (source.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new BuildFailedException("QA config must be outside the Unity project.");
        string raw = File.ReadAllText(source);
        var config = JsonUtility.FromJson<Config>(raw);
        bool refund = WitchTower.Save.RefundSandboxConfiguration.Validate(raw, true,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true);
        if (refund != (Array.IndexOf(args, "-refundSandboxBuild") >= 0))
            throw new BuildFailedException("Refund sandbox requires explicit build opt-in and isolated configuration.");
        if (config == null || (!refund && config.BaseUrl != "https://api.nasus-games.com/qa") ||
            !config.ExperimentalAppleLinking || string.IsNullOrEmpty(config.QaAccessKey))
            throw new BuildFailedException("Invalid QA connection configuration.");
        AppleAccountBuildGate.ValidateConfiguration(true, config.QaAccessKey, true);
        string resource = Path.Combine(Application.dataPath, "Resources/PlayerDataService.json");
        byte[] previous = File.ReadAllBytes(resource);
        try
        {
            File.WriteAllText(resource, raw);
            AssetDatabase.ImportAsset("Assets/Resources/PlayerDataService.json", ImportAssetOptions.ForceSynchronousImport);
            WitchTowerMobileBuild.BuildIosXcodeProjectFromCommandLine();
#if UNITY_IOS
            int outputIndex = Array.IndexOf(args, "-buildOutput");
            if (outputIndex < 0 || outputIndex + 1 >= args.Length)
                throw new BuildFailedException("Explicit QA build output is required.");
            string pbxPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(Path.GetFullPath(args[outputIndex + 1]));
            var pbx = new UnityEditor.iOS.Xcode.PBXProject();
            pbx.ReadFromFile(pbxPath);
            string main = pbx.GetUnityMainTargetGuid();
            pbx.SetBuildProperty(main, "CODE_SIGN_STYLE", "Manual");
            pbx.SetBuildProperty(main, "PROVISIONING_PROFILE_SPECIFIER", "Dungeon Monster Roguelike Apple QA 20260926");
            pbx.SetBuildProperty(main, "CODE_SIGN_IDENTITY", "Apple Development");
            pbx.WriteToFile(pbxPath);
#endif
        }
        finally
        {
            File.WriteAllBytes(resource, previous);
            AssetDatabase.ImportAsset("Assets/Resources/PlayerDataService.json", ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
