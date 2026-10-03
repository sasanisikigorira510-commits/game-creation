using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class DungeonClearHomeReturnTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Result(bool win, int floor, int next, bool firstClear = true) => Activator.CreateInstance(T("Battle.BattleResultViewData"),
            new object[] { win, 100, 50, 50, 3, 1, 1, floor, next, "", "", Array.CreateInstance(T("Battle.BattleResultRewardVisual"),0), firstClear });
        private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden).Invoke(o, args);
        private static void Set(object o, string name, object v) => o.GetType().GetField(name, Hidden).SetValue(o,v);
        private static object Field(object o, string name) => o.GetType().GetField(name, Hidden).GetValue(o);

        [TestCase(10,11,true)] [TestCase(20,21,true)] [TestCase(30,31,true)]
        [TestCase(40,41,true)] [TestCase(50,51,true)] [TestCase(60,60,true)]
        [TestCase(10,10,true)] [TestCase(20,50,true)]
        [TestCase(9,10,false)] [TestCase(11,12,false)] [TestCase(19,20,false)]
        [TestCase(59,60,false)] [TestCase(61,62,false)]
        public void EachDungeonFinalFloorRequiresHomeOnlyOnFirstClear(int floor, int next, bool expected)
        {
            var method = T("Battle.BattleSceneController").GetMethod("RequiresDungeonClearHomeReturn");
            Assert.That(method.Invoke(null, new[] { Result(true,floor,next) }), Is.EqualTo(expected));
            Assert.That(method.Invoke(null, new[] { Result(true,floor,next,false) }), Is.False, "Replay must allow retry.");
            Assert.That(method.Invoke(null, new[] { Result(false,floor,next) }), Is.False, "Defeat must still allow retry.");
        }

        [Test]
        public void FinalFloorResultOnlyOffersHomeAndCancelsQueuedRepeats()
        {
            var saves = T("Managers.SaveManager");
            var game = T("Managers.GameManager");
            object oldSaveManager = saves.GetProperty("Instance").GetValue(null);
            object oldGame = game.GetProperty("Instance").GetValue(null);
            var guardianSession = T("Data.GuardianTrialSession");
            var sessionFields = guardianSession.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsLiteral && !field.IsInitOnly).ToArray();
            object[] oldSessionValues = sessionFields.Select(field => field.GetValue(null)).ToArray();
            GameObject fixtureRoot = null;
            try
            {
                // This tests result UI/routing without any storage I/O or live
                // profile mutation. Preserve all ambient managers and the scene.
                saves.GetProperty("Instance").SetValue(null, null);
                game.GetProperty("Instance").SetValue(null, null);
                guardianSession.GetMethod("Reset").Invoke(null, null);
                fixtureRoot = new GameObject("DungeonClearFixture");
                var canvas = new GameObject("DungeonClearCanvas", typeof(RectTransform), typeof(Canvas));
                canvas.transform.SetParent(fixtureRoot.transform, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080,2341);
                var owner = new GameObject("DungeonClearController"); owner.SetActive(false);
                owner.transform.SetParent(canvas.transform, false);
                var c = owner.AddComponent(T("Battle.BattleSceneController"));
                Set(c,"minimalCanvasRoot",canvas);
                object result = Result(true,10,11);
                Set(c,"lastResultViewData",result); Set(c,"hasLastResultViewData",true);
                Set(c,"autoRepeatSameFloorActive",true); Set(c,"autoRepeatRestartQueued",true);
                Call(c,"ScheduleAutoRepeatRestartIfNeeded");
                Assert.That(Field(c,"autoRepeatSameFloorActive"),Is.False);
                Assert.That(Field(c,"autoRepeatRestartQueued"),Is.False);
                Call(c,"ShowMinimalResultOverlay",result);
                Assert.That(((Button)Field(c,"minimalResultNextFloorButton")).gameObject.activeSelf,Is.False);
                Assert.That(((Button)Field(c,"minimalResultRetryFloorButton")).gameObject.activeSelf,Is.False);
                Assert.That(((Button)Field(c,"minimalResultHomeButton")).gameObject.activeSelf,Is.True);
                Assert.That(((Text)Field(c,"minimalResultSummaryText")).text,Does.Contain("探索完了"));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080,2341);
                canvas.transform.position = Vector3.zero;
                Canvas.ForceUpdateCanvases();
                string capture = Environment.GetEnvironmentVariable("WITCHTOWER_DUNGEON_CLEAR_CAPTURE");
                if (!string.IsNullOrEmpty(capture))
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null,new object[]{(RectTransform)canvas.transform,1179,2556,capture});
                // Invalid scene is an intentional tripwire: all public continuation
                // routes must try returning home instead of starting another battle.
                Set(c,"homeSceneName","MissingHomeSceneForReturnGuardTest");
                foreach (string action in new[]{"GoToNextFloor","RetryClearedFloor","StartAutoRepeatSameFloor"})
                {
                    var exception = Assert.Throws<TargetInvocationException>(() => c.GetType().GetMethod(action).Invoke(c,null));
                    Assert.That(exception.InnerException,Is.TypeOf<ArgumentException>(),action);
                    Assert.That(exception.InnerException.Message,Does.Contain("MissingHomeSceneForReturnGuardTest"));
                }
                Call(c,"ShowMinimalResultOverlay",Result(true,9,10));
                Assert.That(((Button)Field(c,"minimalResultNextFloorButton")).gameObject.activeSelf,Is.True);
                Assert.That(((Button)Field(c,"minimalResultRetryFloorButton")).gameObject.activeSelf,Is.True);
            }
            finally
            {
                if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
                saves.GetProperty("Instance").SetValue(null, oldSaveManager);
                game.GetProperty("Instance").SetValue(null, oldGame);
                for (int i = 0; i < sessionFields.Length; i++) sessionFields[i].SetValue(null, oldSessionValues[i]);
            }
        }
    }
}
