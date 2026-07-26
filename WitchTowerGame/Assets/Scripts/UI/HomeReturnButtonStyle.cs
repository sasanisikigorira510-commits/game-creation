using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace WitchTower.UI
{
    public static class HomeReturnButtonStyle
    {
        public const string DefaultObjectName = "HomeReturnButton";
        private const string LabelPlateObjectName = "HomeReturnLabelPlate";
        private const string LabelObjectName = "HomeReturnLabel";
        public const string LabelText = "ホームへ戻る";

        public static readonly Vector2 Anchor = Vector2.up;
        public static readonly Vector2 Pivot = Vector2.up;
        public static readonly Vector2 AnchoredPosition = new Vector2(34f, -34f);
        public static readonly Vector2 Size = new Vector2(240f, 78f);

        private static readonly Color BackgroundColor = new Color(0.56f, 0.2f, 0.07f, 1f);
        private static readonly Color HighlightColor = new Color(0.82f, 0.36f, 0.12f, 1f);
        private static readonly Color PressedColor = new Color(0.38f, 0.12f, 0.04f, 1f);
        private static readonly Color DisabledColor = new Color(0.18f, 0.16f, 0.15f, 0.52f);
        private static readonly Color ButtonOutlineColor = new Color(0.95f, 0.62f, 0.24f, 0.9f);
        private static readonly Color LabelPlateColor = new Color(0.76f, 0.31f, 0.1f, 1f);
        private static readonly Color LabelPlateOutlineColor = new Color(1f, 0.78f, 0.34f, 0.95f);
        private static readonly Color TextColor = new Color(1f, 0.99f, 0.92f, 1f);
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.82f);

        public static Button Create(Transform parent, UnityAction onClick)
        {
            return Create(parent, DefaultObjectName, onClick);
        }

        public static Button Create(Transform parent, string objectName, UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Button button = buttonObject.GetComponent<Button>();
            Apply(button, LabelText);
            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            buttonObject.transform.SetAsLastSibling();
            return button;
        }

        public static void Apply(Button button, string labelText = LabelText)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            ApplyLayout(rect);

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            image.color = BackgroundColor;
            image.raycastTarget = true;
            ApplyButtonOutline(button.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = BackgroundColor;
            colors.highlightedColor = HighlightColor;
            colors.pressedColor = PressedColor;
            colors.selectedColor = HighlightColor;
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            HideLegacyChildren(button.transform);

            Image labelPlate = button.transform.Find(LabelPlateObjectName)?.GetComponent<Image>();
            if (labelPlate == null)
            {
                GameObject plateObject = new GameObject(LabelPlateObjectName, typeof(RectTransform), typeof(Image));
                plateObject.transform.SetParent(button.transform, false);
                labelPlate = plateObject.GetComponent<Image>();
            }
            else
            {
                labelPlate.gameObject.SetActive(true);
            }

            ApplyLabelPlate(labelPlate);

            Text label = button.transform.Find(LabelObjectName)?.GetComponent<Text>();
            if (label == null)
            {
                GameObject labelObject = new GameObject(LabelObjectName, typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(button.transform, false);
                label = labelObject.GetComponent<Text>();
            }
            else
            {
                label.gameObject.SetActive(true);
            }

            ApplyLabel(label, labelText);
            labelPlate.transform.SetAsLastSibling();
            label.transform.SetAsLastSibling();
        }

        public static void ApplyLayout(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Anchor;
            rect.anchorMax = Anchor;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = Size;
            rect.localScale = Vector3.one;
        }

        private static void ApplyLabelPlate(Image plate)
        {
            if (plate == null)
            {
                return;
            }

            RectTransform plateRect = plate.GetComponent<RectTransform>();
            plateRect.anchorMin = Vector2.zero;
            plateRect.anchorMax = Vector2.one;
            plateRect.offsetMin = new Vector2(6f, 7f);
            plateRect.offsetMax = new Vector2(-6f, -7f);
            plateRect.localScale = Vector3.one;

            plate.color = LabelPlateColor;
            plate.raycastTarget = false;
            plate.enabled = true;

            Outline outline = plate.GetComponent<Outline>();
            if (outline == null)
            {
                outline = plate.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = LabelPlateOutlineColor;
            outline.effectDistance = new Vector2(1.8f, -1.8f);
        }

        private static void ApplyButtonOutline(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            Outline outline = buttonObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonObject.AddComponent<Outline>();
            }

            outline.effectColor = ButtonOutlineColor;
            outline.effectDistance = new Vector2(2.4f, -2.4f);
        }

        private static void ApplyLabel(Text label, string labelText)
        {
            if (label == null)
            {
                return;
            }

            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 9f);
            labelRect.offsetMax = new Vector2(-16f, -9f);
            labelRect.localScale = Vector3.one;

            label.text = string.IsNullOrEmpty(labelText) ? LabelText : labelText;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.lineSpacing = 1f;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = TextColor;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 24;
            label.raycastTarget = false;

            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
            {
                outline = label.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(1.6f, -1.6f);
        }

        private static void HideLegacyChildren(Transform buttonTransform)
        {
            if (buttonTransform == null)
            {
                return;
            }

            for (int i = 0; i < buttonTransform.childCount; i += 1)
            {
                Transform child = buttonTransform.GetChild(i);
                if (child != null)
                {
                    TMP_Text[] tmpLabels = child.GetComponentsInChildren<TMP_Text>(true);
                    for (int labelIndex = 0; labelIndex < tmpLabels.Length; labelIndex += 1)
                    {
                        if (tmpLabels[labelIndex] != null)
                        {
                            tmpLabels[labelIndex].text = LabelText;
                        }
                    }

                    Text[] uiLabels = child.GetComponentsInChildren<Text>(true);
                    for (int labelIndex = 0; labelIndex < uiLabels.Length; labelIndex += 1)
                    {
                        if (uiLabels[labelIndex] != null)
                        {
                            uiLabels[labelIndex].text = LabelText;
                        }
                    }

                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}
