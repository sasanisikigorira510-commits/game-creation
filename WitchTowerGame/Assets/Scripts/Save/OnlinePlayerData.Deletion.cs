using System;
using System.Collections;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        // Describe this operation's boundary without promising a retention
        // deadline or implying that periodic backup expiry is unimplemented.
        private const string DeletionRetentionNotice = "この削除操作では、過去のバックアップや他端末・手動コピーは直接消去されません。\n削除済みデータの復活や購入の再付与を防ぐ確認用記録は別に保持します。";
        [Serializable] private sealed class DeletionBody { public string PlayerId, ConfirmationToken; }
        private AccountDeletionResult deletionReview;
        private AccountDeletionJournal deletionJournal;
        private bool deletionMayRetry;
        private Vector2 accountScroll;
#if UNITY_EDITOR
        internal static Action EditorDeletionPreferencesCleanup;
#endif
        private IEnumerator DeletionPreview()
        {
            if (StorageOwnerUnavailable) yield break;
            if (!AppleAccountEnabled || accountJournal != null || DeletionBlocked) yield break;
            if (WitchTower.Monetization.InAppPurchaseService.Instance?.IsPurchaseInProgress == true)
            { accountMessage = "購入処理中です。購入結果を確認してから削除を開始してください。"; yield break; }
            if (!Prepare()) { accountMessage = failure; yield break; }
            // No registration, receipt delivery, or automatic reward collection
            // as a side effect of reviewing a deletion request.
            yield return Send("POST", "/v1/account-deletion/preview", JsonUtility.ToJson(new DeletionBody { PlayerId = credentials.PlayerId }));
            if (failure != null) { accountMessage = "削除対象を確認できません。接続・登録状態をご確認ください。削除は開始していません。"; yield break; }
            var review = JsonUtility.FromJson<AccountDeletionResult>(response);
            if (DeletionReviewSummary(review) == null || review.PlayerId != credentials.PlayerId)
                throw new InvalidOperationException("Invalid deletion preview.");
            deletionReview = review;
            unlinkReview = null;
            accountMessage = "ゲームアカウントを削除しますか？\n仲間・進行状況・所持石・永続強化を失い、元に戻せません。Apple連携解除とは異なります。\n購入の返金手続きではありません。保留中の購入がある場合は先に確認してください。\n" + DeletionRetentionNotice;
        }
        private IEnumerator DeletionSubmit()
        {
            if (StorageOwnerUnavailable) yield break;
            if (!AppleAccountEnabled || deletionReview == null || credentials == null || accountJournal != null || DeletionBlocked) yield break;
            if (WitchTower.Monetization.InAppPurchaseService.Instance?.IsPurchaseInProgress == true)
            { accountMessage = "購入処理中のため、削除は開始していません。"; yield break; }
            if (DeletionReviewSummary(deletionReview) == null || deletionReview.PlayerId != credentials.PlayerId)
                throw new InvalidOperationException("Account changed or deletion preview is incomplete.");
            var game = GameManager.Instance;
            if (game?.PlayerProfile == null || !SaveManager.Instance.TrySaveWithReason(
                game.PlayerProfile.ToSaveData(game.CurrentFloor), "before_account_deletion", out _))
            { accountMessage = "現在の状態を保存できません。削除は開始していません。"; yield break; }
            deletionJournal = AccountDeletionStorage.Begin(AccountRoot, credentials.PlayerId, credentials.Token,
                deletionReview.ConfirmationToken, baseUrl);
            deletionReview = null;
            deletionMayRetry = false;
            SaveManager.Instance.LoadOrCreate(); // Stop saves, scene changes, IAP and in-memory gameplay.
            yield return DeletionRequest("commit");
        }
        private IEnumerator DeletionRequest(string action)
        {
            if (StorageOwnerUnavailable) yield break;
            deletionJournal = AccountDeletionStorage.Pending(AccountRoot);
            if (deletionJournal?.Phase != "requested") yield break;
            if (baseUrl != deletionJournal.Endpoint)
            { accountMessage = "削除を開始した接続先と設定が異なります。認証情報は送信せず停止しています。"; yield break; }
            deletionMayRetry = false;
            accountMessage = "削除結果を確認しています。アプリを削除しないでください。";
            yield return Send("POST", "/v1/account-deletion/" + action, JsonUtility.ToJson(new DeletionBody {
                PlayerId = deletionJournal.PlayerId, ConfirmationToken = deletionJournal.ConfirmationToken }), false, deletionJournal.Token);
            if (failure != null)
            {
                accountMessage = status == 401 ? "削除結果を認証できません。記録は保全しています。アプリを削除せずサポートへご連絡ください。" :
                    status == 409 ? "確認期限切れ、またはデータが更新されています。削除の取り消しを確認してから、確認画面を開き直してください。" :
                    "結果が不明です。「削除結果を再確認」を押してください。確認が済むまでプレイ・購入は停止しています。";
                yield break;
            }
            var result = JsonUtility.FromJson<AccountDeletionResult>(response);
            if (result == null || result.PlayerId != deletionJournal.PlayerId) throw new InvalidOperationException("Invalid deletion account.");
            if (result.Status == "deleted")
            {
                AccountDeletionStorage.Confirm(AccountRoot, result);
                DeletionCleanup();
            }
            else if (action == "status" && result.Status == "not_deleted")
            {
                deletionMayRetry = true;
                accountMessage = "サーバーではまだ削除されていません。削除を再送するか、サーバーで取り消しを確定してください。";
            }
            else if (action == "cancel" && result.Status == "cancelled")
            {
                AccountDeletionStorage.Cancel(AccountRoot, result);
                deletionJournal = null;
                credentials = null;
                FinishAccountSwitch();
                accountMessage = "削除を取り消しました。ゲームデータは保持しています。";
            }
            else throw new InvalidOperationException("Invalid deletion result.");
        }
        private void DeletionCleanup()
        {
            if (StorageOwnerUnavailable) return;
            AccountDeletionStorage.Erase(AccountRoot);
#if UNITY_EDITOR
            if (EditorDeletionPreferencesCleanup != null) EditorDeletionPreferencesCleanup();
            else
#endif
                WitchTower.Home.TenPullPresentationJournal.Clear();
            AccountDeletionStorage.Finish(AccountRoot);
            deletionJournal = AccountDeletionStorage.Pending(AccountRoot);
            credentials = null;
            pendingRecovery = null;
            response = null;
            accountJournal = null;
            unlinkReview = null;
            purchaseReady = false;
            InvalidatePurchaseCapability();
            SaveManager.Instance.LoadOrCreate();
            accountMessage = "サーバーの現在のゲームデータと、この端末で対象を確認できた保存データを削除しました。\n" + DeletionRetentionNotice +
                (deletionJournal.ManualRevocationRequired ? "\nApple側の失効は追加確認が必要です。サポートへご連絡ください。" :
                 deletionJournal.RevocationPending ? "\nApple側の認証失効は処理待ちです。" : "");
        }
        private bool DrawDeletion(GUIStyle label, GUIStyle button)
        {
            if (deletionJournal != null)
            {
                if (deletionJournal.Phase == "requested")
                {
                    if (AccountButton("削除結果を再確認", button)) AppleFlow(DeletionRequest("status"));
                    if (deletionMayRetry && AccountButton("確認しました：削除を再送", button, danger: true)) AppleFlow(DeletionRequest("commit"));
                    if (AccountButton("削除を取り消す（サーバーに確認）", button)) AppleFlow(DeletionRequest("cancel"));
                }
                else if (deletionJournal.Phase == "confirmed")
                {
                    GUILayout.Label("サーバー削除は確定済みです。端末の消去を再開してください。", label);
                    if (AccountButton("端末の消去を再開", button, danger: true))
                    {
                        try { DeletionCleanup(); }
                        catch (Exception) { accountMessage = "端末の消去を完了できません。記録を保持し、プレイを停止しています。"; }
                    }
                }
                else if (deletionJournal.Phase == "completed")
                {
                    GUILayout.Label("削除済みです。以前のアカウントには自動で戻りません。", label);
                    if (AccountButton("新しいデータで最初から始める", button))
                    {
                        try { DeletionNewGuest(); }
                        catch (Exception) { accountMessage = "新しいデータを保存できません。もう一度お試しください。"; }
                    }
                }
                return true;
            }
            if (DeletionBlocked)
            { GUILayout.Label("削除記録を読み取れません。アプリを削除せずサポートへお問い合わせください。", label); return true; }
            if (deletionReview == null) return false;
            string summary = DeletionReviewSummary(deletionReview);
            GUILayout.Label(summary ?? "削除対象の情報を確認できません。削除は開始していません。戻って再確認してください。", label);
            if (summary != null && AccountButton("すべて失うことを確認して削除する", button, 95, danger: true)) AppleFlow(DeletionSubmit());
            if (AccountButton("削除せず戻る", button))
            { deletionReview = null; accountMessage = "削除は開始していません。"; }
            return true;
        }
        private static string DeletionReviewSummary(AccountDeletionResult review)
        {
            string prefix = AccountPlayerPrefix(review?.PlayerId);
            if (prefix == null || review.Status != "confirm_delete" || !AccountHex(review.ConfirmationToken, 64) ||
                review.Free < 0 || review.Paid < 0) return null;
            return $"削除対象：{prefix}…\nサーバー所持：無償石 {review.Free} / 有償石 {review.Paid}";
        }
        private void DeletionNewGuest()
        {
            if (StorageOwnerUnavailable) return;
            AccountDeletionStorage.StartNewGuest(AccountRoot);
            deletionJournal = null;
            FinishAccountSwitch();
        }
    }
}
