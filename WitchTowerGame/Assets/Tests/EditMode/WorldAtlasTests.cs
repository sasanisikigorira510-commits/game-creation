using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class WorldAtlasTests
    {
        private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name).Invoke(target,args);
        private static object Field(object target,string name)=>target.GetType().GetField(name,Hidden).GetValue(target);

        [TestCase(59,true,true,false)] [TestCase(60,false,true,false)]
        [TestCase(60,true,false,false)] [TestCase(60,true,true,true)]
        public void AtlasOpensAfterTheFinalStoryWithoutChangingTheSave(int floor,bool seen,bool tutorial,bool expected)
        {
            var save=T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
            save.GetType().GetField("HighestFloor").SetValue(save,floor);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save,tutorial);
            if(seen)((IList)save.GetType().GetField("SeenStoryEventIds").GetValue(save)).Add("story_first_arc_complete");
            var profile=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{save});
            string before=JsonUtility.ToJson(save);
            Assert.That(T("Data.WorldAtlasCatalog").GetMethod("IsAvailable").Invoke(null,new[]{profile}),Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(save),Is.EqualTo(before));
            var dungeons=(IEnumerable)T("Data.BattleDungeonCatalog").GetProperty("Dungeons").GetValue(null);
            Assert.That(dungeons.Cast<object>().Count(),Is.EqualTo(6),"Uncharted map regions must not become playable dummy dungeons.");
        }

        [TestCase(1179,2556,177,102,150)]
        [TestCase(640,1136,40,0,150)]
        [TestCase(768,1024,24,20,90)]
        public void MapAndControlsFitAboveAdsAndRegionsNeverStartAnUnavailableBattle(int width,int height,int top,int bottom,int ad)
        {
            using(var view=new AtlasView(width,height,top,bottom,ad))
            {
                var image=view.Content.GetComponent<Image>();
                Assert.That(image.sprite,Is.Not.Null);
                Assert.That(image.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(view.Viewport.GetComponent<RectMask2D>(),Is.Not.Null);
                Assert.That(view.Content.rect.width,Is.LessThanOrEqualTo(view.Viewport.rect.width+.1f));
                Assert.That(view.Content.rect.height,Is.LessThanOrEqualTo(view.Viewport.rect.height+.1f));
                var back=view.Find("AtlasBack");
                Assert.That(Bounds(view.Root,back).yMin,Is.GreaterThan(view.Root.rect.yMin+(bottom+ad)/view.Scale));
                Assert.That(Bounds(view.Root,view.Find("AtlasRegionCard")).yMax,Is.LessThan(Bounds(view.Root,view.Viewport).yMin));
                var regions=(Array)T("Data.WorldAtlasCatalog").GetField("Regions").GetValue(null);
                for(int i=0;i<regions.Length;i++)
                {
                    Call(view.Controller,"SelectRegion",i);
                    var text=view.Find("AtlasRegionCard/AtlasRegionDescription").GetComponent<Text>();
                    Assert.That(text.resizeTextForBestFit,Is.False);
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+.1f),text.text);
                    var status=view.Find("AtlasRegionCard/AtlasRegionStatus").GetComponent<Text>();
                    Assert.That(status.preferredHeight,Is.LessThanOrEqualTo(status.rectTransform.rect.height+.1f),status.text);
                    Assert.That(view.Find("AtlasBack").GetComponentInChildren<Text>().text,
                        Is.EqualTo(i==0?"故郷の6つのダンジョンへ":"故郷の探索地図へ戻る"));
                }
                Call(view.Controller,"SelectRegion",0);
                Capture(view,width,height,"atlas-overview");
                Call(view.Controller,"FocusHomeland");Canvas.ForceUpdateCanvases();
                Assert.That(view.Content.rect.width,Is.GreaterThan(view.Viewport.rect.width));
                Assert.That(view.Content.rect.height,Is.GreaterThan(view.Viewport.rect.height));
                var homeland=(RectTransform)view.Content.Find("AtlasRegion_homeland");
                Rect marker=Bounds(view.Viewport,homeland);
                Assert.That(view.Viewport.rect.Contains(marker.center),Is.True,"Focus must actually bring the homeland marker into view.");
                Capture(view,width,height,"atlas-homeland");
                Call(view.Controller,"SetZoom",1f);Canvas.ForceUpdateCanvases();
                Assert.That(view.Content.anchoredPosition,Is.EqualTo(Vector2.zero));
                Call(view.Controller,"SelectRegion",3);Capture(view,width,height,"atlas-uncharted");
                int timeGate=Array.FindIndex(regions.Cast<object>().ToArray(),r=>(string)r.GetType().GetField("Id").GetValue(r)=="time_gate");
                Assert.That(timeGate,Is.GreaterThanOrEqualTo(0));
                Call(view.Controller,"SelectRegion",timeGate);
                Assert.That(view.Find("AtlasRegionCard/AtlasRegionDescription").GetComponent<Text>().text,Does.Contain("新章の6話").And.Contain("過去の世界"));
                Assert.That(view.Find("AtlasRegionCard/AtlasRegionStatus").GetComponent<Text>().text,Does.Contain("閉ざされている"));
                Capture(view,width,height,"atlas-time-gate");
            }
        }

        [Test]
        public void PanDoesNotSelectARegionAndRepeatedPreviewDoesNotCreateDuplicateOverlays()
        {
            using(var view=new AtlasView(1179,2556,177,102,0))
            {
                var preview=T("UI.WorldAtlasController").GetMethod("ShowEditorPreview");
                Assert.That(preview.Invoke(null,null),Is.False);
                var go=new GameObject("AtlasEventTest",typeof(EventSystem));
                try
                {
                    var tap=view.Content.Find("AtlasRegion_harbor").GetComponent(T("UI.AtlasRegionTap"));
                    var pointer=new PointerEventData(go.GetComponent<EventSystem>()){position=new Vector2(100,100)};
                    Call(tap,"OnPointerDown",pointer);pointer.position=new Vector2(150,160);Call(tap,"OnPointerUp",pointer);
                    tap.GetComponent<Button>().onClick.Invoke();
                    Assert.That(Field(view.Controller,"selected"),Is.EqualTo(0));
                    Call(tap,"OnPointerDown",pointer);Call(tap,"OnPointerUp",pointer);
                    tap.GetComponent<Button>().onClick.Invoke();
                    Assert.That(Field(view.Controller,"selected"),Is.EqualTo(1));
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            Assert.That(T("UI.WorldAtlasController").GetProperty("IsShowing").GetValue(null),Is.False);
            using(var reopened=new AtlasView(1179,2556,177,102,0))
                Assert.That(reopened.Content.anchoredPosition,Is.EqualTo(Vector2.zero));
        }

        private sealed class AtlasView:IDisposable
        {
            public Component Controller;
            public RectTransform Root,Safe,Viewport,Content;
            public float Scale;
            public AtlasView(int width,int height,int top,int bottom,int ad)
            {
                Assert.That(T("UI.WorldAtlasController").GetMethod("ShowEditorPreview").Invoke(null,null),Is.True);
                Controller=(Component)T("UI.WorldAtlasController").GetField("current",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                ((Behaviour)Controller).enabled=false;
                Root=(RectTransform)Controller.transform;Root.GetComponent<CanvasScaler>().enabled=false;
                var canvas=Root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.scaleFactor=1;
                Scale=Mathf.Sqrt(width/1080f*height/2340f);Root.localScale=Vector3.one;Root.position=Vector3.zero;
                Root.sizeDelta=new Vector2(width/Scale,height/Scale);
                Safe=(RectTransform)Field(Controller,"safe");Viewport=(RectTransform)Field(Controller,"viewport");Content=(RectTransform)Field(Controller,"content");
                var area=(Rect)T("UI.WorldAtlasController").GetMethod("ResolveSafeArea").Invoke(null,new object[]{new Rect(0,bottom,width,height-top-bottom),new Vector2(width,height),(float)ad});
                Safe.anchorMin=new Vector2(area.xMin/width,area.yMin/height);Safe.anchorMax=new Vector2(area.xMax/width,area.yMax/height);
                Safe.offsetMin=Safe.offsetMax=Vector2.zero;
                Canvas.ForceUpdateCanvases();Controller.GetType().GetMethod("FitContent",Hidden).Invoke(Controller,null);
                Canvas.ForceUpdateCanvases();
            }
            public RectTransform Find(string path)=>(RectTransform)Safe.Find(path);
            public void Dispose()=>UnityEngine.Object.DestroyImmediate(Controller.gameObject);
        }
        private static Rect Bounds(RectTransform parent,RectTransform child)
        {
            var corners=new Vector3[4];child.GetWorldCorners(corners);var points=corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(v=>v.x),points.Min(v=>v.y),points.Max(v=>v.x),points.Max(v=>v.y));
        }
        private static void Capture(AtlasView view,int width,int height,string name)
        {
            string directory=Environment.GetEnvironmentVariable("WITCHTOWER_ATLAS_CAPTURE_DIR");
            if(string.IsNullOrEmpty(directory))return;
            Directory.CreateDirectory(directory);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{view.Root,width,height,Path.Combine(directory,$"{name}-{width}x{height}.png")});
        }
    }
}
