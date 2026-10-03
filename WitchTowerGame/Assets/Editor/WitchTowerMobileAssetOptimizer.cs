using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WitchTowerMobileAssetOptimizer
{
    private const string ApplyMenuPath = "WitchTower/Release/Apply Mobile Asset Optimization";
    private const int FullSizeTextureLimit = 2048;
    private const int StandardTextureLimit = 1024;
    private const int EffectTextureLimit = 512;
    private const int AstcCompressionQuality = 100;
    private const float BgmVorbisQuality = 0.62f;

    private static readonly string[] TextureFolders =
    {
        "Assets/Resources",
        "Assets/Art",
        "Assets/Branding"
    };

    [MenuItem(ApplyMenuPath)]
    public static void ApplyFromMenu()
    {
        ApplyFromCommandLine();
    }

    public static void ApplyFromCommandLine()
    {
        string[] texturePaths = FindAssetPaths("t:Texture2D", TextureFolders);
        string[] audioPaths = FindAssetPaths("t:AudioClip", new[] { "Assets/Resources/Audio" });

        int changedTextures = 0;
        int changedAudioClips = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in texturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                bool changed = ApplyTextureSettings(path, importer);
                if (!changed)
                {
                    continue;
                }

                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changedTextures++;
            }

            foreach (string path in audioPaths)
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                {
                    continue;
                }

                if (!ApplyAudioSettings(path, importer))
                {
                    continue;
                }

                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changedAudioClips++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"[MobileAssetOptimizer] Complete. Textures changed={changedTextures}/{texturePaths.Length}, " +
            $"audio changed={changedAudioClips}/{audioPaths.Length}. " +
            "Source image and audio files were not modified.");
    }

    private static bool ApplyTextureSettings(string path, TextureImporter importer)
    {
        int maxTextureSize = DetermineTextureLimit(path);
        bool changed = false;

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            changed = true;
        }

        changed |= ApplyPlatformSettings(importer, "Android", maxTextureSize);
        changed |= ApplyPlatformSettings(importer, "iPhone", maxTextureSize);
        return changed;
    }

    private static bool ApplyPlatformSettings(
        TextureImporter importer,
        string platformName,
        int maxTextureSize)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
        bool changed =
            !settings.overridden ||
            settings.maxTextureSize != maxTextureSize ||
            settings.format != TextureImporterFormat.ASTC_6x6 ||
            settings.textureCompression != TextureImporterCompression.CompressedHQ ||
            settings.compressionQuality != AstcCompressionQuality ||
            settings.crunchedCompression;

        if (!changed)
        {
            return false;
        }

        settings.name = platformName;
        settings.overridden = true;
        settings.maxTextureSize = maxTextureSize;
        settings.format = TextureImporterFormat.ASTC_6x6;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = AstcCompressionQuality;
        settings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(settings);
        return true;
    }

    private static int DetermineTextureLimit(string path)
    {
        string normalizedPath = path.Replace('\\', '/');
        string fileName = System.IO.Path.GetFileNameWithoutExtension(normalizedPath);

        if (normalizedPath.StartsWith("Assets/Branding/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/BattleBackgrounds/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/EquipmentBackgrounds/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/FormationUI/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/WorldMap/", StringComparison.Ordinal) ||
            ContainsAny(fileName, "Background", "Backdrop", "Board", "Screen", "MainFrame"))
        {
            return FullSizeTextureLimit;
        }

        if (normalizedPath.Contains("/BattleEffects/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/UI/EquipmentEnhance/", StringComparison.Ordinal) ||
            normalizedPath.Contains("/Effects/", StringComparison.Ordinal))
        {
            return EffectTextureLimit;
        }

        return StandardTextureLimit;
    }

    private static bool ApplyAudioSettings(string path, AudioImporter importer)
    {
        string normalizedPath = path.Replace('\\', '/');
        if (!normalizedPath.Contains("/Audio/BGM/", StringComparison.Ordinal))
        {
            return false;
        }

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        bool changed =
            settings.loadType != AudioClipLoadType.Streaming ||
            settings.compressionFormat != AudioCompressionFormat.Vorbis ||
            Mathf.Abs(settings.quality - BgmVorbisQuality) > 0.001f ||
            settings.preloadAudioData;

        if (!changed)
        {
            return false;
        }

        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = BgmVorbisQuality;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
        return true;
    }

    private static string[] FindAssetPaths(string filter, string[] folders)
    {
        string[] guids = AssetDatabase.FindAssets(filter, folders);
        var paths = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths.ToArray();
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
