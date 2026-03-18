using UnityEngine;
using WitchTower.Data;
using WitchTower.Save;

namespace WitchTower.Managers
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerProfile PlayerProfile { get; private set; }
        public int CurrentFloor { get; private set; }
        public int HighestFloor => PlayerProfile?.HighestFloor ?? 1;

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

        public void InitializeFromSave(PlayerSaveData saveData)
        {
            PlayerProfile = new PlayerProfile(saveData);
            CurrentFloor = Mathf.Max(1, saveData.CurrentFloor);
        }

        public void SetCurrentFloor(int floor)
        {
            CurrentFloor = Mathf.Max(1, floor);
        }

        public void RecordFloorClear(int clearedFloor)
        {
            if (PlayerProfile == null)
            {
                return;
            }

            if (clearedFloor > PlayerProfile.HighestFloor)
            {
                PlayerProfile.HighestFloor = clearedFloor;
            }

            CurrentFloor = clearedFloor + 1;
        }
    }
}
