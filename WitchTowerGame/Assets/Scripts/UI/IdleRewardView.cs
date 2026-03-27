using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class IdleRewardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        public void Bind(int rewardGold)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = rewardGold > 0
                ? $"Idle Reward Ready: {rewardGold} Gold waiting to collect"
                : "Idle Reward: No stored gold right now";
        }
    }
}
