using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class ReleasePurchaseStorageTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        private const string Raw = "{\"BaseUrl\":\"https://api.nasus-games.com\",\"ReleaseAppleLinking\":true}";
        private string root;
        private GameObject owner;
        private object oldSaveManager, oldOverride;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object P(object value, string name) => value.GetType().GetProperty(name, Hidden).GetValue(value);
        private static object Env(string name) => Enum.Parse(T("Monetization.VerifiedApplePurchaseEnvironment"), name);
        private object Bind(string environment, string raw = Raw) => T("Save.StorageStartupContext")
            .GetMethod("BindReleaseEnvironment", Static).Invoke(null, new[] { root, raw, Env(environment) });
        private string Resolve(string environment) => (string)T("Save.ReleasePurchaseStorage")
            .GetMethod("Resolve", Static).Invoke(null, new object[] { root, environment });
        [SetUp] public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "WitchTower-ReleaseStorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }
        [TearDown] public void Cleanup()
        {
            if (owner != null)
            {
                UnityEngine.Object.DestroyImmediate(owner);
                T("Managers.SaveManager").GetField("<Instance>k__BackingField", Static).SetValue(null, oldSaveManager);
                T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", Static).SetValue(null, oldOverride);
            }
            Directory.Delete(root, true);
        }
        [Test]
        public void SandboxStartsSeparatelyAndDoesNotReadCopyOrOverwriteProductionOrOldQa()
        {
            foreach (string relative in new[] { "environments/production", "refund-sandbox/refund-20260928", "" })
            {
                string directory = Path.Combine(root, relative);
                Directory.CreateDirectory(directory);
                foreach (string name in new[] { "save.json", "online-identity.json", "online-request.json", "active-account.json" })
                    File.WriteAllText(Path.Combine(directory, name), "private-fixture-" + relative + name);
            }
            var previous = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            object context = Bind("Sandbox");
            string selected = (string)P(context, "RootDirectory");
            Assert.That(selected, Is.EqualTo(Path.Combine(root, "environments", "review-sandbox-v1")));
            Assert.That(P(context, "PurchaseEnvironment"), Is.EqualTo("Sandbox"));
            Assert.That(P(context, "HasPurchaseRealmAuthority"), Is.True);
            Assert.That((string)P(context, "ConfigurationJson"), Does.Contain("https://api.nasus-games.com/review-sandbox"));
            Assert.That(Directory.GetFiles(selected).Select(Path.GetFileName), Is.EqualTo(new[] { "purchase-environment-v1.txt" }));
            foreach (var file in previous) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
        }
        [Test]
        public void ProductionRetainsExistingSaveIdentityPendingAndSelectedAccountBytes()
        {
            string production = Path.Combine(root, "environments", "production");
            Directory.CreateDirectory(production);
            foreach (string name in new[] { "save.json", "online-identity.json", "online-request.json", "active-account.json" })
                File.WriteAllText(Path.Combine(production, name), "retain-" + name);
            var files = Directory.GetFiles(production).ToDictionary(p => p, File.ReadAllBytes);
            Assert.That(P(Bind("Production"), "RootDirectory"), Is.EqualTo(production));
            foreach (var file in files) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
            Assert.That(P(Bind("Production"), "RootDirectory"), Is.EqualTo(production));
        }
        [TestCase("Unknown")]
        [TestCase("Unsupported")]
        public void UnknownEnvironmentDoesNotCreateAnyStorage(string environment)
        {
            Assert.Throws<TargetInvocationException>(() => Bind(environment));
            Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
        }
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com/review-sandbox\",\"ReleaseAppleLinking\":true}")]
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com\",\"ReleaseAppleLinking\":true,\"QaAccessKey\":\"secret\"}")]
        [TestCase("{\"BaseUrl\":\"https://other.invalid\",\"ReleaseAppleLinking\":true}")]
        public void UnapprovedPackagedConfigurationCannotSelectAReleaseRoot(string raw)
        {
            Assert.Throws<TargetInvocationException>(() => Bind("Sandbox", raw));
            Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
        }
        [Test]
        public void UnmarkedPopulatedReviewRootIsNeverAdopted()
        {
            string directory = Path.Combine(root, "environments", "review-sandbox-v1");
            Directory.CreateDirectory(directory);
            string save = Path.Combine(directory, "save.json");
            File.WriteAllText(save, "unexpected-data");
            Assert.Throws<TargetInvocationException>(() => Resolve("Sandbox"));
            Assert.That(File.ReadAllText(save), Is.EqualTo("unexpected-data"));
        }
        [Test]
        public void BindingMismatchRetainsAllFilesAndBlocksLoading()
        {
            string directory = Resolve("Sandbox");
            string marker = Path.Combine(directory, "purchase-environment-v1.txt");
            File.WriteAllText(marker, "1\nProduction\nhttps://api.nasus-games.com\n");
            Assert.Throws<TargetInvocationException>(() => Resolve("Sandbox"));
            Assert.That(File.ReadAllText(marker), Does.Contain("Production"));
        }
        [Test]
        public void InterruptedMarkerWriteCanResumeWithoutTouchingSaves()
        {
            string directory = Resolve("Sandbox");
            string marker = Path.Combine(directory, "purchase-environment-v1.txt");
            File.Move(marker, marker + ".tmp");
            Assert.That(Resolve("Sandbox"), Is.EqualTo(directory));
            Assert.That(File.Exists(marker), Is.True);
        }
        [Test]
        public void ReviewRepositoryCreatesNewIdentityAndReloadsItWithoutChangingProduction()
        {
            var oldRandom = UnityEngine.Random.state;
            try
            {
                string production = Resolve("Production");
                string sandbox = Resolve("Sandbox");
                Type repository = T("Save.PlayerSaveRepository");
                Func<string, object> load = directory => {
                    object instance = Activator.CreateInstance(repository, Path.Combine(directory, "save.json"));
                    object[] args = { null, false, null };
                    Assert.That(repository.GetMethod("TryLoad").Invoke(instance, args), Is.True, args[2] as string);
                    Assert.That(args[1], Is.False);
                    return args[0];
                };
                object original = load(production);
                var productionBytes = Directory.GetFiles(production, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => path, File.ReadAllBytes);
                object review = load(sandbox);
                string player = (string)review.GetType().GetField("PlayerId").GetValue(review);
                Assert.That(player, Is.Not.EqualTo(original.GetType().GetField("PlayerId").GetValue(original)));
                Assert.That(review.GetType().GetField("PaidGachaStones").GetValue(review), Is.EqualTo(0));
                object reloaded = load(sandbox);
                Assert.That(reloaded.GetType().GetField("PlayerId").GetValue(reloaded), Is.EqualTo(player));
                foreach (var entry in productionBytes) Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value));
            }
            finally { UnityEngine.Random.state = oldRandom; }
        }

        [Test]
        public void ReviewPresentationClearCannotEraseProductionPendingPresentation()
        {
            const string productionKey = "witchtower_pending_ten_pull_presentation_v1";
            const string reviewKey = productionKey + "_review_sandbox_v1";
            bool hadProduction = PlayerPrefs.HasKey(productionKey), hadReview = PlayerPrefs.HasKey(reviewKey);
            string previousProduction = PlayerPrefs.GetString(productionKey), previousReview = PlayerPrefs.GetString(reviewKey);
            try
            {
                Component saves = PendingManager();
                Assert.That(saves.GetType().GetMethod("TryBindReleaseEnvironment", Hidden)
                    .Invoke(saves, new[] { Env("Sandbox") }), Is.True);
                PlayerPrefs.SetString(productionKey, "preserved-production-presentation");
                PlayerPrefs.SetString(reviewKey, "invalid-review-presentation");
                Type journal = T("Home.TenPullPresentationJournal");
                Assert.That(journal.GetProperty("HasPending").GetValue(null), Is.True);
                journal.GetMethod("Clear").Invoke(null, null);
                Assert.That(PlayerPrefs.HasKey(reviewKey), Is.False);
                Assert.That(PlayerPrefs.GetString(productionKey), Is.EqualTo("preserved-production-presentation"));
                Assert.That(journal.GetProperty("HasPending").GetValue(null), Is.False);
            }
            finally
            {
                if (hadProduction) PlayerPrefs.SetString(productionKey, previousProduction); else PlayerPrefs.DeleteKey(productionKey);
                if (hadReview) PlayerPrefs.SetString(reviewKey, previousReview); else PlayerPrefs.DeleteKey(reviewKey);
                PlayerPrefs.Save();
            }
        }

        private Component PendingManager()
        {
            Type savesType = T("Managers.SaveManager");
            oldSaveManager = savesType.GetField("<Instance>k__BackingField", Static).GetValue(null);
            var directoryOverride = savesType.GetProperty("EditorSaveDirectoryOverride", Static);
            oldOverride = directoryOverride.GetValue(null);
            directoryOverride.SetValue(null, root);
            owner = new GameObject("IsolatedReleaseStartup"); owner.SetActive(false);
            var saves = owner.AddComponent(savesType);
            savesType.GetField("<Instance>k__BackingField", Static).SetValue(null, saves);
            savesType.GetField("capturedEditorRootOverride", Hidden).SetValue(saves, root);
            var context = T("Save.StorageStartupContext").GetMethod("AwaitReleaseEnvironment", Static).Invoke(null, new object[] { Raw });
            savesType.GetField("storageContext", Hidden).SetValue(saves, context);
            return saves;
        }

        [Test]
        public void SaveManagerActivatesOnceBeforeLoadAndCannotSwitchAfterActivation()
        {
            Component saves = PendingManager();
            Type savesType = saves.GetType();
            var activate = savesType.GetMethod("TryBindReleaseEnvironment", Hidden);
            Assert.That(activate.Invoke(saves, new[] { Env("Unknown") }), Is.False);
            Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
            Assert.That(activate.Invoke(saves, new[] { Env("Sandbox") }), Is.True);
            string selected = (string)savesType.GetProperty("RootDirectory").GetValue(saves);
            Assert.That(activate.Invoke(saves, new[] { Env("Production") }), Is.False);
            Assert.That(savesType.GetProperty("RootDirectory").GetValue(saves), Is.EqualTo(selected));
        }
    }
}
