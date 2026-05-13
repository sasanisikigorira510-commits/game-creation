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
            "Assets/Resources/MonsterBattle",
            "Assets/Resources/BattleEffects",
            "Assets/Resources/MonsterCardFrames",
            "Assets/Resources/EquipmentFrames",
            "Assets/Resources/UI/HomeMenu",
            "Assets/Resources/UI/FusionPage",
            "Assets/Resources/UI/GachaPage"
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
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
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
        bool isMonsterBattleTexture = normalizedPath.StartsWith("Assets/Resources/MonsterBattle/");
        bool isBattlePixelArt =
            isMonsterBattleTexture ||
            normalizedPath.StartsWith("Assets/Resources/BattleEffects/");
        bool isCardPortrait = normalizedPath.StartsWith("Assets/Resources/FamilyMonsterCards/");
        bool isLegacyPortrait = normalizedPath.StartsWith("Assets/Resources/FamilyMonsters/");
        bool isFrameTexture =
            normalizedPath.StartsWith("Assets/Resources/MonsterCardFrames/") ||
            normalizedPath.StartsWith("Assets/Resources/EquipmentFrames/");
        bool isHomeMenuTexture = normalizedPath.StartsWith("Assets/Resources/UI/HomeMenu/");
        bool isFusionPageTexture = normalizedPath.StartsWith("Assets/Resources/UI/FusionPage/");
        bool isGachaPageTexture = normalizedPath.StartsWith("Assets/Resources/UI/GachaPage/");

        if (!isBattlePixelArt && !isCardPortrait && !isLegacyPortrait && !isFrameTexture && !isHomeMenuTexture && !isFusionPageTexture && !isGachaPageTexture)
        {
            return false;
        }

        importer.textureType = isFrameTexture ? TextureImporterType.Default : TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = (isCardPortrait || isFrameTexture || isHomeMenuTexture || isFusionPageTexture || isGachaPageTexture) ? FilterMode.Trilinear : FilterMode.Point;
        importer.mipmapEnabled = isCardPortrait || isFrameTexture || isHomeMenuTexture || isFusionPageTexture || isGachaPageTexture;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = isMonsterBattleTexture;
        return true;
    }
}
