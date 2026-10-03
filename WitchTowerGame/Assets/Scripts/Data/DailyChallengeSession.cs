using System;
using System.IO;
using UnityEngine;
using WitchTower.Managers;
using WitchTower.Save;

namespace WitchTower.Data
{
    public static class DailyChallengeSession
    {
        public static DailyChallengeRun Run { get; private set; }
        public static bool ReopenPanel { get; set; }
        public static bool IsActive => Run != null && Run.IsActive && DailyChallengeCatalog.IsMode(Run.Mode);
        public static int Stage => Run?.NextStage ?? 1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { Run = null; ReopenPanel = false; }
        public static bool Begin(DailyChallengeRun run)
        {
            if (GuardianTrialSession.IsActive || run == null || !run.IsActive || string.IsNullOrEmpty(run.RunId) ||
                !DailyChallengeCatalog.IsMode(run.Mode) || run.PartyInstanceIds == null || run.PartyInstanceIds.Length == 0) return false;
            Run = Clone(run);
            // A local mid-fight checkpoint is only eligible for the same server-confirmed run and
            // cleared stage. It cannot increase server progression, replace the roster or grant rewards.
            try
            {
                string path = PathFor(run.RunId);
                if (path != null && File.Exists(path))
                {
                    var cached = JsonUtility.FromJson<DailyChallengeRun>(File.ReadAllText(path));
                    if (cached?.RunId == run.RunId && cached.ClearedStage == run.ClearedStage &&
                        cached.Checkpoint != null && cached.Checkpoint.Stage == run.NextStage && !cached.Checkpoint.StageComplete &&
                        string.Join("|", cached.PartyInstanceIds ?? Array.Empty<string>()) == string.Join("|", run.PartyInstanceIds))
                        Run.Checkpoint = cached.Checkpoint;
                }
            }
            catch (Exception e) { Debug.LogWarning("試練の途中記録を読み込めませんでした: " + e.Message); }
            ReopenPanel = false;
            Persist();
            return true;
        }
        public static void Accept(DailyChallengeRun confirmed)
        {
            if (confirmed == null || Run == null || confirmed.RunId != Run.RunId) return;
            Run = Clone(confirmed);
            Persist();
        }
        public static DailyChallengeRun Clone(DailyChallengeRun run) => run == null ? null : JsonUtility.FromJson<DailyChallengeRun>(JsonUtility.ToJson(run));
        public static void SetCheckpoint(DailyChallengeCheckpoint checkpoint)
        {
            if (!IsActive) return;
            Run.Checkpoint = checkpoint;
            Persist();
        }
        public static void End(bool completed)
        {
            if (completed && Run != null && HasStorage)
            {
                try { File.Delete(PathFor(Run.RunId)); } catch (IOException) { }
            }
            Run = null;
            ReopenPanel = true;
        }
        public static void Persist()
        {
            if (Run == null || string.IsNullOrEmpty(Run.RunId) || !HasStorage) return;
            try
            {
                string path = PathFor(Run.RunId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                OnlinePlayerData.WriteAtomic(path, JsonUtility.ToJson(Run));
            }
            catch (Exception e) { Debug.LogWarning("試練の途中記録を保存できませんでした: " + e.Message); }
        }
        private static bool HasStorage => SaveManager.Instance != null && SaveManager.Instance.StorageAccessAvailable;
        private static string PathFor(string id) => HasStorage ? Path.Combine(SaveManager.Instance.DataDirectory, "daily-challenge-runs",
            Uri.EscapeDataString(id) + ".json") : null;
    }
}
