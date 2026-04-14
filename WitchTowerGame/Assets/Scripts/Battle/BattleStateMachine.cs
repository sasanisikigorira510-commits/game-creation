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
        private int displayedPlayerHp;
        private int displayedEnemyHp;

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

            var result = simulator.Tick(deltaTime);
            TickPendingPresentedHits(deltaTime);
            UpdateDisplayedHpHud();
            RefreshSkillHud();
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

        private void SyncDisplayedHpToActual()
        {
            displayedPlayerHp = simulator != null && simulator.PlayerStats != null ? simulator.PlayerStats.CurrentHp : 0;
            displayedEnemyHp = simulator != null && simulator.EnemyStats != null ? simulator.EnemyStats.CurrentHp : 0;
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
