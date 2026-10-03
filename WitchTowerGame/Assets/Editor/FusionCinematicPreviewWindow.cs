using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchTower.Home;
using WitchTower.MasterData;
using WitchTower.Save;

// Editor assembly only. The renderer receives synthetic display data and never calls fusion or save services.
public sealed class FusionCinematicPreviewWindow : EditorWindow
{
    private const int Width = 1080;
    private const int Height = 2340;
    private static readonly string[] Modes = { "通常（クラス3誕生）", "上位（クラス4誕生）" };
    private Scene scene;
    private Camera previewCamera;
    private RenderTexture target;
    private FusionCinematicPresentation presentation;
    private MonsterDataSO born;
    private OwnedMonsterData created;
    private Sprite parentA, parentB;
    private int mode;
    private float elapsed;
    private bool playing;
    private double previousTime;

    [MenuItem("WitchTower/Preview/Fusion Cinematic")]
    public static void Open()
    {
        var window = GetWindow<FusionCinematicPreviewWindow>("配合の儀式");
        window.minSize = new Vector2(360, 600);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        previousTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        Cleanup();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("開発用プレビュー。親・通貨・保存データは変更しません。音はゲーム内の実際の配合で確認できます。", MessageType.Info);
        int next = EditorGUILayout.Popup("誕生結果", mode, Modes);
        if (next != mode) { mode = next; StartPreview(); }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("開始 / 最初から")) { StartPreview(); playing = true; }
        if (GUILayout.Button(playing ? "一時停止" : "再生"))
        {
            if (presentation == null) StartPreview();
            playing = !playing;
        }
        if (GUILayout.Button("結果へスキップ") && presentation != null)
        {
            presentation.Skip(); elapsed = Duration; playing = false; Render();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("途中離脱") && presentation != null) Cleanup();
        if (GUILayout.Button("再読み込み")) { AssetDatabase.Refresh(); StartPreview(); }
        EditorGUILayout.EndHorizontal();
        if (presentation != null)
        {
            EditorGUI.BeginChangeCheck();
            float nextTime = EditorGUILayout.Slider("再生位置（秒）", elapsed, 0f, Duration);
            if (EditorGUI.EndChangeCheck()) { elapsed = nextTime; playing = false; Render(); }
            EditorGUILayout.LabelField($"{born.monsterName}  /  クラス {born.classRank}  /  {elapsed:0.00}秒");
        }
        if (target != null)
            GUI.DrawTexture(GUILayoutUtility.GetRect(position.width, Mathf.Max(100, position.height - 205)), target, ScaleMode.ScaleToFit, false);
    }

    private float Duration => FusionCinematicPresentation.DurationForClass(mode == 1 ? 4 : 3);

    private void StartPreview()
    {
        Cleanup();
        scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("IsolatedFusionCinematicPreview", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1080, 2340);
        var data = Resources.Load<MasterDataRoot>("MasterData/MasterDataRoot");
        if (data == null) throw new InvalidOperationException("Monster master data is missing.");
        born = data.monsterDataList.FirstOrDefault(m => m != null && m.monsterId == (mode == 1 ? "monster_drag_gaia" : "monster_abyss_dragon"))
            ?? data.monsterDataList.First(m => m != null && m.classRank == (mode == 1 ? 4 : 3));
        parentA = Portrait(data.monsterDataList.First(m => m != null && m.monsterId == "monster_flare_drake"));
        parentB = Portrait(data.monsterDataList.First(m => m != null && m.monsterId == "monster_rock_golem"));
        created = new OwnedMonsterData { InstanceId = "fusion_preview_only", MonsterId = born.monsterId, Level = 1, PlusValue = 4, FusionBonusHp = 18, FusionBonusAttack = 6 };
        presentation = new FusionCinematicPresentation(rect);
        var cameraObject = new GameObject("FusionCinematicPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        previewCamera = cameraObject.GetComponent<Camera>(); previewCamera.enabled = false;
        previewCamera.scene = scene; previewCamera.orthographic = true; previewCamera.orthographicSize = 1170;
        previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 2000;
        previewCamera.transform.position = new Vector3(0, 0, -1000);
        previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = Color.black;
        previewCamera.cullingMask = 1 << 31;
        target = new RenderTexture(Width, Height, 24); target.Create();
        previewCamera.targetTexture = target; canvas.worldCamera = previewCamera;
        elapsed = 0; previousTime = EditorApplication.timeSinceStartup;
        Render();
    }

    private static Sprite Portrait(MonsterDataSO monster)
    {
        if (monster.illustrationSprite != null) return monster.illustrationSprite;
        if (monster.portraitSprite != null) return monster.portraitSprite;
        return Resources.Load<Sprite>(!string.IsNullOrEmpty(monster.illustrationResourcePath) ? monster.illustrationResourcePath : monster.portraitResourcePath);
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (presentation != null && playing)
        {
            elapsed = Mathf.Min(Duration, elapsed + (float)(now - previousTime));
            if (elapsed >= Duration) playing = false;
            Render(); Repaint();
        }
        previousTime = now;
    }

    private void Render()
    {
        if (presentation == null || previewCamera == null) return;
        presentation.RenderPreview(parentA, parentB, born, created, elapsed);
        foreach (var root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases(); previewCamera.Render();
    }

    private void Cleanup()
    {
        playing = false;
        presentation?.Dispose(); presentation = null;
        previewCamera = null;
        if (RenderTexture.active == target) RenderTexture.active = null;
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        if (target != null) { target.Release(); DestroyImmediate(target); target = null; }
    }

    // Batch entry point: both variants, fixed frame rate, without Play mode or gameplay singletons.
    public static void CaptureFrames()
    {
        string folder = Environment.GetEnvironmentVariable("WITCHTOWER_FUSION_CAPTURE_DIR");
        if (string.IsNullOrEmpty(folder)) throw new ArgumentException("Set WITCHTOWER_FUSION_CAPTURE_DIR to an output directory.");
        Directory.CreateDirectory(folder);
        int fps = int.TryParse(Environment.GetEnvironmentVariable("WITCHTOWER_FUSION_CAPTURE_FPS"), out int requested) ? Mathf.Clamp(requested, 1, 60) : 30;
        string requestedVariant = Environment.GetEnvironmentVariable("WITCHTOWER_FUSION_CAPTURE_VARIANT");
        bool jpeg = Environment.GetEnvironmentVariable("WITCHTOWER_FUSION_CAPTURE_FORMAT") == "jpg";
        var window = CreateInstance<FusionCinematicPreviewWindow>();
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            for (int variant = 0; variant < 2; variant++)
            {
                // Export separately at 60 fps without retaining both large PNG
                // sequences on disk at once. This does not change playback.
                if (requestedVariant == "normal" && variant != 0 || requestedVariant == "class4" && variant != 1) continue;
                window.mode = variant; window.StartPreview();
                string output = Path.Combine(folder, variant == 1 ? "class4" : "normal"); Directory.CreateDirectory(output);
                int count = Mathf.CeilToInt((window.Duration + .5f) * fps);
                for (int frame = 0; frame < count; frame++)
                {
                    window.elapsed = Mathf.Min(frame / (float)fps, window.Duration); window.Render();
                    RenderTexture.active = window.target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, frame.ToString("D5", CultureInfo.InvariantCulture) + (jpeg ? ".jpg" : ".png")),
                        jpeg ? image.EncodeToJPG(98) : image.EncodeToPNG());
                }
                File.WriteAllText(Path.Combine(output, "capture.txt"), "fps=" + fps + "\nframes=" + count + "\nduration=" + window.Duration.ToString(CultureInfo.InvariantCulture) + "\n");
            }
        }
        finally
        {
            RenderTexture.active = previous;
            DestroyImmediate(image); DestroyImmediate(window);
        }
    }
}
