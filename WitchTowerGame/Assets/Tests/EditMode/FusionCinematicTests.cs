using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class FusionCinematicTests
    {
        private const string Art = "UI/FusionPage/Cinematic/";
        private static readonly string[] RigPartNames = { "Body", "LeftUpperArm", "RightUpperArm", "LeftForearm", "RightForearm", "Foreground" };
        [Serializable]
        private sealed class RigLayout
        {
            public float canvasWidth, canvasHeight;
            public Vector2 leftShoulder, rightShoulder, leftElbow, rightElbow, leftPalm, rightPalm;
        }
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private Scene scene;
        private GameObject canvas;
        private object presentation, created;
        private ScriptableObject born;
        private Sprite parentA, parentB;
        private UnityEngine.Random.State randomBefore;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object target, string method, params object[] arguments) => target.GetType().GetMethod(method, All).Invoke(target, arguments);
        private static object Static(string method, params object[] arguments) => T("Home.FusionCinematicPresentation").GetMethod(method, All).Invoke(null, arguments);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, All).SetValue(target, value);
        private static bool Playing(object target) => (bool)target.GetType().GetProperty("IsPlaying", All).GetValue(target);

        [SetUp]
        public void Setup()
        {
            randomBefore = UnityEngine.Random.state;
            scene = EditorSceneManager.NewPreviewScene();
            canvas = new GameObject("FusionCinematicTestCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvas, scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 2340);
            presentation = Activator.CreateInstance(T("Home.FusionCinematicPresentation"), new object[] { (RectTransform)canvas.transform });
            born = ScriptableObject.CreateInstance(T("MasterData.MonsterDataSO"));
            Set(born, "monsterId", "fusion_cinematic_test_result"); Set(born, "monsterName", "確認用の仲間"); Set(born, "classRank", 3);
            parentA = Portrait("monster_flare_drake"); parentB = Portrait("monster_rock_golem");
            Assert.That(parentA, Is.Not.Null); Assert.That(parentB, Is.Not.Null);
            Set(born, "portraitSprite", parentA); Set(born, "illustrationSprite", parentA);
            created = Activator.CreateInstance(T("Save.OwnedMonsterData"));
            Set(created, "InstanceId", "display_only_result"); Set(created, "MonsterId", "fusion_cinematic_test_result");
            Set(created, "Level", 1); Set(created, "PlusValue", 4); Set(created, "FusionBonusHp", 19); Set(created, "FusionBonusAttack", 7);
        }

        [TearDown]
        public void Cleanup()
        {
            if (presentation != null) Call(presentation, "Dispose");
            if (born != null) UnityEngine.Object.DestroyImmediate(born);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = randomBefore;
        }

        [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, true)] [TestCase(5, true)]
        public void UpperRitualUsesConfirmedResultClassAndLeavesClassThreeOrdinary(int rank, bool upper)
        {
            Set(born, "classRank", rank);
            Render(1.5f);
            Assert.That(Static("UsesUpperPresentation", rank), Is.EqualTo(upper));
            Assert.That(presentation.GetType().GetProperty("IsUpper", All).GetValue(presentation), Is.EqualTo(upper));
            Assert.That((float)Static("DurationForClass", rank), Is.EqualTo(upper ? 9f : 7f).Within(.001f));
        }

        [Test]
        public void AuthoredRigPartsShareOneHighResolutionRegisteredCanvasAndJointsTouchPaint()
        {
            RigLayout layout = LoadRigLayout();
            var parts = RigPartNames.Select(name => Resources.Load<Sprite>(Art + "IonaRig/" + name)).ToArray();
            Assert.That(parts.All(s => s != null), Is.True, "The fixed body, front hair, and four image2 arm parts must be imported.");
            Assert.That(parts.Distinct().Count(), Is.EqualTo(RigPartNames.Length));
            Assert.That(parts.All(s => s.rect.size == new Vector2(layout.canvasWidth, layout.canvasHeight)), Is.True,
                "Parts must retain their full registered canvas, including transparent padding.");
            Assert.That(layout.canvasHeight, Is.GreaterThanOrEqualTo(1000f), "Do not enlarge the former low-resolution frame atlas again.");
            foreach (var sprite in parts)
            {
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), "Keep the authored pixels sharp.");
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            AssertPaintAt(new[] { "Body", "Foreground" }, layout.leftShoulder);
            AssertPaintAt(new[] { "Body", "Foreground" }, layout.rightShoulder);
            AssertPaintAt("LeftUpperArm", layout.leftShoulder); AssertPaintAt("RightUpperArm", layout.rightShoulder);
            AssertPaintAt("LeftUpperArm", layout.leftElbow); AssertPaintAt("RightUpperArm", layout.rightElbow);
            AssertPaintAt("LeftForearm", layout.leftElbow); AssertPaintAt("RightForearm", layout.rightElbow);
            AssertPaintAt("LeftForearm", layout.leftPalm); AssertPaintAt("RightForearm", layout.rightPalm);
            foreach (string name in new[] { "Chamber", "IonaCutIn", "Effects/Seal", "Effects/Ribbon", "Effects/Cocoon", "Effects/Burst", "Effects/Spark" })
                Assert.That(Resources.Load<Sprite>(Art + name), Is.Not.Null, name + " must use the image2 resource.");
        }

        [TestCase(3)] [TestCase(4)]
        public void RitualKeepsOneBodyDrawingWithoutCaptionZoomForwardMovementOrPoseCrossfades(int rank)
        {
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            Render(0f);
            RectTransform rig = RigRoot();
            Image[] images = rig.GetComponentsInChildren<Image>(true);
            Assert.That(images.Length, Is.EqualTo(6), "There must be fixed rear/body and foreground art plus four arm layers, without faint copies of previous poses.");
            Sprite[] sprites = images.Select(image => image.sprite).ToArray();
            Vector2 position = rig.anchoredPosition, size = rig.sizeDelta;
            Vector3 scale = rig.localScale;
            int visibleSamples = 0;
            for (int frame = 0; frame <= 180; frame++)
            {
                Render(duration * frame / 180f);
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.name == "FusionChapterCaption"), Is.False,
                    "Do not recreate the removed explanation above the ritual, even with zero opacity.");
                var bodyImage = images.Single(i => i.name == "FusionIona");
                Assert.That(rig.anchoredPosition, Is.EqualTo(position), "Iona must stay at the same ritual position while her hands animate.");
                Assert.That(rig.localScale, Is.EqualTo(scale), "The former hand-shot zoom enlarged the entire body halfway through the ritual.");
                Assert.That(rig.sizeDelta, Is.EqualTo(size));
                for (int index = 0; index < images.Length; index++)
                {
                    Assert.That(images[index].sprite, Is.SameAs(sprites[index]), "Redrawing entire poses reintroduces face, clothing and hand jitter.");
                    Assert.That(images[index].preserveAspect, Is.True);
                    Assert.That(images[index].color.a, Is.EqualTo(bodyImage.color.a).Within(.0001f), "Arm/body opacity may fade together, but must never crossfade between two drawings.");
                }
                if (!Visible(bodyImage)) continue;
                visibleSamples++;
                float p = frame / 180f;
                if (p >= .22f && p <= .49f)
                    Assert.That(images.All(image => image.color.a == 1f), Is.True, "The visible hand gesture must remain opaque.");
            }
            Assert.That(visibleSamples, Is.GreaterThan(30), "The fix must retain Iona's presence in the ritual.");
        }

        [TestCase(3)] [TestCase(4)]
        public void ShouldersStayRegisteredAndForearmsStayAttachedToTheirElbows(int rank)
        {
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            RigLayout layout = LoadRigLayout();
            Render(0f);
            Image bodyImage = RigRoot().GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona");
            for (int frame = 0; frame <= 120; frame++)
            {
                Render(duration * frame / 120f);
                foreach (bool left in new[] { true, false })
                {
                    string side = left ? "Left" : "Right";
                    var upperArm = RigRoot().GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona" + side + "UpperArm");
                    var forearm = RigRoot().GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona" + side + "Forearm");
                    var joint = RigRoot().GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "FusionIona" + side + "ElbowJoint");
                    Vector2 sourceShoulder = left ? layout.leftShoulder : layout.rightShoulder;
                    Vector3 shoulder = bodyImage.rectTransform.TransformPoint(new Vector3(sourceShoulder.x - layout.canvasWidth * .5f,
                        layout.canvasHeight * .5f - sourceShoulder.y, 0f));
                    Assert.That(Vector3.Distance(upperArm.rectTransform.position, shoulder), Is.LessThan(.001f), "The drawn shoulder and moving sleeve must have the same pivot.");
                    Assert.That(Vector3.Distance(forearm.rectTransform.position, joint.position), Is.LessThan(.001f), "A detached forearm would shake around the wrong elbow.");
                    Assert.That(Quaternion.Angle(forearm.rectTransform.rotation, joint.rotation), Is.LessThan(.02f));
                }
            }
        }

        [TestCase(3)] [TestCase(4)]
        public void RenderedUpperArmsRemainVisibleBetweenShoulderAndElbowThroughoutTheGesture(int rank)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to detect upper arms hidden behind the body drawing.");
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            RigLayout layout = LoadRigLayout();
            // Check the start, midpoint and final pose. The source sprites and
            // their pivots can all be valid while the final layer ordering hides
            // the entire upper arm, so compare the actual composited pixels.
            foreach (float progress in new[] { .22f, .365f, .51f })
            {
                Render(duration * progress);
                var bodyImage = RigRoot().GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona");
                foreach (bool left in new[] { true, false })
                {
                    string side = left ? "Left" : "Right";
                    var upperArm = RigRoot().GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona" + side + "UpperArm");
                    Assert.That(Visible(upperArm), Is.True);
                    Vector2 shoulder = left ? layout.leftShoulder : layout.rightShoulder;
                    Vector2 elbow = left ? layout.leftElbow : layout.rightElbow;
                    Vector2 alongArm = (elbow - shoulder) * .38f;
                    Vector3 worldPoint = upperArm.rectTransform.TransformPoint(new Vector3(alongArm.x, -alongArm.y, 0f));
                    Vector3 bodyPoint = bodyImage.rectTransform.InverseTransformPoint(worldPoint);
                    Vector2 sourceCenter = new Vector2(bodyPoint.x + layout.canvasWidth * .5f, layout.canvasHeight * .5f - bodyPoint.y);
                    // Keep clear of both joint overlaps: a visible hand or a
                    // shoulder cap alone must not make a missing arm pass.
                    Rect[] regions = { new Rect(sourceCenter.x - 32f, sourceCenter.y - 24f, 64f, 48f) };
                    Color32[] withArm = CaptureRigRegions(regions)[0];
                    Color32[] withoutArm;
                    try
                    {
                        upperArm.enabled = false;
                        withoutArm = CaptureRigRegions(regions)[0];
                    }
                    finally { upperArm.enabled = true; }
                    int visibleArmPixels = withArm.Zip(withoutArm, (shown, hidden) =>
                        Mathf.Max(Mathf.Abs(shown.r - hidden.r), Mathf.Abs(shown.g - hidden.g), Mathf.Abs(shown.b - hidden.b)) > 12 ? 1 : 0).Sum();
                    Assert.That(visibleArmPixels, Is.GreaterThan(withArm.Length * .15f),
                        side + " upper arm contributes only " + visibleArmPixels + "/" + withArm.Length +
                        " rendered pixels between its shoulder and elbow at progress " + progress +
                        ". The body/hair or another layer must not hide this entire segment.");
                }
            }
        }

        [TestCase(3)] [TestCase(4)]
        public void FrontHairOccludesMovingArmsAccordingToItsAuthoredOpacity(int rank)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to detect sleeves drawn in front of the front hair.");
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            Render(duration * .23f);
            Image[] images = RigRoot().GetComponentsInChildren<Image>(true);
            Image bodyImage = images.Single(i => i.name == "FusionIona");
            Image foreground = images.Single(i => i.name == "FusionIonaForeground");
            Image[] arms = images.Where(i => i != bodyImage && i != foreground).ToArray();
            Assert.That(arms.Length, Is.EqualTo(4));
            foreach (Image arm in arms)
            {
                Assert.That(bodyImage.transform.GetSiblingIndex(), Is.LessThan(arm.transform.GetSiblingIndex()));
                Assert.That(foreground.transform.GetSiblingIndex(), Is.GreaterThan(arm.transform.GetSiblingIndex()),
                    "Moving the whole sleeve in front of the fixed drawing makes it cross in front of Iona's chest hair.");
            }
            // These regions include the long front locks where they cross the
            // sleeve sweep. The near-opaque foreground mask excludes background
            // and translucent edge pixels, so an unobscured arm elsewhere cannot
            // cause this check to fail or make an occluded lock pass. The image2
            // source uses alpha 253 for most hair interiors, not 255. Requiring
            // alpha >= 254 would incorrectly discard almost the entire lock.
            Rect[] regions = { new Rect(395, 335, 42, 140), new Rect(565, 335, 40, 140) };
            Color32[][] reference = null;
            foreach (float progress in new[] { .23f, .30f, .37f, .44f, .48f })
            {
                Render(duration * progress);
                Color32[][] withArms = CaptureRigRegions(regions);
                Color32[][] withoutArms, foregroundMask;
                try
                {
                    foreach (Image arm in arms) arm.enabled = false;
                    withoutArms = CaptureRigRegions(regions);
                    bodyImage.enabled = false;
                    foregroundMask = CaptureRigRegions(regions, true);
                }
                finally
                {
                    bodyImage.enabled = true;
                    foreach (Image arm in arms) arm.enabled = true;
                }
                Color32[][] sleevesOnTop = null;
                if (reference == null)
                {
                    // Negative control: recreate the previous wrong depth for
                    // one capture, then restore it. The selected lock pixels
                    // must actually expose that regression, not merely pass
                    // because this fixture misses the moving sleeves.
                    int originalOrder = foreground.transform.GetSiblingIndex();
                    try
                    {
                        foreground.transform.SetSiblingIndex(bodyImage.transform.GetSiblingIndex() + 1);
                        sleevesOnTop = CaptureRigRegions(regions);
                    }
                    finally { foreground.transform.SetSiblingIndex(originalOrder); }
                }
                for (int region = 0; region < regions.Length; region++)
                {
                    int nearOpaquePixels = 0, occludedPixels = 0, changedPixels = 0, exposedWrongDepthPixels = 0;
                    for (int index = 0; index < foregroundMask[region].Length; index++)
                    {
                        Color32 maskPixel = foregroundMask[region][index];
                        // The front layer also contains the dark bodice. Only
                        // select the bright lilac/white hair, not black cloth
                        // or gold trim that would hide a missing hair mask.
                        // A transparent render target can preserve alpha or
                        // multiply it again during blending (253 * 253 / 255
                        // is 251). Both describe the authored near-opaque hair.
                        if (maskPixel.a < 250 || maskPixel.r + maskPixel.g + maskPixel.b < 420
                            || maskPixel.b < maskPixel.r || maskPixel.b < maskPixel.g) continue;
                        nearOpaquePixels++;
                        // For correct front-over-back compositing, changing any
                        // underlying color can change an RGB channel by at most
                        // 255 * (1 - alpha). One extra level allows readback
                        // rounding. A sleeve painted over the lock exceeds this
                        // bound; its faint contribution through authored alpha
                        // 253 does not. Never make the original art more opaque
                        // just to satisfy an exact-RGB regression assertion.
                        int allowedColorDifference = 255 - maskPixel.a + 1;
                        if (MaxRgbDifference(withArms[region][index], withoutArms[region][index]) > allowedColorDifference) occludedPixels++;
                        if (reference != null && MaxRgbDifference(withArms[region][index], reference[region][index]) > allowedColorDifference) changedPixels++;
                        if (sleevesOnTop != null && MaxRgbDifference(withArms[region][index], sleevesOnTop[region][index]) > allowedColorDifference) exposedWrongDepthPixels++;
                    }
                    Assert.That(nearOpaquePixels, Is.GreaterThan(100), "The fixture must contain a visible front hair lock, not only transparent padding.");
                    if (sleevesOnTop != null)
                        Assert.That(exposedWrongDepthPixels, Is.GreaterThan(20), "The fixture must detect sleeves placed over the front lock, as in the previous regression.");
                    Assert.That(occludedPixels, Is.Zero, "Arm art covers " + occludedPixels + " front-hair pixels in region " + region + " at progress " + progress + ".");
                    Assert.That(changedPixels, Is.Zero, "The front hair must remain fixed; only the bounded contribution through its authored opacity may change.");
                }
                if (reference == null) reference = withArms;
            }
        }

        [TestCase(3)] [TestCase(4)]
        public void PalmsMoveContinuouslyWithoutReverseStepsAtSixtyAndOneHundredTwentyFps(int rank)
        {
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            Render(0f);
            object rig = presentation.GetType().GetField("iona", All).GetValue(presentation);
            int lastFrame = Mathf.RoundToInt(duration * 120f);
            var leftAt120 = new Vector2[lastFrame + 1];
            var rightAt120 = new Vector2[lastFrame + 1];
            for (int frame = 0; frame <= lastFrame; frame++)
            {
                Render(frame / 120f);
                leftAt120[frame] = (Vector2)Call(rig, "GetPalmInStage", true);
                rightAt120[frame] = (Vector2)Call(rig, "GetPalmInStage", false);
                if (frame == 0) continue;
                Assert.That(leftAt120[frame].x, Is.GreaterThanOrEqualTo(leftAt120[frame - 1].x - .001f), "The left hand must not reopen between closing poses.");
                Assert.That(rightAt120[frame].x, Is.LessThanOrEqualTo(rightAt120[frame - 1].x + .001f), "The right hand must not reopen between closing poses.");
                Assert.That(leftAt120[frame].y, Is.GreaterThanOrEqualTo(leftAt120[frame - 1].y - .001f));
                Assert.That(rightAt120[frame].y, Is.GreaterThanOrEqualTo(rightAt120[frame - 1].y - .001f));
                Assert.That(Vector2.Distance(leftAt120[frame], leftAt120[frame - 1]), Is.LessThan(1f), "A per-pose jump would still look like trembling.");
                Assert.That(Vector2.Distance(rightAt120[frame], rightAt120[frame - 1]), Is.LessThan(1f));
            }
            Assert.That(Vector2.Distance(leftAt120[0], leftAt120[lastFrame]), Is.GreaterThan(15f), "Do not solve jitter by removing the hand gesture.");
            Assert.That(Vector2.Distance(rightAt120[0], rightAt120[lastFrame]), Is.GreaterThan(15f));
            // Sample in reverse order as well: the pose must depend on absolute time,
            // not accumulated frame steps or the most recently displayed frame.
            for (int frame = lastFrame / 2; frame >= 0; frame--)
            {
                Render(frame / 60f);
                Assert.That(Vector2.Distance((Vector2)Call(rig, "GetPalmInStage", true), leftAt120[frame * 2]), Is.LessThan(.001f));
                Assert.That(Vector2.Distance((Vector2)Call(rig, "GetPalmInStage", false), rightAt120[frame * 2]), Is.LessThan(.001f));
            }
        }

        [TestCase(3)] [TestCase(4)]
        public void RenderedFaceBodiceAndBootPixelsDoNotChangeDuringTheHandGesture(int rank)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required for the real rendered-pixel regression check.");
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            // Native-art regions: face, central bodice, and boots. These are
            // outside the arm sweep, so every rendered pixel must remain fixed.
            Rect[] regions = { new Rect(445, 190, 110, 75), new Rect(465, 360, 75, 150), new Rect(440, 1020, 115, 180) };
            Color32[][] reference = null;
            foreach (float progress in new[] { .23f, .30f, .37f, .44f, .48f })
            {
                Render(duration * progress);
                Color32[][] pixels = CaptureRigRegions(regions);
                if (reference == null) { reference = pixels; continue; }
                for (int region = 0; region < regions.Length; region++)
                {
                    Assert.That(pixels[region].Length, Is.EqualTo(reference[region].Length));
                    int changedPixels = reference[region].Zip(pixels[region], (before, after) => before.Equals(after) ? 0 : 1).Sum();
                    Assert.That(changedPixels, Is.Zero, "Face/body/boot region " + region + " changed at progress " + progress + ". Stable transforms alone cannot detect redrawn-body jitter.");
                }
            }
        }

        [TestCase(3)] [TestCase(4)]
        public void ScrubbingBackwardsReconstructsTheSameFrameWithoutMutatingFixedResult(int rank)
        {
            Set(born, "classRank", rank);
            string ownedBefore = JsonUtility.ToJson(created), dataBefore = JsonUtility.ToJson(born);
            var random = UnityEngine.Random.state;
            Render(1.6f); string initial = VisualState();
            Render(rank == 4 ? 8.5f : 6.6f);
            Render(1.6f);
            Assert.That(VisualState(), Is.EqualTo(initial), "Reviewing a previous frame must reset later cut-ins, burst and result layers.");
            Assert.That(JsonUtility.ToJson(created), Is.EqualTo(ownedBefore), "Presentation cannot change the already-created owned monster.");
            Assert.That(JsonUtility.ToJson(born), Is.EqualTo(dataBefore), "Master data is read-only.");
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random), "Visual playback must not consume gameplay RNG.");
        }

        [TestCase(3)] [TestCase(4)]
        public void CharacterArtUsesUniformScalingThroughoutTheTimeline(int rank)
        {
            Set(born, "classRank", rank);
            float duration = (float)Static("DurationForClass", rank);
            for (int frame = 0; frame <= 180; frame++)
            {
                Render(duration * frame / 180f);
                if (rank < 4)
                    Assert.That(canvas.GetComponentsInChildren<Image>(true).Any(i => i.name == "FusionIonaCutIn" && Visible(i)), Is.False, "Class 3 must never play the upper-only cut-in.");
                foreach (Image figure in canvas.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("FusionIona", StringComparison.Ordinal) || i.name == "FusionBornMonster"))
                {
                    Assert.That(figure.preserveAspect, Is.True, figure.name + " should not stretch the drawing.");
                    var scale = figure.rectTransform.localScale;
                    Assert.That(Mathf.Abs(scale.x), Is.EqualTo(Mathf.Abs(scale.y)).Within(.0001f), figure.name + " stretched during frame " + frame);
                    Assert.That(float.IsNaN(scale.x) || float.IsInfinity(scale.x), Is.False);
                }
            }
        }

        [TestCase("monster_drag_gaia")]
        [TestCase("monster_abyss_dragon")]
        public void BornMonsterKeepsItsDrawingAndVisibleSizeAcrossTheFinalHold(string monsterId)
        {
            UseRealResult(monsterId);
            Sprite[] attacks = AttackFrames();
            Assert.That(attacks.Length, Is.EqualTo(4), "These regression fixtures have four authored attack drawings.");
            float duration = (float)Static("DurationForClass", (int)born.GetType().GetField("classRank").GetValue(born));
            Render(duration * .78f);
            var image = canvas.GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionBornMonster");
            Assert.That(image.sprite, Is.SameAs(attacks[0]), "The reveal must start in the same drawing sequence, not switch from differently framed portrait art.");

            Render(duration * .89f);
            Sprite beforeSprite = image.sprite;
            Vector2 beforeSize = image.rectTransform.sizeDelta;
            Rect beforeOpaque = VisibleDrawingBounds(image);
            Capture(monsterId + "-before-hold");
            Render(duration * .92f);
            Rect afterOpaque = VisibleDrawingBounds(image);
            Assert.That(image.sprite, Is.SameAs(beforeSprite), "Entering the hold must not replace the attack pose with a smaller battle idle drawing.");
            Assert.That(image.sprite, Is.SameAs(attacks.Last()));
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(beforeSize));
            Assert.That(afterOpaque.width / beforeOpaque.width, Is.EqualTo(1f).Within(.001f));
            Assert.That(afterOpaque.height / beforeOpaque.height, Is.EqualTo(1f).Within(.001f));
            Assert.That(Vector2.Distance(beforeOpaque.center, afterOpaque.center), Is.LessThan(2f), "The slight arrival movement may finish, but the body must not jump at the hold boundary.");
            Capture(monsterId + "-after-hold");
            Render(duration);
            Assert.That(image.sprite, Is.SameAs(attacks.Last()));
            Assert.That(VisibleDrawingBounds(image).size, Is.EqualTo(afterOpaque.size));
        }

        [TestCase("monster_drag_gaia")]
        [TestCase("monster_abyss_dragon")]
        public void CompleteBirthSequenceFitsOneStableSixHundredEightyPixelContentBox(string monsterId)
        {
            UseRealResult(monsterId);
            Render(0f); // Register the entire real sequence before sampling its drawings.
            var image = canvas.GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionBornMonster");
            Sprite[] attacks = AttackFrames();
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            Vector2? previousOrigin = null;
            float? previousPixelScale = null;
            foreach (Sprite frame in attacks)
            {
                image.sprite = frame;
                Call(presentation, "PoseBorn", image, Vector2.zero, 1f, 1f);
                Rect opaque = VisibleDrawingBounds(image);
                min = Vector2.Min(min, opaque.min); max = Vector2.Max(max, opaque.max);
                if (previousOrigin.HasValue)
                    Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(previousOrigin.Value), "Extending a wing must not recenter the torso on every drawing.");
                float pixelScale = image.rectTransform.rect.width / frame.rect.width;
                if (previousPixelScale.HasValue)
                    Assert.That(pixelScale, Is.EqualTo(previousPixelScale.Value).Within(.0001f), "A pose must not be resized independently to fill the result box.");
                previousOrigin = image.rectTransform.anchoredPosition;
                previousPixelScale = pixelScale;
                Assert.That(opaque.xMin, Is.GreaterThanOrEqualTo(-340.1f));
                Assert.That(opaque.yMin, Is.GreaterThanOrEqualTo(-340.1f));
                Assert.That(opaque.xMax, Is.LessThanOrEqualTo(340.1f));
                Assert.That(opaque.yMax, Is.LessThanOrEqualTo(340.1f));
            }
            Assert.That(Mathf.Max(max.x - min.x, max.y - min.y), Is.EqualTo(680f).Within(.1f), "Transparent padding must not shrink the entire creature.");
            Assert.That(((min + max) * .5f).magnitude, Is.LessThan(.1f));
        }

        [TestCase(3)] [TestCase(4)]
        public void RepeatedSkipEndsTheExistingSequenceWithoutChangingTheGrantedResult(int rank)
        {
            Set(born, "classRank", rank);
            string original = JsonUtility.ToJson(created);
            var sequence = (IEnumerator)Call(presentation, "Play", parentA, parentB, born, created);
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(Playing(presentation), Is.True);
            for (int tap = 0; tap < 100; tap++) Call(presentation, "Skip");
            int remaining = 0;
            while (sequence.MoveNext() && ++remaining < 5) { }
            Assert.That(remaining, Is.LessThan(5), "Skip must leave the timeline promptly.");
            Assert.That(Playing(presentation), Is.False);
            Assert.That(JsonUtility.ToJson(created), Is.EqualTo(original));
            Call(presentation, "Skip");
            Assert.That(Playing(presentation), Is.False);
        }

        [Test]
        public void LeavingDuringPlaybackDisposesTheOverlayAndCannotResumeGhostAnimation()
        {
            var sequence = (IEnumerator)Call(presentation, "Play", parentA, parentB, born, created);
            Assert.That(sequence.MoveNext(), Is.True);
            Call(presentation, "Dispose"); Call(presentation, "Dispose"); Call(presentation, "Skip");
            Assert.That(Playing(presentation), Is.False);
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "FusionCinematicRoot" && t.gameObject.activeSelf), Is.False);
            Assert.That(sequence.MoveNext(), Is.False, "A suspended coroutine must stop after its scene closes.");
        }

        [Test]
        public void AppPauseShowsTheAlreadyGrantedResultAndReleasesTheInputLock()
        {
            var panelObject = new GameObject("InterruptedFusionPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvas.transform, false);
            object panel = panelObject.AddComponent(T("Home.MonsterFusionPanelController"));
            Call(panel, "Build");
            object result = Activator.CreateInstance(T("Data.MonsterFusionResult"), new object[] {
                Enum.Parse(T("Data.MonsterFusionStatus"), "Success"), null, born, created, "確認用の仲間 が誕生しました。" });
            Set(panel, "pendingFusionResult", result);
            Set(panel, "pendingFusionParentA", born); Set(panel, "pendingFusionParentB", born);
            Set(panel, "pendingFusionMessage", "確認用の仲間 が誕生しました。");
            Set(panel, "fusionInProgress", true); Set(panel, "fusionCinematic", presentation);
            string before = JsonUtility.ToJson(created);
            Render(2.8f);
            Call(panel, "OnApplicationPause", true);
            Call(panel, "OnApplicationPause", true);
            Call(panel, "OnApplicationPause", false);
            Assert.That(panel.GetType().GetField("fusionInProgress", All).GetValue(panel), Is.False);
            Assert.That(panel.GetType().GetField("fusionCinematic", All).GetValue(panel), Is.Null);
            Assert.That(panel.GetType().GetField("pendingFusionResult", All).GetValue(panel), Is.SameAs(result));
            var resultRoot = (GameObject)panel.GetType().GetField("resultStageRoot", All).GetValue(panel);
            Assert.That(resultRoot.activeSelf, Is.True);
            var resultImage = (Image)panel.GetType().GetField("resultStageBornImage", All).GetValue(panel);
            Assert.That(resultImage.sprite, Is.EqualTo(parentA)); Assert.That(Visible(resultImage), Is.True);
            Assert.That(resultRoot.GetComponentsInChildren<Button>().Any(b => b.interactable), Is.True, "The committed result must remain dismissible.");
            Assert.That(JsonUtility.ToJson(created), Is.EqualTo(before));
        }

        [TestCase(3)] [TestCase(4)]
        public void RitualHasVisibleParentsBeforeCocoonAndAnActualMonsterAtTheEnd(int rank)
        {
            Set(born, "classRank", rank);
            Render(1.6f);
            Assert.That(canvas.GetComponentsInChildren<Image>(true).Any(i => i.sprite == parentA && Visible(i)), Is.True, "Show the selected first parent before combining their light.");
            Assert.That(canvas.GetComponentsInChildren<Image>(true).Any(i => i.sprite == parentB && Visible(i)), Is.True, "Show the selected second parent before combining their light.");
            Capture(rank == 4 ? "class4-parents" : "normal-parents");
            float duration = (float)Static("DurationForClass", rank);
            foreach (float progress in new[] { .43f, .65f, .76f, .86f })
            {
                Render(duration * progress);
                Capture((rank == 4 ? "class4" : "normal") + "-" + Mathf.RoundToInt(progress * 100));
            }
            Render(duration);
            var result = canvas.GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionBornMonster");
            Assert.That(result.sprite, Is.Not.Null); Assert.That(Visible(result), Is.True);
            var cutin = canvas.GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIonaCutIn");
            Assert.That(Visible(cutin), Is.False, "The cut-in must not cover the final monster.");
            Capture(rank == 4 ? "class4-result" : "normal-result");
        }

        private static RigLayout LoadRigLayout()
        {
            var json = Resources.Load<TextAsset>(Art + "IonaRig/layout");
            Assert.That(json, Is.Not.Null, "The joint registration must be shipped with the drawing.");
            return JsonUtility.FromJson<RigLayout>(json.text);
        }
        private static int MaxRgbDifference(Color32 first, Color32 second) =>
            Mathf.Max(Mathf.Abs(first.r - second.r), Mathf.Abs(first.g - second.g), Mathf.Abs(first.b - second.b));
        private RectTransform RigRoot() => canvas.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "FusionIonaRig");
        private static void AssertPaintAt(string part, Vector2 sourcePoint) => AssertPaintAt(new[] { part }, sourcePoint);
        private static void AssertPaintAt(string[] parts, Vector2 sourcePoint)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                float combinedAlpha = 0f;
                foreach (string part in parts)
                {
                    Sprite sprite = Resources.Load<Sprite>(Art + "IonaRig/" + part);
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(sprite)));
                    Assert.That(ImageConversion.LoadImage(decoded, File.ReadAllBytes(path)), Is.True);
                    int x = Mathf.RoundToInt(sourcePoint.x), y = decoded.height - 1 - Mathf.RoundToInt(sourcePoint.y);
                    combinedAlpha = 1f - (1f - combinedAlpha) * (1f - decoded.GetPixel(x, y).a);
                }
                Assert.That(combinedAlpha, Is.GreaterThan(.75f),
                    string.Join("/", parts) + " has no visible art at its registered joint/palm. A correct transform with an incorrectly drawn pivot still looks detached.");
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }
        private Color32[][] CaptureRigRegions(Rect[] sourceRegions, bool transparentBackground = false)
        {
            RectTransform rigRoot = RigRoot();
            Image bodyImage = rigRoot.GetComponentsInChildren<Image>(true).Single(i => i.name == "FusionIona");
            var unrelatedGraphics = canvas.GetComponentsInChildren<Graphic>(true).Where(g => !g.transform.IsChildOf(rigRoot)).ToArray();
            bool[] previouslyEnabled = unrelatedGraphics.Select(g => g.enabled).ToArray();
            var cameraObject = new GameObject("FusionRigPixelRegressionCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 1170; camera.transform.position = new Vector3(0, 0, -1000);
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = transparentBackground ? Color.clear : Color.black;
            var target = new RenderTexture(540, 1170, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(540, 1170, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            Camera previousCamera = canvas.GetComponent<Canvas>().worldCamera;
            try
            {
                foreach (var graphic in unrelatedGraphics) graphic.enabled = false;
                camera.targetTexture = target; canvas.GetComponent<Canvas>().worldCamera = camera;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 540, 1170), 0, 0); texture.Apply();
                Color32[] allPixels = texture.GetPixels32();
                var result = new Color32[sourceRegions.Length][];
                for (int regionIndex = 0; regionIndex < sourceRegions.Length; regionIndex++)
                {
                    Rect region = sourceRegions[regionIndex];
                    Vector2 artSize = bodyImage.sprite.rect.size;
                    Vector3 min = camera.WorldToScreenPoint(bodyImage.rectTransform.TransformPoint(new Vector3(region.xMin - artSize.x * .5f, artSize.y * .5f - region.yMax, 0f)));
                    Vector3 max = camera.WorldToScreenPoint(bodyImage.rectTransform.TransformPoint(new Vector3(region.xMax - artSize.x * .5f, artSize.y * .5f - region.yMin, 0f)));
                    int left = Mathf.CeilToInt(min.x), bottom = Mathf.CeilToInt(min.y);
                    int width = Mathf.FloorToInt(max.x) - left, height = Mathf.FloorToInt(max.y) - bottom;
                    Assert.That(left, Is.GreaterThanOrEqualTo(0)); Assert.That(bottom, Is.GreaterThanOrEqualTo(0));
                    Assert.That(left + width, Is.LessThanOrEqualTo(texture.width)); Assert.That(bottom + height, Is.LessThanOrEqualTo(texture.height));
                    Assert.That(width * height, Is.GreaterThan(100), "The pixel fixture must cover a meaningful visible part of Iona.");
                    var pixels = new Color32[width * height];
                    for (int y = 0; y < height; y++)
                        Array.Copy(allPixels, (bottom + y) * texture.width + left, pixels, y * width, width);
                    Assert.That(pixels.Count(pixel => pixel.r + pixel.g + pixel.b > 24), Is.GreaterThan(100), "An empty camera capture cannot prove that the drawing is stable.");
                    result[regionIndex] = pixels;
                }
                return result;
            }
            finally
            {
                for (int index = 0; index < unrelatedGraphics.Length; index++) unrelatedGraphics[index].enabled = previouslyEnabled[index];
                RenderTexture.active = previousTarget; canvas.GetComponent<Canvas>().worldCamera = previousCamera; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
        private void Render(float elapsed) => Call(presentation, "RenderPreview", parentA, parentB, born, created, elapsed);
        private static bool Visible(Graphic image) => image.gameObject.activeInHierarchy && image.enabled && image.color.a > .1f && image.GetComponentsInParent<CanvasGroup>().All(g => g.alpha > .1f);
        private string VisualState()
        {
            return string.Join("\n", canvas.GetComponentsInChildren<Image>(true).Select(image =>
                image.name + ":" + image.gameObject.activeSelf + ":" + image.color + ":" + image.rectTransform.anchoredPosition + ":" + image.rectTransform.localScale + ":" + image.rectTransform.localEulerAngles + ":" + image.sprite?.name));
        }
        private static Sprite Portrait(string id)
        {
            object data = MonsterData(id);
            Sprite sprite = (Sprite)data.GetType().GetField("illustrationSprite").GetValue(data) ?? (Sprite)data.GetType().GetField("portraitSprite").GetValue(data);
            if (sprite != null) return sprite;
            string path = (string)data.GetType().GetField("illustrationResourcePath").GetValue(data);
            if (string.IsNullOrEmpty(path)) path = (string)data.GetType().GetField("portraitResourcePath").GetValue(data);
            return Resources.Load<Sprite>(path);
        }
        private static object MonsterData(string id)
        {
            object master = Resources.Load("MasterData/MasterDataRoot", T("MasterData.MasterDataRoot"));
            var list = (IEnumerable)master.GetType().GetField("monsterDataList").GetValue(master);
            return list.Cast<object>().First(m => m != null && (string)m.GetType().GetField("monsterId").GetValue(m) == id);
        }
        private void UseRealResult(string monsterId)
        {
            UnityEngine.Object.DestroyImmediate(born);
            born = UnityEngine.Object.Instantiate((ScriptableObject)MonsterData(monsterId));
            Set(created, "MonsterId", monsterId);
        }
        private Sprite[] AttackFrames() => ((IEnumerable)T("Battle.BattleVisualResolver").GetMethod("ResolveMonsterAttackSprites").Invoke(null, new object[] { born })).Cast<Sprite>().ToArray();
        private static Rect VisibleDrawingBounds(Image image)
        {
            Sprite sprite = image.sprite;
            Assert.That(sprite.texture.isReadable, Is.True, "Runtime sizing requires readable alpha data for this authored sequence.");
            Color32[] pixels = sprite.texture.GetPixels32();
            int width = Mathf.RoundToInt(sprite.rect.width), height = Mathf.RoundToInt(sprite.rect.height);
            int offsetX = Mathf.RoundToInt(sprite.rect.x), offsetY = Mathf.RoundToInt(sprite.rect.y);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(offsetY + y) * sprite.texture.width + offsetX + x].a <= 8) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
            Assert.That(maxX, Is.GreaterThanOrEqualTo(0));
            RectTransform rect = image.rectTransform;
            Vector2 size = Vector2.Scale(rect.rect.size, new Vector2(rect.localScale.x, rect.localScale.y));
            Vector2 origin = rect.anchoredPosition - Vector2.Scale(rect.pivot, size);
            return Rect.MinMaxRect(origin.x + minX / (float)width * size.x, origin.y + minY / (float)height * size.y,
                origin.x + (maxX + 1) / (float)width * size.x, origin.y + (maxY + 1) / (float)height * size.y);
        }
        private void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("WITCHTOWER_FUSION_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("FusionTestCaptureCamera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 1170; camera.transform.position = new Vector3(0, 0, -1000);
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(540, 1170, 24);
            var texture = new Texture2D(540, 1170, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; canvas.GetComponent<Canvas>().worldCamera = camera;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 540, 1170), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; canvas.GetComponent<Canvas>().worldCamera = null; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
