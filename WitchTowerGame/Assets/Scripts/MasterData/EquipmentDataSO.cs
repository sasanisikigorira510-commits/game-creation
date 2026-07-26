using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "WitchTower/MasterData/Equipment Data")]
    public sealed class EquipmentDataSO : ScriptableObject
    {
        public string equipmentId;
        public string equipmentName;
        public EquipmentSlotType slotType;
        [Min(0)] public int baseAttack;
        [Min(0)] public int baseWisdom;
        [Min(0)] public int baseDefense;
        [Min(0)] public int baseMagicDefense;
        [Min(0)] public int baseHp;
        [Min(0f)] public float bonusCritRate;
        [Min(0f)] public float bonusAttackSpeed;
        [Tooltip("0 uses the legacy rarity-based class. Set 1-6 for an explicit equipment class.")]
        [Min(0)] public int classRank;
        public EquipmentRarity rarity;
        [Min(1)] public int maxEnhancementAttempts = 5;

        private void OnValidate()
        {
            baseAttack = Mathf.Max(0, baseAttack);
            baseWisdom = Mathf.Max(0, baseWisdom);
            baseDefense = Mathf.Max(0, baseDefense);
            baseMagicDefense = Mathf.Max(0, baseMagicDefense);
            baseHp = Mathf.Max(0, baseHp);
            bonusCritRate = Mathf.Max(0f, bonusCritRate);
            bonusAttackSpeed = Mathf.Max(0f, bonusAttackSpeed);
            classRank = Mathf.Clamp(classRank, 0, 6);
        }
    }
}
