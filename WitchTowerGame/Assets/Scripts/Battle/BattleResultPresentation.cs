using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Battle
{
    internal static class BattleResultPresentation
    {
        internal static void ApplyBackground(Image image, bool isWin)
        {
            if (image == null) return;
            image.sprite = BattleVisualResolver.LoadSprite(isWin
                ? "UI/BattleResult/BattleResultPanelImage2"
                : "UI/BattleResult/BattleResultDefeatPanelImage2");
            image.color = image.sprite != null ? Color.white
                : isWin ? new Color(.04f, .09f, .16f, 1f) : new Color(.24f, .025f, .035f, 1f);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        internal static void EnlargeRewardSlot(GameObject slot)
        {
            ((RectTransform)slot.transform).sizeDelta = new Vector2(200f, 300f);
            SetImage(slot.transform.Find("Icon") as RectTransform, 120f);
            SetImage(slot.transform.Find("Frame") as RectTransform, 154f);
            SetLabel(slot.transform.Find("Label")?.GetComponent<Text>(), 0f, .22f, 24);
            SetLabel(slot.transform.Find("Detail")?.GetComponent<Text>(), .75f, 1f, 22);
        }

        private static void SetImage(RectTransform rect, float size)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(size, size);
        }

        private static void SetLabel(Text text, float bottom, float top, int size)
        {
            if (text == null) return;
            text.rectTransform.anchorMin = new Vector2(.02f, bottom);
            text.rectTransform.anchorMax = new Vector2(.98f, top);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = size;
        }
    }
}
