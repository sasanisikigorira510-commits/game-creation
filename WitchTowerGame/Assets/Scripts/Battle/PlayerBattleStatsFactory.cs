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
            int maxHpBase = playerData.initialHp + GetPlayerLevelHpBonus(profile) + GetHpBonus(profile, hpLevelOffset);
            int attackBase = playerData.initialAttack + GetPlayerLevelAttackBonus(profile) + GetAttackBonus(profile, attackLevelOffset);
            int wisdomBase = playerData.initialAttack + GetPlayerLevelAttackBonus(profile) + GetAttackBonus(profile, attackLevelOffset);
            int defenseBase = playerData.initialDefense + GetPlayerLevelDefenseBonus(profile) + GetDefenseBonus(profile, defenseLevelOffset);
            return new BattleUnitStats
            {
                MaxHp = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(maxHpBase * (1f + equipmentBonus.HpPercent) * GetHpMultiplier(profile))),
                CurrentHp = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(maxHpBase * (1f + equipmentBonus.HpPercent) * GetHpMultiplier(profile))),
                Attack = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(attackBase * (1f + equipmentBonus.AttackPercent) * GetAttackMultiplier(profile))),
                Wisdom = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(wisdomBase * (1f + equipmentBonus.WisdomPercent) * GetAttackMultiplier(profile))),
                Defense = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(defenseBase * (1f + equipmentBonus.DefensePercent) * GetDefenseMultiplier(profile))),
                MagicDefense = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(defenseBase * (1f + equipmentBonus.MagicDefensePercent) * GetDefenseMultiplier(profile))),
                AttackSpeed = (playerData.initialAttackSpeed + equipmentBonus.AttackSpeed) * GetAttackSpeedMultiplier(profile),
                CritRate = UnityEngine.Mathf.Clamp01(playerData.initialCritRate + equipmentBonus.CritRate + GetCritRateBonus(profile)),
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
            int maxHpBase = 100 + GetPlayerLevelHpBonus(profile) + GetHpBonus(profile, hpLevelOffset);
            int attackBase = 15 + GetPlayerLevelAttackBonus(profile) + GetAttackBonus(profile, attackLevelOffset);
            int wisdomBase = 15 + GetPlayerLevelAttackBonus(profile) + GetAttackBonus(profile, attackLevelOffset);
            int defenseBase = 5 + GetPlayerLevelDefenseBonus(profile) + GetDefenseBonus(profile, defenseLevelOffset);
            return new BattleUnitStats
            {
                MaxHp = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(maxHpBase * (1f + equipmentBonus.HpPercent) * GetHpMultiplier(profile))),
                CurrentHp = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(maxHpBase * (1f + equipmentBonus.HpPercent) * GetHpMultiplier(profile))),
                Attack = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(attackBase * (1f + equipmentBonus.AttackPercent) * GetAttackMultiplier(profile))),
                Wisdom = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(wisdomBase * (1f + equipmentBonus.WisdomPercent) * GetAttackMultiplier(profile))),
                Defense = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(defenseBase * (1f + equipmentBonus.DefensePercent) * GetDefenseMultiplier(profile))),
                MagicDefense = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(defenseBase * (1f + equipmentBonus.MagicDefensePercent) * GetDefenseMultiplier(profile))),
                AttackSpeed = (1.0f + equipmentBonus.AttackSpeed) * GetAttackSpeedMultiplier(profile),
                CritRate = UnityEngine.Mathf.Clamp01(0.05f + equipmentBonus.CritRate + GetCritRateBonus(profile)),
                CritDamage = 1.5f
            };
        }

        private static int GetPlayerLevelAttackBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelAttackBonus() : 0;
        }

        private static int GetPlayerLevelDefenseBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelDefenseBonus() : 0;
        }

        private static int GetPlayerLevelHpBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetLevelHpBonus() : 0;
        }

        private static float GetAttackMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetAttackMultiplier() : 1f;
        }

        private static float GetDefenseMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetDefenseMultiplier() : 1f;
        }

        private static float GetHpMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetHpMultiplier() : 1f;
        }

        private static float GetAttackSpeedMultiplier(PlayerProfile profile)
        {
            return profile != null ? profile.GetAttackSpeedMultiplier() : 1f;
        }

        private static float GetCritRateBonus(PlayerProfile profile)
        {
            return profile != null ? profile.GetCritRateBonus() : 0f;
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

            bonus.AttackPercent += UnityEngine.Mathf.Max(0, equipmentData.baseAttack) / 100f;
            bonus.WisdomPercent += UnityEngine.Mathf.Max(0, equipmentData.baseWisdom) / 100f;
            bonus.DefensePercent += UnityEngine.Mathf.Max(0, equipmentData.baseDefense) / 100f;
            bonus.MagicDefensePercent += UnityEngine.Mathf.Max(0, equipmentData.baseMagicDefense) / 100f;
            bonus.HpPercent += UnityEngine.Mathf.Max(0, equipmentData.baseHp) / 100f;
            bonus.CritRate += UnityEngine.Mathf.Max(0f, equipmentData.bonusCritRate);
            bonus.AttackSpeed += UnityEngine.Mathf.Max(0f, equipmentData.bonusAttackSpeed);
        }

        private struct EquipmentBonus
        {
            public float AttackPercent;
            public float WisdomPercent;
            public float DefensePercent;
            public float MagicDefensePercent;
            public float HpPercent;
            public float CritRate;
            public float AttackSpeed;
        }
    }
}
