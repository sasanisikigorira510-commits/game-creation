using UnityEngine;

namespace WitchTower.MasterData
{
    public readonly struct MonsterClassLevelGrowth
    {
        public MonsterClassLevelGrowth(float hp, float attack, float wisdom, float defense, float magicDefense, float attackSpeed)
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

    public static class MonsterGrowthUtility
    {
        public static MonsterClassLevelGrowth ResolveClassLevelGrowth(int classRank)
        {
            return Mathf.Max(1, classRank) switch
            {
                1 => new MonsterClassLevelGrowth(5.0f, 1.10f, 1.10f, 0.70f, 0.70f, 0.0020f),
                2 => new MonsterClassLevelGrowth(7.0f, 1.70f, 1.70f, 1.05f, 1.05f, 0.0018f),
                3 => new MonsterClassLevelGrowth(10.0f, 2.35f, 2.35f, 1.45f, 1.45f, 0.0015f),
                4 => new MonsterClassLevelGrowth(13.0f, 3.00f, 3.00f, 1.90f, 1.90f, 0.0012f),
                _ => new MonsterClassLevelGrowth(15.0f, 3.45f, 3.45f, 2.20f, 2.20f, 0.0010f)
            };
        }

        public static MonsterPlusGrowth ResolvePlusGrowth(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return default;
            }

            MonsterLevelGrowthCoefficients levelGrowth = monsterData.levelGrowth;
            return new MonsterPlusGrowth
            {
                maxHpPerPlus = ResolveIntegerPlusGrowth(monsterData.classRank, levelGrowth.maxHpCoefficient),
                attackPerPlus = ResolveIntegerPlusGrowth(monsterData.classRank, levelGrowth.attackCoefficient),
                magicAttackPerPlus = ResolveIntegerPlusGrowth(monsterData.classRank, levelGrowth.magicAttackCoefficient),
                defensePerPlus = ResolveIntegerPlusGrowth(monsterData.classRank, levelGrowth.defenseCoefficient),
                magicDefensePerPlus = ResolveIntegerPlusGrowth(monsterData.classRank, levelGrowth.magicDefenseCoefficient),
                attackSpeedPerPlus = ResolveAttackSpeedPlusGrowth(levelGrowth.attackSpeedCoefficient)
            };
        }

        public static bool AreEqual(MonsterPlusGrowth left, MonsterPlusGrowth right)
        {
            return left.maxHpPerPlus == right.maxHpPerPlus &&
                left.attackPerPlus == right.attackPerPlus &&
                left.magicAttackPerPlus == right.magicAttackPerPlus &&
                left.defensePerPlus == right.defensePerPlus &&
                left.magicDefensePerPlus == right.magicDefensePerPlus &&
                Mathf.Abs(left.attackSpeedPerPlus - right.attackSpeedPerPlus) < 0.0001f;
        }

        private static int ResolveIntegerPlusGrowth(int classRank, float coefficient)
        {
            int safeClassRank = Mathf.Max(1, classRank);
            int baseGrowth = safeClassRank <= 2 ? 1 : safeClassRank <= 4 ? 2 : 3;
            int tierBonus = 0;
            if (coefficient >= 1.55f)
            {
                tierBonus = 2;
            }
            else if (coefficient >= 1.20f)
            {
                tierBonus = 1;
            }
            else if (coefficient <= 0.80f)
            {
                tierBonus = -1;
            }

            return Mathf.Max(1, baseGrowth + tierBonus);
        }

        private static float ResolveAttackSpeedPlusGrowth(float coefficient)
        {
            if (coefficient >= 1.45f)
            {
                return 0.004f;
            }

            if (coefficient >= 1.15f)
            {
                return 0.003f;
            }

            if (coefficient >= 0.85f)
            {
                return 0.002f;
            }

            return 0.001f;
        }
    }
}
