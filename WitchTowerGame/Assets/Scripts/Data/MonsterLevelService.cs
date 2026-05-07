using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public static class MonsterLevelService
    {
        public const int LevelsPerClassRank = 20;

        private const int BaseRequiredExp = 30;
        private const int LinearRequiredExpPerLevel = 10;
        private const float QuadraticRequiredExpPerLevel = 0.8f;
        private const float ClassRequiredExpMultiplierStep = 0.55f;

        public static int GetMaxLevel(MonsterDataSO monsterData)
        {
            return GetMaxLevel(monsterData != null ? monsterData.classRank : 1);
        }

        public static int GetMaxLevel(int classRank)
        {
            return Mathf.Max(1, classRank) * LevelsPerClassRank;
        }

        public static int ClampLevelToMax(int level, MonsterDataSO monsterData)
        {
            return Mathf.Clamp(Mathf.Max(1, level), 1, GetMaxLevel(monsterData));
        }

        public static bool IsAtMaxLevel(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            if (monster == null)
            {
                return false;
            }

            return Mathf.Max(1, monster.Level) >= GetMaxLevel(monsterData);
        }

        public static int GetRequiredExpForNextLevel(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            if (monster == null)
            {
                return GetRequiredExpForNextLevel(1, monsterData);
            }

            return GetRequiredExpForNextLevel(monster.Level, monsterData);
        }

        public static int GetRequiredExpForNextLevel(int currentLevel, MonsterDataSO monsterData)
        {
            int classRank = Mathf.Max(1, monsterData != null ? monsterData.classRank : 1);
            int maxLevel = GetMaxLevel(classRank);
            int level = Mathf.Clamp(Mathf.Max(1, currentLevel), 1, maxLevel);
            if (level >= maxLevel)
            {
                return 0;
            }

            float classMultiplier = 1f + ((classRank - 1) * ClassRequiredExpMultiplierStep);
            float levelComponent =
                BaseRequiredExp +
                (level * LinearRequiredExpPerLevel) +
                (level * level * QuadraticRequiredExpPerLevel);

            return Mathf.Max(1, Mathf.RoundToInt(levelComponent * classMultiplier));
        }

        public static int AddExperience(OwnedMonsterData monster, MonsterDataSO monsterData, int amount)
        {
            if (monster == null || amount <= 0)
            {
                return 0;
            }

            int maxLevel = GetMaxLevel(monsterData);
            monster.Level = Mathf.Clamp(Mathf.Max(1, monster.Level), 1, maxLevel);
            if (monster.Level >= maxLevel)
            {
                monster.Exp = 0;
                return 0;
            }

            monster.Exp = Mathf.Max(0, monster.Exp) + amount;
            int levelUps = 0;
            int requiredExp = GetRequiredExpForNextLevel(monster.Level, monsterData);
            while (requiredExp > 0 && monster.Exp >= requiredExp && monster.Level < maxLevel)
            {
                monster.Exp -= requiredExp;
                monster.Level += 1;
                levelUps += 1;
                requiredExp = GetRequiredExpForNextLevel(monster.Level, monsterData);
            }

            if (monster.Level >= maxLevel)
            {
                monster.Level = maxLevel;
                monster.Exp = 0;
            }

            return levelUps;
        }
    }
}
