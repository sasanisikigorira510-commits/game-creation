using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class UnityMcpBridge
{
    private const string Prefix = "http://127.0.0.1:8765/";
    private static readonly ConcurrentQueue<MainThreadWorkItem> MainThreadQueue = new ConcurrentQueue<MainThreadWorkItem>();

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

            Debug.Log("[UnityMcpBridge] Listening on " + Prefix);
        }
        catch (Exception ex)
        {
            isRunning = false;
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
    }

    private static void ListenLoop()
    {
        while (isRunning && listener != null)
        {
            HttpListenerContext context = null;

            try
            {
                context = listener.GetContext();
                HandleRequest(context);
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
                if (context != null)
                {
                    WriteError(context.Response, 500, "Unhandled bridge error: " + ex.Message);
                }

                Debug.LogError("[UnityMcpBridge] Request handling failed: " + ex);
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

        WriteError(context.Response, 404, "Unknown endpoint: " + path);
    }

    private static string RunOnMainThreadAndWait(Func<string> action)
    {
        MainThreadWorkItem item = new MainThreadWorkItem(action);
        MainThreadQueue.Enqueue(item);
        item.Completed.Wait();

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
            EscapeJson(Application.unityVersion) + "\"}";
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
            "\"assetsPath\":\"" + EscapeJson(Application.dataPath) + "\"," +
            "\"activeScenePath\":\"" + EscapeJson(activeScene.path) + "\"," +
            "\"isPlaying\":" + (EditorApplication.isPlaying ? "true" : "false") +
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
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    private static void WriteError(HttpListenerResponse response, int statusCode, string message)
    {
        string json = BuildFailureResponse(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    private static string BuildFailureResponse(string message)
    {
        return "{\"ok\":false,\"message\":\"" + EscapeJson(message) + "\"}";
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
}
