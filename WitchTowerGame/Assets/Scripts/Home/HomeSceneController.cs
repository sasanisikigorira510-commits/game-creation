using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Managers;

namespace WitchTower.Home
{
    public sealed class HomeSceneController : MonoBehaviour
    {
        [SerializeField] private PanelSwitcher panelSwitcher;
        [SerializeField] private HomePanelController homePanelController;
        [SerializeField] private EnhancePanelController enhancePanelController;
        [SerializeField] private EquipmentPanelController equipmentPanelController;
        [SerializeField] private MissionPanelController missionPanelController;
        [SerializeField] private string battleSceneName = "BattleScene";

        private void Start()
        {
            Refresh();
        }

        public void OpenHome()
        {
            panelSwitcher.ShowHome();
            homePanelController.Refresh();
        }

        public void OpenEnhance()
        {
            panelSwitcher.ShowEnhance();
            enhancePanelController.Refresh();
        }

        public void OpenEquipment()
        {
            panelSwitcher.ShowEquipment();
            equipmentPanelController.Refresh();
        }

        public void OpenMission()
        {
            panelSwitcher.ShowMission();
            missionPanelController.Refresh();
        }

        public void StartBattle()
        {
            GameManager.Instance.SetCurrentFloor(Mathf.Max(1, GameManager.Instance.CurrentFloor));
            SceneManager.LoadScene(battleSceneName);
        }

        public void Refresh()
        {
            panelSwitcher.ShowHome();
            homePanelController.Refresh();
        }
    }
}
