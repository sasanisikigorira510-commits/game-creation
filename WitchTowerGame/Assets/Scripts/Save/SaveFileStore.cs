using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WitchTower.Save
{
    public static class SaveFileStore
    {
        public static string GetBackupPath(string primaryPath)
        {
            return primaryPath + ".bak";
        }

        public static bool TryLoad(string path, out PlayerSaveData saveData, out string error)
        {
            saveData = null;
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    error = "Save file does not exist.";
                    return false;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Save file was empty.";
                    return false;
                }

                PlayerSaveData loaded = JsonUtility.FromJson<PlayerSaveData>(json);
                if (loaded == null || loaded.PlayerLevel <= 0)
                {
                    error = "Save data was incomplete.";
                    return false;
                }

                if (!PlayerSaveDataMigration.TryMigrate(loaded, out error))
                {
                    return false;
                }

                saveData = loaded;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TrySave(
            string primaryPath,
            PlayerSaveData saveData,
            out string error,
            bool rotateBackup = true)
        {
            error = string.Empty;
            string temporaryPath = primaryPath + ".tmp";

            try
            {
                if (string.IsNullOrWhiteSpace(primaryPath))
                {
                    error = "Save path was empty.";
                    return false;
                }

                if (!PlayerSaveDataMigration.TryMigrate(saveData, out error))
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(primaryPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(saveData, true);
                WriteDurableText(temporaryPath, json);

                if (!File.Exists(primaryPath))
                {
                    File.Move(temporaryPath, primaryPath);
                    return true;
                }

                if (!rotateBackup)
                {
                    File.Delete(primaryPath);
                    File.Move(temporaryPath, primaryPath);
                    return true;
                }

                string backupPath = GetBackupPath(primaryPath);
                try
                {
                    File.Replace(temporaryPath, primaryPath, backupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithPortableFallback(temporaryPath, primaryPath, backupPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        public static string TryArchiveUnreadablePrimary(string primaryPath)
        {
            if (string.IsNullOrWhiteSpace(primaryPath) || !File.Exists(primaryPath))
            {
                return string.Empty;
            }

            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
                string archivePath = primaryPath + ".corrupt-" + timestamp;
                File.Move(primaryPath, archivePath);
                return archivePath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WriteDurableText(string path, string text)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(text);
            writer.Flush();
            stream.Flush(true);
        }

        private static void ReplaceWithPortableFallback(string temporaryPath, string primaryPath, string backupPath)
        {
            File.Copy(primaryPath, backupPath, true);
            File.Delete(primaryPath);
            File.Move(temporaryPath, primaryPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // A stale temp file is ignored; the next write replaces it.
            }
        }
    }
}
