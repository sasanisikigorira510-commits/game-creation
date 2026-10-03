using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Home;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private BattlePermanentEffectsPanel permanentEffectsPanel;
        public bool IsPermanentEffectsInputBlocked => isRetireConfirmationOpen || GuardianTrialSession.IsPractice || DailyChallengeSession.IsActive;
        public bool CanEnableBattleAutoRepeat => !GuardianTrialSession.IsActive && !DailyChallengeSession.IsActive &&
            GameManager.Instance?.PlayerProfile != null && GameManager.Instance.PlayerProfile.HasCompletedTutorial &&
            !(hasLastResultViewData && RequiresHomeReturn(lastResultViewData));

        // Arm the existing restart path; never restart/discard a battle mid-fight.
        public bool SetBattleAutoRepeatEnabled(bool enabled)
        {
            var profile = GameManager.Instance?.PlayerProfile;
            if (profile == null || GuardianTrialSession.IsPractice || DailyChallengeSession.IsActive || !profile.HasAutoRepeatFloorUpgrade || (enabled && !CanEnableBattleAutoRepeat)) return false;
            profile.IsAutoRepeatFloorUpgradeEnabled = enabled;
            if (!enabled) StopAutoRepeatSameFloor();
            else
            {
                autoRepeatSameFloorActive = true;
                if (resultHandled) ScheduleAutoRepeatRestartIfNeeded();
            }
            return true;
        }

        private void EnsurePermanentEffectsPanel()
        {
            if (permanentEffectsPanel != null) return;
            var existing = minimalCanvasRoot.transform.Find("BattlePermanentEffectsRoot");
            if (existing != null) permanentEffectsPanel = existing.GetComponent<BattlePermanentEffectsPanel>();
            if (permanentEffectsPanel == null)
            {
                var go = new GameObject("BattlePermanentEffectsRoot", typeof(RectTransform));
                go.transform.SetParent(minimalCanvasRoot.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                permanentEffectsPanel = go.AddComponent<BattlePermanentEffectsPanel>();
            }
            permanentEffectsPanel.Initialize(this);
        }
    }
}
