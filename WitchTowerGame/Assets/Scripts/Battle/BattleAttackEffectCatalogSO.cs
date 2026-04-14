using UnityEngine;

namespace WitchTower.Battle
{
    [CreateAssetMenu(fileName = "BattleAttackEffectCatalog", menuName = "WitchTower/Battle/Attack Effect Catalog")]
    public sealed class BattleAttackEffectCatalogSO : ScriptableObject
    {
        public BattleAttackEffectProfileSO[] profiles;

        public BattleAttackEffectProfileSO FindById(string effectId)
        {
            if (string.IsNullOrEmpty(effectId) || profiles == null)
            {
                return null;
            }

            for (int i = 0; i < profiles.Length; i += 1)
            {
                BattleAttackEffectProfileSO profile = profiles[i];
                if (profile != null && profile.effectId == effectId)
                {
                    return profile;
                }
            }

            return null;
        }
    }
}
