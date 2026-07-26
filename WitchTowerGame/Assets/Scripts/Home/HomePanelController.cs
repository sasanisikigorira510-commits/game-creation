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
            DisableRemovedIdleRewardUi();
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

            if (ctaText != null)
            {
                HideHomeAdviceText(ctaText);
            }

            if (rewardSummaryText != null)
            {
                rewardSummaryText.text = HomeActionAdvisor.BuildHomeRewardSummary(profile, System.DateTime.Now);
            }

            if (prepAdviceText != null)
            {
                HideHomeAdviceText(prepAdviceText);
            }

            if (battlePlanText != null)
            {
                HideHomeAdviceText(battlePlanText);
            }
        }

        public void ClaimIdleReward()
        {
            DisableRemovedIdleRewardUi();
        }

        private void DisableRemovedIdleRewardUi()
        {
            if (idleRewardView != null)
            {
                idleRewardView.gameObject.SetActive(false);
            }

            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant != null && descendant.name == "ClaimIdleRewardButton")
                {
                    descendant.gameObject.SetActive(false);
                }
            }
        }

        private static void HideHomeAdviceText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }
    }
}
