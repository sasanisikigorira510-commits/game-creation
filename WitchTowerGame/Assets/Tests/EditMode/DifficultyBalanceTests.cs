using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace WitchTower.Tests
{
    public sealed class DifficultyBalanceTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);
        private static object S(string type,string method,params object[] args) => T(type).GetMethod(method).Invoke(null,args);
        private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method).Invoke(obj,args);
        private static object P(object obj,string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static void Set(object obj,string name,object value) => obj.GetType().GetProperty(name).SetValue(obj,value);
        private static IList List(object obj,string name) => (IList)P(obj,name);
        private UnityEngine.Random.State previousRandomState;
        private GameObject owner;
        private object game;
        private object master;
        private object oldGame;
        private object oldMaster;
        private object oldSave;
        private SceneSetup[] previousScenes;

        [SetUp] public void Setup()
        {
            previousRandomState=UnityEngine.Random.state;
            previousScenes=EditorSceneManager.GetSceneManagerSetup();
            oldGame=T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster=T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave=T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            owner=new GameObject("DifficultyTestManagers"); owner.SetActive(false);
            game=owner.AddComponent(T("Managers.GameManager"));
            master=owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null,game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null,null);
            Call(master,"Initialize");
            S("Data.GuardianTrialSession","Reset");
        }
        [TearDown] public void Cleanup()
        {
            UnityEngine.Random.state=previousRandomState;
            S("Data.GuardianTrialSession","Reset");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null,oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null,oldSave);
            if(previousScenes.Length>0 && previousScenes.All(s=>!string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        }
        private object Profile(int floor=30)
        {
            var save=S("Save.PlayerSaveData","CreateDefault");
            save.GetType().GetField("HighestFloor").SetValue(save,floor);
            save.GetType().GetField("HasCompletedTutorial").SetValue(save,true);
            save.GetType().GetField("TutorialStepId").SetValue(save,"Complete");
            var p=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{save});
            Set(game,"PlayerProfile",p); Call(game,"SetCurrentFloor",floor+1);
            return p;
        }
        private static readonly string[][] Parties = {
            new[]{"monster_rock_golem","monster_apprentice_swordsman","monster_dragon_whelp","monster_chibi_gear","monster_apprentice_mage"},
            new[]{"monster_ore_giant_garm","monster_holy_armor_leon","monster_flare_drake","monster_armed_droid","monster_dark_robe_curse_mage_noah"},
            new[]{"monster_cosmic_ore_fortress_golem","monster_sword_saint_alvarez","monster_abyss_dragon","monster_omega_leon","monster_abyss_grand_mage_seraphis"},
            new[]{"monster_fortress_machine_gigafort","monster_rock_knight_gaius","monster_mecha_dragon_valdrake","monster_magic_sword_saint_luciel","monster_abyss_dragon_mage_valflare"}
        };

        private void BuildParty(object p, int rank, int level, int plus = 0, int individual = 100)
        {
            List(p,"PartyMonsterInstanceIds").Clear();
            foreach (string id in Parties[rank-1])
            {
                Assert.That(Call(master,"GetMonsterData",id),Is.Not.Null,id);
                var monster=Call(p,"AddOwnedMonster",id,level,plus,false);
                foreach(string stat in new[]{"IndividualHp","IndividualAttack","IndividualWisdom","IndividualDefense","IndividualMagicDefense","IndividualAttackSpeed"})
                    monster.GetType().GetField(stat).SetValue(monster,individual);
                List(p,"PartyMonsterInstanceIds").Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
        }

        private void EquipParty(object p)
        {
            int index=0;
            foreach(string monsterId in List(p,"PartyMonsterInstanceIds"))
            {
                var monster=Call(p,"GetOwnedMonster",monsterId);
                var data=Call(master,"GetMonsterData",monster.GetType().GetField("MonsterId").GetValue(monster));
                bool magic=data.GetType().GetField("damageType").GetValue(data).ToString()=="Magic";
                foreach(string id in new[]{magic?"equip_c4_abyss_grimoire":"equip_frost_greatsword","equip_ice_dragon_armor","equip_c4_eclipse_core"})
                {
                    var gear=Call(p,"AddOwnedEquipmentWithInstancePrefix",id,Enum.Parse(T("MasterData.EquipmentRarity"),"Epic"),"balance_");
                    Assert.That(gear,Is.Not.Null);
                    S("Data.EquipmentStatRollService","RollInitialStats",Call(master,"GetEquipmentData",id),gear,new System.Random(5020+index++));
                    Assert.That(Call(p,"EquipEquipmentToMonster",monsterId,gear.GetType().GetField("InstanceId").GetValue(gear)),Is.True);
                }
            }
        }

        // Real simulator, maximum individual values and fixed seeds. No paid upgrades,
        // skill commands or companion unless this is an oath; equipment is explicitly selected.
        [Test]
        public void MeasureProgressionAndTrialMatchups()
        {
            foreach(int floor in new[]{1,10,11,20,21,30,31,40,41,50,51,60})
            {
                int rank=floor<=20?1:floor<=40?2:3;
                foreach(int level in new[]{5,rank*10,rank*20}) Measure(floor,rank,level,null);
            }
            foreach(string id in new[]{"seiryu","suzaku","byakko","genbu"})
                foreach(int level in new[]{5,25,50}) Measure(30,3,level,id);
            foreach(string id in new[]{"seiryu","suzaku","byakko","genbu"})
                foreach(int plus in new[]{0,20,40}) Measure(60,4,80,id,plus);
            foreach(int floor in new[]{40,50,60})
            {
                Measure(floor,4,80,null,40,true);
                Measure(floor,4,80,null,80,true);
            }
            foreach(int floor in new[]{20,30,40,50,60})
            {
                int rank=floor==20?2:floor<=40?3:4;
                foreach(int plus in new[]{0,20,40}) Measure(floor,rank,rank*20,null,plus);
            }
        }

        [TestCase(1,29,7,4,28,11)]
        [TestCase(10,53,12,6,42,38)]
        public void ChapterOneStatsAndRewardsStayUnchanged(int floor,int hp,int attack,int defense,int exp,int gold)
        {
            var enemy=S("Data.BattleDungeonCatalog","CreateEnemyDataForMonsterAtGlobalFloor",floor,master,"monster_dragon_whelp");
            try
            {
                Assert.That(F(enemy,"maxHp"),Is.EqualTo(hp));
                Assert.That(F(enemy,"attack"),Is.EqualTo(attack));
                Assert.That(F(enemy,"defense"),Is.EqualTo(defense));
                Assert.That((float)F(enemy,"attackSpeed"),Is.EqualTo(.9504f).Within(.0001f));
                Assert.That(F(enemy,"rewardExp"),Is.EqualTo(exp));
                Assert.That(F(enemy,"rewardGold"),Is.EqualTo(gold));
            }
            finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)enemy); }
        }

        [TestCase(20,85,73)] [TestCase(30,280,108)] [TestCase(40,635,143)]
        [TestCase(50,951,178)] [TestCase(60,1547,213)]
        public void StageExperienceIncreaseKeepsGoldAndCombatDifficultyIndependent(int floor,int exp,int gold)
        {
            var enemy=S("Data.BattleDungeonCatalog","CreateEnemyDataForMonsterAtGlobalFloor",floor,master,"monster_dragon_whelp");
            try
            {
                Assert.That(F(enemy,"rewardExp"),Is.EqualTo(exp));
                Assert.That(F(enemy,"rewardGold"),Is.EqualTo(gold));
            }
            finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)enemy); }
        }

        [TestCase(1, 2f, 1.8f, 1.25f, 1.10f)]
        [TestCase(2, 2f, 1.8f, 1.35f, 1.12f)]
        [TestCase(3, 1.8f, 1.65f, 1.35f, 1.14f)]
        [TestCase(4, 1.65f, 1.5f, 1.4f, 1.16f)]
        [TestCase(5, 1.5f, 1.4f, 1.45f, 1.18f)]
        public void LaterDungeonsReceiveOnlySmallHpAndOffenseIncrease(int chapter, float oldHp, float oldAttack, float oldDefense, float oldSpeed)
        {
            var enemy = ScriptableObject.CreateInstance(T("MasterData.EnemyDataSO"));
            try
            {
                foreach (string name in new[] { "maxHp", "attack", "magicAttack", "defense", "magicDefense" })
                    enemy.GetType().GetField(name).SetValue(enemy, 100);
                enemy.GetType().GetField("attackSpeed").SetValue(enemy, 1f);
                enemy.GetType().GetField("rewardGold").SetValue(enemy, 73);
                enemy.GetType().GetField("rewardExp").SetValue(enemy, 85);
                T("Data.BattleDungeonCatalog").GetMethod("ApplyChapterCombatDifficulty", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { enemy, chapter, 1 });
                Assert.That(F(enemy, "maxHp"), Is.EqualTo(Mathf.RoundToInt(100 * oldHp * 1.10f)));
                Assert.That(F(enemy, "attack"), Is.EqualTo(Mathf.RoundToInt(100 * oldAttack * 1.05f)));
                Assert.That(F(enemy, "magicAttack"), Is.EqualTo(Mathf.RoundToInt(100 * oldAttack * 1.05f)));
                Assert.That(F(enemy, "defense"), Is.EqualTo(Mathf.RoundToInt(100 * oldDefense)));
                Assert.That(F(enemy, "magicDefense"), Is.EqualTo(Mathf.RoundToInt(100 * oldDefense)));
                Assert.That((float)F(enemy, "attackSpeed"), Is.EqualTo(oldSpeed));
                Assert.That(F(enemy, "rewardGold"), Is.EqualTo(73));
                Assert.That(F(enemy, "rewardExp"), Is.EqualTo(85));
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy); }
        }

        [TestCase(20,1,20,0,false,0)] [TestCase(20,2,40,0,false,5)]
        [TestCase(30,2,40,0,false,0)] [TestCase(30,3,60,0,false,5)]
        [TestCase(40,3,60,0,false,0)] [TestCase(40,3,60,40,false,5)]
        // Dungeon 5's modest HP/offense increase makes an unequipped, unboosted
        // class-4 party at its level cap an unreliable clear (2 / 5 fixed seeds).
        // Prepared gear/+40 parties remain covered by guaranteed-clear cases.
        [TestCase(50,3,60,0,false,0)] [TestCase(50,4,80,0,false,2)]
        [TestCase(60,4,80,40,false,0)] [TestCase(60,4,80,40,true,5)]
        public void LaterChaptersRequireGrowthAndRemainBeatable(int floor,int rank,int level,int plus,bool gear,int wins)
        {
            Assert.That(Measure(floor,rank,level,null,plus,gear).Wins,Is.EqualTo(wins));
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void InitialTrialRejectsFreshFusionsButRewardsTraining(string id)
        {
            Assert.That(Measure(30,3,5,id).Wins,Is.Zero);
            Assert.That(Measure(30,3,25,id).Wins,Is.Zero);
            var prepared=Measure(30,3,50,id);
            Assert.That(prepared.Wins,Is.EqualTo(5));
            Assert.That(prepared.Seconds,Is.InRange(15f,60f),"Prepared parties must experience the guardian's attack phases.");
            Assert.That(Measure(30,3,60,id,20,false,false,50).Wins,Is.EqualTo(5),"Perfect individual values must not be required.");
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void FinalAcquisitionIsAChallengeButBeatableWithoutAnotherGuardian(string id)
        {
            var result=Measure(60,4,80,id,40);
            Assert.That(result.Wins,Is.EqualTo(5));
            Assert.That(result.Seconds,Is.InRange(20f,120f));
        }

        [TestCase("seiryu")] [TestCase("suzaku")] [TestCase("byakko")] [TestCase("genbu")]
        public void OathsAreStrongerThanAcquisitionAndBeatableWithPreparedParty(string id)
        {
            var acquisition=S("Data.GuardianService","TrialEnemy",id,60);
            var oath=S("Data.GuardianService","SessionEnemy",id,60,Enum.Parse(T("Data.GuardianTrialMode"),"Oath"),0,30);
            Assert.That((int)F(oath,"maxHp"),Is.GreaterThan((int)F(acquisition,"maxHp")));
            Assert.That((int)F(oath,"attack"),Is.GreaterThan((int)F(acquisition,"attack")));
            var result=Measure(60,4,80,id,40,true,true);
            Assert.That(result.Wins,Is.EqualTo(5));
            Assert.That(result.Seconds,Is.InRange(15f,180f));
        }

        [Test]
        public void EscortDifficultyUsesAcquisitionTierAndPracticeRetainsItsLowDamage()
        {
            var acquisition=Enum.Parse(T("Data.GuardianTrialMode"),"Acquisition");
            var first=S("Data.GuardianService","SessionEnemy","suzaku",30,acquisition,1,1);
            var fourth=S("Data.GuardianService","SessionEnemy","suzaku",60,acquisition,1,1);
            Assert.That((int)F(fourth,"maxHp"),Is.GreaterThan((int)F(first,"maxHp")));
            Assert.That((int)F(fourth,"attack"),Is.GreaterThan((int)F(first,"attack")));
            foreach(string mode in new[]{"PracticeGroup","PracticeElite"})
            {
                var practice=S("Data.GuardianService","SessionEnemy","suzaku",60,Enum.Parse(T("Data.GuardianTrialMode"),mode),0,30);
                Assert.That(F(practice,"attack"),Is.EqualTo(10));
                Assert.That(F(practice,"magicAttack"),Is.EqualTo(10));
            }
        }

        [Test]
        public void FinalChapterCanBeClearedWithoutPerfectIndividualValuesOrPaidUpgrades()
        {
            Assert.That(Measure(60,4,80,null,40,true,false,50).Wins,Is.EqualTo(5));
        }

        private static object F(object obj,string name) => obj.GetType().GetField(name).GetValue(obj);

        private (int Wins, float Seconds) Measure(int floor,int rank,int level,string guardian,int plus=0,bool equipment=false,bool oath=false,int individual=100)
        {
            int wins=0; float totalSeconds=0; int survivors=0;
            for(int seed=0;seed<5;seed++)
            {
                S("Data.GuardianTrialSession","Reset");
                UnityEngine.Random.InitState(9020+seed);
                var p=Profile(Mathf.Max(30,floor)); BuildParty(p,rank,level,plus,individual);
                if(equipment) EquipParty(p);
                if(guardian!=null)
                {
                    if(oath)
                    {
                        S("Data.GuardianService","GrantCore",p,guardian);
                        S("Data.GuardianService","Birth",p,guardian);
                        S("Data.GuardianService","Equip",p,guardian);
                        var owned=S("Data.GuardianService","Owned",p,guardian);
                        owned.GetType().GetField("Level").SetValue(owned,30);
                        List(p,"SeenStoryEventIds").Add("story_first_arc_complete");
                        Assert.That(S("Data.GuardianTrialSession","BeginOath",p,guardian),Is.True);
                    }
                    else
                    {
                    // Acquisitions use a fixed ownership tier, regardless of guardian identity.
                    foreach(string other in new[]{"seiryu","suzaku","byakko","genbu"}.Where(x=>x!=guardian).Take((floor-30)/10))
                        S("Data.GuardianService","GrantCore",p,other);
                    Assert.That(S("Data.GuardianTrialSession","Begin",p,guardian),Is.True);
                    }
                }
                var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",floor);
                var tickMethod=simulator.GetType().GetMethod("Tick");
                string result="None"; int steps=0;
                while(steps<12000 && result=="None") { result=tickMethod.Invoke(simulator,new object[]{.05f}).ToString(); steps++; }
                if(result=="Win") wins++;
                totalSeconds+=steps*.05f;
                survivors+=(int)P(simulator,"CurrentAliveAllyCount");
                Assert.That(result,Is.Not.EqualTo("None"),"Combat must terminate.");
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)simulator);
            }
            TestContext.WriteLine($"BALANCE floor={floor} rank={rank} level={level} trial={guardian??"none"} plus={plus} equipment={equipment} individual={individual} oath={oath} wins={wins}/5 seconds={totalSeconds/5:F1} survivors={survivors/5f:F1}");
            return (wins,totalSeconds/5);
        }
    }
}
