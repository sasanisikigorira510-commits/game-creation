using TMPro;
using UnityEngine;
using WitchTower.Data;

namespace WitchTower.UI
{
    public sealed class ResourceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text rebirthPointText;

        public void Bind(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (goldText != null)
            {
                goldText.text = $"所持ゴールド {profile.Gold}";
            }

            if (rebirthPointText != null)
            {
                rebirthPointText.text = $"魂片 {profile.RebirthPoints}";
            }
        }
    }
}
