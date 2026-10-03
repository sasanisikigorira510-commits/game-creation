using System;
using System.IO;
using UnityEngine;

namespace WitchTower.Save
{
    public static class SaveEnvironmentConfiguration
    {
        public const string ProductionEndpoint = "https://api.nasus-games.com";
        [Serializable] private sealed class Configuration { public string BaseUrl; }

        public static string ResolveRoot(string root, string raw, bool development)
        {
            // Preserve refund isolation (including its fail-closed validation).
            string refundRoot = RefundSandboxConfiguration.ResolveRoot(root, raw, development);
            if (refundRoot != root) return refundRoot;
            var config = string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<Configuration>(raw);
            string endpoint = config?.BaseUrl?.Trim().TrimEnd('/');
            // The first public release must not reuse pre-release QA credentials,
            // account pointers, purchase journals, or currency. Never migrate them
            // implicitly, and keep this path stable across development/release builds.
            return string.Equals(endpoint, ProductionEndpoint, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(root, "environments", "production")
                : root;
        }

        public static string ResolveRoot(string root) => ResolveRoot(root,
            Resources.Load<TextAsset>("PlayerDataService")?.text,
            Debug.isDebugBuild || Application.isEditor);
    }
}
