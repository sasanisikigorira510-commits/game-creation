using System.Collections;
using WitchTower.Monetization;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.UI;
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

        private IEnumerator Start()
        {
            var saves = SaveManager.Instance;
            if (saves != null && saves.AwaitingPurchaseEnvironment)
            {
                var probe = ApplePurchaseEnvironmentProbe.Ensure();
                if (!probe.IsPending && probe.Environment == VerifiedApplePurchaseEnvironment.Unknown) probe.Refresh();
                while (saves != null && saves.AwaitingPurchaseEnvironment)
                {
                    if (saves.TryBindReleaseEnvironment(probe.Environment)) break;
                    yield return null;
                }
                if (saves == null) yield break;
                InitializeGame();
            }
            if (saves != null && saves.StorageAccessAvailable && !saves.RecoveryRequired)
                SceneTransitionGuard.LoadScene(nextSceneName);
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
            // Release startup selects its endpoint and root before loading saves.
            if (saveManager == null || !saveManager.StorageAccessAvailable) return;
            saveManager.LoadOrCreate();
            if (!saveManager.StorageAccessAvailable || saveManager.RecoveryRequired) return;

            MasterDataManager.Instance.Initialize();

            var gameManager = GameManager.Instance;
            gameManager.InitializeFromSave(saveManager.CurrentSaveData);
            SaveManager.Instance.SaveCurrentGame();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus || SaveManager.Instance?.StorageAccessAvailable != true)
            {
                return;
            }

            SaveManager.Instance?.SaveForSuspend();
        }

        private void OnApplicationQuit()
        {
            if (SaveManager.Instance?.StorageAccessAvailable != true) return;
            SaveManager.Instance?.SaveForSuspend();
        }
    }
}
