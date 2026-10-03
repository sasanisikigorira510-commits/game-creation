using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class AppleQaBuildGateTests
    {
        private static MethodInfo Gate => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
            .GetType("AppleAccountBuildGate", true).GetMethod("ValidateConfiguration");

        [TestCase(false, false)]
        [TestCase(true, false)]
        public void ReleaseRejectsQaKeyRegardlessOfAppleFlag(bool apple, bool development)
        {
            Assert.Throws<TargetInvocationException>(() => Gate.Invoke(null, new object[] {apple, new string('a',64), development}));
        }
        [Test]
        public void ReleaseRejectsAppleAndDevelopmentAcceptsWellFormedKey()
        {
            Assert.Throws<TargetInvocationException>(() => Gate.Invoke(null, new object[] {true, null, false}));
            Assert.DoesNotThrow(() => Gate.Invoke(null, new object[] {true, new string('a',64), true}));
            Assert.DoesNotThrow(() => Gate.Invoke(null, new object[] {false, null, false}));
        }
        [TestCase("short")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        public void MalformedKeyCannotBuild(string key)
        {
            Assert.Throws<TargetInvocationException>(() => Gate.Invoke(null, new object[] {true, key, true}));
        }
        [Test]
        public void QaKeyRequiresAppleDevelopmentFlag()
        {
            Assert.Throws<TargetInvocationException>(() => Gate.Invoke(null, new object[] {false, new string('a',64), true}));
        }

        [Test]
        public void CurrentPublicIosConfigurationMeetsNativeEnvironmentMinimumOs()
        {
            string raw = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/PlayerDataService.json").text;
            var minimum = UnityEditor.PlayerSettings.iOS.targetOSVersionString;
            Assert.That(new Version(minimum), Is.GreaterThanOrEqualTo(new Version(16, 0)));
            Assert.DoesNotThrow(() => Gate.DeclaringType.GetMethod("ValidateReleaseMinimumOs")
                .Invoke(null, new object[] { raw, false, minimum }));
        }

        [TestCase("15.0", false)]
        [TestCase("15.9", false)]
        [TestCase("bad", false)]
        [TestCase("16.0", true)]
        [TestCase("17.1", true)]
        public void ReleaseCannotAdvertiseUnsupportedOperatingSystem(string version, bool accepted)
        {
            var method = Gate.DeclaringType.GetMethod("ValidateReleaseMinimumOs");
            var args = new object[] { "{\"BaseUrl\":\"https://api.nasus-games.com\",\"ReleaseAppleLinking\":true}", false, version };
            if (accepted) Assert.DoesNotThrow(() => method.Invoke(null, args));
            else Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args));
            args[1] = true;
            Assert.DoesNotThrow(() => method.Invoke(null, args), "Existing Development gameplay remains supported.");
        }

        private static Type Policy => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp")
            .GetType("WitchTower.Save.AppleAccountConfiguration", true);
        private static object PublicConfig(string url = "https://api.nasus-games.com", string key = null,
            bool experimental = false, string refund = null)
        {
            var config = Activator.CreateInstance(Policy);
            Policy.GetField("BaseUrl").SetValue(config, url);
            Policy.GetField("ReleaseAppleLinking").SetValue(config, true);
            Policy.GetField("ExperimentalAppleLinking").SetValue(config, experimental);
            Policy.GetField("QaAccessKey").SetValue(config, key);
            Policy.GetField("RefundSandboxProfile").SetValue(config, refund);
            return config;
        }
        private static bool Enabled(object config, bool development) =>
            (bool)Policy.GetMethod("IsEnabled").Invoke(config, new object[] { development });
        private static void Validate(object config, bool development) =>
            Policy.GetMethod("ValidateBuild").Invoke(config, new object[] { development });

        [TestCase(false)]
        [TestCase(true)]
        public void ApprovedProductionOptInIsEnabledAndBuildable(bool development)
        {
            var config = PublicConfig();
            Assert.IsTrue(Enabled(config, development));
            Assert.DoesNotThrow(() => Validate(config, development));
            var method = Gate.DeclaringType.GetMethod("ValidateReleaseConfiguration");
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { JsonUtility.ToJson(config), development }));
        }
        [TestCase(null)]
        [TestCase("")]
        [TestCase("http://api.nasus-games.com")]
        [TestCase("https://api.nasus-games.com/")]
        [TestCase("https://api.nasus-games.com/qa")]
        [TestCase("https://api.nasus-games.com.evil.invalid")]
        [TestCase("https://other.invalid")]
        public void PublicOptInRejectsUnreviewedEndpointInAllBuilds(string url)
        {
            foreach (bool development in new[] { false, true })
            {
                var config = PublicConfig(url);
                Assert.IsFalse(Enabled(config, development));
                Assert.Throws<TargetInvocationException>(() => Validate(config, development));
            }
        }
        [TestCase(true, null, null)]
        [TestCase(false, "synthetic-qa-key", null)]
        [TestCase(false, null, "refund-20260928")]
        public void PublicOptInNeverMixesQaSettings(bool experimental, string key, string refund)
        {
            foreach (bool development in new[] { false, true })
            {
                var config = PublicConfig(key: key, experimental: experimental, refund: refund);
                Assert.IsFalse(Enabled(config, development));
                Assert.Throws<TargetInvocationException>(() => Validate(config, development));
            }
        }
        [Test]
        public void NoOptInRemainsDisabledAndExperimentalStillRequiresDevelopment()
        {
            var config = Activator.CreateInstance(Policy);
            Assert.IsFalse(Enabled(config, false));
            Assert.IsFalse(Enabled(config, true));
            Policy.GetField("ExperimentalAppleLinking").SetValue(config, true);
            Assert.IsFalse(Enabled(config, false));
            Assert.IsTrue(Enabled(config, true));
            Assert.Throws<TargetInvocationException>(() => Validate(config, false));
        }
    }
}
