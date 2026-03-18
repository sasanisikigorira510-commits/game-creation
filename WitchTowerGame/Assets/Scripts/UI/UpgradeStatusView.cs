using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class UpgradeStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text bonusText;

        public void Bind(string label, int level, int cost, int totalBonus)
        {
            if (labelText != null)
            {
                labelText.text = label;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv. {level}";
            }

            if (costText != null)
            {
                costText.text = $"Cost {cost}";
            }

            if (bonusText != null)
            {
                bonusText.text = $"+{totalBonus}";
            }
        }
    }
}
