using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class MonsterMotionCatalogTests
    {
        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);

        [TestCase("armed_droid_idle", new[] {559,579,545,556})]
        [TestCase("armed_droid_attack", new[] {557,549,635,553})]
        [TestCase("sword_saint_alvarez_idle", new[] {573,705,670,517})]
        [TestCase("sword_saint_alvarez_move", new[] {641,630,634,611})]
        [TestCase("abyss_grand_mage_seraphis_move", new[] {583,590,628,641})]
        [TestCase("flare_drake_attack", new[] {502,556,638,491})]
        public void RecropsPreserveReviewedWholePoseWidthsAndMargins(string clip,int[] widths)
        {
            for(int f=0;f<4;f++)
            {
                var sprite=Resources.Load<Sprite>($"MonsterBattle/mon_{clip}_{f}");
                Assert.That(sprite,Is.Not.Null);
                var texture=sprite.texture;
                Assert.That(texture.width,Is.EqualTo(800)); Assert.That(texture.height,Is.EqualTo(659));
                var pixels=texture.GetPixels32(); int l=800,r=-1,b=659,t=-1;
                for(int y=0;y<659;y++) for(int x=0;x<800;x++)
                {
                    if(pixels[y*800+x].a<=12) continue;
                    l=Math.Min(l,x); r=Math.Max(r,x); b=Math.Min(b,y); t=Math.Max(t,y);
                }
                Assert.That(r-l+1,Is.EqualTo(widths[f]).Within(1),clip+" frame "+f+": padding alone must not conceal a clipped pose");
                Assert.That(Math.Min(Math.Min(l,799-r),Math.Min(b,658-t)),Is.GreaterThanOrEqualTo(12));
            }
        }

        [Test]
        public void EveryRegisteredMonsterHasThreeCompleteRenderableClips()
        {
            var master=Resources.Load("MasterData/MasterDataRoot",Runtime("MasterData.MasterDataRoot"));
            var monsters=(IEnumerable)master.GetType().GetField("monsterDataList").GetValue(master);
            var resolver=Runtime("Battle.BattleVisualResolver");
            var preview=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp-Editor")
                .GetType("MonsterMotionPreviewWindow",true);
            var poseType=Runtime("Battle.BattleVisualPose");
            var go=new GameObject("IsolatedMotionTest",typeof(RectTransform),typeof(Image));
            try
            {
                int count=0;
                foreach(var monster in monsters)
                {
                    count++;
                    var idle=resolver.GetMethod("ResolveMonsterIdleSprites").Invoke(null,new[]{monster});
                    int p=0;
                    foreach(string pose in new[]{"Idle","Move","Attack"})
                    {
                        var frames=(IList)resolver.GetMethod("ResolveMonster"+pose+"Sprites").Invoke(null,new[]{monster});
                        Assert.That(frames.Count,Is.EqualTo(4),monster+" / "+pose);
                        foreach(Sprite sprite in frames)
                        {
                            Assert.That(sprite,Is.Not.Null);
                            foreach(float facing in new[]{-1f,1f})
                            {
                                go.transform.localScale=new Vector3(facing,1,1);
                                preview.GetMethod("ApplyFrame").Invoke(null,new[]{go.GetComponent<Image>(),monster,Enum.ToObject(poseType,p),sprite,frames,idle});
                                var rect=(RectTransform)go.transform;
                                Assert.That(float.IsNaN(rect.anchoredPosition.x)||float.IsInfinity(rect.anchoredPosition.x),Is.False);
                                Assert.That(float.IsNaN(rect.anchoredPosition.y)||float.IsInfinity(rect.anchoredPosition.y),Is.False);
                                Assert.That(rect.sizeDelta.x,Is.GreaterThan(0)); Assert.That(rect.sizeDelta.y,Is.GreaterThan(0));
                            }
                        }
                        p++;
                    }
                }
                Assert.That(count,Is.EqualTo(33));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
