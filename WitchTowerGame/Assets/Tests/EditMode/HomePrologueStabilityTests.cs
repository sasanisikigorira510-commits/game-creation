using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class HomePrologueStabilityTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(1920f)]
        [TestCase(2341f)]
        public void PrologueTitleShadeFitsTheHeading(float height)
        {
            var owner = new GameObject("TitleShadeController");
            owner.SetActive(false);
            var menu = new GameObject("TitleShadeCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                menu.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)menu.transform;
                canvas.sizeDelta = new Vector2(1080f, height);
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Home.HomeSceneController", true);
                var controller = owner.AddComponent(type);
                type.GetMethod("EnsureHomePrologue", Private).Invoke(controller, new object[] { menu.transform });
                type.GetMethod("ShowHomeProloguePage", Private).Invoke(controller, new object[] { 0 });
                var root = menu.transform.Find("HomePrologue");
                var title = root.Find("HomePrologueTitle").GetComponent<Text>();
                var shade = root.Find("HomePrologueUpperShade").GetComponent<Image>();
                Assert.That(shade.rectTransform.anchoredPosition, Is.EqualTo(title.rectTransform.anchoredPosition));
                Assert.That(shade.rectTransform.rect.width, Is.EqualTo(title.preferredWidth + 64f).Within(.1f));
                Assert.That(shade.rectTransform.rect.height, Is.EqualTo(title.preferredHeight + 32f).Within(.1f));
                Assert.That(shade.rectTransform.rect.width, Is.LessThan(600f));
                Assert.That(shade.rectTransform.rect.height, Is.LessThan(100f));
                Assert.That(title.fontSize, Is.EqualTo(42));
                Assert.That(shade.raycastTarget, Is.False);
                CaptureIfRequested(canvas, "prologue-title-" + height + ".png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(menu);
            }
        }

        [TestCase(1920f)]
        [TestCase(2341f)]
        public void FirstSummonGuideHasNoOrphanedSentenceEnding(float height)
        {
            var owner = new GameObject("SummonGuideController");
            owner.SetActive(false);
            var menu = new GameObject("SummonGuideCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                menu.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)menu.transform;
                canvas.sizeDelta = new Vector2(1080f, height);
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp");
                var type = assembly.GetType("WitchTower.Home.HomeSceneController", true);
                var controller = owner.AddComponent(type);
                type.GetMethod("EnsureHomeGuidePanel", Private).Invoke(controller, new object[] { menu.transform });
                var saveType = assembly.GetType("WitchTower.Save.PlayerSaveData", true);
                var save = saveType.GetMethod("CreateDefault").Invoke(null, null);
                saveType.GetField("TutorialStepId").SetValue(save, "T01");
                var profile = Activator.CreateInstance(assembly.GetType("WitchTower.Data.PlayerProfile", true), new[] { save });
                var story = assembly.GetType("WitchTower.Data.StoryTutorialService", true);
                var lesson = story.GetMethod("GetNextEvent").Invoke(null, new[] { profile, "HomeScene" });
                var panel = menu.transform.Find("HomeGuidePanel");
                var body = panel.Find("HomeGuideText").GetComponent<Text>();
                body.text = (string)lesson.GetType().GetProperty("Body").GetValue(lesson);
                panel.Find("HomeGuideTitleText").GetComponent<Text>().text = "最初の召喚";
                panel.Find("HomeNextFloorText").GetComponent<Text>().text = "次の操作: 召喚を開く";
                Canvas.ForceUpdateCanvases();
                var generator = new TextGenerator();
                Assert.That(generator.Populate(body.text, body.GetGenerationSettings(body.rectTransform.rect.size)), Is.True);
                Assert.That(generator.lineCount, Is.EqualTo(3), "Each phrase should occupy one complete line.");
                Assert.That(generator.fontSizeUsedForBestFit, Is.GreaterThanOrEqualTo(28));
                for (int i = 0; i < generator.lines.Count; i++)
                {
                    int start = generator.lines[i].startCharIdx;
                    int end = i + 1 < generator.lines.Count ? generator.lines[i + 1].startCharIdx : body.text.Length;
                    Assert.That(body.text.Substring(start, end - start).Trim().Length, Is.GreaterThan(5),
                        "Do not strand a short ending such as す。 on its own line.");
                }
                CaptureIfRequested(canvas, "summon-guide-" + height + ".png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(menu);
            }
        }

        private static void CaptureIfRequested(RectTransform canvas, string name)
        {
            string directory = Environment.GetEnvironmentVariable("HOME_TUTORIAL_LAYOUT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { canvas, 1179, (int)(1179f * canvas.rect.height / 1080f), Path.Combine(directory, name) });
        }

        [TestCase(102f, 2391f, 2112.92f, 102f, 180f, 166.154f)]
        [TestCase(0f, 1920f, 1920f, 0f, 150f, 150f)]
        [TestCase(282f, 2391f, 2109f, 102f, 180f, 0f)]
        [TestCase(0f, 1920f, 1920f, 102f, 0f, 0f)]
        public void BannerInsetReservesOnlyUncoveredScreenPixels(float bottom, float top,
            float height, float safeBottom, float banner, float expected)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.Home.HomeSceneController", true);
            float inset = (float)type.GetMethod("CalculateHomePrologueBannerInset", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { bottom, top, height, safeBottom, banner });
            Assert.That(inset, Is.EqualTo(expected).Within(.01f));
        }

        [Test]
        public void DelayedAndResizedBannerMovesAllDialogueControlsWithoutDrift()
        {
            var owner = new GameObject("PrologueBannerController");
            owner.SetActive(false);
            var menu = new GameObject("PrologueBannerMenu", typeof(RectTransform));
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Home.HomeSceneController", true);
                var controller = owner.AddComponent(type);
                type.GetMethod("EnsureHomePrologue", Private).Invoke(controller, new object[] { menu.transform });
                var root = menu.transform.Find("HomePrologue");
                string[] names = { "DialoguePanel", "Speaker", "Body", "Progress", "Prompt", "LowerShade" };
                var rects = names.Select(n => (RectTransform)root.Find("HomePrologue" + n)).ToArray();
                var positions = rects.Select(r => r.anchoredPosition).ToArray();
                var background = (RectTransform)root.Find("HomePrologueBackground");
                var backgroundPosition = background.anchoredPosition;
                foreach (float inset in new[] { 0f, 166f, 166f, 220f, 0f })
                {
                    type.GetMethod("ApplyHomePrologueBannerInset", Private).Invoke(controller, new object[] { inset });
                    for (int i = 0; i < rects.Length; i++)
                        Assert.That(rects[i].anchoredPosition, Is.EqualTo(positions[i] + Vector2.up * inset), names[i]);
                    // The lowest control still has 41 canvas units of clearance.
                    Assert.That(rects[4].anchoredPosition.y - rects[4].rect.height / 2f, Is.GreaterThan(inset));
                    Assert.That(background.anchoredPosition, Is.EqualTo(backgroundPosition));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(menu);
            }
        }

        [Test]
        public void OpenShopLessonHighlightsTheShopNavigation()
        {
            var owner = new GameObject("ShopPulseController");
            owner.SetActive(false);
            var menu = new GameObject("ShopPulseMenu", typeof(RectTransform));
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp");
                var type = assembly.GetType("WitchTower.Home.HomeSceneController", true);
                var saveType = assembly.GetType("WitchTower.Save.PlayerSaveData", true);
                var save = saveType.GetMethod("CreateDefault").Invoke(null, null);
                saveType.GetField("TutorialStepId").SetValue(save, "T07C_SHOP");
                saveType.GetField("InitialTutorialSummonCount").SetValue(save, 3);
                object profile = Activator.CreateInstance(assembly.GetType("WitchTower.Data.PlayerProfile", true), new[] { save });
                var controller = owner.AddComponent(type);
                var refresh = type.GetMethod("RefreshFirstSummonTutorialPulse", Private);
                refresh.Invoke(controller, new object[] { profile, menu.transform });
                var root = (GameObject)type.GetField("homeFirstSummonPulseRoot", Private).GetValue(controller);
                Assert.That(root, Is.Not.Null, "tutorial_open_shop must highlight home.shop just like tutorial_shop.");
                Assert.That(root.activeSelf, Is.True);
                var frame = root.GetComponent<Image>();
                Assert.That(frame.sprite, Is.Not.Null);
                Assert.That(frame.sprite.texture.name, Is.EqualTo("TutorialSummonHighlightFrameImage2"));
                Assert.That(frame.raycastTarget, Is.False);
                float shopX = (float)type.GetMethod("GetBottomNavButtonX", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { 0 });
                float offset = (float)type.GetField("ShopHighlightOffsetX", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
                Assert.That(frame.rectTransform.anchoredPosition.x, Is.EqualTo(shopX + offset));
                type.GetMethod("AnimateFirstSummonTutorialPulse", Private).Invoke(controller, null);
                Assert.That(frame.color.a, Is.InRange(.72f, 1f));
                refresh.Invoke(controller, new object[] { null, menu.transform });
                Assert.That(root.activeSelf, Is.False, "Clear the frame when there is no active lesson.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(menu);
            }
        }

        [TestCase(1080, 1920)]
        [TestCase(1080, 2341)]
        public void AllSixNarrationPagesKeepStableTextAndLayout(int width, int height)
        {
            var owner = new GameObject("PrologueTestController");
            owner.SetActive(false);
            var canvasObject = new GameObject("PrologueTestCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Home.HomeSceneController", true);
                var controller = owner.AddComponent(type);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(width, height);
                Action<string, object[]> invoke = (name, args) => type.GetMethod(name, Private).Invoke(controller, args);
                invoke("EnsureHomePrologue", new object[] { canvas.transform });
                invoke("ShowHomeProloguePage", new object[] { 0 });
                var root = canvas.transform.Find("HomePrologue");
                string[] names = { "HomePrologueBackground", "HomePrologueDialoguePanel", "HomePrologueGuide",
                    "HomePrologueBody", "HomePrologueSpeaker" };
                var rects = names.Select(n => (RectTransform)root.Find(n)).ToArray();
                var positions = rects.Select(r => r.anchoredPosition).ToArray();
                var sizes = rects.Select(r => r.sizeDelta).ToArray();
                var scales = rects.Select(r => r.localScale).ToArray();
                int childCount = root.childCount;
                var body = root.Find("HomePrologueBody").GetComponent<Text>();
                string previous = null;
                for (int page = 0; page < 6; page++)
                {
                    invoke("ShowHomeProloguePage", new object[] { page });
                    Assert.That(body.text, Is.Not.EqualTo(previous));
                    previous = body.text;
                    Assert.That(body.resizeTextForBestFit, Is.False);
                    Assert.That(body.fontSize, Is.EqualTo(36));
                    Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height),
                        "Every narration page must fit without shrinking or truncating.");
                    Assert.That(root.Find("HomeProloguePrompt").GetComponent<Text>().text,
                        Is.EqualTo(page == 5 ? "タップして冒険を始める" : "タップして次へ"));
                    foreach (float age in new[] { 0f, 1f, 8f })
                    {
                        type.GetField("homeProloguePageStartedAt", Private).SetValue(controller, Time.unscaledTime - age);
                        invoke("AnimateHomePrologue", Array.Empty<object>());
                        for (int i = 0; i < rects.Length; i++)
                        {
                            Assert.That(rects[i].anchoredPosition, Is.EqualTo(positions[i]), names[i] + " position");
                            Assert.That(rects[i].sizeDelta, Is.EqualTo(sizes[i]), names[i] + " size");
                            Assert.That(rects[i].localScale, Is.EqualTo(scales[i]), names[i] + " scale");
                        }
                        Assert.That(root.childCount, Is.EqualTo(childCount), "Reuse the original images and frame");
                        var guide = root.Find("HomePrologueGuide").GetComponent<Image>();
                        Assert.That(guide.gameObject.activeSelf, Is.False, "Narration must not appear to be Luse speaking.");
                        Assert.That(guide.color.a, Is.EqualTo(1f));
                        Assert.That(body.color.a, Is.EqualTo(1f));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
