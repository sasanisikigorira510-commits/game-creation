using System.Collections.Generic;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private float Class5SkillMotionRemaining(int slot)
        {
            var simulator = stateMachine?.Simulator;
            return simulator != null && simulator.IsRunning
                ? simulator.GetClass5SkillMotionRemaining(slot) : 0f;
        }

        private List<Sprite> Class5SkillFrames(int slot)
        {
            var data = slot >= 0 && slot < allyPreviewMonsterData.Count ? allyPreviewMonsterData[slot] : null;
            if (Class5SkillCatalog.Resolve(data?.monsterId) == null) return null;
            return BattleVisualResolver.ResolveSpriteFramesFromResourcePath(
                "MonsterBattle/mon_" + data.monsterId.Substring("monster_".Length) + "_skill");
        }

        private bool TrySelectClass5SkillSprite(int slot, out Sprite sprite)
        {
            sprite = null;
            float remaining = Class5SkillMotionRemaining(slot);
            if (remaining <= 0f) return false;
            var frames = Class5SkillFrames(slot);
            if (frames == null || frames.Count == 0) return false;
            float progress = Mathf.Clamp01(1f - remaining / Class5SkillCatalog.MotionDuration);
            sprite = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(progress * frames.Count))];
            return sprite != null;
        }

        private void HandleClass5SkillInvoked(int slot, string skillId)
        {
            AudioManager.Instance?.PlaySe(AudioCue.Attack);
        }

        private MonsterAttackEffectDefinition ResolveClass5AttackEffect(BattleHitInfo hit, MonsterDataSO data)
        {
            var skill = Class5SkillCatalog.Resolve(data?.monsterId);
            if (skill == null) return null;
            bool skillHit = hit.IsMonsterSkill && hit.MonsterSkillId == skill.SkillId;
            string key = data.monsterId.Substring("monster_".Length);
            bool cannon = key == "ordion";
            bool self = skillHit && key == "geoatlas";
            return new MonsterAttackEffectDefinition
            {
                ResourcePath = "BattleEffects/Monster/fx_" + key + (skillHit ? "_skill" : "_attack"),
                Placement = self ? MonsterAttackEffectPlacement.CasterBurst : cannon
                    ? MonsterAttackEffectPlacement.Beam : MonsterAttackEffectPlacement.TargetBurst,
                Scale = skillHit ? 1.65f : 1.1f,
                Duration = skillHit ? .62f : .36f,
                BeamThickness = skillHit ? 100f : 60f,
                StartOffset = Vector2.zero,
                TargetOffset = Vector2.zero,
                UseCasterCenter = self || cannon,
                PreserveSourceAppearance = true,
                MirrorForEnemies = true,
                FadeOutScale = 1f,
                Tint = Color.white
            };
        }
    }
}
