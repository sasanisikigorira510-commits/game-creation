using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using WitchTower.Managers;
using WitchTower.Data;

namespace WitchTower.Save
{
    [Serializable] public sealed class OnlineRequest
    {
        public string RequestId;
        public string Kind;
        public string Target;
        public int Count;
        public bool Paid;
        public int Epoch;
        public string TransactionId;
        public string Receipt;
        public long EconomyRevision;
        public string Mode;
        public string RunId;
        public int Stage;
        public string Outcome;
        public string Stat;
        public string[] PartyInstanceIds;
        public DailyChallengeCheckpoint Checkpoint;
    }
    [Serializable] public sealed class OnlineOperation
    {
        public string RequestId;
        public long Revision;
        public string Kind;
        public string Target;
        public int Free;
        public int Paid;
        public int RefundDebt;
        public int GoldDelta;
        public string RelicId;
        public int RelicAmount;
        public int TutorialPulls;
        public string ClaimDate;
        public string TransactionId;
        public string UpgradeField;
        public OwnedMonsterData[] Monsters;
        public OwnedMonsterData[] UpdatedMonsters;
        public int TrainingDrops;
        public int TrialStarCores;
        public int TrainingDropsDelta;
        public int TrialStarCoresDelta;
        public DailyChallengeRun DailyChallengeRun;
        public DailyChallengeState DailyChallenges;
        public MonsterExperienceReward[] MonsterExperienceRewards;
    }
    [Serializable] public sealed class OnlineHead
    {
        public string PlayerId;
        public int Epoch;
        public long EconomyRevision;
        public int Free;
        public int Paid;
        public bool Frozen;
        public bool MigrationRequired;
        public bool PurchasesEnabled;
        public WitchTower.Monetization.PurchaseEnvironmentCapability PurchaseCapability;
        public bool AdRewardsEnabled;
        public int RefundDebt;
        public OnlineOperation[] Operations;
        public PlayerSaveData Recovery;
        public DailyChallengeState DailyChallenges;
        public int TrainingDrops;
        public int TrialStarCores;
    }

    public static class OnlineGrantApplier
    {
        // Apply to a clone, persist, then expose to gameplay. A repeated response is a no-op.
        public static PlayerSaveData Stage(PlayerSaveData source, OnlineOperation operation)
        {
            var save = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(source));
            if (operation.Revision <= save.EconomyRevision) return save;
            if (operation.Revision != save.EconomyRevision + 1) throw new InvalidOperationException("Missing economy operation.");
            if (operation.Free < 0 || operation.Paid < 0 || operation.RefundDebt < 0) throw new InvalidOperationException("Invalid server wallet.");
            save.FreeGachaStones = operation.Free;
            save.PaidGachaStones = operation.Paid;
            save.Gold = checked(save.Gold + operation.GoldDelta);
            ApplyChallengeGrant(save, operation);
            if (operation.Kind == "ad_reward")
            {
                if (string.IsNullOrEmpty(operation.ClaimDate)) throw new InvalidOperationException("Missing reward date.");
                save.DailyClaimedAdRewardIds ??= new List<string>();
                if (save.DailyAdRewardDate != operation.ClaimDate)
                {
                    save.DailyAdRewardDate = operation.ClaimDate;
                    save.DailyClaimedAdRewardIds.Clear();
                }
                if (!save.DailyClaimedAdRewardIds.Contains(operation.Target)) save.DailyClaimedAdRewardIds.Add(operation.Target);
                if (operation.RelicAmount > 0)
                {
                    if (operation.RelicId != "relic_safe_ember") throw new InvalidOperationException("Invalid reward relic.");
                    save.OwnedEnhancementRelics ??= new List<OwnedEnhancementRelicData>();
                    var relic = save.OwnedEnhancementRelics.Find(x => x != null && x.RelicId == operation.RelicId);
                    if (relic == null) save.OwnedEnhancementRelics.Add(new OwnedEnhancementRelicData { RelicId = operation.RelicId, Amount = operation.RelicAmount });
                    else relic.Amount = checked(relic.Amount + operation.RelicAmount);
                }
            }
            if (!string.IsNullOrEmpty(operation.TransactionId) && !save.ProcessedIapTransactionIds.Contains(operation.TransactionId))
                save.ProcessedIapTransactionIds.Add(operation.TransactionId);
            if (operation.Kind == "purchase" && !string.IsNullOrWhiteSpace(operation.TransactionId))
                save.HasRemovedAds = true;
            if (operation.Monsters != null)
                foreach (var monster in operation.Monsters)
                {
                    if (string.IsNullOrEmpty(monster.InstanceId) || string.IsNullOrEmpty(monster.MonsterId))
                        throw new InvalidOperationException("Invalid server monster.");
                    if (save.OwnedMonsters.Any(m => m.InstanceId == monster.InstanceId)) continue;
                    monster.AcquiredOrder = save.OwnedMonsters.Count == 0 ? 1 : save.OwnedMonsters.Max(m => m.AcquiredOrder) + 1;
                    save.OwnedMonsters.Add(monster);
                    var entry = save.MonsterDexEntries.Find(m => m.MonsterId == monster.MonsterId);
                    if (entry == null) { entry = new MonsterDexEntryData { MonsterId = monster.MonsterId }; save.MonsterDexEntries.Add(entry); }
                    entry.IsUnlocked = true;
                    entry.OwnedCount++;
                }
            save.InitialTutorialSummonCount = Math.Max(save.InitialTutorialSummonCount, operation.TutorialPulls);
            if (operation.Kind == "reward")
            {
                if (!string.IsNullOrEmpty(operation.ClaimDate))
                {
                    if (save.DailyQuestProgressDate != operation.ClaimDate)
                    {
                        save.DailyQuestProgressDate = operation.ClaimDate;
                        save.DailyBattleWinCount = 0;
                        save.DailyClaimedQuestIds.Clear();
                    }
                    if (!save.DailyClaimedQuestIds.Contains(operation.Target)) save.DailyClaimedQuestIds.Add(operation.Target);
                    if (operation.Target == "daily_battle_win_1") save.LastDailyRewardDate = operation.ClaimDate;
                }
                if (operation.Target == "tutorial_complete" && !save.SeenTutorialHintIds.Contains("tutorial_completion_reward_free_stones"))
                    save.SeenTutorialHintIds.Add("tutorial_completion_reward_free_stones");
                var mission = save.MissionProgressList.Find(m => m.MissionId == operation.Target);
                if (mission != null) mission.IsClaimed = true;
            }
            switch (operation.UpgradeField)
            {
                case "HasAutoRepeatFloorUpgrade": save.HasAutoRepeatFloorUpgrade = true; save.AutoRepeatFloorUpgradeEnabledState = 1; break;
                case "HasAutoSellEquipmentUpgrade": save.HasAutoSellEquipmentUpgrade = true; save.AutoSellEquipmentUpgradeEnabledState = 1; save.AutoSellEquipmentQualityThreshold = 3; break;
                case "HasAutoReleaseMonsterUpgrade": save.HasAutoReleaseMonsterUpgrade = true; save.AutoReleaseMonsterUpgradeEnabledState = 1; save.AutoReleaseMonsterIndividualValueThreshold = 50; break;
                case "MonsterStorageLimit": save.MonsterStorageLimit = checked(Math.Max(save.MonsterStorageLimit, save.OwnedMonsters.Count) + 20); break;
                case "EquipmentStorageLimit": save.EquipmentStorageLimit = checked(Math.Max(save.EquipmentStorageLimit, save.OwnedEquipments.Count) + 20); break;
            }
            save.EconomyRevision = operation.Revision;
            return save;
        }

