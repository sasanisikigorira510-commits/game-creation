using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class DungeonSelectionCanvasOwnershipTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OpeningDungeonSelectionKeepsItOnHomeCanvas(bool blockerActive)
        {
            var home = new GameObject("HomeCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("DungeonSelectionPanel", typeof(RectTransform));
            panel.transform.SetParent(home.transform, false);
            var blocker = new GameObject("OnlineInputBlocker", typeof(RectTransform), typeof(Canvas));
            blocker.GetComponent<Canvas>().sortingOrder = short.MaxValue;
            blocker.SetActive(blockerActive);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Home.DungeonSelectionPanelController", true);
                var controller = panel.AddComponent(type);
                type.GetMethod("EnsurePanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                panel.SetActive(true);
                Assert.That(panel.transform.parent, Is.EqualTo(home.transform), "Do not move the map under a persistent network overlay.");
                Assert.That(panel.activeInHierarchy, Is.True);
                Assert.That(blocker.transform.childCount, Is.Zero);
                Assert.That(blocker.activeSelf, Is.EqualTo(blockerActive));
                Assert.That(panel.transform.Find("DungeonSelectionFrame"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(home);
                UnityEngine.Object.DestroyImmediate(blocker);
            }
        }
    }
}
