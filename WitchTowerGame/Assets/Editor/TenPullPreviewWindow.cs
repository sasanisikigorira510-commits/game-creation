using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Home;
using WitchTower.MasterData;
using WitchTower.Managers;

// Editor assembly only: neither this menu nor its synthetic data ships to players.
public sealed class TenPullPreviewWindow : EditorWindow
{
    public static readonly string[] Scenarios = { "全部通常（クラス1〜3）", "クラス4が1体", "クラス4が複数", "最後だけクラス4", "最初にクラス4", "クラス3＋クラス4", "クラス3のみ（通常演出）" };
    private Scene scene;
    private Camera camera;
    private RenderTexture texture;
    private TenPullPresentationController controller;
    private int scenario;
    private bool singlePull;
    private double lastTime;
    private bool hideFilmMagic;

    [MenuItem("Tools/WitchTower/10連召喚プレビュー")]
    public static void Open() => GetWindow<TenPullPreviewWindow>("10連召喚プレビュー");

    [MenuItem("Tools/WitchTower/単発召喚プレビュー")]
    public static void OpenSingle()
    {
        var window = GetWindow<TenPullPreviewWindow>("召喚プレビュー");
        window.singlePull = true;
    }

    private void OnEnable() { EditorApplication.update += Tick; }
    private void OnDisable() { EditorApplication.update -= Tick; Cleanup(); }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("開発用・消費なし・所持反映なし。実際の抽選は行いません。専用召喚曲・SEはゲーム実行時、または音声付き書き出し動画で確認できます。", MessageType.Info);
        singlePull = EditorGUILayout.ToggleLeft("単発召喚（1体）", singlePull);
        scenario = EditorGUILayout.Popup("結果パターン", scenario, Scenarios);
        hideFilmMagic = EditorGUILayout.ToggleLeft("腕の位置確認：導入の発光を隠す", hideFilmMagic);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("プレビュー開始 / リセット")) StartPreview();
        if (GUILayout.Button("次へ")) controller?.Sequence?.Advance();
        if (GUILayout.Button("自動切替")) controller?.ToggleAuto();
        if (GUILayout.Button("スキップ")) controller?.Skip();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("次へ100連打")) for (int i = 0; i < 100; i++) controller?.Sequence?.Advance();
        if (GUILayout.Button("スキップ100連打")) for (int i = 0; i < 100; i++) controller?.Skip();
        if (GUILayout.Button("途中離脱 → 復帰"))
        {
            if (controller != null) { controller.StopAndHide(); controller.gameObject.SetActive(true); }
        }
        if (GUILayout.Button("アプリ中断")) controller?.Interrupt();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(controller == null ? "待機中" : $"{controller.Sequence.Phase} / {controller.Sequence.Index + 1}個目 / クラス4: {controller.Sequence.Class4Count}体");
        if (texture != null)
        {
            Rect area = GUILayoutUtility.GetRect(position.width, Mathf.Max(100, position.height - 160));
            GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, false);
        }
    }

    private void StartPreview(Action<AudioCue, float> soundCue = null, Action soundStop = null,
        Action<TenPullScoreEvent> scoreCue = null)
    {
        Cleanup();
        scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("IsolatedTenPullPreview", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 31;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 2341);
        var panel = new GameObject("TenPullPreview", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
        controller = panel.AddComponent<TenPullPresentationController>();
        if (soundCue != null) controller.SoundCued += soundCue;
        if (soundStop != null) controller.SoundsStopped += soundStop;
        if (scoreCue != null) controller.ScoreCued += scoreCue;
        var results = CreateResults(scenario);
        if (singlePull) results = new[] { results.FirstOrDefault(r => r.ClassRank == 4) ?? results[0] };
        controller.Present(results, ResolvePortrait, null, null, null, preview: true);
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var cameraObject = new GameObject("TenPullPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 2341 / 2f;
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 2000;
        camera.transform.position = new Vector3(0, 0, -1000);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31; camera.scene = scene;
        texture = new RenderTexture(540, 1170, 24);
        camera.targetTexture = texture; canvas.worldCamera = camera;
        lastTime = EditorApplication.timeSinceStartup;
    }

    private void Tick()
    {
        if (controller == null || camera == null) return;
        double now = EditorApplication.timeSinceStartup;
        controller.TickPresentation((float)(now - lastTime)); lastTime = now;
        ApplyFilmDiagnostics();
        Canvas.ForceUpdateCanvases(); camera.Render(); Repaint();
    }

    // Editor-only review: inspect the painted shoulder without effects concealing a seam.
    private void ApplyFilmDiagnostics()
    {
        var film = controller.transform.Find("TenPullSafeContent/AnimeFilm");
        if (film == null) return;
        foreach (var layer in film.GetComponentsInChildren<Transform>(true))
        {
            if (layer.name == "AttachedMagic") layer.gameObject.SetActive(!hideFilmMagic);
            else if (hideFilmMagic && (layer.name.StartsWith("Class4Radiance", StringComparison.Ordinal)
                || layer.name == "Class4BackdropSeal" || layer.name == "EyeLightWipe"))
                layer.gameObject.SetActive(false);
        }
    }

    private void Cleanup()
    {
        controller = null; camera = null;
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        if (texture != null) { texture.Release(); DestroyImmediate(texture); texture = null; }
    }

    // Batch-mode review capture, also isolated from every gameplay manager and save.
    [Serializable] private sealed class PreviewAudioEvent { public float Time; public string Cue; public float Volume; public bool Stop; }
    [Serializable] private sealed class PreviewScoreEvent
    {
        public float Time;
        public string Bus;
        public string Resource;
        public float Volume;
        public float Pitch;
        public bool Loop;
        public bool Stop;
        public float FadeSeconds;
    }
    [Serializable] private sealed class PreviewAudioTimeline
    {
        public List<PreviewAudioEvent> Events = new List<PreviewAudioEvent>();
        public List<PreviewScoreEvent> ScoreEvents = new List<PreviewScoreEvent>();
        public float Duration;
    }
    public static void CaptureFrames()
    {
        string folder = Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_FRAMES");
        if (string.IsNullOrEmpty(folder)) throw new ArgumentException("Set WITCHTOWER_PREVIEW_FRAMES to an output directory.");
        Directory.CreateDirectory(folder);
        var preview = CreateInstance<TenPullPreviewWindow>();
        preview.singlePull = Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_SINGLE") == "1";
        preview.hideFilmMagic = Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_NO_MAGIC") == "1";
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview.scenario = int.TryParse(Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_SCENARIO"), out int pattern)
                ? Mathf.Clamp(pattern, 0, Scenarios.Length - 1) : 1;
            var audio = new PreviewAudioTimeline();
            int fps = int.TryParse(Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_FPS"), out int requestedFps) ? Mathf.Clamp(requestedFps, 30, 60) : 30;
            float captureSeconds = float.TryParse(Environment.GetEnvironmentVariable("WITCHTOWER_PREVIEW_SECONDS"), out float requestedSeconds) ? Mathf.Clamp(requestedSeconds, 1f, 60f) : 60f;
            float captureTime = 0;
            preview.StartPreview((cue, volume) => audio.Events.Add(new PreviewAudioEvent { Time = captureTime, Cue = cue.ToString(), Volume = volume }),
                () => audio.Events.Add(new PreviewAudioEvent { Time = captureTime, Stop = true }),
                cue => audio.ScoreEvents.Add(new PreviewScoreEvent { Time = captureTime, Bus = cue.Bus,
                    Resource = cue.Resource, Volume = cue.Volume, Pitch = cue.Pitch, Loop = cue.Loop,
                    Stop = cue.Stop, FadeSeconds = cue.FadeSeconds }));
            image = new Texture2D(540, 1170, TextureFormat.RGB24, false);
            int finalFrames = 0;
            for (int frame = 0; frame < fps * captureSeconds; frame++)
            {
                captureTime = frame / (float)fps;
                preview.controller.TickPresentation(1f / fps);
                preview.ApplyFilmDiagnostics();
                Canvas.ForceUpdateCanvases(); preview.camera.Render();
                RenderTexture.active = preview.texture;
                image.ReadPixels(new Rect(0, 0, 540, 1170), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, frame.ToString("D5") + ".png"), image.EncodeToPNG());
                if (preview.controller.Sequence.IsComplete && ++finalFrames >= fps * 1.5f) break;
            }
            audio.Duration = captureTime + 1f / fps;
            File.WriteAllText(Path.Combine(folder, "audio-timeline.json"), JsonUtility.ToJson(audio, true));
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) DestroyImmediate(image);
            DestroyImmediate(preview);
        }
    }

    public static SummonPresentationResult[] CreateResults(int pattern)
    {
        var master = Resources.Load<MasterDataRoot>("MasterData/MasterDataRoot");
        if (master == null) throw new InvalidOperationException("Monster master data is missing.");
        var catalog = master.monsterDataList.Where(m => m != null).OrderBy(m => m.monsterId, StringComparer.Ordinal).ToArray();
        var seen = new HashSet<string>();
        var results = new SummonPresentationResult[10];
        for (int i = 0; i < 10; i++)
        {
            int rank = 1 + i % 3;
            if (pattern == 1 && i == 4 || pattern == 2 && (i == 2 || i == 7) || pattern == 3 && i == 9 || pattern == 4 && i == 0) rank = 4;
            if (pattern == 5) rank = i == 8 ? 4 : i == 2 ? 3 : 1;
            if (pattern == 6) rank = 3;
            var candidates = catalog.Where(m => m.classRank == rank).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No preview monster for class " + rank);
            var monster = candidates[i % candidates.Length];
            results[i] = new SummonPresentationResult { MonsterId = monster.monsterId, InstanceId = "preview_" + i,
                DisplayName = monster.monsterName, ClassRank = monster.classRank, IndividualValue = 50 + i * 3,
                IsNew = seen.Add(monster.monsterId) };
        }
        return results;
    }

    public static Sprite ResolvePortrait(string id)
    {
        var master = Resources.Load<MasterDataRoot>("MasterData/MasterDataRoot");
        var monster = master.monsterDataList.FirstOrDefault(m => m != null && m.monsterId == id);
        // Read-only reuse of the same portrait resolver as the live summon screen.
        return (Sprite)typeof(GachaPanelController).GetMethod("ResolveMonsterSprite", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { monster });
    }
}
