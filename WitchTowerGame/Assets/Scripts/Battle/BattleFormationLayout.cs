using UnityEngine;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class BattleFormationLayout
    {
        public static readonly Vector2[] AllyHomeAnchors =
        {
            new Vector2(0.09f, 0.26f),
            new Vector2(0.15f, 0.34f),
            new Vector2(0.09f, 0.44f),
            new Vector2(0.05f, 0.30f),
            new Vector2(0.05f, 0.40f)
        };

        public static readonly Vector2[] AllyAdvanceAnchors =
        {
            new Vector2(0.22f, 0.26f),
            new Vector2(0.27f, 0.34f),
            new Vector2(0.22f, 0.44f),
            new Vector2(0.16f, 0.30f),
            new Vector2(0.16f, 0.40f)
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
            bool isRanged = monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged;
            float maxAdvance = isRanged ? 0.14f : 0.22f;
            float verticalLeash = isRanged ? 0.10f : 0.15f;

            return new Vector2(
                Mathf.Clamp(desiredAnchor.x, homeAnchor.x - 0.01f, homeAnchor.x + maxAdvance),
                Mathf.Clamp(desiredAnchor.y, homeAnchor.y - verticalLeash, homeAnchor.y + verticalLeash));
        }
    }
}
