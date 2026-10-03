using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class GachaFreshPressTests
    {
        private static int summons;
        [UnityTest]
        public IEnumerator OpeningTouchAndRepeatedReleaseCannotTriggerSummon()
        {
            yield return new EnterPlayMode();
            var owner = new GameObject("FreshSummonPressTest", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(owner);
            Type type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                .GetType("WitchTower.UI.FreshPressButton", true);
            var button = (Button)owner.AddComponent(type);
            summons = 0;
            button.onClick.AddListener(() => summons++);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };

            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(summons, Is.Zero, "The touch that opened this screen must not summon.");
            yield return null;
            yield return null;
            button.OnPointerClick(pointer);
            Assert.That(summons, Is.Zero, "A release without a new press must not summon.");

            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            button.OnPointerClick(pointer);
            Assert.That(summons, Is.EqualTo(1), "One deliberate tap must summon exactly once.");

            owner.SetActive(false);
            owner.SetActive(true);
            button.OnPointerClick(pointer);
            Assert.That(summons, Is.EqualTo(1), "Returning from results must not summon again.");
            yield return null;
            yield return null;
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(summons, Is.EqualTo(2), "The next deliberate tap remains usable.");
            UnityEngine.Object.Destroy(owner);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
