using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;

namespace WitchTower.Core
{
    public sealed partial class TitleSceneController
    {
        private int equipmentBulkSaleMaxQuality = 5;
        private bool equipmentBulkSaleExcludeFavorites = true;
        private bool equipmentBulkSaleUnenhancedOnly;
        private List<string> equipmentBulkSaleCandidateIds = new List<string>();
        private readonly List<Button> equipmentBulkSaleFilterButtons = new List<Button>();
        private Button equipmentBulkSaleClearButton;

        private void ToggleSelectedEquipmentFavorite()
        {
            var profile = GameManager.Instance?.PlayerProfile;
            var equipment = profile?.GetOwnedEquipmentByInstanceId(selectedEquipmentDetailInstanceId);
            if (equipment == null) return;
            equipment.IsFavorite = !equipment.IsFavorite;
            if (Application.isPlaying && (SaveManager.Instance == null || !SaveManager.Instance.TrySaveWithReason(
                profile.ToSaveData(GameManager.Instance.CurrentFloor), "equipment_favorite", out _)))
            {
                equipment.IsFavorite = !equipment.IsFavorite;
                equipmentLastActionMessage = "保存できなかったため、お気に入りを変更しませんでした。";
            }
            else equipmentLastActionMessage = equipment.IsFavorite ? "お気に入りに登録しました。" : "お気に入りを解除しました。";
            RefreshEquipmentScene();
            ShowEquipmentDetailSheet(equipment.InstanceId);
        }

        private void OpenEquipmentBulkSale()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (profile == null || equipmentSceneRoot == null) return;
            if (GetEquipmentTutorialEvent(profile) != null)
            {
                equipmentLastActionMessage = "先に装備のチュートリアルを確認しましょう。";
                RefreshEquipmentScene();
                return;
            }
            CloseEquipmentMonsterPicker();
            CloseEquipmentDetailSheet();
            // Keep the inventory category for this visit; filters never retain hidden selections.
            equipmentBulkSaleSelection.Clear();
            equipmentBulkSalePreview = null;
            equipmentBulkSaleMaxQuality = 5;
            equipmentBulkSaleExcludeFavorites = true;
            equipmentBulkSaleUnenhancedOnly = false;
            equipmentBulkSaleCandidateIds = BuildEquipmentInventoryDisplayEquipments(profile)
                .Where(x => x != null && !string.IsNullOrEmpty(x.InstanceId))
                .Select(x => x.InstanceId).Distinct(StringComparer.Ordinal).ToList();
            if (equipmentBulkSaleRoot == null) BuildEquipmentBulkSaleOverlay(ResolveRuntimeFont());
            equipmentBulkSaleRoot.SetActive(true);
            equipmentBulkSaleRoot.transform.SetAsLastSibling();
            equipmentBulkSaleMessage.text = "条件ボタンを押すと絞り込みを変更できます。\nロック中・装備中・チュートリアル用は売却不可。\nお気に入りは装備の詳細画面で登録できます。";
            RefreshEquipmentBulkSale();
        }

