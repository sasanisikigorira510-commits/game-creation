using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

namespace WitchTower.Tests
{
    public sealed class GuardianProgressionTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);
        private static object S(string type,string method,params object[] args) => T(type).GetMethod(method).Invoke(null,args);
        private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method).Invoke(obj,args);
        private static object P(object obj,string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static void Set(object obj,string name,object value) => obj.GetType().GetProperty(name).SetValue(obj,value);
        private static void Field(object obj,string name,object value) => obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        private static object Private(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        private static IList List(object obj,string name) => (IList)P(obj,name);
        private GameObject owner;
        private object game;
        private object master;
        private object oldGame;
        private object oldMaster;
        private object oldSave;
        private SceneSetup[] previousScenes;

        [SetUp] public void Setup()
        {
            previousScenes=EditorSceneManager.GetSceneManagerSetup();
            oldGame=T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster=T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave=T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            owner=new GameObject("GuardianTestManagers"); owner.SetActive(false);
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
        private void Party(object p,int level=20)
        {
            foreach(var id in new[]{"monster_flare_drake","monster_rock_golem","monster_apprentice_mage","monster_apprentice_swordsman","monster_chibi_gear"})
            {
                var monster=Call(p,"AddOwnedMonster",id,level,0,false);
                List(p,"PartyMonsterInstanceIds").Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
        }
        private object Restore(object p)
        {
            var save=Call(p,"ToSaveData",31);
            string json=JsonUtility.ToJson(save);
            var loaded=JsonUtility.FromJson(json,T("Save.PlayerSaveData"));
            var restored=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{loaded});
            Set(game,"PlayerProfile",restored); return restored;
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void TrialCoreBirthAndDedicatedSlotResumeWithoutConsumingMonsters(string id)
        {
            var p=Profile(29); Party(p);
            Assert.That(S("Data.GuardianTrialSession","Begin",p,id),Is.False);
            Assert.That(S("Data.GuardianService","Birth",p,id),Is.False);
            Set(p,"HighestFloor",30);
            Call(game,"SetCurrentFloor",31);
            string before=JsonUtility.ToJson(Call(p,"ToSaveData",31));
            int gold=(int)P(p,"Gold");
            var party=List(p,"PartyMonsterInstanceIds").Cast<string>().ToArray();
            Assert.That(S("Data.GuardianService","NeedsTutorial",p),Is.True);
            Assert.That(S("Data.StoryTutorialService","GetNextEvent",p,"HomeScene"),Is.Not.Null,"Optional guardians must not suppress chapter or fusion guidance.");
            Assert.That(S("Data.GuardianTrialSession","Begin",p,id),Is.True);
            S("Data.GuardianTrialSession","End");
            Assert.That(List(p,"GuardianCoreIds"),Is.Empty,"Retreat cannot award a core.");
            Assert.That(S("Data.GuardianTrialSession","Begin",p,id),Is.True,"Retry costs nothing.");
            Assert.That(S("Data.GuardianTrialSession","Win",p),Is.True);
            Assert.That(S("Data.GuardianTrialSession","Win",p),Is.False,"Repeated result callbacks must not duplicate rewards.");
            S("Data.GuardianTrialSession","End");
            p=Restore(p);
            Assert.That(List(p,"GuardianCoreIds").Cast<string>(),Is.EqualTo(new[]{id}));
            Assert.That(S("Data.GuardianService","Birth",p,id),Is.True);
            Assert.That(S("Data.GuardianService","Birth",p,id),Is.False);
            p=Restore(p);
            Assert.That(List(p,"OwnedGuardians").Count,Is.EqualTo(1));
            Assert.That(List(p,"GuardianCoreIds"),Is.Empty);
            Assert.That(S("Data.GuardianService","Equip",p,id),Is.True);
            p=Restore(p);
            Assert.That(P(p,"EquippedGuardianId"),Is.EqualTo(id));
            Assert.That(S("Data.GuardianService","NeedsTutorial",p),Is.False);
            Assert.That(S("Data.StoryTutorialService","GetNextEvent",p,"HomeScene"),Is.Not.Null);
            Assert.That(List(p,"OwnedMonsters").Count,Is.EqualTo(5));
            Assert.That(List(p,"PartyMonsterInstanceIds").Cast<string>(),Is.EqualTo(party));
            Assert.That(P(p,"Gold"),Is.EqualTo(gold));
            Assert.That(P(p,"HighestFloor"),Is.EqualTo(30));
            Assert.That(P(game,"CurrentFloor"),Is.EqualTo(31));
        }

        [Test]
        public void OwnershipCapacityAndIndividualExperienceRemainIndependent()
        {
            var p=Profile();
            Assert.That(S("Data.GuardianService","GrantCore",p,"invalid"),Is.False);
            Assert.That(S("Data.GuardianService","GrantCore",p,"seiryu"),Is.True);
            Assert.That(S("Data.GuardianService","GrantCore",p,"suzaku"),Is.False);
            S("Data.GuardianService","Birth",p,"seiryu"); S("Data.GuardianService","Equip",p,"seiryu");
            Assert.That(S("Data.GuardianService","GrantCore",p,"suzaku"),Is.False);
            Set(p,"HighestFloor",40);
            Assert.That(S("Data.GuardianService","GrantCore",p,"suzaku"),Is.True);
            S("Data.GuardianService","Birth",p,"suzaku");
            S("Data.GuardianService","AddBattleExperience",p,2400);
            var first=S("Data.GuardianService","Owned",p,"seiryu");
            var second=S("Data.GuardianService","Owned",p,"suzaku");
            Assert.That((int)first.GetType().GetField("Level").GetValue(first),Is.GreaterThan(1));
            Assert.That(second.GetType().GetField("Level").GetValue(second),Is.EqualTo(1),"Only the companion that fought receives experience.");
            S("Data.GuardianService","Equip",p,"suzaku");
            Assert.That(P(p,"EquippedGuardianId"),Is.EqualTo("suzaku"));
            p=Restore(p);
            Assert.That(List(p,"OwnedGuardians").Count,Is.EqualTo(2));
            Assert.That(List(p,"PartyMonsterInstanceIds"),Is.Empty);
            Set(p,"HighestFloor",50); Assert.That(S("Data.GuardianService","Capacity",p),Is.EqualTo(3));
            Set(p,"HighestFloor",60); Assert.That(S("Data.GuardianService","Capacity",p),Is.EqualTo(4));
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void GuardianIsSixthCombatantAndUsesSkillsAutomatically(string id)
        {
            var p=Profile(); Party(p);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator"));
            Call(simulator,"Setup",21);
            Assert.That(Call(simulator,"HasAllyRuntime",5),Is.False);
            S("Data.GuardianService","GrantCore",p,id); S("Data.GuardianService","Birth",p,id); S("Data.GuardianService","Equip",p,id);
            Call(simulator,"Setup",21);
            Assert.That(Call(simulator,"HasAllyRuntime",5),Is.True);
            Assert.That(P(simulator,"CurrentAliveAllyCount"),Is.EqualTo(6));
            Assert.That(Call(simulator,"GetAllyMonsterId",5),Is.EqualTo("guardian_"+id));
            Assert.That(Call(simulator,"GetAllyMaxHp",5),Is.GreaterThan(0));
            foreach(var type in Enum.GetValues(T("Battle.BattleSpiritType"))) Assert.That(Call(simulator,"TryInvokeSpirit",type),Is.False);
            UnityEngine.Random.InitState(318);
            string result="None";
            for(int i=0;i<6000 && result=="None";i++) result=Call(simulator,"Tick",.05f).ToString();
            TestContext.WriteLine(id+" battle="+result+" skills="+P(simulator,"GuardianSkillCount")+" blocked="+P(simulator,"GuardianDamageBlocked"));
            Assert.That((int)P(simulator,"GuardianSkillCount"),Is.GreaterThan(0));
            Assert.That(result,Is.Not.EqualTo("None"),"Battle must terminate with six units.");
            if(id!="genbu") Assert.That((int)P(simulator,"GuardianDamageDealt"),Is.GreaterThan(0));
            else Assert.That((int)P(simulator,"GuardianDamageBlocked"),Is.GreaterThan(0));
            Assert.That(List(p,"PartyMonsterInstanceIds").Count,Is.EqualTo(5));
        }

        [TestCase("seiryu",1)][TestCase("suzaku",1)][TestCase("byakko",1)][TestCase("genbu",1)]
        [TestCase("seiryu",5)][TestCase("suzaku",5)][TestCase("byakko",5)][TestCase("genbu",5)]
        public void GuardiansStayBehindPartyAndAttackDistantEnemiesWithoutMoving(string id,int partySize)
        {
            var p=Profile(); Party(p);
            var party=List(p,"PartyMonsterInstanceIds");
            while(party.Count>partySize) party.RemoveAt(party.Count-1);
            S("Data.GuardianService","GrantCore",p,id); S("Data.GuardianService","Birth",p,id); S("Data.GuardianService","Equip",p,id);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",21);
            var home=(Vector2)Call(simulator,"GetAllyHomeAnchor",5);
            for(int i=0;i<partySize;i++) Assert.That(home.x,Is.LessThan(((Vector2)Call(simulator,"GetAllyHomeAnchor",i)).x));
            Assert.That(Call(simulator,"GetAllyAttackRange",5),Is.EqualTo(float.PositiveInfinity));
            Call(simulator,"TickPreparation",.5f);
            var allies=(IList)simulator.GetType().GetField("activeAllyRuntimes",Hidden).GetValue(simulator);
            var guardian=allies.Cast<object>().Single(a=>(int)a.GetType().GetField("SlotIndex").GetValue(a)==5);
            var enemies=(IList)simulator.GetType().GetField("activeEnemyRuntimes",Hidden).GetValue(simulator);
            Assert.That(enemies.Count,Is.GreaterThan(0));
            foreach(var enemy in enemies)
            {
                enemy.GetType().GetField("PositionAnchor").SetValue(enemy,new Vector2(12,-8));
                var stats=enemy.GetType().GetField("Stats").GetValue(enemy);
                stats.GetType().GetField("MaxHp").SetValue(stats,100000);
                stats.GetType().GetField("CurrentHp").SetValue(stats,100000);
            }
            Assert.That(Private(simulator,"CanAllyAttackTarget",guardian,0),Is.True);
            Assert.That(Private(simulator,"CanAllyAttackTarget",allies[0],0),Is.False,"Ordinary allies must retain their range limits.");
            Private(simulator,"TickAllyAttackers",3f);
            Assert.That(Call(simulator,"GetEnemyCurrentHp",0),Is.LessThan(100000),"Normal attacks must reach distant targets.");
            Private(simulator,"TickGuardianSkill",8f);
            Assert.That(P(simulator,"GuardianSkillCount"),Is.EqualTo(1),"Skills also reach distant targets, including with fewer than five ordinary allies.");
            if(id=="genbu") Assert.That(Private(simulator,"AbsorbGuardianBarrier",0,100),Is.LessThan(100));
            else Assert.That((int)P(simulator,"GuardianDamageDealt"),Is.GreaterThan(0));
            for(int i=0;i<100;i++)
            {
                Private(simulator,"TickUnitMovement",.05f);
                Assert.That(Call(simulator,"GetAllyPositionAnchor",5),Is.EqualTo(home));
                Assert.That(Call(simulator,"IsAllyMoving",5),Is.False);
            }
            var guardianStats=guardian.GetType().GetField("Stats").GetValue(guardian);
            guardianStats.GetType().GetField("CurrentHp").SetValue(guardianStats,0);
            Assert.That(Private(simulator,"CanAllyAttackTarget",guardian,0),Is.False,"Unlimited range must not allow a defeated guardian to attack.");
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void TrialGuardianAttacksWithoutRequiringAnApproach(string id)
        {
            var p=Profile(); Party(p);
            S("Data.GuardianTrialSession","Begin",p,id);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",30);
            Call(simulator,"TickPreparation",.5f);
            var enemies=(IList)simulator.GetType().GetField("activeEnemyRuntimes",Hidden).GetValue(simulator);
            var allies=(IList)simulator.GetType().GetField("activeAllyRuntimes",Hidden).GetValue(simulator);
            var enemy=enemies[0];
            var home=(Vector2)Call(simulator,"GetEnemyPositionAnchor",0);
            // The avatar must reach even an isolated back-row survivor without moving.
            foreach(var ally in allies)
            {
                ally.GetType().GetField("HomeAnchor").SetValue(ally,new Vector2(.05f,.15f));
                ally.GetType().GetField("PositionAnchor").SetValue(ally,new Vector2(.05f,.15f));
            }
            Assert.That(Private(simulator,"CanEnemyAttackTarget",enemy,0,0),Is.True);
            int hpBefore=Enumerable.Range(0,5).Sum(i=>(int)Call(simulator,"GetAllyCurrentHp",i));
            for(int i=0;i<100;i++)
            {
                Private(simulator,"TickGuardianTrial",.05f);
                Private(simulator,"TickUnitMovement",.05f);
                Private(simulator,"TickEnemyAttackers",.05f);
                Assert.That(Call(simulator,"GetEnemyPositionAnchor",0),Is.EqualTo(home));
                Assert.That(Call(simulator,"IsEnemyMoving",0),Is.False);
            }
            int hpAfter=Enumerable.Range(0,5).Sum(i=>(int)Call(simulator,"GetAllyCurrentHp",i));
            var target=(int)Private(simulator,"ResolveCachedEnemyTargetAllyIndex",enemy,0);
            TestContext.WriteLine(id+" HP="+hpBefore+" -> "+hpAfter+" enemy="+Call(simulator,"GetEnemyPositionAnchor",0)+" target="+target+" ally="+Call(simulator,"GetAllyPositionAnchor",target)+" canAttack="+Private(simulator,"CanEnemyAttackTarget",enemy,0,target));
            Assert.That(hpAfter,Is.LessThan(hpBefore),"The trial avatar must inflict real damage during the opening, not wait indefinitely at melee range.");
            var stats=enemy.GetType().GetField("Stats").GetValue(enemy);
            stats.GetType().GetField("CurrentHp").SetValue(stats,0);
            Assert.That(Private(simulator,"CanEnemyAttackTarget",enemy,0,0),Is.False,"A defeated avatar cannot attack.");
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void EveryMeleeSlotCanReachTheStationaryTrialGuardian(string id)
        {
            var p=Profile();
            for(int i=0;i<5;i++)
            {
                var monster=Call(p,"AddOwnedMonster","monster_apprentice_swordsman",20,0,false);
                List(p,"PartyMonsterInstanceIds").Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            S("Data.GuardianTrialSession","Begin",p,id);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",30);
            Call(simulator,"TickPreparation",.5f);
            var allies=(IList)simulator.GetType().GetField("activeAllyRuntimes",Hidden).GetValue(simulator);
            for(int i=0;i<200;i++) Private(simulator,"TickUnitMovement",.05f);
            for(int i=0;i<5;i++) Assert.That(Private(simulator,"CanAllyAttackTarget",allies[i],0),Is.True,"Melee slot "+i+" must reach the giant's near edge.");
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void TrialRespondsToTheReportedClassFourParty(string id)
        {
            var p=Profile();
            string[] ids={"monster_cosmic_ore_fortress_golem","monster_fortress_machine_gigafort","monster_abyss_dragon","monster_magic_sword_saint_luciel","monster_abyss_grand_mage_seraphis"};
            int[] levels={4,3,4,3,4};
            for(int i=0;i<ids.Length;i++)
            {
                var monster=Call(p,"AddOwnedMonster",ids[i],levels[i],0,false);
                List(p,"PartyMonsterInstanceIds").Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            S("Data.GuardianTrialSession","Begin",p,id);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",30);
            var hits=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(T("Battle.BattleHitInfo")));
            var hitEvent=simulator.GetType().GetEvent("HitResolved");
            hitEvent.AddEventHandler(simulator,Delegate.CreateDelegate(hitEvent.EventHandlerType,hits,hits.GetType().GetMethod("Add")));
            string result="None"; float elapsed=0;
            UnityEngine.Random.InitState(318);
            for(int i=0;i<6000 && result=="None";i++) {result=Call(simulator,"Tick",.05f).ToString();elapsed+=.05f;}
            int avatarHits=hits.Cast<object>().Count(h=>(bool)P(h,"TargetIsPlayer") && (int)P(h,"AttackerIndex")==0);
            TestContext.WriteLine(id+" result="+result+" seconds="+elapsed+" avatar attacks="+avatarHits);
            Assert.That(avatarHits,Is.GreaterThan(0),"The reported party must see the guardian respond before defeating it.");
            Assert.That(result,Is.EqualTo("Lose"),"Freshly fused class-3/4 monsters must train before earning a guardian.");
        }

        [TestCase("seiryu")][TestCase("suzaku")][TestCase("byakko")][TestCase("genbu")]
        public void InitialTrialIsRealCombatWithGuaranteedCoreOnlyAfterVictory(string id)
        {
            var p=Profile();
            foreach(var monsterId in new[]{"monster_cosmic_ore_fortress_golem","monster_sword_saint_alvarez","monster_abyss_dragon","monster_omega_leon","monster_abyss_grand_mage_seraphis"})
            {
                var monster=Call(p,"AddOwnedMonster",monsterId,60,20,false);
                foreach(string stat in new[]{"IndividualHp","IndividualAttack","IndividualWisdom","IndividualDefense","IndividualMagicDefense","IndividualAttackSpeed"})
                    monster.GetType().GetField(stat).SetValue(monster,50);
                List(p,"PartyMonsterInstanceIds").Add(monster.GetType().GetField("InstanceId").GetValue(monster));
            }
            Assert.That(S("Data.GuardianTrialSession","Begin",p,id),Is.True);
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",20);
            Assert.That(P(simulator,"CurrentEnemyCountTarget"),Is.EqualTo(id == "suzaku" ? 3 : 1));
            UnityEngine.Random.InitState(318);
            string result="None";
            for(int i=0;i<6000 && result=="None";i++) result=Call(simulator,"Tick",.05f).ToString();
            TestContext.WriteLine(id+" trial="+result+" partyHP="+((object)P(simulator,"PlayerStats")).GetType().GetField("CurrentHp").GetValue(P(simulator,"PlayerStats")));
            Assert.That(result,Is.EqualTo("Win"),"Initial trial should be attainable without owning a guardian.");
            Assert.That(List(p,"GuardianCoreIds"),Is.Empty,"Simulation alone must not award or persist a core.");
            Assert.That(S("Data.GuardianTrialSession","Win",p),Is.True);
            Assert.That(P(p,"HighestFloor"),Is.EqualTo(30));
            Assert.That(P(game,"CurrentFloor"),Is.EqualTo(31));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BattlePresentationShowsDedicatedGuardianAndTrialResultsAreIsolated(bool trial, bool victory)
        {
            var p=Profile(); Party(p);
            if(trial) S("Data.GuardianTrialSession","Begin",p,"seiryu");
            else { S("Data.GuardianService","GrantCore",p,"seiryu"); S("Data.GuardianService","Birth",p,"seiryu"); S("Data.GuardianService","Equip",p,"seiryu"); }
            var simulator=owner.AddComponent(T("Battle.BattleSimulator")); Call(simulator,"Setup",trial?20:21);
            for(int i=0;i<40;i++) Call(simulator,"Tick",.05f);
            var machine=owner.AddComponent(T("Battle.BattleStateMachine"));
            Field(machine,"simulator",simulator);
            var hud=owner.AddComponent(T("Battle.BattleHudController")); Field(machine,"hudController",hud);
            var controller=owner.AddComponent(T("Battle.BattleSceneController")); Field(controller,"stateMachine",machine); Field(controller,"currentFloor",trial?20:21);
            var canvas=new GameObject("GuardianBattleCapture",typeof(RectTransform),typeof(Canvas));
            Field(controller,"minimalCanvasRoot",canvas);
            Private(controller,"EnsureMinimalCanvas");
            Private(controller,"ApplyCombatantVisuals",trial?20:21);
            Private(controller,"ApplyBackdropForFloor",trial?20:21);
            Private(controller,"UpdatePreviewLayoutFromSimulator",simulator);
            Private(controller,"UpdateGuardianBattlePanel");
            var monster=S("Data.GuardianService","Monster","seiryu");
            var idle=(IList)S("Battle.BattleVisualResolver","ResolveMonsterIdleSprites",monster);
            Assert.That(idle.Count,Is.EqualTo(8));
            Assert.That(((Sprite)idle[0]).rect.width,Is.EqualTo(384));
            Assert.That(((Sprite)idle[0]).rect.height,Is.EqualTo(384));
            if(!trial)
            {
                var images=(IList)controller.GetType().GetField("allyPreviewImages",Hidden).GetValue(controller);
                Assert.That(images.Count,Is.EqualTo(6));
                Assert.That(((Image)images[5]).sprite,Is.Not.Null);
                Assert.That(((Image)images[5]).sprite.name,Does.StartWith("Idle_"));
            }
            Capture(canvas,trial?"trial-battle":"companion-battle");
            if(trial)
            {
                string before = ProfileWithoutGuardianCores(p);
                Call(controller, victory ? "OnBattleWin" : "OnBattleLose");
                Assert.That(List(p,"GuardianCoreIds").Count,Is.EqualTo(victory ? 1 : 0));
                var buttons=canvas.GetComponentsInChildren<Button>(true);
                Assert.That(buttons.Single(b=>b.name=="NextFloorButton").gameObject.activeSelf,Is.False);
                Assert.That(buttons.Single(b=>b.name=="RetryFloorButton").gameObject.activeSelf,Is.False);
                string settled = JsonUtility.ToJson(Call(p,"ToSaveData",31));
                // A result is final. Repeated or contradictory callbacks must
                // neither award a defeated trial nor undo/duplicate a won core.
                for(int i=0;i<10;i++)
                {
                    Call(controller, victory ? "OnBattleLose" : "OnBattleWin");
                    Call(controller, victory ? "OnBattleWin" : "OnBattleLose");
                }
                Assert.That(List(p,"GuardianCoreIds").Count,Is.EqualTo(victory ? 1 : 0));
                Assert.That(JsonUtility.ToJson(Call(p,"ToSaveData",31)),Is.EqualTo(settled));
                var result = controller.GetType().GetField("lastResultViewData",Hidden).GetValue(controller);
                Assert.That(P(result,"IsWin"),Is.EqualTo(victory));
                Assert.That(ProfileWithoutGuardianCores(p),Is.EqualTo(before),
                    "Trials cannot change currency, experience, inventory, formation, story or dungeon progress.");
                Assert.That(P(p,"HighestFloor"),Is.EqualTo(30));
                Assert.That(P(game,"CurrentFloor"),Is.EqualTo(31));
                Capture(canvas,victory ? "trial-victory" : "trial-defeat");
            }
        }

        private static string ProfileWithoutGuardianCores(object profile)
        {
            var save = Call(profile,"ToSaveData",31);
            ((IList)save.GetType().GetField("GuardianCoreIds").GetValue(save)).Clear();
            return JsonUtility.ToJson(save);
        }

        [Test]
        public void SanctuaryHasReadableCardsAndResumesBirthAndEquipSteps()
        {
            var p=Profile();
            var canvas=new GameObject("SanctuaryTestCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var controller=canvas.AddComponent(T("Home.GuardianSanctuaryController"));
            Call(controller,"Initialize"); Call(controller,"Open");
            ConfigurePortraitCanvas(canvas);
            Assert.That(canvas.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("GuardianCard_")),Is.EqualTo(4));
            AssertReadableSanctuaryLabels(canvas);
            Capture(canvas,"choice");
            S("Data.GuardianService","GrantCore",p,"seiryu"); Call(controller,"Open");
            ConfigurePortraitCanvas(canvas);
            Assert.That(canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="SelectedDetails").text,Does.Contain("神核"));
            AssertReadableSanctuaryLabels(canvas);
            Capture(canvas,"core");
            canvas.GetComponentsInChildren<Button>().Single(b=>b.name=="GuardianPrimaryAction").onClick.Invoke();
            Assert.That(List(p,"OwnedGuardians").Count,Is.EqualTo(1));
            Assert.That((bool)P(controller,"IsBirthPlaying"),Is.True);
            Assert.That(P(p,"EquippedGuardianId"),Is.Not.EqualTo("seiryu"),"Birth and formation remain separate actions.");
            Capture(canvas,"birth");
            canvas.GetComponentsInChildren<Button>().Single(b=>b.name=="GuardianBirthSkip").onClick.Invoke();
            Assert.That((bool)P(controller,"IsBirthPlaying"),Is.False);
            var formation=canvas.GetComponentsInChildren<Button>().Single(b=>b.name=="GuardianPrimaryAction");
            Assert.That(formation.GetComponentInChildren<Text>().text,Does.Contain("編成"));
            formation.onClick.Invoke();
            Assert.That(P(p,"EquippedGuardianId"),Is.EqualTo("seiryu"));
            Assert.That(List(p,"OwnedGuardians").Count,Is.EqualTo(1));
            ConfigurePortraitCanvas(canvas);
            AssertReadableSanctuaryLabels(canvas);
            Capture(canvas,"equipped");
        }

        private static void ConfigurePortraitCanvas(GameObject canvas)
        {
            canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var scaler=canvas.GetComponent<CanvasScaler>();
            if(scaler!=null) scaler.enabled=false;
            var rect=(RectTransform)canvas.transform;
            rect.sizeDelta=new Vector2(1080,1920); rect.position=Vector3.zero; rect.localScale=Vector3.one;
            var safe=canvas.transform.Find("GuardianSanctuaryModal/GuardianSafeArea") as RectTransform;
            if(safe!=null) { safe.anchorMin=Vector2.zero; safe.anchorMax=Vector2.one; safe.offsetMin=safe.offsetMax=Vector2.zero; }
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertReadableSanctuaryLabels(GameObject canvas)
        {
            foreach(var label in canvas.GetComponentsInChildren<Text>())
            {
                if(string.IsNullOrWhiteSpace(label.text)) continue;
                int minimum=label.resizeTextForBestFit ? label.resizeTextMinSize : label.fontSize;
                Assert.That(minimum,Is.GreaterThanOrEqualTo(19),label.name+" must retain readable typography.");
                Assert.That(label.rectTransform.rect.width,Is.GreaterThan(0),label.name);
                // Measure the entire string with wrapping at the smallest
                // permitted size. Best-fit itself is valid; clipping is not.
                var settings=label.GetGenerationSettings(new Vector2(label.rectTransform.rect.width,0));
                settings.resizeTextForBestFit=false;
                settings.fontSize=minimum;
                settings.horizontalOverflow=HorizontalWrapMode.Wrap;
                settings.verticalOverflow=VerticalWrapMode.Overflow;
                float required=new TextGenerator().GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
                Assert.That(required,Is.LessThanOrEqualTo(label.rectTransform.rect.height+2),label.name+": "+label.text);
            }
        }
        private void Capture(GameObject canvas,string name)
        {
            string folder=Environment.GetEnvironmentVariable("WITCHTOWER_GUARDIAN_CAPTURE");
            if(string.IsNullOrEmpty(folder)) return;
            ConfigurePortraitCanvas(canvas);
            var rect=(RectTransform)canvas.transform;
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{rect,1080,1920,System.IO.Path.Combine(folder,name+".png")});
        }
    }
}
