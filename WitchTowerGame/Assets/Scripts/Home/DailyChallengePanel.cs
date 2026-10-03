using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;

namespace WitchTower.Home
{
    public sealed class DailyChallengePanel : MonoBehaviour
    {
        private Text status;
        private Text[] counts;
        private Button[] startButtons;
        private Text[] startLabels;
        private Button resumeButton;
        private Text resumeLabel;
        private DailyChallengeState state;
        private bool busy;
        private Action<string> start;
        private Action resume;
        private Action close;
        private Font font;
        public bool IsVisible => gameObject.activeInHierarchy;
        private static readonly Color Ink = new Color(.94f, .95f, 1f);
        private static readonly Color Gold = new Color(1f, .88f, .48f);

        public void Show(DailyChallengeState value, Font runtimeFont, Action<string> onStart, Action onResume, Action onClose)
        {
            font = runtimeFont; start = onStart; resume = onResume; close = onClose;
            if (status == null) Build();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh(value);
        }

        public void SetBusy(bool value, string message = null)
        {
            busy = value;
            Refresh(state);
            if (!string.IsNullOrEmpty(message)) status.text = message;
        }

        public void Refresh(DailyChallengeState value)
        {
            state = value;
            if (status == null) return;
            string day = value?.Day;
            status.text = busy ? "挑戦記録を確認しています…" :
                (string.IsNullOrEmpty(day) ? "各試練は1日2回。毎日0時に入場回数を更新します。" :
                    day + " の試練  ·  毎日0時更新（日本時間）");
            bool active = value?.ActiveRun?.IsActive == true;
            for (int i = 0; i < DailyChallengeCatalog.Modes.Length; i++)
            {
                string mode = DailyChallengeCatalog.Modes[i];
                var entry = value?.Modes?.FirstOrDefault(m => m != null && m.Mode == mode);
                int remaining = DailyChallengeCatalog.Remaining(entry);
                counts[i].text = "本日の入場: 残り " + remaining + "/2  ·  最高突破: " + (entry?.BestStage ?? 0) + "/10";
                startButtons[i].interactable = !busy && value != null && remaining > 0 && !active;
                startLabels[i].text = remaining == 0 ? "本日分 終了" : active ? "再開中の挑戦があります" : "この編成で挑戦";
            }
            resumeButton.gameObject.SetActive(active);
            resumeButton.interactable = active && !busy;
            if (active) resumeLabel.text = DailyChallengeCatalog.Label(value.ActiveRun.Mode) + " 第" +
                Math.Min(10, value.ActiveRun.NextStage) + "段階から再開";
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var shade = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            shade.color = new Color(.015f, .018f, .035f, .96f); shade.raycastTarget = true;
            var card = Rect("DailyChallengeCard", transform, new Vector2(980f, 1530f), Vector2.zero);
            var background = card.gameObject.AddComponent<Image>(); background.color = new Color(.065f, .095f, .17f, .99f);
            TextAt("Title", card, "デイリー試練", 49, new Vector2(0, 692), new Vector2(810, 74), Gold);
            ButtonAt("Close", card, "閉じる", new Vector2(0, -692), new Vector2(300, 82), () =>
            { if (busy) return; gameObject.SetActive(false); close?.Invoke(); });
            status = TextAt("Status", card, string.Empty, 26, new Vector2(0, 617), new Vector2(890, 70), Ink);
            counts = new Text[2]; startButtons = new Button[2]; startLabels = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                string mode = DailyChallengeCatalog.Modes[i];
                float y = 432f - i * 277f;
                var section = Rect(mode + "Card", card, new Vector2(884, 258), new Vector2(0, y));
                section.gameObject.AddComponent<Image>().color = new Color(.12f, .17f, .28f);
                TextAt("Mode", section, DailyChallengeCatalog.Label(mode), 36, new Vector2(-28, 87), new Vector2(700, 52), Gold);
                TextAt("Description", section, DailyChallengeCatalog.Description(mode), 25, new Vector2(0, 31), new Vector2(826, 58), Ink);
                counts[i] = TextAt("Entries", section, string.Empty, 23, new Vector2(0, -26), new Vector2(826, 45), Ink);
                startButtons[i] = ButtonAt("Start", section, "この編成で挑戦", new Vector2(0, -85), new Vector2(610, 68), () => start?.Invoke(DailyChallengeCatalog.Modes[index]));
                startLabels[i] = startButtons[i].GetComponentInChildren<Text>();
            }
            resumeButton = ButtonAt("Resume", card, "挑戦を再開", new Vector2(0, -44), new Vector2(840, 73), () => resume?.Invoke());
            resumeLabel = resumeButton.GetComponentInChildren<Text>();
            TextAt("Rules", card, "全10段階・時間制限なし・入場時の編成固定\nHP・戦闘不能・攻撃待ち時間を持ち越します。", 25,
                new Vector2(0, -131), new Vector2(890, 82), Ink);
            TextAt("RewardTitle", card, "毎回、突破した段階の報酬を獲得", 29, new Vector2(0, -207), new Vector2(830, 55), Gold);
            string rewards = string.Join("\n", Enumerable.Range(1, 10).Select(stage =>
                stage + "段階: " + (DailyChallengeCatalog.Drops(stage) > 0 ? "雫 " + DailyChallengeCatalog.Drops(stage) : "星核 " + DailyChallengeCatalog.StarCores(stage)) +
                (stage == 5 ? "・星核 1" : string.Empty) + "  ｜  金 " + DailyChallengeCatalog.Gold(stage) + "・参加EXP " + DailyChallengeCatalog.MonsterExp(stage)));
            TextAt("Rewards", card, rewards, 24, new Vector2(0, -418), new Vector2(900, 370), Ink);
            Icon(card, "TrainingDrop", "UI/DailyChallenge/TrainingDrop", new Vector2(-368, -609));
            TextAt("Drops", card, "修練の雫＝能力修練", 22, new Vector2(-188, -609), new Vector2(300, 52), Ink);
            Icon(card, "TrialStarCore", "UI/DailyChallenge/TrialStarCore", new Vector2(62, -609));
            TextAt("Cores", card, "星核＝クラス5技能の修練", 22, new Vector2(268, -609), new Vector2(358, 52), Ink);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private Text TextAt(string name, Transform parent, string text, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            var rect = Rect(name, parent, dimensions, position); var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.resizeTextForBestFit = true; label.resizeTextMaxSize = size; label.resizeTextMinSize = size - 4;
            label.supportRichText = false; return label;
        }
        private Button ButtonAt(string name, Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, size, position); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.20f, .29f, .44f); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.onClick.AddListener(action);
            TextAt("Label", rect, text, 28, Vector2.zero, size - new Vector2(20, 8), Ink); return button;
        }
        private static void Icon(Transform parent, string name, string path, Vector2 position)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null) return;
            var icon = Rect(name, parent, new Vector2(62, 62), position).gameObject.AddComponent<Image>();
            icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        }
    }
}
