using System;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.UI
{
    // A modal text explanation, using the established UI artwork.
    public static class HelpDialog
    {
        public static RectTransform ActivePanel { get; private set; }

        public static GameObject Show(Transform parent, string name, string title, string body,
            string actionLabel = null, Action action = null)
        {
            var root = Rect(name, parent, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .86f);
            var panel = Rect("Panel", root, Vector2.zero, new Vector2(900, 1120));
            ActivePanel = panel;
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.015f, .024f, .035f, 1);
            var frame = Rect("Frame", panel, Vector2.zero, panel.sizeDelta).gameObject.AddComponent<Image>();
            frame.sprite = Resources.Load<Sprite>("UI/FusionPage/FusionMainFrame");
            frame.raycastTarget = false;
            var heading = Rect("TitleFrame", panel, new Vector2(0, 642), new Vector2(740, 124))
                .gameObject.AddComponent<Image>();
            heading.sprite = Resources.Load<Sprite>("UI/AudioSettings/SettingsPanelFrameImage2");
            heading.raycastTarget = false;
            Label("Title", panel, title, new Vector2(0, 642), new Vector2(640, 70), 38, TextAnchor.MiddleCenter);
            panel.Find("Title").GetComponent<Text>().fontStyle = FontStyle.Bold;
            Label("Body", panel, body, new Vector2(0, action == null ? 90 : 60),
                new Vector2(560, action == null ? 560 : 420), 28, TextAnchor.MiddleLeft);
            Action close = () => {
                if (ActivePanel == panel) ActivePanel = null;
                root.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
            };
            // Wide stacked buttons keep the enlarged labels clear of the ornate sides.
            if (action != null) AddButton("Action", panel, actionLabel, new Vector2(0, -232),
                "UI/HelpDialog/HelpActionButtonImage2", () => { close(); action(); });
            AddButton("Close", panel, "閉じる", new Vector2(0, -386),
                "UI/HelpDialog/HelpCloseButtonImage2", close);
            root.SetAsLastSibling();
            return root.gameObject;
        }

        private static void AddButton(string name, Transform parent, string title, Vector2 position, string spritePath, Action action)
        {
            var rect = Rect(name, parent, position, new Vector2(590, 144));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(spritePath);
            var button = rect.gameObject.AddComponent<FreshPressButton>();
            button.targetGraphic = image; button.onClick.AddListener(() => action());
            Label("Label", rect, title, Vector2.zero, new Vector2(410, 66), 36, TextAnchor.MiddleCenter);
            var label = rect.Find("Label").GetComponent<Text>();
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        private static void Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.text = value; text.color = new Color(.97f, .95f, .88f); text.alignment = alignment;
            text.raycastTarget = false;
        }
    }
}
