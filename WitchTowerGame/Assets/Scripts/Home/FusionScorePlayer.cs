using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Managers;

namespace WitchTower.Home
{
    /// <summary>
    /// Original stereo score for the two-parent ritual. Tick receives absolute
    /// film time; the score owns no fusion results, currency or saved settings.
    /// </summary>
    public sealed class FusionScorePlayer : IDisposable
    {
        private const string ResourceRoot = "Audio/Music/Fusion/";
        private const float NormalScoreSeconds = 7f;
        private const float UpperScoreSeconds = 9f;
        private const float MusicGain = 0.90f;
        private const float EffectsGain = 0.72f;
        private readonly AudioClip normalMusic;
        private readonly AudioClip normalEffects;
        private readonly AudioClip upperMusic;
        private readonly AudioClip upperEffects;
        private GameObject audioRoot;
        private AudioSource musicSource;
        private AudioSource effectsSource;
        private IDisposable ambientSuppression;
        private float duration;
        private float pitch = 1f;
        private int sceneHandle;
        private bool playing;
        private bool disposed;

        public bool IsAvailable { get; }
        public bool IsPlaying => playing;

        public FusionScorePlayer()
        {
            normalMusic = Resources.Load<AudioClip>(ResourceRoot + "ritual_normal_music");
            normalEffects = Resources.Load<AudioClip>(ResourceRoot + "ritual_normal_effects");
            upperMusic = Resources.Load<AudioClip>(ResourceRoot + "ritual_class4_music");
            upperEffects = Resources.Load<AudioClip>(ResourceRoot + "ritual_class4_effects");
            IsAvailable = Valid(normalMusic) && Valid(normalEffects) && Valid(upperMusic) && Valid(upperEffects);
        }

        public void Begin(bool upper, float durationSeconds)
        {
            if (disposed) return;
            Stop();
            if (!IsAvailable) return;
            float scoredSeconds = upper ? UpperScoreSeconds : NormalScoreSeconds;
            duration = float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)
                ? scoredSeconds : Mathf.Clamp(durationSeconds, 3f, 30f);
            pitch = scoredSeconds / duration;
            EnsureSources();
            Configure(musicSource, upper ? upperMusic : normalMusic);
            Configure(effectsSource, upper ? upperEffects : normalEffects);
            UpdateVolumes();
            playing = true;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            if (!Application.isPlaying) return;

            ambientSuppression = AudioManager.Instance?.SuppressBgm();
            // The same DSP start keeps both stems aligned independently of frame rate.
            double start = AudioSettings.dspTime + 0.025;
            musicSource.PlayScheduled(start);
            effectsSource.PlayScheduled(start);
        }

        public void Tick(float elapsed)
        {
            if (!playing || disposed || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
            if (elapsed >= duration) { Stop(); return; }
            UpdateVolumes();
            if (!Application.isPlaying || AudioListener.pause || elapsed < 0f) return;
            // Ordinary frames use the audio clock. A seek only repairs a large
            // hitch/resume; per-frame seeks cause clicks and unstable playback.
            Synchronize(musicSource, elapsed);
            Synchronize(effectsSource, elapsed);
        }

        public void Stop()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            StopSource(musicSource);
            StopSource(effectsSource);
            ambientSuppression?.Dispose();
            ambientSuppression = null;
            playing = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            if (audioRoot != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(audioRoot);
                else UnityEngine.Object.DestroyImmediate(audioRoot);
            }
            audioRoot = null;
            musicSource = null;
            effectsSource = null;
        }

        private void EnsureSources()
        {
            if (audioRoot != null) return;
            audioRoot = new GameObject("FusionRitualScore");
            sceneHandle = audioRoot.scene.handle;
            musicSource = CreateSource("FusionMusic");
            effectsSource = CreateSource("FusionEffects");
        }

        private AudioSource CreateSource(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(audioRoot.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            source.ignoreListenerPause = false;
            return source;
        }

        private void Configure(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.pitch = pitch;
            source.time = 0f;
        }

        private void UpdateVolumes()
        {
            AudioManager manager = AudioManager.Instance;
            if (musicSource != null)
                musicSource.volume = MusicGain * (manager != null ? manager.BgmVolume : 0.58f);
            if (effectsSource != null)
                effectsSource.volume = EffectsGain * (manager != null ? manager.SeVolume : 0.76f);
        }

        private void Synchronize(AudioSource source, float elapsed)
        {
            if (source == null || source.clip == null || !source.isPlaying) return;
            float expected = Mathf.Clamp(elapsed * pitch, 0f, source.clip.length - 0.01f);
            if (Mathf.Abs(source.time - expected) > 0.35f * pitch) source.time = expected;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.handle == sceneHandle) Stop();
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }

        private static bool Valid(AudioClip clip) => clip != null && clip.length > 0f;
    }
}
