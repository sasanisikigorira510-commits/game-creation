using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Managers
{
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public PlayerSaveData CurrentSaveData { get; private set; }

        private string SaveFilePath => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadOrCreate()
        {
            string primaryPath = SaveFilePath;
            if (SaveFileStore.TryLoad(primaryPath, out PlayerSaveData primarySave, out string primaryError))
            {
                CurrentSaveData = primarySave;
                return;
            }

            string archivedPath = SaveFileStore.TryArchiveUnreadablePrimary(primaryPath);
            string backupPath = SaveFileStore.GetBackupPath(primaryPath);
            if (SaveFileStore.TryLoad(backupPath, out PlayerSaveData backupSave, out string backupError))
            {
                CurrentSaveData = backupSave;
                if (!SaveFileStore.TrySave(primaryPath, backupSave, out string restoreError, rotateBackup: false))
                {
                    Debug.LogError($"[SaveManager] Loaded backup but could not restore primary save: {restoreError}");
                }

                Debug.LogWarning(
                    $"[SaveManager] Recovered save from backup. Primary error: {primaryError}" +
                    (string.IsNullOrEmpty(archivedPath) ? string.Empty : $" Archived: {archivedPath}"));
                return;
            }

            CurrentSaveData = PlayerSaveData.CreateDefault();
            if (!SaveFileStore.TrySave(primaryPath, CurrentSaveData, out string createError, rotateBackup: false))
            {
                Debug.LogError($"[SaveManager] Could not create a new save: {createError}");
            }

            if (!string.IsNullOrEmpty(primaryError) && primaryError != "Save file does not exist.")
            {
                Debug.LogWarning(
                    $"[SaveManager] Started a new save because primary and backup were unreadable. " +
                    $"Primary: {primaryError} Backup: {backupError}" +
                    (string.IsNullOrEmpty(archivedPath) ? string.Empty : $" Archived: {archivedPath}"));
            }
        }

        public void Save(PlayerSaveData saveData)
        {
            if (saveData == null)
            {
                Debug.LogError("[SaveManager] Refused to save null data.");
                return;
            }

            CurrentSaveData = saveData;
            if (!SaveFileStore.TrySave(SaveFilePath, saveData, out string error))
            {
                Debug.LogError($"[SaveManager] Save failed: {error}");
            }
        }

        public void SaveCurrentGame()
        {
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            Save(GameManager.Instance.PlayerProfile.ToSaveData(GameManager.Instance.CurrentFloor));
        }

        public void SaveAfterDungeonStageClear(int clearedFloor)
        {
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            GameManager.Instance.PlayerProfile.LastActiveAt = System.DateTime.Now.ToString("O");
            SaveCurrentGame();
            Debug.Log($"[SaveManager] Auto-saved after dungeon stage clear. clearedFloor={Mathf.Max(1, clearedFloor)}, currentFloor={GameManager.Instance.CurrentFloor}");
        }

        public void SaveForSuspend()
        {
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            GameManager.Instance.PlayerProfile.LastActiveAt = System.DateTime.Now.ToString("O");
            SaveCurrentGame();
        }
    }
}
