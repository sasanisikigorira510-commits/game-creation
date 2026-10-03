using System;
using UnityEditor;
using UnityEngine;
using WitchTower.Data;
using WitchTower.UI;

// Editor-only entry point. Reading scripts and preview playback do not advance story progress.
public sealed class StoryDialoguePreviewWindow : EditorWindow
{
    [SerializeField] private int selected;
    private Vector2 scroll;
    private string playbackMessage;
    private bool playbackRejected;
    private GUIStyle bodyStyle;

    [MenuItem("Tools/WitchTower/ストーリー プレビュー")]
    public static void Open()
    {
        var window = GetWindow<StoryDialoguePreviewWindow>("ストーリー プレビュー");
        window.minSize = new Vector2(400f, 450f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        playbackMessage = null;
        playbackRejected = false;
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "開発用の原稿確認と会話プレビューです。初回召喚の前後と全6話を確認できます。" +
            "このプレビューは召喚・報酬付与・既読更新・進行データの保存を行いません。",
            MessageType.Info);

        var entries = StoryDialogueCatalog.All;
        if (entries == null || entries.Length == 0)
        {
            EditorGUILayout.HelpBox("ストーリー原稿が見つかりません。", MessageType.Warning);
            return;
        }

        var options = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            options[i] = entries[i].Title + " / " + entries[i].Subtitle;
        selected = Mathf.Clamp(selected, 0, entries.Length - 1);
        int nextSelection = EditorGUILayout.Popup("表示する会話", selected, options);
        if (nextSelection != selected)
        {
            selected = nextSelection;
            scroll = Vector2.zero;
            playbackMessage = null;
        }

        var story = entries[selected];
        EditorGUILayout.LabelField(story.Title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(story.Subtitle, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("event_id", story.EventId);
        EditorGUILayout.LabelField("発生タイミング", story.UnlockFloor > 0
            ? "通算 " + story.UnlockFloor + " 層の初回クリア後、ホームへ帰還"
            : "初回召喚の紹介会話（本編とは別枠）");

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorApplication.isPaused))
        {
            if (GUILayout.Button("Game ビューで再生（進行・既読を変更しない）", GUILayout.Height(30f)))
                StartPreview(story.EventId);
        }
        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("本文はこの画面で確認できます。ゲーム内表示を確認するには Play モードで再生してください。", MessageType.None);
        else if (EditorApplication.isPaused)
            EditorGUILayout.HelpBox("ゲームの一時停止を解除すると、会話プレビューを再生できます。", MessageType.None);
        if (!string.IsNullOrEmpty(playbackMessage))
            EditorGUILayout.HelpBox(playbackMessage, playbackRejected ? MessageType.Warning : MessageType.Info);

        EditorGUILayout.Space(4f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("振り返り", EditorStyles.boldLabel);
        DrawSelectableText(story.Summary);
        EditorGUILayout.Space(8f);
        int lineNumber = 0;
        foreach (var line in story.Lines)
        {
            EditorGUILayout.LabelField("[" + (++lineNumber).ToString("D2") + "] " + line.Speaker, EditorStyles.boldLabel);
            DrawSelectableText(line.Text);
            EditorGUILayout.Space(9f);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectableText(string text)
    {
        if (bodyStyle == null)
            bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };
        string value = text ?? string.Empty;
        float height = bodyStyle.CalcHeight(new GUIContent(value), Mathf.Max(200f, position.width - 40f));
        EditorGUILayout.SelectableLabel(value, bodyStyle, GUILayout.Height(Mathf.Max(EditorGUIUtility.singleLineHeight, height)));
    }

    private void StartPreview(string eventId)
    {
        bool started = StoryDialogueController.TryShowPreview(eventId, () =>
        {
            if (this == null) return;
            playbackMessage = "プレビューが終了しました。進行・既読は変更していません。";
            playbackRejected = false;
            Repaint();
        });
        playbackRejected = !started;
        playbackMessage = started
            ? "Game ビューで会話を再生中です。画面の次へ・スキップで確認してください。"
            : "現在は再生できません。進行中の会話やほかのプレビューを終了してから、もう一度お試しください。";
        if (started)
        {
            Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) GetWindow(gameView).Show();
        }
    }
}
