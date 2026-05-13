using System;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Home
{
    public sealed class GachaPanelController : MonoBehaviour
    {
        private const string BackgroundSpritePath = "UI/GachaPage/GachaBackground";
        private const string MainFrameSpritePath = "UI/GachaPage/GachaMainFrame";
        private const string BannerSpritePath = "UI/GachaPage/GachaBannerFrame";
        private const string PortalSpritePath = "UI/GachaPage/GachaPortal";
        private const string CapsuleSpritePath = "UI/GachaPage/GachaCapsule";
        private const string ResultSlotSpritePath = "UI/GachaPage/GachaResultSlot";
        private const string PullButtonSpritePath = "UI/GachaPage/GachaPullButton";
        private const string SmallButtonSpritePath = "UI/GachaPage/GachaSmallButton";
        private const string TicketSpritePath = "UI/GachaPage/GachaTicketIcon";
        private const string SparkleSpritePath = "UI/GachaPage/Effects/GachaSparkle_0";

        private static readonly Color PanelColor = new Color(0.065f, 0.075f, 0.11f, 0.94f);
        private static readonly Color GoldTextColor = new Color(1f, 0.82f, 0.45f, 1f);
        private static readonly Color PaleTextColor = new Color(0.88f, 0.93f, 1f, 0.96f);

        private Action closeAction;
        private bool built;
        private Text ticketText;
        private Text statusText;

        public void Show(Action onClose)
        {
            closeAction = onClose;
            if (!built)
            {
                Build();
                built = true;
            }

            gameObject.SetActive(true);
            UpdatePreviewState();
        }

        private void Build()
        {
            transform.SetAsLastSibling();
            ClearChildren();

            CreateFullScreenImage("GachaBackground", transform, BackgroundSpritePath, new Color(0.02f, 0.025f, 0.045f, 1f));

            GameObject panel = CreatePanel("GachaMainPanel", transform, MainFrameSpritePath,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1680f), PanelColor);

            CreateImage("GachaBanner", panel.transform, BannerSpritePath, new Vector2(0.5f, 1f),
                new Vector2(0f, -152f), new Vector2(780f, 210f), true, Color.white);
            CreateText("Title", panel.transform, "召喚ガチャ", 64, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(680f, 90f), GoldTextColor);
            CreateText("SubTitle", panel.transform, "魔塔の契約陣から仲間を呼び出す準備画面", 25, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(720f, 46f), PaleTextColor);

            GameObject portalPanel = CreatePanel("GachaPortalPanel", panel.transform, null,
                new Vector2(0.5f, 0.63f), new Vector2(0f, -18f), new Vector2(760f, 690f), new Color(0.045f, 0.055f, 0.085f, 0.88f));
            CreateImage("GachaPortal", portalPanel.transform, PortalSpritePath, new Vector2(0.5f, 0.55f),
                new Vector2(0f, 28f), new Vector2(600f, 600f), true, Color.white);
            CreateImage("GachaSparkle", portalPanel.transform, SparkleSpritePath, new Vector2(0.5f, 0.55f),
                new Vector2(0f, 38f), new Vector2(650f, 650f), true, Color.white);
            CreateImage("GachaCapsule", portalPanel.transform, CapsuleSpritePath, new Vector2(0.5f, 0.55f),
                new Vector2(0f, 16f), new Vector2(230f, 230f), true, Color.white);
            CreateText("PortalLabel", portalPanel.transform, "契約陣 待機中", 30, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(420f, 48f), GoldTextColor);

            GameObject ratesPanel = CreatePanel("GachaRatesPanel", panel.transform, null,
                new Vector2(0.5f, 0.30f), new Vector2(0f, 20f), new Vector2(760f, 250f), new Color(0.055f, 0.065f, 0.095f, 0.92f));
            CreateText("RatesTitle", ratesPanel.transform, "提供割合プレビュー", 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(480f, 36f), GoldTextColor);
            CreateRateChip(ratesPanel.transform, "Class4", "C4", "3%", new Vector2(-250f, -14f), new Color(0.92f, 0.60f, 0.95f, 1f));
            CreateRateChip(ratesPanel.transform, "Upper", "上級", "12%", new Vector2(0f, -14f), new Color(0.55f, 0.77f, 1f, 1f));
            CreateRateChip(ratesPanel.transform, "Middle", "中級", "35%", new Vector2(250f, -14f), new Color(0.58f, 0.94f, 0.70f, 1f));
            CreateText("RatesNote", ratesPanel.transform, "排出テーブル・消費通貨・天井は後続実装で接続", 19, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(640f, 34f), new Color(0.72f, 0.78f, 0.88f, 0.94f));

            GameObject resultPanel = CreatePanel("GachaResultPreview", panel.transform, ResultSlotSpritePath,
                new Vector2(0.5f, 0.14f), new Vector2(0f, 0f), new Vector2(760f, 230f), new Color(0.06f, 0.07f, 0.10f, 0.90f));
            CreateText("ResultTitle", resultPanel.transform, "結果表示エリア", 25, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(520f, 38f), GoldTextColor);
            statusText = CreateText("ResultStatus", resultPanel.transform, "未召喚", 34, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(540f, 54f), PaleTextColor);
            CreateText("ResultHint", resultPanel.transform, "ここに獲得モンスター演出とカードを表示予定", 19, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(620f, 34f), new Color(0.72f, 0.78f, 0.88f, 0.94f));

            GameObject ticketPanel = CreatePanel("GachaTicketPanel", panel.transform, null,
                new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(760f, 110f), new Color(0.07f, 0.08f, 0.11f, 0.92f));
            CreateImage("TicketIcon", ticketPanel.transform, TicketSpritePath, new Vector2(0f, 0.5f),
                new Vector2(78f, 0f), new Vector2(74f, 74f), true, Color.white);
            ticketText = CreateText("TicketCount", ticketPanel.transform, "召喚券 0 / 魔晶石 0", 28, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(46f, 0f), new Vector2(520f, 44f), PaleTextColor);

            Button singleButton = CreateSpriteButton("SinglePullButton", panel.transform, PullButtonSpritePath, "1回召喚",
                new Vector2(-205f, 92f), new Vector2(340f, 104f), PreviewPull);
            Button tenButton = CreateSpriteButton("TenPullButton", panel.transform, PullButtonSpritePath, "10回召喚",
                new Vector2(205f, 92f), new Vector2(340f, 104f), PreviewPull);
            Button closeButton = CreateSpriteButton("BackButton", panel.transform, SmallButtonSpritePath, "戻る",
                new Vector2(0f, 26f), new Vector2(260f, 78f), Close);
            singleButton.interactable = Application.isPlaying;
            tenButton.interactable = Application.isPlaying;
            closeButton.interactable = true;
        }

        private void UpdatePreviewState()
        {
            if (ticketText != null)
            {
                ticketText.text = "召喚券 0 / 魔晶石 0";
            }

            if (statusText != null)
            {
                statusText.text = Application.isPlaying ? "召喚準備中" : "エディタプレビュー";
            }
        }

        private void PreviewPull()
        {
            if (statusText != null)
            {
                statusText.text = "ガチャロジック未接続";
            }
        }

        private void Close()
        {
            if (closeAction != null)
            {
                closeAction.Invoke();
                return;
            }

            gameObject.SetActive(false);
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, string spritePath, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color fallbackColor)
        {
            GameObject panel = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Image image = panel.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return panel;
        }

        private static Image CreateFullScreenImage(string name, Transform parent, string spritePath, Color fallbackColor)
        {
            GameObject imageObject = CreateUiObject(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image image = imageObject.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static Image CreateImage(string name, Transform parent, string spritePath, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool preserveAspect, Color color)
        {
            GameObject imageObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Image image = imageObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.color = image.sprite != null ? color : new Color(color.r, color.g, color.b, 0.18f);
            image.preserveAspect = preserveAspect;
            return image;
        }

        private static Button CreateSpriteButton(string name, Transform parent, string spritePath, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateUiObject(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPosition, size);
            Image image = buttonObject.AddComponent<Image>();
            Sprite sprite = LoadSprite(spritePath);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0.34f, 0.19f, 0.08f, 0.95f);
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            CreateText("Label", buttonObject.transform, label, 29, FontStyle.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 18f), Color.white);
            return button;
        }

        private static void CreateRateChip(Transform parent, string name, string rarity, string rate, Vector2 anchoredPosition, Color accentColor)
        {
            GameObject chip = CreatePanel(name + "RateChip", parent, null, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(190f, 96f), new Color(0.08f, 0.09f, 0.13f, 0.92f));
            Image image = chip.GetComponent<Image>();
            image.color = new Color(accentColor.r * 0.18f, accentColor.g * 0.18f, accentColor.b * 0.18f, 0.94f);
            CreateText(name + "Rarity", chip.transform, rarity, 24, FontStyle.Bold, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(160f, 32f), accentColor);
            CreateText(name + "Rate", chip.transform, rate, 30, FontStyle.Bold, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(160f, 38f), Color.white);
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject textObject = CreateUiObject(name, parent, anchor, anchor, anchoredPosition, size);
            Text label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            return gameObject;
        }

        private static Sprite LoadSprite(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }
    }
}
