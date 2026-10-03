using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class HomeSettingsReadabilityTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        [TestCase(1920f)] [TestCase(2341f)]
        public void BlueStoneAmountMatchesGoldYOnCreationAndReuse(float height)
        {
            var owner = new GameObject("InactiveResourceAlignmentController"); owner.SetActive(false);
            var canvasObject = new GameObject("ResourceAlignmentCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1080f, height);
            try
            {
                var home = owner.AddComponent(T("Home.HomeSceneController"));
                Call(home, "EnsureHomeStoneBalanceBar", canvasObject.transform);
                var bar = canvasObject.transform.Find("HomeStoneBalanceBar");
                var gold = bar.Find("GoldCounter/GoldAmount").GetComponent<Text>();
                var blue = bar.Find("FreeStoneCounter/FreeStoneAmount").GetComponent<Text>();
                var paid = bar.Find("PaidStoneCounter/PaidStoneAmount").GetComponent<Text>();
                gold.text = "2,330";
                blue.text = "2,400";
                paid.text = "500,000";
                Canvas.ForceUpdateCanvases();
                Text[] amounts = { gold, blue, paid };
                float[] anchoredX = amounts.Select(t => t.rectTransform.anchoredPosition.x).ToArray();
                float[] worldX = amounts.Select(t => t.rectTransform.position.x).ToArray();
                Vector2[] sizes = amounts.Select(t => t.rectTransform.sizeDelta).ToArray();
                Font[] fonts = amounts.Select(t => t.font).ToArray();
                int[] fontSizes = amounts.Select(t => t.fontSize).ToArray();
                FontStyle[] fontStyles = amounts.Select(t => t.fontStyle).ToArray();
                string capture = Environment.GetEnvironmentVariable("HOME_PAID_AMOUNT_CAPTURE_DIR");
                Action<string> captureHud = phase =>
                {
                    if (string.IsNullOrEmpty(capture)) return;
                    Directory.CreateDirectory(capture);
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { (RectTransform)canvasObject.transform, 1080, (int)height,
                            Path.Combine(capture, "paid-amount-" + phase + "-" + height + ".png") });
                };
                Action assertAlignedAndUnchanged = () =>
                {
                    Assert.That(gold.rectTransform.anchoredPosition.y, Is.EqualTo(-6f));
                    for (int i = 0; i < amounts.Length; i++)
                    {
                        Text amount = amounts[i];
                        Assert.That(amount.rectTransform.position.y,
                            Is.EqualTo(gold.rectTransform.position.y).Within(.01f), amount.name);
                        Assert.That(amount.rectTransform.anchoredPosition.x, Is.EqualTo(anchoredX[i]), amount.name);
                        Assert.That(amount.rectTransform.position.x, Is.EqualTo(worldX[i]).Within(.01f), amount.name);
                        Assert.That(amount.rectTransform.sizeDelta, Is.EqualTo(sizes[i]), amount.name);
                        Assert.That(amount.font, Is.SameAs(fonts[i]), amount.name);
                        Assert.That(amount.fontSize, Is.EqualTo(fontSizes[i]), amount.name);
                        Assert.That(amount.fontStyle, Is.EqualTo(fontStyles[i]), amount.name);
                        Assert.That(amount.alignment, Is.EqualTo(gold.alignment), amount.name);
                        Assert.That(amount.resizeTextForBestFit, Is.EqualTo(gold.resizeTextForBestFit), amount.name);
                        Assert.That(amount.resizeTextMinSize, Is.EqualTo(gold.resizeTextMinSize), amount.name);
                        Assert.That(amount.resizeTextMaxSize, Is.EqualTo(gold.resizeTextMaxSize), amount.name);
                    }
                    Assert.That(gold.text, Is.EqualTo("2,330"));
                    Assert.That(blue.text, Is.EqualTo("2,400"));
                    Assert.That(paid.text, Is.EqualTo("500,000"));
                };
                assertAlignedAndUnchanged();
                captureHud("created");
                // Reused counters can have different parent offsets. Align both
                // stone amounts to the gold world-space Y, not just its local Y.
                ((RectTransform)gold.transform.parent).anchoredPosition += Vector2.up * 5f;
                ((RectTransform)blue.transform.parent).anchoredPosition += Vector2.up * 8f;
                ((RectTransform)paid.transform.parent).anchoredPosition -= Vector2.up * 11f;
                gold.rectTransform.anchoredPosition += Vector2.up * 6f;
                blue.rectTransform.anchoredPosition += Vector2.up * 12f;
                paid.rectTransform.anchoredPosition -= Vector2.up * 15f;
                Call(home, "EnsureHomeStoneBalanceBar", canvasObject.transform);
                Canvas.ForceUpdateCanvases();
                Assert.That(canvasObject.transform.Find("HomeStoneBalanceBar"), Is.SameAs(bar));
                assertAlignedAndUnchanged();
                captureHud("reused");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase(1920f)] [TestCase(2341f)]
        public void HomeHudContainerIsTransparentOnCreationAndReuse(float height)
        {
            var owner = new GameObject("InactiveHudController"); owner.SetActive(false);
            var canvasObject = new GameObject("TransparentHudCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = (RectTransform)canvasObject.transform;
            canvas.sizeDelta = new Vector2(1080f, height);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            try
            {
                var home = owner.AddComponent(T("Home.HomeSceneController"));
                var village = Resources.Load<Sprite>("UI/HomeMenu/HomeVillageSquareImage2");
                Assert.That(village, Is.Not.Null);
                T("Home.HomeSceneController").GetMethod("CreatePortraitCoverBackground", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "HudTestVillage", canvas, village });
                Call(home, "EnsureHomeStoneBalanceBar", canvas);
                var bar = canvas.Find("HomeStoneBalanceBar");
                var background = bar.GetComponent<Image>();
                Assert.That(background.color.a, Is.Zero);
                Assert.That(background.raycastTarget, Is.False);
                // A serialized/previously built HUD must lose its old tint as well.
                background.color = new Color(.012f, .012f, .022f, .1f);
                background.raycastTarget = true;
                Call(home, "EnsureHomeStoneBalanceBar", canvas);
                Assert.That(canvas.Find("HomeStoneBalanceBar"), Is.SameAs(bar));
                Assert.That(background.color.a, Is.Zero);
                Assert.That(background.raycastTarget, Is.False);
                foreach (string name in new[] { "PlayerBadge", "GoldCounter", "FreeStoneCounter", "PaidStoneCounter", "AudioSettingsButton" })
                {
                    var image = bar.Find(name).GetComponent<Image>();
                    Assert.That(image.sprite, Is.Not.Null, name);
                    Assert.That(image.color.a, Is.EqualTo(1f), name);
                }
                var gear = bar.Find("AudioSettingsButton").GetComponent<Button>();
                Assert.That(gear.IsInteractable(), Is.True);
                Assert.That(gear.targetGraphic.raycastTarget, Is.True);
                foreach (var amount in new[] { "GoldCounter/GoldAmount", "FreeStoneCounter/FreeStoneAmount", "PaidStoneCounter/PaidStoneAmount" })
                    Assert.That(bar.Find(amount).GetComponent<Text>().color.a, Is.EqualTo(1f), amount);
                string capture = Environment.GetEnvironmentVariable("HOME_HUD_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(capture))
                {
                    Directory.CreateDirectory(capture);
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { canvas, 1080, (int)height, Path.Combine(capture, "transparent-hud-" + height + ".png") });
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [TestCase("quest")] [TestCase("T00")] [TestCase("T02")]
        public void SettingsSuspendGuidanceAcrossRefreshesAndLeaveAccountAndCloseTouchable(string lesson)
        {
            var flag = T("Save.OnlinePlayerData").GetField("EditorAppleAccountOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object previousFlag = flag.GetValue(null);
            var owner = new GameObject("InactiveSettingsController"); owner.SetActive(false);
            var canvasObject = new GameObject("SettingsModalTest", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1080f, 2341f);
            var cameraObject = new GameObject("SettingsTouchCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var renderTarget = new RenderTexture(1080, 2341, 24);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2341f / 2f;
            camera.targetTexture = renderTarget;
            canvasObject.GetComponent<Canvas>().worldCamera = camera;
            var events = new GameObject("SettingsTestEvents", typeof(EventSystem));
            try
            {
                flag.SetValue(null, true);
                var home = owner.AddComponent(T("Home.HomeSceneController"));
                var menu = new GameObject("UnifiedHomeMenu", typeof(RectTransform));
                menu.transform.SetParent(canvasObject.transform, false);
                var menuRect = (RectTransform)menu.transform;
                menuRect.anchorMin = Vector2.zero; menuRect.anchorMax = Vector2.one;
                menuRect.offsetMin = menuRect.offsetMax = Vector2.zero;
                Action<string, object> field = (name, value) => home.GetType()
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(home, value);
                field("unifiedMenuRoot", menu);
                var guide = new GameObject("TestGuide", typeof(RectTransform), typeof(Image), typeof(Button));
                guide.transform.SetParent(menu.transform, false);
                field("homeGuideButton", guide.GetComponent<Button>());
                var overlay = new GameObject("TestGuideHitSurface", typeof(RectTransform), typeof(Image), typeof(Button));
                overlay.transform.SetParent(menu.transform, false);
                field("homeGuideAdvanceOverlayButton", overlay.GetComponent<Button>());
                var focus = new GameObject("TestFocus", typeof(RectTransform)); focus.transform.SetParent(menu.transform, false);
                field("homeTutorialFocusRoot", focus);
                var pulse = new GameObject("TestPulse", typeof(RectTransform)); pulse.transform.SetParent(menu.transform, false);
                field("homeFirstSummonPulseRoot", pulse);
                object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                save.GetType().GetField("HasCompletedTutorial").SetValue(save, lesson == "quest");
                save.GetType().GetField("TutorialStepId").SetValue(save, lesson == "quest" ? "Complete" : lesson);
                object profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
                T("Home.DailyRewardService").GetMethod("RecordBattleWin").Invoke(null, new[] { profile, (object)DateTime.Now });
                // Normalize legacy completed-tutorial hints before taking the
                // baseline, just as the home guide does before opening settings.
                T("Data.StoryTutorialService").GetMethod("GetNextEvent").Invoke(null, new[] { profile, "HomeScene" });
                string before = JsonUtility.ToJson(profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 }));

                home.GetType().GetMethod("OpenAudioSettingsPanel").Invoke(home, null);
                var root = menu.transform.Find("AudioSettingsPanelRoot");
                var panel = root.Find("AudioSettingsPanel");
                for (int frame = 0; frame < 5; frame++)
                {
                    Call(home, "ApplyHomeGuideDisplay", profile);
                    Call(home, "ApplyHomeTutorialFocus", profile);
                    Call(home, "RefreshFirstSummonTutorialPulse", profile, menu.transform);
                    Assert.That(guide.activeSelf || overlay.activeSelf || focus.activeSelf || pulse.activeSelf, Is.False);
                }
                Canvas.ForceUpdateCanvases();
                Call(home, "ApplyAudioSettingsAvailableRect", Rect.MinMaxRect(-540f, -950f, 540f, 1070f));
                Canvas.ForceUpdateCanvases();
                camera.Render();
                foreach (string name in new[] { "AppleAccountButton", "PrivacyPolicyButton", "AudioSettingsCloseButton" })
                {
                    var button = panel.Find(name).GetComponent<Button>();
                    Assert.That(button.IsInteractable(), Is.True);
                    var rect = (RectTransform)button.transform;
                    var pointer = new PointerEventData(events.GetComponent<EventSystem>())
                    { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
                    var hits = new List<RaycastResult>();
                    // EditMode does not register BaseRaycaster with the EventSystem;
                    // exercise the canvas raycaster directly after rendering it.
                    canvasObject.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), name);
                }
                // Only close the UI; never invoke the real online account action in a test.
                panel.Find("AudioSettingsCloseButton").GetComponent<Button>().onClick.Invoke();
                Call(home, "ApplyHomeGuideDisplay", profile);
                Assert.That(guide.activeSelf, Is.True, "Closing settings must resume the outstanding guide.");
                Assert.That(JsonUtility.ToJson(profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 })), Is.EqualTo(before));
            }
            finally
            {
                flag.SetValue(null, previousFlag);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(events);
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(renderTarget);
            }
        }

        [TestCase(2341f, true)] [TestCase(1920f, true)] [TestCase(1440f, true)]
        [TestCase(2341f, false)] [TestCase(1440f, false)]
        public void LargerSettingsFitAvailableScreenAndKeepControlsSeparate(float height, bool appleEnabled)
        {
            var flag = T("Save.OnlinePlayerData").GetField("EditorAppleAccountOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object previousFlag = flag.GetValue(null);
            var owner = new GameObject("InactiveSettingsController"); owner.SetActive(false);
            var canvasObject = new GameObject("SettingsTestCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = (RectTransform)canvasObject.transform;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas.sizeDelta = new Vector2(1080f, height);
            try
            {
                flag.SetValue(null, appleEnabled);
                var home = owner.AddComponent(T("Home.HomeSceneController"));
                Call(home, "BuildAudioSettingsPanel", canvas);
                var root = (RectTransform)canvas.Find("AudioSettingsPanelRoot"); root.gameObject.SetActive(true);
                var panel = (RectTransform)root.Find("AudioSettingsPanel");
                // Reserve 220 canvas units for the bottom banner and 100 for the top safe area.
                Rect available = Rect.MinMaxRect(-540f, -height / 2f + 220f, 540f, height / 2f - 100f);
                Call(home, "ApplyAudioSettingsAvailableRect", available);
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.rect.width, Is.EqualTo(960f));
                Assert.That(panel.rect.height, Is.GreaterThanOrEqualTo(1220f));
                AssertInside(available, Bounds(canvas, panel));
                var controls = panel.GetComponentsInChildren<Selectable>(true);
                foreach (var control in controls)
                {
                    var rect = (RectTransform)control.transform;
                    Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(96f), control.name);
                    AssertInside(panel.rect, Bounds(panel, rect));
                }
                for (int i = 0; i < controls.Length; i++)
                    for (int j = i + 1; j < controls.Length; j++)
                        Assert.That(Bounds(panel, (RectTransform)controls[i].transform)
                            .Overlaps(Bounds(panel, (RectTransform)controls[j].transform)), Is.False,
                            controls[i].name + " / " + controls[j].name);
                foreach (Text text in panel.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(36), text.name);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name);
                    AssertInside(panel.rect, Bounds(panel, text.rectTransform));
                }
                var sliders = panel.GetComponentsInChildren<Slider>();
                Assert.That(sliders.Length, Is.EqualTo(2));
                foreach (var slider in sliders)
                {
                    slider.SetValueWithoutNotify(0f); Assert.That(slider.value, Is.Zero);
                    slider.SetValueWithoutNotify(1f); Assert.That(slider.value, Is.EqualTo(1f));
                    Assert.That(slider.handleRect.rect.height, Is.EqualTo(108f));
                }
                Assert.That(panel.Find("AppleAccountButton") != null, Is.EqualTo(appleEnabled));
                string capture = Environment.GetEnvironmentVariable("SETTINGS_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(capture) && height == 2341f && appleEnabled)
                {
                    sliders.Single(s => s.name == "AudioSettingsBgmSlider").SetValueWithoutNotify(.58f);
                    sliders.Single(s => s.name == "AudioSettingsSeSlider").SetValueWithoutNotify(.76f);
                    // Include the optional privacy action in the visual audit.
                    panel.Find("AudioSettingsPrivacyButton").gameObject.SetActive(true);
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { canvas, 1080, (int)height, Path.Combine(capture, "settings-large.png") });
                }
                panel.Find("AudioSettingsCloseButton").GetComponent<Button>().onClick.Invoke();
                Assert.That(root.gameObject.activeSelf, Is.False);
            }
            finally
            {
                flag.SetValue(null, previousFlag);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
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
