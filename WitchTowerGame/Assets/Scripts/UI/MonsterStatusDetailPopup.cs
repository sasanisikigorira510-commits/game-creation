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

        public static void Show(Transform parent, PlayerProfile profile, OwnedMonsterData monster, MonsterDataSO monsterData)
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
                Vector2.zero, new Vector2(930f, 1060f), Color.white);
            RawImage panelGraphic = panel.GetComponent<RawImage>();
            panelGraphic.raycastTarget = true;
            Button panelBlocker = panel.AddComponent<Button>();
            panelBlocker.targetGraphic = panelGraphic;

            CreateText("Title", panel.transform, font, monsterData.monsterName, 38, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(620f, 58f), TextAnchor.MiddleCenter, TextMain);

            CreateText("SubTitle", panel.transform, font, BuildMetaLine(monster, monsterData), 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -166f), new Vector2(690f, 34f), TextAnchor.MiddleCenter, TextSub);

            GameObject closeButton = CreateRawPanel("CloseButton", panel.transform, closeTexture,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-116f, -103f), new Vector2(92f, 84f), Color.white);
            Button close = closeButton.AddComponent<Button>();
            RawImage closeGraphic = closeButton.GetComponent<RawImage>();
            closeGraphic.raycastTarget = true;
            close.targetGraphic = closeGraphic;
            close.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
            CreateText("CloseLabel", closeButton.transform, font, "×", 33, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70f, 54f), TextAnchor.MiddleCenter, TextMain);

            CreatePortrait(panel.transform, monsterData, new Vector2(-265f, -300f));
            CreateIdentityBlock(panel.transform, font, monster, monsterData, profile);

            BattleUnitStats stats = MonsterBattleStatsFactory.Create(profile, monster, monsterData);
            CreateSectionTitle(panel.transform, font, "戦闘ステータス", -438f);
            CreateStatRow(panel.transform, font, -490f, "HP", stats != null ? stats.MaxHp.ToString() : "-", "攻撃", stats != null ? stats.Attack.ToString() : "-");
            CreateStatRow(panel.transform, font, -546f, "魔攻", stats != null ? stats.Wisdom.ToString() : "-", "防御", stats != null ? stats.Defense.ToString() : "-");
            CreateStatRow(panel.transform, font, -602f, "魔防", stats != null ? stats.MagicDefense.ToString() : "-", "攻速", stats != null ? stats.AttackSpeed.ToString("0.##") : "-");
            CreateStatRow(panel.transform, font, -658f, "会心率", stats != null ? $"{stats.CritRate * 100f:0.#}%" : "-", "会心倍率", stats != null ? $"{stats.CritDamage:0.##}x" : "-");

            MonsterIndividualValueService.EnsureInitialized(monster);
            CreateSectionTitle(panel.transform, font, $"個体値  平均 {MonsterIndividualValueService.GetAverage(monster)}", -724f);
            CreateStatRow(panel.transform, font, -776f, "HP", monster.IndividualHp.ToString(), "攻撃", monster.IndividualAttack.ToString(), true);
            CreateStatRow(panel.transform, font, -832f, "魔攻", monster.IndividualWisdom.ToString(), "防御", monster.IndividualDefense.ToString(), true);
            CreateStatRow(panel.transform, font, -888f, "魔防", monster.IndividualMagicDefense.ToString(), "攻速", monster.IndividualAttackSpeed.ToString(), true);

            CreateText("BonusEquipment", panel.transform, font, BuildBonusAndEquipmentText(profile, monster), 18, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -970f), new Vector2(680f, 56f), TextAnchor.MiddleCenter, TextSub);

            overlay.transform.SetAsLastSibling();
        }

        private static void CreatePortrait(Transform parent, MonsterDataSO monsterData, Vector2 anchoredPosition)
        {
            Sprite portrait = ResolvePortrait(monsterData);
            GameObject shadowObject = CreateUiObject("PortraitShadow", parent);
            RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 1f);
            shadowRect.anchorMax = new Vector2(0.5f, 1f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.anchoredPosition = anchoredPosition + new Vector2(4f, -4f);
            shadowRect.sizeDelta = new Vector2(250f, 250f);
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
            portraitRect.sizeDelta = new Vector2(250f, 250f);
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

            CreateText("IdentityHeader", parent, font, $"{ResolveRaceName(monsterData.raceId)} / クラス{Mathf.Max(1, monsterData.classRank)} / {ResolveElementName(monsterData.element)}", 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(180f, -248f), new Vector2(410f, 42f), TextAnchor.MiddleLeft, TextGold);

            CreateText("BattleType", parent, font, $"{ResolveRangeName(monsterData.rangeType)} / {ResolveDamageName(monsterData.damageType)} / 攻撃範囲 {monsterData.attackRange:0.##}", 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(180f, -292f), new Vector2(410f, 34f), TextAnchor.MiddleLeft, TextSub);

            CreateText("Training", parent, font, $"プラス {monster.TotalPlusValue} / 継承 {fusionTotal} / 所持順 {Mathf.Max(1, monster.AcquiredOrder)}", 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(180f, -332f), new Vector2(410f, 34f), TextAnchor.MiddleLeft, TextSub);

            string favorite = monster.IsFavorite ? "お気に入り登録中" : "お気に入り未登録";
            CreateText("Favorite", parent, font, favorite, 19, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(180f, -372f), new Vector2(410f, 34f), TextAnchor.MiddleLeft, monster.IsFavorite ? TextWarn : TextSub);
        }

        private static void CreateSectionTitle(Transform parent, Font font, string title, float y)
        {
            CreateText("Section_" + title, parent, font, title, 23, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(680f, 32f), TextAnchor.MiddleLeft, TextGold);
        }

        private static void CreateStatRow(Transform parent, Font font, float y, string leftLabel, string leftValue, string rightLabel, string rightValue, bool individualValues = false)
        {
            Texture2D rowTexture = Resources.Load<Texture2D>(StatRowTexturePath);
            CreateRawPanel("StatRow", parent, rowTexture,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(690f, 50f), new Color(0.05f, 0.08f, 0.1f, 0.92f));

            CreateText("LeftLabel", parent, font, leftLabel, 19, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-255f, y), new Vector2(108f, 28f), TextAnchor.MiddleLeft, TextSub);
            CreateText("LeftValue", parent, font, leftValue, 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-115f, y), new Vector2(135f, 30f), TextAnchor.MiddleRight, individualValues ? ResolveIndividualColor(leftValue) : TextMain);
            CreateText("RightLabel", parent, font, rightLabel, 19, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(88f, y), new Vector2(108f, 28f), TextAnchor.MiddleLeft, TextSub);
            CreateText("RightValue", parent, font, rightValue, 21, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(230f, y), new Vector2(135f, 30f), TextAnchor.MiddleRight, individualValues ? ResolveIndividualColor(rightValue) : TextMain);
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
            return equipmentData != null ? equipmentData.equipmentName : equipment.EquipmentId;
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
            return damageType == MonsterDamageType.Magic ? "魔法攻撃" : "物理攻撃";
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
