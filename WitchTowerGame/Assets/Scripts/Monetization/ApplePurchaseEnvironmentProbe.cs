using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace WitchTower.Monetization
{
    // Only a fixed verdict crosses the native boundary. Never reads a receipt,
    // grants currency, authorizes delivery, changes a save or selects an endpoint.
    public sealed class ApplePurchaseEnvironmentProbe : MonoBehaviour
    {
        [Serializable] private sealed class Result { public string RequestId; public string Environment; public string Reason; }
        private const float TimeoutSeconds = 15f;
        private const float RetrySeconds = 30f;
        private const float UserActionTimeoutSeconds = 180f;
        public static ApplePurchaseEnvironmentProbe Instance { get; private set; }
        public VerifiedApplePurchaseEnvironment Environment { get; private set; }
        public bool IsPending => requestId != null;
        public bool IsUserActionPending => IsPending && userInitiated;
        public string DiagnosticCode { get; private set; } = "NotStarted";
        public string StatusMessage => IsPending
            ? (userInitiated ? "App Storeで確認しています。認証画面が出た場合は操作を完了してください。" : "App Storeに確認しています…")
            : DiagnosticCode == "Cancelled" ? "App Storeの確認がキャンセルされました。もう一度確認できます。"
            : DiagnosticCode == "Unverified" ? "App Storeの情報を検証できませんでした。下のボタンから再確認してください。"
            : DiagnosticCode == "Timeout" ? "App Storeからの応答を待っています。通信を確認して、もう一度お試しください。"
            : DiagnosticCode == "Network" ? "App Storeに接続できませんでした。通信を確認して、もう一度お試しください。"
            : "App Storeの情報を取得できませんでした。下のボタンから再確認してください。";
        private bool userInitiated;
#if UNITY_EDITOR
        internal static Action<string, bool> EditorNativeRequestOverride;
#endif
        private string requestId;
        private float expiresAt;
        private float retryAt = float.PositiveInfinity;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NasusApplePurchaseEnvironment(string receiver, string requestId);
        [DllImport("__Internal")] private static extern void NasusApplePurchaseEnvironmentRefresh(string receiver, string requestId);
        [DllImport("__Internal")] private static extern void NasusApplePurchaseEnvironmentCancel(string requestId);
#endif
        public static ApplePurchaseEnvironmentProbe Ensure()
        {
            if (Instance != null) return Instance;
            var owner = new GameObject("ApplePurchaseEnvironmentProbe");
            return owner.AddComponent<ApplePurchaseEnvironmentProbe>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        private void OnDestroy()
        {
            CancelRequest();
            if (Instance == this) Instance = null;
        }
        public void Refresh()
        {
            // Focus events may arrive while StoreKit is presenting authentication.
            // Keep that request alive; replacing it discards the user's result.
            if (IsPending) return;
            BeginRequest(false);
        }
        public void RetryFromUserAction()
        {
            if (IsPending) return;
            BeginRequest(true);
        }
        private void BeginRequest(bool fromUserAction)
        {
            CancelRequest();
            Environment = VerifiedApplePurchaseEnvironment.Unknown;
            userInitiated = fromUserAction;
            requestId = Guid.NewGuid().ToString("N");
            expiresAt = Time.realtimeSinceStartup + (fromUserAction ? UserActionTimeoutSeconds : TimeoutSeconds);
            retryAt = float.PositiveInfinity;
            DiagnosticCode = fromUserAction ? "Refreshing" : "Checking";
            Debug.Log("[AppleEnvironment] " + DiagnosticCode);
#if UNITY_EDITOR
            if (EditorNativeRequestOverride != null)
            {
                EditorNativeRequestOverride(requestId, fromUserAction);
                return;
            }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (fromUserAction) NasusApplePurchaseEnvironmentRefresh(gameObject.name, requestId);
                else NasusApplePurchaseEnvironment(gameObject.name, requestId);
            }
            catch (Exception) { Complete(VerifiedApplePurchaseEnvironment.Unknown, "BridgeUnavailable"); }
#else
            Complete(VerifiedApplePurchaseEnvironment.Unsupported, "Unsupported");
#endif
        }
        private void Update()
        {
            if (requestId != null && Time.realtimeSinceStartup >= expiresAt)
                Complete(VerifiedApplePurchaseEnvironment.Unknown, "Timeout");
            if (requestId == null && Time.realtimeSinceStartup >= retryAt) Refresh();
        }
        private void Complete(VerifiedApplePurchaseEnvironment environment, string reason = "Unknown")
        {
            CancelRequest();
            Environment = environment;
            userInitiated = false;
            DiagnosticCode = SafeDiagnostic(reason);
            Debug.Log("[AppleEnvironment] " + DiagnosticCode + " / " + environment);
            retryAt = environment == VerifiedApplePurchaseEnvironment.Unknown
                ? Time.realtimeSinceStartup + RetrySeconds : float.PositiveInfinity;
        }
        private static string SafeDiagnostic(string reason)
        {
            switch (reason)
            {
                case "Verified": case "Unverified": case "BundleMismatch": case "UnknownEnvironment":
                case "StoreKitFailure": case "Cancelled": case "Network": case "StoreUnavailable":
                case "NotEntitled": case "Unsupported": case "Timeout": case "BridgeUnavailable":
                case "Malformed": return reason;
                default: return "Unknown";
            }
        }
        private void CancelRequest()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (requestId != null)
            {
                try { NasusApplePurchaseEnvironmentCancel(requestId); }
                catch (Exception) { } // Keep a failed/missing bridge fail-closed.
            }
#endif
            requestId = null;
        }
        [Preserve]
        public void OnApplePurchaseEnvironment(string json)
        {
            if (requestId == null) return;
            // A verdict is an allowlisted hint only. Delivery still independently
            // verifies Apple's signature, environment and appAccountToken server-side.
            try
            {
                var result = JsonUtility.FromJson<Result>(json);
                if (result == null || result.RequestId != requestId || Time.realtimeSinceStartup >= expiresAt) return;
                Complete(result.Environment == "Production" ? VerifiedApplePurchaseEnvironment.Production
                    : result.Environment == "Sandbox" ? VerifiedApplePurchaseEnvironment.Sandbox
                    : result.Environment == "Unsupported" ? VerifiedApplePurchaseEnvironment.Unsupported
                    : VerifiedApplePurchaseEnvironment.Unknown, result.Reason);
            }
            catch (Exception) { Complete(VerifiedApplePurchaseEnvironment.Unknown, "Malformed"); }
        }
    }
}
