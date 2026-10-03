using UnityEditor;
using UnityEngine;
using WitchTower.Data;

// Editor-only playback: does not instantiate gameplay managers or write saves.
public sealed class GarzaBossPreviewWindow : EditorWindow
{
    private readonly string[] poses = { "idle", "attack", "hit", "defeat" };
    private readonly string[] labels = { "待機", "攻撃", "被弾", "撃破" };
    private int selected;
    private double started;
    private float speed = 1f;

    [MenuItem("Tools/WitchTower/魔王ガルザ モーション確認")]
    public static void Open() => GetWindow<GarzaBossPreviewWindow>("魔王ガルザ");
    private void OnEnable() { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    private void OnDisable() => EditorApplication.update -= Tick;
    private void Tick() => Repaint();
    private void OnGUI()
    {
        EditorGUILayout.LabelField("魔王ガルザ / 敵専用・捕獲不可", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("セーブ・報酬・召喚処理なしでモーションを確認できます。第6話はストーリープレビューから確認できます。", MessageType.Info);
        int next = GUILayout.Toolbar(selected, labels);
        if (next != selected) { selected = next; started = EditorApplication.timeSinceStartup; }
        speed = EditorGUILayout.Slider("再生速度", speed, .25f, 1.5f);
        if (GUILayout.Button("最初から再生")) started = EditorApplication.timeSinceStartup;
        float elapsed = (float)(EditorApplication.timeSinceStartup - started) * speed;
        if (selected != 0) elapsed %= 2.5f;
        var sprite = GarzaBossPresentation.Frame(poses[selected], elapsed);
        Rect rect = GUILayoutUtility.GetRect(position.width, Mathf.Max(150, position.height - 160));
        EditorGUI.DrawRect(rect, new Color(.06f,.08f,.12f));
        if (sprite != null) GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
    }
}
