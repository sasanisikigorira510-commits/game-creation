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
                levelText.text = $"Lv. {profile.Level}";
            }

            if (floorText != null)
            {
                floorText.text = $"Highest Floor {profile.HighestFloor}";
            }

            if (expText != null)
            {
                expText.text = $"EXP {profile.Exp}/{profile.GetRequiredExpForNextLevel()}";
            }

            if (progressText != null)
            {
                progressText.text = HomeActionAdvisor.BuildRunProgressText(profile);
            }

            if (rewardForecastText != null)
            {
                rewardForecastText.text = HomeActionAdvisor.BuildRewardForecastText(profile);
            }

            if (threatText != null)
            {
                string threatRead = HomeActionAdvisor.BuildThreatReadText(profile);
                threatText.text = threatRead;
                threatText.color = BattleEncounterAdvisor.GetThreatColor(threatRead.Replace("Threat Read: ", "Threat: "));
            }

            if (confidenceText != null)
            {
                confidenceText.text = HomeActionAdvisor.BuildConfidenceText(profile);
            }

            if (loadoutAlertText != null)
            {
                loadoutAlertText.text = HomeActionAdvisor.BuildLoadoutAlertText(profile);
            }

            if (goldRouteText != null)
            {
                goldRouteText.text = HomeActionAdvisor.BuildGoldRouteText(profile, 10, System.DateTime.Now);
            }

            if (upgradeRouteText != null)
            {
                upgradeRouteText.text = HomeActionAdvisor.BuildUpgradeRouteText(profile, 10);
            }

            if (rewardRouteText != null)
            {
                rewardRouteText.text = HomeActionAdvisor.BuildRewardRouteText(profile, System.DateTime.Now);
            }

            if (pushWindowText != null)
            {
                pushWindowText.text = HomeActionAdvisor.BuildPushWindowText(profile, 10, System.DateTime.Now);
            }

            if (roiReadText != null)
            {
                roiReadText.text = HomeActionAdvisor.BuildRoiReadText(profile, 10);
            }

            if (decisionLineText != null)
            {
                decisionLineText.text = HomeActionAdvisor.BuildDecisionLineText(profile, 10, System.DateTime.Now);
            }

            if (decisionBadgeText != null)
            {
                string badge = HomeActionAdvisor.BuildDecisionBadgeText(profile, 10, System.DateTime.Now);
                decisionBadgeText.text = badge;
                decisionBadgeText.color = GetDecisionBadgeColor(badge);
            }

            if (commandStackText != null)
            {
                commandStackText.text = HomeActionAdvisor.BuildCommandStackText(profile, 10, System.DateTime.Now);
            }

            if (momentumReadText != null)
            {
                momentumReadText.text = HomeActionAdvisor.BuildMomentumReadText(profile, 10, System.DateTime.Now);
            }

            if (runCallText != null)
            {
                runCallText.text = HomeActionAdvisor.BuildRunCallText(profile, 10, System.DateTime.Now);
            }

            if (riskBufferText != null)
            {
                riskBufferText.text = HomeActionAdvisor.BuildRiskBufferText(profile);
            }

            if (enemyTempoText != null)
            {
                enemyTempoText.text = HomeActionAdvisor.BuildEnemyTempoText(profile);
            }

            if (damageRaceText != null)
            {
                damageRaceText.text = HomeActionAdvisor.BuildDamageRaceText(profile);
            }

            if (burstReadText != null)
            {
                burstReadText.text = HomeActionAdvisor.BuildBurstReadText(profile);
            }

            if (killClockText != null)
            {
                killClockText.text = HomeActionAdvisor.BuildKillClockText(profile);
            }

            if (critWindowText != null)
            {
                critWindowText.text = HomeActionAdvisor.BuildCritWindowText(profile);
            }

            if (survivalWindowText != null)
            {
                survivalWindowText.text = HomeActionAdvisor.BuildSurvivalWindowText(profile);
            }

            if (clockEdgeText != null)
            {
                clockEdgeText.text = HomeActionAdvisor.BuildClockEdgeText(profile);
            }

            if (tempoVerdictText != null)
            {
                tempoVerdictText.text = HomeActionAdvisor.BuildTempoVerdictText(profile);
            }

            if (pressureCallText != null)
            {
                pressureCallText.text = HomeActionAdvisor.BuildPressureCallText(profile);
            }

            if (rewardPaceText != null)
            {
                rewardPaceText.text = HomeActionAdvisor.BuildRewardPaceText(profile);
            }

            if (summaryText != null)
            {
                summaryText.text = $"Run State: floor {profile.HighestFloor + 1} is next, upgrades A{profile.AttackUpgradeLevel}/D{profile.DefenseUpgradeLevel}/HP{profile.HpUpgradeLevel}. {HomeActionAdvisor.BuildPriorityTabText(profile, 10, System.DateTime.Now)}";
            }

            if (priorityText != null)
            {
                priorityText.text = HomeActionAdvisor.BuildPriorityTabText(profile, 10, System.DateTime.Now);
            }

            if (actionText != null)
            {
                actionText.text = HomeActionAdvisor.BuildRunAlertText(profile, 10, System.DateTime.Now);
            }
        }

        private static Color GetDecisionBadgeColor(string badge)
        {
            if (string.IsNullOrEmpty(badge))
            {
                return Color.white;
            }

            if (badge.Contains("Cash Out"))
            {
                return new Color(0.99f, 0.84f, 0.53f, 1f);
            }

            if (badge.Contains("Prep"))
            {
                return new Color(0.98f, 0.55f, 0.55f, 1f);
            }

            if (badge.Contains("Tune"))
            {
                return new Color(0.58f, 0.84f, 0.99f, 1f);
            }

            return new Color(0.56f, 0.93f, 0.68f, 1f);
        }
    }
}
