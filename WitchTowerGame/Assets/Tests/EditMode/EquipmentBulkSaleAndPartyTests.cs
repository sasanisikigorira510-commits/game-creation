using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class EquipmentBulkSaleAndPartyTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
        private static object Field(object owner, string name) => owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
        private static object Property(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name).SetValue(owner, value);
        private static object Value(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);
        private object profile;
        private Component controller;
        private GameObject canvas;
        private GameObject fixtureRoot;
        private object oldGame, oldMaster, oldSaveManager;
        private UnityEngine.Random.State randomState;
        private Type gameType, masterType, saveManagerType, service;
        private IList Inventory => (IList)Property(profile, "OwnedEquipments");

        [SetUp]
        public void SetUp()
        {
            randomState = UnityEngine.Random.state;
            gameType = T("WitchTower.Managers.GameManager");
            masterType = T("WitchTower.Managers.MasterDataManager");
            saveManagerType = T("WitchTower.Managers.SaveManager");
            service = T("WitchTower.Data.EquipmentBulkSaleService");
            oldGame = gameType.GetProperty("Instance").GetValue(null);
            oldMaster = masterType.GetProperty("Instance").GetValue(null);
            oldSaveManager = saveManagerType.GetProperty("Instance").GetValue(null);
            try
            {
            // These are in-memory UI/domain tests, not a storage session.
            // Never inherit a real or blocked manager from the open Editor scene.
            saveManagerType.GetProperty("Instance").SetValue(null, null);
            fixtureRoot = new GameObject("EquipmentBulkTestFixture");
            canvas = new GameObject("EquipmentBulkTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(fixtureRoot.transform, false);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, 2341f);
            var owner = new GameObject("EquipmentBulkTestOwner"); owner.SetActive(false);
            owner.transform.SetParent(canvas.transform, false);
            var game = owner.AddComponent(gameType); gameType.GetProperty("Instance").SetValue(null, game);
            var master = owner.AddComponent(masterType); masterType.GetProperty("Instance").SetValue(null, master);
            masterType.GetMethod("Initialize").Invoke(master, null);
            object save = T("WitchTower.Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
            Set(save, "HasCompletedTutorial", true);
            profile = Activator.CreateInstance(T("WitchTower.Data.PlayerProfile"), new[] { save });
            gameType.GetProperty("PlayerProfile").SetValue(game, profile);
            var hints = (IList)Property(profile, "SeenTutorialHintIds");
            var stories = (IList)Property(profile, "SeenStoryEventIds");
            foreach (var field in T("WitchTower.Data.StoryTutorialService").GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.FieldType == typeof(string) && field.IsLiteral)
                {
                    if (field.Name.StartsWith("Hint")) hints.Add(field.GetRawConstantValue());
                    if (field.Name.StartsWith("Story")) stories.Add(field.GetRawConstantValue());
                }
            controller = owner.AddComponent(T("WitchTower.Core.TitleSceneController"));
            }
            catch
            {
                TearDown();
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
            gameType?.GetProperty("Instance").SetValue(null, oldGame);
            masterType?.GetProperty("Instance").SetValue(null, oldMaster);
            saveManagerType?.GetProperty("Instance").SetValue(null, oldSaveManager);
            UnityEngine.Random.state = randomState;
        }

        private object AddEquipment(string id, int quality = 1)
        {
            object equipment = Activator.CreateInstance(T("WitchTower.Save.OwnedEquipmentData"));
            Set(equipment, "InstanceId", id); Set(equipment, "EquipmentId", "equip_apprentice_charm");
            Set(equipment, "QualityRank", quality); Set(equipment, "RemainingEnhanceAttempts", 5); Set(equipment, "MaxEnhanceAttempts", 5);
            Inventory.Add(equipment);
            return equipment;
        }

        private object Preview(params string[] ids)
        {
            object[] args = { profile, ids, null, null };
            Assert.That(service.GetMethod("TryCreatePreview").Invoke(null, args), Is.True, args[3]?.ToString());
            return args[2];
        }

        private bool Commit(object preview, Func<bool> persist = null)
        {
            object[] args = { profile, preview, persist, null };
            return (bool)service.GetMethod("TryCommitWithPersistence").Invoke(null, args);
        }

        [Test]
        public void PreviewDoesNotMutateAndCommitSellsOnlyExactInstancesAtExistingQualityPrices()
        {
            AddEquipment("common", 1); AddEquipment("uncommon", 2); AddEquipment("leave", 5);
            object preview = Preview("common", "uncommon");
            Assert.That(Property(preview, "Gold"), Is.EqualTo(85));
            Assert.That(Inventory.Count, Is.EqualTo(3));
            Assert.That(Property(profile, "Gold"), Is.EqualTo(100));
            AddEquipment("later-added", 1);
            Assert.That(Commit(preview), Is.True);
            Assert.That(Inventory.Cast<object>().Select(x => Value(x, "InstanceId")), Is.EquivalentTo(new[] { "leave", "later-added" }));
            Assert.That(Property(profile, "Gold"), Is.EqualTo(185));
            Assert.That(Commit(preview), Is.False, "A review cannot be replayed.");
        }

        [TestCase("IsLocked")]
        [TestCase("IsEquipped")]
        [TestCase("EquippedMonsterInstanceId")]
        [TestCase("tutorial")]
        [TestCase("slotReference")]
        [TestCase("missingMaster")]
        public void ProtectedEquipmentCannotBeSelectedOrSold(string protection)
        {
            object equipment = AddEquipment(protection == "tutorial" ? "tutorial_gift_equipment_quality_uncommon_test" : "protected");
            if (protection == "IsLocked" || protection == "IsEquipped") Set(equipment, protection, true);
            if (protection == "EquippedMonsterInstanceId") Set(equipment, protection, "another-monster");
            if (protection == "missingMaster") Set(equipment, "EquipmentId", "missing_equipment");
            if (protection == "slotReference")
            {
                object monster = Activator.CreateInstance(T("WitchTower.Save.OwnedMonsterData"));
                Set(monster, "InstanceId", "monster"); Set(monster, "EquippedAccessoryInstanceId", "protected");
                ((IList)Property(profile, "OwnedMonsters")).Add(monster);
            }
            object[] args = { profile, new[] { (string)Value(equipment, "InstanceId") }, null, null };
            Assert.That(service.GetMethod("TryCreatePreview").Invoke(null, args), Is.False);
            Assert.That(Inventory.Count, Is.EqualTo(1));
            Assert.That(Property(profile, "Gold"), Is.EqualTo(100));
        }

        [TestCase("lock")]
        [TestCase("equipped")]
        [TestCase("quality")]
        [TestCase("enhancement")]
        [TestCase("missing")]
        [TestCase("epoch")]
        public void ChangedSelectionRejectsWholeSaleWithoutPartialRemoval(string change)
        {
            object first = AddEquipment("first"); object second = AddEquipment("second");
            object preview = Preview("first", "second");
            if (change == "lock") Set(second, "IsLocked", true);
            if (change == "equipped") Set(second, "EquippedMonsterInstanceId", "friend");
            if (change == "quality") Set(second, "QualityRank", 2);
            if (change == "enhancement") Set(second, "UpgradeLevel", 1);
            if (change == "missing") Inventory.Remove(second);
            if (change == "epoch") profile.GetType().GetProperty("RecoveryEpoch").SetValue(profile, 1);
            Assert.That(Commit(preview), Is.False);
            Assert.That(Inventory.Contains(first), Is.True);
            Assert.That(Property(profile, "Gold"), Is.EqualTo(100));
        }

        [Test]
        public void DuplicateSelectedOrOwnedIdsAndWalletOverflowCannotSell()
        {
            AddEquipment("duplicate");
            object[] args = { profile, new[] { "duplicate", "duplicate" }, null, null };
            Assert.That(service.GetMethod("TryCreatePreview").Invoke(null, args), Is.False);
            object preview = Preview("duplicate");
            AddEquipment("duplicate");
            Assert.That(Commit(preview), Is.False);
            Inventory.RemoveAt(1);
            profile.GetType().GetProperty("Gold").SetValue(profile, int.MaxValue - 24);
            args = new object[] { profile, new[] { "duplicate" }, null, null };
            Assert.That(service.GetMethod("TryCreatePreview").Invoke(null, args), Is.False);
            Assert.That(Commit(preview), Is.False);
            Assert.That(Inventory.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PersistenceFailureRestoresExactInventoryOrderAndGold(bool throws)
        {
            AddEquipment("before"); AddEquipment("sell"); AddEquipment("after");
            object[] original = Inventory.Cast<object>().ToArray();
            object preview = Preview("sell");
            Assert.That(Commit(preview, () => { if (throws) throw new InvalidOperationException("synthetic"); return false; }), Is.False);
            Assert.That(Inventory.Cast<object>(), Is.EqualTo(original));
            Assert.That(Property(profile, "Gold"), Is.EqualTo(100));
        }

        [Test]
        public void LegacyRepresentativeAssignmentIsProtectedEvenWithoutOwnedMonsters()
        {
            object equipment = AddEquipment("legacy-assigned");
            profile.GetType().GetField("legacyEquippedAccessoryId", Hidden).SetValue(profile, "equip_apprentice_charm");
            Assert.That(Property(profile, "OwnedMonsters"), Is.Empty);
            Assert.That(profile.GetType().GetMethod("GetEquippedAccessory").Invoke(profile, null), Is.SameAs(equipment));
            object[] args = { profile, equipment, null };
            Assert.That(service.GetMethod("CanSell").Invoke(null, args), Is.False);
            Assert.That(args[2], Is.EqualTo("装備中"));
            args = new object[] { profile, new[] { "legacy-assigned" }, null, null };
            Assert.That(service.GetMethod("TryCreatePreview").Invoke(null, args), Is.False);
            Assert.That(Inventory.Contains(equipment), Is.True);
        }

        [Test]
        public void FailedRealSaveProjectionRestoresNormalizedFlagsAndLegacyAssignments()
        {
            AddEquipment("sell");
            object inconsistent = AddEquipment("unsold-inconsistent"); Set(inconsistent, "IsEquipped", true);
            object assigned = AddEquipment("assigned"); Set(assigned, "EquippedMonsterInstanceId", "friend");
            object monster = Activator.CreateInstance(T("WitchTower.Save.OwnedMonsterData"));
            Set(monster, "InstanceId", "friend"); Set(monster, "EquippedAccessoryInstanceId", "assigned");
            ((IList)Property(profile, "OwnedMonsters")).Add(monster);
            FieldInfo legacy = profile.GetType().GetField("legacyEquippedAccessoryId", Hidden);
            legacy.SetValue(profile, "legacy-before-projection");
            object preview = Preview("sell");
            object[] original = Inventory.Cast<object>().ToArray();
            bool normalizationObserved = false;
            bool result = Commit(preview, () =>
            {
                profile.GetType().GetMethod("ToSaveData").Invoke(profile, new object[] { 1 });
                normalizationObserved = !(bool)Value(inconsistent, "IsEquipped") && (bool)Value(assigned, "IsEquipped") &&
                    (string)legacy.GetValue(profile) == "equip_apprentice_charm";
                return false;
            });
            Assert.That(normalizationObserved, Is.True, "Exercise actual ToSaveData normalization, not just a fake failing sink.");
            Assert.That(result, Is.False);
            Assert.That(Inventory.Cast<object>(), Is.EqualTo(original));
            Assert.That(Value(inconsistent, "IsEquipped"), Is.True);
            Assert.That(Value(assigned, "IsEquipped"), Is.False);
            Assert.That(Value(assigned, "EquippedMonsterInstanceId"), Is.EqualTo("friend"));
            Assert.That(Value(monster, "EquippedAccessoryInstanceId"), Is.EqualTo("assigned"));
            Assert.That(legacy.GetValue(profile), Is.EqualTo("legacy-before-projection"));
            Assert.That(Property(profile, "Gold"), Is.EqualTo(100));
        }

        [TestCase(1920f)]
        [TestCase(2341f)]
        public void BulkUiFiltersSelectionRequiresReviewAndCancelClearsSelection(float height)
        {
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080f, height);
            AddEquipment("sell-common"); AddEquipment("sell-uncommon", 2);
            object protectedItem = AddEquipment("keep-locked"); Set(protectedItem, "IsLocked", true);
            object weapon = AddEquipment("not-visible"); Set(weapon, "EquipmentId", "equip_rusty_sword");
            Call(controller, "EnsureEquipmentScene");
            var filter = controller.GetType().GetField("equipmentInventoryFilter", Hidden);
            filter.SetValue(controller, Enum.Parse(filter.FieldType, "Accessory"));
            Call(controller, "OpenEquipmentBulkSale");
            Call(controller, "SelectVisibleEquipmentForBulkSale");
            Assert.That(((Text)Field(controller, "equipmentBulkSaleSummary")).text, Does.Contain("2個").And.Contain("85"));
            Capture("bulk-selection-" + height, height);
            Call(controller, "ReviewOrCommitEquipmentBulkSale");
            Assert.That(Field(controller, "equipmentBulkSalePreview"), Is.Not.Null);
            Assert.That(Inventory.Count, Is.EqualTo(4), "Review is not a sale.");
            Assert.That(((Text)Field(controller, "equipmentBulkSaleMessage")).text, Does.Contain("元に戻せません"));
            Capture("bulk-confirmation-" + height, height);
            Call(controller, "CancelEquipmentBulkSale");
            Assert.That(Inventory.Count, Is.EqualTo(4));
            Call(controller, "OpenEquipmentBulkSale");
            Assert.That(((Text)Field(controller, "equipmentBulkSaleSummary")).text, Does.Contain("0個"));
            Assert.That(((Button)Field(controller, "equipmentBulkSaleConfirmButton")).interactable, Is.False);
        }

        [Test]
        public void PartyLabelsDistinguishSameSpeciesByInstanceAndBadgeDoesNotInterceptSelection()
        {
            object party = Activator.CreateInstance(T("WitchTower.Save.OwnedMonsterData"));
            object bench = Activator.CreateInstance(T("WitchTower.Save.OwnedMonsterData"));
            Set(party, "InstanceId", "party-instance"); Set(bench, "InstanceId", "bench-instance");
            Set(party, "MonsterId", "monster_apprentice_mage"); Set(bench, "MonsterId", "monster_apprentice_mage");
            ((IList)Property(profile, "OwnedMonsters")).Add(party); ((IList)Property(profile, "OwnedMonsters")).Add(bench);
            var slots = (IList)Property(profile, "PartyMonsterInstanceIds"); if (slots.Count == 0) slots.Add("party-instance"); else slots[0] = "party-instance";
            Call(controller, "EnsureEquipmentScene"); Call(controller, "RefreshEquipmentScene");
            Assert.That(((Text)Field(controller, "equipmentMonsterPartyText")).text, Does.Contain("編成中：前衛 1"));
            Call(controller, "OpenEquipmentMonsterPicker");
            var list = (RectTransform)Field(controller, "equipmentMonsterPickerListRect");
            var badges = list.GetComponentsInChildren<Text>(true).Where(t => t.name == "PartyBadge").ToArray();
            Assert.That(badges.Length, Is.EqualTo(1));
            Assert.That(badges[0].text, Is.EqualTo("編成中：前衛 1"));
            Assert.That(badges[0].raycastTarget, Is.False);
            Capture("equipment-party-badge", 2341f);
            Call(controller, "SelectEquipmentMonsterFromPicker", "bench-instance");
            Assert.That(((Text)Field(controller, "equipmentMonsterPartyText")).text, Does.StartWith("編成外"));
            Assert.That(Field(controller, "selectedEquipmentMonsterInstanceId"), Is.EqualTo("bench-instance"));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void QualityFilterUsesOwnedQualityWithInclusiveUpperLimit(int maximum)
        {
            for (int rank=1; rank<=5; rank++)
            {
                var equipment=AddEquipment("quality-"+rank,rank);
                Assert.That(service.GetMethod("MatchesFilter").Invoke(null,new object[]{equipment,maximum,true,false}),Is.EqualTo(rank<=maximum));
            }
        }

        [TestCase(1920f)] [TestCase(2341f)]
        public void CombinedFiltersClearOldSelectionAndReviewSellsOnlyVisibleMatches(float height)
        {
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1080,height);
            AddEquipment("common",1); AddEquipment("rare",3); AddEquipment("epic",4);
            var favorite=AddEquipment("favorite",2); Set(favorite,"IsFavorite",true);
            var upgraded=AddEquipment("upgraded",2); Set(upgraded,"UpgradeLevel",1);
            var locked=AddEquipment("locked",1); Set(locked,"IsLocked",true);
            Call(controller,"EnsureEquipmentScene"); Call(controller,"OpenEquipmentBulkSale");
            Call(controller,"SelectVisibleEquipmentForBulkSale");
            Assert.That(((IEnumerable)Field(controller,"equipmentBulkSaleSelection")).Cast<string>(),Does.Not.Contain("favorite"));
            for(int i=0;i<3;i++) Call(controller,"CycleEquipmentBulkSaleFilter",0);
            Assert.That(Field(controller,"equipmentBulkSaleMaxQuality"),Is.EqualTo(3));
            Assert.That(((IEnumerable)Field(controller,"equipmentBulkSaleSelection")).Cast<string>(),Is.Empty);
            Call(controller,"CycleEquipmentBulkSaleFilter",2);
            Call(controller,"SelectVisibleEquipmentForBulkSale");
            Assert.That(((IEnumerable)Field(controller,"equipmentBulkSaleSelection")).Cast<string>(),Is.EquivalentTo(new[]{"common","rare"}));
            Capture("bulk-rare-exclude-favorites-"+height,height);
            Call(controller,"ReviewOrCommitEquipmentBulkSale");
            var buttons=((IEnumerable)Field(controller,"equipmentBulkSaleFilterButtons")).Cast<Button>().ToArray();
            Assert.That(buttons.All(b=>!b.interactable),Is.True);
            Call(controller,"CycleEquipmentBulkSaleFilter",1);
            Assert.That(Field(controller,"equipmentBulkSaleExcludeFavorites"),Is.True);
            Call(controller,"ReviewOrCommitEquipmentBulkSale");
            Assert.That(Inventory.Cast<object>().Select(x=>Value(x,"InstanceId")),Is.EquivalentTo(new[]{"epic","favorite","upgraded","locked"}));
        }

        [Test]
        public void FavoriteTogglePersistsAndIncludingFavoritesRequiresExplicitReview()
        {
            var equipment=AddEquipment("favorite");
            Call(controller,"EnsureEquipmentScene"); Call(controller,"ShowEquipmentDetailSheet","favorite");
            ((Button)Field(controller,"equipmentDetailFavoriteButton")).onClick.Invoke();
            Assert.That(Value(equipment,"IsFavorite"),Is.True);
            Capture("equipment-favorite",2341f);
            var save=profile.GetType().GetMethod("ToSaveData").Invoke(profile,new object[]{1});
            var loaded=JsonUtility.FromJson(JsonUtility.ToJson(save),save.GetType());
            var restored=Activator.CreateInstance(profile.GetType(),new[]{loaded});
            Assert.That(Value(((IList)Property(restored,"OwnedEquipments"))[0],"IsFavorite"),Is.True);
            Call(controller,"OpenEquipmentBulkSale"); Call(controller,"SelectVisibleEquipmentForBulkSale");
            Assert.That(((IEnumerable)Field(controller,"equipmentBulkSaleSelection")).Cast<string>(),Is.Empty);
            Call(controller,"CycleEquipmentBulkSaleFilter",1); Call(controller,"SelectVisibleEquipmentForBulkSale");
            Call(controller,"ReviewOrCommitEquipmentBulkSale");
            Assert.That(((Text)Field(controller,"equipmentBulkSaleMessage")).text,Does.Contain("お気に入りの装備が含まれています"));
            Assert.That(Inventory.Count,Is.EqualTo(1));
            Set(equipment,"IsFavorite",false);
            Assert.That(Commit(Field(controller,"equipmentBulkSalePreview")),Is.False,"Changed favorite status invalidates a review.");
        }

        [Test]
        public void UnenhancedFilterAlsoExcludesFailedAttemptsAndLegacyBonuses()
        {
            var equipment=AddEquipment("attempted");
            Set(equipment,"RemainingEnhanceAttempts",4);
            Assert.That(service.GetMethod("MatchesFilter").Invoke(null,new object[]{equipment,5,true,true}),Is.False);
            Set(equipment,"RemainingEnhanceAttempts",5); Set(equipment,"EnhancementBonusRate",0.05f);
            Assert.That(service.GetMethod("MatchesFilter").Invoke(null,new object[]{equipment,5,true,true}),Is.False);
        }

        [Test]
        public void GuaranteedQualityPairHasAcknowledgementThenManualCommonEquipAndCannotBeBulkSold()
        {
            profile.GetType().GetProperty("HasCompletedTutorial").SetValue(profile, false);
            profile.GetType().GetProperty("TutorialStepId").SetValue(profile, "T07B");
            profile.GetType().GetProperty("InitialTutorialSummonCount").SetValue(profile, 3);
            var hints = (IList)Property(profile, "SeenTutorialHintIds"); hints.Clear();
            hints.Add("tutorial_equipment_quality_pair_received"); hints.Add("tutorial_equipment_gift_received");
            ((IList)Property(profile, "SeenStoryEventIds")).Clear();
            object common = AddEquipment("tutorial_gift_equipment_quality_common_test", 1);
            AddEquipment("tutorial_gift_equipment_quality_uncommon_test", 2);
            object monster = Activator.CreateInstance(T("WitchTower.Save.OwnedMonsterData"));
            Set(monster, "InstanceId", "friend"); Set(monster, "MonsterId", "monster_apprentice_mage");
            Set(monster, "Level", 1); ((IList)Property(profile, "OwnedMonsters")).Add(monster);
            Call(controller, "EnsureEquipmentScene"); Call(controller, "RefreshEquipmentScene");
            Button acknowledge = (Button)Field(controller, "equipmentQualityAcknowledgeButton");
            Assert.That(acknowledge.gameObject.activeSelf, Is.True);
            Assert.That(((Text)Field(controller, "equipmentTutorialGuideBodyText")).text, Does.Contain("コモンとアンコモン"));
            Assert.That(((Button)Field(controller, "equipmentAutoEquipButton")).interactable, Is.False);
            Assert.That((bool)service.GetMethod("CanSell").Invoke(null, new object[] { profile, common, null }), Is.False);
            Capture("equipment-quality-pair", 2341f);
            acknowledge.onClick.Invoke();
            Assert.That(hints.Contains("tutorial_equipment_quality"), Is.True);
            Assert.That(acknowledge.gameObject.activeSelf, Is.False);
            Assert.That(((Text)Field(controller, "equipmentTutorialGuideTitleText")).text, Is.EqualTo("ルシェの装備レッスン"));
            var inventory = (RectTransform)Field(controller, "equipmentInventoryContentRect");
            Assert.That(inventory.Find("EquipmentCard_0/Quality").GetComponent<Text>().text, Does.Contain("コモン"));
        }

        private void Capture(string label, float height)
        {
            string directory = Environment.GetEnvironmentVariable("EQUIPMENT_BULK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((GameObject)Field(controller, "equipmentSceneRoot")).SetActive(true);
            Canvas.ForceUpdateCanvases();
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { (RectTransform)canvas.transform, 1179, Mathf.RoundToInt(height * 1179f / 1080f), System.IO.Path.Combine(directory, label + ".png") });
        }
    }
}
