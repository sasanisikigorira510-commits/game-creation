using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;

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

        public float DebugPlayerHpFillAmount => playerHpFillImage != null ? playerHpFillImage.fillAmount : -1f;
        public float DebugEnemyHpFillAmount => enemyHpFillImage != null ? enemyHpFillImage.fillAmount : -1f;

        public void ShowFloor(int floor)
        {
            if (floorText != null)
            {
                floorText.text = $"{BattleDungeonCatalog.ResolveLocalFloor(floor)}層";
            }
        }

        public void ShowEncounterReadout(int floor, BattleUnitStats playerStats, BattleUnitStats enemyStats)
        {
            if (threatText != null)
            {
                threatText.text = string.Empty;
                threatText.gameObject.SetActive(false);
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
                playerHpText.text = $"味方HP {playerStats.CurrentHp}/{playerStats.MaxHp}";
            }
            else if (playerHpText != null)
            {
                playerHpText.text = "味方HP --/--";
            }

            UpdateHealthBar(playerHpFillImage, playerStats, new Color(0.22f, 0.74f, 0.65f, 1f), new Color(0.95f, 0.67f, 0.23f, 1f), new Color(0.88f, 0.24f, 0.31f, 1f));

            if (enemyHpText != null && enemyStats != null)
            {
                enemyHpText.text = $"敵HP {enemyStats.CurrentHp}/{enemyStats.MaxHp}";
            }
            else if (enemyHpText != null)
            {
                enemyHpText.text = "敵HP --/--";
            }

            UpdateHealthBar(enemyHpFillImage, enemyStats, new Color(0.93f, 0.42f, 0.49f, 1f), new Color(0.96f, 0.67f, 0.29f, 1f), new Color(0.80f, 0.21f, 0.27f, 1f));
        }

        public void UpdateHp(int playerCurrentHp, int playerMaxHp, int enemyCurrentHp, int enemyMaxHp)
        {
            if (playerHpText != null && playerMaxHp > 0)
            {
                playerHpText.text = $"味方HP {Mathf.Clamp(playerCurrentHp, 0, playerMaxHp)}/{playerMaxHp}";
            }
            else if (playerHpText != null)
            {
                playerHpText.text = "味方HP --/--";
            }

            UpdateHealthBar(playerHpFillImage, playerCurrentHp, playerMaxHp, new Color(0.22f, 0.74f, 0.65f, 1f), new Color(0.95f, 0.67f, 0.23f, 1f), new Color(0.88f, 0.24f, 0.31f, 1f));

            if (enemyHpText != null && enemyMaxHp > 0)
            {
                enemyHpText.text = $"敵HP {Mathf.Clamp(enemyCurrentHp, 0, enemyMaxHp)}/{enemyMaxHp}";
            }
            else if (enemyHpText != null)
            {
                enemyHpText.text = "敵HP --/--";
            }

            UpdateHealthBar(enemyHpFillImage, enemyCurrentHp, enemyMaxHp, new Color(0.93f, 0.42f, 0.49f, 1f), new Color(0.96f, 0.67f, 0.29f, 1f), new Color(0.80f, 0.21f, 0.27f, 1f));
        }

        public void SetSkillButtonsInteractable(bool interactable)
        {
            if (skillButton1 != null) skillButton1.interactable = interactable;
            if (skillButton2 != null) skillButton2.interactable = interactable;
            if (skillButton3 != null) skillButton3.interactable = interactable;
        }

        public void UpdateSkillCooldowns(BattleSkillState strikeState, BattleSkillState drainState, BattleSkillState guardState)
        {
            UpdateSkillCooldown(skillButton1, skillCooldown1Text, strikeState, "強撃");
            UpdateSkillCooldown(skillButton2, skillCooldown2Text, drainState, "吸収");
            UpdateSkillCooldown(skillButton3, skillCooldown3Text, guardState, "防御");
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
            if (fillImage == null)
            {
                return;
            }

            fillImage.enabled = true;
            ConfigureHealthBarFill(fillImage);

            if (stats == null || stats.MaxHp <= 0)
            {
                fillImage.fillAmount = 0f;
                fillImage.color = lowColor;
                return;
            }

            float ratio = Mathf.Clamp01((float)stats.CurrentHp / stats.MaxHp);
            fillImage.fillAmount = ratio;
            fillImage.color = ratio > 0.6f ? highColor : (ratio > 0.3f ? midColor : lowColor);
        }

        private static void UpdateHealthBar(Image fillImage, int currentHp, int maxHp, Color highColor, Color midColor, Color lowColor)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.enabled = true;
            ConfigureHealthBarFill(fillImage);

            if (maxHp <= 0)
            {
                fillImage.fillAmount = 0f;
                fillImage.color = lowColor;
                return;
            }

            float ratio = Mathf.Clamp01((float)Mathf.Clamp(currentHp, 0, maxHp) / maxHp);
            fillImage.fillAmount = ratio;
            fillImage.color = ratio > 0.6f ? highColor : (ratio > 0.3f ? midColor : lowColor);
        }

        private static void ConfigureHealthBarFill(Image fillImage)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillClockwise = true;
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
