using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Monetization
{
    [DefaultExecutionOrder(-9000)]
    public sealed class InAppPurchaseService : MonoBehaviour
    {
        private readonly Dictionary<string, Product> products = new Dictionary<string, Product>(StringComparer.Ordinal);
        private readonly HashSet<string> ordersBeingFulfilled = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> confirmationsInFlight = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> failedConfirmations = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SaveManager> confirmationStorageOwners = new Dictionary<string, SaveManager>(StringComparer.Ordinal);
        private SaveManager deliveryStorageOwner;
        private StoreController storeController;
        private bool initializationStarted;
        private bool storeConnected;
        private bool productFetchInProgress;
        private bool slowRequestReported;
        private float catalogRequestStartedAt;
        private float nextCatalogRetryAt = float.PositiveInfinity;
        private int catalogRetryCount;
        private bool purchaseInProgress;
        private bool localizedPriceRefreshRequested;
        private bool purchaseFetchInProgress;
        private float nextPurchaseFetchAt = float.PositiveInfinity;
        private const float RecoveryRetrySeconds = 10f;
        private const float ConfirmationTimeoutSeconds = 30f;
        private const int MaximumCatalogRetries = 3;
        private const float SlowCatalogRequestSeconds = 30f;
#if UNITY_IOS && !UNITY_EDITOR
        private float nextCheckoutPolicyPublishAt;
        private string lastPublishedCheckoutMessage;
#endif
        private enum DiagnosticEvent
        {
            StoreConnectionFailed,
            ProductRequestStartFailed,
            PurchaseStartFailed,
            StoreDisconnected,
            PendingPurchaseLookupFailed,
            ProductFetchFailed,
            PendingOrderInvalid,
            DeliveryRetryPending,
            PurchaseFailed,
            PurchaseConfirmationFailed,
            PurchaseIsolationJournalDeferred
        }

        private static void LogDiagnostic(DiagnosticEvent diagnostic, LogType level, Exception exception = null)
        {
            // Never accept provider details, order/receipt data or delivery text.
            // Even exception type names are allowlisted rather than supplied by a provider.
            string message = "[IAP] Event=" + diagnostic;
            if (exception != null) message += "; Exception=" + SafeExceptionCategory(exception);
            if (level == LogType.Error) Debug.LogError(message);
            else Debug.LogWarning(message);
        }

        private static string SafeExceptionCategory(Exception exception)
        {
            Type type = exception.GetType();
            if (type == typeof(InvalidOperationException)) return "InvalidOperationException";
            if (type == typeof(ArgumentException)) return "ArgumentException";
            if (type == typeof(System.IO.IOException)) return "IOException";
            if (type == typeof(UnauthorizedAccessException)) return "UnauthorizedAccessException";
            if (type == typeof(TimeoutException)) return "TimeoutException";
            return "OtherException";
        }
#if UNITY_EDITOR
        // Deterministic store callbacks for isolated EditMode regression tests.
        internal Action EditorFetchPurchasesOverride;
        internal Action EditorConfirmPurchaseOverride;
        internal Action<string, string, Action<bool, string>> EditorVerifiedDeliveryOverride;
        internal Action EditorConnectOverride;
        internal Action EditorFetchProductsOverride;
        internal Action<string> EditorPurchaseProductOverride;
        internal Func<float> EditorCatalogClockOverride;
        internal Func<bool> EditorPurchaseReadinessOverride;
        internal Func<string> EditorNewPurchaseBlockedMessageOverride;
        internal Func<bool> EditorDeliverySceneReadinessOverride;
#endif

        public static InAppPurchaseService Instance { get; private set; }

        public static event Action ProductsUpdated;
        public static event Action<string, int> PurchaseSucceeded;
        public static event Action<string> PurchaseFailed;

        public bool IsStoreReady { get; private set; }
        public bool IsPurchaseInProgress => purchaseInProgress || ordersBeingFulfilled.Count > 0;
        public bool IsCatalogLoading => (initializationStarted && !storeConnected) || productFetchInProgress;
        public bool AreAllProductsReady => IapProductCatalog.Products.All(p => IsProductAvailable(p.ProductId));
        public bool CanRetryProducts => MonetizationFeatureFlags.StorefrontEnabled && !IsCatalogLoading && !IsPurchaseInProgress;
        public bool HasStoreError { get; private set; }
        public string StoreStatusMessage { get; private set; } = "商品情報を読み込む準備をしています。";
        public string NewPurchaseBlockedMessage
        {
            get
            {
                if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
                    return SaveManager.Instance.StorageStartupMessage;
#if UNITY_EDITOR
                if (EditorNewPurchaseBlockedMessageOverride != null) return EditorNewPurchaseBlockedMessageOverride();
#endif
                string raw = SaveManager.Instance != null ? SaveManager.Instance.PlayerDataConfigurationJson
                    : Resources.Load<TextAsset>("PlayerDataService")?.text;
                string buildMessage = IapPurchaseEnvironmentPolicy.NewPurchaseBlockedMessage(raw,
                    Debug.isDebugBuild || Application.isEditor);
                if (!string.IsNullOrEmpty(buildMessage)) return buildMessage;
#if UNITY_IOS && !UNITY_EDITOR
                return WitchTower.Save.OnlinePlayerData.CheckoutEnvironmentBlockedMessage(ApplePurchaseEnvironmentProbe.Instance?.Environment
                    ?? VerifiedApplePurchaseEnvironment.Unknown);
#else
                return string.Empty;
#endif
            }
        }

        private float CatalogNow
        {
            get
            {
#if UNITY_EDITOR
                if (EditorCatalogClockOverride != null) return EditorCatalogClockOverride();
#endif
                return Time.realtimeSinceStartup;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!MonetizationFeatureFlags.StorefrontEnabled || Instance != null)
            {
                return;
            }

            GameObject root = new GameObject("InAppPurchaseService");
            DontDestroyOnLoad(root);
            root.AddComponent<InAppPurchaseService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_IOS && !UNITY_EDITOR
            var startupProbe = ApplePurchaseEnvironmentProbe.Ensure();
            if (!startupProbe.IsPending && startupProbe.Environment == VerifiedApplePurchaseEnvironment.Unknown)
                startupProbe.Refresh();
#endif
            InitializeStore();
        }

        private void Update()
        {
            PublishCheckoutPolicyChanges();
            UpdateLocalizedPriceRefresh();
            UpdateCatalogRecovery();
            if (Time.unscaledTime >= nextPurchaseFetchAt)
            {
                FetchPendingPurchases();
            }
        }

        private void PublishCheckoutPolicyChanges()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Readiness can change without a catalog event. Refresh visible
            // cards after native verdicts/head changes, without starting an IAP.
            if (Time.realtimeSinceStartup < nextCheckoutPolicyPublishAt) return;
            nextCheckoutPolicyPublishAt = Time.realtimeSinceStartup + .5f;
            string message = NewPurchaseBlockedMessage;
            if (message == lastPublishedCheckoutMessage) return;
            lastPublishedCheckoutMessage = message;
            ProductsUpdated?.Invoke();
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused)
            {
#if UNITY_IOS && !UNITY_EDITOR
                ApplePurchaseEnvironmentProbe.Ensure().Refresh();
#endif
                // Sandbox sign-in / network recovery can happen while the app
                // is in the background. Recover products, not just old orders.
                RefreshLocalizedPrices();
                FetchPendingPurchases();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (storeController == null) return;
            storeController.OnStoreConnected -= HandleStoreConnected;
            storeController.OnStoreDisconnected -= HandleStoreDisconnected;
            storeController.OnProductsFetched -= HandleProductsFetched;
            storeController.OnProductsFetchFailed -= HandleProductsFetchFailed;
            storeController.OnPurchasesFetched -= HandlePurchasesFetched;
            storeController.OnPurchasesFetchFailed -= HandlePurchasesFetchFailed;
            storeController.OnPurchasePending -= HandlePurchasePending;
            storeController.OnPurchaseFailed -= HandlePurchaseFailed;
            storeController.OnPurchaseDeferred -= HandlePurchaseDeferred;
            storeController.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
        }

        private async void InitializeStore()
        {
            if (initializationStarted || !MonetizationFeatureFlags.StorefrontEnabled)
            {
                return;
            }

            initializationStarted = true;
            BeginCatalogRequest("App Storeに接続しています…");
            try
            {
#if UNITY_EDITOR
                if (EditorConnectOverride != null)
                {
                    EditorConnectOverride();
                    return;
                }
#endif
                if (storeController == null)
                {
                    storeController = UnityIAPServices.StoreController();
                    storeController.OnStoreConnected += HandleStoreConnected;
                    storeController.OnStoreDisconnected += HandleStoreDisconnected;
                    storeController.OnProductsFetched += HandleProductsFetched;
                    storeController.OnProductsFetchFailed += HandleProductsFetchFailed;
                    storeController.OnPurchasesFetched += HandlePurchasesFetched;
                    storeController.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
                    storeController.OnPurchasePending += HandlePurchasePending;
                    storeController.OnPurchaseFailed += HandlePurchaseFailed;
                    storeController.OnPurchaseDeferred += HandlePurchaseDeferred;
                    storeController.OnPurchaseConfirmed += HandlePurchaseConfirmed;
                }
                await storeController.Connect();
            }
            catch (Exception exception)
            {
                if (this == null) return;
                initializationStarted = false;
                storeConnected = false;
                IsStoreReady = false;
                LogDiagnostic(DiagnosticEvent.StoreConnectionFailed, LogType.Error, exception);
                ScheduleCatalogRetry("App Storeに接続できませんでした。通信状態をご確認ください。");
            }
        }

        public bool IsProductAvailable(string productId)
        {
            return !string.IsNullOrWhiteSpace(productId) && IsStoreReady && products.TryGetValue(productId, out Product product) && product != null && product.availableToPurchase;
        }

        public void EnsureProductsAvailable()
        {
            if (!AreAllProductsReady && CanRetryProducts) RefreshProducts();
        }

        public void RefreshProducts()
        {
            if (!CanRetryProducts) return;
            catalogRetryCount = 0;
            RequestMissingProducts();
        }

        // Sandbox sign-in can change the storefront after the initial fetch.
        // Missing-only retries leave the old currency cached indefinitely.
        public void RefreshLocalizedPrices()
        {
            localizedPriceRefreshRequested = true;
            UpdateLocalizedPriceRefresh();
        }

        private void UpdateLocalizedPriceRefresh()
        {
            // Never interrupt a purchase or overlap an outstanding request.
            if (!localizedPriceRefreshRequested || !CanRetryProducts) return;
            localizedPriceRefreshRequested = false;
            products.Clear();
            IsStoreReady = false;
            RefreshProducts();
        }

        private void RequestMissingProducts()
        {
            if (IsCatalogLoading || IsPurchaseInProgress || !MonetizationFeatureFlags.StorefrontEnabled) return;
            if (!storeConnected)
            {
                InitializeStore();
                return;
            }

            var definitions = IapProductCatalog.Products.Where(p => !IsProductAvailable(p.ProductId))
                .Select(p => new ProductDefinition(p.ProductId, ProductType.Consumable)).ToList();
            if (definitions.Count == 0)
            {
                nextCatalogRetryAt = float.PositiveInfinity;
                PublishCatalogStatus("商品を選ぶと、App Storeの購入確認画面が開きます。", false);
                return;
            }

            productFetchInProgress = true;
            BeginCatalogRequest("App Storeから商品情報を取得しています…");
            try
            {
#if UNITY_EDITOR
                if (EditorFetchProductsOverride != null)
                {
                    EditorFetchProductsOverride();
                    return;
                }
#endif
                // Own a bounded retry loop. The SDK's default unlimited retry
                // would otherwise hide failures and collide with manual retries.
                storeController.FetchProducts(definitions, new NoRetriesPolicy());
            }
            catch (Exception exception)
            {
                productFetchInProgress = false;
                LogDiagnostic(DiagnosticEvent.ProductRequestStartFailed, LogType.Warning, exception);
                ScheduleCatalogRetry("商品情報を取得できませんでした。通信状態をご確認ください。");
            }
        }

        private void BeginCatalogRequest(string message)
        {
            nextCatalogRetryAt = float.PositiveInfinity;
            catalogRequestStartedAt = CatalogNow;
            slowRequestReported = false;
            PublishCatalogStatus(message, false);
        }

        private void PublishCatalogStatus(string message, bool error)
        {
            StoreStatusMessage = message;
            HasStoreError = error;
            ProductsUpdated?.Invoke();
        }

        private void ScheduleCatalogRetry(string message)
        {
            // A partial fetch triggers success AND failure callbacks. Schedule
            // once and preserve products that the store did return successfully.
            if (float.IsPositiveInfinity(nextCatalogRetryAt) && catalogRetryCount < MaximumCatalogRetries)
            {
                nextCatalogRetryAt = CatalogNow + 5f * (1 << catalogRetryCount);
                catalogRetryCount++;
            }
            string nextStep = float.IsPositiveInfinity(nextCatalogRetryAt)
                ? "\n接続確認後「商品情報を再取得」を押してください。"
                : "\nまもなく自動で再試行します。「再取得」も使えます。";
            PublishCatalogStatus(message + nextStep, true);
        }

        private void UpdateCatalogRecovery()
        {
            if (IsCatalogLoading && !slowRequestReported && CatalogNow - catalogRequestStartedAt >= SlowCatalogRequestSeconds)
            {
                slowRequestReported = true;
                // Do not overlap an outstanding StoreKit request: Unity IAP
                // has no cancellation API. A late response still completes it.
                PublishCatalogStatus("App Storeの応答に時間がかかっています。\n通信を確認し、続く場合はアプリを開き直してください。", true);
            }
            if (CatalogNow >= nextCatalogRetryAt && !IsCatalogLoading && !IsPurchaseInProgress)
                RequestMissingProducts();
        }

        public void Purchase(string productId)
        {
            if (WitchTower.Save.RefundSandboxConfiguration.IsConfigured && !WitchTower.Save.RefundSandboxConfiguration.IsActive)
            {
                PurchaseFailed?.Invoke("返金テストの接続期限が切れています。購入は開始しません。");
                return;
            }
            // Block only a new checkout, before readiness can start networking or
            // bind the active account to StoreKit. Catalog/pending-order recovery
            // and acknowledgement of already delivered orders remain available.
            string blockedMessage = NewPurchaseBlockedMessage;
            if (!string.IsNullOrEmpty(blockedMessage))
            {
                PurchaseFailed?.Invoke(blockedMessage);
                return;
            }
            bool onlineReady = WitchTower.Save.OnlinePlayerData.CanStartPurchase;
#if UNITY_EDITOR
            if (EditorPurchaseReadinessOverride != null) onlineReady = EditorPurchaseReadinessOverride();
#endif
            if (!onlineReady)
            {
                WitchTower.Save.OnlinePlayerData.Ensure().Execute(null, (operation, message) => {
                    if (WitchTower.Save.OnlinePlayerData.CanStartPurchase) Purchase(productId);
                    else PurchaseFailed?.Invoke(message ?? "購入検証サーバーの準備ができていません。現在は購入できません。");
                });
                return;
            }
            if (!MonetizationFeatureFlags.StorefrontEnabled)
            {
                PurchaseFailed?.Invoke("購入機能は現在利用できません。");
                return;
            }

            if (IsPurchaseInProgress)
            {
                PurchaseFailed?.Invoke("購入処理中です。しばらくお待ちください。");
                return;
            }

            if (!IsProductAvailable(productId))
            {
                EnsureProductsAvailable();
                PurchaseFailed?.Invoke("この商品はまだ購入できません。情報取得後、もう一度商品をタップしてください。");
                return;
            }

            purchaseInProgress = true;
            localizedPriceRefreshRequested = true;
            ProductsUpdated?.Invoke();
            try
            {
                // UI callbacks must not be able to invalidate the checked
                // environment/account before StoreKit is bound to that account.
                string finalBlockedMessage = NewPurchaseBlockedMessage;
                if (!string.IsNullOrEmpty(finalBlockedMessage))
                {
                    purchaseInProgress = false;
                    ProductsUpdated?.Invoke();
                    PurchaseFailed?.Invoke(finalBlockedMessage);
                    return;
                }
#if UNITY_IOS && !UNITY_EDITOR
                if (!WitchTower.Save.OnlinePlayerData.CanStartPurchase)
                {
                    purchaseInProgress = false;
                    ProductsUpdated?.Invoke();
                    PurchaseFailed?.Invoke(IapPurchaseEnvironmentPolicy.PurchaseCapabilityUnavailableMessage);
                    return;
                }
#endif
#if UNITY_EDITOR
                if (EditorPurchaseProductOverride != null) EditorPurchaseProductOverride(productId);
                else
#endif
                {
                    storeController.AppleStoreExtendedService?.SetAppAccountToken(new Guid(GameManager.Instance.PlayerProfile.PlayerId));
                    storeController.PurchaseProduct(products[productId]);
                }
            }
            catch (Exception exception)
            {
                purchaseInProgress = false;
                LogDiagnostic(DiagnosticEvent.PurchaseStartFailed, LogType.Warning, exception);
                PurchaseFailed?.Invoke("購入処理を開始できませんでした。通信状態をご確認ください。");
                FetchPendingPurchases();
            }
        }

        public string GetLocalizedPrice(string productId, string fallbackPrice)
        {
            if (products.TryGetValue(productId, out Product product) &&
                product?.metadata != null &&
                !string.IsNullOrWhiteSpace(product.metadata.localizedPriceString))
            {
                ProductMetadata metadata = product.metadata;
                // Use the store amount, never a conversion or catalog fallback.
                if (metadata.isoCurrencyCode == "JPY" && metadata.localizedPrice >= 0m &&
                    decimal.Truncate(metadata.localizedPrice) == metadata.localizedPrice)
                    return metadata.localizedPrice.ToString("N0", CultureInfo.GetCultureInfo("ja-JP")) + "円";
                return metadata.localizedPriceString;
            }

            return fallbackPrice;
        }

        private void HandleStoreConnected()
        {
            storeConnected = true;
            initializationStarted = true;
            RequestMissingProducts();
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription description)
        {
            IsStoreReady = false;
            storeConnected = false;
            productFetchInProgress = false;
            products.Clear();
            initializationStarted = false;
            purchaseFetchInProgress = false;
            LogDiagnostic(DiagnosticEvent.StoreDisconnected, LogType.Warning);
            ScheduleCatalogRetry("App Storeとの接続が切れました。通信状態をご確認ください。");
        }

        private void HandleProductsFetched(List<Product> fetchedProducts)
        {
            if (!storeConnected) return; // Ignore a late callback after disconnection.
            productFetchInProgress = false;
            foreach (Product product in fetchedProducts ?? new List<Product>())
            {
                if (product?.definition != null && IapProductCatalog.TryGet(product.definition.id, out _))
                {
                    products[product.definition.id] = product;
                }
            }

            IsStoreReady = products.Values.Any(p => p != null && p.availableToPurchase);
            if (AreAllProductsReady)
            {
                catalogRetryCount = 0;
                nextCatalogRetryAt = float.PositiveInfinity;
                PublishCatalogStatus("商品を選ぶと、App Storeの購入確認画面が開きます。", false);
            }
            else
            {
                ScheduleCatalogRetry(IsStoreReady
                    ? "一部の商品情報を取得できませんでした。取得済みの商品は購入できます。"
                    : "購入できる商品情報を取得できませんでした。");
            }
            FetchPendingPurchases();
        }

        private void FetchPendingPurchases()
        {
            nextPurchaseFetchAt = float.PositiveInfinity;
            bool hasStore = storeController != null;
#if UNITY_EDITOR
            hasStore |= EditorFetchPurchasesOverride != null;
#endif
            if (!IsStoreReady || !hasStore || purchaseFetchInProgress) return;
            purchaseFetchInProgress = true;
            try
            {
                // Unity IAP automatically routes the fetched pending orders to
                // OnPurchasePending. Do not grant them again in OnPurchasesFetched.
#if UNITY_EDITOR
                if (EditorFetchPurchasesOverride != null) EditorFetchPurchasesOverride();
                else
#endif
                    storeController.FetchPurchases();
            }
            catch (Exception exception)
            {
                purchaseFetchInProgress = false;
                nextPurchaseFetchAt = Time.unscaledTime + RecoveryRetrySeconds;
                LogDiagnostic(DiagnosticEvent.PendingPurchaseLookupFailed, LogType.Warning, exception);
            }
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            purchaseFetchInProgress = false;
        }

        private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            purchaseFetchInProgress = false;
            nextPurchaseFetchAt = Time.unscaledTime + RecoveryRetrySeconds;
            LogDiagnostic(DiagnosticEvent.PendingPurchaseLookupFailed, LogType.Warning);
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            if (!storeConnected) return;
            productFetchInProgress = false;
            IsStoreReady = products.Values.Any(p => p != null && p.availableToPurchase);
            LogDiagnostic(DiagnosticEvent.ProductFetchFailed, LogType.Warning);
            ScheduleCatalogRetry(IsStoreReady
                ? "一部の商品情報を取得できませんでした。取得済みの商品は購入できます。"
                : "商品情報を取得できませんでした。通信状態をご確認ください。");
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            Product product = order?.CartOrdered?.Items().FirstOrDefault()?.Product;
            string productId = product?.definition?.id;
            string transactionId = order?.Info?.TransactionID;
            if (string.IsNullOrWhiteSpace(productId) ||
                string.IsNullOrWhiteSpace(transactionId) ||
                !IapProductCatalog.TryGet(productId, out IapProductDefinition definition))
            {
                purchaseInProgress = false;
                LogDiagnostic(DiagnosticEvent.PendingOrderInvalid, LogType.Error);
                PurchaseFailed?.Invoke("購入情報を確認できませんでした。商品の反映を保留しています。再購入せず、通信状態をご確認ください。");
                return;
            }

            if (ordersBeingFulfilled.Add(transactionId))
            {
                ProductsUpdated?.Invoke();
                StartCoroutine(FulfillWhenPlayerProfileIsReady(order, definition, transactionId));
            }
        }

        private IEnumerator FulfillWhenPlayerProfileIsReady(
            PendingOrder order,
            IapProductDefinition definition,
            string transactionId)
        {
            bool grantedDuringRecovery = false;
            bool errorReported = false;
            try
            {
                while (true)
                {
                    while (GameManager.Instance?.PlayerProfile == null || SaveManager.Instance == null ||
                        !SaveManager.Instance.StorageAccessAvailable ||
                        (!ReferenceEquals(deliveryStorageOwner, null) && deliveryStorageOwner != SaveManager.Instance))
                    {
                        yield return null;
                    }
                    if (ReferenceEquals(deliveryStorageOwner, null)) deliveryStorageOwner = SaveManager.Instance;

                    bool completed = false;
                    bool newlyGranted = false;
                    string deliveryMessage = null;
                    // Delivery is only confirmed after the server verifies the store
                    // transaction and its durable result has been saved on this device.
                    if (!WitchTower.Save.OnlinePlayerData.Busy && IsDeliverySceneReady)
                    {
                        // An explicitly approved Sandbox recovery is not a grant
                        // to whichever production account happens to be active.
                        // Read its durable shared-root tombstone on every retry.
                        if (TryHandleIsolatedDelivery(definition.ProductId, transactionId, out completed, out deliveryMessage))
                        {
                            if (completed) ConfirmPurchase(order, transactionId);
                        }
                        else
                        {
                            bool finished = false;
                            bool alreadyApplied = GameManager.Instance.PlayerProfile.HasProcessedIapTransaction(transactionId);
                            Action<bool, string> delivered = (verified, message) => {
                                deliveryMessage = message;
                                completed = SaveManager.Instance == deliveryStorageOwner && !ReferenceEquals(deliveryStorageOwner, null) &&
                                    deliveryStorageOwner.StorageAccessAvailable && verified && message == null &&
                                    GameManager.Instance?.PlayerProfile?.HasProcessedIapTransaction(transactionId) == true;
                                newlyGranted = completed && !alreadyApplied;
                                finished = true;
                            };
#if UNITY_EDITOR
                            if (EditorVerifiedDeliveryOverride != null) EditorVerifiedDeliveryOverride(definition.ProductId, transactionId, delivered);
                            else
#endif
                            WitchTower.Save.OnlinePlayerData.Ensure().Execute(new WitchTower.Save.OnlineRequest {
                                Kind = "purchase", Target = definition.ProductId,
                                RequestId = "apple-" + transactionId, TransactionId = transactionId,
                                Receipt = order?.Info?.Apple?.jwsRepresentation
                            }, (op, message) => delivered(op != null, message));
                            while (!finished) yield return null;
                            if (completed) ConfirmPurchase(order, transactionId);
                        }
                    }
                    grantedDuringRecovery |= newlyGranted;
                    if (completed)
                    {
                        // ConfirmPurchase is asynchronous. Keep this order until
                        // the store acknowledges it; Unity IAP filters repeat
                        // pending callbacks within a session, so FetchPurchases
                        // alone cannot retry a failed confirmation.
                        float deadline = Time.realtimeSinceStartup + ConfirmationTimeoutSeconds;
                        while (confirmationsInFlight.Contains(transactionId) && Time.realtimeSinceStartup < deadline)
                        {
                            yield return null;
                        }
                        bool timedOut = confirmationsInFlight.Remove(transactionId);
                        bool confirmationFailed = failedConfirmations.Remove(transactionId);
                        if (!timedOut && !confirmationFailed) break;
                    }

                    if (!errorReported)
                    {
                        LogDiagnostic(DiagnosticEvent.DeliveryRetryPending, LogType.Warning);
                        PurchaseFailed?.Invoke(deliveryMessage ?? "購入内容の保存・反映を再試行しています。再購入せず、空き容量と通信状態をご確認ください。");
                        errorReported = true;
                    }
                    yield return new WaitForSecondsRealtime(RecoveryRetrySeconds);
                }
            }
            finally
            {
                ordersBeingFulfilled.Remove(transactionId);
                confirmationsInFlight.Remove(transactionId);
                failedConfirmations.Remove(transactionId);
                confirmationStorageOwners.Remove(transactionId);
                purchaseInProgress = false;
                ProductsUpdated?.Invoke();
            }
            if (grantedDuringRecovery)
            {
                PurchaseSucceeded?.Invoke(definition.ProductId, definition.PaidStoneAmount);
            }
        }

        private bool IsDeliverySceneReady
        {
            get
            {
#if UNITY_EDITOR
                if (EditorDeliverySceneReadinessOverride != null) return EditorDeliverySceneReadinessOverride();
#endif
                return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "BattleScene";
            }
        }

        private static bool TryHandleIsolatedDelivery(string productId, string transactionId,
            out bool completed, out string message)
        {
            completed = false;
            message = null;
            if (SaveManager.Instance == null || !SaveManager.Instance.StorageAccessAvailable)
            { message = SaveManager.Instance?.StorageStartupMessage; return true; }
            try
            {
                var journal = WitchTower.Save.PurchaseIsolationRecoveryStorage.Read(
                    WitchTower.Save.PurchaseIsolationRecoveryStorage.RuntimeRoot);
                if (journal == null || !WitchTower.Save.PurchaseIsolationRecoveryStorage.Matches(
                    WitchTower.Save.PurchaseIsolationRecoveryStorage.RuntimeRoot, productId, transactionId))
                    return false;
                completed = WitchTower.Save.PurchaseIsolationRecoveryStorage.CanConfirm(
                    WitchTower.Save.PurchaseIsolationRecoveryStorage.RuntimeRoot, productId, transactionId);
                if (completed && !journal.SourcePendingCleared)
                    WitchTower.Save.PurchaseIsolationRecoveryStorage.ClearOriginalPending(
                        WitchTower.Save.PurchaseIsolationRecoveryStorage.RuntimeRoot);
                if (!completed) message = "テスト購入の隔離復旧を確認中です。購入情報は保持しています。再購入しないでください。";
                return true;
            }
            catch
            {
                // A partial/corrupt record is never permission to send the order
                // to production or acknowledge it with StoreKit.
                completed = false;
                message = "テスト購入の復旧記録を確認できません。購入情報は保持しています。再購入しないでください。";
                return true;
            }
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            purchaseInProgress = false;
            LogDiagnostic(DiagnosticEvent.PurchaseFailed, LogType.Warning);
            PurchaseFailed?.Invoke("購入が完了しませんでした。キャンセルした場合、料金は発生しません。");
        }

        private void ConfirmPurchase(PendingOrder order, string transactionId)
        {
            if (SaveManager.Instance == null || !SaveManager.Instance.StorageAccessAvailable ||
                (!ReferenceEquals(deliveryStorageOwner, null) && deliveryStorageOwner != SaveManager.Instance))
            { failedConfirmations.Add(transactionId); return; }
            failedConfirmations.Remove(transactionId);
            confirmationsInFlight.Add(transactionId);
            confirmationStorageOwners[transactionId] = SaveManager.Instance;
            try
            {
#if UNITY_EDITOR
                if (EditorConfirmPurchaseOverride != null)
                {
                    EditorConfirmPurchaseOverride();
                    return;
                }
#endif
                storeController.ConfirmPurchase(order);
            }
            catch (Exception exception)
            {
                confirmationsInFlight.Remove(transactionId);
                confirmationStorageOwners.Remove(transactionId);
                LogDiagnostic(DiagnosticEvent.PurchaseConfirmationFailed, LogType.Warning, exception);
                // A start failure is not an acknowledgement. Let the existing
                // fulfillment loop retry without granting the purchase again.
                failedConfirmations.Add(transactionId);
            }
        }

        private void HandlePurchaseDeferred(DeferredOrder order)
        {
            purchaseInProgress = false;
            PurchaseFailed?.Invoke("購入は承認待ちです。承認後に自動で反映されます。");
        }

        private void HandlePurchaseConfirmed(Order order)
        {
            HandleConfirmationResult(order?.Info?.TransactionID, order is FailedOrder);
            if (order is FailedOrder)
            {
                LogDiagnostic(DiagnosticEvent.PurchaseConfirmationFailed, LogType.Error);
            }
        }

        private void HandleConfirmationResult(string transactionId, bool failed)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !confirmationsInFlight.Remove(transactionId)) return;
            confirmationStorageOwners.TryGetValue(transactionId, out var owner);
            confirmationStorageOwners.Remove(transactionId);
            if (owner == null || owner != SaveManager.Instance || !owner.StorageAccessAvailable)
            { failedConfirmations.Add(transactionId); return; }
            if (failed) failedConfirmations.Add(transactionId);
            else
            {
                if (SaveManager.Instance == null || !SaveManager.Instance.StorageAccessAvailable) return;
                try
                {
                    string root = WitchTower.Save.PurchaseIsolationRecoveryStorage.RuntimeRoot;
                    var journal = WitchTower.Save.PurchaseIsolationRecoveryStorage.Read(root);
                    if (journal != null && journal.Request.TransactionId == transactionId)
                        WitchTower.Save.PurchaseIsolationRecoveryStorage.MarkStoreConfirmed(root, journal.Request.Target, transactionId);
                }
                catch (Exception exception)
                {
                    // Delivery/tombstone remain durable even if this additional
                    // acknowledgement record cannot be refreshed.
                    LogDiagnostic(DiagnosticEvent.PurchaseIsolationJournalDeferred, LogType.Warning, exception);
                }
            }
        }
    }
}
