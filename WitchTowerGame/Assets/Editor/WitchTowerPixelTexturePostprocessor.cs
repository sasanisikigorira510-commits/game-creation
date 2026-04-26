using UnityEditor;
using UnityEngine;

public sealed class WitchTowerPixelTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("Assets/Resources/MonsterBattle/") &&
            !normalizedPath.StartsWith("Assets/Resources/FamilyMonsters/") &&
            !normalizedPath.StartsWith("Assets/Resources/BattleEffects/"))
        {
            return;
        }

        if (assetImporter is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
    }
}
