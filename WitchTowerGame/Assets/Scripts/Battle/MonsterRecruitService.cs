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
        private const string StorageFullSummary = "これ以上捕獲できません";

        public static bool CanAttemptRecruitThisBattle(PlayerProfile profile)
        {
            return profile != null && (profile.HasMonsterStorageSpace() || profile.CanAutoReleaseNewMonsters());
        }

        public static bool HasRecruitableMonsterCandidates(int floor)
        {
            string[] monsterIds = BattleDungeonCatalog.ResolveRecruitableMonsterIds(floor);
            return monsterIds != null && monsterIds.Length > 0;
        }

        public static MonsterRecruitResult ResolveAfterEnemyDefeat(
            int floor,
            PlayerProfile profile,
            bool recruitWasEnabledAtBattleStart,
            EnemyDataSO defeatedEnemyData)
        {
            if (profile == null)
            {
                return MonsterRecruitResult.Empty;
            }

            if (!recruitWasEnabledAtBattleStart)
            {
                return new MonsterRecruitResult(
                    wasEligible: false,
                    attempted: false,
                    succeeded: false,
                    monsterId: string.Empty,
                    monsterName: string.Empty,
                    summary: "保有上限に達していたため、このバトルでは仲間化抽選は発生しません。");
            }

            MonsterDataSO defeatedMonster = ResolveRecruitableDefeatedMonster(floor, defeatedEnemyData);
            if (defeatedMonster == null)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, string.Empty);
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

            int recruitLevel = CalculateRecruitLevel(floor, defeatedMonster);
            return GrantRecruitedMonster(profile, defeatedMonster, recruitLevel);
        }

        public static MonsterRecruitResult ResolveAfterBattleWin(int floor, PlayerProfile profile, bool recruitWasEnabledAtBattleStart)
        {
            if (profile == null)
            {
                return MonsterRecruitResult.Empty;
            }

            if (!recruitWasEnabledAtBattleStart)
            {
                return new MonsterRecruitResult(
                    wasEligible: false,
                    attempted: false,
                    succeeded: false,
                    monsterId: string.Empty,
                    monsterName: string.Empty,
                    summary: "保有上限に達していたため、このバトルでは仲間化抽選は発生しません。");
            }

            List<MonsterDataSO> recruitableMonsters = CollectRecruitableMonsters(floor);
            if (recruitableMonsters.Count == 0)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, "この階には仲間化候補モンスターがいません。");
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

            int recruitLevel = CalculateRecruitLevel(floor, recruitedMonster);
            return GrantRecruitedMonster(profile, recruitedMonster, recruitLevel);
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
                if (monsterData != null)
                {
                    results.Add(monsterData);
                }
            }

            return results;
        }

        private static MonsterDataSO ResolveRecruitableDefeatedMonster(int floor, EnemyDataSO defeatedEnemyData)
        {
            if (defeatedEnemyData == null)
            {
                return null;
            }

            string monsterId = BattleDungeonCatalog.ResolveMonsterIdFromEnemyId(defeatedEnemyData.enemyId);
            if (string.IsNullOrEmpty(monsterId) || !BattleDungeonCatalog.IsRecruitableMonsterOnFloor(floor, monsterId))
            {
                return null;
            }

            return MasterDataManager.Instance?.GetMonsterData(monsterId);
        }

        private static int CalculateRecruitLevel(int floor, MonsterDataSO monsterData)
        {
            int rarityBonus = monsterData != null ? (int)monsterData.rarity - 1 : 0;
            return MonsterLevelService.ClampLevelToMax(floor + rarityBonus, monsterData);
        }

        private static MonsterRecruitResult GrantRecruitedMonster(PlayerProfile profile, MonsterDataSO monsterData, int level)
        {
            if (profile == null || monsterData == null)
            {
                return MonsterRecruitResult.Empty;
            }

            bool hadStorageSpace = profile.HasMonsterStorageSpace();
            if (!hadStorageSpace && !profile.CanAutoReleaseNewMonsters())
            {
                return BuildStorageFullResult(monsterData);
            }

            OwnedMonsterData createdMonster = profile.AddOwnedMonster(monsterData.monsterId, level);
            if (createdMonster == null)
            {
                return BuildStorageFullResult(monsterData);
            }

            int individualAverage = MonsterIndividualValueService.GetAverage(createdMonster);
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
                individualAverage: individualAverage);
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
                individualAverage: individualAverage);
        }
    }
}
