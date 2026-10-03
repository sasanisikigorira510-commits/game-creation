using System;
using UnityEngine;

namespace WitchTower.Home
{
    // Cosmetic recovery only. Inventory/save remains owned by the existing summon transaction.
    // A journal can NEVER award monsters, spend stones or roll anything.
    public static class TenPullPresentationJournal
    {
        private const string ProductionKey = "witchtower_pending_ten_pull_presentation_v1";
        private static string Key => WitchTower.Managers.SaveManager.Instance?.PurchaseEnvironment == "Sandbox"
            ? ProductionKey + "_review_sandbox_v1" : ProductionKey;
        private static bool StorageBlocked => WitchTower.Managers.SaveManager.Instance != null &&
            !WitchTower.Managers.SaveManager.Instance.StorageAccessAvailable;
        public static bool HasPending => !StorageBlocked && PlayerPrefs.HasKey(Key);
        [Serializable] private sealed class Entry { public SummonPresentationResult[] Results; }

        public static void Store(SummonPresentationResult[] results)
        {
            if (StorageBlocked) return;
            if (results == null || (results.Length != 1 && results.Length != 10)) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Entry { Results = results }));
            PlayerPrefs.Save();
        }

        public static SummonPresentationResult[] Read(Func<string, bool> ownsInstance)
        {
            if (StorageBlocked) return null;
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var entry = JsonUtility.FromJson<Entry>(PlayerPrefs.GetString(Key));
                if (entry?.Results == null || (entry.Results.Length != 1 && entry.Results.Length != 10)) { Clear(); return null; }
                foreach (var item in entry.Results)
                    if (item == null || string.IsNullOrEmpty(item.InstanceId) || !ownsInstance(item.InstanceId)) { Clear(); return null; }
                return entry.Results;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[TenPull] Discarded an invalid presentation journal: " + exception.GetType().Name);
                Clear();
                return null;
            }
        }

        public static void Clear() { if (StorageBlocked) return; PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }
}
