using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.UI
{
    /// <summary>Shared image2 tutorial target, with no raycast surface of its own.</summary>
    [ExecuteAlways]
    public sealed class TutorialTargetFrame : MonoBehaviour
    {
        private static Sprite frameSprite;
        private Image frame;
        public static void SetVisible(Transform target, bool visible, string caption = "", bool captionBelow = false)
        {
            if (target == null) return;
            Transform existing = target.Find("TutorialTargetFrame");
            if (existing == null && !visible) return;
            if (existing == null)
            {
                var root = new GameObject("TutorialTargetFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TutorialTargetFrame));
                root.transform.SetParent(target, false);
                root.layer = target.gameObject.layer;
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-10, -10); rect.offsetMax = new Vector2(10, 10);
                var component = root.GetComponent<TutorialTargetFrame>();
                component.frame = root.GetComponent<Image>();
                if (frameSprite == null)
                {
                    var texture = Resources.Load<Texture2D>("UI/Tutorial/TutorialSummonHighlightFrameImage2");
                    if (texture != null) frameSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100, 0,
                        SpriteMeshType.FullRect, new Vector4(texture.width * .1f, texture.height * .12f, texture.width * .1f, texture.height * .12f));
                }
                component.frame.sprite = frameSprite;
                component.frame.type = Image.Type.Sliced;
                component.frame.pixelsPerUnitMultiplier = 3f;
                component.frame.raycastTarget = false;
                existing = root.transform;
                var labelObject = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(existing, false);
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = new Vector2(.5f,1); labelRect.anchorMax = labelRect.anchorMin;
                labelRect.pivot = new Vector2(.5f,0); labelRect.anchoredPosition = new Vector2(0,6);
                labelRect.sizeDelta = new Vector2(260,36);
                var text = labelObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 26;
                text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f,.9f,.2f); text.raycastTarget = false;
                var outline = labelObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(2,-2);
            }
            var captionText = existing.GetComponentInChildren<Text>(true);
            captionText.text = caption;
            var captionRect = captionText.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(.5f, captionBelow ? 0f : 1f);
            captionRect.pivot = new Vector2(.5f, captionBelow ? 1f : 0f);
            captionRect.anchoredPosition = new Vector2(0f, captionBelow ? -6f : 6f);
            existing.gameObject.SetActive(visible);
            if (visible) existing.SetAsLastSibling();
        }
        private void Update()
        {
            if (frame == null) frame = GetComponent<Image>();
            float pulse = .5f + .5f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
            frame.color = new Color(1f, .82f, .12f, Mathf.Lerp(.45f, 1f, pulse));
        }
    }
}
