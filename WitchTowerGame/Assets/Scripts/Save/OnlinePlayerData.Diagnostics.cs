using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WitchTower.Managers;

namespace WitchTower.Save
{
    public sealed partial class OnlinePlayerData
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable] private sealed class ConnectionDiagnostic
        {
            public string TimeUtc, Method, Route, Result;
            public long Status;
        }
        [Serializable] private sealed class ConnectionDiagnostics
        {
            public List<ConnectionDiagnostic> Entries = new List<ConnectionDiagnostic>();
        }
        private readonly ConnectionDiagnostics connectionDiagnostics = new ConnectionDiagnostics();
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void RecordConnectionDiagnostic(string method, string path, long code, string result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (StorageOwnerUnavailable) return;
            // Deliberately exclude URL, player ID, headers, tokens, receipts,
            // bodies and error text. Keep a bounded local QA-only ring buffer.
            string route = path == "/v1/accounts" ? "accounts" :
                path.StartsWith("/v1/players/", StringComparison.Ordinal) ?
                path.Contains("/head?") ? "head" : path.EndsWith("/snapshots", StringComparison.Ordinal) ? "snapshots" :
                path.EndsWith("/operations", StringComparison.Ordinal) ? "operations" : "player-other" : "other";
            connectionDiagnostics.Entries.Add(new ConnectionDiagnostic
            {
                TimeUtc = DateTime.UtcNow.ToString("O"), Method = method, Route = route, Status = code, Result = result
            });
            if (connectionDiagnostics.Entries.Count > 32) connectionDiagnostics.Entries.RemoveAt(0);
            Debug.Log($"[OnlineConnection] {method} {route} HTTP={code} result={result}");
            try
            {
                WriteAtomic(Path.Combine(SaveManager.Instance.RootDirectory, "online-connection-diagnostics.json"),
                    JsonUtility.ToJson(connectionDiagnostics));
            }
            catch (Exception) { /* Diagnostics must never block a transaction or alter its result. */ }
#endif
        }
    }
}