        private static void ApplyChallengeGrant(PlayerSaveData save, OnlineOperation operation)
        {
            bool challenge = operation.Kind != null && operation.Kind.StartsWith("daily_challenge_", StringComparison.Ordinal);
            bool training = operation.Kind == "monster_training" || operation.Kind == "monster_skill_training";
            if (challenge || training)
            {
                if (operation.TrainingDrops < 0 || operation.TrialStarCores < 0)
                    throw new InvalidOperationException("Invalid challenge wallet.");
                save.TrainingDrops = operation.TrainingDrops;
                save.TrialStarCores = operation.TrialStarCores;
            }
            if (operation.DailyChallenges != null) save.DailyChallenges = operation.DailyChallenges;
            foreach (var update in operation.UpdatedMonsters ?? Array.Empty<OwnedMonsterData>())
            {
                if (update == null || string.IsNullOrEmpty(update.InstanceId))
                    throw new InvalidOperationException("Missing updated monster identity.");
                var owned = save.OwnedMonsters.Find(m => m != null && m.InstanceId == update.InstanceId);
                if (owned == null || owned.MonsterId != update.MonsterId)
                    throw new InvalidOperationException("Updated monster is not owned.");
                int[] levels = { update.TrainingHp, update.TrainingAttack, update.TrainingWisdom,
                    update.TrainingDefense, update.TrainingMagicDefense, update.TrainingAttackSpeed };
                if (levels.Any(x => x < 0 || x > MonsterTrainingService.MaxTrainingLevel) ||
                    update.MonsterSkillLevel < 1 || update.MonsterSkillLevel > 5)
                    throw new InvalidOperationException("Invalid monster growth grant.");
                owned.TrainingHp = update.TrainingHp;
                owned.TrainingAttack = update.TrainingAttack;
                owned.TrainingWisdom = update.TrainingWisdom;
                owned.TrainingDefense = update.TrainingDefense;
                owned.TrainingMagicDefense = update.TrainingMagicDefense;
                owned.TrainingAttackSpeed = update.TrainingAttackSpeed;
                owned.MonsterSkillLevel = update.MonsterSkillLevel;
            }
            foreach (var reward in operation.MonsterExperienceRewards ?? Array.Empty<MonsterExperienceReward>())
            {
                if (reward == null || string.IsNullOrEmpty(reward.InstanceId) || reward.Amount < 0)
                    throw new InvalidOperationException("Invalid challenge experience grant.");
                var owned = save.OwnedMonsters.Find(m => m != null && m.InstanceId == reward.InstanceId);
                if (owned == null) throw new InvalidOperationException("Experience recipient is not owned.");
                var data = MasterDataManager.Instance?.GetMonsterData(owned.MonsterId);
                if (data == null) throw new InvalidOperationException("Monster data is unavailable for experience grant.");
                MonsterLevelService.AddExperience(owned, data, reward.Amount);
            }
        }
    }

    public sealed partial class OnlinePlayerData : MonoBehaviour
    {
        [Serializable] private sealed class Credentials { public string PlayerId; public string Token; public bool Legacy; }
        public static OnlinePlayerData Instance { get; private set; }
#if UNITY_EDITOR
        // UI tests inject already-applied, authenticated deliveries. Player builds
        // contain no transport override. Real HTTP coverage lives in integration tests.
        internal static Action<string, Action<string, string>> EditorExecuteOverride;
#endif
        private static bool StorageUnavailable => SaveManager.Instance == null || !SaveManager.Instance.StorageAccessAvailable;
        private bool StorageOwnerUnavailable => StorageUnavailable ||
            (!ReferenceEquals(configuredStorageOwner, null) && configuredStorageOwner != SaveManager.Instance);
        private static string StorageBlockedMessage => SaveManager.Instance?.StorageAccessAvailable == false
            ? SaveManager.Instance.StorageStartupMessage : "起動時の保存先を確認できません。データは保持しています。";
        private static bool DeletionBlocked => !StorageUnavailable && AccountDeletionStorage.Blocks(SaveManager.Instance.RootDirectory);
        public static bool Busy => StorageUnavailable || Instance?.StorageOwnerUnavailable == true || DeletionBlocked || (Instance != null && (Instance.busy || Instance.accountMenu || Instance.rewardedAdReserved));
        private bool rewardedAdReserved;
        private bool adRewardsEnabled;
        public static bool AdRewardsAvailable => !StorageUnavailable && Instance != null && Instance.adRewardsEnabled;
        public static string LastMessage { get; private set; } = string.Empty;
        public static string ContractUnavailableMessage
        {
            get
            {
                if (StorageUnavailable || Instance?.StorageOwnerUnavailable == true) return StorageBlockedMessage;
#if UNITY_EDITOR
                if (EditorExecuteOverride != null) return string.Empty;
#endif
                if (DeletionBlocked) return "アカウント削除の確認中です。";
                if (Instance != null && Instance.pendingRecovery != null) return "復旧データの反映が必要です。";
                if (Busy) return "サーバーでデータを確認中です。";
                return Instance == null || Instance.credentials == null
                    ? "サーバーとの接続を確認中です。"
                    : Instance.contractUnavailableMessage;
            }
        }
        private string contractUnavailableMessage = "サーバーとの接続を確認中です。";
        public static bool CanStartPurchase => !Busy && Instance != null && Instance.purchaseReady && Time.realtimeSinceStartup - Instance.lastContact < 30
#if UNITY_IOS && !UNITY_EDITOR
            && string.IsNullOrEmpty(CheckoutEnvironmentBlockedMessage(WitchTower.Monetization.ApplePurchaseEnvironmentProbe.Instance?.Environment
                ?? WitchTower.Monetization.VerifiedApplePurchaseEnvironment.Unknown))
#endif
            ;
        public static string CheckoutEnvironmentBlockedMessage(WitchTower.Monetization.VerifiedApplePurchaseEnvironment environment)
        {
            string realm = SaveManager.Instance?.HasPurchaseRealmAuthority == true
                ? SaveManager.Instance.PurchaseEnvironment : "Unknown";
            var live = GameManager.Instance?.PlayerProfile;
            var capability = !StorageUnavailable && SaveManager.Instance.HasPurchaseRealmAuthority &&
                Instance != null && live != null && Guid.TryParseExact(live.PlayerId, "N", out _) &&
                live.PlayerId == Instance.purchaseCapabilityPlayer &&
                live.RecoveryEpoch == Instance.purchaseCapabilityEpoch ? Instance.purchaseCapability : null;
            return WitchTower.Monetization.IapPurchaseEnvironmentPolicy.VerifiedEnvironmentBlockedMessage(environment, capability, realm);
        }
        public static string PaidSpendingUnavailableMessage => Instance != null && Instance.refundDebt > 0
            ? $"返金に伴う有償宝晶の不足 {Instance.refundDebt:N0}個を確認中です。有償宝晶の利用のみ停止しています。お問い合わせください。"
            : string.Empty;
        private int refundDebt;
        private bool busy;
        private bool purchaseReady;
        private WitchTower.Monetization.PurchaseEnvironmentCapability purchaseCapability;
        private string purchaseCapabilityPlayer;
        private int purchaseCapabilityEpoch;
        private void InvalidatePurchaseCapability()
        {
            purchaseCapability = null;
            purchaseCapabilityPlayer = null;
            purchaseCapabilityEpoch = 0;
        }
        private float lastContact;
        private float nextSync;
        private float nextReconnectAllowedAt;
        private float retryNotBefore;
        private NetworkReachability observedReachability;
        private string lastSyncFailure;
        private string failureNoticeContext;
        private string lastFailureNoticeKey;
        private string dismissedFailureNoticeKey;
        private string lastRefundNotice;
        private string baseUrl;
        private string qaAccessKey;
        private Credentials credentials;
        private Credentials registrationResolvedFor;
        private string response;
        private long status;
        private string failure;
        private PlayerSaveData pendingRecovery;
        private GameObject inputBlocker;
        private string PendingPath => Path.Combine(SaveManager.Instance.DataDirectory, "online-request.json");
        private SaveManager configuredStorageOwner;

        public static OnlinePlayerData Ensure()
        {
            if (Instance != null) return Instance;
            var root = new GameObject("OnlinePlayerData");
            return root.AddComponent<OnlinePlayerData>();
        }
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureFromStorageContext();
        }
        private void ConfigureFromStorageContext()
        {
            if (!ReferenceEquals(configuredStorageOwner, null) || StorageUnavailable) return;
            configuredStorageOwner = SaveManager.Instance;
            string raw = configuredStorageOwner.PlayerDataConfigurationJson;
            var configuration = raw != null ? JsonUtility.FromJson<AppleAccountConfiguration>(raw) : null;
            baseUrl = configuration?.BaseUrl?.TrimEnd('/');
            // Invalid public opt-ins must not send credentials to a QA or substituted endpoint.
            if (configuration?.ReleaseAppleLinking == true && !configuration.IsProduction &&
                !(configuredStorageOwner.HasPurchaseRealmAuthority && configuration.IsReviewSandbox &&
                  configuredStorageOwner.PurchaseEnvironment == "Sandbox"))
            { baseUrl = null; return; }
            if (RefundSandboxConfiguration.IsConfiguredFrom(raw))
            {
                try { RefundSandboxConfiguration.Validate(raw, configuredStorageOwner.StorageDevelopment,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true); }
                catch { baseUrl = null; return; }
            }
            if (!string.IsNullOrEmpty(configuration?.QaAccessKey))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ValidQaKey(configuration.QaAccessKey) && configuration.ExperimentalAppleLinking)
                    qaAccessKey = configuration.QaAccessKey;
                else { baseUrl = null; return; }
