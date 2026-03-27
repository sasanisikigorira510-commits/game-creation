using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Battle
{
    public sealed class BattleHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text threatText;
        [SerializeField] private TMP_Text encounterText;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private Image playerHpFillImage;
        [SerializeField] private Image enemyHpFillImage;
        [SerializeField] private TMP_Text skillCooldown1Text;
        [SerializeField] private TMP_Text skillCooldown2Text;
        [SerializeField] private TMP_Text skillCooldown3Text;
        [SerializeField] private Button skillButton1;
        [SerializeField] private Button skillButton2;
        [SerializeField] private Button skillButton3;
        [SerializeField] private GameObject winLabel;
        [SerializeField] private GameObject loseLabel;
        [SerializeField] private ResultPanelController resultPanelController;

        public void ShowFloor(int floor)
        {
            if (floorText != null)
            {
                floorText.text = $"Floor {floor}";
            }
        }

        public void ShowEncounterReadout(int floor, BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            if (threatText != null)
            {
                threatText.text = BattleEncounterAdvisor.BuildThreatText(playerStats, enemyStats);
                threatText.color = BattleEncounterAdvisor.GetThreatColor(threatText.text);
            }

            if (encounterText != null)
            {
                encounterText.text = BuildEncounterText(floor, playerStats, enemyStats);
            }
        }

        public void ShowResult(bool isWin)
        {
            if (winLabel != null)
            {
                winLabel.SetActive(isWin);
            }

            if (loseLabel != null)
            {
                loseLabel.SetActive(!isWin);
            }
        }

        public void ShowResultPanel(BattleResultViewData viewData)
        {
            if (resultPanelController != null)
            {
                resultPanelController.Show(viewData);
            }
        }

        public void HideResultPanel()
        {
            if (resultPanelController != null)
            {
                resultPanelController.Hide();
            }
        }

        public void UpdateHp(BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            if (playerHpText != null && playerStats != null)
            {
                playerHpText.text = $"Player HP {playerStats.CurrentHp}/{playerStats.MaxHp}";
            }

            UpdateHealthBar(playerHpFillImage, playerStats, new Color(0.22f, 0.74f, 0.65f, 1f), new Color(0.95f, 0.67f, 0.23f, 1f), new Color(0.88f, 0.24f, 0.31f, 1f));

            if (enemyHpText != null && enemyStats != null)
            {
                enemyHpText.text = $"Enemy HP {enemyStats.CurrentHp}/{enemyStats.MaxHp}";
            }

            UpdateHealthBar(enemyHpFillImage, enemyStats, new Color(0.93f, 0.42f, 0.49f, 1f), new Color(0.96f, 0.67f, 0.29f, 1f), new Color(0.80f, 0.21f, 0.27f, 1f));
        }

        public void SetSkillButtonsInteractable(bool interactable)
        {
            if (skillButton1 != null) skillButton1.interactable = interactable;
            if (skillButton2 != null) skillButton2.interactable = interactable;
            if (skillButton3 != null) skillButton3.interactable = interactable;
        }

        public void UpdateSkillCooldowns(BattleSkillState strikeState, BattleSkillState drainState, BattleSkillState guardState)
        {
            UpdateSkillCooldown(skillButton1, skillCooldown1Text, strikeState, "Strike");
            UpdateSkillCooldown(skillButton2, skillCooldown2Text, drainState, "Drain");
            UpdateSkillCooldown(skillButton3, skillCooldown3Text, guardState, "Guard");
        }

        private static void UpdateSkillCooldown(Button button, TMP_Text cooldownText, BattleSkillState state, string readyLabel)
        {
            if (state == null)
            {
                return;
            }

            if (button != null)
            {
                button.interactable = state.IsReady;
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = state.IsReady
                        ? new Color(0.18f, 0.45f, 0.64f, 1f)
                        : new Color(0.20f, 0.24f, 0.30f, 0.95f);
                }
            }

            if (cooldownText != null)
            {
                cooldownText.text = state.IsReady ? readyLabel : state.RemainingCooldown.ToString("F1");
                cooldownText.color = state.IsReady
                    ? new Color(0.95f, 0.97f, 1f, 1f)
                    : new Color(0.90f, 0.78f, 0.42f, 1f);
            }
        }

        private static void UpdateHealthBar(Image fillImage, BattleUnitStats stats, Color highColor, Color midColor, Color lowColor)
        {
            if (fillImage == null || stats == null || stats.MaxHp <= 0)
            {
                return;
            }

            float ratio = Mathf.Clamp01((float)stats.CurrentHp / stats.MaxHp);
            fillImage.fillAmount = ratio;
            fillImage.color = ratio > 0.6f ? highColor : (ratio > 0.3f ? midColor : lowColor);
        }

        private static string BuildEncounterText(int floor, BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            if (playerStats == null || enemyStats == null)
            {
                return $"Encounter: floor {floor} data unavailable.";
            }

            return $"Encounter: floor {floor} enemy opens at {enemyStats.MaxHp} HP / {enemyStats.Attack} ATK. Your build enters with {playerStats.MaxHp} HP / {playerStats.Attack} ATK.";
        }
    }
}
