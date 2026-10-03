using System.Collections;
using UnityEngine;
using WitchTower.Core;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.Save;
using WitchTower.UI;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private bool dailyTransactionBusy;
        private bool dailyStageAcknowledged;
        private bool dailyRunFinished;
        private string dailyOutcome;
        private string dailyError;
        private float dailyLocalSaveTimer;
        private float dailyOnlineSaveTimer;
        private DailyChallengeRun dailyPendingStage;

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveDailyChallengeLocally();
        }

        private void SaveDailyChallengeLocally()
        {
            if (!DailyChallengeSession.IsActive || resultHandled || stateMachine?.Simulator == null) return;
            DailyChallengeSession.SetCheckpoint(stateMachine.Simulator.CaptureDailyChallengeCheckpoint(false));
        }

        private void TickDailyChallengePersistence(float deltaTime)
        {
            if (!DailyChallengeSession.IsActive) return;
            if (DailyChallengeSession.Run.ClearedStage >= DailyChallengeCatalog.StageCount && !resultHandled)
            {
                HandleDailyChallengeEnd("clear");
                return;
            }
            if (resultHandled || dailyTransactionBusy) return;
            dailyLocalSaveTimer += deltaTime;
            dailyOnlineSaveTimer += deltaTime;
            if (dailyLocalSaveTimer >= 2f)
            {
                dailyLocalSaveTimer = 0f;
                SaveDailyChallengeLocally();
            }
            if (dailyOnlineSaveTimer < 15f || OnlinePlayerData.Busy) return;
            dailyOnlineSaveTimer = 0f;
            SaveDailyChallengeLocally();
            var captured = DailyChallengeSession.Clone(DailyChallengeSession.Run);
            OnlinePlayerData.DailyChallengeCheckpoint(captured, (confirmed, error) =>
            {
                if (this == null || resultHandled || dailyTransactionBusy || !DailyChallengeSession.IsActive) return;
                if (error == null && confirmed != null && confirmed.ClearedStage == DailyChallengeSession.Run.ClearedStage)
                {
                    // The live simulation has advanced while the request was in flight.
                    // Keep its newer local snapshot after accepting the authoritative run.
                    DailyChallengeSession.Accept(confirmed);
                    SaveDailyChallengeLocally();
                }
            });
        }

        private void HandleDailyChallengeWin()
        {
            if (dailyTransactionBusy || dailyStageAcknowledged || dailyRunFinished) return;
            StopAutoRepeatSameFloor();
            ClearBattleAnnouncements();
            ClearBossEntranceFlash();
            resultHandled = true;
            lastBattleWon = true;
            dailyOutcome = null;
            DailyChallengeSession.SetCheckpoint(stateMachine.Simulator.CaptureDailyChallengeCheckpoint(true));
            dailyPendingStage = DailyChallengeSession.Clone(DailyChallengeSession.Run);
            SetDailyResult(true);
            CommitDailyStage();
        }

        private void CommitDailyStage()
        {
            if (dailyTransactionBusy || dailyPendingStage == null) return;
            dailyTransactionBusy = true;
            dailyError = null;
            ApplyDailyChallengeResultPresentation();
            StartCoroutine(CommitDailyStageWhenReady());
        }

        private IEnumerator CommitDailyStageWhenReady()
        {
            while (OnlinePlayerData.Busy) yield return null;
            OnlinePlayerData.DailyChallengeCheckpoint(dailyPendingStage, (confirmed, error) =>
            {
                if (this == null) return;
                dailyTransactionBusy = false;
                if (error != null || confirmed == null || confirmed.ClearedStage < currentFloor)
                {
                    dailyError = error ?? "突破記録の保存を確認できません。再送信してください。";
                    ApplyDailyChallengeResultPresentation();
                    return;
                }
                DailyChallengeSession.Accept(confirmed);
                dailyStageAcknowledged = true;
                AudioManager.Instance?.PlaySe(AudioCue.Reward);
                if (confirmed.ClearedStage >= DailyChallengeCatalog.StageCount) HandleDailyChallengeEnd("clear");
                else ApplyDailyChallengeResultPresentation();
            });
        }

        private void SetDailyResult(bool won)
        {
            int level = GameManager.Instance?.PlayerProfile?.Level ?? 1;
            int cleared = DailyChallengeSession.Run.ClearedStage;
            var result = new BattleResultViewData(won, 0, 0, 0, 0, level, level, currentFloor,
                cleared + 1, string.Empty, string.Empty);
            lastResultViewData = result;
            hasLastResultViewData = true;
            stateMachine.ShowResultPanel(result);
            ShowMinimalResultOverlay(result);
        }

        private void HandleDailyChallengeEnd(string outcome)
        {
            if (dailyTransactionBusy || dailyRunFinished) return;
            StopAutoRepeatSameFloor();
            SaveDailyChallengeLocally();
            resultHandled = true;
            dailyOutcome = outcome;
            lastBattleWon = outcome == "clear";
            dailyTransactionBusy = true;
            dailyError = null;
            CancelRetire();
            SetDailyResult(lastBattleWon);
            StartCoroutine(FinishDailyChallengeWhenReady(outcome));
        }

        private IEnumerator FinishDailyChallengeWhenReady(string outcome)
        {
            while (OnlinePlayerData.Busy) yield return null;
            string runId = DailyChallengeSession.Run.RunId;
            OnlinePlayerData.DailyChallengeFinish(runId, outcome, (ok, error) =>
            {
                if (this == null) return;
                dailyTransactionBusy = false;
                if (!ok)
                {
                    dailyError = error ?? "終了記録の保存を確認できません。再送信してください。";
                    ApplyDailyChallengeResultPresentation();
                    return;
                }
                dailyRunFinished = true;
                ApplyDailyChallengeResultPresentation();
            });
        }

        private void ContinueDailyChallenge()
        {
            if (dailyTransactionBusy || !dailyStageAcknowledged || dailyRunFinished ||
                DailyChallengeSession.Run.NextStage > DailyChallengeCatalog.StageCount) return;
            currentFloor = DailyChallengeSession.Run.NextStage;
            dailyStageAcknowledged = false;
            dailyPendingStage = null;
            dailyOutcome = dailyError = null;
            dailyLocalSaveTimer = dailyOnlineSaveTimer = 0f;
            PrepareBattleSession();
            stateMachine.Begin(currentFloor);
            ApplyMinimalPresentation();
            RefreshBattlePresentation(force: true);
        }

        private void ReturnFromDailyChallenge()
        {
            if (dailyTransactionBusy) return;
            if (!dailyRunFinished) { HandleDailyChallengeEnd("retreat"); return; }
            DailyChallengeSession.End(true);
            SceneTransitionGuard.LoadScene(homeSceneName);
        }

        private void ApplyDailyChallengeResultPresentation()
        {
            if (!DailyChallengeSession.IsActive || !hasLastResultViewData) return;
            bool complete = DailyChallengeSession.Run.ClearedStage >= DailyChallengeCatalog.StageCount;
            bool stageResult = string.IsNullOrEmpty(dailyOutcome);
            if (minimalResultTitleText != null) minimalResultTitleText.text = stageResult
                ? "第" + currentFloor + "段階 突破" : complete ? "試練 完全突破" : dailyOutcome == "defeat" ? "試練 敗北" : "試練 終了";
            if (minimalResultSummaryText != null) minimalResultSummaryText.text =
                DailyChallengeCatalog.Label(DailyChallengeSession.Run.Mode) + "\n" +
                (stageResult ? "HP・戦闘不能・攻撃待ち時間を持ち越して進みます。" :
                    "突破した " + DailyChallengeSession.Run.ClearedStage + " 段階分の報酬を" + (dailyRunFinished ? "受け取りました。" : "受け取ります。"));
            if (minimalResultRewardText != null) minimalResultRewardText.text = dailyTransactionBusy
                ? "突破・報酬の記録を保存しています…"
                : !string.IsNullOrEmpty(dailyError) ? dailyError + "\n保存が完了してから進めます。"
                : stageResult ? DailyChallengeCatalog.RewardLabel(currentFloor) + "\n突破報酬を確定・挑戦終了時に受取" : BuildDailyTotalRewardLabel() + "\n" +
                    (dailyRunFinished ? "全突破分の報酬を受取済み" : "報酬の受取を確認しています…");
            if (minimalResultRetryFloorButton != null) minimalResultRetryFloorButton.gameObject.SetActive(false);
            if (minimalResultForecastText != null) minimalResultForecastText.gameObject.SetActive(false);
            if (minimalResultNextFloorButton != null)
            {
                bool retry = !string.IsNullOrEmpty(dailyError);
                minimalResultNextFloorButton.gameObject.SetActive(retry || stageResult);
                minimalResultNextFloorButton.interactable = !dailyTransactionBusy && (retry || dailyStageAcknowledged);
                minimalResultNextFloorButton.onClick.RemoveAllListeners();
                if (retry) minimalResultNextFloorButton.onClick.AddListener(() =>
                { if (stageResult) CommitDailyStage(); else HandleDailyChallengeEnd(dailyOutcome); });
                else minimalResultNextFloorButton.onClick.AddListener(ContinueDailyChallenge);
                ConfigureMinimalResultButtonRect(minimalResultNextFloorButton, new Vector2(.08f, .07f), new Vector2(.48f, .15f));
            }
            if (minimalResultNextFloorButtonText != null) minimalResultNextFloorButtonText.text =
                !string.IsNullOrEmpty(dailyError) ? "記録を再送信" : "第" + (currentFloor + 1) + "段階へ";
            if (minimalResultHomeButton != null)
            {
                minimalResultHomeButton.interactable = !dailyTransactionBusy && string.IsNullOrEmpty(dailyError);
                ConfigureMinimalResultButtonRect(minimalResultHomeButton, new Vector2(.52f, .07f), new Vector2(.92f, .15f));
            }
            if (minimalResultHomeButtonText != null) minimalResultHomeButtonText.text = dailyRunFinished ? "試練一覧へ戻る" : "ここで終了";
        }

        private string BuildDailyTotalRewardLabel()
        {
            int drops = 0, cores = 0, gold = 0, exp = 0;
            for (int stage = 1; stage <= DailyChallengeSession.Run.ClearedStage; stage++)
            {
                drops += DailyChallengeCatalog.Drops(stage); cores += DailyChallengeCatalog.StarCores(stage);
                gold += DailyChallengeCatalog.Gold(stage); exp += DailyChallengeCatalog.MonsterExp(stage);
            }
            return $"修練の雫 ×{drops}  試練の星核 ×{cores}\nゴールド {gold}・参加モンスター経験値 {exp}";
        }
    }
}
