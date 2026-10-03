using UnityEngine;
using WitchTower.Data;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private bool TrySpawnGarzaFlame(BattleHitInfo hit)
        {
            var simulator = stateMachine?.Simulator;
            if (!hit.TargetIsPlayer || simulator == null ||
                simulator.GetEnemyId(hit.AttackerIndex) != GarzaBossPresentation.EnemyId) return false;
            EnsureMinimalCanvas();
            EnsureRangedEffectRoot();
            if (!TryResolveRangedAttackEndpoints(hit, null, false, out var start, out var end)) return true;
            var body = enemyPreviewImages[hit.AttackerIndex].rectTransform;
            // Socket in the registered cast frame, kept separate from target aiming.
            Vector3 socket = body.TransformPoint(new Vector3(body.rect.width * -.21f, body.rect.height * -.045f));
            start = minimalCanvasRoot.transform.InverseTransformPoint(socket);
            SpawnAnimatedMovingRangedAttackEffect(
                BattleVisualResolver.LoadSpriteFrames("BattleEffects/Garza/projectile"), Color.white,
                start, end, GarzaBossPresentation.LaunchDelay, 0f, 100f,
                GarzaBossPresentation.ImpactDelay - GarzaBossPresentation.LaunchDelay, 1f, 1f, 0f, 0f);
            SpawnAnimatedStaticRangedAttackEffect(
                BattleVisualResolver.LoadSpriteFrames("BattleEffects/Garza/impact"), Color.white,
                end, GarzaBossPresentation.ImpactDelay, 165f, .32f, 1f, 1f, 0f, 0f);
            return true;
        }
    }
}
