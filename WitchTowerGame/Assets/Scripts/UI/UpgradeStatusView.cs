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
        [SerializeField] private TMP_Text impactText;

        public void Bind(string label, int level, int cost, int totalBonus, string impact)
        {
            if (labelText != null)
            {
                labelText.text = $"{label} Boost";
            }

            if (levelText != null)
            {
                levelText.text = $"Tier {level}";
            }

            if (costText != null)
            {
                costText.text = $"Next upgrade costs {cost} Gold";
            }

            if (bonusText != null)
            {
                string bonusLabel = label switch
                {
                    "Attack" => "power",
                    "Defense" => "guard",
                    "HP" => "vitality",
                    _ => "bonus"
                };
                bonusText.text = $"+{totalBonus} {bonusLabel}";
            }

            if (impactText != null)
            {
                impactText.text = impact;
            }
        }
    }
}
