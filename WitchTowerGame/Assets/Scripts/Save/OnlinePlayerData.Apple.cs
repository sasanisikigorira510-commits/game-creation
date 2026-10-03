using System;
using System.Collections;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        [Serializable] private sealed class AppleChallengeBody { public string Mode, PlayerId; }
        [Serializable] private sealed class AppleRecoverChallengeBody { public string Mode = "recover"; }
        [Serializable] private sealed class AppleChallenge { public string ChallengeId, Nonce; public int ExpiresIn; }
        [Serializable] private sealed class AppleVerifyBody { public string ChallengeId, IdentityToken, AuthorizationCode; }
        [Serializable] private sealed class AppleResultBody { public string ChallengeId; }
        [Serializable] private sealed class AppleCommitBody { public string ChallengeId, PreviewToken; }
        [Serializable] private sealed class AppleLinked { public string Status, PlayerId; }
        [Serializable] private sealed class AppleUnlinkBody { public string PlayerId, ConfirmationToken; }
        [Serializable] private sealed class AppleUnlinkResult
        { public string Status, PlayerId, ConfirmationToken; public bool RevocationPending, ManualRevocationRequired; }
        private AppleUnlinkResult unlinkReview;
        private bool accountMenu;
        private string accountMessage = "Apple連携は任意です。普段はログインせず遊べます。";
        private AppleRecoveryJournal accountJournal;
        private bool accountRecordInvalid;
        private bool accountDrawFailed;
        private bool accountFallbackLayoutReady;
        private string AccountRoot => SaveManager.Instance.RootDirectory;
#if UNITY_EDITOR
        internal static bool EditorAppleAccountEnabled;
        // Nullable override lets layout fixtures exercise the disabled state even
        // when the packaged public configuration opts in. Never compiled in players.
        internal static bool? EditorAppleAccountOverride;
        internal static Action EditorAccountSwitchCompleted;
        internal static Action EditorAccountDrawingOverride;
