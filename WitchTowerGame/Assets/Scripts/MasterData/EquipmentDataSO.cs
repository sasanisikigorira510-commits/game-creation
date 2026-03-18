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
        public int baseDefense;
        public int baseHp;
        public float bonusCritRate;
        public float bonusAttackSpeed;
        public EquipmentRarity rarity;
    }
}
