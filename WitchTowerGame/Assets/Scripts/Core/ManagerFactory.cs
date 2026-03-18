using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Core
{
    public static class ManagerFactory
    {
        public static void EnsureGameManager()
        {
            if (GameManager.Instance != null)
            {
                return;
            }

            CreateManager<GameManager>("GameManager");
        }

        public static void EnsureSaveManager()
        {
            if (SaveManager.Instance != null)
            {
                return;
            }

            CreateManager<SaveManager>("SaveManager");
        }

        public static void EnsureMasterDataManager()
        {
            if (MasterDataManager.Instance != null)
            {
                return;
            }

            CreateManager<MasterDataManager>("MasterDataManager");
        }

        public static void EnsureAudioManager()
        {
            if (AudioManager.Instance != null)
            {
                return;
            }

            CreateManager<AudioManager>("AudioManager");
        }

        private static void CreateManager<T>(string objectName) where T : Component
        {
            var go = new GameObject(objectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<T>();
        }
    }
}
