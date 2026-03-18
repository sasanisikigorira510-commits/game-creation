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
                statusText.text = canClaim ? $"Daily Reward: {rewardGold} Gold" : "Daily Reward: Claimed";
            }
        }
    }
}
