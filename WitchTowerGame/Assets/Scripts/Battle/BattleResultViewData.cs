namespace WitchTower.Battle
{
    public readonly struct BattleResultViewData
    {
        private static readonly BattleResultRewardVisual[] EmptyRewardVisuals = new BattleResultRewardVisual[0];

        public BattleResultViewData(bool isWin, int gold, int exp, int nextFloor, string recruitSummary)
            : this(
                isWin,
                gold,
                exp,
                exp,
                0,
                0,
                0,
                isWin ? nextFloor - 1 : nextFloor,
                nextFloor,
                string.Empty,
                recruitSummary,
                EmptyRewardVisuals)
        {
        }

        public BattleResultViewData(
            bool isWin,
            int gold,
            int exp,
            int partyMonsterExp,
            int partyMonsterCount,
            int playerLevelBefore,
            int playerLevelAfter,
            int clearedFloor,
            int nextFloor,
            string itemDropSummary,
            string monsterRecruitSummary)
            : this(
                isWin,
                gold,
                exp,
                partyMonsterExp,
                partyMonsterCount,
                playerLevelBefore,
                playerLevelAfter,
                clearedFloor,
                nextFloor,
                itemDropSummary,
                monsterRecruitSummary,
                EmptyRewardVisuals)
        {
        }

        public BattleResultViewData(
            bool isWin,
            int gold,
            int exp,
            int partyMonsterExp,
            int partyMonsterCount,
            int playerLevelBefore,
            int playerLevelAfter,
            int clearedFloor,
            int nextFloor,
            string itemDropSummary,
            string monsterRecruitSummary,
            BattleResultRewardVisual[] rewardVisuals)
        {
            IsWin = isWin;
            Gold = gold;
            Exp = exp;
            PartyMonsterExp = partyMonsterExp;
            PartyMonsterCount = partyMonsterCount;
            PlayerLevelBefore = playerLevelBefore;
            PlayerLevelAfter = playerLevelAfter;
            ClearedFloor = clearedFloor;
            NextFloor = nextFloor;
            ItemDropSummary = itemDropSummary ?? string.Empty;
            MonsterRecruitSummary = monsterRecruitSummary ?? string.Empty;
            RewardVisuals = rewardVisuals ?? EmptyRewardVisuals;
        }

        public bool IsWin { get; }
        public int Gold { get; }
        public int Exp { get; }
        public int PartyMonsterExp { get; }
        public int PartyMonsterCount { get; }
        public int PlayerLevelBefore { get; }
        public int PlayerLevelAfter { get; }
        public int ClearedFloor { get; }
        public int NextFloor { get; }
        public string ItemDropSummary { get; }
        public string MonsterRecruitSummary { get; }
        public BattleResultRewardVisual[] RewardVisuals { get; }
        public string RecruitSummary => MonsterRecruitSummary;
    }
}
