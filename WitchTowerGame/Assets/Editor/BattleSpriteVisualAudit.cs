using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using WitchTower.Battle;

public static class BattleSpriteVisualAudit
{
    private const string MenuPath = "Tools/MCP/Audit Battle Sprite Visuals";
    private const string MonsterBattleFolder = "Assets/Resources/MonsterBattle";

    [MenuItem(MenuPath)]
    public static void GenerateReport()
    {
        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { MonsterBattleFolder });
        var numberedFrameBases = new HashSet<string>();
        var sequencedCharacterKeys = new HashSet<string>();
        foreach (string guid in spriteGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string resourcePath = ResolveResourcePath(assetPath);
            if (TryResolveNumberedFrameBase(resourcePath, out string basePath))
            {
                numberedFrameBases.Add(basePath);
                sequencedCharacterKeys.Add(ResolveCharacterKey(Path.GetFileNameWithoutExtension(assetPath)));
            }
        }

        var rows = new List<ReportRow>();
        foreach (string guid in spriteGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string resourcePath = ResolveResourcePath(assetPath);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isNumberedFrame = TryResolveNumberedFrameBase(resourcePath, out _);
            if (!isNumberedFrame && numberedFrameBases.Contains(resourcePath))
            {
                continue;
            }

            if (!isNumberedFrame &&
                !HasPoseMarker(fileName) &&
                (sequencedCharacterKeys.Contains(ResolveCharacterKey(fileName)) || numberedFrameBases.Any(basePath => basePath.StartsWith(resourcePath + "_", System.StringComparison.Ordinal))))
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                continue;
            }

            BattleSpriteVisualMetrics metrics = BattleVisualResolver.ResolveSpriteVisualMetrics(sprite);
            rows.Add(new ReportRow(
                assetPath,
                resourcePath,
                ResolveCharacterKey(fileName),
                ResolvePose(fileName),
                metrics.SpriteWidth,
                metrics.SpriteHeight,
                metrics.OpaqueWidth,
                metrics.OpaqueHeight,
                metrics.IsReadable));
        }

        rows = rows
            .OrderBy(row => row.CharacterKey)
            .ThenBy(row => PoseSortValue(row.Pose))
            .ThenBy(row => row.AssetPath)
            .ToList();

        string reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "reports"));
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "battle_sprite_visual_audit.csv");
        File.WriteAllText(reportPath, BuildCsv(rows), Encoding.UTF8);

        int unreadableCount = rows.Count(row => !row.IsReadable);
        int characterCount = rows.Select(row => row.CharacterKey).Distinct().Count();
        Debug.Log($"[BattleSpriteVisualAudit] Wrote {rows.Count} rows for {characterCount} characters to {reportPath}. Unreadable textures: {unreadableCount}.");
    }

    private static string BuildCsv(IReadOnlyList<ReportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("assetPath,resourcePath,characterKey,pose,spriteWidth,spriteHeight,opaqueWidth,opaqueHeight,readable,poseHeightVsBaseline");
        var baselineHeightByCharacter = rows
            .Where(row => (row.Pose == "idle" || row.Pose == "move") && row.OpaqueHeight > 0f)
            .GroupBy(row => row.CharacterKey)
            .ToDictionary(group => group.Key, group =>
            {
                var idleRows = group.Where(row => row.Pose == "idle").ToArray();
                IReadOnlyList<ReportRow> baselineRows = idleRows.Length > 0 ? idleRows : group.Where(row => row.Pose == "move").ToArray();
                return baselineRows.Average(row => row.OpaqueHeight);
            });

        var idleHeightByCharacter = baselineHeightByCharacter;

        var directIdleCharacters = new HashSet<string>(rows
            .Where(row => row.Pose == "idle")
            .GroupBy(row => row.CharacterKey)
            .Select(group => group.Key));

        foreach (ReportRow row in rows)
        {
            float ratio = idleHeightByCharacter.TryGetValue(row.CharacterKey, out float idleHeight) && idleHeight > 0f
                ? row.OpaqueHeight / idleHeight
                : 0f;
            string pose = directIdleCharacters.Contains(row.CharacterKey) ? row.Pose : $"{row.Pose}+moveBaseline";
            builder
                .Append(Escape(row.AssetPath)).Append(',')
                .Append(Escape(row.ResourcePath)).Append(',')
                .Append(Escape(row.CharacterKey)).Append(',')
                .Append(Escape(pose)).Append(',')
                .Append(row.SpriteWidth.ToString("0.##")).Append(',')
                .Append(row.SpriteHeight.ToString("0.##")).Append(',')
                .Append(row.OpaqueWidth.ToString("0.##")).Append(',')
                .Append(row.OpaqueHeight.ToString("0.##")).Append(',')
                .Append(row.IsReadable ? "true" : "false").Append(',')
                .Append(ratio.ToString("0.###"))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string ResolveResourcePath(string assetPath)
    {
        const string resourcesPrefix = "Assets/Resources/";
        string withoutExtension = Path.ChangeExtension(assetPath, null)?.Replace('\\', '/') ?? assetPath;
        return withoutExtension.StartsWith(resourcesPrefix)
            ? withoutExtension.Substring(resourcesPrefix.Length)
            : withoutExtension;
    }

    private static bool TryResolveNumberedFrameBase(string resourcePath, out string basePath)
    {
        basePath = null;
        int separatorIndex = resourcePath.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= resourcePath.Length - 1)
        {
            return false;
        }

        string suffix = resourcePath.Substring(separatorIndex + 1);
        if (!int.TryParse(suffix, out _))
        {
            return false;
        }

        basePath = resourcePath.Substring(0, separatorIndex);
        if (!HasPoseMarker(Path.GetFileName(basePath)))
        {
            basePath = null;
            return false;
        }

        return true;
    }

    private static string ResolveCharacterKey(string fileName)
    {
        string normalized = fileName.StartsWith("mon_") ? fileName.Substring(4) : fileName;
        foreach (string marker in new[] { "_idle", "_move", "_attack" })
        {
            int markerIndex = normalized.IndexOf(marker, System.StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                return normalized.Substring(0, markerIndex);
            }
        }

        return normalized;
    }

    private static bool HasPoseMarker(string fileName)
    {
        return fileName.Contains("_idle") || fileName.Contains("_move") || fileName.Contains("_attack");
    }

    private static string ResolvePose(string fileName)
    {
        if (fileName.Contains("_attack"))
        {
            return "attack";
        }

        if (fileName.Contains("_move"))
        {
            return "move";
        }

        return "idle";
    }

    private static int PoseSortValue(string pose)
    {
        return pose switch
        {
            "idle" => 0,
            "move" => 1,
            "attack" => 2,
            _ => 3
        };
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private readonly struct ReportRow
    {
        public readonly string AssetPath;
        public readonly string ResourcePath;
        public readonly string CharacterKey;
        public readonly string Pose;
        public readonly float SpriteWidth;
        public readonly float SpriteHeight;
        public readonly float OpaqueWidth;
        public readonly float OpaqueHeight;
        public readonly bool IsReadable;

        public ReportRow(
            string assetPath,
            string resourcePath,
            string characterKey,
            string pose,
            float spriteWidth,
            float spriteHeight,
            float opaqueWidth,
            float opaqueHeight,
            bool isReadable)
        {
            AssetPath = assetPath;
            ResourcePath = resourcePath;
            CharacterKey = characterKey;
            Pose = pose;
            SpriteWidth = spriteWidth;
            SpriteHeight = spriteHeight;
            OpaqueWidth = opaqueWidth;
            OpaqueHeight = opaqueHeight;
            IsReadable = isReadable;
        }
    }
}
