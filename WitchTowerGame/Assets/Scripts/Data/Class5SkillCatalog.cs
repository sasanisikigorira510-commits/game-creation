using System;
using System.Collections.Generic;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public enum Class5SkillArea { Single, TargetCluster, SelfFront }

    public sealed class Class5SkillDefinition
    {
        public string MonsterId { get; }
        public string SkillId { get; }
        public string Name { get; }
        public MonsterDamageType DamageType { get; }
        public float Cooldown { get; }
        public int MaxTargets { get; }
        public float PowerPerHit { get; }
        public int HitCount { get; }
        public float HitInterval { get; }
        public Class5SkillArea Area { get; }
        // Battle anchor units. These bounded areas are initial runtime geometry,
        // separate from the approved power/cooldown and target-count limits.
        public float AreaRadius { get; }
        public float DamageReduction { get; }
        public float DamageReductionDuration { get; }
        public float MovementSlow { get; }
        public float MovementSlowDuration { get; }

        internal Class5SkillDefinition(string key, string name, MonsterDamageType damageType,
            float cooldown, int maxTargets, float power, Class5SkillArea area = Class5SkillArea.Single,
            int hits = 1, float interval = 0f, float reduction = 0f, float reductionDuration = 0f,
            float slow = 0f, float slowDuration = 0f)
        {
            MonsterId = "monster_" + key;
            SkillId = "class5_skill_" + key;
            Name = name; DamageType = damageType; Cooldown = cooldown;
            MaxTargets = maxTargets; PowerPerHit = power; HitCount = hits;
            HitInterval = interval; Area = area; AreaRadius = area == Class5SkillArea.SelfFront ? .24f : .22f;
            DamageReduction = reduction; DamageReductionDuration = reductionDuration;
            MovementSlow = slow; MovementSlowDuration = slowDuration;
        }
    }

    [Serializable]
    public sealed class Class5SkillState
    {
        public string MonsterInstanceId;
        public float CooldownRemaining, MotionRemaining, DamageReductionRemaining;
        public float PendingElapsed;
        public int NextHitIndex;
        public float CastOffense, CastPowerMultiplier;
        public int[] TargetRuntimeIds = Array.Empty<int>();
    }

    [Serializable]
    public sealed class Class5SlowState
    {
        public int EnemyRuntimeId;
        public float Reduction, Remaining;
    }

    public static class Class5SkillCatalog
    {
        public const float MotionDuration = .8f;
        public const float FirstHitDelay = .2f;
        private static readonly Class5SkillDefinition[] Entries =
        {
            new Class5SkillDefinition("astravarn", "星帝竜牙", MonsterDamageType.Physical, 12f, 1, 6.5f),
            new Class5SkillDefinition("ordion", "創世殲滅砲", MonsterDamageType.Physical, 10f, 6, 2.5f, Class5SkillArea.TargetCluster),
            new Class5SkillDefinition("geoatlas", "星核結晶壁", MonsterDamageType.Physical, 14f, 5, 1.2f, Class5SkillArea.SelfFront,
                reduction: .35f, reductionDuration: 6f),
            new Class5SkillDefinition("regnard", "覇天炎剣・三連", MonsterDamageType.Physical, 8f, 1, 1.5f,
                hits: 3, interval: .15f),
            new Class5SkillDefinition("noxveil", "深淵星葬", MonsterDamageType.Magic, 12f, 8, 2.3f, Class5SkillArea.TargetCluster),
            new Class5SkillDefinition("celestia", "天界裁きの槍", MonsterDamageType.Magic, 12f, 1, 7f),
            new Class5SkillDefinition("yggdrasia", "世界樹の束縛", MonsterDamageType.Magic, 10f, 6, 2f, Class5SkillArea.TargetCluster,
                slow: .3f, slowDuration: 4f)
        };
        public static IReadOnlyList<Class5SkillDefinition> Definitions { get; } = Array.AsReadOnly(Entries);

        public static Class5SkillDefinition Resolve(string monsterId)
        {
            for (int i = 0; i < Entries.Length; i++)
                if (string.Equals(Entries[i].MonsterId, monsterId, StringComparison.Ordinal)) return Entries[i];
            return null;
        }

        public static int CalculateDamage(float offense, float powerPerHit, int defense, float powerMultiplier)
        {
            if (float.IsNaN(offense) || float.IsInfinity(offense) || offense < 0f) offense = 0f;
            if (float.IsNaN(powerPerHit) || float.IsInfinity(powerPerHit) || powerPerHit < 0f) powerPerHit = 0f;
            if (float.IsNaN(powerMultiplier) || float.IsInfinity(powerMultiplier)) powerMultiplier = 1f;
            double raw = (double)offense * powerPerHit * Math.Max(1f, Math.Min(1.4f, powerMultiplier)) *
                100d / (100d + Math.Max(0, defense));
            return (int)Math.Max(1d, Math.Min(int.MaxValue, Math.Round(raw)));
        }
    }
}
