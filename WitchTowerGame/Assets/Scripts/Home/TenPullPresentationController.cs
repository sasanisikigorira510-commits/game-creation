using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    /// <summary>Presentation only: consumes confirmed snapshots, never game currency or inventory.</summary>
    public sealed class TenPullPresentationController : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform Root;
            public Image Frame, Stone, Portrait, Aura, Trail, Landing;
            public CanvasGroup Visibility;
            public Text Number, Name, Detail, Badge;
        }

        private TenPullPresentationSettings settings;
        private TenPullSequence sequence;
        private readonly List<Slot> slots = new List<Slot>(10);
        private readonly List<Image> sparks = new List<Image>(20);
        private readonly List<Image> convergenceParticles = new List<Image>(32);
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private Sprite[] portraits;
        private SummonPresentationResult[] displayResults;
        private RectTransform content, focalRoot, cutInRoot;
        private Image focalStone, focalMonster, glow, rift, cutInPortrait, flash;
        private RectTransform introductionRoot;
        private Image seal, outerSeal, convergenceLight, background;
        private Image cutInSeal, cutInSlash, cutInShade;
        private TenPullCinematicEffects cinematic;
        private TenPullAnimeFilm animeFilm;
        private Text title, subtitle, progress, monsterName, monsterInfo, hint, cutInTitle;
        private Button skipButton, nextButton, autoButton, backButton, homeButton;
        private AudioSource sound;
        private TenPullScorePlayer score;
        private bool suppressScoreCadence;
        private Action finished, back, home;
        private float visualClock;
        private bool isPreview, ownedSettings, built;
        private bool resonancePlayed;
        private int arrivedSoundCount;
        private bool releasePlayed;
        private bool compressionSilent, cutInSoundPlayed;
        private bool frameRateOwned;
        private bool summaryBackAllowed = true, summaryHomeAllowed = true;
        private string summaryGuidance = string.Empty;
        private int previousFrameRate;
        // Preview capture observes the same cue timeline as live playback; it cannot change results.
        public event Action<AudioCue, float> SoundCued;
        public event Action SoundsStopped;
        public event Action<TenPullScoreEvent> ScoreCued;
        public bool UsesOriginalMusic => score != null && score.IsAvailable;
        public bool IsScorePlaying => score != null && score.IsPlaying;
        public bool HasResults => sequence != null;
        public bool IsAnimating => sequence != null && !sequence.IsComplete;
        public TenPullSequence Sequence => sequence;

        public void Present(IReadOnlyList<SummonPresentationResult> results, Func<string, Sprite> resolvePortrait,
            Action onFinished, Action onBack, Action onHome, bool preview = false, bool showSummary = false,
            TenPullPresentationSettings configuration = null)
        {
            score?.Dispose();
            score = null;
            if (sound != null) StopSound();
            ReleaseOwnedSettings();
            settings = configuration != null ? configuration : Resources.Load<TenPullPresentationSettings>(TenPullPresentationSettings.ResourcePath);
            ownedSettings = settings == null;
            if (ownedSettings) settings = ScriptableObject.CreateInstance<TenPullPresentationSettings>();
            sequence = new TenPullSequence(results, settings);
            portraits = new Sprite[results.Count];
            displayResults = new SummonPresentationResult[results.Count];
            for (int i = 0; i < portraits.Length; i++)
            {
                displayResults[i] = sequence.GetResult(i);
                portraits[i] = resolvePortrait?.Invoke(displayResults[i].MonsterId);
            }
            finished = onFinished; back = onBack; home = onHome; isPreview = preview;
            visualClock = 0f;
            resonancePlayed = false;
            releasePlayed = false; arrivedSoundCount = 0;
            compressionSilent = false; cutInSoundPlayed = false;
            suppressScoreCadence = showSummary;
            summaryBackAllowed = summaryHomeAllowed = true;
            summaryGuidance = string.Empty;
            if (!built) Build();
            score = new TenPullScorePlayer(gameObject, settings.Music, preview, value => ScoreCued?.Invoke(value));
            for (int i = 0; i < sequence.Count; i++)
            {
                var item = displayResults[i];
                slots[i].Name.text = item.DisplayName;
                slots[i].Detail.text = $"クラス{item.ClassRank}　個体値 {item.IndividualValue}";
                slots[i].Badge.text = item.IsNew ? "NEW" : "獲得済種";
            }
            sequence.PhaseChanged += PhaseChanged;
            sequence.Completed += () => finished?.Invoke();
            if (Application.isPlaying && !isPreview && !showSummary) AcquireFrameRate();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (showSummary) sequence.Skip(); else PhaseChanged();
            RenderFrame();
        }

        public void ConfigureSummaryNavigation(bool allowBack, bool allowHome, string guidance)
        {
            summaryBackAllowed = allowBack;
            summaryHomeAllowed = allowHome;
            summaryGuidance = guidance ?? string.Empty;
            RenderFrame();
        }

        private void Build()
        {
            built = true;
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            // Block taps even in screen areas outside the safe-area content.
            var blocker = gameObject.AddComponent<Image>(); blocker.color = Color.black; blocker.raycastTarget = true;
            background = Picture("SummonChamber", transform, Load(settings.BackgroundResource), Vector2.one * 0.5f, Vector2.zero);
            background.preserveAspect = false;
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.color = new Color(0.34f, 0.38f, 0.48f, 1f);
            background.raycastTarget = true;
            content = Rect("TenPullSafeContent", transform, new Vector2(0.5f, 0.5f), Vector2.zero);
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = content.offsetMax = Vector2.zero;
            cinematic = new TenPullCinematicEffects(content, settings, Load);
            title = Label("Title", content, "十の契約印", 46, new Vector2(0.5f, 0.90f), new Vector2(720, 70), Gold);
            subtitle = Label("Subtitle", content, "十の石に宿る、新たな出会い", 26, new Vector2(0.5f, 0.858f), new Vector2(830, 58), Color.white);
            progress = Label("Progress", content, "0 / 10", 32, new Vector2(0.5f, 0.812f), new Vector2(600, 54), Gold);
            skipButton = Button("Skip", content, "結果へスキップ", new Vector2(0.82f, 0.96f), new Vector2(290, 100), Skip);
            autoButton = Button("Auto", content, "自動：ON", new Vector2(0.18f, 0.96f), new Vector2(250, 100), ToggleAuto);

            introductionRoot = Rect("SummoningConvergence", content, new Vector2(0.5f, 0.51f), new Vector2(900, 1000));
            seal = Picture("AwakeningSeal", introductionRoot, Load(settings.SealResource), Vector2.one * 0.5f, new Vector2(740, 740));
            outerSeal = Picture("Class4OuterSeal", introductionRoot, Load(settings.SealResource), Vector2.one * 0.5f, new Vector2(960, 960));
            convergenceLight = Picture("GatheredLight", introductionRoot, Load(settings.CutInResource), Vector2.one * 0.5f, new Vector2(700, 300));
            for (int i = 0; i < 32; i++) convergenceParticles.Add(Picture("ConvergingSpark_" + i, introductionRoot,
                Load(settings.ParticleResource), Vector2.one * 0.5f, new Vector2(40, 40)));

            for (int i = 0; i < 10; i++)
            {
                var slot = new Slot { Root = Rect("StoneSlot_" + i, content, new Vector2(0.5f, 0.5f), new Vector2(400, 270)) };
                slot.Visibility = slot.Root.gameObject.AddComponent<CanvasGroup>();
                slot.Frame = Picture("Frame", slot.Root, Load("UI/FusionPage/FusionRosterFrame"), new Vector2(0.5f, 0.5f), new Vector2(400, 270));
                slot.Frame.preserveAspect = false;
                slot.Aura = Picture("Class4Aura", slot.Root, Load(settings.SealResource), new Vector2(0.5f, 0.53f), new Vector2(236, 236));
                slot.Trail = Picture("ArrivalTrail", slot.Root, Load(settings.StreakResource), new Vector2(0.5f, 0.53f), new Vector2(400, 110));
                slot.Trail.preserveAspect = false;
                slot.Landing = Picture("LandingFlash", slot.Root, Load(settings.VortexResourcePrefix + "6"), new Vector2(0.5f, 0.53f), Vector2.one * 240);
                slot.Stone = Picture("Stone", slot.Root, null, new Vector2(0.5f, 0.53f), new Vector2(210, 210));
                slot.Portrait = Picture("Portrait", slot.Root, null, new Vector2(0.5f, 0.66f), new Vector2(180, 160));
                slot.Number = Label("Number", slot.Root, (i + 1).ToString("00"), 26, new Vector2(0.13f, 0.85f), new Vector2(60, 42), Gold);
                slot.Name = Label("Name", slot.Root, "", 28, new Vector2(0.5f, 0.31f), new Vector2(352, 48), Color.white);
                slot.Detail = Label("Detail", slot.Root, "", 23, new Vector2(0.5f, 0.105f), new Vector2(352, 30), Gold);
                slot.Badge = Label("New", slot.Root, "", 24, new Vector2(0.79f, 0.85f), new Vector2(124, 40), new Color(0.6f, 1f, 0.79f));
                slots.Add(slot);
            }

            focalRoot = Rect("FocalReveal", content, new Vector2(0.5f, 0.585f), new Vector2(700, 850));
            glow = Picture("Resonance", focalRoot, Load(settings.CutInResource), new Vector2(0.5f, 0.48f), new Vector2(840, 350));
            focalStone = Picture("FocusedStone", focalRoot, null, new Vector2(0.5f, 0.56f), new Vector2(490, 490));
            focalMonster = Picture("RevealedMonster", focalRoot, null, new Vector2(0.5f, 0.55f), new Vector2(570, 540));
            monsterName = Label("MonsterName", content, "", 40, new Vector2(0.5f, 0.367f), new Vector2(950, 88), Color.white);
            monsterInfo = Label("MonsterInfo", content, "", 29, new Vector2(0.5f, 0.324f), new Vector2(950, 65), Gold);
            for (int i = 0; i < 20; i++) sparks.Add(Picture("Spark_" + i, focalRoot, null, Vector2.one * 0.5f, new Vector2(48, 48)));

            cutInRoot = Rect("RareCutIn", content, new Vector2(0.5f, 0.58f), new Vector2(1080, 760));
            cutInShade = Picture("Shade", cutInRoot, null, Vector2.one * 0.5f, new Vector2(1400, 2900));
            cutInShade.color = new Color(0.005f, 0.009f, 0.025f, 0.97f);
            cutInSeal = Picture("RuptureSeal", cutInRoot, Load(settings.SealResource), Vector2.one * 0.5f, Vector2.one * 1100);
            rift = Picture("DimensionalRift", cutInRoot, Load(settings.CutInResource), Vector2.one * 0.5f, new Vector2(1150, 570));
            cutInPortrait = Picture("MonsterSilhouette", cutInRoot, null, new Vector2(0.5f, 0.54f), new Vector2(560, 550));
            cutInSlash = Picture("DiagonalSlash", cutInRoot, Load(settings.StreakResource), Vector2.one * 0.5f, new Vector2(1850, 300));
            cutInSlash.preserveAspect = false;
            cutInTitle = Label("RareTitle", cutInRoot, "", 52, new Vector2(0.5f, 0.13f), new Vector2(950, 110), Gold);
            animeFilm = new TenPullAnimeFilm(content, settings, Load);
            flash = Picture("SoftFlash", content, null, Vector2.one * 0.5f, Vector2.zero);
            flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
            hint = Label("Hint", content, "", 25, new Vector2(0.5f, 0.055f), new Vector2(930, 56), Color.white);
            nextButton = Button("Next", content, "開放する", new Vector2(0.5f, 0.265f), new Vector2(360, 100), () => sequence?.Advance());
            backButton = Button("BackToSummon", content, "契約画面へ", new Vector2(0.72f, 0.045f), new Vector2(390, 106), () => { StopAndHide(); back?.Invoke(); });
            homeButton = Button("BackHome", content, "ホームへ", new Vector2(0.28f, 0.045f), new Vector2(390, 106), () => { StopAndHide(); home?.Invoke(); });
            // Controls must remain above the flash/cut-in, and the skip control always responds.
            skipButton.transform.SetAsLastSibling(); autoButton.transform.SetAsLastSibling();
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0f;
        }

        private void Update() { TickPresentation(Time.unscaledDeltaTime); }
        public void TickPresentation(float delta)
        {
            if (sequence == null || !gameObject.activeInHierarchy || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            visualClock += Mathf.Clamp(delta, 0f, 0.1f);
            score?.Tick(delta);
            sequence.Tick(delta);
            if (!UsesOriginalMusic && !resonancePlayed && sequence.Phase == TenPullPhase.Introduction && sequence.Elapsed / sequence.Duration >= settings.ResonanceAt)
            {
                resonancePlayed = true;
                var intro = sequence.HasClass4 ? settings.UpperIntroduction : settings.NormalIntroduction;
                Play(intro.ResonanceCue, intro.SoundVolume);
            }
            if (!UsesOriginalMusic && !releasePlayed && sequence.Phase == TenPullPhase.Introduction && sequence.Elapsed / sequence.Duration >= 0.76f)
            {
                releasePlayed = true;
                Play(sequence.HasClass4 ? settings.UpperIntroduction.ResonanceCue : settings.ArrivalCue, 0.8f);
            }
            if (!compressionSilent && sequence.Phase == TenPullPhase.Introduction && sequence.Elapsed / sequence.Duration >= 0.61f && !releasePlayed)
            {
                compressionSilent = true; StopSound();
            }
            if (!cutInSoundPlayed && sequence.Phase == TenPullPhase.CutIn && sequence.Elapsed >= settings.Class4AnticipationSeconds)
            {
                cutInSoundPlayed = true;
                if (UsesOriginalMusic) score.RevealClass4();
                else
                {
                    var rareSound = settings.ForClass(displayResults[sequence.Index].ClassRank);
                    Play(rareSound.RevealCue, rareSound.SoundVolume);
                }
            }
            if (sequence.Phase == TenPullPhase.Materialization)
            {
                int arrived = Mathf.Clamp(Mathf.FloorToInt((sequence.Elapsed - settings.StoneArrivalSeconds) / Mathf.Max(0.03f, settings.StoneArrivalInterval)) + 1, 0, sequence.Count);
                if (arrived > arrivedSoundCount)
                {
                    if (UsesOriginalMusic)
                        for (int i = arrivedSoundCount; i < arrived; i++) score.ArriveStone();
                    arrivedSoundCount = arrived;
                    // The visual landing cadence drives short accents without affecting the result sequence.
                    if (!UsesOriginalMusic && (arrived % 2 == 0 || arrived == 1)) Play(settings.ArrivalCue, 0.08f + arrived * 0.007f);
                }
            }
            RenderFrame();
        }

        private void PhaseChanged()
        {
            if (sequence == null) return;
            var tier = settings.ForClass(displayResults[sequence.Index].ClassRank);
            if (UsesOriginalMusic)
            {
                if (sequence.Phase == TenPullPhase.Summary)
                {
                    StopSound(); ReleaseFrameRate();
                    if (suppressScoreCadence) score.Stop(); else score.Complete();
                }
                else if (sequence.Phase == TenPullPhase.Introduction) score.Begin(sequence.HasClass4, sequence.Duration);
                else if (sequence.Phase == TenPullPhase.Materialization) score.BeginReveal();
                else if (sequence.Phase == TenPullPhase.Crack) score.OpenStone();
                else if (sequence.Phase == TenPullPhase.CutIn) { cutInSoundPlayed = false; StopSound(); }
                else if (sequence.Phase == TenPullPhase.Reveal && sequence.Current.ClassRank == 4 && !settings.HasCutIn(4)) score.RevealClass4();
                RenderFrame();
                return;
            }
            if (sequence.Phase == TenPullPhase.Summary) { StopSound(); ReleaseFrameRate(); }
            else if (sequence.Phase == TenPullPhase.Introduction) Play(settings.StartCue, 0.7f);
            else if (sequence.Phase == TenPullPhase.Materialization) Play(settings.ArrivalCue, 0.5f);
            else if (sequence.Phase == TenPullPhase.Crack) Play(tier.CrackCue, tier.SoundVolume * 0.65f);
            else if (sequence.Phase == TenPullPhase.CutIn) { cutInSoundPlayed = false; StopSound(); }
            else if (sequence.Phase == TenPullPhase.Reveal && !settings.HasCutIn(displayResults[sequence.Index].ClassRank)) Play(tier.RevealCue, tier.SoundVolume);
            RenderFrame();
        }

        private void Play(AudioCue cue, float volume)
        {
            SoundCued?.Invoke(cue, volume);
            if (Application.isPlaying && sound != null) AudioManager.Instance?.PlaySeOnSource(sound, cue, volume);
        }

        private void StopSound() { sound?.Stop(); SoundsStopped?.Invoke(); }

        private void RenderFrame()
        {
            if (sequence == null || !built) return;
            var phase = sequence.Phase;
            bool summary = sequence.IsComplete, intro = phase == TenPullPhase.Introduction;
            bool single = sequence.Count == 1;
            bool singleSummary = single && summary;
            bool arriving = phase == TenPullPhase.Materialization, preparing = intro || arriving;
            var result = displayResults[sequence.Index];
            var tier = settings.ForClass(result.ClassRank);
            float t = Mathf.Clamp01(sequence.Elapsed / sequence.Duration);
            float compact = summary || preparing ? 0f : sequence.Index == 0 && phase == TenPullPhase.Focus
                ? Mathf.SmoothStep(0f, 1f, sequence.Elapsed / Mathf.Max(0.1f, settings.GridTransitionSeconds)) : 1f;
            title.text = single ? (summary ? "契約、成立" : "ひとつの呼びかけ") : summary ? "十の契約、成立" : "十の契約印";
            title.gameObject.SetActive(phase != TenPullPhase.CutIn);
            subtitle.gameObject.SetActive(!intro && phase != TenPullPhase.CutIn);
            progress.gameObject.SetActive(phase != TenPullPhase.CutIn);
            progress.rectTransform.anchorMin = progress.rectTransform.anchorMax = new Vector2(0.5f, intro ? 0.2f : 0.812f);
            subtitle.text = summary ? (single ? "呼びかけに応えた、新たな仲間" : "今回出会った10体の仲間") : intro ? "十の契約を、いまここに" : arriving ? "光が、契約石へと姿を変える" : "ひとつずつ、契約が目を覚ます";
            progress.text = summary ? $"{sequence.Count} / {sequence.Count}　{(isPreview ? "プレビュー" : "獲得済み")}" : intro
                ? sequence.HasClass4 && t >= settings.ResonanceAt ? "まばゆい気配が、共鳴する…" : "召喚陣に、力が集まる…"
                : arriving ? (single ? "ひとつの石が、呼びかけに応える" : "十の石が、呼びかけに応える") : $"開放 {sequence.Index + 1} / {sequence.Count}";
            hint.text = isPreview ? "開発用プレビュー：抽選・消費・仲間の付与は行いません" : preparing ? "いつでも結果へスキップできます" : summary ? summaryGuidance : "開いた石は、獲得した仲間の姿に変わります";
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, summary ? .14f : .055f);
            hint.gameObject.SetActive(phase != TenPullPhase.CutIn && !(summary && isPreview));
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (i >= sequence.Count) { slot.Root.gameObject.SetActive(false); continue; }
                var item = displayResults[i]; var look = settings.ForSealedClass(item.ClassRank);
                bool opened = summary || i < sequence.OpenedCount;
                float arrival = arriving ? Mathf.Clamp01((sequence.Elapsed - i * Mathf.Max(0.03f, settings.StoneArrivalInterval)) / Mathf.Max(0.15f, settings.StoneArrivalSeconds)) : 1f;
                slot.Root.gameObject.SetActive(!intro && arrival > 0f && (!single || arriving));
                slot.Visibility.alpha = Mathf.Clamp01(arrival * 2f);
                slot.Root.localScale = Vector3.one * Mathf.Lerp(0.08f, 1f, TenPullCinematicEffects.EaseOut(arrival));
                Vector2 large = single ? new Vector2(.5f, .585f) : new Vector2(i % 2 == 0 ? 0.28f : 0.72f, 0.71f - i / 2 * 0.14f);
                Vector2 small = new Vector2(0.12f + (i % 5) * 0.19f, 0.19f - i / 5 * 0.09f);
                slot.Root.anchorMin = slot.Root.anchorMax = Vector2.Lerp(large, small, compact);
                slot.Root.sizeDelta = single ? new Vector2(700,850) : Vector2.Lerp(new Vector2(400, Mathf.Min(270, content.rect.height * 0.131f)), new Vector2(184, Mathf.Min(185, content.rect.height * 0.081f)), compact);
                // Fly outward from the seal's center, then settle exactly into the existing grid.
                slot.Root.anchoredPosition = arriving ? TenPullCinematicEffects.ArrivalOffset(i, arrival, large, content.rect.size, settings.StoneOrbitTurns) : Vector2.zero;
                slot.Trail.gameObject.SetActive(arriving && arrival > 0 && arrival < 0.95f);
                var offset = slot.Root.anchoredPosition;
                var previousOffset = TenPullCinematicEffects.ArrivalOffset(i, Mathf.Max(0, arrival - 0.05f), large, content.rect.size, settings.StoneOrbitTurns);
                var direction = offset - previousOffset;
                float trailAngle = Mathf.Atan2(direction.y, direction.x);
                slot.Trail.rectTransform.localEulerAngles = new Vector3(0, 0, trailAngle * Mathf.Rad2Deg);
                slot.Trail.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(trailAngle), Mathf.Sin(trailAngle)) * -115f;
                slot.Trail.color = WithAlpha(look.Accent, Mathf.Sin(arrival * Mathf.PI) * settings.TrailOpacity);
                float landingTime = arriving ? sequence.Elapsed - i * settings.StoneArrivalInterval - settings.StoneArrivalSeconds * 0.85f : -1;
                slot.Landing.gameObject.SetActive(landingTime >= 0 && landingTime < 0.22f);
                slot.Landing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(landingTime / 0.22f));
                slot.Landing.color = WithAlpha(Color.white, Mathf.Clamp01(1 - landingTime / 0.22f) * 0.9f);
                slot.Frame.gameObject.SetActive(!single);
                slot.Frame.rectTransform.sizeDelta = slot.Root.sizeDelta;
                var resultLook = settings.ForClass(item.ClassRank);
                slot.Frame.color = opened ? new Color(resultLook.Accent.r * 0.65f, resultLook.Accent.g * 0.65f, resultLook.Accent.b * 0.65f, 1f) : new Color(0.28f, 0.32f, 0.40f, 0.55f * Mathf.Clamp01((arrival - 0.65f) / 0.35f));
                slot.Stone.gameObject.SetActive(!opened);
                slot.Portrait.gameObject.SetActive(opened);
                slot.Portrait.sprite = portraits[i];
                slot.Portrait.enabled = portraits[i] != null;
                slot.Stone.sprite = Load(look.StoneResourcePrefix + "_Intact");
                float pulse = 1f + Mathf.Sin(visualClock * 2.8f + i * 0.73f) * look.IdlePulse;
                if (item.ClassRank == 4) pulse *= settings.Class4StoneEmphasis;
                float iconSize = single ? 490f : Mathf.Lerp(Mathf.Min(200, slot.Root.sizeDelta.y * 0.83f), Mathf.Min(112, slot.Root.sizeDelta.y * 0.82f), compact);
                slot.Stone.rectTransform.anchorMin = slot.Stone.rectTransform.anchorMax = new Vector2(.5f,single ? .56f : .53f);
                slot.Stone.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                PlaceStone(slot.Stone, look.IntactCenter, pulse,
                    new Vector2(0, Mathf.Sin(visualClock * 1.7f + i) * Mathf.Lerp(7, 3, compact)));
                slot.Stone.color = look.StoneTint;
                slot.Aura.gameObject.SetActive(!opened && item.ClassRank == 4);
                slot.Aura.rectTransform.sizeDelta = Vector2.one * iconSize * 1.32f;
                slot.Aura.rectTransform.localEulerAngles = new Vector3(0, 0, visualClock * -16f);
                slot.Aura.color = WithAlpha(look.Accent, look.GlowOpacity * (0.6f + 0.15f * Mathf.Sin(visualClock * 3f)));
                // Compact cards hide their captions, so the portrait belongs in the center.
                // Keep the summary portrait above its name and detail rows.
                slot.Portrait.rectTransform.anchorMin = slot.Portrait.rectTransform.anchorMax =
                    Vector2.Lerp(new Vector2(.5f, .66f), new Vector2(.5f, .5f), compact);
                slot.Portrait.rectTransform.anchoredPosition = Vector2.zero;
                slot.Portrait.rectTransform.sizeDelta = summary ? new Vector2(180, slot.Root.sizeDelta.y * 0.48f) : new Vector2(100, slot.Root.sizeDelta.y * 0.70f);
                slot.Name.gameObject.SetActive(summary); slot.Detail.gameObject.SetActive(summary); slot.Badge.gameObject.SetActive(summary);
                slot.Number.gameObject.SetActive(!single && (arriving || summary));
                slot.Detail.color = resultLook.Accent;
                if (!opened && !preparing && i == sequence.Index) slot.Frame.color = look.Accent;
            }

            focalRoot.gameObject.SetActive(!preparing && (!summary || singleSummary) && phase != TenPullPhase.CutIn);
            monsterName.gameObject.SetActive(!preparing && (!summary || singleSummary) && phase != TenPullPhase.CutIn);
            monsterInfo.gameObject.SetActive(!preparing && (!summary || singleSummary) && phase != TenPullPhase.CutIn);
            bool revealed = phase == TenPullPhase.Reveal || singleSummary;
            if (singleSummary) t = 1f;
            focalMonster.gameObject.SetActive(revealed);
            focalMonster.sprite = portraits[sequence.Index];
            focalMonster.enabled = portraits[sequence.Index] != null;
            focalMonster.color = new Color(1, 1, 1, revealed ? Mathf.Clamp01(t * 9f) : 0f);
            float punch = result.ClassRank == 4 ? settings.Class4RevealPunch : 0.08f;
            focalMonster.rectTransform.localScale = Vector3.one * (revealed ? 1f + punch * Mathf.Sin(Mathf.Clamp01(t * 3f) * Mathf.PI) : 1f);
            focalStone.gameObject.SetActive(!revealed && phase != TenPullPhase.CutIn);
            string state = phase == TenPullPhase.Crack ? "_Cracked" : phase == TenPullPhase.Burst ? "_Shattered" : "_Intact";
            var sealedTier = settings.ForSealedClass(result.ClassRank);
            focalStone.sprite = Load(sealedTier.StoneResourcePrefix + state);
            float stoneScale = phase == TenPullPhase.Focus ? Mathf.Lerp(single ? 0.95f : 0.70f, 1f, Mathf.SmoothStep(0, 1, t)) : phase == TenPullPhase.Burst ? Mathf.Lerp(1f, 1.65f, t) : 1f;
            // Keep the visible stone centered, not merely the transparent image rectangle.
            // After the zoom settles, hold it still until it breaks.
            PlaceStone(focalStone, sealedTier.CenterForState(state), stoneScale, Vector2.zero);
            focalStone.color = WithAlpha(Color.Lerp(sealedTier.StoneTint, Color.white, phase == TenPullPhase.Crack ? t : 0f), phase == TenPullPhase.Burst ? 1f - t : 1f);
            bool rare = result.ClassRank == 4;
            glow.gameObject.SetActive(rare);
            glow.sprite = Load(settings.SealResource);
            glow.rectTransform.sizeDelta = new Vector2(680, 680);
            glow.color = WithAlpha(tier.Accent, tier.GlowOpacity * (revealed ? 0.7f : 0.55f + t * 0.4f));
            glow.rectTransform.localEulerAngles = new Vector3(0, 0, visualClock * 20f);
            glow.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(visualClock * 1.7f) * 0.06f);
            monsterName.text = revealed ? result.DisplayName : rare ? "強い気配が、石の奥から…" : "契約石が応えています";
            monsterName.color = revealed ? Color.white : tier.Accent;
            monsterName.rectTransform.anchoredPosition = revealed ? new Vector2(0, -24f * (1f - TenPullCinematicEffects.EaseOut(t * 4f))) : Vector2.zero;
            if (revealed) monsterName.color = WithAlpha(Color.white, Mathf.Clamp01((t - 0.1f) * 8f));
            monsterInfo.text = revealed ? $"クラス{result.ClassRank}　個体値 {result.IndividualValue}　{(result.IsNew ? "NEW" : "獲得済種")}" : "";
            monsterInfo.color = tier.Accent;
            cutInRoot.gameObject.SetActive(phase == TenPullPhase.CutIn);
            if (phase == TenPullPhase.CutIn)
            {
                float rupture = TenPullCinematicEffects.EaseOut(Mathf.InverseLerp(settings.Class4AnticipationSeconds, 0.48f, sequence.Elapsed));
                rift.rectTransform.localScale = new Vector3(Mathf.Lerp(0.08f, 1.10f, rupture), Mathf.Lerp(0.5f, 1.1f, rupture), 1f);
                rift.color = new Color(1f, 0.86f, 0.58f, rupture * 0.8f);
                cutInRoot.anchoredPosition = Shake(tier.ShakePixels * (1f - t));
                cutInShade.rectTransform.sizeDelta = new Vector2(1600, content.rect.height * 1.5f);
                cutInSeal.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.2f, rupture);
                cutInSeal.rectTransform.localEulerAngles = new Vector3(0, 0, -t * 50f);
                cutInSeal.color = WithAlpha(tier.Accent, rupture * 0.45f);
                cutInSlash.rectTransform.localEulerAngles = new Vector3(0, 0, -24);
                cutInSlash.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1350, 1150, rupture), Mathf.Lerp(530, -480, rupture));
                cutInSlash.color = WithAlpha(Color.white, Mathf.Sin(rupture * Mathf.PI));
                cutInPortrait.sprite = portraits[sequence.Index];
                cutInPortrait.enabled = portraits[sequence.Index] != null;
                cutInPortrait.color = Color.Lerp(new Color(0.015f, 0.01f, 0.05f), new Color(0.32f, 0.23f, 0.43f), rupture);
                cutInPortrait.rectTransform.sizeDelta = new Vector2(730, 720);
                cutInPortrait.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.14f, 1f, rupture);
                cutInPortrait.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(110, 0, rupture), 30);
                cutInTitle.text = "虹煌の契約";
                cutInTitle.color = WithAlpha(new Color(1f, 0.87f, 0.54f), rupture);
                cutInTitle.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-100, 0, rupture), -42);
            }
            for (int i = 0; i < sparks.Count; i++)
            {
                var spark = sparks[i]; bool active = !preparing && !summary && i < tier.SparkCount && (phase == TenPullPhase.Burst || revealed);
                spark.gameObject.SetActive(active);
                if (!active) continue;
                spark.sprite = Load(sealedTier.StoneResourcePrefix + "_Shattered");
                float angle = i * 2.399963f + visualClock * 0.2f;
                float radius = 160 + (i % 4) * 55 + t * 100;
                spark.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.75f);
                spark.rectTransform.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg + t * 45);
                spark.color = new Color(1, 1, 1, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * 0.75f);
            }
            flash.color = new Color(tier.Accent.r, tier.Accent.g, tier.Accent.b,
                phase == TenPullPhase.Burst ? (1f - t) * tier.FlashOpacity : 0f);
            RenderIntroduction(intro, arriving, t);
            cinematic.Render(sequence, visualClock);
            animeFilm.Render(sequence);
            if (intro && animeFilm.IsVisible)
            {
                // Let the actor and shot changes carry the scene; controls remain above the film.
                title.gameObject.SetActive(false); progress.gameObject.SetActive(false); hint.gameObject.SetActive(false);
                introductionRoot.gameObject.SetActive(false);
                cinematic.Hide();
                flash.color = Color.clear;
            }
            nextButton.gameObject.SetActive(!preparing && !summary && phase != TenPullPhase.CutIn);
            nextButton.interactable = sequence.CanAdvance;
            nextButton.GetComponentInChildren<Text>().text = revealed ? (single ? "結果を確認" : "次の石へ") : "開放する";
            autoButton.gameObject.SetActive(!summary && !intro && phase != TenPullPhase.CutIn); skipButton.gameObject.SetActive(!summary);
            autoButton.GetComponentInChildren<Text>().text = sequence.AutoPlay ? "自動：ON" : "自動：OFF";
            backButton.gameObject.SetActive(summary && summaryBackAllowed);
            homeButton.gameObject.SetActive(summary && summaryHomeAllowed);
            backButton.GetComponentInChildren<Text>().text = summaryHomeAllowed ? "契約画面へ" : "召喚を続ける";
            backButton.GetComponent<RectTransform>().anchorMin = backButton.GetComponent<RectTransform>().anchorMax = new Vector2(summaryHomeAllowed ? .72f : .5f,.045f);
            homeButton.GetComponent<RectTransform>().anchorMin = homeButton.GetComponent<RectTransform>().anchorMax = new Vector2(summaryBackAllowed ? .28f : .5f,.045f);
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        private Vector2 Shake(float pixels) => new Vector2(Mathf.Sin(visualClock * 83f), Mathf.Sin(visualClock * 67f)) * pixels;

        private void RenderIntroduction(bool intro, bool arriving, float t)
        {
            introductionRoot.gameObject.SetActive(intro || arriving);
            // Start dark on the first rendered frame; all ten slots are still inactive here.
            background.color = intro ? Color.Lerp(new Color(0.07f, 0.08f, 0.16f), new Color(0.35f, 0.38f, 0.55f), t)
                : sequence.Phase == TenPullPhase.Reveal && displayResults[sequence.Index].ClassRank == 4
                    ? new Color(0.32f, 0.23f, 0.45f) : new Color(0.34f, 0.38f, 0.48f);
            float cameraZoom = intro ? settings.CameraPush * TenPullCinematicEffects.EaseOut(t)
                : arriving ? Mathf.Lerp(settings.CameraPush, 0.025f, t) : sequence.IsComplete ? 0 : 0.025f;
            background.rectTransform.localScale = Vector3.one * (1f + cameraZoom);
            background.rectTransform.anchoredPosition = new Vector2(0, -cameraZoom * 180);
            if (!intro && !arriving) return;
            var look = sequence.HasClass4 ? settings.UpperIntroduction : settings.NormalIntroduction;
            float resonance = intro ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(settings.ResonanceAt, 0.67f, t)) : 1f;
            float fade = arriving ? 1f - Mathf.Clamp01(sequence.Elapsed / 0.65f) : 1f;
            float strength = sequence.Class4Count > 1 ? 1.1f : 1f;
            Color color = Color.Lerp(settings.NormalIntroduction.Color, look.Color, resonance);
            seal.color = WithAlpha(color, Mathf.Lerp(0.18f, look.GlowOpacity * 0.5f, intro ? t : 1f) * fade);
            seal.rectTransform.localEulerAngles = new Vector3(0, 0, intro ? t * t * 170f : visualClock * 60f);
            seal.rectTransform.localScale = Vector3.one * (intro ? Mathf.Lerp(0.55f, 1.16f, Mathf.SmoothStep(0, 1, t)) : 1.16f + t * 0.4f);
            outerSeal.gameObject.SetActive(sequence.HasClass4 && resonance > 0f);
            outerSeal.color = WithAlpha(look.Color, resonance * look.GlowOpacity * 0.9f * fade);
            outerSeal.rectTransform.localEulerAngles = new Vector3(0, 0, -visualClock * 36f);
            outerSeal.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, strength, resonance);
            introductionRoot.anchoredPosition = Shake(look.ShakePixels * resonance * fade);
            convergenceLight.gameObject.SetActive(false); // The animated vortex and pillar now provide the core light.
            for (int i = 0; i < convergenceParticles.Count; i++)
            {
                var particle = convergenceParticles[i];
                int count = Mathf.Min(32, look.ParticleCount + (sequence.Class4Count > 1 ? 4 : 0));
                particle.gameObject.SetActive(i < count && fade > 0f);
                if (i >= count) continue;
                float travel = Mathf.Repeat(visualClock * 0.65f + i * 0.137f, 1f);
                float angle = i * 2.399963f + visualClock * 0.12f;
                float radius = Mathf.Lerp(520f, 20f, travel);
                particle.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                particle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, travel);
                particle.color = WithAlpha(color, Mathf.Sin(travel * Mathf.PI) * look.GlowOpacity * fade);
            }
            float glint = intro ? Mathf.Clamp01(1f - Mathf.Abs(t - 0.81f) / 0.075f) : 0f;
            flash.color = WithAlpha(Color.Lerp(color, Color.white, 0.7f), glint * (settings.ReleaseFlashOpacity + look.FlashOpacity * 0.4f));
        }

        public void Skip() { suppressScoreCadence = true; score?.Stop(); sequence?.Skip(); StopSound(); RenderFrame(); }
        public void ToggleAuto() { if (sequence != null) sequence.AutoPlay = !sequence.AutoPlay; RenderFrame(); }
        public void StopAndHide() { Interrupt(); gameObject.SetActive(false); }
        public void Interrupt() { if (IsAnimating) Skip(); else { score?.Stop(); StopSound(); } }
        private void OnApplicationPause(bool paused) { if (paused) Interrupt(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Interrupt(); }
        private void OnDisable() { Interrupt(); }
        private void OnDestroy() { score?.Dispose(); score = null; ReleaseFrameRate(); ReleaseOwnedSettings(); }
        private void AcquireFrameRate()
        {
            if (frameRateOwned) return;
            previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = Math.Max(60, previousFrameRate);
            frameRateOwned = true;
        }
        private void ReleaseFrameRate()
        {
            if (!frameRateOwned) return;
            Application.targetFrameRate = previousFrameRate;
            frameRateOwned = false;
        }
        private void ReleaseOwnedSettings()
        {
            if (!ownedSettings || settings == null) return;
            if (Application.isPlaying) Destroy(settings); else DestroyImmediate(settings);
        }

        private Sprite Load(string path)
        {
            if (!spriteCache.TryGetValue(path, out var sprite)) { sprite = Resources.Load<Sprite>(path); spriteCache[path] = sprite; }
            return sprite;
        }

        private static void PlaceStone(Image image, Vector2 artworkCenter, float scale, Vector2 offset)
        {
            var rect = image.rectTransform;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = offset + Vector2.Scale(Vector2.one * 0.5f - artworkCenter, rect.sizeDelta) * scale;
        }
        private static readonly Color Gold = new Color(1f, 0.81f, 0.36f, 1f);
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * 0.5f; rect.sizeDelta = size;
            return rect;
        }
        private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size)
        {
            var image = Rect(name, parent, anchor, size).gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        private static Text Label(string name, Transform parent, string text, int font, Vector2 anchor, Vector2 size, Color color)
        {
            var label = Rect(name, parent, anchor, size).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text;
            label.fontSize = font; label.resizeTextForBestFit = true; label.resizeTextMinSize = Math.Max(20, font - 4); label.resizeTextMaxSize = font;
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter; label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false;
            return label;
        }
        private Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, anchor, size);
            // The full rectangle is tappable; enlarge only the decorative plate to compensate for its transparent margins.
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var image = Picture("Plate", rect, Load(settings.ButtonResource), Vector2.one * 0.5f, size + new Vector2(0, 70));
            image.preserveAspect = false;
            var button = rect.gameObject.AddComponent<FreshPressButton>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var label = Label("Label", rect, text, 29, Vector2.one * 0.5f, size - new Vector2(36, 24), Color.white);
            return button;
        }
    }
}
