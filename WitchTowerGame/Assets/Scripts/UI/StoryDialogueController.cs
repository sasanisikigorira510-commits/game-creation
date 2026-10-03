using System;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Monetization;

namespace WitchTower.UI
{
    // A scene-owned reader. Only dialogue progress is persisted here; all
    // gameplay transactions stay in the summon/battle/tutorial services.
    public sealed class StoryDialogueController : MonoBehaviour
    {
        private const int DialogueFontSize = 39;
        private static StoryDialogueController current;
        private StoryDialogueDefinition story;
        private PlayerProfile profile;
        private Action finished;
        private bool readOnly;
        private bool closing;
        private bool archive;
        private bool returnToArchive;
        private ScrollRect archiveScroll;
        private float archiveScrollPosition = 1f;
        private int lineIndex;
        private float lineStartedAt;
        private float lastInputAt;
        private bool revealAll;
        private Image speakerPortrait;
        private Image garzaAction;
        private string garzaPose;
        private CanvasGroup portraitGroup;
        private string portraitSpeaker;
        private float portraitStartedAt;
        private Text speaker;
        private Text body;
        private Text counter;
        private Text nextLabel;
        private RectTransform safeRoot;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private Font font;
        private static readonly Color Ink = new Color(.94f, .94f, .91f);
        private static readonly Color Gold = new Color(.94f, .79f, .46f);

        public static bool IsShowing => current != null && !current.closing;
        public static string CurrentEventId => IsShowing && current.story != null ? current.story.EventId : string.Empty;
        public int CurrentLineIndex => lineIndex;

        public static bool TryShow(string eventId, Action onFinished = null)
        {
            var data = StoryDialogueCatalog.Find(eventId);
            var player = GameManager.Instance?.PlayerProfile;
            if (!Application.isPlaying || IsShowing || data == null || player == null ||
                StoryTutorialService.HasSeenStory(player, eventId)) return false;
            if (StoryDialogueProgress.Begin(player, eventId))
                SaveManager.Instance?.SaveCurrentGame();
            var reader = Create();
            reader.profile = player;
            reader.story = data;
            reader.finished = onFinished;
            reader.lineIndex = Mathf.Clamp(player.StoryDialogueLineIndex, 0, data.Lines.Length - 1);
            reader.BuildReader();
            return true;
        }

        // Preview and archive readers never begin/advance/complete persisted
        // progress, including when viewing a chapter previously skipped.
        public static bool TryShowPreview(string eventId, Action onFinished = null)
        {
            var data = StoryDialogueCatalog.Find(eventId);
            if (!Application.isPlaying || IsShowing || data == null) return false;
            var reader = Create();
            reader.readOnly = true;
            reader.story = data;
            reader.finished = onFinished;
            reader.BuildReader();
            return true;
        }

        public static bool TryShowArchive()
        {
            var player = GameManager.Instance?.PlayerProfile;
            if (!Application.isPlaying || IsShowing || player == null || !player.HasCompletedTutorial ||
                StoryDialogueProgress.GetPendingHomeDialogue(player) != null) return false;
            var reader = Create();
            reader.profile = player;
            reader.readOnly = true;
            reader.archive = true;
            reader.BuildArchive();
            return true;
        }

        public static void Dismiss()
        {
            if (IsShowing) current.Close(false);
        }

