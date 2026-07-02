using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.Managers
{
    public enum AudioCue
    {
        UiClick,
        UiConfirm,
        UiCancel,
        Skill,
        Hit,
        EnemyDefeat,
        AllyDefeat,
        BattleStart,
        Victory,
        Defeat,
        Reward,
        LevelUp,
        Error,
        EquipmentDrop,
        MissionComplete,
        DailyReward,
        GachaStart,
        GachaReveal,
        GachaRareReveal,
        GachaLegendaryReveal,
        FusionStart,
        Fusion,
        FusionSuccess,
        UpgradeSuccess,
        UpgradeFail,
        UpgradeBreak
    }

    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float ButtonScanInterval = 0.5f;
        private const float DefaultBgmFadeSeconds = 1.0f;
        private const string BgmResourceRoot = "Audio/BGM/";
        private const string BgmVolumePrefsKey = "witchtower_audio_bgm_volume";
        private const string SeVolumePrefsKey = "witchtower_audio_se_volume";
        private const string HapticsEnabledPrefsKey = "witchtower_audio_haptics_enabled";

        private static readonly string[] StemSuffixes = { "base", "rhythm", "melody", "tension" };
        private static readonly float[] StemBaseVolumes = { 0.84f, 0.52f, 0.72f, 0.0f };

        public static AudioManager Instance { get; private set; }

        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.58f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 0.76f;
        [SerializeField] private bool hapticsEnabled = true;

        private readonly Dictionary<AudioCue, AudioClip> proceduralSeCache = new Dictionary<AudioCue, AudioClip>();
        private readonly Dictionary<AudioCue, float> lastCueTimes = new Dictionary<AudioCue, float>();
        private readonly Dictionary<string, string> sceneBgmKeys = new Dictionary<string, string>();
        private readonly HashSet<string> missingBgmKeys = new HashSet<string>();
        private readonly List<AudioSource> stemSources = new List<AudioSource>();

        private AudioSource bgmSourceA;
        private AudioSource bgmSourceB;
        private AudioSource incomingBgmSource;
        private AudioSource outgoingBgmSource;
        private AudioSource activeBgmSource;
        private AudioSource seSource;
        private string currentBgmKey = string.Empty;
        private bool playingStemBgm;
        private float bgmFadeElapsed;
        private float bgmFadeDuration;
        private float nextButtonScanTime;
        private float lastHapticTime = -10f;

        public float BgmVolume => bgmVolume;
        public float SeVolume => seVolume;
        public bool HapticsEnabled => hapticsEnabled;
        public string CurrentBgmKey => currentBgmKey;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPersistedAudioSettings();
            CreateAudioSources();
            BuildDefaultSceneBgmMap();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (Application.isPlaying)
            {
                HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            UpdateBgmFade();

            if (!Application.isPlaying || Time.unscaledTime < nextButtonScanTime)
            {
                return;
            }

            nextButtonScanTime = Time.unscaledTime + ButtonScanInterval;
            BindButtonClickEmitters();
        }

        public void PlaySe(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSources();
            seSource.PlayOneShot(clip, seVolume);
        }

        public void PlaySe(AudioCue cue)
        {
            if (!CanPlayCue(cue))
            {
                return;
            }

            PlaySe(GetOrCreateProceduralSe(cue));
            PlayHaptic(cue);
        }

        public void PlaySe(string cueId)
        {
            PlaySe(ResolveCue(cueId));
        }

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSources();
            PlayBgmClip(clip, clip.name, DefaultBgmFadeSeconds);
        }

        public void PlayBgm(string bgmKey)
        {
            PlayBgm(bgmKey, DefaultBgmFadeSeconds);
        }

        public void PlayBgm(string bgmKey, float fadeSeconds)
        {
            if (string.IsNullOrEmpty(bgmKey))
            {
                StopBgm(fadeSeconds);
                return;
            }

            EnsureAudioSources();
            if (TryPlayStemBgm(bgmKey))
            {
                return;
            }

            AudioClip clip = LoadBgmClip(bgmKey);
            if (clip == null)
            {
                if (missingBgmKeys.Add(bgmKey))
                {
                    Debug.Log($"[AudioManager] BGM not found. Add {BgmResourceRoot}{bgmKey}_loop or {BgmResourceRoot}{bgmKey} under Resources when ready.");
                }

                StopBgm(fadeSeconds);
                return;
            }

            PlayBgmClip(clip, bgmKey, fadeSeconds);
        }

        public void StopBgm(float fadeSeconds = DefaultBgmFadeSeconds)
        {
            if (playingStemBgm)
            {
                StopStemSources();
            }

            currentBgmKey = string.Empty;
            if (activeBgmSource == null || !activeBgmSource.isPlaying)
            {
                return;
            }

            incomingBgmSource = null;
            outgoingBgmSource = activeBgmSource;
            activeBgmSource = null;
            bgmFadeElapsed = 0f;
            bgmFadeDuration = Mathf.Max(0.01f, fadeSeconds);
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            PersistAudioSettings();
            ApplyImmediateBgmVolume();
        }

        public void SetSeVolume(float volume)
        {
            seVolume = Mathf.Clamp01(volume);
            PersistAudioSettings();
            if (seSource != null)
            {
                seSource.volume = seVolume;
            }
        }

        public void SetHapticsEnabled(bool enabled)
        {
            hapticsEnabled = enabled;
            PersistAudioSettings();
        }

        public void PlayHaptic(AudioCue cue)
        {
            if (!Application.isPlaying || !hapticsEnabled || !ShouldPlayHaptic(cue))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastHapticTime < ResolveHapticMinimumInterval(cue))
            {
                return;
            }

            lastHapticTime = now;
            TriggerNativeHaptic();
        }

        public void SetBgmIntensity(float intensity)
        {
            if (!playingStemBgm || stemSources.Count < StemSuffixes.Length)
            {
                return;
            }

            float clamped = Mathf.Clamp01(intensity);
            for (int i = 0; i < stemSources.Count; i += 1)
            {
                AudioSource source = stemSources[i];
                if (source == null)
                {
                    continue;
                }

                float layerVolume = StemBaseVolumes[Mathf.Min(i, StemBaseVolumes.Length - 1)];
                if (i == 1)
                {
                    layerVolume *= Mathf.Lerp(0.45f, 1f, clamped);
                }
                else if (i == 3)
                {
                    layerVolume = Mathf.Lerp(0f, 0.86f, clamped);
                }

                source.volume = bgmVolume * layerVolume;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BindButtonClickEmitters();
            if (sceneBgmKeys.TryGetValue(scene.name, out string bgmKey))
            {
                PlayBgm(bgmKey);
            }
        }

        private void CreateAudioSources()
        {
            bgmSourceA = CreateSource("BgmSourceA", true);
            bgmSourceB = CreateSource("BgmSourceB", true);
            activeBgmSource = bgmSourceA;
            seSource = CreateSource("SeSource", false);

            stemSources.Clear();
            for (int i = 0; i < StemSuffixes.Length; i += 1)
            {
                stemSources.Add(CreateSource("BgmStem_" + StemSuffixes[i], true));
            }
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            GameObject sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = loop ? bgmVolume : seVolume;
            return source;
        }

        private void EnsureAudioSources()
        {
            if (bgmSourceA != null && bgmSourceB != null && seSource != null && stemSources.Count == StemSuffixes.Length)
            {
                return;
            }

            CreateAudioSources();
        }

        private void BuildDefaultSceneBgmMap()
        {
            sceneBgmKeys.Clear();
            sceneBgmKeys["TitleScene"] = "home_theme";
            sceneBgmKeys["HomeScene"] = "home_theme";
            sceneBgmKeys["FormationScene"] = "home_theme";
            sceneBgmKeys["EquipmentScene"] = "home_theme";
            sceneBgmKeys["BattleScene"] = "battle_normal";
            sceneBgmKeys["GachaScene"] = "summon_chamber";
            sceneBgmKeys["FusionScene"] = "fusion_ritual";
        }

        private void LoadPersistedAudioSettings()
        {
            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefsKey, bgmVolume));
            seVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SeVolumePrefsKey, seVolume));
            hapticsEnabled = PlayerPrefs.GetInt(HapticsEnabledPrefsKey, hapticsEnabled ? 1 : 0) != 0;
        }

        private void PersistAudioSettings()
        {
            PlayerPrefs.SetFloat(BgmVolumePrefsKey, bgmVolume);
            PlayerPrefs.SetFloat(SeVolumePrefsKey, seVolume);
            PlayerPrefs.SetInt(HapticsEnabledPrefsKey, hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void PlayBgmClip(AudioClip clip, string bgmKey, float fadeSeconds)
        {
            if (clip == null)
            {
                return;
            }

            if (!playingStemBgm && currentBgmKey == bgmKey && activeBgmSource != null && activeBgmSource.clip == clip)
            {
                return;
            }

            StopStemSources();
            AudioSource nextSource = activeBgmSource == bgmSourceA ? bgmSourceB : bgmSourceA;
            AudioSource previousSource = activeBgmSource;

            nextSource.clip = clip;
            nextSource.loop = true;
            nextSource.volume = 0f;
            nextSource.time = 0f;
            nextSource.Play();

            incomingBgmSource = nextSource;
            outgoingBgmSource = previousSource != null && previousSource.isPlaying ? previousSource : null;
            activeBgmSource = nextSource;
            currentBgmKey = bgmKey;
            playingStemBgm = false;
            bgmFadeElapsed = 0f;
            bgmFadeDuration = Mathf.Max(0.01f, fadeSeconds);
        }

        private bool TryPlayStemBgm(string bgmKey)
        {
            AudioClip[] clips = new AudioClip[StemSuffixes.Length];
            bool hasAnyStem = false;
            for (int i = 0; i < StemSuffixes.Length; i += 1)
            {
                clips[i] = Resources.Load<AudioClip>(BgmResourceRoot + bgmKey + "_" + StemSuffixes[i]);
                hasAnyStem |= clips[i] != null;
            }

            if (!hasAnyStem)
            {
                return false;
            }

            if (playingStemBgm && currentBgmKey == bgmKey)
            {
                return true;
            }

            StopSingleBgmSources();
            currentBgmKey = bgmKey;
            playingStemBgm = true;
            for (int i = 0; i < stemSources.Count; i += 1)
            {
                AudioSource source = stemSources[i];
                source.Stop();
                source.clip = clips[i];
                if (clips[i] == null)
                {
                    continue;
                }

                source.loop = true;
                source.volume = bgmVolume * StemBaseVolumes[Mathf.Min(i, StemBaseVolumes.Length - 1)];
                source.time = 0f;
                source.Play();
            }

            return true;
        }

        private AudioClip LoadBgmClip(string bgmKey)
        {
            AudioClip clip = Resources.Load<AudioClip>(BgmResourceRoot + bgmKey + "_loop");
            return clip != null ? clip : Resources.Load<AudioClip>(BgmResourceRoot + bgmKey);
        }

        private void UpdateBgmFade()
        {
            if (incomingBgmSource == null && outgoingBgmSource == null)
            {
                return;
            }

            bgmFadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(bgmFadeElapsed / Mathf.Max(0.01f, bgmFadeDuration));

            if (incomingBgmSource != null)
            {
                incomingBgmSource.volume = bgmVolume * t;
            }

            if (outgoingBgmSource != null)
            {
                outgoingBgmSource.volume = bgmVolume * (1f - t);
            }

            if (t < 1f)
            {
                return;
            }

            if (outgoingBgmSource != null)
            {
                outgoingBgmSource.Stop();
                outgoingBgmSource.clip = null;
            }

            if (incomingBgmSource != null)
            {
                incomingBgmSource.volume = bgmVolume;
            }

            incomingBgmSource = null;
            outgoingBgmSource = null;
        }

        private void ApplyImmediateBgmVolume()
        {
            if (playingStemBgm)
            {
                SetBgmIntensity(0f);
                return;
            }

            if (activeBgmSource != null)
            {
                activeBgmSource.volume = bgmVolume;
            }
        }

        private void StopSingleBgmSources()
        {
            if (bgmSourceA != null)
            {
                bgmSourceA.Stop();
                bgmSourceA.clip = null;
            }

            if (bgmSourceB != null)
            {
                bgmSourceB.Stop();
                bgmSourceB.clip = null;
            }

            incomingBgmSource = null;
            outgoingBgmSource = null;
            activeBgmSource = bgmSourceA;
        }

        private void StopStemSources()
        {
            for (int i = 0; i < stemSources.Count; i += 1)
            {
                if (stemSources[i] == null)
                {
                    continue;
                }

                stemSources[i].Stop();
                stemSources[i].clip = null;
            }

            playingStemBgm = false;
        }

        private void BindButtonClickEmitters()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i += 1)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                ButtonClickSeEmitter emitter = button.GetComponent<ButtonClickSeEmitter>();
                if (emitter == null)
                {
                    emitter = button.gameObject.AddComponent<ButtonClickSeEmitter>();
                }

                emitter.Bind(button);
            }
        }

        private AudioCue ResolveCue(string cueId)
        {
            if (string.IsNullOrEmpty(cueId))
            {
                return AudioCue.UiClick;
            }

            switch (cueId.Trim().ToLowerInvariant())
            {
                case "confirm":
                case "ok":
                case "submit":
                    return AudioCue.UiConfirm;
                case "cancel":
                case "close":
                case "back":
                    return AudioCue.UiCancel;
                case "skill":
                case "cast":
                    return AudioCue.Skill;
                case "battle_start":
                case "battlestart":
                    return AudioCue.BattleStart;
                case "hit":
                case "damage":
                    return AudioCue.Hit;
                case "enemy_defeat":
                case "enemydefeat":
                    return AudioCue.EnemyDefeat;
                case "ally_defeat":
                case "allydefeat":
                    return AudioCue.AllyDefeat;
                case "victory":
                case "win":
                    return AudioCue.Victory;
                case "defeat":
                case "lose":
                    return AudioCue.Defeat;
                case "reward":
                case "item":
                    return AudioCue.Reward;
                case "levelup":
                case "level_up":
                    return AudioCue.LevelUp;
                case "error":
                case "deny":
                case "disabled":
                    return AudioCue.Error;
                case "equipment_drop":
                case "equipmentdrop":
                    return AudioCue.EquipmentDrop;
                case "mission":
                case "mission_complete":
                    return AudioCue.MissionComplete;
                case "daily":
                case "daily_reward":
                    return AudioCue.DailyReward;
                case "gacha_start":
                case "summon_start":
                    return AudioCue.GachaStart;
                case "gacha":
                case "reveal":
                    return AudioCue.GachaReveal;
                case "rare":
                case "gacha_rare":
                    return AudioCue.GachaRareReveal;
                case "legendary":
                case "gacha_legendary":
                    return AudioCue.GachaLegendaryReveal;
                case "fusion_start":
                    return AudioCue.FusionStart;
                case "fusion":
                    return AudioCue.Fusion;
                case "fusion_success":
                    return AudioCue.FusionSuccess;
                case "upgrade_success":
                case "success":
                    return AudioCue.UpgradeSuccess;
                case "upgrade_fail":
                case "fail":
                    return AudioCue.UpgradeFail;
                case "upgrade_break":
                case "break":
                case "destroy":
                    return AudioCue.UpgradeBreak;
                default:
                    return AudioCue.UiClick;
            }
        }

        private bool CanPlayCue(AudioCue cue)
        {
            float interval = ResolveCueMinimumInterval(cue);
            if (interval <= 0f)
            {
                return true;
            }

            float now = Time.unscaledTime;
            if (lastCueTimes.TryGetValue(cue, out float lastTime) && now - lastTime < interval)
            {
                return false;
            }

            lastCueTimes[cue] = now;
            return true;
        }

        private static float ResolveCueMinimumInterval(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.UiClick:
                    return 0.035f;
                case AudioCue.Hit:
                    return 0.055f;
                case AudioCue.EnemyDefeat:
                    return 0.10f;
                case AudioCue.AllyDefeat:
                    return 0.16f;
                case AudioCue.Skill:
                    return 0.12f;
                case AudioCue.Reward:
                case AudioCue.EquipmentDrop:
                    return 0.12f;
                default:
                    return 0f;
            }
        }

        private static bool ShouldPlayHaptic(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.UiConfirm:
                case AudioCue.UiCancel:
                case AudioCue.Error:
                case AudioCue.Skill:
                case AudioCue.BattleStart:
                case AudioCue.Victory:
                case AudioCue.Defeat:
                case AudioCue.LevelUp:
                case AudioCue.MissionComplete:
                case AudioCue.DailyReward:
                case AudioCue.GachaStart:
                case AudioCue.GachaRareReveal:
                case AudioCue.GachaLegendaryReveal:
                case AudioCue.FusionStart:
                case AudioCue.FusionSuccess:
                case AudioCue.UpgradeSuccess:
                case AudioCue.UpgradeFail:
                case AudioCue.UpgradeBreak:
                    return true;
                default:
                    return false;
            }
        }

        private static float ResolveHapticMinimumInterval(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Error:
                    return 0.35f;
                case AudioCue.Skill:
                case AudioCue.GachaStart:
                case AudioCue.FusionStart:
                    return 0.45f;
                case AudioCue.GachaRareReveal:
                case AudioCue.GachaLegendaryReveal:
                case AudioCue.FusionSuccess:
                case AudioCue.Victory:
                case AudioCue.Defeat:
                case AudioCue.LevelUp:
                    return 0.65f;
                default:
                    return 0.25f;
            }
        }

        private static void TriggerNativeHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor)
            {
                Handheld.Vibrate();
            }
