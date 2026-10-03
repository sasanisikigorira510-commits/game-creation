using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchTower.Tests
{
    public sealed class TenPullScoreTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string BgmKey = "witchtower_audio_bgm_volume";
        private const string SeKey = "witchtower_audio_se_volume";
        private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Get(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

        private sealed class Cue
        {
            public string Bus, Resource, Phase;
            public bool Stop, Loop;
            public int Rank;
            public float Pitch, Volume, Elapsed;
        }

        private sealed class Presentation : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Panel;
            public readonly Component Controller;
            public readonly ScriptableObject Settings;
            public readonly List<Cue> Cues = new List<Cue>();
            public int LegacyCues;
            public object Music => Get(Settings, "Music");
            public object Sequence => Property(Controller, "Sequence");
            public string Phase => Property(Sequence, "Phase").ToString();

            public Presentation()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                Settings = ScriptableObject.CreateInstance(GameType("Home.TenPullPresentationSettings"));
                var root = new GameObject("ScoreTestCanvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, Scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                Panel = new GameObject("ScoreTest", typeof(RectTransform)); Panel.transform.SetParent(root.transform, false);
                Controller = Panel.AddComponent(GameType("Home.TenPullPresentationController"));
                var scoreEvent = Controller.GetType().GetEvent("ScoreCued");
                typeof(TenPullScoreTests).GetMethod(nameof(ObserveScore), BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(scoreEvent.EventHandlerType.GetGenericArguments()[0])
                    .Invoke(null, new object[] { Controller, (Action<object>)Record });
                var legacyEvent = Controller.GetType().GetEvent("SoundCued");
                typeof(TenPullScoreTests).GetMethod(nameof(ObserveLegacy), BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(legacyEvent.EventHandlerType.GetGenericArguments()[0])
                    .Invoke(null, new object[] { Controller, (Action)(() => LegacyCues++) });
            }

            private void Record(object value)
            {
                Cues.Add(new Cue { Bus = (string)Get(value, "Bus"), Resource = (string)Get(value, "Resource"),
                    Stop = (bool)Get(value, "Stop"), Loop = (bool)Get(value, "Loop"),
                    Pitch = (float)Get(value, "Pitch"), Volume = (float)Get(value, "Volume"), Phase = Phase,
                    Rank = (int)Get(Property(Sequence, "Current"), "ClassRank"), Elapsed = (float)Property(Sequence, "Elapsed") });
            }

            public void Present(int pattern, int rankOverride = 0, bool summary = false)
            {
                var preview = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                    .GetType("TenPullPreviewWindow", true);
                var results = (Array)preview.GetMethod("CreateResults").Invoke(null, new object[] { pattern });
                if (rankOverride != 0) foreach (var item in results) Set(item, "ClassRank", rankOverride);
                Call(Controller, "Present", results, (Func<string, Sprite>)(id => null), null, null, null, true, summary, Settings);
            }

            public void TickUntil(string phase)
            {
                for (int tick = 0; tick < 1400 && Phase != phase; tick++) Call(Controller, "TickPresentation", 0.05f);
                Assert.That(Phase, Is.EqualTo(phase));
            }

            public void Dispose()
            {
                if (Controller != null) Call(Controller, "OnDestroy");
                EditorSceneManager.ClosePreviewScene(Scene);
                UnityEngine.Object.DestroyImmediate(Settings);
            }
        }

        private static void ObserveScore<T>(Component controller, Action<object> observe)
        {
            Action<T> handler = value => observe(value);
            controller.GetType().GetEvent("ScoreCued").AddEventHandler(controller, handler);
        }

        private static void ObserveLegacy<T>(Component controller, Action observe)
        {
            Action<T, float> handler = (value, volume) => observe();
            controller.GetType().GetEvent("SoundCued").AddEventHandler(controller, handler);
        }

        [TestCase(0, 0, 0)] [TestCase(0, 5, 0)] [TestCase(3, 0, 1)] [TestCase(2, 0, 2)]
        public void ConfirmedClass4ChoosesTheOriginalScoreAndAccentsOnlyItsOwnStones(int pattern, int rankOverride, int class4Count)
        {
            using (var presentation = new Presentation())
            {
                Set(presentation.Settings, "IntroductionSeconds", 4f);
                presentation.Present(pattern, rankOverride);
                Assert.That(Property(presentation.Controller, "UsesOriginalMusic"), Is.True,
                    "All seven shipped score clips must import and load before the score can replace legacy cues.");
                var intro = presentation.Cues.Single(c => !c.Stop);
                string prefix = (string)Get(presentation.Music, "ResourcePrefix");
                Assert.That(intro.Resource, Is.EqualTo(prefix + Get(presentation.Music, class4Count > 0 ? "Class4Intro" : "NormalIntro")));
                Assert.That(intro.Phase, Is.EqualTo("Introduction"));
                Assert.That(intro.Elapsed, Is.Zero, "The score must begin with the button's first presentation frame.");
                Assert.That(intro.Pitch, Is.EqualTo(0.8f).Within(0.0001f), "The composed 3.2-second intro follows the configured four-second animation.");
                Assert.That(intro.Volume, Is.GreaterThan(0f));
                presentation.TickUntil("Materialization");
                var bed = presentation.Cues.Single(c => !c.Stop && c.Bus == "Bed");
                Assert.That(bed.Resource, Is.EqualTo(prefix + Get(presentation.Music, "RevealLoop")));
                Assert.That(bed.Loop, Is.True, "Manual stone opening must never run out of music.");
                Assert.That(bed.Phase, Is.EqualTo("Materialization"));
                presentation.TickUntil("Summary");
                var starts = presentation.Cues.Where(c => !c.Stop).ToArray();
                var accents = starts.Where(c => c.Resource == prefix + Get(presentation.Music, "Class4Accent")).ToArray();
                Assert.That(accents.Length, Is.EqualTo(class4Count));
                Assert.That(accents.All(c => c.Rank == 4 && c.Phase == "CutIn" && c.Elapsed >= (float)Get(presentation.Settings, "Class4AnticipationSeconds")), Is.True);
                Assert.That(starts.Count(c => c.Resource == prefix + Get(presentation.Music, "ArrivalTick")), Is.EqualTo(10));
                Assert.That(starts.Count(c => c.Resource == prefix + Get(presentation.Music, "StoneOpen")), Is.EqualTo(10));
                Assert.That(starts.Count(c => c.Resource == prefix + Get(presentation.Music, "ResultCadence")), Is.EqualTo(1));
                Assert.That(presentation.LegacyCues, Is.Zero, "The old unrelated start/reveal jingles must not overlap the new composition.");
                for (int tick = 0; tick < 100; tick++) Call(presentation.Controller, "TickPresentation", 0.05f);
                Assert.That(Property(presentation.Controller, "IsScorePlaying"), Is.False, "The final cadence ends without an orphaned loop.");
            }
        }

        [Test]
        public void DisablingVisualCutInKeepsExactlyOneClass4MusicAccentAtReveal()
        {
            using (var presentation = new Presentation())
            {
                Set(presentation.Settings, "Class4CutInEnabled", false);
                presentation.Present(4);
                presentation.TickUntil("Summary");
                var accents = presentation.Cues.Where(c => !c.Stop && c.Resource.EndsWith("/class4_accent", StringComparison.Ordinal)).ToArray();
                Assert.That(accents.Length, Is.EqualTo(1));
                Assert.That(accents[0].Rank, Is.EqualTo(4));
                Assert.That(accents[0].Phase, Is.EqualTo("Reveal"));
            }
        }

        [TestCase("Skip")] [TestCase("Interrupt")] [TestCase("Disable")]
        [TestCase("Destroy")] [TestCase("RePresent")] [TestCase("Recovery")] [TestCase("NaturalSummary")]
        public void PresentationExitStopsEveryScoreBusAndReleasesItsAmbientSuppression(string exit)
        {
            using (var presentation = new Presentation())
            {
                var managerObject = new GameObject("ScopedMusicManager"); managerObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(managerObject, presentation.Scene);
                var manager = managerObject.AddComponent(GameType("Managers.AudioManager"));
                presentation.Present(4);
                var previousScore = Get(presentation.Controller, "score");
                // EditMode does not enter the Application.isPlaying acquisition branch. Attach a real
                // manager lease here so all exit paths still prove ownership and restoration behavior.
                Set(previousScore, "ambientSuppression", Call(manager, "SuppressBgm"));
                presentation.TickUntil("CutIn");
                Call(presentation.Controller, "TickPresentation", 0.1f);
                Call(presentation.Controller, "TickPresentation", 0.1f);
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.True);
                Assert.That(Property(previousScore, "IsPlaying"), Is.True);
                presentation.Cues.Clear();
                switch (exit)
                {
                    case "Skip": for (int tap = 0; tap < 100; tap++) Call(presentation.Controller, "Skip"); break;
                    case "Interrupt": Call(presentation.Controller, "Interrupt"); break;
                    // Unity dispatches these callbacks in play mode; invoke them explicitly in EditMode.
                    case "Disable": presentation.Panel.SetActive(false); Call(presentation.Controller, "OnDisable"); break;
                    case "Destroy": Call(presentation.Controller, "OnDestroy"); break;
                    case "RePresent": presentation.Present(0); break;
                    case "Recovery": presentation.Present(4, summary: true); break;
                    case "NaturalSummary":
                        presentation.TickUntil("Summary");
                        Assert.That(Property(manager, "IsBgmSuppressed"), Is.True,
                            "Ambient music stays suppressed until the result cadence finishes.");
                        for (int tick = 0; tick < 100; tick++) Call(presentation.Controller, "TickPresentation", 0.05f);
                        break;
                }
                Assert.That(Property(previousScore, "IsPlaying"), Is.False);
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.False);
                Assert.That(presentation.Cues.Any(c => c.Stop && c.Bus == "Bed"), Is.True);
                if (exit != "NaturalSummary") Assert.That(presentation.Cues.Any(c => c.Stop && c.Bus == "Accent"), Is.True);
                Assert.That(presentation.Cues.Count(c => !c.Stop && c.Resource.EndsWith("/result_cadence", StringComparison.Ordinal)),
                    Is.EqualTo(exit == "NaturalSummary" ? 1 : 0),
                    "Only a naturally completed reveal sequence plays the result cadence.");
                if (exit == "RePresent")
                {
                    Assert.That(presentation.Panel.GetComponentsInChildren<AudioSource>(true).Count(s => s.transform.parent.name == "TenPullScore"), Is.EqualTo(4));
                    Assert.That(presentation.Cues.Count(c => !c.Stop), Is.EqualTo(1), "Re-presentation starts one fresh intro, without old buses.");
                }
                else Assert.That(Property(presentation.Controller, "IsScorePlaying"), Is.False);
            }
        }

        [Test]
        public void MissingScoreAssetFallsBackAtomicallyWithoutCreatingSilentMusicBuses()
        {
            using (var presentation = new Presentation())
            {
                Set(presentation.Music, "Class4Accent", "absent_test_clip");
                presentation.Present(0);
                Assert.That(Property(presentation.Controller, "UsesOriginalMusic"), Is.False);
                Assert.That(presentation.Cues, Is.Empty);
                Assert.That(presentation.LegacyCues, Is.EqualTo(1));
                Assert.That(presentation.Panel.transform.Find("TenPullScore"), Is.Null);
            }
        }

        [Test]
        public void NestedAmbientSuppressionPreservesSavedVolumeFadesStemsAndPriorMutes()
        {
            bool hadBgm = PlayerPrefs.HasKey(BgmKey), hadSe = PlayerPrefs.HasKey(SeKey);
            float oldBgm = PlayerPrefs.GetFloat(BgmKey), oldSe = PlayerPrefs.GetFloat(SeKey);
            var owner = new GameObject("AmbientSuppressionTest"); owner.SetActive(false);
            IDisposable outer = null, inner = null;
            try
            {
                var manager = owner.AddComponent(GameType("Managers.AudioManager"));
                Call(manager, "EnsureAudioSources");
                Set(manager, "bgmVolume", 0.43f);
                PlayerPrefs.SetFloat(BgmKey, 0.43f); PlayerPrefs.SetFloat(SeKey, 0.67f);
                var sources = owner.GetComponentsInChildren<AudioSource>(true).Where(s => s.name != "SeSource").ToArray();
                Assert.That(sources.Length, Is.EqualTo(6), "Both crossfade sources and all four adaptive music stems need suppression.");
                for (int i = 0; i < sources.Length; i++) { sources[i].volume = 0.05f + i * 0.07f; sources[i].mute = i == 2; }
                var volumes = sources.Select(s => s.volume).ToArray();
                var mutes = sources.Select(s => s.mute).ToArray();
                var se = owner.GetComponentsInChildren<AudioSource>(true).Single(s => s.name == "SeSource");
                se.mute = false;
                Set(manager, "incomingBgmSource", sources[0]); Set(manager, "outgoingBgmSource", sources[1]);
                Set(manager, "bgmFadeElapsed", 0.3f); Set(manager, "bgmFadeDuration", 1f);
                outer = (IDisposable)Call(manager, "SuppressBgm");
                inner = (IDisposable)Call(manager, "SuppressBgm");
                Assert.That(sources.All(s => s.mute), Is.True);
                Assert.That(se.mute, Is.False, "Only ambient music is suppressed; score and UI effects retain their own volumes.");
                CollectionAssert.AreEqual(volumes, sources.Select(s => s.volume));
                Assert.That(Get(manager, "incomingBgmSource"), Is.SameAs(sources[0]));
                Assert.That(Get(manager, "outgoingBgmSource"), Is.SameAs(sources[1]));
                Assert.That(Get(manager, "bgmFadeElapsed"), Is.EqualTo(0.3f));
                outer.Dispose(); outer.Dispose();
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.True, "One owner cannot unmute another presentation's music scope.");
                Assert.That(sources.All(s => s.mute), Is.True);
                inner.Dispose();
                CollectionAssert.AreEqual(mutes, sources.Select(s => s.mute));
                CollectionAssert.AreEqual(volumes, sources.Select(s => s.volume));
                Assert.That(Property(manager, "BgmVolume"), Is.EqualTo(0.43f));
                Assert.That(PlayerPrefs.GetFloat(BgmKey), Is.EqualTo(0.43f));
                Assert.That(PlayerPrefs.GetFloat(SeKey), Is.EqualTo(0.67f));
                Assert.That(Property(manager, "IsBgmSuppressed"), Is.False);
            }
            finally
            {
                outer?.Dispose(); inner?.Dispose(); UnityEngine.Object.DestroyImmediate(owner);
                if (hadBgm) PlayerPrefs.SetFloat(BgmKey, oldBgm); else PlayerPrefs.DeleteKey(BgmKey);
                if (hadSe) PlayerPrefs.SetFloat(SeKey, oldSe); else PlayerPrefs.DeleteKey(SeKey);
                PlayerPrefs.Save();
            }
        }
    }
}
