using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class WitchTowerPixelTexturePostprocessor : AssetPostprocessor
{
    private const string ReimportMenuPath = "Tools/MCP/Reimport Monster Presentation Textures";

    [MenuItem(ReimportMenuPath)]
    public static void ReimportMonsterPresentationTextures()
    {
        string[] folders =
        {
            "Assets/Resources/FamilyMonsterCards",
            "Assets/Resources/FamilyMonsters",
            "Assets/Resources/BattleEffects",
            "Assets/Resources/MonsterCardFrames",
            "Assets/Resources/EquipmentFrames",
            "Assets/Resources/UI/HomeMenu",
            "Assets/Resources/UI/FusionPage"
        };

        List<string> existingFolders = new List<string>();
        foreach (string folder in folders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                existingFolders.Add(folder);
            }
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", existingFolders.ToArray());
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                continue;
            }

            if (ConfigureImporter(path, importer))
            {
                importer.SaveAndReimport();
            }
        }

        Debug.Log($"Reimported {textureGuids.Length} monster presentation textures.");
    }

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (assetImporter is TextureImporter importer)
        {
            ConfigureImporter(normalizedPath, importer);
        }
    }

    private static bool ConfigureImporter(string assetPath, TextureImporter importer)
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        bool isBattlePixelArt =
            normalizedPath.StartsWith("Assets/Resources/MonsterBattle/") ||
            normalizedPath.StartsWith("Assets/Resources/BattleEffects/");
        bool isCardPortrait = normalizedPath.StartsWith("Assets/Resources/FamilyMonsterCards/");
        bool isLegacyPortrait = normalizedPath.StartsWith("Assets/Resources/FamilyMonsters/");
        bool isFrameTexture =
            normalizedPath.StartsWith("Assets/Resources/MonsterCardFrames/") ||
            normalizedPath.StartsWith("Assets/Resources/EquipmentFrames/");
        bool isHomeMenuTexture = normalizedPath.StartsWith("Assets/Resources/UI/HomeMenu/");
        bool isFusionPageTexture = normalizedPath.StartsWith("Assets/Resources/UI/FusionPage/");

        if (!isBattlePixelArt && !isCardPortrait && !isLegacyPortrait && !isFrameTexture && !isHomeMenuTexture && !isFusionPageTexture)
        {
            return false;
        }

        importer.textureType = isFrameTexture ? TextureImporterType.Default : TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = (isCardPortrait || isFrameTexture || isHomeMenuTexture || isFusionPageTexture) ? FilterMode.Trilinear : FilterMode.Point;
        importer.mipmapEnabled = isCardPortrait || isFrameTexture || isHomeMenuTexture || isFusionPageTexture;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        return true;
    }
}
