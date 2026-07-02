using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class BattleFormationLayout
    {
        public static readonly Vector2[] AllyHomeAnchors =
        {
            new Vector2(0.38f, 0.30f),
            new Vector2(0.38f, 0.44f),
            new Vector2(0.18f, 0.37f),
            new Vector2(0.07f, 0.24f),
            new Vector2(0.07f, 0.50f)
        };

        public static readonly Vector2[] AllyAdvanceAnchors =
        {
            new Vector2(0.38f, 0.30f),
            new Vector2(0.38f, 0.44f),
            new Vector2(0.18f, 0.37f),
            new Vector2(0.07f, 0.24f),
            new Vector2(0.07f, 0.50f)
        };

        public static readonly float[] EnemyLaneYAnchors =
        {
            0.66f,
            0.56f,
            0.46f,
            0.36f,
            0.26f,
            0.18f
        };

        public static Vector2 ResolveAllyHomeAnchor(int index)
        {
            if (index < 0)
            {
                return AllyHomeAnchors[0];
            }

            return AllyHomeAnchors[Mathf.Clamp(index, 0, AllyHomeAnchors.Length - 1)];
        }

        public static Vector2 ResolveAllyAdvanceAnchor(int index)
        {
            if (index < 0)
            {
                return AllyAdvanceAnchors[0];
            }

            return AllyAdvanceAnchors[Mathf.Clamp(index, 0, AllyAdvanceAnchors.Length - 1)];
        }

        public static float ResolveEnemyLaneY(int index)
        {
            if (index < 0)
            {
                return EnemyLaneYAnchors[0];
            }

            return EnemyLaneYAnchors[Mathf.Clamp(index % EnemyLaneYAnchors.Length, 0, EnemyLaneYAnchors.Length - 1)];
        }

        public static Vector2 ClampAllyCombatAnchor(int allyIndex, MonsterDataSO monsterData, Vector2 desiredAnchor)
        {
            Vector2 homeAnchor = ResolveAllyHomeAnchor(allyIndex);
            return ClampAllyCombatAnchor(allyIndex, monsterData, desiredAnchor, homeAnchor);
        }

        public static Vector2 ClampAllyCombatAnchor(int allyIndex, MonsterDataSO monsterData, Vector2 desiredAnchor, Vector2 homeAnchor)
        {
            // Callers that shift a unit's home at runtime must pass that resolved home here.
            float maxAdvance = ResolveAllyMaxCombatAdvance(allyIndex, monsterData);
            float verticalLeash = ResolveAllyCombatVerticalLeash(allyIndex, monsterData);

            return new Vector2(
                Mathf.Clamp(desiredAnchor.x, homeAnchor.x, homeAnchor.x + maxAdvance),
                Mathf.Clamp(desiredAnchor.y, homeAnchor.y - verticalLeash, homeAnchor.y + verticalLeash));
        }

        public static float ResolveAllyMaxCombatAdvance(int allyIndex, MonsterDataSO monsterData)
        {
            bool isRanged = monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged;
            bool isDragon = IsDragonLineage(monsterData);
            bool isFrontline = allyIndex == 0 || allyIndex == 1;
            bool isMidline = allyIndex == 2;
            float maxAdvance = isFrontline
                ? (isRanged ? 0.26f : 0.24f)
                : isMidline
                    ? (isRanged ? 0.36f : 0.42f)
                    : (isRanged ? 0.40f : 0.48f);
            if (isDragon)
            {
                maxAdvance += isFrontline ? 0.04f : isMidline ? 0.12f : 0.08f;
            }

            return maxAdvance;
        }

        public static float ResolveAllyCombatVerticalLeash(int allyIndex, MonsterDataSO monsterData)
        {
            bool isRanged = monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged;
            bool isDragon = IsDragonLineage(monsterData);
            bool isFrontline = allyIndex == 0 || allyIndex == 1;
            bool isMidline = allyIndex == 2;
            float verticalLeash = isFrontline
                ? (isRanged ? 0.30f : 0.22f)
                : isMidline
                    ? (isRanged ? 0.32f : 0.24f)
                    : (isRanged ? 0.34f : 0.26f);
            if (isDragon)
            {
                verticalLeash += isFrontline ? 0.03f : 0.04f;
            }

            return verticalLeash;
        }

        private static bool IsDragonLineage(MonsterDataSO monsterData)
        {
            if (monsterData == null || string.IsNullOrEmpty(monsterData.monsterId))
            {
                return false;
            }

            string monsterId = monsterData.monsterId.ToLowerInvariant();
            return monsterId.Contains("dragon") || monsterId.Contains("drake");
        }
    }
}
