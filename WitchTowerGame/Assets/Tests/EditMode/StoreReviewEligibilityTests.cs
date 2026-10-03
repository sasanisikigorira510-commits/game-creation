using System;
using System.Linq;
using NUnit.Framework;

namespace WitchTower.Tests
{
    public sealed class StoreReviewEligibilityTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        [TestCase(false, 10, 121, "1.0", false)]
        [TestCase(true, 9, 121, "1.0", false)]
        [TestCase(true, 10, 119, "1.0", false)]
        [TestCase(true, 10, 121, "2.0", false)]
        [TestCase(true, 10, 120, "1.0", true)]
        public void ReviewWaitsForProgressCooldownAndNewVersion(bool tutorial, int floor, int days, string lastVersion, bool expected)
        {
            var profile = Activator.CreateInstance(T("Data.PlayerProfile"), Activator.CreateInstance(T("Save.PlayerSaveData")));
            profile.GetType().GetProperty("HasCompletedTutorial").SetValue(profile, tutorial);
            profile.GetType().GetProperty("HighestFloor").SetValue(profile, floor);
            var now = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
            bool eligible = (bool)T("Monetization.StoreReviewService").GetMethod("IsEligible").Invoke(null,
                new object[] {profile, now, now.AddDays(-days).ToString("O"), lastVersion, "2.0"});
            Assert.That(eligible, Is.EqualTo(expected));
        }
    }
}