#else
                // A copied QA resource must never contact a server in release.
                baseUrl = null; return;
#endif
            }
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "http://127.0.0.1:8787";
#endif
            if (!string.IsNullOrEmpty(baseUrl) && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && !(Application.isEditor && uri.IsLoopback && uri.Scheme == "http")))) baseUrl = null;
        }
        private static bool ValidQaKey(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        private void Update()
        {
            if (StorageUnavailable) return;
            if (ReferenceEquals(configuredStorageOwner, null) && baseUrl == null) ConfigureFromStorageContext();
            if (!ReferenceEquals(configuredStorageOwner, null) && configuredStorageOwner != SaveManager.Instance) return;
            if (observedReachability != Application.internetReachability)
            {
                observedReachability = Application.internetReachability;
                RequestConnectionRefresh();
            }
            if (DeletionBlocked || busy || rewardedAdReserved || accountMenu || pendingRecovery != null || Time.realtimeSinceStartup < Mathf.Max(nextSync, retryNotBefore) ||
                SaveManager.Instance?.CurrentSaveData == null || GameManager.Instance?.PlayerProfile == null) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != "HomeScene" && scene != "GachaScene") return;
            nextSync = Time.realtimeSinceStartup + 60;
            nextReconnectAllowedAt = Time.realtimeSinceStartup + 5;
            // No automatic gameplay transactions other than already requested delivery.
            StartCoroutine(Run(null, null));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) RequestConnectionRefresh();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) RequestConnectionRefresh();
        }

        public void RequestConnectionRefresh()
        {
            // Reachability/focus are hints only. Only an authenticated sync can
            // clear an error or re-enable transactions. Debounce repeated hints.
            float now = Time.realtimeSinceStartup;
            if (busy || (string.IsNullOrEmpty(contractUnavailableMessage) && now - lastContact < 15)) return;
            nextSync = Mathf.Max(retryNotBefore, Mathf.Min(nextSync, Mathf.Max(now, nextReconnectAllowedAt)));
        }

        private void LateUpdate()
        {
            bool block = StorageOwnerUnavailable || DeletionBlocked || busy || accountMenu || pendingRecovery != null;
            if (block && inputBlocker == null)
            {
                inputBlocker = new GameObject("OnlineInputBlocker", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                inputBlocker.transform.SetParent(transform, false);
                var canvas = inputBlocker.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var surface = new GameObject("BlockInput", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                surface.transform.SetParent(inputBlocker.transform, false);
                var rect = surface.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                surface.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .35f);
            }
            if (inputBlocker != null) inputBlocker.SetActive(block);
        }

        public void Execute(OnlineRequest request, Action<OnlineOperation, string> completed)
        {
            if (StorageOwnerUnavailable) { completed?.Invoke(null, StorageBlockedMessage); return; }
            if (DeletionBlocked) { completed?.Invoke(null, "アカウント削除の確認中です。"); return; }
#if UNITY_EDITOR
            if (EditorExecuteOverride != null)
            {
                EditorExecuteOverride(JsonUtility.ToJson(request), (json, error) => completed?.Invoke(
                    string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<OnlineOperation>(json), error));
                return;
            }
#endif
            if (busy || rewardedAdReserved || accountMenu || pendingRecovery != null) { completed?.Invoke(null, "データを確認中です。しばらくお待ちください。"); return; }
            StartCoroutine(Run(request, completed));
        }
        public static void ClaimReward(string target, Action<bool> completed = null)
        {
            Ensure().Execute(new OnlineRequest { Kind = "reward", Target = target }, (op, error) =>
            {
                // Successful claims are visible in the wallet/claimed state,
                // without an extra notice covering the game screen.
                if (error != null) LastMessage = error;
                completed?.Invoke(op != null && error == null);
                UnityEngine.Object.FindFirstObjectByType<WitchTower.Home.HomeSceneController>()?.RefreshAllPanels();
            });
        }
        private bool Prepare()
        {
            failure = null;
            if (StorageUnavailable || (!ReferenceEquals(configuredStorageOwner, null) && configuredStorageOwner != SaveManager.Instance))
            { failure = StorageUnavailable ? StorageBlockedMessage : "起動時の保存先と一致しません。"; return false; }
            if (ReferenceEquals(configuredStorageOwner, null) && baseUrl == null) ConfigureFromStorageContext();
            if (Time.realtimeSinceStartup < retryNotBefore) { failure = ConnectionFailureMessage(429, null); return false; }
            if (DeletionBlocked) { failure = "アカウント削除の確認中です。"; return false; }
            if (string.IsNullOrEmpty(baseUrl)) { failure = "オンライン機能の接続先が未設定です。"; return false; }
            var save = SaveManager.Instance?.CurrentSaveData;
            if (save == null || SaveManager.Instance.RecoveryRequired) { failure = "セーブデータの復旧が必要です。"; return false; }
            try
            {
                string path = Path.Combine(SaveManager.Instance.DataDirectory, "online-identity.json");
                if (credentials == null)
                {
                    if (File.Exists(path)) credentials = JsonUtility.FromJson<Credentials>(File.ReadAllText(path));
                    else
                    {
                        credentials = new Credentials { PlayerId = save.PlayerId, Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                            Legacy = save.EconomyRevision == 0 && (save.InitialTutorialSummonCount > 0 || save.PaidGachaStones > 0 ||
                                save.ProcessedIapTransactionIds.Count > 0 || save.FreeGachaStones != 900 || save.HasCompletedTutorial) };
                        WriteAtomic(path, JsonUtility.ToJson(credentials));
                    }
                }
                if (credentials == null || credentials.PlayerId != save.PlayerId || string.IsNullOrEmpty(credentials.Token))
                    throw new InvalidOperationException("Credential mismatch");
                return true;
            }
            catch (Exception) { failure = "接続用データを保存・読込できませんでした。アプリを削除せずお問い合わせください。"; return false; }
        }

        private IEnumerator Run(OnlineRequest requested, Action<OnlineOperation, string> completed)
        {
            OnlineOperation delivered = null;
            string message = null;
            var routine = RunCore(requested, (op, error) => { delivered = op; message = error; });
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e)
                {
                    busy = false;
                    purchaseReady = false;
                    InvalidatePurchaseCapability();
                    message = "オンライン処理を保存・確認できませんでした。再接続すると再試行します。";
                    CompleteSyncStatus(message, false);
                    Debug.LogWarning("[Online] Deferred operation: " + e.GetType().Name);
                    delivered = null;
                    more = false;
                }
                if (!more) break;
                yield return routine.Current;
            }
            completed?.Invoke(delivered, message);
        }

        private IEnumerator RunCore(OnlineRequest requested, Action<OnlineOperation, string> completed)
        {
            if (StorageOwnerUnavailable) { completed?.Invoke(null, StorageBlockedMessage); yield break; }
            busy = true;
            InvalidatePurchaseCapability();
            failureNoticeContext = string.Empty;
            OnlineOperation delivered = null;
            try
            {
                ResumeCompletedPurchaseIsolation();
                if (!Prepare()) yield break;
                // Refresh before upload: a restore increments Epoch and fences out old devices.
                OnlineHead head;
                do
                {
                    yield return Send("GET", PlayerPath("head?after=" + GameManager.Instance.PlayerProfile.EconomyRevision), null);
                    // Existing installations authenticate directly. Registration has
                    // a strict anti-abuse quota and must not run on every refresh.
                    // A new identity also receives 401; only then try registration.
                    // Rejected/rotated credentials are never replaced or regenerated.
                    if (status == 401 && registrationResolvedFor != credentials)
                    {
                        yield return Send("POST", "/v1/accounts", JsonUtility.ToJson(credentials), false);
                        if (failure == null || status == 403 || status == 410) registrationResolvedFor = credentials;
                        if (failure != null) yield break;
                        yield return Send("GET", PlayerPath("head?after=" + GameManager.Instance.PlayerProfile.EconomyRevision), null);
                    }
                    if (failure != null) yield break;
                    registrationResolvedFor = credentials;
                    head = JsonUtility.FromJson<OnlineHead>(response);
                    var live = GameManager.Instance.PlayerProfile;
                    if (head.PlayerId != live.PlayerId) { failure = "ユーザー情報が一致しません。"; yield break; }
                    if (head.Epoch != live.RecoveryEpoch)
                    {
                        if (head.Epoch > live.RecoveryEpoch && head.Recovery != null) pendingRecovery = head.Recovery;
                        failure = "復旧データの反映が必要です。";
                        yield break;
                    }
                    if (head.Operations != null)
                        foreach (var op in head.Operations)
                        {
                            if (!Apply(op)) yield break;
                        }
                } while (head.Operations != null && head.Operations.Length == 100);
                if (head.RefundDebt < 0) { failure = "返金情報を確認できませんでした。"; yield break; }
                refundDebt = head.RefundDebt;
                purchaseReady = head.PurchasesEnabled && !head.MigrationRequired && !head.Frozen;
                purchaseCapability = head.PurchaseCapability;
                purchaseCapabilityPlayer = head.PlayerId;
                purchaseCapabilityEpoch = head.Epoch;
                adRewardsEnabled = head.AdRewardsEnabled && !head.MigrationRequired && !head.Frozen;
                lastContact = Time.realtimeSinceStartup;
                if (head.DailyChallenges != null)
                {
                    if (head.TrainingDrops < 0 || head.TrialStarCores < 0)
                    { failure = "専用素材の所持数を確認できませんでした。"; yield break; }
                    dailyChallengeState = head.DailyChallenges;
                    GameManager.Instance.PlayerProfile.DailyChallenges = head.DailyChallenges;
                    GameManager.Instance.PlayerProfile.TrainingDrops = head.TrainingDrops;
                    GameManager.Instance.PlayerProfile.TrialStarCores = head.TrialStarCores;
                }
                if (!SaveManager.Instance.TrySaveWithReason(GameManager.Instance.PlayerProfile.ToSaveData(GameManager.Instance.CurrentFloor), "cloud_checkpoint", out _))
                { failure = "端末に保存できないため、オンライン取引を開始できません。"; yield break; }
                yield return Send("POST", PlayerPath("snapshots"), JsonUtility.ToJson(SaveManager.Instance.CurrentSaveData));
                if (failure != null) yield break;
                if (!ApplyTrainingSnapshotAcknowledgement()) yield break;
                if (head.Frozen || head.MigrationRequired)
                { failure = "オンライン取引はデータ確認待ちです。通常の探索は続けられます。"; yield break; }
                OnlineRequest action = requested;
                bool resumingPending = File.Exists(PendingPath);
                if (resumingPending) action = JsonUtility.FromJson<OnlineRequest>(File.ReadAllText(PendingPath));
                if (IsIsolatedPurchaseRequest(action))
                {
                    failureNoticeContext = action.RequestId;
                    failure = "テスト購入の隔離復旧を確認中です。購入情報は保持しています。再購入しないでください。";
                    yield break;
                }
                if (action == null && GameManager.Instance.PlayerProfile.HasCompletedTutorial &&
                    !GameManager.Instance.PlayerProfile.SeenTutorialHintIds.Contains("tutorial_completion_reward_free_stones"))
                    action = new OnlineRequest { Kind = "reward", Target = "tutorial_complete" };
                if (action == null) yield break;
                if (string.IsNullOrEmpty(action.RequestId)) action.RequestId = Guid.NewGuid().ToString("N");
                failureNoticeContext = action.RequestId;
                action.Epoch = head.Epoch;
                if (IsDailyChallengeRequest(action) && !resumingPending) action.EconomyRevision = head.EconomyRevision;
                WriteAtomic(PendingPath, JsonUtility.ToJson(action));
                yield return Send("POST", PlayerPath("operations"), JsonUtility.ToJson(action));
                if (failure != null)
                {
                    failure = OperationFailureMessage(status, PlayerPath("operations"), response, action.Kind);
                    if (status >= 400 && status < 500 && status != 408 && status != 429) File.Delete(PendingPath);
                    yield break;
                }
                delivered = JsonUtility.FromJson<OnlineOperation>(response);
                if (!Apply(delivered)) { delivered = null; yield break; }
                File.Delete(PendingPath);
                // Persist the result remotely too, so a rollback can select the new economy cursor.
                yield return Send("POST", PlayerPath("snapshots"), JsonUtility.ToJson(SaveManager.Instance.CurrentSaveData));
                // A secondary snapshot network failure does not undo an already
                // durable transaction. A storage-owner change is different: do
                // not hand an old operation to a callback using the new profile.
                if (StorageOwnerUnavailable)
                { delivered = null; failure = StorageBlockedMessage; yield break; }
                if (failure == null) ApplyTrainingSnapshotAcknowledgement();
                failure = null; // The transaction is already durable locally and on the server.
                if (requested != null && (action.Kind != requested.Kind || action.Target != requested.Target || action.Count != requested.Count || action.Paid != requested.Paid || action.TransactionId != requested.TransactionId || action.RunId != requested.RunId || action.Mode != requested.Mode || action.Stat != requested.Stat || action.Stage != requested.Stage || action.Outcome != requested.Outcome ||
                    (IsDailyChallengeRequest(action) && !SameDailyCheckpoint(action.Checkpoint, requested.Checkpoint))))
                { delivered = null; failure = "前回の取引を反映しました。今回の操作はもう一度選んでください。"; }
            }
            finally
            {
                busy = false;
                CompleteOperationStatus(failure, delivered);
                completed?.Invoke(delivered, failure);
            }
        }

        private static bool IsIsolatedPurchaseRequest(OnlineRequest request) => request != null &&
            request.Kind == "purchase" && PurchaseIsolationRecoveryStorage.Matches(
                PurchaseIsolationRecoveryStorage.RuntimeRoot, request.Target, request.TransactionId);

        private static void ResumeCompletedPurchaseIsolation()
        {
            string root = PurchaseIsolationRecoveryStorage.RuntimeRoot;
            var journal = PurchaseIsolationRecoveryStorage.Read(root);
            if (journal != null && !journal.SourcePendingCleared && PurchaseIsolationRecoveryStorage.CanConfirm(
                root, journal.Request.Target, journal.Request.TransactionId))
                PurchaseIsolationRecoveryStorage.ClearOriginalPending(root);
        }

        private void CompleteOperationStatus(string error, OnlineOperation delivered)
        {
            // Also applies to durable rewards/purchases replayed after reconnect/restart.
            // Do not suppress connection/save errors or refund safety warnings.
            bool quietReward = delivered != null && (delivered.Kind == "reward" || delivered.Kind == "ad_reward");
            bool quietPurchase = delivered != null && (delivered.Kind == "purchase" || delivered.Kind == "upgrade");
            bool quietChallenge = delivered != null && IsDailyChallengeRequest(delivered.Kind);
            CompleteSyncStatus(error, delivered != null && !quietReward && !quietPurchase && !quietChallenge);
            // Purchase screens already show the result and any upgrade instructions.
            // Clear a generic success notice left by an earlier transaction too.
            if ((quietPurchase || quietChallenge) && error == null && LastMessage == "データを反映しました。")
                LastMessage = string.Empty;
        }

        private void CompleteSyncStatus(string error, bool delivered)
        {
            contractUnavailableMessage = error ?? string.Empty;
            if (error != null)
            {
                // Closing a notice does not cancel delivery or enable spending.
                // Identical retries for the same account/request stay quiet;
                // changed failures and new transactions still receive a notice.
                lastSyncFailure = error;
                lastFailureNoticeKey = (credentials?.PlayerId ?? string.Empty) + "\n" +
                    failureNoticeContext + "\n" + error;
                if (lastFailureNoticeKey != dismissedFailureNoticeKey) LastMessage = error;
                purchaseReady = false;
                InvalidatePurchaseCapability();
                nextSync = Mathf.Max(Time.realtimeSinceStartup + 15, retryNotBefore);
            }
            else
            {
                // A no-op refresh is also a successful recovery. Do not leave a
                // stale error until the user spends currency; preserve unrelated notices.
                if (refundDebt > 0) lastRefundNotice = LastMessage = PaidSpendingUnavailableMessage;
                else if (delivered) LastMessage = "データを反映しました。";
                else if (LastMessage == lastSyncFailure || LastMessage == lastRefundNotice) LastMessage = string.Empty;
                if (refundDebt == 0) lastRefundNotice = null;
                lastSyncFailure = null;
                lastFailureNoticeKey = dismissedFailureNoticeKey = null;
            }
        }

        private bool Apply(OnlineOperation operation)
        {
            if (StorageOwnerUnavailable) { failure = StorageBlockedMessage; return false; }
            try
            {
                var game = GameManager.Instance;
                if (operation.Revision <= game.PlayerProfile.EconomyRevision) return true;
                var staged = OnlineGrantApplier.Stage(game.PlayerProfile.ToSaveData(game.CurrentFloor), operation);
                if (!SaveManager.Instance.TrySaveWithReason(staged, "online_" + operation.Kind + ":" + operation.RequestId, out string error))
                { failure = "取引は確定済みですが端末への保存を再試行する必要があります。"; return false; }
                game.InitializeFromSave(SaveManager.Instance.CurrentSaveData);
                if (operation.DailyChallenges != null) dailyChallengeState = operation.DailyChallenges;
                refundDebt = operation.RefundDebt;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Online] Apply deferred: " + e.Message); failure = "取引の反映を再試行します。"; return false; }
        }
        private string PlayerPath(string resource) => "/v1/players/" + credentials.PlayerId + "/" + resource;
        private IEnumerator Send(string method, string path, string body, bool authenticated = true, string bearerOverride = null)
        {
            failure = null;
            status = 0;
            if (StorageUnavailable || (!ReferenceEquals(configuredStorageOwner, null) && configuredStorageOwner != SaveManager.Instance))
            { failure = StorageBlockedMessage; yield break; }
            var requestStorageOwner = SaveManager.Instance;
#if UNITY_EDITOR
            // A private save fixture must never register synthetic identities or
            // spend against a packaged live endpoint. Integration tests use a
            // literal loopback server; normal Editor sessions have no override.
            if (!string.IsNullOrEmpty(SaveManager.EditorSaveDirectoryOverride) &&
                (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var fixtureUri) ||
                 !System.Net.IPAddress.TryParse(fixtureUri.Host.Trim('[', ']'), out var fixtureAddress) ||
                 !System.Net.IPAddress.IsLoopback(fixtureAddress)))
            { failure = "私有テスト保存先ではローカル検証サーバーのみ利用できます。"; yield break; }
