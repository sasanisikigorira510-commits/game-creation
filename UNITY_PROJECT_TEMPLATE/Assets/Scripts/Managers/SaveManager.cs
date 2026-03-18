using System.IO;
using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Managers
{
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public PlayerSaveData CurrentSaveData { get; private set; }

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

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
            if (!File.Exists(SaveFilePath))
            {
                CurrentSaveData = PlayerSaveData.CreateDefault();
                Save(CurrentSaveData);
                return;
            }

            var json = File.ReadAllText(SaveFilePath);
            CurrentSaveData = JsonUtility.FromJson<PlayerSaveData>(json);

            if (CurrentSaveData == null)
            {
                CurrentSaveData = PlayerSaveData.CreateDefault();
                Save(CurrentSaveData);
            }
        }

        public void Save(PlayerSaveData saveData)
        {
            CurrentSaveData = saveData;
            var json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SaveFilePath, json);
        }

        public void SaveCurrentGame()
        {
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            Save(GameManager.Instance.PlayerProfile.ToSaveData(GameManager.Instance.CurrentFloor));
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
