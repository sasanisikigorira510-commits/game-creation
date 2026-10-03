using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class SafeAreaFirstFrameTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private SceneSetup[] originalScenes;
        private Component controller;
        private RectTransform canvas;
        private object previousOverride;
        private object previousInsetOverride;
        private Type controllerType;
        private readonly Vector2 safeMin = new Vector2(0f, 102f / 2556f);
        private readonly Vector2 safeMax = new Vector2(1f, 1f - 177f / 2556f);

        [SetUp]
        public void SetUp()
        {
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            controllerType = RuntimeType("WitchTower.UI.SafeAreaLayoutController");
            previousOverride = controllerType.GetProperty("SafeAreaOverride").GetValue(null);
            previousInsetOverride = controllerType.GetProperty("VerticalInsetRatioOverride").GetValue(null);
            controllerType.GetProperty("VerticalInsetRatioOverride").SetValue(null, null);
            SetInsets();
            // Match the real home canvas: scene-owned panels intentionally reject
            // unrelated canvases (including persistent communication overlays).
            var canvasObject = new GameObject("HomeCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas = (RectTransform)canvasObject.transform;
            canvas.sizeDelta = new Vector2(1080f, 2556f * 1080f / 1179f);
            var owner = new GameObject("FirstFrameSafeArea");
            // Explicit lifecycle in edit mode: no game managers or save files.
            owner.SetActive(false);
            controller = owner.AddComponent(controllerType);
            Invoke(controller, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (controller != null) Invoke(controller, "OnDisable");
            controllerType.GetProperty("SafeAreaOverride").SetValue(null, previousOverride);
            controllerType.GetProperty("VerticalInsetRatioOverride").SetValue(null, previousInsetOverride);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (originalScenes.Length > 0 && originalScenes.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LazyHomePageIsFittedOnItsFirstRenderAndDoesNotJumpLater(bool dungeon)
        {
            // Opening a page immediately after a render must not wait for a
            // timer or the next frame, including after a fresh Home scene.
            Canvas.ForceUpdateCanvases();
            RectTransform root = NewRoot(dungeon ? "DungeonSelectionPanel" : "GoldShopPanel", canvas);
            Component page = root.gameObject.AddComponent(RuntimeType(dungeon
                ? "WitchTower.Home.DungeonSelectionPanelController"
                : "WitchTower.Home.GoldShopPanelController"));
            Invoke(page, dungeon ? "EnsurePanel" : "Build");
            root.gameObject.SetActive(true);
            var button = (RectTransform)root.Find(dungeon ? "CloseButton" : "ShopCloseButton");
            var title = (RectTransform)root.Find(dungeon ? "DungeonSelectionFrame/Title" : "ShopMainPanel/Title");
            var background = (RectTransform)root.Find(dungeon ? "DungeonSelectionBackground" : "ShopBackground");
            Assert.That(button, Is.Not.Null, "The page must build under its scene-owned HomeCanvas.");
            Assert.That(title, Is.Not.Null);
            Assert.That(background, Is.Not.Null);
            Vector2 originalTitleSize = title.rect.size;
            int renderCount = 0;
            bool fittedAtFirstRender = false;
            Rect firstButton = default, firstTitle = default;
            Canvas.WillRenderCanvases observe = () =>
            {
                if (renderCount++ != 0) return;
                fittedAtFirstRender = Vector2.Distance(root.anchorMin, safeMin) < 0.00001f &&
                                      Vector2.Distance(root.anchorMax, safeMax) < 0.00001f;
                firstButton = Bounds(button);
                firstTitle = Bounds(title);
            };
            Canvas.willRenderCanvases += observe;
            try { Canvas.ForceUpdateCanvases(); }
            finally { Canvas.willRenderCanvases -= observe; }
            Assert.That(renderCount, Is.GreaterThan(0));
            Assert.That(fittedAtFirstRender, Is.True, "The unadjusted page must never reach the first render.");
            Component fitter = root.GetComponent(RuntimeType("WitchTower.UI.SafeAreaFitter"));
            Assert.That(fitter.GetType().GetProperty("IsLayoutCurrent").GetValue(fitter), Is.True,
                "Stable layouts must not be dirtied on every render.");
            float safeTop = canvas.rect.yMax - 177f * 1080f / 1179f;
            Assert.That(firstButton.yMax, Is.EqualTo(safeTop - 48f).Within(0.1f));
            Assert.That(title.rect.size, Is.EqualTo(originalTitleSize));
            AssertRect(Bounds(background), canvas.rect);
            for (int i = 0; i < 20; i++)
            {
                Canvas.ForceUpdateCanvases();
                AssertRect(Bounds(button), firstButton);
                AssertRect(Bounds(title), firstTitle);
            }
            root.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            AssertRect(Bounds(button), firstButton);
            AssertRect(Bounds(title), firstTitle);
            Debug.Log($"[SafeAreaFirstFrame] {root.name}: first-render safe area verified; later displacement=0; no save/device operations.");
        }

        [Test]
        public void BuilderCanResetAPreviouslyDiscoveredInactiveRootWithoutLosingInsets()
        {
            var root = NewRoot("GoldShopPanel", canvas);
            root.gameObject.SetActive(false);
            // A placeholder can have different anchors from its eventual page.
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            Canvas.ForceUpdateCanvases();
            Component page = root.gameObject.AddComponent(RuntimeType("WitchTower.Home.GoldShopPanelController"));
            Invoke(page, "Build");
            root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            AssertAnchors(root, safeMin, safeMax);
            Canvas.ForceUpdateCanvases();
            AssertAnchors(root, safeMin, safeMax);
        }

        [Test]
        public void NestedCanvasAndTargetDoNotApplySafeAreaTwice()
        {
            var root = NewRoot("GoldShopPanel", canvas);
            var nestedCanvas = NewRoot("NestedCanvas", root);
            nestedCanvas.gameObject.AddComponent<Canvas>();
            var nestedTarget = NewRoot("MonsterDexPanel", nestedCanvas);
            Canvas.ForceUpdateCanvases();
            AssertAnchors(root, safeMin, safeMax);
            AssertAnchors(nestedTarget, Vector2.zero, Vector2.one);
            Assert.That(nestedTarget.GetComponent(RuntimeType("WitchTower.UI.SafeAreaFitter")), Is.Null);
        }

        [Test]
        public void ReplacementBackgroundAndSafeAreaChangesRemainFullBleedWithoutDrift()
        {
            var root = NewRoot("GoldShopPanel", canvas);
            var background = NewRoot("ShopBackground", root);
            Canvas.ForceUpdateCanvases();
            AssertRect(Bounds(background), canvas.rect);
            UnityEngine.Object.DestroyImmediate(background.gameObject);
            background = NewRoot("ShopBackground", root); // Same child count as before.
            Canvas.ForceUpdateCanvases();
            AssertRect(Bounds(background), canvas.rect);
            controllerType.GetProperty("SafeAreaOverride").SetValue(null, new Rect(0, 0, Screen.width, Screen.height));
            Canvas.ForceUpdateCanvases();
            AssertAnchors(root, Vector2.zero, Vector2.one);
            AssertRect(Bounds(background), canvas.rect);
            SetInsets();
            Canvas.ForceUpdateCanvases();
            AssertAnchors(root, safeMin, safeMax);
            AssertRect(Bounds(background), canvas.rect);
        }

        private void SetInsets() => controllerType.GetProperty("SafeAreaOverride").SetValue(null,
            new Rect(0, Screen.height * safeMin.y, Screen.width, Screen.height * (safeMax.y - safeMin.y)));

        private static RectTransform NewRoot(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvas.InverseTransformPoint(corners[0]);
            Vector3 max = canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertRect(Rect actual, Rect expected)
        {
            Assert.That(Vector2.Distance(actual.position, expected.position), Is.LessThan(0.1f));
            Assert.That(Vector2.Distance(actual.size, expected.size), Is.LessThan(0.1f));
        }

        private static void AssertAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            Assert.That(Vector2.Distance(rect.anchorMin, min), Is.LessThan(0.00001f));
            Assert.That(Vector2.Distance(rect.anchorMax, max), Is.LessThan(0.00001f));
        }

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
        private static object Invoke(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, PrivateInstance).Invoke(owner, args);
    }
}
