using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Core;
using WitchTower.Managers;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    [ExecuteAlways]
    public sealed class GachaSceneController : MonoBehaviour
    {
        [SerializeField] private string homeSceneName = "HomeScene";

        private GachaPanelController panelController;

        private void OnEnable()
        {
            NormalizeCanvasScales();
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
            }
        }

        private void Start()
        {
            NormalizeCanvasScales();
            if (!Application.isPlaying)
            {
                ApplyEditorPreview();
                return;
            }

            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            EnsureRuntimeState();
            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            ShowGachaPanel(ReturnHome);
        }

        private void Update()
        {
            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnHome();
            }
        }

        public void ReturnHome()
        {
            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            SaveManager.Instance?.SaveCurrentGame();
            SceneTransitionGuard.LoadScene(homeSceneName);
        }

        private void ApplyEditorPreview()
        {
            ShowGachaPanel(null);
        }

        private void ShowGachaPanel(Action closeAction)
        {
            EnsureEventSystem();
            GachaPanelController panel = EnsurePanel();
            panel.Show(closeAction);
        }

        private GachaPanelController EnsurePanel()
        {
            if (panelController != null)
            {
                return panelController;
            }

            Canvas canvas = EnsureCanvas();
            Transform existingPanel = canvas.transform.Find("GachaScenePanel");
            if (existingPanel != null)
            {
                panelController = existingPanel.GetComponent<GachaPanelController>();
            }

            if (panelController == null)
            {
                GameObject panelObject = new GameObject("GachaScenePanel", typeof(RectTransform), typeof(Image), typeof(GachaPanelController));
                panelObject.transform.SetParent(canvas.transform, false);
                panelController = panelObject.GetComponent<GachaPanelController>();
            }

            ConfigureFullScreenRect(panelController.GetComponent<RectTransform>());
            panelController.gameObject.SetActive(true);
            panelController.transform.SetAsLastSibling();
            return panelController;
        }

        private static void EnsureRuntimeState()
        {
            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            Application.runInBackground = true;
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable)
            {
                return;
            }

            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureAudioManager();
            ManagerFactory.EnsureUiPresentationCamera();

            if (SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            MasterDataManager.Instance?.Initialize();

            if (GameManager.Instance.PlayerProfile == null && SaveManager.Instance.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        private Canvas EnsureCanvas()
        {
            // OnlineInputBlocker is persistent and can be inactive. A global
            // first-Canvas lookup can parent the live panel under that blocker,
            // leaving only the serialized editor preview visible in this scene.
            Canvas canvas = null;
            foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene && candidate.name == "GachaCanvas")
                {
                    canvas = candidate;
                    break;
                }
            }
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("GachaCanvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.localScale = Vector3.one;
                canvasRect.sizeDelta = new Vector2(1080f, 1920f);
                canvas = canvasObject.AddComponent<Canvas>();
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.transform.localScale = Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>(true) != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void ConfigureFullScreenRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private void NormalizeCanvasScales()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null && canvas.gameObject.scene == gameObject.scene && canvas.name == "GachaCanvas")
                {
                    canvas.transform.localScale = Vector3.one;
                }
            }
        }
    }
}
