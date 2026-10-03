using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class BattlePermanentEffectsTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private SceneSetup[] scenes;
        private object oldGame, oldSave, profile;
        private Component battle, panel;
        private RectTransform canvas;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+name,true);
        private static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden|BindingFlags.Public).Invoke(o,args);
        private static object Field(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        private static void SetField(object o,string name,object v)=>o.GetType().GetField(name,Hidden).SetValue(o,v);
        private static void Set(object o,string name,object v)=>o.GetType().GetProperty(name).SetValue(o,v);
        private static object Get(object o,string name)=>o.GetType().GetProperty(name).GetValue(o);
        private static void Singleton(Type t,object v)=>t.GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,v);
        [SetUp] public void Setup()
        {
            scenes=EditorSceneManager.GetSceneManagerSetup();
            oldGame=T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldSave=T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Singleton(T("Managers.SaveManager"),null);
            var gameObject=new GameObject("IsolatedGame"); gameObject.SetActive(false);
            var game=gameObject.AddComponent(T("Managers.GameManager")); Singleton(game.GetType(),game);
            profile=Activator.CreateInstance(T("Data.PlayerProfile"),Activator.CreateInstance(T("Save.PlayerSaveData")));
            Set(game,"PlayerProfile",profile); Set(profile,"HasCompletedTutorial",true);
            Set(profile,"PaidGachaStones",1234);
            var battleObject=new GameObject("InactiveBattle"); battleObject.SetActive(false);
            battle=battleObject.AddComponent(T("Battle.BattleSceneController")); ((Behaviour)battle).enabled=false;
            var root=new GameObject("TestCanvas",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            canvas=(RectTransform)root.transform; canvas.sizeDelta=new Vector2(1080,2341);
            var go=new GameObject("BattlePermanentEffectsRoot",typeof(RectTransform)); go.transform.SetParent(canvas,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.sizeDelta=Vector2.zero;
            panel=go.AddComponent(T("Battle.BattlePermanentEffectsPanel")); Call(panel,"Initialize",battle);
        }
        [TearDown] public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Singleton(T("Managers.GameManager"),oldGame); Singleton(T("Managers.SaveManager"),oldSave);
            if(scenes.Length>0&&scenes.All(s=>!string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }
        private void OwnAll()
        {
            foreach(var prop in new[]{"HasAutoRepeatFloorUpgrade","HasAutoSellEquipmentUpgrade","HasAutoReleaseMonsterUpgrade"}) Set(profile,prop,true);
            Call(panel,"Refresh");
        }
        [Test] public void UnownedControlsNeverPurchaseOrEnableEffects()
        {
            Call(panel,"Open");
            for(int i=0;i<3;i++) Call(panel,"Toggle",i);
            Assert.That(Get(profile,"PaidGachaStones"),Is.EqualTo(1234));
            Assert.That(Get(profile,"HasAutoRepeatFloorUpgrade"),Is.False);
            Assert.That(((Button[])Field(panel,"toggles")).All(b=>!b.interactable),Is.True);
        }
        [Test] public void RepeatArmsCurrentFightWithoutRestartingAndOffClearsCountdown()
        {
            OwnAll(); SetField(battle,"currentFloor",7);
            Assert.That(Call(battle,"SetBattleAutoRepeatEnabled",true),Is.True);
            Assert.That(Call(battle,"IsAutoRepeatSameFloorActive"),Is.True);
            Assert.That(Field(battle,"currentFloor"),Is.EqualTo(7));
            Assert.That(Field(battle,"autoRepeatRestartQueued"),Is.False);
            SetField(battle,"autoRepeatRestartQueued",true); SetField(battle,"autoRepeatRestartTimer",1f);
            Call(battle,"SetBattleAutoRepeatEnabled",false);
            Assert.That(Field(battle,"autoRepeatRestartQueued"),Is.False);
            Assert.That(Field(battle,"autoRepeatRestartTimer"),Is.EqualTo(0f));
        }
        [Test] public void ResultActivationQueuesExistingRestartAndTutorialCannotStart()
        {
            OwnAll(); SetField(battle,"resultHandled",true);
            Call(battle,"SetBattleAutoRepeatEnabled",true);
            Assert.That(Field(battle,"autoRepeatRestartQueued"),Is.True);
            Call(battle,"SetBattleAutoRepeatEnabled",false); Set(profile,"HasCompletedTutorial",false);
            Assert.That(Call(battle,"SetBattleAutoRepeatEnabled",true),Is.False);
        }
        [Test] public void ModalOnlyTogglesOwnedSettingsAndRoundTripsTheirSaveState()
        {
            OwnAll(); Call(panel,"Toggle",1); Assert.That(Get(profile,"IsAutoSellEquipmentUpgradeEnabled"),Is.False);
            Call(panel,"Open");
            for(int i=0;i<3;i++) Call(panel,"Toggle",i);
            var restored=Activator.CreateInstance(T("Data.PlayerProfile"),Call(profile,"ToSaveData",1));
            foreach(var prop in new[]{"IsAutoRepeatFloorUpgradeEnabled","IsAutoSellEquipmentUpgradeEnabled","IsAutoReleaseMonsterUpgradeEnabled"})
                Assert.That(Get(restored,prop),Is.True,prop);
            Call(panel,"Close"); Assert.That(Get(panel,"IsOpen"),Is.False);
            Assert.That(Get(profile,"PaidGachaStones"),Is.EqualTo(1234));
        }
        [Test] public void RetireConfirmationBlocksSettings()
        {
            SetField(battle,"isRetireConfirmationOpen",true); Call(panel,"Open");
            Assert.That(Get(panel,"IsOpen"),Is.False);
        }
        [TestCase(2341)] [TestCase(1920)]
        public void TextAndButtonsFitAndCapture(int height)
        {
            canvas.sizeDelta=new Vector2(1080,height); OwnAll(); Call(panel,"Open");
            for(int i=0;i<3;i++) Call(panel,"Toggle",i);
            Canvas.ForceUpdateCanvases();
            Assert.That(Resources.Load<Sprite>("UI/BattlePermanentEffects/Panel"),Is.Not.Null);
            foreach(var text in panel.GetComponentsInChildren<Text>())
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.name+": "+text.text);
            var buttons=(Button[])Field(panel,"toggles");
            Assert.That(buttons.All(b=>((RectTransform)b.transform).rect.height>=128),Is.True);
            var path=Environment.GetEnvironmentVariable("BATTLE_EFFECT_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(path);
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[]{canvas,1080,height,Path.Combine(path,"settings-"+height+".png")});
                Call(panel,"Close");
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[]{canvas,1080,height,Path.Combine(path,"summary-"+height+".png")});
            }
        }
    }
}
