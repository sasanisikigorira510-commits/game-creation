using System;
using System.Text.RegularExpressions;

namespace WitchTower.Save
{
    // Public opt-in is deliberately separate from development-only QA credentials.
    [Serializable]
    public sealed class AppleAccountConfiguration
    {
        public const string ProductionEndpoint = "https://api.nasus-games.com";
        public const string ReviewSandboxEndpoint = "https://api.nasus-games.com/review-sandbox";
        public string BaseUrl;
        public bool ReleaseAppleLinking;
        public bool ExperimentalAppleLinking;
        public string QaAccessKey;
        public string RefundSandboxProfile;

        public bool IsProduction => ReleaseAppleLinking && BaseUrl == ProductionEndpoint &&
            !ExperimentalAppleLinking && string.IsNullOrEmpty(QaAccessKey) &&
            string.IsNullOrEmpty(RefundSandboxProfile);

        // Runtime-only configuration selected before any save or credential load.
        // ValidateBuild still requires the production resource in the binary.
        public bool IsReviewSandbox => ReleaseAppleLinking && BaseUrl == ReviewSandboxEndpoint &&
            !ExperimentalAppleLinking && string.IsNullOrEmpty(QaAccessKey) &&
            string.IsNullOrEmpty(RefundSandboxProfile);

        public bool IsEnabled(bool development) => ReleaseAppleLinking
            ? IsProduction
            : development && ExperimentalAppleLinking;

        public void ValidateBuild(bool development)
        {
            if (ReleaseAppleLinking && !IsProduction)
                throw new InvalidOperationException("公開用Apple連携には本番接続先が必要です。検証設定やQA資格情報は併用できません。");
            if (!development && (ExperimentalAppleLinking || !string.IsNullOrEmpty(QaAccessKey)))
                throw new InvalidOperationException("公開ビルドにApple連携の開発検証設定を含めることはできません。");
            if (!string.IsNullOrEmpty(QaAccessKey) && (!development || !ExperimentalAppleLinking ||
                !Regex.IsMatch(QaAccessKey, "\\A[a-f0-9]{64}\\z")))
                throw new InvalidOperationException("検証用接続設定が不正です。");
        }
    }
}
