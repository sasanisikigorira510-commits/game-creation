using System.Collections.Generic;
using WitchTower.MasterData;
using UnityEngine;

namespace WitchTower.Battle
{
    public static class BattleAttackRangeResolver
    {
        private const float CombatReachOffsetPerRangeStep = 0.14f;
        private const float MaxCombatReachOffset = 0.24f;

        private static readonly Dictionary<string, float> MonsterAttackRangeDefaults = new Dictionary<string, float>
        {
            { "monster_rock_golem", 0.95f },
            { "monster_goblin", 1.00f },
            { "monster_hell_knight", 1.10f },
            { "monster_shadow", 1.00f },
            { "monster_soul_eater", 1.05f },
            { "monster_spectral_warrior", 1.15f },
            { "monster_vault_guard", 1.10f },
            { "monster_worm", 0.90f },
            { "monster_naga", 1.20f },
            { "monster_dragoon", 1.30f },
            { "monster_dragon_whelp", 1.05f },
            { "monster_flare_drake", 1.30f },
            { "monster_abyss_dragon", 2.25f },
            { "monster_bat", 1.75f },
            { "monster_bee", 1.60f },
            { "monster_centaur", 2.00f },
            { "monster_death_mage_elf", 2.40f },
            { "monster_ghost", 1.85f },
            { "monster_naga_mage", 2.25f },
            { "monster_wraith", 2.10f }
        };

        private static readonly Dictionary<string, float> EnemyAttackRangeDefaults = new Dictionary<string, float>
        {
            { "enemy_slime", 0.90f },
            { "enemy_guard", 1.10f },
            { "enemy_harpy", 1.75f },
            { "enemy_knight", 1.10f },
            { "enemy_wraith", 2.10f }
        };

        public static float ResolveMonsterAttackRange(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return 1.0f;
            }

            if (monsterData.attackRange > 0f)
            {
                return monsterData.attackRange;
            }

            if (!string.IsNullOrEmpty(monsterData.monsterId) &&
                MonsterAttackRangeDefaults.TryGetValue(monsterData.monsterId, out float defaultRange))
            {
                return defaultRange;
            }

            return monsterData.rangeType == MonsterRangeType.Ranged ? 1.8f : 1.0f;
        }

        public static float ResolveEnemyAttackRange(EnemyDataSO enemyData)
        {
            if (enemyData == null)
            {
                return 1.0f;
            }

            if (enemyData.attackRange > 0f)
            {
                return enemyData.attackRange;
            }

            if (!string.IsNullOrEmpty(enemyData.enemyId) &&
                EnemyAttackRangeDefaults.TryGetValue(enemyData.enemyId, out float defaultRange))
            {
                return defaultRange;
            }

            return 1.0f;
        }

        public static float ToAllyHoldOffset(float attackRange)
        {
            return ResolveCombatReachOffset(attackRange);
        }

        public static float ToEnemyHoldOffset(float attackRange)
        {
            return ResolveCombatReachOffset(attackRange);
        }

        public static float ResolveMonsterSearchRange(MonsterDataSO monsterData)
        {
            float attackRange = ResolveMonsterAttackRange(monsterData);
            float bonus = monsterData != null && monsterData.rangeType == MonsterRangeType.Ranged ? 1.00f : 0.70f;
            return attackRange + bonus;
        }

        public static float ResolveEnemySearchRange(EnemyDataSO enemyData)
        {
            float attackRange = ResolveEnemyAttackRange(enemyData);
            return attackRange + 0.80f;
        }

        public static float ToEnemySearchOffset(float searchRange)
        {
            return Mathf.Clamp((searchRange - 1.0f) * 0.065f, 0.08f, 0.24f);
        }

        public static float ToAllySearchOffset(float searchRange)
        {
            return Mathf.Clamp((searchRange - 1.0f) * 0.24f, 0.28f, 0.62f);
        }

        private static float ResolveCombatReachOffset(float attackRange)
        {
            // The battle field uses normalized anchors, so ranged units need a bigger
            // world-space offset than the raw attackRange number suggests.
            return Mathf.Clamp((attackRange - 1.0f) * CombatReachOffsetPerRangeStep, 0f, MaxCombatReachOffset);
        }

        public static float ResolveCombatSearchProgress(IReadOnlyList<float> allySearchRanges, float enemySearchRange)
        {
            float furthestRange = Mathf.Max(1.0f, enemySearchRange);
            if (allySearchRanges != null)
            {
                for (int i = 0; i < allySearchRanges.Count; i += 1)
                {
                    furthestRange = Mathf.Max(furthestRange, allySearchRanges[i]);
                }
            }

            float normalized = Mathf.Clamp01((furthestRange - 1.0f) / 2.2f);
            return Mathf.Lerp(0.88f, 0.46f, normalized);
        }

        public static float ResolveCombatStartProgress(IReadOnlyList<float> allyAttackRanges, float enemyAttackRange)
        {
            float furthestRange = Mathf.Max(1.0f, enemyAttackRange);
            if (allyAttackRanges != null)
            {
                for (int i = 0; i < allyAttackRanges.Count; i += 1)
                {
                    furthestRange = Mathf.Max(furthestRange, allyAttackRanges[i]);
                }
            }

            float normalized = Mathf.Clamp01((furthestRange - 1.0f) / 1.6f);
            return Mathf.Lerp(1.0f, 0.72f, normalized);
        }
    }
}
