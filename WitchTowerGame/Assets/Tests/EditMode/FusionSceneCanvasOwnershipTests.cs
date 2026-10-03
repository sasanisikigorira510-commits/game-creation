using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class FusionSceneCanvasOwnershipTests
    {
        private Scene previousActiveScene;
        private Scene fusionScene;
        private Scene foreignScene;
        private Type controllerType;
        private Type panelType;

        [SetUp]
        public void SetUp()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                // Preview scenes reproduce the ownership boundary of a persistent
                // network overlay without saving, replacing, or changing the user's
                // active scene (which may be an unsaved untitled scene).
                foreignScene = EditorSceneManager.NewPreviewScene();
                fusionScene = EditorSceneManager.NewPreviewScene();
                Assembly runtime = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp");
                controllerType = runtime.GetType("WitchTower.Home.FusionSceneController", true);
                panelType = runtime.GetType("WitchTower.Home.MonsterFusionPanelController", true);
            }
            catch
            {
                RestoreScenes();
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            RestoreScenes();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FusionReusesItsScenePanelWithoutAdoptingOrChangingForeignOverlays(bool blockerActive, bool localCanvasActive)
        {
            GameObject blocker = CreateCanvas("OnlineInputBlocker", foreignScene, blockerActive);
            GameObject blockerChild = CreateRoot("BlockInput", foreignScene);
            blockerChild.transform.SetParent(blocker.transform, false);
            GameObject sameNamedForeignCanvas = CreateCanvas("FusionCanvas", foreignScene, true);
            sameNamedForeignCanvas.transform.localScale = new Vector3(3f, 3f, 3f);
            GameObject localCanvas = CreateCanvas("FusionCanvas", fusionScene, localCanvasActive);
            localCanvas.transform.localScale = new Vector3(4f, 4f, 4f);
            localCanvas.GetComponent<Canvas>().enabled = false;
            GameObject existingPanel = CreateRoot("FusionScenePanel", fusionScene);
            existingPanel.AddComponent<RectTransform>();
            existingPanel.AddComponent<Image>();
            existingPanel.AddComponent(panelType);
            existingPanel.transform.SetParent(localCanvas.transform, false);
            GameObject previewLabel = new GameObject("StatusLabel", typeof(RectTransform), typeof(Text));
            previewLabel.transform.SetParent(existingPanel.transform, false);
            previewLabel.GetComponent<Text>().text = "プレイヤーデータがありません。";
            Component controller = CreateController();

            Invoke(controller, "NormalizeCanvasScales");
            Component result = (Component)Invoke(controller, "EnsurePanel");

            Assert.That(result.gameObject, Is.SameAs(existingPanel), "Bind the real scene panel instead of leaving its editor preview behind.");
            Assert.That(result.GetComponentInParent<Canvas>(), Is.SameAs(localCanvas.GetComponent<Canvas>()));
            Assert.That(result.gameObject.scene, Is.EqualTo(fusionScene));
            Assert.That(result.gameObject.activeInHierarchy, Is.True);
            Assert.That(localCanvas.GetComponent<Canvas>().isActiveAndEnabled, Is.True);
            Assert.That(localCanvas.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(previewLabel.transform.parent, Is.SameAs(existingPanel.transform), "Canvas ownership must not discard the existing preview's content.");
            AssertForeignUnchanged(blocker, blockerActive, blockerChild);
            Assert.That(sameNamedForeignCanvas.transform.localScale, Is.EqualTo(new Vector3(3f, 3f, 3f)));
            Assert.That(sameNamedForeignCanvas.transform.childCount, Is.Zero);
            Assert.That(sameNamedForeignCanvas.GetComponent<Canvas>().sortingOrder, Is.EqualTo(short.MaxValue));

            // Repeated entry and later network busy transitions keep the same
            // panel visible; an inactive overlay must never hide the scene UI.
            blocker.SetActive(!blockerActive);
            Assert.That(Invoke(controller, "EnsurePanel"), Is.SameAs(result));
            Assert.That(existingPanel.activeInHierarchy, Is.True);
            Assert.That(blocker.transform.childCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingFusionCanvasIsCreatedInOwnerSceneWithoutChangingActiveScene(bool blockerActive)
        {
            GameObject blocker = CreateCanvas("OnlineInputBlocker", foreignScene, blockerActive);
            Component controller = CreateController();
            // Preview scenes need not (and cannot safely) become the active scene.
            // New objects begin in the normal active scene; fallback must explicitly
            // move the canvas to its controller's scene rather than leave it there.
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousActiveScene));
            Assert.That(SceneManager.GetActiveScene(), Is.Not.EqualTo(fusionScene));

            Component result = (Component)Invoke(controller, "EnsurePanel");
            Canvas canvas = result.GetComponentInParent<Canvas>();

            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.name, Is.EqualTo("FusionCanvas"));
            Assert.That(canvas.gameObject.scene, Is.EqualTo(fusionScene));
            Assert.That(result.gameObject.scene, Is.EqualTo(fusionScene));
            Assert.That(result.gameObject.activeInHierarchy, Is.True);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            Assert.That(canvas.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(blocker.transform.childCount, Is.Zero);
            Assert.That(blocker.activeSelf, Is.EqualTo(blockerActive));
            Assert.That(blocker.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(blocker.GetComponent<Canvas>().sortingOrder, Is.EqualTo(short.MaxValue));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousActiveScene));
        }

        private Component CreateController()
        {
            GameObject owner = CreateRoot("FusionSceneRoot", fusionScene);
            // ExecuteAlways's OnEnable must not build the editor UI during setup.
            owner.SetActive(false);
            return owner.AddComponent(controllerType);
        }

        private static GameObject CreateCanvas(string name, Scene scene, bool active)
        {
            GameObject root = CreateRoot(name, scene);
            root.AddComponent<RectTransform>();
            root.AddComponent<Canvas>();
            root.GetComponent<Canvas>().sortingOrder = short.MaxValue;
            root.transform.localScale = new Vector3(2f, 2f, 2f);
            root.SetActive(active);
            return root;
        }

        private static GameObject CreateRoot(string name, Scene scene)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static object Invoke(Component controller, string method)
        {
            return controller.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private static void AssertForeignUnchanged(GameObject blocker, bool expectedActive, GameObject existingChild)
        {
            Assert.That(blocker.activeSelf, Is.EqualTo(expectedActive));
            Assert.That(blocker.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(blocker.GetComponent<Canvas>().sortingOrder, Is.EqualTo(short.MaxValue));
            Assert.That(blocker.transform.childCount, Is.EqualTo(1));
            Assert.That(blocker.transform.GetChild(0).gameObject, Is.SameAs(existingChild));
        }

        private void RestoreScenes()
        {
            if (fusionScene.IsValid() && fusionScene.isLoaded)
                EditorSceneManager.ClosePreviewScene(fusionScene);
            if (foreignScene.IsValid() && foreignScene.isLoaded)
                EditorSceneManager.ClosePreviewScene(foreignScene);
        }
    }
}
