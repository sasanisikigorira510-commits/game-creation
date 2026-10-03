using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WitchTowerMobileTextureCompressionTrial
{
    private const string ApplyMenuPath = "WitchTower/Release/Apply ASTC 6x6 Texture Trial";
    private const string RestoreMenuPath = "WitchTower/Release/Restore Texture Settings From Trial Backup";
    private const string RepairGachaTextureMenuPath = "WitchTower/Release/Repair Trial Gacha Texture Import";
    private const string BackupRelativePath = "Library/WitchTowerTextureCompressionTrialBackup.json";
    private const string GachaMainFramePath = "Assets/Resources/UI/GachaPage/GachaMainFrame.png";
    private const int AstcCompressionQuality = 100;

    private static readonly string[] RuntimeTextureFolders =
    {
        "Assets/Resources",
        "Assets/Art",
        "Assets/Branding"
    };

    [MenuItem(ApplyMenuPath)]
    public static void ApplyFromMenu()
    {
        ApplyTrial();
    }

    [MenuItem(RestoreMenuPath)]
    public static void RestoreFromMenu()
    {
        RestoreTrial();
    }

    [MenuItem(RepairGachaTextureMenuPath)]
    public static void RepairGachaTextureImportFromMenu()
    {
        AssetDatabase.ImportAsset(
            GachaMainFramePath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GachaMainFramePath);
        if (texture == null)
        {
            Debug.LogError($"[TextureCompressionTrial] Failed to import {GachaMainFramePath}.");
            return;
        }

        Debug.Log(
            $"[TextureCompressionTrial] Reimported {GachaMainFramePath} successfully " +
            $"({texture.width}x{texture.height}, format={texture.format}).");
    }

    private static void ApplyTrial()
    {
        string[] texturePaths = FindRuntimeTexturePaths();
        TrialBackup backup = CaptureBackup(texturePaths);
        string backupPath = GetBackupPath();

        if (!File.Exists(backupPath))
        {
            File.WriteAllText(backupPath, JsonUtility.ToJson(backup, true));
            Debug.Log($"[TextureCompressionTrial] Saved baseline settings for {backup.textures.Count} textures to {backupPath}.");
        }
        else
        {
            Debug.Log($"[TextureCompressionTrial] Preserved existing baseline backup at {backupPath}.");
        }

        int changedCount = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in texturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                bool changed = ApplyAstcPlatformSettings(importer, "Android");
                changed |= ApplyAstcPlatformSettings(importer, "iPhone");
                if (!changed)
                {
                    continue;
                }

                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changedCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"[TextureCompressionTrial] Applied ASTC 6x6 to Android/iOS for {changedCount} of {texturePaths.Length} runtime textures. " +
            "Resolution, filtering, sprite settings, and mipmaps were left unchanged.");
    }

    private static void RestoreTrial()
    {
        string backupPath = GetBackupPath();
        if (!File.Exists(backupPath))
        {
            Debug.LogError($"[TextureCompressionTrial] Backup not found: {backupPath}");
            return;
        }

        TrialBackup backup = JsonUtility.FromJson<TrialBackup>(File.ReadAllText(backupPath));
        if (backup == null || backup.textures == null)
        {
            Debug.LogError($"[TextureCompressionTrial] Backup is invalid: {backupPath}");
            return;
        }

        int restoredCount = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (TextureBackup texture in backup.textures)
            {
                if (AssetImporter.GetAtPath(texture.path) is not TextureImporter importer)
                {
                    continue;
                }

                RestorePlatformSettings(importer, texture.android);
                RestorePlatformSettings(importer, texture.iphone);
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(texture.path);
                restoredCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[TextureCompressionTrial] Restored mobile texture settings for {restoredCount} textures from {backupPath}.");
    }

    private static bool ApplyAstcPlatformSettings(TextureImporter importer, string platformName)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
        bool changed =
            !settings.overridden ||
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
        settings.format = TextureImporterFormat.ASTC_6x6;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = AstcCompressionQuality;
        settings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(settings);
        return true;
    }

    private static TrialBackup CaptureBackup(IEnumerable<string> texturePaths)
    {
        TrialBackup backup = new TrialBackup
        {
            schemaVersion = 1,
            createdUtc = DateTime.UtcNow.ToString("O")
        };

        foreach (string path in texturePaths)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                continue;
            }

            backup.textures.Add(new TextureBackup
            {
                path = path,
                android = CapturePlatformSettings(importer.GetPlatformTextureSettings("Android")),
                iphone = CapturePlatformSettings(importer.GetPlatformTextureSettings("iPhone"))
            });
        }

        return backup;
    }

    private static PlatformSettingsBackup CapturePlatformSettings(TextureImporterPlatformSettings settings)
    {
        return new PlatformSettingsBackup
        {
            name = settings.name,
            overridden = settings.overridden,
            maxTextureSize = settings.maxTextureSize,
            resizeAlgorithm = (int)settings.resizeAlgorithm,
            format = (int)settings.format,
            textureCompression = (int)settings.textureCompression,
            compressionQuality = settings.compressionQuality,
            crunchedCompression = settings.crunchedCompression,
            allowsAlphaSplitting = settings.allowsAlphaSplitting,
            androidEtc2FallbackOverride = (int)settings.androidETC2FallbackOverride
        };
    }

    private static void RestorePlatformSettings(TextureImporter importer, PlatformSettingsBackup backup)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(backup.name);
        settings.name = backup.name;
        settings.overridden = backup.overridden;
        settings.maxTextureSize = backup.maxTextureSize;
        settings.resizeAlgorithm = (TextureResizeAlgorithm)backup.resizeAlgorithm;
        settings.format = (TextureImporterFormat)backup.format;
        settings.textureCompression = (TextureImporterCompression)backup.textureCompression;
        settings.compressionQuality = backup.compressionQuality;
        settings.crunchedCompression = backup.crunchedCompression;
        settings.allowsAlphaSplitting = backup.allowsAlphaSplitting;
        settings.androidETC2FallbackOverride =
            (AndroidETC2FallbackOverride)backup.androidEtc2FallbackOverride;
        importer.SetPlatformTextureSettings(settings);
    }

    private static string[] FindRuntimeTexturePaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", RuntimeTextureFolders);
        List<string> paths = new List<string>(guids.Length);
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

    private static string GetBackupPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        return Path.Combine(projectRoot, BackupRelativePath);
    }

    [Serializable]
    private sealed class TrialBackup
    {
        public int schemaVersion;
        public string createdUtc;
        public List<TextureBackup> textures = new List<TextureBackup>();
    }

    [Serializable]
    private sealed class TextureBackup
    {
        public string path;
        public PlatformSettingsBackup android;
        public PlatformSettingsBackup iphone;
    }

    [Serializable]
    private sealed class PlatformSettingsBackup
    {
        public string name;
        public bool overridden;
        public int maxTextureSize;
        public int resizeAlgorithm;
        public int format;
        public int textureCompression;
        public int compressionQuality;
        public bool crunchedCompression;
        public bool allowsAlphaSplitting;
        public int androidEtc2FallbackOverride;
    }
}
