using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WitchTower.Save
{
    // A separate local account namespace. Never copy/activate the normal account here.
    public static class RefundSandboxConfiguration
    {
        public const string Profile = "refund-20260928";
        public const string Endpoint = "https://api.nasus-games.com/sandbox-refund-20260928/device";
        [Serializable] private sealed class Config
        {
            public string BaseUrl;
            public string RefundSandboxProfile;
            public string QaAccessKey;
            public bool ExperimentalAppleLinking;
            public double ExpiresUnix;
        }
        private static string Raw => WitchTower.Managers.SaveManager.Instance != null
            ? WitchTower.Managers.SaveManager.Instance.PlayerDataConfigurationJson
            : Resources.Load<TextAsset>("PlayerDataService")?.text;
        private static Config Parse(string raw) => string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<Config>(raw);
        private static bool Requested(Config c) => c != null && (!string.IsNullOrEmpty(c.RefundSandboxProfile) ||
            (c.BaseUrl?.IndexOf("sandbox-refund", StringComparison.OrdinalIgnoreCase) >= 0));
        public static bool IsConfigured => Requested(Parse(Raw));
        internal static bool IsConfiguredFrom(string raw) => Requested(Parse(raw));
        public static bool IsActive
        {
            get
            {
                try { return Validate(Raw, Debug.isDebugBuild || Application.isEditor,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true); }
                catch { return false; }
            }
        }
        public static bool Validate(string raw, bool development, double now, bool requireUnexpired)
        {
            var c = Parse(raw);
            if (!Requested(c)) return false;
            if (!development || c.RefundSandboxProfile != Profile || c.BaseUrl != Endpoint ||
                !c.ExperimentalAppleLinking || c.QaAccessKey == null ||
                !Regex.IsMatch(c.QaAccessKey, "\\A[a-f0-9]{64}\\z") ||
                double.IsNaN(c.ExpiresUnix) || double.IsInfinity(c.ExpiresUnix) || c.ExpiresUnix <= 0 ||
                (requireUnexpired && (c.ExpiresUnix <= now || c.ExpiresUnix > now + 86400)))
                throw new InvalidOperationException("返金テスト設定が無効または期限切れです。通常セーブは使用しません。");
            return true;
        }
        public static string ResolveRoot(string root, string raw, bool development)
        {
            // Expiry disables networking, not storage isolation. Never fall back to normal saves.
            return Validate(raw, development, 0, false) ? Path.Combine(root, "refund-sandbox", Profile) : root;
        }
        public static string ResolveRoot(string root) => ResolveRoot(root, Raw, Debug.isDebugBuild || Application.isEditor);
    }
}
