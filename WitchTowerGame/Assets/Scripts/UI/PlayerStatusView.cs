using TMPro;
using UnityEngine;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Home;

namespace WitchTower.UI
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private TMP_Text rebirthText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardForecastText;
        [SerializeField] private TMP_Text threatText;
        [SerializeField] private TMP_Text confidenceText;
        [SerializeField] private TMP_Text loadoutAlertText;
        [SerializeField] private TMP_Text goldRouteText;
        [SerializeField] private TMP_Text upgradeRouteText;
        [SerializeField] private TMP_Text rewardRouteText;
        [SerializeField] private TMP_Text pushWindowText;
        [SerializeField] private TMP_Text roiReadText;
        [SerializeField] private TMP_Text decisionLineText;
        [SerializeField] private TMP_Text decisionBadgeText;
        [SerializeField] private TMP_Text commandStackText;
        [SerializeField] private TMP_Text momentumReadText;
        [SerializeField] private TMP_Text runCallText;
        [SerializeField] private TMP_Text riskBufferText;
        [SerializeField] private TMP_Text enemyTempoText;
        [SerializeField] private TMP_Text damageRaceText;
        [SerializeField] private TMP_Text burstReadText;
        [SerializeField] private TMP_Text killClockText;
        [SerializeField] private TMP_Text critWindowText;
        [SerializeField] private TMP_Text survivalWindowText;
        [SerializeField] private TMP_Text clockEdgeText;
        [SerializeField] private TMP_Text tempoVerdictText;
        [SerializeField] private TMP_Text pressureCallText;
        [SerializeField] private TMP_Text rewardPaceText;
        [SerializeField] private TMP_Text priorityText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text actionText;

        public void Bind(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv. {profile.Level}  魂片 {profile.RebirthPoints}";
            }

            if (floorText != null)
            {
                floorText.text = $"最高到達 {profile.HighestFloor}階";
            }

            if (expText != null)
            {
                expText.text = $"経験値 {profile.Exp}/{profile.GetRequiredExpForNextLevel()}";
            }

            if (rebirthText != null)
            {
                int reward = profile.GetPendingRebirthPointReward();
                rebirthText.text = reward > 0
                    ? $"転生可能 +{reward}魂片"
                    : $"転生解放 Lv.{RebirthService.MinimumLevel}";
            }

            if (progressText != null)
            {
                progressText.text = HomeActionAdvisor.BuildRunProgressText(profile);
            }

            if (rewardForecastText != null)
            {
                rewardForecastText.text = HomeActionAdvisor.BuildRewardForecastText(profile);
            }

            HideAdvisorText(
                threatText,
                confidenceText,
                loadoutAlertText,
                goldRouteText,
                upgradeRouteText,
                rewardRouteText,
                pushWindowText,
                roiReadText,
                decisionLineText,
                decisionBadgeText,
                commandStackText,
                momentumReadText,
                runCallText,
                riskBufferText,
                enemyTempoText,
                damageRaceText,
                burstReadText,
                killClockText,
                critWindowText,
                survivalWindowText,
                clockEdgeText,
                tempoVerdictText,
                pressureCallText,
                rewardPaceText,
                priorityText,
                summaryText,
                actionText);
        }

        private static void HideAdvisorText(params TMP_Text[] texts)
        {
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                {
                    continue;
                }

                text.text = string.Empty;
                text.gameObject.SetActive(false);
            }
        }

        private static Color GetDecisionBadgeColor(string badge)
        {
            if (string.IsNullOrEmpty(badge))
            {
                return Color.white;
            }

            if (badge.Contains("報酬受取"))
            {
                return new Color(0.99f, 0.84f, 0.53f, 1f);
            }

            if (badge.Contains("準備"))
            {
                return new Color(0.98f, 0.55f, 0.55f, 1f);
            }

            if (badge.Contains("調整"))
            {
                return new Color(0.58f, 0.84f, 0.99f, 1f);
            }

            return new Color(0.56f, 0.93f, 0.68f, 1f);
        }

        private static Color GetThreatReadColor(string threatRead)
        {
            if (string.IsNullOrEmpty(threatRead))
            {
                return BattleEncounterAdvisor.GetThreatColor(string.Empty);
            }

            if (threatRead.Contains("危険"))
            {
                return BattleEncounterAdvisor.GetThreatColor("Threat: dangerous matchup");
            }

            if (threatRead.Contains("五分"))
            {
                return BattleEncounterAdvisor.GetThreatColor("Threat: even fight");
            }

            return BattleEncounterAdvisor.GetThreatColor("Threat: favorable push");
        }
    }
}
