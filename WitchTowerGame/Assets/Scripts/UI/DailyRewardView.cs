using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class DailyRewardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        public void Bind(bool canClaim, bool isClaimed, int progress, int target)
        {
            if (statusText != null)
            {
                if (isClaimed)
                {
                    statusText.text = "デイリークエスト: 本日はすべて受け取り済み";
                    return;
                }

                statusText.text = canClaim
                    ? "デイリークエスト達成分をまとめて受け取れます"
                    : $"デイリークエスト: バトル勝利 {progress}/{target}";
            }
        }
    }
}