        private static StoryDialogueController Create()
        {
            var root = new GameObject("StoryDialogueOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = .5f;
            current = root.AddComponent<StoryDialogueController>();
            current.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            current.lastInputAt = Time.unscaledTime;
            return current;
        }

        private void BuildBase()
        {
            var veil = Rect("StoryVeil", transform, Vector2.zero, Vector2.one);
            var image = veil.gameObject.AddComponent<Image>();
            image.color = new Color(.015f, .025f, .045f, .92f);
            image.raycastTarget = true;
            safeRoot = Rect("StorySafeArea", transform, Vector2.zero, Vector2.one);
            ApplySafeArea();
        }

        private void BuildReader()
        {
            BuildBase();
            Label("StorySubtitle", safeRoot, story.Subtitle, 28, new Vector2(.08f,.94f), new Vector2(.92f,.975f), Gold);
            Label("StoryTitle", safeRoot, story.Title, 46, new Vector2(.08f,.885f), new Vector2(.92f,.94f), Ink);
            BuildSpeakerPortrait();
            var actionRect = Rect("StoryGarzaAction", safeRoot,
                new Vector2(.09f,.615f), new Vector2(.91f,.875f));
            garzaAction = actionRect.gameObject.AddComponent<Image>();
            garzaAction.preserveAspect = true;
            garzaAction.raycastTarget = false;
            garzaAction.gameObject.SetActive(false);
            var panel = Rect("StoryDialoguePanel", safeRoot, new Vector2(.045f,.26f), new Vector2(.955f,.65f));
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/AudioSettings/SettingsPanelFrameImage2");
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            speaker = Label("StoryDialogueSpeaker", panel, "", 39, new Vector2(.12f,.76f), new Vector2(.88f,.88f), Gold, TextAnchor.MiddleLeft);
            body = Label("StoryDialogueBody", panel, "", DialogueFontSize, new Vector2(.12f,.31f), new Vector2(.88f,.73f), Ink, TextAnchor.UpperLeft);
            body.lineSpacing = 1.35f;
            // The typewriter changes the string every frame. Best-fit would resize
            // already visible characters as the sentence grows, so every line uses
            // the same font size from its first character through the final reveal.
            body.resizeTextForBestFit = false;
            counter = Label("StoryDialogueCounter", panel, "", 26, new Vector2(.12f,.14f), new Vector2(.35f,.24f), new Color(.65f,.72f,.81f), TextAnchor.MiddleLeft);
            var next = Button("StoryDialogueNext", panel, "次へ", new Vector2(.56f,.13f), new Vector2(.88f,.27f), NextLine);
            nextLabel = next.GetComponentInChildren<Text>();
            Button("StoryDialogueSkip", safeRoot, readOnly ? "回想を閉じる" : "スキップ",
                new Vector2(.62f,.09f), new Vector2(.94f,.15f), Skip);
            Label("StoryReadingHint", safeRoot, "タップで全文表示・次の会話へ", 24,
                new Vector2(.06f,.09f), new Vector2(.61f,.15f), new Color(.62f,.70f,.8f), TextAnchor.MiddleLeft);
            // Clicking the message itself is equivalent to the next button.
            var advance = body.gameObject.AddComponent<Button>();
            body.raycastTarget = true;
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(NextLine);
            DisplayLine();
        }

        private void BuildSpeakerPortrait()
        {
            // Portraits sit behind the panel's top edge, never behind the text.
            // Preserve the complete illustration at every portrait aspect ratio.
            var rect = Rect("StorySpeakerPortrait", safeRoot,
                new Vector2(.09f,.615f), new Vector2(.91f,.875f));
            speakerPortrait = rect.gameObject.AddComponent<Image>();
            speakerPortrait.type = Image.Type.Simple;
            speakerPortrait.preserveAspect = true;
            speakerPortrait.raycastTarget = false;
            portraitGroup = rect.gameObject.AddComponent<CanvasGroup>();
            portraitGroup.interactable = false;
            portraitGroup.blocksRaycasts = false;
            rect.gameObject.SetActive(false);
        }

        private void DisplaySpeakerPortrait(string speakerName)
        {
            string resourcePath = StorySpeakerPortraitCatalog.GetResourcePath(speakerName);
            if (string.IsNullOrEmpty(resourcePath))
            {
                portraitSpeaker = null;
                speakerPortrait.sprite = null;
                speakerPortrait.gameObject.SetActive(false);
                return;
            }
            if (portraitSpeaker == speakerName && speakerPortrait.gameObject.activeSelf) return;

            speakerPortrait.sprite = Resources.Load<Sprite>(resourcePath);
            portraitSpeaker = speakerName;
            portraitStartedAt = Time.unscaledTime;
            portraitGroup.alpha = 0f;
            speakerPortrait.gameObject.SetActive(speakerPortrait.sprite != null);
        }

        private void UpdateSpeakerPortrait()
        {
            if (speakerPortrait == null || !speakerPortrait.gameObject.activeSelf) return;
            float progress = Mathf.Clamp01((Time.unscaledTime - portraitStartedAt) /
                StorySpeakerPortraitCatalog.FadeDuration);
            portraitGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
        }

        private void BuildArchive()
        {
            BuildBase();
            Label("StoryArchiveTitle", safeRoot, "冒険の記録", 44, new Vector2(.08f,.89f), new Vector2(.92f,.96f), Gold);
            Label("StoryArchiveSubtitle", safeRoot, "これまでの物語を読み返す", 27, new Vector2(.08f,.85f), new Vector2(.92f,.90f), Ink);
            var viewport = Rect("StoryArchiveViewport", safeRoot, new Vector2(.08f,.16f), new Vector2(.92f,.82f));
            var scrollSurface = viewport.gameObject.AddComponent<Image>();
            scrollSurface.color = Color.clear;
            scrollSurface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            archiveScroll = viewport.gameObject.AddComponent<ScrollRect>();
            archiveScroll.horizontal = false;
            archiveScroll.vertical = true;
            archiveScroll.movementType = ScrollRect.MovementType.Clamped;
            archiveScroll.scrollSensitivity = 45f;
            archiveScroll.viewport = viewport;
            var content = Rect("StoryArchiveContent", viewport, new Vector2(0,1), Vector2.one);
            content.pivot = new Vector2(.5f,1f);
            archiveScroll.content = content;
            var all = StoryDialogueCatalog.All;
            const float rowHeight = 160f;
            const float spacing = 18f;
            const float padding = 16f;
            content.sizeDelta = new Vector2(0, padding * 2 + all.Length * rowHeight + Mathf.Max(0, all.Length - 1) * spacing);
            for (int i = 0; i < all.Length; i++)
            {
                var entry = all[i];
                bool unlocked = entry.EventId == StoryDialogueCatalog.GuardianIntroId
                    ? StoryTutorialService.HasSeenStory(profile, entry.EventId)
                    : entry.UnlockFloor == 0 || profile.HighestFloor >= entry.UnlockFloor;
                string text = unlocked ? entry.Subtitle + "\n" + entry.Title : entry.EventId == StoryDialogueCatalog.GuardianIntroId
                    ? "未解放  ──  神獣の試練の紹介" : "未解放  ──  " + entry.UnlockFloor + "層クリア後";
                var button = Button("StoryArchive_" + entry.EventId, content, text,
                    new Vector2(0,1), Vector2.one, () => OpenArchiveEntry(entry));
                var row = (RectTransform)button.transform;
                row.pivot = new Vector2(.5f,1f);
                row.sizeDelta = new Vector2(0,rowHeight);
                row.anchoredPosition = new Vector2(0,-padding-i*(rowHeight+spacing));
                button.interactable = unlocked;
            }
            Label("StoryArchiveScrollHint", safeRoot, "上下にスワイプして物語を選ぶ", 24,
                new Vector2(.08f,.12f), new Vector2(.92f,.15f), Ink);
            Button("StoryArchiveClose", safeRoot, "ホームへ", new Vector2(.30f,.025f), new Vector2(.70f,.105f), Skip);
        }

        private void OpenArchiveEntry(StoryDialogueDefinition entry)
        {
            if (!AcceptInput()) return;
            float position = archiveScroll.verticalNormalizedPosition;
            Close(false);
            if (TryShowPreview(entry.EventId))
            {
                current.returnToArchive = true;
                current.archiveScrollPosition = position;
            }
        }

        private void DisplayLine()
        {
            var line = story.Lines[lineIndex];
            // Manuscripts may use this editorial marker; narration has no visible speaker.
            speaker.text = line.Speaker == "地の文" ? string.Empty : line.Speaker;
            speaker.color = line.Speaker == "イオナ" ? new Color(.77f,.72f,1f) :
                line.Speaker == "契約師" ? new Color(.6f,.86f,1f) : Gold;
            DisplaySpeakerPortrait(line.Speaker);
            garzaPose = GarzaBossPresentation.StoryPose(story.EventId, lineIndex);
            garzaAction.sprite = garzaPose != null ? GarzaBossPresentation.StoryFrame(garzaPose, 0) : null;
            garzaAction.gameObject.SetActive(garzaAction.sprite != null);
            body.text = string.Empty;
            counter.text = (lineIndex + 1) + " / " + story.Lines.Length;
            nextLabel.text = "全文表示";
            lineStartedAt = Time.unscaledTime;
            revealAll = false;
        }

        private bool AcceptInput()
        {
            if (closing || Time.unscaledTime - lastInputAt < .16f) return false;
            lastInputAt = Time.unscaledTime;
            return true;
        }

        public void NextLine()
        {
            if (archive || !AcceptInput()) return;
            string text = story.Lines[lineIndex].Text;
            if (body.text.Length < text.Length)
            {
                revealAll = true;
                body.text = text;
                return;
            }
            if (lineIndex + 1 >= story.Lines.Length) { Close(true); return; }
            lineIndex++;
            if (!readOnly && StoryDialogueProgress.Advance(profile, story.EventId, lineIndex))
                SaveManager.Instance?.SaveCurrentGame();
            DisplayLine();
        }

        public void Skip()
        {
            if (AcceptInput()) Close(!archive);
        }

        private void Close(bool complete)
        {
            if (closing) return;
            closing = true;
            if (!readOnly && complete && story != null &&
                StoryDialogueProgress.Complete(profile, story.EventId)) SaveManager.Instance?.SaveCurrentGame();
            Action callback = complete ? finished : null;
            bool reopen = complete && returnToArchive;
            if (current == this) current = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
            if (reopen && TryShowArchive())
            {
                Canvas.ForceUpdateCanvases();
                current.archiveScroll.verticalNormalizedPosition = archiveScrollPosition;
            }
        }

        private void Update()
        {
            ApplySafeArea();
            if (archive || story == null || closing) return;
            UpdateSpeakerPortrait();
            float animationTime = Time.unscaledTime - lineStartedAt;
            if (portraitSpeaker == GarzaBossPresentation.DisplayName)
                speakerPortrait.sprite = GarzaBossPresentation.StoryFrame("idle", animationTime);
            if (garzaPose != null && garzaAction != null)
                garzaAction.sprite = GarzaBossPresentation.StoryFrame(garzaPose, animationTime);
            string text = story.Lines[lineIndex].Text;
            int length = revealAll ? text.Length : Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime-lineStartedAt)*42f), 0, text.Length);
            if (body.text.Length != length) body.text = text.Substring(0,length);
            nextLabel.text = length < text.Length ? "全文表示" : lineIndex + 1 == story.Lines.Length ? "読み終える" : "次へ  ›";
        }

        private void ApplySafeArea()
        {
            Vector2 size = new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            Rect area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) area = new Rect(Vector2.zero,size);
            area = ResolveContentSafeArea(area, size, AdMobBannerService.Instance?.VisibleHeightPixels ?? 0f);
            if (safeRoot == null || (area == lastSafeArea && size == lastScreen)) return;
            safeRoot.anchorMin = new Vector2(area.xMin/size.x,area.yMin/size.y);
            safeRoot.anchorMax = new Vector2(area.xMax/size.x,area.yMax/size.y);
            lastSafeArea = area;
            lastScreen = size;
        }

        private static Rect ResolveContentSafeArea(Rect area, Vector2 screenSize, float bannerHeightPixels)
        {
            // Bottom-anchored iOS ads sit above the home-indicator safe inset.
            // Leave an additional gap so a story control never touches the ad.
            if (bannerHeightPixels > 0)
                area.yMin += bannerHeightPixels + 16f * screenSize.x / 1080f;
            return area;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && !readOnly && !closing) SaveManager.Instance?.SaveCurrentGame();
        }

        private void OnDestroy()
        {
            // The last visible line was saved before display. Scene exit never
            // completes the event and must not call the continuation callback.
            if (current == this) current = null;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text Label(string name, Transform parent, string text, int size, Vector2 min, Vector2 max,
            Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(name,parent,min,max).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button Button(string name, Transform parent, string text, Vector2 min, Vector2 max, Action action)
        {
            var rect = Rect(name,parent,min,max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/GachaPage/GachaSmallButton");
            image.type = Image.Type.Simple;
            image.color = new Color(.76f,.83f,.94f,1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var label = Label(name+"Text",rect,text,30,new Vector2(.04f,.06f),new Vector2(.96f,.94f),Ink);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 22; label.resizeTextMaxSize = 30;
            return button;
        }
    }
}
