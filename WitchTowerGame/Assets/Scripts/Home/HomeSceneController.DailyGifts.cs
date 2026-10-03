using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Monetization;
using WitchTower.Save;

namespace WitchTower.Home
{
    public sealed partial class HomeSceneController
    {
        private GameObject dailyGiftRoot;
        private Text dailyGiftStatus;
        private readonly List<Button> dailyGiftButtons = new List<Button>();

        private void AddDailyGiftShortcut(Transform panel)
        {
            if (!AdMobRewardedGiftService.PlacementEnabled) return;
            dailyQuestStatusText.rectTransform.anchoredPosition = new Vector2(0, 24);
            dailyQuestStatusText.fontSize = 18;
            var shortcut = CreatePlainButton("DailyGiftShortcut", panel, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 80), new Vector2(610, 54), new Color(.12f, .28f, .32f, 1), OpenDailyGiftPanel);
            CreateUiText("Label", shortcut.transform, "広告で受け取る・毎日のプレゼント", 24, FontStyle.Bold,
                Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(580, 48), Color.white, TextAnchor.MiddleCenter);
        }

        private void OpenDailyGiftPanel()
        {
            EnsureDailyGiftPanel();
            dailyGiftRoot.SetActive(true);
            dailyGiftRoot.transform.SetAsLastSibling();
            if (Application.isPlaying) AdMobRewardedGiftService.Ensure();
            RefreshDailyGiftPanel();
        }

        private void EnsureDailyGiftPanel()
        {
            if (dailyGiftRoot != null) return;
            dailyGiftButtons.Clear();
            dailyGiftRoot = new GameObject("DailyGiftPanel", typeof(RectTransform));
            dailyGiftRoot.transform.SetParent(dailyQuestListRoot.transform, false);
            var root = dailyGiftRoot.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var shade = CreatePlainButton("Shade", root, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero,
                new Vector2(1080, 2400), new Color(0, 0, 0, .7f), () => dailyGiftRoot.SetActive(false));
            var shadeRect = shade.GetComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one; shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            var body = CreatePlainButton("Body", root, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 40),
                new Vector2(860, 820), new Color(.014f, .018f, .024f, .99f), null);
            body.transition = Selectable.Transition.None;
            AddUiOutline(body.gameObject, new Color(.82f, .64f, .28f, .7f), new Vector2(2, -2));
            GiftText("Title", body.transform, "毎日のプレゼント", new Vector2(-30, 345), new Vector2(620, 54), 34);
            GiftText("Rules", body.transform, "各1日1回・日本時間0時更新", new Vector2(0, 285), new Vector2(760, 48), 24);
            var close = CreatePlainButton("Close", body.transform, Vector2.one * .5f, Vector2.one * .5f,
                new Vector2(345, 345), new Vector2(100, 66), new Color(.24f, .10f, .09f, 1), () => dailyGiftRoot.SetActive(false));
            GiftText("Label", close.transform, "閉じる", Vector2.zero, new Vector2(94, 52), 22);
            for (int i = 0; i < DailyAdRewardCatalog.Ids.Count; i++)
            {
                string id = DailyAdRewardCatalog.Ids[i];
                float y = 175 - i * 170;
                GiftText("Reward_" + id, body.transform, DailyAdRewardCatalog.Label(id), new Vector2(-145, y), new Vector2(410, 100), 30);
                var button = CreatePlainButton("Watch_" + id, body.transform, Vector2.one * .5f, Vector2.one * .5f,
                    new Vector2(230, y), new Vector2(280, 110), new Color(.10f, .32f, .30f, 1),
                    () => { AdMobRewardedGiftService.Ensure().Show(id); RefreshDailyGiftPanel(); });
                GiftText("Label", button.transform, "広告を見て\n受け取る", Vector2.zero, new Vector2(260, 96), 28);
                dailyGiftButtons.Add(button);
            }
            dailyGiftStatus = GiftText("Status", body.transform, string.Empty, new Vector2(0, -305), new Vector2(760, 116), 24);
            dailyGiftRoot.SetActive(false);
        }

        private static Text GiftText(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize) =>
            CreateUiText(name, parent, value, fontSize, FontStyle.Bold, Vector2.one * .5f, Vector2.one * .5f,
                position, size, Color.white, TextAnchor.MiddleCenter);

        private void RefreshDailyGiftPanel()
        {
            if (dailyGiftRoot == null || !dailyGiftRoot.activeInHierarchy) return;
            var profile = GetRuntimeProfile();
            var service = AdMobRewardedGiftService.Instance;
            int claimedCount = 0;
            for (int i = 0; i < dailyGiftButtons.Count; i++)
            {
                bool claimed = DailyAdRewardCatalog.IsClaimed(profile, DailyAdRewardCatalog.Ids[i], DateTime.UtcNow);
                if (claimed) claimedCount++;
                var button = dailyGiftButtons[i];
                button.interactable = !claimed && profile != null && service != null && service.CanShow;
                button.GetComponentInChildren<Text>().text = claimed ? "受取済み" : "広告を見て\n受け取る";
            }
            dailyGiftStatus.text = claimedCount == 3 ? "今日のプレゼントはすべて受け取り済みです。" :
                service != null && service.IsBusy ? service.Message :
                !string.IsNullOrEmpty(OnlinePlayerData.ContractUnavailableMessage) ? OnlinePlayerData.ContractUnavailableMessage :
                !OnlinePlayerData.AdRewardsAvailable ? "毎日のプレゼントは準備中です。" :
                service != null ? service.Message : "広告を準備しています。";
        }
    }
}
