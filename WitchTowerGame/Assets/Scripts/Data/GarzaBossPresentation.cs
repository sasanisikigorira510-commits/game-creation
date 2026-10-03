using System.Collections.Generic;
using UnityEngine;
using WitchTower.Battle;
using WitchTower.MasterData;

namespace WitchTower.Data
{
    // Enemy-only identity. Never register this ID as an obtainable MonsterDataSO.
    public static class GarzaBossPresentation
    {
        public const string MonsterId = "monster_demon_king_garza";
        public const string EnemyId = "enemy_demon_king_garza";
        public const string DisplayName = "魔王ガルザ";
        public const string ResourceBase = "MonsterBattle/Garza/garza";
        public const string BattleIdlePath = ResourceBase + "_idle_0";
        public const string StoryResourceBase = "UI/StoryPortraits/Garza/garza";
        public const string PortraitPath = StoryResourceBase + "_idle_0";
        public const float AttackDuration = .74f;
        public const float LaunchDelay = .37f;
        public const float ImpactDelay = .55f;
        public const float VisualScale = 1.35f;

        public static bool IsGarza(EnemyDataSO enemy) => enemy != null && enemy.enemyId == EnemyId;

        public static List<Sprite> Frames(string pose) => BattleVisualResolver.LoadSpriteFrames(ResourceBase + "_" + pose);

        public static Sprite Frame(string pose, float elapsed)
            => SelectFrame(ResourceBase, pose, elapsed);

        public static Sprite StoryFrame(string pose, float elapsed)
            => SelectFrame(StoryResourceBase, pose, elapsed);

        private static Sprite SelectFrame(string resourceBase, string pose, float elapsed)
        {
            var frames = BattleVisualResolver.LoadSpriteFrames(resourceBase + "_" + pose);
            if (frames.Count == 0) return null;
            bool loop = pose == "idle";
            float duration = pose == "attack" ? AttackDuration : pose == "defeat" ? 1.2f : .5f;
            int index = loop ? Mathf.FloorToInt(Mathf.Max(0, elapsed) * 4f) % frames.Count :
                Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, elapsed) / duration * frames.Count), 0, frames.Count - 1);
            // Attacks and hits recover into idle; defeat holds its final frame.
            if (!loop && pose != "defeat" && elapsed >= duration) return SelectFrame(resourceBase,"idle", elapsed - duration);
            return frames[index];
        }

        // Zero-based manuscript indices. Visuals have no battle/reward/save effects.
        public static string StoryPose(string eventId, int index)
        {
            if (eventId != "story_first_arc_complete") return null;
            switch (index)
            {
                case 0: return "idle";
                case 8: return "attack";
                case 13: return "hit";
                case 16: return "defeat";
                default: return null;
            }
        }
    }
}
