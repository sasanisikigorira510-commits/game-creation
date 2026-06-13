using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.UI
{
    public static class MonsterStatusDetailPopup
    {
        private const string PopupObjectName = "MonsterStatusDetailPopup";
        private const string PanelTexturePath = "UI/MonsterDetail/MonsterDetailPanel";
        private const string StatRowTexturePath = "UI/MonsterDetail/MonsterDetailStatRow";
        private const string CloseButtonTexturePath = "UI/MonsterDetail/MonsterDetailCloseButton";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color TextMain = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextSub = new Color(0.74f, 0.88f, 0.96f, 0.94f);
        private static readonly Color TextGold = new Color(1f, 0.82f, 0.42f, 1f);
        private static readonly Color TextGreen = new Color(0.58f, 1f, 0.72f, 1f);
        private static readonly Color TextWarn = new Color(1f, 0.56f, 0.54f, 1f);
        private static readonly Color TextBackgroundColor = new Color(0.015f, 0.025f, 0.035f, 0.82f);
        private static readonly Color TextBackgroundEdgeColor = new Color(0.18f, 0.42f, 0.58f, 0.55f);
        private static readonly Color ReleaseFillColor = new Color(0.36f, 0.075f, 0.055f, 0.98f);
        private static readonly Color ReleaseBorderColor = new Color(1f, 0.62f, 0.32f, 0.96f);
        private static readonly Color DisabledReleaseFillColor = new Color(0.12f, 0.12f, 0.12f, 0.86f);
        private static readonly Color DisabledReleaseBorderColor = new Color(0.55f, 0.52f, 0.48f, 0.80f);

        public static void Show(
            Transform parent,
            PlayerProfile profile,
            OwnedMonsterData monster,
            MonsterDataSO monsterData,
            Func<bool> onReleaseConfirmed = null,
            bool canRelease = false,
            string releaseMessage = "")
        {
            if (parent == null || monster == null || monsterData == null)
            {
                return;
            }

            Transform existing = parent.Find(PopupObjectName);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            GameObject overlay = CreateUiObject(PopupObjectName, parent);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image backdrop = overlay.AddComponent<Image>();
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = true;

            Button backdropButton = overlay.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));

            Font font = GetRuntimeFont();
            Texture2D panelTexture = Resources.Load<Texture2D>(PanelTexturePath);
            Texture2D closeTexture = Resources.Load<Texture2D>(CloseButtonTexturePath);

            GameObject panel = CreateRawPanel("Panel", overlay.transform, panelTexture,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1030f, 1320f), Color.white);
            RawImage panelGraphic = panel.GetComponent<RawImage>();
            panelGraphic.raycastTarget = true;
            Button panelBlocker = panel.AddComponent<Button>();
            panelBlocker.targetGraphic = panelGraphic;

            CreateTextBackground("HeaderBlock", panel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(650f, 136f));

            Text titleText = CreateText("Title", panel.transform, font, monsterData.monsterName, 44, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -74f), new Vector2(650f, 58f), TextAnchor.MiddleCenter, TextMain);
            titleText.alignByGeometry = true;

            Text subtitleText = CreateText("SubTitle", panel.transform, font, BuildMetaLine(monster, monsterData), 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(650f, 36f), TextAnchor.MiddleCenter, TextSub);
            subtitleText.alignByGeometry = true;

            GameObject closeButton = CreateRawPanel("CloseButton", panel.transform, closeTexture,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-128f, -116f), new Vector2(106f, 96f), Color.white);
            Button close = closeButton.AddComponent<Button>();
            RawImage closeGraphic = closeButton.GetComponent<RawImage>();
            closeGraphic.raycastTarget = true;
            close.targetGraphic = closeGraphic;
            close.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
            CreateCloseButtonMark(closeButton.transform);

            CreatePortrait(panel.transform, monsterData, new Vector2(-250f, -360f), 270f);
            CreateIdentityBlock(panel.transform, font, monster, monsterData, profile);

            BattleUnitStats stats = MonsterBattleStatsFactory.Create(profile, monster, monsterData);
            CreateSectionTitle(panel.transform, font, "戦闘ステータス", -500f);
            CreateStatRow(panel.transform, font, -560f, "HP", stats != null ? stats.MaxHp.ToString() : "-", "攻撃", stats != null ? stats.Attack.ToString() : "-");
            CreateStatRow(panel.transform, font, -622f, "魔攻", stats != null ? stats.Wisdom.ToString() : "-", "防御", stats != null ? stats.Defense.ToString() : "-");
            CreateStatRow(panel.transform, font, -684f, "魔防", stats != null ? stats.MagicDefense.ToString() : "-", "攻速", stats != null ? stats.AttackSpeed.ToString("0.##") : "-");
            CreateStatRow(panel.transform, font, -746f, "会心率", stats != null ? $"{stats.CritRate * 100f:0.#}%" : "-", "会心倍率", stats != null ? $"{stats.CritDamage:0.##}x" : "-");

            MonsterIndividualValueService.EnsureInitialized(monster);
            CreateSectionTitle(panel.transform, font, $"個体値  平均 {MonsterIndividualValueService.GetAverage(monster)}", -824f);
            CreateStatRow(panel.transform, font, -884f, "HP", monster.IndividualHp.ToString(), "攻撃", monster.IndividualAttack.ToString(), true);
            CreateStatRow(panel.transform, font, -946f, "魔攻", monster.IndividualWisdom.ToString(), "防御", monster.IndividualDefense.ToString(), true);
            CreateStatRow(panel.transform, font, -1008f, "魔防", monster.IndividualMagicDefense.ToString(), "攻速", monster.IndividualAttackSpeed.ToString(), true);

            bool hasReleaseControls = onReleaseConfirmed != null;
            CreateBottomInfoFrame(panel.transform, hasReleaseControls);
            CreateText("BottomBonusEquipment", panel.transform, font, BuildBonusAndEquipmentText(profile, monster), 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1082f), new Vector2(820f, 68f), TextAnchor.MiddleCenter, TextSub);

            if (hasReleaseControls)
            {
                CreateReleaseControls(panel.transform, overlay, font, monsterData.monsterName, onReleaseConfirmed, canRelease, releaseMessage);
            }

            overlay.transform.SetAsLastSibling();
        }

        private static void CreateReleaseControls(
            Transform parent,
            GameObject overlay,
            Font font,
            string monsterName,
            Func<bool> onReleaseConfirmed,
            bool canRelease,
            string releaseMessage)
        {
            GameObject releaseButton = CreateActionButton("ReleaseButton", parent, font, "逃がす",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-254f, -1206f), new Vector2(300f, 72f),
                canRelease ? ReleaseFillColor : DisabledReleaseFillColor);
            ApplyButtonFrame(releaseButton, canRelease ? ReleaseBorderColor : DisabledReleaseBorderColor);
            Text releaseLabel = releaseButton.transform.Find("Label")?.GetComponent<Text>();
            if (releaseLabel != null)
            {
                releaseLabel.fontSize = 28;
                releaseLabel.color = canRelease ? TextMain : TextSub;
            }

            Button release = releaseButton.GetComponent<Button>();
            release.interactable = canRelease;
            if (canRelease)
            {
                release.onClick.AddListener(() => ShowReleaseConfirm(overlay, font, monsterName, onReleaseConfirmed));
            }

            CreateText("BottomReleaseHint", parent, font,
                string.IsNullOrEmpty(releaseMessage) ? "逃がしたモンスターは戻せません。" : releaseMessage,
                20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(186f, -1206f), new Vector2(520f, 72f), TextAnchor.MiddleLeft,
                canRelease ? TextWarn : TextSub);
        }

        private static void ShowReleaseConfirm(GameObject overlay, Font font, string monsterName, Func<bool> onReleaseConfirmed)
        {
            if (overlay == null || onReleaseConfirmed == null)
            {
                return;
            }

            Transform existing = overlay.transform.Find("ReleaseConfirm");
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            GameObject confirm = CreateUiObject("ReleaseConfirm", overlay.transform);
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = Vector2.zero;
            confirmRect.anchorMax = Vector2.one;
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;

            Image backdrop = confirm.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.76f);
            backdrop.raycastTarget = true;

            GameObject panel = CreateRawPanel("ConfirmPanel", confirm.transform, null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 360f), new Color(0.05f, 0.06f, 0.08f, 0.98f));
            RawImage panelGraphic = panel.GetComponent<RawImage>();
            panelGraphic.raycastTarget = true;

            CreateText("ConfirmTitle", panel.transform, font, "本当に逃がしますか？", 31, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -62f), new Vector2(520f, 48f), TextAnchor.MiddleCenter, TextWarn);
            CreateText("ConfirmBody", panel.transform, font, $"{monsterName} は所持一覧から消えます。\nこの操作は取り消せません。", 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(580f, 92f), TextAnchor.MiddleCenter, TextMain);

            GameObject cancelButton = CreateActionButton("CancelReleaseButton", panel.transform, font, "キャンセル",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-160f, 42f), new Vector2(250f, 68f), new Color(0.12f, 0.18f, 0.24f, 0.96f));
            cancelButton.GetComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(confirm));

            GameObject confirmButton = CreateActionButton("ConfirmReleaseButton", panel.transform, font, "逃がす",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(160f, 42f), new Vector2(250f, 68f), ReleaseFillColor);
            ApplyButtonFrame(confirmButton, ReleaseBorderColor);
            confirmButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (onReleaseConfirmed.Invoke())
                {
                    UnityEngine.Object.Destroy(overlay);
                }
            });

            confirm.transform.SetAsLastSibling();
        }

        private static void CreatePortrait(Transform parent, MonsterDataSO monsterData, Vector2 anchoredPosition, float size = 250f)
        {
            Sprite portrait = ResolvePortrait(monsterData);
            GameObject shadowObject = CreateUiObject("PortraitShadow", parent);
            RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 1f);
            shadowRect.anchorMax = new Vector2(0.5f, 1f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.anchoredPosition = anchoredPosition + new Vector2(6f, -6f);
            shadowRect.sizeDelta = new Vector2(size, size);
            Image shadow = shadowObject.AddComponent<Image>();
            shadow.sprite = portrait;
            shadow.preserveAspect = true;
            shadow.color = new Color(0f, 0f, 0f, 0.58f);
            shadow.raycastTarget = false;

            GameObject portraitObject = CreateUiObject("Portrait", parent);
            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = anchoredPosition;
            portraitRect.sizeDelta = new Vector2(size, size);
            Image image = portraitObject.AddComponent<Image>();
            image.sprite = portrait;
            image.preserveAspect = true;
            image.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            image.raycastTarget = false;
        }

        private static void CreateIdentityBlock(Transform parent, Font font, OwnedMonsterData monster, MonsterDataSO monsterData, PlayerProfile profile)
        {
            int fusionTotal =
                Mathf.Max(0, monster.FusionBonusHp) +
                Mathf.Max(0, monster.FusionBonusAttack) +
                Mathf.Max(0, monster.FusionBonusWisdom) +
                Mathf.Max(0, monster.FusionBonusDefense) +
                Mathf.Max(0, monster.FusionBonusMagicDefense);

            const float rowX = 210f;
            const float rowTopY = -286f;
            const float rowWidth = 516f;
            const float rowHeight = 46f;
            Vector2 rowSize = new Vector2(rowWidth - 24f, rowHeight);
            CreateIdentityRowsFrame(parent, new Vector2(rowX, rowTopY), new Vector2(rowWidth, rowHeight * 4f), rowHeight);

            CreateText("IdentityRowHeader", parent, font, $"{ResolveRaceName(monsterData.raceId)} / クラス{Mathf.Max(1, monsterData.classRank)} / {ResolveElementName(monsterData.element)}", 27, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(rowX, rowTopY), rowSize, TextAnchor.MiddleLeft, TextGold);

            CreateText("IdentityRowBattleType", parent, font, $"{ResolveRangeName(monsterData.rangeType)} / {ResolveDamageName(monsterData.damageType)} / 攻撃範囲 {monsterData.attackRange:0.##}", 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(rowX, rowTopY - rowHeight), rowSize, TextAnchor.MiddleLeft, TextSub);

            CreateText("IdentityRowTraining", parent, font, $"プラス {monster.TotalPlusValue} / 継承 {fusionTotal} / 所持順 {Mathf.Max(1, monster.AcquiredOrder)}", 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(rowX, rowTopY - rowHeight * 2f), rowSize, TextAnchor.MiddleLeft, TextSub);

            string protection = $"{(monster.IsFavorite ? "お気に入り登録中" : "お気に入り未登録")} / {(monster.IsLocked ? "ロック中" : "未ロック")}";
            CreateText("IdentityRowFavorite", parent, font, protection, 22, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(rowX, rowTopY - rowHeight * 3f), rowSize, TextAnchor.MiddleLeft, monster.IsFavorite || monster.IsLocked ? TextWarn : TextSub);
        }

        private static void CreateIdentityRowsFrame(Transform parent, Vector2 anchoredPosition, Vector2 size, float rowHeight)
        {
            GameObject frame = CreateUiObject("IdentityRowsFrame", parent);
            RectTransform rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            RawImage image = frame.AddComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = TextBackgroundColor;
            image.raycastTarget = false;

            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = TextBackgroundEdgeColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            for (int i = 1; i < 4; i += 1)
            {
                GameObject separator = CreateUiObject("IdentityRowSeparator_" + i, frame.transform);
                RectTransform separatorRect = separator.GetComponent<RectTransform>();
                separatorRect.anchorMin = new Vector2(0f, 1f);
                separatorRect.anchorMax = new Vector2(1f, 1f);
                separatorRect.pivot = new Vector2(0.5f, 0.5f);
                separatorRect.anchoredPosition = new Vector2(0f, -rowHeight * i);
                separatorRect.sizeDelta = new Vector2(0f, 2f);

                Image separatorImage = separator.AddComponent<Image>();
                separatorImage.color = TextBackgroundEdgeColor;
                separatorImage.raycastTarget = false;
            }
        }

        private static void CreateBottomInfoFrame(Transform parent, bool includeReleaseControls)
        {
            const float frameTopY = -1078f;
            const float infoRowHeight = 78f;
            Vector2 frameSize = includeReleaseControls
                ? new Vector2(860f, 178f)
                : new Vector2(860f, infoRowHeight);

            GameObject frame = CreateUiObject("BottomInfoFrame", parent);
            RectTransform rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, frameTopY);
            rect.sizeDelta = frameSize;

            RawImage image = frame.AddComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = TextBackgroundColor;
            image.raycastTarget = false;

            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = TextBackgroundEdgeColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            if (!includeReleaseControls)
            {
                return;
            }

            GameObject separator = CreateUiObject("BottomInfoSeparator", frame.transform);
            RectTransform separatorRect = separator.GetComponent<RectTransform>();
            separatorRect.anchorMin = new Vector2(0f, 1f);
            separatorRect.anchorMax = new Vector2(1f, 1f);
            separatorRect.pivot = new Vector2(0.5f, 0.5f);
            separatorRect.anchoredPosition = new Vector2(0f, -infoRowHeight);
            separatorRect.sizeDelta = new Vector2(0f, 2f);

            Image separatorImage = separator.AddComponent<Image>();
            separatorImage.color = TextBackgroundEdgeColor;
            separatorImage.raycastTarget = false;
        }

        private static void CreateSectionTitle(Transform parent, Font font, string title, float y)
        {
            CreateText("Section_" + title, parent, font, title, 27, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(820f, 40f), TextAnchor.MiddleLeft, TextGold);
        }

        private static void CreateStatRow(Transform parent, Font font, float y, string leftLabel, string leftValue, string rightLabel, string rightValue, bool individualValues = false)
        {
            Texture2D rowTexture = Resources.Load<Texture2D>(StatRowTexturePath);
            CreateRawPanel("StatRow", parent, rowTexture,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(820f, 58f), new Color(0.05f, 0.08f, 0.1f, 0.92f));

            CreateText("LeftLabel", parent, font, leftLabel, 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-235f, y), new Vector2(90f, 32f), TextAnchor.MiddleLeft, TextSub);
            CreateText("LeftValue", parent, font, leftValue, 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-90f, y), new Vector2(140f, 34f), TextAnchor.MiddleRight, individualValues ? ResolveIndividualColor(leftValue) : TextMain);
            CreateText("RightLabel", parent, font, rightLabel, 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(92f, y), new Vector2(130f, 32f), TextAnchor.MiddleLeft, TextSub);
            CreateText("RightValue", parent, font, rightValue, 24, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(203f, y), new Vector2(130f, 34f), TextAnchor.MiddleRight, individualValues ? ResolveIndividualColor(rightValue) : TextMain);
        }

        private static string BuildMetaLine(OwnedMonsterData monster, MonsterDataSO monsterData)
        {
            int level = MonsterLevelService.ClampLevelToMax(monster.Level, monsterData);
            int maxLevel = MonsterLevelService.GetMaxLevel(monsterData);
            if (level >= maxLevel)
            {
                return $"Lv.{level}/{maxLevel} MAX";
            }

            int requiredExp = MonsterLevelService.GetRequiredExpForNextLevel(monster, monsterData);
            return $"Lv.{level}/{maxLevel}  EXP {Mathf.Max(0, monster.Exp)}/{Mathf.Max(1, requiredExp)}";
        }

        private static string BuildBonusAndEquipmentText(PlayerProfile profile, OwnedMonsterData monster)
        {
            string fusion = $"継承: HP+{monster.FusionBonusHp} 攻+{monster.FusionBonusAttack} 魔+{monster.FusionBonusWisdom} 防+{monster.FusionBonusDefense} 魔防+{monster.FusionBonusMagicDefense}";
            if (monster.FusionBonusAttackSpeed > 0f)
            {
                fusion += $" 速+{monster.FusionBonusAttackSpeed:0.##}";
            }

            string equipment = profile != null
                ? $"装備: 武器 {ResolveEquipmentName(profile, monster, EquipmentSlotType.Weapon)} / 防具 {ResolveEquipmentName(profile, monster, EquipmentSlotType.Armor)} / 装飾 {ResolveEquipmentName(profile, monster, EquipmentSlotType.Accessory)}"
                : "装備: -";
            return $"{fusion}\n{equipment}";
        }

        private static string ResolveEquipmentName(PlayerProfile profile, OwnedMonsterData monster, EquipmentSlotType slotType)
        {
            OwnedEquipmentData equipment = profile?.GetMonsterEquippedEquipment(monster.InstanceId, slotType);
            if (equipment == null || string.IsNullOrEmpty(equipment.EquipmentId))
            {
                return "-";
            }

            EquipmentDataSO equipmentData = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
            return equipmentData != null
                ? $"{equipmentData.equipmentName}[{EquipmentEnhancementCatalog.ResolveQualityName(equipmentData, equipment)}]"
                : equipment.EquipmentId;
        }

        private static Sprite ResolvePortrait(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (monsterData.portraitSprite != null)
            {
                return monsterData.portraitSprite;
            }

            string path = !string.IsNullOrEmpty(monsterData.portraitResourcePath)
                ? monsterData.portraitResourcePath
                : monsterData.illustrationResourcePath;
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        private static Color ResolveIndividualColor(string valueText)
        {
            if (!int.TryParse(valueText, out int value))
            {
                return TextMain;
            }

            if (value >= 85)
            {
                return TextGold;
            }

            if (value >= 65)
            {
                return TextGreen;
            }

            return value <= 30 ? TextWarn : TextMain;
        }

        private static string ResolveRaceName(string raceId)
        {
            return raceId switch
            {
                "dragon" => "ドラゴン",
                "robot" => "ロボット",
                "golem" => "ゴーレム",
                "swordsman" => "剣士",
                "mage" => "魔法使い",
                "angel" => "天使",
                "spirit" => "精霊",
                "special" => "特殊",
                _ => string.IsNullOrEmpty(raceId) ? "不明" : raceId
            };
        }

        private static string ResolveElementName(MonsterElement element)
        {
            return element switch
            {
                MonsterElement.Wood => "木",
                MonsterElement.Water => "水",
                MonsterElement.Fire => "火",
                MonsterElement.Light => "光",
                MonsterElement.Dark => "闇",
                _ => "無"
            };
        }

        private static string ResolveRangeName(MonsterRangeType rangeType)
        {
            return rangeType == MonsterRangeType.Ranged ? "遠距離" : "近距離";
        }

        private static string ResolveDamageName(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法型" : "物理型";
        }

        private static GameObject CreateRawPanel(
            string objectName,
            Transform parent,
            Texture2D texture,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color fallbackColor)
        {
            GameObject panel = CreateUiObject(objectName, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            RawImage image = panel.AddComponent<RawImage>();
            image.texture = texture;
            image.color = texture != null ? Color.white : fallbackColor;
            image.raycastTarget = false;
            return panel;
        }

        private static GameObject CreateActionButton(
            string objectName,
            Transform parent,
            Font font,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            CreateText("Label", buttonObject.transform, font, text, 22, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size.x - 24f, size.y - 18f), TextAnchor.MiddleCenter, TextMain);
            return buttonObject;
        }

        private static void CreateCloseButtonMark(Transform parent)
        {
            Vector2 opticalCenterOffset = new Vector2(-2f, 3f);
            Vector2 shadowOffset = opticalCenterOffset + new Vector2(2f, -2f);
            CreateCloseButtonBar("CloseMarkShadowA", parent, new Color(0f, 0f, 0f, 0.54f), 45f, shadowOffset);
            CreateCloseButtonBar("CloseMarkShadowB", parent, new Color(0f, 0f, 0f, 0.54f), -45f, shadowOffset);
            CreateCloseButtonBar("CloseMarkA", parent, TextMain, 45f, opticalCenterOffset);
            CreateCloseButtonBar("CloseMarkB", parent, TextMain, -45f, opticalCenterOffset);
        }

        private static void CreateCloseButtonBar(string objectName, Transform parent, Color color, float rotationZ, Vector2 offset)
        {
            GameObject bar = CreateUiObject(objectName, parent);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(34f, 7f);
            rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);

            Image image = bar.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ApplyButtonFrame(GameObject buttonObject, Color borderColor)
        {
            if (buttonObject == null)
            {
                return;
            }

            Outline outline = buttonObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonObject.AddComponent<Outline>();
            }

            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = false;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.93f, 0.84f, 1f);
            colors.pressedColor = new Color(0.86f, 0.78f, 0.72f, 1f);
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.88f);
            button.colors = colors;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Font font,
            string text,
            int fontSize,
            FontStyle fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment,
            Color color)
        {
            if (ShouldCreateTextBackground(objectName))
            {
                CreateTextBackground(objectName, parent, anchorMin, anchorMax, pivot, anchoredPosition, size);
            }

            GameObject label = CreateUiObject(objectName, parent);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text textComponent = label.AddComponent<Text>();
            textComponent.font = font;
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        private static bool ShouldCreateTextBackground(string objectName)
        {
            if (objectName.StartsWith("IdentityRow", StringComparison.Ordinal))
            {
                return false;
            }

            if (objectName.StartsWith("Bottom", StringComparison.Ordinal))
            {
                return false;
            }

            return objectName != "Label" &&
                   objectName != "CloseLabel" &&
                   objectName != "Title" &&
                   objectName != "SubTitle" &&
                   objectName != "LeftLabel" &&
                   objectName != "LeftValue" &&
                   objectName != "RightLabel" &&
                   objectName != "RightValue";
        }

        private static void CreateTextBackground(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject background = CreateUiObject(objectName + "Background", parent);
            RectTransform rect = background.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size + ResolveTextBackgroundPadding(objectName);

            RawImage image = background.AddComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = TextBackgroundColor;
            image.raycastTarget = false;

            Outline outline = background.AddComponent<Outline>();
            outline.effectColor = TextBackgroundEdgeColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        private static Vector2 ResolveTextBackgroundPadding(string objectName)
        {
            if (objectName == "HeaderBlock")
            {
                return Vector2.zero;
            }

            if (objectName.StartsWith("Section_", StringComparison.Ordinal))
            {
                return new Vector2(24f, 8f);
            }

            if (objectName == "BonusEquipment" || objectName == "ConfirmBody")
            {
                return new Vector2(28f, 10f);
            }

            if (objectName == "ReleaseHint" || objectName == "ConfirmTitle")
            {
                return new Vector2(24f, 8f);
            }

            return new Vector2(14f, 6f);
        }

        private static Font GetRuntimeFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    return font;
                }
            }
            catch (Exception)
            {
                // Unity versions differ in builtin font names.
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }
    }
}
