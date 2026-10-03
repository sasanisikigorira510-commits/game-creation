using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
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
        public void ReleaseBuildUsesUprightPortraitOrientation()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
        }

        [Test]
        public void SafeAreaAnchorMappingKeepsUiInsideNotchedPhoneArea()
        {
            Type safeAreaType = RuntimeType("WitchTower.UI.SafeAreaLayoutController");
            MethodInfo remapMethod = safeAreaType.GetMethod(
                "RemapAnchor",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(remapMethod, Is.Not.Null);

            var safeArea = new Rect(0f, 102f, 1179f, 2352f);
            var screenSize = new Vector2(1179f, 2556f);
            Vector2 bottomLeft = (Vector2)remapMethod.Invoke(
                null,
                new object[] { Vector2.zero, safeArea, screenSize });
            Vector2 topRight = (Vector2)remapMethod.Invoke(
                null,
                new object[] { Vector2.one, safeArea, screenSize });

            Assert.That(bottomLeft.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bottomLeft.y, Is.EqualTo(102f / 2556f).Within(0.0001f));
            Assert.That(topRight.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(topRight.y, Is.EqualTo(2454f / 2556f).Within(0.0001f));
        }

        [Test]
        public void MobileIconSlotsUseFinalBranding()
        {
            Texture2D placeholder = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIcon.png");
            Texture2D finalIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIconFinal.png");
            Texture2D adaptiveForeground =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIconAdaptiveForeground.png");
            Texture2D adaptiveBackground =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/AppIconAdaptiveBackground.png");

            Assert.That(finalIcon, Is.Not.Null);
            Assert.That(adaptiveForeground, Is.Not.Null);
            Assert.That(adaptiveBackground, Is.Not.Null);

            AssertPlatformIcons(
                NamedBuildTarget.Android,
                placeholder,
                finalIcon,
                adaptiveForeground,
                adaptiveBackground);
            AssertPlatformIcons(
                NamedBuildTarget.iOS,
                placeholder,
                finalIcon,
                finalIcon,
                finalIcon);
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
        public void InterruptedOpeningBattleOffersRequiredResumeRouteInsteadOfOptionalHomeHints()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            Type tutorialType = RuntimeType("WitchTower.Data.StoryTutorialService");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object profile = CreateProfile(saveData);
            string firstResultStep = ReadConstant<string>(tutorialType, "StepFirstResult");
            profile.GetType().GetProperty("TutorialStepId")?.SetValue(profile, firstResultStep);
            profile.GetType().GetProperty("HasCompletedTutorial")?.SetValue(profile, false);

            object nextHomeEvent = tutorialType.GetMethod(
                "GetNextEvent",
                BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new[] { profile, (object)"HomeScene" });

            Assert.That(nextHomeEvent, Is.Not.Null,
                "Returning home during the first battle must leave a route back to the required lesson.");
            Assert.That(nextHomeEvent.GetType().GetProperty("StepId")?.GetValue(nextHomeEvent), Is.EqualTo(firstResultStep));
            Assert.That(nextHomeEvent.GetType().GetProperty("TargetKey")?.GetValue(nextHomeEvent), Is.EqualTo("home.battle"),
                "The dex/equipment optional hint must not replace the unfinished first battle.");
            Assert.That(nextHomeEvent.GetType().GetProperty("BlocksInput")?.GetValue(nextHomeEvent), Is.EqualTo(true));
        }

        [Test]
        public void LegacyCompletedFlagDoesNotReopenOpeningTutorialHints()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            Type tutorialType = RuntimeType("WitchTower.Data.StoryTutorialService");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object profile = CreateProfile(saveData);
            string firstResultStep = ReadConstant<string>(tutorialType, "StepFirstResult");
            profile.GetType().GetProperty("TutorialStepId")?.SetValue(profile, firstResultStep);
            profile.GetType().GetProperty("HasCompletedTutorial")?.SetValue(profile, true);

            tutorialType.GetMethod("GetNextEvent", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new[] { profile, (object)"HomeScene" });

            string completeStep = ReadConstant<string>(tutorialType, "CompleteStepId");
            Assert.That(profile.GetType().GetProperty("TutorialStepId")?.GetValue(profile), Is.EqualTo(completeStep));
            Assert.That(profile.GetType().GetProperty("HasCompletedTutorial")?.GetValue(profile), Is.EqualTo(true));
            Assert.That(
                tutorialType.GetMethod("HasSeenHint", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new[] { profile, (object)"tutorial_equipment_enhance" }),
                Is.EqualTo(true));
        }

        [Test]
        public void OpeningTutorialBattleCannotAddRecruitmentMonsters()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            Type tutorialType = RuntimeType("WitchTower.Data.StoryTutorialService");
            Type recruitType = RuntimeType("WitchTower.Battle.MonsterRecruitService");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object profile = CreateProfile(saveData);
            string firstBattleStep = ReadConstant<string>(tutorialType, "StepFirstBattle");
            profile.GetType().GetProperty("TutorialStepId")?.SetValue(profile, firstBattleStep);
            profile.GetType().GetProperty("HasCompletedTutorial")?.SetValue(profile, false);

            bool canRecruit = (bool)recruitType.GetMethod(
                "CanAttemptRecruitThisBattle",
                BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new[] { profile });

            Assert.That(canRecruit, Is.False,
                "The first tutorial battle must not add a fourth or fifth monster to the three-summon roster.");
        }

        [Test]
        public void ReleaseBuildFeatureFlagsMatchConfiguredMonetization()
        {
            Type monetizationType = RuntimeType("WitchTower.Monetization.MonetizationFeatureFlags");
            Type bootstrapType = RuntimeType("WitchTower.Data.PrototypePartyBootstrapService");

#if WITCHTOWER_IAP_ENABLED
            Assert.That(ReadConstant<bool>(monetizationType, "StorefrontEnabled"), Is.True);
#else
            Assert.That(ReadConstant<bool>(monetizationType, "StorefrontEnabled"), Is.False);
#endif
#if WITCHTOWER_ADS_ENABLED
            Assert.That(ReadConstant<bool>(monetizationType, "AdsEnabled"), Is.True);
            Type adMobConfigurationType = RuntimeType("WitchTower.Monetization.AdMobConfiguration");
            Assert.That(
                adMobConfigurationType.GetProperty(
                    "HasProductionIosConfiguration",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null),
                Is.True);
#else
            Assert.That(ReadConstant<bool>(monetizationType, "AdsEnabled"), Is.False);
#endif
            Assert.That(ReadConstant<bool>(bootstrapType, "UnlockAllImplementedMonstersForPreview", nonPublic: true), Is.False);
        }

        [Test]
        public void PaidStonePurchaseIsGrantedOnlyOncePerTransaction()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            object profile = CreateProfile(saveData);
            MethodInfo grant = profile.GetType().GetMethod(
                "TryGrantPaidStonePurchase",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(grant, Is.Not.Null);

            bool firstGrant = (bool)grant.Invoke(
                profile,
                new object[] { "com.nasus.dungeonmonsterroguelike.crystals120", 120, "transaction-1" });
            bool duplicateGrant = (bool)grant.Invoke(
                profile,
                new object[] { "com.nasus.dungeonmonsterroguelike.crystals120", 120, "transaction-1" });

            Assert.That(firstGrant, Is.True);
            Assert.That(duplicateGrant, Is.False);
            Assert.That(profile.GetType().GetProperty("PaidGachaStones")?.GetValue(profile), Is.EqualTo(120));
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
        public void OpeningTutorialBootstrapPreservesGenuinelyOwnedMonsters()
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            Type ownedMonsterType = RuntimeType("WitchTower.Save.OwnedMonsterData");
            Type bootstrapType = RuntimeType("WitchTower.Data.PrototypePartyBootstrapService");
            object saveData = saveType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            WriteField(saveData, "InitialTutorialSummonCount", 3);
            IList ownedMonsters = (IList)saveType.GetField("OwnedMonsters")?.GetValue(saveData);

            AddOwnedMonster(ownedMonsters, ownedMonsterType, "legacy-dragon", "monster_dragon_whelp", 1);
            AddOwnedMonster(ownedMonsters, ownedMonsterType, "legacy-chibi", "monster_chibi_gear", 2);
            AddOwnedMonster(ownedMonsters, ownedMonsterType, "summon-dragon", "monster_dragon_whelp", 3);
            AddOwnedMonster(ownedMonsters, ownedMonsterType, "summon-golem", "monster_rock_golem", 4);
            AddOwnedMonster(ownedMonsters, ownedMonsterType, "summon-mage", "monster_apprentice_mage", 5);

            object profile = CreateProfile(saveData);
            Type managerType = RuntimeType("WitchTower.Managers.MasterDataManager");
            FieldInfo instanceField = managerType.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            object previousManager = instanceField.GetValue(null);
            var managerObject = new GameObject("MasterDataManager-OwnedRosterTest");
            try
            {
                Component manager = managerObject.AddComponent(managerType);
                instanceField.SetValue(null, manager);
                managerType.GetMethod("Initialize").Invoke(manager, null);
                bootstrapType.GetMethod("EnsureParty").Invoke(null, new[] { profile, (object)5 });
                IList retained = (IList)profile.GetType().GetProperty("OwnedMonsters").GetValue(profile);
                Assert.That(retained.Cast<object>().Select(m => ReadField<string>(m, "InstanceId")),
                    Is.EquivalentTo(new[] { "legacy-dragon", "legacy-chibi", "summon-dragon", "summon-golem", "summon-mage" }),
                    "Rendering preview cards is not a reason to delete genuine saved monsters.");
            }
            finally
            {
                instanceField.SetValue(null, previousManager);
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

        private static void AssertPlatformIcons(
            NamedBuildTarget target,
            Texture2D placeholder,
            Texture2D finalIcon,
            Texture2D adaptiveForeground,
            Texture2D adaptiveBackground)
        {
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
                Assert.That(icons, Is.Not.Empty, $"{target.TargetName}/{kind} has no icon slots.");
                foreach (PlatformIcon icon in icons)
                {
                    Texture2D[] textures = icon.GetTextures();
                    Assert.That(textures, Is.Not.Empty, $"{target.TargetName}/{kind} has an empty icon.");
                    Assert.That(textures, Has.None.Null);
                    Assert.That(textures, Has.None.EqualTo(placeholder));

                    if (target == NamedBuildTarget.Android && icon.maxLayerCount >= 2)
                    {
                        Assert.That(textures[0], Is.EqualTo(adaptiveBackground));
                        Assert.That(textures[1], Is.EqualTo(adaptiveForeground));
                    }
                    else
                    {
                        Assert.That(textures, Has.All.EqualTo(finalIcon));
                    }
                }
            }
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

        private static void AddOwnedMonster(IList list, Type ownedMonsterType, string instanceId, string monsterId, int acquiredOrder)
        {
            object monster = Activator.CreateInstance(ownedMonsterType);
            WriteField(monster, "InstanceId", instanceId);
            WriteField(monster, "MonsterId", monsterId);
            WriteField(monster, "Level", 1);
            WriteField(monster, "AcquiredOrder", acquiredOrder);
            list.Add(monster);
        }
    }
}
