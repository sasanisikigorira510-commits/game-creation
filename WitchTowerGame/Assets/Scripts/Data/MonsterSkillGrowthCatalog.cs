using System;

namespace WitchTower.Data
{
    // 固有自動スキルの初期Lv1・最大Lv5。CT/対象数/通常攻撃を変えず、威力を段階強化する既定値。
    public static class MonsterSkillGrowthCatalog
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;
        public const float PowerBonusPerLevel = 0.10f;

        public static int NormalizeLevel(int level) => Math.Max(MinLevel, Math.Min(MaxLevel, level));

        public static float GetPowerMultiplier(int level) => 1f + (NormalizeLevel(level) - MinLevel) * PowerBonusPerLevel;
    }
}
