using UnityEngine;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        // Reuse the existing image2 settings art; no new raster assets are needed.
        internal const string AccountFramePath = "UI/AudioSettings/SettingsPanelFrameImage2";
        internal const string AccountButtonPath = "UI/AudioSettings/SettingsActionButtonImage2";
        internal const string AccountRecoverySwitchText = "このデータに切り替える";
        internal const string AccountRecoveryBackupNotice = "元のデータは切り替え前に退避します。";
        internal static readonly Color AccountPanelColor = new Color(0.025f, 0.045f, 0.075f, 1f);
        internal static readonly Color AccountButtonColor = new Color(0.045f, 0.12f, 0.18f, 1f);
        internal static readonly Color AccountDangerColor = new Color(0.23f, 0.035f, 0.045f, 1f);
        private Texture2D accountFrameTexture;
        private Texture2D accountButtonTexture;
        private GUIStyle accountLabelStyle;
        private GUIStyle accountTitleStyle;
        private GUIStyle accountCaptionStyle;
        private GUIStyle accountButtonStyle;

        private void PrepareAccountAppearance()
        {
            if (accountLabelStyle != null) return;
            accountFrameTexture = Resources.Load<Texture2D>(AccountFramePath);
            accountButtonTexture = Resources.Load<Texture2D>(AccountButtonPath);
            accountLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, wordWrap = true,
                normal = { textColor = Color.white }
            };
            accountTitleStyle = new GUIStyle(accountLabelStyle)
            {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.88f, 0.57f, 1f) }
            };
            accountCaptionStyle = new GUIStyle(accountLabelStyle)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.73f, 0.83f, 0.91f, 1f) }
            };
            // Frames are drawn separately, so skin transparency cannot leak through.
            accountButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24, wordWrap = true, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(55, 55, 18, 18), clipping = TextClipping.Clip
            };
            foreach (var state in new[] { accountButtonStyle.normal, accountButtonStyle.hover,
                accountButtonStyle.active, accountButtonStyle.focused, accountButtonStyle.onNormal,
                accountButtonStyle.onHover, accountButtonStyle.onActive, accountButtonStyle.onFocused })
            {
                state.background = null;
#if UNITY_EDITOR
                state.scaledBackgrounds = null;
#endif
                state.textColor = Color.white;
            }
            accountButtonStyle.hover.textColor = new Color(1f, 0.92f, 0.68f, 1f);
            accountButtonStyle.focused.textColor = accountButtonStyle.hover.textColor;
        }

        private static void AccountFill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawAccountPanel()
        {
            // Solid backing remains opaque even if the decorative frame has alpha.
            AccountFill(new Rect(55, 125, 610, 750), AccountPanelColor);
            if (accountFrameTexture != null)
                DrawAccountFrame(new Rect(15, 75, 690, 850));
        }

        private void DrawAccountFrame(Rect rect)
        {
            // Nine-slice the existing landscape image: preserve the corners on a tall dialog.
            float[] x = { rect.x, rect.x + 65, rect.xMax - 65, rect.xMax };
            float[] y = { rect.y, rect.y + 65, rect.yMax - 65, rect.yMax };
            float[] u = { 0, .17f, .83f, 1 };
            float[] v = { 0, .19f, .81f, 1 };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    GUI.DrawTextureWithTexCoords(new Rect(x[col], y[row], x[col + 1] - x[col], y[row + 1] - y[row]),
                        accountFrameTexture, new Rect(u[col], 1 - v[row + 1], u[col + 1] - u[col], v[row + 1] - v[row]));
        }

        internal static int AccountButtonFontSize(GUIStyle style, GUIContent content, Vector2 bounds)
        {
            int original = style.fontSize;
            try
            {
                // Measure with the actual platform font, leaving the ornamental
                // tips clear. Short labels retain the normal readable size.
                for (int size = original; size > 14; size--)
                {
                    style.fontSize = size;
                    if (style.CalcSize(content).x <= bounds.x &&
                        style.CalcHeight(content, bounds.x) <= bounds.y) return size;
                }
                return 14;
            }
            finally { style.fontSize = original; }
        }

        private bool AccountButton(string text, GUIStyle style, float height = 78, bool danger = false)
        {
            // Label length must not expand a button beyond its scroll viewport.
            var rect = GUILayoutUtility.GetRect(0f, height,
                GUILayout.Height(height), GUILayout.ExpandWidth(true));
            bool enabled = GUI.enabled;
            var oldColor = GUI.color;
            // Draw the backing/frame at full opacity even when interaction is disabled.
            GUI.enabled = true;
            AccountFill(new Rect(rect.x + 15, rect.y + 14, rect.width - 30, rect.height - 28),
                danger ? AccountDangerColor : AccountButtonColor);
            GUI.color = danger ? new Color(1f, 0.48f, 0.42f, 1f) : Color.white;
            if (accountButtonTexture != null)
                GUI.DrawTexture(rect, accountButtonTexture, ScaleMode.StretchToFill);
            GUI.color = oldColor;
            GUI.enabled = enabled;
            var content = new GUIContent(text);
            int previousFontSize = style.fontSize;
            try
            {
                style.fontSize = AccountButtonFontSize(style, content, rect.size);
                return GUI.Button(rect, content, style);
            }
            finally { style.fontSize = previousFontSize; }
        }
    }
}