#endif

        public static bool AppleAccountEnabled
        {
            get
            {
                if (SaveManager.Instance != null && !SaveManager.Instance.StorageAccessAvailable) return false;
                string raw = SaveManager.Instance != null ? SaveManager.Instance.PlayerDataConfigurationJson
                    : Resources.Load<TextAsset>("PlayerDataService")?.text;
                if (RefundSandboxConfiguration.IsConfiguredFrom(raw)) return false;
#if UNITY_EDITOR
                if (EditorAppleAccountOverride.HasValue) return EditorAppleAccountOverride.Value;
                if (EditorAppleAccountEnabled) return true;
#endif
                var config = raw != null ? JsonUtility.FromJson<AppleAccountConfiguration>(raw) : null;
                if (SaveManager.Instance?.HasPurchaseRealmAuthority == true &&
                    SaveManager.Instance.PurchaseEnvironment == "Sandbox" && config?.IsReviewSandbox == true)
                    return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return config?.IsEnabled(true) == true;
#else
                return config?.IsEnabled(false) == true;
#endif
            }
        }
        public void OpenAppleAccount(bool resume = false)
        {
            if (StorageOwnerUnavailable) return;
            if (busy || rewardedAdReserved || pendingRecovery != null || (!AppleAccountEnabled && !resume)) return;
            accountMenu = true;
            purchaseReady = false;
            InvalidatePurchaseCapability();
            accountDrawFailed = false;
            accountFallbackLayoutReady = false;
            ReloadAccountRecords(AccountRoot);
        }
        private void ReloadAccountRecords(string root)
        {
            if (StorageOwnerUnavailable) return;
            // Never retain a stale confirmation after a failed/repeated read.
            accountJournal = null;
            deletionJournal = null;
            unlinkReview = null;
            deletionReview = null;
            deletionMayRetry = false;
            accountRecordInvalid = false;
            accountMessage = "Apple連携は任意です。普段はログインせず遊べます。";
            try
            {
                deletionJournal = AccountDeletionStorage.Pending(root);
                if (deletionJournal != null)
                {
                    accountJournal = null;
                    accountMessage = deletionJournal.Phase == "completed" ? "アカウントの削除は完了しています。\n" + DeletionRetentionNotice :
                        "前回のアカウント削除を確認してください。確認が済むまでプレイ・購入を停止しています。";
                    if (deletionJournal.Phase == "completed")
                        accountMessage += deletionJournal.ManualRevocationRequired ? "\nApple側の失効は追加確認が必要です。サポートへご連絡ください。" :
                            deletionJournal.RevocationPending ? "\nApple側の認証失効は処理待ちです。" : "";
                }
                else
                {
                    accountJournal = AppleRecoveryStorage.Pending(root);
                    if (accountJournal?.Phase == "started")
                        accountMessage = "前回のApple本人確認が途中です。認証結果を再確認するか、復旧をキャンセルして元のデータに戻ってください。";
                    else if (accountJournal != null)
                        accountMessage = "前回のデータ復旧が途中です。復旧先と引き継ぎ結果をご確認ください。";
                }
            }
            catch (Exception)
            {
                accountJournal = null;
                deletionJournal = null;
                accountRecordInvalid = true;
                accountMessage = "アカウント操作の記録を読み取れません。データは保持しています。アプリを削除せず、再確認してください。";
            }
        }
        private void AppleFlow(IEnumerator routine)
        {
            if (StorageOwnerUnavailable) return;
            if (busy || rewardedAdReserved) return;
            busy = true;
            StartCoroutine(AppleGuard(routine));
        }
        private IEnumerator AppleGuard(IEnumerator routine)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            try
            {
                // Drive nested iterators too so storage errors stop the whole flow.
                stack.Push(routine);
                while (stack.Count != 0)
                {
                    if (StorageOwnerUnavailable) yield break;
                    object value = null;
                    bool more;
                    try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
                    catch (Exception)
                    {
                        if (StorageOwnerUnavailable) yield break;
                        ReloadAccountRecords(AccountRoot);
                        if (!accountRecordInvalid)
                            accountMessage = "処理を完了できませんでした。アカウント操作の記録を確認してください。";
                        if (DeletionBlocked) SaveManager.Instance.LoadOrCreate();
                        yield break;
                    }
                    if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (value is IEnumerator nested) stack.Push(nested);
                    else yield return value;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                busy = false;
                purchaseReady = false;
                InvalidatePurchaseCapability();
            }
        }
        private IEnumerator AppleSignIn(bool recover)
        {
            if (StorageOwnerUnavailable) yield break;
            if (DeletionBlocked) yield break;
            if (!AppleAccountEnabled || string.IsNullOrEmpty(baseUrl))
            { accountMessage = "Apple連携の接続先が未設定です。"; yield break; }
            if (accountJournal != null) { accountMessage = "先に前回の復旧を確認してください。"; yield break; }
            string token;
            if (recover)
            {
                accountJournal = AppleRecoveryStorage.Begin(AccountRoot);
                token = accountJournal.Token;
            }
            else
            {
                string syncError = null;
                yield return RunCore(null, (_, error) => syncError = error);
                busy = true;
                if (syncError != null || credentials == null) { accountMessage = syncError ?? "先にデータ同期が必要です。"; yield break; }
                token = credentials.Token;
            }
            accountMessage = "Appleで本人確認しています…";
            string challengeBody = recover ? JsonUtility.ToJson(new AppleRecoverChallengeBody()) :
                JsonUtility.ToJson(new AppleChallengeBody { Mode = "link", PlayerId = credentials.PlayerId });
            yield return Send("POST", "/v1/apple/challenge", challengeBody, false, token);
            if (failure != null) { accountMessage = "Apple連携に接続できません。サーバーの有効化も必要です。"; yield break; }
            var challenge = JsonUtility.FromJson<AppleChallenge>(response);
            if (challenge == null || challenge.ChallengeId?.Length != 64 || challenge.Nonce?.Length != 64)
                throw new InvalidOperationException("Invalid challenge.");
            if (recover) AppleRecoveryStorage.SetChallenge(AccountRoot, accountJournal, challenge.ChallengeId);
            var native = GetComponent<AppleNativeIdentity>() ?? gameObject.AddComponent<AppleNativeIdentity>();
            AppleNativeIdentity.Result identity = null;
            yield return native.Authorize(challenge.Nonce, challenge.ChallengeId, value => identity = value);
            if (identity == null || !string.IsNullOrEmpty(identity.Error) || string.IsNullOrEmpty(identity.IdentityToken) ||
                string.IsNullOrEmpty(identity.AuthorizationCode))
            { accountMessage = !string.IsNullOrEmpty(identity?.Error) ? identity.Error : "認証を確認できませんでした。"; yield break; }
            string verification = JsonUtility.ToJson(new AppleVerifyBody { ChallengeId = challenge.ChallengeId,
                IdentityToken = identity.IdentityToken, AuthorizationCode = identity.AuthorizationCode });
            identity.IdentityToken = null;
            identity.AuthorizationCode = null;
            yield return Send("POST", "/v1/apple/verify", verification, false, token);
            verification = null; // Never persist/retry the Apple JWT or single-use code.
            if (failure != null)
            {
                accountMessage = status == 404 ? "このAppleアカウントには連携済みのゲームデータがありません。" :
                    status == 409 ? "認証が処理中・結果不明、または連携先と競合しています。解消しない場合はサポートへご連絡ください。" :
                    status == 423 ? "このデータは運営の確認待ちです。" : "認証を確認できませんでした。もう一度お試しください。";
                yield break;
            }
            if (!recover)
            {
                var linked = JsonUtility.FromJson<AppleLinked>(response);
                if (linked.Status != "linked" || linked.PlayerId != credentials.PlayerId) throw new InvalidOperationException("Invalid linked account.");
                accountMessage = "Apple連携が完了しました。同じAppleアカウントでデータを復旧できます。";
                yield break;
            }
            var bundle = JsonUtility.FromJson<AppleRecoveryBundle>(response);
            AppleRecoveryStorage.Stage(AccountRoot, accountJournal, bundle);
            accountMessage = "復旧するデータをご確認ください。現在のデータとは合算されません。";
        }
        private IEnumerator AppleVerifyRetry()
        {
            if (StorageOwnerUnavailable) yield break;
            if (accountJournal?.Phase != "started" || string.IsNullOrEmpty(accountJournal.ChallengeId)) yield break;
            accountMessage = "認証結果だけを再確認しています…";
            yield return Send("POST", "/v1/apple/verify", JsonUtility.ToJson(new AppleResultBody {
                ChallengeId = accountJournal.ChallengeId }), false, accountJournal.Token);
            if (failure != null)
            {
                accountMessage = status == 401 || status == 400 ? "保存済みの認証結果がないか、期限切れです。復旧をキャンセルして再認証してください。" :
                    "認証結果をまだ確認できません。繰り返す場合は、データを削除せずサポートへご連絡ください。";
                yield break;
            }
            AppleRecoveryStorage.Stage(AccountRoot, accountJournal, JsonUtility.FromJson<AppleRecoveryBundle>(response));
            accountMessage = "復旧するデータをご確認ください。現在のデータとは合算されません。";
        }
        private IEnumerator AppleUnlinkPreview()
        {
            if (StorageOwnerUnavailable) yield break;
            if (DeletionBlocked) yield break;
            if (!AppleAccountEnabled || accountJournal != null || string.IsNullOrEmpty(baseUrl)) yield break;
            string syncError = null;
            yield return RunCore(null, (_, error) => syncError = error);
            busy = true;
            if (syncError != null || credentials == null) { accountMessage = syncError ?? "先にデータ同期が必要です。"; yield break; }
            yield return Send("POST", "/v1/apple/unlink/preview", JsonUtility.ToJson(new AppleUnlinkBody { PlayerId = credentials.PlayerId }));
            if (failure != null) { accountMessage = "連携状態を確認できません。解除は行っていません。"; yield break; }
            var review = JsonUtility.FromJson<AppleUnlinkResult>(response);
            if (review == null || review.PlayerId != credentials.PlayerId) throw new InvalidOperationException("Invalid unlink account.");
            if (review.Status == "not_linked") { unlinkReview = null; accountMessage = "現在のデータはAppleと連携されていません。"; yield break; }
            if (review.Status != "confirm_unlink" || review.ConfirmationToken?.Length != 64)
                throw new InvalidOperationException("Invalid unlink confirmation.");
            unlinkReview = review;
            accountMessage = "Apple連携を解除しますか？\nゲームデータ・仲間・石・購入履歴は残ります。\n解除後は、このAppleアカウントを使ったデータ復旧ができなくなります。\nこれはゲームアカウントの削除ではありません。";
        }
        private IEnumerator AppleUnlinkCommit()
        {
            if (StorageOwnerUnavailable) yield break;
            if (!AppleAccountEnabled || unlinkReview == null || accountJournal != null || credentials == null) yield break;
            if (unlinkReview.PlayerId != credentials.PlayerId) throw new InvalidOperationException("Unlink account changed.");
            yield return Send("POST", "/v1/apple/unlink/commit", JsonUtility.ToJson(new AppleUnlinkBody {
                PlayerId = unlinkReview.PlayerId, ConfirmationToken = unlinkReview.ConfirmationToken }));
            if (failure != null)
            {
                accountMessage = "解除結果を確認できません。同じ確認ボタンで結果を再確認できます。期限切れの場合は戻って連携状態を確認してください。";
                yield break; // Keep the confirmation for safe result-only retries.
            }
            var result = JsonUtility.FromJson<AppleUnlinkResult>(response);
            if (result?.Status != "unlinked" || result.PlayerId != credentials.PlayerId)
                throw new InvalidOperationException("Invalid unlink response.");
            unlinkReview = null;
            accountMessage = "ゲームデータとのApple連携を解除しました。ゲームデータと石は残っています。" +
                (result.ManualRevocationRequired ? "\nApple側の認証失効は追加確認が必要です。サポートへご連絡ください。" :
                 result.RevocationPending ? "\nApple側の認証失効は処理待ちです。" : "");
        }
        private IEnumerator AppleCommit()
        {
            if (StorageOwnerUnavailable) yield break;
            if (accountJournal == null) yield break;
            if (accountJournal.Phase == "preview") AppleRecoveryStorage.MarkRequested(AccountRoot, accountJournal);
            if (accountJournal.Phase == "requested")
            {
                accountMessage = "引き継ぎ結果を確認しています。アプリを削除しないでください。";
                yield return Send("POST", "/v1/apple/commit", JsonUtility.ToJson(new AppleCommitBody {
                    ChallengeId = accountJournal.ChallengeId, PreviewToken = accountJournal.Bundle.PreviewToken }), false, accountJournal.Token);
                if (failure != null)
                {
                    accountMessage = (status == 401 || status == 409 || status == 423)
                        ? "引き継ぎを自動確定できません。記録は保全しています。サポートへお問い合わせください。"
                        : "確定結果が不明です。「引き継ぎ結果を再確認」で同じ処理を確認してください。";
                    yield break;
                }
                AppleRecoveryStorage.MarkCommitted(AccountRoot, accountJournal, JsonUtility.FromJson<AppleRecoveryBundle>(response));
            }
            AppleRecoveryStorage.Activate(AccountRoot, accountJournal);
            accountJournal = null;
            FinishAccountSwitch();
        }
        private void FinishAccountSwitch()
        {
            purchaseReady = false;
            InvalidatePurchaseCapability();
            credentials = null;
            pendingRecovery = null;
            SaveManager.Instance.LoadOrCreate();
            if (SaveManager.Instance.RecoveryRequired) { accountMessage = "保存データの確認が必要です。"; return; }
            MasterDataManager.Instance?.Initialize();
            GameManager.Instance?.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
            accountMenu = false;
            LastMessage = "データを読み込みました。";
#if UNITY_EDITOR
            if (EditorAccountSwitchCompleted != null) { EditorAccountSwitchCompleted(); return; }
#endif
            WitchTower.Home.TenPullPresentationJournal.Clear(); // Cosmetic only; never changes awards.
            WitchTower.UI.SceneTransitionGuard.LoadScene("HomeScene");
        }
        private static string AppleRecoverySummary(AppleRecoveryJournal journal)
        {
            if (journal == null || !(journal.Phase == "preview" || journal.Phase == "requested" || journal.Phase == "committed"))
                return null;
            var bundle = journal.Bundle;
            string prefix = AccountPlayerPrefix(bundle?.PlayerId);
            if (prefix == null || bundle.Status != "preview" || bundle.Snapshot == null ||
                bundle.Snapshot.PlayerId != bundle.PlayerId || bundle.Free < 0 || bundle.Paid < 0) return null;
            return $"復旧先：{prefix}…\nプレイヤーLv {bundle.Snapshot.PlayerLevel}\n無償石 {bundle.Free} / 有償石 {bundle.Paid}";
        }
        private static bool AccountHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        private static string AccountPlayerPrefix(string playerId)
        {
            return AccountHex(playerId, 32) ? playerId.Substring(0, 8) : null;
        }
        private bool DrawAppleAccount()
        {
            if (StorageOwnerUnavailable) return false;
            if (!accountMenu) return false;
            // A fault may occur during Layout. Never replay a different control
            // tree against that incomplete cache in the following Repaint event.
            if (accountDrawFailed && !accountFallbackLayoutReady)
            {
                if (Event.current.type != EventType.Layout) GUIUtility.ExitGUI();
                accountFallbackLayoutReady = true;
            }
            // IMGUI shares one portrait reference scale; normal scene UI remains blocked.
            int previousDepth = GUI.depth;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            bool previousEnabled = GUI.enabled;
            try
            {
                GUI.depth = -10002;
                GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
                GUI.enabled = true;
                GUI.matrix = Matrix4x4.identity;
                AccountFill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.65f));
                float scale = Mathf.Min(Screen.width / 720f, Screen.height / 1000f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 720 * scale) / 2, (Screen.height - 1000 * scale) / 2), Quaternion.identity, Vector3.one * scale);
                PrepareAccountAppearance();
                DrawAccountPanel();
                GUILayout.BeginArea(new Rect(68, 158, 584, 690));
                try
                {
                    accountScroll = GUILayout.BeginScrollView(accountScroll);
                    try { DrawAppleAccountContents(); }
                    finally { GUILayout.EndScrollView(); }
                }
                finally { GUILayout.EndArea(); }
            }
            catch (ExitGUIException) { throw; } // Unity control flow is not an application failure.
            catch (Exception exception)
            {
                accountDrawFailed = true;
                accountFallbackLayoutReady = false;
                // Log only the type, never IDs/tokens or a response body. Do not
                // repeat the failing draw every frame; show a safe retry view.
                Debug.LogError("[AppleAccountUI] Drawing stopped safely: " + exception.GetType().Name);
                GUIUtility.ExitGUI(); // Restart layout safely on the next GUI event.
            }
            finally
            {
                GUI.depth = previousDepth;
                GUI.matrix = previous;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
                GUI.enabled = previousEnabled;
            }
            return true;
        }
        private void DrawAppleAccountContents()
        {
            var label = accountLabelStyle;
            var button = accountButtonStyle;
            if (accountRecordInvalid || accountDrawFailed)
            {
                GUILayout.Label("アカウント情報の再確認が必要です。", accountTitleStyle);
                GUILayout.Label("データは保持しています。アプリを削除しないでください。再確認しても戻れない場合はサポートへお問い合わせください。", label);
                GUI.enabled = !busy;
                if (AccountButton("アカウント情報を再読み込み", button)) OpenAppleAccount(true);
                if (SaveManager.Instance != null && !SaveManager.Instance.RecoveryRequired && !HasPendingAccountRecord() &&
                    AccountButton("閉じる", button)) accountMenu = false;
                return;
            }
#if UNITY_EDITOR
            EditorAccountDrawingOverride?.Invoke();
#endif
            GUILayout.Label("アカウント管理", accountTitleStyle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label("【開発検証】", accountCaptionStyle);
#endif
            GUILayout.Space(12);
            GUILayout.Label(accountMessage, label);
            GUILayout.Space(16);
            GUI.enabled = !busy;
            string recoverySummary = AppleRecoverySummary(accountJournal);
            if (recoverySummary != null)
            {
                GUILayout.Label(recoverySummary, label);
                GUILayout.Space(20);
            }
            if (DrawDeletion(label, button)) { }
            else if (accountJournal != null)
            {
                if (accountJournal.Phase == "started" && !string.IsNullOrEmpty(accountJournal.ChallengeId) &&
                    AccountButton("認証結果を再確認（コードは再送しません）", button, 90)) AppleFlow(AppleVerifyRetry());
                if (accountJournal.Phase == "preview")
                {
                    GUILayout.Label(AccountRecoveryBackupNotice, accountCaptionStyle);
                    GUILayout.Space(6);
                }
                if (recoverySummary != null && AccountButton(accountJournal.Phase == "preview" ?
                    AccountRecoverySwitchText : "引き継ぎ結果を再確認", button, 95)) AppleFlow(AppleCommit());
                if ((accountJournal.Phase == "started" || accountJournal.Phase == "preview") &&
                    AccountButton("復旧をキャンセルして戻る", button))
                {
                    try { AppleRecoveryStorage.Cancel(AccountRoot, accountJournal); accountJournal = null; FinishAccountSwitch(); }
                    catch (Exception) { accountMessage = "記録を保存できません。アプリを削除しないでください。"; }
                }
            }
            else if (unlinkReview != null)
            {
                if (AccountButton("確認しました：Apple連携を解除する", button, 90)) AppleFlow(AppleUnlinkCommit());
                GUILayout.Space(14);
                if (AccountButton("戻る（連携状態を確認し直す）", button, 80))
                { unlinkReview = null; accountMessage = "必要な操作を選んでください。"; }
            }
            else if (!SaveManager.Instance.RecoveryRequired)
            {
                if (AccountButton("現在のデータをAppleと連携", button)) AppleFlow(AppleSignIn(false));
                GUILayout.Space(10);
                if (AccountButton("Appleに連携済みのデータを探す", button)) AppleFlow(AppleSignIn(true));
                GUILayout.Space(10);
                if (AccountButton("Apple連携を解除する（次に確認）", button)) AppleFlow(AppleUnlinkPreview());
                GUILayout.Space(10);
                if (AccountButton("ゲームアカウントを削除（次に確認）", button, danger: true)) AppleFlow(DeletionPreview());
                GUILayout.Space(10);
                if (AccountButton("閉じる", button, 70)) accountMenu = false;
            }
            else if (AppleAccountEnabled && !System.IO.File.Exists(AppleRecoveryStorage.JournalPath(AccountRoot)))
            {
                if (AccountButton("Appleに連携済みのデータを探す", button)) AppleFlow(AppleSignIn(true));
            }
            GUI.enabled = true;
            if (busy) GUILayout.Label("処理中です…", label);
        }
        private bool HasPendingAccountRecord()
        {
            if (StorageOwnerUnavailable) return true;
            return AppleRecoveryStorage.Blocks(AccountRoot) ||
                AccountDeletionStorage.Blocks(AccountRoot);
        }
    }
}
