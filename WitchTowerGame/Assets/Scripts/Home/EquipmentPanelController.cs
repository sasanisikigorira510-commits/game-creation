using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
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
            var selectedMonster = ResolveRepresentativeMonster(profile);
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
                var previewStats = CreateRepresentativeMonsterPreview(profile, selectedMonster);
                equipmentStatusView.Bind(
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Weapon),
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Armor),
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Accessory),
                    BuildSummary(selectedMonster, previewStats),
                    BuildEquipmentPolicyText(selectedMonster),
                    BuildLoadoutImpact(profile, selectedMonster));
            }

            BindEquipmentOption(bronzeBladeButton, bronzeBladeStatusText, profile, "equip_bronze_blade", "未所持");
            BindEquipmentOption(ironSwordButton, ironSwordStatusText, profile, "equip_iron_sword", "未所持");
            BindEquipmentOption(guardClothButton, guardClothStatusText, profile, "equip_guard_cloth", "未所持");
            BindEquipmentOption(boneMailButton, boneMailStatusText, profile, "equip_bone_mail", "未所持");
            BindEquipmentOption(ashenRingButton, ashenRingStatusText, profile, "equip_ashen_ring", "未所持");
            BindEquipmentOption(quickCharmButton, quickCharmStatusText, profile, "equip_quick_charm", "未所持");

            if (ctaText != null)
            {
                ctaText.text = BuildEquipmentHeadline(selectedMonster);
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
                statusText.text = isEquipped ? "装備中" : (isOwned ? "所持" : lockedLabel);
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

        private static string GetEquipmentName(Data.PlayerProfile profile, Save.OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            if (profile == null || monster == null)
            {
                return "-";
            }

            var equipped = profile.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            string equipmentId = equipped != null ? equipped.EquipmentId : string.Empty;
            if (string.IsNullOrEmpty(equipmentId))
            {
                return "-";
            }

            var equipmentData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipmentId)
                : null;
            return equipmentData != null ? equipmentData.equipmentName : equipmentId;
        }

        private static string BuildSummary(Save.OwnedMonsterData monster, BattleUnitStats stats)
        {
            if (stats == null)
            {
                return "戦力プレビューを取得できません";
            }

            string monsterLabel = ResolveMonsterName(monster);
            return $"{monsterLabel}  HP {stats.MaxHp}  ATK {stats.Attack}  DEF {stats.Defense}  CRIT {(stats.CritRate * 100f):0.#}%  SPD {stats.AttackSpeed:0.##}\n評価: {BuildGrade(stats)}";
        }

        private static string BuildEquipmentPolicyText(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "装備方針: 装備対象モンスターを選択してください";
            }

            return "装備方針: 武器 / 防具 / 装飾をモンスター個別に装備します";
        }

        private static string BuildLoadoutImpact(Data.PlayerProfile profile, Save.OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return "強化情報: 選択中モンスターの装備情報を表示します";
            }

            return $"強化情報: {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Weapon, "武器")} / {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Armor, "防具")} / {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Accessory, "装飾")}\n遺物: 安定={profile.GetEnhancementRelicAmount("relic_safe_ember")} 挑戦={profile.GetEnhancementRelicAmount("relic_risky_ember")} 破滅={profile.GetEnhancementRelicAmount("relic_volatile_ember")}";
        }

        private static string BuildEquipmentImpact(Data.PlayerProfile profile, Save.OwnedMonsterData monster, EquipmentSlotType slotType, string slotLabel)
        {
            if (profile == null || monster == null || MasterDataManager.Instance == null)
            {
                return $"{slotLabel} なし";
            }

            var equipped = profile.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            string equipmentId = equipped != null ? equipped.EquipmentId : string.Empty;
            if (string.IsNullOrEmpty(equipmentId))
            {
                return $"{slotLabel} なし";
            }

            var equipmentData = MasterDataManager.Instance.GetEquipmentData(equipmentId);
            if (equipmentData == null)
            {
                return $"{slotLabel} 不明";
            }

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            var resolvedBonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, equipped);
            if (resolvedBonus.Attack != 0)
            {
                parts.Add($"攻+{resolvedBonus.Attack}");
            }

            if (resolvedBonus.Defense != 0)
            {
                parts.Add($"防+{resolvedBonus.Defense}");
            }

            if (resolvedBonus.Hp != 0)
            {
                parts.Add($"HP+{resolvedBonus.Hp}");
            }

            if (resolvedBonus.CritRate != 0f)
            {
                parts.Add($"会心+{resolvedBonus.CritRate * 100f:0.#}%");
            }

            if (resolvedBonus.AttackSpeed != 0f)
            {
                parts.Add($"速+{resolvedBonus.AttackSpeed:0.##}");
            }

            if (parts.Count == 0)
            {
                parts.Add("補正なし");
            }

            return $"{slotLabel} {equipmentData.equipmentName} ({string.Join(", ", parts)})";
        }

        private static string BuildGrade(BattleUnitStats stats)
        {
            float score = stats.MaxHp * 0.12f + stats.Attack * 1.5f + stats.Defense * 1.2f + stats.CritRate * 60f + stats.AttackSpeed * 8f;
            if (score >= 60f)
            {
                return "最前線";
            }

            if (score >= 42f)
            {
                return "安定";
            }

            if (score >= 32f)
            {
                return "発展途上";
            }

            return "脆い";
        }

        private static Save.OwnedMonsterData ResolveRepresentativeMonster(Data.PlayerProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            foreach (string instanceId in profile.PartyMonsterInstanceIds)
            {
                var partyMonster = profile.GetOwnedMonster(instanceId);
                if (partyMonster != null)
                {
                    return partyMonster;
                }
            }

            for (int i = 0; i < profile.OwnedMonsters.Count; i += 1)
            {
                if (profile.OwnedMonsters[i] != null)
                {
                    return profile.OwnedMonsters[i];
                }
            }

            return null;
        }

        private static BattleUnitStats CreateRepresentativeMonsterPreview(Data.PlayerProfile profile, Save.OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return null;
            }

            var monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monster.MonsterId)
                : null;
            return MonsterBattleStatsFactory.Create(profile, monster, monsterData);
        }

        private static string BuildEquipmentHeadline(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "装備管理: 所持モンスターを入手すると個別装備を確認できます。";
            }

            return $"{ResolveMonsterName(monster)} の個別装備を確認中。ロックと強化遺物で装備を管理できます。";
        }

        private static string ResolveMonsterName(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "モンスター";
            }

            var monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monster.MonsterId)
                : null;
            return monsterData != null ? monsterData.monsterName : monster.MonsterId;
        }
    }
}
