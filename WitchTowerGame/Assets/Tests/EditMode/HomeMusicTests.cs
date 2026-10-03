using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class HomeMusicTests
    {
        [Test]
        public void HomeUsesOriginalLoopAndRestoresItAfterBattle()
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var owner = new GameObject("HomeMusicTest");
            owner.SetActive(false);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
                    .GetType("WitchTower.Managers.AudioManager", true);
                var controller = owner.AddComponent(type);
                type.GetMethod("BuildDefaultSceneBgmMap", hidden).Invoke(controller, null);
                var map = (IDictionary)type.GetField("sceneBgmKeys", hidden).GetValue(controller);
                Assert.That(map["HomeScene"], Is.EqualTo("home_theme"));
                Assert.That(map["BattleScene"], Is.Not.EqualTo(map["HomeScene"]));
                var home = Resources.Load<AudioClip>("Audio/BGM/home_theme_loop");
                Assert.That(home, Is.Not.Null);
                Assert.That(home.length, Is.EqualTo(72f).Within(.05f));
                Assert.That(home.channels, Is.EqualTo(2));
                var play = type.GetMethod("PlayBgm", new[] { typeof(string), typeof(float) });
                foreach (string key in new[] { "home_theme", "dungeon_blight_cavern", "battle_normal", "home_theme" })
                {
                    play.Invoke(controller, new object[] { key, 0f });
                    var source = (AudioSource)type.GetField("activeBgmSource", hidden).GetValue(controller);
                    Assert.That(source.clip, Is.EqualTo(Resources.Load<AudioClip>("Audio/BGM/" + key + "_loop")));
                    Assert.That(source.clip, Is.Not.Null);
                    Assert.That(source.loop, Is.True);
                    Assert.That(type.GetProperty("CurrentBgmKey").GetValue(controller), Is.EqualTo(key));
                    if (key != "home_theme") Assert.That(source.clip, Is.Not.SameAs(home));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
