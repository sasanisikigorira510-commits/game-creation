#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

// Compile-only diagnostic. Does not export/install an app, change build
// settings, read saves or contact the player-data/Apple services.
public static class WitchTowerPurchaseEnvironmentCompileAudit
{
    public static void CompileIosScripts()
    {
        string output = Environment.GetEnvironmentVariable("NASUS_PURCHASE_COMPILE_OUTPUT");
        string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tmp")) + Path.DirectorySeparatorChar;
        if (string.IsNullOrWhiteSpace(output) || !Path.GetFullPath(output).StartsWith(allowed, StringComparison.Ordinal))
            throw new InvalidOperationException("A task-owned output under the project tmp directory is required.");
        foreach (bool development in new[] { false, true })
        {
            string destination = Path.Combine(output, development ? "ios-development" : "ios-release");
            if (Directory.Exists(destination))
                throw new InvalidOperationException("Compile output must be new; existing artifacts are not overwritten.");
            Directory.CreateDirectory(destination);
            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.iOS,
                group = BuildTargetGroup.iOS,
                options = development ? ScriptCompilationOptions.DevelopmentBuild : ScriptCompilationOptions.None
            };
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, destination);
            // Unity's result collection contains logical assembly names, not
            // necessarily absolute artifact paths. Inspect this fresh output.
            if (result.assemblies == null || !result.assemblies.Any() ||
                !File.Exists(Path.Combine(destination, "Assembly-CSharp.dll")))
                throw new InvalidOperationException("iOS script compilation did not produce the game assembly.");
            Debug.Log("[PurchaseEnvironmentCompileAudit] iOS " + (development ? "Development" : "Release") + " script compilation succeeded.");
        }
    }
}
#endif
