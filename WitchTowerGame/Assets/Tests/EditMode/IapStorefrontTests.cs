using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class IapStorefrontTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Prefix = "com.nasus.dungeonmonsterroguelike.crystals";
        private const string SensitiveMarker = "SYNTHETIC_IAP_PRIVATE_TOKEN_RECEIPT_TRANSACTION";
        private static readonly int[] Amounts = { 120, 650, 2000, 4200, 8600, 15000 };
        private SceneSetup[] scenes;
        private object oldService, oldGame;
        private Component service, shop;
        private Canvas canvas;
        private Camera camera;
        private RenderTexture target;
        private EventSystem events;
        private float now;
        private int connections, fetches, pendingFetches;
        private readonly List<string> purchases = new List<string>();

        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static Type Iap(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.Purchasing." + name)).First(t => t != null);
        private static object Call(object obj, string name, params object[] args) => obj.GetType()
            .GetMethod(name, Hidden | BindingFlags.Public).Invoke(obj, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
        private static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static void Singleton(Type type, object value) => type.GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private bool Available(int amount) => (bool)Call(service, "IsProductAvailable", Prefix + amount);
        private string Status => (string)Prop(service, "StoreStatusMessage");
        private static string CheckoutBlockedReason => (string)T("Monetization.IapPurchaseEnvironmentPolicy")
            .GetField("DevelopmentProductionMessage").GetRawConstantValue();

        private static void AssertSafeDiagnostic(Action callback, LogType level, string expected)
        {
            var messages = new List<string>();
            var allLogText = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                allLogText.Add(message);
                allLogText.Add(trace);
                if (message.StartsWith("[IAP]", StringComparison.Ordinal)) messages.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                LogAssert.Expect(level, expected);
                callback();
            }
            finally { Application.logMessageReceived -= capture; }
            Assert.That(messages, Is.EqualTo(new[] { expected }));
            Assert.That(string.Join("\n", allLogText), Does.Not.Contain(SensitiveMarker));
        }

        private sealed class UntrustedDiagnosticException : Exception
        {
            public override string Message => throw new InvalidOperationException("Must not inspect exception messages.");
            public override string ToString() => SensitiveMarker;
        }

        [SetUp]
        public void Setup()
        {
            now = 0;
            connections = fetches = pendingFetches = 0;
            purchases.Clear();
            shop = null;
            canvas = null;
            camera = null;
            target = null;
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldService = T("Monetization.InAppPurchaseService").GetProperty("Instance").GetValue(null);
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Singleton(T("Managers.GameManager"), null);
            var owner = new GameObject("IsolatedStorefrontService");
            owner.SetActive(false); // Never start a real store or load a player save.
            service = owner.AddComponent(T("Monetization.InAppPurchaseService"));
            Singleton(service.GetType(), service);
            Set(service, "EditorConnectOverride", (Action)(() => connections++));
            Set(service, "EditorFetchProductsOverride", (Action)(() => fetches++));
            Set(service, "EditorFetchPurchasesOverride", (Action)(() => pendingFetches++));
            Set(service, "EditorPurchaseReadinessOverride", (Func<bool>)(() => true));
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => string.Empty));
            Set(service, "EditorPurchaseProductOverride", (Action<string>)(id => purchases.Add(id)));
            Set(service, "EditorCatalogClockOverride", (Func<float>)(() => now));
        }

        [TearDown]
        public void Cleanup()
        {
            if (shop != null) Call(shop, "OnDisable");
            if (camera != null) camera.targetTexture = null;
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Singleton(T("Monetization.InAppPurchaseService"), oldService);
            Singleton(T("Managers.GameManager"), oldGame);
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }

        private void Connect()
        {
            Call(service, "EnsureProductsAvailable");
            Call(service, "HandleStoreConnected");
        }

        private IList Products(params int[] amounts)
        {
            IList result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Iap("Product")));
            foreach (int amount in amounts)
            {
                object definition = Activator.CreateInstance(Iap("ProductDefinition"), Prefix + amount,
                    Enum.Parse(Iap("ProductType"), "Consumable"));
                decimal price = amount == 120 ? 160m : amount == 650 ? 800m : amount == 2000 ? 2400m :
                    amount == 4200 ? 4800m : amount == 8600 ? 9600m : 16000m;
                object metadata = Activator.CreateInstance(Iap("ProductMetadata"), "¥" + price,
                    "Test product", "Test description", "JPY", price);
                result.Add(Activator.CreateInstance(Iap("Product"), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { definition, metadata, true }, null));
            }
            return result;
        }

        private void Fetched(params int[] amounts) => Call(service, "HandleProductsFetched", Products(amounts));

        private void FetchedPrice(string currency, decimal amount, string display)
        {
            IList result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Iap("Product")));
            object definition = Activator.CreateInstance(Iap("ProductDefinition"), Prefix + 120,
                Enum.Parse(Iap("ProductType"), "Consumable"));
            object metadata = Activator.CreateInstance(Iap("ProductMetadata"), display,
                "Test product", "Test description", currency, amount);
            result.Add(Activator.CreateInstance(Iap("Product"), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { definition, metadata, true }, null));
            Call(service, "HandleProductsFetched", result);
        }

        [TestCase("JPY", "160", "¥160", "160円")]
        [TestCase("JPY", "2400", "¥2,400", "2,400円")]
        [TestCase("JPY", "180", "￥180", "180円")]
        [TestCase("USD", "0.99", "$0.99", "$0.99")]
        [TestCase("EUR", "1.09", "1,09 €", "1,09 €")]
        public void PriceUsesStoreCurrencyAndAmountNotHardcodedJapanesePrice(string currency, string amount, string display, string expected)
        {
            Connect();
            FetchedPrice(currency, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), display);
            Assert.That(Call(service, "GetLocalizedPrice", Prefix + 120, "価格を確認"), Is.EqualTo(expected));
        }

        [Test]
        public void FocusRefreshesCompleteCatalogAndReplacesOldDollarPrice()
        {
            Connect(); Fetched(Amounts); FetchedPrice("USD", 0.99m, "$0.99");
            Call(service, "OnApplicationFocus", true);
            Assert.That(fetches, Is.EqualTo(2));
            Assert.That(Available(120), Is.False);
            Assert.That(Call(service, "GetLocalizedPrice", Prefix + 120, "価格を確認"), Is.EqualTo("価格を確認"));
            Fetched(Amounts);
            Assert.That(Call(service, "GetLocalizedPrice", Prefix + 120, "価格を確認"), Is.EqualTo("160円"));
            Assert.That(purchases, Is.Empty);
        }

        [Test]
        public void RefreshWaitsForPurchaseToFinishAndNeverRepeatsPurchase()
        {
            Connect(); Fetched(Amounts);
            Call(service, "Purchase", Prefix + 120);
            Call(service, "OnApplicationFocus", true);
            Assert.That(fetches, Is.EqualTo(1));
            Call(service, "HandlePurchaseFailed", new object[] { null });
            Call(service, "UpdateLocalizedPriceRefresh");
            Assert.That(fetches, Is.EqualTo(2));
            Assert.That(Available(120), Is.False);
            Fetched(Amounts);
            Assert.That(purchases, Is.EqualTo(new[] { Prefix + 120 }));
        }

        [Test]
        public void ShopOpenRefreshesCachedCatalogWithoutOverlappingRequests()
        {
            Connect(); Fetched(Amounts);
            BuildShop();
            Assert.That(fetches, Is.EqualTo(2));
            Call(service, "RefreshLocalizedPrices");
            Call(service, "RefreshLocalizedPrices");
            Assert.That(fetches, Is.EqualTo(3));
            Assert.That(purchases, Is.Empty);
        }
        private void Failed() => Call(service, "HandleProductsFetchFailed", new object[] { null });
        private void Tick(float seconds)
        {
            now += seconds;
            Call(service, "UpdateCatalogRecovery");
        }

        private void BuildShop()
        {
            bool hadCatalog = (bool)Prop(service, "AreAllProductsReady");
            var root = new GameObject("StorefrontCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
            camera = new GameObject("StorefrontTestCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2341 / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            target = new RenderTexture(1179, 2556, 24);
            camera.targetTexture = target;
            canvas.worldCamera = camera;
            events = new GameObject("StorefrontEvents", typeof(EventSystem)).GetComponent<EventSystem>();
            var panel = new GameObject("PaidShop", typeof(RectTransform));
            panel.SetActive(false);
            panel.transform.SetParent(root.transform, false);
            shop = panel.AddComponent(T("Home.PaidShopPanelController"));
            ((Behaviour)shop).enabled = false;
            Call(shop, "Build");
            Call(shop, "OpenCrystalShop");
            panel.SetActive(true);
            Call(shop, "OnEnable");
            if (hadCatalog) Fetched(Amounts); // Complete the shop-open price refresh.
            Canvas.ForceUpdateCanvases();
            camera.Render();
        }

        private Button Card(int amount) => (Button)((IDictionary)Field(shop, "crystalPurchaseButtons"))[Prefix + amount];
        private Text Price(int amount) => (Text)((IDictionary)Field(shop, "crystalPriceLabels"))[Prefix + amount];

        private void Tap(RectTransform area, Button expectedHandler)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, area.TransformPoint(area.rect.center));
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty, area.name + " must have a real UI raycast target.");
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.EqualTo(expectedHandler.gameObject), "The entire visible card must reach one purchase handler.");
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        [Test]
        public void LoadingCardExplainsStatusAndNeverQueuesAnAutomaticPurchase()
        {
            BuildShop();
            Assert.That(connections, Is.EqualTo(1));
            Assert.That(Price(120).text, Does.Not.Contain("160"));
            Assert.That(((Text)Field(shop, "crystalStoreStatusText")).text, Does.Contain("接続"));
            Tap((RectTransform)Card(120).transform, Card(120));
            Assert.That(((Text)Field(shop, "messageText")).text, Does.Contain("取得後"));
            Assert.That(connections, Is.EqualTo(1), "Loading taps must not duplicate connection requests.");
            Call(service, "HandleStoreConnected");
            Fetched(Amounts);
            Assert.That(purchases, Is.Empty, "Product arrival is not permission to purchase.");
            Assert.That(Price(120).text, Is.EqualTo("160円 で購入"));
            Assert.That(((Button)Field(shop, "crystalRetryButton")).gameObject.activeSelf, Is.False);
        }

        [TestCase("StoneIcon")]
        [TestCase("ProductName")]
        [TestCase("Contents")]
        [TestCase("BuyButton/Label")]
        public void IconNameContentsAndPriceEachReachTheSingleCardButton(string child)
        {
            Connect(); Fetched(Amounts); BuildShop();
            Button card = Card(120);
            Assert.That(card.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
            Tap((RectTransform)card.transform.Find(child), card);
            Assert.That(purchases, Is.EqualTo(new[] { Prefix + "120" }));
            Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.True);
            Tap((RectTransform)card.transform, card);
            Assert.That(purchases.Count, Is.EqualTo(1), "Repeated taps during a purchase must not submit again.");
        }

        [TestCase(120)] [TestCase(650)] [TestCase(2000)]
        [TestCase(4200)] [TestCase(8600)] [TestCase(15000)]
        public void EveryCardStartsExactlyItsOwnStoreProduct(int amount)
        {
            Connect(); Fetched(Amounts); BuildShop();
            Tap((RectTransform)Card(amount).transform, Card(amount));
            Assert.That(purchases, Is.EqualTo(new[] { Prefix + amount }));
        }

        [Test]
        public void BlockedCheckoutStopsBeforeReadinessNetworkingOrStoreSideEffects()
        {
            Connect(); Fetched(Amounts);
            int readinessChecks = 0;
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => CheckoutBlockedReason));
            Set(service, "EditorPurchaseReadinessOverride", (Func<bool>)(() => { readinessChecks++; return false; }));
            object onlineBefore = T("Save.OnlinePlayerData").GetProperty("Instance").GetValue(null);
            string catalogBefore = Status;
            int fetchesBefore = fetches, pendingBefore = pendingFetches;
            var failures = new List<string>();
            Action<string> failure = failures.Add;
            var failed = service.GetType().GetEvent("PurchaseFailed");
            failed.AddEventHandler(null, failure);
            try
            {
                Call(service, "Purchase", Prefix + 120);
                Assert.That(failures, Is.EqualTo(new[] { CheckoutBlockedReason }));
                Assert.That(readinessChecks, Is.Zero);
                Assert.That(purchases, Is.Empty);
                Assert.That(T("Save.OnlinePlayerData").GetProperty("Instance").GetValue(null), Is.SameAs(onlineBefore));
                Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.False);
                Assert.That(fetches, Is.EqualTo(fetchesBefore));
                Assert.That(pendingFetches, Is.EqualTo(pendingBefore));
                Assert.That(Status, Is.EqualTo(catalogBefore), "The checkout warning must not replace catalog readiness/error state.");
                Assert.That(Amounts.All(Available), Is.True);
            }
            finally { failed.RemoveEventHandler(null, failure); }
        }

        [Test]
        public void BlockedStorefrontShowsPricesAndReasonAndRejectsDirectHandlerWithoutStartingPurchase()
        {
            Connect(); Fetched(Amounts);
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => CheckoutBlockedReason));
            BuildShop();
            Assert.That(((Text)Field(shop, "crystalStoreStatusText")).text, Is.EqualTo(CheckoutBlockedReason));
            foreach (int amount in Amounts)
            {
                Assert.That(Card(amount).interactable, Is.False);
                Assert.That(Price(amount).text, Is.EqualTo(Call(service, "GetLocalizedPrice", Prefix + amount, "価格を確認")
                    + "\n検証版では購入不可"));
                Tap((RectTransform)Card(amount).transform, Card(amount));
            }
            int fetchesBefore = fetches;
            Call(shop, "PurchaseCrystalProduct", Prefix + 120, "Synthetic product");
            Assert.That(((Text)Field(shop, "messageText")).text, Is.Empty, "The complete reason belongs only in StoreStatus.");
            Call(shop, "ShowPurchaseMessage", "前回の結果", false);
            Call(service, "Purchase", Prefix + 120);
            Assert.That(((Text)Field(shop, "messageText")).text, Is.Empty, "Direct service rejection must not duplicate the status warning.");
            Assert.That(((Text)Field(shop, "crystalStoreStatusText")).text, Is.EqualTo(CheckoutBlockedReason));
            Assert.That(purchases, Is.Empty);
            Assert.That(fetches, Is.EqualTo(fetchesBefore));
            Assert.That(Amounts.All(Available), Is.True);
            Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.False);
        }

        [Test]
        public void EnvironmentPreflightWarningIsNotMislabelledAsADevelopmentBuild()
        {
            Connect(); Fetched(Amounts);
            string reason = (string)T("Monetization.IapPurchaseEnvironmentPolicy")
                .GetField("PurchaseCapabilityUnavailableMessage").GetRawConstantValue();
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => reason));
            BuildShop();
            Assert.That(((Text)Field(shop, "crystalStoreStatusText")).text, Is.EqualTo(reason));
            foreach (int amount in Amounts)
            {
                Assert.That(Card(amount).interactable, Is.False);
                Assert.That(Price(amount).text, Does.EndWith("\n現在は購入不可"));
                Assert.That(Price(amount).text, Does.Not.Contain("検証版"));
            }
            Assert.That(purchases, Is.Empty);
            Assert.That(Amounts.All(Available), Is.True);
        }

        [Test]
        public void CheckoutPolicyIsRecheckedAfterUiEventsBeforeStorePurchase()
        {
            Connect(); Fetched(Amounts);
            string reason = (string)T("Monetization.IapPurchaseEnvironmentPolicy")
                .GetField("PurchaseEnvironmentMismatchMessage").GetRawConstantValue();
            string blocked = string.Empty;
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => blocked));
            Action changed = () => blocked = reason;
            var updated = service.GetType().GetEvent("ProductsUpdated");
            var failed = service.GetType().GetEvent("PurchaseFailed");
            var failures = new List<string>();
            Action<string> failure = failures.Add;
            updated.AddEventHandler(null, changed);
            failed.AddEventHandler(null, failure);
            try
            {
                Call(service, "Purchase", Prefix + 120);
                Assert.That(purchases, Is.Empty);
                Assert.That(failures, Is.EqualTo(new[] { reason }));
                Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.False);
            }
            finally
            {
                updated.RemoveEventHandler(null, changed);
                failed.RemoveEventHandler(null, failure);
            }
        }

        [Test]
        public void BlockedStorefrontRetainsCatalogRetryAndPendingLookup()
        {
            Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => CheckoutBlockedReason));
            Connect(); BuildShop(); Failed();
            Button retry = (Button)Field(shop, "crystalRetryButton");
            Assert.That(retry.gameObject.activeSelf && retry.interactable, Is.True);
            Tap((RectTransform)retry.transform, retry);
            Assert.That(fetches, Is.EqualTo(2));
            Fetched(Amounts);
            Assert.That(pendingFetches, Is.EqualTo(1));
            Assert.That(Amounts.All(Available), Is.True);
            Assert.That(Amounts.All(a => !Card(a).interactable), Is.True);
            Assert.That(((Text)Field(shop, "crystalStoreStatusText")).text, Is.EqualTo(CheckoutBlockedReason));
            Assert.That(retry.gameObject.activeSelf, Is.False);
            Assert.That(purchases, Is.Empty);
        }

        private static void FinishCardTransition(Button card, string state)
        {
            // Inspect the actual final CanvasRenderer tint, not just Image.color:
            // Selectable's disabled ColorTint changes renderer alpha to 128/255.
            Type stateType = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
            typeof(Selectable).GetMethod("DoStateTransition", Hidden).Invoke(card,
                new[] { Enum.Parse(stateType, state), (object)true });
        }

        [Test]
        public void PurchaseWaitKeepsAllCardsOpaqueAndStationaryWhileBlockingRepeatTaps()
        {
            Connect(); Fetched(Amounts); BuildShop();
            var graphics = Amounts.SelectMany(a => Card(a).GetComponentsInChildren<Graphic>()).ToArray();
            var colors = graphics.Select(g => g.canvasRenderer.GetColor()).ToArray();
            var positions = graphics.Select(g => g.rectTransform.anchoredPosition).ToArray();
            var sizes = graphics.Select(g => g.rectTransform.sizeDelta).ToArray();
            var prices = Amounts.Select(a => Price(a).text).ToArray();
            Tap((RectTransform)Card(120).transform, Card(120));
            foreach (int amount in Amounts)
            {
                Assert.That(Card(amount).interactable, Is.False);
                FinishCardTransition(Card(amount), "Disabled");
            }
            for (int i = 0; i < graphics.Length; i++)
            {
                Assert.That(graphics[i].canvasRenderer.GetColor(), Is.EqualTo(colors[i]),
                    graphics[i].name + " must not fade or expose the background while StoreKit opens.");
                Assert.That(graphics[i].rectTransform.anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(graphics[i].rectTransform.sizeDelta, Is.EqualTo(sizes[i]));
            }
            Assert.That(Amounts.Select(a => Price(a).text), Is.EqualTo(prices));
            foreach (int amount in Amounts) Tap((RectTransform)Card(amount).transform, Card(amount));
            Assert.That(purchases, Is.EqualTo(new[] { Prefix + "120" }));
            Call(service, "HandlePurchaseFailed", new object[] { null });
            foreach (int amount in Amounts)
            {
                Assert.That(Card(amount).interactable, Is.True);
                FinishCardTransition(Card(amount), "Normal");
            }
            for (int i = 0; i < graphics.Length; i++)
                Assert.That(graphics[i].canvasRenderer.GetColor(), Is.EqualTo(colors[i]));
        }

        [Test]
        public void PurchaseWaitUsesOnlyTheExistingStatusLineAndStillShowsFailures()
        {
            Connect(); Fetched(Amounts); BuildShop();
            Text status = (Text)Field(shop, "crystalStoreStatusText");
            Text message = (Text)Field(shop, "messageText");
            Vector2 statusPosition = status.rectTransform.anchoredPosition;
            Vector2 messagePosition = message.rectTransform.anchoredPosition;
            Call(shop, "ShowPurchaseMessage", "前回のエラーメッセージ", false);
            Tap((RectTransform)Card(120).transform, Card(120));
            Assert.That(message.text, Is.Empty, "Opening StoreKit must not add a second, flashing wait message.");
            Assert.That(status.text, Does.Contain("購入確認画面"));
            Assert.That(status.rectTransform.anchoredPosition, Is.EqualTo(statusPosition));
            Assert.That(message.rectTransform.anchoredPosition, Is.EqualTo(messagePosition));
            Call(service, "HandlePurchaseFailed", new object[] { null });
            Assert.That(message.text, Does.Contain("購入が完了しませんでした"));
        }

        [Test]
        public void ProductFailureRetriesWithBackoffAndStopsAfterThreeAutomaticRetries()
        {
            Connect(); Failed();
            Assert.That(fetches, Is.EqualTo(1));
            Assert.That((bool)Prop(service, "IsStoreReady"), Is.False);
            Tick(4); Assert.That(fetches, Is.EqualTo(1));
            Tick(1); Assert.That(fetches, Is.EqualTo(2)); Failed();
            Tick(10); Assert.That(fetches, Is.EqualTo(3)); Failed();
            Tick(20); Assert.That(fetches, Is.EqualTo(4)); Failed();
            Tick(1000); Assert.That(fetches, Is.EqualTo(4));
            Assert.That(Status, Does.Contain("商品情報を再取得"));
            Call(service, "RefreshProducts");
            Assert.That(fetches, Is.EqualTo(5), "Manual retry remains possible after automatic retries stop.");
        }

        [Test]
        public void ManualRetryAndForegroundCannotOverlapAnOutstandingRequest()
        {
            Connect();
            for (int i = 0; i < 5; i++)
            {
                Call(service, "RefreshProducts");
                Call(service, "OnApplicationFocus", true);
            }
            Assert.That(connections, Is.EqualTo(1));
            Assert.That(fetches, Is.EqualTo(1));
            Tick(31);
            Assert.That(Status, Does.Contain("時間がかかっています"));
            Assert.That((bool)Prop(service, "CanRetryProducts"), Is.False);
            Call(service, "RefreshProducts");
            Assert.That(fetches, Is.EqualTo(1));
            Fetched(Amounts);
            Assert.That((bool)Prop(service, "AreAllProductsReady"), Is.True, "A late response must still recover.");
            Assert.That((bool)Prop(service, "HasStoreError"), Is.False);
        }

        [Test]
        public void SandboxSignInForegroundReRequestsProductsAfterFailure()
        {
            Connect(); Failed();
            Call(service, "OnApplicationFocus", false);
            Assert.That(fetches, Is.EqualTo(1));
            Call(service, "OnApplicationFocus", true);
            Assert.That(fetches, Is.EqualTo(2));
            Fetched(Amounts);
            Assert.That(pendingFetches, Is.EqualTo(1));
            Assert.That(purchases, Is.Empty);
        }

        [Test]
        public void PartialSuccessDoesNotDisableValidProductsOrEraseThemOnRetry()
        {
            Connect(); Fetched(120); Failed();
            Assert.That(Available(120), Is.True);
            Assert.That(Available(650), Is.False);
            Assert.That((int)Field(service, "catalogRetryCount"), Is.EqualTo(1));
            Call(service, "RefreshProducts");
            Fetched(650, 2000, 4200, 8600, 15000);
            Assert.That(Amounts.All(Available), Is.True);
            Assert.That((bool)Prop(service, "AreAllProductsReady"), Is.True);
        }

        [Test]
        public void DisconnectClearsStalePricesAndReconnectsBeforeFetchingAgain()
        {
            Connect(); Fetched(Amounts);
            Call(service, "HandleStoreDisconnected", new object[] { null });
            Assert.That(Available(120), Is.False);
            Fetched(Amounts); // Ignore a stale callback from the disconnected session.
            Assert.That(Available(120), Is.False);
            Tick(5); Assert.That(connections, Is.EqualTo(2));
            Call(service, "HandleStoreConnected");
            Assert.That(fetches, Is.EqualTo(2));
            Fetched(Amounts);
            Assert.That(Available(120), Is.True);
        }

        [Test]
        public void SynchronousConnectionFailurePublishesRecoverableStatus()
        {
            Set(service, "EditorConnectOverride", (Action)(() => throw new InvalidOperationException(SensitiveMarker)));
            AssertSafeDiagnostic(() => Call(service, "EnsureProductsAvailable"), LogType.Error,
                "[IAP] Event=StoreConnectionFailed; Exception=InvalidOperationException");
            Assert.That((bool)Prop(service, "CanRetryProducts"), Is.True);
            Assert.That(Status, Does.Contain("接続できません"));
            Set(service, "EditorConnectOverride", (Action)(() => connections++));
            Tick(5); Assert.That(connections, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousProductFailureCanBeRetriedWithoutGettingStuckLoading()
        {
            Set(service, "EditorFetchProductsOverride", (Action)(() => throw new InvalidOperationException(SensitiveMarker)));
            AssertSafeDiagnostic(Connect, LogType.Warning,
                "[IAP] Event=ProductRequestStartFailed; Exception=InvalidOperationException");
            Assert.That((bool)Prop(service, "CanRetryProducts"), Is.True);
            Set(service, "EditorFetchProductsOverride", (Action)(() => fetches++));
            Tick(5); Assert.That(fetches, Is.EqualTo(1));
        }

        [Test]
        public void UnknownExceptionDiagnosticNeverReadsMessageOrStringifiesException()
        {
            Set(service, "EditorConnectOverride", (Action)(() => throw new UntrustedDiagnosticException()));
            AssertSafeDiagnostic(() => Call(service, "EnsureProductsAvailable"), LogType.Error,
                "[IAP] Event=StoreConnectionFailed; Exception=OtherException");
            Assert.That((bool)Prop(service, "CanRetryProducts"), Is.True);
        }

        [Test]
        public void PurchaseStartExceptionUsesOnlySafeClassificationAndLeavesRecoveryAvailable()
        {
            Connect(); Fetched(Amounts);
            Set(service, "EditorPurchaseProductOverride", (Action<string>)(_ => throw new InvalidOperationException(SensitiveMarker)));
            AssertSafeDiagnostic(() => Call(service, "Purchase", Prefix + 120), LogType.Warning,
                "[IAP] Event=PurchaseStartFailed; Exception=InvalidOperationException");
            Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.False);
            Assert.That(purchases, Is.Empty);
        }

        [Test]
        public void PendingLookupExceptionUsesOnlySafeClassificationAndSchedulesRetry()
        {
            Connect();
            Set(service, "EditorFetchPurchasesOverride", (Action)(() => throw new System.IO.IOException(SensitiveMarker)));
            AssertSafeDiagnostic(() => Fetched(Amounts), LogType.Warning,
                "[IAP] Event=PendingPurchaseLookupFailed; Exception=IOException");
            Assert.That((bool)Field(service, "purchaseFetchInProgress"), Is.False);
            Assert.That(float.IsPositiveInfinity((float)Field(service, "nextPurchaseFetchAt")), Is.False);
        }

        [Test]
        public void StoreDisconnectionNeverLogsProviderMessage()
        {
            Connect(); Fetched(Amounts);
            object description = Activator.CreateInstance(Iap("StoreConnectionFailureDescription"), SensitiveMarker, false);
            AssertSafeDiagnostic(() => Call(service, "HandleStoreDisconnected", description), LogType.Warning,
                "[IAP] Event=StoreDisconnected");
            Assert.That(Available(120), Is.False);
            Assert.That(Status, Does.Contain("接続が切れました"));
        }

        [Test]
        public void ProductFetchFailureNeverLogsProviderReason()
        {
            Connect();
            object definitions = Activator.CreateInstance(typeof(List<>).MakeGenericType(Iap("ProductDefinition")));
            object failure = Activator.CreateInstance(Iap("ProductFetchFailed"), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { definitions, SensitiveMarker }, null);
            AssertSafeDiagnostic(() => Call(service, "HandleProductsFetchFailed", failure), LogType.Warning,
                "[IAP] Event=ProductFetchFailed");
            Assert.That((bool)Prop(service, "CanRetryProducts"), Is.True);
        }

        [Test]
        public void PendingLookupFailureNeverStringifiesProviderDescription()
        {
            object failure = Activator.CreateInstance(Iap("PurchasesFetchFailureDescription"),
                Enum.Parse(Iap("PurchasesFetchFailureReason"), "Unknown"), SensitiveMarker);
            Set(service, "purchaseFetchInProgress", true);
            AssertSafeDiagnostic(() => Call(service, "HandlePurchasesFetchFailed", failure), LogType.Warning,
                "[IAP] Event=PendingPurchaseLookupFailed");
            Assert.That((bool)Field(service, "purchaseFetchInProgress"), Is.False);
            Assert.That(float.IsPositiveInfinity((float)Field(service, "nextPurchaseFetchAt")), Is.False);
        }

        [TestCase("HandlePurchaseFailed", LogType.Warning, "PurchaseFailed")]
        [TestCase("HandlePurchaseConfirmed", LogType.Error, "PurchaseConfirmationFailed")]
        public void FailedOrderDetailsReceiptAndTransactionNeverReachDiagnostics(string callback, LogType level, string diagnostic)
        {
            // Empty carts and synthetic OrderInfo never start a store request.
            object items = Activator.CreateInstance(typeof(List<>).MakeGenericType(Iap("CartItem")));
            object cart = Activator.CreateInstance(Iap("Cart"), items);
            object info = Activator.CreateInstance(Iap("OrderInfo"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { SensitiveMarker + "-receipt", SensitiveMarker + "-transaction", "SyntheticStore" }, null);
            object pending = Iap("PendingOrder").GetConstructor(new[] { Iap("ICart"), Iap("IOrderInfo") })
                .Invoke(new[] { cart, info });
            object failure = Iap("FailedOrder").GetConstructor(new[] { Iap("Order"), Iap("PurchaseFailureReason"), typeof(string) })
                .Invoke(new[] { pending, Enum.Parse(Iap("PurchaseFailureReason"), "Unknown"), SensitiveMarker + "-details" });
            var confirmations = (HashSet<string>)Field(service, "confirmationsInFlight");
            confirmations.Add(SensitiveMarker + "-transaction");
            Set(service, "purchaseInProgress", true);
            AssertSafeDiagnostic(() => Call(service, callback, failure), level, "[IAP] Event=" + diagnostic);
            if (callback == "HandlePurchaseConfirmed")
            {
                Assert.That(confirmations, Does.Not.Contain(SensitiveMarker + "-transaction"));
                Assert.That((HashSet<string>)Field(service, "failedConfirmations"), Does.Contain(SensitiveMarker + "-transaction"));
            }
            else Assert.That((bool)Prop(service, "IsPurchaseInProgress"), Is.False);
        }

        [Test]
        public void RetryButtonOnlyLoadsProductsAndPurchaseCancelReenablesCards()
        {
            Connect(); BuildShop(); Failed();
            Button retry = (Button)Field(shop, "crystalRetryButton");
            Assert.That(retry.interactable, Is.True);
            Assert.That(Price(120).text, Does.Contain("未取得"));
            Tap((RectTransform)retry.transform, retry);
            Assert.That(fetches, Is.EqualTo(2));
            Assert.That(retry.interactable, Is.False);
            Fetched(Amounts);
            Assert.That(purchases, Is.Empty);
            Tap((RectTransform)Card(120).transform, Card(120));
            Assert.That(Amounts.All(a => !Card(a).interactable), Is.True);
            Call(service, "HandlePurchaseFailed", new object[] { null });
            Assert.That(Amounts.All(a => Card(a).interactable), Is.True);
            Assert.That(((Text)Field(shop, "messageText")).text, Does.Contain("購入が完了しませんでした"));
        }

        [TestCase("loading")]
        [TestCase("failed")]
        [TestCase("partial")]
        [TestCase("slow")]
        [TestCase("ready")]
        [TestCase("purchasing")]
        [TestCase("blocked")]
        public void StorefrontStatusAndControlsFitWithoutOverlap(string state)
        {
            Connect(); BuildShop();
            if (state == "failed") Failed();
            if (state == "partial") { Fetched(120); Failed(); }
            if (state == "slow") Tick(31);
            if (state == "ready") Fetched(Amounts);
            if (state == "blocked")
            {
                Set(service, "EditorNewPurchaseBlockedMessageOverride", (Func<string>)(() => CheckoutBlockedReason));
                Fetched(Amounts);
            }
            if (state == "purchasing")
            {
                Fetched(Amounts);
                Tap((RectTransform)Card(120).transform, Card(120));
                foreach (int amount in Amounts) FinishCardTransition(Card(amount), "Disabled");
            }
            Canvas.ForceUpdateCanvases();
            Text status = (Text)Field(shop, "crystalStoreStatusText");
            Text message = (Text)Field(shop, "messageText");
            RectTransform retry = ((Button)Field(shop, "crystalRetryButton")).GetComponent<RectTransform>();
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1), status.text);
            RectTransform root = (RectTransform)canvas.transform;
            Rect Bounds(RectTransform rect)
            {
                UnityEngine.Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, rect);
                return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            }
            Assert.That(Bounds(status.rectTransform).Overlaps(Bounds(message.rectTransform)), Is.False);
            Assert.That(Bounds(message.rectTransform).Overlaps(Bounds(retry)), Is.False);
            Assert.That(Bounds(retry).yMin, Is.GreaterThan(root.rect.yMin));
            Assert.That(retry.rect.height, Is.GreaterThanOrEqualTo(128));
            foreach (int amount in Amounts)
            {
                Button card = Card(amount);
                Assert.That(((RectTransform)card.transform).rect.size, Is.EqualTo(new Vector2(420, 330)));
                Text contents = card.transform.Find("Contents").GetComponent<Text>();
                Text price = Price(amount);
                Assert.That(contents.fontSize, Is.GreaterThanOrEqualTo(32));
                Assert.That(price.fontSize, Is.GreaterThanOrEqualTo(34));
                foreach (Text label in new[] { contents, price })
                {
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), label.text);
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1), label.text);
                    Rect cardBounds = Bounds((RectTransform)card.transform);
                    Rect labelBounds = Bounds(label.rectTransform);
                    Assert.That(labelBounds.xMin, Is.GreaterThanOrEqualTo(cardBounds.xMin + 20));
                    Assert.That(labelBounds.xMax, Is.LessThanOrEqualTo(cardBounds.xMax - 20));
                    Assert.That(labelBounds.yMin, Is.GreaterThanOrEqualTo(cardBounds.yMin + 20));
                }
                Assert.That(Bounds(contents.rectTransform).Overlaps(Bounds(price.rectTransform)), Is.False);
                if (state == "blocked")
                    Assert.That(Bounds(contents.rectTransform).yMin - Bounds(price.rectTransform).yMax,
                        Is.GreaterThanOrEqualTo(4), "The two-line checkout warning needs a visible gap below Contents.");
                foreach (int other in Amounts.Where(a => a != amount))
                    Assert.That(Bounds((RectTransform)card.transform).Overlaps(Bounds((RectTransform)Card(other).transform)), Is.False);
                Assert.That(card.GetComponentsInChildren<Graphic>().Where(g => g.gameObject != card.gameObject)
                    .All(g => !g.raycastTarget), Is.True);
            }
            string directory = Environment.GetEnvironmentVariable("WITCHTOWER_STOREFRONT_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(directory))
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { root, 1179, 2556, System.IO.Path.Combine(directory, state + ".png") });
        }
    }
}
