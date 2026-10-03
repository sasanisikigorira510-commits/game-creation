using UnityEditor;
using UnityEngine;
using WitchTower.Home;

public static class TenPullPresentationAssetInstaller
{
    public static void Install()
    {
        const string folder = "Assets/Resources/UI/GachaPage/TenPull";
        AssetDatabase.Refresh();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        string path = folder + "/TenPullPresentationSettings.asset";
        if (AssetDatabase.LoadAssetAtPath<TenPullPresentationSettings>(path) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TenPullPresentationSettings>(), path);
        AssetDatabase.SaveAssets();
        Debug.Log("Ten-pull image2 sprites and presentation configuration installed.");
    }
}
