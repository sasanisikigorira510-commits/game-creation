using WitchTower.Data;

namespace WitchTower.Home
{
    public static class EquipmentUnlockService
    {
        public static bool GrantFloorUnlocks(PlayerProfile profile, int clearedFloor)
        {
            if (profile == null)
            {
                return false;
            }

            bool unlockedAny = false;
            unlockedAny |= Grant(profile, clearedFloor >= 2, "equip_iron_sword");
            unlockedAny |= Grant(profile, clearedFloor >= 4, "equip_bone_mail");
            unlockedAny |= Grant(profile, clearedFloor >= 6, "equip_quick_charm");
            return unlockedAny;
        }

        private static bool Grant(PlayerProfile profile, bool shouldUnlock, string equipmentId)
        {
            if (!shouldUnlock)
            {
                return false;
            }

            return profile.AddOwnedEquipment(equipmentId);
        }
    }
}
