using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WitchTower.Save
{
    [Serializable]
    public sealed class PurchaseNamespaceDescriptor
    {
        public int Version;
        public string Environment;
        public string RealmId;
        public string BundleId;
    }

    [Serializable]
    public sealed class PurchaseNamespaceDeploymentPin
    {
        public int Version;
        public string Environment;
        public string RealmId;
        public string Endpoint;
    }

    // Pure planning only: no startup hook, file access, network, account creation
    // or checkout authorization. Matching claims do not prove server readiness,
    // filesystem ownership or the environment of a future IAP transaction.
    public sealed class PurchaseSessionStoragePlan
    {
        public const string Bundle = "com.nasus.dungeonmonsterroguelike";
        public string Environment { get; }
        public string RealmId { get; }
        public string Endpoint { get; }
        public string RootDirectory { get; }

        private PurchaseSessionStoragePlan(string environment, string realmId, string endpoint, string root)
        {
            Environment = environment;
            RealmId = realmId;
            Endpoint = endpoint;
            RootDirectory = root;
        }

        public static bool TryCreateCandidate(string persistentRoot, string verifiedAppEnvironment,
            PurchaseNamespaceDeploymentPin pin, PurchaseNamespaceDescriptor declaration,
            string responseEndpoint, out PurchaseSessionStoragePlan candidate)
        {
            candidate = null;
            if (pin == null || declaration == null) return false;
            // Capture mutable transport fields once. Never validate one value
            // and then publish a later read of that field into the candidate.
            int pinVersion = pin.Version, declarationVersion = declaration.Version;
            string pinEnvironment = pin.Environment, declarationEnvironment = declaration.Environment;
            string pinRealm = pin.RealmId, declarationRealm = declaration.RealmId;
            string pinnedEndpoint = pin.Endpoint, declaredBundle = declaration.BundleId;
            if ((verifiedAppEnvironment != "Production" && verifiedAppEnvironment != "Sandbox") ||
                pinVersion != 1 || declarationVersion != 1 ||
                pinEnvironment != verifiedAppEnvironment || declarationEnvironment != verifiedAppEnvironment ||
                declaredBundle != Bundle || pinRealm != declarationRealm ||
                !ValidRealmId(pinRealm) || !CanonicalEndpoint(pinnedEndpoint) || responseEndpoint != pinnedEndpoint)
                return false;
            try
            {
                // Lexical validation only. Realpath/symlink/ownership checks are
                // still mandatory before the future storage bootstrap uses it.
                if (string.IsNullOrEmpty(persistentRoot) || !Path.IsPathRooted(persistentRoot) ||
                    persistentRoot.IndexOf('\\') >= 0 || Path.GetFullPath(persistentRoot) != persistentRoot ||
                    persistentRoot != persistentRoot.TrimEnd(Path.DirectorySeparatorChar) ||
                    Path.GetPathRoot(persistentRoot) == persistentRoot)
                    return false;
                string root = verifiedAppEnvironment == "Production"
                    ? Path.Combine(persistentRoot, "environments", "production")
                    : Path.Combine(persistentRoot, "environments", "sandbox", pinRealm);
                // Production's existing root is preserved, not copied/migrated.
                // Sandbox is never the production/legacy QA namespace.
                candidate = new PurchaseSessionStoragePlan(verifiedAppEnvironment, pinRealm, pinnedEndpoint, root);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        private static bool ValidRealmId(string value) => value != null && Regex.IsMatch(value, "\\A[a-f0-9]{32}\\z");

        private static bool CanonicalEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                string.IsNullOrEmpty(uri.DnsSafeHost) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
            string canonical = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
            return value == canonical && uri.AbsolutePath.IndexOf('%') < 0;
        }

        public static bool AreIsolatedCandidates(PurchaseSessionStoragePlan production, PurchaseSessionStoragePlan sandbox)
        {
            // A lexical plan comparison only: physical aliases, common server
            // ledgers/keys, and a durable binding of an existing production root
            // must be verified separately before any future activation.
            if (production == null || sandbox == null || production.Environment != "Production" ||
                sandbox.Environment != "Sandbox" || production.RealmId == sandbox.RealmId ||
                production.Endpoint == sandbox.Endpoint) return false;
            string a = production.RootDirectory + Path.DirectorySeparatorChar;
            string b = sandbox.RootDirectory + Path.DirectorySeparatorChar;
            return production.RootDirectory != sandbox.RootDirectory &&
                !a.StartsWith(b, StringComparison.Ordinal) && !b.StartsWith(a, StringComparison.Ordinal);
        }

        internal bool SameCandidate(PurchaseSessionStoragePlan other) => other != null &&
            Environment == other.Environment && RealmId == other.RealmId &&
            Endpoint == other.Endpoint && RootDirectory == other.RootDirectory;
    }

    // Per-session, one-way candidate latch, not an active storage context.
    // There is intentionally no reset/fallback, runtime initialization or I/O.
    public sealed class PurchaseSessionCandidateLatch
    {
        private readonly object gate = new object();
        private PurchaseSessionStoragePlan selected;
        public PurchaseSessionStoragePlan Selected { get { lock (gate) return selected; } }
        public bool TrySelectCandidate(PurchaseSessionStoragePlan candidate)
        {
            if (candidate == null) return false;
            lock (gate)
            {
                if (selected != null) return selected.SameCandidate(candidate);
                selected = candidate;
                return true;
            }
        }
    }
}
