using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class MissionItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardText;

        public void Bind(string title, int progress, int target, int rewardGold, bool isClaimed)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (progressText != null)
            {
                progressText.text = isClaimed ? "Claimed" : $"Progress {progress}/{target}";
            }

            if (rewardText != null)
            {
                rewardText.text = isClaimed ? $"Reward {rewardGold} Gold collected" : $"Claim for {rewardGold} Gold";
            }
        }
    }
}
