using TMPro;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed class ResultPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject rootObject;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private TMP_Text nextActionText;

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
            }

            if (goldText != null)
            {
                goldText.text = $"Gold +{viewData.Gold}";
            }

            if (expText != null)
            {
                expText.text = $"EXP +{viewData.Exp}";
            }

            if (nextActionText != null)
            {
                nextActionText.text = viewData.IsWin
                    ? $"Next Floor {viewData.NextFloor}"
                    : "Return to Home";
            }
        }

        public void Hide()
        {
            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }
        }
    }
}
