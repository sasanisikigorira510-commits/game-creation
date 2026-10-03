using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Monetization;

namespace WitchTower.UI
{
    public sealed class WorldAtlasController : MonoBehaviour
    {
        private static WorldAtlasController current;
        public static bool IsShowing => current != null && current.gameObject.activeInHierarchy;
        private RectTransform safe, viewport, content;
        private ScrollRect scroll;
        private readonly List<RectTransform> markers = new List<RectTransform>();
        private readonly List<Image> markerImages = new List<Image>();
        private Text regionTitle, description, status, zoomText;
        private Button returnButton;
        private Font font;
        private Action returnToLocal;
        private Transform owner;
        private bool hasOwner;
        private Vector2 fittedSize, lastViewport;
        private float zoom = 1f;
        private int selected;
        private bool closing;
        private static readonly Color Gold = new Color(.96f,.80f,.46f);

        public static bool TryShow(Transform owner, Action onReturn)
        {
            if (!WorldAtlasCatalog.IsAvailable(GameManager.Instance?.PlayerProfile)) return false;
            return Create(owner,onReturn);
        }

#if UNITY_EDITOR
        public static bool ShowEditorPreview() => Create(null,null);
#endif
        private static bool Create(Transform owner, Action onReturn)
        {
            if (IsShowing) return false;
            var root = new GameObject("WorldAtlasOverlay",typeof(RectTransform),typeof(Canvas),
                typeof(CanvasScaler),typeof(GraphicRaycaster));
            // A nested Canvas inherits the local panel's 100x100 layout and its
            // scaler is ignored. Keep this a root overlay and track ownership.
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting=true; canvas.sortingOrder=29500;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,2340); scaler.matchWidthOrHeight=.5f;
            current=root.AddComponent<WorldAtlasController>(); current.returnToLocal=onReturn;
            current.owner=owner; current.hasOwner=owner!=null;
            current.Build(); return true;
        }

        private void Build()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var backdrop=Box("AtlasBackdrop",transform,Vector2.zero,Vector2.one);
            backdrop.gameObject.AddComponent<Image>().color=new Color(.018f,.035f,.052f,.99f);
            safe=Box("AtlasSafeArea",transform,Vector2.zero,Vector2.one); ApplySafeArea();
            Label("AtlasTitle",safe,"世界地図",48,new Vector2(.04f,.934f),new Vector2(.96f,.985f),Gold);
            Label("AtlasSubtitle",safe,"あの冒険は、この世界の片隅だった。",27,
                new Vector2(.04f,.892f),new Vector2(.96f,.93f),new Color(.80f,.87f,.87f));
            Button("AtlasFocusHomeland",safe,"故郷を探す",new Vector2(.04f,.821f),new Vector2(.37f,.881f),FocusHomeland);
            zoomText=Label("AtlasZoom",safe,"全体",25,new Vector2(.39f,.821f),new Vector2(.57f,.881f),Gold);
            Button("AtlasZoomOut",safe,"−",new Vector2(.59f,.821f),new Vector2(.70f,.881f),()=>SetZoom(zoom/1.5f));
            Button("AtlasZoomIn",safe,"＋",new Vector2(.72f,.821f),new Vector2(.83f,.881f),()=>SetZoom(zoom*1.5f));
            Button("AtlasFit",safe,"全体",new Vector2(.85f,.821f),new Vector2(.96f,.881f),()=>SetZoom(1));
            viewport=Box("AtlasViewport",safe,new Vector2(.035f,.326f),new Vector2(.965f,.807f));
            viewport.gameObject.AddComponent<Image>().color=new Color(.035f,.07f,.09f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport;
            scroll.horizontal=scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.inertia=true; scroll.decelerationRate=.08f; scroll.scrollSensitivity=45;
            content=Box("AtlasContent",viewport,Vector2.one*.5f,Vector2.one*.5f);
            var art=content.gameObject.AddComponent<Image>(); art.sprite=Resources.Load<Sprite>(WorldAtlasCatalog.MapPath);
            art.preserveAspect=true; scroll.content=content;
            for(int i=0;i<WorldAtlasCatalog.Regions.Length;i++) BuildMarker(i);
            Label("AtlasGestureHint",safe,"印をタップで地域を見る  ／  拡大した地図はスワイプで移動",22,
                new Vector2(.035f,.294f),new Vector2(.965f,.323f),new Color(.7f,.8f,.81f));
            var card=Box("AtlasRegionCard",safe,new Vector2(.035f,.125f),new Vector2(.965f,.286f));
            var plate=card.gameObject.AddComponent<Image>();
            plate.sprite=Resources.Load<Sprite>("UI/AudioSettings/SettingsPanelFrameImage2");
            plate.raycastTarget=false;
            regionTitle=Label("AtlasRegionTitle",card,"",35,new Vector2(.08f,.70f),new Vector2(.92f,.91f),Gold);
            description=Label("AtlasRegionDescription",card,"",26,new Vector2(.08f,.30f),new Vector2(.92f,.71f),Color.white);
            description.lineSpacing=1.2f;
            status=Label("AtlasRegionStatus",card,"",22,new Vector2(.08f,.10f),new Vector2(.92f,.28f),new Color(.75f,.85f,.86f));
            returnButton=Button("AtlasBack",safe,"故郷の6つのダンジョンへ",new Vector2(.10f,.004f),new Vector2(.90f,.098f),Close);
            SelectRegion(0);
            Canvas.ForceUpdateCanvases(); FitContent();
        }

