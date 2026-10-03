using System;
using System.Linq;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        [Serializable]
        private sealed class TrainingSnapshotAcknowledgement
        {
            public string[] AcknowledgedTrainingFusionReceiptIds;
        }

        private bool ApplyTrainingSnapshotAcknowledgement()
        {
            var acknowledged = JsonUtility.FromJson<TrainingSnapshotAcknowledgement>(response);
            var ids = acknowledged?.AcknowledgedTrainingFusionReceiptIds;
            if (ids == null || ids.Length == 0) return true;
            var profile = GameManager.Instance?.PlayerProfile;
            if (profile == null || StorageOwnerUnavailable) return false;
            var staged = profile.ToSaveData(GameManager.Instance.CurrentFloor);
            var idSet = new System.Collections.Generic.HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)));
            int removed = staged.TrainingFusionReceipts.RemoveAll(receipt => receipt != null && idSet.Contains(receipt.ChildInstanceId));
            if (removed == 0) return true;
            if (!SaveManager.Instance.TrySaveWithReason(staged, "training_fusion_ack", out _))
            {
                failure = "配合履歴の同期結果を端末に保存できませんでした。";
                return false;
            }
            profile.AcknowledgeTrainingFusionReceipts(ids);
            return true;
        }

        private DailyChallengeState dailyChallengeState;
        public static DailyChallengeState DailyChallenges => GameManager.Instance?.PlayerProfile?.DailyChallenges
            ?? Instance?.dailyChallengeState;

        private static bool IsDailyChallengeRequest(OnlineRequest request) => request != null && IsDailyChallengeRequest(request.Kind);
        private static bool IsDailyChallengeRequest(string kind) =>
            (kind != null && kind.StartsWith("daily_challenge_", StringComparison.Ordinal)) ||
                kind == "monster_training" || kind == "monster_skill_training";

        private static bool SameDailyCheckpoint(DailyChallengeCheckpoint first, DailyChallengeCheckpoint second) =>
            first == null ? second == null : second != null && JsonUtility.ToJson(first) == JsonUtility.ToJson(second);

        public static void RefreshDailyChallenges(Action<DailyChallengeState, string> completed)
        {
            Ensure().Execute(null, (_, error) => completed?.Invoke(DailyChallenges, error));
        }

        public static void DailyChallengeStart(string modeId, Action<DailyChallengeRun, string> completed)
        {
            var profile = GameManager.Instance?.PlayerProfile;
            var party = profile?.PartyMonsterInstanceIds?.Where(id => !string.IsNullOrEmpty(id) &&
                profile.GetOwnedMonster(id) != null).Distinct().Take(5).ToArray() ?? Array.Empty<string>();
            if (party.Length == 0) { completed?.Invoke(null, "出撃するモンスターを編成してください。"); return; }
            Ensure().Execute(new OnlineRequest { Kind = "daily_challenge_start", Mode = modeId,
                PartyInstanceIds = party }, (op, error) =>
                completed?.Invoke(op?.DailyChallengeRun ?? op?.DailyChallenges?.ActiveRun, error));
        }

        public static void DailyChallengeCheckpoint(DailyChallengeRun run, Action<DailyChallengeRun, string> completed)
        {
            if (run == null || string.IsNullOrEmpty(run.RunId) || run.Checkpoint == null)
            { completed?.Invoke(null, "挑戦中の記録を確認できません。"); return; }
            Ensure().Execute(new OnlineRequest { Kind = "daily_challenge_stage", RunId = run.RunId,
                Stage = run.Checkpoint.StageComplete ? run.Checkpoint.Stage : run.ClearedStage,
                Checkpoint = run.Checkpoint }, (op, error) =>
                completed?.Invoke(op?.DailyChallengeRun ?? op?.DailyChallenges?.ActiveRun, error));
        }

        public static void DailyChallengeFinish(string runId, string outcome, Action<bool, string> completed)
        {
            Ensure().Execute(new OnlineRequest { Kind = "daily_challenge_finish", RunId = runId,
                Outcome = outcome }, (op, error) => completed?.Invoke(op != null && error == null, error));
        }

        public static void TrainMonster(string instanceId, MonsterTrainingStatType stat, Action<bool, string> completed)
        {
            Ensure().Execute(new OnlineRequest { Kind = "monster_training", Target = instanceId,
                Stat = stat.ToString() }, (op, error) => completed?.Invoke(op != null && error == null, error));
        }

        public static void TrainMonsterSkill(string instanceId, Action<bool, string> completed)
        {
            Ensure().Execute(new OnlineRequest { Kind = "monster_skill_training", Target = instanceId },
                (op, error) => completed?.Invoke(op != null && error == null, error));
        }
    }
}
