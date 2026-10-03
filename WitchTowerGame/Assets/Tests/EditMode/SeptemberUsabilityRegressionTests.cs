using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Tests
{
    public sealed class SeptemberUsabilityRegressionTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + name, true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethods(Any)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(b=>b)).Invoke(obj,args);
        private static object S(string type, string method, params object[] args) => T(type).GetMethod(method,Any).Invoke(null,args);
        private static object P(object obj, string name) => obj.GetType().GetProperty(name,Any).GetValue(obj);
        private static object F(object obj, string name) => obj.GetType().GetField(name,Any).GetValue(obj);
        private static void SetP(object obj, string name, object value) => obj.GetType().GetProperty(name,Any).SetValue(obj,value);
        private static void SetF(object obj, string name, object value) => obj.GetType().GetField(name,Any).SetValue(obj,value);
        private GameObject owner, canvas;
        private object game, master, oldGame, oldMaster, oldSave;
        private SceneSetup[] scenes;
        private UnityEngine.Random.State randomState;

        [SetUp] public void Setup()
        {
            randomState = UnityEngine.Random.state;
            scenes = EditorSceneManager.GetSceneManagerSetup();
            oldGame = T("Managers.GameManager").GetProperty("Instance").GetValue(null);
            oldMaster = T("Managers.MasterDataManager").GetProperty("Instance").GetValue(null);
            oldSave = T("Managers.SaveManager").GetProperty("Instance").GetValue(null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            owner = new GameObject("SeptemberTestManagers"); owner.SetActive(false);
            game = owner.AddComponent(T("Managers.GameManager"));
            master = owner.AddComponent(T("Managers.MasterDataManager"));
            T("Managers.GameManager").GetProperty("Instance").SetValue(null,game);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,master);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null,null);
            Call(master,"Initialize");
            canvas = new GameObject("SeptemberCanvas",typeof(RectTransform),typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080,2341);
        }
        [TearDown] public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            T("Managers.GameManager").GetProperty("Instance").SetValue(null,oldGame);
            T("Managers.MasterDataManager").GetProperty("Instance").SetValue(null,oldMaster);
            T("Managers.SaveManager").GetProperty("Instance").SetValue(null,oldSave);
            UnityEngine.Random.state = randomState;
            if (scenes.Length > 0 && scenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        }
        private object Profile(string step = "Complete", int floor = 0)
        {
            object save = S("Save.PlayerSaveData","CreateDefault");
            SetF(save,"TutorialStepId",step); SetF(save,"HasCompletedTutorial",step == "Complete");
            SetF(save,"InitialTutorialSummonCount",3); SetF(save,"HighestFloor",floor);
            object profile = Activator.CreateInstance(T("Data.PlayerProfile"),new[]{save});
            SetP(game,"PlayerProfile",profile); return profile;
        }
        private object Add(object p,string species="monster_rock_golem",int level=1) => Call(p,"AddOwnedMonster",species,level,0,false);
        private void Capture(string name)
        {
            string dir = Environment.GetEnvironmentVariable("WITCHTOWER_SEPTEMBER_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            typeof(EquipmentEnhanceGuideLayoutTests).GetMethod("Capture",Any).Invoke(null,new object[]{(RectTransform)canvas.transform,1179,2556,dir+"/"+name+".png"});
        }

        [TestCase(20, false)][TestCase(29, false)][TestCase(30, true)]
        public void GuardianBattleLabelMatchesThirdStageUnlock(int floor, bool unlocked)
        {
            var p = Profile(floor: floor);
            S("Data.GuardianTrialSession", "Reset");
            var c = owner.AddComponent(T("Battle.BattleSceneController"));
            var panel = new GameObject("GuardianPanel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            SetF(c, "skillPanelRoot", panel);
            Call(c, "UpdateGuardianBattlePanel");
            // The rebuilt panel separates its heading and unlock guidance.
            string label = ((Text)F(c, "guardianBattleLabel")).text + "\n" +
                ((Text)F(c, "guardianResonanceLabel")).text;
            Assert.That(S("Data.GuardianService", "IsUnlocked", p), Is.EqualTo(unlocked));
            Assert.That(label, Does.Not.Contain("20階層"));
            Assert.That(label, unlocked ? Does.Contain("未編成") : Does.Contain("第3ステージクリアで解放"));
        }

        [Test]
        public void ClassOneBattlePresentationIsSmallerForEveryPoseAndSide()
        {
            string[] first = { "dragon_whelp", "chibi_gear", "rock_golem", "apprentice_swordsman", "apprentice_mage" };
            string[] second = { "flare_drake", "armed_droid", "ore_giant_garm", "holy_armor_leon", "dark_robe_curse_mage_noah" };
            foreach (object pose in Enum.GetValues(T("Battle.BattleVisualPose")))
                for (int i = 0; i < first.Length; i++)
                {
                    var small = Call(master, "GetMonsterData", "monster_" + first[i]);
                    var large = Call(master, "GetMonsterData", "monster_" + second[i]);
                    float smallScale = (float)S("Battle.BattleSceneController", "ResolveMonsterPreviewScale", small, pose);
                    float largeScale = (float)S("Battle.BattleSceneController", "ResolveMonsterPreviewScale", large, pose);
                    Assert.That(smallScale, Is.InRange(.55f, .76f), first[i]);
                    Assert.That(smallScale, Is.LessThan(largeScale * .76f), first[i]);
                    var enemy = ScriptableObject.CreateInstance(T("MasterData.EnemyDataSO"));
                    try
                    {
                        SetF(enemy, "enemyId", "enemy_class1_" + first[i]);
                        float enemyScale = (float)S("Battle.BattleSceneController", "ResolveEnemyPreviewScale", enemy, pose);
                        Assert.That(enemyScale, Is.InRange(.55f, .76f), "Enemy " + first[i]);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(enemy); }
                }
        }

        [Test]
        public void ClassOneRenderedIdleBodiesAreSmallerThanClassTwo()
        {
            string[] first = { "dragon_whelp", "chibi_gear", "rock_golem", "apprentice_swordsman", "apprentice_mage" };
            string[] second = { "flare_drake", "armed_droid", "ore_giant_garm", "holy_armor_leon", "dark_robe_curse_mage_noah" };
            object idle = Enum.Parse(T("Battle.BattleVisualPose"), "Idle");
            for (int row = 0; row < first.Length; row++)
            {
                float[] heights = new float[2];
                for (int column = 0; column < 2; column++)
                {
                    string id = "monster_" + (column == 0 ? first[row] : second[row]);
                    var data = Call(master, "GetMonsterData", id);
                    var sprites = (System.Collections.Generic.List<Sprite>)S("Battle.BattleVisualResolver", "ResolveMonsterIdleSprites", data);
                    Assert.That(sprites.Count, Is.GreaterThan(0), id);
                    var node = new GameObject(id, typeof(RectTransform), typeof(Image));
                    node.transform.SetParent(canvas.transform, false);
                    var image = node.GetComponent<Image>(); image.sprite = sprites[0]; image.preserveAspect = true;
                    float scale = (float)S("Battle.BattleSceneController", "ResolveMonsterPreviewScale", data, idle);
                    var mode = S("Battle.BattleSceneController", "ResolveAllyPreviewMeasurementMode", data, idle);
                    S("Battle.BattleSceneController", "ApplyPreviewVisualLayout", image, new Vector2(220,220) * scale,
                        new Vector2(column == 0 ? -230 : 230, 780 - row * 360), sprites, mode);
                    var metrics = S("Battle.BattleSceneController", "ResolvePreviewVisualMetrics", sprites[0], mode);
                    heights[column] = image.rectTransform.sizeDelta.y * (float)F(metrics, "OpaqueHeight") / (float)F(metrics, "SpriteHeight");
                }
                Assert.That(heights[0], Is.LessThan(heights[1] * .85f), first[row] + " vs " + second[row]);
            }
            Capture("battle-class-size-comparison");
        }

        [Test]
        public void FormationConfirmationIsLargeAndDoesNotCoverStats()
        {
            canvas.name = "TitleCanvas";
            var p = Profile(); Add(p); Add(p, "monster_apprentice_mage");
            var c = owner.AddComponent(T("Formation.FormationSceneController"));
            Call(c, "TrySeedRosterFromPlayerProfile"); Call(c, "EnsureScaffold"); Call(c, "RefreshView");
            Canvas.ForceUpdateCanvases();
            var content = (RectTransform)F(c, "rosterContent");
            Assert.That(content, Is.Not.Null, "The fixture must use the formation scene's owned canvas.");
            foreach (var button in content.GetComponentsInChildren<Button>().Where(b => b.name == "SelectionButton"))
            {
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(230));
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(112));
                Assert.That(button.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(28));
                var labels = new[] { "NameLabel", "DamageTypeLabel", "LevelLabel", "IndividualValueLabel" }
                    .Select(n => button.transform.parent.Find(n).GetComponent<RectTransform>()).ToArray();
                AssertSeparated(labels.Concat(new[] { rect }).ToArray(), button.transform.parent, 6);
            }
            Capture("formation-large-buttons");
        }

        [TestCase(false)][TestCase(true)]
        public void EquipmentContributionDoesNotIncludeAccountBonuses(bool equipped)
        {
            var p = Profile("T07B"); SetP(p,"Level",6); SetP(p,"AttackUpgradeLevel",4);
            var monster = Add(p,"monster_omega_leon",39);
            var data = Call(master,"GetMonsterData","monster_omega_leon");
            S("Data.StoryTutorialService","EnsureEquipmentTutorialGift",p);
            var gift = S("Data.StoryTutorialService","FindEquipmentTutorialGift",p);
            if (equipped) Call(p,"EquipEquipmentToMonster",F(monster,"InstanceId"),F(gift,"InstanceId"));
            string before = JsonUtility.ToJson(Call(p,"ToSaveData",1));
            var stats = S("Battle.MonsterBattleStatsFactory","Create",p,monster,data);
            var noEquipment = S("Battle.MonsterBattleStatsFactory","CreateWithoutEquipment",p,monster,data);
            var bare = S("Battle.MonsterBattleStatsFactory","Create",null,monster,data);
            var contribution = S("UI.MonsterStatusDetailPopup","CalculateEquipmentContribution",p,monster,data,stats);
            Assert.That((int)F(noEquipment,"MaxHp"),Is.GreaterThan((int)F(bare,"MaxHp")),"Keep account-level HP bonuses.");
            foreach (string stat in new[]{"MaxHp","Attack","Wisdom","Defense","MagicDefense"})
            {
                int actual = (int)P(contribution,stat);
                Assert.That(actual,Is.EqualTo((int)F(stats,stat)-(int)F(noEquipment,stat)));
                if (!equipped) Assert.That(actual,Is.Zero);
            }
            if (equipped) Assert.That((int)P(contribution,"MaxHp"),Is.GreaterThan(0));
            Assert.That(JsonUtility.ToJson(Call(p,"ToSaveData",1)),Is.EqualTo(before),"Computing contributions cannot unequip or modify saves.");
        }

        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)]
        public void DungeonClassProgressionAndRandomBossOnlyOnTenthFloor(int stage)
        {
            int normalClass = (stage + 1) / 2;
            for (int local = 1; local <= 10; local++)
            {
                int global = (stage-1)*10+local;
                var floor = S("Data.BattleDungeonCatalog","GetFloorForGlobalFloor",global);
                foreach (string id in (IEnumerable)P(floor,"EnemyMonsterIds"))
                    Assert.That(F(Call(master,"GetMonsterData",id),"classRank"),Is.EqualTo(normalClass));
                var candidates = (string[])S("Data.BattleDungeonCatalog","ResolveBossMonsterIds",global);
                if (stage == 6 && local == 10)
                {
                    string garza = (string)T("Data.GarzaBossPresentation").GetField("MonsterId", Any).GetRawConstantValue();
                    Assert.That(candidates, Is.EqualTo(new[] { garza }), "The final story boss is fixed, not random.");
                }
                else Assert.That(candidates.Length,local == 10 ? Is.GreaterThan(1) : Is.Zero);
                foreach (string id in candidates)
                {
                    var monster = Call(master, "GetMonsterData", id);
                    if (stage == 6 && local == 10)
                        Assert.That(monster, Is.Null, "The story boss must remain enemy-only, not obtainable monster data.");
                    else Assert.That(F(monster,"classRank"),Is.EqualTo(normalClass+1));
                }
                Assert.That(P(floor,"BossEnemyCount"),Is.EqualTo(local==10?1:0));
                UnityEngine.Random.InitState(900+stage);
                var rolls = Enumerable.Range(0,200).Select(_ => (string[])S("Data.BattleDungeonCatalog","RollBossMonsterIdsForBattle",global)).ToArray();
                Assert.That(rolls.All(r=>r.Length==(local==10?1:0)),Is.True);
                if (local==10) Assert.That(rolls.SelectMany(r=>r).Distinct(),Is.EquivalentTo(candidates));
            }
        }

        [Test] public void BattleSetupRollsOneFinalBossAndKeepsThatChoiceForItsSpawn()
        {
            var p = Profile();
            var monster = Add(p);
            Call(p,"SetPartyMonsterIds",new System.Collections.Generic.List<string>{(string)F(monster,"InstanceId")});
            var simulator = owner.AddComponent(T("Battle.BattleSimulator"));
            for (int floor = 1; floor <= 60; floor++)
            {
                Call(simulator,"Setup",floor);
                int count = (int)P(simulator,"CurrentEnemyCountTarget");
                string[] chosen = (string[])F(simulator,"finalBossMonsterIds");
                Assert.That(chosen.Length,Is.EqualTo(floor % 10 == 0 ? 1 : 0));
                for (int index = 0; index < count; index++)
                {
                    string id = (string)Call(simulator,"ResolveFinalBossMonsterIdForSpawnIndex",index);
                    Assert.That(id,Is.EqualTo(chosen.Length == 1 && index == count-1 ? chosen[0] : string.Empty));
                    Assert.That(Call(simulator,"ResolveFinalBossMonsterIdForSpawnIndex",index),Is.EqualTo(id),"Spawn resolution must not reroll the boss.");
                }
            }
        }

        [Test] public void EquipmentTargetsAreLargeAndOrderedPartyThenFavoriteThenLock()
        {
            var p = Profile();
            var monsters = Enumerable.Range(0,8).Select(_=>Add(p)).ToArray();
            SetF(monsters[0],"IsFavorite",true); SetF(monsters[1],"IsLocked",true);
            string[] party = new[]{6,4,2,7,3}.Select(i=>(string)F(monsters[i],"InstanceId")).ToArray();
            Call(p,"SetPartyMonsterIds",party.ToList());
            var order = ((IEnumerable)S("Core.TitleSceneController","GetEquipmentSceneMonsters",p)).Cast<object>().Select(m=>(string)F(m,"InstanceId")).ToArray();
            Assert.That(order,Is.EqualTo(party.Concat(new[]{0,1,5}.Select(i=>(string)F(monsters[i],"InstanceId")))));
            var c = owner.AddComponent(T("Core.TitleSceneController")); Call(c,"EnsureEquipmentScene"); Call(c,"RefreshEquipmentScene");
            var root = (GameObject)F(c,"equipmentSceneRoot"); root.SetActive(true);
            var summary = root.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="EquippedSummaryPanel");
            var buttons = summary.GetComponentsInChildren<Button>().Select(b=>(RectTransform)b.transform).ToArray();
            Assert.That(buttons.Length,Is.EqualTo(5));
            foreach (var rect in buttons) Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(100));
            AssertSeparated(buttons,summary,14);
            var portrait = summary.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "EquipmentMonsterPortraitBackdrop");
            AssertSeparated(buttons.Concat(new[]{portrait}).ToArray(),summary,14);
            Capture("equipment");
            Call(c,"OpenEquipmentMonsterPicker");
            foreach (string field in new[]{"equipmentMonsterClassFilterButtonImages","equipmentMonsterElementFilterButtonImages"})
                foreach (Image button in (IEnumerable)F(c,field)) Assert.That(button.rectTransform.rect.height,Is.GreaterThanOrEqualTo(124));
            Capture("equipment-picker");
        }

        [Test] public void FormationTutorialRequiresDestinationMonsterThenConfirmation()
        {
            canvas.name = "TitleCanvas";
            var p = Profile("T04"); for (int i=0;i<3;i++) Add(p);
            var c = owner.AddComponent(T("Formation.FormationSceneController"));
            Call(c,"TrySeedRosterFromPlayerProfile"); Call(c,"EnsureScaffold"); Call(c,"RefreshView");
            IList roster=(IList)F(c,"roster"), selected=(IList)F(c,"selectedMonsters");
            var content=(Transform)F(c,"rosterContent");
            Assert.That(content, Is.Not.Null, "The fixture must use the formation scene's owned canvas.");
            Assert.That(F(c,"activeSlotIndex"),Is.EqualTo(-1));
            Assert.That(content.GetComponentsInChildren<Button>().Count(b => b.name == "SelectionButton"),Is.Zero);
            Call(c,"SelectTutorialMonster",roster[0]); Call(c,"ToggleSelection",roster[0]);
            Assert.That(selected.Cast<object>().Count(m=>m!=null),Is.Zero);
            Capture("formation-step1");
            for (int i=0;i<3;i++)
            {
                Call(c,"OnSlotPressed",i);
                Call(c,"ToggleSelection",roster[i]);
                Assert.That(selected.Cast<object>().Count(m=>m!=null),Is.EqualTo(i),"Must tap monster before confirm.");
                if(i==0) Capture("formation-step2");
                Call(c,"SelectTutorialMonster",roster[i]);
                Assert.That(selected.Cast<object>().Count(m=>m!=null),Is.EqualTo(i),"Selecting an image does not assign it.");
                Assert.That(content.GetComponentsInChildren<Button>().Count(b => b.name == "SelectionButton"),Is.EqualTo(1),"Only the pending monster exposes confirmation.");
                if(i==0) Capture("formation-step3");
                Call(c,"ToggleSelection",roster[i]);
                Assert.That(selected[i],Is.SameAs(roster[i]));
                Assert.That(F(c,"activeSlotIndex"),Is.EqualTo(-1));
                var displayed=((IEnumerable)Call(c,"BuildDisplayEntries")).Cast<object>().ToArray();
                Assert.That(displayed.Take(i+1),Is.EqualTo(roster.Cast<object>().Take(i+1)));
            }
            Assert.That(P(p,"TutorialStepId"),Is.EqualTo("T05"));
            Assert.That(content.GetComponentsInChildren<Text>().Count(t=>t.text.Contains("編成中：")),Is.EqualTo(3));
            Capture("formation-complete");
        }

        [TestCase(2341)] [TestCase(1920)]
        public void FusionGuidePagesAndAllSixIndividualValuesFit(int canvasHeight)
        {
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, canvasHeight);
            var p=Profile("Complete",20);
            S("Data.StoryTutorialService","EnsureFusionInheritanceTutorialGift",p);
            var root=new GameObject("FusionTest",typeof(RectTransform)); root.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)root.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.sizeDelta=Vector2.zero;
            var c=root.AddComponent(T("Home.MonsterFusionPanelController")); Call(c,"Show",(object)null);
            for(int page=0;page<3;page++)
            {
                var body=(Text)F(c,"fusionTutorialGuideBodyText"); Canvas.ForceUpdateCanvases();
                Assert.That(body.preferredHeight,Is.LessThanOrEqualTo(body.rectTransform.rect.height+1));
                Assert.That(body.fontSize,Is.GreaterThanOrEqualTo(32));
                if(page==0)
                {
                    Assert.That(body.text,Does.Contain("レベルMAX"));
                    body.cachedTextGenerator.Populate(body.text, body.GetGenerationSettings(body.rectTransform.rect.size));
                    Assert.That(body.cachedTextGenerator.lineCount,Is.EqualTo(5),"Keep the first lesson to five readable lines.");
                    Assert.That(body.text.Split('\n')[3],Does.EndWith("用意"));
                    Assert.That(body.text.Split('\n')[4],Is.EqualTo("しました。"));
                }
                if(page==1) Assert.That(body.text.Replace("\n", ""),Does.Contain("個体値は能力毎に50％の確率で親１か親２どちらかの値を継承します。"));
                if(page==2) Assert.That(body.text,Does.Contain("レベル1で誕生").And.Not.Contain("親のレベル"));
                Capture("fusion-guide-"+page+"-"+canvasHeight);
                Call(c,"CompleteFusionTutorialGuide");
            }
            foreach(Text text in root.GetComponentsInChildren<Text>(true).Where(t=>t.name=="IndividualStats"))
            {
                Assert.That(text.text,Does.Contain("HP").And.Contain("攻撃").And.Contain("魔攻").And.Contain("防御").And.Contain("魔防").And.Contain("攻速"));
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1));
            }
            Capture("fusion-list");
        }

        [Test] public void QualityTutorialHighlightsReadableQualityLabel()
        {
            var p=Profile(); Add(p);
            var gear=Call(p,"AddOwnedEquipmentWithInstancePrefix","equip_apprentice_charm",Enum.Parse(T("MasterData.EquipmentRarity"),"Rare"),"test_quality_");
            SetF(gear,"QualityRank",5);
            foreach(string hint in new[]{"tutorial_equipment","tutorial_equipment_auto_equip","tutorial_equipment_enhance","tutorial_equipment_enhance_return_home"})
                S("Data.StoryTutorialService","MarkHintSeen",p,hint);
            var c=owner.AddComponent(T("Core.TitleSceneController")); Call(c,"EnsureEquipmentScene"); Call(c,"RefreshEquipmentScene");
            ((GameObject)F(c,"equipmentSceneRoot")).SetActive(true);
            var content=(RectTransform)F(c,"equipmentInventoryContentRect");
            var quality=content.GetComponentsInChildren<Text>().Single(t=>t.name=="Quality");
            Assert.That(quality.text,Does.StartWith("品質:"));
            Assert.That(quality.transform.Find("TutorialTargetFrame"),Is.Not.Null);
            Assert.That(quality.preferredHeight,Is.LessThanOrEqualTo(quality.rectTransform.rect.height+1));
            Capture("equipment-quality");
        }

        [Test] public void EquipmentSortAndBulkUnequipPreserveInventoryAndSurviveSave()
        {
            var p = Profile("T07B");
            var a = Add(p); var b = Add(p);
            S("Data.StoryTutorialService", "EnsureEquipmentTutorialGift", p);
            var gift = S("Data.StoryTutorialService", "FindEquipmentTutorialGift", p);
            SetP(p,"TutorialStepId","Complete"); SetP(p,"HasCompletedTutorial",true);
            var other=Call(p,"AddOwnedEquipmentWithInstancePrefix","equip_bronze_blade",Enum.Parse(T("MasterData.EquipmentRarity"),"Rare"),"test_other_");
            var low=Call(p,"AddOwnedEquipmentWithInstancePrefix","equip_bronze_blade",Enum.Parse(T("MasterData.EquipmentRarity"),"Common"),"test_low_");
            SetF(other,"QualityRank",5); SetF(low,"QualityRank",1);
            Call(p,"EquipEquipmentToMonster",F(a,"InstanceId"),F(other,"InstanceId"));
            string id=(string)F(gift,"InstanceId");
            Call(p,"EquipEquipmentToMonster",F(b,"InstanceId"),id);
            var c=owner.AddComponent(T("Core.TitleSceneController")); Call(c,"EnsureEquipmentScene"); Call(c,"RefreshEquipmentScene");
            SetF(c,"selectedEquipmentMonsterInstanceId",F(b,"InstanceId"));
            var inventory=((IEnumerable)Call(c,"BuildEquipmentInventoryDisplayEquipments",p)).Cast<object>().ToArray();
            Assert.That(F(inventory[0],"InstanceId"),Is.EqualTo(id));
            for(int i=0;i<3;i++) Call(c,"CycleEquipmentInventorySort");
            Assert.That(Call(c,"GetEquipmentInventorySortLabel"),Is.EqualTo("品質"));
            var sorted=((IEnumerable)Call(c,"BuildEquipmentInventoryDisplayEquipments",p)).Cast<object>().ToArray();
            Assert.That(F(sorted[0],"InstanceId"),Is.EqualTo(id));
            Assert.That(F(sorted[1],"InstanceId"),Is.EqualTo(F(other,"InstanceId")));
            Assert.That(F(sorted[2],"InstanceId"),Is.EqualTo(F(low,"InstanceId")));
            Call(c,"CycleEquipmentInventorySort"); Assert.That(Call(c,"GetEquipmentInventorySortLabel"),Is.EqualTo("通常"));
            Call(c,"UnequipAllEquipment");
            var restored=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{Call(p,"ToSaveData",1)});
            var after=((IEnumerable)P(restored,"OwnedEquipments")).Cast<object>().ToArray();
            Assert.That(after.Length,Is.EqualTo(inventory.Length));
            Assert.That(after.All(e=>string.IsNullOrEmpty((string)F(e,"EquippedMonsterInstanceId"))),Is.True);
            Assert.That(((IEnumerable)P(restored,"OwnedMonsters")).Cast<object>().Count(),Is.EqualTo(2));
        }

        [Test]
        public void TutorialFusionStartsAtLevelOneAndPreservesInheritanceAfterSaving()
        {
            var p=Profile("Complete",20);
            S("Data.StoryTutorialService","EnsureFusionInheritanceTutorialGift",p);
            var parents=((IEnumerable)P(p,"OwnedMonsters")).Cast<object>().ToArray();
            Assert.That(parents,Has.Length.EqualTo(2));
            foreach(var parent in parents) SetF(parent,"Exp",123);
            var result=S("Data.MonsterFusionService","Fuse",p,F(parents[0],"InstanceId"),F(parents[1],"InstanceId"),master,false);
            Assert.That(P(result,"CanFuse"),Is.True);
            var restored=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{Call(p,"ToSaveData",1)});
            var child=((IEnumerable)P(restored,"OwnedMonsters")).Cast<object>().Single();
            Assert.That(F(child,"Level"),Is.EqualTo(1));
            Assert.That(F(child,"Exp"),Is.EqualTo(0));
            Assert.That(P(child,"TotalPlusValue"),Is.EqualTo(3));
            Assert.That(F(child,"FusionBonusHp"),Is.GreaterThan(0));
            foreach(string stat in new[]{"IndividualHp","IndividualAttack","IndividualWisdom","IndividualDefense","IndividualMagicDefense","IndividualAttackSpeed"})
                Assert.That(F(child,stat),Is.EqualTo(F(parents[0],stat)).Or.EqualTo(F(parents[1],stat)),stat);
            foreach(var parent in parents)
                Assert.That(Call(restored,"GetOwnedMonster",F(parent,"InstanceId")),Is.Null);
        }

        [TestCase("monster_sword_saint_alvarez")]
        [TestCase("monster_cosmic_ore_fortress_golem")]
        [TestCase("monster_omega_leon")]
        public void SameSpeciesClassThreeFusionProducesClassThree(string id)
        {
            var p=Profile(); var a=Add(p,id,60); var b=Add(p,id,60);
            object preview=S("Data.MonsterFusionService","PreviewFusion",p,F(a,"InstanceId"),F(b,"InstanceId"),master);
            Assert.That(P(preview,"CanFuse"),Is.True);
            Assert.That(P(P(preview,"Recipe"),"ResultMonsterId"),Is.EqualTo(id));
            if (id == "monster_sword_saint_alvarez")
            {
                var root=new GameObject("ClassThreeFusion",typeof(RectTransform)); root.transform.SetParent(canvas.transform,false);
                var controller=root.AddComponent(T("Home.MonsterFusionPanelController")); Call(controller,"Show",(object)null);
                SetF(controller,"parentAInstanceId",F(a,"InstanceId")); SetF(controller,"parentBInstanceId",F(b,"InstanceId"));
                Call(controller,"RefreshPreview"); Call(controller,"RefreshRoster");
                var status=(Text)F(controller,"statusLabel");
                Assert.That(status.preferredHeight,Is.LessThanOrEqualTo(status.rectTransform.rect.height));
                Capture("fusion-class3");
            }
            object result=S("Data.MonsterFusionService","Fuse",p,F(a,"InstanceId"),F(b,"InstanceId"),master,false);
            Assert.That(P(result,"CanFuse"),Is.True);
            var child=((IEnumerable)P(p,"OwnedMonsters")).Cast<object>().Single();
            Assert.That(F(child,"MonsterId"),Is.EqualTo(id)); Assert.That(F(child,"Level"),Is.EqualTo(1));
            Assert.That(F(child,"Exp"),Is.EqualTo(0));
            Assert.That(Call(p,"GetOwnedMonster",F(a,"InstanceId")),Is.Null);
            Assert.That(Call(p,"GetOwnedMonster",F(b,"InstanceId")),Is.Null);
        }

        [TestCase("monster_magic_sword_saint_luciel", "monster_sword_saint_alvarez", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_sword_saint_alvarez", "monster_magic_sword_saint_luciel", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_rock_golem", "monster_magic_sword_saint_luciel", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_magic_sword_saint_luciel", "monster_rock_golem", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_magic_sword_saint_luciel", "monster_magic_sword_saint_luciel", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_magic_sword_saint_luciel", "monster_dragon_sword_saint_agito", "monster_magic_sword_saint_luciel")]
        [TestCase("monster_dragon_sword_saint_agito", "monster_magic_sword_saint_luciel", "monster_dragon_sword_saint_agito")]
        [TestCase("monster_seraph_michael", "monster_spirit_queen_titania", "monster_seraph_michael")]
        public void HighClassFusionRetainsExactHigherParentAndPersists(string first, string second, string expected)
        {
            var p=Profile();
            var dataA=Call(master,"GetMonsterData",first); var dataB=Call(master,"GetMonsterData",second);
            var a=Call(p,"AddOwnedMonster",first,(int)F(dataA,"classRank")*20,2,false);
            var b=Call(p,"AddOwnedMonster",second,(int)F(dataB,"classRank")*20,3,false);
            string before=JsonUtility.ToJson(Call(p,"ToSaveData",1));
            var preview=S("Data.MonsterFusionService","PreviewFusion",p,F(a,"InstanceId"),F(b,"InstanceId"),master);
            Assert.That(P(preview,"CanFuse"),Is.True);
            Assert.That(P(P(preview,"Recipe"),"ResultMonsterId"),Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(Call(p,"ToSaveData",1)),Is.EqualTo(before),"Preview must not consume parents.");
            if(first=="monster_magic_sword_saint_luciel" && second=="monster_sword_saint_alvarez")
            {
                var root=new GameObject("HighClassFusion",typeof(RectTransform)); root.transform.SetParent(canvas.transform,false);
                var c=root.AddComponent(T("Home.MonsterFusionPanelController")); Call(c,"Show",(object)null);
                SetF(c,"parentAInstanceId",F(a,"InstanceId")); SetF(c,"parentBInstanceId",F(b,"InstanceId"));
                Call(c,"RefreshPreview"); Call(c,"RefreshRoster");
                var rule=root.GetComponentsInChildren<Text>().Single(t=>t.name=="RuleHint");
                Assert.That(rule.preferredHeight,Is.LessThanOrEqualTo(rule.rectTransform.rect.height));
                Assert.That(((Button)F(c,"fuseButton")).interactable,Is.True);
                Capture("fusion-high-class");
            }
            var result=S("Data.MonsterFusionService","Fuse",p,F(a,"InstanceId"),F(b,"InstanceId"),master,false);
            Assert.That(P(result,"CanFuse"),Is.True);
            var restored=Activator.CreateInstance(T("Data.PlayerProfile"),new[]{Call(p,"ToSaveData",1)});
            var child=((IEnumerable)P(restored,"OwnedMonsters")).Cast<object>().Single();
            Assert.That(F(child,"MonsterId"),Is.EqualTo(expected));
            Assert.That(P(child,"TotalPlusValue"),Is.EqualTo(5));
            Assert.That(F(child,"Level"),Is.EqualTo(1));
            Assert.That(F(child,"Exp"),Is.EqualTo(0));
            Assert.That(F(child,"InstanceId"),Is.Not.EqualTo(F(a,"InstanceId")).And.Not.EqualTo(F(b,"InstanceId")));
        }

        [TestCase(4,5)][TestCase(5,4)][TestCase(5,5)][TestCase(5,6)]
        public void HighClassFallbackSupportsHigherRanksAndParentOneTies(int firstClass,int secondClass)
        {
            var a=ScriptableObject.CreateInstance(T("MasterData.MonsterDataSO"));
            var b=ScriptableObject.CreateInstance(T("MasterData.MonsterDataSO"));
            try
            {
                SetF(a,"monsterId","future_first"); SetF(a,"raceId","special"); SetF(a,"classRank",firstClass);
                SetF(b,"monsterId","future_second"); SetF(b,"raceId","angel"); SetF(b,"classRank",secondClass);
                var all=Array.CreateInstance(T("MasterData.MonsterDataSO"),2); all.SetValue(a,0); all.SetValue(b,1);
                object[] args={a,b,all,null,null};
                Assert.That(T("Data.MonsterFusionCatalog").GetMethod("TryResolveNormalRecipe").Invoke(null,args),Is.True);
                Assert.That(args[4],Is.SameAs(secondClass>firstClass?b:a));
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        [Test] public void ExplicitSpecialRecipeWinsBeforeHighClassFallback()
        {
            // Temporary catalog entry represents a future special combination;
            // restore the shared array even if an assertion fails.
            var recipes=(IList)S("Data.MonsterFusionCatalog","GetRecipes"); var original=recipes[0];
            const string first="monster_magic_sword_saint_luciel", second="monster_dragon_sword_saint_agito", expected="monster_seraph_michael";
            try
            {
                recipes[0]=Activator.CreateInstance(T("Data.MonsterFusionRecipeDefinition"),new object[]{first,second,expected,Enum.Parse(T("Data.MonsterFusionRecipeType"),"Special"),true});
                foreach(bool reverse in new[]{false,true})
                {
                    var p=Profile(); var a=Add(p,reverse?second:first,80); var b=Add(p,reverse?first:second,80);
                    var preview=S("Data.MonsterFusionService","PreviewFusion",p,F(a,"InstanceId"),F(b,"InstanceId"),master);
                    Assert.That(P(P(preview,"Recipe"),"ResultMonsterId"),Is.EqualTo(expected));
                    var result=S("Data.MonsterFusionService","Fuse",p,F(a,"InstanceId"),F(b,"InstanceId"),master,false);
                    Assert.That(F(P(result,"CreatedMonster"),"MonsterId"),Is.EqualTo(expected));
                    Assert.That(F(P(result,"CreatedMonster"),"Level"),Is.EqualTo(1));
                    Assert.That(F(P(result,"CreatedMonster"),"Exp"),Is.EqualTo(0));
                }
            }
            finally { recipes[0]=original; }
        }

        [TestCase("level")][TestCase("locked")][TestCase("favorite")]
        public void HighClassFallbackStillEnforcesParentRequirements(string reason)
        {
            var p=Profile(); var a=Add(p,"monster_magic_sword_saint_luciel",80); var b=Add(p,"monster_seraph_michael",80);
            if(reason=="level") SetF(a,"Level",79);
            if(reason=="locked") SetF(a,"IsLocked",true);
            if(reason=="favorite") SetF(a,"IsFavorite",true);
            string before=JsonUtility.ToJson(Call(p,"ToSaveData",1));
            var result=S("Data.MonsterFusionService","Fuse",p,F(a,"InstanceId"),F(b,"InstanceId"),master,false);
            Assert.That(P(result,"CanFuse"),Is.False);
            Assert.That(JsonUtility.ToJson(Call(p,"ToSaveData",1)),Is.EqualTo(before));
        }

        [Test] public void SoulTreeHasNoPurchasesOrBonusesWithLegacySave()
        {
            var p=Profile();
            var levels=(IList)P(p,"RebirthSkillLevels");
            var entry=Activator.CreateInstance(T("Save.RebirthSkillLevelData"));
            SetF(entry,"SkillId","attack_pact"); SetF(entry,"Level",5); levels.Add(entry);
            Assert.That(((IEnumerable)T("Data.RebirthSkillCatalog").GetProperty("Definitions").GetValue(null)).Cast<object>(),Is.Empty);
            Assert.That(Call(p,"GetAttackMultiplier"),Is.EqualTo(1f));
        }

        private static void AssertSeparated(RectTransform[] rects,Transform parent,float minGap)
        {
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<rects.Length;i++) for(int j=i+1;j<rects.Length;j++)
            {
                // Only target rectangles, not decoration/glows attached below them.
                var a=rects[i]; var b=rects[j];
                var ap=(Vector2)parent.InverseTransformPoint(a.TransformPoint(a.rect.min));
                var aq=(Vector2)parent.InverseTransformPoint(a.TransformPoint(a.rect.max));
                var bp=(Vector2)parent.InverseTransformPoint(b.TransformPoint(b.rect.min));
                var bq=(Vector2)parent.InverseTransformPoint(b.TransformPoint(b.rect.max));
                Assert.That(Mathf.Max(Mathf.Max(ap.x-bq.x,bp.x-aq.x),Mathf.Max(ap.y-bq.y,bp.y-aq.y)),Is.GreaterThanOrEqualTo(minGap));
            }
        }
    }
}
