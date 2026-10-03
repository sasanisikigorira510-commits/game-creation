using System;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.UI
{
    public static partial class MonsterStatusDetailPopup
    {
        private static readonly string[] TrainingLabels = { "HP", "攻撃", "魔力", "防御", "魔法防御", "攻撃速度" };
        private static readonly int[] SkillTrainingCosts = { 20, 40, 80, 160 };

        public static void OpenTraining(Transform parent, string instanceId, Action onReturn)
        {
            var monster = GameManager.Instance?.PlayerProfile?.GetOwnedMonster(instanceId);
            MasterDataManager.Instance?.Initialize();
            var data = monster != null ? MasterDataManager.Instance?.GetMonsterData(monster.MonsterId) : null;
            if (parent != null && data != null) ShowTraining(parent, instanceId, data, onReturn);
        }

        private static void ShowTraining(Transform parent, string instanceId, MonsterDataSO data,
            Action refreshDetail, string message = "", bool waiting = false)
        {
            if (parent == null) return;
            var old = parent.Find("MonsterTrainingPopup");
            if (old != null) { old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
            var profile = GameManager.Instance?.PlayerProfile;
            var monster = profile?.GetOwnedMonster(instanceId);
            if (monster == null) return;
            var root = CreateUiObject("MonsterTrainingPopup", parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shade = root.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .88f); shade.raycastTarget = true;
            Font font = GetRuntimeFont();
            var panel = CreateRawPanel("TrainingPanel", root.transform, Resources.Load<Texture2D>(PanelTexturePath),
                Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero,
                new Vector2(970, 1200), Color.white);
            panel.GetComponent<RawImage>().raycastTarget = true;
            CreateText("TrainingTitle", panel.transform, font, data.monsterName + "の修練", 32, FontStyle.Bold,
                Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 490),
                new Vector2(790, 58), TextAnchor.MiddleCenter, TextMain);
            TrainingItemIcon(panel.transform, "UI/DailyChallenge/TrainingDrop", new Vector2(-380, 406));
            CreateText("TrainingWallet", panel.transform, font, $"修練の雫  {profile.TrainingDrops}個", 28, FontStyle.Bold,
                Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-65, 406),
                new Vector2(460, 55), TextAnchor.MiddleLeft, new Color(.5f, 1f, 1f));
            CreateText("TrainingRule", panel.transform, font, "各項目Lv10まで・配合では親の高い修練Lvを継承", 22, FontStyle.Bold,
                Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 342),
                new Vector2(820, 45), TextAnchor.MiddleCenter, TextSub);
            bool fixedParty = profile.DailyChallenges?.ActiveRun?.IsActive == true;
            bool locked = waiting || OnlinePlayerData.Busy || fixedParty;
            BattleUnitStats before = MonsterBattleStatsFactory.Create(profile, monster, data);
            for (int i = 0; i < 6; i++)
            {
                var stat = (MonsterTrainingStatType)i;
                int level = MonsterTrainingService.GetLevel(monster, stat);
                int cost = MonsterTrainingService.GetCostForNextLevel(level);
                float y = 258 - i * 96;
                CreateText("TrainingLabel_" + i, panel.transform, font, $"{TrainingLabels[i]}  Lv{level}/10", 24, FontStyle.Bold,
                    Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-245, y),
                    new Vector2(295, 56), TextAnchor.MiddleLeft, TextMain);
                var preview = JsonUtility.FromJson<OwnedMonsterData>(JsonUtility.ToJson(monster));
                SetPreviewTrainingLevel(preview, stat, Math.Min(10, level + 1));
                BattleUnitStats after = MonsterBattleStatsFactory.Create(profile, preview, data);
                string change = TrainingStatValue(before, stat) + " → " + TrainingStatValue(after, stat);
                CreateText("TrainingPreview_" + i, panel.transform, font, change, 24, FontStyle.Bold,
                    Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(18, y),
                    new Vector2(205, 56), TextAnchor.MiddleCenter, TextGreen);
                var button = CreateActionButton("Train_" + stat, panel.transform, font,
                    level >= 10 ? "最大Lv" : $"雫{cost}個で強化", Vector2.one * .5f, Vector2.one * .5f,
                    Vector2.one * .5f, new Vector2(283, y), new Vector2(235, 64), new Color(.08f, .30f, .34f));
                button.GetComponent<Button>().interactable = !locked && level < 10 && profile.TrainingDrops >= cost;
                button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ShowTraining(parent, instanceId, data, refreshDetail, "強化を保存しています…", true);
                    OnlinePlayerData.TrainMonster(instanceId, stat, (ok, error) =>
                    {
                        if (parent == null) return;
                        UnityEngine.Object.FindFirstObjectByType<WitchTower.Home.HomeSceneController>()?.RefreshAllPanels();
                        ShowTraining(parent, instanceId, data, refreshDetail,
                            ok ? TrainingLabels[(int)stat] + "を強化しました。" : error ?? "強化を確認できませんでした。");
                    });
                });
            }
            if (data.classRank >= 5)
            {
                int level = MonsterSkillGrowthCatalog.NormalizeLevel(monster.MonsterSkillLevel);
                string skillName = Class5SkillCatalog.Resolve(data.monsterId)?.Name ?? "固有スキル";
                TrainingItemIcon(panel.transform, "UI/DailyChallenge/TrialStarCore", new Vector2(-380, -338));
                CreateText("MonsterSkillTraining", panel.transform, font,
                    $"{skillName}\nLv{level}/5  威力{MonsterSkillGrowthCatalog.GetPowerMultiplier(level):0.0}倍・星核{profile.TrialStarCores}個", 23, FontStyle.Bold,
                    Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-95, -338),
                    new Vector2(455, 62), TextAnchor.MiddleLeft, TextMain);
                int cost = level < 5 ? SkillTrainingCosts[level - 1] : 0;
                var skill = CreateActionButton("TrainMonsterSkill", panel.transform, font,
                    level == 5 ? "最大Lv" : $"星核{cost}個で強化", Vector2.one * .5f, Vector2.one * .5f,
                    Vector2.one * .5f, new Vector2(283, -338), new Vector2(235, 64), new Color(.28f, .13f, .4f));
                skill.GetComponent<Button>().interactable = !locked && level < 5 && profile.TrialStarCores >= cost;
                skill.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ShowTraining(parent, instanceId, data, refreshDetail, "固有スキルを強化しています…", true);
                    OnlinePlayerData.TrainMonsterSkill(instanceId, (ok, error) =>
                    {
                        if (parent != null) ShowTraining(parent, instanceId, data, refreshDetail,
                            ok ? "固有スキルを強化しました。" : error ?? "強化を確認できませんでした。");
                    });
                });
            }
            string info = fixedParty ? "挑戦中の試練を終了してから修練できます。" :
                string.IsNullOrEmpty(message) ? "修練の雫はデイリー試練の１〜５階層で獲得できます。" : message;
            CreateText("TrainingMessage", panel.transform, font, info, 22, FontStyle.Bold,
                Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -420),
                new Vector2(820, 70), TextAnchor.MiddleCenter, TextSub);
            var close = CreateActionButton("CloseTraining", panel.transform, font, "戻る", Vector2.one * .5f,
                Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -504), new Vector2(290, 72), new Color(.15f, .21f, .29f));
            close.GetComponent<Button>().interactable = !waiting;
            close.GetComponent<Button>().onClick.AddListener(() => { UnityEngine.Object.Destroy(root); refreshDetail?.Invoke(); });
            root.transform.SetAsLastSibling();
        }

        private static void TrainingItemIcon(Transform parent, string resource, Vector2 position)
        {
            var obj = CreateUiObject(resource.EndsWith("TrainingDrop") ? "TrainingDropIcon" : "TrialStarCoreIcon", parent);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(68, 68);
            var image = obj.AddComponent<Image>(); image.sprite = Resources.Load<Sprite>(resource);
            image.preserveAspect = true; image.raycastTarget = false;
        }

        private static string TrainingStatValue(BattleUnitStats stats, MonsterTrainingStatType type) => type switch
        {
            MonsterTrainingStatType.Hp => stats.MaxHp.ToString(), MonsterTrainingStatType.Attack => stats.Attack.ToString(),
            MonsterTrainingStatType.Wisdom => stats.Wisdom.ToString(), MonsterTrainingStatType.Defense => stats.Defense.ToString(),
            MonsterTrainingStatType.MagicDefense => stats.MagicDefense.ToString(), _ => stats.AttackSpeed.ToString("0.###")
        };

        private static void SetPreviewTrainingLevel(OwnedMonsterData monster, MonsterTrainingStatType type, int level)
        {
            switch (type)
            {
                case MonsterTrainingStatType.Hp: monster.TrainingHp = level; break;
                case MonsterTrainingStatType.Attack: monster.TrainingAttack = level; break;
                case MonsterTrainingStatType.Wisdom: monster.TrainingWisdom = level; break;
                case MonsterTrainingStatType.Defense: monster.TrainingDefense = level; break;
                case MonsterTrainingStatType.MagicDefense: monster.TrainingMagicDefense = level; break;
                case MonsterTrainingStatType.AttackSpeed: monster.TrainingAttackSpeed = level; break;
            }
        }
    }
}
