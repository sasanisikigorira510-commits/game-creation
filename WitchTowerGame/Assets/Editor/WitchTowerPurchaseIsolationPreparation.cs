using System;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WitchTower.Monetization;
using WitchTower.Save;

// Explicit operator preparation on a PRIVATE COPY of iPhone Documents only.
// No network, StoreKit calls, active account switch or live-save writes.
public static class WitchTowerPurchaseIsolationPreparation
{
    private const string Instance = "nasus-refund-sandbox-20260928";
    [Serializable] private sealed class Snapshot
    {
        public string PlayerId;
        public long SaveRevision = 1, EconomyRevision;
        public int RecoveryEpoch, SchemaVersion = 3, PlayerLevel = 1, Gold = 100;
        public int FreeGachaStones = 900, PaidGachaStones, MonsterStorageLimit = 100;
        public int InitialTutorialSummonCount;
        public bool HasCompletedTutorial;
        public OwnedMonsterData[] OwnedMonsters = Array.Empty<OwnedMonsterData>();
        public OwnedEquipmentData[] OwnedEquipments = Array.Empty<OwnedEquipmentData>();
    }
    [Serializable] private sealed class Request
    {
        public string RequestId, Kind, Target, TransactionId, Receipt;
        public int Epoch;
    }
    [Serializable] private sealed class Job
    {
        public int Version = 1;
        public string JobId, PlayerId, SandboxToken;
        public Request Request;
        public Snapshot InitialSnapshot;
    }
    [Serializable] private sealed class Receipt
    {
        public int Version;
        public string JobId, InputSha256, InstanceId, Environment, BundleId, PlayerId, TransactionId, ProductId, OperationSha256;
        public bool Durable;
        public OnlineOperation Operation;
    }
    [Serializable] private sealed class Pointer { public string SlotId, PlayerId; }

