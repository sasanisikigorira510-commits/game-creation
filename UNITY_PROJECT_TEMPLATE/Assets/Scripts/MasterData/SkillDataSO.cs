using UnityEngine;

namespace WitchTower.MasterData
{
    [CreateAssetMenu(fileName = "SkillData", menuName = "WitchTower/MasterData/Skill Data")]
    public sealed class SkillDataSO : ScriptableObject
    {
        public string skillId;
        public string skillName;
        [TextArea] public string description;
        public float cooldown = 5.0f;
        public float powerRate = 1.5f;
        public float healRate;
        public BuffType buffType;
        public float buffValue;
        public float buffDuration;
    }
}
