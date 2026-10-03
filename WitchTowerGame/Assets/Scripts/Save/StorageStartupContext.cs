using System;
using System.IO;

namespace WitchTower.Save
{
    // One immutable endpoint/root decision per application session. Legacy and
    // Development gameplay never acquire release purchase authority.
    internal sealed class StorageStartupContext
    {
        internal const string BindingMessage = "購入環境の設定を確認中です。既存のデータは読み書きしていません。アプリを削除せず、サポートへお問い合わせください。";
        internal const string InvalidMessage = "保存先の設定を確認できません。既存のデータは保持しています。アプリを削除せず、サポートへお問い合わせください。";
        internal const string ChangedMessage = "起動後に保存先の設定が変更されました。データを保護するため停止しています。";

        internal string RootDirectory { get; }
        internal string ConfigurationJson { get; }
        internal bool Development { get; }
        internal bool AccessAvailable { get; }
        internal bool HasPurchaseRealmAuthority => AccessAvailable && PurchaseEnvironment != null;
        internal string PurchaseEnvironment { get; }
        internal bool AwaitingPurchaseEnvironment { get; }
        internal string Message { get; }

        private StorageStartupContext(string root, string configuration, bool development, bool available, string message, string environment = null, bool awaiting = false)
        {
            RootDirectory = root;
            ConfigurationJson = configuration;
            Development = development;
            AccessAvailable = available;
            Message = message;
            PurchaseEnvironment = environment;
            AwaitingPurchaseEnvironment = awaiting;
        }

        internal static StorageStartupContext PreserveLegacyGameplay(string resolvedRoot, string configuration, bool development)
        {
            // This is lexical validation only, not filesystem/provenance proof.
            // No file, identity, journal or account pointer is inspected here.
            if (string.IsNullOrWhiteSpace(resolvedRoot) || !Path.IsPathRooted(resolvedRoot))
                throw new ArgumentException("An absolute legacy root is required.");
            string root = Path.GetFullPath(resolvedRoot);
            return new StorageStartupContext(root, configuration, development, true, string.Empty);
        }

        internal static StorageStartupContext AwaitReleaseEnvironment(string configuration)
            => new StorageStartupContext(null, configuration, false, false,
                "App Storeの環境を確認しています。通信を確認してください。\n既存のデータを保持したまま、自動で再試行します。", awaiting: true);

        internal static StorageStartupContext BindReleaseEnvironment(string persistentRoot, string configuration,
            WitchTower.Monetization.VerifiedApplePurchaseEnvironment environment)
        {
            var config = UnityEngine.JsonUtility.FromJson<AppleAccountConfiguration>(configuration);
            if (config?.IsProduction != true) throw new InvalidOperationException(InvalidMessage);
            string name = environment == WitchTower.Monetization.VerifiedApplePurchaseEnvironment.Production ? "Production"
                : environment == WitchTower.Monetization.VerifiedApplePurchaseEnvironment.Sandbox ? "Sandbox" : null;
            if (name == null) throw new InvalidOperationException(BindingMessage);
            string root = ReleasePurchaseStorage.Resolve(persistentRoot, name);
            string endpoint = name == "Production" ? AppleAccountConfiguration.ProductionEndpoint
                : AppleAccountConfiguration.ReviewSandboxEndpoint;
            // Use fresh configuration, never carry development keys across realms.
            string raw = UnityEngine.JsonUtility.ToJson(new AppleAccountConfiguration {
                BaseUrl = endpoint, ReleaseAppleLinking = true });
            return new StorageStartupContext(root, raw, false, true, string.Empty, name);
        }

        internal static StorageStartupContext BindingRequired(string configuration, bool development)
            => new StorageStartupContext(null, configuration, development, false, BindingMessage);

        internal static StorageStartupContext InvalidConfiguration(bool development)
            => new StorageStartupContext(null, null, development, false, InvalidMessage);
    }
}
