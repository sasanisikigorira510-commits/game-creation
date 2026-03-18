using TMPro;
using UnityEngine;
using WitchTower.Data;

namespace WitchTower.UI
{
    public sealed class ResourceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldText;

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
        }
    }
}
