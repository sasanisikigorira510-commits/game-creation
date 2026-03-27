using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using WitchTower.Battle;
using WitchTower.Home;
using WitchTower.Managers;

    [InitializeOnLoad]
    public static class UnityMcpBridge
    {
        // Keep bridge diagnostics editor-only and easy to retrigger on script refresh / scene-builder updates.
        private const string Prefix = "http://127.0.0.1:8765/";
    private const int MainThreadTimeoutMs = 60000;
    private const string BridgeStateRelativePath = "tools/unity_bridge_state.json";
    private static readonly ConcurrentQueue<MainThreadWorkItem> MainThreadQueue = new ConcurrentQueue<MainThreadWorkItem>();
    private static readonly ConcurrentQueue<LogEntry> LogEntries = new ConcurrentQueue<LogEntry>();
    private const int MaxLogEntries = 200;

    private static HttpListener listener;
    private static Thread listenerThread;
    private static bool isRunning;

    static UnityMcpBridge()
    {
        EditorApplication.update -= ProcessMainThreadQueue;
        EditorApplication.update += ProcessMainThreadQueue;
        AssemblyReloadEvents.beforeAssemblyReload -= StopServer;
        AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        EditorApplication.quitting -= StopServer;
        EditorApplication.quitting += StopServer;
        Application.logMessageReceivedThreaded -= HandleLogMessage;
        Application.logMessageReceivedThreaded += HandleLogMessage;
        StartServer();
    }

    [MenuItem("Tools/MCP/Restart Bridge")]
    public static void RestartBridge()
    {
        StopServer();
        StartServer();
    }

    [MenuItem("Tools/MCP/Log Bridge Status")]
    public static void LogBridgeStatus()
    {
        Debug.Log(string.Format("[UnityMcpBridge] Running={0} Prefix={1}", isRunning, Prefix));
    }

    private static void StartServer()
    {
        if (isRunning)
        {
            return;
        }

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(Prefix);
            listener.Start();

            isRunning = true;
            listenerThread = new Thread(ListenLoop);
            listenerThread.IsBackground = true;
            listenerThread.Start();
            WriteBridgeStateFile(true, null);

            Debug.Log("[UnityMcpBridge] Listening on " + Prefix);
        }
        catch (Exception ex)
        {
            isRunning = false;
            WriteBridgeStateFile(false, ex.Message);
            Debug.LogError("[UnityMcpBridge] Failed to start bridge: " + ex);
        }
    }

    private static void StopServer()
    {
        isRunning = false;

        try
        {
            if (listener != null && listener.IsListening)
            {
                listener.Stop();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UnityMcpBridge] Failed to stop listener cleanly: " + ex.Message);
        }

        try
        {
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(500);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UnityMcpBridge] Failed to join listener thread: " + ex.Message);
        }

        listenerThread = null;
        listener = null;
        WriteBridgeStateFile(false, null);
    }

    private static void ListenLoop()
    {
        while (isRunning && listener != null)
        {
            HttpListenerContext context = null;

            try
            {
                context = listener.GetContext();
                HttpListenerContext capturedContext = context;

                string path = string.Empty;
                try
                {
                    path = capturedContext.Request.Url != null
                        ? capturedContext.Request.Url.AbsolutePath.Trim('/')
                        : string.Empty;
                }
                catch
                {
                }

                if (string.Equals(path, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        HandleRequest(capturedContext);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            WriteError(capturedContext.Response, 500, "Unhandled bridge error: " + ex.Message);
                        }
                        catch
                        {
                        }

                        if (isRunning)
                        {
                            Debug.LogError("[UnityMcpBridge] Ping handling failed: " + ex);
                        }
                    }

                    context = null;
                    continue;
                }

                Thread requestThread = new Thread(() =>
                {
                    try
                    {
                        HandleRequest(capturedContext);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            WriteError(capturedContext.Response, 500, "Unhandled bridge error: " + ex.Message);
                        }
                        catch
                        {
                        }

                        if (isRunning)
                        {
                            Debug.LogError("[UnityMcpBridge] Request handling failed: " + ex);
                        }
                    }
                });
                requestThread.IsBackground = true;
                requestThread.Start();
                context = null;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Debug.LogError("[UnityMcpBridge] Request handling failed: " + ex);
                }
            }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        string path = context.Request.Url.AbsolutePath.Trim('/');
        string method = context.Request.HttpMethod.ToUpperInvariant();

        if (path == "ping" && method == "GET")
        {
            WriteJson(context.Response, BuildPingResponse());
            return;
        }

        if (path == "project-info" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildProjectInfoResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "list-scenes" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildSceneListResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "list-components" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildComponentListResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "list-text" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildTextListResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "list-buttons" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildButtonListResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "console" && method == "GET")
        {
            WriteJson(context.Response, BuildConsoleResponse());
            return;
        }

        if (path == "clear-console" && method == "POST")
        {
            while (LogEntries.TryDequeue(out _))
            {
            }

            WriteJson(context.Response, "{\"ok\":true,\"message\":\"Console log buffer cleared.\"}");
            return;
        }

        if (path == "battle-debug" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildBattleDebugResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "home-debug" && method == "GET")
        {
            string json = RunOnMainThreadAndWait(BuildHomeDebugResponse);
            WriteJson(context.Response, json);
            return;
        }

        if (path == "refresh-assets" && method == "POST")
        {
            string json = RunOnMainThreadAndWait(delegate
            {
                AssetDatabase.Refresh();
                return "{\"ok\":true,\"message\":\"Assets refreshed.\"}";
            });
            WriteJson(context.Response, json);
            return;
        }

        if (path == "open-scene" && method == "POST")
        {
            string requestBody = ReadRequestBody(context.Request);
            OpenSceneRequest payload = ParseJson<OpenSceneRequest>(requestBody);

            if (payload == null || string.IsNullOrEmpty(payload.path))
            {
                WriteError(context.Response, 400, "Missing required field: path");
                return;
            }

            string json = RunOnMainThreadAndWait(delegate
            {
                string scenePath = NormalizeAssetPath(payload.path);
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                if (sceneAsset == null)
                {
                    return BuildFailureResponse("Scene not found: " + payload.path);
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return BuildFailureResponse("Open scene canceled because unsaved changes were not confirmed.");
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                return "{\"ok\":true,\"message\":\"Scene opened.\",\"activeScenePath\":\"" + EscapeJson(scene.path) + "\"}";
            });

            WriteJson(context.Response, json);
            return;
        }

        if (path == "execute-menu-item" && method == "POST")
        {
            string requestBody = ReadRequestBody(context.Request);
            ExecuteMenuItemRequest payload = ParseJson<ExecuteMenuItemRequest>(requestBody);

            if (payload == null || string.IsNullOrEmpty(payload.menuPath))
            {
                WriteError(context.Response, 400, "Missing required field: menuPath");
                return;
            }

            string json = RunOnMainThreadAndWait(delegate
            {
                bool executed = EditorApplication.ExecuteMenuItem(payload.menuPath);
                return executed
                    ? "{\"ok\":true,\"message\":\"Menu item executed.\"}"
                    : BuildFailureResponse("Menu item not found or failed: " + payload.menuPath);
            });

            WriteJson(context.Response, json);
            return;
        }

        if (path == "play-mode" && method == "POST")
        {
            string requestBody = ReadRequestBody(context.Request);
            PlayModeRequest payload = ParseJson<PlayModeRequest>(requestBody);
            string action = payload != null && !string.IsNullOrEmpty(payload.action) ? payload.action : "toggle";

            string json = RunOnMainThreadAndWait(delegate
            {
                switch (action)
                {
                    case "enter":
                        EditorApplication.isPlaying = true;
                        break;
                    case "exit":
                        EditorApplication.isPlaying = false;
                        break;
                    case "toggle":
                        EditorApplication.isPlaying = !EditorApplication.isPlaying;
                        break;
                    default:
                        return BuildFailureResponse("Unsupported play mode action: " + action);
                }

                return "{\"ok\":true,\"message\":\"Play mode action applied.\",\"isPlaying\":" +
                    (EditorApplication.isPlaying ? "true" : "false") + "}";
            });

            WriteJson(context.Response, json);
            return;
        }

        if (path == "invoke-method" && method == "POST")
        {
            string requestBody = ReadRequestBody(context.Request);
            InvokeMethodRequest payload = ParseJson<InvokeMethodRequest>(requestBody);

            if (payload == null || string.IsNullOrEmpty(payload.componentType) || string.IsNullOrEmpty(payload.methodName))
            {
                WriteError(context.Response, 400, "Missing required fields: componentType, methodName");
                return;
            }

            string json = RunOnMainThreadAndWait(delegate
            {
                return InvokeComponentMethod(payload.componentType, payload.methodName);
            });

            WriteJson(context.Response, json);
            return;
        }

        if (path == "simulate-idle-reward" && method == "POST")
        {
            string requestBody = ReadRequestBody(context.Request);
            SimulateIdleRewardRequest payload = ParseJson<SimulateIdleRewardRequest>(requestBody);
            int minutes = payload != null ? payload.minutes : 0;
            if (minutes <= 0)
            {
                WriteError(context.Response, 400, "Missing or invalid field: minutes");
                return;
            }

            string json = RunOnMainThreadAndWait(delegate
            {
                var profile = GameManager.Instance != null ? GameManager.Instance.PlayerProfile : null;
                if (profile == null)
                {
                    return BuildFailureResponse("PlayerProfile is not initialized.");
                }

                var now = DateTime.Now;
                profile.LastActiveAt = now.AddMinutes(-minutes).ToString("O");
                IdleRewardService.EvaluatePendingReward(profile, now);
                SaveManager.Instance?.SaveCurrentGame();
                return "{\"ok\":true,\"message\":\"Idle reward simulated.\",\"minutes\":" + minutes +
                    ",\"pendingIdleRewardGold\":" + profile.PendingIdleRewardGold + "}";
            });

            WriteJson(context.Response, json);
            return;
        }

        WriteError(context.Response, 404, "Unknown endpoint: " + path);
    }

    private static string RunOnMainThreadAndWait(Func<string> action)
    {
        MainThreadWorkItem item = new MainThreadWorkItem(action);
        MainThreadQueue.Enqueue(item);
        if (!item.Completed.Wait(MainThreadTimeoutMs))
        {
            return BuildFailureResponse("Unity main thread work timed out.");
        }

        if (item.Error != null)
        {
            return BuildFailureResponse(item.Error.Message);
        }

        return item.Result ?? "{\"ok\":true}";
    }

    private static void ProcessMainThreadQueue()
    {
        while (MainThreadQueue.TryDequeue(out MainThreadWorkItem item))
        {
            try
            {
                item.Result = item.Action();
            }
            catch (Exception ex)
            {
                item.Error = ex;
            }
            finally
            {
                item.Completed.Set();
            }
        }
    }

    private static string BuildPingResponse()
    {
        return "{\"ok\":true,\"message\":\"Unity MCP bridge is running.\",\"unityVersion\":\"" +
            EscapeJson(Application.unityVersion) + "\",\"baseUrl\":\"" + EscapeJson(Prefix.TrimEnd('/')) + "\"}";
    }

    private static string BuildProjectInfoResponse()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        return "{\"ok\":true," +
            "\"productName\":\"" + EscapeJson(Application.productName) + "\"," +
            "\"companyName\":\"" + EscapeJson(Application.companyName) + "\"," +
            "\"unityVersion\":\"" + EscapeJson(Application.unityVersion) + "\"," +
            "\"projectPath\":\"" + EscapeJson(projectPath) + "\"," +
            "\"baseUrl\":\"" + EscapeJson(Prefix.TrimEnd('/')) + "\"," +
            "\"assetsPath\":\"" + EscapeJson(Application.dataPath) + "\"," +
            "\"activeScenePath\":\"" + EscapeJson(activeScene.path) + "\"," +
            "\"isPlaying\":" + (EditorApplication.isPlaying ? "true" : "false") + "," +
            "\"isPaused\":" + (EditorApplication.isPaused ? "true" : "false") +
            "}";
    }

    private static string BuildSceneListResponse()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true,\"scenes\":[");

        for (int i = 0; i < guids.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(",");
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            builder.Append("\"");
            builder.Append(EscapeJson(path));
            builder.Append("\"");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildComponentListResponse()
    {
        MonoBehaviour[] components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true,\"components\":[");

        bool isFirst = true;
        foreach (MonoBehaviour component in components)
        {
            if (component == null)
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append(",");
            }

            isFirst = false;
            builder.Append("{");
            builder.Append("\"componentType\":\"");
            builder.Append(EscapeJson(component.GetType().FullName));
            builder.Append("\",\"gameObjectName\":\"");
            builder.Append(EscapeJson(component.gameObject.name));
            builder.Append("\",\"scenePath\":\"");
            builder.Append(EscapeJson(component.gameObject.scene.path));
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildTextListResponse()
    {
        TMP_Text[] texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true,\"texts\":[");

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (i > 0)
            {
                builder.Append(",");
            }

            builder.Append("{");
            builder.Append("\"gameObjectName\":\"");
            builder.Append(EscapeJson(text.gameObject.name));
            builder.Append("\",\"scenePath\":\"");
            builder.Append(EscapeJson(text.gameObject.scene.path));
            builder.Append("\",\"active\":");
            builder.Append(text.gameObject.activeInHierarchy ? "true" : "false");
            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(text.text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildButtonListResponse()
    {
        Button[] buttons = UnityEngine.Object.FindObjectsOfType<Button>(true);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true,\"buttons\":[");

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (i > 0)
            {
                builder.Append(",");
            }

            TMP_Text label = GetPrimaryButtonLabel(button);
            Image image = button.GetComponent<Image>();

            builder.Append("{");
            builder.Append("\"gameObjectName\":\"");
            builder.Append(EscapeJson(button.gameObject.name));
            builder.Append("\",\"scenePath\":\"");
            builder.Append(EscapeJson(button.gameObject.scene.path));
            builder.Append("\",\"active\":");
            builder.Append(button.gameObject.activeInHierarchy ? "true" : "false");
            builder.Append(",\"interactable\":");
            builder.Append(button.interactable ? "true" : "false");
            builder.Append(",\"label\":\"");
            builder.Append(EscapeJson(label != null ? label.text : string.Empty));
            builder.Append("\",\"backgroundColor\":\"");
            builder.Append(EscapeJson(ColorUtility.ToHtmlStringRGBA(image != null ? image.color : Color.clear)));
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static TMP_Text GetPrimaryButtonLabel(Button button)
    {
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
            {
                continue;
            }

            if (!label.gameObject.name.EndsWith("Badge"))
            {
                return label;
            }
        }

        return labels.Length > 0 ? labels[0] : null;
    }

    private static string BuildConsoleResponse()
    {
        LogEntry[] entries = LogEntries.ToArray();
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true,\"entries\":[");

        for (int i = 0; i < entries.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(",");
            }

            builder.Append("{");
            builder.Append("\"type\":\"");
            builder.Append(EscapeJson(entries[i].Type));
            builder.Append("\",\"message\":\"");
            builder.Append(EscapeJson(entries[i].Message));
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildBattleDebugResponse()
    {
        BattleSceneController sceneController = UnityEngine.Object.FindObjectOfType<BattleSceneController>(true);
        BattleStateMachine stateMachine = UnityEngine.Object.FindObjectOfType<BattleStateMachine>(true);
        BattleSimulator simulator = UnityEngine.Object.FindObjectOfType<BattleSimulator>(true);
        BattleHudController hud = UnityEngine.Object.FindObjectOfType<BattleHudController>(true);

        if (sceneController == null && stateMachine == null && simulator == null)
        {
            return "{\"ok\":false,\"message\":\"Battle components not found.\"}";
        }

        TMP_Text[] texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true");
        builder.Append(",\"hasSceneController\":");
        builder.Append(sceneController != null ? "true" : "false");
        builder.Append(",\"hasStateMachine\":");
        builder.Append(stateMachine != null ? "true" : "false");
        builder.Append(",\"hasSimulator\":");
        builder.Append(simulator != null ? "true" : "false");
        builder.Append(",\"hasHud\":");
        builder.Append(hud != null ? "true" : "false");
        builder.Append(",\"editorPaused\":");
        builder.Append(EditorApplication.isPaused ? "true" : "false");

        if (stateMachine != null)
        {
            builder.Append(",\"flowState\":\"");
            builder.Append(EscapeJson(stateMachine.CurrentState.ToString()));
            builder.Append("\"");
        }

        if (simulator != null)
        {
            builder.Append(",\"simulatorRunning\":");
            builder.Append(simulator.IsRunning ? "true" : "false");
            builder.Append(",\"simulatorTickCount\":");
            builder.Append(simulator.DebugTickCount);
            builder.Append(",\"simulatorLastDeltaTime\":");
            builder.Append(simulator.DebugLastDeltaTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",\"playerAttackTimer\":");
            builder.Append(simulator.DebugPlayerAttackTimer.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",\"enemyAttackTimer\":");
            builder.Append(simulator.DebugEnemyAttackTimer.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",\"guardRemainingTime\":");
            builder.Append(simulator.DebugGuardRemainingTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",\"playerStats\":");
            AppendStats(builder, simulator.PlayerStats);
            builder.Append(",\"enemyStats\":");
            AppendStats(builder, simulator.EnemyStats);
        }

        if (sceneController != null)
        {
            builder.Append(",\"controllerUpdateCount\":");
            builder.Append(sceneController.DebugUpdateCount);
            builder.Append(",\"controllerLastDeltaTime\":");
            builder.Append(sceneController.DebugLastDeltaTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.Append(",\"texts\":[");
        bool isFirst = true;
        foreach (TMP_Text text in texts)
        {
            if (text == null || text.gameObject.scene.path != "Assets/Scenes/BattleScene.unity")
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append(",");
            }

            isFirst = false;
            builder.Append("{\"name\":\"");
            builder.Append(EscapeJson(text.gameObject.name));
            builder.Append("\",\"active\":");
            builder.Append(text.gameObject.activeInHierarchy ? "true" : "false");
            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(text.text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildHomeDebugResponse()
    {
        var gameManager = GameManager.Instance;
        var profile = gameManager != null ? gameManager.PlayerProfile : null;
        if (profile == null)
        {
            return "{\"ok\":false,\"message\":\"PlayerProfile is not initialized.\"}";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true");
        builder.Append(",\"currentFloor\":");
        builder.Append(gameManager != null ? gameManager.CurrentFloor : 1);
        builder.Append(",\"level\":");
        builder.Append(profile.Level);
        builder.Append(",\"exp\":");
        builder.Append(profile.Exp);
        builder.Append(",\"requiredExp\":");
        builder.Append(profile.GetRequiredExpForNextLevel());
        builder.Append(",\"gold\":");
        builder.Append(profile.Gold);
        builder.Append(",\"highestFloor\":");
        builder.Append(profile.HighestFloor);
        builder.Append(",\"attackUpgradeLevel\":");
        builder.Append(profile.AttackUpgradeLevel);
        builder.Append(",\"defenseUpgradeLevel\":");
        builder.Append(profile.DefenseUpgradeLevel);
        builder.Append(",\"hpUpgradeLevel\":");
        builder.Append(profile.HpUpgradeLevel);
        builder.Append(",\"pendingIdleRewardGold\":");
        builder.Append(profile.PendingIdleRewardGold);
        int baseUpgradeCost = 10;
        builder.Append(",\"homeBadgeCount\":");
        builder.Append(HomeActionAdvisor.GetHomeBadgeCount(profile));
        builder.Append(",\"enhanceBadgeCount\":");
        builder.Append(HomeActionAdvisor.GetEnhanceBadgeCount(profile, baseUpgradeCost));
        builder.Append(",\"equipmentBadgeCount\":");
        builder.Append(HomeActionAdvisor.GetEquipmentBadgeCount(profile));
        builder.Append(",\"missionBadgeCount\":");
        builder.Append(HomeActionAdvisor.GetMissionBadgeCount(profile, DateTime.Now));
        builder.Append(",\"homeHeadline\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildHomeHeadline(profile)));
        builder.Append("\",\"enhanceHeadline\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildEnhanceHeadline(profile, baseUpgradeCost)));
        builder.Append("\",\"equipmentHeadline\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildEquipmentHeadline(profile)));
        builder.Append("\",\"missionHeadline\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildMissionHeadline(profile, DateTime.Now)));
        builder.Append("\",\"runProgressText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRunProgressText(profile)));
        builder.Append("\",\"rewardForecastText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRewardForecastText(profile)));
        builder.Append("\",\"threatReadText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildThreatReadText(profile)));
        builder.Append("\",\"runConfidenceText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildConfidenceText(profile)));
        builder.Append("\",\"loadoutAlertText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildLoadoutAlertText(profile)));
        builder.Append("\",\"goldRouteText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildGoldRouteText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"upgradeRouteText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildUpgradeRouteText(profile, baseUpgradeCost)));
        builder.Append("\",\"rewardRouteText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRewardRouteText(profile, DateTime.Now)));
        builder.Append("\",\"pushWindowText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildPushWindowText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"roiReadText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRoiReadText(profile, baseUpgradeCost)));
        builder.Append("\",\"decisionLineText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildDecisionLineText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"decisionBadgeText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildDecisionBadgeText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"commandStackText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildCommandStackText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"momentumReadText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildMomentumReadText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"runCallText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRunCallText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"riskBufferText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRiskBufferText(profile)));
        builder.Append("\",\"enemyTempoText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildEnemyTempoText(profile)));
        builder.Append("\",\"damageRaceText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildDamageRaceText(profile)));
        builder.Append("\",\"burstReadText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildBurstReadText(profile)));
        builder.Append("\",\"killClockText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildKillClockText(profile)));
        builder.Append("\",\"critWindowText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildCritWindowText(profile)));
        builder.Append("\",\"survivalWindowText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildSurvivalWindowText(profile)));
        builder.Append("\",\"clockEdgeText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildClockEdgeText(profile)));
        builder.Append("\",\"tempoVerdictText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildTempoVerdictText(profile)));
        builder.Append("\",\"pressureCallText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildPressureCallText(profile)));
        builder.Append("\",\"rewardPaceText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRewardPaceText(profile)));
        builder.Append("\",\"runActionText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildRunAlertText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"battlePlanText\":\"");
        builder.Append(EscapeJson(HomeActionAdvisor.BuildBattlePlanText(profile, baseUpgradeCost, DateTime.Now)));
        builder.Append("\",\"lastDailyRewardDate\":\"");
        builder.Append(EscapeJson(profile.LastDailyRewardDate));
        builder.Append("\",\"equippedWeaponId\":\"");
        builder.Append(EscapeJson(profile.EquippedWeaponId));
        builder.Append("\",\"equippedArmorId\":\"");
        builder.Append(EscapeJson(profile.EquippedArmorId));
        builder.Append("\",\"equippedAccessoryId\":\"");
        builder.Append(EscapeJson(profile.EquippedAccessoryId));
        builder.Append("\",\"ownedEquipments\":[");

        for (int i = 0; i < profile.OwnedEquipments.Count; i++)
        {
            var equipment = profile.OwnedEquipments[i];
            if (equipment == null)
            {
                continue;
            }

            if (i > 0)
            {
                builder.Append(",");
            }

            builder.Append("{\"equipmentId\":\"");
            builder.Append(EscapeJson(equipment.EquipmentId));
            builder.Append("\",\"isEquipped\":");
            builder.Append(equipment.IsEquipped ? "true" : "false");
            builder.Append("}");
        }

        builder.Append("],\"missions\":[");
        for (int i = 0; i < profile.MissionProgressList.Count; i++)
        {
            var mission = profile.MissionProgressList[i];
            if (mission == null)
            {
                continue;
            }

            if (i > 0)
            {
                builder.Append(",");
            }

            builder.Append("{\"missionId\":\"");
            builder.Append(EscapeJson(mission.MissionId));
            builder.Append("\",\"progress\":");
            builder.Append(mission.Progress);
            builder.Append(",\"isClaimed\":");
            builder.Append(mission.IsClaimed ? "true" : "false");
            builder.Append("}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static void AppendStats(StringBuilder builder, BattleUnitStats stats)
    {
        if (stats == null)
        {
            builder.Append("null");
            return;
        }

        builder.Append("{\"maxHp\":");
        builder.Append(stats.MaxHp);
        builder.Append(",\"currentHp\":");
        builder.Append(stats.CurrentHp);
        builder.Append(",\"attack\":");
        builder.Append(stats.Attack);
        builder.Append(",\"defense\":");
        builder.Append(stats.Defense);
        builder.Append(",\"attackSpeed\":");
        builder.Append(stats.AttackSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(",\"critRate\":");
        builder.Append(stats.CritRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(",\"critDamage\":");
        builder.Append(stats.CritDamage.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("}");
    }

    private static void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        LogEntries.Enqueue(new LogEntry
        {
            Type = type.ToString(),
            Message = string.IsNullOrEmpty(stackTrace) ? condition : condition + "\n" + stackTrace
        });

        while (LogEntries.Count > MaxLogEntries && LogEntries.TryDequeue(out _))
        {
        }
    }

    private static T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UnityMcpBridge] Failed to parse request JSON: " + ex.Message);
            return null;
        }
    }

    private static string ReadRequestBody(HttpListenerRequest request)
    {
        using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            return reader.ReadToEnd();
        }
    }

    private static void WriteJson(HttpListenerResponse response, string json)
    {
        TryWriteResponse(response, 200, json);
    }

    private static void WriteError(HttpListenerResponse response, int statusCode, string message)
    {
        TryWriteResponse(response, statusCode, BuildFailureResponse(message));
    }

    private static void TryWriteResponse(HttpListenerResponse response, int statusCode, string json)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (HttpListenerException)
        {
        }
    }

    private static string BuildFailureResponse(string message)
    {
        return "{\"ok\":false,\"message\":\"" + EscapeJson(message) + "\"}";
    }

    private static string InvokeComponentMethod(string componentTypeName, string methodName)
    {
        MonoBehaviour[] components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
        {
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            bool isMatch = string.Equals(type.Name, componentTypeName, StringComparison.Ordinal) ||
                string.Equals(type.FullName, componentTypeName, StringComparison.Ordinal);

            if (!isMatch)
            {
                continue;
            }

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null)
            {
                return BuildFailureResponse("Method not found or requires parameters: " + componentTypeName + "." + methodName);
            }

            try
            {
                method.Invoke(component, null);
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                return BuildFailureResponse(type.FullName + "." + methodName + " threw " + inner.GetType().Name + ": " + inner.Message);
            }

            return "{\"ok\":true,\"message\":\"Method invoked.\",\"componentType\":\"" + EscapeJson(type.FullName) +
                "\",\"methodName\":\"" + EscapeJson(methodName) + "\"}";
        }

        return BuildFailureResponse("Component not found in open scenes: " + componentTypeName);
    }

    private static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        string normalized = path.Replace("\\", "/");
        string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");

        if (normalized.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.Substring(projectPath.Length + 1);
        }

        return normalized;
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static void WriteBridgeStateFile(bool running, string errorMessage)
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string statePath = Path.Combine(projectRoot, BridgeStateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string stateDirectory = Path.GetDirectoryName(statePath);
            if (!string.IsNullOrEmpty(stateDirectory))
            {
                Directory.CreateDirectory(stateDirectory);
            }

            string json =
                "{\"ok\":true," +
                "\"running\":" + (running ? "true" : "false") + "," +
                "\"baseUrl\":\"" + EscapeJson(Prefix.TrimEnd('/')) + "\"," +
                "\"prefix\":\"" + EscapeJson(Prefix) + "\"," +
                "\"projectPath\":\"" + EscapeJson(projectRoot) + "\"," +
                "\"productName\":\"" + EscapeJson(Application.productName) + "\"," +
                "\"unityVersion\":\"" + EscapeJson(Application.unityVersion) + "\"," +
                "\"processId\":" + System.Diagnostics.Process.GetCurrentProcess().Id + "," +
                "\"timestamp\":\"" + EscapeJson(DateTime.Now.ToString("O")) + "\"," +
                "\"errorMessage\":\"" + EscapeJson(errorMessage ?? string.Empty) + "\"" +
                "}";

            File.WriteAllText(statePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            if (running)
            {
                UnityEngine.Debug.LogWarning("[UnityMcpBridge] Failed to write bridge state file: " + ex.Message);
            }
        }
    }

    [Serializable]
    private class OpenSceneRequest
    {
        public string path;
    }

    [Serializable]
    private class ExecuteMenuItemRequest
    {
        public string menuPath;
    }

    [Serializable]
    private class PlayModeRequest
    {
        public string action;
    }

    [Serializable]
    private class InvokeMethodRequest
    {
        public string componentType;
        public string methodName;
    }

    [Serializable]
    private class SimulateIdleRewardRequest
    {
        public int minutes;
    }

    private class MainThreadWorkItem
    {
        public readonly Func<string> Action;
        public readonly ManualResetEventSlim Completed;
        public string Result;
        public Exception Error;

        public MainThreadWorkItem(Func<string> action)
        {
            Action = action;
            Completed = new ManualResetEventSlim(false);
        }
    }

    private struct LogEntry
    {
        public string Type;
        public string Message;
    }
}
