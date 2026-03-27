using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Managers;

namespace WitchTower.Core
{
    public sealed class TitleSceneController : MonoBehaviour
    {
        [Serializable]
        private sealed class FormationMonsterEntry
        {
            public string Id;
            public string Name;
            public string Role;
            public string TexturePath;
            public Color FrameColor;

            public FormationMonsterEntry(string id, string name, string role, string texturePath, Color frameColor)
            {
                Id = id;
                Name = name;
                Role = role;
                TexturePath = texturePath;
                FrameColor = frameColor;
            }
        }

        private sealed class FormationSlotView
        {
            public Image Frame;
            public Text SlotLabel;
            public Text NameLabel;
            public Text RoleLabel;
            public RawImage Portrait;
            public Button Button;
        }

        private sealed class FormationRosterCardView
        {
            public Image Frame;
            public Text NameLabel;
            public Text RoleLabel;
            public Text StateLabel;
            public RawImage Portrait;
            public Button Button;
        }

        [SerializeField] private string homeSceneName = "HomeScene";
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string formationSceneName = "FormationScene";

        private static readonly string[] TitleOverlayObjectNames =
        {
            "TitleBackgroundShade",
            "TitleGlowLeft",
            "TitleGlowRight",
            "TitleBandTop",
            "TitleBandBottom",
            "TitleGround",
            "TitleTotemLeft",
            "TitleTotemRight",
            "TitleTopRibbon",
            "TitleBottomRibbon",
            "TitleFrame",
            "Overline",
            "TitleSigil",
            "GameTitle",
            "GameSubtitle",
            "RelicChamber",
            "ActionCard",
            "LoreCard",
            "ContinueButton",
            "Start New RunButton"
        };

        private static readonly FormationMonsterEntry[] FormationRoster =
        {
            new FormationMonsterEntry("bat", "バット", "先制", "FormationMonsters/Bat", new Color(0.62f, 0.87f, 0.53f)),
            new FormationMonsterEntry("goblin", "ゴブリン", "前衛", "FormationMonsters/Goblin", new Color(0.74f, 0.42f, 0.36f)),
            new FormationMonsterEntry("wraith", "レイス", "妨害", "FormationMonsters/Wraith", new Color(0.52f, 0.74f, 0.9f)),
            new FormationMonsterEntry("bee", "ビー", "速攻", "FormationMonsters/Bee", new Color(0.86f, 0.75f, 0.34f)),
            new FormationMonsterEntry("naga", "ナーガ", "後衛", "FormationMonsters/Naga", new Color(0.34f, 0.75f, 0.64f)),
            new FormationMonsterEntry("worm", "ワーム", "盾役", "FormationMonsters/Worm", new Color(0.69f, 0.55f, 0.3f)),
            new FormationMonsterEntry("centaur", "ケンタウロス", "物理", "FormationMonsters/Centaur", new Color(0.78f, 0.47f, 0.31f)),
            new FormationMonsterEntry("ghost", "ゴースト", "支援", "FormationMonsters/Ghost", new Color(0.56f, 0.84f, 0.93f)),
            new FormationMonsterEntry("death_mage_elf", "デスメイジ", "呪術", "FormationMonsters/DeathMageElf", new Color(0.74f, 0.48f, 0.86f)),
            new FormationMonsterEntry("dragoon", "ドラグーン", "突撃", "FormationMonsters/Dragoon", new Color(0.88f, 0.5f, 0.34f)),
            new FormationMonsterEntry("hell_knight", "ヘルナイト", "重装", "FormationMonsters/HellKnight", new Color(0.82f, 0.35f, 0.28f)),
            new FormationMonsterEntry("naga_mage", "ナーガメイジ", "魔法", "FormationMonsters/NagaMage", new Color(0.38f, 0.78f, 0.82f)),
            new FormationMonsterEntry("shadow", "シャドウ", "奇襲", "FormationMonsters/Shadow", new Color(0.45f, 0.5f, 0.68f)),
            new FormationMonsterEntry("soul_eater", "ソウルイーター", "吸収", "FormationMonsters/SoulEater", new Color(0.5f, 0.92f, 0.76f)),
            new FormationMonsterEntry("spectral_warrior", "スペクトル", "霊騎", "FormationMonsters/SpectralWarrior", new Color(0.42f, 0.72f, 0.96f)),
            new FormationMonsterEntry("vault_guard", "ヴォルトガード", "守護", "FormationMonsters/VaultGuard", new Color(0.9f, 0.76f, 0.4f))
        };
        private const string FormationScreenTexturePath = "FormationUI/FormationScreen";

        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private readonly FormationSlotView[] slotViews = new FormationSlotView[5];
        private readonly FormationRosterCardView[] rosterViews = new FormationRosterCardView[FormationRoster.Length];
        private readonly int[] assignedMonsterIndices = { 0, 1, 2, 3, 4 };

