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
        private enum EquipmentListFilter
        {
            All,
            Weapon,
            Armor,
            Accessory,
            Physical,
            Magic
        }

        private enum EquipmentListSortMode
        {
            Default,
            Rarity,
            Power,
            Name
        }

        private sealed class EquipmentOptionBinding
        {
            public string EquipmentId;
            public Button Button;
            public TMP_Text StatusText;
            public Vector2 ButtonPosition;
            public Vector2 StatusPosition;
            public int OriginalIndex;
            public bool HasCapturedLayout;
        }

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
        private readonly List<EquipmentOptionBinding> equipmentOptionBindings = new List<EquipmentOptionBinding>();
        private bool equipmentListControlsBuilt;
        private GameObject equipmentListControlsRoot;
        private Button allEquipmentFilterButton;
        private Button weaponEquipmentFilterButton;
        private Button armorEquipmentFilterButton;
        private Button accessoryEquipmentFilterButton;
        private Button physicalEquipmentFilterButton;
        private Button magicEquipmentFilterButton;
        private Button equipmentSortButton;
        private TMP_Text allEquipmentFilterLabel;
        private TMP_Text weaponEquipmentFilterLabel;
        private TMP_Text armorEquipmentFilterLabel;
        private TMP_Text accessoryEquipmentFilterLabel;
        private TMP_Text physicalEquipmentFilterLabel;
        private TMP_Text magicEquipmentFilterLabel;
        private TMP_Text equipmentSortLabel;
        private TMP_Text equipmentListSummaryText;
        private EquipmentListFilter currentEquipmentListFilter = EquipmentListFilter.All;
        private EquipmentListSortMode currentEquipmentListSortMode = EquipmentListSortMode.Default;

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
            EnsureEquipmentListControls();
            RefreshEquipmentListControls(profile);
            ApplyEquipmentListFilterAndSort(profile);

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

        private void EnsureEquipmentListControls()
        {
            EnsureEquipmentOptionBindings();
            if (equipmentListControlsBuilt)
            {
                return;
            }

            equipmentListControlsRoot = CreatePanel("EquipmentListControls", transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(930f, 96f), new Color(0.025f, 0.04f, 0.058f, 0.90f));

            CreateText("Title", equipmentListControlsRoot.transform, "装備一覧", 20f, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -10f), new Vector2(130f, 26f), TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.54f, 1f));

            equipmentListSummaryText = CreateText("Summary", equipmentListControlsRoot.transform, string.Empty, 15f, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(150f, -12f), new Vector2(-170f, 24f), TextAlignmentOptions.Left, EnhanceSubTextColor);

            allEquipmentFilterButton = CreateButton("AllFilterButton", equipmentListControlsRoot.transform, "全て",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 14f), new Vector2(82f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.All), out allEquipmentFilterLabel);

            weaponEquipmentFilterButton = CreateButton("WeaponFilterButton", equipmentListControlsRoot.transform, "武器",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(110f, 14f), new Vector2(82f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.Weapon), out weaponEquipmentFilterLabel);

            armorEquipmentFilterButton = CreateButton("ArmorFilterButton", equipmentListControlsRoot.transform, "防具",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(200f, 14f), new Vector2(82f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.Armor), out armorEquipmentFilterLabel);

            accessoryEquipmentFilterButton = CreateButton("AccessoryFilterButton", equipmentListControlsRoot.transform, "装飾品",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(290f, 14f), new Vector2(104f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.Accessory), out accessoryEquipmentFilterLabel);

            physicalEquipmentFilterButton = CreateButton("PhysicalFilterButton", equipmentListControlsRoot.transform, "物理",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(402f, 14f), new Vector2(86f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.Physical), out physicalEquipmentFilterLabel);

            magicEquipmentFilterButton = CreateButton("MagicFilterButton", equipmentListControlsRoot.transform, "魔法",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(496f, 14f), new Vector2(86f, 42f), UnlockedButtonColor,
                () => SetEquipmentListFilter(EquipmentListFilter.Magic), out magicEquipmentFilterLabel);

            equipmentSortButton = CreateButton("SortButton", equipmentListControlsRoot.transform, string.Empty,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-20f, 14f), new Vector2(260f, 42f), new Color(0.23f, 0.31f, 0.42f, 0.98f),
                CycleEquipmentListSortMode, out equipmentSortLabel);

            equipmentListControlsBuilt = true;
        }

        private void EnsureEquipmentOptionBindings()
        {
            if (equipmentOptionBindings.Count > 0)
            {
                return;
            }

            AddEquipmentOptionBinding("equip_bronze_blade", bronzeBladeButton, bronzeBladeStatusText);
            AddEquipmentOptionBinding("equip_iron_sword", ironSwordButton, ironSwordStatusText);
            AddEquipmentOptionBinding("equip_guard_cloth", guardClothButton, guardClothStatusText);
            AddEquipmentOptionBinding("equip_bone_mail", boneMailButton, boneMailStatusText);
            AddEquipmentOptionBinding("equip_ashen_ring", ashenRingButton, ashenRingStatusText);
            AddEquipmentOptionBinding("equip_quick_charm", quickCharmButton, quickCharmStatusText);
        }

        private void AddEquipmentOptionBinding(string equipmentId, Button button, TMP_Text statusText)
        {
            var binding = new EquipmentOptionBinding
            {
                EquipmentId = equipmentId,
                Button = button,
                StatusText = statusText,
                OriginalIndex = equipmentOptionBindings.Count
            };
            CaptureEquipmentOptionLayout(binding);
            equipmentOptionBindings.Add(binding);
        }

        private static void CaptureEquipmentOptionLayout(EquipmentOptionBinding binding)
        {
            if (binding == null || binding.HasCapturedLayout)
            {
                return;
            }

            RectTransform buttonRect = binding.Button != null ? binding.Button.GetComponent<RectTransform>() : null;
            RectTransform statusRect = binding.StatusText != null ? binding.StatusText.GetComponent<RectTransform>() : null;
            binding.ButtonPosition = buttonRect != null ? buttonRect.anchoredPosition : Vector2.zero;
            binding.StatusPosition = statusRect != null ? statusRect.anchoredPosition : Vector2.zero;
            binding.HasCapturedLayout = true;
        }

        private void SetEquipmentListFilter(EquipmentListFilter filter)
        {
            currentEquipmentListFilter = filter;
            Refresh();
        }

        private void CycleEquipmentListSortMode()
        {
            currentEquipmentListSortMode = currentEquipmentListSortMode switch
            {
                EquipmentListSortMode.Default => EquipmentListSortMode.Rarity,
                EquipmentListSortMode.Rarity => EquipmentListSortMode.Power,
                EquipmentListSortMode.Power => EquipmentListSortMode.Name,
                _ => EquipmentListSortMode.Default
            };
            Refresh();
        }

        private void RefreshEquipmentListControls(PlayerProfile profile)
        {
            SetFilterButtonState(allEquipmentFilterButton, allEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.All);
            SetFilterButtonState(weaponEquipmentFilterButton, weaponEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.Weapon);
            SetFilterButtonState(armorEquipmentFilterButton, armorEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.Armor);
            SetFilterButtonState(accessoryEquipmentFilterButton, accessoryEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.Accessory);
            SetFilterButtonState(physicalEquipmentFilterButton, physicalEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.Physical);
            SetFilterButtonState(magicEquipmentFilterButton, magicEquipmentFilterLabel, currentEquipmentListFilter == EquipmentListFilter.Magic);

            if (equipmentSortLabel != null)
            {
                equipmentSortLabel.text = "並び替え: " + GetEquipmentSortLabel(currentEquipmentListSortMode);
            }

            if (equipmentListSummaryText != null)
            {
                int visibleCount = CountVisibleEquipmentOptions(profile);
                equipmentListSummaryText.text = $"{GetEquipmentFilterLabel(currentEquipmentListFilter)} / {GetEquipmentSortLabel(currentEquipmentListSortMode)} / 表示 {visibleCount}件";
            }
        }

        private void ApplyEquipmentListFilterAndSort(PlayerProfile profile)
        {
            EnsureEquipmentOptionBindings();
            var visibleBindings = new List<EquipmentOptionBinding>();
            for (int i = 0; i < equipmentOptionBindings.Count; i += 1)
            {
                EquipmentOptionBinding binding = equipmentOptionBindings[i];
                CaptureEquipmentOptionLayout(binding);
                if (PassesEquipmentFilter(binding))
                {
                    visibleBindings.Add(binding);
                }
                else
                {
                    SetEquipmentOptionActive(binding, false);
                }
            }

            visibleBindings.Sort((left, right) => CompareEquipmentOptions(left, right, profile));
            for (int i = 0; i < visibleBindings.Count; i += 1)
            {
                EquipmentOptionBinding binding = visibleBindings[i];
                EquipmentOptionBinding slotBinding = equipmentOptionBindings[Mathf.Clamp(i, 0, equipmentOptionBindings.Count - 1)];
                SetEquipmentOptionActive(binding, true);
                ApplyEquipmentOptionPosition(binding, slotBinding);
            }
        }

        private int CountVisibleEquipmentOptions(PlayerProfile profile)
        {
            EnsureEquipmentOptionBindings();
            int count = 0;
            for (int i = 0; i < equipmentOptionBindings.Count; i += 1)
            {
                if (PassesEquipmentFilter(equipmentOptionBindings[i]))
                {
                    count += 1;
                }
            }

            return count;
        }

        private bool PassesEquipmentFilter(EquipmentOptionBinding binding)
        {
            EquipmentDataSO equipmentData = GetEquipmentData(binding);
            if (equipmentData == null)
            {
                return currentEquipmentListFilter == EquipmentListFilter.All;
            }

            switch (currentEquipmentListFilter)
            {
                case EquipmentListFilter.Weapon:
                    return equipmentData.slotType == EquipmentSlotType.Weapon;
                case EquipmentListFilter.Armor:
                    return equipmentData.slotType == EquipmentSlotType.Armor;
                case EquipmentListFilter.Accessory:
                    return equipmentData.slotType == EquipmentSlotType.Accessory;
                case EquipmentListFilter.Physical:
                    return EquipmentEnhancementCatalog.IsPhysicalFocusedEquipment(equipmentData);
                case EquipmentListFilter.Magic:
                    return EquipmentEnhancementCatalog.IsMagicFocusedEquipment(equipmentData);
                default:
                    return true;
            }
        }

        private static void SetFilterButtonState(Button button, TMP_Text label, bool isActive)
        {
            if (button != null)
            {
                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isActive
                        ? new Color(0.28f, 0.48f, 0.36f, 0.98f)
                        : new Color(0.14f, 0.20f, 0.27f, 0.92f);
                }
            }

            if (label != null)
            {
                label.color = isActive ? Color.white : new Color(0.74f, 0.84f, 0.92f, 0.95f);
            }
        }

        private int CompareEquipmentOptions(EquipmentOptionBinding left, EquipmentOptionBinding right, PlayerProfile profile)
        {
            if (left == null || right == null)
            {
                return left == null ? right == null ? 0 : 1 : -1;
            }

            int result;
            switch (currentEquipmentListSortMode)
            {
                case EquipmentListSortMode.Rarity:
                    result = ResolveEquipmentRarityRank(right, profile).CompareTo(ResolveEquipmentRarityRank(left, profile));
                    if (result != 0) return result;
                    result = ResolveEquipmentPowerScore(right, profile).CompareTo(ResolveEquipmentPowerScore(left, profile));
                    if (result != 0) return result;
                    break;
                case EquipmentListSortMode.Power:
                    result = ResolveEquipmentPowerScore(right, profile).CompareTo(ResolveEquipmentPowerScore(left, profile));
                    if (result != 0) return result;
                    result = ResolveEquipmentRarityRank(right, profile).CompareTo(ResolveEquipmentRarityRank(left, profile));
                    if (result != 0) return result;
                    break;
                case EquipmentListSortMode.Name:
                    result = string.Compare(ResolveEquipmentDisplayName(left), ResolveEquipmentDisplayName(right), System.StringComparison.CurrentCulture);
                    if (result != 0) return result;
                    break;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static void SetEquipmentOptionActive(EquipmentOptionBinding binding, bool isActive)
        {
            if (binding == null)
            {
                return;
            }

            if (binding.Button != null)
            {
                binding.Button.gameObject.SetActive(isActive);
            }

            if (binding.StatusText != null && !IsStatusTextChildOfButton(binding))
            {
                binding.StatusText.gameObject.SetActive(isActive);
            }
        }

        private static void ApplyEquipmentOptionPosition(EquipmentOptionBinding binding, EquipmentOptionBinding slotBinding)
        {
            if (binding == null || slotBinding == null)
            {
                return;
            }

            RectTransform buttonRect = binding.Button != null ? binding.Button.GetComponent<RectTransform>() : null;
            if (buttonRect != null)
            {
                buttonRect.anchoredPosition = slotBinding.ButtonPosition;
            }

            RectTransform statusRect = binding.StatusText != null ? binding.StatusText.GetComponent<RectTransform>() : null;
            if (statusRect != null && !IsStatusTextChildOfButton(binding))
            {
                statusRect.anchoredPosition = slotBinding.StatusPosition;
            }
        }

        private static bool IsStatusTextChildOfButton(EquipmentOptionBinding binding)
        {
            return binding != null &&
                binding.Button != null &&
                binding.StatusText != null &&
                binding.StatusText.transform.IsChildOf(binding.Button.transform);
        }

        private static int ResolveEquipmentRarityRank(EquipmentOptionBinding binding, PlayerProfile profile)
        {
            EquipmentDataSO equipmentData = GetEquipmentData(binding);
            OwnedEquipmentData ownedEquipment = profile != null && binding != null
                ? profile.GetFirstOwnedEquipmentByEquipmentId(binding.EquipmentId)
                : null;
            return EquipmentEnhancementCatalog.ResolveQualityRank(equipmentData, ownedEquipment);
        }

        private static float ResolveEquipmentPowerScore(EquipmentOptionBinding binding, PlayerProfile profile)
        {
            EquipmentDataSO equipmentData = GetEquipmentData(binding);
            if (equipmentData == null)
            {
                return 0f;
            }

            OwnedEquipmentData ownedEquipment = profile != null && binding != null
                ? profile.GetFirstOwnedEquipmentByEquipmentId(binding.EquipmentId)
                : null;
            if (ownedEquipment != null)
            {
                EquipmentResolvedBonus bonus = EquipmentEnhancementCatalog.ResolveEquipmentBonus(equipmentData, ownedEquipment);
                return ((bonus.AttackPercent + bonus.WisdomPercent) * 120f)
                    + ((bonus.DefensePercent + bonus.MagicDefensePercent) * 105f)
                    + (bonus.HpPercent * 55f)
                    + (bonus.CritRate * 130f)
                    + (bonus.AttackSpeed * 45f);
            }

            float flatScore =
                equipmentData.baseAttack * 1.20f +
                equipmentData.baseWisdom * 1.20f +
                equipmentData.baseDefense * 1.00f +
                equipmentData.baseMagicDefense * 1.00f +
                equipmentData.baseHp * 0.24f;
            float specialScore =
                equipmentData.bonusCritRate * 120f +
                equipmentData.bonusAttackSpeed * 45f;
            return flatScore + specialScore;
        }

        private static EquipmentDataSO GetEquipmentData(EquipmentOptionBinding binding)
        {
            return binding != null && MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(binding.EquipmentId)
                : null;
        }

        private static string ResolveEquipmentDisplayName(EquipmentOptionBinding binding)
        {
            EquipmentDataSO equipmentData = GetEquipmentData(binding);
            return equipmentData != null ? equipmentData.equipmentName : binding != null ? binding.EquipmentId : string.Empty;
        }

        private static string GetEquipmentFilterLabel(EquipmentListFilter filter)
        {
            switch (filter)
            {
                case EquipmentListFilter.Weapon:
                    return "武器";
                case EquipmentListFilter.Armor:
                    return "防具";
                case EquipmentListFilter.Accessory:
                    return "装飾品";
                case EquipmentListFilter.Physical:
                    return "物理";
                case EquipmentListFilter.Magic:
                    return "魔法";
                default:
                    return "全て";
            }
        }

        private static string GetEquipmentSortLabel(EquipmentListSortMode sortMode)
        {
            switch (sortMode)
            {
                case EquipmentListSortMode.Rarity:
                    return "レア度";
                case EquipmentListSortMode.Power:
                    return "能力値";
                case EquipmentListSortMode.Name:
                    return "名前";
                default:
                    return "初期順";
            }
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
                ? $"{slotLabel}強化\n{equipmentData.equipmentName}  {EquipmentEnhancementCatalog.BuildQualityLabel(equipmentData, equipment)}\n{EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipment)}"
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
                enhancementOverlayTitleText.text = equipmentData != null
                    ? $"{equipmentData.equipmentName}  {EquipmentEnhancementCatalog.BuildQualityLabel(equipmentData, equipment)}"
                    : "強化対象なし";
            }

            if (enhancementOverlayInfoText != null)
            {
                enhancementOverlayInfoText.text = equipment != null && equipmentData != null
                    ? $"現在 {EquipmentEnhancementCatalog.BuildEnhancementSummary(equipmentData, equipment)} / {EquipmentEnhancementCatalog.BuildEnhanceAttemptsLabel(equipmentData, equipment)} / {(equipment.IsLocked ? "ロック中" : "未ロック")}"
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
                $"成功率 {(relic.SuccessRate * 100f):0.#}% / {EquipmentEnhancementCatalog.BuildRelicEffectSummary(equipmentData, equipment, relic)} / 所持 x{ownedCount}{danger}",
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
            PlayEnhancementResultSe(result.ResultType);
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

        private static void PlayEnhancementResultSe(EquipmentEnhancementResultType resultType)
        {
            switch (resultType)
            {
                case EquipmentEnhancementResultType.Success:
                    AudioManager.Instance?.PlaySe(AudioCue.UpgradeSuccess);
                    break;
                case EquipmentEnhancementResultType.Failed:
                    AudioManager.Instance?.PlaySe(AudioCue.UpgradeFail);
                    break;
                case EquipmentEnhancementResultType.Destroyed:
                    AudioManager.Instance?.PlaySe(AudioCue.UpgradeBreak);
                    break;
                default:
                    AudioManager.Instance?.PlaySe(AudioCue.Error);
                    break;
            }
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
            EquipmentDataSO equipmentData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetEquipmentData(equipmentId)
                : null;
            string displayName = equipmentData != null ? equipmentData.equipmentName : equipmentId;

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
                    label.text = displayName;
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
            return equipmentData != null
                ? $"{equipmentData.equipmentName}[{EquipmentEnhancementCatalog.ResolveQualityName(equipmentData, equipped)}]"
                : equipmentId;
        }

        private static string BuildSummary(Save.OwnedMonsterData monster, BattleUnitStats stats)
        {
            if (stats == null)
            {
                return "戦力プレビューを取得できません";
            }

            string monsterLabel = ResolveMonsterName(monster);
            string damageTypeLabel = ResolveMonsterDamageTypeLabel(monster);
            string individualLabel = MonsterIndividualValueService.BuildAverageLabel(monster);
            return $"{monsterLabel}  {damageTypeLabel}  {individualLabel}  HP {stats.MaxHp}  ATK {stats.Attack}  DEF {stats.Defense}  CRIT {(stats.CritRate * 100f):0.#}%  SPD {stats.AttackSpeed:0.###}\n評価: {BuildGrade(stats)}";
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
            if (resolvedBonus.AttackPercent > 0f)
            {
                parts.Add($"攻+{resolvedBonus.AttackPercent * 100f:0.#}%");
            }

            if (resolvedBonus.WisdomPercent > 0f)
            {
                parts.Add($"賢+{resolvedBonus.WisdomPercent * 100f:0.#}%");
            }

            if (resolvedBonus.DefensePercent > 0f)
            {
                parts.Add($"防+{resolvedBonus.DefensePercent * 100f:0.#}%");
            }

            if (resolvedBonus.MagicDefensePercent > 0f)
            {
                parts.Add($"魔防+{resolvedBonus.MagicDefensePercent * 100f:0.#}%");
            }

            if (resolvedBonus.HpPercent > 0f)
            {
                parts.Add($"HP+{resolvedBonus.HpPercent * 100f:0.#}%");
            }

            if (resolvedBonus.CritRate > 0f)
            {
                parts.Add($"会心+{resolvedBonus.CritRate * 100f:0.#}%");
            }

            if (resolvedBonus.AttackSpeed > 0f)
            {
                parts.Add($"速+{resolvedBonus.AttackSpeed:0.###}");
            }

            if (parts.Count == 0)
            {
                parts.Add("補正なし");
            }

            return $"{slotLabel} {equipmentData.equipmentName}[{EquipmentEnhancementCatalog.ResolveQualityName(equipmentData, equipped)}] ({string.Join(", ", parts)})";
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

            return $"{ResolveMonsterName(monster)}（{ResolveMonsterDamageTypeLabel(monster)}）の個別装備を確認中。ロックと強化遺物で装備を管理できます。";
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

        private static string ResolveMonsterDamageTypeLabel(Save.OwnedMonsterData monster)
        {
            if (monster == null)
            {
                return "型不明";
            }

            var monsterData = MasterDataManager.Instance != null
                ? MasterDataManager.Instance.GetMonsterData(monster.MonsterId)
                : null;
            return monsterData != null ? ResolveDamageTypeLabel(monsterData.damageType) : "型不明";
        }

        private static string ResolveDamageTypeLabel(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法型" : "物理型";
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
