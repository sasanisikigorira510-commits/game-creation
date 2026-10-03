using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace WitchTower.Tests
{
    public sealed class EquipmentEnhanceGuideLayoutTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ShopHeaderClearsHomeButtonAndGuideHasYellowFrame()
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvasObject = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)canvasObject.transform;
                canvas.sizeDelta = new Vector2(1080, 2341);
                var shop = new GameObject("GoldShopPanel", typeof(RectTransform));
                shop.transform.SetParent(canvas, false);
                Component controller = shop.AddComponent(RuntimeType("WitchTower.Home.GoldShopPanelController"));
                Invoke(controller, "Build");
                var guide = (GameObject)Field(controller, "shopTutorialGuideRoot");
                guide.SetActive(true);
                var homeFrame = (Image)Field(controller, "shopTutorialHomeHighlight");
                homeFrame.gameObject.SetActive(true);
                var frame = guide.transform.Find("ShopTutorialGuideFrame").GetComponent<Image>();
                Assert.That(frame.sprite, Is.Not.Null);
                Assert.That(frame.sprite.texture.name, Is.EqualTo("TutorialSummonHighlightFrameImage2"));
                Assert.That(frame.raycastTarget, Is.False);
                Canvas.ForceUpdateCanvases();
                var home = (RectTransform)shop.transform.Find("ShopCloseButton");
                var title = (RectTransform)shop.transform.Find("ShopMainPanel/Title");
                Assert.That(BoundsIn(canvas, title).yMax, Is.LessThan(BoundsIn(canvas, home).yMin - 24f));
                string capture = Environment.GetEnvironmentVariable("WITCHTOWER_SHOP_CAPTURE_PATH");
                if (!string.IsNullOrEmpty(capture)) Capture(canvas, 1179, 2556, capture);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

        [TestCase(1179, 2556, 177, 102)]
        [TestCase(750, 1334, 40, 0)]
        [TestCase(640, 1136, 40, 0)]
        [TestCase(768, 1024, 24, 20)]
        public void EquipmentHomeReturnUsesSafeAreaWithoutMovingEquipment(int width, int height, int top, int bottom)
        {
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvasObject = new GameObject("EquipmentNavigationCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvasObject.transform;
                canvasRect.sizeDelta = new Vector2(1080f, height * 1080f / width);
                var controllerObject = new GameObject("EquipmentNavigationController");
                controllerObject.SetActive(false);
                Component controller = controllerObject.AddComponent(RuntimeType("WitchTower.Core.TitleSceneController"));
                Invoke(controller, "EnsureEquipmentScene");
                var root = (GameObject)Field(controller, "equipmentSceneRoot");
                root.SetActive(true);
                var button = (Button)Field(controller, "equipmentHomeReturnButton");
                var buttonRect = (RectTransform)button.transform;
                var panel = (RectTransform)root.transform.Find("EquipmentPanel");
                var background = (RectTransform)root.transform.Find("EquipmentBackground");
                Canvas.ForceUpdateCanvases();
                Rect originalPanel = BoundsIn(canvasRect, panel);
                Rect originalBackground = BoundsIn(canvasRect, background);
                Component safeAreaController = controllerObject.AddComponent(RuntimeType("WitchTower.UI.SafeAreaLayoutController"));
                var safeArea = new Rect(0, bottom, width, height - top - bottom);
                var screenSize = new Vector2Int(width, height);
                Invoke(safeAreaController, "ApplySafeArea", safeArea, screenSize, true);
                Canvas.ForceUpdateCanvases();
                Rect bounds = BoundsIn(canvasRect, buttonRect);
                float safeTop = canvasRect.rect.yMax - top * 1080f / width;
                Assert.That(bounds.xMin - canvasRect.rect.xMin, Is.EqualTo(64f).Within(0.1f));
                Assert.That(safeTop - bounds.yMax, Is.EqualTo(48f).Within(0.1f));
                Assert.That(bounds.size, Is.EqualTo(new Vector2(240, 78)), "Keep the existing tap target and readable label.");
                Assert.That(button.interactable && button.GetComponent<Image>().raycastTarget, Is.True);
                if (height > width * 1.5f)
                    Assert.That(bounds.yMin, Is.GreaterThan(originalPanel.yMax + 12f), "Do not cover the equipment header on small phones.");
                Assert.That(BoundsIn(canvasRect, panel), Is.EqualTo(originalPanel));
                Assert.That(BoundsIn(canvasRect, background), Is.EqualTo(originalBackground));

                string capturePath = Environment.GetEnvironmentVariable("WITCHTOWER_EQUIPMENT_RETURN_CAPTURE_PATH");
                if (width == 1179 && !string.IsNullOrEmpty(capturePath))
                    Capture(canvasRect, width, height, capturePath);

                Invoke(safeAreaController, "ApplySafeArea", safeArea, screenSize, false);
                Assert.That(BoundsIn(canvasRect, buttonRect), Is.EqualTo(bounds), "Discovery scans must not drift the button.");
                Invoke(safeAreaController, "ApplySafeArea", new Rect(0, 0, width, height), screenSize, true);
                Assert.That(canvasRect.rect.yMax - BoundsIn(canvasRect, buttonRect).yMax, Is.EqualTo(48f).Within(0.1f), "Changing safe areas must use the original anchors.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (originalScenes.Length > 0 && originalScenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            }
        }

        [TestCase(1179, 2556, 1, false)]
        [TestCase(750, 1334, 1, false)]
        [TestCase(1179, 2556, 3, false)]
        [TestCase(750, 1334, 3, false)]
        [TestCase(768, 1024, 1, false)]
        [TestCase(1179, 2556, 0, true)]
        public void EnhancementGuideStaysBelowRelicRows(int width, int height, int relicCount, bool success)
        {
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            Type gameType = RuntimeType("WitchTower.Managers.GameManager");
            Type masterType = RuntimeType("WitchTower.Managers.MasterDataManager");
            object previousGame = gameType.GetProperty("Instance").GetValue(null);
            object previousMaster = masterType.GetProperty("Instance").GetValue(null);
            UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvasObject = new GameObject("EnhanceLayoutCanvas", typeof(RectTransform), typeof(Canvas));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(1080f, height * 1080f / width);
                var controllerObject = new GameObject("EnhanceLayoutController");
                controllerObject.SetActive(false);
                Component game = controllerObject.AddComponent(gameType);
                gameType.GetProperty("Instance").SetValue(null, game);
                Component master = controllerObject.AddComponent(masterType);
                masterType.GetProperty("Instance").SetValue(null, master);
                masterType.GetMethod("Initialize").Invoke(master, null);

                Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
                object save = saveType.GetMethod("CreateDefault").Invoke(null, null);
                saveType.GetField("TutorialStepId").SetValue(save, "T07B");
                saveType.GetField("InitialTutorialSummonCount").SetValue(save, 3);
                object profile = Activator.CreateInstance(RuntimeType("WitchTower.Data.PlayerProfile"), new[] { save });
                gameType.GetProperty("PlayerProfile").SetValue(game, profile);
                object monster = profile.GetType().GetMethod("AddOwnedMonster").Invoke(profile, new object[] { "monster_apprentice_mage", 1, 0, false });
                Type tutorial = RuntimeType("WitchTower.Data.StoryTutorialService");
                // This fixture starts after the manual/auto equip lessons.
                tutorial.GetMethod("MarkHintSeen").Invoke(null, new[] { profile, (object)"tutorial_equipment_auto_equip" });
                tutorial.GetMethod("EnsureEquipmentTutorialGift").Invoke(null, new[] { profile });
                object equipment = tutorial.GetMethod("FindEquipmentTutorialGift").Invoke(null, new[] { profile });
                Assert.That(equipment, Is.Not.Null);
                equipment.GetType().GetField("EquippedMonsterInstanceId").SetValue(equipment, monster.GetType().GetField("InstanceId").GetValue(monster));
                equipment.GetType().GetField("IsEquipped").SetValue(equipment, true);
                var relics = ((IEnumerable)RuntimeType("WitchTower.Data.EquipmentEnhancementCatalog").GetProperty("AllRelics").GetValue(null)).Cast<object>();
                foreach (object relic in relics.Take(relicCount))
                    profile.GetType().GetMethod("AddEnhancementRelics").Invoke(profile, new[] { relic.GetType().GetField("RelicId").GetValue(relic), (object)1 });

                Component controller = controllerObject.AddComponent(RuntimeType("WitchTower.Core.TitleSceneController"));
                Invoke(controller, "EnsureEquipmentScene");
                SetField(controller, "selectedEquipmentEnhanceInstanceId", equipment.GetType().GetField("InstanceId").GetValue(equipment));
                if (success)
                {
                    const string relicId = "relic_safe_ember";
                    profile.GetType().GetMethod("AddEnhancementRelics").Invoke(profile, new object[] { relicId, 1 });
                    profile.GetType().GetMethod("TryEnhanceEquipment").Invoke(profile, new object[] {
                        equipment.GetType().GetField("InstanceId").GetValue(equipment), relicId });
                    SetField(controller, "equipmentEnhanceLastActionMessage", "見習いの護符の強化に成功しました。");
                }
                SetField(controller, "equipmentEnhanceTutorialSuccessPending", success);
                ((GameObject)Field(controller, "equipmentEnhanceOverlayRoot")).SetActive(true);
                Invoke(controller, "RefreshEquipmentEnhancementOverlay", profile);
                Canvas.ForceUpdateCanvases();

                var info = (Text)Field(controller, "equipmentEnhanceOverlayInfoText");
                var targetTitle = (Text)Field(controller, "equipmentEnhanceOverlayTitleText");
                var close = (Button)Field(controller, "equipmentEnhanceCloseButton");
                var ritual = (RectTransform)info.transform.parent.Find("EquipmentEnhanceRitualArea");
                Assert.That(info.fontSize, Is.GreaterThanOrEqualTo(28));
                Assert.That(info.text, Does.Contain("現在"), "Stats stay visible during the lesson, too.");
                Assert.That(info.preferredHeight, Is.LessThanOrEqualTo(info.rectTransform.rect.height + 1f));
                Assert.That(targetTitle.preferredHeight, Is.LessThanOrEqualTo(targetTitle.rectTransform.rect.height + 1f));
                Assert.That(BoundsIn(canvasRect, info.rectTransform).yMin, Is.GreaterThan(BoundsIn(canvasRect, ritual).yMax));
                Assert.That(BoundsIn(canvasRect, close.GetComponent<RectTransform>()).size.y, Is.GreaterThanOrEqualTo(72f));
                Assert.That(close.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(28));
                var resultLabel = (Text)Field(controller, "equipmentEnhanceOverlayResultText");
                if (success)
                    Assert.That(resultLabel.preferredHeight, Is.LessThanOrEqualTo(resultLabel.rectTransform.rect.height + 1f));
                var guide = (GameObject)Field(controller, "equipmentEnhanceTutorialGuideRoot");
                var list = (RectTransform)Field(controller, "equipmentEnhanceOverlayListRect");
                Assert.That(guide.activeSelf, Is.True);
                Rect guideBounds = BoundsIn(canvasRect, (RectTransform)guide.transform);
                var viewport = (RectTransform)Field(controller, "equipmentEnhanceOverlayViewportRect");
                float contentBottom = BoundsIn(canvasRect, viewport).yMin;
                foreach (Text text in list.GetComponentsInChildren<Text>())
                {
                    if (!text.name.Contains("CardDesc") && !text.name.Contains("CardMeta")) continue;
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(26));
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name);
                }
                Debug.Log($"[EnhanceGuideLayout] {width}x{height}, relics={relicCount}, success={success}, gap={contentBottom - guideBounds.yMax:0.0}");
                string capturePath = Environment.GetEnvironmentVariable("WITCHTOWER_ENHANCE_CAPTURE_PATH");
                if (width == 1179 && relicCount == 1 && !success && !string.IsNullOrEmpty(capturePath))
                    Capture(canvasRect, width, height, capturePath);
                string successCapture = Environment.GetEnvironmentVariable("WITCHTOWER_ENHANCE_SUCCESS_CAPTURE");
                if (width == 1179 && success && !string.IsNullOrEmpty(successCapture))
                    Capture(canvasRect, width, height, successCapture);
                Assert.That(guideBounds.yMax, Is.LessThanOrEqualTo(contentBottom - 24f), "The full guide must clear every relic row, including its description and use button.");
                Assert.That(guideBounds.yMin, Is.GreaterThanOrEqualTo(canvasRect.rect.yMin + 24f));
                Assert.That(guideBounds.xMin, Is.GreaterThanOrEqualTo(canvasRect.rect.xMin + 24f));
                Assert.That(guideBounds.xMax, Is.LessThanOrEqualTo(canvasRect.rect.xMax - 24f));
                Assert.That(((Text)Field(controller, "equipmentEnhanceTutorialGuideBodyText")).resizeTextMinSize, Is.GreaterThanOrEqualTo(28));
                Assert.That(guide.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
                foreach (Button button in list.GetComponentsInChildren<Button>())
                {
                    Assert.That(button.interactable, Is.True);
                    if (BoundsIn(canvasRect, viewport).Overlaps(BoundsIn(canvasRect, (RectTransform)button.transform)))
                        Assert.That(guideBounds.Overlaps(BoundsIn(canvasRect, (RectTransform)button.transform)), Is.False);
                }
                Invoke(controller, "RefreshEquipmentEnhancementOverlay", profile);
                Canvas.ForceUpdateCanvases();
                Assert.That(BoundsIn(canvasRect, (RectTransform)guide.transform).yMax, Is.EqualTo(guideBounds.yMax).Within(0.1f), "Refresh must not drift the guide.");
            }
            finally
            {
                UnityEngine.Random.state = previousRandom;
                gameType.GetProperty("Instance").SetValue(null, previousGame);
                masterType.GetProperty("Instance").SetValue(null, previousMaster);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (originalScenes.Length > 0 && originalScenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            }
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        private static void Capture(RectTransform canvas, int width, int height, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var cameraObject = new GameObject("EnhanceLayoutCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = canvas.rect.height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static object Field(object value, string field) => value.GetType().GetField(field, PrivateInstance).GetValue(value);
        private static void SetField(object value, string field, object data) => value.GetType().GetField(field, PrivateInstance).SetValue(value, data);
        private static object Invoke(object value, string method, params object[] args) => value.GetType().GetMethod(method, PrivateInstance).Invoke(value, args);
    }
}
