using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;
using TMPro;

namespace WitchTower.Home
{
    public sealed class HomePanelController : MonoBehaviour
    {
        [SerializeField] private PlayerStatusView playerStatusView;
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private IdleRewardView idleRewardView;
        [SerializeField] private TMP_Text ctaText;
        [SerializeField] private TMP_Text rewardSummaryText;
        [SerializeField] private TMP_Text prepAdviceText;
        [SerializeField] private TMP_Text battlePlanText;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var gameManager = GameManager.Instance;
            var profile = gameManager != null ? gameManager.PlayerProfile : null;

            if (playerStatusView != null)
            {
                playerStatusView.Bind(profile);
            }

            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (idleRewardView != null)
            {
                idleRewardView.Bind(profile != null ? profile.PendingIdleRewardGold : 0);
            }

            if (ctaText != null)
            {
                ctaText.text = HomeActionAdvisor.BuildHomeHeadline(profile);
            }

            if (rewardSummaryText != null)
            {
                rewardSummaryText.text = HomeActionAdvisor.BuildHomeRewardSummary(profile, System.DateTime.Now);
            }

            if (prepAdviceText != null)
            {
                prepAdviceText.text = HomeActionAdvisor.BuildPrepAdviceText(profile, 10);
            }

            if (battlePlanText != null)
            {
                battlePlanText.text = HomeActionAdvisor.BuildBattlePlanText(profile, 10, System.DateTime.Now);
            }
        }

        public void ClaimIdleReward()
        {
            var profile = GameManager.Instance.PlayerProfile;
            var reward = IdleRewardService.Claim(profile, System.DateTime.Now);
            if (reward > 0)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }
    }
}
