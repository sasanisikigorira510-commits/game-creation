using System;
using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Monetization
{
    public enum VerifiedApplePurchaseEnvironment { Unknown, Production, Sandbox, Unsupported }

    [Serializable]
    public sealed class PurchaseEnvironmentCapability
    {
        public int Version;
        public string Store;
        public string AppleEnvironment;
        public string DataRealm;
        public bool Ready;
    }

    // Checkout policy only. Never route a receipt, change an account, grant an
    // order, or suppress pending-order recovery based on this build-time hint.
    public static class IapPurchaseEnvironmentPolicy
    {
        public const string DevelopmentProductionMessage =
            "この開発版は本番データでプレイしています。テスト購入は開始できません。\n課金の検証には、データを分離したテスト専用版を使用してください。";
        public const string InvalidConfigurationMessage =
            "購入の接続設定を確認できないため、購入は開始しません。";
        public const string VerifiedEnvironmentUnavailableMessage =
            "App Storeの購入環境を確認できないため、購入は開始しません。通常のゲームは続けられます。";
        public const string PurchaseCapabilityUnavailableMessage =
            "購入サーバーの環境確認が完了していません。購入は開始しません。通常のゲームは続けられます。";
        public const string PurchaseEnvironmentMismatchMessage =
            "購入環境とゲームデータの保存先が一致しません。データを保護するため、購入は開始しません。";

        // Read-only checkout preflight, not transaction verification or routing.
        // The active realm comes only from the immutable release startup context;
        // a head or a native refresh can never change the selected save root.
        public static string VerifiedEnvironmentBlockedMessage(VerifiedApplePurchaseEnvironment environment,
            PurchaseEnvironmentCapability capability, string activeDataRealm)
        {
            if (environment != VerifiedApplePurchaseEnvironment.Production &&
                environment != VerifiedApplePurchaseEnvironment.Sandbox)
                return VerifiedEnvironmentUnavailableMessage;
            if (capability == null || capability.Version != 1 || capability.Store != "apple" || !capability.Ready)
                return PurchaseCapabilityUnavailableMessage;
            string expected = environment == VerifiedApplePurchaseEnvironment.Production ? "Production" : "Sandbox";
            return capability.AppleEnvironment == expected && capability.DataRealm == expected &&
                activeDataRealm == expected ? string.Empty : PurchaseEnvironmentMismatchMessage;
        }

        [Serializable] private sealed class Configuration { public string BaseUrl; }

        public static string NewPurchaseBlockedMessage(string raw, bool development)
        {
            // This is not a claim that every release transaction is Production:
            // TestFlight/review handling is a separate server/ledger contract.
            if (!development) return string.Empty;
            Configuration config;
            try { config = string.IsNullOrWhiteSpace(raw) ? null : JsonUtility.FromJson<Configuration>(raw); }
            catch (ArgumentException) { return InvalidConfigurationMessage; }
            string endpoint = config?.BaseUrl?.Trim().TrimEnd('/');
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
                return InvalidConfigurationMessage;

            var production = new Uri(SaveEnvironmentConfiguration.ProductionEndpoint);
            // Also reject equivalent spelling/default ports, not just one
            // literal URL. Dedicated QA paths retain their existing validation.
            bool productionRoot = string.Equals(uri.Scheme, production.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.DnsSafeHost, production.DnsSafeHost, StringComparison.OrdinalIgnoreCase) &&
                uri.Port == production.Port && string.IsNullOrEmpty(uri.AbsolutePath.TrimEnd('/'));
            return productionRoot ? DevelopmentProductionMessage : string.Empty;
        }
    }
}
