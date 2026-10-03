using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class OnlineNotificationLayoutTests
    {
        [TestCase(1170f, 2532f, 66f, 190f, 265f, 86f)]
        [TestCase(1080f, 1920f, 64f, 48f, 240f, 78f)]
        [TestCase(640f, 1136f, 38f, 90f, 142f, 46f)]
        public void NotificationDoesNotCoverReturnButtonOrItsTutorialPrompt(float width, float height,
            float x, float y, float buttonWidth, float buttonHeight)
        {
            var message = new Rect(width * .05f, height * .08f, width * .9f, 70f);
            var button = new Rect(x, y, buttonWidth, buttonHeight);
            float gap = Mathf.Max(12f, width * .02f);
            var result = Avoid(message, button, gap);
            Assert.That(result.Overlaps(Rect.MinMaxRect(button.xMin - gap, button.yMin - gap,
                button.xMax + gap, button.yMax + gap * 3f)), Is.False);
            Assert.That(result.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(result.width, Is.GreaterThanOrEqualTo(120f));
            Assert.That(result.center.x, Is.EqualTo(message.center.x), "Never shift a notice beside navigation.");
            Assert.That(result.width, Is.EqualTo(message.width));
            Assert.That(Avoid(result, button, gap), Is.EqualTo(result), "Layout must not drift between GUI events.");
        }

        [Test]
        public void NarrowScreenMovesMessageBelowPromptInsteadOfTruncatingIt()
        {
            var result = Avoid(new Rect(10, 100, 300, 70), new Rect(10, 100, 250, 60), 12);
            Assert.That(result, Is.EqualTo(new Rect(10, 196, 300, 70)));
        }

        [Test]
        public void NonOverlappingMessageKeepsItsPosition()
        {
            var message = new Rect(40, 300, 700, 70);
            Assert.That(Avoid(message, new Rect(64, 48, 240, 78), 12), Is.EqualTo(message));
        }

        [TestCase(1170f, 2532f)]
        [TestCase(1179f, 2556f)]
        [TestCase(640f, 1136f)]
        [TestCase(1080f, 1920f)]
        public void LargeNoticeIsCenteredBelowHeaderAndClearOfSummonButtons(float width, float height)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.Save.OnlinePlayerData", true);
            float panelHeight = 240f * width / 1080f;
            var result = (Rect)type.GetMethod("StatusNotificationBounds", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { width, height, new Rect(0, 44, width, height - 100), panelHeight });
            Assert.That(result.center.x, Is.EqualTo(width / 2).Within(.01));
            Assert.That(result.width, Is.EqualTo(width * .92f).Within(.01));
            Assert.That(result.yMin, Is.GreaterThanOrEqualTo(height * .25f));
            Assert.That(result.yMax, Is.LessThan(height * .5f));
        }

        private static Rect Avoid(Rect message, Rect button, float gap)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.Save.OnlinePlayerData", true);
            return (Rect)type.GetMethod("AvoidReturnButton", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { message, button, gap });
        }
    }
}
