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
        ParentLevelTooLow = 8
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

            if (!TryResolveCatalogRecipe(masterDataManager, parentA.MonsterId, parentB.MonsterId, true, out MonsterFusionRecipeDefinition recipe, out MonsterDataSO resultMonsterData) &&
                !MonsterFusionCatalog.TryResolveNormalRecipe(
                    parentMonsterDataA,
                    parentMonsterDataB,
                    masterDataManager != null ? masterDataManager.GetAllMonsterData() : null,
                    out recipe,
                    out resultMonsterData))
            {
                return new MonsterFusionResult(
                    MonsterFusionStatus.NoRecipe,
                    message: "通常配合はクラス1〜3同士なら異種族・クラス違いでも可能です。クラス4以降は特殊配合の組み合わせが必要です。");
            }

            return new MonsterFusionResult(
                MonsterFusionStatus.Success,
                recipe,
                resultMonsterData,
                message: $"{resultMonsterData.monsterName} を生成できます。");
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
                    preview.ResultMonsterData,
                    message: "お気に入り登録中の親がいるため、配合を止めました。");
            }

            int resultLevel = MonsterLevelService.ClampLevelToMax(Math.Max(parentA.Level, parentB.Level), preview.ResultMonsterData);
            FusionInheritedStats inheritedStats = CalculateInheritedStats(profile, parentA, parentB, masterDataManager);
            MonsterIndividualValues inheritedIndividualValues = MonsterIndividualValueService.Inherit(parentA, parentB);
            RemoveParent(profile, parentA);
            RemoveParent(profile, parentB);

            OwnedMonsterData createdMonster = profile.AddOwnedMonster(preview.ResultMonsterData.monsterId, resultLevel);
            ApplyInheritedStats(createdMonster, inheritedStats);
            MonsterIndividualValueService.Apply(createdMonster, inheritedIndividualValues);
            return new MonsterFusionResult(
                MonsterFusionStatus.Success,
                preview.Recipe,
                preview.ResultMonsterData,
                createdMonster,
                $"{preview.ResultMonsterData.monsterName} が誕生しました。");
        }

        private static void RemoveParent(PlayerProfile profile, OwnedMonsterData parent)
        {
            if (profile == null || parent == null)
            {
                return;
            }

            profile.OwnedMonsters.Remove(parent);
            profile.PartyMonsterInstanceIds.Remove(parent.InstanceId);
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

        private static string BuildMaxLevelRequirementMessage(
            OwnedMonsterData parentA,
            MonsterDataSO parentMonsterDataA,
            OwnedMonsterData parentB,
            MonsterDataSO parentMonsterDataB)
        {
            return "配合には親2体とも最大Lvが必要です。 " +
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
