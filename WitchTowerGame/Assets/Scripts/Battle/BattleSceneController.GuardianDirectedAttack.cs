using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private const float GuardianDirectedChargeDuration = GuardianAttackPresentationSettings.ChargeDuration;
        private const float GuardianDirectedTravelDuration = GuardianAttackPresentationSettings.SeiryuTravelDuration;
        private const float GuardianDirectedSuzakuTravelDuration = GuardianAttackPresentationSettings.SuzakuTravelDuration;
        private const float GuardianDirectedImpactDuration = GuardianAttackPresentationSettings.ImpactDuration;
        private const int GuardianDirectedTrailCount = 9;

        private sealed class GuardianDirectedAttack
        {
            public BattleHitInfo HitInfo;
            public string GuardianId;
            public Vector2 StartPosition;
            public Vector2 EndPosition;
            public float Elapsed;
            public float ImpactTime;
            public bool ImpactApplied;
            public bool TargetRemoved;
            public bool LethalAtResolution;
            public bool OwnsDefeat;
            public Image Charge;
            public Image Projectile;
            public readonly List<Image> Trail = new List<Image>();
            public Image Impact;
            public Image DefeatedTarget;
            public Vector2 DefeatedTargetSize;
            public Vector3 DefeatedTargetScale;
            public IReadOnlyList<Sprite> RetainedTargetIdleFrames;
            public float RetainedTargetAnimationTime;
        }

        private readonly List<GuardianDirectedAttack> activeGuardianDirectedAttacks = new List<GuardianDirectedAttack>();
        private BattleResult guardianPendingPresentationResult = BattleResult.None;
        private bool guardianCompletingAllyDefeat;
        private readonly HashSet<int> guardianHeldAllyDefeats = new HashSet<int>();
        private bool HasPendingGuardianDirectedPresentation => activeGuardianDirectedAttacks.Count > 0;
        private BattleResult ResolveGuardianPresentationResult(BattleResult result)
        {
            if (result != BattleResult.None) guardianPendingPresentationResult = result;
            if (HasPendingGuardianDirectedPresentation) return BattleResult.None;
            BattleResult completed = guardianPendingPresentationResult;
            guardianPendingPresentationResult = BattleResult.None;
            return completed;
        }

        private static bool IsDirectedGuardian(string id) => id == "seiryu" || id == "suzaku";
        private static float GuardianDirectedImpactDelay(string id) => GuardianAttackPresentationSettings.ImpactDelay(id);

        private Sprite SelectGuardianAttackBody(string id, float remaining)
        {
            float t = Mathf.Clamp01(1f - remaining / AttackVisualDuration);
            int frame = Mathf.Clamp(Mathf.FloorToInt(t * 8f), 0, 7);
            return GuardianSprite("Animation/" + GuardianArtName(id) + "/AttackBody_" + frame);
        }

        private string ResolveDirectedGuardianId(BattleHitInfo hit)
        {
            var simulator = stateMachine?.Simulator;
            if (simulator == null) return null;
            if (!hit.TargetIsPlayer)
                return hit.AttackerIndex == GuardianService.BattleSlot && IsDirectedGuardian(simulator.GuardianId)
                    ? simulator.GuardianId : null;
            int index = hit.AttackerRuntimeId >= 0 ? simulator.FindEnemyIndexByRuntimeId(hit.AttackerRuntimeId) : hit.AttackerIndex;
            string enemyId = index >= 0 ? simulator.GetEnemyId(index) : null;
            if (enemyId == "guardian_trial_seiryu") return "seiryu";
            if (enemyId == "guardian_trial_suzaku") return "suzaku";
            return null;
        }

        private bool TryStartGuardianDirectedAttack(BattleHitInfo hit)
        {
            string id = ResolveDirectedGuardianId(hit);
            if (!minimalMonsterPresentation || !IsDirectedGuardian(id)) return false;
            if (hit.HasTargetHits)
            {
                bool started = false;
                foreach (var targetHit in hit.TargetHits)
                    started |= TryStartGuardianDirectedAttack(new BattleHitInfo(hit.TargetIsPlayer, targetHit.Damage,
                        hit.IsCritical, hit.IsSkill, hit.CausesKnockback, targetHit.TargetIndex, hit.AttackerIndex,
                        hit.PresentationDelay, targetHit.TargetRuntimeId, hit.AttackerRuntimeId));
                return started;
            }
            if (minimalCanvasRoot == null) EnsureMinimalCanvas();
            EnsureRangedEffectRoot();
            ArrangeBattleEffectLayers();
            var source = hit.TargetIsPlayer ? enemyPreviewImages : allyPreviewImages;
            var targets = hit.TargetIsPlayer ? allyPreviewImages : enemyPreviewImages;
            if (hit.AttackerIndex < 0 || hit.AttackerIndex >= source.Count || source[hit.AttackerIndex] == null ||
                hit.TargetIndex < 0 || hit.TargetIndex >= targets.Count || targets[hit.TargetIndex] == null) return false;
            Image attacker = source[hit.AttackerIndex];
            Image target = targets[hit.TargetIndex];
            if (!TryGetGuardianMouthPosition(attacker, id, out Vector2 start) ||
                !TryGetCanvasLocalCenter(target.rectTransform, out Vector2 end)) return false;

            var shot = new GuardianDirectedAttack
            {
                HitInfo = hit, GuardianId = id, StartPosition = start, EndPosition = end,
                ImpactTime = GuardianDirectedImpactDelay(id),
                LethalAtResolution = hit.TargetIsPlayer
                    ? stateMachine.Simulator.GetAllyCurrentHp(hit.TargetIndex) <= 0
                    : stateMachine.Simulator.GetEnemyCurrentHp(hit.TargetIndex) <= 0,
                Charge = CreateGuardianDirectedImage(id, "Charge"),
                Projectile = CreateGuardianDirectedImage(id, "Projectile"),
                Impact = CreateGuardianDirectedImage(id, "Impact"),
                DefeatedTarget = CreateGuardianTargetSnapshot(target)
            };
            shot.RetainedTargetIdleFrames = hit.TargetIsPlayer
                ? (hit.TargetIndex >= 0 && hit.TargetIndex < allyIdleSprites.Count ? allyIdleSprites[hit.TargetIndex] : null)
                : ResolveEnemyPreviewVisualData(stateMachine.Simulator, hit.TargetIndex)?.IdleSprites;
            shot.RetainedTargetAnimationTime = battlePresentationClock + hit.TargetIndex * .09f;
            if (shot.DefeatedTarget != null)
            {
                shot.DefeatedTargetSize = shot.DefeatedTarget.rectTransform.sizeDelta;
                shot.DefeatedTargetScale = shot.DefeatedTarget.rectTransform.localScale;
            }
            for (int i = 0; i < GuardianDirectedTrailCount; i++) shot.Trail.Add(CreateGuardianDirectedImage(id, "Trail"));
            // Trails sit behind the travelling head, and the impact owns the foreground.
            shot.Projectile?.transform.SetAsLastSibling();
            shot.Impact?.transform.SetAsLastSibling();
            activeGuardianDirectedAttacks.Add(shot);
            RenderGuardianDirectedAttack(shot);
            return true;
        }

        private bool TryGetGuardianMouthPosition(Image attacker, string id, out Vector2 position)
        {
            position = Vector2.zero;
            if (attacker == null || minimalCanvasRoot == null) return false;
            var rect = attacker.rectTransform;
            // Art sockets are measured against the fixed 384px body canvas, not
            // against the opaque bounds or the length of a previous breath frame.
            Vector2 socket = id == "suzaku" ? new Vector2(.3333333f, .1770833f) : new Vector2(.3802083f, .1223958f);
            float bodySize = rect.rect.height;
            Vector3 world = rect.TransformPoint(new Vector3(socket.x * bodySize, socket.y * bodySize, 0f));
            position = ((RectTransform)minimalCanvasRoot.transform).InverseTransformPoint(world);
            return true;
        }

        private Image CreateGuardianDirectedImage(string id, string part)
        {
            Sprite sprite = GuardianSprite("DirectedAttack/" + GuardianArtName(id) + "_" + part);
            var node = new GameObject("GuardianDirected" + part, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(rangedEffectRoot.transform, false);
            var image = node.GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.pivot = sprite != null
                ? new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height) : new Vector2(.5f, .5f);
            image.color = Color.clear;
            return image;
        }

        private Image CreateGuardianTargetSnapshot(Image source)
        {
            if (source == null || !TryGetCanvasLocalCenter(source.rectTransform, out Vector2 center)) return null;
            var node = new GameObject("GuardianDirectedDefeatedTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(rangedEffectRoot.transform, false);
            var copy = node.GetComponent<Image>();
            copy.sprite = source.sprite; copy.preserveAspect = true; copy.raycastTarget = false;
            var rect = copy.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = center;
            var canvas = (RectTransform)minimalCanvasRoot.transform;
            float scale = Mathf.Abs(source.rectTransform.lossyScale.y / canvas.lossyScale.y);
            rect.sizeDelta = source.rectTransform.rect.size * scale;
            rect.localScale = new Vector3(Mathf.Sign(source.rectTransform.lossyScale.x), 1f, 1f);
            rect.localRotation = source.rectTransform.localRotation;
            node.SetActive(false);
            return copy;
        }

        private static Vector2 SampleGuardianDirectedPath(string id, Vector2 start, Vector2 target, float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (id != "suzaku") return Vector2.Lerp(start, target, t);
            // A tall quadratic arc launches upward and lands from above. The
            // peak follows the target; it is never a horizontal breath sprite.
            Vector2 control = new Vector2(Mathf.Lerp(start.x, target.x, .55f), Mathf.Max(start.y, target.y) + 265f);
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * target;
        }

        private void UpdateGuardianDirectedAttacks(float dt)
        {
            for (int i = activeGuardianDirectedAttacks.Count - 1; i >= 0; i--)
            {
                var shot = activeGuardianDirectedAttacks[i];
                shot.Elapsed += Mathf.Max(0f, dt);
                RefreshGuardianDirectedTarget(shot);
                if (!shot.ImpactApplied && shot.Elapsed >= shot.ImpactTime)
                {
                    shot.ImpactApplied = true;
                    ApplyGuardianDirectedImpact(shot);
                }
                RenderGuardianDirectedAttack(shot);
                if (shot.Elapsed < shot.ImpactTime + GuardianDirectedImpactDuration) continue;
                DestroyGuardianDirectedAttack(shot);
                activeGuardianDirectedAttacks.RemoveAt(i);
            }
        }

        private void RefreshGuardianDirectedTarget(GuardianDirectedAttack shot)
        {
            if (shot.ImpactApplied || shot.TargetRemoved) return;
            int targetIndex = ResolveGuardianDirectedTargetIndex(shot);
            var images = shot.HitInfo.TargetIsPlayer ? allyPreviewImages : enemyPreviewImages;
            if (targetIndex < 0 || targetIndex >= images.Count || images[targetIndex] == null) return;
            Image target = images[targetIndex];
            if (TryGetCanvasLocalCenter(target.rectTransform, out Vector2 end)) shot.EndPosition = end;
            if (shot.DefeatedTarget != null)
            {
                shot.DefeatedTarget.sprite = target.sprite;
                shot.DefeatedTarget.rectTransform.anchoredPosition = shot.EndPosition;
            }
        }

        private int ResolveGuardianDirectedTargetIndex(GuardianDirectedAttack shot)
        {
            var simulator = stateMachine?.Simulator;
            if (simulator == null) return -1;
            if (shot.HitInfo.TargetIsPlayer)
                return simulator.GetAllyRuntimeId(shot.HitInfo.TargetIndex) == shot.HitInfo.TargetRuntimeId || shot.HitInfo.TargetRuntimeId < 0
                    ? shot.HitInfo.TargetIndex : -1;
            return shot.HitInfo.TargetRuntimeId >= 0
                ? simulator.FindEnemyIndexByRuntimeId(shot.HitInfo.TargetRuntimeId) : shot.HitInfo.TargetIndex;
        }

        private void ApplyGuardianDirectedImpact(GuardianDirectedAttack shot)
        {
            int targetIndex = ResolveGuardianDirectedTargetIndex(shot);
            var hit = shot.HitInfo;
            bool lethalStillInFlight = false;
            foreach (var other in activeGuardianDirectedAttacks)
                if (other != shot && other.HitInfo.TargetIsPlayer == hit.TargetIsPlayer &&
                    other.HitInfo.TargetRuntimeId == hit.TargetRuntimeId && other.LethalAtResolution && !other.ImpactApplied)
                    lethalStillInFlight = true;
            if (hit.TargetIsPlayer && !lethalStillInFlight && guardianHeldAllyDefeats.Remove(hit.TargetIndex))
            {
                guardianCompletingAllyDefeat = true;
                HandleAllyDefeated(hit.TargetIndex);
                guardianCompletingAllyDefeat = false;
            }
            AudioManager.Instance?.PlaySe(hit.IsCritical ? AudioCue.CriticalHit : AudioCue.Hit);
            visualHitStopRemaining = Mathf.Max(visualHitStopRemaining, ResolveVisualHitStopDuration(hit));
            if (targetIndex >= 0 && !shot.TargetRemoved)
            {
                var contact = new BattleHitInfo(hit.TargetIsPlayer, hit.Damage, hit.IsCritical, hit.IsSkill,
                    hit.CausesKnockback, targetIndex, hit.AttackerIndex, 0f, hit.TargetRuntimeId, hit.AttackerRuntimeId);
                ApplyTargetHitFlash(contact);
                ApplyTargetKnockback(contact);
            }
            // Use the shared damage-label setting, with at most one label at contact.
            if (hit.Damage > 0)
                SpawnFloatingDamageTextAtPosition(hit.TargetIsPlayer, hit.Damage, hit.IsCritical, 0, 1, shot.EndPosition);
            if (shot.OwnsDefeat) AudioManager.Instance?.PlaySe(AudioCue.EnemyDefeat);
        }

        private void RenderGuardianDirectedAttack(GuardianDirectedAttack shot)
        {
            float t = shot.Elapsed;
            bool suzaku = shot.GuardianId == "suzaku";
            float strength = shot.HitInfo.IsSkill ? 1.28f : 1f;
            float chargeT = Mathf.Clamp01(t / GuardianDirectedChargeDuration);
            float chargeAlpha = t < GuardianDirectedChargeDuration ? Mathf.Lerp(.25f, 1f, chargeT) :
                Mathf.Clamp01(1f - (t - GuardianDirectedChargeDuration) / .13f);
            PlaceGuardianDirectedImage(shot.Charge, shot.StartPosition, (90f + 75f * chargeT) * strength, chargeAlpha, 0f);
            float flightT = Mathf.Clamp01((t - GuardianDirectedChargeDuration) / (shot.ImpactTime - GuardianDirectedChargeDuration));
            bool inFlight = t >= GuardianDirectedChargeDuration && t < shot.ImpactTime;
            Vector2 point = SampleGuardianDirectedPath(shot.GuardianId, shot.StartPosition, shot.EndPosition, flightT);
            Vector2 previous = SampleGuardianDirectedPath(shot.GuardianId, shot.StartPosition, shot.EndPosition, Mathf.Max(0f, flightT - .02f));
            Vector2 aheadOfHead = SampleGuardianDirectedPath(shot.GuardianId, shot.StartPosition, shot.EndPosition, Mathf.Min(1f, flightT + .02f));
            Vector2 tangent = aheadOfHead - previous;
            if (tangent.sqrMagnitude < .01f) tangent = shot.EndPosition - shot.StartPosition;
            float rotation = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            PlaceGuardianDirectedImage(shot.Projectile, point, (suzaku ? 155f : 190f) * strength, inFlight ? 1f : 0f, rotation);
            for (int i = 0; i < shot.Trail.Count; i++)
            {
                // Independent painted trail pieces preserve the art proportions.
                // Cyan fills the path behind its moving head; fire traces the arc.
                float behind = (i + 1f) / (shot.Trail.Count + 1f);
                float sample = suzaku ? flightT - behind * .30f : flightT * (1f - behind);
                Vector2 pos = SampleGuardianDirectedPath(shot.GuardianId, shot.StartPosition, shot.EndPosition, sample);
                Vector2 ahead = SampleGuardianDirectedPath(shot.GuardianId, shot.StartPosition, shot.EndPosition, sample + .015f);
                Vector2 direction = ahead - pos;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float alpha = inFlight && sample >= 0f ? (suzaku ? .75f * (1f - behind) : .78f) : 0f;
                PlaceGuardianDirectedImage(shot.Trail[i], pos, (suzaku ? 130f : 155f) * strength * (1f - behind * .22f), alpha, angle);
            }
            float impactT = Mathf.Clamp01((t - shot.ImpactTime) / GuardianDirectedImpactDuration);
            // The first impact frame is already bright: damage and contact share a frame.
            float impactAlpha = shot.ImpactApplied ? 1f - Mathf.SmoothStep(.15f, 1f, impactT) : 0f;
            Vector2 impactPosition = shot.EndPosition;
            PlaceGuardianDirectedImage(shot.Impact, impactPosition, (suzaku ? 355f : 310f) * strength * Mathf.Lerp(.82f, 1.08f, impactT), impactAlpha, 0f);
            if (shot.DefeatedTarget != null && shot.OwnsDefeat)
            {
                // The combat target is already removed, but remains visible until
                // projectile contact. Continue its existing idle drawings rather
                // than showing a frozen screenshot (or inventing extra attacks).
                if (!shot.ImpactApplied && shot.RetainedTargetIdleFrames != null && shot.RetainedTargetIdleFrames.Count > 1)
                {
                    int frame = Mathf.FloorToInt((shot.RetainedTargetAnimationTime + shot.Elapsed) * 4f)
                        % shot.RetainedTargetIdleFrames.Count;
                    shot.DefeatedTarget.sprite = shot.RetainedTargetIdleFrames[frame];
                }
                shot.DefeatedTarget.gameObject.SetActive(true);
                shot.DefeatedTarget.rectTransform.anchoredPosition = shot.EndPosition + Vector2.up * (impactT * 14f);
                shot.DefeatedTarget.rectTransform.localScale = shot.DefeatedTargetScale * Mathf.Lerp(1f, .82f, impactT);
                shot.DefeatedTarget.color = shot.ImpactApplied ?
                    new Color(1f, Mathf.Lerp(.55f, 1f, impactT), .65f, 1f - impactT) : Color.white;
            }
        }

        private static void PlaceGuardianDirectedImage(Image image, Vector2 position, float size, float alpha, float rotation)
        {
            if (image == null) return;
            image.gameObject.SetActive(alpha > .001f && image.sprite != null);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = Vector2.one * size;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private bool TryDeferGuardianEnemyDefeat(int runtimeId)
        {
            if (runtimeId < 0) return false;
            GuardianDirectedAttack owner = null;
            foreach (var shot in activeGuardianDirectedAttacks)
                if (!shot.HitInfo.TargetIsPlayer && !shot.ImpactApplied && shot.HitInfo.TargetRuntimeId == runtimeId &&
                    (owner == null || (shot.LethalAtResolution && !owner.LethalAtResolution) ||
                     (shot.LethalAtResolution == owner.LethalAtResolution && shot.Elapsed > owner.Elapsed))) owner = shot;
            if (owner == null) return false;
            foreach (var shot in activeGuardianDirectedAttacks)
                if (!shot.HitInfo.TargetIsPlayer && shot.HitInfo.TargetRuntimeId == runtimeId) shot.TargetRemoved = true;
            owner.OwnsDefeat = true;
            RenderGuardianDirectedAttack(owner);
            return true;
        }

        private bool TryDeferGuardianAllyDefeat(int slot)
        {
            if (guardianCompletingAllyDefeat) return false;
            foreach (var shot in activeGuardianDirectedAttacks)
                if (shot.HitInfo.TargetIsPlayer && shot.HitInfo.TargetIndex == slot && !shot.ImpactApplied)
                { guardianHeldAllyDefeats.Add(slot); return true; }
            return false;
        }

        private int GuardianPendingDisplayedDamage(bool targetIsPlayer, int runtimeId)
        {
            int damage = 0;
            foreach (var shot in activeGuardianDirectedAttacks)
                if (!shot.ImpactApplied && shot.HitInfo.TargetIsPlayer == targetIsPlayer && shot.HitInfo.TargetRuntimeId == runtimeId)
                    damage += shot.HitInfo.Damage;
            return damage;
        }

        private bool IsGuardianAllyDefeatHeld(int slot) => guardianHeldAllyDefeats.Contains(slot);

        private void ClearGuardianDirectedAttacks()
        {
            foreach (var shot in activeGuardianDirectedAttacks) DestroyGuardianDirectedAttack(shot);
            activeGuardianDirectedAttacks.Clear();
            guardianHeldAllyDefeats.Clear();
            guardianPendingPresentationResult = BattleResult.None;
        }

        private static void DestroyGuardianDirectedAttack(GuardianDirectedAttack shot)
        {
            DestroyGuardianDirectedImage(shot.Charge); DestroyGuardianDirectedImage(shot.Projectile);
            DestroyGuardianDirectedImage(shot.Impact); DestroyGuardianDirectedImage(shot.DefeatedTarget);
            foreach (var image in shot.Trail) DestroyGuardianDirectedImage(image);
        }

        private static void DestroyGuardianDirectedImage(Image image)
        {
            if (image == null) return;
            image.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(image.gameObject); else DestroyImmediate(image.gameObject);
        }
    }
}
