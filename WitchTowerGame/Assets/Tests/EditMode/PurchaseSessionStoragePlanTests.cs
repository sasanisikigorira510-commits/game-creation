using System;
using System.Linq;
using System.Reflection;
using System.Threading;
#if !NASUS_NAMESPACE_STANDALONE
using NUnit.Framework;
#endif

namespace WitchTower.Tests
{
    // Runs only the actual pure runtime types via reflection. No Unity object,
    // filesystem, save/profile, HTTP, StoreKit or deployment API is used.
    public sealed class PurchaseSessionStoragePlanTests
    {
        private const string Root = "/synthetic/device-data-no-files";
        private const string P = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string S = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Bundle = "com.nasus.dungeonmonsterroguelike";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Save." + name, true);
        private static object Object(string name) => Activator.CreateInstance(T(name));
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field).SetValue(owner, value);
        private static object Get(object owner, string property) => owner.GetType().GetProperty(property).GetValue(owner);
        private static object Pin(string env, string realm, string endpoint)
        {
            var p = Object("PurchaseNamespaceDeploymentPin");
            Set(p, "Version", 1); Set(p, "Environment", env); Set(p, "RealmId", realm); Set(p, "Endpoint", endpoint);
            return p;
        }
        private static object Declaration(string env, string realm)
        {
            var d = Object("PurchaseNamespaceDescriptor");
            Set(d, "Version", 1); Set(d, "Environment", env); Set(d, "RealmId", realm); Set(d, "BundleId", Bundle);
            return d;
        }
        private static object Candidate(string env = "Production", string realm = P,
            string endpoint = "https://production.example.invalid", string root = Root)
            => Create(root, env, Pin(env, realm, endpoint), Declaration(env, realm), endpoint);
        private static object Create(string root, string observed, object pin, object declaration, string origin)
        {
            object[] args = { root, observed, pin, declaration, origin, null };
            bool accepted = (bool)T("PurchaseSessionStoragePlan").GetMethod("TryCreateCandidate").Invoke(null, args);
            if (accepted != (args[5] != null)) throw new InvalidOperationException("Candidate Try contract was inconsistent.");
            return args[5];
        }
        private static bool Isolated(object p, object s) => (bool)T("PurchaseSessionStoragePlan")
            .GetMethod("AreIsolatedCandidates").Invoke(null, new[] { p, s });
        private static bool Select(object latch, object candidate) => (bool)latch.GetType()
            .GetMethod("TrySelectCandidate").Invoke(latch, new[] { candidate });

