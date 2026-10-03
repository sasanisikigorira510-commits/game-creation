using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WitchTower.Save
{
    public sealed class AppleNativeIdentity : MonoBehaviour
    {
        [Serializable] public sealed class Result { public string State, IdentityToken, AuthorizationCode, Error; }
        private Action<Result> callback;
        private string expectedState;
#if UNITY_EDITOR
        internal static Action<string, string, Action<Result>> EditorAuthorizeOverride;
#endif
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NasusAppleSignIn(string receiver, string nonce, string state);
        [DllImport("__Internal")] private static extern void NasusAppleCancel(string state);
#endif
        public IEnumerator Authorize(string nonce, string state, Action<Result> completed)
        {
            if (callback != null) { completed(new Result { Error = "busy" }); yield break; }
            Result result = null;
            expectedState = state;
            callback = value => result = value;
#if UNITY_EDITOR
            if (EditorAuthorizeOverride != null) EditorAuthorizeOverride(nonce, state, value => result = value);
            else result = new Result { Error = "iPhoneの開発ビルドで確認してください。" };
#elif UNITY_IOS
            NasusAppleSignIn(gameObject.name, nonce, state);
#else
            result = new Result { Error = "iPhoneの開発ビルドで確認してください。" };
#endif
            float until = Time.realtimeSinceStartup + 180;
            while (result == null && Time.realtimeSinceStartup < until) yield return null;
            if (result == null)
            {
#if UNITY_IOS && !UNITY_EDITOR
                NasusAppleCancel(state);
#endif
                result = new Result { Error = "認証が時間切れになりました。もう一度お試しください。" };
            }
            callback = null;
            expectedState = null;
            completed(result);
        }
        // Called only by the native plugin; the server independently verifies the JWT.
        public void OnAppleIdentity(string json)
        {
            if (callback == null) return;
            try
            {
                var result = JsonUtility.FromJson<Result>(json);
                if (result == null || result.State != expectedState) return; // Ignore late/cross-session callbacks.
                callback(result);
            }
            catch (Exception) { callback(new Result { Error = "認証応答を確認できませんでした。" }); }
        }
    }
}
