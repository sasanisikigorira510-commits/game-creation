using System;
using System.Globalization;
using UnityEngine;
using WitchTower.Data;

namespace WitchTower.Monetization
{
    public static class StoreReviewService
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LastRequestKey = "witchtower_review_last_request_utc";
        private const string VersionKey = "witchtower_review_requested_version";
        private static float readySince = -1;
        private static bool requestedThisSession;
#endif
        public static void ResetIdleTimer()
        {
#if UNITY_IOS && !UNITY_EDITOR
            readySince = -1;
#endif
        }


        public static bool IsEligible(PlayerProfile profile, DateTime now, string lastRequest, string lastVersion, string version)
        {
            if (profile == null || !profile.HasCompletedTutorial || profile.HighestFloor < 10 || lastVersion == version) return false;
            return !DateTime.TryParse(lastRequest, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last)
                || (now.ToUniversalTime() - last.ToUniversalTime()).TotalDays >= 120;
        }

        public static void Tick(PlayerProfile profile, bool homeIsIdle)
        {
            if (WitchTower.Managers.SaveManager.Instance?.StorageAccessAvailable == false) return;
#if UNITY_IOS && !UNITY_EDITOR
            if (requestedThisSession) return;
            if (!homeIsIdle || Input.touchCount > 0 || Input.GetMouseButton(0) ||
                !IsEligible(profile, DateTime.UtcNow, PlayerPrefs.GetString(LastRequestKey), PlayerPrefs.GetString(VersionKey), Application.version))
            { readySince = -1; return; }
            if (readySince < 0) readySince = Time.unscaledTime;
            if (Time.unscaledTime - readySince < 8) return;
            requestedThisSession = true;
            // StoreKit decides whether to display a prompt. Never infer that a rating was submitted.
            PlayerPrefs.SetString(LastRequestKey, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(VersionKey, Application.version);
            PlayerPrefs.Save();
            UnityEngine.iOS.Device.RequestStoreReview();
#endif
        }
    }
}
