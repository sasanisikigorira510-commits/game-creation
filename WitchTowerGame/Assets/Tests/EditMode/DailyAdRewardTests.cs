using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class DailyAdRewardTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static object DefaultSave() => T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
        private static object Stage(object save, string target, int free, int gold, int relics, long revision = 1, string date = "2026-09-30")
        {
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "Kind", "ad_reward"); Set(op, "Target", target); Set(op, "Revision", revision);
            Set(op, "Free", free); Set(op, "GoldDelta", gold); Set(op, "ClaimDate", date);
            Set(op, "RelicId", relics > 0 ? "relic_safe_ember" : ""); Set(op, "RelicAmount", relics);
            return T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { save, op });
        }

        [Test]
        public void ThreeRewardsPersistThroughProfileRoundTripAndReplayOnlyOnce()
        {
            object original = DefaultSave();
            int initialGold = (int)Field(original, "Gold");
            object save = Stage(original, "ad_stones", 1500, 0, 0);
            save = Stage(save, "ad_gold", 1500, 3000, 0, 2);
            save = Stage(save, "ad_relics", 1500, 0, 5, 3);
            save = Stage(save, "ad_relics", 1500, 0, 5, 3);
            Assert.That(Field(save, "Gold"), Is.EqualTo(initialGold + 3000));
            Assert.That(Field(save, "FreeGachaStones"), Is.EqualTo(1500));
            Assert.That(((IList)Field(save, "DailyClaimedAdRewardIds")).Count, Is.EqualTo(3));
            var relic = ((IList)Field(save, "OwnedEnhancementRelics")).Cast<object>()
                .Single(x => (string)Field(x, "RelicId") == "relic_safe_ember");
            Assert.That(Field(relic, "Amount"), Is.EqualTo(5));
            Assert.That(Field(original, "Gold"), Is.EqualTo(initialGold), "Staging must not mutate live data before saving.");
            object profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            object roundTrip = profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 });
            Assert.That(Field(roundTrip, "DailyAdRewardDate"), Is.EqualTo("2026-09-30"));
            CollectionAssert.AreEquivalent(new[] { "ad_stones", "ad_gold", "ad_relics" }, (IList)Field(roundTrip, "DailyClaimedAdRewardIds"));
        }

        [Test]
        public void NextDayStartsFreshWithoutChangingQuestClaims()
        {
            object save = DefaultSave();
            Set(save, "DailyQuestProgressDate", "2026-09-30");
            ((IList)Field(save, "DailyClaimedQuestIds")).Add("daily_battle_win_1");
            save = Stage(save, "ad_stones", 1500, 0, 0);
            save = Stage(save, "ad_gold", 1500, 3000, 0, 2, "2026-10-01");
            CollectionAssert.AreEqual(new[] { "ad_gold" }, (IList)Field(save, "DailyClaimedAdRewardIds"));
            CollectionAssert.AreEqual(new[] { "daily_battle_win_1" }, (IList)Field(save, "DailyClaimedQuestIds"));
            Assert.That(Field(save, "DailyQuestProgressDate"), Is.EqualTo("2026-09-30"));
        }

        [TestCase(14, 59, "2026-09-30")]
        [TestCase(15, 0, "2026-10-01")]
        public void DailyResetUsesJapanTime(int hour, int minute, string expected)
        {
            var now = new DateTime(2026, 9, 30, hour, minute, 0, DateTimeKind.Utc);
            Assert.That(T("Home.DailyAdRewardCatalog").GetMethod("DateKey").Invoke(null, new object[] { now }), Is.EqualTo(expected));
        }

        [Test]
        public void OlderSaveWithoutAdFieldsLoadsWithNoClaimedRewards()
        {
            object save = DefaultSave();
            Set(save, "DailyClaimedAdRewardIds", null);
            object profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
            Assert.That(((IList)profile.GetType().GetProperty("DailyClaimedAdRewardIds").GetValue(profile)).Count, Is.Zero);
            object oldHead = JsonUtility.FromJson("{}", T("Save.OnlineHead"));
            Assert.That(Field(oldHead, "AdRewardsEnabled"), Is.False, "An old server must not enable unrewardable ad views.");
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false)]
        public void OnlyCompletedAdsQueueOneDelivery(bool earned, bool storageSucceeded)
        {
            var root = new GameObject("IsolatedRewardedService");
            root.SetActive(false);
            try
            {
                var type = T("Monetization.AdMobRewardedGiftService");
                var service = root.AddComponent(type);
                var session = Activator.CreateInstance(type.GetNestedType("GiftSession", BindingFlags.NonPublic));
                Set(session, "PlayerId", "isolated-player"); Set(session, "Target", "ad_stones");
                type.GetField("session", Hidden).SetValue(service, session);
                int queued = 0, completed = 0;
                bool delivered = false;
                type.GetField("EditorQueueRewardOverride", Hidden).SetValue(service, new Func<string, string, bool>((player, target) =>
                { Assert.That(target, Is.EqualTo("ad_stones")); queued++; return storageSucceeded; }));
                type.GetField("EditorCompleteRewardOverride", Hidden).SetValue(service, new Action<bool>(value => { completed++; delivered = value; }));
                if (earned)
                {
                    type.GetMethod("Earned", Hidden).Invoke(service, new[] { session });
                    type.GetMethod("Earned", Hidden).Invoke(service, new[] { session });
                }
                type.GetMethod("Closed", Hidden).Invoke(service, new[] { session });
                type.GetMethod("Closed", Hidden).Invoke(service, new[] { session });
                type.GetMethod("Earned", Hidden).Invoke(service, new[] { session });
                Assert.That(queued, Is.EqualTo(earned ? 1 : 0));
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(delivered, Is.EqualTo(earned && storageSucceeded));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void GiftPanelShowsAllAmountsWithoutTextOverflow()
        {
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvas = new GameObject("GiftTestCanvas", typeof(RectTransform), typeof(Canvas));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(1080, 1920);
                var owner = new GameObject("GiftTestHome"); owner.SetActive(false);
                var controller = owner.AddComponent(T("Home.HomeSceneController"));
                controller.GetType().GetMethod("EnsureDailyQuestList", Hidden).Invoke(controller, new object[] { canvas.transform });
                var quest = (GameObject)controller.GetType().GetField("dailyQuestListRoot", Hidden).GetValue(controller);
                quest.SetActive(true);
                controller.GetType().GetMethod("EnsureDailyGiftPanel", Hidden).Invoke(controller, null);
                var gift = (GameObject)controller.GetType().GetField("dailyGiftRoot", Hidden).GetValue(controller);
                gift.SetActive(true);
                Canvas.ForceUpdateCanvases();
                var texts = gift.GetComponentsInChildren<Text>();
                foreach (string expected in new[] { "無償魔晶石 600個", "通常遺物 5個", "ゴールド 3,000" })
                    Assert.That(texts.Any(t => t.text == expected), Is.True, expected);
                foreach (var text in texts)
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                Assert.That(gift.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Watch_")), Is.EqualTo(3));
                var status = texts.Single(t => t.name == "Status");
                status.text = "各1日1回・日本時間0時更新";
                string capture = Environment.GetEnvironmentVariable("WITCHTOWER_GIFTS_CAPTURE");
                if (!string.IsNullOrEmpty(capture)) typeof(EquipmentEnhanceGuideLayoutTests)
                    .GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { rect, 1080, 1920, capture });
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }
    }
}
