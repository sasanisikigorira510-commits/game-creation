using UnityEngine;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        private GUIStyle statusBodyStyle;
        private GUIStyle statusHeadingStyle;
        private GUIStyle statusCloseStyle;
        private float statusNotificationHeight;

        private static Rect StatusNotificationBounds(float width, float height, Rect safeArea, float panelHeight)
        {
            float margin = width * .04f;
            float left = Mathf.Max(margin, safeArea.xMin + margin);
            float right = Mathf.Min(width - margin, safeArea.xMax - margin);
            float top = Mathf.Max(height * .25f, height - safeArea.yMax + margin);
            top = Mathf.Min(top, height - safeArea.yMin - margin - panelHeight);
            return new Rect(left, Mathf.Max(0, top), right - left, panelHeight);
        }

        private void DrawStatusNotification()
        {
            PrepareAccountAppearance();
            if (statusBodyStyle == null)
            {
                statusBodyStyle = new GUIStyle(accountLabelStyle)
                {
                    fontSize = 32, alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(), richText = false
                };
                statusHeadingStyle = new GUIStyle(accountTitleStyle)
                {
                    fontSize = 32, padding = new RectOffset(), margin = new RectOffset()
                };
                statusCloseStyle = new GUIStyle(accountButtonStyle) { fontSize = 38, padding = new RectOffset() };
            }
            // Use design units, not IMGUI's tiny default pixel font on high-DPI iPhones.
            float scale = Screen.width / 1080f;
            var bounds = StatusNotificationBounds(Screen.width, Screen.height, Screen.safeArea, 0);
            float logicalWidth = bounds.width / scale;
            // The nine-sliced artwork occupies the first/last 65 design units.
            // Keep both lines inside that border instead of over its top ornament.
            const float headingTop = 68, headingHeight = 46, bodyTop = 126, bottomInset = 68;
            float bodyHeight = Mathf.Max(64, statusBodyStyle.CalcHeight(new GUIContent(LastMessage), logicalWidth - 168));
            statusNotificationHeight = (bodyTop + bodyHeight + bottomInset) * scale;
            var rect = PlaceNotificationAboveHelp(ResolveStatusMessageRect());
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y), Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                float w = rect.width / scale, h = rect.height / scale;
                AccountFill(new Rect(22, 22, w - 44, h - 44), AccountPanelColor);
                if (accountFrameTexture != null) DrawAccountFrame(new Rect(0, 0, w, h));
                GUI.Label(new Rect(84, headingTop, w - 168, headingHeight),
                    LastMessage == lastSyncFailure ? "通信・データの確認" : "お知らせ", statusHeadingStyle);
                GUI.Label(new Rect(84, bodyTop, w - 168, bodyHeight), LastMessage, statusBodyStyle);
                if (GUI.Button(new Rect(w - 150, 60, 64, 64), "×", statusCloseStyle)) DismissStatusNotification();
                // Consume taps on the notice, without turning its whole surface
                // into a dismiss button or passing them to a game button behind it.
                if (Event.current.isMouse && new Rect(0, 0, w, h).Contains(Event.current.mousePosition))
                    Event.current.Use();
            }
            finally { GUI.matrix = previous; GUI.color = previousColor; }
        }

        private static Rect PlaceNotificationAboveHelp(Rect notice)
        {
            var panel = WitchTower.UI.HelpDialog.ActivePanel;
            if (panel == null || !panel.gameObject.activeInHierarchy) return notice;
            var canvas = panel.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera,
                panel.TransformPoint(new Vector3(panel.rect.xMin, panel.rect.yMin, 0)));
            var heading = panel.Find("TitleFrame") as RectTransform;
            float top = heading != null ? Mathf.Max(panel.rect.yMax,
                heading.anchoredPosition.y + heading.rect.yMax) : panel.rect.yMax;
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera,
                panel.TransformPoint(new Vector3(panel.rect.xMax, top, 0)));
            var panelBounds = Rect.MinMaxRect(bottomLeft.x, Screen.height - topRight.y,
                topRight.x, Screen.height - bottomLeft.y);
            if (notice.Overlaps(panelBounds))
            {
                float gap = Screen.width * .022f;
                float safeTop = Screen.height - Screen.safeArea.yMax + gap;
                notice.y = Mathf.Max(safeTop, panelBounds.yMin - notice.height - gap);
            }
            return notice;
        }

        private void DismissStatusNotification()
        {
            if (!string.IsNullOrEmpty(lastSyncFailure) && LastMessage == lastSyncFailure)
                dismissedFailureNoticeKey = lastFailureNoticeKey;
            LastMessage = string.Empty;
        }
    }
}
