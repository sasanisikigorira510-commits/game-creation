using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WitchTower.Tests
{
    public sealed class IapPurchaseEnvironmentPolicyTests
    {
        private static Type Policy => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower.Monetization.IapPurchaseEnvironmentPolicy", true);
        private static string Message(string raw, bool development) =>
            (string)Policy.GetMethod("NewPurchaseBlockedMessage").Invoke(null, new object[] { raw, development });
        private static string Constant(string name) => (string)Policy.GetField(name).GetRawConstantValue();
        private static string Raw(string url) => "{\"BaseUrl\":\"" + url + "\",\"ReleaseAppleLinking\":true}";

        [TestCase("https://api.nasus-games.com")]
        [TestCase("https://api.nasus-games.com/")]
        [TestCase(" https://api.nasus-games.com/// ")]
        [TestCase("https://API.NASUS-GAMES.COM:443")]
        public void DevelopmentProductionCheckoutIsBlockedBeforeItStarts(string url)
        {
            Assert.That(Message(Raw(url), true), Is.EqualTo(Constant("DevelopmentProductionMessage")));
        }

        [TestCase("https://api.nasus-games.com/qa")]
        [TestCase("https://api.nasus-games.com/sandbox-refund-20260928/device")]
        [TestCase("http://127.0.0.1:8787")]
        public void SeparateQaEndpointIsLeftToExistingAuthenticatedPreflight(string url)
        {
            Assert.That(Message(Raw(url), true), Is.Empty);
        }

        [TestCase(null)] [TestCase("")] [TestCase("not-json")]
        [TestCase("{}")] [TestCase("{\"BaseUrl\":\"\"}")]
        [TestCase("{\"BaseUrl\":\"file:///private/purchase\"}")]
        [TestCase("{\"BaseUrl\":\"http://external.invalid\"}")]
        [TestCase("{\"BaseUrl\":\"https://secret:token@api.nasus-games.com\"}")]
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com?credential=secret\"}")]
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com#receipt\"}")]
        public void UnusableDevelopmentConfigurationFailsClosedWithoutEchoingIt(string raw)
        {
            Assert.That(Message(raw, true), Is.EqualTo(Constant("InvalidConfigurationMessage")));
        }

        [TestCase(null)] [TestCase("not-json")]
        [TestCase("{\"BaseUrl\":\"https://api.nasus-games.com\"}")]
        public void ReleaseCheckoutContractIsNotChangedByDevelopmentGuard(string raw)
        {
            Assert.That(Message(raw, false), Is.Empty);
        }
    }
}
