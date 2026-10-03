using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class PermanentShopAndRatesTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private SceneSetup[] scenes;
        private object oldGame, oldSave, oldOnline, oldGateway, oldRootOverride, profile;
        private PropertyInfo saveRootOverride;
        private bool globalsCaptured, sceneChanged;
        private string directory;
        private Component saves;
        private RectTransform canvas;
        private Component shop;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType()
            .GetMethod(name, Hidden | BindingFlags.Public).Invoke(obj, args);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
        private static void SetProp(object obj, string name, object value) => obj.GetType().GetProperty(name).SetValue(obj, value);
        private object Prop(string name) => profile.GetType().GetProperty(name).GetValue(profile);
        private static void Singleton(Type type, object value) => type.GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private Button Buy(string field) => (Button)Field(shop, field);
        private GameObject Confirmation => (GameObject)Field(shop, "purchaseConfirmationRoot");

        [SetUp]
        public void Setup()
        {
            try
            {
                scenes = EditorSceneManager.GetSceneManagerSetup();
                oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
                oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
                oldOnline = T("Save.OnlinePlayerData").GetProperty("Instance").GetValue(null);
                oldGateway = T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                saveRootOverride = T("Managers.SaveManager").GetProperty("EditorSaveDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
                oldRootOverride = saveRootOverride.GetValue(null);
                globalsCaptured = true;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                sceneChanged = true;

                // The presentation gateway commits through the real save path.
                // Use one immutable, fixture-owned root and inactive services;
                // no player's save is loaded and no HTTP/StoreKit can run.
                directory = Path.Combine(Path.GetTempPath(), "WitchTower-PermanentShop-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                saveRootOverride.SetValue(null, directory);
                var saveOwner = new GameObject("IsolatedPermanentShopSave"); saveOwner.SetActive(false);
                saves = saveOwner.AddComponent(T("Managers.SaveManager"));
                Singleton(saves.GetType(), saves);
                Assert.That(saves.GetType().GetProperty("RootDirectory").GetValue(saves), Is.EqualTo(directory));
                Assert.That(saves.GetType().GetProperty("StorageAccessAvailable").GetValue(saves), Is.True);

                var owner = new GameObject("IsolatedGame"); owner.SetActive(false);
                var game = owner.AddComponent(T("Managers.GameManager"));
                Singleton(game.GetType(), game);
                var save = T("Save.PlayerSaveData").GetMethod("CreateDefault").Invoke(null, null);
                profile = Activator.CreateInstance(T("Data.PlayerProfile"), new[] { save });
                SetProp(game, "PlayerProfile", profile);
                SetProp(profile, "PaidGachaStones", 10000);
                SetProp(profile, "MonsterStorageLimit", 100);
                SetProp(profile, "EquipmentStorageLimit", 100);
                SetProp(saves, "CurrentSaveData", Call(profile, "ToSaveData", 1));

                var onlineOwner = new GameObject("IsolatedPermanentShopOnline"); onlineOwner.SetActive(false);
                var online = onlineOwner.AddComponent(T("Save.OnlinePlayerData"));
                Singleton(online.GetType(), online);
                online.GetType().GetField("configuredStorageOwner", Hidden).SetValue(online, saves);
                OnlineUiTestGateway.Install();
                var root = new GameObject("PurchaseTestCanvas", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvas = (RectTransform)root.transform;
                canvas.sizeDelta = new Vector2(1080, 2341);
                shop = Panel("Home.PaidShopPanelController", "PaidShop");
                Call(shop, "OpenPermanentUpgradeShop");
            }
            catch { Cleanup(); throw; }
        }

        private Component Panel(string type, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false); go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var controller = go.AddComponent(T(type));
            ((Behaviour)controller).enabled = false;
            Call(controller, "Build");
            go.SetActive(true);
            return controller;
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                if (shop != null) Call(shop, "OnDisable");
            }
            finally
            {
                try
                {
                    if (sceneChanged) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    shop = saves = null;
                    canvas = null;
                    profile = null;
                    if (globalsCaptured)
                    {
                        try
                        {
                            Singleton(T("Managers.GameManager"), oldGame);
                            Singleton(T("Managers.SaveManager"), oldSave);
                            Singleton(T("Save.OnlinePlayerData"), oldOnline);
                            T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, oldGateway);
                        }
                        finally
                        {
                            saveRootOverride.SetValue(null, oldRootOverride);
                            globalsCaptured = false;
                        }
                    }
                    try
                    {
                        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true); // Only this test's GUID root.
                    }
                    finally
                    {
                        directory = null;
                        if (sceneChanged && scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path)))
                            EditorSceneManager.RestoreSceneManagerSetup(scenes);
                        sceneChanged = false;
                    }
                }
            }
        }

        [Test]
        public void RefundDeficitDisablesEveryPaidUpgradeEvenWithEnoughGems()
        {
            Type online = T("Save.OnlinePlayerData");
            object previous = online.GetProperty("Instance").GetValue(null);
            var owner = new GameObject("RefundUiTest"); owner.SetActive(false);
            var service = owner.AddComponent(online);
            try
            {
                Singleton(online, service);
                online.GetField("configuredStorageOwner", Hidden).SetValue(service, saves);
                online.GetField("refundDebt", Hidden).SetValue(service, 300);
                Call(shop, "Refresh");
                foreach (string field in new[] { "autoRepeatFloorUpgradeButton", "autoSellEquipmentUpgradeButton",
                    "autoReleaseMonsterUpgradeButton", "monsterStorageUpgradeButton", "equipmentStorageUpgradeButton" })
                    Assert.That(Buy(field).interactable, Is.False, field);
                online.GetField("refundDebt", Hidden).SetValue(service, 0);
                Call(shop, "Refresh");
                Assert.That(Buy("monsterStorageUpgradeButton").interactable, Is.True);
                Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
            }
            finally { Singleton(online, previous); UnityEngine.Object.DestroyImmediate(owner); }
        }

        [TestCase("autoRepeatFloorUpgradeButton", 1200, "HasAutoRepeatFloorUpgrade", true)]
        [TestCase("autoSellEquipmentUpgradeButton", 1200, "HasAutoSellEquipmentUpgrade", true)]
        [TestCase("autoReleaseMonsterUpgradeButton", 1200, "HasAutoReleaseMonsterUpgrade", true)]
        [TestCase("monsterStorageUpgradeButton", 1500, "MonsterStorageLimit", 120)]
        [TestCase("equipmentStorageUpgradeButton", 1500, "EquipmentStorageLimit", 120)]
        public void EveryPermanentPurchaseRequiresSeparateConfirmation(string field, int cost, string property, object expected)
        {
            var before = Prop(property);
            Buy(field).onClick.Invoke();
            Assert.That(Confirmation, Is.Not.Null);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
            Assert.That(Prop(property), Is.EqualTo(before));
            Assert.That(Confirmation.GetComponent<Image>().raycastTarget, Is.True);
            var panel = Confirmation.transform.Find("ConfirmationPanel");
            Assert.That(panel.Find("Cost").GetComponent<Text>().text, Does.Contain(cost.ToString("N0")));
            panel.Find("CancelPurchase").GetComponent<Button>().onClick.Invoke();
            Assert.That(Confirmation, Is.Null);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
            Buy(field).onClick.Invoke();
            Call(shop, "ConfirmPermanentPurchase");
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000 - cost));
            Assert.That(Prop(property), Is.EqualTo(expected));
            var tutorial = shop.transform.Find("PermanentUpgradeTutorial");
            Assert.That(tutorial, Is.Not.Null, "A committed upgrade explains how to use it.");
            var body = tutorial.Find("Panel/Body").GetComponent<Text>();
            Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height));
            Assert.That(body.text, Does.Contain("設定"));
            if (field == "autoReleaseMonsterUpgradeButton") Assert.That(body.text, Does.Contain("平均個体値"));
            string capture = Environment.GetEnvironmentVariable("WITCHTOWER_UPGRADE_CAPTURE_PATH");
            if (field == "autoRepeatFloorUpgradeButton" && !string.IsNullOrEmpty(capture))
                typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] {canvas, 1080, 2341, capture});
            Call(shop, "ConfirmPermanentPurchase");
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000 - cost), "Double confirmation must not spend twice, including repeatable capacity upgrades.");
        }

        [Test]
        public void ConfirmationRechecksBalanceAndNavigationCancelsPendingPurchase()
        {
            Buy("monsterStorageUpgradeButton").onClick.Invoke();
            SetProp(profile, "PaidGachaStones", 0);
            Call(shop, "ConfirmPermanentPurchase");
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(0));
            Assert.That(Prop("MonsterStorageLimit"), Is.EqualTo(100));
            Assert.That(shop.transform.Find("PermanentUpgradeTutorial"), Is.Null, "Failed purchases must not show a success tutorial.");
            SetProp(profile, "PaidGachaStones", 10000);
            Buy("monsterStorageUpgradeButton").onClick.Invoke();
            Call(shop, "OpenPurchasedPermanentUpgradeList");
            Call(shop, "ConfirmPermanentPurchase");
            Assert.That(Confirmation, Is.Null);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
        }

        [Test]
        public void OpeningTouchCannotConfirmButFreshPressCan()
        {
            Buy("autoRepeatFloorUpgradeButton").onClick.Invoke();
            var confirm = Confirmation.transform.Find("ConfirmationPanel/ConfirmPurchase").GetComponent<Button>();
            Assert.That(confirm.GetType().Name, Is.EqualTo("FreshPressButton"));
            var events = new GameObject("TestEvents", typeof(EventSystem)).GetComponent<EventSystem>();
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, pointerId = 0 };
            confirm.OnPointerClick(pointer);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
            Call(confirm, "LateUpdate");
            confirm.OnPointerDown(pointer); confirm.OnPointerClick(pointer);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(8800));
        }

        [Test]
        public void UnreleasedCategoryIsHiddenEvenThroughItsOldEntryPoint()
        {
            Call(shop, "OpenPremiumItemShop");
            var selector = (GameObject)Field(shop, "selectorRoot");
            Assert.That(selector.activeSelf, Is.True);
            Assert.That(selector.GetComponentsInChildren<Text>().Any(t => t.text.Contains("高級アイテム")), Is.False);
            Assert.That(((GameObject)Field(shop, "categoryRoot")).activeSelf, Is.False);
        }

        [Test]
        public void PurchaseAndToggleButtonsAreLargeContrastingAndKeepTheirLabelsInside()
        {
            foreach (var field in new[] { "autoRepeatFloorUpgradeButton", "autoSellEquipmentUpgradeButton", "autoReleaseMonsterUpgradeButton", "monsterStorageUpgradeButton", "equipmentStorageUpgradeButton" })
            {
                var button = Buy(field);
                Assert.That(((RectTransform)button.transform).sizeDelta.y, Is.GreaterThanOrEqualTo(128));
                Assert.That(button.GetComponent<Image>().color.r, Is.GreaterThan(0.5f));
                Assert.That(button.transform.Find("Label").GetComponent<Text>().text, Does.Contain("購入確認へ"));
            }
            Capture("permanent-shop.png");
            Buy("autoRepeatFloorUpgradeButton").onClick.Invoke();
            Capture("permanent-confirmation.png");
            Call(shop, "ClosePermanentPurchaseConfirmation");
            foreach (var prefix in new[] { "AutoRepeatFloor", "AutoSellEquipment", "AutoReleaseMonster" })
            {
                SetProp(profile, "Has" + prefix + "Upgrade", true);
                SetProp(profile, "Is" + prefix + "UpgradeEnabled", true);
            }
            Call(shop, "OpenPurchasedPermanentUpgradeList");
            var toggles = shop.GetComponentsInChildren<Button>().Where(b => b.name == "ToggleButton").ToArray();
            Assert.That(toggles.Length, Is.EqualTo(3));
            foreach (var button in toggles)
            {
                Assert.That(((RectTransform)button.transform).sizeDelta.y, Is.GreaterThanOrEqualTo(128));
                Assert.That(button.GetComponent<Image>().color.r, Is.GreaterThan(button.GetComponent<Image>().color.b));
            }
            Capture("permanent-enabled.png");
            toggles[0].onClick.Invoke();
            Assert.That(Prop("IsAutoRepeatFloorUpgradeEnabled"), Is.False);
            var enable = shop.GetComponentsInChildren<Button>().Single(b => b.name == "ToggleButton" && b.GetComponentInChildren<Text>().text == "有効化する");
            Assert.That(enable.GetComponent<Image>().color.b, Is.GreaterThan(enable.GetComponent<Image>().color.r));
            Capture("permanent-disabled.png");
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
        }

        [Test]
        public void RatesTableIsSeparateModalAndUsesTheActualClassProbabilities()
        {
            shop.gameObject.SetActive(false);
            var gacha = Panel("Home.GachaPanelController", "Gacha");
            var home = (GameObject)Field(gacha, "contractHomeRoot");
            var rates = home.transform.Find("GachaRatesButton").GetComponent<Button>();
            Assert.That(((RectTransform)rates.transform).anchorMax, Is.EqualTo(Vector2.one));
            Capture("summon-home.png");
            rates.onClick.Invoke();
            var overlay = (GameObject)Field(gacha, "ratesOverlayRoot");
            Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
            var table = overlay.transform.Find("GachaRatesPanel");
            int[] free = { 90, 9, 1, 0 }, paid = { 87, 9, 3, 1 };
            for (int rank = 1; rank <= 4; rank++)
            {
                Assert.That(table.Find($"RateRow_{rank}/Free").GetComponent<Text>().text, Is.EqualTo(free[rank - 1] + "%"));
                Assert.That(table.Find($"RateRow_{rank}/Paid").GetComponent<Text>().text, Is.EqualTo(paid[rank - 1] + "%"));
            }
            Assert.That(table.Find("RatesNotes").GetComponent<Text>().text, Does.Contain("クラス3を最低1体保証"));
            Capture("summon-rates.png");
            SetProp(profile, "HasCompletedTutorial", false);
            SetProp(profile, "TutorialStepId", "T02");
            Call(gacha, "RefreshSummonTutorialGuide");
            Assert.That(overlay.transform.GetSiblingIndex(), Is.EqualTo(home.transform.childCount - 1), "Tutorial refresh must not raise the summon button above the rates modal.");
            string status = ((Text)Field(gacha, "statusText")).text;
            Call(gacha, "RunContract", 1, true);
            Assert.That(((Text)Field(gacha, "statusText")).text, Is.EqualTo(status), "Background purchase actions must not run while reading the rates.");
            table.Find("CloseRatesButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(overlay.activeSelf, Is.False);
            Assert.That(Prop("PaidGachaStones"), Is.EqualTo(10000));
        }

        private void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("WITCHTOWER_PERMANENT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { canvas, 1179, 2556, Path.Combine(folder, name) });
        }

        [TestCase("abyss_grand_mage_seraphis", "attack", 0, 572)]
        [TestCase("abyss_grand_mage_seraphis", "attack", 1, 572)]
        [TestCase("abyss_grand_mage_seraphis", "attack", 2, 668)]
        [TestCase("abyss_grand_mage_seraphis", "attack", 3, 539)]
        [TestCase("magic_sword_saint_luciel", "attack", 0, 477)]
        [TestCase("magic_sword_saint_luciel", "attack", 1, 452)]
        [TestCase("magic_sword_saint_luciel", "attack", 2, 576)]
        [TestCase("magic_sword_saint_luciel", "attack", 3, 525)]
        [TestCase("magic_sword_saint_luciel", "move", 0, 569)]
        [TestCase("magic_sword_saint_luciel", "move", 1, 598)]
        [TestCase("magic_sword_saint_luciel", "move", 2, 729)]
        [TestCase("magic_sword_saint_luciel", "move", 3, 593)]
        public void ReviewedMotionFramesRetainFullSilhouetteWidthAndTransparentMargins(string key, string pose, int index, int expectedWidth)
        {
            var sprite = Resources.Load<Sprite>($"MonsterBattle/mon_{key}_{pose}_{index}");
            Assert.That(sprite, Is.Not.Null);
            var texture = sprite.texture;
            Assert.That(texture.width, Is.EqualTo(800));
            var pixels = texture.GetPixels32();
            int left = texture.width, right = -1, bottom = texture.height, top = -1;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a <= 12) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                bottom = Math.Min(bottom, y); top = Math.Max(top, y);
            }
            Assert.That(right - left + 1, Is.EqualTo(expectedWidth).Within(3), "A padded quarter-crop still loses part of the pose; preserve its reviewed original width.");
            Assert.That(left, Is.GreaterThanOrEqualTo(24));
            Assert.That(texture.width - right - 1, Is.GreaterThanOrEqualTo(24));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(16));
            Assert.That(texture.height - top - 1, Is.GreaterThanOrEqualTo(16));
        }
    }
}
