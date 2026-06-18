using System.Collections.Generic;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed class BattleStateMachine : MonoBehaviour
    {
        [SerializeField] private BattleHudController hudController;
        [SerializeField] private BattleSimulator simulator;
        [SerializeField] private BattleFeedbackController feedbackController;

        public BattleFlowState CurrentState { get; private set; }
        public BattleSimulator Simulator => simulator;
        private readonly List<PendingPresentedHit> pendingPresentedHits = new List<PendingPresentedHit>();
        private readonly List<int> displayedAllyHpBySlot = new List<int>();
        private readonly List<int> displayedEnemyHpByIndex = new List<int>();
        private int displayedPlayerHp;
        private int displayedEnemyHp;
        private BattleResult pendingResult;

        private struct PendingPresentedHit
        {
            public BattleHitInfo HitInfo;
            public float RemainingDelay;
        }

        private void OnEnable()
        {
            if (simulator != null)
            {
                simulator.HitResolved += HandleHitResolved;
                simulator.EncounterChanged += HandleEncounterChanged;
            }
        }

        private void OnDisable()
        {
            if (simulator != null)
            {
                simulator.HitResolved -= HandleHitResolved;
                simulator.EncounterChanged -= HandleEncounterChanged;
            }
        }

        public void Begin(int floor)
        {
            SetState(BattleFlowState.Init);
            simulator.Setup(floor);
            pendingPresentedHits.Clear();
            pendingResult = BattleResult.None;
            SyncDisplayedHpToActual();
            hudController.ShowFloor(floor);
            hudController.ShowEncounterReadout(floor, simulator.PlayerStats, simulator.EnemyStats);
            UpdateDisplayedHpHud();
            RefreshSkillHud();
            hudController.HideResultPanel();
            SetState(BattleFlowState.Ready);
            SetState(BattleFlowState.Fighting);
        }

        public void ShowResult(bool isWin)
        {
            SetState(BattleFlowState.Result);
            hudController.SetSkillButtonsInteractable(false);
            hudController.ShowResult(isWin);
        }

        public BattleResult Tick(float deltaTime)
        {
            if (CurrentState != BattleFlowState.Fighting)
            {
                return BattleResult.None;
            }

            if (pendingResult != BattleResult.None)
            {
                TickPendingPresentedHits(deltaTime);
                UpdateDisplayedHpHud();
                RefreshSkillHud();

                if (pendingPresentedHits.Count > 0)
                {
                    return BattleResult.None;
                }

                BattleResult resolvedPendingResult = pendingResult;
                pendingResult = BattleResult.None;
                return resolvedPendingResult;
            }

            BattleResult result = simulator.Tick(deltaTime);
            TickPendingPresentedHits(deltaTime);
            UpdateDisplayedHpHud();
            RefreshSkillHud();
            if (result != BattleResult.None && pendingPresentedHits.Count > 0)
            {
                pendingResult = result;
                return BattleResult.None;
            }

            return result;
        }

        public void TickPreparation(float deltaTime)
        {
            if (CurrentState != BattleFlowState.Fighting || simulator == null)
            {
                return;
            }

            simulator.TickPreparation(deltaTime);
            TickPendingPresentedHits(deltaTime);
            UpdateDisplayedHpHud();
            RefreshSkillHud();
        }

        public void SetEngagedEnemyCount(int count)
        {
            if (simulator == null)
            {
                return;
            }

            simulator.SetEngagedEnemyCount(count);
        }

        public void UseSkill(BattleSkillType skillType)
        {
            if (CurrentState != BattleFlowState.Fighting)
            {
                return;
            }

            simulator.TryUseSkill(skillType);
            UpdateDisplayedHpHud();
            RefreshSkillHud();
        }

        private void RefreshSkillHud()
        {
            hudController.UpdateSkillCooldowns(
                simulator.GetSkillState(BattleSkillType.Strike),
                simulator.GetSkillState(BattleSkillType.Drain),
                simulator.GetSkillState(BattleSkillType.Guard));
        }

        public void ShowResultPanel(BattleResultViewData viewData)
        {
            SetState(BattleFlowState.Result);
            hudController.SetSkillButtonsInteractable(false);
            hudController.ShowResultPanel(viewData);
        }

        private void HandleHitResolved(BattleHitInfo hitInfo)
        {
            if (hitInfo.PresentationDelay > 0f)
            {
                pendingPresentedHits.Add(new PendingPresentedHit
                {
                    HitInfo = hitInfo,
                    RemainingDelay = hitInfo.PresentationDelay
                });
                return;
            }

            PresentHit(hitInfo);
        }

        private void HandleEncounterChanged()
        {
            if (hudController == null || simulator == null)
            {
                return;
            }

            hudController.ShowFloor(simulator.CurrentFloor);
            hudController.ShowEncounterReadout(simulator.CurrentFloor, simulator.PlayerStats, simulator.EnemyStats);
            pendingPresentedHits.Clear();
            pendingResult = BattleResult.None;
            SyncDisplayedHpToActual();
            UpdateDisplayedHpHud();
        }

        private void TickPendingPresentedHits(float deltaTime)
        {
            for (int i = pendingPresentedHits.Count - 1; i >= 0; i -= 1)
            {
                PendingPresentedHit pending = pendingPresentedHits[i];
                pending.RemainingDelay -= deltaTime;
                if (pending.RemainingDelay > 0f)
                {
                    pendingPresentedHits[i] = pending;
                    continue;
                }

                PresentHit(pending.HitInfo);
                pendingPresentedHits.RemoveAt(i);
            }
        }

        private void PresentHit(BattleHitInfo hitInfo)
        {
            if (feedbackController != null)
            {
                feedbackController.ShowHit(hitInfo);
            }

            ApplyDisplayedUnitDamage(hitInfo);

            if (hitInfo.TargetIsPlayer)
            {
                displayedPlayerHp = Mathf.Max(0, displayedPlayerHp - hitInfo.Damage);
            }
            else
            {
                displayedEnemyHp = Mathf.Max(0, displayedEnemyHp - hitInfo.Damage);
            }

            ClampDisplayedHpToActualBounds();
            UpdateDisplayedHpHud();
        }

        public int GetDisplayedAllyCurrentHp(int index)
        {
            if (simulator == null || !simulator.HasAllyRuntime(index))
            {
                return 0;
            }

            EnsureDisplayedAllyHpCapacity();
            return index >= 0 && index < displayedAllyHpBySlot.Count
                ? Mathf.Clamp(displayedAllyHpBySlot[index], simulator.GetAllyCurrentHp(index), simulator.GetAllyMaxHp(index))
                : simulator.GetAllyCurrentHp(index);
        }

        public int GetDisplayedEnemyCurrentHp(int index)
        {
            if (simulator == null || !simulator.HasEnemyRuntime(index))
            {
                return 0;
            }

            EnsureDisplayedEnemyHpCapacity();
            return index >= 0 && index < displayedEnemyHpByIndex.Count
                ? Mathf.Clamp(displayedEnemyHpByIndex[index], simulator.GetEnemyCurrentHp(index), simulator.GetEnemyMaxHp(index))
                : simulator.GetEnemyCurrentHp(index);
        }

        private void ApplyDisplayedUnitDamage(BattleHitInfo hitInfo)
        {
            if (hitInfo.TargetIsPlayer)
            {
                EnsureDisplayedAllyHpCapacity();
                ApplyDisplayedDamage(displayedAllyHpBySlot, hitInfo);
                return;
            }

            EnsureDisplayedEnemyHpCapacity();
            ApplyDisplayedDamage(displayedEnemyHpByIndex, hitInfo);
        }

        private static void ApplyDisplayedDamage(List<int> displayedHpValues, BattleHitInfo hitInfo)
        {
            if (displayedHpValues == null)
            {
                return;
            }

            if (hitInfo.HasTargetHits)
            {
                for (int i = 0; i < hitInfo.TargetHits.Count; i += 1)
                {
                    BattleHitTargetInfo targetHit = hitInfo.TargetHits[i];
                    ApplyDisplayedDamage(displayedHpValues, targetHit.TargetIndex, targetHit.Damage);
                }

                return;
            }

            ApplyDisplayedDamage(displayedHpValues, hitInfo.TargetIndex, hitInfo.Damage);
        }

        private static void ApplyDisplayedDamage(List<int> displayedHpValues, int targetIndex, int damage)
        {
            if (targetIndex < 0 || targetIndex >= displayedHpValues.Count || damage <= 0)
            {
                return;
            }

            displayedHpValues[targetIndex] = Mathf.Max(0, displayedHpValues[targetIndex] - damage);
        }

        private void SyncDisplayedHpToActual()
        {
            displayedPlayerHp = simulator != null && simulator.PlayerStats != null ? simulator.PlayerStats.CurrentHp : 0;
            displayedEnemyHp = simulator != null && simulator.EnemyStats != null ? simulator.EnemyStats.CurrentHp : 0;
            SyncDisplayedAllyHpToActual();
            SyncDisplayedEnemyHpToActual();
        }

        private void ClampDisplayedHpToActualBounds()
        {
            if (simulator?.PlayerStats != null)
            {
                displayedPlayerHp = Mathf.Clamp(displayedPlayerHp, simulator.PlayerStats.CurrentHp, simulator.PlayerStats.MaxHp);
            }

            if (simulator?.EnemyStats != null)
            {
                displayedEnemyHp = Mathf.Clamp(displayedEnemyHp, simulator.EnemyStats.CurrentHp, simulator.EnemyStats.MaxHp);
            }

            ClampDisplayedAllyHpToActualBounds();
            ClampDisplayedEnemyHpToActualBounds();
        }

        private void SyncDisplayedAllyHpToActual()
        {
            displayedAllyHpBySlot.Clear();
            if (simulator == null)
            {
                return;
            }

            int allyCount = Mathf.Max(0, simulator.CurrentAllyRuntimeCount);
            for (int i = 0; i < allyCount; i += 1)
            {
                displayedAllyHpBySlot.Add(simulator.HasAllyRuntime(i) ? simulator.GetAllyCurrentHp(i) : 0);
            }
        }

        private void SyncDisplayedEnemyHpToActual()
        {
            displayedEnemyHpByIndex.Clear();
            if (simulator == null)
            {
                return;
            }

            int enemyCount = Mathf.Max(0, simulator.CurrentActiveEnemyCount);
            for (int i = 0; i < enemyCount; i += 1)
            {
                displayedEnemyHpByIndex.Add(simulator.HasEnemyRuntime(i) ? simulator.GetEnemyCurrentHp(i) : 0);
            }
        }

        private void EnsureDisplayedAllyHpCapacity()
        {
            if (simulator == null)
            {
                displayedAllyHpBySlot.Clear();
                return;
            }

            int allyCount = Mathf.Max(0, simulator.CurrentAllyRuntimeCount);
            while (displayedAllyHpBySlot.Count < allyCount)
            {
                int index = displayedAllyHpBySlot.Count;
                displayedAllyHpBySlot.Add(simulator.HasAllyRuntime(index) ? simulator.GetAllyCurrentHp(index) : 0);
            }

            if (displayedAllyHpBySlot.Count > allyCount)
            {
                displayedAllyHpBySlot.RemoveRange(allyCount, displayedAllyHpBySlot.Count - allyCount);
            }
        }

        private void EnsureDisplayedEnemyHpCapacity()
        {
            if (simulator == null)
            {
                displayedEnemyHpByIndex.Clear();
                return;
            }

            int enemyCount = Mathf.Max(0, simulator.CurrentActiveEnemyCount);
            while (displayedEnemyHpByIndex.Count < enemyCount)
            {
                int index = displayedEnemyHpByIndex.Count;
                displayedEnemyHpByIndex.Add(simulator.HasEnemyRuntime(index) ? simulator.GetEnemyCurrentHp(index) : 0);
            }

            if (displayedEnemyHpByIndex.Count > enemyCount)
            {
                displayedEnemyHpByIndex.RemoveRange(enemyCount, displayedEnemyHpByIndex.Count - enemyCount);
            }
        }

        private void ClampDisplayedAllyHpToActualBounds()
        {
            EnsureDisplayedAllyHpCapacity();
            for (int i = 0; i < displayedAllyHpBySlot.Count; i += 1)
            {
                if (!simulator.HasAllyRuntime(i))
                {
                    displayedAllyHpBySlot[i] = 0;
                    continue;
                }

                displayedAllyHpBySlot[i] = Mathf.Clamp(
                    displayedAllyHpBySlot[i],
                    simulator.GetAllyCurrentHp(i),
                    simulator.GetAllyMaxHp(i));
            }
        }

        private void ClampDisplayedEnemyHpToActualBounds()
        {
            EnsureDisplayedEnemyHpCapacity();
            for (int i = 0; i < displayedEnemyHpByIndex.Count; i += 1)
            {
                if (!simulator.HasEnemyRuntime(i))
                {
                    displayedEnemyHpByIndex[i] = 0;
                    continue;
                }

                displayedEnemyHpByIndex[i] = Mathf.Clamp(
                    displayedEnemyHpByIndex[i],
                    simulator.GetEnemyCurrentHp(i),
                    simulator.GetEnemyMaxHp(i));
            }
        }

        private void UpdateDisplayedHpHud()
        {
            if (hudController == null || simulator == null)
            {
                return;
            }

            BattleUnitStats playerStats = simulator.PlayerStats;
            BattleUnitStats enemyStats = simulator.EnemyStats;
            if (playerStats == null || enemyStats == null)
            {
                hudController.UpdateHp(playerStats, enemyStats);
                return;
            }

            ClampDisplayedHpToActualBounds();
            hudController.UpdateHp(displayedPlayerHp, playerStats.MaxHp, displayedEnemyHp, enemyStats.MaxHp);
        }

        private void SetState(BattleFlowState nextState)
        {
            CurrentState = nextState;
        }
    }
}
