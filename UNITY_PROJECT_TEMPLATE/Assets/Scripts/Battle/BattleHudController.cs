using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Battle
{
    public sealed class BattleHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text enemyHpText;
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

            if (enemyHpText != null && enemyStats != null)
            {
                enemyHpText.text = $"Enemy HP {enemyStats.CurrentHp}/{enemyStats.MaxHp}";
            }
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
            }

            if (cooldownText != null)
            {
                cooldownText.text = state.IsReady ? readyLabel : state.RemainingCooldown.ToString("F1");
            }
        }
    }
}
