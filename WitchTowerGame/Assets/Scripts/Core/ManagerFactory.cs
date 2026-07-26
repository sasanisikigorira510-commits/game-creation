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
            bool hasActiveCamera = false;
            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    hasActiveCamera = true;
                    break;
                }
            }

            if (hasActiveCamera)
            {
                NormalizeAudioListeners();
                return;
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
            EnsureAudioListener(cameraObject);
            NormalizeAudioListeners();
        }

        public static void NormalizeAudioListeners()
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            AudioListener preferred = ResolvePreferredAudioListener(listeners);
            if (preferred == null)
            {
                GameObject fallbackTarget = ResolveFallbackAudioListenerTarget();
                preferred = EnsureAudioListener(fallbackTarget);
                listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            foreach (AudioListener listener in listeners)
            {
                if (listener == null || !listener.gameObject.activeInHierarchy)
                {
                    continue;
                }

                listener.enabled = listener == preferred;
            }
        }

        private static void CreateManager<T>(string objectName) where T : Component
        {
            var go = new GameObject(objectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<T>();
        }

        private static AudioListener EnsureAudioListener(GameObject fallbackTarget)
        {
            if (fallbackTarget == null)
            {
                return null;
            }

            AudioListener fallbackListener = fallbackTarget.GetComponent<AudioListener>();
            if (fallbackListener == null)
            {
                fallbackListener = fallbackTarget.AddComponent<AudioListener>();
            }

            fallbackListener.enabled = true;
            return fallbackListener;
        }

        private static AudioListener ResolvePreferredAudioListener(AudioListener[] listeners)
        {
            Camera mainCamera = Camera.main;
            AudioListener mainListener = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
            if (IsUsableListener(mainListener) && !IsUiPresentationListener(mainListener))
            {
                return mainListener;
            }

            foreach (AudioListener listener in listeners)
            {
                if (IsUsableListener(listener) && !IsUiPresentationListener(listener))
                {
                    return listener;
                }
            }

            foreach (AudioListener listener in listeners)
            {
                if (IsUsableListener(listener))
                {
                    return listener;
                }
            }

            return null;
        }

        private static GameObject ResolveFallbackAudioListenerTarget()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.enabled && mainCamera.gameObject.activeInHierarchy)
            {
                return mainCamera.gameObject;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera.gameObject;
                }
            }

            GameObject cameraObject = GameObject.Find(UiPresentationCameraName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(UiPresentationCameraName);
                Object.DontDestroyOnLoad(cameraObject);
            }

            return cameraObject;
        }

        private static bool IsUsableListener(AudioListener listener)
        {
            return listener != null && listener.gameObject.activeInHierarchy;
        }

        private static bool IsUiPresentationListener(AudioListener listener)
        {
            return listener != null && listener.gameObject.name == UiPresentationCameraName;
        }
    }
}
