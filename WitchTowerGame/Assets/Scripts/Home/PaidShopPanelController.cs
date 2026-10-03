using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Monetization;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class PaidShopPanelController : MonoBehaviour
    {
        private enum ShopCategory
        {
            Crystal,
            PremiumItem,
            PermanentUpgrade
        }

        private const string PaidStoneIconPath = "UI/GachaPage/GachaStonePaidIcon";
        private const string PremiumSupportPackIconPath = "UI/PaidShop/PremiumSupportPackIcon";
        private const string EquipmentProtectionCharmIconPath = "UI/PaidShop/EquipmentProtectionCharmIcon";
        private const string PremiumItemIconPath = PremiumSupportPackIconPath;
        private const string PermanentUpgradeIconPath = "UI/EquipmentEnhance/EnhanceRuneCircle";
        private const string HeadingFramePath = "UI/AudioSettings/SettingsPanelFrameImage2";
        private const float PermanentUpgradeFrameTop = -164f;
        private const int AutoRepeatFloorUpgradeCost = 1200;
        private const int AutoSellEquipmentUpgradeCost = 1200;
        private const int AutoReleaseMonsterUpgradeCost = 1200;
        private const int MonsterStorageUpgradeCost = 1500;
        private const int MonsterStorageUpgradeAmount = 20;
        private const int EquipmentStorageUpgradeCost = 1500;
        private const int EquipmentStorageUpgradeAmount = 20;
        private const float DefaultButtonLabelYOffset = -12f;
        private const float PriceButtonLabelYOffset = DefaultButtonLabelYOffset + 3f;
        private static readonly Color PageTint = new Color(0.008f, 0.006f, 0.018f, 0.97f);
        private static readonly Color PanelColor = new Color(0.045f, 0.025f, 0.075f, 0.98f);
        private static readonly Color CardColor = new Color(0.035f, 0.045f, 0.075f, 0.98f);
        private static readonly Color AccentGold = new Color(1f, 0.78f, 0.30f, 1f);
        private static readonly Color AccentCrystal = new Color(0.72f, 0.50f, 1f, 1f);
        private static readonly Color TextMain = new Color(1f, 0.98f, 0.92f, 1f);
        private static readonly Color TextSub = new Color(0.78f, 0.82f, 0.94f, 1f);
        private static readonly Color PurchaseButtonColor = new Color(0.62f, 0.30f, 0.035f, 1f);
        private static readonly Color EnableButtonColor = new Color(0.025f, 0.39f, 0.57f, 1f);
        private static readonly Color DisableButtonColor = new Color(0.61f, 0.20f, 0.08f, 1f);

        private Action onClosed;
        private Font runtimeFont;
        private GameObject selectorRoot;
        private GameObject categoryRoot;
        private Text paidStoneBalanceText;
        private Text categoryBalanceText;
        private Text messageText;
        private Text crystalStoreStatusText;
        private Button crystalRetryButton;
        private Text activePermanentUpgradeStatusText;
        private Button autoRepeatFloorUpgradeButton;
        private Text autoRepeatFloorUpgradeButtonText;
        private Button autoSellEquipmentUpgradeButton;
        private Text autoSellEquipmentUpgradeButtonText;
        private Button autoReleaseMonsterUpgradeButton;
        private Text autoReleaseMonsterUpgradeButtonText;
        private Button monsterStorageUpgradeButton;
        private Text monsterStorageUpgradeButtonText;
        private Button equipmentStorageUpgradeButton;
        private Text equipmentStorageUpgradeButtonText;
        private readonly Dictionary<string, Button> crystalPurchaseButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Text> crystalPriceLabels =
            new Dictionary<string, Text>(StringComparer.Ordinal);
        private bool isBuilt;
        private GameObject purchaseConfirmationRoot;
        private GameObject upgradeTutorialRoot;
        private string pendingUpgradeTutorial;
        private UnityEngine.Events.UnityAction pendingPermanentPurchase;

        private void OnEnable()
        {
            InAppPurchaseService.ProductsUpdated += RefreshCrystalProductPrices;
            InAppPurchaseService.PurchaseSucceeded += HandleStorePurchaseSucceeded;
            InAppPurchaseService.PurchaseFailed += HandleStorePurchaseFailed;
            RefreshCrystalProductPrices();
        }

        private void OnDisable()
        {
            if (upgradeTutorialRoot != null)
            {
                upgradeTutorialRoot.SetActive(false);
                if (Application.isPlaying) Destroy(upgradeTutorialRoot); else DestroyImmediate(upgradeTutorialRoot);
                upgradeTutorialRoot = null;
            }
            ClosePermanentPurchaseConfirmation();
            InAppPurchaseService.ProductsUpdated -= RefreshCrystalProductPrices;
            InAppPurchaseService.PurchaseSucceeded -= HandleStorePurchaseSucceeded;
            InAppPurchaseService.PurchaseFailed -= HandleStorePurchaseFailed;
        }

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            if (!MonetizationFeatureFlags.StorefrontEnabled)
            {
                gameObject.SetActive(false);
                onClosed?.Invoke();
                return;
            }

            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowSelector();
        }

        private void Refresh()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            string balance = profile != null
                ? $"所持有償宝晶  {profile.PaidGachaStones:N0}"
                : "所持有償宝晶  -";

            if (paidStoneBalanceText != null)
            {
                paidStoneBalanceText.text = balance;
            }

            if (categoryBalanceText != null)
            {
                categoryBalanceText.text = balance;
            }

            RefreshAutoRepeatFloorUpgradeButton(profile);
            RefreshAutoSellEquipmentUpgradeButton(profile);
            RefreshAutoReleaseMonsterUpgradeButton(profile);
            RefreshStorageUpgradeButton(monsterStorageUpgradeButton, monsterStorageUpgradeButtonText, profile, MonsterStorageUpgradeCost);
            RefreshStorageUpgradeButton(equipmentStorageUpgradeButton, equipmentStorageUpgradeButtonText, profile, EquipmentStorageUpgradeCost);

            if (activePermanentUpgradeStatusText != null)
            {
                string paidUnavailable = WitchTower.Save.OnlinePlayerData.PaidSpendingUnavailableMessage;
                activePermanentUpgradeStatusText.text = string.IsNullOrEmpty(paidUnavailable)
                    ? BuildPermanentUpgradeStatusText(profile) : paidUnavailable;
            }
        }

        private void Build()
        {
            ClearChildren(transform);
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform rootRect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlay = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            overlay.color = PageTint;
            overlay.raycastTarget = true;

            selectorRoot = CreateStretchRoot("PaidShopSelectorRoot", transform);
            categoryRoot = CreateStretchRoot("PaidShopCategoryRoot", transform);
            BuildSelector();
            categoryRoot.SetActive(false);
            isBuilt = true;
        }

        private void BuildSelector()
        {
            CreateFullScreenImage("SelectorBackground", selectorRoot.transform, "UI/FusionPage/FusionBackground");
            GameObject panel = CreatePanel("SelectorMainPanel", selectorRoot.transform, "UI/FusionPage/FusionMainFrame",
                Vector2.zero, new Vector2(1000f, 1710f), PanelColor);

            CreateText("Title", panel.transform, "ショップ選択", 52, FontStyle.Bold,
                new Vector2(0f, -52f), new Vector2(680f, 72f), AccentGold, TextAnchor.MiddleCenter);
            CreateText("Subtitle", panel.transform, "利用するショップを選んでください", 22, FontStyle.Bold,
                new Vector2(0f, -116f), new Vector2(760f, 42f), TextSub, TextAnchor.MiddleCenter);

            GameObject balance = CreatePanel("PaidStoneBalancePanel", panel.transform, null,
                new Vector2(0f, -180f), new Vector2(720f, 88f), new Color(0.025f, 0.035f, 0.065f, 0.96f));
            CreateIcon("PaidStoneIcon", balance.transform, PaidStoneIconPath, new Vector2(-222f, -11f), new Vector2(64f, 64f));
            paidStoneBalanceText = CreateText("PaidStoneBalance", balance.transform, string.Empty, 27, FontStyle.Bold,
                new Vector2(48f, -22f), new Vector2(470f, 48f), AccentCrystal, TextAnchor.MiddleCenter);

            CreateCategoryButton(panel.transform, ShopCategory.Crystal, "宝晶購入", "有償宝晶を購入する",
                PaidStoneIconPath, new Vector2(0f, -310f));
            CreateCategoryButton(panel.transform, ShopCategory.PermanentUpgrade, "永続強化", "冒険を恒久的に支援する効果",
                PermanentUpgradeIconPath, new Vector2(0f, -545f));

            HomeReturnButtonStyle.Create(selectorRoot.transform, "PaidShopCloseButton", Hide);
        }

        private void CreateCategoryButton(
            Transform parent,
            ShopCategory category,
            string title,
            string description,
            string iconPath,
            Vector2 position,
            float iconY = -42f)
        {
            GameObject card = CreatePanel("Category_" + category, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(820f, 205f), CardColor);
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;

            Button button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.onClick.AddListener(() => OpenCategory(category));

            CreateIcon("CategoryIcon", card.transform, iconPath, new Vector2(-300f, iconY), new Vector2(112f, 112f));
            CreateText("CategoryTitle", card.transform, title, 31, FontStyle.Bold,
                new Vector2(0f, -55f), new Vector2(540f, 48f), TextMain, TextAnchor.MiddleCenter);
            CreateText("CategoryDescription", card.transform, description, 20, FontStyle.Bold,
                new Vector2(0f, -108f), new Vector2(560f, 40f), TextSub, TextAnchor.MiddleCenter);
            CreateText("Arrow", card.transform, "›", 44, FontStyle.Bold,
                new Vector2(326f, -72f), new Vector2(70f, 62f), AccentGold, TextAnchor.MiddleCenter);
        }

        private void OpenCategory(ShopCategory category)
        {
            // Unreleased products must not be reachable through old callers either.
            if (category == ShopCategory.PremiumItem) { ShowSelector(); return; }
            ClosePermanentPurchaseConfirmation();
            ClearChildren(categoryRoot.transform);
            crystalStoreStatusText = null;
            crystalRetryButton = null;
            crystalPurchaseButtons.Clear();
            crystalPriceLabels.Clear();
            activePermanentUpgradeStatusText = null;
            autoRepeatFloorUpgradeButton = null;
            autoRepeatFloorUpgradeButtonText = null;
            autoSellEquipmentUpgradeButton = null;
            autoSellEquipmentUpgradeButtonText = null;
            autoReleaseMonsterUpgradeButton = null;
            autoReleaseMonsterUpgradeButtonText = null;
            monsterStorageUpgradeButton = null;
            monsterStorageUpgradeButtonText = null;
            equipmentStorageUpgradeButton = null;
            equipmentStorageUpgradeButtonText = null;
            selectorRoot.SetActive(false);
            categoryRoot.SetActive(true);

            switch (category)
            {
                case ShopCategory.Crystal:
                    BuildCrystalShop();
                    break;
                case ShopCategory.PremiumItem:
                    BuildStandardCategoryPage(
                        "高級アイテム",
                        "冒険を有利にする特別なアイテム",
                        new[]
                        {
                            ("冒険者支援パック", "戦闘支援アイテムの詰め合わせ", "宝晶 300", PremiumSupportPackIconPath),
                            ("装備保護符", "強化失敗時の装備消失を防ぐ", "宝晶 500", EquipmentProtectionCharmIconPath)
                        });
                    break;
                case ShopCategory.PermanentUpgrade:
                    BuildPermanentUpgradePage();
                    break;
            }

            Refresh();
        }

        public void OpenCrystalShop()
        {
            if (!MonetizationFeatureFlags.StorefrontEnabled)
            {
                return;
            }

            OpenCategory(ShopCategory.Crystal);
        }

        public void OpenPremiumItemShop()
        {
            if (!MonetizationFeatureFlags.StorefrontEnabled)
            {
                return;
            }

            OpenCategory(ShopCategory.PremiumItem);
        }

        public void OpenPermanentUpgradeShop()
        {
            if (!MonetizationFeatureFlags.StorefrontEnabled)
            {
                return;
            }

            OpenCategory(ShopCategory.PermanentUpgrade);
        }

        public void ShowPurchasedPermanentUpgradeList(Action closeCallback)
        {
            onClosed = closeCallback;
            OpenPurchasedPermanentUpgradeList();
        }

        public void OpenPurchasedPermanentUpgradeList()
        {
            ClosePermanentPurchaseConfirmation();
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClearChildren(categoryRoot.transform);
            categoryBalanceText = null;
            messageText = null;
            activePermanentUpgradeStatusText = null;
            autoRepeatFloorUpgradeButton = null;
            autoRepeatFloorUpgradeButtonText = null;
            autoSellEquipmentUpgradeButton = null;
            autoSellEquipmentUpgradeButtonText = null;
            autoReleaseMonsterUpgradeButton = null;
            autoReleaseMonsterUpgradeButtonText = null;
            monsterStorageUpgradeButton = null;
            monsterStorageUpgradeButtonText = null;
            equipmentStorageUpgradeButton = null;
            equipmentStorageUpgradeButtonText = null;
            selectorRoot.SetActive(false);
            categoryRoot.SetActive(true);
            BuildPurchasedPermanentUpgradeListPage();
            Refresh();
        }

        private void BuildCrystalShop()
        {
            crystalPurchaseButtons.Clear();
            crystalPriceLabels.Clear();
            GameObject panel = BuildCategoryShell("宝晶購入", "初回購入でホーム下部のバナー広告を削除");
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals120, "月光の小箱", "有償宝晶 120個", new Vector2(-225f, -310f));
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals650, "星導の宝箱", "有償宝晶 650個", new Vector2(225f, -310f));
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals2000, "契約炉の宝庫", "有償宝晶 2,000個", new Vector2(-225f, -660f));
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals4200, "深淵の宝庫", "有償宝晶 4,200個", new Vector2(225f, -660f));
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals8600, "星海の大宝庫", "有償宝晶 8,600個", new Vector2(-225f, -1010f));
            CreateCrystalProductCard(panel.transform, IapProductCatalog.Crystals15000, "天頂の大宝庫", "有償宝晶 15,000個", new Vector2(225f, -1010f));

            CreateText("Notice", panel.transform, "リワード広告は引き続き利用できます。価格はApp Storeから取得します。", 20, FontStyle.Bold,
                new Vector2(0f, -1355f), new Vector2(850f, 40f), TextSub, TextAnchor.MiddleCenter);
            crystalStoreStatusText = CreateText("StoreStatus", panel.transform, string.Empty, 25, FontStyle.Bold,
                new Vector2(0f, -1400f), new Vector2(880f, 96f), TextMain, TextAnchor.MiddleCenter);
            messageText = CreateText("Message", panel.transform, string.Empty, 24, FontStyle.Bold,
                new Vector2(0f, -1498f), new Vector2(880f, 64f), TextMain, TextAnchor.MiddleCenter);
            crystalRetryButton = CreateButton("RetryProductsButton", panel.transform, "商品情報を再取得",
                new Vector2(0f, -1574f), new Vector2(520f, 128f), RetryCrystalProducts, 0f);
            Text retryLabel = crystalRetryButton.GetComponentInChildren<Text>();
            retryLabel.fontSize = retryLabel.resizeTextMaxSize = 28;
            retryLabel.resizeTextMinSize = 26;
            InAppPurchaseService.Instance?.RefreshLocalizedPrices();
            RefreshCrystalProductPrices();
        }

        private void BuildStandardCategoryPage(
            string title,
            string subtitle,
            string iconPath,
            (string Name, string Description, string Price)[] products)
        {
            var productsWithIcons = new (string Name, string Description, string Price, string IconPath)[products.Length];
            for (int i = 0; i < products.Length; i += 1)
            {
                productsWithIcons[i] = (products[i].Name, products[i].Description, products[i].Price, iconPath);
            }

            BuildStandardCategoryPage(title, subtitle, productsWithIcons);
        }

        private void BuildStandardCategoryPage(
            string title,
            string subtitle,
            (string Name, string Description, string Price, string IconPath)[] products)
        {
            GameObject panel = BuildCategoryShell(title, subtitle);
            for (int i = 0; i < products.Length; i += 1)
            {
                CreateWideProductCard(panel.transform, products[i].Name, products[i].Description, products[i].Price,
                    products[i].IconPath, new Vector2(0f, -390f - i * 340f));
            }

            messageText = CreateText("Message", panel.transform, "商品を選んでください。", 21, FontStyle.Bold,
                new Vector2(0f, -1470f), new Vector2(820f, 60f), TextMain, TextAnchor.MiddleCenter);
        }

        private void BuildPermanentUpgradePage()
        {
            autoRepeatFloorUpgradeButton = null;
            autoRepeatFloorUpgradeButtonText = null;
            autoSellEquipmentUpgradeButton = null;
            autoSellEquipmentUpgradeButtonText = null;
            autoReleaseMonsterUpgradeButton = null;
            autoReleaseMonsterUpgradeButtonText = null;
            monsterStorageUpgradeButton = null;
            monsterStorageUpgradeButtonText = null;
            equipmentStorageUpgradeButton = null;
            equipmentStorageUpgradeButtonText = null;
            activePermanentUpgradeStatusText = null;

            GameObject panel = BuildCategoryShell("永続強化", "購入後、効果が永続する特別強化");
            activePermanentUpgradeStatusText = CreateText("ActivePermanentUpgradeStatus", panel.transform, string.Empty, 20, FontStyle.Bold,
                new Vector2(0f, -258f), new Vector2(780f, 42f), AccentCrystal, TextAnchor.MiddleCenter);
            Vector2 compactProductSize = new Vector2(820f, 248f);
            autoRepeatFloorUpgradeButton = CreateWideProductCard(
                panel.transform,
                "同階層オート再挑戦",
                "勝利・敗北後に同じ階層へ自動で再挑戦",
                FormatStonePrice(AutoRepeatFloorUpgradeCost),
                PermanentUpgradeIconPath,
                new Vector2(0f, -320f),
                compactProductSize,
                () => ShowPermanentPurchaseConfirmation("同階層オート再挑戦", AutoRepeatFloorUpgradeCost, PurchaseAutoRepeatFloorUpgrade));
            autoRepeatFloorUpgradeButtonText = autoRepeatFloorUpgradeButton != null
                ? autoRepeatFloorUpgradeButton.transform.Find("Label")?.GetComponent<Text>()
                : null;

            autoSellEquipmentUpgradeButton = CreateWideProductCard(
                panel.transform,
                "装備自動売却",
                "指定品質に満たない装備を自動で売却",
                FormatStonePrice(AutoSellEquipmentUpgradeCost),
                PermanentUpgradeIconPath,
                new Vector2(0f, -575f),
                compactProductSize,
                () => ShowPermanentPurchaseConfirmation("装備自動売却", AutoSellEquipmentUpgradeCost, PurchaseAutoSellEquipmentUpgrade));
            autoSellEquipmentUpgradeButtonText = autoSellEquipmentUpgradeButton != null
                ? autoSellEquipmentUpgradeButton.transform.Find("Label")?.GetComponent<Text>()
                : null;

            autoReleaseMonsterUpgradeButton = CreateWideProductCard(
                panel.transform,
                "モンスター自動逃がし",
                "指定IVに満たない捕獲モンスターを自動で逃がす",
                FormatStonePrice(AutoReleaseMonsterUpgradeCost),
                PermanentUpgradeIconPath,
                new Vector2(0f, -830f),
                compactProductSize,
                () => ShowPermanentPurchaseConfirmation("モンスター自動逃がし", AutoReleaseMonsterUpgradeCost, PurchaseAutoReleaseMonsterUpgrade));
            autoReleaseMonsterUpgradeButtonText = autoReleaseMonsterUpgradeButton != null
                ? autoReleaseMonsterUpgradeButton.transform.Find("Label")?.GetComponent<Text>()
                : null;

            monsterStorageUpgradeButton = CreateWideProductCard(panel.transform, $"モンスター枠 +{MonsterStorageUpgradeAmount}", "モンスター所持上限を恒久拡張",
                FormatStonePrice(MonsterStorageUpgradeCost), PermanentUpgradeIconPath, new Vector2(0f, -1085f), compactProductSize,
                () => ShowPermanentPurchaseConfirmation($"モンスター枠 +{MonsterStorageUpgradeAmount}", MonsterStorageUpgradeCost, PurchaseMonsterStorageUpgrade));
            monsterStorageUpgradeButtonText = monsterStorageUpgradeButton != null
                ? monsterStorageUpgradeButton.transform.Find("Label")?.GetComponent<Text>()
                : null;
            equipmentStorageUpgradeButton = CreateWideProductCard(panel.transform, $"装備枠 +{EquipmentStorageUpgradeAmount}", "装備所持上限を恒久拡張",
                FormatStonePrice(EquipmentStorageUpgradeCost), PermanentUpgradeIconPath, new Vector2(0f, -1340f), compactProductSize,
                () => ShowPermanentPurchaseConfirmation($"装備枠 +{EquipmentStorageUpgradeAmount}", EquipmentStorageUpgradeCost, PurchaseEquipmentStorageUpgrade));
            equipmentStorageUpgradeButtonText = equipmentStorageUpgradeButton != null
                ? equipmentStorageUpgradeButton.transform.Find("Label")?.GetComponent<Text>()
                : null;

            messageText = CreateText("Message", panel.transform, "商品を選んでください。", 21, FontStyle.Bold,
                new Vector2(0f, -1610f), new Vector2(820f, 60f), TextMain, TextAnchor.MiddleCenter);
            RefreshAutoRepeatFloorUpgradeButton(GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null);
            RefreshAutoSellEquipmentUpgradeButton(GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null);
            RefreshAutoReleaseMonsterUpgradeButton(GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null);
            RefreshStorageUpgradeButton(monsterStorageUpgradeButton, monsterStorageUpgradeButtonText, GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null, MonsterStorageUpgradeCost);
            RefreshStorageUpgradeButton(equipmentStorageUpgradeButton, equipmentStorageUpgradeButtonText, GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null, EquipmentStorageUpgradeCost);
        }

        private void BuildPurchasedPermanentUpgradeListPage()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            bool hasAutoRepeat = profile != null && profile.HasAutoRepeatFloorUpgrade;
            bool autoRepeatEnabled = hasAutoRepeat && profile.IsAutoRepeatFloorUpgradeEnabled;
            bool hasAutoSellEquipment = profile != null && profile.HasAutoSellEquipmentUpgrade;
            bool autoSellEquipmentEnabled = hasAutoSellEquipment && profile.IsAutoSellEquipmentUpgradeEnabled;
            bool hasAutoReleaseMonster = profile != null && profile.HasAutoReleaseMonsterUpgrade;
            bool autoReleaseMonsterEnabled = hasAutoReleaseMonster && profile.IsAutoReleaseMonsterUpgradeEnabled;
            int purchasedCount = 0;
            if (hasAutoRepeat) purchasedCount += 1;
            if (hasAutoSellEquipment) purchasedCount += 1;
            if (hasAutoReleaseMonster) purchasedCount += 1;

            CreateFullScreenImage("PurchasedPermanentUpgradeBackground", categoryRoot.transform, "UI/FusionPage/FusionBackground");
            GameObject panel = CreatePanel("PurchasedPermanentUpgradePanel", categoryRoot.transform, "UI/FusionPage/FusionMainFrame",
                new Vector2(0f, PermanentUpgradeFrameTop), new Vector2(1000f, 1710f), PanelColor);

            CreatePermanentUpgradeHeading(categoryRoot.transform, "購入済みの永続効果");

            GameObject summary = CreatePanel("PurchasedPermanentUpgradeSummary", panel.transform, null,
                new Vector2(0f, -225f), new Vector2(720f, 90f), new Color(0.025f, 0.035f, 0.065f, 0.96f));
            CreateText("SummaryText", summary.transform, profile != null ? $"購入済み  {purchasedCount}件" : "購入済み  読込中", 28, FontStyle.Bold,
                new Vector2(0f, -22f), new Vector2(560f, 50f), purchasedCount > 0 ? AccentCrystal : TextSub, TextAnchor.MiddleCenter);

            if (profile == null)
            {
                CreatePurchasedPermanentUpgradeEmptyCard(panel.transform, "プレイヤーデータを読み込めませんでした。", "ホームへ戻ってから再度開いてください。");
            }
            else if (purchasedCount > 0)
            {
                float cardY = -350f;
                if (hasAutoRepeat)
                {
                    CreatePurchasedPermanentUpgradeCard(
                        panel.transform,
                        "同階層オート再挑戦",
                        "勝利・敗北後に同じ階層へ自動で再挑戦",
                        autoRepeatEnabled ? "現在: 有効" : "現在: 無効",
                        autoRepeatEnabled ? "無効化する" : "有効化する",
                        "バトル右上の「永続効果・設定」で周回をONにします。",
                        new Vector2(0f, cardY),
                        ToggleAutoRepeatFloorUpgradeEnabled);
                    cardY -= 380f;
                }

                if (hasAutoSellEquipment)
                {
                    CreatePurchasedPermanentThresholdCard(
                        panel.transform,
                        "装備自動売却",
                        "指定品質に満たない装備を自動で売却",
                        autoSellEquipmentEnabled ? "現在: 有効" : "現在: 無効",
                        autoSellEquipmentEnabled ? "無効化する" : "有効化する",
                        BuildAutoSellEquipmentEffectText(profile),
                        BuildAutoSellEquipmentThresholdText(profile),
                        new Vector2(0f, cardY),
                        ToggleAutoSellEquipmentUpgradeEnabled,
                        DecreaseAutoSellEquipmentThreshold,
                        IncreaseAutoSellEquipmentThreshold);
                    cardY -= 430f;
                }

                if (hasAutoReleaseMonster)
                {
                    CreatePurchasedPermanentThresholdCard(
                        panel.transform,
                        "モンスター自動逃がし",
                        "指定IVに満たない捕獲モンスターを自動で逃がす",
                        autoReleaseMonsterEnabled ? "現在: 有効" : "現在: 無効",
                        autoReleaseMonsterEnabled ? "無効化する" : "有効化する",
                        BuildAutoReleaseMonsterEffectText(profile),
                        BuildAutoReleaseMonsterThresholdText(profile),
                        new Vector2(0f, cardY),
                        ToggleAutoReleaseMonsterUpgradeEnabled,
                        DecreaseAutoReleaseMonsterThreshold,
                        IncreaseAutoReleaseMonsterThreshold);
                }
            }
            else
            {
                CreatePurchasedPermanentUpgradeEmptyCard(panel.transform, "購入済みの永続強化はありません。", "永続強化を購入すると、この画面に有効中の効果だけが表示されます。");
            }

            HomeReturnButtonStyle.Create(categoryRoot.transform, "PurchasedPermanentUpgradeCloseButton", Hide);
        }

        private void CreatePurchasedPermanentUpgradeCard(
            Transform parent,
            string upgradeName,
            string shortDescription,
            string status,
            string actionLabel,
            string effectDescription,
            Vector2 position,
            UnityEngine.Events.UnityAction toggleAction)
        {
            GameObject card = CreatePanel("PurchasedPermanentUpgrade_" + upgradeName, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(820f, 350f), CardColor);
            CreateIcon("UpgradeIcon", card.transform, PermanentUpgradeIconPath, new Vector2(-320f, -42f), new Vector2(86f, 86f));
            CreateText("UpgradeName", card.transform, upgradeName, 30, FontStyle.Bold,
                new Vector2(-32f, -40f), new Vector2(520f, 50f), TextMain, TextAnchor.MiddleLeft);

            CreateText("StatusLabel", card.transform, status, 21, FontStyle.Bold,
                new Vector2(-32f, -92f), new Vector2(520f, 36f), AccentCrystal, TextAnchor.MiddleLeft);
            StyleActionButton(CreateButton("ToggleButton", card.transform, actionLabel,
                new Vector2(225f, -200f), new Vector2(310f, 128f), toggleAction),
                actionLabel == "有効化する" ? EnableButtonColor : DisableButtonColor);

            CreateText("UpgradeDescription", card.transform, shortDescription, 20, FontStyle.Bold,
                new Vector2(-32f, -138f), new Vector2(560f, 50f), TextSub, TextAnchor.MiddleLeft);

            GameObject effectPanel = CreatePanel("EffectPanel", card.transform, null,
                new Vector2(-160f, -220f), new Vector2(440f, 96f), new Color(0.018f, 0.024f, 0.044f, 0.92f));
            CreateText("EffectText", effectPanel.transform, effectDescription, 19, FontStyle.Bold,
                new Vector2(0f, -14f), new Vector2(404f, 68f), TextMain, TextAnchor.MiddleCenter);
        }

        private void CreatePurchasedPermanentThresholdCard(
            Transform parent,
            string upgradeName,
            string shortDescription,
            string status,
            string actionLabel,
            string effectDescription,
            string thresholdText,
            Vector2 position,
            UnityEngine.Events.UnityAction toggleAction,
            UnityEngine.Events.UnityAction decreaseAction,
            UnityEngine.Events.UnityAction increaseAction)
        {
            GameObject card = CreatePanel("PurchasedPermanentUpgrade_" + upgradeName, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(820f, 420f), CardColor);
            CreateIcon("UpgradeIcon", card.transform, PermanentUpgradeIconPath, new Vector2(-320f, -42f), new Vector2(86f, 86f));
            CreateText("UpgradeName", card.transform, upgradeName, 30, FontStyle.Bold,
                new Vector2(-32f, -40f), new Vector2(520f, 50f), TextMain, TextAnchor.MiddleLeft);
            CreateText("StatusLabel", card.transform, status, 21, FontStyle.Bold,
                new Vector2(-32f, -92f), new Vector2(520f, 36f), AccentCrystal, TextAnchor.MiddleLeft);
            CreateText("UpgradeDescription", card.transform, shortDescription, 19, FontStyle.Bold,
                new Vector2(-32f, -136f), new Vector2(560f, 48f), TextSub, TextAnchor.MiddleLeft);

            GameObject thresholdPanel = CreatePanel("ThresholdPanel", card.transform, null,
                new Vector2(-155f, -198f), new Vector2(450f, 108f), new Color(0.018f, 0.024f, 0.044f, 0.92f));
            StyleActionButton(CreateButton("DecreaseThreshold", thresholdPanel.transform, "−", new Vector2(-173f, -6f), new Vector2(96f, 96f), decreaseAction), EnableButtonColor);
            CreateText("ThresholdText", thresholdPanel.transform, thresholdText, 21, FontStyle.Bold,
                new Vector2(0f, -12f), new Vector2(238f, 84f), TextMain, TextAnchor.MiddleCenter);
            StyleActionButton(CreateButton("IncreaseThreshold", thresholdPanel.transform, "+", new Vector2(173f, -6f), new Vector2(96f, 96f), increaseAction), EnableButtonColor);

            StyleActionButton(CreateButton("ToggleButton", card.transform, actionLabel,
                new Vector2(225f, -198f), new Vector2(300f, 128f), toggleAction),
                actionLabel == "有効化する" ? EnableButtonColor : DisableButtonColor);

            GameObject effectPanel = CreatePanel("EffectPanel", card.transform, null,
                new Vector2(0f, -340f), new Vector2(740f, 60f), new Color(0.018f, 0.024f, 0.044f, 0.92f));
            CreateText("EffectText", effectPanel.transform, effectDescription, 18, FontStyle.Bold,
                new Vector2(0f, -8f), new Vector2(700f, 44f), TextSub, TextAnchor.MiddleCenter);
        }

        private void ToggleAutoRepeatFloorUpgradeEnabled()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || !profile.HasAutoRepeatFloorUpgrade)
            {
                return;
            }

            profile.IsAutoRepeatFloorUpgradeEnabled = !profile.IsAutoRepeatFloorUpgradeEnabled;
            SaveManager.Instance?.SaveCurrentGame();
            OpenPurchasedPermanentUpgradeList();
        }

        private void ToggleAutoSellEquipmentUpgradeEnabled()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || !profile.HasAutoSellEquipmentUpgrade)
            {
                return;
            }

            profile.IsAutoSellEquipmentUpgradeEnabled = !profile.IsAutoSellEquipmentUpgradeEnabled;
            SaveManager.Instance?.SaveCurrentGame();
            OpenPurchasedPermanentUpgradeList();
        }

        private void ToggleAutoReleaseMonsterUpgradeEnabled()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || !profile.HasAutoReleaseMonsterUpgrade)
            {
                return;
            }

            profile.IsAutoReleaseMonsterUpgradeEnabled = !profile.IsAutoReleaseMonsterUpgradeEnabled;
            SaveManager.Instance?.SaveCurrentGame();
            OpenPurchasedPermanentUpgradeList();
        }

        private void DecreaseAutoSellEquipmentThreshold()
        {
            AdjustAutoSellEquipmentThreshold(-1);
        }

        private void IncreaseAutoSellEquipmentThreshold()
        {
            AdjustAutoSellEquipmentThreshold(1);
        }

        private void AdjustAutoSellEquipmentThreshold(int delta)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || !profile.HasAutoSellEquipmentUpgrade)
            {
                return;
            }

            profile.SetAutoSellEquipmentQualityThreshold(profile.AutoSellEquipmentQualityThreshold + delta);
            SaveManager.Instance?.SaveCurrentGame();
            OpenPurchasedPermanentUpgradeList();
        }

        private void DecreaseAutoReleaseMonsterThreshold()
        {
            AdjustAutoReleaseMonsterThreshold(-5);
        }

        private void IncreaseAutoReleaseMonsterThreshold()
        {
            AdjustAutoReleaseMonsterThreshold(5);
        }

        private void AdjustAutoReleaseMonsterThreshold(int delta)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || !profile.HasAutoReleaseMonsterUpgrade)
            {
                return;
            }

            profile.SetAutoReleaseMonsterIndividualValueThreshold(profile.AutoReleaseMonsterIndividualValueThreshold + delta);
            SaveManager.Instance?.SaveCurrentGame();
            OpenPurchasedPermanentUpgradeList();
        }

        private void CreatePurchasedPermanentUpgradeEmptyCard(Transform parent, string title, string description)
        {
            GameObject card = CreatePanel("PurchasedPermanentUpgradeEmpty", parent, "UI/FusionPage/FusionRosterFrame",
                new Vector2(0f, -390f), new Vector2(820f, 360f), new Color(0.030f, 0.035f, 0.060f, 0.94f));
            CreateIcon("EmptyIcon", card.transform, PermanentUpgradeIconPath, new Vector2(0f, -72f), new Vector2(132f, 132f));
            CreateText("EmptyTitle", card.transform, title, 28, FontStyle.Bold,
                new Vector2(0f, -210f), new Vector2(700f, 50f), TextMain, TextAnchor.MiddleCenter);
            CreateText("EmptyDescription", card.transform, description, 20, FontStyle.Bold,
                new Vector2(0f, -266f), new Vector2(700f, 70f), TextSub, TextAnchor.MiddleCenter);
        }

        private GameObject BuildCategoryShell(string title, string subtitle)
        {
            CreateFullScreenImage("CategoryBackground", categoryRoot.transform, "UI/FusionPage/FusionBackground");
            bool permanentUpgrade = title == "永続強化";
            GameObject panel = CreatePanel("CategoryMainPanel", categoryRoot.transform, "UI/FusionPage/FusionMainFrame",
                new Vector2(0f, permanentUpgrade ? PermanentUpgradeFrameTop : 0f), new Vector2(1000f, 1710f), PanelColor);

            if (permanentUpgrade) CreatePermanentUpgradeHeading(categoryRoot.transform, subtitle);
            else
            {
                CreateText("Title", panel.transform, title, 50, FontStyle.Bold,
                    new Vector2(0f, -52f), new Vector2(680f, 70f), AccentGold, TextAnchor.MiddleCenter);
                CreateText("Subtitle", panel.transform, subtitle, 21, FontStyle.Bold,
                    new Vector2(0f, -116f), new Vector2(760f, 42f), TextSub, TextAnchor.MiddleCenter);
            }

            GameObject balance = CreatePanel("CategoryBalancePanel", panel.transform, null,
                new Vector2(0f, -180f), new Vector2(720f, 88f), new Color(0.025f, 0.035f, 0.065f, 0.96f));
            CreateIcon("PaidStoneIcon", balance.transform, PaidStoneIconPath, new Vector2(-222f, -11f), new Vector2(64f, 64f));
            categoryBalanceText = CreateText("CategoryBalance", balance.transform, string.Empty, 27, FontStyle.Bold,
                new Vector2(48f, -22f), new Vector2(470f, 48f), AccentCrystal, TextAnchor.MiddleCenter);

            Button backButton = HomeReturnButtonStyle.Create(categoryRoot.transform, "PaidShopCategoryBackButton", ShowSelector);
            HomeReturnButtonStyle.Apply(backButton, "ショップ選択へ");
            return panel;
        }

        private void CreatePermanentUpgradeHeading(Transform parent, string subtitle)
        {
            // A separate opaque image2 heading plate keeps the lettering clear
            // of the large frame's dragon ornament and the return button.
            CreatePanel("PermanentUpgradeTitleFrame", parent, HeadingFramePath,
                new Vector2(0f, -8f), new Vector2(480f, 104f), PanelColor);
            CreateText("Title", parent, "永続強化", 50, FontStyle.Bold,
                new Vector2(0f, -24f), new Vector2(400f, 70f), AccentGold, TextAnchor.MiddleCenter);
            CreateText("Subtitle", parent, subtitle, 21, FontStyle.Bold,
                new Vector2(0f, -122f), new Vector2(600f, 36f), TextMain, TextAnchor.MiddleCenter);
        }

        private void CreateCrystalProductCard(
            Transform parent,
            string productId,
            string productName,
            string contents,
            Vector2 position)
        {
            GameObject card = CreatePanel("Product_" + productName, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(420f, 330f), CardColor);
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            Button cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            // Disabling all cards guards against duplicate purchases, but the
            // default disabled tint also halves their alpha and exposes the
            // ornate background. Keep the storefront visually stable while
            // StoreKit presents its confirmation sheet; status text gives feedback.
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(() => PurchaseCrystalProduct(productId, productName));
            CreateIcon("StoneIcon", card.transform, PaidStoneIconPath, new Vector2(0f, -30f), new Vector2(88f, 88f));
            CreateText("ProductName", card.transform, productName, 26, FontStyle.Bold,
                new Vector2(0f, -124f), new Vector2(360f, 42f), TextMain, TextAnchor.MiddleCenter);
            CreateText("Contents", card.transform, contents, 32, FontStyle.Bold,
                new Vector2(0f, -174f), new Vector2(360f, 48f), AccentCrystal, TextAnchor.MiddleCenter);
            // One click handler for the whole card, including icon, text and
            // price. Decorative children must not swallow or duplicate taps.
            GameObject price = CreatePanel("BuyButton", card.transform, "UI/FusionPage/FusionSmallButton",
                new Vector2(0f, -204f), new Vector2(390f, 124f), CardColor);
            Text label = CreateText("Label", price.transform, "商品情報を取得中", 34, FontStyle.Bold,
                new Vector2(0f, -42f), new Vector2(350f, 44f), TextMain, TextAnchor.MiddleCenter);
            crystalPurchaseButtons[productId] = cardButton;
            crystalPriceLabels[productId] = label;
        }

        private void PurchaseCrystalProduct(string productId, string productName)
        {
            InAppPurchaseService service = InAppPurchaseService.Instance;
            if (service == null)
            {
                ShowPurchaseMessage("購入機能を開始できていません。アプリを開き直してください。", false);
                return;
            }

            if (!string.IsNullOrEmpty(service.NewPurchaseBlockedMessage))
            {
                RefreshCrystalProductPrices();
                // The complete reason already occupies StoreStatus; do not
                // repeat its multi-line text in the shorter result-message row.
                ShowPurchaseMessage(string.Empty, true);
                return;
            }

            if (!service.IsProductAvailable(productId))
            {
                service.EnsureProductsAvailable();
                ShowPurchaseMessage($"{productName}の商品情報を確認中です。\n取得後、もう一度このカードをタップしてください。", false);
                return;
            }

            // Reuse the fixed StoreStatus line instead of adding another wait
            // message below it. Clear any error from a previous attempt.
            ShowPurchaseMessage(string.Empty, true);
            service.Purchase(productId);
        }

        private void RetryCrystalProducts()
        {
            InAppPurchaseService.Instance?.RefreshProducts();
            ShowPurchaseMessage("商品情報だけを再取得します。購入は開始しません。", true);
            RefreshCrystalProductPrices();
        }

        private void RefreshCrystalProductPrices()
        {
            InAppPurchaseService service = InAppPurchaseService.Instance;
            bool busy = service != null && service.IsPurchaseInProgress;
            string blockedMessage = service != null ? service.NewPurchaseBlockedMessage : string.Empty;
            bool blocked = !string.IsNullOrEmpty(blockedMessage);
            foreach (IapProductDefinition product in IapProductCatalog.Products)
            {
                if (crystalPriceLabels.TryGetValue(product.ProductId, out Text priceLabel) && priceLabel != null)
                {
                    bool available = service != null && service.IsProductAvailable(product.ProductId);
                    priceLabel.text = available
                        ? service.GetLocalizedPrice(product.ProductId, "価格を確認") + " で購入"
                        : service != null && service.IsCatalogLoading ? "商品情報を取得中" : "未取得・再試行";
                    if (blocked)
                        priceLabel.text = (available ? service.GetLocalizedPrice(product.ProductId, "価格を確認") + "\n" : string.Empty)
                            + (blockedMessage == IapPurchaseEnvironmentPolicy.DevelopmentProductionMessage
                                ? "検証版では購入不可" : "現在は購入不可");
                    // Retain fetched prices above the warning, inside the existing
                    // price ornament, with a 4-point gap below Contents and at
                    // least 20 points inside the card's bottom edge. Keep the
                    // center fixed; the unblocked/release layout stays unchanged.
                    priceLabel.rectTransform.sizeDelta = new Vector2(350f, blocked ? 84f : 44f);
                    priceLabel.rectTransform.anchoredPosition = new Vector2(0f, blocked ? -22f : -42f);
                    priceLabel.color = available && !blocked ? TextMain : AccentGold;
                }

                if (crystalPurchaseButtons.TryGetValue(product.ProductId, out Button button) && button != null)
                {
                    // Keep unavailable cards responsive so tapping explains
                    // the loading/error state instead of silently doing nothing.
                    button.interactable = !busy && !blocked;
                }
            }

            if (crystalStoreStatusText != null)
            {
                crystalStoreStatusText.text = blocked ? blockedMessage
                    : busy ? "App Storeの購入確認画面を開いています…"
                    : service != null ? service.StoreStatusMessage
                    : "購入機能を開始できていません。アプリを開き直してください。";
                crystalStoreStatusText.color = blocked || service == null || service.HasStoreError ? AccentGold : TextSub;
            }
            if (crystalRetryButton != null)
            {
                crystalRetryButton.gameObject.SetActive(service == null || !service.AreAllProductsReady);
                crystalRetryButton.interactable = service != null && service.CanRetryProducts;
            }
        }

        private void HandleStorePurchaseSucceeded(string productId, int paidStoneAmount)
        {
            if (!IapProductCatalog.TryGet(productId, out IapProductDefinition definition))
            {
                return;
            }

            Refresh();
            RefreshCrystalProductPrices();
            ShowPurchaseMessage($"有償宝晶 {definition.PaidStoneAmount:N0}個を受け取りました。", true);
        }

        private void HandleStorePurchaseFailed(string message)
        {
            RefreshCrystalProductPrices();
            string blockedMessage = InAppPurchaseService.Instance?.NewPurchaseBlockedMessage;
            ShowPurchaseMessage(!string.IsNullOrEmpty(blockedMessage) && message == blockedMessage ? string.Empty : message, false);
        }

        private Button CreateWideProductCard(
            Transform parent,
            string productName,
            string description,
            string price,
            string iconPath,
            Vector2 position,
            UnityEngine.Events.UnityAction action = null)
        {
            return CreateWideProductCard(parent, productName, description, price, iconPath, position, new Vector2(820f, 280f), action);
        }

        private Button CreateWideProductCard(
            Transform parent,
            string productName,
            string description,
            string price,
            string iconPath,
            Vector2 position,
            Vector2 size)
        {
            return CreateWideProductCard(parent, productName, description, price, iconPath, position, size, null);
        }

        private Button CreateWideProductCard(
            Transform parent,
            string productName,
            string description,
            string price,
            string iconPath,
            Vector2 position,
            Vector2 size,
            UnityEngine.Events.UnityAction action = null)
        {
            GameObject card = CreatePanel("Product_" + productName, parent, "UI/FusionPage/FusionRosterFrame",
                position, size, CardColor);
            CreateIcon("ProductIcon", card.transform, iconPath, new Vector2(-324f, -112f), new Vector2(88f, 88f));
            CreateText("ProductName", card.transform, productName, 31, FontStyle.Bold,
                new Vector2(0f, -32f), new Vector2(730f, 48f), TextMain, TextAnchor.MiddleLeft);
            CreateText("Description", card.transform, description, 24, FontStyle.Bold,
                new Vector2(-95f, -98f), new Vector2(340f, 118f), TextSub, TextAnchor.MiddleLeft);
            UnityEngine.Events.UnityAction buttonAction = action;
            if (buttonAction == null)
            {
                buttonAction = () => ShowPurchaseUnavailable(productName);
            }

            Button buy = CreateButton(
                "BuyButton",
                card.transform,
                price,
                new Vector2(235f, -94f),
                new Vector2(280f, 132f),
                buttonAction);
            StyleActionButton(buy, PurchaseButtonColor);
            return buy;
        }

        private void ShowPermanentPurchaseConfirmation(string productName, int cost, UnityEngine.Events.UnityAction purchase)
        {
            if (purchaseConfirmationRoot != null || purchase == null) return;
            string paidUnavailable = WitchTower.Save.OnlinePlayerData.PaidSpendingUnavailableMessage;
            if (!string.IsNullOrEmpty(paidUnavailable)) { ShowPurchaseMessage(paidUnavailable, false); return; }
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null || profile.PaidGachaStones < cost)
            {
                ShowPurchaseMessage(profile == null ? "プレイヤーデータを読み込めませんでした。" : "有償宝晶が不足しています。", false);
                Refresh();
                return;
            }

            pendingPermanentPurchase = purchase;
            purchaseConfirmationRoot = CreateStretchRoot("PermanentPurchaseConfirmation", transform);
            Image dim = purchaseConfirmationRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);
            dim.raycastTarget = true; // No taps can reach the shop behind the dialog.
            GameObject panel = CreatePanel("ConfirmationPanel", purchaseConfirmationRoot.transform, null,
                new Vector2(0f, 340f), new Vector2(900f, 680f), new Color(0.035f, 0.05f, 0.08f, 1f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = AccentGold;
            outline.effectDistance = new Vector2(4f, -4f);
            CreateText("Title", panel.transform, "永続強化の購入確認", 38, FontStyle.Bold,
                new Vector2(0f, -42f), new Vector2(810f, 64f), AccentGold, TextAnchor.MiddleCenter);
            CreateText("ProductName", panel.transform, productName, 34, FontStyle.Bold,
                new Vector2(0f, -132f), new Vector2(810f, 78f), TextMain, TextAnchor.MiddleCenter);
            CreateText("Cost", panel.transform, $"消費する有償宝晶：{cost:N0}個", 32, FontStyle.Bold,
                new Vector2(0f, -236f), new Vector2(810f, 54f), AccentCrystal, TextAnchor.MiddleCenter);
            CreateText("Balance", panel.transform,
                $"所持 {profile.PaidGachaStones:N0}個 → 購入後 {profile.PaidGachaStones - cost:N0}個", 28, FontStyle.Bold,
                new Vector2(0f, -308f), new Vector2(810f, 58f), TextMain, TextAnchor.MiddleCenter);
            CreateText("Hint", panel.transform, "「購入する」を押すと、有償宝晶を消費します。", 26, FontStyle.Bold,
                new Vector2(0f, -395f), new Vector2(810f, 68f), TextSub, TextAnchor.MiddleCenter);
            StyleActionButton(CreateButton("CancelPurchase", panel.transform, "キャンセル",
                new Vector2(-214f, -508f), new Vector2(368f, 132f), ClosePermanentPurchaseConfirmation), EnableButtonColor);
            StyleActionButton(CreateButton("ConfirmPurchase", panel.transform, "購入する",
                new Vector2(214f, -508f), new Vector2(368f, 132f), ConfirmPermanentPurchase, freshPress: true), PurchaseButtonColor);
        }

        private void ConfirmPermanentPurchase()
        {
            // Consume the pending action first: a repeated click cannot spend twice.
            var purchase = pendingPermanentPurchase;
            ClosePermanentPurchaseConfirmation();
            // Each purchase rechecks the current balance and existing entitlement.
            purchase?.Invoke();
        }

        private void ClosePermanentPurchaseConfirmation()
        {
            pendingPermanentPurchase = null;
            if (purchaseConfirmationRoot == null) return;
            purchaseConfirmationRoot.SetActive(false);
            if (Application.isPlaying) Destroy(purchaseConfirmationRoot);
            else DestroyImmediate(purchaseConfirmationRoot);
            purchaseConfirmationRoot = null;
        }

        private static void StyleActionButton(Button button, Color color)
        {
            Image image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = color;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(0.60f, 0.60f, 0.60f, 1f);
            button.colors = colors;
            Outline outline = button.gameObject.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.42f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 10f);
            label.rectTransform.offsetMax = new Vector2(-12f, -10f);
            label.fontSize = label.resizeTextMaxSize = 29;
            label.resizeTextMinSize = 26;
            label.color = Color.white;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void PurchaseAutoRepeatFloorUpgrade() => PurchaseOnlineUpgrade("auto_repeat");
        private void PurchaseAutoSellEquipmentUpgrade() => PurchaseOnlineUpgrade("auto_sell");
        private void PurchaseAutoReleaseMonsterUpgrade() => PurchaseOnlineUpgrade("auto_release");
        private void PurchaseMonsterStorageUpgrade() => PurchaseOnlineUpgrade("monster_storage");
        private void PurchaseEquipmentStorageUpgrade() => PurchaseOnlineUpgrade("equipment_storage");

        private void PurchaseOnlineUpgrade(string target)
        {
            WitchTower.Save.OnlinePlayerData.Ensure().Execute(new WitchTower.Save.OnlineRequest { Kind = "upgrade", Target = target }, (op, error) => {
                if (this == null) return;
                ShowPurchaseMessage(error ?? "購入内容を反映しました。", op != null);
                Refresh();
                if (op != null && string.IsNullOrEmpty(error))
                {
                    pendingUpgradeTutorial = target;
                    ShowPendingUpgradeTutorial();
                }
            });
        }

        private void Update() => ShowPendingUpgradeTutorial();

        private void ShowPendingUpgradeTutorial()
        {
            if (string.IsNullOrEmpty(pendingUpgradeTutorial) || !gameObject.activeInHierarchy) return;
            string target = pendingUpgradeTutorial;
            pendingUpgradeTutorial = null;
            if (upgradeTutorialRoot != null) Destroy(upgradeTutorialRoot);
            bool configurable = target.StartsWith("auto_", StringComparison.Ordinal);
            string body = target switch
            {
                "auto_repeat" => "同階層オート再挑戦を購入しました。\n\n勝利・敗北後に同じ階層へ再挑戦できるようになりました。\n\nホームの「永続強化」で有効／無効を切り替えられます。実際に周回するときは、バトル右上の「永続効果・設定」で周回をONにしてください。",
                "auto_sell" => "装備自動売却を購入しました。\n\n設定した品質に満たない装備を、獲得時に自動で売却します。\n\nホームの「永続強化」で、有効／無効と売却する品質の基準を設定できます。初めに基準を確認しましょう。",
                "auto_release" => "モンスター自動逃がしを購入しました。\n\n探索で仲間になるモンスターのうち、設定した平均個体値に満たない仲間を自動で逃がします。\n\nホームの「永続強化」で、有効／無効と平均個体値の基準を設定できます。初めに基準を確認しましょう。",
                "monster_storage" => "モンスターの所持枠が20体分増えました。\n\n拡張はすぐに反映され、追加の設定は不要です。召喚や編成画面の所持数で、新しい上限を確認できます。\n\nこの効果は永続します。",
                _ => "装備の所持枠が20個分増えました。\n\n拡張はすぐに反映され、追加の設定は不要です。装備画面の所持数で、新しい上限を確認できます。\n\nこの効果は永続します。"
            };
            upgradeTutorialRoot = HelpDialog.Show(transform, "PermanentUpgradeTutorial", "永続強化の使い方", body,
                configurable ? "設定を確認" : null, configurable ? (Action)OpenPurchasedPermanentUpgradeList : null);
        }

        private void RefreshAutoRepeatFloorUpgradeButton(PlayerProfile profile)
        {
            if (autoRepeatFloorUpgradeButton == null)
            {
                return;
            }

            bool purchased = profile != null && profile.HasAutoRepeatFloorUpgrade;
            bool canBuy = profile != null &&
                !purchased &&
                profile.PaidGachaStones >= AutoRepeatFloorUpgradeCost &&
                string.IsNullOrEmpty(WitchTower.Save.OnlinePlayerData.PaidSpendingUnavailableMessage);

            autoRepeatFloorUpgradeButton.interactable = canBuy;
            if (autoRepeatFloorUpgradeButtonText != null)
            {
                autoRepeatFloorUpgradeButtonText.text = purchased
                    ? "購入済"
                    : FormatStonePrice(AutoRepeatFloorUpgradeCost);
            }

            Image buttonImage = autoRepeatFloorUpgradeButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = purchased
                    ? new Color(0.28f, 0.32f, 0.26f, 1f)
                    : canBuy
                        ? PurchaseButtonColor
                    : new Color(0.34f, 0.30f, 0.38f, 0.86f);
            }
        }

        private void RefreshAutoSellEquipmentUpgradeButton(PlayerProfile profile)
        {
            RefreshOneTimePermanentUpgradeButton(
                autoSellEquipmentUpgradeButton,
                autoSellEquipmentUpgradeButtonText,
                profile,
                AutoSellEquipmentUpgradeCost,
                profile != null && profile.HasAutoSellEquipmentUpgrade);
        }

        private void RefreshAutoReleaseMonsterUpgradeButton(PlayerProfile profile)
        {
            RefreshOneTimePermanentUpgradeButton(
                autoReleaseMonsterUpgradeButton,
                autoReleaseMonsterUpgradeButtonText,
                profile,
                AutoReleaseMonsterUpgradeCost,
                profile != null && profile.HasAutoReleaseMonsterUpgrade);
        }

        private void RefreshOneTimePermanentUpgradeButton(Button button, Text buttonText, PlayerProfile profile, int cost, bool purchased)
        {
            if (button == null)
            {
                return;
            }

            bool canBuy = profile != null &&
                !purchased &&
                profile.PaidGachaStones >= cost &&
                string.IsNullOrEmpty(WitchTower.Save.OnlinePlayerData.PaidSpendingUnavailableMessage);

            button.interactable = canBuy;
            if (buttonText != null)
            {
                buttonText.text = purchased ? "購入済" : FormatStonePrice(cost);
            }

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = purchased
                    ? new Color(0.28f, 0.32f, 0.26f, 1f)
                    : canBuy
                        ? PurchaseButtonColor
                        : new Color(0.34f, 0.30f, 0.38f, 0.86f);
            }
        }

        private void RefreshStorageUpgradeButton(Button button, Text buttonText, PlayerProfile profile, int cost)
        {
            if (button == null)
            {
                return;
            }

            bool canBuy = profile != null && profile.PaidGachaStones >= cost &&
                string.IsNullOrEmpty(WitchTower.Save.OnlinePlayerData.PaidSpendingUnavailableMessage);
            button.interactable = canBuy;
            if (buttonText != null)
            {
                buttonText.text = FormatStonePrice(cost);
            }

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = canBuy
                    ? PurchaseButtonColor
                    : new Color(0.34f, 0.30f, 0.38f, 0.86f);
            }
        }

        private void ShowPurchaseMessage(string message, bool success)
        {
            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = success ? AccentGold : new Color(1f, 0.48f, 0.36f, 1f);
            }
        }

        private static string FormatStonePrice(int amount)
        {
            return $"有償宝晶 {Mathf.Max(0, amount):N0}\n購入確認へ";
        }

        private static string BuildPermanentUpgradeStatusText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return "有効中: 読込中";
            }

            var activeUpgrades = new List<string>();
            if (profile.HasAutoRepeatFloorUpgrade && profile.IsAutoRepeatFloorUpgradeEnabled)
            {
                activeUpgrades.Add("同階層オート再挑戦");
            }

            if (profile.HasAutoSellEquipmentUpgrade && profile.IsAutoSellEquipmentUpgradeEnabled)
            {
                activeUpgrades.Add(BuildAutoSellEquipmentEffectText(profile));
            }

            if (profile.HasAutoReleaseMonsterUpgrade && profile.IsAutoReleaseMonsterUpgradeEnabled)
            {
                activeUpgrades.Add(BuildAutoReleaseMonsterEffectText(profile));
            }

            string activeUpgrade = activeUpgrades.Count > 0 ? string.Join(" / ", activeUpgrades) : "なし";
            return $"有効中: {activeUpgrade}  /  モンスター枠 {profile.OwnedMonsters.Count:N0}/{profile.MonsterStorageLimit:N0}  装備枠 {profile.OwnedEquipments.Count:N0}/{profile.EquipmentStorageLimit:N0}";
        }

        private static string BuildAutoSellEquipmentThresholdText(PlayerProfile profile)
        {
            int threshold = profile != null ? profile.AutoSellEquipmentQualityThreshold : 3;
            return $"基準: {ResolveQualityNameByRank(threshold)}以上を残す";
        }

        private static string BuildAutoSellEquipmentEffectText(PlayerProfile profile)
        {
            int threshold = profile != null ? profile.AutoSellEquipmentQualityThreshold : 3;
            return threshold <= 1
                ? "装備自動売却なし"
                : $"{ResolveQualityNameByRank(threshold)}未満を売却";
        }

        private static string BuildAutoReleaseMonsterThresholdText(PlayerProfile profile)
        {
            int threshold = profile != null ? profile.AutoReleaseMonsterIndividualValueThreshold : 50;
            return $"基準: IV{Mathf.Clamp(threshold, 1, 100)}";
        }

        private static string BuildAutoReleaseMonsterEffectText(PlayerProfile profile)
        {
            int threshold = profile != null ? profile.AutoReleaseMonsterIndividualValueThreshold : 50;
            return $"IV{Mathf.Clamp(threshold, 1, 100)}未満を逃がす";
        }

        private static string ResolveQualityNameByRank(int qualityRank)
        {
            switch (Mathf.Clamp(qualityRank, 1, 5))
            {
                case 5:
                    return "レジェンダリー";
                case 4:
                    return "エピック";
                case 3:
                    return "レア";
                case 2:
                    return "アンコモン";
                case 1:
                default:
                    return "コモン";
            }
        }

        private void ShowPurchaseUnavailable(string productName)
        {
            if (messageText != null)
            {
                messageText.text = $"{productName}の購入機能は準備中です。";
                messageText.color = AccentGold;
            }
        }

        private void ShowSelector()
        {
            ClosePermanentPurchaseConfirmation();
            if (selectorRoot != null)
            {
                selectorRoot.SetActive(true);
            }

            if (categoryRoot != null)
            {
                categoryRoot.SetActive(false);
            }

            Refresh();
        }

        private void Hide()
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private static GameObject CreateStretchRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private GameObject CreatePanel(string name, Transform parent, string spritePath, Vector2 position, Vector2 size, Color fallbackColor)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = string.IsNullOrEmpty(spritePath) ? null : Resources.Load<Sprite>(spritePath);
            image.color = image.sprite != null ? Color.white : fallbackColor;
            image.raycastTarget = false;
            return root;
        }

        private void CreateFullScreenImage(string name, Transform parent, string spritePath)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = root.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(spritePath);
            image.color = image.sprite != null ? Color.white : PageTint;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private void CreateIcon(string name, Transform parent, string spritePath, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(spritePath);
            image.color = image.sprite != null ? Color.white : AccentCrystal;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size, Color color, TextAnchor alignment)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = root.GetComponent<Text>();
            text.font = runtimeFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            UnityEngine.Events.UnityAction action,
            float labelYOffset = DefaultButtonLabelYOffset,
            bool freshPress = false)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (freshPress) root.AddComponent<FreshPressButton>();
            else root.AddComponent<Button>();
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/FusionPage/FusionSmallButton");
            image.color = image.sprite != null ? Color.white : new Color(0.38f, 0.20f, 0.48f, 1f);
            image.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText("Label", root.transform, label, 22, FontStyle.Bold,
                new Vector2(0f, labelYOffset), size - new Vector2(30f, 16f), TextMain, TextAnchor.MiddleCenter);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i -= 1)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
