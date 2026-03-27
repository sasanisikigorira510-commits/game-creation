using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class EquipmentStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text accessoryText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text matchupText;
        [SerializeField] private TMP_Text loadoutImpactText;

        public void Bind(string weaponName, string armorName, string accessoryName, string summary = null, string matchup = null, string loadoutImpact = null)
        {
            if (weaponText != null)
            {
                weaponText.text = $"Weapon: {weaponName}";
            }

            if (armorText != null)
            {
                armorText.text = $"Armor: {armorName}";
            }

            if (accessoryText != null)
            {
                accessoryText.text = $"Accessory: {accessoryName}";
            }

            if (summaryText != null)
            {
                summaryText.text = FormatSummary(summary);
            }

            if (matchupText != null)
            {
                matchupText.text = matchup ?? "Next Floor Read: unavailable";
            }

            if (loadoutImpactText != null)
            {
                loadoutImpactText.text = loadoutImpact ?? "Loadout Impact: unavailable";
            }
        }

        private static string FormatSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return "Battle Build: no preview available";
            }

            if (summary.Contains("Build Grade: Tower-ready"))
            {
                return summary.Replace("Build Grade: Tower-ready", "Build Grade: <color=#63E6A8>Tower-ready</color>");
            }

            if (summary.Contains("Build Grade: Stable"))
            {
                return summary.Replace("Build Grade: Stable", "Build Grade: <color=#8FD9FF>Stable</color>");
            }

            if (summary.Contains("Build Grade: Scrappy"))
            {
                return summary.Replace("Build Grade: Scrappy", "Build Grade: <color=#F4C66B>Scrappy</color>");
            }

            if (summary.Contains("Build Grade: Fragile"))
            {
                return summary.Replace("Build Grade: Fragile", "Build Grade: <color=#F07D7D>Fragile</color>");
            }

            return summary;
        }
    }
}