        private GameObject formationPanelRoot;
        private Text formationSummaryText;
        private Text formationHintText;
        private Text floorLabelText;
        private int selectedSlotIndex;

        private void Start()
        {
            EnsureRuntimeState();
            SimplifyTitlePresentation();
        }

        public void StartNewGame()
        {
            EnsureRuntimeState();
            var defaultSave = Save.PlayerSaveData.CreateDefault();
            SaveManager.Instance.Save(defaultSave);
            GameManager.Instance.InitializeFromSave(defaultSave);
            SceneManager.LoadScene(homeSceneName);
        }

        public void ContinueGame()
        {
            EnsureRuntimeState();
            SaveManager.Instance.LoadOrCreate();
            GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            SceneManager.LoadScene(homeSceneName);
        }

        public void OpenBattle()
        {
            SceneManager.LoadScene(battleSceneName);
        }

        public void OpenFormation()
        {
            SceneManager.LoadScene(formationSceneName);
        }

        public void OpenEquipment()
        {
            Debug.Log("[TitleSceneController] Equipment menu is not implemented yet.");
        }

        public void OpenFusion()
        {
            Debug.Log("[TitleSceneController] Fusion menu is not implemented yet.");
        }

        public void CloseFormation()
        {
            if (formationPanelRoot != null)
            {
                formationPanelRoot.SetActive(false);
            }
        }

        private static void EnsureRuntimeState()
        {
            Application.runInBackground = true;
            ManagerFactory.EnsureGameManager();
            ManagerFactory.EnsureSaveManager();
            ManagerFactory.EnsureMasterDataManager();
            ManagerFactory.EnsureAudioManager();

            if (SaveManager.Instance.CurrentSaveData == null)
            {
                SaveManager.Instance.LoadOrCreate();
            }

            MasterDataManager.Instance?.Initialize();

            if (GameManager.Instance.PlayerProfile == null && SaveManager.Instance.CurrentSaveData != null)
            {
                GameManager.Instance.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            }
        }

