using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "WitchTower/MasterData/Equipment Data")]
    public sealed class EquipmentDataSO : ScriptableObject
    {
        public string equipmentId;
        public string equipmentName;
        public EquipmentSlotType slotType;
        public int baseAttack;
        public int baseWisdom;
        public int baseDefense;
        public int baseMagicDefense;
        public int baseHp;
        public float bonusCritRate;
        public float bonusAttackSpeed;
        public EquipmentRarity rarity;
        [Range(0f, 0.5f)] public float statVarianceRate = 0.20f;
        [Min(1)] public int maxEnhancementAttempts = 5;
    }
}
