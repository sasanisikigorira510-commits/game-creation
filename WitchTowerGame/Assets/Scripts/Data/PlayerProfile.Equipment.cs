using System;
using System.Linq;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed partial class PlayerProfile
    {
        private static readonly System.Random EnhancementRandom = new System.Random();

        private string legacyEquippedWeaponId;
        private string legacyEquippedArmorId;
        private string legacyEquippedAccessoryId;

        public string EquippedWeaponId
        {
            get => GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Weapon) ?? legacyEquippedWeaponId;
            set
            {
                legacyEquippedWeaponId = value ?? string.Empty;
                TryEquipLegacyRepresentative(EquipmentSlotType.Weapon, legacyEquippedWeaponId);
            }
        }

        public string EquippedArmorId
        {
            get => GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Armor) ?? legacyEquippedArmorId;
            set
            {
                legacyEquippedArmorId = value ?? string.Empty;
                TryEquipLegacyRepresentative(EquipmentSlotType.Armor, legacyEquippedArmorId);
            }
        }

        public string EquippedAccessoryId
        {
            get => GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Accessory) ?? legacyEquippedAccessoryId;
            set
            {
                legacyEquippedAccessoryId = value ?? string.Empty;
                TryEquipLegacyRepresentative(EquipmentSlotType.Accessory, legacyEquippedAccessoryId);
            }
        }

        public OwnedEquipmentData GetEquippedWeapon()
        {
            return GetRepresentativeEquippedEquipment(EquipmentSlotType.Weapon) ?? GetFirstOwnedEquipmentByEquipmentId(legacyEquippedWeaponId);
        }

        public OwnedEquipmentData GetEquippedArmor()
        {
            return GetRepresentativeEquippedEquipment(EquipmentSlotType.Armor) ?? GetFirstOwnedEquipmentByEquipmentId(legacyEquippedArmorId);
        }

        public OwnedEquipmentData GetEquippedAccessory()
        {
            return GetRepresentativeEquippedEquipment(EquipmentSlotType.Accessory) ?? GetFirstOwnedEquipmentByEquipmentId(legacyEquippedAccessoryId);
        }

        public void EquipWeapon(string equipmentId) { legacyEquippedWeaponId = equipmentId ?? string.Empty; TryEquipLegacyRepresentative(EquipmentSlotType.Weapon, equipmentId); }
        public void EquipArmor(string equipmentId) { legacyEquippedArmorId = equipmentId ?? string.Empty; TryEquipLegacyRepresentative(EquipmentSlotType.Armor, equipmentId); }
        public void EquipAccessory(string equipmentId) { legacyEquippedAccessoryId = equipmentId ?? string.Empty; TryEquipLegacyRepresentative(EquipmentSlotType.Accessory, equipmentId); }

        public bool HasEquipment(string equipmentId) => OwnedEquipments.Any(x => x != null && x.EquipmentId == equipmentId);

        public OwnedEquipmentData GetOwnedEquipmentByInstanceId(string instanceId)
        {
            return OwnedEquipments.FirstOrDefault(x => x != null && x.InstanceId == instanceId);
        }

        public OwnedEquipmentData GetFirstOwnedEquipmentByEquipmentId(string equipmentId)
        {
            return OwnedEquipments.FirstOrDefault(x => x != null && x.EquipmentId == equipmentId);
        }

        public OwnedEquipmentData GetMonsterEquippedEquipment(string monsterInstanceId, EquipmentSlotType slotType)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            return monster == null ? null : GetOwnedEquipmentByInstanceId(GetMonsterEquipmentInstanceId(monster, slotType));
        }

        public bool AddOwnedEquipment(string equipmentId)
        {
            return CreateOwnedEquipmentInstance(equipmentId) != null;
        }

        public bool EquipEquipmentToMonster(string monsterInstanceId, string equipmentInstanceId)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (monster == null || equipment == null) return false;

            EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
            if (equipmentData == null) return false;

            if (!string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId))
            {
                OwnedMonsterData previousMonster = GetOwnedMonster(equipment.EquippedMonsterInstanceId);
                if (previousMonster != null) ClearMonsterEquipmentSlot(previousMonster, equipmentData.slotType, equipment.InstanceId);
            }

            string currentSlotEquipmentId = GetMonsterEquipmentInstanceId(monster, equipmentData.slotType);
            if (!string.IsNullOrEmpty(currentSlotEquipmentId))
            {
                OwnedEquipmentData currentEquipment = GetOwnedEquipmentByInstanceId(currentSlotEquipmentId);
                if (currentEquipment != null)
                {
                    currentEquipment.EquippedMonsterInstanceId = string.Empty;
                    currentEquipment.IsEquipped = false;
                }
            }

            SetMonsterEquipmentInstanceId(monster, equipmentData.slotType, equipment.InstanceId);
            equipment.EquippedMonsterInstanceId = monster.InstanceId;
            SyncEquippedFlags();
            return true;
        }

        public bool ToggleEquipmentLock(string equipmentInstanceId)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (equipment == null) return false;
            equipment.IsLocked = !equipment.IsLocked;
            return equipment.IsLocked;
        }

        public bool TryDiscardEquipment(string equipmentInstanceId, out string message)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (equipment == null)
            {
                message = "対象装備が見つかりません。";
                return false;
            }

            if (equipment.IsLocked)
            {
                message = "ロック中の装備は捨てられません。";
                return false;
            }

            string displayName = GetEquipmentDisplayName(equipment.EquipmentId);
            RemoveOwnedEquipment(equipmentInstanceId, false);
            message = displayName + " を捨てました。";
            return true;
        }

        public int GetEnhancementRelicAmount(string relicId)
        {
            OwnedEnhancementRelicData relic = OwnedEnhancementRelics.FirstOrDefault(x => x != null && x.RelicId == relicId);
            return relic != null ? Math.Max(0, relic.Amount) : 0;
        }

        public void AddEnhancementRelics(string relicId, int amount)
        {
            if (string.IsNullOrEmpty(relicId) || amount <= 0) return;
            OwnedEnhancementRelicData relic = OwnedEnhancementRelics.FirstOrDefault(x => x != null && x.RelicId == relicId);
            if (relic == null)
            {
                OwnedEnhancementRelics.Add(new OwnedEnhancementRelicData { RelicId = relicId, Amount = amount });
                return;
            }

            relic.Amount += amount;
        }

        public EquipmentEnhancementResult TryEnhanceEquipment(string equipmentInstanceId, string relicId)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            EquipmentDataSO equipmentData = equipment != null ? MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId) : null;
            EquipmentEnhancementCatalog.EnsureRolledStats(equipmentData, equipment, EnhancementRandom);
            EnhancementRelicDefinition relic = EquipmentEnhancementCatalog.GetRelic(relicId);
            var result = new EquipmentEnhancementResult
            {
                EquipmentInstanceId = equipmentInstanceId,
                EquipmentId = equipment != null ? equipment.EquipmentId : string.Empty,
                RelicId = relicId
            };

            if (equipment == null) return SetEnhanceResult(result, EquipmentEnhancementResultType.InvalidEquipment, "対象装備が見つかりません。");
            if (relic == null) return SetEnhanceResult(result, EquipmentEnhancementResultType.InvalidRelic, "強化遺物が見つかりません。");
            if (equipment.RemainingEnhanceAttempts <= 0) return SetEnhanceResult(result, EquipmentEnhancementResultType.NoAttempts, "強化可能回数が残っていません。");
            if (GetEnhancementRelicAmount(relic.RelicId) <= 0) return SetEnhanceResult(result, EquipmentEnhancementResultType.NoRelic, $"{relic.RelicName}を所持していません。");
            if (equipment.IsLocked && relic.DestroysOnFailure) return SetEnhanceResult(result, EquipmentEnhancementResultType.Locked, "ロック中の装備には破壊系遺物を使用できません。");

            ConsumeEnhancementRelic(relic.RelicId);
            equipment.RemainingEnhanceAttempts = Math.Max(0, equipment.RemainingEnhanceAttempts - 1);
            result.ConsumedRelic = true;
            result.ConsumedAttempt = true;

            bool success = relic.SuccessRate >= 0.999f || EnhancementRandom.NextDouble() <= relic.SuccessRate;
            if (success)
            {
                equipment.UpgradeLevel += 1;
                EquipmentEnhancementCatalog.ApplyEnhancementSuccess(equipmentData, equipment, relic);
                return SetEnhanceResult(result, EquipmentEnhancementResultType.Success, $"{GetEquipmentDisplayName(equipment.EquipmentId)}の強化に成功しました。");
            }

            if (relic.DestroysOnFailure)
            {
                RemoveOwnedEquipment(equipment.InstanceId, true);
                return SetEnhanceResult(result, EquipmentEnhancementResultType.Destroyed, $"{GetEquipmentDisplayName(equipment.EquipmentId)}は失敗して消滅しました。");
            }

            return SetEnhanceResult(result, EquipmentEnhancementResultType.Failed, $"{GetEquipmentDisplayName(equipment.EquipmentId)}の強化に失敗しました。");
        }

        public EquipmentResolvedBonus GetMonsterEquipmentBonus(string monsterInstanceId)
        {
            OwnedMonsterData monster = GetOwnedMonster(monsterInstanceId);
            if (monster == null) return default;
            return ResolveEquipmentBonus(monster.EquippedWeaponInstanceId) + ResolveEquipmentBonus(monster.EquippedArmorInstanceId) + ResolveEquipmentBonus(monster.EquippedAccessoryInstanceId);
        }

        partial void InitializeEquipmentState(PlayerSaveData saveData)
        {
            legacyEquippedWeaponId = string.IsNullOrEmpty(saveData.EquippedWeaponId) ? "equip_bronze_blade" : saveData.EquippedWeaponId;
            legacyEquippedArmorId = string.IsNullOrEmpty(saveData.EquippedArmorId) ? "equip_guard_cloth" : saveData.EquippedArmorId;
            legacyEquippedAccessoryId = string.IsNullOrEmpty(saveData.EquippedAccessoryId) ? "equip_ashen_ring" : saveData.EquippedAccessoryId;

            foreach (OwnedEquipmentData equipment in OwnedEquipments)
            {
                if (equipment == null || string.IsNullOrEmpty(equipment.EquipmentId)) continue;
                EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
                bool legacyData = string.IsNullOrEmpty(equipment.InstanceId);
                if (legacyData) equipment.InstanceId = equipment.EquipmentId + "_" + Guid.NewGuid().ToString("N");
                if (legacyData)
                {
                    equipment.RemainingEnhanceAttempts = ResolveInitialEnhanceAttempts(equipment.EquipmentId);
                    equipment.EnhancementBonusRate = 0f;
                    equipment.EnhancementAttackFlat = 0;
                    equipment.EnhancementWisdomFlat = 0;
                    equipment.EnhancementDefenseFlat = 0;
                    equipment.EnhancementMagicDefenseFlat = 0;
                    equipment.EnhancementHpFlat = 0;
                    equipment.EnhancementAttackSpeedFlat = 0f;
                    equipment.IsLocked = false;
                    equipment.EquippedMonsterInstanceId = string.Empty;
                }
                else if (equipment.RemainingEnhanceAttempts < 0) equipment.RemainingEnhanceAttempts = 0;

                EquipmentEnhancementCatalog.EnsureRolledStats(equipmentData, equipment, EnhancementRandom);
            }

            foreach (OwnedMonsterData monster in OwnedMonsters)
            {
                if (monster == null) continue;
                monster.EquippedWeaponInstanceId ??= string.Empty;
                monster.EquippedArmorInstanceId ??= string.Empty;
                monster.EquippedAccessoryInstanceId ??= string.Empty;
                SyncEquipmentOwnershipFromMonsterSlot(monster, EquipmentSlotType.Weapon);
                SyncEquipmentOwnershipFromMonsterSlot(monster, EquipmentSlotType.Armor);
                SyncEquipmentOwnershipFromMonsterSlot(monster, EquipmentSlotType.Accessory);
            }

            if (OwnedEnhancementRelics.Count == 0)
            {
                OwnedEnhancementRelics.Add(new OwnedEnhancementRelicData { RelicId = "relic_safe_ember", Amount = 24 });
                OwnedEnhancementRelics.Add(new OwnedEnhancementRelicData { RelicId = "relic_risky_ember", Amount = 12 });
                OwnedEnhancementRelics.Add(new OwnedEnhancementRelicData { RelicId = "relic_volatile_ember", Amount = 6 });
            }

            if (!OwnedMonsters.Any(x => x != null && (!string.IsNullOrEmpty(x.EquippedWeaponInstanceId) || !string.IsNullOrEmpty(x.EquippedArmorInstanceId) || !string.IsNullOrEmpty(x.EquippedAccessoryInstanceId))))
            {
                TryEquipLegacyRepresentative(EquipmentSlotType.Weapon, legacyEquippedWeaponId);
                TryEquipLegacyRepresentative(EquipmentSlotType.Armor, legacyEquippedArmorId);
                TryEquipLegacyRepresentative(EquipmentSlotType.Accessory, legacyEquippedAccessoryId);
            }

            SyncLegacyRepresentativeEquipmentIds();
            SyncEquippedFlags();
        }

        partial void SyncLegacyRepresentativeEquipmentIds()
        {
            legacyEquippedWeaponId = GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Weapon) ?? legacyEquippedWeaponId;
            legacyEquippedArmorId = GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Armor) ?? legacyEquippedArmorId;
            legacyEquippedAccessoryId = GetRepresentativeEquippedEquipmentId(EquipmentSlotType.Accessory) ?? legacyEquippedAccessoryId;
        }

        partial void SyncEquippedFlags()
        {
            foreach (OwnedEquipmentData equipment in OwnedEquipments.Where(x => x != null))
            {
                equipment.IsEquipped = !string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId);
            }
        }

        private void TryEquipLegacyRepresentative(EquipmentSlotType slotType, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return;
            OwnedMonsterData representative = ResolveRepresentativeEquipmentMonster();
            if (representative == null) return;
            OwnedEquipmentData equipment = OwnedEquipments.FirstOrDefault(x => x != null && x.EquipmentId == equipmentId && ResolveEquipmentSlotType(x.EquipmentId) == slotType) ?? CreateOwnedEquipmentInstance(equipmentId);
            if (equipment != null) EquipEquipmentToMonster(representative.InstanceId, equipment.InstanceId);
        }

        private OwnedEquipmentData CreateOwnedEquipmentInstance(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return null;
            EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipmentId);
            var equipment = new OwnedEquipmentData
            {
                InstanceId = equipmentId + "_" + Guid.NewGuid().ToString("N"),
                EquipmentId = equipmentId,
                UpgradeLevel = 0,
                EnhancementBonusRate = 0f,
                EnhancementAttackFlat = 0,
                EnhancementWisdomFlat = 0,
                EnhancementDefenseFlat = 0,
                EnhancementMagicDefenseFlat = 0,
                EnhancementHpFlat = 0,
                EnhancementAttackSpeedFlat = 0f,
                RemainingEnhanceAttempts = ResolveInitialEnhanceAttempts(equipmentId),
                IsEquipped = false,
                IsLocked = false,
                EquippedMonsterInstanceId = string.Empty
            };
            EquipmentEnhancementCatalog.EnsureRolledStats(equipmentData, equipment, EnhancementRandom);
            OwnedEquipments.Add(equipment);
            return equipment;
        }

        private int ResolveInitialEnhanceAttempts(string equipmentId)
        {
            EquipmentDataSO data = MasterDataManager.Instance?.GetEquipmentData(equipmentId);
            return EquipmentEnhancementCatalog.ResolveInitialEnhanceAttempts(data, equipmentId);
        }

        private EquipmentSlotType ResolveEquipmentSlotType(string equipmentId)
        {
            EquipmentDataSO data = MasterDataManager.Instance?.GetEquipmentData(equipmentId);
            if (data != null) return data.slotType;
            if (!string.IsNullOrEmpty(equipmentId) && (equipmentId.Contains("ring") || equipmentId.Contains("charm"))) return EquipmentSlotType.Accessory;
            if (!string.IsNullOrEmpty(equipmentId) && (equipmentId.Contains("mail") || equipmentId.Contains("cloth"))) return EquipmentSlotType.Armor;
            return EquipmentSlotType.Weapon;
        }

        private void SyncEquipmentOwnershipFromMonsterSlot(OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(GetMonsterEquipmentInstanceId(monster, slotType));
            if (equipment == null) { SetMonsterEquipmentInstanceId(monster, slotType, string.Empty); return; }
            equipment.EquippedMonsterInstanceId = monster.InstanceId;
        }

        private OwnedMonsterData ResolveRepresentativeEquipmentMonster()
        {
            foreach (string instanceId in PartyMonsterInstanceIds)
            {
                OwnedMonsterData monster = GetOwnedMonster(instanceId);
                if (monster != null) return monster;
            }

            return OwnedMonsters.FirstOrDefault(x => x != null);
        }

        private OwnedEquipmentData GetRepresentativeEquippedEquipment(EquipmentSlotType slotType)
        {
            OwnedMonsterData monster = ResolveRepresentativeEquipmentMonster();
            return monster == null ? null : GetMonsterEquippedEquipment(monster.InstanceId, slotType);
        }

        private string GetRepresentativeEquippedEquipmentId(EquipmentSlotType slotType)
        {
            return GetRepresentativeEquippedEquipment(slotType)?.EquipmentId;
        }

        private string GetMonsterEquipmentInstanceId(OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            if (monster == null) return string.Empty;
            return slotType switch
            {
                EquipmentSlotType.Weapon => monster.EquippedWeaponInstanceId,
                EquipmentSlotType.Armor => monster.EquippedArmorInstanceId,
                EquipmentSlotType.Accessory => monster.EquippedAccessoryInstanceId,
                _ => string.Empty
            };
        }

        private void SetMonsterEquipmentInstanceId(OwnedMonsterData monster, EquipmentSlotType slotType, string equipmentInstanceId)
        {
            if (monster == null) return;
            switch (slotType)
            {
                case EquipmentSlotType.Weapon: monster.EquippedWeaponInstanceId = equipmentInstanceId ?? string.Empty; break;
                case EquipmentSlotType.Armor: monster.EquippedArmorInstanceId = equipmentInstanceId ?? string.Empty; break;
                case EquipmentSlotType.Accessory: monster.EquippedAccessoryInstanceId = equipmentInstanceId ?? string.Empty; break;
            }
        }

        private void ClearMonsterEquipmentSlot(OwnedMonsterData monster, EquipmentSlotType slotType, string expectedInstanceId)
        {
            if (monster == null) return;
            string current = GetMonsterEquipmentInstanceId(monster, slotType);
            if (!string.IsNullOrEmpty(expectedInstanceId) && current != expectedInstanceId) return;
            SetMonsterEquipmentInstanceId(monster, slotType, string.Empty);
        }

        private void ConsumeEnhancementRelic(string relicId)
        {
            OwnedEnhancementRelicData relic = OwnedEnhancementRelics.FirstOrDefault(x => x != null && x.RelicId == relicId);
            if (relic != null) relic.Amount = Math.Max(0, relic.Amount - 1);
        }

        private void RemoveOwnedEquipment(string equipmentInstanceId, bool forceRemove)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (equipment == null || (equipment.IsLocked && !forceRemove)) return;
            if (!string.IsNullOrEmpty(equipment.EquippedMonsterInstanceId))
            {
                OwnedMonsterData monster = GetOwnedMonster(equipment.EquippedMonsterInstanceId);
                if (monster != null) ClearMonsterEquipmentSlot(monster, ResolveEquipmentSlotType(equipment.EquipmentId), equipment.InstanceId);
            }

            OwnedEquipments.Remove(equipment);
            SyncLegacyRepresentativeEquipmentIds();
            SyncEquippedFlags();
        }

        private EquipmentResolvedBonus ResolveEquipmentBonus(string equipmentInstanceId)
        {
            OwnedEquipmentData equipment = GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            EquipmentDataSO data = equipment != null ? MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId) : null;
            return EquipmentEnhancementCatalog.ResolveEquipmentBonus(data, equipment);
        }

        private static string GetEquipmentDisplayName(string equipmentId)
        {
            EquipmentDataSO data = MasterDataManager.Instance?.GetEquipmentData(equipmentId);
            return data != null ? data.equipmentName : equipmentId;
        }

        private static EquipmentEnhancementResult SetEnhanceResult(EquipmentEnhancementResult result, EquipmentEnhancementResultType type, string message)
        {
            result.ResultType = type;
            result.Message = message;
            return result;
        }
    }
}
