using System;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Home
{
    [Serializable]
    public sealed class SummonMusicPresentation
    {
        public bool Enabled = true;
        public string ResourcePrefix = "Audio/Music/TenPull/";
        public string NormalIntro = "intro_normal";
        public string Class4Intro = "intro_class4";
        public string RevealLoop = "reveal_loop";
        public string Class4Accent = "class4_accent";
        public string StoneOpen = "stone_open";
        public string ArrivalTick = "arrival_tick";
        public string ResultCadence = "result_cadence";
        [Min(0.1f)] public float IntroScoredSeconds = 3.2f;
        [Range(0f, 1f)] public float MasterVolume = 1f;
        [Range(0f, 1f)] public float IntroVolume = 1f;
        [Range(0f, 1f)] public float RevealVolume = 0.72f;
        [Range(0f, 1f)] public float Class4Volume = 0.9f;
        [Range(0f, 1f)] public float StoneVolume = 0.55f;
        [Range(0f, 1f)] public float ArrivalVolume = 0.28f;
        [Range(0f, 1f)] public float ResultVolume = 0.8f;
        [Range(0f, 0.8f)] public float RevealCrossfadeSeconds = 0.25f;
    }

    [Serializable]
    public sealed class SummonFilmRadiancePresentation
    {
        public bool Enabled = true;
        public string ResourcePrefix = "UI/GachaPage/TenPull/Anime/Radiance/";
        [Range(0f, 1.5f)] public float Intensity = 1f;
        [Range(0f, 1f)] public float BacklightOpacity = 0.72f;
        [Range(0f, 1f)] public float ForegroundOpacity = 0.80f;
        [Range(0.5f, 1.5f)] public float CoreScale = 1f;
        [Range(0.25f, 0.7f)] public float GoldIgnitionAt = 0.42f;
        [Range(0.08f, 0.4f)] public float ReleaseAt = 0.18f;
        [Range(0f, 0.4f)] public float LightningTurns = 0.18f;
    }

    [Serializable]
    public sealed class SummonIntroductionPresentation
    {
        public Color Color = new Color(0.42f, 0.72f, 1f, 1f);
        [Range(0f, 1f)] public float GlowOpacity = 0.52f;
        [Range(0, 32)] public int ParticleCount = 12;
        [Range(0f, 1f)] public float FlashOpacity = 0.08f;
        [Range(0f, 15f)] public float ShakePixels = 1.5f;
        public AudioCue ResonanceCue = AudioCue.GachaStart;
        [Range(0f, 1f)] public float SoundVolume = 0.55f;
    }

    [Serializable]
    public sealed class SummonRarityPresentation
    {
        public string StoneResourcePrefix;
        // Registration points in normalized, bottom-left texture coordinates.
        // The image2 cells have equal dimensions, but their artwork is not identically centered.
        public Vector2 IntactCenter = new Vector2(411f / 724f, 367.5f / 724f);
        public Vector2 CrackedCenter = new Vector2(362f / 724f, 367.5f / 724f);
        public Vector2 ShatteredCenter = new Vector2(343f / 724f, 382f / 724f);
        public Vector2 CenterForState(string state) => state == "_Cracked" ? CrackedCenter : state == "_Shattered" ? ShatteredCenter : IntactCenter;
        public Color Accent;
        [Min(0.1f)] public float FocusSeconds = 0.24f;
        [Min(0.1f)] public float CrackSeconds = 0.22f;
        [Min(0.1f)] public float BurstSeconds = 0.20f;
        [Min(0.2f)] public float RevealSeconds = 0.65f;
        [Range(0f, 1.5f)] public float CutInSeconds;
        [Range(0f, 0.1f)] public float IdlePulse = 0.015f;
        [Range(0, 20)] public int SparkCount = 3;
        [Range(0f, 1f)] public float FlashOpacity = 0.12f;
        public AudioCue CrackCue = AudioCue.GachaStart;
        public AudioCue RevealCue = AudioCue.GachaReveal;
        [Range(0f, 1f)] public float SoundVolume = 0.65f;
        public Color StoneTint = Color.white;
        [Range(0f, 1f)] public float GlowOpacity = 0.18f;
        [Range(0f, 15f)] public float ShakePixels = 0f;
    }

    [CreateAssetMenu(menuName = "WitchTower/Ten Pull Presentation Settings")]
    public sealed class TenPullPresentationSettings : ScriptableObject
    {
        public const string ResourcePath = "UI/GachaPage/TenPull/TenPullPresentationSettings";
        public string BackgroundResource = "UI/GachaPage/GachaSummonChamberBackground";
        public string CutInResource = "UI/GachaPage/TenPull/RiftCutIn";
        public string ButtonResource = "UI/FusionPage/FusionSmallButton";
        public string SealResource = "UI/GachaPage/TenPull/ConvergenceSeal";
        public string ParticleResource = "UI/GachaPage/Effects/GachaSparkle_0";
        public string VortexResourcePrefix = "UI/GachaPage/TenPull/Vortex_";
        public string StreakResource = "UI/GachaPage/TenPull/CometStreak";
        public bool AnimeEnabled = true;
        public string AnimeFilmLayoutResource = "UI/GachaPage/TenPull/Anime/Continuous/Layout";
        public string AnimeOpeningPrefix = "UI/GachaPage/TenPull/Anime/Opening_";
        public string AnimeNormalPrefix = "UI/GachaPage/TenPull/Anime/Normal_";
        public string AnimeUpperPrefix = "UI/GachaPage/TenPull/Anime/Upper_";
        [Range(0.25f, 0.6f)] public float AnimeOpeningFraction = 0.42f;
        [Range(0f, 0.12f)] public float AnimeCameraPush = 0.045f;
        [Range(0.05f, 0.4f)] public float AnimeExitFadeSeconds = 0.18f;
        [Range(0.2f, 0.6f)] public float AnimeEyeInsertSeconds = 0.38f;
        public SummonFilmRadiancePresentation AnimeRadiance = new SummonFilmRadiancePresentation();
        public SummonMusicPresentation Music = new SummonMusicPresentation();
        [Range(1f, 5f)] public float IntroductionSeconds = 3.2f;
        [Range(1f, 5f)] public float SingleIntroductionSeconds = 3.2f;
        [Range(0f, 0.2f)] public float CameraPush = 0.12f;
        [Range(0.1f, 1f)] public float PillarOpacity = 0.85f;
        [Range(0f, 1f)] public float TrailOpacity = 0.85f;
        [Range(0f, 0.8f)] public float ReleaseFlashOpacity = 0.42f;
        [Range(0.4f, 1f)] public float StoneOrbitTurns = 0.65f;
        [Range(0.1f, 0.4f)] public float Class4AnticipationSeconds = 0.16f;
        [Range(0.1f, 1f)] public float RevealShockwaveOpacity = 0.85f;
        [Range(0.05f, 0.5f)] public float Class4RevealPunch = 0.22f;
        [Range(0.03f, 0.2f)] public float StoneArrivalInterval = 0.09f;
        [Range(0.15f, 0.6f)] public float StoneArrivalSeconds = 0.40f;
        [Range(0.1f, 1f)] public float StonesHoldSeconds = 0.48f;
        [Range(0f, 1f)] public float ResonanceAt = 0.48f;
        public AudioCue StartCue = AudioCue.GachaStart;
        public AudioCue ArrivalCue = AudioCue.GachaReveal;
        public bool Class4CutInEnabled = true;
        [Range(1f, 1.3f)] public float Class4StoneEmphasis = 1.12f;
        public SummonIntroductionPresentation NormalIntroduction = new SummonIntroductionPresentation();
        public SummonIntroductionPresentation UpperIntroduction = new SummonIntroductionPresentation
        {
            Color = new Color(1f, 0.78f, 0.32f, 1f), GlowOpacity = 0.95f,
            ParticleCount = 26, FlashOpacity = 0.22f, ShakePixels = 6f,
            ResonanceCue = AudioCue.GachaRareReveal, SoundVolume = 0.9f
        };
        public float MaterializationDuration => GetMaterializationDuration(10);
        public float GetMaterializationDuration(int count) => Math.Max(0.03f, StoneArrivalInterval) * Math.Max(0, count - 1)
            + Math.Max(0.15f, StoneArrivalSeconds) + Math.Max(0.1f, StonesHoldSeconds);
        [Min(0.1f)] public float GridTransitionSeconds = 0.38f;
        public bool AutoPlay = true;
        public SummonRarityPresentation Normal = new SummonRarityPresentation
        {
            StoneResourcePrefix = "UI/GachaPage/TenPull/Normal",
            Accent = new Color(0.55f, 0.80f, 1f, 1f),
            StoneTint = new Color(0.70f, 0.78f, 0.90f, 1f)
        };
        public SummonRarityPresentation Rare = new SummonRarityPresentation
        {
            StoneResourcePrefix = "UI/GachaPage/TenPull/Rare",
            IntactCenter = new Vector2(334.5f / 724f, 376.5f / 724f),
            CrackedCenter = new Vector2(348f / 724f, 376.5f / 724f),
            ShatteredCenter = new Vector2(369f / 724f, 382f / 724f),
            Accent = new Color(1f, 0.78f, 0.28f, 1f),
            FocusSeconds = 0.42f, CrackSeconds = 0.32f, BurstSeconds = 0.26f,
            RevealSeconds = 0.85f, CutInSeconds = 0f, IdlePulse = 0.033f,
            SparkCount = 9, FlashOpacity = 0.20f, RevealCue = AudioCue.GachaRareReveal, SoundVolume = 0.85f
        };
        public SummonRarityPresentation Legendary = new SummonRarityPresentation
        {
            StoneResourcePrefix = "UI/GachaPage/TenPull/Legendary",
            IntactCenter = new Vector2(378f / 724f, 388f / 724f),
            CrackedCenter = new Vector2(374f / 724f, 388f / 724f),
            ShatteredCenter = new Vector2(361f / 724f, 412f / 724f),
            Accent = new Color(0.86f, 0.62f, 1f, 1f),
            FocusSeconds = 0.72f, CrackSeconds = 0.38f, BurstSeconds = 0.28f,
            RevealSeconds = 1.10f, CutInSeconds = 0.8f, IdlePulse = 0.048f,
            GlowOpacity = 0.82f, ShakePixels = 7f,
            SparkCount = 16, FlashOpacity = 0.27f, RevealCue = AudioCue.GachaLegendaryReveal, SoundVolume = 1f
        };

        public SummonRarityPresentation ForClass(int rank) => rank >= 4 ? Legendary : rank >= 3 ? Rare : Normal;
        public SummonRarityPresentation ForSealedClass(int rank) => rank == 4 ? Legendary : Normal;
        public bool HasCutIn(int rank) => rank == 4 && Class4CutInEnabled && Legendary.CutInSeconds > 0f;
    }
}
