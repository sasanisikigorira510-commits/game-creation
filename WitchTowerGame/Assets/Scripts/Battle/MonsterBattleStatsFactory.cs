using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public static class MonsterBattleStatsFactory
    {
        private const int HpPerLevel = 8;
        private const int AttackPerLevel = 2;
        private const int WisdomPerLevel = 2;
        private const int DefensePerLevel = 1;
        private const int MagicDefensePerLevel = 1;

        public static BattleUnitStats Create(PlayerProfile profile, OwnedMonsterData ownedMonster, MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            int level = Mathf.Max(1, ownedMonster != null ? ownedMonster.Level : 1);
            int levelOffset = Mathf.Max(0, level - 1);
            int plusHp = Mathf.Max(0, ownedMonster != null ? ownedMonster.PlusHp : 0);
            int plusAttack = Mathf.Max(0, ownedMonster != null ? ownedMonster.PlusAttack : 0);
            int plusWisdom = Mathf.Max(0, ownedMonster != null ? ownedMonster.PlusWisdom : 0);
            int plusDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.PlusDefense : 0);
            int plusMagicDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.PlusMagicDefense : 0);
            int fusionBonusHp = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusHp : 0);
            int fusionBonusAttack = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusAttack : 0);
            int fusionBonusWisdom = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusWisdom : 0);
            int fusionBonusDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusDefense : 0);
            int fusionBonusMagicDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusMagicDefense : 0);
            float fusionBonusAttackSpeed = Mathf.Max(0f, ownedMonster != null ? ownedMonster.FusionBonusAttackSpeed : 0f);
            EquipmentResolvedBonus equipmentBonus = profile != null && ownedMonster != null
                ? profile.GetMonsterEquipmentBonus(ownedMonster.InstanceId)
                : default;

            int maxHp = Mathf.Max(1,
                monsterData.baseStats.maxHp +
                (levelOffset * HpPerLevel) +
                Mathf.RoundToInt(monsterData.plusGrowth.maxHpPerPlus * plusHp) +
                fusionBonusHp +
                equipmentBonus.Hp);
            int attack = Mathf.Max(1,
                monsterData.baseStats.attack +
                (levelOffset * AttackPerLevel) +
                Mathf.RoundToInt(monsterData.plusGrowth.attackPerPlus * plusAttack) +
                fusionBonusAttack +
                equipmentBonus.Attack);
            int wisdom = Mathf.Max(1,
                monsterData.baseStats.magicAttack +
                (levelOffset * WisdomPerLevel) +
                Mathf.RoundToInt(monsterData.plusGrowth.magicAttackPerPlus * plusWisdom) +
                fusionBonusWisdom +
                equipmentBonus.Wisdom);
            int defense = Mathf.Max(1,
                monsterData.baseStats.defense +
                (levelOffset * DefensePerLevel) +
                Mathf.RoundToInt(monsterData.plusGrowth.defensePerPlus * plusDefense) +
                fusionBonusDefense +
                equipmentBonus.Defense);
            int magicDefense = Mathf.Max(1,
                monsterData.baseStats.magicDefense +
                (levelOffset * MagicDefensePerLevel) +
                Mathf.RoundToInt(monsterData.plusGrowth.magicDefensePerPlus * plusMagicDefense) +
                fusionBonusMagicDefense +
                equipmentBonus.MagicDefense);
            float attackSpeed = Mathf.Max(0.2f, monsterData.baseStats.attackSpeed + fusionBonusAttackSpeed + equipmentBonus.AttackSpeed);
            float critRate = Mathf.Clamp01(0.05f + (((int)monsterData.rarity - 1) * 0.01f) + equipmentBonus.CritRate);
            float critDamage = 1.5f + (((int)monsterData.rarity - 1) * 0.05f);

            return new BattleUnitStats
            {
                MaxHp = maxHp,
                CurrentHp = maxHp,
                Attack = attack,
                Wisdom = wisdom,
                Defense = defense,
                MagicDefense = magicDefense,
                AttackSpeed = attackSpeed,
                CritRate = critRate,
                CritDamage = critDamage
            };
        }
    }
}
