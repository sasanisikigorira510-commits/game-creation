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
                progressText.text = isClaimed ? "受取済み" : $"進行 {progress}/{target}";
            }

            if (rewardText != null)
            {
                rewardText.text = isClaimed ? $"{rewardGold}ゴールド受取済み" : $"{rewardGold}ゴールドを受け取る";
            }
        }
    }
}
