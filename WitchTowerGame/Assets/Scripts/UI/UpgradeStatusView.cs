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
                labelText.text = $"{label}強化";
            }

            if (levelText != null)
            {
                levelText.text = $"段階 {level}";
            }

            if (costText != null)
            {
                costText.text = $"次の強化: {cost}ゴールド";
            }

            if (bonusText != null)
            {
                string bonusLabel = label switch
                {
                    "攻撃" => "攻撃",
                    "防御" => "防御",
                    "HP" => "HP",
                    _ => "補正"
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