        private void BuildMarker(int index)
        {
            var region=WorldAtlasCatalog.Regions[index];
            Vector2 point=new Vector2(region.MapPosition.x,1-region.MapPosition.y);
            var marker=Box("AtlasRegion_"+region.Id,content,point,point); marker.sizeDelta=new Vector2(120,120);
            marker.gameObject.AddComponent<Image>().color=new Color(1,1,1,.001f);
            var visual=Box("Marker",marker,new Vector2(.22f,.22f),new Vector2(.78f,.78f));
            var image=visual.gameObject.AddComponent<Image>(); image.sprite=Resources.Load<Sprite>("UI/DungeonSelect/FloorNodeUnlocked");
            image.raycastTarget=false;
            var button=marker.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var tap=marker.gameObject.AddComponent<AtlasRegionTap>();
            button.onClick.AddListener(()=>{if(!tap.WasDragged) SelectRegion(index);});
            var label=Label("Name",marker,region.Name,26,new Vector2(-.65f,-.45f),new Vector2(1.65f,.02f),Color.white);
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.015f,.025f,.035f,.95f);
            outline.effectDistance=new Vector2(2,-2);
            markers.Add(marker);markerImages.Add(image);
        }

        public void SelectRegion(int index)
        {
            if(index<0 || index>=WorldAtlasCatalog.Regions.Length) return;
            selected=index;var region=WorldAtlasCatalog.Regions[index];
            regionTitle.text=region.Name;description.text=region.Description;
            status.text=region.AccessNote ?? (region.IsHomeland?"第一部  第1〜6話":"未踏の地域");
            returnButton.GetComponentInChildren<Text>().text=region.IsHomeland?"故郷の6つのダンジョンへ":"故郷の探索地図へ戻る";
            for(int i=0;i<markerImages.Count;i++) markerImages[i].sprite=Resources.Load<Sprite>(
                i==selected?"UI/DungeonSelect/FloorNodeSelected":"UI/DungeonSelect/FloorNodeUnlocked");
        }

        public void SetZoom(float requested)
        {
            float next=Mathf.Clamp(requested,1,3.375f);
            Vector2 position=content.anchoredPosition*(next/zoom);
            zoom=next;content.sizeDelta=fittedSize*zoom;
            content.anchoredPosition=zoom<=1?Vector2.zero:position;
            ClampPosition();scroll.StopMovement();zoomText.text=zoom<=1?"全体":zoom.ToString("0.0")+"倍";
        }

        public void FocusHomeland()
        {
            SelectRegion(0);SetZoom(3.375f);
            var point=WorldAtlasCatalog.Regions[0].MapPosition;
            content.anchoredPosition=new Vector2((.5f-point.x)*content.sizeDelta.x,(point.y-.5f)*content.sizeDelta.y);
            ClampPosition();
        }

        private void FitContent()
        {
            if(viewport==null || viewport.rect.width<=0 || viewport.rect.height<=0) return;
            lastViewport=viewport.rect.size;
            var sprite=content.GetComponent<Image>().sprite;
            float aspect=sprite!=null?sprite.rect.width/sprite.rect.height:.8f;
            float width=Mathf.Min(lastViewport.x,lastViewport.y*aspect);
            fittedSize=new Vector2(width,width/aspect);SetZoom(zoom);
        }

        private void ClampPosition()
        {
            var bounds=Vector2.Max(Vector2.zero,(content.sizeDelta-viewport.rect.size)*.5f);
            content.anchoredPosition=new Vector2(Mathf.Clamp(content.anchoredPosition.x,-bounds.x,bounds.x),
                Mathf.Clamp(content.anchoredPosition.y,-bounds.y,bounds.y));
        }

        private void Update()
        {
            if(hasOwner && (owner==null || !owner.gameObject.activeInHierarchy))
            {
                returnToLocal=null;Close();return;
            }
            ApplySafeArea();
            if(viewport!=null && viewport.rect.size!=lastViewport) FitContent();
            if(Input.GetKeyDown(KeyCode.Escape)) Close();
        }
        private void ApplySafeArea()
        {
            if(safe==null) return;
            var size=new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            Rect area=Screen.safeArea;
            if(area.width<=0 || area.height<=0) area=new Rect(Vector2.zero,size);
            area=ResolveSafeArea(area,size,AdMobBannerService.Instance?.VisibleHeightPixels??0);
            safe.anchorMin=new Vector2(area.xMin/size.x,area.yMin/size.y);
            safe.anchorMax=new Vector2(area.xMax/size.x,area.yMax/size.y);
            safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        public static Rect ResolveSafeArea(Rect area,Vector2 screen,float bannerHeight)
        {
            float bottom=area.yMin+Mathf.Max(0,bannerHeight)+12;
            return Rect.MinMaxRect(area.xMin,Mathf.Min(bottom,area.yMax-1),area.xMax,area.yMax);
        }
        public void Close()
        {
            if(closing) return; closing=true;
            if(current==this) current=null;
            gameObject.SetActive(false);Destroy(gameObject);returnToLocal?.Invoke();
        }
        private void OnDestroy(){if(current==this)current=null;}
        private void OnDisable()
        {
            if(gameObject.activeInHierarchy) return;
            if(current==this) current=null;
            if(!closing && Application.isPlaying){closing=true;Destroy(gameObject);}
        }

        private static RectTransform Box(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;
            rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private Text Label(string name,Transform parent,string value,int size,Vector2 min,Vector2 max,Color color)
        {
            var rect=Box(name,parent,min,max);var text=rect.gameObject.AddComponent<Text>();
            text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleCenter;
            text.resizeTextForBestFit=false;text.raycastTarget=false;return text;
        }
        private Button Button(string name,Transform parent,string label,Vector2 min,Vector2 max,Action action)
        {
            var rect=Box(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();
            image.sprite=Resources.Load<Sprite>("UI/GachaPage/GachaSmallButton");image.color=new Color(.75f,.83f,.9f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
            Label("Label",rect,label,27,new Vector2(.05f,.08f),new Vector2(.95f,.92f),Color.white);return button;
        }
    }

    // ScrollRect receives the drag; suppress its eventual click on the region.
    public sealed class AtlasRegionTap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Vector2 pressed;
        public bool WasDragged {get;private set;}
        public void OnPointerDown(PointerEventData e){pressed=e.position;WasDragged=false;}
        public void OnPointerUp(PointerEventData e){WasDragged=e.dragging || Vector2.Distance(pressed,e.position)>12;}
    }
}
