using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class HomePrivacyPolicyTests
    {
        private const string ExpectedUrl = "https://nasus-gaming-support.sasanisikigorira.chatgpt.site/#privacy";
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject owner, canvasObject;
        private Component home;
        private RectTransform canvas, root, panel;
        private FieldInfo appleFlag, openOverride;
        private object previousAppleFlag, previousOpenOverride;
        private string openedUrl;
        private int openCount;

        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private object Call(string name, params object[] args) => home.GetType()
            .GetMethod(name, HiddenInstance).Invoke(home, args);

        [SetUp]
        public void SetUp()
        {
            appleFlag = Runtime("Save.OnlinePlayerData").GetField("EditorAppleAccountOverride", HiddenStatic);
            openOverride = Runtime("Home.HomeSceneController").GetField("EditorPrivacyPolicyOpenOverride", HiddenStatic);
            previousAppleFlag = appleFlag.GetValue(null);
            previousOpenOverride = openOverride.GetValue(null);
            openedUrl = null;
            openCount = 0;
            openOverride.SetValue(null, (Action<string>)(url => { openedUrl = url; ++openCount; }));
            owner = new GameObject("InactivePolicySettingsController");
            owner.SetActive(false);
            home = owner.AddComponent(Runtime("Home.HomeSceneController"));
            canvasObject = new GameObject("PolicySettingsCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas = (RectTransform)canvasObject.transform;
        }

        [TearDown]
        public void TearDown()
        {
            appleFlag.SetValue(null, previousAppleFlag);
            openOverride.SetValue(null, previousOpenOverride);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }

        private void Build(bool appleEnabled, float width = 1080f, float height = 2341f)
        {
            appleFlag.SetValue(null, appleEnabled);
            canvas.sizeDelta = new Vector2(width, height);
            Call("BuildAudioSettingsPanel", canvas);
            root = (RectTransform)canvas.Find("AudioSettingsPanelRoot");
            root.gameObject.SetActive(true);
            panel = (RectTransform)root.Find("AudioSettingsPanel");
            Canvas.ForceUpdateCanvases();
        }

        [TestCase(false, false, false)] [TestCase(false, false, true)]
        [TestCase(false, true, false)] [TestCase(false, true, true)]
        [TestCase(true, false, false)] [TestCase(true, false, true)]
        [TestCase(true, true, false)] [TestCase(true, true, true)]
        public void PolicyActionIsAlwaysAvailableIndependentlyOfAppleAndAdvertisingOptions(
            bool appleEnabled, bool adsEnabled, bool privacyOptionsRequired)
        {
            Build(appleEnabled);
            Call("SetAudioSettingsPrivacyOptionsVisibility", adsEnabled, privacyOptionsRequired);
            var policy = panel.Find("PrivacyPolicyButton").GetComponent<Button>();
            Assert.That(policy.gameObject.activeInHierarchy, Is.True);
            Assert.That(policy.IsInteractable(), Is.True);
            Assert.That(policy.targetGraphic.raycastTarget, Is.True);
            Assert.That(policy.GetComponentInChildren<Text>().text, Is.EqualTo("プライバシーポリシー"));
            Assert.That(panel.Find("AudioSettingsPrivacyButton").gameObject.activeSelf,
                Is.EqualTo(adsEnabled && privacyOptionsRequired));
            Assert.That(panel.Find("AppleAccountButton") != null, Is.EqualTo(appleEnabled));

            policy.onClick.Invoke();
            Assert.That(openCount, Is.EqualTo(1));
            Assert.That(openedUrl, Is.EqualTo(ExpectedUrl));
            var url = new Uri(openedUrl);
            Assert.That(url.Scheme, Is.EqualTo("https"));
            Assert.That(url.Query, Is.Empty);
            Assert.That(url.UserInfo, Is.Empty);
            Assert.That(url.Fragment, Is.EqualTo("#privacy"));
            Assert.That(root.gameObject.activeSelf, Is.True, "Opening the policy must not switch accounts or dismiss settings.");

            panel.Find("AudioSettingsCloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.False);
        }

        [TestCase(760f, 1440f, false)] [TestCase(760f, 1440f, true)]
        [TestCase(1080f, 2341f, false)] [TestCase(1080f, 2341f, true)]
        public void PolicyAndOptionalActionsFitNarrowAndTallSettingsWithoutOverlap(float width, float height, bool appleEnabled)
        {
            Build(appleEnabled, width, height);
            Call("SetAudioSettingsPrivacyOptionsVisibility", true, true);
            Rect available = Rect.MinMaxRect(-width / 2f, -height / 2f + 220f, width / 2f, height / 2f - 100f);
            Call("ApplyAudioSettingsAvailableRect", available);
            Canvas.ForceUpdateCanvases();
            AssertInside(available, Bounds(canvas, panel));
            var controls = panel.GetComponentsInChildren<Selectable>(true);
            foreach (var control in controls)
            {
                var rect = (RectTransform)control.transform;
                AssertInside(panel.rect, Bounds(panel, rect));
            }
            for (int i = 0; i < controls.Length; ++i)
                for (int j = i + 1; j < controls.Length; ++j)
                    Assert.That(Bounds(panel, (RectTransform)controls[i].transform)
                        .Overlaps(Bounds(panel, (RectTransform)controls[j].transform)), Is.False,
                        controls[i].name + " / " + controls[j].name);
            var policyText = panel.Find("PrivacyPolicyButton").GetComponentInChildren<Text>();
            Assert.That(policyText.preferredHeight, Is.LessThanOrEqualTo(policyText.rectTransform.rect.height + 1f));
            AssertInside(panel.rect, Bounds(panel, policyText.rectTransform));
            Assert.That(panel.Find("StoryArchiveButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("DevelopmentGameSpeedButton").gameObject.activeInHierarchy, Is.True);
        }

        private static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertInside(Rect outer, Rect inner)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .1f));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .1f));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .1f));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .1f));
        }
    }
}