        private static void SimplifyTitlePresentation()
        {
            foreach (string objectName in TitleOverlayObjectNames)
            {
                GameObject target = GameObject.Find(objectName);
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        private void EnsureFormationPanel()
        {
            if (formationPanelRoot != null)
            {
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            formationPanelRoot = CreateUiObject("FormationPanelRoot", canvas.transform);
            RectTransform rootRect = formationPanelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image dimmer = formationPanelRoot.AddComponent<Image>();
            dimmer.color = new Color(0.01f, 0.03f, 0.06f, 0.9f);

            Button backdropButton = formationPanelRoot.AddComponent<Button>();
            backdropButton.targetGraphic = dimmer;
            backdropButton.onClick.AddListener(CloseFormation);

            GameObject panel = CreateUiObject("FormationPanel", formationPanelRoot.transform);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1024f, 1536f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f);

            Button panelBlocker = panel.AddComponent<Button>();
            panelBlocker.targetGraphic = panelImage;

            RawImage panelBackground = CreateRawPortrait("FormationBackground", panel.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panelBackground.texture = LoadMonsterTexture(FormationScreenTexturePath);
            panelBackground.color = Color.white;

            CreateActionButton(panel.transform, font, "閉じる", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -18f), new Vector2(132f, 48f),
                new Color(0.48f, 0.22f, 0.18f, 0.92f), CloseFormation, 16);

            GameObject topPreview = CreateUiObject("FormationPreview", panel.transform);
            RectTransform previewRect = topPreview.AddComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;

            GameObject floorBadge = CreateUiObject("FloorBadge", topPreview.transform);
            RectTransform floorRect = floorBadge.AddComponent<RectTransform>();
            floorRect.anchorMin = new Vector2(0.5f, 1f);
            floorRect.anchorMax = new Vector2(0.5f, 1f);
            floorRect.pivot = new Vector2(0.5f, 1f);
            floorRect.anchoredPosition = new Vector2(0f, -52f);
            floorRect.sizeDelta = new Vector2(196f, 46f);
            floorLabelText = CreateText("FloorLabel", floorBadge.transform, font, string.Empty, 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject slotArea = CreateUiObject("SlotArea", topPreview.transform);
            RectTransform slotAreaRect = slotArea.AddComponent<RectTransform>();
            slotAreaRect.anchorMin = Vector2.zero;
            slotAreaRect.anchorMax = Vector2.one;
            slotAreaRect.offsetMin = Vector2.zero;
            slotAreaRect.offsetMax = Vector2.zero;

            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i] = CreateFormationSlot(slotArea.transform, font, i);
            }

            GameObject rosterPanel = CreateUiObject("RosterPanel", panel.transform);
            RectTransform rosterRect = rosterPanel.AddComponent<RectTransform>();
            rosterRect.anchorMin = Vector2.zero;
            rosterRect.anchorMax = Vector2.one;
            rosterRect.offsetMin = Vector2.zero;
            rosterRect.offsetMax = Vector2.zero;

            formationSummaryText = CreateText("FormationSummary", rosterPanel.transform, font, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.93f, 0.85f, 0.53f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(320f, 22f));

            CreateText("RosterTitle", rosterPanel.transform, font, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.84f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(84f, -708f), new Vector2(160f, 28f));

            formationHintText = CreateText("FormationHint", rosterPanel.transform, font, string.Empty, 11, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.79f, 0.86f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(360f, 18f));

            for (int i = 0; i < FormationRoster.Length; i++)
            {
                rosterViews[i] = CreateRosterCard(rosterPanel.transform, font, i);
            }

            CreateActionButton(panel.transform, font, "編成を閉じる", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(250f, 48f),
                new Color(0.62f, 0.32f, 0.12f, 0.92f), CloseFormation, 16);

            formationPanelRoot.SetActive(false);
            RefreshFormationPanel();
        }

        private void RefreshFormationPanel()
        {
            int floor = GameManager.Instance?.CurrentFloor ?? 1;
            int level = GameManager.Instance?.PlayerProfile?.Level ?? 1;
            int gold = GameManager.Instance?.PlayerProfile?.Gold ?? 0;

            if (floorLabelText != null)
            {
                floorLabelText.text = $"F{floor}";
            }

            if (formationSummaryText != null)
            {
                formationSummaryText.text = $"保有 {FormationRoster.Length}体  Lv.{level}  Gold {gold}";
            }

            if (formationHintText != null)
            {
                formationHintText.text = string.Empty;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                RefreshSlotView(i);
            }

            for (int i = 0; i < rosterViews.Length; i++)
            {
                RefreshRosterCard(i);
            }
        }

        private FormationSlotView CreateFormationSlot(Transform parent, Font font, int slotIndex)
        {
            GameObject slotObject = CreateUiObject($"FormationSlot{slotIndex + 1}", parent);
            RectTransform rect = slotObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            Vector2[] positions =
            {
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(-9999f, -9999f)
            };
            rect.anchoredPosition = positions[Mathf.Clamp(slotIndex, 0, positions.Length - 1)];
            rect.sizeDelta = new Vector2(1f, 1f);

            Image frame = slotObject.AddComponent<Image>();
            frame.color = new Color(0.12f, 0.7f, 0.86f, 0.14f);

            Button button = slotObject.AddComponent<Button>();
            button.targetGraphic = frame;
            int capturedSlot = slotIndex;
            button.onClick.AddListener(() => SelectFormationSlot(capturedSlot));

            Text slotLabel = CreateText($"FormationSlotLabel{slotIndex + 1}", slotObject.transform, font, $"枠 {slotIndex + 1}", 16,
                FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.95f, 0.86f, 0.68f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 16f), new Vector2(80f, 18f));

            RawImage portrait = CreateRawPortrait($"FormationSlotPortrait{slotIndex + 1}", slotObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));

            Text nameLabel = CreateText($"FormationSlotName{slotIndex + 1}", slotObject.transform, font, string.Empty, 18,
                FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(100f, 14f));

            Text roleLabel = CreateText($"FormationSlotRole{slotIndex + 1}", slotObject.transform, font, string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.82f, 0.88f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(100f, 14f));

            return new FormationSlotView
            {
                Frame = frame,
                SlotLabel = slotLabel,
                Portrait = portrait,
                NameLabel = nameLabel,
                RoleLabel = roleLabel,
                Button = button
            };
        }

        private FormationRosterCardView CreateRosterCard(Transform parent, Font font, int monsterIndex)
        {
            GameObject cardObject = CreateUiObject($"RosterCard{monsterIndex + 1}", parent);
            RectTransform rect = cardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Vector2[] positions =
            {
                new Vector2(162f, -720f),
                new Vector2(352f, -720f),
                new Vector2(542f, -720f),
                new Vector2(732f, -720f),
                new Vector2(162f, -908f),
                new Vector2(352f, -908f),
                new Vector2(542f, -908f),
                new Vector2(732f, -908f),
                new Vector2(162f, -1096f),
                new Vector2(352f, -1096f),
                new Vector2(542f, -1096f),
                new Vector2(732f, -1096f),
                new Vector2(162f, -1284f),
                new Vector2(352f, -1284f),
                new Vector2(542f, -1284f),
                new Vector2(732f, -1284f)
            };
            rect.anchoredPosition = positions[Mathf.Clamp(monsterIndex, 0, positions.Length - 1)];
            rect.sizeDelta = new Vector2(112f, 112f);

            Image frame = cardObject.AddComponent<Image>();
            frame.color = new Color(0f, 0f, 0f, 0f);

            Button button = cardObject.AddComponent<Button>();
            button.targetGraphic = frame;
            int capturedIndex = monsterIndex;
            button.onClick.AddListener(() => AssignMonsterToSelectedSlot(capturedIndex));

            RawImage portrait = CreateRawPortrait($"RosterPortrait{monsterIndex + 1}", cardObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));

