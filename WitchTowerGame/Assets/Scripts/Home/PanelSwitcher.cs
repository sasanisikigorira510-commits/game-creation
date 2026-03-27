using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WitchTower.Data;

namespace WitchTower.Home
{
    public sealed class PanelSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject enhancePanel;
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private GameObject missionPanel;
        [SerializeField] private HomePanelController homePanelController;
        [SerializeField] private EnhancePanelController enhancePanelController;
        [SerializeField] private EquipmentPanelController equipmentPanelController;
        [SerializeField] private MissionPanelController missionPanelController;
        [SerializeField] private Button homeNavButton;
        [SerializeField] private Button enhanceNavButton;
        [SerializeField] private Button equipmentNavButton;
        [SerializeField] private Button missionNavButton;
        [SerializeField] private TMP_Text homeNavBadgeText;
        [SerializeField] private TMP_Text enhanceNavBadgeText;
        [SerializeField] private TMP_Text equipmentNavBadgeText;
        [SerializeField] private TMP_Text missionNavBadgeText;

        private static readonly Color ActiveButtonColor = new Color(0.18f, 0.48f, 0.69f, 1f);
        private static readonly Color InactiveButtonColor = new Color(0.2f, 0.35f, 0.42f, 1f);
        private static readonly Color ActiveLabelColor = Color.white;
        private static readonly Color InactiveLabelColor = new Color(0.84f, 0.9f, 0.96f, 1f);

        public void ShowHome()
        {
            SetActive(homePanel);
            SetNavState(homeNavButton);
            homePanelController?.Refresh();
        }

        public void ShowEnhance()
        {
            SetActive(enhancePanel);
            SetNavState(enhanceNavButton);
            enhancePanelController?.Refresh();
        }

        public void ShowEquipment()
        {
            SetActive(equipmentPanel);
            SetNavState(equipmentNavButton);
            equipmentPanelController?.Refresh();
        }

        public void ShowMission()
        {
            SetActive(missionPanel);
            SetNavState(missionNavButton);
            missionPanelController?.Refresh();
        }

        public void RefreshNavigation(PlayerProfile profile, int baseUpgradeCost)
        {
            SetBadge(homeNavBadgeText, HomeActionAdvisor.GetHomeBadgeCount(profile));
            SetBadge(enhanceNavBadgeText, HomeActionAdvisor.GetEnhanceBadgeCount(profile, baseUpgradeCost));
            SetBadge(equipmentNavBadgeText, HomeActionAdvisor.GetEquipmentBadgeCount(profile));
            SetBadge(missionNavBadgeText, HomeActionAdvisor.GetMissionBadgeCount(profile, System.DateTime.Now));
        }

        private void SetActive(GameObject target)
        {
            if (homePanel != null) homePanel.SetActive(target == homePanel);
            if (enhancePanel != null) enhancePanel.SetActive(target == enhancePanel);
            if (equipmentPanel != null) equipmentPanel.SetActive(target == equipmentPanel);
            if (missionPanel != null) missionPanel.SetActive(target == missionPanel);
        }

        private void SetNavState(Button activeButton)
        {
            UpdateButton(homeNavButton, activeButton == homeNavButton);
            UpdateButton(enhanceNavButton, activeButton == enhanceNavButton);
            UpdateButton(equipmentNavButton, activeButton == equipmentNavButton);
            UpdateButton(missionNavButton, activeButton == missionNavButton);
        }

        private static void UpdateButton(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = isActive ? ActiveButtonColor : InactiveButtonColor;
            }

            var label = GetPrimaryLabel(button);
            if (label != null)
            {
                label.color = isActive ? ActiveLabelColor : InactiveLabelColor;
            }
        }

        private static void SetBadge(TMP_Text badgeText, int count)
        {
            if (badgeText == null)
            {
                return;
            }

            if (badgeText.transform.parent != null)
            {
                badgeText.transform.parent.gameObject.SetActive(count > 0);
            }

            badgeText.text = count > 0 ? count.ToString() : string.Empty;
        }

        private static TMP_Text GetPrimaryLabel(Button button)
        {
            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null)
                {
                    continue;
                }

                if (!label.gameObject.name.EndsWith("Badge"))
                {
                    return label;
                }
            }

            return labels.Length > 0 ? labels[0] : null;
        }
    }
}
