using System;
using System.Collections.Generic;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Home
{
    /// <summary>The preview records the same bus operations that drive live audio.</summary>
    [Serializable]
    public sealed class TenPullScoreEvent
    {
        public string Bus;
        public string Resource;
        public float Volume;
        public float Pitch = 1f;
        public bool Loop;
        public bool Stop;
        public float FadeSeconds;
    }

    /// <summary>Owns only presentation music; never draws, grants, or changes saved sound settings.</summary>
    public sealed class TenPullScorePlayer : IDisposable
    {
        private sealed class Channel
        {
            public string Name;
            public AudioSource Source;
            public AudioClip Clip;
            public string Resource;
            public bool Active, Loop, Stopping;
            public float Gain, Pitch = 1f, Elapsed, Envelope = 1f;
            public float FadeFrom, FadeTo, FadeElapsed, FadeDuration;
        }

        private readonly SummonMusicPresentation settings;
        private readonly bool preview;
        private readonly Action<TenPullScoreEvent> cue;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        private GameObject audioRoot;
        private IDisposable ambientSuppression;
        private bool finishing, running, disposed;
        public bool IsAvailable { get; }
        public bool IsPlaying => running;

        public TenPullScorePlayer(GameObject owner, SummonMusicPresentation configuration, bool isPreview,
            Action<TenPullScoreEvent> onCue)
        {
            settings = configuration;
            preview = isPreview;
            cue = onCue;
            if (settings == null || !settings.Enabled) return;
            string[] names = { settings.NormalIntro, settings.Class4Intro, settings.RevealLoop,
                settings.Class4Accent, settings.StoneOpen, settings.ArrivalTick, settings.ResultCadence };
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name)) return;
                string resource = settings.ResourcePrefix + name;
                AudioClip clip = Resources.Load<AudioClip>(resource);
                if (clip == null || clip.length <= 0f) return;
                clips[resource] = clip;
            }
            audioRoot = new GameObject("TenPullScore");
            audioRoot.transform.SetParent(owner.transform, false);
            foreach (string bus in new[] { "Intro", "Bed", "Accent", "Stone" })
            {
                var child = new GameObject(bus);
                child.transform.SetParent(audioRoot.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = false;
                channels.Add(bus, new Channel { Name = bus, Source = source });
            }
            IsAvailable = true;
        }

        public void Begin(bool hasClass4, float introductionSeconds)
        {
            if (!IsAvailable || disposed) return;
            Stop();
            running = true;
            if (Application.isPlaying && !preview)
                ambientSuppression = AudioManager.Instance?.SuppressBgm();
            Start("Intro", hasClass4 ? settings.Class4Intro : settings.NormalIntro,
                settings.IntroVolume, false,
                Mathf.Clamp(settings.IntroScoredSeconds / Mathf.Max(0.1f, introductionSeconds), 0.1f, 3f));
        }

        public void BeginReveal()
        {
            if (!running) return;
            StopChannel(channels["Intro"], settings.RevealCrossfadeSeconds);
            Start("Bed", settings.RevealLoop, settings.RevealVolume, true, 1f, settings.RevealCrossfadeSeconds);
        }

        public void OpenStone() { if (running) Start("Stone", settings.StoneOpen, settings.StoneVolume); }
        public void ArriveStone() { if (running) Start("Stone", settings.ArrivalTick, settings.ArrivalVolume); }
        public void RevealClass4() { if (running) Start("Accent", settings.Class4Accent, settings.Class4Volume); }

        public void Complete()
        {
            if (!running || finishing) return;
            finishing = true;
            StopChannel(channels["Intro"], 0.12f);
            StopChannel(channels["Bed"], 0.12f);
            StopChannel(channels["Stone"], 0f);
            Start("Accent", settings.ResultCadence, settings.ResultVolume);
        }

        public void Tick(float delta)
        {
            if (!running || disposed || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float step = Mathf.Clamp(delta, 0f, 0.1f);
            bool anyActive = false;
            foreach (Channel channel in channels.Values)
            {
                if (!channel.Active) continue;
                channel.Elapsed += step * channel.Pitch;
                if (channel.FadeDuration > 0f)
                {
                    channel.FadeElapsed += step;
                    channel.Envelope = Mathf.Lerp(channel.FadeFrom, channel.FadeTo,
                        Mathf.Clamp01(channel.FadeElapsed / channel.FadeDuration));
                    if (channel.FadeElapsed >= channel.FadeDuration)
                    {
                        channel.FadeDuration = 0f;
                        if (channel.Stopping) { EndChannel(channel); continue; }
                    }
                }
                if (!channel.Loop && channel.Elapsed >= channel.Clip.length)
                {
                    EndChannel(channel);
                    continue;
                }
                if (channel.Source != null) channel.Source.volume = EffectiveVolume(channel) * channel.Envelope;
                anyActive = true;
            }
            if (finishing && !anyActive) Stop();
        }

        public void Stop()
        {
            foreach (Channel channel in channels.Values) StopChannel(channel, 0f);
            ambientSuppression?.Dispose();
            ambientSuppression = null;
            running = false;
            finishing = false;
        }

        private void Start(string bus, string name, float gain, bool loop = false, float pitch = 1f, float fade = 0f)
        {
            Channel channel = channels[bus];
            StopChannel(channel, 0f);
            channel.Resource = settings.ResourcePrefix + name;
            channel.Clip = clips[channel.Resource];
            channel.Gain = Mathf.Clamp01(gain) * Mathf.Clamp01(settings.MasterVolume);
            channel.Pitch = pitch;
            channel.Loop = loop;
            channel.Active = true;
            channel.Stopping = false;
            channel.Elapsed = 0f;
            channel.Envelope = fade > 0f ? 0f : 1f;
            channel.FadeFrom = channel.Envelope;
            channel.FadeTo = 1f;
            channel.FadeElapsed = 0f;
            channel.FadeDuration = Mathf.Max(0f, fade);
            cue?.Invoke(new TenPullScoreEvent { Bus = bus, Resource = channel.Resource,
                Volume = EffectiveVolume(channel), Pitch = pitch, Loop = loop, FadeSeconds = channel.FadeDuration });
            if (Application.isPlaying && channel.Source != null)
            {
                channel.Source.clip = channel.Clip;
                channel.Source.loop = loop;
                channel.Source.pitch = pitch;
                channel.Source.volume = EffectiveVolume(channel) * channel.Envelope;
                channel.Source.Play();
            }
        }

        private float EffectiveVolume(Channel channel)
        {
            AudioManager manager = preview ? null : AudioManager.Instance;
            float userVolume = channel.Name == "Stone" ? manager != null ? manager.SeVolume : 0.76f
                : manager != null ? manager.BgmVolume : 0.58f;
            return channel.Gain * userVolume;
        }

        private void StopChannel(Channel channel, float fade)
        {
            if (!channel.Active) return;
            cue?.Invoke(new TenPullScoreEvent { Bus = channel.Name, Resource = channel.Resource,
                Stop = true, FadeSeconds = Mathf.Max(0f, fade) });
            if (fade <= 0f) { EndChannel(channel); return; }
            channel.Stopping = true;
            channel.FadeFrom = channel.Envelope;
            channel.FadeTo = 0f;
            channel.FadeElapsed = 0f;
            channel.FadeDuration = fade;
        }

        private static void EndChannel(Channel channel)
        {
            if (channel.Source != null) { channel.Source.Stop(); channel.Source.clip = null; }
            channel.Active = false;
            channel.Stopping = false;
            channel.FadeDuration = 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            if (audioRoot == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(audioRoot);
            else UnityEngine.Object.DestroyImmediate(audioRoot);
            audioRoot = null;
        }
    }
}
