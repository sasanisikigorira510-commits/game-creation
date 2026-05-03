using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Core
{
    public static class ManagerFactory
    {
        private const string UiPresentationCameraName = "UiPresentationCamera";

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

        public static void EnsureUiPresentationCamera()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return;
                }
            }

            GameObject cameraObject = GameObject.Find(UiPresentationCameraName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(UiPresentationCameraName);
                Object.DontDestroyOnLoad(cameraObject);
            }

            Camera uiCamera = cameraObject.GetComponent<Camera>();
            if (uiCamera == null)
            {
                uiCamera = cameraObject.AddComponent<Camera>();
            }

            cameraObject.SetActive(true);
            uiCamera.enabled = true;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0.012f, 0.018f, 0.028f, 1f);
            uiCamera.cullingMask = 0;
            uiCamera.orthographic = true;
            uiCamera.depth = -1000f;
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
        }

        private static void CreateManager<T>(string objectName) where T : Component
        {
            var go = new GameObject(objectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<T>();
        }
    }
}
