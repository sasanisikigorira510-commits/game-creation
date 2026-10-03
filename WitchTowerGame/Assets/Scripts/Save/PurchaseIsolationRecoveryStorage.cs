using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using WitchTower.Monetization;

namespace WitchTower.Save
{
    [Serializable]
    public sealed class PurchaseIsolationRecoveryJournal
    {
        public int Version;
        public string Phase, SandboxEndpoint, SourceDataDirectoryRelative, SourcePlayerId, SandboxToken;
        public string OriginalRequestJson, OriginalRequestBase64, OriginalRequestHash, DeliveredSaveHash;
        public OnlineRequest Request;
        public OnlineOperation ServerOperation;
        public bool SourcePendingCleared;
    }

    // This is deliberately not a SaveManager account switch. Both the routing
    // tombstone and its fresh Sandbox account live outside every gameplay save.
    // Never log/serialize this journal to diagnostics: it contains a receipt and
    // an isolated credential, but never the production credential.
    public static class PurchaseIsolationRecoveryStorage
    {
        private const int CurrentVersion = 1;
        private const int MaximumArchiveBytes = 16 * 1024 * 1024;
        private const string DirectoryName = "iap-isolation-recovery";
        private const string ProductionRelative = "environments/production";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        [Serializable] private sealed class Identity { public string PlayerId, Token; public bool Legacy; }

