using UnityEngine;
using WitchTower.Managers;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class HomePanelController : MonoBehaviour
    {
        [SerializeField] private PlayerStatusView playerStatusView;
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private IdleRewardView idleRewardView;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var profile = GameManager.Instance.PlayerProfile;
            playerStatusView.Bind(profile);
            resourceView.Bind(profile);
            if (idleRewardView != null)
            {
                idleRewardView.Bind(profile != null ? profile.PendingIdleRewardGold : 0);
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
        }
    }
}
