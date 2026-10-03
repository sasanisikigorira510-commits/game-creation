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
    public sealed class StorySpeakerPortraitTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void EveryCatalogLineShowsOnlyTheCurrentSpeakersActualPortrait()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                foreach (object story in Catalog())
                {
                    object[] lines = ((IEnumerable)Property(story, "Lines")).Cast<object>().ToArray();
                    for (int index = 0; index < lines.Length; index++)
                    {
                        reader.ShowLine(story, index);
                        string name = (string)Property(lines[index], "Speaker");
                        Assert.That(reader.Find("StoryDialoguePanel/StoryDialogueSpeaker").GetComponent<Text>().text,
                            Is.EqualTo(name == "地の文" ? string.Empty : name));
                        AssertPortrait(reader, name, $"{Property(story, "EventId")}, line {index + 1}");
                    }
                }
                Assert.That(Field(reader.Controller, "profile"), Is.Null,
                    "A read-only reader must render without attaching or changing a player save.");
                Assert.That(Field(reader.Controller, "readOnly"), Is.True);
            }
        }

        [Test]
        public void ProtagonistAliasesResolveToTheSamePortrait()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                reader.ShowSpeaker("契約師");
                AssertPortrait(reader, "契約師", "The story's contractor name");
                Sprite contractor = reader.Portrait.sprite;
                reader.ShowSpeaker("主人公");
                AssertPortrait(reader, "主人公", "The protagonist alias");
                Assert.That(reader.Portrait.sprite, Is.SameAs(contractor));
            }
        }

        [Test]
        public void NarrationRecordsAndUnknownVoicesClearThePreviousPortrait()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                foreach (string name in new[] { "地の文", "セオの記録", "炉の奥の声", "黒い契約炉", "未知の話者", "", null })
                {
                    reader.ShowSpeaker("イオナ");
                    reader.SettlePortrait();
                    Assert.That(reader.Portrait.gameObject.activeSelf, Is.True);
                    reader.ShowSpeaker(name);
                    Invoke(reader.Controller, "UpdateSpeakerPortrait");
                    AssertPortrait(reader, name, $"After switching from Iona to '{name}'");
                    Assert.That(Field(reader.Controller, "portraitSpeaker"), Is.Null,
                        "A hidden portrait must not leave a speaker cache that suppresses its next fade.");
                    reader.ShowSpeaker("イオナ");
                    Assert.That(reader.Group.alpha, Is.EqualTo(0f), "A returning character starts a fresh reveal.");
                    reader.SettlePortrait();
                    Assert.That(reader.Group.alpha, Is.EqualTo(1f));
                }
            }
        }

        [Test]
        public void ConsecutiveLinesFromTheSameSpeakerDoNotRestartTheFade()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                object intro = Catalog().Single(s => (string)Property(s, "EventId") == "story_first_summon_iona_before");
                reader.ShowLine(intro, 1);
                reader.SettlePortrait();
                float started = (float)Field(reader.Controller, "portraitStartedAt");
                Sprite portrait = reader.Portrait.sprite;
                reader.ShowLine(intro, 3);
                Assert.That(reader.Portrait.sprite, Is.SameAs(portrait));
                Assert.That(reader.Group.alpha, Is.EqualTo(1f), "The same speaker should not blink between lines.");
                Assert.That(Field(reader.Controller, "portraitStartedAt"), Is.EqualTo(started));
                Assert.That(reader.Body.text, Is.Empty, "The new line still starts its text reveal normally.");
            }
        }

        [Test]
        public void RapidSpeakerChangesCannotLeaveAnOldPortraitOrBlockDialogueInput()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                foreach (string name in new[] { "イオナ", "ルシェ", "契約師", "イオナ", "地の文", "契約師", "ルシェ" })
                {
                    reader.ShowSpeaker(name);
                    AssertPortrait(reader, name, "Rapid transition");
                    if (reader.Portrait.gameObject.activeSelf)
                        Assert.That(reader.Group.alpha, Is.EqualTo(0f));
                }
                reader.SettlePortrait();
                AssertPortrait(reader, "ルシェ", "Final settled portrait");
                Assert.That(reader.Group.alpha, Is.EqualTo(1f));
                Assert.That(reader.Body.raycastTarget, Is.True);
                Assert.That(reader.Body.GetComponent<Button>().interactable, Is.True,
                    "The dialogue text remains a usable reveal/advance target.");
            }
        }

        [TestCase(1179, 2556, 177, 102)]
        [TestCase(750, 1334, 40, 0)]
        [TestCase(640, 1136, 40, 0)]
        [TestCase(768, 1024, 24, 20)]
        public void PortraitsClearTheTitleAndDialogueOnPortraitScreens(int width, int height, int top, int bottom)
        {
            using (var reader = new ReaderLayout(width, height, top, bottom))
            {
                object intro = Catalog().Single(s => (string)Property(s, "EventId") == "story_first_summon_iona_before");
                foreach (int index in new[] { 0, 1, 2 })
                {
                    reader.ShowLine(intro, index);
                    reader.Body.text = (string)Property(((Array)Property(intro, "Lines")).GetValue(index), "Text");
                    ((Text)Field(reader.Controller, "nextLabel")).text = "次へ  ›";
                    reader.SettlePortrait();
                    Canvas.ForceUpdateCanvases();
                    Rect portrait = BoundsIn(reader.Root, reader.Portrait.rectTransform);
                    Rect safe = BoundsIn(reader.Root, reader.SafeArea);
                    Rect title = BoundsIn(reader.Root, reader.Find("StoryTitle"));
                    Rect subtitle = BoundsIn(reader.Root, reader.Find("StorySubtitle"));
                    Rect speaker = BoundsIn(reader.Root, reader.Find("StoryDialoguePanel/StoryDialogueSpeaker"));
                    Rect body = BoundsIn(reader.Root, reader.Body.rectTransform);
                    Rect panel = BoundsIn(reader.Root, reader.Find("StoryDialoguePanel"));
                    Assert.That(portrait.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                    Assert.That(portrait.xMax, Is.LessThanOrEqualTo(safe.xMax));
                    Assert.That(portrait.yMax, Is.LessThan(title.yMin), "Keep the character's head clear of the story title.");
                    Assert.That(title.yMax, Is.LessThanOrEqualTo(subtitle.yMin + .01f));
                    Assert.That(subtitle.yMax, Is.LessThanOrEqualTo(safe.yMax));
                    Assert.That(portrait.yMin, Is.GreaterThan(speaker.yMax), "The complete illustration clears the speaker name.");
                    Assert.That(portrait.yMin, Is.GreaterThan(body.yMax), "The illustration never covers dialogue glyphs.");
                    Assert.That(panel.yMax - portrait.yMin, Is.LessThan(safe.height * .04f),
                        "Only the illustration's bottom edge may tuck behind the decorative panel border.");
                    Assert.That(reader.Portrait.transform.GetSiblingIndex(),
                        Is.LessThan(reader.Find("StoryDialoguePanel").GetSiblingIndex()));
                    Assert.That(reader.Portrait.preserveAspect, Is.True, "Do not stretch or crop the artwork to the screen shape.");
                    Assert.That(reader.Body.fontSize, Is.EqualTo(39));
                    Assert.That(reader.Body.resizeTextForBestFit, Is.False);
                    Assert.That(reader.Body.preferredHeight, Is.LessThanOrEqualTo(reader.Body.rectTransform.rect.height + 1f));
                    if (width == 1179 || width == 640)
                        CaptureIfRequested(reader, width, height, $"{reader.Portrait.sprite.name}-{width}x{height}.png");
                }
            }
        }

        private static void AssertPortrait(ReaderLayout reader, string name, string label)
        {
            string expectedPath = name == "イオナ" ? "UI/StoryPortraits/IonaDialogueImage2" :
                name == "魔王ガルザ" ? "UI/StoryPortraits/Garza/garza_idle_0" :
                name == "ルシェ" ? "UI/StoryPortraits/LucheDialogueImage2" :
                name == "契約師" || name == "主人公" ? "UI/StoryPortraits/ContractorDialogueImage2" : null;
            if (expectedPath == null)
            {
                Assert.That(reader.Portrait.gameObject.activeSelf, Is.False, label);
                Assert.That(reader.Portrait.sprite, Is.Null, label + ": no previous face may remain.");
            }
            else
            {
                Sprite expected = Resources.Load<Sprite>(expectedPath);
                Assert.That(expected, Is.Not.Null, label + ": the generated portrait must be imported as a real sprite.");
                Assert.That(reader.Portrait.gameObject.activeSelf, Is.True, label);
                Assert.That(reader.Portrait.sprite, Is.SameAs(expected), label);
            }
            Assert.That(reader.Portrait.raycastTarget, Is.False, label);
            Assert.That(reader.Group.blocksRaycasts, Is.False, label);
            Assert.That(reader.Group.interactable, Is.False, label);
        }

        [Test]
        public void GarzaActionsAppearOnlyOnTheirNarrationAndClearOnTheNextLine()
        {
            using (var reader = new ReaderLayout(1179, 2556, 177, 102))
            {
                object finale = Catalog().Single(s => (string)Property(s, "EventId") == "story_first_arc_complete");
                foreach (int index in new[] {0,8,13,16})
                {
                    reader.ShowLine(finale,index);
                    var action=reader.Find("StoryGarzaAction").GetComponent<Image>();
                    Assert.That(action.gameObject.activeSelf,Is.True);
                    Assert.That(action.sprite,Is.Not.Null);
                    Assert.That(action.raycastTarget,Is.False);
                    Assert.That(reader.Portrait.gameObject.activeSelf,Is.False);
                    reader.Body.text=(string)Property(((Array)Property(finale,"Lines")).GetValue(index),"Text");
                    CaptureIfRequested(reader,1179,2556,$"garza-action-{index+1}.png");
                    reader.ShowLine(finale,index+1);
                    Assert.That(action.gameObject.activeSelf,Is.False,"Narration art must not remain over the next speaker.");
                }
                reader.ShowLine(finale,2); reader.SettlePortrait();
                AssertPortrait(reader,"魔王ガルザ","Finale portrait");
                reader.Body.text=(string)Property(((Array)Property(finale,"Lines")).GetValue(2),"Text");
                CaptureIfRequested(reader,1179,2556,"garza-dialogue.png");
            }
        }

        private sealed class ReaderLayout : IDisposable
        {
            private readonly FieldInfo current;
            private readonly object previousCurrent;
            public Component Controller { get; }
            public RectTransform Root { get; }
            public RectTransform SafeArea { get; }
            public Text Body { get; }
            public Image Portrait { get; }
            public CanvasGroup Group { get; }

            public ReaderLayout(int width, int height, int top, int bottom)
            {
                Type type = RuntimeType("WitchTower.UI.StoryDialogueController");
                current = type.GetField("current", PrivateStatic);
                previousCurrent = current.GetValue(null);
                Controller = (Component)type.GetMethod("Create", PrivateStatic).Invoke(null, null);
                ((Behaviour)Controller).enabled = false;
                SetField(Controller, "readOnly", true);
                SetField(Controller, "story", Catalog().First());
                Invoke(Controller, "BuildReader");
                Root = (RectTransform)Controller.transform;
                var scaler = Root.GetComponent<CanvasScaler>();
                float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / scaler.referenceResolution.x, 2f),
                    Mathf.Log(height / scaler.referenceResolution.y, 2f), scaler.matchWidthOrHeight));
                scaler.enabled = false;
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1f;
                Root.position = Vector3.zero;
                Root.localScale = Vector3.one;
                Root.sizeDelta = new Vector2(width / scale, height / scale);
                SafeArea = (RectTransform)Field(Controller, "safeRoot");
                SafeArea.anchorMin = new Vector2(0, bottom / (float)height);
                SafeArea.anchorMax = new Vector2(1, 1f - top / (float)height);
                SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
                Body = (Text)Field(Controller, "body");
                Portrait = (Image)Field(Controller, "speakerPortrait");
                Group = (CanvasGroup)Field(Controller, "portraitGroup");
                Canvas.ForceUpdateCanvases();
            }

            public void ShowLine(object story, int index)
            {
                SetField(Controller, "story", story);
                SetField(Controller, "lineIndex", index);
                Invoke(Controller, "DisplayLine");
                Find("StoryTitle").GetComponent<Text>().text = (string)Property(story, "Title");
                Find("StorySubtitle").GetComponent<Text>().text = (string)Property(story, "Subtitle");
            }

            public void ShowSpeaker(string name)
            {
                Type lineType = RuntimeType("WitchTower.Data.StoryDialogueLine");
                Array lines = Array.CreateInstance(lineType, 1);
                lines.SetValue(Activator.CreateInstance(lineType, name, "確認用の会話です。"), 0);
                object story = Activator.CreateInstance(RuntimeType("WitchTower.Data.StoryDialogueDefinition"),
                    "portrait_preview", "立ち絵の確認", "開発用プレビュー", "", 0, lines);
                ShowLine(story, 0);
            }

            public void SettlePortrait()
            {
                SetField(Controller, "portraitStartedAt", Time.unscaledTime - 1f);
                Invoke(Controller, "UpdateSpeakerPortrait");
            }

            public RectTransform Find(string path) => (RectTransform)SafeArea.Find(path);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Controller.gameObject);
                current.SetValue(null, previousCurrent);
            }
        }

        private static void CaptureIfRequested(ReaderLayout reader, int width, int height, string filename)
        {
            string directory = Environment.GetEnvironmentVariable("WITCHTOWER_STORY_PORTRAIT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", PrivateStatic)
                .Invoke(null, new object[] { reader.Root, width, height, Path.Combine(directory, filename) });
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
        private static object Invoke(object value, string name) => value.GetType().GetMethod(name, PrivateInstance).Invoke(value, null);
    }
}
