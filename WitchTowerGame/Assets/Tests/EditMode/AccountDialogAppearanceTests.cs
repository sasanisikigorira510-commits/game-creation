using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AccountDialogAppearanceTests
    {
        private static object Field(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower.Save.OnlinePlayerData", true)
            .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        [Test]
        public void AllDialogBackingsAreOpaqueAndDangerIsDistinct()
        {
            var panel = (Color)Field("AccountPanelColor");
            var button = (Color)Field("AccountButtonColor");
            var danger = (Color)Field("AccountDangerColor");
            Assert.AreEqual(1f, panel.a);
            Assert.AreEqual(1f, button.a);
            Assert.AreEqual(1f, danger.a);
            Assert.Less(panel.grayscale, .15f);
            Assert.Greater(danger.r, danger.b * 3);
        }

        [TestCase("AccountFramePath")]
        [TestCase("AccountButtonPath")]
        public void ExistingImage2ArtCanBeLoadedAsGuiTexture(string field)
        {
            string path = (string)Field(field);
            var texture = Resources.Load<Texture2D>(path);
            Assert.NotNull(texture);
            Assert.Greater(texture.width, 100);
            Assert.Greater(texture.height, 100);
        }

        [Test]
        public void RecoveryActionAndBackupNoticeAreSeparate()
        {
            Assert.AreEqual("このデータに切り替える", Field("AccountRecoverySwitchText"));
            StringAssert.Contains("退避", (string)Field("AccountRecoveryBackupNotice"));
        }

        [TestCase("このデータに切り替える", 540f, 95f)]
        [TestCase("確認しました：Apple連携を解除する", 540f, 90f)]
        [TestCase("ゲームアカウントを削除（次に確認）", 540f, 78f)]
        public void ButtonTextFitsDecorativeInsetsAndDoesNotChangeSharedStyle(string text, float width, float height)
        {
            var style = new GUIStyle { fontSize = 24, wordWrap = true,
                padding = new RectOffset(55, 55, 18, 18) };
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.Save.OnlinePlayerData", true);
            var method = type.GetMethod("AccountButtonFontSize", BindingFlags.Static | BindingFlags.NonPublic);
            var content = new GUIContent(text);
            int fitted = (int)method.Invoke(null, new object[] { style, content, new Vector2(width, height) });
            Assert.AreEqual(24, style.fontSize);
            Assert.That(fitted, Is.InRange(14, 24));
            style.fontSize = fitted;
            Assert.LessOrEqual(style.CalcSize(content).x, width);
            Assert.LessOrEqual(style.CalcHeight(content, width), height);
        }
    }
}
