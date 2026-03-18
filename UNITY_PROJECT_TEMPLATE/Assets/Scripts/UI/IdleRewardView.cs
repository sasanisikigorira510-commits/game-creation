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
                ? $"Idle Reward: {rewardGold} Gold"
                : "Idle Reward: None";
        }
    }
}
