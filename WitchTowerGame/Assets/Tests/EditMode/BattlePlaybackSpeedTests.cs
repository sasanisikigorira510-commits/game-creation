using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class BattlePlaybackSpeedTests
    {
        private static Type Speed => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Battle.BattlePlaybackSpeed", true);
        private static object Call(string name, params object[] args) => Speed.GetMethod(name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
        private static float Multiplier => (float)Speed.GetProperty("Multiplier").GetValue(null);

        [SetUp] public void Setup() => Call("ResetSession");
        [TearDown] public void Cleanup() => Call("ResetSession");

        [Test]
        public void SelectionCyclesAndSessionResetRestoresNormalWithoutChangingGlobalTime()
        {
            float globalScale = Time.timeScale, physicsStep = Time.fixedDeltaTime;
            Assert.That(Multiplier, Is.EqualTo(1f));
            foreach (float expected in new[] { 2f, 3f, 5f, 10f, .5f, 1f })
            {
                Call("Cycle");
                Assert.That(Multiplier, Is.EqualTo(expected));
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(physicsStep));
            }
            Call("Cycle"); Call("ResetSession");
            Assert.That(Multiplier, Is.EqualTo(1f));
        }

        [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)] [TestCase(3f)] [TestCase(5f)] [TestCase(10f)]
        public void PlaybackSubstepsPreserveTotalTimeAndDoNotEnlargeCombatTicks(float speed)
        {
            while (Multiplier != speed) Call("Cycle");
            int count = (int)Speed.GetProperty("StepsPerFrame").GetValue(null);
            float dt = (float)Call("StepDelta", 1f / 60f);
            Assert.That(dt * count, Is.EqualTo(speed / 60f).Within(.000001f));
            Assert.That(dt, Is.InRange(0f, 1f / 60f));
            Assert.That((float)Call("StepDelta", 0f), Is.Zero, "A paused clock must stay paused.");
        }

        [Test]
        public void SettingsButtonCyclesVisibleLabelAndDoesNotPersistSelection()
        {
            var owner = new GameObject("SpeedSettingsTest"); owner.SetActive(false);
            var canvas = new GameObject("SpeedSettingsCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                Type homeType = Speed.Assembly.GetType("WitchTower.Home.HomeSceneController", true);
                var home = owner.AddComponent(homeType);
                homeType.GetMethod("BuildAudioSettingsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(home, new object[] { canvas.transform });
                var button = canvas.transform.Find("AudioSettingsPanelRoot/AudioSettingsPanel/DevelopmentGameSpeedButton").GetComponent<Button>();
                Assert.That(button.GetComponentInChildren<Text>(true).text, Does.Contain("1倍"));
                button.onClick.Invoke();
                Assert.That(button.GetComponentInChildren<Text>(true).text, Does.Contain("2倍"));
                Call("ResetSession");
                homeType.GetMethod("RefreshAudioSettingsPanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(home, null);
                Assert.That(button.GetComponentInChildren<Text>(true).text, Does.Contain("1倍"));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(canvas); }
        }
    }

    public sealed partial class GuardianPresentationTests
    {
        [TestCase(2f)] [TestCase(3f)] [TestCase(5f)] [TestCase(10f)]
        public void FasterPlaybackProducesTheSameSimulationForTheSameElapsedBattleTime(float speed)
        {
            var previousRandom = UnityEngine.Random.state;
            try
            {
                string baseline = RunPlaybackSimulation(1f);
                Assert.That(RunPlaybackSimulation(speed), Is.EqualTo(baseline));
            }
            finally
            {
                S("Battle.BattlePlaybackSpeed", "ResetSession");
                UnityEngine.Random.state = previousRandom;
            }
        }

        private string RunPlaybackSimulation(float speed)
        {
            S("Battle.BattlePlaybackSpeed", "ResetSession");
            while ((float)T("Battle.BattlePlaybackSpeed").GetProperty("Multiplier").GetValue(null) != speed)
                S("Battle.BattlePlaybackSpeed", "Cycle");
            UnityEngine.Random.InitState(7225);
            var sim = owner.AddComponent(T("Battle.BattleSimulator"));
            try
            {
                Call(sim, "Setup", 31);
                int steps = (int)T("Battle.BattlePlaybackSpeed").GetProperty("StepsPerFrame").GetValue(null);
                float dt = (float)S("Battle.BattlePlaybackSpeed", "StepDelta", 1f / 60f);
                string result = "";
                for (int frame = 0; frame < (int)(180 / speed); frame++)
                    for (int step = 0; step < steps; step++) result = Call(sim, "Tick", dt).ToString();
                return result + ":" + JsonUtility.ToJson(P(sim, "PlayerStats")) + ":" + JsonUtility.ToJson(P(sim, "EnemyStats"))
                    + ":" + P(sim, "CurrentSpawnedEnemyCount") + ":" + P(sim, "CurrentRemainingEnemyCount");
            }
            finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)sim); }
        }
    }
}
