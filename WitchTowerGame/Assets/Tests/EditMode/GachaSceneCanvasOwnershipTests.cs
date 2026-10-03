using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class GachaSceneCanvasOwnershipTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void GachaUsesItsOwnCanvasWithoutTouchingOnlineInputBlocker(bool blockerActive)
        {
            var canvas = new GameObject("GachaCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("GachaScenePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var blocker = new GameObject("OnlineInputBlocker", typeof(RectTransform), typeof(Canvas));
            blocker.GetComponent<Canvas>().sortingOrder = short.MaxValue;
            blocker.transform.localScale = new Vector3(2f, 2f, 2f);
            blocker.SetActive(blockerActive);
            var owner = new GameObject("GachaSceneRoot");
            owner.SetActive(false);
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp");
                panel.AddComponent(assembly.GetType("WitchTower.Home.GachaPanelController", true));
                var type = assembly.GetType("WitchTower.Home.GachaSceneController", true);
                var controller = owner.AddComponent(type);
                type.GetMethod("NormalizeCanvasScales", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                var result = (Component)type.GetMethod("EnsurePanel", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(controller, null);
                Assert.That(result.gameObject, Is.SameAs(panel), "Reuse the scene panel, never build inside the network blocker.");
                Assert.That(result.gameObject.activeInHierarchy, Is.True);
                Assert.That(blocker.transform.childCount, Is.Zero);
                Assert.That(blocker.activeSelf, Is.EqualTo(blockerActive));
                Assert.That(blocker.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
                Assert.That(blocker.GetComponent<Canvas>().sortingOrder, Is.EqualTo(short.MaxValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(canvas);
                UnityEngine.Object.DestroyImmediate(blocker);
            }
        }
    }
}
