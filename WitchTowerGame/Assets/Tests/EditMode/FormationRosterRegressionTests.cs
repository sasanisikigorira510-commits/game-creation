using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchTower.Tests
{
    public sealed class FormationRosterRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

        [TestCase(3, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(5, true)]
        [TestCase(5, false)]
        public void ReloadedFormationShowsOnlySavedMonsters(int ownedCount, bool completedTutorial)
        {
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            Type gameType = RuntimeType("WitchTower.Managers.GameManager");
            Type masterType = RuntimeType("WitchTower.Managers.MasterDataManager");
            object previousGame = gameType.GetProperty("Instance").GetValue(null);
            object previousMaster = masterType.GetProperty("Instance").GetValue(null);
            GameObject managers = null;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/FormationScene.unity", OpenSceneMode.Single);
                var controllerType = RuntimeType("WitchTower.Formation.FormationSceneController");
                var controller = UnityEngine.Object.FindFirstObjectByType(controllerType);
                Assert.That(controller, Is.Not.Null);
                Transform content = GameObject.Find("FormationUiRoot").transform.Find("RosterPanel/Viewport/Content");
                Assert.That(content.childCount, Is.EqualTo(5), "Editor preview must reproduce the five sample cards.");
                Assert.That(content.Cast<Transform>().All(t => (t.gameObject.hideFlags & HideFlags.DontSaveInBuild) != 0),
                    "Editor preview cards must be excluded from player builds.");

                // Scene/build reload retains child GameObjects, but not this private list.
                ((IList)controllerType.GetField("rosterViews", PrivateInstance).GetValue(controller)).Clear();
                Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
                object save = saveType.GetMethod("CreateDefault").Invoke(null, null);
                saveType.GetField("TutorialStepId").SetValue(save, completedTutorial ? "Complete" : "T04");
                saveType.GetField("HasCompletedTutorial").SetValue(save, completedTutorial);
                saveType.GetField("InitialTutorialSummonCount").SetValue(save, 3);
                IList monsters = (IList)saveType.GetField("OwnedMonsters").GetValue(save);
                string[] species = { "monster_dragon_whelp", "monster_rock_golem", "monster_apprentice_mage", "monster_dragon_whelp", "monster_chibi_gear" };
                Type monsterType = RuntimeType("WitchTower.Save.OwnedMonsterData");
                for (int i = 0; i < ownedCount; i++)
                {
                    object monster = Activator.CreateInstance(monsterType);
                    monsterType.GetField("InstanceId").SetValue(monster, "saved-" + i);
                    monsterType.GetField("MonsterId").SetValue(monster, species[i]);
                    monsterType.GetField("Level").SetValue(monster, 1);
                    monsterType.GetField("AcquiredOrder").SetValue(monster, i + 1);
                    monsters.Add(monster);
                }
                object profile = Activator.CreateInstance(RuntimeType("WitchTower.Data.PlayerProfile"), new[] { save });
                managers = new GameObject("FormationRegressionManagers");
                var game = managers.AddComponent(gameType);
                gameType.GetProperty("Instance").SetValue(null, game);
                gameType.GetProperty("PlayerProfile").SetValue(game, profile);
                var master = managers.AddComponent(masterType);
                masterType.GetProperty("Instance").SetValue(null, master);
                masterType.GetMethod("Initialize").Invoke(master, null);
                ((IList)controllerType.GetField("roster", PrivateInstance).GetValue(controller)).Clear();
                ((IList)controllerType.GetField("selectedMonsters", PrivateInstance).GetValue(controller)).Clear();
                controllerType.GetMethod("TrySeedRosterFromPlayerProfile", PrivateInstance).Invoke(controller, null);
                controllerType.GetMethod("RefreshView", PrivateInstance).Invoke(controller, null);
                string[] cardNames = content.Cast<Transform>().Where(t => t.gameObject.activeSelf).Select(t => t.name).ToArray();
                Canvas.ForceUpdateCanvases();
                foreach (Transform card in content)
                {
                    Transform body = card.Find("Body");
                    var nameBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(body, body.Find("NameLabel"));
                    var actionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(body, body.Find("SelectionButton"));
                    Assert.That(nameBounds.min.y, Is.GreaterThan(actionBounds.max.y), "Name and remove action must have separate rows.");
                    foreach (string label in new[] { "LevelLabel", "IndividualValueLabel" })
                    {
                        var text = body.Find(label).GetComponent<UnityEngine.UI.Text>();
                        Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(22));
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f));
                    }
                }
                Debug.Log($"[FormationRegression] saved={ownedCount}, visible={cardNames.Length}, cards={string.Join(",", cardNames)}");
                if (ownedCount == 3 && !completedTutorial &&
                    Environment.GetEnvironmentVariable("WITCHTOWER_CAPTURE_FORMATION") == "1" &&
                    SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    CaptureFormation();
                Assert.That(cardNames, Is.EquivalentTo(Enumerable.Range(0, ownedCount).Select(i => "Card_saved-" + i)),
                    "Preview cards must not survive a reload or overlay the real roster.");
                // Repeated refreshes must not accumulate cards or retain clickable old ones.
                for (int i = 0; i < 3; i++)
                    controllerType.GetMethod("RefreshView", PrivateInstance).Invoke(controller, null);
                Assert.That(content.childCount, Is.EqualTo(ownedCount));
                Assert.That(((IList)profile.GetType().GetProperty("OwnedMonsters").GetValue(profile)).Count, Is.EqualTo(ownedCount));
            }
            finally
            {
                gameType.GetProperty("Instance").SetValue(null, previousGame);
                masterType.GetProperty("Instance").SetValue(null, previousMaster);
                if (managers != null) UnityEngine.Object.DestroyImmediate(managers);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (originalScenes.Length > 0 && originalScenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
            }
        }

        private static void CaptureFormation()
        {
            var target = new RenderTexture(1179, 2556, 24);
            var capture = new Texture2D(1179, 2556, TextureFormat.RGB24, false);
            var cameraObject = new GameObject("FormationVerificationCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 960f;
            camera.targetTexture = target;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 1179, 2556), 0, 0);
            capture.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "witchtower-formation-2332-fixed.png"), capture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
