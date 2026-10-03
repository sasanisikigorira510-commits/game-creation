using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WitchTower.Tests
{
    public sealed class SaveEnvironmentIsolationTests
    {
        private const string Production = "{\"BaseUrl\":\"https://api.nasus-games.com\"}";
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower.Save." + name, true);
        private static string Root(string root, string raw, bool development) => (string)T("SaveEnvironmentConfiguration")
            .GetMethod("ResolveRoot", new[] { typeof(string), typeof(string), typeof(bool) })
            .Invoke(null, new object[] { root, raw, development });
        private static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);

        [TestCase(true)]
        [TestCase(false)]
        public void ProductionUsesStableSeparateRoot(bool development)
        {
            Assert.That(Root("root", Production, development),
                Is.EqualTo(Path.Combine("root", "environments", "production")));
        }

        [TestCase("https://api.nasus-games.com/")]
        [TestCase(" HTTPS://API.NASUS-GAMES.COM/ ")]
        public void EquivalentProductionAddressKeepsSameSave(string url)
        {
            Assert.That(Root("root", "{\"BaseUrl\":\"" + url + "\"}", true),
                Is.EqualTo(Root("root", Production, false)));
        }

        [TestCase(null)]
        [TestCase("{}")]
        [TestCase("{\"BaseUrl\":\"\"}")]
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com/apple-qa/device\"}")]
        public void PreviousConfigurationsRetainTheirSaveLocation(string raw)
        {
            Assert.That(Root("root", raw, true), Is.EqualTo("root"));
        }

        [Test]
        public void RefundProfileRemainsIsolatedEvenAfterExpiry()
        {
            const string profile = "refund-20260928";
            string raw = "{\"BaseUrl\":\"https://api.nasus-games.com/sandbox-refund-20260928/device" +
                "\",\"RefundSandboxProfile\":\"" + profile +
                "\",\"ExperimentalAppleLinking\":true,\"QaAccessKey\":\"" + new string('a', 64) +
                "\",\"ExpiresUnix\":1}";
            Assert.That(Root("root", raw, true),
                Is.EqualTo(Path.Combine("root", "refund-sandbox", profile)));
            Assert.Throws<TargetInvocationException>(() => Root("root", raw, false));
        }

        [Test]
        public void NewProductionSaveDoesNotReadOrChangeOldAccountFilesAndSurvivesReload()
        {
            string root = Path.Combine(Path.GetTempPath(), "nasus-environment-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string[] oldFiles = { "save.json", "online-identity.json", "online-request.json", "active-account.json", "apple-recovery.json" };
            try
            {
                foreach (string name in oldFiles) File.WriteAllText(Path.Combine(root, name), "old-qa-data:" + name);
                string isolated = Root(root, Production, true);
                Assert.That(T("AppleRecoveryStorage").GetMethod("ActiveDirectory").Invoke(null, new object[] { isolated }), Is.EqualTo(isolated));
                var repository = Activator.CreateInstance(T("PlayerSaveRepository"), Path.Combine(isolated, "save.json"));
                object[] args = { null, false, null };
                Assert.That(T("PlayerSaveRepository").GetMethod("TryLoad").Invoke(repository, args), Is.EqualTo(true), args[2] as string);
                var save = args[0];
                Assert.That(args[1], Is.EqualTo(false));
                Assert.That(Field(save, "PlayerLevel"), Is.EqualTo(1));
                Assert.That(Field(save, "PaidGachaStones"), Is.EqualTo(0));
                Assert.That(Field(save, "EconomyRevision"), Is.EqualTo(0));
                Assert.That(Field(save, "ProcessedIapTransactionIds"), Is.Empty);
                Assert.That(File.Exists(Path.Combine(isolated, "online-identity.json")), Is.False);
                object[] reload = { null, false, null };
                Assert.That(T("PlayerSaveRepository").GetMethod("TryLoad").Invoke(repository, reload), Is.EqualTo(true), reload[2] as string);
                Assert.That(Field(reload[0], "PlayerId"), Is.EqualTo(Field(save, "PlayerId")));
                foreach (string name in oldFiles)
                    Assert.That(File.ReadAllText(Path.Combine(root, name)), Is.EqualTo("old-qa-data:" + name));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