        public static string RuntimeRoot
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(WitchTower.Managers.SaveManager.EditorSaveDirectoryOverride))
                    return WitchTower.Managers.SaveManager.EditorSaveDirectoryOverride;
#endif
                return Application.persistentDataPath;
            }
        }

        private static string RecoveryDirectory(string root) => Path.Combine(FullRoot(root), DirectoryName);
        public static string JournalPath(string root) => Path.Combine(RecoveryDirectory(root), "journal.json");
        public static string IsolatedDirectory(string root) => Path.Combine(RecoveryDirectory(root), "isolated-account");
        public static string IsolatedSavePath(string root) => Path.Combine(IsolatedDirectory(root), "save.json");

        // Even an orphaned/empty directory is not permission to start another
        // order. An interrupted staging attempt must be inspected explicitly.
        public static bool Blocks(string root) => EntryExists(RecoveryDirectory(root));

        public static PurchaseIsolationRecoveryJournal Begin(string root, string sourceDataDirectory,
            string sourcePlayerId, string approvedRequestId, string approvedTransactionId, string approvedProductId)
        {
            EnsureSafeRecoveryPaths(root);
            if (Blocks(root)) throw new InvalidOperationException("An isolation record already exists; resume or review it.");
            if (!Hex(sourcePlayerId, 32)) throw Invalid();
            string relative = SourceRelative(root, sourceDataDirectory);
            string pendingPath = Path.Combine(SourceDirectory(root, relative), "online-request.json");
            EnsureNotLinked(root, pendingPath);
            if (EntryExists(pendingPath + ".tmp") || !File.Exists(pendingPath)) throw Invalid();
            byte[] bytes = File.ReadAllBytes(pendingPath);
            string raw = DecodeArchive(bytes);
            OnlineRequest request = Parse<OnlineRequest>(raw);
            ValidateRequest(request);
            if (request.RequestId != approvedRequestId || request.TransactionId != approvedTransactionId ||
                request.Target != approvedProductId) throw Invalid();
            var journal = new PurchaseIsolationRecoveryJournal
            {
                Version = CurrentVersion, Phase = "pending", SandboxEndpoint = RefundSandboxConfiguration.Endpoint,
                SourceDataDirectoryRelative = relative, SourcePlayerId = sourcePlayerId,
                SandboxToken = AppleRecoveryStorage.NewCredential(), Request = Clone(request),
                OriginalRequestJson = raw, OriginalRequestBase64 = Convert.ToBase64String(bytes),
                OriginalRequestHash = Digest(bytes)
            };
            // Quarantine first. A failure below leaves a durable routing blocker,
            // not a reason to replay this receipt through production on restart.
            Write(root, JournalPath(root), journal);
            var initial = InitialSave(journal);
            EnsureSaveWritePaths(root, 1);
            if (!new PlayerSaveRepository(IsolatedSavePath(root)).TryCommit(initial, null, "purchase_isolation_initial", out _))
                throw new IOException("Cannot persist the isolated initial save.");
            Write(root, Path.Combine(IsolatedDirectory(root), "online-identity.json"),
                new Identity { PlayerId = sourcePlayerId, Token = journal.SandboxToken });
            return Read(root);
        }

        public static PurchaseIsolationRecoveryJournal Read(string root)
        {
            EnsureSafeRecoveryPaths(root);
            string path = JournalPath(root);
            if (EntryExists(path + ".tmp")) throw Invalid();
            if (!File.Exists(path))
            {
                if (Blocks(root)) throw Invalid();
                return null;
            }
            var journal = Parse<PurchaseIsolationRecoveryJournal>(File.ReadAllText(path, StrictUtf8));
            ValidateJournal(root, journal);
            ValidateIsolatedSave(root, journal);
            return journal;
        }

        // The shared-root tombstone is independent of whichever account is now
        // active. Corrupt state throws: callers must stop, never route normally.
        public static bool Matches(string root, string productId, string transactionId)
        {
            var journal = Read(root);
            if (journal == null || journal.Request.TransactionId != transactionId) return false;
            if (journal.Request.Target != productId) throw Invalid();
            return true;
        }

        // The caller must authenticate the server response (or the pinned SSH
        // operator result) first. This storage validates its exact scope; it does
        // not turn an arbitrary local JSON object into authenticated evidence.
        public static void MarkServerDelivered(string root, string authenticatedEndpoint, string authenticatedPlayerId,
            OnlineOperation authenticatedOperation)
        {
            var journal = Required(root);
            if (authenticatedEndpoint != RefundSandboxConfiguration.Endpoint || authenticatedPlayerId != journal.SourcePlayerId)
                throw Invalid();
            ValidateOperation(journal, authenticatedOperation);
            if (journal.Phase != "pending")
            {
                if (JsonUtility.ToJson(journal.ServerOperation) != JsonUtility.ToJson(authenticatedOperation)) throw Invalid();
                return;
            }
            journal.ServerOperation = Clone(authenticatedOperation);
            journal.Phase = "server_delivered";
            Write(root, JournalPath(root), journal);
            Read(root);
        }

        public static void PersistLocalDelivery(string root)
        {
            var journal = Required(root);
            if (journal.Phase == "pending") throw new InvalidOperationException("Authenticated Sandbox delivery is required.");
            if (journal.Phase == "local_delivered" || journal.Phase == "store_confirmed") return;
            var previous = ValidateIsolatedSave(root, journal);
            if (previous.EconomyRevision == 0)
            {
                var staged = OnlineGrantApplier.Stage(previous, Clone(journal.ServerOperation));
                EnsureSaveWritePaths(root, 2);
                if (!new PlayerSaveRepository(IsolatedSavePath(root)).TryCommit(staged, previous, "purchase_isolation_delivered", out _))
                    throw new IOException("Cannot persist the isolated delivery.");
            }
            // If a crash happened after the immutable generation commit, repeat
            // only the verification and journal transition, never the grant.
            var reloaded = ValidateIsolatedSave(root, journal);
            if (reloaded.EconomyRevision != 1) throw Invalid();
            journal.DeliveredSaveHash = SaveDigest(reloaded);
            journal.Phase = "local_delivered";
            Write(root, JournalPath(root), journal);
            Read(root);
        }

        public static bool CanConfirm(string root, string productId, string transactionId)
        {
            var journal = Read(root);
            return journal != null && (journal.Phase == "local_delivered" || journal.Phase == "store_confirmed") &&
                journal.Request.Target == productId && journal.Request.TransactionId == transactionId;
        }

        public static void ClearOriginalPending(string root)
        {
            var journal = Required(root);
            if (!CanConfirm(root, journal.Request.Target, journal.Request.TransactionId))
                throw new InvalidOperationException("Durable isolated delivery is required.");
            string pending = Path.Combine(SourceDirectory(root, journal.SourceDataDirectoryRelative), "online-request.json");
            EnsureNotLinked(root, pending);
            if (EntryExists(pending + ".tmp")) throw Invalid();
            if (File.Exists(pending))
            {
                if (Digest(File.ReadAllBytes(pending)) != journal.OriginalRequestHash) throw Invalid();
                // Only this exact archived request is removed. No save, identity,
                // other order, backup or production processed-ID list is edited.
                File.Delete(pending);
            }
            // A crash between delete and journal rename is safely idempotent:
            // the authenticated local delivery and archive are already durable.
            if (!journal.SourcePendingCleared)
            {
                journal.SourcePendingCleared = true;
                Write(root, JournalPath(root), journal);
                Read(root);
            }
        }

        public static void MarkStoreConfirmed(string root, string productId, string transactionId)
        {
            var journal = Required(root);
            if (!CanConfirm(root, productId, transactionId) || !journal.SourcePendingCleared) throw Invalid();
            if (journal.Phase == "store_confirmed") return;
            journal.Phase = "store_confirmed";
            Write(root, JournalPath(root), journal);
            Read(root);
            // Never remove the journal: a StoreKit redelivery or source-account
            // change must still recognize and suppress this exact order.
        }

        private static PurchaseIsolationRecoveryJournal Required(string root) => Read(root) ?? throw Invalid();
        private static InvalidDataException Invalid() => new InvalidDataException("Purchase isolation state needs review.");
        private static bool Hex(string value, int length) => value != null && value.Length == length && value.All(c => "0123456789abcdef".Contains(c));
        private static bool Identifier(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Any(char.IsControl);
        private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static T Parse<T>(string raw)
        {
            try { return JsonUtility.FromJson<T>(raw); }
            catch { throw Invalid(); }
        }

        private static void ValidateRequest(OnlineRequest request)
        {
            if (request == null || request.Kind != "purchase" || !Identifier(request.RequestId, 128) || request.RequestId.Length < 8 ||
                !Identifier(request.TransactionId, 128) || !IapProductCatalog.TryGet(request.Target, out _) ||
                string.IsNullOrWhiteSpace(request.Receipt) || request.Receipt.Length > MaximumArchiveBytes ||
                request.Count != 0 || request.Paid || request.Epoch < 0) throw Invalid();
        }

        private static void ValidateJournal(string root, PurchaseIsolationRecoveryJournal journal)
        {
            if (journal == null || journal.Version != CurrentVersion || !Hex(journal.SourcePlayerId, 32) ||
                !Hex(journal.SandboxToken, 64) || journal.SandboxEndpoint != RefundSandboxConfiguration.Endpoint ||
                !(journal.Phase == "pending" || journal.Phase == "server_delivered" || journal.Phase == "local_delivered" || journal.Phase == "store_confirmed") ||
                !Hex(journal.OriginalRequestHash, 64)) throw Invalid();
            SourceDirectory(root, journal.SourceDataDirectoryRelative);
            ValidateRequest(journal.Request);
            byte[] archive;
            if (journal.OriginalRequestBase64 == null || journal.OriginalRequestBase64.Length > ((MaximumArchiveBytes + 2L) / 3) * 4 ||
                journal.OriginalRequestJson == null || journal.OriginalRequestJson.Length > MaximumArchiveBytes) throw Invalid();
            try { archive = Convert.FromBase64String(journal.OriginalRequestBase64 ?? string.Empty); }
            catch { throw Invalid(); }
            if (Digest(archive) != journal.OriginalRequestHash || DecodeArchive(archive) != journal.OriginalRequestJson ||
                JsonUtility.ToJson(Parse<OnlineRequest>(journal.OriginalRequestJson)) != JsonUtility.ToJson(journal.Request)) throw Invalid();
            if (journal.Phase == "pending")
            {
                // JsonUtility materializes null nested classes as empty objects.
                if (journal.ServerOperation != null && !string.IsNullOrEmpty(journal.ServerOperation.RequestId)) throw Invalid();
                journal.ServerOperation = null;
                if (!string.IsNullOrEmpty(journal.DeliveredSaveHash) || journal.SourcePendingCleared) throw Invalid();
            }
            else ValidateOperation(journal, journal.ServerOperation);
            bool local = journal.Phase == "local_delivered" || journal.Phase == "store_confirmed";
            if (local ? !Hex(journal.DeliveredSaveHash, 64) : (!string.IsNullOrEmpty(journal.DeliveredSaveHash) || journal.SourcePendingCleared)) throw Invalid();
            if (journal.Phase == "store_confirmed" && !journal.SourcePendingCleared) throw Invalid();
        }

        private static void ValidateOperation(PurchaseIsolationRecoveryJournal journal, OnlineOperation operation)
        {
            if (!IapProductCatalog.TryGet(journal.Request.Target, out var product) || operation == null ||
                operation.Kind != "purchase" || operation.RequestId != journal.Request.RequestId ||
                operation.TransactionId != journal.Request.TransactionId || operation.Target != journal.Request.Target ||
                operation.Revision != 1 || operation.Free != 900 || operation.Paid != product.PaidStoneAmount ||
                operation.RefundDebt != 0 || operation.GoldDelta != 0 || !string.IsNullOrEmpty(operation.RelicId) ||
                operation.RelicAmount != 0 || operation.TutorialPulls != 0 || !string.IsNullOrEmpty(operation.ClaimDate) ||
                !string.IsNullOrEmpty(operation.UpgradeField) || (operation.Monsters != null && operation.Monsters.Length != 0)) throw Invalid();
        }

        private static PlayerSaveData InitialSave(PurchaseIsolationRecoveryJournal journal)
        {
            var save = Clone(PlayerSaveData.CreateDefault());
            save.PlayerId = journal.SourcePlayerId;
            if (!PlayerSaveDataMigration.TryMigrate(save, out _)) throw Invalid();
            return save;
        }

        private static PlayerSaveData ValidateIsolatedSave(string root, PurchaseIsolationRecoveryJournal journal)
        {
            string savePath = IsolatedSavePath(root), identityPath = Path.Combine(IsolatedDirectory(root), "online-identity.json");
            if (!File.Exists(identityPath) || EntryExists(identityPath + ".tmp") || EntryExists(savePath + ".tmp") ||
                !Directory.Exists(savePath + ".history") ||
                Directory.EnumerateFileSystemEntries(savePath + ".history").Any(path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal))) throw Invalid();
            string[] generations = Directory.GetFiles(savePath + ".history", "*.json").Where(path =>
                Path.GetFileNameWithoutExtension(path).Length == 20 &&
                long.TryParse(Path.GetFileNameWithoutExtension(path), out long revision) && revision > 0).ToArray();
            // TryLoad's ordinary new-player fallback must never run here, even
            // when an orphaned legacy-original.json exists without a generation.
            if (generations.Length == 0) throw Invalid();
            foreach (string path in generations) EnsureNotLinked(root, path);
            var identity = Parse<Identity>(File.ReadAllText(identityPath, StrictUtf8));
            if (identity == null || identity.Legacy || identity.PlayerId != journal.SourcePlayerId || identity.Token != journal.SandboxToken) throw Invalid();
            if (!new PlayerSaveRepository(savePath).TryLoad(out var save, out _, out _)) throw Invalid();
            string generation = Path.Combine(savePath + ".history", save.SaveRevision.ToString("D20") + ".json");
            EnsureNotLinked(root, generation);
            if (!File.Exists(generation)) throw Invalid();
            bool delivered = save.EconomyRevision == 1;
            if (save.SaveRevision != (delivered ? 2 : 1) ||
                (journal.Phase == "pending" && delivered) ||
                ((journal.Phase == "local_delivered" || journal.Phase == "store_confirmed") && !delivered)) throw Invalid();
            var expected = InitialSave(journal);
            if (delivered)
            {
                if (journal.ServerOperation == null) throw Invalid();
                expected = OnlineGrantApplier.Stage(expected, Clone(journal.ServerOperation));
            }
            if (SaveDigest(save) != SaveDigest(expected)) throw Invalid();
            if ((journal.Phase == "local_delivered" || journal.Phase == "store_confirmed") &&
                SaveDigest(save) != journal.DeliveredSaveHash &&
                !MatchesPreTrainingVersion1Digest(save, journal.DeliveredSaveHash)) throw Invalid();
            return save;
        }

        // Version 1 journals written before training stored the full then-current
        // save JSON hash. Keep those bytes verifiable after fields are added:
        // never rewrite the journal or weaken the expected-save comparison above.
        private static bool MatchesPreTrainingVersion1Digest(PlayerSaveData save, string digest)
        {
            if (save.TrainingDrops != 0 || save.TrialStarCores != 0 ||
                (save.TrainingFusionReceipts != null && save.TrainingFusionReceipts.Count != 0) ||
                JsonUtility.ToJson(save.DailyChallenges) !=
                    JsonUtility.ToJson(Clone(PlayerSaveData.CreateDefault()).DailyChallenges)) return false;
            var legacy = JsonUtility.FromJson<PreTrainingVersion1Save>(JsonUtility.ToJson(save));
            legacy.SaveRevision = 0; legacy.SavedAtUtc = string.Empty; legacy.SaveReason = string.Empty;
            legacy.AppVersion = string.Empty; legacy.DataWarnings = null;
            return Digest(StrictUtf8.GetBytes(JsonUtility.ToJson(legacy))) == digest;
        }

        // Frozen serialization shape of the 2026-10-02 Version 1 digest.
        // Field order is part of the existing SHA256 contract. Do not extend this
        // DTO when PlayerSaveData evolves. The full current save is also checked.
        [Serializable]
        private sealed class PreTrainingVersion1Save
        {
            public int SchemaVersion;
            public string PlayerId;
            public long SaveRevision;
            public long EconomyRevision;
            public int RecoveryEpoch;
            public string SavedAtUtc;
            public string SaveReason;
            public string AppVersion;
            public System.Collections.Generic.List<string> DataWarnings;
            public int PlayerLevel;
            public int PlayerExp;
            public int RebirthPoints;
            public int TotalRebirthPoints;
            public int RebirthCount;
            public int Gold;
            public int FreeGachaStones;
            public int PaidGachaStones;
            public System.Collections.Generic.List<string> ProcessedIapTransactionIds;
            public bool HasRemovedAds;
            public int HighestFloor;
            public int CurrentFloor;
            public int AttackUpgradeLevel;
            public int DefenseUpgradeLevel;
            public int HpUpgradeLevel;
            public bool HasAutoRepeatFloorUpgrade;
            public int AutoRepeatFloorUpgradeEnabledState;
            public bool HasAutoSellEquipmentUpgrade;
            public int AutoSellEquipmentUpgradeEnabledState;
            public int AutoSellEquipmentQualityThreshold;
            public bool HasAutoReleaseMonsterUpgrade;
            public int AutoReleaseMonsterUpgradeEnabledState;
            public int AutoReleaseMonsterIndividualValueThreshold;
            public string LastDailyRewardDate;
            public string DailyQuestProgressDate;
            public int DailyBattleWinCount;
            public System.Collections.Generic.List<string> DailyClaimedQuestIds;
            public string DailyAdRewardDate;
            public System.Collections.Generic.List<string> DailyClaimedAdRewardIds;
            public string LastActiveAt;
            public System.Collections.Generic.List<MissionProgressData> MissionProgressList;
            public string EquippedWeaponId;
            public string EquippedArmorId;
            public string EquippedAccessoryId;
            public System.Collections.Generic.List<OwnedMaterialData> OwnedMaterials;
            public System.Collections.Generic.List<OwnedEquipmentData> OwnedEquipments;
            public System.Collections.Generic.List<OwnedEnhancementRelicData> OwnedEnhancementRelics;
            public int EquipmentStorageLimit;
            public int MonsterStorageLimit;
            public System.Collections.Generic.List<OwnedMonsterData> OwnedMonsters;
            public System.Collections.Generic.List<MonsterDexEntryData> MonsterDexEntries;
            public System.Collections.Generic.List<string> PartyMonsterInstanceIds;
            public System.Collections.Generic.List<SkillLevelData> SkillLevels;
            public System.Collections.Generic.List<RebirthSkillLevelData> RebirthSkillLevels;
            public bool HasCompletedTutorial;
            public string TutorialStepId;
            public int InitialTutorialSummonCount;
            public string StoryDialogueEventId;
            public int StoryDialogueLineIndex;
            public System.Collections.Generic.List<string> SeenStoryEventIds;
            public System.Collections.Generic.List<string> SeenTutorialHintIds;
            public System.Collections.Generic.List<OwnedGuardianData> OwnedGuardians;
            public System.Collections.Generic.List<string> GuardianCoreIds;
            public string EquippedGuardianId;
            public bool GuardianSharedProgressInitialized;
            public int GuardianSharedLevel;
            public int GuardianSharedExp;
            public bool GuardianIndividualProgressInitialized;
            public System.Collections.Generic.List<string> GuardianOathIds;
            public System.Collections.Generic.List<string> SeenGuardianDialogueIds;
        }

        private static string SaveDigest(PlayerSaveData source)
        {
            var save = Clone(source);
            save.SaveRevision = 0; save.SavedAtUtc = string.Empty; save.SaveReason = string.Empty;
            save.AppVersion = string.Empty; save.DataWarnings = null;
            return Digest(StrictUtf8.GetBytes(JsonUtility.ToJson(save)));
        }
        private static string Digest(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string DecodeArchive(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumArchiveBytes) throw Invalid();
            try
            {
                string raw = StrictUtf8.GetString(bytes);
                return raw.Length > 0 && raw[0] == '\uFEFF' ? raw.Substring(1) : raw;
            }
            catch { throw Invalid(); }
        }
        private static void Write(string root, string path, object value)
        {
            EnsureNotLinked(root, path);
            EnsureNotLinked(root, path + ".tmp");
            if (EntryExists(path + ".tmp")) throw Invalid();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // CreateNew cannot follow an existing temp symlink, including a
            // dangling link. Interrupted temp records are retained, not reset.
            using (var stream = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = StrictUtf8.GetBytes(JsonUtility.ToJson(value));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
        }
        private static string FullRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)) throw Invalid();
            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(full) || full == Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) throw Invalid();
            return full;
        }
        private static string SourceRelative(string root, string source)
        {
            if (string.IsNullOrWhiteSpace(source) || !Path.IsPathRooted(source)) throw Invalid();
            string prefix = FullRoot(root) + Path.DirectorySeparatorChar, full = Path.GetFullPath(source);
            if (!full.StartsWith(prefix, StringComparison.Ordinal)) throw Invalid();
            string relative = full.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/');
            SourceDirectory(root, relative);
            return relative;
        }
        private static string SourceDirectory(string root, string relative)
        {
            const string linkedPrefix = ProductionRelative + "/linked-accounts/";
            if (relative != ProductionRelative && (relative == null || !relative.StartsWith(linkedPrefix, StringComparison.Ordinal) ||
                !Hex(relative.Substring(linkedPrefix.Length), 32))) throw Invalid();
            string full = Path.Combine(FullRoot(root), relative.Replace('/', Path.DirectorySeparatorChar));
            EnsureNotLinked(root, full);
            return full;
        }
        private static void EnsureSafeRecoveryPaths(string root)
        {
            EnsureNotLinked(root, RecoveryDirectory(root));
            EnsureNotLinked(root, IsolatedDirectory(root));
            EnsureNotLinked(root, IsolatedSavePath(root) + ".history");
            foreach (string path in new[] { JournalPath(root), IsolatedSavePath(root), Path.Combine(IsolatedDirectory(root), "online-identity.json") })
            {
                EnsureNotLinked(root, path);
                EnsureNotLinked(root, path + ".tmp");
                EnsureNotLinked(root, path + ".bak");
            }
        }
        private static void EnsureSaveWritePaths(string root, long revision)
        {
            EnsureSafeRecoveryPaths(root);
            string save = IsolatedSavePath(root), generation = Path.Combine(save + ".history", revision.ToString("D20") + ".json");
            foreach (string path in new[] { save, generation })
            {
                EnsureNotLinked(root, path);
                EnsureNotLinked(root, path + ".tmp");
                EnsureNotLinked(root, path + ".bak");
                if (EntryExists(path + ".tmp")) throw Invalid();
            }
            if (EntryExists(generation)) throw Invalid();
        }
        private static bool EntryExists(string path)
        {
            if (File.Exists(path) || Directory.Exists(path)) return true;
            string parent = Path.GetDirectoryName(path), name = Path.GetFileName(path);
            return Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent).Any(p => Path.GetFileName(p) == name);
        }
        private static void EnsureNotLinked(string root, string path)
        {
            string boundary = FullRoot(root), current = Path.GetFullPath(path);
            while (current != boundary)
            {
                if (!current.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw Invalid();
                RejectLinkedEntry(current);
                current = Path.GetDirectoryName(current);
            }
            RejectLinkedEntry(boundary);
        }
        private static void RejectLinkedEntry(string path)
        {
            if (!EntryExists(path)) return;
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch { throw Invalid(); }
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw Invalid();
        }
    }
}
