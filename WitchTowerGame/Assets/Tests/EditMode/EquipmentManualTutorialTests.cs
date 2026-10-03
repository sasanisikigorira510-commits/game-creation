using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class EquipmentManualTutorialTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
        private static object F(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj,args);

        [TestCase(false)]
        [TestCase(true)]
        public void ManualEquipThenAutoEquipThenEnhance(bool alreadyEquippedOnResume)
        {
            var gt = T("WitchTower.Managers.GameManager");
            var mt = T("WitchTower.Managers.MasterDataManager");
            var saves = T("WitchTower.Managers.SaveManager");
            var tutorial = T("WitchTower.Data.StoryTutorialService");
            object oldGame = gt.GetProperty("Instance").GetValue(null);
            object oldMaster = mt.GetProperty("Instance").GetValue(null);
            object oldSaveManager = saves.GetProperty("Instance").GetValue(null);
            var randomState = UnityEngine.Random.state;
            GameObject fixtureRoot = null;
            try
            {
                // Exercise only in-memory tutorial UI. Do not borrow a live
                // storage context or replace/save the user's open Editor scene.
                saves.GetProperty("Instance").SetValue(null, null);
                fixtureRoot = new GameObject("ManualTutorialFixture");
                var canvas = new GameObject("TutorialCanvas", typeof(RectTransform),typeof(Canvas));
                canvas.transform.SetParent(fixtureRoot.transform, false);
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080,2341);
                var owner = new GameObject("TutorialController"); owner.SetActive(false);
                owner.transform.SetParent(canvas.transform, false);
                var game = owner.AddComponent(gt); gt.GetProperty("Instance").SetValue(null,game);
                var master = owner.AddComponent(mt); mt.GetProperty("Instance").SetValue(null,master);
                mt.GetMethod("Initialize").Invoke(master,null);
                var st = T("WitchTower.Save.PlayerSaveData");
                object save = st.GetMethod("CreateDefault").Invoke(null,null);
                st.GetField("TutorialStepId").SetValue(save,"T07B");
                st.GetField("InitialTutorialSummonCount").SetValue(save,3);
                object profile = Activator.CreateInstance(T("WitchTower.Data.PlayerProfile"),new[]{save});
                gt.GetProperty("PlayerProfile").SetValue(game,profile);
                object monster = profile.GetType().GetMethod("AddOwnedMonster").Invoke(profile,new object[]{"monster_apprentice_mage",1,0,false});
                string monsterId = (string)monster.GetType().GetField("InstanceId").GetValue(monster);
                var controller = owner.AddComponent(T("WitchTower.Core.TitleSceneController"));
                Call(controller,"EnsureEquipmentScene");
                controller.GetType().GetField("selectedEquipmentMonsterInstanceId",Hidden).SetValue(controller,monsterId);
                Call(controller,"RefreshEquipmentScene");
                object gift = tutorial.GetMethod("FindEquipmentTutorialGift").Invoke(null,new[]{profile});
                Assert.That(gift,Is.Not.Null,"Manual lesson must still grant the practice item");
                string giftId = (string)gift.GetType().GetField("InstanceId").GetValue(gift);
                Func<string> target = () => {
                    object e = tutorial.GetMethod("GetNextEvent").Invoke(null,new[]{profile,(object)"EquipmentScene"});
                    return (string)e.GetType().GetProperty("TargetKey").GetValue(e);
                };
                Assert.That(target(),Is.EqualTo("equipment.first_item"));
                Assert.That(((Button)F(controller,"equipmentAutoEquipButton")).interactable,Is.False);
                Call(controller,"AutoEquipSelectedMonster");
                Assert.That(target(),Is.EqualTo("equipment.first_item"),"Cannot bypass manual lesson");
                var inventory = (RectTransform)F(controller,"equipmentInventoryContentRect");
                var stats = inventory.GetComponentsInChildren<Text>(true).Single(t => t.name == "Stats");
                Canvas.ForceUpdateCanvases();
                Assert.That(stats.fontSize, Is.GreaterThanOrEqualTo(28));
                Assert.That(stats.resizeTextForBestFit, Is.False);
                Assert.That(stats.preferredHeight, Is.LessThanOrEqualTo(stats.rectTransform.rect.height + 1));
                AssertGuideAndControls(controller);
                var filters = ((IList)F(controller, "equipmentInventoryFilterButtonImages")).Cast<Image>().ToArray();
                filters[1].GetComponent<Button>().onClick.Invoke();
                Assert.That(((RectTransform)F(controller, "equipmentInventoryContentRect")).Find("EquipmentEmptyState"), Is.Not.Null);
                filters[0].GetComponent<Button>().onClick.Invoke();
                ((Text)F(controller, "equipmentInventorySortButtonText")).GetComponentInParent<Button>().onClick.Invoke();
                Assert.That(((Text)F(controller, "equipmentInventorySortButtonText")).text, Does.Not.Contain("通常"));
                string inventoryCapture = Environment.GetEnvironmentVariable("WITCHTOWER_INVENTORY_CAPTURE");
                if (!alreadyEquippedOnResume && !string.IsNullOrEmpty(inventoryCapture))
                {
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    ((GameObject)F(controller,"equipmentSceneRoot")).SetActive(true);
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                        .Invoke(null,new object[]{(RectTransform)canvas.transform,1179,2556,inventoryCapture});
                }
                Call(controller,"ShowEquipmentDetailSheet",giftId);
                var detailStats = (Text)F(controller,"equipmentDetailStatsText");
                var data = mt.GetMethod("GetEquipmentData").Invoke(master, new[] { gift.GetType().GetField("EquipmentId").GetValue(gift) });
                string actual = (string)T("WitchTower.Data.EquipmentEnhancementCatalog").GetMethod("BuildEnhancementSummary")
                    .Invoke(null, new[] { data, gift });
                Assert.That(detailStats.text, Does.StartWith("現在の装備効果（品質・強化込み）\n" + actual));
                Assert.That(detailStats.text, Does.Not.Contain("% 賢さ"), "Do not mix static catalog stats into rolled equipment effects.");
                AssertGuideAndControls(controller);
                var equip = (Button)F(controller,"equipmentDetailEquipButton");
                Assert.That(equip.interactable,Is.True);
                var highlight = equip.transform.Find("TutorialTargetFrame").GetComponent<Image>();
                Assert.That(highlight.sprite,Is.Not.Null);
                Assert.That(highlight.sprite.texture.name,Is.EqualTo("TutorialSummonHighlightFrameImage2"));
                Assert.That(highlight.raycastTarget, Is.False);
                Call(controller,"RefreshEquipmentScene");
                Assert.That(highlight.gameObject.activeSelf, Is.True);
                highlight.GetComponent(T("WitchTower.UI.TutorialTargetFrame")).SendMessage("Update");
                Assert.That(highlight.color.a, Is.LessThan(1f));
                Assert.That(highlight.raycastTarget, Is.False);
                string capture = Environment.GetEnvironmentVariable("WITCHTOWER_DETAIL_CAPTURE");
                if (!alreadyEquippedOnResume && !string.IsNullOrEmpty(capture))
                {
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    ((GameObject)F(controller,"equipmentSceneRoot")).SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)
                        .Invoke(null,new object[]{(RectTransform)canvas.transform,1179,2556,capture});
                }
                Assert.That(((Text)F(controller,"equipmentTutorialGuideFooterText")).text,Does.Contain("詳細画面"));
                Assert.That(((Button)F(controller,"equipmentDetailEnhanceButton")).interactable,Is.False);
                if (alreadyEquippedOnResume)
                {
                    profile.GetType().GetMethod("EquipEquipmentToMonster").Invoke(profile,new object[]{monsterId,giftId});
                    Call(controller,"CloseEquipmentDetailSheet");
                    Call(controller,"RefreshEquipmentScene");
                }
                else equip.onClick.Invoke();
                Assert.That(target(),Is.EqualTo("equipment.auto_equip"));
                Assert.That(((Button)F(controller,"equipmentAutoEquipButton")).interactable,Is.True);
                Assert.That(((Text)F(controller,"equipmentTutorialGuideTitleText")).text,Is.EqualTo("便利な自動装備"));
                AssertGuideAndControls(controller);
                ((GameObject)F(controller,"equipmentSceneRoot")).SetActive(true);
                var autoButton = (Button)F(controller,"equipmentAutoEquipButton");
                Color normalAutoColor = autoButton.GetComponent<Image>().color;
                Call(controller,"EnsureEquipmentTutorialActionButtonFocusVisible");
                Call(controller,"AnimateEquipmentTutorialGuide");
                // Inventory refresh used to forget the tint but leave the yellow
                // on this persistent button after removing its temporary frame.
                Call(controller,"RefreshEquipmentScene");
                Call(controller,"EnsureEquipmentTutorialActionButtonFocusVisible");
                Call(controller,"AnimateEquipmentTutorialGuide");
                Call(controller,"AutoEquipSelectedMonster");
                Call(controller,"AnimateEquipmentTutorialGuide");
                Assert.That(autoButton.GetComponent<Image>().color, Is.EqualTo(normalAutoColor), "Restore normal button color after the auto-equip lesson.");
                Assert.That(autoButton.transform.Cast<Transform>().Any(t => t.name.StartsWith("EquipmentTutorialFocus") && t.gameObject.activeSelf), Is.False);
                Assert.That(target(),Is.EqualTo("equipment.enhance_button"),"Already optimal loadout must complete auto lesson");
                Assert.That(((Button)F(controller,"equipmentAutoEquipButton")).transform.parent.Find("EquipmentTutorialEquipPrompt"),Is.Null);
                Assert.That(gift.GetType().GetField("EquippedMonsterInstanceId").GetValue(gift),Is.EqualTo(monsterId));
                AssertGuideAndControls(controller);
            }
            finally
            {
                if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
                gt.GetProperty("Instance").SetValue(null,oldGame); mt.GetProperty("Instance").SetValue(null,oldMaster);
                saves.GetProperty("Instance").SetValue(null, oldSaveManager);
                UnityEngine.Random.state = randomState;
            }
        }
        private static void AssertGuideAndControls(object controller)
        {
            Canvas.ForceUpdateCanvases();
            var guide = (GameObject)F(controller, "equipmentTutorialGuideRoot");
            var body = (Text)F(controller, "equipmentTutorialGuideBodyText");
            var footer = (Text)F(controller, "equipmentTutorialGuideFooterText");
            Bounds bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(guide.transform, body.transform);
            Bounds footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(guide.transform, footer.transform);
            Assert.That(bodyBounds.min.y - footerBounds.max.y, Is.GreaterThanOrEqualTo(32f));
            Assert.That(footer.preferredHeight, Is.LessThanOrEqualTo(footer.rectTransform.rect.height + 1));
            var filters = ((IList)F(controller, "equipmentInventoryFilterButtonImages")).Cast<Image>().ToArray();
            var sort = ((Text)F(controller, "equipmentInventorySortButtonText")).GetComponentInParent<Button>();
            var buttons = filters.Select(i => i.rectTransform).Concat(new[] { (RectTransform)sort.transform }).ToArray();
            Assert.That(buttons.Select(b => b.anchoredPosition.y).Distinct().Count(), Is.EqualTo(2));
            foreach (var button in buttons)
                Assert.That(button.rect.height, Is.GreaterThanOrEqualTo(124));
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i+1; j < buttons.Length; j++)
                {
                    Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(buttons[i].parent, buttons[i]);
                    Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(buttons[j].parent, buttons[j]);
                    float gap = Mathf.Max(Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x),
                        Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
                    Assert.That(gap, Is.GreaterThanOrEqualTo(18f), "Separate adjacent tap targets.");
                }
            var content = (RectTransform)F(controller,"equipmentInventoryContentRect");
            var viewport = (RectTransform)content.parent;
            Assert.That(viewport.rect.height, Is.GreaterThanOrEqualTo(206f), "Keep at least one entire equipment card visible.");
        }
    }
}
