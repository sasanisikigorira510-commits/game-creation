using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WitchTower.Battle;
using WitchTower.MasterData;

// Editor-only inspection of shipped resources. No managers, saves, grants or device launch.
public sealed class MonsterMotionPreviewWindow : EditorWindow
{
    private MonsterDataSO[] monsters;
    private int selected, frame;
    private float speed = 1f;
    private bool playing = true, flip;
    private double lastTime, elapsed;
    private Scene scene;
    private Camera camera;
    private RenderTexture target;
    private Image[] images;
    private Text title;
    private readonly List<Sprite>[] clips = new List<Sprite>[3];
    private static readonly BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static MethodInfo Method(string name) => typeof(BattleSceneController).GetMethod(name, Hidden);

    [MenuItem("Tools/WitchTower/モンスターモーション確認")]
    public static void Open() => GetWindow<MonsterMotionPreviewWindow>("モーション確認");
    private void OnEnable()
    {
        var master = Resources.Load<MasterDataRoot>("MasterData/MasterDataRoot");
        monsters = master == null ? Array.Empty<MonsterDataSO>() : master.monsterDataList
            .Where(m => m != null).OrderBy(m => m.monsterId, StringComparer.Ordinal).ToArray();
        EditorApplication.update += Tick;
        lastTime = EditorApplication.timeSinceStartup;
    }
    private void OnDisable() { EditorApplication.update -= Tick; Cleanup(); }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("左から待機・移動・攻撃。実際の戦闘の画像・輪郭基準を使用。戦闘移動・SE・別レイヤーの攻撃エフェクトは含みません。セーブ変更なし。", MessageType.Info);
        if (monsters == null || monsters.Length == 0) return;
        int next = EditorGUILayout.Popup("モンスター", selected, monsters.Select(m => m.monsterName).ToArray());
        if (next != selected || camera == null) { selected = next; Build(); }
        playing = EditorGUILayout.Toggle("再生", playing);
        speed = EditorGUILayout.Slider("再生速度", speed, .1f, 2f);
        flip = EditorGUILayout.Toggle("左右反転", flip);
        if (!playing) frame = EditorGUILayout.IntSlider("コマ", frame, 0, clips.Max(c => c.Count)-1);
        if (GUILayout.Button("画像を再読み込み")) { AssetDatabase.Refresh(); Build(); }
        if (target != null) GUI.DrawTexture(GUILayoutUtility.GetRect(position.width, Mathf.Max(100, position.height-200)), target, ScaleMode.ScaleToFit, false);
    }
    private void Build()
    {
        Cleanup(); elapsed = 0;
        scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("IsolatedMonsterMotionPreview", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 31;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = new Vector2(1200, 500);
        title = Label(root.transform, "", new Vector2(0, 215), new Vector2(1190, 38));
        images = new Image[3];
        var monster = monsters[selected];
        clips[0] = BattleVisualResolver.ResolveMonsterIdleSprites(monster);
        clips[1] = BattleVisualResolver.ResolveMonsterMoveSprites(monster);
        clips[2] = BattleVisualResolver.ResolveMonsterAttackSprites(monster);
        for (int p = 0; p < 3; p++)
        {
            var anchor = new GameObject("PoseAnchor", typeof(RectTransform)); anchor.transform.SetParent(root.transform, false);
            ((RectTransform)anchor.transform).anchoredPosition = new Vector2((p-1)*400, 0);
            var go = new GameObject("Sprite", typeof(RectTransform), typeof(Image)); go.transform.SetParent(anchor.transform, false);
            images[p] = go.GetComponent<Image>(); images[p].raycastTarget = false;
            Label(root.transform, new[] { "IDLE", "MOVE", "ATTACK" }[p], new Vector2((p-1)*400, -190), new Vector2(300,35));
        }
        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
        var cameraGo = new GameObject("PreviewCamera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, scene);
        camera = cameraGo.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
        camera.orthographic = true; camera.orthographicSize = 250; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
        camera.transform.position = new Vector3(0,0,-1000); camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f,.09f,.12f);
        target = new RenderTexture(1200,500,24); camera.targetTexture = target; canvas.worldCamera = camera;
        Render(0);
    }
    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = 20; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
    }
    // Uses exactly the battle renderer's pose measurement and reference-frame selection.
    public static void ApplyFrame(Image image, MonsterDataSO monster, BattleVisualPose pose, Sprite sprite, IReadOnlyList<Sprite> clip, IReadOnlyList<Sprite> idle)
    {
        image.sprite = sprite;
        var mode = Method("ResolveAllyPreviewMeasurementMode").Invoke(null, new object[] { monster, pose });
        bool useIdle = pose == BattleVisualPose.Attack && (bool)Method("ShouldUseAttackBodyMetrics").Invoke(null,new object[] { monster });
        Method("ApplyPreviewVisualLayout").Invoke(null,new object[] { image,new Vector2(230,260),Vector2.zero,useIdle ? idle : clip,mode });
    }
    private void Render(int? fixedFrame = null)
    {
        var monster = monsters[selected];
        title.text = monster.monsterId + (fixedFrame.HasValue ? "  / frame " + fixedFrame : "");
        for (int p=0;p<3;p++)
        {
            var clip = clips[p]; if (clip.Count == 0) continue;
            float attackDuration = (float)typeof(BattleSceneController).GetField("AttackVisualDuration", Hidden).GetRawConstantValue();
            float fps = p == 0 ? 4f : p == 1 ? 8f : clip.Count / attackDuration;
            int index = fixedFrame ?? (playing ? (int)(elapsed*fps)%clip.Count : frame);
            images[p].rectTransform.localScale = new Vector3(flip ? -1 : 1,1,1);
            ApplyFrame(images[p],monster,(BattleVisualPose)p,clip[Mathf.Clamp(index,0,clip.Count-1)],clip,clips[0]);
        }
        Canvas.ForceUpdateCanvases(); camera.Render();
    }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (playing) elapsed += (now-lastTime)*speed;
        lastTime = now;
        if (camera != null) { Render(); Repaint(); }
    }
    private void Cleanup()
    {
        camera = null;
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        if (target != null) { target.Release(); DestroyImmediate(target); target = null; }
    }
    public static void CaptureCatalog()
    {
        string output = Environment.GetEnvironmentVariable("WITCHTOWER_MOTION_CAPTURE");
        if (string.IsNullOrEmpty(output)) throw new ArgumentException("Set WITCHTOWER_MOTION_CAPTURE");
        Directory.CreateDirectory(output);
        var preview = CreateInstance<MonsterMotionPreviewWindow>();
        var pixels = new Texture2D(1200,500,TextureFormat.RGB24,false);
        var old = RenderTexture.active;
        try
        {
            for (int m=0;m<preview.monsters.Length;m++)
            {
                preview.selected=m; preview.Build();
                for(int f=0;f<4;f++)
                {
                    preview.Render(f); RenderTexture.active=preview.target;
                    pixels.ReadPixels(new Rect(0,0,1200,500),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,preview.monsters[m].monsterId+"_"+f+".png"),pixels.EncodeToPNG());
                }
            }
        }
        finally { RenderTexture.active=old; DestroyImmediate(pixels); DestroyImmediate(preview); }
    }
}