#endif
        }

        private AudioClip GetOrCreateProceduralSe(AudioCue cue)
        {
            if (proceduralSeCache.TryGetValue(cue, out AudioClip clip) && clip != null)
            {
                return clip;
            }

            clip = CreateProceduralSe(cue);
            proceduralSeCache[cue] = clip;
            return clip;
        }

        private static AudioClip CreateProceduralSe(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.UiConfirm:
                    return CreateToneClip("SE_Confirm", 0.12f, delegate(float t, int i)
                    {
                        float freq = t < 0.055f ? 660f : 990f;
                        return Sine(freq, t) * Envelope(t, 0.12f, 15f) * 0.36f;
                    });
                case AudioCue.UiCancel:
                    return CreateToneClip("SE_Cancel", 0.11f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(520f, 260f, t / 0.11f);
                        return Sine(freq, t) * Envelope(t, 0.11f, 13f) * 0.30f;
                    });
                case AudioCue.Skill:
                    return CreateToneClip("SE_Skill", 0.22f, delegate(float t, int i)
                    {
                        float sweep = Mathf.Lerp(360f, 1280f, Mathf.Clamp01(t / 0.16f));
                        return (Sine(sweep, t) * 0.45f + Noise(i) * 0.10f) * Envelope(t, 0.22f, 8f) * 0.48f;
                    });
                case AudioCue.Hit:
                    return CreateToneClip("SE_Hit", 0.09f, delegate(float t, int i)
                    {
                        return (Sine(120f, t) * 0.55f + Noise(i) * 0.32f) * Envelope(t, 0.09f, 22f) * 0.52f;
                    });
                case AudioCue.EnemyDefeat:
                    return CreateToneClip("SE_EnemyDefeat", 0.20f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(900f, 220f, t / 0.20f);
                        return (Sine(freq, t) * 0.40f + Noise(i) * 0.08f) * Envelope(t, 0.20f, 9f) * 0.42f;
                    });
                case AudioCue.AllyDefeat:
                    return CreateToneClip("SE_AllyDefeat", 0.26f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(440f, 130f, t / 0.26f);
                        return Sine(freq, t) * Envelope(t, 0.26f, 7f) * 0.42f;
                    });
                case AudioCue.BattleStart:
                    return CreateArpeggioClip("SE_BattleStart", 0.40f, new[] { 196f, 261.63f, 392f }, 0.10f, 0.30f);
                case AudioCue.Victory:
                    return CreateArpeggioClip("SE_Victory", 0.56f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.13f, 0.32f);
                case AudioCue.Defeat:
                    return CreateArpeggioClip("SE_Defeat", 0.50f, new[] { 392f, 329.63f, 261.63f }, 0.15f, 0.30f);
                case AudioCue.Reward:
                    return CreateArpeggioClip("SE_Reward", 0.34f, new[] { 659.25f, 783.99f, 987.77f }, 0.085f, 0.24f);
                case AudioCue.LevelUp:
                    return CreateArpeggioClip("SE_LevelUp", 0.72f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.51f }, 0.105f, 0.30f);
                case AudioCue.Error:
                    return CreateToneClip("SE_Error", 0.16f, delegate(float t, int i)
                    {
                        float freq = t < 0.075f ? 220f : 185f;
                        return (Sine(freq, t) * 0.42f + Noise(i) * 0.08f) * Envelope(t, 0.16f, 12f) * 0.38f;
                    });
                case AudioCue.EquipmentDrop:
                    return CreateArpeggioClip("SE_EquipmentDrop", 0.42f, new[] { 440f, 659.25f, 880f }, 0.105f, 0.26f);
                case AudioCue.MissionComplete:
                    return CreateArpeggioClip("SE_MissionComplete", 0.48f, new[] { 587.33f, 739.99f, 987.77f, 1174.66f }, 0.095f, 0.25f);
                case AudioCue.DailyReward:
                    return CreateArpeggioClip("SE_DailyReward", 0.52f, new[] { 523.25f, 659.25f, 880f, 1174.66f }, 0.10f, 0.24f);
                case AudioCue.GachaStart:
                    return CreateToneClip("SE_GachaStart", 0.44f, delegate(float t, int i)
                    {
                        float sweep = Mathf.Lerp(180f, 760f, Mathf.Clamp01(t / 0.36f));
                        return (Sine(sweep, t) * 0.35f + Sine(sweep * 1.5f, t, 0.7f) * 0.13f) * Envelope(t, 0.44f, 4.6f) * 0.38f;
                    });
                case AudioCue.GachaReveal:
                    return CreateArpeggioClip("SE_GachaReveal", 0.64f, new[] { 587.33f, 739.99f, 880f, 1174.66f }, 0.12f, 0.28f);
                case AudioCue.GachaRareReveal:
                    return CreateArpeggioClip("SE_GachaRareReveal", 0.72f, new[] { 659.25f, 830.61f, 987.77f, 1318.51f }, 0.105f, 0.31f);
                case AudioCue.GachaLegendaryReveal:
                    return CreateArpeggioClip("SE_GachaLegendaryReveal", 0.92f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1567.98f }, 0.115f, 0.34f);
                case AudioCue.FusionStart:
                    return CreateToneClip("SE_FusionStart", 0.46f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(160f, 540f, Mathf.Clamp01(t / 0.34f));
                        return (Sine(freq, t) * 0.34f + Sine(freq * 0.5f, t) * 0.16f) * Envelope(t, 0.46f, 4.8f) * 0.42f;
                    });
                case AudioCue.Fusion:
                    return CreateToneClip("SE_Fusion", 0.48f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(180f, 720f, Mathf.Sin(t * Mathf.PI / 0.48f));
                        return (Sine(freq, t) * 0.38f + Sine(freq * 1.5f, t) * 0.16f) * Envelope(t, 0.48f, 4.5f) * 0.45f;
                    });
                case AudioCue.FusionSuccess:
                    return CreateArpeggioClip("SE_FusionSuccess", 0.78f, new[] { 293.66f, 440f, 587.33f, 880f, 1174.66f }, 0.115f, 0.32f);
                case AudioCue.UpgradeSuccess:
                    return CreateArpeggioClip("SE_UpgradeSuccess", 0.36f, new[] { 660f, 880f, 1320f }, 0.10f, 0.30f);
                case AudioCue.UpgradeFail:
                    return CreateToneClip("SE_UpgradeFail", 0.22f, delegate(float t, int i)
                    {
                        return (Sine(180f, t) * 0.34f + Noise(i) * 0.13f) * Envelope(t, 0.22f, 8f) * 0.38f;
                    });
                case AudioCue.UpgradeBreak:
                    return CreateToneClip("SE_UpgradeBreak", 0.38f, delegate(float t, int i)
                    {
                        float crack = Noise(i) * Mathf.Exp(-t * 9f);
                        float fall = Sine(Mathf.Lerp(360f, 90f, t / 0.38f), t) * 0.26f;
                        return (crack * 0.22f + fall) * Envelope(t, 0.38f, 5.2f) * 0.42f;
                    });
                case AudioCue.UiClick:
                default:
                    return CreateToneClip("SE_Click", 0.055f, delegate(float t, int i)
                    {
                        float freq = Mathf.Lerp(820f, 1180f, t / 0.055f);
                        return Sine(freq, t) * Envelope(t, 0.055f, 30f) * 0.25f;
                    });
            }
        }

        private delegate float SampleBuilder(float time, int sampleIndex);

        private static AudioClip CreateToneClip(string name, float duration, SampleBuilder builder)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i += 1)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(builder(t, i), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArpeggioClip(string name, float duration, float[] notes, float stepDuration, float gain)
        {
            return CreateToneClip(name, duration, delegate(float t, int i)
            {
                int noteIndex = Mathf.Clamp(Mathf.FloorToInt(t / Mathf.Max(0.01f, stepDuration)), 0, notes.Length - 1);
                float localTime = t - noteIndex * stepDuration;
                float note = notes[noteIndex];
                float tone = Sine(note, localTime) * 0.72f + Sine(note * 2f, localTime) * 0.16f;
                return tone * Envelope(localTime, stepDuration, 10f) * Envelope(t, duration, 2.4f) * gain;
            });
        }

        private static float Sine(float frequency, float time)
        {
            return Mathf.Sin(2f * Mathf.PI * frequency * time);
        }

        private static float Sine(float frequency, float time, float phaseRadians)
        {
            return Mathf.Sin(2f * Mathf.PI * frequency * time + phaseRadians);
        }

        private static float Envelope(float time, float duration, float decay)
        {
            float attack = Mathf.Clamp01(time / 0.012f);
            float release = Mathf.Clamp01((duration - time) / Mathf.Max(0.018f, duration * 0.18f));
            return attack * release * Mathf.Exp(-time * decay);
        }

        private static float Noise(int sampleIndex)
        {
            float value = Mathf.Sin(sampleIndex * 12.9898f) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }
    }

    internal sealed class ButtonClickSeEmitter : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Button button;

        public void Bind(Button target)
        {
            button = target;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null && (!button.interactable || !button.gameObject.activeInHierarchy))
            {
                return;
            }

            AudioManager.Instance?.PlaySe(AudioCue.UiClick);
        }
    }
}
