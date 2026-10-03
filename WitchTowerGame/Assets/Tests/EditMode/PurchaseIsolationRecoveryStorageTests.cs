using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class PurchaseIsolationRecoveryStorageTests
    {
        private string root, source, pending, raw, product;
        private const string PlayerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Transaction = "synthetic-isolation-transaction";
        private const string RequestId = "apple-synthetic-isolation-transaction";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(string method, params object[] args) => T("Save.PurchaseIsolationRecoveryStorage").GetMethod(method).Invoke(null, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static object Clone(object obj) => JsonUtility.FromJson(JsonUtility.ToJson(obj), obj.GetType());
        private static string Endpoint => (string)T("Save.RefundSandboxConfiguration").GetField("Endpoint").GetRawConstantValue();
        private string JournalPath => (string)Call("JournalPath", root);
        private string SavePath => (string)Call("IsolatedSavePath", root);
        private string IdentityPath => Path.Combine((string)Call("IsolatedDirectory", root), "online-identity.json");

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "NasusPurchaseIsolation_" + Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "environments", "production", "linked-accounts", new string('b', 32));
            Directory.CreateDirectory(source);
            product = (string)T("Monetization.IapProductCatalog").GetField("Crystals15000").GetRawConstantValue();
            pending = Path.Combine(source, "online-request.json");
            var request = Activator.CreateInstance(T("Save.OnlineRequest"));
            Set(request, "Kind", "purchase"); Set(request, "Target", product); Set(request, "RequestId", RequestId);
            Set(request, "TransactionId", Transaction); Set(request, "Receipt", "synthetic-receipt-never-send"); Set(request, "Epoch", 4);
            raw = JsonUtility.ToJson(request, true);
            File.WriteAllText(pending, raw);
            File.WriteAllText(Path.Combine(source, "save.json"), "synthetic-production-save-must-not-change");
            File.WriteAllText(Path.Combine(source, "online-identity.json"), "synthetic-production-credential-must-not-copy");
        }
        [TearDown] public void TearDown()
        {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root, true);
        }
        private object Begin() => Call("Begin", root, source, PlayerId, RequestId, Transaction, product);
        private object Read() => Call("Read", root);
        private object Operation()
        {
            var op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "Kind", "purchase"); Set(op, "Target", product); Set(op, "RequestId", RequestId);
            Set(op, "TransactionId", Transaction); Set(op, "Revision", 1L); Set(op, "Free", 900); Set(op, "Paid", 15000);
            return op;
        }
        private void ServerDelivered() => Call("MarkServerDelivered", root, Endpoint, PlayerId, Operation());
        private void LocalDelivered() { Begin(); ServerDelivered(); Call("PersistLocalDelivery", root); }
        private bool CanConfirm(string selectedProduct = null, string transaction = Transaction) =>
            (bool)Call("CanConfirm", root, selectedProduct ?? product, transaction);
        private object LoadSave(string path = null) => JsonUtility.FromJson(File.ReadAllText(path ?? SavePath), T("Save.PlayerSaveData"));
        private void WriteJournal(object journal) => File.WriteAllText(JournalPath, JsonUtility.ToJson(journal));
        private void AssertProductionUntouched(bool requestPresent = true)
        {
            Assert.AreEqual("synthetic-production-save-must-not-change", File.ReadAllText(Path.Combine(source, "save.json")));
            Assert.AreEqual("synthetic-production-credential-must-not-copy", File.ReadAllText(Path.Combine(source, "online-identity.json")));
            if (requestPresent) Assert.AreEqual(raw, File.ReadAllText(pending));
            Assert.IsFalse(Directory.Exists(Path.Combine(source, "save.json.history")));
        }
        private void Reject(Action action) => Assert.Throws<TargetInvocationException>(() => action());
        private static void SymbolicLink(string link, string target)
        {
            if (!File.Exists("/bin/ln")) Assert.Ignore("Unix symbolic-link coverage requires /bin/ln.");
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

        // Synthetic legacy files: remove the exact four new fields from Unity's
        // canonical JSON. This fixture generator is independent of the frozen DTO.
        private static string BeforeTrainingFields(string json)
        {
            int start = json.IndexOf(",\"TrainingDrops\":", StringComparison.Ordinal);
            int end = json.IndexOf(",\"FreeGachaStones\":", StringComparison.Ordinal);
            Assert.Greater(start, 0); Assert.Greater(end, start);
            return json.Substring(0, start) + json.Substring(end);
        }
        private static string Hash(string text)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        private void MakePreTrainingDeliveredFixture()
        {
            LocalDelivered(); Call("ClearOriginalPending", root); Call("MarkStoreConfirmed", root, product, Transaction);
            var save = LoadSave();
            Set(save, "SaveRevision", 0L); Set(save, "SavedAtUtc", ""); Set(save, "SaveReason", "");
            Set(save, "AppVersion", ""); Set(save, "DataWarnings", null);
            var journal = Read();
            Set(journal, "DeliveredSaveHash", Hash(BeforeTrainingFields(JsonUtility.ToJson(save))));
            foreach (string path in Directory.GetFiles((string)Call("IsolatedDirectory", root), "*.json", SearchOption.AllDirectories))
            {
                if (path == IdentityPath) continue;
                string value = JsonUtility.ToJson(JsonUtility.FromJson(File.ReadAllText(path), T("Save.PlayerSaveData")));
                File.WriteAllText(path, BeforeTrainingFields(value));
            }
            WriteJournal(journal);
        }
        [Test] public void PreTrainingVersion1DeliveredProofRemainsReadableWithoutRewritingAnyFile()
        {
            MakePreTrainingDeliveredFixture();
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(x => x, File.ReadAllBytes);
            Assert.AreEqual("store_confirmed", Field(Read(), "Phase")); Assert.IsTrue(CanConfirm());
            Assert.IsTrue((bool)Call("Matches", root, product, Transaction));
            Call("PersistLocalDelivery", root); // No regrant or changed journal after an upgrade.
            foreach (var pair in files) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            AssertProductionUntouched(false);
        }
        [TestCase("PaidGachaStones", 15001)] [TestCase("TrainingDrops", 1)] [TestCase("TrialStarCores", 1)]
        public void PreTrainingDigestDoesNotAcceptChangedWalletOrNewMaterials(string field, int amount)
        {
            MakePreTrainingDeliveredFixture();
            string generation = Path.Combine(SavePath + ".history", "00000000000000000002.json");
            var save = JsonUtility.FromJson(File.ReadAllText(generation), T("Save.PlayerSaveData"));
            Set(save, field, amount); File.WriteAllText(generation, JsonUtility.ToJson(save));
            Reject(() => Read()); AssertProductionUntouched(false);
        }
        [Test] public void PreTrainingDigestDoesNotAcceptChangedRecordedHash()
        {
            MakePreTrainingDeliveredFixture();
            var journal = JsonUtility.FromJson(File.ReadAllText(JournalPath), T("Save.PurchaseIsolationRecoveryJournal"));
            Set(journal, "DeliveredSaveHash", new string('0', 64)); WriteJournal(journal);
            Reject(() => Read()); AssertProductionUntouched(false);
        }
        [TestCase("TrainingFusionReceipts")] [TestCase("DailyChallenges")]
        public void PreTrainingDigestDoesNotIgnoreNewState(string field)
        {
            MakePreTrainingDeliveredFixture();
            string generation = Path.Combine(SavePath + ".history", "00000000000000000002.json");
            var save = JsonUtility.FromJson(File.ReadAllText(generation), T("Save.PlayerSaveData"));
            if (field == "TrainingFusionReceipts")
            {
                var list = (IList)Field(save, field);
                list.Add(Activator.CreateInstance(T("Save.TrainingFusionReceipt")));
            }
            else Set(Field(save, field), "Day", "2099-01-01");
            File.WriteAllText(generation, JsonUtility.ToJson(save));
            Reject(() => Read()); AssertProductionUntouched(false);
        }

        [Test] public void NoRecordMeansNoRouteNoConfirmationAndNoSaveCreation()
        {
            Assert.IsNull(Read()); Assert.IsFalse((bool)Call("Blocks", root));
            Assert.IsFalse((bool)Call("Matches", root, product, Transaction)); Assert.IsFalse(CanConfirm());
            Assert.IsFalse(Directory.Exists((string)Call("IsolatedDirectory", root)));
            AssertProductionUntouched();
        }
        [Test] public void BeginArchivesExactRequestAndCreatesOnlyFreshIsolatedAccount()
        {
            var journal = Begin(); var save = LoadSave();
            Assert.AreEqual("pending", Field(journal, "Phase"));
            Assert.AreEqual("environments/production/linked-accounts/" + new string('b', 32), Field(journal, "SourceDataDirectoryRelative"));
            Assert.AreEqual(raw, Field(journal, "OriginalRequestJson"));
            CollectionAssert.AreEqual(File.ReadAllBytes(pending), Convert.FromBase64String((string)Field(journal, "OriginalRequestBase64")));
            Assert.AreEqual(4, Field(Field(journal, "Request"), "Epoch"), "Archive must retain the production request unchanged.");
            Assert.AreEqual(PlayerId, Field(save, "PlayerId")); Assert.AreEqual(900, Field(save, "FreeGachaStones"));
            Assert.AreEqual(0, Field(save, "PaidGachaStones")); Assert.AreEqual(100, Field(save, "Gold"));
            Assert.AreEqual(0, ((IList)Field(save, "OwnedMonsters")).Count);
            Assert.AreEqual(0, ((IList)Field(save, "ProcessedIapTransactionIds")).Count);
            string token = (string)Field(journal, "SandboxToken");
            Assert.AreEqual(64, token.Length); Assert.IsTrue(token.All(c => "0123456789abcdef".Contains(c)));
            StringAssert.Contains(token, File.ReadAllText(IdentityPath));
            StringAssert.DoesNotContain("synthetic-production-credential", File.ReadAllText(JournalPath));
            Assert.IsFalse(CanConfirm()); AssertProductionUntouched();
        }
        [Test] public void EachReadIsDetachedAndCallerCannotPromotePhaseOrGrant()
        {
            var journal = Begin(); Set(journal, "Phase", "store_confirmed"); Set(journal, "SandboxToken", new string('c', 64));
            Set(Field(journal, "Request"), "Target", "another-product");
            Assert.AreEqual("pending", Field(Read(), "Phase")); Assert.IsFalse(CanConfirm());
            AssertProductionUntouched();
        }
        [TestCase("request")] [TestCase("transaction")] [TestCase("product")] [TestCase("player")]
        public void BeginRequiresExactApprovedScope(string wrong)
        {
            Reject(() => Call("Begin", root, source, wrong == "player" ? "invalid" : PlayerId,
                wrong == "request" ? "different-request" : RequestId, wrong == "transaction" ? "different-transaction" : Transaction,
                wrong == "product" ? "different-product" : product));
            Assert.IsFalse((bool)Call("Blocks", root)); AssertProductionUntouched();
        }
        [TestCase("Kind", "summon")] [TestCase("Receipt", "")] [TestCase("Count", 1)]
        [TestCase("Paid", true)] [TestCase("Epoch", -1)] [TestCase("RequestId", "short")]
        public void InvalidArchivedRequestCannotBegin(string field, object value)
        {
            var request = JsonUtility.FromJson(raw, T("Save.OnlineRequest")); Set(request, field, value);
            File.WriteAllText(pending, JsonUtility.ToJson(request));
            Reject(() => Begin()); Assert.IsFalse((bool)Call("Blocks", root));
        }
        [TestCase("RequestId")] [TestCase("TransactionId")]
        public void OversizedIdentifierCannotBeQuarantined(string field)
        {
            var request = JsonUtility.FromJson(raw, T("Save.OnlineRequest")); Set(request, field, new string('z', 129));
            File.WriteAllText(pending, JsonUtility.ToJson(request));
            Reject(() => Begin()); Assert.IsFalse((bool)Call("Blocks", root));
        }
        [Test] public void SameQuarantinedTransactionWithDifferentProductFailsClosedRatherThanRoutingNormally()
        {
            Begin(); Reject(() => Call("Matches", root, "other-product", Transaction));
            Assert.IsFalse((bool)Call("Matches", root, product, "other-transaction")); AssertProductionUntouched();
        }
        [Test] public void SourcePathIsProductionOnlyAndJournalPathIsPortableAcrossRoots()
        {
            Reject(() => Call("Begin", root, Path.Combine(root, "refund-sandbox"), PlayerId, RequestId, Transaction, product));
            Reject(() => Call("Begin", root, Path.Combine(root, "environments", "production", "..", "other"), PlayerId, RequestId, Transaction, product));
            Begin(); string movedRoot = root + "_moved";
            Directory.Move(root, movedRoot); root = movedRoot;
            source = Path.Combine(root, "environments", "production", "linked-accounts", new string('b', 32));
            pending = Path.Combine(source, "online-request.json");
            Assert.IsTrue((bool)Call("Matches", root, product, Transaction));
            ServerDelivered(); Call("PersistLocalDelivery", root); Call("ClearOriginalPending", root);
            Assert.IsFalse(File.Exists(pending)); AssertProductionUntouched(false);
        }
        [Test] public void OnlyOneOrderMayBeQuarantinedAndSecondBeginDoesNotReplaceArchive()
        {
            Begin(); byte[] before = File.ReadAllBytes(JournalPath);
            Reject(() => Begin());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(JournalPath)); AssertProductionUntouched();
        }
        [Test] public void CannotCommitClearOrConfirmBeforeVerifiedLocalDelivery()
        {
            Begin(); Reject(() => Call("PersistLocalDelivery", root)); Reject(() => Call("ClearOriginalPending", root));
            Reject(() => Call("MarkStoreConfirmed", root, product, Transaction));
            ServerDelivered(); Assert.AreEqual("server_delivered", Field(Read(), "Phase")); Assert.IsFalse(CanConfirm());
            Reject(() => Call("ClearOriginalPending", root)); AssertProductionUntouched();
        }
        [TestCase("RequestId", "other-request")] [TestCase("TransactionId", "other-transaction")]
        [TestCase("Target", "other-product")] [TestCase("Kind", "ad_reward")] [TestCase("Revision", 2L)]
        [TestCase("Free", 0)] [TestCase("Paid", 14999)] [TestCase("RefundDebt", 1)] [TestCase("GoldDelta", 1)]
        [TestCase("RelicId", "relic_safe_ember")] [TestCase("RelicAmount", 1)] [TestCase("TutorialPulls", 1)]
        [TestCase("ClaimDate", "synthetic-date")] [TestCase("UpgradeField", "HasAutoRepeatFloorUpgrade")]
        public void ServerProofMustExactlyMatchOneCatalogPurchaseWithNoOtherEffects(string field, object value)
        {
            Begin(); var op = Operation(); Set(op, field, value);
            Reject(() => Call("MarkServerDelivered", root, Endpoint, PlayerId, op));
            Assert.AreEqual("pending", Field(Read(), "Phase")); Assert.IsFalse(CanConfirm()); AssertProductionUntouched();
        }
        [Test] public void ServerProofRejectsProductionEndpointOtherAccountAndMonsterGrants()
        {
            Begin(); Reject(() => Call("MarkServerDelivered", root, "https://api.nasus-games.com", PlayerId, Operation()));
            Reject(() => Call("MarkServerDelivered", root, Endpoint, new string('c', 32), Operation()));
            var op = Operation(); Set(op, "Monsters", Array.CreateInstance(T("Save.OwnedMonsterData"), 1));
            Reject(() => Call("MarkServerDelivered", root, Endpoint, PlayerId, op)); Assert.IsFalse(CanConfirm());
        }
        [Test] public void RepeatedServerProofAndLocalPersistenceNeverDuplicateDelivery()
        {
            LocalDelivered(); var journal = Read(); byte[] proof = File.ReadAllBytes(JournalPath);
            ServerDelivered(); Call("PersistLocalDelivery", root);
            CollectionAssert.AreEqual(proof, File.ReadAllBytes(JournalPath));
            Assert.AreEqual("local_delivered", Field(journal, "Phase")); Assert.IsTrue(CanConfirm());
            var save = LoadSave(); Assert.AreEqual(15000, Field(save, "PaidGachaStones"));
            Assert.AreEqual(1L, Field(save, "EconomyRevision")); Assert.AreEqual(2L, Field(save, "SaveRevision"));
            CollectionAssert.AreEqual(new[] { Transaction }, (IList)Field(save, "ProcessedIapTransactionIds"));
            Assert.IsTrue((bool)Field(save, "HasRemovedAds")); AssertProductionUntouched();
        }
        [Test] public void RestartAfterGenerationCommitBeforeJournalPromotionReconcilesWithoutGrantingAgain()
        {
            Begin(); ServerDelivered(); string serverJournal = File.ReadAllText(JournalPath);
            Call("PersistLocalDelivery", root);
            File.WriteAllText(JournalPath, serverJournal);
            Assert.AreEqual("server_delivered", Field(Read(), "Phase")); Assert.IsFalse(CanConfirm());
            Call("PersistLocalDelivery", root); Assert.IsTrue(CanConfirm());
            Assert.AreEqual(2L, Field(LoadSave(), "SaveRevision"));
            Assert.AreEqual(2, Directory.GetFiles(SavePath + ".history", "*.json").Length);
            AssertProductionUntouched();
        }
        [Test] public void CleanupRequiresExactOriginalBytesAndPreservesDifferentPendingRequest()
        {
            LocalDelivered(); string replacement = raw + " "; File.WriteAllText(pending, replacement);
            Reject(() => Call("ClearOriginalPending", root)); Assert.AreEqual(replacement, File.ReadAllText(pending));
            Assert.IsFalse((bool)Field(Read(), "SourcePendingCleared"));
            File.WriteAllText(pending, raw); Call("ClearOriginalPending", root); Assert.IsFalse(File.Exists(pending));
            Call("ClearOriginalPending", root); Assert.IsTrue((bool)Field(Read(), "SourcePendingCleared"));
            AssertProductionUntouched(false);
        }
        [Test] public void ConfirmationAndRoutingTombstoneSurviveRestartAndOtherActiveAccount()
        {
            LocalDelivered(); Assert.IsFalse(CanConfirm(product, "other-transaction")); Assert.IsFalse(CanConfirm("other-product"));
            Reject(() => Call("MarkStoreConfirmed", root, product, Transaction));
            Call("ClearOriginalPending", root);
            Reject(() => Call("MarkStoreConfirmed", root, product, "other-transaction"));
            Call("MarkStoreConfirmed", root, product, Transaction); Call("MarkStoreConfirmed", root, product, Transaction);
            File.WriteAllText(Path.Combine(root, "environments", "production", "active-account.json"), "synthetic-other-account");
            Assert.AreEqual("store_confirmed", Field(Read(), "Phase")); Assert.IsTrue(CanConfirm());
            Assert.IsTrue((bool)Call("Matches", root, product, Transaction)); Assert.IsTrue((bool)Call("Blocks", root));
            Assert.AreEqual(raw, Field(Read(), "OriginalRequestJson")); AssertProductionUntouched(false);
        }
        [TestCase(false)] [TestCase(true)]
        public void InterruptedJournalWriteNeverFallsBackOrResets(bool existing)
        {
            if (existing) Begin(); else Directory.CreateDirectory(Path.GetDirectoryName(JournalPath));
            File.WriteAllText(JournalPath + ".tmp", "synthetic-interrupted-record");
            Assert.IsTrue((bool)Call("Blocks", root)); Reject(() => Read()); Reject(() => Begin()); Reject(() => CanConfirm());
            Assert.AreEqual("synthetic-interrupted-record", File.ReadAllText(JournalPath + ".tmp")); AssertProductionUntouched();
        }
        [TestCase("{}")][TestCase("invalid-json")]
        public void CorruptJournalIsRetainedAndNeverRoutesNormally(string invalid)
        {
            Begin(); File.WriteAllText(JournalPath, invalid);
            Reject(() => Read()); Reject(() => Call("Matches", root, product, Transaction)); Reject(() => CanConfirm());
            Assert.AreEqual(invalid, File.ReadAllText(JournalPath)); AssertProductionUntouched();
        }
        [TestCase("OriginalRequestHash")] [TestCase("OriginalRequestJson")] [TestCase("SourceDataDirectoryRelative")]
        [TestCase("SandboxToken")] [TestCase("SandboxEndpoint")] [TestCase("DeliveredSaveHash")]
        public void ModifiedJournalCannotAuthorizeConfirmation(string field)
        {
            LocalDelivered(); var journal = Read(); Set(journal, field, "invalid"); WriteJournal(journal);
            Reject(() => CanConfirm()); AssertProductionUntouched();
        }
        [TestCase("identity_missing")] [TestCase("identity_tmp")] [TestCase("save_tmp")]
        [TestCase("generation_tmp")] [TestCase("generation_corrupt")] [TestCase("generation_missing")]
        public void MissingCorruptOrInterruptedIsolatedProofNeverConfirms(string fault)
        {
            LocalDelivered(); string generation = Path.Combine(SavePath + ".history", 2L.ToString("D20") + ".json");
            switch (fault)
            {
                case "identity_missing": File.Delete(IdentityPath); break;
                case "identity_tmp": File.WriteAllText(IdentityPath + ".tmp", "interrupted"); break;
                case "save_tmp": File.WriteAllText(SavePath + ".tmp", "interrupted"); break;
                case "generation_tmp": File.WriteAllText(generation + ".tmp", "interrupted"); break;
                case "generation_corrupt": File.WriteAllText(generation, "invalid"); break;
                case "generation_missing": File.Delete(generation); break;
            }
            Reject(() => CanConfirm()); Reject(() => Call("ClearOriginalPending", root)); AssertProductionUntouched();
        }
        [Test] public void MissingHistoryNeverCreatesAnUnrelatedDefaultSaveDuringRead()
        {
            Begin(); Directory.Delete(SavePath + ".history", true); File.Delete(SavePath);
            Reject(() => Read()); Assert.IsFalse(File.Exists(SavePath)); Assert.IsFalse(Directory.Exists(SavePath + ".history"));
            AssertProductionUntouched();
        }
        [Test] public void DanglingRecoveryDirectoryIsAQuarantineBlockerNotANewDestination()
        {
            string recovery = Path.GetDirectoryName(JournalPath), target = Path.Combine(root, "test-owned-missing-target");
            SymbolicLink(recovery, target);
            Assert.IsTrue((bool)Call("Blocks", root)); Reject(() => Begin()); Reject(() => Read());
            Assert.IsFalse(Directory.Exists(target)); AssertProductionUntouched();
        }
        [TestCase("journal_tmp")] [TestCase("identity_tmp")] [TestCase("save_tmp")]
        [TestCase("save_backup")] [TestCase("generation_tmp")]
        public void DanglingLinksCannotBeFollowedByJournalOrSaveWrites(string fault)
        {
            Begin(); string target = Path.Combine(root, "test-owned-missing-target"), link;
            switch (fault)
            {
                case "journal_tmp": link = JournalPath + ".tmp"; break;
                case "identity_tmp": link = IdentityPath + ".tmp"; break;
                case "save_tmp": link = SavePath + ".tmp"; break;
                case "save_backup": link = SavePath + ".bak"; break;
                default: link = Path.Combine(SavePath + ".history", 2L.ToString("D20") + ".json.tmp"); break;
            }
            SymbolicLink(link, target);
            Reject(() => ServerDelivered()); Reject(() => Call("PersistLocalDelivery", root));
            Assert.IsFalse(File.Exists(target)); AssertProductionUntouched();
        }
        [Test] public void ExistingTempLinkNeverOverwritesItsTarget()
        {
            Begin(); string target = Path.Combine(root, "test-owned-private-target");
            File.WriteAllText(target, "synthetic-target-must-not-change");
            SymbolicLink(JournalPath + ".tmp", target);
            Reject(() => ServerDelivered());
            Assert.AreEqual("synthetic-target-must-not-change", File.ReadAllText(target)); AssertProductionUntouched();
        }
        [Test] public void LinkedNextGenerationCannotRedirectDeliveryCommit()
        {
            Begin(); ServerDelivered(); string target = Path.Combine(root, "test-owned-missing-target");
            SymbolicLink(Path.Combine(SavePath + ".history", 2L.ToString("D20") + ".json"), target);
            Reject(() => Call("PersistLocalDelivery", root));
            Assert.IsFalse(File.Exists(target)); AssertProductionUntouched();
        }
        [Test] public void LegacyArtifactIsNotMistakenForDurableGenerationAndCannotCreateDefault()
        {
            Begin(); Directory.Delete(SavePath + ".history", true); File.Delete(SavePath);
            Directory.CreateDirectory(SavePath + ".history");
            File.WriteAllText(Path.Combine(SavePath + ".history", "legacy-original.json"), "synthetic-orphan");
            Reject(() => Read()); Assert.IsFalse(File.Exists(SavePath));
            CollectionAssert.AreEqual(new[] { "legacy-original.json" }, Directory.GetFiles(SavePath + ".history").Select(Path.GetFileName).ToArray());
            AssertProductionUntouched();
        }
        [Test] public void ModifiedDurableSaveCannotAuthorizeEvenMatchingTransactionAndWallet()
        {
            LocalDelivered(); var save = LoadSave(); Set(save, "Gold", 123);
            string generation = Path.Combine(SavePath + ".history", 2L.ToString("D20") + ".json");
            File.WriteAllText(generation, JsonUtility.ToJson(save));
            Reject(() => CanConfirm()); AssertProductionUntouched();
        }
        [Test] public void RuntimeRootHonorsOnlyTheEditorOverrideWithoutChangingGameplayContext()
        {
            var overrideProperty = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object before = overrideProperty.GetValue(null);
            try
            {
                overrideProperty.SetValue(null, root);
                Assert.AreEqual(root, T("Save.PurchaseIsolationRecoveryStorage").GetProperty("RuntimeRoot").GetValue(null));
                overrideProperty.SetValue(null, null);
                Assert.AreEqual(Application.persistentDataPath, T("Save.PurchaseIsolationRecoveryStorage").GetProperty("RuntimeRoot").GetValue(null));
            }
            finally { overrideProperty.SetValue(null, before); }
        }
    }
}
