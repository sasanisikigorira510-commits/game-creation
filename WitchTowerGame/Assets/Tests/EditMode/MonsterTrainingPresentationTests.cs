using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class MonsterTrainingPresentationTests
    {
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type T(string n)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Assembly-CSharp").GetType("WitchTower."+n,true);
        private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Any).SetValue(o,v);
        [TestCase("monster_dragon_whelp",false)] [TestCase("monster_ordion",true)]
        public void TrainingRowsCapsAndDedicatedItemIconsFitPopup(string species,bool class5)
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            var gm=T("Managers.GameManager");var md=T("Managers.MasterDataManager");
            object oldGame=gm.GetProperty("Instance").GetValue(null),oldMaster=md.GetProperty("Instance").GetValue(null);
            var random=UnityEngine.Random.state;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var owner=new GameObject("TrainingPresentationManagers"); owner.SetActive(false);
                var master=owner.AddComponent(md);var game=owner.AddComponent(gm);
                gm.GetProperty("Instance").SetValue(null,game);md.GetProperty("Instance").SetValue(null,master);
                md.GetMethod("Initialize").Invoke(master,null);
                var save=T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null,null);
                Set(save,"TrainingDrops",125);Set(save,"TrialStarCores",20);
                var profile=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{save});
                gm.GetProperty("PlayerProfile").SetValue(game,profile);
                var owned=profile.GetType().GetMethod("AddOwnedMonster").Invoke(profile,new object[]{species,1,0,false});
                Set(owned,"TrainingHp",10);
                string id=(string)owned.GetType().GetField("InstanceId").GetValue(owned);
                var data=md.GetMethod("GetMonsterData").Invoke(master,new object[]{species});
                var canvasObject=new GameObject("TrainingCanvas",typeof(RectTransform),typeof(Canvas));
                canvasObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                var canvas=(RectTransform)canvasObject.transform;canvas.sizeDelta=new Vector2(1080,1920);
                T("UI.MonsterStatusDetailPopup").GetMethod("ShowTraining",Any).Invoke(null,new object[]{canvas,id,data,(Action)(()=>{}),"",false});
                Canvas.ForceUpdateCanvases();
                var popup=canvas.Find("MonsterTrainingPopup");Assert.That(popup,Is.Not.Null);
                var buttons=popup.GetComponentsInChildren<Button>();
                Assert.That(buttons.Single(x=>x.name=="Train_Hp").interactable,Is.False);
                Assert.That(buttons.Count(x=>x.name.StartsWith("Train_")),Is.EqualTo(6));
                Assert.That(popup.Find("TrainingPanel/TrainingRule").GetComponent<Text>().text,Does.Contain("Lv10"));
                foreach(var text in popup.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(((RectTransform)text.transform).rect.height+1),text.name+" overflows");
                    Assert.That(text.transform.position.x,Is.InRange(-540,540),text.name);
                }
                Assert.That(popup.GetComponentsInChildren<Image>().Any(x=>x.sprite!=null&&x.sprite.name=="TrainingDrop"),Is.True);
                Assert.That(popup.GetComponentsInChildren<Image>().Any(x=>x.sprite!=null&&x.sprite.name=="TrialStarCore"),Is.EqualTo(class5));
                var dir=Environment.GetEnvironmentVariable("WITCHTOWER_DAILY_CAPTURE_DIR")??"/tmp/WitchTowerDailyChallengePreviews";
                Directory.CreateDirectory(dir);
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",Any).Invoke(null,new object[]{canvas,1080,1920,Path.Combine(dir,class5?"training-class5.png":"training-class1.png")});
            }
            finally
            {
                gm.GetProperty("Instance").SetValue(null,oldGame);md.GetProperty("Instance").SetValue(null,oldMaster);UnityEngine.Random.state=random;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(scenes.Length>0&&scenes.All(x=>!string.IsNullOrEmpty(x.path)))EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
    }
}
