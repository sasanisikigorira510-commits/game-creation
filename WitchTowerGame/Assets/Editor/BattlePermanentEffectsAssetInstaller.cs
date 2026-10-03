using UnityEditor;
using UnityEngine;

public static class BattlePermanentEffectsAssetInstaller
{
    public static void Install()
    {
        const string path="Assets/Resources/UI/BattlePermanentEffects/Panel.png";
        AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spriteBorder=new Vector4(48,48,48,48); importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
}
