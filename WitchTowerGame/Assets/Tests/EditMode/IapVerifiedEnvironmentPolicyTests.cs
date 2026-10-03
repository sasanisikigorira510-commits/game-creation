using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class IapVerifiedEnvironmentPolicyTests
    {
        private const string Production = "Production";
        private const string Sandbox = "Sandbox";

        private static Assembly RuntimeAssembly => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp");
        private static Type Policy => RuntimeAssembly.GetType(
            "WitchTower.Monetization.IapPurchaseEnvironmentPolicy", true);
        private static Type CapabilityType => RuntimeAssembly.GetType(
            "WitchTower.Monetization.PurchaseEnvironmentCapability", true);
        private static Type EnvironmentType => RuntimeAssembly.GetType(
            "WitchTower.Monetization.VerifiedApplePurchaseEnvironment", true);

        private static object Capability(int version = 1, string store = "apple",
            string appleEnvironment = Production, string dataRealm = Production, bool ready = true)
        {
            object capability = Activator.CreateInstance(CapabilityType);
            Set(capability, "Version", version);
            Set(capability, "Store", store);
            Set(capability, "AppleEnvironment", appleEnvironment);
            Set(capability, "DataRealm", dataRealm);
            Set(capability, "Ready", ready);
            return capability;
        }

        private static void Set(object capability, string field, object value) =>
            CapabilityType.GetField(field).SetValue(capability, value);

        private static string Message(string environment, object capability, string activeDataRealm = Production) =>
            Message(Enum.Parse(EnvironmentType, environment), capability, activeDataRealm);

        private static string Message(object environment, object capability, string activeDataRealm) =>
            (string)Policy.GetMethod("VerifiedEnvironmentBlockedMessage", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { environment, capability, activeDataRealm });

        private static string Constant(string name) => (string)Policy.GetField(name).GetRawConstantValue();

        [Test]
        public void OnlyVerifiedProductionWithCompleteReadyProductionCapabilityAndProductionDataIsAllowed()
        {
            Assert.That(Message(Production, Capability()), Is.Empty);
        }

        [TestCase("Unknown")]
        [TestCase("Unsupported")]
        public void MissingOrUnsupportedNativeVerificationCannotBeReplacedByServerProductionClaim(string environment)
        {
            Assert.That(Message(environment, Capability()),
                Is.EqualTo(Constant("VerifiedEnvironmentUnavailableMessage")));
            Assert.That(Message(environment, null),
                Is.EqualTo(Constant("VerifiedEnvironmentUnavailableMessage")));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(255)]
        [TestCase(int.MaxValue)]
        public void UnrecognizedNativeEnvironmentValueFailsClosed(int environment)
        {
            Assert.That(Message(Enum.ToObject(EnvironmentType, environment), Capability(), Production),
                Is.EqualTo(Constant("VerifiedEnvironmentUnavailableMessage")));
        }

        [TestCase(Production, Production, Production)]
        [TestCase("Unknown", "Unknown", "Unknown")]
        [TestCase(null, null, null)]
        public void SandboxRejectsMismatchedCapabilityOrActiveRealm(
            string appleEnvironment, string dataRealm, string activeDataRealm)
        {
            Assert.That(Message(Sandbox, Capability(appleEnvironment: appleEnvironment, dataRealm: dataRealm),
                    activeDataRealm), Is.EqualTo(Constant("PurchaseEnvironmentMismatchMessage")));
        }

        [Test]
        public void SandboxWithNoServerCapabilityStillCannotStartCheckout()
        {
            Assert.That(Message(Sandbox, null), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [Test]
        public void SandboxRequiresMatchingNativeHeadAndBoundReviewStorage()
        {
            Assert.That(Message(Sandbox, Capability(appleEnvironment: Sandbox, dataRealm: Sandbox), Sandbox), Is.Empty);
            Assert.That(Message(Sandbox, Capability(appleEnvironment: Sandbox, dataRealm: Sandbox, ready: false), Sandbox),
                Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
            Assert.That(Message(Sandbox, Capability(appleEnvironment: Sandbox, dataRealm: Sandbox), Production), Is.Not.Empty);
        }

        [Test]
        public void ProductionWithoutServerCapabilityFailsClosed()
        {
            Assert.That(Message(Production, null), Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void MissingOrUnknownCapabilityVersionFailsClosed(int version)
        {
            Assert.That(Message(Production, Capability(version: version)),
                Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Apple")]
        [TestCase("APPLE")]
        [TestCase(" apple")]
        [TestCase("apple ")]
        [TestCase("google")]
        [TestCase("unknown")]
        public void MissingOrNonExactAppleStoreCapabilityFailsClosed(string store)
        {
            Assert.That(Message(Production, Capability(store: store)),
                Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [Test]
        public void NotReadyCapabilityNeverStartsCheckoutEvenWhenAllOtherFieldsMatch()
        {
            Assert.That(Message(Production, Capability(ready: false)),
                Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(Sandbox)]
        [TestCase("Unknown")]
        [TestCase("production")]
        [TestCase("PRODUCTION")]
        [TestCase(" Production")]
        [TestCase("Production ")]
        public void ServerAppleEnvironmentMustBeExactProduction(string environment)
        {
            Assert.That(Message(Production, Capability(appleEnvironment: environment)),
                Is.EqualTo(Constant("PurchaseEnvironmentMismatchMessage")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(Sandbox)]
        [TestCase("Unknown")]
        [TestCase("production")]
        [TestCase("PRODUCTION")]
        [TestCase(" Production")]
        [TestCase("Production ")]
        public void ServerDataRealmMustBeExactProduction(string realm)
        {
            Assert.That(Message(Production, Capability(dataRealm: realm)),
                Is.EqualTo(Constant("PurchaseEnvironmentMismatchMessage")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(Sandbox)]
        [TestCase("Unknown")]
        [TestCase("production")]
        [TestCase("PRODUCTION")]
        [TestCase(" Production")]
        [TestCase("Production ")]
        public void CurrentDataRealmMustBeExactProduction(string realm)
        {
            Assert.That(Message(Production, Capability(), realm),
                Is.EqualTo(Constant("PurchaseEnvironmentMismatchMessage")));
        }

        [TestCase("{}", "PurchaseCapabilityUnavailableMessage")]
        [TestCase("{\"Store\":\"apple\",\"AppleEnvironment\":\"Production\",\"DataRealm\":\"Production\",\"Ready\":true}",
            "PurchaseCapabilityUnavailableMessage")]
        [TestCase("{\"Version\":1,\"AppleEnvironment\":\"Production\",\"DataRealm\":\"Production\",\"Ready\":true}",
            "PurchaseCapabilityUnavailableMessage")]
        [TestCase("{\"Version\":1,\"Store\":\"apple\",\"DataRealm\":\"Production\",\"Ready\":true}",
            "PurchaseEnvironmentMismatchMessage")]
        [TestCase("{\"Version\":1,\"Store\":\"apple\",\"AppleEnvironment\":\"Production\",\"Ready\":true}",
            "PurchaseEnvironmentMismatchMessage")]
        [TestCase("{\"Version\":1,\"Store\":\"apple\",\"AppleEnvironment\":\"Production\",\"DataRealm\":\"Production\"}",
            "PurchaseCapabilityUnavailableMessage")]
        public void OmittedServerFieldsDoNotAcquirePermissiveDefaults(string json, string expectedConstant)
        {
            object capability = JsonUtility.FromJson(json, CapabilityType);
            Assert.That(Message(Production, capability), Is.EqualTo(Constant(expectedConstant)));
        }

        [Test]
        public void SerializedInactiveBackendDeclarationBlocksProductionCheckout()
        {
            object capability = JsonUtility.FromJson(
                "{\"Version\":1,\"Store\":\"apple\",\"AppleEnvironment\":\"Unknown\",\"DataRealm\":\"Unknown\",\"Ready\":false}",
                CapabilityType);
            Assert.That(Message(Production, capability),
                Is.EqualTo(Constant("PurchaseCapabilityUnavailableMessage")));
        }

        [Test]
        public void EvaluationDoesNotMutateCapabilityAndRepeatedCallsRemainReadOnly()
        {
            object capability = Capability();
            string original = JsonUtility.ToJson(capability);

            Assert.That(Message(Production, capability), Is.Empty);
            Assert.That(Message(Sandbox, capability, Sandbox), Is.Not.Empty);
            Assert.That(Message("Unknown", capability), Is.Not.Empty);
            Assert.That(Message(Production, capability, Sandbox), Is.Not.Empty);

            Assert.That(JsonUtility.ToJson(capability), Is.EqualTo(original));
            Assert.That(Message(Production, capability), Is.Empty);
        }
    }
}
