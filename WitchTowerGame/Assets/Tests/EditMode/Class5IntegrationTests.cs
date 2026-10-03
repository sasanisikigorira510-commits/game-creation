using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class Class5IntegrationTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object F(object o, string n) => o.GetType().GetField(n, Any).GetValue(o);
        private static object S(string t, string m, params object[] args) => T(t).GetMethods(Any).Single(x => x.Name == m && x.GetParameters().Length == args.Length).Invoke(null,args);
        private object master, oldMaster;
        private GameObject owner;
        [SetUp] public void Setup()
        {
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            owner = new GameObject("Class5IntegrationOwner"); owner.SetActive(false);
            master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,master);
            master.GetType().GetMethod("Initialize").Invoke(master,null);
        }
        [TearDown] public void Cleanup()
        {
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,oldMaster);
            UnityEngine.Object.DestroyImmediate(owner);
        }
        private object Monster(string key) => master.GetType().GetMethod("GetMonsterData").Invoke(master,new object[]{"monster_"+key});
        [TestCase("astravarn",34,1)] [TestCase("ordion",35,4)] [TestCase("geoatlas",36,2)]
        [TestCase("regnard",37,1)] [TestCase("noxveil",38,4)] [TestCase("celestia",39,1)] [TestCase("yggdrasia",40,3)]
        public void RegisteredFinalFormsUseApprovedArtAndSkillAssets(string key,int number,int targets)
        {
            var data = Monster(key); Assert.That(data,Is.Not.Null);
            Assert.That(F(data,"classRank"),Is.EqualTo(5)); Assert.That(F(data,"encyclopediaNumber"),Is.EqualTo(number));
            Assert.That(F(data,"normalAttackTargetCount"),Is.EqualTo(targets)); Assert.That(F(data,"fusionExclusive"),Is.True);
            Assert.That(Resources.Load<Sprite>("FamilyMonsters/Class5/"+key),Is.Not.Null);
            foreach(string pose in new[]{"idle","move","attack","skill"})
            {
                var frames = ((IEnumerable)S("Battle.BattleVisualResolver","ResolveSpriteFramesFromResourcePath","MonsterBattle/mon_"+key+"_"+pose)).Cast<Sprite>().ToArray();
                Assert.That(frames,Has.Length.EqualTo(4),key+" "+pose);
                Assert.That(frames.All(x=>AssetDatabase.GetAssetPath(x).StartsWith("Assets/Resources/MonsterBattle/mon_"+key)),Is.True);
            }
            foreach(string pose in new[]{"attack","skill"})
                Assert.That(((IEnumerable)S("Battle.BattleVisualResolver","ResolveSpriteFramesFromResourcePath","BattleEffects/Monster/fx_"+key+"_"+pose)).Cast<Sprite>().Count(),Is.EqualTo(4));
            foreach(string pose in new[]{"Idle","Move","Attack","Skill"})
                Assert.That(S("Battle.BattleVisualResolver","ResolveMonsterFacing",data,Enum.Parse(T("Battle.BattleVisualPose"),pose)).ToString(),Is.EqualTo("Right"));
            Assert.That(S("MasterData.MonsterGrowthUtility","AreEqual",F(data,"plusGrowth"),S("MasterData.MonsterGrowthUtility","ResolvePlusGrowth",data)),Is.True);
            foreach(bool paid in new[]{false,true})
                Assert.That(((IEnumerable)S("Home.GachaPanelController","CollectSummonPool",paid)).Cast<object>().Any(x=>(string)F(x,"monsterId")=="monster_"+key),Is.False);
        }
        [TestCase("monster_seraph_michael","monster_spirit_queen_titania","monster_celestia")]
        [TestCase("monster_spirit_queen_titania","monster_seraph_michael","monster_yggdrasia")]
        public void AngelSpiritParentOrderChoosesDifferentFinalForm(string a,string b,string expected)
        {
            var data = master.GetType().GetMethod("GetFusionResult").Invoke(master,new object[]{a,b});
            Assert.That(F(data,"monsterId"),Is.EqualTo(expected));
            object[] args={a,b,null,true}; Assert.That(S("Data.MonsterFusionCatalog","TryResolveRecipe",args),Is.True);
            Assert.That(args[2].GetType().GetProperty("IgnoreOrder").GetValue(args[2]),Is.False);
        }
        [Test]
        public void AutomaticSkillHitHasDistinctPresentationFromPlayerSkill()
        {
            var controller=owner.AddComponent(T("Battle.BattleSceneController"));
            var data=Monster("ordion"); var hitType=T("Battle.BattleHitInfo");
            var constructor=hitType.GetConstructors().Single(c=>c.GetParameters().Length==11);
            object NormalHit(string skillId)=>constructor.Invoke(new object[]{false,100,false,true,false,0,0,0f,1,1,skillId});
            object Effect(object hit)=>controller.GetType().GetMethod("ResolveMonsterAttackEffect",Any).Invoke(controller,new[]{hit,data});
            Assert.That(F(Effect(NormalHit(null)),"ResourcePath"),Is.EqualTo("BattleEffects/Monster/fx_ordion_attack"));
            Assert.That(F(Effect(NormalHit("class5_skill_ordion")),"ResourcePath"),Is.EqualTo("BattleEffects/Monster/fx_ordion_skill"));
        }
    }
}
