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
        public string CurrentDungeonId { get; private set; } = "blight_cavern";
        public int CurrentDungeonFloor { get; private set; } = 1;
        public int HighestFloor => PlayerProfile?.HighestFloor ?? 0;

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
            if (PlayerProfile != null &&
                saveData.HighestFloor == 1 &&
                saveData.CurrentFloor <= 1 &&
                !HasRecordedBattleWin(saveData))
            {
                PlayerProfile.HighestFloor = 0;
            }

            int nextProgressFloor = PlayerProfile != null ? PlayerProfile.HighestFloor + 1 : 1;
            CurrentFloor = Mathf.Max(1, Mathf.Max(saveData.CurrentFloor, nextProgressFloor));
            SyncDungeonSelectionFromCurrentFloor();
            if (PlayerProfile != null &&
                PrototypePartyBootstrapService.EnsureParty(PlayerProfile))
            {
                SaveManager.Instance?.SaveCurrentGame();
            }
        }

        public void SetCurrentFloor(int floor)
        {
            CurrentFloor = Mathf.Max(1, floor);
            SyncDungeonSelectionFromCurrentFloor();
        }

        public void SetCurrentDungeonFloor(string dungeonId, int dungeonFloor)
        {
            CurrentDungeonId = string.IsNullOrEmpty(dungeonId) ? "blight_cavern" : dungeonId;
            CurrentDungeonFloor = Mathf.Max(1, dungeonFloor);
            CurrentFloor = BattleDungeonCatalog.ResolveGlobalFloor(CurrentDungeonId, CurrentDungeonFloor);
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
            SyncDungeonSelectionFromCurrentFloor();
        }

        private void SyncDungeonSelectionFromCurrentFloor()
        {
            var dungeon = BattleDungeonCatalog.GetDungeonForGlobalFloor(CurrentFloor);
            CurrentDungeonId = dungeon != null ? dungeon.DungeonId : "blight_cavern";
            CurrentDungeonFloor = BattleDungeonCatalog.ResolveLocalFloor(CurrentFloor);
        }

        private static bool HasRecordedBattleWin(PlayerSaveData saveData)
        {
            if (saveData?.MissionProgressList == null)
            {
                return false;
            }

            for (int i = 0; i < saveData.MissionProgressList.Count; i += 1)
            {
                MissionProgressData mission = saveData.MissionProgressList[i];
                if (mission != null && mission.MissionId == "mission_clear_1" && mission.Progress > 0)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
