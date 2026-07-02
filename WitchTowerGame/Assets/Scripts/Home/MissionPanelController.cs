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

        private void OnEnable()
        {
            Refresh();
        }

        public void ClaimDailyReward()
        {
            var profile = GameManager.Instance.PlayerProfile;
            var claimedStones = DailyRewardService.ClaimAll(profile, DateTime.Now);
            if (claimedStones > 0)
            {
                AudioManager.Instance?.PlaySe(AudioCue.DailyReward);
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

            DateTime now = DateTime.Now;
            bool canClaimDaily = profile != null && DailyRewardService.HasClaimableQuest(profile, now);
            bool isDailyClaimed = profile != null && DailyRewardService.AreAllClaimed(profile, now);
            int dailyTarget = DailyRewardService.GetMaximumRequiredBattleWins();
            DailyQuestDefinition finalQuest = DailyRewardService.GetDefinitions()[DailyRewardService.GetDefinitions().Count - 1];
            int dailyProgress = DailyRewardService.GetBattleWinProgress(profile, now, finalQuest.Id);
            if (dailyRewardView != null)
            {
                dailyRewardView.Bind(
                    canClaimDaily,
                    isDailyClaimed,
                    dailyProgress,
                    dailyTarget);
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
                AudioManager.Instance?.PlaySe(AudioCue.MissionComplete);
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