        private void BuildEquipmentBulkSaleOverlay(Font font)
        {
            equipmentBulkSaleRoot = CreateUiObject("EquipmentBulkSaleOverlay", equipmentSceneRoot.transform);
            RectTransform root = equipmentBulkSaleRoot.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Image shade = equipmentBulkSaleRoot.AddComponent<Image>();
            shade.color = new Color(0.01f, 0.02f, 0.03f, 0.82f);
            Button backdrop = equipmentBulkSaleRoot.AddComponent<Button>();
            backdrop.targetGraphic = shade;
            backdrop.onClick.AddListener(CancelEquipmentBulkSale);

            GameObject panel = CreateUiObject("EquipmentBulkSalePanel", equipmentBulkSaleRoot.transform);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1000f, 1520f);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.09f, 0.12f, 0.99f);
            panel.AddComponent<Button>().targetGraphic = panelImage;
            CreateText("Header", panel.transform, font, "装備の一括売却", 36, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.88f, 0.55f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(700f, 52f));
            equipmentBulkSaleSummary = CreateText("SelectionSummary", panel.transform, font, string.Empty, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(920f, 52f));
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                Button button = CreateActionButton(panel.transform, font, string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2((i - 1) * 312f, -160f), new Vector2(296f, 108f),
                    new Color(0.18f, 0.28f, 0.38f, 0.96f), () => CycleEquipmentBulkSaleFilter(filter), 27);
                button.gameObject.name = "BulkSaleFilter" + i;
                ApplyEquipmentActionButtonFrame(button, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));
                equipmentBulkSaleFilterButtons.Add(button);
            }
            equipmentBulkSaleSelectVisibleButton = CreateActionButton(panel.transform, font, "表示中を全選択", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-236f, -286f), new Vector2(448f, 96f), new Color(0.18f, 0.28f, 0.38f, 0.96f), SelectVisibleEquipmentForBulkSale, 28);
            ApplyEquipmentActionButtonFrame(equipmentBulkSaleSelectVisibleButton, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));
            equipmentBulkSaleClearButton = CreateActionButton(panel.transform, font, "選択を解除", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(236f, -286f), new Vector2(448f, 96f), new Color(0.18f, 0.28f, 0.38f, 0.96f), ClearEquipmentBulkSaleSelection, 28);
            ApplyEquipmentActionButtonFrame(equipmentBulkSaleClearButton, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));

            GameObject viewportObject = CreateUiObject("BulkSaleViewport", panel.transform);
            RectTransform viewport = viewportObject.AddComponent<RectTransform>();
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = new Vector2(0f, -408f);
            viewport.sizeDelta = new Vector2(920f, 714f);
            viewportObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();
            GameObject content = CreateUiObject("BulkSaleList", viewport);
            equipmentBulkSaleList = content.AddComponent<RectTransform>();
            equipmentBulkSaleList.anchorMin = equipmentBulkSaleList.anchorMax = equipmentBulkSaleList.pivot = new Vector2(0f, 1f);
            equipmentBulkSaleList.sizeDelta = new Vector2(920f, 714f);
            ScrollRect scroll = viewportObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = equipmentBulkSaleList;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42f;

            equipmentBulkSaleMessage = CreateText("ReviewMessage", panel.transform, font, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.94f, 0.88f, 0.72f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(920f, 180f));
            equipmentBulkSaleMessage.verticalOverflow = VerticalWrapMode.Truncate;
            equipmentBulkSaleConfirmButton = CreateActionButton(panel.transform, font, "選択した装備を確認", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(40f, 28f), new Vector2(448f, 124f), new Color(0.42f, 0.20f, 0.17f, 0.96f), ReviewOrCommitEquipmentBulkSale, 30);
            equipmentBulkSaleConfirmLabel = equipmentBulkSaleConfirmButton.GetComponentInChildren<Text>();
            ApplyEquipmentActionButtonFrame(equipmentBulkSaleConfirmButton, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));
            Button cancel = CreateActionButton(panel.transform, font, "キャンセル", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-40f, 28f), new Vector2(448f, 124f), new Color(0.18f, 0.28f, 0.38f, 0.96f), CancelEquipmentBulkSale, 30);
            cancel.gameObject.name = "EquipmentBulkSaleCancel";
            ApplyEquipmentActionButtonFrame(cancel, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));
            equipmentBulkSaleRoot.SetActive(false);
        }

        private void RefreshEquipmentBulkSale()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (equipmentBulkSaleList == null || profile == null) return;
            equipmentBulkSaleVisibleIds = equipmentBulkSaleCandidateIds.Where(id => EquipmentBulkSaleService.MatchesFilter(
                profile.GetOwnedEquipmentByInstanceId(id), equipmentBulkSaleMaxQuality, equipmentBulkSaleExcludeFavorites, equipmentBulkSaleUnenhancedOnly)).ToList();
            equipmentBulkSaleSelection.RemoveWhere(id => !equipmentBulkSaleVisibleIds.Contains(id) || !EquipmentBulkSaleService.CanSell(profile, profile.GetOwnedEquipmentByInstanceId(id), out _));
            ClearChildren(equipmentBulkSaleList);
            equipmentBulkSaleList.sizeDelta = new Vector2(920f, Mathf.Max(714f, equipmentBulkSaleVisibleIds.Count * 174f));
            Font font = ResolveRuntimeFont();
            for (int i = 0; i < equipmentBulkSaleVisibleIds.Count; i++)
            {
                string id = equipmentBulkSaleVisibleIds[i];
                OwnedEquipmentData equipment = profile.GetOwnedEquipmentByInstanceId(id);
                if (equipment == null) continue;
                bool selectable = EquipmentBulkSaleService.CanSell(profile, equipment, out string reason);
                bool selected = equipmentBulkSaleSelection.Contains(id);
                var data = MasterDataManager.Instance?.GetEquipmentData(equipment.EquipmentId);
                string name = data != null ? data.equipmentName : equipment.EquipmentId;
                string quality = EquipmentEnhancementCatalog.ResolveQualityName(data, equipment);
                string label = (selected ? "✓ 選択中  " : "未選択  ") + name + " [" + quality + "]" + (equipment.IsFavorite ? " お気に入り" : "") + "\n" +
                    "強化 +" + equipment.UpgradeLevel + " / 残り " + equipment.RemainingEnhanceAttempts + " 回\n" +
                    (selectable ? EquipmentBulkSaleService.GetSellGold(equipment).ToString("N0") + " G" : "売却不可：" + reason);
                Button row = CreateActionButton(equipmentBulkSaleList, font, label, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, -i * 174f), new Vector2(920f, 156f),
                    selected ? new Color(0.16f, 0.35f, 0.24f, 0.98f) : new Color(0.12f, 0.17f, 0.22f, 0.98f), () => ToggleEquipmentBulkSaleSelection(id), 28);
                row.gameObject.name = "BulkSaleItem_" + i;
                row.interactable = selectable && equipmentBulkSalePreview == null;
                ApplyEquipmentActionButtonFrame(row, new Color(1f, 0.82f, 0.34f, 0.88f), new Color(0.16f, 0.16f, 0.12f, 0.82f));
            }
            long gold = equipmentBulkSaleSelection.Sum(id => (long)EquipmentBulkSaleService.GetSellGold(profile.GetOwnedEquipmentByInstanceId(id)));
            equipmentBulkSaleSummary.text = equipmentBulkSaleSelection.Count + "個 選択 / 合計 " + gold.ToString("N0") + " G";
            equipmentBulkSaleConfirmLabel.text = equipmentBulkSalePreview == null ? "選択した装備を確認" : "確認した装備を売却する";
            equipmentBulkSaleConfirmButton.interactable = equipmentBulkSaleSelection.Count > 0;
            equipmentBulkSaleSelectVisibleButton.interactable = equipmentBulkSalePreview == null;
            equipmentBulkSaleClearButton.interactable = equipmentBulkSalePreview == null && equipmentBulkSaleSelection.Count > 0;
            string[] labels = { "品質上限\n" + (equipmentBulkSaleMaxQuality == 5 ? "すべて" : EquipmentEnhancementCatalog.ResolveQualityName((EquipmentRarity)(equipmentBulkSaleMaxQuality - 1)) + "以下"),
                "お気に入り\n" + (equipmentBulkSaleExcludeFavorites ? "除外する" : "含める"),
                "強化状態\n" + (equipmentBulkSaleUnenhancedOnly ? "未強化のみ" : "すべて") };
            for (int i = 0; i < equipmentBulkSaleFilterButtons.Count; i++)
            {
                equipmentBulkSaleFilterButtons[i].GetComponentInChildren<Text>().text = labels[i];
                equipmentBulkSaleFilterButtons[i].interactable = equipmentBulkSalePreview == null;
            }
            if (equipmentBulkSaleVisibleIds.Count == 0)
                CreateText("BulkSaleEmpty", equipmentBulkSaleList, font, "条件に合う装備はありません", 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                    Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(860f, 64f));
        }

        private void CycleEquipmentBulkSaleFilter(int filter)
        {
            if (equipmentBulkSalePreview != null) return;
            if (filter == 0) equipmentBulkSaleMaxQuality = equipmentBulkSaleMaxQuality % 5 + 1;
            else if (filter == 1) equipmentBulkSaleExcludeFavorites = !equipmentBulkSaleExcludeFavorites;
            else if (filter == 2) equipmentBulkSaleUnenhancedOnly = !equipmentBulkSaleUnenhancedOnly;
            else return;
            equipmentBulkSaleSelection.Clear();
            equipmentBulkSaleList.anchoredPosition = Vector2.zero;
            equipmentBulkSaleMessage.text = "条件を変更したため選択を解除しました。\n表示中の装備を選択してください。";
            RefreshEquipmentBulkSale();
        }

        private void ClearEquipmentBulkSaleSelection()
        {
            if (equipmentBulkSalePreview != null) return;
            equipmentBulkSaleSelection.Clear();
            RefreshEquipmentBulkSale();
        }

        private void ToggleEquipmentBulkSaleSelection(string id)
        {
            if (equipmentBulkSalePreview != null || !equipmentBulkSaleVisibleIds.Contains(id)) return;
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (!EquipmentBulkSaleService.CanSell(profile, profile?.GetOwnedEquipmentByInstanceId(id), out _)) return;
            if (!equipmentBulkSaleSelection.Remove(id)) equipmentBulkSaleSelection.Add(id);
            RefreshEquipmentBulkSale();
        }

        private void SelectVisibleEquipmentForBulkSale()
        {
            if (equipmentBulkSalePreview != null) return;
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            foreach (string id in equipmentBulkSaleVisibleIds)
                if (EquipmentBulkSaleService.CanSell(profile, profile?.GetOwnedEquipmentByInstanceId(id), out _)) equipmentBulkSaleSelection.Add(id);
            RefreshEquipmentBulkSale();
        }

        private void ReviewOrCommitEquipmentBulkSale()
        {
            PlayerProfile profile = GameManager.Instance?.PlayerProfile;
            if (equipmentBulkSalePreview == null)
            {
                EquipmentBulkSaleService.TryCreatePreview(profile, equipmentBulkSaleSelection.OrderBy(id => id, StringComparer.Ordinal), out equipmentBulkSalePreview, out string message);
                equipmentBulkSaleMessage.text = message;
                if (equipmentBulkSalePreview != null && equipmentBulkSalePreview.InstanceIds.Any(id => profile.GetOwnedEquipmentByInstanceId(id)?.IsFavorite == true))
                    equipmentBulkSaleMessage.text += "\nお気に入りの装備が含まれています。";
                RefreshEquipmentBulkSale();
                return;
            }
            bool sold = EquipmentBulkSaleService.TryCommitWithPersistence(profile, equipmentBulkSalePreview,
                () => !Application.isPlaying || (SaveManager.Instance != null && SaveManager.Instance.TrySaveWithReason(
                    profile.ToSaveData(GameManager.Instance.CurrentFloor), "equipment_bulk_sell", out _)), out string result);
            equipmentBulkSalePreview = null;
            if (sold)
            {
                CancelEquipmentBulkSale();
                equipmentLastActionMessage = result;
                RefreshEquipmentScene();
            }
            else
            {
                equipmentBulkSaleMessage.text = result;
                RefreshEquipmentBulkSale();
            }
        }

        private void CancelEquipmentBulkSale()
        {
            equipmentBulkSalePreview = null;
            equipmentBulkSaleSelection.Clear();
            equipmentBulkSaleVisibleIds.Clear();
            equipmentBulkSaleCandidateIds.Clear();
            if (equipmentBulkSaleRoot != null) equipmentBulkSaleRoot.SetActive(false);
        }
    }
}