            Text nameLabel = CreateText($"RosterName{monsterIndex + 1}", cardObject.transform, font, string.Empty, 18,
                FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(90f, 12f));

            Text roleLabel = CreateText($"RosterRole{monsterIndex + 1}", cardObject.transform, font, string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(90f, 12f));

            Text stateLabel = CreateText($"RosterState{monsterIndex + 1}", cardObject.transform, font, string.Empty, 16,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.48f, 0.95f, 0.64f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(70f, 12f));

            return new FormationRosterCardView
            {
                Frame = frame,
                Portrait = portrait,
                NameLabel = nameLabel,
                RoleLabel = roleLabel,
                StateLabel = stateLabel,
                Button = button
            };
        }

        private void SelectFormationSlot(int slotIndex)
        {
            selectedSlotIndex = Mathf.Clamp(slotIndex, 0, slotViews.Length - 1);
            RefreshFormationPanel();
        }

        private void AssignMonsterToSelectedSlot(int monsterIndex)
        {
            if (monsterIndex < 0 || monsterIndex >= FormationRoster.Length)
            {
                return;
            }

            for (int i = 0; i < assignedMonsterIndices.Length; i++)
            {
                if (assignedMonsterIndices[i] == monsterIndex)
                {
                    assignedMonsterIndices[i] = assignedMonsterIndices[selectedSlotIndex];
                    break;
                }
            }

            assignedMonsterIndices[selectedSlotIndex] = monsterIndex;
            RefreshFormationPanel();
        }

        private void RefreshSlotView(int slotIndex)
        {
            FormationSlotView view = slotViews[slotIndex];
            FormationMonsterEntry entry = FormationRoster[assignedMonsterIndices[slotIndex]];
            bool isSelected = slotIndex == selectedSlotIndex;

            view.Frame.color = isSelected
                ? new Color(0.22f, 0.98f, 0.94f, 0.24f)
                : new Color(0.12f, 0.7f, 0.86f, 0.14f);

            view.SlotLabel.text = selectedSlotIndex == slotIndex ? "選択" : string.Empty;
            view.NameLabel.text = string.Empty;
            view.RoleLabel.text = string.Empty;
            view.Portrait.texture = LoadMonsterTexture(entry.TexturePath);
            view.Portrait.color = Color.white;
        }

        private void RefreshRosterCard(int monsterIndex)
        {
            FormationRosterCardView view = rosterViews[monsterIndex];
            FormationMonsterEntry entry = FormationRoster[monsterIndex];
            int assignedSlot = Array.IndexOf(assignedMonsterIndices, monsterIndex);

            view.Frame.color = assignedSlot >= 0
                ? new Color(entry.FrameColor.r, entry.FrameColor.g, entry.FrameColor.b, 0.14f)
                : new Color(0.88f, 0.84f, 0.76f, 0.1f);

            view.NameLabel.text = string.Empty;
            view.RoleLabel.text = string.Empty;
            view.StateLabel.text = string.Empty;
            view.StateLabel.color = assignedSlot >= 0
                ? new Color(0.45f, 0.98f, 0.64f)
                : new Color(0.8f, 0.78f, 0.72f);
            view.Portrait.texture = LoadMonsterTexture(entry.TexturePath);
            view.Portrait.color = Color.white;
        }

        private Texture2D LoadMonsterTexture(string resourcePath)
        {
            if (textureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            textureCache[resourcePath] = texture;
            return texture;
        }

        private static void CreateFieldMonsterPreview(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int order)
        {
            GameObject marker = CreateUiObject(name, parent);
            RectTransform rect = marker.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image shadow = marker.AddComponent<Image>();
            shadow.color = new Color(0.12f, 0.15f, 0.16f, 0.78f - (order * 0.1f));
        }

        private static RawImage CreateRawPortrait(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject portraitObject = CreateUiObject(name, parent);
            RectTransform rect = portraitObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            RawImage portrait = portraitObject.AddComponent<RawImage>();
            portrait.color = Color.white;
            return portrait;
        }

        private static Button CreateActionButton(
            Transform parent,
            Font font,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick,
            int fontSize)
        {
            GameObject buttonObject = CreateUiObject(label + "Button", parent);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateText(label + "Text", buttonObject.transform, font, label, fontSize, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string textValue,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject textObject = CreateUiObject(name, parent);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = parent.gameObject.layer;
            return gameObject;
        }
    }
}
