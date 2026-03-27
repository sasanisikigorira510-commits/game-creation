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
            hudController.ShowFloor(floor);
            hudController.ShowEncounterReadout(floor, simulator.PlayerStats, simulator.EnemyStats);
            hudController.UpdateHp(simulator.PlayerStats, simulator.EnemyStats);
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
            hudController.UpdateHp(simulator.PlayerStats, simulator.EnemyStats);
             RefreshSkillHud();
            return result;
        }

        public void UseSkill(BattleSkillType skillType)
        {
            if (CurrentState != BattleFlowState.Fighting)
            {
                return;
            }

            simulator.TryUseSkill(skillType);
            hudController.UpdateHp(simulator.PlayerStats, simulator.EnemyStats);
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
            if (feedbackController != null)
            {
                feedbackController.ShowHit(hitInfo);
            }

            hudController.UpdateHp(simulator.PlayerStats, simulator.EnemyStats);
        }

        private void HandleEncounterChanged()
        {
            if (hudController == null || simulator == null)
            {
                return;
            }

            hudController.ShowFloor(simulator.CurrentFloor);
            hudController.ShowEncounterReadout(simulator.CurrentFloor, simulator.PlayerStats, simulator.EnemyStats);
            hudController.UpdateHp(simulator.PlayerStats, simulator.EnemyStats);
        }

        private void SetState(BattleFlowState nextState)
        {
            CurrentState = nextState;
        }
    }
}
