using System;
using System.IO;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
        public bool TryReserveRewardedAd(out string error)
        {
            error = ContractUnavailableMessage;
            if (!string.IsNullOrEmpty(error)) return false;
            if (!AdRewardsAvailable) { error = "毎日のプレゼントは準備中です。"; return false; }
            if (SaveManager.Instance == null || GameManager.Instance?.PlayerProfile == null || File.Exists(PendingPath))
            { error = "前回の報酬を確認中です。しばらくお待ちください。"; return false; }
            if (!SaveManager.Instance.TrySaveWithReason(GameManager.Instance.PlayerProfile.ToSaveData(GameManager.Instance.CurrentFloor),
                    "before_rewarded_ad", out _))
            { error = "データを保存できませんでした。空き容量を確認してください。"; return false; }
            rewardedAdReserved = true;
            return true;
        }

        public bool QueueEarnedAdReward(string playerId, string target, out string error)
        {
            error = null;
            if (StorageOwnerUnavailable) { error = StorageBlockedMessage; return false; }
            if (!rewardedAdReserved || GameManager.Instance?.PlayerProfile?.PlayerId != playerId || DeletionBlocked ||
                string.IsNullOrEmpty(WitchTower.Home.DailyAdRewardCatalog.Label(target)))
            { error = "報酬の受取先を確認できませんでした。"; return false; }
            try
            {
                if (File.Exists(PendingPath)) throw new IOException("A delivery is already pending.");
                // Persist before leaving the ad. Existing online replay delivers this once after reconnect/restart.
                WriteAtomic(PendingPath, JsonUtility.ToJson(new OnlineRequest
                {
                    RequestId = Guid.NewGuid().ToString("N"), Kind = "ad_reward", Target = target,
                    Epoch = GameManager.Instance.PlayerProfile.RecoveryEpoch
                }));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] Reward delivery could not be saved: " + e.GetType().Name);
                error = "報酬を保存できませんでした。空き容量を確認してください。";
                return false;
            }
        }

        public void CompleteRewardedAd(bool earned, Action<OnlineOperation, string> completed)
        {
            rewardedAdReserved = false;
            if (earned) Execute(null, completed);
            else completed?.Invoke(null, null);
        }
    }
}
