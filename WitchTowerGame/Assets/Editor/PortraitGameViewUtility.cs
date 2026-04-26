#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WitchTower.EditorTools
{
    internal static class PortraitGameViewUtility
    {
        private const int PortraitWidth = 1080;
        private const int PortraitHeight = 1920;
        private const string PortraitLabel = "WitchTower 1080x1920 Portrait";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += ApplyPortraitPreviewOnLoad;
        }

        [MenuItem("Tools/WitchTower/View/Use 1080x1920 Portrait Preview")]
        private static void UsePortraitPreview()
        {
            if (!EnsurePortraitPresetExists(out int index))
            {
                Debug.LogWarning("[WitchTower] Could not create the portrait Game view preset.");
                return;
            }

            if (!SelectGameViewSize(index))
            {
                Debug.LogWarning("[WitchTower] Could not select the portrait Game view preset automatically.");
                return;
            }

            Debug.Log("[WitchTower] Game view set to 1080x1920 portrait preview.");
        }

        [MenuItem("Tools/WitchTower/View/Use 1080x1920 Portrait Preview", true)]
        private static bool ValidateUsePortraitPreview()
        {
            return !EditorApplication.isCompiling;
        }

        private static void EnsurePortraitPresetExists()
        {
            EnsurePortraitPresetExists(out _);
        }

        private static void ApplyPortraitPreviewOnLoad()
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            if (EnsurePortraitPresetExists(out int index))
            {
                SelectGameViewSize(index);
            }
        }

        private static bool EnsurePortraitPresetExists(out int portraitIndex)
        {
            portraitIndex = -1;
            object gameViewSizes = GetGameViewSizesInstance();
            if (gameViewSizes == null)
            {
                return false;
            }

            object sizeGroup = GetCurrentSizeGroup(gameViewSizes);
            if (sizeGroup == null)
            {
                return false;
            }

            string[] displayTexts = GetDisplayTexts(sizeGroup);
            portraitIndex = FindPortraitIndex(displayTexts);
            if (portraitIndex >= 0)
            {
                return true;
            }

            if (!AddPortraitSize(sizeGroup))
            {
                return false;
            }

            displayTexts = GetDisplayTexts(sizeGroup);
            portraitIndex = FindPortraitIndex(displayTexts);
            return portraitIndex >= 0;
        }

        private static object GetGameViewSizesInstance()
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            if (sizesType == null)
            {
                return null;
            }

            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            PropertyInfo instanceProperty = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            return instanceProperty?.GetValue(null, null);
        }

        private static object GetCurrentSizeGroup(object gameViewSizes)
        {
            if (gameViewSizes == null)
            {
                return null;
            }

            Type sizesType = gameViewSizes.GetType();
            MethodInfo getGroupMethod = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getGroupMethod == null)
            {
                return null;
            }

            object groupType = ResolveCurrentGroupType(gameViewSizes);
            return groupType != null ? getGroupMethod.Invoke(gameViewSizes, new[] { groupType }) : null;
        }

        private static object ResolveCurrentGroupType(object gameViewSizes)
        {
            Type sizesType = gameViewSizes.GetType();
            PropertyInfo currentGroupTypeProperty = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (currentGroupTypeProperty != null)
            {
                return currentGroupTypeProperty.GetValue(gameViewSizes, null);
            }

            Type groupType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType");
            if (groupType == null)
            {
                return null;
            }

            string activeTargetName = EditorUserBuildSettings.activeBuildTarget.ToString();
            string resolvedName = Enum.GetNames(groupType).Contains(activeTargetName) ? activeTargetName : "Standalone";
            return Enum.Parse(groupType, resolvedName);
        }

        private static string[] GetDisplayTexts(object sizeGroup)
        {
            if (sizeGroup == null)
            {
                return Array.Empty<string>();
            }

            MethodInfo displayTextsMethod = sizeGroup.GetType().GetMethod("GetDisplayTexts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return displayTextsMethod?.Invoke(sizeGroup, null) as string[] ?? Array.Empty<string>();
        }

        private static int FindPortraitIndex(string[] displayTexts)
        {
            if (displayTexts == null || displayTexts.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < displayTexts.Length; i += 1)
            {
                string text = displayTexts[i];
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (text.Contains(PortraitLabel, StringComparison.Ordinal) ||
                    text.Contains("1080x1920", StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool AddPortraitSize(object sizeGroup)
        {
            if (sizeGroup == null)
            {
                return false;
            }

            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            Type sizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            if (sizeType == null || sizeTypeEnum == null)
            {
                return false;
            }

            object fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
            ConstructorInfo constructor = sizeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) },
                null);

            if (constructor == null)
            {
                return false;
            }

            object customSize = constructor.Invoke(new object[] { fixedResolution, PortraitWidth, PortraitHeight, PortraitLabel });
            MethodInfo addCustomSizeMethod = sizeGroup.GetType().GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (addCustomSizeMethod == null)
            {
                return false;
            }

            addCustomSizeMethod.Invoke(sizeGroup, new[] { customSize });
            return true;
        }

        private static bool SelectGameViewSize(int index)
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return false;
            }

            EditorWindow gameViewWindow = EditorWindow.GetWindow(gameViewType);
            if (gameViewWindow == null)
            {
                return false;
            }

            PropertyInfo selectedSizeIndexProperty = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selectedSizeIndexProperty != null)
            {
                selectedSizeIndexProperty.SetValue(gameViewWindow, index, null);
                gameViewWindow.Focus();
                gameViewWindow.Repaint();
                return true;
            }

            MethodInfo sizeSelectionCallbackMethod = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sizeSelectionCallbackMethod != null)
            {
                sizeSelectionCallbackMethod.Invoke(gameViewWindow, new object[] { index, null });
                gameViewWindow.Focus();
                gameViewWindow.Repaint();
                return true;
            }

            return false;
        }
    }
}
#endif
