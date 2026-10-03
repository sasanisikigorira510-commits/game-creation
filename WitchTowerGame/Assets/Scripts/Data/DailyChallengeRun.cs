using System;

namespace WitchTower.Data
{
    // Wire/save models. Names deliberately match the authoritative server contract.
    [Serializable]
    public sealed class DailyChallengeState
    {
        public string Day;
        public string NextResetUtc;
        public DailyChallengeModeState[] Modes = Array.Empty<DailyChallengeModeState>();
        public DailyChallengeRun ActiveRun;
    }

    [Serializable]
    public sealed class DailyChallengeModeState
    {
        public string Mode;
        public int UsedEntries;
        public int BestStage;
        public int BestStageToday;
        public int RemainingEntries = -1;
        public string NextResetUtc;
    }

    [Serializable]
    public sealed class DailyChallengeRun
    {
        public string RunId;
        public string Mode;
        public string StartDay;
        public int ClearedStage;
        public string[] PartyInstanceIds = Array.Empty<string>();
        public DailyChallengeCheckpoint Checkpoint;
        public string Status;
        public int NextStage => Math.Min(11, Math.Max(1, ClearedStage + 1));
        public bool IsActive => string.Equals(Status, "active", StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class DailyChallengeCheckpoint
    {
        public int Stage = 1;
        public bool StageComplete;
        public int DefeatedEnemies;
        public int SpawnedEnemies;
        public int DefeatedEnemyMaxHp;
        public float EnemySpawnTimer;
        public bool OpeningBurstSpawned;
        public int NextAllyRuntimeId = 1;
        public int NextEnemyRuntimeId = 1;
        public float GuardRemaining;
        public DailyChallengePartySnapshot[] Parties = Array.Empty<DailyChallengePartySnapshot>();
        public DailyChallengeEnemySnapshot[] Enemies = Array.Empty<DailyChallengeEnemySnapshot>();
        public DailyChallengePlayerSkillSnapshot[] PlayerSkills = Array.Empty<DailyChallengePlayerSkillSnapshot>();
        public DailyChallengeGuardianSnapshot Guardian;
        public Class5SkillState[] Class5Skills = Array.Empty<Class5SkillState>();
        public Class5SlowState[] Class5Slows = Array.Empty<Class5SlowState>();
    }

    [Serializable]
    public sealed class DailyChallengeStatsSnapshot
    {
        public int MaxHp, CurrentHp, Attack, Wisdom, Defense, MagicDefense;
        public float AttackSpeed, CritRate, CritDamage;
    }

    [Serializable]
    public sealed class DailyChallengePartySnapshot
    {
        public string InstanceId;
        public string MonsterId;
        public int Slot;
        public int RuntimeId;
        public int CurrentHp;
        public float AttackCooldownRemaining;
        public float AttackTimer;
        public float AttackMotionLockRemaining;
        public float PositionX, PositionY;
        public int TargetRuntimeId = -1;
        public DailyChallengeStatsSnapshot Stats;
    }

    [Serializable]
    public sealed class DailyChallengeEnemySnapshot
    {
        public int SpawnIndex;
        public int RuntimeId;
        public int CurrentHp;
        public float AttackTimer;
        public float AttackMotionLockRemaining;
        public float PositionX, PositionY;
        public int TargetRuntimeId = -1;
        public DailyChallengeStatsSnapshot Stats;
    }

    [Serializable]
    public sealed class DailyChallengePlayerSkillSnapshot
    {
        public int SkillType;
        public float CooldownRemaining;
    }

    [Serializable]
    public sealed class DailyChallengeGuardianSnapshot
    {
        public string Id;
        public string ContractId;
        public int Level;
        public DailyChallengePartySnapshot Unit;
        public float Resonance, CombatTime, LastSkillTime, BurnTimer, BurnRemaining, BarrierRemaining, SupportRemaining;
        public int BurnDamage, MarkedEnemyId = -1, SkillCount;
        public int DamageDealt, DamageBlocked, SupportedAttackCount, BurnAssistKillCount;
        public int[] BurnTargets = Array.Empty<int>();
        public int[] FollowupSlots = Array.Empty<int>();
        public int[] BarrierSlots = Array.Empty<int>();
        public int[] BarrierAmounts = Array.Empty<int>();
        public float[] ActionGainTimes = Array.Empty<float>();
        public float[] ActionGainAmounts = Array.Empty<float>();
        public float ActionGainWindow;
    }
}
