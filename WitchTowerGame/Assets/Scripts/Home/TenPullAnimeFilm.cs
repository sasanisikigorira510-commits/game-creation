using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Home
{
    /// <summary>
    /// Three deliberately cut shots, each animated from registered, persistent artwork layers.
    /// Body landmarks never jump between independently generated drawings. Only rigid transforms
    /// and uniformly scaled light sprites move; this renderer never draws or awards a result.
    /// </summary>
    public sealed class TenPullAnimeFilm
    {
        public const string LayoutResource = "UI/GachaPage/TenPull/Anime/Continuous/Layout";
        private const string ArtPrefix = "UI/GachaPage/TenPull/Anime/Continuous/";
        private static readonly Vector2 AuthorSize = new Vector2(360, 640);
        private readonly Image picture;
        private readonly CanvasGroup opacity;
        private readonly RectTransform parent;
        private readonly TenPullPresentationSettings settings;
        private readonly LayeredShot opening, casting;
        private readonly Sprite openingFallback, normalFallback, upperFallback, normalEye, upperEye;
        private readonly Image eyeLight;
        public bool IsVisible => picture.gameObject.activeSelf;

        public TenPullAnimeFilm(RectTransform container, TenPullPresentationSettings configuration, Func<string, Sprite> load)
        {
            parent = container;
            settings = configuration;
            picture = CreateImage("AnimeFilm", parent, null, AuthorSize);
            opacity = picture.gameObject.AddComponent<CanvasGroup>();
            opacity.interactable = opacity.blocksRaycasts = false;
            var source = Resources.Load<TextAsset>(settings.AnimeFilmLayoutResource);
            var layout = source != null ? JsonUtility.FromJson<FilmLayout>(source.text) : new FilmLayout();
            layout = layout ?? new FilmLayout();
            openingFallback = load(settings.AnimeOpeningPrefix + "2");
            normalFallback = load(settings.AnimeNormalPrefix + "6");
            upperFallback = load(settings.AnimeUpperPrefix + "6");
            normalEye = load(settings.AnimeNormalPrefix + "3");
            upperEye = load(settings.AnimeUpperPrefix + "3");
            opening = new LayeredShot(picture.rectTransform, "Opening", layout.opening,
                layout.openingStaffTip, load, settings);
            casting = new LayeredShot(picture.rectTransform, "Casting", layout.casting,
                layout.castingPalm, load, settings);
            eyeLight = CreateImage("EyeLightWipe", picture.rectTransform, load(settings.StreakResource), new Vector2(250, 106));
            eyeLight.gameObject.SetActive(false);
            picture.gameObject.SetActive(false);
        }

        public void Render(TenPullSequence sequence)
        {
            bool intro = sequence.Phase == TenPullPhase.Introduction;
            bool arrival = sequence.Phase == TenPullPhase.Materialization && sequence.Elapsed < settings.AnimeExitFadeSeconds;
            float eyeDuration = Mathf.Min(settings.AnimeEyeInsertSeconds, sequence.Duration * 0.65f);
            bool cutInEye = sequence.Phase == TenPullPhase.CutIn && sequence.Current.ClassRank == 4 && sequence.Elapsed < eyeDuration;
            picture.gameObject.SetActive(settings.AnimeEnabled && (intro || arrival || cutInEye));
            if (!IsVisible) return;

            // Fit the authored portrait canvas as a whole. Never change an individual drawing's aspect ratio.
            float cover = Mathf.Max(parent.rect.width / AuthorSize.x, parent.rect.height / AuthorSize.y);
            picture.rectTransform.localScale = Vector3.one * cover;
            picture.rectTransform.anchoredPosition = Vector2.zero;
            picture.rectTransform.localEulerAngles = Vector3.zero;
            picture.rectTransform.sizeDelta = AuthorSize;
            picture.color = Color.white;
            opacity.alpha = arrival ? 1f - TenPullFilmMotion.Smooth(sequence.Elapsed / Mathf.Max(0.01f, settings.AnimeExitFadeSeconds)) : 1f;

            float p = Mathf.Clamp01(sequence.Elapsed / Mathf.Max(0.01f, sequence.Duration));
            float openingEnd = Mathf.Clamp(settings.AnimeOpeningFraction, 0.25f, 0.6f);
            float eyeEnd = openingEnd + 0.18f;
            if (cutInEye || intro && p >= openingEnd && p < eyeEnd)
            {
                ShowOnly(null);
                float t = cutInEye ? sequence.Elapsed / Mathf.Max(0.01f, eyeDuration) : Mathf.InverseLerp(openingEnd, eyeEnd, p);
                RenderEye(Mathf.Clamp01(t), cutInEye || sequence.HasClass4);
                if (cutInEye) opacity.alpha = 1f - TenPullFilmMotion.Smooth(Mathf.InverseLerp(0.78f, 1f, t));
                return;
            }

            bool firstShot = intro && p < openingEnd;
            eyeLight.gameObject.SetActive(false);
            float progress = firstShot ? p / openingEnd : intro ? Mathf.InverseLerp(eyeEnd, 1f, p) : 1f;
            var shot = firstShot ? opening : casting;
            if (shot.IsReady)
            {
                picture.enabled = false;
                ShowOnly(shot);
                shot.Render(progress, sequence.HasClass4, sequence.Class4Count, settings.AnimeCameraPush);
            }
            else
            {
                ShowOnly(null);
                // An absent layer must not resurrect the inconsistent 55-cel flipbook.
                picture.sprite = firstShot ? openingFallback : sequence.HasClass4 ? upperFallback : normalFallback;
                picture.enabled = picture.sprite != null;
                picture.rectTransform.localScale *= 1f + settings.AnimeCameraPush * TenPullFilmMotion.Smooth(progress);
                if (!picture.enabled) picture.gameObject.SetActive(false);
            }
        }

        private void ShowOnly(LayeredShot shot)
        {
            opening.SetActive(shot == opening);
            casting.SetActive(shot == casting);
        }

        private void RenderEye(float t, bool upper)
        {
            picture.sprite = upper ? upperEye : normalEye;
            picture.enabled = picture.sprite != null;
            if (!picture.enabled) { picture.gameObject.SetActive(false); return; }
            float push = settings.AnimeCameraPush * TenPullFilmMotion.Smooth(t);
            picture.rectTransform.localScale *= 1f + push;
            // One registered close-up, not incompatible face drawings.
            eyeLight.gameObject.SetActive(true);
            eyeLight.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-300, 300, TenPullFilmMotion.Smooth(t)), 24);
            eyeLight.rectTransform.localEulerAngles = new Vector3(0, 0, -18);
            eyeLight.color = Alpha(upper ? new Color(1f, 0.84f, 0.50f) : new Color(0.60f, 0.79f, 1f),
                Mathf.Sin(t * Mathf.PI) * (upper ? 0.65f : 0.28f));
        }

        // Coordinates refer to the layer pivot, in the centered 360 × 640 author canvas.
        // Paths may be absolute Resources paths, or short names relative to ArtPrefix.
        [Serializable] public sealed class LayerPlacement
        {
            public string name, resource;
            public float x, y, width = 360, height = 640, pivotX = 0.5f, pivotY = 0.5f, rotation;
            public bool enabled = true;
            public float motionScale = 1f;
            // Anatomical attachment in the body sprite, measured from its bottom-left.
            // The arm pivot is its painted shoulder; free screen coordinates cannot move it off the body.
            public string attachTo;
            public float bodyAnchorX, bodyAnchorY;
        }

        [Serializable] public sealed class FilmLayout
        {
            public LayerPlacement[] opening, casting;
            public Vector2 openingStaffTip = new Vector2(12, 170);
            public Vector2 castingPalm = new Vector2(-26, 90);
        }

        private sealed class ArtLayer
        {
            public Image Image;
            public LayerPlacement Placement;
            public int Motion;
            public RectTransform BodyJoint;
        }

        private sealed class LayeredShot
        {
            private readonly RectTransform root, camera, actor, magic;
            private readonly List<ArtLayer> layers = new List<ArtLayer>();
            private readonly Image seal, outerSeal, core, backdropSeal, releaseStreak;
            private readonly Image[] motes = new Image[32];
            private readonly TenPullFilmRadiance radiance;
            private readonly bool isOpening;
            private readonly TenPullPresentationSettings settings;
            public bool IsReady { get; }

            public LayeredShot(RectTransform parent, string name, LayerPlacement[] placements, Vector2 emission,
                Func<string, Sprite> load, TenPullPresentationSettings configuration)
            {
                settings = configuration;
                isOpening = name == "Opening";
                root = CreateRect(name + "Shot", parent);
                camera = CreateRect("Camera", root);
                actor = CreateRect("Actor", camera);
                if (placements == null) placements = Array.Empty<LayerPlacement>();
                RectTransform arm = null;
                bool hasBody = false;
                foreach (var placement in placements)
                {
                    if (placement == null || !placement.enabled || string.IsNullOrEmpty(placement.resource)) continue;
                    string path = placement.resource.Contains("/") ? placement.resource : ArtPrefix + placement.resource;
                    var sprite = load(path);
                    if (sprite == null) continue;
                    string layerName = string.IsNullOrEmpty(placement.name) ? sprite.name : placement.name;
                    bool background = layerName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
                    var image = CreateImage(layerName, background ? camera : actor, sprite, new Vector2(placement.width, placement.height));
                    image.rectTransform.pivot = new Vector2(placement.pivotX, placement.pivotY);
                    image.rectTransform.anchoredPosition = new Vector2(placement.x, placement.y);
                    image.rectTransform.localEulerAngles = new Vector3(0, 0, placement.rotation);
                    int motion = layerName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0 ? 1
                        : layerName.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 ? 2
                        : layerName.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0 ? 3 : 0;
                    layers.Add(new ArtLayer { Image = image, Placement = placement, Motion = motion });
                    if (background) image.transform.SetAsFirstSibling();
                    if (layerName.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0) hasBody = true;
                    if (layerName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0) arm = image.rectTransform;
                }
                bool jointsValid = true;
                foreach (var layer in layers)
                {
                    if (string.IsNullOrEmpty(layer.Placement.attachTo)) continue;
                    var body = layers.Find(item => item.Image.name == layer.Placement.attachTo && item != layer);
                    layer.BodyJoint = body?.Image.rectTransform;
                    jointsValid &= layer.BodyJoint != null;
                }
                IsReady = hasBody && arm != null && jointsValid;
                backdropSeal = CreateImage("Class4BackdropSeal", camera, load(settings.SealResource), Vector2.one * 390);
                backdropSeal.transform.SetSiblingIndex(Mathf.Min(1, camera.childCount - 1));
                backdropSeal.rectTransform.anchoredPosition = new Vector2(0, 20);
                magic = CreateRect("AttachedMagic", arm != null ? arm : actor);
                // Use the cropped arm's pivot as the emission origin.
                magic.anchorMin = magic.anchorMax = arm != null ? arm.pivot : Vector2.one * 0.5f;
                magic.anchoredPosition = emission;
                seal = CreateImage("PrimarySeal", magic, load(settings.SealResource), Vector2.one * (isOpening ? 86 : 166));
                outerSeal = CreateImage("Class4Seal", magic, load(settings.SealResource), Vector2.one * (isOpening ? 125 : 231));
                core = CreateImage("CoreLight", magic, load(settings.StreakResource), new Vector2(100, 42));
                // The light's painted tip, rather than its texture center, sits on the staff.
                core.rectTransform.pivot = new Vector2(0.849f, 0.514f);
                releaseStreak = CreateImage("ReleaseStreak", magic, load(settings.StreakResource), new Vector2(140, 60));
                releaseStreak.rectTransform.pivot = new Vector2(0.05f, 0.5f);
                for (int i = 0; i < motes.Length; i++)
                    motes[i] = CreateImage("LightTrail_" + i, magic, load(settings.StreakResource), new Vector2(36, 15));
                radiance = new TenPullFilmRadiance(camera, actor, magic, isOpening, settings.AnimeRadiance, load);
                SetActive(false);
            }

            public void SetActive(bool active)
            {
                if (root.gameObject.activeSelf != active) root.gameObject.SetActive(active);
            }

            public void Render(float progress, bool upper, int class4Count, float cameraPush)
            {
                float t = Mathf.Clamp01(progress);
                var motion = isOpening ? TenPullFilmMotion.Opening(t, cameraPush) : TenPullFilmMotion.Casting(t, cameraPush);
                camera.localScale = Vector3.one * motion.CameraScale;
                camera.anchoredPosition = motion.CameraOffset;
                actor.anchoredPosition = motion.ActorOffset;
                actor.localEulerAngles = new Vector3(0, 0, motion.BodyRotation);
                foreach (var layer in layers)
                {
                    var rect = layer.Image.rectTransform;
                    var placement = layer.Placement;
                    float rotation = layer.Motion == 1 ? motion.ArmRotation : layer.Motion == 2 ? motion.HairRotation
                        : layer.Motion == 3 ? motion.CapeRotation : 0f;
                    float bodyAngle = layer.BodyJoint != null ? layer.BodyJoint.localEulerAngles.z : 0f;
                    rect.localEulerAngles = new Vector3(0, 0, bodyAngle + placement.rotation + rotation * placement.motionScale);
                    if (layer.BodyJoint != null)
                    {
                        var bodyRect = layer.BodyJoint.rect;
                        var shoulder = new Vector3(bodyRect.xMin + bodyRect.width * placement.bodyAnchorX,
                            bodyRect.yMin + bodyRect.height * placement.bodyAnchorY, 0);
                        rect.anchoredPosition = actor.InverseTransformPoint(layer.BodyJoint.TransformPoint(shoulder));
                    }
                }
                RenderMagic(t, motion.Energy, upper, class4Count);
                radiance.Render(t, upper, class4Count);
            }

            private void RenderMagic(float t, float energy, bool upper, int class4Count)
            {
                // The button press lights the staff immediately. After the eye insert the
                // hand retains its charge, so the release shot does not restart in darkness.
                energy = isOpening ? 0.22f + energy * 0.78f : 0.50f + energy * 0.50f;
                var look = upper ? settings.UpperIntroduction : settings.NormalIntroduction;
                // Only the confirmed Class 4 branch develops gold.
                float gold = isOpening ? TenPullFilmMotion.Smooth(Mathf.InverseLerp(settings.AnimeRadiance?.GoldIgnitionAt ?? 0.42f, 0.90f, t)) : 1f;
                Color color = Color.Lerp(new Color(0.66f, 0.80f, 1f), look.Color, gold);
                float luminance = Mathf.Clamp01(look.GlowOpacity) * (upper && class4Count > 1 ? 1.08f : 1f);
                float pulse = isOpening ? 0.80f + 0.20f * TenPullFilmMotion.Smooth(t) : 1f;
                float scale = isOpening ? Mathf.Lerp(0.36f, 0.87f, energy) : Mathf.Lerp(0.54f, 1.16f, energy);
                seal.rectTransform.localScale = Vector3.one * scale;
                seal.rectTransform.localEulerAngles = new Vector3(0, 0, isOpening ? -28 + 52 * TenPullFilmMotion.Smooth(t) : 24 + 74 * TenPullFilmMotion.Smooth(t));
                seal.color = Alpha(color, (0.20f + 0.62f * energy) * luminance * pulse);
                outerSeal.gameObject.SetActive(upper);
                outerSeal.rectTransform.localScale = Vector3.one * scale;
                outerSeal.rectTransform.localEulerAngles = new Vector3(0, 0, 15 - 66 * TenPullFilmMotion.Smooth(t));
                outerSeal.color = Alpha(new Color(1f, 0.78f, 0.34f), gold * energy * luminance * 0.68f);
                core.rectTransform.localScale = Vector3.one * (isOpening ? 0.70f + 0.40f * energy : Mathf.Lerp(0.3f, 1.6f, energy));
                core.rectTransform.localEulerAngles = new Vector3(0, 0, isOpening ? 24 : -18);
                core.color = Alpha(Color.Lerp(Color.white, color, 0.35f),
                    (isOpening ? 0.36f + 0.50f * energy : energy * 0.90f) * luminance);

                backdropSeal.gameObject.SetActive(upper);
                backdropSeal.rectTransform.localScale = Vector3.one * (0.94f + 0.10f * TenPullFilmMotion.Smooth(t));
                backdropSeal.rectTransform.localEulerAngles = new Vector3(0, 0, -8 + 30 * TenPullFilmMotion.Smooth(t));
                backdropSeal.color = Alpha(new Color(1f, 0.72f, 0.25f), gold * energy * (isOpening ? 0.20f : 0.42f));
                // The blast travels from the palm into the visible center/right of the portrait.
                // Its painted aspect ratio remains fixed as the complete streak advances.
                releaseStreak.gameObject.SetActive(!isOpening);
                float release = TenPullFilmMotion.Smooth(Mathf.InverseLerp(0.26f, 0.92f, t));
                releaseStreak.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.06f, upper ? 1.55f : 1.3f, release);
                releaseStreak.rectTransform.localEulerAngles = new Vector3(0, 0, -8);
                releaseStreak.color = Alpha(Color.Lerp(Color.white, color, 0.24f), release * luminance * (upper ? 0.95f : 0.72f));

                int count = Mathf.Min(motes.Length, Mathf.Max(0, look.ParticleCount));
                for (int i = 0; i < motes.Length; i++)
                {
                    var mote = motes[i];
                    mote.gameObject.SetActive(i < count);
                    if (i >= count) continue;
                    // One continuous trip per trail; no modulo reset or random per-frame motion.
                    float start = i * 0.013f - 0.09f;
                    float q = Mathf.Clamp01((t - start) / (1f - start));
                    float travel = TenPullFilmMotion.Smooth(q);
                    float angle = i * 2.399963f + (isOpening ? 0.62f * travel : -0.15f);
                    float distance = isOpening ? Mathf.Lerp(125 + (i % 4) * 22, 9, travel)
                        : Mathf.Lerp(14, 150 + (i % 4) * 28, travel);
                    mote.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    mote.rectTransform.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg + (isOpening ? 180 : 0));
                    mote.rectTransform.localScale = Vector3.one * (isOpening ? 0.62f : 1f) * (0.65f + i % 3 * 0.16f);
                    mote.color = Alpha(color, Mathf.Sin(q * Mathf.PI) * luminance * (upper ? 0.72f : 0.4f));
                }
            }
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = AuthorSize;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.sizeDelta = size;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
