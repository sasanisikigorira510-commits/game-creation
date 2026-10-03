using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchTower.Tests
{
    public sealed class MonsterRecruitWarningRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
        private static Type Service => RuntimeType("WitchTower.Battle.MonsterRecruitService");

        [TestCase("T06", 3, 100, false, "Tutorial")]
        [TestCase("T07", 3, 100, false, "Tutorial")]
        [TestCase("T06", 100, 100, false, "Tutorial")]
        [TestCase("T07B", 3, 100, false, "None")]
        [TestCase("Complete", 3, 100, false, "None")]
        [TestCase("Complete", 100, 100, false, "StorageFull")]
        [TestCase("Complete", 100, 100, true, "None")]
        public void EligibilityDistinguishesTutorialCapacityAndAutoRelease(string step, int count, int limit, bool autoRelease, string expected)
        {
            object profile = CreateProfile(step, count, limit);
            profile.GetType().GetProperty("HasAutoReleaseMonsterUpgrade").SetValue(profile, autoRelease);
            profile.GetType().GetProperty("IsAutoReleaseMonsterUpgradeEnabled").SetValue(profile, autoRelease);
            Assert.That(StartState(profile).ToString(), Is.EqualTo(expected));
            Assert.That(OwnedCount(profile), Is.EqualTo(count));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingProfileIsNotReportedAsFull(bool afterBattleWin)
        {
            object result = Resolve(null, StartState(null), afterBattleWin);
            Assert.That(Read(result, "BlockReason").ToString(), Is.EqualTo("ProfileUnavailable"));
            Assert.That(Read(result, "Summary"), Is.EqualTo("プレイヤーデータを読み込めないため、捕獲できません。"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealStorageLimitStillBlocksRecruitment(bool afterBattleWin)
        {
            object profile = CreateProfile("Complete", 100, 100);
            object result = Resolve(profile, StartState(profile), afterBattleWin);
            Assert.That(Read(result, "BlockReason").ToString(), Is.EqualTo("StorageFull"));
            Assert.That(Read(result, "Summary"), Is.EqualTo("これ以上捕獲できません"));
            Assert.That(Read(result, "Attempted"), Is.False);
            Assert.That(OwnedCount(profile), Is.EqualTo(100));
        }

        [TestCase("T07B")]
        [TestCase("Complete")]
        public void SubsequentBattleIsEligibleAgain(string step)
        {
            object profile = CreateProfile("T06", 3, 100);
            Assert.That(StartState(profile).ToString(), Is.EqualTo("Tutorial"));
            profile.GetType().GetProperty("TutorialStepId").SetValue(profile, step);
            profile.GetType().GetProperty("HasCompletedTutorial").SetValue(profile, step == "Complete");
            object result = Resolve(profile, StartState(profile), false);
            Assert.That(Read(result, "BlockReason").ToString(), Is.EqualTo("None"));
            Assert.That(Read(result, "WasEligible"), Is.True);
            Assert.That(Read(result, "Summary"), Is.EqualTo(string.Empty));
        }

        [TestCase("T06", 3, false)]
        [TestCase("T06", 100, false)]
        [TestCase("Complete", 100, true)]
        public void EnemyDefeatShowsCapacityWarningOnlyForRealCapacityFailure(string step, int count, bool shouldWarn)
        {
            object profile = CreateProfile(step, count, 100);
            WithBattle(profile, (controller, enemy) =>
            {
                Invoke(controller, "TryApplyMonsterRecruitmentForDefeatedEnemy", enemy, false);
                Assert.That(Field(controller, "monsterStorageFullAnnouncementShown"), Is.EqualTo(shouldWarn));
                Assert.That(Field(controller, "activeBattleAnnouncementText"),
                    shouldWarn ? Is.EqualTo("これ以上捕獲できません") : Is.Null.Or.Empty);
                Assert.That(OwnedCount(profile), Is.EqualTo(count));
                if (shouldWarn)
                {
                    Invoke(controller, "TryApplyMonsterRecruitmentForDefeatedEnemy", enemy, false);
                    Assert.That(((ICollection)Field(controller, "battleAnnouncementQueue")).Count, Is.Zero,
                        "Repeated defeats must not requeue the same capacity warning.");
                }
            });
        }

        [Test]
        public void ReachingCapacityDuringBattleReturnsTypedCapacityFailure()
        {
            object profile = CreateProfile("Complete", 3, 100);
            WithBattle(profile, (controller, enemy) =>
            {
                Assert.That(Field(controller, "recruitBlockReasonAtBattleStart").ToString(), Is.EqualTo("None"));
                profile.GetType().GetProperty("MonsterStorageLimit").SetValue(profile, 3);
                Invoke(controller, "TryApplyMonsterRecruitmentForDefeatedEnemy", enemy, false);
                Assert.That(Field(controller, "monsterStorageFullAnnouncementShown"), Is.True);
                Assert.That(Read(Field(controller, "lastRecruitResult"), "BlockReason").ToString(), Is.EqualTo("StorageFull"));
                Assert.That(OwnedCount(profile), Is.EqualTo(3));
            });
        }

        [TestCase("T07B")]
        [TestCase("Complete")]
        public void StartingNextBattleClearsTutorialRestrictionAndAllowsCapture(string nextStep)
        {
            object profile = CreateProfile("T06", 3, 100);
            UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(2332);
                WithBattle(profile, (controller, enemy) =>
                {
                    Assert.That(Field(controller, "recruitBlockReasonAtBattleStart").ToString(), Is.EqualTo("Tutorial"));
                    profile.GetType().GetProperty("TutorialStepId").SetValue(profile, nextStep);
                    profile.GetType().GetProperty("HasCompletedTutorial").SetValue(profile, nextStep == "Complete");
                    Invoke(controller, "PrepareBattleSession");
                    Assert.That(Field(controller, "recruitBlockReasonAtBattleStart").ToString(), Is.EqualTo("None"));
                    for (int i = 0; i < 1000 && OwnedCount(profile) == 3; i++)
                        Invoke(controller, "TryApplyMonsterRecruitmentForDefeatedEnemy", enemy, false);
                    Assert.That(OwnedCount(profile), Is.EqualTo(4), "A subsequent battle must be able to grant a real capture.");
                    Assert.That(Field(controller, "monsterStorageFullAnnouncementShown"), Is.False);
                    Assert.That(Read(Field(controller, "lastRecruitResult"), "Succeeded"), Is.True);
                    Assert.That(Field(controller, "activeBattleAnnouncementText"), Does.StartWith("捕獲成功！"));
                });
            }
            finally
            {
                UnityEngine.Random.state = previousRandom;
            }
        }

        [TestCase(1, false)] [TestCase(10, false)] [TestCase(20, false)]
        [TestCase(30, false)] [TestCase(40, false)] [TestCase(50, false)] [TestCase(60, false)]
        [TestCase(1, true)] [TestCase(10, true)] [TestCase(20, true)]
        [TestCase(30, true)] [TestCase(40, true)] [TestCase(50, true)] [TestCase(60, true)]
        public void AllCapturePathsStartAtLevelOneIncludingBosses(int floor, bool afterBattleWin)
        {
            object profile = CreateProfile("Complete", 1, 100);
            var monsters = (IList)Read(profile, "OwnedMonsters");
            object existing = monsters[0];
            existing.GetType().GetField("Level").SetValue(existing, 17);
            UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(921);
                WithBattle(profile, (controller, enemy) =>
                {
                    Type catalog = RuntimeType("WitchTower.Data.BattleDungeonCatalog");
                    var candidates = (string[])catalog.GetMethod("ResolveRecruitableMonsterIds").Invoke(null, new object[] { floor });
                    Assert.That(candidates, Is.Not.Empty);
                    foreach (string monsterId in candidates)
                    {
                        bool boss = (bool)catalog.GetMethod("IsBossMonsterOnFloor").Invoke(null, new object[] { floor, monsterId });
                        enemy.GetType().GetField("enemyId").SetValue(enemy,
                            catalog.GetMethod("ResolveEnemyIdFromMonsterId").Invoke(null, new object[] { monsterId }));
                        object result = null;
                        for (int attempt = 0; attempt < 20000; attempt++)
                        {
                            result = afterBattleWin
                                ? Service.GetMethod("ResolveAfterBattleWin").Invoke(null, new[] { (object)floor, profile, StartState(profile) })
                                : Service.GetMethod("ResolveAfterEnemyDefeat").Invoke(null, new[] { (object)floor, profile, StartState(profile), enemy, boss });
                            if ((bool)Read(result, "Succeeded")) break;
                        }
                        Assert.That(Read(result, "Succeeded"), Is.True, monsterId);
                        object captured = monsters[monsters.Count - 1];
                        Assert.That(captured.GetType().GetField("Level").GetValue(captured), Is.EqualTo(1), monsterId);
                        Assert.That(captured.GetType().GetField("Exp").GetValue(captured), Is.EqualTo(0));
                        Assert.That(existing.GetType().GetField("Level").GetValue(existing), Is.EqualTo(17));
                        if (!afterBattleWin)
                        {
                            Assert.That(captured.GetType().GetField("MonsterId").GetValue(captured), Is.EqualTo(monsterId));
                            if (boss)
                                foreach (string stat in new[] { "IndividualHp", "IndividualAttack", "IndividualWisdom", "IndividualDefense", "IndividualMagicDefense", "IndividualAttackSpeed" })
                                    Assert.That((int)captured.GetType().GetField(stat).GetValue(captured), Is.GreaterThanOrEqualTo(50));
                        }
                    }
                });
            }
            finally { UnityEngine.Random.state = previousRandom; }
        }

        private static void WithBattle(object profile, Action<Component, ScriptableObject> verify)
        {
            Type gameType = RuntimeType("WitchTower.Managers.GameManager");
            Type masterType = RuntimeType("WitchTower.Managers.MasterDataManager");
            object previousGame = gameType.GetProperty("Instance").GetValue(null);
            object previousMaster = masterType.GetProperty("Instance").GetValue(null);
            var root = new GameObject("RecruitWarningRegression");
            root.SetActive(false);
            ScriptableObject enemy = null;
            try
            {
                Component game = root.AddComponent(gameType);
                gameType.GetProperty("Instance").SetValue(null, game);
                gameType.GetProperty("PlayerProfile").SetValue(game, profile);
                Component master = root.AddComponent(masterType);
                masterType.GetProperty("Instance").SetValue(null, master);
                masterType.GetMethod("Initialize").Invoke(master, null);
                Type catalog = RuntimeType("WitchTower.Data.BattleDungeonCatalog");
                string monsterId = ((string[])catalog.GetMethod("ResolveRecruitableMonsterIds").Invoke(null, new object[] { 1 }))[0];
                Assert.That(masterType.GetMethod("GetMonsterData").Invoke(master, new object[] { monsterId }), Is.Not.Null);
                enemy = ScriptableObject.CreateInstance(RuntimeType("WitchTower.MasterData.EnemyDataSO"));
                enemy.GetType().GetField("enemyId").SetValue(enemy,
                    catalog.GetMethod("ResolveEnemyIdFromMonsterId").Invoke(null, new object[] { monsterId }));
                Component controller = root.AddComponent(RuntimeType("WitchTower.Battle.BattleSceneController"));
                var canvas = new GameObject("RegressionCanvas", typeof(RectTransform), typeof(Canvas));
                canvas.transform.SetParent(root.transform, false);
                controller.GetType().GetField("minimalCanvasRoot", PrivateInstance).SetValue(controller, canvas);
                controller.GetType().GetField("currentFloor", PrivateInstance).SetValue(controller, 1);
                Invoke(controller, "PrepareBattleSession");
                verify(controller, enemy);
            }
            finally
            {
                gameType.GetProperty("Instance").SetValue(null, previousGame);
                masterType.GetProperty("Instance").SetValue(null, previousMaster);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object StartState(object profile) => Service.GetMethod("GetRecruitBlockReason").Invoke(null, new[] { profile });
        private static object Read(object value, string property) => value.GetType().GetProperty(property).GetValue(value);
        private static object Field(object value, string field) => value.GetType().GetField(field, PrivateInstance).GetValue(value);
        private static object Invoke(object value, string method, params object[] args) =>
            value.GetType().GetMethod(method, PrivateInstance).Invoke(value, args);

        [TestCase("T06", false)]
        [TestCase("T07", false)]
        [TestCase("T06", true)]
        [TestCase("T07", true)]
        public void TutorialRestrictionNeverClaimsStorageIsFull(string step, bool afterBattleWin)
        {
            object profile = CreateProfile(step, 3, 100);
            object startState = Service.GetMethod("GetRecruitBlockReason").Invoke(null, new[] { profile });
            object result = Resolve(profile, startState, afterBattleWin);
            Assert.That((bool)result.GetType().GetProperty("Attempted").GetValue(result), Is.False);
            string summary = (string)result.GetType().GetProperty("Summary").GetValue(result);
            Assert.That(summary, Does.Contain("チュートリアル"));
            Assert.That(summary, Does.Not.Contain("上限").And.Not.Contain("これ以上捕獲できません"));
            Assert.That(OwnedCount(profile), Is.EqualTo(3));
        }

        [TestCase("T06")]
        [TestCase("T07")]
        public void BattleResultPreservesTutorialReasonAfterProgressAdvances(string step)
        {
            object profile = CreateProfile(step, 3, 100);
            object startState = Service.GetMethod("GetRecruitBlockReason").Invoke(null, new[] { profile });
            profile.GetType().GetProperty("TutorialStepId").SetValue(profile, "T07B");
            var root = new GameObject("RecruitSummaryRegression");
            root.SetActive(false); // Do not run the scene controller's editor preview.
            try
            {
                Type controllerType = RuntimeType("WitchTower.Battle.BattleSceneController");
                Component controller = root.AddComponent(controllerType);
                controllerType.GetField("recruitBlockReasonAtBattleStart", PrivateInstance).SetValue(controller, startState);
                string summary = (string)controllerType.GetMethod("BuildMonsterRecruitSummary", PrivateInstance).Invoke(controller, null);
                Assert.That(summary, Does.Contain("チュートリアル"));
                Assert.That(summary, Does.Not.Contain("これ以上捕獲できません"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object Resolve(object profile, object startState, bool afterBattleWin)
        {
            return afterBattleWin
                ? Service.GetMethod("ResolveAfterBattleWin").Invoke(null, new[] { (object)1, profile, startState })
                : Service.GetMethod("ResolveAfterEnemyDefeat").Invoke(null, new[] { (object)1, profile, startState, null, false });
        }

        private static object CreateProfile(string step, int ownedCount, int limit)
        {
            Type saveType = RuntimeType("WitchTower.Save.PlayerSaveData");
            object save = saveType.GetMethod("CreateDefault").Invoke(null, null);
            saveType.GetField("TutorialStepId").SetValue(save, step);
            saveType.GetField("HasCompletedTutorial").SetValue(save, step == "Complete");
            saveType.GetField("InitialTutorialSummonCount").SetValue(save, 3);
            saveType.GetField("MonsterStorageLimit").SetValue(save, limit);
            IList monsters = (IList)saveType.GetField("OwnedMonsters").GetValue(save);
            Type monsterType = RuntimeType("WitchTower.Save.OwnedMonsterData");
            for (int i = 0; i < ownedCount; i++)
            {
                object monster = Activator.CreateInstance(monsterType);
                monsterType.GetField("InstanceId").SetValue(monster, "recruit-regression-" + i);
                monsterType.GetField("MonsterId").SetValue(monster, "monster_dragon_whelp");
                monsters.Add(monster);
            }
            return Activator.CreateInstance(RuntimeType("WitchTower.Data.PlayerProfile"), new[] { save });
        }

        private static int OwnedCount(object profile) =>
            ((IList)profile.GetType().GetProperty("OwnedMonsters").GetValue(profile)).Count;
    }
}
