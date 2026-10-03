using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Home
{
    /// <summary>Only presents an already committed fusion. No RNG, granting, or save access.</summary>
    public sealed class FusionCinematicPresentation : IDisposable
    {
        private const string Art = "UI/FusionPage/Cinematic/";
        private static readonly Vector2 AuthorSize = new Vector2(1080, 1920);
        private readonly RectTransform parent, root, stage;
        private readonly Image chamber, shade, sealLeft, sealRight, sealCenter, outerSeal;
        private readonly Image leftParent, rightParent, leftGlow, rightGlow;
        private readonly FusionIonaRig iona;
        private readonly Image ribbonLeft, ribbonRight, cocoon, silhouette, bornImage, burst, pillar, cutin, flash;
        private readonly Image[] particles = new Image[48];
        private readonly Sprite[] cocoonFrames = new Sprite[8], upperCocoonFrames = new Sprite[8];
        private readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();
        private readonly Text nameLabel, classLabel, inheritanceLabel, parentLabel;
        private readonly Button skip;
        private readonly FusionCinematicSettings settings;
        private readonly FusionScorePlayer score;
        private MonsterDataSO lastBorn;
        private List<Sprite> bornAttackFrames;
        private Vector2 bornContentCenter;
        private float bornContentScale = 680f;
        private bool disposed, skipRequested;
        private float elapsed;
        public bool IsPlaying { get; private set; }
        public bool IsUpper { get; private set; }
        public static bool UsesUpperPresentation(int rank) => rank >= 4;
        public static float DurationForClass(int rank)
        {
            var config = Resources.Load<FusionCinematicSettings>(FusionCinematicSettings.ResourcePath);
            float value = UsesUpperPresentation(rank) ? (config != null ? config.UpperSeconds : 9f) : (config != null ? config.NormalSeconds : 7f);
            return Mathf.Clamp(value, 3f, 30f);
        }

        public FusionCinematicPresentation(RectTransform parent)
        {
            this.parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            settings = Resources.Load<FusionCinematicSettings>(FusionCinematicSettings.ResourcePath);
            score = new FusionScorePlayer();
            try
            {
                root = Rect("FusionCinematicRoot", parent); Stretch(root);
                var mask = root.gameObject.AddComponent<RectMask2D>(); mask.padding = Vector4.zero;
                var blocker = root.gameObject.AddComponent<Image>(); blocker.color = Color.black; blocker.raycastTarget = true;
                root.SetAsLastSibling();
                chamber = Picture("FusionChamber", root, Load("Chamber"), AuthorSize);
                shade = Picture("FusionShade", root, null, Vector2.one); Stretch(shade.rectTransform);
                stage = Rect("FusionAuthoredStage", root); stage.sizeDelta = AuthorSize;
                sealLeft = Picture("FusionLeftSeal", stage, Load("Effects/Seal"), Vector2.one * 410);
                sealRight = Picture("FusionRightSeal", stage, Load("Effects/Seal"), Vector2.one * 410);
                sealCenter = Picture("FusionCenterSeal", stage, Load("Effects/Seal"), Vector2.one * 760);
                outerSeal = Picture("FusionOuterSeal", stage, Load("Effects/Seal"), Vector2.one * 970);
                iona = new FusionIonaRig(stage);
                leftGlow = Picture("FusionLeftParentLight", stage, Load("Effects/Cocoon"), Vector2.one * 430);
                rightGlow = Picture("FusionRightParentLight", stage, Load("Effects/Cocoon"), Vector2.one * 430);
                leftParent = Picture("FusionParentA", stage, null, Vector2.one * 340);
                rightParent = Picture("FusionParentB", stage, null, Vector2.one * 340);
                ribbonLeft = Picture("FusionLeftRibbon", stage, Load("Effects/Ribbon"), Vector2.one * 520);
                ribbonRight = Picture("FusionRightRibbon", stage, Load("Effects/Ribbon"), Vector2.one * 520);
                silhouette = Picture("FusionBornSilhouette", stage, null, Vector2.one * 650);
                pillar = Picture("FusionBirthPillar", stage, Load("Effects/Pillar"), Vector2.one * 1700);
                bornImage = Picture("FusionBornMonster", stage, null, Vector2.one * 680);
                cocoon = Picture("FusionCocoon", stage, Load("Effects/Cocoon"), Vector2.one * 720);
                burst = Picture("FusionBirthBurst", stage, Load("Effects/Burst"), Vector2.one * 1080);
                for (int i = 0; i < particles.Length; i++) particles[i] = Picture("FusionSpark" + i, stage, Load("Effects/Spark"), Vector2.one * 28);
                cutin = Picture("FusionIonaCutIn", stage, Load("IonaCutIn"), AuthorSize);
                flash = Picture("FusionReleaseFlash", root, null, Vector2.one); Stretch(flash.rectTransform);
                nameLabel = Label("FusionBornName", stage, 57, new Vector2(0,-445), new Vector2(970,100), Color.white);
                classLabel = Label("FusionBornClass", stage, 31, new Vector2(0,-530), new Vector2(960,60), Color.white);
                inheritanceLabel = Label("FusionInheritance", stage, 29, new Vector2(0,-625), new Vector2(980,120), Color.white);
                parentLabel = Label("FusionParentContinuity", stage, 23, new Vector2(0,-715), new Vector2(950,70), Color.white);
                var skipRect = Rect("FusionSkip", root); skipRect.anchorMin = skipRect.anchorMax = new Vector2(1,1); skipRect.pivot = new Vector2(1,1); skipRect.sizeDelta = new Vector2(246,128);
                var buttonArt = skipRect.gameObject.AddComponent<Image>(); buttonArt.sprite = Resources.Load<Sprite>("UI/FusionPage/FusionSmallButton"); buttonArt.color = new Color(.12f,.12f,.18f,.9f);
                skip = skipRect.gameObject.AddComponent<Button>(); skip.targetGraphic = buttonArt; skip.onClick.AddListener(Skip);
                var skipText = Label("FusionSkipLabel", skipRect, 34, Vector2.zero, skipRect.sizeDelta, Color.white); skipText.text = "スキップ  ≫";
                for (int i = 0; i < 8; i++) { cocoonFrames[i] = Load("CocoonFrames/Cocoon_" + i); upperCocoonFrames[i] = Load("CocoonUpperFrames/Cocoon_" + i); }
                root.gameObject.SetActive(false);
            }
            catch
            {
                // A constructor failure prevents the caller from receiving this
                // instance. Remove its blocker here so the saved result remains usable.
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                    else UnityEngine.Object.DestroyImmediate(root.gameObject);
                }
                score.Dispose();
                throw;
            }
        }

        public IEnumerator Play(Sprite parentA, Sprite parentB, MonsterDataSO born, OwnedMonsterData created)
        {
            if (disposed) yield break;
            skipRequested = false; IsPlaying = true; elapsed = 0f;
            IsUpper = UsesUpperPresentation(born != null ? born.classRank : 1);
            float duration = DurationForClass(born != null ? born.classRank : 1);
            score.Begin(IsUpper, duration);
            try
            {
                while (!disposed && !skipRequested && elapsed < duration)
                {
                    RenderPreview(parentA, parentB, born, created, elapsed);
                    score.Tick(elapsed);
                    yield return null;
                    elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                }
            }
            finally
            {
                IsPlaying = false; score.Stop();
                if (root != null) root.gameObject.SetActive(false);
            }
        }

        public void Skip() { skipRequested = true; IsPlaying = false; score.Stop(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; IsPlaying = false; skipRequested = true; score.Dispose();
            if (root != null) { root.gameObject.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject); else UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        public void RenderPreview(Sprite parentA, Sprite parentB, MonsterDataSO born, OwnedMonsterData created, float seconds)
        {
            if (disposed || root == null) return;
            root.gameObject.SetActive(true);
            IsUpper = UsesUpperPresentation(born != null ? born.classRank : 1);
            float duration = DurationForClass(born != null ? born.classRank : 1);
            float p = Mathf.Clamp01(float.IsNaN(seconds) ? 0f : seconds / duration);
            float t = p * duration;
            Layout();
            Color accent = IsUpper ? (settings != null ? settings.UpperAccent : new Color(1f,.83f,.4f)) : (settings != null ? settings.NormalAccent : new Color(.58f,.92f,1f));
            float glow = IsUpper ? (settings != null ? settings.UpperGlow : 1f) : (settings != null ? settings.NormalGlow : .7f);
            string effectFolder = IsUpper ? "EffectsUpper/" : "Effects/";
            sealLeft.sprite = sealRight.sprite = sealCenter.sprite = outerSeal.sprite = Effect(effectFolder,"Seal");
            ribbonLeft.sprite = ribbonRight.sprite = Effect(effectFolder,"Ribbon");
            leftGlow.sprite = rightGlow.sprite = Effect(effectFolder,"Cocoon");
            burst.sprite = Effect(effectFolder,"Burst"); pillar.sprite = Effect(effectFolder,"Pillar");
            leftParent.sprite = parentA; rightParent.sprite = parentB;
            if (lastBorn != born)
            {
                lastBorn = born;
                bornAttackFrames = born != null ? BattleVisualResolver.ResolveMonsterAttackSprites(born) : null;
                MeasureBornSequence(born);
            }
            // Keep one authored sequence from arrival through the resting pose.
            // Battle idle and portrait art can have different framing and padding.
            Sprite result = bornAttackFrames != null && bornAttackFrames.Count > 0 ? bornAttackFrames[0] : Portrait(born);
            if (p >= .79f && p < .91f && bornAttackFrames != null && bornAttackFrames.Count > 1)
                result = bornAttackFrames[Mathf.Clamp(Mathf.FloorToInt(U(.79f,.91f,p) * bornAttackFrames.Count),0,bornAttackFrames.Count-1)];
            else if (p >= .91f && bornAttackFrames != null && bornAttackFrames.Count > 1)
                result = bornAttackFrames[bornAttackFrames.Count-1];
            bornImage.sprite = silhouette.sprite = result;
            float parentsIn = S(.12f,.22f,p), dissolve = S(.37f,.50f,p);
            float parentAlpha = parentsIn * (1f - dissolve);
            float ionaAlpha = (1f-S(.49f,.57f,p)) * S(0,.08f,p);
            // One fixed body drawing and registered arm joints replace the
            // independently redrawn frames. The gesture has no frame cadence.
            iona.Render(U(.22f,.51f,p),ionaAlpha,settings != null ? settings.IonaHeightPixels : 1200f);
            Vector2 leftPalm = iona.GetPalmInStage(true), rightPalm = iona.GetPalmInStage(false);
            Pose(leftParent,Vector2.Lerp(new Vector2(-315,-520),leftPalm,dissolve),Mathf.Lerp(.86f,.68f,dissolve),parentAlpha);
            Pose(rightParent,Vector2.Lerp(new Vector2(315,-520),rightPalm,dissolve),Mathf.Lerp(.86f,.68f,dissolve),parentAlpha);
            Pose(leftGlow,leftParent.rectTransform.anchoredPosition,1f+dissolve*.4f,parentAlpha*glow*(.35f+.45f*dissolve),-t*19);
            Pose(rightGlow,rightParent.rectTransform.anchoredPosition,1f+dissolve*.4f,parentAlpha*glow*(.35f+.45f*dissolve),t*19);
            Pose(sealLeft,new Vector2(-315,-700),1f,S(.02f,.12f,p)*(1f-S(.40f,.53f,p))*.85f,-t*12);
            Pose(sealRight,new Vector2(315,-700),1f,S(.045f,.15f,p)*(1f-S(.40f,.53f,p))*.85f,t*12);
            float convergence = S(.30f,.43f,p) * (1f-S(.54f,.60f,p));
            Pose(ribbonLeft,Vector2.Lerp(new Vector2(-195,-230),leftPalm,S(.34f,.50f,p)),.85f,convergence*glow,-22+t*3);
            Pose(ribbonRight,Vector2.Lerp(new Vector2(195,-230),rightPalm,S(.34f,.50f,p)),.85f,convergence*glow,158-t*3);
            float build = S(.45f,.60f,p), hush = S(.675f,.72f,p), birth = S(.755f,.825f,p);
            // The energy follows the authored palm coordinates. Once the
            // gesture settles this stays fixed for the cocoon and birth.
            Vector2 center = (leftPalm + rightPalm) * .5f;
            float aliveRing = S(.43f,.58f,p) * (1f-S(.94f,1f,p));
            Pose(sealCenter,center,.92f+.05f*Mathf.Sin(t*1.3f),aliveRing*(.6f-.15f*hush),t*(hush>.5f ? 4 : 11));
            Pose(outerSeal,center,1f+.06f*birth,IsUpper ? aliveRing*.68f : 0f,-t*9);
            int cocoonFrame = p < .675f ? 0 : p < .755f ? Mathf.Clamp(Mathf.FloorToInt(U(.675f,.755f,p)*3),0,2) : Mathf.Clamp(3+Mathf.FloorToInt(U(.755f,.825f,p)*5),3,7);
            cocoon.sprite = IsUpper && upperCocoonFrames[cocoonFrame] != null ? upperCocoonFrames[cocoonFrame] : cocoonFrames[cocoonFrame];
            float cocoonAlpha = build * (1f-S(.80f,.85f,p));
            float pulse = p < .755f ? Mathf.Sin(t*7f)*.012f*(1f-hush*.8f) : 0;
            Pose(cocoon,center,.62f+.38f*build+pulse,cocoonAlpha);
            PoseBorn(silhouette,center,.83f, S(.60f,.65f,p)*(1f-S(.70f,.735f,p))*.3f);
            silhouette.color = new Color(.018f,.018f,.038f,silhouette.color.a);
            float reveal = S(.755f,.80f,p);
            PoseBorn(bornImage,new Vector2(center.x,Mathf.Lerp(center.y,65,S(.755f,.89f,p))),Mathf.Lerp(.91f,1.0f,S(.755f,.82f,p)),reveal);
            Pose(pillar,new Vector2(0,120),1f,IsUpper ? S(.75f,.765f,p)*(1f-S(.82f,.92f,p))*.8f : 0);
            Pose(burst,center,Mathf.Lerp(.6f,1.35f,U(.755f,.85f,p)),S(.75f,.762f,p)*(1f-S(.77f,.85f,p))*glow);
            float cutAlpha=IsUpper && (settings==null || settings.UpperCutInEnabled) ? S(.59f,.603f,p)*(1f-S(.661f,.675f,p)) : 0;
            Pose(cutin,Vector2.zero,1f,cutAlpha);
            float ignition = S(.49f,.504f,p)*(1f-S(.506f,.525f,p));
            float release = S(.75f,.758f,p)*(1f-S(.76f,.782f,p));
            flash.color = new Color(accent.r,accent.g,accent.b,Mathf.Max(ignition*.13f,release*(IsUpper?.55f:.30f)));
            shade.color = new Color(.005f,.003f,.025f,Mathf.Lerp(.50f,.08f,S(0,.15f,p))+.25f*hush*(1f-birth));
            RenderParticles(p,t,center,glow,effectFolder);
            float textAlpha = S(.85f,.91f,p);
            nameLabel.text = born != null ? born.monsterName : "新しい仲間";
            classLabel.text = "CLASS " + (born != null ? born.classRank : 1) + "   ·   Lv." + (created != null ? created.Level : 1)
                + (created != null && created.TotalPlusValue>0 ? "   +"+created.TotalPlusValue : "");
            inheritanceLabel.text = Inheritance(created);
            parentLabel.text = "ふたりから受け継いだ力と、ともに。";
            nameLabel.color = new Color(1f,.98f,.91f,textAlpha);
            classLabel.color = new Color(accent.r,accent.g,accent.b,textAlpha);
            inheritanceLabel.color = new Color(.92f,.94f,1f,textAlpha);
            parentLabel.color = new Color(.78f,.82f,.90f,textAlpha);
            float shake = IsUpper ? (settings!=null ? settings.UpperShakePixels : 5f) : 1.5f;
            float jolt = release * shake;
            stage.anchoredPosition = new Vector2(Mathf.Sin(t*91)*jolt,Mathf.Cos(t*67)*jolt);
            skip.gameObject.SetActive(p < 1f);
        }

        private void RenderParticles(float p,float t,Vector2 center,float glow,string effectFolder)
        {
            int count = IsUpper ? (settings!=null?settings.UpperParticles:36) : (settings!=null?settings.NormalParticles:20);
            float visibility = S(.18f,.34f,p)*(1f-S(.93f,1f,p));
            for(int i=0;i<particles.Length;i++)
            {
                float seed = (i*.61803398875f)%1f;
                float life = Mathf.Repeat(t*.43f+seed,1f);
                Vector2 start = new Vector2((i%2==0?-1:1)*(285+seed*65),-610);
                Vector2 control = new Vector2((i%2==0?1:-1)*(110+seed*110),-60);
                Vector2 point = (1-life)*(1-life)*start+2*(1-life)*life*control+life*life*center;
                if(p>=.755f)
                {
                    float angle = i*2.399963f;
                    float radius = 120+U(.755f,1,p)*(240+seed*370);
                    point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * radius;
                }
                particles[i].sprite=Effect(effectFolder,"Spark");
                Pose(particles[i],point,.45f+seed*.8f,i<count? visibility*glow*Mathf.Sin(life*Mathf.PI)*.85f:0,t*20+i*31);
            }
        }

        private void MeasureBornSequence(MonsterDataSO born)
        {
            // Union alpha bounds once per sequence. Per-frame recentering would
            // move the torso whenever an arm or wing extends.
            var frames = bornAttackFrames != null && bornAttackFrames.Count > 0
                ? bornAttackFrames : new List<Sprite> { Portrait(born) };
            Vector2 min = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                var metrics = BattleVisualResolver.ResolveSpriteVisualMetrics(frame);
                float size = Mathf.Max(1f,Mathf.Max(metrics.SpriteWidth,metrics.SpriteHeight));
                Vector2 bottom = metrics.OpaqueBottomCenterFromSpriteCenter / size;
                min = Vector2.Min(min,bottom-new Vector2(metrics.OpaqueWidth/size*.5f,0));
                max = Vector2.Max(max,bottom+new Vector2(metrics.OpaqueWidth/size*.5f,metrics.OpaqueHeight/size));
            }
            if (float.IsInfinity(min.x)) { bornContentCenter=Vector2.zero;bornContentScale=680f;return; }
            bornContentCenter=(min+max)*.5f;
            bornContentScale=680f/Mathf.Max(.01f,Mathf.Max(max.x-min.x,max.y-min.y));
        }

        private void PoseBorn(Image image,Vector2 center,float scale,float alpha)
        {
            if (image.sprite != null)
            {
                Vector2 size=image.sprite.rect.size;
                image.rectTransform.sizeDelta=size/Mathf.Max(size.x,size.y)*bornContentScale;
            }
            Pose(image,center-bornContentCenter*bornContentScale*scale,scale,alpha);
        }

        private void Layout()
        {
            float width=parent.rect.width>0?parent.rect.width:1080;
            float height=parent.rect.height>0?parent.rect.height:1920;
            float fit=Mathf.Min(width/AuthorSize.x,height/AuthorSize.y);
            stage.localScale=Vector3.one*fit;
            float aspect=chamber.sprite!=null ? chamber.sprite.rect.width/chamber.sprite.rect.height : 9f/16f;
            chamber.rectTransform.sizeDelta = height*aspect>=width?new Vector2(height*aspect,height):new Vector2(width,width/aspect);
            float safeTop=Application.isPlaying && Screen.height>0 ? Mathf.Max(0,Screen.height-Screen.safeArea.yMax)*height/Screen.height : 0;
            skip.GetComponent<RectTransform>().anchoredPosition=new Vector2(-28,-Mathf.Max(32,safeTop+22));
        }
        private Sprite Load(string path) { if(!art.TryGetValue(path,out var s)){s=Resources.Load<Sprite>(Art+path);art[path]=s;}return s; }
        private Sprite Effect(string folder,string name) => Load(folder+name) ?? Load("Effects/"+name);
        private static float U(float a,float b,float p)=>Mathf.Clamp01((p-a)/(b-a));
        private static float S(float a,float b,float p){float u=U(a,b,p);return u*u*(3-2*u);}
        private static void Pose(Image image,Vector2 position,float scale,float alpha,float angle=0)
        {
            image.rectTransform.anchoredPosition=position;image.rectTransform.localScale=Vector3.one*scale;
            image.rectTransform.localRotation=Quaternion.Euler(0,0,angle);image.color=new Color(1,1,1,Mathf.Clamp01(alpha));
            image.enabled=image.sprite!=null && alpha>0;
        }
        private static RectTransform Rect(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);go.layer=parent.gameObject.layer;
            var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);return r;
        }
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static Image Picture(string name,Transform parent,Sprite sprite,Vector2 size)
        {
            var rect=Rect(name,parent);rect.sizeDelta=size;var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        private static Text Label(string name,Transform parent,int fontSize,Vector2 pos,Vector2 size,Color color)
        {
            var rect=Rect(name,parent);rect.anchoredPosition=pos;rect.sizeDelta=size;var label=rect.gameObject.AddComponent<Text>();
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.fontStyle=FontStyle.Bold;label.alignment=TextAnchor.MiddleCenter;
            label.color=color;label.raycastTarget=false;label.resizeTextForBestFit=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.015f,.01f,.03f,.9f);outline.effectDistance=new Vector2(2,-2);return label;
        }
        private static Sprite Portrait(MonsterDataSO data)
        {
            if(data==null)return null;
            if(data.illustrationSprite!=null)return data.illustrationSprite;
            if(data.portraitSprite!=null)return data.portraitSprite;
            return Resources.Load<Sprite>(!string.IsNullOrEmpty(data.illustrationResourcePath)?data.illustrationResourcePath:data.portraitResourcePath);
        }
        private static string Inheritance(OwnedMonsterData created)
        {
            if(created==null)return "";
            var entries=new List<string>();
            if(created.FusionBonusHp>0)entries.Add("HP +"+created.FusionBonusHp);
            if(created.FusionBonusAttack>0)entries.Add("攻撃 +"+created.FusionBonusAttack);
            if(created.FusionBonusWisdom>0)entries.Add("魔力 +"+created.FusionBonusWisdom);
            if(created.FusionBonusDefense>0)entries.Add("防御 +"+created.FusionBonusDefense);
            if(created.FusionBonusMagicDefense>0)entries.Add("魔防 +"+created.FusionBonusMagicDefense);
            if(created.FusionBonusAttackSpeed>0)entries.Add("攻速 +"+created.FusionBonusAttackSpeed.ToString("0.##"));
            return entries.Count>0 ? "受け継いだ力\n"+string.Join("   ",entries) : "新しい仲間が誕生";
        }
    }
}
