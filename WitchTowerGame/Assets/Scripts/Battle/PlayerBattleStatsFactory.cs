using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Home;

namespace WitchTower.Battle
{
    public static class PlayerBattleStatsFactory
    {
        public static BattleUnitStats CreatePreview(PlayerProfile profile)
        {
            return CreatePreview(profile, 0, 0, 0, null, null, null);
        }

        public static BattleUnitStats CreatePreview(PlayerProfile profile, int attackLevelOffset, int defenseLevelOffset, int hpLevelOffset)
        {
            return CreatePreview(profile, attackLevelOffset, defenseLevelOffset, hpLevelOffset, null, null, null);
        }

        public static BattleUnitStats CreatePreview(PlayerProfile profile, int attackLevelOffset, int defenseLevelOffset, int hpLevelOffset, string weaponOverrideId, string armorOverrideId, string accessoryOverrideId)
        {
            var masterDataManager = MasterDataManager.Instance;
            var playerData = masterDataManager != null ? masterDataManager.GetPlayerBaseData() : null;

            if (playerData == null)
            {
                return CreateFallback(profile, attackLevelOffset, defenseLevelOffset, hpLevelOffset, weaponOverrideId, armorOverrideId, accessoryOverrideId);
            }

            var equipmentBonus = GetEquipmentBonus(profile, weaponOverrideId, armorOverrideId, accessoryOverrideId);
            var maxHp = playerData.initialHp + GetHpBonus(profile, hpLevelOffset) + equipmentBonus.Hp;
            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = playerData.initialAttack + GetAttackBonus(profile, attackLevelOffset) + equipmentBonus.Attack,
                Wisdom = playerData.initialAttack + GetAttackBonus(profile, attackLevelOffset) + equipmentBonus.Wisdom,
                Defense = playerData.initialDefense + GetDefenseBonus(profile, defenseLevelOffset) + equipmentBonus.Defense,
                MagicDefense = playerData.initialDefense + GetDefenseBonus(profile, defenseLevelOffset) + equipmentBonus.MagicDefense,
                AttackSpeed = playerData.initialAttackSpeed + equipmentBonus.AttackSpeed,
                CritRate = playerData.initialCritRate + equipmentBonus.CritRate,
                CritDamage = playerData.initialCritDamage
            };
        }

        public static BattleUnitStats CreatePreviewAfterUpgrade(PlayerProfile profile, UpgradeType upgradeType)
        {
            return upgradeType switch
            {
                UpgradeType.Attack => CreatePreview(profile, 1, 0, 0, null, null, null),
                UpgradeType.Defense => CreatePreview(profile, 0, 1, 0, null, null, null),
                UpgradeType.Hp => CreatePreview(profile, 0, 0, 1, null, null, null),
                _ => CreatePreview(profile)
            };
        }

        public static BattleUnitStats CreatePreviewWithEquipment(PlayerProfile profile, string weaponOverrideId, string armorOverrideId, string accessoryOverrideId)
        {
            return CreatePreview(profile, 0, 0, 0, weaponOverrideId, armorOverrideId, accessoryOverrideId);
        }

        private static BattleUnitStats CreateFallback(PlayerProfile profile, int attackLevelOffset, int defenseLevelOffset, int hpLevelOffset, string weaponOverrideId, string armorOverrideId, string accessoryOverrideId)
        {
            var equipmentBonus = GetEquipmentBonus(profile, weaponOverrideId, armorOverrideId, accessoryOverrideId);
            var maxHp = 100 + GetHpBonus(profile, hpLevelOffset) + equipmentBonus.Hp;
            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = 15 + GetAttackBonus(profile, attackLevelOffset) + equipmentBonus.Attack,
                Wisdom = 15 + GetAttackBonus(profile, attackLevelOffset) + equipmentBonus.Wisdom,
                Defense = 5 + GetDefenseBonus(profile, defenseLevelOffset) + equipmentBonus.Defense,
                MagicDefense = 5 + GetDefenseBonus(profile, defenseLevelOffset) + equipmentBonus.MagicDefense,
                AttackSpeed = 1.0f + equipmentBonus.AttackSpeed,
                CritRate = 0.05f + equipmentBonus.CritRate,
                CritDamage = 1.5f
            };
        }

        private static int GetAttackBonus(PlayerProfile profile, int levelOffset)
        {
            return profile != null ? (profile.AttackUpgradeLevel + levelOffset) * 3 : 0;
        }

        private static int GetDefenseBonus(PlayerProfile profile, int levelOffset)
        {
            return profile != null ? (profile.DefenseUpgradeLevel + levelOffset) * 2 : 0;
        }

        private static int GetHpBonus(PlayerProfile profile, int levelOffset)
        {
            return profile != null ? (profile.HpUpgradeLevel + levelOffset) * 10 : 0;
        }

        private static EquipmentBonus GetEquipmentBonus(PlayerProfile profile, string weaponOverrideId, string armorOverrideId, string accessoryOverrideId)
        {
            var result = new EquipmentBonus();
            if (profile == null || MasterDataManager.Instance == null)
            {
                return result;
            }

            AddEquipmentBonus(weaponOverrideId ?? profile.EquippedWeaponId, ref result);
            AddEquipmentBonus(armorOverrideId ?? profile.EquippedArmorId, ref result);
            AddEquipmentBonus(accessoryOverrideId ?? profile.EquippedAccessoryId, ref result);
            return result;
        }

        private static void AddEquipmentBonus(string equipmentId, ref EquipmentBonus bonus)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                return;
            }

            var equipmentData = MasterDataManager.Instance.GetEquipmentData(equipmentId);
            if (equipmentData == null)
            {
                return;
            }

            bonus.Attack += equipmentData.baseAttack;
            bonus.Wisdom += equipmentData.baseWisdom;
            bonus.Defense += equipmentData.baseDefense;
            bonus.MagicDefense += equipmentData.baseMagicDefense;
            bonus.Hp += equipmentData.baseHp;
            bonus.CritRate += equipmentData.bonusCritRate;
            bonus.AttackSpeed += equipmentData.bonusAttackSpeed;
        }

        private struct EquipmentBonus
        {
            public int Attack;
            public int Wisdom;
            public int Defense;
            public int MagicDefense;
            public int Hp;
            public float CritRate;
            public float AttackSpeed;
        }
    }
}
