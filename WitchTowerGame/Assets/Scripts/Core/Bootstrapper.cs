using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Home;
using WitchTower.Managers;

namespace WitchTower.Core
{
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = "HomeScene";

        private void Awake()
        {
            Application.runInBackground = true;
            DontDestroyOnLoad(gameObject);
            EnsureManagers();
            ManagerFactory.EnsureUiPresentationCamera();
            InitializeGame();
        }

        private void Start()
        {
            SceneManager.LoadScene(nextSceneName);
        }

        private static void EnsureManagers()
        {
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureAudioManager();
        }

        private static void InitializeGame()
        {
            var saveManager = SaveManager.Instance;
            saveManager.LoadOrCreate();

            MasterDataManager.Instance.Initialize();

            var gameManager = GameManager.Instance;
            gameManager.InitializeFromSave(saveManager.CurrentSaveData);
            IdleRewardService.EvaluatePendingReward(gameManager.PlayerProfile, System.DateTime.Now);
            SaveManager.Instance.SaveCurrentGame();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                return;
            }

            SaveManager.Instance?.SaveForSuspend();
        }

        private void OnApplicationQuit()
        {
            SaveManager.Instance?.SaveForSuspend();
        }
    }
}
