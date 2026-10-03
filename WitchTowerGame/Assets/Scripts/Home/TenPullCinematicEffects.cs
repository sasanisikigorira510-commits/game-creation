using System;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Home
{
    /// <summary>Deterministic image2 VFX layers. No draw, inventory, timers or gameplay callbacks.</summary>
    public sealed class TenPullCinematicEffects
    {
        private readonly RectTransform stage;
        private readonly Image floor, vortex, vortexBlend, pillar, shockwave;
        private readonly Image[] rays = new Image[12];
        private readonly Sprite[] frames = new Sprite[8];
        private readonly TenPullPresentationSettings settings;

        public TenPullCinematicEffects(RectTransform parent, TenPullPresentationSettings configuration, Func<string, Sprite> load)
        {
            settings = configuration;
            stage = new GameObject("CinematicStage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(parent, false); stage.anchorMin = Vector2.zero; stage.anchorMax = Vector2.one;
            stage.offsetMin = stage.offsetMax = Vector2.zero; stage.SetAsFirstSibling();
            for (int i = 0; i < frames.Length; i++) frames[i] = load(settings.VortexResourcePrefix + i);
            floor = Create("PerspectiveSeal", load(settings.SealResource), new Vector2(1000, 1000));
            pillar = Create("ReleasePillar", load(settings.StreakResource), new Vector2(1850, 580));
            pillar.preserveAspect = false;
            pillar.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            vortex = Create("LivingVortex", frames[0], Vector2.one * 780);
            vortexBlend = Create("LivingVortexBlend", frames[0], Vector2.one * 780);
            shockwave = Create("RevealShockwave", frames[7], Vector2.one * 900);
            for (int i = 0; i < rays.Length; i++)
            {
                rays[i] = Create("RadiantTrail_" + i, load(settings.StreakResource), new Vector2(480, 80));
                rays[i].preserveAspect = false;
            }
        }

        public void Hide() => stage.gameObject.SetActive(false);

        public void Render(TenPullSequence sequence, float clock)
        {
            var phase = sequence.Phase;
            bool intro = phase == TenPullPhase.Introduction;
            bool arrival = phase == TenPullPhase.Materialization;
            bool burst = phase == TenPullPhase.Burst;
            bool reveal = phase == TenPullPhase.Reveal;
            bool rare = sequence.Current.ClassRank == 4;
            stage.gameObject.SetActive(intro || arrival || burst || reveal);
            if (!stage.gameObject.activeSelf) return;
            float t = Mathf.Clamp01(sequence.Elapsed / sequence.Duration);
            var look = sequence.HasClass4 ? settings.UpperIntroduction : settings.NormalIntroduction;
            float response = intro ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(settings.ResonanceAt, 0.7f, t)) : 1;
            Color color = intro || arrival ? Color.Lerp(settings.NormalIntroduction.Color, look.Color, response)
                : rare ? new Color(1f, 0.81f, 0.48f) : settings.Normal.Accent;
            float height = stage.rect.height;
            float release = intro ? Mathf.InverseLerp(0.74f, 0.96f, t) : arrival ? 1f : 0f;
            float arrivalFade = arrival ? 1f - Mathf.Clamp01(sequence.Elapsed / 0.55f) : 1f;
            floor.gameObject.SetActive(intro || arrival || reveal);
            floor.rectTransform.anchoredPosition = new Vector2(0, -height * (reveal ? 0.02f : 0.17f));
            floor.rectTransform.localScale = new Vector3(1f + release * 0.25f, 0.32f + release * 0.08f, 1f);
            floor.rectTransform.localEulerAngles = new Vector3(0, 0, 0);
            floor.color = Alpha(color, (intro ? Mathf.Lerp(0.3f, 0.95f, t) : reveal ? 0.4f * (1f - t) : 0.7f) * arrivalFade);

            pillar.gameObject.SetActive(intro && release > 0 || arrival || burst && rare);
            pillar.rectTransform.anchoredPosition = new Vector2(0, height * 0.045f);
            pillar.rectTransform.localScale = new Vector3(1f, intro ? Mathf.Lerp(0.08f, 1.4f, release) : burst ? Mathf.Lerp(1.3f, 0.1f, t) : 1.4f, 1f);
            pillar.color = Alpha(color, settings.PillarOpacity * (intro ? release : burst ? 1f - t : arrivalFade));

            vortex.gameObject.SetActive(intro || arrival);
            vortexBlend.gameObject.SetActive(intro || arrival);
            if (intro || arrival)
            {
                // Charge -> compress -> release: distinct poses and opposing motion, not a constant spin.
                float anticipation = Mathf.InverseLerp(0.58f, 0.74f, t);
                float index = intro ? t < 0.74f ? Mathf.Clamp(t * 7.3f, 0, 4) : Mathf.Lerp(5, 7, release) : 7;
                int current = Mathf.FloorToInt(index), next = Mathf.Min(7, current + 1);
                float blend = index - current;
                vortex.sprite = frames[current]; vortexBlend.sprite = frames[next];
                float scale = intro ? t < 0.58f ? Mathf.Lerp(0.35f, 0.95f, t / 0.58f)
                    : t < 0.74f ? Mathf.Lerp(0.95f, 0.57f, anticipation) : Mathf.Lerp(0.7f, 1.85f, release)
                    : Mathf.Lerp(1.85f, 2.5f, t);
                float rotation = t < 0.58f ? -t * t * 600 : t < 0.74f ? -202 : -202 - release * 100;
                for (int layer = 0; layer < 2; layer++)
                {
                    var image = layer == 0 ? vortex : vortexBlend;
                    image.rectTransform.anchoredPosition = new Vector2(0, -height * 0.015f);
                    image.rectTransform.localScale = Vector3.one * scale;
                    image.rectTransform.localEulerAngles = new Vector3(0, 0, rotation);
                }
                // Keep the blue-white core luminous while its surrounding seals carry the gold cue.
                Color core = Color.Lerp(Color.white, color, 0.3f);
                vortex.color = Alpha(core, (1f - blend) * arrivalFade);
                vortexBlend.color = Alpha(core, blend * arrivalFade);
            }

            shockwave.gameObject.SetActive(burst || reveal);
            shockwave.rectTransform.anchoredPosition = new Vector2(0, height * 0.09f);
            shockwave.rectTransform.localScale = Vector3.one * (burst ? Mathf.Lerp(0.25f, rare ? 1.8f : 1.3f, EaseOut(t))
                : Mathf.Lerp(0.8f, rare ? 2.1f : 1.35f, EaseOut(t)));
            shockwave.rectTransform.localEulerAngles = new Vector3(0, 0, -clock * 23f);
            shockwave.color = Alpha(color, settings.RevealShockwaveOpacity * (1f - t) * (rare ? 1f : 0.5f));
            for (int i = 0; i < rays.Length; i++)
            {
                var ray = rays[i];
                ray.gameObject.SetActive(intro || arrival && arrivalFade > 0 || burst || reveal && rare && t < 0.55f);
                float travel = intro ? Mathf.Repeat(clock * 0.75f + i * 0.173f, 1f) : t;
                float angle = i * Mathf.PI * 2f / rays.Length + (intro ? t * 1.8f : 0.1f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = intro ? Mathf.Lerp(720, 65, travel) : Mathf.Lerp(100, 830, EaseOut(travel));
                ray.rectTransform.anchoredPosition = direction * radius + new Vector2(0, height * (burst || reveal ? 0.09f : -0.015f));
                ray.rectTransform.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg + (intro ? 180 : 0));
                ray.rectTransform.localScale = new Vector3(intro ? Mathf.Lerp(0.6f, 1.25f, travel) : 1.5f, rare || sequence.HasClass4 ? 1f : 0.6f, 1f);
                ray.color = Alpha(color, settings.TrailOpacity * (intro ? Mathf.Sin(travel * Mathf.PI) * Mathf.Lerp(0.3f, 0.85f, t)
                    : (1f - t) * (reveal ? 0.65f : 1f)) * arrivalFade);
            }
        }

        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static Vector2 ArrivalOffset(int index, float progress, Vector2 destination, Vector2 contentSize, float turns)
        {
            float p = Mathf.Clamp01(progress), ease = EaseOut(p);
            Vector2 from = Vector2.Scale(new Vector2(0.5f, 0.5f) - destination, contentSize);
            float angle = index * 0.628319f + p * Mathf.PI * 2f * turns;
            float arc = Mathf.Sin(p * Mathf.PI) * (1f - p) * 290f;
            return from * (1f - ease) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f) * arc;
        }
        private static Color Alpha(Color color, float a) => new Color(color.r, color.g, color.b, Mathf.Clamp01(a));
        private Image Create(string name, Sprite sprite, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(stage, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.sizeDelta = size; image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
    }
}
