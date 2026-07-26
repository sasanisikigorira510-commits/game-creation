using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class GoldShopPanelController : MonoBehaviour
    {
        private const string TutorialGuideSpritePath = "UI/Tutorial/TutorialGuideAssistant";
        private const string TutorialHighlightFramePath = "UI/Tutorial/TutorialSummonHighlightFrameImage2";
        private const string BuyButtonFrameSpritePath = "UI/FusionPage/FusionConfirmButton";

        private sealed class ProductCardView
        {
            public GoldShopProductDefinition Product;
            public Button BuyButton;
            public Text PriceText;
        }

        private static readonly Color PageTint = new Color(0.01f, 0.008f, 0.004f, 0.97f);
        private static readonly Color PanelColor = new Color(0.055f, 0.038f, 0.018f, 0.98f);
        private static readonly Color CardColor = new Color(0.08f, 0.055f, 0.025f, 0.98f);
        private static readonly Color AccentGold = new Color(1f, 0.78f, 0.30f, 1f);
        private static readonly Color TextMain = new Color(1f, 0.98f, 0.90f, 1f);
        private static readonly Color TextSub = new Color(0.86f, 0.78f, 0.62f, 1f);
        private static readonly Color BuyButtonBackingColor = new Color(0.72f, 0.36f, 0.05f, 0.70f);
        private static readonly Color BuyButtonGlowColor = new Color(1f, 0.72f, 0.18f, 0.34f);
        private static readonly Color BuyButtonDisabledColor = new Color(0.23f, 0.21f, 0.18f, 0.72f);
        private static readonly Color BuyButtonDisabledText = new Color(0.58f, 0.54f, 0.46f, 1f);

        private readonly List<ProductCardView> productCards = new List<ProductCardView>();
        private Action onClosed;
        private Font runtimeFont;
        private Text goldBalanceText;
        private Text playerStatusText;
        private Text messageText;
        private GameObject shopTutorialGuideRoot;
        private Image shopTutorialGuideCharacterImage;
        private Image shopTutorialHomeHighlight;
        private bool isBuilt;

        public void Show(Action closeCallback)
        {
            onClosed = closeCallback;
            if (!isBuilt)
            {
                Build();
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Refresh()
        {
            if (!isBuilt)
            {
                return;
            }

            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (goldBalanceText != null)
            {
                goldBalanceText.text = profile != null ? $"所持ゴールド  {profile.Gold:N0}" : "所持ゴールド  -";
            }

            if (playerStatusText != null)
            {
                playerStatusText.text = profile != null
                    ? $"冒険者 Lv.{profile.Level}  経験値 {profile.Exp:N0}/{profile.GetRequiredExpForNextLevel():N0}    モンスター枠 {profile.OwnedMonsters.Count}/{profile.MonsterStorageLimit}    装備枠 {profile.OwnedEquipments.Count}/{profile.EquipmentStorageLimit}"
                    : string.Empty;
            }

            foreach (ProductCardView card in productCards)
            {
                bool equipmentStorageFull = profile != null &&
                    card.Product.RewardType == GoldShopRewardType.Equipment &&
                    !profile.HasEquipmentStorageSpace();
                bool canBuy = profile != null && profile.Gold >= card.Product.Cost && !equipmentStorageFull;
                if (card.BuyButton != null)
                {
                    ApplyBuyButtonVisualState(card.BuyButton, canBuy);
                }

                if (card.PriceText != null)
                {
                    card.PriceText.color = canBuy ? AccentGold : new Color(0.58f, 0.54f, 0.46f, 1f);
                }
            }

            RefreshShopTutorialGuide();
        }

        private void Update()
        {
            AnimateShopTutorialGuide();
        }

        private void Build()
        {
            ClearChildren();
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform rootRect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlay = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            overlay.color = PageTint;
            overlay.raycastTarget = true;

            CreateFullScreenImage("ShopBackground", transform, "UI/FusionPage/FusionBackground");
            GameObject panel = CreatePanel("ShopMainPanel", transform, "UI/FusionPage/FusionMainFrame",
                Vector2.zero, new Vector2(1000f, 1710f), PanelColor);

            CreateText("Title", panel.transform, "商店", 50, FontStyle.Bold,
                new Vector2(0f, -48f), new Vector2(650f, 68f), AccentGold, TextAnchor.MiddleCenter);
            CreateText("Subtitle", panel.transform, "冒険で集めたゴールドを、次の挑戦の力に変えよう", 22, FontStyle.Bold,
                new Vector2(0f, -108f), new Vector2(760f, 42f), TextSub, TextAnchor.MiddleCenter);

            goldBalanceText = CreateText("GoldBalance", panel.transform, string.Empty, 30, FontStyle.Bold,
                new Vector2(0f, -174f), new Vector2(700f, 48f), AccentGold, TextAnchor.MiddleCenter);
            playerStatusText = CreateText("PlayerStatus", panel.transform, string.Empty, 19, FontStyle.Bold,
                new Vector2(0f, -220f), new Vector2(820f, 38f), TextSub, TextAnchor.MiddleCenter);

            IReadOnlyList<GoldShopProductDefinition> products = GoldShopService.GetProducts();
            for (int i = 0; i < products.Count; i += 1)
            {
                int column = i % 2;
                int row = i / 2;
                Vector2 position = new Vector2(column == 0 ? -226f : 226f, -390f - row * 360f);
                CreateProductCard(panel.transform, products[i], position);
            }

            messageText = CreateText("Message", panel.transform, "商品を選んでください。", 22, FontStyle.Bold,
                new Vector2(0f, -1500f), new Vector2(820f, 60f), TextMain, TextAnchor.MiddleCenter);

            Button closeButton = HomeReturnButtonStyle.Create(transform, "ShopCloseButton", Hide);
            BuildShopTutorialGuide(panel.transform, closeButton != null ? closeButton.transform : null);
            isBuilt = true;
        }

        private void BuildShopTutorialGuide(Transform panelTransform, Transform closeButtonTransform)
        {
            if (panelTransform == null || shopTutorialGuideRoot != null)
            {
                return;
            }

            shopTutorialGuideRoot = CreatePanel("ShopTutorialGuideRoot", panelTransform, null,
                new Vector2(0f, -1350f), new Vector2(920f, 300f), new Color(0.025f, 0.035f, 0.055f, 0.98f));

            Outline panelOutline = shopTutorialGuideRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.78f, 0.24f, 0.94f);
            panelOutline.effectDistance = new Vector2(4f, -4f);
            panelOutline.useGraphicAlpha = false;

            shopTutorialGuideCharacterImage = CreateImage("ShopTutorialGuideLuse", shopTutorialGuideRoot.transform,
                TutorialGuideSpritePath, new Vector2(-323f, -38f), new Vector2(218f, 218f));

            Text badgeText = CreateText("ShopTutorialGuideBadge", shopTutorialGuideRoot.transform, "TUTORIAL", 17, FontStyle.Bold,
                new Vector2(-116f, -22f), new Vector2(136f, 28f), AccentGold, TextAnchor.MiddleCenter);
            AddTextContrast(badgeText);

            Text titleText = CreateText("ShopTutorialGuideTitle", shopTutorialGuideRoot.transform, "ルシェの商店案内", 29, FontStyle.Bold,
                new Vector2(116f, -56f), new Vector2(560f, 36f), new Color(1f, 0.96f, 0.78f, 1f), TextAnchor.MiddleLeft);
            AddTextContrast(titleText);

            Text bodyText = CreateText("ShopTutorialGuideBody", shopTutorialGuideRoot.transform,
                "商店では、探索で集めたゴールドを強化遺物と交換できます。\n今回は場所を確認できれば大丈夫です。\n左上の「ホームへ戻る」から拠点へ戻りましょう。",
                19, FontStyle.Bold, new Vector2(116f, -102f), new Vector2(590f, 116f),
                new Color(0.96f, 0.95f, 0.88f, 1f), TextAnchor.UpperLeft);
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = 15;
            bodyText.resizeTextMaxSize = 19;
            AddTextContrast(bodyText);

            Text footerText = CreateText("ShopTutorialGuideFooter", shopTutorialGuideRoot.transform,
                "次の操作: 左上の「ホームへ戻る」をタップ", 18, FontStyle.Bold,
                new Vector2(116f, -248f), new Vector2(590f, 30f), new Color(0.78f, 0.92f, 1f, 1f), TextAnchor.MiddleLeft);
            AddTextContrast(footerText);

            if (closeButtonTransform != null)
            {
                shopTutorialHomeHighlight = CreateImage("ShopTutorialHomeHighlight", closeButtonTransform,
                    TutorialHighlightFramePath, Vector2.zero, HomeReturnButtonStyle.Size + new Vector2(34f, 30f));
                RectTransform highlightRect = shopTutorialHomeHighlight.rectTransform;
                highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
                highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
                highlightRect.pivot = new Vector2(0.5f, 0.5f);
                highlightRect.anchoredPosition = Vector2.zero;
                shopTutorialHomeHighlight.preserveAspect = false;
                shopTutorialHomeHighlight.transform.SetAsLastSibling();
                shopTutorialHomeHighlight.gameObject.SetActive(false);
            }

            shopTutorialGuideRoot.SetActive(false);
        }

        private void RefreshShopTutorialGuide()
        {
            bool shouldShow = ShouldShowShopTutorialGuide();
            if (shopTutorialGuideRoot != null)
            {
                shopTutorialGuideRoot.SetActive(shouldShow);
                if (shouldShow)
                {
                    shopTutorialGuideRoot.transform.SetAsLastSibling();
                }
            }

            if (shopTutorialHomeHighlight != null)
            {
                shopTutorialHomeHighlight.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    shopTutorialHomeHighlight.transform.SetAsLastSibling();
                }
            }
        }

        private static bool ShouldShowShopTutorialGuide()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            StoryTutorialEvent tutorialEvent = StoryTutorialService.GetNextEvent(profile, "HomeScene");
            return tutorialEvent != null &&
                tutorialEvent.EventId == StoryTutorialService.HintShop &&
                string.Equals(tutorialEvent.TargetKey, "home.shop", StringComparison.Ordinal);
        }

        private static void CompleteShopTutorialGuide()
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            bool changed = StoryTutorialService.MarkHintSeen(profile, StoryTutorialService.HintShop);
            if (changed && Application.isPlaying)
            {
                SaveManager.Instance?.SaveCurrentGame();
            }
        }

        private void AnimateShopTutorialGuide()
        {
            if (shopTutorialGuideRoot == null || !shopTutorialGuideRoot.activeInHierarchy)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.1f);
            if (shopTutorialGuideCharacterImage != null)
            {
                float scale = Mathf.Lerp(0.985f, 1.035f, pulse);
                shopTutorialGuideCharacterImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (shopTutorialHomeHighlight != null)
            {
                shopTutorialHomeHighlight.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.74f, 1f, pulse));
                float scale = Mathf.Lerp(0.99f, 1.035f, pulse);
                shopTutorialHomeHighlight.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void CreateProductCard(Transform parent, GoldShopProductDefinition product, Vector2 position)
        {
            GameObject card = CreatePanel("Product_" + product.Id, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(420f, 320f), CardColor);

            string category = product.RewardType switch
            {
                GoldShopRewardType.PlayerExp => "育成",
                GoldShopRewardType.FreeGachaStones => "石",
                GoldShopRewardType.Equipment => "装備",
                GoldShopRewardType.EnhancementRelic => "遺物",
                GoldShopRewardType.MonsterStorage => "枠拡張",
                _ => "アイテム"
            };

            CreateText("Category", card.transform, category, 16, FontStyle.Bold,
                new Vector2(0f, -30f), new Vector2(320f, 28f), TextSub, TextAnchor.MiddleCenter);
            CreateText("ProductName", card.transform, product.Title, 28, FontStyle.Bold,
                new Vector2(0f, -72f), new Vector2(350f, 46f), TextMain, TextAnchor.MiddleCenter);
            CreateText("Description", card.transform, product.Description, 21, FontStyle.Bold,
                new Vector2(0f, -130f), new Vector2(350f, 52f), TextSub, TextAnchor.MiddleCenter);

            Text priceText = CreateText("Price", card.transform, $"{product.Cost:N0}ゴールド", 28, FontStyle.Bold,
                new Vector2(0f, -184f), new Vector2(300f, 42f), AccentGold, TextAnchor.MiddleCenter);

            Button buyButton = CreateButton("BuyButton", card.transform, "購入", new Vector2(0f, -226f), new Vector2(320f, 84f),
                () => Purchase(product.Id));
            productCards.Add(new ProductCardView
            {
                Product = product,
                BuyButton = buyButton,
                PriceText = priceText
            });
        }

        private void Purchase(string productId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            bool purchased = GoldShopService.TryPurchase(profile, productId, out string message);
            if (purchased)
            {
                SaveManager.Instance?.SaveCurrentGame();
            }

            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = purchased ? AccentGold : new Color(1f, 0.48f, 0.36f, 1f);
            }

            Refresh();
        }

        private void Hide()
        {
            if (ShouldShowShopTutorialGuide())
            {
                CompleteShopTutorialGuide();
            }

            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private Image CreateImage(string name, Transform parent, string spritePath, Vector2 position, Vector2 size)
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
            image.sprite = LoadSprite(spritePath);
            image.color = image.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            return texture != null
                ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f)
                : null;
        }

        private static void AddTextContrast(Text text)
        {
            if (text == null)
            {
                return;
            }

            Outline outline = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
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
            image.sprite = Resources.Load<Sprite>(spritePath);
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(BuyButtonFrameSpritePath);
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = true;

            Shadow shadow = root.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.74f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;

            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.83f, 0.30f, 0.88f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;

            CreateButtonLayer("Glow", root.transform, new Vector2(0f, -10f),
                new Vector2(size.x - 26f, size.y - 20f), BuyButtonGlowColor);
            CreateButtonLayer("VisibilityBacking", root.transform, new Vector2(0f, -21f),
                new Vector2(size.x - 84f, size.y - 40f), BuyButtonBackingColor);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.68f, 1f);
            colors.pressedColor = new Color(0.86f, 0.56f, 0.18f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.48f, 0.45f, 0.38f, 0.74f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text labelText = CreateText("Label", root.transform, label, 28, FontStyle.Bold,
                new Vector2(0f, -18f), new Vector2(size.x - 72f, size.y - 32f), TextMain, TextAnchor.MiddleCenter);
            AddTextContrast(labelText);
            ApplyBuyButtonVisualState(button, true);
            return button;
        }

        private Image CreateButtonLayer(string name, Transform parent, Vector2 position, Vector2 size, Color color)
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
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void ApplyBuyButtonVisualState(Button button, bool canBuy)
        {
            button.interactable = canBuy;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = canBuy ? Color.white : new Color(0.48f, 0.45f, 0.38f, 0.74f);
            }

            Image glowImage = FindChildImage(button.transform, "Glow");
            if (glowImage != null)
            {
                glowImage.color = canBuy ? BuyButtonGlowColor : new Color(0f, 0f, 0f, 0.18f);
            }

            Image backingImage = FindChildImage(button.transform, "VisibilityBacking");
            if (backingImage != null)
            {
                backingImage.color = canBuy ? BuyButtonBackingColor : BuyButtonDisabledColor;
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = canBuy
                    ? new Color(1f, 0.83f, 0.30f, 0.88f)
                    : new Color(0.22f, 0.20f, 0.17f, 0.70f);
            }

            Text labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.color = canBuy ? TextMain : BuyButtonDisabledText;
            }
        }

        private static Image FindChildImage(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i -= 1)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            productCards.Clear();
        }
    }
}