#endif
            // A frozen endpoint is not permission to use an expired legacy QA
            // gate. Revalidate time only; never recapture or select another root.
            if (!ReferenceEquals(configuredStorageOwner, null) && RefundSandboxConfiguration.IsConfiguredFrom(configuredStorageOwner.PlayerDataConfigurationJson))
            {
                bool active;
                try { active = RefundSandboxConfiguration.Validate(configuredStorageOwner.PlayerDataConfigurationJson,
                    configuredStorageOwner.StorageDevelopment, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true); }
                catch { active = false; }
                if (!active) { failure = "返金テスト設定が無効または期限切れです。"; yield break; }
            }
            using var request = new UnityWebRequest(baseUrl + path, method);
            // Never forward gate or player credentials to a redirect target.
            request.redirectLimit = 0;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); request.SetRequestHeader("Content-Type", "application/json"); }
            if (authenticated || bearerOverride != null) request.SetRequestHeader("Authorization", "Bearer " + (bearerOverride ?? credentials.Token));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(qaAccessKey))
            {
                string playerAuth = authenticated || bearerOverride != null ? " Bearer " + (bearerOverride ?? credentials.Token) : "";
                request.SetRequestHeader("Authorization", "NasusQA " + qaAccessKey + playerAuth);
            }
#endif
            request.timeout = 12;
            yield return request.SendWebRequest();
            if (StorageOwnerUnavailable || requestStorageOwner != SaveManager.Instance)
            { failure = StorageBlockedMessage; yield break; }
            status = request.responseCode;
            response = request.downloadHandler.text;
            RecordConnectionDiagnostic(method, path, status, request.result.ToString());
            if (status == 429)
                retryNotBefore = Time.realtimeSinceStartup + RetryDelaySeconds(request.GetResponseHeader("Retry-After"));
            if (request.result != UnityWebRequest.Result.Success)
                failure = ConnectionFailureMessage(status, path);
        }
        private static float RetryDelaySeconds(string value)
        {
            return int.TryParse(value, out int seconds) ? Mathf.Clamp(seconds, 1, 3600) : 60;
        }
        private static string ConnectionFailureMessage(long code, string path)
        {
            // Registration returns 403 when this installation's credential no longer
            // matches the existing player; other 403s are not proof of a transfer.
            return code == 401 || (code == 403 && path == "/v1/accounts") ? "この端末の認証が無効です。設定のアカウント管理をご確認ください。" :
                code == 403 ? "この操作は許可されていません。設定のアカウント管理をご確認ください。" :
                code == 402 ? "返金に伴う不足分を確認中です。有償宝晶の利用はお問い合わせください。" :
                code == 409 ? "データの更新・残高・受取条件を確認できませんでした。再接続してお試しください。" :
                code == 423 ? "このデータは運営による確認待ちです。" :
                code == 429 ? "通信の回数制限に達しました。しばらく待つと自動で再接続します。" :
                code >= 500 ? "サーバーの処理を完了できませんでした。データは保持し、自動で再試行します。" :
                "オンライン接続が必要です。通信状態をご確認ください。";
        }

        [Serializable] private sealed class OperationFailure { public string ErrorCode; }
        private static string OperationFailureMessage(long code, string path, string body, string kind)
        {
            if (code != 503 || kind != "purchase" || path == null ||
                !path.StartsWith("/v1/players/", StringComparison.Ordinal) ||
                !path.EndsWith("/operations", StringComparison.Ordinal))
                return ConnectionFailureMessage(code, path);
            // Only recognize the server's fixed codes in a failed purchase response.
            // Never display arbitrary response text, decode receipts, or grant here.
            string errorCode = null;
            if (!string.IsNullOrEmpty(body) && body.Length <= 4096)
            {
                try { errorCode = JsonUtility.FromJson<OperationFailure>(body)?.ErrorCode; }
                catch (ArgumentException) { }
            }
            if (errorCode == "PURCHASE_ENVIRONMENT_MISMATCH")
                return "テスト購入と接続先の環境が一致しません。購入情報は保持しています。再購入せず、サポートへお問い合わせください。";
            if (errorCode == "PURCHASE_VERIFICATION_REJECTED")
                return "購入情報を確認できず、反映を保留しています。再購入せず、サポートへお問い合わせください。";
            // Older servers only return Error. Preserve their pending purchase too.
            return "購入の確認・反映を保留しています。購入情報は保持し、自動で再試行します。再購入はしないでください。";
        }
        public static void WriteAtomic(string path, string value)
        {
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        public bool ApplyPendingRecovery()
        {
            if (StorageOwnerUnavailable) return false;
            if (DeletionBlocked) return false;
            var saves = SaveManager.Instance;
            var game = GameManager.Instance;
            if (pendingRecovery == null || pendingRecovery.PlayerId != saves.CurrentSaveData.PlayerId ||
                pendingRecovery.RecoveryEpoch <= game.PlayerProfile.RecoveryEpoch) return false;
            if (!saves.TrySaveWithReason(game.PlayerProfile.ToSaveData(game.CurrentFloor), "before_operator_restore", out _)) return false;
            if (!saves.TrySaveWithReason(pendingRecovery, "operator_restore", out _)) return false;
            game.InitializeFromSave(saves.CurrentSaveData);
            pendingRecovery = null;
            if (File.Exists(PendingPath)) File.Delete(PendingPath);
            WitchTower.Home.TenPullPresentationJournal.Clear();
            return true;
        }

        private void OnGUI()
        {
            if (StorageOwnerUnavailable) return;
            if (DrawAppleAccount()) return;
            if (pendingRecovery != null)
            {
                GUI.depth = -10001;
                GUI.ModalWindow(GetInstanceID(), new Rect(Screen.width * .1f, Screen.height * .3f, Screen.width * .8f, 240), windowId =>
                {
                    GUILayout.Label("運営による復旧データがあります。現在の状態を履歴に残し、復旧データを反映します。");
                    if (GUILayout.Button("復旧データを反映してホームへ", GUILayout.Height(60)))
                    {
                        if (ApplyPendingRecovery()) WitchTower.UI.SceneTransitionGuard.LoadScene("HomeScene");
                    }
                }, "データの復旧");
            }
            else if (busy)
            {
                GUI.depth = -10000;
                GUI.ModalWindow(GetInstanceID(), new Rect(Screen.width * .2f, Screen.height * .4f, Screen.width * .6f, 120), _ => GUILayout.Label("オンラインデータを確認しています…"), "通信中");
            }
            else if (!string.IsNullOrEmpty(LastMessage))
            {
                GUI.depth = -9999;
                DrawStatusNotification();
            }
        }

        private Rect ResolveStatusMessageRect()
        {
            var preferred = StatusNotificationBounds(Screen.width, Screen.height, Screen.safeArea, statusNotificationHeight);
            // IMGUI notifications render above Canvas graphics. Raising a tutorial
            // highlight's sibling index cannot uncover a button underneath this toast.
            foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (!button.isActiveAndEnabled || button.transform.Find("HomeReturnLabel") == null) continue;
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera,
                    rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin, 0)));
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera,
                    rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.yMax, 0)));
                var buttonBounds = Rect.MinMaxRect(bottomLeft.x, Screen.height - topRight.y,
                    topRight.x, Screen.height - bottomLeft.y);
                preferred = AvoidReturnButton(preferred, buttonBounds, Mathf.Max(12f, Screen.width * .02f));
            }
            return preferred;
        }

        private static Rect AvoidReturnButton(Rect message, Rect button, float gap)
        {
            // Include the blinking border and its prompt below the button. Keep
            // notification dismissal separate from the tutorial's navigation tap.
            var protectedArea = Rect.MinMaxRect(button.xMin - gap, button.yMin - gap,
                button.xMax + gap, button.yMax + gap * 3f);
            if (!message.Overlaps(protectedArea)) return message;
            // Keep the full readable width and horizontal center. Never squeeze
            // the notification beside a navigation button.
            return new Rect(message.x, protectedArea.yMax, message.width, message.height);
        }
    }
}
