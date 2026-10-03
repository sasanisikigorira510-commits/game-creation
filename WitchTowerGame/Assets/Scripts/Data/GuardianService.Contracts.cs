using System;
using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Data
{
    public static partial class GuardianService
    {
        public const string BasicContract = "basic";
        public const string AlternateContract = "alternate";
        public const int AlternateContractLevel = 10;
        public const int BondDialogueLevel = 20;
        private const int ExperienceRequirementMultiplier = 6;
        public const int MaxCumulativeExperience = ExperienceRequirementMultiplier *
            (100 * (MaxLevel - 1) + 40 * (MaxLevel - 1) * MaxLevel / 2);

        public static int CumulativeExperience(int level, int exp)
        {
            int normalizedLevel = Mathf.Clamp(level, 1, MaxLevel);
            long total = normalizedLevel == MaxLevel ? 0 : Math.Max(0, exp);
            for (int i = 1; i < normalizedLevel; i++) total += RequiredExp(i);
            return (int)Math.Min(MaxCumulativeExperience, total);
        }

        internal static void SetIndividualExperience(OwnedGuardianData guardian, int cumulative)
        {
            int remaining = Mathf.Clamp(cumulative, 0, MaxCumulativeExperience);
            int level = 1;
            while (level < MaxLevel && remaining >= RequiredExp(level))
            {
                remaining -= RequiredExp(level);
                level++;
            }
            guardian.Level = level;
            guardian.Exp = level == MaxLevel ? 0 : remaining;
            if (level < AlternateContractLevel) guardian.ContractId = BasicContract;
        }

        // Unowned practice avatars begin at Lv.1 and never create ownership.
        public static int Level(PlayerProfile p, string id) => Mathf.Clamp(Owned(p, id)?.Level ?? 1, 1, MaxLevel);
        public static int Experience(PlayerProfile p, string id) => Level(p, id) >= MaxLevel ? 0 : Math.Max(0, Owned(p, id)?.Exp ?? 0);
        public static bool CanUseAlternate(PlayerProfile p, string id) => Owned(p, id) != null && Level(p, id) >= AlternateContractLevel;

        public static string ContractId(PlayerProfile p, string id) =>
            CanUseAlternate(p, id) && Owned(p, id)?.ContractId == AlternateContract ? AlternateContract : BasicContract;

        public static bool SetContract(PlayerProfile p, string id, string contract)
        {
            var guardian = Owned(p, id);
            if (!IsUnlocked(p) || guardian == null || GuardianTrialSession.IsActive ||
                (contract != BasicContract && contract != AlternateContract) ||
                (contract == AlternateContract && !CanUseAlternate(p, id))) return false;
            guardian.ContractId = contract;
            return true;
        }

        public static string ContractLabel(string id, string contract)
        {
            bool alternate = contract == AlternateContract;
            switch (id)
            {
                case "seiryu": return alternate ? "追撃" : "追風";
                case "suzaku": return alternate ? "孤炎" : "炎海";
                case "byakko": return alternate ? "追討" : "破軍";
                case "genbu": return alternate ? "反攻" : "堅守";
                default: return string.Empty;
            }
        }

        public static string ContractDescription(string id, string contract)
        {
            bool alternate = contract == AlternateContract;
            switch (id)
            {
                case "seiryu": return alternate
                    ? "神技後4秒、通常仲間それぞれの次の通常攻撃の直接ダメージが25%増加。"
                    : "神技後3秒、通常仲間の攻撃速度が15%増加。";
                case "suzaku": return alternate
                    ? "神技を最大HPが最も高い敵1体に集中。炎上中の対象へ、通常仲間の魔法通常攻撃が30%増加。"
                    : "神技で最大6体を攻撃・炎上。炎上中の敵へ、通常仲間の魔法通常攻撃が15%増加。";
                case "byakko": return alternate
                    ? "神技後4秒、HP30%以下の敵へ通常仲間の通常攻撃ダメージが20%増加。"
                    : "防御を貫く神技で対象に4秒間の牙印。牙印への通常仲間の会心ダメージが20%増加。";
                case "genbu": return alternate
                    ? "神技で自身の最大HP12%分の障壁を6秒間展開。障壁の残る通常仲間の通常攻撃が20%増加。"
                    : "神技で自身の最大HP18%分の障壁を6秒間、仲間に展開。";
                default: return string.Empty;
            }
        }

        public static bool OathCleared(PlayerProfile p, string id) => p?.GuardianOathIds.Contains(id) == true;
        public static bool CanChallengeOath(PlayerProfile p, string id) => IsUnlocked(p) && Owned(p, id) != null &&
            Level(p, id) >= MaxLevel && p.HighestFloor >= 60 &&
            p.SeenStoryEventIds.Contains(StoryTutorialService.StoryFirstArcComplete) && p.EquippedGuardianId == id;

        public static string OathTitle(string id)
        {
            switch (id)
            {
                case "seiryu": return "蒼天を共に翔ける者";
                case "suzaku": return "帰り火を守る者";
                case "byakko": return "白耀と並び立つ者";
                case "genbu": return "帰還の岸を守る者";
                default: return string.Empty;
            }
        }
    }
}
