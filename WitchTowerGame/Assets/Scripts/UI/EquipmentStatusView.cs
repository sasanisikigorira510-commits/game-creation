using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class EquipmentStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text accessoryText;

        public void Bind(string weaponName, string armorName, string accessoryName)
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
        }
    }
}
