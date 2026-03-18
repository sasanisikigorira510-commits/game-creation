using System;
using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "DropTableData", menuName = "WitchTower/MasterData/Drop Table Data")]
    public sealed class DropTableDataSO : ScriptableObject
    {
        public string dropTableId;
        public int minGold;
        public int maxGold;
        public MaterialDropEntry[] materialDrops;
    }

    [Serializable]
    public struct MaterialDropEntry
    {
        public string materialId;
        public int amount;
        [Range(0f, 1f)] public float dropRate;
    }
}
