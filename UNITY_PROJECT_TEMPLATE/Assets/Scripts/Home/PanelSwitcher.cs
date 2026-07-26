using UnityEngine;

namespace WitchTower.Home
{
    public sealed class PanelSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject enhancePanel;
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private GameObject missionPanel;
        [SerializeField] private GameObject rebirthPanel;

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

        public void ShowRebirth()
        {
            SetActive(rebirthPanel);
        }

        private void SetActive(GameObject target)
        {
            if (homePanel != null) homePanel.SetActive(target == homePanel);
            if (enhancePanel != null) enhancePanel.SetActive(target == enhancePanel);
            if (equipmentPanel != null) equipmentPanel.SetActive(target == equipmentPanel);
            if (missionPanel != null) missionPanel.SetActive(target == missionPanel);
            if (rebirthPanel != null) rebirthPanel.SetActive(target == rebirthPanel);
        }
    }
}
