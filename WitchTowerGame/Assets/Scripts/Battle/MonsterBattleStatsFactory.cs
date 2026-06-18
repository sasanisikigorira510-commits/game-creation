using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public static class MonsterBattleStatsFactory
    {
        public static BattleUnitStats Create(PlayerProfile profile, OwnedMonsterData ownedMonster, MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            int level = MonsterLevelService.ClampLevelToMax(ownedMonster != null ? ownedMonster.Level : 1, monsterData);
            int levelOffset = Mathf.Max(0, level - 1);
            MonsterClassLevelGrowth classLevelGrowth = MonsterGrowthUtility.ResolveClassLevelGrowth(monsterData.classRank);
            MonsterLevelGrowthCoefficients levelGrowth = monsterData.levelGrowth;
            MonsterPlusGrowth plusGrowth = MonsterGrowthUtility.ResolvePlusGrowth(monsterData);
            int plusValue = ResolvePlusValue(ownedMonster);
            int fusionBonusHp = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusHp : 0);
            int fusionBonusAttack = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusAttack : 0);
            int fusionBonusWisdom = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusWisdom : 0);
            int fusionBonusDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusDefense : 0);
            int fusionBonusMagicDefense = Mathf.Max(0, ownedMonster != null ? ownedMonster.FusionBonusMagicDefense : 0);
            float fusionBonusAttackSpeed = Mathf.Max(0f, ownedMonster != null ? ownedMonster.FusionBonusAttackSpeed : 0f);
            EquipmentResolvedBonus equipmentBonus = profile != null && ownedMonster != null
                ? profile.GetMonsterEquipmentBonus(ownedMonster.InstanceId)
                : default;
            MonsterIndividualValueService.EnsureInitialized(ownedMonster);

            int intrinsicMaxHp =
                monsterData.baseStats.maxHp +
                ResolveIntegerLevelGrowth(levelOffset, classLevelGrowth.Hp, levelGrowth.maxHpCoefficient);
            int intrinsicAttack =
                monsterData.baseStats.attack +
                ResolveIntegerLevelGrowth(levelOffset, classLevelGrowth.Attack, levelGrowth.attackCoefficient);
            int intrinsicWisdom =
                monsterData.baseStats.magicAttack +
                ResolveIntegerLevelGrowth(levelOffset, classLevelGrowth.Wisdom, levelGrowth.magicAttackCoefficient);
            int intrinsicDefense =
                monsterData.baseStats.defense +
                ResolveIntegerLevelGrowth(levelOffset, classLevelGrowth.Defense, levelGrowth.defenseCoefficient);
            int intrinsicMagicDefense =
                monsterData.baseStats.magicDefense +
                ResolveIntegerLevelGrowth(levelOffset, classLevelGrowth.MagicDefense, levelGrowth.magicDefenseCoefficient);
            float intrinsicAttackSpeed =
                monsterData.baseStats.attackSpeed +
                ResolveFloatLevelGrowth(levelOffset, classLevelGrowth.AttackSpeed, levelGrowth.attackSpeedCoefficient);

            int maxHpBase =
                ResolveIndividualIntegerStat(intrinsicMaxHp, ownedMonster != null ? ownedMonster.IndividualHp : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(plusGrowth.maxHpPerPlus * plusValue) +
                fusionBonusHp;
            int attackBase =
                ResolveIndividualIntegerStat(intrinsicAttack, ownedMonster != null ? ownedMonster.IndividualAttack : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(plusGrowth.attackPerPlus * plusValue) +
                fusionBonusAttack;
            int wisdomBase =
                ResolveIndividualIntegerStat(intrinsicWisdom, ownedMonster != null ? ownedMonster.IndividualWisdom : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(plusGrowth.magicAttackPerPlus * plusValue) +
                fusionBonusWisdom;
            int defenseBase =
                ResolveIndividualIntegerStat(intrinsicDefense, ownedMonster != null ? ownedMonster.IndividualDefense : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(plusGrowth.defensePerPlus * plusValue) +
                fusionBonusDefense;
            int magicDefenseBase =
                ResolveIndividualIntegerStat(intrinsicMagicDefense, ownedMonster != null ? ownedMonster.IndividualMagicDefense : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(plusGrowth.magicDefensePerPlus * plusValue) +
                fusionBonusMagicDefense;
            int maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHpBase * (1f + Mathf.Max(0f, equipmentBonus.HpPercent))));
            int attack = Mathf.Max(1, Mathf.RoundToInt(attackBase * (1f + Mathf.Max(0f, equipmentBonus.AttackPercent))));
            int wisdom = Mathf.Max(1, Mathf.RoundToInt(wisdomBase * (1f + Mathf.Max(0f, equipmentBonus.WisdomPercent))));
            int defense = Mathf.Max(1, Mathf.RoundToInt(defenseBase * (1f + Mathf.Max(0f, equipmentBonus.DefensePercent))));
            int magicDefense = Mathf.Max(1, Mathf.RoundToInt(magicDefenseBase * (1f + Mathf.Max(0f, equipmentBonus.MagicDefensePercent))));
            float attackSpeed = Mathf.Max(0.2f,
                ResolveIndividualAttackSpeed(intrinsicAttackSpeed, ownedMonster != null ? ownedMonster.IndividualAttackSpeed : MonsterIndividualValueService.DefaultValue) +
                (plusGrowth.attackSpeedPerPlus * plusValue) +
                fusionBonusAttackSpeed +
                equipmentBonus.AttackSpeed);
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

        private static int ResolvePlusValue(OwnedMonsterData ownedMonster)
        {
            if (ownedMonster == null)
            {
                return 0;
            }

            int plusValue = Mathf.Max(0, ownedMonster.PlusValue);
            plusValue = Mathf.Max(plusValue, Mathf.Max(0, ownedMonster.PlusHp));
            plusValue = Mathf.Max(plusValue, Mathf.Max(0, ownedMonster.PlusAttack));
            plusValue = Mathf.Max(plusValue, Mathf.Max(0, ownedMonster.PlusWisdom));
            plusValue = Mathf.Max(plusValue, Mathf.Max(0, ownedMonster.PlusDefense));
            plusValue = Mathf.Max(plusValue, Mathf.Max(0, ownedMonster.PlusMagicDefense));
            return plusValue;
        }

        private static int ResolveIntegerLevelGrowth(int levelOffset, float classBaseGrowth, float monsterCoefficient)
        {
            float totalGrowth = ResolveFloatLevelGrowth(levelOffset, classBaseGrowth, monsterCoefficient);
            return Mathf.Max(0, Mathf.FloorToInt(totalGrowth + 0.5f));
        }

        private static float ResolveFloatLevelGrowth(int levelOffset, float classBaseGrowth, float monsterCoefficient)
        {
            if (levelOffset <= 0 || classBaseGrowth <= 0f)
            {
                return 0f;
            }

            float coefficient = monsterCoefficient > 0f ? monsterCoefficient : 1f;
            return levelOffset * classBaseGrowth * coefficient;
        }

        private static int ResolveIndividualIntegerStat(int intrinsicValue, int individualValue)
        {
            float multiplier = MonsterIndividualValueService.ResolveIntegerStatMultiplier(individualValue);
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, intrinsicValue) * multiplier));
        }

        private static float ResolveIndividualAttackSpeed(float intrinsicValue, int individualValue)
        {
            float multiplier = MonsterIndividualValueService.ResolveAttackSpeedMultiplier(individualValue);
            return Mathf.Max(0f, intrinsicValue) * multiplier;
        }
    }
}
