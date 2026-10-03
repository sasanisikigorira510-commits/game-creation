using UnityEngine;
using WitchTower.Save;

namespace WitchTower.Managers
{
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public PlayerSaveData CurrentSaveData { get; private set; }

        private StorageStartupContext storageContext;
        private GameObject storageInputBlocker;
#if UNITY_EDITOR
        private string capturedEditorRootOverride;
        internal static bool EditorNamespaceBindingRequiredOverride;
#endif

        // Capture once even when a status getter is the first entry point.
        // In particular, never inspect an account pointer to decide readiness.
        private StorageStartupContext StorageContext
        {
            get
            {
                if (storageContext != null) return storageContext;
                bool development = Debug.isDebugBuild || Application.isEditor;
#if UNITY_EDITOR
                capturedEditorRootOverride = EditorSaveDirectoryOverride;
#endif
                try
                {
                    // Existence is checked before parsing. An empty, malformed,
                    // wrong-type or future-version opt-in is NOT legacy absence.
                    // No activation API is provided until durable binding exists.
                    if (Resources.Load<UnityEngine.Object>("PurchaseNamespaceBootstrap") != null
#if UNITY_EDITOR
                        || EditorNamespaceBindingRequiredOverride
#endif
                        ) return storageContext = StorageStartupContext.BindingRequired(null, development);
                    var resource = Resources.Load<UnityEngine.Object>("PlayerDataService");
                    if (resource != null && !(resource is TextAsset))
                        return storageContext = StorageStartupContext.InvalidConfiguration(development);
                    string raw = (resource as TextAsset)?.text;
                    if (resource != null && (string.IsNullOrWhiteSpace(raw) || JsonUtility.FromJson<AppleAccountConfiguration>(raw) == null))
                        return storageContext = StorageStartupContext.InvalidConfiguration(development);
#if UNITY_IOS && !UNITY_EDITOR && !DEVELOPMENT_BUILD
                    if (JsonUtility.FromJson<AppleAccountConfiguration>(raw)?.IsProduction == true)
                        return storageContext = StorageStartupContext.AwaitReleaseEnvironment(raw);
#endif
                    string root = SaveEnvironmentConfiguration.ResolveRoot(Application.persistentDataPath, raw, development);
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(capturedEditorRootOverride)) root = capturedEditorRootOverride;
#endif
                    return storageContext = StorageStartupContext.PreserveLegacyGameplay(root, raw, development);
                }
                catch (System.Exception)
                {
                    return storageContext = StorageStartupContext.InvalidConfiguration(development);
                }
            }
        }

        public bool StorageAccessAvailable => StorageContext.AccessAvailable
#if UNITY_EDITOR
            && string.Equals(capturedEditorRootOverride, EditorSaveDirectoryOverride, System.StringComparison.Ordinal)
#endif
            ;
        public string StorageStartupMessage => !StorageContext.AccessAvailable ? StorageContext.Message
            : !StorageAccessAvailable ? StorageStartupContext.ChangedMessage : string.Empty;
        internal string PlayerDataConfigurationJson => StorageContext.ConfigurationJson;
        internal bool StorageDevelopment => StorageContext.Development;
        internal string PurchaseEnvironment => StorageContext.PurchaseEnvironment;
        internal bool AwaitingPurchaseEnvironment => StorageContext.AwaitingPurchaseEnvironment;
        internal bool TryBindReleaseEnvironment(WitchTower.Monetization.VerifiedApplePurchaseEnvironment environment)
        {
            var previous = StorageContext;
            if (!previous.AwaitingPurchaseEnvironment || CurrentSaveData != null ||
                (environment != WitchTower.Monetization.VerifiedApplePurchaseEnvironment.Production &&
                 environment != WitchTower.Monetization.VerifiedApplePurchaseEnvironment.Sandbox)) return false;
            try
            {
                string root = Application.persistentDataPath;
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(capturedEditorRootOverride)) root = capturedEditorRootOverride;
#endif
                storageContext = StorageStartupContext.BindReleaseEnvironment(root, previous.ConfigurationJson, environment);
                return true;
            }
            catch (System.Exception)
            {
                storageContext = StorageStartupContext.BindingRequired(previous.ConfigurationJson, false);
                return false;
            }
        }
        internal bool HasPurchaseRealmAuthority => StorageAccessAvailable && StorageContext.HasPurchaseRealmAuthority;

#if UNITY_EDITOR
        // Integration tests can exercise real saves without touching a developer's progress.
        internal static string EditorSaveDirectoryOverride { get; set; }
