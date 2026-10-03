using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class GarzaBossTests
    {
        private const string Id = "monster_demon_king_garza";
        private const string EnemyId = "enemy_demon_king_garza";
        private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(string type, string method, params object[] args) =>
            Runtime(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);

        [Test]
        public void FinalFloorHasOneFixedGarzaAndEarlierBossesAreUnchanged()
        {
            Assert.That((string[])Call("Data.BattleDungeonCatalog", "ResolveBossMonsterIds", 60), Is.EqualTo(new[]{Id}));
            for (int floor=1; floor<60; floor++)
                Assert.That((string[])Call("Data.BattleDungeonCatalog", "ResolveBossMonsterIds", floor), Does.Not.Contain(Id));
            for (int floor=1; floor<=60; floor++)
            {
                Assert.That((string[])Call("Data.BattleDungeonCatalog", "ResolveRecruitableMonsterIds", floor), Does.Not.Contain(Id));
                Assert.That(Call("Data.BattleDungeonCatalog", "IsRecruitableMonsterOnFloor", floor, Id), Is.False);
            }
            Assert.That(Call("Data.BattleDungeonCatalog", "ResolveMonsterIdFromEnemyId", EnemyId), Is.EqualTo(Id));
        }

        [Test]
        public void RealBossDataHasVisualsButCannotBeGrantedEvenIfRecruitFlagIsAccidentallyEnabled()
        {
            var go = new GameObject("IsolatedGarzaMaster"); go.SetActive(false);
            var manager = go.AddComponent(Runtime("Managers.MasterDataManager"));
            manager.GetType().GetMethod("Initialize").Invoke(manager, null);
            var boss = (ScriptableObject)Call("Data.BattleDungeonCatalog", "CreateEnemyDataForMonsterAtGlobalFloor",60,manager,Id);
            var state = UnityEngine.Random.state;
            try
            {
                Assert.That(boss, Is.Not.Null);
                Assert.That(Field(boss,"enemyId"), Is.EqualTo(EnemyId));
                Assert.That(Field(boss,"enemyName"), Is.EqualTo("魔王ガルザ"));
                Assert.That(Field(boss,"canBeRecruited"), Is.False);
                Assert.That((int)Field(boss,"maxHp"), Is.GreaterThan(0));
                Assert.That(manager.GetType().GetMethod("GetMonsterData").Invoke(manager,new object[]{Id}),Is.Null,
                    "Garza must not enter summon, fusion or compendium master lists.");
                Assert.That((IList)Call("Battle.BattleVisualResolver","ResolveEnemyAttackSprites",boss),Has.Count.EqualTo(8));
                object save=Call("Save.PlayerSaveData","CreateDefault");
                object profile=Activator.CreateInstance(Runtime("Data.PlayerProfile"),new[]{save});
                var none=Enum.Parse(Runtime("Battle.MonsterRecruitBlockReason"),"None");
                foreach(bool accidentalFlag in new[]{false,true})
                {
                    boss.GetType().GetField("canBeRecruited").SetValue(boss,accidentalFlag);
                    string before=JsonUtility.ToJson(save);
                    for(int i=0;i<100;i++)
                    {
                        var result=Call("Battle.MonsterRecruitService","ResolveAfterEnemyDefeat",60,profile,none,boss,true);
                        Assert.That(result.GetType().GetProperty("Attempted").GetValue(result),Is.False);
                        Assert.That(result.GetType().GetProperty("Succeeded").GetValue(result),Is.False);
                    }
                    Assert.That(JsonUtility.ToJson(save),Is.EqualTo(before));
                }
            }
            finally { UnityEngine.Random.state=state; UnityEngine.Object.DestroyImmediate(boss); UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase("idle",4)] [TestCase("move",4)] [TestCase("attack",8)]
        [TestCase("hit",2)] [TestCase("defeat",2)]
        public void AuthoredClipsAreCompletePixelSpritesWithoutClipping(string pose,int count)
        {
            var frames=((IEnumerable)Call("Data.GarzaBossPresentation","Frames",pose)).Cast<Sprite>().ToArray();
            Assert.That(frames,Has.Length.EqualTo(count));
            foreach(var frame in frames)
            {
                var t=frame.texture; Assert.That(t.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(t.mipmapCount,Is.EqualTo(1));
                var p=t.GetPixels32();int width=t.width,height=t.height;
                Assert.That(p.Any(c=>c.a==255),Is.True);
                for(int x=0;x<width;x++) { Assert.That(p[x].a,Is.Zero); Assert.That(p[(height-1)*width+x].a,Is.Zero); }
                for(int y=0;y<height;y++) { Assert.That(p[y*width].a,Is.Zero); Assert.That(p[y*width+width-1].a,Is.Zero); }
            }
        }

        [Test]
        public void AttackChangesDoNotRecenterOrResizeTheBody()
        {
            var method=Runtime("Battle.BattleSceneController").GetMethod("ApplyPreviewVisualLayout",BindingFlags.NonPublic|BindingFlags.Static);
            var mode=Enum.Parse(Runtime("Battle.BattleSceneController").GetNestedType("PreviewMeasurementMode",BindingFlags.NonPublic),"FullSprite");
            var go=new GameObject("GarzaLayout",typeof(RectTransform),typeof(Image));
            try
            {
                var image=go.GetComponent<Image>(); Vector2? size=null,position=null;
                foreach(var frame in ((IEnumerable)Call("Data.GarzaBossPresentation","Frames","attack")).Cast<Sprite>())
                {
                    image.sprite=frame;
                    method.Invoke(null,new object[]{image,new Vector2(200,220),Vector2.zero,null,mode});
                    if(size==null) { size=image.rectTransform.sizeDelta;position=image.rectTransform.anchoredPosition; }
                    Assert.That(image.rectTransform.sizeDelta,Is.EqualTo(size.Value));
                    Assert.That(image.rectTransform.anchoredPosition,Is.EqualTo(position.Value));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void CutsceneActionsRecoverAndDefeatHoldsWithoutLeakingToOtherStories()
        {
            Assert.That(Call("Data.GarzaBossPresentation","StoryPose","story_first_arc_complete",8),Is.EqualTo("attack"));
            Assert.That(Call("Data.GarzaBossPresentation","StoryPose","story_first_arc_complete",16),Is.EqualTo("defeat"));
            Assert.That(Call("Data.GarzaBossPresentation","StoryPose","story_chapter_2_unlocked",8),Is.Null);
            Assert.That(((Sprite)Call("Data.GarzaBossPresentation","Frame","attack",5f)).name,Does.StartWith("garza_idle"));
            Assert.That(((Sprite)Call("Data.GarzaBossPresentation","Frame","defeat",5f)).name,Is.EqualTo("garza_defeat_1"));
            foreach(string pose in new[]{"projectile","impact"})
                Assert.That((IList)Call("Battle.BattleVisualResolver","LoadSpriteFrames","BattleEffects/Garza/"+pose),Has.Count.EqualTo(4));
        }
    }
}
