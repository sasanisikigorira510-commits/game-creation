using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class TenPullPresentationTests
    {
        private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
        private const string JournalKey = "witchtower_pending_ten_pull_presentation_v1";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Home." + name, true);
        private static Type Preview => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("TenPullPreviewWindow", true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Public).Invoke(target, args);
        private static object P(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object F(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static Array Results(int pattern) => (Array)Preview.GetMethod("CreateResults").Invoke(null, new object[] { pattern });
        private ScriptableObject settings;
        private UnityEngine.Random.State random;
        [SetUp] public void Setup() { random = UnityEngine.Random.state; settings = ScriptableObject.CreateInstance(T("TenPullPresentationSettings")); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(settings); UnityEngine.Random.state = random; }
        private object Sequence(Array results) => Activator.CreateInstance(T("TenPullSequence"), new object[] { results, settings });

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SingleSummonRunsTheFilmOneStoneAndOnlyClassFourCutIn(int rank)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("SingleCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080,2341);
                var panel = new GameObject("SingleTest", typeof(RectTransform)); panel.transform.SetParent(root.transform,false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                object result = Results(rank == 4 ? 4 : 0).Cast<object>().First(x => (int)F(x,"ClassRank") == rank);
                var input = Array.CreateInstance(T("SummonPresentationResult"),1); input.SetValue(result,0);
                var resolver = (Func<string,Sprite>)(id => (Sprite)Preview.GetMethod("ResolvePortrait").Invoke(null,new object[]{id}));
                int completed = 0;
                Call(controller,"Present",input,resolver,(Action)(()=>completed++),null,null,true,false,settings);
                object sequence = P(controller,"Sequence");
                var safe = panel.transform.Find("TenPullSafeContent");
                Assert.That(P(sequence,"Count"),Is.EqualTo(1));
                Assert.That(P(sequence,"HasClass4"),Is.EqualTo(rank==4));
                Assert.That(P(sequence,"Phase").ToString(),Is.EqualTo("Introduction"));
                Assert.That(safe.Find("AnimeFilm").gameObject.activeSelf,Is.True);
                Assert.That(safe.Find("StoneSlot_0").gameObject.activeSelf,Is.False);
                var phases = new HashSet<string>();
                var captured = new HashSet<string>();
                for(int tick=0;tick<400 && !(bool)P(sequence,"IsComplete");tick++)
                {
                    Call(controller,"TickPresentation",.04f);
                    string phase=P(sequence,"Phase").ToString(); phases.Add(phase);
                    for(int i=1;i<10;i++) Assert.That(safe.Find("StoneSlot_"+i).gameObject.activeSelf,Is.False);
                    if(phase=="Materialization" && (float)P(sequence,"Elapsed")>.2f)
                        Assert.That(safe.Find("StoneSlot_0").gameObject.activeSelf,Is.True);
                    if((rank==1 || rank==4) && (float)P(sequence,"Elapsed")>(float)P(sequence,"Duration")*.55f && captured.Add(phase))
                        Capture(canvas,540,1170,"single-"+rank+"-"+phase);
                }
                Assert.That(P(sequence,"IsComplete"),Is.True);
                Assert.That(phases.Contains("CutIn"),Is.EqualTo(rank==4));
                foreach(string phase in new[]{"Materialization","Focus","Crack","Burst","Reveal","Summary"})
                    Assert.That(phases.Contains(phase),Is.True,phase);
                Assert.That(completed,Is.EqualTo(1));
                Assert.That(safe.Find("FocalReveal/RevealedMonster").gameObject.activeInHierarchy,Is.True);
                Assert.That(safe.Find("MonsterName").GetComponent<Text>().text,Is.EqualTo(F(result,"DisplayName")));
                Assert.That(safe.Find("BackHome").gameObject.activeSelf,Is.True);
                Assert.That(safe.Find("StoneSlot_0").gameObject.activeSelf,Is.False,"Single results retain the large portrait, not a ten-slot grid.");
                if(rank==1 || rank==4) Capture(canvas,540,1170,"single-"+rank+"-Summary");
                for(int i=0;i<30;i++) Call(controller,"Skip");
                Assert.That(completed,Is.EqualTo(1));
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random),"Presentation cannot redraw any result.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(3, 1)] [TestCase(6, 0)]
        public void IntroductionUsesAllConfirmedClassesBeforeAnyStoneAppears(int scenario, int class4Count)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("IntroductionCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("IntroTest", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                var input = Results(scenario);
                Call(controller, "Present", input, (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                Canvas.ForceUpdateCanvases();
                object sequence = P(controller, "Sequence");
                Assert.That(P(sequence, "Class4Count"), Is.EqualTo(class4Count));
                Assert.That(P(sequence, "HasClass4"), Is.EqualTo(class4Count > 0));
                foreach (var item in input) item.GetType().GetField("ClassRank").SetValue(item, 4);
                Assert.That(P(sequence, "Class4Count"), Is.EqualTo(class4Count), "External mutation cannot alter the chosen introduction.");
                var safe = panel.transform.Find("TenPullSafeContent");
                bool captured = false;
                var animationFrames = new HashSet<Sprite>();
                for (int tick = 0; tick < 100 && P(sequence, "Phase").ToString() == "Introduction"; tick++)
                {
                    Assert.That(safe.Find("SummoningConvergence").gameObject.activeSelf, Is.False, "Opaque anime cels suppress covered effects.");
                    Assert.That(safe.Find("SummoningConvergence/AwakeningSeal").GetComponent<Image>().sprite, Is.Not.Null);
                    Assert.That(safe.Find("FocalReveal").gameObject.activeSelf, Is.False);
                    Assert.That(safe.Find("CinematicStage").gameObject.activeSelf, Is.False);
                    animationFrames.Add(safe.Find("CinematicStage/LivingVortex").GetComponent<Image>().sprite);
                    for (int i = 0; i < 10; i++) Assert.That(safe.Find("StoneSlot_" + i).gameObject.activeSelf, Is.False);
                    if ((float)P(sequence, "Elapsed") > (float)P(sequence, "Duration") * 0.7f && !captured)
                    {
                        Assert.That(safe.Find("SummoningConvergence/Class4OuterSeal").gameObject.activeSelf, Is.EqualTo(class4Count > 0));
                        Capture(canvas, 1080, 2341, "convergence_s" + scenario); captured = true;
                    }
                    for (int tap = 0; tap < 20; tap++) Call(sequence, "Advance");
                    Call(controller, "TickPresentation", 0.05f);
                }
                Assert.That(captured, Is.True);
                Assert.That(animationFrames.Count, Is.GreaterThanOrEqualTo(6), "The charge uses actual flipbook poses, not only a scaled still.");
                Assert.That(animationFrames.Contains(null), Is.False);
                Assert.That(panel.transform.Find("SummonChamber").localScale.x, Is.GreaterThan(1.05f));
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Materialization"));
                Call(controller, "TickPresentation", 0.05f);
                Assert.That(safe.Find("StoneSlot_0").gameObject.activeSelf, Is.True);
                Assert.That(safe.Find("StoneSlot_9").gameObject.activeSelf, Is.False);
                for (int tick = 0; tick < 12; tick++) Call(controller, "TickPresentation", 0.05f);
                Capture(canvas, 1080, 2341, "arriving_s" + scenario);
                for (int tick = 0; tick < 13; tick++) Call(controller, "TickPresentation", 0.05f);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Materialization"));
                for (int i = 0; i < 10; i++) Assert.That(safe.Find("StoneSlot_" + i).gameObject.activeSelf, Is.True);
                Capture(canvas, 1080, 2341, "stones_s" + scenario);
                Call(controller, "StopAndHide"); panel.SetActive(true);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"));
                Assert.That(safe.Find("SummoningConvergence").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("CinematicStage").gameObject.activeSelf, Is.False);
                Assert.That(panel.GetComponentsInChildren<Image>().Count(x => x.name == "Portrait"), Is.EqualTo(10));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void CutInCanBeDisabledButCannotBeEnabledForAnyOtherClass()
        {
            foreach (int rank in new[] { 1, 2, 3, 5 }) Assert.That(Call(settings, "HasCutIn", rank), Is.False);
            Assert.That(Call(settings, "HasCutIn", 4), Is.True);
            settings.GetType().GetField("Class4CutInEnabled").SetValue(settings, false);
            Assert.That(Call(settings, "HasCutIn", 4), Is.False);
            var sequence = Sequence(Results(2)); int cuts = 0;
            sequence.GetType().GetEvent("PhaseChanged").AddEventHandler(sequence,
                (Action)(() => { if (P(sequence, "Phase").ToString() == "CutIn") cuts++; }));
            for (int i = 0; i < 1000 && !(bool)P(sequence, "IsComplete"); i++) Call(sequence, "Tick", 0.1f);
            Assert.That((bool)P(sequence, "IsComplete"), Is.True);
            Assert.That(cuts, Is.Zero);
        }

        private static void ObserveCues<TCue>(Component controller, Action<int, float> observe)
        {
            Action<TCue, float> handler = (cue, volume) => observe(Convert.ToInt32(cue), volume);
            controller.GetType().GetEvent("SoundCued").AddEventHandler(controller, handler);
        }

        [TestCase(0, false)] [TestCase(1, true)] [TestCase(3, true)] [TestCase(6, false)]
        public void AnimeKeepsRegisteredArtworkAndChoosesOnlyTheConfirmedBranch(int scenario, bool upper)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("AnimeCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("AnimeTest", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                Call(controller, "Present", Results(scenario), (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                var sequence = P(controller, "Sequence");
                var safe = panel.transform.Find("TenPullSafeContent");
                var film = safe.Find("AnimeFilm").GetComponent<Image>();
                var opening = film.transform.Find("OpeningShot");
                var casting = film.transform.Find("CastingShot");
                var drawings = film.GetComponentsInChildren<Image>(true).Where(x => x != film).ToDictionary(x => x, x => x.sprite);
                var dimensions = drawings.Keys.ToDictionary(x => x, x => x.rectTransform.sizeDelta);
                var bodyLandmarks = new Dictionary<Transform, Vector3[]>();
                var previousLandmarks = new Dictionary<Transform, Vector3[]>();
                var seenShots = new HashSet<string>();
                var captured = new HashSet<int>();
                Transform previousShot = null;
                Canvas.ForceUpdateCanvases();
                int introTicks = Mathf.CeilToInt((float)P(sequence, "Duration") / 0.02f) + 1;
                for (int tick = 0; tick < introTicks && P(sequence, "Phase").ToString() == "Introduction"; tick++)
                {
                    Assert.That(film.gameObject.activeSelf, Is.True);
                    Assert.That(film.raycastTarget, Is.False);
                    Assert.That(safe.Find("Skip").GetSiblingIndex(), Is.GreaterThan(film.transform.GetSiblingIndex()));
                    Assert.That(safe.Find("StoneSlot_0").gameObject.activeSelf, Is.False);
                    var shot = opening.gameObject.activeSelf ? opening : casting.gameObject.activeSelf ? casting : null;
                    foreach (var pair in drawings)
                    {
                        Assert.That(pair.Key.sprite, Is.SameAs(pair.Value), pair.Key.name + " must not switch to an unrelated drawing between cuts.");
                        Assert.That(pair.Key.rectTransform.sizeDelta, Is.EqualTo(dimensions[pair.Key]), pair.Key.name + " must retain its registered proportions.");
                    }
                    foreach (var rect in film.GetComponentsInChildren<RectTransform>(true))
                        Assert.That(rect.localScale.x, Is.EqualTo(rect.localScale.y).Within(0.0001f), rect.name + " must not stretch.");
                    foreach (var image in film.GetComponentsInChildren<Image>(true))
                        Assert.That(image.material.HasProperty("_Motion"), Is.False, "Authored artwork must not use optical-flow deformation.");
                    int shotIndex;
                    if (shot != null)
                    {
                        Assert.That(film.enabled, Is.False, "The opaque full-frame image must yield to the registered layers.");
                        seenShots.Add(shot.name);
                        shotIndex = shot == opening ? 0 : 2;
                        var actor = shot.Find("Camera/Actor");
                        var body = actor.Cast<Transform>().Single(x => x.name.Contains("Body"));
                        var arm = actor.Cast<Transform>().Single(x => x.name.Contains("Arm"));
                        var magic = arm.Find("AttachedMagic");
                        Assert.That(magic, Is.Not.Null, "Magic must follow the hand or staff, not slide independently across the screen.");
                        Assert.That(magic.Find("Class4Seal").gameObject.activeSelf, Is.EqualTo(upper));
                        var rect = (RectTransform)body;
                        Vector3[] localPoints = { Vector3.zero, new Vector3(rect.rect.xMin, rect.rect.yMax, 0), new Vector3(rect.rect.xMax, rect.rect.yMax, 0) };
                        var pointsInActor = localPoints.Select(p => actor.InverseTransformPoint(body.TransformPoint(p))).ToArray();
                        if (!bodyLandmarks.ContainsKey(body)) bodyLandmarks[body] = pointsInActor;
                        for (int i = 0; i < localPoints.Length; i++)
                            Assert.That(Vector3.Distance(pointsInActor[i], bodyLandmarks[body][i]), Is.LessThan(0.001f), "Face/body landmarks must stay registered to the same actor.");
                        if (previousShot != shot) previousLandmarks.Clear();
                        foreach (var layer in actor.Cast<Transform>().Where(x => x.GetComponent<Image>() != null))
                        {
                            var layerRect = (RectTransform)layer;
                            var points = new[] { Vector3.zero, new Vector3(layerRect.rect.xMin, layerRect.rect.yMax, 0), new Vector3(layerRect.rect.xMax, layerRect.rect.yMin, 0) }
                                .Select(p => film.rectTransform.InverseTransformPoint(layer.TransformPoint(p))).ToArray();
                            if (previousLandmarks.TryGetValue(layer, out var previous))
                                for (int i = 0; i < points.Length; i++)
                                    Assert.That(Vector3.Distance(points[i], previous[i]), Is.LessThan(8f), "A continuous shot must not jump its landmarks between refreshes.");
                            previousLandmarks[layer] = points;
                        }
                    }
                    else
                    {
                        shotIndex = 1;
                        seenShots.Add("Eye");
                        Assert.That(film.enabled, Is.True);
                        Assert.That(film.sprite, Is.Not.Null);
                        Assert.That(film.sprite.name, Is.EqualTo(upper ? "Upper_3" : "Normal_3"), "The eye insert uses one fixed registered face per confirmed branch.");
                    }
                    previousShot = shot;
                    if (captured.Add(shotIndex)) Capture(canvas, 750, 1334, "anime_s" + scenario + "_shot" + shotIndex);
                    Call(controller, "TickPresentation", 0.02f);
                }
                CollectionAssert.AreEquivalent(new[] { "OpeningShot", "Eye", "CastingShot" }, seenShots);
                for (int i = 0; i < 3; i++) Call(controller, "TickPresentation", 0.1f);
                Assert.That(film.gameObject.activeSelf, Is.False, "The film must yield to the stone grid.");
                for (int i = 0; i < 100; i++) Call(controller, "Skip");
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"));
                Assert.That(film.gameObject.activeSelf, Is.False);
                Call(controller, "StopAndHide"); panel.SetActive(true);
                Assert.That(film.gameObject.activeSelf, Is.False, "A resumed summary must never restart the film.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0, 0, false)] [TestCase(6, 0, false)] [TestCase(0, 5, false)]
        [TestCase(1, 0, true)] [TestCase(2, 0, true)] [TestCase(3, 0, true)]
        public void GoldRadianceUsesConfirmedClass4ArtworkAtTheEmitterAndStopsOnSkip(int scenario, int overrideRank, bool upper)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("RadianceCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("RadianceTest", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                var input = Results(scenario);
                if (overrideRank != 0)
                    foreach (var result in input) result.GetType().GetField("ClassRank").SetValue(result, overrideRank);
                Call(controller, "Present", input, (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                var sequence = P(controller, "Sequence");
                var film = panel.transform.Find("TenPullSafeContent/AnimeFilm");
                var prefix = (string)F(F(settings, "AnimeRadiance"), "ResourcePrefix");
                var material = Resources.Load<Material>(prefix + "Light");
                Assert.That(material, Is.Not.Null, "The shipped additive light material must be loadable from Resources.");
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("WitchTower/UI/TenPullAdditive"));
                Assert.That(material.shader.isSupported, Is.True, "The light must render with a supported shader, not a missing-shader fallback.");
                var effectRoots = new List<Transform>();
                for (int shotIndex = 0; shotIndex < 2; shotIndex++)
                {
                    // Sample after gold ignition and after the casting release, while each shot is visible.
                    float fraction = shotIndex == 0 ? 0.35f : 0.89f;
                    float target = (float)P(sequence, "Duration") * fraction;
                    for (int tick = 0; tick < 100 && (float)P(sequence, "Elapsed") < target - 0.0001f; tick++)
                        Call(controller, "TickPresentation", Mathf.Min(0.05f, target - (float)P(sequence, "Elapsed")));
                    Canvas.ForceUpdateCanvases();
                    var shot = film.Find(shotIndex == 0 ? "OpeningShot" : "CastingShot");
                    Assert.That(shot.gameObject.activeInHierarchy, Is.True);
                    var camera = shot.Find("Camera");
                    var arm = camera.Find("Actor").Cast<Transform>().Single(x => x.name.Contains("Arm"));
                    var magic = arm.Find("AttachedMagic");
                    var attached = magic.Find("Class4Radiance");
                    var roots = new[] { camera.Find("Class4RadianceBackdrop"), camera.Find("Class4RadianceForeground"), attached };
                    effectRoots.AddRange(roots);
                    foreach (var effectRoot in roots)
                    {
                        Assert.That(effectRoot, Is.Not.Null);
                        Assert.That(effectRoot.gameObject.activeSelf, Is.EqualTo(upper), "Only an exact Class 4 result enables gold lightning, including when the last stone is Class 4.");
                        var images = effectRoot.GetComponentsInChildren<Image>(true);
                        Assert.That(images, Is.Not.Empty);
                        foreach (var image in images)
                        {
                            Assert.That(image.sprite, Is.Not.Null, image.name + " must use the imported effect artwork.");
                            Assert.That(image.enabled, Is.True);
                            Assert.That(image.material, Is.SameAs(material), "All light planes must share the resource material without per-frame allocations.");
                            Assert.That(image.raycastTarget, Is.False, "The new light planes must not intercept skip input.");
                        }
                        if (upper)
                            Assert.That(images.Any(image => image.gameObject.activeInHierarchy && image.color.a > 0.05f), Is.True,
                                effectRoot.name + " must contribute visible light in each layered shot.");
                    }
                    Assert.That(attached.parent, Is.SameAs(magic));
                    Assert.That(Vector3.Distance(attached.position, magic.position), Is.LessThan(0.01f));
                    foreach (var sourceName in new[] { "WhiteHotCore", "ChargeLightning", "ReleaseFan_0", "ReleaseFan_1", "ReleaseFan_2" })
                        Assert.That(Vector3.Distance(attached.Find(sourceName).position, magic.position), Is.LessThan(0.01f),
                            "The new radiance source must remain at the already registered staff tip or palm.");
                }
                for (int tap = 0; tap < 20; tap++) Call(controller, "Skip");
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"));
                foreach (var effectRoot in effectRoots) Assert.That(effectRoot.gameObject.activeInHierarchy, Is.False);
                Call(controller, "StopAndHide"); panel.SetActive(true); Call(controller, "TickPresentation", 0.1f);
                foreach (var effectRoot in effectRoots) Assert.That(effectRoot.gameObject.activeInHierarchy, Is.False,
                    "Returning to the completed result must not leave the additive foreground or attached lightning visible.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void RegisteredArmAndHairMoveWithoutReplacingTheFaceOrUsingCameraPush()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to compare the composited character layers between display refreshes.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("ContinuousMotionCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                settings.GetType().GetField("AnimeCameraPush").SetValue(settings, 0f);
                var panel = new GameObject("ContinuousMotion", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                Call(controller, "Present", Results(0), (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                var sequence = P(controller, "Sequence");
                float openingSeconds = (float)P(sequence, "Duration") * (float)F(settings, "AnimeOpeningFraction");
                int settlingFrames = Mathf.CeilToInt(openingSeconds * 0.4f * 60f);
                for (int frame = 0; frame < settlingFrames; frame++) Call(controller, "TickPresentation", 1f / 60f);
                var film = panel.transform.Find("TenPullSafeContent/AnimeFilm").GetComponent<Image>();
                var actor = film.transform.Find("OpeningShot/Camera/Actor");
                var cameraPose = actor.parent;
                var cameraPosition = cameraPose.localPosition;
                var cameraScale = cameraPose.localScale;
                var body = actor.Cast<Transform>().Single(x => x.name.Contains("Body")).GetComponent<Image>();
                var arm = actor.Cast<Transform>().Single(x => x.name.Contains("Arm"));
                var hair = actor.Cast<Transform>().Single(x => x.name.Contains("Hair"));
                var magic = arm.Find("AttachedMagic");
                var emitterOffset = magic.localPosition;
                var bodySprite = body.sprite;
                float armAngle = arm.localEulerAngles.z, hairAngle = hair.localEulerAngles.z;
                // Isolate the authored character so a moving effect cannot satisfy the pixel test.
                foreach (Transform child in film.transform.parent) if (child != film.transform) child.gameObject.SetActive(false);
                magic.gameObject.SetActive(false);
                var first = ReadRenderedPixels((RectTransform)root.transform);
                Call(controller, "TickPresentation", 1f / 60f);
                foreach (Transform child in film.transform.parent) if (child != film.transform) child.gameObject.SetActive(false);
                magic.gameObject.SetActive(false);
                var next = ReadRenderedPixels((RectTransform)root.transform);
                Assert.That(cameraPose.localPosition, Is.EqualTo(cameraPosition));
                Assert.That(cameraPose.localScale, Is.EqualTo(cameraScale));
                Assert.That(body.sprite, Is.SameAs(bodySprite));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(armAngle, arm.localEulerAngles.z)), Is.GreaterThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(hairAngle, hair.localEulerAngles.z)), Is.GreaterThan(0.001f));
                Assert.That(magic.parent, Is.SameAs(arm));
                Assert.That(magic.localPosition, Is.EqualTo(emitterOffset));
                int changed = 0;
                for (int i = 0; i < first.Length; i++) if (Math.Abs(first[i].r - next[i].r) + Math.Abs(first[i].g - next[i].g) + Math.Abs(first[i].b - next[i].b) > 9) changed++;
                Assert.That(changed, Is.GreaterThan(20), "The persistent character layers must actually animate on the next display refresh, with no camera push or magic overlay.");
                int originalRate = Application.targetFrameRate;
                try
                {
                    Application.targetFrameRate = 30;
                    var acquire = controller.GetType().GetMethod("AcquireFrameRate", BindingFlags.NonPublic | BindingFlags.Instance);
                    acquire.Invoke(controller, null); acquire.Invoke(controller, null);
                    Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                    Call(controller, "Skip"); Call(controller, "Skip");
                    Assert.That(Application.targetFrameRate, Is.EqualTo(30), "Skipping restores the prior frame-rate setting exactly once.");
                }
                finally { Application.targetFrameRate = originalRate; }
                UnityEngine.Object.DestroyImmediate(panel);
                Assert.That(film == null, Is.True, "The film is destroyed with its presentation.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("Opening")] [TestCase("Casting")]
        public void MovingArmStaysOnItsPaintedShoulderAndCarriesItsEmitterAtBothRefreshRates(string shotName)
        {
            bool openingShot = shotName == "Opening";
            // Registration landmarks selected by reviewing the shoulder/sleeve overlap without effects.
            // This guards the reviewed placement, not the artistic correctness of an arbitrary pivot.
            // Image coordinates start at the top-left; RectTransform coordinates start at the bottom-left.
            var bodyShoulderUv = openingShot
                ? new Vector2(265f / 539f, 1f - 232f / 623f)
                : new Vector2(298f / 611f, 1f - 200f / 568f);
            var armShoulderUv = openingShot
                ? new Vector2(162f / 338f, 1f - 273f / 621f)
                : new Vector2(0.875f, 0.86f);
            var emitterUv = openingShot
                ? new Vector2(291.15f / 338f, 1f - 56.20f / 621f)
                : new Vector2(72.10f / 236f, 1f - 68.97f / 258f);
            Vector3 ArtworkPoint(RectTransform rect, Vector2 uv) => new Vector3(
                rect.rect.xMin + rect.rect.width * uv.x,
                rect.rect.yMin + rect.rect.height * uv.y, 0);

            foreach (int rate in new[] { 30, 60 })
            {
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("ShoulderCanvas", typeof(RectTransform), typeof(Canvas));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                    root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    var canvas = (RectTransform)root.transform;
                    canvas.sizeDelta = new Vector2(1080, 2341);
                    // Exercise the complete transform chain, including a non-identity canvas and camera push.
                    canvas.localEulerAngles = new Vector3(0, 0, 7);
                    canvas.localScale = Vector3.one * 0.8f;
                    settings.GetType().GetField("AnimeCameraPush").SetValue(settings, 0.045f);
                    var film = Activator.CreateInstance(T("TenPullAnimeFilm"), new object[] {
                        canvas, settings, (Func<string, Sprite>)(path => Resources.Load<Sprite>(path))
                    });
                    var sequence = Sequence(Results(1));
                    var shot = canvas.Find("AnimeFilm/" + shotName + "Shot");
                    var actor = shot.Find("Camera/Actor");
                    var body = (RectTransform)actor.Cast<Transform>().Single(x => x.name.Contains("Body"));
                    var arm = (RectTransform)actor.Cast<Transform>().Single(x => x.name.Contains("Arm"));
                    var magic = (RectTransform)arm.Find("AttachedMagic");
                    float minAngle = float.PositiveInfinity, maxAngle = float.NegativeInfinity;
                    float minCameraScale = float.PositiveInfinity, maxCameraScale = float.NegativeInfinity;
                    float firstRadius = -1;
                    int samples = 0;
                    for (int frame = 0; frame < rate * 6 && P(sequence, "Phase").ToString() == "Introduction"; frame++)
                    {
                        Call(film, "Render", sequence);
                        Canvas.ForceUpdateCanvases();
                        if (shot.gameObject.activeSelf)
                        {
                            // A body placement adjustment must carry its shoulder immediately. Hardcoded arm
                            // coordinates can coincide in the initial pose but cannot pass this reattachment check.
                            if (samples == 8)
                            {
                                body.anchoredPosition += new Vector2(11, -7);
                                Call(film, "Render", sequence);
                                Canvas.ForceUpdateCanvases();
                            }
                            var shoulder = body.TransformPoint(ArtworkPoint(body, bodyShoulderUv));
                            Assert.That(Vector3.Distance(arm.position, shoulder), Is.LessThan(0.01f),
                                shotName + " at " + rate + " Hz: the moving arm origin must stay on the body shoulder.");
                            Assert.That(Vector3.Distance(arm.TransformPoint(ArtworkPoint(arm, armShoulderUv)), shoulder), Is.LessThan(0.01f),
                                "The pivot must be the shoulder painted into the arm, not the wrist or an empty corner.");
                            Assert.That(magic.parent, Is.SameAs(arm));
                            Assert.That(Vector3.Distance(magic.position, arm.TransformPoint(ArtworkPoint(arm, emitterUv))), Is.LessThan(0.05f),
                                "Re-pivoting must preserve the staff-tip/palm artwork point used by the magic emitter.");

                            var handFromShoulder = body.InverseTransformPoint(magic.position) - ArtworkPoint(body, bodyShoulderUv);
                            if (firstRadius < 0) firstRadius = handFromShoulder.magnitude;
                            Assert.That(handFromShoulder.magnitude, Is.EqualTo(firstRadius).Within(0.01f),
                                "The hand must rotate on a fixed radius about the shoulder without sliding or stretching.");
                            float angle = Mathf.DeltaAngle(body.eulerAngles.z, arm.eulerAngles.z);
                            minAngle = Mathf.Min(minAngle, angle); maxAngle = Mathf.Max(maxAngle, angle);
                            minCameraScale = Mathf.Min(minCameraScale, actor.parent.localScale.x);
                            maxCameraScale = Mathf.Max(maxCameraScale, actor.parent.localScale.x);
                            samples++;
                        }
                        Call(sequence, "Tick", 1f / rate);
                    }
                    Assert.That(samples, Is.GreaterThan(10), "Both the early and late poses must be inspected.");
                    Assert.That(maxAngle - minAngle, Is.GreaterThan(openingShot ? 1f : 0.25f),
                        "A stationary arm cannot satisfy the attachment regression test.");
                    Assert.That(maxCameraScale - minCameraScale, Is.GreaterThan(0.001f),
                        "Attachment must remain valid while the shot camera moves.");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [TestCase("Opening")] [TestCase("Casting")]
        public void ShotMotionIsContinuousAndIndependentOfRefreshRateAndSeekOrder(string shot)
        {
            var method = T("TenPullFilmMotion").GetMethod(shot, BindingFlags.Static | BindingFlags.Public);
            object Sample(float t) => method.Invoke(null, new object[] { t, 0.03f });
            var fields = Sample(0).GetType().GetFields();
            var expected = Enumerable.Range(0, 31).Select(i => Sample(i / 30f)).ToArray();
            foreach (int rate in new[] { 30, 60, 120 })
            {
                object previous = null;
                for (int frame = 0; frame <= rate; frame++)
                {
                    object pose = Sample(frame / (float)rate);
                    foreach (var field in fields)
                    {
                        object value = field.GetValue(pose);
                        if (frame % (rate / 30) == 0)
                            Assert.That(value, Is.EqualTo(field.GetValue(expected[frame / (rate / 30)])), shot + ": " + field.Name + " must depend on time, not refresh count.");
                        if (previous != null)
                        {
                            if (value is float number)
                                Assert.That(Math.Abs(number - (float)field.GetValue(previous)), Is.LessThan(2f), shot + ": " + field.Name + " must not jump inside a shot.");
                            else if (value is Vector2 point)
                                Assert.That(Vector2.Distance(point, (Vector2)field.GetValue(previous)), Is.LessThan(1f), shot + ": " + field.Name + " must remain continuous.");
                        }
                    }
                    previous = pose;
                }
                // Reverse sampling catches accidental accumulation and preview-seek state.
                for (int i = 30; i >= 0; i--)
                    foreach (var field in fields)
                        Assert.That(field.GetValue(Sample(i / 30f)), Is.EqualTo(field.GetValue(expected[i])));
            }
        }

        [Test] public void MissingLayoutUsesOneStableDrawingPerShotAndSkipStillWorks()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                settings.GetType().GetField("AnimeFilmLayoutResource").SetValue(settings, "MissingTestCels/Layout");
                var root = new GameObject("FallbackCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("FallbackFilm", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                Call(controller, "Present", Results(1), (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                var sequence = P(controller, "Sequence");
                var image = panel.transform.Find("TenPullSafeContent/AnimeFilm").GetComponent<Image>();
                var cels = new HashSet<string>();
                float openingEnd = (float)F(settings, "AnimeOpeningFraction");
                int introTicks = Mathf.CeilToInt((float)P(sequence, "Duration") / 0.02f) + 1;
                for (int i = 0; i < introTicks && P(sequence, "Phase").ToString() == "Introduction"; i++)
                {
                    float p = (float)P(sequence, "Elapsed") / (float)P(sequence, "Duration");
                    Assert.That(image.gameObject.activeSelf && image.enabled, Is.True);
                    Assert.That(image.sprite, Is.Not.Null);
                    Assert.That(image.sprite.name, Is.EqualTo(p < openingEnd ? "Opening_2" : p < openingEnd + 0.18f ? "Upper_3" : "Upper_6"));
                    Assert.That(image.transform.Find("OpeningShot").gameObject.activeSelf, Is.False);
                    Assert.That(image.transform.Find("CastingShot").gameObject.activeSelf, Is.False);
                    cels.Add(image.sprite.name);
                    Call(controller, "TickPresentation", 0.02f);
                }
                CollectionAssert.AreEquivalent(new[] { "Opening_2", "Upper_3", "Upper_6" }, cels);
                Call(controller, "Skip"); Call(controller, "Skip");
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"));
                Assert.That(image.gameObject.activeSelf, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("OpeningBody")] [TestCase("OpeningStaffArm")]
        public void MissingRequiredLayerCannotDisplayAnIncompleteCharacter(string absentLayer)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("MissingLayerCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080, 2341);
                Func<string, Sprite> load = path => path.EndsWith("/" + absentLayer, StringComparison.Ordinal) ? null : Resources.Load<Sprite>(path);
                var film = Activator.CreateInstance(T("TenPullAnimeFilm"), new object[] { canvas, settings, load });
                var sequence = Sequence(Results(1));
                for (int frame = 0; frame < 20; frame++)
                {
                    Call(film, "Render", sequence);
                    var image = canvas.Find("AnimeFilm").GetComponent<Image>();
                    Assert.That(image.enabled, Is.True);
                    Assert.That(image.sprite.name, Is.EqualTo("Opening_2"));
                    Assert.That(image.transform.Find("OpeningShot").gameObject.activeSelf, Is.False);
                    Call(sequence, "Tick", 0.02f);
                }
                Call(sequence, "Skip"); Call(film, "Render", sequence);
                Assert.That(P(film, "IsVisible"), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Color32[] ReadRenderedPixels(RectTransform canvas)
        {
            var cameraObject = new GameObject("MotionPixelCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, canvas.gameObject.scene);
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(128, 256, 24);
            var pixels = new Texture2D(128, 256, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.enabled = false; camera.scene = canvas.gameObject.scene;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = canvas.rect.height / 2;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,128,256),0,0); pixels.Apply();
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        [Test] public void Class4AnticipationIsSilentAndSkipStopsEveryCinematicLayer()
        {
            // Keep the legacy SFX fallback covered independently of the default original score.
            F(settings, "Music").GetType().GetField("Enabled").SetValue(F(settings, "Music"), false);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("SoundTimelineCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("SoundTimeline", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var controller = panel.AddComponent(T("TenPullPresentationController"));
                var cues = new List<int>();
                var cueType = controller.GetType().GetEvent("SoundCued").EventHandlerType.GetGenericArguments()[0];
                typeof(TenPullPresentationTests).GetMethod("ObserveCues", BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(cueType).Invoke(null, new object[] { controller, (Action<int, float>)((cue, volume) => cues.Add(cue)) });
                int stopped = 0;
                controller.GetType().GetEvent("SoundsStopped").AddEventHandler(controller, (Action)(() => stopped++));
                Call(controller, "Present", Results(4), (Func<string, Sprite>)(id => null), null, null, null, true, false, settings);
                Canvas.ForceUpdateCanvases();
                var sequence = P(controller, "Sequence");
                for (int i = 0; i < 150 && P(sequence, "Phase").ToString() != "CutIn"; i++) Call(controller, "TickPresentation", 0.05f);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("CutIn"));
                Assert.That(stopped, Is.GreaterThanOrEqualTo(2), "Charge compression and cut-in anticipation stop the previous sound.");
                cues.Clear();
                Call(controller, "TickPresentation", 0.1f);
                Assert.That(cues, Is.Empty, "The first 0.16 seconds are a deliberate quiet beat.");
                Call(controller, "TickPresentation", 0.1f);
                Assert.That(cues, Has.Count.EqualTo(1));
                Assert.That(cues[0], Is.EqualTo(Convert.ToInt32(F(F(settings, "Legendary"), "RevealCue"))));
                var safe = panel.transform.Find("TenPullSafeContent");
                var film = safe.Find("AnimeFilm").GetComponent<Image>();
                Assert.That(film.gameObject.activeSelf, Is.True);
                Assert.That(film.sprite.name, Does.StartWith("Upper_"));
                Assert.That(safe.Find("RareCutIn/MonsterSilhouette").GetComponent<Image>().enabled, Is.False,
                    "A missing portrait must never become an opaque placeholder rectangle.");
                Call(controller, "Skip");
                Assert.That(safe.Find("CinematicStage").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("RareCutIn").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("AnimeFilm").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("SoftFlash").GetComponent<Image>().color.a, Is.Zero);
                Assert.That(stopped, Is.GreaterThanOrEqualTo(3));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(3, 1)] [TestCase(4, 1)] [TestCase(5, 1)] [TestCase(6, 0)]
        public void EveryConfirmedResultRevealsOnceAndOnlyClass4CutsIn(int scenario, int expectedCutIns)
        {
            Array input = Results(scenario); var sequence = Sequence(input);
            var opened = new List<int>(); var cutIns = new List<int>(); int completions = 0;
            Action changed = () =>
            {
                string phase = P(sequence, "Phase").ToString(); int index = (int)P(sequence, "Index");
                if (phase == "Reveal") opened.Add(index);
                if (phase == "CutIn") { cutIns.Add(index); Assert.That((int)F(P(sequence, "Current"), "ClassRank"), Is.EqualTo(4)); }
            };
            sequence.GetType().GetEvent("PhaseChanged").AddEventHandler(sequence, changed);
            sequence.GetType().GetEvent("Completed").AddEventHandler(sequence, (Action)(() => completions++));
            var before = UnityEngine.Random.state;
            for (int tick = 0; tick < 1200 && !(bool)P(sequence, "IsComplete"); tick++) Call(sequence, "Tick", 0.05f);
            Assert.That((bool)P(sequence, "IsComplete"), Is.True);
            CollectionAssert.AreEqual(Enumerable.Range(0, 10), opened);
            Assert.That(cutIns.Count, Is.EqualTo(expectedCutIns));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(before), "Animation must not advance the draw RNG.");
            for (int i = 0; i < 10; i++) Assert.That(JsonUtility.ToJson(Call(sequence, "GetResult", i)), Is.EqualTo(JsonUtility.ToJson(input.GetValue(i))));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void SkipAtEveryReachablePhaseAndIndexIsIdempotent(int scenario)
        {
            var states = new HashSet<string>();
            var reference = Sequence(Results(scenario));
            for (int ticks = 0; ticks < 1000 && !(bool)P(reference, "IsComplete"); ticks++)
            {
                string key = P(reference, "Index") + ":" + P(reference, "Phase");
                if (states.Add(key))
                {
                    var sequence = Sequence(Results(scenario));
                    for (int j = 0; j < ticks; j++) Call(sequence, "Tick", 0.1f);
                    int completed = 0;
                    sequence.GetType().GetEvent("Completed").AddEventHandler(sequence, (Action)(() => completed++));
                    for (int j = 0; j < 20; j++) { Call(sequence, "Skip"); Call(sequence, "Advance"); Call(sequence, "Tick", 100f); }
                    Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"), key);
                    Assert.That(P(sequence, "OpenedCount"), Is.EqualTo(10));
                    Assert.That(completed, Is.EqualTo(1));
                    for (int j = 0; j < 10; j++) Assert.That(Call(sequence, "GetResult", j), Is.Not.Null);
                }
                Call(reference, "Tick", 0.1f);
            }
        }

        [Test] public void SnapshotIsDetachedAndRapidPressesCannotSkipUnopenedStones()
        {
            var input = Results(0); var sequence = Sequence(input);
            string expected = (string)F(input.GetValue(0), "MonsterId");
            input.GetValue(0).GetType().GetField("MonsterId").SetValue(input.GetValue(0), "tampered");
            object returned = P(sequence, "Current"); returned.GetType().GetField("MonsterId").SetValue(returned, "also_tampered");
            Assert.That(F(P(sequence, "Current"), "MonsterId"), Is.EqualTo(expected));
            sequence.GetType().GetProperty("AutoPlay").SetValue(sequence, false);
            for (int i = 0; i < 200 && !(bool)P(sequence, "CanAdvance"); i++) Call(sequence, "Tick", 0.1f);
            Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Focus"));
            for (int i = 0; i < 100; i++) Call(sequence, "Advance");
            Assert.That(P(sequence, "Index"), Is.EqualTo(0));
            Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Crack"));
            Call(sequence, "Tick", float.NaN); Call(sequence, "Tick", float.PositiveInfinity);
            Assert.That(P(sequence, "Elapsed"), Is.EqualTo(0f));
        }

        [Test] public void RecoveryRequiresAllTenAlreadyOwnedInstances()
        {
            bool existed = PlayerPrefs.HasKey(JournalKey); string old = PlayerPrefs.GetString(JournalKey);
            Type journal = T("TenPullPresentationJournal");
            try
            {
                var input = Results(5);
                journal.GetMethod("Store").Invoke(null, new object[] { input });
                var owned = new HashSet<string>(input.Cast<object>().Select(x => (string)F(x, "InstanceId")));
                Func<string, bool> check = owned.Contains;
                Array recovered = (Array)journal.GetMethod("Read").Invoke(null, new object[] { check });
                Assert.That(recovered.Length, Is.EqualTo(10));
                for (int i = 0; i < 10; i++) Assert.That(JsonUtility.ToJson(recovered.GetValue(i)), Is.EqualTo(JsonUtility.ToJson(input.GetValue(i))));
                owned.Remove("preview_5");
                Assert.That(journal.GetMethod("Read").Invoke(null, new object[] { check }), Is.Null);
                Assert.That(PlayerPrefs.HasKey(JournalKey), Is.False);
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(JournalKey, old); else PlayerPrefs.DeleteKey(JournalKey);
                PlayerPrefs.Save();
            }
        }

        [TestCase(1179, 2556)] [TestCase(750, 1334)]
        public void ActualArtworkRendersAndSkipCleansAllEffects(int width, int height)
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("TenPullCanvas", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(1080, 1080f * height / width);
                var panel = new GameObject("TenPullPresentation", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                Component controller = panel.AddComponent(T("TenPullPresentationController"));
                Func<string, Sprite> resolver = id => (Sprite)Preview.GetMethod("ResolvePortrait").Invoke(null, new object[] { id });
                int completions = 0;
                Call(controller, "Present", Results(5), resolver, (Action)(() => completions++), (Action)(() => { }), (Action)(() => { }), true, false, settings);
                Canvas.ForceUpdateCanvases();
                var safe = (RectTransform)panel.transform.Find("TenPullSafeContent");
                // Simulate the phone's notch / home-indicator insets without querying the test host screen.
                safe.offsetMin = new Vector2(0, width == 1179 ? 94 : 0);
                safe.offsetMax = new Vector2(0, width == 1179 ? -162 : -40);
                Call(controller, "TickPresentation", 0f);
                foreach (var image in panel.GetComponentsInChildren<Image>(true).Where(x => x.name == "Stone"))
                {
                    Assert.That(image.sprite, Is.Not.Null, image.name);
                    Assert.That(image.gameObject.activeInHierarchy, Is.False, "No stones may be visible on the first frame.");
                }
                Capture(canvas, width, height, "intro");
                object sequence = P(controller, "Sequence");
                var captured = new HashSet<string>();
                for (int ticks = 0; ticks < 1200 && !(bool)P(sequence, "IsComplete"); ticks++)
                {
                    Call(controller, "TickPresentation", 0.05f);
                    string phase = P(sequence, "Phase").ToString(); int rank = (int)F(P(sequence, "Current"), "ClassRank");
                    if (phase == "CutIn" && (float)P(sequence, "Elapsed") >= 0.2f && captured.Add("slash"))
                        Capture(canvas, width, height, "cutin_slash");
                    if ((float)P(sequence, "Elapsed") < (phase == "CutIn" ? 0.55f : 0.2f)) continue;
                    string key = phase == "CutIn" ? "cutin_c" + rank : phase == "Reveal" ? "reveal_c" + rank : "";
                    if (key != "" && captured.Add(key)) Capture(canvas, width, height, key);
                    if (phase == "Reveal")
                    {
                        foreach (var portrait in safe.GetComponentsInChildren<Image>().Where(x => x.name == "Portrait"))
                        {
                            Assert.That(portrait.rectTransform.anchorMin, Is.EqualTo(Vector2.one * .5f));
                            Assert.That(portrait.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                            var slotBounds = Bounds(canvas, portrait.transform.parent);
                            var portraitBounds = Bounds(canvas, portrait.transform);
                            Assert.That(portraitBounds.yMin, Is.GreaterThanOrEqualTo(slotBounds.yMin));
                            Assert.That(portraitBounds.yMax, Is.LessThanOrEqualTo(slotBounds.yMax));
                        }
                        var image = safe.Find("FocalReveal/RevealedMonster").GetComponent<Image>();
                        Assert.That(image.sprite, Is.Not.Null); Assert.That(image.preserveAspect, Is.True);
                        Assert.That(Bounds(canvas, safe.Find("MonsterName")).yMin,
                            Is.GreaterThan(Bounds(canvas, safe.Find("MonsterInfo")).yMax + 2));
                    }
                }
                Assert.That(completions, Is.EqualTo(1));
                Capture(canvas, width, height, "summary");
                Assert.That(panel.GetComponentsInChildren<Text>().Count(x => x.name == "Name"), Is.EqualTo(10));
                for (int i = 0; i < 10; i++)
                {
                    var slot = safe.Find("StoneSlot_" + i);
                    Assert.That(Bounds(canvas, slot.Find("Name")).yMin, Is.GreaterThan(Bounds(canvas, slot.Find("Detail")).yMax));
                }
                // Re-present, interrupt at a cut-in, then spam skip. No sound or VFX may remain.
                Call(controller, "Present", Results(4), resolver, (Action)(() => completions++), null, null, true, false, settings);
                sequence = P(controller, "Sequence");
                for (int i = 0; i < 100 && P(sequence, "Phase").ToString() != "CutIn"; i++) Call(controller, "TickPresentation", 0.1f);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("CutIn"));
                Call(controller, "Interrupt"); for (int i = 0; i < 20; i++) Call(controller, "Skip");
                Assert.That(completions, Is.EqualTo(2));
                Assert.That(safe.Find("SummoningConvergence").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("RareCutIn").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("FocalReveal").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("SoftFlash").GetComponent<Image>().color.a, Is.Zero);
                Assert.That(panel.GetComponent<AudioSource>().isPlaying, Is.False);
                panel.SetActive(false); panel.SetActive(true); Call(controller, "TickPresentation", 0.1f);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Summary"));
                Assert.That(completions, Is.EqualTo(2));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }

        private static Rect Bounds(RectTransform canvas, Transform target)
        {
            var corners = new Vector3[4]; ((RectTransform)target).GetWorldCorners(corners);
            var points = corners.Select(canvas.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(v => v.x), points.Min(v => v.y), points.Max(v => v.x), points.Max(v => v.y));
        }

        [TestCase(1, 411f, 367.5f, 362f, 367.5f, 343f, 382f)]
        [TestCase(3, 411f, 367.5f, 362f, 367.5f, 343f, 382f)]
        [TestCase(4, 378f, 388f, 374f, 388f, 361f, 412f)]
        public void VisibleStoneCenterStaysFixedAcrossZoomCracksAndShatter(int rank, float ix, float iy, float cx, float cy, float sx, float sy)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("StoneAlignmentCanvas", typeof(RectTransform), typeof(Canvas));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
                var panel = new GameObject("StoneAlignment", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                Component controller = panel.AddComponent(T("TenPullPresentationController"));
                Array input = Results(0);
                input.GetValue(0).GetType().GetField("ClassRank").SetValue(input.GetValue(0), rank);
                var configuration = Resources.Load("UI/GachaPage/TenPull/TenPullPresentationSettings", T("TenPullPresentationSettings"));
                Assert.That(configuration, Is.Not.Null);
                Call(controller, "Present", input, (Func<string, Sprite>)(id => null), null, null, null, true, false, configuration);
                var sequence = P(controller, "Sequence");
                sequence.GetType().GetProperty("AutoPlay").SetValue(sequence, false);
                while (P(sequence, "Phase").ToString() != "Focus") Call(controller, "TickPresentation", 0.05f);
                Canvas.ForceUpdateCanvases();
                var image = panel.transform.Find("TenPullSafeContent/FocalReveal/FocusedStone").GetComponent<Image>();
                Vector3 Center(float x, float y) => image.rectTransform.TransformPoint(Vector2.Scale(
                    new Vector2(x / 724f - 0.5f, y / 724f - 0.5f), image.rectTransform.rect.size));
                Vector3 expected = Center(ix, iy);
                for (int tick = 0; tick < 35; tick++)
                {
                    Call(controller, "TickPresentation", 0.04f);
                    Assert.That(Vector3.Distance(Center(ix, iy), expected), Is.LessThan(0.01f), "Visible center must not travel while zooming.");
                    if (tick == 2)
                    {
                        for (int tap = 0; tap < 20; tap++) Call(sequence, "Advance");
                        Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Focus"), "Early tapping must not snap the zoom to its final scale.");
                    }
                }
                Assert.That(image.rectTransform.localScale.x, Is.EqualTo(1f));
                Call(sequence, "Advance");
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Crack"));
                for (int tick = 0; tick < 4; tick++)
                {
                    Call(controller, "TickPresentation", 0.03f);
                    Assert.That(Vector3.Distance(Center(cx, cy), expected), Is.LessThan(0.01f));
                    Assert.That(image.rectTransform.localScale.x, Is.EqualTo(1f), "No post-focus scale jitter.");
                }
                for (int tick = 0; tick < 60 && P(sequence, "Phase").ToString() != "Burst"; tick++) Call(controller, "TickPresentation", 0.05f);
                Assert.That(P(sequence, "Phase").ToString(), Is.EqualTo("Burst"));
                Assert.That(Vector3.Distance(Center(sx, sy), expected), Is.LessThan(0.01f));
                Call(controller, "TickPresentation", 0.05f);
                Assert.That(Vector3.Distance(Center(sx, sy), expected), Is.LessThan(0.01f), "Fragments expand about the same center.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Capture(RectTransform canvas, int width, int height, string state)
        {
            string folder = Environment.GetEnvironmentVariable(state.StartsWith("single-", StringComparison.Ordinal)
                ? "WITCHTOWER_SINGLE_CAPTURE_DIR" : "WITCHTOWER_TEN_PULL_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var cameraObject = new GameObject("TenPullCaptureCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, canvas.gameObject.scene);
            var camera = cameraObject.GetComponent<Camera>();
            var previous = RenderTexture.active;
            try
            {
                camera.enabled = false; camera.scene = canvas.gameObject.scene;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = canvas.rect.height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(folder, state + "_" + width + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
