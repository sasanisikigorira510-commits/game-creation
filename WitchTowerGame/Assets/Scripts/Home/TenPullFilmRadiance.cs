using System;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Home
{
    /// <summary>
    /// Image2-painted lightning, composited as light in three depth planes. The same sprites
    /// travel along continuous, time-based paths; neither the character nor the light is warped.
    /// Gold is enabled only by the already-confirmed Class 4 result.
    /// </summary>
    public sealed class TenPullFilmRadiance
    {
        private readonly RectTransform background, foreground, attached;
        private readonly Image corona, backSpiral, charge, chargeSpiral;
        private readonly Image[] beams = new Image[3], waves = new Image[3];
        private readonly bool opening;
        private readonly SummonFilmRadiancePresentation settings;
        private readonly Material lightMaterial;

        public TenPullFilmRadiance(RectTransform camera, RectTransform actor, RectTransform emission,
            bool openingShot, SummonFilmRadiancePresentation configuration, Func<string, Sprite> loadSprite)
        {
            opening = openingShot;
            settings = configuration ?? new SummonFilmRadiancePresentation();
            // The resource is shared and owned by Unity. No material allocation at render time.
            lightMaterial = Resources.Load<Material>(settings.ResourcePrefix + "Light");
            background = Root("Class4RadianceBackdrop", camera);
            background.SetSiblingIndex(actor.GetSiblingIndex());
            foreground = Root("Class4RadianceForeground", camera);
            attached = Root("Class4Radiance", emission);
            corona = Light("Corona", background, Art("Corona"), opening ? 470 : 540);
            backSpiral = Light("BackLightning", background, Art("Spiral"), opening ? 520 : 590);
            chargeSpiral = Light("ChargeLightning", attached, Art("Spiral"), opening ? 135 : 225);
            charge = Light("WhiteHotCore", attached, Art("Starburst"), opening ? 118 : 160);
            charge.rectTransform.pivot = new Vector2(0.50437248f, 0.55816771f);
            for (int i = 0; i < beams.Length; i++)
            {
                beams[i] = Light("ReleaseFan_" + i, attached, Art("Beam"), i == 0 ? 420 : 355);
                // Registration of the bright source in the authored beam crop.
                beams[i].rectTransform.pivot = new Vector2(0.05362776f, 0.540625f);
                waves[i] = Light("ForegroundLightning_" + i, foreground, Art("Spiral"), 510 + 65 * i);
            }
            SetActive(false);

            Sprite Art(string name) => loadSprite(settings.ResourcePrefix + name);
        }

        public void Render(float progress, bool upper, int class4Count)
        {
            bool enabled = upper && settings.Enabled && lightMaterial != null;
            SetActive(enabled);
            if (!enabled) return;
            float t = Mathf.Clamp01(progress);
            float gold = opening ? Ease(settings.GoldIgnitionAt, 0.90f, t) : 1f;
            float gain = settings.Intensity * (class4Count > 1 ? 1.10f : 1f);
            float turn = 360f * settings.LightningTurns * TenPullFilmMotion.Smooth(t);
            float release = opening ? 0 : Ease(settings.ReleaseAt, 0.83f, t);
            float chargeLevel = opening ? 0.35f + 0.65f * Ease(0.05f, 0.95f, t) : 0.73f + 0.27f * release;

            // The dark centers preserve the witch's silhouette. Backlight continues across
            // the eye insert instead of restarting the casting shot from a dark screen.
            Pose(corona, new Vector2(0, opening ? 38 : 30), 0.84f + 0.20f * Ease(0, 1, t),
                -12 + turn * 0.23f, gold * gain * settings.BacklightOpacity * (opening ? 0.76f : 1f));
            Pose(backSpiral, new Vector2(opening ? 0 : 35, opening ? 0 : -20),
                0.92f + 0.13f * release, 32 - turn * 0.72f,
                gold * gain * settings.BacklightOpacity * (opening ? 0.42f : 0.72f));
            Pose(chargeSpiral, Vector2.zero, opening ? 0.58f + 0.42f * chargeLevel : 0.60f + 0.40f * release,
                -18 + turn, gold * gain * (opening ? 0.85f : 0.70f));
            float impact = opening ? Ease(0.79f, 0.94f, t) * 0.20f
                : Envelope(settings.ReleaseAt, settings.ReleaseAt + 0.14f, settings.ReleaseAt + 0.38f, t);
            Pose(charge, Vector2.zero, settings.CoreScale * (0.36f + chargeLevel * 0.42f + impact * 0.55f),
                8 + turn * 0.18f, gold * gain * (0.75f + 0.25f * chargeLevel));

            for (int i = 0; i < beams.Length; i++)
            {
                beams[i].gameObject.SetActive(!opening);
                if (!opening)
                {
                    float start = settings.ReleaseAt + i * 0.12f;
                    float flow = Ease(start, Mathf.Min(1f, start + 0.48f), t);
                    // Staggered fans leave from the same painted palm, then sweep below the face.
                    float angle = i == 0 ? -27f : i == 1 ? -73f : -146f;
                    float settle = 1f - 0.16f * Ease(0.78f, 1f, t);
                    Pose(beams[i], Vector2.zero, 0.08f + flow * (i == 0 ? 1.16f : 0.95f),
                        angle - 7f * flow, flow * gain * settle * (i == 0 ? 0.95f : 0.58f));
                }

                // The first foreground arc starts in anticipation; later arcs are successive
                // expanding shock fronts. Each has a single flight, without looping/teleporting.
                float q = opening ? Ease(0.60f + i * 0.07f, 1f, t)
                    : Ease(settings.ReleaseAt + i * 0.14f, 1.04f, t);
                float alpha = opening ? (i == 0 ? gold * 0.36f : 0f)
                    : Mathf.Sin(q * Mathf.PI * 0.85f) * (0.66f - i * 0.13f);
                Pose(waves[i], new Vector2(35 + i * 16, -285 - i * 32 - q * 48),
                    0.84f + q * 0.64f, 24 + i * 94 - turn * (0.7f + i * 0.17f),
                    alpha * gain * settings.ForegroundOpacity);
            }
        }

        private void SetActive(bool value)
        {
            background.gameObject.SetActive(value);
            foreground.gameObject.SetActive(value);
            attached.gameObject.SetActive(value);
        }

        private Image Light(string name, RectTransform parent, Sprite sprite, float size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.sizeDelta = new Vector2(size, sprite != null ? size * sprite.rect.height / sprite.rect.width : size);
            image.sprite = sprite;
            image.material = lightMaterial;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        private static RectTransform Root(string name, RectTransform parent)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * 0.5f;
            root.sizeDelta = Vector2.zero;
            return root;
        }

        private static void Pose(Image image, Vector2 position, float scale, float angle, float alpha)
        {
            var rect = image.rectTransform;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.localEulerAngles = new Vector3(0, 0, angle);
            image.color = new Color(1, 1, 1, Mathf.Clamp01(alpha));
        }

        private static float Ease(float start, float end, float t) => TenPullFilmMotion.Smooth(Mathf.InverseLerp(start, end, t));
        private static float Envelope(float start, float peak, float end, float t) => Ease(start, peak, t) * (1f - Ease(peak, end, t));
    }
}
