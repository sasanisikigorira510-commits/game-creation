using UnityEngine;
using WitchTower.Data;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public static class MonsterBattleStatsFactory
    {
        private readonly struct ClassLevelGrowth
        {
            public ClassLevelGrowth(float hp, float attack, float wisdom, float defense, float magicDefense, float attackSpeed)
            {
                Hp = hp;
                Attack = attack;
                Wisdom = wisdom;
                Defense = defense;
                MagicDefense = magicDefense;
                AttackSpeed = attackSpeed;
            }

            public float Hp { get; }
            public float Attack { get; }
            public float Wisdom { get; }
            public float Defense { get; }
            public float MagicDefense { get; }
            public float AttackSpeed { get; }
        }

        public static BattleUnitStats Create(PlayerProfile profile, OwnedMonsterData ownedMonster, MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            int level = MonsterLevelService.ClampLevelToMax(ownedMonster != null ? ownedMonster.Level : 1, monsterData);
            int levelOffset = Mathf.Max(0, level - 1);
            ClassLevelGrowth classLevelGrowth = ResolveClassLevelGrowth(monsterData.classRank);
            MonsterLevelGrowthCoefficients levelGrowth = monsterData.levelGrowth;
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

            int maxHp = Mathf.Max(1,
                ResolveIndividualIntegerStat(intrinsicMaxHp, ownedMonster != null ? ownedMonster.IndividualHp : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(monsterData.plusGrowth.maxHpPerPlus * plusHp) +
                fusionBonusHp +
                equipmentBonus.Hp);
            int attack = Mathf.Max(1,
                ResolveIndividualIntegerStat(intrinsicAttack, ownedMonster != null ? ownedMonster.IndividualAttack : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(monsterData.plusGrowth.attackPerPlus * plusAttack) +
                fusionBonusAttack +
                equipmentBonus.Attack);
            int wisdom = Mathf.Max(1,
                ResolveIndividualIntegerStat(intrinsicWisdom, ownedMonster != null ? ownedMonster.IndividualWisdom : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(monsterData.plusGrowth.magicAttackPerPlus * plusWisdom) +
                fusionBonusWisdom +
                equipmentBonus.Wisdom);
            int defense = Mathf.Max(1,
                ResolveIndividualIntegerStat(intrinsicDefense, ownedMonster != null ? ownedMonster.IndividualDefense : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(monsterData.plusGrowth.defensePerPlus * plusDefense) +
                fusionBonusDefense +
                equipmentBonus.Defense);
            int magicDefense = Mathf.Max(1,
                ResolveIndividualIntegerStat(intrinsicMagicDefense, ownedMonster != null ? ownedMonster.IndividualMagicDefense : MonsterIndividualValueService.DefaultValue) +
                Mathf.RoundToInt(monsterData.plusGrowth.magicDefensePerPlus * plusMagicDefense) +
                fusionBonusMagicDefense +
                equipmentBonus.MagicDefense);
            float attackSpeed = Mathf.Max(0.2f,
                ResolveIndividualAttackSpeed(intrinsicAttackSpeed, ownedMonster != null ? ownedMonster.IndividualAttackSpeed : MonsterIndividualValueService.DefaultValue) +
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

        private static ClassLevelGrowth ResolveClassLevelGrowth(int classRank)
        {
            return Mathf.Max(1, classRank) switch
            {
                1 => new ClassLevelGrowth(5.0f, 1.10f, 1.10f, 0.70f, 0.70f, 0.0020f),
                2 => new ClassLevelGrowth(7.0f, 1.70f, 1.70f, 1.05f, 1.05f, 0.0018f),
                3 => new ClassLevelGrowth(10.0f, 2.35f, 2.35f, 1.45f, 1.45f, 0.0015f),
                4 => new ClassLevelGrowth(13.0f, 3.00f, 3.00f, 1.90f, 1.90f, 0.0012f),
                _ => new ClassLevelGrowth(15.0f, 3.45f, 3.45f, 2.20f, 2.20f, 0.0010f)
            };
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
