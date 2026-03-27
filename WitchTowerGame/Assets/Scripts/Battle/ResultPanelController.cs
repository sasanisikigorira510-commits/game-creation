using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Battle
{
    public sealed class ResultPanelController : MonoBehaviour
    {
        private static readonly Color WinTitleColor = new Color(0.97f, 0.88f, 0.55f, 1f);
        private static readonly Color LoseTitleColor = new Color(0.96f, 0.54f, 0.54f, 1f);
        private static readonly Color WinSummaryColor = new Color(0.95f, 0.92f, 0.76f, 0.98f);
        private static readonly Color LoseSummaryColor = new Color(0.96f, 0.82f, 0.82f, 0.98f);
        private static readonly Color WinHintColor = new Color(0.78f, 0.84f, 0.92f, 0.94f);
        private static readonly Color LoseHintColor = new Color(0.95f, 0.81f, 0.81f, 0.94f);
        private static readonly Color PrimaryActionColor = new Color(0.21f, 0.56f, 0.78f, 1f);
        private static readonly Color SecondaryActionColor = new Color(0.35f, 0.24f, 0.42f, 1f);

        [SerializeField] private GameObject rootObject;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text rewardHintText;
        [SerializeField] private TMP_Text nextRewardForecastText;
        [SerializeField] private TMP_Text nextActionText;
        [SerializeField] private Button nextFloorButton;
        [SerializeField] private Button returnHomeButton;
        [SerializeField] private TMP_Text nextFloorButtonText;
        [SerializeField] private TMP_Text returnHomeButtonText;

        private void Awake()
        {
            Hide();
        }

        public void Show(BattleResultViewData viewData)
        {
            if (rootObject != null)
            {
                rootObject.SetActive(true);
            }

            if (titleText != null)
            {
                titleText.text = viewData.IsWin ? "Victory" : "Defeat";
                titleText.color = viewData.IsWin ? WinTitleColor : LoseTitleColor;
            }

            if (goldText != null)
            {
                goldText.text = $"Gold +{viewData.Gold}";
            }

            if (expText != null)
            {
                expText.text = $"EXP +{viewData.Exp}";
            }

            if (summaryText != null)
            {
                summaryText.text = viewData.IsWin
                    ? $"Floor {viewData.NextFloor - 1} cleared. The tower opens floor {viewData.NextFloor}."
                    : "The climb stalls here. Refit at Home, upgrade, and try again.";
                summaryText.color = viewData.IsWin ? WinSummaryColor : LoseSummaryColor;
            }

            if (rewardHintText != null)
            {
                string baseHint = viewData.IsWin
                    ? "Bank the rewards, swap gear if needed, or keep the streak alive."
                    : "Claim anything ready in Home before the next attempt.";
                rewardHintText.text = viewData.IsWin && !string.IsNullOrEmpty(viewData.RecruitSummary)
                    ? viewData.RecruitSummary + "\n" + baseHint
                    : baseHint;
                rewardHintText.color = viewData.IsWin ? WinHintColor : LoseHintColor;
            }

            if (nextRewardForecastText != null)
            {
                int forecastFloor = viewData.NextFloor;
                int highestClearedFloor = viewData.IsWin ? viewData.NextFloor - 1 : viewData.NextFloor - 1;
                BattleRewardResult rewardForecast = BattleRewardCalculator.Calculate(forecastFloor, highestClearedFloor);
                nextRewardForecastText.text = viewData.IsWin
                    ? $"Next Reward Forecast: floor {forecastFloor} should pay about {rewardForecast.Gold} Gold and {rewardForecast.Exp} EXP."
                    : $"Retry Forecast: floor {forecastFloor} should pay about {rewardForecast.Gold} Gold and {rewardForecast.Exp} EXP.";
                nextRewardForecastText.color = viewData.IsWin ? WinHintColor : LoseHintColor;
            }

            if (nextActionText != null)
            {
                nextActionText.text = viewData.IsWin
                    ? $"Next Floor {viewData.NextFloor}"
                    : "Return to Home";
            }

            if (nextFloorButton != null)
            {
                nextFloorButton.gameObject.SetActive(viewData.IsWin);
            }

            if (returnHomeButton != null)
            {
                returnHomeButton.gameObject.SetActive(true);
            }

            if (nextFloorButtonText != null)
            {
                nextFloorButtonText.text = $"Next Floor {viewData.NextFloor}";
            }

            if (returnHomeButtonText != null)
            {
                returnHomeButtonText.text = viewData.IsWin ? "Return Home" : "Back to Home";
            }

            ApplyButtonEmphasis(nextFloorButton, viewData.IsWin ? PrimaryActionColor : SecondaryActionColor);
            ApplyButtonEmphasis(returnHomeButton, viewData.IsWin ? SecondaryActionColor : PrimaryActionColor);
        }

        public void Hide()
        {
            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }
        }

        private static void ApplyButtonEmphasis(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }
}
