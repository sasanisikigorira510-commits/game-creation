using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WitchTower.Tests
{
    public class RefundSandboxIsolationTests
    {
        private static Type Target => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower.Save.RefundSandboxConfiguration", true);
        private static string Raw(string profile = "refund-20260928", string url = "https://api.nasus-games.com/sandbox-refund-20260928/device", double expiry = 10000) =>
            "{\"BaseUrl\":\""+url+"\",\"RefundSandboxProfile\":\""+profile+"\",\"ExperimentalAppleLinking\":true,\"QaAccessKey\":\""+new string('a',64)+"\",\"ExpiresUnix\":"+expiry+"}";
        private static object Validate(string raw, bool development, double now, bool unexpired) =>
            Target.GetMethod("Validate").Invoke(null, new object[] {raw, development, now, unexpired});
        private static string Root(string root, string raw, bool development) => (string)Target.GetMethod("ResolveRoot",
            new[] {typeof(string),typeof(string),typeof(bool)}).Invoke(null, new object[] {root,raw,development});

        [Test] public void NormalBuildKeepsExistingSaveRoot()
        {
            Assert.That(Root("/normal", "{\"BaseUrl\":\"https://api.nasus-games.com/qa\"}", true), Is.EqualTo("/normal"));
            Assert.That(Root("/normal", null, false), Is.EqualTo("/normal"));
        }
        [Test] public void SandboxUsesSeparateDirectoryEvenAfterExpiry()
        {
            string expected = Path.Combine("/normal", "refund-sandbox", "refund-20260928");
            Assert.That(Root("/normal", Raw(), true), Is.EqualTo(expected));
            Assert.That(Root("/normal", Raw(expiry:1), true), Is.EqualTo(expected));
            Assert.Throws<TargetInvocationException>(() => Validate(Raw(expiry:1), true, 2, true));
        }
        [TestCase("", "https://api.nasus-games.com/sandbox-refund-20260928/device")]
        [TestCase("../other", "https://api.nasus-games.com/sandbox-refund-20260928/device")]
        [TestCase("refund-20260928", "https://api.nasus-games.com/qa")]
        public void InconsistentConfigCannotFallBackToNormalSave(string profile, string url)
        {
            Assert.Throws<TargetInvocationException>(() => Root("/normal", Raw(profile,url), true));
        }
        [Test] public void ReleaseCannotBuildOrUseSandboxAndFutureExpiryIsBounded()
        {
            Assert.Throws<TargetInvocationException>(() => Root("/normal", Raw(), false));
            Assert.Throws<TargetInvocationException>(() => Validate(Raw(), false, 100, true));
            Assert.Throws<TargetInvocationException>(() => Validate(Raw(expiry:100000), true, 100, true));
            Assert.That(Validate(Raw(), true, 100, true), Is.True);
        }
    }
}
