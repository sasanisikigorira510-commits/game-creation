using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Monetization;
using WitchTower.UI;

namespace WitchTower.Home
{
    // The sanctuary presents saved contracts. Birth and formation are committed
    // before any presentation starts; closing a view never repeats a transaction.
    public sealed class GuardianSanctuaryController : MonoBehaviour
    {
        private const string Art = "UI/GuardiansReborn/";
        private static readonly string[] Ids = { "seiryu", "suzaku", "byakko", "genbu" };
        private static readonly Color Ink = new Color(.96f, .95f, .91f);
        private static readonly Color Gold = new Color(.93f, .78f, .49f);
        private static readonly Color Muted = new Color(.64f, .73f, .83f);
        private GameObject modal;
        private RectTransform safeRoot;
        private RectTransform content;
        private ScrollRect scroll;
        private Button entry;
        private Text entryLabel;
        private Text primaryLabel;
        private Text nextActionNote;
        private Button primary;
        private bool initialized;
        private bool deferredTrialReturn;
        private bool transitionInProgress;
        private string selectedId = "seiryu";
        private string previewContract = "basic";
        private string message;
        private Font font;
        private GameObject birthOverlay;
        private Coroutine birthRoutine;
        private Image hero;
        private Image heroSeal;
        private Vector2 heroRestPosition;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private float lastBannerHeight = -1;
        private PlayerProfile previewProfile;
        private bool isPreview;
        private bool showPerformance;
        private bool tutorialGuardianSelected;
        private PlayerProfile Profile => previewProfile ?? GameManager.Instance?.PlayerProfile;
        public bool IsPreview => isPreview;
        public bool IsOpen => modal != null && modal.activeInHierarchy;
        public bool IsBirthPlaying => birthOverlay != null;
        public string SelectedGuardianId => selectedId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "HomeScene" && scene.name != "FormationScene") return;
            if (GuardianTrialSession.IsActive) GuardianTrialSession.End();
            var owner = CreateOwner("GuardianSanctuary");
            owner.AddComponent<GuardianSanctuaryController>();
        }

        private static GameObject CreateOwner(string name) => new GameObject(name,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        // A detached profile lets the Editor exercise every visual state without
        // changing the live game, its inventory, or any save file.
        public static GuardianSanctuaryController ShowPreview(PlayerProfile sample, string id = "seiryu")
        {
            if (sample == null) return null;
            var previous = FindObjectsByType<GuardianSanctuaryController>(FindObjectsSortMode.None)
                .FirstOrDefault(view => view.isPreview);
            if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
            var view = CreateOwner("GuardianSanctuaryPreview").AddComponent<GuardianSanctuaryController>();
            view.previewProfile = sample;
            view.isPreview = true;
            view.selectedId = Ids.Contains(id) ? id : "seiryu";
            view.Initialize();
            view.Open();
            return view;
        }

        private void Update()
        {
            if (!initialized && Profile != null) Initialize();
            if (!initialized || Profile == null) return;
            ApplySafeArea();
            if (deferredTrialReturn && !IsStoryBlocking())
            {
                deferredTrialReturn = false;
                Open();
            }
            if (entry != null)
            {
                var home = FindFirstObjectByType<HomeSceneController>();
                bool visible = SceneManager.GetActiveScene().name != "HomeScene" || (home != null && home.IsHomeMenuVisible);
                entry.gameObject.SetActive(!isPreview && Profile.HasCompletedTutorial && visible && !IsStoryBlocking()
                    && (modal == null || !modal.activeSelf));
                var equipped = GuardianService.Equipped(Profile);
                entryLabel.text = !GuardianService.IsUnlocked(Profile) ? "神獣の聖域  ·  第3ステージで解放" :
                    Profile.GuardianCoreIds.Count > 0 ? "神獣の聖域  ·  神核から仲間にする" :
                    equipped == null ? Profile.OwnedGuardians.Count > 0 ? "神獣の聖域  ·  専用枠に編成する" :
                    "神獣の聖域  ·  試練で仲間にする" :
                    $"{GuardianService.Find(equipped.Id).DisplayName}  ·  Lv.{GuardianService.Level(Profile, equipped.Id)}";
                bool guideEntry = GuardianTutorial.IsActive(Profile) && !IsStoryBlocking() &&
                    (modal == null || !modal.activeSelf);
                TutorialTargetFrame.SetVisible(entry.transform, guideEntry, "神獣の試練へ",
                    captionBelow: SceneManager.GetActiveScene().name == "HomeScene");
            }
            if (hero != null && modal != null && modal.activeInHierarchy)
            {
                hero.rectTransform.anchoredPosition = heroRestPosition + new Vector2(0, Mathf.Sin(Time.unscaledTime * .72f) * 5f);
                if (heroSeal != null) heroSeal.rectTransform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 2f);
            }

        }

        public void Initialize()
        {
            if (initialized || Profile == null) return;
            initialized = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = isPreview ? 30500 : 120;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0;
            entry = ActionButton("GuardianSlot", transform, new Vector2(0, -495), new Vector2(640, 108), "神獣の聖域", Open, "TabFrame");
            var entryRect = entry.GetComponent<RectTransform>();
            entryRect.anchorMin = entryRect.anchorMax = new Vector2(.5f, 1);
            if (SceneManager.GetActiveScene().name == "FormationScene")
            { entryRect.anchorMin = entryRect.anchorMax = new Vector2(.5f, 0); entryRect.anchoredPosition = new Vector2(0, 135); }
            entryLabel = entry.GetComponentInChildren<Text>();
            entryLabel.fontSize = 27;
            // Only an explicitly started trial returns here. Unlocking a guardian
            // never queues an automatic page change after a story.
            if (!isPreview && GuardianTrialSession.ReopenSanctuary)
            {
                GuardianTrialSession.ReopenSanctuary = false;
                if (IsStoryBlocking()) deferredTrialReturn = true;
                else Open();
            }
        }

        private bool IsStoryBlocking() => !isPreview && (StoryDialogueController.IsShowing ||
            (SceneManager.GetActiveScene().name == "HomeScene" && StoryDialogueProgress.GetPendingHomeDialogue(Profile) != null));

        public void Open()
        {
            if (Profile == null || IsStoryBlocking() || transitionInProgress) return;
            if (!isPreview)
            {
                if (Profile.GuardianCoreIds.Count > 0) selectedId = Profile.GuardianCoreIds[0];
                else if (GuardianService.Equipped(Profile) != null) selectedId = Profile.EquippedGuardianId;
                else if (Profile.OwnedGuardians.Count > 0) selectedId = Profile.OwnedGuardians[0].Id;
                if (GuardianService.IsUnlocked(Profile) && StoryTutorialService.MarkHintSeen(Profile, GuardianService.IntroductionSeen)) Save();
            }
            previewContract = GuardianService.ContractId(Profile, selectedId);
            tutorialGuardianSelected = false;
            if (modal == null) BuildModal();
            modal.SetActive(true);
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }

        private void BuildModal()
        {
            modal = Stretch("GuardianSanctuaryModal", transform).gameObject;
            var background = Picture("SanctuaryBackground", modal.transform, Vector2.zero, Vector2.zero, "SanctuaryBackground");
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.preserveAspect = false;
            background.raycastTarget = true;
            safeRoot = Stretch("GuardianSafeArea", modal.transform);
            ApplySafeArea();
            Label("Title", safeRoot, new Vector2(42, -66), new Vector2(590, 58), "神獣の聖域", 42, TextAnchor.MiddleLeft, topLeft:true);
            var close = ActionButton("CloseSanctuary", safeRoot, new Vector2(-189, -66), new Vector2(310, 92), isPreview ? "プレビュー終了" : SceneManager.GetActiveScene().name == "HomeScene" ? "ホームへ戻る" : "編成へ戻る", Close, "TabFrame");
            close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = Vector2.one;
            close.GetComponentInChildren<Text>().fontSize = 27;
            var viewport = Stretch("SanctuaryScroll", safeRoot);
            viewport.offsetMin = new Vector2(28, 230); viewport.offsetMax = new Vector2(-28, -136);
            // A transparent input surface keeps dragging available between cards.
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = (RectTransform)Node("SanctuaryContent", viewport, Vector2.zero, new Vector2(1024, 2140)).transform;
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1);
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34;
            var footer = Panel("SanctuaryActionFrame", safeRoot, new Vector2(0, 110), new Vector2(1040, 214));
            footer.anchorMin = footer.anchorMax = new Vector2(.5f, 0);
            primary = ActionButton("GuardianPrimaryAction", safeRoot, new Vector2(0, 132), new Vector2(914, 110), "", Act);
            primary.GetComponent<RectTransform>().anchorMin = primary.GetComponent<RectTransform>().anchorMax = new Vector2(.5f, 0);
            primaryLabel = primary.GetComponentInChildren<Text>();
            primaryLabel.rectTransform.anchoredPosition = new Vector2(0, 7);
            nextActionNote = Label("SeparateSlotNote", safeRoot, new Vector2(0, 48), new Vector2(930, 38), "神獣専用枠 1枠  ·  仲間5体とともに戦う", 23, TextAnchor.MiddleCenter, Muted);
            nextActionNote.rectTransform.anchorMin = nextActionNote.rectTransform.anchorMax = new Vector2(.5f, 0);
        }

        private void Refresh()
        {
            ClearChildren(content);
            var selected = GuardianService.Find(selectedId);
            var current = GuardianService.Owned(Profile, selectedId);
            Color accent = Accent(selectedId);
            StylePortraitCaption(Label("GuardianEyebrow", content, new Vector2(0, -23), new Vector2(880, 46),
                GuardianService.OathCleared(Profile, selectedId) ? GuardianService.OathTitle(selectedId) : Epithet(selectedId),
                30, TextAnchor.MiddleCenter, accent));
            heroSeal = null;
            if (current != null && GuardianService.Level(Profile, selectedId) >= GuardianService.BondDialogueLevel)
            {
                heroSeal = Picture("GuardianBondSeal", content, new Vector2(0, -335), new Vector2(630, 630), "ContractSeal");
                heroSeal.color = new Color(1, 1, 1, .35f);
            }
            hero = Picture("GuardianHeroPortrait", content, new Vector2(0, -335), new Vector2(920, 605), Portrait(selectedId));
            heroRestPosition = hero.rectTransform.anchoredPosition;
            Label("GuardianHeroName", content, new Vector2(0, -655), new Vector2(950, 82), selected.DisplayName, 65, TextAnchor.MiddleCenter);
            StylePortraitCaption(Label("GuardianHeroRole", content, new Vector2(0, -718), new Vector2(950, 64), GuardianService.Role(selectedId), 30, TextAnchor.MiddleCenter, accent));
            for (int i = 0; i < Ids.Length; i++)
            {
                string id = Ids[i];
                bool owned = GuardianService.Owned(Profile, id) != null;
                string state = owned ? $"Lv.{GuardianService.Level(Profile, id)}" + (Profile.EquippedGuardianId == id ? " · 同行中" : "") :
                    Profile.GuardianCoreIds.Contains(id) ? "神核を所持" : "未契約";
                var button = ActionButton("GuardianCard_" + id, content, new Vector2(-366 + i * 244, -825), new Vector2(232, 116), "", () => SelectGuardian(id), "TabFrame");
                button.GetComponent<Image>().color = id == selectedId ? Color.white : new Color(.55f, .63f, .76f);
                Label("Name", button.transform, new Vector2(0, 17), new Vector2(186, 38), GuardianService.Find(id).DisplayName, 32, TextAnchor.MiddleCenter, id == selectedId ? Accent(id) : Ink);
                Label("State", button.transform, new Vector2(0, -27), new Vector2(186, 30), state, 22, TextAnchor.MiddleCenter, id == selectedId ? Ink : Muted);
            }
            int level = GuardianService.Level(Profile, selectedId);
            var growth = Panel("GuardianGrowth", content, new Vector2(0, -971), new Vector2(964, 100));
            Label("Level", growth, new Vector2(-249, 0), new Vector2(380, 46), $"{selected.DisplayName} Lv.{level}", 39, TextAnchor.MiddleLeft, Gold);
            string exp = current == null ? string.Empty : level >= GuardianService.MaxLevel ? "最大レベルに到達" :
                $"次のLvまで {GuardianService.RequiredExp(level) - GuardianService.Experience(Profile, selectedId)} EXP";
            Label("Experience", growth, new Vector2(208, 0), new Vector2(475, 42), exp, 25, TextAnchor.MiddleRight);
            Label("ContractHeading", content, new Vector2(-214, -1085), new Vector2(510, 40), "神技のタイプ", 28, TextAnchor.MiddleLeft, Gold);
            BuildContract("basic", -248);
            BuildContract("alternate", 248);
            string detail = !GuardianService.IsUnlocked(Profile) ? "古契約の地下書庫・第10層を突破すると、\n四神の試練が開きます。" : current != null ?
                $"神技  {GuardianService.Skill(selectedId)}\n仲間の行動で共鳴が満ちると、自動で発動。" : Profile.GuardianCoreIds.Contains(selectedId) ?
                $"{selected.DisplayName}の神核を所持しています。\n新たな契約を結び、神獣を迎えましょう。" : GuardianService.CanChallenge(Profile, selectedId) ?
                "" :
                $"次の契約枠は通算 {GuardianService.NextUnlockFloor(Profile)} 層で解放。\n探索を進めると、ほかの神獣も仲間にできます。";
            string detailsText = string.IsNullOrEmpty(message) ? detail : message;
            float detailsOffset = string.IsNullOrEmpty(detailsText) ? 170 : 0;
            if (!string.IsNullOrEmpty(detailsText))
            {
                var details = Panel("GuardianDetailsPanel", content, new Vector2(0, -1394), new Vector2(964, 172));
                Label("SelectedDetails", details, Vector2.zero, new Vector2(870, 118), detailsText, 28, TextAnchor.MiddleLeft);
            }
            ActionButton("GuardianPerformanceToggle", content, new Vector2(0, -1541 + detailsOffset), new Vector2(964, 94),
                showPerformance ? "性能と常時加護を閉じる  ∧" : "性能と常時加護を見る  ∨",
                () => { showPerformance = !showPerformance; Refresh(); }, "TabFrame");
            if (showPerformance) BuildPerformanceDetails(detailsOffset);
            float lowerOffset = (showPerformance ? 390 : 0) - detailsOffset;
            bool oathDone = GuardianService.OathCleared(Profile, selectedId);
            if (current != null)
            {
                string oathText = oathDone ? GuardianService.OathTitle(selectedId) + "  ·  誓約達成" : "誓約試練  ·  " + selected.DisplayName;
                var oath = ActionButton("GuardianOathTrial", content, new Vector2(0, -1660 - lowerOffset), new Vector2(964, 104), oathText, Oath, "TabFrame");
                oath.interactable = !isPreview && GuardianService.CanChallengeOath(Profile, selectedId);
                Label("OathHint", content, new Vector2(0, -1755 - lowerOffset), new Vector2(946, 82), oathDone ? "獲得した称号を神獣の名前の上に表示しています" :
                    "Lv.30・第一部完了・この神獣の編成で挑戦可能\n達成すると専用の称号を獲得", 22, TextAnchor.MiddleCenter, Muted);
            }
            content.sizeDelta = new Vector2(1024, (current != null ? 1830 : 1630) + lowerOffset);
            RefreshAction();
        }

        private void BuildPerformanceDetails(float detailsOffset)
        {
            var panel = Panel("GuardianPerformancePanel", content, new Vector2(0, -1792 + detailsOffset), new Vector2(964, 364));
            var stats = GuardianService.Stats(selectedId, GuardianService.Level(Profile, selectedId));
            Label("GuardianPerformanceTitle", panel, new Vector2(0, 122), new Vector2(850, 42),
                $"{GuardianService.Find(selectedId).DisplayName}の基本能力  ·  Lv.{GuardianService.Level(Profile, selectedId)}", 28, TextAnchor.MiddleLeft, Gold);
            string values = $"HP {stats.MaxHp:N0}    攻撃 {stats.Attack:N0}    魔力 {stats.Wisdom:N0}\n" +
                $"防御 {stats.Defense:N0}    魔防 {stats.MagicDefense:N0}    攻撃速度 {stats.AttackSpeed:0.0}/秒\n" +
                $"会心率 {Mathf.RoundToInt(stats.CritRate * 100)}%    会心倍率 {stats.CritDamage:0.0}倍";
            var body = Label("GuardianPerformanceStats", panel, new Vector2(0, 36), new Vector2(850, 124), values, 27, TextAnchor.MiddleLeft);
            body.lineSpacing = 1.2f;
            Label("GuardianPassiveTitle", panel, new Vector2(0, -60), new Vector2(850, 36),
                "常時加護  ·  同行中、通常の仲間へ", 25, TextAnchor.MiddleLeft, Gold);
            Label("GuardianPassiveValues", panel, new Vector2(0, -111), new Vector2(850, 58),
                PassiveSummary(selectedId), 27, TextAnchor.MiddleLeft);
        }

        private static string PassiveSummary(string id)
        {
            var modifier = GuardianService.Modifier(id);
            if (id == "seiryu") return $"攻撃速度 +{Mathf.RoundToInt((modifier.AttackSpeedMultiplier - 1) * 100)}%";
            if (id == "suzaku") return $"魔力 +{Mathf.RoundToInt((modifier.WisdomMultiplier - 1) * 100)}%";
            if (id == "byakko") return $"会心率 +{Mathf.RoundToInt(modifier.CritRateBonus * 100)}ポイント";
            return $"最大HP +{Mathf.RoundToInt((modifier.MaxHpMultiplier - 1) * 100)}%  ・  " +
                $"防御 +{Mathf.RoundToInt((modifier.DefenseMultiplier - 1) * 100)}%  ・  " +
                $"魔防 +{Mathf.RoundToInt((modifier.MagicDefenseMultiplier - 1) * 100)}%";
        }

        private void BuildContract(string contract, float x)
        {
            bool chosen = previewContract == contract;
            bool unlocked = contract == "basic" || GuardianService.CanUseAlternate(Profile, selectedId);
            var button = ActionButton("GuardianContract_" + contract, content, new Vector2(x, -1204), new Vector2(468, 218), "", () => SetContract(contract), "PanelFrame");
            button.interactable = unlocked && GuardianService.Owned(Profile, selectedId) != null;
            button.GetComponent<Image>().color = chosen ? Color.white : new Color(.65f, .70f, .82f);
            string status = GuardianService.Owned(Profile, selectedId) == null ? contract == "basic" ? "" : "この神獣のLv.10で解放" :
                chosen ? "選択中" : unlocked ? "切り替える" : "この神獣のLv.10で解放";
            Label("ContractName", button.transform, new Vector2(0, 58), new Vector2(400, 47), GuardianService.ContractLabel(selectedId, contract), 32, TextAnchor.MiddleCenter, chosen ? Accent(selectedId) : Ink);
            var description = Label("ContractDescription", button.transform, new Vector2(0, -3), new Vector2(400, 79), GuardianService.ContractDescription(selectedId, contract), 22, TextAnchor.MiddleCenter);
            description.resizeTextForBestFit = true; description.resizeTextMinSize = 19; description.resizeTextMaxSize = 22;
            if (!string.IsNullOrEmpty(status)) Label("ContractState", button.transform, new Vector2(0, -69), new Vector2(400, 34), status, 23, TextAnchor.MiddleCenter, chosen ? Gold : Muted);
        }

        private void RefreshAction()
        {
            bool owned = GuardianService.Owned(Profile, selectedId) != null;
            primaryLabel.text = owned ? Profile.EquippedGuardianId == selectedId ? "神獣専用枠に編成中" : "神獣専用枠へ編成" :
                Profile.GuardianCoreIds.Contains(selectedId) ? GuardianService.Find(selectedId).DisplayName + "を誕生させる" :
                GuardianService.Find(selectedId).DisplayName + "の試練へ";
            nextActionNote.text = owned ? Profile.EquippedGuardianId == selectedId ? "ホームへ戻り、バトルから探索を続けましょう" : "編成すると次の探索から神獣が参加" :
                Profile.GuardianCoreIds.Contains(selectedId) ? "獲得済みの神核を使います · 石の消費なし" :
                "試練突破で神核を獲得 · 挑戦の消費なし";
            if (!GuardianService.IsUnlocked(Profile)) nextActionNote.text = GuardianService.UnlockRequirementLabel;
            else if (!owned && !Profile.GuardianCoreIds.Contains(selectedId) && !GuardianService.CanChallenge(Profile, selectedId))
                nextActionNote.text = $"次の契約枠は通算 {GuardianService.NextUnlockFloor(Profile)} 層で解放";
            primary.interactable = !transitionInProgress && (owned ? Profile.EquippedGuardianId != selectedId :
                Profile.GuardianCoreIds.Contains(selectedId) || GuardianService.CanChallenge(Profile, selectedId));
            if (isPreview && !owned && !Profile.GuardianCoreIds.Contains(selectedId)) primary.interactable = false;
            RefreshTutorialGuide();
        }

        private void RefreshTutorialGuide()
        {
            string step = isPreview ? "" : GuardianTutorial.Step(Profile, selectedId, tutorialGuardianSelected);
            for (int i = 0; i < Ids.Length; i++)
            {
                // Refresh defers destruction of the previous cards until the
                // end of the frame, so only decorate the newly active card.
                var card = content.Cast<Transform>().FirstOrDefault(child =>
                    child.name == "GuardianCard_" + Ids[i] && child.gameObject.activeSelf);
                bool available = GuardianService.CanChallenge(Profile, Ids[i]) ||
                    Profile.GuardianCoreIds.Contains(Ids[i]) || GuardianService.Owned(Profile, Ids[i]) != null;
                TutorialTargetFrame.SetVisible(card, step == "choose" && available);
            }
            TutorialTargetFrame.SetVisible(primary.transform, !string.IsNullOrEmpty(step) && step != "choose" && birthOverlay == null);
            if (!string.IsNullOrEmpty(step)) nextActionNote.text = GuardianTutorial.Instruction(step);
        }

        private void SelectGuardian(string id)
        {
            if (birthOverlay != null || transitionInProgress) return;
            tutorialGuardianSelected = true;
            selectedId = id; previewContract = GuardianService.ContractId(Profile, id); message = null; Refresh();
        }

        private void SetContract(string contract)
        {
            if (transitionInProgress || birthOverlay != null) return;
            if (contract == "alternate" && !GuardianService.CanUseAlternate(Profile, selectedId)) return;
            if (GuardianService.Owned(Profile, selectedId) != null)
            {
                if (!GuardianService.SetContract(Profile, selectedId, contract)) return;
                Save();
            }
            previewContract = contract;
            message = null;
            Refresh();
        }

        private void Act()
        {
            if (transitionInProgress || birthOverlay != null || !primary.interactable) return;
            if (GuardianService.Owned(Profile, selectedId) != null)
            {
                bool firstGuidedFormation = GuardianTutorial.IsActive(Profile);
                if (GuardianService.Equip(Profile, selectedId)) message = firstGuidedFormation
                    ? "イオナ：これで次の探索から一緒に戦えるわ。\n新しい仲間も連れて、みんなで帰ってきてね。"
                    : "編成しました。\nホームのバトルから探索へ進みましょう。";
                Save(); Refresh();
            }
            else if (Profile.GuardianCoreIds.Contains(selectedId))
            {
                if (!GuardianService.Birth(Profile, selectedId)) return;
                // This is the only grant. Animation, skipping, pausing and closing
                // all lead back to the already-owned guardian.
                Save();
                message = "神獣を迎えました。\n専用枠へ編成して、ともに探索へ。";
                Refresh();
                BeginBirthPresentation();
            }
            else if (!isPreview)
            {
                if (GuardianTrialSession.Begin(Profile, selectedId)) LoadTrial();
                else { message = "通常の仲間を編成してから、試練に挑みましょう。"; Refresh(); }
            }
        }

        private void Oath()
        {
            if (transitionInProgress || isPreview || birthOverlay != null) return;
            if (GuardianTrialSession.BeginOath(Profile, selectedId)) LoadTrial();
        }

        private void LoadTrial()
        {
            transitionInProgress = true;
            if (!GuardianTrialSession.IsPractice) Save();
            try { SceneManager.LoadScene("BattleScene"); }
            catch { transitionInProgress = false; GuardianTrialSession.End(); throw; }
        }

        public void PreviewBirth()
        {
            if (!isPreview || birthOverlay != null) return;
            BeginBirthPresentation();
        }

        private void BeginBirthPresentation()
        {
            AudioManager.Instance?.PlaySe(Resources.Load<AudioClip>("Audio/SE/GuardiansReborn/contract_prepare"));
            birthOverlay = Stretch("GuardianBirthPresentation", safeRoot).gameObject;
            var veil = birthOverlay.AddComponent<Image>(); veil.color = new Color(.018f, .025f, .06f, .98f);
            var seal = Picture("GuardianBirthSeal", birthOverlay.transform, new Vector2(0, 130), new Vector2(940, 940), "ContractSeal");
            var rays = Picture("GuardianBirthRays", birthOverlay.transform, new Vector2(0, 130), new Vector2(1060, 1400), "DivineRay");
            var portrait = Picture("GuardianBirthPortrait", birthOverlay.transform, new Vector2(0, 145), new Vector2(985, 1040), Portrait(selectedId));
            var title = Label("GuardianBirthTitle", birthOverlay.transform, new Vector2(0, -545), new Vector2(940, 140), "新たな契約\n" + GuardianService.Find(selectedId).DisplayName, 47, TextAnchor.MiddleCenter, Gold);
            Label("GuardianBirthPromise", birthOverlay.transform, new Vector2(0, -665), new Vector2(920, 80), "ひとつの声を、ひとつの約束で迎える", 28, TextAnchor.MiddleCenter);
            ActionButton("GuardianBirthSkip", birthOverlay.transform, new Vector2(0, -790), new Vector2(500, 104), "演出をスキップ", FinishBirthPresentation, "TabFrame");
            birthRoutine = StartCoroutine(AnimateBirth(seal, rays, portrait, title));
        }

        private IEnumerator AnimateBirth(Image seal, Image rays, Image portrait, Text title)
        {
            float elapsed = 0;
            bool arrivalPlayed = false;
            while (elapsed < 4f && birthOverlay != null)
            {
                elapsed += Time.unscaledDeltaTime;
                if (!arrivalPlayed && elapsed >= 1.4f)
                {
                    arrivalPlayed = true;
                    AudioManager.Instance?.PlaySe(Resources.Load<AudioClip>("Audio/SE/GuardiansReborn/contract_complete"));
                }
                float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .7f) / 1.2f));
                portrait.color = new Color(1, 1, 1, reveal);
                portrait.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, reveal);
                portrait.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(100, 145, reveal));
                seal.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed * 1.6f) * .7f);
                seal.rectTransform.localRotation = Quaternion.Euler(0, 0, elapsed * 7f);
                float rayAlpha = Mathf.Clamp01((elapsed - .4f) * 1.8f) * (.42f + .13f * Mathf.Sin(elapsed * 3));
                rays.color = new Color(1, 1, 1, rayAlpha);
                title.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01((elapsed - 1.4f) * 1.2f));
                yield return null;
            }
            birthRoutine = null;
            FinishBirthPresentation();
        }

        public void FinishBirthPresentation()
        {
            if (birthRoutine != null) { StopCoroutine(birthRoutine); birthRoutine = null; }
            if (birthOverlay == null) return;
            birthOverlay.SetActive(false);
            if (Application.isPlaying) Destroy(birthOverlay);
            else DestroyImmediate(birthOverlay);
            birthOverlay = null;
            RefreshAction();
        }

        private void Close()
        {
            if (transitionInProgress) return;
            FinishBirthPresentation();
            if (isPreview) { gameObject.SetActive(false); Destroy(gameObject); }
            else if (modal != null) modal.SetActive(false);
        }

        private void Save() { if (!isPreview) SaveManager.Instance?.SaveCurrentGame(); }
        private void OnApplicationPause(bool paused) { if (paused) { Save(); FinishBirthPresentation(); } }
        private void OnDisable() { if (birthRoutine != null) StopCoroutine(birthRoutine); birthRoutine = null; }

        private void ApplySafeArea()
        {
            if (safeRoot == null) return;
            Vector2 size = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            Rect area = Screen.safeArea;
            float bannerHeight = AdMobBannerService.Instance?.VisibleHeightPixels ?? 0;
            if (area == lastSafeArea && size == lastScreen && bannerHeight == lastBannerHeight) return;
            if (area.width <= 0 || area.height <= 0) area = new Rect(Vector2.zero, size);
            float bottom = Mathf.Min(area.yMax - 1, area.yMin + Mathf.Max(0, bannerHeight));
            safeRoot.anchorMin = new Vector2(area.xMin / size.x, bottom / size.y);
            safeRoot.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            lastSafeArea = area; lastScreen = size; lastBannerHeight = bannerHeight;
        }

        private static string Portrait(string id) => id == "seiryu" ? "SeiryuPortrait" : id == "suzaku" ? "SuzakuPortrait" : id == "byakko" ? "ByakkoPortrait" : "GenbuPortrait";
        private static string Epithet(string id) => id == "seiryu" ? "東天を翔ける、蒼き連牙" : id == "suzaku" ? "南天を照らす、不滅の焔翼" : id == "byakko" ? "西天を裂く、白耀の牙" : "北天を支える、玄冥の盾";
        private static Color Accent(string id) => id == "seiryu" ? new Color(.48f, .88f, 1f) : id == "suzaku" ? new Color(1f, .59f, .32f) : id == "byakko" ? new Color(.92f, .86f, 1f) : new Color(.53f, .94f, .78f);

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            { var child = parent.GetChild(i).gameObject; child.SetActive(false); if (Application.isPlaying) Destroy(child); else DestroyImmediate(child); }
        }
        private static RectTransform Stretch(string name, Transform parent)
        {
            var rect = (RectTransform)Node(name, parent, Vector2.zero, Vector2.zero).transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static GameObject Node(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = parent.name == "SanctuaryContent" ? new Vector2(.5f, 1) : new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return go;
        }
        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int points,
            TextAnchor alignment, Color? color = null, bool topLeft = false)
        {
            var text = Node(name, parent, position, size).AddComponent<Text>();
            if (topLeft) { text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1); text.rectTransform.pivot = new Vector2(0, .5f); }
            text.font = font; text.fontSize = points; text.fontStyle = FontStyle.Normal;
            text.text = value; text.alignment = alignment; text.color = color ?? Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
        private static void StylePortraitCaption(Text text)
        {
            text.fontStyle = FontStyle.Bold;
            text.color = Color.Lerp(text.color, Color.white, .65f);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.015f, .025f, .04f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, string resource)
        {
            var image = Node(name, parent, position, size).AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(Art + resource);
            image.preserveAspect = true; image.raycastTarget = false;
            // Missing generated assets are surfaced by the asset verification test;
            // they are never silently replaced with an unrelated old illustration.
            if (image.sprite == null) image.color = Color.clear;
            return image;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var image = Picture(name, parent, position, size, "PanelFrame");
            ConfigureFrame(image, size);
            return image.rectTransform;
        }
        private static void ConfigureFrame(Image image, Vector2 size)
        {
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            // At the imported 65–75 pixel border, two corners exceed a compact
            // button's entire height. Scale the border down before nine-slicing
            // so its dark centre remains large enough for the label.
            image.pixelsPerUnitMultiplier = size.y <= 140 ? 3.2f : size.y <= 260 ? 2.4f : 1.65f;
        }
        private Button ActionButton(string name, Transform parent, Vector2 position, Vector2 size, string text,
            UnityEngine.Events.UnityAction action, string resource = "PrimaryButton")
        {
            var go = Node(name, parent, position, size);
            var image = go.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(Art + resource);
            ConfigureFrame(image, size);
            image.color = image.sprite != null ? Color.white : Color.clear;
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.78f, .85f, .92f); colors.disabledColor = new Color(.50f, .57f, .67f, .72f);
            button.colors = colors; button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(text))
            {
                var label = Label("Label", go.transform, Vector2.zero, size - new Vector2(60, 20), text, 32, TextAnchor.MiddleCenter);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 23; label.resizeTextMaxSize = 32;
            }
            else Label("Label", go.transform, Vector2.zero, size - new Vector2(60, 20), "", 32, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
