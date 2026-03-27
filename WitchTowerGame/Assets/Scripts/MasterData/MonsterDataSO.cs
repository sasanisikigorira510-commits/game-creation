using UnityEngine;

namespace WitchTower.MasterData
{
    public enum MonsterElement
    {
        None = 0,
        Wood = 1,
        Water = 2,
        Fire = 3,
        Light = 4,
        Dark = 5
    }

    public enum MonsterRangeType
    {
        Melee = 0,
        Ranged = 1
    }

    public enum MonsterDamageType
    {
        Physical = 0,
        Magic = 1
    }

    public enum MonsterRarity
    {
        Iron = 1,
        Bronze = 2,
        Silver = 3,
        Gold = 4,
        Emerald = 5,
        Diamond = 6
    }

    [System.Serializable]
    public struct MonsterBaseStats
    {
        public int maxHp;
        public int attack;
        public int magicAttack;
        public int defense;
        public int magicDefense;
        public float attackSpeed;
    }

    [System.Serializable]
    public struct MonsterPlusGrowth
    {
        public int maxHpPerPlus;
        public int attackPerPlus;
        public int magicAttackPerPlus;
        public int defensePerPlus;
        public int magicDefensePerPlus;
        public float attackSpeedPerPlus;
    }

    [System.Serializable]
    public sealed class MonsterFusionRecipeEntry
    {
        public string parentMonsterIdA;
        public string parentMonsterIdB;
        public bool ignoreOrder = true;
        [TextArea] public string note;
    }

    [CreateAssetMenu(fileName = "MonsterData", menuName = "WitchTower/MasterData/Monster Data")]
    public sealed class MonsterDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string monsterId;
        public string monsterName;
        public int encyclopediaNumber;

        [Header("Classification")]
        public MonsterRarity rarity = MonsterRarity.Iron;
        public MonsterElement element = MonsterElement.None;
        public MonsterRangeType rangeType = MonsterRangeType.Melee;
        public MonsterDamageType damageType = MonsterDamageType.Physical;

        [Header("Battle")]
        public MonsterBaseStats baseStats;
        public int plusValueCap = 99;
        public MonsterPlusGrowth plusGrowth;

        [Header("Fusion")]
        public bool fusionExclusive;
        public MonsterFusionRecipeEntry[] fusionRecipes;

        [Header("Visuals")]
        public Sprite portraitSprite;
        public Sprite illustrationSprite;
        public string portraitResourcePath;
        public string illustrationResourcePath;
        public string battleIdleResourcePath;
        public string battleAttackResourcePath;

        [Header("Compendium")]
        [TextArea] public string description;
    }
}
