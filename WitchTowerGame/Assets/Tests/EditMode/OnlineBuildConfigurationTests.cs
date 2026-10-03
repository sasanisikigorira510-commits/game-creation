using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchTower.Tests
{
    public sealed class OnlineBuildConfigurationTests
    {
        private static Type Validator => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("WitchTowerReleaseReadinessValidator", true);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"BaseUrl\":\"\"}")]
        [TestCase("{\"BaseUrl\":\"   \"}")]
        [TestCase("{\"BaseUrl\":\"http://api.example.test\"}")]
        [TestCase("{\"BaseUrl\":\"https://127.0.0.1:8787\"}")]
        [TestCase("{\"BaseUrl\":\"https://localhost\"}")]
        [TestCase("{\"BaseUrl\":\"https://user:password@api.example.test\"}")]
        [TestCase("{\"BaseUrl\":\"https://api.example.test?key=value\"}")]
        [TestCase("{\"BaseUrl\":\"https://api.example.test#fragment\"}")]
        [TestCase("broken-json")]
        public void UnusableDeviceEndpointsAreRejected(string json)
        {
            Assert.That(Validator.GetMethod("ValidateOnlineConfiguration").Invoke(null, new object[] {json}), Is.Not.Null);
        }

        [TestCase("https://api.example.test")]
        [TestCase("https://api.example.test/v1/")]
        public void HttpsServiceEndpointsAreAccepted(string url)
        {
            Assert.That(Validator.GetMethod("ValidateOnlineConfiguration").Invoke(null,
                new object[] {"{\"BaseUrl\":\"" + url + "\"}"}), Is.Null);
        }

        [TestCase(BuildTargetGroup.iOS)]
        [TestCase(BuildTargetGroup.Android)]
        public void MobileReadinessIncludesMissingEndpointAsBlocker(BuildTargetGroup target)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/Resources/PlayerDataService.json");
            var expected = Validator.GetMethod("ValidateOnlineConfiguration").Invoke(null, new object[] {asset != null ? asset.text : null});
            var result = Validator.GetMethod("ValidateProject").Invoke(null, new object[] {false, target});
            var blockers = (IList)result.GetType().GetProperty("Blockers").GetValue(result);
            if (expected != null) Assert.That(blockers.Contains(expected), Is.True);
            else Assert.That(blockers.Cast<string>().Any(x => x.Contains("PlayerDataService.json")), Is.False);
        }
    }
}
