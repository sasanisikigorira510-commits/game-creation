using System;
using UnityEngine;

namespace WitchTower.Battle
{
    public static class BattleSpiritCatalog
    {
        private static readonly BattleSpiritDefinition[] Definitions =
        {
            new BattleSpiritDefinition(
                BattleSpiritType.Suzaku,
                "suzaku",
                "朱雀",
                "攻撃/魔力 +20%",
                "UI/BattleSpirit/SpiritSuzakuIconImage2",
                "UI/BattleSpirit/Animation/SpiritSuzakuSummon256SheetImage2",
                "UI/BattleSpirit/Animation/SpiritSuzakuIdle256SheetImage2",
                new Color(1.00f, 0.26f, 0.12f, 1f),
                new BattleSpiritModifier(
                    1f,
                    1.20f,
                    1.20f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    0,
                    0f,
                    0.10f,
                    1f,
                    1f)),
            new BattleSpiritDefinition(
                BattleSpiritType.Genbu,
                "genbu",
                "玄武",
                "HP/防御 +20%",
                "UI/BattleSpirit/SpiritGenbuIconImage2",
                "UI/BattleSpirit/Animation/SpiritGenbuSummon256SheetImage2",
                "UI/BattleSpirit/Animation/SpiritGenbuIdle256SheetImage2",
                new Color(0.16f, 0.82f, 0.50f, 1f),
                new BattleSpiritModifier(
                    1.20f,
                    1f,
                    1f,
                    1.20f,
                    1.20f,
                    1f,
                    1f,
                    1f,
                    8,
                    0f,
                    0f,
                    1f,
                    1f)),
            new BattleSpiritDefinition(
                BattleSpiritType.Seiryu,
                "seiryu",
                "青龍",
                "速度 +20%",
                "UI/BattleSpirit/SpiritSeiryuIconImage2",
                "UI/BattleSpirit/Animation/SpiritSeiryuSummon256SheetImage2",
                "UI/BattleSpirit/Animation/SpiritSeiryuIdle256SheetImage2",
                new Color(0.18f, 0.62f, 1.00f, 1f),
                new BattleSpiritModifier(
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1.20f,
                    0.85f,
                    1f,
                    0,
                    0f,
                    0f,
                    1f,
                    1f)),
            new BattleSpiritDefinition(
                BattleSpiritType.Byakko,
                "byakko",
                "白虎",
                "会心/報酬 +10%",
                "UI/BattleSpirit/SpiritByakkoIconImage2",
                "UI/BattleSpirit/Animation/SpiritByakkoSummon256SheetImage2",
                "UI/BattleSpirit/Animation/SpiritByakkoIdle256SheetImage2",
                new Color(0.95f, 0.88f, 0.62f, 1f),
                new BattleSpiritModifier(
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    0,
                    0.10f,
                    0.20f,
                    1.10f,
                    1.10f))
        };

        public static BattleSpiritDefinition[] GetActiveDefinitions()
        {
            var result = new BattleSpiritDefinition[Definitions.Length];
            Array.Copy(Definitions, result, Definitions.Length);
            return result;
        }

        public static BattleSpiritModifier CreateCombinedModifier()
        {
            BattleSpiritModifier result = BattleSpiritModifier.Identity;
            for (int i = 0; i < Definitions.Length; i += 1)
            {
                result = result.Combine(Definitions[i].Modifier);
            }

            return result;
        }

        public static BattleSpiritDefinition GetDefinition(BattleSpiritType spiritType)
        {
            for (int i = 0; i < Definitions.Length; i += 1)
            {
                if (Definitions[i].SpiritType == spiritType)
                {
                    return Definitions[i];
                }
            }

            return Definitions.Length > 0 ? Definitions[0] : null;
        }
    }
}
