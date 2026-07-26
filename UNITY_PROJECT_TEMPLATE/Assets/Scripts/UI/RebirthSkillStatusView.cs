using TMPro;
using UnityEngine;
using WitchTower.Data;

namespace WitchTower.UI
{
    public sealed class RebirthSkillStatusView : MonoBehaviour
    {
        [SerializeField] private string skillId;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text descriptionText;

        public string SkillId => skillId;

        public void Bind(PlayerProfile profile)
        {
            var definition = RebirthSkillCatalog.GetDefinition(skillId);
            if (definition == null)
            {
                BindMissingSkill();
                return;
            }

            var currentLevel = profile != null ? profile.GetRebirthSkillLevel(skillId) : 0;
            if (labelText != null)
            {
                labelText.text = definition.DisplayName;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv. {currentLevel}/{definition.MaxLevel}";
            }

            if (costText != null)
            {
                costText.text = currentLevel >= definition.MaxLevel
                    ? "Max"
                    : $"Cost {definition.GetCost(currentLevel)} Soul";
            }

            if (bonusText != null)
            {
                bonusText.text = $"+{definition.GetDisplayPercent(currentLevel)}%";
            }

            if (requirementText != null)
            {
                requirementText.text = GetRequirementText(profile, definition);
            }

            if (descriptionText != null)
            {
                descriptionText.text = definition.Description;
            }
        }

        private void BindMissingSkill()
        {
            if (labelText != null)
            {
                labelText.text = "Unknown Skill";
            }

            if (levelText != null)
            {
                levelText.text = "Lv. 0/0";
            }

            if (costText != null)
            {
                costText.text = "-";
            }

            if (bonusText != null)
            {
                bonusText.text = "+0%";
            }

            if (requirementText != null)
            {
                requirementText.text = string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }
        }

        private static string GetRequirementText(PlayerProfile profile, RebirthSkillDefinition definition)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            if (RebirthService.CanPurchaseSkill(profile, definition.SkillId, out var blockedReason))
            {
                return "Available";
            }

            return blockedReason;
        }
    }
}
