using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WitchTower.Save
{
    public sealed class PlayerSaveRepository
    {
        private readonly string primary;
        public string HistoryDirectory => primary + ".history";
        public PlayerSaveRepository(string primaryPath) { primary = primaryPath; }

        public bool TryLoad(out PlayerSaveData data, out bool recoveryRequired, out string error)
        {
            data = null;
            recoveryRequired = false;
            error = string.Empty;
            try
            {
                var generations = GenerationFiles();
                // The newest immutable generation is the commit record. Never silently
                // choose an older generation: that can undo a delivered purchase.
                string source = generations.FirstOrDefault();
                if (source != null)
                {
                    if (SaveFileStore.TryLoad(source, out data, out error)) return true;
                    recoveryRequired = true;
                    return false;
                }
                if (SaveFileStore.TryLoad(primary, out data, out error)) return true;
                if (File.Exists(primary) || File.Exists(SaveFileStore.GetBackupPath(primary)) ||
                    (Directory.Exists(Path.GetDirectoryName(primary)) &&
                     Directory.GetFiles(Path.GetDirectoryName(primary), "save.json.corrupt-*").Length > 0))
                {
                    // Both files are retained for inspection. Recovery is explicit,
                    // even when the backup parses: it may predate a paid transaction.
                    recoveryRequired = true;
                    return false;
                }
                data = PlayerSaveData.CreateDefault();
                return TryCommit(data, null, "new_player", out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                recoveryRequired = true;
                return false;
            }
        }

        public bool TryCommit(PlayerSaveData data, PlayerSaveData previous, string reason, out string error)
        {
            error = string.Empty;
            try
            {
                if (!PlayerSaveDataMigration.TryMigrate(data, out error)) return false;
                if (data.PlayerLevel < 1) { error = "Invalid player level."; return false; }
                if (previous != null && !string.IsNullOrEmpty(data.PlayerId) &&
                    data.PlayerId != previous.PlayerId) { error = "Player identity changed."; return false; }
                data.PlayerId = previous?.PlayerId ?? data.PlayerId;
                if (string.IsNullOrEmpty(data.PlayerId)) data.PlayerId = Guid.NewGuid().ToString("N");
                data.SaveRevision = checked(Math.Max(previous?.SaveRevision ?? 0, data.SaveRevision) + 1);
                data.SavedAtUtc = DateTime.UtcNow.ToString("O");
                data.SaveReason = string.IsNullOrWhiteSpace(reason) ? "gameplay" : reason;
                data.AppVersion = Application.version;
                data.DataWarnings = SaveDataDiagnostics.Inspect(data);
                Directory.CreateDirectory(HistoryDirectory);
                // Preserve the pre-upgrade save before the first managed commit.
                if (GenerationFiles().Length == 0 && File.Exists(primary) && !File.Exists(Path.Combine(HistoryDirectory, "legacy-original.json")))
                    File.Copy(primary, Path.Combine(HistoryDirectory, "legacy-original.json"), false);
                string generation = Path.Combine(HistoryDirectory, data.SaveRevision.ToString("D20") + ".json");
                if (File.Exists(generation)) { error = "Save revision conflict."; return false; }
                if (!SaveFileStore.TrySave(generation, data, out error, false)) return false;
                // Commit has succeeded. A cache or retention failure must not cause
                // callers to grant/charge again. The generation remains authoritative.
                if (!SaveFileStore.TrySave(primary, data, out string cacheError))
                    Debug.LogWarning("[Save] Committed generation; cache refresh failed: " + cacheError);
                try { Prune(); } catch (Exception e) { Debug.LogWarning("[Save] Retention deferred: " + e.Message); }
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private string[] GenerationFiles() => Directory.Exists(HistoryDirectory)
            ? Directory.GetFiles(HistoryDirectory, "*.json").Where(p =>
                Path.GetFileNameWithoutExtension(p).Length == 20 &&
                long.TryParse(Path.GetFileNameWithoutExtension(p), out _)).OrderByDescending(p => p, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        private void Prune()
        {
            var files = GenerationFiles();
            var retainedDays = new HashSet<DateTime>();
            for (int i = 0; i < files.Length; i++)
            {
                DateTime day = File.GetLastWriteTimeUtc(files[i]).Date;
                bool daily = day >= DateTime.UtcNow.Date.AddDays(-30) && retainedDays.Add(day);
                if (i >= 100 && !daily) File.Delete(files[i]);
            }
        }
    }
}
