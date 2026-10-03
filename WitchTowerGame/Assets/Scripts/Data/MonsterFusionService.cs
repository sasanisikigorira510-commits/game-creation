using System;
using UnityEngine;
using WitchTower.Battle;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    public enum MonsterFusionStatus
    {
        Success = 0,
        InvalidProfile = 1,
        InvalidParent = 2,
        SameMonsterInstance = 3,
        ParentNotOwned = 4,
        FavoriteParentBlocked = 5,
        NoRecipe = 6,
        ResultMonsterDataMissing = 7,
        ParentLevelTooLow = 8,
        LockedParentBlocked = 9,
        PendingTrainingSyncRequired = 10,
        DailyChallengePartyLocked = 11
    }

    public sealed class MonsterFusionResult
    {
        public MonsterFusionResult(
            MonsterFusionStatus status,
            MonsterFusionRecipeDefinition recipe = null,
            MonsterDataSO resultMonsterData = null,
            OwnedMonsterData createdMonster = null,
            string message = null)
        {
            Status = status;
            Recipe = recipe;
            ResultMonsterData = resultMonsterData;
            CreatedMonster = createdMonster;
            Message = message ?? string.Empty;
        }

        public MonsterFusionStatus Status { get; }
        public MonsterFusionRecipeDefinition Recipe { get; }
        public MonsterDataSO ResultMonsterData { get; }
        public OwnedMonsterData CreatedMonster { get; }
        public string Message { get; }
        public bool CanFuse => Status == MonsterFusionStatus.Success;
    }

    public static class MonsterFusionService
    {
        public const int MaxPendingTrainingFusionReceipts = 512;
        public static MonsterFusionResult PreviewFusion(
            PlayerProfile profile,
            string parentInstanceIdA,
            string parentInstanceIdB,
            MasterDataManager masterDataManager = null)
        {
            if (profile == null)
            {
                return new MonsterFusionResult(MonsterFusionStatus.InvalidProfile, message: "プレイヤーデータがありません。");
            }

            if (string.IsNullOrEmpty(parentInstanceIdA) || string.IsNullOrEmpty(parentInstanceIdB))
            {
                return new MonsterFusionResult(MonsterFusionStatus.InvalidParent, message: "親モンスターを2体選んでください。");
            }

            if (parentInstanceIdA == parentInstanceIdB)
            {
                return new MonsterFusionResult(MonsterFusionStatus.SameMonsterInstance, message: "同じ個体は2体分の親として選べません。");
            }

            OwnedMonsterData parentA = profile.GetOwnedMonster(parentInstanceIdA);
            OwnedMonsterData parentB = profile.GetOwnedMonster(parentInstanceIdB);
            if (parentA == null || parentB == null)
            {
                return new MonsterFusionResult(MonsterFusionStatus.ParentNotOwned, message: "選択した親モンスターが所持一覧にありません。");
            }

            if (profile.IsDailyChallengePartyMonster(parentA.InstanceId) || profile.IsDailyChallengePartyMonster(parentB.InstanceId))
            {
                return new MonsterFusionResult(MonsterFusionStatus.DailyChallengePartyLocked,
                    message: "挑戦中のデイリー試練の参加モンスターは配合できません。試練を終了してから操作してください。");
            }

            if (profile.TrainingFusionReceipts.Count >= MaxPendingTrainingFusionReceipts)
            {
                return new MonsterFusionResult(MonsterFusionStatus.PendingTrainingSyncRequired,
                    message: "配合の未同期記録が上限に達しました。オンラインでデータを同期してから配合してください。");
            }

            masterDataManager ??= MasterDataManager.Instance;
            masterDataManager?.Initialize();

            MonsterDataSO parentMonsterDataA = masterDataManager != null
                ? masterDataManager.GetMonsterData(parentA.MonsterId)
                : null;
            MonsterDataSO parentMonsterDataB = masterDataManager != null
                ? masterDataManager.GetMonsterData(parentB.MonsterId)
                : null;

            if (!MonsterLevelService.IsAtMaxLevel(parentA, parentMonsterDataA) ||
                !MonsterLevelService.IsAtMaxLevel(parentB, parentMonsterDataB))
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.ParentLevelTooLow,
                    message: BuildMaxLevelRequirementMessage(parentA, parentMonsterDataA, parentB, parentMonsterDataB));
            }

            if (!TryResolveFusionResult(
                masterDataManager,
                parentA.MonsterId,
                parentB.MonsterId,
                parentMonsterDataA,
                parentMonsterDataB,
                out MonsterFusionRecipeDefinition recipe,
                out _))
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.NoRecipe,
                    message: "配合結果のモンスターデータが見つかりません。");
            }

            return new MonsterFusionResult(
                MonsterFusionStatus.Success,
                recipe,
                message: "配合できます。誕生するモンスターは儀式後に判明します。");
        }

        public static MonsterFusionResult Fuse(
            PlayerProfile profile,
            string parentInstanceIdA,
            string parentInstanceIdB,
            MasterDataManager masterDataManager = null,
            bool allowFavoriteParents = false)
        {
            MonsterFusionResult preview = PreviewFusion(profile, parentInstanceIdA, parentInstanceIdB, masterDataManager);
            if (!preview.CanFuse)
            {
                return preview;
            }

            OwnedMonsterData parentA = profile.GetOwnedMonster(parentInstanceIdA);
            OwnedMonsterData parentB = profile.GetOwnedMonster(parentInstanceIdB);
            if (!allowFavoriteParents && (parentA.IsFavorite || parentB.IsFavorite))
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.FavoriteParentBlocked,
                    preview.Recipe,
                    message: "お気に入り登録中の親がいるため、配合を止めました。");
            }

            if (parentA.IsLocked || parentB.IsLocked)
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.LockedParentBlocked,
                    preview.Recipe,
                    message: "ロック中の親がいるため、配合を止めました。");
            }

            masterDataManager ??= MasterDataManager.Instance;
            masterDataManager?.Initialize();
            MonsterDataSO parentMonsterDataA = masterDataManager != null
                ? masterDataManager.GetMonsterData(parentA.MonsterId)
                : null;
            MonsterDataSO parentMonsterDataB = masterDataManager != null
                ? masterDataManager.GetMonsterData(parentB.MonsterId)
                : null;
            if (!TryResolveFusionResult(
                masterDataManager,
                parentA.MonsterId,
                parentB.MonsterId,
                parentMonsterDataA,
                parentMonsterDataB,
                out MonsterFusionRecipeDefinition recipe,
                out MonsterDataSO resultMonsterData))
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.ResultMonsterDataMissing,
                    preview.Recipe,
                    message: "配合結果データが見つかりません。");
            }

            FusionInheritedStats inheritedStats = CalculateInheritedStats(profile, parentA, parentB, masterDataManager);
            int inheritedPlusValue = CalculateInheritedPlusValue(parentA, parentB);
            MonsterIndividualValues inheritedIndividualValues = MonsterIndividualValueService.Inherit(parentA, parentB);
            string trainingParentInstanceIdA = parentA.InstanceId;
            string trainingParentInstanceIdB = parentB.InstanceId;
            RemoveParent(profile, parentA);
            RemoveParent(profile, parentB);

            OwnedMonsterData createdMonster = profile.AddOwnedMonster(resultMonsterData.monsterId, 1);
            ApplyInheritedPlusValue(createdMonster, inheritedPlusValue);
            ApplyInheritedStats(createdMonster, inheritedStats);
            MonsterIndividualValueService.Apply(createdMonster, inheritedIndividualValues);
            MonsterTrainingService.Inherit(parentA, parentB, createdMonster);
            createdMonster.TrainingParentInstanceIdA = trainingParentInstanceIdA;
            createdMonster.TrainingParentInstanceIdB = trainingParentInstanceIdB;
            profile.TrainingFusionReceipts.Add(new TrainingFusionReceipt
            {
                ChildInstanceId = createdMonster.InstanceId, ChildMonsterId = createdMonster.MonsterId,
                ParentInstanceIdA = trainingParentInstanceIdA, ParentInstanceIdB = trainingParentInstanceIdB,
                ParentMonsterIdA = parentA.MonsterId, ParentMonsterIdB = parentB.MonsterId
            });
            return new MonsterFusionResult(
                MonsterFusionStatus.Success,
                recipe,
                resultMonsterData,
                createdMonster,
                $"{resultMonsterData.monsterName} が誕生しました。");
        }

        private static void RemoveParent(PlayerProfile profile, OwnedMonsterData parent)
        {
            if (profile == null || parent == null)
            {
                return;
            }

            profile.OwnedMonsters.Remove(parent);
            if (profile.PartyMonsterInstanceIds != null)
            {
                for (int i = 0; i < profile.PartyMonsterInstanceIds.Count; i += 1)
                {
                    if (string.Equals(profile.PartyMonsterInstanceIds[i], parent.InstanceId, StringComparison.Ordinal))
                    {
                        profile.PartyMonsterInstanceIds[i] = string.Empty;
                    }
                }
            }

            ClearEquipmentReference(profile, parent.EquippedWeaponInstanceId);
            ClearEquipmentReference(profile, parent.EquippedArmorInstanceId);
            ClearEquipmentReference(profile, parent.EquippedAccessoryInstanceId);
        }

        private static bool TryResolveCatalogRecipe(
            MasterDataManager masterDataManager,
            string parentMonsterIdA,
            string parentMonsterIdB,
            bool includeSpecial,
            out MonsterFusionRecipeDefinition recipe,
            out MonsterDataSO resultMonsterData)
        {
            recipe = null;
            resultMonsterData = null;
            if (masterDataManager == null)
            {
                return false;
            }

            if (!MonsterFusionCatalog.TryResolveRecipe(parentMonsterIdA, parentMonsterIdB, out recipe, includeSpecial))
            {
                return false;
            }

            resultMonsterData = masterDataManager.GetMonsterData(recipe.ResultMonsterId);
            return resultMonsterData != null;
        }

        private static bool TryResolveFusionResult(
            MasterDataManager masterDataManager,
            string parentMonsterIdA,
            string parentMonsterIdB,
            MonsterDataSO parentMonsterDataA,
            MonsterDataSO parentMonsterDataB,
            out MonsterFusionRecipeDefinition recipe,
            out MonsterDataSO resultMonsterData)
        {
            if (TryResolveCatalogRecipe(masterDataManager, parentMonsterIdA, parentMonsterIdB, true, out recipe, out resultMonsterData))
            {
                return true;
            }

            return MonsterFusionCatalog.TryResolveNormalRecipe(
                parentMonsterDataA,
                parentMonsterDataB,
                masterDataManager != null ? masterDataManager.GetAllMonsterData() : null,
                out recipe,
                out resultMonsterData);
        }

        private static string BuildMaxLevelRequirementMessage(
            OwnedMonsterData parentA,
            MonsterDataSO parentMonsterDataA,
            OwnedMonsterData parentB,
            MonsterDataSO parentMonsterDataB)
        {
            return "配合には親2体とも最大レベルが必要です。 " +
                BuildLevelProgressText("親1", parentA, parentMonsterDataA) +
                " / " +
                BuildLevelProgressText("親2", parentB, parentMonsterDataB);
        }

        private static string BuildLevelProgressText(string label, OwnedMonsterData parent, MonsterDataSO monsterData)
        {
            int level = MonsterLevelService.ClampLevelToMax(parent != null ? parent.Level : 1, monsterData);
            int maxLevel = MonsterLevelService.GetMaxLevel(monsterData);
            return $"{label} Lv.{level}/{maxLevel}";
        }

        private static FusionInheritedStats CalculateInheritedStats(
            PlayerProfile profile,
            OwnedMonsterData parentA,
            OwnedMonsterData parentB,
            MasterDataManager masterDataManager)
        {
            BattleUnitStats statsA = CreateParentStats(parentA, masterDataManager);
            BattleUnitStats statsB = CreateParentStats(parentB, masterDataManager);
            return new FusionInheritedStats
            {
                Hp = ResolveInheritedInt(statsA?.MaxHp ?? 0, statsB?.MaxHp ?? 0),
                Attack = ResolveInheritedInt(statsA?.Attack ?? 0, statsB?.Attack ?? 0),
                Wisdom = ResolveInheritedInt(statsA?.Wisdom ?? 0, statsB?.Wisdom ?? 0),
                Defense = ResolveInheritedInt(statsA?.Defense ?? 0, statsB?.Defense ?? 0),
                MagicDefense = ResolveInheritedInt(statsA?.MagicDefense ?? 0, statsB?.MagicDefense ?? 0),
                AttackSpeed = ResolveInheritedFloat(statsA?.AttackSpeed ?? 0f, statsB?.AttackSpeed ?? 0f)
            };
        }

        private static BattleUnitStats CreateParentStats(OwnedMonsterData parent, MasterDataManager masterDataManager)
        {
            MonsterDataSO monsterData = parent != null && masterDataManager != null
                ? masterDataManager.GetMonsterData(parent.MonsterId)
                : null;
            return MonsterBattleStatsFactory.Create(null, parent, monsterData);
        }

        private static int ResolveInheritedInt(int firstValue, int secondValue)
        {
            int total = Math.Max(0, firstValue) + Math.Max(0, secondValue);
            return total <= 0 ? 0 : Math.Max(1, Mathf.RoundToInt(total * 0.05f));
        }

        private static float ResolveInheritedFloat(float firstValue, float secondValue)
        {
            float total = Mathf.Max(0f, firstValue) + Mathf.Max(0f, secondValue);
            return total <= 0f ? 0f : total * 0.05f;
        }

        private static int CalculateInheritedPlusValue(OwnedMonsterData parentA, OwnedMonsterData parentB)
        {
            int plusA = parentA != null ? parentA.TotalPlusValue : 0;
            int plusB = parentB != null ? parentB.TotalPlusValue : 0;
            return Math.Max(0, plusA) + Math.Max(0, plusB);
        }

        private static void ApplyInheritedPlusValue(OwnedMonsterData createdMonster, int inheritedPlusValue)
        {
            if (createdMonster == null)
            {
                return;
            }

            int plusValue = Math.Max(0, inheritedPlusValue);
            createdMonster.PlusValue = plusValue;
            createdMonster.PlusHp = plusValue;
            createdMonster.PlusAttack = plusValue;
            createdMonster.PlusWisdom = plusValue;
            createdMonster.PlusDefense = plusValue;
            createdMonster.PlusMagicDefense = plusValue;
        }

        private static void ApplyInheritedStats(OwnedMonsterData createdMonster, FusionInheritedStats inheritedStats)
        {
            if (createdMonster == null)
            {
                return;
            }

            createdMonster.FusionBonusHp = Math.Max(0, inheritedStats.Hp);
            createdMonster.FusionBonusAttack = Math.Max(0, inheritedStats.Attack);
            createdMonster.FusionBonusWisdom = Math.Max(0, inheritedStats.Wisdom);
            createdMonster.FusionBonusDefense = Math.Max(0, inheritedStats.Defense);
            createdMonster.FusionBonusMagicDefense = Math.Max(0, inheritedStats.MagicDefense);
            createdMonster.FusionBonusAttackSpeed = Mathf.Max(0f, inheritedStats.AttackSpeed);
        }

        private static void ClearEquipmentReference(PlayerProfile profile, string equipmentInstanceId)
        {
            if (profile == null || string.IsNullOrEmpty(equipmentInstanceId))
            {
                return;
            }

            OwnedEquipmentData equipment = profile.GetOwnedEquipmentByInstanceId(equipmentInstanceId);
            if (equipment != null)
            {
                equipment.EquippedMonsterInstanceId = string.Empty;
                equipment.IsEquipped = false;
            }
        }

        private struct FusionInheritedStats
        {
            public int Hp;
            public int Attack;
            public int Wisdom;
            public int Defense;
            public int MagicDefense;
            public float AttackSpeed;
        }
    }
}
