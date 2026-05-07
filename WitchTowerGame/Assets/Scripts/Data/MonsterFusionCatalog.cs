using System;
using System.Collections.Generic;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    public enum MonsterRace
    {
        Unknown = 0,
        Dragon = 1,
        Robot = 2,
        Golem = 3,
        Swordsman = 4,
        Mage = 5,
        Angel = 6,
        Spirit = 7,
        Special = 99
    }

    public enum MonsterClass
    {
        Unknown = 0,
        Lower = 1,
        Middle = 2,
        Upper = 3,
        Class4 = 4,
        Special = 99
    }

    public enum MonsterFusionRecipeType
    {
        ClassUp = 0,
        SameClassParentRace = 1,
        Special = 2,
        ParentRaceHighestClass = 3
    }

    public sealed class MonsterFusionMonsterDefinition
    {
        public MonsterFusionMonsterDefinition(string monsterId, string displayName, MonsterRace race, MonsterClass monsterClass)
        {
            MonsterId = monsterId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Race = race;
            Class = monsterClass;
        }

        public string MonsterId { get; }
        public string DisplayName { get; }
        public MonsterRace Race { get; }
        public MonsterClass Class { get; }
    }

    public sealed class MonsterFusionRecipeDefinition
    {
        public MonsterFusionRecipeDefinition(
            string parentMonsterIdA,
            string parentMonsterIdB,
            string resultMonsterId,
            MonsterFusionRecipeType recipeType,
            bool ignoreOrder = true)
        {
            ParentMonsterIdA = parentMonsterIdA ?? string.Empty;
            ParentMonsterIdB = parentMonsterIdB ?? string.Empty;
            ResultMonsterId = resultMonsterId ?? string.Empty;
            RecipeType = recipeType;
            IgnoreOrder = ignoreOrder;
        }

        public string ParentMonsterIdA { get; }
        public string ParentMonsterIdB { get; }
        public string ResultMonsterId { get; }
        public MonsterFusionRecipeType RecipeType { get; }
        public bool IgnoreOrder { get; }

        public bool Matches(string firstMonsterId, string secondMonsterId)
        {
            if (string.IsNullOrEmpty(firstMonsterId) || string.IsNullOrEmpty(secondMonsterId))
            {
                return false;
            }

            bool directMatch = ParentMonsterIdA == firstMonsterId && ParentMonsterIdB == secondMonsterId;
            bool reverseMatch = IgnoreOrder && ParentMonsterIdA == secondMonsterId && ParentMonsterIdB == firstMonsterId;
            return directMatch || reverseMatch;
        }
    }

    public static class MonsterFusionCatalog
    {
        public const string DragonWhelpId = "monster_dragon_whelp";
        public const string FlareDrakeId = "monster_flare_drake";
        public const string AbyssDragonId = "monster_abyss_dragon";

        public const string ChibiGearId = "monster_chibi_gear";
        public const string ArmedDroidId = "monster_armed_droid";
        public const string OmegaLeonId = "monster_omega_leon";

        public const string RockGolemId = "monster_rock_golem";
        public const string OreGiantGarmId = "monster_ore_giant_garm";
        public const string CosmicOreFortressGolemId = "monster_cosmic_ore_fortress_golem";

        public const string ApprenticeSwordsmanId = "monster_apprentice_swordsman";
        public const string HolyArmorLeonId = "monster_holy_armor_leon";
        public const string SwordSaintAlvarezId = "monster_sword_saint_alvarez";

        public const string ApprenticeMageId = "monster_apprentice_mage";
        public const string DarkRobeCurseMageNoahId = "monster_dark_robe_curse_mage_noah";
        public const string AbyssGrandMageSeraphisId = "monster_abyss_grand_mage_seraphis";

        public const string MechaDragonValdrakeId = "monster_mecha_dragon_valdrake";
        public const string DragGaiaId = "monster_drag_gaia";
        public const string DragonSwordSaintAgitoId = "monster_dragon_sword_saint_agito";
        public const string AbyssDragonMageValflareId = "monster_abyss_dragon_mage_valflare";
        public const string FortressMachineGigafortId = "monster_fortress_machine_gigafort";
        public const string MechaSwordSaintGransaberId = "monster_mecha_sword_saint_gransaber";
        public const string DarkMagicMachineGodMerchionId = "monster_dark_magic_machine_god_merchion";
        public const string RockKnightGaiusId = "monster_rock_knight_gaius";
        public const string AstralEclipseGolemId = "monster_astral_eclipse_golem";
        public const string MagicSwordSaintLucielId = "monster_magic_sword_saint_luciel";
        public const string SeraphMichaelId = "monster_seraph_michael";
        public const string SpiritQueenTitaniaId = "monster_spirit_queen_titania";

        private static readonly MonsterFusionMonsterDefinition[] MonsterDefinitions =
        {
            new MonsterFusionMonsterDefinition(DragonWhelpId, "ヒナドラ", MonsterRace.Dragon, MonsterClass.Lower),
            new MonsterFusionMonsterDefinition(FlareDrakeId, "フレアドレイク", MonsterRace.Dragon, MonsterClass.Middle),
            new MonsterFusionMonsterDefinition(AbyssDragonId, "蒼黒竜アビス", MonsterRace.Dragon, MonsterClass.Upper),

            new MonsterFusionMonsterDefinition(ChibiGearId, "チビギア", MonsterRace.Robot, MonsterClass.Lower),
            new MonsterFusionMonsterDefinition(ArmedDroidId, "アームドロイド", MonsterRace.Robot, MonsterClass.Middle),
            new MonsterFusionMonsterDefinition(OmegaLeonId, "機皇オメガレオン", MonsterRace.Robot, MonsterClass.Upper),

            new MonsterFusionMonsterDefinition(RockGolemId, "ロックゴーレム", MonsterRace.Golem, MonsterClass.Lower),
            new MonsterFusionMonsterDefinition(OreGiantGarmId, "鉱石巨人ガルム", MonsterRace.Golem, MonsterClass.Middle),
            new MonsterFusionMonsterDefinition(CosmicOreFortressGolemId, "宇宙鉱石要塞ゴーレム", MonsterRace.Golem, MonsterClass.Upper),

            new MonsterFusionMonsterDefinition(ApprenticeSwordsmanId, "見習い剣士", MonsterRace.Swordsman, MonsterClass.Lower),
            new MonsterFusionMonsterDefinition(HolyArmorLeonId, "聖鎧剣士レオン", MonsterRace.Swordsman, MonsterClass.Middle),
            new MonsterFusionMonsterDefinition(SwordSaintAlvarezId, "剣聖アルヴァレス", MonsterRace.Swordsman, MonsterClass.Upper),

            new MonsterFusionMonsterDefinition(ApprenticeMageId, "見習い魔導士", MonsterRace.Mage, MonsterClass.Lower),
            new MonsterFusionMonsterDefinition(DarkRobeCurseMageNoahId, "黒衣の呪術師ノア", MonsterRace.Mage, MonsterClass.Middle),
            new MonsterFusionMonsterDefinition(AbyssGrandMageSeraphisId, "深淵大魔導セラフィス", MonsterRace.Mage, MonsterClass.Upper),

            new MonsterFusionMonsterDefinition(MechaDragonValdrakeId, "機竜ヴァルドレイク", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(DragGaiaId, "竜岩巨兵ドラグガイア", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(DragonSwordSaintAgitoId, "竜剣聖アギト", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(AbyssDragonMageValflareId, "深淵竜魔導ヴァルフレア", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(FortressMachineGigafortId, "要塞機兵ギガフォート", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(MechaSwordSaintGransaberId, "機甲剣聖グランセイバー", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(DarkMagicMachineGodMerchionId, "暗黒魔導機神メルキオン", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(RockKnightGaiusId, "巨岩騎士ガイアス", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(AstralEclipseGolemId, "星蝕魔像アストラルゴーレム", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(MagicSwordSaintLucielId, "魔剣聖ルシエル", MonsterRace.Special, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(SeraphMichaelId, "熾天使ミカエル", MonsterRace.Angel, MonsterClass.Class4),
            new MonsterFusionMonsterDefinition(SpiritQueenTitaniaId, "精霊女王ティターニア", MonsterRace.Spirit, MonsterClass.Class4)
        };

        private static readonly MonsterFusionRecipeDefinition[] Recipes =
        {
            new MonsterFusionRecipeDefinition(DragonWhelpId, DragonWhelpId, FlareDrakeId, MonsterFusionRecipeType.ClassUp),
            new MonsterFusionRecipeDefinition(FlareDrakeId, FlareDrakeId, AbyssDragonId, MonsterFusionRecipeType.ClassUp),

            new MonsterFusionRecipeDefinition(ChibiGearId, ChibiGearId, ArmedDroidId, MonsterFusionRecipeType.ClassUp),
            new MonsterFusionRecipeDefinition(ArmedDroidId, ArmedDroidId, OmegaLeonId, MonsterFusionRecipeType.ClassUp),

            new MonsterFusionRecipeDefinition(RockGolemId, RockGolemId, OreGiantGarmId, MonsterFusionRecipeType.ClassUp),
            new MonsterFusionRecipeDefinition(OreGiantGarmId, OreGiantGarmId, CosmicOreFortressGolemId, MonsterFusionRecipeType.ClassUp),

            new MonsterFusionRecipeDefinition(ApprenticeSwordsmanId, ApprenticeSwordsmanId, HolyArmorLeonId, MonsterFusionRecipeType.ClassUp),
            new MonsterFusionRecipeDefinition(HolyArmorLeonId, HolyArmorLeonId, SwordSaintAlvarezId, MonsterFusionRecipeType.ClassUp),

            new MonsterFusionRecipeDefinition(ApprenticeMageId, ApprenticeMageId, DarkRobeCurseMageNoahId, MonsterFusionRecipeType.ClassUp),
            new MonsterFusionRecipeDefinition(DarkRobeCurseMageNoahId, DarkRobeCurseMageNoahId, AbyssGrandMageSeraphisId, MonsterFusionRecipeType.ClassUp),

            new MonsterFusionRecipeDefinition(AbyssDragonId, OmegaLeonId, MechaDragonValdrakeId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(AbyssDragonId, CosmicOreFortressGolemId, DragGaiaId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(AbyssDragonId, SwordSaintAlvarezId, DragonSwordSaintAgitoId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(AbyssDragonId, AbyssGrandMageSeraphisId, AbyssDragonMageValflareId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(OmegaLeonId, CosmicOreFortressGolemId, FortressMachineGigafortId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(OmegaLeonId, SwordSaintAlvarezId, MechaSwordSaintGransaberId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(OmegaLeonId, AbyssGrandMageSeraphisId, DarkMagicMachineGodMerchionId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(CosmicOreFortressGolemId, SwordSaintAlvarezId, RockKnightGaiusId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(CosmicOreFortressGolemId, AbyssGrandMageSeraphisId, AstralEclipseGolemId, MonsterFusionRecipeType.Special),
            new MonsterFusionRecipeDefinition(SwordSaintAlvarezId, AbyssGrandMageSeraphisId, MagicSwordSaintLucielId, MonsterFusionRecipeType.Special)
        };

        public static IReadOnlyList<MonsterFusionMonsterDefinition> GetMonsterDefinitions()
        {
            return MonsterDefinitions;
        }

        public static IReadOnlyList<MonsterFusionRecipeDefinition> GetRecipes()
        {
            return Recipes;
        }

        public static bool TryGetMonsterDefinition(string monsterId, out MonsterFusionMonsterDefinition definition)
        {
            foreach (MonsterFusionMonsterDefinition candidate in MonsterDefinitions)
            {
                if (candidate.MonsterId == monsterId)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static bool TryResolveRecipe(string firstMonsterId, string secondMonsterId, out MonsterFusionRecipeDefinition recipe)
        {
            return TryResolveRecipe(firstMonsterId, secondMonsterId, out recipe, false);
        }

        public static bool TryResolveRecipe(string firstMonsterId, string secondMonsterId, out MonsterFusionRecipeDefinition recipe, bool includeSpecial)
        {
            foreach (MonsterFusionRecipeDefinition candidate in Recipes)
            {
                if (!includeSpecial && candidate.RecipeType == MonsterFusionRecipeType.Special)
                {
                    continue;
                }

                if (candidate.Matches(firstMonsterId, secondMonsterId))
                {
                    recipe = candidate;
                    return true;
                }
            }

            recipe = null;
            return false;
        }

        public static bool TryResolveNormalRecipe(
            MonsterDataSO firstParentData,
            MonsterDataSO secondParentData,
            IEnumerable<MonsterDataSO> allMonsterData,
            out MonsterFusionRecipeDefinition recipe,
            out MonsterDataSO resultMonsterData)
        {
            recipe = null;
            resultMonsterData = null;

            if (firstParentData == null || secondParentData == null || allMonsterData == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(firstParentData.monsterId) ||
                string.IsNullOrEmpty(secondParentData.monsterId) ||
                string.IsNullOrEmpty(firstParentData.raceId) ||
                string.IsNullOrEmpty(secondParentData.raceId))
            {
                return false;
            }

            int firstClassRank = Math.Max(1, firstParentData.classRank);
            int secondClassRank = Math.Max(1, secondParentData.classRank);
            if (!IsNormalClassRank(firstClassRank) || !IsNormalClassRank(secondClassRank))
            {
                return false;
            }

            bool sameRace = string.Equals(firstParentData.raceId, secondParentData.raceId, StringComparison.OrdinalIgnoreCase);
            bool sameClass = firstClassRank == secondClassRank;
            bool classUp = sameRace && sameClass;
            // 通常配合は異種族・クラス違いも許可する。結果は親1の種族で、高い方のクラスにそろえる。
            int resultClassRank = classUp
                ? firstClassRank + 1
                : Math.Max(firstClassRank, secondClassRank);
            if (!IsNormalClassRank(resultClassRank))
            {
                return false;
            }

            resultMonsterData = FindNormalMonsterByRaceAndClass(allMonsterData, firstParentData.raceId, resultClassRank);
            if (resultMonsterData == null)
            {
                return false;
            }

            recipe = new MonsterFusionRecipeDefinition(
                firstParentData.monsterId,
                secondParentData.monsterId,
                resultMonsterData.monsterId,
                classUp ? MonsterFusionRecipeType.ClassUp : MonsterFusionRecipeType.ParentRaceHighestClass,
                false);
            return true;
        }

        private static bool IsNormalClassRank(int classRank)
        {
            return classRank >= 1 && classRank <= 3;
        }

        private static MonsterDataSO FindNormalMonsterByRaceAndClass(IEnumerable<MonsterDataSO> allMonsterData, string raceId, int classRank)
        {
            MonsterDataSO bestCandidate = null;
            foreach (MonsterDataSO monsterData in allMonsterData)
            {
                if (monsterData == null || monsterData.fusionExclusive)
                {
                    continue;
                }

                if (!string.Equals(monsterData.raceId, raceId, StringComparison.OrdinalIgnoreCase) ||
                    Math.Max(1, monsterData.classRank) != classRank)
                {
                    continue;
                }

                if (bestCandidate == null || monsterData.encyclopediaNumber < bestCandidate.encyclopediaNumber)
                {
                    bestCandidate = monsterData;
                }
            }

            return bestCandidate;
        }
    }
}
