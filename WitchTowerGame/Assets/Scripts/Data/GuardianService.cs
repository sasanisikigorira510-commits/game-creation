using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WitchTower.Battle;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Data
{
    // Guardians have their own ownership and slot. They never enter the monster,
    // fusion, release, storage, or equipment inventories.
    public static partial class GuardianService
    {
        public const int UnlockFloor = 30;
        public const string UnlockRequirementLabel = "第3ステージクリアで解放";
        public const int MaxLevel = 30;
        public const string TutorialComplete = "guardian_first_formation";
        public const string IntroductionSeen = "guardian_introduction";
        public const int BattleSlot = PlayerProfile.PartySlotCount;
        private static readonly Dictionary<string, MonsterDataSO> MonsterCache = new Dictionary<string, MonsterDataSO>();
        private static readonly Dictionary<string, EnemyDataSO> TrialCache = new Dictionary<string, EnemyDataSO>();

        public static BattleSpiritDefinition Find(string id) => BattleSpiritCatalog.GetActiveDefinitions().FirstOrDefault(d => d.Id == id);
        public static bool IsUnlocked(PlayerProfile p) => p != null && p.HighestFloor >= UnlockFloor;
        public static bool NeedsTutorial(PlayerProfile p) => IsUnlocked(p) && p.HasCompletedTutorial && !StoryTutorialService.HasSeenHint(p, TutorialComplete);
        public static int Capacity(PlayerProfile p) => !IsUnlocked(p) ? 0 : Mathf.Clamp(1 + (p.HighestFloor - UnlockFloor) / 10, 1, 4);
        public static int NextUnlockFloor(PlayerProfile p) => UnlockFloor + 10 * (p.OwnedGuardians.Count + p.GuardianCoreIds.Count);
        public static OwnedGuardianData Owned(PlayerProfile p, string id) => p?.OwnedGuardians.FirstOrDefault(g => g.Id == id);
        public static OwnedGuardianData Equipped(PlayerProfile p) => IsUnlocked(p) ? Owned(p, p.EquippedGuardianId) : null;
        public static bool CanChallenge(PlayerProfile p, string id) => IsUnlocked(p) && Find(id) != null &&
            Owned(p, id) == null && !p.GuardianCoreIds.Contains(id) &&
            p.OwnedGuardians.Count + p.GuardianCoreIds.Count < Capacity(p);

        public static bool GrantCore(PlayerProfile p, string id)
        {
            if (!CanChallenge(p, id)) return false;
            p.GuardianCoreIds.Add(id);
            return true;
        }

        public static bool Birth(PlayerProfile p, string id)
        {
            if (!IsUnlocked(p) || Find(id) == null || Owned(p, id) != null || !p.GuardianCoreIds.Contains(id)) return false;
            p.GuardianCoreIds.Remove(id);
            p.OwnedGuardians.Add(new OwnedGuardianData { Id = id, Level = 1,
                Exp = 0, ContractId = BasicContract });
            return true;
        }

        public static bool Equip(PlayerProfile p, string id)
        {
            if (!IsUnlocked(p) || Owned(p, id) == null) return false;
            p.EquippedGuardianId = id;
            StoryTutorialService.MarkHintSeen(p, TutorialComplete);
            return true;
        }

        public static int RequiredExp(int level) => ExperienceRequirementMultiplier * (100 + Mathf.Clamp(level, 1, MaxLevel) * 40);
        public static void AddBattleExperience(PlayerProfile p, int exp)
        {
            var guardian = Equipped(p);
            if (guardian == null || exp <= 0 || guardian.Level >= MaxLevel || GuardianTrialSession.IsActive) return;
            SetIndividualExperience(guardian, (int)Math.Min(MaxCumulativeExperience,
                (long)CumulativeExperience(guardian.Level, guardian.Exp) + exp));
        }

        public static string Role(string id)
        {
            switch (id)
            {
                case "seiryu": return "高速連撃 / 味方の攻撃速度を強化";
                case "suzaku": return "範囲魔法 / 炎撃と継続する炎上";
                case "byakko": return "強敵狙い / 会心と防御を貫く一撃";
                case "genbu": return "後方の守護 / 仲間を守る障壁";
                default: return string.Empty;
            }
        }

        public static string Skill(string id)
        {
            switch (id)
            {
                case "seiryu": return "蒼天連牙";
                case "suzaku": return "焔翼の波";
                case "byakko": return "白耀の牙";
                default: return "玄冥の障壁";
            }
        }

        public static BattleSpiritModifier Modifier(string id) => new BattleSpiritModifier(
            id == "genbu" ? 1.08f : 1f, 1f, id == "suzaku" ? 1.08f : 1f,
            id == "genbu" ? 1.08f : 1f, id == "genbu" ? 1.08f : 1f,
            id == "seiryu" ? 1.08f : 1f, 1f, 1f, 0,
            id == "byakko" ? .05f : 0f, 0f, 1f, 1f);

        public static BattleUnitStats Stats(string id, int level)
        {
            float growth = 1f + .13f * (Mathf.Clamp(level, 1, MaxLevel) - 1);
            bool tank = id == "genbu";
            int hp = Mathf.RoundToInt((tank ? 480 : 270) * growth);
            return new BattleUnitStats { MaxHp = hp, CurrentHp = hp,
                Attack = Mathf.RoundToInt((id == "byakko" ? 65 : tank ? 28 : 42) * growth),
                Wisdom = Mathf.RoundToInt((id == "suzaku" ? 68 : 35) * growth),
                Defense = Mathf.RoundToInt((tank ? 40 : 20) * growth),
                MagicDefense = Mathf.RoundToInt((tank ? 35 : 22) * growth),
                AttackSpeed = id == "seiryu" ? 1.7f : tank ? .8f : 1f,
                CritRate = id == "byakko" ? .25f : .05f, CritDamage = id == "byakko" ? 1.8f : 1.5f };
        }

        public static MonsterDataSO Monster(string id)
        {
            var definition = Find(id);
            if (definition == null) return null;
            if (MonsterCache.TryGetValue(id, out var cached) && cached != null) return cached;
            var data = ScriptableObject.CreateInstance<MonsterDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.monsterId = "guardian_" + id;
            data.monsterName = definition.DisplayName;
            data.rangeType = MonsterRangeType.Ranged;
            data.damageType = id == "suzaku" ? MonsterDamageType.Magic : MonsterDamageType.Physical;
            // Visual reach offsets remain clamped; simulation bypasses distance checks.
            data.attackRange = float.PositiveInfinity;
            data.normalAttackTargetCount = id == "suzaku" ? 3 : 1;
            data.portraitResourcePath = definition.IconResourcePath;
            data.battleIdleResourcePath = definition.IdleSheetResourcePath;
            data.battleMoveResourcePath = definition.IdleSheetResourcePath;
            data.battleAttackResourcePath = definition.SummonSheetResourcePath;
            data.battleIdleFacing = data.battleMoveFacing = data.battleAttackFacing = BattleFacingDirection.Right;
            data.battleVisualScale = 1.35f;
            MonsterCache[id] = data;
            return data;
        }

        public static EnemyDataSO TrialEnemy(string id, int floor)
        {
            string key = id + floor;
            if (TrialCache.TryGetValue(key, out var cached) && cached != null) return cached;
            // Fixed acquisition tiers; a freshly fused party must train before earning a guardian.
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var stats = Stats(id, Mathf.Max(1, (floor - UnlockFloor) / 2 + 1));
            data.enemyId = "guardian_trial_" + id;
            data.enemyName = Find(id).DisplayName + "の試練";
            ConfigureTrialCombatStats(data, stats, id);
            data.damageType = id == "suzaku" ? MonsterDamageType.Magic : MonsterDamageType.Physical;
            data.attackRange = float.PositiveInfinity; // Stationary trial avatar, same reach as an allied guardian.
            data.battleIdleFacing = data.battleMoveFacing = data.battleAttackFacing = BattleFacingDirection.Right;
            data.critRate = .03f; data.critDamage = 1.3f;
            TrialCache[key] = data;
            return data;
        }

        public static BattleSpiritDefinition TrialDefinition(EnemyDataSO enemy) => enemy != null &&
            enemy.enemyId != null && enemy.enemyId.StartsWith("guardian_trial_", StringComparison.Ordinal)
            ? Find(enemy.enemyId.Substring("guardian_trial_".Length)) : null;
    }

    // Ephemeral battle context: quitting a trial never grants its reward or
    // changes dungeon progress. Only a completed victory can claim its core.
    public enum GuardianTrialMode { Acquisition, PracticeGroup, PracticeElite, Oath }

    public static class GuardianTrialSession
    {
        private static PlayerProfile owner;
        public const float PracticeDuration = 15f;
        public static string GuardianId { get; private set; }
        public static int Floor { get; private set; }
        public static bool Completed { get; private set; }
        public static GuardianTrialMode Mode { get; private set; }
        public static int LevelOverride { get; private set; }
        public static string ContractId { get; private set; }
        public static bool IsActive => !string.IsNullOrEmpty(GuardianId);
        public static bool IsPractice => IsActive && (Mode == GuardianTrialMode.PracticeGroup || Mode == GuardianTrialMode.PracticeElite);
        public static bool IsOath => IsActive && Mode == GuardianTrialMode.Oath;
        public static bool IsBorrowedGuardian => IsPractice && GuardianService.Owned(owner, GuardianId) == null;
        public static int EnemyCount => !IsActive ? 0 : Mode == GuardianTrialMode.PracticeGroup ? 5 :
            Mode == GuardianTrialMode.PracticeElite ? 1 : GuardianId == "suzaku" ? 3 : 1;
        public static EnemyDataSO Enemy(int spawnIndex) => IsActive
            ? GuardianService.SessionEnemy(GuardianId, Floor, Mode, Mathf.Clamp(spawnIndex, 0, EnemyCount - 1), LevelOverride) : null;
        public static bool ReopenSanctuary { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            owner = null; GuardianId = null; Floor = 0; LevelOverride = 1;
            ContractId = GuardianService.BasicContract; Mode = GuardianTrialMode.Acquisition;
            Completed = false; ReopenSanctuary = false;
        }
        public static bool Begin(PlayerProfile p, string id)
        {
            if (IsActive || !GuardianService.CanChallenge(p, id) ||
                !BattleVisualResolver.ResolvePartyOwnedMonsters(p, 5).Any()) return false;
            Start(p, id, GuardianTrialMode.Acquisition, GuardianService.NextUnlockFloor(p), GuardianService.ContractId(p, id));
            return true;
        }
        public static bool BeginPractice(PlayerProfile p, string id, bool strongEnemy) =>
            BeginPracticeWithContract(p, id, strongEnemy, GuardianService.ContractId(p, id));
        public static bool BeginPracticeWithContract(PlayerProfile p, string id, bool strongEnemy, string contract)
        {
#if !UNITY_EDITOR
            // Removed from the player flow; retained for isolated Editor combat checks.
            return false;
#else
            if (IsActive || !GuardianService.IsUnlocked(p) || GuardianService.Find(id) == null ||
                !BattleVisualResolver.ResolvePartyOwnedMonsters(p, 5).Any() ||
                (contract != GuardianService.BasicContract && contract != GuardianService.AlternateContract) ||
                (contract == GuardianService.AlternateContract && !GuardianService.CanUseAlternate(p, id))) return false;
            Start(p, id, strongEnemy ? GuardianTrialMode.PracticeElite : GuardianTrialMode.PracticeGroup,
                Math.Max(GuardianService.UnlockFloor, p.HighestFloor), contract);
            return true;
#endif
        }
        public static bool BeginOath(PlayerProfile p, string id)
        {
            if (IsActive || !GuardianService.CanChallengeOath(p, id) ||
                !BattleVisualResolver.ResolvePartyOwnedMonsters(p, 5).Any()) return false;
            Start(p, id, GuardianTrialMode.Oath, 60, GuardianService.ContractId(p, id));
            return true;
        }
        private static void Start(PlayerProfile p, string id, GuardianTrialMode mode, int floor, string contract)
        {
            owner = p; GuardianId = id; Mode = mode; Floor = floor;
            LevelOverride = GuardianService.Level(p, id);
            ContractId = contract; Completed = false; ReopenSanctuary = false;
        }
        public static bool Win(PlayerProfile p)
        {
            if (!IsActive || Completed || !ReferenceEquals(owner, p) || Mode != GuardianTrialMode.Acquisition) return false;
            bool granted = GuardianService.GrantCore(p, GuardianId);
            Completed = true;
            return granted;
        }
        public static bool CompleteOath(PlayerProfile p, bool hasLivingOrdinaryAlly)
        {
            if (!IsOath || Completed || !ReferenceEquals(owner, p) || !hasLivingOrdinaryAlly ||
                !GuardianService.CanChallengeOath(p, GuardianId)) return false;
            Completed = true;
            if (GuardianService.OathCleared(p, GuardianId)) return false;
            // Only a cosmetic title is unlocked. No
            // inventory, dungeon, experience, or currency rewards are granted.
            p.GuardianOathIds.Add(GuardianId);
            return true;
        }
        public static void End() { Reset(); ReopenSanctuary = true; }
    }
}
