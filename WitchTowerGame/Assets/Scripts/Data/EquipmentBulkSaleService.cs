using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    /// <summary>A review of exact owned instances, not a filter that can grow before confirmation.</summary>
    public sealed class EquipmentBulkSalePreview
    {
        internal readonly PlayerProfile Owner;
        internal readonly string PlayerId;
        internal readonly int Epoch;
        internal readonly string[] State;
        public ReadOnlyCollection<string> InstanceIds { get; }
        public int Gold { get; }
        internal EquipmentBulkSalePreview(PlayerProfile owner, string[] ids, string[] state, int gold)
        {
            Owner = owner;
            PlayerId = owner.PlayerId;
            Epoch = owner.RecoveryEpoch;
            InstanceIds = Array.AsReadOnly(ids);
            State = state;
            Gold = gold;
        }
    }

    public static class EquipmentBulkSaleService
    {
        public static bool MatchesFilter(OwnedEquipmentData equipment, int maximumQualityRank,
            bool excludeFavorites, bool unenhancedOnly)
        {
            if (equipment == null || maximumQualityRank < 1 || maximumQualityRank > 5) return false;
            var data = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
            if (data == null || EquipmentEnhancementCatalog.ResolveQualityRank(data, equipment) > maximumQualityRank) return false;
            if (excludeFavorites && equipment.IsFavorite) return false;
            return !unenhancedOnly || (equipment.UpgradeLevel == 0 && equipment.EnhancementBonusRate == 0f &&
                equipment.EnhancementAttackFlat == 0 && equipment.EnhancementWisdomFlat == 0 &&
                equipment.EnhancementDefenseFlat == 0 && equipment.EnhancementMagicDefenseFlat == 0 &&
                equipment.EnhancementHpFlat == 0 && equipment.EnhancementCritRateFlat == 0f &&
                equipment.EnhancementAttackSpeedFlat == 0f && equipment.RemainingEnhanceAttempts >= equipment.MaxEnhanceAttempts);
        }

        public static bool CanSell(PlayerProfile profile, OwnedEquipmentData equipment, out string reason)
        {
            reason = string.Empty;
            if (profile == null || equipment == null || string.IsNullOrEmpty(equipment.InstanceId) ||
                profile.OwnedEquipments.Count(x => x != null && x.InstanceId == equipment.InstanceId) != 1 ||
                !profile.OwnedEquipments.Contains(equipment)) reason = "対象装備を確認できません";
            else if (equipment.IsLocked) reason = "ロック中";
            else if (equipment.IsEquipped || !string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId) ||
                profile.OwnedMonsters.Any(x => x != null &&
                    (x.EquippedWeaponInstanceId == equipment.InstanceId ||
                     x.EquippedArmorInstanceId == equipment.InstanceId ||
                     x.EquippedAccessoryInstanceId == equipment.InstanceId)) ||
                IsSameEquipment(profile.GetEquippedWeapon(), equipment) ||
                IsSameEquipment(profile.GetEquippedArmor(), equipment) ||
                IsSameEquipment(profile.GetEquippedAccessory(), equipment)) reason = "装備中";
            else if (equipment.InstanceId.StartsWith("tutorial_", StringComparison.Ordinal) ||
                StoryTutorialService.IsEquipmentTutorialGift(equipment)) reason = "チュートリアル用";
            else if (MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId) == null) reason = "装備情報を確認できません";
            return reason.Length == 0;
        }

        private static bool IsSameEquipment(OwnedEquipmentData assigned, OwnedEquipmentData candidate)
        {
            return assigned != null && candidate != null && (ReferenceEquals(assigned, candidate) ||
                (!string.IsNullOrEmpty(assigned.InstanceId) && string.Equals(assigned.InstanceId, candidate.InstanceId, StringComparison.Ordinal)));
        }

        public static int GetSellGold(OwnedEquipmentData equipment)
        {
            EquipmentDataSO data = equipment != null ? MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId) : null;
            if (data == null) return 0;
            int rank = EquipmentEnhancementCatalog.ResolveQualityRank(data, equipment);
            return StageDropService.ResolveEquipmentAutoSellGold((EquipmentRarity)(rank - 1));
        }

        public static bool TryCreatePreview(PlayerProfile profile, IEnumerable<string> selectedIds,
            out EquipmentBulkSalePreview preview, out string message)
        {
            preview = null;
            message = "売却する装備を選択してください。";
            if (profile == null || selectedIds == null) return false;
            string[] ids = selectedIds.ToArray();
            if (ids.Length == 0) return false;
            if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            {
                message = "選択した装備を確認できません。選び直してください。";
                return false;
            }
            var state = new string[ids.Length];
            long gold = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                OwnedEquipmentData equipment = profile.GetOwnedEquipmentByInstanceId(ids[i]);
                if (!CanSell(profile, equipment, out string reason))
                {
                    message = "売却できない装備があります。選び直してください。" + reason;
                    return false;
                }
                state[i] = JsonUtility.ToJson(equipment);
                gold += GetSellGold(equipment);
            }
            if (gold <= 0 || gold > int.MaxValue || profile.Gold < 0 || (long)profile.Gold + gold > int.MaxValue)
            {
                message = "所持金の上限を超えるため売却できません。";
                return false;
            }
            preview = new EquipmentBulkSalePreview(profile, ids, state, (int)gold);
            message = ids.Length + "個を売却すると " + gold.ToString("N0") + " G を受け取ります。\n売却した装備は元に戻せません。";
            return true;
        }

        public static bool TryCommit(PlayerProfile profile, EquipmentBulkSalePreview preview, out string message)
        {
            return TryCommitWithPersistence(profile, preview, null, out message);
        }

        public static bool TryCommitWithPersistence(PlayerProfile profile, EquipmentBulkSalePreview preview,
            Func<bool> persist, out string message)
        {
            message = "装備の状態が変わりました。選択と金額を確認し直してください。";
            if (profile == null || preview == null || !ReferenceEquals(profile, preview.Owner) ||
                profile.PlayerId != preview.PlayerId || profile.RecoveryEpoch != preview.Epoch)
                return false;
            if (!TryCreatePreview(profile, preview.InstanceIds, out EquipmentBulkSalePreview current, out string _)) return false;
            if (current.Gold != preview.Gold || !current.State.SequenceEqual(preview.State, StringComparer.Ordinal)) return false;
            // Nothing has been mutated until every exact instance and the resulting wallet have passed.
            var selected = new HashSet<string>(preview.InstanceIds, StringComparer.Ordinal);
            OwnedEquipmentData[] originalInventory = profile.OwnedEquipments.ToArray();
            int originalGold = profile.Gold;
            Action restoreSaveNormalization = profile.CaptureEquipmentSaveNormalizationRollback();
            profile.OwnedEquipments.RemoveAll(x => x != null && selected.Contains(x.InstanceId));
            profile.Gold = checked(profile.Gold + preview.Gold);
            bool saved;
            try { saved = persist == null || persist(); }
            catch { saved = false; }
            if (!saved)
            {
                profile.OwnedEquipments.Clear();
                profile.OwnedEquipments.AddRange(originalInventory);
                profile.Gold = originalGold;
                restoreSaveNormalization();
                message = "保存できなかったため売却しませんでした。装備と所持金は変更していません。";
                return false;
            }
            message = preview.InstanceIds.Count + "個を売却しました。 +" + preview.Gold.ToString("N0") + " G";
            return true;
        }
    }
}
