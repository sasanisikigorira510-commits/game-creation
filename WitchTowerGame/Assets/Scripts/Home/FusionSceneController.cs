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
    public sealed class FusionSceneController : MonoBehaviour
    {
        [SerializeField] private string homeSceneName = "HomeScene";

        private MonsterFusionPanelController panelController;

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

            ShowFusionPanel(ReturnHome);
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
            ShowFusionPanel(null);
        }

        private void ShowFusionPanel(Action closeAction)
        {
            EnsureEventSystem();
            MonsterFusionPanelController panel = EnsurePanel();
            panel.Show(closeAction);
        }

        private MonsterFusionPanelController EnsurePanel()
        {
            if (panelController != null)
            {
                return panelController;
            }

            Canvas canvas = EnsureCanvas();
            Transform existingPanel = canvas.transform.Find("FusionScenePanel");
            if (existingPanel != null)
            {
                panelController = existingPanel.GetComponent<MonsterFusionPanelController>();
            }

            if (panelController == null)
            {
                GameObject panelObject = new GameObject("FusionScenePanel", typeof(RectTransform), typeof(Image), typeof(MonsterFusionPanelController));
                panelObject.transform.SetParent(canvas.transform, false);
                panelController = panelObject.GetComponent<MonsterFusionPanelController>();
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
            // A completed purchase leaves its persistent input-blocker canvas
            // inactive. Never build the live panel under that foreign overlay
            // and leave this scene's unbound editor preview on screen.
            Canvas canvas = SceneCanvasOwner.Find(this, "FusionCanvas");
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("FusionCanvas", typeof(RectTransform));
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
            Canvas canvas = SceneCanvasOwner.Find(this, "FusionCanvas");
            if (canvas != null)
            {
                canvas.transform.localScale = Vector3.one;
            }
        }
    }
}
