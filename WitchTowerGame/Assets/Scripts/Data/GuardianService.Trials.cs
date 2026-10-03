using UnityEngine;
using WitchTower.MasterData;
using WitchTower.Battle;

namespace WitchTower.Data
{
    public static partial class GuardianService
    {
        private static void ConfigureTrialCombatStats(EnemyDataSO data, BattleUnitStats stats, string id)
        {
            int hpMultiplier = id == "genbu" ? 24 : id == "byakko" ? 65 : id == "suzaku" ? 60 : 50;
            float attackMultiplier = id == "genbu" ? 3.6f : id == "byakko" ? 3f : 2.8f;
            data.maxHp = stats.MaxHp * hpMultiplier;
            data.attack = Mathf.RoundToInt(stats.Attack * attackMultiplier);
            data.magicAttack = Mathf.RoundToInt(stats.Wisdom * (id == "suzaku" ? 2.8f : attackMultiplier));
            data.defense = stats.Defense * 2;
            data.magicDefense = stats.MagicDefense * 2;
            data.attackSpeed = stats.AttackSpeed * (id == "genbu" ? 1.25f : .95f);
        }

        // Session opponents are transient and carry no drop/EXP/gold data.
        public static EnemyDataSO SessionEnemy(string id, int floor, GuardianTrialMode mode, int index, int level)
        {
            bool practice = mode == GuardianTrialMode.PracticeGroup || mode == GuardianTrialMode.PracticeElite;
            if (!practice && mode == GuardianTrialMode.Acquisition && index == 0) return TrialEnemy(id, floor);
            string key = "session_" + id + "_" + mode + "_" + floor + "_" + level + "_" + index;
            if (TrialCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var stats = Stats(id, mode == GuardianTrialMode.Oath ? MaxLevel : level);
            bool escort = !practice && index > 0;
            bool elite = mode == GuardianTrialMode.PracticeElite;
            data.enemyId = practice || escort
                ? (elite ? "enemy_class1_rock_golem" : index % 2 == 0 ? "enemy_class1_chibi_gear" : "enemy_class1_dragon_whelp")
                : "guardian_trial_" + id;
            data.enemyName = practice ? (elite ? "聖域の試し石" : "聖域の幻影")
                : escort ? "灯火の眷属" : Find(id).DisplayName + "の誓約";
            if (practice)
            {
                data.maxHp = Mathf.Max(12000, stats.MaxHp * (elite ? 60 : 20));
                data.attack = data.magicAttack = 10;
                data.defense = data.magicDefense = 0;
                data.attackSpeed = .7f;
            }
            else if (escort)
            {
                // Escorts follow the same acquisition tier as their guardian, not the
                // level of an as-yet unowned companion (which is always 1).
                var avatar = TrialEnemy(id, floor);
                float oathScale = mode == GuardianTrialMode.Oath ? 1.5f : 1f;
                data.maxHp = Mathf.RoundToInt(avatar.maxHp * .09f * oathScale);
                data.attack = data.magicAttack = Mathf.RoundToInt(Mathf.Max(avatar.attack, avatar.magicAttack) * .35f * oathScale);
                data.defense = avatar.defense / 2;
                data.magicDefense = avatar.magicDefense / 2;
                data.attackSpeed = .9f;
            }
            else
            {
                // Oaths must remain harder than the fourth acquisition trial.
                var avatar = TrialEnemy(id, 60);
                data.maxHp = Mathf.RoundToInt(avatar.maxHp * 4.5f);
                data.attack = Mathf.RoundToInt(avatar.attack * 2f);
                data.magicAttack = Mathf.RoundToInt(avatar.magicAttack * 2f);
                data.defense = Mathf.RoundToInt(avatar.defense * 1.2f);
                data.magicDefense = Mathf.RoundToInt(avatar.magicDefense * 1.2f);
                data.attackSpeed = avatar.attackSpeed * 1.1f;
            }
            data.damageType = id == "suzaku" && !practice ? MonsterDamageType.Magic : MonsterDamageType.Physical;
            data.attackRange = 1.4f;
            data.critRate = .03f;
            data.critDamage = 1.3f;
            data.battleIdleFacing = data.battleMoveFacing = data.battleAttackFacing = BattleFacingDirection.Right;
            TrialCache[key] = data;
            return data;
        }
    }
}
