using System;
using System.IO;
using System.Linq;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Home
{
    // Display metadata only. The saved inventory is the sole authority for a
    // completed fusion; this receipt must never create, remove, or grant a unit.
    [Serializable]
    public sealed class FusionPresentationReceipt
    {
        public string PlayerId;
        public string CreatedInstanceId;
        public string CreatedMonsterId;
        public string ParentMonsterIdA;
        public string ParentMonsterIdB;
        public bool CompletedTutorial;

        private static FusionPresentationReceipt memory;
        private static string memoryDirectory;
        private const string FileName = "fusion-presentation.json";

        public static void Store(PlayerProfile profile, OwnedMonsterData created,
            MonsterDataSO parentA, MonsterDataSO parentB, bool completedTutorial)
        {
            if (profile == null || created == null) return;
            if (SaveManager.Instance?.StorageAccessAvailable == false) return;
            string directory = SaveManager.Instance?.DataDirectory;
            memory = new FusionPresentationReceipt
            {
                PlayerId = profile.PlayerId,
                CreatedInstanceId = created.InstanceId,
                CreatedMonsterId = created.MonsterId,
                ParentMonsterIdA = parentA?.monsterId,
                ParentMonsterIdB = parentB?.monsterId,
                CompletedTutorial = completedTutorial
            };
            memoryDirectory = directory;
            if (string.IsNullOrEmpty(directory)) return;
            try
            {
                Directory.CreateDirectory(directory);
                OnlinePlayerData.WriteAtomic(Path.Combine(directory, FileName), JsonUtility.ToJson(memory));
            }
            catch (Exception exception)
            {
                // The actual result has already committed. Retain the in-memory
                // display and never roll the transaction back for a UI receipt.
                Debug.LogWarning("[Fusion] Result is saved; presentation receipt unavailable: " + exception.Message);
            }
        }

        public static bool TryRead(PlayerProfile profile, out FusionPresentationReceipt receipt)
        {
            receipt = null;
            if (profile == null || SaveManager.Instance?.StorageAccessAvailable == false) return false;
            receipt = ReadUnchecked();
            if (receipt == null) return false;
            OwnedMonsterData child = profile.GetOwnedMonster(receipt.CreatedInstanceId);
            PlayerSaveData saved = SaveManager.Instance?.CurrentSaveData;
            FusionPresentationReceipt loaded = receipt;
            bool savedChildExists = saved?.OwnedMonsters != null && saved.OwnedMonsters.Any(monster =>
                monster != null && monster.InstanceId == loaded.CreatedInstanceId && monster.MonsterId == loaded.CreatedMonsterId);
            bool valid = !string.IsNullOrEmpty(receipt.PlayerId) && receipt.PlayerId == profile.PlayerId &&
                saved != null && saved.PlayerId == receipt.PlayerId && savedChildExists &&
                child != null && child.MonsterId == receipt.CreatedMonsterId;
            if (valid) return true;
            // A reset, account change, or later consumption of the child makes a
            // stale display irrelevant. It can never restore an old inventory.
            Clear(receipt.CreatedInstanceId);
            receipt = null;
            return false;
        }

        public static void Clear(string createdInstanceId)
        {
            if (string.IsNullOrEmpty(createdInstanceId) || SaveManager.Instance?.StorageAccessAvailable == false) return;
            FusionPresentationReceipt current = ReadUnchecked();
            if (current == null || current.CreatedInstanceId != createdInstanceId) return;
            string directory = SaveManager.Instance?.DataDirectory;
            memory = null;
            memoryDirectory = null;
            if (string.IsNullOrEmpty(directory)) return;
            try
            {
                string path = Path.Combine(directory, FileName);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Fusion] Could not acknowledge presentation receipt: " + exception.Message);
            }
        }

        private static FusionPresentationReceipt ReadUnchecked()
        {
            if (SaveManager.Instance?.StorageAccessAvailable == false) return null;
            string directory = SaveManager.Instance?.DataDirectory;
            if (memory != null && memoryDirectory == directory) return memory;
            if (string.IsNullOrEmpty(directory)) return null;
            try
            {
                string path = Path.Combine(directory, FileName);
                if (!File.Exists(path)) return null;
                FusionPresentationReceipt loaded = JsonUtility.FromJson<FusionPresentationReceipt>(File.ReadAllText(path));
                if (loaded == null || string.IsNullOrEmpty(loaded.CreatedInstanceId)) return null;
                memory = loaded;
                memoryDirectory = directory;
                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Fusion] Could not read presentation receipt: " + exception.Message);
                return null;
            }
        }
    }
}
