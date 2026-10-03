using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private Text guardianBattleLabel;
        private Text guardianResonanceLabel;
        private Text guardianTrialCueLabel;
        private Image guardianResonanceFill;
        private bool guardianResultGranted;
        private bool guardianResultMetOathCondition;

        private void ShowGuardianTrialResult(bool won)
        {
            // Both simulation completion and UI callbacks can arrive during the
            // same frame. Once a result exists, its grant and save are final.
            if (hasLastResultViewData) return;
            StopAutoRepeatSameFloor();
            ClearBattleAnnouncements();
            ClearBossEntranceFlash();
            var profile = GameManager.Instance.PlayerProfile;
            var simulator = stateMachine?.Simulator;
            guardianResultGranted = false;
            guardianResultMetOathCondition = !GuardianTrialSession.IsOath;
            if (GuardianTrialSession.IsOath && won && simulator != null)
            {
                for (int slot = 0; slot < PlayerProfile.PartySlotCount; slot++)
                    if (simulator.IsAllyAlive(slot)) { guardianResultMetOathCondition = true; break; }
            }
            bool succeeded = won && guardianResultMetOathCondition;
            if (GuardianTrialSession.IsOath && succeeded)
                guardianResultGranted = GuardianTrialSession.CompleteOath(profile, true);
            else if (!GuardianTrialSession.IsPractice && !GuardianTrialSession.IsOath && won)
                guardianResultGranted = GuardianTrialSession.Win(profile);
            // The practice has no transaction. A failed/repeated oath likewise
            // needs no write; only a first real reward is persisted here.
            if (guardianResultGranted) SaveManager.Instance?.SaveCurrentGame();
            string name = GuardianService.Find(GuardianTrialSession.GuardianId).DisplayName;
            string reward = !guardianResultGranted ? string.Empty : GuardianTrialSession.IsOath
                ? GuardianService.OathTitle(GuardianTrialSession.GuardianId) : name + "の神核 ×1";
            var result = new BattleResultViewData(GuardianTrialSession.IsPractice || succeeded,
                0, 0, 0, 0, profile.Level, profile.Level, currentFloor,
                GameManager.Instance.CurrentFloor, reward, string.Empty);
            resultHandled = true;
            lastBattleWon = result.IsWin;
            lastResultViewData = result;
            hasLastResultViewData = true;
            stateMachine.ShowResultPanel(result);
            ShowMinimalResultOverlay(result);
        }

        private void ApplyGuardianTrialResultPresentation(BattleResultViewData viewData)
        {
            if (!GuardianTrialSession.IsActive) return;
            string id = GuardianTrialSession.GuardianId;
            string name = GuardianService.Find(id).DisplayName;
            string title, summary, reward;
            if (GuardianTrialSession.IsPractice)
            {
                title = "共鳴体験 終了";
                summary = name + "  ·  " + GuardianService.ContractLabel(id, GuardianTrialSession.ContractId) +
                    "\n聖域で、ほかの神獣や契約型も試せます。";
                var simulator = stateMachine?.Simulator;
                reward = $"神技 {simulator?.GuardianSkillCount ?? 0}回\n" +
                    $"{simulator?.GuardianContributionLabel ?? "共鳴への貢献"}  {simulator?.GuardianContributionValue ?? 0}\n" +
                    "体験による消費・報酬はありません。";
            }
            else if (GuardianTrialSession.IsOath)
            {
                title = viewData.IsWin ? "誓約達成" : "誓約への再挑戦";
                summary = viewData.IsWin ? name + "と仲間が、ともに試練を越えました。" :
                    guardianResultMetOathCondition ? "編成と契約型を見直し、もう一度挑めます。" :
                    "通常の仲間が1体以上生存した状態で、\n神獣とともに試練を突破しましょう。";
                reward = guardianResultGranted ?
                    "称号「" + GuardianService.OathTitle(id) + "」\n聖域に称号が表示されます。" :
                    viewData.IsWin ? "この誓約は達成済みです。\n再戦による追加報酬はありません。" :
                    "挑戦による消費はありません。";
            }
            else
            {
                title = viewData.IsWin ? "試練突破" : "試練失敗";
                summary = viewData.IsWin ? name + "の試練を突破！\n聖域で神核から神獣を迎えましょう。" :
                    "挑戦による消費はありません。\n編成を整えて、聖域から再挑戦できます。";
                reward = guardianResultGranted ? name + "の神核 ×1 獲得" :
                    viewData.IsWin ? "この神核は獲得済みです。" : "神核は試練突破時に獲得できます。";
            }
            if (minimalResultTitleText != null) minimalResultTitleText.text = title;
            if (minimalResultSummaryText != null) minimalResultSummaryText.text = summary;
            if (minimalResultRewardText != null) minimalResultRewardText.text = reward;
            if (minimalResultHomeButtonText != null) minimalResultHomeButtonText.text = "神獣の聖域へ戻る";
            ApplyGuardianResultLayout(id);
        }

        private void ApplyGuardianResultLayout(string id)
        {
            var card = minimalResultOverlayRoot != null ? minimalResultOverlayRoot.transform.Find("ResultCard") as RectTransform : null;
            if (card == null) return;
            card.sizeDelta = new Vector2(960, 1120);
            var veil = minimalResultOverlayRoot.GetComponent<Image>();
            if (veil != null) veil.color = new Color(.008f, .014f, .03f, .91f);
            var frame = card.GetComponent<Image>();
            frame.sprite = Resources.Load<Sprite>("UI/GuardiansReborn/PanelFrame");
            frame.type = Image.Type.Sliced; frame.preserveAspect = false;
            frame.pixelsPerUnitMultiplier = 1.5f; frame.color = Color.white;
            ConfigureMinimalResultTextRect(minimalResultTitleText, new Vector2(.09f, .86f), new Vector2(.91f, .95f), 46);
            ConfigureMinimalResultTextRect(minimalResultSummaryText, new Vector2(.09f, .43f), new Vector2(.91f, .55f), 31);
            ConfigureMinimalResultTextRect(minimalResultRewardText, new Vector2(.09f, .22f), new Vector2(.91f, .40f), 29);
            foreach (var text in new[] { minimalResultTitleText, minimalResultSummaryText, minimalResultRewardText, minimalResultHomeButtonText })
            {
                if (text == null) continue;
                text.fontStyle = FontStyle.Normal;
                foreach (var outline in text.GetComponents<Outline>()) outline.enabled = false;
            }
            if (minimalResultTitleText != null) minimalResultTitleText.color = new Color(.94f, .80f, .48f);
            var portrait = card.Find("GuardianResultPortrait")?.GetComponent<Image>();
            if (portrait == null)
            {
                var node = new GameObject("GuardianResultPortrait", typeof(RectTransform), typeof(Image));
                node.transform.SetParent(card, false);
                portrait = node.GetComponent<Image>();
            }
            string asset = id == "seiryu" ? "SeiryuPortrait" : id == "suzaku" ? "SuzakuPortrait" : id == "byakko" ? "ByakkoPortrait" : "GenbuPortrait";
            portrait.sprite = Resources.Load<Sprite>("UI/GuardiansReborn/" + asset);
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            portrait.rectTransform.anchorMin = new Vector2(.21f, .56f);
            portrait.rectTransform.anchorMax = new Vector2(.79f, .84f);
            portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
            if (minimalResultHomeButton != null)
            {
                var image = minimalResultHomeButton.GetComponent<Image>();
                image.sprite = Resources.Load<Sprite>("UI/GuardiansReborn/PrimaryButton");
                image.type = Image.Type.Sliced; image.preserveAspect = false;
                image.pixelsPerUnitMultiplier = 3.2f; image.color = Color.white;
                var colors = minimalResultHomeButton.colors;
                colors.normalColor = colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(.80f, .88f, .97f);
                minimalResultHomeButton.colors = colors;
                if (minimalResultHomeButtonText != null)
                {
                    minimalResultHomeButtonText.alignment = TextAnchor.MiddleCenter;
                    var labelRect = minimalResultHomeButtonText.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    // The frame's lower jewel sits below its visual text area.
                    labelRect.offsetMin = new Vector2(18f, 10f);
                    labelRect.offsetMax = new Vector2(-18f, 10f);
                }
            }
        }

        private void UpdateGuardianBattlePanel()
        {
            if (skillPanelRoot == null) return;
            if (spiritGaugeRoot != null) spiritGaugeRoot.SetActive(false);
            foreach (var definition in BattleSpiritCatalog.GetActiveDefinitions())
            {
                var old = skillPanelRoot.transform.Find(BuildSpiritCommandButtonName(definition));
                if (old != null) old.gameObject.SetActive(false);
            }
            if (guardianBattleLabel == null)
            {
                guardianBattleLabel = GuardianPanelText("GuardianCompanionStatus", new Vector2(.05f, .72f), new Vector2(.95f, .92f), 28, new Color(1, .91f, .66f));
                guardianResonanceLabel = GuardianPanelText("GuardianResonanceStatus", new Vector2(.05f, .31f), new Vector2(.95f, .62f), 27, new Color(.75f, .92f, 1f));
                guardianTrialCueLabel = GuardianPanelText("GuardianTrialCue", new Vector2(.05f, .07f), new Vector2(.95f, .35f), 25, new Color(.86f, .86f, .92f));
                var gauge = new GameObject("GuardianResonanceGauge", typeof(RectTransform), typeof(Image));
                gauge.transform.SetParent(skillPanelRoot.transform, false);
                var gaugeRect = (RectTransform)gauge.transform;
                gaugeRect.anchorMin = new Vector2(.10f,.66f); gaugeRect.anchorMax = new Vector2(.90f,.70f);
                gaugeRect.offsetMin = gaugeRect.offsetMax = Vector2.zero;
                var track = gauge.GetComponent<Image>();
                track.sprite = Resources.Load<Sprite>("UI/GuardiansReborn/ResonanceFill");
                track.color = new Color(.24f,.29f,.36f); track.raycastTarget = false;
                var fill = new GameObject("GuardianResonanceFill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(gauge.transform,false);
                var fillRect = (RectTransform)fill.transform;
                fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                guardianResonanceFill = fill.GetComponent<Image>();
                guardianResonanceFill.sprite = track.sprite; guardianResonanceFill.type = Image.Type.Filled;
                guardianResonanceFill.fillMethod = Image.FillMethod.Horizontal;
                guardianResonanceFill.raycastTarget = false;
                var frame = skillPanelRoot.GetComponent<Image>();
                var sprite = Resources.Load<Sprite>("UI/GuardiansReborn/PanelFrame");
                if (frame != null && sprite != null)
                {
                    frame.sprite = sprite; frame.color = Color.white; frame.type = Image.Type.Sliced;
                    frame.preserveAspect = false; frame.pixelsPerUnitMultiplier = 2.4f;
                }
            }
            var profile = GameManager.Instance?.PlayerProfile;
            var guardian = GuardianService.Equipped(profile);
            var simulator = stateMachine?.Simulator;
            bool practice = GuardianTrialSession.IsPractice;
            string id = practice ? GuardianTrialSession.GuardianId : guardian?.Id;
            if (guardianResonanceFill != null)
            {
                guardianResonanceFill.fillAmount = simulator?.GuardianResonanceNormalized ?? 0f;
                guardianResonanceFill.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(id));
            }
            string trialCue = simulator?.GuardianTrialCue ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                guardianBattleLabel.text = GuardianTrialSession.IsActive
                    ? GuardianService.Find(GuardianTrialSession.GuardianId).DisplayName + "の試練"
                    : GuardianService.IsUnlocked(profile) ? "神獣専用枠：未編成" : "神獣専用枠";
                guardianResonanceLabel.text = GuardianTrialSession.IsActive ? "突破すると神核を獲得" :
                    GuardianService.IsUnlocked(profile) ? "聖域で神獣を編成すると、仲間の行動に応えます" : GuardianService.UnlockRequirementLabel;
                guardianTrialCueLabel.text = trialCue;
                return;
            }
            string name = GuardianService.Find(id).DisplayName;
            int level = practice ? GuardianTrialSession.LevelOverride : GuardianService.Level(profile, id);
            string contract = practice ? GuardianTrialSession.ContractId : GuardianService.ContractId(profile, id);
            string state = simulator == null ? "共鳴の準備中" : !simulator.IsAllyAlive(GuardianService.BattleSlot) ? "戦闘不能" :
                $"HP {simulator.GetAllyCurrentHp(GuardianService.BattleSlot)}/{simulator.GetAllyMaxHp(GuardianService.BattleSlot)}";
            guardianBattleLabel.text = $"{name} Lv.{level}〈{GuardianService.ContractLabel(id, contract)}〉  {state}";
            int resonance = Mathf.Clamp(Mathf.FloorToInt(simulator?.GuardianResonance ?? 0), 0, 100);
            guardianResonanceLabel.text = $"共鳴 {resonance}%  ·  神技 {simulator?.GuardianSkillCount ?? 0}回\n" +
                $"{simulator?.GuardianContributionLabel ?? "仲間への支援"}  {simulator?.GuardianContributionValue ?? 0}";
            guardianTrialCueLabel.text = practice ? $"共鳴体験  残り {simulator?.GuardianTrialRemaining ?? 15f:0.0} 秒  ·  消費・報酬なし" :
                !string.IsNullOrEmpty(trialCue) ? trialCue : resultHandled ? "仲間と重ねた共鳴の記録" :
                $"{GuardianService.Skill(id)}  ·  共鳴が満ちると自動発動";
        }

        private Text GuardianPanelText(string name, Vector2 min, Vector2 max, int fontSize, Color color)
        {
            var node = skillPanelRoot.transform.Find(name)?.gameObject;
            if (node == null)
            {
                node = new GameObject(name, typeof(RectTransform), typeof(Text));
                node.transform.SetParent(skillPanelRoot.transform, false);
            }
            var text = node.GetComponent<Text>();
            text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = ResolveBuiltinUiFont(); text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal; text.alignment = TextAnchor.MiddleCenter;
            text.color = color; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 22; text.resizeTextMaxSize = fontSize;
            return text;
        }

        private void HandleGuardianSkill(string skillName)
        {
            EnqueueBattleAnnouncement(skillName, BattleAnnouncementTone.Gold);
            PlayGuardianDivinePresentation();
        }
    }
}