    public static void PrepareFromBackup()
    {
        try
        {
            string root = BackupRoot();
            string production = Path.Combine(root, "environments", "production");
            string source = ValidateCopiedSource(root);
            string pending = Path.Combine(source, "online-request.json");
            EnsurePrivateBackupPath(root, pending);
            EnsurePrivateBackupPath(root, pending + ".tmp");
            EnsurePrivateBackupPath(root, Path.Combine(source, "save.json"));
            Require(!File.Exists(pending + ".tmp") && File.Exists(pending));
            Require(Hash(File.ReadAllBytes(pending)) == Value("-approvedPendingSha256"));
            var request = JsonUtility.FromJson<OnlineRequest>(File.ReadAllText(pending));
            var save = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(Path.Combine(source, "save.json")));
            Require(save != null && Hex(save.PlayerId, 32));
            AppleRecoveryStorage.ValidateActivePlayer(production, save);
            Require(request != null && request.Kind == "purchase" && request.Target == IapProductCatalog.Crystals15000 &&
                request.TransactionId != null && Regex.IsMatch(request.TransactionId, "\\A[0-9]{1,32}\\z") &&
                request.RequestId == "apple-" + request.TransactionId);
            var journal = PurchaseIsolationRecoveryStorage.Read(root) ?? PurchaseIsolationRecoveryStorage.Begin(
                root, source, save.PlayerId, request.RequestId, request.TransactionId, request.Target);
            Require(journal.SourcePlayerId == save.PlayerId && journal.OriginalRequestHash == Value("-approvedPendingSha256") &&
                journal.Request.RequestId == request.RequestId && journal.Request.TransactionId == request.TransactionId);
            string jobPath = Path.Combine(Path.GetDirectoryName(PurchaseIsolationRecoveryStorage.JournalPath(root)), "operator-job.json");
            EnsurePrivateBackupPath(root, jobPath);
            EnsurePrivateBackupPath(root, jobPath + ".tmp");
            if (EntryExists(jobPath) || EntryExists(jobPath + ".tmp"))
                throw new InvalidOperationException("Operator input exists; review or resume without replacing it.");
            WriteNewPrivateFile(jobPath, BuildJobJson(journal));
            Require(Hash(File.ReadAllBytes(pending)) == journal.OriginalRequestHash);
            Debug.Log("PURCHASE_ISOLATION_PRIVATE_INPUT_READY: no live device or production data changed.");
            EditorApplication.Exit(0);
        }
        catch
        {
            Debug.LogError("PURCHASE_ISOLATION_PREPARATION_STOPPED: private files retained; no live device changes.");
            EditorApplication.Exit(1);
        }
    }

    public static void CompleteFromVerifiedOperatorResult()
    {
        try
        {
            string root = BackupRoot();
            var journal = PurchaseIsolationRecoveryStorage.Read(root);
            Require(journal != null);
            string jobPath = Path.Combine(Path.GetDirectoryName(PurchaseIsolationRecoveryStorage.JournalPath(root)), "operator-job.json");
            string proofPath = Path.GetFullPath(Value("-operatorResultPath"));
            EnsurePrivateBackupPath(root, jobPath);
            EnsurePrivateBackupPath(root, proofPath);
            var result = ValidateOperatorResult(File.ReadAllBytes(jobPath), File.ReadAllBytes(proofPath), journal,
                Value("-operatorJobSha256"), Value("-operatorResultSha256"));
            PurchaseIsolationRecoveryStorage.MarkServerDelivered(root, RefundSandboxConfiguration.Endpoint,
                result.PlayerId, result.Operation);
            PurchaseIsolationRecoveryStorage.PersistLocalDelivery(root);
            Require(PurchaseIsolationRecoveryStorage.CanConfirm(root, result.ProductId, result.TransactionId));
            // Do not clear the copied source pending. On the phone, the runtime
            // verifies the original byte hash again before retiring that record.
            Require(!PurchaseIsolationRecoveryStorage.Read(root).SourcePendingCleared);
            Debug.Log("PURCHASE_ISOLATION_PRIVATE_DELIVERY_READY: isolated durable save only; StoreKit not confirmed.");
            EditorApplication.Exit(0);
        }
        catch
        {
            Debug.LogError("PURCHASE_ISOLATION_DELIVERY_STAGING_STOPPED: records retained; StoreKit not confirmed.");
            EditorApplication.Exit(1);
        }
    }

    private static string BackupRoot()
    {
        return ValidatePrivateRoot(Value("-isolationRoot"));
    }
    private static string ValidatePrivateRoot(string requestedPath)
    {
        Require(!string.IsNullOrWhiteSpace(requestedPath) && Path.IsPathRooted(requestedPath));
        string root = Path.GetFullPath(requestedPath).TrimEnd(Path.DirectorySeparatorChar);
        // This task-specific operator tool cannot be aimed at the actual app's
        // persistent directory, the Unity project or a broad home directory.
        const string boundary = "/Users/andou/Library/Application Support/NasusBackups/";
        Require(root.StartsWith(boundary, StringComparison.Ordinal) && Directory.Exists(root));
        for (string path = root; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            if (EntryExists(path)) Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
        return root;
    }
    private static void EnsurePrivateBackupPath(string root, string requestedPath)
    {
        root = ValidatePrivateRoot(root);
        Require(!string.IsNullOrEmpty(requestedPath) && Path.IsPathRooted(requestedPath));
        string path = Path.GetFullPath(requestedPath);
        Require(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        while (path != root)
        {
            if (EntryExists(path)) Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
            path = Path.GetDirectoryName(path);
        }
    }
    private static string ValidateCopiedSource(string root)
    {
        string production = Path.Combine(ValidatePrivateRoot(root), "environments", "production");
        string pointerPath = Path.Combine(production, "active-account.json");
        EnsurePrivateBackupPath(root, production);
        EnsurePrivateBackupPath(root, pointerPath);
        EnsurePrivateBackupPath(root, pointerPath + ".tmp");
        Require(Directory.Exists(production) && !EntryExists(pointerPath + ".tmp"));
        string source = production;
        if (EntryExists(pointerPath))
        {
            var pointer = JsonUtility.FromJson<Pointer>(File.ReadAllText(pointerPath, new UTF8Encoding(false, true)));
            Require(pointer != null && Hex(pointer.SlotId, 32) && Hex(pointer.PlayerId, 32));
            source = Path.Combine(production, "linked-accounts", pointer.SlotId);
        }
        EnsurePrivateBackupPath(root, source);
        foreach (string name in new[] { "online-identity.json", "save.json" })
        {
            string path = Path.Combine(source, name);
            EnsurePrivateBackupPath(root, path);
            EnsurePrivateBackupPath(root, path + ".tmp");
            Require(File.Exists(path) && !EntryExists(path + ".tmp"));
        }
        Require(AppleRecoveryStorage.ActiveDirectory(production) == source);
        return source;
    }
    private static bool EntryExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) return true;
        string parent = Path.GetDirectoryName(path), name = Path.GetFileName(path);
        if (!Directory.Exists(parent)) return false;
        foreach (string child in Directory.EnumerateFileSystemEntries(parent))
            if (Path.GetFileName(child) == name) return true;
        return false;
    }
    private static void WriteNewPrivateFile(string path, string json)
    {
        Require(!EntryExists(path) && !EntryExists(path + ".tmp"));
        using (var stream = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        File.Move(path + ".tmp", path);
    }
    private static void ValidateApprovedJournal(PurchaseIsolationRecoveryJournal journal)
    {
        var request = journal?.Request;
        Require(journal != null && Hex(journal.SourcePlayerId, 32) && Hex(journal.SandboxToken, 64) &&
            request != null && request.Kind == "purchase" && request.Count == 0 && !request.Paid &&
            request.Target == IapProductCatalog.Crystals15000 && request.TransactionId != null &&
            Regex.IsMatch(request.TransactionId, "\\A[0-9]{1,32}\\z") && request.RequestId == "apple-" + request.TransactionId &&
            !string.IsNullOrWhiteSpace(request.Receipt));
    }
    private static string BuildJobJson(PurchaseIsolationRecoveryJournal journal)
    {
        ValidateApprovedJournal(journal);
        var request = journal.Request;
        return JsonUtility.ToJson(new Job
        {
            JobId = Guid.NewGuid().ToString("N"), PlayerId = journal.SourcePlayerId, SandboxToken = journal.SandboxToken,
            Request = new Request { Epoch = 0, Kind = "purchase", RequestId = request.RequestId,
                Target = request.Target, TransactionId = request.TransactionId, Receipt = request.Receipt },
            InitialSnapshot = new Snapshot { PlayerId = journal.SourcePlayerId }
        });
    }
    private static Receipt ValidateOperatorResult(byte[] input, byte[] proof, PurchaseIsolationRecoveryJournal journal,
        string expectedJobHash, string expectedProofHash)
    {
        ValidateApprovedJournal(journal);
        Require(Hex(expectedJobHash, 64) && Hex(expectedProofHash, 64) && Hash(input) == expectedJobHash && Hash(proof) == expectedProofHash);
        var utf8 = new UTF8Encoding(false, true);
        var job = JsonUtility.FromJson<Job>(utf8.GetString(input));
        var result = JsonUtility.FromJson<Receipt>(utf8.GetString(proof));
        Require(job != null && job.Version == 1 && Hex(job.JobId, 32) && job.Request != null &&
            job.Request.Epoch == 0 && job.Request.Kind == "purchase" && job.Request.Target == journal.Request.Target &&
            job.Request.RequestId == journal.Request.RequestId && job.Request.TransactionId == journal.Request.TransactionId &&
            job.Request.Receipt == journal.Request.Receipt &&
            JsonUtility.ToJson(job.InitialSnapshot) == JsonUtility.ToJson(new Snapshot { PlayerId = journal.SourcePlayerId }) &&
            result != null && result.Version == 1 && result.Durable && result.BundleId == "com.nasus.dungeonmonsterroguelike" &&
            result.Environment == "Sandbox" && result.InstanceId == Instance && result.InputSha256 == expectedJobHash &&
            result.JobId == job.JobId && result.PlayerId == journal.SourcePlayerId && job.PlayerId == journal.SourcePlayerId &&
            job.SandboxToken == journal.SandboxToken && result.TransactionId == journal.Request.TransactionId &&
            result.ProductId == journal.Request.Target && result.OperationSha256 == CanonicalPurchaseOperationHash(journal, result.Operation));
        return result;
    }
    // This is the exact pinned server's compact purchase schema, not a generic
    // serializer. A future schema change stops this one-off handoff for review.
    private static string CanonicalPurchaseOperationHash(PurchaseIsolationRecoveryJournal journal, OnlineOperation op)
    {
        Require(op != null && op.Kind == "purchase" && op.RequestId == journal.Request.RequestId &&
            op.Target == journal.Request.Target && op.TransactionId == journal.Request.TransactionId &&
            op.Revision == 1 && op.Free == 900 && op.Paid == 15000 && op.RefundDebt == 0 && op.GoldDelta == 0 &&
            op.TutorialPulls == 0 && string.IsNullOrEmpty(op.ClaimDate) && string.IsNullOrEmpty(op.RelicId) &&
            op.RelicAmount == 0 && string.IsNullOrEmpty(op.UpgradeField) && op.Monsters != null && op.Monsters.Length == 0);
        string canonical = "{\"ClaimDate\":\"\",\"Free\":900,\"GoldDelta\":0,\"Kind\":\"purchase\",\"Monsters\":[],\"Paid\":" +
            op.Paid.ToString(CultureInfo.InvariantCulture) + ",\"RefundDebt\":0,\"RequestId\":\"" + op.RequestId +
            "\",\"Revision\":1,\"Target\":\"" + op.Target + "\",\"TransactionId\":\"" + op.TransactionId + "\",\"TutorialPulls\":0}";
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }
    private static string Value(string flag)
    {
        var args = System.Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, flag);
        Require(index >= 0 && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]));
        return args[index + 1];
    }
    private static bool Hex(string value, int count) => value != null && Regex.IsMatch(value, "\\A[a-f0-9]{" + count + "}\\z");
    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    private static void Require(bool value)
    {
        if (!value) throw new InvalidDataException("Private isolation preparation failed.");
    }
}
