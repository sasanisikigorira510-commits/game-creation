using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class WitchTowerPurchaseIsolationPreparationTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string Boundary = "/Users/andou/Library/Application Support/NasusBackups";
        private const string Player = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Token = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Transaction = "1234567890123456789";
        private const string Product = "com.nasus.dungeonmonsterroguelike.crystals15000";
        private string root, outside;
        private object journal;
        private static Type EditorType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("WitchTowerPurchaseIsolationPreparation")).First(t => t != null);
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static Type Nested(string name) => EditorType.GetNestedType(name, BindingFlags.NonPublic);
        private static object Call(string name, params object[] args) => EditorType.GetMethod(name, PrivateStatic).Invoke(null, args);
        private static object Field(object obj, string field) => obj.GetType().GetField(field).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field).SetValue(obj, value);
        private static object Decode(string name, byte[] bytes) => JsonUtility.FromJson(Encoding.UTF8.GetString(bytes), Nested(name));
        private static byte[] Encode(object value) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));
        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void Reject(Action action) => Assert.Throws<TargetInvocationException>(() => action());
        private static void SymbolicLink(string link, string target)
        {
            if (!File.Exists("/bin/ln")) Assert.Ignore("Unix link coverage requires /bin/ln.");
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/ln", Arguments = "-s " + Quote(target) + " " + Quote(link),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
            });
            Assert.IsTrue(process.WaitForExit(10000), "Creating a test-owned link timed out.");
            Assert.AreEqual(0, process.ExitCode, "Creating a test-owned link failed.");
        }
        [SetUp] public void SetUp()
        {
            root = Path.Combine(Boundary, "isolated-editor-tests-" + Guid.NewGuid().ToString("N"));
            outside = root + "-outside";
            Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
            journal = Activator.CreateInstance(T("Save.PurchaseIsolationRecoveryJournal"));
            Set(journal, "SourcePlayerId", Player); Set(journal, "SandboxToken", Token);
            var request = Activator.CreateInstance(T("Save.OnlineRequest"));
            Set(request, "Kind", "purchase"); Set(request, "Target", Product);
            Set(request, "TransactionId", Transaction); Set(request, "RequestId", "apple-" + Transaction);
            Set(request, "Receipt", "synthetic-private-receipt-never-send"); Set(request, "Epoch", 4);
            Set(journal, "Request", request);
        }
        [TearDown] public void TearDown()
        {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root, true);
            if (!string.IsNullOrEmpty(outside) && Directory.Exists(outside)) Directory.Delete(outside, true);
        }
        private byte[] Input() => Encoding.UTF8.GetBytes((string)Call("BuildJobJson", journal));
        private object Operation()
        {
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "RequestId", "apple-" + Transaction); Set(op, "Kind", "purchase"); Set(op, "Target", Product);
            Set(op, "TransactionId", Transaction); Set(op, "Revision", 1L); Set(op, "Free", 900); Set(op, "Paid", 15000);
            Set(op, "Monsters", Array.CreateInstance(T("Save.OwnedMonsterData"), 0));
            return op;
        }
        private static string CanonicalOperationHash()
        {
            // Independently mirror Python json.dumps(sort_keys=True,
            // separators=(',', ':'), ensure_ascii=False) for the pinned schema.
            string raw = "{\"ClaimDate\":\"\",\"Free\":900,\"GoldDelta\":0,\"Kind\":\"purchase\",\"Monsters\":[],\"Paid\":15000," +
                "\"RefundDebt\":0,\"RequestId\":\"apple-" + Transaction + "\",\"Revision\":1,\"Target\":\"" + Product +
                "\",\"TransactionId\":\"" + Transaction + "\",\"TutorialPulls\":0}";
            return Hash(Encoding.UTF8.GetBytes(raw));
        }
        private object Receipt(byte[] input)
        {
            var receipt = Activator.CreateInstance(Nested("Receipt"), true);
            Set(receipt, "Version", 1); Set(receipt, "Durable", true);
            Set(receipt, "JobId", Field(Decode("Job", input), "JobId")); Set(receipt, "InputSha256", Hash(input));
            Set(receipt, "InstanceId", "nasus-refund-sandbox-20260928"); Set(receipt, "Environment", "Sandbox");
            Set(receipt, "BundleId", "com.nasus.dungeonmonsterroguelike"); Set(receipt, "PlayerId", Player);
            Set(receipt, "TransactionId", Transaction); Set(receipt, "ProductId", Product);
            Set(receipt, "Operation", Operation()); Set(receipt, "OperationSha256", CanonicalOperationHash());
            return receipt;
        }
        private object Verify(byte[] input, byte[] proof) => Call("ValidateOperatorResult", input, proof, journal, Hash(input), Hash(proof));
        private string CopiedSource(bool linked)
        {
            string production = Path.Combine(root, "environments", "production");
            string source = linked ? Path.Combine(production, "linked-accounts", new string('c', 32)) : production;
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "save.json"), "{\"PlayerId\":\"" + Player + "\",\"PlayerLevel\":1}");
            File.WriteAllText(Path.Combine(source, "online-identity.json"), "{\"PlayerId\":\"" + Player + "\",\"Token\":\"" + Token + "\",\"Legacy\":false}");
            if (linked) File.WriteAllText(Path.Combine(production, "active-account.json"),
                "{\"SlotId\":\"" + new string('c', 32) + "\",\"PlayerId\":\"" + Player + "\"}");
            return source;
        }

        [Test] public void ValidPrivateRootAndDescendantPathDoNotCreateOrRewriteFiles()
        {
            Assert.AreEqual(root, Call("ValidatePrivateRoot", root + Path.DirectorySeparatorChar));
            string pending = Path.Combine(root, "environments", "production", "online-request.json");
            Call("EnsurePrivateBackupPath", root, pending);
            Assert.IsFalse(File.Exists(pending)); Assert.IsEmpty(Directory.GetFileSystemEntries(root));
        }
        [TestCase("/")] [TestCase("/Users/andou")] [TestCase("/Users/andou/Library/Application Support/NasusBackups")]
        [TestCase("/Users/andou/Library/Application Support/NasusBackupsOther/test")]
        [TestCase("/Users/andou/Desktop/game-creation")] [TestCase("relative-backup")]
        public void RootMustBeAnExistingNarrowPrivateBackupDescendant(string invalid)
        {
            Reject(() => Call("ValidatePrivateRoot", invalid)); Assert.IsEmpty(Directory.GetFileSystemEntries(root));
        }
        [Test] public void MissingRootAndPathTraversalOrSiblingCannotEscapePrivateScope()
        {
            Reject(() => Call("ValidatePrivateRoot", Path.Combine(root, "not-created")));
            Reject(() => Call("EnsurePrivateBackupPath", root, Path.Combine(outside, "proof.json")));
            Reject(() => Call("EnsurePrivateBackupPath", root, Path.Combine(root, "..", Path.GetFileName(outside), "proof.json")));
            Reject(() => Call("EnsurePrivateBackupPath", root, root));
            Reject(() => Call("EnsurePrivateBackupPath", root, "relative-proof.json"));
        }
        [Test] public void PrivateRootRejectsLinkedAncestorEvenWhenItsLeafIsARealDirectory()
        {
            string linked = Path.Combine(root, "linked"), child = Path.Combine(outside, "child");
            Directory.CreateDirectory(child); SymbolicLink(linked, outside);
            Reject(() => Call("ValidatePrivateRoot", Path.Combine(linked, "child")));
            Reject(() => Call("EnsurePrivateBackupPath", root, Path.Combine(linked, "child", "result.json")));
            Assert.IsEmpty(Directory.GetFileSystemEntries(child));
        }
        [TestCase(false)] [TestCase(true)]
        public void PrivatePathRejectsExistingAndDanglingFileLinks(bool dangling)
        {
            string target = Path.Combine(outside, "target.json"), linked = Path.Combine(root, "proof.json");
            if (!dangling) File.WriteAllText(target, "synthetic-target-must-not-change");
            SymbolicLink(linked, target);
            Reject(() => Call("EnsurePrivateBackupPath", root, linked));
            if (!dangling) Assert.AreEqual("synthetic-target-must-not-change", File.ReadAllText(target));
            else Assert.IsFalse(File.Exists(target));
        }
        [Test] public void NewPrivateWriterPersistsExactlyOnceWithoutReplacingExistingRecord()
        {
            string path = Path.Combine(root, "operator-job.json"); Call("EnsurePrivateBackupPath", root, path);
            Call("WriteNewPrivateFile", path, "{\"synthetic\":true}");
            Assert.AreEqual("{\"synthetic\":true}", File.ReadAllText(path)); Assert.IsFalse(File.Exists(path + ".tmp"));
            Reject(() => Call("WriteNewPrivateFile", path, "replacement"));
            Assert.AreEqual("{\"synthetic\":true}", File.ReadAllText(path));
        }
        [TestCase(false)] [TestCase(true)]
        public void NewPrivateWriterNeverFollowsOrReplacesTempLink(bool dangling)
        {
            string path = Path.Combine(root, "operator-job.json"), target = Path.Combine(outside, "target.json");
            if (!dangling) File.WriteAllText(target, "synthetic-target-must-not-change");
            SymbolicLink(path + ".tmp", target);
            Reject(() => Call("WriteNewPrivateFile", path, "synthetic-secret")); Assert.IsFalse(File.Exists(path));
            if (!dangling) Assert.AreEqual("synthetic-target-must-not-change", File.ReadAllText(target));
            else Assert.IsFalse(File.Exists(target));
        }
        [Test] public void NewPrivateWriterRetainsInterruptedRegularTempRecord()
        {
            string path = Path.Combine(root, "operator-job.json"); File.WriteAllText(path + ".tmp", "interrupted-record");
            Reject(() => Call("WriteNewPrivateFile", path, "replacement"));
            Assert.AreEqual("interrupted-record", File.ReadAllText(path + ".tmp")); Assert.IsFalse(File.Exists(path));
        }
        [TestCase(false)] [TestCase(true)]
        public void CopiedSourceSelectionKeepsGuestAndLinkedAccountFilesUnchanged(bool linked)
        {
            string source = CopiedSource(linked), save = File.ReadAllText(Path.Combine(source, "save.json"));
            string identity = File.ReadAllText(Path.Combine(source, "online-identity.json"));
            Assert.AreEqual(source, Call("ValidateCopiedSource", root));
            Assert.AreEqual(save, File.ReadAllText(Path.Combine(source, "save.json")));
            Assert.AreEqual(identity, File.ReadAllText(Path.Combine(source, "online-identity.json")));
        }
        [TestCase("pointer")] [TestCase("pointer_tmp")] [TestCase("save")] [TestCase("save_tmp")]
        [TestCase("identity")] [TestCase("identity_tmp")]
        public void CopiedSourceRejectsLinksBeforeReadingSelectedAccount(string fault)
        {
            string source = CopiedSource(true), production = Path.Combine(root, "environments", "production");
            string path = fault.StartsWith("pointer", StringComparison.Ordinal) ? Path.Combine(production, "active-account.json") :
                Path.Combine(source, fault.StartsWith("identity", StringComparison.Ordinal) ? "online-identity.json" : "save.json");
            if (fault.EndsWith("_tmp", StringComparison.Ordinal)) path += ".tmp";
            if (File.Exists(path)) File.Delete(path);
            string target = Path.Combine(outside, "missing-test-owned-target"); SymbolicLink(path, target);
            Reject(() => Call("ValidateCopiedSource", root)); Assert.IsFalse(File.Exists(target));
        }
        [TestCase("pointer_tmp")] [TestCase("save_tmp")] [TestCase("identity_tmp")]
        public void CopiedSourceRejectsInterruptedRecordsWithoutResettingThem(string fault)
        {
            string source = CopiedSource(true), production = Path.Combine(root, "environments", "production");
            string path = fault == "pointer_tmp" ? Path.Combine(production, "active-account.json.tmp") :
                Path.Combine(source, fault == "save_tmp" ? "save.json.tmp" : "online-identity.json.tmp");
            File.WriteAllText(path, "synthetic-interrupted-record");
            Reject(() => Call("ValidateCopiedSource", root)); Assert.AreEqual("synthetic-interrupted-record", File.ReadAllText(path));
        }
        [Test] public void CopiedPointerCannotTraverseOutsideOrChangeSelectedPlayer()
        {
            CopiedSource(true); string pointer = Path.Combine(root, "environments", "production", "active-account.json");
            File.WriteAllText(pointer, "{\"SlotId\":\"../../escape\",\"PlayerId\":\"" + Player + "\"}");
            Reject(() => Call("ValidateCopiedSource", root));
            File.WriteAllText(pointer, "{\"SlotId\":\"" + new string('c', 32) + "\",\"PlayerId\":\"" + new string('d', 32) + "\"}");
            Reject(() => Call("ValidateCopiedSource", root)); Assert.IsEmpty(Directory.GetFileSystemEntries(outside));
        }
        [Test] public void JobSerializationIsExactlyThePinnedServerContractAndNeverCopiesProductionProgress()
        {
            byte[] input = Input(); var job = Decode("Job", input); var request = Field(job, "Request");
            var snapshot = Field(job, "InitialSnapshot");
            CollectionAssert.AreEquivalent(new[] { "Version", "JobId", "PlayerId", "SandboxToken", "Request", "InitialSnapshot" },
                Nested("Job").GetFields().Select(f => f.Name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "RequestId", "Kind", "Target", "TransactionId", "Receipt", "Epoch" },
                Nested("Request").GetFields().Select(f => f.Name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "PlayerId", "SaveRevision", "EconomyRevision", "RecoveryEpoch", "SchemaVersion", "PlayerLevel", "Gold",
                "FreeGachaStones", "PaidGachaStones", "MonsterStorageLimit", "InitialTutorialSummonCount", "HasCompletedTutorial", "OwnedMonsters", "OwnedEquipments" },
                Nested("Snapshot").GetFields().Select(f => f.Name).ToArray());
            Assert.AreEqual(1, Field(job, "Version")); Assert.AreEqual(Player, Field(job, "PlayerId")); Assert.AreEqual(Token, Field(job, "SandboxToken"));
            Assert.AreEqual(32, ((string)Field(job, "JobId")).Length); Assert.AreEqual(0, Field(request, "Epoch"));
            Assert.AreEqual(4, Field(Field(journal, "Request"), "Epoch"), "The production request object must remain unchanged.");
            Assert.AreEqual("synthetic-private-receipt-never-send", Field(request, "Receipt"));
            Assert.AreEqual(Player, Field(snapshot, "PlayerId")); Assert.AreEqual(1L, Field(snapshot, "SaveRevision"));
            Assert.AreEqual(0L, Field(snapshot, "EconomyRevision")); Assert.AreEqual(0, Field(snapshot, "RecoveryEpoch"));
            Assert.AreEqual(3, Field(snapshot, "SchemaVersion")); Assert.AreEqual(1, Field(snapshot, "PlayerLevel")); Assert.AreEqual(100, Field(snapshot, "Gold"));
            Assert.AreEqual(900, Field(snapshot, "FreeGachaStones")); Assert.AreEqual(0, Field(snapshot, "PaidGachaStones"));
            Assert.AreEqual(100, Field(snapshot, "MonsterStorageLimit")); Assert.AreEqual(0, Field(snapshot, "InitialTutorialSummonCount"));
            Assert.IsFalse((bool)Field(snapshot, "HasCompletedTutorial"));
            Assert.IsEmpty((IEnumerable)Field(snapshot, "OwnedMonsters")); Assert.IsEmpty((IEnumerable)Field(snapshot, "OwnedEquipments"));
            Assert.IsEmpty(Directory.GetFileSystemEntries(root), "Pure serialization must not stage a real job or contact a server.");
        }
        [Test] public void CanonicalOperationHashMatchesPinnedPythonSchemaIndependently()
        {
            Assert.AreEqual(CanonicalOperationHash(), Call("CanonicalPurchaseOperationHash", journal, Operation()));
        }
        [TestCase("Kind", "reward")] [TestCase("Target", "com.nasus.dungeonmonsterroguelike.crystals120")]
        [TestCase("Count", 1)] [TestCase("Paid", true)] [TestCase("TransactionId", "synthetic-nonnumeric")]
        [TestCase("RequestId", "other-request")] [TestCase("Receipt", "")]
        public void JobBuilderCannotExpandApprovedPurchaseScope(string field, object value)
        {
            Set(Field(journal, "Request"), field, value); Reject(() => Call("BuildJobJson", journal));
            Assert.IsEmpty(Directory.GetFileSystemEntries(root));
        }
        [Test] public void ExactVerifiedResultReturnsOperationWithoutMutatingJournalOrFiles()
        {
            byte[] input = Input(), proof = Encode(Receipt(input)); string before = JsonUtility.ToJson(journal);
            var result = Verify(input, proof);
            Assert.AreEqual(15000, Field(Field(result, "Operation"), "Paid")); Assert.AreEqual(1, Field(result, "Version"));
            Assert.AreEqual(before, JsonUtility.ToJson(journal)); Assert.IsEmpty(Directory.GetFileSystemEntries(root));
        }
        [TestCase("Version", 2)] [TestCase("Durable", false)] [TestCase("Environment", "Production")]
        [TestCase("BundleId", "other.bundle")] [TestCase("InstanceId", "other-instance")]
        [TestCase("PlayerId", "cccccccccccccccccccccccccccccccc")] [TestCase("TransactionId", "999999")]
        [TestCase("ProductId", "com.nasus.dungeonmonsterroguelike.crystals120")]
        [TestCase("JobId", "cccccccccccccccccccccccccccccccc")] [TestCase("InputSha256", "wrong")]
        [TestCase("OperationSha256", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
        public void ProofMustMatchVersionEnvironmentBundleAndExactApprovedScope(string field, object value)
        {
            byte[] input = Input(); var receipt = Receipt(input); Set(receipt, field, value);
            Reject(() => Verify(input, Encode(receipt))); Assert.IsEmpty(Directory.GetFileSystemEntries(root));
        }
        [TestCase("Version", 2)] [TestCase("PlayerId", "cccccccccccccccccccccccccccccccc")]
        [TestCase("SandboxToken", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
        [TestCase("JobId", "invalid")]
        public void ChangedJobIdentityOrVersionIsRejectedEvenWithMatchingInputByteHash(string field, object value)
        {
            var job = Decode("Job", Input()); Set(job, field, value); byte[] input = Encode(job);
            Reject(() => Verify(input, Encode(Receipt(input))));
        }
        [TestCase("Epoch", 4)] [TestCase("Kind", "reward")] [TestCase("RequestId", "other-request")]
        [TestCase("TransactionId", "999999")][TestCase("Target", "com.nasus.dungeonmonsterroguelike.crystals120")]
        [TestCase("Receipt", "different-receipt")]
        public void ChangedJobRequestCannotBeApprovedByMatchingResultHashes(string field, object value)
        {
            var job = Decode("Job", Input()); Set(Field(job, "Request"), field, value); byte[] input = Encode(job);
            Reject(() => Verify(input, Encode(Receipt(input))));
        }
        [TestCase("SaveRevision", 2L)] [TestCase("EconomyRevision", 1L)] [TestCase("RecoveryEpoch", 1)]
        [TestCase("PlayerLevel", 9)] [TestCase("Gold", 9000)] [TestCase("FreeGachaStones", 1000)]
        [TestCase("PaidGachaStones", 15000)] [TestCase("MonsterStorageLimit", 380)]
        [TestCase("InitialTutorialSummonCount", 1)] [TestCase("HasCompletedTutorial", true)]
        public void JobSnapshotMustBeFreshDefaultInsteadOfCopiedProgress(string field, object value)
        {
            var job = Decode("Job", Input()); Set(Field(job, "InitialSnapshot"), field, value); byte[] input = Encode(job);
            Reject(() => Verify(input, Encode(Receipt(input))));
        }
        [TestCase("Paid", 14999)] [TestCase("Free", 901)] [TestCase("Revision", 2L)] [TestCase("GoldDelta", 1)]
        [TestCase("RefundDebt", 1)] [TestCase("TutorialPulls", 1)] [TestCase("ClaimDate", "synthetic-date")]
        [TestCase("RelicId", "relic_safe_ember")] [TestCase("RelicAmount", 1)] [TestCase("UpgradeField", "MonsterStorageLimit")]
        public void ResultOperationCannotContainChangedWalletOrOtherRewards(string field, object value)
        {
            byte[] input = Input(); var receipt = Receipt(input); Set(Field(receipt, "Operation"), field, value);
            Reject(() => Verify(input, Encode(receipt)));
        }
        [Test] public void ResultCannotAddMonstersOrUseMalformedBinaryOrHashInputs()
        {
            byte[] input = Input(); var receipt = Receipt(input);
            Set(Field(receipt, "Operation"), "Monsters", Array.CreateInstance(T("Save.OwnedMonsterData"), 1));
            Reject(() => Verify(input, Encode(receipt)));
            byte[] proof = Encode(Receipt(input));
            Reject(() => Call("ValidateOperatorResult", input, proof, journal, new string('c', 64), Hash(proof)));
            Reject(() => Call("ValidateOperatorResult", input, proof, journal, Hash(input), new string('c', 64)));
            Reject(() => Verify(new byte[] { 0xff }, proof)); Reject(() => Verify(input, new byte[] { 0xff }));
        }
    }
}
