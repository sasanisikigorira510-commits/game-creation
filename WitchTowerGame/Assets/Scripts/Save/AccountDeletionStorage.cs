using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace WitchTower.Save
{
    [Serializable] public sealed class AccountDeletionFile { public string Path, Hash; }
    [Serializable] public sealed class AccountDeletionJournal
    {
        public string Phase, PlayerId, Token, ConfirmationToken, Endpoint;
        public AccountDeletionFile[] Files;
        public bool RevocationPending, ManualRevocationRequired;
    }
    [Serializable] public sealed class AccountDeletionResult
    {
        public string Status, PlayerId, ConfirmationToken;
        public int Free, Paid;
        public bool RevocationPending, ManualRevocationRequired;
    }

    // An exact, hashed file manifest is persisted BEFORE any destructive request.
    // Never recursively delete an account directory or silently fall back to a
    // retained guest after removing the active account pointer.
    public static class AccountDeletionStorage
    {
        [Serializable] private sealed class Owner { public string PlayerId, Token; }
        public static string JournalPath(string root) => Path.Combine(root, "account-deletion.json");
        public static bool Blocks(string root) => File.Exists(JournalPath(root)) || File.Exists(JournalPath(root) + ".tmp");
        private static bool Hex(string s, int length) => s != null && s.Length == length && s.All(c => "0123456789abcdef".Contains(c));
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void NoLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked paths cannot be erased.");
        }
        private static bool KnownFile(string name) => name == "save.json" || name == "save.json.bak" || name == "save.json.tmp" ||
            (name.StartsWith("save.json.corrupt-", StringComparison.Ordinal) && name.Length > 18 && name.Substring(18).All(c => char.IsDigit(c) || c == '-')) ||
            name == "online-identity.json" || name == "online-identity.json.tmp" || name == "online-request.json" || name == "online-request.json.tmp";
        private static bool HistoryFile(string name)
        {
            if (name == "legacy-original.json") return true;
            return name.Length >= 25 && name.Take(20).All(c => c >= '0' && c <= '9') &&
                (name.Substring(20) == ".json" || name.Substring(20) == ".json.bak" || name.Substring(20) == ".json.tmp");
        }
        private static bool Allowed(string relative)
        {
            if (relative == null || relative.Contains('\\') || Path.IsPathRooted(relative)) return false;
            var parts = relative.Split('/');
            if (parts.Length == 1) return KnownFile(parts[0]) || parts[0] == "active-account.json" || parts[0] == "active-account.json.tmp";
            int start = 0;
            if (parts[0] == "linked-accounts" && parts.Length >= 3 && Hex(parts[1], 32)) start = 2;
            return (start == 2 && parts.Length == 3 && KnownFile(parts[2])) ||
                (parts.Length == start + 2 && parts[start] == "save.json.history" && HistoryFile(parts[start + 1]));
        }
        private static string ExactPath(string root, string relative)
        {
            if (!Allowed(relative)) throw new InvalidDataException("Invalid deletion target.");
            string current = Path.GetFullPath(root);
            NoLink(current);
            foreach (string part in relative.Split('/'))
            {
                current = Path.Combine(current, part);
                if (File.Exists(current) || Directory.Exists(current)) NoLink(current);
            }
            return current;
        }
        private static void Endpoint(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                (uri.Scheme != "https" && !(Application.isEditor && uri.IsLoopback && uri.Scheme == "http")))
                throw new InvalidDataException("Invalid deletion endpoint.");
        }
        private static void Write(string root, AccountDeletionJournal journal)
        {
            NoLink(root);
            foreach (string path in new[] { JournalPath(root), JournalPath(root) + ".tmp" })
                if (File.Exists(path)) NoLink(path);
            OnlinePlayerData.WriteAtomic(JournalPath(root), JsonUtility.ToJson(journal));
        }
        public static AccountDeletionJournal Pending(string root)
        {
            if (!Blocks(root)) return null;
            NoLink(root);
            NoLink(JournalPath(root)); // A temp-only interrupted write fails closed.
            var journal = JsonUtility.FromJson<AccountDeletionJournal>(File.ReadAllText(JournalPath(root)));
            if (journal == null || journal.Files == null) throw new InvalidDataException("Deletion record missing.");
            if (journal.Phase == "completed")
            {
                if (journal.Files.Length != 0 || !string.IsNullOrEmpty(journal.Token) || !string.IsNullOrEmpty(journal.PlayerId) ||
                    !string.IsNullOrEmpty(journal.ConfirmationToken) || !string.IsNullOrEmpty(journal.Endpoint))
                    throw new InvalidDataException("Incomplete cleanup record.");
                return journal;
            }
            if ((journal.Phase != "requested" && journal.Phase != "confirmed") || !Hex(journal.PlayerId, 32) ||
                !Hex(journal.Token, 64) || !Hex(journal.ConfirmationToken, 64) || journal.Files.Length == 0)
                throw new InvalidDataException("Invalid deletion record.");
            Endpoint(journal.Endpoint);
            if (journal.Files.Any(f => f == null || !Allowed(f.Path) || !Hex(f.Hash, 64)) ||
                journal.Files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() != journal.Files.Length)
                throw new InvalidDataException("Invalid deletion manifest.");
            return journal;
        }
        private static string OwnerId(string path)
        {
            try { return JsonUtility.FromJson<Owner>(File.ReadAllText(path))?.PlayerId; }
            catch (ArgumentException) { return null; } // Corrupt save belongs to its validated container.
        }
        private static List<AccountDeletionFile> Manifest(string root, string player)
        {
            var result = new List<AccountDeletionFile>();
            var directories = new List<string> { root };
            string linked = Path.Combine(root, "linked-accounts");
            NoLink(root);
            if (Directory.Exists(linked))
            {
                NoLink(linked);
                directories.AddRange(Directory.GetDirectories(linked).Where(d => Hex(Path.GetFileName(d), 32)));
            }
            foreach (string directory in directories)
            {
                NoLink(directory);
                var candidates = Directory.GetFiles(directory).Where(p => KnownFile(Path.GetFileName(p))).ToList();
                string history = Path.Combine(directory, "save.json.history");
                if (Directory.Exists(history))
                {
                    NoLink(history);
                    candidates.AddRange(Directory.GetFiles(history).Where(p => HistoryFile(Path.GetFileName(p))));
                }
                foreach (string file in candidates) NoLink(file);
                var owners = candidates.Where(p => !Path.GetFileName(p).StartsWith("online-request", StringComparison.Ordinal))
                    .Select(OwnerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
                if (!owners.Contains(player)) continue;
                if (owners.Any(id => id != player)) throw new InvalidDataException("Mixed account files require support review.");
                foreach (string file in candidates)
                {
                    string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                    result.Add(new AccountDeletionFile { Path = relative, Hash = Hash(File.ReadAllBytes(ExactPath(root, relative))) });
                }
            }
            foreach (string relative in new[] { "active-account.json", "active-account.json.tmp" })
            {
                string path = ExactPath(root, relative);
                if (File.Exists(path))
                {
                    if (OwnerId(path) != player) throw new InvalidDataException("Active account changed.");
                    result.Add(new AccountDeletionFile { Path = relative, Hash = Hash(File.ReadAllBytes(path)) });
                }
            }
            return result;
        }
        public static AccountDeletionJournal Begin(string root, string player, string token, string confirmation, string endpoint)
        {
            if (Blocks(root) || AppleRecoveryStorage.Blocks(root)) throw new InvalidOperationException("Resolve the previous account operation first.");
            if (!Hex(player, 32) || !Hex(token, 64) || !Hex(confirmation, 64)) throw new InvalidDataException("Invalid deletion identity.");
            Endpoint(endpoint);
            string active = AppleRecoveryStorage.ActiveDirectory(root);
            string identityPath = Path.Combine(active, "online-identity.json");
            NoLink(identityPath);
            var identity = JsonUtility.FromJson<Owner>(File.ReadAllText(identityPath));
            if (identity?.PlayerId != player || identity.Token != token) throw new InvalidDataException("Deletion credential changed.");
            var journal = new AccountDeletionJournal { Phase = "requested", PlayerId = player, Token = token,
                ConfirmationToken = confirmation, Endpoint = endpoint, Files = Manifest(root, player).ToArray() };
            if (journal.Files.Length == 0) throw new InvalidDataException("No owned account files.");
            Write(root, journal);
            return Pending(root);
        }
        public static void Confirm(string root, AccountDeletionResult result)
        {
            var journal = Pending(root);
            if (journal == null || journal.Phase != "requested" || result?.Status != "deleted" || result.PlayerId != journal.PlayerId)
                throw new InvalidDataException("Server deletion confirmation required.");
            journal.Phase = "confirmed";
            journal.RevocationPending = result.RevocationPending;
            journal.ManualRevocationRequired = result.ManualRevocationRequired;
            Write(root, journal);
        }
        public static void Erase(string root)
        {
            var journal = Pending(root);
            if (journal?.Phase != "confirmed") throw new InvalidOperationException("Confirmed deletion required.");
            var originalPaths = new HashSet<string>(journal.Files.Select(f => f.Path), StringComparer.Ordinal);
            foreach (var current in Manifest(root, journal.PlayerId))
                if (!originalPaths.Contains(current.Path)) throw new IOException("New account files appeared; cleanup paused.");
            // Check ALL surviving files before deleting any. Missing files mean a
            // previous cleanup already removed them. Changed files are preserved.
            foreach (var file in journal.Files)
            {
                string path = ExactPath(root, file.Path);
                if (Directory.Exists(path) || (File.Exists(path) && Hash(File.ReadAllBytes(path)) != file.Hash))
                    throw new IOException("Deletion target changed; cleanup paused.");
                if (File.Exists(path))
                {
                    string owner = OwnerId(path);
                    if (!string.IsNullOrEmpty(owner) && owner != journal.PlayerId)
                        throw new IOException("Deletion target belongs to another player.");
                }
            }
            foreach (var file in journal.Files) File.Delete(ExactPath(root, file.Path));
            // Keep the confirmed journal until caller has cleared cosmetic prefs.
        }
        public static void Finish(string root)
        {
            var journal = Pending(root);
            if (journal?.Phase != "confirmed" || journal.Files.Any(f => File.Exists(ExactPath(root, f.Path))))
                throw new InvalidOperationException("Local cleanup is incomplete.");
            if (Manifest(root, journal.PlayerId).Count != 0) throw new IOException("Account files still exist.");
            Write(root, new AccountDeletionJournal { Phase = "completed", Files = Array.Empty<AccountDeletionFile>(),
                RevocationPending = journal.RevocationPending, ManualRevocationRequired = journal.ManualRevocationRequired });
        }
        public static void Cancel(string root, AccountDeletionResult result)
        {
            var journal = Pending(root);
            if (journal?.Phase != "requested" || result?.Status != "cancelled" || result.PlayerId != journal.PlayerId)
                throw new InvalidOperationException("Server cancellation required.");
            File.Delete(JournalPath(root) + ".tmp");
            File.Delete(JournalPath(root));
        }
        public static void StartNewGuest(string root)
        {
            if (Pending(root)?.Phase != "completed") throw new InvalidOperationException("Finish deletion first.");
            string linked = Path.Combine(root, "linked-accounts");
            if (Directory.Exists(linked)) NoLink(linked);
            foreach (string name in new[] { "active-account.json", "active-account.json.tmp" }) ExactPath(root, name);
            AppleRecoveryStorage.CreateGuestSlot(root);
            File.Delete(JournalPath(root) + ".tmp");
            File.Delete(JournalPath(root)); // Pointer and new identity are durable first.
        }
    }
}
