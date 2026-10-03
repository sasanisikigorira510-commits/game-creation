using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WitchTowerBrandingConfigurator
{
    private const string FullIconPath = "Assets/Branding/AppIconFinal.png";
    private const string AdaptiveForegroundPath = "Assets/Branding/AppIconAdaptiveForeground.png";
    private const string AdaptiveBackgroundPath = "Assets/Branding/AppIconAdaptiveBackground.png";

    [MenuItem("WitchTower/Release/Apply App Icons")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureIconImporter(FullIconPath, alphaIsTransparency: false);
        ConfigureIconImporter(AdaptiveForegroundPath, alphaIsTransparency: true);
        ConfigureIconImporter(AdaptiveBackgroundPath, alphaIsTransparency: false);

        Texture2D fullIcon = LoadIcon(FullIconPath);
        Texture2D adaptiveForeground = LoadIcon(AdaptiveForegroundPath);
        Texture2D adaptiveBackground = LoadIcon(AdaptiveBackgroundPath);

        ConfigurePlatform(NamedBuildTarget.Android, fullIcon, adaptiveForeground, adaptiveBackground);
        ConfigurePlatform(NamedBuildTarget.iOS, fullIcon, fullIcon, fullIcon);

        AssetDatabase.SaveAssets();
        Debug.Log("[Branding] Android and iOS app icons were applied.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static Texture2D LoadIcon(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            throw new InvalidOperationException($"Required app icon is missing or not imported: {path}");
        }

        return texture;
    }

    private static void ConfigureIconImporter(string path, bool alphaIsTransparency)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            throw new InvalidOperationException($"Required app icon importer is missing: {path}");
        }

        bool changed =
            importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            importer.maxTextureSize != 1024 ||
            importer.alphaIsTransparency != alphaIsTransparency;
        if (!changed)
        {
            return;
        }

        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 1024;
        importer.alphaIsTransparency = alphaIsTransparency;
        importer.SaveAndReimport();
    }

    private static void ConfigurePlatform(
        NamedBuildTarget target,
        Texture2D fullIcon,
        Texture2D adaptiveForeground,
        Texture2D adaptiveBackground)
    {
        PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKinds(target);
        foreach (PlatformIconKind kind in kinds)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (PlatformIcon icon in icons)
            {
                int layerCount = Math.Max(1, icon.maxLayerCount);
                Texture2D[] textures = new Texture2D[layerCount];

                if (target == NamedBuildTarget.Android && layerCount >= 2)
                {
                    // Unity's Android adaptive icon API expects background first,
                    // followed by the transparent foreground layer.
                    textures[0] = adaptiveBackground;
                    textures[1] = adaptiveForeground;
                    for (int layer = 2; layer < layerCount; layer++)
                    {
                        textures[layer] = adaptiveForeground;
                    }
                }
                else
                {
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        textures[layer] = fullIcon;
                    }
                }

                icon.SetTextures(textures);
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
            Debug.Log(
                $"[Branding] {target.TargetName}/{kind}: configured {icons.Length} icon slot(s).");
        }
    }
}
