using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class UnityMcpExternalAssetImporter
{
    private const string RootFolder = "Assets/Art/External";
    private const string DerivedFolder = RootFolder + "/Derived";

    private sealed class DownloadAsset
    {
        public string Url;
        public string TargetPath;
        public string SourcePage;
        public string License;
        public bool ExtractZip;
    }

    [MenuItem("Tools/MCP/Import Free Pixel Assets")]
    public static void ImportFreePixelAssets()
    {
        DownloadAsset[] assets =
        {
            new DownloadAsset
            {
                Url = "https://opengameart.org/sites/default/files/mini_fantasy_sprites_oga_ver.png",
                TargetPath = RootFolder + "/MiniFantasy/mini_fantasy_sprites_oga_ver.png",
                SourcePage = "https://opengameart.org/content/mini-fantasy-sprites",
                License = "CC0"
            },
            new DownloadAsset
            {
                Url = "https://opengameart.org/sites/default/files/RPGTileset_0.png",
                TargetPath = RootFolder + "/RpgTileset/RPGTileset.png",
                SourcePage = "https://opengameart.org/content/stunning-pixel-art-rpg-tileset",
                License = "CC0"
            },
            new DownloadAsset
            {
                Url = "https://opengameart.org/sites/default/files/Basic%20Green%20Monster%20Colection%20Batch%201-3_0.zip",
                TargetPath = RootFolder + "/GreenMonsters/basic_green_monsters_batch1-3.zip",
                SourcePage = "https://opengameart.org/content/basic-green-monster-collection",
                License = "CC0",
                ExtractZip = true
            }
        };

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Art", "External"));

        StringBuilder manifest = new StringBuilder();
        manifest.AppendLine("Imported Free Pixel Assets");
        manifest.AppendLine();

        foreach (DownloadAsset asset in assets)
        {
            string absoluteTarget = ToAbsolutePath(asset.TargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget) ?? Application.dataPath);

            byte[] bytes = DownloadBytes(asset.Url);
            File.WriteAllBytes(absoluteTarget, bytes);

            manifest.AppendLine(Path.GetFileName(asset.TargetPath));
            manifest.AppendLine("Source: " + asset.SourcePage);
            manifest.AppendLine("License: " + asset.License);
            manifest.AppendLine();

            if (asset.ExtractZip)
            {
                string extractFolder = Path.Combine(Path.GetDirectoryName(absoluteTarget) ?? Application.dataPath, "Extracted");
                if (Directory.Exists(extractFolder))
                {
                    Directory.Delete(extractFolder, true);
                }

                Directory.CreateDirectory(extractFolder);
                ZipFile.ExtractToDirectory(absoluteTarget, extractFolder);
            }
        }

        string manifestPath = ToAbsolutePath(RootFolder + "/README_FreeAssets.txt");
        File.WriteAllText(manifestPath, manifest.ToString(), Encoding.UTF8);

        AssetDatabase.Refresh();
        ConfigureImportedSprites();
        GenerateDerivedSprites();
        Debug.Log("[UnityMcpExternalAssetImporter] Free pixel assets imported.");
    }

    private static void ConfigureImportedSprites()
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { RootFolder });
        foreach (string guid in textureGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static void GenerateDerivedSprites()
    {
        Directory.CreateDirectory(ToAbsolutePath(DerivedFolder));

        CropTextureToFile(
            RootFolder + "/MiniFantasy/mini_fantasy_sprites_oga_ver.png",
            DerivedFolder + "/witch_idle.png",
            72, 16, 32, 32);

        CropTextureToFile(
            RootFolder + "/MiniFantasy/mini_fantasy_sprites_oga_ver.png",
            DerivedFolder + "/witch_cast.png",
            72, 112, 32, 32);

        CropTextureToFile(
            RootFolder + "/RpgTileset/RPGTileset.png",
            DerivedFolder + "/tree.png",
            0, 112, 64, 64);

        CropTextureToFile(
            RootFolder + "/RpgTileset/RPGTileset.png",
            DerivedFolder + "/dirt_tile.png",
            0, 0, 32, 32);

        CropTextureToFile(
            RootFolder + "/RpgTileset/RPGTileset.png",
            DerivedFolder + "/grass_tile.png",
            0, 80, 32, 32);

        string monsterSource = RootFolder + "/GreenMonsters/Extracted/Basic Green Monster Colection/Death Mage Elf.png";
        string monsterTarget = ToAbsolutePath(DerivedFolder + "/enemy_death_mage_elf.png");
        File.Copy(ToAbsolutePath(monsterSource), monsterTarget, true);

        AssetDatabase.Refresh();

        string[] derivedGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { DerivedFolder });
        foreach (string guid in derivedGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static void CropTextureToFile(string sourceAssetPath, string targetAssetPath, int x, int topY, int width, int height)
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
        if (source == null)
        {
            throw new IOException("Missing source texture: " + sourceAssetPath);
        }

        string sourcePath = ToAbsolutePath(sourceAssetPath);
        TextureImporter sourceImporter = AssetImporter.GetAtPath(sourceAssetPath) as TextureImporter;
        bool restoreReadable = false;
        if (sourceImporter != null && !sourceImporter.isReadable)
        {
            sourceImporter.isReadable = true;
            sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
            sourceImporter.filterMode = FilterMode.Point;
            sourceImporter.mipmapEnabled = false;
            sourceImporter.SaveAndReimport();
            restoreReadable = true;
        }

        Texture2D readableSource = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
        int y = readableSource.height - topY - height;
        Color[] pixels = readableSource.GetPixels(x, y, width, height);
        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.filterMode = FilterMode.Point;
        cropped.SetPixels(pixels);
        cropped.Apply();

        string absoluteTarget = ToAbsolutePath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget) ?? Application.dataPath);
        File.WriteAllBytes(absoluteTarget, cropped.EncodeToPNG());

        Object.DestroyImmediate(cropped);

        if (restoreReadable && sourceImporter != null)
        {
            sourceImporter.isReadable = false;
            sourceImporter.SaveAndReimport();
        }
    }

    private static byte[] DownloadBytes(string url)
    {
        using UnityWebRequest request = UnityWebRequest.Get(url);
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
        {
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new IOException("Failed to download " + url + ": " + request.error);
        }

        return request.downloadHandler.data;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string relative = assetPath.StartsWith("Assets/")
            ? assetPath.Substring("Assets/".Length)
            : assetPath;
        return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
