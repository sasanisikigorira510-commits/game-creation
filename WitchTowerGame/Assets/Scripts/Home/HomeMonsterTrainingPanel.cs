using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class HomeMonsterTrainingPanel : MonoBehaviour
    {
        public const int PageSize = 10;
        public bool IsVisible => gameObject.activeInHierarchy;
        private Font font;
        private Action onClose;
        private Text drops, cores, status, pageLabel, empty;
        private InputField search;
        private Transform rosterRoot;
        private RectTransform pagePanel;
        private Button previous, next;
        private readonly List<Button> classButtons = new List<Button>();
        private int page, selectedClass;
        private static readonly Color Ink = new Color(.94f, .97f, 1f);
        private static readonly Color Gold = new Color(1f, .87f, .48f);

        public void Show(Font runtimeFont, Action close)
        {
            font = runtimeFont; onClose = close;
            if (rosterRoot == null) Build();
            gameObject.SetActive(true); transform.SetAsLastSibling(); FitPage(); Refresh();
        }

        public void Refresh()
        {
            var profile = GameManager.Instance?.PlayerProfile;
            drops.text = "修練の雫  " + (profile?.TrainingDrops ?? 0) + "個";
            cores.text = "試練の星核  " + (profile?.TrialStarCores ?? 0) + "個";
            status.text = profile?.DailyChallenges?.ActiveRun?.IsActive == true
                ? "試練を終了すると修練できます。" : "各項目Lv10まで。配合では親の高い修練Lvを継承します。";
            MasterDataManager.Instance?.Initialize();
            var party = new HashSet<string>(profile?.PartyMonsterInstanceIds ?? new List<string>());
            string query = (search.text ?? string.Empty).Trim();
            var entries = (profile?.OwnedMonsters ?? new List<OwnedMonsterData>())
                .Where(m => m != null)
                .Select(m => new { Owned = m, Data = MasterDataManager.Instance?.GetMonsterData(m.MonsterId) })
                .Where(m => m.Data != null && (selectedClass == 0 || m.Data.classRank == selectedClass) &&
                    (query.Length == 0 || m.Data.monsterName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(m => party.Contains(m.Owned.InstanceId)).ToList();
            int pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = rosterRoot.childCount - 1; i >= 0; i--)
            {
                var child = rosterRoot.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            int index = 0;
            foreach (var entry in entries.Skip(page * PageSize).Take(PageSize))
            {
                AddMonster(entry.Owned, entry.Data, party.Contains(entry.Owned.InstanceId), index++);
            }
            empty.gameObject.SetActive(entries.Count == 0);
            empty.text = query.Length > 0 || selectedClass > 0 ? "条件に合うモンスターがいません。" : "仲間にしたモンスターがここに表示されます。";
            pageLabel.text = $"{page + 1} / {pages} ページ  ・  {entries.Count}体";
            previous.interactable = page > 0; next.interactable = page + 1 < pages;
            for (int i = 0; i < classButtons.Count; i++)
                classButtons[i].GetComponent<Image>().color = i == selectedClass ? new Color(.1f, .43f, .46f) : new Color(.14f, .21f, .30f);
        }

        private void AddMonster(OwnedMonsterData owned, MonsterDataSO data, bool inParty, int index)
        {
            var card = ButtonAt("TrainingMonster_" + owned.InstanceId, rosterRoot, string.Empty,
                new Vector2(index % 2 == 0 ? -185 : 185, 314 - index / 2 * 184), new Vector2(350, 170),
                () => MonsterStatusDetailPopup.OpenTraining(transform, owned.InstanceId, Refresh));
            var portrait = Rect("Portrait", card.transform, new Vector2(90, 118), new Vector2(-120, 0)).gameObject.AddComponent<Image>();
            portrait.sprite = BattleVisualResolver.LoadSprite(data.portraitResourcePath);
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            TextAt("Name", card.transform, data.monsterName, 24, new Vector2(45, 47), new Vector2(230, 66), Ink, TextAnchor.MiddleLeft);
            TextAt("Level", card.transform, $"クラス{data.classRank}・Lv{owned.Level}" + (inParty ? "・編成中" : ""), 21,
                new Vector2(45, 0), new Vector2(230, 38), Gold, TextAnchor.MiddleLeft);
            int total = owned.TrainingHp + owned.TrainingAttack + owned.TrainingWisdom + owned.TrainingDefense + owned.TrainingMagicDefense + owned.TrainingAttackSpeed;
            string progress = $"修練 合計{total}/60" + (data.classRank >= 5 ? $"\n固有スキル Lv{MonsterSkillGrowthCatalog.NormalizeLevel(owned.MonsterSkillLevel)}/5" : "");
            TextAt("TrainingProgress", card.transform, progress, 21, new Vector2(45, -47), new Vector2(230, 50), Ink, TextAnchor.MiddleLeft);
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .85f);
            var panel = pagePanel = Rect("TrainingPage", transform, new Vector2(980, 1660), Vector2.zero);
            var art = panel.gameObject.AddComponent<Image>();
            art.sprite = Resources.Load<Sprite>("UI/Training/TrainingPagePanelImage2");
            art.color = Color.white;
            TextAt("Title", panel, "モンスター修練", 40, new Vector2(0, 745), new Vector2(720, 66), Gold);
            TextAt("Subtitle", panel, "修練するモンスターを選んでください", 25, new Vector2(0, 694), new Vector2(720, 48), Ink);
            Icon(panel, "TrainingDropIcon", "UI/DailyChallenge/TrainingDrop", new Vector2(-324, 635));
            drops = TextAt("TrainingDrops", panel, "", 25, new Vector2(-143, 635), new Vector2(274, 50), Ink, TextAnchor.MiddleLeft);
            Icon(panel, "TrialStarCoreIcon", "UI/DailyChallenge/TrialStarCore", new Vector2(30, 635));
            cores = TextAt("TrialStarCores", panel, "", 25, new Vector2(220, 635), new Vector2(274, 50), Ink, TextAnchor.MiddleLeft);
            var input = Rect("TrainingSearch", panel, new Vector2(720, 62), new Vector2(0, 562));
            input.gameObject.AddComponent<Image>().color = new Color(.06f, .12f, .19f);
            search = input.gameObject.AddComponent<InputField>();
            search.textComponent = TextAt("Text", input, "", 26, Vector2.zero, new Vector2(682, 54), Ink, TextAnchor.MiddleLeft);
            search.placeholder = TextAt("Placeholder", input, "モンスター名で検索", 26, Vector2.zero, new Vector2(682, 54), new Color(.6f, .7f, .8f), TextAnchor.MiddleLeft);
            search.onValueChanged.AddListener(_ => { page = 0; Refresh(); });
            for (int i = 0; i <= 5; i++)
            {
                int rank = i;
                classButtons.Add(ButtonAt("ClassFilter_" + i, panel, i == 0 ? "すべて" : "クラス" + i,
                    new Vector2(-305 + i * 122, 486), new Vector2(110, 58), () => { selectedClass = rank; page = 0; Refresh(); }));
            }
            status = TextAt("Rules", panel, "", 22, new Vector2(0, 435), new Vector2(720, 43), Ink);
            rosterRoot = Rect("MonsterRoster", panel, new Vector2(720, 1000), Vector2.zero);
            empty = TextAt("EmptyRoster", panel, "", 28, new Vector2(0, 0), new Vector2(720, 90), Ink);
            previous = ButtonAt("TrainingPreviousPage", panel, "前へ", new Vector2(-274, -544), new Vector2(144, 52), () => { page--; Refresh(); });
            next = ButtonAt("TrainingNextPage", panel, "次へ", new Vector2(274, -544), new Vector2(144, 52), () => { page++; Refresh(); });
            pageLabel = TextAt("PageCount", panel, "", 25, new Vector2(0, -544), new Vector2(340, 52), Ink);
            TextAt("SelectionHint", panel, "仲間をタップして能力・固有スキルを修練", 22,
                new Vector2(0, -595), new Vector2(720, 36), Ink);
            ButtonAt("CloseTrainingPage", panel, "ホームへ戻る", new Vector2(0, -653), new Vector2(340, 64), Close);
        }

        public void Close()
        {
            if (transform.Find("MonsterTrainingPopup") != null) return;
            gameObject.SetActive(false); onClose?.Invoke();
        }

        private void OnRectTransformDimensionsChange() => FitPage();

        private void FitPage()
        {
            if (pagePanel == null) return;
            var area = ((RectTransform)transform).rect;
            if (area.width <= 0 || area.height <= 0) return;
            float scale = Mathf.Min(1f, Mathf.Max(1, area.width - 40) / 980f, Mathf.Max(1, area.height - 80) / 1660f);
            pagePanel.localScale = Vector3.one * scale;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            var popup = transform.Find("MonsterTrainingPopup");
            if (popup == null) { Close(); return; }
            var back = popup.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "CloseTraining");
            if (back != null && back.interactable) back.onClick.Invoke();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private Text TextAt(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent, dimensions, position).gameObject.AddComponent<Text>();
            label.font = font; label.text = value; label.fontSize = size; label.fontStyle = FontStyle.Bold;
            label.color = color; label.alignment = alignment; label.supportRichText = false; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Math.Max(20, size - 3); label.resizeTextMaxSize = size;
            return label;
        }
        private Button ButtonAt(string name, Transform parent, string title, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = Rect(name, parent, size, position).gameObject;
            var surface = obj.AddComponent<Image>(); surface.color = new Color(.10f, .20f, .27f, .97f);
            var button = obj.AddComponent<Button>(); button.targetGraphic = surface; button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(title)) TextAt("Label", obj.transform, title, 26, Vector2.zero, size - new Vector2(12, 6), Ink);
            return button;
        }
        private static void Icon(Transform parent, string name, string path, Vector2 position)
        {
            var image = Rect(name, parent, new Vector2(58, 58), position).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(path); image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
