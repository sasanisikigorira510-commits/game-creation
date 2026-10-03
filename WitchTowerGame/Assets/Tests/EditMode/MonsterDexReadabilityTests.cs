using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class MonsterDexReadabilityTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name,true);

        [TestCase(1179,2556)]
        [TestCase(750,1334)]
        public void AllMonsterDetailsFitAtReadableFontSizes(int width,int height)
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            var mt = T("WitchTower.Managers.MasterDataManager");
            object previous = mt.GetProperty("Instance").GetValue(null);
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var owner=new GameObject("DexTestServices");owner.SetActive(false);
                var master=owner.AddComponent(mt);mt.GetProperty("Instance").SetValue(null,master);
                mt.GetMethod("Initialize").Invoke(master,null);
                Array monsters=(Array)mt.GetMethod("GetAllMonsterData").Invoke(master,null);
                var canvasObject=new GameObject("DexTestCanvas",typeof(RectTransform),typeof(Canvas));
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1080,height*1080f/width);
                var root=new GameObject("DexTest",typeof(RectTransform));root.transform.SetParent(canvas.transform,false);
                var controller=root.AddComponent(T("WitchTower.Home.MonsterDexPanelController"));
                controller.GetType().GetMethod("Build",Hidden).Invoke(controller,null);
                Canvas.ForceUpdateCanvases();
                var panel=root.transform.Find("DexMainPanel");
                var detail=(RectTransform)panel.Find("DexDetailPanel");
                var grid=(RectTransform)panel.Find("DexGridPanel");
                var title=(RectTransform)panel.Find("Title");
                var close=(RectTransform)root.transform.Find("CloseButton");
                var closeBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,close);
                var titleBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,title);
                Assert.That(titleBounds.max.y,Is.LessThanOrEqualTo(closeBounds.min.y-23f));
                Assert.That(detail.anchoredPosition.y-detail.sizeDelta.y-grid.anchoredPosition.y,Is.EqualTo(8f).Within(.1f));
                foreach(object monster in monsters)
                {
                    controller.GetType().GetMethod("BindDetail",Hidden).Invoke(controller,new[]{monster,(object)1,monsters,true});
                    foreach(string name in new[]{"SelectedName","SelectedInfo","SelectedStats","SelectedDescription"})
                    {
                        var label=detail.Find(name).GetComponent<Text>();
                        Assert.That(label.fontSize,Is.GreaterThanOrEqualTo(24));
                        Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1),name+": "+label.text);
                    }
                }
                object dragon=monsters.Cast<object>().First(m=>(string)m.GetType().GetField("monsterId").GetValue(m)=="monster_dragon_whelp");
                controller.GetType().GetMethod("BindDetail",Hidden).Invoke(controller,new[]{dragon,(object)1,monsters,true});
                var rebuild = controller.GetType().GetMethod("RebuildCards", Hidden);
                var list = (System.Collections.IList)Activator.CreateInstance(rebuild.GetParameters()[0].ParameterType);
                foreach (var monster in monsters.Cast<object>().Take(12)) list.Add(monster);
                rebuild.Invoke(controller, new object[] {list});
                Assert.That(root.GetComponentsInChildren<Text>().Any(x => x.name == "OwnedState"), Is.False);
                Assert.That(root.GetComponentsInChildren<Text>().Count(x => x.name == "ClassRank"), Is.EqualTo(list.Count));
                string capture=Environment.GetEnvironmentVariable("WITCHTOWER_DEX_CAPTURE_PATH");
                if(width==1179 && !string.IsNullOrEmpty(capture))
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                        .Invoke(null,new object[]{canvasRect,width,height,capture});
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                mt.GetProperty("Instance").SetValue(null,previous);
                if(scenes.Length>0 && scenes.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
    }
}
