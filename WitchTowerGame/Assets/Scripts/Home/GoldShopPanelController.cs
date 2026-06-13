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

        private readonly List<ProductCardView> productCards = new List<ProductCardView>();
        private Action onClosed;
        private Font runtimeFont;
        private Text goldBalanceText;
        private Text playerStatusText;
        private Text messageText;
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
                goldBalanceText.text = profile != null ? $"所持ゴールド  {profile.Gold:N0} G" : "所持ゴールド  -";
            }

            if (playerStatusText != null)
            {
                playerStatusText.text = profile != null
                    ? $"冒険者 Lv.{profile.Level}  EXP {profile.Exp:N0}/{profile.GetRequiredExpForNextLevel():N0}    モンスター枠 {profile.OwnedMonsters.Count}/{profile.MonsterStorageLimit}"
                    : string.Empty;
            }

            foreach (ProductCardView card in productCards)
            {
                bool canBuy = profile != null && profile.Gold >= card.Product.Cost;
                if (card.BuyButton != null)
                {
                    card.BuyButton.interactable = canBuy;
                    Image buttonImage = card.BuyButton.GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = canBuy
                            ? new Color(0.42f, 0.24f, 0.06f, 1f)
                            : new Color(0.16f, 0.14f, 0.11f, 0.88f);
                    }
                }

                if (card.PriceText != null)
                {
                    card.PriceText.color = canBuy ? AccentGold : new Color(0.58f, 0.54f, 0.46f, 1f);
                }
            }
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

            CreateText("Title", panel.transform, "ゴールドショップ", 50, FontStyle.Bold,
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

            HomeReturnButtonStyle.Create(transform, "ShopCloseButton", Hide);
            isBuilt = true;
        }

        private void CreateProductCard(Transform parent, GoldShopProductDefinition product, Vector2 position)
        {
            GameObject card = CreatePanel("Product_" + product.Id, parent, "UI/FusionPage/FusionRosterFrame",
                position, new Vector2(420f, 320f), CardColor);

            string category = product.RewardType switch
            {
                GoldShopRewardType.PlayerExp => "TRAINING",
                GoldShopRewardType.FreeGachaStones => "STONE",
                GoldShopRewardType.Equipment => "EQUIPMENT",
                GoldShopRewardType.MonsterStorage => "STORAGE",
                _ => "ITEM"
            };

            CreateText("Category", card.transform, category, 16, FontStyle.Bold,
                new Vector2(0f, -30f), new Vector2(320f, 28f), TextSub, TextAnchor.MiddleCenter);
            CreateText("ProductName", card.transform, product.Title, 28, FontStyle.Bold,
                new Vector2(0f, -72f), new Vector2(350f, 46f), TextMain, TextAnchor.MiddleCenter);
            CreateText("Description", card.transform, product.Description, 21, FontStyle.Bold,
                new Vector2(0f, -130f), new Vector2(350f, 52f), TextSub, TextAnchor.MiddleCenter);

            Text priceText = CreateText("Price", card.transform, $"{product.Cost:N0} G", 28, FontStyle.Bold,
                new Vector2(0f, -184f), new Vector2(300f, 42f), AccentGold, TextAnchor.MiddleCenter);

            Button buyButton = CreateButton("BuyButton", card.transform, "購入", new Vector2(0f, -252f), new Vector2(260f, 70f),
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
            gameObject.SetActive(false);
            onClosed?.Invoke();
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
            image.sprite = Resources.Load<Sprite>("UI/FusionPage/FusionSmallButton");
            image.color = image.sprite != null ? Color.white : new Color(0.42f, 0.24f, 0.06f, 1f);
            image.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText("Label", root.transform, label, 23, FontStyle.Bold, new Vector2(0f, -15f), new Vector2(220f, 42f), TextMain, TextAnchor.MiddleCenter);
            return button;
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
