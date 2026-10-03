using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class BattleResultReadabilityTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Battle." + name, true);

        [TestCase(1179, 2556, false, 2)]
        [TestCase(1179, 2556, true, 4)]
        [TestCase(750, 1334, false, 4)]
        [TestCase(750, 1334, true, 0)]
        public void ResultUsesOutcomeBackgroundAndReadableRewards(int width, int height, bool win, int count)
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvasObject = new GameObject("ResultTestCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvas = (RectTransform)canvasObject.transform;
                canvas.sizeDelta = new Vector2(1080, height * 1080f / width);
                var owner = new GameObject("ResultTestController");
                owner.SetActive(false);
                var controller = owner.AddComponent(T("BattleSceneController"));
                T("BattleSceneController").GetField("minimalCanvasRoot", Hidden).SetValue(controller, canvasObject);
                void Call(string method, params object[] args) => T("BattleSceneController").GetMethod(method, Hidden).Invoke(controller, args);
                Call("EnsureMinimalResultOverlay");
                Call("ApplyReadableMinimalResultLayout", win);
                var card = (RectTransform)canvas.Find("BattleMinimalResultOverlay/ResultCard");
                Assert.That(card.sizeDelta.y, Is.LessThan(canvas.sizeDelta.y));
                Assert.That(card.GetComponent<Image>().sprite.name, Is.EqualTo(win ? "BattleResultPanelImage2" : "BattleResultDefeatPanelImage2"));
                card.Find("Title").GetComponent<Text>().text = win ? "勝利" : "敗北";
                card.Find("Title").GetComponent<Text>().color = win ? new Color(1f, .88f, .42f) : new Color(1f, .55f, .55f);
                card.Find("Summary").GetComponent<Text>().text = win ? "地下洞窟\n第1階層を突破\n次の階層: 第2階層" : "戦闘に敗北しました\n編成や装備を見直しましょう";
                Array visuals = Array.CreateInstance(T("BattleResultRewardVisual"), count);
                for (int i = 0; i < count; i++)
                    visuals.SetValue(Activator.CreateInstance(T("BattleResultRewardVisual"), new object[] {
                        i % 2 == 0 ? "見習い剣士" : "見習いの護符", i % 2 == 0 ? "仲間になりました" : "装備クラス1 / アンコモン / 途中獲得",
                        i % 2 == 0 ? "FamilyMonsters/Swordsman/apprentice_swordsman" : "EquipmentIcons/eq_violet_pendant_icon",
                        i % 2 == 0 ? "UI/BattleResult/BattleResultRecruitFrameImage2" : "UI/BattleResult/BattleResultDropFrameImage2", i % 2 == 0 }), i);
                object data = Activator.CreateInstance(T("BattleResultViewData"), new object[] { win, 62, 40, 40, 3, 2, 3, 1, 2, "", "", visuals });
                card.Find("Rewards").GetComponent<Text>().text = (string)T("BattleSceneController").GetMethod("BuildMinimalResultRewardText", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { data });
                string rewardText = card.Find("Rewards").GetComponent<Text>().text;
                Assert.That(rewardText, Does.Contain("経験値 +40"));
                Assert.That(rewardText, Does.Not.Contain("パーティ"));
                Assert.That(rewardText, Does.Not.Contain("プレイヤー"));
                Assert.That(rewardText, Does.Not.Contain("3体"));
                Call("ShowMinimalResultRewardVisuals", data);
                foreach (Transform slot in card.Find("RewardVisuals"))
                    Assert.That(slot.Find("Icon").GetComponent<Image>().sprite != null, Is.True, "Reward icon must survive repeated result scenes");
                card.Find("RetryFloorButton/Label").GetComponent<Text>().text = "この階層に再挑戦";
                card.Find("HomeButton/Label").GetComponent<Text>().text = "ホームへ戻る";
                card.Find("NextFloorButton/Label").GetComponent<Text>().text = "第2階層へ";
                card.Find("NextFloorButton").gameObject.SetActive(win);
                if (win)
                {
                    card.Find("Forecast").gameObject.SetActive(true);
                    card.Find("Forecast").GetComponent<Text>().text = "契約片を回収しました。ゴールド、経験値、装備、仲間化結果を確認してから拠点へ戻りましょう。";
                }
                Canvas.ForceUpdateCanvases();
                Bounds BoundsOf(string path) => RectTransformUtility.CalculateRelativeRectTransformBounds(card, card.Find(path));
                Assert.That(BoundsOf("RewardVisuals").max.y, Is.LessThan(BoundsOf("Rewards").min.y));
                Assert.That(BoundsOf("Forecast").max.y, Is.LessThan(BoundsOf("RewardVisuals").min.y));
                Assert.That(BoundsOf("RetryFloorButton").max.y, Is.LessThan(BoundsOf("Forecast").min.y));
                foreach (Text text in card.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    Assert.That(text.resizeTextMinSize, Is.GreaterThanOrEqualTo(text.name == "Title" ? 16 : 20), text.name);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
                }
                // Switching an existing panel must restore blue after a loss.
                Call("ApplyReadableMinimalResultLayout", !win);
                Assert.That(card.GetComponent<Image>().sprite.name, Is.EqualTo(!win ? "BattleResultPanelImage2" : "BattleResultDefeatPanelImage2"));
                Call("ApplyReadableMinimalResultLayout", win);
                string capture = Environment.GetEnvironmentVariable("WITCHTOWER_RESULT_CAPTURE");
                if (!string.IsNullOrEmpty(capture) && width == 1179)
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { canvas, width, height, capture + (win ? "-win.png" : "-lose.png") });
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
    }
}
