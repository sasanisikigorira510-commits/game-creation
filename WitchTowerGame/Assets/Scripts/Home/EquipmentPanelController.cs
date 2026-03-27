using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
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
        [SerializeField] private Button bronzeBladeButton;
        [SerializeField] private Button ironSwordButton;
        [SerializeField] private Button guardClothButton;
        [SerializeField] private Button boneMailButton;
        [SerializeField] private Button ashenRingButton;
        [SerializeField] private Button quickCharmButton;
        [SerializeField] private TMP_Text bronzeBladeStatusText;
        [SerializeField] private TMP_Text ironSwordStatusText;
        [SerializeField] private TMP_Text guardClothStatusText;
        [SerializeField] private TMP_Text boneMailStatusText;
        [SerializeField] private TMP_Text ashenRingStatusText;
        [SerializeField] private TMP_Text quickCharmStatusText;
        [SerializeField] private TMP_Text ctaText;

        private static readonly Color UnlockedButtonColor = new Color(0.2f, 0.35f, 0.42f, 1f);
        private static readonly Color EquippedButtonColor = new Color(0.24f, 0.48f, 0.34f, 1f);
        private static readonly Color LockedButtonColor = new Color(0.19f, 0.19f, 0.22f, 0.75f);
        private static readonly Color EquippedStatusColor = new Color(0.45f, 1f, 0.67f, 1f);
        private static readonly Color OwnedStatusColor = new Color(0.72f, 0.92f, 0.72f, 1f);
        private static readonly Color LockedStatusColor = new Color(1f, 0.78f, 0.45f, 1f);

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var gameManager = GameManager.Instance;
            var profile = gameManager != null ? gameManager.PlayerProfile : null;
            if (playerStatusView != null)
            {
                playerStatusView.Bind(profile);
            }

            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (equipmentStatusView != null)
            {
                var previewStats = PlayerBattleStatsFactory.CreatePreview(profile);
                equipmentStatusView.Bind(
                    GetEquipmentName(profile?.EquippedWeaponId),
                    GetEquipmentName(profile?.EquippedArmorId),
                    GetEquipmentName(profile?.EquippedAccessoryId),
                    BuildSummary(previewStats),
                    BuildNextFloorRead(profile, previewStats),
                    BuildLoadoutImpact(profile));
            }

            BindEquipmentOption(bronzeBladeButton, bronzeBladeStatusText, profile, "equip_bronze_blade", "Starter");
            BindEquipmentOption(ironSwordButton, ironSwordStatusText, profile, "equip_iron_sword", "Unlock at Floor 2");
            BindEquipmentOption(guardClothButton, guardClothStatusText, profile, "equip_guard_cloth", "Starter");
            BindEquipmentOption(boneMailButton, boneMailStatusText, profile, "equip_bone_mail", "Unlock at Floor 4");
            BindEquipmentOption(ashenRingButton, ashenRingStatusText, profile, "equip_ashen_ring", "Starter");
            BindEquipmentOption(quickCharmButton, quickCharmStatusText, profile, "equip_quick_charm", "Unlock at Floor 6");

            if (ctaText != null)
            {
                ctaText.text = HomeActionAdvisor.BuildEquipmentHeadline(profile);
            }
        }

        public void EquipBronzeBlade()
        {
            EquipWeapon("equip_bronze_blade");
        }

        public void EquipIronSword()
        {
            EquipWeapon("equip_iron_sword");
        }

        public void EquipGuardCloth()
        {
            EquipArmor("equip_guard_cloth");
        }

        public void EquipBoneMail()
        {
            EquipArmor("equip_bone_mail");
        }

        public void EquipAshenRing()
        {
            EquipAccessory("equip_ashen_ring");
        }

        public void EquipQuickCharm()
        {
            EquipAccessory("equip_quick_charm");
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
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
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
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
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
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        private static bool HasEquipment(Data.PlayerProfile profile, string equipmentId)
        {
            return profile != null && profile.HasEquipment(equipmentId);
        }

        private static void BindEquipmentOption(Button button, TMP_Text statusText, Data.PlayerProfile profile, string equipmentId, string lockedLabel)
        {
            bool isOwned = HasEquipment(profile, equipmentId);
            bool isEquipped = IsEquipped(profile, equipmentId);

            if (button != null)
            {
                button.interactable = isOwned;
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isEquipped ? EquippedButtonColor : (isOwned ? UnlockedButtonColor : LockedButtonColor);
                }

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = isOwned ? Color.white : new Color(0.78f, 0.78f, 0.82f, 1f);
                }
            }

            if (statusText != null)
            {
                statusText.text = isEquipped ? "Equipped" : (isOwned ? "Owned" : lockedLabel);
                statusText.color = isEquipped ? EquippedStatusColor : (isOwned ? OwnedStatusColor : LockedStatusColor);
            }
        }

        private static bool IsEquipped(Data.PlayerProfile profile, string equipmentId)
        {
            return profile != null &&
                (profile.EquippedWeaponId == equipmentId ||
                 profile.EquippedArmorId == equipmentId ||
                 profile.EquippedAccessoryId == equipmentId);
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

        private static string BuildSummary(BattleUnitStats stats)
        {
            if (stats == null)
            {
                return "Battle Build: preview unavailable";
            }

            return $"Battle Build: HP {stats.MaxHp}  ATK {stats.Attack}  DEF {stats.Defense}  CRIT {(stats.CritRate * 100f):0}%  SPD {stats.AttackSpeed:0.##}\nBuild Grade: {BuildGrade(stats)}";
        }

        private static string BuildNextFloorRead(Data.PlayerProfile profile, BattleUnitStats stats)
        {
            if (profile == null || stats == null)
            {
                return "Next Floor Read: unavailable";
            }

            int nextFloor = profile.HighestFloor + 1;
            BattleUnitStats enemyStats = BattleEncounterAdvisor.CreateEnemyPreview(nextFloor);
            string threat = BattleEncounterAdvisor.BuildThreatText(stats, enemyStats).Replace("Threat: ", string.Empty);
            return $"Next Floor Read: {threat} on floor {nextFloor} against {enemyStats.MaxHp} HP / {enemyStats.Attack} ATK.";
        }

        private static string BuildLoadoutImpact(Data.PlayerProfile profile)
        {
            if (profile == null)
            {
                return "Loadout Impact: unavailable";
            }

            string weaponImpact = BuildEquipmentImpact(profile.EquippedWeaponId, "weapon");
            string armorImpact = BuildEquipmentImpact(profile.EquippedArmorId, "armor");
            string accessoryImpact = BuildEquipmentImpact(profile.EquippedAccessoryId, "accessory");
            return $"Loadout Impact: {weaponImpact}; {armorImpact}; {accessoryImpact}.";
        }

        private static string BuildEquipmentImpact(string equipmentId, string slotLabel)
        {
            if (string.IsNullOrEmpty(equipmentId) || MasterDataManager.Instance == null)
            {
                return $"{slotLabel} none";
            }

            var equipmentData = MasterDataManager.Instance.GetEquipmentData(equipmentId);
            if (equipmentData == null)
            {
                return $"{slotLabel} unknown";
            }

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if (equipmentData.baseAttack != 0)
            {
                parts.Add($"+{equipmentData.baseAttack} ATK");
            }

            if (equipmentData.baseDefense != 0)
            {
                parts.Add($"+{equipmentData.baseDefense} DEF");
            }

            if (equipmentData.baseHp != 0)
            {
                parts.Add($"+{equipmentData.baseHp} HP");
            }

            if (equipmentData.bonusCritRate != 0f)
            {
                parts.Add($"+{equipmentData.bonusCritRate * 100f:0}% crit");
            }

            if (equipmentData.bonusAttackSpeed != 0f)
            {
                parts.Add($"+{equipmentData.bonusAttackSpeed:0.##} spd");
            }

            if (parts.Count == 0)
            {
                parts.Add("no stat bonus");
            }

            return $"{slotLabel} {equipmentData.equipmentName} ({string.Join(", ", parts)})";
        }

        private static string BuildGrade(BattleUnitStats stats)
        {
            float score = stats.MaxHp * 0.12f + stats.Attack * 1.5f + stats.Defense * 1.2f + stats.CritRate * 60f + stats.AttackSpeed * 8f;
            if (score >= 60f)
            {
                return "Tower-ready";
            }

            if (score >= 42f)
            {
                return "Stable";
            }

            if (score >= 32f)
            {
                return "Scrappy";
            }

            return "Fragile";
        }
    }
}
