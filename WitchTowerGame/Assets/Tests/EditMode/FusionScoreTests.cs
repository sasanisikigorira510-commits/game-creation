using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class FusionScoreTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object NewScore() => Activator.CreateInstance(GameType("Home.FusionScorePlayer"));
        private static object Get(object item, string name) => item.GetType().GetField(name, Fields).GetValue(item);
        private static void Set(object item, string name, object value) => item.GetType().GetField(name, Fields).SetValue(item, value);
        private static object Property(object item, string name) => item.GetType().GetProperty(name).GetValue(item);
        private static object Call(object item, string name, params object[] args) => item.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(item, args);

        [TestCase(false, 7f)] [TestCase(true, 9f)]
        public void OriginalStereoStemsMatchFilmLengthAndLeaveSpaceBeforeBirth(bool upper, float duration)
        {
            string variant = upper ? "class4" : "normal";
            var music = Resources.Load<AudioClip>("Audio/Music/Fusion/ritual_" + variant + "_music");
            var effects = Resources.Load<AudioClip>("Audio/Music/Fusion/ritual_" + variant + "_effects");
            Assert.That(music, Is.Not.Null);
            Assert.That(effects, Is.Not.Null);
            Assert.That(music.channels, Is.EqualTo(2));
            Assert.That(effects.channels, Is.EqualTo(2));
            Assert.That(music.length, Is.EqualTo(duration).Within(.005f));
            Assert.That(effects.length, Is.EqualTo(duration).Within(.005f));
            float hush = Rms(music, .70f, .735f);
            float birth = Rms(music, .755f, .85f);
            Assert.That(birth, Is.GreaterThan(.03f), "The final chord must be audible.");
            Assert.That(hush, Is.LessThan(birth * .025f), "Reverb must not fill the birth's dramatic pause.");
            var samples = new float[music.samples * music.channels];
            Assert.That(music.GetData(samples, 0), Is.True);
            Assert.That(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Mathf.Abs(x) < .90f), Is.True,
                "The authored score leaves mixing headroom for the separate ritual effects.");
            double stereoDifference = 0;
            for (int frame = (int)(music.samples * .14f); frame < music.samples * .31f; frame++)
                stereoDifference += Math.Abs(samples[frame*2] - samples[frame*2+1]);
            Assert.That(stereoDifference, Is.GreaterThan(20d), "The two parent motifs must occupy different stereo positions.");
        }

        [TestCase(false, 7f, 1f)] [TestCase(true, 9f, 1f)]
        [TestCase(false, 14f, .5f)] [TestCase(true, 12f, .75f)]
        public void DurationAdjustmentKeepsBothStemsTogetherAndStopsAtTheResult(bool upper, float duration, float pitch)
        {
            var score = NewScore();
            try
            {
                Assert.That(Property(score, "IsAvailable"), Is.True);
                Call(score, "Begin", upper, duration);
                var music = (AudioSource)Get(score, "musicSource");
                var effects = (AudioSource)Get(score, "effectsSource");
                Assert.That(music.clip.name, Does.Contain(upper ? "class4" : "normal"));
                Assert.That(effects.clip.name, Does.Contain(upper ? "class4" : "normal"));
                Assert.That(music.pitch, Is.EqualTo(pitch).Within(.0001f));
                Assert.That(effects.pitch, Is.EqualTo(pitch).Within(.0001f));
                Call(score, "Tick", duration * .755f);
                Assert.That(Property(score, "IsPlaying"), Is.True);
                Call(score, "Tick", duration);
                Assert.That(Property(score, "IsPlaying"), Is.False);
                Assert.That(music.clip, Is.Null);
                Assert.That(effects.clip, Is.Null);
                Call(score, "Tick", duration + 100f);
                Assert.That(Property(score, "IsPlaying"), Is.False);
            }
            finally { ((IDisposable)score).Dispose(); }
        }

        [Test]
        public void LiveVolumesAndMuteAffectOnlyTheirBusWithoutWritingSoundPreferences()
        {
            var managerObject = new GameObject("FusionAudioSettingsTest");
            managerObject.SetActive(false);
            var managerType = GameType("Managers.AudioManager");
            var singleton = managerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = singleton.GetValue(null);
            object score = null;
            float savedBgm = PlayerPrefs.GetFloat("witchtower_audio_bgm_volume", -.5f);
            float savedSe = PlayerPrefs.GetFloat("witchtower_audio_se_volume", -.5f);
            try
            {
                var manager = managerObject.AddComponent(managerType);
                singleton.SetValue(null, manager);
                Set(manager, "bgmVolume", 0f);
                Set(manager, "seVolume", .42f);
                score = NewScore();
                Call(score, "Begin", true, 9f);
                var music = (AudioSource)Get(score, "musicSource");
                var effects = (AudioSource)Get(score, "effectsSource");
                Assert.That(music.volume, Is.Zero);
                Assert.That(effects.volume, Is.EqualTo(.42f * .72f).Within(.0001f));
                Set(manager, "bgmVolume", .3f);
                Set(manager, "seVolume", 0f);
                Call(score, "Tick", 2f);
                Assert.That(music.volume, Is.EqualTo(.3f * .9f).Within(.0001f));
                Assert.That(effects.volume, Is.Zero);
                Assert.That(PlayerPrefs.GetFloat("witchtower_audio_bgm_volume", -.5f), Is.EqualTo(savedBgm));
                Assert.That(PlayerPrefs.GetFloat("witchtower_audio_se_volume", -.5f), Is.EqualTo(savedSe));
            }
            finally
            {
                (score as IDisposable)?.Dispose();
                singleton.SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [TestCase("Stop")] [TestCase("Dispose")] [TestCase("Complete")] [TestCase("BeginAgain")]
        public void EveryExitReleasesOnlyItsOwnAmbientMusicLease(string exit)
        {
            var owner = new GameObject("FusionAudioLeaseTest");
            owner.SetActive(false);
            var manager = owner.AddComponent(GameType("Managers.AudioManager"));
            var score = NewScore();
            IDisposable outer = null;
            try
            {
                Call(score, "Begin", false, 7f);
                outer = (IDisposable)Call(manager, "SuppressBgm");
                // EditMode does not start live audio. Attach a real lease so the
                // same stop paths still prove the manager's restoration contract.
                Set(score, "ambientSuppression", Call(manager, "SuppressBgm"));
                if (exit == "Complete") Call(score, "Tick", 7f);
                else if (exit == "BeginAgain") Call(score, "Begin", true, 9f);
                else Call(score, exit);
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.True, "Another active music owner keeps its suppression.");
                outer.Dispose(); outer = null;
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.False, "The fusion's old scope cannot outlive its exit.");
                Call(score, "Stop");
                Call(score, "Stop");
                Assert.That(Property(score, "IsPlaying"), Is.False);
            }
            finally
            {
                outer?.Dispose();
                ((IDisposable)score).Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static float Rms(AudioClip clip, float start, float end)
        {
            int offset = Mathf.FloorToInt(clip.samples * start);
            var values = new float[Mathf.FloorToInt(clip.samples * (end-start)) * clip.channels];
            Assert.That(clip.GetData(values, offset), Is.True);
            double sum = 0;
            foreach (float value in values) sum += value * value;
            return (float)Math.Sqrt(sum / values.Length);
        }
    }
}