        public static int VerifyAllCases()
        {
            int checks = 0;
            Action<bool, string> check = (condition, name) =>
            {
                checks++;
                if (!condition) throw new InvalidOperationException("Namespace contract failed: " + name);
            };
            object p = Candidate();
            object s = Candidate("Sandbox", S, "https://sandbox.example.invalid/device");
            check(p != null && s != null, "known candidates");
            check((string)Get(p, "RootDirectory") == Root + "/environments/production", "preserve production path");
            check((string)Get(s, "RootDirectory") == Root + "/environments/sandbox/" + S, "sandbox realm path");
            check(Isolated(p, s) && !Isolated(s, p), "ordered isolation pair");
            check(!Isolated(null, s) && !Isolated(p, null), "null pair");
            check(!Isolated(p, Candidate("Sandbox", P, "https://sandbox.example.invalid/device")), "realm alias");
            check(!Isolated(p, Candidate("Sandbox", S)), "endpoint alias");
            check(!Isolated(p, Candidate("Sandbox", S, "https://sandbox.example.invalid/device", (string)Get(p, "RootDirectory"))), "nested roots");

            foreach (string env in new[] { null, "", "Unknown", "Unsupported", "production", "sandbox", " Production", "Sandbox ", "Xcode" })
                check(Create(Root, env, Pin("Production", P, "https://production.example.invalid"),
                    Declaration("Production", P), "https://production.example.invalid") == null, "unknown environment");
            foreach (string root in new[] { null, "", "/", "relative", " /synthetic", Root + "/", Root + "/..", "/synthetic//device", "/synthetic\\device", "/synthetic/\0device" })
                check(Candidate(root: root) == null, "invalid root");
            foreach (string id in new[] { null, "", " ", "../production", new string('a', 31), new string('a', 33), new string('A', 32), new string('g', 32), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" })
                check(Candidate(realm: id) == null, "invalid realm");
            foreach (string endpoint in new[] { null, "", "http://sandbox.example.invalid", "HTTPS://sandbox.example.invalid",
                "https://Sandbox.example.invalid", "https://sandbox.example.invalid:443", "https://sandbox.example.invalid/",
                "https://user:secret@sandbox.example.invalid", "https://sandbox.example.invalid?x=1", "https://sandbox.example.invalid#x",
                "https://sandbox.example.invalid/v1/../device", "https://sandbox.example.invalid/%2fdevice" })
                check(Candidate(endpoint: endpoint) == null, "noncanonical endpoint");

            var pin = Pin("Production", P, "https://production.example.invalid");
            var declaration = Declaration("Production", P);
            check(Create(Root, "Production", null, declaration, "https://production.example.invalid") == null, "missing pin");
            check(Create(Root, "Production", pin, null, "https://production.example.invalid") == null, "missing declaration");
            foreach (string origin in new[] { null, "", "https://sandbox.example.invalid", "https://production.example.invalid/" })
                check(Create(Root, "Production", pin, declaration, origin) == null, "response origin mismatch");
            foreach (int version in new[] { 0, -1, 2 })
            {
                Set(pin, "Version", version);
                check(Create(Root, "Production", pin, declaration, "https://production.example.invalid") == null, "pin version");
                Set(pin, "Version", 1); Set(declaration, "Version", version);
                check(Create(Root, "Production", pin, declaration, "https://production.example.invalid") == null, "declaration version");
                Set(declaration, "Version", 1);
            }
            foreach (string field in new[] { "Environment", "RealmId", "Endpoint" })
            {
                string original = (string)pin.GetType().GetField(field).GetValue(pin);
                foreach (string value in new[] { null, "", " ", "Sandbox", "Unknown" })
                {
                    Set(pin, field, value);
                    check(Create(Root, "Production", pin, declaration, "https://production.example.invalid") == null, "pin mismatch");
                }
                Set(pin, field, original);
            }
            foreach (string field in new[] { "Environment", "RealmId", "BundleId" })
            {
                string original = (string)declaration.GetType().GetField(field).GetValue(declaration);
                foreach (string value in new[] { null, "", " ", "other" })
                {
                    Set(declaration, field, value);
                    check(Create(Root, "Production", pin, declaration, "https://production.example.invalid") == null, "declaration mismatch");
                }
                Set(declaration, field, original);
            }
            object immutable = Create(Root, "Production", pin, declaration, "https://production.example.invalid");
            Set(pin, "Environment", "Sandbox"); Set(pin, "RealmId", S); Set(pin, "Endpoint", "https://changed.example.invalid");
            Set(declaration, "Environment", "Sandbox"); Set(declaration, "RealmId", S);
            check((string)Get(immutable, "Environment") == "Production" && (string)Get(immutable, "RealmId") == P &&
                (string)Get(immutable, "Endpoint") == "https://production.example.invalid", "input mutation cannot alter candidate");
            foreach (var property in T("PurchaseSessionStoragePlan").GetProperties()) check(!property.CanWrite, "immutable property");

            var latch = Object("PurchaseSessionCandidateLatch");
            check(Get(latch, "Selected") == null && !Select(latch, null), "unbound latch");
            check(Select(latch, p) && ReferenceEquals(Get(latch, "Selected"), p), "first candidate");
            check(Select(latch, Candidate()) && ReferenceEquals(Get(latch, "Selected"), p), "same candidate idempotence");
            foreach (object different in new[] { s, Candidate(realm: S), Candidate(root: "/synthetic/other-root"), Candidate(endpoint: "https://other.example.invalid") })
                check(!Select(latch, different) && ReferenceEquals(Get(latch, "Selected"), p), "no in-session switch");
            check(T("PurchaseSessionCandidateLatch").GetMethod("Reset") == null, "no fallback reset");

            // Real competing managed threads, but still only pure in-memory
            // candidates: exactly one realm wins; no later selection changes it.
            for (int repetition = 0; repetition < 12; repetition++)
            {
                var racing = Object("PurchaseSessionCandidateLatch");
                var start = new ManualResetEventSlim(false);
                int acceptedP = 0, acceptedS = 0;
                Exception[] failures = new Exception[8];
                Thread[] threads = Enumerable.Range(0, 8).Select(i => new Thread(() =>
                {
                    try
                    {
                        start.Wait();
                        if (Select(racing, i % 2 == 0 ? p : s))
                        {
                            if (i % 2 == 0) Interlocked.Increment(ref acceptedP);
                            else Interlocked.Increment(ref acceptedS);
                        }
                    }
                    catch (Exception error) { failures[i] = error; }
                }) { IsBackground = true }).ToArray();
                int started = 0;
                try
                {
                    foreach (var thread in threads) { thread.Start(); started++; }
                    start.Set();
                    foreach (var thread in threads) check(thread.Join(3000), "bounded worker completion");
                    check(failures.All(error => error == null), "worker exceptions reported on test thread");
                }
                finally
                {
                    start.Set();
                    for (int i = 0; i < started; i++) if (threads[i].IsAlive) threads[i].Join(1000);
                    // Do not dispose a handle a stalled worker might still use.
                    if (threads.Take(started).All(thread => !thread.IsAlive)) start.Dispose();
                }
                check((acceptedP > 0) != (acceptedS > 0), "single concurrent winner");
                object winner = Get(racing, "Selected");
                check(ReferenceEquals(winner, acceptedP > 0 ? p : s), "winner preserved");
                check(!Select(racing, acceptedP > 0 ? s : p), "loser cannot replace winner");
            }
            return checks;
        }

#if NASUS_NAMESPACE_STANDALONE
        public static int Main()
        {
            Assembly.Load("Assembly-CSharp");
            Console.WriteLine("NAMESPACE_CONTRACT_CHECKS=" + VerifyAllCases());
            return 0;
        }
#else
        [Test]
        public void CandidatesRemainPinnedImmutableAndIsolatedWithoutStorageAccess()
            => Assert.That(VerifyAllCases(), Is.GreaterThan(100));
#endif
    }
}
