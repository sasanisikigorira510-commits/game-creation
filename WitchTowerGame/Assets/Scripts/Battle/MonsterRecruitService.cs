using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Battle
{
    public static class MonsterRecruitService
    {
        private const float DefaultRecruitChance = 0.05f;
        private const int DungeonBossRecruitIndividualValueMinimum = MonsterIndividualValueService.DefaultValue;
        private const string StorageFullSummary = "これ以上捕獲できません";

        public static bool CanAttemptRecruitThisBattle(PlayerProfile profile)
        {
            return GetRecruitBlockReason(profile) == MonsterRecruitBlockReason.None;
        }

        public static MonsterRecruitBlockReason GetRecruitBlockReason(PlayerProfile profile)
        {
            if (profile == null)
            {
                return MonsterRecruitBlockReason.ProfileUnavailable;
            }

            // The opening lesson has no recruitment. Preserve this reason
            // separately from capacity, including when the result advances
            // the tutorial before its recruitment summary is displayed.
            if (!profile.HasCompletedTutorial &&
                (profile.TutorialStepId == StoryTutorialService.StepFirstBattle ||
                 profile.TutorialStepId == StoryTutorialService.StepFirstResult))
            {
                return MonsterRecruitBlockReason.Tutorial;
            }

            return profile.HasMonsterStorageSpace() || profile.CanAutoReleaseNewMonsters()
                ? MonsterRecruitBlockReason.None
                : MonsterRecruitBlockReason.StorageFull;
        }

        public static string GetBlockedRecruitmentSummary(MonsterRecruitBlockReason reason)
        {
            switch (reason)
            {
                case MonsterRecruitBlockReason.Tutorial:
                    return "チュートリアルの初回バトルでは捕獲は行いません。";
                case MonsterRecruitBlockReason.StorageFull:
                    return StorageFullSummary;
                case MonsterRecruitBlockReason.ProfileUnavailable:
                    return "プレイヤーデータを読み込めないため、捕獲できません。";
                default:
                    return string.Empty;
            }
        }

        private static MonsterRecruitResult BuildBlockedResult(MonsterRecruitBlockReason reason)
        {
            return new MonsterRecruitResult(false, false, false, string.Empty, string.Empty,
                GetBlockedRecruitmentSummary(reason), blockReason: reason);
        }

        public static bool HasRecruitableMonsterCandidates(int floor)
        {
            string[] monsterIds = BattleDungeonCatalog.ResolveRecruitableMonsterIds(floor);
            return monsterIds != null && monsterIds.Length > 0;
        }

        public static MonsterRecruitResult ResolveAfterEnemyDefeat(
            int floor,
            PlayerProfile profile,
            MonsterRecruitBlockReason blockReasonAtBattleStart,
            EnemyDataSO defeatedEnemyData,
            bool defeatedEnemyIsDungeonBoss)
        {
            if (profile == null)
            {
                return BuildBlockedResult(MonsterRecruitBlockReason.ProfileUnavailable);
            }

            if (blockReasonAtBattleStart != MonsterRecruitBlockReason.None)
            {
                return BuildBlockedResult(blockReasonAtBattleStart);
            }

            MonsterDataSO defeatedMonster = ResolveRecruitableDefeatedMonster(floor, defeatedEnemyData);
            if (defeatedMonster == null)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, string.Empty);
            }

            if (!profile.HasMonsterStorageSpace() && !profile.CanAutoReleaseNewMonsters())
            {
                return BuildBlockedResult(MonsterRecruitBlockReason.StorageFull);
            }

            float recruitChance = BattleDungeonCatalog.ResolvePerDefeatRecruitChance(floor, defeatedMonster);
            if (recruitChance <= 0f)
            {
                recruitChance = DefaultRecruitChance;
            }

            bool recruited = Random.value <= Mathf.Clamp01(recruitChance);
            if (!recruited)
            {
                return new MonsterRecruitResult(true, true, false, string.Empty, string.Empty, "仲間化抽選は発生しましたが、今回は仲間になりませんでした。");
            }

            return GrantRecruitedMonster(profile, defeatedMonster, defeatedEnemyIsDungeonBoss);
        }

        public static MonsterRecruitResult ResolveAfterBattleWin(int floor, PlayerProfile profile, MonsterRecruitBlockReason blockReasonAtBattleStart)
        {
            if (profile == null)
            {
                return BuildBlockedResult(MonsterRecruitBlockReason.ProfileUnavailable);
            }

            if (blockReasonAtBattleStart != MonsterRecruitBlockReason.None)
            {
                return BuildBlockedResult(blockReasonAtBattleStart);
            }

            List<MonsterDataSO> recruitableMonsters = CollectRecruitableMonsters(floor);
            if (recruitableMonsters.Count == 0)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, "この階には仲間化候補モンスターがいません。");
            }

            if (!profile.HasMonsterStorageSpace() && !profile.CanAutoReleaseNewMonsters())
            {
                return BuildBlockedResult(MonsterRecruitBlockReason.StorageFull);
            }

            float recruitChance = BattleDungeonCatalog.ResolveRecruitChance(floor);
            if (recruitChance <= 0f)
            {
                recruitChance = DefaultRecruitChance;
            }

            bool recruited = Random.value <= Mathf.Clamp01(recruitChance);
            if (!recruited)
            {
                return new MonsterRecruitResult(true, true, false, string.Empty, string.Empty, "仲間化抽選は発生しましたが、今回は仲間になりませんでした。");
            }

            MonsterDataSO recruitedMonster = recruitableMonsters[Random.Range(0, recruitableMonsters.Count)];
            if (recruitedMonster == null)
            {
                return new MonsterRecruitResult(true, true, false, string.Empty, string.Empty, "仲間化候補の読み込みに失敗しました。");
            }

            bool recruitedMonsterIsDungeonBoss = BattleDungeonCatalog.IsBossMonsterOnFloor(floor, recruitedMonster.monsterId);
            return GrantRecruitedMonster(profile, recruitedMonster, recruitedMonsterIsDungeonBoss);
        }

        private static List<MonsterDataSO> CollectRecruitableMonsters(int floor)
        {
            var results = new List<MonsterDataSO>();
            string[] monsterIds = BattleDungeonCatalog.ResolveRecruitableMonsterIds(floor);
            if (monsterIds == null || monsterIds.Length == 0)
            {
                return results;
            }

            foreach (string monsterId in monsterIds)
            {
                if (string.IsNullOrEmpty(monsterId))
                {
                    continue;
                }

                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monsterId);
                if (IsRegisteredCurrentMonster(monsterData))
                {
                    results.Add(monsterData);
                }
            }

            return results;
        }

        private static MonsterDataSO ResolveRecruitableDefeatedMonster(int floor, EnemyDataSO defeatedEnemyData)
        {
            if (defeatedEnemyData == null || !defeatedEnemyData.canBeRecruited || GarzaBossPresentation.IsGarza(defeatedEnemyData))
            {
                return null;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(defeatedEnemyData.enemyId);
            if (string.IsNullOrEmpty(monsterId) || !BattleDungeonCatalog.IsRecruitableMonsterOnFloor(floor, monsterId))
            {
                return null;
            }

            MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(monsterId);
            return IsRegisteredCurrentMonster(monsterData) ? monsterData : null;
        }

        private static MonsterRecruitResult GrantRecruitedMonster(PlayerProfile profile, MonsterDataSO monsterData, bool isDungeonBossMonster)
        {
            if (profile == null || !IsRegisteredCurrentMonster(monsterData))
            {
                return MonsterRecruitResult.Empty;
            }

            bool hadStorageSpace = profile.HasMonsterStorageSpace();
            if (!hadStorageSpace && !profile.CanAutoReleaseNewMonsters())
            {
                return BuildStorageFullResult(monsterData);
            }

            // Every capture starts at Lv1, including late-floor and boss recruits.
            OwnedMonsterData createdMonster = profile.AddOwnedMonster(monsterData.monsterId, 1);
            if (createdMonster == null)
            {
                return BuildStorageFullResult(monsterData);
            }

            if (isDungeonBossMonster)
            {
                MonsterIndividualValueService.Apply(
                    createdMonster,
                    MonsterIndividualValueService.RollWithMinimum(DungeonBossRecruitIndividualValueMinimum));
            }

            int individualAverage = MonsterIndividualValueService.GetAverage(createdMonster);
            string individualSummary = MonsterIndividualValueService.BuildSummary(createdMonster);
            if (profile.ShouldAutoReleaseMonster(createdMonster))
            {
                profile.TryReleaseMonster(createdMonster.InstanceId, true, out _);
                int threshold = profile.AutoReleaseMonsterIndividualValueThreshold;
                return new MonsterRecruitResult(
                    wasEligible: true,
                    attempted: true,
                    succeeded: false,
                    monsterId: monsterData.monsterId,
                    monsterName: monsterData.monsterName,
                    summary: $"{monsterData.monsterName} はIV{individualAverage}のため自動で逃しました。",
                    individualAverage: individualAverage,
                    individualSummary: individualSummary,
                    autoReleased: true,
                    autoReleaseThreshold: threshold);
            }

            if (!hadStorageSpace)
            {
                profile.TryReleaseMonster(createdMonster.InstanceId, true, out _);
                return BuildStorageFullResult(monsterData, individualAverage);
            }

            return new MonsterRecruitResult(
                wasEligible: true,
                attempted: true,
                succeeded: true,
                monsterId: monsterData.monsterId,
                monsterName: monsterData.monsterName,
                summary: $"{monsterData.monsterName} が仲間になりました。",
                individualAverage: individualAverage,
                individualSummary: individualSummary);
        }

        private static bool IsRegisteredCurrentMonster(MonsterDataSO monsterData)
        {
            if (monsterData == null || string.IsNullOrEmpty(monsterData.monsterId))
            {
                return false;
            }

            MonsterDataSO registeredMonster = MasterDataManager.Instance?.GetMonsterData(monsterData.monsterId);
            return registeredMonster != null && registeredMonster == monsterData;
        }

        private static MonsterRecruitResult BuildStorageFullResult(MonsterDataSO monsterData, int individualAverage = -1)
        {
            return new MonsterRecruitResult(
                wasEligible: false,
                attempted: true,
                succeeded: false,
                monsterId: monsterData != null ? monsterData.monsterId : string.Empty,
                monsterName: monsterData != null ? monsterData.monsterName : string.Empty,
                summary: StorageFullSummary,
                individualAverage: individualAverage,
                blockReason: MonsterRecruitBlockReason.StorageFull);
        }
    }
}
