using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class DailyRewardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        public void Bind(bool canClaim, int rewardGold)
        {
            if (statusText != null)
            {
                statusText.text = canClaim
                    ? $"Daily Reward Ready: collect {rewardGold} Gold now"
                    : "Daily Reward: already claimed for today";
            }
        }
    }
}
