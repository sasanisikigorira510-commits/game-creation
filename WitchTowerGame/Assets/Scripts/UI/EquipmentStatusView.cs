using TMPro;
using UnityEngine;

namespace WitchTower.UI
{
    public sealed class EquipmentStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text accessoryText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text matchupText;
        [SerializeField] private TMP_Text loadoutImpactText;

        public void Bind(string weaponName, string armorName, string accessoryName, string summary = null, string matchup = null, string loadoutImpact = null)
        {
            if (weaponText != null)
            {
                weaponText.text = $"武器: {weaponName}";
            }

            if (armorText != null)
            {
                armorText.text = $"防具: {armorName}";
            }

            if (accessoryText != null)
            {
                accessoryText.text = $"装飾: {accessoryName}";
            }

            if (summaryText != null)
            {
                summaryText.text = FormatSummary(summary);
            }

            if (matchupText != null)
            {
                matchupText.text = matchup ?? "装備方針: モンスター個別装備";
            }

            if (loadoutImpactText != null)
            {
                loadoutImpactText.text = loadoutImpact ?? "強化情報: 強化遺物で装備を育成";
            }
        }

        private static string FormatSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return "戦力プレビューを取得できません";
            }

            if (summary.Contains("評価: 最前線"))
            {
                return summary.Replace("評価: 最前線", "評価: <color=#63E6A8>最前線</color>");
            }

            if (summary.Contains("評価: 安定"))
            {
                return summary.Replace("評価: 安定", "評価: <color=#8FD9FF>安定</color>");
            }

            if (summary.Contains("評価: 発展途上"))
            {
                return summary.Replace("評価: 発展途上", "評価: <color=#F4C66B>発展途上</color>");
            }

            if (summary.Contains("評価: 脆い"))
            {
                return summary.Replace("評価: 脆い", "評価: <color=#F07D7D>脆い</color>");
            }

            return summary;
        }
    }
}
