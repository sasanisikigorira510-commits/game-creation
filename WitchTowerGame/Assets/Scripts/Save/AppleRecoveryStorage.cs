using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace WitchTower.Save
{
    [Serializable] public sealed class AppleRecoveryBundle
    {
        public string Status, PlayerId, PreviewToken;
        public int Epoch, Free, Paid;
        public long EconomyRevision;
        public PlayerSaveData Snapshot;
        public OnlineOperation[] Operations;
    }
    [Serializable] public sealed class AppleRecoveryJournal
    {
        public string Phase, Token, ChallengeId, SlotId;
        public AppleRecoveryBundle Bundle;
    }

    // No live save is overwritten. The only account switch is a durable pointer
    // rename after the server commit and both staged files have been verified.
    public static class AppleRecoveryStorage
    {
        [Serializable] private sealed class Pointer { public string SlotId, PlayerId; }
        [Serializable] private sealed class Identity { public string PlayerId, Token; public bool Legacy; }
        public static string JournalPath(string root) => Path.Combine(root, "apple-recovery.json");
        public static bool Blocks(string root) => File.Exists(JournalPath(root)) || File.Exists(JournalPath(root) + ".tmp");
        private static string PointerPath(string root) => Path.Combine(root, "active-account.json");
        private static bool Hex(string value, int length) => value != null && value.Length == length && value.All(c => "0123456789abcdef".Contains(c));
        private static string Slot(string root, string id)
        {
            if (!Hex(id, 32)) throw new InvalidDataException("Invalid account slot.");
            return Path.Combine(root, "linked-accounts", id);
        }
        public static string NewCredential()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        private static void Write(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            OnlinePlayerData.WriteAtomic(path, JsonUtility.ToJson(value));
        }
        public static AppleRecoveryJournal Pending(string root)
        {
            // An interrupted atomic write is not an empty/absent operation.
            // Preserve both records for review instead of guessing the phase.
            if (File.Exists(JournalPath(root) + ".tmp"))
                throw new InvalidDataException("Interrupted recovery record needs review.");
            if (!File.Exists(JournalPath(root))) return null;
            var journal = JsonUtility.FromJson<AppleRecoveryJournal>(File.ReadAllText(JournalPath(root)));
            if (journal == null || !Hex(journal.Token, 64) || !Hex(journal.SlotId, 32) ||
                !(journal.Phase == "started" || journal.Phase == "preview" || journal.Phase == "requested" || journal.Phase == "committed"))
                throw new InvalidDataException("Recovery journal needs support review.");
            if (journal.Phase == "started")
            {
                // JsonUtility materializes null nested classes as empty objects
                // after a restart. No preview exists until Stage has succeeded.
                journal.Bundle = null;
            }
            else
            {
                if (!Hex(journal.ChallengeId, 64) || journal.Bundle?.Status != "preview")
                    throw new InvalidDataException("Recovery preview is missing.");
                Replay(journal.Bundle);
            }
            return journal;
        }
        public static AppleRecoveryJournal Begin(string root)
        {
            if (AccountDeletionStorage.Blocks(root)) throw new InvalidOperationException("Resolve account deletion first.");
            if (Blocks(root)) throw new InvalidOperationException("Resume pending recovery first.");
            var journal = new AppleRecoveryJournal { Phase = "started", Token = NewCredential(), SlotId = Guid.NewGuid().ToString("N") };
            Write(JournalPath(root), journal);
            return journal;
        }
        public static void SetChallenge(string root, AppleRecoveryJournal journal, string challenge)
        {
            if (journal.Phase != "started" || !Hex(challenge, 64)) throw new InvalidDataException("Invalid challenge.");
            journal.ChallengeId = challenge;
            Write(JournalPath(root), journal);
        }
        public static PlayerSaveData Replay(AppleRecoveryBundle bundle)
        {
            if (bundle == null || !Hex(bundle.PlayerId, 32) || !Hex(bundle.PreviewToken, 64) || bundle.Epoch < 1 ||
                bundle.Snapshot == null || bundle.Snapshot.PlayerId != bundle.PlayerId || bundle.Snapshot.RecoveryEpoch != bundle.Epoch ||
                bundle.Snapshot.EconomyRevision < 0 || bundle.Snapshot.EconomyRevision > bundle.EconomyRevision ||
                bundle.Operations == null || bundle.Operations.Length > 1000 ||
                bundle.EconomyRevision - bundle.Snapshot.EconomyRevision != bundle.Operations.Length)
                throw new InvalidDataException("Incomplete recovery data.");
            var staged = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(bundle.Snapshot));
            if (!PlayerSaveDataMigration.TryMigrate(staged, out _)) throw new InvalidDataException("Unsupported recovery save.");
            foreach (var operation in bundle.Operations)
            {
                if (operation == null || operation.Revision != staged.EconomyRevision + 1)
                    throw new InvalidDataException("Non-contiguous recovery history.");
                // The existing applier assigns acquired-order on monster objects.
                // Keep the server's immutable preview unchanged for commit comparison.
                var copy = JsonUtility.FromJson<OnlineOperation>(JsonUtility.ToJson(operation));
                staged = OnlineGrantApplier.Stage(staged, copy);
            }
            if (staged.EconomyRevision != bundle.EconomyRevision || staged.FreeGachaStones != bundle.Free ||
                staged.PaidGachaStones != bundle.Paid || bundle.Free < 0 || bundle.Paid < 0)
                throw new InvalidDataException("Recovery wallet mismatch.");
            return staged;
        }
        public static void Stage(string root, AppleRecoveryJournal journal, AppleRecoveryBundle bundle)
        {
            if (journal.Phase != "started" || !Hex(journal.ChallengeId, 64) || bundle.Status != "preview")
                throw new InvalidOperationException("Preview required.");
            var staged = Replay(bundle);
            string directory = Slot(root, journal.SlotId);
            // Refuse to reuse partial output; a new sign-in creates a new slot.
            if (Directory.Exists(directory)) throw new IOException("Recovery slot already exists.");
            Directory.CreateDirectory(directory);
            if (!new PlayerSaveRepository(Path.Combine(directory, "save.json")).TryCommit(staged, null, "apple_recovery_staged", out _))
                throw new IOException("Cannot persist recovery save.");
            Write(Path.Combine(directory, "online-identity.json"), new Identity { PlayerId = bundle.PlayerId, Token = journal.Token });
            journal.Bundle = bundle;
            journal.Phase = "preview";
            Write(JournalPath(root), journal);
        }
        private static PlayerSaveData ValidateSlot(string root, AppleRecoveryJournal journal)
        {
            string directory = Slot(root, journal.SlotId);
            if (!File.Exists(Path.Combine(directory, "save.json"))) throw new IOException("Recovery save missing.");
            if (!new PlayerSaveRepository(Path.Combine(directory, "save.json")).TryLoad(out var save, out _, out _))
                throw new IOException("Recovery save is unreadable.");
            var identity = JsonUtility.FromJson<Identity>(File.ReadAllText(Path.Combine(directory, "online-identity.json")));
            var b = journal.Bundle;
            if (b == null || identity == null || identity.Token != journal.Token || identity.PlayerId != b.PlayerId ||
                save.PlayerId != b.PlayerId || save.RecoveryEpoch != b.Epoch || save.EconomyRevision != b.EconomyRevision ||
                save.FreeGachaStones != b.Free || save.PaidGachaStones != b.Paid)
                throw new InvalidDataException("Staged recovery does not match confirmation.");
            return save;
        }
        public static void MarkRequested(string root, AppleRecoveryJournal journal)
        {
            if (journal.Phase != "preview") throw new InvalidOperationException("Preview required.");
            ValidateSlot(root, journal);
            journal.Phase = "requested"; // Persist BEFORE a request can revoke the old credential.
            Write(JournalPath(root), journal);
        }
        public static void MarkCommitted(string root, AppleRecoveryJournal journal, AppleRecoveryBundle confirmed)
        {
            if (journal.Phase != "requested" || confirmed == null || confirmed.Status != "committed")
                throw new InvalidDataException("Server confirmation required.");
            var expected = JsonUtility.FromJson<AppleRecoveryBundle>(JsonUtility.ToJson(journal.Bundle));
            expected.Status = "committed";
            if (JsonUtility.ToJson(expected) != JsonUtility.ToJson(confirmed))
                throw new InvalidDataException("Server recovery changed.");
            ValidateSlot(root, journal);
            journal.Phase = "committed";
            Write(JournalPath(root), journal);
        }
        public static void Activate(string root, AppleRecoveryJournal journal)
        {
            if (AccountDeletionStorage.Blocks(root)) throw new InvalidOperationException("Resolve account deletion first.");
            if (journal.Phase != "committed") throw new InvalidOperationException("Unconfirmed recovery cannot activate.");
            ValidateSlot(root, journal);
            Write(PointerPath(root), new Pointer { SlotId = journal.SlotId, PlayerId = journal.Bundle.PlayerId });
            // If cleanup fails, reboot safely repeats activation before gameplay.
            File.Delete(JournalPath(root));
        }
        public static void Cancel(string root, AppleRecoveryJournal journal)
        {
            if (journal.Phase != "started" && journal.Phase != "preview")
                throw new InvalidOperationException("A submitted transfer must be reconciled, not discarded.");
            File.Delete(JournalPath(root)); // Original and staged saves are retained.
        }
        public static string ActiveDirectory(string root)
        {
            if (!File.Exists(PointerPath(root))) return root;
            var pointer = JsonUtility.FromJson<Pointer>(File.ReadAllText(PointerPath(root)));
            if (pointer == null || !Hex(pointer.PlayerId, 32)) throw new InvalidDataException("Invalid account pointer.");
            string directory = Slot(root, pointer.SlotId);
            var identity = JsonUtility.FromJson<Identity>(File.ReadAllText(Path.Combine(directory, "online-identity.json")));
            if (identity == null || identity.PlayerId != pointer.PlayerId || !Hex(identity.Token, 64) ||
                !File.Exists(Path.Combine(directory, "save.json")))
                throw new InvalidDataException("Active account is incomplete; no guest fallback allowed.");
            return directory;
        }
        public static void ValidateActivePlayer(string root, PlayerSaveData save)
        {
            if (!File.Exists(PointerPath(root))) return;
            var pointer = JsonUtility.FromJson<Pointer>(File.ReadAllText(PointerPath(root)));
            if (save == null || pointer == null || save.PlayerId != pointer.PlayerId)
                throw new InvalidDataException("Active save identity mismatch.");
        }
        internal static void CreateGuestSlot(string root)
        {
            string id = Guid.NewGuid().ToString("N"), directory = Slot(root, id);
            var save = PlayerSaveData.CreateDefault();
            save.PlayerId = Guid.NewGuid().ToString("N");
            if (!new PlayerSaveRepository(Path.Combine(directory, "save.json")).TryCommit(save, null, "new_player_after_deletion", out _))
                throw new IOException("Cannot persist new guest.");
            Write(Path.Combine(directory, "online-identity.json"), new Identity { PlayerId = save.PlayerId, Token = NewCredential() });
            Write(PointerPath(root), new Pointer { PlayerId = save.PlayerId, SlotId = id });
        }
    }
}
