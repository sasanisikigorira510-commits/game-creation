using System;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;
using TMPro;

namespace WitchTower.Home
{
    public sealed class MissionPanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private DailyRewardView dailyRewardView;
        [SerializeField] private MissionItemView missionItemView1;
        [SerializeField] private MissionItemView missionItemView2;
        [SerializeField] private TMP_Text ctaText;
        [SerializeField] private TMP_Text rewardSummaryText;

        private const int DailyRewardGold = 50;

        private void OnEnable()
        {
            Refresh();
        }

        public void ClaimDailyReward()
        {
            var profile = GameManager.Instance.PlayerProfile;
            var claimedGold = DailyRewardService.Claim(profile, DateTime.Now);
            if (claimedGold > 0)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            Refresh();
            UnityEngine.Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        public void ClaimMissionClear1()
        {
            ClaimMission("mission_clear_1");
        }

        public void ClaimMissionReachFloor3()
        {
            ClaimMission("mission_reach_floor_3");
        }

        public void Refresh()
        {
            var gameManager = GameManager.Instance;
            var profile = gameManager != null ? gameManager.PlayerProfile : null;
            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            var canClaimDaily = profile != null && profile.CanClaimDailyReward(DateTime.Now.ToString("yyyy-MM-dd"));
            if (dailyRewardView != null)
            {
                dailyRewardView.Bind(canClaimDaily, DailyRewardGold);
            }

            BindMission(missionItemView1, profile, "mission_clear_1");
            BindMission(missionItemView2, profile, "mission_reach_floor_3");

            if (ctaText != null)
            {
                ctaText.text = HomeActionAdvisor.BuildMissionHeadline(profile, DateTime.Now);
            }

            if (rewardSummaryText != null)
            {
                rewardSummaryText.text = HomeActionAdvisor.BuildMissionRewardSummary(profile, DateTime.Now);
            }
        }

        private void ClaimMission(string missionId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            var claimedGold = MissionService.ClaimMission(profile, missionId);
            if (claimedGold > 0)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            Refresh();
            UnityEngine.Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        private static void BindMission(MissionItemView itemView, Data.PlayerProfile profile, string missionId)
        {
            if (itemView == null || profile == null)
            {
                return;
            }

            var definition = MissionService.GetDefinition(missionId);
            var progress = profile.GetMissionProgress(missionId);
            if (definition == null || progress == null)
            {
                return;
            }

            itemView.Bind(
                definition.Value.Title,
                progress.Progress,
                definition.Value.TargetValue,
                definition.Value.RewardGold,
                progress.IsClaimed);
        }

    }
}
