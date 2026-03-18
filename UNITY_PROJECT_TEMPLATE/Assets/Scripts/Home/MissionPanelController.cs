using System;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class MissionPanelController : MonoBehaviour
    {
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private DailyRewardView dailyRewardView;
        [SerializeField] private MissionItemView missionItemView1;
        [SerializeField] private MissionItemView missionItemView2;

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
            var profile = GameManager.Instance.PlayerProfile;
            resourceView.Bind(profile);

            var canClaimDaily = profile != null && profile.CanClaimDailyReward(DateTime.Now.ToString("yyyy-MM-dd"));
            dailyRewardView.Bind(canClaimDaily, DailyRewardGold);

            BindMission(missionItemView1, profile, "mission_clear_1");
            BindMission(missionItemView2, profile, "mission_reach_floor_3");
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
