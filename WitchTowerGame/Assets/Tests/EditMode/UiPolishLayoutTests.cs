using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class UiPolishLayoutTests
    {
        [TestCase(1179, 2556)]
        [TestCase(750, 1334)]
        public void FusionInheritanceExplanationFitsAndDismisses(int width, int height)
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var root = new GameObject("HelpTestCanvas", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080, height * 1080f / width);
                var panel = new GameObject("FusionTest", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Home.MonsterFusionPanelController", true);
                var controller = panel.AddComponent(type);
                type.GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                var button = panel.GetComponentsInChildren<Button>().Single(x => x.name == "InheritanceHelpButton");
                button.onClick.Invoke();
                var modal = panel.GetComponentsInChildren<RectTransform>().Single(x => x.name == "InheritanceHelp");
                var body = modal.Find("Panel/Body").GetComponent<Text>();
                Assert.That(body.text, Does.Contain("50%"));
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height));
                Assert.That(modal.Find("Panel").GetComponent<Image>().color.a, Is.EqualTo(1));
                string folder = Environment.GetEnvironmentVariable("WITCHTOWER_TEN_PULL_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(folder))
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] {canvas, width, height, System.IO.Path.Combine(folder, "fusion-help-" + width + ".png")});
                modal.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();
                Assert.That(panel.GetComponentsInChildren<RectTransform>().Any(x => x.name == "InheritanceHelp"), Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (scenes.Length > 0 && scenes.All(x => !string.IsNullOrEmpty(x.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
    }
}
