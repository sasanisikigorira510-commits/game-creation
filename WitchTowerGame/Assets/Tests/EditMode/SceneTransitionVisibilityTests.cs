using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class SceneTransitionVisibilityTests
    {
        [UnityTest]
        public IEnumerator BootAndMenuRoundTripsKeepUiVisibleInDestinationScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            string testSaveDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerSceneVisibility-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testSaveDirectory);
            AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.Managers.SaveManager", true)
                .GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, testSaveDirectory);
            SceneManager.LoadScene("BootScene");
            yield return WaitForScene("HomeScene");
            AssertHomeVisible();

            foreach (string sceneName in new[] { "EquipmentScene", "GachaScene", "FusionScene", "FormationScene" })
            {
                LoadWithGuard(sceneName);
                yield return WaitForScene(sceneName);
                AssertNoTransitionCanvas();
                Assert.That(UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)
                    .Any(g => g.gameObject.scene.name == sceneName && g.isActiveAndEnabled),
                    Is.True, sceneName + " must have visible graphics.");
                LoadWithGuard("HomeScene");
                yield return WaitForScene("HomeScene");
                AssertHomeVisible();
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        private static void LoadWithGuard(string name)
        {
            Type guard = AppDomain.CurrentDomain.GetAssemblies()
                .Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.UI.SceneTransitionGuard", true);
            guard.GetMethod("LoadScene").Invoke(null, new object[] { name });
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
            // Include Start and subsequent updates, when a stale cover used
            // to hide the newly constructed menu.
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
        }

        private static void AssertHomeVisible()
        {
            GameObject home = GameObject.Find("UnifiedHomeMenu");
            Assert.That(home, Is.Not.Null, "The runtime home must remain active after loading.");
            Assert.That(home.scene.name, Is.EqualTo("HomeScene"), "Home must not be parented to a persistent cover.");
            Canvas canvas = home.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            AssertNoTransitionCanvas();
        }

        private static void AssertNoTransitionCanvas()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(c => c.name == "WitchTowerSceneTransitionGuard"), Is.False,
                "The outgoing cover must be destroyed before the destination builds UI.");
        }
    }
}
