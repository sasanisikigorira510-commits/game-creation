using UnityEditor;
using UnityEngine;
using WitchTower.UI;

public static class WitchTowerSafeAreaPreview
{
    private const string EnableMenuPath = "WitchTower/Release/Preview Notched Safe Area";
    private const string DisableMenuPath = "WitchTower/Release/Clear Safe Area Preview";
    private const float PreviewInsetRatio = 0.04f;

    [MenuItem(EnableMenuPath)]
    public static void EnablePreview()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SafeAreaPreview] Enter Play Mode before enabling the preview.");
            return;
        }

        SafeAreaLayoutController.SafeAreaOverride = null;
        SafeAreaLayoutController.VerticalInsetRatioOverride = PreviewInsetRatio;
        Debug.Log($"[SafeAreaPreview] Enabled {PreviewInsetRatio:P0} simulated top/bottom insets.");
    }

    [MenuItem(DisableMenuPath)]
    public static void DisablePreview()
    {
        SafeAreaLayoutController.SafeAreaOverride = null;
        SafeAreaLayoutController.VerticalInsetRatioOverride = null;
        Debug.Log("[SafeAreaPreview] Cleared simulated safe area.");
    }
}
