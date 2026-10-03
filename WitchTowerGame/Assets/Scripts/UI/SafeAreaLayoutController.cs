using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchTower.UI
{
    /// <summary>
    /// Keeps interactive UI inside the device safe area while allowing decorative
    /// backgrounds to continue drawing edge-to-edge.
    /// </summary>
    public sealed class SafeAreaLayoutController : MonoBehaviour
    {
        private const float SafeAreaChangeThresholdPixels = 2f;

        private static readonly HashSet<string> FullSafeAreaRootNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnifiedHomeMenu",
            "MonsterDexPanel",
            "GoldShopPanel",
            "PaidShopPanel",
            "RebirthSkillTreePanel",
            "DungeonSelectionPanel",
            "GachaContractHomeRoot",
            "GachaResultStageRoot",
            "TenPullSafeContent",
            "FusionSelectionRoot",
            "FusionResultStageRoot",
            "FormationUiRoot",
            "FormationPanelRoot",
            "EquipmentSceneRoot"
        };

        private static readonly HashSet<string> GachaSafeAreaTargetNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ContractRitePanel",
            "SummonTitlePanel",
            "GachaTicketPanel",
            "TenPullButton",
            "GachaRatesPanel",
            "SinglePullButton",
            "BackButton"
        };

        private static readonly HashSet<string> FusionSafeAreaTargetNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "FusionMainPanel"
        };

        private static readonly HashSet<string> BattleSafeAreaTargetNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "RetireButton",
            "BattleWaveHud",
            "BattleSkillPanel",
            "BattlePermanentEffectsRoot",
            "BattleAnnouncementBanner"
        };

        private static SafeAreaLayoutController instance;
        private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
        private bool applyingBeforeRender;
        private int lastAppliedFitterCount = -1;

        /// <summary>
        /// Optional test/preview override. Runtime builds leave this null.
        /// </summary>
        public static Rect? SafeAreaOverride { get; set; }
        public static float? VerticalInsetRatioOverride { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root = new GameObject("SafeAreaLayoutController");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SafeAreaLayoutController>();
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += HandleBeforeCanvasRender;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= HandleBeforeCanvasRender;
        }

        private void HandleBeforeCanvasRender()
        {
            if (applyingBeforeRender)
            {
                return;
            }

            // Home builds pages on demand during input handling. A timed Update
            // scan leaves those pages at their unadjusted position for a frame
            // (or more). Discover them before layout/graphics rebuild, including
            // multiple ForceUpdateCanvases calls in the same frame.
            applyingBeforeRender = true;
            try
            {
                Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
                Rect safeArea = ResolveSafeArea(screenSize);
                bool displayChanged = screenSize != lastScreenSize ||
                    !ApproximatelyEqual(safeArea, lastSafeArea, SafeAreaChangeThresholdPixels);
                ApplySafeArea(safeArea, screenSize, displayChanged);
            }
            finally
            {
                applyingBeforeRender = false;
            }
        }

        private static Rect ResolveSafeArea(Vector2Int screenSize)
        {
            if (VerticalInsetRatioOverride.HasValue)
            {
                float inset = screenSize.y * Mathf.Clamp(
                    VerticalInsetRatioOverride.Value,
                    0f,
                    0.45f);
                return new Rect(0f, inset, screenSize.x, screenSize.y - (inset * 2f));
            }

            return SafeAreaOverride ?? Screen.safeArea;
        }

        private void ApplySafeArea(Rect safeArea, Vector2Int screenSize, bool displayChanged)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int appliedFitterCount = 0;
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null ||
                    (canvas.transform.parent != null &&
                     canvas.transform.parent.GetComponentInParent<Canvas>(true) != null))
                {
                    continue;
                }

                ApplyToChildren(canvas.transform, safeArea, screenSize, displayChanged, ref appliedFitterCount);
            }

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            if (appliedFitterCount != lastAppliedFitterCount)
            {
                Debug.Log(
                    $"[SafeAreaLayout] Applied {appliedFitterCount} fitter(s) in " +
                    $"{SceneManager.GetActiveScene().name}; safeArea={safeArea}, screen={screenSize}.");
                lastAppliedFitterCount = appliedFitterCount;
            }
        }

        private static void ApplyToChildren(Transform parent, Rect safeArea, Vector2Int screenSize,
            bool displayChanged, ref int appliedFitterCount)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child is RectTransform rect && IsSafeAreaTarget(rect))
                {
                    SafeAreaFitter fitter = rect.GetComponent<SafeAreaFitter>();
                    if (fitter == null)
                        fitter = rect.gameObject.AddComponent<SafeAreaFitter>();
                    if (displayChanged || !fitter.IsLayoutCurrent)
                        fitter.Apply(safeArea, screenSize);
                    appliedFitterCount++;
                    // Everything below this root already inherits the inset.
                    // Pruning also avoids scanning thousands of cards each render.
                    continue;
                }

                if (child.GetComponent<SafeAreaFitter>() == null)
                    ApplyToChildren(child, safeArea, screenSize, displayChanged, ref appliedFitterCount);
            }
        }

        private static bool ApproximatelyEqual(Rect a, Rect b, float threshold)
        {
            if (b.width < 0f || b.height < 0f)
            {
                return false;
            }

            return Mathf.Abs(a.xMin - b.xMin) <= threshold &&
                Mathf.Abs(a.yMin - b.yMin) <= threshold &&
                Mathf.Abs(a.width - b.width) <= threshold &&
                Mathf.Abs(a.height - b.height) <= threshold;
        }

        private static bool IsSafeAreaTarget(RectTransform rectTransform)
        {
            string objectName = rectTransform.gameObject.name;
            if (FullSafeAreaRootNames.Contains(objectName))
            {
                return true;
            }

            string parentName = rectTransform.parent != null ? rectTransform.parent.name : string.Empty;
            if (string.Equals(parentName, "RuntimeEquipmentSceneRoot", StringComparison.Ordinal))
            {
                return string.Equals(objectName, HomeReturnButtonStyle.DefaultObjectName, StringComparison.Ordinal);
            }

            if (string.Equals(parentName, "GachaScenePanel", StringComparison.Ordinal))
            {
                return GachaSafeAreaTargetNames.Contains(objectName);
            }

            if (string.Equals(parentName, "FusionScenePanel", StringComparison.Ordinal))
            {
                return FusionSafeAreaTargetNames.Contains(objectName);
            }

            if (string.Equals(parentName, "BattleMinimalCanvas", StringComparison.Ordinal))
            {
                return BattleSafeAreaTargetNames.Contains(objectName);
            }

            return false;
        }

        public static Vector2 RemapAnchor(Vector2 baselineAnchor, Rect safeArea, Vector2 screenSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return baselineAnchor;
            }

            Vector2 normalizedMin = new Vector2(
                Mathf.Clamp01(safeArea.xMin / screenSize.x),
                Mathf.Clamp01(safeArea.yMin / screenSize.y));
            Vector2 normalizedMax = new Vector2(
                Mathf.Clamp01(safeArea.xMax / screenSize.x),
                Mathf.Clamp01(safeArea.yMax / screenSize.y));

            return new Vector2(
                Mathf.Lerp(normalizedMin.x, normalizedMax.x, baselineAnchor.x),
                Mathf.Lerp(normalizedMin.y, normalizedMax.y, baselineAnchor.y));
        }
    }

    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Vector2 baselineAnchorMin;
        private Vector2 baselineAnchorMax;
        private Vector2 appliedAnchorMin;
        private Vector2 appliedAnchorMax;
        private Vector2 fullBleedAnchorMin;
        private Vector2 fullBleedAnchorMax;
        private bool hasFullBleedArea;
        private bool initialized;
        public bool HasApplied { get; private set; }

        public bool IsLayoutCurrent
        {
            get
            {
                if (!HasApplied || rectTransform == null ||
                    rectTransform.anchorMin != appliedAnchorMin || rectTransform.anchorMax != appliedAnchorMax)
                    return false;
                if (!hasFullBleedArea) return true;
                for (int i = 0; i < rectTransform.childCount; i++)
                {
                    if (rectTransform.GetChild(i) is RectTransform child && IsFullBleedVisual(child.name) &&
                        (child.anchorMin != fullBleedAnchorMin || child.anchorMax != fullBleedAnchorMax ||
                         child.offsetMin != Vector2.zero || child.offsetMax != Vector2.zero))
                        return false;
                }
                return true;
            }
        }

        public void Apply(Rect safeArea, Vector2 screenSize)
        {
            if (!initialized)
            {
                rectTransform = transform as RectTransform;
                if (rectTransform == null)
                {
                    enabled = false;
                    return;
                }

                baselineAnchorMin = rectTransform.anchorMin;
                baselineAnchorMax = rectTransform.anchorMax;
                initialized = true;
            }
            else if (rectTransform.anchorMin != appliedAnchorMin || rectTransform.anchorMax != appliedAnchorMax)
            {
                // A lazy builder can reset an already discovered inactive root.
                // Its new anchors are the baseline, not an additional safe inset.
                baselineAnchorMin = rectTransform.anchorMin;
                baselineAnchorMax = rectTransform.anchorMax;
            }

            appliedAnchorMin = SafeAreaLayoutController.RemapAnchor(baselineAnchorMin, safeArea, screenSize);
            appliedAnchorMax = SafeAreaLayoutController.RemapAnchor(baselineAnchorMax, safeArea, screenSize);
            if (rectTransform.anchorMin != appliedAnchorMin) rectTransform.anchorMin = appliedAnchorMin;
            if (rectTransform.anchorMax != appliedAnchorMax) rectTransform.anchorMax = appliedAnchorMax;
            ExpandFullBleedChildren(safeArea, screenSize);
            HasApplied = true;
        }

        private void ExpandFullBleedChildren(Rect safeArea, Vector2 screenSize)
        {
            Vector2 safeMin = SafeAreaLayoutController.RemapAnchor(Vector2.zero, safeArea, screenSize);
            Vector2 safeMax = SafeAreaLayoutController.RemapAnchor(Vector2.one, safeArea, screenSize);
            Vector2 safeSize = safeMax - safeMin;
            hasFullBleedArea = safeSize.x > 0f && safeSize.y > 0f;
            if (safeSize.x <= 0f || safeSize.y <= 0f)
            {
                return;
            }

            fullBleedAnchorMin = new Vector2(-safeMin.x / safeSize.x, -safeMin.y / safeSize.y);
            fullBleedAnchorMax = new Vector2((1f - safeMin.x) / safeSize.x, (1f - safeMin.y) / safeSize.y);

            for (int i = 0; i < rectTransform.childCount; i++)
            {
                if (rectTransform.GetChild(i) is not RectTransform childRect ||
                    !IsFullBleedVisual(childRect.gameObject.name))
                {
                    continue;
                }

                if (childRect.anchorMin != fullBleedAnchorMin) childRect.anchorMin = fullBleedAnchorMin;
                if (childRect.anchorMax != fullBleedAnchorMax) childRect.anchorMax = fullBleedAnchorMax;
                if (childRect.offsetMin != Vector2.zero) childRect.offsetMin = Vector2.zero;
                if (childRect.offsetMax != Vector2.zero) childRect.offsetMax = Vector2.zero;
            }
        }

        private static bool IsFullBleedVisual(string objectName)
        {
            return objectName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Backdrop", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