#endif
        public string RootDirectory
        {
            get
            {
                if (!StorageAccessAvailable) throw new System.InvalidOperationException(StorageStartupMessage);
                return StorageContext.RootDirectory;
            }
        }
        private string SaveFilePath => System.IO.Path.Combine(AppleRecoveryStorage.ActiveDirectory(RootDirectory), "save.json");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Freeze before scene Start. Inactive test fixtures capture lazily.
            _ = StorageContext;
        }

        private void LateUpdate()
        {
            bool blocked = !StorageAccessAvailable;
            if (blocked && storageInputBlocker == null)
            {
                storageInputBlocker = new GameObject("StorageStartupInputBlocker", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                storageInputBlocker.transform.SetParent(transform, false);
                var canvas = storageInputBlocker.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var surface = new GameObject("BlockInput", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                surface.transform.SetParent(storageInputBlocker.transform, false);
                var rect = surface.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                surface.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            }
            if (storageInputBlocker != null) storageInputBlocker.SetActive(blocked);
        }

        public void LoadOrCreate()
        {
            if (!StorageAccessAvailable) return;
            try
            {
                if (AccountDeletionStorage.Blocks(RootDirectory))
                {
                    RecoveryRequired = true;
                    CurrentSaveData = null;
                    RecoveryMessage = "アカウント削除の結果・端末消去を確認してください。";
                    GameManager.Instance?.ForgetDeletedAccount();
                    if (Application.isPlaying) OnlinePlayerData.Ensure().OpenAppleAccount(true);
                    return;
                }
                var pending = AppleRecoveryStorage.Pending(RootDirectory);
                if (pending?.Phase == "committed") AppleRecoveryStorage.Activate(RootDirectory, pending);
                else if (pending != null)
                {
                    RecoveryRequired = true;
                    CurrentSaveData = null;
                    RecoveryMessage = "Appleの引き継ぎ結果を確認してください。";
                    if (Application.isPlaying) OnlinePlayerData.Ensure().OpenAppleAccount(true);
                    return;
                }
                LoadResolvedAccount();
            }
            catch (System.Exception)
            {
                RecoveryRequired = true;
                CurrentSaveData = null;
                RecoveryMessage = "アカウントの保存情報を確認できません。データは保持しています。";
            }
        }

        private void LoadResolvedAccount()
        {
            if (!StorageAccessAvailable) return;
            var repository = new PlayerSaveRepository(SaveFilePath);
            if (repository.TryLoad(out var loaded, out bool blocked, out string error))
            {
                AppleRecoveryStorage.ValidateActivePlayer(RootDirectory, loaded);
                CurrentSaveData = loaded;
                RecoveryRequired = false;
                RecoveryMessage = string.Empty;
                if (Application.isPlaying) OnlinePlayerData.Ensure();
                if (string.IsNullOrEmpty(loaded.PlayerId) && !TrySaveWithReason(loaded, "legacy_migration", out string migrationError))
                {
                    RecoveryRequired = true;
                    RecoveryMessage = migrationError;
                    CurrentSaveData = null;
                }
                return;
            }
            RecoveryRequired = true;
            RecoveryMessage = error;
            CurrentSaveData = null;
            Debug.LogWarning("[Save] Recovery required; existing data retained: " + error);
        }

        public bool RecoveryRequired { get; private set; }
        public string RecoveryMessage { get; private set; }
        public string DataDirectory => System.IO.Path.GetDirectoryName(SaveFilePath);

        private void OnGUI()
        {
            if (!StorageAccessAvailable)
            {
                GUI.depth = -10000;
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), string.Empty);
                float scale = Mathf.Clamp(Screen.width / 540f, .8f, 2.6f);
                var body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20f * scale), wordWrap = true };
                var title = new GUIStyle(body) { fontSize = Mathf.RoundToInt(25f * scale), fontStyle = FontStyle.Bold };
                var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(22f * scale), wordWrap = true };
                GUILayout.BeginArea(new Rect(Screen.width * .08f, Screen.height * .23f, Screen.width * .84f, Screen.height * .65f));
                if (AwaitingPurchaseEnvironment)
                {
                    var probe = WitchTower.Monetization.ApplePurchaseEnvironmentProbe.Instance;
                    GUILayout.Label("App Storeを確認しています", title);
                    GUILayout.Space(18f * scale);
                    GUILayout.Label(probe != null ? probe.StatusMessage : StorageStartupMessage, body);
                    GUILayout.Space(12f * scale);
                    GUILayout.Label("既存のデータは保持しています。確認が完了するとゲームを開始します。", body);
                    GUILayout.Space(24f * scale);
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && probe != null && !probe.IsPending;
                    if (GUILayout.Button("App Storeを再確認", button, GUILayout.Height(64f * scale)))
                        probe.RetryFromUserAction();
                    GUI.enabled = wasEnabled;
                    GUILayout.Space(12f * scale);
                    GUILayout.Label("再確認でApp Storeの認証画面が出る場合があります。", body);
                }
                else
                {
                    // A storage binding failure is separate from StoreKit retry.
                    GUILayout.Label("データの確認が必要です", title);
                    GUILayout.Space(18f * scale);
                    GUILayout.Label(StorageStartupMessage, body);
                }
                GUILayout.EndArea();
                return;
            }
            if (!RecoveryRequired) return;
            if (OnlinePlayerData.Busy) return;
            GUI.depth = -10000;
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(Screen.width * .1f, Screen.height * .3f, Screen.width * .8f, Screen.height * .4f));
            GUILayout.Label("セーブデータの確認が必要です。\nデータを保護するため、ゲームを停止しています。\nアプリを削除せず、サポートへお問い合わせください。");
            if (OnlinePlayerData.AppleAccountEnabled && GUILayout.Button("アカウント管理・結果確認", GUILayout.Height(60)))
                OnlinePlayerData.Ensure().OpenAppleAccount(true);
            if (GUILayout.Button("再読み込み", GUILayout.Height(60)))
            {
                LoadOrCreate();
                if (!RecoveryRequired)
                {
                    GameManager.Instance?.InitializeFromSave(CurrentSaveData);
                    WitchTower.UI.SceneTransitionGuard.LoadScene("HomeScene");
                }
            }
            GUILayout.EndArea();
        }

        public void Save(PlayerSaveData saveData)
        {
            if (!TrySave(saveData, out string error))
            {
                Debug.LogError($"[SaveManager] Save failed: {error}");
            }
        }

        public bool TrySave(PlayerSaveData saveData, out string error)
        {
            return TrySaveWithReason(saveData, "gameplay", out error);
        }

        public bool TrySaveWithReason(PlayerSaveData saveData, string reason, out string error)
        {
            if (!StorageAccessAvailable)
            {
                error = StorageStartupMessage;
                return false;
            }
            if (RecoveryRequired || saveData == null || AccountDeletionStorage.Blocks(RootDirectory) || AppleRecoveryStorage.Blocks(RootDirectory))
            {
                error = "Saving is blocked until recovery is complete.";
                return false;
            }
            // Snapshot callers may share mutable inventory lists with the live profile.
            var staged = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(saveData));
            if (!new PlayerSaveRepository(SaveFilePath).TryCommit(staged, CurrentSaveData, reason, out error)) return false;
            CurrentSaveData = staged;
            if (GameManager.Instance?.PlayerProfile != null)
                GameManager.Instance.PlayerProfile.PlayerId = staged.PlayerId;
            return true;
        }

        public void SaveCurrentGame()
        {
            SaveCurrentGameWithReason("gameplay");
        }

        public void SaveCurrentGameWithReason(string reason)
        {
            if (!StorageAccessAvailable) return;
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            if (!TrySaveWithReason(GameManager.Instance.PlayerProfile.ToSaveData(GameManager.Instance.CurrentFloor), reason, out string error))
                Debug.LogWarning("[Save] Save failed: " + error);
        }

        public void SaveAfterDungeonStageClear(int clearedFloor)
        {
            if (!StorageAccessAvailable) return;
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            GameManager.Instance.PlayerProfile.LastActiveAt = System.DateTime.Now.ToString("O");
            SaveCurrentGameWithReason("battle_clear");
            Debug.Log($"[SaveManager] Auto-saved after dungeon stage clear. clearedFloor={Mathf.Max(1, clearedFloor)}, currentFloor={GameManager.Instance.CurrentFloor}");
        }

        public void SaveForSuspend()
        {
            if (!StorageAccessAvailable) return;
            // A sanctuary practice only borrows a guardian in transient battle
            // state. Even a lifecycle save must leave the live profile and save
            // bytes unchanged; in particular, do not stamp LastActiveAt here.
            // Acquisition/oath trials still preserve their real earned state.
            if (WitchTower.Data.GuardianTrialSession.IsPractice) return;
            if (GameManager.Instance?.PlayerProfile == null)
            {
                return;
            }

            GameManager.Instance.PlayerProfile.LastActiveAt = System.DateTime.Now.ToString("O");
            SaveCurrentGameWithReason("suspend");
        }
    }
}
