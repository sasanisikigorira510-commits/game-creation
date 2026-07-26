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
                ? $"放置報酬: {rewardGold}ゴールドを受け取れます"
                : "放置報酬: 現在受け取れるゴールドはありません";
        }
    }
}
