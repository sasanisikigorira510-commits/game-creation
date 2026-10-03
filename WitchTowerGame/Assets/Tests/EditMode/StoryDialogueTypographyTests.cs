using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class StoryDialogueTypographyTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [TestCase(1179, 2556, 177, 102)]
        [TestCase(750, 1334, 40, 0)]
        [TestCase(640, 1136, 40, 0)]
        [TestCase(768, 1024, 24, 20)]
        public void EveryStoryLineFitsAtTheSameReadableSize(int width, int height, int top, int bottom)
        {
            using (var reader = new ReaderLayout(width, height, top, bottom))
            {
                float tallest = 0;
                string tallestLine = string.Empty;
                foreach (object story in Catalog())
                {
                    object[] lines = ((IEnumerable)Property(story, "Lines")).Cast<object>().ToArray();
                    for (int index = 0; index < lines.Length; index++)
                    {
                        reader.ShowLine(story, index);
                        string text = (string)Property(lines[index], "Text");
                        reader.Body.text = text;
                        Canvas.ForceUpdateCanvases();
                        string label = $"{Property(story, "EventId")}, line {index + 1}, {width}x{height}";
                        Assert.That(reader.Body.resizeTextForBestFit, Is.False, label);
                        Assert.That(reader.Body.fontSize, Is.EqualTo(39), label);
                        Assert.That(reader.Body.preferredHeight, Is.LessThanOrEqualTo(reader.Body.rectTransform.rect.height + 1f),
                            $"{label}: the complete dialogue must fit without shrinking or dropping its final lines.");

                        var settings = reader.Body.GetGenerationSettings(reader.Body.rectTransform.rect.size);
                        var visible = new TextGenerator();
                        var unbounded = new TextGenerator();
                        try
                        {
                            visible.Populate(text, settings);
                            settings.verticalOverflow = VerticalWrapMode.Overflow;
                            unbounded.Populate(text, settings);
                            Assert.That(visible.characterCountVisible, Is.EqualTo(unbounded.characterCountVisible), label);
                            Assert.That(visible.vertexCount, Is.EqualTo(unbounded.vertexCount),
                                $"{label}: truncation must not hide any of the generated glyphs.");
                        }
                        finally
                        {
                            ((IDisposable)visible).Dispose();
                            ((IDisposable)unbounded).Dispose();
                        }

                        if (reader.Body.preferredHeight > tallest)
                        {
                            tallest = reader.Body.preferredHeight;
                            tallestLine = label;
                        }
                        Assert.That(BoundsIn(reader.Root, reader.Body.rectTransform).yMin,
                            Is.GreaterThan(BoundsIn(reader.Root, reader.Next).yMax),
                            $"{label}: the dialogue must clear the next button.");
                    }
                }
                Debug.Log($"[StoryTypography] {width}x{height}: tallest text {tallest:0.0}, " +
                    $"available {reader.Body.rectTransform.rect.height:0.0}; {tallestLine}");
            }
        }

        [Test]
        public void TypewriterKeepsTheFirstGlyphSizeFromTheFirstLetterThroughTheCompleteLine()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                object intro = Catalog().Single(s => (string)Property(s, "EventId") == "story_first_summon_iona_before");
                reader.ShowLine(intro, 1);
                string line = (string)Property(((Array)Property(intro, "Lines")).GetValue(1), "Text");
                Vector2 originalScale = reader.Body.rectTransform.localScale;
                Rect originalBounds = BoundsIn(reader.Root, reader.Body.rectTransform);
                Vector2? firstGlyphSize = null;
                for (int length = 1; length <= line.Length; length++)
                {
                    // This is the same progressively longer string sent by the
                    // reader to Unity's text renderer. Check its actual glyph
                    // geometry, not only the nominal Text.fontSize property.
                    reader.Body.text = line.Substring(0, length);
                    Canvas.ForceUpdateCanvases();
                    var generator = reader.Body.cachedTextGenerator;
                    generator.Populate(reader.Body.text,
                        reader.Body.GetGenerationSettings(reader.Body.rectTransform.rect.size));
                    Assert.That(generator.verts.Count, Is.GreaterThanOrEqualTo(4));
                    var quad = generator.verts.Take(4).Select(v => v.position).ToArray();
                    var size = new Vector2(quad.Max(v => v.x) - quad.Min(v => v.x),
                        quad.Max(v => v.y) - quad.Min(v => v.y));
                    Assert.That(size.x, Is.GreaterThan(0), "The inspected Japanese glyph must be visible.");
                    if (!firstGlyphSize.HasValue) firstGlyphSize = size;
                    Assert.That(size.x, Is.EqualTo(firstGlyphSize.Value.x).Within(.01f), $"After {length} letters.");
                    Assert.That(size.y, Is.EqualTo(firstGlyphSize.Value.y).Within(.01f), $"After {length} letters.");
                    Assert.That(reader.Body.fontSize, Is.EqualTo(39));
                    Assert.That(reader.Body.resizeTextForBestFit, Is.False);
                    Assert.That((Vector2)reader.Body.rectTransform.localScale, Is.EqualTo(originalScale));
                    Assert.That(BoundsIn(reader.Root, reader.Body.rectTransform), Is.EqualTo(originalBounds));
                    if (length == 24) CaptureIfRequested(reader, "intro-iona-partial.png");
                }
                CaptureIfRequested(reader, "intro-iona-complete.png");

                var longest = Catalog().SelectMany(story => ((IEnumerable)Property(story, "Lines")).Cast<object>()
                    .Select((text, index) => new { Story = story, Text = (string)Property(text, "Text"), Index = index }))
                    .OrderByDescending(lineInfo => lineInfo.Text.Length).First();
                reader.ShowLine(longest.Story, longest.Index);
                reader.Body.text = longest.Text;
                Canvas.ForceUpdateCanvases();
                CaptureIfRequested(reader, "longest-story-line.png");
            }
        }

        private sealed class ReaderLayout : IDisposable
        {
            private readonly Component controller;
            private readonly FieldInfo current;
            private readonly object previousCurrent;
            public RectTransform Root { get; }
            public Text Body { get; }
            public RectTransform Next { get; }

            public ReaderLayout(int width, int height, int top, int bottom)
            {
                Type type = RuntimeType("WitchTower.UI.StoryDialogueController");
                current = type.GetField("current", PrivateStatic);
                previousCurrent = current.GetValue(null);
                controller = (Component)type.GetMethod("Create", PrivateStatic).Invoke(null, null);
                ((Behaviour)controller).enabled = false;
                SetField(controller, "readOnly", true);
                SetField(controller, "story", Catalog().First());
                Call(controller, "BuildReader");
                Root = (RectTransform)controller.transform;
                var scaler = Root.GetComponent<CanvasScaler>();
                // Model the production ScaleWithScreenSize / Match .5 canvas:
                // geometric interpolation is different from width-only sizing.
                float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / scaler.referenceResolution.x, 2f),
                    Mathf.Log(height / scaler.referenceResolution.y, 2f), scaler.matchWidthOrHeight));
                scaler.enabled = false;
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1f;
                Root.position = Vector3.zero;
                Root.localScale = Vector3.one;
                Root.sizeDelta = new Vector2(width / scale, height / scale);
                var safeArea = (RectTransform)Field(controller, "safeRoot");
                safeArea.anchorMin = new Vector2(0, bottom / (float)height);
                safeArea.anchorMax = new Vector2(1, 1f - top / (float)height);
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
                Body = (Text)Field(controller, "body");
                Next = (RectTransform)Root.Find("StorySafeArea/StoryDialoguePanel/StoryDialogueNext");
                Canvas.ForceUpdateCanvases();
            }

            public void ShowLine(object story, int index)
            {
                SetField(controller, "story", story);
                SetField(controller, "lineIndex", index);
                Call(controller, "DisplayLine");
                var title = Root.Find("StorySafeArea/StoryTitle").GetComponent<Text>();
                var subtitle = Root.Find("StorySafeArea/StorySubtitle").GetComponent<Text>();
                title.text = (string)Property(story, "Title");
                subtitle.text = (string)Property(story, "Subtitle");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
                current.SetValue(null, previousCurrent);
            }
        }

        private static void CaptureIfRequested(ReaderLayout reader, string filename)
        {
            string directory = Environment.GetEnvironmentVariable("WITCHTOWER_STORY_TYPOGRAPHY_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", PrivateStatic)
                .Invoke(null, new object[] { reader.Root, 1179, 2556, Path.Combine(directory, filename) });
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        private static object[] Catalog() => ((IEnumerable)RuntimeType("WitchTower.Data.StoryDialogueCatalog")
            .GetProperty("All").GetValue(null)).Cast<object>().ToArray();
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
        private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        private static object Field(object value, string name) => value.GetType().GetField(name, PrivateInstance).GetValue(value);
        private static void SetField(object value, string name, object data) => value.GetType().GetField(name, PrivateInstance).SetValue(value, data);
        private static object Call(object value, string name) => value.GetType().GetMethod(name, PrivateInstance).Invoke(value, null);
    }
}
