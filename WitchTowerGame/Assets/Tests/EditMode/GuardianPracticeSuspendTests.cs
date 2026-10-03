using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class GuardianPracticeSuspendTests
    {
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method).Invoke(obj, args);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static void Field(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        private static void Property(object obj, string name, object value) => obj.GetType().GetProperty(name).SetValue(obj, value);
        private static PropertyInfo Instance(string type) => T(type).GetProperty("Instance");
        private static PropertyInfo SaveOverride => T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
        private GameObject owner;
        private object previousGame;
        private object previousSave;
        private object previousDirectory;
        private string directory;
        private object game;
        private object saver;
        private object bootstrapper;
        private object profile;

        [SetUp]
        public void Setup()
        {
            previousGame = Instance("Managers.GameManager").GetValue(null);
            previousSave = Instance("Managers.SaveManager").GetValue(null);
            previousDirectory = SaveOverride.GetValue(null);
            directory = Path.Combine(Path.GetTempPath(), "WitchTowerGuardianSuspend_" + Guid.NewGuid().ToString("N"));
            SaveOverride.SetValue(null, directory);
            owner = new GameObject("GuardianSuspendTest");
            owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            saver = owner.AddComponent(T("Managers.SaveManager"));
            bootstrapper = owner.AddComponent(T("Core.Bootstrapper"));
            Instance("Managers.GameManager").SetValue(null, game);
            Instance("Managers.SaveManager").SetValue(null, saver);
            S("Data.GuardianTrialSession", "Reset");
            var saved = S("Save.PlayerSaveData", "CreateDefault");
            Field(saved, "HighestFloor", 60);
            Field(saved, "HasCompletedTutorial", true);
            Field(saved, "TutorialStepId", "Complete");
            var guardian = Activator.CreateInstance(T("Save.OwnedGuardianData"));
            Field(guardian, "Id", "seiryu"); Field(guardian, "Level", 30);
            ((IList)saved.GetType().GetField("OwnedGuardians").GetValue(saved)).Add(guardian);
            ((IList)saved.GetType().GetField("SeenStoryEventIds").GetValue(saved)).Add("story_first_arc_complete");
            Field(saved, "EquippedGuardianId", "seiryu");
            profile = Activator.CreateInstance(T("Data.PlayerProfile"), saved);
            var monster = Call(profile, "AddOwnedMonster", "monster_rock_golem", 30, 0, false);
            Call(profile, "SetPartyMonsterIds", new object[] { new[] { (string)monster.GetType().GetField("InstanceId").GetValue(monster) } });
            Property(profile, "LastActiveAt", "2000-01-01T00:00:00.0000000Z");
            Property(game, "PlayerProfile", profile);
            Property(game, "CurrentFloor", 61);
            Call(saver, "SaveCurrentGame");
        }

        [TearDown]
        public void Cleanup()
        {
            S("Data.GuardianTrialSession", "Reset");
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            Instance("Managers.GameManager").SetValue(null, previousGame);
            Instance("Managers.SaveManager").SetValue(null, previousSave);
            SaveOverride.SetValue(null, previousDirectory);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private string Snapshot() => JsonUtility.ToJson(Call(profile, "ToSaveData", 61));
        private void Suspend(string callback)
        {
            object[] args = callback == "OnApplicationPause" ? new object[] { true } : Array.Empty<object>();
            T("Core.Bootstrapper").GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrapper, args);
        }

        [TestCase(false)] [TestCase(true)]
        public void PracticePauseAndQuitLeaveLiveProfileAndSaveBytesUnchanged(bool elite)
        {
            Assert.That(S("Data.GuardianTrialSession", "BeginPractice", profile, "suzaku", elite), Is.True);
            string profileBefore = Snapshot();
            string path = Path.Combine(directory, "save.json");
            byte[] bytesBefore = File.ReadAllBytes(path);
            var filesBefore = Directory.GetFiles(directory).OrderBy(x => x).ToArray();
            foreach (string callback in new[] { "OnApplicationPause", "OnApplicationQuit" })
            {
                Suspend(callback);
                Assert.That(Snapshot(), Is.EqualTo(profileBefore), callback + " must not update even LastActiveAt.");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytesBefore), callback + " must not rewrite the real save.");
                Assert.That(Directory.GetFiles(directory).OrderBy(x => x).ToArray(), Is.EqualTo(filesBefore), "No backup rotation during practice.");
            }
        }

        [TestCase("ordinary")] [TestCase("acquisition")] [TestCase("oath")]
        public void OtherModesStillSaveLiveChangesOnPauseAndQuit(string mode)
        {
            if (mode == "acquisition") Assert.That(S("Data.GuardianTrialSession", "Begin", profile, "suzaku"), Is.True);
            if (mode == "oath") Assert.That(S("Data.GuardianTrialSession", "BeginOath", profile, "seiryu"), Is.True);
            string path = Path.Combine(directory, "save.json");
            string oldTimestamp = (string)P(profile, "LastActiveAt");
            foreach (string callback in new[] { "OnApplicationPause", "OnApplicationQuit" })
            {
                Call(profile, "AddGold", 17);
                Suspend(callback);
                var loaded = JsonUtility.FromJson(File.ReadAllText(path), T("Save.PlayerSaveData"));
                Assert.That(loaded.GetType().GetField("Gold").GetValue(loaded), Is.EqualTo(P(profile, "Gold")), callback);
                Assert.That(P(profile, "LastActiveAt"), Is.Not.EqualTo(oldTimestamp), callback);
                Assert.That(loaded.GetType().GetField("LastActiveAt").GetValue(loaded), Is.EqualTo(P(profile, "LastActiveAt")), callback);
            }
        }
    }
}
