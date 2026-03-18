using UnityEngine;

namespace WitchTower.Home
{
    public sealed class PanelSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject enhancePanel;
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private GameObject missionPanel;

        public void ShowHome()
        {
            SetActive(homePanel);
        }

        public void ShowEnhance()
        {
            SetActive(enhancePanel);
        }

        public void ShowEquipment()
        {
            SetActive(equipmentPanel);
        }

        public void ShowMission()
        {
            SetActive(missionPanel);
        }

        private void SetActive(GameObject target)
        {
            if (homePanel != null) homePanel.SetActive(target == homePanel);
            if (enhancePanel != null) enhancePanel.SetActive(target == enhancePanel);
            if (equipmentPanel != null) equipmentPanel.SetActive(target == equipmentPanel);
            if (missionPanel != null) missionPanel.SetActive(target == missionPanel);
        }
    }
}
