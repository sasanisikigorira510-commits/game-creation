using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Managers;
using WitchTower.Save;

namespace WitchTower.Monetization
{
    // Development-only controls. All balances and transaction IDs come from the isolated save.
    public sealed class RefundSandboxPanel : MonoBehaviour
    {
        private string message = "この画面は返金検証専用です。通常のセーブは使用しません。";
        private bool refundRequestOpen;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NasusRefundSandboxRequest(string transactionId, string receiver);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SaveManager.Instance?.StorageAccessAvailable == false) return;
            if (!Debug.isDebugBuild || !RefundSandboxConfiguration.IsConfigured) return;
            var root = new GameObject("RefundSandboxPanel");
            DontDestroyOnLoad(root);
            root.AddComponent<RefundSandboxPanel>();
        }
        private void Awake()
        {
            // Block underlying game buttons without modifying any tutorial or game progression.
            var blocker = new GameObject("SandboxInputShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            blocker.transform.SetParent(transform, false);
            var canvas = blocker.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var cover = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(blocker.transform, false);
            var rect = cover.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().color = Color.clear;
            InAppPurchaseService.PurchaseSucceeded += Purchased;
            InAppPurchaseService.PurchaseFailed += Failed;
        }
        private void OnDestroy()
        {
            InAppPurchaseService.PurchaseSucceeded -= Purchased;
            InAppPurchaseService.PurchaseFailed -= Failed;
        }
        private void Purchased(string productId, int amount) => message = $"テスト購入を反映しました：{amount:N0}個。返金はまだ申請していません。";
        private void Failed(string error) => message = error;
        public void OnRefundResult(string result)
        {
            refundRequestOpen = false;
            message = result == "submitted" ? "返金申請を送信しました。承認・宝晶の回収は未確認です。サーバー反映を確認してください。"
                : result == "cancelled" ? "返金申請をキャンセルしました。"
                : result == "not_sandbox" ? "Sandbox環境を確認できないため、返金申請を開きませんでした。"
                : "返金申請画面を開けませんでした。環境と接続を確認してください。";
            OnlinePlayerData.Instance?.RequestConnectionRefresh();
        }
        private void OnGUI()
        {
            if (SaveManager.Instance == null || !SaveManager.Instance.StorageAccessAvailable) return;
            GUI.depth = -10000;
            float scale = Screen.width / 750f;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float height = Screen.height / scale;
            GUI.Box(new Rect(0, 0, 750, height), GUIContent.none);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true };
            var title = new GUIStyle(label) { fontSize = 38, fontStyle = FontStyle.Bold };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 28, wordWrap = true };
            var profile = GameManager.Instance?.PlayerProfile;
            var store = InAppPurchaseService.Instance;
            bool active = RefundSandboxConfiguration.IsActive;
            string transaction = profile?.ProcessedIapTransactionIds?.LastOrDefault();
            GUILayout.BeginArea(new Rect(35, 100, 680, height - 130));
            GUILayout.Label("Sandbox 返金テスト専用", title);
            GUILayout.Label("通常データとは別保存・別サーバー\n購入画面に Sandbox と表示されることを確認してください。", label);
            GUILayout.Space(24);
            GUILayout.Label(active ? "専用設定：有効" : "専用設定：無効／期限切れ（操作停止）", label);
            GUILayout.Label(profile == null ? "テストデータ準備中…" : $"テスト用有償宝晶：{profile.PaidGachaStones:N0}個\n確認済み購入：{profile.ProcessedIapTransactionIds?.Count ?? 0}件", title);
            GUILayout.Label(store?.StoreStatusMessage ?? "ストア準備中…", label);
            GUILayout.Label(OnlinePlayerData.LastMessage, label);
            GUILayout.Label(OnlinePlayerData.PaidSpendingUnavailableMessage, label);
            GUILayout.Space(24);
            GUI.enabled = active && profile != null && store != null && store.IsStoreReady && !store.IsPurchaseInProgress && !refundRequestOpen;
            if (GUILayout.Button("① 120個のテスト購入画面を開く", button, GUILayout.Height(100)))
            {
                message = "Appleの購入確認画面でSandbox表示を確認してください。";
                store.Purchase(IapProductCatalog.Crystals120);
            }
            GUI.enabled = active && !string.IsNullOrEmpty(transaction) && store != null && !store.IsPurchaseInProgress && !refundRequestOpen;
            if (GUILayout.Button("② 最後のテスト購入の返金申請", button, GUILayout.Height(100)))
            {
#if UNITY_IOS && !UNITY_EDITOR
                refundRequestOpen = true;
                message = "Appleの返金申請画面を開いています…";
                NasusRefundSandboxRequest(transaction, gameObject.name);
#else
                message = "返金申請はiPhoneのSandboxでのみ実行できます。";
#endif
            }
            GUI.enabled = active && !refundRequestOpen && !OnlinePlayerData.Busy;
            if (GUILayout.Button("③ サーバーの状態を再確認", button, GUILayout.Height(90)))
                OnlinePlayerData.Instance?.RequestConnectionRefresh();
            GUI.enabled = true;
            GUILayout.Space(24);
            GUILayout.Label(message, label);
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
