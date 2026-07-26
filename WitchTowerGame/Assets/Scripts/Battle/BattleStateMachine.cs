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
        public int DisplayedWaveEnemyHp => displayedWaveEnemyHp;
        public int DisplayedWaveEnemyMaxHp => displayedWaveEnemyMaxHp;
        public float DisplayedWaveEnemyRatio => displayedWaveEnemyMaxHp > 0
            ? Mathf.Clamp01((float)displayedWaveEnemyHp / displayedWaveEnemyMaxHp)
            : 0f;
        public int DebugDisplayedWaveEnemyHp => DisplayedWaveEnemyHp;
        public int DebugDisplayedWaveEnemyMaxHp => DisplayedWaveEnemyMaxHp;
        public float DebugDisplayedWaveEnemyRatio => DisplayedWaveEnemyRatio;
        private readonly List<PendingPresentedHit> pendingPresentedHits = new List<PendingPresentedHit>();
        private readonly List<int> displayedAllyHpBySlot = new List<int>();
        private readonly List<int> displayedEnemyHpByIndex = new List<int>();
        private int displayedPlayerHp;
        private int displayedEnemyHp;
        private int displayedWaveEnemyHp;
        private int displayedWaveEnemyMaxHp;
        private int displayedWaveEnemyFloor;
        private int displayedWaveEnemyWave;
        private int displayedWaveEnemyEncounterSerial;
        private bool hasDisplayedWaveEnemyHp;
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
            ResetDisplayedWaveEnemyHp();
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

        public bool InvokeSpirit(BattleSpiritType spiritType)
        {
            if (CurrentState != BattleFlowState.Fighting || simulator == null)
            {
                return false;
            }

            bool invoked = simulator.TryInvokeSpirit(spiritType);
            if (!invoked)
            {
                return false;
            }

            SyncDisplayedHpToActual();
            UpdateDisplayedHpHud();
            RefreshSkillHud();
            return true;
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
            int actualHp = simulator.GetAllyCurrentHp(index);
            int maxHp = simulator.GetAllyMaxHp(index);
            return index >= 0 && index < displayedAllyHpBySlot.Count
                ? ResolveHpBarCurrentHp(displayedAllyHpBySlot[index], actualHp, maxHp)
                : actualHp;
        }

        public int GetDisplayedEnemyCurrentHp(int index)
        {
            if (simulator == null || !simulator.HasEnemyRuntime(index))
            {
                return 0;
            }

            EnsureDisplayedEnemyHpCapacity();
            int actualHp = simulator.GetEnemyCurrentHp(index);
            int maxHp = simulator.GetEnemyMaxHp(index);
            return index >= 0 && index < displayedEnemyHpByIndex.Count
                ? ResolveHpBarCurrentHp(displayedEnemyHpByIndex[index], actualHp, maxHp)
                : actualHp;
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
            if (playerStats == null)
            {
                hudController.UpdateHp(playerStats, enemyStats);
                return;
            }

            ClampDisplayedHpToActualBounds();
            int playerHpForBar = ResolveHpBarCurrentHp(displayedPlayerHp, playerStats.CurrentHp, playerStats.MaxHp);
            int enemyMaxHpForBar = simulator.CurrentWaveEnemyMaxHp;
            int enemyActualHpForBar = simulator.CurrentWaveEnemyCurrentHp;
            if (enemyMaxHpForBar <= 0 && enemyStats != null)
            {
                enemyMaxHpForBar = enemyStats.MaxHp;
                enemyActualHpForBar = enemyStats.CurrentHp;
            }

            int enemyHpForBar = ResolveDisplayedWaveEnemyHp(enemyActualHpForBar, enemyMaxHpForBar);
            hudController.UpdateHp(playerHpForBar, playerStats.MaxHp, enemyHpForBar, enemyMaxHpForBar);
        }

        private static int ResolveHpBarCurrentHp(int displayedHp, int actualHp, int maxHp)
        {
            return Mathf.Clamp(Mathf.Min(displayedHp, actualHp), 0, Mathf.Max(0, maxHp));
        }

        private void ResetDisplayedWaveEnemyHp()
        {
            displayedWaveEnemyHp = 0;
            displayedWaveEnemyMaxHp = 0;
            displayedWaveEnemyFloor = simulator != null ? simulator.CurrentFloor : 0;
            displayedWaveEnemyWave = simulator != null ? simulator.CurrentWave : 0;
            displayedWaveEnemyEncounterSerial = simulator != null ? simulator.EncounterSerial : 0;
            hasDisplayedWaveEnemyHp = false;
        }

        private int ResolveDisplayedWaveEnemyHp(int actualHp, int maxHp)
        {
            int clampedMaxHp = Mathf.Max(0, maxHp);
            int clampedActualHp = Mathf.Clamp(actualHp, 0, clampedMaxHp);
            if (clampedMaxHp <= 0)
            {
                ResetDisplayedWaveEnemyHp();
                return 0;
            }

            int currentFloor = simulator != null ? simulator.CurrentFloor : 0;
            int currentWave = simulator != null ? simulator.CurrentWave : 0;
            int currentEncounterSerial = simulator != null ? simulator.EncounterSerial : 0;
            bool waveChanged = !hasDisplayedWaveEnemyHp ||
                displayedWaveEnemyFloor != currentFloor ||
                displayedWaveEnemyWave != currentWave;
            if (waveChanged)
            {
                displayedWaveEnemyHp = clampedActualHp;
                displayedWaveEnemyMaxHp = clampedMaxHp;
                displayedWaveEnemyFloor = currentFloor;
                displayedWaveEnemyWave = currentWave;
                displayedWaveEnemyEncounterSerial = currentEncounterSerial;
                hasDisplayedWaveEnemyHp = true;
                return displayedWaveEnemyHp;
            }

            float actualRatio = clampedMaxHp > 0 ? (float)clampedActualHp / clampedMaxHp : 0f;
            float previousRatio = displayedWaveEnemyMaxHp > 0
                ? (float)Mathf.Clamp(displayedWaveEnemyHp, 0, displayedWaveEnemyMaxHp) / displayedWaveEnemyMaxHp
                : actualRatio;
            bool encounterChanged = displayedWaveEnemyEncounterSerial != currentEncounterSerial;
            float resolvedRatio = encounterChanged ? Mathf.Min(actualRatio, previousRatio) : actualRatio;
            displayedWaveEnemyHp = Mathf.Min(
                clampedActualHp,
                Mathf.Clamp(Mathf.FloorToInt(resolvedRatio * clampedMaxHp), 0, clampedMaxHp));
            displayedWaveEnemyMaxHp = clampedMaxHp;
            displayedWaveEnemyFloor = currentFloor;
            displayedWaveEnemyWave = currentWave;
            displayedWaveEnemyEncounterSerial = currentEncounterSerial;
            hasDisplayedWaveEnemyHp = true;
            return displayedWaveEnemyHp;
        }

        private void SetState(BattleFlowState nextState)
        {
            CurrentState = nextState;
        }
    }
}
