using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.UI
{
    /// <summary>
    /// Covers the current frame while a single-mode scene transition is being
    /// prepared. This prevents the previous scene or a legacy canvas from
    /// appearing for one frame on touch-driven devices.
    /// </summary>
    public static class SceneTransitionGuard
    {
        private const string RootName = "WitchTowerSceneTransitionGuard";
        private static GameObject overlayRoot;

        public static void LoadScene(string sceneName)
        {
            if (WitchTower.Managers.SaveManager.Instance?.StorageAccessAvailable == false) return;
            if (WitchTower.Managers.SaveManager.Instance != null &&
                WitchTower.Save.AccountDeletionStorage.Blocks(WitchTower.Managers.SaveManager.Instance.RootDirectory)) return;
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            // Validate before covering the current screen. A failed load must
            // leave that screen visible and interactive.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                throw new ArgumentException("Scene is not available in the build: " + sceneName, nameof(sceneName));
            }

            EnsureOverlay();
            overlayRoot.SetActive(true);
            try
            {
                // Unity completes LoadScene on the next frame. The cover
                // belongs to the outgoing scene and is destroyed with it,
                // before the destination's Awake/Start build their UI.
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            catch
            {
                overlayRoot.SetActive(false);
                UnityEngine.Object.Destroy(overlayRoot);
                overlayRoot = null;
                throw;
            }
        }

        private static void EnsureOverlay()
        {
            if (overlayRoot != null)
            {
                return;
            }

            overlayRoot = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Never persist a transition Canvas: screen builders could select
            // it as their parent and disappear when the cover is hidden.

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2341f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image cover = overlayRoot.AddComponent<Image>();
            cover.color = Color.black;
            cover.raycastTarget = true;
            RectTransform coverRect = cover.rectTransform;
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.offsetMin = Vector2.zero;
            coverRect.offsetMax = Vector2.zero;
        }
    }
}
