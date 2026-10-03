using UnityEditor;
using UnityEngine;

// These illustrations fill a phone screen and must not use nearest-neighbor magnification.
public sealed class TenPullAnimeTextureImporter : AssetPostprocessor
{
    public override int GetPostprocessOrder() => 100;
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/UI/GachaPage/TenPull/Anime/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        bool currentArtwork = assetPath.Contains("/Continuous/") ||
            assetPath.EndsWith("/Opening_2.png") || assetPath.EndsWith("/Normal_6.png") ||
            assetPath.EndsWith("/Upper_6.png") || assetPath.EndsWith("/Normal_3.png") || assetPath.EndsWith("/Upper_3.png");
        importer.filterMode = currentArtwork ? FilterMode.Bilinear : FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = currentArtwork ? 2048 : 1024;
        if (!currentArtwork) return;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Android", "iPhone", "Standalone" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
