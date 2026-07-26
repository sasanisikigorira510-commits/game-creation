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
                goldText.text = profile.Gold.ToString();
            }

            if (rebirthPointText != null)
            {
                rebirthPointText.text = profile.RebirthPoints.ToString();
            }
        }
    }
}
