using UnityEngine;

namespace WitchTower.Battle
{
    public sealed class BattleSpiritDefinition
    {
        public BattleSpiritDefinition(
            BattleSpiritType spiritType,
            string id,
            string displayName,
            string shortEffectText,
            string iconResourcePath,
            string summonSheetResourcePath,
            string idleSheetResourcePath,
            Color themeColor,
            BattleSpiritModifier modifier)
        {
            SpiritType = spiritType;
            Id = id;
            DisplayName = displayName;
            ShortEffectText = shortEffectText;
            IconResourcePath = iconResourcePath;
            SummonSheetResourcePath = summonSheetResourcePath;
            IdleSheetResourcePath = idleSheetResourcePath;
            ThemeColor = themeColor;
            Modifier = modifier;
        }

        public BattleSpiritType SpiritType { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string ShortEffectText { get; }
        public string IconResourcePath { get; }
        public string SummonSheetResourcePath { get; }
        public string IdleSheetResourcePath { get; }
        public Color ThemeColor { get; }
        public BattleSpiritModifier Modifier { get; }
    }
}
