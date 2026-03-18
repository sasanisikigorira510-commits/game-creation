using TMPro;
using UnityEngine;
using WitchTower.Data;

namespace WitchTower.UI
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text expText;

        public void Bind(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv. {profile.Level}";
            }

            if (floorText != null)
            {
                floorText.text = $"Highest Floor {profile.HighestFloor}";
            }

            if (expText != null)
            {
                expText.text = $"EXP {profile.Exp}/{profile.GetRequiredExpForNextLevel()}";
            }
        }
    }
}
