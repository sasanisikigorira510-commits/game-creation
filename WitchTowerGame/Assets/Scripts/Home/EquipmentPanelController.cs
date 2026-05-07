using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.MasterData;
using WitchTower.Managers;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Home
{
    public sealed class EquipmentPanelController : MonoBehaviour
    {
        [SerializeField] private PlayerStatusView playerStatusView;
        [SerializeField] private ResourceView resourceView;
        [SerializeField] private EquipmentStatusView equipmentStatusView;
        [SerializeField] private Button bronzeBladeButton;
        [SerializeField] private Button ironSwordButton;
        [SerializeField] private Button guardClothButton;
        [SerializeField] private Button boneMailButton;
        [SerializeField] private Button ashenRingButton;
        [SerializeField] private Button quickCharmButton;
        [SerializeField] private TMP_Text bronzeBladeStatusText;
        [SerializeField] private TMP_Text ironSwordStatusText;
        [SerializeField] private TMP_Text guardClothStatusText;
        [SerializeField] private TMP_Text boneMailStatusText;
        [SerializeField] private TMP_Text ashenRingStatusText;
        [SerializeField] private TMP_Text quickCharmStatusText;
        [SerializeField] private TMP_Text ctaText;

        private static readonly Color UnlockedButtonColor = new Color(0.2f, 0.35f, 0.42f, 1f);
        private static readonly Color EquippedButtonColor = new Color(0.24f, 0.48f, 0.34f, 1f);
        private static readonly Color LockedButtonColor = new Color(0.19f, 0.19f, 0.22f, 0.75f);
        private static readonly Color EquippedStatusColor = new Color(0.45f, 1f, 0.67f, 1f);
        private static readonly Color OwnedStatusColor = new Color(0.72f, 0.92f, 0.72f, 1f);
        private static readonly Color LockedStatusColor = new Color(1f, 0.78f, 0.45f, 1f);
        private static readonly Color EnhancePanelColor = new Color(0.035f, 0.05f, 0.075f, 0.94f);
        private static readonly Color EnhanceAccentColor = new Color(1f, 0.78f, 0.36f, 1f);
        private static readonly Color EnhanceSubTextColor = new Color(0.78f, 0.88f, 0.95f, 0.95f);

        private const string EnhanceRuneCirclePath = "UI/EquipmentEnhance/EnhanceRuneCircle";
        private const string EnhanceSuccessBasePath = "UI/EquipmentEnhance/EnhanceSuccess_";
        private const string EnhanceFailBasePath = "UI/EquipmentEnhance/EnhanceFail_";
        private const string EnhanceDestroyBasePath = "UI/EquipmentEnhance/EnhanceDestroy_";
        private const int EnhanceEffectFrameCount = 8;
        private const float EnhanceEffectDuration = 0.92f;

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        private bool enhancementUiBuilt;
        private GameObject enhancementLauncherRoot;
        private TMP_Text enhancementLauncherTitleText;
        private TMP_Text enhancementLauncherInfoText;
        private Button weaponEnhanceButton;
        private Button armorEnhanceButton;
        private Button accessoryEnhanceButton;
        private TMP_Text weaponEnhanceButtonLabel;
        private TMP_Text armorEnhanceButtonLabel;
        private TMP_Text accessoryEnhanceButtonLabel;
        private GameObject enhancementOverlayRoot;
        private RectTransform enhancementRelicListRect;
        private TMP_Text enhancementOverlayTitleText;
        private TMP_Text enhancementOverlayInfoText;
        private TMP_Text enhancementOverlayResultText;
        private Image enhancementRuneImage;
        private Image enhancementEffectImage;
        private RectTransform enhancementRuneRect;
        private RectTransform enhancementEffectRect;
        private Sprite[] successEffectSprites;
        private Sprite[] failEffectSprites;
        private Sprite[] destroyEffectSprites;
        private string selectedEnhancementEquipmentInstanceId = string.Empty;
        private string enhancementLastMessage = string.Empty;
        private EquipmentEnhancementResultType activeEnhancementEffect = EquipmentEnhancementResultType.None;
        private float enhancementEffectTimer;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (!enhancementUiBuilt)
            {
                return;
            }

            AnimateEnhancementUi();
        }

        public void Refresh()
        {
            var gameManager = GameManager.Instance;
            var profile = gameManager != null ? gameManager.PlayerProfile : null;
            var selectedMonster = ResolveRepresentativeMonster(profile);
            if (playerStatusView != null)
            {
                playerStatusView.Bind(profile);
            }

            if (resourceView != null)
            {
                resourceView.Bind(profile);
            }

            if (equipmentStatusView != null)
            {
                var previewStats = CreateRepresentativeMonsterPreview(profile, selectedMonster);
                equipmentStatusView.Bind(
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Weapon),
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Armor),
                    GetEquipmentName(profile, selectedMonster, EquipmentSlotType.Accessory),
                    BuildSummary(selectedMonster, previewStats),
                    BuildEquipmentPolicyText(selectedMonster),
                    BuildLoadoutImpact(profile, selectedMonster));
                BindSelectedMonsterDetailButton(profile, selectedMonster);
            }

            BindEquipmentOption(bronzeBladeButton, bronzeBladeStatusText, profile, "equip_bronze_blade", "未所持");
            BindEquipmentOption(ironSwordButton, ironSwordStatusText, profile, "equip_iron_sword", "未所持");
            BindEquipmentOption(guardClothButton, guardClothStatusText, profile, "equip_guard_cloth", "未所持");
            BindEquipmentOption(boneMailButton, boneMailStatusText, profile, "equip_bone_mail", "未所持");
            BindEquipmentOption(ashenRingButton, ashenRingStatusText, profile, "equip_ashen_ring", "未所持");
            BindEquipmentOption(quickCharmButton, quickCharmStatusText, profile, "equip_quick_charm", "未所持");

            if (ctaText != null)
            {
                ctaText.text = BuildEquipmentHeadline(selectedMonster);
            }

            EnsureEnhancementUi();
            RefreshEnhancementLauncher(profile, selectedMonster);
            if (enhancementOverlayRoot != null && enhancementOverlayRoot.activeSelf)
            {
                RefreshEnhancementOverlay(profile);
            }
        }

        public void EquipBronzeBlade()
        {
            EquipWeapon("equip_bronze_blade");
        }

        public void EquipIronSword()
        {
            EquipWeapon("equip_iron_sword");
        }

        public void EquipGuardCloth()
        {
            EquipArmor("equip_guard_cloth");
        }

        public void EquipBoneMail()
        {
            EquipArmor("equip_bone_mail");
        }

        public void EquipAshenRing()
        {
            EquipAccessory("equip_ashen_ring");
        }

        public void EquipQuickCharm()
        {
            EquipAccessory("equip_quick_charm");
        }

        public void EquipWeapon(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipWeapon(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        public void EquipArmor(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipArmor(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        public void EquipAccessory(string equipmentId)
        {
            var profile = GameManager.Instance.PlayerProfile;
            if (profile == null || !HasEquipment(profile, equipmentId))
            {
                return;
            }

            profile.EquipAccessory(equipmentId);
            SaveManager.Instance.SaveCurrentGame();
            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        private void EnsureEnhancementUi()
        {
            if (enhancementUiBuilt)
            {
                return;
            }

            successEffectSprites = LoadSpriteSequence(EnhanceSuccessBasePath, EnhanceEffectFrameCount);
            failEffectSprites = LoadSpriteSequence(EnhanceFailBasePath, EnhanceEffectFrameCount);
            destroyEffectSprites = LoadSpriteSequence(EnhanceDestroyBasePath, EnhanceEffectFrameCount);

            enhancementLauncherRoot = CreatePanel("EquipmentEnhancementLauncher", transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(900f, 190f), EnhancePanelColor);

            enhancementLauncherTitleText = CreateText("Title", enhancementLauncherRoot.transform, "装備強化", 27f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(260f, 34f), TextAlignmentOptions.Center, EnhanceAccentColor);

            enhancementLauncherInfoText = CreateText("Info", enhancementLauncherRoot.transform, string.Empty, 16f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(780f, 26f), TextAlignmentOptions.Center, EnhanceSubTextColor);

            weaponEnhanceButton = CreateButton("WeaponEnhanceButton", enhancementLauncherRoot.transform, string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-292f, 22f), new Vector2(270f, 86f), new Color(0.22f, 0.20f, 0.33f, 0.98f),
                () => OpenEnhancementOverlay(EquipmentSlotType.Weapon), out weaponEnhanceButtonLabel);

            armorEnhanceButton = CreateButton("ArmorEnhanceButton", enhancementLauncherRoot.transform, string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(270f, 86f), new Color(0.18f, 0.27f, 0.34f, 0.98f),
                () => OpenEnhancementOverlay(EquipmentSlotType.Armor), out armorEnhanceButtonLabel);

            accessoryEnhanceButton = CreateButton("AccessoryEnhanceButton", enhancementLauncherRoot.transform, string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(292f, 22f), new Vector2(270f, 86f), new Color(0.18f, 0.32f, 0.25f, 0.98f),
                () => OpenEnhancementOverlay(EquipmentSlotType.Accessory), out accessoryEnhanceButtonLabel);

            enhancementOverlayRoot = CreatePanel("EquipmentEnhancementOverlay", transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));

            GameObject overlayPanel = CreatePanel("EquipmentEnhancementOverlayPanel", enhancementOverlayRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(910f, 1220f), new Color(0.035f, 0.045f, 0.068f, 0.98f));

            CreateText("Header", overlayPanel.transform, "強化炉", 40f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(300f, 48f), TextAlignmentOptions.Center, EnhanceAccentColor);

            CreateButton("CloseButton", overlayPanel.transform, "閉じる",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-34f, -34f), new Vector2(146f, 58f), new Color(0.34f, 0.18f, 0.16f, 0.98f),
                CloseEnhancementOverlay, out _);

            GameObject ritualArea = CreatePanel("RitualArea", overlayPanel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -250f), new Vector2(760f, 330f), new Color(0.012f, 0.018f, 0.026f, 0.86f));

            enhancementRuneImage = CreateImage("RuneCircle", ritualArea.transform, LoadSprite(EnhanceRuneCirclePath),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(310f, 310f));
            enhancementRuneRect = enhancementRuneImage.GetComponent<RectTransform>();
            enhancementRuneImage.raycastTarget = false;

            enhancementEffectImage = CreateImage("EnhancementEffect", ritualArea.transform, null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(330f, 330f));
            enhancementEffectRect = enhancementEffectImage.GetComponent<RectTransform>();
            enhancementEffectImage.raycastTarget = false;
            enhancementEffectImage.enabled = false;

            enhancementOverlayTitleText = CreateText("TargetTitle", overlayPanel.transform, string.Empty, 27f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -438f), new Vector2(760f, 38f), TextAlignmentOptions.Center, Color.white);

            enhancementOverlayInfoText = CreateText("TargetInfo", overlayPanel.transform, string.Empty, 18f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -482f), new Vector2(780f, 58f), TextAlignmentOptions.Center, EnhanceSubTextColor);

            enhancementOverlayResultText = CreateText("ResultMessage", overlayPanel.transform, string.Empty, 20f, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -548f), new Vector2(800f, 40f), TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.58f, 1f));

            enhancementRelicListRect = CreateUiObject("RelicList", overlayPanel.transform).GetComponent<RectTransform>();
            enhancementRelicListRect.anchorMin = new Vector2(0.5f, 1f);
            enhancementRelicListRect.anchorMax = new Vector2(0.5f, 1f);
            enhancementRelicListRect.pivot = new Vector2(0.5f, 1f);
            enhancementRelicListRect.anchoredPosition = new Vector2(0f, -620f);
            enhancementRelicListRect.sizeDelta = new Vector2(800f, 520f);

            enhancementOverlayRoot.SetActive(false);
            enhancementUiBuilt = true;
        }

        private void RefreshEnhancementLauncher(PlayerProfile profile, OwnedMonsterData selectedMonster)
        {
            if (enhancementLauncherRoot == null)
            {
                return;
            }

            bool hasTarget = profile != null && selectedMonster != null;
            enhancementLauncherRoot.SetActive(hasTarget);
            if (!hasTarget)
            {
                return;
            }

            if (enhancementLauncherInfoText != null)
            {
                enhancementLauncherInfoText.text = $"{ResolveMonsterName(selectedMonster)} の装備を強化できます。遺物を使うと成功/失敗演出が再生されます。";
            }

            BindEnhanceSlotButton(weaponEnhanceButton, weaponEnhanceButtonLabel, profile, selectedMonster, EquipmentSlotType.Weapon, "武器");
            BindEnhanceSlotButton(armorEnhanceButton, armorEnhanceButtonLabel, profile, selectedMonster, EquipmentSlotType.Armor, "防具");
            BindEnhanceSlotButton(accessoryEnhanceButton, accessoryEnhanceButtonLabel, profile, selectedMonster, EquipmentSlotType.Accessory, "装飾");
        }

        private void BindSelectedMonsterDetailButton(PlayerProfile profile, OwnedMonsterData selectedMonster)
        {
            if (equipmentStatusView == null)
            {
                return;
            }

            Button button = equipmentStatusView.GetComponent<Button>();
            if (button == null)
            {
                button = equipmentStatusView.gameObject.AddComponent<Button>();
            }

            Graphic targetGraphic = equipmentStatusView.GetComponent<Graphic>();
            if (targetGraphic == null)
            {
                Image image = equipmentStatusView.gameObject.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                targetGraphic = image;
            }

            targetGraphic.raycastTarget = true;
            button.targetGraphic = targetGraphic;
            button.onClick.RemoveAllListeners();
            button.interactable = selectedMonster != null;
            if (selectedMonster != null)
            {
                button.onClick.AddListener(() => ShowMonsterDetail(profile, selectedMonster));
            }
        }

        private void ShowMonsterDetail(PlayerProfile profile, OwnedMonsterData selectedMonster)
        {
            MasterDataManager masterDataManager = MasterDataManager.Instance;
            masterDataManager?.Initialize();
            MonsterDataSO monsterData = selectedMonster != null && masterDataManager != null
                ? masterDataManager.GetMonsterData(selectedMonster.MonsterId)
                : null;
            MonsterStatusDetailPopup.Show(transform, profile, selectedMonster, monsterData);
        }

        private void BindEnhanceSlotButton(Button button, TMP_Text label, PlayerProfile profile, OwnedMonsterData selectedMonster, EquipmentSlotType slotType, string slotLabel)
        {
            OwnedEquipmentData equipment = profile != null && selectedMonster != null
                ? profile.GetMonsterEquippedEquipment(selectedMonster.InstanceId, slotType)
                : null;
            EquipmentDataSO equipmentData = equipment != null && MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipment.EquipmentId)
                : null;

            bool canOpen = equipment != null && equipmentData != null;
            if (button != null)
            {
                button.interactable = canOpen;
                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = canOpen
                        ? new Color(image.color.r, image.color.g, image.color.b, 0.98f)
                        : new Color(0.13f, 0.14f, 0.16f, 0.78f);
                }
            }

            if (label == null)
            {
                return;
            }

            label.text = canOpen
                ? $"{slotLabel}強化\n{equipmentData.equipmentName}\n{EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / 残{equipment.RemainingEnhanceAttempts}"
                : $"{slotLabel}強化\n未装備\n-";
            label.color = canOpen ? Color.white : new Color(0.72f, 0.76f, 0.82f, 0.88f);
        }

        private void OpenEnhancementOverlay(EquipmentSlotType slotType)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            OwnedMonsterData selectedMonster = ResolveRepresentativeMonster(profile);
            OwnedEquipmentData equipment = profile != null && selectedMonster != null
                ? profile.GetMonsterEquippedEquipment(selectedMonster.InstanceId, slotType)
                : null;

            if (equipment == null)
            {
                enhancementLastMessage = "強化する装備がありません。先に装備をセットしてください。";
                Refresh();
                return;
            }

            EnsureEnhancementUi();
            selectedEnhancementEquipmentInstanceId = equipment.InstanceId;
            enhancementLastMessage = "使用する強化遺物を選んでください。";
            activeEnhancementEffect = EquipmentEnhancementResultType.None;
            enhancementEffectTimer = 0f;
            if (enhancementOverlayRoot != null)
            {
                enhancementOverlayRoot.SetActive(true);
            }

            RefreshEnhancementOverlay(profile);
        }

        private void CloseEnhancementOverlay()
        {
            selectedEnhancementEquipmentInstanceId = string.Empty;
            enhancementLastMessage = string.Empty;
            if (enhancementOverlayRoot != null)
            {
                enhancementOverlayRoot.SetActive(false);
            }
        }

        private void RefreshEnhancementOverlay(PlayerProfile profile)
        {
            if (enhancementOverlayRoot == null || enhancementRelicListRect == null)
            {
                return;
            }

            OwnedEquipmentData equipment = profile != null && !string.IsNullOrEmpty(selectedEnhancementEquipmentInstanceId)
                ? profile.GetOwnedEquipmentByInstanceId(selectedEnhancementEquipmentInstanceId)
                : null;
            EquipmentDataSO equipmentData = equipment != null && MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipment.EquipmentId)
                : null;

            if (enhancementOverlayTitleText != null)
            {
                enhancementOverlayTitleText.text = equipmentData != null ? equipmentData.equipmentName : "強化対象なし";
            }

            if (enhancementOverlayInfoText != null)
            {
                enhancementOverlayInfoText.text = equipment != null && equipmentData != null
                    ? $"現在 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / 強化Lv.{Mathf.Max(0, equipment.UpgradeLevel)} / 残り {Mathf.Max(0, equipment.RemainingEnhanceAttempts)}回 / {(equipment.IsLocked ? "ロック中" : "未ロック")}"
                    : "装備カードから強化対象を選んでください。";
            }

            if (enhancementOverlayResultText != null)
            {
                enhancementOverlayResultText.text = enhancementLastMessage;
            }

            ClearChildren(enhancementRelicListRect);
            if (profile == null)
            {
                return;
            }

            for (int i = 0; i < EquipmentEnhancementCatalog.AllRelics.Count; i += 1)
            {
                CreateRelicCard(profile, equipment, equipmentData, EquipmentEnhancementCatalog.AllRelics[i], i);
            }
        }

        private void CreateRelicCard(PlayerProfile profile, OwnedEquipmentData equipment, EquipmentDataSO equipmentData, EnhancementRelicDefinition relic, int index)
        {
            GameObject card = CreatePanel("RelicCard_" + index, enhancementRelicListRect,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -index * 148f), new Vector2(0f, 132f), new Color(0.08f, 0.10f, 0.135f, 0.96f));

            RawImage icon = CreateRawImage("RelicIcon", card.transform, LoadTexture(ResolveEnhancementRelicTexturePath(relic.RelicId)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(86f, 86f));
            icon.raycastTarget = false;

            CreateText("RelicName", card.transform, relic.RelicName, 24f, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(124f, -18f), new Vector2(250f, 30f), TextAlignmentOptions.Left, Color.white);

            int ownedCount = profile.GetEnhancementRelicAmount(relic.RelicId);
            string danger = relic.DestroysOnFailure ? " / 失敗時消滅" : string.Empty;
            CreateText("RelicMeta", card.transform,
                $"成功率 {(relic.SuccessRate * 100f):0.#}% / {EquipmentEnhancementCatalog.BuildRelicEffectSummary(equipmentData, relic)} / 所持 x{ownedCount}{danger}",
                15f, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(124f, -52f), new Vector2(-270f, 24f), TextAlignmentOptions.Left, new Color(0.95f, 0.78f, 0.48f, 1f));

            CreateText("RelicDescription", card.transform, relic.Description, 15f, FontStyles.Normal,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(124f, 18f), new Vector2(-270f, 42f), TextAlignmentOptions.TopLeft, EnhanceSubTextColor);

            Button useButton = CreateButton("UseButton", card.transform, "使用",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(128f, 48f), new Color(0.29f, 0.31f, 0.54f, 0.98f),
                () => UseEnhancementRelic(relic.RelicId), out _);

            bool canUse = equipment != null
                && equipment.RemainingEnhanceAttempts > 0
                && ownedCount > 0
                && (!equipment.IsLocked || !relic.DestroysOnFailure);
            useButton.interactable = canUse;
        }

        private void UseEnhancementRelic(string relicId)
        {
            PlayerProfile profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
            if (profile == null)
            {
                return;
            }

            EquipmentEnhancementResult result = profile.TryEnhanceEquipment(selectedEnhancementEquipmentInstanceId, relicId);
            enhancementLastMessage = result.Message;
            StartEnhancementEffect(result.ResultType);

            if (result.ResultType == EquipmentEnhancementResultType.Destroyed)
            {
                selectedEnhancementEquipmentInstanceId = string.Empty;
            }

            if (Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentGame();
            }

            Refresh();
            Object.FindObjectOfType<HomeSceneController>()?.RefreshAllPanels();
        }

        private void StartEnhancementEffect(EquipmentEnhancementResultType resultType)
        {
            activeEnhancementEffect = resultType;
            enhancementEffectTimer = resultType == EquipmentEnhancementResultType.None ? 0f : EnhanceEffectDuration;
            if (enhancementEffectImage != null)
            {
                enhancementEffectImage.transform.SetAsLastSibling();
            }
        }

        private void AnimateEnhancementUi()
        {
            float time = Application.isPlaying ? Time.unscaledTime : 0f;
            if (enhancementRuneRect != null)
            {
                float scale = 1f + Mathf.Sin(time * 3.1f) * 0.045f;
                enhancementRuneRect.localScale = Vector3.one * scale;
                enhancementRuneRect.localEulerAngles = new Vector3(0f, 0f, time * 18f);
            }

            if (enhancementEffectImage == null || enhancementEffectTimer <= 0f)
            {
                if (enhancementEffectImage != null)
                {
                    enhancementEffectImage.enabled = false;
                }
                return;
            }

            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
            enhancementEffectTimer = Mathf.Max(0f, enhancementEffectTimer - deltaTime);
            float progress = Mathf.Clamp01(1f - enhancementEffectTimer / EnhanceEffectDuration);
            Sprite[] frames = ResolveEnhancementEffectSprites(activeEnhancementEffect);
            if (frames != null && frames.Length > 0)
            {
                int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1);
                enhancementEffectImage.sprite = frames[frameIndex];
            }

            float alpha = Mathf.Sin(progress * Mathf.PI) * 0.95f;
            Color color = enhancementEffectImage.color;
            color.a = alpha;
            enhancementEffectImage.color = color;
            enhancementEffectImage.enabled = enhancementEffectImage.sprite != null && alpha > 0.02f;
            if (enhancementEffectRect != null)
            {
                float scale = 0.78f + progress * 0.52f;
                enhancementEffectRect.localScale = Vector3.one * scale;
            }
        }

        private Sprite[] ResolveEnhancementEffectSprites(EquipmentEnhancementResultType resultType)
        {
            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                    return successEffectSprites;
                case EquipmentEnhancementResultType.Destroyed:
                    return destroyEffectSprites;
                case EquipmentEnhancementResultType.Failed:
                    return failEffectSprites;
                default:
                    return failEffectSprites;
            }
        }

        private static bool HasEquipment(Data.PlayerProfile profile, string equipmentId)
        {
            return profile != null && profile.HasEquipment(equipmentId);
        }

        private static void BindEquipmentOption(Button button, TMP_Text statusText, Data.PlayerProfile profile, string equipmentId, string lockedLabel)
        {
            bool isOwned = HasEquipment(profile, equipmentId);
            bool isEquipped = IsEquipped(profile, equipmentId);

            if (button != null)
            {
                button.interactable = isOwned;
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isEquipped ? EquippedButtonColor : (isOwned ? UnlockedButtonColor : LockedButtonColor);
                }

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = isOwned ? Color.white : new Color(0.78f, 0.78f, 0.82f, 1f);
                }
            }

            if (statusText != null)
            {
                statusText.text = isEquipped ? "装備中" : (isOwned ? "所持" : lockedLabel);
                statusText.color = isEquipped ? EquippedStatusColor : (isOwned ? OwnedStatusColor : LockedStatusColor);
            }
        }

        private static bool IsEquipped(Data.PlayerProfile profile, string equipmentId)
        {
            return profile != null &&
                (profile.EquippedWeaponId == equipmentId ||
                 profile.EquippedArmorId == equipmentId ||
                 profile.EquippedAccessoryId == equipmentId);
        }

        private static string GetEquipmentName(Data.PlayerProfile profile, Save.OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            if (profile == null || monster == null)
            {
                return "-";
            }

            var equipped = profile.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            string equipmentId = equipped != null ? equipped.EquipmentId : string.Empty;
            if (string.IsNullOrEmpty(equipmentId))
            {
                return "-";
            }

            var equipmentData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipmentId)
                : null;
            return equipmentData != null ? equipmentData.equipmentName : equipmentId;
        }

        private static string BuildSummary(Save.OwnedMonsterData monster, BattleUnitStats stats)
        {
            if (stats == null)
            {
                return "戦力プレビューを取得できません";
            }

            string monsterLabel = ResolveMonsterName(monster);
            string individualLabel = MonsterIndividualValueService.BuildAverageLabel(monster);
            return $"{monsterLabel}  {individualLabel}  HP {stats.MaxHp}  ATK {stats.Attack}  DEF {stats.Defense}  CRIT {(stats.CritRate * 100f):0.#}%  SPD {stats.AttackSpeed:0.##}\n評価: {BuildGrade(stats)}";
        }

        private static string BuildEquipmentPolicyText(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "装備方針: 装備対象モンスターを選択してください";
            }

            return "装備方針: 武器 / 防具 / 装飾をモンスター個別に装備します";
        }

        private static string BuildLoadoutImpact(Data.PlayerProfile profile, Save.OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return "強化情報: 選択中モンスターの装備情報を表示します";
            }

            return $"強化情報: {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Weapon, "武器")} / {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Armor, "防具")} / {BuildEquipmentImpact(profile, monster, EquipmentSlotType.Accessory, "装飾")}\n遺物: 安定={profile.GetEnhancementRelicAmount("relic_safe_ember")} 挑戦={profile.GetEnhancementRelicAmount("relic_risky_ember")} 破滅={profile.GetEnhancementRelicAmount("relic_volatile_ember")}";
        }

        private static string BuildEquipmentImpact(Data.PlayerProfile profile, Save.OwnedMonsterData monster, EquipmentSlotType slotType, string slotLabel)
        {
            if (profile == null || monster == null || MasterDataManager.Instance == null)
            {
                return $"{slotLabel} なし";
            }

            var equipped = profile.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            string equipmentId = equipped != null ? equipped.EquipmentId : string.Empty;
            if (string.IsNullOrEmpty(equipmentId))
            {
                return $"{slotLabel} なし";
            }

            var equipmentData = MasterDataManager.Instance.GetEquipmentData(equipmentId);
            if (equipmentData == null)
            {
                return $"{slotLabel} 不明";
            }

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            var resolvedBonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, equipped);
            if (resolvedBonus.Attack != 0)
            {
                parts.Add($"攻+{resolvedBonus.Attack}");
            }

            if (resolvedBonus.Defense != 0)
            {
                parts.Add($"防+{resolvedBonus.Defense}");
            }

            if (resolvedBonus.Hp != 0)
            {
                parts.Add($"HP+{resolvedBonus.Hp}");
            }

            if (resolvedBonus.CritRate != 0f)
            {
                parts.Add($"会心+{resolvedBonus.CritRate * 100f:0.#}%");
            }

            if (resolvedBonus.AttackSpeed != 0f)
            {
                parts.Add($"速+{resolvedBonus.AttackSpeed:0.##}");
            }

            if (parts.Count == 0)
            {
                parts.Add("補正なし");
            }

            return $"{slotLabel} {equipmentData.equipmentName} ({string.Join(", ", parts)})";
        }

        private static string BuildGrade(BattleUnitStats stats)
        {
            float score = stats.MaxHp * 0.12f + stats.Attack * 1.5f + stats.Defense * 1.2f + stats.CritRate * 60f + stats.AttackSpeed * 8f;
            if (score >= 60f)
            {
                return "最前線";
            }

            if (score >= 42f)
            {
                return "安定";
            }

            if (score >= 32f)
            {
                return "発展途上";
            }

            return "脆い";
        }

        private static Save.OwnedMonsterData ResolveRepresentativeMonster(Data.PlayerProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            foreach (string instanceId in profile.PartyMonsterInstanceIds)
            {
                var partyMonster = profile.GetOwnedMonster(instanceId);
                if (partyMonster != null)
                {
                    return partyMonster;
                }
            }

            for (int i = 0; i < profile.OwnedMonsters.Count; i += 1)
            {
                if (profile.OwnedMonsters[i] != null)
                {
                    return profile.OwnedMonsters[i];
                }
            }

            return null;
        }

        private static BattleUnitStats CreateRepresentativeMonsterPreview(Data.PlayerProfile profile, Save.OwnedMonsterData monster)
        {
            if (profile == null || monster == null)
            {
                return null;
            }

            var monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monster.MonsterId)
                : null;
            return MonsterBattleStatsFactory.Create(profile, monster, monsterData);
        }

        private static string BuildEquipmentHeadline(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "装備管理: 所持モンスターを入手すると個別装備を確認できます。";
            }

            return $"{ResolveMonsterName(monster)} の個別装備を確認中。ロックと強化遺物で装備を管理できます。";
        }

        private static string ResolveMonsterName(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "モンスター";
            }

            var monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monster.MonsterId)
                : null;
            return monsterData != null ? monsterData.monsterName : monster.MonsterId;
        }

        private static string ResolveEnhancementRelicTexturePath(string relicId)
        {
            switch (relicId)
            {
                case "relic_safe_ember":
                    return "EquipmentRelics/relic_safe_ember_icon";
                case "relic_risky_ember":
                    return "EquipmentRelics/relic_risky_ember_icon";
                case "relic_volatile_ember":
                    return "EquipmentRelics/relic_volatile_ember_icon";
                default:
                    return string.Empty;
            }
        }

        private Sprite[] LoadSpriteSequence(string basePath, int count)
        {
            Sprite[] sprites = new Sprite[Mathf.Max(0, count)];
            for (int i = 0; i < sprites.Length; i += 1)
            {
                sprites[i] = LoadSprite(basePath + i);
            }

            return sprites;
        }

        private Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (spriteCache.TryGetValue(resourcePath, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Texture2D texture = LoadTexture(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            spriteCache[resourcePath] = sprite;
            return sprite;
        }

        private Texture2D LoadTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (textureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            textureCache[resourcePath] = texture;
            return texture;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject panel = CreateUiObject(objectName, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color,
            UnityEngine.Events.UnityAction onClick,
            out TMP_Text label)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);

            label = CreateText("Label", buttonObject.transform, text, 17f, FontStyles.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(sizeDelta.x - 16f, sizeDelta.y - 12f), TextAlignmentOptions.Center, Color.white);
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 17f;
            return button;
        }

        private Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            return image;
        }

        private static RawImage CreateRawImage(
            string objectName,
            Transform parent,
            Texture texture,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            RawImage image = imageObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            return image;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i -= 1)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
