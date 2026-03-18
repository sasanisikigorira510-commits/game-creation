using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Managers;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class EquipmentPanelController : MonoBehaviour
    {
        [SerializeField] private PlayerStatusView playerStatusView;
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private EquipmentStatusView equipmentStatusView;

        public void Refresh()
        {
            var profile = GameManager.Instance.PlayerProfile;
            playerStatusView.Bind(profile);
            resourceView.Bind(profile);
            equipmentStatusView.Bind(
                GetEquipmentName(profile?.EquippedWeaponId),
                GetEquipmentName(profile?.EquippedArmorId),
                GetEquipmentName(profile?.EquippedAccessoryId));
        }

        public void EquipWeapon(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipWeapon(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
        }

        public void EquipArmor(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipArmor(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
        }

        public void EquipAccessory(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipAccessory(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
        }

        private static bool HasEquipment(Data.PlayerProfile profile, string equipmentId)
        {
            foreach (OwnedEquipmentData equipment in profile.OwnedEquipments)
            {
                if (equipment != null && equipment.EquipmentId == equipmentId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetEquipmentName(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                return "-";
            }

            var equipmentData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipmentId)
                : null;
            return equipmentData != null ? equipmentData.equipmentName : equipmentId;
        }
    }
}
