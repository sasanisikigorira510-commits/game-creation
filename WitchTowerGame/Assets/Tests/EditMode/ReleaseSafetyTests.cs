using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class ReleaseSafetyTests
    {
        private Assembly runtimeAssembly;

        [OneTimeSetUp]
        public void ResolveRuntimeAssembly()
        {
            runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
            Assert.That(runtimeAssembly, Is.Not.Null, "Runtime assembly was not loaded.");
        }

        [Test]
        public void NewSaveStartsWithVersionAndThreeRequiredSummons()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Assert.That(saveData, Is.Not.Null);
            Assert.That(ReadField<int>(saveData, "SchemaVersion"), Is.EqualTo(ReadConstant<int>(saveType, "CurrentSchemaVersion")));
            Assert.That(ReadField<int>(saveData, "InitialTutorialSummonCount"), Is.Zero);

            object profile = CreateProfile(saveData);
            Assert.That(GetInitialSummonRemainingCount(profile), Is.EqualTo(3));
        }

        [Test]
        public void TutorialSummonProgressDoesNotUsePreOwnedRosterSize()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            IList ownedMonsters = (IList)saveType.GetField("OwnedMonsters")?.GetValue(saveData);
            Type ownedMonsterType = RuntimeType("WitchTower.Save.OwnedMonsterData");

            for (int i = 0; i < 8; i += 1)
            {
                object monster = Activator.CreateInstance(ownedMonsterType);
                WriteField(monster, "InstanceId", "test-instance-" + i);
                WriteField(monster, "MonsterId", "test-monster-" + i);
                WriteField(monster, "Level", 1);
                ownedMonsters.Add(monster);
            }

            object profile = CreateProfile(saveData);
            Assert.That(GetInitialSummonRemainingCount(profile), Is.EqualTo(3));

            profile.GetType().GetProperty("InitialTutorialSummonCount")?.SetValue(profile, 2);
            Assert.That(GetInitialSummonRemainingCount(profile), Is.EqualTo(1));
        }

        [Test]
        public void ReleaseBuildFeatureFlagsDefaultToSafeValues()
        {
            Type monetizationType = RuntimeType("WitchTower.Monetization.MonetizationFeatureFlags");
            Type bootstrapType = RuntimeType("WitchTower.Data.PrototypePartyBootstrapService");

            Assert.That(ReadConstant<bool>(monetizationType, "StorefrontEnabled"), Is.False);
            Assert.That(ReadConstant<bool>(monetizationType, "AdsEnabled"), Is.False);
            Assert.That(ReadConstant<bool>(bootstrapType, "UnlockAllImplementedMonstersForPreview", nonPublic: true), Is.False);
        }

        [Test]
        public void NewTutorialProfileDoesNotReceivePreviewMonsters()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            Type managerType = RuntimeType("WitchTower.Managers.MasterDataManager");
            Type bootstrapType = RuntimeType("WitchTower.Data.PrototypePartyBootstrapService");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object profile = CreateProfile(saveData);
            var managerObject = new GameObject("MasterDataManager-ReleaseSafetyTest");

            try
            {
                Component manager = managerObject.AddComponent(managerType);
                FieldInfo instanceField = managerType.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
                instanceField?.SetValue(null, manager);
                managerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance)?.Invoke(manager, null);

                bootstrapType.GetMethod("EnsureParty", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new[] { profile, (object)5 });

                IList ownedMonsters = (IList)profile.GetType().GetProperty("OwnedMonsters")?.GetValue(profile);
                Assert.That(ownedMonsters, Has.Count.Zero);
                Assert.That(GetInitialSummonRemainingCount(profile), Is.EqualTo(3));
            }
            finally
            {
                FieldInfo instanceField = managerType.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
                instanceField?.SetValue(null, null);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SaveStoreKeepsBackupAndRecoversCorruptPrimary()
        {
            string testDirectory = Path.Combine(Path.GetTempPath(), "WitchTowerSaveTests-" + Guid.NewGuid().ToString("N"));
            string primaryPath = Path.Combine(testDirectory, "save.json");

            try
            {
                Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
                Type storeType = RuntimeType("WitchTower.Save.SaveFileStore");
                object firstSave = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                WriteField(firstSave, "Gold", 111);
                InvokeTrySave(storeType, primaryPath, firstSave, rotateBackup: true);

                object secondSave = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                WriteField(secondSave, "Gold", 222);
                InvokeTrySave(storeType, primaryPath, secondSave, rotateBackup: true);

                string backupPath = (string)storeType.GetMethod("GetBackupPath")?.Invoke(null, new object[] { primaryPath });
                Assert.That(File.Exists(backupPath), Is.True, "A second save should retain the previous primary as backup.");

                File.WriteAllText(primaryPath, "{ definitely-not-json }");
                Assert.That(InvokeTryLoad(storeType, primaryPath, out _, out _), Is.False);
                Assert.That(InvokeTryLoad(storeType, backupPath, out object recovered, out string backupError),
                    Is.True, backupError);
                Assert.That(ReadField<int>(recovered, "Gold"), Is.EqualTo(111));

                string archivedPath = (string)storeType.GetMethod("TryArchiveUnreadablePrimary")
                    ?.Invoke(null, new object[] { primaryPath });
                Assert.That(File.Exists(archivedPath), Is.True);
                InvokeTrySave(storeType, primaryPath, recovered, rotateBackup: false);
                Assert.That(InvokeTryLoad(storeType, primaryPath, out object restored, out string restoreError),
                    Is.True, restoreError);
                Assert.That(ReadField<int>(restored, "Gold"), Is.EqualTo(111));
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
        }

        private object CreateProfile(object saveData)
        {
            Type profileType = RuntimeType("WitchTower.Data.PlayerProfile");
            return Activator.CreateInstance(profileType, new[] { saveData });
        }

        private int GetInitialSummonRemainingCount(object profile)
        {
            Type tutorialType = RuntimeType("WitchTower.Data.StoryTutorialService");
            return (int)tutorialType.GetMethod("GetInitialSummonRemainingCount", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { profile });
        }

        private static void InvokeTrySave(Type storeType, string path, object saveData, bool rotateBackup)
        {
            object[] arguments = { path, saveData, null, rotateBackup };
            bool result = (bool)storeType.GetMethod("TrySave")?.Invoke(null, arguments);
            Assert.That(result, Is.True, arguments[2] as string);
        }

        private static bool InvokeTryLoad(Type storeType, string path, out object saveData, out string error)
        {
            object[] arguments = { path, null, null };
            bool result = (bool)storeType.GetMethod("TryLoad")?.Invoke(null, arguments);
            saveData = arguments[1];
            error = arguments[2] as string;
            return result;
        }

        private Type RuntimeType(string fullName)
        {
            Type type = runtimeAssembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, $"Runtime type was not found: {fullName}");
            return type;
        }

        private static T ReadField<T>(object instance, string fieldName)
        {
            return (T)instance.GetType().GetField(fieldName)?.GetValue(instance);
        }

        private static T ReadConstant<T>(Type type, string fieldName, bool nonPublic = false)
        {
            BindingFlags flags = BindingFlags.Static | (nonPublic ? BindingFlags.NonPublic : BindingFlags.Public);
            FieldInfo field = type.GetField(fieldName, flags);
            Assert.That(field, Is.Not.Null, $"Constant was not found: {type.FullName}.{fieldName}");
            return (T)field.GetRawConstantValue();
        }

        private static void WriteField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"Field was not found: {instance.GetType().FullName}.{fieldName}");
            field.SetValue(instance, value);
        }
    }
}
