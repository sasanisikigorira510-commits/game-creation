using System.Collections.Generic;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;

namespace WitchTower.Battle
{
    public static class MonsterRecruitService
    {
        private const float DefaultRecruitChance = 0.35f;

        public static bool CanAttemptRecruitThisBattle(PlayerProfile profile)
        {
            return profile != null && profile.HasMonsterStorageSpace();
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

            FloorDataSO floorData = MasterDataManager.Instance?.GetFloorData(floor);
            if (floorData == null)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, "この階には仲間化候補データがありません。");
            }

            List<MonsterDataSO> recruitableMonsters = CollectRecruitableMonsters(floorData);
            if (recruitableMonsters.Count == 0)
            {
                return new MonsterRecruitResult(true, false, false, string.Empty, string.Empty, "この階には仲間化候補モンスターがいません。");
            }

            float recruitChance = floorData.monsterRecruitChance > 0f ? floorData.monsterRecruitChance : DefaultRecruitChance;
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
            profile.AddOwnedMonster(recruitedMonster.monsterId, recruitLevel);

            return new MonsterRecruitResult(
                wasEligible: true,
                attempted: true,
                succeeded: true,
                monsterId: recruitedMonster.monsterId,
                monsterName: recruitedMonster.monsterName,
                summary: $"{recruitedMonster.monsterName} が仲間になりました。");
        }

        private static List<MonsterDataSO> CollectRecruitableMonsters(FloorDataSO floorData)
        {
            var results = new List<MonsterDataSO>();
            if (floorData == null || floorData.recruitableMonsterIds == null)
            {
                return results;
            }

            foreach (string monsterId in floorData.recruitableMonsterIds)
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

        private static int CalculateRecruitLevel(int floor, MonsterDataSO monsterData)
        {
            int rarityBonus = monsterData != null ? (int)monsterData.rarity - 1 : 0;
            return Mathf.Clamp(floor + rarityBonus, 1, 99);
        }
    }
}
