using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class StoryArchiveLayoutTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);

        [TestCase(1179,2556,177,102,150)]
        [TestCase(750,1334,40,0,100)]
        [TestCase(640,1136,40,0,150)]
        [TestCase(768,1024,24,20,90)]
        [TestCase(1179,2556,177,102,0)]
        public void EveryStoryRemainsReachableAndHomeButtonClearsNativeAd(int width, int height, int top, int bottom, int adHeight)
        {
            Type type = T("UI.StoryDialogueController");
            var current = type.GetField("current", StaticHidden);
            object previous = current.GetValue(null);
            Component controller = null;
            try
            {
                controller = (Component)type.GetMethod("Create", StaticHidden).Invoke(null,null);
                ((Behaviour)controller).enabled = false;
                object save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
                save.GetType().GetField("HighestFloor").SetValue(save,60);
                object profile = Activator.CreateInstance(T("Data.PlayerProfile"),new[]{save});
                type.GetField("profile",Hidden).SetValue(controller,profile);
                type.GetMethod("BuildArchive",Hidden).Invoke(controller,null);
                var root = (RectTransform)controller.transform;
                var scaler = root.GetComponent<CanvasScaler>();
                float scale = Mathf.Sqrt(width / 1080f * height / 2340f);
                scaler.enabled = false;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1;
                root.position = Vector3.zero;
                root.localScale = Vector3.one;
                root.sizeDelta = new Vector2(width/scale,height/scale);
                Rect area = (Rect)type.GetMethod("ResolveContentSafeArea",StaticHidden).Invoke(null,
                    new object[]{new Rect(0,bottom,width,height-top-bottom),new Vector2(width,height),(float)adHeight});
                var safe = (RectTransform)type.GetField("safeRoot",Hidden).GetValue(controller);
                safe.anchorMin = new Vector2(0,area.yMin/height);
                safe.anchorMax = new Vector2(1,area.yMax/height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                Canvas.ForceUpdateCanvases();
                var scroll = root.GetComponentInChildren<ScrollRect>();
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(),Is.Not.Null);
                Assert.That(scroll.horizontal,Is.False);
                Assert.That(scroll.vertical,Is.True);
                var stories = (Array)T("Data.StoryDialogueCatalog").GetProperty("All").GetValue(null);
                Assert.That(scroll.content.childCount,Is.EqualTo(stories.Length));
                Assert.That(scroll.content.GetComponentsInChildren<Button>().Select(b => b.name),
                    Is.EquivalentTo(stories.Cast<object>().Select(s => "StoryArchive_" + s.GetType().GetProperty("EventId").GetValue(s))));
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                var close = (RectTransform)safe.Find("StoryArchiveClose");
                Rect footer = Bounds(root,close);
                Rect view = Bounds(root,scroll.viewport);
                float footerBottomPixels = (footer.yMin-root.rect.yMin)*scale;
                Assert.That(footerBottomPixels,Is.GreaterThan(bottom+adHeight+8));
                Assert.That(footer.yMax,Is.LessThan(view.yMin));
                Rect footerBeforeScroll = footer;
                scroll.verticalNormalizedPosition = 1;
                Canvas.ForceUpdateCanvases();
                AssertInside(scroll.viewport,(RectTransform)scroll.content.GetChild(0));
                Capture(root,width,height,$"archive-{width}x{height}-ad{adHeight}-top.png");
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                AssertInside(scroll.viewport,(RectTransform)scroll.content.GetChild(stories.Length - 1));
                Assert.That(Bounds(root,close),Is.EqualTo(footerBeforeScroll),"The home action stays fixed while the list moves.");
                Capture(root,width,height,$"archive-{width}x{height}-ad{adHeight}-bottom.png");
                Rect withoutAd = (Rect)type.GetMethod("ResolveContentSafeArea",StaticHidden).Invoke(null,
                    new object[]{new Rect(0,bottom,width,height-top-bottom),new Vector2(width,height),0f});
                Assert.That(withoutAd.yMin,Is.EqualTo(bottom),"Hidden/removed ads must not keep stale reserved space.");
            }
            finally
            {
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
                current.SetValue(null,previous);
            }
        }

        private static void AssertInside(RectTransform view,RectTransform row)
        {
            Rect bounds=Bounds(view,row);
            Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(view.rect.yMin-.5f));
            Assert.That(bounds.yMax,Is.LessThanOrEqualTo(view.rect.yMax+.5f));
        }
        private static Rect Bounds(RectTransform parent,RectTransform child)
        {
            var corners=new Vector3[4]; child.GetWorldCorners(corners);
            var points=corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private static void Capture(RectTransform root,int width,int height,string filename)
        {
            string directory=Environment.GetEnvironmentVariable("WITCHTOWER_STORY_CAPTURE_DIR");
            if(string.IsNullOrEmpty(directory) || (width!=1179 && width!=640)) return;
            Directory.CreateDirectory(directory);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",StaticHidden)
                .Invoke(null,new object[]{root,width,height,Path.Combine(directory,filename)});
        }
    }
}
